// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The span and metric half of <c>InstrumentationPolicy</c>: the per-attempt <see cref="Activity"/>, its tags, the
/// W3C trace-context headers and the two instruments (design 5b position A). Calls into the activity and the meter are
/// never wrapped by the logging guard (OBS-20, design §11 item 37).
/// </summary>
internal struct AttemptTelemetry
{
    // Instruments are created once from the shared Meter.
    private static readonly Histogram<double> s_requestDuration =
        DexpaceDiagnostics.Meter.CreateHistogram<double>(
            "http.client.request.duration",
            unit: "s",
            description: "Duration of HTTP client requests.");

    private static readonly UpDownCounter<long> s_activeRequests =
        DexpaceDiagnostics.Meter.CreateUpDownCounter<long>(
            "http.client.active_requests",
            unit: "{request}",
            description: "Number of HTTP requests currently in flight.");

    private Activity? _activity;
    private string _method;

    /// <summary>The request to send: the original, or a copy carrying <c>traceparent</c> and <c>tracestate</c>.</summary>
    internal Request Outgoing { get; private set; }

    /// <summary>The context to hand downstream: the original, or a copy carrying the attempt activity.</summary>
    internal PipelineContext Downstream { get; private set; }

    /// <summary>Starts the attempt's activity (when a listener wants one), tags it and counts the request in flight.</summary>
    /// <param name="scope">The attempt's scope.</param>
    /// <param name="request">The request the attempt was entered with.</param>
    /// <param name="context">The context the attempt was entered with.</param>
    /// <returns>The begun telemetry; <see cref="End"/> must run exactly once.</returns>
    internal static AttemptTelemetry Begin(ref AttemptScope scope, Request request, PipelineContext context)
    {
        var telemetry = new AttemptTelemetry
        {
            _method = scope.MethodName,
            Outgoing = request,
            Downstream = context,

            // Start a client-kind Activity only when there are listeners; null if none.
            _activity = DexpaceDiagnostics.ActivitySource.StartActivity(scope.MethodName, ActivityKind.Client),
        };

        if (telemetry._activity is { } activity)
        {
            telemetry.Tag(activity, ref scope, request, context);
        }

        if (s_activeRequests.Enabled)
        {
            s_activeRequests.Add(1, new TagList { { "http.request.method", scope.MethodName } });
        }

        return telemetry;
    }

    /// <summary>Records a response: the status tag and the duration (stopping the scope's clock).</summary>
    /// <param name="response">The response.</param>
    /// <param name="scope">The attempt's scope.</param>
    internal readonly void Succeeded(Response response, ref AttemptScope scope)
    {
        scope.Stop();
        var statusCode = response.Status.Code;
        _activity?.SetTag("http.response.status_code", statusCode);

        if (s_requestDuration.Enabled)
        {
            var durationTags = new TagList
            {
                { "http.request.method", _method },
                { "http.response.status_code", statusCode },
            };
            s_requestDuration.Record(scope.Elapsed.TotalSeconds, durationTags);
        }
    }

    /// <summary>Records a failure: the error tag, the error status and the duration (stopping the scope's clock).</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="scope">The attempt's scope.</param>
    internal readonly void Failed(Exception exception, ref AttemptScope scope)
    {
        scope.Stop();
        if (_activity is not null)
        {
            _activity.SetTag("error.type", exception.GetType().FullName);
            _activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        if (s_requestDuration.Enabled)
        {
            var durationTags = new TagList
            {
                { "http.request.method", _method },
                { "error.type", exception.GetType().FullName },
            };
            s_requestDuration.Record(scope.Elapsed.TotalSeconds, durationTags);
        }
    }

    /// <summary>Counts the request out of flight and ends the activity.</summary>
    internal readonly void End()
    {
        if (s_activeRequests.Enabled)
        {
            s_activeRequests.Add(-1, new TagList { { "http.request.method", _method } });
        }

        _activity?.Dispose();
    }

    private void Tag(Activity activity, ref AttemptScope scope, Request request, PipelineContext context)
    {
        activity.SetTag("http.request.method", scope.MethodName);
        activity.SetTag("url.full", scope.RedactedUrl);
        activity.SetTag("url.scheme", request.Url.Scheme);
        activity.SetTag("server.address", request.Url.Host);
        activity.SetTag("server.port", request.Url.IsDefaultPort ? -1 : request.Url.Port);
        activity.SetTag("http.request.resend_count", context.AttemptNumber);

        // Inject W3C trace context onto the request so any transport carries the span.
        if (activity.IdFormat == ActivityIdFormat.W3C && activity.Id is not null)
        {
            var headers = request.Headers.Set("traceparent", activity.Id);
            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                headers = headers.Set("tracestate", activity.TraceStateString);
            }

            Outgoing = request.WithHeaders(headers);
        }

        // The span and its trace-context header travel downstream on a copy of the context and the request passed to
        // continuation; nothing is restored afterwards because nothing upstream could observe the write (PIPE-16).
        Downstream = context.WithActivity(activity);
    }
}
