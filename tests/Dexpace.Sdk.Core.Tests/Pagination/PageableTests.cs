// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/pagination/paginator.test.ts (9 cases plus the cap theory) and
// cancellation.test.ts (4 cases), and from the 24 facts of the pre-7c PageableTests where they are still true. No case
// was dropped as a JavaScript host fact. The Node strategy-returning-a-promise cases become the typed-envelope strategy
// (design P7c-2); Node's `signal` becomes the CancellationToken; "closed and discarded" for a page fetched while the abort
// was in flight becomes PageStepTests and PaginationLifecycleTests, because a .NET page owns no response (entry 17).

#pragma warning disable CA2000 // The transports and bodies under test are released by the walk under test; the tests read their counters.

using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Streams;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>
/// The async engine through the public surface: <see cref="Pageable.Create{TPage,T}"/>, <see cref="AsyncPageable{T}"/> and
/// <see cref="Page{T}"/> (PAGE-1, PAGE-5 to PAGE-10, PAGE-14, PAGE-25, PAGE-26, PAGE-31, PAGE-36; PIPE-26).
/// </summary>
[Trait("Category", "Unit")]
public sealed class PageableTests
{
    private static readonly Request s_first = Request.Get("https://api.example/items");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IPageStrategy<Envelope, int> CursorStrategy() =>
        PaginationStrategies.Cursor<Envelope, int>(e => e.Items, e => e.Next);

    private static AsyncPageable<int> Make(
        IAsyncHttpClient client,
        int? maxPages = null,
        RequestOptions? options = null,
        IPageStrategy<Envelope, int>? strategy = null) =>
        Pageable.Create<Envelope, int>(client, s_first, new EnvelopeSerde(), strategy ?? CursorStrategy(), options, maxPages);

    private static async Task<List<int>> ItemsOf(AsyncPageable<int> pageable)
    {
        var items = new List<int>();
        await foreach (var item in pageable.WithCancellation(Ct))
        {
            items.Add(item);
        }

        return items;
    }

    private static async Task<List<Page<int>>> PagesOf(AsyncPageable<int> pageable)
    {
        var pages = new List<Page<int>>();
        await foreach (var page in pageable.AsPages().WithCancellation(Ct))
        {
            pages.Add(page);
        }

        return pages;
    }

    // A server of `count` pages: the cursor is the zero-based page number, page n holds [n], the last has no next.
    private static Func<Request, Response> Numbered(int count) =>
        request =>
        {
            var cursor = QuerySplice.Get(request.Url, "cursor");
            var n = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
            return PageFixtures.Respond([n], n + 1 < count ? (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : null)(request);
        };

    // ── PAGE-1: two views of one walk ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_item_view_flattens_pages_in_server_order()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1, 2], "b"), PageFixtures.Respond([3], "c"), PageFixtures.Respond([4, 5]));

        var items = await ItemsOf(Make(transport));

        Assert.Equal([1, 2, 3, 4, 5], items);
        Assert.Equal(3, transport.CallCount);
    }

    [Fact]
    public async Task The_page_view_yields_each_page_with_its_own_status_headers_and_sent_request()
    {
        Func<Request, Response> Page(int number, string? next) =>
            PageFixtures.Respond([number], next, new Headers.Builder().Add("X-Page", number.ToString(System.Globalization.CultureInfo.InvariantCulture)).Build());
        var transport = new ScriptedTransport(Page(1, "b"), Page(2, "c"), Page(3, null));

        var pages = await PagesOf(Make(transport));

        Assert.Equal(3, pages.Count);
        Assert.Equal(["1", "2", "3"], pages.Select(p => p.Headers.Get("X-Page")));
        Assert.All(pages, p => Assert.Equal(Status.Ok, p.Status));
        Assert.Equal(["", "?cursor=b", "?cursor=c"], pages.Select(p => p.Request.Url.Query));
        Assert.Equal([[1], [2], [3]], pages.Select(p => p.Values));
    }

