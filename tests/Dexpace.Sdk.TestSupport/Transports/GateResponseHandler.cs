// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// An <see cref="IResponseHandler{T}"/> that counts its calls and blocks on a gate the test opens, so a test can hold a
/// parse in flight and prove that no other caller is blocked behind it (HTTP-45).
/// </summary>
/// <typeparam name="T">The handled value type.</typeparam>
public sealed class GateResponseHandler<T> : IResponseHandler<T>
{
    private int _calls;

    /// <summary>How many times <see cref="HandleAsync"/> ran.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Completes when the first call has started.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The gate every call awaits; the test completes it with the value, or faults it.</summary>
    public TaskCompletionSource<T> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The token the last call was given.</summary>
    public CancellationToken RecordedToken { get; private set; }

    /// <summary>The response the last call was given.</summary>
    public Response? ReceivedResponse { get; private set; }

    /// <summary>When set, a call throws this instead of waiting at the gate.</summary>
    public Exception? ThrowOnCall { get; set; }

    /// <inheritdoc />
    public async ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        RecordedToken = cancellationToken;
        ReceivedResponse = response;
        Started.TrySetResult();
        if (ThrowOnCall is not null)
        {
            throw ThrowOnCall;
        }

        return await Gate.Task.ConfigureAwait(false);
    }
}
