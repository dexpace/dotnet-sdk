// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The log half of <c>InstrumentationPolicy</c>: every log emission of the attempt (design 5b positions B to E).
/// </summary>
/// <remarks>
/// <para>
/// <b>Gate order (OBS-1, OBS-34).</b> The level is checked first, then <see cref="ILogger.IsEnabled"/>, and only then is
/// state built. The redacted URL is read from the <see cref="AttemptScope"/> only after both, so a disabled attempt
/// computes and allocates nothing.
/// </para>
/// <para>
/// <b>The guard (OBS-20, XCUT-20, P5b-11).</b> Each emission method holds its own <c>try</c>/<c>catch</c> (a delegate-based
/// guard would allocate on the disabled path). A non-fatal failure is reported as one
/// <c>http.instrumentation.log_failed</c> event and swallowed; a failure while reporting is swallowed too; a fatal exception,
/// and a cancellation of the call's own token, propagate. The span and meter calls (<c>AttemptTelemetry</c>) are never
/// inside a guard.
/// </para>
/// <para>
/// The HTTP events go through <see cref="ILogger.Log{TState}"/> with an <see cref="HttpLogRecord"/> because their key set is
/// dynamic (one key per logged header); the fixed-key diagnostics use <c>LoggerMessage.Define</c>. Both are a
/// departure from the source generator of styleguide 6.2, which cannot express OpenTelemetry's dotted keys (P5b-3).
/// </para>
/// </remarks>
internal static partial class HttpLogEmitter
{
    private const string RequestTemplate = "HTTP {http.request.method} {url.full}";
    private const string ResponseTemplate = "HTTP {http.request.method} {url.full} {http.response.status_code}";
    private const string FailureTemplate = "HTTP {http.request.method} {url.full} failed with {error.type}";

    private static readonly EventId s_requestEvent = new(DexpaceLogEvents.HttpRequestId, DexpaceLogEvents.HttpRequest);
    private static readonly EventId s_responseEvent = new(DexpaceLogEvents.HttpResponseId, DexpaceLogEvents.HttpResponse);
    private static readonly EventId s_failureEvent = new(DexpaceLogEvents.HttpFailureId, DexpaceLogEvents.HttpResponse);

    // The placeholder names are the published OpenTelemetry-style state keys (OBS-39, P5b-3), which CA1727's PascalCase rule
    // and the source generator cannot express; the fixed-key Define form is the verified way (design §8.1).
#pragma warning disable CA1727
    private static readonly Action<ILogger, string, string, Exception?> s_logFailed = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.LogFailedId, DexpaceLogEvents.LogFailed),
        "Emitting the {dexpace.instrumentation.failed_event} log event failed with {error.type}; the request was not affected.");
