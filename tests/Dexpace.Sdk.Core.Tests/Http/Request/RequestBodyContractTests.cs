// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Serialization;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>
/// The synchronous twins and the bounded materialisation of <see cref="RequestBody"/>: HTTP-36 (3b's row; it cites these
/// tests), IO-9, IO-2, IO-40 (position D, P3a-5, P3a-12).
/// </summary>
[Trait("Category", "Unit")]
public class RequestBodyContractTests
{
    private static readonly byte[] s_payload = [10, 20, 30, 40, 50];

    public static TheoryData<string> Variants => ["bytes", "string", "value", "stream", "replayed-stream"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RequestBody Create(string variant) => variant switch
    {
        "bytes" => RequestBody.FromBytes(s_payload),
        "string" => RequestBody.FromString("hello"),
        "value" => RequestBody.FromValue(1, new BytesSerde(MediaType.Of("application", "json"), s_payload)),
        "stream" => RequestBody.FromStream(new MemoryStream(s_payload), contentLength: s_payload.Length),
        "replayed-stream" => RequestBody.FromStream(new MemoryStream(s_payload)).ToReplayable(TestContext.Current.CancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    // 3b: every replayable factory reports the same triple and writes identical bytes twice (HTTP-36, BODY-1, BODY-3).
    // Phase 3b's file and multipart bodies are added to this list as they land.
    public static TheoryData<string> ReplayableFactories => ["bytes", "string", "form", "seekable-stream"];

    private static RequestBody CreateReplayable(string variant) => variant switch
    {
        "bytes" => RequestBody.FromBytes(s_payload, MediaType.Of("application", "octet-stream")),
        "string" => RequestBody.FromString("hello"),
        "form" => RequestBody.FromForm([new("k", "v v"), new("e", "\u00E9")]),
        "seekable-stream" => RequestBody.FromStream(new MemoryStream(s_payload), contentLength: s_payload.Length),
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    [Theory]
    [MemberData(nameof(ReplayableFactories))]
    public async Task Every_replayable_factory_reports_its_exact_length_and_writes_identical_bytes_twice(string variant)
    {
        var body = CreateReplayable(variant);

        Assert.True(body.IsReplayable);
        Assert.Same(body, await body.ToReplayableAsync(Token));
        Assert.Same(body, body.ToReplayable(Token));
        var writes = new List<byte[]>();
        for (var i = 0; i < 2; i++)
        {
            using var asyncSink = new MemoryStream();
            await body.WriteToAsync(asyncSink, Token);
            using var syncSink = new MemoryStream();
            body.WriteTo(syncSink, Token);
            writes.Add(asyncSink.ToArray());
            writes.Add(syncSink.ToArray());
        }

        Assert.All(writes, w => Assert.Equal(writes[0], w));
        Assert.Equal(writes[0].LongLength, body.ContentLength);
    }

    [Fact]
    public void A_test_local_subclass_reports_minus_one_and_not_replayable_and_its_unoverridden_WriteTo_throws_NotSupportedException_naming_the_subclass()
    {
        var body = new AsyncOnlyBody(s_payload);

        Assert.Equal(-1, body.ContentLength);
        Assert.False(body.IsReplayable);
        var error = Assert.Throws<NotSupportedException>(() => body.WriteTo(new MemoryStream(), Token));
        Assert.Contains(nameof(AsyncOnlyBody), error.Message, StringComparison.Ordinal);
        Assert.Contains("WriteTo", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task Each_SDK_variant_writes_the_same_bytes_through_WriteTo_and_WriteToAsync(string variant)
    {
        using var sync = new MemoryStream();
        Create(variant).WriteTo(sync, Token);
        using var async = new MemoryStream();
        await Create(variant).WriteToAsync(async, Token);

        Assert.NotEmpty(sync.ToArray());
        Assert.Equal(sync.ToArray(), async.ToArray());
    }

    [Fact]
    public async Task A_stream_body_written_once_in_either_form_throws_StreamConsumedException_on_a_second_write_in_the_other_form()
    {
        var first = RequestBody.FromStream(new MemoryStream(s_payload));
        first.WriteTo(new MemoryStream(), Token);
        await Assert.ThrowsAsync<StreamConsumedException>(() => first.WriteToAsync(new MemoryStream(), Token));

        var second = RequestBody.FromStream(new MemoryStream(s_payload));
        await second.WriteToAsync(new MemoryStream(), Token);
        Assert.Throws<StreamConsumedException>(() => second.WriteTo(new MemoryStream(), Token));
    }

    [Fact]
    public async Task ToReplayable_and_ToReplayableAsync_return_equal_bodies()
    {
        var sync = RequestBody.FromStream(new MemoryStream(s_payload)).ToReplayable(Token);
        var async = await RequestBody.FromStream(new MemoryStream(s_payload)).ToReplayableAsync(Token);

        Assert.True(sync.IsReplayable);
        Assert.Equal(sync, async);
        Assert.Equal(sync.GetHashCode(), async.GetHashCode());
    }

    [Fact]
    public async Task ToReplayable_returns_this_for_a_replayable_body()
    {
        var body = RequestBody.FromBytes(s_payload);

        Assert.Same(body, body.ToReplayable(Token));
        Assert.Same(body, await body.ToReplayableAsync(Token));
    }

    [Fact]
    public async Task ToReplayable_refuses_a_declared_length_above_Array_MaxLength_without_consuming_the_body()
    {
        var body = new DeclaredLengthBody(s_payload, Array.MaxLength + 1L);

        Assert.Throws<BodyTooLargeException>(() => body.ToReplayable(Token));
        await Assert.ThrowsAsync<BodyTooLargeException>(() => body.ToReplayableAsync(Token));
        Assert.Equal(0, body.WriteCalls);

        // The refusal came before any write, so the body is still usable.
        using var destination = new MemoryStream();
        body.WriteTo(destination, Token);
        Assert.Equal(s_payload, destination.ToArray());
    }

    [Fact]
    public async Task ToReplayable_of_an_unknown_length_stream_body_buffers_it_and_is_then_replayable()
    {
        var body = RequestBody.FromStream(new MemoryStream(s_payload));

        var replayable = body.ToReplayable(Token);

        Assert.True(replayable.IsReplayable);
        Assert.Equal(s_payload.Length, replayable.ContentLength);
        for (var i = 0; i < 2; i++)
        {
            using var sink = new MemoryStream();
            await replayable.WriteToAsync(sink, Token);
            Assert.Equal(s_payload, sink.ToArray());
        }
    }

    [Fact]
    public async Task ToReplayableAsync_of_an_unknown_length_stream_body_buffers_it_and_is_then_replayable()
    {
        var body = RequestBody.FromStream(new MemoryStream(s_payload), MediaType.Of("application", "octet-stream"));

        var replayable = await body.ToReplayableAsync(Token);

        Assert.True(replayable.IsReplayable);
        Assert.Equal(body.ContentType, replayable.ContentType);
        using var sink = new MemoryStream();
        replayable.WriteTo(sink, Token);
        Assert.Equal(s_payload, sink.ToArray());
    }

    [Fact]
    public async Task ToReplayable_over_a_longer_than_Array_MaxLength_stream_throws_BodyTooLargeException()
    {
        // A stream longer than Array.MaxLength is too slow to build, so the limit is lowered through the internal overload.
        var sync = RequestBody.FromStream(new MemoryStream(new byte[100]));
        Assert.Throws<BodyTooLargeException>(() => sync.ToReplayableBounded(50, Token));

        var async = RequestBody.FromStream(new MemoryStream(new byte[100]));
        await Assert.ThrowsAsync<BodyTooLargeException>(() => async.ToReplayableBoundedAsync(50, Token));

        // A known length above the limit is refused before the stream is read.
        using var source = new StrictReadStream(new MemoryStream(new byte[100]));
        var known = RequestBody.FromStream(source, contentLength: 100);
        var refusal = Assert.Throws<BodyTooLargeException>(() => known.ToReplayableBounded(50, Token));
        Assert.DoesNotContain("OpenRead", refusal.Message, StringComparison.Ordinal);
        Assert.False(source.AnyRead);
    }

    [Fact]
    public async Task A_cancelled_token_aborts_ToReplayable()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Assert.Throws<OperationCanceledException>(
            () => RequestBody.FromStream(new MemoryStream(new byte[100])).ToReplayable(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RequestBody.FromStream(new MemoryStream(new byte[100])).ToReplayableAsync(cts.Token));
    }

    [Fact]
    public async Task Sync_members_add_no_zero_count_read()
    {
        using var syncSource = new StrictReadStream(new MemoryStream(new byte[300_000]));
        _ = RequestBody.FromStream(syncSource).ToReplayable(Token);
        Assert.All(syncSource.RequestedCounts, count => Assert.True(count >= 1));

        using var exactSource = new StrictReadStream(new MemoryStream(new byte[300_000]));
        RequestBody.FromStream(exactSource, contentLength: 1000).WriteTo(new MemoryStream(), Token);
        Assert.All(exactSource.RequestedCounts, count => Assert.True(count >= 1));

        using var asyncSource = new StrictReadStream(new MemoryStream(new byte[300_000]));
        _ = await RequestBody.FromStream(asyncSource).ToReplayableAsync(Token);
        Assert.All(asyncSource.RequestedCounts, count => Assert.True(count >= 1));
    }

    [Fact]
    public void The_failed_ToReplayable_leaves_a_stream_body_consumed()
    {
        // Documented: buffering a single-use body is the point of no return.
        var body = RequestBody.FromStream(new MemoryStream(new byte[100]));
        Assert.Throws<BodyTooLargeException>(() => body.ToReplayableBounded(50, Token));

        Assert.Throws<StreamConsumedException>(() => body.WriteTo(new MemoryStream(), Token));
    }

    [Fact]
    public void WriteTo_of_a_known_length_stream_body_writes_exactly_the_declared_bytes()
    {
        var body = RequestBody.FromStream(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6 }), contentLength: 4);
        using var destination = new MemoryStream();

        body.WriteTo(destination, Token);

        Assert.Equal([1, 2, 3, 4], destination.ToArray());
    }

    [Fact]
    public void WriteTo_of_a_short_known_length_stream_body_throws_EndOfStreamException()
    {
        var body = RequestBody.FromStream(new MemoryStream(new byte[] { 1, 2 }), contentLength: 4);

        var error = Assert.Throws<EndOfStreamException>(() => body.WriteTo(new MemoryStream(), Token));

        Assert.Equal("The source ended after 2 of 4 bytes.", error.Message);
    }

    private sealed class AsyncOnlyBody(byte[] payload) : RequestBody
    {
        public override MediaType? ContentType => null;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            destination.WriteAsync(payload, cancellationToken).AsTask();
    }

    private sealed class DeclaredLengthBody(byte[] payload, long declaredLength) : RequestBody
    {
        public int WriteCalls { get; private set; }

        public override MediaType? ContentType => null;

        public override long ContentLength => declaredLength;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            return destination.WriteAsync(payload, cancellationToken).AsTask();
        }

        public override void WriteTo(Stream destination, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            destination.Write(payload);
        }
    }
}
