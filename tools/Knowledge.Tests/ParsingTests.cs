// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Xunit;

namespace Dexpace.Tools.Knowledge.Tests;

public sealed class CanonicalIdsTests : KnowledgeFixture
{
    [Fact]
    public void AppendixC_ParsesIdLevelAndSubsystem()
    {
        Assert.Equal(9, Appendix.Count);
        Assert.Equal("MUST", Appendix["HTTP-1"].Level);
        Assert.Equal("SHOULD", Appendix["HTTP-7"].Level);
        Assert.Equal("Core HTTP domain model", Appendix["HTTP-70"].Subsystem);
    }

    [Fact]
    public void PrefixAllowlist_IsDerivedFromTheTable()
    {
        Assert.Equal(["HTTP", "PAGE", "SEAM"], Appendix.Prefixes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Allowlist_RejectsTheShapesABareRegexClaims()
    {
        const string Text = "Encode as UTF-8, hash with SHA-256, per RFC-3986 and ISO-8601, see HTTP-1.";
        Assert.Equal(["HTTP-1"], Ids.Extract(Text, Appendix.Prefixes));
        foreach (var prefix in new[] { "UTF", "SHA", "RFC", "ISO" })
        {
            Assert.DoesNotContain(prefix, Appendix.Prefixes);
        }
    }

    [Fact]
    public void Extraction_IsExactToken_SoHttp7DoesNotMatchHttp70()
    {
        Assert.Equal(["HTTP-70"], Ids.Extract("Covers HTTP-70 but not the short one.", Appendix.Prefixes));
    }

    [Fact]
    public void Extraction_DeDuplicatesAndKeepsFirstSeenOrder()
    {
        Assert.Equal(["PAGE-2", "HTTP-1"], Ids.Extract("PAGE-2 then HTTP-1 then PAGE-2 again.", Appendix.Prefixes));
    }

    [Fact]
    public void PlainExtraction_StillReadsARangeAsItsTwoEndpoints()
    {
        // Entry citations and spec prose keep the exact-token reading; only --phase expands ranges.
        Assert.Equal(["HTTP-1", "HTTP-7"], Ids.Extract("HTTP-1\u2013HTTP-7", Appendix.Prefixes));
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
    public void Ranges_ExpandToTheCanonicalIdsBetweenTheEndpoints(string citation)
    {
        // Appendix C holds HTTP-1, 2, 7 and 70 in the fixture, so 3 to 6 are not requirements to credit.
        Assert.Equal(
            ["HTTP-1", "HTTP-2", "HTTP-7"],
            Ids.ExtractWithRanges($"See {citation}.", Appendix.Prefixes, Appendix.AllIds));
    }

    [Fact]
    public void Ranges_ComposeWithPlainTokensInFirstSeenOrderWithoutDuplicates()
    {
        Assert.Equal(
            ["PAGE-4", "PAGE-1", "PAGE-2", "PAGE-3", "HTTP-70"],
            Ids.ExtractWithRanges("PAGE-4, then PAGE-1..4, then HTTP-70 and PAGE-2.", Appendix.Prefixes, Appendix.AllIds));
    }

    [Theory]
    [InlineData("HTTP-7\u2013HTTP-1", new[] { "HTTP-7", "HTTP-1" })]
    [InlineData("HTTP-2..2", new[] { "HTTP-2" })]
    [InlineData("HTTP-1\u2013PAGE-2", new[] { "HTTP-1", "PAGE-2" })]
    [InlineData("HTTP-1 \u2013 7", new[] { "HTTP-1" })]
    [InlineData("HTTP-70 - 3 retries", new[] { "HTTP-70" })]
    [InlineData("UTF-8\u2013UTF-16 and SHA-1..5", new string[0])]
    public void Ranges_ThatAreNotRangesCreditOnlyTheEndpointsTheyName(string citation, string[] expected)
    {
        Assert.Equal(expected, Ids.ExtractWithRanges(citation, Appendix.Prefixes, Appendix.AllIds));
    }

    [Fact]
    public void Ranges_ReadBackWhatCompressPrints()
    {
        // `--phase` prints a document's IDs as `PAGE-1..4`; a checklist pasted from that output must credit the same set.
        string[] ids = ["HTTP-7", "PAGE-1", "PAGE-2", "PAGE-3", "PAGE-4"];
        var compressed = Ids.Compress(ids);
        Assert.Equal("HTTP-7 PAGE-1..4", compressed);
        Assert.Equal(ids, Ids.ExtractWithRanges(compressed, Appendix.Prefixes, Appendix.AllIds));
    }

    [Fact]
    public void Ranges_AreBoundedByAppendixCNotByTheirEndpoints()
    {
        Assert.Equal(
            ["HTTP-1", "HTTP-2", "HTTP-7", "HTTP-70", "HTTP-9999999999"],
            Ids.ExtractWithRanges("HTTP-1\u2013HTTP-9999999999", Appendix.Prefixes, Appendix.AllIds));
        Assert.Equal(
            ["PAGE-3", "PAGE-4", "PAGE-99999999999999999999999999"],
            Ids.ExtractWithRanges("PAGE-3..99999999999999999999999999", Appendix.Prefixes, Appendix.AllIds));
    }

    [Fact]
    public void Ids_SortNumericallyWithinAPrefix()
    {
        Assert.Equal(
            ["HTTP-1", "HTTP-2", "HTTP-7", "HTTP-70", "PAGE-1"],
            Ids.Sort(["PAGE-1", "HTTP-70", "HTTP-7", "HTTP-2", "HTTP-1"]));
    }

    [Fact]
    public void Ids_CompressRunsIntoRanges()
    {
        Assert.Equal("PAGE-1..4", Ids.Compress(["PAGE-4", "PAGE-1", "PAGE-2", "PAGE-3"]));
        Assert.Equal("HTTP-1 HTTP-2 PAGE-1", Ids.Compress(["PAGE-1", "HTTP-1", "HTTP-2"]));
    }

    [Fact]
    public void SubsystemAndOwningChapter_ComeFromTheTableNotARoutingMap()
    {
        Assert.Equal("Core HTTP domain model", Appendix.SubsystemFor("HTTP"));
        Assert.Equal("04-core-http-domain-model.md", Appendix.ChapterFor("HTTP"));
        Assert.Equal("12-pagination.md", Appendix.ChapterFor("PAGE"));
        // "Product vision, pluggable seams and extension model" -> chapter 03.
        Assert.Equal("03-pluggable-seams-and-extension-model.md", Appendix.ChapterFor("SEAM"));
    }

    [Fact]
    public void Levels_AreCountedPerPrefix()
    {
        Assert.Equal(
            [new KeyValuePair<string, int>("MUST", 3), new KeyValuePair<string, int>("MAY", 1)],
            Appendix.LevelsFor("PAGE"));
    }
}

public sealed class SubLineTests
{
    [Fact]
    public void TheCommonShape_IsRoleSourceConfidenceSha()
    {
        var parsed = SubLine.Parse("spec · `docs/product-spec/04.md:9` · high · sha:abc123");
        Assert.Equal(["spec"], parsed.Roles);
        Assert.Equal(["docs/product-spec/04.md:9"], parsed.Sources);
        Assert.Equal("high", parsed.Confidence);
        Assert.Equal("abc123", parsed.Sha);
    }

    [Fact]
    public void AConflictsLine_CarriesTwoRoleSourcePairsAndNoSha()
    {
        var parsed = SubLine.Parse("design `a/b.md:1` · styleguide `/c/d.md:2` · unresolved 2026-01-01");
        Assert.Equal(["design", "styleguide"], parsed.Roles);
        Assert.Equal(["a/b.md:1", "/c/d.md:2"], parsed.Sources);
        Assert.Null(parsed.Sha);
        Assert.Equal("unresolved 2026-01-01", parsed.Confidence);
    }
}

public sealed class ParsingTests : KnowledgeFixture
{
    [Fact]
    public void Entries_AreAttributedToTheHeadingAboveThem()
    {
        Assert.Equal("Rules", EntryWithKey(RuleHttp1).Section);
        Assert.Equal("Reference", EntryWithKey(RollupHttp2).Section);
        Assert.Contains(Corpus.Entries, entry => entry.Section == "Conflicts");
    }

    [Fact]
    public void AnEntry_RecordsItsLineRoleSourceAndReqs()
    {
        var found = EntryWithKey(RuleHttp1);
        Assert.Equal(4, found.Line);
        Assert.Equal("spec", found.Role);
        Assert.Equal("docs/product-spec/04-core-http-domain-model.md:9", found.Source);
        Assert.Equal(["HTTP-1"], found.Reqs);
    }

    [Fact]
    public void Notes_AreLoadedFromTheSecondTreeWithTheirOwnOrigin()
    {
        var note = Corpus.Entries.Single(entry => entry.IsNote);
        Assert.Equal("note", note.Origin);
        Assert.Equal("review", note.Role);
        Assert.Equal("notes/pagination.md:8", note.Location);
        Assert.Equal("manual-fixture-erratum", note.Sha);
    }

    [Fact]
    public void IndexSourcesAndReadme_AreNotTopicFiles()
    {
        Assert.Equal(["http-domain-model", "pagination", "testing"], Corpus.Topics());
    }

    [Fact]
    public void ACrlfOrBomTopicFile_StillParses()
    {
        WriteBytes("windows.md",
            "﻿# t\r\n\r\n## Rules\r\n- A rule (HTTP-1).\r\n" +
            "  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:9` · high · sha:abc123456789</sub>\r\n");
        var entries = new TopicParser(Appendix.Prefixes).Parse(FixturePath("windows.md"), "harvested");
        var entry = Assert.Single(entries);
        Assert.Equal("Rules", entry.Section);
        Assert.Equal(["HTTP-1"], entry.Reqs);
    }

    [Fact]
    public void AMultiParagraphBullet_KeepsItsTailAndTheIdsItCites()
    {
        WriteFixture("multi.md",
            "# t\n\n## Conflicts\n- **A long conflict** opens here.\n\n  It continues, citing PAGE-3.\n" +
            "  <sub>design `a.md:1` · styleguide `b.md:2` · unresolved</sub>\n");
        var entry = Assert.Single(new TopicParser(Appendix.Prefixes).Parse(FixturePath("multi.md"), "harvested"));
        Assert.Equal("**A long conflict** opens here. It continues, citing PAGE-3.", entry.Text);
        Assert.Equal(["PAGE-3"], entry.Reqs);
    }

    [Fact]
    public void ASecondSubLine_AddsItsSourcesInsteadOfHidingTheFirst()
    {
        WriteFixture("twice.md",
            "# t\n\n## Rules\n- A rule.\n  <sub>spec · `docs/product-spec/a.md:1` · high · sha:abc123456789</sub>\n" +
            "  <sub>review · `docs/work/x.md` · high · sha:manual-x</sub>\n");
        var entry = Assert.Single(new TopicParser(Appendix.Prefixes).Parse(FixturePath("twice.md"), "harvested"));
        Assert.Equal(["spec", "review"], entry.Roles);
        Assert.Equal(["docs/product-spec/a.md:1", "docs/work/x.md"], entry.Sources);
        Assert.Equal("abc123456789", entry.Sha);
    }

    [Fact]
    public void AnUnreadableTopicFile_NamesItselfInTheError()
    {
        var error = Assert.Throws<UsageException>(
            () => new TopicParser(Appendix.Prefixes).Parse(FixturePath("missing.md"), "harvested"));
        Assert.Contains("missing.md", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntryKey_IsTopicSlashEightHexDigestedFromTheTextAlone()
    {
        Assert.Matches("^http-domain-model/[0-9a-f]{8}$", EntryWithKey(RuleHttp1).Key);
        // Trailing whitespace is not part of the rule; the topic scopes the key.
        Assert.Equal(TopicParser.EntryKey("t", "A rule."), TopicParser.EntryKey("t", "A rule.  \t"));
        Assert.NotEqual(TopicParser.EntryKey("t", "A rule."), TopicParser.EntryKey("u", "A rule."));
    }

    [Fact]
    public void AnEntryKey_SurvivesAReorderButNotARewording()
    {
        var path = "docs/knowledge/harvested/http-domain-model.md";
        var original = File.ReadAllText(FixturePath(path));
        var lines = original.Split('\n');
        // Swap the two Rules bullets (lines 4-5 and 6-7): same text, new line numbers.
        WriteFixture(path, string.Join('\n', [.. lines[..3], lines[5], lines[6], lines[3], lines[4], .. lines[7..]]));
        Assert.Equal(6, EntryWithKey(RuleHttp1).Line);

        WriteFixture(path, original.Replace("uppercased at construction", "upper-cased when built", StringComparison.Ordinal));
        Assert.DoesNotContain(Corpus.Entries, entry => entry.Key == RuleHttp1);
    }

    private void WriteBytes(string relative, string content) =>
        File.WriteAllBytes(FixturePath(relative), Encoding.UTF8.GetBytes(content));
}

public sealed class RollupTests : KnowledgeFixture
{
    [Fact]
    public void AnEntrySourcedOnlyFromAppendixB_IsARollup()
    {
        Assert.True(EntryWithKey(RollupHttp2).IsRollup);
    }

    [Fact]
    public void AnEntrySourcedFromARealChapter_IsNot()
    {
        Assert.False(EntryWithKey(RuleHttp1).IsRollup);
        Assert.False(EntryWithKey(ReferencePage2).IsRollup);
    }

    [Fact]
    public void ARollupOnlyResult_PrintsTheWarningAndTheTag()
    {
        var (stdout, _, _) = Run("--req", "HTTP-2");
        Assert.Contains("[appendix-B roll-up]", stdout, StringComparison.Ordinal);
        Assert.Contains("WARNING: every result is an appendix-B conformance roll-up", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ASubstantiveResult_PrintsNeither()
    {
        var (stdout, _, _) = Run("--req", "HTTP-1");
        Assert.DoesNotContain("roll-up", stdout, StringComparison.Ordinal);
    }
}
