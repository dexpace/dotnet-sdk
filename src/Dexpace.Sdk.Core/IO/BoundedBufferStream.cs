// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// A write-only in-memory sink that refuses to grow past a limit, used to buffer a request body for
/// <c>ToReplayable</c> and <c>ToReplayableAsync</c> (IO-9, P3a-12).
/// </summary>
/// <remarks>
/// A write that would take the total over the limit throws <see cref="BodyTooLargeException"/> naming the limit, never the
/// content. The buffer is pre-sized when the length is known. Both async write overloads are overridden so a write never
/// hops to the pool (design fact 1). Single-threaded contract (IO-37): one writer at a time.
/// </remarks>
internal sealed class BoundedBufferStream : Stream
{
    private readonly long _limit;
    private readonly ArrayBufferWriter<byte> _buffer;

    /// <summary>Initializes a new bounded buffer.</summary>
    /// <param name="limit">The most bytes the buffer accepts; non-negative.</param>
    /// <param name="expectedLength">The known length for pre-sizing, or a negative number when unknown.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is negative (IO-3).</exception>
    internal BoundedBufferStream(long limit, long expectedLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        _limit = limit;
        var presize = expectedLength > 0 ? Math.Min(Math.Min(expectedLength, limit), Array.MaxLength) : 0;
        _buffer = presize > 0 ? new ArrayBufferWriter<byte>((int)presize) : new ArrayBufferWriter<byte>();
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => _buffer.WrittenCount;

    /// <inheritdoc />
    public override long Position
    {
        get => _buffer.WrittenCount;
        set => throw new NotSupportedException();
    }

    /// <summary>An exact-size copy of the bytes written.</summary>
    /// <returns>A new array.</returns>
    internal byte[] ToArray() => _buffer.WrittenSpan.ToArray();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Write(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_buffer.WrittenCount + (long)buffer.Length > _limit)
        {
            throw new BodyTooLargeException(
                $"The body exceeds the limit of {_limit} bytes; stream it instead of buffering it.");
        }

        _buffer.Write(buffer);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
}
