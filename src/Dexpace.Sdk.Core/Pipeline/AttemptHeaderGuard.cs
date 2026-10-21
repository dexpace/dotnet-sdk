// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Resilience;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// The stage stack's half of the RETRY-38 collision check (<see cref="AttemptHeaderCollision"/> holds the rule and the recovery
/// stack's half): a pipeline that holds a <see cref="RetryPolicy"/> and an <see cref="IdempotencyPolicy"/> whose header is the
/// attempt header.
/// </summary>
internal static class AttemptHeaderGuard
{
    /// <summary>Finds a collision among a pipeline's policies.</summary>
    /// <param name="policies">The pipeline's policies.</param>
    /// <param name="retry">The retry options that will run with them.</param>
    /// <returns>The message to throw with, or <see langword="null"/> when the stamp and the key cannot meet.</returns>
    internal static string? Collision(IEnumerable<HttpPipelinePolicy> policies, RetryOptions retry)
    {
        if (retry.AttemptHeaderName is null)
        {
            return null;
        }

        // Without a retry policy nothing stamps, so the same name is not a collision.
        var stamps = false;
        string? collision = null;
        foreach (var policy in policies)
        {
            stamps |= policy is RetryPolicy;
            if (policy is IdempotencyPolicy keyed)
            {
                collision ??= AttemptHeaderCollision.Between(retry, keyed.HeaderName, nameof(IdempotencyPolicy));
            }
        }

        return stamps ? collision : null;
    }
}
