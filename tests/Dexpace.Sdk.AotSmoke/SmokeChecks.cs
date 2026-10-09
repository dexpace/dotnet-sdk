// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Net;
using System.Text;
using Dexpace.Sdk.Core.Auth;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace Dexpace.Sdk.AotSmoke;

/// <summary>
/// Light checks over the current public surface, each run inside the published NativeAOT binary: the HTTP value
/// models, a JSON round trip through the default pipeline with a source-generated context, and the reference
/// transport's request/response mapping over an in-process handler (no socket; the loopback round trip is phase
/// 7's extension).
/// </summary>
internal static partial class SmokeChecks
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
            await CheckPhase5aConfigurationAsync();
            await CheckTracingAndMetricsAsync();
            await CheckPhase6aRetryAsync();
            await CheckPhase6bRedirectAsync();
            await CheckPhase6cAuthAsync();
            await CheckPhase7aSerdeAsync();
            await CheckServerSentEventsAsync();
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
    // Phase 5c: the operation span, its attempt child and the two instruments, observed from public listeners only.
    private static async Task CheckTracingAndMetricsAsync()
    {
        var request = Request.Get("https://smoke.example.test/traced");
        var started = new List<Activity>();
        var durations = 0;
        long inFlight = 0;

        using var handler = new CannedHandler();
        using var client = new HttpClient(handler);
        using var transport = new SystemNetHttpClient(client);
        using var pipeline = DexpacePipeline.CreateDefault(transport);

        using (var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dexpace.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = started.Add,
        })
        using (var meters = new MeterListener())
        {
            ActivitySource.AddActivityListener(activities);
            meters.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Dexpace.Sdk")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meters.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            {
                if (instrument.Name == "http.client.request.duration")
                {
                    durations++;
                }
            });
            meters.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            {
                if (instrument.Name == "http.client.active_requests")
                {
                    inFlight += value;
                }
            });
            meters.Start();

            using var traced = await pipeline.SendAsync(request, CancellationToken.None);
        }

        var operation = started.Count == 2 ? started[0] : null;
        Expect(
            operation is { Kind: ActivityKind.Internal } && started[1].Kind == ActivityKind.Client && started[1].ParentId == operation.Id,
            "an ActivityListener sees one Internal operation span with one Client attempt child");
        Expect(durations == 1 && inFlight == 0, "a MeterListener sees one duration measurement and a balanced active-requests counter");

        started.Clear();
        durations = 0;
        using var untraced = await pipeline.SendAsync(request, CancellationToken.None);
        Expect(started.Count == 0 && durations == 0, "with the listeners disposed there is no span and no measurement");
    }

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

    // Phase 5a (CFG-8, CFG-9, CFG-15 to CFG-18, CFG-22 to CFG-25, CFG-29 to CFG-31, CFG-36): the configuration surface
    // under NativeAOT, including the hand-written glob matcher behind the proxy bypass list.
    private static async Task CheckPhase5aConfigurationAsync()
    {
        var options = new DexpaceClientOptions { BaseAddress = new Uri("https://api.example.test/v1?sig=secret") };
        var derived = options with { Retry = options.Retry with { MaxRetryAttempts = 5 } };
        Expect(options.Retry.MaxRetryAttempts == 2 && derived.Retry.MaxRetryAttempts == 5, "options derive with `with`");
        Expect(!options.ToString().Contains("secret", StringComparison.Ordinal), "options ToString redacts the base address");
        var relativeRejected = false;
        try
        {
            _ = new DexpaceClientOptions { BaseAddress = new Uri("/relative", UriKind.Relative) };
        }
        catch (ArgumentException)
        {
            relativeRejected = true;
        }

        Expect(relativeRejected, "a relative base address is rejected at init");

        var instant = new DateTimeOffset(2026, 1, 1, 0, 0, 10, TimeSpan.Zero);
        Expect(HttpDate.TryParse(HttpDate.Format(instant), out var back) && back == instant, "HttpDate round trip");
        Expect(HttpDate.TryParse("Thu, 01 Jan 2026 00:00:10 utc", out _), "HttpDate accepts a lower-case UTC zone");
        Expect(!HttpDate.TryParse("Sunday, 06-Nov-94 08:49:37 GMT", out _), "HttpDate rejects RFC 850");

        var proxy = new ProxyOptions
        {
            Host = "proxy.example.test",
            Port = 3128,
            Password = "hunter2",
            NonProxyHosts = ["*.internal.example.com"],
        };
        Expect(proxy.IsBypassed("a.internal.example.com") && !proxy.IsBypassed("internal.example.com"), "proxy globs under AOT");
        Expect(!proxy.ToString().Contains("hunter2", StringComparison.Ordinal), "ProxyOptions ToString masks the password");
        var resolved = ProxyOptions.FromEnvironment(
            key => key == "HTTPS_PROXY" ? "http://p.example.test:3128" : null,
            NullLogger.Instance);
        Expect(resolved is { Host: "p.example.test", Port: 3128 }, "ProxyOptions.FromEnvironment resolves host and port");

        Expect(BuildInfo.IdentityTokens.Count == 2 && BuildInfo.IdentityTokens[0].Length > 0 && BuildInfo.IdentityTokens[1].Length > 0, "BuildInfo identity tokens");
        Expect(BuildInfo.RuntimeVersion != BuildInfo.Unknown, "BuildInfo runtime version under AOT");

        TimeProvider.System.Sleep(TimeSpan.Zero, CancellationToken.None);
        await TimeProvider.System.DelayAsync(TimeSpan.Zero, CancellationToken.None);
    }

    // Phase 6a: the retry surface under NativeAOT, on a clock whose timers fire almost at once (no reflection).
    private static async Task CheckPhase6aRetryAsync()
    {
        var clock = new InstantClock();
        var retryOptions = new RetryOptions { Jitter = 0, BaseDelay = TimeSpan.FromMilliseconds(200) };
        var clientOptions = new DexpaceClientOptions { Retry = retryOptions };

        // RetryPolicy: 503 then 200 is two sends and the 200 comes back.
        var sends = 0;
        await using var flaky = DelegateHttpClient.Create((request, _, _) =>
        {
            sends++;
            return Task.FromResult(new Response(request, sends == 1 ? Status.ServiceUnavailable : Status.Ok, Protocol.Http11));
        });
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(clock)).Build(flaky);
        using var recovered = await pipeline.SendAsync(Request.Get("https://smoke.example.test/"), clientOptions, CancellationToken.None);
        Expect(recovered.Status == Status.Ok && sends == 2, "RetryPolicy retries a 503 once and returns the 200");

        // RetryRecovery through RecoveryDispatcher: 503, 503, 200 reaches the 200.
        var dispatched = 0;
        await using var recoveryTransport = DelegateHttpClient.Create((request, _, _) =>
        {
            dispatched++;
            return Task.FromResult(new Response(request, dispatched < 3 ? Status.ServiceUnavailable : Status.Ok, Protocol.Http11));
        });
        var dispatcher = new RecoveryDispatcher(
            RequestRecoveryChain.Empty,
            ResponseRecoveryChain.Empty,
            new RetryRecovery(retryOptions, timeProvider: clock));
        using var reached = await dispatcher.DispatchAsync(recoveryTransport, Request.Get("https://smoke.example.test/"), RequestOptions.Empty);
        Expect(reached.Status == Status.Ok && dispatched == 3, "RetryRecovery retries through RecoveryDispatcher to the 200");

        await CheckPhase6aTimeoutAndCapabilityAsync(clock, clientOptions);
        CheckPhase6aOptionValidation();
    }

    private static async Task CheckPhase6aTimeoutAndCapabilityAsync(TimeProvider clock, DexpaceClientOptions clientOptions)
    {
        // OperationPolicy: an expired overall deadline is an OperationTimeoutException.
        await using var hanging = DelegateHttpClient.Create(async (request, _, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new Response(request, Status.Ok, Protocol.Http11);
        });
        var timed = new PipelineBuilder().Add(new OperationPolicy(clock)).Build(hanging);
        var timedOut = false;
        try
        {
            await timed.SendAsync(Request.Get("https://smoke.example.test/"), new DexpaceClientOptions { OverallTimeout = TimeSpan.FromSeconds(30) }, CancellationToken.None);
        }
        catch (OperationTimeoutException)
        {
            timedOut = true;
        }

        Expect(timedOut, "OperationPolicy throws OperationTimeoutException when the deadline expires");

        // IRetryableError: a custom exception is retried and a plain one is not.
        var custom = 0;
        await using var customTransport = DelegateHttpClient.Create((request, _, _) =>
            ++custom == 1 ? Task.FromException<Response>(new RetryableSmokeException()) : Task.FromResult(new Response(request, Status.Ok, Protocol.Http11)));
        var customPipeline = new PipelineBuilder().Add(new RetryPolicy(clock)).Build(customTransport);
        using var retriedCustom = await customPipeline.SendAsync(Request.Get("https://smoke.example.test/"), clientOptions, CancellationToken.None);
        Expect(retriedCustom.Status == Status.Ok && custom == 2, "an IRetryableError exception is retried");

        var plain = 0;
        await using var plainTransport = DelegateHttpClient.Create((request, _, _) =>
        {
            plain++;
            return Task.FromException<Response>(new InvalidOperationException("plain"));
        });
        var plainPipeline = new PipelineBuilder().Add(new RetryPolicy(clock)).Build(plainTransport);
        var plainThrown = false;
        try
        {
            await plainPipeline.SendAsync(Request.Get("https://smoke.example.test/"), clientOptions, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            plainThrown = true;
        }

        Expect(plainThrown && plain == 1, "a plain exception is not retried");
    }

    private static void CheckPhase6aOptionValidation()
    {
        // RetryOptions validates in its init accessors.
        var rejected = 0;
        try
        {
            _ = new RetryOptions { MaxRetryAttempts = -1 };
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected++;
        }

        try
        {
            _ = new RetryOptions { Jitter = 2 };
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected++;
        }

        Expect(rejected == 2, "RetryOptions rejects a negative MaxRetryAttempts and a Jitter of 2");
    }

    // Phase 6b: the redirect surface under NativeAOT (no reflection): a frozen allowed-method set with a predicate lambda, a
    // followed 307, and an https to http refusal that names only redacted URLs.
    private static async Task CheckPhase6bRedirectAsync()
    {
        var options = new RedirectOptions
        {
            AllowedMethods = new HashSet<Method> { Method.Get, Method.Post },
            Predicate = static condition => condition.Target is { } target && target.Host == "smoke.example.test",
        };
        Expect(options.AllowedMethods.Contains(Method.Post) && !options.AllowedMethods.Contains(Method.Put), "RedirectOptions.AllowedMethods is a copied set");

        var sends = 0;
        Request? second = null;
        await using var scripted = DelegateHttpClient.Create((request, _, _) =>
        {
            sends++;
            if (sends == 1)
            {
                var location = new Headers.Builder().Set("Location", "/moved").Build();
                return Task.FromResult(new Response(request, Status.TemporaryRedirect, Protocol.Http11, location));
            }

            second = request;
            return Task.FromResult(new Response(request, Status.Ok, Protocol.Http11));
        });
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(scripted);
        var post = Request.Post("https://smoke.example.test/start", RequestBody.FromString("payload"));
        using var followed = await pipeline.SendAsync(post, new DexpaceClientOptions { Redirect = options }, CancellationToken.None);
        Expect(
            followed.Status == Status.Ok && sends == 2 && second is { } hop && hop.Method == Method.Post && hop.Url.AbsolutePath == "/moved",
            "RedirectPolicy follows an allowed POST 307 with the method preserved");

        await using var downgrading = DelegateHttpClient.Create((request, _, _) =>
        {
            var location = new Headers.Builder().Set("Location", "http://smoke.example.test/plain?sig=secret").Build();
            return Task.FromResult(new Response(request, Status.Found, Protocol.Http11, location));
        });
        var refusing = new PipelineBuilder().Add(new RedirectPolicy()).Build(downgrading);
        string? message = null;
        try
        {
            using var never = await refusing.SendAsync(Request.Get("https://smoke.example.test/start?token=abc"), CancellationToken.None);
        }
        catch (RedirectSchemeDowngradeException ex)
        {
            message = ex.Message;
        }

        Expect(
            message is not null && message.Contains("smoke.example.test", StringComparison.Ordinal)
                && !message.Contains("secret", StringComparison.Ordinal) && !message.Contains("abc", StringComparison.Ordinal),
            "RedirectSchemeDowngradeException names the redacted URLs");
    }

    // Phase 6c: the challenge parser, the resolver, Digest (BCL hash and CSPRNG cnonce) and the bearer cache with its
    // background refresh, all without reflection.
    private static async Task CheckPhase6cAuthAsync()
    {
        var challenges = AuthenticationChallenge.Parse("Basic realm=\"a\", Digest realm=\"b\", nonce=\"n\", qop=\"auth\"");
        Expect(
            challenges.Count == 2 && challenges[0].Scheme == "basic" && challenges[1].Parameters["nonce"] == "n",
            "AuthenticationChallenge.Parse splits two challenges");
        Expect(AuthenticationChallenge.Parse("Digest realm=\"unterminated").Count == 1, "The challenge parser is lenient");

        var resolved = AuthResolver.Resolve(null, new AuthDescriptor(new AuthRequirement(AuthScheme.Digest), AuthRequirement.NoAuth), null, []);
        Expect(resolved.Scheme == AuthScheme.NoAuth, "AuthResolver falls through to NoAuth");

        var digest = new DigestChallengeHandler(new DigestCredential("Mufasa", "Circle Of Life"), [DigestAlgorithm.Sha256]);
        var answered = digest.Authorize(
            AuthenticationChallenge.Parse("Digest realm=\"r\", nonce=\"n\", qop=\"auth\", algorithm=SHA-256"),
            Request.Get("https://smoke.example.test/dir"),
            proxy: false);
        var header = answered?.Headers.Get("Authorization") ?? string.Empty;
        var response = AuthenticationChallenge.Parse(header).Count == 1 ? AuthenticationChallenge.Parse(header)[0].Parameters["response"] : string.Empty;
        Expect(
            header.Contains("algorithm=SHA-256", StringComparison.Ordinal) && response.Length == 64,
            "DigestChallengeHandler answers a SHA-256 challenge");

        Expect(
            new BasicCredential("u", "s3cr3t").ToString().Contains("***", StringComparison.Ordinal)
                && !new BasicCredential("u", "s3cr3t").ToString().Contains("s3cr3t", StringComparison.Ordinal)
                && new AccessToken("tok-secret").ToString().Contains("***", StringComparison.Ordinal)
                && !new AuthCredentials { Basic = new BasicCredential("u", "s3cr3t") }.ToString().Contains("s3cr3t", StringComparison.Ordinal),
            "Credentials redact their secrets");

        // A token inside the refresh margin is stamped now and refreshed in the background (cache, BoundedMap, SemaphoreSlim,
        // BackgroundWork under AOT).
        var credential = new SmokeTokenCredential();
        var seen = new List<string?>();
        await using var transport = DelegateHttpClient.Create((request, _, _) =>
        {
            seen.Add(request.Headers.Get("Authorization"));
            return Task.FromResult(new Response(request, Status.Ok, Protocol.Http11));
        });
        var pipeline = new PipelineBuilder().Add(new BearerTokenAuthPolicy(credential, "smoke")).Build(transport);
        using var first = await pipeline.SendAsync(Request.Get("https://smoke.example.test/"), CancellationToken.None);
        using var second = await pipeline.SendAsync(Request.Get("https://smoke.example.test/"), CancellationToken.None);
        for (var i = 0; i < 500 && credential.Calls < 2; i++)
        {
            await Task.Delay(10);
        }

        Expect(
            seen is ["Bearer smoke-1", "Bearer smoke-1"] && credential.Calls >= 2,
            "BearerTokenAuthPolicy stamps the valid token and refreshes in the background");
    }

    // Phase 7a (SERDE-14..SERDE-20, SERDE-27, SERDE-28, HTTP-44, HTTP-45; NFR-9): the Tristate PATCH round trip. The value-type
    // Tristate<int> is what exercises the converter factory's one justified IL2067 suppression (GetUninitializedObject over a
    // closed generic struct, reached by interface dispatch with no MakeGenericType) under the published NativeAOT binary.
    private static async Task CheckPhase7aSerdeAsync()
    {
        var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(SmokeJsonContext.Default));
        var patch = new WidgetPatch(Tristate.Null, Tristate.Present(3), default);

        // 1. The wire bytes: Null written as null, Present written, Absent omitted, camelCase from CreateDefaultOptions.
        Expect(serde.SerializeToString(patch) == "{\"name\":null,\"size\":3}", "phase 7a: the Tristate wire bytes are {\"name\":null,\"size\":3}");

        // 2. Sent as a PATCH body through RequestBody.FromValue over the in-process transport, which echoes it back.
        await using var transport = new EchoTransport();
        using var pipeline = DexpacePipeline.CreateDefault(transport);
        var request = Request.Post(s_endpoint.OriginalString, RequestBody.FromValue(patch, serde)).WithMethod(Method.Patch);
        var response = await pipeline.SendAsync(request, new DexpaceClientOptions(), CancellationToken.None);

        // 3. Decoded lazily through TypedResponse + the status-aware handler: Null, Present and Absent hold.
        using var typed = new TypedResponse<WidgetPatch>(response, ResponseHandlers.DeserializeOnSuccess<WidgetPatch>(serde));
        Expect(typed.Status == Status.Ok, "phase 7a: the typed response exposes its status without a parse");
        var first = await typed.GetValueAsync();
        Expect(first.Name.IsNull && first.Size == Tristate.Present(3) && first.Note.IsAbsent, "phase 7a: Null, Present and Absent survive the round trip");

        // 4. Memoized: a second GetValueAsync is the same instance, and the handler did not run again.
        Expect(ReferenceEquals(first, await typed.GetValueAsync()), "phase 7a: TypedResponse parses once");

        // 5. A wire null into a reference type is rejected, naming the type; the nullable route admits it.
        var rejected = false;
        try
        {
            _ = await ResponseBody.FromBytes("null"u8.ToArray()).ReadValueAsync<WidgetPatch>(serde);
        }
        catch (DeserializationException)
        {
            rejected = true;
        }

        Expect(rejected, "phase 7a: ReadValueAsync rejects a wire null for a reference type");
        Expect(await ResponseBody.FromBytes("null"u8.ToArray()).ReadValueOrDefaultAsync<WidgetPatch>(serde) is null, "phase 7a: ReadValueOrDefaultAsync admits a wire null");

        // 6. The synchronous stream decode streams through the adapter's override.
        Expect(
            serde.Deserialize<WidgetPatch>(new MemoryStream("{\"size\":null}"u8.ToArray()))!.Size.IsNull,
            "phase 7a: the synchronous stream decode reads a Tristate");
    }

    /// <summary>A token that is always inside the 30 s refresh margin, so every request after the first starts a refresh.</summary>
    private sealed class SmokeTokenCredential : TokenCredential
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            new(new AccessToken("smoke-" + Interlocked.Increment(ref _calls), DateTimeOffset.UtcNow.AddSeconds(20)));
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

    /// <summary>A clock whose timers fire almost at once, so a retry wait or a deadline passes without waiting.</summary>
    private sealed class InstantClock : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            base.CreateTimer(callback, state, TimeSpan.FromMilliseconds(1), period);
    }

    /// <summary>An exception that opts into retries through the capability, with no SDK base type.</summary>
    private sealed class RetryableSmokeException : Exception, IRetryableError
    {
        public bool IsRetryable => true;
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
