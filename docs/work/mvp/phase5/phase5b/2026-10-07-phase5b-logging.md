# Phase 5b — Logging and Redaction: Implementation Plan

**Status:** Draft, for review. Written 2026-10-07 against `main` at `4130f7b` (phases 0–4c merged; branch
`62-phase-5-planning`, issue #62). Design: [phase 5b logging design](2026-10-07-phase5b-logging-design.md), the authority for
every decision below. The plan cites its rows, positions (A–G), facts (R1–R7, V1–V6) and rulings (`P5b-1`…`P5b-25`) rather than
restating them. Scope authority: the roadmap's Phase 5 card and Phase List row 5. Format precedent: the
[4c plan](../../phase4/phase4c/2026-10-05-phase4c-pipeline.md) and its 4a/4b siblings.

**What this document is.** The roadmap's step 3 for sub-phase 5b: numbered TDD tasks in the design's landing order (five
pull-request-sized steps), each with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking**
markings, `CHANGELOG.md` entry, requirement IDs and verification commands. It is not the checklist (step 4, written from what was
built in task 5.3) and it writes no production code. The [coverage table](#checklist-one-row-per-owned-id) below is the plan's
own row table: exactly one row per ID 5b owns.

**Scope.** 28 rows: `OBS-1`–`OBS-20`, `OBS-24`, `OBS-34`–`OBS-40` (22 MUST, 6 SHOULD). 5c owns `OBS-21`–`OBS-23`, `OBS-25`–`OBS-33`
(12). 28 + 12 = 40. **P5b-1 is now cross-checked:** the 5c design on disk
(`../phase5c/2026-10-07-phase5c-tracing-design.md`, "Scope and the OBS split", P5c-1) claims exactly those 12 and hands
`OBS-24` to 5b, so the two censuses agree; task 0 re-checks them once more against whatever 5c has merged. Planned statuses:
21 ✅, 5 N/A (`OBS-5`, `OBS-8`, `OBS-9`, `OBS-40` on §10 entry 22; `OBS-10` on entry 23), 2 ⏳ (`OBS-19` → 8b, `OBS-35` → 9).
`XCUT-19`(c)/(e), `XCUT-20`, `XCUT-24`, `BODY-34`, `RETRY-40`, `REDIR-18`, `REDIR-28` are other owners' rows on which 5b does work; they
appear in [the cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-5b) and carry no exit mark in 5b's checklist.

**Order and gates.** 5a and 5c are **conveniences** (design "Prerequisites"); 3a, 3b, 4b and 4c are met dependencies. PR 1 is the
`InstrumentationPolicy` split and is **dropped if 5c has already merged its own** (P5b-20); PR 2 needs PR 1 (or 5c's split); PR 3
needs PR 2; PR 4 needs PR 3; PR 5 needs PRs 1–4. No PR needs a type from 5a or 5c.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile error
   counts and is the expected red for a new type or changed signature); make the production change; run them green; then run the
   wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is proven able to fail by
   temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the project's
   curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await` including `await using` and `await foreach` (`CA2007`); methods at most
   70 lines (`MA0051`; **PR 1 removes the `InstrumentationPolicy` waiver and no task adds one**); `///` XML docs on every public member
   (CS1591), each citing its requirement ID; no new `PackageReference` in `Dexpace.Sdk.Core` (constraint 2: `FrozenSet`,
   `LoggerMessage`, `Stopwatch` and `Activity` are in the shared framework or the already-recorded logging abstraction); no reflection
   in `src/` (`IsAotCompatible`). `CA1031` (catch of `Exception`) is suppressed only by a scoped `#pragma` with a why-comment citing
   `OBS-20`, never project-wide.
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); `AotSmoke` for the smoke
   check. Any test class that drives `DexpaceDiagnostics.ActivitySource` or its `Meter` is in `[Collection("Instrumentation")]`
   (the as-built `InstrumentationPolicyTests` collection name; xUnit creates the collection from the attribute). Core tests live under
   the folder of the type they pin (`Diagnostics/`, `Configuration/`, `Pipeline/Policies/`, `Http/Response/`, `Internal/`); doubles
   under `tests/Dexpace.Sdk.TestSupport/Diagnostics/` (namespace `Dexpace.Sdk.TestSupport.Diagnostics`). `Dexpace.Sdk.Core.Tests`
   references Core and `TestSupport` only (SEAM-2). Tests of internals use the `InternalsVisibleTo` Core grants `Core.Tests`.
5. **Security tests are never deleted or loosened.** 5b edits **no** `Security` class and adds none (P5b-23). The V-gate's diff
   line is therefore an *empty-diff* check:
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
   must print nothing. A change that seems to force an edit there is a signal to re-read design "Security classes that must stay
   green", not to edit the file. Not editing `UrlRedactionDefaultDenyTests` also means `UrlRedactor`'s existing members keep their
   signatures and behaviour: `RedactHeaderValue` is a new member beside them.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in `Unshipped`, and a
   removed one is a deleted line. Lines are listed per task from the design's "Public API surface"; the build's `RS0016`/`RS0017`
   output is the authority (apply the analyzer's code fix and compare, including the record's synthesised members). Only core's
   file changes; the SystemNet and STJ files must not (the V-gate checks).
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes, stating what
   it was. The PR's last task adds the `CHANGELOG.md` `[Unreleased]` line for each (design "Breaking changes", items 1–6).
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*` (3a finding F1); 5b reaches the
   wrappers, never `StreamCopy`. New internal types go in `src/Dexpace.Sdk.Core/Diagnostics/` (namespace
   `Dexpace.Sdk.Core.Diagnostics`), beside `UrlRedactor` and `DexpaceDiagnostics`, except `HttpLoggingOptions`/`HttpLogLevel`
   (`Configuration/`).
9. **Commits** follow the repository style: `feat!:` for a breaking feature PR, `feat:` for additive, `chore:` for the behaviour-
   preserving split, `test:` for tests only, `docs:` for documentation only. **No AI attribution of any kind in a commit message,
   PR title or body, changelog line or review record** (global instruction). The plan authorises no `git push`, no `gh` command and
   no remote action.
10. **Names from 5a and 5c are placeholders.** The design's cross-sub-phase table fixes the contract, not member names. Tasks 0 and 1.0
    re-derive every call site from the merged code of whichever sibling has landed, and **the sibling's merged code wins**. If 5a has
    made `DexpaceClientOptions` a record, task 2.3 adds an `init` property; if not, a `{ get; set; }` one (P5b-5).
11. **Ported cases cite path and sha** in a header comment (constraint 10): `ruby-sdk@5b17395`, `nodejs-sdk@54aeed4`, the shas phase 1
    cited. Both repositories are on disk at `/home/mohammad/Projects/{ruby-sdk,nodejs-sdk}`; `git rev-parse --short HEAD` there is
    re-run at the time of porting and the sha written is what it prints.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green build is the
lint gate. Test runner is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`, not VSTest's `--filter`). **The authoring
host had no .NET SDK**; every command below was written, not run (design "Governing documents").

### Verification blocks

**V-fast** (per task; one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-gate** (the last task of every PR; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/26/27/30, MA0051, CA2007, CA1848, CA1031
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # expect EMPTY (convention 5)
git diff --stat -- src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt   # expect empty
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`, then
`dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and `scripts/ci/coverage-gate-selftest.sh artifacts/test-results`)
runs before the push of PR 3 and PR 4 (they add the most code). No `PackageReference` changes, so no `packages.lock.json` change is
expected; if a locked restore complains, run `dotnet restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info OBS
scripts/knowledge --prefix-info XCUT
scripts/knowledge --gaps OBS               # expect 0: no OBS ID is a gap ID (the roadmap's gap table names none)
scripts/knowledge --req OBS-16             # per row in scope of the PR (also XCUT-19, XCUT-20, BODY-34 for cited work)
```

---

## Plan-level readings (where the design was ambiguous or needed an adaptation)

The design's decisions are not changed. Where it left a point open the plan took the reading most consistent with it.

- **R1 — 5c's split shape differs in one detail from position A (open for the lead, P5b-20).** The 5c design on disk
  (P5c-13, "`internal struct AttemptTelemetry`", plus `HttpClientMetrics`, `HttpSemanticConventions`, `OperationTelemetry`) makes
  `AttemptTelemetry` a *struct* begun per transmission with `Succeeded`/`Failed`/`End`, where the 5b design's position A sketches a
  static class taking an `AttemptScope`. Both fix the same seams (orchestrator under 70 lines, a shared per-attempt value, a log half
  and a span/metric half). **Task 1.2 builds the 5b-first form of position A with `AttemptTelemetry` as an `internal struct`**
  (constructed from the `AttemptScope`, methods `Succeeded`, `Failed`, `End`), so that 5c's PRs, if they land second, rebase onto the
  shape they designed rather than convert it. If 5c has landed first, task 1.0 drops PR 1 and tasks 2.6 and 3.5 edit 5c's orchestrator
  instead.
- **R2 — Where `AttemptScope`'s redactor comes from before the cache exists.** PR 1 precedes the options record and the cache, so
  `AttemptScope` takes a `UrlRedactor` argument; PR 1 passes the static default one (today's `s_redactor`). Task 2.6 swaps the argument
  for the cached per-call one. `AttemptScope` therefore never reads options itself.
- **R3 — The policy cache must respect `ConcurrencyTests.Every_shipped_policy_holds_only_readonly_instance_fields`.** The design's
  "one-slot `volatile` reference" is a mutable field, which that test (PIPE-11) forbids on a shipped policy. The plan keeps the policy's
  field `private readonly RedactionCache _redaction = new();` and puts the `volatile` slot inside `RedactionCache` (an internal sealed
  class, not a policy). The behaviour is the design's; only the holder moves.
- **R4 — `HttpPipeline`, not `PipelineBuilder.Build`, resolves the logger.** The design says "at `Build`"; the as-built constructor is
  `HttpPipeline(PipelineEntry[] entries, PipelineTerminal terminal, DexpaceClientOptions options)` and `SendCoreAsync` creates the
  context. Task 3.2 resolves the logger **in the `HttpPipeline` constructor** (one scan of `entries` for the `Diagnostics` pillar) and
  hands it to `PipelineContext.Create`, which hands it to `CallState`. Same effect, and `Flatten`/`Nest` pipelines (each its own
  `HttpPipeline`) each report to their own logger.
- **R5 — `Disposal`'s doc remark.** `Disposal`'s `<remarks>` says the gap "closes in phase 5b". Task 3.3 rewrites it to state the
  residual (a pipeline with no `InstrumentationPolicy`; `HttpClientExtensions`' blocking bridge) as the design's position F says.
- **R6 — Existing assertions that must change.** `InstrumentationPolicyTests.ProcessAsync_LogsStructuredEvent_WithRedactedUrl`
  asserts a log entry at the default options; after Breaking 1 nothing is logged at `Level = None`. Task 3.5 edits it to set
  `Logging.Level = Headers` and to assert `url.full` instead of the old message text; this is listed in the checklist's "existing
  assertions changed" table. `DisposalTests` assertions on `EventId(1, "DisposeSuppressed")` (if any) move to id 130 in task 3.3.
  No other existing assertion is expected to change; the V-gate proves it.
- **R7 — Vectors.** Header-value cases live in `tests/vectors/redaction/header-values.json` (shared `{ "source", "cases" }` shape, read
  by `VectorFile.Load<T>`; `VectorFileTests.Every_vector_file_names_its_source_path_and_sha` enforces the header). The source line
  names Ruby (`gems/dexpace-core/test/dexpace/instrumentation/redactor_test.rb`, **every** `header_value` assertion located by
  `grep -n header_value`, not by a line range, plus the Ruby page's header cases) and Node (`packages/core/src/observability/redaction.test.ts`); each case carries a `note` naming its origin and the
  Ruby test it came from. Preview cases live in `tests/vectors/redaction/body-previews.json` (bytes as base64).
- **R8 — `AttemptScope` is a mutable struct passed by `ref` (open for the lead; departs from position A's "readonly struct").** A readonly
  struct whose holder is "allocated only on first read" cannot be built: a readonly field cannot be filled lazily, an array holder
  allocates at construction (failing `OBS-1`'s test), and a lazily-written field behind `in` is lost on the defensive copy. The plan keeps the
  observable contract (zero-allocation construction, URL computed on first read, once per attempt) with a non-readonly struct held as one local
  in the orchestrator and passed `ref` to every consumer. No `ref` crosses an `await` (every consumer is synchronous). 5c's `url.full` tag reads
  the same local. Proposed dated correction to design position A ("readonly struct" → "struct passed by `ref`"), owed at task 5.4.
- **R9 — The disposal key is internal (open for the lead; design B row 130 vs the 16-key public block).** `dexpace.dispose.resource_type` is
  state on `dexpace.dispose.suppressed` but is not one of the design's 16 public `DexpaceLogKeys`. The plan does **not** add a 17th public
  constant. It declares `internal const string DisposeResourceType` on an `internal static class InternalLogKeys` (`Diagnostics/`), used by
  `Disposal`, and adds that one constant to the catalogue test's allow-set with the reason stated. Proposed dated correction to the design's API
  block and position C ("or the one internal dispose key"), owed at task 5.4. If the lead prefers a public constant: add
  `DisposeResourceType`, one `PublicAPI.Unshipped.txt` line, and change 16 to 17 in task 2.2's test.

---

## Task 0 — Phase-start queries, the six runtime facts, and the sibling check (no PR; run before PR 1)

**Purpose.** The authoring host could not run `scripts/knowledge` or the SDK (design "Governing documents"). Run on a host with the
pinned SDK, record results in the PR 1 description and in the checklist's "findings" table.

1. Run the [phase-start queries](#phase-start-queries-re-run-at-the-start-of-each-pr) and compare with the design's table: `--origin
   note` lists six notes (none about logging); `--section conflicts` shows no OBS conflict. Any difference is written down and
   judged before PR 1 starts.
2. **Sibling check.** `git log --oneline main -- src/Dexpace.Sdk.Core/Pipeline/Policies/InstrumentationPolicy.cs` and `git grep -n
   "AttemptTelemetry\|AttemptScope\|HttpLogEmitter" main -- src`: decide whether PR 1 is needed (task 1.0). Read 5a's merged
   `DexpaceClientOptions` (record or class) and 5c's census (`OBS-24` ownership, P5c-1) once more.
3. **V1–V6**, each a throwaway test in a scratch file under `tests/Dexpace.Sdk.Core.Tests/` (**never committed**), run, then deleted:
   - **V1** `CA1848` and a direct `logger.Log<HttpLogRecord-like>(…)` call. A one-method scratch class in `src/` that makes the
     call under `Release` build. *If it warns:* the one emitter method gets a scoped `#pragma warning disable CA1848` with a
     why-comment citing P5b-3 (constraint 1), and task 3.5 says so.
   - **V2** `Uri.TryCreate("/cb?code=x", UriKind.RelativeOrAbsolute, out var u)` then `u.IsAbsoluteUri` on Linux. Either result is
     fine; the scheme-prefix classification is correct on every OS (design D).
   - **V3** `GC.GetAllocatedBytesForCurrentThread()` around a warm `Histogram<double>.Record(1.0, tags)` and
     `UpDownCounter<long>.Add(1, tags)` with a `TagList` and no listener. *If either allocates:* task 3.7's pipeline-delta test is
     dropped and `OBS-1` rests on the emitter-level test alone (design V3 fallback); the delta test moves to 5c's `OBS-25` row.
   - **V4** (input to `OBS-35`'s ⏳ 9 note only) `ConfigurationBinder` converting `" body "` and `"BODY"` into `HttpLogLevel` in a
     scratch project: written into the checklist row, not tested in 5b.
   - **V5** a `record` with a private field assigned in a property and a `with` expression: the field is copied (language-certain;
     the redaction cache design depends on it, so write one assertion and delete it).
   - **V6** `Encoding.UTF8.GetString` over `[0xE2, 0x82]` yields `"�"` and does not throw. Task 4.3 keeps this as a permanent
     test (`TextDecoding` pin).
   - **V7 (added by this plan)** A throwing `ActivityListener.ActivityStarted` callback and a throwing `MeterListener` measurement
     callback propagate out of `StartActivity` and `Histogram.Record` respectively. *If either is swallowed by the runtime:* task 3.6's
     asymmetry test uses only the callback that propagates; if neither does, the test is dropped and the row's evidence is 5c's own
     throwing-listener test (P5c-18(d)).
4. **Record**: results go in the PR 1 description as one line per V-number, and fallbacks taken are written into the corresponding
   task's commit message.

---

## PR 1 — The `InstrumentationPolicy` split (behaviour-preserving)

**Gate: task 1.0.** Rows: `OBS-1` (the lazy-URL half; the rest lands in PR 3). Files:
`src/Dexpace.Sdk.Core/Pipeline/Policies/InstrumentationPolicy.cs`, new `src/Dexpace.Sdk.Core/Diagnostics/{AttemptScope,AttemptTelemetry,HttpLogEmitter}.cs`,
`tests/Dexpace.Sdk.Core.Tests/Diagnostics/AttemptScopeTests.cs`. **Public API: no change.** Every existing `InstrumentationPolicyTests`
assertion stays unchanged and green.

### Task 1.0 — Is PR 1 needed? (P5b-20)

If `git grep` (task 0) finds 5c's `AttemptTelemetry`/`HttpClientMetrics` in `main` and the `MA0051` waiver gone from
`InstrumentationPolicy`, **PR 1 is dropped**: tasks 1.1–1.3 are skipped, `AttemptScope` and `HttpLogEmitter` are added by tasks 2.6 and
3.5 against 5c's orchestrator, and the checklist records the dropped PR. Otherwise continue. If 5c has merged only part of its work
(for example `HttpClientMetrics` but not the orchestrator), take what exists and move only the remaining code.

### Task 1.1 — Pins first: the scope, the lazy URL, the timestamp (`OBS-1`; position A)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/AttemptScopeTests.cs`, class `AttemptScopeTests`
(`[Trait("Category", "Unit")]`, internal access):

- `Construction_allocates_nothing` — `GC.GetAllocatedBytesForCurrentThread()` delta over 1,000 constructions after warm-up is 0
  (`AttemptScope` is a plain `internal struct` with a `string?` cache field, R8; nothing is allocated until the first read). `OBS-1`, R1, R8.
- `The_redacted_url_is_not_computed_until_read` — construct over a request whose URL has a query; assert the allocation delta of
  construction is 0 and that the first `RedactedUrl` read allocates; the second read allocates nothing and returns the same string
  instance (`ReferenceEquals`).
- `The_cached_url_survives_being_passed_by_ref` (R8: read through a `ref AttemptScope` in a helper, then read again in the caller; the same
  string instance, so the span tag and the log never compute it twice).
- `The_redacted_url_uses_the_supplied_redactor` — a `UrlRedactor(["keep"])` over `?keep=1&drop=2` yields `…?keep=1&drop=***` (so
  task 2.6's redactor swap is covered by an existing pin).
- `Elapsed_is_measured_from_construction_without_a_stopwatch_instance` — `AttemptScope.Elapsed` is non-negative and monotonic across
  two reads separated by `await Task.Delay(5)` (uses `Stopwatch.GetElapsedTime(start)`, R1).
- `Method_and_attempt_number_are_captured` — `MethodName == "GET"`, `AttemptNumber == context.AttemptNumber` (built with
  `TestContexts.For`).

Red: CS0246 (`AttemptScope`).

### Task 1.2 — The split (`OBS-1`; position A, P5b-20, R1, R2)

**Production.**

- `Diagnostics/AttemptScope.cs`: `internal struct AttemptScope(Request request, PipelineContext context, UrlRedactor redactor)` (R8: **not**
  `readonly`) holding `Request`, `MethodName`, `AttemptNumber`, `long StartTimestamp = Stopwatch.GetTimestamp()`, the redactor and a private
  `string? _redactedUrl`. `RedactedUrl` computes `redactor.Redact(request.Uri)` on first read and stores it in `_redactedUrl` (a plain field:
  construction allocates nothing and no holder object exists). The orchestrator holds the scope in **one local** and passes it **by `ref`**
  to every consumer, so the cache is written to the one instance and never to a defensive copy. `Elapsed` is
  `Stopwatch.GetElapsedTime(StartTimestamp)`. The type's doc states the by-`ref` rule; one attempt runs on one orchestrator, so no lock is needed.
- `Diagnostics/AttemptTelemetry.cs`: the as-built span, tag, `traceparent` and metric code **moved verbatim** into an `internal struct
  AttemptTelemetry` with `Begin(ref AttemptScope, PipelineContext)` (starts the `Activity`, tags, injects `traceparent`/`tracestate`,
  returns the outgoing request and downstream context, increments `http.client.active_requests`), `Succeeded(Response, ref AttemptScope)`,
  `Failed(Exception, ref AttemptScope)` and `End(ref AttemptScope)`. 5c rewrites the body of these; 5b never edits it again (R1).
- `Diagnostics/HttpLogEmitter.cs`: `internal static partial class HttpLogEmitter` holding the **three as-built `[LoggerMessage]`
  partials unchanged** (same ids 1–3, same templates, `Debug`/`Debug`/`Warning`) behind `OnRequest(ILogger, ref AttemptScope)`,
  `OnResponse(ILogger, ref AttemptScope, int statusCode)` and `OnFailure(ILogger, ref AttemptScope, Exception)`. Each reads
  `scope.RedactedUrl` only when `logger.IsEnabled(level)` or when called from the activity-tagging path (so the URL is computed only when
  a listener or an enabled event consumes it).
- `InstrumentationPolicy.ProcessCoreAsync` becomes the orchestrator of position A (under 70 lines), calling the three; the `#pragma warning
  disable MA0051` / `restore` pair and the waiver comment are **deleted**. The class stays `sealed` (drop `partial`: no generated
  member remains on it; no API change). The two instruments `s_requestDuration`/`s_activeRequests` become `internal static readonly` fields of `InstrumentationPolicy` (accessibility
  widened only so `AttemptTelemetry` can record into them; names, units and tags unchanged; 5c moves them into its own `HttpClientMetrics`).
  XML remarks gain one sentence naming the split.

**`PublicAPI.Unshipped.txt`:** none (verify: `git diff -- src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt` is empty).
**IDs:** `OBS-1` (half). **Verify:** V-fast `AttemptScopeTests`, `InstrumentationPolicyTests`, `DexpaceDiagnosticsTests`,
`UrlRedactorTests`, then the Security category.

### Task 1.3 — Close-out (PR 1)

No `CHANGELOG.md` entry (behaviour-preserving, internal). Run **V-gate**. **Commit:**
`chore: split InstrumentationPolicy into scope, telemetry and log emitter (OBS-1 lazy URL; MA0051 waiver retired)`.

---

## PR 2 — Redaction, options and the vocabulary

**Gate: PR 1 merged (or 5c's split, task 1.0); 5a's record shape read (convention 10).** Rows: `OBS-7`, `OBS-12` (configurable),
`OBS-16`–`OBS-18`, and the vocabulary `OBS-39` needs. Files: `src/Dexpace.Sdk.Core/Diagnostics/{UrlRedactor,DexpaceLogEvents,DexpaceLogKeys,HeaderLogRenderer,LogText,RedactionCache}.cs`,
`src/Dexpace.Sdk.Core/Configuration/{HttpLogLevel,HttpLoggingOptions,DexpaceClientOptions}.cs`, `tests/vectors/redaction/header-values.json`,
`tests/Dexpace.Sdk.Core.Tests/{Diagnostics,Configuration}/*`.

### Task 2.1 — `UrlRedactor.RedactHeaderValue` and the header-value vectors (`OBS-16`, `OBS-11`; P5b-9)

**Failing tests first.** New `tests/vectors/redaction/header-values.json` (`{ "source": "ruby-sdk@5b17395 …/redactor_test.rb (every header_value case, found by grep, lines 175-370) and docs/sdk-documentation/logging-and-redaction.md §Redaction header cases; nodejs-sdk@54aeed4 packages/core/src/observability/redaction.test.ts", "cases": [ { "input", "expected", "note" } ] }`),
the cases of the design's position D plus **every** `header_value` case in `redactor_test.rb` (located with `grep -n header_value`, not a line range: it includes the cases near lines 338-368 such as `bad path?secret=1` → `bad path?***` and `https://h/a b?c=1`) and every header case on the Ruby page's §Redaction (the 17 of the design's PR row) (the `location` relative cases: `/cb?code=SECRET` →
`/cb?***`, `/cb#access_token=T` → `/cb?***`, `/cb?a=1#b=2` → `/cb?***`, `/cb?` → `/cb?***`, `#frag` → `?***`; the verbatim
cases: `/static/path`, `relative/no/slash`, `//h/x`, `//[::1]:8443/x`, `//h:8443/x#f` → `//h:8443/x?***`; the userinfo cases:
`//user:secret@h/x` → `//***:***@h/x`, `//user@h` → `//***:***@h`; the absolute cases through `Redact(Uri)`; the whitespace-
and bracket-hostile cases (including those after line 305), adapted to what `Uri` accepts; the empty string → `""`; and the sentinel-free cases). A case whose
expectation depends on Ruby's parser rather than the URL is **not ported** and is listed in the test file's header comment (as phase 1
did for its sentinel cases). Node's `redaction.test.ts` header cases are ported the same way, each with its note.

New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/UrlRedactorHeaderValueTests.cs`, class `UrlRedactorHeaderValueTests`:

- `Vectors_match` (`[Theory]` over `VectorFile.Load<HeaderCase>("redaction/header-values.json")`; `OBS-16`)
- `An_absolute_value_is_redacted_like_a_request_url` (`https://u:p@h/cb?code=S&api-version=1` equals `Redact(Uri)`; `OBS-16`)
- `The_scheme_prefix_decides_not_IsAbsoluteUri` (`/cb?code=x` is classified relative whatever `Uri.TryCreate` says on the host OS; V2)
- `The_malformed_sentinel_is_never_returned` (`[Theory]` over hostile inputs: control chars, lone `%`, `http://[bad`, 100 KB of `?`; the
  result never equals `[malformed url]` and never contains a userinfo secret; `OBS-16`, `OBS-15`)
- `Userinfo_is_masked_on_every_route` (relative, protocol-relative, unparseable absolute with a space; `OBS-11`)
- `The_method_is_total` (`OBS-15`/`XCUT-20`: no input throws; a null argument throws `ArgumentNullException` only)
- `Existing_members_are_unchanged` (pin: `Redact("https://h/p?a=1")` is still `https://h/p?a=***`, `DefaultQueryAllowList` is `["api-version"]`)

Red: CS1061 (`RedactHeaderValue`).

**Production.** `UrlRedactor.RedactHeaderValue(string value)` per design D (empty → `""`; RFC 3986 scheme prefix, verbatim-safe and
`UriKind.Absolute` parse and a non-sentinel `Redact(Uri)` → that result; otherwise every `//authority` userinfo →
`***:***@`, cut at the first `?` or `#`, `?***` appended iff a cut happened). The scheme-prefix test is a hand-written `ReadOnlySpan<char>`
loop (no regex). The method wraps the fallback in the same scoped `#pragma warning disable CA1031` pattern as `Redact(Uri)` and, on an
internal failure, returns the **relative-marker form** (`?***`), never the sentinel. XML doc cites `OBS-16`, `OBS-11`, P5b-9.

**`PublicAPI.Unshipped.txt`:** `Dexpace.Sdk.Core.Diagnostics.UrlRedactor.RedactHeaderValue(string! value) -> string!`.
**IDs:** `OBS-16`, `OBS-11` (header route). **Verify:** V-fast `UrlRedactorHeaderValueTests`, `UrlRedactorTests`, `VectorFileTests`,
then `UrlRedactionDefaultDenyTests` (unedited).

### Task 2.2 — The vocabulary: `DexpaceLogEvents` and `DexpaceLogKeys` (`OBS-39`, `OBS-3`, `OBS-4`; P5b-7, P5b-21)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/LogVocabularyTests.cs`, class `LogVocabularyTests` (reflection over
the two static classes is allowed in tests):

- `Event_names_and_ids_are_the_published_values` (`HttpRequest` `"http.request"` 100, `HttpResponse` `"http.response"` 101,
  `HttpFailureId` 102, `LogFailed` `"http.instrumentation.log_failed"` 120, `BodyCaptureFailed` 121, `DisposeSuppressed`
  `"dexpace.dispose.suppressed"` 130; `OBS-39`)
- `No_key_or_event_name_is_empty_or_collides` (every `const string` is non-empty, no duplicates across the two classes; `OBS-3`)
- `The_header_prefixes_end_with_a_dot_and_no_key_is_named_event` (`OBS-4`: no constant equals `"event"`)
- `Keys_are_the_OpenTelemetry_names` (each of the 16 key constants equals its literal in the design's public-API block, so a rename is a
  failing test as well as an `RS0017` diff)
- `Id_ranges_are_inside_the_reserved_blocks` (100–109 request/response, 120–129 diagnostics, 130–139 dispose; the reserved
  110–119, 140–169 blocks are asserted unused; P5b-21)

Red: CS0103 (`DexpaceLogEvents`).

**Production.** The two `public static class`es exactly as the design's block (every member `const`, each with a `///` summary citing
its ID; `DexpaceLogKeys.RedactedHeaderValue = "REDACTED"`, `FailedEvent = "dexpace.instrumentation.failed_event"`, the two header
prefixes). `DexpaceLogEvents` also carries the int ids. No instance state, no reflection in `src/`. Also `internal static class InternalLogKeys { internal const string
DisposeResourceType = "dexpace.dispose.resource_type"; }` (R9: not public, not in the 16-key test, no `PublicAPI` line), pinned by
`LogVocabularyTests.The_internal_dispose_key_is_the_published_literal_and_collides_with_nothing`.

**`PublicAPI.Unshipped.txt`:** the `DexpaceLogEvents` and `DexpaceLogKeys` lines of the design block (6 + 1 class lines and 16 + 1 class
lines; the analyzer's code fix is authoritative). **IDs:** `OBS-39`, `OBS-3`, `OBS-4`. **Verify:** V-fast `LogVocabularyTests`.

### Task 2.3 — `HttpLogLevel`, `HttpLoggingOptions`, `DexpaceClientOptions.Logging` (`OBS-34`, `OBS-36`, `OBS-12`, `OBS-18`; P5b-5, P5b-10)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Configuration/HttpLoggingOptionsTests.cs`, class `HttpLoggingOptionsTests`:

- `Defaults_are_none_8_KiB_26_names_api_version_and_three_url_headers` (`Level == None`; `BodyPreviewSize == 8192` and
  `DefaultBodyPreviewSize == 8192`; `AllowedHeaderNames.Count == 26`; `AllowedQueryParameters` is exactly `["api-version"]`;
  `UrlValuedHeaderNames` is `{location, content-location, referer}`; `OmitDisallowedHeaders == false`; `OBS-34`, `OBS-18`, P5b-8)
- `The_default_header_allow_list_contains_no_credential_name` (against a deny-list: `authorization`, `proxy-authorization`, `cookie`,
  `set-cookie`, `x-api-key`, `x-auth-token`, `x-amz-security-token`, `www-authenticate`, `proxy-authenticate`; `XCUT-19`(c), `OBS-18`)
- `The_default_allow_list_is_Rubys_26_names` (the exact set, in the design's order)
- `A_negative_preview_size_throws_and_an_oversized_one_clamps` (`ArgumentOutOfRangeException`, `ParamName` `BodyPreviewSize`;
  `int.MaxValue` → `Array.MaxLength`; `BODY-32`, `OBS-36`)
- `The_init_accessors_copy_the_callers_collection` (mutate the source list afterwards; the options are unchanged; `CFG-8`)
- `A_null_element_in_any_collection_is_rejected` (`ArgumentException`)
- `With_changes_one_property_and_keeps_the_rest` (`options with { Level = HttpLogLevel.Body }` keeps the other five values; V5)
- `Default_is_a_shared_instance_with_level_none` (`ReferenceEquals(Default, Default)`)

Extend `tests/Dexpace.Sdk.Core.Tests/Configuration/DexpaceClientOptionsTests.cs`: `Logging_defaults_to_HttpLoggingOptions_Default`
(`OBS-34`). Red: CS0246 (`HttpLoggingOptions`).

**Production.** `Configuration/HttpLogLevel.cs` (`enum HttpLogLevel { None = 0, Headers = 1, Body = 2 }`, docs state the `Body`
warning), `Configuration/HttpLoggingOptions.cs` (sealed record per design G; `init` accessors copy into `ImmutableArray<string>`-like
`string[]` wrappers exposed as `IReadOnlyCollection<string>` — `System.Collections.Immutable` is in the shared framework; **nothing derived
is cached on the record**, P5b-10), `DexpaceClientOptions.Logging` (`{ get; init; }` if 5a has merged, else `{ get; set; }` on today's
class, default `HttpLoggingOptions.Default`). The record's remarks warn that `Body` logs payloads verbatim up to the preview size
(`XCUT-19`(e)) and that equality over collection members is reference equality (the `CFG-33` residual, P5b-5). `DefaultAllowedHeaderNames`
is a `static readonly IReadOnlyCollection<string>` of the 26 names.

**`PublicAPI.Unshipped.txt`:** the `HttpLogLevel` (4 lines) and `HttpLoggingOptions` blocks of the design (class, ctor, six
`get`/`init` pairs, `Default`, `DefaultAllowedHeaderNames`, `DefaultBodyPreviewSize`, the record's synthesised members) and
`DexpaceClientOptions.Logging.get`/`init` (or `set`). **IDs:** `OBS-34` (the level), `OBS-36` (the size), `OBS-12` (the list),
`OBS-18` (the defaults). **Verify:** V-fast `HttpLoggingOptionsTests`, `DexpaceClientOptionsTests`, `ModelImmutabilityArchitectureTests`
(if the architecture test flags a public settable member, read its allow-list rule before editing it, convention 5's spirit).

### Task 2.4 — Truncation and the header renderer (`OBS-7`, `OBS-17`, `OBS-18`; P5b-8, P5b-15)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/LogTextTests.cs`, class `LogTextTests` (`LogText.Truncate`,
internal): `A_value_of_exactly_8192_chars_is_unchanged`, `A_value_of_8193_chars_is_cut_to_8192_plus_the_suffix` (suffix `…[truncated]`),
`A_cut_never_splits_a_surrogate_pair` (a high surrogate at index 8191 is dropped from the kept prefix), `Null_and_empty_pass_through`
(`OBS-7`).

New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/HeaderLogRendererTests.cs`, class `HeaderLogRendererTests`:

- `An_allowed_header_is_rendered_verbatim` (`content-type: application/json`; `OBS-18`)
- `A_disallowed_header_is_REDACTED_by_default` (`authorization: Bearer SECRET` → value `REDACTED`, the secret absent from every
  rendered pair; `OBS-18`, `XCUT-19`(c))
- `A_disallowed_header_is_omitted_when_OmitDisallowedHeaders_is_true` (`OBS-18`)
- `A_url_valued_header_goes_through_RedactHeaderValue` (`Location: https://h/cb?code=S` → `https://h/cb?code=***`; `location:
  /cb?code=SECRET` → `/cb?***`; `OBS-17`)
- `A_second_location_value_meets_the_redactor_on_its_own` (two `Location` values redacted individually then joined with `", "`; the design's
  P5-108 note)
- `Header_names_are_matched_case_insensitively_and_keyed_lower_case` (`Content-TYPE` allowed by `content-type`; the emitted key is
  `http.request.header.content-type`)
- `Insertion_order_is_preserved`
- `An_empty_value_is_kept_as_empty`
- `A_very_long_value_is_truncated_after_redaction` (the redacted form is what is cut; `OBS-7`)
- `A_custom_URL_valued_name_is_honoured` (`UrlValuedHeaderNames = ["x-callback"]`; `OBS-17`)
- `The_renderer_is_stateless_across_calls_and_threads` (same renderer, `Parallel.For` of 50 renders, identical output)

Red: CS0246 (`LogText`, `HeaderLogRenderer`).

**Production.** `Diagnostics/LogText.cs` (`internal static string? Truncate(string? value)`; limit constant `8192`; suffix constant).
`Diagnostics/HeaderLogRenderer.cs`: `internal sealed class HeaderLogRenderer(HttpLoggingOptions options, UrlRedactor redactor)` with two
`FrozenSet<string>` (`StringComparer.OrdinalIgnoreCase`) fields and `internal void Render(Headers headers, string prefix,
List<KeyValuePair<string, object?>> into)` (design D). The renderer calls `redactor.RedactHeaderValue`, never `Redact(string)`.
**PublicAPI:** none. **IDs:** `OBS-7`, `OBS-17`, `OBS-18`. **Verify:** V-fast `LogTextTests`, `HeaderLogRendererTests`.

### Task 2.5 — The per-call redaction cache (`OBS-12`; P5b-10, R3)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/RedactionCacheTests.cs`, class `RedactionCacheTests`:
`The_same_options_instance_returns_the_same_redactor_and_renderer` (reference equality), `A_different_instance_rebuilds`,
`A_with_copy_that_changes_AllowedQueryParameters_gets_a_redactor_that_honours_it` (V5: the cache is keyed by reference and is **not** a
field of the record), `A_with_copy_that_changes_nothing_is_a_different_key_but_equivalent_output`, `Concurrent_callers_never_observe_a_torn_triple`
(`Parallel.For` over two alternating instances; each returned redactor/renderer pair belongs to the same options instance),
`HttpLoggingOptions_has_no_derived_instance_field` (reflection: no instance field of type `UrlRedactor` or `HeaderLogRenderer` on the record).

Red: CS0246 (`RedactionCache`).

**Production.** `internal sealed class RedactionCache` with a single `volatile Entry?` slot, `Entry(HttpLoggingOptions Key, UrlRedactor
Redactor, HeaderLogRenderer Renderer)` an immutable record; `internal Entry Get(HttpLoggingOptions options)` rebuilds when
`!ReferenceEquals(slot.Key, options)` (no lock; the worst race builds twice, `XCUT-11`). `InstrumentationPolicy` gains
`private readonly RedactionCache _redaction = new();` — the **field is readonly** (R3), so
`ConcurrencyTests.Every_shipped_policy_holds_only_readonly_instance_fields` stays green (run it in this task's verify).
**PublicAPI:** none. **IDs:** `OBS-12`. **Verify:** V-fast `RedactionCacheTests`, `ConcurrencyTests`.

### Task 2.6 — The span tag and the log key share one redactor (`OBS-12`; P5b-10, Breaking 6)

**Failing tests first.** `InstrumentationPolicyTests` (collection `Instrumentation`): `The_url_full_tag_honours_the_calls_allowed_query_parameters`
(`Logging = new() { AllowedQueryParameters = ["keep"] }`, URL `?keep=1&drop=2`, the recorded `url.full` tag is `…?keep=1&drop=***`; a
second call with default options records `…?keep=***&drop=***`), `The_default_url_full_tag_is_unchanged` (**pin** against the as-built
`s_redactor` output; prove it can fail by temporarily changing the default list). Red: the first test fails (the tag still uses the
static redactor).

**Production.** `InstrumentationPolicy` builds the `AttemptScope` with `_redaction.Get(context.Options.Logging).Redactor` (R2), and the
static `s_redactor` is removed. **Breaking 6 (default-preserving):** the `InstrumentationPolicy` remarks say the span tag's allow-list
is the call's. **PublicAPI:** none. **IDs:** `OBS-12`. **Verify:** V-fast `InstrumentationPolicyTests`, then the Security category.

### Task 2.7 — Close-out (PR 2)

`CHANGELOG.md`: `### Added`: `HttpLogLevel`, `HttpLoggingOptions`, `DexpaceClientOptions.Logging`, `UrlRedactor.RedactHeaderValue`,
`DexpaceLogEvents`, `DexpaceLogKeys`; `### Changed`: **Breaking 6** (the `url.full` span tag uses the call's `AllowedQueryParameters`;
default unchanged). Run **V-gate**. **Commit:** `feat: logging options, header redaction and the log vocabulary (OBS-7, OBS-12, OBS-16..OBS-18, OBS-39)`.

---

## PR 3 — The emitter: events, levels, the guard, plumbing

**Gate: PR 2 merged.** Rows: `OBS-1`–`OBS-4`, `OBS-6`, `OBS-20`, `OBS-24`, `OBS-34`, `OBS-39`. Files:
`src/Dexpace.Sdk.Core/Diagnostics/{HttpLogEmitter,HttpLogRecord}.cs`, `src/Dexpace.Sdk.Core/Pipeline/{CallState,PipelineContext,HttpPipeline}.cs`,
`src/Dexpace.Sdk.Core/Pipeline/Policies/{InstrumentationPolicy,RetryPolicy,RedirectPolicy}.cs`, `src/Dexpace.Sdk.Core/Internal/Disposal.cs`,
`tests/Dexpace.Sdk.TestSupport/Diagnostics/*`, `tests/Dexpace.Sdk.Core.Tests/{Diagnostics,Pipeline,Pipeline/Policies,Internal,Support}/*`.

### Task 3.1 — Logger doubles (support; no IDs)

New under `tests/Dexpace.Sdk.TestSupport/Diagnostics/` (each `public sealed class`, XML docs, the header):

- `RecordingLogger : ILogger` — records each `Log` call as `RecordedLog(LogLevel Level, EventId EventId, IReadOnlyList<KeyValuePair<string,
  object?>> State, Exception? Exception, string Message, Activity? Activity, IReadOnlyList<object> Scopes)` (the state is copied when it is an
  `IReadOnlyList<KeyValuePair<string, object?>>`; `Message` is `formatter(state, exception)`); an `AsyncLocal<ImmutableStack<object>>` scope
  stack so `BeginScope` is visible in the `OBS-24` test; `MinimumLevel`; thread-safe `Entries`.
- `ProviderLikeLogger : ILogger` — on every `Log` call it renders like a real provider: `formatter(state, exception)`, then
  `exception?.ToString()` and `exception?.Message`, recording only afterwards (so a throwing `Message` getter throws out of `Log`).
- `ThrowingLogger : ILogger` — `ThrowOnIsEnabled`, `ThrowOnLog`, `ThrowOnLogFirst(int n)` (throws for the first n `Log` calls then records),
  configurable exception factory; records what it received.
- `DisabledLogger : ILogger` — `IsEnabled` is `false` for every level and counts its calls; `Log` throws `InvalidOperationException`.

Self-tests in `tests/Dexpace.Sdk.Core.Tests/Support/LoggerDoublesTests.cs` (the 3b precedent `DisposalCountingBodyTests`):
`RecordingLogger_copies_state_and_captures_the_current_activity`, `ThrowingLogger_throws_for_the_first_n_calls_only`, `ProviderLikeLogger_renders_the_exception_inside_Log`,
`DisabledLogger_counts_IsEnabled_calls_and_never_accepts_a_log`. The SystemNet test project's private `RecordingLogger` is **not**
touched. **IDs:** none. **Verify:** V-fast `LoggerDoublesTests`; `dotnet build tests/Dexpace.Sdk.TestSupport --configuration Release`.

### Task 3.2 — The logger on the call (`OBS-20` plumbing; P5b-6, R4)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/CallLoggerTests.cs`, class `CallLoggerTests`:

- `The_calls_logger_is_the_diagnostics_policys_logger` (build with `new InstrumentationPolicy(recording)`; a probe policy at `Serde` reads
  `context.State.Logger` and asserts `ReferenceEquals`)
- `Without_an_InstrumentationPolicy_the_logger_is_NullLogger` (`ReferenceEquals(context.State.Logger, NullLogger.Instance)`)
- `A_nested_pipeline_uses_its_own_logger` (`Nest`: inner and outer each report their own)
- `CreateDefault_forwards_its_logger` (`DexpacePipeline.CreateDefault(transport, logger: recording)`)

Red: CS1061 (`CallState.Logger`).

**Production.** `CallState` gains `internal ILogger Logger { get; }` (constructor parameter); `PipelineContext.Create` gains an `ILogger`
parameter (default `NullLogger.Instance` is **not** used: pass explicitly, and `TestContexts.For` in `Core.Tests` gains an optional
`ILogger? logger` that defaults to `NullLogger.Instance`); `HttpPipeline`'s constructor scans `entries` for the `Diagnostics` pillar and
reads a new `internal ILogger InstrumentationPolicy.Logger` (R4). **PublicAPI:** none. **IDs:** `OBS-20` (plumbing), P3b-3 gap.
**Verify:** V-fast `CallLoggerTests`, `PipelineContextTests`, `HttpPipelineTests`, `DexpacePipelineTests`.

### Task 3.3 — `Disposal` reports to the pipeline's logger (P3b-3 gap, `OBS-39`; R5, P5b-21)

**Failing tests first.**

- `DisposalTests`: `The_suppressed_event_is_dexpace_dispose_suppressed_id_130_at_Warning`, `The_event_carries_resource_type_and_error_type_keys_never_a_message`
  (state keys `dexpace.dispose.resource_type`, `error.type`; the exception message is absent from state and rendered text). These are the
  edited assertions of R6 if the as-built test names `EventId(1, …)`.
- `RetryPolicyTests.A_throwing_dispose_of_a_superseded_response_reaches_the_pipelines_logger` and
  `RedirectPolicyTests.A_throwing_dispose_of_a_superseded_response_reaches_the_pipelines_logger` (a `TrackingResponseBody` whose dispose
  throws `InvalidOperationException`; a 503→200 and 307→200 script; the recording logger holds exactly one `dexpace.dispose.suppressed`
  warning; the call still succeeds).

Red: assertion failures (id 1, no logger on those paths).

**Production.** `Disposal`: the event becomes `new EventId(DexpaceLogEvents.DisposeSuppressedId, DexpaceLogEvents.DisposeSuppressed)` with a
`LoggerMessage.Define` whose template keys are `InternalLogKeys.DisposeResourceType` (`dexpace.dispose.resource_type`, R9) and
`DexpaceLogKeys.ErrorType` (`error.type`; the fixed-key form, design B); the `<remarks>` "interim gap" paragraph is rewritten (R5): the gap now narrows to a pipeline with no `InstrumentationPolicy` and
`HttpClientExtensions`' blocking bridge. `RetryPolicy` (`:156/:160`) and `RedirectPolicy` (`:181/:185`) pass `context.State.Logger` to the
two no-primary calls. `HttpClientExtensions.cs:168` stays logger-less (documented residual). `ErrorBodyBuffer`, `StepFailure` and
`HttpPipeline` pass a primary and so report nowhere else (unchanged). **Breaking 4.** **PublicAPI:** none (the event constants were added
in task 2.2). **IDs:** `OBS-39`; closes P3b-3. **Verify:** V-fast `DisposalTests`, `RetryPolicyTests`, `RedirectPolicyTests`.

### Task 3.4 — `HttpLogRecord` and the cached formatter (`OBS-6`, `OBS-3`, `OBS-4`; P5b-3, V1)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/HttpLogRecordTests.cs`, class `HttpLogRecordTests`:

- `The_record_is_a_read_only_list_of_key_value_pairs` (`Count`, indexer in range, `ArgumentOutOfRangeException` outside, enumeration order
  equals insertion order)
- `OriginalFormat_is_the_last_pair_so_template_grouping_sinks_group` (`{OriginalFormat}` key present once)
- `A_null_value_is_carried_as_null` (`OBS-3`)
- `Values_are_strings_ints_longs_or_doubles_only` (every SDK-owned value in a response and a failure record is one of those types; header
  lists are joined strings; `OBS-6`)
- `The_formatter_renders_a_short_message_from_the_record` (`HTTP GET https://h/p?***` + the status or error type; never a header value or a
  preview)
- `No_key_is_named_event_and_none_is_empty` (`OBS-3`, `OBS-4`)

Red: CS0246 (`HttpLogRecord`).

**Production.** `internal sealed class HttpLogRecord : IReadOnlyList<KeyValuePair<string, object?>>` over a pre-sized
`KeyValuePair<string, object?>[]`, plus `internal static readonly Func<HttpLogRecord, Exception?, string> Formatter`. Built only by
`HttpLogEmitter` after the level checks. **If V1 found `CA1848` firing,** the one emitter method that calls `logger.Log(…)` carries a scoped
`#pragma warning disable CA1848` with a why-comment citing P5b-3 and the styleguide 6.2 departure. **PublicAPI:** none.
**IDs:** `OBS-6`, `OBS-3`, `OBS-4`. **Verify:** V-fast `HttpLogRecordTests`.

### Task 3.5 — The emitter and the three HTTP events (`OBS-1`, `OBS-2`, `OBS-34`, `OBS-39`; position B, P5b-4, P5b-25)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/HttpLogEmitterTests.cs`, class `HttpLogEmitterTests`
(`[Collection("Instrumentation")]`; the helper runs `pipeline.SendAsync(request, options, ct)` with
`options = new() { Logging = new() { Level = … } }` over `RecordingTransport` and a `RecordingLogger`):

- `At_Headers_a_request_and_a_response_event_are_emitted_at_Information` (ids 100, 101, names `http.request`/`http.response`; `OBS-2`, `OBS-39`)
- `The_request_event_carries_method_url_resend_count_and_allowed_headers` (`http.request.method`, `url.full` redacted — secret query value
  absent, `http.request.resend_count == 0`, `http.request.header.user-agent`; `Authorization` rendered `REDACTED`; `OBS-18`, `OBS-39`)
- `The_response_event_carries_status_duration_and_response_headers` (`http.response.status_code` int, `http.response.duration_ms` double ≥ 0,
  `url.full`, `http.response.header.content-type`; `OBS-39`)
- `Body_size_keys_appear_only_for_a_declared_length` (`http.request.body.size`/`http.response.body.size` present when `ContentLength >= 0`, absent
  when `-1`; P5b-7)
- `A_failure_emits_one_Warning_event_with_error_type_and_the_exception` (a transport throwing `ServiceRequestException`; id 102, name
  `http.response`, `error.type == typeof(...).FullName`, `Exception` is the same instance; the request still fails with the original
  exception; `OBS-2`, `OBS-39`)
- `The_resend_count_follows_the_attempt_number` (Retry + Instrumentation: second attempt logs `1`)
- `A_logger_that_disables_Information_receives_nothing_and_no_state_is_built` (`DisabledLogger`: zero `Log` calls; the header enumeration is not
  performed — assert with a `Headers` subclass is impossible, so assert zero allocations in task 3.7; here assert the `IsEnabled` call count is
  exactly 1 per event and the transport response is untouched; `OBS-1`)
- `A_url_valued_response_header_is_redacted_on_both_paths` (`[Theory]` over `async` ∈ {true, false}, driving `SendAsync` and `Send`; the
  transport returns `Location: /cb?code=SECRET`; at `Headers` the `http.response` event's `http.response.header.location` is `/cb?***` and no
  entry contains `SECRET`; `OBS-17`'s "shared by the sync and async paths")
- `At_None_nothing_is_emitted_and_IsEnabled_is_not_consulted` (`OBS-34`, `OBS-1`)
- `Event_ids_are_emitted_in_the_documented_order` (100 then 101)
- `The_existing_instrumentation_pins_still_hold` — nothing new; the edited R6 test (`ProcessAsync_LogsStructuredEvent_WithRedactedUrl`) now sets
  `Logging.Level = Headers` and asserts the recorded `url.full` contains `api_key=***` and no entry contains `SECRET`.

Red: assertion failures (the as-built events are `Debug`, ids 1–3, other keys) and CS1061 where `Logging` is read.

**Production.** `HttpLogEmitter` (replacing PR 1's three unchanged `[LoggerMessage]` partials, which are **deleted**):
`OnRequest(ILogger, ref AttemptScope, Request, HttpLoggingOptions, RedactionCache.Entry)`, `OnResponse(…, Response)`, `OnFailure(…, Exception)`;
gate order `Level == None` → return; `!logger.IsEnabled(level)` → return; only then `HttpLogRecord` is built (design B). The orchestrator
(`InstrumentationPolicy`) calls `AttemptTelemetry.Begin` **before** `OnRequest` so the attempt span's ids are on the `Activity.Current` the
provider sees (P5b-20). The `http.request.body.size` key reads `request.Body?.ContentLength`. **Breaking 1, 2:** the XML remarks of
`InstrumentationPolicy` state both; the `Logging` section of the class remarks replaces "emitted at `Debug`". Every method stays under 70
lines; if `OnResponse` would exceed it, the key assembly moves to a private `ResponseKeys` helper. **PublicAPI:** none.
**IDs:** `OBS-1`, `OBS-2`, `OBS-34`, `OBS-39`. **Verify:** V-fast `HttpLogEmitterTests`, `InstrumentationPolicyTests`.

### Task 3.6 — The emission guard (`OBS-20`, `OBS-6`, `XCUT-20`; P5b-11, V7)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/EmissionGuardTests.cs`, class `EmissionGuardTests`
(`[Collection("Instrumentation")]`):

- `A_logger_whose_Log_throws_does_not_fail_the_request` (`ThrowingLogger.ThrowOnLogFirst(1)`; the call returns the 200; the next emission is
  a `http.instrumentation.log_failed` Warning (id 120) carrying `dexpace.instrumentation.failed_event == "http.request"` and the exception;
  `OBS-20`, Breaking 3)
- `A_logger_whose_IsEnabled_throws_does_not_fail_the_request` (`OBS-20`)
- `A_second_failure_while_reporting_the_first_is_swallowed` (`ThrowOnLog` always: no exception escapes, no recursion; the logger saw at most
  one diagnostic attempt per failed event)
- `A_fatal_exception_from_the_logger_propagates_unlogged` (`OutOfMemoryException`; `RETRY-25`, §10 entry 12)
- `An_OperationCanceledException_from_the_calls_own_token_propagates` (the logger throws OCE after the call's token is cancelled)
- `An_unrelated_OperationCanceledException_from_the_logger_is_swallowed` (P5b-11: rejected design §8.1 reading)
- `An_exception_whose_Message_throws_does_not_break_the_failure_path` (a transport throwing an exception type whose `Message` getter throws;
  the logger is a `ProviderLikeLogger` that, like a real provider, renders the exception inside `Log` via `exception.ToString()` and
  `exception.Message`, so rendering throws **inside** the guard; assert the call fails with the **original** exception, exactly one
  `http.instrumentation.log_failed` diagnostic is attempted (id 120, `failed_event == "http.response"`) and nothing else escapes; `OBS-6`'s
  "rendering that throws is contained". **Pin proof:** temporarily remove the guard's `catch` and see the test fail with the `Message`
  exception; never committed)
- `A_failing_diagnostic_is_not_logged_at_a_lower_level` (the diagnostic is `Warning`)
- `A_throwing_telemetry_callback_is_not_swallowed_by_the_log_guard` (V7: `ActivityListener.ActivityStarted` or `MeterListener` measurement callback
  throws; the exception reaches the caller; **no** `http.instrumentation.log_failed` is emitted; `OBS-20`'s second sentence, §11 item 37)
- `The_guard_never_wraps_AttemptTelemetry` (a source-text pin is not allowed; the behavioural test above is the pin; prove it fails by
  temporarily wrapping the telemetry call in the guard)

Red: CS0103 where the guard is absent and request-failing assertions.

**Production.** `HttpLogEmitter.Guard(ILogger, string eventName, CancellationToken token, Action<…> emit)` is replaced by a **non-delegate,
allocation-free** shape (convention 3, `OBS-1`): each emitter method contains its own `try`/`catch (Exception ex) when
(Reportable(ex, token))` that calls a private `ReportFailure(logger, eventName, ex)`; `Reportable` is `!ExceptionFacts.IsFatal(ex) &&
!(ex is OperationCanceledException && token.IsCancellationRequested)`. `ReportFailure` has its own inner `try`/`catch` for the second failure.
The `CA1031` suppressions are scoped `#pragma`s citing `OBS-20`. **Breaking 3:** `InstrumentationPolicy`'s `<remarks>` gain `<para><b>Breaking:</b>
a logger that throws used to fail the request (the as-built log calls were unguarded); it now never does, and the failure surfaces as one
`http.instrumentation.log_failed` event</para>`. The `log_failed` event is a cached `LoggerMessage.Define<string, string>`
with the dotted keys. `AttemptTelemetry` calls stay outside every guard. **PublicAPI:** none. **IDs:** `OBS-20`, `OBS-6`.
**Verify:** V-fast `EmissionGuardTests`.

### Task 3.7 — The disabled path allocates and emits nothing (`OBS-1`, `OBS-34`; P5b-19, V3)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/DisabledPathAllocationTests.cs`, class
`DisabledPathAllocationTests` (`[Collection("Instrumentation")]`, no listeners attached, 3 warm-up iterations then a measured loop of
1,000 with `GC.GetAllocatedBytesForCurrentThread()`):

- `Every_emitter_method_allocates_nothing_at_None` (`OnRequest`/`OnResponse`/`OnFailure` under `Level = None`, over a `DisabledLogger`, and over
  `NullLogger.Instance`: 0 bytes in all three combinations; `OBS-1`)
- `Every_emitter_method_allocates_nothing_when_the_logger_disables_Information` (`Level = Body` + `DisabledLogger`: 0 bytes)
- `A_pipeline_over_a_synchronous_transport_adds_zero_bytes_per_call_over_a_pass_through_policy` (the delta form of `OBS-1`: the same request
  and cached response through a pipeline with `InstrumentationPolicy` versus a pass-through policy at `Diagnostics`, `Level = None`; **skipped
  if V3 found the instruments allocate**, with the skip reason recorded in the checklist)
- `The_redacted_url_is_not_computed_on_the_disabled_path` (an allocation-free read of a URL whose redaction would allocate: the delta stays 0)

Red: the pipeline-delta and emitter tests fail on the as-built `Stopwatch.StartNew()`/eager `Redact` **only if PR 1 was skipped**; with
PR 1 merged most of these are expected to be **pins** that already pass. State which in the commit message and prove at least the
emitter-level pin can fail by temporarily building the state before the level check.

**Production.** Whatever the red demands (expected: none after PR 1 and task 3.5). **PublicAPI:** none. **IDs:** `OBS-1`, `OBS-34`.
**Verify:** V-fast `DisabledPathAllocationTests`.

### Task 3.8 — The diagnostic context flows to the log event (`OBS-24`; P5b-18)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/DiagnosticContextFlowTests.cs`, class
`DiagnosticContextFlowTests` (`[Collection("Instrumentation")]`):

- `A_caller_activity_and_scope_are_visible_at_the_response_event_after_the_transport_completes_on_another_thread` (start an
  `Activity` from a test `ActivitySource` with a listener, `using (recordingLogger.BeginScope("order-42"))`, a transport that `await
  Task.Yield()` then `Task.Run`s its completion; the recorded `http.response` entry has the caller's trace id in `Activity` or in
  `Activity.Current.ParentId`, and `"order-42"` in `Scopes`; `OBS-24`, `ASYNC-9`)
- `The_attempt_span_is_current_when_http_request_is_emitted` (a listener on `Dexpace.Sdk`; the recorded `http.request` entry's `Activity` is the
  attempt activity, so a provider using `ActivityTrackingOptions` stamps its ids; P5b-20)
- `No_SDK_source_suppresses_execution_context_flow` (**pin** on the existing `BannedSymbols.txt` entry: a `Compilation`-free check that
  `M:System.Threading.ExecutionContext.SuppressFlow` is still listed in the file; prove it fails by deleting the line locally)

Red: expected pins that pass; the second test fails until `Begin` is ordered before `OnRequest` (task 3.5). **PublicAPI:** none.
**IDs:** `OBS-24`. **Verify:** V-fast `DiagnosticContextFlowTests`.

### Task 3.9 — Level defaults, the catalogue test and the none-level proof (`OBS-2`, `OBS-3`, `OBS-4`, `OBS-34`, `OBS-39`; `XCUT-19`(c)/(e))

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/LogCatalogueTests.cs`, class `LogCatalogueTests`
(`[Collection("Instrumentation")]`): drives scenario list [success at `Headers`, failure at `Headers`, `log_failed`, a throwing dispose], and for
every recorded entry asserts: `EventId.Name` is a value of a `const string` on `DexpaceLogEvents` (reflection in the test), every state key is a
`DexpaceLogKeys` constant, or is `InternalLogKeys.DisposeResourceType` (the one allow-set addition, R9, reached by the throwing-dispose
scenario), or begins with one of the two header prefixes, or is `{OriginalFormat}`, no key is empty (`OBS-3`), no key is named
`event` (`OBS-4`), and the `LogLevel` is one of `Error`/`Warning`/`Information`/`Debug` (`OBS-2`). A second theory over `DexpaceLogEvents` asserts
each event id is within its reserved block. (Task 4.6 adds the `Body` scenarios.)

New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/HttpLoggingDefaultsTests.cs`, class `HttpLoggingDefaultsTests`
(`[Collection("Instrumentation")]`):

- `The_default_options_emit_no_http_event` (`OBS-34`; `XCUT-19`(e): a recording logger enabled for everything sees none of ids 100–102)
- `Spans_and_instruments_still_record_at_None` (`ActivityRecorder("Dexpace.Sdk")` sees one Client activity with `url.full`, and
  `MetricRecorder` sees `http.client.request.duration` and the `active_requests` pair; `OBS-34`'s conformance clause)
- `Headers_level_logs_headers_only_and_never_a_body_preview` (`XCUT-19`(c): no `*.body.preview` key at `Headers`)
- `A_secret_header_value_never_appears_in_any_event_field_or_message` (`Authorization`, `Cookie`, `X-Api-Key`, `Set-Cookie` with the marker
  `SECRET`; asserted over every state pair and every rendered message at `Headers`; `XCUT-19`(c))

Red: any of these not yet true. **PublicAPI:** none. **IDs:** `OBS-2`, `OBS-3`, `OBS-4`, `OBS-34`, `OBS-39`, `XCUT-19`(c)/(e) evidence.
**Verify:** V-fast `LogCatalogueTests`, `HttpLoggingDefaultsTests`.

### Task 3.10 — Close-out (PR 3)

`CHANGELOG.md` `### Changed` (**Breaking 1–4**, one bullet each: no log events unless `Logging.Level` is `Headers`/`Body`; renamed and re-keyed
events at ids 100–102 and `Information`, `error.type` the full type name; a throwing logger no longer fails the request
(`http.instrumentation.log_failed`); `Disposal`'s warning is `dexpace.dispose.suppressed` (130) and now reaches the pipeline's logger).
Run **V-gate** and the coverage gate with its self-test. **Commit:**
`feat!: structured http.request/http.response events, emission guard and logger plumbing (OBS-1..OBS-4, OBS-6, OBS-20, OBS-24, OBS-34, OBS-39)`.

---

## PR 4 — Body level

**Gate: PR 3 merged.** Rows: `OBS-36`–`OBS-38` (and `BODY-34`'s ⏳ 5b clause, `XCUT-24` evidence). Files:
`src/Dexpace.Sdk.Core/Http/Response/{Response,LoggingResponseBody}.cs`, `src/Dexpace.Sdk.Core/Diagnostics/{HttpLogEmitter,BodyPreviewRenderer}.cs`,
`tests/vectors/redaction/body-previews.json`, `tests/Dexpace.Sdk.Core.Tests/{Http/Response,Diagnostics,Pipeline/Policies}/*`.

### Task 4.1 — `Response.ReplaceBody`: the ownership-moving swap (`BODY-34`; P5b-13, R5 of the design)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Response/ResponseReplaceBodyTests.cs`, class `ResponseReplaceBodyTests`
(internal access; `TrackingResponseBody` from `TestSupport`; an `ExchangeContext` built through `ExecutionTestSupport`):

- `The_new_response_owns_the_new_body_and_keeps_status_headers_protocol_and_reason`
- `Disposing_the_original_afterwards_does_not_dispose_either_body` (latched; the old body's dispose count is 0, the new body's is 0)
- `Disposing_the_new_response_disposes_the_new_body_exactly_once_and_never_the_old_directly` (the new body is the wrapper that owns the old one
  and releases it itself; assert the old body is disposed exactly once overall, through the wrapper)
- `Exchange_links_move_to_the_new_response` (an exchange attached to the original is closed when the **new** response is disposed, once,
  in reverse order of attachment; not closed by the original's dispose; CTX close-on-dispose survives the swap)
- `Replacing_the_body_of_a_disposed_response_throws` (`ObjectDisposedException`)
- `WithBody_is_unchanged` (**pin**: the public member still leaves the original to be disposed and does not move links; the doc stays as is,
  P5b-13)
- `The_async_dispose_latch_is_shared` (`DisposeAsync` on the new response after `Dispose` is a no-op)

Red: CS1061 (`ReplaceBody`).

**Production.** `internal Response ReplaceBody(ResponseBody body)` on `Response`: under the existing latch (`Interlocked.Exchange(ref
_disposed, 1)`) mark the original disposed **without** disposing its `Body`, move `_exchanges` to the new response (`AttachExchange` each, under
`_exchangeGate`), and return `new Response(Request, Status, Protocol, Headers, body, ReasonPhrase)`. Document on the internal member that the
caller passes a body that already owns the old one. The public `WithBody` doc is unchanged (design "Corrections owed"). **PublicAPI:** none.
**IDs:** `BODY-34` (clause), P5b-13. **Verify:** V-fast `ResponseReplaceBodyTests`, `ResponseDisposeLatchTests`, `ResponseTests`.

### Task 4.2 — `LoggingResponseBody`: sync snapshot and a logger for quiet closes (`OBS-17` sync path, `OBS-20`; R6, design position F)

**Failing tests first.** Extend `tests/Dexpace.Sdk.Core.Tests/Http/Response/LoggingResponseBodyTests.cs`:

- `Snapshot_sync_returns_the_same_bytes_as_SnapshotAsync` (a body below, equal to and above the cap; copy semantics)
- `Snapshot_sync_never_throws_a_drain_failure_and_exposes_it_through_DrainFailure` (`BODY-26`)
- `Snapshot_sync_honours_the_starters_token` (a cancelled token cancels the wait or the drain it started, like the async twin)
- `Snapshot_sync_after_dispose_returns_what_was_captured` (`BODY-28`)
- `A_throwing_delegate_dispose_during_a_quiet_close_is_reported_to_the_supplied_logger` (the optional `ILogger` constructor argument;
  exactly one `dexpace.dispose.suppressed` warning; the consumer's dispose behaviour is unchanged otherwise)
- `Constructing_without_a_logger_is_unchanged` (**pin**)

`LoggingWrapperSurfaceTests` stays green (`Snapshot` is on its allow-list; the new member returns `byte[]` copies). Red: CS1501/CS1061.

**Production.** `internal byte[] Snapshot(int maxBytes, CancellationToken cancellationToken)` over `EnsureDrained` (the sync twin of the
existing async member); a third optional constructor parameter `ILogger? logger = null` passed to the two `Disposal.DisposeQuietly(Async)`
calls at `:366/:367/:390/:391`. **PublicAPI:** none. **IDs:** `OBS-36` (sync path), `OBS-20`. **Verify:** V-fast `LoggingResponseBodyTests`,
`LoggingWrapperSurfaceTests`.

### Task 4.3 — The preview renderer (`OBS-38`; P5b-14, V6)

**Failing tests first.** New `tests/vectors/redaction/body-previews.json` (source: `ruby-sdk@5b17395
gems/dexpace-core/test/dexpace/instrumentation/preview_test.rb` and `nodejs-sdk@54aeed4 packages/core/src/observability/logging-step.test.ts`;
cases `{ mediaType, bytesBase64, expected, note }`) and `tests/Dexpace.Sdk.Core.Tests/Diagnostics/BodyPreviewRendererTests.cs`, class
`BodyPreviewRendererTests`:

- `Vectors_match` (`[Theory]` over the file: ISO-8859-1 declared charset decodes to the right text, binary → `[binary N bytes captured]`,
  `application/problem+json`, `+xml`, `+yaml`, a cut multibyte tail, an unknown charset falls back to UTF-8, `multipart/form-data` renders
  as binary, an absent media type renders as binary, empty input renders `""`; `OBS-38`)
- `Every_text_media_type_in_the_design_list_is_text` (`text/*`, `application/json`, `xml`, `x-www-form-urlencoded`, `javascript`, `x-ndjson`,
  `yaml`, `graphql`)
- `A_cut_multibyte_sequence_yields_the_replacement_character_and_does_not_throw` (V6 pin: `[0xE2, 0x82]` → `"�"`)
- `A_matching_BOM_is_stripped` (`TextDecoding`'s rule; **pin**)
- `The_renderer_is_total` (random bytes × random media types never throw)

Red: CS0246 (`BodyPreviewRenderer`).

**Production.** `internal static class BodyPreviewRenderer` with `Render(ReadOnlySpan<byte>, MediaType?)` and a private `IsText(MediaType?)` over
a `FrozenSet<string>` of subtype names plus the `+json`/`+xml`/`+yaml` suffix rule; decodes through `TextDecoding.Decode(bytes,
mediaType.Charset)` (`MediaType.Charset` is already null-safe). No byte sniffing (P5b-14). **PublicAPI:** none. **IDs:** `OBS-38`.
**Verify:** V-fast `BodyPreviewRendererTests`, `VectorFileTests`.

### Task 4.4 — Body-level engagement on both paths (`OBS-34`, `OBS-36`, `OBS-37`, `BODY-34`; position E, P5b-7, P5b-12, P5b-25)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/BodyLoggingTests.cs`, class `BodyLoggingTests`
(`[Collection("Instrumentation")]`; every test is a `[Theory]` over `async` ∈ {true, false}, driving `SendAsync` and `Send`; `OBS-17`'s sync-path
evidence is task 3.5's header theory):

- `At_Headers_and_None_no_wrapper_is_constructed` (the response `Body` the caller receives is the transport's instance, `ReferenceEquals`; the
  request the transport receives has the caller's body instance; `BODY-34`, `OBS-34`)
- `At_Body_the_response_event_carries_a_preview_and_its_size` (`http.response.body.preview` text, `http.response.body.preview.size` = captured
  count; `OBS-36`, `OBS-38`)
- `The_consumer_still_receives_every_byte_of_an_over_cap_body` (cap 16, body 4 KB: the consumer reads all 4 KB; the preview is 16 bytes;
  `OBS-36`, `BODY-24`)
- `A_body_that_fits_is_served_from_memory_and_can_be_opened_again` (`BODY-23`; **Breaking 5** noted)
- `An_unknown_length_body_is_never_wrapped` (`ContentLength == -1`: the body instance is the transport's; no `*.body.preview` key; both paths;
  `OBS-37`, P5b-12)
- `A_text_event_stream_body_is_never_wrapped_even_when_it_declares_a_length` (`OBS-37`)
- `The_request_preview_rides_on_the_response_event` (`http.request.body.preview` and `.preview.size` on `http.response`, not on `http.request`; a
  `RequestBody.FromString` echoed by the transport writing it; P5b-25, `BODY-20`)
- `The_request_preview_rides_on_the_failure_event` (a transport that writes the body then throws; the 102 event carries the request preview, no
  response preview; P5b-25)
- `Each_attempt_wraps_afresh_and_the_tap_reflects_that_attempt_only` (Retry above Instrumentation, a replayable body; second attempt's preview is
  the second write only; `BODY-18`)
- `The_request_a_policy_holds_is_never_the_logging_wrapper` (a probe policy above `InstrumentationPolicy` captures `request.Body` before and after
  the call; `ReferenceEquals` with its own body; the wrapper is applied to the request passed down only; `PIPE-16`, S6 spirit, pinned in a `Unit`
  class so `ReDriveRequestIsolationTests` is untouched)
- `A_drain_failure_emits_body_capture_failed_and_the_response_event_still_emits_with_the_partial_preview` (121, `error.type`, the exception
  attached; the consumer's later read throws the cached failure; `BODY-26`)
- `Cancellation_of_the_calls_token_during_the_preview_drain_propagates_and_disposes_the_response` (`XCUT-3`; the tracking body's dispose count is 1;
  `OperationCanceledException`, not swallowed by the guard; P5b-11)
- `Disposing_the_returned_response_closes_the_exchange_link_once` (4.1's property, end to end)
- `ContentLength_follows_BODY_29` (after a full capture the wrapper's length is the captured size)
- `A_zero_preview_size_captures_nothing_and_still_serves_every_byte` (`BodyPreviewSize = 0`)

Red: assertion failures (no body level yet).

**Production.** In `HttpLogEmitter`: at `Level == Body` and `request.Body is not null`, `OnRequest` returns `request.WithBody(new
LoggingRequestBody(body, cap))` and exposes it to the orchestrator (the orchestrator passes it downstream and keeps the wrapper reference for
the post-call `Snapshot(cap)`); `OnResponse` checks `ContentLength >= 0` and `ContentType` not `text/event-stream`, then
`response.ReplaceBody(new LoggingResponseBody(inner, cap, logger))`, captures with `SnapshotAsync(cap, token)` or the sync twin (the policy body is
one `ProcessCoreAsync(…, bool async)`, so the choice is a single branch), checks `DrainFailure`, renders through `BodyPreviewRenderer`, and
adds the four preview keys. The preview drain runs inside the guard, with cancellation of the call's own token propagating and the owned response
disposed first through `Disposal.DisposeQuietlyAsync(response, oce, logger)` / its sync twin (design B). A new `body_capture_failed` `Define`
delegate is added. `InstrumentationPolicy` remarks document **Breaking 5** and the added latency (design "Risks"). **PublicAPI:** none.
**IDs:** `OBS-34`, `OBS-36`, `OBS-37`, `OBS-38`, `BODY-34`. **Verify:** V-fast `BodyLoggingTests`, `InstrumentationPolicyTests`,
`HttpLogEmitterTests`, `EmissionGuardTests`, then the Security category.

### Task 4.5 — A 10 MB body, a small cap (`XCUT-24`; `OBS-36`)

**Failing test first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/BodyPreviewTests.cs`, class `BodyPreviewTests`:
`A_10_MB_body_with_a_small_cap_is_captured_in_the_cap_and_delivered_whole` (a generated read-only `Stream` of 10 MiB of a known pattern via
`ResponseBody.FromStream` with a declared length, cap 1 KiB, `Level = Body`; the consumer counts and checksums 10 MiB; the preview size is
1 KiB; the process allocated bytes during the call stay under 2 MiB, measured with `GC.GetAllocatedBytesForCurrentThread()` on the async path
**only** — never on the sync path, which has `ConfigureAwait` machinery noise; the threshold is a ceiling not a goal) and
`The_same_body_in_a_single_pass_is_not_replayed` (a stream-backed body is consumed once: a second open throws `StreamConsumedException` per
`BODY-24`). Red: expected to be a **pin** after 4.4; prove it fails by temporarily lifting the cap. **IDs:** `XCUT-24` evidence, `OBS-36`.
**Verify:** V-fast `BodyPreviewTests`.

### Task 4.6 — Extend the catalogue to body-level events (`OBS-2`, `OBS-3`, `OBS-4`, `OBS-39`)

Extend task 3.9's `LogCatalogueTests` scenario list with `Body` success, `Body` failure and `body_capture_failed`; assert `*.body.preview`
and `*.body.preview.size` keys are `DexpaceLogKeys` constants. **Verify:** V-fast `LogCatalogueTests`.

### Task 4.7 — Close-out (PR 4)

`CHANGELOG.md` `### Changed` (**Breaking 5**: at `Body`, a response with a known-length body comes back with a wrapped body, up to the preview size
is read before `SendAsync` returns, a body that fits is served from memory and can be opened again, `ContentLength` follows `BODY-29`;
opt-in). Run **V-gate** and the coverage gate with its self-test. **Commit:**
`feat!: body-level logging with bounded previews (OBS-36..OBS-38, BODY-34)`.

---

## PR 5 — Close-out

**Gate: PRs 1–4 merged (PR 1 may be "dropped" per task 1.0).** The docs close the sub-phase (roadmap step 7). Rows: all 28 (closing).

### Task 5.1 — NativeAOT smoke over the new surface (`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`)

`RunAllAsync` calls one more check, `CheckHttpLoggingAsync()` (after `CheckPipelineReworkAsync`, in the existing style with its `Expect` helper):
a `DexpacePipeline.CreateDefault` over the existing in-process handler with a local recording `ILogger` (a private nested class in the smoke
project; no `TestSupport` reference), `Logging = new() { Level = HttpLogLevel.Body }`; one call with a JSON body and one with a binary body; asserts
the `http.response` event's `http.response.body.preview` is the JSON text, the binary one is `[binary N bytes captured]`, the `Authorization`
header is `REDACTED`, `url.full` has no secret value, and the consumer reads every byte. No reflection; `AotSmoke` gets no `InternalsVisibleTo`.
A trim/AOT warning means the source is fixed, not the smoke. **Check:** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output
artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` prints "all checks passed".

### Task 5.2 — User documentation

New `docs/sdk-documentation/logging-and-redaction.md` (the directory holds `bodies.md`, `execution-context.md`, `http.md`, `io.md`,
`pipelines.md`, `recovery.md`, `seams.md`), opening "As built by phase 5b, written against source on <date>". Content: the three levels and
the opt-in (`Logging.Level`, per-call options, the `Body` warning); the event catalogue (names, ids, levels, keys — one table generated from
`DexpaceLogEvents`/`DexpaceLogKeys`), the reserved id ranges; the header allow-list and the `REDACTED`/omit switch; URL redaction and
`RedactHeaderValue` with the vectors' headline cases; body previews, the unknown-length/SSE skip and the latency note; the guard and what it
does not wrap; the diagnostic context (`LoggerFactoryOptions.ActivityTrackingOptions` with `TraceId | SpanId`, the `OBS-10` recommendation);
nesting (double logging when a pipeline is another's transport); the two residuals (a pipeline with no `InstrumentationPolicy`, the blocking
bridge); the levels ruling (P5b-4: `Information`/`Warning`, diverges from Ruby and Node on the failure level); the migration table for the six
breaking changes; `OBS-35`'s note (binding is phase 9; an unrecognised level fails at startup; V4's result). Cite requirement IDs; do not copy
design §8.1. `docs/README.md`'s ownership table gains the row the probe asks for. **Verify:** the probe's `links` check.

### Task 5.3 — The checklist

Write `docs/work/mvp/phase5/phase5b/<date>-phase5b-logging-checklist.md` (the housekeeping `apply` step files the design and this plan beside
it, task 5.6) from what was built: **28 rows**, phase-1 legend, mirroring the design's disposition table and this plan's coverage table: 21 ✅,
5 N/A (with the §10 entry 22/23 citation), 2 ⏳ (`OBS-19` → 8b, `OBS-35` → 9). Include the "existing assertions changed" table (R6: the
`ProcessAsync_LogsStructuredEvent_WithRedactedUrl` test and any `DisposalTests` id assertion), the **empty** Security diff evidence of convention
5, the cross-owner table's cited tests, the V1–V7 results with the fallbacks taken, the PR 1 dropped/landed outcome, the styleguide audit groups
(6.1 templates, 6.2 departed, 6.3 levels, 6.4 correlation, 6.7 redact-before-sink), and the deviation ledger P5b-1, P5b-3, P5b-4, P5b-5, P5b-6,
P5b-7, P5b-12, P5b-16, P5b-20 as built (accepted or changed by the lead).

### Task 5.4 — Dated corrections, knowledge note, roadmap note

Frozen documents change only by dated correction. In `docs/sdk-design-dotnet/`:

- **§8.1** (`08-…`): "moves to `Define` delegates with the semconv keys" → `Define` for the fixed-key events and an `ILogger.Log<TState>` record
  for the header-bearing HTTP events (P5b-3); "OBS-2's mapping puts request/response events at `Debug`" → `Information` (P5b-4); the **As built**
  line gains the 5b verdict.
- **§10 entry 22:** add the `OBS-10` and `OBS-24` pointers where they rest on entry 23, unchanged in substance; **no new entry is opened**.
- **§11:** no change (the design's ledger routes P5b-11 to "§11 item 37 unchanged" and P5b-12 to the user page). If the lead wants new items
  for P5b-11 and P5b-12 they are proposed to the lead as a plan-level reading, not planned here.
- **Design corrections owed from R8 and R9** (proposed to the lead, applied only if accepted): position A "readonly struct" → "struct passed by
  `ref`"; the design's API block / position C gain the internal `dexpace.dispose.resource_type` key.
- **§12:** the `OBS` row's notes gain P5b-3's mechanism and the 28/12 split.

New `docs/knowledge/notes/logging-and-observability.md` (role `review`, the shape of the existing notes; `scripts/knowledge verify-structure`
checks it): the styleguide 6.2 departure (SDK HTTP events use `LoggerMessage.Define` and `ILogger.Log<TState>` because the source generator
cannot express OpenTelemetry's dotted keys, constraint 6), plus the SDK overlay row in `docs/styleguide/README.md`'s "SDK overlay" table.
Never edit `harvested/`.

Append to the roadmap: the Phase List row 5's `sdk-design refs` cell gains this design's link (appended, never replacing), and a dated Phase
Status Note recording the PRs as landed (PR 1 landed or dropped), the rulings the lead accepted or changed (P5b-1, P5b-3, P5b-4, P5b-5, P5b-6,
P5b-7, P5b-12, P5b-16, P5b-20), the hand-offs (8b: `OBS-19`, the `SystemNetHttpClient` event ids 1–3 into 110–119, `TRANSPORT-13`; 9:
`OBS-35`; 6a/6b/6c: `CallState.Logger`, the guard and the reserved id ranges), and the two 3b/4c corrections: P5b-13 satisfies 3b's "(the old
response must not be disposed separately)" by `ReplaceBody`, not `WithBody`; the 4c `InstrumentationPolicy` hand-off is closed. `CLAUDE.md`: the
`Diagnostics/` layout line and "What is genuinely unbuilt" (drop "body/header logging" from phase 5's entry). `src/Dexpace.Sdk.Core/README.md`:
the `Diagnostics` row. `docs/first-release.md`: **no entry** (every SHOULD 5b owns is built or ⏳ to a named phase). 3b checklist is **not**
edited: `BODY-34`'s ⏳ 5b clause is closed by 5b's `BodyLoggingTests`, cited in 5b's checklist row and the status note.
**Verify:** the probe's `links` and `citations` checks.

### Task 5.5 — Close-out

`CHANGELOG.md` `### Added`: "`docs/sdk-documentation/logging-and-redaction.md`; the AOT smoke covers `Body`-level logging." Each earlier PR already
carries its own lines. Run **V-gate** and the coverage gate with its self-test. **Commits:** `test: NativeAOT smoke over body-level logging`,
then `docs: phase 5b checklist, user page and dated corrections`.

### Task 5.6 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5b            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5b --write    # git mv (a no-op if the files are already filed)
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix whatever the final probe reports (counts in `CLAUDE.md`/`README.md`, package READMEs, broken links, misattributed IDs). `apply --write`
performs `git mv` only; the plan authorises no commit beyond those in 5.5 and **no push**.

---

## Checklist: one row per owned ID

Exactly 28 rows. **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 5.3 records what was built. "Pin"
means the test already passes on the as-built behaviour and is proven able to fail (convention 1). ⏳ marks a clause with a later owner.

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `OBS-1` | MUST | 1, 3 | 1.1, 1.2, 3.5, 3.7 | ✅ | `AttemptScopeTests.Construction_allocates_nothing`, `DisabledPathAllocationTests.Every_emitter_method_allocates_nothing_at_None`, `…A_pipeline_over_a_synchronous_transport_adds_zero_bytes…` (V3) |
| `OBS-2` | MUST | 3, 4 | 3.5, 3.9, 4.6 | ✅ | `HttpLogEmitterTests.At_Headers_a_request_and_a_response_event_are_emitted_at_Information`, `LogCatalogueTests` (levels) |
| `OBS-3` | MUST | 2, 3 | 2.2, 3.4, 3.9 | ✅ (structured form; `(null)` residual, §10 entry 22) | `LogVocabularyTests.No_key_or_event_name_is_empty_or_collides`, `HttpLogRecordTests.A_null_value_is_carried_as_null` |
| `OBS-4` | MUST | 2, 3 | 2.2, 3.4, 3.9 | ✅ (duplicate-suppression vacuous, §10 entry 22) | `LogVocabularyTests.The_header_prefixes_end_with_a_dot_and_no_key_is_named_event`, `LogCatalogueTests` |
| `OBS-5` | MUST | — | 5.3, 5.4 | N/A (§10 entry 22, `no-log-event-object`) | Checklist row; user page |
| `OBS-6` | MUST | 3 | 3.4, 3.6 | ✅ | `HttpLogRecordTests.Values_are_strings_ints_longs_or_doubles_only`, `EmissionGuardTests.An_exception_whose_Message_throws_does_not_break_the_failure_path` |
| `OBS-7` | SHOULD | 2 | 2.4 | ✅ | `LogTextTests`, `HeaderLogRendererTests.A_very_long_value_is_truncated_after_redaction` |
| `OBS-8` | MUST | — | 5.3 | N/A (§10 entry 22) | Checklist row |
| `OBS-9` | MUST | — | 5.3 | N/A (§10 entry 22) | Checklist row |
| `OBS-10` | MUST | — | 5.2, 5.3 | N/A (§10 entry 23, `activity-as-tracing-model`) | User page recommends `ActivityTrackingOptions`; `DiagnosticContextFlowTests.The_attempt_span_is_current_when_http_request_is_emitted` is the supporting pin |
| `OBS-11` | MUST | 1 (phase 1), 2 | 2.1 | ✅ | `UrlRedactionDefaultDenyTests` (Security, unedited); `UrlRedactorHeaderValueTests.Userinfo_is_masked_on_every_route` |
| `OBS-12` | MUST | 2 | 2.3, 2.5, 2.6 | ✅ (configurable list) | `UrlRedactionDefaultDenyTests`; `RedactionCacheTests`, `InstrumentationPolicyTests.The_url_full_tag_honours_the_calls_allowed_query_parameters` |
| `OBS-13` | MUST | phase 1 | — | ✅ | `UrlRedactionDefaultDenyTests` (Security, unedited) |
| `OBS-14` | MUST | phase 1 | — | ✅ | `UrlRedactionDefaultDenyTests` (Security, unedited) |
| `OBS-15` | MUST | phase 1, 2 | 2.1 | ✅ | `UrlRedactionDefaultDenyTests.Malformed_url_text_yields_the_sentinel`; `UrlRedactorHeaderValueTests.The_malformed_sentinel_is_never_returned` |
| `OBS-16` | MUST | 2 | 2.1 | ✅ | `UrlRedactorHeaderValueTests.Vectors_match`, `An_absolute_value_is_redacted_like_a_request_url` |
| `OBS-17` | MUST | 2, 3 | 2.4, 3.5 | ✅ | `HeaderLogRendererTests.A_url_valued_header_goes_through_RedactHeaderValue`; `HttpLogEmitterTests.A_url_valued_response_header_is_redacted_on_both_paths` (sync and async) |
| `OBS-18` | MUST | 2, 3 | 2.3, 2.4, 3.5 | ✅ | `HeaderLogRendererTests.A_disallowed_header_is_REDACTED_by_default`, `HttpLoggingOptionsTests.The_default_header_allow_list_contains_no_credential_name` |
| `OBS-19` | SHOULD | — | 5.3, 5.4 | ⏳ 8b (`TRANSPORT-13` owns the drop policy; ids 110–119 reserved) | `LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` |
| `OBS-20` | MUST | 3, 4 | 3.2, 3.6, 4.2, 4.4 | ✅ | `EmissionGuardTests` (all), `CallLoggerTests` |
| `OBS-24` | MUST | 3 | 3.8 | ✅ (by the runtime, pinned) | `DiagnosticContextFlowTests.A_caller_activity_and_scope_are_visible_at_the_response_event…` |
| `OBS-34` | MUST | 2, 3, 4 | 2.3, 3.5, 3.9, 4.4 | ✅ | `HttpLoggingDefaultsTests.The_default_options_emit_no_http_event`, `…Spans_and_instruments_still_record_at_None`; 5c's `Spans_and_metrics_record_at_the_default_log_level` |
| `OBS-35` | SHOULD | — | 5.3, 5.4 | ⏳ 9 (binding in the DI package; fail-fast on an unrecognised level, §10 entry 26) | Checklist row; V4 result |
| `OBS-36` | MUST | 2, 4 | 2.3, 4.2, 4.4, 4.5 | ✅ | `BodyLoggingTests.The_consumer_still_receives_every_byte_of_an_over_cap_body`, `BodyPreviewTests` |
| `OBS-37` | SHOULD | 4 | 4.4 | ✅ (stronger than the SHOULD, P5b-12) | `BodyLoggingTests.An_unknown_length_body_is_never_wrapped`, `…A_text_event_stream_body_is_never_wrapped…` |
| `OBS-38` | SHOULD | 4 | 4.3 | ✅ | `BodyPreviewRendererTests.Vectors_match`, `A_cut_multibyte_sequence_yields_the_replacement_character_and_does_not_throw` |
| `OBS-39` | MUST | 2, 3, 4 | 2.2, 3.3, 3.5, 3.9, 4.6 | ✅ | `LogVocabularyTests`, `LogCatalogueTests`, `HttpLogEmitterTests.The_request_event_carries_method_url_resend_count_and_allowed_headers` |
| `OBS-40` | SHOULD | — | 5.3 | N/A (§10 entry 22) | Checklist row |

Count: 28 rows, all mapped (22 MUST, 6 SHOULD: `OBS-7`, `OBS-19`, `OBS-35`, `OBS-37`, `OBS-38`, `OBS-40`). Planned exits: **21 ✅**
(`OBS-1`–`OBS-4`, `OBS-6`, `OBS-7`, `OBS-11`–`OBS-18`, `OBS-20`, `OBS-24`, `OBS-34`, `OBS-36`–`OBS-39`), **5 N/A** (`OBS-5`, `OBS-8`,
`OBS-9`, `OBS-10`, `OBS-40`), **2 ⏳** (`OBS-19`, `OBS-35`): 21 + 5 + 2 = 28. `OBS-11`, `OBS-13`, `OBS-14` and `OBS-15` were fixed by phase 1
and are re-evidenced, not re-built, by 5b; their rows carry no 5b task beyond the header route of task 2.1.

### Work on other owners' rows (no checklist row in 5b)

These carry no exit mark in 5b's checklist (3a precedent). 5b's tests are cited by the owner's row.

| ID (owner) | Work | Task | Evidence the owner cites |
|---|---|---|---|
| `XCUT-19`(c), (e) (phase 10) | Header logging default-deny; body logging off by default (`None`, no wrapper constructed) | 2.3, 3.9, 4.4 | `HeaderLogRendererTests`, `HttpLoggingDefaultsTests`, `BodyLoggingTests.At_Headers_and_None_no_wrapper_is_constructed` |
| `XCUT-20` (phase 10) | The emission guard and the total header redactor; listener callbacks deliberately not wrapped (§11 item 37) | 2.1, 3.6 | `EmissionGuardTests`, `UrlRedactorHeaderValueTests.The_method_is_total` |
| `XCUT-24` (phase 10) | Previews byte-capped and non-consuming by construction of 3b's wrappers, now engaged | 4.5 | `BodyPreviewTests` |
| `BODY-34` (3b; ⏳ 5b) | Engagement only at `Body`, one shared preview size | 4.4 | `BodyLoggingTests`, `HttpLoggingOptionsTests` |
| `RETRY-40`, `REDIR-18`, `REDIR-28` (6a, 6b) | The guarded emitter, the event-id ranges and the logger on `CallState`; 5b emits none of their events | 3.2, 3.5 | — (6a/6b) |
| `P3b-3`, `RECOV`-era hand-off (3b, 4b) | `Disposal` reaches the pipeline's logger from retry, redirect and the response wrapper | 3.3, 4.2 | `RetryPolicyTests`, `RedirectPolicyTests`, `LoggingResponseBodyTests` |
| `PIPE-11` (4c) | The new policy cache keeps every policy field readonly (R3) | 2.5 | `ConcurrencyTests.Every_shipped_policy_holds_only_readonly_instance_fields` (unedited) |
| `OBS-25`, `OBS-27`–`OBS-33` (5c) | None; the shared evidence is the joint zero-allocation assertion (made by whichever lands second, P5c-18(e)) and `AttemptScope`'s lazy URL read by 5c's span tag | 1.2, 3.7 | 5c's checklist |

---

## Traceability: PR → rows → tasks

| PR | Gate | Rows | Tasks |
|---|---|---|---|
| 0 | (before PR 1) | — | Task 0 (queries, V1–V7, sibling check) |
| 1 | task 1.0 (dropped if 5c split) | `OBS-1` (half) | 1.0–1.3 (4) |
| 2 | PR 1 (or 5c's split) | `OBS-7`, `OBS-12`, `OBS-16`–`OBS-18`, `OBS-39` (vocabulary), `OBS-3`/`OBS-4` (vocabulary) | 2.1–2.7 (7) |
| 3 | PR 2 | `OBS-1`–`OBS-4`, `OBS-6`, `OBS-20`, `OBS-24`, `OBS-34`, `OBS-39` | 3.1–3.10 (10) |
| 4 | PR 3 | `OBS-36`–`OBS-38`, `BODY-34` clause | 4.1–4.7 (7) |
| 5 | PRs 1–4 | all 28 (closing) | 5.1–5.6 (6) |
| **Total** | | | **34 numbered tasks plus task 0 (35 in all), in 5 PRs** |

Rows by the PR that first lands them: PR 1: `OBS-1` (half); PR 2: `OBS-7`, `OBS-12`, `OBS-16`–`OBS-18` (5 behavioural rows plus the vocabulary
that `OBS-39` completes in PR 3); PR 3: `OBS-1`, `OBS-2`, `OBS-3`, `OBS-4`, `OBS-6`, `OBS-20`, `OBS-24`, `OBS-34`, `OBS-39`; PR 4: `OBS-36`–`OBS-38`;
phase 1: `OBS-11`, `OBS-13`–`OBS-15`; N/A and ⏳ rows land in PR 5's checklist. The authoritative count is the 28-row table above.

Every PR stays green in the orders the gates allow: PR 1 touches no new public API; PR 2 adds public types with their tests; PR 3 changes
runtime behaviour behind `Level = None`; PR 4 is opt-in behaviour. PR 2's `Logging` property is unread until PR 3, which is a deliberate
two-step (the property and its tests cannot be tested end to end before the emitter reads them).

---

## Findings while planning

Items checked against the repository at `4130f7b` on 2026-10-07, each with what the plan did.

1. **F1 — 5c's design exists on disk and agrees with the split.** P5c-1 claims `OBS-21`–`OBS-23`, `OBS-25`–`OBS-33` and hands `OBS-24` to 5b: 28/12.
   P5b-1's open point reduces to "re-confirm against 5c's merged census" in task 0.
2. **F2 — `AttemptTelemetry` is a struct in 5c's design and a static class in 5b's sketch** (R1). The plan builds the struct form so the
   second lander rebases, not converts. P5b-20 stays open for the lead.
3. **F3 — `ConcurrencyTests` forbids a mutable policy field** (R3). The design's `volatile` slot would fail PIPE-11's test; moved into a
   `RedactionCache` holder.
4. **F4 — The design says the logger is resolved "at `Build`"; the as-built wiring is the `HttpPipeline` constructor** (R4).
5. **F5 — `ProcessAsync_LogsStructuredEvent_WithRedactedUrl` asserts a default-options log entry** (R6). It changes with Breaking 1; the plan edits
   it explicitly and the checklist lists it, rather than letting it fail by accident.
6. **F6 — `RecordingLogger` is private to `InstrumentationPolicyTests` and a second copy lives in the SystemNet security tests.** The plan adds a
   shared one to `TestSupport` and leaves both private copies alone (convention 5: the SystemNet one belongs to a Security class's project).
7. **F7 — `Response.WithBody` does not move exchange links** (design R5, P5b-13): `ReplaceBody` (task 4.1) is the fix, with a pin that `WithBody`
   is unchanged.
8. **F8 — A `[LoggerMessage]` generator cannot be used for the HTTP events** (P5b-3); `Define` keeps the fixed-key ones. V1 decides whether one
   `#pragma warning disable CA1848` is needed.
9. **F9 — `OBS-1`'s pipeline-delta test depends on V3**; its fallback is the emitter-level test, recorded in the checklist.
10. **F10 — The design's guard is a delegate wrapper (`try { emit(); }`); a delegate allocates when it captures.** The plan puts the `try`/`catch` in
    each emitter method (task 3.6) so the disabled path stays allocation-free; the semantics are the design's.
11. **F11 — Disposal's remark and `HttpPipeline`'s docs mention the gap or the old event** (R5). Rewritten in tasks 3.3 and 3.5.
12. **F12 — V7 is added by the plan:** the design's asymmetry test assumes a throwing listener callback propagates; the plan verifies it first and
    states the fallback.
13. **F13 — `OBS-35` and `OBS-19` have no 5b code** and no task beyond the checklist row, the user page and the reserved id range; every other
    row has at least one failing-test task.

---

## What the design could not fully plan, and why

- **Exact `AttemptScope` field layout** (task 1.2): whether the lazy URL slot is a field or a one-element holder depends on a measured zero-
  allocation construction; the plan states both candidates and the test that decides.
- **5a's accessor form** (task 2.3) and **5c's final split** (task 1.0): conditional on what has merged; the plan states the branch for each.
- **V1–V7** (task 0): not runnable on the authoring host; each has a stated fallback.
- **The exact `PublicAPI.Unshipped.txt` text of the record's synthesised members and the dotted `const` lines**: left to the analyzer's code fix,
  as every earlier plan did.
- **`OBS-19` and `OBS-35`**: ⏳ by the design (8b, 9); no 5b task builds them.
