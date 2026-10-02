# pagination

## Rules
- PAGE-6 (MUST) - Iteration MUST be page-lazy, with exactly one HTTP exchange per page actually yielded.
  <sub>spec · `docs/product-spec/12-pagination.md:10-10` · high · sha:ba759edd34ec</sub>
- PAGE-6 (MUST) - For the blocking engine, constructing the paginator, obtaining the iterable/stream, and obtaining the item iterator MUST trigger zero exchanges; the first exchange happens only when the consumer first probes for data.
  <sub>spec · `docs/product-spec/12-pagination.md:10-10` · high · sha:ba759edd34ec</sub>
- PAGE-7 (MUST) - Fetching MUST advance only forward and only on demand; after a page whose strategy returned a null/absent next-request, the engine MUST perform no further exchange, and repeated end-of-stream probes MUST be idempotent (no re-fetch).
  <sub>spec · `docs/product-spec/12-pagination.md:11-11` · high · sha:ba759edd34ec</sub>
- PAGE-8 (MUST) - Each independent iteration MUST restart from the initial request with its own fresh state, and the engine itself MUST hold only immutable configuration and be safe to share.
  <sub>spec · `docs/product-spec/12-pagination.md:12-12` · high · sha:ba759edd34ec</sub>
- PAGE-8 (MUST) - A single returned iterator/stream/walk MAY be single-consumer, but two separate iterations from the same engine MUST each drive a full fetch sequence and yield identical results.
  <sub>spec · `docs/product-spec/12-pagination.md:12-12` · high · sha:ba759edd34ec</sub>
- PAGE-36 (MUST) - Per-call request overrides (timeout, retry budget, tags) supplied to the strategy-based engine MUST be applied to every page exchange, not just the first; the default is no overrides.
  <sub>spec · `docs/product-spec/12-pagination.md:13-13` · high · sha:ba759edd34ec</sub>
- PAGE-2 (MUST) - A page's materialized item list and its derived status/headers/originating-request MUST remain readable after the page is closed; only the raw response body/connection becomes invalid at close.
  <sub>spec · `docs/product-spec/12-pagination.md:19-19` · high · sha:ba759edd34ec</sub>
- PAGE-2 (MUST) - A page's items MUST never be null, though they MAY be empty.
  <sub>spec · `docs/product-spec/12-pagination.md:19-19` · high · sha:ba759edd34ec</sub>
- PAGE-3 (MUST) - A page MUST be a closeable resource owning exactly one underlying response, whoever pulls a page owns closing it, and closing the page MUST release that response's body/connection.
  <sub>spec · `docs/product-spec/12-pagination.md:20-20` · high · sha:ba759edd34ec</sub>
- PAGE-3 (MUST) - A component that hands a caller a live page (e.g. a first-page fetcher) MUST NOT itself close the response, because ownership transfers to the page.
  <sub>spec · `docs/product-spec/12-pagination.md:20-20` · high · sha:ba759edd34ec</sub>
- PAGE-4 (MUST) - A strategy's parse output MUST carry items plus a next-request value, where a null/absent next-request is the single, exclusive end-of-stream signal the engine recognizes.
  <sub>spec · `docs/product-spec/12-pagination.md:26-26` · high · sha:ba759edd34ec</sub>
- PAGE-4 (MUST) - Strategy parse MUST always return a well-formed result (never null) and MUST signal termination only by a null next-request, never by throwing and never via a side channel.
  <sub>spec · `docs/product-spec/12-pagination.md:26-26` · high · sha:ba759edd34ec</sub>
- PAGE-5 (MUST) - A strategy MUST read everything it needs from the response synchronously inside parse because the body is single-use, MUST NOT retain the response or its body beyond the call, and MUST NOT close or mutate the response, since lifecycle ownership belongs to the engine.
  <sub>spec · `docs/product-spec/12-pagination.md:27-27` · high · sha:ba759edd34ec</sub>
- PAGE-5 (MUST) - Strategies MUST be immutable and safe to share concurrently.
  <sub>spec · `docs/product-spec/12-pagination.md:27-27` · high · sha:ba759edd34ec</sub>
- A pagination port MUST preserve the two-view model, the page-lazy fetch discipline, deterministic response-lifecycle management, and the strategy contract.
  <sub>spec · `docs/product-spec/12-pagination.md:3-3` · high · sha:ba759edd34ec</sub>
- PAGE-9 (MUST) - The engine MUST accept a page cap (maximum exchanges) bounding a server that never advances its cursor.
  <sub>spec · `docs/product-spec/12-pagination.md:31-31` · high · sha:ba759edd34ec</sub>
- PAGE-9 (MUST) - The page cap counts exchanges/pages, not items, and once reached the engine MUST stop fetching even if the strategy still reports a next-request.
  <sub>spec · `docs/product-spec/12-pagination.md:31-31` · high · sha:ba759edd34ec</sub>
- PAGE-9 (MUST) - The page cap MUST be validated as strictly positive at construction (fail fast), not lazily.
  <sub>spec · `docs/product-spec/12-pagination.md:31-31` · high · sha:ba759edd34ec</sub>
- PAGE-10 (SHOULD) - The default page cap SHOULD be effectively unbounded (matching plain lazy-sequence semantics), and documentation SHOULD direct production callers to set a finite cap.
  <sub>spec · `docs/product-spec/12-pagination.md:32-32` · high · sha:ba759edd34ec</sub>
