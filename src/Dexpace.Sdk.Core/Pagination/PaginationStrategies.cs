// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The built-in <see cref="IPageStrategy{TPage,T}"/> factories for <see cref="Pageable.Create{TPage,T}"/>: cursor,
/// page number and <c>Link</c> header, plus <see cref="Create{TPage,T}"/> for two delegates (PAGE-4, PAGE-5, PAGE-16 to
/// PAGE-20).
/// </summary>
/// <remarks>
/// <para>
/// Each factory returns a strategy that holds only its configuration, so one instance serves any number of concurrent
/// walks (PAGE-5), reads no body (the engine reads it once) and builds the next request from the walk's first request,
/// keeping its method, headers and body (PAGE-23, P7c-8). Query handling is the internal splice (PAGE-21 to PAGE-24), which
/// is not public in v1 (P7c-9): a custom strategy uses <see cref="Request.WithUrl"/> and its own query handling, or
/// <see cref="Create{TPage,T}"/> over a cursor it computes.
/// </para>
/// <para>
/// <b>Breaking:</b> the factories used to return a <c>nextRequest</c> delegate and took no item selector. They now return
/// an <see cref="IPageStrategy{TPage,T}"/> and take the selector, so they gain a second type parameter (P7c-2). Cursor's
/// parameter name defaults to <c>cursor</c>. <see cref="PageNumber{TPage,T}"/> lost <c>hasMore</c> and gained
/// <c>startPage</c>. <see cref="LinkHeader{TPage,T}"/> lost <c>rel</c> and gained <c>headerName</c> and
/// <c>allowCrossOrigin</c>. The splice matches parameter names case-sensitively and drops duplicate occurrences of the
/// parameter it sets.
/// </para>
/// </remarks>
public static class PaginationStrategies
{
    // ── the IPageStrategy factories (PAGE-4, PAGE-5, PAGE-16 to PAGE-20; design P7c-2) ────────────────

