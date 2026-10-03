// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Streams;

/// <summary>
/// A write-only stream that records every written byte, the flush count and the token of each async call. It overrides
/// both async write overloads and, unlike a sync-only double, records the calling thread of each async write so a
/// pool-thread hop is visible (design fact 1).
/// </summary>
public sealed class RecordingWriteStream : Stream
{
    private readonly MemoryStream _written = new();
    private readonly List<CancellationToken> _tokens = [];
    private readonly List<int> _asyncWriteThreads = [];
    private int _flushCount;
    private int _syncWriteCalls;

    /// <summary>Every byte written, in order.</summary>
    public byte[] WrittenBytes => _written.ToArray();

    /// <summary>The number of <c>Flush</c> and <c>FlushAsync</c> calls.</summary>
    public int FlushCount => Volatile.Read(ref _flushCount);

    /// <summary>The number of synchronous write calls (<c>Write</c> and <c>WriteByte</c>).</summary>
    public int SyncWriteCalls => Volatile.Read(ref _syncWriteCalls);

    /// <summary>The token passed to each async write or flush, in order.</summary>
    public IReadOnlyList<CancellationToken> Tokens => [.. _tokens];

    /// <summary>The managed thread id each async write ran on.</summary>
    public IReadOnlyList<int> AsyncWriteThreadIds => [.. _asyncWriteThreads];

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
        Interlocked.Increment(ref _syncWriteCalls);
        _written.Write(buffer);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        RecordAsync(buffer.AsSpan(offset, count), cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        RecordAsync(buffer.Span, cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override void Flush() => Interlocked.Increment(ref _flushCount);

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        lock (_tokens)
        {
            _tokens.Add(cancellationToken);
        }

        Interlocked.Increment(ref _flushCount);
        return Task.CompletedTask;
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

    private void RecordAsync(ReadOnlySpan<byte> buffer, CancellationToken cancellationToken)
    {
        lock (_tokens)
        {
            _tokens.Add(cancellationToken);
            _asyncWriteThreads.Add(Environment.CurrentManagedThreadId);
        }

        _written.Write(buffer);
    }
}
