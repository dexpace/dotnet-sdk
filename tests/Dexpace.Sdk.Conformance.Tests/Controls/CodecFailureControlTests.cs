// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>serde-9</c>, <c>serde-10</c> and <c>serde-12</c> (plan 2.3).</summary>
[Trait("Category", "Unit")]
public sealed class CodecFailureControlTests
{
    internal static IReadOnlyList<CodecControlRow> Rows { get; } =
    [
        .. CodecControlRow.OnEveryFace("serde-9.no-library-type-escapes", "lets the library's exception type escape", SerdeFlaw.LeaksLibraryException, "never the backing library"),
        .. CodecControlRow.OnEveryFace("serde-9.no-library-type-escapes", "drops the original failure", SerdeFlaw.DropsCause, "chain"),
        .. CodecControlRow.OnEveryFace("serde-10.directional-subtypes", "raises a read failure as the write subtype", SerdeFlaw.WrongDirection, "deserialization-specific"),
        .. CodecControlRow.OnEveryFace("serde-12.stream-error-unwrapped", "re-wraps a genuine stream error", SerdeFlaw.WrapsIoErrors, "unwrapped"),
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
