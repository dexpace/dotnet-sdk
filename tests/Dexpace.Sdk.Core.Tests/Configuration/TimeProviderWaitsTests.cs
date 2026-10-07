// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-15, CFG-17, CFG-18, P5a-7: the blocking and the awaitable wait over TimeProvider, both rejecting a negative delay
// (closing the -1 ms = infinite trap), chunking past ~49.7 days, and honouring cancellation.
[Trait("Category", "Unit")]
public sealed class TimeProviderWaitsTests
{
    [Fact]
    public void Waits_the_requested_time_on_a_fake_clock()
    {
        var clock = new FakeTimeProvider();
        var wait = Task.Run(
            () => clock.Sleep(TimeSpan.FromSeconds(5), CancellationToken.None),
            TestContext.Current.CancellationToken);

        // The timer is created on the wait's thread; advance until it has fired.
        while (!wait.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            Thread.Sleep(1);
        }

        Assert.True(wait.IsCompletedSuccessfully);
    }

    [Fact]
    public void Cancels_when_the_token_is_cancelled()
    {
        var clock = new FakeTimeProvider();
        using var cts = new CancellationTokenSource();
        var wait = Task.Run(() => clock.Sleep(TimeSpan.FromDays(1), cts.Token), TestContext.Current.CancellationToken);

        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => wait.GetAwaiter().GetResult());
    }

    [Fact]
    public void Chunks_a_wait_longer_than_49_days()
    {
        var clock = new FakeTimeProvider();
        var dueTimes = new List<TimeSpan>();
        var recording = new RecordingClock(clock, dueTimes);
        var wait = Task.Run(
            () => recording.Sleep(TimeSpan.FromDays(365), CancellationToken.None),
            TestContext.Current.CancellationToken);

        while (!wait.IsCompleted)
        {
            clock.Advance(TimeSpan.FromDays(50));
            Thread.Sleep(1);
        }

        Assert.True(wait.IsCompletedSuccessfully);
        lock (dueTimes)
        {
            Assert.Equal(TimeSpan.FromDays(365), dueTimes.Aggregate(TimeSpan.Zero, (sum, due) => sum + due));
            Assert.All(dueTimes, due => Assert.True(due <= TimeSpan.FromDays(49)));
            Assert.True(dueTimes.Count >= 8);
        }
    }

    [Fact]
    public void A_zero_wait_returns_at_once_without_arming_a_timer()
    {
        var dueTimes = new List<TimeSpan>();

        new RecordingClock(new FakeTimeProvider(), dueTimes).Sleep(TimeSpan.Zero, CancellationToken.None);

        Assert.Empty(dueTimes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5000)]
    public void A_negative_delay_is_rejected(int milliseconds)
    {
        var delay = TimeSpan.FromMilliseconds(milliseconds);

        var sleep = Assert.Throws<ArgumentOutOfRangeException>(() => new FakeTimeProvider().Sleep(delay, CancellationToken.None));
        var delayAsync = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = new FakeTimeProvider().DelayAsync(delay, CancellationToken.None); });

        Assert.Equal("delay", sleep.ParamName);
        Assert.Equal("delay", delayAsync.ParamName);
    }

    [Fact]
    public void An_infinite_delay_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FakeTimeProvider().Sleep(Timeout.InfiniteTimeSpan, CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = new FakeTimeProvider().DelayAsync(Timeout.InfiniteTimeSpan, CancellationToken.None); });
    }

    [Fact]
    public void A_null_time_provider_is_rejected()
    {
        var sleep = Assert.Throws<ArgumentNullException>(() => TimeProviderWaits.Sleep(null!, TimeSpan.Zero, CancellationToken.None));
        var delay = Assert.Throws<ArgumentNullException>(() => { _ = TimeProviderWaits.DelayAsync(null!, TimeSpan.Zero, CancellationToken.None); });

        Assert.Equal("timeProvider", sleep.ParamName);
        Assert.Equal("timeProvider", delay.ParamName);
    }

    [Fact]
    public void An_already_cancelled_token_throws_before_any_timer_even_for_a_zero_delay()
    {
        var dueTimes = new List<TimeSpan>();
        var clock = new RecordingClock(new FakeTimeProvider(), dueTimes);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var error = Assert.ThrowsAny<OperationCanceledException>(() => clock.Sleep(TimeSpan.Zero, cts.Token));
        var error2 = Assert.ThrowsAny<OperationCanceledException>(() => clock.Sleep(TimeSpan.FromSeconds(1), cts.Token));

        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.Equal(cts.Token, error2.CancellationToken);
        Assert.Empty(dueTimes);
    }

    [Fact]
    public void Cancellation_surfaces_with_the_callers_token_and_leaves_it_signalled()
    {
        var clock = new FakeTimeProvider();
        using var cts = new CancellationTokenSource();
        var wait = Task.Run(() => clock.Sleep(TimeSpan.FromDays(1), cts.Token), TestContext.Current.CancellationToken);

        cts.Cancel();
        var error = Assert.ThrowsAny<OperationCanceledException>(() => wait.GetAwaiter().GetResult());

        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.True(cts.Token.IsCancellationRequested);
    }

    [Fact]
    public void A_sub_millisecond_delay_is_passed_to_the_timer_unrounded()
    {
        var clock = new FakeTimeProvider();
        var dueTimes = new List<TimeSpan>();
        var recording = new RecordingClock(clock, dueTimes);
        var wait = Task.Run(
            () => recording.Sleep(TimeSpan.FromTicks(5), CancellationToken.None),
            TestContext.Current.CancellationToken);

        while (!wait.IsCompleted)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            Thread.Sleep(1);
        }

        lock (dueTimes)
        {
            Assert.Equal([TimeSpan.FromTicks(5)], dueTimes);
        }
    }

    [Fact]
    public async Task DelayAsync_completes_when_the_fake_clock_advances()
    {
        var clock = new FakeTimeProvider();

        var delay = clock.DelayAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.False(delay.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(5));
        await delay;

        Assert.True(delay.IsCompletedSuccessfully);
    }

    [Fact]
    public void DelayAsync_of_zero_completes_synchronously_without_a_timer()
    {
        var dueTimes = new List<TimeSpan>();

        var delay = new RecordingClock(new FakeTimeProvider(), dueTimes).DelayAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.True(delay.IsCompletedSuccessfully);
        Assert.Empty(dueTimes);
    }

    [Fact]
    public void DelayAsync_with_an_already_cancelled_token_returns_a_cancelled_task_without_a_timer()
    {
        var dueTimes = new List<TimeSpan>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var delay = new RecordingClock(new FakeTimeProvider(), dueTimes).DelayAsync(TimeSpan.FromSeconds(1), cts.Token);

        Assert.True(delay.IsCanceled);
        Assert.Empty(dueTimes);
    }

    [Fact]
    public async Task DelayAsync_cancelled_mid_wait_disposes_the_timer()
    {
        var dueTimes = new List<TimeSpan>();
        var clock = new RecordingClock(new FakeTimeProvider(), dueTimes);
        using var cts = new CancellationTokenSource();

        var delay = clock.DelayAsync(TimeSpan.FromDays(1), cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delay);

        Assert.Equal(1, clock.TimersCreated);
        Assert.Equal(1, clock.TimersDisposed);
    }

    [Fact]
    public async Task DelayAsync_chunks_a_delay_above_49_days()
    {
        var clock = new FakeTimeProvider();
        var dueTimes = new List<TimeSpan>();
        var recording = new RecordingClock(clock, dueTimes);

        var delay = recording.DelayAsync(TimeSpan.FromDays(365), CancellationToken.None);
        while (!delay.IsCompleted)
        {
            clock.Advance(TimeSpan.FromDays(50));
            await Task.Yield();
        }

        await delay;
        lock (dueTimes)
        {
            Assert.Equal(TimeSpan.FromDays(365), dueTimes.Aggregate(TimeSpan.Zero, (sum, due) => sum + due));
            Assert.All(dueTimes, due => Assert.True(due <= TimeSpan.FromDays(49)));
            Assert.True(dueTimes.Count >= 8);
        }
    }

    private sealed class RecordingClock(FakeTimeProvider inner, List<TimeSpan> dueTimes) : TimeProvider
    {
        private int _created;
        private int _disposed;

        public int TimersCreated => Volatile.Read(ref _created);

        public int TimersDisposed => Volatile.Read(ref _disposed);

        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (dueTimes)
            {
                dueTimes.Add(dueTime);
            }

            Interlocked.Increment(ref _created);
            return new CountingTimer(inner.CreateTimer(callback, state, dueTime, period), () => Interlocked.Increment(ref _disposed));
        }

        private sealed class CountingTimer(ITimer timer, Action onDispose) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => timer.Change(dueTime, period);

            public void Dispose()
            {
                onDispose();
                timer.Dispose();
            }

            public ValueTask DisposeAsync()
            {
                onDispose();
                return timer.DisposeAsync();
            }
        }
    }
}
