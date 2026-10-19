// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>serde-1</c>, <c>serde-2</c> and <c>serde-3</c> (plan 2.2): each flawed reference codec must fail the assertion that judges its clause, and the conforming one must pass.</summary>
[Trait("Category", "Unit")]
public sealed class CodecRoundTripControlTests
{
    internal static IReadOnlyList<CodecControlRow> Rows { get; } =
    [
        .. CodecControlRow.OnEveryFace("serde-1.round-trip", "loses the list", SerdeFlaw.LosesField, "survive a round trip"),
        .. CodecControlRow.OnEveryFace("serde-2.declared-media-type", "declares application/octet-stream", SerdeFlaw.OctetStream, "format-agnostic constant"),
        .. CodecControlRow.OnEveryFace("serde-2.declared-media-type", "declares a different media type on every read", SerdeFlaw.UnstableMediaType, "default Content-Type"),
        .. CodecControlRow.OnEveryFace("serde-3.target-not-closed", "closes the caller's target", SerdeFlaw.ClosesTarget, "the caller's"),
        .. CodecControlRow.OnEveryFace("serde-3.target-not-closed", "closes the caller's source", SerdeFlaw.ClosesSource, "the caller's source stream"),
    ];

    public static IReadOnlyCollection<string> Covered => CodecControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => CodecControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => CodecControlRow.PositiveCases(Rows);

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_flawed_codec_is_caught(string assertion, TransportFace face, string control) => CodecControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_conforming_codec_passes(string assertion, TransportFace face) => CodecControlRow.RunPositiveAsync(assertion, face);
}
