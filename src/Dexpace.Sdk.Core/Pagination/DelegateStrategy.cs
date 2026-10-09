// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The two-delegate strategy of design section 7.1 (<c>selectItems</c>, <c>nextRequest</c>), kept as the adapter
/// <c>PaginationStrategies.Create</c> returns (PAGE-4, PAGE-5; P7c-2). It holds only the two delegates.
/// </summary>
/// <typeparam name="TPage">The envelope type.</typeparam>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="items">Extracts the ordered items from the envelope.</param>
/// <param name="nextRequest">Returns the next request from the envelope, the response and the first request, or <see langword="null"/> to end.</param>
internal sealed class DelegateStrategy<TPage, T>(
    Func<TPage, IReadOnlyList<T>> items,
    Func<TPage, Response, Request, Request?> nextRequest) : IPageStrategy<TPage, T>
{
    /// <inheritdoc/>
    public PageInfo<T> Parse(TPage page, Response response, Request first) =>
        new(items(page), nextRequest(page, response, first));
}
