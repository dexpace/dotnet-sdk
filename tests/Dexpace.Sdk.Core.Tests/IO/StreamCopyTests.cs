// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.IO;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>The copy primitives: IO-1, IO-2, IO-3, IO-12, IO-17, IO-40, HTTP-39 and the HTTP-52 drain (design position F).</summary>
[Trait("Category", "Unit")]
public class StreamCopyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Bytes(int length) => Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();

    private static async Task<long> CopyToEnd(bool useAsync, Stream source, Stream destination, CancellationToken token)
    {
        if (useAsync)
        {
            return await StreamCopy.CopyToEndAsync(source, destination, token);
        }

        return StreamCopy.CopyToEnd(source, destination, token);
    }

    private static async Task CopyExactly(bool useAsync, Stream source, Stream destination, long count, CancellationToken token)
    {
        if (useAsync)
        {
            await StreamCopy.CopyExactlyAsync(source, destination, count, token);
            return;
        }

        StreamCopy.CopyExactly(source, destination, count, token);
    }

    private static async Task<bool> Drain(bool useAsync, Stream source, ArrayBufferWriter<byte> into, long max, CancellationToken token)
    {
        if (useAsync)
        {
            return await StreamCopy.DrainUpToAsync(source, into, max, token);
        }

        return StreamCopy.DrainUpTo(source, into, max, token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyToEnd_returns_the_total_and_stops_on_end_of_stream(bool useAsync)
    {
        var data = Bytes(200_000);
        using var source = new MemoryStream(data);
        using var destination = new MemoryStream();

        var total = await CopyToEnd(useAsync, source, destination, Token);

        Assert.Equal(200_000, total);
        Assert.Equal(data, destination.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Draining_into_a_non_empty_accumulator_appends_after_its_existing_bytes(bool useAsync)
    {
        var into = new ArrayBufferWriter<byte>();
        into.Write(new byte[] { 9, 9, 9 });
        using var source = new MemoryStream([1, 2, 3, 4]);

        var sawEnd = await Drain(useAsync, source, into, 100, Token);

        Assert.True(sawEnd);
        Assert.Equal([9, 9, 9, 1, 2, 3, 4], into.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 7)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 7)]
    public async Task A_chunked_source_is_reassembled_in_order(bool useAsync, int perRead)
    {
        var data = Bytes(50);
        using var source = new ChunkedReadStream(data, perRead);
        using var destination = new MemoryStream();
        await CopyToEnd(useAsync, source, destination, Token);
        Assert.Equal(data, destination.ToArray());

        var into = new ArrayBufferWriter<byte>();
        using var second = new ChunkedReadStream(data, perRead);
        await Drain(useAsync, second, into, 1000, Token);
        Assert.Equal(data, into.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(false, 3, 1)]
    [InlineData(false, 10, 7)]
    [InlineData(false, 10, 81_920)]
    [InlineData(false, 10_000, 1)]
    [InlineData(true, 10, 7)]
    [InlineData(true, 100_000, 81_920)]
    [InlineData(true, 200_000, 7000)]
    public async Task CopyExactly_writes_exactly_count_bytes_when_the_source_is_equal_or_longer(bool useAsync, int count, int perRead)
    {
        foreach (var sourceLength in new[] { count, count + 1, count + 5000 })
        {
            var data = Bytes(sourceLength);
            using var source = new ChunkedReadStream(data, perRead);
            using var destination = new MemoryStream();

            await CopyExactly(useAsync, source, destination, count, Token);

            Assert.Equal(data.AsSpan(0, count).ToArray(), destination.ToArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyExactly_leaves_the_longer_sources_remainder_unread(bool useAsync)
    {
        using var source = new MemoryStream(Bytes(100));
        using var destination = new MemoryStream();

        await CopyExactly(useAsync, source, destination, 40, Token);

        Assert.Equal(40, source.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyExactly_on_a_short_source_throws_EndOfStreamException_naming_both_numbers(bool useAsync)
    {
        using var source = new MemoryStream(Bytes(6));
        using var destination = new MemoryStream();

        var error = await Assert.ThrowsAsync<EndOfStreamException>(
            () => CopyExactly(useAsync, source, destination, 10, Token));

        Assert.Equal("The source ended after 6 of 10 bytes.", error.Message);
        Assert.Equal(6, destination.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyExactly_of_zero_performs_no_read(bool useAsync)
    {
        using var source = new StrictReadStream(new MemoryStream(Bytes(10)));
        using var destination = new MemoryStream();

        await CopyExactly(useAsync, source, destination, 0, Token);

        Assert.False(source.AnyRead);
        Assert.Empty(destination.ToArray());
    }

    [Fact]
    public void ShortTransferMessage_has_one_form()
    {
        Assert.Equal("The source ended after 3 of 9 bytes.", StreamCopy.ShortTransferMessage(3, 9));
        Assert.Equal("The source ended after 0 of 0 bytes.", StreamCopy.ShortTransferMessage(0, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DrainUpTo_stops_at_the_cap_and_reports_whether_the_end_was_seen(bool useAsync)
    {
        // Longer than the cap: stops at the cap, end not seen.
        var into = new ArrayBufferWriter<byte>();
        using (var longer = new MemoryStream(Bytes(20)))
        {
            Assert.False(await Drain(useAsync, longer, into, 8, Token));
            Assert.Equal(8, into.WrittenCount);
        }

        // Shorter than the cap: end seen.
        into = new ArrayBufferWriter<byte>();
        using (var shorter = new MemoryStream(Bytes(5)))
        {
            Assert.True(await Drain(useAsync, shorter, into, 8, Token));
            Assert.Equal(5, into.WrittenCount);
        }

        // Empty source: end seen.
        into = new ArrayBufferWriter<byte>();
        using (var empty = new MemoryStream())
        {
            Assert.True(await Drain(useAsync, empty, into, 8, Token));
            Assert.Equal(0, into.WrittenCount);
        }

        // Exactly the cap: stopped at the cap without seeing the end (R5), so the answer is false.
        into = new ArrayBufferWriter<byte>();
        using (var exact = new MemoryStream(Bytes(8)))
        {
            Assert.False(await Drain(useAsync, exact, into, 8, Token));
            Assert.Equal(8, into.WrittenCount);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DrainUpTo_keeps_the_bytes_read_before_a_failure(bool useAsync)
    {
        var into = new ArrayBufferWriter<byte>();
        using var source = new FailingReadStream(Bytes(10), 3, new IOException("boom"));

        await Assert.ThrowsAsync<IOException>(() => Drain(useAsync, source, into, 1000, Token));

        Assert.Equal(Bytes(10).AsSpan(0, 3).ToArray(), into.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 8)]
    [InlineData(false, 9)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 8)]
    [InlineData(true, 9)]
    public async Task No_helper_issues_a_zero_count_read(bool useAsync, int sourceLength)
    {
        const int Cap = 8;
        using var destination = new MemoryStream();

        using (var s = new StrictReadStream(new MemoryStream(Bytes(sourceLength))))
        {
            await CopyToEnd(useAsync, s, destination, Token);
        }

        using (var s = new StrictReadStream(new MemoryStream(Bytes(sourceLength))))
        {
            await Drain(useAsync, s, new ArrayBufferWriter<byte>(), Cap, Token);
        }

        using (var s = new StrictReadStream(new MemoryStream(Bytes(sourceLength))))
        {
            await Drain(useAsync, s, new ArrayBufferWriter<byte>(), 0, Token);
            Assert.False(s.AnyRead);
        }

        using (var s = new StrictReadStream(new MemoryStream(Bytes(sourceLength))))
        {
            var count = Math.Min(sourceLength, Cap);
            await CopyExactly(useAsync, s, destination, count, Token);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task A_negative_argument_throws_before_any_io(bool useAsync, bool exactly)
    {
        using var source = new StrictReadStream(new MemoryStream(Bytes(10)));
        using var destination = new MemoryStream();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
            {
                if (exactly)
                {
                    await CopyExactly(useAsync, source, destination, -1, Token);
                }
                else
                {
                    await Drain(useAsync, source, new ArrayBufferWriter<byte>(), -1, Token);
                }
            });

        Assert.Equal(exactly ? "count" : "maxBytes", error.ParamName);
        Assert.False(source.AnyRead);
    }

    [Fact]
    public void A_cancelled_token_stops_the_sync_copy_between_chunks()
    {
        using var cts = new CancellationTokenSource();
        using var source = new CancelAfterFirstReadStream(Bytes(500_000), cts);
        using var destination = new MemoryStream();

        Assert.Throws<OperationCanceledException>(() => StreamCopy.CopyToEnd(source, destination, cts.Token));

        Assert.Equal(1, source.ReadCalls);
        Assert.True(destination.Length > 0);
    }

    [Fact]
    public async Task Cancellation_surfaces_the_same_OperationCanceledException_from_the_async_copy()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var source = new MemoryStream(Bytes(100));
        using var destination = new MemoryStream();

        var error = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await StreamCopy.CopyToEndAsync(source, destination, cts.Token));

        Assert.Equal(cts.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Neither_stream_is_disposed(bool useAsync)
    {
        using var source = new DisposeCountingStream(new MemoryStream(Bytes(10)));
        using var destination = new DisposeCountingStream(new MemoryStream());

        await CopyToEnd(useAsync, source, destination, Token);
        source.Position = 0;
        await CopyExactly(useAsync, source, destination, 5, Token);
        source.Position = 0;
        await Drain(useAsync, source, new ArrayBufferWriter<byte>(), 100, Token);

        Assert.Equal(0, source.DisposeCount);
        Assert.Equal(0, destination.DisposeCount);
    }

    private sealed class FailingReadStream(byte[] data, int failAfter, Exception failure) : Stream
    {
        private int _position;

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
            if (_position >= failAfter)
            {
                throw failure;
            }

            var n = Math.Min(Math.Min(buffer.Length, 3), failAfter - _position);
            data.AsSpan(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(Read(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(buffer.AsSpan(offset, count)));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CancelAfterFirstReadStream(byte[] data, CancellationTokenSource cts) : Stream
    {
        private int _position;

        public int ReadCalls { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalls++;
            var n = Math.Min(count, data.Length - _position);
            Array.Copy(data, _position, buffer, offset, n);
            _position += n;
            cts.Cancel();
            return n;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
