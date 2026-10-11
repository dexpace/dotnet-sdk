// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>The bounded waits the fixture's tests share: conditions with a ceiling, and the one timed negative.</summary>
internal static class Waits
{
    /// <summary>The ceiling on a wait that is expected to complete.</summary>
    internal static TimeSpan Bound { get; } = TimeSpan.FromSeconds(10);

    /// <summary>The window of the one timed negative (suite-contract clause 7): long enough to be a signal, short enough to be cheap.</summary>
    internal static TimeSpan NegativeWindow { get; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Whether <paramref name="task"/> is still incomplete after <see cref="NegativeWindow"/>.</summary>
    internal static async Task<bool> StaysPendingAsync(Task task, CancellationToken cancellationToken)
    {
        var winner = await Task.WhenAny(task, TimeProvider.System.DelayAsync(NegativeWindow, cancellationToken));
        return winner != task;
    }

    /// <summary>Awaits <paramref name="task"/> under <see cref="Bound"/>; a hang is a failure, not a hung test.</summary>
    internal static Task CompletesAsync(Task task, CancellationToken cancellationToken) => task.WaitAsync(Bound, cancellationToken);
}
