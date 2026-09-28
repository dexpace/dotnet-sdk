## 7. Pagination, SSE, and Serialization

Three subsystems share one shape on .NET: a lazy, pull-based sequence over a single-use HTTP body. The runtime
ships that shape — `IAsyncEnumerable<T>`, `await foreach`, `[EnumeratorCancellation]` — so the question this chapter
answers is not which iteration protocol to adopt but where the runtime's protocol silently stops honouring the
specification's lifecycle and parsing clauses. Behaviour asserted as verified below was run as file-based apps on the
scratchpad .NET SDK 10.0.401 (runtime 10.0.12); a claim about the `net8.0` floor was checked against the
`Microsoft.NETCore.App.Ref` 8.0.31 reference pack, since no .NET 8 runtime was available to execute on.

### 7.1 Pagination

**PAGE-1**'s two consumption views over one lazy walk are `AsyncPageable<T> : IAsyncEnumerable<T>` (items) and
`AsPages()` (pages), the shape Azure.Core established and .NET consumers already recognise (P14). Both are C# async
iterators over one private `PagesCore` routine, so the item view is literally the page view flattened and the two
cannot drift. The composition benefit Ruby got from `Enumerable` arrives here with one version-dependent caveat
worth stating because it is invisible in an IDE targeting .NET 10: **`System.Linq.AsyncEnumerable` — `FirstAsync`,
`Take`, `Where` over `IAsyncEnumerable<T>` — is in the shared framework from .NET 10 only** (verified:
`System.Linq.AsyncEnumerable.dll` is in `Microsoft.NETCore.App` 10.0.12 and absent from the 8.0.31 reference pack).
On the `net8.0` target the same operators come from a NuGet package, which core does not take (**NFR-1**); callers
on .NET 8 bring it themselves, and nothing in core's surface depends on it.

**PAGE-6**'s "construction triggers zero exchanges" is free: an async iterator's body does not run until the first
`MoveNextAsync`, and `Pageable.Create` only captures delegates. The specification carves out the non-blocking engine
("invoking a walk method is itself the consumption trigger"); .NET does not need the carve-out, because its single
engine is lazy on both paths, which is the stronger of the two readings. **PAGE-8**'s fresh restart per iteration
is also free, and for the same reason it is a hazard for **PAGE-14**: calling `GetAsyncEnumerator` twice on one
compiler-generated async-iterator object starts a second, independent walk rather than failing (verified: two
`await foreach` passes over one sequence yielded ten items from a five-item generator). That is exactly right for
the item view (**PAGE-8**) and exactly wrong for the page view, which **PAGE-14** requires be single-use. The fix is
a thin wrapper: `AsPages()` returns a sequence whose `GetAsyncEnumerator` latches an `Interlocked` flag and throws
`InvalidOperationException` on the second call. Each *call* to `AsPages()` is still a fresh walk — the single-use
rule binds the returned object, as it does in the reference.

**The close-on-abandon question, and the verified answer that shapes the subsystem.** **PAGE-11** requires the item
view to eager-close each page before yielding its items; **PAGE-12** requires the page view to close the previous
page on advance and release a fetched-but-undelivered page; **SSE-25** wants the same of a partially consumed
stream. .NET's early-termination hook is `IAsyncDisposable.DisposeAsync` on the enumerator, which runs the
iterator's pending `finally` blocks. The hidden precondition (P7) is that *someone must call it*. Verified on
10.0.401: `await foreach` with `break` runs the iterator's `finally`, and so does `System.Linq.AsyncEnumerable`'s
`FirstAsync`; a consumer that calls `GetAsyncEnumerator()` and `MoveNextAsync()` by hand and then drops the
enumerator without `await using` leaves the `finally` **unexecuted, even after two forced full GCs** — compiler
generated enumerators have no finalizer, and garbage collection is not a cleanup hook. This is Ruby's
external-`Enumerator` result reproduced on a different runtime, and it produces the same hard rule: **no live
response ever spans a `yield return`.** The engine sends the request, deserializes the page, captures status and
headers, asks the next-request delegate for the continuation while the response is still open, and disposes the
response in a `finally` — all *before* the `yield`. A consumer that abandons the enumerator by hand therefore
strands nothing, because at every suspension point the iterator holds no resource.

