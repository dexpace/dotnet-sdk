// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Xunit;

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

    private sealed class UnknownHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
