// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// How much of each HTTP exchange the SDK writes to its logger (OBS-34). The default is <see cref="None"/>: logging is
/// opt-in, and traces and metrics are recorded at every level.
/// </summary>
/// <remarks>
/// <b>Warning.</b> <see cref="Body"/> logs payloads verbatim up to <see cref="HttpLoggingOptions.BodyPreviewSize"/> bytes.
/// It is for diagnosis, not for production: a body can carry credentials or personal data that no header or URL
/// redaction reaches (XCUT-19).
/// </remarks>
public enum HttpLogLevel
{
    /// <summary>No <c>http.request</c> or <c>http.response</c> event is written and no body wrapper is constructed (OBS-34).</summary>
    None = 0,

    /// <summary>The request and response events with the redacted URL, the status and the allow-listed headers (OBS-34).</summary>
    Headers = 1,

    /// <summary>Everything in <see cref="Headers"/> plus a bounded preview of the request and response bodies (OBS-34, OBS-36).</summary>
    Body = 2,
}
