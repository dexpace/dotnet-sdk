// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The streams under test are owned by the test.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary><see cref="BoundedWriteStream"/>: the multipart length guard (HTTP-51; ported from Node's <c>boundedWriter</c>).</summary>
[Trait("Category", "Unit")]
public class BoundedWriteStreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_chunk_that_would_exceed_the_known_length_is_refused_before_it_is_written()
    {
        var primary = new MemoryStream();
        var bounded = new BoundedWriteStream(primary, 5);
        bounded.Write(new byte[3]);

        Assert.Throws<IOException>(() => bounded.Write(new byte[3]));
        await Assert.ThrowsAsync<IOException>(async () => await bounded.WriteAsync(new byte[3], Token));

        Assert.Equal(3, primary.Length);
        Assert.Equal(3, bounded.Total);
    }

    [Fact]
    public async Task The_total_is_checked_at_the_end_and_a_short_total_fails()
    {
        var bounded = new BoundedWriteStream(new MemoryStream(), 5);
        await bounded.WriteAsync(new byte[2], Token);

        var error = Assert.Throws<EndOfStreamException>(bounded.Complete);

        Assert.Contains("2 of 5", error.Message, StringComparison.Ordinal);
        await bounded.WriteAsync(new byte[3], Token);
        bounded.Complete();
    }

    [Fact]
    public void An_unknown_length_never_refuses()
    {
        var primary = new MemoryStream();
        var bounded = new BoundedWriteStream(primary, -1);

        bounded.Write(new byte[10_000]);
        bounded.Complete();

        Assert.Equal(10_000, primary.Length);
    }

    [Fact]
    public async Task Dispose_does_not_dispose_the_primary()
    {
        var primary = new DisposeCountingStream(new MemoryStream());
        var bounded = new BoundedWriteStream(primary, 1);

        bounded.Dispose();
        await bounded.DisposeAsync();

        Assert.Equal(0, primary.DisposeCount);
    }

    [Fact]
    public async Task Flush_forwards_to_the_primary()
    {
        var primary = new FlushCountingStream();
        var bounded = new BoundedWriteStream(primary, -1);

        bounded.Flush();
        await bounded.FlushAsync(Token);

        Assert.True(primary.Flushes >= 2);
    }

    [Fact]
    public void Seek_and_Read_are_unsupported()
    {
        var bounded = new BoundedWriteStream(new MemoryStream(), -1);

        Assert.False(bounded.CanRead);
        Assert.False(bounded.CanSeek);
        Assert.True(bounded.CanWrite);
        Assert.Throws<NotSupportedException>(() => bounded.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => bounded.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => bounded.SetLength(0));
        Assert.Throws<NotSupportedException>(() => bounded.Length);
        Assert.Throws<NotSupportedException>(() => bounded.Position);
    }

    private sealed class FlushCountingStream : MemoryStream
    {
        private int _flushes;

        public int Flushes => Volatile.Read(ref _flushes);

        public override void Flush()
        {
            Interlocked.Increment(ref _flushes);
            base.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _flushes);
            return base.FlushAsync(cancellationToken);
        }
    }
}
