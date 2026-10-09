// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>The two response handlers core ships (SERDE-27, SERDE-28), as factories over an <see cref="ISerde"/> (P7a-16).</summary>
/// <remarks>
/// The implementations are internal: a generated SDK that needs different behaviour implements
/// <see cref="IResponseHandler{T}"/> itself. Both handlers dispose the response they are given on every path.
/// </remarks>
public static class ResponseHandlers
{
    /// <summary>
    /// A handler that decodes the body as <typeparamref name="T"/> whatever the status (SERDE-27): it streams, closes the
    /// response on every path, names <typeparamref name="T"/> when the body is missing or is a wire <c>null</c>, chains a
    /// codec failure, and lets an <see cref="IOException"/> through.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="serde">The codec.</param>
    /// <returns>The handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serde"/> is <see langword="null"/>.</exception>
    public static IResponseHandler<T> Deserialize<T>(ISerde serde)
    {
        ArgumentNullException.ThrowIfNull(serde);
        return new DeserializingHandler<T>(serde);
    }

    /// <summary>
    /// A status-aware handler (SERDE-28): a 2xx response is decoded as <typeparamref name="T"/> exactly as
    /// <see cref="Deserialize{T}"/> does; a 400 to 599 response throws <see cref="Errors.HttpResponseException"/> over a bounded
    /// buffered copy of the error body (the live response is released); anything else (a 1xx, an unfollowed 3xx, a 304) is
    /// disposed and fails with a <see cref="Errors.DeserializationException"/> that leads with the status code and carries the
    /// <c>ETag</c> and the redacted <c>Location</c> when present.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="serde">The codec.</param>
    /// <returns>The handler.</returns>
    /// <remarks>
    /// The error branch is the same capture <see cref="Http.Response.Response.EnsureSuccessAsync"/> uses, with the same 1 MiB bound
    /// (<see cref="Http.Response.Response.MaxBufferedErrorBytes"/>). The <c>Location</c> in the third branch's message goes through
    /// <c>UrlRedactor</c>, because an exception message is logged and a redirect target can carry a credential.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="serde"/> is <see langword="null"/>.</exception>
    public static IResponseHandler<T> DeserializeOnSuccess<T>(ISerde serde)
    {
        ArgumentNullException.ThrowIfNull(serde);
        return new SuccessDeserializingHandler<T>(serde);
    }
}
