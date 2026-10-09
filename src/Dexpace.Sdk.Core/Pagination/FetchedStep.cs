// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>What one <c>PageStep</c> produced: the page to yield and the request for the page after it.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Page">The materialized page; the response behind it is already closed (design section 10 entry 17).</param>
/// <param name="Next">The request for the next page, or <see langword="null"/> when the strategy ended the stream.</param>
internal readonly record struct FetchedStep<T>(Page<T> Page, Request? Next);
