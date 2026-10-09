// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/pagination/fetchers.test.ts (the 12 cases). No case was dropped as a
// JavaScript host fact. Divergences, kept as the .NET expectation: Node's pages own their response and the engine closes
// them as the consumer advances; a .NET page owns none (design section 10 entry 17), so the "pages are closed" cases become
// the ownership rule of P7c-15, a fetcher disposes its own response, and the "close failure while advancing" case has no
// counterpart. Node's `maxPages` theory over 1.5 and NaN is an int here.

#pragma warning disable CA2000 // The responses and bodies under test are released by the fetchers under test; the tests read their counters.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-34 and PAGE-35 (and PAGE-8, PAGE-9, PAGE-14 for fetchers): <see cref="Pageable.FromFetchers{T}"/>.</summary>
[Trait("Category", "Unit")]
public sealed class FetcherPageableTests
{
    private static readonly Request s_request = Request.Get("https://api.example/");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FetchedPage<int> Fetched(IReadOnlyList<int> values, string? link = null, string? token = null) =>
        new(new Page<int>(values, Status.Ok, Headers.Empty, s_request), link, token);

    private static ValueTask<FetchedPage<int>?> Result(FetchedPage<int>? page) => new(page);

    private static async Task<List<int>> ItemsOf(AsyncPageable<int> pageable)
    {
        var items = new List<int>();
        await foreach (var item in pageable.WithCancellation(Ct))
        {
            items.Add(item);
        }

        return items;
    }

    // ── PAGE-34: keys ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_next_link_wins_over_the_continuation_token_and_both_are_written_to_the_options()
    {
        var seen = new List<(string Key, string? Link, string? Token)>();
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => Result(Fetched([1], "https://api.example/p2", "tok")),
            (key, o, ct) =>
            {
                seen.Add((key, o.NextLink, o.ContinuationToken));
                return Result(Fetched([2]));
            });

        var items = await ItemsOf(pageable);

