// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>transport-29</c> and <c>transport-2</c> (plan 2.8).</summary>
[Trait("Category", "Integration")]
public sealed class ConcurrencyControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-29.concurrent-no-crosstalk", TransportFace.Async, "hands every caller the last response that arrived", BrokenTransports.CrossesResponses),
        ControlRow.Over("transport-29.concurrent-no-crosstalk", TransportFace.Blocking, "hands every caller the last response that arrived", BrokenTransports.CrossesResponses),
        ControlRow.Over("transport-2.no-silent-resend", TransportFace.Async, "buffers the body and sends it again after a failure", BrokenTransports.ResendsBufferedBody),
        ControlRow.Over("transport-2.no-silent-resend", TransportFace.Blocking, "buffers the body and sends it again after a failure", BrokenTransports.ResendsBufferedBody),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => ControlRow.PositiveCases(Rows);

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => ControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_raw_socket_client_passes(string assertion, TransportFace face) => ControlRow.RunPositiveAsync(assertion, face);
}
