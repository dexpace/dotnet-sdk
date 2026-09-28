// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Dexpace.Tools.Knowledge;

/// <summary>
/// <c>docs/knowledge/</c> is two trees, and this is the gate that keeps them apart. A <c>&lt;sub&gt;</c>
/// sha digests the WHOLE source file, not the entry, so an edit to an entry's text does not change it and
/// the next harvest cannot see the edit — it regenerates the original text or writes a duplicate.
/// Hand-written knowledge under <c>harvested/</c> is therefore scheduled for silent deletion. The rules
/// below are the smallest set that catches it. Structural only: whether an entry is TRUE is what the
/// drift report and a re-harvest are for. Exit 0 clean, 1 with violations, 2 when it cannot run.
/// </summary>
internal sealed partial class StructureGate
{
    public const string Help = """
        Usage: scripts/knowledge verify-structure [--root DIR]

        Checks that docs/knowledge/harvested/ (generated) and docs/knowledge/notes/
        (hand-written) keep to their structural rules, so a hand-written entry is
        never mistaken for a generated one. Exits 0 clean, 1 with violations, 2 when
        the gate itself cannot run.

          --root DIR   repository root (default: the checkout this tool was built from)
          -h, --help   print this message

        """;

    // A generated INDEX that a hand edit has drifted below is a stale index, not a parse hole; only a
    // collapse this large means the parser stopped working.
    private const double ParseFloorRatio = 0.5;

    private readonly KnowledgePaths _paths;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public StructureGate(KnowledgePaths paths, TextWriter stdout, TextWriter stderr)
    {
        _paths = paths;
        _stdout = stdout;
        _stderr = stderr;
    }

    // `| topic | `file.md` | 42 | roles | conflicts | date |` in INDEX.md.
    [GeneratedRegex(@"\A\|\s*([a-z0-9-]+)\s*\|\s*`([^`]+)`\s*\|\s*([0-9]+)\s*\|", RegexOptions.ECMAScript)]
    private static partial Regex IndexRow();

    public int Run()
    {
        if (!Directory.Exists(_paths.KnowledgeDir))
        {
            _stdout.Write($"knowledge structure OK: {_paths.Relative(_paths.KnowledgeDir)} does not exist under " +
                          $"{_paths.Root}, so there are no two trees to keep apart yet.\n");
            return 0;
        }

        var roots = SourceRoots(SourceManifest.Load(_paths));
        var corpus = Corpus.Load(_paths, AppendixC.Load(_paths).Prefixes);
        var harvested = corpus.Entries.Where(entry => !entry.IsNote).ToList();
        CheckParseFloor(harvested);

        var violations = StructuralViolations(harvested, roots)
            .Concat(OrphanedNoteKeys(corpus.Entries))
            .Concat(NoteViolations(corpus.Entries))
            .Concat(StrayTopicFiles())
            .ToList();
        if (violations.Count > 0)
        {
            foreach (var violation in violations)
            {
                _stderr.Write($"knowledge-structure violation: {violation}\n");
            }

            _stderr.Write($"{violations.Count} violation(s). docs/knowledge/harvested/ carries only harvested " +
                          "entries; hand-written knowledge lives in docs/knowledge/notes/ with role `review` and " +
                          "a manual sha marker.\n");
            return 1;
        }

        var notes = corpus.Entries.Count - harvested.Count;
        _stdout.Write($"knowledge structure OK: {harvested.Count} harvested entries, each with a source under one " +
                      $"of {roots.Count} roots and a role among {string.Join('/', Vocabulary.HarvestedRoles)}, none " +
                      $"Superseded; {notes} notes, all review-role, every cited key live; nothing stranded at the root.\n");
        return 0;
    }

