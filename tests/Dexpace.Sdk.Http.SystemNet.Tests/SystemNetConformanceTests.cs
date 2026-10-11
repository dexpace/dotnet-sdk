// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Http.SystemNet.Tests.Conformance;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// The transport conformance kit driven against the reference transport (phase 8a, P8a-15) through both of its constructions
/// (phase 8b, P8b-21): one case per (subject, assertion, face), plus the whole suite as a report per subject. The waivers in
/// <see cref="SystemNetSubject.Options"/> are phase 8b's rows; phase 8b closes each by deleting its waiver, and the kit fails the
/// run if a waiver is left after its row is fixed.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class SystemNetConformanceTests
{
    // <driver>
    public static TheoryData<string, string, TransportFace> Rows()
    {
        var rows = new TheoryData<string, string, TransportFace>();
        foreach (var kind in SystemNetSubject.Kinds)
        {
            foreach (var assertion in TransportSuite.Assertions)
            {
                foreach (var face in assertion.Faces)
                {
                    rows.Add(kind, assertion.Name, face);
                }
            }
        }

        return rows;
    }

    public static TheoryData<string> Subjects() => [.. SystemNetSubject.Kinds];

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Assertion_holds(string subject, string name, TransportFace face)
    {
        var assertion = TransportSuite.Assertions.Single(a => a.Name == name);
        var result = await TransportSuite.RunAsync(SystemNetSubject.For(subject), assertion, face, SystemNetSubject.Options, TestContext.Current.CancellationToken);

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

    [Theory]
    [MemberData(nameof(Subjects))]
    public async Task The_whole_suite_is_green_and_its_report_is_written(string subject)
    {
        var report = await TransportSuite.RunAllAsync(SystemNetSubject.For(subject), SystemNetSubject.Options, TestContext.Current.CancellationToken);

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