- PAGE-11 (MUST) - The item-level view MUST eager-close each page before yielding any of that page's items (after copying the materialized items), so abandoning item iteration mid-page never strands the response.
  <sub>spec · `docs/product-spec/12-pagination.md:38-38` · high · sha:ba759edd34ec</sub>
- PAGE-12 (MUST) - The page-level view MUST be auto-closing with a close-on-abandon guarantee, closing the previous page as the consumer advances and the last page at exhaustion.
  <sub>spec · `docs/product-spec/12-pagination.md:39-39` · high · sha:ba759edd34ec</sub>
- PAGE-12 (MUST) - Because probing for the next page eagerly runs that page's exchange, the page-level view MUST buffer the fetched-but-undelivered page in storage it owns so an emptiness probe or early break followed by an explicit close still releases it.
  <sub>spec · `docs/product-spec/12-pagination.md:39-39` · high · sha:ba759edd34ec</sub>
- PAGE-12 (MUST) - Explicit close of the page-level view MUST release both the currently-held page and any buffered page, and consumers MUST be told to wrap the view in a scoped/auto-close construct, since up to two live pages can exist at once.
  <sub>spec · `docs/product-spec/12-pagination.md:39-39` · high · sha:ba759edd34ec</sub>
- PAGE-14 (MUST) - The page-level view MUST be single-use, its iterator/stream may be obtained at most once, and re-iteration MUST fail rather than silently restart.
  <sub>spec · `docs/product-spec/12-pagination.md:40-40` · high · sha:ba759edd34ec</sub>
- PAGE-15 (MUST) - A close error while releasing held page(s) MUST be surfaced, not swallowed.
  <sub>spec · `docs/product-spec/12-pagination.md:41-41` · high · sha:ba759edd34ec</sub>
- PAGE-15 (MUST) - When exposed through a stream whose terminal cannot declare the underlying I/O error type, a close error MUST be re-thrown wrapped so the caller can still catch it at the close site.
  <sub>spec · `docs/product-spec/12-pagination.md:41-41` · high · sha:ba759edd34ec</sub>
- PAGE-15 (MUST) - When both held pages fail to close, the first failure MUST propagate with the second attached as suppressed.
  <sub>spec · `docs/product-spec/12-pagination.md:41-41` · high · sha:ba759edd34ec</sub>
- PAGE-13 (MUST) - If a strategy's parse throws, the engine MUST close that response inline on the exceptional path (the page is never constructed, so nothing else would close it) and then propagate the failure.
  <sub>spec · `docs/product-spec/12-pagination.md:45-45` · high · sha:ba759edd34ec</sub>
- PAGE-13 (MUST) - A close failure during parse-failure handling MUST NOT mask the parse failure; it MUST be attached as a suppressed/secondary error with the parse error primary.
  <sub>spec · `docs/product-spec/12-pagination.md:45-45` · high · sha:ba759edd34ec</sub>
- PAGE-16 (MUST) - The cursor strategy MUST read items and the next cursor from a single read of the response body.
  <sub>spec · `docs/product-spec/12-pagination.md:49-49` · high · sha:ba759edd34ec</sub>
- PAGE-16 (MUST) - The cursor strategy MUST treat a null OR empty next cursor as end-of-stream.
  <sub>spec · `docs/product-spec/12-pagination.md:49-49` · high · sha:ba759edd34ec</sub>
- PAGE-16 (MUST) - The cursor strategy MUST derive the next request by setting a configurable cursor query parameter (default `cursor`) on the template.
  <sub>spec · `docs/product-spec/12-pagination.md:49-49` · high · sha:ba759edd34ec</sub>
- PAGE-17 (MUST) - The page-number strategy MUST treat an empty items list as end-of-stream (defensive against servers that return an empty page past the end).
  <sub>spec · `docs/product-spec/12-pagination.md:50-50` · high · sha:ba759edd34ec</sub>
- PAGE-17 (MUST) - The page-number strategy MUST infer the current page from the originating (executed) request's page query parameter, defaulting to a configurable start page (default 1) when that parameter is absent, empty, or non-numeric, and set the next request's page to current+1.
  <sub>spec · `docs/product-spec/12-pagination.md:50-50` · high · sha:ba759edd34ec</sub>
- PAGE-17 (MUST) - The page-number strategy's page parameter name (default `page`) and start page (default 1, allowing 0-based servers) MUST be configurable.
  <sub>spec · `docs/product-spec/12-pagination.md:50-50` · high · sha:ba759edd34ec</sub>
- PAGE-18 (MUST) - The Link-header strategy MUST select the next URL from the Link header(s) using RFC 5988/8288 semantics, taking the first link-value whose `rel` contains the token `next` case-insensitively (rel may be quoted or unquoted and list multiple space/tab-separated types).
  <sub>spec · `docs/product-spec/12-pagination.md:51-51` · high · sha:ba759edd34ec</sub>
- PAGE-18 (MUST) - The Link-header strategy MUST parse link-values so commas inside angle-bracketed URLs or quoted parameter values do not split link-values, and MUST support quoted-pair escapes.
  <sub>spec · `docs/product-spec/12-pagination.md:51-51` · high · sha:ba759edd34ec</sub>
