// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The page view <c>AsyncPageable&lt;T&gt;.AsPages()</c> returns: one walk, enumerable once (PAGE-14).
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// A compiler-generated async iterator starts a second, independent walk on a second <c>GetAsyncEnumerator</c> call
/// (design fact 8), which is right for the item view (PAGE-8) and wrong for the page view, where the second call would
/// silently restart the stream. The latch is set with <see cref="Interlocked.Exchange(ref int, int)"/>, so two racing
/// callers cannot both win. The factory runs on the first call only, so constructing the view and calling
/// <c>AsPages()</c> issue no exchange (PAGE-6). The token goes to both the factory and the enumerator, so a walk that is
/// not a compiler iterator, or whose parameter lacks <c>[EnumeratorCancellation]</c>, still sees
/// <c>WithCancellation</c>; an iterator that has the attribute combines two identical tokens harmlessly.
/// </remarks>
/// <param name="factory">Starts the walk; called at most once.</param>
internal sealed class SingleUseAsyncEnumerable<T>(Func<CancellationToken, IAsyncEnumerable<T>> factory) : IAsyncEnumerable<T>
{
    private int _used;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The view was already enumerated.</exception>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _used, 1) != 0)
        {
            throw new InvalidOperationException("This page view is single-use; call AsPages() again for a new walk.");
        }

        return factory(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }
}
