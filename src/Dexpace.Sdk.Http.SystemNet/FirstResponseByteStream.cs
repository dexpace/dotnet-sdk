// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// A pass-through wrapper over a connection's plaintext stream that stops <c>SocketsHttpHandler</c> re-sending a request whose
/// connection ended before the server sent a single response byte (<c>TRANSPORT-2</c>; design §10 entry 33, P11-12). It is installed as
/// the handler's <c>PlaintextStreamFilter</c> by <see cref="SystemNetHttpClient.CreatePrimaryHandler"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> The handler retries, up to three more times and with no switch to stop it, a request with no content when the first read
/// of its response returns zero bytes (runtime 10.0, <c>HttpConnection.SendAsync</c>). The SDK's retry layer is the one place a request is
/// re-sent, under the classifier and the re-send gate (<c>RETRY-2</c>, <c>RETRY-4</c>). For a <c>POST</c>, <c>PUT</c> or <c>PATCH</c> without a
/// body the transport closes the clause by giving it a zero-length content; <c>GET</c>, <c>HEAD</c>, <c>OPTIONS</c>, <c>DELETE</c> and
/// <c>TRACE</c> are idempotent and cannot be given content (<c>TRANSPORT-26</c>, RFC 9110 section 8.6). The runtime takes the retry only on
/// a zero-byte read, not on an exception, so this stream turns that one read into the <see cref="HttpIOException"/> (<see cref="HttpRequestError.ResponseEnded"/>)
/// the runtime raises itself when a response ends prematurely after its first byte, so the failure reads as the same one it was before and the
/// request fails once instead of arriving four times.
/// </para>
/// <para>
/// <b>What it does.</b> It tracks one fact: a write has happened and no response byte has been read since. In that state a read with room
/// for at least a byte that returns 0 throws that <see cref="HttpIOException"/>; every other read, every write, every flush and every dispose passes through to the inner
/// stream unchanged, in order, and no byte is inspected, buffered or altered (the contrast with a filter that rewrote the response head,
/// which §10 entry 32 declines). An idle connection that the server closes still reads 0, because no request was written after its last
/// response byte; a zero-count read is a request to wait for data and says nothing about the end of the stream, so it is never judged.
/// </para>
/// <para>
/// <b>Reading the limit.</b> A response body that ends with the connection (no length, not chunked) reads 0 at its end; the state is clear
/// by then, because the response began with a byte. A request body still being written after an early response re-arms the state, so a
/// connection-delimited response to such a request would report its end as that exception; HTTP/1.1 servers delimit by length
/// or chunks, so the case is a server that answers early, closes to end the body and still reads the upload. The filter is installed for
/// HTTP/1.x connections only: an HTTP/2 connection multiplexes, has no such retry on a plain end of stream, and is passed through untouched.
/// </para>
/// <para>
/// A handler the caller builds does not get the filter; its retry is the caller's native client's (design §11 item 71). The wrapper owns the
/// stream it wraps: disposing it disposes the inner stream once.
/// </para>
/// </remarks>
internal sealed class FirstResponseByteStream(Stream inner) : Stream
{
    private int _awaitingFirstByte;
    private int _disposed;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Observe(inner.Read(buffer, offset, count), count);

    public override int Read(Span<byte> buffer) => Observe(inner.Read(buffer), buffer.Length);

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Observe(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false), count);

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var requested = buffer.Length;
        return Observe(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false), requested);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Arm(count);
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Arm(buffer.Length);
        inner.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Arm(count);
        return inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Arm(buffer.Length);
        return inner.WriteAsync(buffer, cancellationToken);
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await inner.DisposeAsync().ConfigureAwait(false);
        }

        // The base routes to Dispose(true), which finds the latch already taken and releases nothing twice.
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    // A write of at least a byte is a request on its way (or the rest of its body): nothing has been read of the answer yet.
    private void Arm(int count)
    {
        if (count > 0)
        {
            Volatile.Write(ref _awaitingFirstByte, 1);
        }
    }

    // A byte read ends the wait. A read that returns 0 while the wait is on is a connection that ended before any response byte: it throws
    // the HttpIOException(ResponseEnded) the runtime raises itself for a response that ends early, so the runtime's own re-send of the
    // request is never offered and the failure keeps the classification (and the error code) it had. A zero-count read is a wait for
    // data, not a read of the stream's end, so it is passed through without a verdict.
    private int Observe(int read, int requested)
    {
        if (requested == 0)
        {
            return read;
        }

        if (read > 0)
        {
            Volatile.Write(ref _awaitingFirstByte, 0);
            return read;
        }

        if (Volatile.Read(ref _awaitingFirstByte) != 0)
        {
            throw new HttpIOException(HttpRequestError.ResponseEnded, "The connection closed before any byte of the response was received.");
        }

        return 0;
    }
}
