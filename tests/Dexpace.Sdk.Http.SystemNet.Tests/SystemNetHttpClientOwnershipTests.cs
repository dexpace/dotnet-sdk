// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// SEAM-14, ownership half (design §3.7): the adapter never disposes a caller-supplied <c>HttpClient</c> and does
/// dispose the one it created. Repeated dispose is safe today because it relies on <c>HttpClient</c>'s own
/// idempotence; the SDK-owned idempotence latch is phase 3b's. Over an in-process handler, so no socket and <c>Unit</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SystemNetHttpClientOwnershipTests
{
    private static readonly Uri s_url = new("https://example.test/ping");

    [Fact]
    public async Task A_borrowed_client_still_sends_after_the_transport_is_disposed()
    {
        using var handler = new StubHandler();
        using var borrowed = new SystemHttpClient(handler);
        var transport = new SystemNetHttpClient(borrowed);

        transport.Dispose();

        using var message = new HttpRequestMessage(HttpMethod.Get, s_url);
        using var response = await borrowed.SendAsync(message, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(handler.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_leaves_a_borrowed_client_alone_too()
    {
        using var handler = new StubHandler();
        using var borrowed = new SystemHttpClient(handler);
        var transport = new SystemNetHttpClient(borrowed);

        await transport.DisposeAsync();

        using var message = new HttpRequestMessage(HttpMethod.Get, s_url);
        using var response = await borrowed.SendAsync(message, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(handler.IsDisposed);
    }

    [Fact]
    public async Task An_owned_client_is_disposed_with_the_transport()
    {
        var transport = new SystemNetHttpClient();

        transport.Dispose();

        // HttpClient checks disposal before any I/O, so no socket is opened.
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => transport.ExecuteAsync(Request.Get(s_url.AbsoluteUri), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dispose_twice_does_not_throw()
    {
        var owned = new SystemNetHttpClient();
        owned.Dispose();
        var ex = Record.Exception(owned.Dispose);
        Assert.Null(ex);

        using var handler = new StubHandler();
        using var borrowed = new SystemHttpClient(handler);
        var transport = new SystemNetHttpClient(borrowed);
        transport.Dispose();
        Assert.Null(Record.Exception(transport.Dispose));
    }

    [Fact]
    public async Task DisposeAsync_twice_does_not_throw()
    {
        var owned = new SystemNetHttpClient();
        await owned.DisposeAsync();
        Assert.Null(await Record.ExceptionAsync(() => owned.DisposeAsync().AsTask()));

        using var handler = new StubHandler();
        using var borrowed = new SystemHttpClient(handler);
        var transport = new SystemNetHttpClient(borrowed);
        await transport.DisposeAsync();
        Assert.Null(await Record.ExceptionAsync(() => transport.DisposeAsync().AsTask()));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public bool IsDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
