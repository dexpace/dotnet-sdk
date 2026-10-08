// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Resilience;

/// <summary>
/// The one server pacing-hint parser both retry stacks share (RETRY-15 to RETRY-22, RECOV-22 to RECOV-25; design §6.1 F).
/// </summary>
/// <remarks>
/// Total by construction: it never throws, uses no <c>Parse</c> and no culture, and maps a malformed value to "no hint"
/// (<see langword="null"/>), never to zero. A valid past instant is a zero delay, which is not "no hint" (RETRY-17). The
/// precedence is fixed: <c>Retry-After</c> (seconds, then an HTTP-date), <c>retry-after-ms</c>,
/// <c>x-ms-retry-after-ms</c>, <c>X-RateLimit-Reset</c> (RETRY-21). A well-formed numeral of any size saturates and is
/// clamped to 365 days (P6a-18).
/// </remarks>
internal static class RetryPacing
{
    private const string RetryAfterMs = "retry-after-ms";
    private const string MsRetryAfterMs = "x-ms-retry-after-ms";
    private const string RateLimitReset = "X-RateLimit-Reset";

    // The integer part saturates one second above the 365-day ceiling, so it clamps without ever overflowing.
    private const long SecondsSaturation = (RetryBackoff.MaxClampTicks / TimeSpan.TicksPerSecond) + 1;

    // A digits-only numeral saturates here: far above anything a 365-day clamp keeps, far below ulong overflow.
    private const ulong DigitsSaturation = 1_000_000_000_000_000UL;

    private const int FractionDigits = 7;
    private const double ResetJitter = 0.2;

    /// <summary>Reads the server's pacing hint from the response headers.</summary>
    /// <param name="headers">The response headers.</param>
    /// <param name="now">The current UTC instant, for an HTTP-date or an epoch.</param>
    /// <param name="random">A source of samples in <c>[0, 1)</c>, used only by <c>X-RateLimit-Reset</c>.</param>
    /// <returns>The delay to wait (zero for an instant already past), or <see langword="null"/> for no usable hint.</returns>
    internal static TimeSpan? TryGetHint(Headers headers, DateTimeOffset now, Func<double> random)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(random);

        if (TryRetryAfter(Trimmed(headers.Get(HttpHeaderName.WellKnown.RetryAfter)), now, out var ticks)
            || TryMilliseconds(headers.Get(RetryAfterMs), out ticks)
            || TryMilliseconds(headers.Get(MsRetryAfterMs), out ticks)
            || TryRateLimitReset(Trimmed(headers.Get(RateLimitReset)), now, random, out ticks))
        {
            return TimeSpan.FromTicks(RetryBackoff.ClampTicks(ticks));
        }

        return null;
    }

    private static ReadOnlySpan<char> Trimmed(string? value) =>
        value is null ? default : value.AsSpan().Trim(" \t");

    private static bool TryRetryAfter(ReadOnlySpan<char> value, DateTimeOffset now, out double ticks)
    {
        ticks = 0;
        if (value.IsEmpty)
        {
            return false;
        }

        if (TryDeltaSeconds(value, out var deltaTicks))
        {
            ticks = deltaTicks;
            return true;
        }

        if (HttpDate.TryParse(value.ToString(), out var instant))
        {
            ticks = Math.Max(0, (instant - now).Ticks);
            return true;
        }

        return false;
    }

    private static bool TryMilliseconds(string? raw, out double ticks)
    {
        ticks = 0;
        if (!TryDigitsOnly(Trimmed(raw), out var milliseconds))
        {
            return false;
        }

        ticks = (double)milliseconds * TimeSpan.TicksPerMillisecond;
        return true;
    }

    private static bool TryRateLimitReset(ReadOnlySpan<char> value, DateTimeOffset now, Func<double> random, out double ticks)
    {
        ticks = 0;
        if (!TryDigitsOnly(value, out var epochSeconds))
        {
            return false;
        }

        var deltaSeconds = (double)epochSeconds - now.ToUnixTimeSeconds();
        if (deltaSeconds <= 0)
        {
            // A reset already past is zero, and the random source is not consumed.
            return true;
        }

        ticks = deltaSeconds * TimeSpan.TicksPerSecond * (1.0 + (ResetJitter * random()));
        return true;
    }

    // digits [ "." digits ] in ASCII, nothing else (RETRY-19). The integer part saturates; the fraction keeps seven digits
    // (one tick) and validates the rest.
    private static bool TryDeltaSeconds(ReadOnlySpan<char> value, out long ticks)
    {
        ticks = 0;
        var index = 0;
        long seconds = 0;
        while (index < value.Length && IsDigit(value[index]))
        {
            if (seconds < SecondsSaturation)
            {
                seconds = Math.Min((seconds * 10) + (value[index] - '0'), SecondsSaturation);
            }

            index++;
        }

        if (index == 0)
        {
            return false;
        }

        long fraction = 0;
        if (index < value.Length)
        {
            if (value[index] != '.')
            {
                return false;
            }

            index++;
            var start = index;
            var scale = TimeSpan.TicksPerSecond / 10;
            while (index < value.Length && IsDigit(value[index]))
            {
                if (index - start < FractionDigits)
                {
                    fraction += (value[index] - '0') * scale;
                    scale /= 10;
                }

                index++;
            }

            if (index == start || index < value.Length)
            {
                return false;
            }
        }

        ticks = (seconds * TimeSpan.TicksPerSecond) + fraction;
        return true;
    }

    private static bool TryDigitsOnly(ReadOnlySpan<char> value, out ulong number)
    {
        number = 0;
        if (value.IsEmpty)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!IsDigit(c))
            {
                return false;
            }

            if (number < DigitsSaturation)
            {
                number = Math.Min((number * 10) + (ulong)(c - '0'), DigitsSaturation);
            }
        }

        return true;
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
