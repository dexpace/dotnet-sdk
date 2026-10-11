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
/// reviewed change. The ids are partitioned by subsystem (P5b-21): 100-109 request and response, 110-119 transport (110-113
/// header drops and proxy warnings, OBS-19, TRANSPORT-13, TRANSPORT-30; phase 8b), 120-129 instrumentation diagnostics,
/// 130-139 disposal, 140-149 retry, 150-159 redirect and 160-169 authentication.
/// </para>
/// </remarks>
public static class DexpaceLogEvents
{
    /// <summary>The event written before a request is sent (<see cref="HttpRequestId"/>, OBS-39).</summary>
    public const string HttpRequest = "http.request";

    /// <summary>The event written when a response arrives (<see cref="HttpResponseId"/>) or an attempt fails (<see cref="HttpFailureId"/>) (OBS-39).</summary>
    public const string HttpResponse = "http.response";

    /// <summary>The warning written when a retry policy's delay override threw or returned a negative delay, so the computed delay was used (<see cref="RetryDelayOverrideFailedId"/>, RETRY-40).</summary>
    public const string RetryDelayOverrideFailed = "dexpace.retry.delay_override_failed";

    /// <summary>The information event written when the redirect policy follows a hop, before the superseded response is disposed (<see cref="RedirectHopId"/>, REDIR-28).</summary>
    public const string RedirectHop = "http.redirect.hop";

    /// <summary>The warning written when a redirect is not followed because its target was already visited on the call (<see cref="RedirectLoopDetectedId"/>, REDIR-28).</summary>
    public const string RedirectLoopDetected = "http.redirect.loop_detected";

    /// <summary>The warning written when an https to http redirect is refused (<see cref="RedirectSchemeDowngradeRejectedId"/>, REDIR-28).</summary>
    public const string RedirectSchemeDowngradeRejected = "http.redirect.scheme_downgrade_rejected";

    /// <summary>The warning written when an https to http redirect is followed because the options permit it (<see cref="RedirectSchemeDowngradePermittedId"/>, REDIR-28).</summary>
    public const string RedirectSchemeDowngradePermitted = "http.redirect.scheme_downgrade_permitted";

    /// <summary>The warning written when an eligible redirect has an unusable <c>Location</c> and is returned unfollowed (<see cref="RedirectLocationMalformedId"/>, REDIR-28).</summary>
    public const string RedirectLocationMalformed = "http.redirect.location_malformed";

    /// <summary>The event a transport writes when it drops one request header and the drop-logging policy decides its level: <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/> for the first drop of a name, <see cref="Microsoft.Extensions.Logging.LogLevel.Debug"/> after (<see cref="TransportHeaderDroppedId"/>, TRANSPORT-12, TRANSPORT-13, OBS-19). The header name is logged, never its value.</summary>
    public const string TransportHeaderDropped = "http.transport.header_dropped";

    /// <summary>The debug event a transport writes when it drops a caller-set framing header it computes itself (<see cref="TransportFramingHeaderDroppedId"/>, TRANSPORT-11).</summary>
    public const string TransportFramingHeaderDropped = "http.transport.framing_header_dropped";

    /// <summary>The debug event a transport writes when it drops a response header whose value carries a control character (<see cref="TransportInboundHeaderDroppedId"/>, TRANSPORT-14).</summary>
    public const string TransportInboundHeaderDropped = "http.transport.inbound_header_dropped";

    /// <summary>The warning a transport writes once at construction when a proxy setting cannot be honoured, naming the feature and never a credential (<see cref="TransportProxyFeatureUnsupportedId"/>, TRANSPORT-30).</summary>
    public const string TransportProxyFeatureUnsupported = "http.transport.proxy_feature_unsupported";

    /// <summary>The diagnostic written when emitting a log event itself failed (<see cref="LogFailedId"/>, OBS-20).</summary>
    public const string LogFailed = "http.instrumentation.log_failed";

    /// <summary>The diagnostic written when capturing a response body preview failed (<see cref="BodyCaptureFailedId"/>, OBS-36).</summary>
    public const string BodyCaptureFailed = "http.instrumentation.body_capture_failed";

    /// <summary>The warning written when disposing a resource failed and the failure was suppressed (<see cref="DisposeSuppressedId"/>).</summary>
    public const string DisposeSuppressed = "dexpace.dispose.suppressed";

    /// <summary>The warning written when a background bearer-token refresh failed and the still-valid token kept being used (<see cref="TokenRefreshFailedId"/>, AUTH-37).</summary>
    public const string TokenRefreshFailed = "dexpace.auth.token_refresh_failed";

    /// <summary>The numeric id of <see cref="HttpRequest"/>.</summary>
    public const int HttpRequestId = 100;

    /// <summary>The numeric id of <see cref="HttpResponse"/> for a response that arrived.</summary>
    public const int HttpResponseId = 101;

    /// <summary>The numeric id of <see cref="HttpResponse"/> for an attempt that failed.</summary>
    public const int HttpFailureId = 102;

    /// <summary>The numeric id of <see cref="TransportHeaderDropped"/>; ids 110 to 119 are the transport block.</summary>
    public const int TransportHeaderDroppedId = 110;

    /// <summary>The numeric id of <see cref="TransportFramingHeaderDropped"/>.</summary>
    public const int TransportFramingHeaderDroppedId = 111;

    /// <summary>The numeric id of <see cref="TransportInboundHeaderDropped"/>.</summary>
    public const int TransportInboundHeaderDroppedId = 112;

    /// <summary>The numeric id of <see cref="TransportProxyFeatureUnsupported"/>; ids 114 to 119 stay reserved.</summary>
    public const int TransportProxyFeatureUnsupportedId = 113;

    /// <summary>The numeric id of <see cref="LogFailed"/>.</summary>
    public const int LogFailedId = 120;

    /// <summary>The numeric id of <see cref="BodyCaptureFailed"/>.</summary>
    public const int BodyCaptureFailedId = 121;

    /// <summary>The numeric id of <see cref="DisposeSuppressed"/>.</summary>
    public const int DisposeSuppressedId = 130;

    /// <summary>The numeric id of <see cref="RetryDelayOverrideFailed"/>; ids 141 to 149 stay reserved for retry.</summary>
    public const int RetryDelayOverrideFailedId = 140;

    /// <summary>The numeric id of <see cref="RedirectHop"/>; ids 150 to 159 are the redirect block.</summary>
    public const int RedirectHopId = 150;

    /// <summary>The numeric id of <see cref="RedirectLoopDetected"/>.</summary>
    public const int RedirectLoopDetectedId = 151;

    /// <summary>The numeric id of <see cref="RedirectSchemeDowngradeRejected"/>.</summary>
    public const int RedirectSchemeDowngradeRejectedId = 152;

    /// <summary>The numeric id of <see cref="RedirectSchemeDowngradePermitted"/>.</summary>
    public const int RedirectSchemeDowngradePermittedId = 153;

    /// <summary>The numeric id of <see cref="RedirectLocationMalformed"/>.</summary>
    public const int RedirectLocationMalformedId = 154;

    /// <summary>The numeric id of <see cref="TokenRefreshFailed"/>; it sits in the auth range reserved from 160 (OBS-39).</summary>
    public const int TokenRefreshFailedId = 160;
}
