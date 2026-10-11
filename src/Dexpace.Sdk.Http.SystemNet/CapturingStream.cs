// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// A write-through wrapper over the native client's request stream that copies what is written into a <see cref="ResendCapture"/>
/// (<c>TRANSPORT-18</c>; phase 8b, P8b-10). It forwards every write unchanged and in order, so the wire sees exactly what an
/// unwrapped stream would; it is write-only and never owns, closes or disposes the stream it wraps.
/// </summary>
internal sealed class CapturingStream(Stream inner, ResendCapture capture) : Stream
{
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        capture.Append(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        inner.Write(buffer);
        capture.Append(buffer);
    }

    public override void WriteByte(byte value)
    {
        inner.WriteByte(value);
        capture.Append([value]);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        capture.Append(buffer.AsSpan(offset, count));
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        capture.Append(buffer.Span);
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
