// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Housekeeping.Tests;

/// <summary>
/// Builds a throwaway repository the probe and the apply stage can be pointed at, so a test can
/// assert a check FIRES rather than asserting the live tree happens to be clean.
/// </summary>
/// <remarks>
/// The distinction is not academic. A suite that only asserts the real repository is clean passes
/// just as happily over a check whose body has become <c>[]</c> — and the real repository is clean
/// most of the time, so the suite would be green for years while the checks rotted.
/// <c>git init</c> runs here because the tree is a temp directory that this class also deletes.
/// </remarks>
internal sealed class Fixture : IDisposable
{
    /// <summary>
    /// The counts the fixture's own documents state, and which its tree must satisfy: two shipped
    /// projects, one phase directory, one harvested topic.
    /// </summary>
    public const string CleanClaims =
        "Two shipped projects live under `src/`, both published to NuGet.\n\n" +
        "One phase directory under docs/work/ so far, and one harvested topic in the corpus.\n";

    private static readonly Dictionary<string, string> GitEnvironment = new()
    {
        ["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
        ["GIT_CONFIG_SYSTEM"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
    };

    private Fixture(string root) => Root = root;

    /// <summary>The fixture's absolute, symlink-free root.</summary>
    public string Root { get; }

    /// <summary>
    /// A committed clean tree. <paramref name="overrides"/> replaces or adds files before the commit;
    /// a <c>null</c> value deletes. <paramref name="untracked"/> is written after the commit, so it stays
    /// untracked.
    /// </summary>
    public static Fixture Create(
        Dictionary<string, string?>? overrides = null,
        Dictionary<string, string>? untracked = null)
    {
        var root = Guard.RealPath(Directory.CreateTempSubdirectory("housekeeping-fixture-").FullName);
        var fixture = new Fixture(root);
        foreach (var (path, text) in CleanTree())
        {
            Write(root, path, text);
        }

        foreach (var (path, text) in overrides ?? new Dictionary<string, string?>())
        {
            if (text is null)
            {
                Delete(Path.Combine(root, path));
            }
            else
            {
                Write(root, path, text);
            }
        }

        Git(root, "init", "-q");
        Git(root, "add", "-A");
        Git(root, "commit", "-qm", "fixture");
        foreach (var (path, text) in untracked ?? new Dictionary<string, string>())
        {
            Write(root, path, text);
        }

        return fixture;
    }

    /// <summary>A probe over this fixture.</summary>
    public IReadOnlyList<Finding> Probe(params string[] only) => new Probe(new Repo(Root), only).Run();

    /// <summary>Writes <paramref name="text"/> to a repository-relative path, creating directories.</summary>
    public static string Write(string root, string path, string text)
    {
        var absolute = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, text);
        return absolute;
    }

    /// <summary>Creates a symlink at <paramref name="link"/> pointing at <paramref name="target"/>, both repository-relative.</summary>
    public static void Symlink(string root, string target, string link)
    {
        var absolute = Path.Combine(root, link);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        Directory.CreateSymbolicLink(absolute, Path.Combine(root, target));
    }

    /// <summary>Deletes a file or a directory tree, whichever is there.</summary>
    public static void Delete(string absolute)
    {
        if (new FileInfo(absolute).LinkTarget is not null || File.Exists(absolute))
        {
            File.Delete(absolute);
        }
        else if (Directory.Exists(absolute))
        {
            Directory.Delete(absolute, recursive: true);
        }
    }

    /// <summary>Runs git with a hermetic configuration, throwing on failure.</summary>
    public static string Git(string root, params string[] args)
    {
        var result = Housekeeping.Git.Run(
            root,
            ["-c", "user.email=fixture@example.invalid", "-c", "user.name=fixture", "-c", "commit.gpgsign=false",
             "-c", "init.defaultBranch=main", .. args],
            GitEnvironment);
        return result.Success
            ? result.Output
            : throw new InvalidOperationException($"git {string.Join(' ', args)} failed in {root}: {result.Error}{result.Output}");
    }

    /// <summary><c>git status --porcelain</c>, for read-only assertions.</summary>
    public string Status() => Git(Root, "status", "--porcelain");

    /// <summary>A packable project file declaring <paramref name="packageId"/>.</summary>
    public static string Csproj(string packageId, bool packable = true, bool readme = true) =>
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n" +
        $"    <PackageId>{packageId}</PackageId>\n" +
        (packable ? string.Empty : "    <IsPackable>false</IsPackable>\n") +
        (readme ? "    <PackageReadmeFile>README.md</PackageReadmeFile>\n" : string.Empty) +
        "  </PropertyGroup>\n</Project>\n";

    /// <summary>A README long enough to clear the thin-README floor, headed by <paramref name="name"/>.</summary>
    public static string Readme(string name) =>
        $"# {name}\n\n" +
        string.Join("\n", Enumerable.Range(1, 24).Select(n => $"Line {n} of a README long enough to clear the floor.")) +
        "\n";

    /// <inheritdoc/>
    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            // git objects are read-only there, and Directory.Delete refuses read-only files.
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }

        Directory.Delete(Root, recursive: true);
    }

    private static Dictionary<string, string> CleanTree() => new()
    {
        ["README.md"] = $"# fixture\n\n{CleanClaims}",
        ["CLAUDE.md"] = $"# CLAUDE.md\n\n{CleanClaims}\nSee [docs](docs/README.md).\n",
        ["docs/README.md"] = $"# `docs/`\n\n{CleanClaims}\nThe index. See [the inbox](superpowers/README.md).\n",
        // No register: this repository has never had one, so a clean tree cites none; a test that
        // needs one invents its own prefix and file.
        ["docs/superpowers/README.md"] = "# the inbox\n\nNew documents land here and do not stay.\n",
        ["docs/superpowers/specs/.gitkeep"] = string.Empty,
        ["docs/superpowers/plans/.gitkeep"] = string.Empty,
        ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = "# phase 1\n\nSatisfies task 3 of the plan.\n",
        ["docs/architecture.md"] = "# architecture\n\nAs built.\n",
        ["docs/knowledge/harvested/documentation.md"] = "# documentation\n\nHarvested.\n",
        ["docs/knowledge/harvested/INDEX.md"] = "# index\n\nNot a topic.\n",
        ["docs/knowledge/notes/pagination.md"] = "# pagination\n\nHand written.\n",
        ["src/Dexpace.Sdk.Core/Dexpace.Sdk.Core.csproj"] = Csproj("Dexpace.Sdk.Core"),
        ["src/Dexpace.Sdk.Core/README.md"] = Readme("Dexpace.Sdk.Core"),
        ["src/Dexpace.Sdk.Http.SystemNet/Dexpace.Sdk.Http.SystemNet.csproj"] = Csproj("Dexpace.Sdk.Http.SystemNet"),
        ["src/Dexpace.Sdk.Http.SystemNet/README.md"] = Readme("Dexpace.Sdk.Http.SystemNet"),
    };
}
