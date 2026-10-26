# Write a paging strategy

**As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). Ported from Node's `docs/sdk-documentation/write-a-paging-strategy.md` (`nodejs-sdk`, `origin/main`) and restated for
`IPageStrategy<in TPage, T>`. [`pagination.md`](./pagination.md) is the reference for the engine (the two views of a walk, the cap, cancellation, the `Link` origin guard); this page is for the author of a
strategy, or of a fetcher for an API that is already wrapped in functions.

A strategy is one synchronous method:

```csharp
public interface IPageStrategy<in TPage, T>
{
    PageInfo<T> Parse(TPage page, Response response, Request first);
}

public sealed class PageInfo<T>
{
    public PageInfo(IReadOnlyList<T> items, Request? nextRequest);
    public IReadOnlyList<T> Items { get; }
    public Request? NextRequest { get; }   // null ends the walk
}
```

Given the page envelope the engine has **already deserialized** (`TPage`), the response that carried it, and the walk's first request, produce this page's items and the request for the next one. `null` for
`NextRequest` is how a walk ends (`PAGE-4`): there is no separate "done" flag to keep consistent with it.

**`first` is the walk's first request, not the one just sent.** (Node's `template` advances with the walk; this port's `first` does not, so a next request derived from it never accumulates a stamp.) It carries the
method, headers, body and per-call options the caller started with. The request that was actually executed, post-redirect and with whatever the authorization policies stamped on it, is `response.Request`: read a
position from it (the current page number), never build the next request from it.

## Three ship already

Reach for a custom strategy only when none of these fits. Each takes an `items` selector and holds nothing else, so one instance serves any number of concurrent walks (`PAGE-5`):

```csharp
using Dexpace.Sdk.Core.Pagination;

// ?cursor=<opaque>, taken from the envelope; a null or empty cursor ends the stream (PAGE-16)
IPageStrategy<ThingPage, Thing> cursor = PaginationStrategies.Cursor<ThingPage, Thing>(page => page.Items, page => page.Next, queryParameter: "cursor");

// ?page=1,2,3...; an empty item list ends the stream (PAGE-17)
IPageStrategy<ThingPage, Thing> byNumber = PaginationStrategies.PageNumber<ThingPage, Thing>(page => page.Items, queryParameter: "page", startPage: 1);

// RFC 8288 Link: <...>; rel="next"; a target on another origin ends the walk (PAGE-18 to PAGE-20)
IPageStrategy<ThingPage, Thing> byLink = PaginationStrategies.LinkHeader<ThingPage, Thing>(page => page.Items, headerName: "Link");

// The two-delegate form of the same contract, to stop early on a field of the envelope (PAGE-17's old hasMore)
IPageStrategy<ThingPage, Thing> custom = PaginationStrategies.Create<ThingPage, Thing>(
    page => page.Items,
    (page, response, first) => page.HasMore ? first.WithUrl(new Uri(first.Url, $"?page={page.Number + 1}")) : null);

Console.WriteLine($"{cursor.GetType().Name} {byNumber.GetType().Name} {byLink.GetType().Name} {custom.GetType().Name}");

internal sealed record Thing(string Id);

internal sealed record ThingPage(IReadOnlyList<Thing> Items, string? Next, bool HasMore, int Number);
```

`PageNumber` costs one more, empty, exchange after a non-empty last page; use `Create` to stop on the envelope. `LinkHeader` resolves a relative target against the **response** URL and drops the userinfo of a target.
`Cursor` and `PageNumber` splice into the first request's query through an internal helper that replaces the first occurrence of a name and drops later duplicates.

## Writing one

Offset paging is none of the three: the server wants `?offset=` and a short page is the last. This strategy is complete, and so is the program that drives it, over a `DelegateHttpClient` that serves five things two to
a page, so it runs with no network. Both are compiled by the documentation build.

```csharp
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(ThingJson.Default));

string[] ids = ["t0", "t1", "t2", "t3", "t4"];
await using IAsyncHttpClient transport = DelegateHttpClient.Create((request, options, cancellationToken) =>
{
    int offset = int.TryParse(Query.Parse(request.Url.Query).Get("offset"), CultureInfo.InvariantCulture, out int o) ? o : 0;
    string json = "{\"items\":[" + string.Join(',', ids.Skip(offset).Take(2).Select(id => $"{{\"id\":\"{id}\"}}")) + "]}";
    var body = ResponseBody.FromBytes(Encoding.UTF8.GetBytes(json), CommonMediaTypes.ApplicationJson);
    return Task.FromResult(new Response(request, Status.Ok, Protocol.Http11, Headers.Empty, body));
});

AsyncPageable<Thing> things = Pageable.Create(
    transport,
    Request.Get("https://api.example.com/things"),
    serde,                                                                          // reads each page envelope into ThingPage
    new OffsetStrategy<ThingPage, Thing>(page => page.Items, pageSize: 2),
    maxPages: 10);                                                                  // the backstop; set a finite cap in production

await foreach (Thing thing in things)
{
    Console.WriteLine(thing.Id);   // t0, t1, t2, t3, t4: three exchanges, the third a short page
}

internal sealed class OffsetStrategy<TPage, T>(Func<TPage, IReadOnlyList<T>> items, int pageSize) : IPageStrategy<TPage, T>
{
    public PageInfo<T> Parse(TPage page, Response response, Request first)
    {
        IReadOnlyList<T> list = items(page);
        if (list.Count < pageSize)
        {
            return new PageInfo<T>(list, null);   // a short page is the last; null is the single end signal
        }

        // Read the position from the executed request; derive the next request from the first one.
        int current = int.TryParse(Query.Parse(response.Request.Url.Query).Get("offset"), CultureInfo.InvariantCulture, out int o) ? o : 0;
        Query query = Query.Parse(first.Url.Query).ToBuilder().Set("offset", (current + pageSize).ToString(CultureInfo.InvariantCulture)).Build();
        return new PageInfo<T>(list, first.WithUrl(new UriBuilder(first.Url) { Query = query.Encode() }.Uri));
    }
}

internal sealed record Thing(string Id);

internal sealed record ThingPage(IReadOnlyList<Thing> Items);

[JsonSerializable(typeof(Thing))]
[JsonSerializable(typeof(ThingPage))]
internal sealed partial class ThingJson : JsonSerializerContext;
```

