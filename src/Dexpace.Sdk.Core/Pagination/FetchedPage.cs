// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// What a fetcher returns: a page it built, plus how to reach the one after it (PAGE-34).
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Page">The materialized page; never <see langword="null"/>.</param>
/// <param name="NextLink">
/// The link for the next page. It wins over <paramref name="ContinuationToken"/> when it is not blank; a blank link with no
/// token ends the stream.
/// </param>
/// <param name="ContinuationToken">The token used only when there is no usable <paramref name="NextLink"/>.</param>
/// <remarks>
/// <para>
/// <b>Ownership (P7c-15).</b> A <see cref="Page{T}"/> owns no response (design section 10 entry 17), so there is nothing to
/// transfer to it: the fetcher reads its response, builds the page and disposes the response itself, typically with
/// <c>await using</c>. This inverts PAGE-34's "the fetcher MUST NOT close" clause for the same reason entry 17 inverts
/// PAGE-3; the guarantee that clause protects, no leaked response, is kept by the fetcher's own scope. A fetcher that
/// throws before it builds its page still owns whatever it opened, as PAGE-34 says.
/// </para>
/// <para>
/// A record so that <c>with</c> is available to fetcher authors; a <see langword="null"/> <paramref name="Page"/> is rejected
/// at construction and by <c>with</c>.
/// </para>
/// </remarks>
public sealed record FetchedPage<T>(Page<T> Page, string? NextLink = null, string? ContinuationToken = null)
{
    private readonly Page<T> _page = Page ?? throw new ArgumentNullException(nameof(Page));

    /// <summary>The materialized page; never <see langword="null"/>.</summary>
    public Page<T> Page
    {
        get => _page;
        init => _page = value ?? throw new ArgumentNullException(nameof(value));
    }
}
