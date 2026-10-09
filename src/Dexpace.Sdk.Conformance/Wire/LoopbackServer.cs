// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Dexpace.Sdk.Conformance.Wire;

/// <summary>
/// A minimal HTTP/1.1 server over a <see cref="TcpListener"/> on the loopback interface, for wire-level transport
/// tests (design §9.3, roadmap constraint 4). It records every request byte-for-byte (<see cref="Requests"/>) and
/// answers from a script of raw <see cref="LoopbackResponse"/>s, so a test can assert on exactly what a transport put
/// on the socket and can feed it replies a conforming server would refuse to send.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not Kestrel and not an <c>HttpMessageHandler</c> stub: a handler stub cannot see the socket, and a
/// conforming server normalises exactly what these tests need to observe. It speaks just enough HTTP/1.1 to frame a
/// request (the header block, then a <c>Content-Length</c> or chunked body) and nothing more: no validation, no
/// normalisation, no <c>100-continue</c>. Each connection may carry several requests; a reply whose
/// <see cref="LoopbackResponse.CloseConnection"/> is set ends its connection.
/// </para>
/// <para>
/// <b>Library contract.</b> The type uses the base class library only and no test-framework type, so it runs under any
/// runner; every <c>await</c> in it is configured (<c>ConfigureAwait(false)</c>), so it never needs the caller's context.
/// The observation members (<see cref="Requests"/>, <see cref="Faults"/>, <see cref="ConnectionCount"/>,
/// <see cref="WaitForRequestAsync"/> and <see cref="WaitForConnectionReleasedAsync"/>) are safe to call from any thread,
/// at any time, concurrently with the exchange they observe: each returns a snapshot or a condition to wait on, never a
/// live collection. The responder passed to <see cref="Start(Func{RecordedRequest, LoopbackResponse})"/> runs on the
/// connection's own task, so requests on different connections may be answered concurrently and a stateful responder must
/// synchronise itself.
/// </para>
/// </remarks>
public sealed class LoopbackServer : IAsyncDisposable
{
    private const int WriteSliceBytes = 64 * 1024;

    private readonly TcpListener _listener;
    private readonly Func<RecordedRequest, LoopbackResponse> _respond;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConnectionTracker _tracker = new();
    private readonly ConcurrentQueue<Exception> _faults = new();
    private readonly ConcurrentBag<Task> _connections = [];
    private readonly Task _acceptLoop;
    private int _connectionCount;
    private int _disposed;

    private LoopbackServer(Func<RecordedRequest, LoopbackResponse> respond)
    {
        _respond = respond;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        _acceptLoop = AcceptLoopAsync();
    }

    /// <summary>The server's root, <c>http://127.0.0.1:{port}/</c>.</summary>
    public Uri BaseUri { get; }

    /// <summary>Every request received so far, in arrival order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => _tracker.Requests();

    /// <summary>
    /// Failures inside the server, never rethrown (not even by <see cref="DisposeAsync"/>): a request whose framing
    /// cannot be parsed (a <see cref="MalformedRequestException"/> carrying the bytes received, after which the server
    /// answers <c>400</c> and closes the connection), a request beyond the end of the script or a responder that threw
    /// (answered <c>500</c>), and any other unexpected error on a connection. A test that expects a clean exchange
    /// asserts this is empty.
    /// </summary>
    public IReadOnlyList<Exception> Faults => [.. _faults];

    /// <summary>The number of connections accepted so far.</summary>
    public int ConnectionCount => Volatile.Read(ref _connectionCount);

    /// <summary>
    /// Starts a server that answers request <c>n</c> with <paramref name="script"/> entry <c>n</c>. A request past the
    /// end of the script gets a <c>500</c> and is recorded in <see cref="Faults"/>.
    /// </summary>
    /// <param name="script">The replies, in order.</param>
    public static LoopbackServer Start(params IEnumerable<LoopbackResponse> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var queue = new ConcurrentQueue<LoopbackResponse>(script);
        return new LoopbackServer(request => queue.TryDequeue(out var next)
            ? next
            : throw new InvalidOperationException(
                $"Loopback script exhausted at request {request.RequestLine}."));
    }

