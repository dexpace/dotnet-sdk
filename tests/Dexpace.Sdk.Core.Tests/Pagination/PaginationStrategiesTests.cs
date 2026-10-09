// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/pagination/strategies.test.ts (the 18 cases; its Link-target cases
// live in LinkTargetTests) and strategy.test.ts (type-level assertions, kept as reflection facts below). No case was
// dropped as a JavaScript host fact. Divergences, kept as the .NET expectation: Node's cursor and page-number strategies
// read the body themselves (`extract`), here the engine reads it once and the strategy gets the envelope; Node follows an
// absolute cross-origin `Link` target and `<not a url>`, this port ends the stream (P7c-12); an unpaired surrogate in a
// cursor surfaces as ArgumentException, not Node's UrlConstructionError.

using System.Globalization;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-4, PAGE-5, PAGE-16 to PAGE-20 and PAGE-23: the built-in strategies, called directly.</summary>
[Trait("Category", "Unit")]
public sealed class PaginationStrategiesTests
{
    private static readonly Request s_first = Request.Get("https://api.example/items");

    private static Func<Envelope, IReadOnlyList<int>> Items => e => e.Items;

    private static Response ResponseFor(Request executed, Headers? headers = null) =>
        TestResponses.Create(Status.Ok, executed, headers);

    private static Headers LinkHeaders(params string[] lines)
    {
        var builder = new Headers.Builder();
        foreach (var line in lines)
        {
            builder.Add("Link", line);
        }

        return builder.Build();
    }

    // ── Create (the delegate adapter) ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_wraps_the_two_delegates_into_one_parse_result()
    {
        var envelope = new Envelope([1, 2], "x");
        var response = ResponseFor(s_first);
        var next = Request.Get("https://api.example/next");
        var strategy = PaginationStrategies.Create<Envelope, int>(
            e => e.Items,
            (page, r, first) =>
            {
                Assert.Same(envelope, page);
                Assert.Same(response, r);
                Assert.Same(s_first, first);
                return next;
            });

        var info = strategy.Parse(envelope, response, s_first);

        Assert.Equal([1, 2], info.Items);
        Assert.Same(next, info.NextRequest);
    }

    [Fact]
    public void Create_with_a_null_next_request_ends_the_stream()
    {
        var strategy = PaginationStrategies.Create<Envelope, int>(Items, (_, _, _) => null);
        Assert.Null(strategy.Parse(new Envelope([1], null), ResponseFor(s_first), s_first).NextRequest);
    }

