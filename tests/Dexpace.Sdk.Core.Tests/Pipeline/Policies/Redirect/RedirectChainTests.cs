// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>The per-call redirect state (REDIR-8, REDIR-11, REDIR-16, REDIR-20; HTTP-46; P6b-8, P6b-11, R5).</summary>
[Trait("Category", "Unit")]
public sealed class RedirectChainTests
{
    private static RedirectChain Chain(string url, string? seedUrl = null, Method? method = null)
    {
        var first = Request.Create(method ?? Method.Get, url);
        var seed = seedUrl is null ? first : Request.Get(seedUrl);
        return new RedirectChain(first, TestContexts.For(seed));
    }

    private static void Advance(RedirectChain chain, string url)
    {
        var target = new Uri(url);
        chain.Advance(Request.Get(target.AbsoluteUri), target);
    }

    [Fact]
    public void A_new_chain_is_seeded_with_the_first_requests_userinfo_free_key()
    {
        var chain = Chain("https://u:p@h/a");

        Assert.True(chain.Contains("https://h/a"));
        Assert.Equal(0, chain.Followed);
        Assert.Equal("https://h/a", RedirectChain.KeyOf(new Uri("https://u:p@h/a")));
    }

    [Fact]
    public void The_seed_origin_is_the_context_seed_requests_not_the_first_drive()
    {
        var chain = Chain("https://other.example/b", seedUrl: "https://seed.example/a", method: Method.Post);

        Assert.Equal(new HttpOrigin("https", "seed.example", 443), chain.SeedOrigin);

        // The original method is the policy's own first request's (P6b-4), not the seed's.
        Assert.Equal(Method.Post, chain.OriginalMethod);
    }

    [Fact]
    public void Advance_appends_the_target_key_and_counts_the_hop()
    {
        var chain = Chain("https://h/a");

        Advance(chain, "https://h/b");

        Assert.Equal(1, chain.Followed);
        Assert.True(chain.Contains("https://h/b"));
        Assert.Equal("https://h/b", chain.Current.Url.AbsoluteUri);
    }

    [Fact]
    public void Keys_compare_ordinally_and_keep_the_fragment()
    {
        var chain = Chain("https://h/a#x");

        Assert.True(chain.Contains(RedirectChain.KeyOf(new Uri("https://h/a#x"))));
        Assert.False(chain.Contains(RedirectChain.KeyOf(new Uri("https://h/a#y"))));
        Assert.False(chain.Contains(RedirectChain.KeyOf(new Uri("https://h/A#x"))));
    }

    [Fact]
    public void Re_spelled_equivalents_collide()
    {
        var chain = Chain("https://h/a");

        Assert.True(chain.Contains(RedirectChain.KeyOf(new Uri("HTTPS://H:443/a"))));
    }

    [Fact]
    public void Snapshot_copies_the_visited_list()
    {
        var chain = Chain("https://h/a");
        using var response = TestResponses.Redirect(302, "/b");
        var condition = chain.Snapshot(response, new Uri("https://h/b"));

        if (condition.VisitedUris is IList<Uri> list && !list.IsReadOnly)
        {
            list[0] = new Uri("https://evil/");
        }

        Assert.True(chain.Contains("https://h/a"));
        Assert.False(chain.Contains("https://evil/"));

        Advance(chain, "https://h/b");
        Assert.Single(condition.VisitedUris);
        Assert.Equal(2, chain.Snapshot(response, null).VisitedUris.Count);
    }

    [Fact]
    public void Snapshot_carries_the_response_the_count_and_the_target()
    {
        var chain = Chain("https://h/a");
        Advance(chain, "https://h/b");
        using var response = TestResponses.Redirect(302, "/c");

        var withTarget = chain.Snapshot(response, new Uri("https://h/c"));
        var without = chain.Snapshot(response, null);

        Assert.Same(response, withTarget.Response);
        Assert.Equal(1, withTarget.RedirectsFollowed);
        Assert.Equal("https://h/c", withTarget.Target!.AbsoluteUri);
        Assert.Equal(["https://h/a", "https://h/b"], withTarget.VisitedUris.Select(u => u.AbsoluteUri).ToArray());
        Assert.Null(without.Target);
    }
}
