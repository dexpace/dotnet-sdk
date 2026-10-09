// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The mutable bag one fetcher walk threads through every fetcher call (PAGE-35; design P7c-16).
/// </summary>
/// <remarks>
/// <para>
/// <b>Mutable on purpose.</b> The same instance is passed to the first and to every next fetcher call of a walk, and the
/// engine writes <see cref="NextLink"/> and <see cref="ContinuationToken"/> onto it before each next call, so a custom
/// retriever can read either and stash cursor or auth state in <see cref="State"/> between pages. Cross-call visibility is
/// the point (PAGE-35), not a hazard to design away.
/// </para>
/// <para>
/// A fresh instance is created per walk, so two enumerations never share state (PAGE-8). It is single-consumer and not
/// thread-safe: a walk calls the fetchers one at a time, and an instance must not be used by two walks.
/// </para>
/// </remarks>
public sealed class PagingOptions
{
    /// <summary>The link for the next page, as the previous page's fetcher reported it; <see langword="null"/> before the first next call.</summary>
    public string? NextLink { get; set; }

    /// <summary>The continuation token the previous page's fetcher reported; <see langword="null"/> before the first next call.</summary>
    public string? ContinuationToken { get; set; }

    /// <summary>Custom per-walk state a fetcher stashes for the following calls of the same walk.</summary>
    public IDictionary<string, object?> State { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
