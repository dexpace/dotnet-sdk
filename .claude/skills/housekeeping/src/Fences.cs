// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Housekeeping;

/// <summary>One C# code fence of a documentation page.</summary>
/// <param name="Page">The repository-relative path, with forward slashes.</param>
/// <param name="Line">The 1-based line of the opening <c>```csharp</c>; code line <c>n</c> (0-based) is page line <c>Line + 1 + n</c>.</param>
/// <param name="Fragment">The info string carried <c>fragment</c>: counted and reported, never compiled.</param>
/// <param name="Code">The lines between the fences, dedented by the opening fence's indentation.</param>
internal sealed record Fence(string Page, int Line, bool Fragment, IReadOnlyList<string> Code)
{
    /// <summary>The fence's text.</summary>
    public string Text => string.Join('\n', Code);
}

/// <summary>Which fences the <c>fences</c> command reads: every <c>csharp</c> fence naming a <c>Dexpace.Sdk</c> namespace.</summary>
internal static partial class FenceExtractor
{
    private static readonly string[] s_pathspecs = ["README.md", "docs/sdk-documentation/*.md", "src/*/README.md"];

    /// <summary>
    /// Every fence of <c>README.md</c>, <c>docs/sdk-documentation/*.md</c> and <c>src/*/README.md</c> (tracked or
    /// not yet committed) whose text names <c>Dexpace.Sdk</c>, in page then line order.
    /// </summary>
    /// <remarks>
    /// The test is for the NAMESPACE, never for a <c>using</c> line: a fence that writes
    /// <c>Dexpace.Sdk.Core.Http.Request.Request.Get(...)</c> and imports nothing is as much a claim about the API as one
    /// that opens with three <c>using</c> lines, and a matcher keyed on the import would skip exactly the
    /// fences that are easiest to get wrong. A fence with no mention is somebody else's API in an illustrative
    /// snippet; it is not extracted and not counted.
    /// </remarks>
    public static IReadOnlyList<Fence> Extract(Repo repo)
    {
        var pages = repo.Present(s_pathspecs)
            .Where(IsPage)
            .Order(StringComparer.Ordinal);
        return [.. pages.SelectMany(page => Parse(page, repo.Read(page)))];
    }

    /// <summary>The fences of one page's text that name a <c>Dexpace.Sdk</c> namespace.</summary>
    public static IEnumerable<Fence> Parse(string page, string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var open = Opener().Match(lines[i]);
            if (!open.Success)
            {
                continue;
            }

            var ticks = open.Groups["ticks"].Value;
            var indent = open.Groups["indent"].Length;
            var code = new List<string>();
            var j = i + 1;
            while (j < lines.Length && !Closes(lines[j], ticks))
            {
                code.Add(Dedent(lines[j], indent));
                j++;
            }

            var info = open.Groups["info"].Value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            var fence = new Fence(page, i + 1, info.Contains("fragment", StringComparer.Ordinal), code);
            if (info.Length > 0 && info[0] == "csharp" && NamesNamespace().IsMatch(fence.Text))
            {
                yield return fence;
            }

            i = j;
        }
    }

    private static bool IsPage(string path) =>
        path == "README.md" ||
        (path.StartsWith("docs/sdk-documentation/", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal) && path.Count(c => c == '/') == 2) ||
        SourceReadme().IsMatch(path);

    private static bool Closes(string line, string ticks)
    {
        var close = Closer().Match(line);
        return close.Success && close.Groups["ticks"].Value[0] == ticks[0] && close.Groups["ticks"].Length >= ticks.Length;
    }

    private static string Dedent(string line, int indent)
    {
        var strip = 0;
        while (strip < indent && strip < line.Length && line[strip] == ' ')
        {
            strip++;
        }

        return line[strip..];
    }

    [GeneratedRegex(@"^(?<indent> *)(?<ticks>`{3,}|~{3,})(?<info>[^`]*)$")]
    private static partial Regex Opener();

    [GeneratedRegex(@"^ *(?<ticks>`{3,}|~{3,}) *$")]
    private static partial Regex Closer();

    [GeneratedRegex(@"\bDexpace\.Sdk\b")]
    private static partial Regex NamesNamespace();

    [GeneratedRegex(@"^src/[^/]+/README\.md$")]
    private static partial Regex SourceReadme();
}

