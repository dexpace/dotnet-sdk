// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Xunit;

namespace Dexpace.Tools.Knowledge.Tests;

public sealed class StructureGateTests : KnowledgeFixture
{
    private const string Smuggled = "docs/knowledge/harvested/smuggled.md";

    [Fact]
    public void TheGate_PassesOnTheFixture()
    {
        var (stdout, _, status) = Run("verify-structure");
        Assert.Equal(0, status);
        Assert.Contains("knowledge structure OK: 9 harvested entries", stdout, StringComparison.Ordinal);
        Assert.Contains("one of 3 roots", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_RejectsAReviewRoleUnderHarvested()
    {
        WriteFixture(Smuggled,
            "# smuggled\n\n## Rules\n- What the implementation found, written into the wrong tree.\n" +
            "  <sub>review · `docs/product-spec/04-core-http-domain-model.md:1` · high · sha:manual-x</sub>\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(1, status);
        Assert.Contains("role `review` under harvested/", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_RejectsAnInventedRole()
    {
        WriteFixture(Smuggled,
            "# smuggled\n\n## Rules\n- A rule.\n" +
            "  <sub>opinion · `docs/product-spec/04-core-http-domain-model.md:1` · high · sha:abc123456789</sub>\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(1, status);
        Assert.Contains("role `opinion` under harvested/", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_RejectsASupersededSectionAndAnOffRootSource()
    {
        WriteFixture(Smuggled,
            "# smuggled\n\n## Superseded\n- A judgement that belongs in notes/.\n" +
            "  <sub>spec · `docs/work/mvp/phase1/phase1a/2026-01-01-phase1a-http.md:1` · high · sha:abc123456789</sub>\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(1, status);
        Assert.Contains("a Superseded entry under harvested/", stderr, StringComparison.Ordinal);
        Assert.Contains("which is under none of the harvested source roots", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_SeesThroughADotDotEscapeFromARoot()
    {
        WriteFixture(Smuggled,
            "# smuggled\n\n## Rules\n- A rule.\n" +
            "  <sub>spec · `docs/product-spec/../work/mvp/x.md:1` · high · sha:abc123456789</sub>\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(1, status);
        Assert.Contains("which is under none of the harvested source roots", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_RejectsABulletWithNoProvenanceLine()
    {
        WriteFixture(Smuggled, "# smuggled\n\n## Rules\n- A bullet somebody typed.\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(1, status);
        Assert.Contains("no source.", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_RejectsATopicFileStrandedAtTheRoot()
    {
        WriteFixture("docs/knowledge/stray.md", "# stray\n\n## Rules\n- Written by a --corpus-less harvest run.\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(1, status);
        Assert.Contains("a topic file at the root of docs/knowledge/", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_RejectsANoteThatIsNotReviewRole()
    {
        WriteFixture("docs/knowledge/notes/testing.md",
            "# testing — notes\n\n## Reference\n- A note.\n  <sub>spec · `docs/work/x.md` · high · sha:manual-x</sub>\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(1, status);
        Assert.Contains("a note whose role is `spec`, not `review`", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_RejectsANoteCitingAKeyNoEntryCarries()
    {
        WriteFixture("docs/knowledge/notes/testing.md",
            "# testing — notes\n\n## Superseded\n- Supersedes `testing/deadbeef`.\n" +
            "  <sub>review · `docs/work/x.md` · high · sha:manual-x</sub>\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(1, status);
        Assert.Contains("cites `testing/deadbeef`, which no harvested entry carries", stderr, StringComparison.Ordinal);
    }

    // The gate is only meaningful over a corpus that parsed; the floor is derived from INDEX.md rather
    // than from a hand-maintained magic number.
    [Fact]
    public void TheGate_RefusesToPassOverAParseHole()
    {
        WriteFixture("docs/knowledge/harvested/testing.md", "# testing\n\nEvery bullet lost to a bad edit.\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(2, status);
        Assert.Contains("parses to zero", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGate_RefusesARootThatSwallowsAnother()
    {
        var manifest = "docs/knowledge/harvested/SOURCES.md";
        File.AppendAllText(FixturePath(manifest), "| `docs/stray.md` | spec | `abcdef012345` | 2026-01-01 |\n");
        var (_, stderr, status) = Run("verify-structure");
        Assert.Equal(2, status);
        Assert.Contains("derives the source root 'docs'", stderr, StringComparison.Ordinal);
    }
}

public sealed class DriftReportTests : KnowledgeFixture
{
    [Fact]
    public void TheDriftReport_StatesOkDriftAndNotVerifiable()
    {
        var (stdout, _, status) = Run("drift");
        Assert.Equal(0, status);
        Assert.Contains("DRIFT\tdocs/product-spec/12-pagination.md\trecorded 000000000000, actual ", stdout, StringComparison.Ordinal);
        Assert.Contains("NOT VERIFIABLE\tdocs/styleguide/csharp/11-testing.md", stdout, StringComparison.Ordinal);
        Assert.Contains("6 harvested sources: 3 OK, 1 DRIFT, 2 NOT VERIFIABLE, 0 UNREADABLE.", stdout, StringComparison.Ordinal);
        Assert.Contains("1 note citation(s) resolve, 0 do not.", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDriftReport_ListsAStaleNoteKeyButStillExits0()
    {
        WriteFixture("docs/knowledge/notes/testing.md",
            "# testing — notes\n\n## Superseded\n- Supersedes `testing/deadbeef`.\n" +
            "  <sub>review · `docs/work/x.md` · high · sha:manual-x</sub>\n");
        var (stdout, _, status) = Run("drift");
        Assert.Equal(0, status);
        Assert.Contains("STALE KEY\tnotes/testing.md:4\tcites testing/deadbeef", stdout, StringComparison.Ordinal);
        Assert.Contains("1 note citation(s) resolve, 1 do not.", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDriftReport_FlagsAnEntryWhoseShaIsNotItsSourcesCurrentRow()
    {
        // The fixture's pagination entries carry 5555eeee6666 while SOURCES.md records another digest for the
        // file: exactly what a re-harvest that only added and updated would leave behind.
        var (stdout, _, status) = Run("drift");
        Assert.Equal(0, status);
        Assert.Contains("STALE ENTRY\tdocs/product-spec/12-pagination.md\t", stdout, StringComparison.Ordinal);
        Assert.Contains("carry sha 5555eeee6666, SOURCES.md records 000000000000", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AMalformedManifest_Exits2()
    {
        File.AppendAllText(FixturePath("docs/knowledge/harvested/SOURCES.md"), "| `docs/x.md` | spec | `abc` | 2026-01-01 |\n");
        var (_, stderr, status) = Run("drift");
        Assert.Equal(2, status);
        Assert.Contains("at least 12 are needed", stderr, StringComparison.Ordinal);
    }
}

/// <summary>
/// <c>--help</c> on the two companions: a banner naming the command and a short description, exit 0; and
/// an unknown flag is a usage error rather than being silently ignored.
/// </summary>
public sealed class CompanionHelpTests : KnowledgeFixture
{
    [Theory]
    [InlineData("drift")]
    [InlineData("verify-structure")]
    public void Help_PrintsAShortUsageAndExits0(string command)
    {
        var (stdout, stderr, status) = RunRaw(command, "--help");
        Assert.Equal(0, status);
        Assert.Contains($"Usage: scripts/knowledge {command}", stdout, StringComparison.Ordinal);
        var lines = stdout.Split('\n')
            .Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith("-h", StringComparison.Ordinal)
                && !line.TrimStart().StartsWith("--root", StringComparison.Ordinal))
            .ToList();
        Assert.InRange(lines.Count, 4, 6);
        Assert.Empty(stderr);
    }

    [Theory]
    [InlineData("drift")]
    [InlineData("verify-structure")]
    public void AnUnknownFlag_Exits2(string command)
    {
        var (_, stderr, status) = RunRaw(command, "--bogus");
        Assert.Equal(2, status);
        Assert.Contains("invalid option: --bogus", stderr, StringComparison.Ordinal);
    }
}

/// <summary>
/// One end-to-end run of the built executable: the process exit code and argument plumbing are not
/// exercised by the in-process tests above.
/// </summary>
public sealed class ExecutableTests : KnowledgeFixture
{
    [Fact]
    public void TheExecutable_RunsAsAProgramAndExits1OnNoMatch()
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var start = new ProcessStartInfo(host)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "knowledge.dll"), "--root", Root, "--req", "HTTP-7" })
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(1, process.ExitCode);
        Assert.Contains("HTTP-7 is canonical but no entry cites it yet.", stdout, StringComparison.Ordinal);
    }
}
