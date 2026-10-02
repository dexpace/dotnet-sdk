// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Tools.Knowledge.Tests;

public sealed class OverrideTests : KnowledgeFixture
{
    [Fact]
    public void ANoteCitingAKey_LinksBothEnds()
    {
        Assert.Equal(["notes/pagination.md:8"], EntryWithKey(ReferencePage2).OverriddenBy);
        Assert.Equal([ReferencePage2], Corpus.Entries.Single(entry => entry.IsNote).Overrides);
    }

    [Fact]
    public void TheOverriddenEntry_IsTaggedWhereverItIsReturned()
    {
        var (stdout, _, _) = Run("--key", ReferencePage2);
        Assert.Contains("[overridden by notes/pagination.md:8]", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AKeyThatResolvesToNothing_IsReportedAsDangling()
    {
        Assert.Empty(Corpus.DanglingKeys());
        var notesOnly = new TopicParser(Appendix.Prefixes)
            .Parse(FixturePath("docs/knowledge/notes/pagination.md"), "note");
        Assert.Single(new Corpus(notesOnly).DanglingKeys());
    }
}

/// <summary>
/// A note cites a harvested key for two different reasons, and the corpus has to tell them apart: it
/// OVERRIDES the rule it corrects, and it CITES the rules it leans on. The signal is the note's own relation
/// verb immediately before the key it governs.
/// </summary>
public sealed class RelationTests : KnowledgeFixture
{
    private const string NotePath = "docs/knowledge/notes/http-domain-model.md";

    private static readonly string s_supportNote = $"""
        # http-domain-model — notes

        Hand-written. This entry leans on a harvested rule; it does not correct it.

        ## Reference
        - **A citation in support, not a correction.** Adds to `{RulePage1}`, which the decision rests on.
          <sub>review · `docs/work/mvp/phase1/phase1a/2026-01-01-phase1a-http.md` · high · sha:manual-fixture-support</sub>

        """;

    private static readonly string s_mixedNote = $"""
        # http-domain-model — notes

        Hand-written. One entry, one correction, one supporting citation.

        ## Superseded
        - **One relation verb scopes one key.** Supersedes `{RulePage1}`, and rests on `{ReferencePage2}`, which is unchanged. Restating `{RulePage1}` does not name it twice.
          <sub>review · `docs/work/mvp/phase1/phase1a/2026-01-01-phase1a-http.md` · high · sha:manual-fixture-mixed</sub>

        """;

    [Fact]
    public void AKeyCitedInSupport_IsACitationAndNotAnOverride()
    {
        WriteFixture(NotePath, s_supportNote);
        var rule = EntryWithKey(RulePage1);
        Assert.Equal(["notes/http-domain-model.md:6"], rule.CitedBy);
        Assert.Empty(rule.OverriddenBy);
    }

    [Fact]
    public void ACitedEntry_PrintsCitedByRatherThanOverriddenBy()
    {
        WriteFixture(NotePath, s_supportNote);
        var (stdout, _, _) = Run("--key", RulePage1);
        Assert.Contains("[cited by notes/http-domain-model.md:6]", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("[overridden by", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ARelationVerb_ScopesOnlyTheKeysThatFollowIt()
    {
        WriteFixture(NotePath, s_mixedNote);
        Assert.Equal(["notes/http-domain-model.md:6"], EntryWithKey(RulePage1).OverriddenBy);
        Assert.Empty(EntryWithKey(RulePage1).CitedBy);
        Assert.Equal(["notes/http-domain-model.md:6"], EntryWithKey(ReferencePage2).CitedBy);
    }

    [Fact]
    public void AKeyNamedTwiceInOneNote_IsListedOnce()
    {
        WriteFixture(NotePath, s_mixedNote);
        var note = Corpus.Entries.Single(entry => entry.IsNote && entry.Topic == "http-domain-model");
        Assert.Equal([RulePage1], note.Overrides);
        Assert.Equal([ReferencePage2], note.Cites);
    }

    [Fact]
    public void TheDriftReport_CountsASupportingCitationAsResolved()
    {
        WriteFixture(NotePath, s_supportNote);
        var (stdout, _, status) = Run("drift");
        Assert.Equal(0, status);
        Assert.Contains("2 note citation(s) resolve, 0 do not.", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void OverriddenKeys_ReadsEveryRelationVerbForm()
    {
        foreach (var verb in new[] { "Supersedes", "superseding", "Resolves", "Answers", "Narrows", "Corrects", "Overrides", "Replaces" })
        {
            Assert.Equal(["a/0123abcd", "b/4567cdef"], Corpus.OverriddenKeys($"{verb} `a/0123abcd` and `b/4567cdef`."));
        }

        Assert.Empty(Corpus.OverriddenKeys("Adds to `a/0123abcd`."));
    }
}

/// <summary>
/// A Conflicts entry ends its source line in a status. Without a tag, a conflict the port settled by keeping
/// the departure or by conforming prints exactly like one nobody has decided.
/// </summary>
public sealed class ConflictStatusTests : KnowledgeFixture
{
    private const string Topic = """
        # naming

        ## Conflicts
        - **Open one** — nobody decided.
          <sub>styleguide `docs/a.md:1-2` · design `docs/b.md:3-4` · unresolved 2026-10-02</sub>
        - **Kept one** — the port keeps it.
          <sub>styleguide `docs/a.md:1-2` · design `docs/b.md:3-4` · kept 2026-10-02</sub>
        - **Conformed one** — the port conforms.
          <sub>styleguide `docs/a.md:1-2` · design `docs/b.md:3-4` · conformed 2026-10-02</sub>
        """;

    [Fact]
    public void KeptAndConformedConflictsAreTaggedAndAnOpenOneIsNot()
    {
        WriteFixture("docs/knowledge/harvested/naming.md", Topic);
        var (stdout, _, _) = Run("--section", "conflicts", "--topic", "naming", "--brief", "--no-drift-check");
        var lines = stdout.Split('\n');
        Assert.DoesNotMatch(@"\[(kept|conformed)\]", lines.Single(l => l.Contains("(Conflicts)", StringComparison.Ordinal) && l.Contains(":4 ", StringComparison.Ordinal)));
        Assert.Contains(lines, l => l.Contains(":6 ", StringComparison.Ordinal) && l.Contains("[kept]", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains(":8 ", StringComparison.Ordinal) && l.Contains("[conformed]", StringComparison.Ordinal));
    }
}
