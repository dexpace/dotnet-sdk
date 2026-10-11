// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-4, -8, -20, XCUT-1, XCUT-2 (phase 8b, P8b-7, G8): what the classification of <c>SendFailures</c> means once the SDK's
/// own retry sits on top of the transport. A timeout and a request failure are retried to the limit; an internal cancellation is
/// terminal after one send, because the retry classifier calls any <see cref="IOException"/> in a cause chain retryable and the
/// transport therefore keeps the native one off <see cref="Exception.InnerException"/>. Every row runs on both faces.
/// <c>Dexpace.Sdk.Core.Tests</c> may not reference a transport, so these live here.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FailureClassificationTests
{
    private const int MaxRetries = 3;
    private static readonly Request s_request = Request.Get("https://example.test/ping");

    // Throws the same native shape on every send, and counts the sends.
    private sealed class ThrowingHandler(Func<CancellationToken, Exception> shape) : HttpMessageHandler
    {
        private int _sends;

        public int Sends => Volatile.Read(ref _sends);

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sends);
            throw shape(cancellationToken);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sends);
            throw shape(cancellationToken);
        }
    }

    // Blocks (asynchronously on the async face, on the calling thread on the sync face) until the token it was given is signalled.
    private sealed class BlockingHandler : HttpMessageHandler
    {
        private int _sends;
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Sends => Volatile.Read(ref _sends);

        public Task Started => _started.Task;

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sends);
            _started.TrySetResult();
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sends);
            _started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static HttpPipeline Pipeline(IAsyncHttpClient transport) =>
        new PipelineBuilder()
            .AddStandardResilience(new InstantTimeProvider())
            .Build(transport, new DexpaceClientOptions { Retry = new RetryOptions { MaxRetryAttempts = MaxRetries } });

    // Drives the pipeline over the transport on the face the row names: SendAsync reaches ExecuteAsync, Send reaches Execute (PIPE-28).
    private static async Task<Exception> FailureOfAsync(string face, Func<SystemNetHttpClient> transport, CancellationToken cancellationToken)
    {
        await using var t = transport();
        var pipeline = Pipeline(t);
        if (face == "async")
        {
            return await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                using var response = await pipeline.SendAsync(s_request, cancellationToken);
            });
        }

        return await Assert.ThrowsAnyAsync<Exception>(() => Task.Run(
            () =>
            {
                using var response = pipeline.Send(s_request, cancellationToken);
            },
            cancellationToken));
    }

    private static SystemNetHttpClient OverHandler(HttpMessageHandler handler, TimeProvider? clock = null, TimeSpan? timeout = null) =>
        new(handler, new SystemNetHttpClientOptions { TimeProvider = clock ?? TimeProvider.System, Timeout = timeout ?? TimeSpan.FromSeconds(100) });

    public static TheoryData<string> Faces() => ["async", "sync"];

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task Row_4_an_internal_cancel_is_terminal_after_one_send(string face)
    {
        using var handler = new ThrowingHandler(_ => new TaskCanceledException("cancelled", new IOException("The request was canceled.")));

        var failure = await FailureOfAsync(face, () => OverHandler(handler), TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.Sends);
        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.NotNull(cancelled);
        Assert.Null(cancelled.InnerException);
        Assert.IsNotAssignableFrom<SdkException>(failure);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task Row_3_a_native_clients_own_timeout_is_retried_to_the_limit(string face)
    {
        using var handler = new ThrowingHandler(_ => new TaskCanceledException("timed out", new TimeoutException("The operation was canceled.")));

        var failure = await FailureOfAsync(face, () => OverHandler(handler), TestContext.Current.CancellationToken);

        Assert.Equal(MaxRetries + 1, handler.Sends);
        Assert.IsType<ServiceRequestTimeoutException>(failure);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task Row_5_a_request_failure_is_retried_to_the_limit(string face)
    {
        using var handler = new ThrowingHandler(_ => new HttpRequestException("refused", new System.Net.Sockets.SocketException()));

        var failure = await FailureOfAsync(face, () => OverHandler(handler), TestContext.Current.CancellationToken);

        Assert.Equal(MaxRetries + 1, handler.Sends);
        Assert.IsType<ServiceRequestException>(failure);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task Row_2_the_transports_own_deadline_is_retried_to_the_limit(string face)
    {
        using var handler = new BlockingHandler();

        // A clock whose timers all fire after a millisecond: the deadline elapses at once, as a real one would after its time.
        var failure = await FailureOfAsync(face, () => OverHandler(handler, new InstantTimeProvider(), TimeSpan.FromMilliseconds(50)), TestContext.Current.CancellationToken);

        Assert.Equal(MaxRetries + 1, handler.Sends);
        Assert.IsType<ServiceRequestTimeoutException>(failure);
    }

    [Fact]
    public async Task CancelPendingRequests_on_a_borrowed_client_during_a_call_is_terminal_and_not_retried()
    {
        using var handler = new BlockingHandler();
        using var borrowed = new SystemHttpClient(handler);
        await using var transport = new SystemNetHttpClient(borrowed);

        var call = Task.Run(
            async () =>
            {
                using var response = await Pipeline(transport).SendAsync(s_request, TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);
        borrowed.CancelPendingRequests();

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(1, handler.Sends);
        Assert.IsNotAssignableFrom<SdkException>(failure);
        Assert.Null(failure.InnerException);
    }

    [Fact]
    public async Task The_caller_cancelling_is_a_cancellation_and_not_retried()
    {
        using var handler = new BlockingHandler();
        await using var transport = OverHandler(handler);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var call = Task.Run(
            async () =>
            {
                using var response = await Pipeline(transport).SendAsync(s_request, caller.Token);
            },
            TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TestContext.Current.CancellationToken);
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(1, handler.Sends);
    }
}
