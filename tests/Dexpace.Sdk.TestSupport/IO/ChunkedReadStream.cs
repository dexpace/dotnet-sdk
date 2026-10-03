// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Streams;

/// <summary>
/// A read-only stream over a byte array that returns at most <c>maxPerRead</c> bytes per read, the "network reads are
/// short" fact of design §3.1, so every boundary of a helper is exercised.
/// </summary>
public sealed class ChunkedReadStream : Stream
{
    private readonly byte[] _data;
    private readonly int _maxPerRead;
    private int _position;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="data">The bytes to serve.</param>
    /// <param name="maxPerRead">The most bytes any one read returns; at least 1.</param>
    public ChunkedReadStream(byte[] data, int maxPerRead)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPerRead, 1);
        _data = data;
        _maxPerRead = maxPerRead;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var n = Math.Min(Math.Min(buffer.Length, _maxPerRead), _data.Length - _position);
        _data.AsSpan(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer.AsSpan(offset, count)));

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(Read(buffer.Span));

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
