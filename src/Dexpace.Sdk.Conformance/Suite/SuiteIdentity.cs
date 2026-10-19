// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Which of the kit's suites produced a report, and what a green run of that suite does not prove (design section B, P10-7).
/// The text is the suite's own, so a report never claims more than its subject was asked.
/// </summary>
/// <param name="Name">The suite's name: <c>transport</c>, <c>invariant</c>, <c>codec</c> or <c>packaging</c>; <c>combined</c> for a merged report.</param>
/// <param name="Preamble">What a green run of the suite does not prove.</param>
internal sealed record SuiteIdentity(string Name, string Preamble)
{
    /// <summary>The transport suite: the text <see cref="ConformanceReport.Preamble"/> has always carried.</summary>
    internal static SuiteIdentity Transport { get; } = new("transport", ConformanceReport.Preamble);
}
