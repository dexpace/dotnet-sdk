// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The blocking twin of PageableTests: the same rows over Pageable.CreateBlocking and the synchronous seam. There is no
// Node counterpart (the Node SDK has no blocking pager), so the cases come from the PAGE-n rows themselves and from
// PageableTests. A transport that fails if its asynchronous member is called (SyncFirstTransport) proves the blocking path
// never takes it.

#pragma warning disable CA2000 // The transports and bodies under test are released by the walk under test; the tests read their counters.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-1, PAGE-6, PAGE-7, PAGE-9, PAGE-14, PAGE-31 and PAGE-36 on the blocking path (design P7c-4, P7c-5).</summary>
[Trait("Category", "Unit")]
public sealed class BlockingPageableTests
{
    private static readonly Request s_first = Request.Get("https://api.example/items");

    private static Pageable<int> Make(
        IHttpClient client,
        int? maxPages = null,
        RequestOptions? options = null,
        IPageStrategy<Envelope, int>? strategy = null,
        CancellationToken? token = null) =>
        Pageable.CreateBlocking<Envelope, int>(
            client,
            s_first,
            new EnvelopeSerde(),
            strategy ?? PaginationStrategies.Cursor<Envelope, int>(e => e.Items, e => e.Next),
            options,
            maxPages,
            token ?? TestContext.Current.CancellationToken);

