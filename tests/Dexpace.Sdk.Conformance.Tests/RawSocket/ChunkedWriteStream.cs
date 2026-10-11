// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Conformance.Tests.RawSocket;

/// <summary>A write-only stream that frames everything written to it as chunked transfer coding; <see cref="FinishAsync"/> writes the terminating chunk.</summary>
internal sealed class ChunkedWriteStream(Stream inner) : Stream
{
    private static readonly byte[] s_crlf = "\r\n"u8.ToArray();

    internal async Task FinishAsync(CancellationToken cancellationToken) =>
        await inner.WriteAsync("0\r\n\r\n"u8.ToArray(), cancellationToken);

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        await inner.WriteAsync(Encoding.ASCII.GetBytes(buffer.Length.ToString("X", System.Globalization.CultureInfo.InvariantCulture) + "\r\n"), cancellationToken);
        await inner.WriteAsync(buffer, cancellationToken);
        await inner.WriteAsync(s_crlf, cancellationToken);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
