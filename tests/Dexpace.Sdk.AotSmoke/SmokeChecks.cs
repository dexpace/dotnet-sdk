// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Text;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Operations;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.AotSmoke;

/// <summary>
/// Light checks over the current public surface, each run inside the published NativeAOT binary: the HTTP value
/// models, a JSON round trip through the default pipeline with a source-generated context, and the reference
/// transport's request/response mapping over an in-process handler (no socket; the loopback round trip is phase
/// 7's extension).
/// </summary>
internal static class SmokeChecks
{
    private static readonly Uri s_endpoint = new("https://smoke.example.test/widgets");

    public static async Task<int> RunAllAsync()
    {
        try
        {
            CheckValueModels();
            CheckPhase2aModels();
            await CheckPipelineJsonRoundTripAsync();
            await CheckReferenceTransportAsync();
            await CheckBridgesAndDelegateClientsAsync();
            CheckSerdeProfiles();
            CheckOperationDescriptor();
            await CheckBodiesAndIoAsync();
            await CheckPhase3bBodiesAsync();
            CheckExecutionContext();
            await CheckPhase4bRecoveryAsync();
            await CheckPipelineReworkAsync();
            await CheckHttpLoggingAsync();
        }
        catch (SmokeFailureException failure)
        {
            await Console.Error.WriteLineAsync($"aot-smoke: FAILED: {failure.Message}");
            return 1;
        }

        Console.WriteLine("aot-smoke: all checks passed");
        return 0;
    }

    private static void CheckValueModels()
    {
        var mediaType = MediaType.Parse("application/json; charset=\"utf-8\"");
        Expect(mediaType.Type == "application" && mediaType.Subtype == "json", "MediaType.Parse type/subtype");

        var headers = new Headers.Builder().Add("X-Smoke", "one").Add("x-smoke", "two").Build();
        Expect(headers.GetAll("X-SMOKE").Count == 2, "Headers is a case-insensitive multimap");

        Expect(Status.FromCode(200) == Status.Ok, "Status.FromCode returns the well-known instance");
        Expect(Method.Get.Name == "GET", "Method.Get wire name");
    }

    // Phase 2a: every new public model type, touched in an AOT-safe way (no reflection).
    private static void CheckPhase2aModels()
    {
        var options = RequestOptions.Empty with { Timeout = TimeSpan.FromSeconds(5), MaxRetries = 2 };
        Expect(options.WithTag("k", "v").Tags["k"] == "v" && options.Tags.Count == 0, "RequestOptions with/WithTag");

        var query = Query.Parse("?a=1&a=2&flag&q=a%20b");
        Expect(query.GetAll("a").Count == 2 && query.Get("flag") == string.Empty, "Query.Parse");
        Expect(Query.Parse(query.Encode()) == query && query.Encode() == "a=1&a=2&flag=&q=a%20b", "Query encode round trip");

        var name = HttpHeaderName.Of("X-Smoke-Typed");
        var headers = Headers.Empty.With(name, "1").Set("Other", "2");
        Expect(headers.Get("x-smoke-typed") == "1" && headers.Names[0] == "X-Smoke-Typed", "Headers typed overloads and casing");
        Expect(headers == Headers.Empty.With("x-smoke-typed", "1").With("other", "2"), "Headers value equality");
        Expect(HttpHeaderSyntax.IsValidName("X-A") && !HttpHeaderSyntax.IsValidOutboundValue("a\r\nb"), "HttpHeaderSyntax");

        var request = Request.Post(s_endpoint.OriginalString, RequestBody.FromString("{}"))
            .WithHeader(name, "1")
            .WithUrl(new Uri("https://smoke.example.test/other"));
        var same = Request.Post("https://smoke.example.test/other", RequestBody.FromString("{}")).WithHeader("x-smoke-typed", "1");
        Expect(request == same && request.WithoutBody().Body is null, "Request With* and equality");
        Expect(request.ToString().StartsWith("POST https://smoke.example.test/other", StringComparison.Ordinal), "Request.ToString");

        using var response = new Response(request, Status.Ok, Protocol.Http2, reasonPhrase: "OK");
        using var derived = response.WithBody(ResponseBody.FromBytes("x"u8.ToArray()));
        Expect(derived.Request == request && derived.ReasonPhrase == "OK" && derived.IsSuccess && !derived.IsError, "Response.WithBody");

        var strong = ETag.Parse("\"abc\"");
        var range = HttpRange.Parse("Bytes=0-9");
        Expect(strong == ETag.Strong("abc") && range == HttpRange.Bounded(0, 10), "ETag and HttpRange");
        var conditions = new RequestConditions { IfMatch = [strong!], IfModifiedSince = DateTimeOffset.UnixEpoch };
        var conditional = conditions.ApplyTo(request);
        Expect(conditional.Headers.Get("If-Match") == "\"abc\"" && conditional.Headers.Contains(HttpHeaderName.WellKnown.IfModifiedSince), "RequestConditions.ApplyTo");
        Expect(Status.TryGetKnown(404, out var known) && known.IsError, "Status.TryGetKnown");
    }

