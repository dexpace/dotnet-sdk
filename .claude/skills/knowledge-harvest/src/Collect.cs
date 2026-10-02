// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KnowledgeHarvest;

/// <summary>One entry as an extractor proposed it.</summary>
internal sealed record Proposed(string Type, string Topic, string Statement, string Evidence, string Confidence);

/// <summary>What <see cref="Collect"/> found, besides the decisions it wrote.</summary>
internal sealed record CollectReport(int Parsed, int Kept, int Duplicates, List<string> Dropped, SortedDictionary<string, int> Topics);

/// <summary>
/// Turns the extractors' text output into <c>entries.json</c>: a deterministic parse, a mechanical
/// validation of every citation against the manifest and the files, and the attachment of the role and
/// whole-file sha the extractor never emits. It never invents an entry and never rewrites a statement; an
/// entry whose evidence does not resolve is dropped and reported, not repaired.
/// </summary>
internal static partial class Collect
{
    private static readonly string[] s_types = ["rule", "constraint", "conclusion", "reference"];
    private static readonly string[] s_confidences = ["high", "medium", "low"];

    [GeneratedRegex(@"^(?<path>.+?):(?<start>\d+)(?:-(?<end>\d+))?$", RegexOptions.ECMAScript)]
    private static partial Regex EvidenceShape();

    [GeneratedRegex(@"^\s*-\s+(?<key>[a-z]+):\s?(?<value>.*)$", RegexOptions.ECMAScript)]
    private static partial Regex ItemStart();

    [GeneratedRegex(@"^\s{2,}(?<key>type|topic|statement|evidence|confidence):\s?(?<value>.*)$", RegexOptions.ECMAScript)]
    private static partial Regex ItemField();

    /// <summary>Parses one extractor's output. Anything outside <c>## Entries</c> is ignored.</summary>
    public static List<Proposed> ParseOutput(string text)
    {
        var proposals = new List<Proposed>();
        Dictionary<string, string>? current = null;
        string? lastKey = null;
        var inEntries = false;

        void Flush()
        {
            if (current is not null)
            {
                proposals.Add(new Proposed(
                    current.GetValueOrDefault("type", string.Empty).Trim().ToLowerInvariant(),
                    current.GetValueOrDefault("topic", string.Empty).Trim().ToLowerInvariant(),
                    current.GetValueOrDefault("statement", string.Empty).Trim(),
                    current.GetValueOrDefault("evidence", string.Empty).Trim().Trim('`'),
                    current.GetValueOrDefault("confidence", "medium").Trim().ToLowerInvariant()));
            }

            current = null;
            lastKey = null;
        }

        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                inEntries = line.Trim().Equals("## Entries", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inEntries)
            {
                continue;
            }

            var start = ItemStart().Match(line);
            if (start.Success && start.Groups["key"].Value == "type")
            {
                Flush();
                current = new Dictionary<string, string> { ["type"] = start.Groups["value"].Value };
                lastKey = "type";
                continue;
            }

            var field = ItemField().Match(line);
            if (current is not null && field.Success)
            {
                lastKey = field.Groups["key"].Value;
                current[lastKey] = field.Groups["value"].Value;
            }
            else if (current is not null && lastKey is not null && line.Trim().Length > 0)
            {
                current[lastKey] = $"{current[lastKey]} {line.Trim()}";
            }
        }

