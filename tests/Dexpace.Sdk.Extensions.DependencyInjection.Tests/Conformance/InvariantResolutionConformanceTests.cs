// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests.Conformance;

/// <summary>
/// The invariant suite driven against the pipeline <c>AddDexpaceClient</c> builds (phase 10, P10-5; the DI half of <c>XCUT-10</c>, <c>XCUT-22</c> and
/// <c>XCUT-23</c>): one case per (assertion, face), plus the whole suite as a report. This driver supplies <c>SeamResolution</c>, so <c>xcut-23</c> is the
/// one only it exercises; the four assertions that need core's internals are not exercised here and are pinned (P10-28). No assertion is waived.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class InvariantResolutionConformanceTests
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
        await using var factory = new InvariantSubjectFactory();
        var assertion = InvariantSuite.Assertions.Single(a => a.Name == name);

        var result = await InvariantSuite.RunAsync(factory.Create(), assertion, face, InvariantSubjectFactory.Options, TestContext.Current.CancellationToken);

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
    public async Task The_suite_runs_over_the_DI_built_pipeline_and_the_report_is_written()
    {
        await using var factory = new InvariantSubjectFactory();

        var report = await InvariantSuite.RunAllAsync(factory.Create(), InvariantSubjectFactory.Options, TestContext.Current.CancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        Assert.True(report.IsGreen, report.ToString());
        Assert.Equal(ConformanceStatus.Passed, report.ByRequirement["XCUT-23"]);
        Assert.Equal(ConformanceStatus.Passed, report.ByRequirement["XCUT-10"]);
        Assert.Equal(ConformanceStatus.Passed, report.ByRequirement["XCUT-22"]);
    }

    [Fact]
    public async Task Only_the_four_hook_less_assertions_are_not_exercised()
    {
        await using var factory = new InvariantSubjectFactory();

        var report = await InvariantSuite.RunAllAsync(factory.Create(), InvariantSubjectFactory.Options, TestContext.Current.CancellationToken);

        var notExercised = report.Results.Where(static r => r.Status == ConformanceStatus.NotExercised).Select(static r => r.Assertion.Name).Distinct().Order(StringComparer.Ordinal);
        Assert.Equal(InvariantSubjectFactory.ExpectedNotExercised, notExercised);
        Assert.DoesNotContain(report.Results, static r => r.Status is ConformanceStatus.Vacuous or ConformanceStatus.Waived);
        Assert.Empty(InvariantSubjectFactory.Options.Waivers);
    }

    [Fact]
    public void No_codec_fails_the_start_with_the_install_hint()
    {
        var failure = Assert.ThrowsAny<Exception>(InvariantSubjectFactory.Resolution.ResolveWithNone);

        Assert.Contains("UseSerde", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_different_second_install_fails_the_call_naming_both()
    {
        var failure = Assert.Throws<InvalidOperationException>(InvariantSubjectFactory.Resolution.ResolveWithTwo);

        Assert.Contains("ScriptedTransport", failure.Message, StringComparison.Ordinal);
        Assert.Contains("AsyncOnlyTransport", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_explicit_install_wins() => Assert.True(InvariantSubjectFactory.Resolution.ExplicitWins());
}
