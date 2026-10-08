// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Resilience;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// The retry configuration of the recovery stack: pass it to a <see cref="RecoveryDispatcher"/> and every dispatched call
/// is retried by the same engine that drives <c>RetryPolicy</c> (RETRY-13, RECOV-30; design §6.1 H, P6a-5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Configuration only.</b> The class holds <see cref="Options"/>, a total-time budget and a clock; it keeps no per-call
/// state, so one instance is safe to share across concurrent calls and starts every call from a clean budget (RECOV-28).
/// </para>
/// <para>
/// <b>How a dispatcher uses it.</b> The request chain runs once, so one idempotency key and one client-identity line
/// cover the logical call. Each send is then the transport followed by the response steps of the response chain, and every
/// surviving response whose status is in <see cref="RetryOptions.RetryableStatusCodes"/> is buffered (at most 1 MiB) and
/// mapped to an <see cref="Errors.HttpResponseException"/> on arrival (RECOV-19, RETRY-36), so an initial 503 is retried
/// whether or not an <c>ErrorMappingStep</c> is installed. The recovery steps run once, on the terminal outcome. The
/// classifier, the re-send gate, the backoff, the pacing headers (always honoured, RECOV-22) and the trail are the
/// stage policy's, because the loop is the same (RETRY-8, RETRY-21, RETRY-34).
/// </para>
/// <para>
/// <b>The budget.</b> <see cref="TotalTimeout"/> bounds the whole sequence (RETRY-27): a retry is abandoned when the
/// elapsed time has reached the budget or when elapsed plus the planned delay would exceed it, and the last failure is then
/// surfaced unchanged with its trail. Zero, the default, means no budget. It lives here and not on
/// <see cref="RetryOptions"/> so the stage policy cannot read it (RETRY-28); it is not
/// <see cref="DexpaceClientOptions.OverallTimeout"/>, which cancels the whole pipeline call from outside both loops.
/// </para>
/// </remarks>
public sealed class RetryRecovery
{
    // RECOV-34: representable in nanoseconds (~292 years); TimeSpan.MaxValue is about 29 000 years.
    private static readonly TimeSpan s_maxRepresentable = TimeSpan.FromTicks(long.MaxValue / 100);

    /// <summary>
    /// Initializes a new <see cref="RetryRecovery"/>.
    /// </summary>
    /// <param name="options">The schedule, the retry count, the status set and the attempt header.</param>
    /// <param name="totalTimeout">The total-time budget, or <see cref="TimeSpan.Zero"/> (the default) for none.</param>
    /// <param name="timeProvider">
    /// The clock for the waits and the budget; it is never disposed. Defaults to <see cref="TimeProvider.System"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalTimeout"/> is negative or above about 292 years.</exception>
    public RetryRecovery(RetryOptions options, TimeSpan totalTimeout = default, TimeProvider? timeProvider = null)
        : this(options, totalTimeout, timeProvider, null)
    {
    }

    // P6a-27: the random source is injectable only here, for tests.
    internal RetryRecovery(RetryOptions options, TimeSpan totalTimeout, TimeProvider? timeProvider, Func<double>? random)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (totalTimeout < TimeSpan.Zero || totalTimeout > s_maxRepresentable)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalTimeout),
                "The total timeout must be zero (no budget) or greater, and no more than about 292 years.");
        }

        Options = options;
        TotalTimeout = totalTimeout;
        Clock = timeProvider ?? TimeProvider.System;
        Engine = new RetryEngine(Clock, random ?? Random.Shared.NextDouble);
    }

    /// <summary>Gets the retry options: the schedule, the retry count, the status set and the attempt header.</summary>
    public RetryOptions Options { get; }

    /// <summary>Gets the total-time budget of one dispatched call; <see cref="TimeSpan.Zero"/> means none (RETRY-27).</summary>
    public TimeSpan TotalTimeout { get; }

    internal TimeProvider Clock { get; }

    internal RetryEngine Engine { get; }
}