- PAGE-18 (MUST) - Absence of a Link header or of a rel=next segment MUST mean end-of-stream, and the Link header name MUST be configurable (default `Link`).
  <sub>spec · `docs/product-spec/12-pagination.md:51-51` · high · sha:ba759edd34ec</sub>
- PAGE-19 (MUST) - A rel=next target MUST resolve as an RFC 3986 reference against the originating page's response URL, with absolute targets used as-is and relative targets resolved against the base.
  <sub>spec · `docs/product-spec/12-pagination.md:52-52` · high · sha:ba759edd34ec</sub>
- PAGE-19 (MUST) - A query-only reference (starting with `?`) MUST preserve the base URL's full path and replace only the query, and MUST NOT drop the last path segment (the RFC 2396 behavior).
  <sub>spec · `docs/product-spec/12-pagination.md:52-52` · high · sha:ba759edd34ec</sub>
- PAGE-19 (MUST) - A rel=next target that cannot resolve into a valid URL MUST be treated as end-of-stream, not an error.
  <sub>spec · `docs/product-spec/12-pagination.md:52-52` · high · sha:ba759edd34ec</sub>
- PAGE-20 (SHOULD) - When a server splits pagination links across multiple separate Link header instances, the strategy SHOULD normalize them (e.g. by concatenation), and an empty header set SHOULD map to no next link.
  <sub>spec · `docs/product-spec/12-pagination.md:53-53` · high · sha:ba759edd34ec</sub>
- PAGE-21 (MUST) - When rewriting the next-page query string, the rebuilder MUST splice the raw query verbatim: every untargeted parameter is copied byte-for-byte (value-less flags stay value-less, reserved characters not rewritten, order preserved) and only the targeted parameter's name/value is decoded/encoded.
  <sub>spec · `docs/product-spec/12-pagination.md:59-59` · high · sha:ba759edd34ec</sub>
- PAGE-21 (MUST) - The query rebuilder MUST NOT re-render or canonicalize the whole query.
  <sub>spec · `docs/product-spec/12-pagination.md:59-59` · high · sha:ba759edd34ec</sub>
- PAGE-22 (MUST) - Encoding a newly-set query parameter MUST use RFC 3986 component encoding (space becomes `%20`, literal `+` preserved as data and not treated as a space).
  <sub>spec · `docs/product-spec/12-pagination.md:60-60` · high · sha:ba759edd34ec</sub>
- PAGE-22 (MUST) - Reading a query parameter MUST decode with RFC 3986 semantics: a literal `+` reads back as `+`, `%20` as a space, a value-less flag as empty string, and the first match wins.
  <sub>spec · `docs/product-spec/12-pagination.md:60-60` · high · sha:ba759edd34ec</sub>
- PAGE-23 (MUST) - Setting a query parameter MUST replace the first existing occurrence in place (dropping further duplicates, a single-value convention for paging params), append if absent, and remove entirely when the new value is null/absent, with order otherwise preserved.
  <sub>spec · `docs/product-spec/12-pagination.md:61-61` · high · sha:ba759edd34ec</sub>
- PAGE-23 (MUST) - Following an absolute/whole next URL MUST swap only the request's URL, preserving the template's method, headers, and body.
  <sub>spec · `docs/product-spec/12-pagination.md:61-61` · high · sha:ba759edd34ec</sub>
- PAGE-24 (MUST) - URL rewriting MUST preserve all non-query components exactly (scheme/protocol, userinfo, host, port, path, and fragment); only the query may change.
  <sub>spec · `docs/product-spec/12-pagination.md:62-62` · high · sha:ba759edd34ec</sub>
- PAGE-25 (MUST) - The async engine MUST drive fetch, parse, delivery, and re-arm inside the async completion graph without any thread blocking on a page.
  <sub>spec · `docs/product-spec/12-pagination.md:68-68` · high · sha:ba759edd34ec</sub>
- PAGE-25 (MUST) - Cancelling/completing the async walk's result future MUST halt the walk (no further pages) and best-effort abort the in-flight exchange by cancelling its transport future.
  <sub>spec · `docs/product-spec/12-pagination.md:68-68` · high · sha:ba759edd34ec</sub>
- PAGE-26 (MUST) - Async cancellation MUST take effect at page granularity; if the result settles mid-drain, items already being delivered from that page still reach the consumer and the driver stops at the next page boundary rather than interrupting an in-progress drain.
  <sub>spec · `docs/product-spec/12-pagination.md:69-69` · high · sha:ba759edd34ec</sub>
- PAGE-26 (MUST) - A page fetched but not yet drained when the async result settles MUST be dropped undrained AND closed, and any close error on that already-settled path MUST be swallowed.
  <sub>spec · `docs/product-spec/12-pagination.md:69-69` · high · sha:ba759edd34ec</sub>
- PAGE-27 (MUST) - The async engine MUST close each page's response exactly once on whichever path consumes it (after drain, when a fetched-but-undrained page is dropped via external settlement or rejected re-dispatch, or inline on parse failure), with no double-close and no leak.
  <sub>spec · `docs/product-spec/12-pagination.md:70-70` · high · sha:ba759edd34ec</sub>
- PAGE-28 (MUST) - A consumer that throws, a transport/connection failure, a parse failure, or a null success completion MUST terminate the async walk and complete the result future exceptionally, surfacing the original underlying cause (unwrapping any future-composition wrapper).
  <sub>spec · `docs/product-spec/12-pagination.md:71-71` · high · sha:ba759edd34ec</sub>
