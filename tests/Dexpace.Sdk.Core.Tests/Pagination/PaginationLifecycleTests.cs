// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/pagination/lifecycle.test.ts (13 cases) and adapted to design section
// 10 entry 17: a .NET page owns no response, the engine closes it before it yields, so Node's "closes the held page
// when the consumer advances / breaks" becomes "no response is open at any yield". Node's SuppressedError becomes
// ExceptionTrail (the parse error stays primary, the release failure is attached). Node's "consumer throws mid-iteration
// keeps the consumer error primary" is vacuous here: the response is already closed when the consumer runs (PAGE-32).
// These are pins over the public API for rules PageStep and the two shells already implement; each was proven able to
// fail by temporarily removing the matching close or guard (never committed). Every row runs over the async and the
// blocking pager.

#pragma warning disable CA2000 // The transports and bodies under test are released by the walk under test; the tests read their counters.

using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-4, PAGE-11, PAGE-12, PAGE-13, PAGE-15, PAGE-27, PAGE-28, PAGE-32 and PAGE-33 through the public API.</summary>
[Trait("Category", "Unit")]
public sealed class PaginationLifecycleTests
{
    private static readonly Request s_first = Request.Get("https://api.example/items");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class SentinelException(string message) : Exception(message);

    private sealed class ConsumerException(string message) : Exception(message);

    private sealed class ThrowingStrategy(Exception exception) : IPageStrategy<Envelope, int>
    {
        public PageInfo<int> Parse(Envelope page, Response response, Request first) => throw exception;
    }

    private sealed class NullReturningStrategy : IPageStrategy<Envelope, int>
    {
        public PageInfo<int> Parse(Envelope page, Response response, Request first) => null!;
    }

    private static IPageStrategy<Envelope, int> Cursor() => PaginationStrategies.Cursor<Envelope, int>(e => e.Items, e => e.Next);

    // Wraps a blocking enumerable so one set of rows drives both pagers; it blocks exactly as the pager does.
    private static async IAsyncEnumerable<T> AsAsync<T>(IEnumerable<T> source)
    {
        foreach (var element in source)
        {
            yield return element;
        }

        await Task.CompletedTask;
    }

