// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>
/// PIPE-40: a re-driving policy disposes each superseded response before the next drive and returns the in-flight one,
/// undisposed, on every abandon path; a throwing dispose cannot mask the next drive's outcome.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReDriveLifecycleTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/start");

    private static DexpaceClientOptions Options(int maxRedirects = 20, int maxRetries = 3) => new()
    {
        Redirect = new RedirectOptions { MaxRedirects = maxRedirects },
        Retry = new RetryOptions { MaxRetryAttempts = maxRetries, BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) },
    };

    private static Response Redirect(string location, TrackingResponseBody body) =>
        TestResponses.Create(Status.TemporaryRedirect, headers: new Headers.Builder().Set("Location", location).Build(), body: body);

    [Fact]
    public async Task A_redirect_disposes_the_superseded_response_before_the_next_drive_and_returns_the_last_open()
    {
        var log = new List<string>();
        using var redirectBody = new TrackingResponseBody(log, "redirect");
        using var finalBody = new TrackingResponseBody(log, "final");
        var transport = new ScriptedTransport(
            () => { log.Add("send:1"); return Redirect("https://api.example.com/next", redirectBody); },
            () => { log.Add("send:2"); return TestResponses.Create(Status.Ok, body: finalBody); });
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(MakeRequest(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(["send:1", "redirect:dispose", "send:2"], log);
        Assert.Equal(0, finalBody.DisposeCount);
    }

    [Fact]
    public async Task A_retry_disposes_the_superseded_response_before_the_next_drive_and_returns_the_last_open()
    {
        var log = new List<string>();
        using var retriedBody = new TrackingResponseBody(log, "retried");
        using var finalBody = new TrackingResponseBody(log, "final");
        var transport = new ScriptedTransport(
            () => { log.Add("send:1"); return TestResponses.Create(Status.ServiceUnavailable, body: retriedBody); },
            () => { log.Add("send:2"); return TestResponses.Create(Status.Ok, body: finalBody); });
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(new InstantTimeProvider())).Build(transport);

        using var response = await pipeline.SendAsync(MakeRequest(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(["send:1", "retried:dispose", "send:2"], log);
        Assert.Equal(0, finalBody.DisposeCount);
    }

    [Fact]
    public async Task A_redirect_budget_exhausted_returns_the_in_flight_response_undisposed()
    {
        var log = new List<string>();
        var body = new TrackingResponseBody(log, "redirect");
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(new ScriptedTransport(Redirect("https://api.example.com/next", body)));

        using var response = await pipeline.SendAsync(MakeRequest(), Options(maxRedirects: 0), TestContext.Current.CancellationToken);

        Assert.Equal(Status.TemporaryRedirect, response.Status);
        Assert.Equal(0, body.DisposeCount);
    }

    [Fact]
    public async Task A_redirect_without_a_Location_returns_the_in_flight_response_undisposed()
    {
        var log = new List<string>();
        var body = new TrackingResponseBody(log, "redirect");
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(
            new ScriptedTransport(TestResponses.Create(Status.TemporaryRedirect, body: body)));

        using var response = await pipeline.SendAsync(MakeRequest(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.TemporaryRedirect, response.Status);
        Assert.Equal(0, body.DisposeCount);
    }

    [Fact]
    public async Task A_redirect_over_a_non_replayable_body_returns_the_in_flight_response_undisposed()
    {
        var log = new List<string>();
        var body = new TrackingResponseBody(log, "redirect");
        var request = Request.Post("https://api.example.com/start", RequestBody.FromStream(new MemoryStream([1, 2, 3])));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(new ScriptedTransport(Redirect("https://api.example.com/next", body)));

        using var response = await pipeline.SendAsync(request, Options(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.TemporaryRedirect, response.Status);
        Assert.Equal(0, body.DisposeCount);
    }

    [Fact]
    public async Task A_retry_budget_exhausted_returns_the_last_response_undisposed_and_disposes_the_earlier_one()
    {
        var log = new List<string>();
        using var first = new TrackingResponseBody(log, "first");
        using var last = new TrackingResponseBody(log, "last");
        var transport = new ScriptedTransport(
            TestResponses.Create(Status.ServiceUnavailable, body: first),
            TestResponses.Create(Status.ServiceUnavailable, body: last));
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(new InstantTimeProvider())).Build(transport);

        using var response = await pipeline.SendAsync(MakeRequest(), Options(maxRetries: 1), TestContext.Current.CancellationToken);

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, last.DisposeCount);
    }

    [Fact]
    public async Task A_throwing_dispose_of_a_superseded_response_does_not_mask_the_next_drives_outcome()
    {
        var log = new List<string>();
        using var redirectBody = new TrackingResponseBody(log, "redirect", disposeFailure: new IOException("dispose failed"));
        var transport = new ScriptedTransport(Redirect("https://api.example.com/next", redirectBody), TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(MakeRequest(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(1, redirectBody.DisposeCount);
    }

    [Fact]
    public void The_sync_paths_dispose_the_same_way()
    {
        var log = new List<string>();
        using var retriedBody = new TrackingResponseBody(log, "retried");
        var transport = new ScriptedTransport(
            () => { log.Add("send:1"); return TestResponses.Create(Status.ServiceUnavailable, body: retriedBody); },
            () => { log.Add("send:2"); return TestResponses.Create(Status.Ok); });
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(new InstantTimeProvider())).Build(transport);

        using var response = pipeline.Send(MakeRequest(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(["send:1", "retried:dispose", "send:2"], log);
    }
}
