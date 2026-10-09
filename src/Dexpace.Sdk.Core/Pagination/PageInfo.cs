// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// What an <see cref="IPageStrategy{TPage,T}"/> makes of one fetched page: its items and the request for the next
/// page (PAGE-4).
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <remarks>
/// <see cref="NextRequest"/> being <see langword="null"/> is the <em>only</em> end-of-stream signal: an empty item list
/// with a non-null next request fetches again (P7c-2). A sealed class and not a record, because value equality over an
/// <see cref="IReadOnlyList{T}"/> would be reference equality and mislead (P7c-19).
/// </remarks>
public sealed class PageInfo<T>
{
    /// <summary>Creates the result of parsing one page.</summary>
    /// <param name="items">The page's items; never <see langword="null"/> (PAGE-2).</param>
    /// <param name="nextRequest">The request for the next page, or <see langword="null"/> to end the stream.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public PageInfo(IReadOnlyList<T> items, Request? nextRequest)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
        NextRequest = nextRequest;
    }

    /// <summary>The page's items, in server order.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>The request for the next page; <see langword="null"/> ends the stream (PAGE-4).</summary>
    public Request? NextRequest { get; }
}
