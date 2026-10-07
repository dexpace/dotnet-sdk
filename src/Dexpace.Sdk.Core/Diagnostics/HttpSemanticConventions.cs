// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The attribute and event names of the stable OpenTelemetry HTTP client conventions and of the SDK's own
/// <c>dexpace.*</c> events, with the small pure mappings the spans and instruments share (OBS-21, OBS-32; design 5c
/// positions E and F, P5c-8, P5c-9).
/// </summary>
/// <remarks>
/// Internal on purpose (P5c-16): the names are documented strings. 5b's log keys cite the same constants so that a span
/// tag and a log key cannot drift.
/// </remarks>
internal static class HttpSemanticConventions
{
    internal const string RequestMethod = "http.request.method";
    internal const string RequestMethodOriginal = "http.request.method_original";
    internal const string RequestResendCount = "http.request.resend_count";
    internal const string ResponseStatusCode = "http.response.status_code";
    internal const string ServerAddress = "server.address";
    internal const string ServerPort = "server.port";
    internal const string UrlScheme = "url.scheme";
    internal const string UrlFull = "url.full";
    internal const string NetworkProtocolVersion = "network.protocol.version";
    internal const string ErrorTypeKey = "error.type";

    internal const string ExceptionEvent = "exception";
    internal const string ExceptionType = "exception.type";
    internal const string ExceptionMessage = "exception.message";
    internal const string ExceptionStackTrace = "exception.stacktrace";

    internal const string AttemptFailedEvent = "dexpace.attempt.failed";
    internal const string RetryExhaustedEvent = "dexpace.retry.exhausted";
    internal const string RedirectHopEvent = "dexpace.redirect.hop";
    internal const string RetryDelay = "dexpace.retry.delay";
    internal const string RetryAttempts = "dexpace.retry.attempts";
    internal const string RedirectHop = "dexpace.redirect.hop";
    internal const string RedirectCrossOrigin = "dexpace.redirect.cross_origin";

    // The conventions' known-method set: RFC 9110's methods plus PATCH (RFC 5789). Case-sensitive.
    private static readonly FrozenSet<string> s_knownMethods =
        new[] { "GET", "HEAD", "POST", "PUT", "DELETE", "CONNECT", "OPTIONS", "TRACE", "PATCH" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly string[] s_errorStatuses = BuildErrorStatuses();
    private static readonly object[] s_boxedStatuses = BuildBoxedStatuses();

    /// <summary>The method name for a known method, <c>_OTHER</c> for any other token (P5c-9).</summary>
    /// <param name="method">The method.</param>
    /// <returns>A low-cardinality value for a span or metric dimension.</returns>
    internal static string MethodValue(Method method) => s_knownMethods.Contains(method.Name) ? method.Name : "_OTHER";

    /// <summary>Whether <paramref name="method"/> is reported as <c>_OTHER</c>.</summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for a method outside the conventions' known set.</returns>
    internal static bool IsOther(Method method) => !s_knownMethods.Contains(method.Name);

    /// <summary>The span name: the method for a known method, <c>HTTP</c> otherwise (P5c-9).</summary>
    /// <param name="method">The method.</param>
    /// <returns>The span name.</returns>
    internal static string SpanName(Method method) => s_knownMethods.Contains(method.Name) ? method.Name : "HTTP";

    /// <summary>The <c>error.type</c> for an exception: its full type name.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The full type name, or the simple name for a type without one.</returns>
    internal static string ErrorType(Exception exception) => exception.GetType().FullName ?? exception.GetType().Name;

    /// <summary>The <c>error.type</c> for a status: its decimal string, cached for 400 to 599.</summary>
    /// <param name="status">The status code.</param>
    /// <returns>The status as a string.</returns>
    internal static string ErrorType(int status) =>
        status is >= 400 and <= 599 ? s_errorStatuses[status - 400] : status.ToString(CultureInfo.InvariantCulture);

    /// <summary>A boxed status code, shared for 100 to 599 so that recording it does not allocate.</summary>
    /// <param name="status">The status code.</param>
    /// <returns>The boxed <see cref="int"/>.</returns>
    internal static object BoxedStatusCode(int status) => status is >= 100 and <= 599 ? s_boxedStatuses[status - 100] : status;

    /// <summary>The <c>network.protocol.version</c> of a protocol, or <see langword="null"/> for an undefined value.</summary>
    /// <param name="protocol">The protocol.</param>
    /// <returns><c>1.0</c>, <c>1.1</c>, <c>2</c> or <c>3</c>.</returns>
    internal static string? ProtocolVersion(Protocol protocol) => protocol switch
    {
        Protocol.Http10 => "1.0",
        Protocol.Http11 => "1.1",
        Protocol.Http2 or Protocol.H2PriorKnowledge => "2",
        Protocol.Quic => "3",
        _ => null,
    };

    private static string[] BuildErrorStatuses()
    {
        var table = new string[200];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (400 + i).ToString(CultureInfo.InvariantCulture);
        }

        return table;
    }

    private static object[] BuildBoxedStatuses()
    {
        var table = new object[500];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = 100 + i;
        }

        return table;
    }
}
