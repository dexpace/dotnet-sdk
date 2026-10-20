// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The streams under test are released by the assertions.

using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// <see cref="FirstResponseByteStream"/> (TRANSPORT-2; design §10 entry 33, P11-12): the pass-through wrapper that turns an end of stream
/// before any response byte into an <see cref="IOException"/>, and does nothing else. The wire tests prove the effect on
/// <c>SocketsHttpHandler</c>; these pin the rule on a scripted stream, including every path that must not throw.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirstResponseByteStreamTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A duplex fake: reads come from a script of chunks (an empty chunk is the end of the stream); writes are recorded in order.</summary>
    private sealed class ScriptedStream(params byte[][] reads) : Stream
    {
        private readonly Queue<byte[]> _reads = new(reads);

        public MemoryStream Written { get; } = new();

        public int Disposals { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            if (!_reads.TryPeek(out var chunk))
            {
                return 0;
            }

            var take = Math.Min(buffer.Length, chunk.Length);
            chunk.AsSpan(0, take).CopyTo(buffer);
            _ = _reads.Dequeue();
            if (take < chunk.Length)
            {
                var rest = chunk[take..];
                var remaining = _reads.ToArray();
                _reads.Clear();
                _reads.Enqueue(rest);
                foreach (var later in remaining)
                {
                    _reads.Enqueue(later);
                }
            }

            return take;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Read(buffer.Span));

        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => Written.Write(buffer);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Written.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }

    private static readonly byte[] s_end = [];

    private static async Task<int> ReadAsync(Stream stream, int count) => await stream.ReadAsync(new byte[count], Ct);

    private static int ReadSync(Stream stream, int count) => stream.Read(new byte[count], 0, count);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_end_of_stream_after_a_write_and_before_any_byte_is_the_runtimes_ResponseEnded_error(bool useAsync)
    {
        using var stream = new FirstResponseByteStream(new ScriptedStream(s_end));
        stream.Write([1, 2, 3]);

        var thrown = useAsync
            ? await Assert.ThrowsAsync<HttpIOException>(() => ReadAsync(stream, 16))
            : Assert.Throws<HttpIOException>(() => ReadSync(stream, 16));

        Assert.Equal(HttpRequestError.ResponseEnded, thrown.HttpRequestError);
        Assert.StartsWith("The connection closed before any byte of the response was received.", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_end_of_stream_with_no_write_is_still_a_zero_read(bool useAsync)
    {
        // An idle pooled connection the server closes: the runtime must see the 0 it uses to retire the connection.
        using var stream = new FirstResponseByteStream(new ScriptedStream(s_end));

        var read = useAsync ? await ReadAsync(stream, 16) : ReadSync(stream, 16);

        Assert.Equal(0, read);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_end_of_a_response_that_began_with_a_byte_is_a_zero_read(bool useAsync)
    {
        // A response delimited by the end of the connection: its first byte cleared the wait, so its end is a plain end of stream.
        using var stream = new FirstResponseByteStream(new ScriptedStream([7, 8], s_end));
        stream.Write([1]);

        var first = useAsync ? await ReadAsync(stream, 16) : ReadSync(stream, 16);
        var end = useAsync ? await ReadAsync(stream, 16) : ReadSync(stream, 16);

        Assert.Equal(2, first);
        Assert.Equal(0, end);
    }

    [Fact]
    public void The_next_request_on_the_same_connection_arms_the_wait_again()
    {
        using var stream = new FirstResponseByteStream(new ScriptedStream([7], s_end));
        stream.Write([1]);
        Assert.Equal(1, ReadSync(stream, 16));

        stream.Write([2]);

        Assert.Throws<HttpIOException>(() => ReadSync(stream, 16));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_zero_count_read_is_never_judged(bool useAsync)
    {
        // A zero-count read waits for data and says nothing about the end of the stream, armed or not.
        using var stream = new FirstResponseByteStream(new ScriptedStream(s_end));
        stream.Write([1]);

        var read = useAsync ? await ReadAsync(stream, 0) : ReadSync(stream, 0);

        Assert.Equal(0, read);
    }

    [Fact]
    public void A_zero_length_write_does_not_arm_the_wait()
    {
        using var stream = new FirstResponseByteStream(new ScriptedStream(s_end));
        stream.Write([], 0, 0);
        stream.Write(ReadOnlySpan<byte>.Empty);

        Assert.Equal(0, ReadSync(stream, 16));
    }

    [Fact]
    public async Task Every_byte_passes_through_in_both_directions_unchanged()
    {
        var inner = new ScriptedStream([10, 11, 12, 13], [14, 15]);
        using var stream = new FirstResponseByteStream(inner);

        stream.Write([1, 2], 0, 2);
        await stream.WriteAsync(new byte[] { 3 }, Ct);
#pragma warning disable CA1835 // The array overloads are the ones under test here: the wrapper overrides them as well as the Memory ones.
        await stream.WriteAsync(new byte[] { 4, 5, 6 }, 1, 2, Ct);
#pragma warning restore CA1835
        stream.Write((ReadOnlySpan<byte>)[7]);
        stream.Flush();
        await stream.FlushAsync(Ct);

        var buffer = new byte[3];
#pragma warning disable CA1835 // As above: the array overload is the one under test.
        var first = await stream.ReadAsync(buffer, 0, 3, Ct);
#pragma warning restore CA1835
        var second = stream.Read(buffer.AsSpan(0, 1));
        var rest = new byte[8];
        var third = await stream.ReadAsync(rest.AsMemory(), Ct);

        Assert.Equal(new byte[] { 1, 2, 3, 5, 6, 7 }, inner.Written.ToArray());
        Assert.Equal((3, 1, 2), (first, second, third));
        Assert.Equal(new byte[] { 14, 15 }, rest[..third]);
        Assert.True(stream.CanRead);
        Assert.True(stream.CanWrite);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [Fact]
    public async Task The_inner_stream_is_disposed_exactly_once_however_the_wrapper_is()
    {
        var sync = new ScriptedStream();
        var syncWrapper = new FirstResponseByteStream(sync);
        syncWrapper.Dispose();
        syncWrapper.Dispose();
        await syncWrapper.DisposeAsync();

        var async = new ScriptedStream();
        var asyncWrapper = new FirstResponseByteStream(async);
        await asyncWrapper.DisposeAsync();
        await asyncWrapper.DisposeAsync();
        asyncWrapper.Dispose();

        Assert.Equal(1, sync.Disposals);
        Assert.Equal(1, async.Disposals);
    }
}
