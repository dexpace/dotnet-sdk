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
/// <c>http.request.method</c>, <c>url.full</c> (redacted with the call's
/// <see cref="Configuration.HttpLoggingOptions.AllowedQueryParameters"/>, the same value the log events carry; default
/// <c>api-version</c> only, P5b-10), <c>url.scheme</c>,
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
/// <b>Structure.</b> The attempt is split into an <c>AttemptScope</c> (the lazily redacted URL and the entry timestamp),
/// <c>AttemptTelemetry</c> (the span and the instruments) and <c>HttpLogEmitter</c> (the log events); this method only
/// orchestrates them.
/// </para>
/// <para>
/// <b>Logging (OBS-1 to OBS-4, OBS-6, OBS-20, OBS-34, OBS-39).</b> Logging is opt-in per call through
/// <see cref="Configuration.DexpaceClientOptions.Logging"/>: at <see cref="Configuration.HttpLogLevel.None"/> (the default)
/// no <c>http.request</c> or <c>http.response</c> event is emitted, nothing is allocated and no body wrapper is
/// constructed, while the span and the instruments still record. At <see cref="Configuration.HttpLogLevel.Headers"/> the
/// policy writes <c>http.request</c> (id 100) before the continuation and <c>http.response</c> (id 101, or 102 at
/// <see cref="LogLevel.Warning"/> when the attempt threw) after it, at <see cref="LogLevel.Information"/>, with the
/// OpenTelemetry keys of <see cref="DexpaceLogKeys"/>, the always-redacted URL, and the allow-listed headers (the rest are
/// <c>REDACTED</c>). Secrets are never logged. A logger that throws never fails the request: the failure surfaces as one
/// <c>http.instrumentation.log_failed</c> event, and span and meter callbacks are not wrapped by that guard.
/// </para>
/// <para>
/// <b>Breaking (5b):</b> the policy used to log at <see cref="LogLevel.Debug"/> on every call, with generated event names
/// (ids 1 to 3) and <c>{Method}</c>, <c>{Url}</c>, <c>{StatusCode}</c> keys, <c>error.type</c> as the short type name, and
/// unguarded log calls (a throwing logger failed the request). It now logs nothing unless asked, under the names, ids,
/// levels and keys above, <c>error.type</c> is the full type name, and the log calls are guarded.
/// </para>
/// </remarks>
public sealed class InstrumentationPolicy : HttpPipelinePolicy
{
    private readonly RedactionCache _redaction = new();
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

    // The logger this policy writes to; the pipeline hands it to the call (CallState.Logger, P5b-6).
    internal ILogger Logger => _logger;

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.Diagnostics;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(InstrumentationPolicy));

    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var token = context.CancellationToken;
        var logging = context.Options.Logging;
        var redaction = _redaction.Get(logging);
        var scope = new AttemptScope(request, context, redaction.Redactor);
        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
        var log = new RequestLog(telemetry.Outgoing);
        try
        {
            log = HttpLogEmitter.OnRequest(_logger, ref scope, telemetry.Outgoing, logging, redaction, token);
            Response response;
            try
            {
                response = async
                    ? await continuation.RunAsync(log.Request, telemetry.Downstream).ConfigureAwait(false)
                    : continuation.Run(log.Request, telemetry.Downstream);
                telemetry.Succeeded(response, ref scope);
            }
            catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
            {
                // RETRY-25: a fatal exception is not logged or recorded here; no SDK frame catches it (design §10 entry 12).
                telemetry.Failed(ex, ref scope);
                HttpLogEmitter.OnFailure(_logger, ref scope, log, ex, token);
                throw;
            }

            return async
                ? await HttpLogEmitter.CompleteAsync(_logger, scope, log, response, token).ConfigureAwait(false)
                : HttpLogEmitter.Complete(_logger, ref scope, log, response, token);
        }
        finally
        {
            telemetry.End();
        }
    }
}
