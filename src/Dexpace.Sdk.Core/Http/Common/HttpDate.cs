// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// Formats and parses the RFC 1123 form of an HTTP-date, <c>Sun, 06 Nov 1994 08:49:37 GMT</c> (CFG-29, CFG-30, CFG-31;
/// design §8.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not the BCL.</b> <c>DateTimeOffset.ParseExact(…, "r", …)</c> validates the weekday and rejects <c>UTC</c>,
/// <c>+0000</c> and lower case; <c>RetryConditionHeaderValue.TryParse</c> accepts RFC 850 and asctime and rejects an
/// inconsistent weekday. They are wrong in opposite directions, so this parser is hand-written over a span (design §6.1)
/// and is the one the retry policy's <c>Retry-After</c> date uses (RETRY-15).
/// </para>
/// <para>
/// <b>Grammar</b> (applied to the caller's string with no trimming or normalisation): a three-letter weekday and
/// <c>", "</c>, matched and discarded, never compared with the date (CFG-30); a one- or two-digit day (a single digit is
/// accepted because RETRY-15 requires it, P5a-11); a three-letter month, case-insensitive; a four-digit year; a
/// <c>HH:mm:ss</c> time (no leap second: <c>60</c> is rejected); and a zone of <c>GMT</c>, <c>UTC</c> (letters
/// case-insensitive), <c>+0000</c> or <c>+00:00</c>; then the end of the input. Blank input, a missing comma, RFC 850,
/// asctime and every other form fail (CFG-31; design §11 item 27): a rejected date is "no hint" to the retry policy,
/// never a wrong wait.
/// </para>
/// <para>
/// Numbers are read digit by digit, so no culture can enter. <see cref="Parse(string)"/> throws a
/// <see cref="FormatException"/> that never echoes the input (a header value can carry anything).
/// </para>
/// </remarks>
public static class HttpDate
{
    private static readonly string[] s_months =
        ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>Formats <paramref name="instant"/> as an RFC 1123 HTTP-date in UTC (CFG-29).</summary>
    /// <param name="instant">The instant; any offset is converted to the same UTC instant.</param>
    /// <returns>For example <c>Sun, 06 Nov 1994 08:49:37 GMT</c>, with a zero-padded day and the literal <c>GMT</c>.</returns>
    public static string Format(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("r", CultureInfo.InvariantCulture);

    /// <summary>Parses an RFC 1123 HTTP-date (CFG-30, CFG-31).</summary>
    /// <param name="value">The text to parse.</param>
    /// <returns>The instant, with a zero offset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="value"/> is not in the accepted form; the message does not echo it.
    /// </exception>
    public static DateTimeOffset Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TryParseCore(value, out var instant)
            ? instant
            : throw new FormatException(
                "The value is not an HTTP-date; expected the RFC 1123 form, for example 'Sun, 06 Nov 1994 08:49:37 GMT'.");
    }

    /// <summary>Parses an RFC 1123 HTTP-date without throwing (CFG-30, CFG-31).</summary>
    /// <param name="value">The text to parse; <see langword="null"/> yields <see langword="false"/>.</param>
    /// <param name="instant">The instant, with a zero offset; <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is in the accepted form.</returns>
    public static bool TryParse(string? value, out DateTimeOffset instant)
    {
        if (value is null)
        {
            instant = default;
            return false;
        }

        return TryParseCore(value, out instant);
    }

    private static bool TryParseCore(ReadOnlySpan<char> text, out DateTimeOffset instant)
    {
        instant = default;
        var cursor = new Cursor(text);
        if (!cursor.TryTakeLetters(3) || !cursor.TryTake(',') || !cursor.TryTake(' '))
        {
            return false;
        }

        if (!cursor.TryTakeDigits(1, 2, out var day) || !cursor.TryTake(' ')
            || !TryReadMonth(ref cursor, out var month) || !cursor.TryTake(' ')
            || !cursor.TryTakeDigits(4, 4, out var year) || !cursor.TryTake(' '))
        {
            return false;
        }

        if (!TryReadTime(ref cursor, out var hour, out var minute, out var second)
            || !cursor.TryTake(' ') || !TryReadZone(ref cursor) || !cursor.AtEnd)
        {
            return false;
        }

        return TryAssemble(year, month, day, hour, minute, second, out instant);
    }

