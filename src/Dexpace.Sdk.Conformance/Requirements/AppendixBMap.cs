// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Globalization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The many-to-one view from requirement IDs to the checklist items of appendix B that cover them (design §9.3, P8a-14).
/// Phase 8a holds B.6 (transport, five items) and B.7 (asynchronous runtime adapter, six items: the design said five, plan
/// reading R1); phase 10 widens it to all 61 items. Each key is <c>B.&lt;section&gt;.&lt;n&gt;</c>, the n-th checklist bullet
/// of the section. A Unit test parses the appendix and compares, so the table cannot drift from it.
/// </summary>
internal static class AppendixBMap
{
    /// <summary>The requirement IDs each checklist item covers, in appendix order.</summary>
    internal static FrozenDictionary<string, string[]> Items { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["B.6.1"] = [.. Ids("TRANSPORT", 1, 2)],
        ["B.6.2"] = [.. Ids("TRANSPORT", 3, 9)],
        ["B.6.3"] = [.. Ids("TRANSPORT", 10, 14)],
        ["B.6.4"] = [.. Ids("TRANSPORT", 15, 19)],
        ["B.6.5"] = [.. Ids("TRANSPORT", 20, 30)],
        ["B.7.1"] = [.. Ids("ASYNC", 1, 2)],
        ["B.7.2"] = [.. Ids("ASYNC", 3, 7)],
        ["B.7.3"] = [.. Ids("ASYNC", 8, 12)],
        ["B.7.4"] = [.. Ids("ASYNC", 13, 14)],
        ["B.7.5"] = [.. Ids("ASYNC", 15, 17)],
        ["B.7.6"] = [.. Ids("ASYNC", 18, 22)],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static IEnumerable<string> Ids(string prefix, int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(n => string.Create(CultureInfo.InvariantCulture, $"{prefix}-{n}"));
}
