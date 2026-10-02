// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace KnowledgeHarvest.Tests;

public sealed class ScanTests
{
    private static Stats StatsFor(TempTree tree, string role = "spec", params string[] exclude) =>
        Scan.BuildStats([(tree.Combine("docs"), role)], exclude);

    private static Routing RoutingFor(Stats stats, long budget = 100, long threshold = 50, bool split = true) =>
        new([.. stats.Roots.Select(r => new RootRouting(r.Path, budget, threshold, split))]);

    [Fact]
    public void Stats_records_size_hash_and_sections()
    {
        using var tree = new TempTree();
        tree.Write("docs/a.md", "## One\nbody\n");
        var file = Assert.Single(StatsFor(tree).Files);
        Assert.Equal("spec", file.Role);
        Assert.Equal("## One\nbody\n".Length, file.Bytes);
        Assert.Equal(64, file.Sha256.Length);
        Assert.Equal("One", file.Sections[0].Heading);
    }

    [Fact]
    public void Stats_skips_vendor_directories_lock_files_and_binaries()
    {
        using var tree = new TempTree();
        tree.Write("docs/keep.md", "keep\n");
        tree.Write("docs/node_modules/skip.md", "skip\n");
        tree.Write("docs/package-lock.json", "{}\n");
        File.WriteAllBytes(tree.Combine("docs/blob.md"), "text\0more"u8.ToArray());
        Assert.Equal(["keep.md"], StatsFor(tree).Files.Select(f => Path.GetFileName(f.Path)));
    }

    [Fact]
    public void Stats_applies_exclude_globs_to_the_file_name()
    {
        using var tree = new TempTree();
        tree.Write("docs/01-intro.md", "a\n");
        tree.Write("docs/appendix-c-index.md", "b\n");
        Assert.Equal(["01-intro.md"], StatsFor(tree, "spec", "appendix-c-*").Files.Select(f => Path.GetFileName(f.Path)));
    }

    [Theory]
    [InlineData("appendix-c-*", "appendix-c-x.md", true)]
    [InlineData("*.md", "a/b.md", true)]
    [InlineData("a?.md", "ab.md", true)]
    [InlineData("a?.md", "abc.md", false)]
    [InlineData("[ab].md", "b.md", true)]
    [InlineData("[!ab].md", "a.md", false)]
    public void Globs_follow_fnmatch(string pattern, string text, bool expected) =>
        Assert.Equal(expected, Scan.GlobMatches(pattern, text));

    [Fact]
    public void Stats_aggregates_median_and_max()
    {
        using var tree = new TempTree();
        tree.Write("docs/a.md", new string('x', 10));
        tree.Write("docs/b.md", new string('x', 20));
        tree.Write("docs/c.md", new string('x', 60));
        var root = Assert.Single(StatsFor(tree).Roots);
        Assert.Equal(3, root.Files);
        Assert.Equal(90, root.Bytes);
        Assert.Equal(20, root.MedianFileBytes);
        Assert.Equal(60, root.MaxFileBytes);
    }

    [Fact]
    public void A_missing_path_is_an_error()
    {
        using var tree = new TempTree();
        Assert.Throws<Scan.ScanException>(() => Scan.BuildStats([(tree.Combine("nope"), "spec")], []));
    }

    [Fact]
    public void Multiple_roots_keep_their_own_roles()
    {
        using var tree = new TempTree();
        tree.Write("spec/a.md", "a\n");
        tree.Write("style/b.md", "b\n");
        var stats = Scan.BuildStats([(tree.Combine("spec"), "spec"), (tree.Combine("style"), "styleguide")], []);
        Assert.Equal(["spec", "styleguide"], stats.Files.Select(f => f.Role).Order());
    }

    [Fact]
    public void Pack_fills_to_budget_then_starts_a_new_chunk()
    {
        using var tree = new TempTree();
        tree.Write("docs/a.md", new string('x', 60));
        tree.Write("docs/b.md", new string('y', 60));
        var stats = StatsFor(tree);
        Assert.Equal(2, Scan.Pack(stats, RoutingFor(stats)).Totals.Chunks);
    }

    [Fact]
    public void Pack_keeps_files_together_under_budget()
    {
        using var tree = new TempTree();
        tree.Write("docs/a.md", new string('x', 30));
        tree.Write("docs/b.md", new string('y', 30));
        var stats = StatsFor(tree);
        Assert.Equal(2, Assert.Single(Scan.Pack(stats, RoutingFor(stats)).Chunks).Files.Count);
    }

