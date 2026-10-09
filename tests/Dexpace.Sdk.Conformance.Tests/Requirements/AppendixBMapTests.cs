// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;
using static Dexpace.Sdk.Conformance.Tests.Support.TestAssertions;

namespace Dexpace.Sdk.Conformance.Tests.Catalogue;

/// <summary>P8a-14: the B.6 and B.7 view of appendix B, its drift test and the report's worst-of use of it.</summary>
[Trait("Category", "Unit")]
public sealed partial class AppendixBMapTests
{
    [GeneratedRegex(@"\b(?:TRANSPORT|ASYNC)-\d+\b", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    private static string[] Lines => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "spec", "appendix-b.md"));

    /// <summary>The checklist bullets of one section, each as the requirement IDs it cites, keyed <c>B.&lt;section&gt;.&lt;n&gt;</c>.</summary>
    private static Dictionary<string, string[]> ParseSection(string section, string next)
    {
        var lines = Lines;
        var start = Array.FindIndex(lines, line => line.StartsWith($"### {section} ", StringComparison.Ordinal));
        var end = Array.FindIndex(lines, start + 1, line => line.StartsWith($"### {next} ", StringComparison.Ordinal));
        Assert.True(start >= 0 && end > start, $"sections {section} and {next} were not found in appendix B");

        var items = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var bullet in lines[start..end].Where(line => line.StartsWith("- [ ]", StringComparison.Ordinal)))
        {
            items[$"{section}.{items.Count + 1}"] = [.. IdPattern().Matches(bullet).Select(match => match.Value)];
        }

        return items;
    }

    [Fact]
    public void The_map_equals_the_checklist_items_of_appendix_B_6_and_B_7()
    {
        var parsed = ParseSection("B.6", "B.7").Concat(ParseSection("B.7", "B.8")).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

        Assert.Equal(11, parsed.Count);
        Assert.Equal(5, parsed.Keys.Count(key => key.StartsWith("B.6.", StringComparison.Ordinal)));
        Assert.Equal(6, parsed.Keys.Count(key => key.StartsWith("B.7.", StringComparison.Ordinal)));
        Assert.Equal(parsed.Keys.Order(StringComparer.Ordinal), AppendixBMap.Items.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, ids) in parsed)
        {
            Assert.Equal(ids, AppendixBMap.Items[key]);
        }
    }

    [Fact]
    public void Every_id_of_the_map_exists_in_the_catalogue_and_none_is_mapped_twice()
    {
        var all = AppendixBMap.Items.Values.SelectMany(ids => ids).ToArray();

        Assert.All(all, id => Assert.True(RequirementIndex.Contains(id), id));
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(30 + 22, all.Length);
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
