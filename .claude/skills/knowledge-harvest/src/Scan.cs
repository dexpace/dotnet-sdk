// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KnowledgeHarvest;

/// <summary>One measured file.</summary>
internal sealed record FileStat(
    string Path,
    string Root,
    string Role,
    string Dir,
    long Bytes,
    string Sha256,
    int? SectionLevel,
    List<Section> Sections);

/// <summary>A directory's file count and total size.</summary>
internal sealed record DirStat(string Path, int Files, long Bytes);

/// <summary>The few largest files of a root, with their section shape.</summary>
internal sealed record LargeFile(string Path, long Bytes, int Sections, int? SectionLevel, int MaxSectionBytes);

/// <summary>One root's summary.</summary>
internal sealed record RootStat(
    string Path,
    string Role,
    int Files,
    long Bytes,
    long MedianFileBytes,
    long MaxFileBytes,
    List<DirStat> Dirs,
    List<LargeFile> LargestFiles);

/// <summary>The totals block of a stats document.</summary>
internal sealed record StatsTotals(int Files, long Bytes);

/// <summary>The output of <c>stats</c>.</summary>
internal sealed record Stats(string Generated, List<RootStat> Roots, List<FileStat> Files, StatsTotals Totals);

/// <summary>The author's routing for one root.</summary>
internal sealed record RootRouting(
    string Path,
    long Budget,
    long TierThreshold = 15000,
    bool Split = true,
    string? Role = null);

/// <summary>The author's routing document.</summary>
internal sealed record Routing(List<RootRouting> Roots);

/// <summary>A file or line range inside a chunk. <c>Lines</c> is null for the whole file.</summary>
internal sealed record ChunkFile(string Path, int[]? Lines, long Bytes, string Sha256);

/// <summary>One extractor assignment.</summary>
internal sealed record Chunk(string Id, string Tier, string Role, long Bytes, List<ChunkFile> Files);

/// <summary>A root as the manifest lists it.</summary>
internal sealed record ManifestRoot(string Path, string Role);

/// <summary>The totals block of a manifest.</summary>
internal sealed record ManifestTotals(int Chunks, long Bytes, int Haiku, int Sonnet);

/// <summary>The output of <c>pack</c>.</summary>
internal sealed record Manifest(string Generated, List<ManifestRoot> Roots, List<Chunk> Chunks, ManifestTotals Totals);

/// <summary>Measures a corpus and packs it into extractor chunks. Neither step makes a judgment call.</summary>
internal static partial class Scan
{
    private const int BinarySniffBytes = 8192;

    private static readonly HashSet<string> s_skipDirs =
    [
        ".git", "node_modules", "dist", "build", "__pycache__", ".venv", "venv", ".mypy_cache",
        ".pytest_cache", ".next", "target", "bin", "obj",
    ];

