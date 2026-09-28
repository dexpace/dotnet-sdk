# pagination

## Rules
- A port MUST preserve the pagination engine's two-view model, page-lazy fetch discipline, deterministic response-lifecycle management, and strategy contract.
  <sub>spec · `docs/product-spec/12-pagination.md:3-3` · high · sha:ba759edd34ec</sub>
- The pagination engine MUST expose both an item-level view (flattened, ordered across page boundaries) and a page-level view (whole pages with status, headers, request, and live response) over the same walk. (PAGE-1)
  <sub>spec · `docs/product-spec/12-pagination.md:7-9` · high · sha:ba759edd34ec</sub>
- Pagination iteration MUST be page-lazy so exactly one HTTP exchange occurs per page yielded, with zero exchanges triggered merely by constructing the paginator or obtaining its iterator/stream. (PAGE-6)
  <sub>spec · `docs/product-spec/12-pagination.md:10-10` · high · sha:ba759edd34ec</sub>
- The pagination engine MUST fetch only forward and only on demand, stopping permanently after a strategy returns a null/absent next-request, and repeated end-of-stream probes MUST be idempotent. (PAGE-7)
  <sub>spec · `docs/product-spec/12-pagination.md:11-11` · high · sha:ba759edd34ec</sub>
- Each independent pagination iteration MUST restart from the initial request with fresh state, while the engine itself holds only immutable configuration and is safe to share across iterations. (PAGE-8)
  <sub>spec · `docs/product-spec/12-pagination.md:12-12` · high · sha:ba759edd34ec</sub>
- Per-call request overrides such as timeout, retry budget, and tags supplied to the strategy-based pagination engine MUST be applied to every page exchange, not just the first, and the default is no overrides. (PAGE-36)
  <sub>spec · `docs/product-spec/12-pagination.md:13-13` · high · sha:ba759edd34ec</sub>
- A Page's materialized item list and its derived status, headers, and originating request MUST remain readable after the page is closed, with only the raw response body/connection invalidated at close, and items MUST never be null. (PAGE-2)
  <sub>spec · `docs/product-spec/12-pagination.md:19-19` · high · sha:ba759edd34ec</sub>
- A Page MUST be a closeable resource owning exactly one underlying response, and closing responsibility transfers to whoever holds the page rather than staying with the component that produced it. (PAGE-3)
  <sub>spec · `docs/product-spec/12-pagination.md:20-20` · high · sha:ba759edd34ec</sub>
- A pagination strategy's parse output MUST always be well-formed and signal end-of-stream solely via a null/absent next-request, never by throwing or via a side channel, with an empty items list plus a non-null next-request being a valid non-terminal page. (PAGE-4)
  <sub>spec · `docs/product-spec/12-pagination.md:26-26` · high · sha:ba759edd34ec</sub>
- A pagination strategy MUST read everything it needs from the response synchronously inside parse, must not retain the response or its body beyond the call, must not close or mutate the response, and must be immutable and safe to share concurrently. (PAGE-5)
  <sub>spec · `docs/product-spec/12-pagination.md:27-27` · high · sha:ba759edd34ec</sub>
- The pagination engine MUST accept a page cap counting exchanges (not items) that is validated as strictly positive at construction, and MUST stop fetching once reached even if the strategy still reports a next-request. (PAGE-9)
  <sub>spec · `docs/product-spec/12-pagination.md:31-31` · high · sha:ba759edd34ec</sub>
- The default pagination page cap SHOULD be effectively unbounded, matching plain lazy-sequence semantics, and documentation SHOULD direct production callers to set a finite cap. (PAGE-10)
  <sub>spec · `docs/product-spec/12-pagination.md:32-32` · high · sha:ba759edd34ec</sub>
- The item-level pagination view MUST eager-close each page before yielding any of that page's items, after copying the materialized items, so abandoning iteration mid-page never strands the response. (PAGE-11)
  <sub>spec · `docs/product-spec/12-pagination.md:38-38` · high · sha:ba759edd34ec</sub>
- The page-level pagination view MUST close the previous page as the consumer advances, close the last page at exhaustion, and buffer a fetched-but-undelivered page so an emptiness probe or early break followed by explicit close still releases it. (PAGE-12)
  <sub>spec · `docs/product-spec/12-pagination.md:39-39` · high · sha:ba759edd34ec</sub>
- The page-level pagination view MUST be single-use, allowing its iterator/stream to be obtained at most once, with re-iteration failing rather than silently restarting. (PAGE-14)
  <sub>spec · `docs/product-spec/12-pagination.md:40-40` · high · sha:ba759edd34ec</sub>
