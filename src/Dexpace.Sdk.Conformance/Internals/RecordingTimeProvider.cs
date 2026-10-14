// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A <see cref="TimeProvider"/> over <see cref="TimeProvider.System"/> that counts the timers it creates and the timers that are
/// disposed, so an assertion can prove that a cancelled wait cancelled the timer it armed (<c>XCUT-3</c>) without sleeping on
/// the wall clock: the kit never calls <c>Task.Delay</c> or reads a wall clock (the kit's 8a rule). <see cref="Advance"/> moves
/// the clock it reports forward without waiting.
/// </summary>
internal sealed class RecordingTimeProvider : TimeProvider
{
    private int _created;
    private int _disposed;
    private long _offsetTicks;

    /// <summary>The number of timers created through this provider.</summary>
    internal int TimersCreated => Volatile.Read(ref _created);

    /// <summary>The number of those timers that have been disposed (each counted once).</summary>
    internal int TimersDisposed => Volatile.Read(ref _disposed);

    /// <summary>The timers created and not yet disposed.</summary>
    internal int TimersLive => TimersCreated - TimersDisposed;

    /// <inheritdoc />
    public override long TimestampFrequency => System.TimestampFrequency;

    /// <summary>Moves the time this provider reports forward by <paramref name="by"/>; timers still run on real time.</summary>
    /// <param name="by">A non-negative amount.</param>
    internal void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        Interlocked.Add(ref _offsetTicks, by.Ticks);
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    /// <inheritdoc />
    public override long GetTimestamp() =>
        System.GetTimestamp() + (long)(Interlocked.Read(ref _offsetTicks) * ((double)TimestampFrequency / TimeSpan.TicksPerSecond));

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var inner = System.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref _created);
        return new CountingTimer(inner, this);
    }

    private void TimerDisposed() => Interlocked.Increment(ref _disposed);

    private sealed class CountingTimer(ITimer inner, RecordingTimeProvider owner) : ITimer
    {
        private int _done;

        public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);

        public void Dispose()
        {
            inner.Dispose();
            Count();
        }

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            Count();
        }

        private void Count()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
            {
                owner.TimerDisposed();
            }
        }
    }
}
