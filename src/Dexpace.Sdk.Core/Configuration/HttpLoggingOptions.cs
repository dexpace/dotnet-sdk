// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Diagnostics;

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// Controls the SDK's request and response logging (OBS-34 to OBS-38). Logging is off by default.
/// </summary>
/// <remarks>
/// <para>
/// <b>Warning.</b> <see cref="HttpLogLevel.Body"/> logs payloads verbatim up to <see cref="BodyPreviewSize"/> bytes; it is
/// for diagnosis, not for production (XCUT-19).
/// </para>
/// <para>
/// The collection properties copy what they are given, so a caller mutating its list afterwards changes nothing (CFG-8).
/// Equality over the collection members is reference equality; deep equality of options records is decided for all of them
/// together (CFG-33).
/// </para>
/// </remarks>
public sealed record HttpLoggingOptions
{
    /// <summary>The default <see cref="BodyPreviewSize"/>, 8,192 bytes (OBS-36).</summary>
    public const int DefaultBodyPreviewSize = 8192;

    /// <summary>
    /// The 26 header names logged by default: diagnostic, non-credential names only (OBS-18, XCUT-19). Every other header is
    /// logged as <c>REDACTED</c> or omitted.
    /// </summary>
    public static readonly IReadOnlyCollection<string> DefaultAllowedHeaderNames = ImmutableArray.Create(
        "accept", "accept-encoding", "cache-control", "connection", "content-encoding", "content-length",
        "content-location", "content-type", "date", "etag", "expires", "if-match", "if-modified-since", "if-none-match",
        "if-unmodified-since", "last-modified", "location", "retry-after", "server", "traceparent", "tracestate",
        "user-agent", "vary", "via", "x-correlation-id", "x-request-id");

    private static readonly IReadOnlyCollection<string> s_defaultUrlValuedHeaderNames =
        ImmutableArray.Create("location", "content-location", "referer");

    /// <summary>The shared default instance: logging <see cref="HttpLogLevel.None"/> (OBS-34).</summary>
    public static HttpLoggingOptions Default { get; } = new();

    /// <summary>How much of each exchange is logged; <see cref="HttpLogLevel.None"/> by default (OBS-34).</summary>
    public HttpLogLevel Level { get; init; }

    /// <summary>
    /// The most body bytes captured for a preview, for both request and response (OBS-36, BODY-32, BODY-34). A negative
    /// value throws; a value above <see cref="Array.MaxLength"/> is clamped to it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int BodyPreviewSize
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = Math.Min(value, Array.MaxLength);
        }
    } = DefaultBodyPreviewSize;

    /// <summary>The header names whose values are logged (case-insensitive); all others are redacted or omitted (OBS-18).</summary>
    public IReadOnlyCollection<string> AllowedHeaderNames
    {
        get;
        init => field = Copy(value);
    } = DefaultAllowedHeaderNames;

    /// <summary>The query parameter names whose values survive redaction in <c>url.full</c> (OBS-12); default <c>api-version</c>.</summary>
    public IReadOnlyCollection<string> AllowedQueryParameters
    {
        get;
        init => field = Copy(value);
    } = UrlRedactor.DefaultQueryAllowList;

    /// <summary>
    /// The header names whose values are URLs and so are redacted as URLs before logging (OBS-16, OBS-17); default
    /// <c>location</c>, <c>content-location</c> and <c>referer</c>.
    /// </summary>
    public IReadOnlyCollection<string> UrlValuedHeaderNames
    {
        get;
        init => field = Copy(value);
    } = s_defaultUrlValuedHeaderNames;

    /// <summary>
    /// When <see langword="true"/> a header that is not allow-listed is left out; when <see langword="false"/> (the default)
    /// it is logged with the value <c>REDACTED</c>, so "present and hidden" stays distinguishable from "absent" (OBS-18).
    /// </summary>
    public bool OmitDisallowedHeaders { get; init; }

    private static ImmutableArray<string> Copy(IReadOnlyCollection<string> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var item in source)
        {
            if (item is null)
            {
                throw new ArgumentException("The collection must not contain a null element.", nameof(source));
            }
        }

        return ImmutableArray.CreateRange(source);
    }
}
