// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-1 and TRANSPORT-15 (phase 8b, P8b-3): the handler-taking construction borrows the handler and refuses one whose
/// primary follows redirects, without ever changing it. This class tests the chain walk; the constructor assertions follow it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HandlerConstructorTests
{
    [Fact]
    public void A_redirect_following_sockets_handler_is_rejected_naming_the_setting_and_the_requirement()
    {
        using var handler = new SocketsHttpHandler();

        var ex = Assert.Throws<ArgumentException>(() => HandlerChain.ThrowIfFollowsRedirects(handler));

        Assert.Equal("handler", ex.ParamName);
        Assert.Contains("AllowAutoRedirect", ex.Message, StringComparison.Ordinal);
        Assert.Contains("TRANSPORT-1", ex.Message, StringComparison.Ordinal);
        Assert.True(handler.AllowAutoRedirect, "the check must not flip the caller's handler");
    }

    [Fact]
    public void A_redirect_following_client_handler_is_rejected()
    {
        using var handler = new HttpClientHandler();

        var ex = Assert.Throws<ArgumentException>(() => HandlerChain.ThrowIfFollowsRedirects(handler));

        Assert.Equal("handler", ex.ParamName);
        Assert.True(handler.AllowAutoRedirect);
    }

    [Fact]
    public void A_redirect_refusing_primary_is_accepted_and_left_alone()
    {
        using var sockets = new SocketsHttpHandler { AllowAutoRedirect = false };
        using var client = new HttpClientHandler { AllowAutoRedirect = false };

        HandlerChain.ThrowIfFollowsRedirects(sockets);
        HandlerChain.ThrowIfFollowsRedirects(client);

        Assert.False(sockets.AllowAutoRedirect);
        Assert.False(client.AllowAutoRedirect);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_redirect_following_primary_under_delegating_handlers_is_rejected(int wrappers)
    {
        using var chain = Wrap(new SocketsHttpHandler(), wrappers);

        var ex = Assert.Throws<ArgumentException>(() => HandlerChain.ThrowIfFollowsRedirects(chain));

        Assert.Contains("AllowAutoRedirect", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_redirect_refusing_primary_under_delegating_handlers_is_accepted(int wrappers)
    {
        using var chain = Wrap(new SocketsHttpHandler { AllowAutoRedirect = false }, wrappers);

        HandlerChain.ThrowIfFollowsRedirects(chain);
    }

    [Fact]
    public void A_delegating_handler_with_no_inner_handler_yet_is_accepted()
    {
        // IHttpClientFactory assigns the primary later; HttpClient fails the first send by itself if none ever arrives.
        using var chain = new Passthrough();

        HandlerChain.ThrowIfFollowsRedirects(chain);
    }

    [Fact]
    public void An_unknown_primary_handler_is_accepted()
    {
        using var unknown = new UnknownHandler();
        using var wrapped = Wrap(new UnknownHandler(), 1);

        HandlerChain.ThrowIfFollowsRedirects(unknown);
        HandlerChain.ThrowIfFollowsRedirects(wrapped);
    }

    [Fact]
    public void A_very_deep_chain_is_walked_without_recursion()
    {
        using var chain = Wrap(new SocketsHttpHandler { AllowAutoRedirect = false }, 1_000);

        HandlerChain.ThrowIfFollowsRedirects(chain);
    }

    [Fact]
    public void A_null_handler_throws()
    {
        Assert.Throws<ArgumentNullException>(() => HandlerChain.ThrowIfFollowsRedirects(null!));
    }

    // The constructor: it borrows the handler, owns the HttpClient it builds over it, and refuses a redirect-following primary.
    [Fact]
    public async Task The_constructor_never_disposes_the_borrowed_handler_but_releases_its_own_client_once()
    {
        using var primary = new DisposeTrackingPrimary();
        using var counting = new DisposeCountingWrapper(primary);
        var transport = new SystemNetHttpClient(counting);

        transport.Dispose();
        await transport.DisposeAsync();

        Assert.Equal(1, transport.OwnedClientReleases);
        Assert.Equal(0, counting.Disposals);
        Assert.Equal(0, primary.Disposals);
    }

    [Fact]
    public async Task The_constructor_sends_through_the_borrowed_handler()
    {
        using var handler = new UnknownHandler();
        await using var transport = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions());

        await using var response = await transport.ExecuteAsync(Request.Get("https://example.test/ping"), TestContext.Current.CancellationToken);

        Assert.Equal(200, response.Status.Code);
    }

    [Fact]
    public void The_constructor_rejects_a_redirect_following_handler_and_leaves_it_alone()
    {
        using var handler = new SocketsHttpHandler();

        var ex = Assert.Throws<ArgumentException>(() => new SystemNetHttpClient(handler));

        Assert.Contains("AllowAutoRedirect", ex.Message, StringComparison.Ordinal);
        Assert.True(handler.AllowAutoRedirect);
        Assert.Throws<ArgumentException>(() => new SystemNetHttpClient(handler, new SystemNetHttpClientOptions()));
    }

    [Fact]
    public void A_null_handler_or_options_throws()
    {
        using var handler = new UnknownHandler();

        Assert.Throws<ArgumentNullException>(() => new SystemNetHttpClient((HttpMessageHandler)null!));
        Assert.Throws<ArgumentNullException>(() => new SystemNetHttpClient((HttpMessageHandler)null!, new SystemNetHttpClientOptions()));
        Assert.Throws<ArgumentNullException>(() => new SystemNetHttpClient(handler, null!));
        Assert.Throws<ArgumentNullException>(() => new SystemNetHttpClient((SystemNetHttpClientOptions)null!));
        Assert.Throws<ArgumentNullException>(() => new SystemNetHttpClient((SystemHttpClient)null!, new SystemNetHttpClientOptions()));
    }

    [Fact]
    public void A_proxy_with_a_handler_or_a_borrowed_client_is_rejected_naming_the_option_and_the_requirement()
    {
        var withProxy = new SystemNetHttpClientOptions { Proxy = new ProxyOptions { Host = "proxy.test", Port = 3128 } };
        using var handler = new UnknownHandler();
        using var client = new SystemHttpClient(new UnknownHandler());

        var viaHandler = Assert.Throws<ArgumentException>(() => new SystemNetHttpClient(handler, withProxy));
        var viaClient = Assert.Throws<ArgumentException>(() => new SystemNetHttpClient(client, withProxy));

        foreach (var ex in new[] { viaHandler, viaClient })
        {
            Assert.Contains("Proxy", ex.Message, StringComparison.Ordinal);
            Assert.Contains("TRANSPORT-15", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("proxy.test", ex.Message, StringComparison.Ordinal);
        }
    }

    private static HttpMessageHandler Wrap(HttpMessageHandler primary, int wrappers)
    {
        var current = primary;
        for (var i = 0; i < wrappers; i++)
        {
            current = new Passthrough { InnerHandler = current };
        }

        return current;
    }

    private sealed class Passthrough : DelegatingHandler;

    // The wrapper counts the Dispose calls that reach it and the primary below it counts its own, so a test sees whether the
    // transport released anything of the caller's.
    private sealed class DisposeCountingWrapper(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private int _disposals;

        public int Disposals => Volatile.Read(ref _disposals);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposals);
            }

            base.Dispose(disposing);
        }
    }

    private sealed class DisposeTrackingPrimary : HttpMessageHandler
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

    private sealed class UnknownHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
