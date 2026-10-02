// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json;
using Xunit;

namespace Dexpace.Tools.Knowledge.Tests;

public sealed class GapsTests : KnowledgeFixture
{
    [Fact]
    public void Gaps_SeparatesRollupOnlyFromUncited()
    {
        var (stdout, _, status) = Run("--gaps", "HTTP");
        Assert.Equal(0, status);
        Assert.Contains("4 canonical IDs: 2 substantive, 1 roll-up only, 1 uncited", stdout, StringComparison.Ordinal);
        Assert.Matches(@"roll-up only .*\n\s+HTTP-2\n", stdout);
        Assert.Matches(@"uncited .*\n\s+HTTP-7\n", stdout);
        // The fixture's chapter 04 states no HTTP-<n> token, so the pointer says so rather than naming it.
        Assert.Contains("appendix C is their only normative statement", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void GapsAll_CoversEveryPrefixInIdOrder()
    {
        var (stdout, _, status) = Run("--gaps", "all");
        Assert.Equal(0, status);
        Assert.Contains("HTTP — Core HTTP domain model", stdout, StringComparison.Ordinal);
        Assert.Contains("PAGE — Pagination", stdout, StringComparison.Ordinal);
        Assert.Contains("SEAM — Product vision, pluggable seams and extension model", stdout, StringComparison.Ordinal);
        Assert.Contains("4 of 9 IDs in 3 prefixes have no substantive entry", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Gaps_ListsAPrefixWithNoCorpusEntryAtAllAsUncited()
    {
        var (stdout, _, _) = Run("--gaps", "SEAM");
        Assert.Contains("1 canonical IDs: 0 substantive, 0 roll-up only, 1 uncited", stdout, StringComparison.Ordinal);
        Assert.Contains("SEAM-1", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Gaps_TakesSeveralPrefixesAtOnce()
    {
        var (stdout, _, status) = Run("--gaps", "HTTP,SEAM");
        Assert.Equal(0, status);
        Assert.Contains("HTTP — Core HTTP domain model", stdout, StringComparison.Ordinal);
        Assert.Contains("SEAM — Product vision", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("PAGE — Pagination", stdout, StringComparison.Ordinal);
        Assert.Contains("3 of 5 IDs in 2 prefixes have no substantive entry", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Gaps_TakesSpaceSeparatedPrefixesLikeTheCommaForm()
    {
        // The roadmap documents the flag as `--gaps <PREFIXES>`, and a shell user types a space. The second
        // prefix used to be a bare query word that --gaps never looked at: the report covered HTTP alone
        // and exited 0 (#31).
        var (stdout, stderr, status) = Run("--gaps", "HTTP", "SEAM");
        Assert.Equal(0, status);
        Assert.Equal("", stderr);
        Assert.Contains("HTTP — Core HTTP domain model", stdout, StringComparison.Ordinal);
        Assert.Contains("SEAM — Product vision", stdout, StringComparison.Ordinal);
        Assert.Contains("3 of 5 IDs in 2 prefixes have no substantive entry", stdout, StringComparison.Ordinal);
        Assert.Equal(Run("--gaps", "HTTP,SEAM").Stdout, stdout);
    }

    [Fact]
    public void Gaps_MixesSpaceAndCommaFormsAndTakesWordsAfterOtherOptions()
    {
        var (stdout, _, status) = Run("--gaps", "HTTP,PAGE", "--brief", "SEAM");
        Assert.Equal(0, status);
        Assert.Contains("HTTP — Core HTTP domain model", stdout, StringComparison.Ordinal);
        Assert.Contains("PAGE — Pagination", stdout, StringComparison.Ordinal);
        Assert.Contains("SEAM — Product vision", stdout, StringComparison.Ordinal);
        Assert.Contains("in 3 prefixes have no substantive entry", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Gaps_AWordThatIsNotAPrefixIsAUsageErrorNotASilentDrop()
    {
        var (stdout, stderr, status) = Run("--gaps", "HTTP", "UTF");
        Assert.Equal(2, status);
        Assert.Equal("", stdout);
        Assert.Contains("'UTF' is not a requirement-ID prefix in appendix C", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Gaps_ReportsEachPrefixOnceHoweverItWasSpelled()
    {
        var (stdout, _, status) = Run("--gaps", "http", "HTTP,SEAM", "seam");
        Assert.Equal(0, status);
        Assert.Contains("3 of 5 IDs in 2 prefixes have no substantive entry", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownPrefix_Exits2()
    {
        var (_, stderr, status) = Run("--gaps", "UTF");
        Assert.Equal(2, status);
        Assert.Contains("not a requirement-ID prefix in appendix C", stderr, StringComparison.Ordinal);
        Assert.Equal(2, Run("--gaps", "").Status);
    }
}

/// <summary>
/// <c>--gaps</c> derives its trailing pointer from appendix C's subsystem cell, which names the prefix's
/// owning chapter without asserting the chapter states the ID. The pointer therefore checks the chapters.
/// </summary>
public sealed class GapPointerTests : KnowledgeFixture
{
    private const string PageChapter = "docs/product-spec/12-pagination.md";

    [Fact]
    public void ThePointer_NamesAppendixCForAnIdNoChapterStates()
    {
        var (stdout, _, _) = Run("--gaps", "PAGE", "--no-drift-check");
        Assert.Contains("appendix C is their only normative statement", stdout, StringComparison.Ordinal);
        Assert.Contains("grep -n '^| PAGE-4 ' docs/product-spec/appendix-c-", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("read these out of docs/product-spec/12-pagination.md", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePointer_NamesTheChapterForAnIdTheChapterStates()
    {
        AppendToChapter(PageChapter, "PAGE-4");
        var (stdout, _, _) = Run("--gaps", "PAGE", "--no-drift-check");
        Assert.Contains("read these out of docs/product-spec/12-pagination.md", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("appendix C is their only normative statement", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ANearMissIdInTheChapter_DoesNotSatisfyThePointer()
    {
        // Whole-token matching: PAGE-40 is a different requirement from PAGE-4.
        AppendToChapter(PageChapter, "PAGE-40");
        var (stdout, _, _) = Run("--gaps", "PAGE", "--no-drift-check");
        Assert.Contains("appendix C is their only normative statement", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void APrefixSplitAcrossBothSources_GetsBothPointersWithTheirIds()
    {
        // HTTP-2 is roll-up-only and HTTP-7 uncited; stating just one of them splits the pointer in two.
        AppendToChapter("docs/product-spec/04-core-http-domain-model.md", "HTTP-2");
        var (stdout, _, _) = Run("--gaps", "HTTP", "--no-drift-check");
        Assert.Contains("read these out of docs/product-spec/04-core-http-domain-model.md: HTTP-2", stdout, StringComparison.Ordinal);
        Assert.Contains("chapter states them: HTTP-7", stdout, StringComparison.Ordinal);
    }

    private void AppendToChapter(string relative, string id) =>
        File.AppendAllText(FixturePath(relative), $"\n{id} is stated here for this test.\n");
}

public sealed class PrefixInfoTests : KnowledgeFixture
{
    [Fact]
    public void PrefixInfo_ReportsSubsystemChapterAndCounts()
    {
        var (stdout, _, status) = Run("--prefix-info", "http");
        Assert.Equal(0, status);
        Assert.Contains("HTTP — Core HTTP domain model", stdout, StringComparison.Ordinal);
        Assert.Contains("4 canonical IDs (HTTP-1..HTTP-70), 3 MUST, 1 SHOULD", stdout, StringComparison.Ordinal);
        Assert.Contains("owning chapter: docs/product-spec/04-core-http-domain-model.md", stdout, StringComparison.Ordinal);
        Assert.Contains("2 of 4 IDs have a substantive entry, 1 are roll-up only, 1 are uncited", stdout, StringComparison.Ordinal);
        Assert.Contains("topics carrying HTTP knowledge: http-domain-model", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void PrefixInfo_TakesOnePrefix_AndRejectsAnExtraWordRatherThanDroppingIt()
    {
        var (stdout, stderr, status) = Run("--prefix-info", "HTTP", "SEAM");
        Assert.Equal(2, status);
        Assert.Equal("", stdout);
        Assert.Contains("--prefix-info takes one prefix", stderr, StringComparison.Ordinal);
        Assert.Contains("SEAM", stderr, StringComparison.Ordinal);
    }
}

public sealed class PrefixCoverageTests : KnowledgeFixture
{
    [Fact]
    public void ANarrowedPrefixQuery_NamesTheCanonicalIdsItDidNotCover()
    {
        var (stdout, _, _) = Run("--prefix", "PAGE", "--section", "rules", "--brief", "--no-drift-check");
        Assert.Contains("these filters cover 2 of PAGE's 4 canonical IDs", stdout, StringComparison.Ordinal);
        Assert.Matches("PAGE-3 — in the corpus under Conflicts", stdout);
        Assert.Matches("PAGE-4 — no substantive entry anywhere", stdout);
        Assert.Contains("--gaps PAGE", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnnarrowedPrefixQuery_ReportsOnlyWhatTheCorpusLacks()
    {
        var (stdout, _, _) = Run("--prefix", "PAGE", "--brief", "--no-drift-check");
        Assert.Contains("these filters cover 3 of PAGE's 4 canonical IDs", stdout, StringComparison.Ordinal);
        Assert.Matches("PAGE-4 — no substantive entry anywhere", stdout);
        Assert.DoesNotContain("PAGE-3 —", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AQueryWithNoPrefixFilter_CarriesNoCoverageFooter()
    {
        var (stdout, _, _) = Run("--req", "PAGE-1", "--brief", "--no-drift-check");
        Assert.DoesNotContain("canonical IDs", stdout, StringComparison.Ordinal);
    }
}

public sealed class PhaseTests : KnowledgeFixture
{
    [Fact]
    public void Phase_ReportsWhichDocumentContributedWhichIdsThenQueriesThem()
    {
        var (stdout, _, status) = Run("--phase", "1a", "--brief");
        Assert.Equal(0, status);
        Assert.Contains("phase 1a: 1 document", stdout, StringComparison.Ordinal);
        Assert.Contains("docs/work/mvp/phase1/phase1a/2026-01-01-phase1a-http.md — 3: HTTP-1 HTTP-2 PAGE-1", stdout, StringComparison.Ordinal);
        Assert.Contains("3 distinct requirement IDs cited by phase 1a", stdout, StringComparison.Ordinal);
        // The union of the three IDs: two HTTP entries plus the PAGE-1 rule.
        Assert.Contains("3 entries across 2 topic files", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ABarePhaseNumber_WalksTheSubPhaseDirectoriesToo()
    {
        var (stdout, _, _) = Run("--phase", "phase1", "--brief");
        Assert.Contains("phase 1: 2 documents", stdout, StringComparison.Ordinal);
        Assert.Contains("2026-01-01-phase1-segmentation.md — 1: SEAM-1", stdout, StringComparison.Ordinal);
        Assert.Contains("4 distinct requirement IDs cited by phase 1", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_ComposesWithTheOtherFilters()
    {
        var (stdout, _, _) = Run("--phase", "1a", "--section", "rules", "--brief");
        Assert.Contains("2 entries across 2 topic files", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnwrittenPhase_SaysSoAndExits0()
    {
        var (stdout, _, status) = Run("--phase", "9");
        Assert.Equal(0, status);
        Assert.Contains("no phase documents found for phase 9", stdout, StringComparison.Ordinal);
        Assert.Contains("--prefix-info and --gaps", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_CreditsEveryCanonicalIdInAnEnDashRange()
    {
        // The fixture's canonical HTTP IDs are 1, 2, 7 and 70, so HTTP-1–HTTP-7 is three of them: a range
        // credits what appendix C defines, not every integer between the endpoints.
        WriteFixture("docs/work/mvp/phase3/2026-01-02-phase3-ranges.md", "Covers HTTP-1\u2013HTTP-7 in one sweep.\n");
        var (stdout, _, status) = Run("--phase", "3", "--brief", "--no-drift-check");
        Assert.Equal(0, status);
        Assert.Contains("2026-01-02-phase3-ranges.md — 3: HTTP-1 HTTP-2 HTTP-7", stdout, StringComparison.Ordinal);
        Assert.Contains("3 distinct requirement IDs cited by phase 3", stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("HTTP-1\u2013HTTP-7")]
    [InlineData("HTTP-1-HTTP-7")]
    [InlineData("HTTP-1\u20137")]
    [InlineData("HTTP-1-7")]
    [InlineData("HTTP-1..HTTP-7")]
    [InlineData("HTTP-1..7")]
    [InlineData("HTTP-1 \u2013 HTTP-7")]
    [InlineData("HTTP-1 - HTTP-7")]
    [InlineData("HTTP-1 .. HTTP-7")]
    [InlineData("`HTTP-1`\u2013`HTTP-7`")]
    [InlineData("`HTTP-1`-`HTTP-7`")]
    [InlineData("`HTTP-1`..`HTTP-7`")]
    [InlineData("**HTTP-1**\u2013**HTTP-7**")]
    [InlineData("**HTTP-1**\u2013 **HTTP-7**")]
    [InlineData("`HTTP-1\u2013HTTP-7`")]
    [InlineData("`HTTP-1`\u20137")]
    public void Phase_ExpandsEveryRangeForm(string citation)
    {
        WriteFixture("docs/work/mvp/phase3/2026-01-02-phase3-ranges.md", $"Covers {citation}.\n");
        var (stdout, _, _) = Run("--phase", "3", "--brief", "--no-drift-check");
        Assert.Contains("— 3: HTTP-1 HTTP-2 HTTP-7", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_ExpandsARangeToTheEntriesItCites()
    {
        // HTTP-2 sits strictly inside the range and is named nowhere else, so its entry reaches the corpus
        // query only through the expansion.
        WriteFixture("docs/work/mvp/phase3/2026-01-02-phase3-ranges.md", "Covers HTTP-1\u2013HTTP-70.\n");
        var (stdout, _, status) = Run("--phase", "3", "--brief", "--no-drift-check");
        Assert.Equal(0, status);
        Assert.Contains("4 distinct requirement IDs cited by phase 3", stdout, StringComparison.Ordinal);
        Assert.Contains(RollupHttp2, stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_LeavesAnUnexpandableRangeAsItsEndpoints()
    {
        // Reversed, spanning two prefixes, and a short form with spaces (`- 3 retries` is prose, not a
        // range): none of them is a range, so each credits only the IDs it actually names.
        WriteFixture(
            "docs/work/mvp/phase3/2026-01-02-phase3-ranges.md",
            "Reversed HTTP-7\u2013HTTP-1. Across HTTP-2\u2013PAGE-1. Prose HTTP-70 - 3 retries.\n");
        var (stdout, _, _) = Run("--phase", "3", "--brief", "--no-drift-check");
        Assert.Contains("— 5: HTTP-1 HTTP-2 HTTP-7 HTTP-70 PAGE-1", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_DoesNotExpandARangeOverAPrefixAppendixCDoesNotDefine()
    {
        WriteFixture(
            "docs/work/mvp/phase3/2026-01-02-phase3-ranges.md",
            "Encode as UTF-8\u2013UTF-16, hash with SHA-1..5, see RFC-7230\u20137235, and HTTP-70.\n");
        var (stdout, _, _) = Run("--phase", "3", "--brief", "--no-drift-check");
        Assert.Contains("— 1: HTTP-70", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_BoundsAnAbsurdRangeByAppendixC()
    {
        // The expansion walks appendix C's IDs for the prefix, never the integers between the endpoints, so a
        // typo'd upper bound costs nothing and credits no more than the family holds. The typo'd endpoint is
        // still credited as written — the corpus query warns that it is not in appendix C, which is the
        // feedback a bad citation deserves — and a bound too large for a long behaves the same way.
        WriteFixture(
            "docs/work/mvp/phase3/2026-01-02-phase3-ranges.md",
            "HTTP-1\u2013HTTP-9999999999 and PAGE-2..99999999999999999999999999.\n");
        var (stdout, stderr, status) = Run("--phase", "3", "--brief", "--no-drift-check");
        Assert.Equal(0, status);
        Assert.Contains("— 9: HTTP-1 HTTP-2 HTTP-7 HTTP-70 HTTP-9999999999 PAGE-2..4 PAGE-99999999999999999999999999", stdout, StringComparison.Ordinal);
        Assert.Contains("HTTP-9999999999 is not in appendix C", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_DoesNotCreditABareNumberItsRangeCannotClose()
    {
        // `HTTP-70-3 times` and a reversed `HTTP-70\u20133` name HTTP-70 and a number; HTTP-3 is not cited.
        WriteFixture(
            "docs/work/mvp/phase3/2026-01-02-phase3-ranges.md",
            "Reversed HTTP-70\u20133. Retried HTTP-7-2 times. Marked `HTTP-70`\u20131.\n");
        var (stdout, _, _) = Run("--phase", "3", "--brief", "--no-drift-check");
        Assert.Contains("— 2: HTTP-7 HTTP-70", stdout, StringComparison.Ordinal);
        Assert.Contains("2 distinct requirement IDs cited by phase 3", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_ReadsTheMarkdownWrappedRangesTheRealPhaseDocumentsUse()
    {
        // The two spellings docs/work uses almost exclusively: each endpoint in code, or each in bold.
        WriteFixture(
            "docs/work/mvp/phase3/2026-01-02-phase3-ranges.md",
            "Covers `PAGE-1`\u2013`PAGE-3` and **HTTP-1**\u2013**HTTP-7**.\n");
        var (stdout, _, _) = Run("--phase", "3", "--brief", "--no-drift-check");
        Assert.Contains("— 6: HTTP-1 HTTP-2 HTTP-7 PAGE-1..3", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AMalformedPhase_Exits2()
    {
        var (_, stderr, status) = Run("--phase", "nope");
        Assert.Equal(2, status);
        Assert.Contains("expected a phase like 5 or 5a", stderr, StringComparison.Ordinal);
    }
}

public sealed class DriftWarningTests : KnowledgeFixture
{
    [Fact]
    public void AQueryTouchingADriftedSource_WarnsOnStderrWithoutFailing()
    {
        var (stdout, stderr, status) = Run("--topic", "pagination", "--section", "rules", "--brief");
        Assert.Equal(0, status);
        Assert.Contains("warning: stale docs/product-spec/12-pagination.md", stderr, StringComparison.Ordinal);
        Assert.Contains("harvested at sha 000000000000", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("warning", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWarning_IsOneLinePerSourceHoweverManyEntriesTouchIt()
    {
        var (_, stderr, _) = Run("--topic", "pagination", "--brief");
        Assert.Single(stderr.Split('\n'), line => line.StartsWith("warning: stale", StringComparison.Ordinal));
    }

    [Fact]
    public void NoDriftCheck_SilencesIt()
    {
        Assert.Empty(Run("--topic", "pagination", "--brief", "--no-drift-check").Stderr);
    }

    [Fact]
    public void AnUndriftedSourceSaysNothing_AndAMissingOneIsNotDrift()
    {
        Assert.Empty(Run("--topic", "http-domain-model,testing", "--brief").Stderr);
    }
}

public sealed class OutputTests : KnowledgeFixture
{
    [Fact]
    public void AResult_CarriesLocationSectionAndKey()
    {
        var (stdout, _, status) = Run("--req", "HTTP-1", "--no-drift-check");
        Assert.Equal(0, status);
        Assert.Contains($"http-domain-model.md:4 (Rules) {RuleHttp1}", stdout, StringComparison.Ordinal);
        Assert.Contains("<sub>spec · `docs/product-spec/04-core-http-domain-model.md:9`", stdout, StringComparison.Ordinal);
        Assert.Contains("1 entry across 1 topic file", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Brief_DropsTheProvenanceLine()
    {
        Assert.DoesNotContain("<sub>", Run("--req", "HTTP-1", "--brief", "--no-drift-check").Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_IsRecordsWithOriginKeyAndRollup()
    {
        var (stdout, _, status) = Run("--req", "HTTP-2", "--json", "--no-drift-check");
        Assert.Equal(0, status);
        using var document = JsonDocument.Parse(stdout);
        var record = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(RollupHttp2, record.GetProperty("key").GetString());
        Assert.Equal("harvested", record.GetProperty("origin").GetString());
        Assert.True(record.GetProperty("rollup").GetBoolean());
        Assert.Equal(JsonValueKind.Array, record.GetProperty("overridden_by").ValueKind);
        Assert.Contains("<sub>", record.GetProperty("sub_line").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void JsonWithNoMatch_IsAnEmptyArrayAndExits1()
    {
        var (stdout, _, status) = Run("--req", "HTTP-7", "--json");
        Assert.Equal(1, status);
        Assert.Equal("[]", stdout.Trim());
    }

    [Fact]
    public void ListTopics_CountsEntriesIdsAndNotes()
    {
        var (stdout, _, _) = Run("--list-topics");
        Assert.Contains("http-domain-model\t3\t3\t0", stdout, StringComparison.Ordinal);
        Assert.Contains("pagination\t4\t3\t1", stdout, StringComparison.Ordinal);
        Assert.Contains("testing\t2\t0\t0", stdout, StringComparison.Ordinal);
        Assert.Contains("1 harvested topics carry no requirement ID at all", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Coverage_SplitsSubstantiveRollupAndUncited()
    {
        var (stdout, _, _) = Run("--coverage");
        Assert.Contains("HTTP\t2\t1\t1\t4\tHTTP-7", stdout, StringComparison.Ordinal);
        Assert.Contains("SEAM\t0\t0\t1\t1\tSEAM-1", stdout, StringComparison.Ordinal);
        Assert.Contains("5/9 canonical IDs have a substantive entry", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ListReqs_MapsEveryCitedIdToItsLocations()
    {
        var (stdout, _, _) = Run("--list-reqs");
        Assert.Contains("HTTP-1\thttp-domain-model.md:4", stdout, StringComparison.Ordinal);
        Assert.Contains("6 requirement IDs cited across the corpus", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void NoFilter_PrintsTheUsageRatherThanTheCorpus()
    {
        var (stdout, _, status) = Run();
        Assert.Equal(0, status);
        Assert.Contains("Usage: scripts/knowledge", stdout, StringComparison.Ordinal);
    }
}

public sealed class NoMatchTests : KnowledgeFixture
{
    [Fact]
    public void ZeroMatches_Exits1AndNamesTheBarrenFilter()
    {
        var (stdout, _, status) = Run("--req", "HTTP-7");
        Assert.Equal(1, status);
        Assert.Contains("HTTP-7 is canonical but no entry cites it yet.", stdout, StringComparison.Ordinal);
        Assert.Contains("nearest cited HTTP IDs: HTTP-2 HTTP-1 HTTP-70", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ANonCanonicalId_WarnsAndIsNamedAsSuch()
    {
        var (stdout, stderr, status) = Run("--req", "ZZZ-1");
        Assert.Equal(1, status);
        Assert.Contains("ZZZ-1 is not in appendix C", stderr, StringComparison.Ordinal);
        Assert.Contains("not a canonical requirement ID", stdout, StringComparison.Ordinal);
        Assert.Contains("no ZZZ ID is cited anywhere. cited prefixes: HTTP PAGE", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AFilterCombinationThatCannotHold_SaysSoInsteadOfBlamingOneSide()
    {
        var (stdout, _, status) = Run("--prefix", "HTTP", "--req", "PAGE-1");
        Assert.Equal(1, status);
        Assert.Contains("every filter matches something on its own", stdout, StringComparison.Ordinal);
        Assert.Contains("matching alone: reqs 1, prefixes 3", stdout, StringComparison.Ordinal);
        Assert.Contains("Filters AND together", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownTopic_ListsTheTopicsThatExist()
    {
        Assert.Contains("available: http-domain-model pagination testing", Run("--topic", "nosuchtopic").Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnresolvableKey_SaysTheRuleWasReworded()
    {
        Assert.Contains("A key digests the entry's text", Run("--key", "pagination/deadbeef").Stdout, StringComparison.Ordinal);
        Assert.Contains("no topic 'nosuch' exists", Run("--key", "nosuch/deadbeef").Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyChapterOrSection_ExplainsItself()
    {
        Assert.Contains("harvested chapters: 6 11", Run("--chapter", "3").Stdout, StringComparison.Ordinal);
        Assert.Contains("the Constraints section holds no entry in either tree.", Run("--section", "constraints").Stdout, StringComparison.Ordinal);
    }
}

/// <summary>A checkout that has the specification but has never been harvested — every port's first day.</summary>
public sealed class MissingCorpusTests : KnowledgeFixture
{
    public MissingCorpusTests() => Directory.Delete(FixturePath("docs/knowledge"), recursive: true);

    [Fact]
    public void Help_WorksWithNothingSetUpAtAll()
    {
        var (stdout, _, status) = RunRaw("--root", "/nonexistent", "--help");
        Assert.Equal(0, status);
        Assert.Contains("Usage: scripts/knowledge", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingAppendixC_IsExplainedAndExits1()
    {
        var (_, stderr, status) = RunRaw("--root", "/nonexistent", "--req", "HTTP-1");
        Assert.Equal(1, status);
        Assert.Contains("cannot read the canonical requirement index", stderr, StringComparison.Ordinal);
        Assert.Contains("--root", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingCorpus_IsExplainedAndExits1ForAQuery()
    {
        var (_, stderr, status) = Run("--req", "HTTP-1");
        Assert.Equal(1, status);
        Assert.Contains("does not exist under", stderr, StringComparison.Ordinal);
        Assert.Contains("harvested/", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Gaps_StillAnswersFromAppendixCAlone()
    {
        var (stdout, stderr, status) = Run("--gaps", "HTTP");
        Assert.Equal(0, status);
        Assert.Contains("reporting against appendix C alone", stderr, StringComparison.Ordinal);
        Assert.Contains("4 canonical IDs: 0 substantive, 0 roll-up only, 4 uncited", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCompanions_PassOverAnUnharvestedCheckout()
    {
        Assert.Contains("there are no two trees to keep apart yet", Run("verify-structure").Stdout, StringComparison.Ordinal);
        Assert.Contains("nothing can have drifted", Run("drift").Stdout, StringComparison.Ordinal);
    }
}
