// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>
/// The seed origin (P4c-5, REDIR-11, REDIR-24, AUTH-29): the request handed to <c>Send</c>, fixed on the call-scoped
/// context at entry, is what the redirect and authorization policies compare against.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SeedOriginTests
{
    [Fact]
    public async Task The_seed_request_is_fixed_at_entry_and_not_the_held_request()
    {
        var original = Request.Get("https://api.example.com/v1/resource");
        Request? seedSeen = null;
        Request? heldSeen = null;
        var stamper = new DelegatePolicy(
            PipelineStage.PerCall,
            (request, context, next) => next.RunAsync(request.WithHeaders(request.Headers.Set("X-Stamped", "1")), context));
        var probe = new DelegatePolicy(PipelineStage.PerAttempt, (request, context, next) =>
        {
            seedSeen = context.SeedRequest;
            heldSeen = request;
            return next.RunAsync(request, context);
        });
        var pipeline = new PipelineBuilder().Add(stamper).Add(probe).Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(original, TestContext.Current.CancellationToken);

        Assert.Same(original, seedSeen);
        Assert.NotSame(original, heldSeen);
        Assert.Equal("1", heldSeen!.Headers.Get("X-Stamped"));
    }

    [Fact]
    public async Task A_redirect_judges_cross_origin_against_the_seed_not_the_previous_hop()
    {
        // A -> B -> A: the first hop leaves the seed origin, so Cookie is stripped there and gone on every later hop.
        var seed = Request.Get("https://a.example/start").WithHeaders(new Headers.Builder().Set("Cookie", "session=1").Build());
        var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "https://b.example/next"),
            TestResponses.Redirect(302, "https://a.example/back"),
            TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(seed, new DexpaceClientOptions(), TestContext.Current.CancellationToken);

        Assert.Equal("session=1", transport.Requests[0].Headers.Get("Cookie"));
        Assert.Null(transport.Requests[1].Headers.Get("Cookie"));
        Assert.Null(transport.Requests[2].Headers.Get("Cookie"));
    }

    [Fact]
    public async Task A_same_origin_chain_keeps_the_cookie_on_every_hop()
    {
        var seed = Request.Get("https://a.example/start").WithHeaders(new Headers.Builder().Set("Cookie", "session=1").Build());
        var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "https://a.example/one"),
            TestResponses.Redirect(302, "https://a.example/two"),
            TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(seed, new DexpaceClientOptions(), TestContext.Current.CancellationToken);

        Assert.All(transport.Requests, sent => Assert.Equal("session=1", sent.Headers.Get("Cookie")));
    }

    [Fact]
    public async Task A_policy_above_redirect_that_rewrites_the_url_does_not_move_the_seed()
    {
        // The seed is the request passed to Send. A policy above redirect rewrites the URL to another host; the hop back
        // to the rewritten host is same-origin with the held request but cross-origin with the seed, so Cookie is stripped.
        var seed = Request.Get("https://seed.example/start").WithHeaders(new Headers.Builder().Set("Cookie", "session=1").Build());
        var rewriter = new DelegatePolicy(
            PipelineStage.PerCall,
            (request, context, next) => next.RunAsync(request.WithUrl(new Uri("https://rewritten.example/start")), context));
        var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "https://rewritten.example/next"),
            TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(rewriter).Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(seed, new DexpaceClientOptions(), TestContext.Current.CancellationToken);

        Assert.Equal("session=1", transport.Requests[0].Headers.Get("Cookie"));
        Assert.Null(transport.Requests[1].Headers.Get("Cookie"));
    }

    [Fact]
    public async Task Auth_compares_the_origin_against_the_seed_request()
    {
        var credential = new CountingCredential();
        var policy = new BearerTokenAuthPolicy(credential, "scope");
        var context = TestContexts.For(Request.Get("https://api.example.com/v1/resource"));

        // A hop that left the seed origin: stripped, and the credential is not resolved.
        var foreign = await TestContexts.SentAsync(
            policy,
            Request.Get("https://other.example.org/cb").WithHeaders(new Headers.Builder().Set("Authorization", "Bearer stale").Build()),
            context);
        Assert.Null(foreign.Headers.Get("Authorization"));
        Assert.Equal(0, credential.CallCount);

        // A hop back to the seed origin is stamped.
        var back = await TestContexts.SentAsync(policy, Request.Get("https://api.example.com/other"), context);
        Assert.Equal("Bearer secret", back.Headers.Get("Authorization"));
        Assert.Equal(1, credential.CallCount);
    }

    [Fact]
    public async Task The_retired_bag_key_cannot_be_hijacked()
    {
        // The string key "dexpace.auth.origin" was overwritable by any policy (design §6.2). A policy above auth that
        // writes a property of that name changes nothing.
        var hijack = new DelegatePolicy(PipelineStage.PerAttempt, (request, context, next) =>
        {
            context.SetProperty(new PipelinePropertyKey<string>("dexpace.auth.origin"), "https://other.example.org:443");
            return next.RunAsync(request, context);
        });
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(hijack)
            .Add(new ApiKeyAuthPolicy(new ApiKeyCredential("key")))
            .Build(transport);

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/x"), TestContext.Current.CancellationToken);

        Assert.Equal("key", transport.LastRequest!.Headers.Get("Authorization"));
    }

    private sealed class CountingCredential : TokenCredential
    {
        private int _calls;

        public int CallCount => Volatile.Read(ref _calls);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            return new ValueTask<AccessToken>(new AccessToken("secret", DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}
