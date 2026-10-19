// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;
using static Dexpace.Sdk.Conformance.Tests.Support.TestAssertions;

namespace Dexpace.Sdk.Conformance.Tests.Catalogue;

/// <summary>P8a-14, widened by phase 10 (P10-7, plan 4.1): the view of all 61 items of appendix B, its drift test and the report's worst-of use of it.</summary>
[Trait("Category", "Unit")]
public sealed partial class AppendixBMapTests
{
    [GeneratedRegex(@"\b(?:SEAM|HTTP|IO|BODY|CTX|PIPE|RECOV|RETRY|REDIR|AUTH|PAGE|SSE|SERDE|OBS|CFG|TRANSPORT|ASYNC|XCUT|NFR)-\d+\b", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    private static string[] Lines => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "spec", "appendix-b.md"));

    /// <summary>The checklist bullets of one section, each as the requirement IDs it cites, keyed <c>B.&lt;section&gt;.&lt;n&gt;</c>.</summary>
    private static Dictionary<string, string[]> ParseSection(string section, string? next)
    {
        var lines = Lines;
        var start = Array.FindIndex(lines, line => line.StartsWith($"### {section} ", StringComparison.Ordinal));
        var end = next is null ? lines.Length : Array.FindIndex(lines, start + 1, line => line.StartsWith($"### {next} ", StringComparison.Ordinal));
        Assert.True(start >= 0 && end > start, $"sections {section} and {next} were not found in appendix B");

        var items = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var bullet in lines[start..end].Where(line => line.StartsWith("- [ ]", StringComparison.Ordinal)))
        {
            items[$"{section}.{items.Count + 1}"] = [.. IdPattern().Matches(bullet).Select(match => match.Value)];
        }

        return items;
    }

    private static Dictionary<string, string[]> ParseAll()
    {
        var all = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var section = 1; section <= 9; section++)
        {
            foreach (var (key, ids) in ParseSection($"B.{section}", section == 9 ? null : $"B.{section + 1}"))
            {
                all[key] = ids;
            }
        }

        return all;
    }

    [Fact]
    public void The_map_equals_the_checklist_items_of_appendix_B()
    {
        var parsed = ParseAll();

        Assert.Equal(61, parsed.Count);
        Assert.Equal([10, 6, 7, 8, 6, 5, 6, 6, 7], Enumerable.Range(1, 9).Select(section => parsed.Keys.Count(key => key.StartsWith($"B.{section}.", StringComparison.Ordinal))));
        Assert.Equal(parsed.Keys.Order(StringComparer.Ordinal), AppendixBMap.Items.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, ids) in parsed)
        {
            // Compared as sets: B.1.6 prints PAGE-20 before PAGE-19 (V8), and the map keeps the printed order.
            Assert.Equal(ids.Order(StringComparer.Ordinal), AppendixBMap.Items[key].Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void Every_id_of_the_map_exists_in_the_catalogue_and_none_is_mapped_twice()
    {
        var all = AppendixBMap.Items.Values.SelectMany(ids => ids).ToArray();

        Assert.All(all, id => Assert.True(RequirementIndex.Contains(id), id));
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(276, all.Length);
        Assert.Equal(61, AppendixBMap.Items.Count);
    }

    [Fact]
    public void Appendix_B_is_silent_on_PAGE_4_and_PAGE_5()
    {
        // A pin (design section 11 item 9, V8): the map states the silence; it does not invent coverage.
        var all = AppendixBMap.Items.Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("PAGE-4", all);
        Assert.DoesNotContain("PAGE-5", all);
        Assert.Contains("PAGE-3", all);
        Assert.Contains("PAGE-6", all);
    }

    [Fact]
    public void The_drift_check_can_fail()
    {
        // Negative control: a map that disagreed with the appendix on one ID must not compare equal.
        var parsed = ParseSection("B.6", "B.7");
        var skewed = AppendixBMap.Items["B.6.2"].Where(id => id != "TRANSPORT-5").ToArray();

        Assert.NotEqual(parsed["B.6.2"], skewed);
    }

    [Fact]
    public void An_item_is_as_bad_as_the_worst_requirement_it_covers()
    {
        var report = ConformanceReport.Create(
            "subject",
            [
                Result(Make("transport-3.a", ["TRANSPORT-3"]), ConformanceStatus.Passed),
                Result(Make("transport-8.a", ["TRANSPORT-8"]), ConformanceStatus.Failed),
            ]);

        Assert.Equal(ConformanceStatus.Failed, report.ByAppendixBItem["B.6.2"]);
        Assert.False(report.ByAppendixBItem.ContainsKey("B.6.1"));
    }

    [Fact]
    public void A_vacuous_requirement_among_passes_leaves_the_item_vacuous()
    {
        var report = ConformanceReport.Create(
            "subject",
            [
                Result(Make("async-21.a", ["ASYNC-21"]), ConformanceStatus.Vacuous),
                Result(Make("async-18.a", ["ASYNC-18"]), ConformanceStatus.Passed),
                Result(Make("async-22.a", ["ASYNC-22"]), ConformanceStatus.Passed),
            ]);

        Assert.Equal(ConformanceStatus.Vacuous, report.ByAppendixBItem["B.7.6"]);
    }

    [Fact]
    public void Rendering_includes_the_appendix_B_table()
    {
        var text = ConformanceReport.Create("subject", [Result(Make("transport-3.a", ["TRANSPORT-3"]), ConformanceStatus.Passed)]).ToString();

        Assert.Contains("Appendix B items", text, StringComparison.Ordinal);
        Assert.Contains("B.6.2", text, StringComparison.Ordinal);
    }
}
