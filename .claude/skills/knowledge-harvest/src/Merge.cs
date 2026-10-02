// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KnowledgeHarvest;

/// <summary>A source row in <c>SOURCES.md</c>.</summary>
internal sealed record SourceRecord(string Role, string Sha256, string Date);

/// <summary>
/// Applies the researcher's decisions to a persistent corpus. Makes no semantic judgment: whether two
/// statements contradict is decided by the researcher and recorded as an action, never here. Applying is
/// deterministic and idempotent.
/// </summary>
internal static class Merge
{
    /// <summary>Exit code: applied cleanly.</summary>
    public const int Applied = 0;

    /// <summary>Exit code: bad input.</summary>
    public const int BadInput = 2;

    /// <summary>Exit code: applied, but the corpus holds unresolved conflicts.</summary>
    public const int Conflicts = 3;

    /// <summary>Exit code: refused, the corpus has uncommitted git changes and force was not given.</summary>
    public const int Dirty = 4;

    /// <summary>Applies one decision to a topic document.</summary>
    public static void Apply(TopicDocument document, JsonNode decision, string date)
    {
        var action = Str(decision, "action");
        switch (action)
        {
            case "new" or "update":
                Upsert(document, Obj(decision, "entry"));
                break;
            case "supersede":
                Supersede(document, decision, date);
                break;
            case "conflict":
                Conflict(document, decision, date);
                break;
            default:
                throw new FormatException($"unknown action: {action}");
        }
    }

    private static void Upsert(TopicDocument document, JsonNode entry) =>
        document.Upsert(Str(entry, "type"), ToEntry(entry));

    private static TopicEntry ToEntry(JsonNode entry) => new(
        Str(entry, "statement"),
        Opt(entry, "role") ?? "unspecified",
        Str(entry, "evidence"),
        Opt(entry, "confidence") ?? "medium",
        Opt(entry, "sha256") ?? string.Empty);

    private static void Supersede(TopicDocument document, JsonNode decision, string date)
    {
        var replaced = Str(decision, "replaces");
        var entry = Obj(decision, "entry");
        var removed = document.Remove(replaced);
        var record = new SupersededRecord(
            replaced,
            Opt(decision, "date") ?? date,
            Opt(decision, "old_sha256") ?? removed?.Sha256 ?? string.Empty,
            Opt(entry, "sha256") ?? string.Empty);
        var key = TopicDocument.Normalize(replaced);
        if (!document.Superseded.Any(s => TopicDocument.Normalize(s.Statement) == key))
        {
            document.Superseded.Add(record);
        }

        Upsert(document, entry);
    }

    private static void Conflict(TopicDocument document, JsonNode decision, string date)
    {
        var sources = decision["sources"] is JsonArray array
            ? string.Join(" · ", array.Select(n => n?.GetValue<string>() ?? string.Empty))
            : Opt(decision, "sources") ?? string.Empty;
        var conflict = new TopicConflict(Str(decision, "title"), Str(decision, "text"), sources, Opt(decision, "date") ?? date, Opt(decision, "status") ?? TopicConflict.Unresolved);
        var text = TopicDocument.Normalize(conflict.Text);
        if (!document.Conflicts.Any(c => c.Title == conflict.Title && TopicDocument.Normalize(c.Text) == text))
        {
            document.Conflicts.Add(conflict);
        }
    }

    private static string Str(JsonNode node, string name) =>
        Opt(node, name) ?? throw new FormatException($"missing '{name}'");

    private static string? Opt(JsonNode node, string name) => node[name]?.GetValue<string>();

    private static JsonNode Obj(JsonNode node, string name) => node[name] ?? throw new FormatException($"missing '{name}'");

    /// <summary>Renders <c>INDEX.md</c> over every topic in the corpus.</summary>
    public static string RenderIndex(IReadOnlyDictionary<string, TopicDocument> topics, IReadOnlyDictionary<string, string> dates)
    {
        var output = new StringBuilder("# Knowledge Index\n\n");
        output.Append("| topic | file | entries | roles | conflicts | last harvest |\n| --- | --- | --- | --- | --- | --- |\n");
        foreach (var slug in topics.Keys.Order(StringComparer.Ordinal))
        {
            var document = topics[slug];
            var roles = document.Entries.Values.SelectMany(v => v).Select(e => e.Role).Distinct().Order(StringComparer.Ordinal).ToList();
            output.Append(CultureInfo.InvariantCulture,
                $"| {slug} | `{slug}.md` | {document.Count} | {(roles.Count == 0 ? "—" : string.Join(", ", roles))} | {document.Conflicts.Count} | {dates.GetValueOrDefault(slug, "—")} |\n");
        }

        return output.ToString();
    }

    /// <summary>The per-topic harvest dates an existing <c>INDEX.md</c> records.</summary>
    public static Dictionary<string, string> ParseIndexDates(string text)
    {
        var dates = new Dictionary<string, string>();
        foreach (var line in text.Split('\n'))
        {
            var cells = Cells(line);
            if (cells.Count == 6 && cells[0] is not ("topic" or "---"))
            {
                dates[cells[0]] = cells[5];
            }
        }

        return dates;
    }

