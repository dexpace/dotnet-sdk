// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// A write-only pass-through that enforces a declared length on what is written through it (HTTP-51; the multipart
/// length guard, design P3b-8, ported from Node's <c>boundedWriter</c>).
/// </summary>
/// <remarks>
/// When the limit is known, a chunk that would carry the total past it is refused <b>before</b> it is written, so a byte
/// past the stamped <c>Content-Length</c> never reaches the peer; <see cref="Complete"/> checks that the total reached the
/// limit. An unknown limit (<c>-1</c>) never refuses. Disposing it never disposes the primary: the destination belongs to
/// the transport (A8).
/// </remarks>
internal sealed class BoundedWriteStream : Stream
{
    private readonly Stream _primary;
    private readonly long _limit;
    private long _total;

    /// <summary>Initializes a new bounded stream over <paramref name="primary"/>.</summary>
    /// <param name="primary">The destination; it is never disposed by this stream.</param>
    /// <param name="limit">The exact byte count to allow, or <c>-1</c> for unbounded.</param>
    internal BoundedWriteStream(Stream primary, long limit)
    {
        ArgumentNullException.ThrowIfNull(primary);
        _primary = primary;
        _limit = limit;
    }

    /// <summary>The bytes written through this stream so far.</summary>
    internal long Total => _total;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Checks that the declared total was written.</summary>
    /// <exception cref="EndOfStreamException">A known limit was not reached.</exception>
    internal void Complete()
    {
        if (_limit >= 0 && _total != _limit)
        {
            throw new EndOfStreamException(StreamCopy.ShortTransferMessage(_total, _limit));
        }
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Admit(buffer.Length);
        _primary.Write(buffer);
        _total += buffer.Length;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Admit(buffer.Length);
        await _primary.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _total += buffer.Length;
    }

    /// <inheritdoc />
    public override void Flush() => _primary.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _primary.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    private void Admit(int count)
    {
        if (_limit >= 0 && _total + count > _limit)
        {
            throw new IOException(
                $"A multipart part wrote more than its declared length of {_limit} bytes; the extra chunk was refused before it was written.");
        }
    }
}