- A close error while releasing held pagination pages MUST be surfaced rather than swallowed, and when both held pages fail to close, the first failure MUST propagate with the second attached as suppressed. (PAGE-15)
  <sub>spec · `docs/product-spec/12-pagination.md:41-41` · high · sha:ba759edd34ec</sub>
- If a pagination strategy's parse throws, the engine MUST close that response inline on the exceptional path and propagate the failure, attaching any close failure as a suppressed/secondary error rather than masking the parse failure. (PAGE-13)
  <sub>spec · `docs/product-spec/12-pagination.md:45-45` · high · sha:ba759edd34ec</sub>
- A rel=next target in the Link-header pagination strategy MUST resolve as an RFC 3986 reference against the originating page's response URL, preserving the base path for a query-only reference, and MUST be treated as end-of-stream rather than an error if it cannot resolve into a valid URL. (PAGE-19)
  <sub>spec · `docs/product-spec/12-pagination.md:52-52` · high · sha:ba759edd34ec</sub>
- When a server splits pagination links across multiple separate Link header instances, the strategy SHOULD normalize them by concatenation, and an empty header set SHOULD map to no next link. (PAGE-20)
  <sub>spec · `docs/product-spec/12-pagination.md:53-53` · high · sha:ba759edd34ec</sub>
- When rewriting the next-page query string, the rebuilder MUST splice the raw query verbatim, copying every untargeted parameter byte-for-byte and preserving order, rather than re-rendering or canonicalizing the whole query. (PAGE-21)
  <sub>spec · `docs/product-spec/12-pagination.md:59-59` · high · sha:ba759edd34ec</sub>
- Encoding a newly-set pagination query parameter MUST use RFC 3986 component encoding (space encodes to `%20`, literal `+` preserved as data), and reading a parameter MUST decode with the same RFC 3986 semantics. (PAGE-22)
  <sub>spec · `docs/product-spec/12-pagination.md:60-60` · high · sha:ba759edd34ec</sub>
- Setting a pagination query parameter MUST replace the first existing occurrence in place, drop further duplicates, append if absent, and remove entirely when the new value is null, while following an absolute next URL swaps only the request's URL, preserving method, headers, and body. (PAGE-23)
  <sub>spec · `docs/product-spec/12-pagination.md:61-61` · high · sha:ba759edd34ec</sub>
- Pagination URL rewriting MUST preserve scheme, userinfo, host, port, path, and fragment exactly, changing only the query. (PAGE-24)
  <sub>spec · `docs/product-spec/12-pagination.md:62-62` · high · sha:ba759edd34ec</sub>
- The async pagination engine MUST drive fetch, parse, delivery, and re-arm inside the async completion graph without any thread blocking on a page, and cancelling/completing the walk's result future MUST halt further pages and best-effort abort the in-flight exchange. (PAGE-25)
  <sub>spec · `docs/product-spec/12-pagination.md:68-68` · high · sha:ba759edd34ec</sub>
- Async pagination cancellation MUST take effect at page granularity — items already being delivered from a settling page still reach the consumer, while a fetched-but-not-yet-drained page is dropped undrained and closed with any close error swallowed. (PAGE-26)
  <sub>spec · `docs/product-spec/12-pagination.md:69-69` · high · sha:ba759edd34ec</sub>
- The async pagination engine MUST close each page's response exactly once, whether after drain, when dropping a fetched-but-undrained page, or inline on parse failure, with no double-close and no leak. (PAGE-27)
  <sub>spec · `docs/product-spec/12-pagination.md:70-70` · high · sha:ba759edd34ec</sub>
- A consumer throw, transport/connection failure, parse failure, or null success completion MUST terminate the async pagination walk and complete the result future exceptionally with the original underlying cause unwrapped. (PAGE-28)
  <sub>spec · `docs/product-spec/12-pagination.md:71-71` · high · sha:ba759edd34ec</sub>
- For a single async pagination walk the consumer MUST NOT be invoked concurrently, items MUST be delivered one at a time in server order, and the driver runs inline on the page-completion thread by default or on a caller-supplied executor when configured. (PAGE-29)
  <sub>spec · `docs/product-spec/12-pagination.md:72-72` · high · sha:ba759edd34ec</sub>
- If the async pagination engine's executor rejects a (re-)dispatch, the walk MUST terminate with the result future completed exceptionally carrying the rejection, and any staged page MUST be closed. (PAGE-30)
  <sub>spec · `docs/product-spec/12-pagination.md:73-73` · high · sha:ba759edd34ec</sub>
