// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// A typed, case-insensitive HTTP header name.
/// </summary>
/// <remarks>
/// Header field names are case-insensitive (RFC 7230 §3.2). This type stores the name in
/// lower-case canonical form so it can be used directly as a <see cref="Headers"/> key without
/// re-lowering on the hot path, while <see cref="Original"/> preserves the spelling the caller
/// supplied for display. Equality and hashing are over the canonical form.
/// </remarks>
public readonly record struct HttpHeaderName
{
    private HttpHeaderName(string canonical, string original)
    {
        CanonicalName = canonical;
        Original = original;
    }

    /// <summary>The lower-cased canonical name used for lookups and equality.</summary>
    public string CanonicalName { get; }

    /// <summary>The original spelling supplied by the caller (for display only).</summary>
    public string Original { get; }

    /// <summary>Creates a header name, validating it as an RFC 7230 token.</summary>
    /// <param name="name">The header field name.</param>
    /// <returns>The typed header name.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or not a valid token.</exception>
    public static HttpHeaderName Of(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0)
        {
            throw new ArgumentException("Header name must not be empty.", nameof(name));
        }

        foreach (var c in name)
        {
            if (!IsTokenChar(c))
            {
                throw new ArgumentException($"Invalid header-name character '{c}'.", nameof(name));
            }
        }

        return new HttpHeaderName(name.ToLowerInvariant(), name);
    }

    /// <summary>Returns the canonical (lower-cased) name.</summary>
    public override string ToString() => CanonicalName;

    /// <summary>Equality over the canonical (case-insensitive) name.</summary>
    public bool Equals(HttpHeaderName other) => CanonicalName == other.CanonicalName;

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalName.GetHashCode(StringComparison.Ordinal);

    private static bool IsTokenChar(char c) =>
        c is >= 'a' and <= 'z'
        or >= 'A' and <= 'Z'
        or >= '0' and <= '9'
        or '!' or '#' or '$' or '%' or '&' or '\'' or '*'
        or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';

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

        /// <summary>The <c>Retry-After</c> header.</summary>
        public static HttpHeaderName RetryAfter { get; } = Of("Retry-After");

        /// <summary>The <c>User-Agent</c> header.</summary>
        public static HttpHeaderName UserAgent { get; } = Of("User-Agent");
    }
}
