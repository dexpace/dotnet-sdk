// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The streams under test are released by the assertions.
#pragma warning disable CA1835 // The byte[] ReadAsync overload is a member of the wrapper under test.

using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// The stream a transport response body hands out (BODY-14, HTTP-41): a pass-through until it is disposed, then a stream that fails every
/// use with <see cref="ObjectDisposedException"/> and forwards the disposal to the content stream exactly once.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DisposalAwareStreamTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class CountingStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public int Disposals { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }

    private static readonly byte[] s_content = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task It_forwards_every_read_form_while_live()
    {
        using var stream = new DisposalAwareStream(new CountingStream(s_content));
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(8, stream.Length);

        Assert.Equal(1, stream.ReadByte());
        var array = new byte[2];
        Assert.Equal(2, stream.Read(array, 0, 2));
        Assert.Equal<byte>([2, 3], array);
        Span<byte> span = stackalloc byte[1];
        Assert.Equal(1, stream.Read(span));
        Assert.Equal(4, span[0]);
        Assert.Equal(1, await stream.ReadAsync(array.AsMemory(0, 1), Ct));
        Assert.Equal(5, array[0]);
        Assert.Equal(1, await stream.ReadAsync(array, 0, 1, Ct));
        Assert.Equal(6, stream.Position);
        stream.Position = 0;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, Ct);
        Assert.Equal(s_content, copy.ToArray());
        Assert.Equal(2, stream.Seek(2, SeekOrigin.Begin));
        using var syncCopy = new MemoryStream();
        stream.CopyTo(syncCopy);
        Assert.Equal(s_content[2..], syncCopy.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_disposed_stream_fails_every_use_and_the_inner_is_disposed_once(bool asynchronously)
    {
        var inner = new CountingStream(s_content);
        var stream = new DisposalAwareStream(inner);

        if (asynchronously)
        {
            await stream.DisposeAsync();
            await stream.DisposeAsync();
        }
        else
        {
            stream.Dispose();
            stream.Dispose();
        }

        Assert.Equal(1, inner.Disposals);
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1].AsSpan()));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => _ = await stream.ReadAsync(new byte[1].AsMemory(), Ct));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => _ = await stream.ReadAsync(new byte[1], 0, 1, Ct));
        Assert.Throws<ObjectDisposedException>(() => stream.CopyTo(Stream.Null));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.CopyToAsync(Stream.Null, Ct));
        Assert.Throws<ObjectDisposedException>(() => stream.Length);
        Assert.Throws<ObjectDisposedException>(() => stream.Position);
        Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<ObjectDisposedException>(() => stream.Flush());
    }

    [Fact]
    public void An_inner_stream_that_cannot_seek_is_reported_as_it_is()
    {
        using var stream = new DisposalAwareStream(new ForwardOnlyStream());

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
    }

    private sealed class ForwardOnlyStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
