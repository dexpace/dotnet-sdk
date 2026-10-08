// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Diagnostics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>OBS-28, OBS-29: the interim wiring of today's <c>RetryPolicy</c> to the event shape (P5c-6, P5c-7, P5c-20).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class RetryTraceEventsTests
{
    private static readonly Func<Response> s_serviceUnavailable = () => TestResponses.Create(Status.ServiceUnavailable);
    private static readonly Func<Response> s_notFound = () => TestResponses.Create(Status.NotFound);

    private static Request Get() => Request.Get("https://api.example.com/v1/items");

    private static Activity Operation(ActivityRecorder recorder) => Assert.Single(recorder.StartedOfKind(ActivityKind.Internal));

    [Fact]
    public async Task A_retried_failure_emits_attempt_failed_with_the_next_delay()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new ScriptedTransport(s_serviceUnavailable, () => TestResponses.Create(Status.Ok)));

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        var operation = Operation(recorder);
        var failed = Assert.Single(operation.Events);
        Assert.Equal("dexpace.attempt.failed", failed.Name);
        Assert.Equal("503", TracingFixtures.EventTag(failed, "error.type"));
        Assert.Equal(503, TracingFixtures.EventTag(failed, "http.response.status_code"));
        Assert.Equal(0, TracingFixtures.EventTag(failed, "http.request.resend_count"));
        Assert.IsType<double>(TracingFixtures.EventTag(failed, "dexpace.retry.delay"));
    }

    [Fact]
    public async Task A_retried_exception_emits_attempt_failed_with_the_exception_type()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(
            new ScriptedTransport(new ServiceRequestException("never sent"), (Func<Response>)(() => TestResponses.Create(Status.Ok))));

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        var failed = Assert.Single(Operation(recorder).Events);
        Assert.Equal(typeof(ServiceRequestException).FullName, TracingFixtures.EventTag(failed, "error.type"));
        Assert.Null(TracingFixtures.EventTag(failed, "http.response.status_code"));
    }

    [Fact]
    public async Task The_resend_count_of_a_later_failure_follows_the_transmission_ordinal()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(
            new ScriptedTransport(s_serviceUnavailable, s_serviceUnavailable, () => TestResponses.Create(Status.Ok)));

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [0, 1],
            Operation(recorder).Events.Select(e => (int)TracingFixtures.EventTag(e, "http.request.resend_count")!));
    }

    [Fact]
    public async Task A_spent_budget_on_a_would_retry_failure_records_exhaustion()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(
            new ScriptedTransport(s_serviceUnavailable, s_serviceUnavailable, s_serviceUnavailable),
            mapped: true,
            retries: 2);

        await Assert.ThrowsAsync<HttpResponseException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        var operation = Operation(recorder);
        Assert.Equal(
            ["dexpace.attempt.failed", "dexpace.attempt.failed", "dexpace.retry.exhausted", "exception"],
            TracingFixtures.EventNames(operation));
        Assert.Equal(3, TracingFixtures.EventTag(TracingFixtures.Event(operation, "dexpace.retry.exhausted"), "dexpace.retry.attempts"));
    }

    [Fact]
    public async Task A_non_replayable_request_is_not_exhausted_it_is_not_retried()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new ScriptedTransport(s_serviceUnavailable), mapped: true);
        var request = Request.Create(Method.Put, "https://api.example.com/v1/items", body: RequestBody.FromStream(new MemoryStream([1, 2, 3])));

        await Assert.ThrowsAsync<HttpResponseException>(async () => await pipeline.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(["exception"], TracingFixtures.EventNames(Operation(recorder)));
    }

    [Fact]
    public async Task A_request_that_cannot_be_resent_is_not_exhausted()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new ScriptedTransport(s_serviceUnavailable), mapped: true);

        await Assert.ThrowsAsync<HttpResponseException>(
            async () => await pipeline.SendAsync(new Request(Method.Post, new Uri("https://api.example.com/v1/items")), TestContext.Current.CancellationToken));

        Assert.Equal(["exception"], TracingFixtures.EventNames(Operation(recorder)));
    }

    [Fact]
    public async Task A_zero_budget_is_retries_off_not_exhausted()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new ScriptedTransport(s_serviceUnavailable), mapped: true, retries: 0);

        await Assert.ThrowsAsync<HttpResponseException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        Assert.Equal(["exception"], TracingFixtures.EventNames(Operation(recorder)));
    }

    [Fact]
    public async Task A_non_retryable_status_is_not_exhausted()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new ScriptedTransport(s_notFound), mapped: true);

        await Assert.ThrowsAsync<HttpResponseException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        Assert.Equal(["exception"], TracingFixtures.EventNames(Operation(recorder)));
    }

    [Fact]
    public async Task The_exhausted_event_follows_the_final_predicate()
    {
        // OBS-29, P6a-28: exhausted = the cap was spent while the condition and the re-send gate held and the effective count
        // was above zero. Each other combination ends without the event.
        async Task<string[]> RunAsync(IAsyncHttpClient transport, Request request, int retries)
        {
            using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
            var pipeline = TracingFixtures.Pipeline(transport, retries: retries);
            await Assert.ThrowsAnyAsync<Exception>(async () => await pipeline.SendAsync(request, TestContext.Current.CancellationToken));
            return TracingFixtures.EventNames(Operation(recorder));
        }

        static ScriptedTransport Thrower(int count) =>
            new(Enumerable.Range(0, count).Select(_ => (object)new ServiceRequestException("never sent")).ToArray());

        Assert.Equal(
            ["dexpace.attempt.failed", "dexpace.attempt.failed", "dexpace.retry.exhausted", "exception"],
            await RunAsync(Thrower(3), Get(), retries: 2));
        Assert.Equal(["exception"], await RunAsync(Thrower(1), Get(), retries: 0));
        Assert.Equal(
            ["exception"],
            await RunAsync(new ScriptedTransport(new InvalidOperationException("not retryable")), Get(), retries: 2));
        Assert.Equal(
            ["exception"],
            await RunAsync(Thrower(1), new Request(Method.Post, new Uri("https://api.example.com/v1/items")), retries: 2));
    }

    [Fact]
    public async Task A_new_retry_sequence_clears_an_earlier_exhaustion()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");

        // A test-only Redirect-stage policy drives its continuation twice, as a redirect hop would after 6a/6b: the first
        // run ends exhausted, the second run starts a fresh retry sequence and then fails with something else.
        var builder = new PipelineBuilder();
        builder.Add(new DelegatePolicy(PipelineStage.Redirect, async (request, context, next) =>
        {
            using (await next.RunAsync(request, context).ConfigureAwait(false))
            {
            }

            return await next.RunAsync(request, context).ConfigureAwait(false);
        }));
        builder.Add(new RetryPolicy(new InstantTimeProvider()));
        builder.Add(new InstrumentationPolicy());
        var pipeline = builder.Build(
            new ScriptedTransport(s_serviceUnavailable, s_serviceUnavailable, new InvalidOperationException("later")),
            new DexpaceClientOptions { Retry = new RetryOptions { MaxRetryAttempts = 1, BaseDelay = TimeSpan.FromMilliseconds(1) } });

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        // The first run ended exhausted but returned its 503. The second run's failure is not a would-retry failure, so
        // the stale record must not pair with it.
        var names = TracingFixtures.EventNames(Operation(recorder));
        Assert.Equal("exception", names[^1]);
        Assert.DoesNotContain("dexpace.retry.exhausted", names);
    }

    [Fact]
    public async Task The_retry_policy_still_returns_and_throws_exactly_what_it_did()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var failure = new ServiceRequestException("never sent");
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(_ => throw failure), retries: 1);

        var thrown = await Assert.ThrowsAsync<ServiceRequestException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
    }
}