- PAGE-28 (MUST) - A transport that eagerly throws instead of returning a failed future MUST be handled as a failed walk.
  <sub>spec · `docs/product-spec/12-pagination.md:71-71` · high · sha:ba759edd34ec</sub>
- PAGE-29 (MUST) - For a single async walk the consumer MUST NOT be invoked concurrently, items MUST be delivered one at a time in server order, and the consumer MUST NOT assume a particular thread.
  <sub>spec · `docs/product-spec/12-pagination.md:72-72` · high · sha:ba759edd34ec</sub>
- PAGE-29 (MUST) - The async engine MUST also offer a mode running the driver, and therefore every consumer invocation, on a caller-supplied executor so a blocking consumer does not tie up transport callback threads.
  <sub>spec · `docs/product-spec/12-pagination.md:72-72` · high · sha:ba759edd34ec</sub>
- PAGE-30 (MUST) - If the async engine is given an executor that rejects a (re-)dispatch, the walk MUST terminate with the result future completed exceptionally carrying the rejection, and any staged page MUST be closed.
  <sub>spec · `docs/product-spec/12-pagination.md:73-73` · high · sha:ba759edd34ec</sub>
- PAGE-30 (MUST) - An executor rejection MUST NOT hang the result future or leak the rejection.
  <sub>spec · `docs/product-spec/12-pagination.md:73-73` · high · sha:ba759edd34ec</sub>
- PAGE-31 (SHOULD) - The async driver SHOULD process synchronously-completed page futures iteratively (a trampoline), not via recursive future composition, so an arbitrarily long run of already-complete pages does not overflow the stack.
  <sub>spec · `docs/product-spec/12-pagination.md:74-74` · high · sha:ba759edd34ec</sub>
- PAGE-31 - A port on a runtime without deep-recursion risk MAY satisfy the trampoline intent with its native loop model but MUST NOT recurse per page.
  <sub>spec · `docs/product-spec/12-pagination.md:74-74` · high · sha:ba759edd34ec</sub>
- PAGE-32 (MUST) - In the async drain path, releasing a page's response MUST happen whether the consumer succeeds or throws, and a throwing close MUST NOT escape the driver.
  <sub>spec · `docs/product-spec/12-pagination.md:75-75` · high · sha:ba759edd34ec</sub>
- PAGE-32 (MUST) - On the async success path a throwing close MUST be reported through the result future (so the walk terminates instead of hanging); if the consumer already failed, that cause stays primary and the close error is swallowed.
  <sub>spec · `docs/product-spec/12-pagination.md:75-75` · high · sha:ba759edd34ec</sub>
- PAGE-33 (MUST) - A port MUST document the inherent async cancellation race, namely that if an external cancel settles the transport future before the transport delivers its response, that response never reaches the paginator's close path and releasing it is the transport's responsibility.
  <sub>spec · `docs/product-spec/12-pagination.md:76-76` · high · sha:ba759edd34ec</sub>
- PAGE-33 (MUST) - A page request already dispatched MAY still complete after the abort, and if it completes successfully the paginator MUST close and discard that response.
  <sub>spec · `docs/product-spec/12-pagination.md:76-76` · high · sha:ba759edd34ec</sub>
- PAGE-34 (MUST) - The fetcher-based front-end MUST call the first-page fetcher exactly once, then drive subsequent pages by keying the next-page fetcher off the previous page's next link, falling back to its continuation token when no next link is present (next link wins).
  <sub>spec · `docs/product-spec/12-pagination.md:82-82` · high · sha:ba759edd34ec</sub>
- PAGE-34 (MUST) - In the fetcher-based front-end, an empty/blank next link with no fallback token, or a null page from either fetcher, MUST end the stream, and a null first page yields an empty stream.
  <sub>spec · `docs/product-spec/12-pagination.md:82-82` · high · sha:ba759edd34ec</sub>
- PAGE-34 (MUST) - Each fetcher MUST build a page that owns its response and MUST NOT close it, while a fetcher that throws before building the page remains responsible for that response.
  <sub>spec · `docs/product-spec/12-pagination.md:82-82` · high · sha:ba759edd34ec</sub>
- PAGE-35 (SHOULD) - If a mutable paging-options object is offered to fetchers, the same instance SHOULD be threaded through every fetcher call so a custom retriever can stash cursor/state between pages, and this cross-call mutation visibility SHOULD be documented.
  <sub>spec · `docs/product-spec/12-pagination.md:83-83` · high · sha:ba759edd34ec</sub>
- PAGE-1 (MUST) - Both the item view and the page view MUST be available over the same walk, and items MUST be delivered in server-defined order across page boundaries.
  <sub>spec · `docs/product-spec/12-pagination.md:9-9` · high · sha:ba759edd34ec</sub>
