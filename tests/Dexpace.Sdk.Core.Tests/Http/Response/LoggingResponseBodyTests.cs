// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/body/response-body-logging.test.ts, excluding the WritableStream abort
// cases (a host fact) and "teardown is close() only" (a Node runtime-floor probe). The Node chunk-contract cases about a
// zero-length chunk are the .NET zero-count read rule (BODY-25) and are tested as such.

#pragma warning disable CA2000 // The streams and bodies under test are released by the assertions.

using System.Diagnostics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary><see cref="LoggingResponseBody"/>: BODY-22 to BODY-29, BODY-32, BODY-34 (consumer clause); design P3b-9, P3b-10.</summary>
[Trait("Category", "Unit")]
public sealed class LoggingResponseBodyTests : IDisposable
{
    private readonly ActivitySource _source = new("Dexpace.Sdk.Core.Tests.LoggingResponseBody");
    private readonly ActivityListener _listener;

    public LoggingResponseBodyTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == _source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<int, int> CapAndLength => new()
    {
        { 0, 0 }, { 0, 5 }, { 1, 0 }, { 1, 1 }, { 1, 5 }, { 4, 3 }, { 4, 4 }, { 4, 5 }, { 7, 1000 }, { 1000, 7 }, { 10, 200_000 },
    };

