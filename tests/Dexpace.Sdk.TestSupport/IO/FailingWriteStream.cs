// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Streams;

/// <summary>
/// A write-only stream that throws a given exception on the <em>n</em>-th write call (1-based, every write overload) and
/// records the bytes written before it.
/// </summary>
public sealed class FailingWriteStream : Stream
{
    private readonly int _failOnWriteNumber;
    private readonly Exception _failure;
    private readonly MemoryStream _written = new();
    private int _writeCalls;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="failOnWriteNumber">The 1-based write call that throws.</param>
    /// <param name="failure">The exception to throw.</param>
    public FailingWriteStream(int failOnWriteNumber, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        _failOnWriteNumber = failOnWriteNumber;
        _failure = failure;
    }

    /// <summary>The bytes written by the calls that did not fail.</summary>
    public byte[] WrittenBeforeFailure => _written.ToArray();

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

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (Interlocked.Increment(ref _writeCalls) == _failOnWriteNumber)
        {
            throw _failure;
        }

        _written.Write(buffer);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
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

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _written.Dispose();
        }

        base.Dispose(disposing);
    }
}
