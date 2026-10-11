// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>transport-11</c> (plan 2.8). The raw-socket client logs no drops, so <c>transport-11.drop-logged</c> has no positive counterpart here: it is waived for it in its driver.</summary>
[Trait("Category", "Integration")]
public sealed class OutboundControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-11.framing-recomputed", TransportFace.Async, "forwards the caller's Host and Content-Length verbatim", BrokenTransports.ForwardsCallerFraming),
        ControlRow.Over("transport-11.framing-recomputed", TransportFace.Blocking, "forwards the caller's Host and Content-Length verbatim", BrokenTransports.ForwardsCallerFraming),
        ControlRow.Over("transport-11.drop-logged", TransportFace.Async, "drops the headers silently", BrokenTransports.Plain),
        new(
            "transport-11.drop-logged",
            TransportFace.Async,
            "logs the dropped values",
            () => new TransportSubject { Name = "logs values", CreateAsync = settings => BrokenTransports.LogsDroppedValues(settings.Logger) }),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => ControlRow.PositiveCases(Rows, "transport-11.drop-logged");

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => ControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_raw_socket_client_passes(string assertion, TransportFace face) => ControlRow.RunPositiveAsync(assertion, face);
}
