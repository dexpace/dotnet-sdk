// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Runs transport assertions against a <see cref="TransportSubject"/>: one fresh <see cref="SuiteContext"/> per
/// (assertion, face). The status, waiver and bound logic is <see cref="SuiteRunnerCore"/>'s, shared with the other suites
/// (design section C, P10-7).
/// </summary>
internal static class SuiteRunner
{
    /// <summary>Runs one assertion on one face and applies the waivers.</summary>
    /// <exception cref="ArgumentException"><paramref name="assertion"/> does not declare <paramref name="face"/>, or <paramref name="face"/> is <see cref="TransportFace.None"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled: the run is abandoned, never reported as an error.</exception>
    internal static async Task<ConformanceResult> RunAsync(
        TransportSubject subject,
        ConformanceAssertion assertion,
        TransportFace face,
        TransportSuiteOptions options,
        CancellationToken cancellationToken)
    {
        if (face == TransportFace.None)
        {
            throw new ArgumentException("The transport suite has no None face: every transport assertion drives a call surface.", nameof(face));
        }

        if (!assertion.Faces.Contains(face))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' does not declare the {face} face.", nameof(face));
        }

        if (!SuiteContext.Supplies(subject, face))
        {
            return SuiteRunnerCore.ApplyWaivers(
                SuiteRunnerCore.Result(assertion, face, ConformanceStatus.NotExercised, $"the subject supplies no {face} factory"),
                options.Waivers);
        }

        return await SuiteRunnerCore.RunAsync(
            assertion,
            face,
            () => new SuiteContext(subject, face, options),
            options.AssertionTimeout,
            options.Waivers,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs every assertion on every face it declares, sequentially in the given order, and builds the report.</summary>
    internal static Task<ConformanceReport> RunAllAsync(
        TransportSubject subject,
        IReadOnlyList<ConformanceAssertion> assertions,
        TransportSuiteOptions options,
        CancellationToken cancellationToken) =>
        SuiteRunnerCore.RunAllAsync(
            subject.Name,
            SuiteIdentity.Transport,
            assertions,
            (assertion, face) => RunAsync(subject, assertion, face, options, cancellationToken),
            options.Waivers);
}
