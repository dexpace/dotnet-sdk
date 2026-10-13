// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>A parser from configured text to a value: <see langword="true"/> with the value, or <see langword="false"/> with a rule.</summary>
/// <typeparam name="T">The parsed type.</typeparam>
/// <param name="text">The ASCII-trimmed, non-empty text.</param>
/// <param name="value">The value on success.</param>
/// <param name="rule">The rule text on failure; never the input.</param>
/// <returns>Whether the text parsed.</returns>
internal delegate bool Parser<T>(string text, out T value, out string? rule);

/// <summary>
/// The scalar grammars of the configuration reader (CFG-5, CFG-6, OBS-35; P9-8, P9-27). Every parser returns a rule on failure and
/// never throws; no rule quotes the input, because a configured value can be a secret (XCUT-19). Nothing reads the culture.
/// </summary>
internal static class ScalarGrammar
{
    /// <summary>ASCII whitespace: space, tab, line feed, vertical tab, form feed and carriage return (P9-27).</summary>
    private static readonly char[] s_whitespace = [' ', '\t', '\n', '\u000B', '\f', '\r'];

    /// <summary>Trims ASCII whitespace from both ends.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    internal static string AsciiTrim(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Trim(s_whitespace);
    }

    /// <summary>Upper-cases ASCII letters only, so no culture or special casing rule can turn a non-ASCII letter into an ASCII one.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with <c>a</c> to <c>z</c> upper-cased.</returns>
    internal static string AsciiUpper(string text) => string.Create(text.Length, text, static (span, source) =>
    {
        for (var i = 0; i < span.Length; i++)
        {
            var c = source[i];
            span[i] = c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
        }
    });

    /// <summary>Lower-cases ASCII letters only.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with <c>A</c> to <c>Z</c> lower-cased.</returns>
    internal static string AsciiLower(string text) => string.Create(text.Length, text, static (span, source) =>
    {
        for (var i = 0; i < span.Length; i++)
        {
            var c = source[i];
            span[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
        }
    });

    /// <summary>CFG-5: base-10, <c>[+-]?[0-9]+</c> in ASCII, in the range of <see cref="int"/>.</summary>
    internal static bool TryParseInt32(string text, out int value, out string? rule)
    {
        value = 0;
        rule = "not a base-10 integer that fits a 32-bit integer (digits only, an optional sign, no separators, exponent or fraction)";
        if (!IsSignedDigits(text) || !int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        rule = null;
        return true;
    }

    /// <summary>CFG-5: base-10, <c>[+-]?[0-9]+</c> in ASCII, in the range of <see cref="long"/>.</summary>
    internal static bool TryParseInt64(string text, out long value, out string? rule)
    {
        value = 0;
        rule = "not a base-10 integer that fits a 64-bit integer (digits only, an optional sign, no separators, exponent or fraction)";
        if (!IsSignedDigits(text) || !long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        rule = null;
        return true;
    }

    /// <summary>CFG-6: only <c>true</c> or <c>false</c>, case-insensitive (the caller has ASCII-trimmed).</summary>
    internal static bool TryParseBoolean(string text, out bool value, out string? rule)
    {
        value = false;
        if (string.Equals(AsciiLower(text), "true", StringComparison.Ordinal))
        {
            value = true;
            rule = null;
            return true;
        }

        if (string.Equals(AsciiLower(text), "false", StringComparison.Ordinal))
        {
            rule = null;
            return true;
        }

        rule = "not a boolean: only true or false, in any case (not 1, 0, yes, no, on or off)";
        return false;
    }

    /// <summary>A finite number in invariant floating-point notation; the record's own range applies after.</summary>
    internal static bool TryParseDouble(string text, out double value, out string? rule)
    {
        value = 0;
        rule = "not a finite decimal number in invariant notation (for example 1.5)";
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value))
        {
            return false;
        }

        rule = null;
        return true;
    }

    /// <summary>A duration (CFG-7).</summary>
    internal static bool TryParseDuration(string text, out TimeSpan value, out string? rule) =>
        DurationGrammar.TryParse(text, out value, out rule);

    /// <summary>A duration, or <c>none</c> (any case) for <see langword="null"/>.</summary>
    internal static bool TryParseOptionalDuration(string text, out TimeSpan? value, out string? rule)
    {
        if (string.Equals(AsciiLower(text), "none", StringComparison.Ordinal))
        {
            value = null;
            rule = null;
            return true;
        }

        var parsed = DurationGrammar.TryParse(text, out var duration, out rule);
        value = parsed ? duration : null;
        if (!parsed)
        {
            rule += "; or none";
        }

        return parsed;
    }

    /// <summary>An enum by name only, case-insensitive (OBS-35): a number, an undefined value or any other name fails and the rule lists the names.</summary>
    internal static bool TryParseEnum<TEnum>(string text, out TEnum value, out string? rule)
        where TEnum : struct, Enum
    {
        value = default;
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase) && !text.Any(static c => c > '\u007F'))
            {
                value = Enum.Parse<TEnum>(name);
                rule = null;
                return true;
            }
        }

        rule = "not one of the names " + string.Join(", ", Enum.GetNames<TEnum>()) + " (by name only, in any case)";
        return false;
    }

    /// <summary>An absolute URI; the record checks scheme and fragment.</summary>
    internal static bool TryParseAbsoluteUri(string text, out Uri value, out string? rule)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            value = uri;
            rule = null;
            return true;
        }

        value = null!;
        rule = "not an absolute URI";
        return false;
    }

    /// <summary>Any non-empty string.</summary>
    internal static bool TryParseString(string text, out string value, out string? rule)
    {
        value = text;
        rule = null;
        return true;
    }

    /// <summary>
    /// Splits a scalar list on commas, trimming each item; an empty item is a failure, never silently dropped. <c>none</c> is the empty list.
    /// </summary>
    /// <param name="text">The ASCII-trimmed, non-empty text.</param>
    /// <param name="items">The items.</param>
    /// <param name="rule">The failure's rule.</param>
    /// <returns>Whether the text split cleanly.</returns>
    internal static bool TrySplitList(string text, out List<string> items, out string? rule)
    {
        items = [];
        rule = null;
        if (string.Equals(AsciiLower(text), "none", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var part in text.Split(','))
        {
            var item = AsciiTrim(part);
            if (item.Length == 0)
            {
                rule = "a list item is empty (a stray or trailing comma)";
                return false;
            }

            items.Add(item);
        }

        return true;
    }

    private static bool IsSignedDigits(string text)
    {
        var start = text.Length > 0 && text[0] is '+' or '-' ? 1 : 0;
        if (text.Length == start)
        {
            return false;
        }

        for (var i = start; i < text.Length; i++)
        {
            if (text[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
