# Phase 7b — Server-Sent Events: Design

**Status:** Draft, for review. Written 2026-10-09 against `main` at `8dc8ee4` (phases 0 to 6c merged). Brainstormed
without a human in the loop: every judgement call the brainstorming skill would have put to the lead is taken here as a
numbered ruling (`P7b-n`) with its options and rationale, and the ones the lead may still reverse are marked **open for the
lead**. 7a (serde) and 7c (pagination) are being designed in parallel by other authors in this working tree; they are
independent of 7b by `SSE-37` and chapter 12 (roadmap, "How Phases Get Executed"), and the few files all three touch are listed
in [Shared files](#shared-files-with-7a-and-7c). The scope authority is the roadmap's Phase 7 card
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`, "Phase 7 — Serde, SSE and Pagination"). The format follows the
phase 6 designs (`docs/work/mvp/phase6/phase6b/2026-10-08-phase6b-redirect-design.md`).

**What this document is.** The sub-phase design for 7b. It holds:

- one disposition per requirement row (41 rows, `SSE-1`..`SSE-41`);
- the parser built over phase 3a's line reader, the event value, the streaming facade and the typed adapter;
- the public surface 7b adds (`PublicAPI.Unshipped.txt`), and the internal types behind it;
- the rulings, which double as the deviation-ledger IDs (roadmap constraint 7);
- the test strategy, the sibling ports, the vectors, and the AOT smoke contribution;
- the exit criteria, including closing issue [#10](https://github.com/dexpace/dotnet-sdk/issues/10).

**What this document is not.** It is not the plan and not the checklist. It does not re-argue design §7.2's rejection of
`System.Net.ServerSentEvents.SseParser` and `StreamReader` (§11 item 17), nor §10 entry 20 (`sse-line-length-cap`), nor §11
item 21 (the reactive adapter). It cites them and records a decision against each row. It edits no design chapter, roadmap
cell or `CLAUDE.md` line; the corrections it owes are listed in
[Corrections owed at close-out](#design-roadmap-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

| Predecessor or sibling | Kind | State at `8dc8ee4` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030`, `MA0051`, `CA2007`, `IsAotCompatible`, AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. Everything below is written under them. |
| The `SSE-37` architecture test (`tests/Dexpace.Sdk.Core.Tests/Architecture/Sse37ArchitectureTests.cs`) | **dependency** | Wired and inert: it skips while `Dexpace.Sdk.Core.ServerSentEvents` holds no type, and enforces with no edit from the first type that lands. Its stray guard already fails any core type outside that namespace carrying an SSE signal (an `Sse` prefix, `ServerSent`/`EventStream` in a name, a `text/event-stream` literal, a use of `CommonMediaTypes.TextEventStream`). |
| Phase 3a — `Utf8LineReader` with `LineTerminators.Whatwg` (`src/Dexpace.Sdk.Core/IO/`) | **dependency** (roadmap card "Entry") | Met. A byte-level reader: a CR ends the line immediately and sets a skip-one-LF flag, so no read waits on lookahead; malformed UTF-8 decodes to U+FFFD; a mandatory line cap (`DefaultMaxLineBytes = 1 MiB`) throws `InvalidDataException` naming the cap and leaves the reader failed; `leaveOpen`; never a zero-count read (IO-2). Tested in `IO/Utf8LineReaderTests.cs` (`Whatwg_mode_terminates_on_CR_immediately_…`, both forms). |
| Phase 4b — `Disposal.DisposeQuietly{,Async}`, `ExceptionTrail.AddSuppressed`, `ExceptionFacts.IsFatal` | **dependency** | Met. `Disposal` reports a non-primary failure as an `exception` event on `Activity.Current` plus a `dexpace.dispose.suppressed` warning (event id 130) on an optional logger, and attaches it to the primary's trail when one is in flight. This is design §7.1's "suppressed-error helper" for `SSE-29`/`SSE-36`; 7b does not wait on 7c. |
| Phase 4c — the real synchronous path (`HttpPipeline.Send`, `ResponseBody.OpenRead`) | **dependency** | Met. The blocking SSE views ride it. |
| Phase 5b — response-body previews skip `text/event-stream` and unknown-length bodies (`OBS-37`, P5b-12) | convenience | Met. The instrumentation wrapper never drains an event stream before `SendAsync` returns, so a live stream reaches the facade unbuffered. |
| Phase 6a — `AttemptTimeout` covers the time to response headers only | convenience | Met. A long-lived stream is not cut by the attempt timeout; `OverallTimeout` and the caller's token are the only bounds. |
| 7a, 7c | **independent** | 7b consumes nothing from either. The AOT smoke's SSE check uses the STJ adapter as it exists today, not 7a's `Tristate<T>`. |

## Governing documents, and the phase-start queries

Read for this design: product-spec chapter 13 in full; design §7.1 (the lifetime rule and the suppressed-close argument), §7.2
in full, §3.4 (the serde seam, for `SSE-37`'s boundary), §10 entries 12, 13 and 20, §11 items 17 and 21, §12's SSE row; the
roadmap Phase 7 card and constraints 1, 3, 4, 5, 7 and 10; `docs/sdk-documentation/{io,logging-and-redaction,retry}.md`; the
Node sibling's `packages/core/src/sse/` at `nodejs-sdk@c0ff3fd`.

```bash
scripts/knowledge --origin note --brief           # 12 notes; none touches SSE
scripts/knowledge --section conflicts --brief     # no SSE conflict; sse-streaming.md has an empty Conflicts section
scripts/knowledge --prefix-info SSE               # 41 IDs: 36 MUST, 3 SHOULD, 2 MAY; 41 of 41 substantive
scripts/knowledge --gaps SSE                      # 0 gaps: nothing has to be read out of appendix C alone
```

The corpus (`docs/knowledge/harvested/sse-streaming.md`) holds no fact the design and the chapter do not; the one stale
entry it warned of (`05-pipeline-architecture.md` re-harvest) does not touch SSE.

**Sibling sources.** The card names `ruby-sdk/gems/dexpace-core/test/dexpace/sse/`, `sse_test.rb` and
`test/support/sse_fixtures.rb`. None of them exists in the local checkout on any branch (`ruby-sdk@fa402cd` `mvp`,
`ruby-sdk@90075b1`, `main`): Ruby has the SSE design chapter and the harvested corpus only. The Node port is complete
(`parser`, `line-reader`, `event`, `stream`, `typed`, `errors`, `lifecycle`, two property suites; 92 tests) and is the port
source. The plan re-checks Ruby at its start and ports what has landed by then (P7b-22).

---

## Scope and the 41-row census

Legend (roadmap constraint 3): **build** = 7b implements and tests it; **met** = already true, 7b adds the pinning test the
checklist row needs; **N/A** = not applicable, with the section that retires it.

### 13.1 Line and field parsing

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| SSE-1 | MUST | build | `EventAccumulator` (internal): a blank line collapses the accumulated fields into one event and resets every accumulator. |
| SSE-2 | MUST | met (line layer) + build (event layer) | 3a's `LineTerminators.Whatwg` already ends a line on LF, CR or CRLF and treats CRLF as one terminator even when split across reads (`Utf8LineReaderTests.Whatwg_mode_terminates_on_CR_immediately_and_skips_the_following_LF`). 7b adds the event-level test: the same event under each terminator and mixed terminators yields identical data, and every chunk split of the bytes yields the same events. |
| SSE-3 | MUST | build | Split at the first `:`; no colon → whole line is the name, value `""`. |
| SSE-4 | MUST | build | Present-but-empty is `""`, absent is `null` (`Id`, `Event`, `Comment`) or no list entry (`Data`, P7b-7). |
| SSE-5 | MUST | build | Strip exactly one U+0020 after the colon, for fields and comments. |
| SSE-6 | MUST | build | A line starting `:` is a comment; latest wins; counts as a field seen. |
| SSE-7 | MUST | build | Only `id`, `event`, `data`, `retry` (ordinal, case-sensitive) are interpreted; any other name is dropped and is not a field seen. |
| SSE-8 | MUST | build | `data` values accumulate unjoined, in wire order. |
| SSE-9 | MUST | build | An `id` value containing U+0000 is ignored entirely (no state, not a field seen, does not overwrite). |
| SSE-10 | MUST | build | `event` stored raw, latest wins, `null` when absent; never defaulted to `message`. |
| SSE-11 | MUST | build | ASCII digits only, overflow-checked against the documented cap `int.MaxValue` ms; an ignored value sets nothing (P7b-6). |
| SSE-12 | MUST | build | One leading BOM consumed once, by the parser's one persistent flag (P7b-3). |

### 13.2 Dispatch and end of stream

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| SSE-13 | MUST | build | Dispatch when any of the five fields was set; a block with none set is skipped. |
| SSE-14 | MUST | build | At EOF a pending block dispatches; the line reader already returns an unterminated final line as content. |
| SSE-15 | MUST | build | `ReadNextAsync`/`ReadNext` return `null` at end and keep returning `null`. |

### 13.3 Reader statefulness and ownership

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| SSE-16 | MUST | build | The only cross-call state is `_bomChecked`; no last-event-id buffer exists (the field set is reviewed in the reader test by reflection over its instance fields). |
| SSE-17 | MUST | build | `ServerSentEventReader` is not disposable and builds its line reader with `leaveOpen: true` (P7b-8). |
| SSE-18 | MUST | build (contract) | Single-threaded by documented contract, the same as `Utf8LineReader` (IO-37). The one sanctioned cross-thread call is the facade's close (`SSE-31`). The checklist row cites the XML remark and the `SSE-31` tests; no behavioural test can prove an absence of thread-safety. |
| SSE-19 | MAY | build (sanctioned divergence) | The cap the clause permits: default 1 MiB per line, configurable, failure is `ServerSentEventLineTooLongException`. §10 entry 20 is the documentation the clause requires. Closes #10. |

### 13.4 The event value

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| SSE-20 | MUST | build | `Data`'s `init` copies into a private array; `with { Data = … }` copies again (P7b-7). |
| SSE-21 | SHOULD | build | Hand-written `Equals`/`GetHashCode` over the five fields, `Data` element-wise; a hand-written, lossless `PrintMembers`. |
| SSE-22 | SHOULD | build | `IsEmpty` true only when `Id`, `Event`, `Comment`, `Retry` are `null` and `Data` is empty. |

### 13.5 The streaming facade

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| SSE-23 | MUST | build | `ServerSentEventStream` owns one `Response`; release is latched by `Interlocked.Exchange`. |
| SSE-24 | MUST | build | End of stream ends the iterator and releases (quiet path). |
| SSE-25 | MUST | build | The enumerator's `finally` and the facade's own `Dispose`/`DisposeAsync` call the same latched release. |
| SSE-26 | MUST | build | One single-use latch shared by all four views; the second take throws `InvalidOperationException`. |
| SSE-27 | MUST | build | Taking a view after close throws `ObjectDisposedException`; a close between pulls ends the in-flight iterator cleanly. |
| SSE-28 | MUST | build | The latch makes every call after the first, explicit or automatic, a no-op. |
| SSE-29 | MUST | build | A non-fatal mid-stream failure releases first, with any release failure attached through `ExceptionTrail`, then rethrows the primary unchanged. |
| SSE-30 | MUST | build | Quiet path: `Disposal` reports out of band (activity event plus `dexpace.dispose.suppressed`) and swallows; explicit `Dispose`/`DisposeAsync` propagates (P7b-12). |
| SSE-31 | MUST | build | Close is thread-safe; it cancels the facade's internal token and releases; an in-flight read that fails because of it surfaces as `IOException` (P7b-13). |
| SSE-32 | MUST | build | `ServerSentEventStream.FromResponse` binds to the response and fails loudly (`ArgumentException`) on a bodyless one (P7b-9). |

### 13.6 The typed adapter

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| SSE-33 | MUST | build | The mapper receives `(Event, string.Join('\n', Data))`; no data → `""`; absent name → `null`. |
| SSE-34 | MUST | build | `Value` yields, `Skip` advances silently, `Done` releases (quiet path) and completes without a model for the sentinel. |
| SSE-35 | MUST | build | The adapter is an iterator; the mapper runs inside `MoveNext{,Async}` only. |
| SSE-36 | MUST | build | A mapper throw releases first (failure suppressed onto the mapper's exception), then propagates unchanged. |

### 13.7 Toolkit boundaries and backpressure

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| SSE-37 | MUST | build (gate turned on) | No SSE type references `Dexpace.Sdk.Core.Serialization`; the existing architecture test goes from skipped to enforcing. 7b adds a literal scan: no SSE type holds a sentinel literal (`[DONE]`) or names `message` as a default. The paging-engine half of the test is 7c's. |
| SSE-38 | MUST | build | No reconnect code. A new `Sse38ArchitectureTests` asserts that no core type holds a `Last-Event-ID` literal and that no SSE type references `IHttpClient`, `IAsyncHttpClient`, `HttpPipeline` or any `Send`/`SendAsync` member. The retry hint and raw id are surfaced; the reconnect loop is a documented caller recipe (`docs/sdk-documentation/sse.md`). |
| SSE-39 | MUST | build | Pull-based: the parser reads the source only inside a pull; the byte buffer (4 KiB) is a read-ahead of bytes, not events (design §7.2). Tested by counting source reads and raw reads against consumer pulls. |
| SSE-40 | SHOULD | build | `ServerSentEventReader.ReadAllAsync(Stream)`/`ReadAll(Stream)`: lazy, one reader per view, read errors at the failing pull, single-use latch on the returned sequence. "MUST NOT be invoked twice on the same source" is a caller obligation, documented. |
| SSE-41 | MAY | N/A (adapter-scoped) | No reactive adapter ships (§11 item 21, §12's SSE row; `Dexpace.Sdk.Reactive` is a `docs/first-release.md` candidate). Its SHOULD half, "apply its own runtime's fatal/non-fatal split and document source ownership", is honoured by the facade anyway: every catch site filters with `ExceptionFacts.IsFatal` (§10 entry 12) and ownership is documented on both types. `ASYNC-21` travels with any later adapter. |

**Count.** 39 build (including SSE-2's event layer and SSE-18's contract), SSE-2's line layer already met, 1 N/A. No row is
deferred.

---

## As built at `8dc8ee4`

No SSE code exists: `src/Dexpace.Sdk.Core` has no `ServerSentEvents` namespace, no type with an SSE signal (the stray guard
proves it), and `PublicAPI.Unshipped.txt` lists nothing SSE-related. What exists and is reused:

- `IO/Utf8LineReader` + `LineTerminators.Whatwg` (3a), unchanged by 7b (P7b-2).
- `Internal/Disposal`, `Errors/ExceptionTrail`, `Errors/ExceptionFacts` (4b).
- `Errors/StreamingException` (public, unsealed, three standard constructors) as the base of the one new exception.
- `Http/Common/CommonMediaTypes.TextEventStream` — 7b does not use it (P7b-18), so the stray guard stays satisfied.
- `Diagnostics/DexpaceLogEvents.DisposeSuppressedId` (130) — the `SSE-30` out-of-band report.

## Facts the design rests on

1. `Utf8LineReader` in `Whatwg` mode never holds a CR-terminated line for the next chunk: the CR ends the line in the scan,
   and the LF-skip decision is deferred to the *next* `ReadLine{,Async}` call (read in `TryAdvance`). Its cap check runs before
   each refill, so the accumulator never exceeds `maxLineBytes + 4096` bytes (one buffer). Read from source, `8dc8ee4`.
2. `Encoding.UTF8.GetString` does not strip a preamble, and a decoded string can begin with U+FEFF only if the bytes began
   `EF BB BF`: UTF-8 is prefix-free, and the replacement fallback emits U+FFFD, never U+FEFF. So "the first decoded line starts
   with U+FEFF" is equivalent to "the stream's first three bytes are `EF BB BF`" (P7b-3). Plan task 1 pins it with a test over
   `EF BB`, `EF BB 41`, `EF BB BF EF BB BF` and a split `EF | BB BF`.
3. `Response.Body` is never `null`: an absent body is `ResponseBody.FromReplayableBytes([])`, recognisable through the internal
   `IsEmptyReplayable`. A transport's empty body reports `ContentLength == 0` when known.
4. A compiler-generated async enumerator that a caller abandons without `DisposeAsync` never runs its `finally` (design §7.1,
   verified on 10.0.401). Hence the facade owns release itself and does not rely on the enumerator.
5. A `finally` that throws replaces the exception in flight (design §7.1, verified). Hence no throwing `finally` anywhere in
   the facade; release on a failure path goes through `Disposal` with the primary.
6. `Disposal.DisposeQuietly{,Async}` swallows non-fatal failures, attaching to `primary` when given and otherwise reporting to
   `Activity.Current` and the optional logger: exactly one of the two, never both (P4b-15).
7. A type that implements both `IEnumerable<T>` and `IAsyncEnumerable<T>` makes LINQ calls such as `Where`/`Select` ambiguous
   once `System.Linq` (which carries `System.Linq.AsyncEnumerable` in .NET 10's shared framework, design §7.1) is imported.
   *Not executed in this session* (no SDK on the brainstorm host's `PATH`); plan task 4 verifies it with a file-based app
   before relying on it, and P7b-15 stands either way on clarity grounds.

---

## Argued positions

### A. One parser, over the existing line reader (`SSE-1`–`SSE-16`; P7b-1, P7b-2, P7b-3)

**P7b-1. Core ships its own grammar; `SseParser` and `StreamReader` are not used.** Carried from design §7.2 and §11 item 17,
not re-argued: `SseParser` is strict WHATWG and fails `SSE-6`, `SSE-8`, `SSE-10`, `SSE-13`, `SSE-14`, `SSE-16` on the
specification's own fixtures; `StreamReader` switches encodings on a BOM and holds a CR-terminated line for the next chunk.
`TextReader.ReadLine*` is already banned (`BannedSymbols.txt`, IO-14). No strict-WHATWG mode ships.

**P7b-2. The line layer is 3a's `Utf8LineReader` in `Whatwg` mode, unchanged.** Options: (a) reuse it; (b) a second,
SSE-specific byte reader with a BOM check built in, as design §7.2 sketches; (c) extend `Utf8LineReader` with a
`consumeLeadingBom` flag. (b) duplicates a tested state machine for three bytes of difference, and (c) widens an IO type for
one caller and re-opens 3a's tests. Chosen: (a). The roadmap's "Entry: 3a, which provides the line reader" says the same. The
design's "`ArrayPool<byte>`-rented buffer" is not how 3a built it (a 4 KiB `byte[]` per reader) — a correction owed, not a 7b
change: a per-stream 4 KiB array on a long-lived stream is not a pooling win.

**P7b-3. The BOM is consumed at the string level by the parser, which owns the one persistent flag.** On the first line the
parser reads, a leading U+FEFF is removed once and `_bomChecked` is set; every later line is untouched, so a later BOM survives
as data, and a BOM-prefixed later line such as `﻿data: x` is an unknown field name and is dropped (`SSE-7`; the Node test
"a BOM on subsequent lines causes the line to be treated as an unknown field"). Fact 2 makes this byte-equivalent to the card's
"3-byte BOM check" and to `SSE-12`'s "non-consuming lookahead": a non-BOM prefix never decodes to U+FEFF, so nothing else is
ever removed. It also makes `SSE-16`'s "only the BOM-consumed flag persists" literally true of the parser type. The three BOM
bytes count against the first line's cap; the effect is three bytes on a 1 MiB cap and is documented.

**The grammar, in order, per line** (implemented in `EventAccumulator.Accept(string line)`, internal):

1. empty line → dispatch if any field was seen, then reset; otherwise skip (`SSE-1`, `SSE-13`);
2. first char `:` → comment = text after the colon with one leading space stripped; seen (`SSE-5`, `SSE-6`);
3. split at the first `:` (none → name = line, value = `""`); strip one leading U+0020 from the value (`SSE-3`, `SSE-5`);
4. `data` → append to the list; `event` → set; `id` → if the value contains U+0000, ignore, else set; `retry` → if valid,
   set, else ignore; anything else → ignore. Only a *set* marks the block as seen (`SSE-4`, `SSE-7`–`SSE-11`).

At EOF (`ReadLine` returns `null`) a seen block dispatches once and the next call returns `null`; an unseen one returns `null`
(`SSE-14`, `SSE-15`). Field names compare ordinally and case-sensitively (WHATWG; `Data:` is an unknown field).

### B. `retry` (`SSE-11`; P7b-6)

**P7b-6. The cap is `int.MaxValue` milliseconds; the value is a whole-millisecond `TimeSpan`; an ignored value is not a field
seen.** The cap and the type are design §7.2's (every .NET timer accepts `int.MaxValue` ms; Ruby uses the same cap, so the
shared vector agrees across ports; Node uses `Number.MAX_SAFE_INTEGER`, so the Node vector's over-cap case is re-derived
rather than copied). Digits are `char.IsAsciiDigit` only — `char.IsDigit` would admit Arabic-Indic digits — accumulated in a
`long` with an early exit past the cap, so a thousand-digit value is rejected in O(length) without overflow and leading zeros
are accepted (`0005` → 5 ms). Empty, signed, spaced, fractional and over-cap values are ignored, do not overwrite an earlier
valid `retry` in the block, and do not make the block dispatchable on their own (Node: "invalid retry value does not overwrite
a prior valid retry"). `retry: 0` is accepted as `TimeSpan.Zero`.

### C. The event value (`SSE-4`, `SSE-20`–`SSE-22`; P7b-7)

```csharp
public sealed record ServerSentEvent
{
    public string? Id { get; init; }                       // rejects U+0000 (ArgumentException)
    public string? Event { get; init; }
    public IReadOnlyList<string> Data { get; init; }       // never null; empty when no data line; init copies
    public string? Comment { get; init; }
    public TimeSpan? Retry { get; init; }                  // 0 ≤ value ≤ int.MaxValue ms, whole milliseconds
    public bool IsEmpty { get; }
    public bool Equals(ServerSentEvent? other);            // Data element-wise, ordinal
    public override int GetHashCode();
}
```

**P7b-7. `Data` is a non-null list, empty when no `data` line arrived; the other four are `null` when absent.** Design §7.2
says "every absent field `null` rather than empty"; read literally for `Data` that would make callers null-check the one
field they always read, and `SSE-4`'s distinction does not need it — a present-but-empty `data` is `[""]`, distinct from
`[]`. The `IsEmpty` rule follows: all four nullable fields `null` and `Data.Count == 0`. Construction routes are an object
initializer and `with`; there is no positional constructor (the record's five fields would otherwise be a positional
parameter list whose `Data` argument bypasses the copying `init`). The `init` accessors validate so that no event the parser
could never produce can be built: `Id` rejects U+0000 (`SSE-9`), `Retry` rejects negative, sub-millisecond and over-cap values
(`SSE-11`), `Data` rejects a `null` list or a `null` element (`ArgumentNullException`/`ArgumentException`). The Node port draws
the same line ("a NUL-bearing id cannot be built into an event").

`SSE-21`'s equality is hand-written because a record compares a collection member by reference (design §7.2, verified).
`PrintMembers` is hand-written too, so the string form is stable and lossless: strings are quoted with `"`, `\` and controls
escaped, `null` printed as `null`, `Data` as `["a", "b"]`, `Retry` as whole milliseconds (`5000ms`), so two unequal events
never print the same. The event is added to `ModelImmutabilityArchitectureTests`' type list. The SDK never logs an event, so
the string form opens no redaction path.

### D. The reader (`SSE-15`–`SSE-19`, `SSE-39`, `SSE-40`; P7b-4, P7b-5, P7b-8)

```csharp
public sealed class ServerSentEventReader
{
    public const int DefaultMaxLineBytes = 1_048_576;
    public ServerSentEventReader(Stream source, int maxLineBytes = DefaultMaxLineBytes);
    public ValueTask<ServerSentEvent?> ReadNextAsync(CancellationToken cancellationToken = default);
    public ServerSentEvent? ReadNext();
    public static IAsyncEnumerable<ServerSentEvent> ReadAllAsync(Stream source, int maxLineBytes = DefaultMaxLineBytes);
    public static IEnumerable<ServerSentEvent> ReadAll(Stream source, int maxLineBytes = DefaultMaxLineBytes);
}
```

**P7b-8. The reader is not disposable, and the names are `ReadNext{,Async}` and `ReadAll{,Async}`.** `SSE-17` forbids it to
own its source, so it has nothing to dispose: the `Utf8LineReader` it holds is built `leaveOpen: true`, whose `Dispose` would be
a no-op. `CA1001` (a type owning a disposable field should be disposable) is waived with a scoped `SuppressMessage` citing
`SSE-17` (roadmap constraint 1's waiver form). `null` is the end sentinel (`SSE-15`), the .NET idiom for an exhausted reader
(`Utf8LineReader.ReadLine`, `TextReader.ReadLine`). The sync form takes no token because `Stream.Read` has none, the same as
`Utf8LineReader`. The static views are named after `ChannelReader.ReadAllAsync`; design §7.2's `ReadAsync(Stream)` would sit
beside an instance `ReadNextAsync` and read as a single read (correction owed). Each view builds one reader at its first
`MoveNext` (so BOM consumption is per stream, `SSE-40`), and its `GetEnumerator`/`GetAsyncEnumerator` latches through
`Interlocked`, throwing `InvalidOperationException` the second time, because a second walk would resume mid-stream. The
`[EnumeratorCancellation]` token reaches every `ReadAsync`. A read exception surfaces at the failing pull, after the events
already parsed (`SSE-40`).

**P7b-4. A line over the cap fails with `ServerSentEventLineTooLongException : StreamingException`, and the reader stays
failed.** Default 1 MiB (§10 entry 20, matching `BODY-30`), configurable per reader and per stream, validated positive
(`ArgumentOutOfRangeException`). The reader catches the line reader's `InvalidDataException` and throws the SSE leaf with it as
the inner exception and a `MaxLineBytes` property; the message names the cap, never content. Every later read throws the same
type again, because the source is now mid-line (3a's sticky failure). Options weighed: (a) let `InvalidDataException`
escape — it is a `SystemException`, not an SDK error, and the card and issue #10 ask for "a streaming error"; (b) throw the
`StreamingException` base — no catchable distinction for the one failure a caller may want to handle; (c) a leaf. Chosen: (c).
It lives in `Dexpace.Sdk.Core.ServerSentEvents`, not `Errors` (P7b-20). Node keeps the cap opt-in (no default); this port
defaults it on, which §10 entry 20 sanctions and the sibling difference is noted in the vector file.

**P7b-5. No aggregate per-event cap in 7b — open for the lead.** The line cap bounds one line, not a block: a hostile server
can send unlimited 1 MiB `data` lines with no blank line, and the accumulator grows until the consumer's token or
`OverallTimeout` stops it. Options: (a) a second cap on a block's accumulated bytes, default e.g. 16 MiB; (b) leave it. (a) is
a new divergence `SSE-19` licenses only by reading "values" as the joined data, and it would need a §10 dated correction;
the issue being closed (#10) is about lines. Chosen: (b), documented as residual R1 in the reader's remarks and in `sse.md`
("bound a stream from an untrusted server with a token or `OverallTimeout`"). Reversible later without breaking anything:
an added optional parameter.

### E. The facade (`SSE-23`–`SSE-32`; P7b-9 to P7b-15, P7b-19)

```csharp
public sealed class ServerSentEventStream : IAsyncEnumerable<ServerSentEvent>, IAsyncDisposable, IDisposable
{
    public static ServerSentEventStream FromResponse(
        Response response, int maxLineBytes = ServerSentEventReader.DefaultMaxLineBytes, ILogger? logger = null);
    public IAsyncEnumerator<ServerSentEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default);
    public IEnumerable<ServerSentEvent> AsEnumerable();
    public IAsyncEnumerable<T> MapAsync<T>(Func<string?, string, SseMapResult<T>> mapper);
    public IEnumerable<T> Map<T>(Func<string?, string, SseMapResult<T>> mapper);
    public ValueTask DisposeAsync();
    public void Dispose();
}
```

**P7b-9. `FromResponse` takes ownership on call, success or failure, and rejects a bodyless response.** Bodyless means:
status 204, 205 or 304; request method `HEAD`; `Body.ContentLength == 0`; or the internal empty replayable body (fact 3). It
then disposes the response and throws `ArgumentException` (`paramName: "response"`) naming the reason, never the URL. Options:
(a) caller keeps ownership on throw; (b) ownership transfers on call. The common shape is
`await using var events = ServerSentEventStream.FromResponse(await pipeline.SendAsync(request, ct));`, where (a) leaks the
response on the throw path. Chosen: (b), documented on the method. No status or `Content-Type` check: the default pipeline's
`ErrorMappingPolicy` already throws on 4xx/5xx, servers mislabel event streams, and `SSE-37` keeps conventions out of core.
`maxLineBytes` is validated before anything else and an invalid value also disposes the response (one rule: the call owns it).

**P7b-10. The body stream opens lazily, at the first pull, through the view's own path.** The async views call
`Body.OpenReadAsync(token)`, the blocking views `Body.OpenRead(token)`, so each path is genuinely sync or async (§5.3) and
construction does no I/O (`SSE-39`). A stream that is never enumerated only releases its response.

**P7b-11. Release disposes the opened stream, then the response; both always run.** Reverse acquisition order, as Node
(`closingBoth`): a wrapper stream returned by `OpenRead*` (a logging or prefixed stream) may not be released by the response
alone. If both fail, the stream's failure is primary and the response's is attached to it through `ExceptionTrail`. "Exactly
one close" (`SSE-23`) is counted on the response, which is what the caller handed over; disposing an already-closed stream is
a no-op by the `Stream` contract.

**P7b-12. Which release paths swallow and which propagate.**

| Path | Release failure | Rationale |
|---|---|---|
| natural end of stream (`SSE-24`) | reported out of band, swallowed | `SSE-30`'s automatic clean-terminal path |
| typed `Done` (`SSE-34`) | reported, swallowed | `SSE-30` names the done-sentinel explicitly |
| enumerator disposed early (`break`, an exception in the consumer's loop body, `FirstAsync`) (`SSE-25`) | reported, swallowed | the enumerator cannot know whether the consumer has an exception in flight, and a throw from `DisposeAsync` there would replace it (fact 5) |
| mid-stream read failure, cancellation, cap failure (`SSE-29`) | attached to the primary, primary rethrown | `SSE-29` |
| mapper throws (`SSE-36`) | attached to the mapper's exception, rethrown | `SSE-36` |
| explicit `Dispose()`/`DisposeAsync()` (`SSE-30`) | **propagates** | the caller asked |
| any call after the first release (`SSE-28`) | nothing to do | the latch |

"Reported" is `Disposal`'s single out-of-band channel: an `exception` event on `Activity.Current` tagged
`dexpace.dispose.suppressed` and, when `FromResponse` was given a logger, the `dexpace.dispose.suppressed` warning (id 130).
**P7b-19.** The logger is an optional `FromResponse` parameter (precedent: `InstrumentationPolicy(ILogger?)`,
`DexpacePipeline.CreateDefault(…, ILogger?)`), because a `Response` carries no logger. Without one the activity event is the
only report, the same residual `Disposal` documents for its other callers (P3b-3). Rethrow on the failure paths is `throw;`
inside the catch, or `ExceptionDispatchInfo.Throw` where the release is awaited outside it, so the stack survives and the
exception is the same object (`RECOV-10`'s "unchanged", design §10 entry 13).

**P7b-13. Close from another thread: an atomic flag, an internal token, and one normalised error.** `_closed` is an
`Interlocked.Exchange` flag; the release latch is a second one (`_released`), so "closed" can be observed before the release
completes. `Dispose`/`DisposeAsync` set `_closed`, cancel a facade-owned `CancellationTokenSource`, then release. Each async
view reads under a token linked from the caller's enumeration token and that source, so a cooperative stream unblocks at once
and a non-cooperative one unblocks when its disposal tears it down. The iterator checks `_closed` before every pull: a close
observed between pulls ends iteration cleanly (`SSE-27`). An exception from an in-flight read while `_closed` is set — an
`ObjectDisposedException`, an `OperationCanceledException` from the internal token, an `IOException`, or whatever a transport
raises — is surfaced as `IOException("The event stream was closed while a read was in flight.", inner)` (`SSE-31`); an
`OperationCanceledException` from the *caller's* token is never rewritten (`XCUT-1`). The release on that path is the latched
no-op, so the count stays one. The blocking views have no token; they rely on the disposal tearing the stream down, and the
same normalisation applies. `SSE-18`'s single-threaded contract is otherwise unchanged: two threads pulling one stream is
undefined.
*Dated correction, 2026-10-09 (review finding on SSE-30, P7b-12):* "the release on that path is the latched no-op" has to be
made true by the closer, not assumed. Cancelling the internal token unblocks the parked read, and for a read that completes
synchronously the iterator's own quiet release ran inline inside `Cancel()`, took the latch first and swallowed a release
failure the explicit close was owed (and, with an asynchronous release, let `Dispose`/`DisposeAsync` return before the
response was released). So the first closer takes `_released` in `BeginClose`, **before** it cancels; the order is set
`_closed`, take the latch, cancel, then release through the propagating path, and `ReleaseKind.Propagate` is gone because an
explicit close no longer goes through the iterator's latch-taking `Release`. `RegisterOpened` already covered a stream that
opens after the latch is taken. Pinned by `A_close_owns_the_release_so_its_failure_propagates_...` (async) and
`A_blocking_close_owns_the_release_so_its_failure_propagates_...`.

**P7b-14. One latch for all four views; misuse is `InvalidOperationException`, use after close is
`ObjectDisposedException`.** `GetAsyncEnumerator`, `AsEnumerable`, `MapAsync` and `Map` return lazily, and the latch is taken
when the returned sequence's enumerator is requested, so `var typed = events.MapAsync(f);` costs nothing until enumerated.
The check is synchronous (the async iterator body is a private method behind a non-iterator `GetAsyncEnumerator`), so the
throw happens at the call, not at the first `MoveNextAsync` (`SSE-26`, `SSE-27`). `ObjectDisposedException` derives from
`InvalidOperationException`, so `catch (InvalidOperationException)` catches both; the BCL's own idiom, matching the page view's
latch in design §7.1.

**P7b-15. The facade is an `IAsyncEnumerable` and not an `IEnumerable`; the blocking view is `AsEnumerable()`.** Options: (a)
implement both interfaces; (b) a second blocking type, as pagination plans `Pageable<T>` beside `AsyncPageable<T>`; (c) one type
with a method for the blocking view. (a) makes LINQ ambiguous on the facade (fact 7) and makes `foreach` versus `await foreach`
silently pick the path. (b) doubles the facade (a second owner type, a second factory) for one view. Chosen: (c). The name
avoids `System.Threading.Tasks.TaskAsyncEnumerableExtensions.ToBlockingEnumerable`, which is sync-over-async; `AsEnumerable()`
is a genuinely synchronous walk over `Stream.Read`. The XML docs say so, and say not to call `ToBlockingEnumerable` on the
facade.

### F. The typed adapter (`SSE-33`–`SSE-36`; P7b-16, P7b-17)

```csharp
public enum SseMapResultKind { Value = 1, Skip = 2, Done = 3 }

public readonly record struct SseMapResult<T>
{
    public SseMapResultKind Kind { get; }
    public T Value { get; }                                    // throws InvalidOperationException unless Kind == Value
    public static implicit operator SseMapResult<T>(SseMapResult signal);
}

public readonly record struct SseMapResult                 // the type-free signals and the factory
{
    public static SseMapResult Skip { get; }
    public static SseMapResult Done { get; }
    public static SseMapResult<T> Value<T>(T value);
    public SseMapResultKind Kind { get; }
}
```

**P7b-16. A generic result plus a non-generic signal type, and `default` is invalid.** Static members on a generic type trip
`CA1000`, and a caller would have to spell `SseMapResult<Chunk>.Skip`. A non-generic `SseMapResult` carries `Skip`, `Done` and
`Value<T>(T)`, and converts implicitly to any `SseMapResult<T>` — the shape design §10 entry 21 gives `Tristate<T>`'s markers. A
mapper reads: `(name, data) => data == "[DONE]" ? SseMapResult.Done : SseMapResult.Value(Parse(data))` (the sentinel lives in
the caller's code, `SSE-37`). `Kind` starts at 1, so `default(SseMapResult<T>)` has `Kind == 0`: the adapter treats it as a
mapper failure, an `InvalidOperationException` naming the mistake, released first like any mapper throw (`SSE-36`). The
options were making `default` mean `Skip`, which would silently drop every event for a mapper that forgot to return, or `Done`,
which would silently end the stream; failing loudly costs nothing. Record structs give value equality free, which keeps
`CA1815` quiet and lets a caller unit-test a mapper with `Assert.Equal`.

**P7b-17. The mapper is a synchronous `Func`.** The data is already a string in memory, so decoding is CPU work; an async
mapper would invite I/O inside the pull. The same mapper type serves `MapAsync` and `Map`. Laziness (`SSE-35`) is the
iterator's: one mapper call per raw event pulled, and the adapter pulls raw events only until one yields (`Skip` loops,
`SSE-39`'s typed carve-out). After `Done` the iterator releases and completes; no later raw event is parsed or mapped. A fatal
exception from the mapper (`ExceptionFacts.IsFatal`) propagates without the release attempt, which the enumerator's own
`finally` then performs on disposal (§10 entry 12).

### G. Boundaries (`SSE-37`, `SSE-38`; P7b-18, P7b-20)

**P7b-18. Core adds no SSE request helper.** No `Accept: text/event-stream` setter, no `Last-Event-ID`, no reconnect loop, no
use of `CommonMediaTypes.TextEventStream`. The caller sets `Accept` on the request it sends; `sse.md` documents the reconnect
recipe over `Retry` and each event's `Id` (`SSE-38`). A request helper would be the first step of the reconnecting client the
design overturned.

**P7b-20. Every SSE type, the exception included, lives in `Dexpace.Sdk.Core.ServerSentEvents`.** The `SSE-37` stray guard
fails any core type outside that namespace whose name contains `ServerSent` or starts `Sse`, so
`ServerSentEventLineTooLongException` cannot sit in `Errors` without retargeting the gate. Options: rename it generically
(`LineTooLongException` in `Errors`) and use it from IO too — but IO-14's callers throw `InvalidDataException` today and 7b would
be changing 3a's contract for no SSE gain; or keep it in the SSE namespace. Chosen: the SSE namespace. It still derives from
`Errors.StreamingException`, so `catch (StreamingException)` and `catch (SdkException)` see it. Recorded so the next phase does
not "tidy" it into `Errors`.

---

## The public surface (`PublicAPI.Unshipped.txt`)

All additions; nothing changes or is removed, so 7b has **no breaking change**. The exact lines are produced by the
`RS0016` code fix and reviewed as a diff; the shape is:

```text
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.ServerSentEvent() -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Id.get -> string?
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Id.init -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Event.get -> string?
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Event.init -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Data.get -> System.Collections.Generic.IReadOnlyList<string!>!
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Data.init -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Comment.get -> string?
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Comment.init -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Retry.get -> System.TimeSpan?
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Retry.init -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.IsEmpty.get -> bool
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.Equals(Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent? other) -> bool
override Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent.GetHashCode() -> int
  (+ the record's synthesized members: <Clone>$, EqualityContract, ToString, op_Equality, op_Inequality, Equals(object?))
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventReader
const Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventReader.DefaultMaxLineBytes = 1048576 -> int
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventReader.ServerSentEventReader(System.IO.Stream! source, int maxLineBytes = 1048576) -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventReader.ReadNextAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Threading.Tasks.ValueTask<Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent?>
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventReader.ReadNext() -> Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent?
static Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventReader.ReadAllAsync(System.IO.Stream! source, int maxLineBytes = 1048576) -> System.Collections.Generic.IAsyncEnumerable<Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent!>!
static Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventReader.ReadAll(System.IO.Stream! source, int maxLineBytes = 1048576) -> System.Collections.Generic.IEnumerable<Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent!>!
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream
static Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream.FromResponse(Dexpace.Sdk.Core.Http.Response.Response! response, int maxLineBytes = 1048576, Microsoft.Extensions.Logging.ILogger? logger = null) -> Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream!
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream.GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IAsyncEnumerator<Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent!>!
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream.AsEnumerable() -> System.Collections.Generic.IEnumerable<Dexpace.Sdk.Core.ServerSentEvents.ServerSentEvent!>!
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream.MapAsync<T>(System.Func<string?, string!, Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>>! mapper) -> System.Collections.Generic.IAsyncEnumerable<T>!
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream.Map<T>(System.Func<string?, string!, Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>>! mapper) -> System.Collections.Generic.IEnumerable<T>!
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream.DisposeAsync() -> System.Threading.Tasks.ValueTask
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventStream.Dispose() -> void
Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind
Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind.Value = 1 -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind
Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind.Skip = 2 -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind
Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind.Done = 3 -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind
Dexpace.Sdk.Core.ServerSentEvents.SseMapResult
Dexpace.Sdk.Core.ServerSentEvents.SseMapResult.Kind.get -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind
static Dexpace.Sdk.Core.ServerSentEvents.SseMapResult.Skip.get -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResult
static Dexpace.Sdk.Core.ServerSentEvents.SseMapResult.Done.get -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResult
static Dexpace.Sdk.Core.ServerSentEvents.SseMapResult.Value<T>(T value) -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>
Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>
Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>.Kind.get -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResultKind
Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>.Value.get -> T
static Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>.implicit operator Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>(Dexpace.Sdk.Core.ServerSentEvents.SseMapResult signal) -> Dexpace.Sdk.Core.ServerSentEvents.SseMapResult<T>
  (+ both record structs' synthesized equality members and their public parameterless constructors)
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventLineTooLongException
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventLineTooLongException.ServerSentEventLineTooLongException() -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventLineTooLongException.ServerSentEventLineTooLongException(string! message) -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventLineTooLongException.ServerSentEventLineTooLongException(string! message, System.Exception! innerException) -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventLineTooLongException.ServerSentEventLineTooLongException(int maxLineBytes, System.Exception? innerException = null) -> void
Dexpace.Sdk.Core.ServerSentEvents.ServerSentEventLineTooLongException.MaxLineBytes.get -> int
```

`SseMapResult<T>.implicit operator` is declared on the generic type (a user-defined conversion must be declared in the source
or the target type). `CA2225` (provide a named alternative for an operator) is met by `SseMapResult.Skip`/`Done` themselves
being the named route; if the analyzer still fires, a `ToSseMapResult<T>()` alternative is added rather than a waiver. The three
standard exception constructors satisfy `CA1032`; the `maxLineBytes` constructor builds the cap-naming message. Every member
carries `///` docs (CS1591).

### Internal types

| Type | Role |
|---|---|
| `EventAccumulator` (sealed class) | The per-block state and the grammar of position A; returns a `ServerSentEvent?` per line. Unit-tested directly through `InternalsVisibleTo`. |
| `RetryField` (static) | `TryParse(string, out TimeSpan)` for P7b-6. |
| `SingleUseSequence<T>` / `SingleUseAsyncSequence<T>` | The latched wrappers behind `ReadAll{,Async}` and the facade's four views (P7b-8, P7b-14). |
| `ServerSentEventStream` partials | `ServerSentEventStream.cs` (factory, latch, close, release), `.Async.cs` (async iterators), `.Blocking.cs` (sync iterators), `.Typed.cs` (`MapAsync`/`Map`), each method under `MA0051`'s 70 lines. |

Namespace: `Dexpace.Sdk.Core.ServerSentEvents`, folder `src/Dexpace.Sdk.Core/ServerSentEvents/`. No `GlobalUsings.cs`
change; `Microsoft.Extensions.Logging` is imported where `ILogger` is used.

---

## Tests, vectors and ports

All under `tests/Dexpace.Sdk.Core.Tests/ServerSentEvents/` unless stated, `[Trait("Category", "Unit")]` unless stated.
`Dexpace.Sdk.Core.Tests` uses in-memory streams and fakes only (`SEAM-2`); responses are built with the public `Response`
constructor over `ResponseBody.FromStream`. Every test names its ID(s) in the method name or a comment.

| Class | Covers | Notes |
|---|---|---|
| `ServerSentEventParserTests` | SSE-1, SSE-3–SSE-11, SSE-13, SSE-14, SSE-16 | Every conformance fixture in chapter 13, verbatim, plus the Node `parser.test.ts` cases. |
| `ServerSentEventLineTests` | SSE-2, SSE-12, SSE-14 | Each terminator and mixed; CR at a chunk end then LF at the next chunk start is one terminator; BOM fact-2 cases. |
| `ServerSentEventReaderTests` | SSE-15, SSE-16, SSE-17, SSE-18, SSE-39, SSE-40 | End sentinel stable; source never disposed (`DisposeCountingStream`); `ChunkedReadStream` counts source reads per pull; a view taken twice throws; a mid-stream read failure surfaces at the failing element after the earlier events; instance-field review for `SSE-16`. |
| `ServerSentEventChunkSplitTests` | SSE-2, SSE-12, SSE-14 (property) | Replaces Node's `line-reader.property.test.ts`/`parser.property.test.ts`: for every fixture, **every** split of the byte sequence into two chunks and, for fixtures up to 24 bytes, every split into three yields identical events — exhaustive rather than sampled. A seeded round-trip (`new Random(seed)`, seeds listed in the test) serialises generated events with arbitrary terminators and parses them back (P7b-21). |
| `ServerSentEventTests` | SSE-4, SSE-9, SSE-11, SSE-20, SSE-21, SSE-22 | Defensive copy at construction and through `with`; equality and hash over all five fields, order-sensitive, present-but-empty ≠ absent; lossless string form; `IsEmpty`; `init` validation. |
| `ServerSentEventStreamLifecycleTests` | SSE-23–SSE-28, SSE-30, SSE-32 | A close-counting `ResponseBody` subclass that can be told to throw on dispose. Every row of P7b-12's table; ownership on a throwing `FromResponse`; every bodyless case. Async and blocking views both. |
| `ServerSentEventStreamFailureTests` | SSE-29, SSE-31 | Failure after N events → release first, suppressed close error readable through `ExceptionTrail.GetSuppressed`; a gated stream parked inside `ReadAsync`, closed from another thread → `IOException`, one release; close between pulls → clean end; caller-token cancellation → `OperationCanceledException`, not rewritten. |
| `ServerSentEventStreamTypedTests` | SSE-33–SSE-36, SSE-39 (typed carve-out) | Join with `\n`; `""` for no data; `Skip`/`Done`/`Value`; post-sentinel events never mapped; lazy per element (mapper call count); a throwing mapper and a `default` result both release first. |
| `Architecture/Sse37ArchitectureTests` (existing) | SSE-37 | Goes from skipped to enforcing with no edit to its serde check; 7b adds the sentinel-literal scan. |
| `Architecture/Sse38ArchitectureTests` (new) | SSE-38 | No `Last-Event-ID` literal in core; no SSE type references the transports, `HttpPipeline` or a `Send`/`SendAsync` member. |
| `Architecture/ModelImmutabilityArchitectureTests` (existing) | SSE-20 | `ServerSentEvent` added to its list. |
| `Security/ServerSentEventLineCapTests` (`[Trait("Category", "Security")]`) | SSE-19, issue #10 | The card's regression: an unterminated 8 MiB line from a counting source fails with `ServerSentEventLineTooLongException` once at most `maxLineBytes + 2 × 4096` bytes have been read, the message holds no content byte, and the next pull throws again; a line of exactly the cap passes; a custom cap. Tagged `Security` so it is permanent (roadmap constraint 5; phase 6a's `RetryPacingOverflowTests` precedent). |
| `tests/Dexpace.Sdk.Http.SystemNet.Tests/ServerSentEventWireTests.cs` (`Integration`) | SSE-2, SSE-31, SSE-32, SSE-39 end to end | Over the loopback server through `SystemNetHttpClient` and `DexpacePipeline.CreateDefault`: a CR-terminated event is delivered before the server sends more bytes (the defect design §7.2 found in `StreamReader`); a close from another thread while the transport read is parked surfaces as `IOException` with one response release; a 204 fails loudly. Needs a gated response in the loopback fixture (P7b-23). |

**P7b-21. No FsCheck.** The card says "with the property tests moved to FsCheck". Options: (a) add FsCheck (a test-only
`PackageVersion`, lock files in every test project that references it — and 7a and 7c are editing `Directory.Packages.props`
in parallel); (b) exhaustive split enumeration plus seeded generators in plain xUnit. The two Node property suites check one
property each (split-invariance and serialise/parse round trip); for split-invariance exhaustive enumeration over the fixture
corpus is strictly stronger than sampling, and a seeded round trip with listed seeds reproduces exactly on failure. Chosen:
(b). **Open for the lead**: if FsCheck is adopted repository-wide later, the round trip moves to it unchanged.

**Vectors (roadmap constraint 10).** `tests/vectors/sse/grammar.json`: `source` names `nodejs-sdk@c0ff3fd
packages/core/src/sse/parser.test.ts` plus `docs/product-spec/13-server-sent-events-and-streaming.md` (the chapter's
conformance fixtures). Each case is `{ "name", "ids", "input" (with `\r`, `\n`, `\u0000`, `﻿` escapes), "events": [{ id,
event, data, comment, retryMs }] }`. A `note` records the sibling differences: the over-cap `retry` cases are re-derived for the
`int.MaxValue` cap, and the line-cap cases are .NET's (Node has no default cap). `ServerSentEventParserTests` and
`ServerSentEventChunkSplitTests` both load it through `VectorFile`, so the split property runs over the same corpus the
example tests assert. Node-specific host facts (`ReadableStream` reader locks, `AbortSignal` listener removal,
`Symbol.asyncDispose` availability) are not ported; their .NET counterparts are P7b-11 and P7b-13.

**P7b-22. Ruby ports.** The card's Ruby test files do not exist locally (see Governing documents). The plan's first task
re-checks `ruby-sdk` and ports any fixture that has landed into the vector file with its sha; otherwise the checklist rows
cite Node and the chapter, and the gap is stated in the checklist preamble.

**P7b-23. A gated loopback response.** `LoopbackResponse` today writes fixed bytes. 7b adds a test-only
`LoopbackResponse.Streamed(headers, IAsyncEnumerable<byte[]> chunks)` (chunked transfer encoding, written as the test releases
each chunk), with its own `LoopbackServerTests` case. Phase 8a promotes the fixture into the conformance kit and inherits it.

**AOT smoke contribution (exit criterion).** `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` gains `CheckServerSentEventsAsync`,
`[Trait]`-free as the consumer is (category `AotSmoke` is its project): a `DelegateHttpClient` behind
`DexpacePipeline.CreateDefault` answers with `text/event-stream` bytes `: hi\n\ndata: {"name":"a"}\r\rdata:
{"name":"b"}\n\ndata: [DONE]\n\ndata: {"name":"never"}\n\n`; `FromResponse` + `MapAsync` with a source-generated STJ
`JsonTypeInfo` (the smoke's existing context, extended with one record) yields `a`, `b`, skips the comment, stops at `[DONE]`,
never maps `never`, and the response body's dispose count is one. The blocking path repeats it with `Map` over
`HttpPipeline.Send`. This proves the async iterators, the latch, the record struct conversion and the typed adapter survive
trimming and NativeAOT, and makes the card's "JSON, Tristate, paged and SSE round trip" whole once 7a and 7c add theirs.

---

## Shared files with 7a and 7c

| File | 7b's edit | Collision handling |
|---|---|---|
| `src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt` | additions only, all under `Dexpace.Sdk.Core.ServerSentEvents` | line-level, alphabetical; a rebase merges by taking both |
| `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (+ `SmokeModels.cs`) | one new check method and one call in `RunAllAsync`; one record in the smoke JSON context | each sub-phase appends its own method |
| `tests/Dexpace.Sdk.Core.Tests/Architecture/Sse37ArchitectureTests.cs` | the sentinel-literal test only | 7c owns the paging-engine half |
| `CHANGELOG.md`, `CLAUDE.md`, the roadmap's status notes | own entries | append-only |
| `Directory.Packages.props`, lock files | **none** (P7b-21) | — |

## PR segmentation

One pull request, closing #10. 41 rows over one new namespace with no breaking change is reviewable in one sitting, and the
facade's lifecycle tests need the parser. The plan orders it as: (1) the event value; (2) `EventAccumulator`, `RetryField` and
the reader with the vectors, the split property and the #10 `Security` test; (3) the facade and its lifecycle/failure tests;
(4) the typed adapter; (5) the architecture tests, the loopback wire tests and the AOT smoke; (6) docs, checklist, changelog.

## Hand-offs to later phases

- **Phase 8a** — the conformance kit inherits `LoopbackResponse.Streamed` and may lift `ServerSentEventWireTests` into a
  per-transport conformance script (streamed body delivery is `TRANSPORT` territory, not `SSE`).
- **First release / an adapter package** — `Dexpace.Sdk.Reactive` (`IObservable<T>`), which would carry `SSE-41` and
  `ASYNC-21`; an optional `SseItem<T>` bridge for strict-WHATWG callers (§11 item 17). Neither is scheduled by 7b.
- **The lead** — P7b-5 (aggregate per-event cap) and P7b-21 (FsCheck).

## Design, roadmap and `CLAUDE.md` corrections owed at close-out

Proposals, applied as dated corrections when the checklist lands, not by this document:

1. Design §7.2: the reader is 3a's `Utf8LineReader` with a 4 KiB array, not an `ArrayPool`-rented buffer (P7b-2); the BOM check
   is string-level in the parser, byte-equivalent (P7b-3); the static view is `ReadAllAsync(Stream)`, not `ReadAsync(Stream)`
   (P7b-8); `Data` is an empty list when absent and `IsEmpty` reads it so (P7b-7); the facade's logger comes from
   `FromResponse` (P7b-19); the "net8.0 floor" remarks predate decision D1.
2. Design §10 entry 20: name `ServerSentEventLineTooLongException`, its namespace (P7b-20) and the residual R1 (P7b-5).
3. Design §12's SSE row: "SSE-41 deferred" becomes "N/A, adapter-scoped" per §11 item 21's own rule ("reported as
   adapter-scoped and vacuous, never as satisfied or deferred").
4. Roadmap Phase 7 card: "property tests moved to FsCheck" → P7b-21; the Ruby SSE test paths do not exist at
   `ruby-sdk@fa402cd` (P7b-22).
5. `CLAUDE.md`: add `ServerSentEvents/` to the core layout, move SSE out of "What is genuinely unbuilt", and point to
   `docs/sdk-documentation/sse.md`.

## Risks and open questions (all resolved here)

| # | Question | Resolution |
|---|---|---|
| Q1 | Does 3a's reader need changes for SSE? | No (P7b-2). Its `Whatwg` mode, cap and sticky failure are exactly what SSE needs; the BOM is the parser's (P7b-3). |
| Q2 | Is a string-level BOM strip equivalent to the card's 3-byte check? | Yes, by fact 2; pinned by tests on partial and doubled BOMs. |
| Q3 | Which exception for the cap, and where? | `ServerSentEventLineTooLongException : StreamingException`, SSE namespace (P7b-4, P7b-20). |
| Q4 | Should `default(SseMapResult<T>)` mean something? | No: it is a mapper failure (P7b-16). |
| Q5 | Does an early `break` propagate a release failure? | No: it is swallowed and reported, because a throw would mask the consumer's own exception (P7b-12). An explicit dispose propagates. |
| Q6 | Is a blocked read unblocked by close on every stream? | On cooperative streams through the internal token, on transport streams through disposal; a stream that honours neither stays blocked until data or EOF, documented (residual R2). |
| Q7 | Should `FromResponse` check status or content type? | No (P7b-9, P7b-18). |
| Q8 | Sync twin as a second type? | No: `AsEnumerable()`/`Map` on the one facade (P7b-15). |
| Q9 | FsCheck? | No (P7b-21), open for the lead. |
| Q10 | Aggregate per-event cap? | Not in 7b (P7b-5), open for the lead; residual R1. |
| Q11 | `CA1001` on the non-disposable reader, `CA1000` on the generic result, `CA2225` on the conversion | Scoped waiver citing `SSE-17`; avoided by the non-generic signal type; met or answered with a named alternative, never waived (positions D, F, public surface). |
| Q12 | Does `Event` as a property name trip `CA1716`? | The rule's default scope is namespaces, types and virtual/interface members; `Event` is a non-virtual property of a sealed record. If it fires, the scoped waiver cites the specification's field name. |

**Residuals.** R1: no aggregate cap on a block's data (P7b-5). R2: a stream implementation that ignores both cancellation and
disposal cannot be interrupted by `SSE-31`'s close until it returns. R3: with no logger passed to `FromResponse` and no
activity listener, a quiet-path release failure is reported to nobody (the `Disposal` residual, P3b-3).

## Exit criteria

From the card, for 7b's share:

1. 41 checklist rows, one per `SSE-n`, each ✅ with a named test, `[Trait]` and file, or N/A with its section (SSE-41).
2. The `SSE-37` architecture test enforces (no skip) and `Sse38ArchitectureTests` passes.
3. The AOT smoke consumer performs the SSE round trip above, async and blocking, on Linux CI (`dotnet publish
   tests/Dexpace.Sdk.AotSmoke -c Release` then run), alongside 7a's JSON/Tristate and 7c's paging checks.
4. Issue #10 is closed by the 7b pull request; the closing comment names the checklist rows `SSE-19`, `SSE-2`, `SSE-12` and
   the `Security/ServerSentEventLineCapTests` regression.
5. Every gate green: `dotnet build -c Release` (warnings as errors, `RS0016`/`RS0017` with the reviewed Unshipped diff,
   `MA0051`, `CA2007`), `dotnet format --verify-no-changes`, the full test run, the 80% coverage gate, the dependency audit
   (core gains no package), the reproducible pack, and `scripts/knowledge verify-structure`.
6. `docs/sdk-documentation/sse.md` written (the API, the release table of P7b-12, the cap and R1, the reconnect recipe, the
   `ToBlockingEnumerable` warning), the core package `README.md` updated, a `CHANGELOG.md` `[Unreleased]` entry, a dated roadmap
   status note, the close-out corrections above, and a housekeeping probe that reports nothing.

## Rulings

| ID | Ruling | Open for the lead |
|---|---|---|
| P7b-1 | Own grammar; no `SseParser`, no `StreamReader`, no strict mode | — |
| P7b-2 | Reuse 3a's `Utf8LineReader` (`Whatwg`) unchanged | — |
| P7b-3 | BOM consumed at the string level by the parser's one persistent flag | — |
| P7b-4 | Cap failure is `ServerSentEventLineTooLongException : StreamingException`, sticky; default 1 MiB, configurable | — |
| P7b-5 | No aggregate per-event cap; residual R1 | **yes** |
| P7b-6 | `retry`: ASCII digits, cap `int.MaxValue` ms, whole-ms `TimeSpan`, ignored value sets nothing | — |
| P7b-7 | `Data` non-null and empty when absent; `init` validation; hand-written equality and string form | — |
| P7b-8 | Reader not disposable (`CA1001` waived for `SSE-17`); `ReadNext{,Async}`, `ReadAll{,Async}`; `null` sentinel | — |
| P7b-9 | `FromResponse` owns the response from the call, success or failure; bodyless rules; no status/content-type check | — |
| P7b-10 | Body stream opened lazily through the view's own sync or async path | — |
| P7b-11 | Release disposes the opened stream then the response, both always, failures chained | — |
| P7b-12 | Swallow on natural end, `Done` and enumerator disposal; suppress onto the primary on failure; propagate on explicit dispose | — |
| P7b-13 | Atomic close flag, internal token, `IOException` normalisation for a read torn down by close | — |
| P7b-14 | One latch for four views; `InvalidOperationException` / `ObjectDisposedException` at the call | — |
| P7b-15 | Facade is `IAsyncEnumerable` only; blocking view via `AsEnumerable()` | — |
| P7b-16 | `SseMapResult<T>` + non-generic `SseMapResult` signals; `default` is a mapper failure | — |
| P7b-17 | Mapper is a synchronous `Func`; one call per raw event pulled | — |
| P7b-18 | No SSE request helper, no `Accept`, no `Last-Event-ID`, no reconnect | — |
| P7b-19 | Optional `ILogger` on `FromResponse` for `SSE-30`'s report | — |
| P7b-20 | All SSE types, the exception included, in `Dexpace.Sdk.Core.ServerSentEvents` | — |
| P7b-21 | No FsCheck: exhaustive splits plus seeded round trips | **yes** |
| P7b-22 | Port from Node `@c0ff3fd` and the chapter; re-check Ruby at plan start | — |
| P7b-23 | Test-only `LoopbackResponse.Streamed` for the wire tests | — |

## Deviation Ledger

Entries for roadmap constraint 7. None introduces a new specification deviation: `SSE-19`'s cap is §10 entry 20, the absent
strict mode is §11 item 17, and `SSE-41` is §11 item 21. The port-level departures from design §7.2's text are P7b-2, P7b-3,
P7b-7, P7b-8 and P7b-19 (close-out correction 1); from the roadmap card, P7b-21 and P7b-22 (correction 4).
