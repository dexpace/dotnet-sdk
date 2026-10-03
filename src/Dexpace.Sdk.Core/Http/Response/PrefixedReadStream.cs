// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// The single-use stream the over-cap regime of <see cref="LoggingResponseBody"/> serves (BODY-24, BODY-25, BODY-27): the
/// captured prefix, then the staged overflow bytes, then the live tail of the delegate.
/// </summary>
/// <remarks>
/// A read never issues a zero-count read to the tail: a caller asking for zero bytes gets zero without touching it, so a
/// source cannot be made to spin (BODY-25, design §10 entry 4). Reaching the end of the tail, and disposing this stream,
/// both route to the wrapper's shared close-once guard, so the delegate is released at most once however it is reached
/// (BODY-27). The prefix array is the wrapper's captured buffer and is only ever read. Not seekable.
/// </remarks>
internal sealed class PrefixedReadStream : Stream
{
    private readonly byte[] _prefix;
    private readonly int _prefixLength;
    private readonly byte[] _staged;
    private readonly Stream _tail;
    private readonly Action _closeDelegate;
    private readonly Func<ValueTask> _closeDelegateAsync;
    private int _prefixPosition;
    private int _stagedPosition;
    private int _disposed;
    private bool _ended;

    internal PrefixedReadStream(
        byte[] prefix,
        int prefixLength,
        byte[]? staged,
        Stream tail,
        Action closeDelegate,
        Func<ValueTask> closeDelegateAsync)
    {
        _prefix = prefix;
        _prefixLength = prefixLength;
        _staged = staged ?? [];
        _tail = tail;
        _closeDelegate = closeDelegate;
        _closeDelegateAsync = closeDelegateAsync;
    }

    public override bool CanRead => Volatile.Read(ref _disposed) == 0;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        if (buffer.IsEmpty)
        {
            return 0;
        }

        var served = ServeFromMemory(buffer);
        if (served > 0 || _ended)
        {
            return served;
        }

        Debug.Assert(buffer.Length > 0, "BODY-25: never issue a zero-count read to the tail.");
        var read = _tail.Read(buffer);
        if (read == 0)
        {
            _ended = true;
            _closeDelegate();
        }

        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (buffer.IsEmpty)
        {
            return 0;
        }

        var served = ServeFromMemory(buffer.Span);
        if (served > 0 || _ended)
        {
            return served;
        }

        Debug.Assert(buffer.Length > 0, "BODY-25: never issue a zero-count read to the tail.");
        var read = await _tail.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            _ended = true;
            await _closeDelegateAsync().ConfigureAwait(false);
        }

        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                await _closeDelegateAsync().ConfigureAwait(false);
            }
            finally
            {
                // The base routes to Dispose(true), which finds the latch already taken.
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }

        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _closeDelegate();
        }

        base.Dispose(disposing);
    }

    // The prefix first, then the staged overflow; 0 means the memory is exhausted and the tail is next.
    private int ServeFromMemory(Span<byte> buffer)
    {
        var served = CopyFrom(_prefix, _prefixLength, ref _prefixPosition, buffer);
        if (served == 0)
        {
            served = CopyFrom(_staged, _staged.Length, ref _stagedPosition, buffer);
        }

        return served;
    }

    private static int CopyFrom(byte[] source, int length, ref int position, Span<byte> buffer)
    {
        var count = Math.Min(length - position, buffer.Length);
        if (count <= 0)
        {
            return 0;
        }

        source.AsSpan(position, count).CopyTo(buffer);
        position += count;
        return count;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
