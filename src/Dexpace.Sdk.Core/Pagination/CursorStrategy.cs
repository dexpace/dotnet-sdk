// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// Cursor pagination (PAGE-16): the envelope carries the cursor, a <see langword="null"/> or empty cursor ends the
/// stream, and the next request is the first request with the cursor spliced into the configured query parameter. It
/// holds only its configuration (PAGE-5) and reads the body zero times: the engine did, once.
/// </summary>
/// <typeparam name="TPage">The envelope type.</typeparam>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="items">Extracts the ordered items from the envelope.</param>
/// <param name="nextCursor">Extracts the continuation cursor from the envelope.</param>
/// <param name="queryParameter">The query parameter that carries the cursor on the next request.</param>
internal sealed class CursorStrategy<TPage, T>(
    Func<TPage, IReadOnlyList<T>> items,
    Func<TPage, string?> nextCursor,
    string queryParameter) : IPageStrategy<TPage, T>
{
    /// <inheritdoc/>
    public PageInfo<T> Parse(TPage page, Response response, Request first)
    {
        var list = items(page);
        var cursor = nextCursor(page);
        if (string.IsNullOrEmpty(cursor))
        {
            return new PageInfo<T>(list, null);
        }

        return new PageInfo<T>(list, first.WithUrl(QuerySplice.Set(first.Url, queryParameter, cursor)));
    }
}
