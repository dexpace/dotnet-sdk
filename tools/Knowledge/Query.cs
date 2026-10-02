// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Dexpace.Tools.Knowledge;

/// <summary>The raw flag values the CLI collected, before validation.</summary>
internal sealed class QueryOptions
{
    public List<string> Req { get; } = [];

    public List<string> Key { get; } = [];

    public List<string> Prefix { get; } = [];

    public List<string> Origin { get; } = [];

    public List<string> Topic { get; } = [];

    public List<string> Section { get; } = [];

    public List<string> Role { get; } = [];

    public List<string> Chapter { get; } = [];

    public List<string> Grep { get; } = [];

    public string? Phase { get; set; }

    public List<string> Gaps { get; } = [];

    public string? PrefixInfo { get; set; }

    public string? Root { get; set; }

    public bool Brief { get; set; }

    public bool Json { get; set; }

    public bool ListTopics { get; set; }

    public bool ListReqs { get; set; }

    public bool Coverage { get; set; }

    public bool DriftCheck { get; set; } = true;

    public bool Help { get; set; }
}

/// <summary>The filter dimensions, named as the zero-result diagnosis prints them.</summary>
internal enum Dimension
{
    Reqs,
    Keys,
    Prefixes,
    Origins,
    Topics,
    Roles,
    Chapters,
    Sections,
    Patterns,
}

/// <summary>
/// The filters, and the one predicate that applies them. Different filters AND together; multiple
/// values inside one filter OR.
/// </summary>
internal sealed partial class Query
{
    private readonly Dictionary<Dimension, int> _counts;

    public Query(
        List<string>? reqs = null,
        List<string>? keys = null,
        List<string>? prefixes = null,
        List<string>? origins = null,
        List<string>? topics = null,
        List<string>? roles = null,
        List<string>? chapters = null,
        List<string>? sections = null,
        List<Regex>? patterns = null)
    {
        Reqs = reqs ?? [];
        Keys = keys ?? [];
        Prefixes = prefixes ?? [];
        Origins = origins ?? [];
        Topics = topics ?? [];
        Roles = roles ?? [];
        Chapters = chapters ?? [];
        Sections = sections ?? [];
        Patterns = patterns ?? [];
        _counts = new Dictionary<Dimension, int>
        {
            [Dimension.Reqs] = Reqs.Count,
            [Dimension.Keys] = Keys.Count,
            [Dimension.Prefixes] = Prefixes.Count,
            [Dimension.Origins] = Origins.Count,
            [Dimension.Topics] = Topics.Count,
            [Dimension.Roles] = Roles.Count,
            [Dimension.Chapters] = Chapters.Count,
            [Dimension.Sections] = Sections.Count,
            [Dimension.Patterns] = Patterns.Count,
        };
    }

    // A key is `<topic>/<8 hex>`. Not validated against the corpus: a key that resolves to nothing is
    // exactly what a reader needs told.
    [GeneratedRegex(@"\A[a-z0-9-]+/[0-9a-f]{8}\z", RegexOptions.ECMAScript)]
    private static partial Regex EntryKey();

    [GeneratedRegex(@"\A([0-9]{1,2})(?:\.([0-9]+))?\z", RegexOptions.ECMAScript)]
    private static partial Regex ChapterSpec();

    public List<string> Reqs { get; }

    public List<string> Keys { get; }

    public List<string> Prefixes { get; }

    public List<string> Origins { get; }

    public List<string> Topics { get; }

    public List<string> Roles { get; }

    public List<string> Chapters { get; }

    public List<string> Sections { get; }

    public List<Regex> Patterns { get; }

    public List<Dimension> UsedDimensions() => [.. Enum.GetValues<Dimension>().Where(d => _counts[d] > 0)];

    public bool IsEmpty => UsedDimensions().Count == 0;

    public static Query Build(
        QueryOptions options, IReadOnlyList<string> words, AppendixC appendix, IEnumerable<string>? extraReqs, TextWriter warn)
    {
        var reqs = SplitValues(options.Req, "--req").Concat(extraReqs ?? []).Distinct(StringComparer.Ordinal).ToList();
        foreach (var id in reqs.Where(id => !appendix.Contains(id)))
        {
            warn.Write($"warning: {id} is not in appendix C — it is not a canonical requirement ID, so no " +
                       "entry can legitimately cite it\n");
        }

        return new Query(
            reqs: reqs,
            keys: ParseKeys(options.Key),
            prefixes: ParseIdPrefixes(options.Prefix, appendix.Prefixes),
            origins: ParseEnum(options.Origin, "--origin", Vocabulary.Origins),
            topics: SplitValues(options.Topic, "--topic"),
            roles: ParseEnum(options.Role, "--role", Vocabulary.Roles),
            chapters: ParseChapters(options.Chapter, warn),
            sections: ParseSections(options.Section),
            patterns: ParsePatterns(options.Grep, words));
    }