- The async pagination driver SHOULD process synchronously-completed page futures iteratively via a trampoline rather than recursive future composition, so a long run of already-complete pages does not overflow the stack. (PAGE-31)
  <sub>spec · `docs/product-spec/12-pagination.md:74-74` · high · sha:ba759edd34ec</sub>
- In the async pagination drain path, releasing a page's response MUST happen whether the consumer succeeds or throws, with a throwing close reported through the result future on the success path but swallowed if the consumer already failed. (PAGE-32)
  <sub>spec · `docs/product-spec/12-pagination.md:75-75` · high · sha:ba759edd34ec</sub>
- A port MUST document the inherent async pagination cancellation race whereby a response delivered after an external cancel settles the transport future never reaches the paginator's close path, while an already-dispatched request MAY still complete after abort and must then be closed and discarded. (PAGE-33)
  <sub>spec · `docs/product-spec/12-pagination.md:76-76` · high · sha:ba759edd34ec</sub>
- The fetcher-based pagination front-end MUST call the first-page fetcher exactly once, then key the next-page fetcher off the previous page's next link (falling back to its continuation token when absent), ending the stream on a blank next link with no fallback token or a null page from either fetcher. (PAGE-34)
  <sub>spec · `docs/product-spec/12-pagination.md:82-82` · high · sha:ba759edd34ec</sub>
- If a mutable paging-options object is offered to pagination fetchers, the same instance SHOULD be threaded through every fetcher call so a custom retriever can stash cursor/state between pages. (PAGE-35)
  <sub>spec · `docs/product-spec/12-pagination.md:83-83` · high · sha:ba759edd34ec</sub>

## Constraints

## Conclusions

## Reference
- The built-in cursor pagination strategy reads items and the next cursor from a single read of the response body, treats a null or empty next cursor as end-of-stream, and derives the next request via a configurable cursor query parameter defaulting to `cursor`. (PAGE-16)
  <sub>spec · `docs/product-spec/12-pagination.md:49-49` · high · sha:ba759edd34ec</sub>
- The built-in page-number pagination strategy treats an empty items list as end-of-stream, infers the current page from the originating request's page query parameter (defaulting to a configurable start page, default 1, when absent/empty/non-numeric), and sets the next request's page to current+1, with the page parameter name (default `page`) and start page configurable. (PAGE-17)
  <sub>spec · `docs/product-spec/12-pagination.md:50-50` · high · sha:ba759edd34ec</sub>
- The built-in Link-header pagination strategy selects the next URL using RFC 5988/8288 semantics — the first link-value whose case-insensitive `rel` contains the token `next` — correctly handling quoted commas and quoted-pair escapes, with the header name configurable (default `Link`). (PAGE-18)
  <sub>spec · `docs/product-spec/12-pagination.md:51-51` · high · sha:ba759edd34ec</sub>
