// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Text;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
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