That rule has a consequence the specification did not anticipate, and it is argued rather than hidden. **PAGE-3**
requires that "A page MUST be a closeable resource owning exactly one underlying response", and the page view that
**PAGE-1** makes mandatory is described in the specification's 12.1 preamble (`docs/product-spec/12-pagination.md`)
as yielding pages "each exposing ... the live response". `Page<T>` owns no response: it is a sealed, immutable
carrier of the materialized items, the status and the headers, and it is not disposable. The item view's
eager-close (**PAGE-11**) is applied to *both* views. The reference keeps the page live because a JVM caller cannot
be forced into a scoped close; .NET cannot force one either (above), and the verified abandonment behaviour makes a
live page strictly more dangerous here than there, because the reference's page at least has a `close()` a
disciplined caller can reach while a stranded .NET iterator has nothing. What is lost is a capability, not a
guarantee: a page-view consumer cannot stream a page's raw body. **PAGE-12**'s machinery — up to two live pages, a
one-slot look-ahead buffer, release-on-probe — collapses to nothing because zero pages are live at any yield
(P4), and **PAGE-15**'s two-page suppressed-close case is unreachable. The raw response remains reachable where it
is safe to reach: the `nextRequest` delegate receives it before disposal, which is where header-driven continuation
(**PAGE-18**) needs it. Recorded as §10 entry 17. **PAGE-2** survives with one gap: the page
carries status and headers but not the originating request, which the specification lists; adding
`Page<T>.Request` is a one-field change.

**PAGE-13** is where the obvious C# idiom is wrong (P13). The engine disposes the response in a `finally`, and a
`finally` block that throws *replaces* the exception already in flight — verified: `try { throw new
InvalidOperationException("parse failed"); } finally { throw new IOException("close failed"); }` surfaces only the
`IOException`, with no inner exception. **PAGE-13** says "A close failure MUST NOT mask the parse failure — it MUST
be attached as a suppressed/secondary error, with the parse error primary". The engine therefore disposes through
§5.2's suppressed-error helper — catch the primary, attempt the disposal inside its own `try`, attach a disposal
failure to the primary, rethrow the primary with `ExceptionDispatchInfo` so its stack survives — rather than a bare
`finally`. The same helper serves **SSE-29** and **SSE-36** below. **PAGE-15**'s single-page case (a disposal
failure on the success path) propagates from `MoveNextAsync` unchanged, which is what the requirement asks. A
deserializer that returns `null` for the page envelope is currently rethrown as `InvalidOperationException`; it
belongs in the serde family as a `DeserializationException` naming `TPage` (**SERDE-13**, §7.3).

**The strategy contract** (**PAGE-4**, **PAGE-5**) is expressed as two delegates rather than one `PageInfo`-returning
parser: `selectItems: Func<TPage, IReadOnlyList<T>>` and `nextRequest: Func<TPage, Response, Request, Request?>`,
where `null` is the single end-of-stream signal. This changes *how* **PAGE-4** is satisfied, not *whether* (P6): the
engine deserializes the envelope once through the serde, both delegates read the typed value, and **PAGE-16**'s
"single read of the response body" holds by construction. Delegates are immutable and shareable (**PAGE-5**); the
built-in strategies capture only configuration. **PAGE-9**'s cap exists as `maxPages`, counts exchanges, and stops
fetching when reached — but it is not validated at construction, so `maxPages: 0` silently yields an empty walk
instead of failing fast as **PAGE-9** requires ("The cap MUST be validated as strictly positive at construction");
`ArgumentOutOfRangeException.ThrowIfNegativeOrZero` in `Pageable.Create` closes it. **PAGE-10**'s unbounded default
holds. **PAGE-36** holds: the same `DexpaceClientOptions` and cancellation token reach every page's
`HttpPipeline.SendAsync`, and each page is an independent pipeline invocation with a fresh `PipelineContext`, so
retry, auth and telemetry govern pages 2..N.

**The built-in strategies each diverge from the letter in small ways**, listed so the roadmap can close them in one
pass. `Cursor` (**PAGE-16**) requires the query-parameter name rather than defaulting to `cursor`. `PageNumber`
(**PAGE-17**) ends on a caller predicate rather than on an empty item list, parses the current page with the
culture-sensitive `int.TryParse(string, out int)` rather than invariant base-10, and hard-codes the start page to 1
rather than making it configurable. `LinkHeader` (**PAGE-18**) reads only the first `Link` header instance, so a
server that splits links across instances (**PAGE-20**) loses them; splits `rel` tokens on space but not tab; does
not handle quoted-pair escapes; and hard-codes the header name. **PAGE-19**'s query-only reference is correct —
`new Uri(base, "?page=2")` keeps `/repo/issues` and replaces only the query (verified) — but **PAGE-19**'s
"cannot resolve into a valid URL" clause meets a lenient parser: `Uri.TryCreate(base, "not a url", out _)`
*succeeds*, producing `https://h/repo/not%20a%20url` (verified), so the specification's own conformance fixture
(`<not a url>; rel=next` → end of stream) is followed as a relative path. The resolver must reject a target
containing characters RFC 3986 forbids unescaped (space, `<`, `>`, `"`) before resolution. The existing scheme guard
(only `http`/`https` survive) is a good addition the specification does not require, and stays. **PAGE-19** also
says the base is "the originating page's response URL"; the as-built resolves against the current request URL,
which differs after a redirect.

