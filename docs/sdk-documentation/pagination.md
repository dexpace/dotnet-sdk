# Pagination

**As built by phase 7c, written against source on 2026-10-09** (branch `81-phase-7c-pagination`). This page describes how
`Dexpace.Sdk.Core` pages through a list endpoint after phase 7c: the two views of a walk, the three factories, the built-in and
custom strategies, the cap, cancellation, the `Link` header's origin guard, the fetcher form, the blocking pager, and what changed.
The requirement IDs it cites are the normative text (`docs/product-spec/12-pagination.md`, `PAGE-1` to `PAGE-36`); the decisions
behind each type are in the [phase 7c design](../work/mvp/phase7/phase7c/2026-10-09-phase7c-pagination-design.md) and in
[design §7.1](../sdk-design-dotnet/07-pagination-sse-and-serialization.md). Neither is restated here.

A pageable is a **per-call object over a transport**. It is not a service and holds nothing between walks: build one per call, walk
it, drop it. Each page is an ordinary pipeline call, so retry, redirect and authorization (the [pipelines](./pipelines.md) you already
configured) govern every page.

## In one place

| | Default | To change it |
|---|---|---|
| Pages fetched | unbounded | `maxPages` on every factory; **set a finite cap in production** (`PAGE-10`) |
| `AsPages()` | a **single-use** view | call `AsPages()` again for a new walk (`PAGE-14`) |
| Enumerating the pageable | a fresh walk from the first request each time | (`PAGE-8`) |
| Per-call overrides | `RequestOptions.Empty`, the same instance on every page | the `options` argument (`PAGE-36`) |
| A `Link` target on another origin | the walk **ends** | `LinkHeader(..., allowCrossOrigin: true)`, with the warning below |
| The template's body | must be replayable | `await body.ToReplayableAsync()` before `Create` |
| A page cursor named | `cursor` | `Cursor(..., queryParameter)` |
| A page number named, starting at | `page`, `1` | `PageNumber(..., queryParameter, startPage)` |

## Driving one

```csharp
var pageable = Pageable.Create<WidgetPage, Widget>(
    pipeline,                                           // an HttpPipeline converts to IAsyncHttpClient
    Request.Get("https://api.example.com/v1/widgets"),
    serde,                                              // reads each page envelope into WidgetPage
    PaginationStrategies.Cursor<WidgetPage, Widget>(page => page.Items, page => page.Next),
    options: new RequestOptions { MaxRetries = 2 },
    maxPages: 50);

await foreach (var widget in pageable.WithCancellation(token)) { /* item by item, in server order */ }
await foreach (var page in pageable.AsPages()) { /* page.Values, page.Status, page.Headers, page.Request */ }
```

**Two views of one walk** (`PAGE-1`). Enumerating the pageable flattens a fresh walk into its items; `AsPages()` yields the pages
themselves with their status, headers and the request the walk sent for each (`Page<T>.Request`, before the pipeline ran, so it
carries no credential the auth policies stamped). Both are built on one private walk, so they cannot drift.

**Lazy** (`PAGE-6`, `PAGE-7`). Creating the pageable, calling `AsPages()` and obtaining an enumerator send nothing; the first
`MoveNextAsync` sends the first request, and a further page is fetched only when the consumer advances past the last item of the
previous one. After the last page `MoveNextAsync` returns `false` forever and sends nothing. An **empty page with a next request
costs one exchange and the walk continues**: `null` as the next request is the only end signal (`PAGE-4`).

**Restartable, but the page view is not** (`PAGE-8`, `PAGE-14`). Each `GetAsyncEnumerator` call on the pageable starts again from
the first request. The view `AsPages()` returns throws `InvalidOperationException` on its second `GetAsyncEnumerator`, because a
silent restart would re-run the first request behind the consumer's back; ask for a new view instead.

### Pages are values, not resources

A `Page<T>` is a sealed, immutable value: `Values` (a snapshot, not a live view of the envelope), `Status`, `Headers`, `Request`. It
is neither `IDisposable` nor `IAsyncDisposable`. The engine reads the body once, parses it, and **closes the response before it
yields the page**, so no response is open while you hold a page or an item, and an enumerator you drop without `DisposeAsync`
leaks nothing (`PAGE-3`, `PAGE-11`, `PAGE-12`; design §10 entry 17). The item view closes a page before it hands over even the
first of its items. Still enumerate with `await foreach`, or `await using` a hand-driven enumerator, so the walk itself is disposed
on an early exit.

