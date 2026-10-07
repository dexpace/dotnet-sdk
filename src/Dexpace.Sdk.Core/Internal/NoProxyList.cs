// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>The result of splitting a <c>NO_PROXY</c> value.</summary>
/// <param name="Tokens">The bypass patterns; empty when <paramref name="BypassAll"/> is set.</param>
/// <param name="BypassAll">Whether the list was exactly one bare <c>*</c> (CFG-27).</param>
internal readonly record struct NoProxyResult(IReadOnlyList<string> Tokens, bool BypassAll);

/// <summary>
/// Splits a <c>NO_PROXY</c> value (CFG-26, CFG-27): on commas not preceded by a backslash, dropping empty fragments, then
/// replacing <c>\,</c> with <c>,</c>, then trimming, in that order. A whitespace-only fragment therefore survives as an
/// empty token. A list that is then exactly one <c>*</c> is reported as bypass-all, not as a token. The pipe-separated
/// system-property form of CFG-26 has no .NET source.
/// </summary>
internal static class NoProxyList
{
    /// <summary>Splits <paramref name="value"/>.</summary>
    /// <param name="value">The raw variable value.</param>
    /// <returns>The tokens, or the bypass-all flag.</returns>
    internal static NoProxyResult Parse(string value)
    {
        var tokens = new List<string>();
        foreach (var fragment in SplitOnUnescapedCommas(value))
        {
            if (fragment.Length == 0)
            {
                continue;
            }

            tokens.Add(fragment.Replace("\\,", ",", StringComparison.Ordinal).Trim());
        }

        return tokens is ["*"] ? new NoProxyResult([], BypassAll: true) : new NoProxyResult(tokens, BypassAll: false);
    }

    private static List<string> SplitOnUnescapedCommas(string value)
    {
        var fragments = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == ',' && (i == 0 || value[i - 1] != '\\'))
            {
                fragments.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fragments.Add(current.ToString());
        return fragments;
    }
}
