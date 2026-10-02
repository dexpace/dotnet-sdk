// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Pipeline.Policies;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// An HTTP request method.
/// </summary>
/// <remarks>
/// A <see langword="sealed"/> <see langword="record"/> wrapping the wire token rather than a closed
/// <see langword="enum"/>, so callers may use registered extension methods (WebDAV, vendor verbs) the SDK does not
/// enumerate. The well-known verbs are exposed as static members; arbitrary tokens go through
/// <see cref="Of(string)"/>, which validates an RFC 9110 token (HTTP-9). Two methods are equal when their
/// <see cref="Name"/> tokens are equal.
/// <para>
/// <b>Case.</b> RFC 9110 makes method names case-sensitive, but <see cref="Of(string)"/> deliberately folds the nine
/// well-known verbs, ASCII case-insensitively: <c>get</c> and <c>Get</c> resolve to <see cref="Get"/> (design §4.3,
/// ruled 2026-09-30). Every other token is kept verbatim, so <c>Foo</c> and <c>FOO</c> are different methods.
/// </para>
/// <para>
/// <b>Breaking:</b> was a <c>readonly record struct</c>; <c>default(Method)</c> and <c>Nullable&lt;Method&gt;</c> no
/// longer exist, <see cref="Of(string)"/> rejects a non-token, and the public <c>IsSafe</c> and <c>IsIdempotent</c>
/// properties are removed (the single source is internal; HTTP-9).
/// </para>
/// </remarks>
public sealed record Method
{
    private Method(string name) => Name = name;

    /// <summary>The method token exactly as it appears on the wire (e.g. <c>GET</c>).</summary>
    public string Name { get; }

    /// <summary>The <c>GET</c> method — read-only, idempotent, no request body.</summary>
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
    /// Returns the <see cref="Method"/> for <paramref name="token"/>. Surrounding SP/HTAB is trimmed and the rest must
    /// be an RFC 9110 token. The nine well-known verbs resolve, ASCII case-insensitively, to their cached static
    /// instances; any other token is kept verbatim.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Breaking:</b> previously accepted any non-blank string; a non-token such as <c>"GET\r\nX: y"</c> or
    /// <c>"FOO BAR"</c> now throws.
    /// </para>
    /// </remarks>
    /// <param name="token">The method token, e.g. <c>"GET"</c> or a vendor verb.</param>
    /// <returns>A <see cref="Method"/> wrapping the token.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="token"/> is blank or not a token. The message names the offending character by code point and
    /// never echoes the input (HTTP-20).
    /// </exception>
    public static Method Of(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var trimmed = token.Trim(' ', '\t');
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("HTTP method token must not be empty.", nameof(token));
        }

        for (var i = 0; i < trimmed.Length; i++)
        {
            if (!HeaderSyntax.IsTokenChar(trimmed[i]))
            {
                throw new ArgumentException(
                    $"HTTP method token contains the invalid character {HeaderSyntax.CodePoint(trimmed[i])} at index {i}; "
                    + "a method must be an RFC 9110 token.",
                    nameof(token));
            }
        }

        return WellKnown(trimmed) ?? new Method(trimmed);
    }

    /// <summary>Returns <see cref="Name"/>.</summary>
    /// <returns>The wire token.</returns>
    public override string ToString() => Name;

    /// <summary>True when the method is idempotent (the single source is <c>RetryFacts</c>; HTTP-9).</summary>
    internal bool IsIdempotent => RetryFacts.IdempotentMethods.Contains(this);

    /// <summary>True for exactly GET, HEAD, TRACE and CONNECT, which a request may not carry a body for (HTTP-7).</summary>
    internal bool ForbidsBody => Name is "GET" or "HEAD" or "TRACE" or "CONNECT";

    private static Method? WellKnown(string token)
    {
        foreach (var known in s_wellKnown)
        {
            if (Ascii.EqualsIgnoreCase(token, known.Name))
            {
                return known;
            }
        }

        return null;
    }

    private static readonly Method[] s_wellKnown = [Get, Head, Post, Put, Patch, Delete, Options, Trace, Connect];
}
