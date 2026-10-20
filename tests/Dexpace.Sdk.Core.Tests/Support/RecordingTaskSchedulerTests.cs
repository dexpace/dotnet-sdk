// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.TestSupport.Threading;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Support;

/// <summary>The scheduler the bridge tests rely on (plan task 3.1): its own thread per task, counted, never inlined.</summary>
[Trait("Category", "Unit")]
public sealed class RecordingTaskSchedulerTests
{
    private static Task<(int ThreadId, Thread Thread, bool IsPool)> Run(TaskScheduler scheduler) =>
        Task.Factory.StartNew(
            () => (Environment.CurrentManagedThreadId, Thread.CurrentThread, Thread.CurrentThread.IsThreadPoolThread),
            CancellationToken.None,
            TaskCreationOptions.None,
            scheduler);

    [Fact]
    public async Task It_runs_each_task_on_its_own_non_pool_thread()
    {
        using var scheduler = new RecordingTaskScheduler();

        var first = await Run(scheduler);
        var second = await Run(scheduler);

        Assert.False(first.IsPool);
        Assert.False(second.IsPool);
        // Two distinct threads, compared by identity: a managed thread id is recycled once its thread has exited, so the first task's thread (finished before
        // the second is queued) can hand its id to the second ("Expected: Not 52, Actual: 52" in 3 of 60 runs under load).
        Assert.NotSame(first.Thread, second.Thread);
        Assert.Equal([first.ThreadId, second.ThreadId], scheduler.ThreadIds);
    }

    [Fact]
    public async Task It_counts_every_QueueTask()
    {
        using var scheduler = new RecordingTaskScheduler();

        await Run(scheduler);
        await Run(scheduler);
        await Run(scheduler);

        Assert.Equal(3, scheduler.QueueCount);
    }

    [Fact]
    public async Task It_never_inlines_a_task_on_the_calling_thread()
    {
        using var scheduler = new RecordingTaskScheduler();
        var caller = Environment.CurrentManagedThreadId;

        var ran = await Run(scheduler);

        Assert.NotEqual(caller, ran.ThreadId);
    }

    [Fact]
    public async Task Dispose_is_recorded_and_not_triggered_by_running_tasks()
    {
        var scheduler = new RecordingTaskScheduler();

        await Run(scheduler);
        Assert.False(scheduler.IsDisposed);

        scheduler.Dispose();
        Assert.True(scheduler.IsDisposed);
    }
}