    // Hands the token to the walk, as WithCancellation does, but stays an IAsyncEnumerable the rows can share.
    private static async IAsyncEnumerable<T> Cancellable<T>(IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var element in source.WithCancellation(token))
        {
            yield return element;
        }
    }

    private static IAsyncEnumerable<Page<int>> Pages(
        bool blocking,
        ScriptedTransport transport,
        IPageStrategy<Envelope, int>? strategy = null,
        CancellationToken? token = null) =>
        blocking
            ? AsAsync(Pageable.CreateBlocking<Envelope, int>(transport, s_first, new EnvelopeSerde(), strategy ?? Cursor(), null, null, token ?? Ct).AsPages())
            : Cancellable(Pageable.Create<Envelope, int>(transport, s_first, new EnvelopeSerde(), strategy ?? Cursor()).AsPages(), token ?? Ct);

    private static IAsyncEnumerable<int> Items(bool blocking, ScriptedTransport transport, IPageStrategy<Envelope, int>? strategy = null) =>
        blocking
            ? AsAsync(Pageable.CreateBlocking<Envelope, int>(transport, s_first, new EnvelopeSerde(), strategy ?? Cursor()))
            : Pageable.Create<Envelope, int>(transport, s_first, new EnvelopeSerde(), strategy ?? Cursor());

    private static async Task Drain<T>(IAsyncEnumerable<T> source)
    {
        await foreach (var element in source)
        {
            _ = element;
        }
    }

    // ── PAGE-11: closed before the first item, one exchange for one item ──────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_item_view_closes_a_page_before_delivering_any_of_its_items(bool blocking)
    {
        var events = new List<string>();
        var transport = new ScriptedTransport(
            PageFixtures.Respond(EnvelopeBody.Of([1, 2], "b", onDispose: () => events.Add("close:0"))),
            PageFixtures.Respond(EnvelopeBody.Of([3, 4], onDispose: () => events.Add("close:1"))));

        await foreach (var item in Items(blocking, transport))
        {
            events.Add($"item:{item}");
        }

        // Stronger than the letter of PAGE-11: even the first item of a page is delivered after the page's close.
        Assert.Equal(["close:0", "item:1", "item:2", "close:1", "item:3", "item:4"], events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Taking_one_item_and_stopping_closes_that_page_and_fetches_no_second_page(bool blocking)
    {
        var body = EnvelopeBody.Of([1, 2], "b");
        var transport = new ScriptedTransport(PageFixtures.Respond(body), PageFixtures.Respond([3]));

        await foreach (var item in Items(blocking, transport))
        {
            _ = item;
            break;
        }

        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(1, transport.CallCount);
    }

    // ── PAGE-12: nothing is open at a yield, so abandonment leaks nothing ─────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hand_driven_enumerator_dropped_without_disposal_leaves_every_fetched_body_closed(bool blocking)
    {
        var first = EnvelopeBody.Of([1], "b");
        var second = EnvelopeBody.Of([2], "c");
        var transport = new ScriptedTransport(PageFixtures.Respond(first), PageFixtures.Respond(second), PageFixtures.Respond([3]));
        var enumerator = Pages(blocking, transport).GetAsyncEnumerator(Ct);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.True(await enumerator.MoveNextAsync());

        // The enumerator is deliberately never disposed: disposed count equals fetched count.
        Assert.Equal(2, transport.CallCount);
        Assert.Equal((1, 1), (first.DisposeCount, second.DisposeCount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Breaking_out_of_the_page_view_and_draining_it_close_every_fetched_page(bool blocking)
    {
        var bodies = new[] { EnvelopeBody.Of([1], "b"), EnvelopeBody.Of([2]) };
        var transport = new ScriptedTransport(PageFixtures.Respond(bodies[0]), PageFixtures.Respond(bodies[1]));

        await foreach (var page in Pages(blocking, transport))
        {
            _ = page;
            break;
        }

        Assert.Equal((1, 0), (bodies[0].DisposeCount, bodies[1].DisposeCount));

        var drained = new[] { EnvelopeBody.Of([1], "b"), EnvelopeBody.Of([2]) };
        await Drain(Pages(blocking, new ScriptedTransport(PageFixtures.Respond(drained[0]), PageFixtures.Respond(drained[1]))));

        Assert.Equal((1, 1), (drained[0].DisposeCount, drained[1].DisposeCount));
    }

    // ── PAGE-13: a parse failure closes inline, the parse error stays primary ─────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_parse_failure_closes_the_response_and_a_release_failure_lands_on_the_trail(bool blocking)
    {
        var body = EnvelopeBody.Of([1], disposeFailure: new IOException("close failed"));
        var transport = new ScriptedTransport(PageFixtures.Respond(body));

        var ex = await Assert.ThrowsAsync<SentinelException>(async () =>
            await Drain(Pages(blocking, transport, new ThrowingStrategy(new SentinelException("malformed page")))));

        Assert.Equal(1, body.DisposeCount);
        Assert.Contains(ExceptionTrail.GetSuppressed(ex), s => s is IOException { Message: "close failed" });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_parse_failure_on_page_two_does_not_close_page_one_again(bool blocking)
    {
        var first = EnvelopeBody.Of([1], "b");
        var second = EnvelopeBody.Of([2]);
        var transport = new ScriptedTransport(PageFixtures.Respond(first), PageFixtures.Respond(second));
        var failOnSecond = PaginationStrategies.Create<Envelope, int>(
            e => e.Items,
            (page, _, template) => page.Next is null ? throw new SentinelException("page two") : template.WithUrl(new Uri("https://api.example/items?cursor=b")));

        await Assert.ThrowsAsync<SentinelException>(async () => await Drain(Pages(blocking, transport, failOnSecond)));

        Assert.Equal((1, 1), (first.DisposeCount, second.DisposeCount));
    }

    // ── PAGE-15 and PAGE-32: a success-path release failure ends the walk ─────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_success_path_release_failure_surfaces_unwrapped_and_the_walk_ends(bool blocking)
    {
        var body = EnvelopeBody.Of([1], "b", new IOException("close failed"));
        var transport = new ScriptedTransport(PageFixtures.Respond(body), PageFixtures.Respond([2]));
        var enumerator = Pages(blocking, transport).GetAsyncEnumerator(Ct);

        var ex = await Assert.ThrowsAsync<IOException>(async () => await enumerator.MoveNextAsync());

        Assert.Equal("close failed", ex.Message);
        Assert.False(await enumerator.MoveNextAsync());
        Assert.Equal(1, transport.CallCount);
    }

    // ── PAGE-27: exactly one release on every path ────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Every_response_is_released_on_every_path(bool blocking)
    {
        // success, early break, consumer throw, parse failure, null envelope, null PageInfo: fetched == released.
        var matrix = new List<(string Path, EnvelopeBody[] Bodies, Func<ScriptedTransport, Task> Run)>();
        EnvelopeBody[] Two() => [EnvelopeBody.Of([1], "b"), EnvelopeBody.Of([2])];

        var drainBodies = Two();
        matrix.Add(("drain", drainBodies, t => Drain(Items(blocking, t))));
        var breakBodies = Two();
        matrix.Add(("break", breakBodies, async t =>
        {
            await foreach (var item in Items(blocking, t))
            {
                _ = item;
                break;
            }
        }));
        var consumerBodies = Two();
        matrix.Add(("consumer throws", consumerBodies, async t =>
        {
            await Assert.ThrowsAsync<ConsumerException>(async () =>
            {
                await foreach (var item in Items(blocking, t))
                {
                    _ = item;
                    throw new ConsumerException("consumer blew up");
                }
            });
        }));
        var parseBodies = Two();
        matrix.Add(("parse failure", parseBodies, async t =>
            await Assert.ThrowsAsync<SentinelException>(async () => await Drain(Items(blocking, t, new ThrowingStrategy(new SentinelException("parse")))))));
        var nullEnvelopeBodies = new[] { new EnvelopeBody(System.Text.Encoding.UTF8.GetBytes("null")) };
        matrix.Add(("null envelope", nullEnvelopeBodies, async t =>
            await Assert.ThrowsAsync<DeserializationException>(async () => await Drain(Items(blocking, t)))));
        var nullInfoBodies = Two();
        matrix.Add(("null PageInfo", nullInfoBodies, async t =>
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Drain(Items(blocking, t, new NullReturningStrategy())))));

        var expected = new Dictionary<string, int[]>
        {
            ["drain"] = [1, 1],
            ["break"] = [1, 0],
            ["consumer throws"] = [1, 0],
            ["parse failure"] = [1, 0],
            ["null envelope"] = [1],
            ["null PageInfo"] = [1, 0],
        };

        foreach (var (path, bodies, run) in matrix)
        {
            var transport = new ScriptedTransport(bodies.Select(b => PageFixtures.Respond(b)));
            await run(transport);
            Assert.True(expected[path].SequenceEqual(bodies.Select(b => b.DisposeCount)), $"{path}: disposed {string.Join(",", bodies.Select(b => b.DisposeCount))}");
            Assert.True(bodies.Count(b => b.DisposeCount > 0) == transport.CallCount, $"{path}: fetched != released");
        }
    }

    // ── PAGE-28: the cause surfaces as itself ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_transport_fault_surfaces_as_the_original_type(bool blocking)
    {
        var transport = new ScriptedTransport(new IOException("connection reset"));

        var ex = await Assert.ThrowsAsync<IOException>(async () => await Drain(Items(blocking, transport)));

        Assert.Equal("connection reset", ex.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_eager_throw_from_the_transport_surfaces_as_the_original_type(bool blocking)
    {
        var transport = new ScriptedTransport((Func<Request, Response>)(_ => throw new TimeoutException("eager")));

        await Assert.ThrowsAsync<TimeoutException>(async () => await Drain(Items(blocking, transport)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_null_response_is_an_InvalidOperationException_naming_the_client(bool blocking)
    {
        var transport = new ScriptedTransport((Func<Request, Response>)(_ => null!));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Drain(Items(blocking, transport)));

        Assert.Contains(nameof(ScriptedTransport), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_consumer_throw_inside_the_loop_surfaces_as_itself_with_the_response_already_closed(bool blocking)
    {
        var body = EnvelopeBody.Of([1, 2]);
        var transport = new ScriptedTransport(PageFixtures.Respond(body));

        var ex = await Assert.ThrowsAsync<ConsumerException>(async () =>
        {
            await foreach (var item in Items(blocking, transport))
            {
                Assert.Equal(1, body.DisposeCount);
                throw new ConsumerException($"consumer rejected {item}");
            }
        });

        Assert.Empty(ExceptionTrail.GetSuppressed(ex));
    }

    // ── PAGE-4 ─────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_null_PageInfo_closes_the_response_and_names_the_strategy_type(bool blocking)
    {
        var body = EnvelopeBody.Of([1], disposeFailure: new IOException("close failed"));
        var transport = new ScriptedTransport(PageFixtures.Respond(body));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Drain(Pages(blocking, transport, new NullReturningStrategy())));

        Assert.Contains(nameof(NullReturningStrategy), ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);
        Assert.Contains(ExceptionTrail.GetSuppressed(ex), s => s is IOException { Message: "close failed" });
    }

    // ── PAGE-33: a response delivered after the cancel is discarded ───────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_late_response_after_cancel_is_discarded_disposed_once_and_delivers_no_item(bool blocking)
    {
        using var cts = new CancellationTokenSource();
        var body = EnvelopeBody.Of([1]);
        var transport = new ScriptedTransport((Func<Request, Response>)(request =>
        {
            cts.Cancel();
            return TestResponses.Create(Status.Ok, request, body: body);
        }));
        var delivered = new List<Page<int>>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var page in Pages(blocking, transport, token: cts.Token))
            {
                delivered.Add(page);
            }
        });

        Assert.Empty(delivered);
        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(1, transport.CallCount);
    }

    // ── PAGE-14 at the iterator level ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_second_pass_over_the_same_page_view_throws_and_sends_nothing()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1]));
        var view = Pageable.Create<Envelope, int>(transport, s_first, new EnvelopeSerde(), Cursor()).AsPages();

        await Drain(view);
        var sends = transport.CallCount;

        Assert.Throws<InvalidOperationException>(() => view.GetAsyncEnumerator(Ct));
        Assert.Equal(sends, transport.CallCount);
    }
}
