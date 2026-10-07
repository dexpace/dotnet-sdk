// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// The SDK's waits over a <see cref="TimeProvider"/>: a blocking interruptible sleep and an awaitable delay, so a fake
/// clock drives every wait (CFG-15, CFG-17, CFG-18; design §8.3).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TimeProvider"/> is the SDK's one time seam (design §3.8) but exposes no sleep; these two extension
/// methods add the operations it lacks. Both reject a negative delay, including
/// <see cref="Timeout.InfiniteTimeSpan"/>, with <see cref="ArgumentOutOfRangeException"/>: the BCL treats
/// <c>-1 ms</c> as "wait forever", a trap this closes. A zero delay returns at once (or a completed task) without arming
/// a timer. A delay above 49 days runs as successive bounded waits, because a timer rejects a due time above
/// <c>uint.MaxValue - 1</c> milliseconds (S7). Nothing rounds a sub-millisecond delay.
/// </para>
/// <para>
/// Cancellation surfaces as <see cref="OperationCanceledException"/> carrying the caller's token, which stays signalled
/// (CFG-17). <c>Thread.Sleep</c> and every <c>Task.Delay</c> overload are banned in <c>src/</c> (P5a-8); this class holds
/// the one sanctioned <c>Task.Delay</c> call. It replaces the internal blocking wait that phase 4c added (P5a-7).
/// </para>
/// </remarks>
public static class TimeProviderWaits
{
    // A timer rejects a due time above uint.MaxValue - 1 ms (~49.7 days) with ArgumentOutOfRangeException, so a longer
    // wait runs as successive waits of at most this long (design §6.1; eight for the 365-day ceiling).
    private static readonly TimeSpan s_maxSingleWait = TimeSpan.FromDays(49);

    /// <summary>Blocks the calling thread for <paramref name="delay"/> on <paramref name="timeProvider"/>'s clock.</summary>
    /// <remarks>
    /// A timer from <see cref="TimeProvider.CreateTimer"/> sets a <see cref="ManualResetEventSlim"/> that the caller
    /// waits on with the token. There is no task under it, so it is not sync-over-async; it blocks only the caller's
    /// thread, which is what a synchronous call asked for (design §11 item 1).
    /// </remarks>
    /// <param name="timeProvider">The clock whose timers drive the wait.</param>
    /// <param name="delay">How long to wait; zero returns at once.</param>
    /// <param name="cancellationToken">Interrupts the wait.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The token was signalled before or during the wait.</exception>
    public static void Sleep(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        RequireNonNegative(delay);
        cancellationToken.ThrowIfCancellationRequested();
        while (delay > TimeSpan.Zero)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wait = delay < s_maxSingleWait ? delay : s_maxSingleWait;
            WaitOnce(wait, timeProvider, cancellationToken);
            delay -= wait;
        }
    }

    /// <summary>Returns a task that completes after <paramref name="delay"/> on <paramref name="timeProvider"/>'s clock.</summary>
    /// <remarks>
    /// A timer, no thread held. Validation is synchronous: a null provider or a negative delay throws from the call
    /// itself rather than faulting the returned task. A zero delay returns a completed task, and an already-signalled
    /// token returns a cancelled one, in both cases without arming a timer. Cancellation disposes the timer (CFG-18).
    /// </remarks>
    /// <param name="timeProvider">The clock whose timers drive the delay.</param>
    /// <param name="delay">How long to wait; zero completes synchronously.</param>
    /// <param name="cancellationToken">Cancels the delay.</param>
    /// <returns>A task that completes when the delay has elapsed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is negative.</exception>
    public static Task DelayAsync(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        RequireNonNegative(delay);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return delay == TimeSpan.Zero ? Task.CompletedTask : DelayChunksAsync(timeProvider, delay, cancellationToken);
    }

    private static void RequireNonNegative(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                "A delay must be zero or greater; a negative value (including Timeout.InfiniteTimeSpan) is rejected, not waited forever.");
        }
    }

    private static async Task DelayChunksAsync(TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken)
    {
        while (delay > TimeSpan.Zero)
        {
            var wait = delay < s_maxSingleWait ? delay : s_maxSingleWait;
#pragma warning disable RS0030 // CFG-17, CFG-18, design §8.3: the one sanctioned Task.Delay site; the guard rejects the -1 ms infinite trap and this loop chunks past ~49.7 days.
            await Task.Delay(wait, timeProvider, cancellationToken).ConfigureAwait(false);
#pragma warning restore RS0030
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
