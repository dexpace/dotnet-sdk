// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Http.SystemNet.Tests.Conformance;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// The transport conformance kit driven against the reference transport (phase 8a, P8a-15) through both of its constructions
/// (phase 8b, P8b-21): one case per (subject, assertion, face), plus the whole suite as a report per subject. The waivers in
/// <see cref="SystemNetSubject.Options"/> are the two permanent decisions phase 8b recorded (design section 10 entries 32 and 33); the
/// kit fails the run if a waiver is left after its clause starts to pass, so a runtime that fixes either is noticed.
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
    // Phase 8b closed every row it owned by deleting its waiver (exit criterion 2): no waiver carries Owner "8b" any more. What is
    // left is two recorded decisions, each narrowed to the one assertion it covers, each citing the design section 10 entry that
    // records why the clause is unmet on a stated domain.
    [Fact]
    public void Only_the_two_recorded_permanent_decisions_remain()
    {
        var waivers = SystemNetSubject.Options.Waivers;

        Assert.DoesNotContain(waivers, waiver => waiver.Owner is not null);
        Assert.Equal(
            ["TRANSPORT-14|transport-14.name-malformed-dropped", "TRANSPORT-2|transport-2.bodyless-not-resent"],
            waivers.Select(waiver => $"{waiver.RequirementId}|{waiver.Assertion}").Order(StringComparer.Ordinal));
        Assert.Contains("design section 10 entry 32", waivers.Single(waiver => waiver.RequirementId == "TRANSPORT-14").Reason, StringComparison.Ordinal);
        Assert.Contains("design section 10 entry 33", waivers.Single(waiver => waiver.RequirementId == "TRANSPORT-2").Reason, StringComparison.Ordinal);
        Assert.All(waivers, waiver => Assert.Null(waiver.Face));
    }

    [Fact]
    public void Only_known_waiver_targets_remain() => Assert.All(
        SystemNetSubject.Options.Waivers.Where(waiver => waiver.Assertion is not null),
        waiver => Assert.Contains(TransportSuite.Assertions, a => a.Name == waiver.Assertion && a.RequirementIds.Contains(waiver.RequirementId)));
}
