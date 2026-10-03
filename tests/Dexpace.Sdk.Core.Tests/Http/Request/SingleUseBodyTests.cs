// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/body/materialize.test.ts (the five materialisation cases) and
// packages/core/src/body/stream-body.test.ts (single use, concurrency). All are pins over behaviour that already held.

#pragma warning disable CA2000 // The streams under test are owned by the test.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>Single-use request bodies: HTTP-37, BODY-3, BODY-6, BODY-7.</summary>
[Trait("Category", "Unit")]
public class SingleUseBodyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly byte[] s_payload = [1, 2, 3, 4, 5, 6];

    // A single-use body: unknown length, so it is never promoted to the seekable variant.
    private static RequestBody SingleUse(MediaType? contentType = null) =>
        RequestBody.FromStream(new MemoryStream(s_payload), contentType);

    private static async Task<byte[]> WriteAsync(RequestBody body)
    {
        using var sink = new MemoryStream();
        await body.WriteToAsync(sink, Token);
        return sink.ToArray();
    }

    [Fact]
    public async Task A_second_write_throws_StreamConsumedException()
    {
        var body = SingleUse();

        _ = await WriteAsync(body);

        await Assert.ThrowsAsync<StreamConsumedException>(() => WriteAsync(body));
        Assert.Throws<StreamConsumedException>(() => body.WriteTo(new MemoryStream(), Token));
    }

    [Fact]
    public async Task Concurrent_writes_admit_exactly_one()
    {
        const int Contenders = 16;
        var body = SingleUse();
        using var barrier = new Barrier(Contenders);
        var tasks = Enumerable.Range(0, Contenders).Select(contender => Task.Run(
            async () =>
            {
                barrier.SignalAndWait(Token);
                _ = contender;
                try
                {
                    await body.WriteToAsync(new MemoryStream(), Token);
                    return true;
                }
                catch (StreamConsumedException)
                {
                    return false;
                }
            },
            Token)).ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Single(results, admitted => admitted);
    }

    [Fact]
    public async Task ToReplayableAsync_on_a_single_use_body_leaves_the_original_consumed()
    {
        var body = SingleUse();

        var replayable = await body.ToReplayableAsync(Token);

        Assert.True(replayable.IsReplayable);
        await Assert.ThrowsAsync<StreamConsumedException>(() => WriteAsync(body));
    }

    [Fact]
    public async Task ToReplayableAsync_on_a_replayable_body_returns_this()
    {
        var body = RequestBody.FromBytes(s_payload);

        Assert.Same(body, await body.ToReplayableAsync(Token));
    }

    [Fact]
    public async Task ToReplayableAsync_preserves_the_media_type()
    {
        var type = MediaType.Of("application", "octet-stream");

        var replayable = await SingleUse(type).ToReplayableAsync(Token);

        Assert.Equal(type, replayable.ContentType);
    }

    [Fact]
    public async Task The_materialised_body_writes_twice_identically()
    {
        var replayable = await SingleUse().ToReplayableAsync(Token);

        var first = await WriteAsync(replayable);
        var second = await WriteAsync(replayable);

        Assert.Equal(s_payload, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ToReplayableAsync_drains_the_source_once()
    {
        var source = new CountingReadStream(s_payload);
        var body = RequestBody.FromStream(source);

        var replayable = await body.ToReplayableAsync(Token);
        var readsAfterBuffering = source.Reads;
        _ = await WriteAsync(replayable);
        _ = await WriteAsync(replayable);

        Assert.Equal(readsAfterBuffering, source.Reads);
    }

    [Fact]
    public async Task Under_N_concurrent_ToReplayableAsync_callers_exactly_one_drains_and_the_rest_see_StreamConsumedException()
    {
        const int Contenders = 16;
        var body = SingleUse();
        using var barrier = new Barrier(Contenders);
        var tasks = Enumerable.Range(0, Contenders).Select(contender => Task.Run(
            async () =>
            {
                barrier.SignalAndWait(Token);
                _ = contender;
                try
                {
                    _ = await body.ToReplayableAsync(Token);
                    return true;
                }
                catch (StreamConsumedException)
                {
                    return false;
                }
            },
            Token)).ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Single(results, drained => drained);
    }

    private sealed class CountingReadStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Interlocked.Increment(ref _reads);
            return _inner.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _reads);
            return _inner.ReadAsync(buffer, cancellationToken);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