        Flush();
        return proposals;
    }

    /// <summary>
    /// Resolves an evidence string to a manifest file and a checked range, or explains why it cannot.
    /// A bare <c>path:N</c> means the one line <c>N</c>. The extractor contract is ONE range per entry; if an
    /// extractor lists several parts the first one that resolves is kept and the rest are dropped.
    /// </summary>
    public static (ChunkFile File, string Evidence)? ResolveEvidence(
        string evidence, IReadOnlyDictionary<string, List<ChunkFile>> files, IReadOnlyDictionary<string, int> lineCounts, out string? problem)
    {
        problem = null;
        foreach (var part in evidence.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = EvidenceShape().Match(part.Trim('`', ' '));
            if (!match.Success)
            {
                problem = $"evidence '{part}' is not path:start-end";
                continue;
            }

            var path = match.Groups["path"].Value;
            if (!int.TryParse(match.Groups["start"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(match.Groups["end"].Success ? match.Groups["end"].Value : match.Groups["start"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var end))
            {
                problem = $"evidence '{part}' has a line number that is not a valid line";
                continue;
            }

            if (!files.TryGetValue(path, out var assigned))
            {
                problem = $"evidence path '{path}' is not in the manifest";
                continue;
            }

            var total = lineCounts.GetValueOrDefault(path);
            var owner = assigned.FirstOrDefault(f => f.Lines is null || (start >= f.Lines[0] && end <= f.Lines[1]));
            if (start < 1 || end < start || end > total || owner is null)
            {
                problem = $"evidence '{path}:{start}-{end}' is outside the assigned lines (file has {total})";
                continue;
            }

            problem = null;
            return (owner, $"{path}:{start}-{end}");
        }

        problem ??= "no evidence";
        return null;
    }

    /// <summary>Collects every <c>*.md</c> / <c>*.txt</c> extractor output in a directory into decisions.</summary>
    public static (JsonObject Entries, CollectReport Report) Run(
        Manifest manifest,
        string outputsDirectory,
        IReadOnlyDictionary<string, string> topicMap,
        IReadOnlyList<JsonNode> extraDecisions,
        string harvested)
    {
        var files = manifest.Chunks.SelectMany(c => c.Files).GroupBy(f => f.Path).ToDictionary(g => g.Key, g => g.ToList());
        var byChunk = manifest.Chunks.ToDictionary(
            c => c.Id,
            c => (IReadOnlyDictionary<string, List<ChunkFile>>)c.Files.GroupBy(f => f.Path).ToDictionary(g => g.Key, g => g.ToList()),
            StringComparer.Ordinal);
        var roles = manifest.Chunks.SelectMany(c => c.Files.Select(f => (f.Path, c.Role))).GroupBy(x => x.Path).ToDictionary(g => g.Key, g => g.First().Role);
        var lineCounts = files.Keys.ToDictionary(p => p, p => Sections.CountLines(File.ReadAllText(p, Encoding.UTF8)));

        var kept = new Dictionary<(string Role, string Topic, string Type, string Key), (Proposed P, string Evidence, ChunkFile File)>();
        var dropped = new List<string>();
        var parsed = 0;
        var duplicates = 0;

        var outputs = Directory.GetFiles(outputsDirectory)
            .Where(f => f.EndsWith(".md", StringComparison.Ordinal) || f.EndsWith(".txt", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        foreach (var path in outputs)
        {
            foreach (var proposal in ParseOutput(File.ReadAllText(path, Encoding.UTF8)))
            {
                parsed++;
                var label = $"{System.IO.Path.GetFileName(path)}: {Truncate(proposal.Statement)}";

                // An entry is validated against the files and ranges of the chunk that produced it (the output
                // file is named for its chunk id), not against every chunk's: an extractor reading lines 1-245
                // of a split file has no business citing line 300.
                if (!byChunk.TryGetValue(System.IO.Path.GetFileNameWithoutExtension(path), out var chunkFiles))
                {
                    dropped.Add($"{label} — the output file name is not a chunk id of the manifest");
                    continue;
                }

                if (Validate(proposal, topicMap, chunkFiles, lineCounts, label, dropped) is not { } checkedEntry)
                {
                    continue;
                }

                var role = roles[checkedEntry.File.Path];
                var key = (role, checkedEntry.P.Topic, checkedEntry.P.Type, TopicDocument.Normalize(checkedEntry.P.Statement));
                if (kept.TryGetValue(key, out var existing))
                {
                    duplicates++;
                    if (Span(checkedEntry.Evidence) >= Span(existing.Evidence))
                    {
                        continue;
                    }
                }

                kept[key] = checkedEntry;
            }
        }

        var (decisions, topics) = ToDecisions(kept, roles);
        foreach (var extra in extraDecisions)
        {
            decisions.Add(extra.DeepClone());
        }

        var sources = SourceRows(files, roles);

        var entries = new JsonObject { ["harvested"] = harvested, ["sources"] = sources, ["decisions"] = decisions };
        return (entries, new CollectReport(parsed, kept.Count, duplicates, dropped, topics));
    }

    private static JsonArray SourceRows(Dictionary<string, List<ChunkFile>> files, Dictionary<string, string> roles)
    {
        var sources = new JsonArray();
        foreach (var (path, assigned) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            sources.Add(new JsonObject { ["path"] = path, ["role"] = roles[path], ["sha256"] = assigned[0].Sha256 });
        }

        return sources;
    }

    private static (Proposed P, string Evidence, ChunkFile File)? Validate(
        Proposed proposal,
        IReadOnlyDictionary<string, string> topicMap,
        IReadOnlyDictionary<string, List<ChunkFile>> files,
        IReadOnlyDictionary<string, int> lineCounts,
        string label,
        List<string> dropped)
    {
        if (!s_types.Contains(proposal.Type) || proposal.Statement.Length == 0)
        {
            dropped.Add($"{label} — bad type '{proposal.Type}' or empty statement");
            return null;
        }

        if (ResolveEvidence(proposal.Evidence, files, lineCounts, out var problem) is not { } resolved)
        {
            dropped.Add($"{label} — {problem}");
            return null;
        }

        var topic = topicMap.GetValueOrDefault(proposal.Topic, proposal.Topic);
        if (!Regex.IsMatch(topic, "^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.ECMAScript, TimeSpan.FromSeconds(1)))
        {
            dropped.Add($"{label} — topic '{topic}' is not a kebab-case slug");
            return null;
        }

        var confidence = s_confidences.Contains(proposal.Confidence) ? proposal.Confidence : "medium";
        return (proposal with { Topic = topic, Confidence = confidence }, resolved.Evidence, resolved.File);
    }

    private static (JsonArray Decisions, SortedDictionary<string, int> Topics) ToDecisions(
        Dictionary<(string Role, string Topic, string Type, string Key), (Proposed P, string Evidence, ChunkFile File)> kept,
        Dictionary<string, string> roles)
    {
        var decisions = new JsonArray();
        var topics = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, (proposal, evidence, file)) in kept.OrderBy(k => k.Key.Topic, StringComparer.Ordinal).ThenBy(k => k.Value.File.Path, StringComparer.Ordinal).ThenBy(k => StartLine(k.Value.Evidence)).ThenBy(k => k.Value.Evidence, StringComparer.Ordinal))
        {
            topics[proposal.Topic] = topics.GetValueOrDefault(proposal.Topic) + 1;
            decisions.Add(new JsonObject
            {
                ["action"] = "new",
                ["topic"] = proposal.Topic,
                ["entry"] = new JsonObject
                {
                    ["type"] = proposal.Type,
                    ["statement"] = proposal.Statement,
                    ["evidence"] = evidence,
                    ["confidence"] = proposal.Confidence,
                    ["role"] = roles[file.Path],
                    ["sha256"] = file.Sha256,
                },
            });
        }

        return (decisions, topics);
    }

    private static int StartLine(string evidence) =>
        EvidenceShape().Match(evidence) is { Success: true } m ? int.Parse(m.Groups["start"].Value, CultureInfo.InvariantCulture) : 0;

    private static int Span(string evidence)
    {
        var match = EvidenceShape().Match(evidence);
        return match.Success && match.Groups["end"].Success
            ? int.Parse(match.Groups["end"].Value, CultureInfo.InvariantCulture) - int.Parse(match.Groups["start"].Value, CultureInfo.InvariantCulture)
            : 0;
    }

    private static string Truncate(string text) => text.Length <= 80 ? text : text[..80] + "…";
}