- PAGE-11: the item view eagerly closes each page before yielding its items on partial consumption.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:10-10` · high · sha:0451cc7f3bb4</sub>
- PAGE-12: the page view releases the held page and buffered pages on early break or probe-then-close.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:10-10` · high · sha:0451cc7f3bb4</sub>
- PAGE-14: a second page-view iterator throws.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:10-10` · high · sha:0451cc7f3bb4</sub>
- PAGE-15: an error closing a held page surfaces wrapped, with a second failure suppressed.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:10-10` · high · sha:0451cc7f3bb4</sub>
- PAGE-13: when parsing throws, the response is closed inline with the parse error primary and any close error suppressed.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:11-11` · high · sha:0451cc7f3bb4</sub>
- PAGE-16: a null or empty cursor ends pagination and the body is read a single time.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:12-12` · high · sha:0451cc7f3bb4</sub>
- PAGE-17: with page-number pagination, empty items end the walk and garbage falls back to the start page.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:12-12` · high · sha:0451cc7f3bb4</sub>
- PAGE-18 and PAGE-20: Link-header rel=next parsing handles quoted commas, multi-token rel values and multi-header splitting.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:12-12` · high · sha:0451cc7f3bb4</sub>
- PAGE-19: a query-only next reference preserves the base path and an unparseable target ends pagination.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:12-12` · high · sha:0451cc7f3bb4</sub>
- PAGE-21: verbatim query splicing preserves untouched parameters byte-for-byte.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:13-13` · high · sha:0451cc7f3bb4</sub>
- PAGE-22: query components use RFC 3986 encode and decode with `+` treated as data.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:13-13` · high · sha:0451cc7f3bb4</sub>
- PAGE-23: setting a query parameter replaces the first occurrence, appends if absent or removes it, and following a whole URL preserves method, headers and body.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:13-13` · high · sha:0451cc7f3bb4</sub>
- PAGE-24: non-query URL components are preserved when the query is modified.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:13-13` · high · sha:0451cc7f3bb4</sub>
- PAGE-25: async pagination blocks no thread per page and cancellation aborts the in-flight transport future.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-26: page-granular cancellation drops and closes a staged page.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-27: every response is closed exactly once across all paths.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-28: failures surface the original cause and an eager transport throw is handled.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-29: async delivery is serial and ordered, with an executor mode.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-30: an executor rejection fails the walk and closes the staged page.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-31: a trampoline handles thousands of synchronously completing pages.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-32: a throwing close on success is reported through the future.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-33: cancellation-race ownership is documented.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14-14` · high · sha:0451cc7f3bb4</sub>
- PAGE-34: the fetcher front-end calls the first fetcher once, prefers nextLink over token, and terminates on a blank or null link.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:15-15` · high · sha:0451cc7f3bb4</sub>
- PAGE-35: the same options instance is threaded through the fetcher and mutation of it is observed.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:15-15` · high · sha:0451cc7f3bb4</sub>
- PAGE-36: per-call options reach every page exchange.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:16-16` · high · sha:0451cc7f3bb4</sub>
- PAGE-1: an item view and a page view over one 3-page walk yield the concatenated server-order items and three page objects respectively.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:7-7` · high · sha:0451cc7f3bb4</sub>
- PAGE-2: page metadata survives closing the page.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:7-7` · high · sha:0451cc7f3bb4</sub>
- PAGE-3: a page closes its response exactly once and a fetcher does not close it.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:7-7` · high · sha:0451cc7f3bb4</sub>
- PAGE-6: blocking iteration triggers zero exchanges until the first probe and then one exchange per page consumed, while async iteration begins fetching on invocation.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:8-8` · high · sha:0451cc7f3bb4</sub>
- PAGE-7: no fetch happens past the terminal page and end-probes are idempotent.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:8-8` · high · sha:0451cc7f3bb4</sub>
- PAGE-8: two iterations over the same pageable each drive a full sequence.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:8-8` · high · sha:0451cc7f3bb4</sub>
- PAGE-9: a page cap less than or equal to zero throws at construction, and a non-advancing server stops the walk at exactly N exchanges.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:9-9` · high · sha:0451cc7f3bb4</sub>
- PAGE-10: the default page cap is effectively unbounded.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:9-9` · high · sha:0451cc7f3bb4</sub>
- The Link resolver must reject a target containing characters RFC 3986 forbids unescaped (space, <, >, ") before resolution.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:108-109` · high · sha:68af5c6bf0ea</sub>
- Query keys are case-sensitive, so the PAGE-21 query splice must match keys ordinally rather than with StringComparison.OrdinalIgnoreCase, which as built rewrote ?Page=1 to page=2 when setting page.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:114-118` · high · sha:68af5c6bf0ea</sub>
- On the net8.0 target the async LINQ operators come from a NuGet package that core does not take (NFR-1), so callers on .NET 8 bring it themselves and nothing in core's surface depends on it.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:25-27` · high · sha:68af5c6bf0ea</sub>
- AsPages() returns a thin wrapper sequence whose GetAsyncEnumerator latches an Interlocked flag and throws InvalidOperationException on the second call, while each call to AsPages() is still a fresh walk because the single-use rule binds the returned object.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:36-39` · high · sha:68af5c6bf0ea</sub>
- PAGE-11 requires the item view to eager-close each page before yielding its items, PAGE-12 requires the page view to close the previous page on advance and release a fetched-but-undelivered page, and SSE-25 requires the same of a partially consumed stream.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:41-43` · high · sha:68af5c6bf0ea</sub>
- No live response may ever span a yield return, so the engine sends the request, deserializes the page, captures status and headers, asks the next-request delegate for the continuation while the response is open, and disposes the response in a finally, all before the yield.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:50-54` · high · sha:68af5c6bf0ea</sub>
- PAGE-13 requires that a close failure MUST NOT mask the parse failure and MUST be attached as a suppressed/secondary error with the parse error primary.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:76-78` · high · sha:68af5c6bf0ea</sub>
- Strategy delegates are immutable and shareable (PAGE-5), and built-in strategies capture only configuration.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:89-90` · high · sha:68af5c6bf0ea</sub>
- ArgumentOutOfRangeException.ThrowIfNegativeOrZero in Pageable.Create closes the maxPages validation gap.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:93-93` · high · sha:68af5c6bf0ea</sub>

