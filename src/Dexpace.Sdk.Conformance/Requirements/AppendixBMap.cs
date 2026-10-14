// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Globalization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The many-to-one view from requirement IDs to the checklist items of appendix B that cover them (design §9.3, P8a-14, widened by phase 10, P10-7).
/// All 61 items of all nine sections, B.1 to B.9; phase 8a held only B.6 (transport, five items) and B.7 (asynchronous runtime adapter, six items: the
/// design said five, plan reading R1). Each key is <c>B.&lt;section&gt;.&lt;n&gt;</c>, the n-th checklist bullet of the section; each value is the IDs the
/// bullet cites, in the order the specification prints them (B.1.6 prints <c>PAGE-20</c> before <c>PAGE-19</c>). Together they hold 276 distinct IDs,
/// none in two items. Appendix B is silent on <c>PAGE-4</c> and <c>PAGE-5</c> (design §11 item 9). A Unit test parses the appendix and compares ID sets,
/// so the table cannot drift from it, and another compares it with <c>APPENDIX_B.md</c>, the map of where each item is evidenced.
/// </summary>
internal static class AppendixBMap
{
    /// <summary>The requirement IDs each checklist item covers, in appendix order.</summary>
    internal static FrozenDictionary<string, string[]> Items { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["B.1.1"] = [.. Ids("PAGE", 1, 3)],
        ["B.1.2"] = [.. Ids("PAGE", 6, 8)],
        ["B.1.3"] = ["PAGE-9", "PAGE-10"],
        ["B.1.4"] = ["PAGE-11", "PAGE-12", "PAGE-14", "PAGE-15"],
        ["B.1.5"] = ["PAGE-13"],
        ["B.1.6"] = [.. Ids("PAGE", 16, 18), "PAGE-20", "PAGE-19"],
        ["B.1.7"] = [.. Ids("PAGE", 21, 24)],
        ["B.1.8"] = [.. Ids("PAGE", 25, 33)],
        ["B.1.9"] = ["PAGE-34", "PAGE-35"],
        ["B.1.10"] = ["PAGE-36"],
        ["B.2.1"] = [.. Ids("SSE", 1, 12)],
        ["B.2.2"] = [.. Ids("SSE", 13, 19)],
        ["B.2.3"] = [.. Ids("SSE", 20, 22)],
        ["B.2.4"] = [.. Ids("SSE", 23, 32)],
        ["B.2.5"] = [.. Ids("SSE", 33, 36)],
        ["B.2.6"] = [.. Ids("SSE", 37, 41)],
        ["B.3.1"] = ["SERDE-1", "SERDE-2"],
        ["B.3.2"] = ["SERDE-3", "SERDE-4"],
        ["B.3.3"] = [.. Ids("SERDE", 5, 8)],
        ["B.3.4"] = [.. Ids("SERDE", 9, 13)],
        ["B.3.5"] = [.. Ids("SERDE", 14, 20), "SERDE-30"],
        ["B.3.6"] = [.. Ids("SERDE", 21, 26), "SERDE-29"],
        ["B.3.7"] = ["SERDE-27", "SERDE-28"],
        ["B.4.1"] = [.. Ids("OBS", 1, 9), "OBS-40"],
        ["B.4.2"] = ["OBS-10"],
        ["B.4.3"] = [.. Ids("OBS", 11, 19)],
        ["B.4.4"] = ["OBS-20"],
        ["B.4.5"] = [.. Ids("OBS", 21, 25)],
        ["B.4.6"] = [.. Ids("OBS", 26, 30)],
        ["B.4.7"] = [.. Ids("OBS", 31, 33)],
        ["B.4.8"] = [.. Ids("OBS", 34, 39)],
        ["B.5.1"] = [.. Ids("CFG", 1, 4), "CFG-38"],
        ["B.5.2"] = [.. Ids("CFG", 5, 7)],
        ["B.5.3"] = [.. Ids("CFG", 8, 14), "CFG-37"],
        ["B.5.4"] = [.. Ids("CFG", 15, 21)],
        ["B.5.5"] = [.. Ids("CFG", 22, 28)],
        ["B.5.6"] = [.. Ids("CFG", 29, 36)],
        ["B.6.1"] = ["TRANSPORT-1", "TRANSPORT-2"],
        ["B.6.2"] = [.. Ids("TRANSPORT", 3, 9)],
        ["B.6.3"] = [.. Ids("TRANSPORT", 10, 14)],
        ["B.6.4"] = [.. Ids("TRANSPORT", 15, 19)],
        ["B.6.5"] = [.. Ids("TRANSPORT", 20, 30)],
        ["B.7.1"] = ["ASYNC-1", "ASYNC-2"],
        ["B.7.2"] = [.. Ids("ASYNC", 3, 7)],
        ["B.7.3"] = [.. Ids("ASYNC", 8, 12)],
        ["B.7.4"] = ["ASYNC-13", "ASYNC-14"],
        ["B.7.5"] = [.. Ids("ASYNC", 15, 17)],
        ["B.7.6"] = [.. Ids("ASYNC", 18, 22)],
        ["B.8.1"] = [.. Ids("XCUT", 1, 3)],
        ["B.8.2"] = [.. Ids("XCUT", 4, 9)],
        ["B.8.3"] = ["XCUT-10"],
        ["B.8.4"] = [.. Ids("XCUT", 11, 13), "XCUT-22", "XCUT-23"],
        ["B.8.5"] = ["XCUT-14", "XCUT-15"],
        ["B.8.6"] = [.. Ids("XCUT", 16, 21), "XCUT-24"],
        ["B.9.1"] = ["NFR-1", "NFR-2"],
        ["B.9.2"] = ["NFR-3", "NFR-4"],
        ["B.9.3"] = [.. Ids("NFR", 5, 7), "NFR-17"],
        ["B.9.4"] = ["NFR-8", "NFR-9"],
        ["B.9.5"] = ["NFR-10"],
        ["B.9.6"] = ["NFR-11"],
        ["B.9.7"] = [.. Ids("NFR", 12, 16)],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static IEnumerable<string> Ids(string prefix, int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(n => string.Create(CultureInfo.InvariantCulture, $"{prefix}-{n}"));
}
