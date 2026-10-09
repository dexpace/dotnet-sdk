// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A request body source whose first asynchronous read parks until a gate opens, the token fires or the stream is
/// disposed: the instrument of <c>transport-19.abandoned-body-unblocks</c> (a producer abandoned by its consumer must
/// unblock). <see cref="ReadStarted"/> says the transport has begun reading; <see cref="ObservedCancellation"/> says the
/// parked read saw the call's token. Asynchronous only: a synchronous read throws, because the assertion runs on the
/// asynchronous face.
/// </summary>
internal sealed class ParkedReadStream : Stream
{
    private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _observedCancellation;
    private int _disposed;

    /// <summary>Completes when the first read has begun and is parked.</summary>
    internal Task ReadStarted => _readStarted.Task;

    /// <summary>Whether a parked read ended because its cancellation token fired.</summary>
    internal bool ObservedCancellation => Volatile.Read(ref _observedCancellation) == 1;

    /// <summary>Whether the stream was disposed.</summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    /// <summary>Lets the parked read end with end-of-stream.</summary>
    internal void Release() => _gate.TrySetResult();

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _readStarted.TrySetResult();
        try
        {
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref _observedCancellation, 1);
            throw;
        }

        return 0;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("ParkedReadStream is asynchronous only.");

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _gate.TrySetException(new ObjectDisposedException(nameof(ParkedReadStream)));
        }

        base.Dispose(disposing);
    }
}
