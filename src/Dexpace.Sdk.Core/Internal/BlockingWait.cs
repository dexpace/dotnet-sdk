// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// A genuinely blocking wait for the synchronous retry path: a <see cref="TimeProvider"/> timer sets a
/// <see cref="ManualResetEventSlim"/> that the calling thread waits on with the cancellation token (design §5.3,
/// position E part 3).
/// </summary>
/// <remarks>
/// There is no task under it, so it is not sync-over-async, and it runs on the caller's <see cref="TimeProvider"/> so a
/// fake clock drives it in tests. A wait longer than a single timer accepts runs as successive bounded waits (S7).
/// </remarks>
internal static class BlockingWait
{
    // A timer rejects a due time above uint.MaxValue - 1 ms (~49.7 days) with ArgumentOutOfRangeException, so a longer
    // wait runs as successive waits of at most this long (design §6.1; eight for the 365-day ceiling).
    private static readonly TimeSpan s_maxSingleWait = TimeSpan.FromDays(49);

    internal static TimeSpan MaxSingleWait => s_maxSingleWait;

    internal static void Wait(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        while (delay > TimeSpan.Zero)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wait = delay < s_maxSingleWait ? delay : s_maxSingleWait;
            WaitOnce(wait, timeProvider, cancellationToken);
            delay -= wait;
        }
    }

    private static void WaitOnce(TimeSpan wait, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        using var signal = new ManualResetEventSlim(initialState: false);
        using var timer = timeProvider.CreateTimer(
            static state =>
            {
                try
                {
                    ((ManualResetEventSlim)state!).Set();
                }
                catch (ObjectDisposedException)
                {
                    // The wait was cancelled and the signal disposed before the timer fired: nothing is waiting.
                }
            },
            signal,
            wait,
            Timeout.InfiniteTimeSpan);
        signal.Wait(cancellationToken);
    }
}
