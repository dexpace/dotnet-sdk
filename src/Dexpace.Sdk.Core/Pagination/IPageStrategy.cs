// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// Turns one fetched page into its items and the request for the next page (PAGE-4, PAGE-5; design P7c-2).
/// </summary>
/// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
/// <typeparam name="T">The item type.</typeparam>
/// <remarks>
/// <para>
/// The engine reads the response body once, deserializes it into <typeparamref name="TPage"/> through the
/// <c>ISerde</c> and hands the strategy that value: a strategy never reads a body, so the single-read rule
/// (PAGE-16) is the engine's, and one strategy object serves the async and the blocking engine alike. The built-ins
/// are in <see cref="PaginationStrategies"/>; <c>PaginationStrategies.Create</c> adapts two delegates.
/// </para>
/// <para>
/// <b>Contract.</b> <see cref="Parse"/> is synchronous, pure and holds no per-walk state (PAGE-5): the same instance may
/// be used by two concurrent walks. It reads the response's status, headers and <c>response.Request.Url</c> (the
/// <em>executed</em> request, post-redirect, with whatever the authorization policies stamped on it), and builds the
/// next request from <c>first</c> (the template), never from <c>response.Request</c>, so no stamped credential is carried forward
/// (P7c-8). The response's body is already read and is closed as soon as <see cref="Parse"/> returns. Returning
/// <see langword="null"/> is a contract violation (<see cref="InvalidOperationException"/>, PAGE-4); to end the stream
/// return a <see cref="PageInfo{T}"/> whose <see cref="PageInfo{T}.NextRequest"/> is <see langword="null"/>. An
/// exception from <see cref="Parse"/> surfaces from the walk after the response is closed (PAGE-13).
/// </para>
/// </remarks>
public interface IPageStrategy<in TPage, T>
{
    /// <summary>Parses one page.</summary>
    /// <param name="page">The deserialized envelope; never <see langword="null"/>.</param>
    /// <param name="response">The response, with its body already read; read status, headers and request URL only.</param>
    /// <param name="first">
    /// The walk's first request, the <em>template</em> the next request is built from (method, headers and body are kept).
    /// Named <c>first</c> because CA1716 reserves <c>template</c> on an interface member.
    /// </param>
    /// <returns>The page's items and the next request; never <see langword="null"/>.</returns>
    PageInfo<T> Parse(TPage page, Response response, Request first);
}