/// <summary>What a fence needs on top of the built libraries: pinned packages, or one it cannot have.</summary>
/// <param name="Packages">The <c>PackageReference</c>s (id and the version <c>Directory.Packages.props</c> pins).</param>
/// <param name="Missing">Namespaces that belong to a package <c>Directory.Packages.props</c> does not pin.</param>
internal sealed record FenceNeeds(IReadOnlyList<(string Id, string Version)> Packages, IReadOnlyList<string> Missing);

/// <summary>Turns a fence's namespaces into package references, against what <c>Directory.Packages.props</c> pins.</summary>
/// <remarks>
/// The scratch project is outside the repository and inherits nothing, so every version is read from the one place the
/// repository pins them, as text (the tool references no package, and MSBuild is not asked to evaluate a file it
/// would then apply to the throwaway project). Two packages are not the fence's choice: Core's public surface exposes
/// <c>ILogger</c>, and a bare <c>&lt;Reference&gt;</c> to its DLL does not bring the assembly that defines it
/// (<c>CS0012</c>), so the logging abstractions are always referenced; and the host-integration package exposes
/// <c>IServiceCollection</c> and <c>IConfigurationSection</c>, which the one third-party root of its family
/// (<c>Microsoft.Extensions.Http</c>) carries with the rest of its closure.
/// </remarks>
internal sealed partial class FencePackages
{
    /// <summary>The package every scratch project references.</summary>
    public const string Logging = "Microsoft.Extensions.Logging.Abstractions";

    /// <summary>The root whose closure carries dependency injection, options, configuration and logging.</summary>
    public const string Hosting = "Microsoft.Extensions.Http";

    private static readonly string[] s_closure =
    [
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.Diagnostics",
    ];

    private readonly IReadOnlyDictionary<string, string> _pinned;

    private FencePackages(IReadOnlyDictionary<string, string> pinned) => _pinned = pinned;

    /// <summary>The pins of <paramref name="repo"/>'s <c>Directory.Packages.props</c>; none when it has no such file.</summary>
    public static FencePackages Read(Repo repo)
    {
        var pinned = new Dictionary<string, string>(StringComparer.Ordinal);
        if (repo.Exists("Directory.Packages.props"))
        {
            foreach (Match m in PackageVersion().Matches(repo.Read("Directory.Packages.props")))
            {
                pinned[m.Groups["id"].Value] = m.Groups["version"].Value;
            }
        }

        return new FencePackages(pinned);
    }

    /// <summary>Whether the logging abstractions every project references are pinned.</summary>
    public bool PinsLogging => _pinned.ContainsKey(Logging);

