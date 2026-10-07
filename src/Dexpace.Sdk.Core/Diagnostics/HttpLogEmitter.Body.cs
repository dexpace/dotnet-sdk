// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <content>
/// The body-level half of the emitter (OBS-34, OBS-36 to OBS-38, BODY-34). Wrappers are constructed at
/// <see cref="HttpLogLevel.Body"/> only, and only when something will consume the preview.
/// </content>
internal static partial class HttpLogEmitter
{
    // The placeholder name is the published state key (OBS-39, P5b-3); see s_logFailed.
#pragma warning disable CA1727
    private static readonly Action<ILogger, string, Exception?> s_bodyCaptureFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.BodyCaptureFailedId, DexpaceLogEvents.BodyCaptureFailed),
        "Capturing the response body preview failed with {error.type}; the caller still receives the body.");
#pragma warning restore CA1727

    // BODY-34 / OBS-34: the request body is tapped at body level only. Retry and redirect sit above this stage and re-send the
    // request they hold, so each attempt wraps afresh and the tap reflects that attempt only (BODY-18); the wrapper is applied
    // to the request passed down and never written upward (PIPE-16). With nothing enabled there is nothing to consume the
    // preview, so no wrapper is built.
    private static (Request Sent, LoggingRequestBody? Tap) TapBody(
        ILogger logger,
        Request outgoing,
        HttpLoggingOptions options,
        bool enabled)
    {
        if (options.Level != HttpLogLevel.Body || outgoing.Body is not { } body)
        {
            return (outgoing, null);
        }

        if (!enabled && !logger.IsEnabled(LogLevel.Warning))
        {
            return (outgoing, null);
        }

        var tap = new LoggingRequestBody(body, options.BodyPreviewSize);
        return (outgoing.WithBody(tap), tap);
    }

    // OBS-37 / P5b-12: an unknown-length or text/event-stream body is never wrapped, on either path, so no capture can wait on
    // a slow producer. The swap moves ownership and the exchange links (P5b-13).
    private static Response Engage(ILogger logger, in RequestLog log, Response response, out LoggingResponseBody? wrapper)
    {
        wrapper = null;
        var options = log.Options!;
        var body = response.Body;
        if (options.Level != HttpLogLevel.Body || body.ContentLength < 0 || IsEventStream(body.ContentType))
        {
            return response;
        }

        var created = new LoggingResponseBody(body, options.BodyPreviewSize, logger);
        var swapped = response.ReplaceBody(created);
        wrapper = created;
        return swapped;
    }

    private static bool IsEventStream(MediaType? type) =>
        type is not null
        && string.Equals(type.Type, "text", StringComparison.OrdinalIgnoreCase)
        && string.Equals(type.Subtype, "event-stream", StringComparison.OrdinalIgnoreCase);

    // After the preview drain: report a failed capture, then emit http.response (with the partial preview, BODY-26).
    private static void Conclude(
        ILogger logger,
        ref AttemptScope scope,
        in RequestLog log,
        Response response,
        long declaredLength,
        ResponseCapture captured,
        CancellationToken token)
    {
        if (captured.Wrapper?.DrainFailure is { } failure)
        {
            // XCUT-3: the call's own cancellation is not a capture failure; the caller disposes the response and rethrows.
            if (failure is OperationCanceledException)
            {
                token.ThrowIfCancellationRequested();
            }

            s_bodyCaptureFailed(logger, failure.GetType().FullName ?? failure.GetType().Name, failure);
        }

        EmitResponse(logger, ref scope, log, response, declaredLength, captured);
    }

    private static void AddPreviews(List<KeyValuePair<string, object?>> pairs, in RequestLog log, Response response, ResponseCapture captured)
    {
        AddRequestPreview(pairs, log);
        if (captured.Bytes is { } bytes)
        {
            pairs.Add(new KeyValuePair<string, object?>(DexpaceLogKeys.HttpResponseBodyPreview, BodyPreviewRenderer.Render(bytes, response.Body.ContentType)));
            pairs.Add(new KeyValuePair<string, object?>(DexpaceLogKeys.HttpResponseBodyPreviewSize, bytes.Length));
        }
    }

    // P5b-25: the request body is written inside the continuation, after http.request, so its preview rides on the response
    // or the failure event.
    private static void AddRequestPreview(List<KeyValuePair<string, object?>> pairs, in RequestLog log)
    {
        if (log.Tap is not { } tap)
        {
            return;
        }

        var bytes = tap.Snapshot(log.Options!.BodyPreviewSize);
        pairs.Add(new KeyValuePair<string, object?>(DexpaceLogKeys.HttpRequestBodyPreview, BodyPreviewRenderer.Render(bytes, tap.ContentType)));
        pairs.Add(new KeyValuePair<string, object?>(DexpaceLogKeys.HttpRequestBodyPreviewSize, bytes.Length));
    }
}
