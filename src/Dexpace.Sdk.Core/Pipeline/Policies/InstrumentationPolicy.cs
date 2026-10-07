// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A per-attempt diagnostics policy that records distributed tracing spans, metrics, and
/// structured log events for each HTTP request attempt.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tracing.</b> A client-kind <see cref="Activity"/> is started from
/// <see cref="DexpaceDiagnostics.ActivitySource"/> for each attempt. The activity name is the
/// HTTP method (low cardinality). OTel HTTP semantic-convention tags are attached:
/// <c>http.request.method</c>, <c>url.full</c> (redacted), <c>url.scheme</c>,
/// <c>server.address</c>, <c>server.port</c>, <c>http.response.status_code</c>, and
/// <c>http.request.resend_count</c>. On exception, <c>error.type</c> is set and the activity
/// status is <see cref="ActivityStatusCode.Error"/>. When no listener is registered,
/// <c>ActivitySource.StartActivity</c> returns <see langword="null"/> and the hot path
/// allocates nothing for tracing.
/// </para>
/// <para>
/// <b>W3C trace-context propagation.</b> When the started <see cref="Activity"/> is non-null
/// and its <see cref="Activity.IdFormat"/> is <see cref="ActivityIdFormat.W3C"/>, the policy
/// stamps <c>traceparent</c> (and, when non-empty, <c>tracestate</c>) onto the request headers
/// before forwarding the call. This ensures trace context propagates over any transport
/// without relying on transport-level auto-injection.
/// </para>
/// <para>
/// <b>Metrics.</b> Two instruments are recorded per attempt:
/// <list type="bullet">
///   <item>
///     <c>http.client.request.duration</c> — <see cref="Histogram{T}"/> in seconds, tagged
///     with <c>http.request.method</c> and (on completion) <c>http.response.status_code</c> or
///     <c>error.type</c>.
///   </item>
///   <item>
///     <c>http.client.active_requests</c> — <see cref="UpDownCounter{T}"/> incremented before
///     the send and decremented after (in a <c>finally</c> block), tagged with
///     <c>http.request.method</c>.
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Logging.</b> Structured <see cref="ILogger"/> events are emitted at
/// <see cref="LogLevel.Debug"/> with the redacted URL. Secrets are never logged.
/// </para>
/// </remarks>
public sealed partial class InstrumentationPolicy : HttpPipelinePolicy
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

    private static readonly UrlRedactor s_redactor = new();

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new <see cref="InstrumentationPolicy"/>.
    /// </summary>
    /// <param name="logger">
    /// The logger to write request/response events to. Defaults to
    /// <see cref="NullLogger.Instance"/> when <see langword="null"/>.
    /// </param>
    public InstrumentationPolicy(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.Diagnostics;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(InstrumentationPolicy));

    // MA0051 waiver: interleaved log, span and metric handling. Roadmap phase 5 (5b's LoggerMessage delegates and
    // emission guard, 5c's operation-level Activity and metric rework) restructures this method; splitting it now would
    // be rewritten there.
#pragma warning disable MA0051
    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var method = request.Method.Name;
        var redactedUrl = s_redactor.Redact(request.Url);

        // Start a client-kind Activity only when there are listeners; null if none.
        using var activity = DexpaceDiagnostics.ActivitySource.StartActivity(
            method,
            ActivityKind.Client);

        // The span and its trace-context header travel downstream on a copy of the context and the request passed to
        // continuation; nothing is restored afterwards because nothing upstream could observe the write (PIPE-16).
        var outgoing = request;
        var downstream = context;
        if (activity is not null)
        {
            activity.SetTag("http.request.method", method);
            activity.SetTag("url.full", redactedUrl);
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

                outgoing = request.WithHeaders(headers);
            }

            downstream = context.WithActivity(activity);
        }

        LogSendingRequest(_logger, method, redactedUrl);

        var sw = Stopwatch.StartNew();
        var methodTag = new TagList { { "http.request.method", method } };
        s_activeRequests.Add(1, methodTag);

        try
        {
            var response = async
                ? await continuation.RunAsync(outgoing, downstream).ConfigureAwait(false)
                : continuation.Run(outgoing, downstream);

            var statusCode = response.Status.Code;
            activity?.SetTag("http.response.status_code", statusCode);
            LogReceivedResponse(_logger, method, statusCode, redactedUrl);

            var durationTags = new TagList
            {
                { "http.request.method", method },
                { "http.response.status_code", statusCode },
            };
            s_requestDuration.Record(sw.Elapsed.TotalSeconds, durationTags);
            return response;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // RETRY-25: a fatal exception is not logged or recorded here; no SDK frame catches it (design §10 entry 12).
            if (activity is not null)
            {
                activity.SetTag("error.type", ex.GetType().FullName);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }

            LogRequestFailed(_logger, ex, method, redactedUrl, ex.GetType().Name);

            var durationTags = new TagList
            {
                { "http.request.method", method },
                { "error.type", ex.GetType().FullName },
            };
            s_requestDuration.Record(sw.Elapsed.TotalSeconds, durationTags);

            throw;
        }
        finally
        {
            s_activeRequests.Add(-1, methodTag);
        }
    }
#pragma warning restore MA0051

    // ─── Source-generated zero-alloc logger messages ──────────────────────────

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
