// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Internal;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

[Trait("Category", "Unit")]
public sealed class BlockingWaitTests
{
    [Fact]
    public void Waits_the_requested_time_on_a_fake_clock()
    {
        var clock = new FakeTimeProvider();
        var wait = Task.Run(
            () => BlockingWait.Wait(TimeSpan.FromSeconds(5), clock, CancellationToken.None),
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
        var wait = Task.Run(() => BlockingWait.Wait(TimeSpan.FromDays(1), clock, cts.Token), TestContext.Current.CancellationToken);

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
            () => BlockingWait.Wait(TimeSpan.FromDays(365), recording, CancellationToken.None),
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

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_zero_or_negative_wait_returns_at_once(int seconds)
    {
        BlockingWait.Wait(TimeSpan.FromSeconds(seconds), new FakeTimeProvider(), CancellationToken.None);
    }

    private sealed class RecordingClock(FakeTimeProvider inner, List<TimeSpan> dueTimes) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (dueTimes)
            {
                dueTimes.Add(dueTime);
            }

            return inner.CreateTimer(callback, state, dueTime, period);
        }
    }
}
