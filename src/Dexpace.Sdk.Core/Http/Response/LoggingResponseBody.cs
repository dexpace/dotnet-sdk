// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.ExceptionServices;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// The response-logging wrapper (BODY-22 to BODY-29, BODY-32, BODY-34; design P3b-9, P3b-10): it drains the delegate body
/// once, lazily, into a bounded capture, and serves the consumer from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lazy, once (BODY-22).</b> Nothing is read until the first <see cref="OpenReadAsync"/>, <see cref="OpenRead"/> or
/// <see cref="SnapshotAsync(CancellationToken)"/>; concurrent first accessors serialise on a
/// <see cref="SemaphoreSlim"/> (which parks no thread when awaited), and the delegate is read exactly once. The drain runs
/// under a token linked from the accessor that started it and the wrapper's own lifetime, which <see cref="ResponseBody.Dispose()"/>
/// cancels (P3b-10, a dated correction to §3.1): a cancelled starter fails the drain like any other failure.
/// </para>
/// <para>
/// <b>Two regimes.</b> A body that fits the cap is captured whole, the delegate is disposed (quietly, BODY-28) and every
/// open is a fresh read-only view over the captured array (BODY-23). A body larger than the cap keeps the delegate open and
/// the next open is a single-use <see cref="PrefixedReadStream"/> over the prefix and the live tail; a second open throws
/// <see cref="StreamConsumedException"/> (BODY-24). Reads never issue a zero-count read, so a source cannot make the drain
/// spin; there is no declared-length cross-check, because a <c>HEAD</c> response carries a length and no body (BODY-25,
/// design §10 entry 4). A drain failure keeps the partial bytes, is cached as an <see cref="ExceptionDispatchInfo"/>, and is
/// rethrown by every open; <see cref="DrainFailure"/> and the snapshots never throw it and never start a drain (BODY-26).
/// </para>
/// <para>
/// <b>Close once (BODY-27).</b> The wrapper's own dispose and the tail stream's dispose share one guard, so the delegate is
/// released at most once; a throwing release still flips it and propagates once. The wrapper takes ownership of its
/// delegate. The captured array outlives the wrapper's dispose (BODY-28). Sync and async paths are twins over the same
/// capture state, because core bans blocking on a task. Equality is identity; no member hands out the capture buffer
/// (BODY-37).
/// </para>
/// </remarks>
internal sealed class LoggingResponseBody : ResponseBody
{
    private const int Undrained = 0;
    private const int Fits = 1;
    private const int Exceeds = 2;

    private readonly ResponseBody _inner;
    private readonly int _cap;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private byte[] _captured = [];
    private int _count;
    private byte[]? _pending;
    private Stream? _stream;
    private bool _started;
    private int _regime = Undrained;
    private long _fullLength = -1;
    private ExceptionDispatchInfo? _failure;
    private int _delegateClosed;
    private int _tailClaimed;
    private int _wrapperDisposed;
    private int _released;

