// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Conformance;

/// <summary>
/// The invariant suite driven against core (phase 10, P10-5): one case per (assertion, face), plus the whole suite as a report. This
/// driver supplies the three hooks that reach core's internals (<c>MapStrict</c>, <c>CreateCyclicException</c>, <c>CreateBoundedMap</c>),
/// so all but <c>xcut-23</c> are exercised; the list of what is not is pinned (P10-28), so a missing hook never reads as conformance. No
/// assertion is waived.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class InvariantConformanceTests
{
    public static TheoryData<string, TransportFace> Rows()
    {
        var rows = new TheoryData<string, TransportFace>();
        foreach (var assertion in InvariantSuite.Assertions)
        {
            foreach (var face in assertion.Faces)
            {
                rows.Add(assertion.Name, face);
            }
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Assertion_holds(string name, TransportFace face)
    {
        var assertion = InvariantSuite.Assertions.Single(a => a.Name == name);
        var result = await InvariantSuite.RunAsync(InvariantCoreSubject.Create(), assertion, face, InvariantCoreSubject.Options, TestContext.Current.CancellationToken);

        if (result.Status == ConformanceStatus.Passed)
        {
            return;
        }

        if (result.Status is ConformanceStatus.Failed or ConformanceStatus.Errored)
        {
            Assert.Fail(result.Detail);
        }
        else
        {
            Assert.Skip($"{result.Status}: {result.Detail}");
        }
    }

    [Fact]
    public async Task The_whole_suite_is_green_and_its_report_is_written()
    {
        var report = await InvariantSuite.RunAllAsync(InvariantCoreSubject.Create(), InvariantCoreSubject.Options, TestContext.Current.CancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        Assert.True(report.IsGreen, report.ToString());
        Assert.Equal("invariant", report.Suite);
    }

    [Fact]
    public async Task Only_xcut_23_is_not_exercised_and_nothing_else_is_vacuous_or_waived()
    {
        var report = await InvariantSuite.RunAllAsync(InvariantCoreSubject.Create(), InvariantCoreSubject.Options, TestContext.Current.CancellationToken);

        var notExercised = report.Results.Where(static r => r.Status == ConformanceStatus.NotExercised).Select(static r => r.Assertion.Name).Distinct().Order(StringComparer.Ordinal);
        Assert.Equal(InvariantCoreSubject.ExpectedNotExercised, notExercised);
        Assert.DoesNotContain(report.Results, static r => r.Status is ConformanceStatus.Vacuous or ConformanceStatus.Waived);
    }

    [Fact]
    public void No_assertion_is_waived_by_this_driver() => Assert.Empty(InvariantCoreSubject.Options.Waivers);

    [Fact]
    public async Task Every_xcut_requirement_is_covered_by_a_result_that_is_not_a_gap()
    {
        var report = await InvariantSuite.RunAllAsync(InvariantCoreSubject.Create(), InvariantCoreSubject.Options, TestContext.Current.CancellationToken);

        foreach (var n in Enumerable.Range(1, 24).Where(n => n != 23))
        {
            Assert.Equal(ConformanceStatus.Passed, report.ByRequirement[$"XCUT-{n}"]);
        }

        Assert.Equal(ConformanceStatus.NotExercised, report.ByRequirement["XCUT-23"]);
    }
}
