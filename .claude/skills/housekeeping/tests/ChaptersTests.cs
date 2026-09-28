// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Housekeeping.Tests;

/// <summary>
/// The probe's ninth check, <c>chapters</c>: the clause rule, the ranges, the negations, and the two
/// regression fixtures. The blind spots are asserted too, so the check never claims to have seen what
/// it did not.
/// </summary>
public sealed partial class ChaptersTests
{
    // Chapter 03 as the spec carries it, trimmed to the SEAM IDs that matter: 13, 15 and 22 are not in
    // it (02 carries SEAM-13; appendix C alone carries 15 and 22).
    private const string Chapter03 = "# 3\n\nSEAM-11 SEAM-12 SEAM-14 SEAM-16 SEAM-17 SEAM-24 SEAM-25 SEAM-29 SEAM-30\n";
    private const string Chapter02 = "# 2\n\nSEAM-13 SEAM-29\n";
    private const string Chapter03Path = "docs/product-spec/03-pluggable-seams-and-extension-model.md";

    // The two regression fixtures, carried over verbatim from the Ruby port, where each was a real
    // governing-documents line (phase 8a's design :67, phase 8c's design :69-70) before it was
    // corrected. A check whose only evidence is a live defect loses its evidence the moment the defect
    // is fixed; these are the shapes the clause rule was designed against.
    private const string BacktickedRangeLine =
        "- `docs/product-spec/03-pluggable-seams-and-extension-model.md` for `SEAM-11`–`SEAM-15`, `SEAM-22`, `SEAM-29`\n" +
        "  -- a governing-documents line kept verbatim as a regression fixture.\n";

    private const string ContinuedListLine =
        "- `docs/product-spec/03-pluggable-seams-and-extension-model.md` — `SEAM-11`, `SEAM-13`, `SEAM-14`, `SEAM-15`, `SEAM-16`,\n" +
        "  `SEAM-17`, `SEAM-24`, `SEAM-25`, `SEAM-30`\n";

    private static IReadOnlyList<Finding> RunOver(string document, Chapters? check = null, string documentPath = "docs/work/mvp/x-design.md")
    {
        using var fixture = Fixture.Create(new()
        {
            [Chapter03Path] = Chapter03,
            ["docs/product-spec/02-architectural-principles.md"] = Chapter02,
            [documentPath] = document,
        });
        return (check ?? new Chapters()).Run(new Repo(fixture.Root));
    }

    private static string[] Ids(IReadOnlyList<Finding> findings) =>
        [.. findings.Select(f => SeamId().Match(f.Message).Value).Order(StringComparer.Ordinal)];

    [Fact]
    public void A_governing_documents_list_naming_an_appendix_c_only_id_is_a_finding() =>
        Assert.Equal(["SEAM-15"], Ids(RunOver($"`{Chapter03Path}` for `SEAM-11`, `SEAM-15`.\n")));

    [Fact]
    public void Prose_whose_subject_is_the_absence_is_not_a_finding() =>
        Assert.Empty(RunOver(
            $"`{Chapter03Path}` carries 22 of the 30 IDs; `SEAM-15` and `SEAM-22` appear nowhere in the specification's prose.\n"));

    [Fact]
    public void A_clause_boundary_reassigns_the_chapter() =>
        Assert.Empty(RunOver($"`{Chapter03Path}` for `SEAM-11`; `docs/product-spec/02-architectural-principles.md` for `SEAM-13`.\n"));

    // The backticked range is what hid SEAM-13 from a pattern written for bare text; with the range
    // read, all three wrong IDs are found.
    [Fact]
    public void The_backticked_range_regression_line_fires_on_all_three_wrong_ids() =>
        Assert.Equal(["SEAM-13", "SEAM-15", "SEAM-22"], Ids(RunOver(BacktickedRangeLine)));

    // SEAM-13 and SEAM-15 sit on the chapter's own line and are found. A clause continued onto the
    // NEXT line would be the stated blind spot -- see below.
    [Fact]
    public void The_continued_list_regression_line_fires_on_both_wrong_ids() =>
        Assert.Equal(["SEAM-13", "SEAM-15"], Ids(RunOver(ContinuedListLine)));

