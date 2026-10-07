// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Diagnostics;

namespace Dexpace.Sdk.Core.Execution;

/// <summary>
/// The correlation bundle every call context carries (CTX-2, CTX-14, CTX-15, CTX-20; design §5.4, §8.1, P4a-3).
/// </summary>
/// <remarks>
/// <para>
/// It composes an <see cref="System.Diagnostics.ActivityContext"/> (trace id, span id, flags, trace state, remoteness),
/// a derived <see cref="IsValid"/>, the trace-id encoding flavour as an <see cref="ActivityIdFormat"/>, the active span
/// (<see langword="null"/> is the no-op span) and a per-operation tracer factory over
/// <see cref="DexpaceDiagnostics.ActivitySource"/>. It is built only through <see cref="FromActivity"/> and
/// <see cref="FromContext"/>, so the identifiers and the active span cannot disagree. Equality is reference identity,
/// which is what CTX-2 asks promotion to carry.
/// </para>
/// <para>
/// <see cref="None"/> is the one shared untraced instance: all-zero ids, no flags, empty trace state, no active span,
/// <see cref="ActivityIdFormat.Unknown"/>, and a factory that returns <see langword="null"/> whatever listeners exist.
/// A legacy hierarchical activity reports <see cref="ActivityIdFormat.Hierarchical"/> and is not valid. No Datadog
/// flavour exists on .NET (design §10, entry 24). Adding a member after phase 4a is a dated design correction, never a
/// silent change; phase 5c populates the bundle and may not redefine it.
/// </para>
/// </remarks>
public sealed class InstrumentationContext
{
    private InstrumentationContext(ActivityContext context, Activity? activeSpan, ActivityIdFormat format, string traceState)
    {
        ActivityContext = context;
        ActiveSpan = activeSpan;
        TraceIdFormat = format;
        TraceState = traceState;
    }

    /// <summary>The shared untraced bundle (CTX-15); the default of every context constructor.</summary>
    public static InstrumentationContext None { get; } =
        new(default, activeSpan: null, ActivityIdFormat.Unknown, string.Empty);

    /// <summary>The underlying activity context.</summary>
    public ActivityContext ActivityContext { get; }

    /// <summary>The trace id.</summary>
    public ActivityTraceId TraceId => ActivityContext.TraceId;

    /// <summary>The span id.</summary>
    public ActivitySpanId SpanId => ActivityContext.SpanId;

    /// <summary>The trace flags.</summary>
    public ActivityTraceFlags TraceFlags => ActivityContext.TraceFlags;

    /// <summary>The W3C trace state; never <see langword="null"/>, empty when absent.</summary>
    public string TraceState { get; }

    /// <summary>The trace-id encoding flavour; <see cref="ActivityIdFormat.Unknown"/> for <see cref="None"/>.</summary>
    public ActivityIdFormat TraceIdFormat { get; }

    /// <summary>Whether both the trace id and the span id are non-zero.</summary>
    public bool IsValid => TraceId != default && SpanId != default;

    /// <summary>Whether the context was propagated from a remote parent.</summary>
    public bool IsRemote => ActivityContext.IsRemote;

    /// <summary>The active span, or <see langword="null"/> for the no-op span.</summary>
    public Activity? ActiveSpan { get; }

    /// <summary>Builds a bundle from <paramref name="activity"/>; <see langword="null"/> returns <see cref="None"/>.</summary>
    /// <param name="activity">The active span, or <see langword="null"/>.</param>
    /// <returns>The bundle.</returns>
    public static InstrumentationContext FromActivity(Activity? activity) =>
        activity is null
            ? None
            : new InstrumentationContext(activity.Context, activity, activity.IdFormat, activity.TraceStateString ?? string.Empty);

    /// <summary>Builds a bundle from a context (for example an extracted remote parent), with no active span.</summary>
    /// <param name="context">The context; <c>default</c> returns <see cref="None"/>.</param>
    /// <returns>The bundle.</returns>
    public static InstrumentationContext FromContext(in ActivityContext context) =>
        context == default
            ? None
            : new InstrumentationContext(context, activeSpan: null, ActivityIdFormat.W3C, context.TraceState ?? string.Empty);

    /// <summary>
    /// Starts a span named <paramref name="operationName"/> as a child of this bundle's context, not of
    /// <see cref="Activity.Current"/> (P4a-16). Thread-safe; returns <see langword="null"/> with no listener and always
    /// for <see cref="None"/>.
    /// </summary>
    /// <param name="operationName">The span name; non-blank.</param>
    /// <param name="kind">The span kind.</param>
    /// <returns>The started activity, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="operationName"/> is null, empty or whitespace.</exception>
    public Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        return ReferenceEquals(this, None)
            ? null
            : DexpaceDiagnostics.ActivitySource.StartActivity(operationName, kind, ActivityContext);
    }
}
