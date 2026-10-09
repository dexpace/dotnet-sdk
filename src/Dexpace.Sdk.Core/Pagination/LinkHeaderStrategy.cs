// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// <c>Link</c>-header pagination (PAGE-18 to PAGE-20): every instance of the header is read and joined, the first
/// <c>rel=next</c> target is resolved against the page's response URL, and the next request is the first request with
/// that URL. A missing header, a missing <c>next</c>, an unresolvable target or (unless
/// <paramref name="allowCrossOrigin"/>) a target on another origin ends the stream quietly (P7c-12). It holds only its
/// configuration (PAGE-5).
/// </summary>
/// <typeparam name="TPage">The envelope type.</typeparam>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="items">Extracts the ordered items from the envelope.</param>
/// <param name="headerName">The header to read; <c>Link</c> by default.</param>
/// <param name="allowCrossOrigin">Whether a target on another origin than the first request may be followed.</param>
internal sealed class LinkHeaderStrategy<TPage, T>(
    Func<TPage, IReadOnlyList<T>> items,
    string headerName,
    bool allowCrossOrigin) : IPageStrategy<TPage, T>
{
    /// <inheritdoc/>
    public PageInfo<T> Parse(TPage page, Response response, Request first)
    {
        var list = items(page);
        var values = response.Headers.GetAll(headerName);
        if (values.Count == 0)
        {
            return new PageInfo<T>(list, null);
        }

        var raw = LinkHeaderParser.FindNext(string.Join(", ", values));
        return LinkTarget.TryResolve(response.Request.Url, raw, first.Url, allowCrossOrigin, out var target)
            ? new PageInfo<T>(list, first.WithUrl(target))
            : new PageInfo<T>(list, null);
    }
}
