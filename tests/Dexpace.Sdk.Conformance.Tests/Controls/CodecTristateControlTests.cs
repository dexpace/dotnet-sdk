// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>serde-15</c>, <c>-16</c>, <c>-17</c>, <c>-19</c> and <c>-20</c> (plan 2.4).</summary>
[Trait("Category", "Unit")]
public sealed class CodecTristateControlTests
{
    internal static IReadOnlyList<CodecControlRow> Rows { get; } =
    [
        .. CodecControlRow.OnEveryFace("serde-15.tristate-emission", "writes a wire null for an absent member", SerdeFlaw.NullForAbsent, "must not encode alike"),
        .. CodecControlRow.OnEveryFace("serde-16.tristate-decode", "decodes an explicit null as absent", SerdeFlaw.NullDecodesAsAbsent, "explicit null member must decode to Null"),
        .. CodecControlRow.OnEveryFace("serde-17.missing-key-absent", "decodes a missing key as null", SerdeFlaw.MissingKeyIsNull, "no key on the wire"),
        .. CodecControlRow.OnEveryFace("serde-19.default-wiring", "has no tri-state wiring", SerdeFlaw.NoTristateWiring, "default codec configuration"),
        .. CodecControlRow.OnEveryFace("serde-20.top-level-degrades", "throws for a top-level absent value", SerdeFlaw.ThrowsForTopLevelAbsent, "top-level Absent"),
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
