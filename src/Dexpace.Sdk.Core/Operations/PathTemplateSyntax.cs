// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Text;

namespace Dexpace.Sdk.Core.Operations;

/// <summary>
/// Parses and validates an <see cref="OperationDescriptor.PathTemplate"/> (SEAM-26) and renders it (SEAM-27).
/// </summary>
/// <remarks>
/// A template is literal text and <c>{name}</c> placeholders. A placeholder name is any non-empty run of characters
/// other than <c>{</c> and <c>}</c>, compared ordinally. Outside placeholders the text must be an RFC 3986 path
/// (<c>pchar</c>, <c>/</c>, <c>%HH</c>), and no literal-only segment may be a dot-segment: <c>System.Uri</c> removes
/// <c>.</c> and <c>..</c> segments, <c>%2E</c>-encoded ones included, so such a segment would escape the base path
/// (design position K).
/// </remarks>
internal static class PathTemplateSyntax
{
    /// <summary>One piece of a template: literal text as written, or a placeholder name.</summary>
    internal readonly record struct Part(string Text, bool IsPlaceholder);

    /// <summary>Parses and validates <paramref name="template"/>.</summary>
    /// <param name="template">The template.</param>
    /// <param name="paramName">The parameter name an <see cref="ArgumentException"/> carries.</param>
    /// <returns>The parts in order; adjacent literals are never split except by a placeholder.</returns>
    internal static ImmutableArray<Part> Parse(string template, string paramName)
    {
        var parts = ImmutableArray.CreateBuilder<Part>();
        var literalStart = 0;
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '}')
            {
                throw Invalid(template, paramName, "a '}' has no matching '{'");
            }

            if (c != '{')
            {
                continue;
            }

            AddLiteral(parts, template, literalStart, i, paramName);
            var close = FindClose(template, i, paramName);
            var name = template.Substring(i + 1, close - i - 1);
            if (name.Length == 0)
            {
                throw Invalid(template, paramName, "a placeholder has an empty name");
            }

            parts.Add(new Part(name, IsPlaceholder: true));
            i = close;
            literalStart = close + 1;
        }

        AddLiteral(parts, template, literalStart, template.Length, paramName);
        var result = parts.ToImmutable();
        RejectLiteralDotSegments(result, template, paramName);
        return result;
    }

    /// <summary>The distinct placeholder names of <paramref name="parts"/>, in first-appearance order.</summary>
    /// <param name="parts">Parsed template parts.</param>
    /// <returns>The names.</returns>
    internal static ImmutableArray<string> PlaceholderNames(ImmutableArray<Part> parts) =>
        [.. parts.Where(p => p.IsPlaceholder).Select(p => p.Text).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Whether <paramref name="segment"/> is <c>.</c> or <c>..</c> once <c>%2E</c> and <c>%2e</c> are read as <c>.</c>.
    /// </summary>
    /// <param name="segment">One path segment (no <c>/</c>).</param>
    /// <returns><see langword="true"/> for a dot-segment.</returns>
    internal static bool IsDotSegment(ReadOnlySpan<char> segment)
    {
        var dots = 0;
        var i = 0;
        while (i < segment.Length)
        {
            if (segment[i] == '.')
            {
                i++;
            }
            else if (segment[i] == '%' && i + 2 < segment.Length && segment[i + 1] == '2' && segment[i + 2] is 'e' or 'E')
            {
                i += 3;
            }
            else
            {
                return false;
            }

            if (++dots > 2)
            {
                return false;
            }
        }

        return dots is 1 or 2;
    }

    /// <summary>Whether <paramref name="text"/> holds a UTF-16 surrogate that is not part of a valid pair.</summary>
    /// <param name="text">The text to inspect.</param>
    /// <returns><see langword="true"/> when a lone surrogate is present.</returns>
    internal static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindClose(string template, int open, string paramName)
    {
        for (var j = open + 1; j < template.Length; j++)
        {
            if (template[j] == '}')
            {
                return j;
            }

            if (template[j] == '{')
            {
                break;
            }
        }

        throw Invalid(template, paramName, "a '{' has no matching '}'");
    }

    private static void AddLiteral(ImmutableArray<Part>.Builder parts, string template, int start, int end, string paramName)
    {
        if (end <= start)
        {
            return;
        }

        var literal = template.AsSpan(start, end - start);
        for (var i = 0; i < literal.Length; i++)
        {
            var c = literal[i];
            if (c == '%')
            {
                if (i + 2 >= literal.Length || !char.IsAsciiHexDigit(literal[i + 1]) || !char.IsAsciiHexDigit(literal[i + 2]))
                {
                    throw Invalid(template, paramName, "a '%' is not followed by two hexadecimal digits");
                }

                i += 2;
            }
            else if (!IsPathChar(c))
            {
                throw Invalid(template, paramName, $"the character U+{(int)c:X4} is not allowed in an RFC 3986 path");
            }
        }

        parts.Add(new Part(literal.ToString(), IsPlaceholder: false));
    }

    // pchar = unreserved / sub-delims / ":" / "@" (percent-encodings are handled by the caller), plus the "/" separator.
    private static bool IsPathChar(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~' or '!' or '$' or '&' or '\'' or '(' or ')'
            or '*' or '+' or ',' or ';' or '=' or ':' or '@' or '/';

    private static void RejectLiteralDotSegments(ImmutableArray<Part> parts, string template, string paramName)
    {
        var segment = new StringBuilder();
        var hasPlaceholder = false;

        void Finish()
        {
            if (!hasPlaceholder && IsDotSegment(segment.ToString()))
            {
                throw Invalid(template, paramName, "a literal dot-segment ('.' or '..') would escape the base path");
            }

            segment.Clear();
            hasPlaceholder = false;
        }

        foreach (var part in parts)
        {
            if (part.IsPlaceholder)
            {
                hasPlaceholder = true;
                continue;
            }

            var start = 0;
            int slash;
            while ((slash = part.Text.IndexOf('/', start)) >= 0)
            {
                segment.Append(part.Text, start, slash - start);
                Finish();
                start = slash + 1;
            }

            segment.Append(part.Text, start, part.Text.Length - start);
        }

        Finish();
    }

    private static ArgumentException Invalid(string template, string paramName, string reason) =>
        new($"The path template '{template}' is invalid: {reason}.", paramName);
}
