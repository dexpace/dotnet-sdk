// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The retry policy's one log event, <c>dexpace.retry.delay_override_failed</c> (id 140; RETRY-40, P6a-28).
/// </summary>
/// <remarks>
/// Written under the emission guard of 5b (OBS-20): a logger that is disabled, or that throws, never affects the call. A
/// fatal exception propagates.
/// </remarks>
internal static class RetryLog
{
    // The placeholder name is the published OpenTelemetry-style state key (OBS-39, P5b-3), which CA1727's PascalCase rule
    // cannot express; the fixed-key Define form is the verified way (design §8.1).
#pragma warning disable CA1727
    private static readonly Action<ILogger, string, Exception?> s_delayOverrideFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.RetryDelayOverrideFailedId, DexpaceLogEvents.RetryDelayOverrideFailed),
        "The retry delay override failed with {error.type}; the computed delay was used.");
#pragma warning restore CA1727

    /// <summary>Reports that a delay override threw or returned a negative delay.</summary>
    /// <param name="logger">The call's logger.</param>
    /// <param name="failure">What the override threw, or a description of the negative value.</param>
    internal static void DelayOverrideFailed(ILogger logger, Exception failure)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                s_delayOverrideFailed(logger, failure.GetType().FullName ?? failure.GetType().Name, failure);
            }
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // A logger that throws must not change the retry decision (OBS-20).
        }
    }
}
