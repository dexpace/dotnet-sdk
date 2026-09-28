// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Time;

/// <summary>
/// A <see cref="TimeProvider"/> with a pinned clock whose timers all fire after one millisecond, whatever due time
/// they were created with — so a retry back-off or a <c>Retry-After</c> wait completes at once and the test never
/// blocks on a real delay. Use <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c> instead when the test must
/// control exactly when a timer fires.
/// </summary>
/// <param name="utcNow">The instant <see cref="GetUtcNow"/> returns.</param>
public sealed class InstantTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    /// <summary>The default pinned instant: 2026-06-14T12:00:00Z.</summary>
    public static readonly DateTimeOffset DefaultUtcNow = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a provider pinned to <see cref="DefaultUtcNow"/>.</summary>
    public InstantTimeProvider()
        : this(DefaultUtcNow)
    {
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => utcNow;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        base.CreateTimer(callback, state, TimeSpan.FromMilliseconds(1), period);
}