#pragma warning restore CA1727

    /// <summary>
    /// Emits <c>http.request</c> and decides what the attempt sends: the request itself, or at body level a copy whose body is
    /// tapped.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scope">The attempt's scope.</param>
    /// <param name="outgoing">The request about to be sent.</param>
    /// <param name="options">The call's logging options.</param>
    /// <param name="entry">The redactor and header renderer for <paramref name="options"/>.</param>
    /// <param name="token">The call's cancellation token.</param>
    /// <returns>What the response half needs.</returns>
    internal static RequestLog OnRequest(
        ILogger logger,
        ref AttemptScope scope,
        Request outgoing,
        HttpLoggingOptions options,
        RedactionCache.Entry entry,
        CancellationToken token)
    {
        if (options.Level == HttpLogLevel.None)
        {
            return new RequestLog(outgoing);
        }

        var sent = outgoing;
        LoggingRequestBody? tap = null;
        var enabled = false;
        try
        {
            enabled = logger.IsEnabled(LogLevel.Information);
            (sent, tap) = TapBody(logger, outgoing, options, enabled);
            if (enabled)
            {
                EmitRequest(logger, ref scope, outgoing, entry);
            }
        }
        catch (Exception ex) when (Reportable(ex, token))
        {
            ReportFailure(logger, DexpaceLogEvents.HttpRequest, ex);
        }

        return new RequestLog(sent, tap, options, entry, enabled);
    }

    /// <summary>Emits the failure event for an attempt whose continuation threw.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scope">The attempt's scope.</param>
    /// <param name="log">The request half's result.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="token">The call's cancellation token.</param>
    internal static void OnFailure(
        ILogger logger,
        ref AttemptScope scope,
        in RequestLog log,
        Exception exception,
        CancellationToken token)
    {
        if (log.Options is null)
        {
            return;
        }

        try
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                EmitFailure(logger, ref scope, log, exception);
            }
        }
        catch (Exception ex) when (Reportable(ex, token))
        {
            ReportFailure(logger, DexpaceLogEvents.HttpResponse, ex);
        }
    }

    /// <summary>Emits <c>http.response</c> for a response that arrived (synchronous path).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scope">The attempt's scope.</param>
    /// <param name="log">The request half's result.</param>
    /// <param name="response">The response.</param>
    /// <param name="token">The call's cancellation token.</param>
    /// <returns>The response to hand back: the same instance, or one carrying a wrapped body at body level.</returns>
    internal static Response Complete(
        ILogger logger,
        ref AttemptScope scope,
        in RequestLog log,
        Response response,
        CancellationToken token)
    {
        if (!log.Enabled)
        {
            return response;
        }

        var declared = response.Body.ContentLength;
        var captured = default(ResponseCapture);
        try
        {
            response = Engage(logger, log, response, out var wrapper);
            if (wrapper is not null)
            {
                captured = new ResponseCapture(wrapper, wrapper.Snapshot(log.Options!.BodyPreviewSize, token));
            }

            Conclude(logger, ref scope, log, response, declared, captured, token);
        }
        catch (Exception ex) when (Reportable(ex, token))
        {
            ReportFailure(logger, DexpaceLogEvents.HttpResponse, ex);
        }
        catch (OperationCanceledException ex) when (token.IsCancellationRequested)
        {
            Disposal.DisposeQuietly(response, ex, logger);
            throw;
        }

        return response;
    }

    /// <summary>Emits <c>http.response</c> for a response that arrived (asynchronous path).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scope">A copy of the attempt's scope; the redacted URL is already cached in it when it was read.</param>
    /// <param name="log">The request half's result.</param>
    /// <param name="response">The response.</param>
    /// <param name="token">The call's cancellation token.</param>
    /// <returns>The response to hand back: the same instance, or one carrying a wrapped body at body level.</returns>
    internal static async ValueTask<Response> CompleteAsync(
        ILogger logger,
        AttemptScope scope,
        RequestLog log,
        Response response,
        CancellationToken token)
    {
        if (!log.Enabled)
        {
            return response;
        }

        var declared = response.Body.ContentLength;
        var captured = default(ResponseCapture);
        try
        {
            response = Engage(logger, log, response, out var wrapper);
            if (wrapper is not null)
            {
                var bytes = await wrapper.SnapshotAsync(log.Options!.BodyPreviewSize, token).ConfigureAwait(false);
                captured = new ResponseCapture(wrapper, bytes);
            }

            Conclude(logger, ref scope, log, response, declared, captured, token);
        }
        catch (Exception ex) when (Reportable(ex, token))
        {
            ReportFailure(logger, DexpaceLogEvents.HttpResponse, ex);
        }
        catch (OperationCanceledException ex) when (token.IsCancellationRequested)
        {
            await Disposal.DisposeQuietlyAsync(response, ex, logger).ConfigureAwait(false);
            throw;
        }

        return response;
    }

    // OBS-20 / P5b-11: a fatal exception, and a cancellation of the call's own token, are not the logger's to swallow.
    private static bool Reportable(Exception exception, CancellationToken token) =>
        !ExceptionFacts.IsFatal(exception) && !(exception is OperationCanceledException && token.IsCancellationRequested);

    // OBS-20: one diagnostic is attempted; a failure while reporting is swallowed.
    private static void ReportFailure(ILogger logger, string eventName, Exception failure)
    {
#pragma warning disable CA1031 // OBS-20: a secondary failure while reporting a logging failure is swallowed, never propagated.
        try
        {
            s_logFailed(logger, eventName, failure.GetType().FullName ?? failure.GetType().Name, failure);
        }
        catch (Exception inner) when (!ExceptionFacts.IsFatal(inner))
        {
            // Swallowed by design.
        }
#pragma warning restore CA1031
    }

    private static void EmitRequest(ILogger logger, ref AttemptScope scope, Request request, RedactionCache.Entry entry)
    {
        var pairs = new List<KeyValuePair<string, object?>>(8 + request.Headers.Count)
        {
            new(DexpaceLogKeys.HttpRequestMethod, scope.MethodName),
            new(DexpaceLogKeys.UrlFull, LogText.Truncate(scope.RedactedUrl)),
            new(DexpaceLogKeys.HttpRequestResendCount, scope.AttemptNumber),
        };
        entry.Renderer.Render(request.Headers, DexpaceLogKeys.HttpRequestHeaderPrefix, pairs);
        AddDeclaredSize(pairs, DexpaceLogKeys.HttpRequestBodySize, request.Body?.ContentLength ?? -1);

        var message = $"HTTP {scope.MethodName} {LogText.Truncate(scope.RedactedUrl)}";
        var record = new HttpLogRecord(pairs, RequestTemplate, message);
        logger.Log(LogLevel.Information, s_requestEvent, record, null, HttpLogRecord.Formatter);
    }

    private static void EmitResponse(
        ILogger logger,
        ref AttemptScope scope,
        in RequestLog log,
        Response response,
        long declaredLength,
        ResponseCapture captured)
    {
        var pairs = new List<KeyValuePair<string, object?>>(12 + response.Headers.Count)
        {
            new(DexpaceLogKeys.HttpRequestMethod, scope.MethodName),
            new(DexpaceLogKeys.UrlFull, LogText.Truncate(scope.RedactedUrl)),
            new(DexpaceLogKeys.HttpRequestResendCount, scope.AttemptNumber),
            new(DexpaceLogKeys.HttpResponseStatusCode, response.Status.Code),
            new(DexpaceLogKeys.HttpResponseDurationMs, scope.Elapsed.TotalMilliseconds),
        };
        log.Entry!.Renderer.Render(response.Headers, DexpaceLogKeys.HttpResponseHeaderPrefix, pairs);
        AddDeclaredSize(pairs, DexpaceLogKeys.HttpResponseBodySize, declaredLength);
        AddPreviews(pairs, log, response, captured);

        var message = $"HTTP {scope.MethodName} {LogText.Truncate(scope.RedactedUrl)} {response.Status.Code}";
        var record = new HttpLogRecord(pairs, ResponseTemplate, message);
        logger.Log(LogLevel.Information, s_responseEvent, record, null, HttpLogRecord.Formatter);
    }

    private static void EmitFailure(ILogger logger, ref AttemptScope scope, in RequestLog log, Exception exception)
    {
        var errorType = exception.GetType().FullName ?? exception.GetType().Name;
        var pairs = new List<KeyValuePair<string, object?>>(8)
        {
            new(DexpaceLogKeys.HttpRequestMethod, scope.MethodName),
            new(DexpaceLogKeys.UrlFull, LogText.Truncate(scope.RedactedUrl)),
            new(DexpaceLogKeys.HttpRequestResendCount, scope.AttemptNumber),
            new(DexpaceLogKeys.ErrorType, errorType),
            new(DexpaceLogKeys.HttpResponseDurationMs, scope.Elapsed.TotalMilliseconds),
        };
        AddRequestPreview(pairs, log);

        var message = $"HTTP {scope.MethodName} {LogText.Truncate(scope.RedactedUrl)} failed with {errorType}";
        var record = new HttpLogRecord(pairs, FailureTemplate, message);
        logger.Log(LogLevel.Warning, s_failureEvent, record, exception, HttpLogRecord.Formatter);
    }

    // OBS-36 / P5b-7: *.body.size is the declared length, and only when it is known.
    private static void AddDeclaredSize(List<KeyValuePair<string, object?>> pairs, string key, long declared)
    {
        if (declared >= 0)
        {
            pairs.Add(new KeyValuePair<string, object?>(key, declared));
        }
    }
}
