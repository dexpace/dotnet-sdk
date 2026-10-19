// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A <see cref="TokenCredential"/> whose fetch the assertion controls: it counts the fetches, says when the first one began, and
/// (when gated) parks every fetch until the assertion releases the gate. No sleeping: the gate is a task the kit completes
/// (<c>XCUT-12</c>).
/// </summary>
internal sealed class GatedCredential : TokenCredential
{
    private readonly TimeProvider _time;
    private readonly TaskCompletionSource? _gate;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _fetches;

    /// <summary>Creates a credential.</summary>
    /// <param name="time">The clock the token's expiry is read against: an hour ahead of it.</param>
    /// <param name="gated">Whether each fetch parks until <see cref="Release"/>.</param>
    internal GatedCredential(TimeProvider time, bool gated)
    {
        _time = time;
        _gate = gated ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
    }

    /// <summary>The number of fetches so far.</summary>
    internal int Fetches => Volatile.Read(ref _fetches);

    /// <summary>Completed when the first fetch has begun.</summary>
    internal Task Started => _started.Task;

    /// <summary>Lets every parked fetch (and every later one) finish.</summary>
    internal void Release() => _gate?.TrySetResult();

    /// <inheritdoc />
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _fetches);
        _started.TrySetResult();
        if (_gate is not null)
        {
            await _gate.Task.WaitAsync(ct).ConfigureAwait(false);
        }

        return new AccessToken("gated-token-value", _time.GetUtcNow().AddHours(1));
    }
}