## Constraints
- Uri.TryCreate(base, "not a url", out _) succeeds, producing https://h/repo/not%20a%20url, so PAGE-19's conformance fixture (`<not a url>; rel=next` meaning end of stream) is followed as a relative path under the lenient parser.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:106-109` · high · sha:68af5c6bf0ea</sub>
- System.Uri canonicalizes the query before any splice sees it (for ?x=%7E%41&page=1, Uri.Query returns ?x=~A&page=1), and a UriBuilder round trip writes an explicit default port into OriginalString (https://h:443/p?...), so byte-for-byte preservation of untargeted query parameters is unattainable while Request.Url is a System.Uri.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:119-123` · high · sha:68af5c6bf0ea</sub>
- System.Linq.AsyncEnumerable operators (FirstAsync, Take, Where over IAsyncEnumerable<T>) are in the shared framework from .NET 10 only, being present in Microsoft.NETCore.App 10.0.12 and absent from the 8.0.31 reference pack.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:22-25` · high · sha:68af5c6bf0ea</sub>
- Calling GetAsyncEnumerator twice on one compiler-generated async-iterator object starts a second independent walk rather than failing (two await foreach passes over a five-item generator yielded ten items), which suits PAGE-8's fresh restart per iteration on the item view but violates PAGE-14's single-use requirement on the page view.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:32-36` · high · sha:68af5c6bf0ea</sub>
- IAsyncDisposable.DisposeAsync on the enumerator is .NET's early-termination hook that runs the iterator's pending finally blocks, but someone must call it (P7).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:43-46` · high · sha:68af5c6bf0ea</sub>
- A consumer that calls GetAsyncEnumerator() and MoveNextAsync() by hand and drops the enumerator without await using leaves the iterator's finally unexecuted even after two forced full GCs, because compiler-generated enumerators have no finalizer and garbage collection is not a cleanup hook.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:48-50` · high · sha:68af5c6bf0ea</sub>
- A finally block that throws replaces the exception already in flight in C# (a thrown IOException in finally surfaces alone with no inner exception over an InvalidOperationException), so a bare finally cannot satisfy PAGE-13.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:73-76` · high · sha:68af5c6bf0ea</sub>

## Conclusions
- The existing scheme guard, under which only http/https Link targets survive, is a good addition beyond the specification and stays.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:109-110` · high · sha:68af5c6bf0ea</sub>
- The accepted residual of the System.Uri limitation is RFC 3986 section 6.2.2.2 percent-encoding normalization of unreserved characters, which is equivalent and unobservable to a conforming server, while reserved characters, value-less flags (?flag), order and + are preserved (recorded as section 10 entry 18).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:123-128` · high · sha:68af5c6bf0ea</sub>
- The async engine (PAGE-25 to PAGE-33) is not a second engine, because .NET has one iteration protocol that is already non-blocking (P4).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:130-132` · high · sha:68af5c6bf0ea</sub>
- PAGE-31 is satisfied by construction because the walk is a while loop in a state machine and a synchronously completing ValueTask continues inline without growing the stack, using the specification's latitude that a port MAY use its native loop model but MUST NOT recurse per page.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:137-140` · high · sha:68af5c6bf0ea</sub>
- PAGE-33's race, a response delivered to a send that was already cancelled, is documented as the transport's responsibility (TRANSPORT-9, section 3.3).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:141-142` · high · sha:68af5c6bf0ea</sub>
- PAGE-29 and PAGE-30 (push delivery, optional caller-supplied executor, rejected re-dispatch) have nothing to configure on .NET because a pull-based IAsyncEnumerable never invokes the consumer, the consumer's own await foreach decides its context, and serial in-order delivery is the protocol's definition (recorded as section 10 entry 19).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:142-146` · high · sha:68af5c6bf0ea</sub>
- The PAGE-34/PAGE-35 fetcher front-end is not built; its .NET shape is a factory taking Func<string?, CancellationToken, ValueTask<Page<T>>> keyed by a continuation token, which needs Page<T> to carry ContinuationToken/NextLink (PR #9 deferred ContinuationToken and token-based resumption).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:148-151` · high · sha:68af5c6bf0ea</sub>
- The blocking pager view is a Pageable<T> : IEnumerable<T> beside the async one (permitted beside the static Pageable factory because the arities differ) and waits on a genuinely synchronous pipeline path, since HttpPipeline.Send is sync-over-async today (section 5.3) and a sync pager built on it would inherit the thread-pool starvation hazard.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:151-154` · high · sha:68af5c6bf0ea</sub>
- PAGE-1's two consumption views over one lazy walk are AsyncPageable<T> : IAsyncEnumerable<T> for items and AsPages() for pages, the shape Azure.Core established.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:19-21` · high · sha:68af5c6bf0ea</sub>
- Both the item view and the page view are C# async iterators over one private PagesCore routine, so the item view is the page view flattened and the two cannot drift.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:21-22` · high · sha:68af5c6bf0ea</sub>
- PAGE-6's "construction triggers zero exchanges" is satisfied for free because an async iterator body does not run until the first MoveNextAsync and Pageable.Create only captures delegates.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:29-30` · high · sha:68af5c6bf0ea</sub>
- Pagination, SSE and serialization share one shape on .NET, a lazy pull-based sequence over a single-use HTTP body, using the runtime's IAsyncEnumerable<T>, await foreach and [EnumeratorCancellation] rather than a custom iteration protocol.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:3-6` · high · sha:68af5c6bf0ea</sub>
- .NET does not need the specification's carve-out for the non-blocking engine ("invoking a walk method is itself the consumption trigger") because its single engine is lazy on both paths, the stronger reading.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:30-32` · high · sha:68af5c6bf0ea</sub>
- Because the iterator holds no resource at any suspension point, a consumer that abandons the enumerator by hand strands nothing.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:53-54` · high · sha:68af5c6bf0ea</sub>
- Page<T> owns no response and is not disposable; it is a sealed immutable carrier of the materialized items, status and headers, and the item view's eager-close (PAGE-11) is applied to both views, deviating from PAGE-3 ("a page MUST be a closeable resource owning exactly one underlying response") and the 12.1 preamble's "live response" (recorded as section 10 entry 17).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:56-69` · high · sha:68af5c6bf0ea</sub>
- The reference's live page was rejected for .NET because .NET cannot force a scoped close and the verified abandonment behaviour makes a live page strictly more dangerous, as a stranded .NET iterator has nothing like the reference page's reachable close().
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:61-65` · high · sha:68af5c6bf0ea</sub>
- PAGE-12's machinery (up to two live pages, a one-slot look-ahead buffer, release-on-probe) collapses to nothing because zero pages are live at any yield, and PAGE-15's two-page suppressed-close case is unreachable.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:65-68` · high · sha:68af5c6bf0ea</sub>
- The engine disposes the response through section 5.2's suppressed-error helper (catch the primary, attempt disposal in its own try, attach a disposal failure to the primary, rethrow the primary with ExceptionDispatchInfo so its stack survives) rather than a bare finally, and the same helper serves SSE-29 and SSE-36.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:77-80` · high · sha:68af5c6bf0ea</sub>
- The strategy contract (PAGE-4, PAGE-5) is two delegates, selectItems Func<TPage, IReadOnlyList<T>> and nextRequest Func<TPage, Response, Request, Request?> with null as the single end-of-stream signal, rather than one PageInfo-returning parser.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:85-87` · high · sha:68af5c6bf0ea</sub>
- The engine deserializes the page envelope once through the serde and both delegates read the typed value, so PAGE-16's single read of the response body holds by construction.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:87-89` · high · sha:68af5c6bf0ea</sub>
- PAGE-36 holds because the same DexpaceClientOptions and cancellation token reach every page's HttpPipeline.SendAsync and each page is an independent pipeline invocation with a fresh PipelineContext, so retry, auth and telemetry govern pages 2..N.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:94-96` · high · sha:68af5c6bf0ea</sub>

## Reference
- The non-blocking engine has no separate lazy obtain step; invoking a walk method is itself the consumption trigger and begins fetching immediately, but the one-exchange-per-page-consumed guarantee still holds.
  <sub>spec · `docs/product-spec/12-pagination.md:10-10` · high · sha:ba759edd34ec</sub>
- An empty page carrying a non-null next-request still counts as one consumed exchange.
  <sub>spec · `docs/product-spec/12-pagination.md:11-11` · high · sha:ba759edd34ec</sub>
- A strategy is a stateless parser that, given a response and the original request template, returns a PageInfo carrying the page's items plus the next-page request.
  <sub>spec · `docs/product-spec/12-pagination.md:24-24` · high · sha:ba759edd34ec</sub>
- An empty items list paired with a non-null next-request is a valid non-terminal page.
  <sub>spec · `docs/product-spec/12-pagination.md:26-26` · high · sha:ba759edd34ec</sub>
- The pagination engine is transport-agnostic and serde-agnostic; a single stateless strategy parses each response into the page's items plus the fully-formed next-page request (or an end-of-stream signal), and the engine drives iteration, owns each page's live HTTP response, and bounds a misbehaving server with a page cap.
  <sub>spec · `docs/product-spec/12-pagination.md:3-3` · high · sha:ba759edd34ec</sub>
- The next-page request is built by splicing the query string, not by re-rendering the whole URL.
  <sub>spec · `docs/product-spec/12-pagination.md:57-57` · high · sha:ba759edd34ec</sub>
- The async engine drives the walk inside a future/completion graph without blocking a thread per page.
  <sub>spec · `docs/product-spec/12-pagination.md:66-66` · high · sha:ba759edd34ec</sub>
- The engine exposes an item-level view that flattens each page's items into one ordered sequence and a page-level view that yields whole pages exposing raw per-page status, headers, originating request, and the live response.
  <sub>spec · `docs/product-spec/12-pagination.md:7-7` · high · sha:ba759edd34ec</sub>
- By default the async consumer runs inline on the page-completion thread.
  <sub>spec · `docs/product-spec/12-pagination.md:72-72` · high · sha:ba759edd34ec</sub>
- An alternative fetcher-based front-end lets the caller supply a first-page fetcher and a next-page fetcher instead of a strategy.
  <sub>spec · `docs/product-spec/12-pagination.md:80-80` · high · sha:ba759edd34ec</sub>
- PAGE-35 - A mutable paging-options object offered to fetchers is single-consumer and need not be thread-safe.
  <sub>spec · `docs/product-spec/12-pagination.md:83-83` · high · sha:ba759edd34ec</sub>
- The built-in PageNumber strategy (PAGE-17) ends on a caller predicate rather than on an empty item list, parses the current page with culture-sensitive int.TryParse(string, out int) rather than invariant base-10, and hard-codes the start page to 1 rather than making it configurable.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:100-102` · high · sha:68af5c6bf0ea</sub>
- The built-in LinkHeader strategy (PAGE-18) reads only the first Link header instance (losing links a server splits across instances, PAGE-20), splits rel tokens on space but not tab, does not handle quoted-pair escapes, and hard-codes the header name.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:102-104` · high · sha:68af5c6bf0ea</sub>
- PAGE-19's query-only reference is correct because new Uri(base, "?page=2") keeps /repo/issues and replaces only the query.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:104-106` · high · sha:68af5c6bf0ea</sub>
- PAGE-19 says the base is the originating page's response URL, but the as-built code resolves against the current request URL, which differs after a redirect.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:110-112` · high · sha:68af5c6bf0ea</sub>
- The as-built query splice keeps duplicates after the first match where PAGE-23 says to replace the first occurrence in place and drop further duplicates, and it has no remove-on-null path.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:118-119` · high · sha:68af5c6bf0ea</sub>
- PAGE-22 holds because Uri.EscapeDataString encodes space as %20 and + as %2B, and Uri.UnescapeDataString("a+b") returns a+b.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:126-127` · high · sha:68af5c6bf0ea</sub>
- PAGE-24 holds for userinfo, port, path and fragment through the as-built splice.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:127-128` · high · sha:68af5c6bf0ea</sub>
- PAGE-25 holds because no thread blocks per page and the [EnumeratorCancellation] token flows into SendAsync, so cancelling aborts the in-flight exchange.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:131-132` · high · sha:68af5c6bf0ea</sub>
- PAGE-26's page-granular cancellation holds because the token is observed at the top of each page iteration and inside the send, never inside the inner item loop, so items of an already-fetched page still reach the consumer.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:132-134` · high · sha:68af5c6bf0ea</sub>
- PAGE-27's exactly-once close is provided by the engine's finally block.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:134-135` · high · sha:68af5c6bf0ea</sub>
- PAGE-28's surfacing of the original underlying cause is free on the await path, which never wraps in AggregateException (only .Result/.Wait() do, which section 5.3 keeps off every path).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:135-137` · high · sha:68af5c6bf0ea</sub>
- PAGE-32 holds because a disposal failure on the success path propagates from MoveNextAsync.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:140-140` · high · sha:68af5c6bf0ea</sub>
- As built (d45e64b), pagination is partial: AsyncPageable/AsPages/Page/strategies are built, while the page-view single-use guard, construction-time cap validation, suppressed-close helper, Page.Request, case-sensitive single-value splice, strategy defaults, multi-instance Link headers, invalid-target rejection, fetcher front-end and blocking view are missing.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:155-157` · high · sha:68af5c6bf0ea</sub>
- Behaviour marked verified in design chapter 7 was run as file-based apps on .NET SDK 10.0.401 (runtime 10.0.12), and net8.0-floor claims were checked against the Microsoft.NETCore.App.Ref 8.0.31 reference pack because no .NET 8 runtime was available.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:3-8` · high · sha:68af5c6bf0ea</sub>
- On .NET 10.0.401, await foreach with break runs the iterator's finally, and so does System.Linq.AsyncEnumerable's FirstAsync.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:46-48` · high · sha:68af5c6bf0ea</sub>
- The cost of the non-live Page<T> is a capability rather than a guarantee: a page-view consumer cannot stream a page's raw body.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:64-65` · high · sha:68af5c6bf0ea</sub>
- The raw response remains reachable through the nextRequest delegate, which receives it before disposal, where header-driven continuation (PAGE-18) needs it.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:68-69` · high · sha:68af5c6bf0ea</sub>
- PAGE-2 holds except that Page<T> carries status and headers but not the originating request that the specification lists; adding Page<T>.Request is a one-field change.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:69-71` · high · sha:68af5c6bf0ea</sub>
- PAGE-15's single-page case, a disposal failure on the success path, propagates unchanged from MoveNextAsync as the requirement asks.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:80-82` · high · sha:68af5c6bf0ea</sub>
- A deserializer returning null for the page envelope is currently rethrown as InvalidOperationException, whereas it belongs in the serde family as a DeserializationException naming TPage (SERDE-13).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:82-83` · high · sha:68af5c6bf0ea</sub>
- PAGE-9's cap exists as maxPages, counts exchanges and stops fetching when reached, but is not validated at construction, so maxPages: 0 silently yields an empty walk instead of failing fast as PAGE-9 requires ("The cap MUST be validated as strictly positive at construction").
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:90-93` · high · sha:68af5c6bf0ea</sub>
- PAGE-10's unbounded default cap holds.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:93-94` · high · sha:68af5c6bf0ea</sub>
- The built-in Cursor strategy (PAGE-16) requires the query-parameter name rather than defaulting to "cursor".
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:98-100` · high · sha:68af5c6bf0ea</sub>

## Conflicts

## Superseded

