// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The invariant assertion catalogue, in the order of the design's table (phase 10, section C): each group contributes its
/// assertions in declaration order.
/// </summary>
internal static class InvariantCatalogue
{
    /// <summary>Every assertion the invariant suite runs.</summary>
    internal static IReadOnlyList<ConformanceAssertion> All { get; } =
    [
        .. CancellationInvariants.Assertions(),
    ];
}
