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
    public async Task A_stream_body_that_was_read_and_then_disposed_reports_consumed_not_closed()
    {
        var body = ResponseBody.FromStream(new MemoryStream([1, 2, 3]));
        _ = await body.OpenReadAsync(Token);
        body.Dispose();

        await Assert.ThrowsAsync<StreamConsumedException>(() => body.OpenReadAsync(Token));
        Assert.Throws<StreamConsumedException>(() => body.OpenRead(Token));
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

    [Theory]
    [InlineData("stream")]
    [InlineData("bytes")]
    public async Task A_second_open_throws_and_names_the_buffering_route(string variant)
    {
        var body = variant == "stream"
            ? ResponseBody.FromStream(new MemoryStream([1]))
            : ResponseBody.FromBytes(new byte[] { 1 });
        _ = await body.OpenReadAsync(Token);

        var async = await Assert.ThrowsAsync<StreamConsumedException>(() => body.OpenReadAsync(Token));
        var sync = Assert.Throws<StreamConsumedException>(() => body.OpenRead(Token));

        Assert.Equal(ResponseBody.ConsumedMessage, async.Message);
        Assert.Equal(ResponseBody.ConsumedMessage, sync.Message);
        Assert.Contains("buffer", async.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FromBytes", async.Message, StringComparison.Ordinal);
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
