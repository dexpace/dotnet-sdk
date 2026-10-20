// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using System.Text.RegularExpressions;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// A small C# source scanner for the fatal-filter gate (RECOV-2, RETRY-25, design §10 entry 12): it finds every catch-all in a source text and decides whether
/// it lets a fatal exception through. It reads the text, not the syntax tree (no Roslyn package is added to the test project), so it first masks comments,
/// string literals and character literals, which keeps their characters out of the brace, parenthesis and keyword matching without moving a line.
/// </summary>
/// <remarks>
/// <b>Catch-all</b>: <c>catch</c>, <c>catch when (...)</c>, or a catch of <c>Exception</c> / <c>System.Exception</c>, with or without a variable. A typed catch
/// (<c>catch (IOException)</c>) is not one. <b>Compliant</b> means an unfiltered catch-all whose handler ends in a bare <c>throw;</c> (or rethrows a fatal
/// exception in an <c>if (ExceptionFacts.IsFatal(ex)) { throw; }</c> block), or a filtered one whose <c>when</c> excludes fatal exceptions: it calls
/// <c>!ExceptionFacts.IsFatal(...)</c>, <c>EmissionGuard.Reportable(...)</c>, <c>IsClosedInFlight(...)</c> or <c>SendFailures.IsTransportFailure(...)</c> (each
/// of which is built on <c>IsFatal</c> or tests two named non-fatal types), or is a typed <c>is</c> test over named non-fatal types.
/// </remarks>
internal static partial class FatalFilterScanner
{
    private static readonly string[] s_tooBroadForATypedFilter = ["Exception", "System.Exception", "SystemException", "System.SystemException", "OutOfMemoryException", "System.OutOfMemoryException", "InsufficientMemoryException"];

    /// <summary>One catch-all: where it is, its <c>when</c> filter (null when it has none) and its handler body, all with comments and literals masked.</summary>
    /// <param name="File">The file the catch is in, as given to <see cref="CatchAlls"/>.</param>
    /// <param name="Line">The 1-based line of the <c>catch</c> keyword.</param>
    /// <param name="Filter">The text between the parentheses of the <c>when</c> clause, or null when there is none.</param>
    /// <param name="Body">The text between the braces of the handler.</param>
    internal sealed record CatchSite(string File, int Line, string? Filter, string Body);

    [GeneratedRegex(@"\bcatch\b", RegexOptions.CultureInvariant)]
    private static partial Regex CatchKeyword();

