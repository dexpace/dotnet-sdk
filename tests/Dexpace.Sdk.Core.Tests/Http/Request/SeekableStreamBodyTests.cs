// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The seekable cases are new: nodejs-sdk@c0ff3fd packages/core/src/body/stream-body.test.ts has no seekable variant (a Node
// stream body is always single-use).

#pragma warning disable CA2000 // The streams under test are owned by the test.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>The seekable known-length stream variant: BODY-9, BODY-35, BODY-8 (design P3b-4).</summary>
[Trait("Category", "Unit")]
public class SeekableStreamBodyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Bytes(int count, int first = 0) => [.. Enumerable.Range(first, count).Select(i => (byte)i)];

    private static async Task<byte[]> WriteAsync(RequestBody body)
    {
        using var sink = new MemoryStream();
        await body.WriteToAsync(sink, Token);
        return sink.ToArray();
    }

    private sealed class GateStream : Stream
    {
        private readonly ManualResetEventSlim _entered = new();
        private readonly ManualResetEventSlim _release = new();

        public void WaitEntered() => Assert.True(_entered.Wait(TimeSpan.FromSeconds(10), Token));

        public void Release() => _release.Set();

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
            _entered.Set();
            Assert.True(_release.Wait(TimeSpan.FromSeconds(10), Token));
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    [Fact]
    public async Task A_seekable_stream_with_a_declared_length_is_replayable_with_that_length()
    {
        var body = RequestBody.FromStream(new MemoryStream(Bytes(8)), contentLength: 8);

        Assert.True(body.IsReplayable);
        Assert.Equal(8, body.ContentLength);
        Assert.Same(body, await body.ToReplayableAsync(Token));
        Assert.Same(body, body.ToReplayable(Token));
    }

    [Fact]
    public async Task Every_write_seeks_to_the_start_position_captured_at_construction()
    {
        var source = new MemoryStream(Bytes(10)) { Position = 3 };
        var body = RequestBody.FromStream(source, contentLength: 4);
        var expected = Bytes(4, first: 3);

        source.Position = 0;
        Assert.Equal(expected, await WriteAsync(body));
        source.Position = source.Length;
        Assert.Equal(expected, await WriteAsync(body));
        using var sink = new MemoryStream();
        body.WriteTo(sink, Token);
        Assert.Equal(expected, sink.ToArray());
    }

    [Fact]
    public async Task Two_writes_are_byte_identical()
    {
        var body = RequestBody.FromStream(new MemoryStream(Bytes(100)), contentLength: 100);

        Assert.Equal(await WriteAsync(body), await WriteAsync(body));
    }

    [Fact]
    public async Task A_concurrent_second_write_throws_InvalidOperationException()
    {
        var source = new MemoryStream(Bytes(4));
        var body = RequestBody.FromStream(source, contentLength: 4);
        using var gate = new GateStream();

        var first = Task.Run(() => body.WriteTo(gate, Token), Token);
        gate.WaitEntered();
        var positionDuringFirst = source.Position;

        Assert.Throws<InvalidOperationException>(() => body.WriteTo(new MemoryStream(), Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => body.WriteToAsync(new MemoryStream(), Token));
        Assert.Equal(positionDuringFirst, source.Position);

        gate.Release();
        await first;
        Assert.Equal(Bytes(4), await WriteAsync(body));
    }

    [Fact]
    public async Task An_unknown_length_stays_single_use()
    {
        var body = RequestBody.FromStream(new MemoryStream(Bytes(8)));

        Assert.False(body.IsReplayable);
        Assert.Equal(-1, body.ContentLength);
        _ = await WriteAsync(body);
        await Assert.ThrowsAsync<StreamConsumedException>(() => WriteAsync(body));
    }

    [Fact]
    public async Task A_non_seekable_stream_with_a_length_stays_single_use()
    {
        var body = RequestBody.FromStream(new ChunkedReadStream(Bytes(8), 3), contentLength: 8);

        Assert.False(body.IsReplayable);
        _ = await WriteAsync(body);
        await Assert.ThrowsAsync<StreamConsumedException>(() => WriteAsync(body));
    }

    [Fact]
    public async Task A_short_seekable_source_fails()
    {
        var body = RequestBody.FromStream(new MemoryStream(Bytes(4)), contentLength: 10);

        var error = await Assert.ThrowsAsync<EndOfStreamException>(() => WriteAsync(body));
        Assert.Contains("4 of 10", error.Message, StringComparison.Ordinal);
        Assert.Throws<EndOfStreamException>(() => body.WriteTo(new MemoryStream(), Token));
    }

    [Fact]
    public async Task The_callers_stream_stays_open_after_write_failure_and_ToReplayableAsync()
    {
        var source = new DisposeCountingStream(new MemoryStream(Bytes(4)));
        var body = RequestBody.FromStream(source, contentLength: 10);

        await Assert.ThrowsAsync<EndOfStreamException>(() => WriteAsync(body));
        Assert.Equal(0, source.DisposeCount);

        var good = new DisposeCountingStream(new MemoryStream(Bytes(4)));
        var goodBody = RequestBody.FromStream(good, contentLength: 4);
        _ = await WriteAsync(goodBody);
        _ = await goodBody.ToReplayableAsync(Token);
        Assert.Equal(0, good.DisposeCount);
        Assert.True(good.CanRead);
    }

    [Fact]
    public void The_length_must_be_in_0_to_Array_MaxLength()
    {
        Assert.False(RequestBody.FromStream(new MemoryStream(), contentLength: long.MaxValue).IsReplayable);
        Assert.False(RequestBody.FromStream(new MemoryStream(), contentLength: Array.MaxLength + 1L).IsReplayable);
        Assert.True(RequestBody.FromStream(new MemoryStream(), contentLength: 0).IsReplayable);
    }

    [Fact]
    public async Task A_zero_length_seekable_body_writes_nothing_and_succeeds()
    {
        var body = RequestBody.FromStream(new MemoryStream(Bytes(4)), contentLength: 0);

        Assert.Empty(await WriteAsync(body));
        Assert.Empty(await WriteAsync(body));
    }

    [Fact]
    public void Equality_is_identity()
    {
        var shared = new MemoryStream(Bytes(4));
        var first = RequestBody.FromStream(shared, contentLength: 4);
        var second = RequestBody.FromStream(shared, contentLength: 4);

        Assert.Equal(first, first);
        Assert.NotEqual(first, second);
    }
}
