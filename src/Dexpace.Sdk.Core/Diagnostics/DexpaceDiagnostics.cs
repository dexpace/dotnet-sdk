// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// Central home for the SDK's OpenTelemetry instrumentation objects.
/// </summary>
/// <remarks>
/// <para>
/// Consumers wire up collection by subscribing to <see cref="ActivitySource"/> (tracing) and
/// <see cref="Meter"/> (metrics) via their chosen OTel SDK — no SDK-internal configuration needed.
/// When no listener is active, <c>ActivitySource.StartActivity</c> returns
/// <see langword="null"/> and the hot path allocates nothing for tracing (OBS-25).
/// </para>
/// <para>
/// <b>Spans.</b> One <see cref="ActivityKind.Internal"/> operation span per <c>HttpPipeline</c> call (named for the method,
/// <c>HTTP</c> for an unknown one), ended when the response is returned; one <see cref="ActivityKind.Client"/> attempt span
/// per transmission, a child of the operation span.
/// </para>
/// <para>
/// <b>Events on the operation span.</b> <c>dexpace.attempt.failed</c> (an attempt failed and another follows),
/// <c>dexpace.retry.exhausted</c> (immediately before the <c>exception</c> event of a failing operation whose retry budget
/// was spent), <c>dexpace.redirect.hop</c> and OpenTelemetry's <c>exception</c> event.
/// </para>
/// <para>
/// <b>Instruments.</b> <c>http.client.request.duration</c> (histogram, <c>s</c>) and <c>http.client.active_requests</c>
/// (up-down counter, <c>{request}</c>), with the stable HTTP client attribute sets. Enabling <c>Dexpace.Sdk</c> and
/// <c>System.Net.Http</c> measures each attempt twice under one name; enable one or the other.
/// </para>
/// </remarks>
public static class DexpaceDiagnostics
{
    /// <summary>
    /// The <see cref="System.Diagnostics.ActivitySource"/> used by the SDK for distributed tracing.
    /// Name: <c>"Dexpace.Sdk"</c>.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("Dexpace.Sdk", SdkVersion.Value);

    /// <summary>
    /// The <see cref="System.Diagnostics.Metrics.Meter"/> used by the SDK for metrics.
    /// Name: <c>"Dexpace.Sdk"</c>.
    /// </summary>
    public static readonly Meter Meter = new("Dexpace.Sdk", SdkVersion.Value);
}
