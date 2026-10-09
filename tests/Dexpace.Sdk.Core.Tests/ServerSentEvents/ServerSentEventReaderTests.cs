// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Text;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The reader: SSE-14 to SSE-18, SSE-39, SSE-40.</summary>
[Trait("Category", "Unit")]
public class ServerSentEventReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static ServerSentEventReader ReaderOver(string text, int maxLineBytes = ServerSentEventReader.DefaultMaxLineBytes) =>
        new(new MemoryStream(Utf8(text)), maxLineBytes);

    private static async Task<ServerSentEvent?> Next(ServerSentEventReader reader, bool useAsync) =>
        useAsync ? await reader.ReadNextAsync(Token) : reader.ReadNext();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadNext_returns_null_at_the_end_and_keeps_returning_null(bool useAsync)
    {
        var reader = ReaderOver("data: x\n\n");

        Assert.NotNull(await Next(reader, useAsync));
        for (var i = 0; i < 3; i++)
        {
            Assert.Null(await Next(reader, useAsync));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_pending_block_at_EOF_dispatches_once_then_null(bool useAsync)
    {
        var reader = ReaderOver("id: 5\ndata: tail");

        var pending = await Next(reader, useAsync);

        Assert.NotNull(pending);
        Assert.Equal("5", pending.Id);
        Assert.Equal(["tail"], pending.Data);
        Assert.Null(await Next(reader, useAsync));
        Assert.Null(await Next(reader, useAsync));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_source_is_never_disposed_or_closed(bool useAsync)
    {
        using var inner = new MemoryStream(Utf8("data: a\n\ndata: b\n\n"));
        using var source = new DisposeCountingStream(inner);
        var reader = new ServerSentEventReader(source);

        while (await Next(reader, useAsync) is not null)
        {
        }

        Assert.Equal(0, source.DisposeCount);
        Assert.True(source.CanRead);
    }

    [Fact]
    public void The_reader_is_not_disposable()
    {
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(ServerSentEventReader)));
        Assert.False(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ServerSentEventReader)));
    }

    [Fact]
    public void The_reader_holds_exactly_the_reviewed_instance_fields()
    {
        // SSE-16 and plan reading R1: only _bomChecked is cross-call event state; _lines is the line reader,
        // _accumulator is reset at every dispatch and _maxLineBytes only names the cap in the failure. A new field is
        // a reviewed decision, so it fails here until it is added to this list.
        var fields = typeof(ServerSentEventReader)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(f => f.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["_accumulator", "_bomChecked", "_lines", "_maxLineBytes"], fields);
    }

    [Fact]
    public void A_hostile_stream_of_many_capped_lines_with_no_blank_line_is_the_documented_residual_R1()
    {
        // P7b-5, open for the lead: the cap bounds one line, not a block, so this accumulates without limit. This test
        // DOCUMENTS that, in the Unit category on purpose: a ruling still open must not be pinned in the Security
        // class, which is never loosened.
        const int Cap = 64;
        var line = "data: " + new string('x', Cap - 6) + "\n";
        var reader = ReaderOver(string.Concat(Enumerable.Repeat(line, 100)), Cap);

        var only = reader.ReadNext();

        Assert.NotNull(only);
        Assert.Equal(100, only.Data.Count);
        Assert.Null(reader.ReadNext());
    }

    [Theory]
    [InlineData("id: 7\ndata: a\n\ndata: b\n\n")]
    [InlineData("retry: 9\ndata: a\n\ndata: b\n\n")]
    [InlineData("event: e\ndata: a\n\ndata: b\n\n")]
    [InlineData(": c\ndata: a\n\ndata: b\n\n")]
    public void No_event_state_survives_a_dispatch(string wire)
    {
        var reader = ReaderOver(wire);

        Assert.NotNull(reader.ReadNext());
        var second = reader.ReadNext();

        Assert.NotNull(second);
        Assert.Equal(["b"], second.Data);
        Assert.Null(second.Id);
        Assert.Null(second.Event);
        Assert.Null(second.Comment);
        Assert.Null(second.Retry);
    }

    [Fact]
    public void Reading_is_pull_based()
    {
        // One byte per source read makes every read countable: a pull consumes the bytes its own event needs and not
        // one more, so nothing is parsed ahead of the consumer (SSE-39). The 4 KiB buffer is a read-ahead of bytes,
        // never of events.
        string[] events = ["data: 1\n\n", "data: 22\n\n", "id: 3\ndata: 333\n\n", ": four\n\n", "data: 5\n\n"];
        using var chunked = new ChunkedReadStream(Utf8(string.Concat(events)), 1);
        using var source = new CountingReadStream(chunked);
        var reader = new ServerSentEventReader(source);

        Assert.Equal(0, source.ReadCount);

        var consumed = 0;
        foreach (var wire in events)
        {
            Assert.NotNull(reader.ReadNext());
            consumed += wire.Length;
            Assert.Equal(consumed, source.ReadCount);
        }
    }

    [Fact]
    public void A_pull_over_a_large_source_reads_at_most_one_buffer_ahead()
    {
        using var source = new CountingReadStream(new MemoryStream(Utf8(string.Concat(Enumerable.Repeat("data: x\n\n", 1000)))));
        var reader = new ServerSentEventReader(source);

        Assert.NotNull(reader.ReadNext());

        Assert.Equal(1, source.ReadCount);
        Assert.True(source.BytesServed <= 4096);
        Assert.NotNull(reader.ReadNext());
        Assert.Equal(1, source.ReadCount);
    }

    [Fact]
    public async Task The_asynchronous_form_forwards_the_token_and_the_synchronous_one_takes_none()
    {
        Assert.Empty(typeof(ServerSentEventReader).GetMethod(nameof(ServerSentEventReader.ReadNext))!.GetParameters());
        var reader = new ServerSentEventReader(new TokenObservingStream());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadNextAsync(cancelled.Token).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_zero_byte_read_is_never_issued(bool useAsync)
    {
        // IO-2: StrictReadStream throws on a zero-count read, so reaching the end proves none was issued.
        foreach (var wire in new[] { string.Empty, "data: x", "data: x\n\ndata: y\r\r" })
        {
            var reader = new ServerSentEventReader(new StrictReadStream(new ChunkedReadStream(Utf8(wire), 3)));

            while (await Next(reader, useAsync) is not null)
            {
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxLineBytes_must_be_positive(int cap)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ServerSentEventReader(new MemoryStream(), cap));

        Assert.Equal("maxLineBytes", ex.ParamName);
    }

    [Fact]
    public void A_null_or_unreadable_source_is_rejected()
    {
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => new ServerSentEventReader(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentException>(() => new ServerSentEventReader(new UnreadableStream())).ParamName);
    }

    [Fact]
    public async Task A_source_that_throws_InvalidDataException_is_not_mislabelled_as_a_line_cap()
    {
        // A decompressing stream raises InvalidDataException for corrupt input. Only the line reader's own cap failure
        // may become ServerSentEventLineTooLongException; the source's failure is the primary and passes unchanged.
        var reader = new ServerSentEventReader(new ThrowingSourceStream(new InvalidDataException("corrupt gzip")));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadNextAsync(Token).AsTask());

        Assert.Equal("corrupt gzip", ex.Message);
        Assert.IsNotType<ServerSentEventLineTooLongException>(ex);
    }

    [Fact]
    public void A_source_read_failure_surfaces_unchanged_from_the_synchronous_form()
    {
        var failure = new IOException("connection reset");
        var reader = new ServerSentEventReader(new ThrowingSourceStream(failure));

        Assert.Same(failure, Assert.Throws<IOException>(reader.ReadNext));
    }

    [Fact]
    public async Task ReadAll_is_lazy_and_reads_nothing_until_enumerated()
    {
        using var blockingInner = new MemoryStream(Utf8("data: a\n\n"));
        using var blockingSource = new CountingReadStream(blockingInner);
        using var asyncInner = new MemoryStream(Utf8("data: a\n\n"));
        using var asyncSource = new CountingReadStream(asyncInner);

        var blocking = ServerSentEventReader.ReadAll(blockingSource);
        var asynchronous = ServerSentEventReader.ReadAllAsync(asyncSource);

        Assert.Equal(0, blockingSource.ReadCount);
        Assert.Equal(0, asyncSource.ReadCount);

        using var enumerator = blocking.GetEnumerator();
        await using var asyncEnumerator = asynchronous.GetAsyncEnumerator(Token);
        Assert.Equal(0, blockingSource.ReadCount);
        Assert.Equal(0, asyncSource.ReadCount);

        Assert.True(enumerator.MoveNext());
        Assert.True(await asyncEnumerator.MoveNextAsync());
        Assert.Equal(1, blockingSource.ReadCount);
        Assert.Equal(1, asyncSource.ReadCount);
    }

    [Fact]
    public async Task ReadAll_yields_every_event_then_ends()
    {
        const string Wire = "data: a\n\nid: 2\ndata: b\n\n: tail";

        var blocking = ServerSentEventReader.ReadAll(new MemoryStream(Utf8(Wire))).ToList();
        var asynchronous = new List<ServerSentEvent>();
        await foreach (var ev in ServerSentEventReader.ReadAllAsync(new MemoryStream(Utf8(Wire))))
        {
            asynchronous.Add(ev);
        }

        Assert.Equal(3, blocking.Count);
        Assert.Equal(blocking, asynchronous);
        Assert.Equal("2", blocking[1].Id);
        Assert.Equal("tail", blocking[2].Comment);
    }

    [Fact]
    public async Task Each_view_builds_its_own_reader_so_the_BOM_is_per_stream()
    {
        byte[] wire = [0xEF, 0xBB, 0xBF, .. Utf8("data: x\n\n")];

        var first = ServerSentEventReader.ReadAll(new MemoryStream(wire)).ToList();
        var second = new List<ServerSentEvent>();
        await foreach (var ev in ServerSentEventReader.ReadAllAsync(new MemoryStream(wire)))
        {
            second.Add(ev);
        }

        Assert.Equal(["x"], Assert.Single(first).Data);
        Assert.Equal(["x"], Assert.Single(second).Data);
    }

    [Fact]
    public async Task A_view_enumerated_twice_throws_InvalidOperationException()
    {
        var blocking = ServerSentEventReader.ReadAll(new MemoryStream(Utf8("data: a\n\n")));
        using var first = blocking.GetEnumerator();
        Assert.Throws<InvalidOperationException>(() => blocking.GetEnumerator());

        var asynchronous = ServerSentEventReader.ReadAllAsync(new MemoryStream(Utf8("data: a\n\n")));
        await using var firstAsync = asynchronous.GetAsyncEnumerator(Token);
        Assert.Throws<InvalidOperationException>(() => asynchronous.GetAsyncEnumerator(Token));
    }

    [Fact]
    public async Task A_read_failure_surfaces_at_the_failing_pull_after_the_earlier_events()
    {
        var failure = new IOException("connection dropped");
        var wire = Utf8("data: 1\n\ndata: 2\n\n");

        using var blocking = ServerSentEventReader.ReadAll(new FailingAfterStream(wire, failure)).GetEnumerator();
        Assert.True(blocking.MoveNext());
        Assert.True(blocking.MoveNext());
        Assert.Same(failure, Assert.Throws<IOException>(() => blocking.MoveNext()));

        await using var asynchronous = ServerSentEventReader.ReadAllAsync(new FailingAfterStream(wire, failure)).GetAsyncEnumerator(Token);
        Assert.True(await asynchronous.MoveNextAsync());
        Assert.True(await asynchronous.MoveNextAsync());
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(async () => await asynchronous.MoveNextAsync()));
    }

    [Fact]
    public async Task The_enumeration_token_reaches_every_read()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await using var enumerator = ServerSentEventReader
            .ReadAllAsync(new ParkedAfterStream(Utf8("data: a\n\n")))
            .GetAsyncEnumerator(cancel.Token);

        Assert.True(await enumerator.MoveNextAsync());
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task The_view_does_not_close_the_source()
    {
        using var inner = new MemoryStream(Utf8("data: a\n\ndata: b\n\n"));
        using var source = new DisposeCountingStream(inner);

        Assert.Equal(2, ServerSentEventReader.ReadAll(source).Count());
        await using var early = ServerSentEventReader.ReadAllAsync(new DisposeCountingStream(new MemoryStream(Utf8("data: a\n\n")))).GetAsyncEnumerator(Token);
        Assert.True(await early.MoveNextAsync());

        Assert.Equal(0, source.DisposeCount);
        Assert.True(source.CanRead);
    }

    [Fact]
    public void ReadAll_validates_its_arguments_at_the_call_not_at_the_first_pull()
    {
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ServerSentEventReader.ReadAll(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ServerSentEventReader.ReadAllAsync(null!)).ParamName);
        Assert.Equal("maxLineBytes", Assert.Throws<ArgumentOutOfRangeException>(() => ServerSentEventReader.ReadAll(new MemoryStream(), 0)).ParamName);
        Assert.Equal("maxLineBytes", Assert.Throws<ArgumentOutOfRangeException>(() => ServerSentEventReader.ReadAllAsync(new MemoryStream(), -5)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentException>(() => ServerSentEventReader.ReadAll(new UnreadableStream())).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentException>(() => ServerSentEventReader.ReadAllAsync(new UnreadableStream())).ParamName);
    }

    [Fact]
    public void A_line_over_the_cap_surfaces_from_the_view_as_the_SSE_exception()
    {
        var view = ServerSentEventReader.ReadAll(new MemoryStream(Utf8("data: " + new string('x', 100) + "\n\n")), 32);

        using var enumerator = view.GetEnumerator();

        var ex = Assert.Throws<ServerSentEventLineTooLongException>(() => enumerator.MoveNext());
        Assert.Equal(32, ex.MaxLineBytes);
    }

    private sealed class CountingReadStream(Stream inner) : Stream
    {
        private int _reads;
        private long _bytes;

        public int ReadCount => Volatile.Read(ref _reads);

        public long BytesServed => Interlocked.Read(ref _bytes);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Count(int read)
        {
            Interlocked.Increment(ref _reads);
            Interlocked.Add(ref _bytes, read);
            return read;
        }
    }

    private sealed class TokenObservingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<int>(0);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingSourceStream(Exception failure) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw failure;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw failure;

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public override bool CanRead => false;
    }
}
