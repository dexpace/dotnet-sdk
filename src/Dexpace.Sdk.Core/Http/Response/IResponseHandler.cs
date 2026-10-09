// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// Turns a received <see cref="Response"/> into a value of type <typeparamref name="T"/>, and releases the response (P7a-16).
/// </summary>
/// <typeparam name="T">The value the handler produces.</typeparam>
/// <remarks>
/// <para>
/// This is the seam a generated SDK implements to supply its own typed-error handling; core ships two implementations,
/// built by <see cref="Serialization.ResponseHandlers"/>. The handler <b>owns</b> the response from the moment
/// it is called: it disposes it on every path, so the caller never does. It is asynchronous only: a typed response is lazy
/// and parses once (<see cref="TypedResponse{T}"/>), and the synchronous reader is
/// <see cref="Serialization.ResponseBodySerdeExtensions.ReadValue{T}"/>.
/// </para>
/// <para>
/// A handler throws for a failure, and its exception is memoized by <see cref="TypedResponse{T}"/>: it is the same object on
/// every later access.
/// </para>
/// </remarks>
public interface IResponseHandler<T>
{
    /// <summary>Handles <paramref name="response"/>, returning the value and disposing the response.</summary>
    /// <param name="response">The received response. Owned, and disposed, by this call.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>.</exception>
    ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken);
}
