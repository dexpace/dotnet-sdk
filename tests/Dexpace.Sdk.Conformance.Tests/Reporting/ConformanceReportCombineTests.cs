// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;
using static Dexpace.Sdk.Conformance.Tests.Support.TestAssertions;

namespace Dexpace.Sdk.Conformance.Tests.Reporting;

/// <summary>Plan 1.2 (P10-6, P10-7): merging the reports of several suites, and each suite's own preamble.</summary>
[Trait("Category", "Unit")]
public sealed class ConformanceReportCombineTests
{
    private static ConformanceReport Report(string suite, params ConformanceResult[] results) =>
        ConformanceReport.Create("subject", results, [], new SuiteIdentity(suite, suite + " preamble"));

    [Fact]
    public void Combine_concatenates_results_and_takes_the_worst_status_per_requirement()
    {
        var transport = Report("transport", Result(Make("transport-3.a", ["XCUT-1"]), ConformanceStatus.Passed));
        var invariant = Report("invariant", Result(Make("xcut-1.a", ["XCUT-1"], faces: [TransportFace.None]), ConformanceStatus.Failed, face: TransportFace.None));

        var combined = ConformanceReport.Combine("both", [transport, invariant]);

        Assert.Equal("both", combined.SubjectName);
        Assert.Equal("combined", combined.Suite);
        Assert.Equal(2, combined.Results.Count);
        Assert.Equal(ConformanceStatus.Failed, combined.ByRequirement["XCUT-1"]);
        Assert.False(combined.IsGreen);
    }

    [Fact]
    public void Combine_reads_across_suites_per_appendix_B_item()
    {
        var invariant = Report("invariant", Result(Make("xcut-1.a", ["XCUT-1"]), ConformanceStatus.Passed));
        var packaging = Report("packaging", Result(Make("nfr-1.a", ["NFR-1"]), ConformanceStatus.NotExercised));

        var combined = ConformanceReport.Combine("both", [invariant, packaging]);

        Assert.True(combined.IsGreen);
        Assert.Equal(ConformanceStatus.Passed, combined.ByRequirement["XCUT-1"]);
        Assert.Equal(ConformanceStatus.NotExercised, combined.ByRequirement["NFR-1"]);
    }

    [Fact]
    public void The_combined_waiver_list_is_the_union()
    {
        var one = new ConformanceWaiver("XCUT-1", "a");
        var two = new ConformanceWaiver("XCUT-2", "b");
        var left = ConformanceReport.Create("s", [], [one], new SuiteIdentity("invariant", "p"));
        var right = ConformanceReport.Create("s", [], [one, two], new SuiteIdentity("codec", "q"));

        var text = ConformanceReport.Combine("both", [left, right]).ToString();

        Assert.Contains("XCUT-1", text, StringComparison.Ordinal);
        Assert.Contains("XCUT-2", text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("XCUT-1 [", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Combine_of_zero_reports_and_null_guards()
    {
        Assert.Throws<ArgumentException>(() => ConformanceReport.Combine("x", []));
        Assert.Throws<ArgumentNullException>(() => ConformanceReport.Combine(null!, [Report("transport")]));
        Assert.Throws<ArgumentNullException>(() => ConformanceReport.Combine("x", null!));
        Assert.Throws<ArgumentNullException>(() => ConformanceReport.Combine("x", [Report("transport"), null!]));
    }

    [Fact]
    public void Each_suite_report_renders_its_own_preamble()
    {
        var transport = ConformanceReport.Create("subject", []);
        var invariant = Report("invariant");

        Assert.StartsWith(ConformanceReport.Preamble, transport.ToString(), StringComparison.Ordinal);
        Assert.Equal("transport", transport.Suite);
        Assert.StartsWith("invariant preamble", invariant.ToString(), StringComparison.Ordinal);
        Assert.Equal("invariant", invariant.Suite);
        Assert.Equal("invariant preamble", invariant.PreambleText);
    }

    [Fact]
    public void A_combined_report_carries_each_distinct_input_preamble_once()
    {
        var combined = ConformanceReport.Combine("both", [Report("invariant"), Report("codec"), Report("invariant")]);

        Assert.Equal("invariant preamble" + Environment.NewLine + Environment.NewLine + "codec preamble", combined.PreambleText);
    }
}
