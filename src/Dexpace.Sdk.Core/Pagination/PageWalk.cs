// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The constants of one pageable, bundled so <c>PageStep.FetchAsync</c> takes the walk, the current request, the async
/// flag and the token (plan reading R5). Immutable: every walk over the same pageable shares one instance (PAGE-8), and
/// the same <see cref="Options"/> instance reaches every page (PAGE-36).
/// </summary>
/// <typeparam name="TPage">The envelope type.</typeparam>
/// <typeparam name="T">The item type.</typeparam>
internal sealed class PageWalk<TPage, T>
{
    private PageWalk(
        IAsyncHttpClient? asyncClient,
        IHttpClient? blockingClient,
        Request first,
        ISerde serde,
        IPageStrategy<TPage, T> strategy,
        RequestOptions options,
        int? maxPages)
    {
        AsyncClient = asyncClient;
        BlockingClient = blockingClient;
        First = first;
        Serde = serde;
        Strategy = strategy;
        Options = options;
        MaxPages = maxPages;
    }

    /// <summary>The transport of an async walk; <see langword="null"/> for a blocking one.</summary>
    internal IAsyncHttpClient? AsyncClient { get; }

    /// <summary>The transport of a blocking walk; <see langword="null"/> for an async one.</summary>
    internal IHttpClient? BlockingClient { get; }

    /// <summary>The walk's first request, which is also the template every next request is built from.</summary>
    internal Request First { get; }

    /// <summary>The codec that reads each page envelope.</summary>
    internal ISerde Serde { get; }

    /// <summary>The strategy that turns an envelope into items and a next request.</summary>
    internal IPageStrategy<TPage, T> Strategy { get; }

    /// <summary>The per-call options, passed as the same instance to every page (PAGE-36).</summary>
    internal RequestOptions Options { get; }

    /// <summary>The page cap, or <see langword="null"/> for none (PAGE-9, PAGE-10).</summary>
    internal int? MaxPages { get; }

    /// <summary>Validates the arguments of <c>Pageable.Create</c> and bundles them.</summary>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPages"/> is zero or negative (PAGE-9).</exception>
    /// <exception cref="ArgumentException"><paramref name="first"/> has a body that cannot be re-sent (P7c-13).</exception>
    internal static PageWalk<TPage, T> ForAsync(
        IAsyncHttpClient client,
        Request first,
        ISerde serde,
        IPageStrategy<TPage, T> strategy,
        RequestOptions? options,
        int? maxPages)
    {
        ArgumentNullException.ThrowIfNull(client);
        Validate(first, serde, strategy, maxPages);
        return new PageWalk<TPage, T>(client, null, first, serde, strategy, options ?? RequestOptions.Empty, maxPages);
    }

    /// <summary>Validates the arguments of <c>Pageable.CreateBlocking</c> and bundles them.</summary>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPages"/> is zero or negative (PAGE-9).</exception>
    /// <exception cref="ArgumentException"><paramref name="first"/> has a body that cannot be re-sent (P7c-13).</exception>
    internal static PageWalk<TPage, T> ForBlocking(
        IHttpClient client,
        Request first,
        ISerde serde,
        IPageStrategy<TPage, T> strategy,
        RequestOptions? options,
        int? maxPages)
    {
        ArgumentNullException.ThrowIfNull(client);
        Validate(first, serde, strategy, maxPages);
        return new PageWalk<TPage, T>(null, client, first, serde, strategy, options ?? RequestOptions.Empty, maxPages);
    }

    // Every page re-sends the template's body, so a single-use stream fails here, at construction, not with a
    // StreamConsumedException on page two (P7c-13).
    private static void Validate(Request first, ISerde serde, IPageStrategy<TPage, T> strategy, int? maxPages)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(serde);
        ArgumentNullException.ThrowIfNull(strategy);
        if (maxPages is { } cap)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cap, nameof(maxPages));
        }

        if (first.Body is { IsReplayable: false })
        {
            throw new ArgumentException(
                "Every page re-sends the first request's body, but this body is single-use and cannot be written twice. " +
                "Call RequestBody.ToReplayableAsync() first, or use a bytes- or string-backed body.",
                nameof(first));
        }
    }
}
