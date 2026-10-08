// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.Core.Resilience;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// The retry pipeline policy: it re-sends a failed request on a jittered exponential schedule, honouring the server's
/// pacing headers, and bounds each attempt with <see cref="DexpaceClientOptions.AttemptTimeout"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>One engine.</b> The policy is an adapter over <c>RetryEngine</c>, the loop the recovery stack
/// (<c>RetryRecovery</c>) drives too, so the classifier, the backoff calculator and the pacing parser are written
/// once (RETRY-13). The policy has no total-time budget (RETRY-28); <see cref="DexpaceClientOptions.OverallTimeout"/> is
/// enforced outside it.
/// </para>
/// <para>
/// <b>Classification.</b> A response is retried when its status is in <see cref="RetryOptions.RetryableStatusCodes"/>
/// (default 408, 429, 500, 502, 503, 504). An exception is retried when it, or any cause in its chain, reports
/// <see cref="IRetryableError.IsRetryable"/> or is in the I/O family (<see cref="System.IO.IOException"/>,
/// <see cref="System.Net.Sockets.SocketException"/>, <see cref="TimeoutException"/>, an
/// <see cref="System.Net.Http.HttpRequestException"/> with no status); an <see cref="HttpResponseException"/> anywhere in
/// the chain is decided by the configured status set alone. A cancelled call is never retried.
/// </para>
/// <para>
/// <b>Re-send gate.</b> A request is re-sent only when it has no body and an idempotent method (GET, HEAD, OPTIONS, PUT,
/// DELETE), or when its body is replayable (RETRY-5); the same gate applies to every failure. At most
/// <c>MaxRetryAttempts</c> retries follow the first send, or <c>RequestOptions.MaxRetries</c> when it is set; zero means
/// one send (RETRY-41).
/// </para>
/// <para>
/// <b>Delay.</b> In order: <see cref="GetDelayOverride"/>, the server's pacing hint (when
/// <see cref="RetryOptions.HonorRetryAfter"/> is on), <see cref="RetryOptions.FixedDelay"/>, then
/// <c>BaseDelay × Multiplier^(n−1)</c> capped at <c>MaxDelay</c> with symmetric jitter (RETRY-39). Every delay is clamped
/// to 365 days and waited through <see cref="TimeProvider"/>: the clock passed to the constructor, which the SDK never
/// disposes (RETRY-45).
/// </para>
/// <para>
/// <b>Failures.</b> A discarded error response is drained (at most 1 MiB) into an <see cref="HttpResponseException"/>; when
/// the call fails, the exception it throws carries every earlier attempt's failure in <see cref="SdkException.Suppressed"/>
/// or <see cref="ExceptionTrail"/>, oldest first (RETRY-34). When the retries are spent on a response, that response is
/// returned live and unread (PIPE-40). An attempt that exceeds <see cref="DexpaceClientOptions.AttemptTimeout"/> is a
/// retried <see cref="ServiceRequestTimeoutException"/>; the timeout is cooperative, so a transport that ignores its token
/// is not bounded by it.
/// </para>
/// <para>
/// <b>Request isolation:</b> every attempt is driven with the request held at entry, so a retry never carries what a
/// downstream policy wrote during the previous attempt (RETRY-44, PIPE-16). The synchronous path waits with a genuine
/// blocking wait over the <see cref="TimeProvider"/>, not sync-over-async (PIPE-28).
/// </para>
/// <para>
/// <b>Hooks.</b> <see cref="ShouldRetry"/> and <see cref="GetDelayOverride"/> are the two extension points; every other
/// member is sealed, so a subclass cannot change the stage or the loop (PIPE-36). They must be pure, stateless and fast:
/// the policy is shared across concurrent calls (RETRY-42). A server-driven override (RETRY-29) is a subclass:
/// </para>
/// <code>
/// public sealed class ServerHintRetryPolicy : RetryPolicy
/// {
///     protected override bool? ShouldRetry(RetryAttemptContext attempt) =>
///         attempt.Response?.Headers.Get("X-Should-Retry") switch
///         {
///             "true" or "1" or "yes" or "retry" =&gt; true,
///             "false" or "0" or "no" or "stop" =&gt; false,
///             _ =&gt; null,
///         };
/// }
/// </code>
/// <para>
/// <b>Diagnostic events (OBS-28, OBS-29):</b> the policy reports each retried failure, with the wait before the next
/// attempt, and a spent budget through the operation span's events (<c>dexpace.attempt.failed</c>,
/// <c>dexpace.retry.exhausted</c>), and a failing delay override through the log event 140
/// (<c>dexpace.retry.delay_override_failed</c>).
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> TRACE is no longer retried; idempotency is read from the single internal set GET, HEAD,
/// OPTIONS, PUT, DELETE (HTTP-9).
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> an HTTP-date <c>Retry-After</c> is parsed by <see cref="Http.Common.HttpDate"/> (CFG-30, RETRY-15;
/// phase 5a); RFC 850 and asctime are still ignored (a rejected date is "no hint", never a wrong wait; design §11 item 27).
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> classification is no longer limited to <see cref="ServiceRequestException"/> and
/// <see cref="ServiceResponseException"/>: a raw I/O-family exception, or any exception reporting
/// <see cref="IRetryableError.IsRetryable"/>, is retried, and the configured status set decides for a wrapped
/// <see cref="HttpResponseException"/> (RETRY-2, RETRY-37).
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> <c>HonorRetryAfter</c> now covers <c>retry-after-ms</c>, <c>x-ms-retry-after-ms</c> and
/// <c>X-RateLimit-Reset</c> as well as a fractional <c>Retry-After</c> (RETRY-15), and the schedule is symmetric jitter
/// around the capped delay (was full jitter over <c>[0, delay]</c>).
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> a retried error response's body is drained (at most 1 MiB) before it is disposed (was:
/// disposed unread), and the exception a failed call throws carries every earlier attempt's failure (RETRY-34).
/// </para>
/// <para>
/// <b>Breaking (source):</b> the class is no longer <see langword="sealed"/>; <see cref="HttpPipelinePolicy.Stage"/>,
/// <see cref="Process"/> and <see cref="ProcessAsync"/> are sealed overrides. Reflection over <c>IsSealed</c> changes.
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> <see cref="DexpaceClientOptions.AttemptTimeout"/> is enforced here (was: read by nothing).
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> a response that arrives after the caller's token fired is disposed and the call throws
/// <see cref="OperationCanceledException"/> (RETRY-32).
/// </para>
/// </remarks>
public class RetryPolicy : HttpPipelinePolicy
{
    private readonly TimeProvider _timeProvider;
    private readonly RetryEngine _engine;

    /// <summary>
    /// Initializes a new <see cref="RetryPolicy"/>.
    /// </summary>
    /// <param name="timeProvider">
    /// The time source used to obtain the current UTC instant (for HTTP-date and epoch pacing hints), to drive
    /// <see cref="TimeProviderWaits"/> and to arm the attempt timeout. It is never disposed. Defaults to
    /// <see cref="TimeProvider.System"/> when <see langword="null"/>.
    /// </param>
    public RetryPolicy(TimeProvider? timeProvider = null)
        : this(timeProvider, null)
    {
    }

    // P6a-27: the random source is injectable only here, for tests.
    internal RetryPolicy(TimeProvider? timeProvider, Func<double>? random)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _engine = new RetryEngine(_timeProvider, random ?? Random.Shared.NextDouble);
    }

    /// <inheritdoc/>
    public sealed override PipelineStage Stage => PipelineStage.Retry;

    /// <inheritdoc/>
    public sealed override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public sealed override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(RetryPolicy));

    /// <summary>
    /// Overrides the retry condition for one finished send (RETRY-29, RETRY-40).
    /// </summary>
    /// <remarks>
    /// Return <see langword="true"/> or <see langword="false"/> to force the condition, or <see langword="null"/> (the
    /// default) to defer to the classifier. The override flips the condition only: the re-send gate and the attempt cap
    /// still apply, and the hook is never asked about a cancelled call. If it throws, the call fails with an
    /// <see cref="InvalidOperationException"/> carrying the exception, the live response is disposed, and the attempt's own
    /// failure is attached as suppressed. Must be pure, stateless and fast.
    /// </remarks>
    /// <param name="attempt">The finished send.</param>
    /// <returns>The forced condition, or <see langword="null"/>.</returns>
    protected virtual bool? ShouldRetry(RetryAttemptContext attempt) => null;

    /// <summary>
    /// Overrides the wait before the next send (RETRY-39, RETRY-40).
    /// </summary>
    /// <remarks>
    /// Return the delay to use, or <see langword="null"/> (the default) to fall through to the server's pacing hint, the
    /// fixed delay and the backoff. A negative value, or an exception, is reported as the log event
    /// <c>dexpace.retry.delay_override_failed</c> (id 140) and falls through. Must be pure, stateless and fast.
    /// </remarks>
    /// <param name="attempt">The finished send.</param>
    /// <returns>The delay, or <see langword="null"/>.</returns>
    protected virtual TimeSpan? GetDelayOverride(RetryAttemptContext attempt) => null;

    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        OperationTelemetry.RetrySequenceStarted(context);
        var options = context.Options.Retry;
        var run = new RetryRun(request, options, (req, send, isAsync, token) => SendAttemptAsync(req, send, isAsync, context, continuation, token))
        {
            // RETRY-41, HTTP-35: the per-call override wins; zero means one send.
            MaxRetries = context.RequestOptions.MaxRetries ?? options.MaxRetryAttempts,
            Budget = RetryBudget.Unbounded,
            HonorPacing = options.HonorRetryAfter,
            Condition = attempt => ConsultShouldRetry(attempt, request, context),
            DelayOverride = attempt => GetDelayOverride(ToHookContext(attempt, request, context)),
            Observer = new RetryObserver(
                (outcome, delay) => ReportAttemptFailed(context, outcome, delay),
                sends => OperationTelemetry.RetriesExhausted(context, sends),
                failure => RetryLog.DelayOverrideFailed(context.State.Logger, failure)),
            Logger = context.State.Logger,
        };

        var terminal = await _engine.RunAsync(run, async, context.CancellationToken).ConfigureAwait(false);
        return Conclude(terminal);
    }

    private static Response Conclude(Outcome terminal)
    {
        switch (terminal)
        {
            case Outcome.Success success:
                // PIPE-40: the returned response is live and unread.
                return success.Response;
            case Outcome.Failure failure:
                ExceptionDispatchInfo.Capture(failure.Error).Throw();
                throw new UnreachableException();
            default:
                throw new UnreachableException();
        }
    }

    // RETRY-44, PIPE-16: each send drives a fresh ForAttempt copy with the request the policy received. send is 1-based and
    // ForAttempt is 0-based (R6).
    private async ValueTask<Outcome> SendAttemptAsync(
        Request request,
        int send,
        bool async,
        PipelineContext context,
        PipelineRunner continuation,
        CancellationToken callToken)
    {
        var drive = context.ForAttempt(send - 1);
        if (context.Options.AttemptTimeout is not { } timeout)
        {
            return new Outcome.Success(async
                ? await continuation.RunAsync(request, drive).ConfigureAwait(false)
                : continuation.Run(request, drive));
        }

        // P6a-23: a cooperative per-attempt deadline, linked to the call's token, disposed when the drive returns (the
        // response body is read afterwards without it; design fact 10).
        using var attemptSource = new CancellationTokenSource(timeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(callToken, attemptSource.Token);
        try
        {
            var linkedDrive = drive.WithCancellationToken(linked.Token);
            return new Outcome.Success(async
                ? await continuation.RunAsync(request, linkedDrive).ConfigureAwait(false)
                : continuation.Run(request, linkedDrive));
        }
        catch (OperationCanceledException ex) when (attemptSource.IsCancellationRequested && !callToken.IsCancellationRequested)
        {
            return new Outcome.Failure(new ServiceRequestTimeoutException(
                $"The attempt exceeded its timeout of {timeout}.",
                ex));
        }
    }

    private static RetryAttemptContext ToHookContext(RetryAttempt attempt, Request request, PipelineContext context) =>
        new(
            attempt.Send,
            request,
            (attempt.Outcome as Outcome.Success)?.Response,
            (attempt.Outcome as Outcome.Failure)?.Error,
            context);

    private bool? ConsultShouldRetry(RetryAttempt attempt, Request request, PipelineContext context)
    {
        var hookContext = ToHookContext(attempt, request, context);
        try
        {
            return ShouldRetry(hookContext);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            var wrapped = new InvalidOperationException($"{nameof(RetryPolicy)}.{nameof(ShouldRetry)} threw {ex.GetType().Name}.", ex);
            if (hookContext.Failure is { } failure)
            {
                ExceptionTrail.AddSuppressed(wrapped, failure);
            }

            throw wrapped;
        }
    }

    private static void ReportAttemptFailed(PipelineContext context, Outcome outcome, TimeSpan delay)
    {
        switch (outcome)
        {
            case Outcome.Success success:
                OperationTelemetry.AttemptFailed(context, success.Response, failure: null, delay);
                break;
            case Outcome.Failure failure:
                OperationTelemetry.AttemptFailed(context, response: null, failure.Error, delay);
                break;
        }
    }
}
