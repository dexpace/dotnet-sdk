// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>
/// The response body contract: the sync twins, the single-open rule across both forms, the materialisation cap and the
/// shared decode (IO-2, IO-3, IO-9, IO-11, IO-13, IO-40; position B, P3a-3, P3a-12).
/// </summary>
[Trait("Category", "Unit")]
public class ResponseBodyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Bytes(int length) => Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();

    [Fact]
    public void ResponseBody_FromStream_rejects_a_content_length_below_minus_one()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => ResponseBody.FromStream(new MemoryStream(), contentLength: -2));

        Assert.Equal("contentLength", error.ParamName);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReadAsBytes_of_an_empty_body_is_an_empty_array(bool useAsync, bool fromStream)
    {
        // A pin: the as-built behaviour.
        var body = fromStream ? ResponseBody.FromStream(Stream.Null) : ResponseBody.FromBytes(Array.Empty<byte>());

        var bytes = useAsync ? await body.ReadAsBytesAsync(Token) : body.ReadAsBytes(Token);

        Assert.NotNull(bytes);
        Assert.Empty(bytes);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    [InlineData(false, 7)]
    [InlineData(true, 1)]
    [InlineData(true, 3)]
    [InlineData(true, 7)]
    public async Task ReadAsBytes_returns_every_byte_of_a_chunked_source(bool useAsync, int perRead)
    {
        var data = Bytes(500);
        var body = ResponseBody.FromStream(new ChunkedReadStream(data, perRead), contentLength: data.Length);

        var bytes = useAsync ? await body.ReadAsBytesAsync(Token) : body.ReadAsBytes(Token);

        Assert.Equal(data, bytes);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("stream")]
    public async Task OpenRead_after_OpenReadAsync_throws_StreamConsumedException(string variant)
    {
        var body = Variant(variant);
        await using var stream = await body.OpenReadAsync(Token);

        Assert.Throws<StreamConsumedException>(() => body.OpenRead(Token));
        await Assert.ThrowsAsync<StreamConsumedException>(() => body.OpenReadAsync(Token));
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("stream")]
    public async Task OpenReadAsync_after_OpenRead_throws_StreamConsumedException(string variant)
    {
        var body = Variant(variant);
        using var stream = body.OpenRead(Token);

        await Assert.ThrowsAsync<StreamConsumedException>(() => body.OpenReadAsync(Token));
        Assert.Throws<StreamConsumedException>(() => body.OpenRead(Token));
    }

    private static ResponseBody Variant(string name) => name == "bytes"
        ? ResponseBody.FromBytes(Bytes(5))
        : ResponseBody.FromStream(new MemoryStream(Bytes(5)));

    [Fact]
    public async Task The_replayable_error_body_opens_a_fresh_view_each_time_in_both_forms()
    {
        // A pin: the buffered error body is replayable by design (HTTP-52).
        var body = ResponseBody.FromReplayableBytes(Bytes(5), null);

        using var first = body.OpenRead(Token);
        await using var second = await body.OpenReadAsync(Token);
        using var third = body.OpenRead(Token);

        Assert.Equal(0, first.ReadByte());
        Assert.Equal(0, second.ReadByte());
        Assert.Equal(0, third.ReadByte());
        Assert.Equal(Bytes(5), await body.ReadAsBytesAsync(Token));
        Assert.Equal(Bytes(5), body.ReadAsBytes(Token));
    }

    [Fact]
    public void OpenRead_of_an_unoverridden_subclass_throws_NotSupportedException_naming_the_subclass()
    {
        using var body = new AsyncOnlyResponseBody();

        var error = Assert.Throws<NotSupportedException>(() => body.OpenRead(Token));

        Assert.Contains(nameof(AsyncOnlyResponseBody), error.Message, StringComparison.Ordinal);
        Assert.Contains("OpenRead", error.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => body.ReadAsBytes(Token));
        Assert.Throws<NotSupportedException>(() => body.ReadAsString(Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsBytes_over_a_declared_length_above_the_cap_throws_BodyTooLargeException_before_reading(bool useAsync)
    {
        using var counting = new DisposeCountingStream(new StrictReadStream(new MemoryStream(Bytes(100))));
        var body = ResponseBody.FromStream(counting, contentLength: 100);

        await Assert.ThrowsAsync<BodyTooLargeException>(
            () => useAsync ? body.ReadAsBytesBoundedAsync(8, Token) : Task.FromResult(body.ReadAsBytesBounded(8, Token)));

        // The reader's finally closed the stream it opened (BODY-16), even though nothing was read. Since 3b the body's
        // own release also reaches the same source, so the count is at least one (BCL streams tolerate a second dispose).
        Assert.True(counting.DisposeCount >= 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsBytes_over_a_declared_length_above_the_cap_reads_nothing(bool useAsync)
    {
        using var source = new StrictReadStream(new MemoryStream(Bytes(100)));
        var body = ResponseBody.FromStream(source, contentLength: 100);

        await Assert.ThrowsAsync<BodyTooLargeException>(
            () => useAsync ? body.ReadAsBytesBoundedAsync(8, Token) : Task.FromResult(body.ReadAsBytesBounded(8, Token)));

        Assert.False(source.AnyRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsBytes_over_an_unknown_length_body_above_the_cap_throws(bool useAsync)
    {
        var body = ResponseBody.FromStream(new MemoryStream(Bytes(100)));

        await Assert.ThrowsAsync<BodyTooLargeException>(
            () => useAsync ? body.ReadAsBytesBoundedAsync(8, Token) : Task.FromResult(body.ReadAsBytesBounded(8, Token)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsBytes_over_an_unknown_length_body_at_exactly_the_cap_succeeds(bool useAsync)
    {
        var data = Bytes(8);
        var body = ResponseBody.FromStream(new MemoryStream(data));

        var bytes = useAsync ? await body.ReadAsBytesBoundedAsync(8, Token) : body.ReadAsBytesBounded(8, Token);

        Assert.Equal(data, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsString_over_a_declared_length_above_the_cap_throws_BodyTooLargeException_before_reading(bool useAsync)
    {
        using var source = new StrictReadStream(new MemoryStream(Bytes(100)));
        var body = ResponseBody.FromStream(source, contentLength: 100);

        await Assert.ThrowsAsync<BodyTooLargeException>(
            () => useAsync ? body.ReadAsStringBoundedAsync(8, Token) : Task.FromResult(body.ReadAsStringBounded(8, Token)));

        Assert.False(source.AnyRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsString_over_an_unknown_length_body_above_the_cap_is_refused(bool useAsync)
    {
        var body = ResponseBody.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(new string('a', 100))));

        await Assert.ThrowsAsync<BodyTooLargeException>(
            () => useAsync ? body.ReadAsStringBoundedAsync(8, Token) : Task.FromResult(body.ReadAsStringBounded(8, Token)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_public_readers_refuse_a_declared_length_above_the_default_cap_without_allocating_it(bool useAsync)
    {
        using var source = new StrictReadStream(new MemoryStream());
        var body = ResponseBody.FromStream(source, contentLength: ResponseBody.DefaultMaxMaterializedBytes + 1);

        await Assert.ThrowsAsync<BodyTooLargeException>(
            () => useAsync ? body.ReadAsBytesAsync(Token) : Task.FromResult(body.ReadAsBytes(Token)));

        Assert.False(source.AnyRead);
    }

    [Fact]
    public async Task The_public_string_readers_refuse_a_declared_length_above_the_default_cap()
    {
        var syncBody = ResponseBody.FromStream(new MemoryStream(), contentLength: ResponseBody.DefaultMaxMaterializedBytes + 1);
        var asyncBody = ResponseBody.FromStream(new MemoryStream(), contentLength: ResponseBody.DefaultMaxMaterializedBytes + 1);

        Assert.Throws<BodyTooLargeException>(() => syncBody.ReadAsString(Token));
        await Assert.ThrowsAsync<BodyTooLargeException>(() => asyncBody.ReadAsStringAsync(Token));
    }

    [Fact]
    public void DefaultMaxMaterializedBytes_is_64_MiB() =>
        Assert.Equal(67_108_864L, ResponseBody.DefaultMaxMaterializedBytes);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsString_decodes_with_the_declared_charset_and_defaults_to_UTF8(bool useAsync)
    {
        var latin1 = ResponseBody.FromBytes(new byte[] { 0x63, 0x61, 0x66, 0xE9 }, MediaType.Parse("text/plain; charset=iso-8859-1"));
        var utf8 = ResponseBody.FromBytes(Encoding.UTF8.GetBytes("café €"));

        Assert.Equal("café", useAsync ? await latin1.ReadAsStringAsync(Token) : latin1.ReadAsString(Token));
        Assert.Equal("café €", useAsync ? await utf8.ReadAsStringAsync(Token) : utf8.ReadAsString(Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsBytes_never_issues_a_zero_count_read(bool useAsync)
    {
        using var source = new StrictReadStream(new ChunkedReadStream(Bytes(300_000), 70_000));
        var body = ResponseBody.FromStream(source);

        var bytes = useAsync ? await body.ReadAsBytesAsync(Token) : body.ReadAsBytes(Token);

        Assert.Equal(300_000, bytes.Length);
        Assert.All(source.RequestedCounts, count => Assert.True(count >= 1));
    }

    [Fact]
    public async Task A_cancelled_token_aborts_ReadAsBytes()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Assert.Throws<OperationCanceledException>(
            () => ResponseBody.FromStream(new MemoryStream(Bytes(10))).ReadAsBytes(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ResponseBody.FromStream(new MemoryStream(Bytes(10))).ReadAsBytesAsync(cts.Token));
    }

    [Fact]
    public async Task ReadAsBytesAsync_disposes_the_stream_it_opened_on_failure()
    {
        // A pin: the existing `await using` scope.
        using var counting = new DisposeCountingStream(new FailingWriteStreamAdapter());
        var body = ResponseBody.FromStream(counting);

        await Assert.ThrowsAsync<IOException>(() => body.ReadAsBytesAsync(Token));

        // At least one: the scope closes the stream, and since 3b the reader's body release reaches the same source too.
        Assert.True(counting.DisposeCount >= 1);
    }

    [Fact]
    public async Task A_request_body_and_a_response_body_agree_on_Latin1_text()
    {
        var request = RequestBody.FromString("café", encoding: Encoding.Latin1);
        using var sink = new MemoryStream();
        await request.WriteToAsync(sink, Token);

        var response = ResponseBody.FromBytes(sink.ToArray(), MediaType.Parse("text/plain; charset=iso-8859-1"));

        Assert.Equal(new byte[] { 0x63, 0x61, 0x66, 0xE9 }, sink.ToArray());
        Assert.Equal("café", await response.ReadAsStringAsync(Token));
    }

    private sealed class AsyncOnlyResponseBody : ResponseBody
    {
        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());
    }

    /// <summary>A readable stream whose reads always fail.</summary>
    private sealed class FailingWriteStreamAdapter : Stream
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

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("read failed");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("read failed");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
