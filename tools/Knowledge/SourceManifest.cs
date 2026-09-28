// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;

namespace Dexpace.Tools.Knowledge;

/// <summary>One manifest row: a source path, its role, and the truncated sha256 recorded at harvest.</summary>
internal sealed record SourceRow(string Path, string Role, string Sha);

/// <summary>
/// <c>harvested/SOURCES.md</c>: every source the corpus was derived from, with the sha256 of the whole
/// file at harvest time.
/// </summary>
internal sealed partial class SourceManifest
{
    // A digest short enough to collide by accident is not a pin.
    public const int MinShaLength = 12;

    private readonly Dictionary<string, SourceRow> _byPath;

    public SourceManifest(List<SourceRow> rows, string path)
    {
        Rows = rows;
        FilePath = path;
        _byPath = new Dictionary<string, SourceRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            _byPath[row.Path] = row;
        }
    }

    // `| `path` | role | `sha` | date |` — the sha cell sometimes carries an annotation after the
    // digest, so take the first backticked token in it.
    [GeneratedRegex(@"\A\|\s*`([^`]+)`\s*\|\s*([^|]*?)\s*\|\s*`([0-9a-f]+)`", RegexOptions.ECMAScript)]
    private static partial Regex Row();

    // Any data row of the manifest table, parseable or not. Counting these is what turns "47 sources OK"
    // from a count of rows that happened to match into a statement about the table.
    [GeneratedRegex(@"\A\|\s*`([^`]+)`\s*\|", RegexOptions.ECMAScript)]
    private static partial Regex AnyRow();

    public List<SourceRow> Rows { get; }

    public string FilePath { get; }

    public static bool Exists(KnowledgePaths paths) => File.Exists(paths.SourcesManifest);

    public static SourceManifest Load(KnowledgePaths paths)
    {
        var path = paths.SourcesManifest;
        try
        {
            return new SourceManifest(Parse(File.ReadAllText(path), path), path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new NotHarvestedException(
                $"cannot read the harvest manifest at {path}; it is written by the knowledge-harvest skill " +
                "alongside harvested/", e);
        }
    }

    public static List<SourceRow> Parse(string text, string path)
    {
        var rows = new List<SourceRow>();
        var listed = 0;
        foreach (var line in text.Split('\n'))
        {
            if (!AnyRow().IsMatch(line))
            {
                continue;
            }

            listed++;
            var match = Row().Match(line);
            if (match.Success)
            {
                rows.Add(new SourceRow(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
            }
        }

        if (rows.Count == 0)
        {
            throw new UsageException($"parsed zero source rows out of {path}; its table format changed");
        }

        if (rows.Count < listed)
        {
            throw new UsageException(
                $"{path} lists {listed} sources but only {rows.Count} parse; the rest carry a malformed " +
                "digest cell and would be silently skipped");
        }

        var weak = rows.FirstOrDefault(row => row.Sha.Length < MinShaLength);
        if (weak is not null)
        {
            throw new UsageException(
                $"{weak.Path} is pinned to a {weak.Sha.Length}-character digest; at least {MinShaLength} " +
                "are needed for the comparison to mean anything");
        }

        return rows;
    }

    public string? ShaFor(string sourcePath) => _byPath.TryGetValue(sourcePath, out var row) ? row.Sha : null;

    /// <summary>
    /// The directories the manifest itself names — the allowlist of source roots the structural gate
    /// checks harvested citations against.
    /// </summary>
    public List<string> Roots() =>
        [.. Rows.Select(row => DirName(row.Path)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    // Ruby's File.dirname, independent of the host's directory separator.
    private static string DirName(string path)
    {
        var slash = path.TrimEnd('/').LastIndexOf('/');
        return slash switch
        {
            < 0 => ".",
            0 => "/",
            _ => path[..slash],
        };
    }
}

/// <summary>
/// A query that touches an entry whose source has changed since the harvest is answering from a stale
/// document. That is a warning, never a failure: drift is normal, and the fix (a re-harvest) is a
/// user-invoked skill.
/// </summary>
internal sealed class DriftCheck
{
    private readonly SourceManifest _manifest;
    private readonly KnowledgePaths _paths;
    private readonly Dictionary<string, string?> _digests = new(StringComparer.Ordinal);

    public DriftCheck(SourceManifest manifest, KnowledgePaths paths)
    {
        _manifest = manifest;
        _paths = paths;
    }

    /// <summary>Null when there is no usable manifest to check against, so the caller stays simple.</summary>
    public static DriftCheck? For(KnowledgePaths paths)
    {
        try
        {
            return SourceManifest.Exists(paths) ? new DriftCheck(SourceManifest.Load(paths), paths) : null;
        }
        catch (Exception e) when (e is UsageException or NotHarvestedException)
        {
            return null;
        }
    }

    /// <summary>
    /// One line per distinct stale source, however many entries derive from it: each source file is
    /// hashed at most once per run.
    /// </summary>
    public List<string> WarningsFor(IEnumerable<Entry> entries)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var source in entries.SelectMany(entry => entry.SourcePaths()))
        {
            if (_manifest.ShaFor(source) is not null)
            {
                counts[source] = counts.GetValueOrDefault(source) + 1;
            }
        }

        var warnings = new List<string>();
        foreach (var (source, count) in counts)
        {
            var recorded = _manifest.ShaFor(source)!;
            var actual = DigestOf(source, recorded.Length);
            if (actual is null || actual == recorded)
            {
                continue;
            }

            warnings.Add($"warning: stale {source} — harvested at sha {recorded}, now {actual}; " +
                         $"{count} of these entries derive from it. Re-harvest it, or read the file.");
        }

        return warnings;
    }

    // A missing file is not drift: a source can be legitimately absent from a checkout (a styleguide
    // harvested from a sibling repository by absolute path, in a port that has not vendored it).
    private string? DigestOf(string source, int width)
    {
        if (!_digests.TryGetValue(source, out var digest))
        {
            try
            {
                digest = KnowledgePaths.DigestOf(_paths.Resolve(source), width);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                digest = null;
            }

            _digests[source] = digest;
        }

        return digest;
    }
}
