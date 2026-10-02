// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// An immutable HTTP request the SDK hands to a transport (HTTP-1, HTTP-4, HTTP-6, HTTP-7, HTTP-46, HTTP-47).
/// </summary>
/// <remarks>
/// A request carries exactly a <see cref="Method"/>, a <see cref="Url"/>, <see cref="Headers"/> and an optional
/// <see cref="Body"/> (HTTP-6). Instances are immutable and safe to share across threads; the <see cref="Body"/>, when
/// present, may carry single-use stream state (see <see cref="RequestBody"/>). Every way to make a request, the
/// constructor, <see cref="Create"/> and every <c>With*</c> derivation, goes through the one validating constructor
/// (HTTP-2): a missing field is an <see cref="ArgumentNullException"/> (HTTP-4), a body on GET, HEAD, TRACE or CONNECT
/// is an <see cref="ArgumentException"/> (HTTP-7), and a URL that is not absolute <c>http</c> or <c>https</c> is an
/// <see cref="ArgumentException"/> carrying the URL as <see cref="UrlRedactor"/> renders it (HTTP-47).
/// <para>
/// <b>Equality (HTTP-46).</b> Two requests are equal when their method, <see cref="Uri.AbsoluteUri"/> (compared
/// ordinally, so userinfo and fragment count and no DNS runs), headers (by value) and bodies are equal. Bodies compare
/// by value where their bytes are a construction-time fact (bytes, string and replayable bodies) and by identity for a
/// single-use stream body or an unknown <see cref="RequestBody"/> subclass; see <see cref="RequestBody"/>.
/// </para>
/// <para>
/// <b>Breaking (phase 2a):</b> <see cref="Method"/>, <see cref="Url"/>, <see cref="Headers"/> and <see cref="Body"/>
/// lost their <see langword="init"/> accessors, so <c>request with { Url = u }</c> no longer compiles: use
/// <see cref="WithMethod"/>, <see cref="WithUrl"/>, <see cref="WithHeaders"/>, <see cref="WithBody"/> and
/// <see cref="WithoutBody"/>. A request now rejects a body on GET, HEAD, TRACE and CONNECT, equality compares bodies
/// by value where in-memory, and <see cref="ToString"/> prints the method and the redacted URL.
/// </para>
/// </remarks>
public sealed record Request
{
    /// <summary>Creates a request. Prefer <see cref="Create"/> or the per-method factories.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The fully-resolved, absolute <c>http</c> or <c>https</c> target URL.</param>
    /// <param name="headers">The request headers (defaults to empty).</param>
    /// <param name="body">The request body, or <see langword="null"/>; not allowed for GET, HEAD, TRACE or CONNECT.</param>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> or <paramref name="url"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="body"/> is set for a method that forbids one (<c>ParamName</c> <c>body</c>), or
    /// <paramref name="url"/> is not an absolute <c>http</c> or <c>https</c> URI (<c>ParamName</c> <c>url</c>; the
    /// message carries the redacted input).
    /// </exception>
    public Request(Method method, Uri url, Headers? headers = null, RequestBody? body = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(url);
        if (method.ForbidsBody && body is not null)
        {
            throw new ArgumentException(
                $"A {method.Name} request must not carry a body; clear the body first (WithoutBody()) before using this method.",
                nameof(body));
        }

        if (!IsHttpUrl(url))
        {
            throw UrlError(url.OriginalString, nameof(url));
        }

        Method = method;
        Url = url;
        Headers = headers ?? Headers.Empty;
        Body = body;
    }

    /// <summary>The HTTP method on the wire.</summary>
    /// <remarks><b>Breaking:</b> was <c>init</c>; derive with <see cref="WithMethod"/>.</remarks>
    public Method Method { get; }

    /// <summary>The fully-resolved, absolute <c>http</c> or <c>https</c> target URL.</summary>
    /// <remarks><b>Breaking:</b> was <c>init</c>; derive with <see cref="WithUrl"/>.</remarks>
    public Uri Url { get; }

    /// <summary>The request headers; may be empty but never <see langword="null"/>.</summary>
    /// <remarks><b>Breaking:</b> was <c>init</c>; derive with <see cref="WithHeaders"/> or <c>WithHeader</c>.</remarks>
    public Headers Headers { get; }

    /// <summary>The request body, or <see langword="null"/> for methods without a payload.</summary>
    /// <remarks><b>Breaking:</b> was <c>init</c>; derive with <see cref="WithBody"/> or <see cref="WithoutBody"/>.</remarks>
    public RequestBody? Body { get; }

    /// <summary>Creates a request from a method and a string URL.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The absolute <c>http</c> or <c>https</c> target URL.</param>
    /// <param name="headers">The request headers, or <see langword="null"/>.</param>
    /// <param name="body">The request body, or <see langword="null"/>.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> or <paramref name="url"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is malformed, relative, or not <c>http</c> or <c>https</c> (including the Linux
    /// <c>file:</c> reading of <c>"/rel"</c>); the message carries the input as <see cref="UrlRedactor"/> renders it.
    /// Also thrown when <paramref name="body"/> is set for a method that forbids one.
    /// </exception>
    public static Request Create(Method method, string url, Headers? headers = null, RequestBody? body = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw UrlError(url, nameof(url));
        }

