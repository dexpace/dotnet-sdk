// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Http.SystemNet;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Core.Tests.Transport;

public class SystemNetHttpClientTests
{
    [Fact]
    public async Task ExecuteAsync_MapsStatusHeadersAndBody()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://example.test/ping", request.RequestUri!.ToString());
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("pong", Encoding.UTF8, "text/plain"),
            };
            response.Headers.TryAddWithoutValidation("X-Trace", "abc123");
            return response;
        });

        await using var transport = new SystemNetHttpClient(new SystemHttpClient(handler));
        await using var response = await transport.ExecuteAsync(Request.Get("https://example.test/ping"));

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal("abc123", response.Headers.Get("x-trace"));
        Assert.Equal("text", response.Body.ContentType!.Type);
        Assert.Equal("pong", await response.Body.ReadAsStringAsync());
    }

    [Fact]
    public async Task ExecuteAsync_SendsRequestBody()
    {
        string? observed = null;
        var handler = new StubHandler(request =>
        {
            observed = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        await using var transport = new SystemNetHttpClient(new SystemHttpClient(handler));
        var request = Request.Post(
            "https://example.test/items",
            RequestBody.FromString("{\"name\":\"widget\"}", CommonMediaTypes.ApplicationJson));
        await using var response = await transport.ExecuteAsync(request);

        Assert.Equal(Status.Created, response.Status);
        Assert.Equal("{\"name\":\"widget\"}", observed);
    }

    [Fact]
    public async Task ExecuteAsync_WrapsTransportFailure()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));
        await using var transport = new SystemNetHttpClient(new SystemHttpClient(handler));

        await Assert.ThrowsAsync<ServiceRequestException>(
            () => transport.ExecuteAsync(Request.Get("https://example.test/x")));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