    [GeneratedRegex(@"(^|[;{}])\s*throw\s*;\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex EndsInBareThrow();

    [GeneratedRegex(@"if\s*\(\s*ExceptionFacts\.IsFatal\(\s*\w+\s*\)\s*\)\s*\{\s*throw\s*;\s*\}", RegexOptions.CultureInvariant)]
    private static partial Regex RethrowsAFatalException();

    [GeneratedRegex(@"^\s*\w+\s+is\s+(?<types>[A-Z]\w*(?:\.\w+)*(?:\s+or\s+[A-Z]\w*(?:\.\w+)*)*)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TypedIsTest();

    /// <summary>Every catch-all in <paramref name="text"/>.</summary>
    /// <param name="file">A label for the file, carried into each site.</param>
    /// <param name="text">The C# source.</param>
    /// <returns>The catch-all sites, in source order.</returns>
    internal static List<CatchSite> CatchAlls(string file, string text)
    {
        var masked = Mask(text);
        var sites = new List<CatchSite>();
        foreach (Match keyword in CatchKeyword().Matches(masked))
        {
            var position = SkipWhitespace(masked, keyword.Index + keyword.Length);
            var catchAll = true;
            if (position < masked.Length && masked[position] == '(')
            {
                var close = Matching(masked, position, '(', ')');
                if (close < 0)
                {
                    continue;
                }

                var declared = masked[(position + 1)..close].Trim();
                var type = declared.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                catchAll = type is "Exception" or "System.Exception";
                position = SkipWhitespace(masked, close + 1);
            }

            string? filter = null;
            if (string.CompareOrdinal(masked, position, "when", 0, 4) == 0)
            {
                var open = SkipWhitespace(masked, position + 4);
                var close = open < masked.Length && masked[open] == '(' ? Matching(masked, open, '(', ')') : -1;
                if (close < 0)
                {
                    continue;
                }

                filter = masked[(open + 1)..close].Trim();
                position = SkipWhitespace(masked, close + 1);
            }

            if (!catchAll || position >= masked.Length || masked[position] != '{')
            {
                continue;
            }

            var end = Matching(masked, position, '{', '}');
            if (end < 0)
            {
                continue;
            }

            var line = 1 + masked.AsSpan(0, keyword.Index).Count('\n');
            sites.Add(new CatchSite(file, line, filter, masked[(position + 1)..end]));
        }

        return sites;
    }

    /// <summary>Why <paramref name="site"/> lets a fatal exception through, or null when it does not.</summary>
    /// <param name="site">A catch-all.</param>
    /// <returns>The reason, or null when the site is compliant.</returns>
    internal static string? Violation(CatchSite site)
    {
        ArgumentNullException.ThrowIfNull(site);
        if (site.Filter is null)
        {
            return EndsInBareThrow().IsMatch(site.Body) || RethrowsAFatalException().IsMatch(site.Body)
                ? null
                : "an unfiltered catch-all whose handler neither ends in `throw;` nor rethrows a fatal exception swallows OutOfMemoryException";
        }

        return ExcludesFatalExceptions(site.Filter)
            ? null
            : $"the filter `when ({site.Filter})` does not exclude fatal exceptions: call !ExceptionFacts.IsFatal, or test named non-fatal types";
    }

    private static bool ExcludesFatalExceptions(string filter)
    {
        // A disjunction can let a fatal exception through on its other arm, so a filter with `||` is judged non-compliant outright.
        if (filter.Contains("||", StringComparison.Ordinal))
        {
            return false;
        }

        if (filter.Contains("!ExceptionFacts.IsFatal(", StringComparison.Ordinal)
            || filter.Contains("EmissionGuard.Reportable(", StringComparison.Ordinal)
            || filter.Contains("IsClosedInFlight(", StringComparison.Ordinal)
            || filter.Contains("SendFailures.IsTransportFailure(", StringComparison.Ordinal))
        {
            return true;
        }

        var typed = TypedIsTest().Match(filter);
        return typed.Success
            && typed.Groups["types"].Value.Split(" or ", StringSplitOptions.TrimEntries).All(type => !s_tooBroadForATypedFilter.Contains(type, StringComparer.Ordinal));
    }

    private static int SkipWhitespace(string text, int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        return position;
    }

    // The index of the bracket closing the one at `open`, or -1; the text is masked, so no bracket sits in a literal or a comment.
    private static int Matching(string text, int open, char opening, char closing)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == opening)
            {
                depth++;
            }
            else if (text[i] == closing && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Replaces comments, string literals (regular, verbatim, interpolated, raw) and character literals with spaces, keeping every line break.</summary>
    /// <param name="text">The C# source.</param>
    /// <returns>The same text with those regions blanked.</returns>
    internal static string Mask(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var masked = new StringBuilder(text);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = Blank(masked, i, IndexOrEnd(text, '\n', i));
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = Blank(masked, i, close < 0 ? text.Length : close + 2);
            }
            else if (text[i] == '"')
            {
                i = Blank(masked, i, EndOfString(text, i));
            }
            else if (text[i] == '\'')
            {
                i = Blank(masked, i, EndOfCharacter(text, i));
            }
            else
            {
                i++;
            }
        }

        return masked.ToString();
    }

    private static int IndexOrEnd(string text, char value, int from)
    {
        var index = text.IndexOf(value, from);
        return index < 0 ? text.Length : index;
    }

    private static int Blank(StringBuilder masked, int from, int to)
    {
        for (var i = from; i < to && i < masked.Length; i++)
        {
            if (masked[i] is not ('\n' or '\r'))
            {
                masked[i] = ' ';
            }
        }

        return to;
    }

    // The index one past the string literal opening at `start` (the quote); an @ or $ prefix before the quote is left as code, which no matching reads.
    private static int EndOfString(string text, int start)
    {
        var quotes = 0;
        while (start + quotes < text.Length && text[start + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3)
        {
            // A raw string closes on a run of the same number of quotes.
            var closing = new string('"', quotes);
            var close = text.IndexOf(closing, start + quotes, StringComparison.Ordinal);
            return close < 0 ? text.Length : close + quotes;
        }

        if (quotes == 2)
        {
            return start + 2;
        }

        var verbatim = start > 0 && (text[start - 1] == '@' || (start > 1 && text[start - 1] == '$' && text[start - 2] == '@'));
        for (var i = start + 1; i < text.Length; i++)
        {
            if (verbatim && text[i] == '"')
            {
                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                return i + 1;
            }

            if (!verbatim && text[i] == '\\')
            {
                i++;
                continue;
            }

            if (!verbatim && text[i] == '"')
            {
                return i + 1;
            }

            if (!verbatim && text[i] == '\n')
            {
                return i;
            }
        }

        return text.Length;
    }

    private static int EndOfCharacter(string text, int start)
    {
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == '\'')
            {
                return i + 1;
            }
            else if (text[i] == '\n')
            {
                return i;
            }
        }

        return text.Length;
    }
}
