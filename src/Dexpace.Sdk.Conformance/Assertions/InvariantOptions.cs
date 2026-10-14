// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Conformance;

/// <summary>The client options the invariant assertions share, so no assertion waits on a real backoff it did not ask for.</summary>
internal static class InvariantOptions
{
    /// <summary>The URL every assertion requests: nothing is sent to it, the transport is in memory.</summary>
    internal const string Url = "https://example.test/resource";

    /// <summary>Retries up to <paramref name="attempts"/> times, a millisecond apart and without jitter.</summary>
    /// <param name="attempts">The retries after the first send.</param>
    internal static RetryOptions Fast(int attempts) =>
        new() { MaxRetryAttempts = attempts, FixedDelay = TimeSpan.FromMilliseconds(1), Jitter = 0 };

    /// <summary>Client options retrying <paramref name="attempts"/> times, fast.</summary>
    /// <param name="attempts">The retries after the first send.</param>
    internal static DexpaceClientOptions Retrying(int attempts) => new() { Retry = Fast(attempts) };
}
