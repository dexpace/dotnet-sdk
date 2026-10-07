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
/// <b>Structure.</b> The attempt is split into an <c>AttemptScope</c> (the lazily redacted URL and the entry timestamp),
/// <c>AttemptTelemetry</c> (the span and the instruments) and <c>HttpLogEmitter</c> (the log events); this method only
/// orchestrates them.
/// </para>
/// <para>
/// <b>Logging.</b> Structured <see cref="ILogger"/> events are emitted at
/// <see cref="LogLevel.Debug"/> with the redacted URL. Secrets are never logged.
/// </para>
/// </remarks>
public sealed class InstrumentationPolicy : HttpPipelinePolicy
{
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

    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var scope = new AttemptScope(request, context, s_redactor);
        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
        HttpLogEmitter.OnRequest(_logger, ref scope);
        try
        {
            var response = async
                ? await continuation.RunAsync(telemetry.Outgoing, telemetry.Downstream).ConfigureAwait(false)
                : continuation.Run(telemetry.Outgoing, telemetry.Downstream);

            telemetry.Succeeded(response, ref scope);
            HttpLogEmitter.OnResponse(_logger, ref scope, response.Status.Code);
            return response;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // RETRY-25: a fatal exception is not logged or recorded here; no SDK frame catches it (design §10 entry 12).
            telemetry.Failed(ex, ref scope);
            HttpLogEmitter.OnFailure(_logger, ref scope, ex);
            throw;
        }
        finally
        {
            telemetry.End();
        }
    }
}
