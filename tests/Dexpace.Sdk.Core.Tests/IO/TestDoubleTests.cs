// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>The phase 3a stream doubles are only trusted once they are tested (infrastructure for IO-2, IO-27, IO-41).</summary>
[Trait("Category", "Unit")]
public class TestDoubleTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void ChunkedReadStream_never_returns_more_than_k_per_read(int k)
    {
        var data = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        using var stream = new ChunkedReadStream(data, k);
        var buffer = new byte[100];
        var total = 0;

        while (true)
        {
            var read = stream.Read(buffer, total, 50);
            Assert.InRange(read, 0, k);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        Assert.Equal(data, buffer.AsSpan(0, total).ToArray());
    }

    [Fact]
    public async Task StrictReadStream_throws_on_a_zero_count_read_in_every_overload()
    {
        using var stream = new StrictReadStream(new MemoryStream([1, 2, 3]));
        var buffer = new byte[4];

        Assert.Throws<InvalidOperationException>(() => stream.Read(buffer, 0, 0));
        Assert.Throws<InvalidOperationException>(() => stream.Read(Span<byte>.Empty));
        await Assert.ThrowsAsync<InvalidOperationException>(() => stream.ReadAsync(buffer, 0, 0, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => stream.ReadAsync(Memory<byte>.Empty, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task StrictReadStream_records_requested_counts()
    {
        using var stream = new StrictReadStream(new MemoryStream([1, 2, 3, 4, 5]));
        Assert.False(stream.AnyRead);
        var buffer = new byte[8];

        var first = stream.Read(buffer, 0, 2);
        var second = await stream.ReadAsync(buffer.AsMemory(0, 5), TestContext.Current.CancellationToken);
        Assert.Equal(2, first);
        Assert.Equal(3, second);

        Assert.True(stream.AnyRead);
        Assert.Equal([2, 5], stream.RequestedCounts);
    }

    [Fact]
    public async Task FailingWriteStream_throws_on_the_nth_write_in_every_overload()
    {
        var failure = new IOException("boom");
        using var stream = new FailingWriteStream(3, failure);

        stream.Write([1], 0, 1);
        await stream.WriteAsync(new byte[] { 2 }.AsMemory(), TestContext.Current.CancellationToken);
        var thrown = Assert.Throws<IOException>(() => stream.Write(new byte[] { 3 }.AsSpan()));

        Assert.Same(failure, thrown);
        Assert.Equal([1, 2], stream.WrittenBeforeFailure);

        using var second = new FailingWriteStream(1, failure);
        await Assert.ThrowsAsync<IOException>(async () => await second.WriteAsync(new byte[] { 1 }.AsMemory(), TestContext.Current.CancellationToken));
        using var third = new FailingWriteStream(1, failure);
        await Assert.ThrowsAsync<IOException>(() => third.WriteAsync(new byte[] { 1 }, 0, 1, TestContext.Current.CancellationToken));
        using var fourth = new FailingWriteStream(1, failure);
        Assert.Throws<IOException>(() => fourth.Write([1], 0, 1));
    }

    [Fact]
    public async Task DisposeCountingStream_counts_inner_disposals()
    {
        var counting = new DisposeCountingStream(new MemoryStream());

        counting.Dispose();
        counting.Dispose();
        await counting.DisposeAsync();

        Assert.True(counting.DisposeCount >= 2);
    }
}
