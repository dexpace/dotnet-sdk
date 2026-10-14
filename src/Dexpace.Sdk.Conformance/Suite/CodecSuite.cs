// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The codec conformance suite (phase 10, design section D): eleven assertions over the <c>SERDE</c> requirements a codec seam can be asked
/// (<c>SERDE-1</c> to <c>-3</c>, <c>-9</c>, <c>-10</c>, <c>-12</c>, <c>-15</c> to <c>-17</c>, <c>-19</c>, <c>-20</c>, and <c>SEAM-20</c>), run against a
/// <see cref="CodecSubject"/> on both faces of <see cref="Dexpace.Sdk.Core.Serialization.ISerde"/>: the asynchronous stream pair and the synchronous
/// buffer and span pair. Framework-free like <see cref="TransportSuite"/>.
/// </summary>
/// <remarks>
/// The suite serializes only the probe models of <c>Dexpace.Sdk.Conformance.Codec</c>; a codec that needs compile-time metadata registers them.
/// <c>SERDE-4</c>, <c>-13</c>, <c>-14</c>, <c>-18</c> and <c>-30</c> judge core code (the span profile, the typed readers, <c>Tristate&lt;T&gt;</c>'s own
/// API), not the codec seam, so they are not assertions here (validation F6).
/// </remarks>
public static class CodecSuite
{
    /// <summary>What a green run of the codec suite does not prove.</summary>
    public static string Preamble { get; } = CodecIdentity.Preamble;

    /// <summary>Every assertion, in a stable order. Names are stable public identifiers, frozen at the first shipped surface.</summary>
    public static IReadOnlyList<ConformanceAssertion> Assertions => CodecCatalogue.All;

    /// <summary>Runs one assertion on one face of <paramref name="subject"/>.</summary>
    /// <param name="subject">The codec under test.</param>
    /// <param name="assertion">One of <see cref="Assertions"/>.</param>
    /// <param name="face">A face <paramref name="assertion"/> declares.</param>
    /// <param name="options">Bounds and waivers; defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Abandons the run with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The result. Assertion failures and codec crashes are results, not exceptions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> or <paramref name="assertion"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="assertion"/> is not one of <see cref="Assertions"/>, or <paramref name="face"/> is not one it declares.</exception>
    public static Task<ConformanceResult> RunAsync(
        CodecSubject subject,
        ConformanceAssertion assertion,
        TransportFace face,
        SuiteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(assertion);
        if (!Assertions.Contains(assertion))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' is not a codec assertion.", nameof(assertion));
        }

        if (!assertion.Faces.Contains(face))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' does not declare the {face} face.", nameof(face));
        }

        var effective = options ?? new SuiteOptions();
        return SuiteRunnerCore.RunAsync(
            assertion,
            face,
            () => new CodecContext(subject, face, effective),
            effective.AssertionTimeout,
            effective.Waivers,
            cancellationToken);
    }

    /// <summary>Runs every assertion on every face it declares, sequentially, and builds the report.</summary>
    /// <param name="subject">The codec under test.</param>
    /// <param name="options">Bounds and waivers; defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Abandons the run with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The report; its <see cref="ConformanceReport.IsGreen"/> is the verdict.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is null.</exception>
    public static Task<ConformanceReport> RunAllAsync(
        CodecSubject subject,
        SuiteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var effective = options ?? new SuiteOptions();
        return SuiteRunnerCore.RunAllAsync(
            subject.Name,
            CodecIdentity.Identity,
            Assertions,
            (assertion, face) => RunAsync(subject, assertion, face, effective, cancellationToken),
            effective.Waivers);
    }
}