        Assert.Equal([1, 2], items);
        Assert.Equal([("https://api.example/p2", "https://api.example/p2", "tok")], seen);
    }

    [Fact]
    public async Task The_token_is_used_only_when_there_is_no_link_and_the_first_fetcher_runs_exactly_once()
    {
        var firstCalls = 0;
        var keys = new List<string>();
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) =>
            {
                firstCalls++;
                return Result(Fetched([1], "/p2", "tok-2"));
            },
            (key, o, ct) =>
            {
                keys.Add(key);
                return Result(keys.Count == 1 ? Fetched([2], token: "tok-3") : null);
            });

        var items = await ItemsOf(pageable);

        Assert.Equal([1, 2], items);
        Assert.Equal(["/p2", "tok-3"], keys);
        Assert.Equal(1, firstCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_blank_link_with_no_token_ends_the_stream(string? link)
    {
        var nextCalls = 0;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => Result(Fetched([1], link)),
            (key, o, ct) =>
            {
                nextCalls++;
                return Result(Fetched([2]));
            });

        Assert.Equal([1], await ItemsOf(pageable));
        Assert.Equal(0, nextCalls);
    }

    [Fact]
    public async Task A_blank_link_falls_back_to_the_token()
    {
        var keys = new List<string>();
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => Result(Fetched([1], "  ", "tok")),
            (key, o, ct) =>
            {
                keys.Add(key);
                return Result(null);
            });

        await ItemsOf(pageable);

        Assert.Equal(["tok"], keys);
    }

    [Fact]
    public async Task A_null_first_page_is_an_empty_stream_and_a_null_next_page_ends_the_stream()
    {
        var empty = Pageable.FromFetchers<int>((o, ct) => Result(null), (key, o, ct) => Result(null));
        var ends = Pageable.FromFetchers<int>((o, ct) => Result(Fetched([1], "/p2")), (key, o, ct) => Result(null));

        Assert.Empty(await ItemsOf(empty));
        Assert.Equal([1], await ItemsOf(ends));
    }

    [Fact]
    public void A_FetchedPage_with_a_null_page_is_rejected_at_construction_and_by_with()
    {
        Assert.Throws<ArgumentNullException>(() => new FetchedPage<int>(null!));
        var valid = Fetched([1]);
        Assert.Throws<ArgumentNullException>(() => valid with { Page = null! });
    }

    [Fact]
    public void FetchedPage_is_a_record_with_deconstruction_and_with()
    {
        var page = Fetched([1], "/p2", "tok");
        var (inner, link, token) = page;

        Assert.Same(page.Page, inner);
        Assert.Equal(("/p2", "tok"), (link, token));
        var changed = page with { NextLink = "/p3" };
        Assert.Equal("/p3", changed.NextLink);
        Assert.Same(page.Page, changed.Page);
        Assert.Equal(page with { }, page);
    }

    // ── PAGE-35: one PagingOptions per walk ────────────────────────────────────────────────────────────

    [Fact]
    public async Task One_PagingOptions_instance_is_passed_to_every_call_of_a_walk_and_state_is_per_walk_scratch()
    {
        var received = new List<PagingOptions>();
        object? stashed = null;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) =>
            {
                received.Add(o);
                o.State["custom"] = "stashed";
                return Result(Fetched([1], "/p2"));
            },
            (key, o, ct) =>
            {
                received.Add(o);
                stashed = o.State["custom"];
                return Result(Fetched([2]));
            });

        await ItemsOf(pageable);

        Assert.Same(received[0], received[1]);
        Assert.Equal("stashed", stashed);
    }

    [Fact]
    public async Task A_second_walk_gets_a_fresh_PagingOptions_with_empty_state()
    {
        var instances = new List<PagingOptions>();
        var leaked = new List<bool>();
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) =>
            {
                instances.Add(o);
                leaked.Add(o.State.ContainsKey("walk"));
                o.State["walk"] = true;
                return Result(Fetched([1]));
            },
            (key, o, ct) => Result(null));

        await ItemsOf(pageable);
        await ItemsOf(pageable);

        Assert.Equal(2, instances.Count);
        Assert.NotSame(instances[0], instances[1]);
        Assert.Equal([false, false], leaked);
    }

    // ── PAGE-9 ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_cap_bounds_a_fetcher_pair_that_never_terminates_fetching_nothing_extra()
    {
        var nextCalls = 0;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => Result(Fetched([0], "/loop")),
            (key, o, ct) =>
            {
                nextCalls++;
                return Result(Fetched([nextCalls], "/loop"));
            },
            maxPages: 3);

        var items = await ItemsOf(pageable);

        // Three pages delivered means the next fetcher ran twice, not three times.
        Assert.Equal([0, 1, 2], items);
        Assert.Equal(2, nextCalls);
    }

    [Fact]
    public async Task A_cap_of_two_calls_the_next_fetcher_once_even_with_a_next_key()
    {
        var nextCalls = 0;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => Result(Fetched([1], "/p2")),
            (key, o, ct) =>
            {
                nextCalls++;
                return Result(Fetched([2], "/p3"));
            },
            maxPages: 2);

        await ItemsOf(pageable);

        Assert.Equal(1, nextCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_cap_of_zero_or_less_is_rejected_at_construction(int maxPages)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Pageable.FromFetchers<int>((o, ct) => Result(null), (key, o, ct) => Result(null), maxPages));

        Assert.Equal("maxPages", ex.ParamName);
    }

    [Fact]
    public void Null_fetchers_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => Pageable.FromFetchers<int>(null!, (key, o, ct) => Result(null)));
        Assert.Throws<ArgumentNullException>(() => Pageable.FromFetchers<int>((o, ct) => Result(null), null!));
    }

    // ── PAGE-14, PAGE-6 ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_page_view_is_single_use_and_does_not_re_run_the_first_fetcher()
    {
        var firstCalls = 0;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) =>
            {
                firstCalls++;
                return Result(Fetched([1]));
            },
            (key, o, ct) => throw new InvalidOperationException("must not be called"));
        var view = pageable.AsPages();

        await foreach (var page in view.WithCancellation(Ct))
        {
            _ = page;
        }

        Assert.Throws<InvalidOperationException>(() => view.GetAsyncEnumerator(Ct));
        Assert.Equal(1, firstCalls);
    }

    [Fact]
    public async Task The_item_view_re_enumerates_with_a_fresh_walk()
    {
        var firstCalls = 0;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) =>
            {
                firstCalls++;
                return Result(Fetched([firstCalls]));
            },
            (key, o, ct) => Result(null));

        Assert.Equal([1], await ItemsOf(pageable));
        Assert.Equal([2], await ItemsOf(pageable));
    }

    [Fact]
    public async Task Creating_the_pageable_and_asking_for_pages_run_no_fetcher_and_the_first_MoveNextAsync_runs_the_first()
    {
        var firstCalls = 0;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) =>
            {
                firstCalls++;
                return Result(Fetched([1]));
            },
            (key, o, ct) => Result(null));

        var view = pageable.AsPages();
        await using var enumerator = view.GetAsyncEnumerator(Ct);
        Assert.Equal(0, firstCalls);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, firstCalls);
    }

    // ── cancellation ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_token_reaches_both_fetchers_and_a_cancel_between_pages_stops_before_the_next_fetch()
    {
        using var cts = new CancellationTokenSource();
        var seen = new List<CancellationToken>();
        var nextCalls = 0;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) =>
            {
                seen.Add(ct);
                return Result(Fetched([1], "/p2"));
            },
            (key, o, ct) =>
            {
                seen.Add(ct);
                nextCalls++;
                return Result(Fetched([2]));
            });
        var items = new List<int>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in pageable.WithCancellation(cts.Token))
            {
                items.Add(item);
                await cts.CancelAsync();
            }
        });

        Assert.Equal([1], items);
        Assert.Equal(0, nextCalls);
        Assert.Equal(cts.Token, Assert.Single(seen));
    }

    [Fact]
    public async Task The_token_reaches_the_next_fetcher_too()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seenByNext = default;
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => Result(Fetched([1], "/p2")),
            (key, o, ct) =>
            {
                seenByNext = ct;
                return Result(null);
            });

        await foreach (var item in pageable.WithCancellation(cts.Token))
        {
            _ = item;
        }

        Assert.Equal(cts.Token, seenByNext);
    }

    // ── ownership (P7c-15) ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_fetcher_that_disposes_its_own_response_leaves_the_body_released_exactly_once()
    {
        var first = new DisposalCountingBody();
        var second = new DisposalCountingBody();
        async ValueTask<FetchedPage<int>?> Fetch(DisposalCountingBody body, int value, string? link)
        {
            await using var response = TestResponses.Create(Status.Ok, s_request, body: body);
            return Fetched([value], link);
        }

        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => Fetch(first, 1, "/p2"),
            (key, o, ct) => Fetch(second, 2, null));

        var items = await ItemsOf(pageable);

        Assert.Equal([1, 2], items);
        Assert.Equal((1, 1), (first.DisposeCount, second.DisposeCount));
    }

    [Fact]
    public async Task A_fetcher_that_throws_propagates_unwrapped_because_the_engine_owns_nothing()
    {
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => Result(Fetched([1], "/p2")),
            (key, o, ct) => throw new IOException("page 2 fetch blew up"));
        var items = new List<int>();

        var ex = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await foreach (var item in pageable.WithCancellation(Ct))
            {
                items.Add(item);
            }
        });

        Assert.Equal("page 2 fetch blew up", ex.Message);
        Assert.Equal([1], items);
    }

    [Fact]
    public async Task A_first_fetcher_that_throws_propagates_unwrapped()
    {
        var pageable = Pageable.FromFetchers<int>(
            (o, ct) => throw new TimeoutException("first"),
            (key, o, ct) => Result(null));

        await Assert.ThrowsAsync<TimeoutException>(async () => await ItemsOf(pageable));
    }
}