    /// <summary>
    /// Adapts two delegates, the <c>selectItems</c> and <c>nextRequest</c> shape of design section 7.1, to an
    /// <see cref="IPageStrategy{TPage,T}"/> (PAGE-4, PAGE-5).
    /// </summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">Extracts the ordered item list from a deserialized envelope.</param>
    /// <param name="nextRequest">
    /// Given the envelope, the response (its body already read; read status, headers and <c>response.Request.Url</c>
    /// only) and the walk's first request, returns the next request, or <see langword="null"/> to end the stream.
    /// Build the next request from the first request, not from <c>response.Request</c>, which carries whatever the
    /// authorization policies stamped (P7c-8).
    /// </param>
    /// <returns>A strategy holding only the two delegates.</returns>
    /// <remarks>
    /// This is the migration path for the old two-delegate <c>Pageable.Create</c> and for a <c>hasMore</c>
    /// page-number predicate: <c>nextRequest</c> may inspect the envelope and stop early.
    /// </remarks>
    public static IPageStrategy<TPage, T> Create<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items,
        Func<TPage, Response, Request, Request?> nextRequest)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(nextRequest);
        return new DelegateStrategy<TPage, T>(items, nextRequest);
    }

    /// <summary>Builds the cursor strategy (PAGE-16): the envelope carries the cursor for the next page.</summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">Extracts the ordered item list from a deserialized envelope.</param>
    /// <param name="nextCursor">
    /// Extracts the continuation cursor from the envelope; <see langword="null"/> or empty ends the stream.
    /// </param>
    /// <param name="queryParameter">
    /// The query parameter set to the cursor on the next request (replacing an existing one in place); <c>cursor</c> by default.
    /// </param>
    /// <returns>A strategy holding only its configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="queryParameter"/> is empty or holds a lone surrogate.</exception>
    public static IPageStrategy<TPage, T> Cursor<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items,
        Func<TPage, string?> nextCursor,
        string queryParameter = "cursor")
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(nextCursor);
        RequireQueryParameter(queryParameter);
        return new CursorStrategy<TPage, T>(items, nextCursor, queryParameter);
    }

    /// <summary>Builds the page-number strategy (PAGE-17).</summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">Extracts the ordered item list from a deserialized envelope; an empty list ends the stream.</param>
    /// <param name="queryParameter">The query parameter that carries the page number; <c>page</c> by default.</param>
    /// <param name="startPage">
    /// The page number assumed when the executed request's URL carries none or a value that is not ASCII digits;
    /// <c>1</c> by default, <c>0</c> for a zero-based server.
    /// </param>
    /// <returns>A strategy holding only its configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="queryParameter"/> is empty or holds a lone surrogate.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="startPage"/> is negative.</exception>
    /// <remarks>
    /// <para>
    /// The current page is read from the <em>executed</em> request, parsed with the invariant culture and no sign
    /// or whitespace (a Unicode digit is not a digit), and the next request carries the current number plus one; a
    /// page that is already <see cref="int.MaxValue"/> ends the stream (P7c-11).
    /// </para>
    /// <para>
    /// <b>Breaking:</b> the old factory took a <c>hasMore</c> predicate and stopped on it. This one ends only on an empty
    /// page, so a non-empty last page now costs one more (empty) exchange. Use <see cref="Create{TPage,T}"/> for a
    /// predicate.
    /// </para>
    /// </remarks>
    public static IPageStrategy<TPage, T> PageNumber<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items,
        string queryParameter = "page",
        int startPage = 1)
    {
        ArgumentNullException.ThrowIfNull(items);
        RequireQueryParameter(queryParameter);
        ArgumentOutOfRangeException.ThrowIfNegative(startPage);
        return new PageNumberStrategy<TPage, T>(items, queryParameter, startPage);
    }

    /// <summary>Builds the <c>Link</c>-header strategy (PAGE-18 to PAGE-20).</summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">Extracts the ordered item list from a deserialized envelope.</param>
    /// <param name="headerName">The header to read; <c>Link</c> by default. Every instance of it is read.</param>
    /// <param name="allowCrossOrigin">
    /// Whether a <c>next</c> target on another origin than the first request may be followed. <see langword="false"/>
    /// by default: the walk ends there. Setting it is a security decision, because each page is a fresh pipeline call and
    /// the authorization policies stamp the credential for any origin the call's request is same-origin with.
    /// </param>
    /// <returns>A strategy holding only its configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="headerName"/> is empty or holds a lone surrogate.</exception>
    /// <remarks>
    /// <para>
    /// Every instance of the header is read and joined, the first <c>rel=next</c> link-value wins (RFC 8288: quoted or
    /// unquoted <c>rel</c>, a space- or tab-separated token list, case-insensitive), and its target is resolved against the
    /// page's <em>response</em> URL, so a first page that redirected still resolves relative links correctly (PAGE-19).
    /// </para>
    /// <para>
    /// <b>The walk ends, quietly, instead of following</b> a missing header, a missing <c>next</c>, a target holding a space,
    /// control character, <c>&lt;</c>, <c>&gt;</c> or <c>"</c>, a scheme other than <c>http</c> or <c>https</c>, and a target
    /// whose origin is not the first request's, a scheme downgrade included. Userinfo in a target is removed. A rejected
    /// target is silent (strategies have no logger and PAGE-19 asks for an end, not an error), which can hide a
    /// misconfigured server.
    /// </para>
    /// <para>
    /// <b>Why the origin guard (P7c-12).</b> Each page is a fresh pipeline call, and the authorization policies stamp a
    /// credential when a request is same-origin with <em>its own call's</em> first request. Without the guard a hostile or
    /// compromised server could harvest the bearer token with one <c>Link</c> header naming its own host. The origin compared
    /// is the first request's, not the response's, so a first page that redirected to another origin cannot launder it.
    /// Pass <paramref name="allowCrossOrigin"/> only for an API that pages across hosts (a CDN or a regional host), and
    /// know that per-call authorization will then stamp the credential for the new origin.
    /// </para>
    /// <para>
    /// <b>Breaking:</b> a cross-origin target, a target that is not a URL (<c>not a url</c> used to resolve as a relative
    /// path) and a userinfo-bearing target (now followed with the userinfo removed) behave differently; every
    /// <c>Link</c> instance is read, not only the first; the base is the response URL, not the current request.
    /// </para>
    /// </remarks>
    public static IPageStrategy<TPage, T> LinkHeader<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items,
        string headerName = "Link",
        bool allowCrossOrigin = false)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrEmpty(headerName);
        QuerySplice.RejectLoneSurrogate(headerName, nameof(headerName), "The header name");
        return new LinkHeaderStrategy<TPage, T>(items, headerName, allowCrossOrigin);
    }

    // Fails at construction, not on the first server cursor (P7c-10).
    private static void RequireQueryParameter(string queryParameter)
    {
        ArgumentException.ThrowIfNullOrEmpty(queryParameter);
        QuerySplice.RejectLoneSurrogate(queryParameter, nameof(queryParameter), "The query parameter name");
    }
}
