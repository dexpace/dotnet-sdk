// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Threading;

/// <summary>
/// A <see cref="TaskScheduler"/> that starts every queued task on a fresh background thread (so
/// <see cref="ExecutionContext"/> flows as it does for any scheduler), counts the tasks it is given and records the
/// thread each one ran on. It never inlines. Used to prove where the <c>AsAsync</c> bridge runs a blocking call
/// (SEAM-18), that the caller's context reaches the worker (SEAM-24) and that disposing the bridge does not dispose its
/// scheduler (SEAM-25).
/// </summary>
public sealed class RecordingTaskScheduler : TaskScheduler, IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<int> _threadIds = [];
    private int _queueCount;

    /// <summary>The number of tasks queued so far.</summary>
    public int QueueCount => Volatile.Read(ref _queueCount);

    /// <summary>Whether <see cref="Dispose"/> has been called.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>The managed thread id each task ran on, in the order the tasks started running.</summary>
    public IReadOnlyList<int> ThreadIds
    {
        get
        {
            lock (_gate)
            {
                return [.. _threadIds];
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;

    /// <inheritdoc />
    protected override void QueueTask(Task task)
    {
        Interlocked.Increment(ref _queueCount);
        var thread = new Thread(() =>
        {
            lock (_gate)
            {
                _threadIds.Add(Environment.CurrentManagedThreadId);
            }

            TryExecuteTask(task);
        })
        {
            IsBackground = true,
        };
        thread.Start();
    }

    /// <inheritdoc />
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

    /// <summary>Always empty: every task is handed to its own thread at once, so none waits in a queue.</summary>
    /// <returns>An empty sequence.</returns>
    protected override IEnumerable<Task> GetScheduledTasks() => [];
}
