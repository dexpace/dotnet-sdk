// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The stable names and numeric ids of the events the SDK writes to its <see cref="Microsoft.Extensions.Logging.ILogger"/>
/// (OBS-39). A log provider reports the name as <see cref="Microsoft.Extensions.Logging.EventId.Name"/>, which is the
/// event's categorisation tag (OBS-4); consumers filter on these constants rather than on string literals.
/// </summary>
/// <remarks>
/// <para>
/// Every member is a <see langword="const"/>, so <c>PublicAPI.Unshipped.txt</c> records each value and renaming one is a
/// reviewed change. The ids are partitioned by subsystem (P5b-21): 100-109 request and response, 110-119 transport header
/// drops (reserved for the transport conformance phase, OBS-19), 120-129 instrumentation diagnostics, 130-139 disposal,
/// 140-149 retry, 150-159 redirect and 160-169 authentication (reserved).
/// </para>
/// </remarks>
public static class DexpaceLogEvents
{
    /// <summary>The event written before a request is sent (<see cref="HttpRequestId"/>, OBS-39).</summary>
    public const string HttpRequest = "http.request";

    /// <summary>The event written when a response arrives (<see cref="HttpResponseId"/>) or an attempt fails (<see cref="HttpFailureId"/>) (OBS-39).</summary>
    public const string HttpResponse = "http.response";

    /// <summary>The diagnostic written when emitting a log event itself failed (<see cref="LogFailedId"/>, OBS-20).</summary>
    public const string LogFailed = "http.instrumentation.log_failed";

    /// <summary>The diagnostic written when capturing a response body preview failed (<see cref="BodyCaptureFailedId"/>, OBS-36).</summary>
    public const string BodyCaptureFailed = "http.instrumentation.body_capture_failed";

    /// <summary>The warning written when disposing a resource failed and the failure was suppressed (<see cref="DisposeSuppressedId"/>).</summary>
    public const string DisposeSuppressed = "dexpace.dispose.suppressed";

    /// <summary>The numeric id of <see cref="HttpRequest"/>.</summary>
    public const int HttpRequestId = 100;

    /// <summary>The numeric id of <see cref="HttpResponse"/> for a response that arrived.</summary>
    public const int HttpResponseId = 101;

    /// <summary>The numeric id of <see cref="HttpResponse"/> for an attempt that failed.</summary>
    public const int HttpFailureId = 102;

    /// <summary>The numeric id of <see cref="LogFailed"/>.</summary>
    public const int LogFailedId = 120;

    /// <summary>The numeric id of <see cref="BodyCaptureFailed"/>.</summary>
    public const int BodyCaptureFailedId = 121;

    /// <summary>The numeric id of <see cref="DisposeSuppressed"/>.</summary>
    public const int DisposeSuppressedId = 130;
}
