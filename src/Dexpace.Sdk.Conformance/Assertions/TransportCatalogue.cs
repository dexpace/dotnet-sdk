// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The assertion catalogue, in the order of the design's table: each group contributes its assertions in declaration order.
/// </summary>
internal static class TransportCatalogue
{
    /// <summary>Every assertion the suite runs.</summary>
    internal static IReadOnlyList<ConformanceAssertion> All { get; } =
    [
        .. ResponseShapeAssertions.Assertions(),
        .. CancellationAssertions.Assertions(),
        .. CloseAssertions.Assertions(),
        .. BodyAssertions.Assertions(),
        .. InboundAssertions.Assertions(),
    ];
}
