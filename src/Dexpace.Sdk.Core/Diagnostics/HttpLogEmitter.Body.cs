// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <content>The body-level half of the emitter (OBS-34, OBS-36 to OBS-38, BODY-34).</content>
internal static partial class HttpLogEmitter
{
    // Wired in the body-level step; at header level the request is sent as is.
    private static (Request Sent, LoggingRequestBody? Tap) TapBody(ILogger logger, Request outgoing, HttpLoggingOptions options, bool enabled) =>
        (outgoing, null);

    private static Response Engage(ILogger logger, in RequestLog log, Response response, out LoggingResponseBody? wrapper)
    {
        wrapper = null;
        return response;
    }

    private static void Conclude(
        ILogger logger,
        ref AttemptScope scope,
        in RequestLog log,
        Response response,
        long declaredLength,
        ResponseCapture captured,
        CancellationToken token) =>
        EmitResponse(logger, ref scope, log, response, declaredLength, captured);

    private static void AddPreviews(List<KeyValuePair<string, object?>> pairs, in RequestLog log, Response response, ResponseCapture captured)
    {
    }

    private static void AddRequestPreview(List<KeyValuePair<string, object?>> pairs, in RequestLog log)
    {
    }
}
