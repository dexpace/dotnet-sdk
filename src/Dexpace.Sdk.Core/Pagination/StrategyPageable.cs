// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.CompilerServices;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The async engine behind <c>Pageable.Create</c>: one iterator shell over <see cref="PageStep"/> (PAGE-1, PAGE-6 to
/// PAGE-9, PAGE-25, PAGE-26, PAGE-31; design P7c-4).
/// </summary>
/// <remarks>
/// The walk is a <c>while</c> loop in a state machine, so a synchronously completing page continues inline and the
/// stack does not grow per page (PAGE-31). It holds only the immutable <see cref="PageWalk{TPage,T}"/>, so every
/// enumeration is an independent walk from the first request (PAGE-8).
/// </remarks>
/// <typeparam name="TPage">The envelope type.</typeparam>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="walk">The walk's constants.</param>
internal sealed class StrategyPageable<TPage, T>(PageWalk<TPage, T> walk) : AsyncPageable<T>
{
    /// <inheritdoc/>
    protected override async IAsyncEnumerable<Page<T>> WalkPagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var current = walk.First;
        var fetched = 0;
        while (true)
        {
            // PAGE-26: the token is observed per page, never inside the items of a page that was fetched.
            cancellationToken.ThrowIfCancellationRequested();
            if (walk.MaxPages is { } cap && fetched >= cap)
            {
                yield break;
            }

            var step = await PageStep.FetchAsync(walk, current, async: true, cancellationToken).ConfigureAwait(false);
            fetched++;

            // The response behind the page is already closed: nothing is live across this yield (entry 17).
            yield return step.Page;

            if (step.Next is null)
            {
                yield break;
            }

            current = step.Next;
        }
    }
}
