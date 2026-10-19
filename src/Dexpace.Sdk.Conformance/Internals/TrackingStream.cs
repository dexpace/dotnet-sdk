// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A caller-owned stream for the codec suite: it reads and writes an in-memory buffer, counts how often it was closed (the codec must not
/// close it, <c>SERDE-3</c>) and, when given a failure, throws it from every read and write (a genuine stream I/O error that must propagate
/// unwrapped, <c>SERDE-12</c>). Closing it does not release the buffer, so the assertion can still read what was written.
/// </summary>
internal sealed class TrackingStream : Stream
{
    private readonly MemoryStream _buffer;
    private readonly IOException? _failure;
    private int _closes;

    /// <summary>Creates a stream over <paramref name="initial"/> (empty when none).</summary>
    /// <param name="initial">The bytes to read.</param>
    /// <param name="failure">The error to throw from every read and write, or <see langword="null"/> for none.</param>
    internal TrackingStream(byte[]? initial = null, IOException? failure = null)
    {
        _buffer = initial is null ? new MemoryStream() : new MemoryStream(initial, writable: true) { Position = 0 };
        _failure = failure;
    }

    /// <summary>How many times the stream was closed or disposed.</summary>
    internal int Closes => Volatile.Read(ref _closes);

    /// <summary>The bytes written so far.</summary>
    internal byte[] Written => _buffer.ToArray();

    /// <summary>The stream's length, readable after it was closed.</summary>
    internal long Total => _buffer.Length;

    /// <summary>The read position, readable after it was closed.</summary>
    internal long Cursor => _buffer.Position;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => _buffer.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _buffer.Position;
        set => _buffer.Position = value;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        ThrowIfClosedOrFaulting();
        return _buffer.Read(buffer);
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            return ValueTask.FromResult(Read(buffer.Span));
        }
#pragma warning disable CA1031 // The failure belongs in the returned task, as any asynchronous stream delivers it.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return ValueTask.FromException<int>(ex);
        }
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfClosedOrFaulting();
        _buffer.Write(buffer);
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
#pragma warning disable CA1031 // The failure belongs in the returned task, as any asynchronous stream delivers it.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return ValueTask.FromException(ex);
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _buffer.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => _buffer.SetLength(value);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Increment(ref _closes);
        }

        base.Dispose(disposing);
    }

    private void ThrowIfClosedOrFaulting()
    {
        if (_failure is not null)
        {
            throw _failure;
        }

        ObjectDisposedException.ThrowIf(Closes > 0, this);
    }
}
