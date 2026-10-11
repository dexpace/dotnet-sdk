// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// One call's deadline (<c>TRANSPORT-5</c>, <c>TRANSPORT-6</c>; design §C, phase 8b P8b-6): a timer source armed on the transport's
/// <see cref="TimeProvider"/> and linked with the caller's token. The shared <c>HttpClient</c> is never mutated and the caller's
/// token is never touched, so a timeout and a cancellation stay distinguishable by <see cref="Fired"/>.
/// </summary>
/// <remarks>
/// <para>
/// The deadline bounds the exchange until the response headers arrive, as <c>AttemptTimeout</c> (phase 6a) and
/// <c>HttpClient.Timeout</c> under <c>ResponseHeadersRead</c> do. It is disposed when the native send returns: cancelling or
/// disposing the token a <c>SendAsync(..., ResponseHeadersRead, token)</c> was given after it returned does not affect reading
/// the body (design fact G3, measured on .NET 10.0.401), so the body read is bounded by the token the caller passes to it.
/// </para>
/// <para>
/// With no deadline at all nothing is allocated and <see cref="Token"/> is the caller's token. A class rather than a struct
/// (plan reading R7): it owns two sources, and the allocation exists only when a deadline is armed.
/// </para>
/// </remarks>
internal sealed class CallDeadline : IDisposable
{
    // TRANSPORT-6: the timer's resolution is one millisecond, and a CancellationTokenSource rejects a delay above
    // uint.MaxValue - 1 ms (about 49.7 days). A positive deadline outside those bounds is moved to the nearest, never refused and
    // never read as "no deadline"; 49 days is the ceiling DexpaceClientOptions accepts too (P6a-24).
    private static readonly TimeSpan s_floor = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan s_ceiling = TimeSpan.FromDays(49);

    private readonly CancellationTokenSource? _timer;
    private readonly CancellationTokenSource? _linked;

    private CallDeadline(CancellationTokenSource? timer, CancellationTokenSource? linked, CancellationToken token)
    {
        Token = token;
        _timer = timer;
        _linked = linked;
    }

    /// <summary>The token to give the native send: signalled by the caller's cancellation or by the deadline, whichever comes first.</summary>
    internal CancellationToken Token { get; }

    /// <summary>Whether the deadline elapsed. A caller's own cancellation leaves it <see langword="false"/>.</summary>
    internal bool Fired => _timer is { IsCancellationRequested: true };

    /// <summary>
    /// The deadline for one call: the per-call value when the call has one, else the transport's default, else none.
    /// </summary>
    /// <param name="options">The call's options.</param>
    /// <param name="transportDefault">The transport's default, or <see langword="null"/> for none.</param>
    internal static TimeSpan? Effective(RequestOptions options, TimeSpan? transportDefault)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Timeout ?? transportDefault;
    }

    /// <summary>Moves a positive deadline to the timer's range: at least one millisecond, at most 49 days.</summary>
    /// <param name="timeout">A positive deadline.</param>
    internal static TimeSpan Clamp(TimeSpan timeout) => timeout < s_floor ? s_floor : timeout > s_ceiling ? s_ceiling : timeout;

    /// <summary>Arms a deadline of <paramref name="timeout"/> (none when <see langword="null"/>) for one call.</summary>
    /// <param name="timeout">The effective deadline, or <see langword="null"/> for none.</param>
    /// <param name="timeProvider">The clock that arms it.</param>
    /// <param name="caller">The caller's token, which the deadline links to and never signals.</param>
    internal static CallDeadline Start(TimeSpan? timeout, TimeProvider timeProvider, CancellationToken caller)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (timeout is not { } value)
        {
            return new CallDeadline(timer: null, linked: null, caller);
        }

        var timer = new CancellationTokenSource(Clamp(value), timeProvider);
        if (!caller.CanBeCanceled)
        {
            return new CallDeadline(timer, linked: null, timer.Token);
        }

        var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, timer.Token);
        return new CallDeadline(timer, linked, linked.Token);
    }

    /// <summary>Releases the timer. Idempotent.</summary>
    public void Dispose()
    {
        _linked?.Dispose();
        _timer?.Dispose();
    }
}
