// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-5 and TRANSPORT-6 (phase 8b, P8b-6; R7): the per-call deadline, armed on the options' <see cref="TimeProvider"/>
/// and linked with the caller's token without ever touching it. Every row runs on a <see cref="FakeTimeProvider"/>, so none
/// waits on a wall clock. The end-to-end rows through the transport are in the same class's second half.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeadlineTests
{
    private sealed class CountingTimeProvider : FakeTimeProvider
    {
        public int TimersCreated { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            TimersCreated++;
            return base.CreateTimer(callback, state, dueTime, period);
        }
    }

    [Fact]
    public void Clamp_raises_a_sub_resolution_deadline_to_one_millisecond_and_lowers_an_enormous_one_to_49_days()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(1), CallDeadline.Clamp(TimeSpan.FromTicks(1000)));
        Assert.Equal(TimeSpan.FromMilliseconds(1), CallDeadline.Clamp(TimeSpan.FromTicks(1)));
        Assert.Equal(TimeSpan.FromMilliseconds(1), CallDeadline.Clamp(TimeSpan.FromMilliseconds(1)));
        Assert.Equal(TimeSpan.FromMilliseconds(250), CallDeadline.Clamp(TimeSpan.FromMilliseconds(250)));
        Assert.Equal(TimeSpan.FromDays(49), CallDeadline.Clamp(TimeSpan.FromDays(49)));
        Assert.Equal(TimeSpan.FromDays(49), CallDeadline.Clamp(TimeSpan.FromDays(50)));
        Assert.Equal(TimeSpan.FromDays(49), CallDeadline.Clamp(TimeSpan.MaxValue));
    }

    [Fact]
    public void No_deadline_allocates_no_timer_and_passes_the_callers_token_straight_through()
    {
        var clock = new CountingTimeProvider();
        using var source = new CancellationTokenSource();

        using var deadline = CallDeadline.Start(timeout: null, clock, source.Token);

        Assert.Equal(source.Token, deadline.Token);
        Assert.Equal(0, clock.TimersCreated);
        Assert.False(deadline.Fired);
    }

    [Fact]
    public void Dispose_twice_is_safe()
    {
        var deadline = CallDeadline.Start(TimeSpan.FromMilliseconds(100), new FakeTimeProvider(), CancellationToken.None);

        deadline.Dispose();

        Assert.Null(Record.Exception(deadline.Dispose));
        Assert.Null(Record.Exception(CallDeadline.Start(timeout: null, new FakeTimeProvider(), CancellationToken.None).Dispose));
    }

    [Fact]
    public void The_deadline_fires_at_its_time_and_never_touches_the_callers_token()
    {
        var clock = new FakeTimeProvider();
        using var caller = new CancellationTokenSource();
        using var deadline = CallDeadline.Start(TimeSpan.FromMilliseconds(100), clock, caller.Token);

        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.False(deadline.Token.IsCancellationRequested);
        Assert.False(deadline.Fired);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(deadline.Token.IsCancellationRequested);
        Assert.True(deadline.Fired);
        Assert.False(caller.IsCancellationRequested);
    }

    [Fact]
    public void A_caller_cancellation_signals_the_token_without_the_deadline_having_fired()
    {
        var clock = new FakeTimeProvider();
        using var caller = new CancellationTokenSource();
        using var deadline = CallDeadline.Start(TimeSpan.FromSeconds(1), clock, caller.Token);

        caller.Cancel();

        Assert.True(deadline.Token.IsCancellationRequested);
        Assert.False(deadline.Fired);
    }

    [Fact]
    public void A_token_that_cannot_be_cancelled_still_gets_a_working_deadline()
    {
        var clock = new FakeTimeProvider();
        using var deadline = CallDeadline.Start(TimeSpan.FromMilliseconds(10), clock, CancellationToken.None);

        clock.Advance(TimeSpan.FromMilliseconds(10));

        Assert.True(deadline.Token.IsCancellationRequested);
        Assert.True(deadline.Fired);
    }

    [Fact]
    public void Concurrent_deadlines_advance_independently_on_one_clock()
    {
        var clock = new FakeTimeProvider();
        using var shortOne = CallDeadline.Start(TimeSpan.FromMilliseconds(50), clock, CancellationToken.None);
        using var longOne = CallDeadline.Start(TimeSpan.FromMilliseconds(500), clock, CancellationToken.None);

        clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.True(shortOne.Fired);
        Assert.False(longOne.Fired);

        clock.Advance(TimeSpan.FromMilliseconds(450));
        Assert.True(longOne.Fired);
    }

    [Fact]
    public void After_dispose_the_clock_passing_the_deadline_signals_nothing_and_throws_nothing()
    {
        var clock = new FakeTimeProvider();
        var deadline = CallDeadline.Start(TimeSpan.FromMilliseconds(100), clock, CancellationToken.None);
        deadline.Dispose();

        var thrown = Record.Exception(() => clock.Advance(TimeSpan.FromSeconds(10)));

        Assert.Null(thrown);
        Assert.False(deadline.Fired);
    }

    [Fact]
    public void A_sub_resolution_deadline_is_armed_for_one_millisecond_not_for_never()
    {
        var clock = new FakeTimeProvider();
        using var deadline = CallDeadline.Start(TimeSpan.FromTicks(1000), clock, CancellationToken.None);

        Assert.False(deadline.Fired);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(deadline.Fired);
    }

    [Fact]
    public void An_enormous_deadline_is_armed_for_49_days_and_does_not_throw()
    {
        var clock = new FakeTimeProvider();
        using var deadline = CallDeadline.Start(TimeSpan.MaxValue, clock, CancellationToken.None);

        clock.Advance(TimeSpan.FromDays(49) - TimeSpan.FromMilliseconds(1));
        Assert.False(deadline.Fired);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(deadline.Fired);
    }

    [Fact]
    public void The_per_call_value_wins_and_a_missing_one_keeps_the_transport_default()
    {
        var transportDefault = TimeSpan.FromSeconds(100);

        Assert.Equal(TimeSpan.FromSeconds(2), CallDeadline.Effective(new RequestOptions { Timeout = TimeSpan.FromSeconds(2) }, transportDefault));
        Assert.Equal(transportDefault, CallDeadline.Effective(RequestOptions.Empty, transportDefault));
        Assert.Equal(TimeSpan.FromSeconds(2), CallDeadline.Effective(new RequestOptions { Timeout = TimeSpan.FromSeconds(2) }, transportDefault: null));
        Assert.Null(CallDeadline.Effective(RequestOptions.Empty, transportDefault: null));
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // End to end through the transport, over a stub handler that waits on the token the transport gives it. The clock is fake, so
    // "100 seconds" is one Advance and no row waits on a wall clock.

    private static readonly Request s_request = Request.Get("https://example.test/ping");

    // How long a row that expects the fake clock to have cut a call waits before it counts as not cut; a pass never waits on it.
    private static readonly TimeSpan s_regressionBound = TimeSpan.FromSeconds(10);

    // Waits until its gate opens or the token it was given is signalled, and reports when it started.
    private sealed class WaitingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls => _calls;

        private int _calls;

        public Task Started => _started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            _started.TrySetResult();
            await Gate.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static SystemNetHttpClient Over(HttpMessageHandler handler, FakeTimeProvider clock, TimeSpan? timeout = null, bool noDefault = false) =>
        new(handler, noDefault
            ? new SystemNetHttpClientOptions { TimeProvider = clock, Timeout = null }
            : timeout is { } t ? new SystemNetHttpClientOptions { TimeProvider = clock, Timeout = t } : new SystemNetHttpClientOptions { TimeProvider = clock });

    [Fact]
    public async Task A_per_call_timeout_ends_the_call_as_a_retryable_timeout_and_leaves_the_callers_token_alone()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        await using var transport = Over(handler, clock);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var call = transport.ExecuteAsync(s_request, new RequestOptions { Timeout = TimeSpan.FromMilliseconds(100) }, caller.Token);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(100));

        var failure = await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call);
        Assert.True(failure.IsRetryable);
        Assert.False(caller.IsCancellationRequested);
    }

    [Fact]
    public async Task A_call_without_a_timeout_uses_the_transports_hundred_second_default()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        await using var transport = Over(handler, clock);

        var call = transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(99));
        Assert.False(call.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call);
    }

    [Fact]
    public async Task A_configured_default_replaces_the_hundred_seconds()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        await using var transport = Over(handler, clock, timeout: TimeSpan.FromSeconds(2));

        var call = transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call);
    }

    [Fact]
    public async Task No_default_and_no_per_call_timeout_means_the_call_is_never_cut()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        await using var transport = Over(handler, clock, noDefault: true);

        var call = transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromDays(30));
        Assert.False(call.IsCompleted);
        handler.Gate.SetResult();

        await using var response = await call;
        Assert.Equal(200, response.Status.Code);
    }

    [Fact]
    public async Task One_calls_timeout_does_not_leak_into_another_call_on_the_same_transport()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        await using var transport = Over(handler, clock);

        var shortCall = transport.ExecuteAsync(s_request, new RequestOptions { Timeout = TimeSpan.FromMilliseconds(100) }, TestContext.Current.CancellationToken);
        var defaultCall = transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken);
        while (handler.Calls < 2)
        {
            await Task.Yield();
        }

        clock.Advance(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => shortCall);
        Assert.False(defaultCall.IsCompleted);
        handler.Gate.SetResult();
        await using var response = await defaultCall;
        Assert.Equal(200, response.Status.Code);
    }

    [Fact]
    public async Task The_callers_own_cancellation_is_a_cancellation_and_never_a_timeout()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        await using var transport = Over(handler, clock);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var call = transport.ExecuteAsync(s_request, new RequestOptions { Timeout = TimeSpan.FromSeconds(5) }, caller.Token);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);
        await caller.CancelAsync();

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(caller.Token, failure.CancellationToken);
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // A borrowed client (review finding, phase 8b): the transport's default deadline applies to every construction, so it also caps
    // a borrowed HttpClient whose own Timeout is longer. That is a documented breaking change for the borrowed construction (before
    // 8b only the client's own Timeout bounded the call), not a silent one, and the migration is a null transport timeout.

    [Fact]
    public async Task A_borrowed_client_with_an_infinite_own_timeout_is_still_cut_at_the_transports_hundred_second_default()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        using var borrowed = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        await using var transport = new SystemNetHttpClient(borrowed, new SystemNetHttpClientOptions { TimeProvider = clock });

        var call = transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(99));
        Assert.False(call.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        // Bounded, so a transport that stops cutting the call fails this row instead of hanging the run.
        var failure = await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call.WaitAsync(s_regressionBound, TestContext.Current.CancellationToken));
        Assert.True(failure.IsRetryable);
        Assert.Equal(Timeout.InfiniteTimeSpan, borrowed.Timeout);
    }

    [Fact]
    public async Task A_null_transport_timeout_leaves_a_borrowed_clients_own_timeout_the_only_bound()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        using var borrowed = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        await using var transport = new SystemNetHttpClient(borrowed, new SystemNetHttpClientOptions { TimeProvider = clock, Timeout = null });

        var call = transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromDays(30));
        Assert.False(call.IsCompleted);
        handler.Gate.SetResult();

        await using var response = await call;
        Assert.Equal(200, response.Status.Code);
    }

    [Fact]
    public async Task A_per_call_timeout_lifts_the_default_over_a_borrowed_client_whose_own_timeout_allows_it()
    {
        var clock = new FakeTimeProvider();
        using var handler = new WaitingHandler();
        using var borrowed = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        await using var transport = new SystemNetHttpClient(borrowed, new SystemNetHttpClientOptions { TimeProvider = clock });

        var call = transport.ExecuteAsync(s_request, new RequestOptions { Timeout = TimeSpan.FromMinutes(5) }, TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(299));
        Assert.False(call.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call.WaitAsync(s_regressionBound, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_borrowed_constructors_without_options_carry_the_hundred_second_default_like_every_other_construction()
    {
        using var borrowed = new HttpClient(new WaitingHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        await using var bare = new SystemNetHttpClient(borrowed);
        await using var withLogger = new SystemNetHttpClient(borrowed, NullLogger.Instance);
        await using var withOptions = new SystemNetHttpClient(borrowed, new SystemNetHttpClientOptions());
        await using var withHandler = new SystemNetHttpClient(new WaitingHandler());
        await using var owned = new SystemNetHttpClient();

        Assert.All(
            new[] { bare, withLogger, withOptions, withHandler, owned },
            transport => Assert.Equal(TimeSpan.FromSeconds(100), transport.Settings.Timeout));
    }

    [Fact]
    public async Task The_owned_clients_own_timeout_is_infinite_and_a_borrowed_clients_is_untouched()
    {
        await using var owned = new SystemNetHttpClient();
        await using var viaHandler = new SystemNetHttpClient(new WaitingHandler());
        using var borrowed = new HttpClient(new WaitingHandler()) { Timeout = TimeSpan.FromSeconds(7) };
        await using var transport = new SystemNetHttpClient(borrowed);

        Assert.Equal(Timeout.InfiniteTimeSpan, owned.NativeClientTimeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, viaHandler.NativeClientTimeout);
        Assert.Equal(TimeSpan.FromSeconds(7), borrowed.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(7), transport.NativeClientTimeout);
    }
}
