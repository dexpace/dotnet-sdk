// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>TRANSPORT-15, XCUT-22 (P9-13, V8): the container-owned lease releases the primary the package built, and only that.</summary>
[Trait("Category", "Integration")]
public sealed class PrimaryHandlerLeaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Disposing_the_container_releases_the_pooled_connection()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("pooled", keepAlive: true));
        var services = new ServiceCollection();
        _ = TestClients.Add(services);
        var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);
        using (var response = await transport.ExecuteAsync(Request.Get(server.Url("/owned").AbsoluteUri), Ct))
        {
            _ = await response.Body.ReadAsStringAsync(Ct);
        }

        await provider.DisposeAsync();

        // Without the lease the factory's infinite lifetime leaves the idle connection open and this wait never completes (V8).
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        await server.WaitForConnectionReleasedAsync(0, bound.Token);
    }

    [Fact]
    public async Task A_callers_DelegatingHandler_is_not_disposed_by_the_container()
    {
        var services = new ServiceCollection();
        var builder = TestClients.Add(services);
        using var spy = new ChainSpy();
        builder.HttpClient.AddHttpMessageHandler(() => spy);
        var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        await provider.DisposeAsync();

        Assert.Equal(0, spy.Disposals);
    }

    [Fact]
    public async Task A_replaced_primary_is_the_callers_and_only_the_discarded_instance_is_released()
    {
        var services = new ServiceCollection();
        var builder = TestClients.Add(services);
        using var replacement = new CountingHandler();
        builder.HttpClient.ConfigurePrimaryHttpMessageHandler(() => replacement);
        var provider = services.BuildServiceProvider();

        // The package's own primary was built and discarded by the factory only if its delegate ran; resolving the lease shows what it holds.
        var lease = provider.GetRequiredKeyedService<PrimaryHandlerLease>(TestClients.Name);
        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);
        var discarded = Assert.IsType<SocketsHttpHandler>(Assert.Single(lease.Tracked));
        Assert.NotSame(replacement, discarded);
        await provider.DisposeAsync();

        Assert.Equal(0, replacement.Disposals);
        _ = Assert.Throws<ObjectDisposedException>(() => discarded.PooledConnectionLifetime = TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_provider_built_but_never_resolved_disposes_nothing_and_throws_nothing()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services);
        var provider = services.BuildServiceProvider();

        await provider.DisposeAsync();
    }

    [Fact]
    public void The_lease_disposes_what_it_tracks_once_and_a_late_track_disposes_at_once()
    {
        var lease = new PrimaryHandlerLease();
        var first = new CountingHandler();
        lease.Track(first);

        lease.Dispose();
        lease.Dispose();
        var late = new CountingHandler();
        lease.Track(late);

        Assert.Equal(1, first.Disposals);
        Assert.Equal(1, late.Disposals);
    }

    [Fact]
    public async Task The_lease_is_created_before_and_disposed_after_the_transport()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        var builder = TestClients.Add(services);
        _ = builder.UseTransport(_ => new OrderedTransport(order));
        var provider = services.BuildServiceProvider();
        var lease = provider.GetRequiredKeyedService<PrimaryHandlerLease>(TestClients.Name);
        lease.Track(new OrderedHandler(order));
        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        await provider.DisposeAsync();

        Assert.Equal(["transport", "lease-handler"], order);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _disposals;

        internal int Disposals => Volatile.Read(ref _disposals);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage());

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposals);
            }

            base.Dispose(disposing);
        }
    }

    private sealed class OrderedHandler(List<string> order) : IDisposable
    {
        public void Dispose() => order.Add("lease-handler");
    }

    private sealed class OrderedTransport(List<string> order) : IAsyncHttpClient
    {
        public Task<Dexpace.Sdk.Core.Http.Response.Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            order.Add("transport");
            return ValueTask.CompletedTask;
        }
    }
}
