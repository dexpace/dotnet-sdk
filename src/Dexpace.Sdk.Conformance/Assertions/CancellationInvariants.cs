// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 tests/conformance/xcut/cancellation-and-timeout.conformance.test.ts and ruby-sdk@5b17395
// gems/dexpace-conformance/lib/dexpace/conformance/invariant_suite/ (cancellation and timeout), reshaped for .NET's token-based
// cancellation (design §10 entry 8: there is no interrupt flag to re-assert; the caller's token stays signalled).
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>XCUT-1</c> to <c>XCUT-3</c>: cancellation is terminal and distinct from a timeout, a timeout is a retryable failure that
/// does not signal the caller's token, and an inter-attempt wait is promptly cancellable and disarms its timer. The core-level
/// clauses, through the subject's pipeline; the transport-level clauses are <c>transport-3</c> and <c>transport-4</c> (P10-6).
/// </summary>
internal static class CancellationInvariants
{
    internal static ConformanceAssertion[] Assertions() =>
    [
        InvariantAssertionBuilder.Define("xcut-1.cancel-is-terminal", ["XCUT-1"], RequirementLevel.Must, InvariantAssertionBuilder.Both, CancelIsTerminalAsync),
        InvariantAssertionBuilder.Define("xcut-2.timeout-is-retryable", ["XCUT-2"], RequirementLevel.Must, InvariantAssertionBuilder.Both, TimeoutIsRetryableAsync),
        InvariantAssertionBuilder.Define("xcut-3.wait-promptly-cancellable", ["XCUT-3"], RequirementLevel.Must, InvariantAssertionBuilder.Both, WaitIsPromptlyCancellableAsync),
    ];

    // A send parked in the transport, the caller cancels: one send, the caller's own token on the exception, no retry.
    private static async Task CancelIsTerminalAsync(InvariantContext context, CancellationToken token)
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = context.Transport(ProbeTransport.Behavior.HangUntilCancelled(sent));
        var pipeline = context.Build(transport, InvariantOptions.Retrying(2));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);

        var call = context.FailureOfAsync(pipeline, Request.Get(InvariantOptions.Url), "the cancelled call", token, caller.Token);
        await Bounded.WaitAsync(sent.Task, context.Bound, "the send to reach the transport", token).ConfigureAwait(false);
        await caller.CancelAsync().ConfigureAwait(false);
        var failure = await call.ConfigureAwait(false);

        Check.True(
            failure is OperationCanceledException cancelled && cancelled.CancellationToken == caller.Token,
            "a cancelled caller must surface OperationCanceledException carrying its own token, kept separate from a timeout",
            "OperationCanceledException with the caller's token",
            Outcome.Name(failure));
        Check.True(caller.Token.IsCancellationRequested, "the caller's token must still be signalled after the call ends", "signalled", "clear");
        Check.True(failure is not IRetryableError { IsRetryable: true }, "a cancellation must not report itself retryable", "non-retryable", "retryable");
        Check.Equal(transport.Sends, 1, "the number of sends of a cancelled call (a cancelled operation is never re-issued)");
    }

    // An attempt deadline over a transport that never answers: a retryable timeout, the caller's token clear, the call re-issued.
    private static async Task TimeoutIsRetryableAsync(InvariantContext context, CancellationToken token)
    {
        var transport = context.Transport(ProbeTransport.Behavior.HangUntilCancelled());
        var options = new DexpaceClientOptions { AttemptTimeout = TimeSpan.FromMilliseconds(50), Retry = InvariantOptions.Fast(2) };
        var pipeline = context.Build(transport, options);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);

        var failure = await context.FailureOfAsync(pipeline, Request.Get(InvariantOptions.Url), "the call whose every attempt times out", token, caller.Token).ConfigureAwait(false);

        Check.True(failure is not null, "a call whose every attempt times out must fail", "a failure", "the call succeeded");
        Check.True(failure is not OperationCanceledException, "a deadline must not be reported as a cancellation", "a timeout", Outcome.Name(failure));
        Check.True(Outcome.FindTimeout(failure) is not null, "the failure must be classified as a timeout (the timeout branch is checked first)", "a timeout exception", Outcome.Name(failure));
        Check.True(failure is IRetryableError { IsRetryable: true }, "a timeout must be a retryable failure", "retryable", Outcome.Name(failure));
        Check.True(!caller.Token.IsCancellationRequested, "a timeout must not signal the caller's token", "clear", "signalled");
        Check.True(transport.Sends >= 2, "a timed-out attempt must be re-issued by the retry layer", "at least 2 sends", transport.Sends.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // A retry waiting out a long backoff, the caller cancels: the call ends promptly with the caller's cancellation and every
    // timer the wait armed is disposed. The wait is on the run's recording clock, so the timers are countable.
    private static async Task WaitIsPromptlyCancellableAsync(InvariantContext context, CancellationToken token)
    {
        var transport = context.Transport(ProbeTransport.Behavior.Status(503));
        var options = new DexpaceClientOptions
        {
            Retry = new RetryOptions { MaxRetryAttempts = 3, FixedDelay = TimeSpan.FromSeconds(30), Jitter = 0 },
        };
        var pipeline = context.Build(transport, options);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);

        var call = context.FailureOfAsync(pipeline, Request.Get(InvariantOptions.Url), "the call cancelled during a 30 s retry wait", token, caller.Token);
        await Poll.UntilAsync(() => transport.Sends >= 1, context.Bound, "the first attempt to reach the transport", token).ConfigureAwait(false);

        // The wait arms a timer on the run's clock; a pipeline that waits some other way arms none and is judged on the rest.
        await Poll.HoldsWithinAsync(() => context.Time.TimersCreated >= 1, TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
        await caller.CancelAsync().ConfigureAwait(false);
        var failure = await call.ConfigureAwait(false);

        Check.True(
            failure is OperationCanceledException cancelled && cancelled.CancellationToken == caller.Token,
            "a cancelled retry wait must surface the cancellation, not a spurious timeout or the last error",
            "OperationCanceledException with the caller's token",
            Outcome.Name(failure));
        Check.Equal(transport.Sends, 1, "the number of sends of a call cancelled during its first wait");
        await Poll.UntilAsync(
            () => context.Time.TimersLive == 0,
            context.Bound,
            $"every timer the wait armed to be disposed ({context.Time.TimersCreated} created, {context.Time.TimersDisposed} disposed)",
            token).ConfigureAwait(false);
    }
}
