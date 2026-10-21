// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>One thing a ledger-audit rule found wrong.</summary>
/// <param name="Rule">The rule, <c>L1</c> to <c>L11</c>.</param>
/// <param name="Message">What is wrong and where.</param>
internal sealed record LedgerFinding(string Rule, string Message);
