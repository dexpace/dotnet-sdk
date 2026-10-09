// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.CompilerServices;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// An async-enumerable sequence of items that is backed by a series of HTTP pages.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <remarks>
/// <para>
/// <b>Two views of one walk.</b> Enumerating the pageable (<c>await foreach (var item in pageable)</c>) flattens the
/// pages into their items in server order; <see cref="AsPages"/> yields the pages themselves, with their status, headers
/// and request (PAGE-1). Both are built on one private walk, so they cannot drift. Each <c>GetAsyncEnumerator</c> call
/// starts a fresh walk from the first request, so the pageable can be enumerated again (PAGE-8); the view
/// <see cref="AsPages"/> returns is single-use (PAGE-14).
/// </para>
/// <para>
/// <b>Lazy, one exchange per page.</b> Creating the pageable, calling <see cref="AsPages"/> and calling
/// <c>GetAsyncEnumerator</c> send nothing; the first <c>MoveNextAsync</c> sends the first request, and each further
/// page is fetched only when the consumer advances past the last item of the previous one (PAGE-6, PAGE-7). Each page is
/// an independent pipeline call, so retry, redirect and authorization govern every page.
/// </para>
/// <para>
/// <b>Pages are values, not resources.</b> The engine reads a page's body, parses it and closes the response
/// <em>before</em> it yields the page, so nothing is live while a consumer holds a page or an item, and an enumerator
/// dropped without <c>DisposeAsync</c> leaks nothing (PAGE-3, PAGE-11, PAGE-12; design section 10 entry 17). Enumerate with
/// <c>await foreach</c>, or <c>await using</c> a hand-driven enumerator, so the walk itself is disposed on an early exit.
/// </para>
/// <para>
/// <b>Cap the walk in production (PAGE-10).</b> A server that never reports an end would be walked forever; pass a finite
/// <c>maxPages</c> to the factory.
/// </para>
/// <para>
/// <b>Cancellation (PAGE-25, PAGE-26, PAGE-33).</b> The token flows into every exchange and is observed at the top of each
/// page, never between the items of a fetched page, so a page that was fetched is always delivered whole. A response the
/// transport builds after the cancel is never delivered to the pageable and is the transport's to release
/// (<c>TRANSPORT-9</c>); one that <em>is</em> delivered after the token was cancelled is disposed and discarded, and the walk
/// throws <see cref="OperationCanceledException"/>.
/// </para>
/// <para>
/// <b>Subclassing.</b> The abstract class is the seam a caller mocks in tests. A subclass overrides
/// <see cref="WalkPagesAsync"/> to return a fresh walk per call, and obeys one rule the type system cannot enforce: hold no
/// live response across a <c>yield</c>. <see cref="AsPages"/> and <c>GetAsyncEnumerator</c> are not virtual, so the
/// single-use view and the flattening hold for every subclass.
/// </para>
/// <para>
/// <b>Breaking:</b> <c>AsPages(int? pageSizeHint = null)</c> and <c>GetAsyncEnumerator</c> were abstract. <c>pageSizeHint</c>
/// is gone (no implementation used it), <see cref="AsPages"/> returns a single-use view, and subclasses override
/// <see cref="WalkPagesAsync"/> instead (P7c-3).
/// </para>
/// </remarks>
public abstract class AsyncPageable<T> : IAsyncEnumerable<T>
{
    /// <summary>Returns the pages of a fresh walk, with their status, headers and request.</summary>
    /// <returns>
    /// A single-use view: its first <c>GetAsyncEnumerator</c> starts the walk, and a second call throws
    /// <see cref="InvalidOperationException"/>. Call <see cref="AsPages"/> again for another walk (PAGE-14).
    /// </returns>
    public IAsyncEnumerable<Page<T>> AsPages() => new SingleUseAsyncEnumerable<Page<T>>(WalkPagesAsync);

    /// <summary>Returns an enumerator over the items of a fresh walk, pages flattened in server order.</summary>
    /// <param name="cancellationToken">A token to cancel the walk; it is combined with the one the walk receives.</param>
    /// <returns>An <see cref="IAsyncEnumerator{T}"/> over all items across all pages. No exchange happens until it advances.</returns>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        ItemsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

    /// <summary>
    /// Starts one fresh walk of the pages. Called once per enumeration, so the walk is lazy (nothing is sent before the
    /// first <c>MoveNextAsync</c>) and independent of every other (PAGE-6, PAGE-8).
    /// </summary>
    /// <param name="cancellationToken">The consumer's token; honour it at page granularity and pass it to the transport.</param>
    /// <returns>
    /// The pages in server order. An implementation must close each page's response before it yields the page (entry 17).
    /// </returns>
    protected abstract IAsyncEnumerable<Page<T>> WalkPagesAsync(CancellationToken cancellationToken);

    // The item view flattens a fresh page walk; no page is held across a yield (the page values are a snapshot).
    private async IAsyncEnumerable<T> ItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var page in WalkPagesAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            foreach (var item in page.Values)
            {
                yield return item;
            }
        }
    }
}
