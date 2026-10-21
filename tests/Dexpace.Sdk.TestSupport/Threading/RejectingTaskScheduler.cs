// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Threading;

/// <summary>
/// A <see cref="TaskScheduler"/> whose <see cref="QueueTask"/> always throws, counting the attempts: the scheduler of a host that is shutting down or
/// saturated. <c>Task.Factory.StartNew</c> surfaces that as a <see cref="TaskSchedulerException"/> thrown from the call that queued the task. Used to
/// prove that <c>AsAsync</c> delivers a failure detectable while constructing the async operation through the returned task and never throws it
/// synchronously (ASYNC-2), and that a call cancelled before the send never reaches the scheduler.
/// </summary>
public sealed class RejectingTaskScheduler : TaskScheduler
{
    private int _queueAttempts;

    /// <summary>The number of times <see cref="QueueTask"/> was called, including the calls that threw.</summary>
    public int QueueAttempts => Volatile.Read(ref _queueAttempts);

    /// <inheritdoc />
    protected override void QueueTask(Task task)
    {
        Interlocked.Increment(ref _queueAttempts);
        throw new InvalidOperationException("The scheduler rejects every task.");
    }

    /// <inheritdoc />
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

    /// <summary>Always empty: nothing is ever accepted.</summary>
    /// <returns>An empty sequence.</returns>
    protected override IEnumerable<Task> GetScheduledTasks() => [];
}
