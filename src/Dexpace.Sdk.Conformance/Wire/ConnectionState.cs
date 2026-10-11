// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Wire;

/// <summary>One accepted connection: its index, whether the server closed it first, and the signal that it was released.</summary>
internal sealed class ConnectionState(int index)
{
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _requestsServed;
    private int _serverClosedFirst;

    /// <summary>The zero-based accept order.</summary>
    internal int Index { get; } = index;

    /// <summary>Completes when the connection is released.</summary>
    internal Task Released => _released.Task;

    /// <summary>Whether the server closed or reset the connection on its own, before the client left.</summary>
    internal bool ServerClosedFirst => Volatile.Read(ref _serverClosedFirst) == 1;

    /// <summary>Notes that the server is about to close or reset the connection; call before <see cref="Release"/>.</summary>
    internal void MarkServerClosedFirst() => Volatile.Write(ref _serverClosedFirst, 1);

    /// <summary>Notes a request read from the connection; a second one releases it, since the client moved on.</summary>
    internal void RequestRead()
    {
        if (Interlocked.Increment(ref _requestsServed) > 1)
        {
            Release();
        }
    }

    /// <summary>Signals that the connection is released. Idempotent.</summary>
    internal void Release() => _released.TrySetResult();
}