    /// <summary>Renders <c>SOURCES.md</c>.</summary>
    public static string RenderSources(IReadOnlyDictionary<string, SourceRecord> sources)
    {
        var output = new StringBuilder("# Harvested Sources\n\n| source | role | sha256 | last harvest |\n| --- | --- | --- | --- |\n");
        foreach (var path in sources.Keys.Order(StringComparer.Ordinal))
        {
            var record = sources[path];
            output.Append(CultureInfo.InvariantCulture, $"| `{path}` | {record.Role} | `{TopicDocument.Short(record.Sha256)}` | {record.Date} |\n");
        }

        return output.ToString();
    }

    /// <summary>The source rows an existing <c>SOURCES.md</c> records.</summary>
    public static Dictionary<string, SourceRecord> ParseSources(string text)
    {
        var sources = new Dictionary<string, SourceRecord>();
        foreach (var line in text.Split('\n'))
        {
            var cells = Cells(line);
            if (cells.Count == 4 && cells[0] is not ("source" or "---"))
            {
                sources[cells[0].Trim('`')] = new SourceRecord(cells[1], cells[2].Trim('`'), cells[3]);
            }
        }

        return sources;
    }

    private static List<string> Cells(string line) =>
        line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();

    private static string ReadOrEmpty(string path) => File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;

    /// <summary>Writes through a temp file and a rename: a half-written corpus file is worse than none.</summary>
    public static void WriteAtomic(string path, string text)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        var temporary = $"{path}.tmp.{Environment.ProcessId}";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>True when <paramref name="corpus"/> is inside a git work tree and has uncommitted changes.</summary>
    public static bool GitIsDirty(string corpus)
    {
        var inside = Git(corpus, "rev-parse", "--is-inside-work-tree");
        if (inside is null || inside.Trim() != "true")
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(Git(corpus, "status", "--porcelain", "--", "."));
    }

