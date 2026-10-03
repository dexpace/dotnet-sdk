// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Streams;

/// <summary>
/// Wraps a readable stream and throws <see cref="InvalidOperationException"/> on any zero-count read (the IO-2 caller-side
/// rule: core never issues one). It records every requested count. It never disposes the inner stream on its own account
/// beyond forwarding disposal.
/// </summary>
public sealed class StrictReadStream : Stream
{
    private readonly Stream _inner;
    private readonly List<int> _requested = [];

    /// <summary>Initializes a new instance.</summary>
    /// <param name="inner">The stream to read from.</param>
    public StrictReadStream(Stream inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>The count requested by each read, in order.</summary>
    public IReadOnlyList<int> RequestedCounts
    {
        get
        {
            lock (_requested)
            {
                return [.. _requested];
            }
        }
    }

    /// <summary>Whether any read was issued.</summary>
    public bool AnyRead => RequestedCounts.Count > 0;

    /// <inheritdoc />
    public override bool CanRead => _inner.CanRead;

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
    public override int Read(byte[] buffer, int offset, int count)
    {
        Record(count);
        return _inner.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        Record(buffer.Length);
        return _inner.Read(buffer);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Record(count);
        return _inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Record(buffer.Length);
        return _inner.ReadAsync(buffer, cancellationToken);
    }

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

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Record(int count)
    {
        lock (_requested)
        {
            _requested.Add(count);
        }

        if (count <= 0)
        {
            throw new InvalidOperationException("A zero-count read was issued (IO-2).");
        }
    }
}
