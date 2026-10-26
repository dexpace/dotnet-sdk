// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Housekeeping.Tests;

/// <summary>
/// Tests the <c>fences</c> command: which fences it picks, what it compiles them against, and that it
/// writes nothing in the repository. Phase 12, P12-18.
/// </summary>
/// <remarks>
/// A fixture pair for every rule, as the suite's convention is. The tests that compile a fence build a
/// real generated project against the real <c>src/*/bin/Release/net10.0/*.dll</c>, so the solution has to be
/// built in Release first (<c>dotnet build Dexpace.Sdk.sln --configuration Release</c>); they fail with a
/// message saying so, they never skip, because a skipped compile test is a gate that reports green without
/// having compiled anything.
/// </remarks>
public sealed class FencesTests
{
    private const string Page = "docs/sdk-documentation/demo.md";

    private static readonly Lazy<string> s_libs = new(CopyLiveLibraries);

    // --- helpers -------------------------------------------------------------------------------

    private static string Md(params string[] fences) =>
        "# Demo\n\nProse before.\n\n" + string.Concat(fences.Select(f => f + "\n\nProse between.\n\n"));

    private static string Fence(string info, params string[] code) =>
        "```" + info + "\n" + string.Concat(code.Select(line => line + "\n")) + "```";

    private static string PinsText()
    {
        var live = Git.RepositoryRoot();
        return File.ReadAllText(Path.Combine(live, "Directory.Packages.props"));
    }

    private static Fixture WithPage(string markdown, Dictionary<string, string?>? more = null)
    {
        var files = new Dictionary<string, string?>
        {
            [Page] = markdown,
            ["Directory.Packages.props"] = PinsText(),
        };
        foreach (var (path, text) in more ?? [])
        {
            files[path] = text;
        }

        return Fixture.Create(files);
    }

    private static (int Status, string Out, string Err) Fences(string root, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var status = Program.Run(["fences", "--root", root, .. args], output, error);
        return (status, output.ToString(), error.ToString());
    }

    private static (int Status, string Out, string Err) FencesWithLibs(string root, params string[] args) =>
        Fences(root, ["--libs", s_libs.Value, .. args]);