    private static string? Git(string directory, params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("-C");
            start.ArgumentList.Add(directory);
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Runs a merge. See the exit-code constants for the outcomes.</summary>
    public static int Run(string entriesPath, string corpus, bool dryRun, bool force, TextWriter output, TextWriter error)
    {
        JsonNode payload;
        try
        {
            payload = JsonNode.Parse(File.ReadAllText(entriesPath, Encoding.UTF8)) ?? throw new FormatException("empty document");
            return Apply(payload, corpus, dryRun, force, output, error);
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidOperationException or JsonException)
        {
            error.WriteLine($"merge: malformed entries.json: {e.Message}");
            return BadInput;
        }
    }

    private static int Apply(JsonNode payload, string corpus, bool dryRun, bool force, TextWriter output, TextWriter error)
    {
        var decisions = payload["decisions"]?.AsArray() ?? [];
        var stamp = Opt(payload, "harvested");
        var date = stamp is { Length: >= 10 } ? stamp[..10] : DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (Directory.Exists(corpus) && !force && GitIsDirty(corpus))
        {
            error.WriteLine($"merge: {corpus} has uncommitted changes. Review or commit them first, or re-run with --force.");
            return Dirty;
        }

        var all = LoadAll(corpus);
        var originals = all.Keys.ToDictionary(slug => slug, slug => ReadOrEmpty(System.IO.Path.Combine(corpus, slug + ".md")));
        var touched = new HashSet<string>();
        var purged = Purge(payload, all, touched);
        ApplyDecisions(decisions, all, originals, touched, date);

        var planned = Plan(payload, corpus, date, touched, originals, all);
        var changed = planned.Where(p => p.Before != p.After).ToList();
        foreach (var (path, before, after) in changed)
        {
            if (dryRun)
            {
                WriteDiff(output, path, before, after ?? string.Empty);
            }
            else if (after is null)
            {
                File.Delete(path);
            }
            else
            {
                WriteAtomic(path, after);
            }
        }

        if (dryRun)
        {
            output.WriteLine($"merge --dry-run: {changed.Count} file(s) would change, nothing written");
        }

        if (purged > 0)
        {
            output.WriteLine($"merge: replaced {purged} existing entries from the re-harvested sources");
        }

        return Report(decisions, all, output, error);
    }

    /// <summary>
    /// A harvest of a source REPLACES what the corpus holds from it. Only adding and updating would leave an
    /// entry from an older revision of the file (reworded, or deleted from it) in place with its old sha, and
    /// the SOURCES.md row would say the file is current while that entry says otherwise.
    /// </summary>
    private static int Purge(JsonNode payload, Dictionary<string, TopicDocument> all, HashSet<string> touched)
    {
        var purged = 0;
        foreach (var source in payload["sources"]?.AsArray() ?? [])
        {
            var path = Str(source ?? throw new FormatException("a source is null"), "path");
            foreach (var (slug, document) in all)
            {
                var removed = document.RemoveFromSource(path);
                if (removed > 0)
                {
                    purged += removed;
                    touched.Add(slug);
                }
            }
        }

        return purged;
    }

    private static void ApplyDecisions(
        JsonArray decisions, Dictionary<string, TopicDocument> all, Dictionary<string, string> originals, HashSet<string> touched, string date)
    {
        foreach (var decision in decisions)
        {
            var node = decision ?? throw new FormatException("a decision is null");
            var slug = Str(node, "topic");
            if (!all.TryGetValue(slug, out var document))
            {
                all[slug] = document = new TopicDocument(slug);
                originals[slug] = string.Empty;
            }

            touched.Add(slug);
            Apply(document, node, date);
        }
    }

    /// <summary>A unified-style line diff (changed lines only, with their line numbers); the dry run's report.</summary>
    public static void WriteDiff(TextWriter output, string path, string before, string after)
    {
        output.WriteLine($"--- a/{path}\n+++ b/{path}");
        var old = before.Length == 0 ? [] : before.Split('\n');
        var current = after.Length == 0 ? [] : after.Split('\n');
        if ((long)old.Length * current.Length > 4_000_000)
        {
            output.WriteLine($"@@ file replaced: {old.Length} -> {current.Length} lines @@");
            return;
        }

        var lcs = new int[old.Length + 1, current.Length + 1];
        for (var i = old.Length - 1; i >= 0; i--)
        {
            for (var j = current.Length - 1; j >= 0; j--)
            {
                lcs[i, j] = old[i] == current[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        int a = 0, b = 0;
        while (a < old.Length || b < current.Length)
        {
            if (a < old.Length && b < current.Length && old[a] == current[b])
            {
                a++;
                b++;
            }
            else if (b < current.Length && (a == old.Length || lcs[a, b + 1] >= lcs[a + 1, b]))
            {
                output.WriteLine($"@@ +{b + 1} @@ +{current[b]}");
                b++;
            }
            else
            {
                output.WriteLine($"@@ -{a + 1} @@ -{old[a]}");
                a++;
            }
        }
    }

    /// <summary>Every topic file in the corpus, parsed, so INDEX.md and the re-harvest purge see the whole corpus.</summary>
    private static Dictionary<string, TopicDocument> LoadAll(string corpus)
    {
        var all = new Dictionary<string, TopicDocument>();
        if (!Directory.Exists(corpus))
        {
            return all;
        }

        foreach (var file in Directory.GetFiles(corpus, "*.md").Order(StringComparer.Ordinal))
        {
            var name = System.IO.Path.GetFileName(file);
            if (name is not ("INDEX.md" or "SOURCES.md"))
            {
                var slug = name[..^3];
                all[slug] = TopicDocument.Parse(ReadOrEmpty(file), slug);
            }
        }

        return all;
    }

    private static List<(string Path, string Before, string? After)> Plan(
        JsonNode payload,
        string corpus,
        string date,
        HashSet<string> touched,
        Dictionary<string, string> originals,
        Dictionary<string, TopicDocument> all)
    {
        var indexPath = System.IO.Path.Combine(corpus, "INDEX.md");
        var sourcesPath = System.IO.Path.Combine(corpus, "SOURCES.md");
        var dates = ParseIndexDates(ReadOrEmpty(indexPath));
        var planned = new List<(string Path, string Before, string? After)>();
        foreach (var slug in touched.Order(StringComparer.Ordinal))
        {
            var document = all[slug];
            var path = System.IO.Path.Combine(corpus, slug + ".md");
            if (document.Count == 0 && document.Conflicts.Count == 0 && document.Superseded.Count == 0)
            {
                // Every entry came from a source the harvest replaced and none came back: the topic is gone.
                all.Remove(slug);
                dates.Remove(slug);
                planned.Add((path, originals[slug], null));
                continue;
            }

            dates[slug] = date;
            planned.Add((path, originals[slug], document.Render()));
        }

        var sources = ParseSources(ReadOrEmpty(sourcesPath));
        foreach (var source in payload["sources"]?.AsArray() ?? [])
        {
            sources[Str(source!, "path")] = new SourceRecord(Opt(source!, "role") ?? "unspecified", Opt(source!, "sha256") ?? string.Empty, date);
        }

        planned.Add((indexPath, ReadOrEmpty(indexPath), RenderIndex(all, dates)));
        if (sources.Count > 0)
        {
            planned.Add((sourcesPath, ReadOrEmpty(sourcesPath), RenderSources(sources)));
        }

        return planned;
    }

    private static int Report(JsonArray decisions, Dictionary<string, TopicDocument> all, TextWriter output, TextWriter error)
    {
        int Count(string action) => decisions.Count(d => Opt(d!, "action") == action);
        output.WriteLine($"merge: {Count("new")} new, {Count("update")} updated, {Count("supersede")} superseded, {Count("conflict")} conflicts recorded");

        var unresolved = all.SelectMany(t => t.Value.Conflicts.Where(c => c.Status == TopicConflict.Unresolved).Select(c => (t.Key, Conflict: c))).ToList();
        if (unresolved.Count == 0)
        {
            return Applied;
        }

        error.WriteLine("\nunresolved conflicts:");
        foreach (var (slug, conflict) in unresolved)
        {
            error.WriteLine($"  {slug}: {conflict.Title} — {conflict.Text}");
        }

        return Conflicts;
    }
}
