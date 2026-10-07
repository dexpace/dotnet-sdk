# Phase 5c — Tracing and Metrics: Design

**Status:** Draft, for review. Written 2026-10-07 against `main` at `4130f7b` (phases 0–4 merged: 4a's
`InstrumentationContext`, 4b's `ExceptionFacts`/`Disposal`, 4c's `PipelineContext`, `CallState` and `HttpPipeline` call
entry are in the tree). Brainstormed without a human in the loop: every judgement call the brainstorming skill would have
put to the lead is taken here as a numbered ruling (`P5c-n`) with the options and the rationale, and every ruling is listed
in the [Deviation Ledger](#deviation-ledger). Rulings marked **open for the lead** stay open until ruled. 5a (configuration)
and 5b (logging and redaction) are being designed in parallel and nothing of theirs is in the tree; the interfaces this
design assumes of them are ledger items (P5c-17 to P5c-19) for the lead to reconcile.

**What this document is.** The sub-phase design for 5c: the `OBS` split with 5b, one disposition per owned requirement row
(12 rows), the operation-level `Activity` and its `OBS-29` lifecycle, the population of 4a's correlation bundle, the
per-attempt and per-hop event shape phase 6 enters on, the attempt-span and metric rework (the `server.port` fix and the
current OpenTelemetry HTTP client conventions), `traceparent` stripping in `Dexpace.Sdk.Http.SystemNet`, the zero-allocation
untraced-path test, the public and internal surface, the breaking changes, the PR segmentation, the `Security` classes that
must stay green, and the deviation ledger.

**What this document is not.** It is not the plan (numbered TDD tasks) and not the checklist. It does not restate design
§8.1's argument that tracing is `System.Diagnostics.Activity` and metrics are `Meter` (P2, P14), §10 entries 23 and 24, or
§11 items 37 and 38 (roadmap constraint 9); it cites them and records the decision against each row. It does not design 5b's
log events, emission guard, header allow-list or body preview, 6a's retry engine, or 6b's redirect rewrite; it fixes the
event shape those phases emit through and wires today's `RetryPolicy` to it as an interim.

**How the phase-start queries were run.** The host has no .NET SDK, so `scripts/knowledge` (a `dotnet run` wrapper) cannot
execute here. The same content was read directly: `docs/knowledge/notes/*` (the `--origin note` set: six files, none about
observability), the Conflicts entries (none in `observability.md`; the ten styleguide conflicts are all tagged kept or
conformed and none bears on 5c), `docs/knowledge/harvested/observability.md` in full (the `--prefix-info OBS` source),
`execution-context.md` for the `CTX-14`/`CTX-15` coupling, and appendix C rows `OBS-21`–`OBS-33` for each owned ID (the
roadmap's gap table lists no `OBS` gap ID, but appendix C's `OBS-21` and `OBS-29` carry text the chapter lacks: `OBS-21`'s
"drop their data" and `OBS-29`'s 2026-09-25 amendment C11, both used below). The normative chapter
`docs/product-spec/15-instrumentation-and-observability.md` §15.5–§15.8, design §8.1, §10 entries 22–24, §11 items 37, 38 and
47, §12's `OBS`/`CTX` rows, the 4a, 4b and 4c designs and status notes, and the Ruby sibling's phase 5 segmentation design,
5c checklist and `docs/sdk-documentation/tracing-and-metrics.md` were read. The as-built code read is listed in
[As built at 4130f7b](#as-built-at-4130f7b).

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor | Kind | State at `4130f7b` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030`, `MA0051` at 70 lines, `CA2007` on `src/`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. Every 5c change is written under them; `InstrumentationPolicy`'s `MA0051` waiver is removed by the restructure (P5c-17). |
| Phase 1, S5 (default-deny `UrlRedactor`) | **dependency** | Met. Span `url.full` is the redacted form; `UrlRedactionDefaultDenyTests` stays green unedited. |
| 4a (`InstrumentationContext`, `CallKey`, the context chain) | **dependency** | Met. 5c populates the bundle through `InstrumentationContext.FromActivity` and starts attempt spans through `InstrumentationContext.StartActivity`; it adds, renames and retypes no member (coupling obligation 1, 4a Position A). |
| 4b (`ExceptionFacts.IsFatal`, `Disposal`) | **dependency** | Met. Every new catch is `when (!ExceptionFacts.IsFatal(ex))`; P5c-13's response disposal uses `Disposal.DisposeQuietly(Async)`. |
| 4c (`HttpPipeline` call entry, `CallState`, `PipelineContext.Instrumentation`, the `Operation` stage) | **dependency** | Met. The operation span opens where 4c builds the `DispatchContext` (P5c-2); `CallState` gains two fields. |
| 3b | **none** for 5c | The card's "Entry: 4c and 3b have exited" is 5b's body-logging edge; no 5c change touches a body. |
| 5a (configuration) | **convenience** | 5c reads no option 5a introduces. If 5a makes `DexpaceClientOptions` a record first, nothing in 5c changes. |
| 5b (logging and redaction) | **convenience**, one shared file | Both rework `InstrumentationPolicy`. Neither blocks the other; the second to land rebases onto the first's split (P5c-17). The card's "5a leading 5b is soft" leaves 5c unordered against both. |
| Phase 6 (6a retry, 6b redirect) | **downstream dependency on 5c** | The phase 6 card's entry criterion is "5c's per-attempt event shape is fixed". This design fixes it ([Position E](#e-the-per-attempt-and-per-hop-event-shape-phase-6-enters-on), P5c-7). |
| 8b (real synchronous `Execute`) | **convenience** | The `traceparent` strip lives in the adapter's shared `ToHttpRequestMessage`, so 8b's sync send inherits it. |

No dependency edge inverts the roadmap's order, so no dated status note is owed for ordering.

---

## Scope and the OBS split with 5b

**5c owns 12 rows: `OBS-21`, `OBS-22`, `OBS-23`, `OBS-25`–`OBS-33`** (10 MUST, 2 SHOULD). 5b owns the other 28:
`OBS-1`–`OBS-20`, `OBS-24` and `OBS-34`–`OBS-40`. The phase 5 total is 40 (Phase List row 5), each ID once.

The cut is chapter 15 by section — §15.5–§15.8 (tracing, trace context, HTTP-tracer vocabulary, metrics) to 5c, §15.1–§15.4
and §15.9 (logging facade, diagnostic-context allow-list, redaction, failure containment, log level and body preview) to 5b —
**less `OBS-24`**, which moves to 5b. That is 27 + 13 by section and 28 + 12 after the move, the split the Ruby sibling's
phase 5 segmentation design took for the same reason: `OBS-24` is the diagnostic-context snapshot itself, with no span in
it, and it belongs with `OBS-10`'s fold. `OBS-23` stays with 5c although its effect lands in the diagnostic context,
because its subject is span activation. This split is P5c-1, **open for the lead** to reconcile against 5b's census.

**Rows 5c does work on but does not own** (cross-references in the checklist, never rows):

- `CTX-14`, `CTX-15` (4a): 5c populates the bundle; the rows stay 4a's.
- `OBS-20` (5b): its tracer/meter clause — "assert a throwing tracer/meter is NOT caught" — is evidenced by 5c's
  throwing-listener test (P5c-18).
- `OBS-34` (5b): its "span lifecycle AND metric recording run on every request independent of the log level" clause is
  evidenced by 5c's test at the default (none) level (P5c-18).
- `XCUT-20` (phase 10): §11 item 37's reading, kept: the SDK's tag computation is total, listener callbacks are not wrapped.
- `RETRY-*` (6a), `REDIR-*` (6b): they emit through the event shape fixed here.
- `SEAM-28` (⏳, carrier undefined): the operation span is named from it once a carrier exists (P5c-4).

---

## Requirement census

Legend (roadmap constraint 3): ✅ built and tested; ⏳ owner — later phase; 🚫 N/A with argument. A combined mark gives each
clause its own mark.

| ID | Level | Planned | Disposition (one line) |
|---|---|---|---|
| `OBS-21` | MUST | ✅ | `Activity` is the span; `IsAllDataRequested`/`Recorded` is the recording flag; every SDK mutator (tags, events, `AddException`) is guarded by `IsAllDataRequested`, so the SDK's writes to a non-recording span are inert; `Stop` is idempotent. `Activity.SetTag` itself is not inert — §10 entry 23's "recording flag's form". |
| `OBS-22` | MUST | ✅ | `Activity.Current` is the scope: `Stop`/`Dispose` restores the parent, including on throw; tests pin it for both the operation and the attempt span, sync and async. |
| `OBS-23` | MUST | ✅ (§10 entry 23) | Log correlation is the host's `ActivityTrackingOptions`; 5c guarantees the precondition: the attempt span is `Activity.Current` at every SDK log site and an untraced call leaves the caller's `Activity.Current` untouched. Keys are `TraceId`/`SpanId`, not `trace.id`/`span.id` (entry 23). |
| `OBS-25` | MUST | ✅ (§10 entry 23) | Untraced: `StartActivity` returns `null`, the bundle is the shared `InstrumentationContext.None`, metric tag lists are not built, the redacted URL is not computed; the zero-allocation test asserts 0 bytes per sync call for the tracing and metric path (Position G). No shared no-op span object exists (entry 23). |
| `OBS-26` | MUST | ✅ | 4a's bundle over `ActivityContext`; 5c adds the traced half: a traced call's bundle carries the operation span's 32/16-hex W3C ids and is valid; an untraced call's is `None` with the all-zero sentinels. |
| `OBS-27` | MUST | ✅ W3C and no-op flavours; 🚫 Datadog flavour (§10 entry 24); the zero-draw coercion clause **to verify** (P5c-14) | `ActivityTraceId` is the W3C flavour, `default(ActivityTraceId)` the no-op flavour; the SDK never sets the process-wide `Activity.TraceIdGenerator`. |
| `OBS-28` | SHOULD | ✅ in part (§10 entry 23) | Operation started/succeeded/failed = the operation span; attempt started = the attempt span; attempt failed with next delay and retries exhausted = events on the operation span (Position E); transport milestones = the runtime's own `System.Net.Http` spans on the reference transport; byte-count milestones are not emitted. |
| `OBS-29` | MUST | ✅ | One operation span per `HttpPipeline` call; it ends exactly once, `Error` on failure and not `Error` on success; attempt spans repeat; `dexpace.retry.exhausted` is the event immediately before the `exception` event carrying the same exception type, and only on a failing operation (Positions B–E). An `ActivityListener` ordering test drives a succeeding and a retry-exhausted operation. |
| `OBS-30` | MUST | ✅ by contract (§11 item 37) | Listener callbacks are the contract party and are not wrapped; tests: a throwing `ActivityStarted` propagates out of `SendAsync`, and concurrent calls under one recording listener keep each operation's spans under its own trace. |
| `OBS-31` | MUST | ✅ | `DexpaceDiagnostics.Meter` manufactures `Counter<long>`, `UpDownCounter<long>` and `Histogram<double>` with per-measurement tags; with no listener every `Add`/`Record` is a no-op over shared static instruments; `System.Diagnostics.DiagnosticSource` is in the shared framework (the dependency audit is the "no metrics runtime" evidence). |
| `OBS-32` | SHOULD | ✅ (§11 item 38) | `http.client.request.duration` (`s`, with OpenTelemetry's bucket advice) and `http.client.active_requests` (`{request}`) with the stable HTTP client attribute sets; no count instrument (derivable from the histogram). |
| `OBS-33` | MUST | ✅ | The SDK records no counter delta it could get wrong (`active_requests` is an up-down counter paired in `finally`); `Histogram<double>` tolerates `NaN`/`±Infinity` with and without a listener (test); `Counter<T>`'s non-negative contract is the BCL's documentation, and the SDK adds no hot-path validation. |

**Total: 12 rows — 12 ✅ (two with a clause carried by an existing §10 entry, one with a 🚫 clause, one with a clause to
verify).** No ⏳.

**Retired as N/A with argument, as the card says** (no row is retired — the topics are mechanisms, not IDs): the bespoke span,
tracer and tracer-factory apparatus (design topic `activity-as-tracing-model`: the runtime type is OpenTelemetry .NET's
tracing API, §8.1, §10 entry 23), the MDC push/restore and trace-id flavour machinery (topic `trace-id-flavours`: §10
entries 23 and 24). The rows those topics touch are ✅ by mechanism above, with the letter each misses named.

---

## As built at 4130f7b

Read for this design:

- `src/Dexpace.Sdk.Core/Pipeline/Policies/InstrumentationPolicy.cs` — one `MA0051`-waived method interleaving span, metric
  and log work. Defects 5c owns: `server.port` is `-1` for a default port; `Stopwatch.StartNew()` allocates per call; the
  redacted URL (a `UriBuilder`, a `StringBuilder` and a string) is computed before any listener or log check; the duration
  tags box an `int?`; the metric tag lists are built even with no `MeterListener`; `http.request.resend_count` counts retries
  only and is set on the first attempt (`0`); no `error.type` for a 4xx/5xx response; an unknown method goes on a metric
  dimension verbatim; the span is started from the static source and parented to `Activity.Current`, not to the bundle
  (P4a-16); a throwing `ActivityStopped` after the response exists leaks the response.
- `src/Dexpace.Sdk.Core/Pipeline/HttpPipeline.cs` `SendCoreAsync` — builds
  `new DispatchContext(InstrumentationContext.FromActivity(Activity.Current))`: the bundle is the *caller's* activity, the
  interim 4a and 4c stated "until 5c replaces it with the operation span".
- `src/Dexpace.Sdk.Core/Pipeline/Policies/OperationPolicy.cs` — the deadline only; `PipelineStage.Operation`'s doc says the
  stage "opens the operation span", which nothing does.
- `src/Dexpace.Sdk.Core/Pipeline/CallState.cs` — the call-scoped state; 5c adds two fields.
- `src/Dexpace.Sdk.Core/Pipeline/Policies/RetryPolicy.cs` — 6a rewrites it; 5c adds the interim event calls (P5c-20).
- `src/Dexpace.Sdk.Core/Execution/InstrumentationContext.cs` — consumed as is.
- `src/Dexpace.Sdk.Core/Diagnostics/DexpaceDiagnostics.cs` — the source and meter; unchanged surface.
- `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs` `ToHttpRequestMessage` — copies every header, so the SDK's
  `traceparent` reaches the runtime, whose propagator does not overwrite it (design §8.1, verified there).
- Tests: `InstrumentationPolicyTests`, `DexpacePipelineTests`, `InstrumentationContextTests` (the `"Instrumentation"`
  collection, which has no `CollectionDefinition`, so it does not exclude other collections running in parallel), and the
  `TestSupport/Diagnostics` recorders, which listen process-wide.

---

## Argued positions

### A. The operation span opens at call entry, around the `Operation` pillar (P5c-2)

4c's hand-off says "5c opens the operation span at `Operation` and populates the bundle the `DispatchContext` carries", and
4a's says "5c replaces [`FromActivity(Activity.Current)` at dispatch] with the operation span". The two readings meet only at
one point. The bundle is fixed when `HttpPipeline.SendCoreAsync` builds the `DispatchContext`, before `OperationPolicy` runs;
`CallState` is shared by reference and immutable, and `CallKey` is minted from the bundle's `ActivityContext` at
construction (`CallContext`'s constructor). A span opened *inside* `OperationPolicy` would therefore either leave the bundle
holding the caller's activity (4a's "replace" not done) or need a mutable `CallState.Dispatch` (re-keying the call after
the fact).

Options:

1. **`OperationPolicy` opens the span** and an internal `PipelineContext.WithInstrumentation` swaps a new `DispatchContext`
   into a fresh `CallState`. Rejected: the `Operation` pillar is replaceable (a custom policy may occupy it), so `OBS-29`'s
   1:1 guarantee would hang on a policy a caller can remove; and re-keying after construction breaks 4a's "the key is minted
   once" reading.
2. **`OperationPolicy` opens the span and the bundle keeps the caller's activity.** Rejected: the bundle would not be
   "populated", and the attempt span (parented through the bundle, P4a-16) would be a sibling of the operation span.
3. **The pipeline opens the span at call entry** — in `SendCoreAsync`, before the `DispatchContext` — and builds the bundle
   from it; the span ends when `SendCoreAsync` returns or throws (chosen).

Chosen 3. The operation span is the outer edge of the `Operation` stage: it encloses `OperationPolicy`'s deadline, every
`PerCall` policy (including `ErrorMappingPolicy`, so a mapped 4xx/5xx is the operation's failure), the redirect and retry
loops and every attempt. It exists for every pipeline shape, `CreateEmpty` included, because it is not a policy (PIPE-39's
"step-less" is about policies; with no listener the span is `null` and the empty pipeline's path is unchanged).
`PipelineStage.Operation`'s doc comment is corrected to "the pipeline opens the operation span around this stage; the stage's
policy applies the overall deadline". **Open for the lead**, because it reads 4c's hand-off differently from its letter.

### B. The bundle is the operation span, and `None` when the SDK is untraced (P5c-3)

`SendCoreAsync` becomes, in outline:

```csharp
var operation = OperationTelemetry.Start(request);                      // null with no listener
var dispatch = new DispatchContext(InstrumentationContext.FromActivity(operation));
var context = PipelineContext.Create(request, options, requestOptions, dispatch, cancellationToken);
// run; on success OperationTelemetry.Complete(operation, response); on failure OperationTelemetry.Fail(operation, ex, state)
// finally OperationTelemetry.Stop(operation, response)   — P5c-13 governs a throwing stop
```

So a traced call's bundle is `FromActivity(operationSpan)`: its ids are the operation span's, its `ActiveSpan` is the
operation span, its `TraceIdFormat` is `W3C`, and `CallKey` embeds those ids. An untraced call's bundle is
`InstrumentationContext.None`, **even when the caller has an ambient `Activity.Current`** from another source.

Options for the untraced case: (1) `FromActivity(Activity.Current)`, as 4c's interim did — keeps the caller's trace id in
`CallKey`, but allocates one bundle per call whenever any other source is traced, and gives a bundle whose `StartActivity`
would start SDK spans parented to a span the SDK did not open; (2) `None` (chosen) — `CTX-15` makes the no-op bundle "the
default" for "disabled-tracing", and the SDK's tracing *is* disabled when its source has no listener; `OBS-25`'s "selecting
a no-op path MUST NOT allocate per call" then holds unconditionally; log correlation with the caller's trace is unharmed,
because no SDK span is created, so `Activity.Current` stays the caller's and the host's `ActivityTrackingOptions` folds it.
The cost is that `CallKey.TraceId` is all-zero for an untraced call even under an ambient trace; `CTX-4` already requires the
key to be unique without the trace id. **Open for the lead** (it changes 4c's interim behaviour).

A consequence stated in the user page: **an attempt span exists only under an operation span.** `InstrumentationContext.None`
returns `null` from `StartActivity` "whatever listeners exist" (4a), so a listener whose sampler drops the operation span
also gets no attempt spans for that call. That is the coherent reading of parent-based sampling, and it keeps `OBS-29`'s
attempt events inside an operation.

### C. The operation span's shape (P5c-4, P5c-5)

- **Source and kind.** `DexpaceDiagnostics.ActivitySource`, `ActivityKind.Internal`. The attempt spans are `Client` (the HTTP
  client semantic conventions describe each physical request); the logical operation is not an HTTP request.
- **Name.** The operation id once `SEAM-28` has a carrier (4a/8b define it; not 5c's), else the normalised method
  (`HttpSemanticConventions.SpanName`: the method for a known method, `HTTP` otherwise — P5c-9). Low cardinality either way.
- **Tags at start** (only when `IsAllDataRequested`): `http.request.method` (normalised, plus `http.request.method_original`
  for `_OTHER`), `server.address`, `server.port` (always the port number — `Uri.Port` is the scheme default when implicit),
  `url.full` (the redacted seed URL, computed only here).
- **End on success** (a `Response` returned): `http.response.status_code` of the final response; status **left `Unset`**.
- **End on failure** (a non-fatal exception escapes the chain): in this order, (1) `dexpace.retry.exhausted` if the call
  state says the last retry sequence was exhausted (Position D), (2) `Activity.AddException(ex)` — OpenTelemetry's `exception`
  event with `exception.type`, `exception.message`, `exception.stacktrace` — (3) `error.type` = the exception's full type
  name, (4) `SetStatus(Error, ex.Message)`. Cancellation (`OperationCanceledException`, the deadline included) is a failure.
- **Fatal exception** (`ExceptionFacts.IsFatal`): no SDK frame catches it (design §10 entry 12, `RETRY-25`); the `finally`
  sets `Error` with no description and no exception event if neither `Complete` nor `Fail` ran, so succeeded/failed stay
  mutually exclusive.
- **When it ends.** When `SendAsync`/`Send` returns the `Response` — at response headers, not when the body is consumed. That
  is the runtime's own `HttpClient` span semantics and the only point the pipeline observes; `Send<T>(handler)` ends the span
  before the handler runs. Stated in the user page.

**Success leaves the status `Unset`, not `Ok` (P5c-5).** Design §8.1 says "exactly one operation end whose status is `Ok` or
`Error`". OpenTelemetry's tracing API says instrumentation libraries SHOULD NOT set `Ok` (it is reserved for the application),
and `System.Net.Http`'s own spans leave success `Unset`. `OBS-29`'s "operationSucceeded and operationFailed are mutually
exclusive" maps to "ended without `Error`" versus "ended with `Error`", which is just as exclusive. A dated correction to §8.1
is proposed. **Open for the lead.**

**A returned 4xx/5xx is a successful operation unless the pipeline maps it.** With `ErrorMappingPolicy` in the chain (the
default pipeline), a 4xx/5xx becomes an `HttpResponseException` inside the operation span, so the operation fails. Without
it, the response is returned and the operation succeeds at the `OBS-29` level, while the *attempt* span carries `error.type`
and `Error` per the HTTP client conventions (Position F). The operation span mirrors the caller's own success test.

### D. Retries exhausted is recorded by the retry policy and emitted at the operation's end (P5c-6)

`OBS-29`: "retries-exhausted (when it fires) is immediately followed by operationFailed with the same throwable". Between
the retry loop and the operation's end sit the redirect policy, every `PerCall` policy (error mapping among them) and the
deadline. If the retry policy added the event when it gave up, a later success (a 503 returned without error mapping) or a
different exception (a mapped `HttpResponseException`, a deadline cancellation) could follow it, breaking the pairing.

Options: (1) the retry policy emits the event on the operation span at exhaustion — breaks the pairing as above; (2) the
retry policy throws a marker exception — changes the caller-visible exception; (3) **the retry policy records the exhaustion
on the call state, and the operation's failure path emits the event immediately before `AddException`, with
`error.type` equal to the failing exception's type** (chosen). The pairing then holds by construction: the event fires only
when the operation fails, and nothing is emitted between it and the `exception` event.

`CallState` gains `RetriesExhausted` (a bool plus the attempt count), written by the retry policy only: set when a sequence
ends exhausted, cleared when a new retry sequence starts (each redirect hop starts one), so an exhaustion on an earlier hop
cannot leak into a later one. The **exhaustion predicate** is 6a's to finalise (`RETRY-*`); 5c's interim wiring in today's
`RetryPolicy` is "a failure that would otherwise be retried (retryable status or exception, replayable and idempotent
request) met a spent budget, with `MaxRetryAttempts > 0`" — a budget of zero means retries are off, and "exhausted" with no
retry is noise. **Open for the lead.**

### E. The per-attempt and per-hop event shape phase 6 enters on (P5c-7)

The phase 6 card's entry criterion is "5c's per-attempt event shape is fixed". Fixed here; emitted only when the operation
span is non-null and `IsAllDataRequested`, as `ActivityEvent`s on the **operation** span (the attempt span has already ended
when the retry policy decides, because `Retry` sits outside `Diagnostics`).

| Event | Emitted by | When | Attributes |
|---|---|---|---|
| (attempt started) | `InstrumentationPolicy` | each transmission | the attempt span itself (Position F) |
| `dexpace.attempt.failed` | the retry policy (5c wires today's; 6a's engine keeps the call) | an attempt failed **and another follows** | `http.request.resend_count` (int, the failed transmission's ordinal, `0` for the first), `error.type` (exception full type name, or the status code as a string), `http.response.status_code` (int, when a response), `dexpace.retry.delay` (double, seconds — UCUM `s`, the wait before the next attempt) |
| `dexpace.retry.exhausted` | `OperationTelemetry.Fail`, from the state the retry policy recorded | immediately before the `exception` event of a failing operation | `dexpace.retry.attempts` (int, transmissions in the exhausted sequence), `error.type` (equal to the next event's `exception.type`) |
| `dexpace.redirect.hop` | 6b's redirect policy | a redirect is followed | `dexpace.redirect.hop` (int, 1-based number of the new hop), `http.response.status_code` (the 3xx), `url.full` (the **redacted** target), `dexpace.redirect.cross_origin` (bool) |
| `exception` | `OperationTelemetry.Fail` | the operation fails | OpenTelemetry's exception event (`Activity.AddException`) |

The emitter is the internal static `OperationTelemetry` (below). It is **internal** (P5c-16): 6a and 6b are in the same
assembly; a public emitter would let a third-party retry policy emit, but it adds surface whose shape phase 6 may still want
to move, and it is additive later. 5c ships `RedirectHop` with unit tests and no production caller; 6b calls it (the roadmap
gives "redacted hop events" to 6b). 6a and 6b must not add events of their own name without a dated correction here.

### F. The attempt span and the metrics (P5c-8, P5c-9, P5c-10)

The attempt span keeps its place (`InstrumentationPolicy`, `Diagnostics` stage, `Client` kind) and changes:

- **Parenting.** Started through `context.Instrumentation.StartActivity(spanName, ActivityKind.Client)`, so its parent is the
  operation span by the bundle, not by `Activity.Current` (P4a-16); `null` whenever the bundle is `None`.
- **Name.** `HttpSemanticConventions.SpanName(method)`: the method, or `HTTP` for a method outside RFC 9110's set plus
  `PATCH` (P5c-9).
- **Tags** (only when `IsAllDataRequested`): `http.request.method` (+ `http.request.method_original` for `_OTHER`),
  `server.address`, `server.port` (the port number, **the fix**), `url.scheme`, `url.full` (redacted, computed here only),
  `http.request.resend_count` = the call's transmission ordinal across retries **and** redirect hops, **omitted on the first
  transmission** (the convention's "SHOULD NOT be set" for the first); on completion `http.response.status_code`,
  `network.protocol.version` (from `Response.Protocol`), and for a status `>= 400` `error.type` = the status code string with
  `SetStatus(Error)` (no description); on exception `error.type` = full type name and `SetStatus(Error, ex.Message)`.
- **The transmission ordinal** is a new `CallState.Transmissions` counter the policy increments at attempt start.
- **`traceparent` stamping stays in core** (P5c-12): when the attempt span is W3C, the policy stamps `traceparent`/`tracestate`
  on the request it forwards, for any transport that does not propagate; the reference adapter strips it (Position H).

The metrics stay on `DexpaceDiagnostics.Meter` under the conventions' names (§11 item 38) and change:

| Instrument | Unit | Attributes (stable HTTP client conventions) |
|---|---|---|
| `http.client.request.duration` (`Histogram<double>`) | `s` | `http.request.method` (normalised), `server.address`, `server.port`, `url.scheme`, and on completion `http.response.status_code` and `network.protocol.version`, or `error.type` (exception type; or the status code for `>= 400`) |
| `http.client.active_requests` (`UpDownCounter<long>`) | `{request}` | `http.request.method`, `server.address`, `server.port`, `url.scheme` — the same tag list for `+1` and `-1` |

- **Bucket advice.** The histogram is created with OpenTelemetry's recommended boundaries for this instrument (`0.005` …
  `10` s) through `InstrumentAdvice<double>`, as `System.Net.Http`'s meter does. *To verify at plan time*: if the advice API
  carries an `[Experimental]` diagnostic on the pinned SDK, the advice is dropped and `OBS-32` is unaffected.
- **Zero cost untraced.** Every tag list is built only when `instrument.Enabled`; the duration is measured with
  `Stopwatch.GetTimestamp()`/`GetElapsedTime` (no `Stopwatch` object), one timestamp shared with 5b's
  `http.response.duration_ms` (P5c-18); status codes are boxed from a static cache for 100–599, so the enabled path does not
  box per call either.
- **No count instrument and no operation-level metric.** The count is the histogram's count (§11 item 38); an
  operation-duration histogram has no convention name and would double every request in a dashboard. Rejected options, P5c-10.
- **Both meters.** A consumer enabling `Dexpace.Sdk` and `System.Net.Http` sees each attempt measured twice under one name;
  the user page and the `Http.SystemNet` README say to enable one or the other (design §8.1's commitment, built here as docs).

### G. The zero-allocation untraced path, and how to test it (P5c-15)

`OBS-25`'s untraced path through the SDK is: `OperationTelemetry.Start` → `null`; `FromActivity(null)` → `None`;
`None.StartActivity` → `null`; `HttpClientMetrics` with `Enabled == false` → no tag list; no redacted URL. The tests:

1. **Primitives.** In a loop of 1 000 after warm-up, `GC.GetAllocatedBytesForCurrentThread()` is unchanged across
   `OperationTelemetry.Start(request)`, `InstrumentationContext.FromActivity(null)`, `InstrumentationContext.None.StartActivity(…)`,
   and the metric record calls, with no listener.
2. **The policy, end to end, on the synchronous path.** Two pipelines over the same synchronous fake transport returning a
   pre-built response: one with `InstrumentationPolicy` (and a `NullLogger`), one with a pass-through policy at
   `Diagnostics`. Allocated bytes per `Send` must be equal. The sync path completes on the calling thread, so the per-thread
   counter is exact; the async path's state-machine box is the pipeline's, not tracing's.
3. **Isolation.** `ActivitySource.AddActivityListener` and `MeterListener` are process-wide: a recorder in a parallel test
   makes `StartActivity` non-null and the measurement wrong. The zero-allocation class goes in a new collection
   `[CollectionDefinition("NoDiagnosticListeners", DisableParallelization = true)]`, which xUnit v3 runs alone.
4. **The other side of the same problem.** From PR 2, *every* `HttpPipeline` call creates an operation span when any
   `Dexpace.Sdk` listener is live, so `Assert.Single(Activities)` in `InstrumentationPolicyTests` can see another test's spans.
   `ActivityRecorder` and `MetricRecorder` gain a scoped mode: the test starts a root activity from a test-only source and the
   recorder keeps only activities in that trace (measurements are filtered on a unique `server.address` per test). This lands
   in PR 1, before the operation span.

The test is a `Unit` test in the default run, as the card's exit requires ("The zero-allocation unit tests for the disabled and
untraced paths (`OBS-1`, `OBS-25`) are in the default run"). `OBS-1`'s disabled-log half is 5b's; test 2 holds both only once
5b's log path is lazy too — whichever sub-phase lands second makes the joint assertion (P5c-18). If 5c lands first, test 2
uses a `NullLogger` (`IsEnabled` false) and 5c moves the redacted-URL computation behind `activity is not null ||
logger.IsEnabled(LogLevel.Debug)` itself.

### H. `traceparent` stripping in `Dexpace.Sdk.Http.SystemNet` (P5c-11, P5c-12)

Design §8.1 (verified there): `System.Net.Http`'s propagator does not overwrite a `traceparent` already on the request, so
the SDK's stamp puts the attempt span's id on the wire and the runtime's own child span becomes invisible to the server. The
fix is the adapter's: in `ToHttpRequestMessage`, skip a `traceparent` whose value equals `Activity.Current?.Id` — proof it is
the SDK's stamp for this attempt, not a caller's header — and the `tracestate` that came with it, **when runtime propagation
is on**. "On" is the runtime's own global switch: `AppContext` switch `System.Net.Http.EnableActivityPropagation`, else the
`DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION` environment variable, default true — the condition under which
`SocketsHttpHandler` installs its diagnostics handler. When the attempt span exists, `Activity.Current` is non-null at the
transport, so the runtime injects either its child span's id (a `System.Net.Http` listener is present) or the attempt span's
own id: either way the header is restored and the trace is unbroken.

The rule applies to owned **and** borrowed clients. A borrowed `HttpClient` whose handler chain has no
`SocketsHttpHandler`/`HttpClientHandler` at its root, or whose primary handler sets `ActivityHeadersPropagator = null`, sends
no `traceparent` for a traced call; the adapter cannot see the handler. Options and why stripping for both won are P5c-11
(**open for the lead**); the residual is documented in the `Http.SystemNet` README, and the borrowed-client remedy is to leave
propagation on or not to listen to `Dexpace.Sdk`.

Core keeps stamping (P5c-12): a transport that does not propagate (a custom `IAsyncHttpClient`) still carries the attempt
span on the wire, which is design §8.1's split ("transports that do not propagate keep the policy's stamping").

---

## Public API surface

**No line is added to or removed from either `PublicAPI.Unshipped.txt`** (P5c-16). Every 5c type is internal. The surface
changes are behavioural and documentary:

- `PipelineStage.Operation` — doc comment corrected (Position A).
- `OperationPolicy` — remarks state that the operation span is the pipeline's and encloses this policy.
- `InstrumentationPolicy` — remarks rewritten (span parenting, `resend_count`, `error.type` for 4xx/5xx, `_OTHER`, metric
  attributes, the stamping/stripping split); its constructor is 5b's to change (P5c-17).
- `PipelineContext.Instrumentation` — remarks: the operation span's bundle, or `InstrumentationContext.None` untraced.
- `PipelineContext.Activity` — remarks: the attempt span, downstream of `Diagnostics` only (unchanged meaning).
- `DexpaceDiagnostics` — remarks list the spans, events and instruments the SDK emits.
- `SystemNetHttpClient` — remarks gain the stripping rule and the two-meters note.

Rejected additions (each is additive later): public constants for the event and attribute names
(`DexpaceDiagnostics.Events.*`), a public `OperationTelemetry` for third-party retry policies, a `SourceName` constant (the
source's `Name` already serves `AddSource`).

### Internal types

All in `src/Dexpace.Sdk.Core/Diagnostics/` (namespace `Dexpace.Sdk.Core.Diagnostics`) unless stated; `InternalsVisibleTo`
already grants `Dexpace.Sdk.Core.Tests`.

- `internal static class HttpSemanticConventions` — the attribute-name constants (`http.request.method`, `url.full`, …, and
  the three `dexpace.*` event names and their attribute names); `MethodValue(Method)` (the method or `_OTHER`);
  `SpanName(Method)`; `ErrorType(Exception)`; `ErrorType(int status)` (cached strings 400–599); `BoxedStatusCode(int)` (cached
  100–599); `ProtocolVersion(Protocol)` (`"1.0"`, `"1.1"`, `"2"`, `"3"`). Offered to 5b for its `LoggerMessage.Define`
  templates (`"{" + HttpSemanticConventions.RequestMethod + "}"` is a constant expression).
- `internal static class HttpClientMetrics` — the two instruments (moved out of the policy), `RequestStarted`/`RequestEnded`
  over one `TagList` built only when enabled.
- `internal struct AttemptTelemetry` — begun by `InstrumentationPolicy` per transmission: holds the attempt span, the start
  timestamp and the metric tags; `Succeeded(Response)`, `Failed(Exception)`, `End(Response?)` (P5c-13). A struct, so the
  untraced sync path allocates nothing.
- `internal static class OperationTelemetry` — `Start(Request seed)`, `Complete(Activity?, Response)`,
  `Fail(Activity?, Exception, CallState)`, `Stop(Activity?, Response?)`, and the phase 6 entry points
  `AttemptFailed(PipelineContext, Response?, Exception?, TimeSpan nextDelay)`, `RetrySequenceStarted(PipelineContext)`,
  `RetriesExhausted(PipelineContext, int attempts)`, `RedirectHop(PipelineContext, int hop, int statusCode, Uri target, bool crossOrigin)`.
- `CallState` (`src/Dexpace.Sdk.Core/Pipeline/`) — `int Transmissions` (incremented with `Interlocked`), and the exhaustion
  record (`bool`, `int`) under the existing gate.
- `SystemNetHttpClient` (`src/Dexpace.Sdk.Http.SystemNet/`) — a private static `RuntimePropagatesTraceContext` read once, and
  the skip in `ToHttpRequestMessage`.

`InstrumentationPolicy.ProcessCoreAsync` becomes an orchestrator under 70 lines (attempt telemetry begin, 5b's request log,
forward, 5b's response or failure log, attempt telemetry end), and its `MA0051` waiver goes.

---

## Breaking changes (all behavioural; each gets a `CHANGELOG.md` `[Unreleased]` line)

1. Every `HttpPipeline` call opens an `Internal` operation span on `Dexpace.Sdk` when listened to; attempt spans are its
   children, no longer children of the caller's `Activity.Current`.
2. `PipelineContext.Instrumentation` is the operation span's bundle, or `InstrumentationContext.None` when the SDK's source has
   no listener — even under an ambient activity (it was `FromActivity(Activity.Current)`); `CallKey.TraceId` is then zero.
3. Attempt spans: `server.port` is the port number (was `-1` for a default port); `http.request.resend_count` counts redirect
   hops as well as retries and is absent on the first transmission (was `0`); a 4xx/5xx response sets `error.type` and
   `Error`; an unknown method is `_OTHER` with `http.request.method_original`, and the span is named `HTTP`.
4. Metrics: `http.client.request.duration` and `http.client.active_requests` gain `server.address`, `server.port` and
   `url.scheme` (and the histogram `network.protocol.version`, and `error.type` for 4xx/5xx); unknown methods are `_OTHER`; the
   histogram carries bucket advice. Dashboards keyed on the old tag set see new series.
5. `SystemNetHttpClient` strips the SDK's own `traceparent`/`tracestate` when runtime propagation is on, so the wire carries the
   runtime's child span id; a borrowed client without runtime propagation sends no `traceparent` for a traced call.
6. A throwing `ActivityStopped` listener after a response exists now disposes that response before the exception propagates
   (it leaked before).
7. New events on the operation span: `dexpace.attempt.failed`, `dexpace.retry.exhausted`, `exception` (additive for exporters,
   listed because the span payload grows).

---

## PR segmentation

Four pull-request-sized steps, tests with their code (TDD), docs last; one branch, `<issue>-phase-5c-tracing`. Not one PR:
the work spans two packages and two reviewable concerns (the attempt half, which collides with 5b's file, and the operation
half, which phase 6 enters on), and the 4c precedent keeps each step green on the whole local gate.

1. **Test isolation, then the attempt span and metrics.** Scoped `ActivityRecorder`/`MetricRecorder` and the
   `NoDiagnosticListeners` collection; `HttpSemanticConventions`, `HttpClientMetrics`, `AttemptTelemetry`; the restructured
   `InstrumentationPolicy` (5c half) parented through the bundle, the `server.port` fix, `resend_count` over
   `CallState.Transmissions`, `error.type` for `>= 400`, `_OTHER`, lazy URL redaction, P5c-13; the existing
   `InstrumentationPolicyTests` updated (Assert on the `Client`-kind span; new expectations for items 3 and 4 above).
2. **The operation span, the bundle and the events.** `OperationTelemetry`; `HttpPipeline.SendCoreAsync` (Positions A–C);
   the `CallState` exhaustion record; today's `RetryPolicy` calling `RetrySequenceStarted`, `AttemptFailed` and
   `RetriesExhausted` (three call sites, no restructure, P5c-20); doc comments on `PipelineStage.Operation`, `OperationPolicy`
   and `PipelineContext`; the `Activity.TraceIdGenerator` setter in `BannedSymbols.txt` (P5c-14); the `OBS-29` ordering tests, the `OBS-30` throwing-listener and concurrency tests, the `OBS-25`
   zero-allocation tests (Position G), the `OBS-22`/`OBS-23` scope tests; an AOT-smoke check that an `ActivityListener` sees an
   operation span with one `Client` child and a `MeterListener` sees one duration measurement.
3. **The adapter.** `SystemNetHttpClient`'s strip (Position H); loopback wire tests: with a `Dexpace.Sdk` and a
   `System.Net.Http` listener, the wire's `traceparent` parent id is the runtime's child span; with propagation disabled by the
   `AppContext` switch, the SDK's stamp survives; a caller-set `traceparent` (not the current id) passes unchanged; the
   `Http.SystemNet` README's two-meters and borrowed-client notes.
4. **Close-out.** `docs/sdk-documentation/tracing-and-metrics.md` (opening "As built by phase 5c … written against source on
   <date>"), the checklist (`2026-MM-DD-phase5c-tracing-checklist.md`, one row per owned ID and cross-reference rows for
   `CTX-14`, `CTX-15`, `OBS-20`, `OBS-34`), the `CHANGELOG.md` entry, the dated design corrections below, the roadmap status
   note, and the housekeeping probe.

**Design and roadmap corrections owed at close-out** (dated, proposed here, applied in PR 4 if the lead accepts the rulings):
§8.1 — the operation span opens at call entry (P5c-2), success is `Unset` (P5c-5), the untraced bundle is `None` (P5c-3), the
as-built defect list is closed; §10 entry 23 — `OBS-29`'s evidence is the ordering test, `OBS-21`'s mutators are inert for the
SDK's own writes only; §10 entry 24 — name `OBS-27`'s zero-draw clause (P5c-14); §11 item 38 — the attribute sets and bucket
advice; §12's `OBS` row notes; the 4a and 4c designs' "5c replaces it with the operation span" lines point here.

---

## Tests

`[Trait("Category", "Unit")]` in `tests/Dexpace.Sdk.Core.Tests/Diagnostics/` and `…/Pipeline/` unless stated; wire tests in
`tests/Dexpace.Sdk.Http.SystemNet.Tests/` take the existing wire classes' category. Every test that installs a listener uses
the scoped recorders; the zero-allocation class is in `NoDiagnosticListeners`.

| Area | Tests (indicative names) | IDs |
|---|---|---|
| Lifecycle | `A_succeeding_call_ends_one_operation_span_without_error`, `A_retry_exhausted_call_ends_with_exhausted_then_exception_carrying_the_same_type`, `A_returned_503_without_error_mapping_emits_no_exhausted_event`, `Attempt_spans_are_children_of_the_operation_span`, `A_fatal_exception_ends_the_operation_span_in_error_without_an_exception_event` | `OBS-28`, `OBS-29` |
| Events | `Attempt_failed_carries_resend_count_error_type_and_delay`, `Exhaustion_on_an_earlier_hop_does_not_leak_into_a_later_one`, `RedirectHop_redacts_the_target` | `OBS-28`, `OBS-29` |
| Bundle | `A_traced_call_bundle_is_the_operation_span`, `An_untraced_call_bundle_is_None_even_under_an_ambient_activity`, `A_traced_bundle_has_W3C_ids_and_is_valid` | `OBS-25`, `OBS-26`, `CTX-14`, `CTX-15` |
| Span semantics | `Sdk_writes_to_a_non_recording_span_are_skipped`, `Stop_is_idempotent`, `Activity_Current_is_restored_after_the_call_including_on_throw`, `The_attempt_span_is_current_at_the_log_site` | `OBS-21`–`OBS-23` |
| Contract | `A_throwing_ActivityStarted_propagates_out_of_SendAsync`, `A_throwing_ActivityStopped_disposes_the_response_before_propagating`, `Concurrent_calls_keep_their_spans_in_their_own_traces` | `OBS-30`, `OBS-20` (5b's row) |
| Zero allocation | `Untraced_tracing_primitives_allocate_nothing`, `An_untraced_sync_send_through_InstrumentationPolicy_allocates_no_more_than_a_pass_through` | `OBS-25` |
| Metrics | `Duration_has_the_stable_attribute_set`, `Server_port_is_the_port_number_for_a_default_port`, `A_4xx_sets_error_type_to_the_status_code`, `An_unknown_method_is_OTHER`, `Active_requests_returns_to_zero`, `The_histogram_tolerates_NaN_and_infinity`, `Spans_and_metrics_record_at_the_default_log_level` | `OBS-31`–`OBS-33`, `OBS-34` (5b's row) |
| Trace ids | `Generated_trace_ids_are_32_lowercase_hex_and_non_zero` (over the runtime's generator), and the setter of `Activity.TraceIdGenerator` added to `BannedSymbols.txt` (`RS0030`), so no `src/` code can set it | `OBS-27` |
| Adapter (wire) | `The_runtime_child_span_is_the_wire_parent`, `The_sdk_stamp_survives_when_propagation_is_off`, `A_caller_traceparent_is_not_stripped` | design §8.1 |

---

## Security (phase 1) tests that must stay green

None of these classes touches instrumentation (checked by search at `4130f7b`: no reference to `InstrumentationPolicy`,
`Activity`, `traceparent`, `DexpaceDiagnostics` or the recorders), and **none needs an edit, mechanical or otherwise**.

- `tests/Dexpace.Sdk.Core.Tests/Security/`: `AuthHttpsGuardTests`, `EnsureSuccessErrorMappingTests`,
  `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, `RedirectCredentialHygieneTests`, `ReDriveRequestIsolationTests`,
  `RetryPacingOverflowTests`, `UrlRedactionDefaultDenyTests` (S5 — the card's exit names it).
- `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security/`: `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`,
  `MalformedContentTypeWireTests`, `RedirectWireTests` (with its `RecordingLogger` helper).

Two watch points, not edits: `ReDriveRequestIsolationTests` and `RetryPacingOverflowTests` drive `RetryPolicy`, which gains
three event calls (no-ops untraced); the four wire classes go through `ToHttpRequestMessage`, whose strip touches only a
`traceparent` equal to `Activity.Current?.Id`, which none of them sets. Non-`Security` tests that change:
`InstrumentationPolicyTests` (the span count, `server.port`, `resend_count`, and the 4xx expectations), `DexpacePipelineTests`
(span counts), and `TestSupport/Diagnostics` (the scoped recorders).

---

## Deviation Ledger

Each entry is a ruling: the options considered, the chosen option and why. **Open** marks a judgement call the lead may
reverse; **taken** marks one this design settles. P5c-17 to P5c-21 are the assumed cross-sub-phase interfaces.

**P5c-1 — The `OBS` split with 5b. Open (reconcile with 5b's census).**
Options: (a) by chapter section, 5c = §15.5–§15.8 (13 IDs, `OBS-21`–`OBS-33`); (b) (a) less `OBS-24` (12 IDs), as Ruby;
(c) the roadmap card's named items only, leaving unnamed IDs to whoever claims them. Chosen (b): `OBS-24` is the diagnostic-
context snapshot with no span in it and travels with `OBS-10`; `OBS-23` stays 5c's because its subject is activation. If 5b's
census omits `OBS-24`, 5c takes it as ✅ by `ExecutionContext` flow (design §8.1, `ASYNC-9`), with one test that the attempt
span is `Activity.Current` inside a transport that hops threads and the caller's is restored after, including on throw.

**P5c-2 — The operation span opens at `HttpPipeline` call entry, not inside `OperationPolicy`. Open.**
Options and argument: [Position A](#a-the-operation-span-opens-at-call-entry-around-the-operation-pillar-p5c-2). Chosen
because the bundle and `CallKey` are fixed at dispatch construction and the `Operation` pillar is replaceable. Departs from
the letter of 4c's hand-off ("at `Operation`"), keeps its intent (outermost, once per call).

**P5c-3 — The untraced bundle is `InstrumentationContext.None`, even under an ambient activity. Open.**
Options: `FromActivity(Activity.Current)` (4c's interim) versus `None`. Chosen `None`: `CTX-15`'s disabled-tracing default,
`OBS-25`'s unconditional no-allocation, and log correlation is unharmed because `Activity.Current` is untouched. Cost:
`CallKey.TraceId` is zero for untraced calls. [Position B](#b-the-bundle-is-the-operation-span-and-none-when-the-sdk-is-untraced-p5c-3).

**P5c-4 — The operation span is `Internal`, named by the operation id when `SEAM-28` has a carrier, else the normalised method;
it ends when the response is returned. Taken.**
Options for the name: a fixed `dexpace.operation` (uninformative), the method (chosen fallback, low cardinality), the URL
path (high cardinality, rejected). Options for the kind: `Client` (would double-count HTTP client spans in backends that
aggregate by kind, rejected). End at headers matches the runtime's `HttpClient` spans; ending at body consumption would need a
hook on every `Response` dispose and would leave spans open on undisposed responses.

**P5c-5 — Success leaves the operation status `Unset`, not `Ok`; failure is `Error` plus `AddException`; a fatal exception
ends in `Error` without an exception event. Open (dated correction to §8.1).**
Options: `Ok` (design §8.1's wording; OpenTelemetry reserves it for applications), `Unset` (chosen; matches the runtime's
spans). For failure detail: `SetStatus` only (as built), or `AddException` (chosen: OpenTelemetry's exception event; SDK
exception messages are already redaction-disciplined, §10 entry 29, and `HttpResponseException`'s default message is the
status only). *To verify at plan time*: `Activity.AddException` on the pinned runtime and its stack-trace rendering of an
`SdkException` trail (4b's `ToString`); if the trail renders suppressed messages that could carry a URL, record
`exception.stacktrace` from `ex.StackTrace` instead.

**P5c-6 — Retries exhausted is recorded on the call state by the retry policy and emitted at the operation's failure, immediately
before the `exception` event; interim predicate: a would-retry failure met a spent, non-zero budget. Open.**
Options and argument: [Position D](#d-retries-exhausted-is-recorded-by-the-retry-policy-and-emitted-at-the-operations-end-p5c-6).
6a owns the final predicate and may change it without a correction here, as long as the emission point stays.

**P5c-7 — The event shape: `dexpace.attempt.failed`, `dexpace.retry.exhausted`, `dexpace.redirect.hop` on the operation span,
with the attributes in Position E; delays in seconds as doubles. Open (phase 6's entry criterion).**
Options: events on the attempt span (impossible for "failed with next delay", which is decided after the attempt span ends);
a separate span per retry wait (noisy, and not the spec's vocabulary); events on the operation span (chosen). Delay unit:
milliseconds as a long (matches 5b's `http.response.duration_ms`) versus seconds as a double (chosen, the convention's UCUM
`s` used by the duration histogram). Attribute names reuse the conventions' keys wherever one exists
(`http.request.resend_count`, `error.type`, `http.response.status_code`, `url.full`).

**P5c-8 — Attempt spans are parented through the bundle; `resend_count` counts retries and hops and is omitted on the first
transmission; a `>= 400` response sets `error.type` and `Error`. Taken.**
Each follows the HTTP client conventions; parenting follows P4a-16. Option rejected: keeping `resend_count = AttemptNumber`
(resets per hop, so two transmissions in one call could carry the same ordinal).

**P5c-9 — Methods outside RFC 9110 plus `PATCH` are `_OTHER` on spans and metrics, with `http.request.method_original` on spans;
the span name is `HTTP`. Taken.**
`Method.Of` accepts any token, so a verbatim method is an unbounded metric dimension. Logs may carry the original (5b's call).

**P5c-10 — Metrics: the two instruments of §11 item 38 with the stable attribute sets, bucket advice, enabled-guarded tag lists,
cached boxed status codes; no count instrument, no operation-level histogram. Taken.**
Rejected: `http.client.request.count` (§11 item 38), an operation duration histogram (no convention name, double counting),
renaming to avoid the clash with `System.Net.Http`'s meter (core is transport-agnostic; the docs say enable one).

**P5c-11 — The adapter strips the SDK's `traceparent`/`tracestate` when it equals `Activity.Current`'s and the runtime's
propagation switch is on, for owned and borrowed clients alike. Open.**
Options: (a) strip for owned clients only (borrowed clients keep the SDK id on the wire: trace connected, runtime span
orphaned); (b) strip for both (chosen: correct for every `SocketsHttpHandler`/`IHttpClientFactory` client, the common case);
(c) a public constructor flag on `SystemNetHttpClient` (more surface for an edge case); (d) stop stamping in core (breaks
non-propagating transports). Residual of (b): a borrowed chain without runtime propagation sends no `traceparent`;
documented. *To verify at plan time*: the switch name and its environment fallback on the pinned runtime, and whether a
`DistributedContextPropagator.CreateNoOutputPropagator()` global should also disable the strip (it should, if detectable
without reflection; otherwise it joins the residual).

**P5c-12 — Core keeps stamping `traceparent` in `InstrumentationPolicy`. Taken.** Design §8.1's split; a custom transport
that does not propagate still carries the attempt span.

**P5c-13 — Listener callbacks are not wrapped (§11 item 37), but a throw from stopping a span after a `Response` exists
disposes that response quietly before propagating. Taken.**
Options: wrap `Stop` (violates `OBS-20`/`OBS-30`'s "not defensively wrapped"); do nothing (as built: the caller loses the
response and the connection leaks); dispose then rethrow (chosen: the exception still propagates, nothing leaks; uses
`Disposal.DisposeQuietly(Async)` with the throw as primary). Applies to both the attempt and the operation span.

**P5c-14 — `OBS-27`'s zero-draw coercion clause is unverified for the runtime's id generator; the SDK never sets the process-wide
`Activity.TraceIdGenerator`. Open.**
Options: install a coercing generator (a library must not set a process-wide hook the application owns); verify the runtime's
behaviour at plan time and, if it does not coerce, extend §10 entry 24 by dated correction to name the clause as admitted
(probability 2⁻¹²⁸ per draw); chosen the latter. The test asserts generated ids are non-zero over a large sample, which cannot
prove coercion.

**P5c-15 — Zero-allocation testing on the synchronous path with `GC.GetAllocatedBytesForCurrentThread`, in a
`DisableParallelization` collection; recorders scoped by trace. Taken.**
Options: BenchmarkDotNet's memory diagnoser (not a unit test, not in the default run), a per-process counter (polluted by
parallel tests), the async path (state-machine boxes are not tracing's). [Position G](#g-the-zero-allocation-untraced-path-and-how-to-test-it-p5c-15).

**P5c-16 — No new public API; event and attribute names are documented strings and the emitter is internal. Open.**
Options: public name constants and a public emitter now (lets third-party retry policies emit; freezes a shape phase 6 has
not used yet) versus internal now, additive later (chosen).

**P5c-17 — Interface with 5b: `InstrumentationPolicy` is restructured jointly. Open (assumed).**
5c owns the span and metric half (`AttemptTelemetry`, `HttpClientMetrics`); 5b owns the log half (its `LoggerMessage.Define`
delegates, the emission guard, any constructor parameter such as `HttpLoggingOptions`). The orchestrator calls both; whichever
sub-phase lands second rebases onto the first's split and removes the `MA0051` waiver if the first did not. 5b's emission
guard wraps log calls only and must not catch span or metric exceptions (`OBS-20`'s asymmetry); 5c's code must not wrap log
calls.

**P5c-18 — Interface with 5b: shared evidence and inputs. Open (assumed).**
(a) URL redaction: 5c calls the redactor 5b makes the client's (assumed `UrlRedactor.Redact(Uri)`, total, default-deny, the
same allow-list as logs — `XCUT-19` says "logging/telemetry"); until 5b lands, the static default-deny instance phase 1 built.
(b) Keys: `HttpSemanticConventions`' constants are offered for 5b's templates, so a span tag and a log key cannot drift.
(c) One `Stopwatch.GetTimestamp()` per attempt feeds both the histogram and 5b's `http.response.duration_ms`.
(d) 5b's `OBS-34` row cites 5c's `Spans_and_metrics_record_at_the_default_log_level`; 5b's `OBS-20` row cites 5c's
throwing-listener test. (e) The joint `OBS-1`/`OBS-25` zero-allocation assertion is made by whichever lands second.

**P5c-19 — Interface with 5a: none required. Taken (assumed).**
5c adds no option. If 5a makes `DexpaceClientOptions` an immutable record, `OperationTelemetry` reads nothing from it; if 5a
introduces a diagnostics options record, a future switch to disable the operation span is additive.

**P5c-20 — Interface with 6a and 6b: they emit through `OperationTelemetry`; 5c wires today's `RetryPolicy` as an interim.
Taken.**
5c adds three calls to `RetryPolicy` (sequence start, attempt failed with the computed delay, exhausted) without restructuring
it; 6a's engine keeps the same calls at the same decisions. 6b calls `RedirectHop` with the redacted target. 6a/6b own the
`RETRY`/`REDIR` rows those emissions serve.

**P5c-21 — Order and branch: 5c is unordered against 5a and 5b (conveniences only) and lands as four PRs on one branch.
Taken.**
Options: wait for 5b (serialises the phase for one shared file); land first and let 5b rebase (chosen as "either order");
one PR (mixes two packages and phase 6's entry contract with the 5b collision).
