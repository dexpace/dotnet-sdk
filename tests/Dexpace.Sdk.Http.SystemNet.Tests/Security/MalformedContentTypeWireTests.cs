// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Security;

/// <summary>
/// Roadmap phase 1, S9 (TRANSPORT-27, TRANSPORT-22; design §3.2, §4.4). The verified defect: a server reply with
/// <c>Content-Type: text/plain; foo</c>, which <c>HttpClient</c> accepts, made <c>ExecuteAsync</c> throw a raw
/// <see cref="ArgumentException"/> after the <see cref="HttpResponseMessage"/> was live, and nothing disposed it. The
/// malformed reply is written byte-for-byte by the loopback server. Permanent (roadmap constraint 5); phases 8b
/// (inbound adaptation) and 3b (dispose latches) own the structural form.
/// </summary>
[Trait("Category", "Security")]
public sealed class MalformedContentTypeWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemHttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    [Theory]
    [InlineData("text/plain; foo")]
    [InlineData("text/plain; charset=\"utf-8")]
    [InlineData("text/plain; name=vålue")]
    [InlineData("\u00A0text/plain")]
    [InlineData("text/plain;\u00A0charset=utf-8")]
    public async Task An_unparseable_inbound_content_type_becomes_no_media_type(string contentType)
    {
        var reply = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: 2\r\nConnection: close\r\n\r\nhi";
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw(reply));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);

        await using var response = await transport.ExecuteAsync(Request.Get(server.Url("/malformed").ToString()), Ct);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Null(response.Body.ContentType);
        Assert.Equal("hi", await response.Body.ReadAsStringAsync(Ct));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_well_formed_inbound_content_type_is_still_parsed()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("hi", "text/plain; charset=utf-8"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);

        await using var response = await transport.ExecuteAsync(Request.Get(server.Url("/ok").ToString()), Ct);

        Assert.Equal("text/plain", response.Body.ContentType?.FullType);
        Assert.Equal("utf-8", response.Body.ContentType?.Parameters["charset"]);
    }

    [Fact]
    public async Task The_native_response_is_disposed_when_adaptation_throws()
    {
        // A handler stub, because the point is a throw the wire cannot script: the content's length computation fails
        // while the response is being adapted, after the HttpResponseMessage is live (TRANSPORT-22).
        var content = new ThrowingLengthContent();
        var message = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        using var client = new SystemHttpClient(new StubHandler(message));
        await using var transport = new SystemNetHttpClient(client);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.ExecuteAsync(Request.Get("https://example.test/x"), Ct));

        Assert.True(content.IsDisposed);
    }

    private sealed class ThrowingLengthContent : HttpContent
    {
        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

        protected override bool TryComputeLength(out long length) =>
            throw new InvalidOperationException("length computation failed");

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
