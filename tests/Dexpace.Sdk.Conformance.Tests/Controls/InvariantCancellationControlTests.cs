// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>xcut-1</c>, <c>xcut-2</c> and <c>xcut-3</c> (plan 1.4): each broken pipeline must fail the assertion that judges its clause, and the defaults must pass.</summary>
[Trait("Category", "Unit")]
public sealed class InvariantCancellationControlTests
{
    internal static IReadOnlyList<InvariantControlRow> Rows { get; } =
    [
        .. Both("xcut-1.cancel-is-terminal", "retries a cancelled send", "never re-issued", () => BrokenPipelines.Subject("retries a cancelled send", _ => [new BrokenPipelines.RetriesCancellation()])),
        .. Both("xcut-1.cancel-is-terminal", "reports a cancellation as a retryable transport failure", "carrying its own token", () => BrokenPipelines.Subject("wraps a cancellation", _ => [new BrokenPipelines.WrapsCancellation()])),
        .. Both("xcut-2.timeout-is-retryable", "reports a deadline as a cancellation", "not be reported as a cancellation", () => BrokenPipelines.Subject("deadline as cancellation", _ => [new BrokenPipelines.DeadlineAsCancellation()])),
        .. Both("xcut-3.wait-promptly-cancellable", "waits without honouring the caller's token", "did not finish within", () => BrokenPipelines.Subject("ignores the token", time => [new BrokenPipelines.IgnoresTokenWait(time)])),
        .. Both("xcut-3.wait-promptly-cancellable", "leaves the timer it armed running", "every timer the wait armed", () => BrokenPipelines.Subject("leaks its timer", time => [new BrokenPipelines.LeaksItsTimer(time)])),
    ];

    public static IReadOnlyCollection<string> Covered => InvariantControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => InvariantControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => InvariantControlRow.PositiveCases(Rows);

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => InvariantControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_default_subject_passes(string assertion, TransportFace face) => InvariantControlRow.RunPositiveAsync(assertion, face);

    private static InvariantControlRow[] Both(string assertion, string name, string because, Func<InvariantSubject> subject) =>
        [
            new(assertion, TransportFace.Async, name, subject, Because: because),
            new(assertion, TransportFace.Blocking, name, subject, Because: because),
        ];
}
