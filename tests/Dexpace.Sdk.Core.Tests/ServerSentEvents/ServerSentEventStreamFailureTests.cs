// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>Failure paths and close from another thread: SSE-29, SSE-31, XCUT-1, SSE-18 (P7b-12, P7b-13).</summary>
[Trait("Category", "Unit")]
public sealed class ServerSentEventStreamFailureTests : IDisposable
{
    private static readonly TimeSpan s_hangGuard = TimeSpan.FromSeconds(10);

    private readonly ActivitySource _source = new("Dexpace.Sdk.Core.Tests.ServerSentEventStreamFailure");
    private readonly ActivityListener _listener;

    public ServerSentEventStreamFailureTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == _source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    private static SseBody BodyOver(Func<Stream> open, Exception? disposeFailure = null) =>
        new(open) { DisposeFailure = disposeFailure };

    private static async Task<List<ServerSentEvent>> DrainAsync(ServerSentEventStream stream, CancellationToken token = default)
    {
        var events = new List<ServerSentEvent>();
        await foreach (var ev in stream.WithCancellation(token))
        {
            events.Add(ev);
        }

        return events;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_mid_stream_failure_releases_first_then_rethrows_the_primary_unchanged(bool blocking)
    {
        var failure = new IOException("connection dropped");
        var body = BodyOver(() => new FailingAfterStream("data: 1\n\ndata: 2\n\n"u8.ToArray(), failure));
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        var delivered = new List<string>();

        IOException? caught = null;
        try
        {
            if (blocking)
            {
                foreach (var ev in stream.AsEnumerable())
                {
                    delivered.Add(ev.Data[0]);
                }
            }
            else
            {
                await foreach (var ev in stream.WithCancellation(Token))
                {
                    delivered.Add(ev.Data[0]);
                }
            }
        }
        catch (IOException ex)
        {
            // The release ran before the error reached the caller (SSE-29).
            Assert.Equal(1, body.DisposeCount);
            caught = ex;
        }

        Assert.Equal(["1", "2"], delivered);
        Assert.Same(failure, caught);
        Assert.NotNull(caught);
        Assert.Contains(nameof(FailingAfterStream), caught.StackTrace, StringComparison.Ordinal);
        Assert.Empty(ExceptionTrail.GetSuppressed(caught));
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_release_failure_on_a_failure_path_is_attached_to_the_primary_and_reported_nowhere_else()
    {
        var primary = new IOException("connection dropped");
        var releaseFailure = new InvalidOperationException("close boom");
        var logger = new RecordingLogger();
        var body = BodyOver(() => new FailingAfterStream("data: 1\n\n"u8.ToArray(), primary), releaseFailure);
        using var activity = _source.StartActivity("sse-failure-path");
        Assert.NotNull(activity);
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body), logger: logger);

        var thrown = await Assert.ThrowsAsync<IOException>(() => DrainAsync(stream, Token));

        Assert.Same(primary, thrown);
        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
        // P4b-15: exactly one of "attached" and "reported", never both.
        Assert.Empty(logger.Entries);
        Assert.DoesNotContain(activity.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task A_line_cap_failure_releases_and_rethrows_the_exception_unchanged()
    {
        var releaseFailure = new InvalidOperationException("close boom");
        var body = BodyOver(() => new MemoryStream(System.Text.Encoding.UTF8.GetBytes("data: " + new string('x', 200) + "\n\n")), releaseFailure);
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body), maxLineBytes: 32);

        ServerSentEventLineTooLongException? caught = null;
        try
        {
            await DrainAsync(stream, Token);
        }
        catch (ServerSentEventLineTooLongException ex)
        {
            Assert.Equal(1, body.DisposeCount);
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal(32, caught.MaxLineBytes);
        Assert.Same(releaseFailure, Assert.Single(caught.Suppressed));
    }

    [Fact]
    public async Task A_cancelled_caller_token_surfaces_OperationCanceledException_and_is_not_rewritten()
    {
        using var gated = new GatedStream("data: a\n\n"u8.ToArray());
        var body = BodyOver(() => gated);
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var loop = Task.Run(() => DrainAsync(stream, cancel.Token), Token);
        await gated.Parked.WaitAsync(s_hangGuard, Token);

        await cancel.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loop.WaitAsync(s_hangGuard, Token));
        Assert.IsNotType<IOException>(thrown, exactMatch: false);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Close_from_another_thread_while_a_read_is_parked_surfaces_IOException_and_one_release()
    {
        using var gated = new GatedStream("data: a\n\n"u8.ToArray());
        var body = BodyOver(() => gated);
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        var delivered = new List<string>();
        var loop = Task.Run(
            async () =>
            {
                await foreach (var ev in stream.WithCancellation(Token))
                {
                    delivered.Add(ev.Data[0]);
                }
            },
            Token);
        await gated.Parked.WaitAsync(s_hangGuard, Token);

        await stream.DisposeAsync();

        var thrown = await Assert.ThrowsAsync<IOException>(() => loop.WaitAsync(s_hangGuard, Token));
        Assert.True(thrown.InnerException is ObjectDisposedException or OperationCanceledException, thrown.InnerException?.GetType().FullName);
        Assert.Equal(["a"], delivered);
        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(1, gated.DisposeCount);
    }

    [Fact]
    public async Task Close_cancels_the_internal_token_so_a_cooperative_stream_unblocks_at_once()
    {
        // The gate is NOT completed by disposal here, so only the facade's internal token can unblock the parked read.
        using var gated = new GatedStream("data: a\n\n"u8.ToArray(), completeOnDispose: false);
        var body = BodyOver(() => gated);
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        var loop = Task.Run(() => DrainAsync(stream), Token);
        await gated.Parked.WaitAsync(s_hangGuard, Token);

        await stream.DisposeAsync();

        var thrown = await Assert.ThrowsAsync<IOException>(() => loop.WaitAsync(s_hangGuard, Token));
        Assert.IsAssignableFrom<OperationCanceledException>(thrown.InnerException);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task The_blocking_view_normalises_a_close_the_same_way()
    {
        using var gated = new GatedStream("data: a\n\n"u8.ToArray());
        var body = BodyOver(() => gated);
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        var loop = Task.Run(() => stream.AsEnumerable().ToList(), Token);
        await gated.Parked.WaitAsync(s_hangGuard, Token);

        stream.Dispose();

        var thrown = await Assert.ThrowsAsync<IOException>(() => loop.WaitAsync(s_hangGuard, Token));
        Assert.IsType<ObjectDisposedException>(thrown.InnerException);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_fatal_exception_is_never_wrapped_or_swallowed()
    {
#pragma warning disable CA2201 // A test double: the fatal exception the SDK must never wrap, swallow or attach to.
        var fatal = new OutOfMemoryException("simulated");
#pragma warning restore CA2201
        var body = BodyOver(() => new FailingAfterStream("data: 1\n\n"u8.ToArray(), fatal), new InvalidOperationException("close boom"));
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => DrainAsync(stream, Token));

        Assert.Same(fatal, thrown);
        // Nothing is attached to a fatal exception (ExceptionFacts.IsFatal); the iterator's own finally still released.
        Assert.Empty(ExceptionTrail.GetSuppressed(thrown));
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_read_failure_while_not_closed_is_never_rewritten_to_IOException()
    {
        var failure = new InvalidOperationException("not a close");
        var body = BodyOver(() => new FailingAfterStream("data: 1\n\n"u8.ToArray(), failure));
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => DrainAsync(stream, Token));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task A_body_open_failure_is_attached_to_the_primary()
    {
        var openFailure = new InvalidOperationException("open failed");
        var releaseFailure = new IOException("close boom");
        var body = new SseBody(SseResponses.Bytes("data: a\n\n")) { OpenFailure = openFailure, DisposeFailure = releaseFailure };
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => DrainAsync(stream, Token));

        Assert.Same(openFailure, thrown);
        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_close_during_the_open_is_an_IOException_and_one_release()
    {
        var opening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var body = new SseBody(SseResponses.Bytes("data: a\n\n"))
        {
            AsyncOpen = async cancellationToken =>
            {
                opening.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new MemoryStream();
            },
        };
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        var loop = Task.Run(() => DrainAsync(stream), Token);
        await opening.Task.WaitAsync(s_hangGuard, Token);

        await stream.DisposeAsync();

        var thrown = await Assert.ThrowsAsync<IOException>(() => loop.WaitAsync(s_hangGuard, Token));
        Assert.IsAssignableFrom<OperationCanceledException>(thrown.InnerException);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_stream_that_arrives_after_the_close_is_disposed_and_reported_as_closed()
    {
        // The close's release ran before the open returned, so it never saw the stream; the open disposes it itself.
        var opening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var late = new ThrowingDisposeStream(new MemoryStream("data: a\n\n"u8.ToArray()), null);
        var body = new SseBody(SseResponses.Bytes(string.Empty))
        {
            AsyncOpen = async _ =>
            {
                opening.TrySetResult();
                await proceed.Task;
                return late;
            },
        };
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        var loop = Task.Run(() => DrainAsync(stream), Token);
        await opening.Task.WaitAsync(s_hangGuard, Token);
        await stream.DisposeAsync();

        proceed.SetResult();

        var thrown = await Assert.ThrowsAsync<IOException>(() => loop.WaitAsync(s_hangGuard, Token));
        Assert.IsType<ObjectDisposedException>(thrown.InnerException);
        Assert.Equal(1, late.DisposeCount);
        Assert.Equal(1, body.DisposeCount);
    }
}
