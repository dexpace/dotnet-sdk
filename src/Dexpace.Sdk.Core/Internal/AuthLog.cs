// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// The authentication layer's one log event, <c>dexpace.auth.token_refresh_failed</c> (id 160; AUTH-37, P6c-37).
/// </summary>
/// <remarks>
/// Written under the emission guard of 5b (OBS-20): a logger that is disabled, or that throws, never affects the call.
/// </remarks>
internal static class AuthLog
{
    // The placeholder name is the published OpenTelemetry-style state key (OBS-39, P5b-3), which CA1727's PascalCase rule
    // cannot express; the fixed-key Define form is the verified way (design §8.1).
#pragma warning disable CA1727
    private static readonly Action<ILogger, string, Exception?> s_tokenRefreshFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.TokenRefreshFailedId, DexpaceLogEvents.TokenRefreshFailed),
        "Background token refresh failed with {error.type}.");
#pragma warning restore CA1727

    /// <summary>Reports that a background token refresh failed; the still-valid token keeps being used.</summary>
    /// <param name="logger">The logger of the call that launched the refresh.</param>
    /// <param name="failure">What the provider threw.</param>
    internal static void TokenRefreshFailed(ILogger logger, Exception failure)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                s_tokenRefreshFailed(logger, HttpSemanticConventions.ErrorType(failure), failure);
            }
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // A logger that throws must not change the outcome of a token fetch (OBS-20).
        }
    }
}