    [Fact]
    public void A_clause_continued_onto_the_next_line_is_a_stated_blind_spot()
    {
        Assert.Empty(RunOver($"- `{Chapter03Path}` for\n  `SEAM-15`.\n"));
        Assert.Contains("continued_clause", Chapters.Gaps);
    }

    [Fact]
    public void A_chapter_named_by_an_ellipsis_is_a_stated_blind_spot()
    {
        Assert.Empty(RunOver("`docs/product-spec/03-…md` for `SEAM-15`.\n"));
        Assert.Contains("dynamic_chapter_path", Chapters.Gaps);
    }

    [Fact]
    public void The_range_vocabulary_tolerates_backticks_and_a_bare_upper_bound()
    {
        Assert.Equal(
            [("03-x.md", "SEAM-11"), ("03-x.md", "SEAM-12"), ("03-x.md", "SEAM-13")],
            Chapters.Pairs("docs/product-spec/03-x.md for `SEAM-11`–`SEAM-13`").Take(3));
        Assert.Equal(3, Chapters.Pairs("docs/product-spec/03-x.md for SEAM-11–13").Count);
        Assert.Equal(3, Chapters.Pairs("docs/product-spec/03-x.md for SEAM-11—SEAM-13").Count);
    }

    // "appears in" is a FORWARD binding: the IDs before it belong to the chapter after it, so a wrong
    // one fires and a right one does not.
    [Fact]
    public void An_appears_in_attribution_binds_forward_and_fires_when_wrong()
    {
        Assert.Equal(["SEAM-15"], Ids(RunOver($"`SEAM-15` appears in `{Chapter03Path}`.\n")));
        Assert.Empty(RunOver($"`SEAM-11` appears in `{Chapter03Path}`.\n"));
    }

    // A second run followed by "appears in" and a chapter on the NEXT line must not bind backward to
    // the first run's chapter.
    [Fact]
    public void A_second_appears_in_run_does_not_bind_backward()
    {
        var document =
            "every one of `SEAM-11`–`SEAM-12` appears in\n" +
            $"`{Chapter03Path}` and every one of `SEAM-13` appears in\n" +
            "`docs/product-spec/02-architectural-principles.md`.\n";

        Assert.Empty(RunOver(document));
        Assert.Equal([("03-x.md", "SEAM-11")], Chapters.Pairs("`SEAM-11` appears in docs/product-spec/03-x.md"));
    }

    // Each spelling of the negated verb is a negation, not a forward binding.
    [Theory]
    [InlineData("does not appear in")]
    [InlineData("do not appear in")]
    [InlineData("did not appear in")]
    [InlineData("never appears in")]
    [InlineData("doesn't appear in")]
    [InlineData("don't appear in")]
    [InlineData("appears nowhere in")]
    public void A_negated_appears_in_is_not_an_attribution(string verb) =>
        Assert.Empty(RunOver($"`SEAM-15` {verb} `{Chapter03Path}`.\n"));

    [Fact]
    public void Appendix_c_is_never_a_target() =>
        Assert.Empty(RunOver("`docs/product-spec/appendix-c-consolidated-normative-requirement-index.md` for `SEAM-99`.\n"));

    [Fact]
    public void The_spec_itself_is_not_scanned() =>
        Assert.Empty(RunOver($"`{Chapter03Path}` for `SEAM-15`.\n", documentPath: "docs/product-spec/05-other.md"));

    [Fact]
    public void The_design_document_is_scanned() =>
        Assert.Equal(["SEAM-15"], Ids(RunOver($"`{Chapter03Path}` for `SEAM-15`.\n", documentPath: "docs/sdk-design-dotnet/03-seams.md")));

    // The Ruby port exempted its own phase-10 record as a source; this repository has no such record,
    // so the list is empty by default and configurable.
    [Fact]
    public void The_exemption_list_is_empty_by_default_and_exempts_by_path_prefix()
    {
        Assert.Empty(Chapters.DefaultExemptDocuments);
        var wrong = $"`{Chapter03Path}` for `SEAM-15`.\n";

        Assert.Single(RunOver(wrong));
        Assert.Empty(RunOver(wrong, new Chapters(["docs/work/mvp/"])));
    }

    [GeneratedRegex(@"SEAM-\d+")]
    private static partial Regex SeamId();
}