    // A server of `count` pages: the cursor is the zero-based page number, page n holds [n], the last has no next.
    private static Func<Request, Response> Numbered(int count) =>
        request =>
        {
            var cursor = QuerySplice.Get(request.Url, "cursor");
            var n = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
            return PageFixtures.Respond([n], n + 1 < count ? (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : null)(request);
        };

    [Fact]
    public void The_blocking_walk_uses_Execute_never_ExecuteAsync_and_flattens_in_order()
    {
        var transport = new SyncFirstTransport(r => PageFixtures.Respond([1, 2], r.Url.Query.Contains("cursor", StringComparison.Ordinal) ? null : "b")(r));

        var pageable = Make(transport);

        Assert.Equal([1, 2, 1, 2], pageable.ToArray());
        Assert.Equal(2, transport.CallCount);
    }

    // ── PAGE-6 ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Creating_the_pageable_asking_for_pages_and_obtaining_an_enumerator_send_nothing()
    {
        var transport = new RecordingSyncTransport(Numbered(2));

        var pageable = Make(transport);
        var view = pageable.AsPages();
        using var itemEnumerator = pageable.GetEnumerator();
        using var pageEnumerator = view.GetEnumerator();
        Assert.Equal(0, transport.CallCount);

        Assert.True(itemEnumerator.MoveNext());
        Assert.Equal(1, transport.CallCount);
    }

    // ── PAGE-1, PAGE-7, PAGE-9 ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_page_view_yields_each_page_with_its_own_status_headers_and_sent_request()
    {
        Func<Request, Response> Page(int number, string? next) =>
            PageFixtures.Respond([number], next, new Headers.Builder().Add("X-Page", number.ToString(System.Globalization.CultureInfo.InvariantCulture)).Build());
        var transport = new RecordingSyncTransport(r => QuerySplice.Get(r.Url, "cursor") switch
        {
            null => Page(1, "b")(r),
            "b" => Page(2, "c")(r),
            _ => Page(3, null)(r),
        });

        var pages = Make(transport).AsPages().ToList();

        Assert.Equal(["1", "2", "3"], pages.Select(p => p.Headers.Get("X-Page")));
        Assert.All(pages, p => Assert.Equal(Status.Ok, p.Status));
        Assert.Equal(["", "?cursor=b", "?cursor=c"], pages.Select(p => p.Request.Url.Query));
        Assert.Equal([1, 2, 3], pages.SelectMany(p => p.Values));
    }

    [Fact]
    public void After_the_last_page_further_advances_return_false_and_send_nothing()
    {
        var transport = new RecordingSyncTransport(Numbered(2));
        using var enumerator = Make(transport).GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public void An_empty_page_with_a_next_cursor_costs_one_exchange_and_the_walk_continues()
    {
        var transport = new SyncFirstTransport(r => QuerySplice.Get(r.Url, "cursor") is null ? PageFixtures.Respond([], "b")(r) : PageFixtures.Respond([7])(r));

        var pages = Make(transport).AsPages().ToList();

        Assert.Equal([0, 1], pages.Select(p => p.Values.Count));
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public void The_cap_stops_a_server_that_never_ends_and_a_cap_of_zero_or_less_is_rejected()
    {
        var transport = new RecordingSyncTransport(PageFixtures.Respond([9], "again"));

        Assert.Equal([9, 9, 9], Make(transport, maxPages: 3).ToArray());
        Assert.Equal(3, transport.CallCount);
        Assert.Equal("maxPages", Assert.Throws<ArgumentOutOfRangeException>(() => Make(transport, maxPages: 0)).ParamName);
        Assert.Equal("maxPages", Assert.Throws<ArgumentOutOfRangeException>(() => Make(transport, maxPages: -1)).ParamName);
    }

    // ── PAGE-14 and PAGE-8 ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_second_GetEnumerator_on_one_page_view_throws_and_the_item_view_re_enumerates()
    {
        var transport = new RecordingSyncTransport(Numbered(1));
        var pageable = Make(transport);
        var view = pageable.AsPages();
        using var first = view.GetEnumerator();

        Assert.Throws<InvalidOperationException>(() => view.GetEnumerator());
        Assert.Equal([0], pageable.ToArray());
        Assert.Equal([0], pageable.ToArray());
        Assert.Equal(2, transport.CallCount);
    }

    // ── PAGE-31 ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ten_thousand_synchronously_completed_pages_do_not_overflow_the_stack()
    {
        var transport = new RecordingSyncTransport(Numbered(10_000));
        var count = 0;
        long sum = 0;

        foreach (var item in Make(transport))
        {
            count++;
            sum += item;
        }

        Assert.Equal(10_000, count);
        Assert.Equal(49_995_000L, sum);
    }

    // ── the token the factory captured ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_pre_cancelled_token_throws_from_the_first_MoveNext_with_no_exchange()
    {
        var transport = new RecordingSyncTransport(Numbered(2));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var enumerator = Make(transport, token: cts.Token).GetEnumerator();

        Assert.Throws<OperationCanceledException>(() => enumerator.MoveNext());
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public void A_token_cancelled_mid_walk_stops_before_the_next_page_and_reaches_every_exchange()
    {
        using var cts = new CancellationTokenSource();
        var transport = new RecordingSyncTransport(Numbered(5));
        var seen = new List<int>();

        Assert.Throws<OperationCanceledException>(() =>
        {
            foreach (var item in Make(transport, token: cts.Token))
            {
                seen.Add(item);
                if (item == 1)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal([0, 1], seen);
        Assert.Equal(2, transport.CallCount);
        Assert.All(transport.Calls, call => Assert.Equal(cts.Token, call.CancellationToken));
    }

    // ── PAGE-36 ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_same_RequestOptions_instance_reaches_every_exchange()
    {
        var options = new RequestOptions { MaxRetries = 7 }.WithTag("page-walk", "1");
        var transport = new RecordingSyncTransport(Numbered(3));

        _ = Make(transport, options: options).ToArray();

        Assert.Equal(3, transport.Calls.Count);
        Assert.All(transport.Calls, call => Assert.Same(options, call.Options));
        var defaults = new RecordingSyncTransport(Numbered(2));
        _ = Make(defaults).ToArray();
        Assert.All(defaults.Calls, call => Assert.Same(RequestOptions.Empty, call.Options));
    }

    // ── a pipeline converts to both seams ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_pipeline_is_accepted_as_the_blocking_client()
    {
        var transport = new ScriptedTransport(PageFixtures.Respond([1], "b"), PageFixtures.Respond([2]));
        using var pipeline = new PipelineBuilder().Build(transport);

        var items = Make(pipeline).ToArray();

        Assert.Equal([1, 2], items);
        Assert.Equal(2, transport.CallCount);
    }

    // ── close and failure parity (PAGE-13, PAGE-15, PAGE-32) ──────────────────────────────────────────

    private sealed class StrategySentinelException(string message) : Exception(message);

    private sealed class ThrowingStrategy : IPageStrategy<Envelope, int>
    {
        public PageInfo<int> Parse(Envelope page, Response response, Request first) => throw new StrategySentinelException("parse failed");
    }

    [Fact]
    public void A_parse_failure_closes_the_response_once_and_the_parse_error_is_primary()
    {
        var body = EnvelopeBody.Of([1], disposeFailure: new IOException("close failed"));
        var transport = new SyncFirstTransport(PageFixtures.Respond(body));

        var ex = Assert.Throws<StrategySentinelException>(() => Make(transport, strategy: new ThrowingStrategy()).ToArray());

        Assert.Equal(1, body.DisposeCount);
        Assert.Contains(ExceptionTrail.GetSuppressed(ex), s => s is IOException { Message: "close failed" });
    }

    [Fact]
    public void A_success_path_dispose_failure_surfaces_from_MoveNext_unwrapped_and_ends_the_walk()
    {
        var body = EnvelopeBody.Of([1], "b", new IOException("close failed"));
        var transport = new SyncFirstTransport(r => QuerySplice.Get(r.Url, "cursor") is null ? PageFixtures.Respond(body)(r) : PageFixtures.Respond([2])(r));
        using var enumerator = Make(transport).GetEnumerator();

        var ex = Assert.Throws<IOException>(() => enumerator.MoveNext());

        Assert.Equal("close failed", ex.Message);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(1, transport.CallCount);
    }

    // ── the 64 MiB note ────────────────────────────────────────────────────────────────────────────────

    private sealed class HugeDeclaredBody : ResponseBody
    {
        public override MediaType? ContentType => null;

        public override long ContentLength => ResponseBody.DefaultMaxMaterializedBytes + 1;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("must not be read");

        public override Stream OpenRead(CancellationToken cancellationToken = default) => new MemoryStream([1]);
    }

    [Fact]
    public void A_page_envelope_over_the_materialisation_cap_fails_with_BodyTooLargeException()
    {
        var transport = new SyncFirstTransport(PageFixtures.Respond(new HugeDeclaredBody()));

        Assert.Throws<BodyTooLargeException>(() => Make(transport).ToArray());
    }

    // ── subclassing ────────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeBlockingPageable(params int[][] pages) : Pageable<int>
    {
        protected override IEnumerable<Page<int>> WalkPages()
        {
            foreach (var values in pages)
            {
                yield return new Page<int>(values, Status.Ok, Headers.Empty, s_first);
            }
        }
    }

    [Fact]
    public void A_test_double_gets_the_single_use_page_view_and_the_item_view_for_free()
    {
        var fake = new FakeBlockingPageable([1, 2], [3]);

        Assert.Equal([1, 2, 3], fake.ToArray());
        Assert.Equal([1, 2, 3], fake.ToArray());
        var view = fake.AsPages();
        using var enumerator = view.GetEnumerator();
        Assert.Throws<InvalidOperationException>(() => view.GetEnumerator());
    }
}
