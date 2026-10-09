// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The assertion catalogue, in the order of the design's table. Empty until the assertion groups of plan tasks 2.5 to 2.11
/// register themselves here.
/// </summary>
internal static class TransportCatalogue
{
    /// <summary>Every assertion the suite runs.</summary>
    internal static IReadOnlyList<ConformanceAssertion> All { get; } = [];
}