    private static async Task CheckPipelineJsonRoundTripAsync()
    {
        var serde = new SystemTextJsonSerde(SmokeJsonContext.Default);
        await using var transport = new EchoTransport();
        using var pipeline = DexpacePipeline.CreateDefault(transport);

        var request = Request.Post(s_endpoint.OriginalString, RequestBody.FromValue(new Widget("gear", 9), serde));
        using var response = await pipeline.SendAsync(request, new DexpaceClientOptions(), CancellationToken.None);
        await response.EnsureSuccessAsync();

        var echoed = await response.Body.ReadValueAsync<Widget>(serde);
        Expect(echoed == new Widget("gear", 9), "JSON body survives the pipeline round trip");
        Expect(transport.LastUserAgent?.StartsWith("dexpace-", StringComparison.Ordinal) == true,
            "the default pipeline stamps the User-Agent");
    }

    private static async Task CheckReferenceTransportAsync()
    {
        using var handler = new CannedHandler();
        using var client = new HttpClient(handler);
        await using var transport = new SystemNetHttpClient(client);

        var request = Request.Post(s_endpoint.OriginalString, RequestBody.FromString("ping", CommonMediaTypes.TextPlain));
        using var response = await transport.ExecuteAsync(request);

        Expect(response.Status == Status.Created, "transport maps the status code");
        Expect(response.Headers.Get("X-Echo-Method") == "POST", "transport maps response headers");
        Expect(await response.Body.ReadAsStringAsync() == "pong", "transport streams the response body");
    }

    // Phase 2b: both bridges and both DelegateHttpClient forms, over an in-process transport (no reflection).
    private static async Task CheckBridgesAndDelegateClientsAsync()
    {
        var request = Request.Get(s_endpoint.OriginalString);

        // AsAsync on TaskScheduler.Default (a dedicated thread through LongRunning) and the option-less extension.
        using var blocking = DelegateHttpClient.CreateBlocking((r, _, _) => new Response(r, Status.Ok, Protocol.Http11));
        await using var asAsync = blocking.AsAsync(TaskScheduler.Default);
        using var viaAsAsync = await asAsync.ExecuteAsync(request);
        Expect(viaAsAsync.Status == Status.Ok, "AsAsync(TaskScheduler.Default) round trip");

        // AsBlocking over an async delegate transport, passing the options and the token through.
        var options = new RequestOptions { MaxRetries = 1 };
        RequestOptions? seen = null;
        await using var asyncClient = DelegateHttpClient.Create((r, o, _) =>
        {
            seen = o;
            return Task.FromResult(new Response(r, Status.Accepted, Protocol.Http11));
        });
        using var viaBlocking = asyncClient.AsBlocking();
        using var response = viaBlocking.Execute(request, options, CancellationToken.None);
        Expect(response.Status == Status.Accepted && ReferenceEquals(seen, options), "AsBlocking round trip");

        using var optionless = DelegateHttpClient.CreateBlocking((r, o, _) => new Response(r, ReferenceEquals(o, RequestOptions.Empty) ? Status.Ok : Status.BadRequest, Protocol.Http11));
        using var optionlessResponse = optionless.Execute(request);
        Expect(optionlessResponse.Status == Status.Ok, "DelegateHttpClient.CreateBlocking with the option-less call");
    }

