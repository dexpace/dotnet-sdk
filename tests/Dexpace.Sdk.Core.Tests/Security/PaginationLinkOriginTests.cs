// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Pagination;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Phase 7c's one permanent security regression (design P7c-12, facts 4 and 9; PAGE-19): a server must not be able to take a
/// credential with one <c>Link</c> header. Each page is a fresh pipeline call, and the authorization policies stamp the
/// credential when a request is same-origin with <em>its own call's</em> first request, so a <c>next</c> target on another
/// origin would have received the bearer token. The pipeline here is the real stack (a <see cref="BearerTokenAuthPolicy"/>
/// over a scripted transport) and every assertion runs on every request the transport saw. The origin the guard compares is
/// the walk's first request's, not the response's, so a first page that redirected to another origin cannot launder it.
/// Tagged <c>Security</c> on the precedent of <c>RedirectCredentialLeakTests</c> (P6b-25). Never delete or loosen.
/// </summary>
[Trait("Category", "Security")]
public sealed class PaginationLinkOriginTests
{
    private static readonly Request s_first = Request.Get("https://api.example/items");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Headers Link(string value) => new Headers.Builder().Add("Link", value).Build();

    private static IPageStrategy<Envelope, int> LinkStrategy(bool allowCrossOrigin = false) =>
        PaginationStrategies.LinkHeader<Envelope, int>(e => e.Items, allowCrossOrigin: allowCrossOrigin);

    private static async Task<List<int>> Walk(HttpPipeline pipeline, bool allowCrossOrigin = false)
    {
        var items = new List<int>();
        var pageable = Pageable.Create<Envelope, int>(pipeline, s_first, new EnvelopeSerde(), LinkStrategy(allowCrossOrigin));
        await foreach (var item in pageable.WithCancellation(Ct))
        {
            items.Add(item);
        }

        return items;
    }

    [Theory]
    [InlineData("<https://evil.example/p2>; rel=next")]
    [InlineData("<//evil.example/p2>; rel=next")]
    [InlineData("<http://api.example/p2>; rel=next")]
    [InlineData("<https://api.example:8443/p2>; rel=next")]
    public async Task A_cross_origin_next_link_ends_the_walk_and_no_request_reaches_the_other_origin(string link)
    {
        using var transport = new ScriptedTransport(
            (Func<Request, Response>)(r => PageFixtures.Respond([1], null, Link(link))(r)),
            (Func<Request, Response>)(r => PageFixtures.Respond([2])(r)));
        using var pipeline = new PipelineBuilder().Add(new BearerTokenAuthPolicy(new FixedTokenCredential("tok"), "scope")).Build(transport);

        var items = await Walk(pipeline);

        // The first request carried the credential, so the next one would have too: the guard is what stops it.
        Assert.Equal(["Bearer tok"], transport.Requests[0].Headers.GetAll("Authorization"));
        Assert.Equal([1], items);
        Assert.Equal(1, transport.CallCount);
        Assert.All(transport.Requests, r =>
        {
            Assert.Equal("api.example", r.Url.Host);
            Assert.Equal(443, r.Url.Port);
            Assert.Equal("https", r.Url.Scheme);
        });
    }

    [Fact]
    public async Task A_same_origin_next_link_is_followed_and_carries_the_policys_own_stamp()
    {
        using var transport = new ScriptedTransport(
            (Func<Request, Response>)(r => PageFixtures.Respond([1], null, Link("</items?page=2>; rel=next"))(r)),
            (Func<Request, Response>)(r => PageFixtures.Respond([2])(r)));
        using var pipeline = new PipelineBuilder().Add(new BearerTokenAuthPolicy(new FixedTokenCredential("tok"), "scope")).Build(transport);

        var items = await Walk(pipeline);

        Assert.Equal([1, 2], items);
        Assert.Equal(["Bearer tok"], transport.Requests[1].Headers.GetAll("Authorization"));
    }

    [Fact]
    public async Task A_userinfo_bearing_same_origin_target_is_followed_with_the_userinfo_removed()
    {
        using var transport = new ScriptedTransport(
            (Func<Request, Response>)(r => PageFixtures.Respond([1], null, Link("<https://planted:secret@api.example/items?page=2>; rel=next"))(r)),
            (Func<Request, Response>)(r => PageFixtures.Respond([2])(r)));
        using var pipeline = new PipelineBuilder().Add(new BearerTokenAuthPolicy(new FixedTokenCredential("tok"), "scope")).Build(transport);

        var items = await Walk(pipeline);

        Assert.Equal([1, 2], items);
        var second = transport.Requests[1];
        Assert.Equal(string.Empty, second.Url.UserInfo);
        Assert.DoesNotContain("planted", second.Url.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", second.Url.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(["Bearer tok"], second.Headers.GetAll("Authorization"));
    }

    [Theory]
    [InlineData("<https://other.example/p2>; rel=next")]
    [InlineData("</p2>; rel=next")]
    public async Task A_first_page_that_redirected_to_another_origin_does_not_license_following_links_there(string link)
    {
        // The template's origin is api.example. The first page redirects to other.example, whose response carries a link
        // to other.example. Comparing against the response URL would follow it (laundering the redirect); comparing against
        // the template, as P7c-12 requires, ends the walk with exactly the redirect hop and nothing at /p2.
        using var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "https://other.example/items"),
            (Func<Request, Response>)(r => PageFixtures.Respond([1], null, Link(link))(r)),
            (Func<Request, Response>)(r => PageFixtures.Respond([2])(r)));
        using var pipeline = new PipelineBuilder()
            .Add(new RedirectPolicy())
            .Add(new BearerTokenAuthPolicy(new FixedTokenCredential("tok"), "scope"))
            .Build(transport);

        var items = await Walk(pipeline);

        Assert.Equal([1], items);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal("api.example", transport.Requests[0].Url.Host);
        Assert.Equal("other.example", transport.Requests[1].Url.Host);
        Assert.DoesNotContain(transport.Requests, r => r.Url.AbsolutePath.EndsWith("/p2", StringComparison.Ordinal));

        // The credential did not follow the redirect off the seed origin either (REDIR-7), so the walk leaked nothing.
        Assert.Empty(transport.Requests[1].Headers.GetAll("Authorization"));
    }

    private sealed class FixedTokenCredential(string token) : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            new(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1)));
    }
}
