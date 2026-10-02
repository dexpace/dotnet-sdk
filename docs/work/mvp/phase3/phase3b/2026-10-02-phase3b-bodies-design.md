# Phase 3b — Bodies: Design

**Status:** Draft, for review. Written 2026-10-02 against `main` at `313188f` (phase 2b merged). Brainstormed without a
human in the loop: every judgement call the brainstorming skill would have put to the lead is taken here as a numbered
ruling (`P3b-n`) with the options and the rationale. The three that needed the lead's sign-off (P3b-3, P3b-5, P3b-10)
were **accepted as written by the lead's ruling of 2026-10-02**. The scope authority is the roadmap's Phase 3 card and Phase List row 3
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`), which put 3a (I/O) before 3b (bodies) as a
**dependency**. The format follows the phase 2a and 2b designs (`docs/work/mvp/phase2/phase2a/2026-09-29-phase2a-domain-model-design.md`,
`docs/work/mvp/phase2/phase2b/2026-09-30-phase2b-seams-design.md`).

**What this document is.** The sub-phase design for 3b: one explicit disposition per requirement row (48 rows:
`BODY-1`–`BODY-37` and `HTTP-36`–`HTTP-38`, `HTTP-40`–`HTTP-45`, `HTTP-51`, `HTTP-52`; `HTTP-39` is 3a's, by the lead's
ruling of 2026-10-02 on P3a-1), the public shape of every body type 3b adds or
changes, the internal shape of the two logging wrappers and of `Disposal`, the interface 3b assumes from 3a, argued
positions and rulings, the breaking-changes list, the test strategy and a landing order in pull-request-sized steps.

**What this document is not.** It is not the plan (numbered TDD tasks) and not the checklist. It does not restate
design §3.1's body contract, §3.7's lifecycle rule or §4's construction pattern (roadmap constraint 9); it cites them,
records the decision against each row, and argues only what the design leaves open or what verification on the pinned
runtime changed. It does not design 3a's scope (the exact-length copy, the line reader, `TeeStream`, the
materialisation cap, the synchronous surface); it states what it assumes of it.

---

## Prerequisites

| Predecessor | Kind | State at `313188f` |
|---|---|---|
| Phase 0 gates | **dependency** | Met. `RS0016`/`RS0017`, `RS0030` (`BannedSymbols.txt`), `MA0051` (70 lines), `CA2007` in `src/`, `IsAotCompatible`, the dependency audit. |
| Phase 1, S8 and S9 | **convenience** | Met, with clauses routed here: S9's "the dispose latches on `Response` and the body" is 3b's (phase 1 checklist, row S9). S8 built `EnsureSuccessAsync`'s capped replayable error body, which 3b pins and does not move (4c owns the policy). |
| 2a | **convenience** (inherited contracts) | Met. 3b inherits `RequestBody`'s equality contract (`HTTP-46`, design §11 item 39: value for construction-time bytes, identity for live sources), `HttpHeaderSyntax`, the validating `MediaType`, the non-null empty response body (§10 entry 30). |
| 2b | **convenience** (hand-offs) | Met. 3b owes 2b: `Disposal.DisposeQuietly` replaces `AsAsync`'s direct `Dispose()` and its pin test `SyncToAsyncBridgeTests.A_throwing_dispose_after_cancellation_faults_the_task_with_that_exception`; the dispose latches close `SEAM-14`'s idempotence clause (2b checklist row `SEAM-14`, ⏳ 3b). |
| **3a** | **dependency** for PRs 2–6 | Designed concurrently ([3a design](../phase3a/2026-10-02-phase3a-io-design.md)). See [the 3a interface](#the-interface-assumed-from-3a). PR 1 (latches, `Disposal`, BOM, `BODY-16`) and the form body have no edge into 3a. **The shared decode is two-way:** whichever of 3a's PR 5 and 3b's PR 1 lands second converges both string readers on the one routine with the preamble strip (P3b-12), and the other PR's BOM and decode tests, sync and async, must pass over it. |

### The interface assumed from 3a

3b is written against the roadmap's 3a list. Each item is stated as the **contract** 3b relies on; the names are
placeholders, and **3a's names win** wherever they differ (the plan re-derives them from 3a's merged code). If 3a
declines an item, the row that consumes it here reopens.

| 3a item (roadmap) | Contract 3b relies on | Consumed by |
|---|---|---|
| The shared exact-length copy (`BODY-10`, `HTTP-39`) | `internal` sync and async `CopyExactly(Stream source, Stream destination, long count, CancellationToken)`: writes exactly `count` bytes, never issues a zero-count read, and throws `EndOfStreamException` whose message names delivered-of-total when the source ends early; `count == 0` is a legitimate empty write. 3a also wires it into the existing single-use `StreamRequestBody` when a length is declared. | file body (`BODY-13`), seekable body (`BODY-9`) |
| `TeeStream` (`IO-25`–`IO-29`) and its tap | An `internal` write-only `Stream` over a primary and a capped tap: mirrors each chunk into the tap **before** forwarding, forwards every byte whatever the cap, routes `Flush`/`Dispose` to the primary only (and leaves the primary open on request); the tap offers clear, a copying snapshot, a byte count and no writable handle (`IO-28`). | request-logging wrapper (`BODY-17`–`BODY-21`, `BODY-37`) |
| The materialisation cap | `ToReplayableAsync` and `ReadAsBytesAsync` refuse to materialise above the configured cap (default 64 MiB, design §3.1) and above `Array.MaxLength`, with a message pointing at streaming. | every new body inherits it through the base members |
| Synchronous `WriteTo` / `OpenRead` | `RequestBody.WriteTo(Stream, CancellationToken)` and `ResponseBody.OpenRead(CancellationToken)` (and any synchronous reader 3a adds beside them), on the base types. Whether abstract or virtual is 3a's call. | every new variant implements both shapes; the response wrapper's drain has a synchronous path |
| The helper-layer ownership rule (`IO-6`) | Internal helpers take ownership unless told `leaveOpen`, as the BCL does. | the wrappers construct helpers with `leaveOpen: true` over streams they do not own |

**3b's hand-offs back to 3a: none.** 3b consumes; it changes no 3a contract. Two files are edited by both:
`RequestBody.cs` (3a wires the exact copy into `StreamRequestBody` and adds `WriteTo`; 3b adds the factories and the
seekable variant) and `ResponseBody.cs` (3a caps `ReadAsBytesAsync` and adds `OpenRead`; 3b rewrites disposal, the
readers' `finally` and the decode). 3a lands first; whichever 3b PR follows re-derives its change on 3a's merged code
rather than resolving hunks (the 2a/2b precedent).

**One decode routine, in either order.** 3a's PR 5 adds a synchronous `ReadAsString` beside `ReadAsStringAsync`; 3b's PR 1
builds `TextDecoding` (P3b-12). Exactly one routine survives: whichever of the two PRs lands second converges **both**
readers on `TextDecoding` with the preamble strip (if 3b's PR 1 is second, it replaces 3a's private decode; if 3a's PR 5 is
second, its sync reader calls `TextDecoding` and adds none). The PR that lands second runs the other's tests over the one
routine unchanged: 3a's `IO-13` decode and round-trip tests and 3b's BOM cases, each in both the sync and the async form.

---

## Governing documents, and the phase-start queries

- **Normative.** Each row's appendix-C text (`docs/product-spec/appendix-c-consolidated-normative-requirement-index.md`),
  with chapter 06 (`docs/product-spec/06-request-and-response-body-lifecycle.md`) for the conformance clauses and
  chapter 05 for the tee rows 3b consumes.
- **Design.** §3.1 (the body contract, the variants, the two wrappers, the encoding boundary), §3.7 (latches,
  ownership, `DisposeQuietly`), §4.5 (construction), §5.1–§5.2 (error-body buffering, the fatal filter,
  `ExceptionTrail`), §10 entries 4, 5, 6, 13, 30, §11 items 7, 12, 34, 39, §12's `BODY`/`HTTP` rows (which list
  `BODY-33` as unaddressed and `BODY-36` as deferred).
- **Styleguide.** `docs/styleguide/csharp/13-resource-management.md` (13.1 and 13.5 the dispose interfaces, 13.3 no
  finalizer without an unmanaged handle, 13.4 never dispose an injected dependency), `08-error-handling.md`,
  `09-concurrency.md` (9.1, no blocking on async), `10-api-design.md` (10.1 minimal surface).
- **Siblings, read locally.** `nodejs-sdk@c0ff3fd` `packages/core/src/body/*` and `packages/body-file/src/*` (the
  case lists below). The Ruby repository holds documents only (2a/2b status notes), so nothing executable is ported
  from it.

| Query, run 2026-10-02 | Result |
|---|---|
| `scripts/knowledge --origin note --brief` | 5 notes, all phase-0 styleguide conflicts (`CA1062`, `I` prefix, `Async` suffix, xUnit `Assert`, `LangVersion`); none touches bodies |
| `scripts/knowledge --section conflicts --brief` | 15 entries; none touches bodies, I/O or disposal beyond `CA2007` (conformed) |
| `scripts/knowledge --prefix-info BODY` | 37 IDs: 31 MUST, 5 SHOULD, 1 MAY; 37 substantive |
| `scripts/knowledge --prefix-info HTTP` | 53 IDs (12 in scope here); 53 substantive |
| `scripts/knowledge --prefix-info IO` | 42 IDs, 4 uncited (3a's) |
| `scripts/knowledge --gaps BODY,IO,HTTP` | only `IO-32`–`IO-35` (3a's; the roadmap's older table also listed `BODY-6`, `BODY-7`, which the corpus now covers — both were read from appendix C regardless) |

**Scope.** 48 rows, by the lead's ruling of 2026-10-02 on P3a-1: `HTTP-39` is 3a's row (3a holds 43; 43 + 48 = 91, the
roadmap's count), and `BODY-10` stays here as ⏳ 3a. `HTTP-36` and `HTTP-52` are 3b's rows and cite 3a's tests for the
sync twin and the drain re-home. `HTTP-44`/`HTTP-45` are dispositioned here and built in 7a (Phase List row 3). Every `IO`
row is 3a's.

---

## Verified facts that shape the decisions

Each was verified on 2026-10-02 on the pinned SDK (10.0.401, Linux) with a throwaway file-based program in the
scratchpad, never in the repository.

1. **The shared framework cannot tell a regular file from a FIFO or a device on Unix.** `new FileInfo(p).Attributes`
   is `Normal` and `File.Exists` is `true` for a regular file, for `/dev/null`, `/dev/zero` and a FIFO; `Length` is 0
   for all three special files. A directory reports `Exists == false` and `Attributes == Directory`.
2. **Opening a FIFO for reading blocks.** `new FileStream(fifo, FileMode.Open, FileAccess.Read)` did not return in
   2 s with no writer. `/dev/null` and `/dev/zero` open, report `CanSeek == true` and `Length == 0`. Opening a
   directory as a `FileStream` throws `UnauthorizedAccessException`.
3. **`FileInfo.Length` on a symbolic link is the link's own size.** For `link.txt -> reg.txt` (6 bytes),
   `new FileInfo("link.txt").Length` is 7 (the target path's length) and `Attributes` is `ReparsePoint`;
   `ResolveLinkTarget(returnFinalTarget: true)` returns the target, whose `Length` is 6. A `FileStream` opened on the
   link reads the target (length 6).
4. **A file that shrinks after open is a short read, not silent truncation.** Opened with
   `FileShare.ReadWrite | FileShare.Delete`, a 100-byte file rewritten to 10 bytes makes `ReadExactly(buf, 0, 100)`
   throw `EndOfStreamException`.
5. **Decoding keeps a BOM for every encoding, not only UTF-8.** `Encoding.UTF8.GetString(EF BB BF 68 69)` has 3
   characters; `Encoding.GetEncoding("utf-16").GetString(FF FE 68 00)` has 2 (U+FEFF, `h`). Preambles:
   UTF-8 `EFBBBF`, `utf-16` `FFFE`, `utf-16be` `FEFF`, UTF-32 `FFFE0000`, Latin-1 empty.
6. **Three encoders disagree on `a b*-._~!'()é`.** WHATWG's `application/x-www-form-urlencoded` serializer gives
   `a+b*-._%7E%21%27%28%29%C3%A9`; `WebUtility.UrlEncode` gives `a+b*-._%7E!%27()%C3%A9` (leaves `!()`);
   `Uri.EscapeDataString` gives `a%20b%2A-._~%21%27%28%29%C3%A9` (RFC 3986). None of the built-ins is the WHATWG
   serializer, as design §3.5 says. `Encoding.UTF8.GetBytes("\uD800")` is `EFBFBD` (silent U+FFFD); a strict
   `UTF8Encoding(false, true)` throws `EncoderFallbackException`.
7. **`Activity.AddException(ex, tags)`** records an event named `exception` carrying the caller's tags plus
   `exception.type`, `exception.message` and `exception.stacktrace`; with no listener, `StartActivity` returns `null`
   and there is no current activity to record on.
8. `RandomNumberGenerator.GetString(ReadOnlySpan<char>, int)` exists in-box (the boundary generator needs nothing
   else), and `MemoryStream.Dispose` twice does not throw.

---

## Decisions, one per requirement row

**How to read the table.** **Exit** uses the roadmap's constraint-3 legend; a combined mark gives each clause its own
mark. **Tests** are `[Trait("Category", "Unit")]` unless another category is named; core test classes go under
`tests/Dexpace.Sdk.Core.Tests/Http/{Request,Response}/` (namespaces `…Tests.Http.Requests` / `…Responses`, the 2a
precedent) and `…/Internal/` for `Disposal`. A **pin** adds a test for behaviour that is already correct. **PR**
points to [the landing order](#landing-order).

| ID | Level | Decision | Rationale / source | Types affected | Test approach | PR | Exit |
|---|---|---|---|---|---|---|---|
| `HTTP-36` | MUST | **Already met**, extended to every new variant: one write operation, nullable `ContentType`, `ContentLength` with −1, `IsReplayable` defaulting to `false`. The synchronous twin `WriteTo`/`ToReplayable` is 3a's work (P3a-5), cited here; every 3b variant overrides both forms. | §3.1, §4.5; 3a's P3a-5 | `RequestBody` and every new variant | Cite 3a's sync twin tests (3a's `RequestBodyContractTests`, 3a plan task 5.2: the un-overridden `WriteTo` default, sync/async byte parity, the shared consume guard across forms). Pin per 3b variant in `RequestBodyContractTests`: a `[Theory]` over every factory asserting the triple and, for replayable ones, byte-identical double writes in both forms. | 2–4 | ✅ |
| `HTTP-37` | MUST | **Already met** (`Interlocked.Exchange` guard; base `ToReplayableAsync` drains once and returns a bytes body). 3b adds pins and the multipart composite guard (P3b-8). | §3.1; §11 item 7 | `RequestBody`, multipart | `SingleUseBodyTests`: second write throws `StreamConsumedException`; N concurrent writers, exactly one proceeds; `ToReplayableAsync` on a single-use body leaves the original consumed. | 2 | ✅ |
| `HTTP-38` | MUST | **Build.** Classification by source: bytes, string, value, form, file → replayable; multipart → conjunction; stream → single-use unless seekable with a known length (P3b-4). `FromForm` uses the WHATWG serializer, `+` for space (P3b-7). | §3.1, §3.5 (form encoder) | `FromForm`, `FromFile`, `Multipart`, `FromStream` | `FormBodyTests` over the vector table (fact 6), `RequestBodyContractTests`' classification theory. | 2–4 | ✅ |
| `HTTP-40` | MUST | **Build** `FileRequestBody` with fail-fast validation (exists, not a directory, offset and count within the size captured at construction), a fresh handle per write, `ContentLength` the exact count. "Regular file" has a platform limit on Unix (P3b-5, accepted by the lead's ruling of 2026-10-02 and routed as a new design §10 entry). | §3.1 file-backed bodies; facts 1–4 | `FileRequestBody`, `RequestBody.FromFile` | `FileRequestBodyTests` (port of Node's `body-file` case list, below). | 3 | ✅, reading P3b-5 |
| `HTTP-41` | MUST | **Build** the idempotent close (the latch, P3b-2) and the readers' `finally` close (`BODY-16`). Single use is already met; the "same handle" clause keeps §10 entry 6, as 3a argues it (P3a-3; position B here). | §3.1, §3.7; §10 entry 6 | `ResponseBody`, `Response` | `ResponseBodyLifecycleTests`. | 1 | ✅, with §10 entry 6 |
| `HTTP-42` | MUST | **Already met** for the charset fallback (2a fixed `utf-7`); **build** the BOM strip (P3b-12), in one decode routine shared with 3a's synchronous reader. | §3.1 encoding; §11 item 34; fact 5 | `ResponseBody.ReadAsStringAsync` | `ResponseBodyDecodeTests`: UTF-8 BOM stripped; UTF-16 LE BOM stripped under `charset=utf-16`; a BOM not matching the resolved charset kept; no charset → UTF-8; unknown charset → UTF-8 (pin). | 1 | ✅ |
| `HTTP-43` | MUST | **Build** the `Response` latch: `Dispose`/`DisposeAsync` share one `Interlocked` flag and forward to the (also latched) body. | §3.7 | `Response` | `ResponseDisposeLatchTests`: double dispose releases once (sync, async, mixed); a throwing body propagates once and the second call is a no-op; a never-read body is released. | 1 | ✅ |
| `HTTP-44` | MUST | **Hand-off to 7a** (the lazy typed-response wrapper). | Phase List row 3; Phase 7 card | — | None in 3b. | — | ⏳ 7a (Phase 7 card, "the lazy typed-response wrapper") |
| `HTTP-45` | MUST | **Hand-off to 7a**, as `HTTP-44`. | as above | — | None. | — | ⏳ 7a |
| `HTTP-51` | SHOULD (embedded MUST) | **Build** the multipart body: one framing routine computes each part header once at construction, used for both the length and the write; replayable/length as conjunctions; a random spec-valid boundary; a caller boundary validated against RFC 2046; part-header values that cannot break the framing (P3b-8). | §3.1 composite bodies | `RequestBody.Multipart`, `MultipartPart` | `MultipartBodyTests` (port of Node's case list, below). | 4 | ✅ |
| `HTTP-52` | MUST | **Already met** by phase 1 S8 (`EnsureSuccessAsync`: capped at 1 MiB, replayable copy, drained inside the dispose scope). 3a re-homes the drain onto `StreamCopy.DrainUpToAsync` (3a's P3a-13), behaviour unchanged; 3b re-runs the `Security` tests over the new latch; the policy form is 4c's. | phase 1 checklist S8; §5.1; 3a's P3a-13 | `Response.EnsureSuccessAsync` (3a's re-home) | Cite 3a's re-home (P3a-13; 3a plan tasks 1.2 and 1.5, `StreamCopyTests.DrainUpTo_stops_at_the_cap_and_reports_whether_the_end_was_seen`) and `EnsureSuccessErrorMappingTests` (`Security`), which the re-home leaves unedited; 3b's only touch is P3b-2's mechanical `TrackingBody` rewrite (see Migration), which changes no assertion. | 1 | ✅ ⏳ 4c (`ErrorMappingPolicy`, `ErrorBodyBuffer`) |
| `BODY-1` | MUST | As `HTTP-36`. | §3.1 | all variants | `RequestBodyContractTests`. | 2–4 | ✅ |
| `BODY-2` | MUST | **Build**: multipart `IsReplayable` is the conjunction over parts; `ContentLength` is −1 if any part's is. | §3.1 | multipart | `MultipartBodyTests`. | 4 | ✅ |
| `BODY-3` | MUST | **Already met** (base `ToReplayableAsync`); every new replayable variant returns `this`; the logging wrapper re-wraps (`BODY-21`). | §3.1; §11 item 7 | base, variants | `SingleUseBodyTests`, `RequestBodyContractTests` (`ToReplayableAsync` returns the same instance for replayable bodies). | 2 | ✅ |
| `BODY-4` | MUST | **Hand-off** to the three gates' owners: retry (6a), redirect's loud failure (6b, the `REDIR-15` error), auth replay (6c). 3b's share is that every variant reports `IsReplayable` truthfully, which `RequestBodyContractTests` proves. | §3.1 replay gate; §6 | `RetryPolicy`, `RedirectPolicy`, auth | None in 3b beyond the property. | — | ⏳ 6a, 6b, 6c (Phase 6 card) |
| `BODY-5` | MUST | **Hand-off to 6a** (the retry-only idempotency gate). | §3.1; §6.1 | `RetryPolicy` | None. | — | ⏳ 6a |
| `BODY-6` | MUST | **Already met** for `FromStream`; pinned. The seekable variant is replayable, so the clause does not reach it; a non-replayable multipart throws before writing a byte (P3b-8). | §3.1 | stream, multipart | `SingleUseBodyTests`, `MultipartBodyTests.A_single_use_composite_refuses_a_second_write_before_any_byte`. | 2, 4 | ✅ |
| `BODY-7` | MUST | **Already met** (`Interlocked`); pinned under real concurrency. | §3.1; §11 item 7 | stream, multipart | `SingleUseBodyTests.Concurrent_writes_admit_exactly_one` (Barrier-started, N = 16). | 2 | ✅ |
| `BODY-8` | MUST | **Verdict: keep design §10 entry 5** (position A): a request body closes exactly what it opened. `FromStream` (single-use and seekable) never disposes the caller's stream; `FromFile` opens and disposes its own handle per write; multipart disposes nothing it was given. | §3.1; §10 entry 5; §11 item 12 | stream, seekable, file, multipart | `RequestBodyOwnershipTests`: the caller's stream is open after a write, a failed write and a `ToReplayableAsync`; the file handle is released after each write and after a failed write (the test reopens the file with `FileShare.None` immediately afterwards). | 2, 3 | ✅, with §10 entry 5 |
| `BODY-9` | SHOULD | **Build** the seekable variant (P3b-4): `FromStream` over a readable, seekable stream with a declared length in `[0, Array.MaxLength]` is replayable; each write seeks to the position captured at construction under an in-flight latch, so at most one rewind happens between two writes; otherwise single-use. | §3.1 seekable streams | `RequestBody.FromStream`, private `SeekableStreamRequestBody` | `SeekableStreamBodyTests`. | 2 | ✅ |
| `BODY-10` | MUST | **Hand-off to 3a**: the exact-length copy is 3a's row `HTTP-39` (by the lead's ruling of 2026-10-02 on P3a-1). 3b consumes it in the file and seekable bodies and proves the consumption. | Phase 3 card; 3a's P3a-6 | — | Cite 3a's `HTTP-39` tests (`StreamCopyTests`, `ExactLengthBodyWireTests`). 3b's consumer tests: `FileRequestBodyTests.A_file_that_shrinks_after_construction_fails_naming_transferred_of_total`, `SeekableStreamBodyTests.A_short_seekable_source_fails`. | — | ⏳ 3a (its plan's exact-copy task) |
| `BODY-11` | MUST | As `HTTP-40`, plus count's rest-of-file sentinel (−1). | §3.1 | `FileRequestBody` | `FileRequestBodyTests`. | 3 | ✅, reading P3b-5 |
| `BODY-12` | SHOULD | **Recognisable by type: build** (`FileRequestBody` is public sealed and exposes `FilePath`, `Offset`, `ContentLength`). **Kernel zero-copy: not built**: `SocketsHttpHandler` has no file-to-socket path for request content (§3.1), so the transfer is a large-buffer `FileStream` copy with the stream's own buffering off (`bufferSize: 0`), stated, never claimed as zero-copy. | §3.1 | `FileRequestBody` | `FileRequestBodyTests.Is_recognisable_by_type_and_exposes_its_range`. | 3 | ✅ 🚫 (kernel path: §3.1) |
| `BODY-13` | MUST | **Build** through 3a's exact copy, so the message form is shared with `BODY-10` (§3.1). | §3.1 | `FileRequestBody` | `FileRequestBodyTests.A_file_that_shrinks_after_construction_fails_naming_transferred_of_total` (fact 4). | 3 | ✅ |
| `BODY-14` | MUST | **Verdict argued by 3a** (P3a-3, the lead's ruling of 2026-10-02): design §10 entry 6 stands, across `OpenRead` and `OpenReadAsync`. 3b extends it to its new variants (position B): a second open throws `StreamConsumedException`; repeatable access needs the explicit buffering wrapper (the response-logging wrapper's fits-cap regime, or `FromReplayableBytes` for error bodies). | §3.1; §10 entry 6; 3a's P3a-3 | `ResponseBody` variants | `ResponseBodyLifecycleTests.A_second_open_throws_and_names_the_buffering_route`. | 1 | ✅, with §10 entry 6 |
| `BODY-15` | MUST | **Build** the latch (P3b-2): close releases the transport resource whether or not the body was read, at most once, and a release that throws still flips the latch. | §3.7 | `ResponseBody`, `HttpResponseMessageBody` | `ResponseBodyLifecycleTests`; `SystemNet.Tests/HttpResponseMessageBodyTests` (the message is disposed once across `Dispose` + `DisposeAsync`). | 1 | ✅ |
| `BODY-16` | MUST | **Build** (P3b-13): `ReadAsBytesAsync`/`ReadAsStringAsync` (and 3a's synchronous readers) dispose the **body** in `finally`, not only the stream. | §3.1; §11 item 13 (`BODY-16` is "HTTP-16-body") | `ResponseBody` | `ResponseBodyLifecycleTests.Readers_dispose_the_body_on_success_and_on_failure` (a body whose stream throws mid-read). | 1 | ✅ |
| `BODY-17` | MUST | **Build** the request-logging wrapper (internal, P3b-9) over 3a's `TeeStream`: the delegate writes once into the tee; the wire receives every byte. | §3.1 request-logging wrapper | `LoggingRequestBody` (internal) | `LoggingRequestBodyTests` (port of Node's `request-body-logging.test.ts`). | 5 | ✅ |
| `BODY-18` | MUST | **Build**: the tap is cleared at the start of every write. | §3.1 | `LoggingRequestBody` | `…The_tap_reflects_only_the_latest_attempt`. | 5 | ✅ |
| `BODY-19` | MUST | **Build**: the cap is a required constructor argument (no unbounded default, P3b-9); 0 mirrors nothing and forwards everything. | §3.1 | `LoggingRequestBody` | `…A_multi_megabyte_write_mirrors_only_the_cap`, `…A_cap_of_zero_mirrors_nothing`. | 5 | ✅ |
| `BODY-20` | SHOULD | **Build**: mirror-before-forward is 3a's tee contract; the snapshot after a primary failure contains the failing chunk. | §3.1; IO-25 | `LoggingRequestBody` | `…A_primary_failure_leaves_the_failing_chunk_captured`. | 5 | ✅ |
| `BODY-21` | MUST | **Build**: `IsReplayable` is the delegate's; `ToReplayableAsync` returns `this` when the delegate is replayable, otherwise a new wrapper (own tap, same cap) over the delegate's replayable form. | §3.1 | `LoggingRequestBody` | `…Materialising_rewraps_with_the_cap_and_a_separate_tap`. | 5 | ✅ |
| `BODY-22` | MUST | **Build** the response-logging wrapper's lazy drain-once: first read, snapshot or error query triggers it; concurrent first accesses serialise on a `SemaphoreSlim(1, 1)`, which parks no thread (§3.1). The drain's token is a ruling (P3b-10, accepted by the lead's ruling of 2026-10-02). | §3.1 response-logging wrapper | `LoggingResponseBody` (internal) | `LoggingResponseBodyTests` (port of Node's `response-body-logging.test.ts`), including a counting delegate under 16 concurrent first readers. | 6 | ✅ |
| `BODY-23` | MUST | **Build** the fits-cap regime: capture all, dispose the delegate (quietly, `BODY-28`), serve each read as a fresh read-only `MemoryStream` view over the captured array. | §3.1; IO-19–IO-24 | `LoggingResponseBody` | `…Fits_cap_every_read_is_an_independent_view`. | 6 | ✅ |
| `BODY-24` | MUST | **Build** the over-cap regime: keep the delegate open; the next read is a single-use internal concatenating stream (prefix view, then the live tail); a second read throws `StreamConsumedException`. | §3.1 | `LoggingResponseBody`, internal `PrefixedReadStream` | `…Over_cap_the_consumer_receives_every_byte_once`. | 6 | ✅ |
| `BODY-25` | MUST | **Met through design §10 entry 4**: `Stream.Read` returning 0 *is* end of stream on .NET; the drain and the tail never issue a zero-count read (asserted in debug builds), so a source cannot make them spin. No declared-length cross-check in the drain: a `HEAD` response carries a `Content-Length` and no body, so the check would fail correct responses (position E). | §3.1 note 1; §10 entry 4 | `LoggingResponseBody`, `PrefixedReadStream` | `…Never_issues_a_zero_count_read` (a probe stream that throws on `count == 0`). | 6 | ✅, with §10 entry 4 |
| `BODY-26` | MUST | **Build**: partial bytes kept; the failure cached as an `ExceptionDispatchInfo`; every read rethrows it; snapshot returns the partial bytes without throwing; `DrainFailure` reports it (or `null`) without starting a drain. | §3.1 | `LoggingResponseBody` | `…A_drain_failure_is_cached_and_the_partial_bytes_kept`, `…The_failure_query_never_starts_a_drain`. | 6 | ✅ |
| `BODY-27` | MUST | **Build**: one `Interlocked` close-once guard shared by the wrapper's own dispose and the tail stream's dispose; a throwing delegate dispose still flips it and propagates once. | §3.1; §3.7 | `LoggingResponseBody` | `…The_delegate_is_closed_at_most_once_across_every_path`. | 6 | ✅ |
| `BODY-28` | MUST | **Build**: on the fits-cap path a delegate-dispose failure goes through `Disposal.DisposeQuietly` (no primary), never into the drain error; the captured array outlives the wrapper's dispose. | §3.1; §3.7 | `LoggingResponseBody`, `Disposal` | `…A_close_failure_after_full_capture_is_reported_not_raised`, `…Snapshot_survives_dispose`. | 6 | ✅ |
| `BODY-29` | SHOULD | **Build**: `ContentLength` is the delegate's declared length until a complete capture, then the captured length. | §3.1 | `LoggingResponseBody` | `…Content_length_is_the_captured_size_only_after_full_capture`. | 6 | ✅ |
| `BODY-30` | MUST | **Already met** (phase 1 S8), as `HTTP-52`; "a response with no body is returned unchanged" is the pipeline step's, 4c's. | phase 1 checklist S8; §5.1 | — | Cite `EnsureSuccessErrorMappingTests` (`Security`). | 1 | ✅ ⏳ 4c |
| `BODY-31` | MUST | **Already met** (phase 1 S8): only 400–599 map. | as above | — | Cite `EnsureSuccessErrorMappingTests.A_non_error_status_is_returned_with_its_body_intact` (`Security`). | — | ✅ ⏳ 4c (`ErrorMappingPolicy`) |
| `BODY-32` | MUST | **Build** on both wrappers: a negative cap (constructor or `Snapshot(int)`) throws `ArgumentOutOfRangeException`; a cap above `Array.MaxLength` is clamped silently; a snapshot returns what exists up to the cap. The capless snapshot's "fail above `Array.MaxLength`" clause is **vacuous**: the capture is bounded by a cap that is itself clamped to `Array.MaxLength`, so it can never exceed it (position F). | §3.1; IO-9 | both wrappers | `…Negative_caps_are_rejected`, `…Caps_above_the_array_bound_are_clamped`. | 5, 6 | ✅ (capless clause vacuous) |
| `BODY-33` | SHOULD | **Met by construction** (resolving §12's "unaddressed"): the exception-side error body is the replayable bytes body of S8, so every read is a fresh, non-consuming view; "null when there is no body" reads as an empty body under §10 entry 30 (the response body is never `null`). | §5.1; §10 entry 30 | `HttpResponseException.Response.Body` | `ErrorBodyPreviewTests.Reading_the_error_body_twice_yields_the_same_bytes_and_GetErrorAsync_still_works`. | 1 | ✅ |
| `BODY-34` | MUST | **Hand-off to 5b** for engagement ("only when body-level logging is enabled") and the one shared preview-size setting (`HttpLoggingOptions`, `OBS-34`, `OBS-36`–`OBS-38`). The consumer-receives-every-byte clause is built and proven here. | Phase 5 card (5b) | — | `LoggingRequestBodyTests`/`LoggingResponseBodyTests` property theory: for (cap, body) pairs the consumer receives every byte and the capture stays bounded. | 5, 6 | ✅ ⏳ 5b |
| `BODY-35` | MUST | **Build**: every new variant reports the exact length or −1; `FromStream` rejects `contentLength < -1` (`ArgumentOutOfRangeException`) and a non-readable source (`ArgumentException`), unless 3a has already added either. | §3.1 | factories | `RequestBodyContractTests`. | 2 | ✅ |
| `BODY-36` | MAY | **Declined for v1**: no memory-mapped view; offered later only if a signing use case asks (§3.1, §12 "Deferred"). | §3.1; §12 | — | None. | 7 | ⏳ (`docs/first-release.md`, "SHOULD- and MAY-level requirements declined for v1", bullet added in PR 7) |
| `BODY-37` | MUST | **Build** the wrapper half: the wrapper exposes no tap handle, only copying snapshots; the tee's own refusal (`IO-28`) is 3a's. | §3.1 | `LoggingRequestBody` | `…Snapshots_are_copies` (mutating a returned snapshot leaves the next one intact); an architecture assertion that no member of either wrapper returns `Stream`, `Memory<byte>` or `byte[]` other than `Snapshot`'s copy. | 5 | ✅ (tee half ⏳ 3a, `IO-28`) |

**Inherited row, closed here (not in the 48).** `SEAM-14` idempotence (2b checklist ⏳ 3b): `SystemNetHttpClient`'s
`Dispose` is latched, and `DelegateHttpClient`'s and both bridges' disposes are no-ops (idempotent trivially). The 2b
checklist row gets a dated correction pointing at `SystemNetHttpClientDisposeTests`; 8b keeps `SEAM-15`.

**Totals.** 48 rows, no N/A: 36 ✅ outright; 6 ✅ with a clause another owner holds (`HTTP-52`, `BODY-30`,
`BODY-31` and `BODY-34` with a ⏳ clause, `BODY-12` with a 🚫 clause, `BODY-37` with the tee half ⏳ 3a); 6 ⏳ wholly
(`BODY-10` to 3a; `HTTP-44`, `HTTP-45` to 7a; `BODY-4` to 6a/6b/6c; `BODY-5` to 6a; `BODY-36` to
`docs/first-release.md`). The plan recounts against the built code.

---

## Argued positions

### A. The body-ownership verdict (`body-stream-ownership`, `BODY-8`) — 3b argues it

**Which phase argues which verdict (P3b-1).** By the lead's ruling of 2026-10-02, **3b argues only
`body-stream-ownership`** (`BODY-8`, a request-body rule) and 3a argues `response-body-reopen-throws` (`BODY-14`), because
3a's `OpenRead` forces the answer ([3a's position B](../phase3a/2026-10-02-phase3a-io-design.md#b-response-body-reopen-throws-confirmed-and-extended-across-the-two-forms),
P3a-2, P3a-3). 3a also fixes the helper-layer half of §10 entry 5 (`IO-6`: an internal wrapper takes ownership unless
told `leaveOpen`), which 3b inherits. 3b cites 3a's position for the reopen verdict (position B below).

**Verdict: §10 entry 5 stands, extended to the 3b variants.** A request body closes exactly the sources it opened:

| Body | Opens | Closes |
|---|---|---|
| `FromStream`, single-use | nothing | nothing (the caller's stream stays open after success, failure and `ToReplayableAsync`) |
| `FromStream`, seekable (P3b-4) | nothing | nothing; it moves the caller's position, which is documented |
| `FromFile` | a `FileStream` per write | that `FileStream`, in the same write |
| `FromForm`, `Multipart` | nothing | nothing; multipart never disposes a part or the destination |
| logging request wrapper | the tee | the tee, with `leaveOpen: true` over the destination |
| `ResponseBody.FromStream` | — | **takes ownership** of the transport's stream (unchanged) |
| logging response wrapper | — | **takes ownership** of its delegate (it is an SDK body handed over by the pipeline, as `Response` owns its body) |

**For.** `BODY-8` sanctions the choice ("A port MUST decide its stream-ownership/close rule deliberately"). One
direction for every request body makes the rule checkable without reading each variant, and it is styleguide 13.4.
The reference's per-variant inconsistency (its buffered-source body drains-and-closes; its raw-stream bodies do not)
is exactly what the requirement asks a port not to inherit by accident.

**Against, and why it does not outweigh.** A caller who passes a stream and forgets it leaks it. That is the cost of
every `leaveOpen: true` API in the BCL, and the alternative (a body that closes a stream it did not open) breaks the
seekable variant, which must keep the stream open to replay.

### B. The reopen verdict (`response-body-reopen-throws`, `BODY-14`) — 3a argues it

**Cited, not argued here.** The verdict is 3a's
([3a's position B](../phase3a/2026-10-02-phase3a-io-design.md#b-response-body-reopen-throws-confirmed-and-extended-across-the-two-forms),
P3a-3, by the lead's ruling of 2026-10-02): §10 entry 6 stands, and a second open in either form (`OpenRead` or
`OpenReadAsync`, one latch per variant) throws `StreamConsumedException` instead of returning the same `Stream`. 3b
extends it to its new response variants: the response-logging wrapper's over-cap stream is single-use under the same
rule, while its fits-cap views and the replayable error body are replayable by design and open a fresh view each time.
3b keeps its own `BODY-14` tests (`ResponseBodyLifecycleTests`). §10 entry 6's dated correction is 3a's (one owner);
3b writes none. 3b adds two things the verdict needs to be honest: the exception message names
the buffering route (the explicit wrapper the requirement's last sentence asks for), and the ordering rule of P3b-11
(a consumed **and** disposed stream body reports "consumed", so existing callers see the same exception type).

### C. Disposal before its dependencies exist (P3b-3, accepted by the lead's ruling of 2026-10-02)

Design §3.7's `DisposeQuietly` attaches to the primary through §5.2's `ExceptionTrail.AddSuppressed` and filters
through `ExceptionFacts.IsFatal`; both are 4b's, which follows 3b. And it reports through "a `Warning` log and an
exception event on the current SDK `Activity`", but core has no logger plumbing until 5b. Options:

1. **Build `Disposal` now with the parts that exist, and name the repoints** (chosen). `Disposal` is internal:
   `DisposeQuietly(IDisposable? resource, Exception? primary = null, ILogger? logger = null)` and
   `DisposeQuietlyAsync(IAsyncDisposable? resource, Exception? primary = null, ILogger? logger = null)`. Null-safe;
   a fatal exception (`OutOfMemoryException` and subtypes, §5.2's one-line rule, as a private predicate) propagates;
   any other is reported on `Activity.Current` through `AddException` with the tag `dexpace.dispose.suppressed = true`
   (fact 7) and, when a logger is supplied, a `Warning` naming the resource type and the exception type (never a
   value). With a primary in flight, the same report carries `dexpace.dispose.primary_type`, and the primary is never
   replaced. **4b** repoints the primary branch to `ExceptionTrail.AddSuppressed` and the filter to
   `ExceptionFacts.IsFatal`; **5b** plumbs the client's logger to the call sites that have one.
2. Build a minimal `ExceptionTrail` in 3b. Rejected: it designs 4b's public `SdkException.Suppressed` surface early.
3. Defer `Disposal` to 4b. Rejected: the 2b hand-off and `BODY-28` need it now.

**Why it went to the lead, and the ruling.** With no listener and no logger (a bare `AsAsync` call outside any traced
scope), option 1 reports to nobody, which is §3.7's "never swallows silently" violated until 5b. The mitigation would be a
counter on `DexpaceDiagnostics.Meter`, but its instrument name is 5b's `OBS-32` decision. The default taken was: accept the
gap until 5b, documented on `Disposal`. **The lead's ruling of 2026-10-02 accepted it:** the no-listener/no-logger silence
is an accepted interim gap, and it closes in 5b, which plumbs the client's logger to the call sites and owns the counter's
name. 3b adds no counter.

The signature adds `ILogger?` to §3.7's two-parameter form; it is optional and internal, so §3.7 gets a dated
correction rather than a deviation.

### D. The drain's cancellation token (P3b-10, accepted by the lead's ruling of 2026-10-02)

§3.1 says the response-logging drain "runs under the wrapper's own lifetime token, not the first caller's, so one
caller cancelling cannot poison the shared drain for the others". Taken literally, a hung server body then blocks the
first reader with **no** way to cancel it: its own token governs only the semaphore wait, and the drain ignores it.
Under 5b the first reader is the pipeline's logging step, whose token carries the call's timeout, so the call's
deadline would stop working whenever body logging is on. Options:

1. **Literal §3.1**: lifetime token only. A wedged body wedges the call.
2. **Linked token (chosen)**: the drain runs under a token linked from the starting accessor's token and the
   wrapper's lifetime token (cancelled by `Dispose`). If the starting accessor cancels mid-drain, the drain fails like
   any other failure: partial bytes kept, the `OperationCanceledException` cached (`BODY-26`), later reads rethrow it.
   Other accessors are "poisoned" only in the sense that the body really is unreadable: a cancelled read of a live
   transport stream does not leave a resumable stream behind.
3. A shared background drain task awaited with the non-generic `Task.WaitAsync(token)` by each accessor: every caller
   can stop waiting and the drain keeps the connection busy in the background. Rejected: it orphans work holding a
   connection, which is what 2b's `WaitAsync` ban exists to stop, and it has no synchronous form without blocking on a
   task (styleguide 9.1).

Option 2 departs from a sentence of §3.1, so it is a dated correction to §3.1 (the mechanism changes; `BODY-22`'s
"upstream read exactly once" is kept). Option 1's literal reading was put to the lead; the ruling of 2026-10-02 accepted
option 2.

### E. No declared-length cross-check on the response drain (`BODY-25`)

A drain that compares captured bytes against `ContentLength` would turn §10 entry 4's undetectable early EOF into a
detectable one for known-length bodies. It is not built: a `HEAD` response (and a 304) carries a `Content-Length` with
no body, the transport (`SocketsHttpHandler`) already throws on a premature end of a `Content-Length` body, and the
wrapper sits above any transport. The SDK-side truncation guard stays on the request side, where the SDK is the
writer (3a's exact copy).

### F. Vacuous clauses

- `BODY-32`'s capless-snapshot failure: unreachable (position in the row). Reported as vacuous in §12's list by PR 7,
  never as passing.
- `BODY-6` for the seekable variant: the antecedent ("a single-use body") is not reached; covered by the classification
  test, not a separate row.

---

## Rulings (2026-10-02, taken by the brainstorm)

Each ruling lists the options considered and why the chosen one won. The three put to the lead (P3b-3, P3b-5, P3b-10)
were **accepted as written by the lead's ruling of 2026-10-02**, which also settled P3b-1's split with 3a.

- **P3b-1 — 3b argues `body-stream-ownership`; 3a argues the reopen verdict; both stand** (positions A and B). *Options:*
  split one per sub-phase; 3a argues both; 3b argues both. This design first took "3b argues both", because `BODY-8` and
  `BODY-14` are body rules; 3a's design argued the reopen verdict because its `OpenRead` forces the answer (P3a-2). *Ruled
  by the lead, 2026-10-02:* split one per sub-phase — 3b argues `BODY-8` (3a's ownership surface is only `IO-6` at the
  helper layer), and 3a argues `BODY-14` (P3a-3), which 3b cites and extends to its new variants, keeping its `BODY-14`
  tests. §10 entry 5 gets a dated correction naming the new variants (3b, PR 7); §10 entry 6's correction is 3a's alone.
- **P3b-2 — `ResponseBody` adopts the standard dispose pattern with one SDK latch.** Public `Dispose()` and
  `DisposeAsync()` become non-virtual: `if (Interlocked.Exchange(ref _disposed, 1) != 0) return;` then
  `Dispose(true)` / `await DisposeAsyncCore()`. Subclasses override `protected virtual void Dispose(bool disposing)`
  and `protected virtual ValueTask DisposeAsyncCore()` (default: `Dispose(true)`). No finalizer (13.3). *Options:*
  (a) keep `public virtual Dispose` and latch only the built-in variants and `Response` — non-breaking, but a
  third-party body stays unlatched and the guarantee stays the inner object's, which §3.7 rejects; (b) the standard
  pattern — breaking for every subclass (one in `SystemNet`, seven test fakes), and the shape `CA1063` and every BCL
  base type already use. (b) chosen. No `protected IsDisposed` is added (10.1): the built-in variants track their own
  state, and a subclass can do the same.
- **P3b-3 — `Disposal` is built now, internal, with an optional `ILogger`, reporting through `Activity` until 4b and
  5b repoint it** (position C). **Accepted by the lead's ruling of 2026-10-02:** the no-listener/no-logger silence is an
  accepted interim gap that closes in 5b.
- **P3b-4 — `FromStream` promotes a seekable stream to replayable, automatically.** Conditions: `CanRead`, `CanSeek`,
  and a declared `contentLength` in `[0, Array.MaxLength]`. The start position is captured at construction; **every**
  write (the first included) seeks to it, so a caller who moved the stream after construction still gets the
  declared bytes; the copy is 3a's exact copy of `contentLength` bytes. An `Interlocked` in-flight flag admits one
  write at a time; a concurrent second write throws `InvalidOperationException` ("a seekable-stream body cannot be
  written concurrently"), so at most one rewind happens between two writes. An unknown length (−1) stays single-use:
  the length is **not** inferred from `Length − Position`, because that would turn a chunked upload into a
  `Content-Length` one behind the caller's back. *Options:* a separate `FromSeekableStream` factory (explicit, but
  `HTTP-38` says the factories classify by source, and every caller would have to know to choose it); inference of
  the length (rejected above). Equality: identity (a live source, 2a's rule). Behaviour change, listed as breaking 4.
- **P3b-5 — "Regular file" is enforced where the platform can see it** (facts 1–3). **Accepted by the lead's ruling of
  2026-10-02, routed as a new design §10 entry.**
  `FromFile` resolves a symbolic link to its final target before capturing the size (fact 3), throws
  `FileNotFoundException` for a missing path, `ArgumentException` for a directory (and, on Windows, a path whose
  attributes carry `Device`), and `ArgumentOutOfRangeException` for the range. On Unix a FIFO or a character device is
  indistinguishable from a regular file through the shared framework (fact 1), and opening a FIFO blocks (fact 2).
  The mitigation: such files report size 0, so their count resolves to 0, and **a count of 0 never opens the file** —
  the write is a legitimate empty write. The residue: a special file uploads as an empty body instead of failing at
  construction. *Options:* P/Invoke `stat` (platform-specific struct layouts and an AOT-visible native dependency for
  one validation clause — rejected); accept and document (chosen). Because a MUST clause is unmet on a stated domain,
  this is a **design §10 candidate** in the "four leave a MUST clause unmet" class. The lead's ruling of 2026-10-02
  routed it as a new §10 entry, not a §11 reading; PR 7 writes it.
- **P3b-6 — File handle and sharing.** Each write opens
  `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 0,
  FileOptions.SequentialScan | (async ? FileOptions.Asynchronous : 0))`, seeks to `Offset`, copies exactly the count
  through 3a's helper and disposes the handle. *Options:* `FileShare.Read` (locks writers out for the length of an
  upload, which breaks log shipping and fails outright when another process holds the file open for writing) or the
  chosen permissive share, whose guarantee comes from the captured count: a shrink is a loud short read (fact 4), a
  growth is ignored past the count. A file rewritten in place between two writes breaks byte-identity; documented,
  and the same as every reference port.
- **P3b-7 — `FromForm` is the WHATWG serializer, hand-written.** Input
  `IEnumerable<KeyValuePair<string, string>>` (a `Dictionary`, a list of pairs; order and duplicates kept). Each name
  and value is UTF-8 encoded with a **strict** encoder (a lone surrogate throws `ArgumentException` naming the field
  index, never the value, consistent with `Query.Builder`); bytes `A-Z a-z 0-9 * - . _` are literal, `0x20` is `+`,
  every other byte is `%XX` uppercase (fact 6). A `null` name or value throws. The content type is
  `CommonMediaTypes.ApplicationFormUrlEncoded` with no `charset` (WHATWG sends none). The result is the existing
  in-memory bytes variant, so it inherits value equality and `O(1)` hashing. *Options:* Node's RFC 3986 encode plus
  `%20 → +` (leaves `~` literal and encodes `*`: neither WHATWG nor what browsers send); `WebUtility.UrlEncode` (leaves
  `!()`); a `Query` input (its values are nullable and it renders RFC 3986, so the types would invite the confusion
  §3.5 forbids). The chosen input shape is the minimal one (10.1); a `Query` overload is additive later.
- **P3b-8 — Multipart framing and part headers.**
  - `MultipartPart` is a `sealed class` (not a record: `with` would bypass validation, the 2a `Request` precedent)
    with `Name`, `FileName?` and `Body`. `Name` and `FileName` reject CR, LF and every other C0 control and DEL
    (`ArgumentException`, naming the code point, never the value); `"` and `\` are backslash-escaped inside the
    quoted-string (RFC 7578 §4.2 via RFC 2045's quoted-string); non-ASCII is written as UTF-8. *Options:* Node strips
    CR/LF silently (changes a field name without telling anyone — rejected); WHATWG percent-encodes `"`/CR/LF (what
    browsers send, but a server reading RFC 7578's quoted-string sees `%22` literally — rejected for sibling
    agreement with Node's escaping).
  - The part `Content-Type` is the part body's `MediaType.ToString()`: `MediaType` is validated at construction (2a),
    so a control character cannot reach the header; pinned anyway because `RequestBody` is open.
  - An empty part list throws (`ArgumentException`): RFC 2046 §5.1.1 requires one or more body parts. Node allows it.
  - Boundary: generated as `dexpace-` plus 32 characters from `RandomNumberGenerator.GetString` over `[A-Za-z0-9]`;
    a caller boundary must match RFC 2046 (`1`–`70` `bchars`, not ending in space) or `ArgumentException`. Rendered
    through `MediaType.Of("multipart", "form-data", {boundary})`, which quotes a non-token value, so `boundary=a,b` is
    sent as a quoted-string (Node's own finding).
  - The shared framing routine runs **once, at construction**: each part's header bytes and the trailer bytes are
    computed and stored, and both `ContentLength` and the write use the stored arrays, so drift is impossible by
    construction. The write goes through an internal non-disposing `BoundedWriteStream` that refuses a chunk that
    would carry the total past a known `ContentLength` (before writing it) and checks the total at the end, so a part
    whose own length lies is caught (Node's `boundedWriter`, ported). A non-replayable composite carries its own
    `Interlocked` consume-once guard, so a second write throws before any byte; a replayable one admits concurrent
    writes. Equality: identity.
- **P3b-9 — The logging wrappers are internal, with a required cap.** `LoggingRequestBody(RequestBody delegate, int
  tapCap)` and `LoggingResponseBody(ResponseBody delegate, int captureCap)`, in `Http/Request` and `Http/Response`,
  consumed by 5b's instrumentation in the same assembly. *Options:* public (the reference's "unbounded default cap
  exists for direct wrapper use" implies direct users) or internal (10.1; making them public later is additive, while
  the reverse is breaking). Internal chosen, so the unbounded default has no user and is not built (`BODY-19`'s
  parenthesis). Both clamp a cap above `Array.MaxLength` and reject a negative one (`BODY-32`). Equality: identity.
  Snapshots return `byte[]` copies; the request wrapper's tap is per-wrapper and cleared per write, so two concurrent
  writes of one wrapped replayable body would interleave its preview — documented, since a call has one attempt in
  flight at a time.
- **P3b-10 — The drain runs under a token linked from the starting accessor and the wrapper's lifetime** (position D).
  **Accepted by the lead's ruling of 2026-10-02.** The drain is one `bool async` core (§11 item 12) split under the 70-line cap: acquire
  (`Wait`/`WaitAsync` with the caller's token), drain-or-return-cached, release.
- **P3b-11 — Use after dispose.** A stream-backed response body (the built-in `FromStream` variant and `SystemNet`'s
  `HttpResponseMessageBody`) throws `StreamClosedException` (the existing type, whose summary describes exactly this)
  from `OpenReadAsync` after dispose; the consumed check runs first, so a body that was read and then disposed still
  reports `StreamConsumedException`. In-memory bodies (`FromBytes`, the replayable error body, the wrapper's captured
  array) keep serving after dispose: they hold memory, not a connection (`IO-42`'s split, `BODY-28`, and `BODY-30`'s
  "readable after the connection is released"). *Options:* `ObjectDisposedException` (the BCL idiom, used for the
  transport in §3.7) or `StreamClosedException` (an `SdkException`, so one `catch (SdkException)` covers body misuse).
  The SDK type wins on the body; the transport keeps `ObjectDisposedException` (8b, `SEAM-15`).
- **P3b-12 — The BOM rule is general.** `ReadAsStringAsync` (and 3a's synchronous reader) decode through one internal
  routine that resolves the encoding (declared charset, else UTF-8) and skips a leading `encoding.Preamble` when the
  bytes start with it, for any encoding with a preamble (fact 5), then decodes the rest. A BOM that does not match the
  resolved charset is kept, because the declared charset wins (`HTTP-42`). This is §11 item 34 as written, extended
  from UTF-8 to every preamble-bearing encoding (dated correction to §11 item 34). **One routine, two-way:** whichever
  of 3a's PR 5 (the sync `ReadAsString`) and 3b's PR 1 (`TextDecoding`) lands second converges both readers on this
  routine, and the other's BOM and decode tests, sync and async, pass over it unchanged (Prerequisites, landing order).
- **P3b-13 — The convenience readers dispose the body, not only the stream** (`BODY-16`). `ReadAsBytesAsync` becomes
  `try { … } finally { await DisposeAsync() }` (3a's synchronous readers mirror it). `ResponseBodySerdeExtensions
  .ReadValueAsync` is a typed reader, not one of `BODY-16`'s two, and is left to 7a (`SERDE`), named in the coupling.
- **P3b-14 — Names and shapes.** `RequestBody.FromFile(string path, MediaType? contentType = null, long offset = 0,
  long count = -1)` returns the public sealed `FileRequestBody` (internal constructor; `FilePath` is the resolved full
  path, `Offset`, `ContentLength` the count); `RequestBody.FromForm(…)`; `RequestBody.Multipart(…)` (design §4.5's
  name). `FromFile` has no default media type: the SDK does not guess MIME from an extension.
- **P3b-15 — Exit marks for the rows other phases own** are as in the table: `BODY-4`/`BODY-5` to 6a/6b/6c,
  `BODY-30`/`BODY-31`/`HTTP-52` ✅ with the policy form ⏳ 4c, `BODY-34` ✅ ⏳ 5b, `BODY-36` declined into
  `docs/first-release.md`, `BODY-12` ✅ 🚫, `BODY-10` ⏳ 3a (`HTTP-39` is 3a's row), `HTTP-44`/`HTTP-45` ⏳ 7a.
- **P3b-16 — The 2b pin test is replaced, not deleted silently.**
  `A_throwing_dispose_after_cancellation_faults_the_task_with_that_exception` becomes
  `A_throwing_dispose_after_cancellation_cancels_the_task_and_reports_the_failure`: the task is `Canceled`, and an
  `ActivityListener` sees one `exception` event tagged `dexpace.dispose.suppressed`. Design §3.3 and §11 item 43 get a
  dated correction removing "until phase 3b".

---

## Type shapes

Signatures only; XML docs, `ConfigureAwait(false)` and argument checks are implied. Every new public member goes into
`src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt` (or `SystemNet`'s), with the removals `RS0017` reports.

### `RequestBody` (additions)

```csharp
public abstract class RequestBody
{
    // existing members unchanged; FromStream's classification changes (P3b-4)

    public static FileRequestBody FromFile(string path, MediaType? contentType = null, long offset = 0, long count = -1);
    public static RequestBody FromForm(IEnumerable<KeyValuePair<string, string>> fields);
    public static RequestBody Multipart(IEnumerable<MultipartPart> parts, string? boundary = null);

    // private sealed: SeekableStreamRequestBody, MultipartRequestBody (+ the existing variants)
}

public sealed class FileRequestBody : RequestBody
{
    internal FileRequestBody(string filePath, long offset, long count, MediaType? contentType);
    public string FilePath { get; }
    public long Offset { get; }
    public override MediaType? ContentType { get; }
    public override long ContentLength { get; }   // the exact count, never -1
    public override bool IsReplayable => true;
    public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default);
    // + 3a's synchronous WriteTo; ToReplayableAsync inherited (returns this)
}

public sealed class MultipartPart
{
    public MultipartPart(string name, RequestBody body, string? fileName = null);
    public string Name { get; }
    public string? FileName { get; }
    public RequestBody Body { get; }
}
```

### `ResponseBody` and `Response`

```csharp
public abstract class ResponseBody : IAsyncDisposable, IDisposable
{
    public void Dispose();                       // was: public virtual. Latched (P3b-2).
    public ValueTask DisposeAsync();             // was: public virtual. Same latch.
    protected virtual void Dispose(bool disposing);       // new
    protected virtual ValueTask DisposeAsyncCore();       // new; default calls Dispose(true)

    public virtual Task<byte[]> ReadAsBytesAsync(CancellationToken cancellationToken = default);  // disposes the body in finally
    public virtual Task<string> ReadAsStringAsync(CancellationToken cancellationToken = default); // strips a matching BOM
}

public sealed class Response : IAsyncDisposable, IDisposable
{
    public void Dispose();             // latched, forwards to Body
    public ValueTask DisposeAsync();   // same latch
}
```

`SystemNet`'s internal `HttpResponseMessageBody` moves its release into `Dispose(bool)`, throws `StreamClosedException`
from `OpenReadAsync` after dispose (P3b-11), and `SystemNetHttpClient.Dispose` gains the latch (`SEAM-14`). No public
`SystemNet` surface changes.

### Internal types

```csharp
internal static class Disposal                                      // Dexpace.Sdk.Core.Internal
{
    internal static void DisposeQuietly(IDisposable? resource, Exception? primary = null, ILogger? logger = null);
    internal static ValueTask DisposeQuietlyAsync(IAsyncDisposable? resource, Exception? primary = null, ILogger? logger = null);
}

internal sealed class LoggingRequestBody : RequestBody              // Http/Request
{
    internal LoggingRequestBody(RequestBody inner, int tapCap);
    internal byte[] Snapshot();
    internal byte[] Snapshot(int maxBytes);
}

internal sealed class LoggingResponseBody : ResponseBody            // Http/Response
{
    internal LoggingResponseBody(ResponseBody inner, int captureCap);
    internal Task<byte[]> SnapshotAsync(CancellationToken cancellationToken);   // triggers the drain
    internal Task<byte[]> SnapshotAsync(int maxBytes, CancellationToken cancellationToken);
    internal Exception? DrainFailure { get; }                                   // never triggers the drain
    internal bool IsFullyCaptured { get; }
}

internal sealed class PrefixedReadStream : Stream     // prefix view, then the live tail; dispose routes to the shared close-once guard
internal sealed class BoundedWriteStream : Stream     // multipart's length guard; Dispose does not dispose the primary
internal static class FormUrlEncoder                  // WHATWG serializer (P3b-7)
internal static class MultipartFraming                // header/trailer bytes, boundary generation and validation
internal static class TextDecoding                    // the one decode routine (P3b-12), shared with 3a's sync reader
```

A synchronous `Snapshot` on the response wrapper exists only if 5b's synchronous path needs it; the drain's
synchronous half exists for 3a's `OpenRead` regardless.

---

## Migration plan from the as-built code

| Change | Sites | Rewrite |
|---|---|---|
| `ResponseBody` dispose pattern (P3b-2) | `src/Dexpace.Sdk.Http.SystemNet/HttpResponseMessageBody.cs`; test fakes overriding `Dispose`/`DisposeAsync`: `TestSupport/Transports/DisposalCountingBody.cs`, `Core.Tests/Security/EnsureSuccessErrorMappingTests.cs` (`TrackingBody`), `Core.Tests/Http/Response/ResponseTests.cs`, `Core.Tests/Pagination/PageableTests.cs`, `Core.Tests/Pipeline/Policies/RedirectPolicyTests.cs`, `Core.Tests/Client/SyncToAsyncBridgeTests.cs` (`ThrowingDisposeBody`); the plan greps for any other `: ResponseBody` (`AotSmoke/SmokeChecks.cs`, `SystemTextJson.Tests/BodyConvenienceTests.cs` subclass without overriding dispose today) | `override void Dispose()` → `protected override void Dispose(bool disposing)`; `override ValueTask DisposeAsync()` → `protected override ValueTask DisposeAsyncCore()`. `DisposalCountingBody` now counts **releases** (at most one, by the latch); every test that asserts `DisposeCount == 1` or `0` keeps its meaning ("released exactly once, or never") |
| `ReadAsBytesAsync`/`ReadAsStringAsync` dispose the body | callers that read then use the body again: the plan greps; none known in `src/`. `HttpResponseException.GetErrorAsync` reads the replayable error body, which keeps serving after dispose | none, beyond the grep |
| `AsAsync`'s direct `Dispose()` | `HttpClientExtensions.cs` `SyncToAsyncAdapter.Run` | `Disposal.DisposeQuietly(response)`; the pin test replaced (P3b-16) |
| `FromStream` classification | `RequestBody.cs`; tests all pass `contentLength` −1, so none changes behaviour | factory dispatch only |
| Docs and package READMEs | `src/Dexpace.Sdk.Core/README.md` (bodies row), `docs/architecture.md` (bodies paragraph), `CLAUDE.md`'s "Single-use bodies" bullet (gains: seekable known-length streams are replayable) | PR 7 |

**`Security` classes.** `EnsureSuccessErrorMappingTests` gets the mechanical `Dispose(bool)` rewrite of its private
`TrackingBody` fake and nothing else: no assertion changes value (constraint 5; the checklist lists it as
"mechanical"). `MalformedContentTypeWireTests` is **unedited**: `The_native_response_is_disposed_when_adaptation_throws`
exercises `SystemNetHttpClient`'s TRANSPORT-22 guard, which 3b does not touch, and the body's migration is internal.
The phase-1 checklist row S9 gets a dated note that its 3b clause (the dispose latches) is closed by
`ResponseDisposeLatchTests` and `HttpResponseMessageBodyTests`.

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes and gets a `CHANGELOG.md` `[Unreleased]` line in
its PR (constraint 8).

| # | Change | Kind | Evidence | PR |
|---|---|---|---|---|
| 1 | `ResponseBody.Dispose()`/`DisposeAsync()` are no longer virtual; subclasses override `Dispose(bool)`/`DisposeAsyncCore()` | binary and source (every subclass) | `RS0016`/`RS0017` diff | 1 |
| 2 | `Response` and `ResponseBody` dispose at most once; a release that throws propagates once and later calls are no-ops | behaviour | `ResponseDisposeLatchTests` | 1 |
| 3 | `ReadAsBytesAsync`/`ReadAsStringAsync` dispose the body when they finish, success or failure | behaviour | `ResponseBodyLifecycleTests` | 1 |
| 4 | `ReadAsStringAsync` strips a leading BOM that matches the resolved charset | behaviour | `ResponseBodyDecodeTests` | 1 |
| 5 | A stream-backed response body read after dispose throws `StreamClosedException` | behaviour | `ResponseBodyLifecycleTests` | 1 |
| 6 | `AsAsync`: a throwing dispose of a response produced after cancellation no longer faults the task; it completes cancelled and the failure is reported | behaviour | `SyncToAsyncBridgeTests` | 1 |
| 7 | `RequestBody.FromStream` over a readable, seekable stream with a declared length is replayable and seeks the caller's stream before each write; `contentLength < -1` and a non-readable stream are rejected | behaviour | `SeekableStreamBodyTests`, `RequestBodyContractTests` | 2 |

Additive only: `FromFile`, `FileRequestBody`, `FromForm`, `Multipart`, `MultipartPart`, the two protected dispose
members' existence (listed under 1), and every internal type.

---

## Landing order

Each step is one pull request carrying code and tests together, its `PublicAPI.Unshipped.txt` diff and its
`CHANGELOG.md` line.

| PR | Content | Rows | Gate | Notes |
|---|---|---|---|---|
| **1** | `Disposal`; the `ResponseBody` dispose pattern and latch; `Response` latch; `HttpResponseMessageBody` migration and `SystemNetHttpClient` latch; the fakes' rewrite; `AsAsync` on `DisposeQuietly` and the replaced pin test; readers dispose the body; `TextDecoding` and the BOM; `ErrorBodyPreviewTests` | `HTTP-41`, `HTTP-42`, `HTTP-43`, `HTTP-52` (cite), `BODY-14`, `BODY-15`, `BODY-16`, `BODY-30` (cite), `BODY-33`, inherited `SEAM-14` | **free now** (no 3a edge); re-derive on 3a's `ResponseBody.cs` if 3a lands first | Breaking 1–6. **Shared decode, two-way:** if 3a's PR 5 merged first, this PR converges its sync `ReadAsString` onto `TextDecoding` and runs 3a's `IO-13` decode tests over it; if this PR merges first, 3a's PR 5 calls `TextDecoding` from the sync reader and runs these BOM cases in the sync form. One routine either way |
| **2** | `FromForm` and `FormUrlEncoder` (+ `tests/vectors/body/form-urlencoded.json`); `FromStream` validation and the seekable variant; `RequestBodyContractTests`, `SingleUseBodyTests`, `RequestBodyOwnershipTests` (stream half) | `HTTP-36`–`HTTP-38`, `BODY-1`, `BODY-3`, `BODY-6`–`BODY-9`, `BODY-35` | form: **free now**; seekable: **3a's exact copy and sync `WriteTo`** | Breaking 7. May split into 2a (form) and 2b (seekable) if 3a is late |
| **3** | `FileRequestBody`, `FromFile` | `HTTP-40`, `BODY-8` (file half), `BODY-11`–`BODY-13` | 3a's exact copy and sync `WriteTo` | Additive |
| **4** | `Multipart`, `MultipartPart`, `MultipartFraming`, `BoundedWriteStream` | `HTTP-51`, `BODY-2`, `BODY-6` (composite) | 3a's sync `WriteTo` | Additive |
| **5** | `LoggingRequestBody` | `BODY-17`–`BODY-21`, `BODY-32`, `BODY-34` (consumer clause), `BODY-37` | 3a's `TeeStream` and tap | Internal only |
| **6** | `LoggingResponseBody`, `PrefixedReadStream` | `BODY-22`–`BODY-29`, `BODY-32`, `BODY-34` (consumer clause) | 3a's sync `OpenRead`; PR 1 (`Disposal`, the latch) | Internal only |
| **7** | Close-out: `AotSmoke` over `FromFile` (temp file), `FromForm`, `Multipart` and the latched dispose; `docs/sdk-documentation/bodies.md`; the 3b checklist; dated corrections (design §3.1 for P3b-4/P3b-10 and the variants' "As built", §3.3 and §11 item 43 for P3b-16, §3.7 for `Disposal`'s signature and status, §4.5, §10 entry 5 (entry 6 is 3a's, P3a-3), §11 item 34, §12's `BODY` row: `BODY-33` addressed, `BODY-32`'s vacuous clause); the new §10 entry for P3b-5 (the lead's routing of 2026-10-02); `docs/first-release.md`'s `BODY-36` bullet; the 2b checklist's `SEAM-14` correction and phase 1's S9 note; the roadmap status note | all 48 (closing) | 1–6 | Docs close the phase (roadmap step 7) |

---

## Tests, vectors and ports

- **Categories.** `Unit` throughout, including the file-body tests (temp files under the test's own directory, no
  network) and `HttpResponseMessageBodyTests` (an in-process handler, no socket). 3b adds **no `Security` class**: it
  fixes no new phase-1 defect; it closes S9's routed clause with `Unit` tests and keeps the two named `Security`
  classes green as described under Migration.
- **Vectors.** `tests/vectors/body/form-urlencoded.json` (input pairs → wire bytes, including fact 6's line, `~`,
  `*`, space, `+`, `&`, `=`, `é`, an emoji, empty name, empty value, and the lone-surrogate rejection), loaded through
  2a's vector loader with a `"source"` field. The table is upstreamable; Node's own form cases are re-derived under
  WHATWG where they disagree (`~`), and the disagreement is noted in the file.
- **Ported, with a header comment citing `nodejs-sdk@c0ff3fd` and the path:**
  - `packages/body-file/src/file-body.test.ts`: recognisable and replayable; missing path and directory rejected;
    negative start / out-of-range count rejected; the destination is not closed; exact range written; count 0; a
    fresh handle per write; read and write errors propagate. **Added here:** the symlink size (fact 3), the shrink
    (fact 4), count 0 on a zero-size file never opens it (P3b-5).
  - `packages/core/src/body/multipart-body.test.ts`: every case except the builder ones (`newBuilder`, `parts`), which
    test a builder object this port does not have (§10 entry 10). The strip-CR/LF case is **inverted** to a rejection
    (P3b-8). **Added:** an empty part list is rejected.
  - `packages/core/src/body/simple-bodies.test.ts`: the form cases (re-derived per the vectors) and the
    sink-failure cases; the `freeze` cases are host facts (constraint 10) and are not ported.
  - `packages/core/src/body/stream-body.test.ts`: ownership (`BODY-8`), single use, concurrency; the declared-length
    cases are 3a's and stay there. **Added:** the seekable cases (`BODY-9`), which Node does not have (Node's stream
    body is always single-use).
  - `packages/core/src/body/materialize.test.ts`: all five.
  - `packages/core/src/body/request-body-logging.test.ts` and `response-body-logging.test.ts`: every case except the
    sink-abort/close cases of a `WritableStream` (a host fact: a .NET `Stream` has no abort), and
    "teardown is close() only" (a Node runtime-floor probe).
- **Concurrency tests** start their contenders on a `Barrier` and assert counts, not timings; none sleeps.
- **NativeAOT.** PR 7 extends `AotSmoke` so the file, form and multipart bodies write inside the published binary and a
  response is disposed twice.
- **Analyzer gates** the plan must watch: `MA0051` (the response drain and the multipart write each split into
  helpers), `CA2007` on every `await`, `await using` and `await foreach`, `CA1063`/`CA1816` on the new dispose pattern
  (the plan verifies with a throwaway build that the pattern without a finalizer passes `latest-recommended`), `CA2000`
  on the per-write `FileStream`, `RS0030` (no `Task<T>.WaitAsync`; the drain uses `SemaphoreSlim`).

---

## Coupling with later phases

- **4b** repoints `Disposal`'s primary branch to `ExceptionTrail.AddSuppressed` and its filter to
  `ExceptionFacts.IsFatal` (position C).
- **4c** builds `ErrorMappingPolicy` and the shared `ErrorBodyBuffer` (`BODY-30`, `BODY-31`, `HTTP-52`); it should move
  `EnsureSuccessAsync`'s `finally { await DisposeAsync(); }` onto `Disposal.DisposeQuietlyAsync(this, primary)`, so a
  dispose failure cannot replace a drain failure (§3.7).
- **5b** engages both wrappers only at body-level logging with one shared preview size (`BODY-34`, `OBS-34`,
  `OBS-36`–`OBS-38`), plumbs the client's `ILogger` into `Disposal` call sites, and swaps a response's body for the
  wrapper through `Response.WithBody`: the old `Response` must not be disposed separately (it would close the
  delegate — harmless now, under the latch, but a wasted release).
- **6a / 6b / 6c** own `BODY-4`'s three gates and `BODY-5`; the seekable variant makes more requests eligible for
  replay than before, which their tests should include.
- **7a** builds `HTTP-44`/`HTTP-45` and decides whether `ReadValueAsync` closes the body (`SERDE`).
- **8b** keeps `SEAM-15` (`ObjectDisposedException` after transport dispose), writes `RequestBodyContent`'s synchronous
  `SerializeToStream` over 3a's `WriteTo`, and may recognise `FileRequestBody` (no kernel path exists on
  `SocketsHttpHandler`; recognition matters only to a future transport). Its TRANSPORT-22 dispose-on-throw cannot call
  core's internal `Disposal`; 8b decides between a local equivalent and making `Disposal` public.

---

## Deviation Ledger

| ID | Decision | Touches | Kind | Argued in | Route |
|---|---|---|---|---|---|
| P3b-1 | 3b argues `body-stream-ownership`; §10 entry 5 stands, extended to the 3b variants. The reopen verdict (§10 entry 6, `BODY-14`, `HTTP-41`) is 3a's (P3a-3), cited and extended to the new variants | `BODY-8` (argued); `BODY-14`, `HTTP-41` (cited) | verdict on an existing entry | Position A (position B cites 3a) | Dated correction to §10 entry 5 (PR 7); entry 6's correction is 3a's (3a's PR 6), by the lead's ruling of 2026-10-02 |
| P3b-3 | `Disposal` reports through `Activity` (and an optional logger) until 4b/5b; silent with neither | §3.7, `XCUT-13` (by way of §3.7) | mechanism, interim | Position C | Dated correction to §3.7 (PR 7); accepted as interim by the lead's ruling of 2026-10-02, closing in 5b |
| P3b-4 | Seekable known-length streams are auto-promoted; every write seeks to the captured start; length never inferred | `BODY-9`, `HTTP-38` | mechanism | Rulings | Dated correction to §3.1 (PR 7) |
| P3b-5 | "Regular file" enforced only where the shared framework can see it; a Unix FIFO/device uploads as an empty body | `BODY-11`, `HTTP-40` | a MUST clause unmet on a stated domain | Rulings | New design §10 entry (PR 7), by the lead's ruling of 2026-10-02 |
| P3b-10 | The response drain's token is linked from the starting accessor and the lifetime | `BODY-22`, `BODY-26` | mechanism, departs from a §3.1 sentence | Position D | Dated correction to §3.1 (PR 7); accepted by the lead's ruling of 2026-10-02 |
| P3b-12 | The BOM strip covers every preamble-bearing encoding | `HTTP-42` | a reading, extending §11 item 34 | Rulings | Dated correction to §11 item 34 (PR 7) |
| P3b-7, P3b-8 | WHATWG form serializer; multipart rejects controls in part-header values instead of stripping (Node) | `HTTP-38`, `BODY-35`, `HTTP-51` | sibling divergence, not a spec deviation | Rulings | Noted in the vector file and `bodies.md`; no §10 entry |
