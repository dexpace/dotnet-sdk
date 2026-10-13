// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests.Conformance;

/// <summary>
/// The transport conformance kit driven against the transport <c>AddDexpaceClient</c> builds (phase 9, P9-22; the third driver beside the two SystemNet constructions
/// and the raw-socket client): one case per (assertion, face), plus the whole suite as a report. The waivers are the two permanent assertion-scoped decisions
/// (design section 10 entries 32 and 33); the kit fails the run if a waiver is left after its clause starts to pass. <c>transport-1.redirect-not-followed</c>
/// passing on both faces is the proof the 8a hand-off names, and <c>transport-15.owned-released</c> is what the container-owned lease earns (V8).
/// </summary>
[Trait("Category", "Conformance")]
public sealed class DependencyInjectionConformanceTests
{
    public static TheoryData<string, TransportFace> Rows()
    {
        var rows = new TheoryData<string, TransportFace>();
        foreach (var assertion in TransportSuite.Assertions)
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
        var assertion = TransportSuite.Assertions.Single(a => a.Name == name);
        var result = await TransportSuite.RunAsync(DependencyInjectionSubject.Create(), assertion, face, DependencyInjectionSubject.Options, TestContext.Current.CancellationToken);

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
        var report = await TransportSuite.RunAllAsync(DependencyInjectionSubject.Create(), DependencyInjectionSubject.Options, TestContext.Current.CancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        Assert.True(report.IsGreen, report.ToString());
    }

    [Theory]
    [InlineData(TransportFace.Async)]
    [InlineData(TransportFace.Blocking)]
    public async Task The_redirect_is_not_followed_on_both_faces(TransportFace face)
    {
        var assertion = TransportSuite.Assertions.Single(static a => a.Name == "transport-1.redirect-not-followed");

        var result = await TransportSuite.RunAsync(DependencyInjectionSubject.Create(), assertion, face, DependencyInjectionSubject.Options, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, result.Status);
    }

    [Fact]
    public void Only_the_two_recorded_permanent_decisions_remain()
    {
        var waivers = DependencyInjectionSubject.Options.Waivers;

        Assert.DoesNotContain(waivers, static waiver => waiver.Owner is not null);
        Assert.Equal(
            ["TRANSPORT-14|transport-14.name-malformed-dropped", "TRANSPORT-2|transport-2.bodyless-not-resent"],
            waivers.Select(static waiver => $"{waiver.RequirementId}|{waiver.Assertion}").Order(StringComparer.Ordinal));
        Assert.All(waivers, static waiver => Assert.Null(waiver.Face));
    }

    [Fact]
    public void Only_known_waiver_targets_remain() => Assert.All(
        DependencyInjectionSubject.Options.Waivers.Where(static waiver => waiver.Assertion is not null),
        waiver => Assert.Contains(TransportSuite.Assertions, a => a.Name == waiver.Assertion && a.RequirementIds.Contains(waiver.RequirementId)));

    [Fact]
    public async Task Only_the_borrowed_assertion_is_not_exercised_and_the_driver_says_why()
    {
        var report = await TransportSuite.RunAllAsync(DependencyInjectionSubject.Create(), DependencyInjectionSubject.Options, TestContext.Current.CancellationToken);

        var notExercised = report.Results.Where(static r => r.Status == ConformanceStatus.NotExercised).Select(static r => r.Assertion.Name).Distinct().ToList();
        Assert.Equal(["transport-15.borrowed-survives"], notExercised);
    }
}