    private static bool TryReadMonth(ref Cursor cursor, out int month)
    {
        month = 0;
        if (!cursor.TryPeekLetters(3, out var name))
        {
            return false;
        }

        for (var i = 0; i < s_months.Length; i++)
        {
            if (EqualsFolded(name, s_months[i]))
            {
                month = i + 1;
                cursor.Skip(3);
                return true;
            }
        }

        return false;
    }

    private static bool TryReadTime(ref Cursor cursor, out int hour, out int minute, out int second)
    {
        minute = 0;
        second = 0;
        if (!cursor.TryTakeDigits(2, 2, out hour) || hour > 23 || !cursor.TryTake(':')
            || !cursor.TryTakeDigits(2, 2, out minute) || minute > 59 || !cursor.TryTake(':')
            || !cursor.TryTakeDigits(2, 2, out second) || second > 59)
        {
            return false;
        }

        return true;
    }

    private static bool TryReadZone(ref Cursor cursor)
    {
        if (cursor.TryTakeFolded("gmt") || cursor.TryTakeFolded("utc"))
        {
            return true;
        }

        return cursor.TryTakeExact("+0000") || cursor.TryTakeExact("+00:00");
    }

    private static bool TryAssemble(int year, int month, int day, int hour, int minute, int second, out DateTimeOffset instant)
    {
        instant = default;
        if (year < 1 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        instant = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
        return true;
    }

    // ASCII-only fold: the second argument is lower case.
    private static bool EqualsFolded(ReadOnlySpan<char> text, string lower)
    {
        if (text.Length != lower.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if ((text[i] | 0x20) != lower[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private ref struct Cursor(ReadOnlySpan<char> text)
    {
        private readonly ReadOnlySpan<char> _text = text;
        private int _position;

        internal readonly bool AtEnd => _position == _text.Length;

        internal void Skip(int count) => _position += count;

        internal bool TryTake(char expected)
        {
            if (_position < _text.Length && _text[_position] == expected)
            {
                _position++;
                return true;
            }

            return false;
        }

        internal bool TryTakeLetters(int count)
        {
            if (!TryPeekLetters(count, out _))
            {
                return false;
            }

            _position += count;
            return true;
        }

        internal readonly bool TryPeekLetters(int count, out ReadOnlySpan<char> letters)
        {
            letters = default;
            if (_text.Length - _position < count)
            {
                return false;
            }

            var slice = _text.Slice(_position, count);
            foreach (var c in slice)
            {
                if (!IsAsciiLetter(c))
                {
                    return false;
                }
            }

            letters = slice;
            return true;
        }

        internal bool TryTakeDigits(int min, int max, out int value)
        {
            value = 0;
            var count = 0;
            while (count < max && _position + count < _text.Length && IsAsciiDigit(_text[_position + count]))
            {
                value = (value * 10) + (_text[_position + count] - '0');
                count++;
            }

            if (count < min)
            {
                return false;
            }

            _position += count;
            return true;
        }

        internal bool TryTakeFolded(string lower)
        {
            if (_text.Length - _position < lower.Length || !EqualsFolded(_text.Slice(_position, lower.Length), lower))
            {
                return false;
            }

            _position += lower.Length;
            return true;
        }

        internal bool TryTakeExact(string literal)
        {
            if (!_text[_position..].StartsWith(literal, StringComparison.Ordinal))
            {
                return false;
            }

            _position += literal.Length;
            return true;
        }
    }
}
