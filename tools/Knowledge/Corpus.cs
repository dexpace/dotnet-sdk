// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Dexpace.Tools.Knowledge;

/// <summary>A topic file in one of the two trees.</summary>
internal sealed record TopicFile(string File, string Topic, string Path, string Origin);

/// <summary>Turns one topic file into entries.</summary>
internal sealed partial class TopicParser
{
    private readonly IReadOnlySet<string> _prefixes;

    public TopicParser(IReadOnlySet<string> prefixes) => _prefixes = prefixes;

    [GeneratedRegex(@"\A##\s+(.+?)\s*\z", RegexOptions.ECMAScript)]
    private static partial Regex SectionHeading();

    [GeneratedRegex(@"\A-\s+(.*)\z", RegexOptions.ECMAScript)]
    private static partial Regex BulletStart();

    [GeneratedRegex(@"\A\s+<sub>(.*)</sub>\s*\z", RegexOptions.ECMAScript)]
    private static partial Regex SubLineRow();

    public List<Entry> Parse(string path, string origin)
    {
        var lines = ReadLines(path);
        var file = System.IO.Path.GetFileName(path);
        var topic = file.EndsWith(".md", StringComparison.Ordinal) ? file[..^3] : file;
        var entries = new List<Entry>();
        string? section = null;
        Entry? current = null;

        void Flush()
        {
            if (current is null)
            {
                return;
            }

            current.Reqs = Ids.Extract(current.Text, _prefixes);
            current.Key = EntryKey(topic, current.Text);
            entries.Add(current);
            current = null;
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var heading = SectionHeading().Match(line);
            if (heading.Success)
            {
                Flush();
                section = heading.Groups[1].Value;
                continue;
            }

            var sub = SubLineRow().Match(line);
            if (current is not null && sub.Success)
            {
                current.MergeSub(SubLine.Parse(sub.Groups[1].Value), Text.Strip(line));
                continue;
            }

            var bullet = BulletStart().Match(line);
            if (bullet.Success)
            {
                Flush();
                current = new Entry(file, topic, origin, index + 1, section, bullet.Groups[1].Value);
                continue;
            }

            // A continuation line: any non-`<sub>` line before the open bullet's provenance line — a few
            // Conflicts entries run to several paragraphs, and dropping the tail silently loses the
            // requirement IDs it cites. An entry ends only at its `<sub>`, the next bullet, the next
            // heading, or end of file.
            if (current is not null && current.SubLineText is null && !Text.IsBlank(line))
            {
                current.Text = $"{current.Text} {Text.Strip(line)}";
            }
        }

        Flush();
        return entries;
    }

    /// <summary>
    /// A topic file authored on Windows, or saved with a BOM, is the one input that fails silently
    /// rather than loudly: <c>\r</c> defeats the bullet anchor and a BOM defeats the heading, so the file
    /// parses to zero entries and every downstream gate then reports OK over a hole. Normalize both at
    /// the door.
    /// </summary>
    public static string[] ReadLines(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new UsageException($"cannot read the topic file {path}", e);
        }

        if (text.StartsWith('﻿'))
        {
            text = text[1..];
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    /// <summary>
    /// A citable name for one entry, stable across a re-order and a re-harvest. The line number is not
    /// that name: an entry moves whenever a neighbour is added. The <c>&lt;sub&gt;</c> sha is not either —
    /// it digests the whole source file, so every entry harvested from one file shares it. The digest of
    /// the entry's own text changes when, and only when, the rule changes.
    /// </summary>
    public static string EntryKey(string topic, string text)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text.StripEnd(text))));
        return $"{topic}/{digest[..8]}";
    }
}

/// <summary>Both trees, loaded and cross-linked.</summary>
internal sealed partial class Corpus
{
    private Dictionary<string, List<Entry>>? _citationIndex;

    public Corpus(List<Entry> entries)
    {
        Entries = entries;
        LinkRelations();
    }

    /// <summary>A key inside an entry's prose, backticked: <c>`pagination/81881061`</c>.</summary>
    [GeneratedRegex("`([a-z0-9-]+/[0-9a-f]{8})`", RegexOptions.ECMAScript)]
    public static partial Regex CitedKey();

