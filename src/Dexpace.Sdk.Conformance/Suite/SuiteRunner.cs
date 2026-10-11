// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Runs assertions against a subject: one fresh context per (assertion, face), a bounded body, the six statuses and the
/// waiver rules (design section C). The heart of the kit: every assertion depends on it.
/// </summary>
internal static class SuiteRunner
{
    /// <summary>Runs one assertion on one face and applies the waivers.</summary>
    /// <exception cref="ArgumentException"><paramref name="assertion"/> does not declare <paramref name="face"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled: the run is abandoned, never reported as an error.</exception>
    internal static async Task<ConformanceResult> RunAsync(
        TransportSubject subject,
        ConformanceAssertion assertion,
        TransportFace face,
        TransportSuiteOptions options,
        CancellationToken cancellationToken)
    {
        if (!assertion.Faces.Contains(face))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' does not declare the {face} face.", nameof(face));
        }

        if (!SuiteContext.Supplies(subject, face))
        {
            return ApplyWaivers(Result(assertion, face, ConformanceStatus.NotExercised, $"the subject supplies no {face} factory"), options.Waivers);
        }

#pragma warning disable CA2000 // Disposed on every path below: DisposeAsync(context, ...) on completion, DisposeQuietlyAsync on cancellation.
        var context = new SuiteContext(subject, face, options);
#pragma warning restore CA2000
        ConformanceResult outcome;
        try
        {
            outcome = await ExecuteBodyAsync(assertion, context, options, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await DisposeQuietlyAsync(context).ConfigureAwait(false);
            throw;
        }

        return ApplyWaivers(await DisposeAsync(context, assertion, face, outcome).ConfigureAwait(false), options.Waivers);
    }

    /// <summary>Runs every assertion on every face it declares, sequentially in the given order, and builds the report.</summary>
    internal static async Task<ConformanceReport> RunAllAsync(
        TransportSubject subject,
        IReadOnlyList<ConformanceAssertion> assertions,
        TransportSuiteOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<ConformanceResult>();
        foreach (var assertion in assertions)
        {
            foreach (var face in assertion.Faces)
            {
                results.Add(await RunAsync(subject, assertion, face, options, cancellationToken).ConfigureAwait(false));
            }
        }

        results.AddRange(UnknownWaiverTargets(assertions, options.Waivers));
        return ConformanceReport.Create(subject.Name, results, options.Waivers);
    }

    private static async Task<ConformanceResult> ExecuteBodyAsync(
        ConformanceAssertion assertion,
        SuiteContext context,
        TransportSuiteOptions options,
        CancellationToken cancellationToken)
    {
        var face = context.Face;
        try
        {
            await Bounded.RunAsync(token => assertion.Body(context, token), options.AssertionTimeout, assertion.Name, cancellationToken).ConfigureAwait(false);
            return Result(assertion, face, ConformanceStatus.Passed, "passed");
        }
        catch (NotExercisedException ex)
        {
            return Result(assertion, face, ConformanceStatus.NotExercised, ex.Message);
        }
        catch (ConformanceVacuousException ex)
        {
            return Result(assertion, face, ConformanceStatus.Vacuous, ex.Message);
        }
        catch (ConformanceException ex)
        {
            return Result(assertion, face, ConformanceStatus.Failed, Describe(ex)) with { Exception = ex };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A transport crash or a kit bug is a result (Errored), never an unhandled exception that ends the run.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Result(assertion, face, ConformanceStatus.Errored, $"{ex.GetType().Name}: {ex.Message}") with { Exception = ex };
        }
    }

