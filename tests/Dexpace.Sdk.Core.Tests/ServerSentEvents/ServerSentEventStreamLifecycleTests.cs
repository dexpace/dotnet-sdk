// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The facade's ownership and release rules: SSE-23 to SSE-28, SSE-30, SSE-32, SSE-39 (P7b-9 to P7b-14).</summary>
[Trait("Category", "Unit")]
public sealed class ServerSentEventStreamLifecycleTests : IDisposable
{
    private readonly ActivitySource _source = new("Dexpace.Sdk.Core.Tests.ServerSentEventStream");
    private readonly ActivityListener _listener;

    public ServerSentEventStreamLifecycleTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == _source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> BodylessCases =>
    [
        "status-204",
        "status-205",
        "status-304",
        "head-request",
        "content-length-zero",
        "no-body",
    ];

    private static (Response Response, SseBody? Body) BodylessResponse(string kind)
    {
        if (kind == "no-body")
        {
            return (new Response(Dexpace.Sdk.Core.Http.Request.Request.Get("https://sse.example.test/events"), Status.Ok, Protocol.Http11), null);
        }

        var body = new SseBody(SseResponses.Bytes("data: x\n\n")) { Length = kind == "content-length-zero" ? 0 : -1 };
        var response = kind switch
        {
            "status-204" => SseResponses.Respond(body, Status.FromCode(204)),
            "status-205" => SseResponses.Respond(body, Status.FromCode(205)),
            "status-304" => SseResponses.Respond(body, Status.NotModified),
            "head-request" => SseResponses.Respond(body, Status.Ok, Method.Head),
            "content-length-zero" => SseResponses.Respond(body),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown bodyless case."),
        };
        return (response, body);
    }

    [Theory]
    [MemberData(nameof(BodylessCases))]
    public void FromResponse_rejects_a_bodyless_response_and_disposes_it(string kind)
    {
        var (response, body) = BodylessResponse(kind);

        var ex = Assert.Throws<ArgumentException>(() => ServerSentEventStream.FromResponse(response));

        Assert.Equal("response", ex.ParamName);
        Assert.DoesNotContain("sse.example.test", ex.Message, StringComparison.Ordinal);
        if (body is not null)
        {
            Assert.Equal(1, body.DisposeCount);
            Assert.Equal(0, body.OpenCount);
        }
    }

    [Fact]
    public void FromResponse_disposes_the_response_when_the_cap_is_invalid()
    {
        var (response, body) = SseResponses.Respond("data: x\n\n");

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ServerSentEventStream.FromResponse(response, maxLineBytes: 0));

