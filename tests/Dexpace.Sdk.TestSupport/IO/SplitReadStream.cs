// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Streams;

/// <summary>
/// A read-only stream over a byte array that never returns bytes across a cut offset, so a test can place the boundary
/// between two reads exactly (a CR at the end of one read and its LF at the start of the next, the middle of a BOM, the
/// middle of a multi-byte character). It never returns zero before the end (IO-2).
/// </summary>
public sealed class SplitReadStream : Stream
{
    private readonly byte[] _data;
    private readonly int[] _cuts;
    private int _position;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="data">The bytes to serve.</param>
    /// <param name="cuts">
    /// The offsets at which a read must end: strictly ascending, each inside <c>(0, data.Length)</c>. No cuts means one
    /// read may serve everything.
    /// </param>
    public SplitReadStream(byte[] data, params int[] cuts)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(cuts);
        var previous = 0;
        foreach (var cut in cuts)
        {
            if (cut <= previous || cut >= data.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cuts),
                    "Cuts must be strictly ascending offsets inside (0, data.Length).");
            }

            previous = cut;
        }

        _data = data;
        _cuts = cuts;
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
        var boundary = _data.Length;
        foreach (var cut in _cuts)
        {
            if (cut > _position)
            {
                boundary = cut;
                break;
            }
        }

        var n = Math.Min(buffer.Length, boundary - _position);
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
