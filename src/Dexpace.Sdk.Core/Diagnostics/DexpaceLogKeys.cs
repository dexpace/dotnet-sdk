// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The structured-state keys of the SDK's log events (OBS-3, OBS-39), named after the OpenTelemetry HTTP semantic
/// conventions where one exists. A <see langword="null"/> value is carried as <see langword="null"/> in the state.
/// </summary>
/// <remarks>
/// Every member is a <see langword="const"/>, so <c>PublicAPI.Unshipped.txt</c> records each value. No key is named
/// <c>event</c>: the categorisation tag is <see cref="Microsoft.Extensions.Logging.EventId.Name"/> (OBS-4).
/// </remarks>
public static class DexpaceLogKeys
{
    /// <summary>The HTTP method of the request (OBS-39).</summary>
    public const string HttpRequestMethod = "http.request.method";

    /// <summary>The request URL, always redacted (OBS-11 to OBS-15, OBS-39).</summary>
    public const string UrlFull = "url.full";

    /// <summary>The zero-based attempt number of the request (OBS-39).</summary>
    public const string HttpRequestResendCount = "http.request.resend_count";

    /// <summary>The numeric response status code (OBS-39).</summary>
    public const string HttpResponseStatusCode = "http.response.status_code";

    /// <summary>The attempt's duration in milliseconds, a double (OBS-39).</summary>
    public const string HttpResponseDurationMs = "http.response.duration_ms";

    /// <summary>The full type name of the exception that failed the attempt (OBS-39).</summary>
    public const string ErrorType = "error.type";

    /// <summary>The declared request body length in bytes; absent when unknown (OBS-36, OBS-39).</summary>
    public const string HttpRequestBodySize = "http.request.body.size";

    /// <summary>The declared response body length in bytes; absent when unknown (OBS-36, OBS-39).</summary>
    public const string HttpResponseBodySize = "http.response.body.size";

    /// <summary>The rendered request body preview, at body level only (OBS-36, OBS-38).</summary>
    public const string HttpRequestBodyPreview = "http.request.body.preview";

    /// <summary>The rendered response body preview, at body level only (OBS-36, OBS-38).</summary>
    public const string HttpResponseBodyPreview = "http.response.body.preview";

    /// <summary>The number of request body bytes the preview was rendered from (OBS-36).</summary>
    public const string HttpRequestBodyPreviewSize = "http.request.body.preview.size";

    /// <summary>The number of response body bytes the preview was rendered from (OBS-36).</summary>
    public const string HttpResponseBodyPreviewSize = "http.response.body.preview.size";

    /// <summary>The prefix of a request header key; the lower-cased header name follows (OBS-18).</summary>
    public const string HttpRequestHeaderPrefix = "http.request.header.";

    /// <summary>The prefix of a response header key; the lower-cased header name follows (OBS-18).</summary>
    public const string HttpResponseHeaderPrefix = "http.response.header.";

    /// <summary>The value logged for a header that is not on the allow-list (OBS-18).</summary>
    public const string RedactedHeaderValue = "REDACTED";

    /// <summary>The event name whose emission failed, on the log-failed diagnostic (OBS-20).</summary>
    public const string FailedEvent = "dexpace.instrumentation.failed_event";
}
