// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A non-seekable, read-only stream over another that counts what is read from it and how often it is disposed: a
/// single-use request body source whose reads and disposal an assertion can observe (<c>TRANSPORT-2</c>,
/// <c>TRANSPORT-17</c>). Non-seekable on purpose: a seekable stream with a declared length is replayable
/// (<c>RequestBody.FromStream</c>), and these assertions are about the single-use case.
/// </summary>
internal sealed class CountingStream(Stream inner) : Stream
{
    private long _bytesRead;
    private int _readCalls;
    private int _disposeCount;

    /// <summary>The bytes handed to readers so far.</summary>
    internal long BytesRead => Interlocked.Read(ref _bytesRead);

    /// <summary>How many read calls returned or failed so far (a call that returned zero bytes counts).</summary>
    internal int ReadCalls => Volatile.Read(ref _readCalls);

    /// <summary>Whether the stream has been disposed at least once.</summary>
    internal bool IsDisposed => DisposeCount > 0;

    /// <summary>How many times <see cref="Dispose(bool)"/> ran with disposing set.</summary>
    internal int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>A counting stream over a copy of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The content.</param>
    internal static CountingStream OfBytes(byte[] bytes) => new(new MemoryStream(bytes, writable: false));

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
    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Count(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

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
        if (disposing)
        {
            Interlocked.Increment(ref _disposeCount);
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private int Count(int read)
    {
        Interlocked.Add(ref _bytesRead, read);
        Interlocked.Increment(ref _readCalls);
        return read;
    }
}
