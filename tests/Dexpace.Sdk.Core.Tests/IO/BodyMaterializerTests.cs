// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.IO;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>The read side of bounded materialisation: IO-9, IO-11, IO-2 (plan reading R4).</summary>
[Trait("Category", "Unit")]
public class BodyMaterializerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Bytes(int length) => Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();

    private static async Task<byte[]> ReadAll(bool useAsync, Stream source, long declared, long limit) =>
        useAsync
            ? await BodyMaterializer.ReadAllAsync(source, declared, limit, Token)
            : BodyMaterializer.ReadAll(source, declared, limit, Token);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_declared_length_above_the_limit_is_refused_before_any_read(bool useAsync)
    {
        using var source = new StrictReadStream(new MemoryStream(Bytes(100)));

        var error = await Assert.ThrowsAsync<BodyTooLargeException>(() => ReadAll(useAsync, source, 100, 50));

        Assert.False(source.AnyRead);
        Assert.Contains("50", error.Message, StringComparison.Ordinal);
        Assert.Contains("100", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_unknown_length_body_that_exceeds_the_limit_is_refused(bool useAsync)
    {
        using var source = new MemoryStream(Bytes(1000));

        await Assert.ThrowsAsync<BodyTooLargeException>(() => ReadAll(useAsync, source, -1, 100));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_unknown_length_body_of_exactly_the_limit_succeeds(bool useAsync)
    {
        var data = Bytes(100);
        using var source = new MemoryStream(data);

        var result = await ReadAll(useAsync, source, -1, 100);

        Assert.Equal(data, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_body_one_byte_over_the_limit_is_refused_by_the_probe(bool useAsync)
    {
        using var source = new MemoryStream(Bytes(101));

        await Assert.ThrowsAsync<BodyTooLargeException>(() => ReadAll(useAsync, source, -1, 100));
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
    public async Task ReadAll_returns_every_byte_of_a_chunked_source(bool useAsync, int perRead)
    {
        var data = Bytes(60);
        using var source = new ChunkedReadStream(data, perRead);

        var result = await ReadAll(useAsync, source, -1, 1000);

        Assert.Equal(data, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAll_of_an_empty_source_is_an_empty_array(bool useAsync)
    {
        var result = await ReadAll(useAsync, Stream.Null, -1, 100);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_message_points_at_OpenRead(bool useAsync)
    {
        using var source = new MemoryStream(Bytes(10));

        var error = await Assert.ThrowsAsync<BodyTooLargeException>(() => ReadAll(useAsync, source, 10, 5));

        Assert.Contains("OpenRead", error.Message, StringComparison.Ordinal);
        Assert.Contains("OpenReadAsync", error.Message, StringComparison.Ordinal);
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
    public async Task No_read_is_ever_zero_count(bool useAsync, int length)
    {
        using var source = new StrictReadStream(new MemoryStream(Bytes(length)));

        try
        {
            _ = await ReadAll(useAsync, source, -1, 8);
        }
        catch (BodyTooLargeException)
        {
            // A body over the limit is refused; the point is that no zero-count read was issued on the way.
        }

        Assert.All(source.RequestedCounts, count => Assert.True(count >= 1));
    }

    [Fact]
    public void A_negative_limit_throws_ArgumentOutOfRangeException()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => BodyMaterializer.ReadAll(new MemoryStream(), -1, -1, Token));

        Assert.Equal("limit", error.ParamName);
    }

    [Fact]
    public void A_limit_of_zero_accepts_an_empty_body_and_refuses_any_byte()
    {
        Assert.Empty(BodyMaterializer.ReadAll(new MemoryStream(), -1, 0, Token));
        Assert.Throws<BodyTooLargeException>(() => BodyMaterializer.ReadAll(new MemoryStream([1]), -1, 0, Token));
    }
}
