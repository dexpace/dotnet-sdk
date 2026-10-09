// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The one place an assertion is declared: its stable name, the requirements it is evidence for, the strength of its clause, its faces and its body.</summary>
internal static class AssertionBuilder
{
    /// <summary>Both faces.</summary>
    internal static TransportFace[] Both { get; } = [TransportFace.Async, TransportFace.Blocking];

    /// <summary>The asynchronous face only.</summary>
    internal static TransportFace[] AsyncOnly { get; } = [TransportFace.Async];

    /// <summary>Declares an assertion.</summary>
    /// <param name="name">The stable public name, <c>transport-24.vendor-status-readable</c>.</param>
    /// <param name="ids">The requirement IDs it is evidence for.</param>
    /// <param name="level">The strength of the clause it checks; never above the strongest of <paramref name="ids"/>.</param>
    /// <param name="faces">The faces it runs on.</param>
    /// <param name="body">The check; throws <see cref="ConformanceException"/> on a broken clause.</param>
    internal static ConformanceAssertion Define(
        string name,
        string[] ids,
        RequirementLevel level,
        TransportFace[] faces,
        Func<SuiteContext, CancellationToken, Task> body) => new(name, ids, level, faces, body);
}
