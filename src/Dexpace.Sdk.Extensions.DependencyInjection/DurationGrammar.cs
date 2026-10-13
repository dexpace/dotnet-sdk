// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// The duration grammar of CFG-7 (P9-8, P9-29): ISO-8601 day-time durations (<c>PnDTnHnMn.nS</c>, a fraction of at most seven digits
/// on the seconds), a number with a unit (<c>250ms</c>, <c>3s</c>, <c>2m</c>, <c>1h</c>, <c>1d</c>) and whole milliseconds. Hand-written
/// scanners over ASCII, so no culture, no regular expression and no exception reaches the caller; a failure carries a rule that
/// describes the grammar and never the input.
/// </summary>
internal static class DurationGrammar
{
    internal const string Rule =
        "not a duration: use ISO-8601 such as PT5S or P1DT2H (no years, months or weeks), a number with a unit such as 250ms, 3s, 2m, 1h or 1d, "
        + "or whole milliseconds; a duration is never negative and fits in a TimeSpan";

    private const int MaxFractionDigits = 7;

    /// <summary>Parses <paramref name="text"/> (ASCII-trimmed first) as a duration.</summary>
    /// <param name="text">The configured text.</param>
    /// <param name="value">The duration when the method returns <see langword="true"/>.</param>
    /// <param name="rule">The grammar's rule text when it returns <see langword="false"/>; never the input.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a duration.</returns>
    internal static bool TryParse(string text, out TimeSpan value, out string? rule)
    {
        ArgumentNullException.ThrowIfNull(text);
        value = default;
        rule = Rule;
        var trimmed = ScalarGrammar.AsciiTrim(text);
        if (trimmed.Length == 0)
        {
            return false;
        }

        UInt128 ticks;
        var parsed = trimmed[0] is 'P' or 'p' ? TryParseIso(trimmed, out ticks) : TryParsePlain(trimmed, out ticks);
        if (!parsed || ticks > (UInt128)long.MaxValue)
        {
            return false;
        }

        value = TimeSpan.FromTicks((long)ticks);
        rule = null;
        return true;
    }

    private static bool TryParseIso(string text, out UInt128 ticks)
    {
        ticks = 0;
        var upper = ScalarGrammar.AsciiUpper(text);
        var index = 1;
        var any = false;
        if (TryReadDigits(upper, ref index, out var days, out _) && index < upper.Length && upper[index] == 'D')
        {
            ticks += (UInt128)days * (ulong)TimeSpan.TicksPerDay;
            index++;
            any = true;
        }
        else if (index != 1)
        {
            // Digits that are not followed by D: a year, month or week designator, or a stray character.
            return false;
        }

        if (index == upper.Length)
        {
            return any;
        }

        return upper[index] == 'T' && TryParseTime(upper, index + 1, ref ticks);
    }

    // The part after T: [nH][nM][n[.f]S], in that order, at least one component, nothing left over.
    private static bool TryParseTime(string text, int index, ref UInt128 ticks)
    {
        var any = false;
        var stage = 0;
        while (index < text.Length)
        {
            if (!TryReadDigits(text, ref index, out var whole, out _) || index >= text.Length)
            {
                return false;
            }

            UInt128 fractionTicks = 0;
            if (text[index] == '.')
            {
                index++;
                if (!TryReadFraction(text, ref index, out var digits, out var count) || index >= text.Length || text[index] != 'S')
                {
                    return false;
                }

                fractionTicks = (UInt128)digits * Pow10(MaxFractionDigits - count);
            }

            var designator = text[index++];
            var order = designator switch { 'H' => 1, 'M' => 2, 'S' => 3, _ => 0 };
            if (order <= stage)
            {
                return false;
            }

            stage = order;
            var unit = designator switch { 'H' => (ulong)TimeSpan.TicksPerHour, 'M' => (ulong)TimeSpan.TicksPerMinute, _ => (ulong)TimeSpan.TicksPerSecond };
            ticks += ((UInt128)whole * unit) + fractionTicks;
            any = true;
        }

        return any;
    }

    // <number>[ ]<unit>, or digits alone as milliseconds.
    private static bool TryParsePlain(string text, out UInt128 ticks)
    {
        ticks = 0;
        var index = 0;
        if (!TryReadDigits(text, ref index, out var whole, out _))
        {
            return false;
        }

        var fractionDigits = 0;
        ulong fraction = 0;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            if (!TryReadFraction(text, ref index, out fraction, out fractionDigits))
            {
                return false;
            }
        }

        if (index == text.Length)
        {
            ticks = (UInt128)whole * (ulong)TimeSpan.TicksPerMillisecond;
            return fractionDigits == 0;
        }

        if (text[index] == ' ')
        {
            index++;
        }

        var unitTicks = ScalarGrammar.AsciiLower(text[index..]) switch
        {
            "ms" => (ulong)TimeSpan.TicksPerMillisecond,
            "s" => (ulong)TimeSpan.TicksPerSecond,
            "m" => (ulong)TimeSpan.TicksPerMinute,
            "h" => (ulong)TimeSpan.TicksPerHour,
            "d" => (ulong)TimeSpan.TicksPerDay,
            _ => 0UL,
        };
        if (unitTicks == 0)
        {
            return false;
        }

        // The fraction of one unit, in ticks, must be exact: a duration finer than a tick fails rather than truncating.
        var scaled = (UInt128)fraction * unitTicks;
        var divisor = Pow10(fractionDigits);
        if (scaled % divisor != 0)
        {
            return false;
        }

        ticks = ((UInt128)whole * unitTicks) + (scaled / divisor);
        return true;
    }

    private static UInt128 Pow10(int exponent)
    {
        UInt128 result = 1;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10;
        }

        return result;
    }

    // ASCII digits only; false when there is none or the value does not fit a ulong.
    private static bool TryReadDigits(string text, ref int index, out ulong value, out int count)
    {
        value = 0;
        var start = index;
        while (index < text.Length && text[index] is >= '0' and <= '9')
        {
            var digit = (ulong)(text[index] - '0');
            if (value > (ulong.MaxValue - digit) / 10)
            {
                count = index - start;
                return false;
            }

            value = (value * 10) + digit;
            index++;
        }

        count = index - start;
        return count > 0;
    }

    // One to seven digits after the point: the tick resolution (a longer fraction is a failure, not a silent truncation).
    private static bool TryReadFraction(string text, ref int index, out ulong digits, out int count) =>
        TryReadDigits(text, ref index, out digits, out count) && count <= MaxFractionDigits;
}
