// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.Recovery;

namespace Dexpace.Sdk.Core.Resilience;

/// <summary>
/// The one retry loop both stacks drive (RETRY-13, RETRY-14, RECOV-30; design §6.1 A, P6a-3).
/// </summary>
/// <remarks>
/// <para>
/// Each stack is an adapter that supplies a <see cref="RetrySend"/> and the hooks that differ; the engine performs the
/// sends, applies the classifier, the re-send gate and the attempt cap, resolves the delay, waits, keeps the suppressed
/// trail and returns the terminal <see cref="Outcome"/>. It never throws for a non-fatal failure: the adapter rethrows a
/// <see cref="Outcome.Failure"/> with its original stack (RETRY-33). A fatal exception passes every frame untouched
/// (RETRY-25).
/// </para>
/// <para>
/// The class holds only the clock and the random source; everything a call needs is a local of
/// <see cref="RunAsync"/> or lives in a per-call <see cref="RetryCall"/>, so a shared instance is safe for concurrent
/// calls (RETRY-42, RECOV-28). The loop is one <see langword="while"/> in one async method, so N retries reuse one state
/// machine and the stack depth is constant (RETRY-30). The class references no pipeline type (P6a-4).
/// </para>
/// </remarks>
/// <param name="timeProvider">The caller-owned clock; it is never disposed (RETRY-45).</param>
/// <param name="random">A thread-safe source of samples in <c>[0, 1)</c>.</param>
internal sealed class RetryEngine(TimeProvider timeProvider, Func<double> random)
{
    /// <summary>Runs the sequence of sends for one logical call.</summary>
    /// <param name="run">The call's input.</param>
    /// <param name="async">Whether the call is asynchronous; when <see langword="false"/> nothing suspends.</param>
    /// <param name="cancellationToken">The call's token: checked before and after every send and used for every wait.</param>
    /// <returns>The terminal outcome: a live response, or the failure to surface with its trail attached.</returns>
    internal async ValueTask<Outcome> RunAsync(RetryRun run, bool async, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var call = new RetryCall(run, async, timeProvider.GetTimestamp(), cancellationToken);
        while (true)
        {
            call.Sends++;
            var outcome = await SendOnceAsync(call).ConfigureAwait(false);
            var terminal = await DecideAsync(call, outcome).ConfigureAwait(false);
            if (terminal is not null)
            {
                return terminal;
            }
        }
    }

    private static Outcome Terminal(RetryCall call, Outcome outcome)
    {
        if (outcome is Outcome.Failure failure)
        {
            foreach (var prior in call.Trail)
            {
                ExceptionTrail.AddSuppressed(failure.Error, prior);
            }
        }

        return outcome;
    }

    private static bool DefaultCondition(RetryCall call, Outcome outcome)
    {
        var statuses = call.Run.Options.RetryableStatusCodes;
        return outcome switch
        {
            Outcome.Success success => statuses.Contains(success.Response.Status.Code),
            Outcome.Failure failure => RetryFacts.IsRetryableFailure(failure.Error, statuses, call.Token),
            _ => false,
        };
    }

    private static Headers? PacingHeaders(Outcome outcome)
    {
        if (outcome is Outcome.Success success)
        {
            return success.Response.Headers;
        }

        if (outcome is Outcome.Failure failure)
        {
            foreach (var cause in ExceptionFacts.EnumerateCauses(failure.Error))
            {
                if (cause is HttpResponseException http)
                {
                    return http.Response.Headers;
                }
            }
        }

        return null;
    }

