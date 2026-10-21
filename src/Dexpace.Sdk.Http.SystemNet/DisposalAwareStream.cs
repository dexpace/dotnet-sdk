// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// A read-through wrapper over an <see cref="HttpContent"/> read stream that reports its own disposal the way every other
/// body's stream does: once disposed, a read throws <see cref="ObjectDisposedException"/> and <see cref="CanRead"/> is
/// <see langword="false"/> (BODY-14, HTTP-41; design section 10 entry 6).
/// </summary>
/// <remarks>
/// The stream <c>SocketsHttpHandler</c> hands out for a response returns 0 from a read after it was disposed, which is a
/// clean end of stream to every consumer. A body returns the same stream on a second open, so a second consumer (a second
/// <c>HttpResponseException.GetError</c>, a convenience read after a manual one) is handed a stream the first consumer
/// disposed, and without this wrapper it read an empty payload instead of failing. It forwards every member, including the
/// bulk copies, so the inner stream's own fast paths are kept, and it adds one volatile read per call.
/// </remarks>
/// <param name="inner">The content stream; disposed with this one.</param>
internal sealed class DisposalAwareStream(Stream inner) : Stream
{
    private int _disposed;

    public override bool CanRead => IsLive && inner.CanRead;

    public override bool CanSeek => IsLive && inner.CanSeek;

    public override bool CanWrite => IsLive && inner.CanWrite;

    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return inner.Length;
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return inner.Position;
        }

        set
        {
            ThrowIfDisposed();
            inner.Position = value;
        }
    }

    private bool IsLive => Volatile.Read(ref _disposed) == 0;

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        return inner.Read(buffer, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        return inner.Read(buffer);
    }

    public override int ReadByte()
    {
        ThrowIfDisposed();
        return inner.ReadByte();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return inner.ReadAsync(buffer, cancellationToken);
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ThrowIfDisposed();
        inner.CopyTo(destination, bufferSize);
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return inner.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        inner.Write(buffer, offset, count);
    }

    public override void Flush()
    {
        ThrowIfDisposed();
        inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return inner.FlushAsync(cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        return inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        ThrowIfDisposed();
        inner.SetLength(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await inner.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(!IsLive, this);
}
