// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Suite;

/// <summary>The one send primitive: each face goes through its own member, and a null anywhere is a conformance failure.</summary>
[Trait("Category", "Unit")]
public sealed class FaceTransportTests
{
    private static readonly Request s_request = Request.Get("http://127.0.0.1:1/");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_async_face_returns_the_transports_response_and_disposes_the_transport()
    {
        var transport = new FakeTransport();
        await using var face = new AsyncFaceTransport(transport);

        using var response = await face.SendAsync(s_request, Ct);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(1, transport.AsyncCalls);
        Assert.Equal(TransportFace.Async, face.Face);
        await face.DisposeAsync();
        Assert.Equal(1, transport.Disposed);
    }

    [Fact]
    public async Task The_async_face_reports_a_null_task_and_a_null_response_as_conformance_failures()
    {
        var nullTask = new FakeTransport { OnAsync = (_, _) => null! };
        var nullResponse = new FakeTransport { OnAsync = (_, _) => Task.FromResult<Response>(null!) };
        await using var taskFace = new AsyncFaceTransport(nullTask);
        await using var responseFace = new AsyncFaceTransport(nullResponse);

        await Assert.ThrowsAsync<ConformanceException>(() => taskFace.SendAsync(s_request, Ct));
        await Assert.ThrowsAsync<ConformanceException>(() => responseFace.SendAsync(s_request, Ct));
    }

    [Fact]
    public async Task A_transport_failure_passes_through_the_face_unchanged()
    {
        var boom = new InvalidOperationException("boom");
        await using var face = new AsyncFaceTransport(new FakeTransport { OnAsync = (_, _) => Task.FromException<Response>(boom) });

        Assert.Same(boom, await Assert.ThrowsAsync<InvalidOperationException>(() => face.SendAsync(s_request, Ct)));
    }

    [Fact]
    public async Task The_blocking_face_calls_Execute_off_the_thread_pool_and_disposes_the_transport()
    {
        var transport = new FakeTransport();
        await using var face = new BlockingFaceTransport(transport);

        using var response = await face.SendAsync(s_request, Ct);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(1, transport.BlockingCalls);
        Assert.False(transport.LastBlockingCallOnPoolThread);
        Assert.Equal(TransportFace.Blocking, face.Face);
        await face.DisposeAsync();
        Assert.Equal(1, transport.Disposed);
    }

    [Fact]
    public async Task The_blocking_face_reports_a_null_response_as_a_conformance_failure()
    {
        await using var face = new BlockingFaceTransport(new FakeTransport { OnBlocking = (_, _) => null! });

        await Assert.ThrowsAsync<ConformanceException>(() => face.SendAsync(s_request, Ct));
    }

    [Fact]
    public async Task A_token_that_is_already_signalled_still_reaches_a_blocking_transport()
    {
        // Cancellation of a blocking call is cooperative: the transport must see the token, so the face must not drop the
        // call before it starts (TRANSPORT-3's own face).
        CancellationToken seen = default;
        var transport = new FakeTransport { OnBlocking = (request, token) => { seen = token; return FakeTransport.Ok(request); } };
        await using var face = new BlockingFaceTransport(transport);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var response = await face.SendAsync(s_request, cts.Token);

        Assert.True(seen.IsCancellationRequested);
        Assert.Equal(1, transport.BlockingCalls);
    }

    [Fact]
    public async Task The_options_overload_passes_the_options_through()
    {
        RequestOptions? seen = null;
        var transport = new CapturingTransport(options => seen = options);
        await using var face = new AsyncFaceTransport(transport);
        var options = new RequestOptions { Timeout = TimeSpan.FromSeconds(3) };

        using var response = await face.SendAsync(s_request, options, Ct);

        Assert.Same(options, seen);
    }

    private sealed class CapturingTransport(Action<RequestOptions> capture) : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            capture(options);
            return Task.FromResult(FakeTransport.Ok(request));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
