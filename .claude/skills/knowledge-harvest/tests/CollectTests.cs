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
        var sha = new string('d', 64);
        Chunk ChunkOf(string id) => new(id, "haiku", "design", 40, [new ChunkFile(path, null, 40, sha)]);
        var manifest = new Manifest("now", [new ManifestRoot(tree.Combine("docs"), "design")], [ChunkOf("c01"), ChunkOf("c02")], new ManifestTotals(2, 80, 2, 0));
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
        Assert.Contains(decisions, d => d!["entry"]!["evidence"]!.GetValue<string>() == $"{path}:4-4");
    }

    [Fact]
    public void A_duplicate_across_chunks_keeps_the_narrower_citation_and_an_extra_decision_is_appended()
    {
        using var tree = new TempTree();
        var (manifest, outputs, path) = Fixture(tree);
        tree.Write("outputs/c02.md", $"## Entries\n- type: constraint\n  topic: retry-and-resilience\n  statement: A response body can be consumed only once\n  evidence: {path}:3-4\n  confidence: high\n");
        var conflict = new JsonObject { ["action"] = "conflict", ["topic"] = "x", ["title"] = "t", ["text"] = "x", ["sources"] = new JsonArray() };
        var (entries, report) = Collect.Run(manifest, outputs, new Dictionary<string, string>(), [conflict], "2026-10-02");

        Assert.Equal(1, report.Duplicates);
        var decisions = entries["decisions"]!.AsArray();
        Assert.Equal("conflict", decisions[^1]!["action"]!.GetValue<string>());
        var once = decisions.Select(d => d!["entry"]).Where(e => e is not null && e["statement"]!.GetValue<string>().StartsWith("A response body", StringComparison.Ordinal)).ToList();
        Assert.Equal($"{path}:4-4", Assert.Single(once)!["evidence"]!.GetValue<string>());
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

    [Fact]
    public void A_line_number_too_large_for_an_int_is_a_problem_not_a_crash()
    {
        var files = new Dictionary<string, List<ChunkFile>> { ["a.md"] = [new ChunkFile("a.md", null, 1, "s")] };
        var lines = new Dictionary<string, int> { ["a.md"] = 100 };
        Assert.Null(Collect.ResolveEvidence("a.md:1-99999999999999999999", files, lines, out var problem));
        Assert.Contains("not a valid line", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_is_validated_against_its_own_chunk_not_every_chunk_of_the_file()
    {
        using var tree = new TempTree();
        var path = tree.Write("docs/big.md", string.Concat(Enumerable.Range(1, 40).Select(n => $"line {n}\n")));
        var sha = new string('e', 64);
        var first = new Chunk("c01", "sonnet", "design", 10, [new ChunkFile(path, [1, 20], 10, sha)]);
        var second = new Chunk("c02", "sonnet", "design", 10, [new ChunkFile(path, [21, 40], 10, sha)]);
        var manifest = new Manifest("now", [new ManifestRoot(tree.Combine("docs"), "design")], [first, second], new ManifestTotals(2, 20, 0, 2));
        tree.Write("outputs/c01.md", $"## Entries\n- type: rule\n  topic: t\n  statement: Line thirty is cited by the chunk that read lines one to twenty.\n  evidence: {path}:30-30\n  confidence: high\n");
        tree.Write("outputs/c02.md", $"## Entries\n- type: rule\n  topic: t\n  statement: Line thirty is cited by the chunk that read it.\n  evidence: {path}:30-30\n  confidence: high\n");

        var (_, report) = Collect.Run(manifest, tree.Combine("outputs"), new Dictionary<string, string>(), [], "2026-10-02");
        Assert.Equal(1, report.Kept);
        Assert.Contains("c01.md", Assert.Single(report.Dropped), StringComparison.Ordinal);
    }

    [Fact]
    public void An_output_file_that_is_not_a_chunk_id_is_dropped()
    {
        using var tree = new TempTree();
        var (manifest, outputs, path) = Fixture(tree);
        tree.Write("outputs/notes.md", $"## Entries\n- type: rule\n  topic: t\n  statement: Stray.\n  evidence: {path}:2-2\n  confidence: high\n");
        var (_, report) = Collect.Run(manifest, outputs, new Dictionary<string, string>(), [], "2026-10-02");
        Assert.Contains(report.Dropped, d => d.Contains("not a chunk id", StringComparison.Ordinal));
    }

    [Fact]
    public void Entries_are_ordered_by_line_number_not_by_citation_text()
    {
        using var tree = new TempTree();
        var path = tree.Write("docs/long.md", string.Concat(Enumerable.Range(1, 120).Select(n => $"line {n}\n")));
        var chunk = new Chunk("c01", "sonnet", "spec", 10, [new ChunkFile(path, null, 10, new string('f', 64))]);
        var manifest = new Manifest("now", [new ManifestRoot(tree.Combine("docs"), "spec")], [chunk], new ManifestTotals(1, 10, 0, 1));
        tree.Write("outputs/c01.md", $"## Entries\n- type: rule\n  topic: t\n  statement: Late.\n  evidence: {path}:102-102\n  confidence: high\n- type: rule\n  topic: t\n  statement: Early.\n  evidence: {path}:30-30\n  confidence: high\n");
        var (entries, _) = Collect.Run(manifest, tree.Combine("outputs"), new Dictionary<string, string>(), [], "2026-10-02");
        Assert.Equal(["Early.", "Late."], entries["decisions"]!.AsArray().Select(d => d!["entry"]!["statement"]!.GetValue<string>()));
    }
}
