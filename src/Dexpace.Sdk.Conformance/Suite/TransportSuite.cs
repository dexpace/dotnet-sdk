// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The transport conformance suite (design "The assertion catalogue"): the named assertions for the <c>TRANSPORT</c> and
/// <c>ASYNC</c> requirements, and the runner that drives a <see cref="TransportSubject"/> through them. Framework-free: it
/// returns results and a <see cref="ConformanceReport"/>, and the caller's test framework decides what a failure means.
/// </summary>
/// <remarks>
/// <para>
/// <b>Driving it.</b> Run one assertion on one face with <see cref="RunAsync"/> (one test-framework case per
/// (assertion, face) pair), or everything with <see cref="RunAllAsync"/> for the report. Each run builds a fresh transport
/// and a fresh loopback server, disposes both, and is bounded by <see cref="TransportSuiteOptions.AssertionTimeout"/>.
/// </para>
/// <para>
/// <b>Statuses.</b> See <see cref="ConformanceStatus"/>. A waiver by requirement ID turns a failure into
/// <see cref="ConformanceStatus.Waived"/> and must stay needed.
/// </para>
/// </remarks>
public static class TransportSuite
{
    /// <summary>Every assertion, in a stable order. Names are stable public identifiers, frozen at the first shipped surface.</summary>
    public static IReadOnlyList<ConformanceAssertion> Assertions => TransportCatalogue.All;

    /// <summary>Runs one assertion on one face of <paramref name="subject"/>.</summary>
    /// <param name="subject">The transport under test.</param>
    /// <param name="assertion">One of <see cref="Assertions"/>.</param>
    /// <param name="face">A face <paramref name="assertion"/> declares; <see cref="ConformanceStatus.NotExercised"/> when the subject supplies none.</param>
    /// <param name="options">Bounds and waivers; defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Abandons the run with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The result. Assertion failures and transport crashes are results, not exceptions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> or <paramref name="assertion"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="face"/> is not one <paramref name="assertion"/> declares.</exception>
    public static Task<ConformanceResult> RunAsync(
        TransportSubject subject,
        ConformanceAssertion assertion,
        TransportFace face,
        TransportSuiteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(assertion);
        if (!assertion.Faces.Contains(face))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' does not declare the {face} face.", nameof(face));
        }

        return SuiteRunner.RunAsync(subject, assertion, face, options ?? new TransportSuiteOptions(), cancellationToken);
    }

    /// <summary>Runs every assertion on every face it declares, sequentially, and builds the report.</summary>
    /// <param name="subject">The transport under test.</param>
    /// <param name="options">Bounds and waivers; defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Abandons the run with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The report; its <see cref="ConformanceReport.IsGreen"/> is the verdict.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is null.</exception>
    public static Task<ConformanceReport> RunAllAsync(
        TransportSubject subject,
        TransportSuiteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return SuiteRunner.RunAllAsync(subject, Assertions, options ?? new TransportSuiteOptions(), cancellationToken);
    }
}
