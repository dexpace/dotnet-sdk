// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The blocking engine behind <c>Pageable.CreateBlocking</c>: the same loop as the async shell over the same
/// <see cref="PageStep"/>, read through <see cref="SyncPath.GetCompletedResult{T}"/> because <c>async: false</c> never
/// suspends (P7c-4). No sync-over-async, no second copy of the fetch, parse and close logic.
/// </summary>
/// <typeparam name="TPage">The envelope type.</typeparam>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="walk">The walk's constants.</param>
/// <param name="cancellationToken">The token the factory captured; observed per page and passed to the transport.</param>
internal sealed class StrategyBlockingPageable<TPage, T>(PageWalk<TPage, T> walk, CancellationToken cancellationToken) : Pageable<T>
{
    /// <inheritdoc/>
    protected override IEnumerable<Page<T>> WalkPages()
    {
        var current = walk.First;
        var fetched = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (walk.MaxPages is { } cap && fetched >= cap)
            {
                yield break;
            }

            var step = SyncPath.GetCompletedResult(PageStep.FetchAsync(walk, current, async: false, cancellationToken), nameof(Pageable));
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