    [Fact]
    public async Task The_item_view_and_the_flattened_page_view_see_identical_items()
    {
        var transport = new RecordingTransport(Numbered(4));

        var viaItems = await ItemsOf(Make(transport));
        var viaPages = (await PagesOf(Make(transport))).SelectMany(p => p.Values).ToList();

        Assert.Equal([0, 1, 2, 3], viaItems);
        Assert.Equal(viaItems, viaPages);
    }

    [Fact]
    public async Task A_page_keeps_its_values_after_the_walk_has_moved_on()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1, 2], "b"), PageFixtures.Respond([3]));

        var pages = await PagesOf(Make(transport));

        // The pages are read after the walk finished and every response was closed.
        Assert.Equal([1, 2], pages[0].Values);
        Assert.Equal([3], pages[1].Values);
    }

    // ── PAGE-6: lazy, one exchange per page ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creating_the_pageable_asking_for_pages_and_obtaining_an_enumerator_send_nothing()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1], "b"), PageFixtures.Respond([2]));

        var pageable = Make(transport);
        var view = pageable.AsPages();
        await using var itemEnumerator = pageable.GetAsyncEnumerator(Ct);
        await using var pageEnumerator = view.GetAsyncEnumerator(Ct);
        Assert.Equal(0, transport.CallCount);

        Assert.True(await itemEnumerator.MoveNextAsync());
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task Advancing_to_the_second_page_triggers_the_second_exchange_and_not_before()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1], "b"), PageFixtures.Respond([2]));
        await using var enumerator = Make(transport).AsPages().GetAsyncEnumerator(Ct);

        await enumerator.MoveNextAsync();
        Assert.Equal(1, transport.CallCount);

        await enumerator.MoveNextAsync();
        Assert.Equal(2, transport.CallCount);
    }

    // ── PAGE-7: forward-only, idempotent end ───────────────────────────────────────────────────────────

    [Fact]
    public async Task After_the_last_page_further_advances_return_false_and_send_nothing()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1], "b"), PageFixtures.Respond([2]));
        await using var enumerator = Make(transport).GetAsyncEnumerator(Ct);
        while (await enumerator.MoveNextAsync())
        {
        }

        Assert.False(await enumerator.MoveNextAsync());
        Assert.False(await enumerator.MoveNextAsync());
        Assert.False(await enumerator.MoveNextAsync());
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task An_empty_page_with_a_next_cursor_costs_one_exchange_and_the_walk_continues()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([], "b"), PageFixtures.Respond([7]));

        var pages = await PagesOf(Make(transport));

        Assert.Equal(2, transport.CallCount);
        Assert.Equal([0, 1], pages.Select(p => p.Values.Count));
    }

    // ── PAGE-8: every enumeration is a fresh walk from the first request ──────────────────────────────

    [Fact]
    public async Task Each_enumeration_restarts_from_the_first_request()
    {
        var transport = new RecordingTransport(Numbered(2));
        var pageable = Make(transport);

        var first = await ItemsOf(pageable);
        var second = await ItemsOf(pageable);

        Assert.Equal([0, 1], first);
        Assert.Equal([0, 1], second);
        Assert.Equal(4, transport.CallCount);
        Assert.Equal(string.Empty, transport.Calls[2].Request.Url.Query);
    }

    [Fact]
    public async Task Two_concurrent_walks_of_one_pageable_are_independent()
    {
        var transport = new RecordingTransport(Numbered(5));
        var pageable = Make(transport);

        var walks = await Task.WhenAll(ItemsOf(pageable), ItemsOf(pageable));

        Assert.Equal([0, 1, 2, 3, 4], walks[0]);
        Assert.Equal([0, 1, 2, 3, 4], walks[1]);
        Assert.Equal(10, transport.CallCount);
    }

    // ── PAGE-5: a strategy holds no per-walk state ─────────────────────────────────────────────────────

    [Fact]
    public async Task One_strategy_instance_serves_two_pageables_walked_at_once()
    {
        var strategy = CursorStrategy();
        var left = new RecordingTransport(Numbered(3));
        var right = new RecordingTransport(Numbered(6));

        var walks = await Task.WhenAll(ItemsOf(Make(left, strategy: strategy)), ItemsOf(Make(right, strategy: strategy)));

        Assert.Equal([0, 1, 2], walks[0]);
        Assert.Equal([0, 1, 2, 3, 4, 5], walks[1]);
        Assert.Equal(["", "?cursor=1", "?cursor=2"], left.Requests.Select(r => r.Url.Query));
    }

    // ── PAGE-9 and PAGE-10: the cap ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_cap_stops_a_server_that_never_ends_at_exactly_n_exchanges()
    {
        var transport = new RecordingTransport(PageFixtures.Respond([9], "again"));

        var items = await ItemsOf(Make(transport, maxPages: 4));

        Assert.Equal(4, transport.CallCount);
        Assert.Equal([9, 9, 9, 9], items);
    }

    [Fact]
    public async Task A_cap_stops_the_walk_even_when_the_next_cursor_is_non_null()
    {
        var transport = new ScriptedTransport(
            PageFixtures.Respond([0], "b"), PageFixtures.Respond([1], "c"), PageFixtures.Respond([2], "d"), PageFixtures.Respond([3], "e"), PageFixtures.Respond([4]));

        var items = await ItemsOf(Make(transport, maxPages: 2));

        Assert.Equal(2, transport.CallCount);
        Assert.Equal([0, 1], items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_cap_of_zero_or_less_is_rejected_at_construction_not_lazily(int maxPages)
    {
        var transport = new ScriptedTransport();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Make(transport, maxPages: maxPages));

        Assert.Equal("maxPages", ex.ParamName);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task With_no_cap_a_thousand_pages_are_walked()
    {
        var transport = new RecordingTransport(Numbered(1_000));

        var items = await ItemsOf(Make(transport, maxPages: null));

        Assert.Equal(1_000, items.Count);
        Assert.Equal(1_000, transport.CallCount);
    }

    // ── PAGE-14: the page view is single-use, the item view is not ────────────────────────────────────

    [Fact]
    public async Task The_second_GetAsyncEnumerator_on_one_page_view_throws()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1]));
        var view = Make(transport).AsPages();
        await using var first = view.GetAsyncEnumerator(Ct);

        var ex = Assert.Throws<InvalidOperationException>(() => view.GetAsyncEnumerator(Ct));

        Assert.Contains("single-use", ex.Message, StringComparison.Ordinal);
        Assert.True(await first.MoveNextAsync());
    }

    [Fact]
    public async Task Two_AsPages_calls_are_two_independent_single_use_views()
    {
        var transport = new RecordingTransport(Numbered(2));
        var pageable = Make(transport);

        var one = await Task.Run(async () => (await PagesOf(pageable)).Count, Ct);
        var two = await Task.Run(async () => (await PagesOf(pageable)).Count, Ct);

        Assert.Equal((2, 2), (one, two));
    }

    [Fact]
    public async Task The_item_views_GetAsyncEnumerator_may_be_called_repeatedly()
    {
        var transport = new RecordingTransport(Numbered(1));
        var pageable = Make(transport);

        await using var a = pageable.GetAsyncEnumerator(Ct);
        await using var b = pageable.GetAsyncEnumerator(Ct);

        Assert.True(await a.MoveNextAsync());
        Assert.True(await b.MoveNextAsync());
    }

    // ── PAGE-25 and PAGE-26: cancellation ──────────────────────────────────────────────────────────────

    // A transport whose response never arrives until the token fires, so the cancel is observed in flight.
    private sealed class TokenBlockedTransport : IAsyncHttpClient
    {
        private int _calls;

        public int CallCount => Volatile.Read(ref _calls);

        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            var tcs = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() =>
            {
                Observed.TrySetResult();
                tcs.TrySetCanceled(cancellationToken);
            });
            return tcs.Task;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Cancelling_mid_walk_is_observed_by_the_in_flight_exchange_and_no_further_exchange_happens()
    {
        using var cts = new CancellationTokenSource();
        var transport = new TokenBlockedTransport();
        await using var enumerator = Make(transport).GetAsyncEnumerator(cts.Token);

        var pending = enumerator.MoveNextAsync().AsTask();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await transport.Observed.Task;
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task The_consumers_token_reaches_every_exchange()
    {
        using var cts = new CancellationTokenSource();
        var transport = new RecordingTransport(Numbered(3));
        var items = new List<int>();

        await foreach (var item in Make(transport).WithCancellation(cts.Token))
        {
            items.Add(item);
        }

        Assert.Equal(3, transport.CallCount);
        Assert.All(transport.Calls, call => Assert.Equal(cts.Token, call.CancellationToken));
    }

    [Fact]
    public async Task Cancelling_between_items_of_a_fetched_page_does_not_drop_the_rest_of_that_page()
    {
        using var cts = new CancellationTokenSource();
        var transport = new ScriptedTransport(PageFixtures.Respond([1, 2, 3], "b"), PageFixtures.Respond([4]));
        var seen = new List<int>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in Make(transport).WithCancellation(cts.Token))
            {
                seen.Add(item);
                if (item == 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        // PAGE-26: the token is observed at the next page, so the fetched page is delivered whole and page 2 never starts.
        Assert.Equal([1, 2, 3], seen);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task An_already_cancelled_token_throws_before_any_exchange_for_both_views()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1]));
        var pageable = Make(transport);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in pageable.WithCancellation(cts.Token))
            {
                _ = item;
            }
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var page in pageable.AsPages().WithCancellation(cts.Token))
            {
                _ = page;
            }
        });

        Assert.Equal(0, transport.CallCount);
    }

    // ── PAGE-31: no recursion per page ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ten_thousand_synchronously_completed_pages_do_not_overflow_the_stack()
    {
        var transport = new RecordingTransport(Numbered(10_000));
        var count = 0;
        long sum = 0;

        await foreach (var item in Make(transport).WithCancellation(Ct))
        {
            count++;
            sum += item;
        }

        Assert.Equal(10_000, count);
        Assert.Equal(49_995_000L, sum);
    }

    // ── PAGE-36: the same options instance reaches every exchange ─────────────────────────────────────

    [Fact]
    public async Task A_custom_RequestOptions_reaches_every_page_as_the_same_instance()
    {
        // Deliberately not RequestOptions.Empty: a bug that honours the options on page one and falls back to the default
        // afterwards would be invisible against the default (Node paginator.test.ts, PAGE-36).
        var options = new RequestOptions { MaxRetries = 7 }.WithTag("page-walk", "1");
        var transport = new RecordingTransport(Numbered(3));

        await ItemsOf(Make(transport, options: options));

        Assert.Equal(3, transport.Calls.Count);
        Assert.All(transport.Calls, call => Assert.Same(options, call.Options));
    }

    [Fact]
    public async Task Omitted_options_mean_RequestOptions_Empty_on_every_page()
    {
        var transport = new RecordingTransport(Numbered(2));

        await ItemsOf(Make(transport));

        Assert.All(transport.Calls, call => Assert.Same(RequestOptions.Empty, call.Options));
    }

    // ── PIPE-26: a pipeline backs a paginator ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pipeline_backing_a_paginator_works_as_the_client()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1], "b"), PageFixtures.Respond([2]));
        using var pipeline = new PipelineBuilder().Build(transport);

        var items = await ItemsOf(Make(pipeline));

        Assert.Equal([1, 2], items);
        Assert.Equal(2, transport.CallCount);
    }

    // ── early exit (PAGE-11) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EarlyBreak_FirstBodyDisposed_AndTransportCalledOnce()
    {
        var body = EnvelopeBody.Of([1, 2], "b");
        var transport = new ScriptedTransport(PageFixtures.Respond(body), PageFixtures.Respond([3]));

        await foreach (var item in Make(transport).WithCancellation(Ct))
        {
            _ = item;
            break;
        }

        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(1, transport.CallCount);
    }

    // ── construction guards ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_rejects_a_null_client_first_serde_or_strategy()
    {
        var transport = new ScriptedTransport();
        var serde = new EnvelopeSerde();
        var strategy = CursorStrategy();

        Assert.Throws<ArgumentNullException>(() => Pageable.Create<Envelope, int>(null!, s_first, serde, strategy));
        Assert.Throws<ArgumentNullException>(() => Pageable.Create<Envelope, int>(transport, null!, serde, strategy));
        Assert.Throws<ArgumentNullException>(() => Pageable.Create<Envelope, int>(transport, s_first, null!, strategy));
        Assert.Throws<ArgumentNullException>(() => Pageable.Create<Envelope, int>(transport, s_first, serde, null!));
    }

    [Fact]
    public void A_template_with_a_single_use_body_is_rejected_at_construction()
    {
        var body = RequestBody.FromStream(new StrictReadStream(new MemoryStream([1])));
        Assert.False(body.IsReplayable);
        var template = Request.Post("https://api.example/search", body);

        var ex = Assert.Throws<ArgumentException>(() =>
            Pageable.Create<Envelope, int>(new ScriptedTransport(), template, new EnvelopeSerde(), CursorStrategy()));

        Assert.Equal("first", ex.ParamName);
        Assert.Contains("ToReplayableAsync", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_replayable_template_body_is_re_sent_intact_on_every_page()
    {
        var template = Request.Post("https://api.example/search", RequestBody.FromString("{\"q\":1}"));
        var transport = new RecordingTransport(Numbered(3));

        await ItemsOf(Pageable.Create<Envelope, int>(transport, template, new EnvelopeSerde(), CursorStrategy()));

        Assert.Equal(3, transport.Calls.Count);
        foreach (var call in transport.Calls)
        {
            Assert.Equal(Method.Post, call.Request.Method);
            using var written = new MemoryStream();
            await call.Request.Body!.WriteToAsync(written, Ct);
            Assert.Equal("{\"q\":1}", System.Text.Encoding.UTF8.GetString(written.ToArray()));
        }
    }

    // ── subclassing (design Q1) ────────────────────────────────────────────────────────────────────────

    private sealed class FakePageable(params int[][] pages) : AsyncPageable<int>
    {
        protected override async IAsyncEnumerable<Page<int>> WalkPagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var values in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return new Page<int>(values, Status.Ok, Headers.Empty, s_first);
            }
        }
    }

    // Not an iterator and no [EnumeratorCancellation]: it only sees the token through the argument.
    private sealed class PlainMethodPageable : AsyncPageable<int>
    {
        public CancellationToken Seen { get; private set; }

        protected override IAsyncEnumerable<Page<int>> WalkPagesAsync(CancellationToken cancellationToken)
        {
            Seen = cancellationToken;
            return Walk();

            static async IAsyncEnumerable<Page<int>> Walk()
            {
                await Task.Yield();
                yield return new Page<int>([1], Status.Ok, Headers.Empty, s_first);
            }
        }
    }

    [Fact]
    public async Task A_test_double_gets_the_single_use_page_view_and_the_item_view_for_free()
    {
        var fake = new FakePageable([1, 2], [3]);

        Assert.Equal([1, 2, 3], await ItemsOf(fake));
        Assert.Equal([1, 2, 3], await ItemsOf(fake));

        var view = fake.AsPages();
        await using var enumerator = view.GetAsyncEnumerator(Ct);
        Assert.Throws<InvalidOperationException>(() => view.GetAsyncEnumerator(Ct));
    }

    [Fact]
    public async Task WithCancellation_reaches_a_subclass_whose_walk_is_a_plain_method()
    {
        using var cts = new CancellationTokenSource();
        var plain = new PlainMethodPageable();

        await foreach (var page in plain.AsPages().WithCancellation(cts.Token))
        {
            _ = page;
        }

        Assert.Equal(cts.Token, plain.Seen);
    }
}
