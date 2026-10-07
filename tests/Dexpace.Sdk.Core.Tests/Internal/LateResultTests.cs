// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

// CFG-21 (cooperative form, design §10 entry 8), ASYNC-5, TRANSPORT-9: a result produced after the caller stopped
// awaiting is disposed exactly once and quietly.
[Trait("Category", "Unit")]
public sealed class LateResultTests
{
    private static readonly TimeSpan s_patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_result_delivered_after_cancellation_is_disposed_exactly_once()
    {
        var source = new TaskCompletionSource<TrackingDisposable>();
        using var cts = new CancellationTokenSource();
        var wait = LateResult.WaitOrDisposeAsync(source.Task, cts.Token);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        var late = new TrackingDisposable();
        source.SetResult(late);
        await late.Disposed.Task.WaitAsync(s_patience, TestContext.Current.CancellationToken);
        await Task.Yield();

        Assert.Equal(1, late.DisposeCount);
    }

    [Fact]
    public async Task A_result_delivered_before_cancellation_is_returned_and_never_disposed()
    {
        var early = new TrackingDisposable();
        using var cts = new CancellationTokenSource();

        var result = await LateResult.WaitOrDisposeAsync(Task.FromResult(early), cts.Token);
        await cts.CancelAsync();
        await Task.Yield();

        Assert.Same(early, result);
        Assert.Equal(0, early.DisposeCount);
    }

    [Fact]
    public async Task A_late_result_whose_dispose_throws_is_swallowed()
    {
        var source = new TaskCompletionSource<TrackingDisposable>();
        using var cts = new CancellationTokenSource();
        var wait = LateResult.WaitOrDisposeAsync(source.Task, cts.Token);

        await cts.CancelAsync();
        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        var late = new TrackingDisposable(throwOnDispose: true);
        source.SetResult(late);
        await late.Disposed.Task.WaitAsync(s_patience, TestContext.Current.CancellationToken);

        Assert.Equal(cts.Token, thrown.CancellationToken);
        Assert.Equal(1, late.DisposeCount);
    }

    [Fact]
    public async Task A_faulted_late_task_is_observed_and_raises_no_UnobservedTaskException()
    {
        var unobserved = 0;
        void Handler(object? sender, UnobservedTaskExceptionEventArgs e) => Interlocked.Increment(ref unobserved);
        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            var source = new TaskCompletionSource<TrackingDisposable>();
            using var cts = new CancellationTokenSource();
            var wait = LateResult.WaitOrDisposeAsync(source.Task, cts.Token);

            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
            source.SetException(new InvalidOperationException("late failure"));
            source = null!;
            wait = null!;

            for (var i = 0; i < 2; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Yield();
            }

            Assert.Equal(0, Volatile.Read(ref unobserved));
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Handler;
        }
    }

    [Fact]
    public async Task A_cancelled_late_task_is_ignored()
    {
        var source = new TaskCompletionSource<TrackingDisposable>();
        using var cts = new CancellationTokenSource();
        var wait = LateResult.WaitOrDisposeAsync(source.Task, cts.Token);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        source.TrySetCanceled(TestContext.Current.CancellationToken);
        await Task.Yield();

        Assert.True(source.Task.IsCanceled);
    }

    [Fact]
    public async Task DisposeWhenCompleted_disposes_a_late_result_without_waiting()
    {
        var source = new TaskCompletionSource<TrackingDisposable>();
        var pending = new TrackingDisposable();
        LateResult.DisposeWhenCompleted(source.Task);

        source.SetResult(pending);
        await pending.Disposed.Task.WaitAsync(s_patience, TestContext.Current.CancellationToken);

        var completed = new TrackingDisposable();
        LateResult.DisposeWhenCompleted(Task.FromResult(completed));
        await completed.Disposed.Task.WaitAsync(s_patience, TestContext.Current.CancellationToken);

        Assert.Equal(1, pending.DisposeCount);
        Assert.Equal(1, completed.DisposeCount);
    }

    [Fact]
    public async Task Cancellation_before_the_call_throws_and_still_disposes_a_later_result()
    {
        var source = new TaskCompletionSource<TrackingDisposable>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var wait = LateResult.WaitOrDisposeAsync(source.Task, cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        var late = new TrackingDisposable();
        source.SetResult(late);
        await late.Disposed.Task.WaitAsync(s_patience, TestContext.Current.CancellationToken);

        Assert.Equal(1, late.DisposeCount);
    }

    [Fact]
    public async Task A_result_completed_between_the_token_firing_and_the_catch_is_still_disposed()
    {
        // The F1 race made deterministic: callbacks run last-registered first, so this one (registered before the wait)
        // runs after the wait has been cancelled and completes the source while the cancellation is in flight.
        var source = new TaskCompletionSource<TrackingDisposable>();
        using var late = new TrackingDisposable();
        using var cts = new CancellationTokenSource();
        using var registration = cts.Token.Register(() => source.SetResult(late));
        var wait = LateResult.WaitOrDisposeAsync(source.Task, cts.Token);

        await cts.CancelAsync();
        var outcome = await Record.ExceptionAsync(() => wait);
        if (outcome is null)
        {
            // The result won the race and was delivered to the caller, who owns it now.
            Assert.Equal(0, late.DisposeCount);
            return;
        }

        Assert.IsAssignableFrom<OperationCanceledException>(outcome);
        await late.Disposed.Task.WaitAsync(s_patience, TestContext.Current.CancellationToken);
        Assert.Equal(1, late.DisposeCount);
    }

    [Fact]
    public async Task A_late_null_result_is_ignored_without_throwing()
    {
        var source = new TaskCompletionSource<TrackingDisposable>();
        using var cts = new CancellationTokenSource();
        var wait = LateResult.WaitOrDisposeAsync(source.Task, cts.Token);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        source.SetResult(null!);
        await Task.Yield();

        Assert.True(source.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task A_task_that_ends_cancelled_with_the_token_not_signalled_surfaces_unchanged()
    {
        var source = new TaskCompletionSource<TrackingDisposable>();
        var wait = LateResult.WaitOrDisposeAsync(source.Task, TestContext.Current.CancellationToken);

        source.SetCanceled(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    private sealed class TrackingDisposable(bool throwOnDispose = false) : IDisposable
    {
        private int _count;

        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCount => Volatile.Read(ref _count);

        public void Dispose()
        {
            Interlocked.Increment(ref _count);
            Disposed.TrySetResult();
            if (throwOnDispose)
            {
                throw new InvalidOperationException("dispose failed");
            }
        }
    }
}