    // Phase 2b: the derived serialization profiles over the source-generated codec, and the serde failure hierarchy.
    private static void CheckSerdeProfiles()
    {
        var serde = new SystemTextJsonSerde(SmokeJsonContext.Default);
        var widget = new Widget("gear", 9);

        var bytes = serde.SerializeToUtf8Bytes(widget);
        Expect(serde.SerializeToString(widget) == "{\"Name\":\"gear\",\"Size\":9}", "SerializeToString");

        var fixedBuffer = new byte[bytes.Length + 2];
        var count = serde.Serialize(fixedBuffer.AsSpan(2), widget);
        Expect(count == bytes.Length && fixedBuffer.AsSpan(2).SequenceEqual(bytes), "fixed-buffer Serialize");

        var overflow = false;
        try
        {
            _ = serde.Serialize(new byte[1], widget);
        }
        catch (ArgumentOutOfRangeException)
        {
            overflow = true;
        }

        Expect(overflow, "fixed-buffer Serialize overflow");

        var caught = false;
        try
        {
            _ = serde.Deserialize<Widget>("{ not json"u8);
        }
        catch (SerdeException ex) when (ex is DeserializationException)
        {
            caught = true;
        }

        Expect(caught, "a DeserializationException is caught as SerdeException");
    }

    // Phase 2b: OperationDescriptor.BuildRequest with a path parameter, a query and a base address.
    private static void CheckOperationDescriptor()
    {
        var descriptor = new OperationDescriptor
        {
            Method = Method.Get,
            PathTemplate = "/pets/{id}",
            PathParameters = ImmutableDictionary<string, string>.Empty.Add("id", "a/b"),
            Query = new Query.Builder().Add("limit", "10").Build(),
        };

        var request = descriptor.BuildRequest(new Uri("https://smoke.example.test/v1?sig=abc"));

        Expect(
            request.Url.AbsoluteUri == "https://smoke.example.test/v1/pets/a%2Fb?sig=abc&limit=10",
            "OperationDescriptor.BuildRequest composition");
    }

    // Phase 3a: the sync body twins, the exact-length stream body, the bounded materialisation and BodyTooLargeException,
    // all through the public surface (the internal helpers are reached through these members; no reflection).
    private static async Task CheckBodiesAndIoAsync()
    {
        using var sink = new MemoryStream();
        RequestBody.FromBytes("abc"u8.ToArray()).WriteTo(sink);
        Expect(sink.ToArray().AsSpan().SequenceEqual("abc"u8), "RequestBody.FromBytes WriteTo");

        // A known-length FromStream writes exactly the declared bytes in both forms.
        using var syncExact = new MemoryStream();
        RequestBody.FromStream(new MemoryStream("0123456789"u8.ToArray()), contentLength: 4).WriteTo(syncExact);
        using var asyncExact = new MemoryStream();
        await RequestBody.FromStream(new MemoryStream("0123456789"u8.ToArray()), contentLength: 4).WriteToAsync(asyncExact);
        Expect(syncExact.ToArray().AsSpan().SequenceEqual("0123"u8) && asyncExact.ToArray().AsSpan().SequenceEqual("0123"u8),
            "known-length FromStream writes exactly its length in both forms");

        // A short source is caught as EndOfStreamException.
        var caughtShort = false;
        try
        {
            RequestBody.FromStream(new MemoryStream("ab"u8.ToArray()), contentLength: 4).WriteTo(new MemoryStream());
        }
        catch (EndOfStreamException)
        {
            caughtShort = true;
        }

        Expect(caughtShort, "a short known-length source throws EndOfStreamException");

        var replayable = RequestBody.FromStream(new MemoryStream("xyz"u8.ToArray())).ToReplayable();
        using var again = new MemoryStream();
        replayable.WriteTo(again);
        replayable.WriteTo(again);
        Expect(replayable.IsReplayable && again.Length == 6, "ToReplayable round trip is replayable");

        var response = ResponseBody.FromBytes("hello"u8.ToArray(), MediaType.Parse("text/plain; charset=utf-8"));
        Expect(response.ReadAsString() == "hello", "ResponseBody.ReadAsString");
        Expect(ResponseBody.FromBytes("hello"u8.ToArray()).ReadAsBytes().Length == 5, "ResponseBody.ReadAsBytes");

        // A declared length above the cap is refused before any read, so nothing near 64 MiB is allocated.
        var refused = false;
        try
        {
            ResponseBody.FromStream(new MemoryStream(), contentLength: ResponseBody.DefaultMaxMaterializedBytes + 1).ReadAsBytes();
        }
        catch (BodyTooLargeException)
        {
            refused = true;
        }

        Expect(refused, "BodyTooLargeException is raised through the public path");
    }

