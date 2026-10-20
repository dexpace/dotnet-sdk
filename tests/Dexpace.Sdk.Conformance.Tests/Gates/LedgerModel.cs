// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>What <see cref="LedgerAudit.Load"/> read out of <c>docs/deviations.md</c>: the tables as rows of cells, and the retained-deviation blocks by entry number.</summary>
/// <param name="Audit">The audit rows (<c>#</c>, title, IDs, status, disposition, reason, evidence).</param>
/// <param name="Retained">The text of each <c>### N.</c> block by entry number.</param>
/// <param name="Readings">The readings rows (item, IDs, verdict, disposition, evidence).</param>
/// <param name="Scans">The negative-scan rows (scan, entry, claim, pattern, paths, allowed, probe).</param>
/// <param name="CountLine">The text of the <c>Unmet clauses</c> line, or <see langword="null"/>.</param>
internal sealed record LedgerModel(
    IReadOnlyList<IReadOnlyList<string>> Audit,
    IReadOnlyDictionary<int, string> Retained,
    IReadOnlyList<IReadOnlyList<string>> Readings,
    IReadOnlyList<IReadOnlyList<string>> Scans,
    string? CountLine);