**PAGE-21**'s verbatim query splice is this subsystem's obvious-tool-is-wrong case, twice over. First, the as-built
splice matches keys with `StringComparison.OrdinalIgnoreCase`, so setting `page` on `?Page=1` rewrote the caller's
`Page=1` to `page=2` (verified); query keys are case-sensitive and the match must be ordinal. It also keeps
duplicates after the first match, where **PAGE-23** says "replace the first existing occurrence in place (dropping
further duplicates ...)", and it has no remove-on-null path. Second, and not fixable in the splice: **`System.Uri`
canonicalizes the query before the splice ever sees it.** Verified: for `?x=%7E%41&page=1`, `Uri.Query` returns
`?x=~A&page=1` — percent-encoded unreserved characters are decoded — and a `UriBuilder` round trip additionally
writes an explicit default port into `OriginalString` (`https://h:443/p?...`). Since `Request.Url` is a `Uri` and the
transport sends `Uri`'s canonical form, byte-for-byte preservation of untargeted parameters is unattainable for as
long as the model's URL type is `System.Uri`. The residual is narrow and named (P6, P8): the only rewriting is
RFC 3986 §6.2.2.2 percent-encoding normalization of *unreserved* characters, which the RFC defines as equivalent,
so a conforming server cannot observe it; reserved characters, value-less flags (`?flag`), order and `+` are
preserved (verified). **PAGE-22** holds: `Uri.EscapeDataString` encodes space as `%20` and `+` as `%2B`, and
`Uri.UnescapeDataString("a+b")` returns `a+b` (verified). **PAGE-24** holds for userinfo, port, path and fragment
(verified through the as-built splice). Recorded as §10 entry 18.

**The async engine** (**PAGE-25**–**PAGE-33**) is not a second engine: .NET has one iteration protocol and it is
already non-blocking, so the reference's split collapses (P4). **PAGE-25**: no thread blocks per page, and the
`[EnumeratorCancellation]` token flows into `SendAsync`, so cancelling aborts the in-flight exchange. **PAGE-26**'s
page-granular cancellation holds because the token is observed at the top of each page iteration and inside the
send, never inside the inner item loop, so items of a page already fetched still reach the consumer. **PAGE-27**'s
exactly-once close is the `finally` above. **PAGE-28**'s "surfacing the *original* underlying cause" is free on the
`await` path, which never wraps in `AggregateException` (only `.Result`/`.Wait()` do, which §5.3 keeps off every
path). **PAGE-31** is satisfied by construction: the walk is a `while` loop in a state machine, and a synchronously
completing `ValueTask` continues inline without growing the stack — the specification's own latitude applies ("A
port on a runtime without deep-recursion risk MAY satisfy the intent with its native loop model but MUST NOT recurse
per page"). **PAGE-32** holds because a disposal failure on the success path propagates from `MoveNextAsync`.
**PAGE-33**'s race — a response delivered to a send that was already cancelled — is documented as the transport's
responsibility (**TRANSPORT-9**, §3.3). **PAGE-29** and **PAGE-30** presume push delivery: an engine that invokes a
consumer callback, optionally on a caller-supplied executor that may reject a re-dispatch. A pull-based
`IAsyncEnumerable` never invokes the consumer at all — the consumer's own `await foreach` decides its context, and
serial in-order delivery is the protocol's definition — so the executor mode has nothing to configure and the
rejection path cannot occur. Recorded as §10 entry 19.

**The fetcher front-end** (**PAGE-34**, **PAGE-35**) is not built. Its .NET shape is a factory taking
`Func<string?, CancellationToken, ValueTask<Page<T>>>` keyed by a continuation token, which needs `Page<T>` to carry
`ContinuationToken`/`NextLink` (the slice design had `ContinuationToken`; the as-built dropped it). **The blocking
view** — a `Pageable<T> : IEnumerable<T>` beside the async one, which C# permits beside the static `Pageable` factory
because the arities differ — waits on a genuinely synchronous pipeline path; `HttpPipeline.Send` is sync-over-async
today (§5.3), and a sync pager built on it would inherit the thread-pool starvation hazard rather than remove it.

