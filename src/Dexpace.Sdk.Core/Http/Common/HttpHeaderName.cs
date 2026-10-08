// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// A typed, case-insensitive HTTP header name (HTTP-21).
/// </summary>
/// <remarks>
/// Header field names are case-insensitive (RFC 9110 §5.1). Equality and hashing are over <see cref="CanonicalName"/>,
/// the ASCII-folded lower-case form, while <see cref="Original"/> keeps the trimmed spelling the caller supplied, which
/// is what goes on the wire and what <see cref="Headers"/> enumerates. Construct through <see cref="Of(string)"/>.
/// <para>
/// <b>Breaking:</b> was a <c>readonly record struct</c>, so <c>default(HttpHeaderName)</c> and
/// <c>Nullable&lt;HttpHeaderName&gt;</c> no longer exist (a nullable <c>HttpHeaderName?</c> is now a nullable
/// reference), and <see cref="ToString"/> returns <see cref="Original"/> (it returned the canonical name).
/// </para>
/// </remarks>
public sealed record HttpHeaderName
{
    private HttpHeaderName(string canonical, string original)
    {
        CanonicalName = canonical;
        Original = original;
    }

    /// <summary>The ASCII-folded lower-case name used for lookups and equality.</summary>
    public string CanonicalName { get; }

    /// <summary>The trimmed spelling supplied by the caller; the wire form.</summary>
    public string Original { get; }

    /// <summary>
    /// Creates a header name: surrounding SP/HTAB is trimmed, and the rest must be an RFC 9110 token (HTTP-17, design
    /// §11 item 36).
    /// </summary>
    /// <param name="name">The header field name.</param>
    /// <returns>The typed header name; <see cref="Original"/> is the trimmed spelling.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or not a valid token. The message names the offending character by code point
    /// (<c>U+000D</c>) and never carries the raw character (HTTP-20).
    /// </exception>
    public static HttpHeaderName Of(string name)
    {
        var trimmed = HeaderSyntax.ValidateName(name, nameof(name));
        return new HttpHeaderName(AsciiFold.ToLower(trimmed), trimmed);
    }

    /// <summary>Returns <see cref="Original"/>.</summary>
    /// <remarks><b>Breaking:</b> returned <see cref="CanonicalName"/> before HTTP-21.</remarks>
    /// <returns>The trimmed caller spelling.</returns>
    public override string ToString() => Original;

    /// <summary>Equality over <see cref="CanonicalName"/>, ordinally.</summary>
    /// <param name="other">The name to compare with.</param>
    /// <returns><see langword="true"/> when both fold to the same name.</returns>
    public bool Equals(HttpHeaderName? other) =>
        other is not null && string.Equals(CanonicalName, other.CanonicalName, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalName.GetHashCode(StringComparison.Ordinal);

    /// <summary>Common request/response header names as typed constants.</summary>
    public static class WellKnown
    {
        /// <summary>The <c>Accept</c> header.</summary>
        public static HttpHeaderName Accept { get; } = Of("Accept");

        /// <summary>The <c>Authorization</c> header.</summary>
        public static HttpHeaderName Authorization { get; } = Of("Authorization");

        /// <summary>The <c>Content-Length</c> header.</summary>
        public static HttpHeaderName ContentLength { get; } = Of("Content-Length");

        /// <summary>The <c>Content-Type</c> header.</summary>
        public static HttpHeaderName ContentType { get; } = Of("Content-Type");

        /// <summary>The <c>Date</c> header.</summary>
        public static HttpHeaderName Date { get; } = Of("Date");

        /// <summary>The <c>ETag</c> header.</summary>
        public static HttpHeaderName ETag { get; } = Of("ETag");

        /// <summary>The <c>Location</c> header.</summary>
        public static HttpHeaderName Location { get; } = Of("Location");

        /// <summary>The <c>Idempotency-Key</c> header.</summary>
        public static HttpHeaderName IdempotencyKey { get; } = Of("Idempotency-Key");

        /// <summary>The <c>If-Match</c> header.</summary>
        public static HttpHeaderName IfMatch { get; } = Of("If-Match");

        /// <summary>The <c>If-None-Match</c> header.</summary>
        public static HttpHeaderName IfNoneMatch { get; } = Of("If-None-Match");

        /// <summary>The <c>If-Modified-Since</c> header.</summary>
        public static HttpHeaderName IfModifiedSince { get; } = Of("If-Modified-Since");

        /// <summary>The <c>If-Unmodified-Since</c> header.</summary>
        public static HttpHeaderName IfUnmodifiedSince { get; } = Of("If-Unmodified-Since");

        /// <summary>The <c>Proxy-Authenticate</c> header.</summary>
        public static HttpHeaderName ProxyAuthenticate { get; } = Of("Proxy-Authenticate");

        /// <summary>The <c>Proxy-Authorization</c> header.</summary>
        public static HttpHeaderName ProxyAuthorization { get; } = Of("Proxy-Authorization");

        /// <summary>The <c>Range</c> header (see <c>HttpRange</c>).</summary>
        public static HttpHeaderName Range { get; } = Of("Range");

        /// <summary>The <c>Retry-After</c> header.</summary>
        public static HttpHeaderName RetryAfter { get; } = Of("Retry-After");

        /// <summary>The <c>User-Agent</c> header.</summary>
        public static HttpHeaderName UserAgent { get; } = Of("User-Agent");

        /// <summary>The <c>WWW-Authenticate</c> header.</summary>
        public static HttpHeaderName WwwAuthenticate { get; } = Of("WWW-Authenticate");
    }
}
