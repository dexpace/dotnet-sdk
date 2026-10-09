// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The assertion catalogue, in the order of the design's table: each group contributes its assertions in declaration order.
/// </summary>
internal static class TransportCatalogue
{
    /// <summary>Every assertion the suite runs, in the order of the design table: by family (transport, async, seam, http), then requirement number, then declaration.</summary>
    internal static IReadOnlyList<ConformanceAssertion> All { get; } = Order(
    [
        .. ResponseShapeAssertions.Assertions(),
        .. CancellationAssertions.Assertions(),
        .. CloseAssertions.Assertions(),
        .. BodyAssertions.Assertions(),
        .. InboundAssertions.Assertions(),
        .. OutboundAssertions.Assertions(),
        .. ConcurrencyAssertions.Assertions(),
        .. FailureAssertions.Assertions(),
        .. LifecycleAssertions.Assertions(),
        .. RedirectAndTimeoutAssertions.Assertions(),
        .. HeaderAssertions.Assertions(),
        .. ResendAssertions.Assertions(),
        .. ProxyAssertions.Assertions(),
        .. DisposeAssertions.Assertions(),
    ]);

    private static ConformanceAssertion[] Order(ConformanceAssertion[] declared) =>
        [.. declared
            .Select((assertion, index) => (Assertion: assertion, Index: index))
            .OrderBy(entry => Family(entry.Assertion.Name))
            .ThenBy(entry => Number(entry.Assertion.Name))
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Assertion)];

    // transport-24.vendor-status-readable: family "transport", number 24.
    private static int Family(string name) => name.Split('-')[0] switch
    {
        "transport" => 0,
        "async" => 1,
        "seam" => 2,
        "http" => 3,
        _ => throw new InvalidOperationException($"Assertion '{name}' belongs to no known family."),
    };

    private static int Number(string name) => int.Parse(name.Split('-')[1].Split('.')[0], System.Globalization.CultureInfo.InvariantCulture);
}
