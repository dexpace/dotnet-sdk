// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Waiting for a condition another thread or task is about to make true, bounded in real time (suite-contract clause 6). The
/// kit never sleeps on the wall clock and never uses the banned delay overloads: the pause between looks is
/// <see cref="TimeProviderWaits.DelayAsync"/> over the system clock, and the bound is a cancellation source, so no assertion reads
/// a clock to decide a verdict.
/// </summary>
internal static class Poll
{
    private static readonly TimeSpan s_interval = TimeSpan.FromMilliseconds(2);

    /// <summary>Returns once <paramref name="condition"/> holds; fails naming <paramref name="what"/> if it has not within <paramref name="bound"/>.</summary>
    /// <param name="condition">The condition; cheap and side-effect free.</param>
    /// <param name="bound">The ceiling.</param>
    /// <param name="what">What was being waited for, named in the failure.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <exception cref="ConformanceException">The bound passed first.</exception>
    internal static async Task UntilAsync(Func<bool> condition, TimeSpan bound, string what, CancellationToken cancellationToken)
    {
        if (!await HoldsWithinAsync(condition, bound, cancellationToken).ConfigureAwait(false))
        {
            throw new ConformanceException(
                $"{what} did not happen within its {Bounded.Format(bound)} bound",
                $"{what} within {Bounded.Format(bound)}",
                "it had not happened");
        }
    }

    /// <summary>Whether <paramref name="condition"/> holds within <paramref name="bound"/>; returns as soon as it does.</summary>
    /// <param name="condition">The condition; cheap and side-effect free.</param>
    /// <param name="bound">The ceiling.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    internal static async Task<bool> HoldsWithinAsync(Func<bool> condition, TimeSpan bound, CancellationToken cancellationToken)
    {
        using var expiry = new CancellationTokenSource(bound);
        while (!condition())
        {
            if (expiry.IsCancellationRequested)
            {
                return false;
            }

            await TimeProvider.System.DelayAsync(s_interval, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
