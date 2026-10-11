// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;

namespace Dexpace.Sdk.Conformance.Tests.RawSocket;

/// <summary>
/// The response body as a read-only stream framed by <c>Content-Length</c>, chunked coding or the end of the connection
/// (RFC 9112 section 6.3). It owns the exchange: disposing it closes the socket, which is what releases the connection. A
/// connection that closes before a declared length or a chunk is complete is an <see cref="IOException"/>.
/// </summary>
internal sealed class FramedBodyStream : Stream
{
    private readonly RawSocketReader _reader;
    private readonly RawSocketExchange _exchange;
    private readonly bool _chunked;
    private long _remaining;
    private bool _done;
    private int _disposed;

    private FramedBodyStream(RawSocketReader reader, RawSocketExchange exchange, bool chunked, long length)
    {
        _reader = reader;
        _exchange = exchange;
        _chunked = chunked;
        _remaining = length;
    }

    /// <summary>A body of exactly <paramref name="length"/> bytes.</summary>
    internal static FramedBodyStream Fixed(RawSocketReader reader, RawSocketExchange exchange, long length) => new(reader, exchange, false, length);

    /// <summary>A body that ends when the connection does.</summary>
    internal static FramedBodyStream UntilClose(RawSocketReader reader, RawSocketExchange exchange) => new(reader, exchange, false, -1);

    /// <summary>A <c>Transfer-Encoding: chunked</c> body.</summary>
    internal static FramedBodyStream Chunked(RawSocketReader reader, RawSocketExchange exchange) => new(reader, exchange, true, 0);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        if (_done || buffer.Length == 0)
        {
            return 0;
        }

        if (_chunked)
        {
            return await ReadChunkedAsync(buffer, cancellationToken);
        }

        if (_remaining < 0)
        {
            var read = await _reader.ReadAsync(buffer, cancellationToken);
            _done = read == 0;
            return read;
        }

        var count = await _reader.ReadAsync(buffer[..(int)Math.Min(_remaining, buffer.Length)], cancellationToken);
        if (count == 0)
        {
            throw new IOException("The server closed the connection before the declared Content-Length was read.");
        }

        _remaining -= count;
        _done = _remaining == 0;
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override int Read(Span<byte> buffer)
    {
        var rented = new byte[buffer.Length];
        var read = ReadAsync(rented).AsTask().GetAwaiter().GetResult();
        rented.AsSpan(0, read).CopyTo(buffer);
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _exchange.Dispose();
        }

        base.Dispose(disposing);
    }

    private async ValueTask<int> ReadChunkedAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_remaining == 0)
        {
            var sizeLine = await _reader.ReadLineAsync(cancellationToken) ?? throw new IOException("The server closed the connection inside a chunked body.");
            var size = long.Parse(sizeLine.Split(';')[0].Trim(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            if (size == 0)
            {
                while (await _reader.ReadLineAsync(cancellationToken) is { Length: > 0 })
                {
                }

                _done = true;
                return 0;
            }

            _remaining = size;
        }

        var count = await _reader.ReadAsync(buffer[..(int)Math.Min(_remaining, buffer.Length)], cancellationToken);
        if (count == 0)
        {
            throw new IOException("The server closed the connection inside a chunk.");
        }

        _remaining -= count;
        if (_remaining == 0)
        {
            _ = await _reader.ReadLineAsync(cancellationToken);
        }

        return count;
    }
}
