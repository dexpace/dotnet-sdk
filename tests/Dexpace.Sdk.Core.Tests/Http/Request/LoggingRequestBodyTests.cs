// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/body/request-body-logging.test.ts, excluding the sink-abort and sink-close
// cases (a .NET Stream has no abort; the wrapper leaves the destination open) and "teardown is close() only" (a Node
// runtime-floor probe).

#pragma warning disable CA2000 // The streams and bodies under test are owned by the test.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary><see cref="LoggingRequestBody"/>: BODY-17 to BODY-21, BODY-32, BODY-34 (consumer clause), BODY-37.</summary>
[Trait("Category", "Unit")]
public class LoggingRequestBodyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Bytes(int count) => [.. Enumerable.Range(0, count).Select(i => (byte)(i % 251))];

    public static TheoryData<int, int> CapAndLength => new()
    {
        { 0, 0 }, { 0, 5 }, { 1, 0 }, { 1, 1 }, { 1, 5 }, { 4, 3 }, { 4, 4 }, { 4, 5 }, { 7, 1000 }, { 1000, 7 },
    };

    private static async Task<byte[]> WriteAsync(RequestBody body)
    {
        using var sink = new MemoryStream();
        await body.WriteToAsync(sink, Token);
        return sink.ToArray();
    }

    // A single-use body: the stream has no declared length, so it is not promoted to the seekable variant.
    private static RequestBody SingleUse(byte[] bytes) => RequestBody.FromStream(new MemoryStream(bytes));

    [Fact]
    public async Task The_wire_receives_every_byte_and_the_tap_mirrors_up_to_the_cap()
    {
        var payload = Bytes(100);
        var body = new LoggingRequestBody(RequestBody.FromBytes(payload), 10);

        var wire = await WriteAsync(body);

        Assert.Equal(payload, wire);
        Assert.Equal(payload[..10], body.Snapshot());
    }

    [Fact]
    public async Task The_sync_form_mirrors_and_forwards_too()
    {
        var payload = Bytes(100);
        var body = new LoggingRequestBody(RequestBody.FromBytes(payload), 10);
        using var sink = new MemoryStream();

        body.WriteTo(sink, Token);

        Assert.Equal(payload, sink.ToArray());
        Assert.Equal(payload[..10], body.Snapshot());
    }

    [Fact]
    public async Task The_tap_reflects_only_the_latest_attempt()
    {
        var first = new byte[] { 1, 2, 3 };
        var second = new byte[] { 9, 8 };
        var attempts = new Queue<byte[]>([first, second]);
        var body = new LoggingRequestBody(new SequencedBody(attempts), 100);

        _ = await WriteAsync(body);
        Assert.Equal(first, body.Snapshot());
        _ = await WriteAsync(body);

        Assert.Equal(second, body.Snapshot());
    }

    [Fact]
    public async Task A_multi_megabyte_write_mirrors_only_the_cap()
    {
        var payload = Bytes(3 * 1024 * 1024);
        var body = new LoggingRequestBody(RequestBody.FromBytes(payload), 4096);

        var wire = await WriteAsync(body);

        Assert.Equal(payload.Length, wire.Length);
        Assert.Equal(4096, body.Snapshot().Length);
        Assert.Equal(payload[..4096], body.Snapshot());
    }

    [Fact]
    public async Task A_cap_of_zero_mirrors_nothing_and_forwards_everything()
    {
        var payload = Bytes(50);
        var body = new LoggingRequestBody(RequestBody.FromBytes(payload), 0);

        Assert.Equal(payload, await WriteAsync(body));
        Assert.Empty(body.Snapshot());
    }

    [Fact]
    public async Task A_primary_failure_leaves_the_failing_chunk_captured()
    {
        var payload = Bytes(20);
        var body = new LoggingRequestBody(RequestBody.FromBytes(payload), 100);
        using var failing = new FailingWriteStream(1, new IOException("sink failed"));

        await Assert.ThrowsAsync<IOException>(() => body.WriteToAsync(failing, Token));

        // The tee mirrors before it forwards, so the chunk that failed is in the preview (BODY-20).
        Assert.Equal(payload, body.Snapshot());
    }

    [Fact]
    public async Task IsReplayable_is_the_delegates_and_a_replayable_delegate_materialises_to_this()
    {
        var replayable = new LoggingRequestBody(RequestBody.FromBytes(Bytes(3)), 5);
        var single = new LoggingRequestBody(SingleUse(Bytes(3)), 5);

        Assert.True(replayable.IsReplayable);
        Assert.False(single.IsReplayable);
        Assert.Same(replayable, await replayable.ToReplayableAsync(Token));
        Assert.Same(replayable, replayable.ToReplayable(Token));
    }

    [Fact]
    public async Task Materialising_rewraps_with_the_cap_and_a_separate_tap()
    {
        var payload = Bytes(30);
        var single = new LoggingRequestBody(SingleUse(payload), 8);

        var materialised = (LoggingRequestBody)await single.ToReplayableAsync(Token);

        Assert.NotSame(single, materialised);
        Assert.True(materialised.IsReplayable);
        Assert.Equal(payload, await WriteAsync(materialised));
        Assert.Equal(payload[..8], materialised.Snapshot());
        // The first wrapper's tap is its own: the write above did not touch it.
        Assert.Empty(single.Snapshot());
        Assert.Equal(payload, await WriteAsync(materialised));
        Assert.Equal(8, materialised.Snapshot().Length);
    }

    [Fact]
    public void The_sync_ToReplayable_rewraps_too()
    {
        var single = new LoggingRequestBody(SingleUse(Bytes(5)), 3);

        var materialised = single.ToReplayable(Token);

        Assert.IsType<LoggingRequestBody>(materialised);
        Assert.True(materialised.IsReplayable);
    }

    [Fact]
    public async Task Snapshots_are_copies()
    {
        var body = new LoggingRequestBody(RequestBody.FromBytes(Bytes(10)), 10);
        _ = await WriteAsync(body);

        var snapshot = body.Snapshot();
        snapshot[0] = 99;
        var partial = body.Snapshot(4);
        partial[1] = 99;

        Assert.Equal(Bytes(10), body.Snapshot());
        Assert.NotSame(body.Snapshot(), body.Snapshot());
    }

    [Fact]
    public async Task Snapshot_with_a_smaller_max_returns_a_prefix()
    {
        var body = new LoggingRequestBody(RequestBody.FromBytes(Bytes(10)), 10);
        _ = await WriteAsync(body);

        Assert.Equal(Bytes(10)[..3], body.Snapshot(3));
        Assert.Equal(Bytes(10), body.Snapshot(1000));
        Assert.Empty(body.Snapshot(0));
        Assert.Equal(Bytes(10), body.Snapshot(int.MaxValue));
    }

    [Fact]
    public void Negative_caps_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoggingRequestBody(RequestBody.FromBytes(Bytes(1)), -1));
        var body = new LoggingRequestBody(RequestBody.FromBytes(Bytes(1)), 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => body.Snapshot(-1));
    }

    [Fact]
    public async Task Caps_above_the_array_bound_are_clamped()
    {
        var body = new LoggingRequestBody(RequestBody.FromBytes(Bytes(10)), int.MaxValue);

        Assert.Equal(Bytes(10), await WriteAsync(body));
        Assert.Equal(Bytes(10), body.Snapshot());
    }

    [Fact]
    public async Task The_destination_is_left_open()
    {
        var destination = new DisposeCountingStream(new MemoryStream());
        var body = new LoggingRequestBody(RequestBody.FromBytes(Bytes(10)), 5);

        await body.WriteToAsync(destination, Token);
        body.WriteTo(destination, Token);

        Assert.Equal(0, destination.DisposeCount);
        Assert.True(destination.CanWrite);
    }

    [Fact]
    public void ContentType_and_ContentLength_forward_to_the_delegate()
    {
        var type = MediaType.Of("application", "octet-stream");
        var known = new LoggingRequestBody(RequestBody.FromBytes(Bytes(12), type), 5);
        var unknown = new LoggingRequestBody(SingleUse(Bytes(12)), 5);

        Assert.Equal(type, known.ContentType);
        Assert.Equal(12, known.ContentLength);
        Assert.Equal(-1, unknown.ContentLength);
        Assert.Null(unknown.ContentType);
    }

    [Fact]
    public void Equality_is_identity()
    {
        var inner = RequestBody.FromBytes(Bytes(3));
        var first = new LoggingRequestBody(inner, 5);
        var second = new LoggingRequestBody(inner, 5);

        Assert.Equal(first, first);
        Assert.NotEqual<RequestBody>(first, second);
    }

    [Theory]
    [MemberData(nameof(CapAndLength))]
    public async Task For_cap_and_body_pairs_the_consumer_receives_every_byte_and_the_capture_stays_bounded(int cap, int length)
    {
        var payload = Bytes(length);
        var body = new LoggingRequestBody(RequestBody.FromBytes(payload), cap);

        var wire = await WriteAsync(body);

        Assert.Equal(payload, wire);
        Assert.Equal(Math.Min(cap, length), body.Snapshot().Length);
        Assert.Equal(payload[..Math.Min(cap, length)], body.Snapshot());
    }

    private sealed class SequencedBody(Queue<byte[]> attempts) : RequestBody
    {
        public override MediaType? ContentType => null;

        public override bool IsReplayable => true;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            destination.WriteAsync(attempts.Dequeue(), cancellationToken).AsTask();
    }
}
