// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// Reads a <see cref="ValueTask"/> that the <c>async: false</c> branch of a <c>bool async</c> core already completed
/// (design §5.3, P4b-7).
/// </summary>
/// <remarks>
/// The sync entry point of a recovery type passes <c>async: false</c>, in which no step's <c>ApplyAsync</c> and no
/// transport's <c>ExecuteAsync</c> is called, so the returned task is always complete. A task that is not complete is a
/// core defect and throws <see cref="InvalidOperationException"/>. A task that faulted rethrows the original exception
/// (never an <see cref="AggregateException"/>).
/// </remarks>
internal static class SyncPath
{
    /// <summary>Returns the result of a completed task, or rethrows its original exception.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="task">The completed task.</param>
    /// <returns>The result.</returns>
    /// <exception cref="InvalidOperationException">The task is not complete: a core defect.</exception>
    internal static T GetResult<T>(ValueTask<T> task)
    {
        AssertCompleted(task.IsCompleted);

        // The one sanctioned ValueTask<T>.Result read in core: the task is complete by construction and Result rethrows
        // the original exception (design §5.3, P4b-7).
#pragma warning disable RS0030
        return task.Result;
#pragma warning restore RS0030
    }

    /// <summary>Returns when a completed task finished, or rethrows its original exception.</summary>
    /// <param name="task">The completed task.</param>
    /// <exception cref="InvalidOperationException">The task is not complete: a core defect.</exception>
    internal static void GetResult(ValueTask task)
    {
        AssertCompleted(task.IsCompleted);
        if (task.IsCompletedSuccessfully)
        {
            return;
        }

        // A completed faulted or cancelled task: GetResult rethrows the original exception, never an AggregateException
        // (design §5.3, P4b-7).
#pragma warning disable RS0030
        task.GetAwaiter().GetResult();
#pragma warning restore RS0030
    }

    private static void AssertCompleted(bool isCompleted)
    {
        if (!isCompleted)
        {
            throw new InvalidOperationException(
                "A synchronous recovery path produced a task that is not complete; this is a defect in the SDK core.");
        }
    }
}
