// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The bodies under test are released by the assertions, not by a using scope.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>
/// The response-body dispose latch and its use-after-dispose rules: HTTP-41, BODY-14, BODY-15, BODY-16 (design P3b-2,
/// P3b-11, P3b-13).
/// </summary>
[Trait("Category", "Unit")]
public class ResponseBodyLifecycleTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class ReleaseCountingBody(Exception? failure = null, Func<Stream>? open = null) : ResponseBody
    {
        private int _releases;

        public int Releases => Volatile.Read(ref _releases);

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(open?.Invoke() ?? new MemoryStream([1, 2, 3]));

        public override Stream OpenRead(CancellationToken cancellationToken = default) =>
            open?.Invoke() ?? new MemoryStream([1, 2, 3]);

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _releases);
            base.Dispose(disposing);
            if (failure is not null)
            {
                throw failure;
            }
        }
    }

    private sealed class AsyncReleaseBody : ResponseBody
    {
        public int AsyncReleases { get; private set; }

        public int SyncReleases { get; private set; }

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        protected override void Dispose(bool disposing)
        {
            SyncReleases++;
            base.Dispose(disposing);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            AsyncReleases++;
            await Task.Yield();
            await base.DisposeAsyncCore();
        }
    }

    private sealed class ThrowingStream : Stream
    {
        private int _calls;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ++_calls == 1 ? FillOne(buffer, offset) : throw new IOException("mid-read failure");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ++_calls == 1 ? new ValueTask<int>(FillOne(buffer.Span)) : throw new IOException("mid-read failure");

        private static int FillOne(byte[] buffer, int offset)
        {
            buffer[offset] = 1;
            return 1;
        }

        private static int FillOne(Span<byte> buffer)
        {
            buffer[0] = 1;
            return 1;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Dispose_releases_once_across_Dispose_and_DisposeAsync_in_any_order()
    {
        var syncSync = new ReleaseCountingBody();
        syncSync.Dispose();
        syncSync.Dispose();
        Assert.Equal(1, syncSync.Releases);

        var asyncAsync = new ReleaseCountingBody();
        await asyncAsync.DisposeAsync();
        await asyncAsync.DisposeAsync();
        Assert.Equal(1, asyncAsync.Releases);

        var syncThenAsync = new ReleaseCountingBody();
        syncThenAsync.Dispose();
        await syncThenAsync.DisposeAsync();
        Assert.Equal(1, syncThenAsync.Releases);

        var asyncThenSync = new ReleaseCountingBody();
        await asyncThenSync.DisposeAsync();
        asyncThenSync.Dispose();
        Assert.Equal(1, asyncThenSync.Releases);
    }

    [Fact]
    public async Task Concurrent_disposes_release_exactly_once()
    {
        const int Contenders = 16;
        var body = new ReleaseCountingBody();
        using var barrier = new Barrier(Contenders);

        var tasks = Enumerable.Range(0, Contenders).Select(i => Task.Run(
            async () =>
            {
                barrier.SignalAndWait(Token);
                if (i % 2 == 0)
                {
                    body.Dispose();
                }
                else
                {
                    await body.DisposeAsync();
                }
            },
            Token));
        await Task.WhenAll(tasks);

        Assert.Equal(1, body.Releases);
    }

    [Fact]
    public async Task A_release_that_throws_still_flips_the_latch_and_propagates_once()
    {
        var body = new ReleaseCountingBody(new InvalidOperationException("release failed"));

        Assert.Throws<InvalidOperationException>(body.Dispose);
        body.Dispose();
        await body.DisposeAsync();

        Assert.Equal(1, body.Releases);
    }

    [Fact]
    public void A_never_read_body_is_released_by_dispose()
    {
        var body = new ReleaseCountingBody();
        body.Dispose();
        Assert.Equal(1, body.Releases);
    }

    [Fact]
    public async Task The_default_DisposeAsyncCore_calls_Dispose_true()
    {
        var body = new ReleaseCountingBody();
        await body.DisposeAsync();
        Assert.Equal(1, body.Releases);
    }

    [Fact]
    public async Task An_overridden_DisposeAsyncCore_runs_for_DisposeAsync_and_not_for_Dispose()
    {
        var asyncBody = new AsyncReleaseBody();
        await asyncBody.DisposeAsync();
        Assert.Equal(1, asyncBody.AsyncReleases);
        Assert.Equal(1, asyncBody.SyncReleases);

        var syncBody = new AsyncReleaseBody();
        syncBody.Dispose();
        Assert.Equal(0, syncBody.AsyncReleases);
        Assert.Equal(1, syncBody.SyncReleases);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_stream_body_read_after_dispose_throws_StreamClosedException(bool sync)
    {
        var body = ResponseBody.FromStream(new MemoryStream([1, 2, 3]));
        await body.DisposeAsync();

        if (sync)
        {
            Assert.Throws<StreamClosedException>(() => body.OpenRead(Token));
        }
        else
        {
            await Assert.ThrowsAsync<StreamClosedException>(() => body.OpenReadAsync(Token));
        }
    }

    [Fact]
    public async Task A_stream_body_that_was_read_and_then_disposed_reports_closed()
    {
        // BODY-14 (P11-8): an open after the first returns the same stream while the body is live; once the body is disposed the stream
        // is gone with it, so the open reports "closed" (the BCL's HttpContent does the same). It used to report "consumed" first.
        var body = ResponseBody.FromStream(new MemoryStream([1, 2, 3]));
        _ = await body.OpenReadAsync(Token);
        body.Dispose();

        await Assert.ThrowsAsync<StreamClosedException>(() => body.OpenReadAsync(Token));
        Assert.Throws<StreamClosedException>(() => body.OpenRead(Token));
    }

    // The first row is a pin (a body disposed before it was opened was already "closed"); the other two fail until BODY-14 lands.
    [Theory]
    [InlineData("stream", false)]
    [InlineData("stream", true)]
    [InlineData("bytes", true)]
    public async Task An_open_after_dispose_still_throws_StreamClosedException(string variant, bool openedFirst)
    {
        var body = variant == "stream"
            ? ResponseBody.FromStream(new MemoryStream([1, 2, 3]))
            : ResponseBody.FromBytes(new byte[] { 1, 2, 3 });
        if (openedFirst)
        {
            _ = await body.OpenReadAsync(Token);
        }

        await body.DisposeAsync();

        await Assert.ThrowsAsync<StreamClosedException>(() => body.OpenReadAsync(Token));
        Assert.Throws<StreamClosedException>(() => body.OpenRead(Token));
    }

    [Theory]
    [InlineData("stream", false)]
    [InlineData("stream", true)]
    [InlineData("bytes", false)]
    [InlineData("bytes", true)]
    public async Task A_second_open_returns_the_same_stream(string variant, bool syncFirst)
    {
        // BODY-14, HTTP-41: requesting the read handle repeatedly returns the same underlying handle, not a replay and not a failure,
        // whichever form asks first (position B: the two forms share the one handle).
        var body = variant == "stream"
            ? ResponseBody.FromStream(new MemoryStream([1, 2, 3]))
            : ResponseBody.FromBytes(new byte[] { 1, 2, 3 });

        var first = syncFirst ? body.OpenRead(Token) : await body.OpenReadAsync(Token);
        var second = syncFirst ? await body.OpenReadAsync(Token) : body.OpenRead(Token);
        var third = body.OpenRead(Token);

        Assert.Same(first, second);
        Assert.Same(first, third);
        body.Dispose();
    }

    // Review fix (CLAUDE.md said a stream-backed response body "throws StreamConsumedException on a second read"): a convenience read disposes the
    // body, so the second one reports the dispose, and a stream-backed response body never raises StreamConsumedException (that is the request side's
    // second write and the logging wrapper's tail past its cap).
    [Theory]
    [InlineData("bytes", false)]
    [InlineData("bytes", true)]
    [InlineData("string", false)]
    [InlineData("string", true)]
    public async Task A_second_convenience_read_of_a_stream_body_reports_closed_never_consumed(string reader, bool sync)
    {
        var body = ResponseBody.FromStream(new MemoryStream([104, 105]));
        _ = sync ? Read(body, reader) : await ReadAsync(body, reader);

        var thrown = sync
            ? Assert.Throws<StreamClosedException>(() => Read(body, reader))
            : await Assert.ThrowsAsync<StreamClosedException>(() => ReadAsync(body, reader));

        Assert.IsNotType<StreamConsumedException>(thrown);

        static object Read(ResponseBody body, string reader) => reader == "bytes" ? body.ReadAsBytes(Token) : body.ReadAsString(cancellationToken: Token);
        static async Task<object> ReadAsync(ResponseBody body, string reader) =>
            reader == "bytes" ? await body.ReadAsBytesAsync(Token) : await body.ReadAsStringAsync(cancellationToken: Token);
    }

    [Fact]
    public async Task A_convenience_read_after_a_manual_drain_of_a_stream_body_returns_the_empty_remainder_of_the_same_handle()
    {
        var body = ResponseBody.FromStream(new MemoryStream([1, 2, 3]));
        var handle = await body.OpenReadAsync(Token);
        using var drained = new MemoryStream();
        await handle.CopyToAsync(drained, Token);

        var rest = await body.ReadAsBytesAsync(Token);

        Assert.Equal(new byte[] { 1, 2, 3 }, drained.ToArray());
        Assert.Empty(rest);
    }

    [Fact]
    public async Task A_typed_reader_after_a_manual_open_reads_the_remaining_bytes_of_the_same_handle()
    {
        // BODY-14: the convenience readers no longer refuse a body that was opened; they continue the one handle.
        var body = ResponseBody.FromBytes(new byte[] { 1, 2, 3, 4, 5 });
        var handle = await body.OpenReadAsync(Token);
        Assert.Equal(1, handle.ReadByte());
        Assert.Equal(2, handle.ReadByte());

        var rest = await body.ReadAsBytesAsync(Token);

        Assert.Equal(new byte[] { 3, 4, 5 }, rest);
    }

    [Fact]
    public async Task An_unread_in_memory_body_still_serves_after_dispose()
    {
        var body = ResponseBody.FromBytes(new byte[] { 7, 8, 9 });
        body.Dispose();

        Assert.Equal(new byte[] { 7, 8, 9 }, await ReadAllAsync(body));
    }

    private static async Task<byte[]> ReadAllAsync(ResponseBody body)
    {
        await using var stream = await body.OpenReadAsync(Token);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, Token);
        return buffer.ToArray();
    }

    [Fact]
    public async Task The_release_of_the_stream_variant_disposes_the_source_exactly_once()
    {
        var syncSource = new DisposeCountingStream(new MemoryStream());
        var syncBody = ResponseBody.FromStream(syncSource);
        syncBody.Dispose();
        syncBody.Dispose();
        Assert.Equal(1, syncSource.DisposeCount);

        var asyncSource = new DisposeCountingStream(new MemoryStream());
        var asyncBody = ResponseBody.FromStream(asyncSource);
        await asyncBody.DisposeAsync();
        asyncBody.Dispose();
        await asyncBody.DisposeAsync();
        Assert.Equal(1, asyncSource.DisposeCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Readers_dispose_the_body_on_success_and_on_failure(bool sync, bool text)
    {
        var ok = new ReleaseCountingBody();
        var failing = new ReleaseCountingBody(open: () => new ThrowingStream());

        if (sync)
        {
            _ = text ? ok.ReadAsString(Token).Length : ok.ReadAsBytes(Token).Length;
            Assert.Throws<IOException>(() =>
            {
                _ = text ? failing.ReadAsString(Token).Length : failing.ReadAsBytes(Token).Length;
            });
        }
        else
        {
            _ = text ? (await ok.ReadAsStringAsync(Token)).Length : (await ok.ReadAsBytesAsync(Token)).Length;
            await Assert.ThrowsAsync<IOException>(async () =>
            {
                _ = text ? (await failing.ReadAsStringAsync(Token)).Length : (await failing.ReadAsBytesAsync(Token)).Length;
            });
        }

        Assert.Equal(1, ok.Releases);
        Assert.Equal(1, failing.Releases);
    }
}