A failure to parse closes the response with the parse error primary and a failing close attached to its trail
(`ExceptionTrail.GetSuppressed`, `PAGE-13`); a failure to close on the success path ends the walk with that exception, unwrapped
(`PAGE-15`, `PAGE-32`). Every cause surfaces as itself (`PAGE-28`): a transport fault, a serde failure, your strategy's exception.
A `null` response from the client, a `null` envelope (`DeserializationException` naming the page type) and a `null` `PageInfo`
from a strategy are contract violations and close the response first.

## The three factories

| | For | Seam |
|---|---|---|
| `Pageable.Create<TPage, T>` | a strategy over the async transport | `IAsyncHttpClient` |
| `Pageable.CreateBlocking<TPage, T>` | the same, blocking | `IHttpClient` |
| `Pageable.FromFetchers<T>` | an API already wrapped in functions | none |

`CreateBlocking` has a different name from `Create` on purpose: an `HttpPipeline` converts to both seams, so two `Create` overloads
would be ambiguous (`CS0121`) for the most common caller. All three validate at construction, not on the first page: a `null`
argument, a `maxPages` of zero or less (`PAGE-9`), and, for the first two, a template whose body is a single-use stream
(`ArgumentException` naming `ToReplayableAsync`, because every page re-sends the body). None disposes the client you pass.

## Strategies

A strategy turns one page into its items and the request for the next page. Three ship, each holding only its configuration, so one
instance serves any number of concurrent walks (`PAGE-5`):

```csharp
PaginationStrategies.Cursor<WidgetPage, Widget>(p => p.Items, p => p.Next, queryParameter: "cursor");   // ?cursor=<opaque>
PaginationStrategies.PageNumber<WidgetPage, Widget>(p => p.Items, queryParameter: "page", startPage: 1); // ?page=1,2,3
PaginationStrategies.LinkHeader<WidgetPage, Widget>(p => p.Items, headerName: "Link");                  // RFC 8288
```

- **Cursor** (`PAGE-16`): a `null` or empty cursor ends the stream; otherwise the cursor is spliced into the first request's query,
  replacing an existing parameter in place.
- **PageNumber** (`PAGE-17`): an empty item list ends the stream. The current page is read from the **executed** request's URL
  (ASCII digits only, invariant culture; anything else, including a signed, spaced or non-ASCII number, is `startPage`) and the next
  request carries it plus one. `startPage: 0` suits a zero-based server. A page already at `int.MaxValue` ends the stream.
  **A non-empty last page costs one more, empty, exchange**; the old `hasMore` predicate is gone. To stop early on a field of
  the envelope use `Create`:

  ```csharp
  PaginationStrategies.Create<WidgetPage, Widget>(
      page => page.Items,
      (page, response, first) => page.HasMore ? first.WithUrl(/* the same URL, page + 1 */) : null);
  ```
- **LinkHeader** (`PAGE-18` to `PAGE-20`): every instance of the header is read and joined; the first `rel=next` wins, `rel` may be
  quoted or not, a space- or tab-separated token list, case-insensitive; commas and semicolons inside `<...>` and quoted values are
  data. The target is resolved against the page's **response** URL (RFC 3986), so a first page that redirected still resolves
  relative links, and a query-only `?page=2` keeps the path.

The query splice behind `Cursor` and `PageNumber` is internal in v1: it copies every untargeted segment verbatim, matches names
ordinally on the decoded name, replaces the first occurrence and drops later duplicates, and rebuilds the URL from its components
(`PAGE-21` to `PAGE-24`). Its one residue is `System.Uri`'s own canonical form of the query, which decodes percent-escaped
unreserved characters (`%7E` becomes `~`) before the splice sees it (design §10 entry 18). A lone surrogate in a name or value is an
`ArgumentException` that names the parameter and never echoes the value, because a cursor can carry a session token.

### The `Link` origin guard

Each page is a fresh pipeline call, and the authorization policies stamp a credential when a request is same-origin with *its own
call's* first request. So a hostile or compromised server could take your bearer token with one `Link` header naming its own
host. The strategy therefore **ends the walk, quietly,** when the target:

- is on another origin than the **first** request (another host, another port, or `https` to `http`), including a network-path
  reference like `//evil.example/x`;