    private static readonly string[] s_skipFilePatterns =
        ["*.lock", "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "poetry.lock", "Cargo.lock", "packages.lock.json"];

    private static readonly string[] s_markdownSuffixes = [".md", ".markdown", ".mdx"];

    /// <summary>Thrown when a path is missing or nothing eligible was found; maps to exit 2.</summary>
    public sealed class ScanException(string message) : Exception(message);

    /// <summary>The path as provenance shows it: relative to the working directory when under it.</summary>
    public static string DisplayPath(string path)
    {
        var absolute = System.IO.Path.GetFullPath(path);
        var relative = System.IO.Path.GetRelativePath(Directory.GetCurrentDirectory(), absolute);
        return relative.StartsWith("..", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(relative)
            ? absolute
            : relative.Replace('\\', '/');
    }

    /// <summary>Lower-case hex SHA-256 of a file's bytes.</summary>
    public static string Sha256Of(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    /// <summary>An fnmatch-style glob (<c>*</c>, <c>?</c>, <c>[...]</c>) tested against a whole string.</summary>
    public static bool GlobMatches(string pattern, string text)
    {
        var regex = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var c = pattern[index];
            switch (c)
            {
                case '*':
                    regex.Append(".*");
                    break;
                case '?':
                    regex.Append('.');
                    break;
                case '[':
                    var close = pattern.IndexOf(']', index + 2);
                    if (close < 0)
                    {
                        regex.Append(@"\[");
                        break;
                    }

                    var body = pattern[(index + 1)..close];
                    regex.Append('[').Append(body.StartsWith('!') ? "^" + body[1..] : body).Append(']');
                    index = close;
                    break;
                default:
                    regex.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        return Regex.IsMatch(text, regex.Append('$').ToString(), RegexOptions.Singleline, TimeSpan.FromSeconds(2));
    }

    private static bool Excluded(string relative, IReadOnlyList<string> patterns)
    {
        var name = System.IO.Path.GetFileName(relative);
        return patterns.Any(pattern => GlobMatches(pattern, relative) || GlobMatches(pattern, name));
    }

    private static bool IsBinary(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[BinarySniffBytes];
        var read = stream.Read(buffer, 0, buffer.Length);
        return Array.IndexOf(buffer, (byte)0, 0, read) >= 0;
    }

    private static FileStat? Record(string path, string root, string role, string relative, IReadOnlyList<string> patterns)
    {
        var name = System.IO.Path.GetFileName(path);
        if (Excluded(relative, patterns) || s_skipFilePatterns.Any(p => GlobMatches(p, name)) || IsBinary(path))
        {
            return null;
        }

        var markdown = s_markdownSuffixes.Any(s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase));
        int? level = null;
        var sections = new List<Section>();
        if (markdown)
        {
            var (chosen, parsed) = Sections.Choose(File.ReadAllText(path, Encoding.UTF8));
            level = chosen;
            sections = parsed;
        }

        return new FileStat(
            DisplayPath(path),
            DisplayPath(root),
            role,
            DisplayPath(System.IO.Path.GetDirectoryName(path) ?? root),
            new FileInfo(path).Length,
            Sha256Of(path),
            level,
            sections);
    }

    /// <summary>Walks one root; a root that is a single file is measured alone.</summary>
    public static List<FileStat> CollectFiles(string root, string role, IReadOnlyList<string> patterns)
    {
        root = System.IO.Path.GetFullPath(root);
        if (File.Exists(root))
        {
            return Record(root, root, role, System.IO.Path.GetFileName(root), patterns) is { } single ? [single] : [];
        }

        if (!Directory.Exists(root))
        {
            throw new ScanException($"path does not exist: {root}");
        }

        var collected = new List<FileStat>();
        Walk(root);
        return collected;

        void Walk(string directory)
        {
            foreach (var file in Directory.GetFiles(directory).OrderBy(f => f, StringComparer.Ordinal))
            {
                var relative = System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
                if (Record(file, root, role, relative, patterns) is { } stat)
                {
                    collected.Add(stat);
                }
            }

            foreach (var child in Directory.GetDirectories(directory).OrderBy(d => d, StringComparer.Ordinal))
            {
                var name = System.IO.Path.GetFileName(child);
                if (!s_skipDirs.Contains(name) && !name.StartsWith(".git", StringComparison.Ordinal))
                {
                    Walk(child);
                }
            }
        }
    }

    /// <summary>Measures every root: sizes, hashes and heading offsets. No budget, no opinion.</summary>
    public static Stats BuildStats(IReadOnlyList<(string Path, string Role)> roots, IReadOnlyList<string> patterns)
    {
        var all = new List<FileStat>();
        var summaries = new List<RootStat>();
        foreach (var (path, role) in roots)
        {
            var files = CollectFiles(path, role, patterns);
            all.AddRange(files);
            var sizes = files.Select(f => f.Bytes).Order().ToList();
            var dirs = files.GroupBy(f => f.Dir)
                .Select(g => new DirStat(g.Key, g.Count(), g.Sum(f => f.Bytes)))
                .OrderBy(d => d.Path, StringComparer.Ordinal)
                .ToList();
            var largest = files.OrderByDescending(f => f.Bytes)
                .Take(5)
                .Select(f => new LargeFile(
                    f.Path, f.Bytes, f.Sections.Count, f.SectionLevel, f.Sections.Select(s => s.Bytes).DefaultIfEmpty(0).Max()))
                .ToList();
            summaries.Add(new RootStat(
                DisplayPath(System.IO.Path.GetFullPath(path)),
                role,
                files.Count,
                sizes.Sum(),
                Median(sizes),
                sizes.Count == 0 ? 0 : sizes[^1],
                dirs,
                largest));
        }

        return new Stats(Timestamp(), summaries, all, new StatsTotals(all.Count, all.Sum(f => f.Bytes)));
    }

    private static long Median(List<long> sorted)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static string Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Packs a file's sections into groups no larger than the budget. A section larger than the budget
    /// alone becomes its own group: splitting mid-section would break provenance.
    /// </summary>
    public static List<List<Section>> SplitFile(FileStat file, long budget)
    {
        var groups = new List<List<Section>>();
        var current = new List<Section>();
        long size = 0;
        foreach (var section in file.Sections)
        {
            if (current.Count > 0 && size + section.Bytes > budget)
            {
                groups.Add(current);
                current = [];
                size = 0;
            }

            current.Add(section);
            size += section.Bytes;
            if (size >= budget)
            {
                groups.Add(current);
                current = [];
                size = 0;
            }
        }

        if (current.Count > 0)
        {
            groups.Add(current);
        }

        return groups;
    }

    /// <summary>Applies the author's routing to measured stats. Pure arithmetic; chunks never span directories.</summary>
    public static Manifest Pack(Stats stats, Routing routing)
    {
        var rules = routing.Roots.ToDictionary(r => r.Path, StringComparer.Ordinal);
        var missing = stats.Roots.Select(r => r.Path).Where(p => !rules.ContainsKey(p)).Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new ScanException("routing is missing these roots present in stats: " + string.Join(", ", missing));
        }

        var chunks = new List<Chunk>();
        var groups = stats.Files
            .GroupBy(f => (f.Root, f.Dir))
            .OrderBy(g => g.Key.Root, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Dir, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var rule = rules[group.Key.Root];
            var role = rule.Role ?? stats.Roots.First(r => r.Path == group.Key.Root).Role;
            PackDirectory(group.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(), rule, role, chunks);
        }

        return new Manifest(
            Timestamp(),
            stats.Roots.Select(r => new ManifestRoot(r.Path, r.Role)).ToList(),
            chunks,
            new ManifestTotals(chunks.Count, chunks.Sum(c => c.Bytes), chunks.Count(c => c.Tier == "haiku"), chunks.Count(c => c.Tier == "sonnet")));
    }

    private static void PackDirectory(List<FileStat> files, RootRouting rule, string role, List<Chunk> chunks)
    {
        var current = new List<ChunkFile>();
        long size = 0;

        void Emit(List<ChunkFile> emitted)
        {
            if (emitted.Count == 0)
            {
                return;
            }

            var bytes = emitted.Sum(f => f.Bytes);
            chunks.Add(new Chunk($"c{chunks.Count + 1:00}", bytes < rule.TierThreshold ? "haiku" : "sonnet", role, bytes, emitted));
        }

        foreach (var file in files)
        {
            var whole = new ChunkFile(file.Path, null, file.Bytes, file.Sha256);
            if (file.Bytes > rule.Budget)
            {
                Emit(current);
                current = [];
                size = 0;
                if (rule.Split && file.Sections.Count > 0)
                {
                    foreach (var part in SplitFile(file, rule.Budget))
                    {
                        Emit([new ChunkFile(file.Path, [part[0].Lines[0], part[^1].Lines[1]], part.Sum(s => (long)s.Bytes), file.Sha256)]);
                    }
                }
                else
                {
                    Emit([whole]);
                }

                continue;
            }

            if (current.Count > 0 && size + file.Bytes > rule.Budget)
            {
                Emit(current);
                current = [];
                size = 0;
            }

            current.Add(whole);
            size += file.Bytes;
        }

        Emit(current);
    }
}