- A cursor/continuation token is an opaque string a server returns to identify the next page of a paginated result, which the pagination cursor strategy folds into the next request's query.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:21` · high · sha:f0b3d2058626</sub>
- A Page wraps one page of results from the live transport response; its materialized items and derived metadata survive close, while the raw body/connection is valid only until close.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:39` · high · sha:f0b3d2058626</sub>
- PageInfo is a pagination strategy's parse output, consisting of the items on a page plus the next-page request, where a null/absent next-request is the single end-of-stream signal.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:41` · high · sha:f0b3d2058626</sub>
- A pagination strategy is a stateless, immutable parser that, given a response and the original request template, returns a PageInfo, with three built-ins being cursor, page-number, and Link-header.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:43` · high · sha:f0b3d2058626</sub>
- Item view and page view iterating a single 3-page walk yield concatenated server-order items and three page objects. (PAGE-1, PAGE-2, PAGE-3)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:7` · high · sha:0451cc7f3bb4</sub>
- Page metadata survives after the page's underlying response is closed. (PAGE-1, PAGE-2, PAGE-3)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:7` · high · sha:0451cc7f3bb4</sub>
- A page closes its underlying response exactly once, and a fetcher does not close the page's response itself. (PAGE-1, PAGE-2, PAGE-3)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:7` · high · sha:0451cc7f3bb4</sub>
- Blocking iteration over a pagination triggers zero exchanges until the first probe and then one exchange per page consumed, while async iteration begins fetching as soon as it is invoked. (PAGE-6, PAGE-7, PAGE-8)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:8` · high · sha:0451cc7f3bb4</sub>
- Iteration performs no fetch past the terminal page, and repeated end-of-stream probes are idempotent. (PAGE-6, PAGE-7, PAGE-8)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:8` · high · sha:0451cc7f3bb4</sub>
- Two full iterations over the same pagination each drive a complete fetch sequence. (PAGE-6, PAGE-7, PAGE-8)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:8` · high · sha:0451cc7f3bb4</sub>
- A page cap of zero or less throws at construction, and a non-advancing (looping) server causes iteration to stop at exactly the cap number of exchanges. (PAGE-9, PAGE-10)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:9` · high · sha:0451cc7f3bb4</sub>
- The default page cap is effectively unbounded. (PAGE-9, PAGE-10)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:9` · high · sha:0451cc7f3bb4</sub>
- The item view eager-closes each page before yielding its items when a page is only partially consumed. (PAGE-11, PAGE-12, PAGE-14, PAGE-15)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:10` · high · sha:0451cc7f3bb4</sub>
- The page view releases held and buffered pages on early break or on a probe followed by close. (PAGE-11, PAGE-12, PAGE-14, PAGE-15)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:10` · high · sha:0451cc7f3bb4</sub>
- Requesting a second page-view iterator throws. (PAGE-11, PAGE-12, PAGE-14, PAGE-15)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:10` · high · sha:0451cc7f3bb4</sub>
- A close error on a held page surfaces wrapped, with a second close failure suppressed. (PAGE-11, PAGE-12, PAGE-14, PAGE-15)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:10` · high · sha:0451cc7f3bb4</sub>
- When parsing a page throws, the response is closed inline with the parse error as the primary error and any close error suppressed. (PAGE-13)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:11` · high · sha:0451cc7f3bb4</sub>
- The cursor pagination strategy treats a null or empty cursor as end-of-stream after a single body read. (PAGE-16, PAGE-17, PAGE-18, PAGE-20, PAGE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:12` · high · sha:0451cc7f3bb4</sub>
- The page-number pagination strategy treats empty items as end-of-stream and falls back to the start page on garbage input. (PAGE-16, PAGE-17, PAGE-18, PAGE-20, PAGE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:12` · high · sha:0451cc7f3bb4</sub>
- The Link-header pagination strategy handles rel=next with quoted commas, a multi-token rel attribute, and multi-header splitting. (PAGE-16, PAGE-17, PAGE-18, PAGE-20, PAGE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:12` · high · sha:0451cc7f3bb4</sub>
- A query-only next-page reference preserves the base path, and an unparseable target is treated as end-of-stream. (PAGE-16, PAGE-17, PAGE-18, PAGE-20, PAGE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:12` · high · sha:0451cc7f3bb4</sub>
- Verbatim query splicing when following pagination links preserves untouched query parameters byte-for-byte. (PAGE-21, PAGE-22, PAGE-23, PAGE-24)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:13` · high · sha:0451cc7f3bb4</sub>
- Query component encoding/decoding follows RFC 3986 and treats `+` as data. (PAGE-21, PAGE-22, PAGE-23, PAGE-24)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:13` · high · sha:0451cc7f3bb4</sub>
- Setting a query parameter replaces the first occurrence, appending adds a new one, removing deletes it, and a whole-URL follow preserves the original method, headers, and body. (PAGE-21, PAGE-22, PAGE-23, PAGE-24)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:13` · high · sha:0451cc7f3bb4</sub>
- Non-query URL components are preserved when following a pagination link. (PAGE-21, PAGE-22, PAGE-23, PAGE-24)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:13` · high · sha:0451cc7f3bb4</sub>
- Async pagination performs no per-page thread blocking, and cancellation aborts the in-flight transport future. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- Page-granular cancellation in async pagination drops and closes a staged page. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- Every response in an async pagination walk is closed exactly once across all code paths. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- Async pagination failures surface the original cause, including when the eager transport call itself throws. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- Async pagination supports serial ordered delivery as well as an executor-driven mode. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- An executor rejection during async pagination fails the walk and closes the staged page. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- Async pagination trampolines correctly over thousands of synchronously-completing pages without stack growth issues. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- A throwing close on an otherwise-successful async page is reported through the future. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- Ownership of a cancellation race in async pagination is documented behavior. (PAGE-25, PAGE-26, PAGE-27, PAGE-28, PAGE-29, PAGE-30, PAGE-31, PAGE-32, PAGE-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:14` · high · sha:0451cc7f3bb4</sub>
- The pagination fetcher front-end invokes the first fetcher once, follows the next-link over a token, and terminates on a blank link or null. (PAGE-34, PAGE-35)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:15` · high · sha:0451cc7f3bb4</sub>
- The same options instance is threaded through the pagination fetcher, and mutations to it are observed. (PAGE-34, PAGE-35)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:15` · high · sha:0451cc7f3bb4</sub>
- Per-call options reach every page exchange during pagination. (PAGE-36)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:16` · high · sha:0451cc7f3bb4</sub>

## Conflicts

## Superseded
