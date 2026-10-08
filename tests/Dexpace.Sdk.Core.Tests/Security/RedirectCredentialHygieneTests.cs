// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 packages/core/src/redirect/decide.test.ts: the REDIR-7, REDIR-9, REDIR-10 and
// REDIR-12 rows ("Authorization is stripped unconditionally, even same-origin", "Cookie and Proxy-Authorization
// survive a same-origin hop", "Cookie and Proxy-Authorization are stripped on a cross-origin hop", "userinfo embedded
// in the Location is dropped unconditionally", "every Authorization value is stripped, whatever its casing", "a
// protocol-relative Location inherits the scheme and is judged cross-origin"), and the REDIR-8 seed row of
// nodejs-sdk@54aeed4 packages/core/src/redirect/cross-origin.test.ts ("comparison is against the SEED, not a previous
// hop"), its "an explicit default port equals an omitted one", "host comparison is case-insensitive" and "a differing
// path/query/fragment alone is never cross-origin" rows (as fixed examples, not fast-check properties). Not ported
// here: the marker rows, which this port retires (design §6.2, §10 entry 15). The rest of the decide() matrix (codes,
// methods, predicate, loop detection, hop cap, the replayability and downgrade errors) is RedirectDecisionMatrixTests',
// and the end-to-end proof through auth and retry is RedirectCredentialLeakTests'.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S3's <see cref="RedirectPolicy"/> half (REDIR-7, REDIR-8, REDIR-9, REDIR-12, XCUT-17(a)–(c); design
/// §6.2). The verified defects, visible once the transport stopped following redirects itself: <c>Authorization</c>
/// was kept on a same-origin hop, cross-origin was judged against the previous hop rather than the seed request,
/// <c>Proxy-Authorization</c> crossed origins, and userinfo in a <c>Location</c> was re-issued. The wire-level proof is
/// <c>Dexpace.Sdk.Http.SystemNet.Tests.Security.RedirectWireTests</c>. Permanent (roadmap constraint 5); phase 6b's
/// redirect rewrite owns the structural form, over phase 4c's seed origin on the call context.
/// </summary>
[Trait("Category", "Security")]
public sealed class RedirectCredentialHygieneTests
{
    private static readonly DexpaceClientOptions s_options = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Headers Credentials() => new Headers.Builder()
        .Add("Authorization", "Bearer x")
        .Add("Cookie", "a=b")
        .Add("Proxy-Authorization", "Basic cHJveHk6cHc=")
        .Add("X-Pass", "kept")
        .Build();

    private static Task<IReadOnlyList<Request>> FollowAsync(Request seed, params Response[] script) =>
        FollowAsync(seed, new RedirectOptions(), script);