        Assert.Equal("maxLineBytes", ex.ParamName);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public void FromResponse_rejects_a_null_response()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ServerSentEventStream.FromResponse(null!));

        Assert.Equal("response", ex.ParamName);
    }

    [Fact]
    public void A_throw_while_disposing_a_rejected_response_is_attached_not_thrown()
    {
        var failure = new InvalidOperationException("close failed");
        var body = new SseBody(SseResponses.Bytes("data: x\n\n")) { DisposeFailure = failure };
        var response = SseResponses.Respond(body, Status.NoContent);

        var ex = Assert.Throws<ArgumentException>(() => ServerSentEventStream.FromResponse(response));

        Assert.Same(failure, Assert.Single(ExceptionTrail.GetSuppressed(ex)));
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Construction_does_no_io_and_a_never_enumerated_stream_only_releases_the_response()
    {
        var (response, body) = SseResponses.Respond("data: x\n\n");

        var stream = ServerSentEventStream.FromResponse(response);

        Assert.Equal(0, body.OpenCount);
        Assert.Equal(0, body.DisposeCount);

        await stream.DisposeAsync();

        Assert.Equal(0, body.OpenCount);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Dispose_and_DisposeAsync_in_any_mix_release_exactly_once()
    {
        var (response, body) = SseResponses.Respond("data: x\n\n");
        var stream = ServerSentEventStream.FromResponse(response);

        stream.Dispose();
        await stream.DisposeAsync();
        stream.Dispose();
        await stream.DisposeAsync();

        Assert.Equal(1, body.DisposeCount);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    // The out-of-band report of Disposal (P4b-15): an exception event on the current activity tagged
    // dexpace.dispose.suppressed.
    private static bool Reported(Activity activity) =>
        activity.Events.Any(e => e.Name == "exception" && e.Tags.Any(tag => tag.Key == "dexpace.dispose.suppressed"));

    private static async Task<List<ServerSentEvent>> Collect(IAsyncEnumerable<ServerSentEvent> events)
    {
        var all = new List<ServerSentEvent>();
        await foreach (var ev in events)
        {
            all.Add(ev);
        }

        return all;
    }

    [Fact]
    public async Task The_events_are_the_readers_events()
    {
        var (response, body) = SseResponses.Respond("data: a\n\nevent: e\ndata: b\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);

        var events = await Collect(stream);

        Assert.Equal(["a", "b"], events.Select(e => e.Data[0]));
        Assert.Equal("e", events[1].Event);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Natural_end_releases_once_and_a_release_failure_is_reported_not_thrown()
    {
        var logger = new RecordingLogger();
        var (response, body) = SseResponses.Respond("data: a\n\ndata: b\n\n", disposeFailure: new InvalidOperationException("close boom"));
        using var activity = _source.StartActivity("sse-natural-end");
        Assert.NotNull(activity);
        var stream = ServerSentEventStream.FromResponse(response, logger: logger);

        var events = await Collect(stream);

        Assert.Equal(2, events.Count);
        Assert.Equal(1, body.DisposeCount);
        Assert.True(Reported(activity));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(DexpaceLogEvents.DisposeSuppressedId, entry.EventId.Id);
        Assert.DoesNotContain("boom", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Breaking_out_early_releases_once_and_swallows_a_release_failure()
    {
        var logger = new RecordingLogger();
        var (response, body) = SseResponses.Respond("data: a\n\ndata: b\n\n", disposeFailure: new InvalidOperationException("close boom"));
        using var activity = _source.StartActivity("sse-break");
        Assert.NotNull(activity);
        var stream = ServerSentEventStream.FromResponse(response, logger: logger);

        await foreach (var ev in stream)
        {
            Assert.Equal(["a"], ev.Data);
            break;
        }

        Assert.Equal(1, body.DisposeCount);
        Assert.True(Reported(activity));
        Assert.Equal(DexpaceLogEvents.DisposeSuppressedId, Assert.Single(logger.Entries).EventId.Id);
        await stream.DisposeAsync();
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_consumers_own_exception_in_the_loop_body_is_not_replaced_by_a_release_failure()
    {
        // Fact 5: a finally that throws replaces the exception in flight, so the early-dispose release must never throw.
        var (response, body) = SseResponses.Respond("data: a\n\ndata: b\n\n", disposeFailure: new InvalidOperationException("close boom"));
        var stream = ServerSentEventStream.FromResponse(response);

        var thrown = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var unused in stream)
            {
                throw new ArgumentException("consumer");
            }
        });

        Assert.Equal("consumer", thrown.Message);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_GetAsyncEnumerator_that_is_never_disposed_leaves_release_to_the_facade()
    {
        // Fact 4: a compiler-generated enumerator that is abandoned never runs its finally, so the facade owns the
        // release itself; an abandoned stream is released by the explicit dispose and by nothing else.
        var (response, body) = SseResponses.Respond("data: a\n\ndata: b\n\n");
        var stream = ServerSentEventStream.FromResponse(response);
        var abandoned = stream.GetAsyncEnumerator(Token);
        Assert.True(await abandoned.MoveNextAsync());

        Assert.Equal(0, body.DisposeCount);

        await stream.DisposeAsync();

        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_dispose_propagates_a_release_failure(bool useAsync)
    {
        var failure = new InvalidOperationException("close boom");
        var (response, body) = SseResponses.Respond("data: a\n\n", disposeFailure: failure);
        var stream = ServerSentEventStream.FromResponse(response);

        var thrown = useAsync
            ? await Assert.ThrowsAsync<InvalidOperationException>(async () => await stream.DisposeAsync())
            : Assert.Throws<InvalidOperationException>(stream.Dispose);

        Assert.Same(failure, thrown);
        Assert.Equal(1, body.DisposeCount);

        // Only the first call propagates; the second is a no-op (SSE-28).
        await stream.DisposeAsync();
        stream.Dispose();
        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task On_an_explicit_dispose_the_streams_failure_is_primary_and_the_responses_is_attached(bool useAsync)
    {
        var streamFailure = new InvalidOperationException("stream boom");
        var responseFailure = new IOException("response boom");
        var body = new SseBody(() => new ThrowingDisposeStream(new MemoryStream("data: a\n\n"u8.ToArray()), streamFailure))
        {
            DisposeFailure = responseFailure,
        };
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        var live = stream.GetAsyncEnumerator(Token);
        Assert.True(await live.MoveNextAsync());

        var thrown = useAsync
            ? await Assert.ThrowsAsync<InvalidOperationException>(async () => await stream.DisposeAsync())
            : Assert.Throws<InvalidOperationException>(stream.Dispose);

        Assert.Same(streamFailure, thrown);
        Assert.Same(responseFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Taking_a_view_after_close_throws_ObjectDisposedException()
    {
        var (response, _) = SseResponses.Respond("data: a\n\n");
        var stream = ServerSentEventStream.FromResponse(response);
        await stream.DisposeAsync();

        var ex = Assert.Throws<ObjectDisposedException>(() => stream.GetAsyncEnumerator(Token));

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public async Task A_second_GetAsyncEnumerator_throws_InvalidOperationException_at_the_call()
    {
        var (response, _) = SseResponses.Respond("data: a\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);
        _ = stream.GetAsyncEnumerator(Token);

        // Synchronous: the throw is at the call, before any MoveNextAsync.
        var ex = Assert.Throws<InvalidOperationException>(() => stream.GetAsyncEnumerator(Token));

        Assert.IsType<InvalidOperationException>(ex);
    }

    [Fact]
    public async Task The_body_is_opened_at_the_first_pull_through_OpenReadAsync()
    {
        var (response, body) = SseResponses.Respond("data: a\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);

        await using var enumerator = stream.GetAsyncEnumerator(Token);
        Assert.Equal(0, body.OpenCount);

        Assert.True(await enumerator.MoveNextAsync());

        Assert.Equal(1, body.AsyncOpenCount);
        Assert.Equal(0, body.SyncOpenCount);
    }

    [Fact]
    public async Task The_opened_stream_is_released_before_the_response()
    {
        var log = new List<string>();
        var body = new SseBody(() => new ThrowingDisposeStream(new MemoryStream("data: a\n\n"u8.ToArray()), null, log)) { Log = log };
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));

        await Collect(stream);

        Assert.Equal(["body:open-async", "stream:dispose", "body:dispose"], log);
    }

    [Fact]
    public async Task A_close_between_pulls_ends_the_iterator_cleanly()
    {
        var (response, body) = SseResponses.Respond("data: a\n\ndata: b\n\n");
        var stream = ServerSentEventStream.FromResponse(response);
        await using var enumerator = stream.GetAsyncEnumerator(Token);
        Assert.True(await enumerator.MoveNextAsync());

        await stream.DisposeAsync();

        Assert.False(await enumerator.MoveNextAsync());
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_view_taken_before_close_but_first_pulled_after_it_ends_cleanly_without_opening()
    {
        var (response, body) = SseResponses.Respond("data: a\n\n");
        var stream = ServerSentEventStream.FromResponse(response);
        await using var enumerator = stream.GetAsyncEnumerator(Token);

        await stream.DisposeAsync();

        Assert.False(await enumerator.MoveNextAsync());
        Assert.Equal(0, body.OpenCount);
        Assert.Equal(1, body.DisposeCount);
    }

    // ── The blocking view (task 3.3) ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AsEnumerable_yields_the_events_and_releases_once_at_the_end()
    {
        var (response, body) = SseResponses.Respond("data: a\n\nid: 2\ndata: b\n\n");
        var stream = ServerSentEventStream.FromResponse(response);

        var events = stream.AsEnumerable().ToList();

        Assert.Equal(["a", "b"], events.Select(e => e.Data[0]));
        Assert.Equal("2", events[1].Id);
        Assert.Equal(1, body.DisposeCount);
        stream.Dispose();
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public void The_body_is_opened_through_OpenRead_never_OpenReadAsync()
    {
        var body = new SseBody(SseResponses.Bytes("data: a\n\n")) { AsyncOpen = _ => throw new NotSupportedException("async open") };
        using var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));

        var events = stream.AsEnumerable().ToList();

        Assert.Single(events);
        Assert.Equal(1, body.SyncOpenCount);
        Assert.Equal(0, body.AsyncOpenCount);
    }

    [Fact]
    public void Breaking_out_early_swallows_a_release_failure_and_does_not_mask_the_consumer()
    {
        var logger = new RecordingLogger();
        var (response, body) = SseResponses.Respond("data: a\n\ndata: b\n\n", disposeFailure: new InvalidOperationException("close boom"));
        using var activity = _source.StartActivity("sse-blocking-break");
        Assert.NotNull(activity);
        var stream = ServerSentEventStream.FromResponse(response, logger: logger);

        foreach (var ev in stream.AsEnumerable())
        {
            Assert.Equal(["a"], ev.Data);
            break;
        }

        Assert.Equal(1, body.DisposeCount);
        Assert.True(Reported(activity));
        Assert.Equal(DexpaceLogEvents.DisposeSuppressedId, Assert.Single(logger.Entries).EventId.Id);

        var (second, secondBody) = SseResponses.Respond("data: a\n\n", disposeFailure: new InvalidOperationException("close boom"));
        var secondStream = ServerSentEventStream.FromResponse(second);
        var thrown = Assert.Throws<ArgumentException>(() =>
        {
            foreach (var unused in secondStream.AsEnumerable())
            {
                throw new ArgumentException("consumer");
            }
        });
        Assert.Equal("consumer", thrown.Message);
        Assert.Equal(1, secondBody.DisposeCount);
    }

    [Fact]
    public void AsEnumerable_is_lazy_and_takes_the_latch_at_GetEnumerator()
    {
        var (response, body) = SseResponses.Respond("data: a\n\n");
        using var stream = ServerSentEventStream.FromResponse(response);

        var first = stream.AsEnumerable();
        var second = stream.AsEnumerable();

        Assert.Equal(0, body.OpenCount);
        using var taken = first.GetEnumerator();
        Assert.Throws<InvalidOperationException>(() => second.GetEnumerator());
        Assert.Equal(0, body.OpenCount);
        Assert.True(taken.MoveNext());
        Assert.Equal(1, body.OpenCount);
    }

    [Fact]
    public void Taking_the_blocking_view_after_close_throws_ObjectDisposedException()
    {
        var (response, _) = SseResponses.Respond("data: a\n\n");
        var stream = ServerSentEventStream.FromResponse(response);
        var view = stream.AsEnumerable();
        stream.Dispose();

        var ex = Assert.Throws<ObjectDisposedException>(() => view.GetEnumerator());

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public async Task A_second_view_throws_InvalidOperationException_at_the_call_in_either_order()
    {
        var (first, _) = SseResponses.Respond("data: a\n\n");
        await using var blockingFirst = ServerSentEventStream.FromResponse(first);
        using var taken = blockingFirst.AsEnumerable().GetEnumerator();
        Assert.IsType<InvalidOperationException>(Assert.Throws<InvalidOperationException>(() => blockingFirst.GetAsyncEnumerator(Token)));
        Assert.Throws<InvalidOperationException>(() => blockingFirst.AsEnumerable().GetEnumerator());

        var (second, _) = SseResponses.Respond("data: a\n\n");
        await using var asyncFirst = ServerSentEventStream.FromResponse(second);
        _ = asyncFirst.GetAsyncEnumerator(Token);
        Assert.Throws<InvalidOperationException>(() => asyncFirst.AsEnumerable().GetEnumerator());
    }

    [Fact]
    public void The_same_view_object_enumerated_twice_throws()
    {
        var (response, _) = SseResponses.Respond("data: a\n\n");
        using var stream = ServerSentEventStream.FromResponse(response);
        var view = stream.AsEnumerable();
        using var taken = view.GetEnumerator();

        Assert.Throws<InvalidOperationException>(() => view.GetEnumerator());
    }

    [Fact]
    public void The_facade_is_not_an_IEnumerable()
    {
        // P7b-15: implementing both interfaces makes LINQ ambiguous (CS0121, verified in the pre-flight) and lets foreach
        // versus await foreach silently choose the path.
        Assert.False(typeof(IEnumerable<ServerSentEvent>).IsAssignableFrom(typeof(ServerSentEventStream)));
        Assert.True(typeof(IAsyncEnumerable<ServerSentEvent>).IsAssignableFrom(typeof(ServerSentEventStream)));
    }

    [Fact]
    public void A_close_between_pulls_ends_a_blocking_iteration_cleanly()
    {
        var (response, body) = SseResponses.Respond("data: a\n\ndata: b\n\n");
        var stream = ServerSentEventStream.FromResponse(response);
        using var enumerator = stream.AsEnumerable().GetEnumerator();
        Assert.True(enumerator.MoveNext());

        stream.Dispose();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(1, body.DisposeCount);
    }
}
