// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The status, waiver and bound logic every suite shares (design section C, P10-7): one fresh context per (assertion, face), a
/// bounded body, the six statuses and the rule that a waiver must stay needed. Generic over the suite's context, so the transport,
/// invariant and codec runners are thin callers and a status means the same thing in all of them.
/// </summary>
internal static class SuiteRunnerCore
{
    /// <summary>Runs one assertion on one face against a fresh context and applies the waivers.</summary>
    /// <typeparam name="TContext">What one run owns; disposed after the body on every path.</typeparam>
    /// <param name="assertion">The assertion.</param>
    /// <param name="face">The face; the assertion must declare it.</param>
    /// <param name="create">Builds the run's context. Called once.</param>
    /// <param name="assertionTimeout">The bound on the body.</param>
    /// <param name="waivers">The waivers in force.</param>
    /// <param name="cancellationToken">Abandons the run with <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="assertion"/> does not declare <paramref name="face"/>.</exception>
    internal static async Task<ConformanceResult> RunAsync<TContext>(
        ConformanceAssertion assertion,
        TransportFace face,
        Func<TContext> create,
        TimeSpan assertionTimeout,
        IReadOnlyList<ConformanceWaiver> waivers,
        CancellationToken cancellationToken)
        where TContext : class, IAsyncDisposable
    {
        if (!assertion.Faces.Contains(face))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' does not declare the {face} face.", nameof(face));
        }

        var context = create();
        ConformanceResult outcome;
        try
        {
            outcome = await ExecuteBodyAsync(assertion, face, context, assertionTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await DisposeQuietlyAsync(context).ConfigureAwait(false);
            throw;
        }

        return ApplyWaivers(await DisposeAsync(context, assertion, face, outcome).ConfigureAwait(false), waivers);
    }

    /// <summary>Runs every assertion on every face it declares, sequentially in the given order, and builds the report.</summary>
    /// <param name="subjectName">The subject's name.</param>
    /// <param name="suite">The suite that produced the report; selects the report's preamble.</param>
    /// <param name="assertions">The assertions, in order.</param>
    /// <param name="runOne">Runs one assertion on one face.</param>
    /// <param name="waivers">The waivers in force.</param>
    internal static async Task<ConformanceReport> RunAllAsync(
        string subjectName,
        SuiteIdentity suite,
        IReadOnlyList<ConformanceAssertion> assertions,
        Func<ConformanceAssertion, TransportFace, Task<ConformanceResult>> runOne,
        IReadOnlyList<ConformanceWaiver> waivers)
    {
        var results = new List<ConformanceResult>();
        foreach (var assertion in assertions)
        {
            foreach (var face in assertion.Faces)
            {
                results.Add(await runOne(assertion, face).ConfigureAwait(false));
            }
        }

        results.AddRange(UnknownWaiverTargets(assertions, waivers));
        return ConformanceReport.Create(subjectName, results, waivers, suite);
    }

    /// <summary>
    /// A waiver turns Failed and Errored into Waived; it turns a Passed result it covers into a Failed "no longer needed"
    /// one (P8a-8: a fixed row must lose its waiver); it never touches Vacuous or NotExercised.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="waivers">The waivers in force.</param>
    internal static ConformanceResult ApplyWaivers(ConformanceResult result, IReadOnlyList<ConformanceWaiver> waivers)
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

    /// <summary>A result row for <paramref name="assertion"/> on <paramref name="face"/>.</summary>
    internal static ConformanceResult Result(ConformanceAssertion assertion, TransportFace face, ConformanceStatus status, string detail) =>
        new(assertion, face, status, detail);

    private static async Task<ConformanceResult> ExecuteBodyAsync(
        ConformanceAssertion assertion,
        TransportFace face,
        object context,
        TimeSpan assertionTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            await Bounded.RunAsync(token => assertion.Body(context, token), assertionTimeout, assertion.Name, cancellationToken).ConfigureAwait(false);
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
#pragma warning disable CA1031 // A subject crash or a kit bug is a result (Errored), never an unhandled exception that ends the run.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Result(assertion, face, ConformanceStatus.Errored, $"{ex.GetType().Name}: {ex.Message}") with { Exception = ex };
        }
    }

    // The context is disposed after the body, on every path. A failure to dispose is a result too, but never replaces a
    // result that is already a failure or an error: the first problem is the one the report shows.
    private static async Task<ConformanceResult> DisposeAsync(IAsyncDisposable context, ConformanceAssertion assertion, TransportFace face, ConformanceResult outcome)
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

    private static async Task DisposeQuietlyAsync(IAsyncDisposable context)
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

    private static string Describe(ConformanceException ex) =>
        ex.Expected is null && ex.Actual is null
            ? ex.Message
            : string.Create(CultureInfo.InvariantCulture, $"{ex.Message} (expected: {ex.Expected}; actual: {ex.Actual})");
}
