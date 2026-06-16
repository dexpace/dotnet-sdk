// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

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
        const string redirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect302(redirectUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        // Act
        var result = await pipeline.SendAsync(MakePostRequest(), MakeOptions());

        // Assert
        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);

        var secondRequest = transport.Requests[1];
        Assert.Equal(Method.Get, secondRequest.Method);
        Assert.Null(secondRequest.Body);
        Assert.Equal(new Uri(redirectUrl), secondRequest.Url);
    }

    // -------------------------------------------------------------------------
    // 307 preserves method and body
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_307OnPost_PreservesMethodAndBody()
    {
        const string redirectUrl = "https://api.example.com/v2/items";
        var body = RequestBody.FromBytes(new byte[] { 1, 2, 3 });
        var originalRequest = Request.Post("https://api.example.com/v1/items", body);

        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect(307, redirectUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(originalRequest, MakeOptions());

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);

        var secondRequest = transport.Requests[1];
        Assert.Equal(Method.Post, secondRequest.Method);
        Assert.NotNull(secondRequest.Body);
        Assert.Equal(new Uri(redirectUrl), secondRequest.Url);
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
            ScriptedTransport.Redirect302("../v2/items"),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(
            MakeGetRequest("https://api.example.com/v1/items"),
            MakeOptions());

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
        const string loc = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect302(loc),
            ScriptedTransport.Redirect302(loc),
            ScriptedTransport.Redirect302(loc),
            new Response(Status.Ok), // never reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions(maxRedirects: 2));

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

        const string crossOriginUrl = "https://other.example.org/v1/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect302(crossOriginUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(request, MakeOptions(stripSensitiveHeadersOnCrossOrigin: true));

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);

        var secondRequest = transport.Requests[1];
        Assert.Null(secondRequest.Headers.Get("authorization"));
        Assert.Null(secondRequest.Headers.Get("cookie"));
    }

    // -------------------------------------------------------------------------
    // Same-origin hop does NOT strip Authorization
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_SameOriginRedirect_KeepsAuthorizationHeader()
    {
        var headers = new Headers.Builder()
            .Set("Authorization", "Bearer token123")
            .Build();
        var request = Request.Create(Method.Get, "https://api.example.com/v1/items", headers);

        // Same host, different path
        const string sameOriginUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect302(sameOriginUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(request, MakeOptions(stripSensitiveHeadersOnCrossOrigin: true));

        Assert.Equal(Status.Ok, result.Status);
        var secondRequest = transport.Requests[1];
        Assert.Equal("Bearer token123", secondRequest.Headers.Get("authorization"));
    }

    // -------------------------------------------------------------------------
    // HTTPS → HTTP downgrade rejected when flag is false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_HttpsToHttpDowngrade_NotFollowed_WhenFlagFalse()
    {
        const string httpUrl = "http://api.example.com/v1/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect302(httpUrl),
            new Response(Status.Ok), // never reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        // allowHttpsToHttpDowngrade defaults to false
        var result = await pipeline.SendAsync(
            MakeGetRequest("https://api.example.com/secure"),
            MakeOptions(allowHttpsToHttpDowngrade: false));

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
        const string httpUrl = "http://api.example.com/v1/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect302(httpUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(
            MakeGetRequest("https://api.example.com/secure"),
            MakeOptions(allowHttpsToHttpDowngrade: true));

        Assert.Equal(Status.Ok, result.Status);
        Assert.Equal(2, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // 303 always becomes GET regardless of original method
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_303OnPut_BecomesGetWithNoBody()
    {
        const string redirectUrl = "https://api.example.com/v2/items";
        var body = RequestBody.FromBytes(new byte[] { 1, 2, 3 });
        var putRequest = Request.Create(Method.Put, "https://api.example.com/v1/items", body: body);

        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect(303, redirectUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(putRequest, MakeOptions());

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
        const string redirectUrl = "https://api.example.com/v2/items";
        var body = RequestBody.FromBytes(new byte[] { 1, 2, 3 });
        var originalRequest = Request.Post("https://api.example.com/v1/items", body);

        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect(308, redirectUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(originalRequest, MakeOptions());

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
        const string redirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect(301, redirectUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakePostRequest(), MakeOptions());

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
        const string redirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect(301, redirectUrl),
            new Response(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions());

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
        var transport = new ScriptedTransport([new Response(Status.Ok)]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions());

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
            new Response(Status.FromCode(302), Headers.Empty),
            new Response(Status.Ok), // never reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions());

        Assert.Equal(302, result.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    // -------------------------------------------------------------------------
    // Non-replayable body with body-preserving redirect is not followed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_307OnPost_NonReplayableBody_NotFollowed()
    {
        const string redirectUrl = "https://api.example.com/v2/items";
        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect(307, redirectUrl),
            new Response(Status.Ok), // never reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        // Non-replayable body — cannot re-send
        var result = await pipeline.SendAsync(MakePostRequest(replayable: false), MakeOptions());

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
            new Response(Status.FromCode(302), malformedHeaders),
            new Response(Status.Ok), // must never be reached
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        // No exception should escape the pipeline.
        var result = await pipeline.SendAsync(MakeGetRequest(), MakeOptions());

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
        const string urlA = "https://api.example.com/a";
        const string urlB = "https://api.example.com/b";
        const string urlC = "https://api.example.com/c";

        var transport = new ScriptedTransport(
        [
            ScriptedTransport.Redirect302(urlB),  // A → B
            ScriptedTransport.Redirect302(urlC),  // B → C
            new Response(Status.Ok),              // C → 200
        ]);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        var result = await pipeline.SendAsync(
            MakeGetRequest(urlA),
            MakeOptions(maxRedirects: 10));

        // Final response is 200.
        Assert.Equal(Status.Ok, result.Status);

        // Transport was called exactly 3 times.
        Assert.Equal(3, transport.CallCount);

        // URLs progressed A → B → C.
        Assert.Equal(new Uri(urlA), transport.Requests[0].Url);
        Assert.Equal(new Uri(urlB), transport.Requests[1].Url);
        Assert.Equal(new Uri(urlC), transport.Requests[2].Url);
    }

    // -------------------------------------------------------------------------
    // Scripted transport helper
    // -------------------------------------------------------------------------

    private sealed class ScriptedTransport : IAsyncHttpClient
    {
        private readonly List<object> _script;
        private int _callCount;
        private readonly List<Request> _requests = [];

        public ScriptedTransport(IEnumerable<object> script)
        {
            _script = [.. script];
        }

        public int CallCount => _callCount;
        public List<Request> Requests => _requests;

        public static Response Redirect302(string location)
        {
            var h = new Headers.Builder().Set("Location", location).Build();
            return new Response(Status.FromCode(302), h);
        }

        public static Response Redirect(int statusCode, string location)
        {
            var h = new Headers.Builder().Set("Location", location).Build();
            return new Response(Status.FromCode(statusCode), h);
        }

        public Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default)
        {
            _requests.Add(request);
            var index = Interlocked.Increment(ref _callCount) - 1;
            if (index >= _script.Count)
            {
                throw new InvalidOperationException($"Script ran out of entries at call {index + 1}.");
            }

            var entry = _script[index];
            return entry switch
            {
                Response r => Task.FromResult(r),
                Exception ex => Task.FromException<Response>(ex),
                _ => throw new InvalidOperationException($"Unknown script entry type: {entry.GetType()}"),
            };
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
