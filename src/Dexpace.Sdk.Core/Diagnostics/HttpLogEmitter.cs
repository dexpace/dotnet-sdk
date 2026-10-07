// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The log half of <c>InstrumentationPolicy</c>: every log emission of the attempt (design 5b position A). The redacted URL
/// is read from the scope only when the event is enabled, so a disabled event computes nothing (OBS-1).
/// </summary>
internal static partial class HttpLogEmitter
{
    /// <summary>Logs that a request is about to be sent.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scope">The attempt's scope.</param>
    internal static void OnRequest(ILogger logger, ref AttemptScope scope)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            LogSendingRequest(logger, scope.MethodName, scope.RedactedUrl);
        }
    }

    /// <summary>Logs that a response arrived.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scope">The attempt's scope.</param>
    /// <param name="statusCode">The response status code.</param>
    internal static void OnResponse(ILogger logger, ref AttemptScope scope, int statusCode)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            LogReceivedResponse(logger, scope.MethodName, statusCode, scope.RedactedUrl);
        }
    }

    /// <summary>Logs that the attempt failed.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scope">The attempt's scope.</param>
    /// <param name="exception">The failure.</param>
    internal static void OnFailure(ILogger logger, ref AttemptScope scope, Exception exception)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            LogRequestFailed(logger, exception, scope.MethodName, scope.RedactedUrl, exception.GetType().Name);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Sending {Method} request to {Url}")]
    private static partial void LogSendingRequest(ILogger logger, string method, string url);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Received {Method} response {StatusCode} from {Url}")]
    private static partial void LogReceivedResponse(ILogger logger, string method, int? statusCode, string url);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Request {Method} to {Url} failed with {ErrorType}")]
    private static partial void LogRequestFailed(ILogger logger, Exception ex, string method, string url, string errorType);
}
