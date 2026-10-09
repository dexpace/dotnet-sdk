// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The typed adapter: SSE-33 to SSE-36, SSE-26, SSE-39 (P7b-16, P7b-17).</summary>
[Trait("Category", "Unit")]
public class ServerSentEventStreamTypedTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Every ordered pair of distinct views: the first is enumerated, the second must then be refused.
    public static TheoryData<int, int> ViewPairs
    {
        get
        {
            var data = new TheoryData<int, int>();
            for (var first = 0; first < 4; first++)
            {
                for (var second = 0; second < 4; second++)
                {
                    if (first != second)
                    {
                        data.Add(first, second);
                    }
                }
            }

            return data;
        }
    }

    // The sentinel is the caller's: this mapper is the test's, and core never names it (SSE-37).
    private static SseMapResult<string> Sentinel(string? name, string data) =>
        data == "[DONE]" ? SseMapResult.Done : data.Length == 0 ? SseMapResult.Skip : SseMapResult.Value(data);

    private static async Task<List<T>> Collect<T>(IAsyncEnumerable<T> sequence)
    {
        var all = new List<T>();
        await foreach (var item in sequence.WithCancellation(Token))
        {
            all.Add(item);
        }

        return all;
    }

    [Fact]
    public async Task The_mapper_receives_the_event_name_and_the_data_lines_joined_with_LF()
    {
        var (response, _) = SseResponses.Respond("event: x\ndata: a\ndata: b\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);
        var seen = new List<(string? Name, string Data)>();

        var mapped = await Collect(stream.MapAsync<int>((name, data) =>
        {
            seen.Add((name, data));
            return SseMapResult.Value(seen.Count);
        }));

        Assert.Equal([1], mapped);
        Assert.Equal([("x", "a\nb")], seen);
    }

    [Fact]
    public async Task No_data_lines_give_an_empty_string_and_an_absent_name_gives_null()
    {
        var (response, _) = SseResponses.Respond(": hi\nid: 1\n\ndata\n\nevent:\ndata: z\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);
        var seen = new List<(string? Name, string Data)>();

        await Collect(stream.MapAsync<int>((name, data) =>
        {
            seen.Add((name, data));
            return SseMapResult.Skip;
        }));

        // A comment-and-id event has no data; a colon-less data line is one empty line (also ""); a present-but-empty
        // event name is "" and not null.
        Assert.Equal([(null, string.Empty), (null, string.Empty), (string.Empty, "z")], seen);
    }

    [Fact]
    public async Task Value_yields_Skip_advances_silently_and_Done_ends()
    {
        var (response, body) = SseResponses.Respond(": hi\n\ndata: a\n\ndata: b\n\ndata: [DONE]\n\ndata: never\n\n");
        var stream = ServerSentEventStream.FromResponse(response);
        var calls = 0;

        var mapped = await Collect(stream.MapAsync<string>((name, data) =>
        {
            calls++;
            return Sentinel(name, data);
        }));

        Assert.Equal(["a", "b"], mapped);
        Assert.Equal(4, calls);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Done_releases_the_stream_quietly_and_completes()
    {
        var logger = new Dexpace.Sdk.TestSupport.Diagnostics.RecordingLogger();
        var (response, body) = SseResponses.Respond("data: a\n\ndata: [DONE]\n\n", disposeFailure: new InvalidOperationException("close boom"));
        var stream = ServerSentEventStream.FromResponse(response, logger: logger);
        await using var enumerator = stream.MapAsync<string>(Sentinel).GetAsyncEnumerator(Token);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(0, body.DisposeCount);

        // The Done step completes the enumeration with the release already done, and its failure is not thrown.
        Assert.False(await enumerator.MoveNextAsync());

        Assert.Equal(1, body.DisposeCount);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task Events_after_Done_are_never_parsed_or_mapped()
    {
        const string Through = "data: a\n\ndata: [DONE]\n\n";
        using var chunked = new ChunkedReadStream(System.Text.Encoding.UTF8.GetBytes(Through + "data: never\n\ndata: nor this\n\n"), 1);
        using var counting = new CountingReadStream(chunked);
        var body = new SseBody(() => counting);
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        var calls = new List<string>();

        var mapped = await Collect(stream.MapAsync<string>((name, data) =>
        {
            calls.Add(data);
            return Sentinel(name, data);
        }));

        Assert.Equal(["a"], mapped);
        Assert.Equal(["a", "[DONE]"], calls);
        // One byte per read makes the count exact: nothing was read beyond the sentinel's own block.
        Assert.Equal(Through.Length, counting.ReadCount);
    }

    [Fact]
    public async Task The_adapter_is_lazy_one_mapper_call_per_raw_event_pulled()
    {
        var (response, body) = SseResponses.Respond("data: a\n\n: ping\n\ndata: b\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);
        var calls = 0;

        var typed = stream.MapAsync<string>((name, data) =>
        {
            calls++;
            return data.Length == 0 ? SseMapResult.Skip : SseMapResult.Value(data);
        });

        Assert.Equal(0, calls);
        Assert.Equal(0, body.OpenCount);

        await using var enumerator = typed.GetAsyncEnumerator(Token);
        Assert.Equal(0, calls);
        Assert.Equal(0, body.OpenCount);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("a", enumerator.Current);
        Assert.Equal(1, calls);

        // The Skip is looped past inside one pull: two mapper calls, one element.
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("b", enumerator.Current);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task A_throwing_mapper_releases_first_then_propagates_unchanged()
    {
        var failure = new FormatException("not json");
        var releaseFailure = new InvalidOperationException("close boom");
        var (response, body) = SseResponses.Respond("data: a\n\ndata: bad\n\n", disposeFailure: releaseFailure);
        var stream = ServerSentEventStream.FromResponse(response);

        FormatException? caught = null;
        try
        {
            await Collect(stream.MapAsync<string>((_, data) => data == "bad" ? throw failure : SseMapResult.Value(data)));
        }
        catch (FormatException ex)
        {
            // Released before the caller sees it (SSE-36).
            Assert.Equal(1, body.DisposeCount);
            caught = ex;
        }

        Assert.Same(failure, caught);
        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(failure)));
    }

    [Fact]
    public void A_throwing_mapper_releases_first_then_propagates_unchanged_on_the_blocking_path()
    {
        var failure = new FormatException("not json");
        var releaseFailure = new InvalidOperationException("close boom");
        var (response, body) = SseResponses.Respond("data: bad\n\n", disposeFailure: releaseFailure);
        var stream = ServerSentEventStream.FromResponse(response);

        var thrown = Assert.Throws<FormatException>(() => stream.Map<string>((_, _) => throw failure).ToList());

        Assert.Same(failure, thrown);
        Assert.Equal(1, body.DisposeCount);
        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(failure)));
    }

    [Fact]
    public async Task A_default_result_is_a_mapper_failure_released_first()
    {
        var (response, body) = SseResponses.Respond("data: a\n\n");
        var stream = ServerSentEventStream.FromResponse(response);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Collect(stream.MapAsync<int>((_, _) => default)));

        Assert.Contains("default SseMapResult", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);

        var (blockingResponse, blockingBody) = SseResponses.Respond("data: a\n\n");
        var blockingStream = ServerSentEventStream.FromResponse(blockingResponse);
        Assert.Throws<InvalidOperationException>(() => blockingStream.Map<int>((_, _) => default).ToList());
        Assert.Equal(1, blockingBody.DisposeCount);
    }

    [Fact]
    public async Task A_default_signal_converted_inside_the_mapper_is_an_ArgumentException_released_first()
    {
        // default(SseMapResult) converts to SseMapResult<int> in the mapper's own return, so it throws there; the adapter
        // treats it like any other mapper throw (release, then the same exception) and not as the InvalidOperationException
        // reserved for a mapper that returns default(SseMapResult<int>) (SSE-36, P7b-16).
        var releaseFailure = new IOException("close boom");
        var (response, body) = SseResponses.Respond("data: a\n\n", disposeFailure: releaseFailure);
        var stream = ServerSentEventStream.FromResponse(response);

        var thrown = await Assert.ThrowsAsync<ArgumentException>(
            () => Collect(stream.MapAsync<int>((_, _) => default(SseMapResult))));

        Assert.Equal(1, body.DisposeCount);
        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));

        var (blockingResponse, blockingBody) = SseResponses.Respond("data: a\n\n", disposeFailure: releaseFailure);
        var blockingStream = ServerSentEventStream.FromResponse(blockingResponse);
        var blockingThrown = Assert.Throws<ArgumentException>(() => blockingStream.Map<int>((_, _) => default(SseMapResult)).ToList());
        Assert.Equal(1, blockingBody.DisposeCount);
        Assert.Contains(releaseFailure, ExceptionTrail.GetSuppressed(blockingThrown));
    }

    [Fact]
    public async Task A_fatal_mapper_exception_propagates_without_an_attach_and_the_enumerator_still_releases()
    {
        // ExceptionFacts.IsFatal: no release is attached to it, but the enumerator's own finally releases on the way out.
#pragma warning disable CA2201 // A test double: the fatal exception the SDK must never wrap, swallow or attach to.
        var fatal = new OutOfMemoryException("simulated");
#pragma warning restore CA2201
        var (response, body) = SseResponses.Respond("data: a\n\n", disposeFailure: new InvalidOperationException("close boom"));
        var stream = ServerSentEventStream.FromResponse(response);

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(
            () => Collect(stream.MapAsync<int>((_, _) => throw fatal)));

        Assert.Same(fatal, thrown);
        Assert.Empty(ExceptionTrail.GetSuppressed(thrown));
        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [MemberData(nameof(ViewPairs))]
    public async Task The_four_views_share_one_latch(int first, int second)
    {
        var (response, _) = SseResponses.Respond("data: a\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);
        object? held = Take(stream, first);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Take(stream, second));
            Assert.IsType<InvalidOperationException>(ex);
        }
        finally
        {
            switch (held)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task After_close_every_view_throws_ObjectDisposedException(int view)
    {
        var (response, _) = SseResponses.Respond("data: a\n\n");
        var stream = ServerSentEventStream.FromResponse(response);
        await stream.DisposeAsync();

        var ex = Assert.Throws<ObjectDisposedException>(() => Take(stream, view));

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public async Task A_mapper_view_object_enumerated_twice_throws()
    {
        var (response, _) = SseResponses.Respond("data: a\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);
        var typed = stream.MapAsync<string>(Sentinel);
        _ = typed.GetAsyncEnumerator(Token);

        Assert.Throws<InvalidOperationException>(() => typed.GetAsyncEnumerator(Token));
    }

    [Fact]
    public void Map_over_the_blocking_path_opens_through_OpenRead()
    {
        var body = new SseBody(SseResponses.Bytes("data: a\n\ndata: [DONE]\n\n")) { AsyncOpen = _ => throw new NotSupportedException("async open") };
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));

        var mapped = stream.Map<string>(Sentinel).ToList();

        Assert.Equal(["a"], mapped);
        Assert.Equal(1, body.SyncOpenCount);
        Assert.Equal(0, body.AsyncOpenCount);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task MapAsync_forwards_the_enumeration_token()
    {
        var body = new SseBody(() => new ParkedAfterStream("data: a\n\n"u8.ToArray()));
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await using var enumerator = stream.MapAsync<string>(Sentinel).GetAsyncEnumerator(cancel.Token);
        Assert.True(await enumerator.MoveNextAsync());

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task MapAsync_and_Map_reject_a_null_mapper()
    {
        var (response, _) = SseResponses.Respond("data: a\n\n");
        await using var stream = ServerSentEventStream.FromResponse(response);

        Assert.Equal("mapper", Assert.Throws<ArgumentNullException>(() => stream.MapAsync<int>(null!)).ParamName);
        Assert.Equal("mapper", Assert.Throws<ArgumentNullException>(() => stream.Map<int>(null!)).ParamName);
    }

    // Takes one of the four views the way a caller does, at the call; the returned enumerator is for the caller to dispose.
    private static object Take(ServerSentEventStream stream, int view) => view switch
    {
        0 => stream.GetAsyncEnumerator(Token),
        1 => stream.AsEnumerable().GetEnumerator(),
        2 => stream.MapAsync<string>(Sentinel).GetAsyncEnumerator(Token),
        3 => stream.Map<string>(Sentinel).GetEnumerator(),
        _ => throw new ArgumentOutOfRangeException(nameof(view)),
    };
}
