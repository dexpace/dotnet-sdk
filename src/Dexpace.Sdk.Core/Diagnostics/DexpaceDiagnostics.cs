// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// Central home for the SDK's OpenTelemetry instrumentation objects.
/// </summary>
/// <remarks>
/// Consumers wire up collection by subscribing to <see cref="ActivitySource"/> (tracing) and
/// <see cref="Meter"/> (metrics) via their chosen OTel SDK — no SDK-internal configuration needed.
/// When no listener is active, <c>ActivitySource.StartActivity</c> returns
/// <see langword="null"/> and the hot path allocates nothing for tracing.
/// </remarks>
public static class DexpaceDiagnostics
{
    private static readonly string s_version = BuildVersion();

    /// <summary>
    /// The <see cref="System.Diagnostics.ActivitySource"/> used by the SDK for distributed tracing.
    /// Name: <c>"Dexpace.Sdk"</c>.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("Dexpace.Sdk", s_version);

    /// <summary>
    /// The <see cref="System.Diagnostics.Metrics.Meter"/> used by the SDK for metrics.
    /// Name: <c>"Dexpace.Sdk"</c>.
    /// </summary>
    public static readonly Meter Meter = new("Dexpace.Sdk", s_version);

    private static string BuildVersion()
    {
        var version = typeof(DexpaceDiagnostics).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(DexpaceDiagnostics).Assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        // Strip git commit hash suffix (e.g. "0.0.1-alpha.1+abc123" → "0.0.1-alpha.1").
        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
        return plusIndex >= 0 ? version[..plusIndex] : version;
    }
}
