// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// What one transmission attempt knows about itself, shared by the log half and the span and metric half of
/// <c>InstrumentationPolicy</c> (OBS-1, design 5b position A).
/// </summary>
/// <remarks>
/// <para>
/// Construction allocates nothing: the entry time is a <see cref="Stopwatch.GetTimestamp"/> value (no
/// <see cref="Stopwatch"/> instance) and the redacted URL is computed on the first read of <see cref="RedactedUrl"/>, so
/// an attempt that nothing consumes (no listener, no enabled event) never redacts.
/// </para>
/// <para>
/// This is a mutable struct that carries a lazy cache. The orchestrator holds it in one local and passes it by
/// <see langword="ref"/> to every synchronous consumer, so the cache is written to that one instance and never to a
/// defensive copy. One attempt runs on one orchestrator, so no lock is needed (plan reading R8).
/// </para>
/// </remarks>
internal struct AttemptScope
{
    private readonly UrlRedactor _redactor;
    private string? _redactedUrl;
    private long _stopTimestamp;

    internal AttemptScope(Request request, PipelineContext context, UrlRedactor redactor)
    {
        Request = request;
        MethodName = request.Method.Name;
        AttemptNumber = context.AttemptNumber;
        StartTimestamp = Stopwatch.GetTimestamp();
        _redactor = redactor;
    }

    /// <summary>The request the attempt was entered with.</summary>
    internal Request Request { get; }

    /// <summary>The HTTP method name.</summary>
    internal string MethodName { get; }

    /// <summary>The zero-based retry attempt number of the context the attempt was entered with.</summary>
    internal int AttemptNumber { get; }

    /// <summary>
    /// The call's zero-based transmission ordinal across retries and redirect hops: the value of
    /// <c>http.request.resend_count</c> on the attempt span and in the log events, so a span tag and a log key cannot drift
    /// (P5c-18(b), OBS-39). Set by <c>AttemptTelemetry.Begin</c>; zero until then.
    /// </summary>
    internal int ResendCount { get; set; }

    /// <summary>The <see cref="Stopwatch.GetTimestamp"/> value at entry.</summary>
    internal long StartTimestamp { get; }

    /// <summary>The redacted request URL, computed on first read and cached (OBS-1, OBS-12).</summary>
    internal string RedactedUrl => _redactedUrl ??= _redactor.Redact(Request.Url);

    /// <summary>The time since entry, or up to the moment <see cref="Stop"/> was called.</summary>
    internal readonly TimeSpan Elapsed =>
        Stopwatch.GetElapsedTime(StartTimestamp, _stopTimestamp != 0 ? _stopTimestamp : Stopwatch.GetTimestamp());

    /// <summary>Freezes <see cref="Elapsed"/> at the moment the response (or failure) arrived.</summary>
    internal void Stop() => _stopTimestamp = Stopwatch.GetTimestamp();
}
