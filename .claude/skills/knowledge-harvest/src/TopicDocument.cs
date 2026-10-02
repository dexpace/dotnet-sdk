// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using System.Text.RegularExpressions;

namespace KnowledgeHarvest;

/// <summary>One stored entry: a statement and its provenance.</summary>
internal sealed record TopicEntry(string Statement, string Role, string Evidence, string Confidence, string Sha256);

/// <summary>A recorded cross-source contradiction, unresolved until a human decides it.</summary>
internal sealed record TopicConflict(string Title, string Text, string Sources, string Date);

/// <summary>A statement that a changed source replaced.</summary>
internal sealed record SupersededRecord(string Statement, string Date, string OldSha256, string Sha256);

/// <summary>A topic file, parsed. The renderer in this class is the only writer of these files.</summary>
internal sealed partial class TopicDocument(string topic)
{
    /// <summary>Entry types in emission order, with the heading each renders under.</summary>
    public static readonly IReadOnlyList<(string Type, string Heading)> TypeSections =
        [("rule", "Rules"), ("constraint", "Constraints"), ("conclusion", "Conclusions"), ("reference", "Reference")];

    [GeneratedRegex(@"^- (.+?)\s*$", RegexOptions.ECMAScript)]
    private static partial Regex EntryLine();

    [GeneratedRegex(@"^\s{2}<sub>([^·]+?)\s*·\s*`([^`]+)`\s*·\s*(\w+)\s*·\s*sha:([0-9a-f]+)</sub>\s*$", RegexOptions.ECMAScript)]
    private static partial Regex SubLine();

    [GeneratedRegex(@"^- \*\*(.+?)\*\* — (.+?)\s*$", RegexOptions.ECMAScript)]
    private static partial Regex ConflictLine();

    [GeneratedRegex(@"^\s{2}<sub>(.+?)\s*·\s*unresolved ([\d-]+)</sub>\s*$", RegexOptions.ECMAScript)]
    private static partial Regex ConflictSub();

    [GeneratedRegex(@"^- ~~(.+?)~~ source changed ([\d-]+) \(sha ([0-9a-f]+)… → ([0-9a-f]+)…\)\s*$", RegexOptions.ECMAScript)]
    private static partial Regex SupersededLine();

    /// <summary>The topic slug, which is also the file name without <c>.md</c>.</summary>
    public string Topic { get; } = topic;

    /// <summary>Entries by type.</summary>
    public Dictionary<string, List<TopicEntry>> Entries { get; } = TypeSections.ToDictionary(t => t.Type, _ => new List<TopicEntry>());

    /// <summary>Recorded conflicts.</summary>
    public List<TopicConflict> Conflicts { get; } = [];

    /// <summary>Superseded statements.</summary>
    public List<SupersededRecord> Superseded { get; } = [];

    /// <summary>Total entries across the four types.</summary>
    public int Count => Entries.Values.Sum(v => v.Count);

    /// <summary>Identity of a statement: lower-case, ASCII-unpunctuated, single-spaced.</summary>
    public static string Normalize(string statement)
    {
        var kept = statement.ToLowerInvariant().Where(c => !(c < 128 && char.IsPunctuation(c)) && !(c < 128 && char.IsSymbol(c)));
        return string.Join(' ', new string([.. kept]).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The first 12 hex digits of a digest, the width every provenance line records.</summary>
    public static string Short(string? sha) => sha is { Length: > 12 } ? sha[..12] : sha ?? string.Empty;

    /// <summary>Parses a topic file this class rendered.</summary>
    public static TopicDocument Parse(string text, string topic)
    {
        var document = new TopicDocument(topic);
        var typeOfSection = TypeSections.ToDictionary(t => t.Heading, t => t.Type);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string? section = null;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                section = line[3..].Trim();
                continue;
            }

            var next = index + 1 < lines.Length ? lines[index + 1] : string.Empty;
            if (section is not null && typeOfSection.TryGetValue(section, out var type))
            {
                var entry = EntryLine().Match(line);
                var sub = SubLine().Match(next);
                if (entry.Success && sub.Success)
                {
                    document.Entries[type].Add(new TopicEntry(
                        entry.Groups[1].Value, sub.Groups[1].Value.Trim(), sub.Groups[2].Value, sub.Groups[3].Value, sub.Groups[4].Value));
                    index++;
                }
            }
            else if (section == "Conflicts")
            {
                var conflict = ConflictLine().Match(line);
                var sub = ConflictSub().Match(next);
                if (conflict.Success && sub.Success)
                {
                    document.Conflicts.Add(new TopicConflict(
                        conflict.Groups[1].Value, conflict.Groups[2].Value, sub.Groups[1].Value, sub.Groups[2].Value));
                    index++;
                }
            }
            else if (section == "Superseded" && SupersededLine().Match(line) is { Success: true } old)
            {
                document.Superseded.Add(new SupersededRecord(
                    old.Groups[1].Value, old.Groups[2].Value, old.Groups[3].Value, old.Groups[4].Value));
            }
        }

        return document;
    }

    /// <summary>Renders the topic file: four type sections, then Conflicts and Superseded.</summary>
    public string Render()
    {
        var output = new StringBuilder();
        output.Append("# ").Append(Topic).Append("\n\n");
        foreach (var (type, heading) in TypeSections)
        {
            output.Append("## ").Append(heading).Append('\n');
            foreach (var entry in Entries[type])
            {
                output.Append("- ").Append(entry.Statement).Append('\n');
                output.Append($"  <sub>{entry.Role} · `{entry.Evidence}` · {entry.Confidence} · sha:{Short(entry.Sha256)}</sub>\n");
            }

            output.Append('\n');
        }

        output.Append("## Conflicts\n");
        foreach (var conflict in Conflicts)
        {
            output.Append($"- **{conflict.Title}** — {conflict.Text}\n");
            output.Append($"  <sub>{conflict.Sources} · unresolved {conflict.Date}</sub>\n");
        }

        output.Append("\n## Superseded\n");
        foreach (var record in Superseded)
        {
            output.Append($"- ~~{record.Statement}~~ source changed {record.Date} (sha {Short(record.OldSha256)}… → {Short(record.Sha256)}…)\n");
        }

        output.Append('\n');
        return output.ToString();
    }

    /// <summary>Adds an entry, or refreshes the one whose normalized statement matches.</summary>
    public void Upsert(string type, TopicEntry entry)
    {
        if (!Entries.TryGetValue(type, out var list))
        {
            throw new FormatException($"unknown entry type: {type}");
        }

        var key = Normalize(entry.Statement);
        var position = list.FindIndex(e => Normalize(e.Statement) == key);
        if (position >= 0)
        {
            list[position] = entry;
        }
        else
        {
            list.Add(entry);
        }
    }

    /// <summary>Removes and returns the entry whose normalized statement matches, from whichever type holds it.</summary>
    public TopicEntry? Remove(string statement)
    {
        var key = Normalize(statement);
        foreach (var list in Entries.Values)
        {
            var position = list.FindIndex(e => Normalize(e.Statement) == key);
            if (position >= 0)
            {
                var found = list[position];
                list.RemoveAt(position);
                return found;
            }
        }

        return null;
    }
}
