// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// A write-only tee: every write is mirrored into a private tap (up to a limit) and then forwarded in full to a primary
/// stream (IO-4 to IO-6, IO-8, IO-25 to IO-29, IO-40 to IO-42; P3a-10).
/// </summary>
/// <remarks>
/// <para>
/// The primary receives every byte whatever the tap limit (IO-25). Each write mirrors first and forwards second, so a
/// failed primary write leaves its attempted bytes in the tap (IO-27), and there is no staging buffer because the caller's
/// span is forwarded directly. At the tap limit mirroring stops and forwarding continues (IO-26). No member hands out the
/// tap (IO-28); <see cref="SnapshotTap"/> returns a fresh copy and still works after disposal (IO-42).
/// </para>
/// <para>
/// <see cref="Flush"/>, <see cref="FlushAsync"/>, <see cref="Dispose(bool)"/> and <see cref="DisposeAsync"/> reach the
/// primary only (IO-5, IO-29). Disposal is latched with <see cref="Interlocked"/> so the primary is disposed at most
/// once across any mix of <see cref="Stream.Dispose()"/> and <see cref="DisposeAsync"/> (IO-41), unless
/// <c>leaveOpen</c> is set (IO-6). After disposal a write or a flush throws <see cref="ObjectDisposedException"/>
/// (IO-42). The caller's token is forwarded to the primary unchanged and any <see cref="OperationCanceledException"/>
/// it raises propagates as the same instance (IO-40).
/// </para>
/// <para>
/// Single-threaded contract (IO-37): <see cref="Stream"/>'s. One writer at a time; the type holds no shared state.
/// </para>
/// </remarks>
internal sealed class TeeStream : Stream
{
    /// <summary>The default tap limit: everything, clamped to <see cref="Array.MaxLength"/> internally (IO-26).</summary>
    internal const long Unbounded = long.MaxValue;

    private readonly Stream _primary;
    private readonly bool _leaveOpen;
    private readonly long _tapLimit;
    private readonly ArrayBufferWriter<byte> _tap = new();
    private int _disposed;

    /// <summary>Initializes a new tee over <paramref name="primary"/>.</summary>
    /// <param name="primary">The stream that receives every byte; it must be writable.</param>
    /// <param name="tapLimit">The most bytes the tap holds; zero mirrors nothing.</param>
    /// <param name="leaveOpen">When <see langword="true"/>, disposing the tee leaves <paramref name="primary"/> open.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tapLimit"/> is negative (IO-3).</exception>
    /// <exception cref="ArgumentException"><paramref name="primary"/> is not writable.</exception>
    internal TeeStream(Stream primary, long tapLimit = Unbounded, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentOutOfRangeException.ThrowIfNegative(tapLimit);
        if (!primary.CanWrite)
        {
            throw new ArgumentException("The primary stream must be writable.", nameof(primary));
        }

        _primary = primary;
        _tapLimit = Math.Min(tapLimit, Array.MaxLength);
        _leaveOpen = leaveOpen;
    }

    /// <summary>The number of bytes in the tap.</summary>
    internal long TapLength => _tap.WrittenCount;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => Volatile.Read(ref _disposed) == 0;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>A fresh copy of the mirrored bytes (IO-8, IO-28). It works after disposal (IO-42).</summary>
    /// <returns>A new array.</returns>
    internal byte[] SnapshotTap() => _tap.WrittenSpan.ToArray();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBuffer(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfDisposed();
        Mirror(buffer);
        _primary.Write(buffer);
    }

    /// <inheritdoc />
    public override void WriteByte(byte value)
    {
        ReadOnlySpan<byte> single = [value];
        Write(single);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBuffer(buffer, offset, count);
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Mirror(buffer.Span);
        return _primary.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override void Flush()
    {
        ThrowIfDisposed();
        _primary.Flush();
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _primary.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && !_leaveOpen)
        {
            await _primary.DisposeAsync().ConfigureAwait(false);
        }

        // The base class routes to Dispose(true), which finds the latch already taken and disposes nothing twice.
        await base.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0 && !_leaveOpen)
        {
            _primary.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void ValidateBuffer(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);
    }

    private void Mirror(ReadOnlySpan<byte> buffer)
    {
        var room = _tapLimit - _tap.WrittenCount;
        var take = (int)Math.Min(buffer.Length, room);
        if (take > 0)
        {
            _tap.Write(buffer[..take]);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
