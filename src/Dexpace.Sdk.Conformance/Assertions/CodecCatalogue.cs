// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The codec assertion catalogue, in the order of the design (phase 10, section D): each group contributes its assertions in declaration order.</summary>
internal static class CodecCatalogue
{
    /// <summary>Every assertion the codec suite runs.</summary>
    internal static IReadOnlyList<ConformanceAssertion> All { get; } =
    [
        .. CodecRoundTripAssertions.Assertions(),
        .. CodecFailureAssertions.Assertions(),
        .. CodecTristateAssertions.Assertions(),
    ];
}