    /// <summary>What <paramref name="fence"/> needs beyond the libraries.</summary>
    public FenceNeeds Resolve(Fence fence)
    {
        var text = fence.Text;
        var ids = new List<string> { Logging };
        var missing = new List<string>();
        var hosting = HostIntegration().IsMatch(text);
        foreach (var token in Qualified().Matches(text).Select(m => m.Value).Distinct(StringComparer.Ordinal))
        {
            var (pinned, closure, absent) = Classify(token);
            if (pinned is not null)
            {
                ids.Add(pinned);
            }
            else if (closure)
            {
                hosting = true;
            }
            else if (absent is not null)
            {
                missing.Add(absent);
            }
        }

        if (hosting)
        {
            ids.Add(Hosting);
        }

        var packages = ids.Distinct(StringComparer.Ordinal)
            .Select(id => _pinned.TryGetValue(id, out var version) ? (Id: id, Version: version) : (Id: id, Version: string.Empty))
            .ToList();
        missing.AddRange(packages.Where(p => p.Version.Length == 0).Select(p => p.Id));
        return new FenceNeeds([.. packages.Where(p => p.Version.Length > 0)], [.. missing.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// The pinned package a token falls under; or <c>Closure</c> when it falls in the closure of the hosting root; or the
    /// package it names when that package is not pinned.
    /// </summary>
    /// <remarks>
    /// Longest prefix first, down to the third segment (the package id of <c>Microsoft.Extensions.*</c>): a token may be
    /// a namespace or a fully qualified type, and <c>Microsoft.Extensions.Logging.ILogger</c> has to land on
    /// <c>Microsoft.Extensions.Logging</c>, not on a package called <c>ILogger</c>.
    /// </remarks>
    private (string? Pinned, bool Closure, string? Absent) Classify(string token)
    {
        var segments = token.Split('.');
        var floor = segments[0] == "Microsoft" ? 3 : 1;
        if (segments.Length < floor)
        {
            return (null, false, null);
        }

        for (var length = segments.Length; length >= floor; length--)
        {
            var prefix = string.Join('.', segments.Take(length));
            if (_pinned.ContainsKey(prefix))
            {
                return (prefix, false, null);
            }

            if (s_closure.Contains(prefix, StringComparer.Ordinal))
            {
                return (null, true, null);
            }
        }

        return (null, false, string.Join('.', segments.Take(floor)));
    }

    [GeneratedRegex("<PackageVersion\\s+Include=\"(?<id>[^\"]+)\"\\s+Version=\"(?<version>[^\"]+)\"")]
    private static partial Regex PackageVersion();

    [GeneratedRegex(@"\b(?:Microsoft\.Extensions|OpenTelemetry|Polly)(?:\.[A-Za-z_][A-Za-z0-9_]*)*")]
    private static partial Regex Qualified();

    [GeneratedRegex(@"\bDexpace\.Sdk\.Extensions\.DependencyInjection\b")]
    private static partial Regex HostIntegration();
}

/// <summary>The outcome of a <c>fences</c> run: the four counts and the lines to print.</summary>
internal sealed record FenceReport(int Compiled, int Fragments, int Skipped, int Failed, IReadOnlyList<string> Lines, string? ScratchRoot)
{
    /// <summary>The closing line: <c>n compiled, m fragments, s skipped, k failed</c>.</summary>
    public string Summary => string.Create(
        CultureInfo.InvariantCulture,
        $"{Compiled} compiled, {Fragments} fragments, {Skipped} skipped, {Failed} failed");
}

/// <summary>
/// Compiles the C# fences of the documentation against the built libraries. Writes only into a scratch directory
/// outside the repository.
/// </summary>
/// <remarks>
/// One generated project per fence under a scratch directory that is NOT under the repository, because MSBuild finds
/// <c>Directory.Build.props</c>, <c>Directory.Build.targets</c> and <c>Directory.Packages.props</c> by walking up from the
/// project: anything under the tree would inherit warnings-as-errors with the analyzers, the lock files,
/// <c>NuGetAudit</c>, strong naming and central package management (where a versioned <c>PackageReference</c> is
/// <c>NU1008</c>). Outside it none is found and nothing here has to be kept in step with a future repository property.
/// Every path written goes through <see cref="Guard.AssertWritable"/> all the same, which refuses only the frozen
/// repository paths: it is the repository's convention for a write, not the isolation. The projects are built
/// together, as one generated solution, and each source file starts with a <c>#line</c> directive so a compiler
/// diagnostic names the page and the line.
/// </remarks>
internal sealed partial class FenceCompiler(Repo repo, string? libs = null, string? scratchParent = null)
{
    /// <summary>
    /// An entry point for a fence that declares types and has no statements. A fence WITH top-level statements has global code
    /// as its entry point and the compiler ignores this one (<c>CS7022</c>, switched off in the generated project), so one
    /// rule serves both: a fence is a complete program, or the declarations a reader would add to one.
    /// </summary>
    private const string EntryPoint = "internal static class FenceEntry { private static void Main() { } }\n";

    private static readonly Dictionary<string, string> s_buildEnvironment = new()
    {
        ["DOTNET_NOLOGO"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
        ["MSBUILDDISABLENODEREUSE"] = "1",
    };

    /// <summary>Extracts, classifies and compiles; returns the report. Throws <see cref="ArgumentException"/> for a usage problem.</summary>
    public FenceReport Run()
    {
        var packages = FencePackages.Read(repo);
        var lines = new SortedDictionary<(string Page, int Line), List<string>>();
        var compile = new List<(Fence Fence, FenceNeeds Needs)>();
        var fragments = 0;
        var skipped = 0;
        foreach (var fence in FenceExtractor.Extract(repo))
        {
            var key = (fence.Page, fence.Line);
            if (fence.Fragment)
            {
                fragments++;
                lines[key] = ["fragment"];
                continue;
            }

            var needs = packages.Resolve(fence);
            var absent = needs.Missing.Where(id => id != FencePackages.Logging).ToList();
            if (absent.Count > 0)
            {
                skipped++;
                lines[key] = [$"skipped: needs {string.Join(", ", absent)}"];
                continue;
            }

            compile.Add((fence, needs));
        }

        if (compile.Count == 0)
        {
            return Render(0, fragments, skipped, 0, lines, null);
        }

        if (!packages.PinsLogging)
        {
            throw new ArgumentException($"Directory.Packages.props does not pin {FencePackages.Logging}, which every generated project references");
        }

        var built = Build(compile);
        foreach (var (index, diagnostics) in built.Failures)
        {
            foreach (var (line, message) in diagnostics)
            {
                var key = (compile[index].Fence.Page, line);
                if (!lines.TryGetValue(key, out var messages))
                {
                    lines[key] = messages = [];
                }

                messages.Add(message);
            }
        }

        return Render(compile.Count - built.Failures.Count, fragments, skipped, built.Failures.Count, lines, built.ScratchRoot);
    }

    private static FenceReport Render(
        int compiled,
        int fragments,
        int skipped,
        int failed,
        SortedDictionary<(string Page, int Line), List<string>> lines,
        string? scratchRoot) =>
        new(
            compiled,
            fragments,
            skipped,
            failed,
            [.. lines.SelectMany(entry => entry.Value.Select(message => $"{entry.Key.Page}:{entry.Key.Line}: [fence] {message}"))],
            scratchRoot);

    private (Dictionary<int, List<(int Line, string Message)>> Failures, string ScratchRoot) Build(
        IReadOnlyList<(Fence Fence, FenceNeeds Needs)> compile)
    {
        var dlls = ResolveLibraries();
        var scratch = CreateScratch();
        try
        {
            Populate(scratch, compile, dlls);
            var result = Dotnet(scratch, ["build", "fences.slnx", "-nologo", "-v:q", "-clp:NoSummary", "-nr:false", "--disable-build-servers"]);
            var failures = result.ExitCode == 0 ? [] : Diagnose(result.Output + result.Error, compile);
            return (failures, scratch);
        }
        finally
        {
            TryDelete(scratch);
        }
    }

    private string CreateScratch()
    {
        if (scratchParent is null)
        {
            return Directory.CreateTempSubdirectory("dexpace-fences-").FullName;
        }

        var path = Guard.AssertWritable(Path.Combine(scratchParent, "dexpace-fences-" + Guid.NewGuid().ToString("N")[..8]), repo.Root);
        Directory.CreateDirectory(path);
        return path;
    }

    private void Populate(string scratch, IReadOnlyList<(Fence Fence, FenceNeeds Needs)> compile, IReadOnlyList<string> dlls)
    {
        var solution = new StringBuilder("<Solution>\n");
        for (var i = 0; i < compile.Count; i++)
        {
            var name = ProjectName(i);
            var (fence, needs) = compile[i];
            Write(Path.Combine(scratch, name, name + ".csproj"), Csproj(needs, dlls));
            Write(Path.Combine(scratch, name, "Program.cs"), $"#line {fence.Line + 1} \"{fence.Page}\"\n{fence.Text}\n");
            Write(Path.Combine(scratch, name, "Entry.cs"), EntryPoint);
            solution.Append(CultureInfo.InvariantCulture, $"  <Project Path=\"{name}/{name}.csproj\" />\n");
        }

        solution.Append("</Solution>\n");
        Write(Path.Combine(scratch, "fences.slnx"), solution.ToString());
        if (repo.Exists("global.json"))
        {
            // The SDK the repository pins, not whichever is newest on the machine: the scratch directory has no global.json of its own.
            Write(Path.Combine(scratch, "global.json"), repo.Read("global.json"));
        }
    }

    private void Write(string path, string text)
    {
        var guarded = Guard.AssertWritable(path, repo.Root);
        Directory.CreateDirectory(Path.GetDirectoryName(guarded)!);
        File.WriteAllText(guarded, text);
    }

    private static string ProjectName(int index) => string.Create(CultureInfo.InvariantCulture, $"fence-{index:D4}");

    private static string Csproj(FenceNeeds needs, IReadOnlyList<string> dlls)
    {
        var references = string.Concat(dlls.Select(dll =>
            $"    <Reference Include=\"{Path.GetFileNameWithoutExtension(dll)}\">\n      <HintPath>{SecurityElement.Escape(dll)}</HintPath>\n      <Private>false</Private>\n    </Reference>\n"));
        var packages = string.Concat(needs.Packages.Select(p => $"    <PackageReference Include=\"{p.Id}\" Version=\"{p.Version}\" />\n"));
        return "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
               "  <PropertyGroup>\n" +
               "    <OutputType>Exe</OutputType>\n" +
               "    <TargetFramework>net10.0</TargetFramework>\n" +
               "    <Nullable>enable</Nullable>\n" +
               "    <ImplicitUsings>enable</ImplicitUsings>\n" +
               "    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n" +
               "    <NuGetAudit>false</NuGetAudit>\n" +
               "    <NoWarn>$(NoWarn);CS7022</NoWarn>\n" +
               "    <UseAppHost>false</UseAppHost>\n" +
               "    <InvariantGlobalization>true</InvariantGlobalization>\n" +
               "  </PropertyGroup>\n" +
               $"  <ItemGroup>\n{references}  </ItemGroup>\n" +
               $"  <ItemGroup>\n{packages}  </ItemGroup>\n" +
               "</Project>\n";
    }

    /// <summary>The five built libraries: under <c>--libs</c>, else each <c>src/&lt;Id&gt;/bin/Release/net10.0/&lt;Id&gt;.dll</c>.</summary>
    private List<string> ResolveLibraries()
    {
        const string BuildHint = "build the libraries first: dotnet build Dexpace.Sdk.sln --configuration Release";
        if (libs is not null)
        {
            var given = Directory.Exists(libs) ? Directory.EnumerateFiles(libs, "Dexpace.Sdk.*.dll").Order(StringComparer.Ordinal).ToList() : [];
            return given.Count > 0
                ? given
                : throw new FenceLibrariesMissingException($"no Dexpace.Sdk.*.dll under {libs}; {BuildHint}");
        }

        var wanted = repo.Children("src").Where(name => name.StartsWith("Dexpace.Sdk.", StringComparison.Ordinal))
            .Select(name => repo.Abs(Path.Combine("src", name, "bin", "Release", "net10.0", name + ".dll")))
            .ToList();
        var absent = wanted.Where(dll => !File.Exists(dll)).ToList();
        return wanted.Count > 0 && absent.Count == 0
            ? wanted
            : throw new FenceLibrariesMissingException(
                $"{(wanted.Count == 0 ? "no library under src/" : "not built: " + string.Join(", ", absent.Select(Path.GetFileName)))}; {BuildHint}");
    }

    /// <summary>The compiler's errors per fence index, each with the page line it names.</summary>
    private static Dictionary<int, List<(int Line, string Message)>> Diagnose(string log, IReadOnlyList<(Fence Fence, FenceNeeds Needs)> compile)
    {
        var failures = new Dictionary<int, List<(int Line, string Message)>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in log.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var project = ProjectOf().Match(line);
            if (!project.Success || !seen.Add(line))
            {
                continue;
            }

            var index = int.Parse(project.Groups["index"].Value, CultureInfo.InvariantCulture);
            if (index >= compile.Count)
            {
                continue;
            }

            var fence = compile[index].Fence;
            var diagnostic = Located().Match(line);
            var unlocated = Unlocated().Match(line);
            if (!diagnostic.Success && !unlocated.Success)
            {
                continue;
            }

            var match = diagnostic.Success ? diagnostic : unlocated;
            var pageLine = diagnostic.Success ? int.Parse(diagnostic.Groups["line"].Value, CultureInfo.InvariantCulture) : fence.Line;
            var message = $"{match.Groups["code"].Value}: {match.Groups["message"].Value.Trim()}";
            if (!failures.TryGetValue(index, out var messages))
            {
                failures[index] = messages = [];
            }

            messages.Add((pageLine, message));
        }

        return failures.Count > 0 ? failures : Unattributed(log, compile);
    }

    /// <summary>A failed build that names no fence still fails every fence: nothing is reported compiled that was not.</summary>
    private static Dictionary<int, List<(int Line, string Message)>> Unattributed(
        string log,
        IReadOnlyList<(Fence Fence, FenceNeeds Needs)> compile)
    {
        var tail = log.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).TakeLast(5);
        var summary = "dotnet build failed with no diagnostic naming this fence: " + string.Join(" | ", tail);
        return Enumerable.Range(0, compile.Count).ToDictionary(i => i, i => new List<(int Line, string Message)> { (compile[i].Fence.Line, summary) });
    }

    private static (int ExitCode, string Output, string Error) Dotnet(string workingDirectory, IEnumerable<string> args)
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        var info = new ProcessStartInfo(string.IsNullOrEmpty(host) ? "dotnet" : host)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in s_buildEnvironment)
        {
            info.Environment[key] = value;
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("could not start dotnet");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error.GetAwaiter().GetResult());
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A temp directory: leaving it behind is a nuisance, not a result.
        }
    }

    [GeneratedRegex(@"fence-(?<index>[0-9]{4})")]
    private static partial Regex ProjectOf();

    [GeneratedRegex(@"\((?<line>[0-9]+),[0-9]+(?:,[0-9]+,[0-9]+)?\): error (?<code>[A-Za-z]+[0-9]+): (?<message>.*?)(?: \[[^\]]+\])?\s*$")]
    private static partial Regex Located();

    [GeneratedRegex(@": error (?<code>[A-Za-z]+[0-9]+): (?<message>.*?)(?: \[[^\]]+\])?\s*$")]
    private static partial Regex Unlocated();
}