**As built (d45e64b):** partial: `AsyncPageable`/`AsPages`/`Page`/strategies built; missing page-view single-use
guard, construction-time cap validation, suppressed-close helper, `Page.Request`, case-sensitive single-value splice,
strategy defaults, multi-instance Link headers, invalid-target rejection, fetcher front-end, and the blocking view.

### 7.2 Server-Sent Events

.NET *does* ship an SSE parser, which makes this section the reverse of Ruby's: the work is arguing against the
native implementation, not the absence of one. **`System.Net.ServerSentEvents.SseParser` is in the shared framework
from .NET 10 and is a NuGet package on .NET 8** (verified against both packs), and on .NET 10.0.401 it implements
strict WHATWG dispatch, which is precisely what the specification's preamble says a port "MUST replicate" the
deviations from "or offer a strict-WHATWG mode". Verified, feeding it the specification's own fixtures: an event
with no `event:` field reports type `message`, violating **SSE-10** ("MUST NOT be defaulted to `message`");
`data: a\ndata: b` arrives joined as `a\nb`, violating **SSE-8** ("the parser MUST NOT join them"); a comment-only
block, an id-only block and a retry-only block each dispatch *nothing*, violating **SSE-6** and **SSE-13**; an
unterminated final `data: hello` at end of stream is dropped, violating **SSE-14**; and the parser object keeps a
`LastEventId` across events, the WHATWG buffer **SSE-16** forbids. `SseParser` is therefore the named P13 gotcha of
this subsystem — it looks like the answer, and it is the strict mode the specification declines to make the
default. It is also unavailable in-box on the `net8.0` floor. Core implements its own parser; the specification's
choice between replicating the deviations and offering a strict mode is resolved as §11 item 17.
An adapter to `SseItem<T>` for callers who want the strict semantics is a possible later convenience, not a seam.

**The line reader is hand-written, and `StreamReader` is rejected for three verified reasons.** The tempting
implementation is `StreamReader.ReadLineAsync`, and its terminator handling is correct — LF, CR and CRLF all end a
line, and an unterminated final line is returned as content (**SSE-2**, **SSE-14**; verified). Three things are
wrong with it. First, **byte-order-mark detection is on by default and switches encodings**: a stream beginning
`FF FE` is decoded as UTF-16 (verified: the bytes `FF FE 64 00 61 00 0A 00` came back as the line `da`), whereas an
event stream is UTF-8 by definition. Second, the BOM clause needs the right constructor, not the default one:
`new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: false)` strips exactly one leading `EF BB BF`
and preserves a second consecutive BOM and any later one as `U+FEFF` (verified), which *is* **SSE-12**, while
`new UTF8Encoding(false)` strips none (verified) — three constructors, three behaviours, one correct. Third, and
decisive: **a lone CR at the end of the bytes currently available blocks the line read until more bytes arrive**,
because `ReadLine` must peek to decide whether an LF follows. Verified with a gated stream delivering
`data: x\r\r` and then pausing: the first line returns, the second `ReadLineAsync` did not complete within 500 ms
and completed only when the next event's bytes arrived. A server that terminates events with CR therefore has
every event delivered one event late — a live-stream latency defect **SSE-2**'s "a lone CR terminates a line by
itself" is written to exclude. (`StreamReader` also allocates a string per line and has no length bound.)
`PipeReader` is not the answer on the floor either: `System.IO.Pipelines` is in the shared framework from .NET 9 but
only a NuGet package on .NET 8 (verified: present in the 9.0.18 and 10.0.12 runtimes, absent from the 8.0.31
reference pack), so core cannot take it before the floor rises (§2.4, §9.2).

