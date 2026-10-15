// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Internals;

[Trait("Category", "Unit")]
public sealed class RecordingTimeProviderTests
{
    [Fact]
    public void A_timer_is_live_counted_and_disposal_is_counted_once()
    {
        var time = new RecordingTimeProvider();

        var timer = time.CreateTimer(static _ => { }, null, TimeSpan.FromHours(1), Timeout.InfiniteTimeSpan);

        Assert.Equal(1, time.TimersCreated);
        Assert.Equal(1, time.TimersLive);
        timer.Dispose();
        timer.Dispose();
        Assert.Equal(1, time.TimersDisposed);
        Assert.Equal(0, time.TimersLive);
    }

    [Fact]
    public async Task A_timer_fires_on_real_time_and_an_async_disposal_is_counted()
    {
        var time = new RecordingTimeProvider();
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var timer = time.CreateTimer(static state => ((TaskCompletionSource)state!).TrySetResult(), fired, TimeSpan.FromMilliseconds(1), Timeout.InfiniteTimeSpan);
        await fired.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await timer.DisposeAsync();

        Assert.Equal(1, time.TimersDisposed);
    }

    [Fact]
    public void Time_delegates_to_the_system_provider_unless_advanced()
    {
        var time = new RecordingTimeProvider();
        var before = time.GetUtcNow();

        time.Advance(TimeSpan.FromHours(2));
        var after = time.GetUtcNow();

        Assert.True(after - before >= TimeSpan.FromHours(2));
        Assert.True(after - before < TimeSpan.FromHours(2) + TimeSpan.FromMinutes(5));
        Assert.Equal(TimeProvider.System.TimestampFrequency, time.TimestampFrequency);
        Assert.True(time.GetTimestamp() >= TimeProvider.System.GetTimestamp());
        Assert.Throws<ArgumentOutOfRangeException>(() => time.Advance(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task A_cancelled_delay_over_the_provider_disposes_the_timer_it_armed()
    {
        var time = new RecordingTimeProvider();
        using var cts = new CancellationTokenSource();

        var wait = time.DelayAsync(TimeSpan.FromHours(1), cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);

        Assert.Equal(time.TimersCreated, time.TimersDisposed);
    }
}
