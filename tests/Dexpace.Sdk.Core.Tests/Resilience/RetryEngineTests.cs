// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Net.Http;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.Core.Resilience;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static Dexpace.Sdk.Core.Tests.Resilience.EngineHarness;

namespace Dexpace.Sdk.Core.Tests.Resilience;

#pragma warning disable CA2201 // OutOfMemoryException is the fatal exception under test (design §5.2).
#pragma warning disable CA1859 // The helpers return Outcome so a script can mix successes and failures.

/// <summary>The shared retry loop (RETRY-8, RETRY-13, RETRY-14, RETRY-23 to RETRY-26, RETRY-30 to RETRY-35, RETRY-39 to RETRY-42, RETRY-45).</summary>
[Trait("Category", "Unit")]
public sealed class RetryEngineTests
{
    private static RetryEngine Engine(TimeProvider? clock = null, Func<double>? random = null) =>
        new(clock ?? new FakeTimeProvider(), random ?? (() => 0.5));

    private static Outcome Fail(string message = "x") => new Outcome.Failure(new ServiceRequestException(message));

    private static Outcome Ok() => new Outcome.Success(TestResponses.Create(Status.Ok));

    private static Outcome Unavailable(Headers? headers = null, ResponseBody? body = null) =>
        new Outcome.Success(TestResponses.Create(Status.ServiceUnavailable, headers: headers, body: body));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task Sends_at_most_maxRetries_plus_one_times(int retries)
    {
        var sends = 0;
        var run = new RetryRun(s_get, Options(retries), Script(_ => Fail(), _ => sends++));

        var outcome = await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Equal(retries + 1, sends);
        Assert.IsType<ServiceRequestException>(Error(outcome));
        Assert.Equal(retries, ExceptionTrail.GetSuppressed(Error(outcome)).Count);
    }

    [Fact]
    public async Task Zero_retries_sends_once_and_is_never_exhausted()
    {
        var exhausted = 0;
        var sends = 0;
        var run = new RetryRun(s_get, Options(0), Script(_ => Unavailable(), _ => sends++))
        {
            Observer = new RetryObserver(OnExhausted: _ => exhausted++),
        };

        var outcome = await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Equal(1, sends);
        Assert.Equal(0, exhausted);
        Assert.IsType<Outcome.Success>(outcome);
    }

