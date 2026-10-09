// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>The test double that places read boundaries exactly (phase 7b task 2.5).</summary>
[Trait("Category", "Unit")]
public class SplitReadStreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void No_read_returns_bytes_across_a_cut()
    {
        byte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
        using var stream = new SplitReadStream(data, 3, 7);
        var buffer = new byte[100];

        Assert.Equal(3, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(4, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(3, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public async Task The_concatenation_of_the_reads_is_the_data_and_the_end_is_zero()
    {
        byte[] data = [.. Enumerable.Range(0, 40).Select(i => (byte)i)];
        using var stream = new SplitReadStream(data, 1, 2, 17, 39);
        using var sink = new MemoryStream();
        var buffer = new byte[8];

        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), Token)) > 0)
        {
            sink.Write(buffer, 0, read);
        }

        Assert.Equal(data, sink.ToArray());
        Assert.Equal(0, await stream.ReadAsync(buffer.AsMemory(), Token));
    }

    [Fact]
    public void A_small_buffer_is_honoured_inside_a_segment()
    {
        using var stream = new SplitReadStream([1, 2, 3, 4, 5, 6], 4);
        var buffer = new byte[2];

        Assert.Equal(2, stream.Read(buffer, 0, 2));
        Assert.Equal(2, stream.Read(buffer, 0, 2));
        Assert.Equal(2, stream.Read(buffer, 0, 2));
        Assert.Equal(0, stream.Read(buffer, 0, 2));
    }

    [Fact]
    public void No_cuts_serves_the_data_in_one_read()
    {
        using var stream = new SplitReadStream([1, 2, 3]);

        Assert.Equal(3, stream.Read(new byte[10], 0, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void A_cut_outside_the_data_is_rejected(int cut)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SplitReadStream([1, 2, 3, 4, 5], cut));
    }

    [Fact]
    public void Cuts_must_ascend_strictly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SplitReadStream([1, 2, 3, 4, 5], 3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SplitReadStream([1, 2, 3, 4, 5], 2, 2));
    }
}
