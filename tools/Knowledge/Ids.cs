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

    public static string PrefixOf(string id) => id[..id.LastIndexOf('-')];

    public static long NumberOf(string id) =>
        long.TryParse(id[(id.LastIndexOf('-') + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n
            : long.MaxValue;

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