Because `Parse` is a pure synchronous function over a value, a strategy needs no transport to test: construct a `Response` and a `ThingPage`, call `Parse`, and assert on `Items` and `NextRequest`.

## The five rules

**1. Read only what you need, synchronously, and keep nothing** (`PAGE-5`, `PAGE-16`). The engine read the body into `TPage` through the `ISerde` and closes the response as soon as `Parse` returns; you never read a
body. Read `response.Status`, `response.Headers` and `response.Request.Url`, hold no state between calls, and never hand the `Response` to a caller. (Node's `parse` is async because Node has no synchronous body
read; `PAGE-5` says synchronously, and on .NET it is.)

**2. Build the next request from `first`**, with `first.WithUrl(...)`, which keeps the method, headers, body and options of the walk. A next request built from scratch loses them, and one built from
`response.Request` carries whatever the auth policies stamped on it. Use `Query` for the URL work, not string concatenation: it encodes, and it matches names exactly ([`http.md`](./http.md)).

**3. A page is closed before its items are yielded** (`PAGE-3`, `PAGE-11`). The engine does this for you, and it means your `Parse` is the **only** place the response is readable. A `Page<T>` owns no response, so a
`Page<T>` is not `IDisposable`.

**4. Terminate.** Returning a next request equal to the one just fetched is an infinite walk. `maxPages` on `Pageable.Create` is the backstop, not the design; loop detection is not the engine's job.

**5. Return a well-formed `PageInfo`, and end differently from failing** (`PAGE-4`, `PAGE-13`, `PAGE-28`). `Items` is never `null`; an empty list is a valid non-terminal page when `NextRequest` is not `null`
(it costs one exchange). A `null` return from `Parse` is a contract violation: the engine disposes the response and throws `InvalidOperationException` naming your strategy type, rather than ending the walk
quietly, so a forgotten `return` is not read as "the server ran out of pages". To **end**, return a `PageInfo` with a `null` next request. To **fail**, throw: the engine disposes the response and your exception
reaches the consumer unwrapped.

**A custom strategy gets no origin guard.** The built-in `LinkHeader` ends the walk when the target is on another origin than the first request, because each page is a fresh pipeline call and the authorization
policies stamp a credential on a request that is same-origin with its own call's first request: a hostile server could otherwise take your bearer token by naming itself. If your strategy follows a URL the
server put in the body, **check its origin against `first.Url` yourself** (`PaginationLinkOriginTests` is the permanent `Security` test of the built-in).

## The fetcher form

When the API is already wrapped in functions rather than reachable as requests, skip `IPageStrategy` and the request entirely. This program is complete and runs with no network:

```csharp
using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;

string[] ids = ["t0", "t1", "t2", "t3", "t4"];

AsyncPageable<string> things = Pageable.FromFetchers<string>(
    (options, cancellationToken) => ValueTask.FromResult<FetchedPage<string>?>(Fetch(0)),                                                 // runs exactly once per walk
    (key, options, cancellationToken) => ValueTask.FromResult<FetchedPage<string>?>(Fetch(int.Parse(key, CultureInfo.InvariantCulture))),   // key: the previous NextLink, else its ContinuationToken
    maxPages: 20);

await foreach (string id in things)
{
    Console.WriteLine(id);
}

FetchedPage<string> Fetch(int offset)
{
    var page = new Page<string>([.. ids.Skip(offset).Take(2)], Status.Ok, Headers.Empty, Request.Get($"https://api.example.com/things?offset={offset}"));
    return new FetchedPage<string>(page, NextLink: offset + 2 < ids.Length ? (offset + 2).ToString(CultureInfo.InvariantCulture) : null);
}
```

`first` returns a `FetchedPage<T>` (a `Page<T>` plus either a `NextLink` or a `ContinuationToken`), or `null` to end the walk; `next` receives the key of the previous page: its `NextLink` when that is not blank, else its
`ContinuationToken` when that is not empty, and neither ends the stream (`PAGE-34`). One mutable `PagingOptions` is passed to every call of a walk, with `NextLink` and `ContinuationToken` written onto it before each
`next` call and a `State` dictionary for your own scratch; a new walk gets a fresh instance (`PAGE-35`). It is single-consumer and not thread-safe. This is the adapter for a generated client whose pagination is
already a pair of methods.

**Ownership inverts here:** a `Page<T>` owns no response, so a fetcher reads its response, builds the page and **disposes the response itself**, typically with `await using`. The engine never sees it. A fetcher that
throws propagates unwrapped. Fetchers are asynchronous only.

## Cancellation and the blocking pager

The token reaches every exchange and the body read, and is observed at the **top of each page**: a fetched page is always delivered whole, and the walk throws `OperationCanceledException` when the consumer asks for
the next. `await foreach (var x in things.WithCancellation(token))` passes it. `Pageable.CreateBlocking` is the blocking twin over `IHttpClient`, taking the token once at creation; it needs a transport whose
bodies read synchronously, and the reference transport's do ([`pagination.md`](./pagination.md)).
