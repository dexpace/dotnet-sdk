// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// SEAM-11, PIPE-28, TRANSPORT-5, TRANSPORT-17 (phase 8b, P8b-9; G1, G2; R1, R8): <c>Execute</c> is a genuinely synchronous call.
/// It reaches <c>HttpClient.Send</c>, writes the request body on the calling thread through the body's own <c>WriteTo</c>, and
/// shares the deadline, the failure classification and the guarded adaptation with the asynchronous face. No row here waits on a
/// wall clock: the deadline runs on a <see cref="FakeTimeProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SyncExecuteTests
{
    private static readonly Request s_request = Request.Get("https://example.test/ping");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Implements the synchronous path only: a transport that reached SendAsync would fail loudly.
    private sealed class SendOnlyHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(HttpStatusCode.OK) { Content = new StringContent("sync", System.Text.Encoding.UTF8, "text/plain") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Execute must not reach SendAsync: there is no sync-over-async in the transport.");
    }

    // Implements only SendAsync: HttpClient.Send over it is the documented G1 hazard.
    private sealed class AsyncOnlyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    // A DelegatingHandler that overrides only SendAsync: the sync face skips it silently (G2).
    private sealed class AsyncOnlyDelegatingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private int _ran;

        public int Ran => Volatile.Read(ref _ran);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _ran);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class SyncAndAsyncHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    // Waits on the token it was given, on the calling thread, and says when it has started.
    private sealed class BlockingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Execute must not reach SendAsync.");
    }

    [Fact]
    public void Execute_runs_through_a_handler_that_implements_only_Send()
    {
        using var handler = new SendOnlyHandler();
        using var transport = new SystemNetHttpClient(handler);

        using var response = transport.Execute(s_request, RequestOptions.Empty, Ct);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal("sync", response.Body.ReadAsString(Ct));
    }

    [Fact]
    public void Execute_over_a_borrowed_client_whose_handler_implements_only_Send_works_too()
    {
        using var handler = new SendOnlyHandler();
        using var borrowed = new SystemHttpClient(handler);
        using var transport = new SystemNetHttpClient(borrowed);

        using var response = transport.Execute(s_request, RequestOptions.Empty, Ct);

        Assert.Equal(200, response.Status.Code);
    }

    [Fact]
    public void G1_a_primary_handler_with_no_Send_fails_loudly_with_NotSupportedException_not_an_SdkException()
    {
        using var handler = new AsyncOnlyHandler();
        using var transport = new SystemNetHttpClient(handler);

        var failure = Assert.Throws<NotSupportedException>(() => transport.Execute(s_request, RequestOptions.Empty, Ct));

        Assert.IsNotAssignableFrom<SdkException>(failure);
        Assert.Contains("Send", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task G2_a_delegating_handler_with_no_Send_is_skipped_on_the_sync_face_and_honoured_on_the_async_face()
    {
        using var primary = new SyncAndAsyncHandler();
        using var delegating = new AsyncOnlyDelegatingHandler(primary);
        using var transport = new SystemNetHttpClient(delegating);

        using (var sync = transport.Execute(s_request, RequestOptions.Empty, Ct))
        {
            Assert.Equal(200, sync.Status.Code);
        }

        Assert.Equal(0, delegating.Ran);

        await using var async = await transport.ExecuteAsync(s_request, RequestOptions.Empty, Ct);
        Assert.Equal(200, async.Status.Code);
        Assert.Equal(1, delegating.Ran);
    }

    [Fact]
    public async Task The_sync_face_applies_the_same_per_call_deadline()
    {
        var clock = new FakeTimeProvider();
        using var handler = new BlockingHandler();
        using var transport = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { TimeProvider = clock });

        var call = Task.Run(() => transport.Execute(s_request, new RequestOptions { Timeout = TimeSpan.FromMilliseconds(100) }, Ct), Ct);
        await handler.Started.WaitAsync(Ct);
        clock.Advance(TimeSpan.FromMilliseconds(100));

        var failure = await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call);
        Assert.True(failure.IsRetryable);
    }

    [Fact]
    public async Task The_sync_face_classifies_the_callers_cancellation_a_request_failure_and_an_internal_cancel_like_the_async_face()
    {
        using var handler = new BlockingHandler();
        using var borrowed = new SystemHttpClient(handler);
        using var transport = new SystemNetHttpClient(borrowed);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        // Internal cancel: CancelPendingRequests during a blocked Send.
        var internalCancel = Task.Run(() => transport.Execute(s_request, RequestOptions.Empty, Ct), Ct);
        await handler.Started.WaitAsync(Ct);
        borrowed.CancelPendingRequests();
        var terminal = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => internalCancel);
        Assert.IsNotAssignableFrom<SdkException>(terminal);
        Assert.Null(terminal.InnerException);
        Assert.Single(ExceptionTrail.GetSuppressed(terminal));

        // The caller's own cancellation carries the caller's token.
        using var second = new BlockingHandler();
        using var transport2 = new SystemNetHttpClient(second);
        var cancelled = Task.Run(() => transport2.Execute(s_request, RequestOptions.Empty, caller.Token), Ct);
        await second.Started.WaitAsync(Ct);
        await caller.CancelAsync();
        var byCaller = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(caller.Token, byCaller.CancellationToken);
    }

    [Fact]
    public void A_request_failure_on_the_sync_face_is_a_retryable_transport_failure()
    {
        using var handler = new ThrowingSendHandler(new HttpRequestException("refused", new System.Net.Sockets.SocketException()));
        using var transport = new SystemNetHttpClient(handler);

        var failure = Assert.Throws<ServiceRequestException>(() => transport.Execute(s_request, RequestOptions.Empty, Ct));

        Assert.True(failure.IsRetryable);
        Assert.IsType<HttpRequestException>(failure.InnerException);
    }

    private sealed class ThrowingSendHandler(Exception toThrow) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => throw toThrow;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw toThrow;
    }

    // A handler that follows a "redirect" itself: the final request URI it leaves on the response differs from the one sent.
    private sealed class RedirectedHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var built = response();
            built.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://elsewhere.test/other");
            return built;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class DisposeThrowingResponse(Exception toThrow) : HttpResponseMessage(HttpStatusCode.Found)
    {
        public int Disposals { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
                throw toThrow;
            }

            base.Dispose(disposing);
        }
    }

    [Fact]
    public void The_sync_face_detects_a_redirect_the_native_client_followed_and_disposes_the_response()
    {
        DisposeCountingResponse? seen = null;
        using var handler = new RedirectedHandler(() => seen = new DisposeCountingResponse());
        using var transport = new SystemNetHttpClient(handler);

        var failure = Assert.Throws<SdkException>(() => transport.Execute(s_request, RequestOptions.Empty, Ct));

        Assert.Contains("AllowAutoRedirect", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, seen!.Disposals);
    }

    private sealed class DisposeCountingResponse : HttpResponseMessage
    {
        public int Disposals { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task A_response_whose_dispose_throws_never_replaces_the_adaptation_failure_on_either_face()
    {
        var secondary = new IOException("dispose failed");
        using var handler = new RedirectedHandler(() => new DisposeThrowingResponse(secondary));
        using var transport = new SystemNetHttpClient(handler);

        var sync = Assert.Throws<SdkException>(() => transport.Execute(s_request, RequestOptions.Empty, Ct));
        var async = await Assert.ThrowsAsync<SdkException>(() => transport.ExecuteAsync(s_request, RequestOptions.Empty, Ct));

        foreach (var primary in new[] { sync, async })
        {
            Assert.Contains("AllowAutoRedirect", primary.Message, StringComparison.Ordinal);
            Assert.Same(secondary, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
        }
    }

    // A single-use body that records the thread and the number of times its synchronous write ran.
    private sealed class ThreadRecordingBody : RequestBody
    {
        private int _writes;

        public int Writes => Volatile.Read(ref _writes);

        public int WriterThread { get; private set; }

        public override MediaType? ContentType => null;

        public override long ContentLength => -1;

        public override bool IsReplayable => false;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The sync face must not write the body asynchronously.");

        public override void WriteTo(Stream destination, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _writes);
            WriterThread = Environment.CurrentManagedThreadId;
            destination.Write("payload"u8);
        }
    }

    // Drains the request content the way the native handler does on the sync path: through HttpContent.CopyTo.
    private sealed class DrainingHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var sink = new MemoryStream();
            request.Content?.CopyTo(sink, context: null, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(sink.ToArray()) };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Execute must not reach SendAsync.");
    }

    [Fact]
    public void The_request_body_is_written_on_the_calling_thread_exactly_once()
    {
        var body = new ThreadRecordingBody();
        using var handler = new DrainingHandler();
        using var transport = new SystemNetHttpClient(handler);

        using var response = transport.Execute(Request.Post("https://example.test/upload", body), RequestOptions.Empty, Ct);

        Assert.Equal(1, body.Writes);
        Assert.Equal(Environment.CurrentManagedThreadId, body.WriterThread);
        Assert.Equal("payload", response.Body.ReadAsString(Ct));
    }

    // A third-party body that implements the async write only: the base WriteTo throws NotSupportedException (P3a-5).
    private sealed class AsyncOnlyBody : RequestBody
    {
        public override MediaType? ContentType => null;

        public override long ContentLength => -1;

        public override bool IsReplayable => false;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void A_body_with_no_synchronous_write_fails_the_sync_send_with_NotSupportedException()
    {
        using var handler = new DrainingHandler();
        using var transport = new SystemNetHttpClient(handler);

        var failure = Assert.Throws<NotSupportedException>(() => transport.Execute(Request.Post("https://example.test/upload", new AsyncOnlyBody()), RequestOptions.Empty, Ct));

        Assert.Contains("WriteTo", failure.Message, StringComparison.Ordinal);
    }
}
