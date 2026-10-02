// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

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

        public async Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default)
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