    private static async Task<IReadOnlyList<Request>> FollowAsync(Request seed, RedirectOptions redirect, params Response[] script)
    {
        var transport = new ScriptedTransport([.. script, TestResponses.Create(Status.Ok, seed)]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);
        using var response = await pipeline.SendAsync(seed, new DexpaceClientOptions { Redirect = redirect }, Ct);
        Assert.Equal(Status.Ok, response.Status);
        return transport.Requests;
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task Authorization_is_stripped_on_every_hop_even_same_origin(int status)
    {
        var seed = Request.Create(Method.Get, "https://example.com/a", Credentials());

        // A 303 is followed only with the opt-in; without it the assertion below would be vacuous.
        var sent = await FollowAsync(
            seed,
            new RedirectOptions { FollowSeeOther = true },
            TestResponses.Redirect(status, "https://example.com/b", seed));

        Assert.Empty(sent[1].Headers.GetAll("Authorization"));
    }

    [Theory]
    [InlineData("https://example.com/b")]
    [InlineData("https://example.com:443/b")]
    [InlineData("https://EXAMPLE.com/b")]
    [InlineData("https://Example.COM:443/b?x=1#y")]
    [InlineData("/b?x=1")]
    public async Task Cookie_and_proxy_authorization_survive_a_same_origin_hop(string location)
    {
        // REDIR-8's origin tuple: an explicit default port equals an omitted one, the host compares case-insensitively,
        // and path, query and fragment never participate.
        var seed = Request.Create(Method.Get, "https://example.com/a", Credentials());

        var sent = await FollowAsync(seed, TestResponses.Redirect(302, location, seed));

        Assert.Equal("a=b", sent[1].Headers.Get("Cookie"));
        Assert.Equal("Basic cHJveHk6cHc=", sent[1].Headers.Get("Proxy-Authorization"));
        Assert.Equal("kept", sent[1].Headers.Get("X-Pass"));
    }

    [Theory]
    [InlineData("https://evil.example/b")]
    [InlineData("http://example.com/b")]
    [InlineData("https://example.com:8443/b")]
    [InlineData("//other.example/x")]
    public async Task Cookie_and_proxy_authorization_are_stripped_on_a_cross_origin_hop(string location)
    {
        var seed = Request.Create(Method.Get, "https://example.com/a", Credentials());
        var options = new DexpaceClientOptions { Redirect = new RedirectOptions { AllowHttpsToHttpDowngrade = true } };
        var transport = new ScriptedTransport(TestResponses.Redirect(302, location, seed), TestResponses.Create(Status.Ok, seed));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(seed, options, Ct);

        var hop = transport.Requests[1];
        Assert.Empty(hop.Headers.GetAll("Authorization"));
        Assert.Empty(hop.Headers.GetAll("Cookie"));
        Assert.Empty(hop.Headers.GetAll("Proxy-Authorization"));
        Assert.Equal("kept", hop.Headers.Get("X-Pass"));
    }

    [Fact]
    public async Task A_same_origin_sub_redirect_on_a_foreign_host_is_still_judged_against_the_seed()
    {
        // seed (example.com) -> hop 1 (other.example, cross-origin) -> hop 2 (other.example again). This pins that no
        // credential reaches either foreign hop. It does not distinguish a seed comparison from a previous-hop one:
        // stripping only removes headers and each hop is built from the already-stripped request, so both give the
        // same headers here (the phase 1 checklist's S3 row records this; the difference is auth stamping's, phase 6c).
        var seed = Request.Create(Method.Get, "https://example.com/a", Credentials());

        var sent = await FollowAsync(
            seed,
            TestResponses.Redirect(307, "https://other.example/b", seed),
            TestResponses.Redirect(307, "https://other.example/c", seed));

        foreach (var hop in sent.Skip(1))
        {
            Assert.Empty(hop.Headers.GetAll("Authorization"));
            Assert.Empty(hop.Headers.GetAll("Cookie"));
            Assert.Empty(hop.Headers.GetAll("Proxy-Authorization"));
        }
    }

    [Fact]
    public async Task Returning_to_the_seed_origin_does_not_restore_what_a_foreign_hop_stripped()
    {
        var seed = Request.Create(Method.Get, "https://example.com/a", Credentials());

        var sent = await FollowAsync(
            seed,
            TestResponses.Redirect(302, "https://other.example/b", seed),
            TestResponses.Redirect(302, "https://example.com/c", seed));

        var back = sent[2];
        Assert.Equal(new Uri("https://example.com/c"), back.Url);
        Assert.Empty(back.Headers.GetAll("Authorization"));
        Assert.Empty(back.Headers.GetAll("Cookie"));
        Assert.Empty(back.Headers.GetAll("Proxy-Authorization"));
    }

    [Theory]
    [InlineData("https://user:pass@other.example/x", "https://other.example/x")]
    [InlineData("https://user:pass@example.com/x?q=%2F#f", "https://example.com/x?q=%2F#f")]
    [InlineData("https://token@example.com/y", "https://example.com/y")]
    public async Task Userinfo_in_the_location_is_dropped_before_re_issue(string location, string expected)
    {
        var seed = Request.Get("https://example.com/a");

        var sent = await FollowAsync(seed, TestResponses.Redirect(302, location, seed));

        var hop = sent[1];
        Assert.Equal(string.Empty, hop.Url.UserInfo);
        Assert.Equal(expected, hop.Url.AbsoluteUri);
    }

    [Fact]
    public async Task Stripping_holds_under_every_redirect_option()
    {
        // REDIR-7 and REDIR-9 are MUSTs and the switch that once pretended to narrow them is gone (design 6.2, P6b-24):
        // no option, however permissive, re-exposes a credential on a cross-origin hop.
        var seed = Request.Create(Method.Get, "https://example.com/a", Credentials());
        var options = new DexpaceClientOptions
        {
            Redirect = new RedirectOptions
            {
                AllowHttpsToHttpDowngrade = true,
                FollowSeeOther = true,
                AllowedMethods = new HashSet<Method>
                {
                    Method.Get, Method.Head, Method.Post, Method.Put, Method.Patch, Method.Delete, Method.Options, Method.Trace, Method.Connect,
                },
                MaxRedirects = 10,
                Predicate = static _ => true,
            },
        };
        var transport = new ScriptedTransport(TestResponses.Redirect(302, "https://evil.example/b", seed), TestResponses.Create(Status.Ok, seed));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(seed, options, Ct);

        var hop = transport.Requests[1];
        Assert.Empty(hop.Headers.GetAll("Authorization"));
        Assert.Empty(hop.Headers.GetAll("Cookie"));
        Assert.Empty(hop.Headers.GetAll("Proxy-Authorization"));
    }
}
