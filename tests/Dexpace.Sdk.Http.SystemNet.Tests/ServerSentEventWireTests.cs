// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// Server-sent events over a real socket (SSE-2, SSE-31, SSE-32, SSE-39, OBS-37, P7b-23): the loopback server streams a
/// chunked reply that the test gates, the reference transport delivers it live, and the facade reads it through the
/// default pipeline. A chunk reaches the consumer before the server has written the next byte, so none of this is
/// buffered anywhere on the way. Needs <see cref="LoopbackResponse.Streamed"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ServerSentEventWireTests
{
    private static readonly TimeSpan s_hangGuard = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<KeyValuePair<string, string>> EventStreamHeaders => [new("Content-Type", "text/event-stream")];

    private static Request EventRequest(Uri url) =>
        Request.Create(Method.Get, url.AbsoluteUri, new Headers.Builder().Add("Accept", "text/event-stream").Build());

    // The reference transport behind a counter that sees each body released exactly once, under the default pipeline.
    private static (HttpPipeline Pipeline, CountingTransport Counter) PipelineOverLoopback()
    {
        var counter = new CountingTransport();
        return (DexpacePipeline.CreateDefault(counter), counter);
    }

    private static async IAsyncEnumerable<byte[]> Chunks(
        IReadOnlyList<(string Text, Task? Gate)> script,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var (text, gate) in script)
        {
            if (gate is not null)
            {
                await gate.WaitAsync(cancellationToken);
            }

            yield return Encoding.UTF8.GetBytes(text);
        }
    }

    [Fact]
    public async Task A_CR_terminated_event_is_delivered_before_the_server_sends_more()
    {
        // The defect design 7.2 found in StreamReader: a CR-terminated line was held for the next chunk, so an event
        // ended by CR CR was not delivered until the server wrote more. Here the server is gated on the second chunk.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = LoopbackServer.Start(LoopbackResponse.Streamed(
            EventStreamHeaders,
            Chunks([("data: a\r\r", null), ("data: b\r\r", gate.Task)], Ct)));
        var (pipeline, counter) = PipelineOverLoopback();
        await using var transport = counter;

        var stream = ServerSentEventStream.FromResponse(await pipeline.SendAsync(EventRequest(server.Url("/events")), Ct));
        await using var enumerator = stream.GetAsyncEnumerator(Ct);

        Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(s_hangGuard, Ct));
        Assert.Equal(["a"], enumerator.Current.Data);
        Assert.False(gate.Task.IsCompleted);

        gate.SetResult();

        Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(s_hangGuard, Ct));
        Assert.Equal(["b"], enumerator.Current.Data);
        Assert.False(await enumerator.MoveNextAsync().AsTask().WaitAsync(s_hangGuard, Ct));
        Assert.Equal(1, counter.BodyDisposals);
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_close_from_another_thread_while_the_transport_read_is_parked_surfaces_IOException_with_one_release()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = LoopbackServer.Start(LoopbackResponse.Streamed(
            EventStreamHeaders,
            Chunks([("data: first\n\n", null), ("data: never\n\n", gate.Task)], Ct)));
        var (pipeline, counter) = PipelineOverLoopback();
        await using var transport = counter;
        var stream = ServerSentEventStream.FromResponse(await pipeline.SendAsync(EventRequest(server.Url("/events")), Ct));
        var delivered = new List<string>();
        var firstEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = Task.Run(
            async () =>
            {
                await foreach (var ev in stream.WithCancellation(Ct))
                {
                    delivered.Add(ev.Data[0]);
                    // Armed before the consumer pulls again, so the next transport read it starts is the parked one.
                    counter.ArmReadSignal();
                    firstEvent.TrySetResult();
                }
            },
            Ct);
        await firstEvent.Task.WaitAsync(s_hangGuard, Ct);
        await counter.ReadStartedAfterArming.WaitAsync(s_hangGuard, Ct);

        await stream.DisposeAsync();

        var thrown = await Assert.ThrowsAsync<IOException>(() => consumer.WaitAsync(s_hangGuard, Ct));
        Assert.NotNull(thrown.InnerException);
        Assert.Equal(["first"], delivered);
        Assert.Equal(1, counter.BodyDisposals);
        gate.SetResult();
    }

    [Fact]
    public async Task A_204_fails_loudly_and_releases_the_response()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Status(204, "No Content"));
        var (pipeline, counter) = PipelineOverLoopback();
        await using var transport = counter;
        var response = await pipeline.SendAsync(EventRequest(server.Url("/events")), Ct);

        var ex = Assert.Throws<ArgumentException>(() => ServerSentEventStream.FromResponse(response));

        Assert.Equal("response", ex.ParamName);
        Assert.Equal(1, counter.BodyDisposals);
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task The_events_arrive_in_order_over_a_chunked_stream()
    {
        // An event split across chunk boundaries, then CRLF framing, then a comment-only keep-alive.
        await using var server = LoopbackServer.Start(LoopbackResponse.Streamed(
            EventStreamHeaders,
            Chunks(
                [
                    ("id: 1\nda", null),
                    ("ta: hel", null),
                    ("lo\n\n: ke", null),
                    ("ep-alive\r\n\r\nevent: tick\r\ndata: 2\r\n\r\n", null),
                ],
                Ct)));
        var (pipeline, counter) = PipelineOverLoopback();
        await using var transport = counter;
        await using var stream = ServerSentEventStream.FromResponse(await pipeline.SendAsync(EventRequest(server.Url("/events")), Ct));

        var events = new List<ServerSentEvent>();
        await foreach (var ev in stream.WithCancellation(Ct))
        {
            events.Add(ev);
        }

        Assert.Equal(3, events.Count);
        Assert.Equal("1", events[0].Id);
        Assert.Equal(["hello"], events[0].Data);
        Assert.Equal("keep-alive", events[1].Comment);
        Assert.Equal("tick", events[2].Event);
        Assert.Equal(["2"], events[2].Data);
        Assert.Equal(1, counter.BodyDisposals);
    }

    [Fact]
    public async Task A_blocking_Map_over_HttpPipeline_Send_round_trips()
    {
        // The synchronous path end to end: HttpPipeline.Send, then OpenRead and Stream.Read on the transport's body.
        // The Accept header is the caller's to set (SSE-38, P7b-18); the server records what arrived.
        await using var server = LoopbackServer.Start(LoopbackResponse.Streamed(
            EventStreamHeaders,
            Chunks([("data: one\n\ndata: two\n\n", null), ("data: [DONE]\n\ndata: never\n\n", null)], Ct)));
        var (pipeline, counter) = PipelineOverLoopback();
        await using var transport = counter;

        var response = await Task.Run(() => pipeline.Send(EventRequest(server.Url("/events")), Ct), Ct);
        var models = await Task.Run(
            () =>
            {
                using var stream = ServerSentEventStream.FromResponse(response);
                return stream.Map<string>((_, data) => data == "[DONE]" ? SseMapResult.Done : SseMapResult.Value(data)).ToList();
            },
            Ct);

        Assert.Equal(["one", "two"], models);
        Assert.Equal("text/event-stream", Assert.Single(server.Requests).Header("Accept"));
        Assert.Equal(1, counter.BodyDisposals);
    }

    // Decorates the real transport so each response body is released through a counter, the observable the wire tests
    // assert on; every other behaviour is the reference transport's.
    private sealed class CountingTransport : IAsyncHttpClient, IHttpClient
    {
        // The SDK-owned construction, with the proxy pinned off so an environment proxy cannot reroute the loopback exchange.
        private readonly SystemNetHttpClient _inner = new(handler => handler.UseProxy = false);
        private int _disposals;
        private int _armed;
        private TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int BodyDisposals => Volatile.Read(ref _disposals);

        /// <summary>Completes when a transport read begins after <see cref="ArmReadSignal"/>: the consumer is parked in it.</summary>
        public Task ReadStartedAfterArming => Volatile.Read(ref _readStarted).Task;

        /// <summary>From now on, the next transport read to begin completes <see cref="ReadStartedAfterArming"/>.</summary>
        public void ArmReadSignal() => Volatile.Write(ref _armed, 1);

        private ReadSignalingStream Observe(Stream stream) => new ReadSignalingStream(stream, () =>
        {
            if (Volatile.Read(ref _armed) == 1)
            {
                Volatile.Read(ref _readStarted).TrySetResult();
            }
        });

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            var response = await _inner.ExecuteAsync(request, options, cancellationToken);
            return response.WithBody(new CountingBody(response.Body, Observe, () => Interlocked.Increment(ref _disposals)));
        }

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            var response = _inner.Execute(request, options, cancellationToken);
            return response.WithBody(new CountingBody(response.Body, Observe, () => Interlocked.Increment(ref _disposals)));
        }

        public void Dispose() => _inner.Dispose();

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class CountingBody(ResponseBody inner, Func<Stream, Stream> observe, Action onDispose) : ResponseBody
    {
        public override MediaType? ContentType => inner.ContentType;

        public override long ContentLength => inner.ContentLength;

        public override async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            observe(await inner.OpenReadAsync(cancellationToken));

        public override Stream OpenRead(CancellationToken cancellationToken = default) => observe(inner.OpenRead(cancellationToken));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                onDispose();
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    // Forwards to the transport's stream and reports each read as it begins.
    private sealed class ReadSignalingStream(Stream inner, Action onReadStarted) : Stream
    {
        public override bool CanRead => inner.CanRead;

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
            onReadStarted();
            return inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            onReadStarted();
            return inner.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            onReadStarted();
            return inner.ReadAsync(buffer, cancellationToken);
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
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
