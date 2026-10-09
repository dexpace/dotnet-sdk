// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.CompilerServices;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The walk behind <c>Pageable.FromFetchers</c>: caller-supplied per-page fetchers instead of a strategy (PAGE-34,
/// PAGE-35; design P7c-15, P7c-16).
/// </summary>
/// <remarks>
/// The engine never sees a response: a fetcher reads, builds a materialized page and disposes its own response, so a fetcher
/// that throws still owns whatever it opened and the engine owns nothing. A fresh <see cref="PagingOptions"/> is created per
/// walk (PAGE-8), the first fetcher runs exactly once per walk, and the cap is checked after the yield so a cap of <c>n</c>
/// never starts fetch <c>n + 1</c>.
/// </remarks>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="firstPage">Fetches the first page, or returns <see langword="null"/> for an empty stream.</param>
/// <param name="nextPage">Fetches the page for a key (the previous page's next link, else its continuation token).</param>
/// <param name="maxPages">The page cap, or <see langword="null"/> for none.</param>
internal sealed class FetcherPageable<T>(
    Func<PagingOptions, CancellationToken, ValueTask<FetchedPage<T>?>> firstPage,
    Func<string, PagingOptions, CancellationToken, ValueTask<FetchedPage<T>?>> nextPage,
    int? maxPages) : AsyncPageable<T>
{
    /// <inheritdoc/>
    protected override async IAsyncEnumerable<Page<T>> WalkPagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var options = new PagingOptions();
        cancellationToken.ThrowIfCancellationRequested();
        var fetched = await firstPage(options, cancellationToken).ConfigureAwait(false);
        var count = 0;
        while (fetched is not null)
        {
            count++;
            yield return fetched.Page;

            // PAGE-9: stop before fetching the page that would exceed the cap.
            if (maxPages is { } cap && count >= cap)
            {
                yield break;
            }

            var key = NextKey(fetched);
            if (key is null)
            {
                yield break;
            }

            options.NextLink = fetched.NextLink;
            options.ContinuationToken = fetched.ContinuationToken;
            cancellationToken.ThrowIfCancellationRequested();
            fetched = await nextPage(key, options, cancellationToken).ConfigureAwait(false);
        }
    }

    // PAGE-34: the next link wins; the continuation token is the fallback only when there is no usable link.
    private static string? NextKey(FetchedPage<T> fetched)
    {
        if (!string.IsNullOrWhiteSpace(fetched.NextLink))
        {
            return fetched.NextLink;
        }

        return string.IsNullOrEmpty(fetched.ContinuationToken) ? null : fetched.ContinuationToken;
    }
}
