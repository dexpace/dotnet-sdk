// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;
using static Dexpace.Sdk.Conformance.Tests.Support.TestAssertions;

namespace Dexpace.Sdk.Conformance.Tests.Reporting;

/// <summary>Design section C: the report's colour, its per-requirement worst-of view and its rendering.</summary>
[Trait("Category", "Unit")]
public sealed class ConformanceReportTests
{
    private static readonly ConformanceAssertion s_one = Make("transport-1.redirect-not-followed", ["TRANSPORT-1", "SEAM-13"]);
    private static readonly ConformanceAssertion s_two = Make("transport-1.second", ["TRANSPORT-1"]);
    private static readonly ConformanceAssertion s_three = Make("transport-3.cancel-is-terminal", ["TRANSPORT-3"]);

    [Theory]
    [InlineData(ConformanceStatus.Passed)]
    [InlineData(ConformanceStatus.Waived)]
    [InlineData(ConformanceStatus.Vacuous)]
    [InlineData(ConformanceStatus.NotExercised)]
    public void A_report_without_failed_or_errored_results_is_green(ConformanceStatus status)
    {
        var report = ConformanceReport.Create("subject", [Result(s_one, ConformanceStatus.Passed), Result(s_three, status)]);

        Assert.True(report.IsGreen);
    }

    [Theory]
    [InlineData(ConformanceStatus.Failed)]
    [InlineData(ConformanceStatus.Errored)]
    public void One_failed_or_errored_result_makes_the_report_red(ConformanceStatus status)
    {
        var report = ConformanceReport.Create("subject", [Result(s_one, ConformanceStatus.Passed), Result(s_three, status)]);

        Assert.False(report.IsGreen);
    }

    [Fact]
    public void By_requirement_is_the_worst_status_of_every_result_citing_the_id()
    {
        var report = ConformanceReport.Create(
            "subject",
            [
                Result(s_one, ConformanceStatus.Passed),
                Result(s_two, ConformanceStatus.Failed),
                Result(s_three, ConformanceStatus.Passed),
            ]);

        Assert.Equal(ConformanceStatus.Failed, report.ByRequirement["TRANSPORT-1"]);
        Assert.Equal(ConformanceStatus.Passed, report.ByRequirement["SEAM-13"]);
        Assert.Equal(ConformanceStatus.Passed, report.ByRequirement["TRANSPORT-3"]);
        Assert.False(report.ByRequirement.ContainsKey("TRANSPORT-2"));
    }

    [Fact]
    public void A_multi_id_assertion_fans_out_to_each_of_its_ids()
    {
        var report = ConformanceReport.Create("subject", [Result(s_one, ConformanceStatus.Errored)]);

        Assert.Equal(ConformanceStatus.Errored, report.ByRequirement["TRANSPORT-1"]);
        Assert.Equal(ConformanceStatus.Errored, report.ByRequirement["SEAM-13"]);
    }

    [Fact]
    public void The_results_are_a_read_only_defensive_copy()
    {
        var results = new List<ConformanceResult> { Result(s_one, ConformanceStatus.Passed) };
        var report = ConformanceReport.Create("subject", results);

        results.Add(Result(s_three, ConformanceStatus.Failed));

        Assert.Single(report.Results);
        Assert.True(report.IsGreen);
        Assert.IsNotType<List<ConformanceResult>>(report.Results);
        Assert.Equal("subject", report.SubjectName);
    }

    [Fact]
    public void Rendering_starts_with_the_preamble_which_says_what_a_green_run_does_not_prove()
    {
        var text = ConformanceReport.Create("subject", [Result(s_one, ConformanceStatus.Passed)]).ToString();

        Assert.StartsWith(ConformanceReport.Preamble, text, StringComparison.Ordinal);
        Assert.Contains("does not prove", ConformanceReport.Preamble, StringComparison.Ordinal);
        Assert.Contains("TLS", ConformanceReport.Preamble, StringComparison.Ordinal);
        Assert.Contains("HTTP/2", ConformanceReport.Preamble, StringComparison.Ordinal);
        Assert.Contains("connect", ConformanceReport.Preamble, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rendering_lists_requirement_ids_in_natural_order()
    {
        var ten = Make("transport-10.x", ["TRANSPORT-10"]);
        var two = Make("transport-2.x", ["TRANSPORT-2"]);
        var text = ConformanceReport.Create("subject", [Result(ten, ConformanceStatus.Passed), Result(two, ConformanceStatus.Passed)]).ToString();

        Assert.True(text.IndexOf("TRANSPORT-2 ", StringComparison.Ordinal) < text.IndexOf("TRANSPORT-10 ", StringComparison.Ordinal));
    }

    [Fact]
    public void Rendering_lists_every_waiver_even_when_the_run_is_green()
    {
        var waiver = new ConformanceWaiver("TRANSPORT-8", "F3: a borrowed client cancelled internally") { Owner = "8b" };
        var waived = Result(s_three, ConformanceStatus.Waived, "waived") with { Waiver = waiver };
        var report = ConformanceReport.Create("subject", [Result(s_one, ConformanceStatus.Passed), waived]);

        Assert.True(report.IsGreen);
        var text = report.ToString();
        Assert.Contains("F3: a borrowed client cancelled internally", text, StringComparison.Ordinal);
        Assert.Contains("8b", text, StringComparison.Ordinal);
        Assert.Contains("TRANSPORT-8", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Rendering_lists_every_not_exercised_assertion_with_its_reason()
    {
        var proxy = Make("transport-30.proxy-discoverable-no-leak", ["TRANSPORT-30"]);
        var text = ConformanceReport.Create(
            "subject",
            [Result(proxy, ConformanceStatus.NotExercised, "the subject supplies no CreateWithProxy hook")]).ToString();

        Assert.Contains("transport-30.proxy-discoverable-no-leak", text, StringComparison.Ordinal);
        Assert.Contains("the subject supplies no CreateWithProxy hook", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Rendering_lists_failures_with_their_detail_and_is_stable()
    {
        var report = ConformanceReport.Create("subject", [Result(s_three, ConformanceStatus.Failed, "expected 520, was 502", TransportFace.Blocking)]);

        var text = report.ToString();
        Assert.Contains("transport-3.cancel-is-terminal", text, StringComparison.Ordinal);
        Assert.Contains("expected 520, was 502", text, StringComparison.Ordinal);
        Assert.Contains("Blocking", text, StringComparison.Ordinal);
        Assert.Equal(text, report.ToString());
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ConformanceReport.Create(null!, []));
        Assert.Throws<ArgumentNullException>(() => ConformanceReport.Create("s", null!));
    }
}
