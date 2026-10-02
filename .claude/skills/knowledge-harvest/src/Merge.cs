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
        File.WriteAllText(temporary, text, new UTF8Encoding(false));
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

        var touched = new Dictionary<string, TopicDocument>();
        var originals = new Dictionary<string, string>();
        foreach (var decision in decisions)
        {
            var slug = Str(decision!, "topic");
            if (!touched.TryGetValue(slug, out var document))
            {
                var original = ReadOrEmpty(System.IO.Path.Combine(corpus, slug + ".md"));
                originals[slug] = original;
                document = touched[slug] = original.Length > 0 ? TopicDocument.Parse(original, slug) : new TopicDocument(slug);
            }

            Apply(document, decision!, date);
        }

        var all = LoadAll(corpus, touched);
        var planned = Plan(payload, corpus, date, touched, originals, all);
        var changed = planned.Where(p => p.Before != p.After).ToList();
        foreach (var (path, before, after) in changed)
        {
            if (dryRun)
            {
                output.WriteLine($"would write {path} ({before.Split('\n').Length} -> {after.Split('\n').Length} lines)");
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

        return Report(decisions, all, output, error);
    }

    /// <summary>Every topic in the corpus: the touched ones, plus the rest reloaded so INDEX.md covers the whole corpus.</summary>
    private static Dictionary<string, TopicDocument> LoadAll(string corpus, Dictionary<string, TopicDocument> touched)
    {
        var all = new Dictionary<string, TopicDocument>(touched);
        if (!Directory.Exists(corpus))
        {
            return all;
        }

        foreach (var file in Directory.GetFiles(corpus, "*.md").Order(StringComparer.Ordinal))
        {
            var name = System.IO.Path.GetFileName(file);
            var slug = name[..^3];
            if (name is not ("INDEX.md" or "SOURCES.md") && !all.ContainsKey(slug))
            {
                all[slug] = TopicDocument.Parse(ReadOrEmpty(file), slug);
            }
        }

        return all;
    }

    private static List<(string Path, string Before, string After)> Plan(
        JsonNode payload,
        string corpus,
        string date,
        Dictionary<string, TopicDocument> touched,
        Dictionary<string, string> originals,
        Dictionary<string, TopicDocument> all)
    {
        var indexPath = System.IO.Path.Combine(corpus, "INDEX.md");
        var sourcesPath = System.IO.Path.Combine(corpus, "SOURCES.md");
        var dates = ParseIndexDates(ReadOrEmpty(indexPath));
        foreach (var slug in touched.Keys)
        {
            dates[slug] = date;
        }

        var sources = ParseSources(ReadOrEmpty(sourcesPath));
        foreach (var source in payload["sources"]?.AsArray() ?? [])
        {
            sources[Str(source!, "path")] = new SourceRecord(Opt(source!, "role") ?? "unspecified", Opt(source!, "sha256") ?? string.Empty, date);
        }

        var planned = touched.Select(t => (Path: System.IO.Path.Combine(corpus, t.Key + ".md"), Before: originals[t.Key], After: t.Value.Render())).ToList();
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
