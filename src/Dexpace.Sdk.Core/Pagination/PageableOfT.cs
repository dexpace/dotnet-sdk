// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The type here is Pageable<T>; the file is named PageableOfT.cs because Pageable.cs holds the arity-0 static factory
// class and the repository names a file for its type (one type per file), which a plain Pageable.cs cannot do twice.

using System.Collections;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The blocking twin of <see cref="AsyncPageable{T}"/>: a sequence of items backed by a series of HTTP pages, fetched
/// with the synchronous transport (PAGE-1, PAGE-6, PAGE-7, PAGE-9, PAGE-14, PAGE-31).
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <remarks>
/// <para>
/// The same contract as <see cref="AsyncPageable{T}"/>, enumerated with <c>foreach</c>: the pageable flattens a fresh
/// walk per <see cref="GetEnumerator"/> (PAGE-8), <see cref="AsPages"/> returns a single-use page view (PAGE-14), nothing
/// is sent until the first <c>MoveNext</c> (PAGE-6), and no response is live at any point the consumer can observe
/// (design section 10 entry 17). Enumerate with <c>foreach</c>, or <c>using</c> a hand-driven enumerator, so the walk is
/// disposed on an early exit (PAGE-12).
/// </para>
/// <para>
/// <b>Honest above the transport, not below it.</b> The pager never blocks on a task. The thread it blocks is the
/// transport's, and the transport has to support a synchronous read: each page is read through
/// <c>ResponseBody.OpenRead</c>. <c>SystemNetHttpClient</c>'s response body implements it (phase 7b), so a blocking walk
/// over that transport, directly or through an <c>HttpPipeline</c>, works; its <c>Execute</c> still blocks on the async send
/// until roadmap phase 8b (<c>PIPE-28</c>). Over a transport whose bodies do not override <c>OpenRead</c>, the first
/// <c>MoveNext</c> throws <see cref="NotSupportedException"/>. Each page is buffered through the body's stream under the
/// 64 MiB materialisation cap, so a page envelope larger than that fails with <c>BodyTooLargeException</c>, refused up front
/// when the body declares a larger length; the async pager streams. A page envelope over 64 MiB is not a paging use case.
/// </para>
/// <para>
/// Subclasses override <see cref="WalkPages"/> to return a fresh walk per call and hold no live response across a
/// <c>yield</c>; <see cref="AsPages"/> and <see cref="GetEnumerator"/> are not virtual.
/// </para>
/// </remarks>
public abstract class Pageable<T> : IEnumerable<T>
{
    /// <summary>Returns the pages of a fresh walk, with their status, headers and request.</summary>
    /// <returns>
    /// A single-use view: its first <c>GetEnumerator</c> starts the walk, and a second call throws
    /// <see cref="InvalidOperationException"/>. Call <see cref="AsPages"/> again for another walk (PAGE-14).
    /// </returns>
    public IEnumerable<Page<T>> AsPages() => new SingleUseEnumerable<Page<T>>(WalkPages);

    /// <summary>Returns an enumerator over the items of a fresh walk, pages flattened in server order.</summary>
    /// <returns>An enumerator; no exchange happens until its first <c>MoveNext</c>.</returns>
    public IEnumerator<T> GetEnumerator() => Items().GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Starts one fresh walk of the pages. Called once per enumeration; the walk must be lazy (nothing is sent before the
    /// first <c>MoveNext</c>) and independent of every other (PAGE-6, PAGE-8).
    /// </summary>
    /// <returns>
    /// The pages in server order. An implementation must close each page's response before it yields the page (entry 17).
    /// </returns>
    protected abstract IEnumerable<Page<T>> WalkPages();

    // The item view is a lazy iterator, so WalkPages runs on the first MoveNext, not when the enumerator is obtained.
    private IEnumerable<T> Items()
    {
        foreach (var page in WalkPages())
        {
            foreach (var item in page.Values)
            {
                yield return item;
            }
        }
    }
}