    // The source roots are not hardcoded: they are the directories the harvest manifest itself names, so
    // a new root is a SOURCES.md edit, reviewable in the same diff as the entries that use it. One row at a
    // root's parent would widen the allowlist to everything beneath it; refuse rather than silently widen.
    private static List<string> SourceRoots(SourceManifest manifest)
    {
        var roots = manifest.Roots();
        foreach (var root in roots)
        {
            var swallowed = roots.FirstOrDefault(other => other != root && other.StartsWith(root + "/", StringComparison.Ordinal));
            if (swallowed is not null)
            {
                throw new UsageException(
                    $"{manifest.FilePath} derives the source root '{root}', which contains '{swallowed}'. A root " +
                    "that contains another admits everything beneath it; list sources at one level, not two.");
            }
        }

        return roots;
    }

    // A parse that silently yields nothing (a CRLF topic file, a moved directory) would let this gate print
    // OK over an empty tree, which is worse than failing. The floor is derived from the generated INDEX.md,
    // so it calibrates itself as the corpus grows rather than needing a magic number maintained by hand.
    private void CheckParseFloor(List<Entry> harvested)
    {
        var counts = harvested.GroupBy(entry => entry.Topic, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var stated = IndexCounts();
        if (stated.Count == 0)
        {
            CheckUncountedFloor(counts);
            return;
        }

        var empty = stated.Where(pair => pair.Value > 0 && counts.GetValueOrDefault(pair.Key) == 0)
            .Select(pair => pair.Key).Order(StringComparer.Ordinal).ToList();
        if (empty.Count > 0)
        {
            throw new UsageException(
                $"{_paths.Relative(_paths.TopicIndex)} states entries for {string.Join(", ", empty)}, and each " +
                "parses to zero. The corpus did not shrink by hand — a parse is failing (a CRLF or BOM topic file, " +
                "a moved directory), and every rule below would pass vacuously over the hole.");
        }

        var total = counts.Values.Sum();
        var statedTotal = stated.Values.Sum();
        var floor = (int)Math.Floor(statedTotal * ParseFloorRatio);
        if (total < floor)
        {
            throw new UsageException(
                $"only {total} harvested entries parsed against the {statedTotal} " +
                $"{_paths.Relative(_paths.TopicIndex)} states; below the {floor} floor that separates an edit " +
                "from a broken parse.");
        }
    }

    // Without a generated index, the tell is a topic file that plainly holds bullets and yields no entry.
    private void CheckUncountedFloor(Dictionary<string, int> counts)
    {
        var broken = Corpus.TopicFiles(_paths)
            .Where(topic => topic.Origin == "harvested" && counts.GetValueOrDefault(topic.Topic) == 0)
            .Where(topic => File.ReadLines(topic.Path).Any(line => line.StartsWith("- ", StringComparison.Ordinal)))
            .Select(topic => topic.File)
            .ToList();
        if (broken.Count > 0)
        {
            throw new UsageException(
                $"{string.Join(", ", broken)} hold bullets but parse to zero entries; the parser is failing on " +
                "them and every rule below would pass vacuously over the hole.");
        }
    }

    private Dictionary<string, int> IndexCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!File.Exists(_paths.TopicIndex))
        {
            return counts;
        }

        foreach (var line in File.ReadLines(_paths.TopicIndex))
        {
            var match = IndexRow().Match(line);
            if (match.Success)
            {
                counts[match.Groups[1].Value] = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            }
        }

