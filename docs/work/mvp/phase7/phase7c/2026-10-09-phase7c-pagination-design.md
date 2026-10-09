# Phase 7c — Pagination: Design

**Status:** Draft, for review. Written 2026-10-09 against `main` at `8dc8ee4` (phases 0 to 6c merged). No issue is filed for
7c. Brainstormed without a human in the loop: every judgement call the brainstorming skill would have put to the lead is
taken here as a numbered ruling (`P7c-n`), with the options and the rationale. The calls the lead may still reverse are
marked **open for the lead**. 7a (serde) and 7b (SSE) are being designed in parallel by other authors in this working tree.
The roadmap says the three sub-phases are independent (`SSE-37`, ch.12's serde-agnostic engine). The few files they share
are listed in [Cross-sub-phase interfaces](#cross-sub-phase-interfaces-with-7a-and-7b). The scope authority is the roadmap's
Phase 7 card (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`, "Phase 7 — Serde, SSE and Pagination"). The
format follows the phase 6 designs (`docs/work/mvp/phase6/phase6b/2026-10-08-phase6b-redirect-design.md` and its
siblings).

**What this document is.** The sub-phase design for 7c. It holds:

- one disposition per requirement row (36 rows, `PAGE-1`–`PAGE-36`);
- the approaches considered for the three load-bearing choices, and the one picked;
- the rewritten paging engine (async and blocking), the strategy contract, the query splice, the `Link` parser and the
  fetcher front-end;
- the public surface 7c adds, changes or removes, and the internal types behind it;
- the breaking changes;
- the test strategy, the vectors and the ports from Node and Ruby;
- the exit criteria, including 7c's share of the AOT smoke round trip;
- the decisions (`P7c-n`), which double as the deviation-ledger IDs (roadmap constraint 7).

**What this document is not.** It is not the plan and not the checklist. It does not restate design §7.1's argument for
materialized pages (§10 entry 17), for the canonical-form splice (§10 entry 18) or for the single pull-based engine (§10
entry 19). It cites them and records a decision against each row. It edits no design chapter, roadmap cell or `CLAUDE.md`
line; the corrections it owes are listed in [Corrections owed at close-out](#design-roadmap-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

| Predecessor or sibling | Kind | State at `8dc8ee4` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030`, `MA0051`, `CA2007`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. Every type below is written under them. |
| 2a (`Request` validated constructor, `WithUrl`, `Headers.GetAll`, `Query`, internal `Rfc3986`) | **dependency** | Met. The splice reuses `Rfc3986.EncodeComponent`/`DecodeComponent` (its doc comment already names "the pagination splice (PAGE-22)"). `Query` itself is **not** used for the splice (P7c-9). |
| 2b (`IAsyncHttpClient.ExecuteAsync(Request, RequestOptions, CancellationToken)`, `IHttpClient.Execute`, `RequestOptions`, `ISerde`) | **dependency** | Met. `RequestOptions` is the per-call override `PAGE-36` names. |
| 3a/3b (`ResponseBody.ReadAsBytes` sync twin and its 64 MiB cap, the dispose latches, `RequestBody.IsReplayable`) | **dependency** | Met. |
| 4b (`Disposal.DisposeQuietly[Async](resource, primary)`, `ExceptionTrail`, `ExceptionFacts.IsFatal`) | **dependency** | Met. The card's "suppressed-close helper from 4b" is `Disposal`; 4b's hand-off "7c uses `ExceptionTrail` for `PAGE-13`" is taken. |
| 4c (`HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient`; the real sync `Send`; `SyncPath.GetCompletedResult`) | **dependency** | Met. 4c's hand-off "7c may take `HttpPipeline` as an `IAsyncHttpClient` in `Pageable`" is taken (P7c-5). |
| 6a/6b/6c (retry, redirect, auth over the pipeline; `AuthOrigin`/`HttpOrigin`) | **convenience** | Met. Each page is an independent pipeline call, so retry, redirect and auth govern every page. 6c's per-call seed-origin stamping is why the `Link` cross-origin guard exists (P7c-12). |
| 8b (the real synchronous transport) | 7c's blocking pager **inherits** its gap | Not built. Until 8b, `SystemNetHttpClient.Execute` is sync-over-async inside the transport (roadmap ordering strand 5). The blocking pager is honest above the transport and inherits `PIPE-28`'s ⏳. **Corrected 2026-10-09 (review):** the gap is wider than sync-over-async. The pager reads through `ResponseBody.OpenRead`, and `HttpResponseMessageBody` does not override it, so `CreateBlocking` over `SystemNetHttpClient` throws `NotSupportedException` on the first page until 8b adds `HttpResponseMessageBody.OpenRead`. **Corrected 2026-10-09 (rebase onto 7b):** 7b added `HttpResponseMessageBody.OpenRead` first, so `CreateBlocking` over `SystemNetHttpClient` walks every page; what the pager still inherits from 8b is the sync-over-async `Execute`. |
| **7a** (serde), **7b** (SSE) | **convenience**, both ways | Designed in parallel. 7c consumes none of their new types. See [Cross-sub-phase interfaces](#cross-sub-phase-interfaces-with-7a-and-7b). |

**No dependency edge inverts the roadmap's order.** The card's entry criterion ("3a … and 4b/4c have exited") is met.

---

## Governing documents, and the phase-start queries

Read in full for this design: the Phase 7 card; `docs/product-spec/12-pagination.md`; appendix C rows 374–409; design
§7.1 (`docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md`); §10 entries 13, 17, 18, 19; §3.7's quiet-dispose
paragraph; §5.2's suppressed-trail paragraph; §12's PAGE row; the 4b, 4c, 6a and 6b hand-offs that name 7c. §3.4 (the serde
seam) was read for the envelope read only; §7.2/§7.3 are 7b's and 7a's.

Queries run at phase start (`scripts/knowledge`, SDK 10.0.401):

| Query | Result |
|---|---|
| `--prefix-info PAGE` | 36 canonical IDs, 32 MUST, 4 SHOULD; owning chapter ch.12; **36 of 36 substantive**, 0 roll-up only, 0 uncited. |
| `--gaps PAGE` | **0 gaps.** No ID has to be read out of appendix C alone; appendix C was read anyway for the five rows whose text decides a ruling (`PAGE-4`, `PAGE-12`, `PAGE-18`, `PAGE-19`, `PAGE-34`). |
| `--origin note --brief` | 12 notes across 8 topics; **none touches PAGE**. The `suppressed-trail` (§10 entry 13) and the `CA1062` note apply as house rules. |
| `--section conflicts --brief` | No conflict entry touches PAGE. |

Sibling sources consulted: `nodejs-sdk/packages/core/src/pagination/` at `c0ff3fd` (11 test files, 113 cases; the
`query-splice.ts`, `link-header.ts`, `fetchers.ts` implementations), and `ruby-sdk/docs/work/mvp/phase7/phase7c/2026-09-10-phase7c-pagination-design.md`
at `90075b1`. **The Ruby test sources the card names (`ruby-sdk/.../page/`, `page_fixtures.rb`) are not in the local
checkout** at `90075b1` (no `gems/` tree); see risk R3.

---

## Scope and the 36-row census

**7c owns 36 rows: `PAGE-1`–`PAGE-36`** (32 MUST, 4 SHOULD: `PAGE-10`, `PAGE-20`, `PAGE-31`, `PAGE-35`). The card's
"17 met, 8 partial, 1 missing, 6 N/A candidates" counts MUSTs at `d45e64b`. 7c contributes tests to `PIPE-26` ("a pipeline
backing a paginator") and `SERDE-13` (the null envelope), whose rows stay with 4c and 7a.

Disposition legend: **build** (7c writes the code), **met** (already holds; evidence named; 7c adds the pinning test where
none exists), **N/A** (cannot apply on .NET; reason and §10/§11 anchor), **elsewhere** (the row's work travels to another
phase). Planned checklist status uses roadmap constraint 3's legend: ✅ built and tested, 🚫 permanent simplification, ⏳
deferred.

| ID | Level | Disposition | Planned | One line |
|---|---|---|---|---|
| `PAGE-1` | MUST | met + build | ✅ | Item view (`GetAsyncEnumerator`/`GetEnumerator`) and page view (`AsPages()`) flatten one private walk, so they cannot drift; server order across page boundaries. The "live response" of the 12.1 preamble is §10 entry 17. Evidence today: `PageableTests.GetAsyncEnumerator_FlattensItemsAcrossPages`, `AsPages_YieldsCorrectNumberOfPages`. Build: the blocking twin, and a 3-page fixture asserting status/headers per page. |
| `PAGE-2` | MUST | build | ✅ | `Page<T>` gains `Request` (P7c-7). Items, status, headers and request are immutable values, so readable forever. Items never null: `Page<T>` and `PageInfo<T>` throw on null. |
| `PAGE-3` | MUST | N/A | 🚫 | §10 entry 17: a page owns no response; the engine disposes before it yields. The "fetcher MUST NOT close" clause is inverted for the same reason (P7c-15), a dated amendment to entry 17. |
| `PAGE-4` | MUST | build | ✅ | `IPageStrategy<TPage,T>.Parse` returns one `PageInfo<T>` carrying items and `NextRequest`; `null` `NextRequest` is the only end signal. A `null` `PageInfo` is a contract violation (`InvalidOperationException` naming the strategy type, response closed on the `PAGE-13` path). Empty items + non-null next fetches again (P7c-2). |
| `PAGE-5` | MUST | build | ✅ | The engine reads the body once into `TPage`; `Parse` is synchronous over that value plus the already-read response's status, headers and request. Built-ins capture configuration only. Test: one strategy instance across two concurrent walks. |
| `PAGE-6` | MUST | met + build | ✅ | Async iterator bodies do not run before the first `MoveNextAsync`; factories capture only. Evidence: `Laziness_*` tests. Build: the blocking engine's three zero-exchange probes (construct, `AsPages()`/enumerable, `GetEnumerator()`). |
| `PAGE-7` | MUST | met | ✅ | A `null` next ends the iterator; a finished compiler iterator returns `false` forever. New test: three `MoveNextAsync` calls after the end, exchange count unchanged; an empty page with a non-null next costs one exchange. |
| `PAGE-8` | MUST | met + build | ✅ | Each `GetAsyncEnumerator`/`AsPages()` call is a fresh walk from `first`. Evidence: `ReEnumeration_EachEnumerationRestartsFromFirst`. Build: the engine holds only immutable configuration (`Request`, `RequestOptions`, the strategy); a fetcher walk gets a fresh `PagingOptions` (P7c-16). |
| `PAGE-9` | MUST | build | ✅ | `maxPages` validated by `ArgumentOutOfRangeException.ThrowIfNegativeOrZero` in every factory, at construction. Counts exchanges; stops even with a non-null next. |
| `PAGE-10` | SHOULD | met | ✅ | `maxPages: null` is unbounded. The XML docs and the user page direct production callers to set a finite cap. Test: a 1,000-page walk with no cap. |
| `PAGE-11` | MUST | met | ✅ | Every page is closed before it is yielded, so the item view closes each page before its first item (stronger than the letter). Evidence: `EarlyBreak_FirstBodyDisposed_AndTransportCalledOnce`; ported Node case "taking one item and stopping …". |
| `PAGE-12` | MUST | met by construction | ✅ | §10 entry 17: zero pages are live at any `yield`, so the close-on-abandon guarantee holds even for a hand-driven enumerator dropped without `DisposeAsync` (test). The look-ahead buffer is unreachable (entry 17); the "tell consumers to scope it" clause is the XML doc's `await using`/`await foreach` note. |
| `PAGE-13` | MUST | build | ✅ | Fetch-and-parse runs in a helper whose `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` calls `Disposal.DisposeQuietlyAsync(response, ex)` and rethrows; a dispose failure lands on `ExceptionTrail` with the parse error primary. |
| `PAGE-14` | MUST | build | ✅ | `AsPages()` returns a fresh view whose `GetAsyncEnumerator`/`GetEnumerator` latches with `Interlocked.Exchange` and throws `InvalidOperationException` on the second call (design §7.1). Same for the blocking and fetcher views. |
| `PAGE-15` | MUST | met | ✅ | A success-path dispose failure propagates unwrapped from `MoveNextAsync`/`MoveNext` (single-page case). The "wrap for a terminal that cannot declare the I/O type" clause is vacuous: .NET has no checked exceptions, so the caller catches by type at the close site already. The two-page case is unreachable (entry 17). |
| `PAGE-16` | MUST | build | ✅ | `PaginationStrategies.Cursor`: parameter default `"cursor"`; `null` or empty cursor ends; next = template with the parameter spliced; one body read by construction. |
| `PAGE-17` | MUST | build | ✅ | `PaginationStrategies.PageNumber`: empty items end; current page read from the **executed** request (`response.Request.Url`) with invariant, sign-less base-10 parsing; absent/empty/non-numeric → `startPage` (default 1, ≥ 0 allowed); next = template with `page = current + 1`; `int.MaxValue` ends (P7c-11). |
| `PAGE-18` | MUST | build | ✅ | A new internal `LinkHeaderParser`: angle-bracket and quoted-string aware split, quoted-pair escapes, `rel` quoted or unquoted, SP/HTAB token lists, case-insensitive `next`, first match wins; header name configurable (default `Link`). |
| `PAGE-19` | MUST | build | ✅ | Base is the originating page's response URL (`response.Request.Url`, post-redirect). RFC 3986 resolution through `Uri.TryCreate(base, target)`; a target with a space, control, `<`, `>`, `"` is rejected first (design §7.1: `Uri` accepts `not a url` as a relative path, verified). Unresolvable → end of stream. Plus the kept http(s) scheme guard, userinfo strip and the new cross-origin guard (P7c-12). |
| `PAGE-20` | SHOULD | build | ✅ | Every instance of the header is read (`Headers.GetAll`) and joined with `", "`; no instance → no next link. |
| `PAGE-21` | MUST | build | ✅ (residual: §10 entry 18) | Internal `QuerySplice`: untargeted `&`-segments copied verbatim, name matched **ordinally on the decoded name** (fixes the `?Page=1` case-insensitive bug, design §7.1). The residual is `Uri`'s canonical decoding of percent-encoded unreserved characters, §10 entry 18. |
| `PAGE-22` | MUST | met + build | ✅ | Encoding via `Rfc3986.EncodeComponent` (`%20`, `%2B`; verified). Reading decodes with `Rfc3986.DecodeComponent` (`+` stays `+`), a value-less flag reads `""`, first match wins. A lone surrogate in name or value throws `ArgumentException` naming the parameter, never echoing the value (P7c-10). |
| `PAGE-23` | MUST | build | ✅ | Set replaces the first occurrence in place and drops later duplicates; appends when absent; `null` removes. A whole-URL follow is `template.WithUrl(target)`, keeping method, headers and body. |
| `PAGE-24` | MUST | build | ✅ | The URL is rebuilt from `GetComponents(SchemeAndServer \| UserInfo \| Path, UriEscaped)` + query + fragment, not `UriBuilder`, so no explicit default port is written (verified). Scheme, userinfo, host, port, path and fragment survive. |
| `PAGE-25` | MUST | met | ✅ | No thread blocks per page; the `[EnumeratorCancellation]` token flows into `ExecuteAsync`. New test: cancel mid-walk; the in-flight transport call observes cancellation; no further exchange. |
| `PAGE-26` | MUST | met | ✅ | The token is checked at the top of each page iteration and inside the send, never in the inner item loop, so a fetched page's items all reach the consumer. A fetched-but-undrained page is already closed (entry 17). |
| `PAGE-27` | MUST | met + build | ✅ | Exactly-once close on every path: after parse (success), on parse failure (`PAGE-13`), on the post-cancel discard (`PAGE-33`). The rejected re-dispatch path is N/A (entry 19). Dispose-count tests per path. |
| `PAGE-28` | MUST | met + build | ✅ | `await` never wraps, so the original cause surfaces. Build: a `null` response from the client becomes `InvalidOperationException` naming the client type (the `SEAM-16` wording). Matrix test: transport fault, eager throw, parse throw, consumer throw, null response. |
| `PAGE-29` | MUST | N/A (executor clause) | 🚫 | §10 entry 19: a pull-based `IAsyncEnumerable<T>` invokes no consumer callback, so there is no executor to configure. The serial, ordered, thread-agnostic delivery clauses hold by protocol and are tested. |
| `PAGE-30` | MUST | N/A | 🚫 | §10 entry 19: no executor, so no rejection path. |
| `PAGE-31` | SHOULD | met | ✅ | The walk is a `while` loop in a state machine; a synchronously completing task continues inline. Test: 10,000 synchronously completed pages through both engines, no stack overflow. |
| `PAGE-32` | MUST | met | ✅ | A success-path dispose failure ends the walk with that exception (it does not hang). The "consumer already failed" clause is vacuous: nothing is live when the consumer runs, so no close races its failure. |
| `PAGE-33` | MUST | build | ✅ | Documented in `AsyncPageable<T>`'s remarks and the user page (the transport owns a response it never delivered, `TRANSPORT-9`). Build: if a response arrives after the token was cancelled, the engine disposes it and throws `OperationCanceledException` instead of parsing it (P7c-14). |
| `PAGE-34` | MUST | build | ✅ | `Pageable.FromFetchers<T>`: first fetcher exactly once per walk; next keyed by `NextLink`, else `ContinuationToken`; blank link with no token, or a `null` page, ends; a `null` first page is an empty stream. Ownership per P7c-15. |
| `PAGE-35` | SHOULD | build | ✅ | `PagingOptions`: one mutable instance per walk, passed to every fetcher call; `NextLink`/`ContinuationToken` written before each next call; documented as single-consumer and not thread-safe (P7c-16). |
| `PAGE-36` | MUST | build | ✅ | The factories take `RequestOptions` (default `RequestOptions.Empty`, "no overrides") and pass the same instance to every page's `ExecuteAsync`/`Execute`. Replaces today's `DexpaceClientOptions` argument (P7c-6). |

**Totals.** ✅ 33, 🚫 3 (`PAGE-3`, `PAGE-29`, `PAGE-30`), ⏳ 0. No row travels elsewhere. One ✅ carries a residual
(`PAGE-21`, §10 entry 18), and the blocking engine's rows inherit `PIPE-28`'s transport-level ⏳ against 8b (stated on
the checklist, not a 7c deferral).

---

## As built at `8dc8ee4`

`src/Dexpace.Sdk.Core/Pagination/` holds four files, unchanged since `d45e64b` apart from 4c's compile-only edits:

- `AsyncPageable<T>`: abstract, with abstract `AsPages(int? pageSizeHint = null)` and abstract `GetAsyncEnumerator`.
  `pageSizeHint` is ignored by the only implementation.
- `Page<T>`: sealed class, `Values`, `Status`, `Headers`. No `Request`.
- `Pageable.Create<TPage,T>(HttpPipeline, Request, ISerde, DexpaceClientOptions, selectItems, nextRequest, int? maxPages)`.
  Sends through `SendAsync(Request, DexpaceClientOptions, CancellationToken)`, so a caller's *per-call* `RequestOptions`
  cannot be passed at all. `maxPages` is not validated. Disposal is a bare `finally`, so a dispose failure replaces a
  parse failure (design §7.1, P13). A `null` envelope throws `InvalidOperationException`.
- `PaginationStrategies`: `Cursor<TPage>(nextCursor, queryParameter)` (no default), `PageNumber<TPage>(queryParameter,
  hasMore)` (caller predicate; culture-sensitive `int.TryParse`; start fixed at 1), `LinkHeader<TPage>(rel = "next")`
  (first instance only; space-only `rel` split; no quoted-pair; header name fixed; resolves against the current
  request, not the response URL; accepts `not a url`). The splice matches keys case-insensitively, keeps duplicates, has
  no remove path and rebuilds with `UriBuilder`.

Tests: `tests/Dexpace.Sdk.Core.Tests/Pagination/PageableTests.cs` (24 tests) and `PaginationStrategiesTests.cs` (25 tests),
all `Unit`. `PipelineAsTransportTests` points at `PageableTests` for `PIPE-26`. The AOT smoke has no paging check.

---

## Facts the design rests on

Verified on SDK 10.0.401 with a file-based app in the scratchpad (2026-10-09) unless cited to design §7.1, which verified
the rest on the same SDK.

| # | Fact | Consequence |
|---|---|---|
| 1 | `new Uri("https://u:p@h:8443/a%2Fb/c?x=1#f").GetComponents(SchemeAndServer \| UserInfo \| Path, UriEscaped)` is `https://u:p@h:8443/a%2Fb/c`; for `https://h:443/p` it is `https://h/p`. | The splice rebuilds from components; no `UriBuilder`, no explicit default port (`PAGE-24`). |
| 2 | For `?flag&filter=a:b&page=1&q=a+b&x=%7E%41&r=%2F`, `Uri.Query` is `?flag&filter=a:b&page=1&q=a+b&x=~A&r=%2F`. | Flags, reserved characters, `+` and order survive; `%7E%41` → `~A` is §10 entry 18's residual. |
| 3 | `Uri.TryCreate(new Uri("https://h/repo/issues?page=1"), "not a url", out r)` succeeds with `https://h/repo/not a url`; with `"https://evil/x\ty"` it also succeeds. | The `Link` resolver screens raw targets for SP, HTAB, other controls, `<`, `>`, `"` before resolving (`PAGE-19`). |
| 4 | `new Uri(base, "?page=2")` gives `https://h/repo/issues?page=2`; `Uri.TryCreate(base, "//evil.example/x")` gives `https://evil.example/x`. | Query-only references keep the path (`PAGE-19`); a network-path reference can change origin, so the origin guard is needed (P7c-12). |
| 5 | `Uri.EscapeDataString("a+b/c=")` is `a%2Bb%2Fc%3D`; `Uri.UnescapeDataString("a+b%20c")` is `a+b c`. | `PAGE-22` holds through `Rfc3986`. |
| 6 | `int.TryParse("٣", NumberStyles.None, CultureInfo.InvariantCulture, out _)` and `(" 3", …)` are both `false`. | `PAGE-17` parses ASCII digits only; anything else falls back to `startPage`. |
| 7 | A hand-driven async enumerator dropped without `DisposeAsync` never runs its `finally`, even after forced GCs (design §7.1). | The engine never holds a response across a `yield` (§10 entry 17). |
| 8 | Two `GetAsyncEnumerator` calls on one compiler-generated async iterator start two walks (design §7.1). | Right for the item view (`PAGE-8`), wrong for the page view: `AsPages()` needs a latch (`PAGE-14`). |
| 9 | `AuthorizationPolicy` stamps when the request is same-origin with **its own call's** `PipelineContext.SeedRequest` (`AuthorizationPolicy.cs` lines 156, 218). | Each page is a fresh call whose seed is the page URL, so a server-supplied cross-origin `Link` target would receive the credential. Redirect's seed-origin rule does not cover it (P7c-12). |
| 10 | `Response.Request` is the request the transport executed (`SystemNetHttpClient` line 404 passes its own `request`); after a redirect it is the final hop, with any `Authorization` the auth policy stamped. | Strategies read only `response.Request.Url` (`PAGE-17`, `PAGE-19`) and build the next request from the **template**, never from `response.Request` (P7c-8). |
| 11 | `Rfc3986.EncodeComponent` turns a lone surrogate into U+FFFD's bytes; `Query.Builder` rejects one with `ArgumentException` first. | The splice rejects a lone surrogate the same way rather than silently corrupt a server cursor (P7c-10). |
| 12 | `HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient` explicitly; the seam calls take `RequestOptions`. | The factories can take the seams, and a pipeline passes as either (P7c-5). |
| 13 | `SyncPath.GetCompletedResult(task, owner)` is the sanctioned way to read a `ValueTask` the sync path completed synchronously (`RS0030` bans every other `GetResult`). | One fetch-and-parse helper with an `async` flag serves both engines (P7c-4). |

---

## Approaches considered

Three choices carry the design. For each, the options and the pick.

### 1. The strategy contract (`PAGE-4`, `PAGE-5`, `PAGE-16`, `PAGE-17`)

- **A. Keep design §7.1's two delegates** (`selectItems`, `nextRequest`) and patch the built-ins. `PageNumber` must end on
  an empty item list (`PAGE-17`), but its `nextRequest` delegate cannot see the items, so every page-number caller would
  pass `selectItems` twice (once to the engine, once to the strategy). `PAGE-4`'s "parse output MUST carry items plus a
  next-request" is met only as two outputs from two calls.
- **B. The reference's async parser**: `IPageStrategy<T>.ParseAsync(Response, Request, CancellationToken)`, where the
  strategy reads the body itself. Spec-literal, but each built-in then needs an `ISerde`, the sync engine needs a second
  method, and the single-read guarantee (`PAGE-16`) becomes each strategy's burden rather than the engine's.
- **C. A typed-envelope strategy (picked).** The engine reads the body once into `TPage` through the serde (as today);
  `IPageStrategy<in TPage, T>.Parse(TPage page, Response response, Request template)` returns one `PageInfo<T>(Items,
  NextRequest)`. It is `PAGE-4`'s shape exactly, synchronous over a materialized value (`PAGE-5`), single-read by
  construction (`PAGE-16`), and one strategy object serves both engines. The delegate idiom of §7.1 survives as the
  adapter `PaginationStrategies.Create(items, nextRequest)`. P7c-2.

### 2. Who owns a page's response (`PAGE-3`, `PAGE-12`, `PAGE-15`, `PAGE-34`)

- **A. Live, disposable pages** as the specification describes. Rejected by §10 entry 17 (fact 7): a stranded .NET
  enumerator has no close a caller can reach.
- **B. Materialized pages (picked, unchanged).** Entry 17 stands; 7c extends it to the fetcher front-end (P7c-15).

### 3. The blocking engine (`PAGE-6`, the card's "blocking `Pageable<T>` over 4c's synchronous path")

- **A. A wrapper that blocks on `AsyncPageable<T>`.** Sync-over-async, banned by `RS0030` outside the documented bridges,
  and the thread-pool starvation design §7.1 warns about.
- **B. A second, hand-written sync engine.** Two copies of fetch, parse, close and cap logic would drift.
- **C. One fetch-and-parse step with an `async` flag (picked).** `PageStep.FetchAsync(…, async)` calls `ExecuteAsync` or
  `Execute`, reads the body async or sync, parses, and disposes; with `async: false` it never awaits, and the blocking
  iterator reads it through `SyncPath.GetCompletedResult` (4c's pattern, fact 13). Only the two iterator shells differ,
  and each is a dozen lines. P7c-4.

---

## The design

### The engine

One internal step, two iterator shells, three factories.

```text
PageStep.FetchAsync<TPage,T>(client, request, template, serde, strategy, options, async, ct) -> (Page<T>, Request? next)
    response = async ? await client.ExecuteAsync(request, options, ct) : client.Execute(request, options, ct)
    response ?? throw InvalidOperationException("The client <type> returned a null response (SEAM-16).")   // PAGE-28
    if ct.IsCancellationRequested: dispose response quietly; throw OperationCanceledException(ct)           // PAGE-33 (P7c-14)
    try
        envelope = async ? await serde.DeserializeAsync<TPage>(body stream) : serde.Deserialize<TPage>(body.ReadAsBytes())
        envelope ?? throw DeserializationException("… page type '<TPage>' … null")                          // P7c-17
        info = strategy.Parse(envelope, response, template) ?? throw InvalidOperationException(<strategy type>) // PAGE-4
        page = new Page<T>(snapshot(info.Items), response.Status, response.Headers, request)                 // PAGE-2, PAGE-11
    catch (ex) when !IsFatal(ex)
        Disposal.DisposeQuietly[Async](response, ex); rethrow                                                 // PAGE-13
    dispose response (a failure propagates)                                                                    // PAGE-15, PAGE-32
    return (page, info.NextRequest)
```

The async shell (`StrategyPageable<TPage,T>.WalkPagesAsync`, an `async IAsyncEnumerable<Page<T>>` with
`[EnumeratorCancellation]`):

```text
current = first; fetched = 0
loop:
    ct.ThrowIfCancellationRequested()                       // PAGE-26: page granularity
    if maxPages is set and fetched == maxPages: yield break  // PAGE-9
    (page, next) = await PageStep.FetchAsync(..., async: true, ct)
    fetched++
    yield return page                                        // nothing live across this yield (entry 17)
    if next is null: yield break                             // PAGE-4, PAGE-7
    current = next
```

The blocking shell (`StrategyBlockingPageable<TPage,T>.WalkPages`, an `IEnumerable<Page<T>>` iterator) is the same loop
over `SyncPath.GetCompletedResult(PageStep.FetchAsync(..., async: false, token), nameof(Pageable))`, with the token the
factory captured.

`try`/`catch` cannot surround a `yield return` in C#, so the step lives outside the iterators; that also keeps each method
inside `MA0051`'s 70 lines.

**`Page<T>.Values` is a snapshot** (`Array.AsReadOnly([.. info.Items])`): a strategy that returns a live view of the
envelope cannot change a page after it was yielded (`PAGE-2`, `PAGE-11`'s "after copying the materialized items").

**The template's body must be replayable.** Every page re-sends the template's body (a POST-based search), so the
factories throw `ArgumentException` (`ParamName` `first`) when `first.Body is { IsReplayable: false }`, naming
`ToReplayableAsync` (P7c-13). Fail fast at construction rather than `StreamConsumedException` on page 2.

### The base types and the single-use page view

```csharp
public abstract class AsyncPageable<T> : IAsyncEnumerable<T>
{
    protected AsyncPageable();
    public IAsyncEnumerable<Page<T>> AsPages();                                   // non-virtual: fresh, single-use view (PAGE-14)
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default); // non-virtual: flattens a fresh walk (PAGE-1, PAGE-8)
    protected abstract IAsyncEnumerable<Page<T>> WalkPagesAsync(CancellationToken cancellationToken);
}

public abstract class Pageable<T> : IEnumerable<T>
{
    protected Pageable();
    public IEnumerable<Page<T>> AsPages();
    public IEnumerator<T> GetEnumerator();
    protected abstract IEnumerable<Page<T>> WalkPages();
}
```

`AsPages()` and `GetAsyncEnumerator` are non-virtual so that `PAGE-11` and `PAGE-14` hold for every subclass, including a
test double. A subclass supplies one fresh walk per call of `WalkPagesAsync`; the remarks state the one obligation that
cannot be enforced: hold no live response across a `yield`. `AsPages()` wraps the walk in an internal
`SingleUseAsyncEnumerable<Page<T>>` (an `int` latch set with `Interlocked.Exchange`; the second
`GetAsyncEnumerator` throws `InvalidOperationException`: "This page view is single-use; call AsPages() again for a new
walk."). The blocking twin is the same over `IEnumerable<T>`. `pageSizeHint` is removed (P7c-3).

`Pageable<T>` (arity 1) sits beside the static `Pageable` factory class (arity 0), which C# permits (design §7.1).

### The factories

```csharp
public static class Pageable
{
    public static AsyncPageable<T> Create<TPage, T>(
        IAsyncHttpClient client, Request first, ISerde serde, IPageStrategy<TPage, T> strategy,
        RequestOptions? options = null, int? maxPages = null);

    public static Pageable<T> CreateBlocking<TPage, T>(
        IHttpClient client, Request first, ISerde serde, IPageStrategy<TPage, T> strategy,
        RequestOptions? options = null, int? maxPages = null, CancellationToken cancellationToken = default);

    public static AsyncPageable<T> FromFetchers<T>(
        Func<PagingOptions, CancellationToken, ValueTask<FetchedPage<T>?>> firstPage,
        Func<string, PagingOptions, CancellationToken, ValueTask<FetchedPage<T>?>> nextPage,
        int? maxPages = null);
}
```

Every factory null-checks with `ArgumentNullException.ThrowIfNull`, validates `maxPages` with
`ArgumentOutOfRangeException.ThrowIfNegativeOrZero` when it has a value (`PAGE-9`), and checks the template's body
(P7c-13). `options ?? RequestOptions.Empty` is captured once; the same instance reaches every page (`PAGE-36`). The client
is never disposed by the pageable.

Distinct names, not overloads, for the blocking factory: an `HttpPipeline` argument converts to both `IAsyncHttpClient`
and `IHttpClient`, so two `Create` overloads differing only in that parameter would be ambiguous (`CS0121`) for the most
common caller. `CreateBlocking` matches the SDK's `AsBlocking` vocabulary.

### The strategy contract and the built-ins

```csharp
public interface IPageStrategy<in TPage, T>
{
    PageInfo<T> Parse(TPage page, Response response, Request template);
}

public sealed class PageInfo<T>
{
    public PageInfo(IReadOnlyList<T> items, Request? nextRequest);   // items null → ArgumentNullException (PAGE-2)
    public IReadOnlyList<T> Items { get; }
    public Request? NextRequest { get; }                             // null is the only end signal (PAGE-4)
}

public static class PaginationStrategies
{
    public static IPageStrategy<TPage, T> Create<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items, Func<TPage, Response, Request, Request?> nextRequest);
    public static IPageStrategy<TPage, T> Cursor<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items, Func<TPage, string?> nextCursor, string queryParameter = "cursor");
    public static IPageStrategy<TPage, T> PageNumber<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items, string queryParameter = "page", int startPage = 1);
    public static IPageStrategy<TPage, T> LinkHeader<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items, string headerName = "Link", bool allowCrossOrigin = false);
}
```

`response` reaches `Parse` with its body already read; a strategy reads status, headers and `response.Request.Url`.
`template` is the walk's `first` request. Each built-in is an internal sealed class holding only its configuration
(`PAGE-5`), validated at construction: names non-null and non-empty and free of lone surrogates, `startPage >= 0`
(`ArgumentOutOfRangeException.ThrowIfNegative`).

- **Cursor** (`PAGE-16`): `cursor = nextCursor(page)`; `string.IsNullOrEmpty(cursor)` → `NextRequest = null`; otherwise
  `template.WithUrl(QuerySplice.Set(template.Url, queryParameter, cursor))`.
- **PageNumber** (`PAGE-17`): `items(page).Count == 0` → end. Otherwise read `QuerySplice.Get(response.Request.Url,
  queryParameter)`; parse with `int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out n)`; failure or
  absence → `current = startPage`; `current == int.MaxValue` → end (P7c-11); next = template with `current + 1`.
- **LinkHeader** (`PAGE-18`–`PAGE-20`): `values = response.Headers.GetAll(headerName)`; none → end; `raw =
  LinkHeaderParser.FindNext(string.Join(", ", values))`; `LinkTarget.TryResolve(response.Request.Url, raw, template.Url,
  allowCrossOrigin, out target)` → `template.WithUrl(target)` or end.
- **Create**: `PageInfo<T>(items(page), nextRequest(page, response, template))`.

### The query splice (`PAGE-21`–`PAGE-24`)

Internal `QuerySplice` in `Pagination/`:

- `string? Get(Uri url, string name)`: walks the raw query (`url.Query` without `?`) in `&`-segments, skipping empty
  ones (the `HTTP-31` leniency Node also takes); a segment's name is the text before the first `=`, **decoded** with
  `Rfc3986.DecodeComponent` and compared to `name` with `StringComparison.Ordinal`; returns `""` for a value-less flag,
  the decoded value otherwise, `null` when absent. First match wins.
- `Uri Set(Uri url, string name, string? value)`: the same walk; untargeted segments are appended verbatim; the first
  match is replaced by `Encode(name)=Encode(value)` (or dropped when `value` is `null`); later matches are dropped; an
  absent name is appended. The URL is rebuilt as `GetComponents(SchemeAndServer | UserInfo | Path, UriEscaped)` +
  (`?` + query when non-empty) + (`#` + `GetComponents(Fragment, UriEscaped)` when the source had one), then `new
  Uri(…, UriKind.Absolute)`.
- A lone surrogate in `name` or `value` throws `ArgumentException` whose message names the parameter and never the value
  (P7c-10). From a strategy, that surfaces through `PAGE-13`'s path.

Matching on the *decoded* name rather than the encoded one (Node compares encoded forms) makes `?my%20key=1` and `?my
key=1` the same parameter, which is what a reader of `Get` expects; `Uri` already canonicalizes unreserved escapes, so the
two choices differ only for reserved-character names. P7c-9.

### The `Link` header (`PAGE-18`–`PAGE-20`)

Internal `LinkHeaderParser.FindNext(string value)` is a single-pass scanner over the joined value:

1. Skip OWS and stray commas; expect `<`; read to the matching `>` (commas inside are data).
2. Read `;`-separated parameters: `name`, optional `=`, then a token or a quoted-string. A quoted-string honours
   quoted-pair (`\"`, `\\`) and its commas and semicolons are data.
3. For a `rel` parameter (name compared case-insensitively), split the value on SP and HTAB; if any token equals `next`
   case-insensitively, return the URI-reference. Only the first `rel` of a link-value counts (RFC 8288 §3.3).
4. A malformed link-value (no `<`, unterminated `>` or quote) is skipped to the next top-level comma, never thrown.

Internal `LinkTarget.TryResolve(Uri responseUrl, string raw, Uri templateUrl, bool allowCrossOrigin, out Uri? target)` is
total (never throws, like 6b's `RedirectLocation`): trim; reject when `raw` holds SP, any control, `<`, `>` or `"` (fact
3); `Uri.TryCreate(responseUrl, raw, out created)` inside a catch for `UriFormatException`/`ArgumentException`/
`InvalidOperationException`; require absolute http(s) with a non-empty `IdnHost` (the kept scheme guard); drop userinfo
with `GetComponents(AbsoluteUri & ~UserInfo, UriEscaped)` (the `REDIR-12` analogue: a server must not be able to plant
credentials in the URL); finally, unless `allowCrossOrigin`, require `HttpOrigin.From(created) ==
HttpOrigin.From(templateUrl)` (P7c-12). Any failure ends the stream (`PAGE-19`).

### The fetcher front-end (`PAGE-34`, `PAGE-35`)

```csharp
public sealed record FetchedPage<T>(Page<T> Page, string? NextLink = null, string? ContinuationToken = null);

public sealed class PagingOptions
{
    public string? NextLink { get; set; }
    public string? ContinuationToken { get; set; }
    public IDictionary<string, object?> State { get; }     // per-walk scratch for custom retrievers
}
```

Walk (internal `FetcherPageable<T>.WalkPagesAsync`): a new `PagingOptions` per walk (`PAGE-8`); `fetched = await
firstPage(options, ct)` exactly once; `null` → empty stream. Then per page: cap check, `yield return fetched.Page`; the key
is `NextLink` when not `string.IsNullOrWhiteSpace`, else `ContinuationToken` when not null or empty, else end; write both
onto `options` (Node's behaviour, so a retriever can read either), then `await nextPage(key, options, ct)`; `null` → end.
A `null` `FetchedPage.Page` is rejected by the record's constructor (`ArgumentNullException`).

Ownership: `Page<T>` holds no response (entry 17), so a fetcher reads its response, builds the page, and disposes the
response itself, typically with `await using` (P7c-15). The engine never sees a response, so a fetcher that throws
before building its page still owns whatever it opened, which is `PAGE-34`'s last clause verbatim.

### `Page<T>`

```csharp
public sealed class Page<T>
{
    public Page(IReadOnlyList<T> values, Status status, Headers headers, Request request);
    public IReadOnlyList<T> Values { get; }
    public Status Status { get; }
    public Headers Headers { get; }
    public Request Request { get; }      // the request the walk sent for this page (P7c-7)
}
```

Still not disposable, still a sealed class (not a record: value equality over an `IReadOnlyList` would be reference
equality and mislead).

### Cancellation and the documented race (`PAGE-25`, `PAGE-26`, `PAGE-33`)

The item view's token and `WithCancellation`'s are combined by `[EnumeratorCancellation]`. The token is observed at the top
of each page and passed to `ExecuteAsync` and the deserializer; it is not observed between items of a fetched page
(`PAGE-26`). The remarks on `AsyncPageable<T>` and the user page document `PAGE-33`'s race: a response the transport
builds after the cancel is never delivered to the pageable and is the transport's to release (`TRANSPORT-9`); a response
that *is* delivered after the token was cancelled is disposed and discarded (P7c-14).

---

## The public surface (`PublicAPI.Unshipped.txt`)

All in `Dexpace.Sdk.Core`, namespace `Dexpace.Sdk.Core.Pagination`. Every member gets a `///` comment citing its IDs.

**Removed** (rows deleted from `PublicAPI.Unshipped.txt`):

- `abstract AsyncPageable<T>.AsPages(int? pageSizeHint = null)`
- `abstract AsyncPageable<T>.GetAsyncEnumerator(CancellationToken)`
- `Page<T>.Page(IReadOnlyList<T>!, Status, Headers!)`
- `static Pageable.Create<TPage, T>(HttpPipeline!, Request!, ISerde!, DexpaceClientOptions!, Func<…>!, Func<…>!, int?)`
- `static PaginationStrategies.Cursor<TPage>(…)`, `.PageNumber<TPage>(…)`, `.LinkHeader<TPage>(string! rel = "next")`

**Added:**

- `AsyncPageable<T>`: `AsyncPageable() -> void` (protected), `AsPages() -> IAsyncEnumerable<Page<T>!>!`,
  `GetAsyncEnumerator(CancellationToken = default) -> IAsyncEnumerator<T>!`, `abstract WalkPagesAsync(CancellationToken)
  -> IAsyncEnumerable<Page<T>!>!` (protected).
- `Pageable<T>`: the type, `Pageable() -> void` (protected), `AsPages() -> IEnumerable<Page<T>!>!`, `GetEnumerator() ->
  IEnumerator<T>!`, `abstract WalkPages() -> IEnumerable<Page<T>!>!` (protected).
- `Page<T>`: `Page(IReadOnlyList<T>!, Status, Headers!, Request!)`, `Request.get -> Request!`.
- `IPageStrategy<TPage, T>`, `IPageStrategy<TPage, T>.Parse(TPage, Response!, Request!) -> PageInfo<T>!`.
- `PageInfo<T>`: the type, its constructor, `Items.get`, `NextRequest.get -> Request?`.
- `FetchedPage<T>`: the record and its compiler-generated members (constructor, `Page`, `NextLink`, `ContinuationToken`,
  `init` accessors, `Deconstruct`, equality members, `<Clone>$`), as the analyzer lists them.
- `PagingOptions`: the type, constructor, `NextLink` get/set, `ContinuationToken` get/set, `State.get`.
- `static Pageable.Create<TPage, T>(IAsyncHttpClient!, Request!, ISerde!, IPageStrategy<TPage, T>!, RequestOptions? = null,
  int? = null) -> AsyncPageable<T>!`, `static Pageable.CreateBlocking<TPage, T>(IHttpClient!, …, CancellationToken =
  default) -> Pageable<T>!`, `static Pageable.FromFetchers<T>(…) -> AsyncPageable<T>!`.
- `static PaginationStrategies.Create<TPage, T>`, `.Cursor<TPage, T>`, `.PageNumber<TPage, T>`, `.LinkHeader<TPage, T>`,
  each `-> IPageStrategy<TPage, T>!`.

The splice is **not** public in v1 (P7c-9): custom strategies use `Request.WithUrl` and their own query handling, or
`PaginationStrategies.Create` over a cursor they compute. Making `QuerySplice` public is a hand-off.

### Internal types

`PageStep` (the shared fetch-and-parse step), `StrategyPageable<TPage,T>`, `StrategyBlockingPageable<TPage,T>`,
`FetcherPageable<T>`, `SingleUseAsyncEnumerable<T>`, `SingleUseEnumerable<T>`, `CursorStrategy<TPage,T>`,
`PageNumberStrategy<TPage,T>`, `LinkHeaderStrategy<TPage,T>`, `DelegateStrategy<TPage,T>`, `QuerySplice`,
`LinkHeaderParser`, `LinkTarget`. All in `src/Dexpace.Sdk.Core/Pagination/`, all `internal sealed` or `internal static`.
`HttpOrigin` (2a/6b) is reused, not copied.

---

## Breaking changes

All pre-release; the PR is `feat!`.

1. `Pageable.Create` takes `IAsyncHttpClient` (an `HttpPipeline` still converts), `IPageStrategy<TPage,T>` instead of the
   two delegates, and `RequestOptions?` instead of `DexpaceClientOptions`. Migration: `PaginationStrategies.Create(selectItems,
   nextRequest)` wraps the old delegates; a caller who passed different client options builds a pipeline with them.
2. `PaginationStrategies.*` gain the item selector and a second type parameter; `Cursor`'s parameter name defaults to
   `"cursor"`; `PageNumber` loses `hasMore` (it ends on an empty page, per `PAGE-17`) and gains `startPage`; `LinkHeader`
   loses `rel` and gains `headerName` and `allowCrossOrigin`.
3. `PageNumber` behaviour: a non-empty page with no more data now costs one extra (empty) exchange where a `hasMore`
   predicate used to stop early. A caller who wants the predicate writes `PaginationStrategies.Create`.
4. `LinkHeader` behaviour: a cross-origin `next` target, a `not a url` target and a userinfo-bearing target now end the
   stream (the last is followed with userinfo removed); every `Link` instance is read; the base is the response URL.
5. The splice matches parameter names case-sensitively and drops duplicate occurrences.
6. `AsyncPageable<T>.AsPages()` loses `pageSizeHint`, becomes non-virtual and single-use per returned view.
   `GetAsyncEnumerator` becomes non-virtual; subclasses override `WalkPagesAsync`.
7. `Page<T>`'s constructor requires the `Request`.
8. `maxPages <= 0` throws at construction; a template with a non-replayable body throws at construction.
9. A `null` envelope throws `DeserializationException` instead of `InvalidOperationException`.

---

## Tests, vectors and ports

Every new test class carries `[Trait("Category", …)]` (`TestCategoryTests` enforces it). Core tests reference core and
`Dexpace.Sdk.TestSupport` fakes only (`SEAM-2`): `ScriptedTransport`, `RecordingTransport`, `RecordingSyncTransport`,
`SyncFirstTransport`, `TrackingResponseBody`, `DisposalCountingBody`. Assertions are xUnit `Assert` only (the testing
note).

| File (under `tests/`) | Category | Covers | Ported from |
|---|---|---|---|
| `Dexpace.Sdk.Core.Tests/Pagination/QuerySpliceTests.cs` | Unit | `PAGE-21`–`PAGE-24`; the `?Page=1` case-sensitivity regression; lone-surrogate rejection naming the parameter only; no explicit default port; fragment and userinfo survive | Node `query-splice.test.ts` (22 cases) |
| `Dexpace.Sdk.Core.Tests/Pagination/QuerySplicePropertyTests.cs` | Unit | untargeted segments byte-identical; write-then-read identity for any well-formed string; no exception other than `ArgumentException` for any server string. **Seeded** `System.Random` generator, 500 cases per property, seed in the test name (P7c-18) | Node `query-splice.property.test.ts` |
| `Dexpace.Sdk.Core.Tests/Pagination/LinkHeaderParserTests.cs` + `tests/vectors/pagination/link-header.json` | Unit | `PAGE-18`, `PAGE-20`: quoted and unquoted `rel`, case, SP/HTAB lists, first match, decoys, commas in `<…>` and in quoted values, quoted-pair, absent header, split instances, whitespace | Node `link-header.test.ts` (15 cases); the vector file names `nodejs-sdk/packages/core/src/pagination/link-header.test.ts` and `c0ff3fd` (constraint 10) |
| `Dexpace.Sdk.Core.Tests/Pagination/LinkTargetTests.cs` | Unit | `PAGE-19`: absolute, path-relative, query-only (`/repo/issues?page=2`), `<not a url>` ends, controls/`<>"` rejected, non-http schemes end, userinfo stripped, cross-origin and network-path targets end unless allowed, HTTPS→HTTP ends (origin differs) | Node `strategies.test.ts` PAGE-19 cases (diverging on `<not a url>`, P7c-12 note) |
| `Dexpace.Sdk.Core.Tests/Pagination/PaginationStrategiesTests.cs` (rewritten) | Unit | `PAGE-4`, `PAGE-5` (one instance, two concurrent walks), `PAGE-16`, `PAGE-17` (executed request, garbage value, 0-based, `int.MaxValue`, invariant digits), `PAGE-23` (template method/headers/body kept), constructor validation | Node `strategies.test.ts`, `strategy.test.ts` |
| `Dexpace.Sdk.Core.Tests/Pagination/PageableTests.cs` (rewritten) | Unit | `PAGE-1`, `PAGE-2`, `PAGE-6`–`PAGE-10`, `PAGE-14`, `PAGE-25`–`PAGE-28`, `PAGE-31` (10,000 sync pages), `PAGE-33` (late response discarded and disposed once), `PAGE-36` (`RecordingTransport` sees the same `RequestOptions` instance on every page), replayable-body check | Node `paginator.test.ts`, `cancellation.test.ts`; existing facts kept where still true |
| `Dexpace.Sdk.Core.Tests/Pagination/PaginationLifecycleTests.cs` | Unit | `PAGE-11`, `PAGE-12` (hand-driven enumerator abandoned after page 1: disposed count equals fetched count), `PAGE-13` (parse throws; parse and dispose both throw → dispose on `ExceptionTrail.GetSuppressed`), `PAGE-15`/`PAGE-32` (success-path dispose failure ends the walk), `PAGE-27` (exactly-once per path), `PAGE-4` (null `PageInfo` closes and throws) | Node `lifecycle.test.ts` (13 cases), adapted to entry 17: "closes the held page" becomes "no response is open at any yield" |
| `Dexpace.Sdk.Core.Tests/Pagination/BlockingPageableTests.cs` | Unit | `PAGE-6` (three zero-exchange probes), `PAGE-1`/`PAGE-7`/`PAGE-9`/`PAGE-14` on the sync path, `PAGE-31` sync, the token captured by `CreateBlocking`, `RecordingSyncTransport` shows `Execute` (not `ExecuteAsync`) was called | — |
| `Dexpace.Sdk.Core.Tests/Pagination/FetcherPageableTests.cs` | Unit | `PAGE-34`, `PAGE-35`, `PAGE-9` and `PAGE-14` for fetchers, a fresh `PagingOptions` per walk | Node `fetchers.test.ts` (12 cases) |
| `Dexpace.Sdk.Core.Tests/Architecture/PaginationArchitectureTests.cs` | Unit | `Page<T>`, `PageInfo<T>`, `FetchedPage<T>` are not `IDisposable`/`IAsyncDisposable` (entry 17); `AsPages`/`GetAsyncEnumerator`/`GetEnumerator` are non-virtual on both bases; no public type in the namespace exposes a `Response`-typed property | — |
| `Dexpace.Sdk.Core.Tests/Security/PaginationLinkOriginTests.cs` | **Security** | A pipeline with `BearerTokenPolicy` over a `ScriptedTransport`; page 1 sends `Link: <https://evil.example/p2>; rel=next`; the walk ends after one exchange and no request reaches `evil.example`. Also a `//evil.example` network-path and an `https`→`http` target. Tagged `Security` on 6b's P6b-25 precedent | — (no sibling has the guard) |
| `Dexpace.Sdk.Http.SystemNet.Tests/PaginationWireTests.cs` | Integration | Over the loopback server: a three-page `Link` walk with the links split across two header lines and a query-only reference; a cursor walk whose `RequestOptions.Timeout` governs every page. Pins that `SystemNetHttpClient` surfaces each `Link` line as its own value | — |

**Existing tests edited.** `PipelineAsTransportTests`'s comment (it points at `PageableTests` for `PIPE-26`) stays true; the
rewritten `PageableTests` keeps a fact named for `PIPE-26` that passes an `HttpPipeline` as the client. If
`ModelImmutabilityArchitectureTests` takes closed generic types, `PageInfo<int>` and `Page<int>` join its list; `PagingOptions`
is excluded by name with the `PAGE-35` rationale (plan decides after reading the test's type filter).

**The AOT smoke** (`tests/Dexpace.Sdk.AotSmoke`) gains `CheckPhase7cPaginationAsync`: a three-page cursor walk through
`DexpacePipeline` over an in-memory `DelegateHttpClient`, deserialized by the STJ serde with a source-generated
`JsonTypeInfo` for a `WidgetPage` envelope in its **own** `JsonSerializerContext` (`PaginationSmokeContext`, so 7a's
context is not edited); asserts item order, `Page<T>.Request`, that a second `GetAsyncEnumerator` on one `AsPages()` view
throws, a `Link`-header walk, and the same cursor walk through `CreateBlocking`. This is 7c's share of the exit's "JSON,
Tristate, paged and SSE round trip".

**Ruby ports.** The card's `ruby-sdk/.../page/` tests and `page_fixtures.rb` are not in the local Ruby checkout (risk R3).
The Ruby 7c design's fixture descriptions (3-page fixture, echoing-cursor server, quoted-comma `Link` values) are covered
by the Node ports above; the plan fetches the Ruby files from GitHub only if the Node ports leave a row without a
conformance case.

---

## Cross-sub-phase interfaces with 7a and 7b

| Touch point | 7c's assumption | If the sibling lands first |
|---|---|---|
| `ResponseBodySerdeExtensions.ReadValueAsync` (7a may change its `null` handling, `SERDE-13`) | 7c does not call it; `PageStep` opens the body and calls `ISerde.DeserializeAsync`/`Deserialize` itself and throws `DeserializationException` naming `TPage` on a root `null` | If 7a adds a shared root-null helper, the plan switches `PageStep` to it (a convenience, not a dependency). |
| `src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt` | Shared file. 7c's lines are all `Dexpace.Sdk.Core.Pagination.*` (or `static`/`abstract` prefixed of the same), a contiguous sorted run, so a rebase conflict is mechanical | Rebase and re-sort. |
| `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`, `SmokeModels.cs` | 7c adds one `CheckPhase7cPaginationAsync` method, one line in `RunAllAsync`, and its own `PaginationSmokeContext` | Resolve the one `RunAllAsync` line. |
| `CHANGELOG.md`, `CLAUDE.md`, roadmap status notes, `src/Dexpace.Sdk.Core/README.md` | 7c edits only its own bullets/rows | Rebase. |
| `Directory.Packages.props` | 7c adds **no** package (P7c-18), so 7b's possible FsCheck addition cannot conflict with it | — |
| `Sse37ArchitectureTests` (7b) | 7c adds no reference from `Pagination` to SSE or vice versa | — |

---

## Hand-offs to later phases

- **8a (conformance kit):** the `link-header.json` vector and the splice properties are candidates for the shared kit.
- **8b:** the blocking pager becomes truly synchronous end to end when `SystemNetHttpClient.Execute` calls
  `HttpClient.Send` **and** `HttpResponseMessageBody` overrides `ResponseBody.OpenRead` (corrected 2026-10-09: without
  the override `CreateBlocking` over `SystemNetHttpClient` throws `NotSupportedException` on the first page, pinned by
  `PaginationWireTests.A_blocking_walk_over_the_real_transport_throws_NotSupportedException_until_8b_adds_OpenRead`,
  which 8b flips to a passing walk); no 7c source changes. *Dated correction, 2026-10-09 (rebase onto 7b):* 7b built
  `HttpResponseMessageBody.OpenRead`, so that half is done and the wire test is already flipped
  (`PaginationWireTests.A_blocking_walk_over_the_real_transport_reads_every_page`); 8b's remaining share is
  `SystemNetHttpClient.Execute` over `HttpClient.Send` (and `RequestOptions.Timeout`), still with no 7c source change.
- **9 (DI):** nothing; pageables are per-call objects.
- **12 (release):** `docs/first-release.md` gains a "Behavioural asymmetries" entry for the `Link` cross-origin guard and
  `allowCrossOrigin`, and the public-splice question (P7c-9) is listed as a post-1.0 candidate.

---

## Design, roadmap and `CLAUDE.md` corrections owed at close-out

Proposals; 7c's PR applies them as dated corrections.

1. **Design §7.1, dated correction (P7c-2, P7c-5, P7c-6, P7c-12, P7c-15).** The strategy contract is
   `IPageStrategy<TPage,T>` returning `PageInfo<T>`, with the two-delegate shape kept as `PaginationStrategies.Create`;
   the factories take the transport seams and `RequestOptions`; the fetcher front-end returns `FetchedPage<T>` rather than
   `Page<T>` carrying `ContinuationToken`/`NextLink`; the `Link` strategy adds a cross-origin guard and a userinfo strip.
   "As built (phase 7c)" verdict appended.
2. **§10 entry 17, dated amendment (P7c-15).** Touches `PAGE-34`'s ownership clause as well: a fetcher disposes its own
   response, because the page it returns owns none.
3. **§10 entry 18, dated amendment.** The `PAGE-24` clause ("insofar as `UriBuilder` writes an explicit default port") is
   retired: the splice no longer uses `UriBuilder` (fact 1). The `PAGE-21` residual stands.
4. **§11, new item (P7c-12).** `PAGE-19` says "absolute targets are used as-is"; the port ends the stream on a cross-origin
   target unless `allowCrossOrigin` is set, because 6c stamps credentials per call against each page's own seed (fact 9).
   Stronger than the letter; recorded as a reading, not a §10 deviation, because a server cannot observe it except by not
   being followed.
5. **§12's PAGE row:** the "As built" counts.
6. **`CLAUDE.md`:** the `Pagination/` layout line becomes `AsyncPageable<T>, Pageable<T>, Page<T>, PageInfo<T>,
   IPageStrategy, Pageable (Create, CreateBlocking, FromFetchers), PaginationStrategies, PagingOptions, FetchedPage<T>`;
   the "What is genuinely unbuilt" sentence drops "the remaining pagination surface (7)" and gains a pointer to
   `docs/sdk-documentation/pagination.md`.
7. **`src/Dexpace.Sdk.Core/README.md`:** the `Dexpace.Sdk.Core.Pagination` row.
8. **Roadmap:** a dated Phase Status Note; the Phase List row 7 gains the 7c design, plan and checklist links.

---

## Risks and open questions (resolved)

| # | Risk or question | Resolution |
|---|---|---|
| R1 | Parallel edits by 7a/7b to shared files (`PublicAPI.Unshipped.txt`, the AOT smoke, `CHANGELOG.md`, `CLAUDE.md`) | Contained to contiguous, sub-phase-owned blocks; own `JsonSerializerContext` in the smoke; see the interface table. |
| R2 | 7a may change root-`null` deserialization semantics underneath the engine | 7c does its own null check through the seam; converges if 7a lands a helper. |
| R3 | The Ruby test sources the card names are absent from the local checkout (`90075b1` has no `gems/`) | Port from Node at `c0ff3fd`, which covers every row's conformance clause; vectors cite Node. Fetch Ruby from GitHub only for a gap. |
| R4 | FsCheck is not a pinned package, and 7b may add it in parallel | 7c's property tests use a seeded `System.Random` generator; no package, no lock-file churn (P7c-18). |
| R5 | The blocking pager is sync-over-async inside `SystemNetHttpClient` until 8b | Stated on the checklist as `PIPE-28`'s inherited ⏳, not a 7c gap. The pager itself never blocks on a task. **Corrected 2026-10-09:** over `SystemNetHttpClient` the blocking pager does not merely wait on a thread, it fails with `NotSupportedException` (no `OpenRead` on the transport's body until 8b); the docs say to use `Pageable.Create` there, and a wire test pins the failure. **Corrected again 2026-10-09 (rebase onto 7b):** 7b's `HttpResponseMessageBody.OpenRead` removed the failure; the blocking pager walks over `SystemNetHttpClient` and the wire test pins the walk. The sync-over-async `Execute` remains, as originally stated. |
| R6 | The blocking path buffers each page through `ReadAsBytes`, so it inherits 3a's 64 MiB materialisation cap; the async path streams | Accepted and documented on `CreateBlocking`: a page envelope over 64 MiB is not a paging use case. `ISerde` has no sync stream overload to avoid it. **Corrected 2026-10-09 (rebase onto 7a):** 7a added `ISerde.Deserialize<T>(Stream)` (a default interface member that buffers under the same cap; `SystemTextJsonSerde` overrides it and streams), so the overload now exists. `PageStep` keeps `BodyMaterializer.ReadAll` (which also refuses a declared length over the cap before reading, pinned by `BlockingPageableTests`); moving the blocking read onto the stream decode is a possible follow-up, not a 7c gap. |
| R7 | The cross-origin guard stops APIs that legitimately page across hosts (a CDN or a regional host) | `allowCrossOrigin: true` opts in; its XML doc warns that per-call auth will stamp the credential for the new origin. **Open for the lead** (P7c-12). |
| R8 | An ended `Link` walk (rejected target) is silent, which can hide a misconfigured server | Accepted for v1: strategies have no logger and `PAGE-19` mandates a quiet end. Noted in the user page; a debug event is a later option. |
| R9 | `Response.Request` after a redirect carries the auth policy's stamped headers | Strategies read only its URL; the next request is built from the template (P7c-8), and `Page<T>.Request` is the pre-pipeline request (P7c-7), so no credential is retained on a page. |
| Q1 | Should `AsyncPageable<T>` stay abstract and subclassable? | Yes: it is the mocking seam for callers' tests; the non-virtual public members keep the guarantees. |
| Q2 | Should the blocking engine also get a fetcher front-end? | No (P7c-16 note): `PAGE-34` names one front-end; a blocking fetcher is a `Pageable<T>` subclass away. |
| Q3 | Does the walk need its own span or log events? | No: each page is a pipeline call with its own operation span (5c); no OBS row asks for a walk-level signal. |

---

## Decisions

Taken by this design in the absence of a human reviewer. **Open for the lead:** P7c-3, P7c-6, P7c-12, P7c-16. The rest are
taken.

1. **P7c-1 — Row ownership.** 7c owns `PAGE-1`–`PAGE-36` (36) and nothing else; it contributes tests to `PIPE-26` and
   `SERDE-13`, whose rows stay with 4c and 7a. Options: taking `SERDE-13` because 7c raises the exception (rejected: the card
   lists root-`null` rejection under 7a).
2. **P7c-2 — The strategy contract is `IPageStrategy<in TPage, T>.Parse(TPage, Response, Request template) ->
   PageInfo<T>`.** Rationale: `PAGE-4`'s single output; `PAGE-17` needs the items and the next decision in one place;
   one synchronous method serves both engines; single read by construction (`PAGE-16`). The two-delegate shape stays
   available as `PaginationStrategies.Create`. Options: §7.1's delegates (rejected: `PageNumber` would take the selector
   twice); an async body-reading parser (rejected: pushes I/O and the single-read rule into every strategy). An interface,
   not a delegate type, because the SDK's extension points are interfaces (`CLAUDE.md`, "Interfaces for SPIs") and a
   strategy with configuration reads better as an object.
3. **P7c-3 — `AsPages()` takes no arguments; `pageSizeHint` is removed. Open for the lead.** Rationale: no implementation
   uses it, no requirement asks for it, and an ignored parameter on a public API misleads. Options: keep it as Azure does
   (rejected: Azure's implementations honour it; ours would not); plumb it into a query parameter (rejected: which
   parameter is per-API, a strategy concern).
4. **P7c-4 — One fetch-and-parse step with an `async` flag; two thin iterator shells.** Rationale: 4c's proven pattern
   (`SendCoreAsync(…, async)` read through `SyncPath`); no sync-over-async; no duplicated close logic. Options: a blocking
   wrapper over the async pager (rejected: `RS0030`, starvation); two hand-written engines (rejected: drift).
5. **P7c-5 — The factories take the transport seams (`IAsyncHttpClient`, `IHttpClient`), not `HttpPipeline`.**
   Rationale: 4c's hand-off; an `HttpPipeline` converts to both, and a bare transport or `DelegateHttpClient` works in tests
   and in a caller's own stack. Distinct factory names because one `HttpPipeline` argument would make overloads ambiguous.
6. **P7c-6 — Per-call overrides are `RequestOptions`, default `RequestOptions.Empty`, the same instance on every page.
   Open for the lead** (it removes a parameter type). Rationale: `PAGE-36` names "timeout, retry budget, tags", which is
   `RequestOptions`; the old `DexpaceClientOptions` argument replaced the *client's* options rather than adding per-call
   overrides. Options: accept both (rejected: two option types for one concept, which 5a retired elsewhere).
7. **P7c-7 — `Page<T>.Request` is the request the walk sent for that page, before the pipeline.** Rationale: `PAGE-2`'s
   "originating request"; it carries no credential the auth policy stamped, so a retained page holds no secret (fact 10).
   Options: `response.Request` (rejected: post-pipeline, may hold `Authorization`).
8. **P7c-8 — Strategies build the next request from the template and read only the URL of `response.Request`.**
   Rationale: `PAGE-16`/`PAGE-23` name the template; `PAGE-17`/`PAGE-19` name the executed request and response URL for
   *reading*; building from `response.Request` would carry stamped credentials forward.
9. **P7c-9 — An internal, raw-string `QuerySplice`, matching on the decoded name, ordinally.** Rationale: `Query`
   re-renders the whole query (`PAGE-21` forbids it); ordinal matching fixes the as-built `?Page=1` bug; decoded matching
   treats `my%20key` and `my key` as one name. Kept internal for v1 to keep the surface narrow. Options: Node's encoded-name
   match (rejected for the reason above); a public helper (deferred, hand-off to 12).
10. **P7c-10 — A lone surrogate in a spliced name or value throws `ArgumentException` naming the parameter only.**
    Rationale: `Rfc3986` would otherwise write U+FFFD, silently corrupting a server's cursor (fact 11); same type
    `Query.Builder` throws; the value is never echoed because a cursor can carry a session token. Through a strategy it is
    a parse failure (`PAGE-13`), not a quiet end.
11. **P7c-11 — `PageNumber` parses ASCII digits only and ends at `int.MaxValue`.** Rationale: `PAGE-17`'s "non-numeric →
    start page" with an invariant, sign-less reading (fact 6); a page number that cannot advance cannot produce a next
    request. `startPage` must be `>= 0`.
12. **P7c-12 — The `Link` strategy ends the stream on a cross-origin target unless `allowCrossOrigin: true`, strips
    userinfo, and keeps the http(s) guard. Open for the lead.** Rationale: each page is a fresh pipeline call, and 6c stamps
    credentials when a request is same-origin with *its own* seed (fact 9), so a hostile or compromised server could
    otherwise harvest the bearer token with one header. The origin compared is the **template's**, matching §10 entry 15's
    seed-origin rule: a first page that redirected to another origin does not license following links there. A
    scheme downgrade is a different origin and ends too. Stronger than `PAGE-19`'s letter; recorded as a §11 reading. The
    rejection of SP/controls/`<>"` follows design §7.1 and diverges from Node, which follows `<not a url>` as a relative
    path. Options: no guard, as Node and Ruby (rejected: credential leak); throw instead of ending (rejected: `PAGE-4`'s
    only-null-ends rule and `PAGE-19`'s "end, not error"); compare against the response URL (rejected: launders a redirect).
13. **P7c-13 — A template with a non-replayable body fails at construction.** Rationale: every page re-sends it;
    fail-fast with a message naming `ToReplayableAsync` beats `StreamConsumedException` on page 2.
14. **P7c-14 — A response delivered after the token was cancelled is disposed and the walk throws
    `OperationCanceledException`.** Rationale: `PAGE-33`'s "if it completes successfully the paginator MUST close and
    discard that response"; parsing it would yield a page the caller asked not to receive.
15. **P7c-15 — Fetchers dispose their own responses; `FetchedPage<T>` carries a materialized page.** Rationale: §10 entry
    17 makes pages response-free, so `PAGE-34`'s "fetcher MUST NOT close; ownership transfers to the page" cannot transfer
    anything; the guarantee it protects (no leak) is kept by the fetcher's `await using`. Recorded as an amendment to entry
    17. Options: let a fetcher return a live `Response` plus a parser (rejected: reintroduces a response across the engine's
    boundary for no capability a caller lacks).
16. **P7c-16 — `PagingOptions` is a mutable class, fresh per walk, shared by every fetcher call of that walk; async
    fetchers only. Open for the lead** (a mutable public type in a namespace of immutable models). Rationale: `PAGE-35`;
    a fresh instance per walk is what keeps `PAGE-8`'s independent re-iteration true, which a closure captured once at
    construction would break. A blocking fetcher front-end is not built (Q2).
17. **P7c-17 — A `null` envelope throws `DeserializationException` naming `TPage`; a `null` `PageInfo` or a `null` response
    throws `InvalidOperationException` naming the strategy or client type.** Rationale: the first is a serde outcome
    (design §7.1's note, `SERDE-13`); the second and third are contract violations, worded as `SEAM-16`'s existing guards
    are. Each goes through the `PAGE-13` close path.
18. **P7c-18 — No new package.** Property tests use a seeded generator. Rationale: core's dependency rule is untouched and
    test projects avoid a lock-file change that would race 7b's possible FsCheck addition. Options: FsCheck (deferred: if
    7b pins it, 7c's plan may port the properties).
19. **P7c-19 — `Page<T>`, `PageInfo<T>` stay sealed classes; `FetchedPage<T>` is a sealed record.** Rationale: the first
    two wrap a list, where record equality would be reference equality and mislead; `FetchedPage<T>` is a small carrier
    whose `with` is useful to fetcher authors.
20. **P7c-20 — One pull request, `feat!`, closing no issue,** grouped as (1) splice, `Link` parser, strategies; (2) engine,
    bases, factories, blocking pager; (3) fetchers, AOT smoke, docs and corrections. Rationale: phases 6a–6c each landed
    as one PR; the groups are the plan's task order.

---

## Deviation Ledger

| ID | Decision | Touches | Kind | State | Route |
|---|---|---|---|---|---|
| P7c-2 | Typed-envelope strategy interface | `PAGE-4`, `PAGE-5`, `PAGE-16` | mechanism (letter kept) | taken | §7.1 dated correction |
| P7c-3 | `pageSizeHint` removed | none (surface) | surface judgement | **open** | §7.1 dated correction |
| P7c-6 | `RequestOptions` replaces `DexpaceClientOptions` | `PAGE-36` | surface judgement | **open** | §7.1 dated correction |
| P7c-12 | Cross-origin `Link` targets end the stream | `PAGE-19` | stronger than the letter | **open** | new §11 item; `first-release.md` asymmetry |
| P7c-15 | Fetchers own and dispose their responses | `PAGE-34`, `PAGE-3` | existing §10 entry 17, extended | taken | §10 entry 17 dated amendment |
| P7c-16 | Mutable `PagingOptions`; async fetchers only | `PAGE-35`, `PAGE-8` | surface judgement | **open** | §7.1 dated correction |
| — | Materialized pages | `PAGE-3`, `PAGE-12`, `PAGE-15` | existing §10 entry 17 | taken | checklist cites entry 17 |
| — | Canonical-form splice | `PAGE-21` | existing §10 entry 18 (its `PAGE-24` clause retired) | taken | §10 entry 18 dated amendment |
| — | No executor mode | `PAGE-29`, `PAGE-30` | existing §10 entry 19 | taken | checklist cites entry 19 |

No ruling leaves a MUST's letter unmet beyond what §10 entries 17, 18 and 19 already record, so no new §10 entry is opened.

---

## Exit criteria

From the Phase 7 card and the universal criteria, for 7c's share:

1. **36 checklist rows** (`PAGE-1`–`PAGE-36`) in constraint 3's legend, none blank: 33 ✅, 3 🚫 (`PAGE-3` entry 17,
   `PAGE-29` and `PAGE-30` entry 19). 7c's 36 rows are part of the card's 107.
2. **The AOT smoke performs a paged round trip on Linux CI** (`CheckPhase7cPaginationAsync`: async cursor walk, `Link`
   walk, single-use page view, blocking walk, through the STJ serde with a source-generated `JsonTypeInfo`), contributing to
   the card's "JSON, Tristate, paged and SSE round trip".
3. CI green on every matrix row: build (warnings as errors, `RS0016`/`RS0017` clean for the surface above, `MA0051`),
   `dotnet format --verify-no-changes`, all tests including `--filter-trait "Category=Security"`, the 80% coverage gate,
   the dependency audit (no new package), reproducible pack, the AOT smoke.
4. `docs/sdk-documentation/pagination.md` written, opening "As built by phase 7c … written against source on <date>"; it
   documents the two views, the cap advice (`PAGE-10`), `PAGE-33`'s race, the `Link` origin guard, the fetcher ownership
   rule and the blocking pager's 64 MiB note. Ported in structure from `ruby-sdk/docs/sdk-documentation/pagination.md` and
   Node's `write-a-paging-strategy.md`.
5. A `CHANGELOG.md` `[Unreleased]` entry listing the breaking changes above.
6. A dated roadmap status note, and the corrections in [Corrections owed](#design-roadmap-and-claudemd-corrections-owed-at-close-out).
7. `dotnet run --project .claude/skills/housekeeping/src -- probe` clean for 7c's files after filing this design, the plan
   and the checklist under `docs/work/mvp/phase7/phase7c/`.
8. No issue to close (7c has none); the PR description names the checklist.