    private static async ValueTask<Outcome> SendOnceAsync(RetryCall call)
    {
        var run = call.Run;
        if (call.Token.IsCancellationRequested)
        {
            return new Outcome.Failure(new OperationCanceledException(call.Token));
        }

        var stamped = Stamp(run.Request, run.Options.AttemptHeaderName, call.Sends);
        Outcome outcome;
        try
        {
            outcome = await run.Send(stamped, call.Sends, call.Async, call.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            return new Outcome.Failure(ex);
        }

        if (outcome is Outcome.Success late && call.Token.IsCancellationRequested)
        {
            // RETRY-32, P6a-25: the caller abandoned the call; a response nobody will read is a leak.
            await DisposeQuietlyAsync(call, late.Response).ConfigureAwait(false);
            return new Outcome.Failure(new OperationCanceledException(call.Token));
        }

        return outcome;
    }

    private static Request Stamp(Request request, string? headerName, int send) =>
        headerName is null ? request : request.WithHeader(headerName, send.ToString(CultureInfo.InvariantCulture));

    // Returns the terminal outcome, or null to continue with the next send.
    private async ValueTask<Outcome?> DecideAsync(RetryCall call, Outcome outcome)
    {
        var run = call.Run;
        if (run.MaxRetries <= 0)
        {
            return Terminal(call, outcome);
        }

        bool retryable;
        try
        {
            retryable = ShouldRetry(call, outcome);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            await ReleaseUnwantedAsync(call, outcome).ConfigureAwait(false);
            return Terminal(call, new Outcome.Failure(ex));
        }

        if (!retryable)
        {
            return Terminal(call, outcome);
        }

        if (call.Sends > run.MaxRetries)
        {
            // OBS-29, P6a-28: the cap was spent while the condition and the gate both held.
            run.Observer?.OnExhausted?.Invoke(call.Sends);
            return Terminal(call, outcome);
        }

        return await RetryAsync(call, outcome).ConfigureAwait(false);
    }

    private static bool ShouldRetry(RetryCall call, Outcome outcome)
    {
        if (call.Token.IsCancellationRequested || !call.Resendable)
        {
            return false;
        }

        var forced = call.Run.Condition?.Invoke(new RetryAttempt(call.Sends, outcome));
        return forced ?? DefaultCondition(call, outcome);
    }

    private async ValueTask<Outcome?> RetryAsync(RetryCall call, Outcome outcome)
    {
        var released = false;
        try
        {
            var budget = call.Run.Budget;
            if (budget.IsSpent(call.Start))
            {
                return Terminal(call, outcome);
            }

            var delay = ResolveDelay(call, outcome);
            if (!budget.Allows(call.Start, delay))
            {
                return Terminal(call, outcome);
            }

            call.Run.Observer?.OnAttemptFailed?.Invoke(outcome, delay);
            var entry = await ReleaseAsync(call, outcome).ConfigureAwait(false);
            released = true;
            if (entry is not null)
            {
                call.Trail.Add(entry);
            }

            await WaitAsync(call, budget.Clamp(call.Start, delay)).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            if (!released)
            {
                await ReleaseUnwantedAsync(call, outcome).ConfigureAwait(false);
            }

            return Terminal(call, new Outcome.Failure(ex));
        }
    }

    private TimeSpan ResolveDelay(RetryCall call, Outcome outcome)
    {
        var run = call.Run;
        var attempt = new RetryAttempt(call.Sends, outcome);
        var delay = OverrideDelay(call, attempt);
        if (delay is null && run.HonorPacing)
        {
            delay = PacingDelay(outcome);
        }

        delay ??= RetryBackoff.Compute(call.Sends, run.Options, SafeRandom);
        return RetryBackoff.ClampToCeiling(delay.Value);
    }

    private static TimeSpan? OverrideDelay(RetryCall call, RetryAttempt attempt)
    {
        var run = call.Run;
        if (run.DelayOverride is null)
        {
            return null;
        }

        try
        {
            var value = run.DelayOverride(attempt);
            if (value is { } delay && delay < TimeSpan.Zero)
            {
                run.Observer?.OnOverrideFailed?.Invoke(new InvalidOperationException("The retry delay override returned a negative delay."));
                return null;
            }

            return value;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            run.Observer?.OnOverrideFailed?.Invoke(ex);
            return null;
        }
    }

    // RETRY-22, RECOV-29: a failure while reading the hint degrades to "no hint"; it never replaces the upstream failure.
    private TimeSpan? PacingDelay(Outcome outcome)
    {
        try
        {
            return PacingHeaders(outcome) is { } headers
                ? RetryPacing.TryGetHint(headers, timeProvider.GetUtcNow(), random)
                : null;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            return null;
        }
    }

    private double SafeRandom()
    {
        try
        {
            return random();
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            return 0.5;
        }
    }

    // RETRY-35, P6a-21: the hint has been read; now the response is released. An error response is drained (at most 1 MiB,
    // the original disposed) into the HttpResponseException that becomes the trail entry; any other status is disposed and
    // leaves no entry (XCUT-8 forbids a successful exception). A failure is its own entry.
    private static async ValueTask<Exception?> ReleaseAsync(RetryCall call, Outcome outcome)
    {
        if (outcome is Outcome.Failure failure)
        {
            return failure.Error;
        }

        var response = ((Outcome.Success)outcome).Response;
        if (!response.Status.IsError)
        {
            await DisposeQuietlyAsync(call, response).ConfigureAwait(false);
            return null;
        }

        try
        {
            // The buffered copy is owned by the exception that carries it (it is a replayable in-memory body).
#pragma warning disable CA2000
            var buffered = call.Async
                ? await ErrorBodyBuffer.CaptureAsync(response, call.Token, call.Run.Logger).ConfigureAwait(false)
                : ErrorBodyBuffer.Capture(response, call.Token, call.Run.Logger);
#pragma warning restore CA2000
            return ErrorMapping.ToException(buffered);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // The drain failed: the original is already disposed (fact 12), and the failure is the entry.
            return ex;
        }
    }

    private static ValueTask ReleaseUnwantedAsync(RetryCall call, Outcome outcome) =>
        outcome is Outcome.Success success ? DisposeQuietlyAsync(call, success.Response) : ValueTask.CompletedTask;

    private static async ValueTask DisposeQuietlyAsync(RetryCall call, Response response)
    {
        if (call.Async)
        {
            await Disposal.DisposeQuietlyAsync(response, logger: call.Run.Logger).ConfigureAwait(false);
        }
        else
        {
            Disposal.DisposeQuietly(response, logger: call.Run.Logger);
        }
    }

    // RETRY-26, RETRY-31: a timer through TimeProviderWaits; a zero delay continues inline and arms nothing.
    private async ValueTask WaitAsync(RetryCall call, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        if (call.Async)
        {
            await timeProvider.DelayAsync(delay, call.Token).ConfigureAwait(false);
        }
        else
        {
            timeProvider.Sleep(delay, call.Token);
        }
    }

    /// <summary>The state of one logical call: the engine's only mutable data, never shared across calls.</summary>
    private sealed class RetryCall(RetryRun run, bool async, long start, CancellationToken token)
    {
        internal RetryRun Run { get; } = run;

        internal bool Async { get; } = async;

        internal CancellationToken Token { get; } = token;

        internal long Start { get; } = start;

        internal bool Resendable { get; } = RetryFacts.IsResendable(run.Request);

        internal int Sends { get; set; }

        internal List<Exception> Trail { get; } = [];
    }
}