    // Phase 4a: mint, promote twice, look up, close, look up again; no reflection.
    private static void CheckExecutionContext()
    {
        var key = CallKey.Next();
        var request = new Request(Method.Get, s_endpoint);
        using var response = new Response(request, Status.Ok, Protocol.Http11);
        var dispatch = new DispatchContext(InstrumentationContext.None, key);
        var exchange = dispatch.PromoteToRequest(request, "smoke").PromoteToExchange(response);

        Expect(DexpaceCallContexts.TryGet(key, out var found) && ReferenceEquals(found, exchange), "TryGet returns the exchange link");
        exchange.Close();
        Expect(!DexpaceCallContexts.TryGet(key, out _), "TryGet is absent after Close");
        exchange.Close();
        Expect(InstrumentationContext.None.StartActivity("smoke") is null, "None starts no activity");
    }

    // Phase 3b: the file, form and multipart bodies, the seekable promotion and the latched dispose, inside the published binary.
    private static async Task CheckPhase3bBodiesAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "dexpace-aot-smoke-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllBytesAsync(path, "0123456789abcdef"u8.ToArray());
        try
        {
            var file = RequestBody.FromFile(path, offset: 4, count: 6);
            using var fileAsync = new MemoryStream();
            await file.WriteToAsync(fileAsync);
            using var fileSync = new MemoryStream();
            file.WriteTo(fileSync);
            Expect(
                file.IsReplayable && file.ContentLength == 6
                    && fileAsync.ToArray().AsSpan().SequenceEqual("456789"u8) && fileSync.ToArray().AsSpan().SequenceEqual("456789"u8),
                "FromFile writes its exact range in both forms");
        }
        finally
        {
            File.Delete(path);
        }

        // Design fact 6: the WHATWG serializer, not RFC 3986 and not WebUtility.
        using var form = new MemoryStream();
        RequestBody.FromForm([new("a b*-._~!'()\u00E9", "x")]).WriteTo(form);
        Expect(
            form.ToArray().AsSpan().SequenceEqual("a+b*-._%7E%21%27%28%29%C3%A9=x"u8),
            "FromForm encodes with the WHATWG serializer");

        var multipart = RequestBody.Multipart(
            [new MultipartPart("field", RequestBody.FromBytes("value"u8.ToArray())), new MultipartPart("file", RequestBody.FromBytes("A"u8.ToArray(), MediaType.Parse("text/plain")), "a.txt")],
            "B");
        using var multipartSink = new MemoryStream();
        await multipart.WriteToAsync(multipartSink);
        Expect(
            multipart.ContentLength == multipartSink.Length
                && Encoding.UTF8.GetString(multipartSink.ToArray()).StartsWith("--B\r\nContent-Disposition: form-data; name=\"field\"\r\n\r\nvalue\r\n--B\r\n", StringComparison.Ordinal)
                && Encoding.UTF8.GetString(multipartSink.ToArray()).EndsWith("--B--\r\n", StringComparison.Ordinal),
            "Multipart frames its parts and reports the exact length");

