// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Recovery;

namespace Dexpace.Sdk.TestSupport.Recovery;

/// <summary>A lambda-backed <see cref="IRecoveryStep"/> that counts its calls and records the last token (P4b-26).</summary>
public sealed class DelegateRecoveryStep : IRecoveryStep
{
    private readonly Func<Outcome, CancellationToken, Outcome> _apply;
    private readonly Func<Outcome, CancellationToken, ValueTask<Outcome>>? _applyAsync;
    private int _syncCalls;
    private int _asyncCalls;

    /// <summary>Creates the step.</summary>
    /// <param name="apply">The synchronous body.</param>
    /// <param name="applyAsync">The asynchronous body; defaults to wrapping <paramref name="apply"/>.</param>
    public DelegateRecoveryStep(
        Func<Outcome, CancellationToken, Outcome> apply,
        Func<Outcome, CancellationToken, ValueTask<Outcome>>? applyAsync = null)
    {
        ArgumentNullException.ThrowIfNull(apply);
        _apply = apply;
        _applyAsync = applyAsync;
    }

    /// <summary>How many times <see cref="Apply"/> ran.</summary>
    public int SyncCalls => Volatile.Read(ref _syncCalls);

    /// <summary>How many times <see cref="ApplyAsync"/> ran.</summary>
    public int AsyncCalls => Volatile.Read(ref _asyncCalls);

    /// <summary>The total number of calls, in either form.</summary>
    public int CallCount => SyncCalls + AsyncCalls;

    /// <summary>The token of the most recent call.</summary>
    public CancellationToken LastToken { get; private set; }

    /// <inheritdoc />
    public Outcome Apply(Outcome outcome, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _syncCalls);
        LastToken = cancellationToken;
        return _apply(outcome, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Outcome> ApplyAsync(Outcome outcome, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _asyncCalls);
        LastToken = cancellationToken;
        return _applyAsync is null ? ValueTask.FromResult(_apply(outcome, cancellationToken)) : _applyAsync(outcome, cancellationToken);
    }
}
