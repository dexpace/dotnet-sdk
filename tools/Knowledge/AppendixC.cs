// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;

namespace Dexpace.Tools.Knowledge;

/// <summary>One appendix-C row: the ID, its MUST/SHOULD/MAY level, and its subsystem cell.</summary>
internal sealed record Requirement(string Id, string Level, string Subsystem);

/// <summary>
/// Appendix C is the canonical requirement index and the only authority on what a requirement ID looks
/// like. The prefix allowlist is derived from it at runtime and never hardcoded, so a spec revision that
/// adds a subsystem is picked up without editing this file — and so is the prefix → subsystem routing
/// that used to be a hand-maintained table in the skill.
/// </summary>
internal sealed partial class AppendixC
{
    // A chapter must share at least half its name with the subsystem cell before it is offered as that
    // prefix's chapter; below that it is a coincidence.
    private const double ChapterMatchFloor = 0.5;

    private readonly Dictionary<string, Requirement> _requirements;
    private readonly List<string> _ids;
    private readonly KnowledgePaths _paths;
    private List<string>? _chapterFiles;

    public AppendixC(IReadOnlyList<Requirement> requirements, KnowledgePaths paths)
    {
        _requirements = new Dictionary<string, Requirement>(StringComparer.Ordinal);
        _ids = [];
        foreach (var requirement in requirements)
        {
            // A duplicated row replaces the earlier value but keeps its position, as a Ruby Hash does.
            if (!_requirements.ContainsKey(requirement.Id))
            {
                _ids.Add(requirement.Id);
            }

            _requirements[requirement.Id] = requirement;
        }

        _paths = paths;
        Prefixes = _ids.Select(Ids.PrefixOf).ToHashSet(StringComparer.Ordinal);
    }

    [GeneratedRegex(@"\A\|\s*([A-Z][A-Z0-9]{1,11}-[0-9]+)\s*\|", RegexOptions.ECMAScript)]
    private static partial Regex Row();

    [GeneratedRegex("[a-z0-9]+", RegexOptions.ECMAScript)]
    private static partial Regex Word();

    /// <summary>Every canonical ID, in table order.</summary>
    public IReadOnlyList<string> AllIds => _ids;

    public int Count => _ids.Count;

    public IReadOnlySet<string> Prefixes { get; }

    public Requirement this[string id] => _requirements[id];

    public bool Contains(string id) => _requirements.ContainsKey(id);

    public static AppendixC Load(KnowledgePaths paths)
    {
        var path = paths.AppendixC;
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new NotHarvestedException(
                $"cannot read the canonical requirement index at {path}\n" +
                "The requirement-ID allowlist is derived from that table at runtime and there is no\n" +
                "fallback — a bare regex would claim UTF-8, SHA-256 and RFC-3986 as requirement IDs.\n" +
                $"Point this at a checkout that has it with --root DIR (or set {KnowledgePaths.EnvRoot}).", e);
        }

        return new AppendixC(Parse(text, path), paths);
    }

    public static List<Requirement> Parse(string text, string path)
    {
        var requirements = new List<Requirement>();
        foreach (var line in text.Split('\n'))
        {
            var match = Row().Match(line);
            if (!match.Success)
            {
                continue;
            }

            // `| ID | Level | Subsystem | Requirement |` — leading/trailing empties.
            var cells = line.Split('|').Select(cell => cell.Trim()).ToArray();
            requirements.Add(new Requirement(
                match.Groups[1].Value, cells.ElementAtOrDefault(2) ?? "", cells.ElementAtOrDefault(3) ?? ""));
        }

        if (requirements.Count == 0)
        {
            throw new UsageException(
                $"parsed zero requirement IDs out of {path}; its table format changed and the allowlist " +
                "cannot be derived — refusing to fall back to a bare regex, which false-positives on " +
                "UTF-8 and SHA-256");
        }

        return requirements;
    }

    public List<string> IdsFor(string prefix) =>
        Ids.Sort(_ids.Where(id => Ids.PrefixOf(id) == prefix));

    /// <summary>
    /// The subsystem cell, taken as the most common value across the family: one stray row must not
    /// rename the subsystem.
    /// </summary>
    public string SubsystemFor(string prefix) =>
        CountBy(IdsFor(prefix).Select(id => _requirements[id].Subsystem))
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .FirstOrDefault() ?? "";

    public List<KeyValuePair<string, int>> LevelsFor(string prefix) =>
        [.. CountBy(IdsFor(prefix).Select(id => _requirements[id].Level)).OrderByDescending(pair => pair.Value)];

    /// <summary>
    /// The owning chapter, derived by matching the subsystem cell against the numbered chapter files
    /// rather than from a routing table that drifts.
    /// </summary>
    public string? ChapterFor(string prefix)
    {
        var wanted = NameTokens(SubsystemFor(prefix));
        if (wanted.Count == 0)
        {
            return null;
        }

        (double Score, string File)? best = null;
        foreach (var file in ChapterFiles())
        {
            var stem = ChapterStem(file);
            var tokens = NameTokens(stem);
            if (tokens.Count == 0)
            {
                continue;
            }

            var score = (double)tokens.Count(wanted.Contains) / tokens.Count;
            if (score >= ChapterMatchFloor && (best is null || score > best.Value.Score))
            {
                best = (score, file);
            }
        }

        return best?.File;
    }

    public IReadOnlyList<string> ChapterFiles()
    {
        if (_chapterFiles is null)
        {
            _chapterFiles = Directory.Exists(_paths.ProductSpecDir)
                ? [.. Directory.EnumerateFileSystemEntries(_paths.ProductSpecDir)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Where(IsChapterFile)
                    .Order(StringComparer.Ordinal)]
                : [];
        }

        return _chapterFiles;
    }

    private static bool IsChapterFile(string name) =>
        name.Length > 3 && char.IsAsciiDigit(name[0]) && char.IsAsciiDigit(name[1]) && name[2] == '-'
        && name.EndsWith(".md", StringComparison.Ordinal);

    // `04-core-http-domain-model.md` → `core-http-domain-model`.
    private static string ChapterStem(string file)
    {
        var dash = file.IndexOf('-', StringComparison.Ordinal);
        var stem = dash > 0 && file[..dash].All(char.IsAsciiDigit) ? file[(dash + 1)..] : file;
        return stem.EndsWith(".md", StringComparison.Ordinal) ? stem[..^3] : stem;
    }

    // Lower-cased word tokens with a naive singular fold, so `seams` matches `seam` and `pipelines`
    // matches `pipeline`.
    private static List<string> NameTokens(string text) =>
        [.. Word().Matches(text.ToLowerInvariant())
            .Select(match => match.Value.EndsWith('s') ? match.Value[..^1] : match.Value)
            .Distinct(StringComparer.Ordinal)];

    // Insertion-ordered counts, so a tie resolves to the first value seen (OrderByDescending is stable).
    private static List<KeyValuePair<string, int>> CountBy(IEnumerable<string> values)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (counts.TryGetValue(value, out var count))
            {
                counts[value] = count + 1;
            }
            else
            {
                order.Add(value);
                counts[value] = 1;
            }
        }

        return [.. order.Select(value => new KeyValuePair<string, int>(value, counts[value]))];
    }
}
