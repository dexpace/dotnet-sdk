// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net.Sockets;

namespace Dexpace.Sdk.Conformance.Tests.RawSocket;

/// <summary>
/// One call's connection: a socket opened for the call and closed with it, never pooled. A response's body takes over this
/// exchange when the head has been read, so the connection is released when the body is disposed.
/// </summary>
internal sealed class RawSocketExchange : IDisposable
{
    private readonly TcpClient _client = new() { NoDelay = true };
    private int _closed;

    /// <summary>The stream, once <see cref="ConnectAsync"/> has completed.</summary>
    internal NetworkStream Stream => _client.GetStream();

    /// <summary>Opens the connection to <paramref name="host"/>:<paramref name="port"/>.</summary>
    internal async Task ConnectAsync(string host, int port, CancellationToken cancellationToken) =>
        await _client.ConnectAsync(host, port, cancellationToken);

    /// <summary>Closes the socket now, which fails any pending read or write: how cancellation reaches blocked I/O.</summary>
    internal void Abort() => Dispose();

    /// <summary>Closes the socket, once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            _client.Dispose();
        }
    }
}