        var seekable = RequestBody.FromStream(new MemoryStream("seek"u8.ToArray()), contentLength: 4);
        using var seekFirst = new MemoryStream();
        using var seekSecond = new MemoryStream();
        seekable.WriteTo(seekFirst);
        seekable.WriteTo(seekSecond);
        Expect(seekable.IsReplayable && seekFirst.Length == 4 && seekSecond.Length == 4, "a seekable known-length stream is replayable");

        var counting = new ReleaseCountingBody();
        var smoked = new Response(Request.Get("https://smoke.example.test/"), Status.Ok, Protocol.Http11, body: counting);
        smoked.Dispose();
        await smoked.DisposeAsync();
        smoked.Dispose();
        Expect(counting.Releases == 1, "a Response disposed three times releases its body once");

        var gone = ResponseBody.FromStream(new MemoryStream());
        await gone.DisposeAsync();
        var closed = false;
        try
        {
            await gone.OpenReadAsync();
        }
        catch (StreamClosedException)
        {
            closed = true;
        }

        Expect(closed, "a stream body opened after dispose throws StreamClosedException");
    }

    // Phase 4b: the closed outcome, a dispatch through the error-mapping step, and the suppressed trail on a foreign
    // exception through Exception.Data. No reflection anywhere on these paths.
    private static async Task CheckPhase4bRecoveryAsync()
    {
        using var okResponse = new Response(Request.Get("https://smoke.example.test/"), Status.Ok, Protocol.Http11);
        Outcome ok = new Outcome.Success(okResponse);
        Outcome bad = new Outcome.Failure(new IOException("smoke"));
        Expect(
            ok.Match(_ => "s", _ => "f") == "s" && bad.Match(_ => "s", _ => "f") == "f" && ok.IsSuccess && bad.IsFailure,
            "Outcome folds to exactly one branch");

        var dispatcher = new RecoveryDispatcher(
            RequestRecoveryChain.Empty,
            new ResponseRecoveryChain([ErrorMappingStep.Instance], []));
        await using var transport = new ServiceUnavailableTransport();
        HttpResponseException? mapped = null;
        try
        {
            await dispatcher.DispatchAsync(transport, Request.Get("https://smoke.example.test/"), RequestOptions.Empty);
        }
        catch (HttpResponseException ex)
        {
            mapped = ex;
        }

        Expect(
            mapped is not null && mapped.Status.Code == 503
                && Encoding.UTF8.GetString(await mapped.Response.Body.ReadAsBytesAsync()) == "unavailable",
            "a dispatch through ErrorMappingStep throws HttpResponseException over the buffered body");

        var primary = new InvalidOperationException("primary");
        var secondary = new IOException("secondary");
        var causes = 0;
        foreach (var cause in ExceptionFacts.EnumerateCauses(new AggregateException(primary)))
        {
            causes += cause is null ? 0 : 1;
        }

        ExceptionTrail.AddSuppressed(primary, secondary);
        Expect(
            ExceptionTrail.GetSuppressed(primary).Count == 1 && ReferenceEquals(ExceptionTrail.GetSuppressed(primary)[0], secondary)
                && causes == 2,
            "ExceptionTrail attaches to a foreign exception through Data");
    }

    // Phase 4c: a synchronous Send through the default pipeline, ErrorMappingPolicy over a 503, and a pipeline nested as
    // another pipeline's transport (no reflection).
    private static async Task CheckPipelineReworkAsync()
    {
        var request = Request.Get("https://smoke.example.test/");

        using var syncHandler = new CannedHandler();
        using var syncClient = new HttpClient(syncHandler);
        using var syncTransport = new SystemNetHttpClient(syncClient);
        using var defaultPipeline = DexpacePipeline.CreateDefault(syncTransport);
        using var syncResponse = defaultPipeline.Send(request, CancellationToken.None);
        Expect(
            syncResponse.Status == Status.Created,
            "a synchronous Send through CreateDefault reaches SystemNetHttpClient's synchronous terminal");

        using var notFoundHandler = new CannedHandler(HttpStatusCode.NotFound);
        using var notFoundClient = new HttpClient(notFoundHandler);
        await using var failing = new SystemNetHttpClient(notFoundClient);
        using var mapping = new PipelineBuilder().Add(new ErrorMappingPolicy()).Build(failing);
        HttpResponseException? mapped = null;
        try
        {
            using var unexpected = await mapping.SendAsync(request, CancellationToken.None);
        }
        catch (HttpResponseException ex)
        {
            mapped = ex;
        }

        Expect(
            mapped is not null && mapped.Status.Code == 404
                && Encoding.UTF8.GetString(await mapped.Response.Body.ReadAsBytesAsync()) == "pong",
            "ErrorMappingPolicy maps a 404 response to HttpResponseException with a readable buffered body");

        using var innerTransport = new SyncCapableTransport();
        using var inner = new PipelineBuilder().Build(innerTransport);
        using var outer = PipelineBuilder.Nest(inner).Build();
        IAsyncHttpClient seam = outer;
        var options = new RequestOptions { MaxRetries = 1 };
        using var nested = await seam.ExecuteAsync(request, options, CancellationToken.None);
        Expect(
            nested.Status == Status.Ok && ReferenceEquals(innerTransport.LastOptions, options),
            "an HttpPipeline used as an IAsyncHttpClient under Nest threads the RequestOptions through");
    }

    // Phase 5b: body-level logging over CreateDefault, with a JSON and a binary body (OBS-34, OBS-36 to OBS-38, OBS-16 to OBS-18).
    private static async Task CheckHttpLoggingAsync()
    {
        var logger = new SmokeLogger();
        var transport = new LoggingProbeTransport();
        using var pipeline = DexpacePipeline.CreateDefault(transport, logger: logger);
        var options = new DexpaceClientOptions { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Body } };
        var headers = new Headers.Builder().Add("Authorization", "Bearer smoke-secret").Add("Accept", "application/json").Build();

        var jsonRequest = new Request(Method.Post, new Uri("https://smoke.example.test/logs?token=smoke-secret"), headers, RequestBody.FromString("{\"in\":1}"));
        using var json = await pipeline.SendAsync(jsonRequest, options, CancellationToken.None);
        Expect(
            Encoding.UTF8.GetString(await json.Body.ReadAsBytesAsync()) == "{\"out\":2}",
            "the consumer reads every byte of a body-level logged JSON response");

        using var binary = await pipeline.SendAsync(Request.Get("https://smoke.example.test/binary"), options, CancellationToken.None);
        Expect((await binary.Body.ReadAsBytesAsync()).Length == 4, "the consumer reads every byte of a body-level logged binary response");

        var responses = logger.Events.FindAll(e => e.Id == DexpaceLogEvents.HttpResponseId);
        Expect(responses.Count == 2, "one http.response event per call");
        Expect(responses[0].Get(DexpaceLogKeys.HttpResponseBodyPreview) as string == "{\"out\":2}", "the JSON response preview is the JSON text");
        Expect(responses[0].Get(DexpaceLogKeys.HttpRequestBodyPreview) as string == "{\"in\":1}", "the request preview rides on the response event");
        Expect(responses[1].Get(DexpaceLogKeys.HttpResponseBodyPreview) as string == "[binary 4 bytes captured]", "the binary response preview is the size-only marker");

        var request = logger.Events.Find(e => e.Id == DexpaceLogEvents.HttpRequestId);
        Expect(request is not null && request.Get("http.request.header.authorization") as string == "REDACTED", "Authorization is logged as REDACTED");
        Expect(request!.Get(DexpaceLogKeys.UrlFull) as string == "https://smoke.example.test/logs?token=***", "url.full carries no secret value");
        Expect(logger.Events.TrueForAll(e => !e.Text.Contains("smoke-secret", StringComparison.Ordinal)), "no event carries the secret");

        Expect(new UrlRedactor().RedactHeaderValue("/cb?code=smoke-secret") == "/cb?***", "UrlRedactor.RedactHeaderValue");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition)
        {
            throw new SmokeFailureException(what);
        }
    }

    /// <summary>An empty body that counts how many times it was released.</summary>
    private sealed class ReleaseCountingBody : ResponseBody
    {
        private int _releases;

        public int Releases => Volatile.Read(ref _releases);

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _releases);
            base.Dispose(disposing);
        }
    }

    /// <summary>An in-process dual-interface transport that counts which member was reached.</summary>
    private sealed class SyncCapableTransport : IAsyncHttpClient, IHttpClient
    {
        private int _sync;
        private int _async;

        public int SyncCalls => Volatile.Read(ref _sync);

        public int AsyncCalls => Volatile.Read(ref _async);

        public RequestOptions? LastOptions { get; private set; }

        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _async);
            LastOptions = options;
            return Task.FromResult(new Response(request, Status.Ok, Protocol.Http11));
        }

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sync);
            LastOptions = options;
            return new Response(request, Status.Ok, Protocol.Http11);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>An in-process transport that answers every request with a 503 and a small body.</summary>
    private sealed class ServiceUnavailableTransport : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new Response(request, Status.ServiceUnavailable, Protocol.Http11, body: ResponseBody.FromBytes("unavailable"u8.ToArray())));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>An in-process transport that echoes the request body back as a 200 JSON response.</summary>
    private sealed class EchoTransport : IAsyncHttpClient
    {
        public string? LastUserAgent { get; private set; }

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            LastUserAgent = request.Headers.Get("User-Agent");
            using var buffer = new MemoryStream();
            if (request.Body is { } body)
            {
                await body.WriteToAsync(buffer, cancellationToken);
            }

            return new Response(request, Status.Ok, Protocol.Http11, body: ResponseBody.FromBytes(buffer.ToArray(), CommonMediaTypes.ApplicationJson));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A handler that answers every request with 201 "pong", echoing the method in a header.</summary>
    private sealed class CannedHandler(HttpStatusCode status = HttpStatusCode.Created) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Build(request));

        private HttpResponseMessage Build(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent("pong", Encoding.UTF8, "text/plain"),
                RequestMessage = request,
            };
            response.Headers.Add("X-Echo-Method", request.Method.Method);
            return response;
        }
    }

    /// <summary>An ILogger that keeps each event's id, key/value state and flattened text.</summary>
    private sealed class SmokeLogger : ILogger
    {
        public List<SmokeEvent> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var pairs = new List<KeyValuePair<string, object?>>();
            var text = new StringBuilder(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> list)
            {
                foreach (var pair in list)
                {
                    pairs.Add(pair);
                    text.Append(' ').Append(pair.Value);
                }
            }

            Events.Add(new SmokeEvent(eventId.Id, pairs, text.ToString()));
        }
    }

    private sealed record SmokeEvent(int Id, List<KeyValuePair<string, object?>> State, string Text)
    {
        public object? Get(string key) => State.Find(p => p.Key == key).Value;
    }

    /// <summary>An in-process transport that writes the request body and answers JSON or binary by path.</summary>
    private sealed class LoggingProbeTransport : IAsyncHttpClient
    {
        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            if (request.Body is not null)
            {
                await request.Body.WriteToAsync(Stream.Null, cancellationToken);
            }

            var body = request.Url.AbsolutePath == "/binary"
                ? ResponseBody.FromBytes(new byte[] { 0, 1, 2, 255 }, MediaType.Parse("application/octet-stream"))
                : ResponseBody.FromBytes("{\"out\":2}"u8.ToArray(), MediaType.Parse("application/json"));
            return new Response(request, Status.Ok, Protocol.Http11, body: body);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
