// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Internals;

/// <summary>Suite-contract clause 6: a hang is a failure naming the bound, never a hung run.</summary>
[Trait("Category", "Unit")]
public sealed class BoundedTests
{
    private static readonly TimeSpan s_short = TimeSpan.FromMilliseconds(300);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_body_that_finishes_inside_the_bound_passes()
    {
        var ran = false;

        await Bounded.RunAsync(_ => { ran = true; return Task.CompletedTask; }, Waits.Bound, "a", Ct);

        Assert.True(ran);
    }

    [Fact]
    public async Task A_body_that_observes_its_token_is_cut_and_reported_with_the_bound()
    {
        var error = await Assert.ThrowsAsync<ConformanceException>(() =>
            Bounded.RunAsync(token => Task.Delay(Timeout.Infinite, token), s_short, "transport-3.cancel-is-terminal", Ct));

        Assert.Contains("transport-3.cancel-is-terminal", error.Message, StringComparison.Ordinal);
        Assert.Contains("300 ms", error.Message, StringComparison.Ordinal);
        Assert.Equal("still running", error.Actual);
    }

    [Fact]
    public async Task A_body_that_ignores_its_token_is_abandoned_and_its_failure_is_observed()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var error = await Assert.ThrowsAsync<ConformanceException>(() => Bounded.RunAsync(_ => release.Task, s_short, "stuck", Ct));
        release.SetException(new InvalidOperationException("late failure"));

        Assert.Contains("stuck", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_callers_own_cancellation_propagates_as_cancellation_not_as_a_failure()
    {
        using var outer = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var run = Bounded.RunAsync(token => Task.Delay(Timeout.Infinite, token), Waits.Bound, "a", outer.Token);

        await outer.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task A_body_failure_other_than_the_bound_propagates_unchanged()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded.RunAsync(_ => throw new InvalidOperationException("boom"), Waits.Bound, "a", Ct));
    }

    [Fact]
    public async Task WaitAsync_bounds_a_non_generic_task_and_names_what_was_awaited()
    {
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var error = await Assert.ThrowsAsync<ConformanceException>(() => Bounded.WaitAsync(never.Task, s_short, "the server to see the request", Ct));
        await Bounded.WaitAsync(Task.CompletedTask, s_short, "nothing", Ct);

        Assert.Contains("the server to see the request", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(300, "300 ms")]
    [InlineData(1000, "1 s")]
    [InlineData(30000, "30 s")]
    [InlineData(1500, "1.5 s")]
    public void Format_renders_whole_seconds_from_one_second_up_and_milliseconds_below(int milliseconds, string expected)
    {
        Assert.Equal(expected, Bounded.Format(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void Format_renders_microseconds_below_one_millisecond()
    {
        Assert.Equal("100 \u00b5s", Bounded.Format(TimeSpan.FromTicks(1000)));
    }
}
