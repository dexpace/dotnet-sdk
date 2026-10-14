// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Assertions;

/// <summary>
/// The union of every catalogue the kit ships (phase 10, validation F2): the transport suite's, and the invariant, codec and
/// packaging suites' as they arrive. A negative control may name an assertion of any of them.
/// </summary>
internal static class KitCatalogues
{
    /// <summary>Every assertion of every suite.</summary>
    internal static IReadOnlyList<ConformanceAssertion> All { get; } =
    [
        .. TransportSuite.Assertions,
        .. InvariantSuite.Assertions,
        .. CodecSuite.Assertions,
    ];
}