- holds a space, a control character, `<`, `>` or `"` (System.Uri would otherwise accept `not a url` as a relative path);
- is not `http` or `https`, or has no host.

Userinfo in a target is removed (`https://user:pw@api.example/x` is followed as `https://api.example/x`). The origin compared is the
first request's, not the response's, so a first page that redirected to another origin does not license following links there
(`PaginationLinkOriginTests`, a permanent `Security` test). An API that really pages across hosts (a CDN, a regional host) opts in
with `allowCrossOrigin: true`, **and per-call authorization will then stamp the credential for the new origin**: only do it for
hosts you trust with that credential. A rejected target is silent (a strategy has no logger and `PAGE-19` asks for an end, not an
error), which can hide a misconfigured server: if a walk ends earlier than expected, look at the `Link` header first.

### Writing a custom strategy

`IPageStrategy<in TPage, T>` is one synchronous method over a value the engine already read:

```csharp
public interface IPageStrategy<in TPage, T>
{
    PageInfo<T> Parse(TPage page, Response response, Request first);
}
```

`PageInfo<T>(items, nextRequest)` is the one output; `nextRequest` being `null` is the end of the stream. Five rules:

1. **Read only what you need, synchronously.** The engine deserialized the body into `TPage` through the `ISerde`; you never read a
   body, and the response is closed as soon as `Parse` returns (`PAGE-5`, `PAGE-16`). Read `response.Status`, `response.Headers` and
   `response.Request.Url`; keep no state between calls.
2. **Build the next request from `first`, the walk's first request** (named for the template it is; CA1716 reserves the word on an
   interface member), with `first.WithUrl(...)`, which keeps the method, headers and body. Never build it from `response.Request`:
   that is the request the transport executed, post-redirect, and carries whatever the auth policies stamped on it.
3. **Terminate.** Returning a next request equal to the one just fetched is an infinite walk. `maxPages` is the backstop, not the
   design; loop detection is not the engine's job.
4. **Return a well-formed `PageInfo`.** `Items` is never `null` (an empty list is a valid, non-terminal page when the next request
   is not `null`). Returning `null` from `Parse` is a contract violation: the engine closes the response and throws
   `InvalidOperationException` naming your strategy type, rather than ending the walk quietly, so a forgotten `return` is not read as
   "the server ran out of pages".
5. **End and fail are different acts.** To end, return a `PageInfo` with a `null` next request. To fail, throw: the engine closes the
   response and your exception reaches the consumer unwrapped (`PAGE-13`, `PAGE-28`).

```csharp
internal sealed class OffsetStrategy<TPage, T>(Func<TPage, IReadOnlyList<T>> items, int pageSize) : IPageStrategy<TPage, T>
{
    public PageInfo<T> Parse(TPage page, Response response, Request first)
    {
        var list = items(page);
        if (list.Count < pageSize) { return new PageInfo<T>(list, null); }       // a short page is the last

        var current = int.TryParse(Query.Parse(response.Request.Url.Query).Get("offset"), out var o) ? o : 0;
        var query = Query.Parse(first.Url.Query).ToBuilder().Set("offset", (current + pageSize).ToString(CultureInfo.InvariantCulture)).Build();
        return new PageInfo<T>(list, first.WithUrl(new UriBuilder(first.Url) { Query = query.Encode() }.Uri));
    }
}
```

`PaginationStrategies.Create(items, nextRequest)` is the two-delegate form of the same contract. **A custom strategy and `Create` get
no origin guard**: if you follow a URL the server put in the body, check its origin against `first.Url` yourself.

## The cap

`maxPages` counts exchanges and stops the walk even when the last page named a next one (`PAGE-9`); `null` means unbounded
(`PAGE-10`, a SHOULD that production code should not take up): a server that never reports an end is walked forever. A value of
zero or less throws at construction.

## Cancellation

The token reaches every exchange and the body read, and is observed at the **top of each page**, never between the items of a page
that was fetched: a fetched page is always delivered whole, and the walk throws `OperationCanceledException` when the consumer asks
for the next (`PAGE-25`, `PAGE-26`). `await foreach (var x in pageable.WithCancellation(token))` and
`AsPages().WithCancellation(token)` both pass it.

