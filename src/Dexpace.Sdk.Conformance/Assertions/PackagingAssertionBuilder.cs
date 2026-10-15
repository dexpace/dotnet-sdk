// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The one place a packaging assertion is declared. Every one judges a value, so its only face is <see cref="TransportFace.None"/>; its body completes synchronously.</summary>
internal static class PackagingAssertionBuilder
{
    /// <summary>Declares an assertion.</summary>
    /// <param name="name">The stable public name, <c>nfr-1.core-dependencies</c>.</param>
    /// <param name="ids">The requirement IDs it is evidence for.</param>
    /// <param name="level">The strength of the clause it checks; never above the strongest of <paramref name="ids"/>.</param>
    /// <param name="body">The check; throws <see cref="ConformanceException"/> on a broken clause.</param>
    internal static ConformanceAssertion Define(string name, string[] ids, RequirementLevel level, Action<PackagingContext> body) =>
        ConformanceAssertion.Create<PackagingContext>(
            name,
            ids,
            level,
            [TransportFace.None],
            (context, _) =>
            {
                body(context);
                return Task.CompletedTask;
            });
}
