// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// The single source of the retry facts the model and the policies share (HTTP-9; design §6.1). Phase 6a grows this
/// class and derives its allow-lists from the same set.
/// </summary>
internal static class RetryFacts
{
    /// <summary>
    /// The methods whose repetition has the same effect as issuing them once (RFC 9110 §9.2.2): GET, HEAD, OPTIONS,
    /// PUT and DELETE. TRACE is deliberately absent: it is safe but not idempotent for retry purposes here, so
    /// <see cref="RetryPolicy"/> no longer retries it. Read through <c>Method.IsIdempotent</c>.
    /// </summary>
    internal static FrozenSet<Method> IdempotentMethods { get; } =
        new[] { Method.Get, Method.Head, Method.Options, Method.Put, Method.Delete }.ToFrozenSet();
}