/// <summary>The built libraries a compile needs are not there. A message, not a skip.</summary>
internal sealed class FenceLibrariesMissingException(string message) : Exception(message);

/// <summary>The <c>fences</c> command line.</summary>
internal static class FencesCli
{
    /// <summary>The usage text.</summary>
    public const string Usage =
        "usage: dotnet run --project .claude/skills/housekeeping/src -- fences [--root PATH] [--libs DIR]\n" +
        "  compiles every ```csharp fence of README.md, docs/sdk-documentation/*.md and src/*/README.md that names\n" +
        "  Dexpace.Sdk against the built libraries (warnings are errors); ```csharp fragment is counted, never compiled\n" +
        "  --root PATH   check this tree instead of the enclosing repository\n" +
        "  --libs DIR    a directory holding the built Dexpace.Sdk.*.dll files, instead of src/<Id>/bin/Release/net10.0/\n" +
        "  -h, --help    this message";

    /// <summary>0 when every fence compiles or is a fragment or is skipped, 1 on any failure, 2 on a usage error.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            string? root = null;
            string? libs = null;
            var reader = new ArgumentReader(args);
            while (reader.Next() is { } arg)
            {
                switch (arg.Name)
                {
                    case "--root":
                        root = reader.Value(arg);
                        break;
                    case "--libs":
                        libs = reader.Value(arg);
                        break;
                    case "-h" or "--help":
                        output.WriteLine(Usage);
                        return 0;
                    default:
                        throw new ArgumentException($"invalid option: {arg.Name}\n{Usage}");
                }
            }

            var report = new FenceCompiler(new Repo(root ?? Git.RepositoryRoot()), libs).Run();
            foreach (var line in report.Lines)
            {
                output.WriteLine(line);
            }

            output.WriteLine(report.Summary);
            return report.Failed == 0 ? 0 : 1;
        }
        catch (ArgumentException e)
        {
            error.WriteLine(e.Message);
            return 2;
        }
        catch (Exception e) when (e is FenceLibrariesMissingException or FrozenPathException)
        {
            error.WriteLine($"refusing: {e.Message}");
            return 1;
        }
    }
}