    /// <summary>
    /// <c>--req A,B --req C</c> and <c>--req A --req B</c> mean the same thing. An empty value is
    /// dropped, and a filter whose values were ALL empty is a refusal: <c>--topic ''</c> and the typo
    /// <c>--topic 'a,b,'</c> would otherwise match every file.
    /// </summary>
    public static List<string> SplitValues(IEnumerable<string> values, string flag)
    {
        var supplied = values.SelectMany(value => value.Length == 0 ? [value] : value.Split(',')).ToList();
        var kept = supplied.Where(value => !Text.IsBlank(value)).ToList();
        if (supplied.Count > 0 && kept.Count == 0)
        {
            throw new UsageException(
                $"{flag} was given only empty values; an empty value matches every entry, so this would " +
                "print the whole corpus. Drop the flag, or a stray comma.");
        }

        return kept;
    }

    private static List<string> ParseEnum(IEnumerable<string> values, string flag, IReadOnlyList<string> allowed) =>
        [.. SplitValues(values, flag).Select(value => allowed.Contains(value)
            ? value
            : throw new UsageException(
                $"unknown {flag[2..]} '{value}'; the values are {string.Join(", ", allowed)}"))];

    private static List<string> ParseSections(IEnumerable<string> values) =>
        [.. SplitValues(values, "--section").Select(value =>
            Vocabulary.Sections.FirstOrDefault(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            ?? throw new UsageException(
                $"unknown section '{value}'; the six sections are {string.Join(", ", Vocabulary.Sections)}"))];

    // An ID family, for the audit query. Validated against appendix C for the same reason --req warns:
    // `--prefix UTF` would otherwise be a silent empty result rather than a typo.
    private static List<string> ParseIdPrefixes(IEnumerable<string> values, IReadOnlySet<string> known) =>
        [.. SplitValues(values, "--prefix").Select(value =>
        {
            var prefix = Text.Strip(value).ToUpperInvariant();
            return known.Contains(prefix)
                ? prefix
                : throw new UsageException(
                    $"'{prefix}' is not a requirement-ID prefix in appendix C, so no entry can legitimately " +
                    $"cite one. The prefixes are {string.Join(' ', known.Order(StringComparer.Ordinal))}");
        })];

    private static List<string> ParseKeys(IEnumerable<string> values) =>
        [.. SplitValues(values, "--key").Select(value =>
        {
            var key = Text.Strip(value);
            return EntryKey().IsMatch(key)
                ? key
                : throw new UsageException(
                    $"'{key}' is not an entry key; a key is <topic>/<8 hex>, as printed after the section " +
                    "name on every result");
        })];

    // "styleguide 6.7" — the chapter is queryable, the sub-section number is not, so take the chapter
    // and say plainly that the rest was dropped.
    private static List<string> ParseChapters(IEnumerable<string> values, TextWriter warn)
    {
        var chapters = new List<string>();
        foreach (var value in SplitValues(values, "--chapter"))
        {
            var match = ChapterSpec().Match(Text.Strip(value));
            if (!match.Success)
            {
                throw new UsageException($"unknown chapter '{value}'; expected a styleguide chapter like 6 or 6.7");
            }

            if (match.Groups[2].Success)
            {
                warn.Write($"note: entries record a chapter file and line range, not section numbers — querying " +
                           $"chapter {match.Groups[1].Value}, ignoring .{match.Groups[2].Value}. Narrow with bare words.\n");
            }

            chapters.Add(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
        }

        return chapters;
    }

    // An empty pattern matches every entry, exactly as an empty --topic does, so it gets the same
    // refusal rather than printing the corpus.
    private static List<Regex> ParsePatterns(IEnumerable<string> greps, IReadOnlyList<string> words)
    {
        var grepList = greps.ToList();
        var supplied = grepList.Concat(words).ToList();
        if (supplied.Count > 0 && supplied.All(Text.IsBlank))
        {
            throw new UsageException(
                "--grep was given only empty values; an empty pattern matches every entry, so this would " +
                "print the whole corpus.");
        }

        const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var patterns = new List<Regex>();
        foreach (var source in grepList.Where(source => !Text.IsBlank(source)))
        {
            try
            {
                patterns.Add(new Regex(source, Options, TimeSpan.FromSeconds(5)));
            }
            catch (ArgumentException e)
            {
                throw new UsageException($"--grep '{source}' is not a valid regex: {e.Message}", e);
            }
        }

        patterns.AddRange(words.Where(word => !Text.IsBlank(word))
            .Select(word => new Regex(Regex.Escape(word), Options, TimeSpan.FromSeconds(5))));
        return patterns;
    }

    /// <summary>
    /// One dimension on its own, for the zero-result diagnosis: only a filter that matches nothing by
    /// itself gets to explain the empty result.
    /// </summary>
    public Query Solo(Dimension dimension) => dimension switch
    {
        Dimension.Reqs => new Query(reqs: Reqs),
        Dimension.Keys => new Query(keys: Keys),
        Dimension.Prefixes => new Query(prefixes: Prefixes),
        Dimension.Origins => new Query(origins: Origins),
        Dimension.Topics => new Query(topics: Topics),
        Dimension.Roles => new Query(roles: Roles),
        Dimension.Chapters => new Query(chapters: Chapters),
        Dimension.Sections => new Query(sections: Sections),
        _ => new Query(patterns: Patterns),
    };

    public bool Matches(Entry entry)
    {
        if ((Keys.Count > 0 && !Keys.Contains(entry.Key))
            || (Reqs.Count > 0 && !Reqs.Any(entry.Reqs.Contains))
            || (Prefixes.Count > 0 && !entry.Reqs.Any(id => Prefixes.Contains(Ids.PrefixOf(id))))
            || (Topics.Count > 0 && !Topics.Any(topic => entry.File.Contains(topic, StringComparison.Ordinal)))
            || (Origins.Count > 0 && !Origins.Contains(entry.Origin))
            || (Sections.Count > 0 && (entry.Section is null || !Sections.Contains(entry.Section)))
            || (Roles.Count > 0 && !Roles.Any(entry.Roles.Contains)))
        {
            return false;
        }

        if (Chapters.Count > 0)
        {
            var found = entry.Chapters();
            if (!Chapters.Any(found.Contains))
            {
                return false;
            }
        }

        return Patterns.All(pattern => pattern.IsMatch(entry.Text));
    }
}

/// <summary>
/// <c>docs/work/&lt;delivery&gt;/phaseN[/phaseNx]/*.md</c> — a phase's design, plan and checklist.
/// Collecting the requirement IDs they cite turns "what did phase 5a depend on" into one query. A document
/// that writes <c>HTTP-1–HTTP-35</c> rather than thirty-five IDs is credited with all of them
/// (<see cref="Ids.ExtractWithRanges"/>), which is why this takes appendix C and not just its prefixes.
/// </summary>
internal sealed partial class PhaseDocs
{
    private readonly KnowledgePaths _paths;
    private readonly AppendixC _appendix;

    public PhaseDocs(KnowledgePaths paths, AppendixC appendix)
    {
        _paths = paths;
        _appendix = appendix;
    }

    [GeneratedRegex(@"\A([0-9]+)([a-z])?\z", RegexOptions.ECMAScript)]
    private static partial Regex Spec();

    public static string Normalize(string spec)
    {
        var value = Text.Strip(spec).ToLowerInvariant();
        if (value.StartsWith("phase", StringComparison.Ordinal))
        {
            value = value["phase".Length..];
        }

        var match = Spec().Match(value);
        return match.Success
            ? match.Groups[1].Value + match.Groups[2].Value
            : throw new UsageException($"unknown phase '{spec}'; expected a phase like 5 or 5a");
    }

    public List<(string Relative, List<string> Reqs)> Find(string spec)
    {
        var phase = Normalize(spec);
        var match = Spec().Match(phase);
        var number = match.Groups[1].Value;
        var letter = match.Groups[2].Value;
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var delivery in Children(_paths.WorkDir, directories: true))
        {
            if (letter.Length > 0)
            {
                files.UnionWith(Children(Path.Combine(delivery, $"phase{number}", $"phase{number}{letter}"), false));
                files.UnionWith(Children(Path.Combine(delivery, $"phase{number}{letter}"), false));
            }
            else
            {
                var phaseDir = Path.Combine(delivery, $"phase{number}");
                files.UnionWith(Children(phaseDir, false));
                foreach (var sub in Children(phaseDir, directories: true))
                {
                    files.UnionWith(Children(sub, false));
                }
            }
        }

        return [.. files.Select(path => (
            _paths.Relative(path), Ids.ExtractWithRanges(File.ReadAllText(path), _appendix.Prefixes, _appendix.AllIds)))];
    }

    // A shell glob's `*` (directories) or `*.md` (files); like a glob, it skips dot-entries.
    private static IEnumerable<string> Children(string dir, bool directories)
    {
        if (!Directory.Exists(dir))
        {
            return [];
        }

        var entries = directories
            ? Directory.EnumerateDirectories(dir)
            : Directory.EnumerateFiles(dir).Where(path => path.EndsWith(".md", StringComparison.Ordinal));
        return entries.Where(path => !Path.GetFileName(path).StartsWith('.'));
    }
}
