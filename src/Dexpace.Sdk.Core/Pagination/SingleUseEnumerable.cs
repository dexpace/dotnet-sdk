// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The blocking twin of <c>SingleUseAsyncEnumerable&lt;T&gt;</c>: the page view <c>Pageable&lt;T&gt;.AsPages()</c>
/// returns, enumerable once (PAGE-14). The factory runs on the first <c>GetEnumerator</c> call only (PAGE-6).
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="factory">Starts the walk; called at most once.</param>
internal sealed class SingleUseEnumerable<T>(Func<IEnumerable<T>> factory) : IEnumerable<T>
{
    private int _used;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The view was already enumerated.</exception>
    public IEnumerator<T> GetEnumerator()
    {
        if (Interlocked.Exchange(ref _used, 1) != 0)
        {
            throw new InvalidOperationException("This page view is single-use; call AsPages() again for a new walk.");
        }

        return factory().GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
