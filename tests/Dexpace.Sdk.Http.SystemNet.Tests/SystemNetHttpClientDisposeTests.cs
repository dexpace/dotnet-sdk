// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The transports under test are disposed by the assertions.

using System.Net;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>SEAM-14, the transport's idempotent dispose (design §3.7, closed by phase 3b).</summary>
[Trait("Category", "Unit")]
public sealed class SystemNetHttpClientDisposeTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _disposals;

        public int Disposals => Volatile.Read(ref _disposals);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposals);
            }

            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task An_owned_client_disposes_its_HttpClient_once_across_Dispose_and_DisposeAsync()
    {
        var transport = new SystemNetHttpClient();

        transport.Dispose();
        await transport.DisposeAsync();
        transport.Dispose();

        Assert.Equal(1, transport.OwnedClientReleases);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => transport.ExecuteAsync(Request.Get("https://example.test/"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_borrowed_client_is_never_disposed_by_either_form()
    {
        using var handler = new CountingHandler();
        using var borrowed = new SystemHttpClient(handler, disposeHandler: false);
        var transport = new SystemNetHttpClient(borrowed);

        transport.Dispose();
        await transport.DisposeAsync();

        Assert.Equal(0, transport.OwnedClientReleases);
        Assert.Equal(0, handler.Disposals);
        using var response = await borrowed.GetAsync(new Uri("https://example.test/"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_disposes_are_safe()
    {
        const int Contenders = 16;
        var transport = new SystemNetHttpClient();
        using var barrier = new Barrier(Contenders);

        await Task.WhenAll(Enumerable.Range(0, Contenders).Select(i => Task.Run(
            async () =>
            {
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                if (i % 2 == 0)
                {
                    transport.Dispose();
                }
                else
                {
                    await transport.DisposeAsync();
                }
            },
            TestContext.Current.CancellationToken)));

        Assert.Equal(1, transport.OwnedClientReleases);
    }
}
