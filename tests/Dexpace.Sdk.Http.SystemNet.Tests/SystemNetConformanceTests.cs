// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Http.SystemNet.Tests.Conformance;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// The transport conformance kit driven against the reference transport (phase 8a, P8a-15): one case per (assertion, face),
/// plus the whole suite as a report. The waivers in <see cref="SystemNetSubject.Options"/> are phase 8b's rows; phase 8b closes
/// each by deleting its waiver, and the kit fails the run if a waiver is left after its row is fixed.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class SystemNetConformanceTests
{
    // <driver>
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
        var result = await TransportSuite.RunAsync(SystemNetSubject.Create(), assertion, face, SystemNetSubject.Options, TestContext.Current.CancellationToken);

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
        var report = await TransportSuite.RunAllAsync(SystemNetSubject.Create(), SystemNetSubject.Options, TestContext.Current.CancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        Assert.True(report.IsGreen, report.ToString());
    }

    // </driver>
    [Fact]
    public void Only_8b_waivers_remain() => Assert.All(SystemNetSubject.Options.Waivers, waiver => Assert.Equal("8b", waiver.Owner));

    [Fact]
    public void Only_known_waiver_targets_remain() => Assert.All(
        SystemNetSubject.Options.Waivers.Where(waiver => waiver.Assertion is not null),
        waiver => Assert.Contains(TransportSuite.Assertions, a => a.Name == waiver.Assertion && a.RequirementIds.Contains(waiver.RequirementId)));
}
