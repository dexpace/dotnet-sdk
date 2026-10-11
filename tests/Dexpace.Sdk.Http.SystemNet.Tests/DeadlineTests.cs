// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
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
}
