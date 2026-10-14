// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A forward-only stream of a fixed length, generated on demand: a large body that costs no memory to hold, whose every byte a
/// consumer can verify, and that counts how much of it was pulled (<c>XCUT-24</c>). It opens with <see cref="Prefix"/>, printable
/// ASCII, so a log line or an error snapshot that carries the start of the body can be recognised as carrying it; every byte after
/// that at position <c>i</c> is <c>i mod 251</c>.
/// </summary>
internal sealed class PatternStream(long length) : Stream
{
    /// <summary>The opening of every body this stream produces: what a preview or a snapshot of it must show.</summary>
    internal const string Prefix = "XCUT24-PREVIEW-MARKER|";

    private static readonly byte[] s_prefix = System.Text.Encoding.ASCII.GetBytes(Prefix);

    private long _position;
    private long _pulled;

    /// <summary>How many bytes have been read from the stream so far.</summary>
    internal long BytesPulled => Interlocked.Read(ref _pulled);

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => length;

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    /// <summary>The byte at <paramref name="position"/>.</summary>
    /// <param name="position">A zero-based position.</param>
    internal static byte At(long position) => position < s_prefix.Length ? s_prefix[position] : (byte)(position % 251);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var take = (int)Math.Min(buffer.Length, length - _position);
        for (var i = 0; i < take; i++)
        {
            buffer[i] = At(_position + i);
        }

        _position += take;
        Interlocked.Add(ref _pulled, take);
        return take;
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

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
