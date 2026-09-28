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
}

/// <summary>
/// <c>--prefix P --section rules</c> answers a different question from <c>--prefix-info P</c>, so a
/// narrowed prefix query says which canonical IDs it did NOT cover, and why each is missing.
/// </summary>
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