    [Theory]
    [InlineData(true, true, 3)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 1)]
    public async Task The_condition_and_the_resend_gate_must_both_hold(bool retryableFailure, bool resendable, int expectedSends)
    {
        var sends = 0;
        Exception failure = retryableFailure ? new ServiceRequestException("x") : new InvalidOperationException("x");
        var run = new RetryRun(resendable ? s_get : s_barePost, Options(2), Script(_ => new Outcome.Failure(failure), _ => sends++));

        await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Equal(expectedSends, sends);
    }

    [Fact]
    public async Task A_non_retryable_failure_is_surfaced_unchanged_with_an_empty_trail()
    {
        var failure = new InvalidOperationException("no");
        var run = new RetryRun(s_get, Options(), Script(_ => new Outcome.Failure(failure)));

        var outcome = await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Same(failure, Error(outcome));
        Assert.Empty(ExceptionTrail.GetSuppressed(failure));
    }

    [Fact]
    public async Task The_trail_holds_every_prior_failure_oldest_first_and_never_the_surfaced_instance()
    {
        // A foreign exception keeps the trail in Data; an SdkException in Suppressed; a reused instance is never attached to itself.
        var foreign = new[] { new System.IO.IOException("1"), new System.IO.IOException("2"), new System.IO.IOException("3") };
        var foreignOutcome = await Engine().RunAsync(
            new RetryRun(s_get, Options(2), Script(n => new Outcome.Failure(foreign[n - 1]))),
            async: true,
            TestContext.Current.CancellationToken);

        Assert.Same(foreign[2], Error(foreignOutcome));
        Assert.Equal([foreign[0], foreign[1]], ExceptionTrail.GetSuppressed(foreign[2]));

        var sdk = new[] { new ServiceRequestException("1"), new ServiceRequestException("2"), new ServiceRequestException("3") };
        var sdkOutcome = await Engine().RunAsync(
            new RetryRun(s_get, Options(2), Script(n => new Outcome.Failure(sdk[n - 1]))),
            async: true,
            TestContext.Current.CancellationToken);

        Assert.Same(sdk[2], Error(sdkOutcome));
        Assert.Equal([sdk[0], sdk[1]], sdk[2].Suppressed);

        var reused = new ServiceRequestException("same");
        var reusedOutcome = await Engine().RunAsync(
            new RetryRun(s_get, Options(2), Script(_ => new Outcome.Failure(reused))),
            async: true,
            TestContext.Current.CancellationToken);

        Assert.Same(reused, Error(reusedOutcome));
        Assert.DoesNotContain(reused, reused.Suppressed);
    }

    [Fact]
    public async Task A_fatal_exception_passes_every_frame_untouched()
    {
        var fatal = new OutOfMemoryException();
        var sends = 0;
        var observed = 0;
        var run = new RetryRun(s_get, Options(), Script(_ => throw fatal, _ => sends++))
        {
            Observer = new RetryObserver((_, _) => observed++, _ => observed++, _ => observed++),
        };

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(
            async () => await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken));

        Assert.Same(fatal, thrown);
        Assert.Equal(1, sends);
        Assert.Equal(0, observed);
        Assert.Empty(ExceptionTrail.GetSuppressed(fatal));
    }

    [Fact]
    public async Task Cancellation_before_a_send_throws_with_the_trail_and_does_not_send()
    {
        var sends = 0;
        using var cts = new CancellationTokenSource();
        var first = new ServiceRequestException("first");
        var run = new RetryRun(s_get, Options(), Script(_ => new Outcome.Failure(first), _ => sends++))
        {
            // The hook runs before the (zero) wait, so the next send finds the call cancelled.
            Observer = new RetryObserver(OnAttemptFailed: (_, _) => cts.Cancel()),
        };

        var outcome = await Engine().RunAsync(run, async: true, cts.Token);

        var error = Assert.IsAssignableFrom<OperationCanceledException>(Error(outcome));
        Assert.Equal(1, sends);
        Assert.Equal([first], ExceptionTrail.GetSuppressed(error));
        Assert.True(cts.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task A_token_cancelled_before_the_first_send_sends_nothing()
    {
        var sends = 0;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var run = new RetryRun(s_get, Options(), Script(_ => Ok(), _ => sends++));

        var outcome = await Engine().RunAsync(run, async: true, cts.Token);

        Assert.IsAssignableFrom<OperationCanceledException>(Error(outcome));
        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task Cancellation_during_the_wait_surfaces_OperationCanceledException_with_the_token_still_signalled()
    {
        var clock = new RecordingFakeTimeProvider();
        using var cts = new CancellationTokenSource();
        var first = new ServiceRequestException("first");
        var run = new RetryRun(s_get, Options(baseMs: 5_000), Script(_ => new Outcome.Failure(first)));

        var task = Engine(clock).RunAsync(run, async: true, cts.Token).AsTask();
        while (clock.Timers.Count == 0)
        {
            await Task.Yield();
        }

        await cts.CancelAsync();
        var outcome = await task;

        var error = Assert.IsAssignableFrom<OperationCanceledException>(Error(outcome));
        Assert.True(cts.Token.IsCancellationRequested);
        Assert.Equal([first], ExceptionTrail.GetSuppressed(error));
    }

    [Fact]
    public async Task A_success_that_arrives_after_the_token_fired_is_disposed_and_the_call_throws()
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log, "late");
        using var cts = new CancellationTokenSource();
        var run = new RetryRun(
            s_get,
            Options(),
            Script(_ =>
            {
                cts.Cancel();
                return new Outcome.Success(TestResponses.Create(Status.Ok, body: body));
            }));

        var outcome = await Engine().RunAsync(run, async: true, cts.Token);

        Assert.IsAssignableFrom<OperationCanceledException>(Error(outcome));
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_zero_delay_continues_inline_without_arming_a_timer()
    {
        var clock = new RecordingFakeTimeProvider();
        var run = new RetryRun(s_get, Options(3), Script(_ => Fail()));

        await Engine(clock).RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Empty(clock.Timers);
    }

    [Fact]
    public async Task Ten_thousand_retries_with_zero_delay_keep_constant_stack_depth()
    {
        var depths = new List<int>();
        var run = new RetryRun(
            s_get,
            Options(10_000),
            (_, _, _, _) =>
            {
                depths.Add(new StackTrace().FrameCount);
                return ValueTask.FromResult<Outcome>(Fail());
            });

        await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Equal(10_001, depths.Count);
        Assert.Equal(depths[0], depths.Max());
    }

    private static async Task<List<TimeSpan>> DelaysAsync(RetryOptions options, Outcome outcome, RetryDelayOverride? over = null, bool honor = true)
    {
        var delays = new List<TimeSpan>();
        var run = new RetryRun(s_get, options, Script(n => n == 1 ? outcome : Ok()))
        {
            MaxRetries = 1,
            HonorPacing = honor,
            DelayOverride = over,
            Observer = new RetryObserver((_, d) => delays.Add(d)),
        };
        var clock = new FakeTimeProvider();

        await DriveAsync(Engine(clock).RunAsync(run, async: true, TestContext.Current.CancellationToken).AsTask(), clock);
        return delays;
    }

    [Fact]
    public async Task Delay_precedence_is_override_then_pacing_then_fixed_then_backoff()
    {
        var hinted = new Headers.Builder().Set("Retry-After", "3").Build();
        var fixedOptions = Options(baseMs: 200, fixedDelay: TimeSpan.FromSeconds(2));

        // override > pacing > fixed > backoff
        Assert.Equal([TimeSpan.FromSeconds(5)], await DelaysAsync(fixedOptions, Unavailable(hinted), _ => TimeSpan.FromSeconds(5)));
        Assert.Equal([TimeSpan.FromSeconds(3)], await DelaysAsync(fixedOptions, Unavailable(hinted)));
        Assert.Equal([TimeSpan.FromSeconds(2)], await DelaysAsync(fixedOptions, Unavailable()));
        Assert.Equal([TimeSpan.FromMilliseconds(200)], await DelaysAsync(Options(baseMs: 200), Unavailable()));

        // HonorPacing off ignores the hint.
        Assert.Equal([TimeSpan.FromSeconds(2)], await DelaysAsync(fixedOptions, Unavailable(hinted), honor: false));

        // A hint is not jittered (RETRY-20).
        Assert.Equal([TimeSpan.FromSeconds(3)], await DelaysAsync(Options(baseMs: 200, jitter: 1), Unavailable(hinted)));
    }

    [Fact]
    public async Task A_throwing_or_negative_override_is_logged_and_falls_back()
    {
        var reported = new List<Exception>();
        var delays = new List<TimeSpan>();
        foreach (RetryDelayOverride over in new RetryDelayOverride[]
        {
            _ => throw new InvalidOperationException("boom"),
            _ => TimeSpan.FromSeconds(-1),
        })
        {
            var run = new RetryRun(s_get, Options(baseMs: 200), Script(n => n == 1 ? Fail() : Ok()))
            {
                DelayOverride = over,
                Observer = new RetryObserver((_, d) => delays.Add(d), null, reported.Add),
            };
            var clock = new FakeTimeProvider();

            await DriveAsync(Engine(clock).RunAsync(run, async: true, TestContext.Current.CancellationToken).AsTask(), clock);
        }

        Assert.Equal(2, reported.Count);
        Assert.Equal([TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200)], delays);
    }

    [Fact]
    public async Task A_throwing_pacing_read_degrades_to_no_hint_and_the_upstream_failure_is_still_the_one_surfaced()
    {
        var headers = new Headers.Builder().Set("X-RateLimit-Reset", "9999999999").Build();
        var upstream = new HttpResponseException(TestResponses.Create(Status.ServiceUnavailable, headers: headers));
        var run = new RetryRun(s_get, Options(1, baseMs: 0, jitter: 0.2), Script(_ => new Outcome.Failure(upstream)));
        var engine = Engine(random: () => throw new InvalidOperationException("random"));

        var outcome = await engine.RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Same(upstream, Error(outcome));
    }

    [Fact]
    public async Task A_throwing_release_a_throwing_hook_and_a_throwing_wait_each_fault_the_task_with_the_response_disposed()
    {
        // The observer throws (the step between the hint and the release).
        var log = new List<string>();
        using var observerBody = new TrackingResponseBody(log, "observer");
        var thrownByObserver = new InvalidOperationException("observer");
        var run1 = new RetryRun(s_get, Options(), Script(_ => new Outcome.Success(TestResponses.Create(Status.ServiceUnavailable, body: observerBody))))
        {
            Observer = new RetryObserver(OnAttemptFailed: (_, _) => throw thrownByObserver),
        };
        var out1 = await Engine().RunAsync(run1, async: true, TestContext.Current.CancellationToken);
        Assert.Same(thrownByObserver, Error(out1));
        Assert.Equal(1, observerBody.DisposeCount);

        // The condition hook throws.
        using var hookBody = new TrackingResponseBody(log, "hook");
        var thrownByHook = new InvalidOperationException("hook");
        var run2 = new RetryRun(s_get, Options(), Script(_ => new Outcome.Success(TestResponses.Create(Status.ServiceUnavailable, body: hookBody))))
        {
            Condition = _ => throw thrownByHook,
        };
        var out2 = await Engine().RunAsync(run2, async: true, TestContext.Current.CancellationToken);
        Assert.Same(thrownByHook, Error(out2));
        Assert.Equal(1, hookBody.DisposeCount);

        // The wait throws: the response was already released, and the failure still surfaces.
        using var waitBody = new TrackingResponseBody(log, "wait");
        var run3 = new RetryRun(s_get, Options(baseMs: 100), Script(_ => new Outcome.Success(TestResponses.Create(Status.ServiceUnavailable, body: waitBody))));
        var out3 = await Engine(new ThrowingTimerProvider()).RunAsync(run3, async: true, TestContext.Current.CancellationToken);
        Assert.IsType<NotSupportedException>(Error(out3));
        Assert.Equal(1, waitBody.DisposeCount);
    }

    private sealed class ThrowingTimerProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            throw new NotSupportedException("no timers");
    }

    [Fact]
    public async Task Both_waits_use_TimeProviderWaits()
    {
        // Async: a 60-day delay is waited as chunks of at most 49 days.
        var clock = new RecordingFakeTimeProvider();
        var run = new RetryRun(s_get, Options(1, fixedDelay: TimeSpan.FromDays(60)), Script(n => n == 1 ? Fail() : Ok()));
        await DriveAsync(Engine(clock).RunAsync(run, async: true, TestContext.Current.CancellationToken).AsTask(), clock, TimeSpan.FromDays(1));
        Assert.Equal([TimeSpan.FromDays(49), TimeSpan.FromDays(11)], clock.Timers);

        // Sync: the wait blocks on a timer from the same provider.
        var syncClock = new RecordingFakeTimeProvider();
        var syncRun = new RetryRun(s_get, Options(1, fixedDelay: TimeSpan.FromSeconds(30)), Script(n => n == 1 ? Fail() : Ok()));
        var syncTask = Task.Run(
            () => SyncResult(Engine(syncClock).RunAsync(syncRun, async: false, TestContext.Current.CancellationToken)),
            TestContext.Current.CancellationToken);
        await DriveAsync(syncTask, syncClock, TimeSpan.FromSeconds(10));
        Assert.Equal([TimeSpan.FromSeconds(30)], syncClock.Timers);
        Assert.IsType<Outcome.Success>(await syncTask);
    }

    private static Outcome SyncResult(ValueTask<Outcome> task)
    {
        Assert.True(task.IsCompleted);
#pragma warning disable RS0030 // The sync path's task is complete by construction (a test mirror of SyncPath.GetResult).
        return task.Result;
#pragma warning restore RS0030
    }

    private sealed class DisposableClock : TimeProvider, IDisposable
    {
        public int Disposed { get; private set; }

        public void Dispose() => Disposed++;
    }

    [Fact]
    public async Task The_TimeProvider_is_never_disposed()
    {
        using var clock = new DisposableClock();
        var run = new RetryRun(s_get, Options(2), Script(_ => Fail()));

        await Engine(clock).RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Equal(0, clock.Disposed);
    }

    [Fact]
    public async Task Concurrent_calls_through_one_engine_do_not_share_state()
    {
        var engine = Engine();
        var tasks = Enumerable.Range(0, 64).Select(async i =>
        {
            var failures = i % 4;
            var sends = 0;
            var run = new RetryRun(s_get, Options(5), Script(n => n <= failures ? Fail($"c{i}-{n}") : Ok(), _ => Interlocked.Increment(ref sends)));
            var outcome = await engine.RunAsync(run, async: true, TestContext.Current.CancellationToken);
            return (failures, sends, outcome);
        }).ToList();

        foreach (var (failures, sends, outcome) in await Task.WhenAll(tasks))
        {
            Assert.Equal(failures + 1, sends);
            Assert.IsType<Outcome.Success>(outcome);
        }
    }

    [Fact]
    public async Task A_discarded_error_response_is_drained_into_an_HttpResponseException_trail_entry()
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log, "retried");
        var final = new ServiceRequestException("final");
        var run = new RetryRun(
            s_get,
            Options(1),
            Script(n => n == 1
                ? new Outcome.Success(TestResponses.Create(Status.ServiceUnavailable, body: body))
                : new Outcome.Failure(final)));

        var outcome = await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Same(final, Error(outcome));
        var entry = Assert.IsType<HttpResponseException>(Assert.Single(final.Suppressed));
        Assert.Equal(503, entry.Status.Code);
        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(["retried:open", "retried:dispose"], log);
    }

    [Fact]
    public async Task A_forced_non_error_status_leaves_no_trail_entry()
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log, "ok");
        var final = new ServiceRequestException("final");
        var run = new RetryRun(
            s_get,
            Options(1),
            Script(n => n == 1 ? new Outcome.Success(TestResponses.Create(Status.Ok, body: body)) : new Outcome.Failure(final)))
        {
            Condition = a => a.Outcome is Outcome.Success ? true : null,
        };

        await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Empty(final.Suppressed);
        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(["ok:dispose"], log);
    }

    [Fact]
    public async Task A_drain_failure_becomes_the_entry_and_the_loop_continues()
    {
        var log = new List<string>();
        var drainFailure = new System.IO.IOException("drain");
#pragma warning disable CA2000 // The body belongs to the response the engine drains and disposes.
        var body = new ThrowingOpenBody(drainFailure);
#pragma warning restore CA2000
        var final = new ServiceRequestException("final");
        var run = new RetryRun(
            s_get,
            Options(1),
            Script(n => n == 1 ? new Outcome.Success(TestResponses.Create(Status.ServiceUnavailable, body: body)) : new Outcome.Failure(final)));

        var outcome = await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Same(final, Error(outcome));
        Assert.Same(drainFailure, Assert.Single(final.Suppressed));
        Assert.Empty(log);
    }

    private sealed class ThrowingOpenBody(Exception failure) : ResponseBody
    {
        public override MediaType? ContentType => null;

        public override long ContentLength => -1;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) => Task.FromException<Stream>(failure);

        public override Stream OpenRead(CancellationToken cancellationToken = default) => throw failure;
    }

    [Fact]
    public async Task The_exhausted_callback_follows_the_final_predicate()
    {
        async Task<int> ExhaustedAsync(Request request, int retries, Func<int, Outcome> script, RetryCondition? condition = null)
        {
            var exhausted = 0;
            var run = new RetryRun(request, Options(retries), Script(script))
            {
                Condition = condition,
                Observer = new RetryObserver(OnExhausted: _ => exhausted++),
            };
            await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);
            return exhausted;
        }

        Assert.Equal(1, await ExhaustedAsync(s_get, 2, _ => Fail()));
        Assert.Equal(0, await ExhaustedAsync(s_get, 0, _ => Fail()));
        Assert.Equal(0, await ExhaustedAsync(s_get, 2, _ => new Outcome.Failure(new InvalidOperationException("x"))));
        Assert.Equal(0, await ExhaustedAsync(s_barePost, 2, _ => Fail()));
        Assert.Equal(1, await ExhaustedAsync(s_get, 1, _ => Ok(), _ => true));
        Assert.Equal(0, await ExhaustedAsync(s_get, 1, _ => Fail(), _ => false));
    }

    [Fact]
    public async Task A_stamped_attempt_header_carries_the_one_based_send_number()
    {
        var seen = new List<string?>();
        var options = new RetryOptions { MaxRetryAttempts = 2, BaseDelay = TimeSpan.Zero, Jitter = 0, AttemptHeaderName = "X-Attempt" };
        var run = new RetryRun(
            s_get.WithHeader("X-Key", "k"),
            options,
            (request, _, _, _) =>
            {
                seen.Add(request.Headers.Get("X-Attempt") + "/" + request.Headers.Get("X-Key"));
                return ValueTask.FromResult<Outcome>(Fail());
            });

        await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Equal(["1/k", "2/k", "3/k"], seen);
    }

    [Fact]
    public async Task A_disabled_attempt_header_sends_the_same_request_instance()
    {
        Request? seen = null;
        var run = new RetryRun(s_get, Options(0), (request, _, _, _) =>
        {
            seen = request;
            return ValueTask.FromResult<Outcome>(Ok());
        });

        await Engine().RunAsync(run, async: true, TestContext.Current.CancellationToken);

        Assert.Same(s_get, seen);
    }

    [Fact]
    public async Task The_budget_aborts_an_overshooting_retry_and_surfaces_the_last_failure_with_the_trail()
    {
        var clock = new FakeTimeProvider();
        var first = new ServiceRequestException("first");
        var middle = new ServiceRequestException("middle");
        var last = new ServiceRequestException("last");
        var sends = 0;
        var run = new RetryRun(s_get, Options(5, fixedDelay: TimeSpan.FromSeconds(4)), Script(n => new Outcome.Failure(n == 1 ? first : n == 2 ? middle : last), _ => sends++))
        {
            Budget = RetryBudget.For(TimeSpan.FromSeconds(10), clock),
        };

        var outcome = await DriveAsync(Engine(clock).RunAsync(run, async: true, TestContext.Current.CancellationToken).AsTask(), clock, TimeSpan.FromSeconds(1));

        // 4 s elapsed after send 1, 8 s after send 2; a third wait of 4 s would end at 12 s, past the 10 s budget.
        Assert.Equal(3, sends);
        Assert.Same(last, Error(outcome));
        Assert.Equal([first, middle], last.Suppressed);
    }
}
