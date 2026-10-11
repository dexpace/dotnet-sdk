// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-18, TRANSPORT-17 (phase 8b, P8b-10; R10; G5 to G7): a single-use request body is teed into a bounded capture on its
/// first serialisation and replayed on a later one, so a send the native client repeats carries identical bytes and the
/// caller's source is read once. Beyond the limit, or after a failed first write, the re-send is refused with the transport-failure
/// type and a message that names the limit. The wire rows are in <c>ResendCaptureWireTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResendCaptureTests
{
    private const int MiB = 1024 * 1024;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static byte[] Pattern(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i * 31) + 7);
        }

        return bytes;
    }

    // A non-seekable single-use source that counts what it served, and can fail after some bytes.
    private sealed class SingleUseSource(byte[] content, int failAfter = int.MaxValue) : Stream
    {
        private int _position;

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Take(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer) => Take(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Take(buffer.Span));

        private int Take(Span<byte> destination)
        {
            if (_position >= failAfter)
            {
                throw new IOException("The body source failed mid-write.");
            }

            var count = Math.Min(Math.Min(destination.Length, 16 * 1024), Math.Min(content.Length, failAfter) - _position);
            content.AsSpan(_position, count).CopyTo(destination);
            _position += count;
            BytesRead += count;
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // HttpContent.CopyTo[Async] wraps an IOException from serialisation in an HttpRequestException, exactly as the native client does
    // on a real send; the IOException is what carries the message and the cause.
    private static IOException RefusalOf(HttpRequestException wrapped) =>
        Assert.IsType<IOException>(wrapped.InnerException);

    private static RequestBodyContent ContentOver(SingleUseSource source, long limit, int? declaredLength = null)
    {
        var body = RequestBody.FromStream(source, contentLength: declaredLength ?? -1);
        return new RequestBodyContent(body, new ResendCapture(limit));
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // The content, serialised twice the way a native re-send does.

    [Fact]
    public async Task A_one_mebibyte_body_at_the_limit_replays_identical_bytes_and_the_source_is_read_once()
    {
        var payload = Pattern(MiB);
        using var source = new SingleUseSource(payload);
        using var content = ContentOver(source, MiB);
        using var first = new MemoryStream();
        using var second = new MemoryStream();

        await content.CopyToAsync(first, Ct);
        await content.CopyToAsync(second, Ct);

        Assert.Equal(payload, first.ToArray());
        Assert.Equal(payload, second.ToArray());
        Assert.Equal(MiB, source.BytesRead);
    }

    [Fact]
    public void The_synchronous_serialisation_replays_identically_too()
    {
        var payload = Pattern(300_000);
        using var source = new SingleUseSource(payload);
        using var content = ContentOver(source, MiB);
        using var first = new MemoryStream();
        using var second = new MemoryStream();

        content.CopyTo(first, context: null, Ct);
        content.CopyTo(second, context: null, Ct);

        Assert.Equal(payload, first.ToArray());
        Assert.Equal(payload, second.ToArray());
        Assert.Equal(payload.Length, source.BytesRead);
    }

    [Fact]
    public async Task A_body_one_byte_over_the_limit_still_sends_the_first_time_and_refuses_the_second_naming_the_limit()
    {
        var payload = Pattern(MiB + 1);
        using var source = new SingleUseSource(payload);
        using var content = ContentOver(source, MiB);
        using var first = new MemoryStream();

        await content.CopyToAsync(first, Ct);

        Assert.Equal(payload, first.ToArray());
        var refused = RefusalOf(await Assert.ThrowsAsync<HttpRequestException>(() => content.CopyToAsync(Stream.Null, Ct)));
        Assert.Contains("ResendBufferLimit", refused.Message, StringComparison.Ordinal);
        Assert.Contains("ToReplayableAsync", refused.Message, StringComparison.Ordinal);
        Assert.IsType<StreamConsumedException>(refused.InnerException);
        Assert.Equal(MiB + 1, source.BytesRead);
    }

    [Fact]
    public void The_synchronous_second_serialisation_refuses_the_same_way()
    {
        var payload = Pattern(2_000);
        using var source = new SingleUseSource(payload);
        using var content = ContentOver(source, limit: 1_000);

        content.CopyTo(Stream.Null, context: null, Ct);

        var refused = RefusalOf(Assert.Throws<HttpRequestException>(() => content.CopyTo(Stream.Null, context: null, Ct)));
        Assert.IsType<StreamConsumedException>(refused.InnerException);
    }

    [Fact]
    public async Task A_limit_of_zero_refuses_every_resend_of_a_body_and_an_empty_body_resends()
    {
        using var source = new SingleUseSource(Pattern(10));
        using var content = ContentOver(source, limit: 0);
        await content.CopyToAsync(Stream.Null, Ct);
        await Assert.ThrowsAsync<HttpRequestException>(() => content.CopyToAsync(Stream.Null, Ct));

        using var emptySource = new SingleUseSource([]);
        using var empty = ContentOver(emptySource, limit: 0);
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        await empty.CopyToAsync(first, Ct);
        await empty.CopyToAsync(second, Ct);

        Assert.Empty(first.ToArray());
        Assert.Empty(second.ToArray());
    }

    [Fact]
    public async Task A_source_that_fails_mid_write_fails_the_first_send_with_its_own_exception_and_a_resend_is_refused()
    {
        using var source = new SingleUseSource(Pattern(100_000), failAfter: 50_000);
        using var content = ContentOver(source, MiB);

        var first = RefusalOf(await Assert.ThrowsAsync<HttpRequestException>(() => content.CopyToAsync(Stream.Null, Ct)));
        var second = RefusalOf(await Assert.ThrowsAsync<HttpRequestException>(() => content.CopyToAsync(Stream.Null, Ct)));

        Assert.Equal("The body source failed mid-write.", first.Message);
        Assert.Contains("ResendBufferLimit", second.Message, StringComparison.Ordinal);
        Assert.IsType<StreamConsumedException>(second.InnerException);
    }

    [Fact]
    public async Task The_capture_is_released_when_the_content_is_disposed()
    {
        using var source = new SingleUseSource(Pattern(1_000));
        var capture = new ResendCapture(MiB);
        var content = new RequestBodyContent(RequestBody.FromStream(source, contentLength: -1), capture);
        await content.CopyToAsync(Stream.Null, Ct);

        content.Dispose();

        Assert.True(capture.Released);
    }

    [Fact]
    public async Task The_tee_forwards_every_write_in_order_so_the_wire_is_unchanged()
    {
        var payload = Pattern(250_000);
        using var source = new SingleUseSource(payload);
        using var content = ContentOver(source, MiB);
        using var wire = new MemoryStream();

        await content.CopyToAsync(wire, Ct);

        Assert.Equal(payload, wire.ToArray());
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // Through the transport: which constructions capture, and which bodies are never captured.

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Content?.CopyTo(Stream.Null, context: null, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is { } content)
            {
                await content.CopyToAsync(Stream.Null, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Handler_built_and_borrowed_constructions_capture_a_single_use_body_once_per_send()
    {
        using var handlerBuilt = new OkHandler();
        await using var viaHandler = new SystemNetHttpClient(handlerBuilt);
        using var borrowed = new SystemHttpClient(new OkHandler());
        await using var viaBorrowed = new SystemNetHttpClient(borrowed);
        using var source1 = new SingleUseSource(Pattern(10));
        using var source2 = new SingleUseSource(Pattern(10));

        await using (await viaHandler.ExecuteAsync(Request.Post("https://example.test/u", RequestBody.FromStream(source1, contentLength: -1)), RequestOptions.Empty, Ct))
        {
        }

        await using (await viaBorrowed.ExecuteAsync(Request.Post("https://example.test/u", RequestBody.FromStream(source2, contentLength: -1)), RequestOptions.Empty, Ct))
        {
        }

        Assert.Equal(1, viaHandler.CapturesCreated);
        Assert.Equal(1, viaBorrowed.CapturesCreated);
    }

    [Fact]
    public async Task Replayable_bodies_are_never_captured()
    {
        using var handler = new OkHandler();
        await using var transport = new SystemNetHttpClient(handler);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, Pattern(100), Ct);
            using var seekable = new MemoryStream(Pattern(100));
            RequestBody[] bodies =
            [
                RequestBody.FromBytes(Pattern(10)),
                RequestBody.FromString("text"),
                RequestBody.FromStream(seekable, contentLength: 100),
                RequestBody.FromFile(path, null, offset: 3, count: 10),
            ];

            foreach (var body in bodies)
            {
                Assert.True(body.IsReplayable);
                await using var response = await transport.ExecuteAsync(Request.Post("https://example.test/u", body), RequestOptions.Empty, Ct);
            }

            Assert.Equal(0, transport.CapturesCreated);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_request_with_no_body_never_captures()
    {
        using var handler = new OkHandler();
        await using var transport = new SystemNetHttpClient(handler);

        await using var response = await transport.ExecuteAsync(Request.Get("https://example.test/u"), RequestOptions.Empty, Ct);

        Assert.Equal(0, transport.CapturesCreated);
    }
}
