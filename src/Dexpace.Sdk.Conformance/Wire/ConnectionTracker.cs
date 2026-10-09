// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Wire;

/// <summary>
/// What the <see cref="LoopbackServer"/> knows about each connection and each request, as conditions a test can wait on
/// (P8a-21, suite-contract clause 7). A connection is <em>released</em> when the client closes it, sends a further request
/// on it, or the server tears it down; <see cref="ConnectionState.ServerClosedFirst"/> says which of those the server did on its own, so a
/// release check after a reply the server closed after (a <c>Connection: close</c> reply) cannot pass vacuously (plan
/// reading R13).
/// </summary>
internal sealed class ConnectionTracker
{
    private readonly object _lock = new();
    private readonly List<ConnectionState> _connections = [];
    private readonly List<RecordedRequest> _requests = [];
    private TaskCompletionSource _connectionAccepted = NewSignal();
    private TaskCompletionSource _requestArrived = NewSignal();

    /// <summary>Registers a newly accepted connection and returns its state; its index is the accept order.</summary>
    internal ConnectionState Accept()
    {
        TaskCompletionSource accepted;
        ConnectionState state;
        lock (_lock)
        {
            state = new ConnectionState(_connections.Count);
            _connections.Add(state);
            accepted = _connectionAccepted;
            _connectionAccepted = NewSignal();
        }

        accepted.TrySetResult();
        return state;
    }

    /// <summary>Records a request in arrival order and wakes every waiter.</summary>
    internal void Record(RecordedRequest request)
    {
        TaskCompletionSource arrived;
        lock (_lock)
        {
            _requests.Add(request);
            arrived = _requestArrived;
            _requestArrived = NewSignal();
        }

        arrived.TrySetResult();
    }

    /// <summary>A snapshot of every request recorded so far.</summary>
    internal IReadOnlyList<RecordedRequest> Requests()
    {
        lock (_lock)
        {
            return [.. _requests];
        }
    }

    /// <summary>The request at <paramref name="index"/> if it has arrived, otherwise the signal that completes when another does.</summary>
    internal (RecordedRequest? Request, Task Arrival) RequestOrSignal(int index)
    {
        lock (_lock)
        {
            return index < _requests.Count ? (_requests[index], Task.CompletedTask) : (null, _requestArrived.Task);
        }
    }

    /// <summary>The state of connection <paramref name="index"/> once it has been accepted; waits for it when it has not.</summary>
    internal async Task<ConnectionState> ConnectionAsync(int index, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task accepted;
            lock (_lock)
            {
                if (index < _connections.Count)
                {
                    return _connections[index];
                }

                accepted = _connectionAccepted.Task;
            }

            await accepted.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The state of an already accepted connection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">No such connection has been accepted.</exception>
    internal ConnectionState Existing(int index)
    {
        lock (_lock)
        {
            return index >= 0 && index < _connections.Count
                ? _connections[index]
                : throw new ArgumentOutOfRangeException(nameof(index), index, "No such connection has been accepted.");
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
