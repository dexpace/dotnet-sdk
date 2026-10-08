// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Core.Resilience;

/// <summary>
/// The one backoff calculator both retry stacks share (RETRY-9 to RETRY-11, RETRY-13, RECOV-21, RECOV-26; design §6.1 E).
/// </summary>
/// <remarks>
/// It computes in <see cref="double"/> over ticks and compares against the cap before any cast to <see cref="long"/>, so
/// no configuration overflows and nothing throws except a programmer error (<c>attempt &lt; 1</c>). Every result is
/// clamped to 365 days.
/// </remarks>
internal static class RetryBackoff
{
    /// <summary>The ceiling every pacing delta is clamped to: 365 days in ticks (RETRY-18, RECOV-26).</summary>
    internal const long MaxClampTicks = TimeSpan.TicksPerDay * 365;

    /// <summary>
    /// Computes the delay before retry number <paramref name="attempt"/>.
    /// </summary>
    /// <param name="attempt">The 1-based retry ordinal.</param>
    /// <param name="options">The schedule.</param>
    /// <param name="random">A source of samples in <c>[0, 1)</c>, called at most once.</param>
    /// <returns>The delay, between zero and 365 days.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempt"/> is below one.</exception>
    internal static TimeSpan Compute(int attempt, RetryOptions options, Func<double> random)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);

        if (options.FixedDelay is { } fixedDelay)
        {
            return ClampToCeiling(fixedDelay);
        }

        var baseTicks = options.BaseDelay.Ticks;
        if (baseTicks == 0)
        {
            return TimeSpan.Zero;
        }

        var capTicks = options.MaxDelay.Ticks;
        var growth = baseTicks * Math.Pow(options.Multiplier, attempt - 1);
        var delay = growth >= capTicks || double.IsInfinity(growth) ? (double)capTicks : Math.Floor(growth);

        var jitter = options.Jitter;
        if (jitter > 0)
        {
            var width = delay * jitter;
            if (width >= 1)
            {
                // A sub-tick width cannot be represented (P6a-14); otherwise the sample is symmetric around the delay.
                delay = delay - (width / 2) + (random() * width);
            }
        }

        return TimeSpan.FromTicks(ClampTicks(delay));
    }

    /// <summary>Clamps a duration to the 365-day ceiling.</summary>
    /// <param name="delay">The duration.</param>
    /// <returns>The duration, at most 365 days and at least zero.</returns>
    internal static TimeSpan ClampToCeiling(TimeSpan delay) =>
        delay.Ticks < 0 ? TimeSpan.Zero : delay.Ticks > MaxClampTicks ? TimeSpan.FromTicks(MaxClampTicks) : delay;

    /// <summary>Clamps a tick count computed in <see cref="double"/> to <c>[0, 365 days]</c>, mapping <c>NaN</c> to zero.</summary>
    /// <param name="ticks">The tick count.</param>
    /// <returns>The clamped tick count.</returns>
    internal static long ClampTicks(double ticks)
    {
        if (double.IsNaN(ticks) || ticks <= 0)
        {
            return 0;
        }

        return ticks >= MaxClampTicks ? MaxClampTicks : (long)ticks;
    }
}
