// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// Starts work that must not capture the caller's ambient state: the one sanctioned site of
/// <c>ExecutionContext.SuppressFlow</c> (design §5.4, §8.1; CTX-19).
/// </summary>
internal static class BackgroundWork
{
    /// <summary>Runs <paramref name="work"/> on the thread pool with no execution context flowed into it.</summary>
    /// <param name="work">The work. A failure faults the returned task, never the caller.</param>
    /// <returns>The task running the work; callers observe it.</returns>
    internal static Task Run(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (ExecutionContext.IsFlowSuppressed())
        {
            return Task.Run(work);
        }

        // The one sanctioned use (design §5.4): the task captures no Activity.Current, no AsyncLocal and no culture, so a
        // background refresh neither pins the triggering call's span nor reports under it (CTX-19, P6c-25).
#pragma warning disable RS0030
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(work);
        }
#pragma warning restore RS0030
    }
}
