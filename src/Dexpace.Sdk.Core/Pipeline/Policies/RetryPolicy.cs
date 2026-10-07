// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A retry pipeline policy that retries failed requests with exponential back-off and
/// full jitter, optionally honoring <c>Retry-After</c> response headers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Retryable statuses:</b> 408, 429, 500, 502, 503, 504. Any other status (including 4xx)
/// is returned immediately.
/// </para>
/// <para>
/// <b>Retryable exceptions:</b> <see cref="ServiceRequestException"/> (request never sent) and
/// <see cref="ServiceResponseException"/> (sent but response unreadable). All other exceptions,
/// including <see cref="OperationCanceledException"/>, propagate unchanged.
/// </para>
/// <para>
/// <b>Non-idempotent requests</b> are retried only when the request body is replayable
/// (or absent) AND <see cref="RetryOptions.RetryNonIdempotentWhenReplayable"/> is
/// <see langword="true"/>.
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> TRACE is no longer retried; idempotency is read from the single internal
/// set GET, HEAD, OPTIONS, PUT, DELETE (HTTP-9).
/// </para>
/// <para>
/// <b>Delay:</b> when <c>Retry-After</c> is present and
/// <see cref="RetryOptions.HonorRetryAfter"/> is <see langword="true"/>, the parsed value
/// is used; otherwise the delay is drawn from a uniform random distribution over
/// <c>[0, min(BaseDelay × 2^attempt, MaxDelay)]</c> (full jitter). The
/// <see cref="TimeProvider"/> passed to the constructor drives both the current-time lookup
/// (for HTTP-date parsing) and both waits, <see cref="TimeProviderWaits.DelayAsync"/> and
/// <see cref="TimeProviderWaits.Sleep"/>, so tests can control delays without real sleeps. Every delay, hinted or
/// computed, is clamped to 365 days, and a delay longer than a timer accepts is waited as successive shorter waits.
/// </para>
/// <para>
/// <b>Request isolation:</b> every attempt is driven with the request held at entry, so a retry never carries what a
/// downstream policy wrote during the previous attempt (RETRY-44, PIPE-16). The synchronous path waits with a genuine
/// blocking wait over the <see cref="TimeProvider"/>, not sync-over-async (PIPE-28).
/// </para>
/// <para>
/// <b>Response disposal:</b> when a retryable response is going to be retried, the response
/// is disposed before sleeping to release the connection promptly.
/// </para>
/// </remarks>
public sealed class RetryPolicy : HttpPipelinePolicy
{
    private static readonly HashSet<int> s_retryableStatusCodes = [408, 429, 500, 502, 503, 504];

    // RETRY-18 / RECOV-26: the ceiling every pacing delta is clamped to, whether it came from a server hint or the
    // back-off schedule.
    private static readonly TimeSpan s_maxPacingDelay = TimeSpan.FromDays(365);

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new <see cref="RetryPolicy"/>.
    /// </summary>
    /// <param name="timeProvider">
    /// The time source used to obtain the current UTC instant (for <c>Retry-After</c> HTTP-date
    /// parsing) and to drive <see cref="TimeProviderWaits"/>.
    /// Defaults to <see cref="TimeProvider.System"/> when <see langword="null"/>.
    /// </param>
    public RetryPolicy(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.Retry;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(RetryPolicy));

    // MA0051 waiver: phase 6a rewrites the retry policy on this signature (design §6.1); splitting it now would be
    // rewritten there.
#pragma warning disable MA0051
    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Options.Retry;
        var attempt = 0;

        // RETRY-44 / PIPE-16: every attempt re-sends the request this policy received; a downstream stamp (auth, a
        // per-attempt header) is on the callee's copy and there is nothing to restore.
        while (true)
        {
            Response? response = null;
            Exception? caughtException = null;

            try
            {
                var drive = context.ForAttempt(attempt);
                response = async
                    ? await continuation.RunAsync(request, drive).ConfigureAwait(false)
                    : continuation.Run(request, drive);
            }
            catch (Exception ex) when (IsRetryableException(ex))
            {
                caughtException = ex;
            }

            var canRetryRequest = CanRetryRequest(request, options);

            if (caughtException is not null)
            {
                // Exception path: re-throw if exhausted or not retryable.
                if (attempt >= options.MaxRetryAttempts || !canRetryRequest)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(caughtException);
                }

                await SleepAsync(DelayFor(null, attempt, options), async, context.CancellationToken).ConfigureAwait(false);
                attempt++;
                continue;
            }

