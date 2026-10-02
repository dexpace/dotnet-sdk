// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S8 (BODY-31, RECOV-15, HTTP-52, BODY-30; design §4.2, §5.1): <c>EnsureSuccessAsync</c> maps only
/// 400..599, returning a 304 or an unfollowed 3xx with its body intact; it buffers the error body into a replayable
/// copy capped at 1 MiB; and it disposes the original response inside the buffering scope, whether or not the drain
/// completes. Permanent (roadmap constraint 5); phase 4c's <c>ErrorMappingPolicy</c> and shared
/// <c>ErrorBodyBuffer</c> own the rework.
/// </summary>
[Trait("Category", "Security")]
public sealed class EnsureSuccessErrorMappingTests
{
    // The request every response in this class answers: the scenario is a failed GET of an API resource.
    private static readonly Request s_request = Request.Get("https://api.example.com/v1/items");

    [Theory]
    [InlineData(100)]
    [InlineData(199)]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(304)]
    [InlineData(307)]
    [InlineData(308)]
    [InlineData(399)]
    [InlineData(600)]
    [InlineData(999)]
    public async Task A_non_error_status_is_returned_with_its_body_intact(int code)
    {
        var payload = Encoding.UTF8.GetBytes("kept");
        var body = new TrackingBody(payload);
        using var response = TestResponses.Create(Status.FromCode(code), s_request, body: body);

        await response.EnsureSuccessAsync(TestContext.Current.CancellationToken);

        Assert.False(body.IsDisposed);
        Assert.Equal(payload, await response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(499)]
    [InlineData(500)]
    [InlineData(599)]
    public async Task An_error_status_throws(int code)
    {
        using var response = TestResponses.Create(Status.FromCode(code), s_request);

        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            () => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(code, ex.Status.Code);
    }

    [Fact]
    public async Task The_buffered_error_body_is_replayable()
    {
        var payload = Encoding.UTF8.GetBytes("{\"error\":\"boom\"}");
        using var response = TestResponses.Create(Status.InternalServerError, s_request, body: ResponseBody.FromStream(new MemoryStream(payload), CommonMediaTypes.ApplicationJsonUtf8));

        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            () => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(payload, await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(payload, await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("{\"error\":\"boom\"}", await ex.Response.Body.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_original_response_is_disposed_when_the_error_is_raised()
    {
        var body = new TrackingBody(Encoding.UTF8.GetBytes("error"));
        using var response = TestResponses.Create(Status.ServiceUnavailable, s_request, body: body);

        await Assert.ThrowsAsync<HttpResponseException>(
            () => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.True(body.IsDisposed);
    }

    [Fact]
    public async Task The_original_response_is_disposed_when_the_drain_fails()
    {
        using var stream = new FailingStream();
        var body = new TrackingBody(stream);
        using var response = TestResponses.Create(Status.InternalServerError, s_request, body: body);

        await Assert.ThrowsAsync<IOException>(
            () => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.True(body.IsDisposed);
    }

    [Fact]
    public async Task The_buffered_copy_is_capped_at_one_mebibyte()
    {
        var payload = new byte[Response.MaxBufferedErrorBytes + 4096];
        Array.Fill(payload, (byte)'x');
        using var response = TestResponses.Create(Status.BadGateway, s_request, body: ResponseBody.FromStream(new MemoryStream(payload)));

        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            () => response.EnsureSuccessAsync(TestContext.Current.CancellationToken).AsTask());

        var buffered = await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Response.MaxBufferedErrorBytes, buffered.Length);
        Assert.Equal(Response.MaxBufferedErrorBytes, ex.Response.Body.ContentLength);
    }

    /// <summary>A response body standing in for a transport's: it records whether it was disposed.</summary>
    private sealed class TrackingBody(Stream source) : ResponseBody
    {
        public TrackingBody(byte[] payload)
            : this(new MemoryStream(payload))
        {
        }

        public bool IsDisposed { get; private set; }

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(source);

        public override void Dispose()
        {
            IsDisposed = true;
            source.Dispose();
            base.Dispose();
        }
    }

    /// <summary>A stream whose reads fail, as a connection reset mid-body would.</summary>
    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("connection reset"));

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("connection reset");
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
