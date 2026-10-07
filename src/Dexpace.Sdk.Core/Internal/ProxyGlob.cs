// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using System.Text.RegularExpressions;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// The proxy bypass-list glob dialect (CFG-23, P5a-14): <c>*</c> matches any run of characters, <c>?</c> exactly one,
/// every other character is a literal (a backslash included), the match is full-string and case-insensitive.
/// </summary>
/// <remarks>
/// A glob compiles once to a <see cref="RegexOptions.NonBacktracking"/> expression anchored with <c>\A</c> and
/// <c>\z</c>: <c>\z</c> rather than <c>$</c>, so a host with a trailing newline is not a match, and no
/// <see cref="RegexOptions.Singleline"/>, so <c>.</c> never crosses a newline. A non-backtracking engine cannot be driven
/// into catastrophic backtracking by a pattern such as <c>*a*a*a*b</c>, so no timeout is needed. Like the specification
/// and unlike curl, a leading dot is a literal, not a domain suffix.
/// </remarks>
internal static class ProxyGlob
{
    private const RegexOptions Options = RegexOptions.NonBacktracking | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>Compiles <paramref name="glob"/> to an anchored regular expression.</summary>
    /// <param name="glob">The glob text.</param>
    /// <returns>The compiled expression.</returns>
    internal static Regex Compile(string glob)
    {
        var body = new StringBuilder(@"\A");
        foreach (var c in glob)
        {
            switch (c)
            {
                case '*':
                    body.Append(".*");
                    break;
                case '?':
                    body.Append('.');
                    break;
                default:
                    body.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        body.Append(@"\z");
        return new Regex(body.ToString(), Options);
    }

    /// <summary>Whether any pattern matches the whole of <paramref name="host"/>.</summary>
    /// <param name="patterns">The compiled patterns.</param>
    /// <param name="host">The host name to test.</param>
    /// <returns><see langword="true"/> when a pattern matches.</returns>
    internal static bool Matches(Regex[] patterns, string host)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.IsMatch(host))
            {
                return true;
            }
        }

        return false;
    }
}