            // Success or non-retryable response path (PIPE-40: the in-flight response is returned undisposed).
            if (attempt >= options.MaxRetryAttempts
                || !canRetryRequest
                || !IsRetryableStatus(response!.Status.Code))
            {
                return response!;
            }

            // Parse Retry-After before disposing the response.
            TimeSpan? retryAfterDelay = null;
            if (options.HonorRetryAfter)
            {
                var retryAfterHeader = response.Headers.Get(HttpHeaderName.WellKnown.RetryAfter);
                retryAfterDelay = ParseRetryAfter(retryAfterHeader);
            }

            // PIPE-40: dispose the superseded response before sleeping, to release the connection promptly; a throwing
            // dispose is suppressed and cannot mask the continuation attempt's outcome.
            if (async)
            {
                await Disposal.DisposeQuietlyAsync(response, logger: context.State.Logger).ConfigureAwait(false);
            }
            else
            {
                Disposal.DisposeQuietly(response, logger: context.State.Logger);
            }

            await SleepAsync(DelayFor(retryAfterDelay, attempt, options), async, context.CancellationToken).ConfigureAwait(false);
            attempt++;
        }
    }
#pragma warning restore MA0051

    private static bool IsRetryableException(Exception ex) =>
        ex is ServiceRequestException or ServiceResponseException;

    private static bool IsRetryableStatus(int code) =>
        s_retryableStatusCodes.Contains(code);

    private static bool CanRetryRequest(
        Request request,
        RetryOptions options)
    {
        var bodyReplayable = request.Body is null || request.Body.IsReplayable;
        return bodyReplayable
            && (request.Method.IsIdempotent || options.RetryNonIdempotentWhenReplayable);
    }

    /// <summary>
    /// Parses a <c>Retry-After</c> header value.
    /// Returns the delay as a <see cref="TimeSpan"/>, or <see langword="null"/> when the
    /// value cannot be interpreted.
    /// </summary>
    /// <remarks>
    /// Accepts two forms per RFC 7231 §7.1.3:
    /// <list type="bullet">
    ///   <item>An integer representing a delta-seconds value.</item>
    ///   <item>An HTTP-date whose distance from the current instant is the delay (floored at zero).</item>
    /// </list>
    /// </remarks>
    private TimeSpan? ParseRetryAfter(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return null;
        }

        // Delta-seconds form.
        if (int.TryParse(headerValue, System.Globalization.NumberStyles.None, null, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        // HTTP-date form (RFC 1123 / "r" format).
        if (DateTimeOffset.TryParseExact(
                headerValue,
                "r",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var httpDate))
        {
            var delta = httpDate - _timeProvider.GetUtcNow();
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        return null;
    }

    /// <summary>
    /// Sleeps for the appropriate back-off delay, using <paramref name="explicitDelay"/> when
    /// supplied (from <c>Retry-After</c>) or full-jitter exponential back-off otherwise.
    /// </summary>
    private static TimeSpan DelayFor(TimeSpan? explicitDelay, int attempt, RetryOptions options)
    {
        TimeSpan delay;

        if (explicitDelay.HasValue)
        {
            delay = explicitDelay.Value;
        }
        else
        {
            // Full jitter: uniform in [0, min(BaseDelay * 2^attempt, MaxDelay)].
            // Guard the shift: cap at 30 to avoid overflow (2^30 ≈ 1e9 ms >> any MaxDelay).
            // Saturate BEFORE multiplying: if BaseDelay.Ticks * 2^shift would overflow,
            // clamp to MaxDelay.Ticks rather than letting the long wrap negative.
            var shift = Math.Min(attempt, 30);
            var baseTicks = options.BaseDelay.Ticks;
            var maxTicks = options.MaxDelay.Ticks;
            var capTicks = baseTicks <= (maxTicks >> shift)
                ? baseTicks << shift
                : maxTicks;
            var cap = TimeSpan.FromTicks(Math.Min(capTicks, maxTicks));
            delay = TimeSpan.FromTicks((long)(cap.Ticks * Random.Shared.NextDouble()));
        }

        return delay > s_maxPacingDelay ? s_maxPacingDelay : delay;
    }

    // Both paths run through TimeProviderWaits (CFG-15, CFG-18): the async path awaits a timer, the sync path is a genuine
    // blocking wait, and each runs a delay above the timer's ceiling as successive bounded waits (S7). A non-positive
    // delay is skipped so a hinted zero never arms a timer.
    private async ValueTask SleepAsync(TimeSpan delay, bool async, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        if (async)
        {
            await _timeProvider.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _timeProvider.Sleep(delay, cancellationToken);
        }
    }
}
