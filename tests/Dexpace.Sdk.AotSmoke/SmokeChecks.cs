// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Text;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Operations;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;

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
        var pipeline = DexpacePipeline.CreateDefault(transport);

        var request = Request.Post(s_endpoint.OriginalString, RequestBody.FromValue(new Widget("gear", 9), serde));
        using var response = await pipeline.SendAsync(request, new DexpaceClientOptions());
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

    private static void Expect(bool condition, string what)
    {
        if (!condition)
        {
            throw new SmokeFailureException(what);
        }
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
    private sealed class CannedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("pong", Encoding.UTF8, "text/plain"),
                RequestMessage = request,
            };
            response.Headers.Add("X-Echo-Method", request.Method.Method);
            return Task.FromResult(response);
        }
    }
}
