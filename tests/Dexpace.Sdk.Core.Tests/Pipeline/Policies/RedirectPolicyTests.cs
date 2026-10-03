// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

[Trait("Category", "Unit")]
public sealed class RedirectPolicyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Request MakeGetRequest(string url = "https://api.example.com/v1/items") =>
        Request.Get(url);

    private static Request MakePostRequest(string url = "https://api.example.com/v1/items", bool replayable = true)
    {
        var body = replayable
            ? RequestBody.FromBytes(ReadOnlyMemory<byte>.Empty)
            : RequestBody.FromStream(new MemoryStream([1, 2, 3]));
        return Request.Post(url, body);
    }

    private static DexpaceClientOptions MakeOptions(
        int maxRedirects = 10,
        bool allowHttpsToHttpDowngrade = false,
        bool stripSensitiveHeadersOnCrossOrigin = true) =>
        new()
        {
            Redirect = new RedirectOptions
            {
                MaxRedirects = maxRedirects,
                AllowHttpsToHttpDowngrade = allowHttpsToHttpDowngrade,
                StripSensitiveHeadersOnCrossOrigin = stripSensitiveHeadersOnCrossOrigin,
            }
        };

    // -------------------------------------------------------------------------
    // Stage
    // -------------------------------------------------------------------------

    [Fact]
    public void Stage_IsRedirect()
    {
        Assert.Equal(PipelineStage.Redirect, new RedirectPolicy().Stage);
    }

    // -------------------------------------------------------------------------
    // 302 POST → GET (method downgrade)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_302OnPost_BecomesGetWithNoBody()
    {
        // Arrange: POST → 302 to /v2/items → 200 OK
        const string RedirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(302, RedirectUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        // Act
        var result = await pipeline.SendAsync(MakePostRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);

        var secondRequest = transport.Requests[1];
        Assert.Equal(Method.Get, secondRequest.Method);
        Assert.Null(secondRequest.Body);
        Assert.Equal(new Uri(RedirectUrl), secondRequest.Url);
    }

    // -------------------------------------------------------------------------
    // Phase 2a (HTTP-7): the hop is built through Request's validating constructor
    // -------------------------------------------------------------------------

    [Fact]
    public async Task A_303_on_a_POST_with_a_body_follows_as_a_bodiless_GET()
    {
        // Pin through the new constructor: the hop is one new Request(...), so the body is cleared with the method.
        const string RedirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport([TestResponses.Redirect(303, RedirectUrl), TestResponses.Create(Status.Ok)]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);
        var post = Request.Post("https://api.example.com/v1/items", RequestBody.FromBytes(new byte[] { 1, 2, 3 }));

        var result = await pipeline.SendAsync(post, MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal(Method.Get, transport.Requests[1].Method);
        Assert.Null(transport.Requests[1].Body);
    }

    [Theory]
    [InlineData("ftp://files.example.com/x")]
    [InlineData("mailto:ops@example.com")]
    [InlineData("file:///etc/passwd")]
    public async Task A_Location_that_is_not_http_or_https_returns_the_3xx_unfollowed(string location)
    {
        var tracking = new DisposalTrackingBody();
        var redirect = TestResponses.Create(Status.FromCode(302), headers: new Headers.Builder().Set("Location", location).Build(), body: tracking);
        var transport = new ScriptedTransport([redirect]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(1, transport.CallCount);
        Assert.Same(redirect, result);
        Assert.Equal(302, result.Status.Code);
        Assert.False(tracking.Disposed);
    }

    private sealed class DisposalTrackingBody : ResponseBody
    {
        public bool Disposed { get; private set; }

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    // -------------------------------------------------------------------------
    // 307 preserves method and body
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_307OnPost_PreservesMethodAndBody()
    {
        const string RedirectUrl = "https://api.example.com/v2/items";
        var body = RequestBody.FromBytes(new byte[] { 1, 2, 3 });
        var originalRequest = Request.Post("https://api.example.com/v1/items", body);

        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(307, RedirectUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(originalRequest, MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);

        var secondRequest = transport.Requests[1];
        Assert.Equal(Method.Post, secondRequest.Method);
        Assert.NotNull(secondRequest.Body);
        Assert.Equal(new Uri(RedirectUrl), secondRequest.Url);
    }

    // -------------------------------------------------------------------------
    // Relative Location resolves against current URL
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_RelativeLocation_ResolvesAgainstCurrentUrl()
    {
        // Arrange: GET /v1/items → 302 ../v2/items → 200
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(302, "../v2/items"),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest("https://api.example.com/v1/items"), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);

        // new Uri(new Uri("https://api.example.com/v1/items"), "../v2/items") = https://api.example.com/v2/items
        Assert.Equal(new Uri("https://api.example.com/v2/items"), transport.Requests[1].Url);
    }

    // -------------------------------------------------------------------------
    // MaxRedirects respected — stops and returns the last 3xx
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_MaxRedirects_StopsAndReturnsLast3xxResponse()
    {
        // MaxRedirects = 2: initial + 2 hops = 3 transport calls; 3rd call still 302 → return it
        const string Location = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(302, Location),
            TestResponses.Redirect(302, Location),
            TestResponses.Redirect(302, Location),
            TestResponses.Create(Status.Ok), // never reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions(maxRedirects: 2), TestContext.Current.CancellationToken);

        // Should stop after 2 redirects and return the last 3xx (the 3rd call)
        Assert.Equal(302, result.Status.Code);
        Assert.Equal(3, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // Cross-origin hop strips Authorization header
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_CrossOriginRedirect_StripsAuthorizationAndCookieHeaders()
    {
        // Arrange: request with Authorization, redirect to different host
        var headers = new Headers.Builder()
            .Set("Authorization", "Bearer token123")
            .Set("Cookie", "session=abc")
            .Build();
        var request = Request.Create(Method.Get, "https://api.example.com/v1/items", headers);

        const string CrossOriginUrl = "https://other.example.org/v1/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(302, CrossOriginUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(request, MakeOptions(stripSensitiveHeadersOnCrossOrigin: true), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);

        var secondRequest = transport.Requests[1];
        Assert.Null(secondRequest.Headers.Get("authorization"));
        Assert.Null(secondRequest.Headers.Get("cookie"));
    }

    // -------------------------------------------------------------------------
    // Same-origin hop strips Authorization too (REDIR-7: every hop; the auth policy re-stamps per hop)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_SameOriginRedirect_StripsAuthorizationHeader()
    {
        var headers = new Headers.Builder()
            .Set("Authorization", "Bearer token123")
            .Build();
        var request = Request.Create(Method.Get, "https://api.example.com/v1/items", headers);

        // Same host, different path
        const string SameOriginUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(302, SameOriginUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(request, MakeOptions(stripSensitiveHeadersOnCrossOrigin: true), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        var secondRequest = transport.Requests[1];
        Assert.Null(secondRequest.Headers.Get("authorization"));
    }

    // -------------------------------------------------------------------------
    // HTTPS → HTTP downgrade rejected when flag is false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_HttpsToHttpDowngrade_NotFollowed_WhenFlagFalse()
    {
        const string HttpUrl = "http://api.example.com/v1/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(302, HttpUrl),
            TestResponses.Create(Status.Ok), // never reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        // allowHttpsToHttpDowngrade defaults to false
        var result = await pipeline.SendAsync(MakeGetRequest("https://api.example.com/secure"), MakeOptions(allowHttpsToHttpDowngrade: false), TestContext.Current.CancellationToken);

        // Should return the 302 without following
        Assert.Equal(302, result.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // HTTPS → HTTP downgrade followed when flag is true
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_HttpsToHttpDowngrade_Followed_WhenFlagTrue()
    {
        const string HttpUrl = "http://api.example.com/v1/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(302, HttpUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest("https://api.example.com/secure"), MakeOptions(allowHttpsToHttpDowngrade: true), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // 303 always becomes GET regardless of original method
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_303OnPut_BecomesGetWithNoBody()
    {
        const string RedirectUrl = "https://api.example.com/v2/items";
        var body = RequestBody.FromBytes(new byte[] { 1, 2, 3 });
        var putRequest = Request.Create(Method.Put, "https://api.example.com/v1/items", body: body);

        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(303, RedirectUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(putRequest, MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        var secondRequest = transport.Requests[1];
        Assert.Equal(Method.Get, secondRequest.Method);
        Assert.Null(secondRequest.Body);
    }

    // -------------------------------------------------------------------------
    // 308 preserves method and body
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_308OnPost_PreservesMethodAndBody()
    {
        const string RedirectUrl = "https://api.example.com/v2/items";
        var body = RequestBody.FromBytes(new byte[] { 1, 2, 3 });
        var originalRequest = Request.Post("https://api.example.com/v1/items", body);

        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(308, RedirectUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(originalRequest, MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        var secondRequest = transport.Requests[1];
        Assert.Equal(Method.Post, secondRequest.Method);
        Assert.NotNull(secondRequest.Body);
    }

    // -------------------------------------------------------------------------
    // 301 on POST → GET (legacy browser behavior)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_301OnPost_BecomesGet()
    {
        const string RedirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(301, RedirectUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakePostRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        var secondRequest = transport.Requests[1];
        Assert.Equal(Method.Get, secondRequest.Method);
        Assert.Null(secondRequest.Body);
    }

    // -------------------------------------------------------------------------
    // 301 on GET preserves GET
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_301OnGet_KeepsGet()
    {
        const string RedirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(301, RedirectUrl),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        var secondRequest = transport.Requests[1];
        Assert.Equal(Method.Get, secondRequest.Method);
    }

    // -------------------------------------------------------------------------
    // Non-redirect status codes are passed through
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_200_IsPassedThrough_NoRedirect()
    {
        var transport = new ScriptedTransport([TestResponses.Create(Status.Ok)]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(1, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // Missing Location header stops redirect
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_302WithNoLocationHeader_StopsRedirect()
    {
        // 302 with no Location header — must not follow
        var transport = new ScriptedTransport(
        [
            TestResponses.Create(Status.FromCode(302), headers: Headers.Empty),
            TestResponses.Create(Status.Ok), // never reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(302, result.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // Non-replayable body with body-preserving redirect is not followed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_307OnPost_NonReplayableBody_NotFollowed()
    {
        const string RedirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(307, RedirectUrl),
            TestResponses.Create(Status.Ok), // never reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        // Non-replayable body — cannot re-send
        var result = await pipeline.SendAsync(MakePostRequest(replayable: false), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(307, result.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // Malformed Location header — must not throw, must not follow
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_MalformedLocation_NoExceptionEscapes_3xxReturnedUnfollowed()
    {
        // "http://[bad" has an invalid IPv6 literal that causes new Uri(...) to throw
        // UriFormatException but Uri.TryCreate to return false — that is exactly the
        // boundary the fix guards.
        var malformedHeaders = new Headers.Builder().Set("Location", "http://[bad").Build();
        var transport = new ScriptedTransport(
        [
            TestResponses.Create(Status.FromCode(302), headers: malformedHeaders),
            TestResponses.Create(Status.Ok), // must never be reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        // No exception should escape the pipeline.
        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        // Redirect is not followed — the 3xx comes back to the caller.
        Assert.Equal(302, result.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // Multi-hop chained redirect A→B→C→200
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_ChainedMultiHopRedirect_FollowsAllHops_EndsAt200()
    {
        // A → B → C → 200
        const string UrlA = "https://api.example.com/a";
        const string UrlB = "https://api.example.com/b";
        const string UrlC = "https://api.example.com/c";

        var transport = new ScriptedTransport(
        [
            TestResponses.Redirect(302, UrlB),  // A → B
            TestResponses.Redirect(302, UrlC),  // B → C
            TestResponses.Create(Status.Ok),              // C → 200
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(UrlA), MakeOptions(maxRedirects: 10), TestContext.Current.CancellationToken);

        // Final response is 200.
        Assert.Equal(Status.Ok, result.Status);

        // Transport was called exactly 3 times.
        Assert.Equal(3, transport.CallCount);

        // URLs progressed A → B → C.
        Assert.Equal(new Uri(UrlA), transport.Requests[0].Url);
        Assert.Equal(new Uri(UrlB), transport.Requests[1].Url);
        Assert.Equal(new Uri(UrlC), transport.Requests[2].Url);
    }

    // -------------------------------------------------------------------------
    // Scripted transport helper
    // -------------------------------------------------------------------------

}
