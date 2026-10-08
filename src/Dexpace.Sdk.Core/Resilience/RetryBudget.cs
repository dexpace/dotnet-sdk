// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Resilience;

/// <summary>
/// The optional total-time budget of a retry sequence (RETRY-27). The stage policy always runs with
/// <see cref="Unbounded"/> (RETRY-28); only <c>RetryRecovery</c> carries a budget.
/// </summary>
/// <remarks>
/// Elapsed time is measured with <see cref="TimeProvider.GetTimestamp"/> and
/// <see cref="TimeProvider.GetElapsedTime(long)"/> (CFG-16), never with the wall clock. A zero total disables the budget.
/// </remarks>
internal readonly struct RetryBudget
{
    private readonly TimeSpan _total;
    private readonly TimeProvider? _timeProvider;

    private RetryBudget(TimeSpan total, TimeProvider timeProvider)
    {
        _total = total;
        _timeProvider = timeProvider;
    }

    /// <summary>A budget that never expires.</summary>
    internal static RetryBudget Unbounded => default;

    /// <summary>Gets a value indicating whether the budget never expires.</summary>
    internal bool IsUnbounded => _timeProvider is null;

    /// <summary>Creates a budget of <paramref name="total"/>; zero means unbounded.</summary>
    /// <param name="total">The total time allowed, measured from the sequence's first send.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <returns>The budget.</returns>
    internal static RetryBudget For(TimeSpan total, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return total <= TimeSpan.Zero ? Unbounded : new RetryBudget(total, timeProvider);
    }

    /// <summary>Whether the elapsed time since <paramref name="startTimestamp"/> has reached the budget.</summary>
    /// <param name="startTimestamp">The timestamp taken before the first send.</param>
    /// <returns><see langword="true"/> when no further retry may start.</returns>
    internal bool IsSpent(long startTimestamp) =>
        _timeProvider is not null && _timeProvider.GetElapsedTime(startTimestamp) >= _total;

    /// <summary>Whether a retry after <paramref name="delay"/> still fits: elapsed plus the delay does not exceed the budget.</summary>
    /// <param name="startTimestamp">The timestamp taken before the first send.</param>
    /// <param name="delay">The planned wait.</param>
    /// <returns><see langword="false"/> when the retry must be abandoned.</returns>
    internal bool Allows(long startTimestamp, TimeSpan delay) =>
        _timeProvider is null || delay <= _total - _timeProvider.GetElapsedTime(startTimestamp);

    /// <summary>
    /// Clamps a delay to the time that remains. It narrows the wait only across the clock read between <see cref="Allows"/>
    /// and the wait itself.
    /// </summary>
    /// <param name="startTimestamp">The timestamp taken before the first send.</param>
    /// <param name="delay">The planned wait.</param>
    /// <returns>The wait, at most the remainder and at least zero.</returns>
    internal TimeSpan Clamp(long startTimestamp, TimeSpan delay)
    {
        if (_timeProvider is null)
        {
            return delay;
        }

        var remaining = _total - _timeProvider.GetElapsedTime(startTimestamp);
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        return delay < remaining ? delay : remaining;
    }
}
