// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http;

[Trait("Category", "Unit")]
public sealed class EnsureSuccessTests
{
    // -------------------------------------------------------------------------
    // Success cases — should NOT throw
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(299)]
    public async Task EnsureSuccessAsync_SuccessStatusCode_DoesNotThrow(int statusCode)
    {
        using var response = new Response(Status.FromCode(statusCode));
        await response.EnsureSuccessAsync(TestContext.Current.CancellationToken);   // must not throw
    }

    // -------------------------------------------------------------------------
    // Error cases — must throw HttpResponseException
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task EnsureSuccessAsync_ErrorStatusCode_ThrowsHttpResponseException(int statusCode)
    {
        using var response = new Response(Status.FromCode(statusCode));
        await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());
    }

    // -------------------------------------------------------------------------
    // Exception carries status
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureSuccessAsync_ExceptionCarriesCorrectStatus()
    {
        using var response = new Response(Status.FromCode(404));
        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(Status.FromCode(404), ex.Status);
    }

    // -------------------------------------------------------------------------
    // Body is buffered in the thrown exception (replayable)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureSuccessAsync_ErrorBodyIsBufferedInException()
    {
        var bodyBytes = Encoding.UTF8.GetBytes("{\"code\":\"not_found\",\"message\":\"Resource not found\"}");
        var body = ResponseBody.FromBytes(bodyBytes, MediaType.Of("application", "json"));
        using var response = new Response(Status.FromCode(404), body: body);

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        // The body on the exception must be readable (replayable)
        var readBytes = await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(bodyBytes, readBytes);
    }

    [Fact]
    public async Task EnsureSuccessAsync_ErrorBodyReadableTwice_AfterBuffering()
    {
        // Verify that the buffered body on the exception can be opened more than once
        // by reading it twice in sequence.
        var bodyBytes = Encoding.UTF8.GetBytes("error payload");
        var body = ResponseBody.FromBytes(bodyBytes);
        using var response = new Response(Status.FromCode(500), body: body);

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        var first = await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken);
        var second = await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken);

        // The buffered copy on the exception is replayable (HTTP-52, BODY-30): the same instance reads twice.
        Assert.Equal(bodyBytes, first);
        Assert.Equal(bodyBytes, second);
    }

    // -------------------------------------------------------------------------
    // Content-type is preserved on the buffered body
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureSuccessAsync_PreservesContentTypeOnBufferedBody()
    {
        var contentType = MediaType.Of("application", "json");
        var body = ResponseBody.FromBytes(Encoding.UTF8.GetBytes("{}"), contentType);
        using var response = new Response(Status.FromCode(422), body: body);

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(contentType, ex.Response.Body.ContentType);
    }

    // -------------------------------------------------------------------------
    // Status, Headers, and Protocol are carried through
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureSuccessAsync_ExceptionResponseCarriesOriginalHeadersAndProtocol()
    {
        var headers = Headers.Empty.Set("X-Request-Id", "abc-123");
        using var response = new Response(Status.FromCode(503), headers: headers, protocol: Protocol.Http2);

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("abc-123", ex.Response.Headers.Get("X-Request-Id"));
        Assert.Equal(Protocol.Http2, ex.Response.Protocol);
    }

    // -------------------------------------------------------------------------
    // Cancellation propagates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureSuccessAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var body = ResponseBody.FromStream(new NeverEndingStream());
        using var response = new Response(Status.FromCode(500), body: body);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => response.EnsureSuccessAsync(cts.Token).AsTask());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>A stream that never returns data, used to test cancellation.</summary>
    private sealed class NeverEndingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
