// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dexpace.Tools.Knowledge;

/// <summary>
/// Requirement-ID arithmetic. A bare <c>\b[A-Z]{2,12}-\d+\b</c> is not a requirement-ID matcher: it
/// also claims <c>UTF-8</c>, <c>SHA-256</c>, <c>ISO-8601</c> and <c>RFC-3986</c>. The only authority on
/// what a requirement ID looks like is appendix C, so every function here takes the derived prefix
/// allowlist.
/// </summary>
internal static partial class Ids
{
    // ECMAScript mode keeps \b and \d ASCII, as Ruby's are: a .NET \b would treat an accented letter
    // before the ID as a word character and hide the token.
    [GeneratedRegex(@"\b[A-Z][A-Z0-9]{1,11}-[0-9]+\b", RegexOptions.ECMAScript)]
    private static partial Regex Token();

    // A token that may open a range: groups 1-2 are the lower ID's prefix and number. What follows is either
    // the full form, `-HTTP-35` (groups 3-4, a space allowed each side of the separator), or the short form,
    // `-35` (group 5, no spaces, because `HTTP-1 - 3 retries` is prose). The separator is an en dash
    // (U+2013), a hyphen or `..`, the last being what Compress prints. Up to two backticks or asterisks may
    // close the lower endpoint and open the upper one, because docs/work writes `REDIR-3`–`REDIR-5` and
    // **HTTP-28**–**HTTP-32** far more often than a bare range. An underscore is left out: `_` is a word
    // character, so `\b` already refuses `_HTTP-1_` as a token and a range could not rescue it.
    [GeneratedRegex(
        @"\b([A-Z][A-Z0-9]{1,11})-([0-9]+)(?:[`*]{0,2} ?(?:[\u2013-]|\.\.) ?[`*]{0,2}([A-Z][A-Z0-9]{1,11})-([0-9]+)|[`*]{0,2}(?:[\u2013-]|\.\.)([0-9]+))?\b",
        RegexOptions.ECMAScript)]
    private static partial Regex TokenOrRange();

    public static string PrefixOf(string id) => id[..id.LastIndexOf('-')];

    public static long NumberOf(string id) => ParseNumber(id[(id.LastIndexOf('-') + 1)..]);

    // Digits too many for a long saturate: such a number is bigger than any ID appendix C holds, which is
    // all the callers need to know about it.
    private static long ParseNumber(string digits) =>
        long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : long.MaxValue;

    /// <summary>
    /// Tokenize, then compare whole tokens. Never substring-match: <c>HTTP-7</c> and <c>HTTP-70</c>
    /// are different requirements and a substring test conflates them.
    /// </summary>
    public static List<string> Extract(string text, IReadOnlySet<string> prefixes)
    {
        var found = new List<string>();
        foreach (Match match in Token().Matches(text))
        {
            var token = match.Value;
            if (prefixes.Contains(PrefixOf(token)) && !found.Contains(token))
            {
                found.Add(token);
            }
        }

        return found;
    }

    /// <summary>
    /// <see cref="Extract"/>, plus the range a document writes in place of listing every ID:
    /// <c>HTTP-1–HTTP-35</c>, <c>HTTP-1-HTTP-35</c>, <c>HTTP-1..HTTP-35</c> and the short forms
    /// <c>HTTP-1–35</c>, <c>HTTP-1-35</c>, <c>HTTP-1..35</c>, each also with its endpoints in code or bold
    /// (<c>`HTTP-1`–`HTTP-35`</c>, <c>**HTTP-1**–**HTTP-35**</c>). A range credits its two endpoints as written
    /// and every ID appendix C defines between them — never an integer appendix C lacks, and never more than
    /// the family holds, so a typo'd upper bound is bounded by the table rather than by the number.
    /// </summary>
    /// <remarks>
    /// A range is one prefix at both ends. A reversed one (<c>HTTP-35–HTTP-1</c>), one that spans two prefixes
    /// (<c>HTTP-1–PAGE-2</c>) and one over a prefix the allowlist does not hold (<c>UTF-8–UTF-16</c>) expand to
    /// nothing, and each endpoint the text names is then credited exactly as <see cref="Extract"/> would
    /// credit it. A short form's upper end is only a number, so it names an ID solely by closing a valid range:
    /// <c>HTTP-35–3</c> and <c>RETRY-12-3 times</c> credit the lower ID alone, never <c>HTTP-3</c> or
    /// <c>RETRY-3</c>. Used for a phase's documents, whose job is to say what they depend on; harvested entries
    /// and spec prose keep the exact-token reading, because their citations are a pinned, reviewed set.
    /// </remarks>
    public static List<string> ExtractWithRanges(
        string text, IReadOnlySet<string> prefixes, IReadOnlyList<string> canonicalIds)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TokenOrRange().Matches(text))
        {
            var prefix = match.Groups[1].Value;
            var lower = match.Groups[2].Value;
            Credit(found, seen, prefixes, $"{prefix}-{lower}");

            var fullForm = match.Groups[4].Success;
            var upper = fullForm ? match.Groups[4] : match.Groups[5];
            if (!upper.Success)
            {
                continue;
            }

            var (low, high) = (ParseNumber(lower), ParseNumber(upper.Value));
            if (!fullForm && low > high)
            {
                // `HTTP-35–3`, `RETRY-12-3 times`: a bare number is an ID only as the end of a valid range.
                continue;
            }

            var upperPrefix = fullForm ? match.Groups[3].Value : prefix;
            if (upperPrefix == prefix)
            {
                var between = canonicalIds.Where(id => PrefixOf(id) == prefix && NumberOf(id) >= low && NumberOf(id) <= high);
                foreach (var id in Sort(between))
                {
                    Credit(found, seen, prefixes, id);
                }
            }

            Credit(found, seen, prefixes, $"{upperPrefix}-{upper.Value}");
        }

        return found;
    }

    private static void Credit(List<string> found, HashSet<string> seen, IReadOnlySet<string> prefixes, string id)
    {
        if (prefixes.Contains(PrefixOf(id)) && seen.Add(id))
        {
            found.Add(id);
        }
    }

    public static int Compare(string left, string right)
    {
        var byPrefix = string.CompareOrdinal(PrefixOf(left), PrefixOf(right));
        return byPrefix != 0 ? byPrefix : NumberOf(left).CompareTo(NumberOf(right));
    }

    public static List<string> Sort(IEnumerable<string> ids)
    {
        var sorted = ids.ToList();
        sorted.Sort(Compare);
        return sorted;
    }

    /// <summary>
    /// <c>RETRY-1..45 RETRY-47</c> rather than 46 tokens. A phase checklist cites its whole family, and
    /// printing every ID three times over is most of the output.
    /// </summary>
    public static string Compress(IEnumerable<string> ids)
    {
        var runs = new List<List<string>>();
        foreach (var id in Sort(ids))
        {
            var last = runs.Count > 0 ? runs[^1] : null;
            if (last is not null && PrefixOf(last[0]) == PrefixOf(id) && NumberOf(last[^1]) + 1 == NumberOf(id))
            {
                last.Add(id);
            }
            else
            {
                runs.Add([id]);
            }
        }

        var builder = new StringBuilder();
        foreach (var run in runs)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(run.Count < 3
                ? string.Join(' ', run)
                : string.Create(CultureInfo.InvariantCulture, $"{run[0]}..{NumberOf(run[^1])}"));
        }

        return builder.ToString();
    }
}
