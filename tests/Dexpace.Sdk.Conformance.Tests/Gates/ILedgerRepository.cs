// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// The files the ledger-audit gate reads (phase 11, P11-5): the real repository for the permanent tests and an in-memory tree for the fixtures that
/// prove each rule can fail. Paths are repository-relative with forward slashes.
/// </summary>
internal interface ILedgerRepository
{
    /// <summary>The text of <paramref name="relative"/>, or <see langword="null"/> when no such file exists.</summary>
    /// <param name="relative">A repository-relative path.</param>
    string? ReadText(string relative);

    /// <summary>The repository-relative paths of every file under <paramref name="directory"/> (recursively, <c>bin</c> and <c>obj</c> excluded) with one of <paramref name="extensions"/>.</summary>
    /// <param name="directory">A repository-relative directory.</param>
    /// <param name="extensions">Extensions including the dot, such as <c>.cs</c>.</param>
    IEnumerable<string> Files(string directory, IReadOnlyCollection<string> extensions);
}
