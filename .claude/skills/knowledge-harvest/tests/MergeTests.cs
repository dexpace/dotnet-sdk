// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;

namespace KnowledgeHarvest.Tests;

public sealed class MergeTests
{
    private static readonly string s_sha = new('a', 64);

    private static JsonObject Entry(string statement, string type = "rule", string role = "spec", string evidence = "docs/a.md:1-5", string confidence = "high", string? sha = null) =>
        new()
        {
            ["type"] = type,
            ["statement"] = statement,
            ["role"] = role,
            ["evidence"] = evidence,
            ["confidence"] = confidence,
            ["sha256"] = sha ?? s_sha,
        };

    private static JsonObject New(string topic, JsonObject entry, string action = "new") =>
        new() { ["action"] = action, ["topic"] = topic, ["entry"] = entry };

    private static JsonObject Payload(string harvested, JsonArray? sources, params JsonNode[] decisions) =>
        new() { ["harvested"] = harvested, ["sources"] = sources ?? [], ["decisions"] = new JsonArray([.. decisions]) };

    private static int Apply(TempTree tree, JsonObject payload, out string corpus, bool dryRun = false, bool force = true, TextWriter? error = null, TextWriter? output = null)
    {
        var path = tree.Write("entries.json", payload.ToJsonString());
        corpus = tree.Combine("knowledge");
        return Merge.Run(path, corpus, dryRun, force, output ?? TextWriter.Null, error ?? TextWriter.Null);
    }

    private static string Topic(string corpus, string slug = "retry") => File.ReadAllText(Path.Combine(corpus, slug + ".md"));

    [Fact]
    public void Normalize_ignores_case_punctuation_and_spacing() =>
        Assert.Equal(TopicDocument.Normalize("Retries  are capped."), TopicDocument.Normalize("retries are capped"));