    [Fact]
    public void Create_rejects_null_delegates()
    {
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.Create<Envelope, int>(null!, (_, _, _) => null));
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.Create<Envelope, int>(Items, null!));
    }

    // ── Cursor (PAGE-16) ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cursor_sets_the_parameter_on_the_template_and_keeps_its_method_headers_and_body()
    {
        var template = Request.Post("https://api.example/search?q=a", RequestBody.FromString("{}")).WithHeader("X-Trace", "1");
        var strategy = PaginationStrategies.Cursor<Envelope, int>(e => e.Items, e => e.Next);

        var info = strategy.Parse(new Envelope([1], "abc"), TestResponses.Create(Status.Ok, template), template);

        var next = Assert.IsType<Request>(info.NextRequest);
        Assert.Equal(Method.Post, next.Method);
        Assert.Equal("https://api.example/search?q=a&cursor=abc", next.Url.AbsoluteUri);
        Assert.Equal("1", next.Headers.Get("X-Trace"));
        Assert.Same(template.Body, next.Body);
    }

    [Fact]
    public void Cursor_default_parameter_name_is_cursor_and_it_is_configurable()
    {
        var byDefault = PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next);
        var custom = PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next, "after");

        Assert.Equal("?cursor=c", byDefault.Parse(new Envelope([1], "c"), ResponseFor(s_first), s_first).NextRequest!.Url.Query);
        Assert.Equal("?after=c", custom.Parse(new Envelope([1], "c"), ResponseFor(s_first), s_first).NextRequest!.Url.Query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_null_or_empty_cursor_ends_the_stream_and_the_items_are_still_returned(string? cursor)
    {
        var strategy = PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next);

        var info = strategy.Parse(new Envelope([7, 8], cursor), ResponseFor(s_first), s_first);

        Assert.Null(info.NextRequest);
        Assert.Equal([7, 8], info.Items);
    }

    [Fact]
    public void An_existing_cursor_parameter_is_replaced_in_place_not_appended()
    {
        var template = Request.Get("https://api.example/items?cursor=old&limit=5");
        var strategy = PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next);

        var next = strategy.Parse(new Envelope([1], "new"), ResponseFor(template), template).NextRequest!;

        Assert.Equal("https://api.example/items?cursor=new&limit=5", next.Url.AbsoluteUri);
    }

    [Fact]
    public void The_next_request_is_built_from_the_template_never_from_the_executed_request()
    {
        // P7c-8: response.Request is post-pipeline and may carry an Authorization header and a redirect's URL.
        var executed = Request.Get("https://api.example/redirected?page=9").WithHeader("Authorization", "Bearer secret");
        var strategy = PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next);

        var next = strategy.Parse(new Envelope([1], "c"), ResponseFor(executed), s_first).NextRequest!;

        Assert.Equal("https://api.example/items?cursor=c", next.Url.AbsoluteUri);
        Assert.Null(next.Headers.Get("Authorization"));
    }

    [Fact]
    public void Each_delegate_is_called_once_per_parse()
    {
        // Node: "the extract function reads once". Here the engine reads the body once; the strategy never does.
        int itemCalls = 0, cursorCalls = 0;
        var strategy = PaginationStrategies.Cursor<Envelope, int>(
            e =>
            {
                itemCalls++;
                return e.Items;
            },
            e =>
            {
                cursorCalls++;
                return e.Next;
            });

        _ = strategy.Parse(new Envelope([1], "c"), ResponseFor(s_first), s_first);

        Assert.Equal((1, 1), (itemCalls, cursorCalls));
    }

    [Fact]
    public void A_server_cursor_with_a_lone_surrogate_fails_as_ArgumentException_without_echoing_it()
    {
        var strategy = PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next);

        var ex = Assert.Throws<ArgumentException>(() =>
            strategy.Parse(new Envelope([1], "secret\uD800"), ResponseFor(s_first), s_first));

        Assert.DoesNotContain("secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cursor_validates_its_arguments_at_construction()
    {
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.Cursor<Envelope, int>(null!, e => e.Next));
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.Cursor<Envelope, int>(Items, null!));
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next, null!));
        Assert.Throws<ArgumentException>(() => PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next, string.Empty));
        var ex = Assert.Throws<ArgumentException>(() => PaginationStrategies.Cursor<Envelope, int>(Items, e => e.Next, "p\uD800"));
        Assert.Equal("queryParameter", ex.ParamName);
    }

    // ── PageNumber (PAGE-17) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PageNumber_on_the_first_page_with_no_parameter_advances_to_start_plus_one()
    {
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items);

        var info = strategy.Parse(new Envelope([1], null), ResponseFor(s_first), s_first);

        Assert.Equal("https://api.example/items?page=2", info.NextRequest!.Url.AbsoluteUri);
    }

    [Fact]
    public void An_empty_item_list_ends_the_stream_defensively()
    {
        var executed = Request.Get("https://api.example/items?page=4");
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items);

        var info = strategy.Parse(new Envelope([], null), ResponseFor(executed), executed);

        Assert.Null(info.NextRequest);
        Assert.Empty(info.Items);
    }

    [Fact]
    public void The_current_page_comes_from_the_executed_request_not_the_template()
    {
        var executed = Request.Get("https://api.example/items?page=4");
        var template = Request.Get("https://api.example/items?page=1");
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items);

        var next = strategy.Parse(new Envelope([1], null), ResponseFor(executed), template).NextRequest!;

        Assert.Equal("https://api.example/items?page=5", next.Url.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("-3")]
    [InlineData("%2B3")]
    [InlineData("%203")]
    [InlineData("%D9%A3")]
    [InlineData("99999999999")]
    public void A_garbage_page_value_falls_back_to_the_start_page(string rawValue)
    {
        // `%D9%A3` is the Arabic-Indic digit three (design fact 6); `%2B3` is "+3", `%203` is " 3".
        var executed = Request.Get("https://api.example/items?page=" + rawValue);
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items);

        var next = strategy.Parse(new Envelope([1], null), ResponseFor(executed), s_first).NextRequest!;

        Assert.Equal("https://api.example/items?page=2", next.Url.AbsoluteUri);
    }

    [Fact]
    public void A_zero_based_server_is_supported_with_start_page_zero_and_a_custom_name()
    {
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items, "offset", startPage: 0);

        var next = strategy.Parse(new Envelope([1], null), ResponseFor(s_first), s_first).NextRequest!;

        Assert.Equal("https://api.example/items?offset=1", next.Url.AbsoluteUri);
    }

    [Fact]
    public void A_page_that_is_already_int_max_value_ends_the_stream()
    {
        var executed = Request.Get("https://api.example/items?page=2147483647");
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items);

        var info = strategy.Parse(new Envelope([1], null), ResponseFor(executed), s_first);

        Assert.Null(info.NextRequest);
        Assert.Equal([1], info.Items);
    }

    [Fact]
    public void The_page_number_is_parsed_and_written_under_the_invariant_culture()
    {
        CultureInfo culture;
        try
        {
            culture = new CultureInfo("ar-SA");
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
        }

        var executed = Request.Get("https://api.example/items?page=41");
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            var next = strategy.Parse(new Envelope([1], null), ResponseFor(executed), s_first).NextRequest!;
            Assert.Equal("https://api.example/items?page=42", next.Url.AbsoluteUri);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void PageNumber_keeps_the_templates_method_headers_and_body()
    {
        var template = Request.Post("https://api.example/search?q=a", RequestBody.FromString("{}")).WithHeader("X-Trace", "1");
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items);

        var next = strategy.Parse(new Envelope([1], null), ResponseFor(template), template).NextRequest!;

        Assert.Equal(Method.Post, next.Method);
        Assert.Equal("https://api.example/search?q=a&page=2", next.Url.AbsoluteUri);
        Assert.Equal("1", next.Headers.Get("X-Trace"));
        Assert.Same(template.Body, next.Body);
    }

    [Fact]
    public void PageNumber_validates_its_arguments_at_construction()
    {
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.PageNumber<Envelope, int>(null!));
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.PageNumber<Envelope, int>(Items, null!));
        Assert.Throws<ArgumentException>(() => PaginationStrategies.PageNumber<Envelope, int>(Items, string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaginationStrategies.PageNumber<Envelope, int>(Items, "page", -1));
        var ex = Assert.Throws<ArgumentException>(() => PaginationStrategies.PageNumber<Envelope, int>(Items, "p\uD800"));
        Assert.Equal("queryParameter", ex.ParamName);
    }

    // ── LinkHeader (PAGE-18 to PAGE-20) ──

    [Fact]
    public void LinkHeader_follows_the_rel_next_target_with_the_templates_method_headers_and_body()
    {
        var template = Request.Post("https://api.example/search?q=a", RequestBody.FromString("{}")).WithHeader("X-Trace", "1");
        var strategy = PaginationStrategies.LinkHeader<Envelope, int>(Items);
        var response = ResponseFor(template, LinkHeaders("<https://api.example/search?q=a&page=2>; rel=\"next\""));

        var next = strategy.Parse(new Envelope([1], null), response, template).NextRequest!;

        Assert.Equal(Method.Post, next.Method);
        Assert.Equal("https://api.example/search?q=a&page=2", next.Url.AbsoluteUri);
        Assert.Equal("1", next.Headers.Get("X-Trace"));
        Assert.Same(template.Body, next.Body);
    }

    [Fact]
    public void LinkHeader_reads_every_header_instance_and_finds_next_on_the_second()
    {
        var strategy = PaginationStrategies.LinkHeader<Envelope, int>(Items);
        var response = ResponseFor(s_first, LinkHeaders("</items?page=1>; rel=\"prev\"", "</items?page=3>; rel=\"next\""));

        var next = strategy.Parse(new Envelope([1], null), response, s_first).NextRequest!;

        Assert.Equal("https://api.example/items?page=3", next.Url.AbsoluteUri);
    }

    [Fact]
    public void LinkHeader_header_name_is_configurable()
    {
        var strategy = PaginationStrategies.LinkHeader<Envelope, int>(Items, "X-Next");
        var headers = new Headers.Builder().Add("X-Next", "</items?page=2>; rel=next").Build();

        var next = strategy.Parse(new Envelope([1], null), ResponseFor(s_first, headers), s_first).NextRequest!;
        var ignored = strategy.Parse(new Envelope([1], null), ResponseFor(s_first, LinkHeaders("</items?page=2>; rel=next")), s_first);

        Assert.Equal("https://api.example/items?page=2", next.Url.AbsoluteUri);
        Assert.Null(ignored.NextRequest);
    }

    [Fact]
    public void LinkHeader_ends_the_stream_with_no_header_or_no_next_and_still_returns_the_items()
    {
        var strategy = PaginationStrategies.LinkHeader<Envelope, int>(Items);

        var none = strategy.Parse(new Envelope([1, 2], null), ResponseFor(s_first), s_first);
        var prevOnly = strategy.Parse(new Envelope([3], null), ResponseFor(s_first, LinkHeaders("</items?page=1>; rel=\"prev\"")), s_first);

        Assert.Null(none.NextRequest);
        Assert.Equal([1, 2], none.Items);
        Assert.Null(prevOnly.NextRequest);
        Assert.Equal([3], prevOnly.Items);
    }

    [Theory]
    [InlineData("<https://evil.example/p2>; rel=next")]
    [InlineData("<not a url>; rel=next")]
    [InlineData("<ftp://api.example/p2>; rel=next")]
    public void LinkHeader_ends_the_stream_on_a_target_LinkTarget_rejects(string link)
    {
        var strategy = PaginationStrategies.LinkHeader<Envelope, int>(Items);

        var info = strategy.Parse(new Envelope([1], null), ResponseFor(s_first, LinkHeaders(link)), s_first);

        Assert.Null(info.NextRequest);
        Assert.Equal([1], info.Items);
    }

    [Fact]
    public void LinkHeader_with_allowCrossOrigin_follows_a_cross_origin_target()
    {
        var strategy = PaginationStrategies.LinkHeader<Envelope, int>(Items, allowCrossOrigin: true);

        var next = strategy.Parse(
            new Envelope([1], null),
            ResponseFor(s_first, LinkHeaders("<https://cdn.example/p2>; rel=next")),
            s_first).NextRequest!;

        Assert.Equal("https://cdn.example/p2", next.Url.AbsoluteUri);
    }

    [Fact]
    public void LinkHeader_resolves_a_relative_target_against_the_response_url()
    {
        // The base is the executed request's URL (post-redirect), not the template's (PAGE-19).
        var executed = Request.Get("https://api.example/repo/redirected/");
        var strategy = PaginationStrategies.LinkHeader<Envelope, int>(Items);

        var next = strategy.Parse(
            new Envelope([1], null),
            ResponseFor(executed, LinkHeaders("<next?page=2>; rel=next")),
            s_first).NextRequest!;

        Assert.Equal("https://api.example/repo/redirected/next?page=2", next.Url.AbsoluteUri);
    }

    [Fact]
    public void LinkHeader_validates_its_arguments_at_construction()
    {
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.LinkHeader<Envelope, int>(null!));
        Assert.Throws<ArgumentNullException>(() => PaginationStrategies.LinkHeader<Envelope, int>(Items, null!));
        Assert.Throws<ArgumentException>(() => PaginationStrategies.LinkHeader<Envelope, int>(Items, string.Empty));
        var ex = Assert.Throws<ArgumentException>(() => PaginationStrategies.LinkHeader<Envelope, int>(Items, "X\uD800"));
        Assert.Equal("headerName", ex.ParamName);
    }

    // ── the opt-in (P7c-12): documents the warning on LinkHeader's allowCrossOrigin ───────────────────

    private sealed class FixedTokenCredential(string token) : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            new(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1)));
    }

    [Fact]
    public async Task With_allowCrossOrigin_the_walk_follows_and_per_call_auth_stamps_the_credential_for_the_new_origin()
    {
        // This is a Unit fact, not a Security one: it pins the behaviour the XML doc warns about. The refusal is the Security
        // class PaginationLinkOriginTests.
        using var transport = new ScriptedTransport(
            (Func<Request, Response>)(r => PageFixtures.Respond([1], null, LinkHeaders("<https://cdn.example/p2>; rel=next"))(r)),
            (Func<Request, Response>)(r => PageFixtures.Respond([2])(r)));
        using var pipeline = new PipelineBuilder().Add(new BearerTokenAuthPolicy(new FixedTokenCredential("tok"), "scope")).Build(transport);
        var pageable = Pageable.Create<Envelope, int>(
            pipeline,
            s_first,
            new EnvelopeSerde(),
            PaginationStrategies.LinkHeader<Envelope, int>(Items, allowCrossOrigin: true));
        var items = new List<int>();

        await foreach (var item in pageable.WithCancellation(TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2], items);
        Assert.Equal("cdn.example", transport.Requests[1].Url.Host);
        Assert.Equal(["Bearer tok"], transport.Requests[1].Headers.GetAll("Authorization"));
    }

    // ── contract (PAGE-4, PAGE-5; Node strategy.test.ts) ───────────────────────────────────────────────

    [Fact]
    public void An_empty_item_list_with_a_non_null_next_request_is_allowed_by_PageInfo()
    {
        var next = Request.Get("https://api.example/items?page=2");
        var strategy = PaginationStrategies.Create<Envelope, int>(Items, (_, _, _) => next);

        var info = strategy.Parse(new Envelope([], null), ResponseFor(s_first), s_first);

        Assert.Empty(info.Items);
        Assert.Same(next, info.NextRequest);
    }

    [Fact]
    public void One_strategy_instance_is_safe_across_concurrent_parses()
    {
        var strategy = PaginationStrategies.PageNumber<Envelope, int>(Items);
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(1, 201, new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken }, page =>
        {
            var executed = Request.Get("https://api.example/items?page=" + page);
            var next = strategy.Parse(new Envelope([page], null), ResponseFor(executed), s_first).NextRequest;
            var expected = "https://api.example/items?page=" + (page + 1);
            if (next?.Url.AbsoluteUri != expected)
            {
                failures.Add($"page {page}: {next?.Url.AbsoluteUri}");
            }
        });

        Assert.Empty(failures);
    }

    [Fact]
    public void The_strategy_contract_is_generic_in_the_item_type_and_contravariant_in_the_page()
    {
        var parse = typeof(IPageStrategy<,>).GetMethod(nameof(IPageStrategy<object, int>.Parse))!;
        Assert.Equal(typeof(PageInfo<>), parse.ReturnType.GetGenericTypeDefinition());
        Assert.Equal([typeof(Response), typeof(Request)], parse.GetParameters().Skip(1).Select(p => p.ParameterType));

        IPageStrategy<object, int> general = PaginationStrategies.Create<object, int>(_ => [1], (_, _, _) => null);
        IPageStrategy<string, int> specific = general;
        Assert.Equal([1], specific.Parse("page", ResponseFor(s_first), s_first).Items);
    }
}