    /// <summary>The five built libraries of the working tree, copied flat so <c>--libs</c> can point at them.</summary>
    private static string CopyLiveLibraries()
    {
        var live = Git.RepositoryRoot();
        var target = Directory.CreateTempSubdirectory("housekeeping-fence-libs-").FullName;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDelete(target);
        foreach (var project in Directory.EnumerateDirectories(Path.Combine(live, "src"), "Dexpace.Sdk.*"))
        {
            var dll = Path.Combine(project, "bin", "Release", "net10.0", Path.GetFileName(project) + ".dll");
            if (!File.Exists(dll))
            {
                throw new InvalidOperationException(
                    $"{dll} is missing: build the solution first (dotnet build Dexpace.Sdk.sln --configuration Release). " +
                    "The fence tests compile against the built libraries and do not skip without them.");
            }

            File.Copy(dll, Path.Combine(target, Path.GetFileName(dll)));
        }

        return target;
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: it is a temp directory.
        }
    }

    private static int PageLineOf(string markdown, string needle) =>
        Prose.LineAt(markdown, markdown.IndexOf(needle, StringComparison.Ordinal));

    // --- which fences ---------------------------------------------------------------------------

    [Fact]
    public void A_fence_that_names_a_namespace_is_extracted_with_its_page_and_line()
    {
        // A fragment is counted without being compiled, which makes it the cheapest way to observe extraction.
        var markdown = Md(Fence("csharp fragment", "using Dexpace.Sdk.Core.Http.Request;", "var r = Request.Get(\"https://x/\");"));
        using var fixture = WithPage(markdown);

        var (status, output, error) = Fences(fixture.Root);

        Assert.Equal(string.Empty, error);
        Assert.Equal(0, status);
        Assert.Contains($"{Page}:{PageLineOf(markdown, "```csharp")}: [fence] fragment", output, StringComparison.Ordinal);
        Assert.Contains("0 compiled, 1 fragments, 0 skipped, 0 failed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_qualified_type_without_a_using_line_counts()
    {
        // The single-line-import trap: a fence that never writes "using Dexpace.Sdk" still names the namespace.
        var markdown = Md(Fence("csharp fragment", "var r = Dexpace.Sdk.Core.Http.Request.Request.Get(\"https://x/\");"));
        using var fixture = WithPage(markdown);

        var (_, output, _) = Fences(fixture.Root);

        Assert.Contains("0 compiled, 1 fragments, 0 skipped, 0 failed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_fence_with_no_Dexpace_reference_is_skipped()
    {
        // Not extracted at all: an illustrative snippet in someone else's API is not this command's business, and
        // it is not counted, so a page full of them reports nothing rather than a number that means nothing.
        using var fixture = WithPage(Md(
            Fence("csharp", "var list = new List<int>();", "list.Add(this_does_not_compile);"),
            Fence("json", "{ \"Dexpace.Sdk\": 1 }"),
            Fence("text", "using Dexpace.Sdk.Core;")));

        var (status, output, _) = Fences(fixture.Root);

        Assert.Equal(0, status);
        Assert.Contains("0 compiled, 0 fragments, 0 skipped, 0 failed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_documented_files_are_read()
    {
        var fragment = Md(Fence("csharp fragment", "using Dexpace.Sdk.Core;"));
        using var fixture = WithPage(
            fragment,
            new()
            {
                ["README.md"] = fragment,
                ["src/Dexpace.Sdk.Core/README.md"] = fragment,
                ["CLAUDE.md"] = fragment,
                ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = fragment,
                ["docs/sdk-documentation/deeper/x.md"] = fragment,
            });

        var (_, output, _) = Fences(fixture.Root);

        // The page, README.md and the package README; not CLAUDE.md, a work document or a nested directory.
        Assert.Contains("3 fragments", output, StringComparison.Ordinal);
        Assert.Contains("README.md:", output, StringComparison.Ordinal);
        Assert.DoesNotContain("CLAUDE.md", output, StringComparison.Ordinal);
        Assert.DoesNotContain("phase1-thing", output, StringComparison.Ordinal);
        Assert.DoesNotContain("deeper", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_fragment_fence_is_counted_and_never_compiled()
    {
        // It would not compile, and there are no libraries to compile against: exit 0 proves neither was attempted.
        using var fixture = WithPage(Md(Fence("csharp fragment", "using Dexpace.Sdk.Core;", "this_does_not_compile();")));

        var (status, output, _) = Fences(fixture.Root, "--libs", Path.Combine(fixture.Root, "no-such-directory"));

        Assert.Equal(0, status);
        Assert.Contains("0 compiled, 1 fragments, 0 skipped, 0 failed", output, StringComparison.Ordinal);
    }

    // --- compiling ------------------------------------------------------------------------------

    [Fact]
    public void A_clean_fence_compiles()
    {
        using var fixture = WithPage(Md(Fence(
            "csharp",
            "using Dexpace.Sdk.Core.Http.Request;",
            "",
            "var request = Request.Get(\"https://api.example.com/health\");",
            "Console.WriteLine(request.Url);")));

        var (status, output, error) = FencesWithLibs(fixture.Root);

        Assert.Equal(string.Empty, error);
        Assert.Contains("1 compiled, 0 fragments, 0 skipped, 0 failed", output, StringComparison.Ordinal);
        Assert.Equal(0, status);
    }

    [Fact]
    public void A_fence_that_only_declares_a_type_compiles()
    {
        // The write-a guides show a class, not a program: a fence is a complete program or the declarations a reader would add to one.
        using var fixture = WithPage(Md(Fence(
            "csharp",
            "using Dexpace.Sdk.Core.Http.Request;",
            "",
            "public sealed class Probe",
            "{",
            "    public static Request Health() => Request.Get(\"https://api.example.com/health\");",
            "}")));

        var (status, output, error) = FencesWithLibs(fixture.Root);

        Assert.Equal(string.Empty, error);
        Assert.Contains("1 compiled, 0 fragments, 0 skipped, 0 failed", output, StringComparison.Ordinal);
        Assert.Equal(0, status);
    }

    [Fact]
    public void A_broken_fence_fails_naming_page_and_line()
    {
        var markdown = Md(
            Fence("csharp", "using Dexpace.Sdk.Core.Http.Request;", "var request = Request.Get(\"https://x/\");", "Console.WriteLine(undefinedName);", "Console.WriteLine(alsoUndefined);"),
            Fence("csharp", "using Dexpace.Sdk.Core.Http.Request;", "Console.WriteLine(Request.Get(\"https://x/\").Url);"));
        using var fixture = WithPage(markdown);

        var (status, output, _) = FencesWithLibs(fixture.Root);

        // The failing statement is the third line of the first fence; the second fence is clean.
        var line = PageLineOf(markdown, "undefinedName");
        Assert.Matches($@"{Regex.Escape(Page)}:{line}: \[fence\] CS0103: .*undefinedName", output);
        Assert.Contains("1 compiled, 0 fragments, 0 skipped, 1 failed", output, StringComparison.Ordinal);
        Assert.Equal(1, status);
    }

    [Fact]
    public void A_warning_is_an_error()
    {
        // TreatWarningsAsErrors: an unused local is a documentation bug too.
        using var fixture = WithPage(Md(Fence("csharp", "using Dexpace.Sdk.Core.Http.Request;", "int unused = 3;", "_ = Request.Get(\"https://x/\");")));

        var (status, output, _) = FencesWithLibs(fixture.Root);

        Assert.Equal(1, status);
        Assert.Matches(@"\[fence\] CS0219", output);
    }

    [Fact]
    public void Missing_libraries_fail_with_a_message_and_do_not_skip()
    {
        using var fixture = WithPage(Md(Fence("csharp", "using Dexpace.Sdk.Core;")));

        var (status, _, error) = Fences(fixture.Root);

        Assert.Equal(1, status);
        Assert.Contains("--configuration Release", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_fence_needing_an_unpinned_optional_package_is_reported_and_not_failed()
    {
        // Microsoft.Extensions.Hosting is a package Directory.Packages.props does not pin: reported, counted, not a failure.
        var markdown = Md(Fence("csharp", "using Dexpace.Sdk.Core;", "using Microsoft.Extensions.Hosting;", "var host = Host.CreateApplicationBuilder();"));
        using var fixture = WithPage(markdown);

        var (status, output, _) = Fences(fixture.Root, "--libs", Path.Combine(fixture.Root, "no-such-directory"));

        Assert.Equal(0, status);
        Assert.Contains($"{Page}:{PageLineOf(markdown, "```csharp")}: [fence] skipped: needs Microsoft.Extensions.Hosting", output, StringComparison.Ordinal);
        Assert.Contains("0 compiled, 0 fragments, 1 skipped, 0 failed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_fence_that_needs_ILogger_compiles_because_the_logging_abstractions_are_always_referenced()
    {
        // Core's public surface exposes ILogger, so a bare <Reference> to the DLL is CS0012 without the package. This is also
        // what a project rooted inside the repository would trip (NU1008 on a versioned PackageReference, under central
        // package management), so a clean compile here is the proof the scratch project inherits none of it.
        using var fixture = WithPage(Md(Fence(
            "csharp",
            "using Dexpace.Sdk.Core.Pipeline;",
            "using Microsoft.Extensions.Logging;",
            "using Microsoft.Extensions.Logging.Abstractions;",
            "",
            "ILogger logger = NullLogger.Instance;",
            "Console.WriteLine(logger.IsEnabled(LogLevel.Information));",
            "Console.WriteLine(typeof(DexpacePipeline).Name);")));

        var (status, output, error) = FencesWithLibs(fixture.Root);

        Assert.Equal(string.Empty, error);
        Assert.Contains("1 compiled, 0 fragments, 0 skipped, 0 failed", output, StringComparison.Ordinal);
        Assert.Equal(0, status);
    }

    [Fact]
    public void A_fence_using_host_integration_gets_its_package_closure()
    {
        // IServiceCollection comes with Microsoft.Extensions.Http's closure, which the generated project references
        // for a fence that names the host-integration package or a dependency-injection namespace.
        using var fixture = WithPage(Md(Fence(
            "csharp",
            "using Dexpace.Sdk.Extensions.DependencyInjection;",
            "using Microsoft.Extensions.DependencyInjection;",
            "",
            "var services = new ServiceCollection();",
            "Console.WriteLine(services.Count);")));

        var (status, output, error) = FencesWithLibs(fixture.Root);

        Assert.Equal(string.Empty, error);
        Assert.Contains("1 compiled, 0 fragments, 0 skipped, 0 failed", output, StringComparison.Ordinal);
        Assert.Equal(0, status);
    }

    [Theory]
    [InlineData("using Microsoft.Extensions.Http.Resilience;", "Microsoft.Extensions.Http.Resilience", null)]
    [InlineData("var l = typeof(Microsoft.Extensions.Logging.ILogger);", "Microsoft.Extensions.Logging.Abstractions", null)]
    [InlineData("using Microsoft.Extensions.Hosting;", null, "Microsoft.Extensions.Hosting")]
    [InlineData("services.AddOpenTelemetry();\nusing OpenTelemetry.Trace;", null, "OpenTelemetry")]
    [InlineData("using Polly;", null, "Polly")]
    public void Packages_are_resolved_against_the_pins_and_the_rest_is_missing(string line, string? pinned, string? missing)
    {
        using var fixture = WithPage("# none\n");
        var needs = FencePackages.Read(new Repo(fixture.Root))
            .Resolve(new Fence(Page, 3, false, ["using Dexpace.Sdk.Core;", .. line.Split('\n')]));

        Assert.Contains(needs.Packages, p => p.Id == FencePackages.Logging && p.Version.Length > 0);
        if (pinned is not null)
        {
            Assert.Contains(needs.Packages, p => p.Id == pinned);
        }

        string[] expected = missing is null ? [] : [missing];
        Assert.Equal(expected, needs.Missing);
    }

    // --- writes ---------------------------------------------------------------------------------

    [Fact]
    public void The_scratch_project_is_written_through_the_guard()
    {
        using var fixture = WithPage(Md(Fence("csharp", "using Dexpace.Sdk.Core;")));
        var frozen = Path.Combine(fixture.Root, "docs", "product-spec", "scratch");

        var compiler = new FenceCompiler(new Repo(fixture.Root), s_libs.Value, frozen);

        Assert.Throws<FrozenPathException>(compiler.Run);
        Assert.False(Directory.Exists(frozen));
    }

    [Fact]
    public void The_scratch_root_is_outside_the_repository_and_is_removed()
    {
        using var fixture = WithPage(Md(Fence("csharp", "using Dexpace.Sdk.Core;")));

        var report = new FenceCompiler(new Repo(fixture.Root), s_libs.Value).Run();

        var scratch = Assert.IsType<string>(report.ScratchRoot);
        Assert.StartsWith(Guard.RealPath(Path.GetTempPath()), Guard.RealPath(Path.GetDirectoryName(scratch)!), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, scratch, StringComparison.Ordinal);
        Assert.False(Directory.Exists(scratch));
        Assert.Equal(0, report.Failed);
    }

    [Fact]
    public void The_command_writes_nothing_in_the_repository()
    {
        using var fixture = WithPage(
            Md(
                Fence("csharp", "using Dexpace.Sdk.Core.Http.Request;", "Console.WriteLine(Request.Get(\"https://x/\").Url);"),
                Fence("csharp", "using Dexpace.Sdk.Core.Http.Request;", "Console.WriteLine(undefinedName);"),
                Fence("csharp fragment", "using Dexpace.Sdk.Core;")),
            new() { ["global.json"] = File.ReadAllText(Path.Combine(Git.RepositoryRoot(), "global.json")) });
        var before = fixture.Status();
        var tree = Directory.EnumerateFileSystemEntries(fixture.Root, "*", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(1, FencesWithLibs(fixture.Root).Status);

        Assert.Equal(before, fixture.Status());
        Assert.Equal(
            tree,
            [.. Directory.EnumerateFileSystemEntries(fixture.Root, "*", SearchOption.AllDirectories)
                .Where(p => !p.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)]);
    }

    // --- the command line -----------------------------------------------------------------------

    [Fact]
    public void An_unknown_option_is_a_usage_error()
    {
        using var fixture = Fixture.Create();

        var (status, _, error) = Fences(fixture.Root, "--nope");

        Assert.Equal(2, status);
        Assert.Contains("invalid option: --nope", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_prints_the_usage_and_exits_zero()
    {
        using var output = new StringWriter();

        var status = Program.Run(["fences", "--help"], output, TextWriter.Null);

        Assert.Equal(0, status);
        Assert.Contains("fences [--root", output.ToString(), StringComparison.Ordinal);
    }
}