The reader is therefore a small byte-level state machine over `Stream.ReadAsync` into an `ArrayPool<byte>`-rented
buffer. A CR ends the line *immediately* and sets a one-bit "swallow a following LF" flag, so no read ever waits on
lookahead; the leading-BOM check inspects the first three bytes once and keeps them if they are not `EF BB BF`
(**SSE-12**); each completed line is decoded with `Encoding.UTF8`, whose default replacement fallback turns
malformed input into `U+FFFD` rather than throwing (verified). **SSE-19** is a MAY with a documented-divergence
escape — "a port MAY add a configurable cap and reject/truncate oversized lines, documenting the divergence" — and
the reader exercises it: lines over a configurable cap (default 1 MiB, matching **BODY-30**'s error-body cap) fail
the read, because an unbounded line from a hostile server is an unbounded allocation. Recorded as sanctioned,
§10 entry 20. **SSE-11**'s "a port MUST pick a documented cap and reject beyond it rather than
wrap" is honoured with the cap `int.MaxValue` milliseconds (about 24.8 days): digits are accumulated with an
overflow check and anything longer is ignored, never wrapped, and the value is surfaced as a `TimeSpan`. The cap is
chosen because every .NET timer API accepts it, and it matches the Ruby port's cap so two ports agree on the same
fixture. **SSE-1**, **SSE-3**–**SSE-9**, **SSE-13**–**SSE-16** are the field and dispatch grammar, implemented as
written on top of the line reader; the parser is non-thread-safe by contract (**SSE-18**) and never owns or
disposes the stream it reads (**SSE-17**).

**The parsed event is a value, and C# records get the list wrong by default.** `ServerSentEvent` is a sealed record
over the five fields — `Id`, `Event`, `Data`, `Comment`, `Retry` — with `Data` an `IReadOnlyList<string>` and every
absent field `null` rather than empty, so **SSE-4**'s present-but-empty stays distinct from absent. **SSE-21**'s
structural equality is not free (P13): a record's synthesized `Equals` compares a collection member *by reference*
(verified: two records wrapping equal `int[]` contents compare unequal), and so do `ImmutableArray<T>` and
`ReadOnlyCollection<T>`. The record overrides `Equals`/`GetHashCode` to compare `Data` element-wise. **SSE-20**'s
defensive copy lives in the `Data` property's `init` accessor, which copies into a private array, so neither the
constructor argument nor a `with { Data = list }` expression can alias a mutable list; a `with` that leaves `Data`
alone shares the already-immutable array, which is unobservable. **SSE-22**'s `IsEmpty` is true only when all five
fields are null, so a comment-only keep-alive is non-empty.

**Delivery is pull-based** (**SSE-39**), which `IAsyncEnumerable<T>` gives for free: the parser advances only when the
consumer's `MoveNextAsync` asks for the next event, a pending `ReadAsync` is the backpressure mechanism, and there is
no queue between parser and consumer to grow. The reader's byte buffer is a read-ahead of *bytes*, not of events —
a chunk may hold several events, and the stream is not read again until they are consumed — which is the level at
which **SSE-39**'s conformance ("raw `next()` call count against consumer pulls") measures. The raw convenience view
`ServerSentEventReader.ReadAsync(Stream)` (**SSE-40**) reuses one reader across pulls, so BOM consumption is
per-stream, and is single-pass by the same latched guard §7.1 gives the page view, because a second view would
resume mid-stream. The sync twin, `IEnumerable<ServerSentEvent>` over `Stream.Read`, is a real synchronous path on
.NET and ships with the blocking pipeline (§5.3).

**The streaming facade** (**SSE-23**–**SSE-32**) is `ServerSentEventStream : IAsyncEnumerable<ServerSentEvent>,
IAsyncDisposable, IDisposable`, owning exactly one `Response`. It reuses §7.1's lifetime rule verbatim so one
cleanup story covers both subsystems, with one difference forced by the use case: an event stream is long-lived, so
the facade *does* hold its response across yields and does *not* rely on the enumerator's `finally` alone. It
exposes `DisposeAsync` itself, and its enumerator's `finally` calls the same close-once helper, so an `await using`
on the stream releases it even when the caller enumerated by hand and abandoned the enumerator (**SSE-25**). The
closed state is an `Interlocked.Exchange` flag, which makes close idempotent (**SSE-28**) and safe from another
thread (**SSE-31**); a close observed between pulls ends iteration cleanly, and a close that disposes the body while
a `ReadAsync` is in flight surfaces as an `IOException` from that read, which the facade normalises (a transport
body may throw `ObjectDisposedException` instead) so **SSE-31**'s "surfaces as a read failure (an I/O error)"
holds across transports. Cancelling the enumeration token is the cooperative alternative and surfaces
`OperationCanceledException`. **SSE-26**/**SSE-27** are the single-use latch plus a closed check at
`GetAsyncEnumerator`. **SSE-29** and **SSE-36** use §7.1's suppressed-error helper, never a throwing `finally`.
**SSE-30**'s sync/async asymmetry — a release failure on the automatic terminal path is swallowed and reported out
of band, a failure on an explicit close propagates — is two call sites into one helper: the terminal path logs at
`Warning` through the stream's `ILogger` and swallows, `DisposeAsync` rethrows. **SSE-32**'s response-opening
factory binds the stream to the response and fails loudly on a bodyless response (a 204, or a response whose
content length is zero).

**The typed adapter** (**SSE-33**–**SSE-36**) is a method on the facade taking
`Func<string?, string, SseMapResult<T>>` — the raw `event` name and the data lines joined with a single `\n` — where
`SseMapResult<T>` is a `readonly struct` with three factories, `Value(T)`, `Skip` and `Done`, matched exhaustively.
Decoding is lazy per element because the adapter is itself an async iterator (**SSE-35**); a `Skip` pulls the next
raw event, a `Done` closes the stream and completes without yielding the sentinel (**SSE-34**).

**What the slice design got wrong, and is overturned.** The 2026-06-14 SSE slice sketched
`ServerSentEvent(string? Id, string EventType, string Data, TimeSpan? Retry)` with "`event:` sets the type (default
`"message"`)", "`id:` is sticky for the connection", joined `data`, a lean toward dropping comments, and a
reconnecting `ServerSentEventStream` that resumes with `Last-Event-ID` and honours `retry:`. Each of those is a
MUST-level conflict — **SSE-10**, **SSE-16**, **SSE-8**, **SSE-6**/**SSE-13** and, for the reconnecting client,
**SSE-38**: "the subsystem ... MUST NOT auto-reconnect, MUST NOT persist a last-event-id across events ..., and MUST
NOT set a reconnect request header." The slice was reproducing `SseParser`'s WHATWG semantics — the same gotcha one
layer up. The reconnect loop is documented as a caller recipe over the retry hint and each event's raw id; it does
not ship. **SSE-37**'s hard boundary — "no serialization dependency" — is enforced mechanically: an architecture test
asserts no type in `Dexpace.Sdk.Core.ServerSentEvents` or `Dexpace.Sdk.Core.Pagination`'s engine references
`Dexpace.Sdk.Core.Serialization` (§9.2); the paging *factory* takes an `ISerde` by design, so the rule binds the
engine and the SSE namespace, not the convenience entry point. **ASYNC-21**'s reactive adapter is optional: an
`IObservable<T>` bridge over System.Reactive would be an adapter package, not a seam, and would inherit the
one-poll-per-demand property from the pull-based reader (§11 item 21).

**As built (d45e64b):** not built.

### 7.3 Serialization: the reified type as witness

**SERDE-5**–**SERDE-8** defend against generic erasure. The porter's first job is to classify the host's erasure
precisely, and .NET is the opposite case from both the reference and Ruby: **the runtime reifies generics.**
`ISerde.DeserializeAsync<List<Dto>>` carries `List<Dto>` to run time in `typeof(T)`; there is no raw type to decay
to, and **SERDE-8**'s "unresolved type variable" state is unreachable, because an open generic type can never be a
type argument at run time — inside a generic method `T` is always the caller's concrete closed type. The reified
helper **SERDE-7** asks for is the ordinary C# signature `ResponseBody.ReadValueAsync<T>(ISerde)`. So the whole
type-token apparatus is free (P7), and the hidden precondition is the one this chapter exists to name: **reflection
is what trimming and NativeAOT remove.** System.Text.Json's reflection resolver is annotated for exactly that —
`DefaultJsonTypeInfoResolver`'s constructor carries `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`
(verified) — so the type token is free only when the metadata comes from a source-generated `JsonSerializerContext`
as a `JsonTypeInfo<T>`. The as-built `SystemTextJsonSerde` holds this line: it resolves `JsonTypeInfo<T>` through
the configured resolver on every call and, when the resolver has no metadata for `T`, throws the serde exception
naming `T` and telling the caller to add it to a context. That is **SERDE-6**'s "fail loudly ... rather than silently
decoding into the raw type" in its .NET form: the failure mode on this host is not a raw map but a missing
`JsonTypeInfo`, and it is surfaced at the first call, not deep inside a parse. Where the reference's argument ends
in a reflective carrier, .NET's ends in a build-time artifact the trim analyzer can see (§9.2).

**SERDE-1**/**SERDE-2** hold: `ISerde` bundles both directions with `DefaultMediaType`, and
`RequestBody.FromValue<T>` stamps it when no media type is given, so the SPI never defaults to a format-agnostic
constant. **SERDE-3** holds (verified: `JsonSerializer.Serialize` into a caller's stream does not dispose it).
**SERDE-4**'s encode-into-buffer profile — "MUST return the number of bytes written, MUST honor a start offset, and
MUST throw a range/overflow error" — is met as written by §3.4's core-derived `Serialize<T>(Span<byte>, T)`: the
caller passes `buffer.AsSpan(offset)`, the count is returned, and a payload that does not fit throws
`ArgumentOutOfRangeException`. The seam's own primitive is the growable `IBufferWriter<byte>` that `Utf8JsonWriter`,
pipelines and sockets converged on (P14); the fixed-buffer profile is derived from it once in core, so no adapter
implements or bends it.

**The failure model** (**SERDE-9**–**SERDE-13**) is mostly right and has one structural gap. Adapters catch
`JsonException` and `NotSupportedException` (plus `InvalidOperationException` on the synchronous writer path) and
rethrow with the original as `InnerException`; `IOException` is outside the filter and propagates unwrapped
(**SERDE-12**), and C# has no checked exceptions (**SERDE-11**). The gap is **SERDE-10**: "both of the common root".
`SerializationException` and `DeserializationException` both derive directly from `SdkException`, so the only
catchable
common root is the root of *every* SDK failure. An abstract `SerdeException : SdkException` between them restores
the requirement at no cost while the package is pre-1.0. **SERDE-13** hits a .NET-specific wall worth naming (P13):
nullable reference annotations are erased at run time — `typeof(Dto?) == typeof(Dto)` — so no serializer can know
whether the caller of `DeserializeAsync<Dto>` wanted `null` to be legal. System.Text.Json's
`RespectNullableAnnotations` (STJ 9+, so only on the `net10.0` target; the .NET 8 shared framework carries STJ 8)
enforces nullability on *members* but still returns `null` for a top-level `null` (verified: `{"Name":null}` into a
non-nullable member throws, bare `null` into `Dto` returns `null` with the option on). The root check is therefore the
SDK's job: `ISerde` keeps its honest `T?` return, and the convenience layer — `ReadValueAsync<T>`, the pager, the
typed SSE adapter — rejects a wire `null` for a non-nullable target with a `DeserializationException` naming `T`,
with an explicit nullable-returning overload for callers who want `null`. `RespectNullableAnnotations` is enabled in
the default options on `net10.0`.

**Tristate** (**SERDE-14**–**SERDE-20**, **SERDE-30**) is `readonly struct Tristate<T> where T : notnull`, and
choosing a
struct makes the hardest clause free. **SERDE-17** notes that "a missing key is short-circuited by the codec before
the decoder's null hook runs", so the field's *default* must be Absent; for a struct, `default(Tristate<T>)` **is**
Absent, so every property, field and positional-record parameter of type `Tristate<T>` that the JSON omits is Absent
without a default initializer anywhere (verified: decoding `{"B":null,"C":"v"}` into a three-`Tristate` record gave
`A=Absent B=Null C=Present(v)`). The illegal fourth state is excluded twice: `Tristate<T>.Present(value)` throws
`ArgumentNullException` on `null` at run time, and the `notnull` constraint makes `Tristate<string?>` a nullability
warning at compile time — a *warning* in a consumer's build, an error only in ours. **SERDE-14**'s covariance SHOULD
cannot be met: .NET variance exists only on interfaces and delegates, never on a struct. Absent and Null remain
assignable to any parameterization through the non-generic `Tristate.Absent`/`Tristate.Null` markers and implicit
conversions, which is the usage covariance was for. Recorded as §10 entry 21.
**SERDE-18**'s helpers are `FromNullable(T?)` (Present or Null, never Absent), `Match`, `GetValueOrDefault` and the
three `Is*` predicates; **SERDE-30**'s stable strings are an overridden `ToString` returning `Absent`, `Null` and
`Present(value)`, since a record struct's synthesized form would print its private fields.

The name overturns the serde slice, which called the type `Optional<T>`. .NET already has a two-state `Optional<T>`
in wide circulation (Roslyn's `Microsoft.CodeAnalysis.Optional<T>`: `HasValue` and `Value`, nothing more), so reusing
the name for three states would contradict vocabulary the ecosystem has converged on (P14); the specification's own
word is `Tristate`.

**Wiring Tristate into System.Text.Json is where AOT bites, and the chosen shape was verified.** **SERDE-15** needs
Absent to *omit the key*, which a converter cannot do — the property name is written before the converter runs —
and **SERDE-19** needs the wiring on by default. The adapter does both on a **private copy** of the caller's options:
it adds a converter factory for `Tristate<>` to the copy's `Converters`, and wraps the copy's resolver with
`JsonTypeInfoResolver.WithAddedModifier`, a modifier that sets `ShouldSerialize` to "not Absent" on every property
whose type is a `Tristate<>`. Verified end to end over a source-generated context with no attribute on the struct:
Absent/Null/Present serialized as *omitted*/`null`/`"x"`. **SERDE-20**'s top-level and array-element degradation
falls out: with no enclosing property to omit, the converter writes `null` for Absent. The factory is the hazard.
An open-generic `[JsonConverter(typeof(TristateConverter<>))]` is not supported by the source generator (verified:
`SYSLIB1220` at build and a `NotSupportedException` at run time), so a factory must close the generic at run time.
The conventional `MakeGenericType` + `Activator.CreateInstance` factory raises `IL3050` (verified), and under
NativeAOT a value-type instantiation it did not see statically cannot be created. The shape adopted instead
dispatches through an interface the closed `Tristate<T>` implements — the factory obtains an uninitialized instance
of the requested type and asks it for its converter, so `new TristateConverter<T>()` is emitted from statically
typed generic code — which leaves one `IL2067` the factory suppresses with a written justification (verified: both
shapes ran correctly under a NativeAOT publish for a value-type argument on 10.0.401, but only the interface shape
is correct by construction). §9.2's NativeAOT smoke test is what keeps that suppression honest (**NFR-9**). The
struct itself carries no System.Text.Json attribute: STJ is in the shared framework, so an attribute would cost no
dependency (P2), but it would couple a core model to one codec's wiring, which is the policy default P3 keeps out
of core.

**The strict-coercion policy** (**SERDE-21**, **SERDE-22**) is System.Text.Json's `General` default and is *not*
its `Web` default — the P13 gotcha of this section. Verified with `JsonSerializerDefaults.General`: `"5"`→`int`,
`1.5`→`int`, `true`→`double`, `5`→`string`, `"true"`→`bool` and `1`→`bool` all throw `JsonException`, while
`5`→`double` widens. `JsonSerializerDefaults.Web` sets `NumberHandling = AllowReadingFromString`, and verified,
`"5"` then decodes to `5` — the first coercion **SERDE-21** forbids. The serde slice made `Web` the default
("camelCase,
case-insensitive"); that is overturned to `Web` naming with `NumberHandling = Strict` forced back. **SERDE-23**
(unknown members ignored) and **SERDE-24** (ISO-8601 round-trip) are STJ defaults (verified). **SERDE-25**'s fresh
instance per factory call applies to a `SystemTextJsonSerde.CreateDefaultOptions()` factory the adapter does not
have yet.

**SERDE-26** is violated as built, and the verified symptom is concrete: "the SDK MUST NOT mutate the caller's
instance during normal construction; it MUST operate on a private copy". `SystemTextJsonSerde`'s constructor calls
`options.MakeReadOnly()` on the caller's `JsonSerializerOptions`, after which the caller's own
`options.WriteIndented = true` throws `InvalidOperationException` (verified). The fix is the copy constructor —
`new JsonSerializerOptions(callerOptions)` — followed by the Tristate wiring and `MakeReadOnly()` on the copy;
verified
to work over a source-generated context's options. The specification's fallback clause ("If the codec cannot be
copied ...") is never needed, because STJ options are always copyable. **SERDE-29** holds: read-only
`JsonSerializerOptions` are thread-safe and STJ's metadata cache is publication-safe.

**The response handlers.** **SERDE-27**: `ReadValueAsync<T>` streams through `DeserializeAsync` without
materializing the body and disposes the body stream on every path; a bodyless response currently surfaces as a
generic `DeserializationException` from an empty stream rather than one naming the missing body. **SERDE-28**'s
status-aware handler is the pipeline's error path (§5, §6): `HttpResponseException.GetErrorAsync<T>` reads the
bounded, buffered error body the pipeline captured before throwing. **HTTP-44**/**HTTP-45**'s lazy typed-response
wrapper is not built; its .NET shape is `Lazy<Task<T>>` in `ExecutionAndPublication` mode, which memoizes a `null`
success and a faulted task alike, where the obvious `_value ??= await ParseAsync()` re-runs the handler on a
legitimate
`null` and reads an already-consumed single-use body a second time — Ruby's `@value ||= load` gotcha in C#. The lock
is
held only while the factory *starts* the task; concurrent first callers await the same task rather than block, which
is **HTTP-45**'s non-pinning clause satisfied by the primitive.

**As built (d45e64b):** partial: `ISerde`, `SystemTextJsonSerde`, `FromValue`, `ReadValueAsync`, `GetErrorAsync`
built; diverges: caller options made read-only (**SERDE-26**), no common serde root (**SERDE-10**), root `null`
accepted (**SERDE-13**); missing: `Tristate<T>` and its wiring, default-options factory, typed-response wrapper.

### 7.4 Webhooks, outside the reference contract

The 2026-06-14 webhooks slice (Standard Webhooks HMAC-SHA256 verification behind an `IWebhookVerifier` seam) has no
counterpart in the specification — no requirement prefix covers it — so it is an extension, and this document
neither overturns nor adopts it beyond three .NET facts its design already respects: the body is verified as
`ReadOnlySpan<byte>` because a `string` round trip can change the signed bytes; comparison is
`CryptographicOperations.FixedTimeEquals`; and time comes from `TimeProvider`. It takes an `ISerde` for its
`Unwrap<T>` convenience, so it must stay outside the SSE and paging engines' serde boundary (**SSE-37**).

**As built (d45e64b):** not built.

---