        return new Request(method, uri, headers, body);
    }

    /// <summary>Creates a <c>GET</c> request for <paramref name="url"/>.</summary>
    /// <param name="url">The absolute target URL.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    public static Request Get(string url) => Create(Method.Get, url);

    /// <summary>Creates a <c>POST</c> request for <paramref name="url"/> with the given body.</summary>
    /// <param name="url">The absolute target URL.</param>
    /// <param name="body">The request body.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    public static Request Post(string url, RequestBody body) =>
        Create(Method.Post, url, body: body);

    /// <summary>Returns a copy with the given method.</summary>
    /// <param name="method">The replacement method.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    /// <exception cref="ArgumentException">
    /// The current body is not allowed for <paramref name="method"/>; clear it first with <see cref="WithoutBody"/>.
    /// </exception>
    public Request WithMethod(Method method) => new(method, Url, Headers, Body);

    /// <summary>Returns a copy with the given URL, validated like the constructor's.</summary>
    /// <param name="url">The replacement absolute <c>http</c> or <c>https</c> URL.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute <c>http</c> or <c>https</c> URI.</exception>
    public Request WithUrl(Uri url) => new(Method, url, Headers, Body);

    /// <summary>Returns a copy with the given headers, replacing the current ones.</summary>
    /// <param name="headers">The replacement headers.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    public Request WithHeaders(Headers headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return new Request(Method, Url, headers, Body);
    }

    /// <summary>Returns a copy with <paramref name="value"/> appended under <paramref name="name"/>.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    public Request WithHeader(string name, string value) => WithHeaders(Headers.With(name, value));

    /// <summary>Returns a copy with <paramref name="value"/> appended under the typed <paramref name="name"/>.</summary>
    /// <param name="name">The typed header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> holds a character outside HTAB and printable ASCII.</exception>
    public Request WithHeader(HttpHeaderName name, string value) => WithHeaders(Headers.With(name, value));

    /// <summary>Returns a copy with the given body.</summary>
    /// <param name="body">The replacement body.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    /// <exception cref="ArgumentException">The method forbids a body (GET, HEAD, TRACE, CONNECT).</exception>
    public Request WithBody(RequestBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new Request(Method, Url, Headers, body);
    }

    /// <summary>Returns a copy with no body, or this instance when there is none.</summary>
    /// <returns>A <see cref="Request"/> whose <see cref="Body"/> is <see langword="null"/>.</returns>
    public Request WithoutBody() => Body is null ? this : new Request(Method, Url, Headers, null);

    /// <summary>
    /// Equality over <see cref="Method"/>, <see cref="Uri.AbsoluteUri"/> (ordinal), <see cref="Headers"/> (by value) and
    /// <see cref="Body"/> (by value where in-memory, otherwise by identity) (HTTP-46).
    /// </summary>
    /// <remarks>
    /// <b>Breaking (behaviour):</b> bodies compare by value where in-memory (they compared by reference), and the URL
    /// compares by its absolute text ordinally.
    /// </remarks>
    /// <param name="other">The request to compare with.</param>
    /// <returns><see langword="true"/> when all four parts are equal.</returns>
    public bool Equals(Request? other) =>
        other is not null
        && (ReferenceEquals(this, other)
            || (Method.Equals(other.Method)
                && string.Equals(Url.AbsoluteUri, other.Url.AbsoluteUri, StringComparison.Ordinal)
                && Headers.Equals(other.Headers)
                && EqualityComparer<RequestBody?>.Default.Equals(Body, other.Body)));

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Method, Url.AbsoluteUri.GetHashCode(StringComparison.Ordinal), Headers, Body);

    /// <summary>
    /// The method and the URL as <see cref="UrlRedactor"/> renders it (<c>GET https://***:***@h/p?token=***</c>);
    /// never the headers or the body.
    /// </summary>
    /// <remarks>
    /// The record-generated form would print the raw URL, userinfo and query tokens included, so it is overridden.
    /// </remarks>
    /// <returns>The log-safe description.</returns>
    public override string ToString() => $"{Method} {UrlRedactor.Default.Redact(Url)}";

    private static bool IsHttpUrl(Uri url) =>
        url.IsAbsoluteUri
        && (url.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    // HTTP-47 (ruled P2a-2): the input goes through the shared redactor, so a password or query token never lands in
    // an exception message; an unparseable input becomes the redactor's sentinel.
    private static ArgumentException UrlError(string input, string paramName) =>
        new(
            $"Request URL must be an absolute http or https URL, but was '{UrlRedactor.Default.Redact(input)}'.",
            paramName);
}
