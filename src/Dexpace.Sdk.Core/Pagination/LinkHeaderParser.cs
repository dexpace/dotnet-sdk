// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// Finds the <c>next</c> link of an RFC 8288 <c>Link</c> header value (PAGE-18, PAGE-20).
/// </summary>
/// <remarks>
/// <para>
/// A regular expression is the wrong tool and a single-pass scanner the right one, because the separators are
/// context-sensitive in two directions: a comma ends a link-value only outside both <c>&lt;…&gt;</c> and a quoted
/// string, and a semicolon ends a parameter under the same condition. A quoted string honours quoted-pair
/// (<c>\"</c>, <c>\\</c>), so quote tracking is not a toggle.
/// </para>
/// <para>
/// The scanner never throws. A malformed link-value (no <c>&lt;</c>) is skipped to the next top-level comma; an
/// unterminated <c>&lt;</c> or quoted string ends the scan, because nothing after it can be trusted. Only the first
/// <c>rel</c> parameter of a link-value counts (RFC 8288 section 3.3). The result is the raw URI-reference, trimmed of
/// SP and HTAB; resolving it is <c>LinkTarget</c>'s job.
/// </para>
/// </remarks>
internal static class LinkHeaderParser
{
    /// <summary>
    /// Returns the target of the first link-value whose first <c>rel</c> parameter lists the token <c>next</c>
    /// (case-insensitive, SP/HTAB separated, quoted or unquoted).
    /// </summary>
    /// <param name="value">
    /// The header value; for several header instances, the instances already joined with <c>", "</c> (PAGE-20).
    /// </param>
    /// <returns>The raw URI-reference between the angle brackets, or <see langword="null"/> when there is none.</returns>
    internal static string? FindNext(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var i = 0;
        while (true)
        {
            SkipSeparators(value, ref i);
            if (i >= value.Length)
            {
                return null;
            }

            if (value[i] != '<')
            {
                SkipToTopLevelComma(value, ref i);
                continue;
            }

            var close = value.IndexOf('>', i + 1);
            if (close < 0)
            {
                return null;
            }

            var target = value[(i + 1)..close];
            i = close + 1;
            if (!ReadRel(value, ref i, out var rel))
            {
                return null;
            }

            if (HasNextToken(rel))
            {
                return target.Trim(' ', '\t');
            }
        }
    }

    // Reads the parameters of one link-value, up to (not including) the next top-level comma or the end, and returns
    // the value of the first `rel` parameter (null when absent or valueless). False means a quoted string was
    // unterminated: the rest of the header is untrusted.
    private static bool ReadRel(string value, ref int i, out string? rel)
    {
        rel = null;
        var seenRel = false;
        while (true)
        {
            SkipWhitespace(value, ref i);
            if (i >= value.Length || value[i] == ',')
            {
                return true;
            }

            if (value[i] != ';')
            {
                // Garbage between the target and the next parameter: skip to the next boundary.
                if (!SkipToParameterBoundary(value, ref i))
                {
                    return false;
                }

                continue;
            }

            i++;
            SkipWhitespace(value, ref i);
            var name = ReadToken(value, ref i);
            SkipWhitespace(value, ref i);
            string? parameterValue = null;
            if (i < value.Length && value[i] == '=')
            {
                i++;
                SkipWhitespace(value, ref i);
                if (!ReadParameterValue(value, ref i, out parameterValue))
                {
                    i = value.Length;
                    return false;
                }
            }

            if (!seenRel && name.Equals("rel", StringComparison.OrdinalIgnoreCase))
            {
                seenRel = true;
                rel = parameterValue;
            }
        }
    }

    // A parameter value is a quoted-string or a token; false only for an unterminated quoted-string.
    private static bool ReadParameterValue(string value, ref int i, out string? parameterValue)
    {
        if (i < value.Length && value[i] == '"')
        {
            return ReadQuoted(value, ref i, out parameterValue);
        }

        parameterValue = ReadToken(value, ref i);
        return true;
    }

    // Reads a quoted-string starting at the opening quote, unescaping quoted-pair; false when unterminated.
    private static bool ReadQuoted(string value, ref int i, out string? text)
    {
        var builder = new StringBuilder();
        i++;
        while (i < value.Length)
        {
            var ch = value[i++];
            if (ch == '"')
            {
                text = builder.ToString();
                return true;
            }

            if (ch == '\\' && i < value.Length)
            {
                ch = value[i++];
            }

            builder.Append(ch);
        }

        text = null;
        return false;
    }

    // A token ends at whitespace, '=', ';' or ','.
    private static string ReadToken(string value, ref int i)
    {
        var start = i;
        while (i < value.Length && value[i] is not (' ' or '\t' or '=' or ';' or ','))
        {
            i++;
        }

        return value[start..i];
    }

    private static bool HasNextToken(string? rel)
    {
        if (rel is null)
        {
            return false;
        }

        foreach (var token in rel.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("next", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void SkipSeparators(string value, ref int i)
    {
        while (i < value.Length && value[i] is ' ' or '\t' or ',')
        {
            i++;
        }
    }

    private static void SkipWhitespace(string value, ref int i)
    {
        while (i < value.Length && value[i] is ' ' or '\t')
        {
            i++;
        }
    }

    // Skips garbage to the next ';' or top-level comma; false when it ran into an unterminated quote.
    private static bool SkipToParameterBoundary(string value, ref int i)
    {
        while (i < value.Length && value[i] is not (';' or ','))
        {
            if (value[i] == '"')
            {
                if (!ReadQuoted(value, ref i, out _))
                {
                    i = value.Length;
                    return false;
                }
            }
            else
            {
                i++;
            }
        }

        return true;
    }

    // Skips a malformed link-value: to the next comma outside "…" and <…>. An unterminated quote or bracket
    // runs to the end, which ends the scan.
    private static void SkipToTopLevelComma(string value, ref int i)
    {
        while (i < value.Length && value[i] != ',')
        {
            switch (value[i])
            {
                case '"':
                    if (!ReadQuoted(value, ref i, out _))
                    {
                        i = value.Length;
                    }

                    break;
                case '<':
                    var close = value.IndexOf('>', i + 1);
                    i = close < 0 ? value.Length : close + 1;
                    break;
                default:
                    i++;
                    break;
            }
        }
    }
}