    [Fact]
    public void The_tier_threshold_is_inclusive_for_sonnet()
    {
        using var tree = new TempTree();
        tree.Write("docs/a.md", new string('x', 50));
        var stats = StatsFor(tree);
        Assert.Equal("sonnet", Scan.Pack(stats, RoutingFor(stats, threshold: 50)).Chunks[0].Tier);
        Assert.Equal("haiku", Scan.Pack(stats, RoutingFor(stats, threshold: 51)).Chunks[0].Tier);
    }

    [Fact]
    public void An_oversized_file_splits_at_heading_boundaries_and_carries_the_whole_file_hash()
    {
        using var tree = new TempTree();
        tree.Write("docs/big.md", "## A\n" + new string('x', 80) + "\n## B\n" + new string('y', 80) + "\n");
        var stats = StatsFor(tree);
        var manifest = Scan.Pack(stats, RoutingFor(stats));
        Assert.Equal(2, manifest.Totals.Chunks);
        Assert.Equal([[1, 2], [3, 4]], manifest.Chunks.Select(c => c.Files[0].Lines!));
        Assert.Equal([stats.Files[0].Sha256], manifest.Chunks.Select(c => c.Files[0].Sha256).Distinct());
    }

    [Fact]
    public void A_single_oversized_section_is_never_split()
    {
        using var tree = new TempTree();
        tree.Write("docs/big.md", "## Huge\n" + new string('x', 500) + "\n");
        var stats = StatsFor(tree);
        var chunk = Assert.Single(Scan.Pack(stats, RoutingFor(stats)).Chunks);
        Assert.True(chunk.Bytes > 100);
    }

    [Fact]
    public void Split_false_and_non_markdown_leave_an_oversized_file_whole()
    {
        using var tree = new TempTree();
        tree.Write("docs/big.md", "## A\n" + new string('x', 80) + "\n## B\n" + new string('y', 80) + "\n");
        tree.Write("docs/big.txt", new string('x', 500));
        var stats = StatsFor(tree);
        var manifest = Scan.Pack(stats, RoutingFor(stats, split: false));
        Assert.Equal(2, manifest.Totals.Chunks);
        Assert.All(manifest.Chunks, c => Assert.Null(c.Files[0].Lines));
    }

    [Fact]
    public void Pack_rejects_a_routing_missing_a_root()
    {
        using var tree = new TempTree();
        tree.Write("docs/a.md", "x");
        var exception = Assert.Throws<Scan.ScanException>(() => Scan.Pack(StatsFor(tree), new Routing([])));
        Assert.Contains("missing these roots", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pack_is_deterministic()
    {
        using var tree = new TempTree();
        tree.Write("docs/a.md", new string('x', 30));
        tree.Write("docs/b.md", new string('y', 30));
        var stats = StatsFor(tree);
        Assert.Equal(
            Scan.Pack(stats, RoutingFor(stats)).Chunks.Select(c => (c.Id, c.Tier, c.Bytes)),
            Scan.Pack(stats, RoutingFor(stats)).Chunks.Select(c => (c.Id, c.Tier, c.Bytes)));
    }

    [Fact]
    public void The_command_line_round_trips_stats_and_pack_through_json()
    {
        using var tree = new TempTree();
        tree.Write("docs/a.md", "## One\nbody\n");
        var stats = tree.Combine("stats.json");
        var routing = tree.Write("routing.json", $$"""{"roots":[{"path":"{{tree.Combine("docs").Replace("\\", "\\\\", StringComparison.Ordinal)}}","budget":100,"tier_threshold":50}]}""");
        var manifest = tree.Combine("manifest.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(0, Program.Run(["stats", "--path", tree.Combine("docs"), "--as", "spec", "-o", stats], output, error));
        Assert.Equal(0, Program.Run(["pack", "--stats", stats, "--routing", routing, "-o", manifest], output, error));
        Assert.Contains("packed 1 chunks (1 haiku, 0 sonnet)", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Stats_exits_two_when_nothing_is_eligible()
    {
        using var tree = new TempTree();
        tree.Write("docs/only.md", "a\n");
        using var error = new StringWriter();
        Assert.Equal(2, Program.Run(["stats", "--path", tree.Combine("docs"), "--as", "spec", "--exclude", "*.md", "-o", tree.Combine("o.json")], TextWriter.Null, error));
        Assert.Contains("no eligible files", error.ToString(), StringComparison.Ordinal);
    }
}
