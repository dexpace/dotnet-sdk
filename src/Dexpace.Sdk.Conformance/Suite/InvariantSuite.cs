// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The cross-cutting invariant suite (phase 10, design section C): one assertion per clause of <c>XCUT-1</c> to <c>XCUT-24</c>
/// that the SDK's own components can be asked, plus the scheduler-agnostic clause of <c>NFR-11</c>, run against an
/// <see cref="InvariantSubject"/>. Framework-free like <see cref="TransportSuite"/>: it returns results and a
/// <see cref="ConformanceReport"/>, and the caller's test framework decides what a failure means.
/// </summary>
/// <remarks>
/// The transport-level clauses of <c>XCUT-1</c>, <c>-2</c>, <c>-4</c> and <c>-22</c> stay assertions of <see cref="TransportSuite"/>
/// (P10-6); this suite asserts the core-level clause of each, and <see cref="ConformanceReport.Combine"/> merges the two by
/// requirement. An assertion whose hook the driver did not supply is <see cref="ConformanceStatus.NotExercised"/>, never passed.
/// </remarks>
public static class InvariantSuite
{
    /// <summary>
    /// What a green run of the invariant suite does not prove: the hooks the driver did not supply, core's internals reached
    /// only through hooks, behaviour over a real socket, and any requirement no assertion cites.
    /// </summary>
    public static string Preamble { get; } = InvariantIdentity.Preamble;

    /// <summary>Every assertion, in a stable order. Names are stable public identifiers, frozen at the first shipped surface.</summary>
    public static IReadOnlyList<ConformanceAssertion> Assertions => InvariantCatalogue.All;

    /// <summary>Runs one assertion on one face of <paramref name="subject"/>.</summary>
    /// <param name="subject">The composition under test.</param>
    /// <param name="assertion">One of <see cref="Assertions"/>.</param>
    /// <param name="face">A face <paramref name="assertion"/> declares.</param>
    /// <param name="options">Bounds and waivers; defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Abandons the run with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The result. Assertion failures and subject crashes are results, not exceptions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> or <paramref name="assertion"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="assertion"/> is not one of <see cref="Assertions"/>, or <paramref name="face"/> is not one it declares.</exception>
    public static Task<ConformanceResult> RunAsync(
        InvariantSubject subject,
        ConformanceAssertion assertion,
        TransportFace face,
        SuiteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(assertion);
        if (!Assertions.Contains(assertion))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' is not an invariant assertion.", nameof(assertion));
        }

        if (!assertion.Faces.Contains(face))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' does not declare the {face} face.", nameof(face));
        }

        var effective = options ?? new SuiteOptions();
        return SuiteRunnerCore.RunAsync(
            assertion,
            face,
            () => new InvariantContext(subject, face, effective),
            effective.AssertionTimeout,
            effective.Waivers,
            cancellationToken);
    }

    /// <summary>Runs every assertion on every face it declares, sequentially, and builds the report.</summary>
    /// <param name="subject">The composition under test.</param>
    /// <param name="options">Bounds and waivers; defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Abandons the run with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The report; its <see cref="ConformanceReport.IsGreen"/> is the verdict.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is null.</exception>
    public static Task<ConformanceReport> RunAllAsync(
        InvariantSubject subject,
        SuiteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var effective = options ?? new SuiteOptions();
        return SuiteRunnerCore.RunAllAsync(
            subject.Name,
            InvariantIdentity.Identity,
            Assertions,
            (assertion, face) => RunAsync(subject, assertion, face, effective, cancellationToken),
            effective.Waivers);
    }
}