    /// <summary>Initializes a new wrapper that takes ownership of <paramref name="inner"/>.</summary>
    /// <param name="inner">The body to capture.</param>
    /// <param name="captureCap">The most bytes captured; 0 captures nothing. Above <see cref="Array.MaxLength"/> is clamped.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="captureCap"/> is negative (BODY-32).</exception>
    internal LoggingResponseBody(ResponseBody inner, int captureCap)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(captureCap);
        _inner = inner;
        _cap = Math.Min(captureCap, Array.MaxLength);
    }

    public override MediaType? ContentType => _inner.ContentType;

    /// <summary>The captured size once the whole body was captured, otherwise the delegate's declared length (BODY-29).</summary>
    public override long ContentLength
    {
        get
        {
            var captured = Interlocked.Read(ref _fullLength);
            return captured >= 0 ? captured : _inner.ContentLength;
        }
    }

    /// <summary>The cached drain failure, or <see langword="null"/>. It never starts a drain (BODY-26).</summary>
    internal Exception? DrainFailure => Volatile.Read(ref _failure)?.SourceException;

    /// <summary><see langword="true"/> once the whole body fit within the cap and was captured.</summary>
    internal bool IsFullyCaptured => Interlocked.Read(ref _fullLength) >= 0;

    /// <summary>A copy of what was captured, draining first if nothing has started. It never throws a drain failure (BODY-26).</summary>
    /// <param name="cancellationToken">Cancels this accessor's wait, or the drain it starts.</param>
    /// <returns>A new array; the partial bytes after a failed drain.</returns>
    internal async Task<byte[]> SnapshotAsync(CancellationToken cancellationToken)
    {
        await EnsureDrainedAsync(cancellationToken).ConfigureAwait(false);
        return CopyCaptured(_count);
    }

    /// <summary>A copy of at most the first <paramref name="maxBytes"/> captured bytes.</summary>
    /// <param name="maxBytes">The most bytes to return; above <see cref="Array.MaxLength"/> is clamped.</param>
    /// <param name="cancellationToken">Cancels this accessor's wait, or the drain it starts.</param>
    /// <returns>A new array.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative (BODY-32).</exception>
    internal async Task<byte[]> SnapshotAsync(int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        await EnsureDrainedAsync(cancellationToken).ConfigureAwait(false);
        return CopyCaptured(Math.Min(maxBytes, _count));
    }

    public override async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDrainedAsync(cancellationToken).ConfigureAwait(false);
        return Serve();
    }

    public override Stream OpenRead(CancellationToken cancellationToken = default)
    {
        EnsureDrained(cancellationToken);
        return Serve();
    }

    protected override void Dispose(bool disposing)
    {
        Volatile.Write(ref _wrapperDisposed, 1);
        if (disposing && Interlocked.Exchange(ref _released, 1) == 0)
        {
            try
            {
                _lifetime.Cancel();
                CloseDelegate(quiet: false);
            }
            finally
            {
                _lifetime.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        Volatile.Write(ref _wrapperDisposed, 1);
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            try
            {
                await _lifetime.CancelAsync().ConfigureAwait(false);
                await CloseDelegateAsync(quiet: false).ConfigureAwait(false);
            }
            finally
            {
                _lifetime.Dispose();
            }
        }

        // The base routes to Dispose(true), which finds the release already done.
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    // BODY-22: acquire (the caller's token governs only the wait), drain-or-return-cached, release.
    private async ValueTask EnsureDrainedAsync(CancellationToken starter)
    {
        await _gate.WaitAsync(starter).ConfigureAwait(false);
        try
        {
            if (!TryBeginDrain())
            {
                return;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(starter, _lifetime.Token);
            try
            {
                await DrainAsync(linked.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Volatile.Write(ref _failure, ExceptionDispatchInfo.Capture(ex));
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureDrained(CancellationToken starter)
    {
        _gate.Wait(starter);
        try
        {
            if (!TryBeginDrain())
            {
                return;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(starter, _lifetime.Token);
            try
            {
                Drain(linked.Token);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Volatile.Write(ref _failure, ExceptionDispatchInfo.Capture(ex));
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // After the wrapper is disposed nothing is left to drain (BODY-28); a drain already done is never repeated.
    private bool TryBeginDrain()
    {
        if (_started || Volatile.Read(ref _wrapperDisposed) != 0)
        {
            return false;
        }

        _started = true;
        return true;
    }

    private async Task DrainAsync(CancellationToken token)
    {
        var stream = await _inner.OpenReadAsync(token).ConfigureAwait(false);
        _stream = stream;
        using var chunk = PooledChunk.Rent(StreamCopy.ChunkSize);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(chunk.Array.AsMemory(0, NextReadSize()), token).ConfigureAwait(false);
            if (read == 0)
            {
                MarkFits();
                await CloseDelegateAsync(quiet: true).ConfigureAwait(false);
                return;
            }

            if (Absorb(chunk.Array, read))
            {
                return;
            }
        }
    }

    private void Drain(CancellationToken token)
    {
        var stream = _inner.OpenRead(token);
        _stream = stream;
        using var chunk = PooledChunk.Rent(StreamCopy.ChunkSize);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var read = stream.Read(chunk.Array, 0, NextReadSize());
            if (read == 0)
            {
                MarkFits();
                CloseDelegate(quiet: true);
                return;
            }

            if (Absorb(chunk.Array, read))
            {
                return;
            }
        }
    }

    // At most one byte past the cap is ever requested, so the overflow is learned without buffering a chunk, and the count is
    // always positive: the drain never issues a zero-count read (BODY-25).
    private int NextReadSize() => (int)Math.Min(StreamCopy.ChunkSize, (long)_cap - _count + 1);

    // Appends up to the cap; returns true when bytes beyond the cap arrived (the over-cap regime).
    private bool Absorb(byte[] chunk, int read)
    {
        var take = Math.Min(read, _cap - _count);
        if (take > 0)
        {
            if (_count + take > _captured.Length)
            {
                Array.Resize(ref _captured, (int)Math.Min(_cap, Math.Max((long)_count + take, Math.Max(4096L, _captured.Length * 2L))));
            }

            Buffer.BlockCopy(chunk, 0, _captured, _count, take);
            _count += take;
        }

        if (read == take)
        {
            return false;
        }

        _pending = chunk.AsSpan(take, read - take).ToArray();
        Volatile.Write(ref _regime, Exceeds);
        return true;
    }

    private void MarkFits()
    {
        Volatile.Write(ref _regime, Fits);
        Interlocked.Exchange(ref _fullLength, _count);
    }

    // The failure first (BODY-26), then the regime (BODY-23, BODY-24).
    private Stream Serve()
    {
        Volatile.Read(ref _failure)?.Throw();
        switch (Volatile.Read(ref _regime))
        {
            case Fits:
                return new MemoryStream(_captured, 0, _count, writable: false, publiclyVisible: false);
            case Exceeds:
                return ClaimTail();
            default:
                throw new StreamClosedException("This response body was disposed before it was read.");
        }
    }

    private PrefixedReadStream ClaimTail()
    {
        if (Volatile.Read(ref _tailClaimed) != 0)
        {
            throw new StreamConsumedException(ConsumedMessage);
        }

        if (Volatile.Read(ref _delegateClosed) != 0)
        {
            throw new StreamClosedException("This response body was disposed before it was read.");
        }

        if (Interlocked.Exchange(ref _tailClaimed, 1) != 0)
        {
            throw new StreamConsumedException(ConsumedMessage);
        }

        return new PrefixedReadStream(
            _captured,
            _count,
            _pending,
            _stream!,
            () => CloseDelegate(quiet: false),
            () => CloseDelegateAsync(quiet: false));
    }

    // BODY-27: the one close-once guard. It is flipped before the release, so a release that throws still counts.
    private void CloseDelegate(bool quiet)
    {
        if (Interlocked.Exchange(ref _delegateClosed, 1) != 0)
        {
            return;
        }

        if (quiet)
        {
            Disposal.DisposeQuietly(_stream);
            Disposal.DisposeQuietly(_inner);
            return;
        }

        try
        {
            _stream?.Dispose();
        }
        finally
        {
            _inner.Dispose();
        }
    }

    private async ValueTask CloseDelegateAsync(bool quiet)
    {
        if (Interlocked.Exchange(ref _delegateClosed, 1) != 0)
        {
            return;
        }

        if (quiet)
        {
            await Disposal.DisposeQuietlyAsync(_stream).ConfigureAwait(false);
            await Disposal.DisposeQuietlyAsync(_inner).ConfigureAwait(false);
            return;
        }

        try
        {
            if (_stream is not null)
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }
    }

    private byte[] CopyCaptured(int length) => _captured.AsSpan(0, length).ToArray();
}
