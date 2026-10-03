// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The streams under test are owned by the test.

using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary><see cref="PrefixedReadStream"/>: BODY-24, BODY-25, BODY-27.</summary>
[Trait("Category", "Unit")]
public class PrefixedReadStreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class ProbeStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public int Reads { get; private set; }

        public override bool CanRead => true;

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
            Assert.NotEqual(0, buffer.Length);
            Reads++;
            return _inner.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Assert.NotEqual(0, buffer.Length);
            Reads++;
            return _inner.ReadAsync(buffer, cancellationToken);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static PrefixedReadStream Create(byte[] prefix, byte[]? staged, Stream tail, out int[] closes)
    {
        var counter = new int[1];
        closes = counter;
        return new PrefixedReadStream(
            prefix,
            prefix.Length,
            staged,
            tail,
            () => Interlocked.Increment(ref counter[0]),
            () =>
            {
                Interlocked.Increment(ref counter[0]);
                return ValueTask.CompletedTask;
            });
    }

    [Fact]
    public async Task Reads_the_prefix_then_the_live_tail()
    {
        using var stream = Create([1, 2, 3], [4], new ProbeStream([5, 6, 7]), out var closes);
        using var sink = new MemoryStream();

        await stream.CopyToAsync(sink, Token);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, sink.ToArray());
        Assert.Equal(1, closes[0]);
    }

    [Fact]
    public async Task Never_issues_a_zero_count_read()
    {
        var tail = new ProbeStream([9]);
        using var stream = Create([], null, tail, out _);

        Assert.Equal(0, stream.Read(new byte[1], 0, 0));
        Assert.Equal(0, await stream.ReadAsync(Memory<byte>.Empty, Token));
        Assert.Equal(0, tail.Reads);
        Assert.Equal(1, await stream.ReadAsync(new byte[4], Token));
        Assert.Equal(1, tail.Reads);
    }

    [Fact]
    public async Task Dispose_routes_to_the_shared_close_once_guard()
    {
        var syncStream = Create([1], null, new ProbeStream([2]), out var syncCloses);
        syncStream.Dispose();
        syncStream.Dispose();
        Assert.Equal(1, syncCloses[0]);

        var asyncStream = Create([1], null, new ProbeStream([2]), out var asyncCloses);
        await asyncStream.DisposeAsync();
        await asyncStream.DisposeAsync();
        asyncStream.Dispose();
        Assert.Equal(1, asyncCloses[0]);
    }

    [Fact]
    public async Task Read_after_dispose_throws_ObjectDisposedException()
    {
        var stream = Create([1], null, new ProbeStream([2]), out _);
        await stream.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.ReadAsync(new byte[1], Token).AsTask());
    }

    [Fact]
    public void Seek_and_Length_are_unsupported()
    {
        using var stream = Create([1], null, new ProbeStream([2]), out _);

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [Fact]
    public async Task Sync_and_async_reads_agree()
    {
        byte[] prefix = [1, 2, 3, 4, 5];
        byte[] tail = [6, 7, 8, 9, 10, 11];
        using var sync = Create(prefix, [12], new ProbeStream(tail), out _);
        using var async = Create(prefix, [12], new ProbeStream(tail), out _);
        using var syncSink = new MemoryStream();
        using var asyncSink = new MemoryStream();
        var small = new byte[3];

        int read;
        while ((read = sync.Read(small, 0, small.Length)) > 0)
        {
            syncSink.Write(small, 0, read);
        }

        while ((read = await async.ReadAsync(small, Token)) > 0)
        {
            asyncSink.Write(small, 0, read);
        }

        Assert.Equal(syncSink.ToArray(), asyncSink.ToArray());
        Assert.Equal(11 + 1, syncSink.Length);
    }

    [Fact]
    public void Reading_past_the_end_stays_at_the_end_without_touching_the_tail_again()
    {
        var tail = new ProbeStream([]);
        using var stream = Create([1], null, tail, out var closes);
        var buffer = new byte[8];

        Assert.Equal(1, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));

        Assert.Equal(1, tail.Reads);
        Assert.Equal(1, closes[0]);
    }
}
