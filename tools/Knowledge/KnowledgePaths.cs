// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;

namespace Dexpace.Tools.Knowledge;

/// <summary>
/// Where everything lives, relative to one root. <c>--root</c> exists so the CLI can be pointed at
/// another checkout (a sibling SDK's populated corpus) for a comparison run, and so the tests can run
/// against a fixture tree.
/// </summary>
internal sealed class KnowledgePaths
{
    public const string EnvRoot = "KNOWLEDGE_ROOT";

    // The file that marks the repository root: the tool's own project, found by walking up from
    // wherever the build put the executable. `dotnet run --project tools/Knowledge` therefore works
    // from any working directory, as the Ruby script did by resolving the parent of scripts/.
    private const string Marker = "tools/Knowledge/Knowledge.csproj";

    public KnowledgePaths(string root)
    {
        var full = Path.GetFullPath(root);
        Root = full.Length > 1 ? full.TrimEnd('/', '\\') : full;
    }

    public string Root { get; }

    public string KnowledgeDir => Path.Combine(Root, "docs", "knowledge");

    public string HarvestedDir => Path.Combine(KnowledgeDir, "harvested");

    public string NotesDir => Path.Combine(KnowledgeDir, "notes");

    public string SourcesManifest => Path.Combine(HarvestedDir, "SOURCES.md");

    public string TopicIndex => Path.Combine(HarvestedDir, "INDEX.md");

    public string ProductSpecDir => Path.Combine(Root, "docs", "product-spec");

    public string WorkDir => Path.Combine(Root, "docs", "work");

    public string AppendixC =>
        Path.Combine(ProductSpecDir, "appendix-c-consolidated-normative-requirement-index.md");

    /// <summary><c>KNOWLEDGE_ROOT</c>, else the checkout the executable was built from, else the cwd.</summary>
    public static KnowledgePaths Default()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvRoot);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return new KnowledgePaths(fromEnv);
        }

        return new KnowledgePaths(FindRoot(AppContext.BaseDirectory)
            ?? FindRoot(Directory.GetCurrentDirectory())
            ?? Directory.GetCurrentDirectory());
    }

    private static string? FindRoot(string start)
    {
        // Bounded: a filesystem is never deeper than this, and the walk must terminate regardless.
        var dir = new DirectoryInfo(start);
        for (var depth = 0; dir is not null && depth < 64; depth++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, Marker)))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// An absolute source path (a styleguide harvested from a sibling repository) is used as given;
    /// everything else is repo-relative. "Absolute" is the host's notion (<c>/x</c>, <c>C:\x</c>, <c>\\host\x</c>).
    /// </summary>
    public string Resolve(string source) =>
        Path.IsPathRooted(source) ? source : Path.Combine(Root, source);

    /// <summary>
    /// <paramref name="path"/> relative to <see cref="Root"/>, with <c>/</c> separators on every host, since
    /// that is how the corpus and every printed command spell paths; a path outside the root is returned as given.
    /// </summary>
    public string Relative(string path) => RelativeTo(Root, path, Path.DirectorySeparatorChar);

    /// <summary>
    /// The separator-explicit core of <see cref="Relative"/>, so a host with one separator can test the other's
    /// paths. Both <paramref name="separator"/> and <c>/</c> separate segments (Windows accepts either).
    /// </summary>
    internal static string RelativeTo(string root, string path, char separator)
    {
        var normalRoot = root.Replace(separator, '/').TrimEnd('/') + "/";
        var normalPath = path.Replace(separator, '/');
        return normalPath.StartsWith(normalRoot, StringComparison.Ordinal) ? normalPath[normalRoot.Length..] : path;
    }

    /// <summary>The sha256 of a whole file, lower-case hex, truncated to <paramref name="width"/>.</summary>
    public static string DigestOf(string path, int width)
    {
        using var stream = File.OpenRead(path);
        var hex = Convert.ToHexStringLower(SHA256.HashData(stream));
        return hex[..Math.Min(width, hex.Length)];
    }
}
