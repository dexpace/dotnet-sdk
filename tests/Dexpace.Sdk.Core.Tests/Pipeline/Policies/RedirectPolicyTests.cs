// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Replaces the phase 1-5 class wholesale (phase 6b, task 1.9). The conforming facts are kept in substance; the non-conforming
// ones (POST rewritten to GET on 301/302, a 303 followed by default, a silent downgrade return, the default of 20 hops) are
// gone, and the matrix rows that cover them live in RedirectDecisionMatrixTests. The two strip facts moved to
// RedirectReissueTests. Ported from ruby-sdk step_test.rb: LifecycleTest (L776) as the lifecycle facts below, PredicateTest
// (L202) as the predicate facts, RebuildTest (L680) as the sync-parity pairs and the 303 rebuild through the pipeline.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

[Trait("Category", "Unit")]
public sealed class RedirectPolicyTests
{
    private const string Start = "https://api.example.com/v1/items";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Request Get(string url = Start) => Request.Get(url);

    private static DexpaceClientOptions Opts(RedirectOptions? redirect = null) => new() { Redirect = redirect ?? new RedirectOptions() };

    private static async Task<Response> SendAsync(HttpPipeline pipeline, Request request, DexpaceClientOptions options, bool sync) =>
        sync ? pipeline.Send(request, options, Ct) : await pipeline.SendAsync(request, options, Ct);

    private static HttpPipeline Build(ScriptedTransport transport, params HttpPipelinePolicy[] more)
    {
        var builder = new PipelineBuilder().Add(new RedirectPolicy());
        foreach (var policy in more)
        {
            builder.Add(policy);
        }

        return builder.Build(transport);
    }

    [Fact]
    public void Stage_is_Redirect() => Assert.Equal(PipelineStage.Redirect, new RedirectPolicy().Stage);

    // ---- conforming facts, kept ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_relative_Location_is_followed_against_the_current_hop(bool sync)
    {
        var transport = new ScriptedTransport(TestResponses.Redirect(302, "next"), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Get(), Opts(), sync);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(new Uri("https://api.example.com/v1/next"), transport.Requests[1].Url);
    }

    [Fact]
    public async Task A_200_is_passed_through()
    {
        var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Get(), Opts(), sync: false);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task A_301_on_a_GET_keeps_GET()
    {
        var transport = new ScriptedTransport(TestResponses.Redirect(301, "https://api.example.com/v2/items"), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Get(), Opts(), sync: false);

        Assert.Equal(Method.Get, transport.Requests[1].Method);
    }