    // A note names a harvested rule for two different reasons, and they are not the same relation. It
    // OVERRIDES the rule it corrects; it CITES the rules it leans on. The signal is the note's own
    // relation verb, immediately before the key it governs ("Supersedes `x`", "Resolves `x` and `y`").
    // Every other backticked key in the entry is a citation in support.
    [GeneratedRegex(@"\b(?:supersed|resolv|answer|narrow|overrid|replac|correct)(?:e|es|ed|ing|s)?\b",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex RelationVerb();

    // The run of keys a verb governs: keys, separators and `and`, and nothing else. Local rather than
    // sentence-scoped, so no sentence splitter has to be right about a quoted rule's punctuation.
    [GeneratedRegex(@"\A(?:[\s,]|\band\b|`[a-z0-9-]+/[0-9a-f]{8}`)+", RegexOptions.ECMAScript)]
    private static partial Regex RelationRun();

    public List<Entry> Entries { get; }

    public bool IsEmpty => Entries.Count == 0;

    public static Corpus Load(KnowledgePaths paths, IReadOnlySet<string> prefixes)
    {
        if (!Directory.Exists(paths.KnowledgeDir))
        {
            throw new NotHarvestedException(
                $"{paths.Relative(paths.KnowledgeDir)} does not exist under {paths.Root}\n" +
                "The corpus is two trees — harvested/ (what the source documents say, written by the\n" +
                "knowledge-harvest skill) and notes/ (what the implementation found) — and neither has\n" +
                "been created here yet. Harvest first, or query another checkout with --root DIR.");
        }

        if (!Directory.Exists(paths.HarvestedDir))
        {
            throw new NotHarvestedException(
                $"cannot read the harvested corpus at {paths.HarvestedDir}; docs/knowledge/ is two trees " +
                "(harvested/ and notes/) and the harvested one is not optional");
        }

        var parser = new TopicParser(prefixes);
        return new Corpus([.. TopicFiles(paths).SelectMany(topic => parser.Parse(topic.Path, topic.Origin))]);
    }

    /// <summary>
    /// Every topic file in both trees, harvested first, as records rather than bare names: a caller needs
    /// the tree an entry came from, and <c>pagination.md</c> exists in both.
    /// </summary>
    public static List<TopicFile> TopicFiles(KnowledgePaths paths)
    {
        var found = new List<TopicFile>();
        foreach (var (origin, dir) in new[] { ("harvested", paths.HarvestedDir), ("note", paths.NotesDir) })
        {
            // The notes tree holds a file only where a note exists. None yet is a legitimate state.
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var names = Directory.EnumerateFiles(dir)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => name.EndsWith(".md", StringComparison.Ordinal) && !Vocabulary.NonTopicFiles.Contains(name))
                .Order(StringComparer.Ordinal);
            found.AddRange(names.Select(name => new TopicFile(name, name[..^3], Path.Combine(dir, name), origin)));
        }

        return found;
    }

    public List<string> Topics() => [.. Entries.Select(e => e.Topic).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>Requirement ID → every entry citing it, in first-cited order.</summary>
    public Dictionary<string, List<Entry>> CitationIndex()
    {
        if (_citationIndex is null)
        {
            _citationIndex = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
            foreach (var entry in Entries)
            {
                foreach (var id in entry.Reqs)
                {
                    if (!_citationIndex.TryGetValue(id, out var list))
                    {
                        _citationIndex[id] = list = [];
                    }

                    list.Add(entry);
                }
            }
        }

        return _citationIndex;
    }

    /// <summary>
    /// Every note citation that resolves to no entry. A key digests entry text, so a re-harvest that
    /// rewords a rule leaves the citing note pointing at nothing.
    /// </summary>
    public List<(string Note, string Cited)> DanglingKeys()
    {
        var known = Entries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var dangling = new List<(string, string)>();
        foreach (var note in Entries.Where(e => e.IsNote))
        {
            foreach (Match match in CitedKey().Matches(note.Text))
            {
                if (!known.Contains(match.Groups[1].Value))
                {
                    dangling.Add((note.Location, match.Groups[1].Value));
                }
            }
        }

        return dangling;
    }

    /// <summary>Every key a note names after a relation verb: the rules it overrides.</summary>
    public static List<string> OverriddenKeys(string text)
    {
        var found = new List<string>();
        foreach (Match verb in RelationVerb().Matches(text))
        {
            var run = RelationRun().Match(text[(verb.Index + verb.Length)..]);
            if (!run.Success)
            {
                continue;
            }

            found.AddRange(CitedKey().Matches(run.Value).Select(match => match.Groups[1].Value));
        }

        return [.. found.Distinct(StringComparer.Ordinal)];
    }

    // A note names harvested rules by key. Resolve those references once, at load, split them by
    // relation, and hang the answer on both ends — so a query that lands on the harvested entry by any
    // route still says the implementation overruled (or leans on) it.
    private void LinkRelations()
    {
        var byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var entry in Entries)
        {
            byKey[entry.Key] = entry;
        }

        foreach (var note in Entries.Where(e => e.IsNote))
        {
            var overridden = OverriddenKeys(note.Text);
            var cited = CitedKey().Matches(note.Text).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal);
            foreach (var key in cited)
            {
                if (!byKey.TryGetValue(key, out var target) || ReferenceEquals(target, note))
                {
                    continue;
                }

                if (overridden.Contains(key))
                {
                    target.OverriddenBy.Add(note.Location);
                    note.Overrides.Add(key);
                }
                else
                {
                    target.CitedBy.Add(note.Location);
                    note.Cites.Add(key);
                }
            }
        }
    }
}