        return counts;
    }

    // Normalize first: a `..` segment makes a string prefix test meaningless, and the point of this check
    // is where the path actually lands.
    private static bool UnderRoot(string source, List<string> roots)
    {
        var path = Normalize(Entry.LineSuffix().Replace(source, "", 1));
        return roots.Any(root => path.StartsWith(Normalize(root) + "/", StringComparison.Ordinal));
    }

    // Ruby's File.expand_path(path, "/"): resolve `.` and `..` against the filesystem root, host-independent.
    public static string Normalize(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            segments.Add(segment);
        }

        return "/" + string.Join('/', segments);
    }

    private static List<string> StructuralViolations(List<Entry> harvested, List<string> roots)
    {
        var violations = new List<string>();
        foreach (var entry in harvested)
        {
            var at = $"{entry.Location} ({entry.Section})";
            if (entry.Roles.Contains("review"))
            {
                violations.Add($"{at}: role `review` under harvested/. A review-role entry is hand-written " +
                               "knowledge; move it to docs/knowledge/notes/.");
            }

            // `review` is the label an honest hand edit wears. An invented role is what a careless one
            // wears, and it would otherwise pass every rule here.
            violations.AddRange(entry.Roles
                .Where(role => role != "review" && !Vocabulary.HarvestedRoles.Contains(role))
                .Select(role => $"{at}: role `{role}` under harvested/, which is not one of " +
                                $"{string.Join(", ", Vocabulary.HarvestedRoles)}. A harvested entry carries the " +
                                "role of the document it came from."));

            // The per-source loop below iterates zero times on an entry with no provenance line at all, so
            // the strongest rule here would never run on the shape a hand-written bullet most likely has.
            if (entry.Sources.Count == 0)
            {
                violations.Add($"{at}: no source. Every harvested entry carries a `<sub>` naming the document " +
                               "it came from; a bullet without one was written by hand.");
            }

            if (entry.Section == "Superseded")
            {
                violations.Add($"{at}: a Superseded entry under harvested/. Superseding is a judgement the " +
                               "implementation made; it belongs in docs/knowledge/notes/.");
            }

            violations.AddRange(entry.Sources
                .Where(source => !UnderRoot(source, roots))
                .Select(source => $"{at}: cites `{source}`, which is under none of the harvested source roots " +
                                  $"({string.Join(", ", roots)}). Only a harvest of those roots belongs in harvested/."));
        }

        return violations;
    }

    // A note names a harvested rule by that rule's stable key — whether it overrides the rule or only leans
    // on it. The key is digested from the entry's text, so a re-harvest that rewords the rule orphans every
    // note citing it. Failing here means a re-harvest cannot land until the notes it invalidates are
    // updated in the same commit, which is the point.
    private static List<string> OrphanedNoteKeys(List<Entry> entries)
    {
        var live = entries.Where(entry => !entry.IsNote).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        return [.. entries.Where(entry => entry.IsNote)
            .SelectMany(note => Corpus.CitedKey().Matches(note.Text)
                .Select(match => match.Groups[1].Value)
                .Where(key => !live.Contains(key))
                .Select(key => $"{note.Location}: cites `{key}`, which no harvested entry carries. A key is " +
                               "digested from the entry text, so a re-harvest that rewords the rule changes it — " +
                               "update the note to the new key (`scripts/knowledge --topic <topic> --section " +
                               "<section>` prints it)."))];
    }

    // The mirror of the first rule, and fatal for the same reason: a note that does not say `review` reads,
    // in a query result, exactly as though a source document had said it.
    private static List<string> NoteViolations(List<Entry> entries) =>
        [.. entries.Where(entry => entry.IsNote && !entry.Roles.Contains("review"))
            .Select(entry => $"{entry.Location}: a note whose role is `{entry.Role ?? "none"}`, not `review`. A note " +
                             "states what the implementation found; that is what the role says.")];

    // `knowledge-harvest` defaults its --corpus to `<cwd>/docs/knowledge/`, so a run that forgets
    // `--corpus docs/knowledge/harvested` writes a third copy of the corpus at the root. No query reads
    // it, so the knowledge is not wrong, it is invisible.
    private List<string> StrayTopicFiles() =>
        [.. Directory.EnumerateFiles(_paths.KnowledgeDir)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => name.EndsWith(".md", StringComparison.Ordinal) && name != "README.md")
            .Order(StringComparer.Ordinal)
            .Select(name => $"docs/knowledge/{name}: a topic file at the root of docs/knowledge/, which is neither " +
                            "tree — no query reads it. Move it into harvested/ or notes/. (A `knowledge-harvest` " +
                            "run without `--corpus docs/knowledge/harvested` writes here.)")];
}