    // The context is disposed after the body, on every path. A failure to dispose is a result too, but never replaces a
    // result that is already a failure or an error: the first problem is the one the report shows.
    private static async Task<ConformanceResult> DisposeAsync(SuiteContext context, ConformanceAssertion assertion, TransportFace face, ConformanceResult outcome)
    {
        try
        {
            await context.DisposeAsync().ConfigureAwait(false);
            return outcome;
        }
        catch (ConformanceException ex) when (outcome.Status is not (ConformanceStatus.Failed or ConformanceStatus.Errored))
        {
            return Result(assertion, face, ConformanceStatus.Failed, Describe(ex)) with { Exception = ex };
        }
#pragma warning disable CA1031 // See ExecuteBodyAsync: a crash is a result.
        catch (Exception ex) when (outcome.Status is not (ConformanceStatus.Failed or ConformanceStatus.Errored))
#pragma warning restore CA1031
        {
            return Result(assertion, face, ConformanceStatus.Errored, $"disposing the run threw {ex.GetType().Name}: {ex.Message}") with { Exception = ex };
        }
#pragma warning disable CA1031 // The outcome is already a failure or an error; this one is dropped in its favour.
        catch (Exception)
#pragma warning restore CA1031
        {
            return outcome;
        }
    }

    private static async Task DisposeQuietlyAsync(SuiteContext context)
    {
        try
        {
            await context.DisposeAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The run is being abandoned for the caller's cancellation; a disposal failure must not replace it.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>
    /// A waiver turns Failed and Errored into Waived; it turns a Passed result it covers into a Failed "no longer needed"
    /// one (P8a-8: a fixed row must lose its waiver); it never touches Vacuous or NotExercised.
    /// </summary>
    private static ConformanceResult ApplyWaivers(ConformanceResult result, IReadOnlyList<ConformanceWaiver> waivers)
    {
        if (result.Status is not (ConformanceStatus.Failed or ConformanceStatus.Errored or ConformanceStatus.Passed))
        {
            return result;
        }

        var waiver = waivers.FirstOrDefault(w => Covers(w, result));
        if (waiver is null)
        {
            return result;
        }

        return result.Status == ConformanceStatus.Passed
            ? result with
            {
                Status = ConformanceStatus.Failed,
                Detail = $"waiver for {waiver.RequirementId} is no longer needed: {result.Assertion.Name} passes on {result.Face} (waiver reason: {waiver.Reason})",
                Waiver = waiver,
            }
            : result with
            {
                Status = ConformanceStatus.Waived,
                Detail = $"waived ({(waiver.Owner is null ? "permanent" : "owner " + waiver.Owner)}): {waiver.Reason} | {result.Detail}",
                Waiver = waiver,
            };
    }

    private static bool Covers(ConformanceWaiver waiver, ConformanceResult result) =>
        result.Assertion.RequirementIds.Contains(waiver.RequirementId, StringComparer.Ordinal)
        && (waiver.Assertion is null || string.Equals(waiver.Assertion, result.Assertion.Name, StringComparison.Ordinal))
        && (waiver.Face is null || waiver.Face == result.Face);

    // A waiver aimed at an assertion that does not run is a typo that would otherwise silently waive nothing.
    private static IEnumerable<ConformanceResult> UnknownWaiverTargets(IReadOnlyList<ConformanceAssertion> assertions, IReadOnlyList<ConformanceWaiver> waivers)
    {
        var names = assertions.Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var waiver in waivers.Where(w => w.Assertion is not null && !names.Contains(w.Assertion)))
        {
            var synthetic = new ConformanceAssertion("waiver", [waiver.RequirementId], RequirementLevel.Must, [TransportFace.Async], static (_, _) => Task.CompletedTask);
            yield return new ConformanceResult(
                synthetic,
                TransportFace.Async,
                ConformanceStatus.Failed,
                $"unknown waiver target '{Text.Truncate(waiver.Assertion!)}'")
            {
                Waiver = waiver,
            };
        }
    }

    private static ConformanceResult Result(ConformanceAssertion assertion, TransportFace face, ConformanceStatus status, string detail) =>
        new(assertion, face, status, detail);

    private static string Describe(ConformanceException ex) =>
        ex.Expected is null && ex.Actual is null
            ? ex.Message
            : string.Create(CultureInfo.InvariantCulture, $"{ex.Message} (expected: {ex.Expected}; actual: {ex.Actual})");
}
