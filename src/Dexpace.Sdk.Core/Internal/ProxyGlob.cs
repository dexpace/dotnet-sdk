// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// The proxy bypass-list glob dialect (CFG-23, P5a-14): <c>*</c> matches any run of characters, <c>?</c> exactly one,
/// every other character is a literal (a backslash included), the match is full-string and case-insensitive.
/// </summary>
/// <remarks>
/// A hand-written matcher rather than a regular expression: it has no pattern-size limit (so an arbitrarily long
/// token can never make construction or resolution throw, CFG-24), needs no compilation, and runs in at most
/// <c>O(pattern x host)</c> time because it remembers only the most recent <c>*</c>. A wildcard never matches a line
/// feed, and a host with a trailing newline is not a match. Like the specification and unlike curl, a leading dot is a
/// literal, not a domain suffix.
/// </remarks>
internal static class ProxyGlob
{
    /// <summary>Validates and normalises <paramref name="glob"/> for matching.</summary>
    /// <param name="glob">The glob text.</param>
    /// <returns>The pattern, ready for <see cref="Matches"/>.</returns>
    internal static string Compile(string glob) => glob;

    /// <summary>Whether any pattern matches the whole of <paramref name="host"/>.</summary>
    /// <param name="patterns">The patterns.</param>
    /// <param name="host">The host name to test.</param>
    /// <returns><see langword="true"/> when a pattern matches.</returns>
    internal static bool Matches(string[] patterns, string host)
    {
        foreach (var pattern in patterns)
        {
            if (IsMatch(pattern, host))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMatch(string pattern, string host)
    {
        int p = 0, h = 0, star = -1, mark = 0;
        while (h < host.Length)
        {
            if (p < pattern.Length && pattern[p] == '*' && host[h] != '\n')
            {
                star = p++;
                mark = h;
            }
            else if (p < pattern.Length && pattern[p] != '*' && SingleMatches(pattern[p], host[h]))
            {
                p++;
                h++;
            }
            else if (star >= 0 && host[mark] != '\n')
            {
                p = star + 1;
                h = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    private static bool SingleMatches(char pattern, char c) =>
        pattern == '?' ? c != '\n' : char.ToLowerInvariant(pattern) == char.ToLowerInvariant(c);
}
