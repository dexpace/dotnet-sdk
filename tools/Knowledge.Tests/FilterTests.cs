// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Tools.Knowledge.Tests;

public sealed class FilterTests : KnowledgeFixture
{
    [Fact]
    public void Req_IsExactToken()
    {
        Assert.Equal([RuleHttp70], Select(o => o.Req.Add("HTTP-70")).Select(e => e.Key));
        Assert.Empty(Select(o => o.Req.Add("HTTP-7")));
    }

    [Fact]
    public void ValuesWithinOneFilter_Or()
    {
        var keys = Select(o => o.Req.Add("HTTP-1,PAGE-1")).Select(e => e.Key).Order(StringComparer.Ordinal);
        Assert.Equal(new[] { RuleHttp1, RulePage1 }.Order(StringComparer.Ordinal), keys);
    }

    [Fact]
    public void RepeatedFlags_OrLikeCommaValues()
    {
        Assert.Equal(2, Select(o => { o.Req.Add("HTTP-1"); o.Req.Add("PAGE-1"); }).Count);
    }

    [Fact]
    public void DifferentFilters_And()
    {
        Assert.Empty(Select(o => { o.Req.Add("HTTP-1"); o.Topic.Add("pagination"); }));
        Assert.Single(Select(o => { o.Req.Add("PAGE-1"); o.Topic.Add("pagination"); }));
    }

    [Fact]
    public void Prefix_TakesAWholeFamily()
    {
        Assert.Equal(3, Select(o => o.Prefix.Add("HTTP")).Count);
        // Five: the four harvested pagination entries plus the note that cites PAGE-2.
        Assert.Equal(5, Select(o => o.Prefix.Add("page")).Count);
    }

    [Fact]
    public void Prefix_IsValidatedAgainstAppendixC()
    {
        var error = Assert.Throws<UsageException>(() => Select(o => o.Prefix.Add("UTF")));
        Assert.Contains("not a requirement-ID prefix in appendix C", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Section_IsCaseInsensitiveAndValidated()
    {
        Assert.Equal(6, Select(o => o.Section.Add("rules")).Count);
        Assert.Throws<UsageException>(() => Select(o => o.Section.Add("rulez")));
    }

    [Fact]
    public void Origin_SplitsTheTwoTrees()
    {
        Assert.Single(Select(o => o.Origin.Add("note")));
        Assert.Equal(9, Select(o => o.Origin.Add("harvested")).Count);
        Assert.Throws<UsageException>(() => Select(o => o.Origin.Add("harvest")));
    }

    [Fact]
    public void Role_MatchesAnyRoleOnTheEntry()
    {
        Assert.Single(Select(o => o.Role.Add("review")));
        Assert.Equal(2, Select(o => o.Role.Add("design")).Count);
        Assert.Equal(3, Select(o => o.Role.Add("styleguide")).Count);
        Assert.Throws<UsageException>(() => Select(o => o.Role.Add("reviewer")));
    }

    [Fact]
    public void Topic_IsASubstringOfTheFileName()
    {
        Assert.Equal(5, Select(o => o.Topic.Add("pagination")).Count);
        Assert.Equal(2, Select(o => o.Topic.Add("testing")).Count);
        Assert.Equal(5, Select(o => o.Topic.Add("pagin")).Count);
    }

    [Fact]
    public void Chapter_MatchesOnlyStyleguideSources()
    {
        Assert.Equal(2, Select(o => o.Chapter.Add("11")).Count);
        Assert.Single(Select(o => o.Chapter.Add("06")));
        // The spec chapter 04 is a numbered file too, and must not answer "chapter 4".
        Assert.Empty(Select(o => o.Chapter.Add("4")));
        Assert.Throws<UsageException>(() => Select(o => o.Chapter.Add("six")));
    }

    [Fact]
    public void AChapterWithASectionNumber_DropsTheSectionNumber()
    {
        using var warn = new StringWriter();
        var query = BuildQuery(o => o.Chapter.Add("6.7"), warn);
        Assert.Equal(["6"], query.Chapters);
        Assert.Contains("ignoring .7", warn.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BareWordsAndGrep_AreCaseInsensitiveAndAllMustMatch()
    {
        Assert.Single(Select(_ => { }, "XUNIT", "header"));
        Assert.Empty(Select(_ => { }, "xunit", "pagination"));
        Assert.Equal(3, Select(o => o.Grep.Add("closeable|eager-close")).Count);
    }

    [Fact]
    public void BareWords_AreLiteralNotRegex()
    {
        Assert.Empty(Select(_ => { }, "closeable|eager-close"));
    }

    [Fact]
    public void AnInvalidGrep_IsAUsageError()
    {
        var error = Assert.Throws<UsageException>(() => Select(o => o.Grep.Add("(unclosed")));
        Assert.Contains("is not a valid regex", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_SelectsTheSingleEntryAndMustLookLikeAKey()
    {
        Assert.Equal([RuleHttp1], Select(o => o.Key.Add(RuleHttp1)).Select(e => e.Key));
        Assert.Throws<UsageException>(() => Select(o => o.Key.Add("not-a-key")));
    }

    [Fact]
    public void AnAllEmptyFilter_IsRefusedRatherThanMatchingEverything()
    {
        var error = Assert.Throws<UsageException>(() => Select(o => o.Topic.Add("")));
        Assert.Contains("would print the whole corpus", error.Message, StringComparison.Ordinal);
        Assert.Throws<UsageException>(() => Select(o => o.Topic.Add(",")));
        Assert.Throws<UsageException>(() => Select(o => o.Grep.Add("  ")));
    }

    [Fact]
    public void ATrailingComma_KeepsTheRealValues()
    {
        Assert.Equal(5, Select(o => o.Topic.Add("pagination,")).Count);
    }

    [Fact]
    public void AnUnknownReq_OnlyWarns()
    {
        using var warn = new StringWriter();
        var query = BuildQuery(o => o.Req.Add("ZZZ-1"), warn);
        Assert.Equal(["ZZZ-1"], query.Reqs);
        Assert.Contains("ZZZ-1 is not in appendix C", warn.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BadEnumValues_ExitTwoFromTheCli()
    {
        foreach (var argv in new[]
                 {
                     new[] { "--role", "bogus" }, ["--section", "rulez"], ["--chapter", "x"], ["--origin", "nope"],
                     ["--prefix", "UTF"], ["--gaps", "UTF"], ["--prefix-info", "UTF"], ["--bogus-flag"], ["--req"],
                 })
        {
            Assert.Equal(2, Run(argv).Status);
        }
    }

    [Fact]
    public void Cli_AcceptsEqualsFormAndOptionsAfterWords()
    {
        var (stdout, _, status) = Run("xunit", "--topic=testing", "--brief", "--no-drift-check");
        Assert.Equal(0, status);
        Assert.Contains("1 entry across 1 topic file", stdout, StringComparison.Ordinal);
    }
}
