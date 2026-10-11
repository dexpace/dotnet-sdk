// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pagination;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// SEAM-11, PIPE-28, TRANSPORT-5, TRANSPORT-17 (phase 8b, P8b-9) at the wire: the synchronous face over a real socket. It must
/// deliver what the asynchronous face delivers, write a single-use body once, and carry the per-call timeout through the
/// blocking pager (phase 7c's inherit-only hand-off). The synchronous <c>traceparent</c> row (5c's hand-off) is in
/// <c>TracePropagationWireTests</c>, which owns the process-wide listeners.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SyncExecuteWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // A non-seekable source that counts what is read from it.
    private sealed class OneShotStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content);

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

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }

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
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task A_sync_get_delivers_what_the_async_face_delivers()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Status(200, "OK", [new("X-Reply", "hello"), new("Content-Type", "text/plain; charset=utf-8")], "body", keepAlive: true));
        await using var transport = new SystemNetHttpClient();
        var request = Request.Get(server.Url("/get").AbsoluteUri).WithHeader("X-Caller", "yes");

        using var sync = await Task.Run(() => transport.Execute(request, RequestOptions.Empty, Ct), Ct);
        await using var async = await transport.ExecuteAsync(request, RequestOptions.Empty, Ct);

        Assert.Equal(async.Status, sync.Status);
        Assert.Equal(async.Headers.Get("X-Reply"), sync.Headers.Get("X-Reply"));
        Assert.Equal(await async.Body.ReadAsStringAsync(Ct), sync.Body.ReadAsString(Ct));
        Assert.Equal(["yes", "yes"], server.Requests.Select(r => r.Header("X-Caller")));
        Assert.Equal(server.Requests[0].RequestLine, server.Requests[1].RequestLine);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task A_sync_post_of_a_single_use_stream_body_writes_its_source_once(int declaredLength)
    {
        var payload = "single-use payload"u8.ToArray();
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        await using var transport = new SystemNetHttpClient();
        using var source = new OneShotStream(payload);
        var length = declaredLength < 0 ? -1 : payload.Length;
        var request = Request.Post(server.Url("/upload").AbsoluteUri, RequestBody.FromStream(source, contentLength: length));

        using var response = await Task.Run(() => transport.Execute(request, RequestOptions.Empty, Ct), Ct);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(payload.Length, source.BytesRead);
        var recorded = Assert.Single(server.Requests);
        Assert.Equal(payload, recorded.Body.ToArray());
    }

    [Fact]
    public async Task A_body_with_no_synchronous_write_fails_a_real_sync_send_with_NotSupportedException()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        await using var transport = new SystemNetHttpClient();

        var failure = await Assert.ThrowsAsync<NotSupportedException>(() => Task.Run(
            () => transport.Execute(Request.Post(server.Url("/upload").AbsoluteUri, new AsyncOnlyBody()), RequestOptions.Empty, Ct),
            Ct));

        Assert.IsNotAssignableFrom<SdkException>(failure);
        Assert.Contains("WriteTo", failure.Message, StringComparison.Ordinal);
    }

    private sealed class AsyncOnlyBody : RequestBody
    {
        public override Dexpace.Sdk.Core.Http.Common.MediaType? ContentType => null;

        public override long ContentLength => -1;

        public override bool IsReplayable => false;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task A_real_sync_call_to_a_server_that_never_answers_is_cut_by_the_per_call_timeout()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Hang());
        await using var transport = new SystemNetHttpClient();

        var failure = await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => Task.Run(
            () => transport.Execute(Request.Get(server.Url("/hang").AbsoluteUri), new RequestOptions { Timeout = TimeSpan.FromMilliseconds(300) }, Ct),
            Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct));

        Assert.True(failure.IsRetryable);
    }

    [Fact]
    public async Task The_blocking_pager_walks_two_pages_through_Execute_with_a_per_call_timeout_that_reaches_every_page()
    {
        await using var server = LoopbackServer.Start(request => request.Target switch
        {
            "/items" => PaginationWireTests.Page("1|a"),
            "/items?cursor=a" => PaginationWireTests.Page("2|"),
            _ => LoopbackResponse.Status(404, "Not Found"),
        });
        using var client = PaginationWireTests.DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        var pageable = Pageable.CreateBlocking<PaginationWireTests.WirePage, int>(
            transport,
            Request.Get(server.Url("/items").ToString()),
            new PaginationWireTests.WireSerde(),
            PaginationStrategies.Cursor<PaginationWireTests.WirePage, int>(p => p.Items, p => p.Next),
            options: new RequestOptions { Timeout = TimeSpan.FromSeconds(20) },
            maxPages: 10,
            cancellationToken: Ct);

        var items = await Task.Run(() => pageable.ToList(), Ct);

        Assert.Equal([1, 2], items);
        Assert.Equal(["/items", "/items?cursor=a"], server.Requests.Select(r => r.Target));
    }

    [Fact]
    public async Task The_blocking_pager_fails_a_page_that_never_answers_with_the_per_call_timeout()
    {
        await using var server = LoopbackServer.Start(request => request.Target == "/items"
            ? PaginationWireTests.Page("1|a")
            : LoopbackResponse.Hang());
        using var client = PaginationWireTests.DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        var pageable = Pageable.CreateBlocking<PaginationWireTests.WirePage, int>(
            transport,
            Request.Get(server.Url("/items").ToString()),
            new PaginationWireTests.WireSerde(),
            PaginationStrategies.Cursor<PaginationWireTests.WirePage, int>(p => p.Items, p => p.Next),
            options: new RequestOptions { Timeout = TimeSpan.FromMilliseconds(300) },
            maxPages: 10,
            cancellationToken: Ct);

        await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => Task.Run(() => pageable.ToList(), Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct));
    }
}
