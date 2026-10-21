// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Recovery;

namespace Dexpace.Sdk.Core.Resilience;

/// <summary>
/// RETRY-38 "MUST preserve any existing idempotency key": finds a retry stack whose attempt stamp would be written under the
/// header an idempotency step stamps its key under.
/// </summary>
/// <remarks>
/// <see cref="RetryOptions.AttemptHeaderName"/> refuses the literal <c>Idempotency-Key</c> on its own, but the step's header is
/// configurable, and the retry engine replaces whatever a request carries under the stamp's name on every send, so a pair that
/// agrees on any other name would send the attempt number as the key, a different value on every attempt. The two are only seen
/// together where a stack is assembled: the pipeline builder and the pipeline's per-call options on the stage stack
/// (<c>AttemptHeaderGuard</c>, which owns the pipeline types this namespace may not name), the dispatcher on the recovery stack.
/// A custom policy or request step that stamps a key of its own is opaque to both; naming the attempt header is the caller's
/// half of the contract.
/// </remarks>
internal static class AttemptHeaderCollision
{
    /// <summary>Compares one stamped key header with the attempt header.</summary>
    /// <param name="retry">The retry options that will run.</param>
    /// <param name="keyHeader">The header an idempotency step stamps its key under.</param>
    /// <param name="owner">The name of the type that owns the key header, for the message.</param>
    /// <returns>The message to throw with, or <see langword="null"/> when the two names differ or no attempt header is set.</returns>
    internal static string? Between(RetryOptions retry, HttpHeaderName keyHeader, string owner) =>
        retry.AttemptHeaderName is { } attempt && string.Equals(attempt, keyHeader.CanonicalName, StringComparison.OrdinalIgnoreCase)
            ? $"The retry attempt header '{attempt}' (RetryOptions.AttemptHeaderName) is the header {owner} stamps the idempotency key under; "
                + "the attempt stamp replaces the header on every send, so the key would be overwritten with the attempt number (RETRY-38 "
                + "requires the key to be preserved). Give the attempt header another name."
            : null;

    /// <summary>Finds a collision on the recovery stack.</summary>
    /// <param name="steps">The request chain's steps.</param>
    /// <param name="retry">The retry options the dispatcher will run with them.</param>
    /// <returns>The message to throw with, or <see langword="null"/> when the stamp and the key cannot meet.</returns>
    internal static string? In(IEnumerable<IRequestStep> steps, RetryOptions retry)
    {
        foreach (var step in steps)
        {
            if (step is IdempotencyKeyStep keyed && Between(retry, keyed.HeaderName, nameof(IdempotencyKeyStep)) is { } message)
            {
                return message;
            }
        }

        return null;
    }
}
