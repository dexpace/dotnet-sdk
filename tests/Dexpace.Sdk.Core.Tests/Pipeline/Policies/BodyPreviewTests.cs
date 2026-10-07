// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The responses and streams under test are released by the assertions.

using System.Security.Cryptography;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>XCUT-24, OBS-36: a preview is byte-capped and non-consuming however large the body.</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class BodyPreviewTests
{
    private const long TenMiB = 10L * 1024 * 1024;

    // A read-only stream of a known repeating pattern that is never held in memory.
    private sealed class PatternStream(long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var n = (int)Math.Min(buffer.Length, length - _position);
            for (var i = 0; i < n; i++)
            {
                buffer[i] = (byte)((_position + i) % 251);
            }

            _position += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(Read(buffer.Span));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static byte[] ExpectedHash()
    {
        using var stream = new PatternStream(TenMiB);
        return SHA256.HashData(stream);
    }

    [Fact]
    public async Task A_10_MB_body_with_a_small_cap_is_captured_in_the_cap_and_delivered_whole()
    {
        var logger = new RecordingLogger();
        var expected = ExpectedHash();
        var body = ResponseBody.FromStream(new PatternStream(TenMiB), MediaType.Parse("application/octet-stream"), TenMiB);
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(logger))
            .Build(
                new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: body)),
                new DexpaceClientOptions { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Body, BodyPreviewSize = 1024 } });

        var before = GC.GetAllocatedBytesForCurrentThread();
        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/big"), TestContext.Current.CancellationToken);
        await using var stream = await response.Body.OpenReadAsync(TestContext.Current.CancellationToken);
        var hash = await SHA256.HashDataAsync(stream, TestContext.Current.CancellationToken);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(expected, hash);
        var entry = logger.Entries.Single(e => e.EventId.Id == DexpaceLogEvents.HttpResponseId);
        Assert.Equal(1024, entry[DexpaceLogKeys.HttpResponseBodyPreviewSize]);
        Assert.Equal("[binary 1024 bytes captured]", entry[DexpaceLogKeys.HttpResponseBodyPreview]);
        Assert.Equal(TenMiB, entry[DexpaceLogKeys.HttpResponseBodySize]);

        // A ceiling, not a goal: nothing buffers the body (the call allocates a few KiB, not 10 MiB).
        Assert.True(allocated < 2 * 1024 * 1024, $"{allocated} bytes allocated");
    }

    [Fact]
    public async Task The_same_body_in_a_single_pass_is_not_replayed()
    {
        var body = ResponseBody.FromStream(new PatternStream(TenMiB), MediaType.Parse("application/octet-stream"), TenMiB);
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(new RecordingLogger()))
            .Build(
                new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: body)),
                new DexpaceClientOptions { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Body, BodyPreviewSize = 1024 } });

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/big"), TestContext.Current.CancellationToken);
        await using (await response.Body.OpenReadAsync(TestContext.Current.CancellationToken))
        {
        }

        // The tail was claimed by the first open: BODY-24, a stream-backed body is consumed once.
        await Assert.ThrowsAsync<StreamConsumedException>(() => response.Body.OpenReadAsync(TestContext.Current.CancellationToken));
    }
}
