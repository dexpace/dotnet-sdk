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
/// default precedence is <c>Retry-After</c> (seconds, then an HTTP-date), <c>retry-after-ms</c>,
/// <c>x-ms-retry-after-ms</c>, <c>X-RateLimit-Reset</c> (<see cref="DefaultOrder"/>); the recovery stack always uses it,
/// and the stage stack walks the caller's ordered subset of it (<c>RetryOptions.PacingHeaders</c>), parsing each header
/// by its own name's semantics (RETRY-21). A well-formed numeral of any size saturates and is clamped to 365 days (P6a-18).
/// </remarks>
internal static class RetryPacing
{
    private const string RetryAfter = "Retry-After";
    private const string RetryAfterMs = "retry-after-ms";
    private const string MsRetryAfterMs = "x-ms-retry-after-ms";
    private const string RateLimitReset = "X-RateLimit-Reset";

    // The integer part saturates one second above the 365-day ceiling, so it clamps without ever overflowing.
    private const long SecondsSaturation = (RetryBackoff.MaxClampTicks / TimeSpan.TicksPerSecond) + 1;

    // A digits-only numeral saturates here: far above anything a 365-day clamp keeps, far below ulong overflow.
    private const ulong DigitsSaturation = 1_000_000_000_000_000UL;

    private const int FractionDigits = 7;
    private const double ResetJitter = 0.2;

    /// <summary>The four headers the parser knows, in the fixed precedence and in their canonical spelling; read-only.</summary>
    internal static IReadOnlyList<string> DefaultOrder { get; } = Array.AsReadOnly([RetryAfter, RetryAfterMs, MsRetryAfterMs, RateLimitReset]);

    /// <summary>Reads the server's pacing hint from the response headers in the fixed precedence (<see cref="DefaultOrder"/>).</summary>
    /// <param name="headers">The response headers.</param>
    /// <param name="now">The current UTC instant, for an HTTP-date or an epoch.</param>
    /// <param name="random">A source of samples in <c>[0, 1)</c>, used only by <c>X-RateLimit-Reset</c>.</param>
    /// <returns>The delay to wait (zero for an instant already past), or <see langword="null"/> for no usable hint.</returns>
    internal static TimeSpan? TryGetHint(Headers headers, DateTimeOffset now, Func<double> random) =>
        TryGetHint(headers, now, random, DefaultOrder);

    /// <summary>Reads the server's pacing hint from the first of <paramref name="order"/> that holds a parseable value (RETRY-21).</summary>
    /// <param name="headers">The response headers.</param>
    /// <param name="now">The current UTC instant, for an HTTP-date or an epoch.</param>
    /// <param name="random">A source of samples in <c>[0, 1)</c>, used only by <c>X-RateLimit-Reset</c>.</param>
    /// <param name="order">The pacing header names to try, in order, compared ignoring case; a name that is not one of the four is skipped.</param>
    /// <returns>The delay to wait (zero for an instant already past), or <see langword="null"/> for no usable hint.</returns>
    internal static TimeSpan? TryGetHint(Headers headers, DateTimeOffset now, Func<double> random, IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(order);

        foreach (var name in order)
        {
            if (TryHeader(name, headers, now, random, out var ticks))
            {
                return TimeSpan.FromTicks(RetryBackoff.ClampTicks(ticks));
            }
        }

        return null;
    }

    // Each name is parsed by its own semantics: seconds or an HTTP-date, milliseconds, or an epoch reset.
    private static bool TryHeader(string name, Headers headers, DateTimeOffset now, Func<double> random, out double ticks)
    {
        ticks = 0;
        if (string.Equals(name, RetryAfter, StringComparison.OrdinalIgnoreCase))
        {
            return TryRetryAfter(Trimmed(headers.Get(HttpHeaderName.WellKnown.RetryAfter)), now, out ticks);
        }

        if (string.Equals(name, RetryAfterMs, StringComparison.OrdinalIgnoreCase))
        {
            return TryMilliseconds(headers.Get(RetryAfterMs), out ticks);
        }

        if (string.Equals(name, MsRetryAfterMs, StringComparison.OrdinalIgnoreCase))
        {
            return TryMilliseconds(headers.Get(MsRetryAfterMs), out ticks);
        }

        return string.Equals(name, RateLimitReset, StringComparison.OrdinalIgnoreCase)
            && TryRateLimitReset(Trimmed(headers.Get(RateLimitReset)), now, random, out ticks);
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
