// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json.Nodes;
using Xunit;

namespace KnowledgeHarvest.Tests;

public sealed class CollectTests
{
    private const string Output = """
        ## Entries
        - type: rule
          topic: Retry-Budget
          statement: Retry budgets are enforced per request rather than per attempt,
            counted across every attempt.
          evidence: {PATH}:2-3
          confidence: high
        - type: constraint
          topic: retry-and-resilience
          statement: A response body can be consumed only once.
          evidence: {PATH}:4
          confidence: medium
        - type: rule
          topic: retry-and-resilience
          statement: This evidence runs past the end of the file.
          evidence: {PATH}:3-99
          confidence: high
        - type: opinion
          topic: retry-and-resilience
          statement: This type does not exist.
          evidence: {PATH}:1-1
          confidence: high
        - type: rule
          topic: retry-and-resilience
          statement: This cites a file nobody assigned.
          evidence: docs/elsewhere.md:1-2
          confidence: high

        ## Open Questions
        - none
        """;

    private static (Manifest Manifest, string Outputs, string Path) Fixture(TempTree tree)
    {
        var path = tree.Write("docs/a.md", "## A\nline two\nline three\nline four\n");
        var chunk = new Chunk("c01", "haiku", "design", 40, [new ChunkFile(path, null, 40, new string('d', 64))]);
        var manifest = new Manifest("now", [new ManifestRoot(tree.Combine("docs"), "design")], [chunk], new ManifestTotals(1, 40, 1, 0));
        tree.Write("outputs/c01.md", Output.Replace("{PATH}", path, StringComparison.Ordinal));
        return (manifest, tree.Combine("outputs"), path);
    }

    [Fact]
    public void Entries_are_parsed_with_continuation_lines_joined()
    {
        var parsed = Collect.ParseOutput(Output.Replace("{PATH}", "p", StringComparison.Ordinal));
        Assert.Equal(5, parsed.Count);
        Assert.Equal("retry-budget", parsed[0].Topic);
        Assert.Equal("Retry budgets are enforced per request rather than per attempt, counted across every attempt.", parsed[0].Statement);
        Assert.Equal("p:4", parsed[1].Evidence);
    }

    [Fact]
    public void Only_entries_with_evidence_that_resolves_are_kept_and_they_carry_role_and_whole_file_sha()
    {
        using var tree = new TempTree();
        var (manifest, outputs, path) = Fixture(tree);
        var (entries, report) = Collect.Run(manifest, outputs, new Dictionary<string, string> { ["retry-budget"] = "retry-and-resilience" }, [], "2026-10-02");

        Assert.Equal((5, 2, 3), (report.Parsed, report.Kept, report.Dropped.Count));
        Assert.Equal(2, report.Topics["retry-and-resilience"]);
        var decisions = entries["decisions"]!.AsArray();
        var first = decisions.Select(d => d!["entry"]!).Single(e => e["statement"]!.GetValue<string>().StartsWith("Retry budgets", StringComparison.Ordinal));
        Assert.Equal("design", first["role"]!.GetValue<string>());
        Assert.Equal(new string('d', 64), first["sha256"]!.GetValue<string>());
        Assert.Equal($"{path}:2-3", first["evidence"]!.GetValue<string>());
        Assert.Equal($"{path}:4-4", decisions.Select(d => d!["entry"]!["evidence"]!.GetValue<string>()).Single(e => e.EndsWith(":4-4", StringComparison.Ordinal)).ToString());
    }

    [Fact]
    public void An_extra_decision_is_appended_and_a_duplicate_keeps_the_more_specific_evidence()
    {
        using var tree = new TempTree();
        var (manifest, outputs, path) = Fixture(tree);
        tree.Write("outputs/c02.md", $"## Entries\n- type: constraint\n  topic: retry-and-resilience\n  statement: A response body can be consumed only once\n  evidence: {path}:4-4\n  confidence: high\n");
        var conflict = new JsonObject { ["action"] = "conflict", ["topic"] = "x", ["title"] = "t", ["text"] = "x", ["sources"] = new JsonArray() };
        var (entries, report) = Collect.Run(manifest, outputs, new Dictionary<string, string>(), [conflict], "2026-10-02");
        Assert.Equal(1, report.Duplicates);
        Assert.Equal("conflict", entries["decisions"]!.AsArray()[^1]!["action"]!.GetValue<string>());
    }

    [Fact]
    public void Evidence_is_checked_against_the_assigned_line_range()
    {
        var files = new Dictionary<string, List<ChunkFile>> { ["a.md"] = [new ChunkFile("a.md", [10, 20], 1, "s")] };
        var lines = new Dictionary<string, int> { ["a.md"] = 100 };
        Assert.NotNull(Collect.ResolveEvidence("a.md:12-15", files, lines, out _));
        Assert.Null(Collect.ResolveEvidence("a.md:5-15", files, lines, out var problem));
        Assert.Contains("outside the assigned lines", problem, StringComparison.Ordinal);
    }
}
