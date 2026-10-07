// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// Options for the retry policy.
/// </summary>
/// <remarks>
/// A sealed record with <see langword="init"/> accessors (CFG-8): derive a modified copy with <see langword="with"/>
/// (CFG-9). <para><b>Breaking:</b> this was a mutable class; assigning a property after construction no longer
/// compiles, and equality and the hash code are by value (was: by reference).</para>
/// </remarks>
public sealed record RetryOptions
{
    /// <summary>
    /// The number of retry attempts after the initial send. Defaults to <c>3</c>.
    /// Matches the Polly v8 / <c>Microsoft.Extensions.Http.Resilience</c> naming convention.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// The base delay for exponential back-off. Defaults to <c>200 ms</c>.
    /// </summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The maximum back-off delay cap. Defaults to <c>30 s</c>.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When <see langword="true"/>, the retry policy respects a <c>Retry-After</c> response
    /// header. Defaults to <see langword="true"/>.
    /// </summary>
    public bool HonorRetryAfter { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, the retry policy may retry non-idempotent methods (e.g.
    /// <c>POST</c>) if the request body is replayable. Defaults to <see langword="false"/>.
    /// </summary>
    public bool RetryNonIdempotentWhenReplayable { get; init; }
}
