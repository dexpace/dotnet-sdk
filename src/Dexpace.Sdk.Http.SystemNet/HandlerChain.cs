// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// Reads a caller's handler chain without changing it (<c>TRANSPORT-1</c>, <c>TRANSPORT-15</c>; phase 8b, P8b-3): the SDK's
/// redirect policy is the only redirect authority, so a handler that follows redirects itself is refused up front.
/// </summary>
internal static class HandlerChain
{
    /// <summary>
    /// Walks <paramref name="handler"/> and its <see cref="DelegatingHandler.InnerHandler"/>s to the primary handler and throws when
    /// that primary is a <see cref="SocketsHttpHandler"/> or <see cref="HttpClientHandler"/> whose <c>AllowAutoRedirect</c> is
    /// <see langword="true"/> (both default to it).
    /// </summary>
    /// <remarks>
    /// An unknown primary (a platform handler, a test double) is accepted: inspecting it would take reflection, which
    /// <c>IsAotCompatible</c> forbids, and the transport's last-line check of the final request URI still applies. A chain whose
    /// innermost <see cref="DelegatingHandler"/> has no inner handler yet is accepted too (<c>IHttpClientFactory</c> assigns it
    /// later; <c>HttpClient</c> fails its first send by itself if none ever arrives). The handler is never mutated: flipping
    /// <c>AllowAutoRedirect</c> would break <c>TRANSPORT-15</c>'s "a caller's client MUST NOT be mutated".
    /// </remarks>
    /// <param name="handler">The outermost handler of the caller's chain.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The primary handler follows redirects.</exception>
    internal static void ThrowIfFollowsRedirects(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var current = handler;
        while (current is DelegatingHandler { InnerHandler: { } inner })
        {
            current = inner;
        }

        if (current is SocketsHttpHandler { AllowAutoRedirect: true } or HttpClientHandler { AllowAutoRedirect: true })
        {
            throw new ArgumentException(
                "The handler's primary " + current.GetType().Name + " follows redirects (AllowAutoRedirect is true, its default), "
                + "so a credential could cross an origin before the SDK's redirect policy saw the 3xx. Set AllowAutoRedirect = false "
                + "on it; the SDK is the only redirect authority (TRANSPORT-1).",
                nameof(handler));
        }
    }
}
