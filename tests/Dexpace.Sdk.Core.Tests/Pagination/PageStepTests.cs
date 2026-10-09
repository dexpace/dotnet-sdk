// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The transports and bodies under test are released by the step under test; the tests read their counters.
#pragma warning disable CA2012 // The blocking tests store the already-completed task and read it exactly once, as the blocking shell does.
#pragma warning disable CA2201 // OutOfMemoryException is the fatal exception under test (ExceptionFacts.IsFatal).

using System.Buffers;
using System.Text;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>
/// The one fetch-and-parse step behind both engines (PAGE-4, PAGE-11, PAGE-13, PAGE-15, PAGE-27, PAGE-28, PAGE-32,
/// PAGE-33, PAGE-36; design P7c-4, P7c-14, P7c-17): send, read once, parse, snapshot, close.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PageStepTests
{
    private static readonly Request s_first = Request.Get("https://api.example/items");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // A private exception no serde can throw, so a ThrowsAsync<> on it proves the strategy path ran.
    private sealed class StrategySentinelException(string message) : Exception(message);

    private sealed class ThrowingStrategy(Exception exception) : IPageStrategy<Envelope, int>
    {
        public PageInfo<int> Parse(Envelope page, Response response, Request first) => throw exception;
    }

    private sealed class NullReturningStrategy : IPageStrategy<Envelope, int>
    {
        public PageInfo<int> Parse(Envelope page, Response response, Request first) => null!;
    }

    // Counts which member the step calls, and how often.
    private sealed class CountingSerde : ISerde
    {
        private readonly EnvelopeSerde _inner = new();

        public int AsyncCalls { get; private set; }

        public int SyncCalls { get; private set; }

        public MediaType DefaultMediaType => _inner.DefaultMediaType;

        public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) =>
            _inner.SerializeAsync(destination, value, cancellationToken);

        public ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
        {
            AsyncCalls++;
            return _inner.DeserializeAsync<T>(source, cancellationToken);
        }

        public void Serialize<T>(IBufferWriter<byte> destination, T value) => _inner.Serialize(destination, value);

        public T? Deserialize<T>(ReadOnlySpan<byte> utf8)
        {
            SyncCalls++;
            return _inner.Deserialize<T>(utf8);
        }
    }

    private static IPageStrategy<Envelope, int> CursorStrategy() =>
        PaginationStrategies.Cursor<Envelope, int>(e => e.Items, e => e.Next);

    private static PageWalk<Envelope, int> AsyncWalk(
        IAsyncHttpClient client,
        IPageStrategy<Envelope, int>? strategy = null,
        ISerde? serde = null,
        RequestOptions? options = null) =>
        PageWalk<Envelope, int>.ForAsync(client, s_first, serde ?? new EnvelopeSerde(), strategy ?? CursorStrategy(), options, maxPages: null);

    private static PageWalk<Envelope, int> BlockingWalk(
        IHttpClient client,
        IPageStrategy<Envelope, int>? strategy = null,
        ISerde? serde = null,
        RequestOptions? options = null) =>
        PageWalk<Envelope, int>.ForBlocking(client, s_first, serde ?? new EnvelopeSerde(), strategy ?? CursorStrategy(), options, maxPages: null);

    private static ValueTask<FetchedStep<int>> FetchAsync(PageWalk<Envelope, int> walk) =>
        PageStep.FetchAsync(walk, walk.First, async: true, Ct);

    // ── success ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_page_carries_a_snapshot_of_the_items_the_sent_request_and_the_responses_status_and_headers()
    {
        var items = new List<int> { 1, 2, 3 };
        var body = EnvelopeBody.Of([1, 2, 3], "c");
        var headers = new Headers.Builder().Add("X-Page", "1").Build();
        var otherRequest = Request.Get("https://api.example/executed-and-stamped");
        var transport = new ScriptedTransport((Request _) => TestResponses.Create(Status.Created, otherRequest, headers, body));
        var strategy = PaginationStrategies.Create<Envelope, int>(_ => items, (_, _, first) => first);

        var step = await FetchAsync(AsyncWalk(transport, strategy));

        items.Add(4);
        Assert.Equal([1, 2, 3], step.Page.Values);
        Assert.Equal(Status.Created, step.Page.Status);
        Assert.Equal("1", step.Page.Headers.Get("X-Page"));
        Assert.Same(s_first, step.Page.Request);
        Assert.NotSame(otherRequest, step.Page.Request);
        Assert.Same(s_first, step.Next);
    }

    [Fact]
    public async Task The_response_is_closed_exactly_once_before_the_step_returns()
    {
        var body = EnvelopeBody.Of([1]);
        var transport = new ScriptedTransport(PageFixtures.Respond(body));

        var step = await FetchAsync(AsyncWalk(transport));

        Assert.Equal(1, body.DisposeCount);
        Assert.Null(step.Next);
    }

    [Fact]
    public async Task The_async_path_deserializes_through_the_async_member_and_the_blocking_path_through_the_sync_one()
    {
        var asyncSerde = new CountingSerde();
        var syncSerde = new CountingSerde();

        _ = await FetchAsync(AsyncWalk(new ScriptedTransport(PageFixtures.Respond([1])), serde: asyncSerde));
        var blocking = PageStep.FetchAsync(BlockingWalk(new SyncFirstTransport(PageFixtures.Respond([1])), serde: syncSerde), s_first, async: false, Ct);
        _ = SyncResult(blocking);

        Assert.Equal((1, 0), (asyncSerde.AsyncCalls, asyncSerde.SyncCalls));
        Assert.Equal((0, 1), (syncSerde.AsyncCalls, syncSerde.SyncCalls));
    }

    [Fact]
    public async Task A_walks_options_and_token_reach_the_transport_as_the_same_instances()
    {
        var options = new RequestOptions { MaxRetries = 7 };
        using var cts = new CancellationTokenSource();
        var transport = new RecordingTransport(PageFixtures.Respond([1]));

        _ = await PageStep.FetchAsync(AsyncWalk(transport, options: options), s_first, async: true, cts.Token);

        Assert.Same(options, transport.LastCall!.Options);
        Assert.Equal(cts.Token, transport.LastCall.CancellationToken);
    }

    // ── PAGE-13: a parse failure closes the response, the parse error stays primary ───────────────────

    [Fact]
    public async Task A_parse_failure_closes_the_response_once_and_the_parse_error_is_primary()
    {
        var body = EnvelopeBody.Of([1], disposeFailure: new IOException("close failed"));
        var transport = new ScriptedTransport(PageFixtures.Respond(body));
        var walk = AsyncWalk(transport, new ThrowingStrategy(new StrategySentinelException("parse failed")));

        var ex = await Assert.ThrowsAsync<StrategySentinelException>(async () => await FetchAsync(walk));

        Assert.Equal(1, body.DisposeCount);
        Assert.Contains(ExceptionTrail.GetSuppressed(ex), s => s is IOException { Message: "close failed" });
    }

    [Fact]
    public async Task A_serde_failure_closes_the_response_and_surfaces_as_the_original_type()
    {
        var body = new EnvelopeBody(Encoding.UTF8.GetBytes("not an envelope"));
        var transport = new ScriptedTransport(PageFixtures.Respond(body));

        await Assert.ThrowsAsync<MalformedEnvelopeException>(async () => await FetchAsync(AsyncWalk(transport)));

        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_fatal_exception_is_not_intercepted_by_the_close_path()
    {
        // ExceptionFacts.IsFatal gates the catch: an OutOfMemoryException propagates and nothing is disposed.
        var body = EnvelopeBody.Of([1]);
        var transport = new ScriptedTransport(PageFixtures.Respond(body));
        var walk = AsyncWalk(transport, new ThrowingStrategy(new OutOfMemoryException()));

        await Assert.ThrowsAsync<OutOfMemoryException>(async () => await FetchAsync(walk));

        Assert.Equal(0, body.DisposeCount);
    }

    // ── PAGE-15 and PAGE-32: a success-path dispose failure propagates unwrapped ──────────────────────

    [Fact]
    public async Task A_dispose_failure_on_the_success_path_propagates_unwrapped()
    {
        var body = EnvelopeBody.Of([1], disposeFailure: new IOException("close failed"));
        var transport = new ScriptedTransport(PageFixtures.Respond(body));

        var ex = await Assert.ThrowsAsync<IOException>(async () => await FetchAsync(AsyncWalk(transport)));

        Assert.Equal("close failed", ex.Message);
        Assert.Equal(1, body.DisposeCount);
    }

    // ── PAGE-28: the cause surfaces as itself ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_null_response_is_a_contract_violation_naming_the_client()
    {
        var transport = new ScriptedTransport((Func<Request, Response>)(_ => null!));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await FetchAsync(AsyncWalk(transport)));

        Assert.Contains(nameof(ScriptedTransport), ex.Message, StringComparison.Ordinal);
        Assert.Contains("SEAM-16", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_transport_fault_and_an_eager_throw_surface_as_their_own_types()
    {
        var fault = new ScriptedTransport(new IOException("connection reset"));
        var eager = new ScriptedTransport((Func<Request, Response>)(_ => throw new TimeoutException("eager")));

        await Assert.ThrowsAsync<IOException>(async () => await FetchAsync(AsyncWalk(fault)));
        await Assert.ThrowsAsync<TimeoutException>(async () => await FetchAsync(AsyncWalk(eager)));
    }

    // ── P7c-17: a null envelope and a null PageInfo ───────────────────────────────────────────────────

    [Fact]
    public async Task A_null_envelope_is_a_DeserializationException_naming_the_page_type_and_the_response_is_closed()
    {
        var body = new EnvelopeBody(Encoding.UTF8.GetBytes("null"));
        var transport = new ScriptedTransport(PageFixtures.Respond(body));

        var ex = await Assert.ThrowsAsync<DeserializationException>(async () => await FetchAsync(AsyncWalk(transport)));

        Assert.Contains(nameof(Envelope), ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_null_PageInfo_closes_the_response_and_names_the_strategy_type()
    {
        var body = EnvelopeBody.Of([1]);
        var transport = new ScriptedTransport(PageFixtures.Respond(body));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await FetchAsync(AsyncWalk(transport, new NullReturningStrategy())));

        Assert.Contains(nameof(NullReturningStrategy), ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);
    }

    // ── PAGE-33: a response delivered after the cancel is discarded ───────────────────────────────────

    [Fact]
    public async Task A_response_delivered_after_the_token_was_cancelled_is_disposed_and_discarded()
    {
        using var cts = new CancellationTokenSource();
        var body = EnvelopeBody.Of([1]);
        var serde = new CountingSerde();
        var transport = new ScriptedTransport((Request request) =>
        {
            cts.Cancel();
            return TestResponses.Create(Status.Ok, request, body: body);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await PageStep.FetchAsync(AsyncWalk(transport, serde: serde), s_first, async: true, cts.Token));

        Assert.Equal(1, body.DisposeCount);
        Assert.Equal((0, 0), (serde.AsyncCalls, serde.SyncCalls));
    }

    // ── P7c-4: with async false the step never awaits ─────────────────────────────────────────────────

    [Fact]
    public void With_async_false_the_step_never_calls_ExecuteAsync_and_returns_an_already_completed_task()
    {
        var transport = new SyncFirstTransport(PageFixtures.Respond([1, 2], "c"));

        var task = PageStep.FetchAsync(BlockingWalk(transport), s_first, async: false, Ct);

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal([1, 2], SyncResult(task).Page.Values);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public void Every_blocking_path_returns_an_already_completed_task()
    {
        var cases = new (Func<EnvelopeBody> Make, IPageStrategy<Envelope, int>? Strategy)[]
        {
            (() => EnvelopeBody.Of([1]), null),
            (() => EnvelopeBody.Of([1]), new ThrowingStrategy(new StrategySentinelException("parse"))),
            (() => new EnvelopeBody(Encoding.UTF8.GetBytes("null")), null),
            (() => EnvelopeBody.Of([1]), new NullReturningStrategy()),
            (() => EnvelopeBody.Of([1], disposeFailure: new IOException("close")), null),
        };

        foreach (var (make, strategy) in cases)
        {
            var transport = new SyncFirstTransport(PageFixtures.Respond(make()));
            var task = PageStep.FetchAsync(BlockingWalk(transport, strategy), s_first, async: false, Ct);
            Assert.True(task.IsCompleted, "a blocking path suspended");
        }

        var nullResponse = new SyncFirstTransport(_ => null!);
        Assert.True(PageStep.FetchAsync(BlockingWalk(nullResponse), s_first, async: false, Ct).IsCompleted);
    }

    [Fact]
    public void A_blocking_parse_failure_closes_once_with_the_parse_error_primary()
    {
        var body = EnvelopeBody.Of([1], disposeFailure: new IOException("close failed"));
        var transport = new SyncFirstTransport(PageFixtures.Respond(body));
        var walk = BlockingWalk(transport, new ThrowingStrategy(new StrategySentinelException("parse failed")));

        var task = PageStep.FetchAsync(walk, s_first, async: false, Ct);

        var ex = Assert.Throws<StrategySentinelException>(() => SyncResult(task));
        Assert.Equal(1, body.DisposeCount);
        Assert.Contains(ExceptionTrail.GetSuppressed(ex), s => s is IOException { Message: "close failed" });
    }

    // The blocking engine's own read of the step's task; it throws if the step suspended (PIPE-28).
    private static T SyncResult<T>(ValueTask<T> task) => SyncPath.GetCompletedResult(task, nameof(PageStepTests));
}
