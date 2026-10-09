// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>
/// The second conformance driver (D3, P8a-2, P8a-15): the kit run against a test-only HTTP/1.1 client on raw sockets, reached
/// through <c>DelegateHttpClient</c>. It shares no code with <c>HttpClient</c>, so an assertion that passes here and against
/// <c>SystemNetHttpClient</c> is about the contract and not about <c>SocketsHttpHandler</c>. The client is honest about what it
/// does not do: it supplies none of the capability hooks (no native client to borrow, no proxy), declares no post-dispose
/// behaviour, and waives the two SHOULDs it does not implement.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class RawSocketConformanceTests
{
    private static readonly string[] s_needsAHook =
    [
        "transport-15.borrowed-survives",
        "transport-18.native-resend-identical",
        "transport-22.adaptation-failure-releases",
        "transport-30.proxy-discoverable-no-leak",
        "transport-8.internal-cancel-is-terminal",
    ];

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
        var result = await TransportSuite.RunAsync(RawSocketSubject.Create(), assertion, face, RawSocketSubject.Options, TestContext.Current.CancellationToken);

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
        var report = await TransportSuite.RunAllAsync(RawSocketSubject.Create(), RawSocketSubject.Options, TestContext.Current.CancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        Assert.True(report.IsGreen, report.ToString());
    }

    // </driver>
    [Fact]
    public void Only_known_waiver_targets_remain() => Assert.All(
        RawSocketSubject.Options.Waivers.Where(waiver => waiver.Assertion is not null),
        waiver => Assert.Contains(TransportSuite.Assertions, a => a.Name == waiver.Assertion && a.RequirementIds.Contains(waiver.RequirementId)));

    [Fact]
    public void Every_waiver_is_permanent_and_says_why()
    {
        Assert.All(RawSocketSubject.Options.Waivers, waiver =>
        {
            Assert.Null(waiver.Owner);
            Assert.StartsWith("test-only transport", waiver.Reason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task What_is_not_exercised_is_exactly_what_needs_a_hook_the_client_cannot_supply()
    {
        var report = await TransportSuite.RunAllAsync(RawSocketSubject.Create(), RawSocketSubject.Options, TestContext.Current.CancellationToken);

        var notExercised = report.Results.Where(r => r.Status == ConformanceStatus.NotExercised).Select(r => r.Assertion.Name).Distinct().Order(StringComparer.Ordinal);
        var vacuous = report.Results.Where(r => r.Status == ConformanceStatus.Vacuous).Select(r => r.Assertion.Name).Distinct().Order(StringComparer.Ordinal);

        Assert.Equal(s_needsAHook.Order(StringComparer.Ordinal), notExercised);
        Assert.Equal(["seam-15.after-dispose"], vacuous);
    }
}