    [Fact]
    public async Task A_302_without_a_Location_is_returned()
    {
        var transport = new ScriptedTransport(TestResponses.Create(Status.Found), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Get(), Opts(), sync: false);

        Assert.Equal(302, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData("http://[bad")]
    [InlineData("ftp://files.example.com/x")]
    [InlineData("mailto:ops@example.com")]
    public async Task A_malformed_or_non_http_Location_is_returned_unfollowed(string location)
    {
        var transport = new ScriptedTransport(TestResponses.Redirect(302, location), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Get(), Opts(), sync: false);

        Assert.Equal(302, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task Two_Location_values_are_returned_unfollowed()
    {
        var transport = new ScriptedTransport(RedirectFixtures.MultiLocation("/a", "/b"), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Get(), Opts(), sync: false);

        Assert.Equal(302, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_chained_multi_hop_redirect_follows_every_hop(bool sync)
    {
        var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "https://api.example.com/b"),
            TestResponses.Redirect(302, "https://api.example.com/c"),
            TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Get("https://api.example.com/a"), Opts(), sync);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(
            ["https://api.example.com/a", "https://api.example.com/b", "https://api.example.com/c"],
            transport.Requests.Select(r => r.Url.AbsoluteUri).ToArray());
    }

    [Fact]
    public async Task A_followed_https_to_http_hop_under_the_opt_in_is_sent()
    {
        var transport = new ScriptedTransport(TestResponses.Redirect(302, "http://api.example.com/x"), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Get(), Opts(new RedirectOptions { AllowHttpsToHttpDowngrade = true }), sync: false);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal("http", transport.Requests[1].Url.Scheme);
    }

    // ---- the new behaviour through the pipeline -------------------------------------------------------------------

    [Fact]
    public async Task A_POST_302_is_returned_not_rewritten_to_a_GET()
    {
        var transport = new ScriptedTransport(TestResponses.Redirect(302, "/next"), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport), Request.Post(Start, RequestBody.FromString("x")), Opts(), sync: false);

        Assert.Equal(302, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task An_opted_in_303_follows_as_a_bodiless_GET()
    {
        var transport = new ScriptedTransport(TestResponses.Redirect(303, "/next"), TestResponses.Create(Status.Ok));
        var post = Request.Post(Start, RequestBody.FromString("x"));

        using var response = await SendAsync(Build(transport), post, Opts(new RedirectOptions { FollowSeeOther = true }), sync: false);

        Assert.Equal(Method.Get, transport.Requests[1].Method);
        Assert.Null(transport.Requests[1].Body);
    }

    [Fact]
    public async Task An_allowed_POST_307_keeps_the_method_and_the_body()
    {
        var transport = new ScriptedTransport(TestResponses.Redirect(307, "/next"), TestResponses.Create(Status.Ok));
        var post = Request.Post(Start, RequestBody.FromString("x"));
        var redirect = new RedirectOptions { AllowedMethods = new HashSet<Method> { Method.Post } };

        using var response = await SendAsync(Build(transport), post, Opts(redirect), sync: false);

        Assert.Equal(Method.Post, transport.Requests[1].Method);
        Assert.Same(post.Body, transport.Requests[1].Body);
    }

    // ---- REDIR-22 lifecycle ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_superseded_response_is_disposed_before_the_next_send(bool sync)
    {
        var log = new List<string>();
        var transport = new ScriptedTransport(
            () => { log.Add("send:1"); return RedirectFixtures.TrackedRedirect("/next", log, "redirect"); },
            () => { log.Add("send:2"); return TestResponses.Create(Status.Ok); });

        using var response = await SendAsync(Build(transport), Get(), Opts(), sync);

        Assert.Equal(["send:1", "redirect:dispose", "send:2"], log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_downgrade_failure_disposes_the_response_once_and_throws_the_exception_unchanged(bool sync)
    {
        var log = new List<string>();
        using var transport = new ScriptedTransport(RedirectFixtures.TrackedRedirect("http://api.example.com/x", log, "redirect", 302));

        var ex = await Assert.ThrowsAsync<RedirectSchemeDowngradeException>(async () => await SendAsync(Build(transport), Get(), Opts(), sync));

        Assert.Equal(["redirect:dispose"], log);
        Assert.Contains("AllowHttpsToHttpDowngrade", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_replay_failure_disposes_the_response_once_and_throws_the_exception_unchanged(bool sync)
    {
        var log = new List<string>();
        using var transport = new ScriptedTransport(RedirectFixtures.TrackedRedirect("/next", log, "redirect"));
        var post = Request.Post(Start, RequestBody.FromStream(new MemoryStream([1, 2, 3])));
        var redirect = new RedirectOptions { AllowedMethods = new HashSet<Method> { Method.Post } };

        var ex = await Assert.ThrowsAsync<RedirectBodyNotReplayableException>(async () => await SendAsync(Build(transport), post, Opts(redirect), sync));

        Assert.Equal(["redirect:dispose"], log);
        Assert.Contains("ToReplayableAsync", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_throwing_predicate_disposes_the_response_and_propagates_unchanged(bool sync)
    {
        var log = new List<string>();
        var boom = new InvalidOperationException("predicate failed");
        using var transport = new ScriptedTransport(RedirectFixtures.TrackedRedirect("/next", log, "redirect"));
        var redirect = new RedirectOptions { Predicate = _ => throw boom };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await SendAsync(Build(transport), Get(), Opts(redirect), sync));

        Assert.Same(boom, thrown);
        Assert.Equal(["redirect:dispose"], log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_dispose_failure_on_a_failing_hop_rides_the_primary_exceptions_suppressed_trail(bool sync)
    {
        var log = new List<string>();
        var disposeFailure = new IOException("dispose failed");
        var response = TestResponses.Create(
            Status.Found,
            headers: new Headers.Builder().Set("Location", "http://api.example.com/x").Build(),
            body: new TrackingResponseBody(log, "redirect", disposeFailure));
        using var transport = new ScriptedTransport(response);

        var ex = await Assert.ThrowsAsync<RedirectSchemeDowngradeException>(async () => await SendAsync(Build(transport), Get(), Opts(), sync));

        Assert.Equal(["redirect:dispose"], log);
        Assert.Contains(ExceptionTrail.GetSuppressed(ex), s => ReferenceEquals(s, disposeFailure));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public async Task Every_stop_reason_returns_the_response_undisposed(int reasonValue, bool sync)
    {
        var reason = (RedirectStopReason)reasonValue;
        var log = new List<string>();
        var (status, location, request, redirect) = reason switch
        {
            RedirectStopReason.NotARedirect => (200, "/next", Get(), new RedirectOptions()),
            RedirectStopReason.NotEligible => (302, "/next", Request.Post(Start, RequestBody.FromString("x")), new RedirectOptions()),
            RedirectStopReason.MalformedLocation => (302, "ftp://x/y", Get(), new RedirectOptions()),
            RedirectStopReason.LoopDetected => (302, Start, Get(), new RedirectOptions()),
            _ => (302, "/next", Get(), new RedirectOptions { MaxRedirects = 0 }),
        };
        var body = new TrackingResponseBody(log, "current");
        var response = TestResponses.Create(
            Status.FromCode(status),
            headers: new Headers.Builder().Set("Location", location).Build(),
            body: body);
        var transport = new ScriptedTransport(response);

        using var result = await SendAsync(Build(transport), request, Opts(redirect), sync);

        Assert.Same(response, result);
        Assert.Equal(0, body.DisposeCount);
        Assert.Equal(1, transport.CallCount);
    }

    // ---- loop, cap and stack safety -------------------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_to_B_to_A_returns_Bs_3xx_open(bool sync)
    {
        var log = new List<string>();
        var bBody = new TrackingResponseBody(log, "b");
        var bResponse = TestResponses.Create(
            Status.Found,
            headers: new Headers.Builder().Set("Location", "https://api.example.com/a").Build(),
            body: bBody);
        var transport = new ScriptedTransport(TestResponses.Redirect(302, "https://api.example.com/b"), bResponse);

        using var response = await SendAsync(Build(transport), Get("https://api.example.com/a"), Opts(), sync);

        Assert.Same(bResponse, response);
        Assert.Equal(0, bBody.DisposeCount);
        Assert.Equal(2, transport.CallCount);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(0, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(1, 2, true)]
    [InlineData(3, 4, false)]
    [InlineData(3, 4, true)]
    public async Task The_cap_at_0_1_and_3_returns_the_last_3xx_without_throwing(int max, int sends, bool sync)
    {
        var transport = new ScriptedTransport(RedirectFixtures.Chain(10));

        using var response = await SendAsync(Build(transport), Get(), Opts(new RedirectOptions { MaxRedirects = max }), sync);

        Assert.Equal(302, response.Status.Code);
        Assert.Equal(sends, transport.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_thousand_hop_chain_completes_under_a_raised_cap(bool sync)
    {
        var transport = new ScriptedTransport(RedirectFixtures.Chain(1000));

        using var response = await SendAsync(Build(transport), Get(), Opts(new RedirectOptions { MaxRedirects = 1000 }), sync);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(1001, transport.CallCount);
    }

    // ---- the predicate ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_predicate_cannot_reach_the_live_visited_set()
    {
        var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "https://api.example.com/b"),
            TestResponses.Redirect(302, "https://api.example.com/a"),
            TestResponses.Create(Status.Ok));
        var redirect = new RedirectOptions
        {
            Predicate = condition =>
            {
                // An attempt to forget a visited URI: either the cast fails or it writes to a copy.
                if (condition.VisitedUris is IList<Uri> { IsReadOnly: false } list)
                {
                    list.Clear();
                }

                return true;
            },
        };

        using var response = await SendAsync(Build(transport), Get("https://api.example.com/a"), Opts(redirect), sync: false);

        // The loop is still detected at the second 3xx: A to B to A.
        Assert.Equal(302, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task The_predicate_is_called_once_per_recognised_3xx_and_never_otherwise()
    {
        var calls = 0;
        var redirect = new RedirectOptions { Predicate = _ => { calls++; return true; } };

        foreach (var status in new[] { 200, 300, 304, 305 })
        {
            using var transport = new ScriptedTransport(TestResponses.Create(Status.FromCode(status), headers: new Headers.Builder().Set("Location", "/x").Build()));
            using var response = await SendAsync(Build(transport), Get(), Opts(redirect), sync: false);
        }

        Assert.Equal(0, calls);

        foreach (var location in new[] { string.Empty, "ftp://x/y" })
        {
            var headers = location.Length == 0 ? Headers.Empty : new Headers.Builder().Set("Location", location).Build();
            using var transport = new ScriptedTransport(TestResponses.Create(Status.Found, headers: headers));
            using var response = await SendAsync(Build(transport), Get(), Opts(redirect), sync: false);
        }

        Assert.Equal(2, calls);

        var capped = new ScriptedTransport(TestResponses.Redirect(302, "/next"));
        using (await SendAsync(Build(capped), Get(), Opts(redirect with { MaxRedirects = 0 }), sync: false))
        {
        }

        Assert.Equal(3, calls);
    }

    // ---- cancellation, the auth stage, request isolation -----------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_cancelled_token_between_hops_throws_after_disposing_and_before_the_next_send(bool sync)
    {
        using var cts = new CancellationTokenSource();
        var log = new List<string>();
        using var transport = new ScriptedTransport(
            () =>
            {
                cts.Cancel();
                return RedirectFixtures.TrackedRedirect("/next", log, "redirect");
            },
            TestResponses.Create(Status.Ok));
        var pipeline = Build(transport);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            _ = sync ? pipeline.Send(Get(), Opts(), cts.Token) : await pipeline.SendAsync(Get(), Opts(), cts.Token));

        Assert.Equal(["redirect:dispose"], log);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task An_auth_stage_probe_runs_once_per_hop()
    {
        var log = new List<string>();
        var probe = new ProbePolicy("auth", PipelineStage.Auth, log);
        var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "https://api.example.com/b"),
            TestResponses.Redirect(302, "https://api.example.com/c"),
            TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport, probe), Get(), Opts(), sync: false);

        Assert.Equal(3, probe.Entered);
    }

    [Fact]
    public async Task Each_hop_is_driven_with_the_policys_own_request()
    {
        // PIPE-15 / PIPE-16: a stamp written downstream during hop 1 never reaches hop 2.
        var log = new List<string>();
        var seen = new List<Request>();
        var observer = new DelegatePolicy(
            PipelineStage.PerHop,
            (request, context, next) =>
            {
                seen.Add(request);
                return next.RunAsync(request, context);
            });
        var stamper = new DelegatePolicy(
            PipelineStage.Auth,
            (request, context, next) => next.RunAsync(request.WithHeader("X-Stamp", "downstream"), context));
        var transport = new ScriptedTransport(TestResponses.Redirect(302, "/next"), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Build(transport, observer, stamper), Get(), Opts(), sync: false);

        Assert.Equal(2, seen.Count);
        Assert.All(seen, r => Assert.False(r.Headers.Contains("X-Stamp")));
        Assert.All(transport.Requests, r => Assert.True(r.Headers.Contains("X-Stamp")));
        Assert.Empty(log);
    }
}
