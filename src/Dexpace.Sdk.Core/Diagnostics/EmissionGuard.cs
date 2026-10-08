// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The emission guard of 5b (OBS-20), shared by the HTTP log emitter and the redirect events: what is reportable, and the one
/// <c>http.instrumentation.log_failed</c> diagnostic a failing emission is reduced to.
/// </summary>
internal static class EmissionGuard
{
    // The placeholder names are the published OpenTelemetry-style state keys (OBS-39, P5b-3), which CA1727's PascalCase rule
    // and the source generator cannot express; the fixed-key Define form is the verified way (design §8.1).
#pragma warning disable CA1727
    private static readonly Action<ILogger, string, string, Exception?> s_logFailed = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.LogFailedId, DexpaceLogEvents.LogFailed),
        "Emitting the {dexpace.instrumentation.failed_event} log event failed with {error.type}; the request was not affected.");
#pragma warning restore CA1727

    // OBS-20 / P5b-11: a fatal exception, and a cancellation of the call's own token, are not the logger's to swallow.
    internal static bool Reportable(Exception exception, CancellationToken token) =>
        !ExceptionFacts.IsFatal(exception) && !(exception is OperationCanceledException && token.IsCancellationRequested);

    // OBS-20: one diagnostic is attempted; a failure while reporting is swallowed.
    internal static void ReportFailure(ILogger logger, string eventName, Exception failure)
    {
#pragma warning disable CA1031 // OBS-20: a secondary failure while reporting a logging failure is swallowed, never propagated.
        try
        {
            s_logFailed(logger, eventName, failure.GetType().FullName ?? failure.GetType().Name, failure);
        }
        catch (Exception inner) when (!ExceptionFacts.IsFatal(inner))
        {
            // Swallowed by design.
        }
#pragma warning restore CA1031
    }
}
