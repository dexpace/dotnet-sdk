// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Loopback;

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
/// Self-contained and framework-free (BCL only, no test-framework types) because roadmap phase 8a promotes it into
/// <c>Dexpace.Sdk.Conformance</c>.
/// </para>
/// </remarks>
public sealed class LoopbackServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Func<RecordedRequest, LoopbackResponse> _respond;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
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
    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

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
    /// Stops listening, closes every open connection, and waits for the server's work to finish. Idempotent, and never
    /// throws a request-handling failure: those are in <see cref="Faults"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync();
        _listener.Stop();
        await IgnoreShutdownAsync(_acceptLoop);
        foreach (var connection in _connections)
        {
            await IgnoreShutdownAsync(connection);
        }

        _listener.Dispose();
        _stopping.Dispose();
    }

    private async Task IgnoreShutdownAsync(Task task)
    {
        try
        {
            await task;
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
            var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            var index = Interlocked.Increment(ref _connectionCount) - 1;
            _connections.Add(ServeAsync(client, index));
        }
    }

    private async Task ServeAsync(TcpClient client, int connection)
    {
        using (client)
        {
            try
            {
                await ServeRequestsAsync(client, connection);
            }
            catch (Exception ex) when (_stopping.IsCancellationRequested
                && ex is OperationCanceledException or ObjectDisposedException or SocketException or IOException)
            {
                // The server is shutting down; a torn connection is expected.
            }
#pragma warning disable CA1031 // Not swallowed: recorded in Faults, which the test asserts on.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _faults.Enqueue(ex);
            }
        }
    }

    private async Task ServeRequestsAsync(TcpClient client, int connection)
    {
        var stream = client.GetStream();
        var reader = new RawRequestReader(stream);
        while (!_stopping.IsCancellationRequested)
        {
            RecordedRequest? request;
            try
            {
                request = await reader.ReadAsync(connection, _stopping.Token);
            }
            catch (MalformedRequestException ex)
            {
                // Record what reached the socket, then answer, so the client sees a reply rather than a hang.
                if (ex.Partial is { } partial)
                {
                    _requests.Enqueue(partial);
                }

                _faults.Enqueue(ex);
                await WriteAsync(client, LoopbackResponse.Status(400, "Loopback Malformed Request"));
                await DrainAsync(stream);
                return;
            }

            if (request is null)
            {
                return;
            }

            _requests.Enqueue(request);
            if (await WriteAsync(client, Reply(request)))
            {
                return;
            }
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
            while (await stream.ReadAsync(sink, grace.Token) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            // Grace period over, or the peer reset: either way there is nothing left to protect.
        }
    }

    /// <summary>Writes a reply; returns whether the connection was closed after it.</summary>
    private async Task<bool> WriteAsync(TcpClient client, LoopbackResponse reply)
    {
        var stream = client.GetStream();
        await stream.WriteAsync(reply.Bytes, _stopping.Token);
        await stream.FlushAsync(_stopping.Token);
        if (!reply.CloseConnection)
        {
            return false;
        }

        client.Client.Shutdown(SocketShutdown.Send);
        return true;
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
}
