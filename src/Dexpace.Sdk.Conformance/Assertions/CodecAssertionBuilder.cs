// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The one place a codec assertion is declared: its stable name, the requirements it is evidence for, the strength of its clause, its faces and its body.</summary>
internal static class CodecAssertionBuilder
{
    /// <summary>The asynchronous stream pair and the synchronous buffer and span pair of the codec.</summary>
    internal static TransportFace[] Both { get; } = [TransportFace.Async, TransportFace.Blocking];

    /// <summary>No call surface: the assertion judges a value.</summary>
    internal static TransportFace[] NoFace { get; } = [TransportFace.None];

    /// <summary>Declares an assertion.</summary>
    /// <param name="name">The stable public name, <c>serde-1.round-trip</c>.</param>
    /// <param name="ids">The requirement IDs it is evidence for.</param>
    /// <param name="level">The strength of the clause it checks; never above the strongest of <paramref name="ids"/>.</param>
    /// <param name="faces">The faces it runs on.</param>
    /// <param name="body">The check; throws <see cref="ConformanceException"/> on a broken clause.</param>
    internal static ConformanceAssertion Define(
        string name,
        string[] ids,
        RequirementLevel level,
        TransportFace[] faces,
        Func<CodecContext, CancellationToken, Task> body) =>
        ConformanceAssertion.Create(name, ids, level, faces, body);
}
