// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// Page-number pagination (PAGE-17): an empty item list ends the stream; otherwise the current page number is read from
/// the <em>executed</em> request's URL (so a first page that carried no parameter is <c>startPage</c>), parsed as
/// ASCII digits only under the invariant culture, and the next request is the first request with the parameter set to
/// the current number plus one (P7c-8, P7c-11). A number that cannot advance (<see cref="int.MaxValue"/>) ends the
/// stream. It holds only its configuration (PAGE-5).
/// </summary>
/// <typeparam name="TPage">The envelope type.</typeparam>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="items">Extracts the ordered items from the envelope.</param>
/// <param name="queryParameter">The query parameter that carries the page number.</param>
/// <param name="startPage">The page number assumed when the executed URL carries none or garbage.</param>
internal sealed class PageNumberStrategy<TPage, T>(
    Func<TPage, IReadOnlyList<T>> items,
    string queryParameter,
    int startPage) : IPageStrategy<TPage, T>
{
    /// <inheritdoc/>
    public PageInfo<T> Parse(TPage page, Response response, Request first)
    {
        var list = items(page);
        ArgumentNullException.ThrowIfNull(list, nameof(items));
        if (list.Count == 0)
        {
            return new PageInfo<T>(list, null);
        }

        var raw = QuerySplice.Get(response.Request.Url, queryParameter);
        var current = raw is { Length: > 0 } && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : startPage;
        if (current == int.MaxValue)
        {
            return new PageInfo<T>(list, null);
        }

        var next = QuerySplice.Set(first.Url, queryParameter, (current + 1).ToString(CultureInfo.InvariantCulture));
        return new PageInfo<T>(list, first.WithUrl(next));
    }
}