    /// <summary>Starts a server that builds each reply from the request it answers.</summary>
    /// <param name="respond">Builds the reply to a recorded request.</param>
    public static LoopbackServer Start(Func<RecordedRequest, LoopbackResponse> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);
        return new LoopbackServer(respond);
    }

    /// <summary>An absolute URL on this server.</summary>
    /// <param name="pathAndQuery">A path, with an optional query, relative to <see cref="BaseUri"/>.</param>
    public Uri Url(string pathAndQuery) => new(BaseUri, pathAndQuery);

    /// <summary>
    /// Completes with request <paramref name="index"/> (arrival order, zero-based) once it has been read: a condition to wait
    /// on instead of a sleep. Completes at once when it has already arrived.
    /// </summary>
    /// <param name="index">The zero-based arrival index.</param>
    /// <param name="cancellationToken">Ends the wait with <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative (thrown synchronously).</exception>
    /// <exception cref="ObjectDisposedException">The server was disposed before the request arrived (delivered through the task).</exception>
    public Task<RecordedRequest> WaitForRequestAsync(int index, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return WaitForRequestCoreAsync(index, cancellationToken);
    }

    /// <summary>
    /// Completes when the client has <em>released</em> connection <paramref name="connection"/> (accept order, zero-based):
    /// closed it, or sent a further request on it. "Released" is either, because a pooling client returns a connection to
    /// its pool rather than closing it; a leaked response is a connection that does neither. It also completes when the
    /// server tears the connection down (after a <c>Connection: close</c> reply, an aborted reply, or on disposal); a test
    /// that asserts the <em>client</em> released a connection must use a reply that leaves it open, because after a
    /// server-side close the wait proves nothing. Waits for a connection not yet accepted.
    /// </summary>
    /// <param name="connection">The zero-based connection index.</param>
    /// <param name="cancellationToken">Ends the wait with <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="connection"/> is negative (thrown synchronously).</exception>
    /// <exception cref="ObjectDisposedException">The server was disposed before the connection was accepted (delivered through the task).</exception>
    public Task WaitForConnectionReleasedAsync(int connection, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(connection);
        return WaitForConnectionReleasedCoreAsync(connection, cancellationToken);
    }

    /// <summary>Whether the server closed or reset connection <paramref name="connection"/> before the client left it.</summary>
    /// <param name="connection">The zero-based connection index of an accepted connection.</param>
    internal bool ServerClosedFirst(int connection) => _tracker.Existing(connection).ServerClosedFirst;

    private async Task<RecordedRequest> WaitForRequestCoreAsync(int index, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        try
        {
            while (true)
            {
                var (request, arrival) = _tracker.RequestOrSignal(index);
                if (request is not null)
                {
                    return request;
                }

                await arrival.WaitAsync(linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(LoopbackServer));
        }
    }

    private async Task WaitForConnectionReleasedCoreAsync(int connection, CancellationToken cancellationToken)
    {
        ConnectionState state;
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token))
        {
            try
            {
                state = await _tracker.ConnectionAsync(connection, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(LoopbackServer));
            }
        }

        // Disposal tears every accepted connection down, which releases it, so this wait is not linked to the server's
        // own cancellation: a connection that exists is always released eventually.
        await state.Released.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops listening, closes every open connection, and waits for the server's work to finish. Idempotent, and never
    /// throws a request-handling failure: those are in <see cref="Faults"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        await IgnoreShutdownAsync(_acceptLoop).ConfigureAwait(false);
        foreach (var connection in _connections)
        {
            await IgnoreShutdownAsync(connection).ConfigureAwait(false);
        }

        _listener.Dispose();
        _stopping.Dispose();
    }

    private async Task IgnoreShutdownAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException or IOException)
        {
            // The server is shutting down; a torn connection is expected.
        }
