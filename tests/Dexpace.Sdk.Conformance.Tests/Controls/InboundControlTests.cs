// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>transport-27</c> and <c>http-39</c> (plan 2.7).</summary>
[Trait("Category", "Integration")]
public sealed class InboundControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-27.inbound-downgrade", TransportFace.Async, "fails on a malformed Content-Type", BrokenTransports.ThrowsOnMalformedContentType),
        ControlRow.Over("transport-27.inbound-downgrade", TransportFace.Blocking, "fails on a malformed Content-Type", BrokenTransports.ThrowsOnMalformedContentType),
        ControlRow.Over("http-39.short-source-fails", TransportFace.Async, "pads a short source with zeros", BrokenTransports.PadsShortBodies),
        ControlRow.Over("http-39.short-source-fails", TransportFace.Blocking, "pads a short source with zeros", BrokenTransports.PadsShortBodies),
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
