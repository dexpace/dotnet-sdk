// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// Factory methods for creating <see cref="AsyncPageable{T}"/> instances.
/// </summary>
public static class Pageable
{
    /// <summary>
    /// Creates an <see cref="AsyncPageable{T}"/> that fetches pages through <paramref name="client"/>, starting with
    /// <paramref name="first"/> and following <paramref name="strategy"/> (PAGE-1, PAGE-4, PAGE-36).
    /// </summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type extracted from each page.</typeparam>
    /// <param name="client">
    /// The transport seam used for every page. An <c>HttpPipeline</c> converts to it, so retry, redirect and
    /// authorization govern every page; a bare transport or <c>DelegateHttpClient</c> works too. The pageable never
    /// disposes it.
    /// </param>
    /// <param name="first">
    /// The initial request, which is also the template every next request is built from. Every page re-sends its body, so
    /// the body must be replayable.
    /// </param>
    /// <param name="serde">The serde that deserializes each page into <typeparamref name="TPage"/>.</param>
    /// <param name="strategy">Turns each page into its items and the next request; see <see cref="PaginationStrategies"/>.</param>
    /// <param name="options">
    /// Per-call overrides (timeout, retry budget, tags), passed as the same instance to every page's exchange; omitted
    /// means <see cref="RequestOptions.Empty"/> (PAGE-36).
    /// </param>
    /// <param name="maxPages">
    /// The maximum number of exchanges, or <see langword="null"/> for no limit. Set a finite cap in production (PAGE-9,
    /// PAGE-10).
    /// </param>
    /// <returns>A lazy pageable: nothing is sent until the first <c>MoveNextAsync</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/>, <paramref name="first"/>, <paramref name="serde"/> or <paramref name="strategy"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPages"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="first"/> has a body that cannot be written twice (call <c>ToReplayableAsync</c> first).</exception>
    /// <remarks>
    /// <para>
    /// <b>Breaking:</b> this took an <c>HttpPipeline</c>, a <c>DexpaceClientOptions</c> and the two delegates
    /// <c>selectItems</c> and <c>nextRequest</c>. It now takes the transport seam (an <c>HttpPipeline</c> still converts),
    /// an <see cref="IPageStrategy{TPage,T}"/> (<see cref="PaginationStrategies.Create{TPage,T}"/> wraps the old delegates)
    /// and <see cref="RequestOptions"/> (a caller who passed different client options builds a pipeline with them)
    /// (P7c-2, P7c-5, P7c-6).
    /// </para>
    /// <para>
    /// <b>Breaking:</b> <paramref name="maxPages"/> of zero or less, and a template body that cannot be replayed, throw here
    /// instead of misbehaving later. A <see langword="null"/> envelope throws
    /// <see cref="Errors.DeserializationException"/> naming <typeparamref name="TPage"/>, where it used to throw
    /// <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    public static AsyncPageable<T> Create<TPage, T>(
        IAsyncHttpClient client,
        Request first,
        ISerde serde,
        IPageStrategy<TPage, T> strategy,
        RequestOptions? options = null,
        int? maxPages = null) =>
        new StrategyPageable<TPage, T>(PageWalk<TPage, T>.ForAsync(client, first, serde, strategy, options, maxPages));

    /// <summary>
    /// Creates a <see cref="Pageable{T}"/>, the blocking twin of <see cref="Create{TPage,T}"/>, that fetches pages through
    /// the synchronous <paramref name="client"/> (PAGE-1, PAGE-6, PAGE-9, PAGE-14, PAGE-31, PAGE-36).
    /// </summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type extracted from each page.</typeparam>
    /// <param name="client">
    /// The synchronous transport seam. An <c>HttpPipeline</c> converts to it and drives its real synchronous path. The
    /// pageable never disposes it.
    /// </param>
    /// <param name="first">The initial request and the template for every next request; its body must be replayable.</param>
    /// <param name="serde">The serde that deserializes each page into <typeparamref name="TPage"/>.</param>
    /// <param name="strategy">Turns each page into its items and the next request; see <see cref="PaginationStrategies"/>.</param>
    /// <param name="options">Per-call overrides, passed as the same instance to every page; omitted means <see cref="RequestOptions.Empty"/>.</param>
    /// <param name="maxPages">The maximum number of exchanges, or <see langword="null"/> for no limit; set a finite cap in production.</param>
    /// <param name="cancellationToken">
    /// A token captured for the whole pageable (an iterator over <see cref="IEnumerable{T}"/> has no per-enumeration token): it
    /// is observed before each page and passed to every exchange.
    /// </param>
    /// <returns>A lazy pageable: nothing is sent until the first <c>MoveNext</c>.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPages"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="first"/> has a body that cannot be written twice.</exception>
    /// <remarks>
    /// <para>
    /// <b>Why a different name from <see cref="Create{TPage,T}"/>.</b> An <c>HttpPipeline</c> converts to both
    /// <see cref="IAsyncHttpClient"/> and <see cref="IHttpClient"/>, so two <c>Create</c> overloads that differ only in that
    /// parameter would be ambiguous (<c>CS0121</c>) for the most common caller. The name matches the SDK's
    /// <c>AsBlocking</c> vocabulary.
    /// </para>
    /// <para>
    /// The blocking pager is honest above the transport and inherits <c>PIPE-28</c>'s gap below it. It reads each page
    /// through <c>ResponseBody.OpenRead</c>, which <c>SystemNetHttpClient</c>'s response body implements (phase 7b), so it
    /// works over that transport, directly or through an <c>HttpPipeline</c>; the transport's <c>Execute</c> still blocks on
    /// its async send until phase 8b. Over a transport whose bodies do not override <c>OpenRead</c>, the first
    /// <c>MoveNext</c> throws <see cref="NotSupportedException"/>; use <see cref="Create{TPage,T}"/> there. Each page
    /// envelope is buffered under the 64 MiB materialisation cap.
    /// </para>
    /// </remarks>
    public static Pageable<T> CreateBlocking<TPage, T>(
        IHttpClient client,
        Request first,
        ISerde serde,
        IPageStrategy<TPage, T> strategy,
        RequestOptions? options = null,
        int? maxPages = null,
        CancellationToken cancellationToken = default) =>
        new StrategyBlockingPageable<TPage, T>(
            PageWalk<TPage, T>.ForBlocking(client, first, serde, strategy, options, maxPages),
            cancellationToken);

    /// <summary>
    /// Creates an <see cref="AsyncPageable{T}"/> driven by caller-supplied fetchers instead of a strategy (PAGE-34, PAGE-35).
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="firstPage">
    /// Called exactly once per walk with the walk's <see cref="PagingOptions"/>; return <see langword="null"/> for an empty
    /// stream.
    /// </param>
    /// <param name="nextPage">
    /// Called with the previous page's next link when it is not blank, else its continuation token, and the same
    /// <see cref="PagingOptions"/>; return <see langword="null"/> to end the stream.
    /// </param>
    /// <param name="maxPages">The maximum number of pages delivered, or <see langword="null"/> for no limit; set a finite cap in production.</param>
    /// <returns>A lazy pageable: no fetcher runs until the first <c>MoveNextAsync</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="firstPage"/> or <paramref name="nextPage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPages"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// <b>Ownership (P7c-15).</b> Each fetcher reads its own response, builds a materialized <see cref="Page{T}"/> and
    /// disposes the response itself, typically with <c>await using</c>; the engine never sees a response (see
    /// <see cref="FetchedPage{T}"/>). A fetcher that throws propagates unwrapped.
    /// </para>
    /// <para>
    /// <b>One <see cref="PagingOptions"/> per walk</b>, shared by every fetcher call of that walk and fresh for the next
    /// enumeration; the page view is single-use like <see cref="AsyncPageable{T}.AsPages"/>. Fetchers are asynchronous only:
    /// a blocking fetcher front-end is not built (a <see cref="Pageable{T}"/> subclass covers it).
    /// </para>
    /// </remarks>
    public static AsyncPageable<T> FromFetchers<T>(
        Func<PagingOptions, CancellationToken, ValueTask<FetchedPage<T>?>> firstPage,
        Func<string, PagingOptions, CancellationToken, ValueTask<FetchedPage<T>?>> nextPage,
        int? maxPages = null)
    {
        ArgumentNullException.ThrowIfNull(firstPage);
        ArgumentNullException.ThrowIfNull(nextPage);
        if (maxPages is { } cap)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cap, nameof(maxPages));
        }

        return new FetcherPageable<T>(firstPage, nextPage, maxPages);
    }
}
