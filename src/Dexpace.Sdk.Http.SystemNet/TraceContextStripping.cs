// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// The rule that lets the runtime own <c>traceparent</c> on the wire (design §8.1, phase 5c, P5c-11).
/// </summary>
/// <remarks>
/// <para>
/// <c>InstrumentationPolicy</c> stamps the attempt span's <c>traceparent</c> (and <c>tracestate</c>) on the request so a
/// transport that does not propagate still carries the trace. <c>System.Net.Http</c> does not overwrite a header already on
/// the request, so for this transport the stamp would hide the runtime's own child span. The adapter therefore drops a
/// <c>traceparent</c> that equals <see cref="Activity.Current"/>'s id, which proves it is the SDK's stamp for this attempt
/// and not a caller's header, and the <c>tracestate</c> that came with it, when the runtime will write its own for a span
/// somebody records. The runtime then writes its child span's id, so the server's span is parented to a span the
/// application can see.
/// </para>
/// <para>
/// "The runtime will write its own" is three facts. A listener subscribes to the <c>System.Net.Http</c> source: without
/// one the runtime's child span is not recorded (on .NET 10 it still takes a fresh span id for the header), so stripping
/// would point the server at a span no backend holds; the SDK's stamp, which names the recorded attempt span, stays.
/// The runtime's global switch (<c>System.Net.Http.EnableActivityPropagation</c>, else the environment variable
/// <c>DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION</c>, default on) is read once. The process-wide
/// <see cref="DistributedContextPropagator.Current"/> must list <c>traceparent</c> among its fields (a no-output
/// propagator does not). A borrowed handler that overrides <c>ActivityHeadersPropagator</c> cannot be seen from here; that
/// is the documented residual.
/// </para>
/// </remarks>
internal static class TraceContextStripping
{
    private const string TraceparentName = "traceparent";
    private const string TracestateName = "tracestate";
    private const string RuntimeSwitchName = "System.Net.Http.EnableActivityPropagation";
    private const string RuntimeEnvironmentName = "DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION";

    // A source with the runtime's own name, never used to start an activity: creating it makes every listener decide whether
    // it wants "System.Net.Http", so HasListeners() answers whether the runtime's child span is recorded, without reflection.
    private static readonly ActivitySource s_runtimeSource = new("System.Net.Http");

    /// <summary>The runtime's global activity-propagation switch, read once.</summary>
    internal static bool RuntimeSwitch { get; } = ReadRuntimeSwitch();

    /// <summary>Whether <paramref name="name"/> is <c>traceparent</c> (case-insensitive).</summary>
    /// <param name="name">The header name.</param>
    /// <returns><see langword="true"/> for <c>traceparent</c>.</returns>
    internal static bool IsTraceparent(string name) => string.Equals(name, TraceparentName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="name"/> is <c>tracestate</c> (case-insensitive).</summary>
    /// <param name="name">The header name.</param>
    /// <returns><see langword="true"/> for <c>tracestate</c>.</returns>
    internal static bool IsTracestate(string name) => string.Equals(name, TracestateName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the runtime will write the <c>traceparent</c> of a recorded child span for a request sent under an ambient
    /// activity.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a <c>System.Net.Http</c> listener exists, the switch is on and the global propagator
    /// injects <c>traceparent</c>.
    /// </returns>
    internal static bool RuntimeInjects()
    {
        if (!RuntimeSwitch || !s_runtimeSource.HasListeners())
        {
            return false;
        }

        foreach (var field in DistributedContextPropagator.Current.Fields)
        {
            if (IsTraceparent(field))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a carried <c>traceparent</c> is the SDK's stamp for the current attempt and should be dropped.</summary>
    /// <param name="value">The request's <c>traceparent</c> value.</param>
    /// <param name="currentId">The current activity's id, or <see langword="null"/> when there is none.</param>
    /// <param name="runtimePropagates">Whether the runtime will inject its own.</param>
    /// <returns><see langword="true"/> when the value equals the current id (ordinal) and the runtime injects.</returns>
    internal static bool ShouldStripTraceparent(string value, string? currentId, bool runtimePropagates) =>
        runtimePropagates && currentId is not null && string.Equals(value, currentId, StringComparison.Ordinal);

    /// <summary>Whether a carried <c>tracestate</c> came with a stripped <c>traceparent</c> and should be dropped too.</summary>
    /// <param name="value">The request's <c>tracestate</c> value.</param>
    /// <param name="currentTraceState">The current activity's trace state, or <see langword="null"/>.</param>
    /// <param name="traceparentStripped">Whether the <c>traceparent</c> was stripped.</param>
    /// <returns><see langword="true"/> when the value equals the current trace state (ordinal).</returns>
    internal static bool ShouldStripTracestate(string value, string? currentTraceState, bool traceparentStripped) =>
        traceparentStripped && currentTraceState is not null && string.Equals(value, currentTraceState, StringComparison.Ordinal);

    /// <summary>Resolves the runtime switch: the <c>AppContext</c> switch, else the environment value, else on.</summary>
    /// <param name="appContextSwitch">The <c>AppContext</c> switch, or <see langword="null"/> when unset.</param>
    /// <param name="environmentValue">The environment variable's value, or <see langword="null"/> when unset.</param>
    /// <returns>Whether the runtime propagates activity headers.</returns>
    internal static bool ResolvePropagation(bool? appContextSwitch, string? environmentValue)
    {
        if (appContextSwitch is { } configured)
        {
            return configured;
        }

        if (environmentValue is not null)
        {
            if (environmentValue is "0" || environmentValue.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ReadRuntimeSwitch()
    {
        bool? configured = AppContext.TryGetSwitch(RuntimeSwitchName, out var enabled) ? enabled : null;

        // The runtime reads this variable for its own switch; it is runtime configuration, not SDK configuration, so the
        // CFG-28 ban on implicit environment reads does not apply (phase 5c, P5c-11).
#pragma warning disable RS0030
        var environment = Environment.GetEnvironmentVariable(RuntimeEnvironmentName);
#pragma warning restore RS0030
        return ResolvePropagation(configured, environment);
    }
}
