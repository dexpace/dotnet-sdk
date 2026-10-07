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
/// <b>Tracing (OBS-21, OBS-25, OBS-32).</b> A <see cref="ActivityKind.Client"/> attempt <see cref="Activity"/> is started
/// through <see cref="PipelineContext.Instrumentation"/>, so its parent is the operation span the pipeline opened and not
/// <see cref="Activity.Current"/> (P4a-16); a call whose source has no listener has the untraced bundle and gets no attempt
/// span, and the hot path allocates nothing for tracing. The span is named for the method (<c>HTTP</c> for a method outside
/// the conventions' set) and carries <c>http.request.method</c> (<c>_OTHER</c> plus <c>http.request.method_original</c> for
/// such a method), <c>url.full</c> (redacted with the call's
/// <see cref="Configuration.HttpLoggingOptions.AllowedQueryParameters"/>, the same value the log events carry; default
/// <c>api-version</c> only, P5b-10), <c>url.scheme</c>, <c>server.address</c>, <c>server.port</c> (always the port number),
/// <c>http.request.resend_count</c> (the call's transmission ordinal across retries and redirect hops; absent on the first
/// transmission), and on completion <c>http.response.status_code</c> and <c>network.protocol.version</c>. A status of 400 or
/// more sets <c>error.type</c> to the status code and the status to <see cref="ActivityStatusCode.Error"/>; an exception
/// sets <c>error.type</c> to its full type name and the status to <see cref="ActivityStatusCode.Error"/> with its message.
/// Every write is guarded by <see cref="Activity.IsAllDataRequested"/>.
/// </para>
/// <para>
/// <b>W3C trace-context propagation.</b> When the started <see cref="Activity"/> is non-null
/// and its <see cref="Activity.IdFormat"/> is <see cref="ActivityIdFormat.W3C"/>, the policy
/// stamps <c>traceparent</c> (and, when non-empty, <c>tracestate</c>) onto the request headers
/// before forwarding the call, for a transport that does not propagate trace context itself. The reference transport,
/// <c>SystemNetHttpClient</c>, strips the stamp when the runtime injects its own child span (P5c-11, P5c-12).
/// </para>
/// <para>
/// <b>Metrics (OBS-31 to OBS-33).</b> Two instruments are recorded per attempt, with the stable HTTP client attribute sets:
/// <list type="bullet">
///   <item>
///     <c>http.client.request.duration</c> — <see cref="Histogram{T}"/> in seconds with OpenTelemetry's bucket advice,
///     tagged with <c>http.request.method</c>, <c>server.address</c>, <c>server.port</c>, <c>url.scheme</c> and, on
///     completion, <c>http.response.status_code</c> and <c>network.protocol.version</c>, or <c>error.type</c> (for an
///     exception, or a status of 400 or more).
///   </item>
///   <item>
///     <c>http.client.active_requests</c> — <see cref="UpDownCounter{T}"/> incremented before
///     the send and decremented after (in a <c>finally</c> block), tagged with the four start attributes.
///   </item>
/// </list>
/// A consumer that enables both <c>Dexpace.Sdk</c> and <c>System.Net.Http</c> sees each attempt measured twice under one
/// name; enable one or the other.
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
/// <b>Body level (OBS-34, OBS-36 to OBS-38, BODY-34).</b> At <see cref="Configuration.HttpLogLevel.Body"/> the request body
/// is tapped on the request passed downstream (never on the one a retry or redirect policy holds) and a response with a
/// known-length body that is not <c>text/event-stream</c> comes back with a logging wrapper: up to
/// <see cref="Configuration.HttpLoggingOptions.BodyPreviewSize"/> bytes are read before the call returns, a body that fits
/// is then served from memory and can be opened again, and a larger one is served as the captured prefix followed by the
/// live remainder, so the caller always receives every byte. Unknown-length and event-stream bodies are never wrapped, so
/// no capture waits on a slow producer. <b>Breaking:</b> the wrapped body, the added latency of that read and
/// <c>ContentLength</c> following BODY-29. The request preview rides on the response (or failure) event because the body is
/// written inside the continuation, after <c>http.request</c>. <b>Body level logs payloads verbatim up to the preview
/// size: it is for diagnosis, not for production.</b>
/// </para>
/// <para>
/// <b>Breaking (5c):</b> attempt spans are children of the operation span, no longer of the caller's
/// <see cref="Activity.Current"/>; <c>server.port</c> is the port number (it was <c>-1</c> for a default port);
/// <c>http.request.resend_count</c> counts redirect hops as well as retries and is absent on the first transmission (it was
/// <c>0</c>); a 4xx/5xx response sets <c>error.type</c> and the error status; an unknown method is <c>_OTHER</c> and the span
/// is named <c>HTTP</c>; the metrics gain <c>server.address</c>, <c>server.port</c> and <c>url.scheme</c> (and, on the
/// histogram, <c>network.protocol.version</c>), and unknown methods are <c>_OTHER</c>; a listener whose
/// <c>ActivityStopped</c> throws after the response exists now has that response disposed before the exception propagates
/// (it leaked before).
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
            }
            catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
            {
                // RETRY-25: a fatal exception is not logged or recorded here; no SDK frame catches it (design §10 entry 12).
                telemetry.Failed(ex, ref scope);
                HttpLogEmitter.OnFailure(_logger, ref scope, log, ex, token);
                throw;
            }

            // Recording the response and ending the span run meter and listener callbacks, which are not wrapped (OBS-20,
            // OBS-30). A throw from one after the response exists must not leak that response (P5c-13): release it, then
            // let the exception propagate.
            var held = response;
            try
            {
                telemetry.Succeeded(response, ref scope);
                held = async
                    ? await HttpLogEmitter.CompleteAsync(_logger, scope, log, response, token).ConfigureAwait(false)
                    : HttpLogEmitter.Complete(_logger, ref scope, log, response, token);
                telemetry.End();
            }
            catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
            {
                await ReleaseAsync(held, ex, async).ConfigureAwait(false);
                throw;
            }

            return held;
        }
        finally
        {
            telemetry.End();
        }
    }

    private ValueTask ReleaseAsync(Response response, Exception primary, bool async)
    {
        if (async)
        {
            return Disposal.DisposeQuietlyAsync(response, primary, _logger);
        }

        Disposal.DisposeQuietly(response, primary, _logger);
        return ValueTask.CompletedTask;
    }
}
