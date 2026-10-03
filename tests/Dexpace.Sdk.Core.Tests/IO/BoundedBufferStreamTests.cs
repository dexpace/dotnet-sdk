// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.IO;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>The write side of bounded materialisation: IO-9, IO-3, IO-37, design fact 1.</summary>
[Trait("Category", "Unit")]
public class BoundedBufferStreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Writes_accumulate_and_ToArray_returns_an_exact_size_copy()
    {
        using var stream = new BoundedBufferStream(100, -1);

        stream.Write([1, 2, 3], 0, 3);
        stream.Write(new byte[] { 4, 5 }.AsSpan());
        var first = stream.ToArray();
        var second = stream.ToArray();

        Assert.Equal([1, 2, 3, 4, 5], first);
        Assert.NotSame(first, second);
        Assert.Equal(5, stream.Length);
    }

    [Fact]
    public void A_known_expected_length_pre_sizes_the_buffer()
    {
        // Capacity is not observable, so the evidence is that a pre-sized buffer allocates less than a growing one for the
        // same payload. Both run on this thread, back to back. Recorded as a risk: delete if it ever flakes (plan F6).
        var chunk = new byte[1024];
        const int Total = 1024 * 1024;

        long Measure(long expected)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            using var stream = new BoundedBufferStream(Total, expected);
            for (var written = 0; written < Total; written += chunk.Length)
            {
                stream.Write(chunk, 0, chunk.Length);
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        _ = Measure(Total);
        _ = Measure(-1);
        var presized = Measure(Total);
        var growing = Measure(-1);

        Assert.True(presized < growing, $"pre-sized {presized} bytes, growing {growing} bytes.");
    }

    [Fact]
    public void The_write_that_would_exceed_the_limit_throws_BodyTooLargeException()
    {
        using var stream = new BoundedBufferStream(5, -1);
        stream.Write([1, 2, 3], 0, 3);

        var error = Assert.Throws<BodyTooLargeException>(() => stream.Write([4, 5, 6], 0, 3));

        Assert.Contains("5", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0004", error.Message, StringComparison.Ordinal);
        Assert.Equal([1, 2, 3], stream.ToArray());
    }

    [Fact]
    public async Task A_write_of_exactly_the_limit_succeeds()
    {
        using var stream = new BoundedBufferStream(4, 4);

        await stream.WriteAsync(new byte[] { 1, 2, 3, 4 }.AsMemory(), Token);

        Assert.Equal([1, 2, 3, 4], stream.ToArray());
    }

    [Fact]
    public async Task Both_async_write_overloads_stay_asynchronous()
    {
        // Fact 1: a Stream that overrides only the sync Write turns each async write into a pool-thread hop. The bounded
        // buffer overrides both async overloads, so a write completes synchronously on the calling thread.
        using var stream = new BoundedBufferStream(100, -1);

        var memory = stream.WriteAsync(new byte[] { 1, 2 }.AsMemory(), Token);
        var array = stream.WriteAsync(new byte[] { 3, 4 }, 0, 2, Token);

        Assert.True(memory.IsCompletedSuccessfully);
        Assert.True(array.IsCompletedSuccessfully);
        Assert.Equal([1, 2, 3, 4], stream.ToArray());
        await Task.CompletedTask;
    }

    [Fact]
    public void It_is_write_only()
    {
        using var stream = new BoundedBufferStream(10, -1);

        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [Fact]
    public void A_negative_limit_throws_ArgumentOutOfRangeException()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedBufferStream(-1, -1));

        Assert.Equal("limit", error.ParamName);
    }
}