#pragma warning disable CA1031 // Not swallowed: recorded in Faults; disposal must not mask the test's own failure.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _faults.Enqueue(ex);
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
            Interlocked.Increment(ref _connectionCount);
            _connections.Add(ServeAsync(client, _tracker.Accept()));
        }
    }

    private async Task ServeAsync(TcpClient client, ConnectionState state)
    {
        using (client)
        {
            try
            {
                await ServeRequestsAsync(client, state).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException or IOException)
            {
                // The server is shutting down, or the client left mid-exchange (closed or reset a connection it was still
                // reading): both expected, and neither is a fault of the server.
            }
#pragma warning disable CA1031 // Not swallowed: recorded in Faults, which the test asserts on.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _faults.Enqueue(ex);
            }
            finally
            {
                // Any exit of the connection releases it (P8a-21): the client closed it, or the server did.
                state.Release();
            }
        }
    }

    private async Task ServeRequestsAsync(TcpClient client, ConnectionState state)
    {
        // Taken once: TcpClient.GetStream() throws after the server has half-closed the connection.
        var stream = client.GetStream();
        var reader = new RawRequestReader(stream);
        while (!_stopping.IsCancellationRequested)
        {
            var request = await ReadRequestAsync(client, stream, reader, state).ConfigureAwait(false);
            if (request is null)
            {
                return;
            }

            _tracker.Record(request);
            state.RequestRead();
            if (await WriteReplyAsync(client, stream, state, Reply(request)).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>Reads the next request; <see langword="null"/> when the peer closed between requests or the request was malformed.</summary>
    private async Task<RecordedRequest?> ReadRequestAsync(TcpClient client, NetworkStream stream, RawRequestReader reader, ConnectionState state)
    {
        try
        {
            return await reader.ReadAsync(state.Index, _stopping.Token).ConfigureAwait(false);
        }
        catch (MalformedRequestException ex)
        {
            // Record what reached the socket, then answer, so the client sees a reply rather than a hang.
            if (ex.Partial is { } partial)
            {
                _tracker.Record(partial);
            }

            _faults.Enqueue(ex);
            await WriteReplyAsync(client, stream, state, LoopbackResponse.Status(400, "Loopback Malformed Request")).ConfigureAwait(false);
            await DrainAsync(stream).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>
    /// Reads and discards what the peer still sends, until it closes or a short grace period ends, so that closing a
    /// socket with unread input does not reset the connection and destroy the reply before the peer reads it.
    /// </summary>
    private async Task DrainAsync(Stream stream)
    {
        using var grace = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        grace.CancelAfter(TimeSpan.FromSeconds(2));
        var sink = new byte[4096];
        try
        {
            while (await stream.ReadAsync(sink, grace.Token).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            // Grace period over, or the peer reset: either way there is nothing left to protect.
        }
    }

    /// <summary>Plays one scripted reply; returns whether the connection ended with it.</summary>
    private async Task<bool> WriteReplyAsync(TcpClient client, NetworkStream stream, ConnectionState state, LoopbackResponse reply)
    {
        // A gated reply waits for its gate, watching the connection so that a client that gives up releases it.
        while (reply.Gate is { } gate)
        {
            if (!await WaitForGateAsync(stream, gate).ConfigureAwait(false))
            {
                return true;
            }

            reply = reply.Then!;
        }

        switch (reply.Mode)
        {
            case ReplyMode.Abort:
                // Marked before the reset, and the reset before the release: a release check can tell this from a client leaving.
                state.MarkServerClosedFirst();
                client.Client.LingerState = new LingerOption(true, 0);
                client.Dispose();
                return true;
            case ReplyMode.Hang:
                await HangAsync(stream).ConfigureAwait(false);
                return true;
            default:
                return await WriteBytesAsync(client, stream, state, reply).ConfigureAwait(false);
        }
    }

    private async Task<bool> WriteBytesAsync(TcpClient client, NetworkStream stream, ConnectionState state, LoopbackResponse reply)
    {
        // In slices, so a client that disposes mid-body makes a later write fail instead of the whole body sitting in a buffer.
        for (var offset = 0; offset < reply.Bytes.Length; offset += WriteSliceBytes)
        {
            await stream.WriteAsync(reply.Bytes.Slice(offset, Math.Min(WriteSliceBytes, reply.Bytes.Length - offset)), _stopping.Token).ConfigureAwait(false);
        }

        await stream.FlushAsync(_stopping.Token).ConfigureAwait(false);
        if (reply.Chunks is { } chunks)
        {
            await WriteChunksAsync(stream, chunks).ConfigureAwait(false);
        }

        if (!reply.CloseConnection)
        {
            return false;
        }

        // Before the release is signalled (in ServeAsync's finally): a release check can then tell a server-side close
        // from a client-side one (plan reading R13).
        state.MarkServerClosedFirst();
        client.Client.Shutdown(SocketShutdown.Send);
        return true;
    }

    /// <summary>
    /// Waits for <paramref name="gate"/> while reading the connection for the peer leaving. Returns <see langword="true"/>
    /// when the gate opened and the reply may go ahead, <see langword="false"/> when the peer left first.
    /// </summary>
    private async Task<bool> WaitForGateAsync(NetworkStream stream, Task gate)
    {
        using var stopWatching = new CancellationTokenSource();
        var watch = WatchPeerAsync(stream, stopWatching.Token);
        try
        {
            var first = await Task.WhenAny(gate.WaitAsync(_stopping.Token), watch).ConfigureAwait(false);
            if (first == watch)
            {
                return await watch.ConfigureAwait(false) switch
                {
                    PeerEvent.Left => false,
                    _ => throw new InvalidOperationException("The client sent bytes while a gated reply was waiting; the fixture does not support pipelining."),
                };
            }

            await first.ConfigureAwait(false);
            return true;
        }
        finally
        {
            // The pending one-byte read must not outlive the gate: the next request is read through RawRequestReader.
            await stopWatching.CancelAsync().ConfigureAwait(false);
            await watch.ConfigureAwait(false);
        }
    }

    private static async Task<PeerEvent> WatchPeerAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await stream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) == 0 ? PeerEvent.Left : PeerEvent.Sent;
        }
        catch (OperationCanceledException)
        {
            return PeerEvent.Stopped;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            return PeerEvent.Left;
        }
    }

    /// <summary>Reads and discards until the peer leaves; the server's disposal cancels it.</summary>
    private async Task HangAsync(NetworkStream stream)
    {
        var sink = new byte[256];
        while (await stream.ReadAsync(sink, _stopping.Token).ConfigureAwait(false) > 0)
        {
        }
    }

    /// <summary>
    /// Writes a streamed body as chunked transfer-encoding: each chunk is size, CRLF, data, CRLF, flushed before the
    /// next one is requested from the script, then the terminating zero chunk.
    /// </summary>
    private async Task WriteChunksAsync(NetworkStream stream, IAsyncEnumerable<byte[]> chunks)
    {
        await foreach (var chunk in chunks.WithCancellation(_stopping.Token).ConfigureAwait(false))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"{chunk.Length:X}\r\n"), _stopping.Token).ConfigureAwait(false);
            await stream.WriteAsync(chunk, _stopping.Token).ConfigureAwait(false);
            await stream.WriteAsync("\r\n"u8.ToArray(), _stopping.Token).ConfigureAwait(false);
            await stream.FlushAsync(_stopping.Token).ConfigureAwait(false);
        }

        await stream.WriteAsync("0\r\n\r\n"u8.ToArray(), _stopping.Token).ConfigureAwait(false);
        await stream.FlushAsync(_stopping.Token).ConfigureAwait(false);
    }

    private LoopbackResponse Reply(RecordedRequest request)
    {
        try
        {
            return _respond(request);
        }
#pragma warning disable CA1031 // Not swallowed: the failure is recorded in Faults and answered with a 500.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _faults.Enqueue(ex);
            return LoopbackResponse.Status(500, "Loopback Script Failure");
        }
    }

    private enum PeerEvent
    {
        Left,
        Sent,
        Stopped,
    }
}
