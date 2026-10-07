# Phase 5b — Logging and Redaction: Design

**Status:** Draft, for review. Written 2026-10-07 against `main` at `4130f7b` (phases 0–4c merged; branch
`62-phase-5-planning`, issue #62). 5a (configuration) and 5c (tracing and metrics) are being designed in parallel and
nothing of theirs is in the tree. Brainstormed without a human in the loop: every judgement call is taken here as a
numbered ruling (`P5b-n`) with the options considered and the reason for the choice, and the rulings are the
[Deviation Ledger](#deviation-ledger) (roadmap constraint 7). Rulings marked **open for the lead** are judgement calls the
lead may reverse; the rest are taken. The scope authority is the roadmap's Phase 5 card and Phase List row 5
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). No phase 5 segmentation design exists; [P5b-1](#deviation-ledger)
and [the census](#scope-and-the-requirement-id-census) state 5b's row split and record it for reconciliation with 5c. The
format follows the phase 4 designs (`docs/work/mvp/phase4/phase4c/2026-10-05-phase4c-pipeline-design.md` and its 4a/4b
siblings).

**What this document is.** The sub-phase design for 5b: one disposition per owned requirement row (28 rows), the public
surface 5b adds or changes as it would appear in `PublicAPI.Unshipped.txt`, the internal design of the logging path and of
the `InstrumentationPolicy` restructure it shares with 5c, the breaking changes, the PR segmentation, the `Security`
classes it keeps green, the corrections it owes the design and roadmap at close-out, and the ledger.

**What this document is not.** It is not the plan (numbered TDD tasks) and not the checklist. It does not restate design
§8.1's argument that `ILogger` replaces the reference facade (§10 entry 22, topic `no-log-event-object`) or that `Activity`
replaces the span model (§10 entry 23, topic `activity-as-tracing-model`); it cites them (constraint 9). It does not design
the operation span, `traceparent` handling or the metric instruments (5c), the options records' immutability (5a), or
configuration binding (phase 9). Where it needs something of 5a or 5c it states the interface it assumes
([Cross-sub-phase interfaces](#cross-sub-phase-interfaces)) and **the sibling's merged code wins** wherever it differs; the
plan re-derives the call sites from it (the 3a/3b and 4a/4b/4c precedent).

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor | Kind | State at `4130f7b` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030`, `MA0051` at 70 lines, `CA2007` on `src/`, `CA1848`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. 5b is written under every gate. Retiring `InstrumentationPolicy`'s `MA0051` waiver is in scope (4c hand-off). |
| Phase 1, S5 (`UrlRedactionDefaultDenyTests`) | **dependency** (constraint 5) | Met. The phase-1 hand-off table gives 5b "S5 as the checklist says"; the S5 row hands 5b `OBS-16`–`OBS-18`, `XCUT-19`(c), `OBS-7`'s truncation, the `OBS-20` guard, computing the redacted URL only when something consumes it, and pinning `XCUT-19`(e). [Security classes](#security-classes-that-must-stay-green). |
| 3a (`TeeStream`, `StreamCopy`, `CapturedBytes`) | **dependency** | Met, consumed only through 3b's wrappers. |
| 3b (`LoggingRequestBody`, `LoggingResponseBody`, `PrefixedReadStream`, `TextDecoding`, `Disposal`, the dispose latches) — the card's "5b's body logging depends on 3b" | **dependency** | Met. The 3b hand-off is taken in full: both wrappers engaged only at body level with one shared preview size (`BODY-34`), the client's `ILogger` plumbed into `Disposal`'s no-primary call sites (P3b-3's interim gap closes here), and the body swapped through `Response` without disposing the old response separately ([P5b-13](#deviation-ledger) corrects how). |
| 4b (`ExceptionFacts.IsFatal`, `ExceptionTrail`) | **dependency** | Met. The emission guard filters fatal exceptions exactly as every other SDK catch does (§10 entry 12). The 4b hand-off "5b: plumbs the client's logger into `Disposal`'s no-primary branch" is taken. |
| 4c (`ProcessCoreAsync(…, bool async)`, the call-scoped `PipelineContext`/`CallState`, `SyncPath`, the per-call `DexpaceClientOptions` overloads, the exchange-link hook on `Response`) — the card's entry criterion | **dependency** | Met. One policy body serves both paths, which is what makes `OBS-17`'s "shared by the sync and async logging paths" structural. The 4c hand-off "5b/5c: restructure `InstrumentationPolicy` (it has its `MA0051` waiver)" is taken jointly with 5c ([P5b-20](#deviation-ledger)). |
| **5a** (immutable options records) | **convenience** (the card: "5a leading 5b is soft") | Not in the tree. 5b defines `HttpLoggingOptions` itself and needs only one property on `DexpaceClientOptions` (`Logging`). If 5a has merged, 5b adds an `init` property to 5a's record; if not, 5b adds a `get; set;` property to today's class and 5a converts it with the rest ([P5b-5](#deviation-ledger)). No 5b row waits on 5a. |
| **5c** (operation span, `traceparent`, metrics) | **convenience** | Not in the tree. Both sub-phases edit `InstrumentationPolicy`. Whichever lands first performs the behaviour-preserving split of [position A](#a-the-instrumentationpolicy-split-shared-with-5c); the other rebases onto it. No 5b row consumes a 5c type: `OBS-34`'s "spans and metrics still run at `None`" is proven against today's per-attempt `Activity` and instruments, whatever names 5c gives them. |
| A phase 5 segmentation design | **convenience**, with a stated substitute | Absent. The segmentation rule applies (phase 5 spans ch.15 and ch.16). The card names each sub-phase's build list; P5b-1 states the `OBS` split and records it for the lead to reconcile against 5c's. |

**5b does not invert the roadmap's order.** Nothing here makes 5a or 5c a dependency, so no dated status note about a
reversed edge is owed (contrast 4c's P4c-2).

### What 5b consumes, as a contract

| Type (owner) | Contract 5b relies on |
|---|---|
| `LoggingRequestBody(RequestBody, int tapCap)` (3b, internal) | Mirrors the latest write up to the cap; `Snapshot()` never throws; replayability is the delegate's (`BODY-17`–`BODY-21`). |
| `LoggingResponseBody(ResponseBody, int captureCap)` (3b, internal) | Lazy, once, bounded drain; `SnapshotAsync` never throws a drain failure and `DrainFailure` exposes it; the consumer still receives every byte (`BODY-22`–`BODY-29`). 5b adds an internal sync `Snapshot(int, CancellationToken)` twin over the existing `EnsureDrained` and an optional `ILogger` for its quiet closes. |
| `TextDecoding.Decode(ReadOnlySpan<byte>, Encoding?)` (3b, internal) | Declared charset else UTF-8, replacement on malformed input, matching BOM stripped; never throws. |
| `MediaType.Charset` (2a) | `null` for an absent or unknown charset (it already swallows `ArgumentException`/`NotSupportedException`). |
| `Disposal.DisposeQuietly(Async)(resource, primary, logger)` (3b, repointed by 4b) | Already takes an optional `ILogger`; 5b supplies it. |
| `PipelineContext`/`CallState` (4c) | Call-scoped; `Options` is the call's `DexpaceClientOptions` (per-call overloads exist), `AttemptNumber` and `CancellationToken` per drive. 5b adds one internal member, `CallState.Logger`. |
| `UrlRedactor` (phase 1) | Default-deny, total, `[malformed url]` sentinel; its existing members do not change. |

---

## Governing documents, and the phase-start queries

- **Normative.** `docs/product-spec/15-instrumentation-and-observability.md` §15.1–§15.4 and §15.9, and each owned row's
  appendix-C text (read directly for all 28, and for `OBS-21`–`OBS-33` to fix the boundary with 5c). `XCUT-19`, `XCUT-20`,
  `XCUT-24` (ch.19), `BODY-17`–`BODY-34` (ch.06), `RETRY-25`, `RETRY-40`, `REDIR-18`, `REDIR-28` (the later consumers of 5b's
  vocabulary), `SEAM-24` and `ASYNC-8`–`ASYNC-12` (the context-flow neighbours of `OBS-24`), `TRANSPORT-13` (`OBS-19`'s
  owner), `CFG-14` (`OBS-35`'s key constants).
- **Design.** §8.1 (all of it, including its **As built (d45e64b)** verdict: "diverges: default-allow redaction (`OBS-12`),
  non-semconv log keys (`OBS-39`), unconditional `traceparent` stamping, no emission guard (`OBS-20`); missing: operation span,
  granularity/body preview, header redaction") and its 2026-10-07 correction (P4a-3); §8.2 (the binding tier `OBS-35` rides);
  §3.8 (the seam table); §3.1/§3.7 (the wrappers and `Disposal`); §10 entries 1, 12, 22, 23, 26 and 29; §11 items 37 and 38;
  §12's `OBS` and `XCUT` rows.
- **Styleguide.** `csharp-aspnetcore/06-logging-and-observability.md` 6.1 (structured templates), **6.2 (the `LoggerMessage`
  source generator on hot paths — departed from, P5b-3)**, 6.3 (levels; the exception object attached), 6.4 (correlation via
  `Activity`), 6.7 (redact before the sink); `csharp/10-api-design.md` 10.1 (minimal surface), `csharp/06-types-and-data-modeling.md`
  6.1 (records), `csharp/15-performance.md` (allocation on hot paths), `csharp/08-error-handling.md` (catch filters).
- **Siblings (on disk).** `ruby-sdk/docs/work/mvp/phase5/phase5b/2026-09-09-phase5b-logging-and-redaction-design.md` (its
  census and split with 5c), `ruby-sdk/docs/sdk-documentation/logging-and-redaction.md` (the header vectors, the step's
  records), `ruby-sdk/gems/dexpace-core/lib/dexpace/instrumentation/{emitter,redaction_policy,http_logging,preview}.rb` and
  `test/dexpace/instrumentation/`; `nodejs-sdk/packages/core/src/observability/{redaction,logging-step}.ts` and their tests.
  Both are read as case sources (constraint 10), not as shapes.

| Query | Result |
|---|---|
| `scripts/knowledge --origin note --brief`, `--section conflicts --brief`, `--prefix-info OBS`/`XCUT`, `--gaps OBS`/`XCUT` | **Not runnable on the authoring host** (no .NET SDK is installed; the CLI is `dotnet run`). The equivalent was read from the corpus files directly: `docs/knowledge/notes/` holds six notes (`CA1062`, `I` prefix, `Async` suffix, Shouldly, `LangVersion`, and the 4b `Outcome` closed-class notes); none touches logging. `harvested/observability.md` and `harvested/redaction-and-security.md` have empty `## Conflicts` sections, so 5b inherits no open conflict. The plan re-runs the four queries on a host with the pinned SDK as its task 0 and records any difference. |
| Gap IDs | The roadmap's gap table names no `OBS` or `XCUT` ID; every 5b row has its chapter statement in ch.15 (or ch.19). |
| Phase Status Notes addressed to 5b | Phase 1 (S5 hand-off), 3a (none), 3b (wrappers, logger plumbing, `WithBody` swap, counter names), 4b (logger into `Disposal`), 4c (`InstrumentationPolicy` restructure with 5c). Every one is taken below. |
| `docs/first-release.md` | No `OBS` entry. 5b adds none: every SHOULD it owns is built or ⏳ to a named phase. |

---

## Scope and the requirement-ID census

**5b owns 28 rows: `OBS-1`–`OBS-20`, `OBS-24` and `OBS-34`–`OBS-40` (22 MUST, 6 SHOULD: `OBS-7`, `OBS-19`, `OBS-35`,
`OBS-37`, `OBS-38`, `OBS-40`).** 5c owns the other 12 (`OBS-21`–`OBS-23`, `OBS-25`–`OBS-33`). 28 + 12 = 40, the card's
`OBS` exit. This is the Ruby port's split (its 5b design, "Scope"), kept so cross-port checklists line up; the argument for
`OBS-24` (a context *snapshot*, no span in it) and `OBS-10` (the log-side fold, not the span-side push of `OBS-23`) being
logging rows is Ruby's and holds unchanged here. **P5b-1 records the assumed split for the lead to reconcile against 5c's
own census**; if 5c claims a row listed here, the row moves and nothing in 5b's design depends on it except its test.

Legend per roadmap constraint 3. "Planned status" is what the checklist will say if the plan is built as designed.

| ID | Level | Planned status | Disposition (one line) |
|---|---|---|---|
| `OBS-1` | MUST | ✅ | Disabled path allocates and emits nothing: every emission is gated on `HttpLogLevel` then `ILogger.IsEnabled`, state is built only after both; the redacted URL is computed only when a listener or an enabled event consumes it. Zero-allocation tests in the default run. The shared-inert-event conformance step has no referent (§10 entry 22). |
| `OBS-2` | MUST | ✅ | The catalogue uses exactly `Error`/`Warning`/`Information`/`Debug` (VERBOSE → `Debug`); a test enumerates the catalogue's levels. HTTP events at `Information`, failures and diagnostics at `Warning` (P5b-4). |
| `OBS-3` | MUST | ✅ (structured form) | Keys are compile-time constants on `DexpaceLogKeys`, a test rejects an empty one in the catalogue; a `null` value is carried as `null` in the state. Rendered text shows `(null)`: the residual §10 entry 22 already names. |
| `OBS-4` | MUST | ✅ | The categorisation tag is `EventId.Name` (`http.request`, `http.response`, …); the state never carries an `event` key, so the duplicate-suppression clause is vacuous (§10 entry 22). |
| `OBS-5` | MUST | N/A (§10 entry 22, `no-log-event-object`) | Precedence between per-event, global and diagnostic-context keys is the logging provider's; the SDK contributes one key set per event. |
| `OBS-6` | MUST | ✅ | SDK-owned values are strings, integers and doubles; header lists are joined, never rendered as collections; the exception is passed as `ILogger`'s exception argument. A value or exception whose rendering throws inside the provider is caught by the `OBS-20` guard (test: an exception whose `Message` throws). |
| `OBS-7` | SHOULD | ✅ | `url.full` and each header value are truncated at 8,192 UTF-16 chars with the suffix `…[truncated]`; previews are already bounded by the preview size; numbers are exempt (P5b-15). |
| `OBS-8` | MUST | N/A (§10 entry 22) | A log call is one call; there is no event instance to emit twice. |
| `OBS-9` | MUST | N/A (§10 entry 22) | Global context is the host's (`BeginScope`, OpenTelemetry resource attributes); the SDK offers no channel of its own. |
| `OBS-10` | MUST | N/A (§10 entry 23, `activity-as-tracing-model`) | The diagnostic-context fold is `LoggerFactoryOptions.ActivityTrackingOptions`, a host allow-list; the user page recommends `TraceId \| SpanId`, the nearest form of `{trace.id, span.id}`. |
| `OBS-11` | MUST | ✅ | Phase 1 (S5); `UrlRedactionDefaultDenyTests`. 5b's header path applies the same mask on every route (P5b-9). |
| `OBS-12` | MUST | ✅ | Phase 1 (S5); the query allow-list becomes configurable through `HttpLoggingOptions.AllowedQueryParameters`, default exactly `{api-version}` (P5b-10). |
| `OBS-13` | MUST | ✅ | Phase 1 (S5); `UrlRedactionDefaultDenyTests`. |
| `OBS-14` | MUST | ✅ | Phase 1 (S5); `UrlRedactionDefaultDenyTests`. |
| `OBS-15` | MUST | ✅ | Phase 1 (S5); `UrlRedactionDefaultDenyTests.Malformed_url_text_yields_the_sentinel`. |
| `OBS-16` | MUST | ✅ | New `UrlRedactor.RedactHeaderValue(string)`: a value with a scheme that parses is redacted like a request URL; anything else keeps its path, masks any `//` userinfo, drops query and fragment and appends `?***` iff either was present; never returns the sentinel (P5b-9). Vectors ported from Ruby. |
| `OBS-17` | MUST | ✅ | URL-valued header names (`location`, `content-location`, `referer` by default) go through `RedactHeaderValue` before logging; one internal renderer serves both paths because both run one `ProcessCoreAsync` (P5b-8). |
| `OBS-18` | MUST | ✅ | Header names gated by `HttpLoggingOptions.AllowedHeaderNames` (default: 26 diagnostic, non-credential names); a disallowed header is logged as `REDACTED` or omitted per `OmitDisallowedHeaders` (default `false`) (P5b-8). |
| `OBS-19` | SHOULD | ⏳ 8b | The dropped-header verbosity policy is the transport's (`TRANSPORT-13`'s once-per-name latch), routed to 8b by the phase-1 hand-off table (row 8b, S1/S2). 5b reserves the event-ID range ([position C](#c-the-vocabulary)). |
| `OBS-20` | MUST | ✅ | Every emission site, and the preview drain feeding them, runs inside one guard: non-fatal failures are caught, one `http.instrumentation.log_failed` diagnostic is attempted, a second failure is swallowed; `Activity` and `Meter` calls are not wrapped (§11 item 37). P5b-11. |
| `OBS-24` | MUST | ✅ (by the runtime, pinned) | The diagnostic context is `Activity.Current` and the host's `BeginScope` state, both `AsyncLocal`-backed and carried by `ExecutionContext` (§8.1, verified there); the banned-API gate keeps SDK code from suppressing flow. A test proves the SDK preserves it: a caller scope and activity are visible at the `http.response` emission after the transport completes on another thread. |
| `OBS-34` | MUST | ✅ | `HttpLogLevel { None, Headers, Body }`, default `None`; at `None` no `http.request`/`http.response` event is emitted and no wrapper is constructed, while the attempt `Activity` and the instruments still record (test with `ActivityRecorder`/`MetricRecorder`). |
| `OBS-35` | SHOULD | ⏳ 9 | Layered resolution is `IConfiguration` binding of `DexpaceClientOptions.Logging.Level` in the DI package (§8.2, §10 entry 25); the section name is the caller's argument there, so no key is baked into core. An unrecognised value fails at startup rather than falling back (§10 entry 26), recorded against this SHOULD (P5b-16). |
| `OBS-36` | MUST | ✅ | One `HttpLoggingOptions.BodyPreviewSize` (default 8,192 bytes) sizes both wrappers (`BODY-34`); the caller receives every byte of an over-cap body; the preview and `*.body.preview.size` describe the capture (P5b-7, P5b-12). |
| `OBS-37` | SHOULD | ✅ | An unknown-length (`ContentLength < 0`) or `text/event-stream` response body is never wrapped, on both paths, so no capture can wait on a slow producer (P5b-12). |
| `OBS-38` | SHOULD | ✅ | Text media types decode through `TextDecoding` with the declared charset, else UTF-8, with replacement; anything else (an absent media type included) renders `[binary N bytes captured]`; empty input renders `""` (P5b-14). |
| `OBS-39` | MUST | ✅ | Stable names as public constants (`DexpaceLogEvents`, `DexpaceLogKeys`) so `PublicAPI.Unshipped.txt` records every value; `http.request`, `http.response` (success and failure) carrying `http.request.method`, `url.full` (always redacted), `http.response.status_code`, `http.response.duration_ms`, `error.type`, body-size and header keys (P5b-7). |
| `OBS-40` | SHOULD | N/A (§10 entry 22) | No reserved `event` key can collide, so the collision diagnostic has nothing to diagnose. |

**Totals: 28 rows — 21 ✅ (`OBS-1`–`OBS-4`, `OBS-6`, `OBS-7`, `OBS-11`–`OBS-18`, `OBS-20`, `OBS-24`, `OBS-34`, `OBS-36`–`OBS-39`),
5 N/A (`OBS-5`, `OBS-8`, `OBS-9`, `OBS-10`, `OBS-40`), 2 ⏳ (`OBS-19` → 8b, `OBS-35` → 9).** The N/A rows are exactly the
card's "Retired (N/A with argument): the bespoke facade … and MDC apparatus", each resting on an existing §10 entry; 5b opens
no new N/A argument.

### Work 5b does on rows other phases own

| Row (owner) | 5b's contribution | Evidence the owner cites |
|---|---|---|
| `XCUT-19`(c), (e) (phase 10) | Header logging default-deny; body logging off by default (`HttpLogLevel.None`, no wrapper constructed). (a), (b) are phase 1's; (d) is 6c's. | `HeaderLogRendererTests`, `HttpLoggingDefaultsTests` |
| `XCUT-20` (phase 10) | The emission guard and the total header redactor; listener callbacks deliberately not wrapped (§11 item 37). | `EmissionGuardTests` |
| `XCUT-24` (phase 10) | Previews are byte-capped and non-consuming by construction of 3b's wrappers, now engaged. | `BodyPreviewTests` (a 10 MB body, small cap) |
| `BODY-34` (3b, ✅ with ⏳ 5b) | Engagement only at `Body`, one shared preview size. | `BodyPreviewTests` |
| `RETRY-40`, `REDIR-18`, `REDIR-28` (6a, 6b) | The guarded emitter, the event-ID ranges and the logger on `CallState` they will emit through; 5b emits none of their events. | — (6a/6b) |

---

## Facts that shape the decisions

**Read from the as-built code at `4130f7b`** (certain):

- **R1.** `InstrumentationPolicy` computes `s_redactor.Redact(request.Url)` on every attempt before any listener or log check,
  and `Stopwatch.StartNew()` allocates a `Stopwatch` per attempt: the disabled path allocates today (design §8.1 names the
  first; the second is new).
- **R2.** Its three `[LoggerMessage]` events are `Debug`/`Debug`/`Warning` with keys `{Method}`, `{Url}`, `{StatusCode}`,
  `{ErrorType}` and default event names (`LogSendingRequest` …): not `OBS-39`'s vocabulary.
- **R3.** The only `ILogger` reaching the pipeline is `InstrumentationPolicy`'s (through `AddStandardResilience`/`CreateDefault`).
  `RetryPolicy` and `RedirectPolicy` dispose superseded responses through `Disposal` with no logger, and so do
  `LoggingResponseBody`'s quiet closes; `HttpClientExtensions`' blocking bridge has no pipeline at all.
- **R4.** `Disposal`'s event is `EventId(1, "DisposeSuppressed")`, and `SystemNetHttpClient`'s three events are ids 1–3; one
  logger shared by both sees id 1 twice.
- **R5.** `Response.WithBody` returns a new `Response` that does **not** carry the exchange links `PipelineTerminal` attached
  to the original (`AttachExchange`), and its documentation says the original "must still be disposed". Disposing the original
  disposes the body a `LoggingResponseBody` now owns; not disposing it leaves its exchange link open until `BoundedMap`
  evicts it. Neither is right ([P5b-13](#deviation-ledger)).
- **R6.** `LoggingResponseBody` has `SnapshotAsync` but no synchronous snapshot; its sync drain `EnsureDrained` exists.
- **R7.** `PipelineContext.Options` is the call's `DexpaceClientOptions` (4c's per-call overloads), so an options member is
  per-call configurable for free.

**To verify on the pinned SDK (10.0.401) in the plan's task 0** (the authoring host has no .NET SDK; each has a stated
fallback):

- **V1.** `CA1848` does not flag a direct `ILogger.Log<TState>(…)` call (it targets the `LoggerExtensions.Log*` helpers).
  *Fallback:* a scoped `#pragma` with a why-comment on the one emitter method (constraint 1).
- **V2.** On Unix, `Uri.TryCreate("/cb?code=x", UriKind.RelativeOrAbsolute, …)` yields an **absolute** `file:` URI (the
  known implicit-file-path behaviour). `RedactHeaderValue` therefore classifies "absolute" by a scheme prefix in the text,
  never by `IsAbsoluteUri` (P5b-9). *If false,* nothing changes: the scheme test is correct on every OS.
- **V3.** `Histogram<double>.Record` and `UpDownCounter<long>.Add` with a `TagList` and no listener allocate nothing, so the
  pipeline-delta form of the `OBS-1` test is meaningful. *Fallback:* the emitter-level test alone carries `OBS-1`, and the
  delta test moves to 5c's `OBS-25` row.
- **V4.** `ConfigurationBinder`'s enum conversion is case-insensitive and tolerates surrounding whitespace (input to the
  `OBS-35` ⏳ 9 note only).
- **V5.** A `record`'s `with` expression copies private instance fields (language-certain; recorded because it forbids caching
  a derived redactor on `HttpLoggingOptions`, P5b-10).
- **V6.** `Encoding.UTF8.GetString` over a cut multibyte sequence yields U+FFFD and does not throw (runtime-certain; pinned by
  an `OBS-38` test).

---

## Public API surface (`PublicAPI.Unshipped.txt`, core only)

The plan confirms the exact lines from the analyzer's code fix. `Dexpace.Sdk.Http.SystemNet` and
`Dexpace.Sdk.Serialization.SystemTextJson` do not change.

**Added:**

```text
Dexpace.Sdk.Core.Configuration.HttpLogLevel
Dexpace.Sdk.Core.Configuration.HttpLogLevel.None = 0 -> Dexpace.Sdk.Core.Configuration.HttpLogLevel
Dexpace.Sdk.Core.Configuration.HttpLogLevel.Headers = 1 -> Dexpace.Sdk.Core.Configuration.HttpLogLevel
Dexpace.Sdk.Core.Configuration.HttpLogLevel.Body = 2 -> Dexpace.Sdk.Core.Configuration.HttpLogLevel
Dexpace.Sdk.Core.Configuration.HttpLoggingOptions                                   (sealed record)
Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.HttpLoggingOptions() -> void
Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.Level.get / init                  (HttpLogLevel, default None)
Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.BodyPreviewSize.get / init        (int, default 8192; negative throws, above Array.MaxLength clamps)
Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.AllowedHeaderNames.get / init     (IReadOnlyCollection<string!>!, default the 26 names)
Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.AllowedQueryParameters.get / init (IReadOnlyCollection<string!>!, default {api-version})
Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.UrlValuedHeaderNames.get / init   (IReadOnlyCollection<string!>!, default {location, content-location, referer})
Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.OmitDisallowedHeaders.get / init  (bool, default false)
static Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.Default.get -> Dexpace.Sdk.Core.Configuration.HttpLoggingOptions!
static readonly Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.DefaultAllowedHeaderNames -> System.Collections.Generic.IReadOnlyCollection<string!>!
const Dexpace.Sdk.Core.Configuration.HttpLoggingOptions.DefaultBodyPreviewSize = 8192 -> int
  (+ the record's synthesised members: Equals, GetHashCode, ToString, PrintMembers, <Clone>$, op_Equality, op_Inequality, EqualityContract)
Dexpace.Sdk.Core.Configuration.DexpaceClientOptions.Logging.get / init (or set, P5b-5) -> Dexpace.Sdk.Core.Configuration.HttpLoggingOptions!
Dexpace.Sdk.Core.Diagnostics.UrlRedactor.RedactHeaderValue(string! value) -> string!
Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents                                       (static class)
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.HttpRequest = "http.request" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.HttpResponse = "http.response" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.LogFailed = "http.instrumentation.log_failed" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.BodyCaptureFailed = "http.instrumentation.body_capture_failed" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.DisposeSuppressed = "dexpace.dispose.suppressed" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.HttpRequestId = 100 -> int        (and HttpResponseId = 101, HttpFailureId = 102,
                                                                                         LogFailedId = 120, BodyCaptureFailedId = 121, DisposeSuppressedId = 130)
Dexpace.Sdk.Core.Diagnostics.DexpaceLogKeys                                         (static class)
const …DexpaceLogKeys.HttpRequestMethod = "http.request.method"
const …DexpaceLogKeys.UrlFull = "url.full"
const …DexpaceLogKeys.HttpRequestResendCount = "http.request.resend_count"
const …DexpaceLogKeys.HttpResponseStatusCode = "http.response.status_code"
const …DexpaceLogKeys.HttpResponseDurationMs = "http.response.duration_ms"
const …DexpaceLogKeys.ErrorType = "error.type"
const …DexpaceLogKeys.HttpRequestBodySize = "http.request.body.size"
const …DexpaceLogKeys.HttpResponseBodySize = "http.response.body.size"
const …DexpaceLogKeys.HttpRequestBodyPreview = "http.request.body.preview"
const …DexpaceLogKeys.HttpResponseBodyPreview = "http.response.body.preview"
const …DexpaceLogKeys.HttpRequestBodyPreviewSize = "http.request.body.preview.size"
const …DexpaceLogKeys.HttpResponseBodyPreviewSize = "http.response.body.preview.size"
const …DexpaceLogKeys.HttpRequestHeaderPrefix = "http.request.header."
const …DexpaceLogKeys.HttpResponseHeaderPrefix = "http.response.header."
const …DexpaceLogKeys.RedactedHeaderValue = "REDACTED"
const …DexpaceLogKeys.FailedEvent = "dexpace.instrumentation.failed_event"
```

**Unchanged:** `InstrumentationPolicy(ILogger? logger = null)`, its `Stage`, `ProcessAsync`, `Process`;
`AddStandardResilience(TimeProvider?, ILogger?)`; `DexpacePipeline.CreateDefault(…, ILogger? logger, …)`; every existing
`UrlRedactor` member; `DexpaceDiagnostics`. **Removed:** nothing public. The three private `[LoggerMessage]` partials are
internal detail.

Every new member carries a `///` summary citing its requirement ID; `HttpLoggingOptions`' remarks warn that `Body` logs
payloads verbatim up to the preview size, so body logging is for diagnosis, not production (`XCUT-19`(e)).

### Why each public addition is public (styleguide 10.1)

- `HttpLogLevel`/`HttpLoggingOptions`/`DexpaceClientOptions.Logging` — the opt-in `OBS-34` requires must be the caller's,
  and phase 9 binds it.
- `UrlRedactor.RedactHeaderValue` — `OBS-16` is a redaction primitive callers need for their own headers as much as the SDK
  does; `UrlRedactor` is already public.
- `DexpaceLogEvents`/`DexpaceLogKeys` — `OBS-39`'s "stable and predictable" is mechanised: a `const` value is recorded in
  `PublicAPI.Unshipped.txt`, so renaming an event or key is a reviewed `RS0017` diff, and consumers filter on the constants
  instead of string literals (P5b-7).

---

## Internal design

### A. The `InstrumentationPolicy` split (shared with 5c)

The 4c hand-off gives the restructure to "5b/5c". The split is behaviour-preserving and lands in whichever sub-phase's first
PR merges first (P5b-20, open for the lead); the other rebases. Its shape, fixed here so 5c can design against it:

```text
InstrumentationPolicy (public, unchanged signature; Diagnostics pillar)
  ProcessCoreAsync(request, context, next, async)        // orchestrator, < 70 lines, no MA0051 waiver
    var scope   = new AttemptScope(request, context, redactorFor(context.Options.Logging))  // readonly struct
    var span    = AttemptTelemetry.Start(scope)           // 5c: Activity, tags, traceparent; null when untraced
    var outgoing = HttpLogEmitter.OnRequest(scope, …)     // 5b: http.request; wraps the request body at Body level
    try { response = await/Run next(outgoing, span-context) }
    catch (non-fatal ex) { AttemptTelemetry.Fail(span, scope, ex); HttpLogEmitter.OnFailure(scope, ex, …); throw; }
    AttemptTelemetry.Complete(span, scope, response)      // 5c: status tag, duration histogram
    return HttpLogEmitter.OnResponse(scope, response, …)  // 5b: http.response; swaps the response body at Body level
    finally AttemptTelemetry.End(scope)                    // 5c: active-requests decrement
```

- **`AttemptScope`** (internal `readonly struct`, shared): the method name, `Stopwatch.GetTimestamp()` at entry (no
  `Stopwatch` instance, R1), `context.AttemptNumber`, and a **lazily computed redacted URL** — computed on first read and
  cached in a one-slot holder allocated only on that first read. 5c's `url.full` span tag and 5b's `url.full` log key read
  the same value, so the span and the log can never disagree, and with no listener and no enabled event nothing is computed
  (`OBS-1`, phase 1's S5 hand-off). The redactor is the one built from the call's `HttpLoggingOptions.AllowedQueryParameters`
  (P5b-10), which therefore governs the span tag too.
- **`HttpLogEmitter`** (internal static, 5b's): every log emission, the guard, the header renderer, the wrappers, the
  previews. Each method well under 70 lines.
- **`AttemptTelemetry`** (internal static, 5c's): the as-built span, tag, `traceparent` and metric code moved verbatim by the
  split, then reworked by 5c. 5b never edits it.
- `error.type` is the exception's `GetType().FullName` in both the span tag and the log key (OpenTelemetry's convention;
  the as-built log used `Name`).

### B. The emission path, the state, and the guard

**Gate order (`OBS-1`, `OBS-34`).** `options.Level == None` → return; `!logger.IsEnabled(level)` → return; only then is
state built. At `None` the emitter is three loads and two compares.

**State (P5b-3).** The two HTTP events carry a *dynamic* key set (one key per logged header name, `http.request.header.<name>`),
which neither the `[LoggerMessage]` generator (placeholders bind to C# parameters; a dotted name is `SYSLIB1014`, verified
in §8.1) nor `LoggerMessage.Define` (a fixed template) can express. They are emitted through `ILogger.Log<HttpLogRecord>`
with an internal `HttpLogRecord : IReadOnlyList<KeyValuePair<string, object?>>` built only on the enabled path, and a cached
static formatter that renders a short message (`HTTP {method} {url.full} → {status}`) from the record. The fixed-key events
— `http.instrumentation.log_failed`, `http.instrumentation.body_capture_failed`, `dexpace.dispose.suppressed` — use cached
`LoggerMessage.Define` delegates with the dotted keys (§8.1's verified form). Both forms check enablement once and allocate
nothing when disabled; `CA1848` is satisfied by both (V1). `{OriginalFormat}` is included in `HttpLogRecord` so
template-grouping sinks still group.

**Events (`OBS-39`, `OBS-2`; P5b-4, P5b-7).**

| Event (`EventId`) | Level | When | Keys |
|---|---|---|---|
| `http.request` (100) | `Information` | before the continuation | `http.request.method`, `url.full`, `http.request.resend_count`, each request header (`http.request.header.<lower-name>`), `http.request.body.size` when the declared length is ≥ 0 |
| `http.response` (101) | `Information` | after the continuation returns (and, at `Body`, after the response preview is captured) | `http.response.status_code`, `http.response.duration_ms` (double), `url.full`, `http.request.resend_count`, each response header, `http.response.body.size` when declared ≥ 0; at `Body`: `http.request.body.preview`/`.preview.size`, `http.response.body.preview`/`.preview.size` |
| `http.response` (102, failure) | `Warning` | when the continuation throws a non-fatal exception | `error.type`, `http.response.duration_ms`, `url.full`, `http.request.resend_count`; at `Body`: the request preview (`BODY-20`'s bytes mirrored to the failure point); the exception as `ILogger`'s exception argument. No response preview: there is no response. |
| `http.instrumentation.log_failed` (120) | `Warning` | the guard caught a failure | `dexpace.instrumentation.failed_event` (the event that failed), `error.type`; the exception attached |
| `http.instrumentation.body_capture_failed` (121) | `Warning` | a response drain failed (`DrainFailure` set after the snapshot) | `error.type`; the exception attached. The consumer still sees the failure on its read (`BODY-26`); the `http.response` event still emits with the partial preview |
| `dexpace.dispose.suppressed` (130) | `Warning` | `Disposal`'s no-primary branch | `dexpace.dispose.resource_type`, `error.type` (never a message or a value, as today) |

The request preview rides on the response or failure event because the request body is written inside the continuation,
after `http.request` (Ruby's finding, adopted). Reserved ranges for later phases, so their events never collide: 110–119
`OBS-19`/`TRANSPORT-13` header drops (8b), 140–149 retry (6a, `RETRY-40`), 150–159 redirect (6b, `REDIR-18`, `REDIR-28`),
160–169 auth (6c). `SystemNetHttpClient`'s ids 1–3 move into 110–119 in 8b (P5b-21).

**The guard (`OBS-20`, `XCUT-20`; P5b-11).** One internal method, `HttpLogEmitter.Guard`, wraps each emission:

```text
try { emit(); }
catch (Exception ex) when (!ExceptionFacts.IsFatal(ex) && !(ex is OperationCanceledException && token.IsCancellationRequested))
{
    try { s_logFailed(logger, failedEventName, ex.GetType().FullName, ex); }
    catch (Exception) when (!ExceptionFacts.IsFatal(...)) { /* OBS-20: a secondary failure is swallowed */ }
}
```

The `CA1031` suppressions are scoped `#pragma`s with a why-comment citing `OBS-20`. A fatal exception propagates unlogged
(`RETRY-25`, §10 entry 12). Cancellation of the call's own token during the preview drain propagates (`XCUT-3`): the policy
then disposes the response it still owns through `Disposal.DisposeQuietlyAsync(response, oce, logger)` before rethrowing.
`AttemptTelemetry` calls are **outside** the guard: a throwing `ActivityListener` or `MeterListener` propagates (`OBS-20`'s
second sentence, `OBS-30`, §11 item 37), and a test pins that asymmetry.

### C. The vocabulary

`DexpaceLogEvents` and `DexpaceLogKeys` (public, [above](#public-api-surface-publicapiunshippedtxt-core-only)) are the only
place a name or key is spelled; `HttpLogEmitter` and `Disposal` reference the constants. A `Unit` test asserts the catalogue:
every emitted `EventId.Name` is a `DexpaceLogEvents` constant, every state key is a `DexpaceLogKeys` constant or begins with a
header prefix, no key is empty (`OBS-3`), no state carries a key named `event` (`OBS-4`), and every level is one of the four
(`OBS-2`). Header keys are the lower-cased name (OpenTelemetry's `http.request.header.<key>` form); a multi-valued header is
one key whose value is the individually redacted values joined with `", "` (Ruby's P5-108: a second `Location` meets the
redactor on its own).

### D. Header rendering and URL-valued headers

`HeaderLogRenderer` (internal, stateless, built once per distinct `HttpLoggingOptions` instance and cached beside the
redactor, P5b-10) holds `FrozenSet<string>`s (`StringComparer.OrdinalIgnoreCase`) of the allowed and URL-valued names. For
each header in insertion order: not allowed → `REDACTED` or omitted (`OBS-18`); allowed and URL-valued → each value through
`RedactHeaderValue` (`OBS-16`, `OBS-17`); otherwise the value verbatim; then `OBS-7` truncation. Request headers are the ones
the attempt sends, so a stamped `Authorization` is present and rendered `REDACTED` under the default list.

**`UrlRedactor.RedactHeaderValue(string value)`** (`OBS-16`, P5b-9): total, never the `[malformed url]` sentinel.

1. Empty → `""`.
2. The text begins with an RFC 3986 scheme (`ALPHA *( ALPHA / DIGIT / "+" / "-" / "." ) ":"`), is verbatim-safe, parses
   with `UriKind.Absolute`, and `Redact(Uri)` returns something other than the sentinel → that result (redacted "exactly like
   a request URL").
3. Otherwise (relative, or unparseable): every `//authority` userinfo in the text becomes `***:***@` (`OBS-11`'s
   "unconditionally" overrides `OBS-16`'s "verbatim", Ruby's P5-100); the text is cut at the first `?` or `#`; `?***` is
   appended iff a cut happened. `/cb?code=SECRET` → `/cb?***`; `/cb?` → `/cb?***`; `/static/path` → unchanged;
   `//user:secret@h/x` → `//***:***@h/x`.

Classifying by the scheme prefix rather than `Uri.IsAbsoluteUri` is load-bearing on Unix (V2).

**Default allowed header names (26, Ruby's list, P5b-8):** `accept`, `accept-encoding`, `cache-control`, `connection`,
`content-encoding`, `content-length`, `content-location`, `content-type`, `date`, `etag`, `expires`, `if-match`,
`if-modified-since`, `if-none-match`, `if-unmodified-since`, `last-modified`, `location`, `retry-after`, `server`,
`traceparent`, `tracestate`, `user-agent`, `vary`, `via`, `x-correlation-id`, `x-request-id`. No credential-bearing name
(`authorization`, `proxy-authorization`, `cookie`, `set-cookie`, `x-api-key`, …) is in it, which a test asserts against a
deny-list of known credential headers.

### E. Body level: engagement, capture, preview

At `HttpLogLevel.Body` only (`OBS-34`, `BODY-34`), with `cap = options.BodyPreviewSize`:

- **Request.** When `request.Body` is non-null, the request passed down is `request.WithBody(new LoggingRequestBody(body, cap))`.
  Retry and redirect sit above the Diagnostics stage and re-send the request *they* hold, so every attempt wraps afresh and
  the tap reflects that attempt only (`BODY-18`). After the continuation (success or failure), `Snapshot(cap)` is rendered.
- **Response.** When the response body's `ContentLength >= 0` and its media type is not `text/event-stream` (`OBS-37`,
  P5b-12), the response is re-bodied with `new LoggingResponseBody(inner, cap, logger)` through an internal ownership-moving
  swap (P5b-13), then `SnapshotAsync(cap, token)` (or the new sync twin, R6) captures at most `cap + 1` bytes before the
  `http.response` event. Over the cap, the consumer gets the prefix then the live tail (`BODY-24`, `OBS-36`); a drain failure
  is cached for the consumer (`BODY-26`) and reported as `http.instrumentation.body_capture_failed`.
- **`*.body.preview.size`** is the captured byte count; **`*.body.size`** stays the declared length when known (P5b-7).
- **Preview rendering (`OBS-38`, P5b-14).** Text when the media type is `text/*`; `application/json`, `xml`,
  `x-www-form-urlencoded`, `javascript`, `x-ndjson`, `yaml`, `graphql`; or a `+json`/`+xml`/`+yaml` suffix. Decoded by
  `TextDecoding.Decode(bytes, mediaType.Charset)` (unknown charset → UTF-8, replacement, never throws). Anything else,
  `multipart/*` and an absent media type included → `[binary N bytes captured]`. Empty → `""`.

### F. The logger on the call, and `Disposal`

`HttpPipeline` takes its logger from its **Diagnostics pillar**: at `Build`, if the `Diagnostics` entry is an
`InstrumentationPolicy`, its logger; otherwise `NullLogger.Instance` (P5b-6). `CallState` gains `internal ILogger Logger`.
Every no-primary `Disposal` call site in a policy passes `context.State.Logger` (`RetryPolicy`, `RedirectPolicy`); the
response wrapper receives the emitter's logger at construction for its quiet closes. `HttpClientExtensions`' blocking bridge
has no pipeline and stays Activity-only — the one residual of P3b-3's gap, documented on `Disposal` and in the user page.
`Disposal`'s event moves to `dexpace.dispose.suppressed` (130) (R4).

### G. Options

```csharp
public sealed record HttpLoggingOptions
{
    public static HttpLoggingOptions Default { get; } = new();
    public HttpLogLevel Level { get; init; }                               // None
    public int BodyPreviewSize { get; init { /* < 0 throws ArgumentOutOfRangeException (BODY-32); clamp to Array.MaxLength */ } } = DefaultBodyPreviewSize;
    public IReadOnlyCollection<string> AllowedHeaderNames { get; init; } = DefaultAllowedHeaderNames;
    public IReadOnlyCollection<string> AllowedQueryParameters { get; init; } = UrlRedactor.DefaultQueryAllowList;
    public IReadOnlyCollection<string> UrlValuedHeaderNames { get; init; } = DefaultUrlValuedHeaderNames;
    public bool OmitDisallowedHeaders { get; init; }
}
```

`init` accessors copy the caller's collection into an immutable array (so a caller mutating its list later changes nothing,
`CFG-8`) and reject `null` elements. **Nothing derived is cached on the record**: `with` copies private fields (V5), so a cached
redactor would survive a `with` that changed `AllowedQueryParameters`. The policy instead keeps a one-slot cache — a
`volatile` reference to an immutable `(HttpLoggingOptions key, UrlRedactor, HeaderLogRenderer)` triple — rebuilt when the
call's options instance differs by reference (`XCUT-11`: no lock, the worst race builds twice). Record equality over the
collection members is reference equality; `CFG-33`'s deep equality is 5a's to decide for every options record, and 5b's
record follows whatever 5a decides.

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes and gets a `CHANGELOG.md` `[Unreleased]` line in the PR
that makes it (constraint 8).

| # | Change | Kind | PR |
|---|---|---|---|
| 1 | `InstrumentationPolicy` emits no request/response log event unless `DexpaceClientOptions.Logging.Level` is `Headers` or `Body` (was: always, at `Debug`) | behaviour | 3 |
| 2 | The events are renamed and re-keyed: `http.request`/`http.response` with OpenTelemetry keys and ids 100–102 (was: generated names, `{Method}`/`{Url}`/`{StatusCode}` keys, ids 1–3); request/response at `Information` (was `Debug`); `error.type` is the full type name | behaviour | 3 |
| 3 | A logger that throws no longer fails the request; the failure surfaces as `http.instrumentation.log_failed` | behaviour | 3 |
| 4 | `Disposal`'s warning is `dexpace.dispose.suppressed` (130) (was `DisposeSuppressed` (1)) and now reaches the pipeline's logger from retry, redirect and the response wrapper | behaviour | 3 |
| 5 | At `Body`, a response with a known-length body comes back with a wrapped body: up to the preview size is read before `SendAsync` returns; a body that fits is served from memory (fresh views, so it can be opened again); `ContentLength` follows `BODY-29` | behaviour, opt-in | 4 |
| 6 | The span tag `url.full` and the log key are redacted with the call's `AllowedQueryParameters` (default unchanged: `{api-version}`) | behaviour, default-preserving | 2 |

Additive, with no **Breaking** marker: `HttpLogLevel`, `HttpLoggingOptions`, `DexpaceClientOptions.Logging`,
`UrlRedactor.RedactHeaderValue`, `DexpaceLogEvents`, `DexpaceLogKeys`.

---

## PR segmentation

**One pull request per step, each carrying its code and its tests** (roadmap step 5's allowance, as 2a–4c used it), not a
code → tests → docs stack: each step adds public surface that `RS0016` and the coverage gate require to land with its tests,
so a code-only PR would land untested public members, and a tests-first PR would not compile. Docs ride in the close-out PR.

| PR | Content | Rows | Depends on |
|---|---|---|---|
| **1** | The `InstrumentationPolicy` split ([position A](#a-the-instrumentationpolicy-split-shared-with-5c)): `AttemptScope` with the lazy redacted URL and `Stopwatch.GetTimestamp`; `AttemptTelemetry` (the as-built span/metric code moved verbatim); `HttpLogEmitter` holding the as-built three log calls unchanged; the `MA0051` waiver removed. Behaviour-preserving; every existing `InstrumentationPolicyTests` assertion unchanged. **If 5c has already merged its split, this PR is dropped.** | `OBS-1` (the lazy URL half) | nothing (5c coordination, P5b-20) |
| **2** | Redaction and options: `UrlRedactor.RedactHeaderValue`; `HeaderLogRenderer`; `OBS-7` truncation; `HttpLogLevel`, `HttpLoggingOptions`, `DexpaceClientOptions.Logging`; the redactor/renderer cache; `DexpaceLogEvents`, `DexpaceLogKeys`. Header vectors as JSON under `tests/vectors/redaction/header-values.json` (constraint 10), ported from the Ruby page's 17 header cases and Node's `redaction.test.ts`, each citing its source. | `OBS-7`, `OBS-12` (configurable), `OBS-16`–`OBS-18` | PR 1 (or 5c's split); 5a for the property's accessor form only |
| **3** | The emitter: `HttpLogRecord`, the three HTTP events, the guard and the two diagnostics, levels; `CallState.Logger` and the `Disposal` plumbing; the catalogue test; the zero-allocation tests; the `OBS-24` flow test; the `OBS-34` none-level test (spans and instruments still record). | `OBS-1`–`OBS-4`, `OBS-6`, `OBS-20`, `OBS-24`, `OBS-34`, `OBS-39` | PR 2 |
| **4** | Body level: wrapper engagement, the ownership-moving body swap, the sync snapshot twin, the unknown-length/SSE skip, the preview renderer, the drain-failure diagnostic; the `XCUT-24` 10 MB test. | `OBS-36`–`OBS-38` (and `BODY-34`'s ⏳ 5b clause) | PR 3 |
| **5** | Close-out: `docs/sdk-documentation/logging-and-redaction.md`; the 5b checklist (28 rows plus the styleguide audit groups); the AOT smoke extended (a `Body`-level call over `CreateDefault` with a JSON and a binary body); `CHANGELOG.md` already carries each PR's lines; the [corrections owed](#corrections-owed-at-close-out); the roadmap status note; the knowledge note for the styleguide 6.2 departure. | all 28 (closing) | 1–4 |

---

## Security classes that must stay green

Constraint 5: green, or moved without weakening.

| Class | Edit | Why |
|---|---|---|
| `UrlRedactionDefaultDenyTests` (S5, Core) | **None.** | `Redact(Uri)`, `Redact(string)`, both constructors and `DefaultQueryAllowList` keep their signatures and behaviour; `RedactHeaderValue` is a new member beside them. The policy's switch to an options-built redactor uses the same constructor with the same default list. |
| `ReDriveRequestIsolationTests` (S6, Core) | **None.** | Its probes run at `PerHop` with the default `Level = None`, so no wrapper is constructed and the request the retry holds is untouched. At `Body` the wrapper is applied to the request *passed down* only, never written upward (`PIPE-16`); a new `Unit` test pins that the held request's body is not the wrapper. |
| `EnsureSuccessErrorMappingTests` (S8, Core) | **None.** | `EnsureSuccess(Async)` is not touched; at `Body` it would read a wrapped body, which serves every byte. |
| `RedirectCredentialHygieneTests`, `AuthHttpsGuardTests`, `RetryPacingOverflowTests`, `HeaderInjectionValidationTests`, `MediaTypeTryParseTests` (Core) | **None.** | No code they exercise changes beyond `Disposal` receiving a logger, which they do not assert on. |
| SystemNet `FramingHeaderDropWireTests` (and its `RecordingLogger`), `HeaderInjectionWireTests`, `MalformedContentTypeWireTests`, `RedirectWireTests` | **None.** | 5b does not touch `SystemNetHttpClient`; its event ids move in 8b (P5b-21), and the framing test asserts on level and name, not on the id. |

**No `Security` class is added.** 5b fixes no phase-1 defect; the `XCUT-19`(c) header default-deny and (e) body-off tests are
`Unit` (P5b-23).

---

## Tests, vectors and ports

- **Categories.** `Unit` throughout `tests/Dexpace.Sdk.Core.Tests` (fakes only; `SEAM-2` holds). `AotSmoke` for PR 5. No
  wire test is needed: nothing 5b adds reaches the socket.
- **Test support** (`tests/Dexpace.Sdk.TestSupport/Diagnostics/`): `RecordingLogger` (captures level, `EventId`, the state's
  key/value pairs and `Activity.Current` at the call, with an `AsyncLocal` scope stack for the `OBS-24` test), `ThrowingLogger`
  (configurable: `IsEnabled` throws, `Log` throws, `Log` throws only for the first n calls), `DisabledLogger`. The SystemNet
  test project's private `RecordingLogger` stays where it is.
- **Zero allocation (`OBS-1`).** Measured with `GC.GetAllocatedBytesForCurrentThread()` after warm-up: (a) every emitter
  method under `Level = None`, under a `DisabledLogger`, and under `NullLogger` — 0 bytes; (b) a pipeline over a
  synchronously completing fake transport and a cached response, with `InstrumentationPolicy` versus a pass-through policy
  at `Diagnostics`, no listener, `Level = None` — delta 0 bytes (V3's fallback applies if the instruments allocate).
- **Ported, each with a header comment citing path and sha:** Ruby's header-value cases
  (`logging-and-redaction.md` §Redaction, and `test/dexpace/instrumentation/redactor_test.rb`'s `header_value` cases), its
  `preview_test.rb` cases (ISO-8859-1, binary, `+json`, cut multibyte, unknown charset), its step records (the two event
  shapes, adapted to the keys of P5b-7); Node's `redaction.test.ts` header cases and `logging-step.test.ts` containment cases;
  Node's `tests/conformance/xcut/diagnostic-previews` scenarios where present.
- **Not ported:** Ruby's `Event`/`Logger`/`NULL_SINK` tests and Node's `LogEventBuilder` tests (no event object, §10 entry 22);
  any test asserting a host-language fact (constraint 10).

---

## Cross-sub-phase interfaces

Stated so the lead can reconcile the three phase 5 designs. Each is also a ledger entry.

| With | 5b assumes | 5b provides | Ledger |
|---|---|---|---|
| **5c** | 5c owns `OBS-21`–`OBS-23`, `OBS-25`–`OBS-33`; the operation `Activity`, `traceparent` handling, the instruments and `server.port`. | The split shape of position A; `AttemptScope`'s lazy redacted URL (5c's `url.full` tag reads it) and entry timestamp; `error.type` as the full type name; the guard never wraps 5c's calls. | P5b-1, P5b-10, P5b-19, P5b-20 |
| **5c** | The per-attempt `Activity` stays at the Diagnostics stage, created before the `http.request` event, so a provider with `ActivityTrackingOptions` stamps the attempt span's ids on the HTTP events. | — | P5b-20 |
| **5a** | `DexpaceClientOptions` becomes a sealed record with `init` and nested records (`CFG-8`, `CFG-9`); per-call overloads stay. | `HttpLoggingOptions` (a sealed record, so 5a's shallow-`with` precondition holds) and the `Logging` property to add to 5a's record. | P5b-5 |
| **5a** | 5a decides `CFG-33` deep equality for options records. | 5b's record follows it. | P5b-5 |
| **6a/6b/6c, 8b** | — | `CallState.Logger`, the guard, the event-id ranges; 8b re-ids `SystemNetHttpClient`'s events. | P5b-6, P5b-21 |
| **9** | The DI package binds `Dexpace:Logging:*` into `HttpLoggingOptions` with the section name as an argument. | `OBS-35`'s row ⏳ 9. | P5b-16 |

---

## Corrections owed at close-out

Dated corrections, written in PR 5, never silent edits (design is frozen to routine work):

- **Design §8.1.** "it moves to `Define` delegates with the semconv keys" → `Define` for the fixed-key events and an
  `ILogger.Log<TState>` record for the header-bearing HTTP events (P5b-3); "**OBS-2**'s mapping puts request/response events
  at `Debug`" → `Information` (P5b-4); the **As built** line gains a 5b verdict.
- **Design §10 entry 22.** Add `OBS-10`'s and `OBS-24`'s pointers where they rest on entry 23, unchanged in substance; no new
  entry is opened (no 5b ruling leaves a MUST's letter unmet beyond what entries 22, 23 and 26 already record).
- **Design §12.** The `OBS` row's notes gain P5b-3's mechanism and the 28/12 split.
- **`Response.WithBody`'s XML doc** stays as is for callers; the internal swap is documented on the internal member (P5b-13).
  The 3b status note's "(the old response must not be disposed separately)" is satisfied, by a different mechanism than
  `WithBody`; the 5b roadmap status note says so.
- **3b checklist, `BODY-34`.** Its ⏳ 5b clause is closed by 5b's `BodyPreviewTests`; the 5b checklist row cites it and the
  status note records it (the 3b checklist is not edited).
- **Styleguide departure.** A note under `docs/knowledge/notes/` (role `review`) records that SDK HTTP events use
  `LoggerMessage.Define`/`ILogger.Log<TState>` instead of 6.2's source generator, because the generator cannot express
  OpenTelemetry's dotted keys (constraint 6), and the SDK overlay gains the row.
- **`CLAUDE.md`'s "What is genuinely unbuilt"** drops "body/header logging" from phase 5's entry.

---

## Risks and open questions

- **The response preview adds latency at `Body`.** Up to `cap + 1` bytes are read before `SendAsync` returns. Opt-in and
  bounded; P5b-12 records the alternative (emit `http.response` without the response preview and a second event when the
  consumer reads) and why it was rejected.
- **Double logging under nesting.** A pipeline used as another's transport logs at both Diagnostics stages; that is the
  caller's composition, documented on the user page.
- **`HttpLogRecord` allocation on the enabled path** scales with the header count. Acceptable: the caller opted in, and the
  record is one array plus the joined strings.

---

## Deviation Ledger

Each entry is a ruling with the options considered, the choice and why. **Open for the lead** marks a judgement call the lead
may reverse; the others are taken. P5b-1, P5b-5, P5b-6, P5b-10, P5b-19 and P5b-20 are the cross-sub-phase interfaces.

**P5b-1 — The `OBS` split with 5c (28/12). Open for the lead.** 5b owns `OBS-1`–`OBS-20`, `OBS-24`, `OBS-34`–`OBS-40`;
5c owns `OBS-21`–`OBS-23`, `OBS-25`–`OBS-33`. *Options:* (a) Ruby's split, which this is; (b) give `OBS-10` and `OBS-24` to 5c
because on .NET both are `Activity`/`ExecutionContext` behaviour; (c) give `OBS-34` to 5c because its conformance clause
asserts spans and metrics. *Chosen:* (a). `OBS-10` and `OBS-24` are about what reaches a *log event*, and `OBS-34`'s subject
is the log level; keeping Ruby's split keeps the cross-port checklists comparable. *Interface:* if 5c's census claims any of
these rows, the row moves with its test and 5b's design is otherwise unaffected.

**P5b-2 — Ordering.** 3a, 3b, 4b and 4c are dependencies (met); 5a and 5c are conveniences. *Options:* make 5a a dependency
because `DexpaceClientOptions.Logging` lands on its record. *Chosen:* convenience — 5b adds the property to whatever shape is
on `main` and 5a converts it with the rest; no 5b row waits.

**P5b-3 — HTTP events through an `ILogger.Log<TState>` record; fixed-key events through `LoggerMessage.Define`. Open for the
lead.** *Options:* (a) `[LoggerMessage]` generator (styleguide 6.2) — cannot express dotted keys (`SYSLIB1014`, §8.1);
(b) `Define` for everything, with headers in one key whose value is a rendered string or a list — loses per-header
attributes, which OpenTelemetry's log bridge maps one key to one attribute; (c) a record type implementing
`IReadOnlyList<KeyValuePair<string, object?>>`, built only after the enablement checks, for the two header-bearing events,
`Define` for the rest. *Chosen:* (c) — the only form that carries `http.request.header.<name>` as attributes, and it keeps
`OBS-1` (nothing is built when disabled). A departure from styleguide 6.2 recorded as a knowledge note and an overlay row;
a dated correction to §8.1's "moves to `Define` delegates".

**P5b-4 — Levels: `Information` for `http.request`/`http.response`, `Warning` for the failure event and the diagnostics. Open
for the lead.** *Options:* (a) as built, `Debug`/`Warning` — a caller who opts in at `Headers` must also lower the category
level, two switches for one intent; (b) the siblings' `Information`/`Error`; (c) `Information`/`Warning`. *Chosen:* (c).
`HttpLogLevel` is the opt-in, so the events sit at styleguide 6.3's "normal milestones" level; an attempt failure is often
retried, which is 6.3's "recoverable anomaly" (`Warning`), not a failed operation — the operation's outcome is 5c's span
status. Diverges from Ruby and Node on the failure level; recorded on the user page.

**P5b-5 — `HttpLoggingOptions` is a sealed record nested as `DexpaceClientOptions.Logging`. Open for the lead (5a
interface).** *Options:* (a) a constructor argument of `InstrumentationPolicy`; (b) a property of the client options.
*Chosen:* (b) — per-call configurable through 4c's overloads (R7), bound by phase 9 under the same section as retry and
redirect, and consistent with the nested-record shape 5a is making immutable. *Interface:* 5a's record gains
`HttpLoggingOptions Logging { get; init; } = HttpLoggingOptions.Default`; if 5b lands first it adds `{ get; set; }` to today's
class and 5a converts it. 5b's record follows 5a's `CFG-33` equality decision.

**P5b-6 — The pipeline's logger is its Diagnostics pillar's logger. Open for the lead.** *Options:* (a) a public
`PipelineBuilder.WithLogger(ILogger)`; (b) a public `PipelineContext.Logger`; (c) at `Build`, take the logger of the
`InstrumentationPolicy` in the Diagnostics pillar, exposed internally as `CallState.Logger`. *Chosen:* (c) — zero new public
surface; the spec's LOGGING pillar (`PIPE-2`) is the natural owner of the logger, and both `CreateDefault` and a hand-built
pipeline already hand their logger to that policy. *Residual:* a pipeline with no `InstrumentationPolicy`, and
`HttpClientExtensions`' blocking bridge, report dispose failures to `Activity` only; P3b-3's accepted gap narrows to those two.
6a/6b/6c emit through `CallState.Logger`.

**P5b-7 — The vocabulary: public constants, OpenTelemetry keys, ids 100–102/120/121/130, declared `*.body.size` plus
`*.body.preview.size`. Open for the lead.** *Options for stability:* string literals in the emitter (Ruby/Node keep
constants internal) versus public `const`s recorded in `PublicAPI.Unshipped.txt`; *chosen:* public, so a rename is a reviewed
`RS0017` diff and consumers filter on constants. *Options for the size key:* (a) Ruby's — at `Body`, `*.body.size` is the
captured size; (b) `*.body.size` is always the declared length (OpenTelemetry's meaning) and a separate
`*.body.preview.size` carries the capture. *Chosen:* (b) — `OBS-36`'s "size field reflects the preview" is met by the preview
size key, and the OpenTelemetry key keeps its meaning at every level. Diverges from Ruby; recorded. Header values are joined
with `", "`, each value redacted first.

**P5b-8 — Header defaults: Ruby's 26 allowed names, `REDACTED` marker by default, URL-valued `{location, content-location,
referer}`.** *Options:* Node's 4-name list (too narrow to diagnose caching or tracing); a .NET-specific list. *Chosen:* Ruby's
26 for cross-port parity, none credential-bearing. Marker over omission by default (Ruby P5-35: present-and-redacted and
absent are different facts). `referer` is added to the URL-valued set beyond the spec's minimum because a caller who
allow-lists it would otherwise log a query verbatim.

**P5b-9 — `RedactHeaderValue` is public, total, sentinel-free, and classifies by scheme prefix.** *Options:* reuse
`Redact(string)` (returns the sentinel and keeps relative query names, both against `OBS-16`); classify by
`Uri.IsAbsoluteUri` (wrong on Unix, V2). *Chosen:* a separate entry point (Ruby's R9: `OBS-15` and `OBS-16` answer the same
parse failure differently), with `OBS-11`'s mask applied on every route.

**P5b-10 — One redaction policy per call, built from the call's options, shared with 5c's span tag (5c interface).**
*Options:* (a) the static default redactor for the span and the configured one for logs; (b) one redactor per call, from
`HttpLoggingOptions.AllowedQueryParameters`, for both. *Chosen:* (b) — a span and a log line about the same attempt cannot
disagree about `url.full`. The redactor and header renderer are cached in a one-slot, reference-keyed holder on the policy,
never on the record (V5: `with` copies private fields). *Interface:* 5c reads `AttemptScope.RedactedUrl`.

**P5b-11 — The guard's semantics.** Catch non-fatal exceptions (`ExceptionFacts.IsFatal`, §10 entry 12) except an
`OperationCanceledException` from the call's own cancelled token; attempt one `http.instrumentation.log_failed` with the
exception; swallow a second failure. `Activity` and `Meter` calls are outside it (`OBS-20`, `OBS-30`, §11 item 37). *Options:*
design §8.1's "excludes `OperationCanceledException`" unconditionally (rejected: a logger throwing an unrelated OCE would fail
the request); Node's `Debug`-level diagnostic (rejected: a swallowed failure is an operator-visible anomaly). Taken.

**P5b-12 — Body-level capture is eager before `http.response`, and skipped for unknown-length and `text/event-stream` bodies
on both paths. Open for the lead.** *Options:* (a) eager snapshot (Ruby); (b) emit `http.response` without the response
preview and emit a later preview event when the consumer reads (no added latency, but two events per response and none if
the consumer never reads); (c) skip only on the async path, as `OBS-37` literally says. *Chosen:* (a) with the skip on both
paths. On .NET the async drain parks no thread, but it still delays `SendAsync`'s return until `cap + 1` bytes arrive, which
for a stream that idles (SSE, long-poll, chunked downloads) is unbounded; the sync path has the same hazard and a blocked
thread on top. Stronger than the SHOULD.

**P5b-13 — The response body swap moves ownership and exchange links.** `Response.WithBody` leaves the original's exchange
links behind and documents that the original must be disposed (R5), which here would dispose the body the wrapper owns.
*Options:* (a) `WithBody` and never dispose the original (leaks the exchange link until `BoundedMap` evicts it); (b) change
`WithBody`'s public contract; (c) an internal `Response.ReplaceBody(ResponseBody)` that returns a new response owning the new
body, moves the exchange links to it, and latches the original as disposed without disposing its body. *Chosen:* (c) — no
public contract changes, and `CTX`'s close-on-dispose survives the swap. Taken.

**P5b-14 — Preview rendering.** The text set of [position E](#e-body-level-engagement-capture-preview); `TextDecoding` with
`MediaType.Charset` (unknown → UTF-8); `multipart/*` and an absent media type render as binary. *Options:* sniffing bytes for
text (rejected: a guess, and `OBS-38` keys on the media type). Taken.

**P5b-15 — `OBS-7` truncation at 8,192 UTF-16 chars with `…[truncated]`, on `url.full` and header values only.** *Options:* a
byte cap (Ruby; needs a UTF-8 boundary-safe cut for no gain in .NET, where strings are UTF-16); truncating previews too
(redundant: the preview size bounds them). Taken.

**P5b-16 — `OBS-35` is ⏳ 9, and its fall-back-to-default clause yields to fail-fast. Open for the lead.** The layered lookup
is the DI package's binding (§8.2, §10 entry 25); an unrecognised level fails `ValidateOnStart` rather than falling back,
which is §10 entry 26's position applied to a SHOULD. *Options:* ship a tolerant public `HttpLogLevel` parser in core
(rejected: no core caller, and phase 9 owns conversion). Phase 9's checklist owns the row's evidence; V4 informs its
converter.

**P5b-17 — `OBS-19` is ⏳ 8b.** The drop-verbosity policy belongs to the transport that drops (`TRANSPORT-13`), routed to 8b
by the phase-1 hand-off table. 5b reserves ids 110–119. Taken.

**P5b-18 — The N/A rows rest on existing §10 entries.** `OBS-5`, `OBS-8`, `OBS-9`, `OBS-40` on entry 22
(`no-log-event-object`); `OBS-10` on entry 23 (`activity-as-tracing-model`). `OBS-24` is ✅ by the runtime with a
flow-preservation test rather than N/A, because the SDK *can* break it (a banned flow suppressor) and the test pins that it
does not. No new §10 entry is opened. Taken.

**P5b-19 — The `OBS-1` test design and the disabled path (5c interface).** The emitter-level zero-allocation test is the
row's proof; the pipeline-delta test is added when V3 holds. The disabled path's two as-built allocations (R1) are removed by
the split: the URL is computed lazily in `AttemptScope`, and `Stopwatch.GetTimestamp` replaces `Stopwatch.StartNew`.
*Interface:* 5c's `OBS-25` untraced-path test can reuse the delta harness.

**P5b-20 — The `InstrumentationPolicy` split is done once, by whichever of 5b or 5c lands first. Open for the lead (5c
interface).** *Options:* (a) 5b splits; (b) 5c splits; (c) whoever lands first, to the shape fixed in position A. *Chosen:* (c)
— both designs are parallel and neither should wait; the shape (orchestrator, `AttemptScope`, `HttpLogEmitter`,
`AttemptTelemetry`) is fixed here so the second lander rebases onto known seams. The split PR retires the `MA0051` waiver.
The per-attempt `Activity` starts before the `http.request` event so log correlation sees the attempt span.

**P5b-21 — Event-id ranges.** Core's events take 100–169 by subsystem; `Disposal` moves from id 1 to 130 (R4); 8b moves
`SystemNetHttpClient`'s 1–3 into 110–119. *Options:* leave ids to each emitter (the as-built collision). Taken.

**P5b-22 — PR segmentation: one PR per step with code and tests, docs at close-out.** *Options:* the roadmap's code → tests →
docs stack (rejected: new public surface cannot land without its tests under the coverage gate and `RS0016`). Taken.

**P5b-23 — No `Security` class is added or edited.** The `XCUT-19`(c)/(e) tests are `Unit`. *Options:* tag the header
default-deny test `Security` (rejected: the category is the permanent phase-1 regression set, constraint 5, and 5b fixes no
phase-1 defect). The lead may promote it. Taken.

**P5b-24 — The `XCUT` rows stay phase 10's.** 5b supplies the evidence for `XCUT-19`(c)/(e), `XCUT-20` and `XCUT-24`
([work on other rows](#work-5b-does-on-rows-other-phases-own)) and owns none, as the Phase List places them. Taken.

**P5b-25 — The request preview rides on the response or failure event.** The request body is written inside the continuation,
after `http.request`, so a preview read at `http.request` would always be empty (Ruby's finding). Taken.

| ID | Kind | Touches | Open for the lead | Route at close-out |
|---|---|---|---|---|
| P5b-1 | scope split (5c interface) | `OBS-*` | yes | roadmap status note |
| P5b-2 | ordering | — | no | — |
| P5b-3 | mechanism; styleguide 6.2 departure | `OBS-1`, `OBS-39` | yes | §8.1 dated correction; knowledge note; overlay row |
| P5b-4 | judgement | `OBS-2` | yes | §8.1 dated correction; user page |
| P5b-5 | shape (5a interface) | `OBS-34`, `OBS-35`, `CFG-8` | yes | — |
| P5b-6 | shape | `OBS-20`, P3b-3 | yes | user page |
| P5b-7 | vocabulary | `OBS-36`, `OBS-39` | yes | user page |
| P5b-8 | defaults | `OBS-17`, `OBS-18` | no | — |
| P5b-9 | mechanism | `OBS-11`, `OBS-15`, `OBS-16` | no | — |
| P5b-10 | shape (5c interface) | `OBS-12`, `OBS-17` | no | — |
| P5b-11 | reading | `OBS-20`, `XCUT-20` | no | §11 item 37 unchanged |
| P5b-12 | judgement (stronger than a SHOULD) | `OBS-36`, `OBS-37` | yes | user page |
| P5b-13 | internal fix | `BODY-34`, `CTX-9` | no | roadmap status note |
| P5b-14 | mechanism | `OBS-38` | no | — |
| P5b-15 | mechanism | `OBS-7` | no | — |
| P5b-16 | deferral and reading | `OBS-35` | yes | phase 9's checklist; §10 entry 26 |
| P5b-17 | deferral | `OBS-19` | no | 8b |
| P5b-18 | disposition | `OBS-5`, `OBS-8`–`OBS-10`, `OBS-24`, `OBS-40` | no | §10 entries 22, 23 (pointers) |
| P5b-19 | test design (5c interface) | `OBS-1` | no | — |
| P5b-20 | coordination (5c interface) | 4c hand-off, `MA0051` | yes | roadmap status note |
| P5b-21 | vocabulary | — | no | 8b |
| P5b-22 | process | — | no | — |
| P5b-23 | process | constraint 5 | no | — |
| P5b-24 | scope | `XCUT-19`, `XCUT-20`, `XCUT-24` | no | phase 10 |
| P5b-25 | mechanism | `BODY-20`, `OBS-39` | no | — |