    [Fact]
    public void A_new_entry_lands_in_its_type_section_and_all_four_sections_exist()
    {
        using var tree = new TempTree();
        Assert.Equal(0, Apply(tree, Payload("2026-07-24T10:00:00Z", null, New("retry", Entry("Budgets are per request"))), out var corpus));
        var text = Topic(corpus);
        Assert.Contains("## Rules\n- Budgets are per request", text, StringComparison.Ordinal);
        Assert.Contains("spec · `docs/a.md:1-5` · high · sha:aaaaaaaaaaaa", text, StringComparison.Ordinal);
        foreach (var heading in new[] { "## Constraints", "## Conclusions", "## Reference", "## Conflicts", "## Superseded" })
        {
            Assert.Contains(heading, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_matching_statement_updates_in_place_without_duplicating()
    {
        using var tree = new TempTree();
        Apply(tree, Payload("2026-07-24", null, New("retry", Entry("Budgets are per request"))), out var corpus);
        Apply(tree, Payload("2026-07-24", null, New("retry", Entry("Budgets are per request.", evidence: "docs/a.md:9-12", confidence: "medium"), "update")), out _);
        var text = Topic(corpus);
        Assert.Equal(1, text.Split("Budgets are per request").Length - 1);
        Assert.Contains("docs/a.md:9-12", text, StringComparison.Ordinal);
        Assert.Contains("medium", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Supersede_moves_the_old_statement_and_records_the_sha_transition_once()
    {
        using var tree = new TempTree();
        Apply(tree, Payload("2026-07-24", null, New("retry", Entry("Budgets are per attempt"))), out var corpus);
        var supersede = new JsonObject
        {
            ["action"] = "supersede",
            ["topic"] = "retry",
            ["replaces"] = "Budgets are per attempt",
            ["entry"] = Entry("Budgets are per request", sha: new string('b', 64)),
        };
        Apply(tree, Payload("2026-07-24", null, supersede.DeepClone()), out _);
        Apply(tree, Payload("2026-07-24", null, supersede.DeepClone()), out _);
        var text = Topic(corpus);
        Assert.Contains("- Budgets are per request", text, StringComparison.Ordinal);
        Assert.Contains("~~Budgets are per attempt~~", text, StringComparison.Ordinal);
        Assert.Contains("sha aaaaaaaaaaaa… → bbbbbbbbbbbb…", text, StringComparison.Ordinal);
        Assert.DoesNotContain("- Budgets are per attempt\n", text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("~~Budgets are per attempt~~").Length - 1);
    }

    [Fact]
    public void A_conflict_is_recorded_once_and_exits_three()
    {
        using var tree = new TempTree();
        var conflict = new JsonObject
        {
            ["action"] = "conflict",
            ["topic"] = "retry",
            ["title"] = "spec vs styleguide",
            ["text"] = "the spec permits jitter; the styleguide forbids it",
            ["sources"] = new JsonArray("spec `a.md:88`", "styleguide `b.md:204`"),
        };
        Assert.Equal(3, Apply(tree, Payload("2026-07-24T10:00:00Z", null, conflict.DeepClone()), out var corpus));
        Assert.Equal(3, Apply(tree, Payload("2026-07-24T10:00:00Z", null, conflict.DeepClone()), out _));
        var text = Topic(corpus);
        Assert.Contains("- **spec vs styleguide** — the spec permits jitter; the styleguide forbids it", text, StringComparison.Ordinal);
        Assert.Contains("spec `a.md:88` · styleguide `b.md:204` · unresolved 2026-07-24", text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("spec vs styleguide").Length - 1);
    }

    [Fact]
    public void A_conformed_conflict_is_recorded_with_its_status_and_does_not_fail_the_merge()
    {
        using var tree = new TempTree();
        var conflict = new JsonObject
        {
            ["action"] = "conflict",
            ["topic"] = "retry",
            ["title"] = "net10.0 floor",
            ["text"] = "the port conforms",
            ["sources"] = new JsonArray("styleguide `a.md:1`", "design `b.md:2`"),
            ["status"] = "conformed",
        };
        Assert.Equal(0, Apply(tree, Payload("2026-10-02T00:00:00Z", null, conflict), out var corpus));
        var text = Topic(corpus);
        Assert.Contains("styleguide `a.md:1` · design `b.md:2` · conformed 2026-10-02", text, StringComparison.Ordinal);
        Assert.Equal("conformed", TopicDocument.Parse(text, "retry").Conflicts[0].Status);
    }

    [Fact]
    public void Applying_the_same_entries_twice_changes_nothing()
    {
        using var tree = new TempTree();
        var payload = Payload(
            "2026-07-24",
            [new JsonObject { ["path"] = "docs/a.md", ["role"] = "spec", ["sha256"] = s_sha }],
            New("retry", Entry("Budgets are per request")),
            New("retry", Entry("Timeouts default to 30 seconds", "reference")));
        Apply(tree, payload, out var corpus);
        var first = Topic(corpus);
        Apply(tree, payload, out _);
        Assert.Equal(first, Topic(corpus));
    }

    [Fact]
    public void Render_then_parse_round_trips()
    {
        using var tree = new TempTree();
        var conflict = new JsonObject
        {
            ["action"] = "conflict",
            ["topic"] = "retry",
            ["title"] = "spec vs design",
            ["text"] = "they disagree",
            ["sources"] = new JsonArray("spec `a.md:1`"),
        };
        Apply(tree, Payload("2026-07-24", null, New("retry", Entry("A rule with — punctuation")), New("retry", Entry("A constraint", "constraint", "styleguide")), conflict), out var corpus);
        var text = Topic(corpus);
        var parsed = TopicDocument.Parse(text, "retry");
        Assert.Equal(text, parsed.Render());
        Assert.Equal("A rule with — punctuation", parsed.Entries["rule"][0].Statement);
        Assert.Equal("styleguide", parsed.Entries["constraint"][0].Role);
        Assert.Equal("spec vs design", parsed.Conflicts[0].Title);
    }

    [Fact]
    public void The_index_lists_every_topic_and_an_untouched_topic_keeps_its_date()
    {
        using var tree = new TempTree();
        Apply(tree, Payload("2026-01-01T00:00:00Z", null, New("retry", Entry("R"))), out var corpus);
        Apply(tree, Payload("2026-07-24T00:00:00Z", null, New("errors", Entry("E"))), out _);
        var index = File.ReadAllText(Path.Combine(corpus, "INDEX.md"));
        Assert.Contains("| errors |", index, StringComparison.Ordinal);
        Assert.Contains("2026-01-01", index.Split('\n').Single(l => l.StartsWith("| retry ", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_sources_table_records_role_and_a_twelve_digit_hash()
    {
        using var tree = new TempTree();
        Apply(tree, Payload("2026-07-24", [new JsonObject { ["path"] = "docs/a.md", ["role"] = "spec", ["sha256"] = new string('c', 64) }], New("retry", Entry("R"))), out var corpus);
        var text = File.ReadAllText(Path.Combine(corpus, "SOURCES.md"));
        Assert.Contains("| `docs/a.md` | spec | `cccccccccccc` | 2026-07-24 |", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dry_run_writes_nothing()
    {
        using var tree = new TempTree();
        using var output = new StringWriter();
        Assert.Equal(0, Apply(tree, Payload("2026-07-24", null, New("retry", Entry("R"))), out var corpus, dryRun: true, output: output));
        Assert.False(Directory.Exists(corpus));
        Assert.Contains("nothing written", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_dirty_corpus_is_refused_without_force_and_accepted_with_it()
    {
        using var tree = new TempTree();
        Git(tree.Root, "init", "-q");
        tree.Write("knowledge/stray.md", "# stray\n");
        using var error = new StringWriter();
        var payload = Payload("2026-07-24", null, New("retry", Entry("R")));
        Assert.Equal(4, Apply(tree, payload, out _, force: false, error: error));
        Assert.Contains("uncommitted changes", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, Apply(tree, payload, out _, force: true));
    }

    [Theory]
    [InlineData("""{"decisions":[{"action":"invent","topic":"t"}]}""")]
    [InlineData("""{"decisions":[{"action":"new","topic":"t","entry":{"type":"opinion","statement":"x","evidence":"a:1-1"}}]}""")]
    [InlineData("""{"decisions":[{"action":"new","topic":"t"}]}""")]
    [InlineData("not json")]
    public void Malformed_entries_exit_two(string text)
    {
        using var tree = new TempTree();
        using var error = new StringWriter();
        var path = tree.Write("entries.json", text);
        Assert.Equal(2, Merge.Run(path, tree.Combine("k"), false, true, TextWriter.Null, error));
        Assert.Contains("malformed", error.ToString(), StringComparison.Ordinal);
    }

    private static void Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";
        using var process = Process.Start(start)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
