// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// An immutable HTTP request the SDK hands to a transport.
/// </summary>
/// <remarks>
/// Instances are immutable and safe to share across threads; the <see cref="Body"/>, when
/// present, may carry single-use stream state (see <see cref="RequestBody"/>). Use the
/// <c>With*</c> helpers or C# <c>with</c> expressions for non-destructive mutation. The
/// <see cref="Url"/> is compared by value as a <see cref="Uri"/>; no DNS resolution is performed
/// during equality.
/// </remarks>
public sealed record Request
{
    /// <summary>Creates a request. Prefer <see cref="Create"/> or the per-method factories.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The fully-resolved, absolute target URL.</param>
    /// <param name="headers">The request headers (defaults to empty).</param>
    /// <param name="body">The request body, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is not an absolute <c>http</c> or <c>https</c> URI.
    /// </exception>
    public Request(Method method, Uri url, Headers? headers = null, RequestBody? body = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("Request URL must be an absolute URI.", nameof(url));
        }

        if (!url.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Request URL scheme must be http or https, but was '{url.Scheme}'.", nameof(url));
        }

        Method = method;
        Url = url;
        Headers = headers ?? Headers.Empty;
        Body = body;
    }

    /// <summary>The HTTP method on the wire.</summary>
    public Method Method { get; init; }

    /// <summary>The fully-resolved, absolute target URL.</summary>
    public Uri Url { get; init; }

    /// <summary>The request headers; may be empty but never <see langword="null"/>.</summary>
    public Headers Headers { get; init; }

    /// <summary>The request body, or <see langword="null"/> for methods without a payload.</summary>
    public RequestBody? Body { get; init; }

    /// <summary>Creates a request from a method and a string URL.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The absolute target URL.</param>
    /// <param name="headers">The request headers, or <see langword="null"/>.</param>
    /// <param name="body">The request body, or <see langword="null"/>.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute URI.</exception>
    public static Request Create(Method method, string url, Headers? headers = null, RequestBody? body = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Request URL must be an absolute URI.", nameof(url));
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

    /// <summary>Returns a copy with <paramref name="value"/> appended under <paramref name="name"/>.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    public Request WithHeader(string name, string value) => this with { Headers = Headers.With(name, value) };

    /// <summary>Returns a copy with the given body.</summary>
    /// <param name="body">The replacement body.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    public Request WithBody(RequestBody body) => this with { Body = body };
}
