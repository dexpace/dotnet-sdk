// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The outcome of one assertion on one face.</summary>
/// <param name="Assertion">The assertion that ran.</param>
/// <param name="Face">The face it ran on.</param>
/// <param name="Status">What happened.</param>
/// <param name="Detail">One line: why it failed, errored, was waived or not exercised, or what vacuity was established.</param>
public sealed record ConformanceResult(ConformanceAssertion Assertion, TransportFace Face, ConformanceStatus Status, string Detail)
{
    /// <summary>The exception that ended the assertion, for a failed or errored result; otherwise <see langword="null"/>.</summary>
    public Exception? Exception { get; init; }

    /// <summary>The waiver that applied, for a waived result, or the stale waiver a no-longer-needed failure names; otherwise <see langword="null"/>.</summary>
    public ConformanceWaiver? Waiver { get; init; }
}