**The race** (`PAGE-33`). A response the transport builds *after* you cancelled is never delivered to the pageable, and is the
transport's to release (`TRANSPORT-9`). A response that *is* delivered after the token was cancelled is disposed and discarded
rather than parsed, and the walk throws `OperationCanceledException`: you never receive a page you asked not to get.

## The fetcher form

When the API is already wrapped in functions, skip the request and the strategy:

```csharp
var pageable = Pageable.FromFetchers<Widget>(
    async (options, ct) => await FetchFirstAsync(ct),                       // returns FetchedPage<Widget>? ; null is an empty stream
    async (key, options, ct) => await FetchNextAsync(key, options, ct),     // key: the previous NextLink, else its ContinuationToken
    maxPages: 20);
```

The first fetcher runs exactly once per walk. The key for the next is `NextLink` when it is not blank, else `ContinuationToken` when it
is not empty; neither, or a `null` page, ends the stream (`PAGE-34`). One mutable `PagingOptions` is passed to **every** call of a
walk, with `NextLink` and `ContinuationToken` written onto it before each next call and a `State` dictionary for your own scratch; a
new walk gets a fresh instance (`PAGE-35`, `PAGE-8`). It is single-consumer and not thread-safe.

**Ownership: the fetcher disposes its own response.** A `Page<T>` owns no response, so a fetcher reads its response, builds the
page, and disposes the response itself, typically `await using` (design §10 entry 17 as amended). The engine never sees one. This
inverts `PAGE-34`'s "the fetcher must not close it" for the same reason `PAGE-3` is inverted; the guarantee it protects, no leaked
response, is your `await using`. A fetcher that throws propagates unwrapped. Fetchers are async only; a blocking fetcher form is a
`Pageable<T>` subclass away.

## The blocking pager

`Pageable<T>` is the blocking twin, enumerated with `foreach`; it takes the `CancellationToken` once, at `CreateBlocking`. It runs
the same fetch, parse and close step as the async pager with a flag that stops it awaiting, and reads each result without ever
blocking on a task. Two things to know:

- **It is honest above the transport only.** Until the real synchronous transport (roadmap phase 8b, `PIPE-28`),
  `SystemNetHttpClient.Execute` is sync-over-async inside the transport, so the pager inherits that gap.
- **Each page envelope is buffered** under the 64 MiB materialisation cap (a larger one fails with `BodyTooLargeException`);
  the async pager streams. A page envelope over 64 MiB is not a paging use case.

## What changed

All pre-release. Callers of the old surface migrate as follows:

| Before | Now |
|---|---|
| `Pageable.Create(pipeline, first, serde, clientOptions, selectItems, nextRequest, maxPages)` | `Pageable.Create(pipeline, first, serde, PaginationStrategies.Create(selectItems, nextRequest), requestOptions, maxPages)`; a caller who passed different client options builds a pipeline with them |
| `Cursor<TPage>(nextCursor, queryParameter)` returning a delegate | `Cursor<TPage, T>(items, nextCursor, queryParameter = "cursor")` returning a strategy |
| `PageNumber<TPage>(queryParameter, hasMore)` | `PageNumber<TPage, T>(items, queryParameter = "page", startPage = 1)`; it ends on an empty page; use `Create` for a predicate |
| `LinkHeader<TPage>(rel)` | `LinkHeader<TPage, T>(items, headerName = "Link", allowCrossOrigin = false)`: cross-origin, malformed and userinfo-bearing targets behave differently; every instance is read; the base is the response URL |
| the splice matched names case-insensitively and kept duplicates | it matches ordinally on the decoded name and drops later duplicates |
| `AsPages(int? pageSizeHint)` (abstract), `GetAsyncEnumerator` (abstract) | `AsPages()` (a single-use view) and `GetAsyncEnumerator` are not virtual; subclasses override `WalkPagesAsync` |
| `new Page<T>(values, status, headers)` | `new Page<T>(values, status, headers, request)` |
| `maxPages` of zero or less, and a single-use template body, were accepted | both throw at construction |
| a `null` envelope threw `InvalidOperationException` | it throws `DeserializationException` naming the page type |

New: `Pageable.CreateBlocking`, `Pageable.FromFetchers`, `Pageable<T>`, `PageInfo<T>`, `IPageStrategy<TPage, T>`, `FetchedPage<T>` and
`PagingOptions`. The query splice stays internal; making it public is a post-1.0 candidate ([`first-release.md`](../first-release.md)).
