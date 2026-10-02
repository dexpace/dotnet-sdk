# Phase 3a — I/O: Design

**Status:** Draft, for review. Written 2026-10-02 against `main` at `313188f` (2a and 2b merged). The scope authority is
the roadmap's Phase 3 card and Phase List row 3 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). No phase 3
segmentation design exists; [P3a-1](#rulings) records what this document assumes in its place. The structural precedent
is the 2b design (`docs/work/mvp/phase2/phase2b/2026-09-30-phase2b-seams-design.md`).

**What this document is.** The sub-phase design for 3a. It gives one explicit decision per requirement row (45 rows), the
shape of every type 3a adds or changes, argued positions on the judgement calls the card and design §3.1 leave open, the
two verdicts §3.1 owes (one argued here, one handed to 3b), a migration plan from the as-built code, the breaking
changes, a landing order in pull-request-sized steps, the test strategy, the interface 3b builds on, and the rulings
(`P3a-1`…`P3a-15`). The rulings double as the deviation-ledger IDs (roadmap constraint 7).

**What this document is not.** It is not the plan (numbered TDD tasks) and not the checklist. It does not restate design
§3.1's mapping of the IO contract onto `Stream`, §3.7's lifecycle rules or §4.5's body construction. It cites them
(roadmap constraint 9). Where the design already decides something, this document records the decision against its row.
It argues only what the design leaves open, and the places where verification on the pinned runtime changed an obvious
answer. It does not design 3b's scope, which a sibling workflow is brainstorming at the same time; it states the
interface it hands to 3b and the row split it assumes.

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor | Kind | State at `313188f` |
|---|---|---|
| Phase 0 (gates: warnings as errors, `RS0016`/`RS0017`, `RS0030` with `BannedSymbols.txt`, `MA0051` at 70 lines, `CA2007` on `src/`, `IsAotCompatible`, the AOT smoke consumer, the test partition and `TestCategoryTests`) | **dependency** | Met. 3a is written under every gate from its first line. |
| Phase 1, S8 (`HTTP-52`'s close scope and 1 MiB cap) | **dependency**, for the `HTTP-52` row | Met. `EnsureSuccessErrorMappingTests` (`Security`) is the evidence; 3a re-homes the drain under it and must keep it green **unedited** (constraint 5). |
| **2a** (the phase 3 card's only entry criterion) | **dependency** | Met. 3a inherits `RequestBody`'s equality contract (`HTTP-46`, P2a-1), `MediaType.Charset`'s lookup that returns `null` on `utf-7` (`HTTP-24`, the decode boundary `IO-13` leans on), P2a-4's empty replayable body for an absent response body, and `HttpHeaderSyntax` (unused by 3a; 3b's multipart quotes against it). |
| 2b | **convenience** | Met. The roadmap graph draws `P2b --> P3`; the 2 segmentation design already doubted the edge, and 3a confirms it is a convenience: no 3a type compiles against a 2b-only type. 3a copies 2b's shape for the synchronous token (`Execute(…, CancellationToken)`) so the body's sync surface matches the seam's. |
| A phase 3 segmentation design | **convenience**, with a stated substitute | Absent. The segmentation rule applies (phase 3 spans ch.05 and ch.06), but the card already fixes the cut ("3a then 3b … a dependency") and names 3a's build list. P3a-1 states the row split this design assumes; the lead reconciles it with 3b's design. |

**The 3a → 3b edge is a dependency, and runs one way.** 3b's request-logging wrapper (`BODY-17`–`BODY-21`, `BODY-37`)
is built on 3a's `TeeStream` (`IO-25`–`IO-29`); its response-logging wrapper (`BODY-22`–`BODY-29`) on 3a's capped
drain and captured-byte views (`IO-19`–`IO-24`); its file body's short-transfer error (`BODY-13`) on 3a's exact-length
copy message; its materialise-once (`BODY-3`) on 3a's bounded buffer. **Nothing in 3a needs anything from 3b.** 3b's
*design* can therefore run in parallel with 3a's; 3b's *implementation* PRs that use a helper wait for the 3a PR that
lands it (see [Coupling](#coupling-with-3b-and-later-phases)).

---

## Governing documents, and the phase-start queries

- **Normative.** Every row's canonical text is its appendix-C row. `docs/product-spec/05-i-o-contracts.md` states
  `IO-1`–`IO-31`, `IO-36`–`IO-42`; `IO-32`–`IO-35` are the gap IDs, read from appendix C only.
  `docs/product-spec/06-request-and-response-body-lifecycle.md` states `HTTP-36`, `HTTP-39`, `HTTP-52` (§6.1, §6.7).
- **Design.** §3.1 (the whole IO mapping, the four .NET notes, the views, the canonical body representation, the two
  ownership rules), §3.7 (the latch), §4.5 (bodies), §5.1 (the bounded error-body copy, for `HTTP-52`), §10 entries 3–6
  and 20, §11 items 34 and 39, §12's `IO` and `BODY` rows.
- **Styleguide.** `13-resource-management.md` (13.1/13.5 the dual dispose, 13.4 never dispose an injected dependency,
  13.6 pool transient buffers and return them in `finally`), `15-performance.md` (no LOH chunk, `ArrayPool`),
  `08-error-handling.md` (8.6 custom exceptions sealed), `10-api-design.md` (10.1 minimal surface, 10.5 token last),
  `09-concurrency.md` (9.1 no blocking on async outside a documented bridge). The overlay's `Async`-suffix departure is
  kept: the new sync members are the un-suffixed twins.
- **Siblings.** `nodejs-sdk@c0ff3fd` `packages/core/src/io/` (local). `ruby-sdk`'s gems are not in the local clone (as
  2a and 2b found), so its case lists come from `ruby-sdk/docs/work/mvp/phase3/phase3a/2026-09-08-phase3a-io-contracts-design.md`
  ("Testing strategy") and its segmentation design, both local.

| Query, run 2026-10-02 | Result |
|---|---|
| `scripts/knowledge --origin note --brief` | five notes (`CA1062`, `I` prefix, `Async` suffix, Shouldly, `LangVersion`); none touches I/O |
| `scripts/knowledge --section conflicts --brief` | ten Conflicts, every one `[conformed]` or `[kept]` with a note; none is open, so 3a inherits no conflict |
| `scripts/knowledge --prefix-info IO` | 42 IDs: 35 MUST, 6 SHOULD, 1 MAY; 38 substantive, 4 uncited |
| `scripts/knowledge --prefix-info BODY` / `HTTP` | 37/37 and 53/53 substantive |
| `scripts/knowledge --gaps IO` | `IO-32`–`IO-35`, "appendix C is their only normative statement" |
| `scripts/knowledge --gaps BODY` / `HTTP` | none |
| `scripts/knowledge --prefix IO --brief`; `--req HTTP-36`, `HTTP-39`, `HTTP-52`, `IO-32` | consistent with design §3.1; `IO-32` "no entry cites it yet" |
| `scripts/knowledge --role styleguide --grep 'Stream\|leaveOpen\|ArrayPool\|NotSupported'` | 13.6 and 15.x pool transient buffers; nothing on stream ownership beyond 13.4 |

**One corpus observation, not a finding against 3a.** The `--prefix IO` query warns that 30 entries derive from
`docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md` at a stale sha (2b's dated corrections to §3.2–§3.5 changed
the file). §3.1's text is unchanged in substance, and this design read the file directly. A re-harvest is the lead's call;
nothing here depends on it.

**Scope.** 45 rows: `IO-1`–`IO-42` (35 MUST, 6 SHOULD, 1 MAY) and `HTTP-36`, `HTTP-39`, `HTTP-52` (3 MUST). The other nine
phase-3 `HTTP` IDs and all 37 `BODY` IDs are 3b's (P3a-1). 3b's concurrent design also claims `HTTP-36`, `HTTP-39` and
`HTTP-52`; P3a-1 recommends that 3a keep only `HTTP-39` (43 rows), and the lead decides.

---

## Verified facts that shape the decisions

Each was verified on 2026-10-02 on the pinned SDK (10.0.401) with a throwaway program in the scratchpad, never in the
repository. Rows and rulings cite them by number.

1. **A `Stream` that overrides only the synchronous `Write` turns every async write into a pool-thread sync write.**
   `WriteAsync(ReadOnlyMemory<byte>)` and `WriteAsync(byte[], int, int)` both reached the sync `Write`, on a thread-pool
   thread. And overriding only `WriteAsync(ReadOnlyMemory<byte>)` is not enough: `WriteAsync(byte[], int, int)` still
   went to the sync `Write`. A core `Stream` subclass must override **both** async overloads.
2. **`HttpContent` without a sync `SerializeToStream` throws `NotSupportedException`** on the synchronous path: "The
   synchronous method is not supported by 'AsyncOnlyContent'. If you're using a custom 'HttpContent' and wish to use
   synchronous HTTP methods, you must override its 'SerializeToStream' virtual method." The BCL's own extensible
   content type makes the sync path opt-in with a loud default.
3. **`new MemoryStream(array, index, count, …)` rejects `index > array.Length` eagerly** (`ArgumentException`), and
   accepts `index == array.Length` with `count` 0. `IO-21`'s lazy overflow is therefore not free on a `MemoryStream`
   view; a helper must clamp.
4. **`Stream.CopyTo` and `CopyToAsync` never issue a zero-count read** (an instrumented source recorded zero such
   calls over a 100,000-byte copy).
5. **`ReadExactly` on a short source throws `EndOfStreamException`** ("Unable to read beyond the end of the stream.").
   The message names neither count, so `BODY-10`'s "naming delivered-of-total" needs core's own message.
6. **A disposed read-only `MemoryStream` view** throws `ObjectDisposedException` on `ReadByte`, still returns its bytes
   from `ToArray()`, and with `publiclyVisible: false` refuses `TryGetBuffer`.
7. **`Encoding.UTF8.GetString` replaces a malformed sequence with U+FFFD** (`61 FF 62` decodes to `a�b`); it does
   not throw.
8. **`Encoding.GetEncoding("iso-8859-1")` and `Encoding.Latin1` are in-box** (`é` → `E9`), with no provider
   registration.
9. **`Stream.Read` returns 0 for a zero-count request mid-stream and at EOF**, and 0 for a one-byte request at EOF
   (design §3.1's first note, re-verified).
10. **`Array.MaxLength` is 2,147,483,591.**
11. **`Stream.Dispose()` is not latched by the BCL.** Calling `Dispose()` twice and `DisposeAsync()` once on a
    `MemoryStream` subclass ran `Dispose(bool)` three times. A core wrapper that disposes an inner stream from
    `Dispose(bool)` disposes it as often as it is itself disposed unless it latches (`IO-41`'s "closed at most once").
12. **`new StreamReader(stream)` closes the caller's stream on dispose** (design §3.1's `leaveOpen` trap, re-verified);
    **`StreamReader.ReadLine` over `"a\rb\r\nc"` returns `a|b|c`**, splitting on the lone `\r` that `IO-14` keeps.
13. **`HttpContent.ReadAsStreamAsync` returns the same `Stream` on a second call**, and the second handle observes the
    first reader's position (1 after one `ReadByte`). **`ReadAsStream()` after `ReadAsStreamAsync()` throws
    `HttpRequestException`** ("The content's stream has already been retrieved via async ReadAsStreamAsync and cannot be
    subsequently accessed synchronously"). The BCL itself is not uniform about re-opening a body.

---

## Decisions, one per requirement row

**How to read the table.**

- **Exit** uses the roadmap's constraint-3 legend. A combined mark (`N/A; ✅`) is used as 2b used it: each clause has its
  own mark and the row says which is which.
- **Tests** are `[Trait("Category", "Unit")]` unless another category is named. Core test classes for the new helpers
  live under `tests/Dexpace.Sdk.Core.Tests/IO/` (namespace `…Tests.IO`, which shadows nothing); body tests under
  `tests/Dexpace.Sdk.Core.Tests/Http/{Request,Response}/`.
- A "pin" adds a test for behaviour that is already correct.
- **PR** points to [the landing order](#landing-order).
- §10 topic labels: `byte-stream-provider-retired` (entry 3, coined by the phase 2 segmentation design),
  `body-stream-ownership` (entry 5) and `response-body-reopen-throws` (entry 6, both coined by the roadmap card). Entry 4
  has no label yet; this document coins **`eof-is-zero-read`** for it, under the roadmap's rule of citing §10 by topic.

| ID | Level | Decision | Rationale / source | Types affected | Test approach | PR | Exit |
|---|---|---|---|---|---|---|---|
| `IO-1` | MUST | **N/A for the letter** (`Source.read(Buffer)` and the −1 sentinel do not exist; `Stream.Read` fills a caller span at the caller's offset and returns 0 at EOF). **✅ for the invariant** core can own: every core accumulator appends in order and never overwrites, and no read returns more than requested. | §3.1 table; §10 `eof-is-zero-read`; fact 9. | `StreamCopy` | `StreamCopyTests.Draining_into_a_non_empty_accumulator_appends_after_its_existing_bytes`; `…A_chunked_source_is_reassembled_in_order` (over `ChunkedReadStream` at 1, 2, 3 and 7 bytes per read). | 1 | N/A; ✅ |
| `IO-2` | MUST | **N/A for the letter**; **✅ for the caller-side rule** §3.1 substitutes: **core never issues a zero-count read**. Every helper requests at least one byte, and `CopyExactly(…, 0)` performs no read at all. | §3.1 first note; §10 `eof-is-zero-read`; facts 4, 9; P3a-7. | every helper | `TestSupport`'s new `StrictReadStream` throws on any zero-count read and records each requested count. `ZeroCountReadRuleTests` drives `StreamCopy` (all four operations), `Utf8LineReader` (both modes), `BodyMaterializer`, `RequestBody.ToReplayable(Async)` and `ResponseBody.ReadAsBytes(Async)` over it: no zero-count request is recorded. | 1, 4, 5 | N/A; ✅ (P3a-7) |
| `IO-3` | MUST | Free on the BCL (`Stream.Read(buf, 0, -1)` throws), and **built** for every size-taking parameter core adds: `CopyExactly`'s count, `DrainUpTo`'s cap, `CapturedBytes.Slice`'s offset and count, `TeeStream`'s tap limit, `Utf8LineReader`'s line cap, `FromStream`'s `contentLength` (below −1). Each throws `ArgumentOutOfRangeException` before any I/O. | §3.1 table. | the helpers; `RequestBody.FromStream`, `ResponseBody.FromStream` | One `[Theory]` per helper: a negative argument throws `ArgumentOutOfRangeException` naming the parameter, and the source's `StrictReadStream` records no read. `FromStream(…, contentLength: -2)` throws for both directions. | 1–4 | ✅ |
| `IO-4` | MUST | Free: `Stream.Write` has no partial-write result. Core's one sink, `TeeStream`, forwards the full span to the primary in one call. | §3.1 table. | `TeeStream` | `TeeStreamTests.The_primary_receives_every_byte_written_in_order` (sync, `Span`, `byte[]`, `ReadOnlyMemory`, `WriteByte`). | 3 | ✅ |
| `IO-5` | MUST | Flush is `Stream.Flush`/`FlushAsync`; `TeeStream` forwards both. Close is `IDisposable`/`IAsyncDisposable` on every core wrapper. | §3.1 table. | `TeeStream`, `Utf8LineReader` | `TeeStreamTests.Flush_and_FlushAsync_reach_the_primary`; the dispose tests of `IO-41`. | 3, 4 | ✅ |
| `IO-6` | MUST | **Helper layer: wrapping takes ownership unless `leaveOpen: true`** (the BCL convention, fact 12). `TeeStream` and `Utf8LineReader` dispose their stream on dispose by default; `CapturedBytes` wraps a core-owned array and owns no external resource. The "stream bridges" clause is vacuous (no bridges, `IO-16`). The body-layer inversion is 3b's verdict (P3a-2). | §3.1 "Two ownership rules"; §10 `body-stream-ownership`; P3a-4. | `TeeStream`, `Utf8LineReader` | `…Disposing_the_wrapper_disposes_the_stream_it_owns` and `…With_leaveOpen_the_stream_survives_the_wrapper` for both types, over `DisposeCountingStream`. | 3, 4 | ✅ (helper half; body half is 3b's `BODY-8`) |
| `IO-7` | MUST | **N/A.** No `Buffer` type ships: core's write-then-read needs are met by `ArrayBufferWriter<byte>` (write side) feeding `CapturedBytes` (read side), whose FIFO order is the BCL's. Argued in position C, which closes §12's note that `IO-7` "is not argued by ID". | §3.1; §10 `byte-stream-provider-retired`; P3a-9. | — | None of its own; `IO-1`'s ordering tests exercise the pair. | 6 | N/A (P3a-9) |
| `IO-8` | MUST | **Built.** `CapturedBytes.ToArray()` and `TeeStream.SnapshotTap()` each return a fresh copy; neither consumes. | §3.1 table. | `CapturedBytes`, `TeeStream` | `CapturedBytesTests.ToArray_returns_a_fresh_copy_each_call` (mutating a returned array changes neither the capture nor the next copy); `TeeStreamTests.A_snapshot_is_unchanged_by_later_writes`. | 2, 3 | ✅ |
| `IO-9` | SHOULD | **Built, literally** (`Array.MaxLength` exists, fact 10). Materialising helpers refuse a size above the bound with `BodyTooLargeException`, whose message names the limit and points at `OpenRead`/`OpenReadAsync`; with a known length the refusal happens **before** any read or write. The response side additionally applies design §3.1's materialisation cap (64 MiB, P3a-12). The slice clause is vacuous: a capture is an array, so no slice of it can exceed the bound. | §3.1 fourth note; P3a-12. | `BodyMaterializer`, `BoundedBufferStream`, `BodyTooLargeException`, `RequestBody.ToReplayable(Async)`, `ResponseBody.ReadAsBytes(Async)`/`ReadAsString(Async)` | `BodyMaterializerTests.A_declared_length_above_the_limit_is_refused_before_any_read` (a `StrictReadStream` records no read); `…An_unknown_length_body_that_exceeds_the_limit_is_refused` (limit lowered through the internal overload); `ToReplayable_refuses_a_declared_length_above_Array_MaxLength_without_consuming_the_body` (a later `WriteTo` still succeeds). | 5 | ✅ |
| `IO-10` | MUST | **N/A.** Both operations belong to the reference `Buffer`, which does not ship (`IO-7`). The one windowing operation core has is `CapturedBytes.Slice`, and it follows `IO-21`'s lazy rule, not `IO-10`'s eager rejection; the two clauses describe different reference operations and cannot both bind one .NET operation. Recorded as a §11 resolution. | P3a-9 (position C). | — | `Slice(…).ToArray()` is `IO-20`/`IO-21`'s test. | 6 | N/A (P3a-9) |
| `IO-11` | MUST | **N/A for `exhausted()` and `readByte()`** (the surface does not exist; `Stream.ReadByte` returns −1 at EOF). **✅ for the count-less drain:** `ResponseBody.ReadAsBytes`/`ReadAsBytesAsync` return every remaining byte, and an empty array for an empty body. | §3.1 table; §10 `eof-is-zero-read`. | `ResponseBody` | Pin: `ResponseBodyTests.ReadAsBytes_of_an_empty_body_is_an_empty_array` (both forms, over `FromBytes([])` and `FromStream(Stream.Null)`); `…ReadAsBytes_returns_every_byte_of_a_chunked_source`. | 5 | N/A; ✅ |
| `IO-12` | MUST | Free on the BCL (`ReadExactly`, fact 5), and **built** where core needs an exact count: `StreamCopy.CopyExactly` never delivers a short result; a premature end throws `EndOfStreamException` naming delivered-of-total. | §3.1 table; `HTTP-39`. | `StreamCopy` | The `HTTP-39` tests. | 1 | ✅ |
| `IO-13` | MUST | Free on `Encoding`, with the write side already built (`FromString(text, contentType, encoding)`) and the read side `ReadAsString(Async)`'s single decode boundary (whose charset policy is `HTTP-42`, 3b's row). 3a moves the decode into one private helper both forms share, so 3b's BOM strip (§11 item 34) lands once. The line reader decodes UTF-8 only, replacing malformed sequences (fact 7, P3a-11). The `writeUtf8(substring range)` letter is `Encoding.GetBytes(text.AsSpan(range))`, BCL. | §3.1 "Encoding, stated once". | `ResponseBody`, `Utf8LineReader` | `TextRoundTripTests.Non_ASCII_text_round_trips_through_UTF_8_and_ISO_8859_1` (`FromString("é…", …, Encoding.Latin1)` writes `E9`; a response body declaring `charset=iso-8859-1` reads it back, sync and async). | 4, 5 | ✅ |
| `IO-14` | MUST | **Built:** the internal `Utf8LineReader` in its default mode. `\n` and `\r\n` terminate; a lone `\r` is content, decided by one byte of lookahead even across a refill; `null` when exhausted before any byte; a final unterminated line as-is; an empty line is `""`. It replaces `StreamReader.ReadLine`, which is the wrong tool (fact 12) and is banned in `src/` (P3a-14). | §3.1 third note; P3a-11. | `Utf8LineReader`, `LineTerminators` | `Utf8LineReaderTests`, driven by `tests/vectors/io/utf8-lines.json` (ported from `nodejs-sdk@c0ff3fd` `io/buffered-source.text.test.ts` and appendix C), each vector run at 1, 2, 3, 7 and 4,096 bytes per read so every terminator straddles a refill; `…A_lone_CR_before_EOF_is_content`; `…A_multi_byte_character_split_across_reads_decodes_once`; `…A_line_longer_than_the_cap_fails_and_the_reader_stays_failed`; `…Reading_a_long_line_byte_by_byte_scans_each_byte_once` (an internal scanned-byte counter equals the input length). | 4 | ✅ |
| `IO-15` | MUST | **N/A.** No `skip`; `Stream.Seek` or a discard read is the BCL's. | §10 `eof-is-zero-read`. | — | None. | 6 | N/A |
| `IO-16` | SHOULD | **N/A.** The SDK's byte surface already *is* the host-native `Stream`, so there is nothing to bridge to. | §3.1 table; §10 `eof-is-zero-read`. | — | None. | 6 | N/A |
| `IO-17` | MUST | **✅ for the pump:** `StreamCopy.CopyToEnd`/`CopyToEndAsync` pump to EOF, return the total, request at least one byte per read (fact 4's property, owned by core), and terminate on 0. `RequestBody.FromStream` of unknown length uses it. **N/A for the zero-read-is-a-violation clause:** 0 *is* EOF on .NET, so no spin is possible and the clause has no reading to enforce. | §3.1 first note; §10 `eof-is-zero-read`. | `StreamCopy`, the private stream request body | `StreamCopyTests.CopyToEnd_returns_the_total_and_stops_on_end_of_stream`; the `IO-2` strict-stream run. | 1 | ✅; N/A |
| `IO-18` | SHOULD | **N/A.** `Stream` has no emit tier. | §3.1 table; §10 `eof-is-zero-read`. | — | None. | 6 | N/A |
| `IO-19` | MUST | **Built:** `CapturedBytes.OpenRead()` returns a fresh read-only view over the whole capture, with its own cursor. | §3.1 "Non-consuming views". | `CapturedBytes` | `CapturedBytesTests.Reading_one_view_moves_no_other_view` (two views, interleaved reads, both see every byte). | 2 | ✅ |
| `IO-20` | MUST | **Built:** `CapturedBytes.Slice(offset, count)` is a window of at most `count` bytes from `offset`; reading past it returns 0 (end of window). Slicing never touches the parent. | §3.1. | `CapturedBytes` | `…A_slice_exposes_its_window_and_then_ends`; `…Slicing_leaves_the_parent_unchanged`. | 2 | ✅ |
| `IO-21` | MUST | **Built** (not free, fact 3): `Slice` with `offset > Length` constructs and reads as empty; a negative offset or count throws `ArgumentOutOfRangeException` at construction. Each read form surfaces the overflow its own way: `Read` returns 0, `ReadExactly` throws `EndOfStreamException`, the line reader returns `null`. | §3.1 ("Lazy offset overflow … falls out" — corrected: it needs the clamp). | `CapturedBytes` | `…An_overflowing_slice_constructs_and_reads_empty` (all three read forms); `…A_negative_offset_or_count_is_rejected_eagerly`. | 2 | ✅ |
| `IO-22` | MUST | **✅** for "closing a view closes neither the capture nor another view". **✅ by construction** for "closing the parent invalidates every slice": a capture has no close, because **captured bytes are never pooled**, so a stale read of recycled memory cannot happen. The rule is mechanised by P3a-8's `RS0030` entry, not left to review. | §3.1 "parent-close invalidation reduces to a pooling rule"; P3a-8. | `CapturedBytes`, `PooledChunk`, `BannedSymbols.txt` | `…Disposing_a_view_leaves_the_capture_and_other_views_readable`. The ban's evidence is the build. | 1, 2 | ✅ (P3a-8) |
| `IO-23` | MUST | **Built:** views and slices are independent (own cursor, own budget); `Slice` of a `Slice` adds offsets and caps at the outer window. | §3.1. | `CapturedBytes` | `…A_slice_of_a_slice_composes_additively_and_is_capped_by_the_outer_window` (`[Theory]` over offset/count pairs, including an inner window that overruns the outer one). | 2 | ✅ |
| `IO-24` | MUST | **Free on the view** (fact 6): reading a disposed view throws `ObjectDisposedException`, distinct from EOF. Pinned for every read form because core relies on it. | §3.1 table (`IO-42` row). | `CapturedBytes` | `…Reading_a_disposed_view_throws_in_every_read_form` (`Read`, `ReadByte`, `ReadAsync`, `CopyTo`). | 2 | ✅ |
| `IO-25` | MUST | **Built:** `TeeStream` mirrors each write into a private tap and forwards the full payload to the primary. The primary's bytes never depend on the tap. | §3.1 "the request-logging wrapper"; P3a-10. | `TeeStream` | `TeeStreamTests.The_primary_receives_the_full_payload_whatever_the_tap_limit` (`[Theory]` over limits 0, 5, payload length, unbounded). | 3 | ✅ |
| `IO-26` | MUST | **Built:** a tap limit; at the limit, mirroring stops and forwarding continues. Default effectively unbounded (clamped to `Array.MaxLength`, fact 10); 0 mirrors nothing. | §3.1; P3a-10. | `TeeStream` | `…The_tap_stops_at_its_limit_while_forwarding_continues`; `…A_zero_limit_mirrors_nothing`; `…The_default_limit_mirrors_everything`; the `IO-3` negative-limit case. | 3 | ✅ |
| `IO-27` | MUST | **Built:** mirror **then** forward, so a failed primary write leaves its attempted bytes in the tap. The staging clause is satisfied by construction: there is no staging buffer, because the caller's span is forwarded directly, so no later write can prepend stale bytes. | §3.1; P3a-10. | `TeeStream` | `…A_failed_primary_write_still_leaves_the_attempted_bytes_in_the_tap` (`FailingWriteStream` throws on the second write; the tap holds both chunks); `…A_write_after_a_failed_one_adds_only_its_own_bytes`. | 3 | ✅ |
| `IO-28` | MUST | **✅ by construction:** `TeeStream` does not derive from `MemoryStream`, and no member hands out the tap; `SnapshotTap()` returns a fresh copy. There is no accessor to "attempt", so the "fail with a clear error" clause is vacuous. | §3.1 (the `publiclyVisible: false` note); `BODY-37` restates this at the body layer (3b). | `TeeStream` | `…Exposes_no_handle_on_its_tap` (reflection: no non-private member of `TeeStream` returns `byte[]`, `Memory<byte>`, `ArraySegment<byte>` or a `Stream`, except `SnapshotTap`, whose two calls return distinct arrays). | 3 | ✅ |
| `IO-29` | MUST | **Built:** `Flush`, `FlushAsync`, `Dispose` and `DisposeAsync` reach the primary only; the tap survives disposal and can still be snapshotted. | §3.1; P3a-10. | `TeeStream` | `…Dispose_reaches_the_primary_once_and_the_tap_survives`; `…Flush_never_touches_the_tap`. | 3 | ✅ |
| `IO-30` | MUST | **N/A.** No provider seam, so no factories. The one clause with a .NET counterpart — a byte-array source is an independent copy — is held by `RequestBody.FromBytes`/`ResponseBody.FromBytes` (`XCUT-15`, design §4.5). | §10 `byte-stream-provider-retired`; §3.6. | — | None. | 6 | N/A |
| `IO-31` | MUST | **N/A.** No registry, no discovery. | §10 `byte-stream-provider-retired`; §10 `no-provider-registry`. | — | None. | 6 | N/A |
| `IO-32` | MUST | **N/A** (gap ID, read from appendix C: install idempotence and different-provider rejection). There is no install call. | As `IO-31`; 2b's `SEAM-6` row. | — | None. | 6 | N/A |
| `IO-33` | MUST | **N/A** (gap ID: explicit install wins after auto-resolution). Neither exists. | As `IO-31`. | — | None. | 6 | N/A |
| `IO-34` | SHOULD | **N/A** (gap ID: caching a successful auto-resolution, re-evaluating an unresolved one). No resolution to cache. | As `IO-31`; 2b's `SEAM-7` row. | — | None. | 6 | N/A |
| `IO-35` | SHOULD | **N/A** (gap ID: warn on late replacement of a handed-out provider). Nothing can be replaced. | As `IO-31`; 2b's `SEAM-8` row. | — | None. | 6 | N/A |
| `IO-36` | MAY | **N/A.** One default load context; no hierarchical discovery. | As `IO-31`; 2b's `SEAM-10` row. | — | None. | 6 | N/A |
| `IO-37` | MUST | **✅, by documentation and one test.** Each helper's remarks state the single-threaded contract (`Stream`'s). The one concurrency guarantee that does exist — independent views may be read from different threads — is tested. | §3.1 table. | the helpers | `CapturedBytesTests.Independent_views_can_be_read_on_different_threads` (two tasks, one view each, `Parallel` reads; each sees every byte). | 2 | ✅ |
| `IO-38` | MUST | **✅ by construction**, as `IO-22`'s second half: no capture is pooled, so there is no parent close whose visibility could tear (P3a-8). If a later phase pools a capture, §3.1's `Volatile`-read closed flag is owed, and the `RS0030` entry forces that phase to face it. | §3.1; P3a-8. | `BannedSymbols.txt` | The ban (build evidence). | 1 | ✅ (P3a-8) |
| `IO-39` | SHOULD | **N/A.** No registry. | §10 `byte-stream-provider-retired`. | — | None. | 6 | N/A |
| `IO-40` | MUST | **✅ by rule, mechanised.** Core never sets `ReadTimeout`/`WriteTimeout` (now an `RS0030` entry, P3a-14). `TeeStream` forwards the caller's token to the primary unchanged, links nothing, and lets an `OperationCanceledException` from the primary propagate as the same instance. The sync helpers check the token between chunks and nowhere else. | §3.1 table; §10 `cooperative-cancellation`. | `TeeStream`, `StreamCopy`, `BannedSymbols.txt` | `TeeStreamTests.The_callers_token_reaches_the_primary_unchanged`; `…An_OperationCanceledException_from_the_primary_propagates_as_the_same_instance`; `StreamCopyTests.A_cancelled_token_stops_the_sync_copy_between_chunks`. | 1, 3 | ✅ |
| `IO-41` | MUST | **Built** (not free, fact 11): `TeeStream` and `Utf8LineReader` latch their dispose with `Interlocked.Exchange`, so the owned stream is disposed at most once across any mix of `Dispose` and `DisposeAsync`. Views are `MemoryStream`s, whose double dispose is harmless. | §3.7 "Idempotence is a latch". | `TeeStream`, `Utf8LineReader` | `…Disposing_twice_in_any_mix_disposes_the_owned_stream_once` for both types (`DisposeCountingStream`). | 3, 4 | ✅ |
| `IO-42` | MUST | **Built:** after dispose, `TeeStream.Write`/`Flush` and `Utf8LineReader.ReadLine` throw `ObjectDisposedException` (design §3.1's reading of "an I/O error"); the in-memory exemption holds — `TeeStream.SnapshotTap()` still works after dispose, and a capture has no close. | §3.1 table (`IO-42` row). | `TeeStream`, `Utf8LineReader` | `…Writing_after_dispose_throws_ObjectDisposedException`; `…The_tap_can_be_snapshotted_after_dispose`; `Utf8LineReaderTests.Reading_after_dispose_throws_ObjectDisposedException`. | 3, 4 | ✅ |
| `HTTP-36` | MUST | **Built:** the single write-to-sink operation gains its **synchronous twin** `WriteTo(Stream, CancellationToken)`, with `ToReplayable` beside it (P3a-5). Both forms produce identical bytes and share one consume guard. `ContentType` stays nullable; `ContentLength`'s −1 sentinel is now validated (`FromStream` rejects anything below −1); `IsReplayable` defaults to `false`. | §3.1 "The canonical body representation"; §4.5; P3a-5. | `RequestBody` and its private variants | `RequestBodyContractTests`: a test-local subclass reports −1 and `false`; its un-overridden `WriteTo` throws `NotSupportedException` naming the subclass; each SDK variant writes the same bytes through `WriteTo` and `WriteToAsync`; a stream body written once in either form throws `StreamConsumedException` on a second write in the other form; `ToReplayable` and `ToReplayableAsync` return equal bodies. | 1, 5 | ✅ |
| `HTTP-39` | MUST | **Built:** `StreamCopy.CopyExactly(Async)`. Writes exactly `count` bytes; a premature end throws `EndOfStreamException` "The source ended after {delivered} of {count} bytes."; `count` 0 performs no read; never reads past `count`. `RequestBody.FromStream` with a known length uses it (P3a-6). The zero-length-read clause is `eof-is-zero-read`: 0 is EOF, and the length comparison is what catches truncation. | §3.1 first note; §10 `eof-is-zero-read`; P3a-6. | `StreamCopy`, the private stream request body | `StreamCopyTests` (`[Theory]` over sources shorter, equal and longer than `count`, at several read sizes; the message names both numbers; the longer source keeps its remainder unread). Integration, in `tests/Dexpace.Sdk.Http.SystemNet.Tests`: `ExactLengthBodyWireTests.A_body_shorter_than_its_declared_length_fails_the_send` (the failure chain contains the `EndOfStreamException` naming "of N") and `…A_source_longer_than_its_declared_length_sends_exactly_the_declared_bytes` (the loopback receives N bytes and `Content-Length: N`). | 1 | ✅ (`BODY-10`'s row is 3b's and cites these tests) |
| `HTTP-52` | MUST | **Already met by phase 1 (S8)**; 3a re-homes `Response`'s private `DrainCappedAsync` onto `StreamCopy.DrainUpToAsync`, with a pooled chunk instead of a fresh 80 KiB array per call (styleguide 13.6, design §5.1's stated improvement). Behaviour is unchanged and the `Security` class is untouched. The shared `ErrorBodyBuffer`, the `ErrorMappingPolicy` and the no-body-returns-unchanged clause on the pipeline step stay **⏳ 4c**, as phase 1's checklist routed them. | §5.1 "The bounded error-body copy"; phase 1 checklist S8; P3a-13. | `Response`, `StreamCopy` | Cite `EnsureSuccessErrorMappingTests` (`Security`, all six methods, unedited). Pin: `StreamCopyTests.DrainUpTo_stops_at_the_cap_and_reports_whether_the_end_was_seen`. | 1 | ✅; ⏳ 4c (`ErrorBodyBuffer`, `ErrorMappingPolicy`) |

**Totals, as designed.** 28 ✅; 4 split `N/A; ✅` or `✅; N/A` (`IO-1`, `IO-2`, `IO-11`, `IO-17`); 13 N/A (`IO-7`,
`IO-10`, `IO-15`, `IO-16`, `IO-18`, `IO-30`–`IO-36`, `IO-39`). One ✅ carries a ⏳ half (`HTTP-52`, 4c). No 🚫.

---

## Argued positions

### A. Which sub-phase argues which §3.1 verdict

The roadmap card says §3.1 owes two verdicts, `body-stream-ownership` and `response-body-reopen-throws`, "each argued",
and the task leaves the split to this design.

- **`response-body-reopen-throws` is argued here (P3a-3).** 3a adds `ResponseBody.OpenRead`. The moment a second form of
  "open" exists, the question "what does the second open return?" has to be answered for two forms and their mix, and
  it is answered in the code 3a writes. Leaving it to 3b would mean 3a ships `OpenRead` with an undecided second-call
  behaviour that 3b then changes.
- **`body-stream-ownership` is argued by 3b (P3a-2).** Its subject is `BODY-8`: which *bodies* close which sources. Every
  variant where the rule bites — the file body (opens and disposes a `FileStream` per write), the seekable-stream body
  (must not dispose what it rewinds), `ResponseBody.FromStream`'s ownership transfer under 3b's dispose latch — is 3b's.
  3a fixes only the half §10 entry 5 calls "`IO-6` at the helper layer" (P3a-4), which 3b inherits and must not
  contradict: 3b's request-logging wrapper builds its `TeeStream` with `leaveOpen: true`, because the destination it
  wraps belongs to the transport.

### B. `response-body-reopen-throws`: confirmed, and extended across the two forms

**Options.** (1) Return the same handle on a second open, the specification's letter (`BODY-14`: "requesting the handle
repeatedly MUST return the same underlying handle, not a fresh replay"). (2) Throw `StreamConsumedException` on a second
open in either form (§10 entry 6 as built for the async form). (3) Throw for one form and return the handle for the other.

**Evidence.** Fact 13 shows what option 1 means on .NET: `HttpContent` returns the same stream, and the second holder
reads from wherever the first left off. That is the silent interleave §10 entry 6 names. Fact 13 also shows that the BCL
itself does not apply option 1 across forms — a sync open after an async open throws — so option 1 has no uniform
ecosystem precedent to lean on (P14 does not decide it). Option 3 is the worst: a caller's behaviour would depend on which
overload it happened to call.

**Decision: option 2, with one latch for both forms.** Each SDK response variant keeps a single `_consumed` flag, flipped
by whichever of `OpenRead` and `OpenReadAsync` runs first; the loser throws `StreamConsumedException`, whatever its form.
This narrows no correctness property (P9): the guarantee `BODY-14` protects is "not a replay", and throwing is the louder
form of it. The replayable error body (`FromReplayableBytes`) is unaffected: it is replayable by design (`HTTP-52`) and
opens a fresh view each time. The `BODY-14`/`HTTP-41` rows (3b's) cite this position; §10 entry 6 gets a dated
correction naming both forms. The adapter's `HttpResponseMessageBody` gains `OpenRead` in 8b (P3a-5), and must flip the
same latch **before** touching `HttpContent`, so the SDK's `StreamConsumedException` wins over fact 13's
`HttpRequestException`.

### C. `IO-7` and `IO-10`: one windowing operation, and no buffer type

§12 records that `IO-7` "is not argued by ID". The argument: the reference `Buffer` is two things at once, a FIFO
write-then-read queue and the backing store for views. On .NET these separate cleanly. The write-then-read role is
`ArrayBufferWriter<byte>` (or `MemoryStream`), BCL types whose ordering is theirs; core never needs a structure that is
*simultaneously* read and written, because every core use is "fill, then read many times" (a capped drain, a tap, a
materialised body). The view role is `CapturedBytes`. Shipping a `Buffer` type to satisfy `IO-7`'s letter would be the
parallel vocabulary P14 forbids and the fabricated tier P11 forbids, which §3.1 already decided. `IO-7` is N/A.

`IO-10` is the sharper case. It requires `copyTo(offset, count)` to **reject** out-of-range windows, while `IO-21` requires
`slice(offset, count)` to **accept** an overflowing offset lazily. In the reference these are two operations on two
types. Core has one windowing operation, `CapturedBytes.Slice`, whose consumers (3b's response-logging views, `BODY-33`'s
preview, `BODY-32`'s capped snapshot as `Slice(0, cap).ToArray()`) all want the slice semantics: "give me what is there,
up to this much". Building a second, strict `CopyRange` to tick `IO-10` would ship a method nothing calls. So `IO-10`'s
copy clause and its `clear` clause are N/A with the `Buffer`, and the single windowing operation follows `IO-21`. This
is recorded as a new design §11 item (a reading of two clauses that cannot both bind one .NET operation), not a §10
deviation, because neither clause's letter has a .NET subject.

### D. The sync surface: virtual with a loud default, not abstract

**Options.** (1) `abstract` sync members on `RequestBody` and `ResponseBody`: a compile-time guarantee that every body
supports the real sync path of §3.2. (2) `virtual` members whose base implementation bridges to the async member
(sync-over-async). (3) `virtual` members whose base implementation throws `NotSupportedException` naming the subclass,
overridden by every SDK variant.

**Against option 1.** `RequestBody` and `ResponseBody` are the one open hierarchy in the model (§4.5), "because
transports and generated code must be able to implement bodies". Making the sync members abstract is a source break for
every subclass, and at `313188f` that is eight test subclasses of `ResponseBody`, one of them inside a `Security` class
(`EnsureSuccessErrorMappingTests.TrackingBody`), plus the adapter's `HttpResponseMessageBody`, which would force 3a to
implement `HttpContent.ReadAsStream` in `Dexpace.Sdk.Http.SystemNet` ahead of 8b and outside the card's project list. It
also forces every async-only third-party source (a generator, a pipe) to hand-write a sync path it may not have.

**Against option 2.** Sync-over-async is banned in `src/` (`RS0030`) precisely so the real sync path of coupling
obligation 5 stays real; a base-class bridge would reintroduce it silently on every body that forgets to override.

**Decision: option 3**, which is fact 2's BCL precedent: `HttpContent` makes its sync path opt-in with exactly this
default. Every SDK variant (bytes, string, value, stream; response bytes, replayable bytes, stream) overrides both forms,
so the SDK's own bodies are never the failure. A third-party body used on the sync path fails loudly, with a message
naming its type and the member to override. 3b's new variants override both forms (see [Coupling](#coupling-with-3b-and-later-phases)).
The derived sync members (`ToReplayable`, `ReadAsBytes`, `ReadAsString`) are implemented in the base over `WriteTo` and
`OpenRead`, so a subclass overrides one member per direction.

**Why a sync `ToReplayable`, `ReadAsBytes` and `ReadAsString`, and not only `WriteTo`/`OpenRead`.** 4c's real
synchronous `Process` and 6a's retry decide whether to materialise a single-use body; without a sync `ToReplayable`
they would need sync-over-async, which is banned. The two readers complete the twin set so no sync caller reaches for a
bridge. The rule is uniform: **every async body member has a sync twin with the same name minus `Async`**. `Response`'s
`EnsureSuccessAsync` is not a body member; its sync twin is 4c's (with `ErrorMappingPolicy`'s sync `Process`), over 3a's
sync drain, which ships now so 4c needs nothing new.

### E. The materialisation cap: response side only, fixed until 5a

Design §3.1 says a configurable materialisation cap (default 64 MiB) "applies first" to materialising helpers and "is part
of the options surface (§8.2)". §8.2 does not define it, and phase 5a owns the options surface. Three questions are
left open.

**Which helpers.** The cap protects the process from a size **someone else controls**. A response body's size is the
server's choice, and on an adversarial server unbounded: `ReadAsBytes(Async)` and `ReadAsString(Async)` take the cap. A
request body's size is the caller's own data, and `ToReplayable(Async)` is an explicit call to buffer it; capping it at
64 MiB would refuse a caller's deliberate 100 MiB upload-with-retries, and for an unknown-length body would do so only
after consuming it, leaving the caller with neither the stream nor a copy. So the request side is bounded only by
`IO-9`'s host limit (`Array.MaxLength`), refused before any write when the length is known. Retry's *automatic*
materialisation is a different question with a different owner: 6a decides its own buffering cap (`BODY-4`, `RETRY`).
This departs from §3.1's wording ("applies first" to every materialising helper), so it is a dated correction to §3.1,
and P3a-12 marks it open for the lead.

**The failure.** A new public `BodyTooLargeException : StreamingException`, sealed with the standard constructors
(styleguide 8.6, `CA1032`). Options considered: `InvalidOperationException` (untyped, so no caller can catch the
adversarial-size case distinctly), `IOException` (`MemoryStream`'s own "Stream was too long" at `int.MaxValue`, which
is what the as-built `ToReplayableAsync` throws — a low-level failure `IO-9` says to replace), and the existing concrete
`StreamingException` (too broad: it also roots consumption errors). The message names the limit and, when known, the
declared length, and says to read the body as a stream with `OpenRead`/`OpenReadAsync`. It never echoes body content.

**The knob.** None in 3a. Adding a `maxBytes` overload to each reader runs into `RS0026`/`RS0027` with the existing
`ReadAsBytesAsync(CancellationToken = default)` (an optional-parameter member must have the most parameters among its
overloads), and the clean fix — reshaping the existing members — is churn that 5a's real options surface would redo. So
3a ships the default as a public constant (`ResponseBody.DefaultMaxMaterializedBytes`, like `Response.MaxBufferedErrorBytes`)
and enforces it through an internal overload that tests can lower. A caller who needs more streams through `OpenRead(Async)`,
which is the actionable message `IO-9` asks for. 5a makes the cap configurable (hand-off).

**Detection.** With a declared length above the cap, refuse before reading. With an unknown length, drain up to the cap;
if the drain stopped at the cap without seeing the end, read one more byte: any byte means the body is larger and the call
throws. The convenience reader disposes the body in its `finally` either way (`BODY-16`), so the probe consumes nothing a
caller could still use.

### F. The exact-length copy in the stream request body

`RequestBody.FromStream(stream, type, contentLength)` today copies the source to EOF whatever `contentLength` says. With a
known length it now copies **exactly** that many bytes (P3a-6):

- **Short source:** `EndOfStreamException` naming delivered-of-total, from inside the write, so the transport fails the
  send instead of framing a short body. (On the reference transport `SocketsHttpHandler` would also notice a short
  `Content-Length` body, but with its own message and only on that transport; `HTTP-39` is core's to guarantee.)
- **Long source:** exactly `contentLength` bytes are sent and the remainder is **left unread**. Reading one byte further to
  detect the excess would consume from a stream the body does not own (§3.1: `FromStream` never disposes or otherwise
  owns the caller's stream) and could block on a live source. The declared length is the contract; the documentation
  says so.
- **Zero:** no read is issued (`BODY-10`'s legitimate empty write; `IO-2`'s caller-side rule).
- **Unknown (−1):** `CopyToEnd`, as before.

The exception type is the BCL's `EndOfStreamException`, as design §3.1 names it: it is an `IOException`, which
`HttpClient` wraps into the send failure and which design §6.1's classification already reads as the retryable I/O family.
The message text comes from one internal helper (`StreamCopy.ShortTransferMessage`) that 3b's file body reuses for
`BODY-13`, so "the message form cannot diverge" (§3.1, corpus `message-bodies/4d85050f`).

`FromStream` also validates what it can at construction: `contentLength` below −1 throws `ArgumentOutOfRangeException`
(both directions), and a request source with `CanRead == false` throws `ArgumentException`. Both are fail-fast checks
that turn a later, opaque failure into an immediate one.

### G. The line reader's two modes, cap and decoding

**Modes.** `LineTerminators.LineFeedOrCrLf` (the default, `IO-14`) and `LineTerminators.Whatwg` (CR, LF or CRLF, the
event-stream rule 7b needs, design §7.2). 3a builds both because the hard part — a terminator straddling a refill — is
shared, and testing it once is cheaper than 7b re-opening the reader. The modes differ in exactly one decision:

- In `IO-14` mode a `\r` at the end of the buffered bytes needs one byte of lookahead: the reader refills; `\n` makes it a
  terminator, anything else makes it content, and EOF makes it content of the final line. The refill may block, which
  `IO-11`'s "may block while it waits" permits.
- In `Whatwg` mode a `\r` terminates **immediately** and sets a "skip one leading `\n`" flag for the next read, so a
  CR-terminated event is never held hostage to the next network chunk. This is the property an SSE consumer needs and the
  reason the mode is not just a lookup-table change.

7b owns everything above the terminator: the leading-BOM strip, field parsing, and the public failure type.

**The cap is mandatory.** A reader with no bound grows without limit on a hostile stream that never sends a newline
(issue #10, `SSE-19`). The constructor takes `maxLineBytes` (content bytes, terminator excluded), default 1 MiB, design §10
entry 20's figure. Exceeding it throws `InvalidDataException` naming the cap, and the reader latches into a failed state:
every later read rethrows, because the stream is now mid-line and no resynchronisation is correct. `InvalidDataException`
is internal plumbing; 7b maps it to its own public failure (§10 entry 20).

**Decoding.** UTF-8 only, over the whole accumulated line (so a character split across reads decodes once), with
malformed sequences replaced by U+FFFD (fact 7). That is the reference's `readUtf8Line` behaviour and WHATWG's decode
rule; throwing would let one bad byte kill an event stream. A leading BOM is ordinary content in both modes (Node's
"a BOM is preserved on every line" case), and 7b strips it at stream start. Recorded as a §11 item, because `IO-14` says
only "decoded as UTF-8".

**Linear time.** The scan resumes from the last examined index after a refill, so a long line delivered a byte at a time
is examined once per byte (Node's "readUtf8Line stays linear" case). The test counts scanned bytes through an internal
counter rather than timing, which would be flaky.

**Shape.** `string? ReadLine()` and `ValueTask<string?> ReadLineAsync(CancellationToken)`. The sync form takes no token,
because `Stream.Read` has none; a sync caller cancels by disposing. The reader's working buffer is a plain 4 KiB array
per instance, not pooled: it is long-lived (one per event stream), and P3a-8 keeps pooling to transient chunks.

### H. Captured bytes are never pooled, and the build enforces it

§3.1 reduces `IO-22`'s parent-close invalidation and `IO-38`'s cross-thread visibility to one rule: **captured body bytes
are never pooled**; `ArrayPool<byte>` is for transient copy buffers no view can reach. Left as prose, the rule is one
"optimisation" away from a stale-read bug that no test would catch, because a pooled array only shows the bug under
reuse. P3a-8 mechanises it the way this repository mechanises every such rule: `BannedSymbols.txt` gains
`ArrayPool<T>.Rent` and `MemoryPool<T>.Rent`, and the one sanctioned rent lives in an internal `PooledChunk`
(`IDisposable`, returns the array in `Dispose`, used with `using` so the return happens on every path — styleguide 13.6),
behind a single scoped `#pragma warning disable RS0030` whose why-comment cites this rule. Styleguide 13.6 ("rent
transient buffers from `ArrayPool<T>.Shared`") is kept, through `PooledChunk`. A later phase that wants to pool a capture
must lift the ban for its type, and §3.1 tells it what it then owes (a `Volatile`-read closed flag on every view). Chunk
size is 81,920 bytes, below the 85,000-byte LOH threshold (styleguide 15.x).

---

## N/A candidates, decided

The roadmap card's gap analysis counted 27 `IO` N/A candidates; its per-ID list is not in the tree, so this table is
argued from the rows, not reconciled against that count. This design marks 13 rows N/A outright and 4 more N/A in letter
with an invariant ✅; every other `IO` row is built or pinned, several because .NET did not make them free.

| ID(s) | Disposition | Section relied on | Was a candidate |
|---|---|---|---|
| `IO-30`–`IO-36`, `IO-39` | N/A | §3.1, §3.6; §10 `byte-stream-provider-retired`, `no-provider-registry` | yes |
| `IO-15`, `IO-16`, `IO-18` | N/A | §3.1 table; §10 `eof-is-zero-read` | yes |
| `IO-7`, `IO-10` | N/A | §3.1; position C (P3a-9), a new §11 item | yes |
| `IO-1`, `IO-2`, `IO-11`, `IO-17` | N/A in letter; ✅ for the invariant | §3.1 table and first note; §10 `eof-is-zero-read` | yes |
| `IO-19`–`IO-24`, `IO-37`, `IO-38`, `IO-41`, `IO-42` | built or pinned, not N/A | §3.1 "Non-consuming views"; facts 3, 6, 11; P3a-8 | partly: §3.1 calls the views "free"; facts 3 and 11 show the overflow clamp and the dispose latch are not |

---

## Type shapes

All new helpers are `internal`, in a new folder `src/Dexpace.Sdk.Core/IO/` and namespace `Dexpace.Sdk.Core.IO` (P3a-15).
No public `IO` namespace ships: the SDK's public byte surface stays `Stream` and `ReadOnlyMemory<byte>` (§3.1, P14).
Shapes are fixed here to the level 3b and later phases depend on; private members and exact parameter names are the
plan's. Every `await` in them uses `ConfigureAwait(false)`, including `await using`; every method stays under 70 lines
(the line reader's scan, refill and accumulate steps are separate methods); nothing uses reflection or dynamic code, so
`IsAotCompatible` and `IsTrimmable` hold without annotations.

### `StreamCopy` (`IO-1`, `IO-2`, `IO-12`, `IO-17`, `HTTP-39`, `HTTP-52`)

```csharp
internal static class StreamCopy
{
    internal const int ChunkSize = 81_920;                       // below the LOH threshold

    // IO-17: pump to end of stream; returns the total. Never requests zero bytes.
    internal static long CopyToEnd(Stream source, Stream destination, CancellationToken cancellationToken);
    internal static ValueTask<long> CopyToEndAsync(Stream source, Stream destination, CancellationToken cancellationToken);

    // HTTP-39 / BODY-10: exactly `count` bytes, or EndOfStreamException naming delivered-of-total. count 0: no read.
    internal static void CopyExactly(Stream source, Stream destination, long count, CancellationToken cancellationToken);
    internal static ValueTask CopyExactlyAsync(Stream source, Stream destination, long count, CancellationToken cancellationToken);

    // Capped drain: appends up to maxBytes into a caller-owned accumulator (so partial bytes survive a throw, which
    // 3b's BODY-26 needs). Returns true when end of stream was seen before the cap; false when it stopped at the cap.
    internal static bool DrainUpTo(Stream source, ArrayBufferWriter<byte> into, long maxBytes, CancellationToken cancellationToken);
    internal static ValueTask<bool> DrainUpToAsync(Stream source, ArrayBufferWriter<byte> into, long maxBytes, CancellationToken cancellationToken);

    // One message form for HTTP-39, BODY-10 and 3b's BODY-13.
    internal static string ShortTransferMessage(long delivered, long total);
}
```

None of them disposes either stream (they are not wrappers). Sync forms check the token between chunks.

### `PooledChunk` (P3a-8)

```csharp
internal readonly struct PooledChunk : IDisposable     // the one sanctioned ArrayPool<byte>.Shared.Rent (RS0030 pragma)
{
    internal static PooledChunk Rent(int minimumLength);
    internal byte[] Array { get; }                        // transient: never handed to a view or a caller
    public void Dispose();                                // returns the array
}
```

### `CapturedBytes` (`IO-8`, `IO-19`–`IO-24`, `IO-37`, `IO-38`)

```csharp
internal sealed class CapturedBytes
{
    internal static CapturedBytes Empty { get; }
    internal static CapturedBytes Own(byte[] bytes);              // takes ownership of a core-allocated, never-pooled array
    internal int Length { get; }
    internal CapturedBytes Slice(long offset, long count);        // IO-20/21/23: negative throws; overflow is an empty window
    internal Stream OpenRead();                                   // IO-19: fresh read-only, non-publicly-visible view
    internal byte[] ToArray();                                    // IO-8: fresh copy
    internal ReadOnlySpan<byte> Span { get; }
}
```

### `TeeStream` (`IO-4`–`IO-6`, `IO-25`–`IO-29`, `IO-40`–`IO-42`)

```csharp
internal sealed class TeeStream : Stream
{
    internal const long Unbounded = long.MaxValue;               // IO-26 default; clamped to Array.MaxLength internally
    internal TeeStream(Stream primary, long tapLimit = Unbounded, bool leaveOpen = false);
    internal long TapLength { get; }
    internal byte[] SnapshotTap();                               // IO-8, IO-28: a copy; works after dispose (IO-42)

    // Write-only: CanRead/CanSeek false; Read, Seek, SetLength, Length and Position throw NotSupportedException.
    // Overrides Write(byte[],int,int), Write(ReadOnlySpan<byte>), WriteByte, WriteAsync(byte[],int,int,CT) and
    // WriteAsync(ReadOnlyMemory<byte>,CT) (fact 1); each mirrors, then forwards (IO-27).
    // Flush/FlushAsync forward to the primary (IO-29). Dispose(bool)/DisposeAsync are latched (IO-41, fact 11) and
    // dispose the primary unless leaveOpen; the tap survives.
}
```

### `Utf8LineReader` and `LineTerminators` (`IO-6`, `IO-13`, `IO-14`, `IO-41`, `IO-42`)

```csharp
internal enum LineTerminators { LineFeedOrCrLf, Whatwg }

internal sealed class Utf8LineReader : IDisposable, IAsyncDisposable
{
    internal const int DefaultMaxLineBytes = 1024 * 1024;        // design §10 entry 20's figure
    internal Utf8LineReader(Stream source, LineTerminators terminators = LineTerminators.LineFeedOrCrLf,
        int maxLineBytes = DefaultMaxLineBytes, bool leaveOpen = false);
    internal string? ReadLine();
    internal ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken);
}
```

### `BodyMaterializer` and `BoundedBufferStream` (`IO-9`, P3a-12)

```csharp
internal static class BodyMaterializer
{
    // Read side: refuse a declared length above `limit` before reading; drain; probe one byte at the cap.
    internal static byte[] ReadAll(Stream source, long declaredLength, long limit, CancellationToken cancellationToken);
    internal static ValueTask<byte[]> ReadAllAsync(Stream source, long declaredLength, long limit, CancellationToken cancellationToken);
}

internal sealed class BoundedBufferStream : Stream               // write side, for ToReplayable(Async)
{
    internal BoundedBufferStream(long limit, long expectedLength); // pre-sizes when the length is known
    internal byte[] ToArray();                                    // exact-size copy
    // Throws BodyTooLargeException on the write that would exceed `limit`; write-only; overrides both WriteAsync forms.
}
```

### The public surface (`PublicAPI.Unshipped.txt`, core only)

```text
Dexpace.Sdk.Core.Errors.BodyTooLargeException
Dexpace.Sdk.Core.Errors.BodyTooLargeException.BodyTooLargeException() -> void
Dexpace.Sdk.Core.Errors.BodyTooLargeException.BodyTooLargeException(string! message) -> void
Dexpace.Sdk.Core.Errors.BodyTooLargeException.BodyTooLargeException(string! message, System.Exception? innerException) -> void
virtual Dexpace.Sdk.Core.Http.Request.RequestBody.WriteTo(System.IO.Stream! destination, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> void
virtual Dexpace.Sdk.Core.Http.Request.RequestBody.ToReplayable(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Dexpace.Sdk.Core.Http.Request.RequestBody!
virtual Dexpace.Sdk.Core.Http.Response.ResponseBody.OpenRead(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.IO.Stream!
virtual Dexpace.Sdk.Core.Http.Response.ResponseBody.ReadAsBytes(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> byte[]!
virtual Dexpace.Sdk.Core.Http.Response.ResponseBody.ReadAsString(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> string!
const Dexpace.Sdk.Core.Http.Response.ResponseBody.DefaultMaxMaterializedBytes = 67108864 -> long
```

The plan confirms the exact lines from the analyzer's code fix. `Dexpace.Sdk.Http.SystemNet`'s and
`Dexpace.Sdk.Serialization.SystemTextJson`'s API files do not change in 3a. Each new public member carries a `///` summary
(`CS1591`); the sync members' remarks state the `NotSupportedException` default, the shared consume guard, and that
`WriteTo` writes exactly `ContentLength` bytes when it is known.

---

## Migration plan from the as-built code

| Change | Sites | Mechanical rewrite |
|---|---|---|
| `StreamRequestBody.WriteToAsync` uses `CopyExactlyAsync` when the length is known, `CopyToEndAsync` otherwise; gains `WriteTo` | `RequestBody.cs` | One consume guard (`_consumed`) shared by both forms. Callers with a declared length that disagrees with their stream change behaviour (Breaking 1); the plan greps `FromStream(` across `src/` and `tests/` for a non-default length and records each site |
| `BytesRequestBody` gains `WriteTo` | `RequestBody.cs` | `destination.Write(bytes)` |
| `ToReplayableAsync` writes into `BoundedBufferStream`; gains `ToReplayable` | `RequestBody.cs` | Pre-sized when `ContentLength` is known; refuses above `Array.MaxLength` before writing |
| `FromStream` validation | `RequestBody.cs`, `ResponseBody.cs` | `ArgumentOutOfRangeException.ThrowIfLessThan(contentLength, -1)`; request side also rejects `!source.CanRead` |
| `ResponseBody` gains `OpenRead`, `ReadAsBytes`, `ReadAsString`; `ReadAsBytesAsync` goes through `BodyMaterializer`; the two string readers share one private decode | `ResponseBody.cs` (all three private variants override `OpenRead`, sharing each variant's latch) | `DefaultMaxMaterializedBytes` is the limit; the internal overload taking a limit exists for tests |
| `Response.DrainCappedAsync` → `StreamCopy.DrainUpToAsync` | `Response.cs` | The private method is removed; `EnsureSuccessAsync` keeps its `try`/`finally` dispose scope verbatim |
| Test subclasses of `ResponseBody`/`RequestBody` (`TrackingBody` ×3, `DisposalCountingBody`, `DisposalTrackingBody`, `ThrowingDisposeBody`, `ForeignBody`, the `Security` class's `TrackingBody`) | `tests/` | **None**: the sync members are virtual (P3a-5), so no subclass must change. The `Security` class stays unedited |
| `BannedSymbols.txt` gains the P3a-8 and P3a-14 entries | repository root | The plan verifies each documentation ID fires with a throwaway probe, as 2b did; `src/` has no current use of any of them (verified by grep at `313188f`) |
| `TestSupport` gains `IO/`: `ChunkedReadStream`, `StrictReadStream`, `DisposeCountingStream`, `FailingWriteStream`, `RecordingWriteStream` | `tests/Dexpace.Sdk.TestSupport/IO/` | New, non-packable; `TestCategoryTests` unaffected (TestSupport holds no tests) |
| Documentation | `CLAUDE.md` (layout tree gains `IO/`; "What is genuinely unbuilt" drops 3a's items), `src/Dexpace.Sdk.Core/README.md` (the sync body members), `docs/architecture.md` (the body paragraph, if it names only the async members) | Close-out PR |

**`Security` classes kept green** (constraint 5), **no file edited**: `EnsureSuccessErrorMappingTests` (the drain re-home is
behaviour-preserving, and its `TrackingBody` needs no new override), and every other core and wire `Security` class, none
of which touches the code 3a changes. 3a adds no `Security` class: it fixes no phase-1 defect.

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes and gets a `CHANGELOG.md` `[Unreleased]` line in the
PR that makes it (constraint 8).

| # | Change | Kind | Evidence | PR |
|---|---|---|---|---|
| 1 | `RequestBody.FromStream` with a known `contentLength` writes exactly that many bytes: a shorter source throws `EndOfStreamException` naming delivered-of-total; a longer source's remainder is left unread (was: copied to end of stream, whatever was declared) | behaviour | `StreamCopyTests`, `ExactLengthBodyWireTests` | 1 |
| 2 | `RequestBody.FromStream` and `ResponseBody.FromStream` reject `contentLength` below −1; `RequestBody.FromStream` rejects a non-readable stream | behaviour | `RequestBodyContractTests` | 1 |
| 3 | `ResponseBody.ReadAsBytesAsync` and `ReadAsStringAsync` throw `BodyTooLargeException` above `DefaultMaxMaterializedBytes` (64 MiB) (was: unbounded) | behaviour | `BodyMaterializerTests`, `ResponseBodyTests` | 5 |
| 4 | `RequestBody.ToReplayableAsync` throws `BodyTooLargeException` above `Array.MaxLength`, before writing when the length is known (was: `IOException` "Stream was too long" or `OutOfMemoryException`, after consuming) | behaviour | `RequestBodyContractTests` | 5 |
| 5 | A response body's `OpenRead` after `OpenReadAsync` (or the reverse) throws `StreamConsumedException` | behaviour (new member, new interaction) | `ResponseBodyTests` | 5 |

Additive, with no **Breaking** marker: the five virtual sync members, `DefaultMaxMaterializedBytes`,
`BodyTooLargeException`, the `BannedSymbols.txt` entries (a build rule for `src/`, not public surface).

---

## Landing order

Each step is one pull request carrying its code **and** its tests (roadmap step 5's one-PR allowance, as 2a and 2b used
it), with its `PublicAPI.Unshipped.txt` diff and `CHANGELOG.md` line where it has one.

| PR | Content | Rows | Gate | Notes |
|---|---|---|---|---|
| **1** | `PooledChunk`; `StreamCopy`; the `BannedSymbols.txt` entries; `TestSupport/IO/`; the stream request body's exact copy and `FromStream` validation; `Response`'s drain re-home; `ExactLengthBodyWireTests` (Integration) | `IO-1`, `IO-2` (copy half), `IO-3` (copy, `FromStream`), `IO-12`, `IO-17`, `IO-22`/`IO-38` (the ban), `IO-40` (copy, ban), `HTTP-39`, `HTTP-52` | none | Breaking 1, 2. Every later PR uses `PooledChunk` or the fakes, so this lands first. `EnsureSuccessErrorMappingTests` must pass unedited |
| **2** | `CapturedBytes` | `IO-8`, `IO-19`–`IO-24`, `IO-37` | PR 1 | Independent of PRs 3–5 |
| **3** | `TeeStream` | `IO-4`, `IO-5`, `IO-6` (tee), `IO-8` (tap), `IO-25`–`IO-29`, `IO-40` (tee), `IO-41`, `IO-42` | PR 1 | Independent of PRs 2, 4, 5. **3b's request-logging wrapper waits for this PR** |
| **4** | `Utf8LineReader`, `LineTerminators`; `tests/vectors/io/utf8-lines.json` | `IO-2` (reader), `IO-6` (reader), `IO-13` (decode), `IO-14`, `IO-41`, `IO-42` | PR 1 | Independent of PRs 2, 3, 5. 7b's consumer |
| **5** | The sync body surface; `BodyMaterializer`, `BoundedBufferStream`, `BodyTooLargeException`, `DefaultMaxMaterializedBytes`; the shared private decode | `HTTP-36`, `IO-2` (bodies), `IO-9`, `IO-11`, `IO-13` | PR 1 | Breaking 3–5; the only PR with public-API lines. **3b's new variants override the sync members once this lands** (see Coupling) |
| **6** | Close-out: the N/A rows; the AOT smoke extended over the sync body members, `BodyTooLargeException` and a known-length `FromStream`; the user page `docs/sdk-documentation/io.md`; the 3a checklist; the dated design corrections; `CLAUDE.md`, READMEs; the roadmap status note | all 45 (closing) | 1–5 | The docs close the sub-phase (roadmap step 7) |

---

## Tests, vectors and ports

- **Categories.** `Unit` throughout `tests/Dexpace.Sdk.Core.Tests` (fakes only; `SEAM-2`'s partition holds — no transport
  in the core suite). `Integration` for `ExactLengthBodyWireTests` in `tests/Dexpace.Sdk.Http.SystemNet.Tests`, over the
  existing loopback fixture. `AotSmoke` for the extended smoke checks. **No `Security` class is added or edited.**
- **Test doubles** (new, `tests/Dexpace.Sdk.TestSupport/IO/`): `ChunkedReadStream` (returns at most *k* bytes per read,
  the "network reads are short" fact of §3.1, so every boundary is exercised); `StrictReadStream` (throws on a zero-count
  read, records requested counts and whether any read happened); `DisposeCountingStream`; `FailingWriteStream` (throws on
  the *n*-th write); `RecordingWriteStream` (records bytes, flushes and the token of each call).
- **Vectors.** `tests/vectors/io/utf8-lines.json`: input as escaped text or hex, mode, expected lines (with `null` as the
  terminal entry), loaded through 2a's `VectorFile`, with a top-level `"source"` naming `nodejs-sdk@c0ff3fd` and appendix C
  `IO-14`. Cases: `\n` and `\r\n` split; lone `\r` mid-line and before EOF; final unterminated line; empty lines;
  exhausted-before-any-byte; a BOM on the first and on a later line (kept, both modes); multi-byte UTF-8 and a malformed
  byte (U+FFFD); and the `Whatwg` mode's CR, LF, CRLF, and CR-then-LF across a read boundary. The table is upstreamable.
- **Ported, each with a header comment citing path and sha:** from `nodejs-sdk@c0ff3fd` `packages/core/src/io/`:
  `tee-sink.test.ts` (mirror-before-forward, limit, lifecycle and argument-validation cases; **not** the `emit`, text-write
  or `toWritableStream` cases, which are Okio-shaped), `buffered-source.text.test.ts` (the `IO-14` and decode cases into
  the vector file; **not** `readString`'s charset-label cases, which are `IO-13` API surface .NET does not have),
  `buffered-source.views.test.ts` (peek and slice cases onto `CapturedBytes`), `pump.test.ts` (`IO-17`), `limits.test.ts`
  (`IO-3`, `IO-9`). From the Ruby 3a design's testing strategy (local), its case list for the tee and the line reader,
  cross-checked against the Node files. Ruby's gem tests are not in the local clone; the plan records that, as 2a and 2b
  did.
- **Not ported:** every Okio-shaped API case (`Buffer` FIFO, `readByte`, `exhausted`, `skip`, `emit`, the stream bridges,
  the provider factories and registry), and any test that asserts a host-language fact (constraint 10).
- **NativeAOT.** PR 6 extends `AotSmoke` with the sync body members over `FromBytes` and a known-length `FromStream`,
  `ReadAsBytes`, and a `BodyTooLargeException` raised from a declared length above the cap without allocating. The internal
  helpers are reached through those public members; `AotSmoke` gets no `InternalsVisibleTo`.

---

## Coupling with 3b and later phases

### The interface 3a hands to 3b

3b is a dependency of nothing in 3a. What 3b builds on, each fixed here:

| 3b work | Uses from 3a | Contract 3b may rely on |
|---|---|---|
| Request-logging wrapper (`BODY-17`–`BODY-21`, `BODY-37`) | `TeeStream` | Construct one per write with `leaveOpen: true` and the shared preview cap (that gives `BODY-18`'s per-write reset for free); mirror-before-forward; `SnapshotTap` after a failed write holds the attempted bytes; flush and dispose reach the primary only |
| Response-logging wrapper (`BODY-22`–`BODY-29`) | `StreamCopy.DrainUpToAsync`, `CapturedBytes` | The drain appends into a caller-owned `ArrayBufferWriter<byte>`, so bytes read before a failure survive the throw (`BODY-26`); it returns whether end of stream was seen before the cap, and leaves "probe for one more byte" to the caller (`BODY-23` versus `BODY-24`); fits-cap reads are `CapturedBytes.OpenRead()` views (`BODY-23`) |
| Capped snapshot and preview (`BODY-32`, `BODY-33`) | `CapturedBytes.Slice(0, cap).ToArray()` | Negative caps rejected; 3b clamps to `Array.MaxLength` itself, as `BODY-32` says |
| File body (`BODY-11`–`BODY-13`, `HTTP-40`) | `StreamCopy.CopyExactly(Async)`, `ShortTransferMessage` | One message form for `HTTP-39`, `BODY-10` and `BODY-13` |
| Materialise-once (`BODY-3`, `HTTP-37`) | `RequestBody.ToReplayable(Async)` as rebuilt here | Bounded by `Array.MaxLength`, fail-fast on a known length (P3a-12) |
| Every new body variant (file, form, multipart, seekable stream, both logging wrappers) | The sync surface (P3a-5) | Override **both** `WriteTo` and `WriteToAsync` (and `OpenRead` with `OpenReadAsync` on the response side), sharing one consume latch across forms. If a 3b variant lands before 3a's PR 5, PR 5 adds its sync override; if after, 3b's PR does |
| BOM strip in `ReadAsStringAsync` (§11 item 34) | The shared private decode | One edit covers both forms |
| `BODY-14`/`HTTP-41` rows | Position B (P3a-3) | Cite it; the reopen verdict is not re-argued |
| `BODY-8` and `body-stream-ownership` | P3a-4 (helper half) | 3b argues the body half and must keep the helper default |
| `BODY-10` row | 3a's `HTTP-39` tests | 3b's row cites them |
| Dispose latches (`HTTP-41`, `HTTP-43`, `BODY-15`, `SEAM-14`) and `Disposal.DisposeQuietly` | the `Interlocked` latch pattern of `TeeStream`/`Utf8LineReader` | Same mechanism (§3.7); 3a builds no public latch |

### Later phases

- **4c** builds `ErrorBodyBuffer` and `ErrorMappingPolicy` over `StreamCopy.DrainUpTo(Async)` (`HTTP-52`'s ⏳ half,
  `RECOV-16`, `RETRY-36`), adds `Response`'s sync `EnsureSuccess` for the real sync `Process`, and uses `WriteTo` and
  `ToReplayable` on that path.
- **5a** makes `DefaultMaxMaterializedBytes` configurable through the options surface (design §3.1, §8.2), and owns the
  SSE line cap's configuration (with 7b).
- **5b** consumes 3b's wrappers; nothing of 3a directly.
- **6a** decides retry's automatic materialisation cap; it calls `ToReplayable(Async)`, which refuses only above
  `Array.MaxLength` (P3a-12).
- **7a** adds the sync serde readers (`ReadValue` over `OpenRead`, `HttpResponseException.GetError<T>`).
- **7b** builds the SSE parser on `Utf8LineReader` in `Whatwg` mode: it strips the leading BOM, maps
  `InvalidDataException` to its public failure (§10 entry 20, `SSE-19`), and turns on the `SSE-37` architecture gate.
- **8a**'s kit checks `HTTP-39` per transport (a declared length that disagrees with the source).
- **8b** overrides `RequestBodyContent.SerializeToStream` to call `WriteTo` (fact 2) and gives `HttpResponseMessageBody` an
  `OpenRead` over `HttpContent.ReadAsStream`, flipping the body's latch first (position B, fact 13). Until then the sync
  members of a transport-produced body throw `NotSupportedException`, which is consistent with `SystemNetHttpClient.Execute`
  still being sync-over-async (coupling obligation 5).

---

## Design corrections owed at close-out

Dated corrections, in PR 6, each naming the ruling that caused it:

- **§3.1**: the materialisation cap applies to the response-side readers, and the request side is bounded by
  `Array.MaxLength` only (P3a-12); the sync members are virtual with a `NotSupportedException` default (P3a-5);
  "lazy offset overflow falls out" is corrected to "needs a clamp" (fact 3); the never-pooled rule is mechanised (P3a-8);
  the **As built** line.
- **§10 entry 6**: the reopen verdict covers both forms (P3a-3). **§10 entry 4** gains the topic label `eof-is-zero-read`.
- **§11**: a new item for the `IO-10`/`IO-21` windowing reading (P3a-9) and one for the line reader's U+FFFD decoding
  (P3a-11), numbered at the next free number when they land (3b may add items concurrently).
- **§12**: the `IO` row's note that `IO-7` "is not argued by ID" is closed (position C).
- **Roadmap**: the phase 3 row's `sdk-design refs` cell gains this design's link (appended, never replacing), and a dated
  status note.

---

## Rulings

Taken by this design in the absence of a human reviewer. Each lists the options considered; three are **open for the
lead**.

1. **P3a-1 — The 3a/3b row split. Open for the lead.** 3a holds 45 rows: `IO-1`–`IO-42`, `HTTP-36`, `HTTP-39`,
   `HTTP-52`. 3b holds 46: `BODY-1`–`BODY-37` and `HTTP-37`, `HTTP-38`, `HTTP-40`–`HTTP-45`, `HTTP-51`, with
   `HTTP-44`/`HTTP-45` ⏳ 7a in 3b's checklist (response side). `BODY-10` stays in 3b's prefix and cites 3a's `HTTP-39`
   tests. Options: Ruby's split (all 12 `HTTP` IDs to 3b, `IO` only to 3a) — rejected because the card puts the
   exact-length copy and the sync surface in 3a, and a row should sit with the phase that builds its evidence;
   moving `BODY-10` into 3a too — rejected to keep every `BODY` row in one checklist and avoid a collision with 3b's
   concurrent design. No phase 3 segmentation design exists; this ruling and the card stand in for it.

   **Observed conflict, 2026-10-02.** 3b's concurrent design (`docs/superpowers/specs/2026-10-02-phase3b-bodies-design.md`,
   read only for this check) claims all 49 of `BODY-1`–`BODY-37` and the 12 `HTTP` IDs, with `HTTP-36` and `HTTP-52`
   "already met" and `HTTP-39`/`BODY-10` "⏳ 3a". So `HTTP-36`, `HTTP-39` and `HTTP-52` are claimed twice. Nothing else
   conflicts: 3b's assumed 3a interface (sync and async `CopyExactly` naming delivered-of-total, the base-type `WriteTo`
   and `OpenRead` "abstract or virtual … 3a's call", a capped `ReadAsBytesAsync`, a decode routine shared with the sync
   reader) matches what this design ships. **Recommended reconciliation:** `HTTP-39` is 3a's row (3a is its only
   builder, and a ⏳ row in 3b would point at 3a's plan anyway); `HTTP-36` and `HTTP-52` go to 3b's checklist, where 3b
   already marks them met, citing 3a's tests for the sync twin and the drain re-home. That gives 3a 43 rows and 3b 48,
   moves one row out of 3b's design, and changes no decision in either document: the `HTTP-36` and `HTTP-52` rows above
   stay as the record of 3a's work on them. The lead decides.
2. **P3a-2 — Who argues the two §3.1 verdicts.** 3a argues `response-body-reopen-throws`, because 3a's `OpenRead` forces
   the answer; 3b argues `body-stream-ownership` (`BODY-8`), with 3a fixing only `IO-6`'s helper half (position A).
3. **P3a-3 — Reopen throws, in both forms.** One consume latch per response variant, shared by `OpenRead` and
   `OpenReadAsync`; a second open in either form throws `StreamConsumedException`. Options: same handle (the letter;
   fact 13 shows the interleave hazard), mixed per form (rejected). Confirms §10 entry 6, extended (position B).
4. **P3a-4 — Helper-layer ownership (`IO-6`).** Internal wrappers take ownership by default with a `leaveOpen` opt-out
   (the BCL convention, fact 12); their dispose is latched so the owned stream is disposed at most once (fact 11,
   `IO-41`). `CapturedBytes` owns no external resource.
5. **P3a-5 — The sync body surface. Open for the lead.** Every async body member gains a sync twin (`WriteTo`,
   `ToReplayable`, `OpenRead`, `ReadAsBytes`, `ReadAsString`), `virtual`, with `WriteTo`/`OpenRead` throwing
   `NotSupportedException` naming the subclass by default (fact 2's `HttpContent` precedent) and every SDK variant
   overriding them. Options: `abstract` (compile-time guarantee; breaks every subclass, including a `Security` class and
   the adapter ahead of 8b), a sync-over-async default (banned; defeats the real sync path). The lead may prefer the
   compile-time guarantee; the cost is listed in position D.
6. **P3a-6 — Exact-length semantics.** A known-length stream body writes exactly its length, throws `EndOfStreamException`
   naming delivered-of-total when short, never reads past its length, issues no read for 0; `FromStream` rejects lengths
   below −1 and (request side) a non-readable stream. One message helper serves `HTTP-39`, `BODY-10` and `BODY-13`
   (position F).
7. **P3a-7 — `IO-2` moves to the caller.** Core never issues a zero-count read; a strict test stream checks every helper.
8. **P3a-8 — Captured bytes are never pooled, and the build says so.** `ArrayPool<T>.Rent` and `MemoryPool<T>.Rent` are
   banned in `src/` (`RS0030`); the one sanctioned rent is the internal `PooledChunk` behind a scoped pragma. This is the
   evidence for `IO-22`'s second half and `IO-38` (position H).
9. **P3a-9 — No buffer type; one windowing operation.** `IO-7` and `IO-10` are N/A; `CapturedBytes.Slice` follows
   `IO-21`'s lazy semantics. A new design §11 item (position C).
10. **P3a-10 — `TeeStream`.** Write-only; mirror then forward; tap limit default unbounded, clamped to `Array.MaxLength`,
    0 mirrors nothing; flush and dispose reach the primary only; after dispose writes throw and the snapshot still works;
    both async write overloads overridden (fact 1); the caller's token forwarded unchanged (`IO-40`); no staging buffer.
11. **P3a-11 — `Utf8LineReader`.** Two modes (`IO-14`'s, and `Whatwg` for 7b, with a deferred-LF skip so a CR-terminated
    line is never held for the next chunk); a mandatory line cap (default 1 MiB) that throws `InvalidDataException` and
    latches the reader failed; UTF-8 with U+FFFD for malformed bytes; a BOM is content; linear scan (position G).
12. **P3a-12 — The materialisation cap. Open for the lead.** The response-side readers refuse more than
    `ResponseBody.DefaultMaxMaterializedBytes` (64 MiB) with a new public `BodyTooLargeException`, refusing a declared
    length before reading and probing one byte at the cap for an unknown length; `ToReplayable(Async)` is bounded only by
    `Array.MaxLength`; no public knob until 5a. Options and the departure from §3.1's wording are in position E. This is a
    behaviour change to `ReadAsBytesAsync` and `ReadAsStringAsync` that a caller downloading more than 64 MiB will feel.
13. **P3a-13 — `HTTP-52`'s drain is re-homed, not redesigned.** `Response` drains through `StreamCopy.DrainUpToAsync` with
    a pooled chunk; `EnsureSuccessErrorMappingTests` stays unedited; `ErrorBodyBuffer` and `ErrorMappingPolicy` stay 4c's.
14. **P3a-14 — Three new tripwires in `BannedSymbols.txt`:** `Stream.set_ReadTimeout`/`set_WriteTimeout` (`IO-40`);
    `TextReader.ReadLine`/`ReadLineAsync` and their `StreamReader`/`StringReader` overrides (`IO-14`, fact 12); and
    P3a-8's pool entries. Each message cites its design section, as the existing entries do.
15. **P3a-15 — Placement.** The helpers are `internal`, under `src/Dexpace.Sdk.Core/IO/`, namespace
    `Dexpace.Sdk.Core.IO`; no public I/O vocabulary ships (§3.1, P14). `CLAUDE.md`'s layout tree gains the folder.

---

## Deviation Ledger

| ID | Decision | Touches | Kind | Argued in | Route |
|---|---|---|---|---|---|
| P3a-3 | A second open of a response body throws in either form, across forms, instead of returning the same handle | `BODY-14`, `HTTP-41` | mechanism: extends the existing §10 entry 6 to the new sync form | [Position B](#b-response-body-reopen-throws-confirmed-and-extended-across-the-two-forms) | Design §10 entry 6, dated correction (PR 6) |
| P3a-5 | The sync body members are virtual and throw `NotSupportedException` by default; only the SDK's own variants are guaranteed to support the sync path | `HTTP-36`, `SEAM-11`'s sync path | design-level: §3.1 names the sync members without fixing their dispatch | [Position D](#d-the-sync-surface-virtual-with-a-loud-default-not-abstract) | Design §3.1, dated correction (PR 6) |
| P3a-8 | `IO-22`'s parent-close invalidation and `IO-38` are met by construction (no capture is pooled), enforced by `RS0030` | `IO-22`, `IO-38` | mechanism: §3.1's rule, mechanised | [Position H](#h-captured-bytes-are-never-pooled-and-the-build-enforces-it) | Design §3.1, dated correction (PR 6) |
| P3a-9 | `IO-7` and `IO-10` are N/A; the one windowing operation follows `IO-21`, not `IO-10`'s eager rejection | `IO-7`, `IO-10`, `IO-21` | a reading: two clauses on two reference operations, one .NET operation | [Position C](#c-io-7-and-io-10-one-windowing-operation-and-no-buffer-type) | New design §11 item; §12 `IO` row, dated correction (PR 6) |
| P3a-11 | The line reader decodes malformed UTF-8 to U+FFFD and keeps a BOM as content | `IO-14`, `IO-13` | a reading: `IO-14` says only "decoded as UTF-8" | [Position G](#g-the-line-readers-two-modes-cap-and-decoding) | New design §11 item (PR 6) |
| P3a-12 | The 64 MiB materialisation cap applies to response-side readers only; request-side materialisation is bounded by `Array.MaxLength`; no public knob until 5a | `IO-9` (a SHOULD, met), `BODY-3` | design-level: departs from §3.1's "applies first" for the request side | [Position E](#e-the-materialisation-cap-response-side-only-fixed-until-5a) | Design §3.1, dated correction (PR 6) |

No ruling leaves a MUST's letter unmet on a stated domain beyond what §10 entries 3 and 4 already record, so no new §10
entry is opened; P3a-3 amends entry 6.
