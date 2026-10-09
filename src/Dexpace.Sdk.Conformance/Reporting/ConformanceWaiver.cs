// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A decision to accept a failing assertion for one requirement, by ID (design section C, P8a-8). A waiver turns a
/// <see cref="ConformanceStatus.Failed"/> or <see cref="ConformanceStatus.Errored"/> result into
/// <see cref="ConformanceStatus.Waived"/>; it must stay needed, so a waived assertion that passes fails the run with
/// "waiver for X is no longer needed", and every waiver is listed on every report, failing run or not (§9.3: "so the gap
/// stays visible").
/// </summary>
/// <param name="RequirementId">
/// The requirement the waiver covers, for example <c>TRANSPORT-8</c>; it must exist in the requirement catalogue
/// generated from appendix C.
/// </param>
/// <param name="Reason">Why the gap is accepted: the fact, the clause or the hand-off that explains it. Never empty.</param>
/// <remarks>
/// <see cref="Assertion"/> and <see cref="Face"/> narrow a waiver (plan reading R3): a requirement cited by both a passing
/// and a failing assertion (for example <c>TRANSPORT-11</c>, MUST for its framing clause and SHOULD for its logging clause)
/// cannot otherwise be waived without making the passing assertion's waiver stale.
/// </remarks>
public sealed record ConformanceWaiver(string RequirementId, string Reason)
{
    private readonly string? _assertion;

    /// <summary>The requirement the waiver covers; validated against the requirement catalogue.</summary>
    public string RequirementId { get; } = RequirementIndex.Require(RequirementId);

    /// <summary>Why the gap is accepted.</summary>
    public string Reason { get; } = Require(Reason, nameof(Reason));

    /// <summary>The phase that will remove this waiver (for example <c>"8b"</c>), or <see langword="null"/> for a permanent decision.</summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Narrows the waiver to one assertion by name; <see langword="null"/> means every assertion citing
    /// <see cref="RequirementId"/>. Not validated against the assertion catalogue here: the runner reports an unknown name
    /// when it applies waivers.
    /// </summary>
    public string? Assertion
    {
        get => _assertion;
        init => _assertion = value is null ? null : Require(value, nameof(Assertion));
    }

    /// <summary>Narrows the waiver to one face; <see langword="null"/> means both.</summary>
    public TransportFace? Face { get; init; }

    private static string Require(string value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("The value must not be empty.", name)
            : value;
    }
}
