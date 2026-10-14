// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Dexpace.Sdk.Conformance.Tests.Gates;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Requirements;

/// <summary>
/// The appendix-B gate (phase 10, P10-19, plan 4.2): <c>APPENDIX_B.md</c> maps every one of the 61 items of the specification's checklist to its evidence, and
/// five rules keep it honest: (1) 61 rows in the specification's section counts; (2) each row's ID set is the set its item cites; (3) every status is one of the
/// four and every path and assertion reference it names exists; (4) a <c>suite</c> row names a real kit assertion that cites one of its IDs; (5) the kit's
/// <c>AppendixBMap</c> equals the map's ID column. Each rule has a negative control built from the parsed map with one defect.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class AppendixBCoverageTests
{
    private static readonly string[] s_statuses = ["suite", "by reference", "restated", "scoped out"];
    private static readonly int[] s_sectionCounts = [10, 6, 7, 8, 6, 5, 6, 6, 7];
    private static readonly string[] s_suites = ["transport", "invariant", "codec", "packaging"];

    [GeneratedRegex(@"\b(?:SEAM|HTTP|IO|BODY|CTX|PIPE|RECOV|RETRY|REDIR|AUTH|PAGE|SSE|SERDE|OBS|CFG|TRANSPORT|ASYNC|XCUT|NFR)-\d+\b", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    private sealed record Row(string Section, string Item, string[] Ids, string[] Evidence, string Status, string Notes);

    private static string MapText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "spec", "APPENDIX_B.md"));

    private static string[] SpecLines => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "spec", "appendix-b.md"));

    private static List<Row> ParseMap(string text) =>
        [.. text.Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Where(static line => line.StartsWith("| B.", StringComparison.Ordinal))
            .Select(static line => line.Trim('|').Split('|').Select(static cell => cell.Trim()).ToArray())
            .Select(static cells => new Row(
                cells[0],
                cells[1],
                [.. cells[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                [.. cells[3].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                cells[4],
                cells[5]))];

    // Each checklist bullet of the specification, keyed B.<section>.<n>, as the IDs it cites.
    private static Dictionary<string, string[]> ParseSpecification()
    {
        var items = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string? section = null;
        var count = 0;
        foreach (var line in SpecLines)
        {
            var header = Regex.Match(line, @"^### (B\.\d+) ", RegexOptions.CultureInvariant);
            if (header.Success)
            {
                section = header.Groups[1].Value;
                count = 0;
            }
            else if (section is not null && line.StartsWith("- [ ]", StringComparison.Ordinal))
            {
                items[$"{section}.{++count}"] = [.. IdPattern().Matches(line).Select(static match => match.Value)];
            }
        }

        return items;
    }

    private static IReadOnlyList<ConformanceAssertion> Catalogue(string suite) => suite switch
    {
        "transport" => TransportSuite.Assertions,
        "invariant" => InvariantSuite.Assertions,
        "codec" => CodecSuite.Assertions,
        "packaging" => PackagingSuite.Assertions,
        _ => [],
    };

    // An evidence item that names an assertion: "<suite>:<name>", the name a prefix when it ends in "*". Null for a path.
    private static List<ConformanceAssertion>? Resolve(string evidence)
    {
        var colon = evidence.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || !s_suites.Contains(evidence[..colon], StringComparer.Ordinal))
        {
            return null;
        }

        var name = evidence[(colon + 1)..];
        var catalogue = Catalogue(evidence[..colon]);
        return name.EndsWith('*')
            ? [.. catalogue.Where(a => a.Name.StartsWith(name[..^1], StringComparison.Ordinal))]
            : [.. catalogue.Where(a => string.Equals(a.Name, name, StringComparison.Ordinal))];
    }

    private static List<string> Problems(IReadOnlyList<Row> rows)
    {
        var specification = ParseSpecification();
        var problems = SectionProblems(rows);
        foreach (var row in rows)
        {
            problems.AddRange(RowProblems(row, specification));
        }

        problems.AddRange(MapProblems(rows));
        return problems;
    }

    // Rule 1: 61 rows, with the specification's per-section counts.
    private static List<string> SectionProblems(IReadOnlyList<Row> rows)
    {
        var problems = new List<string>();
        if (rows.Count != 61)
        {
            problems.Add($"rule 1: {rows.Count} rows, expected 61");
        }

        for (var section = 1; section <= 9; section++)
        {
            var have = rows.Count(row => row.Section == $"B.{section}");
            if (have != s_sectionCounts[section - 1])
            {
                problems.Add($"rule 1: section B.{section} has {have} rows, expected {s_sectionCounts[section - 1]}");
            }
        }

        return problems;
    }

    // Rules 2 to 4, for one row.
    private static List<string> RowProblems(Row row, Dictionary<string, string[]> specification)
    {
        var problems = new List<string>();

        // Rule 2: the row's ID set is the set its item cites.
        if (!specification.TryGetValue(row.Item, out var cited))
        {
            problems.Add($"rule 2: {row.Item} is not an item of appendix B");
        }
        else if (!row.Ids.ToHashSet(StringComparer.Ordinal).SetEquals(cited))
        {
            problems.Add($"rule 2: {row.Item} lists {string.Join(", ", row.Ids.Order(StringComparer.Ordinal))} but the item cites {string.Join(", ", cited.Order(StringComparer.Ordinal))}");
        }

        // Rule 3: one of the four statuses, and every path and assertion reference exists.
        if (!s_statuses.Contains(row.Status, StringComparer.Ordinal))
        {
            problems.Add($"rule 3: {row.Item} has the status '{row.Status}'");
        }

        foreach (var evidence in row.Evidence)
        {
            var resolved = Resolve(evidence);
            if (resolved is null)
            {
                if (!File.Exists(RepositoryRoot.Of(evidence)))
                {
                    problems.Add($"rule 3: {row.Item} names the path '{evidence}', which does not exist");
                }
            }
            else if (resolved.Count == 0)
            {
                problems.Add($"rule 3: {row.Item} names the assertion '{evidence}', which no kit catalogue holds");
            }
        }

        // Rule 4: a suite row names a real assertion that cites one of its IDs.
        if (row.Status == "suite" && !row.Evidence.Any(evidence => Resolve(evidence) is { } found && found.Any(assertion => assertion.RequirementIds.Intersect(row.Ids, StringComparer.Ordinal).Any())))
        {
            problems.Add($"rule 4: the suite row {row.Item} names no assertion that cites one of its IDs");
        }

        return problems;
    }

    // Rule 5: the kit's own AppendixBMap is the map's ID column.
    private static List<string> MapProblems(IReadOnlyList<Row> rows)
    {
        var mapped = rows.ToDictionary(static row => row.Item, static row => row.Ids, StringComparer.Ordinal);
        if (!mapped.Keys.Order(StringComparer.Ordinal).SequenceEqual(AppendixBMap.Items.Keys.Order(StringComparer.Ordinal)))
        {
            return ["rule 5: AppendixBMap and the map name different items"];
        }

        return
        [
            .. AppendixBMap.Items
                .Where(item => !item.Value.ToHashSet(StringComparer.Ordinal).SetEquals(mapped[item.Key]))
                .Select(item => $"rule 5: AppendixBMap differs from the map at {item.Key}"),
        ];
    }

    [Fact]
    public void The_map_passes_all_five_rules()
    {
        var problems = Problems(ParseMap(MapText));

        Assert.Empty(problems);
    }

    [Fact]
    public void The_map_opens_with_the_two_limits()
    {
        var text = MapText;

        Assert.Contains("covers nine of the nineteen requirement prefixes", text, StringComparison.Ordinal);
        Assert.Contains("silent on `PAGE-4` and `PAGE-5`", text, StringComparison.Ordinal);
        Assert.Contains("proves a claim and a file, never the behaviour", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tally_is_17_8_36_0()
    {
        var rows = ParseMap(MapText);

        Assert.Equal(17, rows.Count(static row => row.Status == "suite"));
        Assert.Equal(8, rows.Count(static row => row.Status == "restated"));
        Assert.Equal(36, rows.Count(static row => row.Status == "by reference"));
        Assert.Equal(0, rows.Count(static row => row.Status == "scoped out"));
        Assert.Contains("17 `suite`, 8 `restated`, 36 `by reference`, 0 `scoped out` (61)", MapText, StringComparison.Ordinal);
        Assert.Equal(276, rows.SelectMany(static row => row.Ids).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Rule_1_can_fail_a_map_missing_a_row()
    {
        var rows = ParseMap(MapText);

        Assert.Contains(Problems([.. rows.Where(static row => row.Item != "B.4.2")]), static p => p.StartsWith("rule 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Rule_2_can_fail_a_row_with_one_id_too_many()
    {
        var rows = ParseMap(MapText).Select(static row => row.Item == "B.3.7" ? row with { Ids = [.. row.Ids, "SERDE-30"] } : row).ToList();

        Assert.Contains(Problems(rows), static p => p.StartsWith("rule 2: B.3.7", StringComparison.Ordinal));
    }

    [Fact]
    public void Rule_3_can_fail_a_dangling_path_a_missing_assertion_and_an_unknown_status()
    {
        var rows = ParseMap(MapText);

        var dangling = rows.Select(static row => row.Item == "B.2.3" ? row with { Evidence = ["tests/Dexpace.Sdk.Core.Tests/ServerSentEvents/NoSuchTests.cs"] } : row).ToList();
        var missing = rows.Select(static row => row.Item == "B.8.3" ? row with { Evidence = ["invariant:xcut-10.no-such-assertion"] } : row).ToList();
        var status = rows.Select(static row => row.Item == "B.2.3" ? row with { Status = "mostly" } : row).ToList();

        Assert.Contains(Problems(dangling), static p => p.Contains("does not exist", StringComparison.Ordinal));
        Assert.Contains(Problems(missing), static p => p.Contains("no kit catalogue holds", StringComparison.Ordinal));
        Assert.Contains(Problems(status), static p => p.Contains("the status 'mostly'", StringComparison.Ordinal));
    }

    [Fact]
    public void Rule_4_can_fail_a_suite_row_naming_an_assertion_that_cites_none_of_its_ids()
    {
        var rows = ParseMap(MapText).Select(static row => row.Item == "B.8.3" ? row with { Evidence = ["invariant:xcut-1.cancel-is-terminal"] } : row).ToList();

        Assert.Contains(Problems(rows), static p => p.StartsWith("rule 4: the suite row B.8.3", StringComparison.Ordinal));
    }

    [Fact]
    public void Rule_5_can_fail_when_the_map_and_AppendixBMap_disagree()
    {
        var rows = ParseMap(MapText).Select(static row => row.Item == "B.9.5" ? row with { Ids = ["NFR-10", "NFR-11"] } : row).ToList();

        Assert.Contains(Problems(rows), static p => p.StartsWith("rule 5", StringComparison.Ordinal));
    }

    [Fact]
    public void A_wildcard_matches_a_name_prefix_and_a_path_is_not_an_assertion()
    {
        Assert.NotEmpty(Resolve("invariant:xcut-1*")!);
        Assert.Empty(Resolve("invariant:zzz*")!);
        Assert.Null(Resolve("tests/Dexpace.Sdk.Core.Tests/Pagination/PageableTests.cs"));
        Assert.Null(Resolve("scripts/ci/coverage-gate.cs"));
    }
}
