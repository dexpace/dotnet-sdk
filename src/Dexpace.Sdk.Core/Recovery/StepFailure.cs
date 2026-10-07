// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// The one place a response is released when a step throws while a success is in hand (RECOV-12, design §5.2).
/// </summary>
/// <remarks>
/// It runs only on a throw: a step that returns a different outcome owns what it dropped (RECOV-13). The release goes
/// through <see cref="Disposal"/> with the thrown exception as primary, so a close failure lands on the primary's trail
/// and never replaces it. "Exactly once" is the response's own dispose latch.
/// </remarks>
internal static class StepFailure
{
    /// <summary>Releases the response in <paramref name="current"/> (if any) and returns the failure.</summary>
    /// <param name="thrown">The exception the step threw.</param>
    /// <param name="current">The outcome in hand before the step ran.</param>
    /// <param name="async">Whether to release through the asynchronous dispose.</param>
    /// <returns>A <see cref="Outcome.Failure"/> carrying <paramref name="thrown"/>.</returns>
    internal static async ValueTask<Outcome> ConvertAsync(Exception thrown, Outcome current, bool async)
    {
        if (current is Outcome.Success success)
        {
            if (async)
            {
                await Disposal.DisposeQuietlyAsync(success.Response, thrown).ConfigureAwait(false);
            }
            else
            {
                Disposal.DisposeQuietly(success.Response, thrown);
            }
        }

        return new Outcome.Failure(thrown);
    }

    /// <summary>The synchronous form of <see cref="ConvertAsync"/>.</summary>
    /// <param name="thrown">The exception the step threw.</param>
    /// <param name="current">The outcome in hand before the step ran.</param>
    /// <returns>A <see cref="Outcome.Failure"/> carrying <paramref name="thrown"/>.</returns>
    internal static Outcome Convert(Exception thrown, Outcome current) =>
        SyncPath.GetResult(ConvertAsync(thrown, current, async: false));
}
