// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Globalization;

namespace Dexpace.Sdk.Core.Execution;

/// <summary>
/// The key of one call's context chain: a trace id, a span id and a process-wide sequence number (CTX-4, CTX-5, CTX-6).
/// </summary>
/// <remarks>
/// <para>
/// A key is minted only through <see cref="Next()"/> or <see cref="Next(in ActivityContext)"/>; there is no public
/// constructor (P4a-2). To pin one key across several contexts, mint it once and pass it to each constructor. The
/// sequence makes the key call-unique even when the trace and span ids are equal or all zero (the untraced sentinel,
/// CTX-15). <c>default(CallKey)</c> has sequence 0, was never minted, and every context constructor rejects it.
/// </para>
/// <para>
/// Equality is field-wise over all three parts. No string is allocated unless <see cref="ToString"/> is called.
/// </para>
/// </remarks>
public readonly record struct CallKey
{
    private static long s_sequence;

    private CallKey(ActivityTraceId traceId, ActivitySpanId spanId, long sequence)
    {
        TraceId = traceId;
        SpanId = spanId;
        Sequence = sequence;
    }

    /// <summary>The trace id the key was minted under (all zero for the untraced sentinel).</summary>
    public ActivityTraceId TraceId { get; }

    /// <summary>The span id the key was minted under (all zero for the untraced sentinel).</summary>
    public ActivitySpanId SpanId { get; }

    /// <summary>The process-wide sequence number: at least 1 for a minted key, 0 only for <c>default(CallKey)</c>.</summary>
    public long Sequence { get; }

    /// <summary>Mints a key with zero trace and span ids (the untraced sentinel).</summary>
    /// <returns>A call-unique key.</returns>
    public static CallKey Next() => new(default, default, Interlocked.Increment(ref s_sequence));

    /// <summary>Mints a key carrying the trace and span ids of <paramref name="context"/>.</summary>
    /// <param name="context">The activity context to take the ids from.</param>
    /// <returns>A call-unique key.</returns>
    public static CallKey Next(in ActivityContext context) =>
        new(context.TraceId, context.SpanId, Interlocked.Increment(ref s_sequence));

    /// <summary>Renders <c>{trace id: 32 hex}:{span id: 16 hex}:{sequence}</c>.</summary>
    /// <returns>The rendering.</returns>
    public override string ToString()
    {
        var trace = TraceId == default ? new string('0', 32) : TraceId.ToHexString();
        var span = SpanId == default ? new string('0', 16) : SpanId.ToHexString();
        return string.Create(CultureInfo.InvariantCulture, $"{trace}:{span}:{Sequence}");
    }
}
