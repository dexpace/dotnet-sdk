// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for the header assertions of plan 2.11: <c>transport-10</c>, <c>-12</c>, <c>-13</c> and the three clauses of <c>-14</c>. The rows are 8b's, the assertions 8a's.</summary>
[Trait("Category", "Integration")]
public sealed class HeaderControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-10.content-type-authoritative", TransportFace.Async, "overwrites the caller's Content-Type", BrokenTransports.OverwritesContentType),
        ControlRow.Over("transport-10.content-type-authoritative", TransportFace.Blocking, "overwrites the caller's Content-Type", BrokenTransports.OverwritesContentType),
        ControlRow.Over("transport-12.native-rejected-header-dropped", TransportFace.Async, "throws on a header its native layer refuses", BrokenTransports.ThrowsOnRejectedHeader),
        ControlRow.Over("transport-12.native-rejected-header-dropped", TransportFace.Blocking, "drops the refused headers silently", BrokenTransports.DropsRejectedHeadersSilently),
        new(
            "transport-13.drop-log-once-per-name",
            TransportFace.Async,
            "logs every drop loudly every time",
            () => new TransportSubject { Name = "logs every drop", CreateAsync = settings => BrokenTransports.LogsEveryDrop(settings.Logger) }),
        ControlRow.Over("transport-13.drop-log-once-per-name", TransportFace.Async, "drops the headers and logs nothing", BrokenTransports.DropsRejectedHeadersSilently),
        ControlRow.Over("transport-14.value-control-dropped", TransportFace.Async, "fails the whole response on a control byte in a value", BrokenTransports.StrictInbound),
        ControlRow.Over("transport-14.value-control-dropped", TransportFace.Blocking, "fails the whole response on a control byte in a value", BrokenTransports.StrictInbound),
        ControlRow.Over("transport-14.obs-text-preserved", TransportFace.Async, "strips obs-text from values", BrokenTransports.StripsObsText),
        ControlRow.Over("transport-14.obs-text-preserved", TransportFace.Blocking, "strips obs-text from values", BrokenTransports.StripsObsText),
        ControlRow.Over("transport-14.name-malformed-dropped", TransportFace.Async, "fails the whole response on a malformed name", BrokenTransports.StrictInbound),
        ControlRow.Over("transport-14.name-malformed-dropped", TransportFace.Blocking, "fails the whole response on a malformed name", BrokenTransports.StrictInbound),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    // The raw-socket client writes every header it is given, so it passes transport-12 and has nothing to log for transport-13: that
    // assertion is Vacuous against it (A_transport_that_drops_nothing_is_vacuous_for_transport_13), and its positive case is the
    // conforming dropper (The_conforming_dropper_passes_transport_13).
    public static TheoryData<string, TransportFace> PositiveCases => ControlRow.PositiveCases(Rows, "transport-13.drop-log-once-per-name");

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => ControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_raw_socket_client_passes(string assertion, TransportFace face) => ControlRow.RunPositiveAsync(assertion, face);

    [Fact]
    public Task The_conforming_dropper_passes_transport_13() =>
        ControlRow.RunPositiveAsync("transport-13.drop-log-once-per-name", TransportFace.Async, ConformingHooks.OncePerNameDropper());

    [Fact]
    public async Task A_transport_that_drops_nothing_is_vacuous_for_transport_13_and_needs_no_waiver()
    {
        // The raw-socket client puts every header on the wire, so there is no drop whose logging could be judged. The result is
        // Vacuous (not Failed, not Passed) from the assertion's own measurement, and the driver carries no waiver for it.
        var result = await AssertionControl.RunAsync("transport-13.drop-log-once-per-name", TransportFace.Async, RawSocketSubject.Create(), RawSocketSubject.Options);

        Assert.Equal(ConformanceStatus.Vacuous, result.Status);
        Assert.Contains("dropped none", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(RawSocketSubject.Options.Waivers, waiver => waiver.RequirementId == "TRANSPORT-13");
    }
}