    private static byte[] Bytes(int count) => [.. Enumerable.Range(0, count).Select(i => (byte)(i % 251))];

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using var scope = stream;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, Token);
        return buffer.ToArray();
    }

    // A delegate that counts how often it was opened and released and what its stream was asked for.
    private sealed class SpyBody(Func<Stream> open, Exception? disposeFailure = null, long declaredLength = -1, MediaType? type = null)
        : ResponseBody
    {
        private int _opens;
        private int _releases;

        public int Opens => Volatile.Read(ref _opens);

        public int Releases => Volatile.Read(ref _releases);

        public override MediaType? ContentType => type;

        public override long ContentLength => declaredLength;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _opens);
            return Task.FromResult(open());
        }

        public override Stream OpenRead(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _opens);
            return open();
        }

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _releases);
            base.Dispose(disposing);
            if (disposeFailure is not null)
            {
                throw disposeFailure;
            }
        }
    }

    // Serves its data in small chunks, and fails the test run if asked for a zero-count read.
    private sealed class ProbeStream(byte[] data, int chunk = 7, Exception? failAtEnd = null) : Stream
    {
        private readonly MemoryStream _inner = new(data);
        private int _reads;
        private int _disposals;

        public int Reads => Volatile.Read(ref _reads);

        public int Disposals => Volatile.Read(ref _disposals);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                throw new InvalidOperationException("A zero-count read was issued (BODY-25).");
            }

            Interlocked.Increment(ref _reads);
            var read = _inner.Read(buffer[..Math.Min(buffer.Length, chunk)]);
            return read == 0 && failAtEnd is not null ? throw failAtEnd : read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(Read(buffer.Span));

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
                Interlocked.Increment(ref _disposals);
            }

            base.Dispose(disposing);
        }
    }

    // Reads park until the caller's token is cancelled (a wedged upstream).
    private sealed class HungStream : Stream
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

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

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static (LoggingResponseBody Wrapper, SpyBody Spy, ProbeStream Stream) Wrap(byte[] data, int cap, int chunk = 7, long declared = -1)
    {
        var stream = new ProbeStream(data, chunk);
        var spy = new SpyBody(() => stream, declaredLength: declared);
        return (new LoggingResponseBody(spy, cap), spy, stream);
    }

    // ---- laziness, concurrency, triggers (BODY-22) ----

    [Fact]
    public async Task Nothing_is_read_until_the_first_access()
    {
        var (wrapper, spy, stream) = Wrap(Bytes(10), 100);

        Assert.Equal(0, spy.Opens);
        Assert.Equal(0, stream.Reads);
        Assert.Null(wrapper.DrainFailure);
        Assert.False(wrapper.IsFullyCaptured);
        await wrapper.DisposeAsync();
    }

    [Fact]
    public async Task The_first_read_triggers_exactly_one_upstream_drain_under_16_concurrent_readers()
    {
        const int Contenders = 16;
        var (wrapper, spy, _) = Wrap(Bytes(1000), 4096);
        using var barrier = new Barrier(Contenders);

        var results = await Task.WhenAll(Enumerable.Range(0, Contenders).Select(i => Task.Run(
            async () =>
            {
                barrier.SignalAndWait(Token);
                return i % 2 == 0
                    ? await ReadAllAsync(await wrapper.OpenReadAsync(Token))
                    : await wrapper.SnapshotAsync(Token);
            },
            Token)));

        Assert.Equal(1, spy.Opens);
        Assert.All(results, r => Assert.Equal(Bytes(1000), r));
    }

    [Fact]
    public async Task A_snapshot_triggers_the_drain()
    {
        var (wrapper, spy, _) = Wrap(Bytes(20), 100);

        var snapshot = await wrapper.SnapshotAsync(Token);

        Assert.Equal(Bytes(20), snapshot);
        Assert.Equal(1, spy.Opens);
        Assert.True(wrapper.IsFullyCaptured);
        // The later read reuses the same drain.
        Assert.Equal(Bytes(20), await ReadAllAsync(await wrapper.OpenReadAsync(Token)));
        Assert.Equal(1, spy.Opens);
    }

    [Fact]
    public async Task Sync_OpenRead_drains_once_and_serves_the_same_bytes()
    {
        var (wrapper, spy, _) = Wrap(Bytes(50), 100);

        using var first = wrapper.OpenRead(Token);
        using var second = wrapper.OpenRead(Token);

        Assert.Equal(1, spy.Opens);
        using var a = new MemoryStream();
        first.CopyTo(a);
        using var b = new MemoryStream();
        await second.CopyToAsync(b, Token);
        Assert.Equal(Bytes(50), a.ToArray());
        Assert.Equal(Bytes(50), b.ToArray());
    }

    [Fact]
    public async Task A_sync_and_an_async_first_reader_race_to_one_drain()
    {
        const int Contenders = 16;
        var (wrapper, spy, _) = Wrap(Bytes(5000), 100_000);
        using var barrier = new Barrier(Contenders);

        await Task.WhenAll(Enumerable.Range(0, Contenders).Select(i => Task.Run(
            async () =>
            {
                barrier.SignalAndWait(Token);
                if (i % 2 == 0)
                {
                    using var sync = wrapper.OpenRead(Token);
                }
                else
                {
                    await using var async = await wrapper.OpenReadAsync(Token);
                }
            },
            Token)));

        Assert.Equal(1, spy.Opens);
    }

    // ---- failure caching (BODY-26) ----

    [Fact]
    public async Task The_failure_query_never_starts_a_drain()
    {
        var (wrapper, spy, _) = Wrap(Bytes(10), 100);

        Assert.Null(wrapper.DrainFailure);
        Assert.Null(wrapper.DrainFailure);

        Assert.Equal(0, spy.Opens);
        await wrapper.DisposeAsync();
    }

    [Fact]
    public async Task A_drain_failure_is_cached_and_the_partial_bytes_kept()
    {
        var failure = new IOException("upstream failed");
        var stream = new ProbeStream(Bytes(20), chunk: 7, failAtEnd: failure);
        var wrapper = new LoggingResponseBody(new SpyBody(() => stream), 100);

        var first = await Assert.ThrowsAsync<IOException>(() => wrapper.OpenReadAsync(Token));
        var second = await Assert.ThrowsAsync<IOException>(() => wrapper.OpenReadAsync(Token));
        var third = Assert.Throws<IOException>(() => wrapper.OpenRead(Token));

        Assert.Same(failure, first);
        Assert.Same(failure, second);
        Assert.Same(failure, third);
        Assert.Same(failure, wrapper.DrainFailure);
        Assert.Equal(Bytes(20), await wrapper.SnapshotAsync(Token));
        Assert.Equal(Bytes(20)[..5], await wrapper.SnapshotAsync(5, Token));
        Assert.False(wrapper.IsFullyCaptured);
    }

    [Fact]
    public async Task A_failure_to_open_the_delegate_is_cached_too()
    {
        var failing = new SpyBody(() => throw new InvalidOperationException("cannot open"));
        var wrapper = new LoggingResponseBody(failing, 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => wrapper.OpenReadAsync(Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => wrapper.OpenReadAsync(Token));

        Assert.Equal(1, failing.Opens);
        Assert.Empty(await wrapper.SnapshotAsync(Token));
    }

    // ---- zero-count rule (BODY-25) ----

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(1000)]
    public async Task Never_issues_a_zero_count_read(int cap)
    {
        // The probe throws InvalidOperationException on a zero-count read; the drain would cache it as a failure.
        var (wrapper, _, _) = Wrap(Bytes(300), cap);

        await using var stream = await wrapper.OpenReadAsync(Token);
        _ = await ReadAllAsync(stream);

        Assert.Null(wrapper.DrainFailure);
    }

    // ---- cancellation (P3b-10) ----

    [Fact]
    public async Task The_drain_is_cancelled_by_the_starting_accessors_token_and_the_failure_is_cached()
    {
        var hung = new HungStream();
        var wrapper = new LoggingResponseBody(new SpyBody(() => hung), 100);
        using var cts = new CancellationTokenSource();

        var starter = wrapper.OpenReadAsync(cts.Token);
        await hung.Entered;
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starter);
        Assert.IsAssignableFrom<OperationCanceledException>(wrapper.DrainFailure);
        // Later readers see the cached cancellation, with a token that was never cancelled.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wrapper.OpenReadAsync(Token));
        Assert.Empty(await wrapper.SnapshotAsync(Token));
    }

    [Fact]
    public async Task Dispose_cancels_a_running_drain()
    {
        var hung = new HungStream();
        var wrapper = new LoggingResponseBody(new SpyBody(() => hung), 100);

        var reader = wrapper.OpenReadAsync(Token);
        await hung.Entered;
        await wrapper.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader);
        Assert.IsAssignableFrom<OperationCanceledException>(wrapper.DrainFailure);
    }

    [Fact]
    public async Task The_semaphore_wait_honours_the_callers_token()
    {
        var hung = new HungStream();
        var wrapper = new LoggingResponseBody(new SpyBody(() => hung), 100);
        var starter = wrapper.OpenReadAsync(Token);
        await hung.Entered;
        using var cts = new CancellationTokenSource();
        var waiter = wrapper.OpenReadAsync(cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        // The waiter's cancellation did not touch the drain: it is still running and still uncached.
        Assert.False(starter.IsCompleted);
        Assert.Null(wrapper.DrainFailure);
        await wrapper.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starter);
    }

    // ---- regimes (BODY-23, BODY-24) ----

    [Fact]
    public async Task Fits_cap_every_read_is_an_independent_view()
    {
        var (wrapper, spy, stream) = Wrap(Bytes(40), 100);

        var first = await wrapper.OpenReadAsync(Token);
        var second = await wrapper.OpenReadAsync(Token);
        var third = wrapper.OpenRead(Token);

        Assert.NotSame(first, second);
        Assert.False(first.CanWrite);
        Assert.Equal(Bytes(40), await ReadAllAsync(first));
        Assert.Equal(Bytes(40), await ReadAllAsync(second));
        using var sink = new MemoryStream();
        third.CopyTo(sink);
        Assert.Equal(Bytes(40), sink.ToArray());
        Assert.Equal(1, spy.Releases);
        Assert.Equal(1, stream.Disposals);
        Assert.True(wrapper.IsFullyCaptured);
    }

    [Fact]
    public async Task Over_cap_the_consumer_receives_every_byte_once()
    {
        var (wrapper, spy, _) = Wrap(Bytes(500), 64);

        var stream = await wrapper.OpenReadAsync(Token);

        Assert.Equal(0, spy.Releases);
        Assert.False(wrapper.IsFullyCaptured);
        Assert.Equal(Bytes(500), await ReadAllAsync(stream));
        Assert.Equal(1, spy.Releases);
        Assert.Equal(Bytes(500)[..64], await wrapper.SnapshotAsync(Token));
    }

    [Fact]
    public async Task Over_cap_a_second_read_throws_StreamConsumedException()
    {
        var (wrapper, _, _) = Wrap(Bytes(500), 64);
        await using var first = await wrapper.OpenReadAsync(Token);

        await Assert.ThrowsAsync<StreamConsumedException>(() => wrapper.OpenReadAsync(Token));
        Assert.Throws<StreamConsumedException>(() => wrapper.OpenRead(Token));
    }

    [Fact]
    public async Task A_cap_of_zero_serves_the_whole_body_through_the_tail()
    {
        var (wrapper, _, _) = Wrap(Bytes(30), 0);

        Assert.Equal(Bytes(30), await ReadAllAsync(await wrapper.OpenReadAsync(Token)));
        Assert.Empty(await wrapper.SnapshotAsync(Token));
    }

    // ---- close once (BODY-27) and close failures (BODY-28) ----

    [Fact]
    public async Task The_delegate_is_closed_at_most_once_across_every_path()
    {
        var (wrapper, spy, _) = Wrap(Bytes(500), 64);
        var tail = await wrapper.OpenReadAsync(Token);

        await tail.DisposeAsync();
        tail.Dispose();
        wrapper.Dispose();
        await wrapper.DisposeAsync();

        Assert.Equal(1, spy.Releases);
    }

    [Fact]
    public async Task A_throwing_delegate_dispose_still_flips_the_guard_and_propagates_once()
    {
        var spy = new SpyBody(() => new ProbeStream(Bytes(500)), new InvalidOperationException("close failed"));
        var wrapper = new LoggingResponseBody(spy, 64);
        var tail = await wrapper.OpenReadAsync(Token);

        Assert.Throws<InvalidOperationException>(tail.Dispose);
        tail.Dispose();
        wrapper.Dispose();
        await wrapper.DisposeAsync();

        Assert.Equal(1, spy.Releases);
    }

    [Fact]
    public async Task The_wrapper_dispose_after_a_tail_read_to_the_end_does_not_release_twice()
    {
        var (wrapper, spy, _) = Wrap(Bytes(200), 10);

        _ = await ReadAllAsync(await wrapper.OpenReadAsync(Token));
        wrapper.Dispose();

        Assert.Equal(1, spy.Releases);
    }

    [Fact]
    public async Task A_close_failure_after_full_capture_is_reported_not_raised()
    {
        using var scope = _source.StartActivity("call");
        var spy = new SpyBody(() => new ProbeStream(Bytes(10)), new InvalidOperationException("close failed"));
        var wrapper = new LoggingResponseBody(spy, 100);

        var bytes = await ReadAllAsync(await wrapper.OpenReadAsync(Token));

        Assert.Equal(Bytes(10), bytes);
        Assert.Null(wrapper.DrainFailure);
        Assert.True(wrapper.IsFullyCaptured);
        var reported = Assert.Single(scope!.Events);
        Assert.Equal("exception", reported.Name);
        Assert.Equal(true, reported.Tags.ToDictionary(t => t.Key, t => t.Value)["dexpace.dispose.suppressed"]);
    }

    [Fact]
    public async Task Snapshot_survives_dispose()
    {
        var (wrapper, _, _) = Wrap(Bytes(40), 100);
        _ = await wrapper.SnapshotAsync(Token);

        await wrapper.DisposeAsync();

        Assert.Equal(Bytes(40), await wrapper.SnapshotAsync(Token));
        // The fits-cap regime still serves repeatable reads after the wrapper's own close.
        Assert.Equal(Bytes(40), await ReadAllAsync(await wrapper.OpenReadAsync(Token)));
        Assert.Null(wrapper.DrainFailure);
    }

    [Fact]
    public async Task Dispose_before_any_read_leaves_the_snapshot_empty_and_starts_no_drain()
    {
        var (wrapper, spy, _) = Wrap(Bytes(40), 100);

        await wrapper.DisposeAsync();

        Assert.Empty(await wrapper.SnapshotAsync(Token));
        Assert.Null(wrapper.DrainFailure);
        Assert.Equal(0, spy.Opens);
        await Assert.ThrowsAsync<StreamClosedException>(() => wrapper.OpenReadAsync(Token));
    }

    [Fact]
    public async Task Dispose_then_read_in_the_over_cap_regime_throws_StreamClosedException()
    {
        var (wrapper, _, _) = Wrap(Bytes(500), 64);
        _ = await wrapper.SnapshotAsync(Token);

        await wrapper.DisposeAsync();

        await Assert.ThrowsAsync<StreamClosedException>(() => wrapper.OpenReadAsync(Token));
        Assert.Equal(Bytes(500)[..64], await wrapper.SnapshotAsync(Token));
    }

    // ---- lengths and caps (BODY-29, BODY-32, BODY-34) ----

    [Fact]
    public async Task Content_length_is_the_captured_size_only_after_full_capture()
    {
        var (fits, _, _) = Wrap(Bytes(40), 100, declared: 9999);
        var (exceeds, _, _) = Wrap(Bytes(40), 10, declared: 9999);

        Assert.Equal(9999, fits.ContentLength);
        _ = await fits.SnapshotAsync(Token);
        _ = await exceeds.SnapshotAsync(Token);

        Assert.Equal(40, fits.ContentLength);
        Assert.Equal(9999, exceeds.ContentLength);
    }

    [Fact]
    public void ContentType_forwards_to_the_delegate()
    {
        var type = MediaType.Of("application", "json");
        var wrapper = new LoggingResponseBody(new SpyBody(() => new MemoryStream(), type: type), 10);

        Assert.Equal(type, wrapper.ContentType);
    }

    [Fact]
    public async Task Negative_caps_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoggingResponseBody(ResponseBody.FromBytes(Bytes(1)), -1));
        var (wrapper, _, _) = Wrap(Bytes(5), 5);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => wrapper.SnapshotAsync(-1, Token));
    }

    [Fact]
    public async Task Caps_above_the_array_bound_are_clamped()
    {
        var wrapper = new LoggingResponseBody(ResponseBody.FromBytes(Bytes(100)), int.MaxValue);

        Assert.Equal(Bytes(100), await wrapper.SnapshotAsync(int.MaxValue, Token));
        Assert.True(wrapper.IsFullyCaptured);
    }

    [Fact]
    public async Task Snapshot_returns_what_exists_up_to_the_cap()
    {
        var (wrapper, _, _) = Wrap(Bytes(40), 100);

        Assert.Equal(Bytes(40)[..7], await wrapper.SnapshotAsync(7, Token));
        Assert.Equal(Bytes(40), await wrapper.SnapshotAsync(1000, Token));
        Assert.Empty(await wrapper.SnapshotAsync(0, Token));
    }

    [Fact]
    public async Task A_snapshot_of_an_over_cap_body_returns_exactly_the_cap()
    {
        var (wrapper, _, _) = Wrap(Bytes(500), 64);

        Assert.Equal(Bytes(500)[..64], await wrapper.SnapshotAsync(Token));
        Assert.Equal(64, (await wrapper.SnapshotAsync(int.MaxValue, Token)).Length);
    }

    [Fact]
    public async Task Snapshots_are_copies()
    {
        var (wrapper, _, _) = Wrap(Bytes(10), 100);
        var first = await wrapper.SnapshotAsync(Token);
        first[0] = 99;

        Assert.Equal(Bytes(10), await wrapper.SnapshotAsync(Token));
        Assert.Equal(Bytes(10), await ReadAllAsync(await wrapper.OpenReadAsync(Token)));
    }

    [Theory]
    [MemberData(nameof(CapAndLength))]
    public async Task For_cap_and_body_pairs_the_consumer_receives_every_byte_and_the_capture_stays_bounded(int cap, int length)
    {
        var payload = Bytes(length);
        var (wrapper, _, _) = Wrap(payload, cap, chunk: 13);

        var received = await ReadAllAsync(await wrapper.OpenReadAsync(Token));
        var captured = await wrapper.SnapshotAsync(Token);

        Assert.Equal(payload, received);
        Assert.Equal(Math.Min(cap, length), captured.Length);
        Assert.Equal(payload[..captured.Length], captured);
        Assert.Equal(length <= cap, wrapper.IsFullyCaptured);
    }

    [Fact]
    public async Task IsFullyCaptured_reflects_the_regime()
    {
        var (fits, _, _) = Wrap(Bytes(10), 10);
        var (exceeds, _, _) = Wrap(Bytes(11), 10);

        _ = await fits.SnapshotAsync(Token);
        _ = await exceeds.SnapshotAsync(Token);

        Assert.True(fits.IsFullyCaptured);
        Assert.False(exceeds.IsFullyCaptured);
    }

    [Fact]
    public async Task The_convenience_readers_work_through_the_wrapper()
    {
        var fits = new LoggingResponseBody(ResponseBody.FromBytes(Bytes(20)), 100);
        var exceeds = new LoggingResponseBody(ResponseBody.FromBytes(Bytes(20)), 5);

        Assert.Equal(Bytes(20), await fits.ReadAsBytesAsync(Token));
        Assert.Equal(Bytes(20), await exceeds.ReadAsBytesAsync(Token));
        Assert.Equal(Bytes(20), new LoggingResponseBody(ResponseBody.FromBytes(Bytes(20)), 5).ReadAsBytes(Token));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(4, 3)]
    [InlineData(4, 4)]
    [InlineData(4, 5)]
    [InlineData(10, 200_000)]
    public async Task Snapshot_sync_returns_the_same_bytes_as_SnapshotAsync(int cap, int length)
    {
        var (syncWrapper, _, _) = Wrap(Bytes(length), cap);
        var (asyncWrapper, _, _) = Wrap(Bytes(length), cap);

        var sync = syncWrapper.Snapshot(cap, Token);
        var async = await asyncWrapper.SnapshotAsync(cap, Token);

        Assert.Equal(async, sync);
        Assert.Equal(Math.Min(cap, length), sync.Length);
        Assert.Equal(Bytes(length)[..sync.Length], sync);
        if (sync.Length > 0)
        {
            Assert.NotSame(sync, syncWrapper.Snapshot(cap, Token));
        }

        Assert.Equal(sync[..Math.Min(2, sync.Length)], syncWrapper.Snapshot(2, Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => syncWrapper.Snapshot(-1, Token));
    }

    [Fact]
    public void Snapshot_sync_never_throws_a_drain_failure_and_exposes_it_through_DrainFailure()
    {
        var failure = new IOException("upstream failed");
        var wrapper = new LoggingResponseBody(new SpyBody(() => new ProbeStream(Bytes(20), chunk: 7, failAtEnd: failure)), 100);

        var snapshot = wrapper.Snapshot(100, Token);

        Assert.Equal(Bytes(20), snapshot);
        Assert.Same(failure, wrapper.DrainFailure);
        Assert.Throws<IOException>(() => wrapper.OpenRead(Token));
    }

    [Fact]
    public async Task Snapshot_sync_honours_the_starters_token()
    {
        var (wrapper, _, _) = Wrap(Bytes(10), 100);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Assert.ThrowsAny<OperationCanceledException>(() => wrapper.Snapshot(100, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wrapper.SnapshotAsync(100, cancelled.Token));
    }

    [Fact]
    public async Task Snapshot_sync_after_dispose_returns_what_was_captured()
    {
        var (captured, _, _) = Wrap(Bytes(10), 100);
        _ = captured.Snapshot(100, Token);
        captured.Dispose();

        var (neverDrained, _, _) = Wrap(Bytes(10), 100);
        await neverDrained.DisposeAsync();

        Assert.Equal(Bytes(10), captured.Snapshot(100, Token));
        Assert.Empty(neverDrained.Snapshot(100, Token));
    }

    [Fact]
    public void A_throwing_delegate_dispose_during_a_quiet_close_is_reported_to_the_supplied_logger()
    {
        var logger = new RecordingLogger();
        var wrapper = new LoggingResponseBody(
            new SpyBody(() => new ProbeStream(Bytes(10)), disposeFailure: new IOException("release failed")),
            100,
            logger);

        _ = wrapper.Snapshot(100, Token);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(DexpaceLogEvents.DisposeSuppressedId, entry.EventId.Id);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, entry.Level);
    }

    [Fact]
    public async Task A_throwing_delegate_dispose_during_an_async_quiet_close_is_reported_too()
    {
        var logger = new RecordingLogger();
        var wrapper = new LoggingResponseBody(
            new SpyBody(() => new ProbeStream(Bytes(10)), disposeFailure: new IOException("release failed")),
            100,
            logger);

        _ = await wrapper.SnapshotAsync(100, Token);

        Assert.Equal(DexpaceLogEvents.DisposeSuppressedId, Assert.Single(logger.Entries).EventId.Id);
    }

    [Fact]
    public void Constructing_without_a_logger_is_unchanged()
    {
        var wrapper = new LoggingResponseBody(
            new SpyBody(() => new ProbeStream(Bytes(10)), disposeFailure: new IOException("release failed")),
            100);

        Assert.Equal(Bytes(10), wrapper.Snapshot(100, Token));
    }
}
