// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// An HTTP request method.
/// </summary>
/// <remarks>
/// Modelled as an immutable value type wrapping the wire token rather than a closed
/// <see langword="enum"/> so that callers may use registered extension methods (WebDAV,
/// PATCH variants, vendor verbs) the SDK does not enumerate. The well-known verbs are
/// exposed as static members; arbitrary tokens go through <see cref="Of(string)"/>.
/// Two methods are equal when their (case-sensitive, upper-cased) <see cref="Name"/>
/// tokens match, mirroring the case sensitivity HTTP assigns to method names (RFC 7231
/// §4.1).
/// </remarks>
public readonly record struct Method
{
    private Method(string name) => Name = name;

    /// <summary>The method token exactly as it appears on the wire (e.g. <c>GET</c>).</summary>
    public string Name { get; }

    /// <summary>The <c>GET</c> method — safe, idempotent, no request body.</summary>
    public static Method Get { get; } = new("GET");

    /// <summary>The <c>HEAD</c> method — like GET but no response body.</summary>
    public static Method Head { get; } = new("HEAD");

    /// <summary>The <c>POST</c> method — submits an entity; neither safe nor idempotent.</summary>
    public static Method Post { get; } = new("POST");

    /// <summary>The <c>PUT</c> method — replaces the target resource; idempotent.</summary>
    public static Method Put { get; } = new("PUT");

    /// <summary>The <c>PATCH</c> method — applies a partial modification.</summary>
    public static Method Patch { get; } = new("PATCH");

    /// <summary>The <c>DELETE</c> method — removes the target resource; idempotent.</summary>
    public static Method Delete { get; } = new("DELETE");

    /// <summary>The <c>OPTIONS</c> method — describes the communication options.</summary>
    public static Method Options { get; } = new("OPTIONS");

    /// <summary>The <c>TRACE</c> method — performs a message loop-back test.</summary>
    public static Method Trace { get; } = new("TRACE");

    /// <summary>The <c>CONNECT</c> method — establishes a tunnel to the server.</summary>
    public static Method Connect { get; } = new("CONNECT");

    /// <summary>
    /// Returns the <see cref="Method"/> for <paramref name="token"/>. Known verbs resolve to
    /// their cached static instances; unknown tokens are accepted verbatim (after trimming and
    /// upper-casing the canonical HTTP verbs).
    /// </summary>
    /// <param name="token">The method token, e.g. <c>"GET"</c> or a vendor verb.</param>
    /// <returns>A <see cref="Method"/> wrapping the token.</returns>
    /// <exception cref="ArgumentException"><paramref name="token"/> is empty or whitespace.</exception>
    public static Method Of(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("HTTP method token must not be empty.", nameof(token));
        }

        return trimmed.ToUpperInvariant() switch
        {
            "GET" => Get,
            "HEAD" => Head,
            "POST" => Post,
            "PUT" => Put,
            "PATCH" => Patch,
            "DELETE" => Delete,
            "OPTIONS" => Options,
            "TRACE" => Trace,
            "CONNECT" => Connect,
            _ => new Method(trimmed),
        };
    }

    /// <summary>
    /// True when this method is defined as safe (read-only) by RFC 7231: GET, HEAD, OPTIONS, TRACE.
    /// </summary>
    public bool IsSafe =>
        Name is "GET" or "HEAD" or "OPTIONS" or "TRACE";

    /// <summary>
    /// True when repeating the request has the same effect as issuing it once (RFC 7231 §4.2.2).
    /// Safe methods plus PUT and DELETE are idempotent.
    /// </summary>
    public bool IsIdempotent =>
        IsSafe || Name is "PUT" or "DELETE";

    /// <summary>Returns <see cref="Name"/>.</summary>
    public override string ToString() => Name;
}
