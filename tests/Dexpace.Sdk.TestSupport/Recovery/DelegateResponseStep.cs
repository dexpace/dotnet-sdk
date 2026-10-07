// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;

namespace Dexpace.Sdk.TestSupport.Recovery;

/// <summary>A lambda-backed <see cref="IResponseStep"/> that counts its calls and records the last token (P4b-26).</summary>
public sealed class DelegateResponseStep : IResponseStep
{
    private readonly Func<Response, CancellationToken, Response> _apply;
    private readonly Func<Response, CancellationToken, ValueTask<Response>>? _applyAsync;
    private int _syncCalls;
    private int _asyncCalls;

    /// <summary>Creates the step.</summary>
    /// <param name="apply">The synchronous body.</param>
    /// <param name="applyAsync">The asynchronous body; defaults to wrapping <paramref name="apply"/>.</param>
    public DelegateResponseStep(
        Func<Response, CancellationToken, Response> apply,
        Func<Response, CancellationToken, ValueTask<Response>>? applyAsync = null)
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
    public Response Apply(Response response, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _syncCalls);
        LastToken = cancellationToken;
        return _apply(response, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Response> ApplyAsync(Response response, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _asyncCalls);
        LastToken = cancellationToken;
        return _applyAsync is null ? ValueTask.FromResult(_apply(response, cancellationToken)) : _applyAsync(response, cancellationToken);
    }
}
