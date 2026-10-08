// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Recovery;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Resilience;

/// <summary>
/// One performed send, as the engine hands it to an adapter's hooks.
/// </summary>
/// <param name="Send">The 1-based number of sends performed so far, which is also the retry ordinal about to be scheduled.</param>
/// <param name="Outcome">The result of the send.</param>
internal readonly record struct RetryAttempt(int Send, Outcome Outcome);

/// <summary>
/// Performs one send. The adapter supplies it: the stage policy drives the pipeline continuation, the recovery stack drives
/// the transport and the response steps. Any non-fatal exception it throws becomes a <see cref="Outcome.Failure"/>.
/// </summary>
/// <param name="request">The request to send, already stamped with the attempt header when one is configured.</param>
/// <param name="send">The 1-based send number.</param>
/// <param name="async">Whether the call is on the asynchronous path; when <see langword="false"/> the task must be complete.</param>
/// <param name="cancellationToken">The call's token.</param>
/// <returns>The outcome of the send.</returns>
internal delegate ValueTask<Outcome> RetrySend(Request request, int send, bool async, CancellationToken cancellationToken);

/// <summary>An adapter's override of the retry condition: <see langword="null"/> defers to the classifier (RETRY-29).</summary>
/// <param name="attempt">The performed send.</param>
/// <returns>The forced condition, or <see langword="null"/>.</returns>
internal delegate bool? RetryCondition(RetryAttempt attempt);

/// <summary>An adapter's override of the delay: <see langword="null"/> falls through to the next source (RETRY-39).</summary>
/// <param name="attempt">The performed send.</param>
/// <returns>The delay, or <see langword="null"/>.</returns>
internal delegate TimeSpan? RetryDelayOverride(RetryAttempt attempt);

/// <summary>
/// The callbacks through which the engine reports to an adapter without referencing a pipeline type (the stage adapter maps
/// them onto the operation span's events and the call's logger).
/// </summary>
/// <param name="OnAttemptFailed">Called once per scheduled retry, before the response is released, with the planned wait.</param>
/// <param name="OnExhausted">Called when the cap was spent while the condition and the re-send gate held, with the send count.</param>
/// <param name="OnOverrideFailed">Called when the delay override threw or returned a negative delay.</param>
internal sealed record RetryObserver(
    Action<Outcome, TimeSpan>? OnAttemptFailed = null,
    Action<int>? OnExhausted = null,
    Action<Exception>? OnOverrideFailed = null);

/// <summary>Everything one logical call hands the engine; all of it is read-only configuration or input.</summary>
/// <param name="Request">The request the adapter received; every send re-sends it (RETRY-44).</param>
/// <param name="Options">The schedule and the configured status set.</param>
/// <param name="Send">Performs one send.</param>
internal sealed record RetryRun(Request Request, RetryOptions Options, RetrySend Send)
{
    /// <summary>The retries allowed after the first send: the per-call override or <c>MaxRetryAttempts</c> (RETRY-41).</summary>
    internal int MaxRetries { get; init; } = Options.MaxRetryAttempts;

    /// <summary>The total-time budget; the stage policy leaves it unbounded (RETRY-28).</summary>
    internal RetryBudget Budget { get; init; } = RetryBudget.Unbounded;

    /// <summary>Whether the server's pacing headers are read (<c>HonorRetryAfter</c> on the stage stack, always on for recovery).</summary>
    internal bool HonorPacing { get; init; } = true;

    /// <summary>The condition override, or <see langword="null"/>.</summary>
    internal RetryCondition? Condition { get; init; }

    /// <summary>The delay override, or <see langword="null"/>.</summary>
    internal RetryDelayOverride? DelayOverride { get; init; }

    /// <summary>The adapter's callbacks, or <see langword="null"/>.</summary>
    internal RetryObserver? Observer { get; init; }

    /// <summary>The logger for a suppressed dispose failure, or <see langword="null"/>.</summary>
    internal ILogger? Logger { get; init; }
}
