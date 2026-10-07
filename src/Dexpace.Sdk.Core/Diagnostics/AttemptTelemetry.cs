// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The span and metric half of <c>InstrumentationPolicy</c>: the per-attempt <see cref="Activity"/>, its tags, the
/// W3C trace-context headers and the two instruments (OBS-21, OBS-25, OBS-31 to OBS-33; design 5b position A, design 5c
/// position F). Calls into the activity and the meter are never wrapped by the logging guard (OBS-20, design §11 item 37).
/// </summary>
/// <remarks>
/// The attempt span is started through the call's <see cref="Execution.InstrumentationContext"/>, so its parent is the
/// operation span and not <see cref="Activity.Current"/> (P4a-16); it exists only under an operation span, because the
/// untraced bundle starts nothing. A struct, so the untraced synchronous path allocates nothing. Every write to the span
/// is guarded by <see cref="Activity.IsAllDataRequested"/>.
/// </remarks>
internal struct AttemptTelemetry
{
    private Activity? _activity;
    private Request _request;
    private bool _counted;
    private bool _ended;

    /// <summary>The request to send: the original, or a copy carrying <c>traceparent</c> and <c>tracestate</c>.</summary>
    internal Request Outgoing { get; private set; }

    /// <summary>The context to hand downstream: the original, or a copy carrying the attempt activity.</summary>
    internal PipelineContext Downstream { get; private set; }

    /// <summary>The attempt span, or <see langword="null"/> when the call is untraced.</summary>
    internal readonly Activity? Activity => _activity;

    /// <summary>Starts the attempt's activity (when the call is traced), tags it and counts the request in flight.</summary>
    /// <param name="scope">The attempt's scope.</param>
    /// <param name="request">The request the attempt was entered with.</param>
    /// <param name="context">The context the attempt was entered with.</param>
    /// <returns>The begun telemetry; <see cref="End"/> must run.</returns>
    internal static AttemptTelemetry Begin(ref AttemptScope scope, Request request, PipelineContext context)
    {
        var ordinal = context.State.NextTransmission();
        scope.ResendCount = ordinal;
        var telemetry = new AttemptTelemetry
        {
            _request = request,
            Outgoing = request,
            Downstream = context,
            _activity = context.Instrumentation.StartActivity(HttpSemanticConventions.SpanName(request.Method), ActivityKind.Client),
        };

        if (telemetry._activity is { } activity)
        {
            Tag(activity, ref scope, request, ordinal);
            telemetry.Stamp(activity, request);
            telemetry.Downstream = context.WithActivity(activity);
        }

        telemetry._counted = HttpClientMetrics.RequestStarted(request);
        return telemetry;
    }

    /// <summary>Records a response: the status, protocol and error tags and the duration (stopping the scope's clock).</summary>
    /// <param name="response">The response.</param>
    /// <param name="scope">The attempt's scope.</param>
    internal readonly void Succeeded(Response response, ref AttemptScope scope)
    {
        scope.Stop();
        if (_activity is { IsAllDataRequested: true } activity)
        {
            var code = response.Status.Code;
            activity.SetTag(HttpSemanticConventions.ResponseStatusCode, HttpSemanticConventions.BoxedStatusCode(code));
            if (HttpSemanticConventions.ProtocolVersion(response.Protocol) is { } version)
            {
                activity.SetTag(HttpSemanticConventions.NetworkProtocolVersion, version);
            }

            if (code >= 400)
            {
                activity.SetTag(HttpSemanticConventions.ErrorTypeKey, HttpSemanticConventions.ErrorType(code));
                activity.SetStatus(ActivityStatusCode.Error);
            }
        }

        HttpClientMetrics.RecordDuration(_request, scope.Elapsed.TotalSeconds, response, failure: null);
    }

    /// <summary>Records a failure: the error tag, the error status and the duration (stopping the scope's clock).</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="scope">The attempt's scope.</param>
    internal readonly void Failed(Exception exception, ref AttemptScope scope)
    {
        scope.Stop();
        if (_activity is { IsAllDataRequested: true } activity)
        {
            activity.SetTag(HttpSemanticConventions.ErrorTypeKey, HttpSemanticConventions.ErrorType(exception));
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        HttpClientMetrics.RecordDuration(_request, scope.Elapsed.TotalSeconds, response: null, exception);
    }

    /// <summary>
    /// Counts the request out of flight and ends the activity, once: a second call does nothing. A listener callback
    /// that throws propagates, after the span has been stopped.
    /// </summary>
    internal void End()
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        try
        {
            HttpClientMetrics.RequestEnded(_request, _counted);
        }
        finally
        {
            _activity?.Dispose();
        }
    }

    private static void Tag(Activity activity, ref AttemptScope scope, Request request, int ordinal)
    {
        if (!activity.IsAllDataRequested)
        {
            return;
        }

        var url = request.Url;
        activity.SetTag(HttpSemanticConventions.RequestMethod, HttpSemanticConventions.MethodValue(request.Method));
        if (HttpSemanticConventions.IsOther(request.Method))
        {
            activity.SetTag(HttpSemanticConventions.RequestMethodOriginal, request.Method.Name);
        }

        activity.SetTag(HttpSemanticConventions.UrlFull, scope.RedactedUrl);
        activity.SetTag(HttpSemanticConventions.UrlScheme, url.Scheme);
        activity.SetTag(HttpSemanticConventions.ServerAddress, url.Host);
        activity.SetTag(HttpSemanticConventions.ServerPort, url.Port);

        // The convention: SHOULD NOT be set on the first transmission.
        if (ordinal > 0)
        {
            activity.SetTag(HttpSemanticConventions.RequestResendCount, ordinal);
        }
    }

    // Inject W3C trace context onto the request so any transport carries the span (P5c-12); the reference adapter strips
    // it when the runtime injects its own child span.
    private void Stamp(Activity activity, Request request)
    {
        if (activity.IdFormat == ActivityIdFormat.W3C && activity.Id is not null)
        {
            var headers = request.Headers.Set("traceparent", activity.Id);
            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                headers = headers.Set("tracestate", activity.TraceStateString);
            }

            Outgoing = request.WithHeaders(headers);
        }
    }
}
