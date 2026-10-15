// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The packaging assertion catalogue, in the order of the design table (phase 10, section E): each group contributes its assertions in declaration order.</summary>
internal static class PackagingCatalogue
{
    /// <summary>Every assertion the packaging suite runs.</summary>
    internal static IReadOnlyList<ConformanceAssertion> All { get; } =
    [
        .. DependencyPackagingAssertions.Assertions(),
        .. AssemblyPackagingAssertions.Assertions(),
        .. MetadataPackagingAssertions.Assertions(),
    ];
}
