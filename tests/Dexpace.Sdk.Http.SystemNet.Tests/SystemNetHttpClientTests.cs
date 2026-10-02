// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

// Handler-stub tests of the adapter's model mapping: no socket, so Unit (design §9.3). Moved, assertions
// unchanged, from Dexpace.Sdk.Core.Tests/Transport/ when core's suite stopped referencing a transport (SEAM-2,
// design §2.3).
[Trait("Category", "Unit")]
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
        await using var response = await transport.ExecuteAsync(Request.Get("https://example.test/ping"), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal("abc123", response.Headers.Get("x-trace"));
        Assert.Equal("text", response.Body.ContentType!.Type);
        Assert.Equal("pong", await response.Body.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_response_carries_the_request_that_was_sent()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await using var transport = new SystemNetHttpClient(new SystemHttpClient(handler));
        var request = Request.Get("https://example.test/ping").WithHeader("X-Trace", "abc");

        await using var response = await transport.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(request, response.Request);
    }

    [Fact]
    public async Task The_reason_phrase_is_carried_when_it_is_header_safe()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { ReasonPhrase = "All Good" });
        await using var transport = new SystemNetHttpClient(new SystemHttpClient(handler));

        await using var response = await transport.ExecuteAsync(Request.Get("https://example.test/ping"), TestContext.Current.CancellationToken);

        Assert.Equal("All Good", response.ReasonPhrase);
    }

    [Fact]
    public async Task An_unsafe_reason_phrase_from_a_handler_is_dropped_to_null_not_thrown()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { ReasonPhrase = "Fine\u0001X" });
        await using var transport = new SystemNetHttpClient(new SystemHttpClient(handler));

        await using var response = await transport.ExecuteAsync(Request.Get("https://example.test/ping"), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Null(response.ReasonPhrase);
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
        await using var response = await transport.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Created, response.Status);
        Assert.Equal("{\"name\":\"widget\"}", observed);
    }

    [Fact]
    public async Task ExecuteAsync_WrapsTransportFailure()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));
        await using var transport = new SystemNetHttpClient(new SystemHttpClient(handler));

        await Assert.ThrowsAsync<ServiceRequestException>(
            () => transport.ExecuteAsync(Request.Get("https://example.test/x"), TestContext.Current.CancellationToken));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
