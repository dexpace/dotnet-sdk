# Phase 6a — Retry: Implementation Plan

**Status:** Draft, for review. Written 2026-10-08 against `main` at `3a1db00` (phases 0 to 5c merged), on branch
`70-phase-6-planning` (issue #70). Design: [phase 6a retry design](2026-10-08-phase6a-retry-design.md), the
authority for every decision below. The plan cites its census rows, positions (A–K), facts (1–19) and rulings
(`P6a-1`…`P6a-33`) rather than restating them. Scope authority: the roadmap's Phase 6 card and Phase List row 6. Format
precedent: the [5a plan](../../phase5/phase5a/2026-10-07-phase5a-configuration.md) and its 5b and 5c siblings (all read
first).

**What this document is.** The roadmap's step 3 for sub-phase 6a: numbered TDD tasks in the design's six-PR segmentation, each
with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking** markings, `CHANGELOG.md` entry,
requirement IDs and verification commands. It is not the checklist (step 4, written from what was built in task 6.3) and it
writes no production code. The [checklist section](#checklist-one-row-per-owned-id) below is the plan's own row table: exactly
one row per ID 6a owns, plus the carried-rows table.

**Scope.** 45 rows: `RETRY-1`–`RETRY-45` (39 MUST, 1 MUST NOT, 3 SHOULD, 2 MAY). 6a also does the work for eighteen rows that
stay in the 4b/3b checklists as ⏳: `RECOV-17`–`RECOV-31`, `RECOV-34`, the re-sent clause of `RECOV-16`, the engine clause of
`RECOV-26`, `BODY-5` and the retry third of `BODY-4`. `XCUT-1`–`XCUT-7`, `XCUT-9`, `XCUT-10`, `HTTP-9`, `HTTP-35`, `CFG-35`,
`OBS-28`, `OBS-29`, `PIPE-16`, `PIPE-40` and `TRANSPORT-2` are other owners' rows on which 6a supplies evidence (the
[cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-6a)). Planned exits: 45 ✅, 0 ⏳, 0 🚫, 0 N/A.

**Order and gates.** Six PRs, the design's segmentation unchanged (P6a-32): PR 1 and PR 4 are independent of PR 2; **PR 3
needs PRs 1 and 2; PR 5 needs PR 3**; PR 6 needs 1–5. `RetryPacingOverflowTests` (S7) runs first after PR 2 and PR 3, and
`ReDriveRequestIsolationTests` (S6) first after PR 3 (P6a-31).

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or a changed signature); make the production change; run them green;
   then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is
   proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off). A `git mv`ed file keeps its header.
3. **`src/` code**: `ConfigureAwait(false)` on every `await` (`CA2007`); methods at most 70 lines (`MA0051`; **the waiver on
   `RetryPolicy.ProcessCoreAsync` is deleted in task 3.3, no new waiver is added**, the engine is split as named in 3.2); `///`
   XML docs on every public member (CS1591); no new `PackageReference` in `Dexpace.Sdk.Core` (constraint 2: `FrozenSet`,
   `TimeProvider`, `ExceptionDispatchInfo` are shared framework); no reflection in `src/`; no `double.Parse`/`TryParse`
   anywhere in `Resilience/` (task 2.3 greps for it).
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it); `Integration` for the loopback
   case; `AotSmoke` for smoke checks. 6a adds **no** `Security` class and edits **none** (P6a-31): see
   [Keeping the `Security` classes green](#keeping-the-security-classes-green). Core tests live under
   `tests/Dexpace.Sdk.Core.Tests/<area>/`; doubles under `tests/Dexpace.Sdk.TestSupport/`. `Dexpace.Sdk.Core.Tests` references
   Core and `TestSupport` only (SEAM-2). Waits use `FakeTimeProvider`; none sleeps on the wall clock.
5. **Security tests are never deleted or loosened.** The V-gate's diff check is the exact allowed diff: **empty**.
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
   lists nothing in every PR. A change that seems to force an edit there is a signal to re-read the design, not to edit.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty; a changed member is an edit of its line in `Unshipped`
   and a removed one a deleted line; the file is sorted ordinally and the build's `RS0016`/`RS0017` output is the authority
   (apply the analyzer's code fix and compare with the design's "The public surface" lists). Only core's file changes; the
   SystemNet and STJ files must not (the V-gate checks).
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes, stating
   what it was. The PR's last task adds the `CHANGELOG.md` `[Unreleased]` line for each (design "Breaking changes"). 6b and 6c
   edit the same changelog and `PublicAPI.Unshipped.txt`: whichever PR lands second re-derives its hunks on the merged file,
   never resolves a conflict by keeping either side whole.
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows `IO` inside `Dexpace.Sdk.Core.*`; write `System.IO.IOException` fully
   qualified in core files (`RetryFacts`).
9. **Commits** follow the repository style: `feat!:` breaking feature PR, `feat:` additive, `test:`, `docs:`, `chore:`. **No AI
   attribution** of any kind in a commit message, changelog line or document. The plan authorises no `git push`, no `gh`
   command and no remote action. Branches: `<issue>-phase-6a-<slug>` off `main` (`70-phase-6a-pr1-classifier`, …).
10. **Random and time are injected, never ambient.** Tests reach the random source through the internal constructors
    (P6a-27, `InternalsVisibleTo` already granted to the core test project): a scripted `Func<double>` for exact values, a
    locked `Random(seed)` wrapper for statistical ones. A bare `new Random(seed)` is never shared across threads (fact 5).
11. **Names from siblings are placeholders where 6b/6c touch the same file** (`PublicAPI.Unshipped.txt`, `CHANGELOG.md`,
    `DexpaceLogEvents.cs`, `LogVocabularyTests`, `PipelineBuilder.AddStandardResilience`, `DexpaceClientOptions.cs`,
    `README.md`, `CLAUDE.md`, the roadmap): re-derive hunks, never resolve.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors. The test runner
is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`). **The design was written on a host with no .NET SDK;**
every command below runs for the first time on the implementer's host. Facts 6 (the `long` half), 8, 9, 10 and 19 are
therefore verified in task 0.1 before any code is written.

### Verification blocks

**V-fast** (per task; one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-gate** (the last task of every PR; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/26/27/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # expect empty (convention 5)
git diff --stat -- src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt   # expect empty
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`,
then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and `scripts/ci/coverage-gate-selftest.sh
artifacts/test-results`) runs before the push of PR 3, PR 5 and PR 6. No `PackageReference` changes, so no lock-file change
is expected; if a locked restore complains, run `dotnet restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info RETRY
scripts/knowledge --gaps RECOV              # RECOV-17..RECOV-34 are gap IDs: appendix C rows 245-262 are the only statement
scripts/knowledge --req RETRY-21            # per row in scope of the PR (also XCUT-1, XCUT-7, RECOV-19 for cited work)
```

Task 0.1 records any difference from the design's corpus-file reading. A difference that contradicts a ruling reopens it.

---

## Plan-level readings (where the design was silent, ambiguous or wrong about the tree)

The design's decisions are not changed. **R1–R3 are corrections of fact against the tree at `3a1db00`; R4–R9 are additions.**
Those that add behaviour are flagged for the lead.

- **R1 — `ErrorBodyBuffer` and `ErrorMapping` are `internal` in `Http/Response` and `Errors`.** They are reachable from
  `Resilience` (same assembly); no visibility change. `ErrorBodyBuffer.Capture`/`CaptureAsync` take `(Response,
  CancellationToken)` and return the response with a buffered body; `ErrorMapping.ToException(Response)` throws for a status
  outside 400–599 (fact 12).
- **R2 — `HttpDate` lives in `Http/Common/HttpDate.cs`, not `Configuration/`.** `TimeProviderWaits` is in `Configuration/`
  (`Sleep(this TimeProvider, TimeSpan, CancellationToken)`, `DelayAsync(…) : Task`). `RetryPacing` calls `HttpDate.TryParse(string?,
  out DateTimeOffset)`; it needs a string, so the grammar screen for the date form is "not digits, then `HttpDate.TryParse`".
- **R3 — `OperationTelemetry`'s three methods are `internal static` on `Diagnostics/OperationTelemetry.cs`** with the signatures
  `RetrySequenceStarted(PipelineContext)`, `AttemptFailed(PipelineContext, Response?, Exception?, TimeSpan)` and
  `RetriesExhausted(PipelineContext, int attempts)`. The engine does not call them (it must not reference `Pipeline`): the
  stage adapter passes an `IRetryObserver` of three internal delegates. `Diagnostics` is not `Pipeline`, but `PipelineContext`
  is, so the observer is the seam.
- **R4 — `RetryEngine` is an `internal sealed class`, not static,** so the two stacks share one configured instance per
  adapter (`TimeProvider`, `Func<double>`); all per-call state is a local of `RunAsync` (`RETRY-42`, `RECOV-28`). The
  split under `MA0051` is fixed in task 3.2: `RunAsync` (the loop), `SendOnceAsync`, `Decide`, `ResolveDelay`, `ReleaseAsync`,
  `Terminal`, each under 70 lines.
- **R5 — `RetryAttemptContext` is a public `readonly record struct`** in `Pipeline/Policies` (design G). Its `Response` member
  is a live `Response` the hook must not dispose; the record's synthesised `ToString` would print it, so the struct overrides
  nothing but the XML docs say so, and `PublicAPI` lines are taken from the analyzer.
- **R6 — `ForAttempt` is 0-based; `RetryAttemptContext.Attempt` and the attempt-header ordinal are 1-based** (fact 14, P6a-26):
  send `n` (1-based) drives `context.ForAttempt(n - 1)` and stamps `n`. A test pins both.
- **R7 — `RetryOptions.RetryableStatusCodes` accepts `IReadOnlySet<int>`; the `init` copies to `FrozenSet<int>` and stores the
  frozen set** (so a later mutation of the caller's `HashSet` changes nothing). The default is
  `RetryFacts.DefaultRetryableStatusCodes` (a `FrozenSet<int>`), shared, never mutated.
- **R8 — `OperationPolicy` becomes non-static-core:** its `ProcessCoreAsync` is currently `static`; it gains a `TimeProvider`
  field, so it becomes an instance method. The validation of `OverallTimeout`/`AttemptTimeout` (task 4.3) moves the
  `ts <= TimeSpan.Zero` branch out of the policy (the option can no longer hold it); the policy keeps `is not { } ts`.
- **R9 — Tests that assume four sends, the removed option, the old defaults, or a delay bounded by `MaxDelay`** are found by grep
  (`MaxRetryAttempts`, `RetryNonIdempotentWhenReplayable`, `Times(4)`/`CallCount == 4`, `s_retryableStatusCodes`, `<= maxDelay`/
  `MaxDelay`) and re-derived **in the PR that breaks them**: those broken by the option changes (the removed property, the new
  defaults, the `ToString` member list, the AOT smoke) in PR 2 (task 2.4); those broken by the engine in PR 3 (task 3.1); those
  broken by the timeout change in PR 4 (task 4.1). Selection is by reliance on `new RetryOptions()` defaults, not by `Equal(4`
  matches alone: a test that passes `MaxRetryAttempts = 3` explicitly keeps its four sends. The list is written into the 6a checklist's "existing assertions changed" table (task 6.3).

---

## Task 0.1 — Pre-flight: queries and the to-verify facts (no PR; no commit)

**Why first.** Facts 6 (the `long` half), 8, 9, 10 and 19 were never run (no SDK on the design host); tasks 2.2, 2.3, 4.2, 4.3
and 3.3 depend on them.

**Do.**

1. Run the five phase-start queries and compare with the design's table. Record every difference in
   `$SCRATCH/phase6a-preflight.md`; a difference that contradicts a ruling goes to the lead before PR 1 starts.
2. Create a throwaway console project **outside the repository** (scratchpad directory, `net10.0`, no package references) and
   print, for each fact:
   - **6 (long half)** — `long.TryParse("+5", NumberStyles.None, CultureInfo.InvariantCulture, out _)`, `" 5"`, `"1e3"`, `"5 "`
     (expect `false` for all four); `"0005"` true.
   - **8** — `(long)1e20`, `(long)double.PositiveInfinity`, `(long)double.NaN` (expect `long.MinValue`/undefined, never a
     saturation); this is why task 2.2 compares before casting.
   - **9** — `new CancellationTokenSource(TimeSpan.FromMilliseconds(50), fakeTimeProvider)` fires on `fake.Advance(50 ms)`;
     `new CancellationTokenSource(TimeSpan.FromMilliseconds(uint.MaxValue), TimeProvider.System)` throws
     `ArgumentOutOfRangeException`; the largest timer-accepted value (`uint.MaxValue - 1` ms, about 49.71 days) does not. The design rules `(0, 49 days]`, inside
     that limit, for task 4.3.
   - **10** — a loopback `HttpListener`/Kestrel-free socket server (or the existing `Loopback/` server in
     `Dexpace.Sdk.Http.SystemNet.Tests`) streaming a body slowly; `HttpClient.SendAsync(…, ResponseHeadersRead, cts.Token)`,
     dispose `cts` after the headers arrive, read the body to the end. Expect: no cancellation. If it faults, task 3.4 keeps
     the attempt source alive until the response is released instead of until the drive returns (design fact 10 reopens
     P6a-23's "disposed when the drive returns").
   - **19** — two interface overloads `Bind(IHttpClient)` / `Bind(IAsyncHttpClient)` and an argument implementing both: expect
     `CS0121` (confirms no public `Bind` pair; nothing in the plan depends on a pair).
   - **Extra** — `new StackTrace().FrameCount` inside 10 000 sequential `await` iterations of an `async ValueTask` loop is
     constant (the premise of task 3.2's `RETRY-30` test).
3. Record `grep -rn "RetryNonIdempotentWhenReplayable\|s_retryableStatusCodes\|MaxRetryAttempts" src tests docs README.md`
   and `grep -rn "CallCount.*4\|Times(4)\|Equal(4\|<= maxDelay\|MaxDelay" tests/Dexpace.Sdk.Core.Tests tests/Dexpace.Sdk.AotSmoke` hits: the input to R9 and task 3.1.
4. Confirm `Method.IsIdempotent` is the only reader of `RetryFacts.IdempotentMethods` in `src` (`grep -rn IdempotentMethods src`).

**Exit:** the scratch note exists; nothing is committed.

---

## PR 1 — Classifier, capability, re-send gate

**Gate: none.** Rows: `RETRY-1`–`RETRY-8`, the classifier halves of `RETRY-23`–`RETRY-25`, `RECOV-17`, `RECOV-18`; evidence for
`XCUT-4`–`XCUT-7`, `XCUT-9`, `XCUT-10`, `BODY-4`, `BODY-5`, `CFG-35`. Files: `src/Dexpace.Sdk.Core/Resilience/RetryFacts.cs`
(moved), `Errors/{IRetryableError,SdkException,TransportExceptions}.cs` and `HttpResponseException` (wherever it is declared
in `Errors/`), `Http/Common/Method.cs`, `PublicAPI.Unshipped.txt`, the tests below.

### Task 1.1 — Move `RetryFacts` to `Resilience` and add the architecture fact (`HTTP-9`; P6a-4)

1. `git mv src/Dexpace.Sdk.Core/Pipeline/Policies/RetryFacts.cs src/Dexpace.Sdk.Core/Resilience/RetryFacts.cs`; namespace
   `Dexpace.Sdk.Core.Resilience`; update the `Method.cs` using (`RetryPolicy.cs` does not reference `RetryFacts`, so it gets no using); rewrite the two `<see cref="RetryPolicy"/>` crefs in `RetryFacts`' XML docs as `<c>RetryPolicy</c>` (a `Pipeline.Policies` using would break `HTTP-9`/P6a-4, and an unresolved cref is CS1574 under warnings-as-errors). `git mv` the test to
   `tests/Dexpace.Sdk.Core.Tests/Resilience/RetryFactsTests.cs` (namespace `…Tests.Resilience`). Behaviour unchanged.
2. In `RecoveryLayerArchitectureTests` add `No_resilience_type_references_a_pipeline_type` (same `TypeReferences.Of` walk over
   types in `Dexpace.Sdk.Core.Resilience`; `Assert.NotEmpty` on the type set so the fact cannot pass vacuously). It is green at
   once with `RetryFacts` alone; it stays green through PRs 2–5 and is the proof the engine never draws a `Resilience` →
   `Pipeline` edge.

**IDs:** none new (move). **Verify:** build, V-fast `RetryFactsTests`, `RecoveryLayerArchitectureTests`. **Commit:** `chore:
move RetryFacts into the Resilience namespace`.

### Task 1.2 — Failing tests: the capability and the classifier (`RETRY-1`–`RETRY-4`, `RETRY-23`–`RETRY-25`, `RETRY-37`, `RECOV-17`; P6a-7, P6a-8, P6a-9)

New `tests/Dexpace.Sdk.Core.Tests/Resilience/RetryClassifierTests.cs`, class `RetryClassifierTests` (port of Node's
`classify.test.ts`; one case per exception class):

- `Every_status_100_to_599_matches_the_classifier` (`RETRY-1`, `RETRY-3`: for each code, `new HttpResponseException(response
  with code).IsRetryable == RetryFacts.IsRetryableStatus(code)`; the true set has exactly 100 members; 3xx and 1xx are false)
- `The_default_configured_set_is_a_subset_of_the_classifier` (`RETRY-1`, `XCUT-5`: each member of
  `RetryFacts.DefaultRetryableStatusCodes` is `IsRetryableStatus`; the set equals `{408, 429, 500, 502, 503, 504}`, `XCUT-7`)
- `A_service_request_or_response_exception_is_always_retryable` (`RETRY-4`, `XCUT-4`: both report `IsRetryable == true`; both
  types are sealed on that member — reflection `IsFinal` on the getter)
- `A_raw_IO_family_exception_is_retryable` (`[Theory]`: `System.IO.IOException`, `SocketException`, `TimeoutException`,
  `HttpRequestException` with a null `StatusCode`; `RETRY-2`, `RETRY-4`)
- `A_custom_IRetryableError_that_reports_true_is_retryable_and_one_that_reports_false_is_not` (`XCUT-6`)
- `A_capability_that_is_false_does_not_veto_an_IO_cause` (`P6a-8`: a `DeserializationException` over an `IOException` is
  retryable)
- `The_cause_chain_is_walked_to_depth_64_and_is_cycle_safe` (`RETRY-2`, `XCUT-9`: an `IOException` at depth 10 true; a chain of
  100 wrappers false; a two-node cycle terminates false)
- `An_HttpResponseException_decides_by_the_configured_set_alone` (`RETRY-37`, `RECOV-17`, `XCUT-7`: a 501 whose baked flag is
  `false` is retryable under a set containing 501; a 503 is not under a set `{429}`; a `ServiceResponseException` wrapping an
  `HttpResponseException(503)` under `{429}` is **not** retryable although the wrapper's capability is `true`; the walk stops
  at the response-bearing node)
- `A_cancelled_call_token_is_never_retryable_whatever_the_exception` (`RETRY-23`: an `IOException` with the token signalled is
  `false`)
- `A_TaskCanceledException_over_a_TimeoutException_with_an_unsignalled_token_is_retryable` (`RETRY-24`) and
  `A_TaskCanceledException_with_no_TimeoutException_is_not` (caller cancellation)
- `ThreadInterruptedException_is_not_retryable` (§6.1)
- `A_fatal_exception_is_never_classified` (`RETRY-25`: `IsRetryableFailure(new OutOfMemoryException(), …)` is not reached by an
  engine catch — pinned in task 3.1 against the engine; here the classifier is asserted to return `false` for it)
- `IsRetryableCause_still_passes_its_5a_cases_and_accepts_a_custom_IRetryableError` (`CFG-35`: the 5a `IsRetryableCause_*`
  cases keep passing unchanged; add the capability case)

New `RetryResendGateTests` (`RETRY-5`–`RETRY-8`, `RECOV-18`, `XCUT-10`, `BODY-4`, `BODY-5`; P6a-10):

- `IdempotentMethods_is_exactly_GET_HEAD_OPTIONS_PUT_DELETE` (`RETRY-6`; TRACE, POST, PATCH, CONNECT absent)
- `IsResendable_matrix` (`[Theory]`, five methods × {no body, bytes, string, form-urlencoded, file, seekable stream with a
  declared length, single-use stream, empty-but-present bytes body}: bodiless → idempotent method only; bodied → `IsReplayable`
  only, method irrelevant; `RETRY-5`, `BODY-5`). The expected value is computed in the test from the definition, not from
  `IsResendable`.
- `A_bare_POST_is_not_resendable_but_a_POST_with_a_replayable_body_is` (`RETRY-7`, `XCUT-10`: no special case for a transport
  error)
- `Method_IsIdempotent_reads_the_moved_set` (`HTTP-9`)

Red: CS0117/CS0246 (`IRetryableError`, `IsRetryableFailure`, `IsResendable`, `DefaultRetryableStatusCodes` and
`HttpResponseException.IsRetryable` do not exist). The engine-level half of `RETRY-8`'s matrix (send counts) is task 3.1's.

### Task 1.3 — The capability and the classifier (`RETRY-1`–`RETRY-4`, `RETRY-6`; P6a-7, P6a-8) — additive

**Production.**

- New `Errors/IRetryableError.cs` (design B, verbatim, with XML docs: "implement this on a custom exception to make it
  retryable; core never reads a type name").
- `SdkException : IRetryableError`, `public virtual bool IsRetryable => false`. `ServiceRequestException` and
  `ServiceResponseException`: `public sealed override bool IsRetryable => true`. `HttpResponseException`: a `readonly bool
  _isRetryable = RetryFacts.IsRetryableStatus(response.Status.Code)` assigned in the constructor, `public sealed override bool
  IsRetryable => _isRetryable` (`RETRY-3`: computed once).
- `Resilience/RetryFacts.cs`: add `DefaultRetryableStatusCodes` (`FrozenSet<int>`), `IsRetryableFailure(Exception failure,
  IReadOnlySet<int> retryableStatuses, CancellationToken callToken)` exactly as design B (call token first; breadth-first walk
  over `ExceptionFacts.EnumerateCauses`; `HttpResponseException` decides and stops; a `true` capability or the I/O family
  makes `true`; otherwise `false`; `TaskCanceledException` over `TimeoutException` is covered by the I/O family because its
  inner is a `TimeoutException`), widen `IsRetryableCause` with the capability (no `HttpResponseException` branch, no token),
  add `IsResendable(Request)` and make `IdempotentMethods` the single set `Method.IsIdempotent` reads.
- `PublicAPI.Unshipped.txt`: the `E.IRetryableError` and `IsRetryable` lines of the design (analyzer-driven).

No engine behaviour changes yet: `RetryPolicy` keeps its own `IsRetryableException` until PR 3.

**IDs:** `RETRY-1`–`RETRY-8`, `RETRY-23`–`RETRY-25` (classifier halves), `RECOV-17`, `RECOV-18`; evidence `XCUT-4`–`XCUT-7`,
`XCUT-9`, `XCUT-10`, `BODY-4`, `BODY-5`, `CFG-35`. **Verify:** V-fast `RetryClassifierTests`, `RetryResendGateTests`,
`RetryFactsTests`.

### Task 1.4 — A pin: `HttpResponseException.IsRetryable` on a constructed response and the sealed overrides (`RETRY-3`, `XCUT-5`)

In `RetryClassifierTests`: `IsRetryable_is_computed_once_in_the_constructor` (a response whose `Status` is read through a
counting wrapper is not applicable — `Status` is a value type; instead assert the property is backed by a `readonly` field via
reflection and returns the same value on 1000 reads); `The_overrides_are_sealed` (reflection). Passes after 1.3; prove each
can fail by making the override virtual-computed (never committed).

### Task 1.5 — Close-out (PR 1)

`CHANGELOG.md` `[Unreleased]`: additive line (`IRetryableError`, `SdkException.IsRetryable`) and the **Breaking 4** classifier
line is *not* added yet (it takes effect in PR 3 when the policy adopts the classifier; the PR 3 close-out adds it). Run V-gate.
**Commit:** `feat: the retryable-error capability and the single classifier`.

---

## PR 2 — Options, backoff, pacing

**Gate: PR 1** (`RetryOptions` status validation reads `RetryFacts`). **`RetryPacingOverflowTests` runs first.** Rows:
`RETRY-9`–`RETRY-22`, `RETRY-41` (config half), `RETRY-43`, `RECOV-21`–`RECOV-26`, `RECOV-29`, `RECOV-34`. Files:
`Configuration/RetryOptions.cs`, `Resilience/{RetryBackoff,RetryPacing}.cs`, `tests/vectors/retry/{backoff,pacing}.json`.

### Task 2.1 — Failing tests: `RetryOptions` members, defaults, validation, equality (`RETRY-12`, `RETRY-41`, `RETRY-43`, `RECOV-34`; P6a-12, P6a-13, P6a-16)

New `tests/Dexpace.Sdk.Core.Tests/Configuration/RetryOptionsTests.cs`:

- `Defaults_are_200ms_2_8s_0_2_and_two_retries` (`RETRY-12`, `RETRY-14`: `MaxRetryAttempts == 2`, `BaseDelay`, `Multiplier ==
  2.0`, `MaxDelay == 8 s`, `Jitter == 0.2`, `FixedDelay is null`, `HonorRetryAfter`, `AttemptHeaderName is null`, status set
  equals the default)
- `MaxRetryAttempts_rejects_a_negative_and_accepts_zero` (`RECOV-34`, `RETRY-41`; `ArgumentOutOfRangeException` from the object
  initializer; `RETRY-41`'s clamp clause is vacuous, P6a-13)
- `Durations_reject_negative_and_above_the_292_year_ceiling_and_accept_the_boundary` (`BaseDelay`, `MaxDelay`, `FixedDelay`;
  boundary `TimeSpan.FromTicks(long.MaxValue / 100)` accepted, one tick more rejected; `TimeSpan.MaxValue` rejected)
- `Multiplier_must_be_finite_and_at_least_one` (`0.99`, `NaN`, `+∞`, `-∞` rejected; `1.0` accepted)
- `Jitter_must_lie_in_0_to_1` (`-0.01`, `1.01`, `NaN` rejected; `0`, `1` accepted)
- `RetryableStatusCodes_rejects_null_and_a_status_outside_400_to_599` (`399`, `600`, `0`, `-1` each throw; `400`, `599` accepted)
- `RetryableStatusCodes_is_copied_so_later_mutation_of_the_source_changes_nothing` (R7)
- `AttemptHeaderName_accepts_null_and_a_token_and_rejects_a_space_or_CRLF` (`HttpHeaderSyntax.IsValidName`)
- `Equality_compares_the_status_set_by_content_and_hashes_alike` (fact 17; two options with equal sets in different insertion
  orders are `Equal` with equal `GetHashCode`)
- `ToString_renders_the_status_set_in_ascending_order`
- `RetryNonIdempotentWhenReplayable_no_longer_exists` (reflection: no such property on `RetryOptions`; Breaking 2)
- `With_derives_a_copy_and_validates_the_changed_member` (`options with { Jitter = 2 }` throws)

Red: compile errors (new members); once stubbed, validation and defaults fail.

### Task 2.2 — Failing tests: backoff and its vectors (`RETRY-9`–`RETRY-11`, `RETRY-13`, `RECOV-21`, `RECOV-26`; P6a-14, P6a-15)

New `tests/vectors/retry/backoff.json` (cites `nodejs-sdk@54aeed4 packages/core/src/retry/backoff.test.ts`; one case per row
with `attempt`, the option values, a pinned `u`, and the expected ticks or `"throws"`; a `note` where the .NET value differs
by tick resolution), loaded through `TestSupport`'s `VectorFile`. New `RetryBackoffTests` (internal API through
`InternalsVisibleTo`):

- `Matches_every_vector` (the table: attempt 1 → base, 2 → ×2, 3 → ×4; capped at `MaxDelay`; `FixedDelay` set → fixed value
  with no growth, no jitter, no `MaxDelay` cap; zero base → zero for any multiplier including `1e308` with attempt 1000)
- `Jitter_is_symmetric_over_d_minus_w_over_2_to_d_plus_w_over_2` (`RETRY-10`: `u = 0` → `d(1 − j/2)`, `u = 1` → `d(1 + j/2)`,
  `u = 0.5` → `d`)
- `Zero_jitter_returns_the_unjittered_delay_and_a_sub_tick_width_returns_the_base` (`P6a-14`)
- `A_negative_sample_floors_to_zero_and_a_hostile_random_never_throws` (`u = -5`, `u = 1e18`, `NaN`: never an exception, result in
  `[0, 365 days]`; design E's "in `[0, cap × 1.5]`" sentence does not follow from its steps, since `u` is not clamped, and is
  corrected here: only `u ∈ [0, 1)` gives `[0, cap × (1 + j/2)]`)
- `Saturates_and_never_overflows` (`RETRY-11`, fact 8: base 1 day, multiplier 1e6, attempt 1000 → `MaxDelay`; `MaxDelay`
  400 days → exactly 365 days; the comparison is in `double` before the cast)
- `Attempt_below_one_is_a_programmer_error` (`ArgumentOutOfRangeException`)
- `Every_result_is_clamped_to_365_days` (`RECOV-26`; S7's backoff case is the evidence, unedited)
- `Random_source_is_called_at_most_once_per_computation` (a scripted source asserts the consumed count)

### Task 2.3 — Failing tests: pacing and its vectors (`RETRY-15`–`RETRY-22`, `RECOV-22`–`RECOV-25`, `RECOV-29`; P6a-17, P6a-18, P6a-19)

New `tests/vectors/retry/pacing.json` (cites Node `pacing.test.ts`, Ruby `policy_test.rb` pacing cases and Java
`RetryAfterParser` cases; Node's/Ruby's host-fact cases are not ported, constraint 10; each divergence carries a `note`
naming the ruling). New `RetryPacingTests`:

- `Matches_every_vector` (header sets → `TimeSpan?` ticks; `now` and `u` per case)
- `Retry_After_delta_seconds_integer_and_fractional` (`5` → 5 s, `1.5` → 1.5 s, `0` → zero, `0.0000001` → 1 tick, 8th decimal
  digit truncated; a leading/trailing SP/HTAB trimmed; only the first of two header values is read)
- `The_strict_grammar_rejects_what_the_BCL_would_accept` (`RETRY-19`: `+5`, `-5`, ` 5` after an interior space, `30d`, `0x1p4`,
  `1e3`, `NaN`, `Infinity`, `.5`, `5.`, `1,5`, `٣` (Arabic-Indic digit), empty → `null`, never zero)
- `A_thirty_digit_numeral_saturates_to_365_days` (`RETRY-18`, P6a-18; `int.MaxValue`, `86400000`, `31536000`, `5184000` as in
  S7)
- `Retry_After_http_date_uses_HttpDate_and_a_past_date_is_zero` (`RETRY-17`: past → `TimeSpan.Zero`, which `Assert.NotNull`
  distinguishes from `null`; future → delta; RFC 850 and asctime → `null`)
- `retry_after_ms_and_x_ms_retry_after_ms_are_digits_only_milliseconds` (precedence: `Retry-After` first, then `retry-after-ms`,
  then `x-ms-retry-after-ms`, `RETRY-21`, `RECOV-24`; a malformed earlier header falls through to a valid later one)
- `X_RateLimit_Reset_is_an_epoch_with_positive_jitter` (`RECOV-25`, P6a-19: delta × `1 + 0.2u` for `u ∈ {0, 0.5, 0.999…}` → in
  `[100%, 120%)`; a past epoch → zero and the random source is **not** consumed; a signed or fractional epoch → `null`)
- `No_header_or_only_malformed_headers_yield_null` (`RETRY-16`)
- `The_parser_is_total` (`RETRY-22`, `RECOV-23`: a property-style loop over 10 000 seeded random strings, including control
  characters and 10 000-character numerals, never throws)
- `No_result_exceeds_365_days_in_ticks` (`RETRY-18`)
- `The_hint_is_not_given_symmetric_jitter` (`RETRY-20`: `Retry-After: 7` with `u` varied → always 7 s)
- `A_throwing_random_source_degrades_to_no_hint_in_the_engine` — written in task 3.1 (engine-level, `RETRY-22`, `RECOV-29`)
- A source scan test `Resilience_contains_no_double_Parse` (`RETRY-19`: reads `src/Dexpace.Sdk.Core/Resilience/*.cs` text via the
  repository root and asserts none contains `double.Parse`, `double.TryParse`, `decimal.Parse`, `Convert.ToDouble`; the
  source-text precedent is `Architecture/Seam2ArchitectureTests`; `Configuration/NoImplicitProxyReadTests` is an IL scan, the alternative model)

Red: CS0103/CS0117 (`RetryBackoff`, `RetryPacing`).

### Task 2.4 — `RetryOptions` (`RETRY-12`, `RETRY-14`, `RETRY-41`, `RETRY-43`, `RECOV-34`; P6a-12, P6a-13, P6a-16) — Breaking 1, 2, 3

**Production.** Rewrite `Configuration/RetryOptions.cs` per design D's table (`field`-backed `init` accessors,
`ArgumentOutOfRangeException` naming the redacted member; `MaxRepresentable = TimeSpan.FromTicks(long.MaxValue / 100)`;
`RetryableStatusCodes` stored as `FrozenSet<int>`; hand-written `Equals(RetryOptions?)`, `GetHashCode`, and `PrintMembers`
in ascending order; the sealed record keeps its synthesised `with` support). Remove `RetryNonIdempotentWhenReplayable` and fix
its two readers in `RetryPolicy` (compile-time: in `CanRetryRequest` replace `request.Method.IsIdempotent || options.RetryNonIdempotentWhenReplayable` by the
**new gate** `request.Body is null ? request.Method.IsIdempotent : request.Body.IsReplayable`, i.e. `RetryFacts.IsResendable`
(present since PR 1), with a `// 6a PR 3 rewrites` comment; deleted in task 3.3) and every test that sets it. The three
`RetryPolicyTests` named for the option are re-pointed **here, in PR 2**, to the new default (a POST with a replayable body
**is** retried; a POST with no body is sent once), which the interim gate satisfies, so no test is skipped. Also re-derive in
PR 2 the three sites broken by the changed defaults and the removed member (R9): `DexpaceClientOptionsTests.
RetryOptions_Defaults_AreCorrect` (`MaxRetryAttempts` 2, `MaxDelay` 8 s, no `RetryNonIdempotentWhenReplayable`, plus the new
members' defaults), `DexpaceClientOptionsTests.ToString_of_the_nested_records_renders_every_member` (the member list: drop the
removed member, add the new ones in ascending order), and `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (~line 560,
`options.Retry.MaxRetryAttempts == 2`; PR 2's V-gate publishes and runs the smoke). XML docs mark **Breaking** on `MaxRetryAttempts` (3→2), `MaxDelay` (30 s→8 s), the removed
property, and the validation (items 1, 2, 3). `PublicAPI.Unshipped.txt`: the `RetryOptions` lines.

Run `RetryPacingOverflowTests` now: its backoff case uses the current `RetryPolicy`, whose delays do not read `Multiplier`, so
it still passes; this run proves the options change alone does not break S7.

**IDs:** `RETRY-12`, `RETRY-14` (default half), `RETRY-41` (config half), `RETRY-43`, `RECOV-34`. **Verify:** V-fast
`RetryOptionsTests`; `--filter-class "*RetryPacingOverflowTests"`.

### Task 2.5 — `RetryBackoff` (`RETRY-9`–`RETRY-11`, `RETRY-13`, `RECOV-21`, `RECOV-26`; P6a-14, P6a-15)

**Production.** New `Resilience/RetryBackoff.cs`: `internal static class RetryBackoff { internal static TimeSpan Compute(int
attempt, RetryOptions options, Func<double> random); }` implementing design E steps 1–6 in order, in `double` over ticks, with
a private `ClampTicks(double)` helper (compare against `MaxClampTicks = TimeSpan.FromDays(365).Ticks` and against
`long.MaxValue` before any `(long)` cast; `NaN` → 0) shared with `RetryPacing`. Also `internal static TimeSpan ClampToCeiling(
TimeSpan)` used by the engine.

**IDs:** as above. **Verify:** V-fast `RetryBackoffTests`.

### Task 2.6 — `RetryPacing` (`RETRY-15`–`RETRY-22`, `RECOV-22`–`RECOV-25`, `RECOV-29`; P6a-14, P6a-17, P6a-18, P6a-19)

**Production.** New `Resilience/RetryPacing.cs`: `internal static TimeSpan? TryGetHint(Headers headers, DateTimeOffset now,
Func<double> random)` per design F. Helpers (each under 70 lines): `TryDeltaSeconds(ReadOnlySpan<char>, out long ticks)` (the
hand-written scan `digits [ "." digits ]`: integer part accumulated in `ulong` with saturation at `MaxClampTicks / TicksPerSecond
+ 1`, fraction to seven digits, remaining digits validated but ignored); `TryDigitsOnly(ReadOnlySpan<char>, out long)` for the
`-ms` headers and the epoch (saturating); `TryRateLimitReset(...)` (delta, floor at zero, jitter `1 + 0.2 × random()` only when
positive, clamp). Header values read through `headers.Get(name)` (first value, `Headers` already case-insensitive), trimmed
of `' '` and `'\t'` only. HTTP-date: when the first value contains a non-digit/non-dot character, call
`HttpDate.TryParse` (R2). No `throw`, no `Parse`, no culture. Constants (`"retry-after-ms"`, `"x-ms-retry-after-ms"`,
`"X-RateLimit-Reset"`) go through `HttpHeaderName`'s well-known set if present, else `private const string` in this file.

Run `RetryPacingOverflowTests` after this task (the policy does not yet call it; it proves the file compiles into the same
test assembly without disturbing S7).

**IDs:** `RETRY-15`–`RETRY-22`, `RECOV-22`–`RECOV-25`, `RECOV-29` (parser half). **Verify:** V-fast `RetryPacingTests`,
`RetryPacingOverflowTests`.

### Task 2.7 — Close-out (PR 2)

`CHANGELOG.md`: Breaking 1 (defaults), 2 (removal; the replayable-body re-send gate is live from this PR via the interim gate), 3 (validation) lines. The jitter and schedule half of Breaking 1 is live only once the engine lands, so its CHANGELOG line is written in task 3.8, not here (the old `DelayFor` is still the live scheduler in PR 2). V-gate; the coverage gate. **Commit:**
`feat!: retry options, backoff and the pacing parser (RETRY-9..RETRY-22)`.

---

## PR 3 — The engine and the stage policy

**Gate: PRs 1 and 2. `RetryPacingOverflowTests` and `ReDriveRequestIsolationTests` run first, and again at the end.** Rows:
`RETRY-13`, `RETRY-14` (stage half), `RETRY-26`, `RETRY-28`–`RETRY-35`, `RETRY-38`–`RETRY-42`, `RETRY-44`, `RETRY-45`;
`XCUT-2`'s `AttemptTimeout` wiring; `OBS-28`, `OBS-29`. Files: `Resilience/{RetryEngine,RetryBudget,RetryAttempt}.cs`,
`Pipeline/Policies/{RetryPolicy,RetryAttemptContext}.cs`, `Diagnostics/DexpaceLogEvents.cs`, `Diagnostics/OperationTelemetry.cs`
(only the exhausted predicate's caller, not its signature).

### Task 3.1 — Failing tests: the engine and the policy (`RETRY-8`, `RETRY-23`–`RETRY-45`; P6a-20, P6a-21, P6a-22, P6a-25, P6a-26)

First re-derive the existing assertions broken by the engine (R9; list from task 0.1 step 3). Those that rely on the new
defaults (`new RetryOptions()`) or on the old schedule are re-derived; tests that pass `MaxRetryAttempts = 3` explicitly through
`MakeOptions(maxRetryAttempts: 3)` (e.g. `ProcessAsync_Repeated503_ReturnsLastResponseAfterMaxAttempts`,
`ProcessAsync_ServiceRequestException_ExhaustsRetries_Rethrows`) keep their four sends and are **not** edited (design fact 18's
"as-built default" wording is wrong for them); drop the explicit argument only where the intent is to pin the new default.
The list:

- the three option-named `RetryPolicyTests` were re-pointed in PR 2 (task 2.4) and now run against the real gate unchanged;
- `RetryPolicyTests.ProcessAsync_OverflowGuard_AllRecordedDelaysAreNonNegativeAndBoundedByMaxDelay` and
  `ProcessAsync_DelayBound_LaterAttemptsArePinnedToMaxDelay` assert `<= maxDelay` under the default `Jitter`, but design E caps
  at `MaxDelay` and then jitters, so a delay can reach `MaxDelay × (1 + Jitter/2)`: assert `<= MaxDelay × (1 + Jitter/2)` (or
  set `Jitter = 0` for the pinned-to-`MaxDelay` case);
- P6a-21's drain of a discarded retryable response through `ErrorBodyBuffer` opens the body and changes the recorded order, so
  re-derive `ReDriveLifecycleTests.The_sync_paths_dispose_the_same_way` (`["send:1", "retried:open", "retried:dispose",
  "send:2"]`, or whatever the drain records), `ReDriveLifecycleTests.A_throwing_dispose_of_a_retried_response_reaches_the_
  pipelines_logger`, `ReDriveLifecycleTests.The_sync_paths_report_a_throwing_dispose_to_the_pipelines_logger`, and
  `LogCatalogueTests` (the retried-503 case with a throwing-dispose `TrackingResponseBody`, ~line 61, and the
  `DisposeSuppressedId` assertion, ~line 123). **Decision (keeps the 5b/OBS contract, so these assertions stay and only the
  recorded sequence moves):** a dispose failure after a successful drain is logged through `CallState.Logger` as
  `dexpace.dispose.suppressed` (task 3.2's `ReleaseAsync` does the drain with `ErrorBodyBuffer`, then disposes the original with
  `Disposal.DisposeQuietlyAsync(response, logger: context.State.Logger)` semantics, never letting it propagate and never
  making it a trail entry); the `Assert.Single(... DisposeSuppressedId)` counts stay at one. If the implementer finds this
  cannot be done without an `ErrorBodyBuffer` change (its plain `Dispose` propagates), **raise it to the lead** before PR 3
  proceeds, since the alternative (trail entry) changes OBS behaviour;
- `RetryTraceEventsTests`' "returns and throws exactly what it did" is re-derived against P6a-28's predicate.

No `Security` class is edited.

New `tests/Dexpace.Sdk.Core.Tests/Resilience/RetryEngineTests.cs` (port of Node `engine.test.ts` tables; the engine is driven
through a test adapter, so these are engine-only; `FakeTimeProvider` + scripted random + a scripted `Func` send):

- `Sends_at_most_maxRetries_plus_one_times` (`RETRY-14`, `RETRY-9`: 0, 1, 2, 5 retries against an always-failing send)
- `Zero_retries_sends_once_and_is_never_exhausted` (`RETRY-41`, P6a-28)
- `The_condition_and_the_resend_gate_must_both_hold` (`RETRY-8`: crossing {retryable, non-retryable} × {resendable, not} →
  expected send counts)
- `A_non_retryable_failure_is_surfaced_unchanged_with_an_empty_trail` (same instance, `ExceptionTrail.GetSuppressed` empty)
- `The_trail_holds_every_prior_failure_oldest_first_and_never_the_surfaced_instance` (`RETRY-34`: one reused exception
  instance across three sends does not attach to itself; a foreign `IOException` carries the trail in `Data`; an
  `SdkException` in `Suppressed`)
- `A_fatal_exception_passes_every_frame_untouched` (`RETRY-25`: `OutOfMemoryException` from the send is rethrown as the same
  instance; one send; empty trail; no observer call)
- `Cancellation_before_a_send_throws_with_the_trail_and_does_not_send` (`RETRY-23`, `RETRY-32`)
- `Cancellation_during_the_wait_surfaces_OperationCanceledException_with_the_token_still_signalled` (`RETRY-26`, `RETRY-23`;
  trail attached)
- `A_success_that_arrives_after_the_token_fired_is_disposed_and_the_call_throws` (`RETRY-32`, P6a-25, Breaking 10)
- `A_zero_delay_continues_inline_without_arming_a_timer` (`RETRY-31`: `FakeTimeProvider` timer count 0)
- `Ten_thousand_retries_with_zero_delay_keep_constant_stack_depth` (`RETRY-30`: the send records `new StackTrace().FrameCount`;
  the max equals the first)
- `Delay_precedence_is_override_then_pacing_then_fixed_then_backoff` (`RETRY-39`; `RETRY-20`: a hint replaces the schedule)
- `A_throwing_or_negative_override_is_logged_and_falls_back` (`RETRY-40`; event 140 asserted in 3.5; here the fallback)
- `A_throwing_pacing_read_degrades_to_no_hint_and_the_upstream_failure_is_still_the_one_surfaced` (`RETRY-22`, `RECOV-29`)
- `A_throwing_release_a_throwing_hook_and_a_throwing_wait_each_fault_the_task_with_the_response_disposed` (`RETRY-33`,
  `RETRY-35`)
- `Both_waits_use_TimeProviderWaits` (`RETRY-26`: sync path asserts `Sleep`, async `DelayAsync`; a delay of 60 days is waited
  as chunks, S7 style)
- `The_TimeProvider_is_never_disposed` (`RETRY-45`: a `TimeProvider` subclass implementing `IDisposable` records no dispose)
- `Concurrent_calls_through_one_engine_do_not_share_state` (`RETRY-42`: 64 parallel runs, each its own send count and trail)

New `RetryBudgetTests` (`RETRY-27`, `RETRY-28`): `Unbounded_never_expires`; `A_zero_total_timeout_disables_the_budget`;
`Aborts_when_elapsed_reaches_the_budget`; `Aborts_when_elapsed_plus_delay_exceeds_the_budget`; `Clamps_the_delay_to_the_remainder`
(all with `FakeTimeProvider`, elapsed from `GetTimestamp`/`GetElapsedTime`, `CFG-16`).

Rewrite `RetryPolicyTests` additions (stage level, using the policy through `TestSupport` pipeline helpers):

- `A_503_then_200_returns_the_200_and_disposes_the_503` (`PIPE-40`; the returned response is not disposed)
- `A_503_after_the_cap_is_returned_live_and_unread` (`PIPE-40`, `RETRY-34`: trail dropped, response undisposed)
- `A_discarded_503_body_is_drained_into_an_HttpResponseException_trail_entry` (P6a-21, Breaking 6; the original is disposed,
  at most 1 MiB read)
- `A_forced_non_error_status_leaves_no_trail_entry` (`ShouldRetry` returns `true` for a 200; the response is disposed)
- `A_drain_failure_becomes_the_entry_and_the_loop_continues`
- `ShouldRetry_true_false_null_and_the_cap_still_applies` (`RETRY-29`, `RETRY-8`: the server-header recipe subclass using
  `X-Should-Retry` with Java's tables `true/1/yes/retry`, `false/0/no/stop` is a test in this file)
- `ShouldRetry_throwing_aborts_with_InvalidOperationException_and_suppressed_failure` (`RETRY-40`; the response disposed;
  a fatal exception passes unchanged)
- `ShouldRetry_is_never_asked_about_a_cancelled_call_or_a_fatal_exception`
- `GetDelayOverride_wins_over_everything` (`RETRY-39`)
- `FixedDelay_replaces_the_backoff_and_still_sits_below_the_pacing_hint` (`RETRY-43`, `RETRY-39`)
- `HonorRetryAfter_false_ignores_all_four_headers` (`RETRY-21`, P6a-17)
- `The_attempt_header_stamps_a_one_based_ordinal_on_a_per_attempt_copy` (`RETRY-38`, `RECOV-31`, P6a-26, R6: sends carry `1`,
  `2`, `3`; the idempotency key stamped upstream is preserved; disabled → the same `Request` instance; the received request is
  never mutated, `RETRY-44`)
- `MaxRetries_on_RequestOptions_wins_and_zero_means_no_retries` (`RETRY-41`, `HTTP-35`)
- `The_policy_is_stateless_across_calls_and_concurrency` (`RETRY-42`: 64 parallel calls through one `RetryPolicy`)
- `Each_attempt_drives_a_fresh_ForAttempt_copy` (`RETRY-44`, `PIPE-16`; 0-based attempt numbers 0, 1, 2 reach the continuation)
- `A_service_request_exception_is_retried_a_raw_IOException_is_retried_and_an_InvalidOperationException_is_not` (Breaking 4,
  `RETRY-2`)
- `The_sync_and_async_paths_agree` (`RETRY-30`/§11 item 12: the same scripted outcome table through `Process` and `ProcessAsync`
  yields the same send count, delays and surfaced exception)
- `RetryPolicy_subclass_cannot_change_its_stage` (`PIPE-36`: the `Stage` override is sealed — reflection)
- `RetryPolicy_carries_no_budget` (`RETRY-28`: reflection asserts `RetryOptions` has no `TotalTimeout`-like member and `RetryPolicy` has no `RetryBudget` field; the stage run is observed to use `RetryBudget.Unbounded`)

`RetryTraceEventsTests` (existing) gains `The_exhausted_event_follows_the_final_predicate` (cap spent while the condition and the
gate held and the effective count above zero → one `dexpace.retry.exhausted`; zero retries → none; non-retryable → none;
non-resendable → none; `OBS-29`).

Red: compile errors (`RetryEngine`, `RetryBudget`, `RetryAttemptContext`, the hooks); once stubbed, behaviour.

### Task 3.2 — `RetryBudget`, `RetryAttempt` and `RetryEngine` (`RETRY-8`, `RETRY-13`, `RETRY-14`, `RETRY-23`–`RETRY-28`, `RETRY-30`–`RETRY-35`, `RETRY-42`, `RETRY-45`; P6a-3, P6a-22, P6a-25)

**Production.**

- `Resilience/RetryBudget.cs`: `internal readonly struct RetryBudget` — `Unbounded`, `For(TimeSpan total, TimeProvider)`;
  `Remaining(long startTimestamp)` / `TryClamp(ref TimeSpan delay, long start)` implementing `RETRY-27`'s three aborts and the
  clamp. Zero disables.
- `Resilience/RetryAttempt.cs`: `internal readonly record struct RetryAttempt(int Send, Outcome Outcome)`; the engine's
  callbacks as named internal delegates in the same file: `RetrySend` (`ValueTask<Outcome>(Request, int send, bool async,
  CancellationToken)`), `RetryCondition` (`bool?(RetryAttempt)`, null defers to the classifier), `RetryDelayOverride`
  (`TimeSpan?(RetryAttempt)`), and the observer record `RetryObserver(Action<Outcome, TimeSpan>? OnAttemptFailed,
  Action<int>? OnExhausted, Action<Exception>? OnOverrideFailed)`. None names a `Pipeline` type (R3; the architecture fact
  from 1.1 proves it).
- `Resilience/RetryEngine.cs`: `internal sealed class RetryEngine(TimeProvider timeProvider, Func<double> random)` with
  `ValueTask<Outcome> RunAsync(RetryRun run, bool async, CancellationToken token)` where `RetryRun` carries `Request`,
  `RetryOptions`, `MaxRetries`, `RetryBudget`, `IReadOnlySet<int>` statuses, `HonorPacing`, `RetrySend`, the two hooks and
  the observer. The loop is design A's, in this method split (R4, each ≤ 70 lines): `RunAsync` (one `while`, locals only:
  `start`, `trail`, `sends`), `SendOnceAsync` (token check, stamp, send, non-fatal throw → `Failure`, post-send token check
  disposing a late `Success`), `Decide` (condition ∧ gate ∧ cap, `IsExhausted` per P6a-28), `ResolveDelay`
  (override → pacing → fixed → `RetryBackoff.Compute`, each step under `try/catch when (!IsFatal)`; clamp to 365 days; budget
  clamp), `ReleaseAsync` (read hint, then drain-and-map or dispose into a trail entry; any throw disposes first; **a dispose failure after a successful drain is logged as `dexpace.dispose.suppressed` through `CallState.Logger`, as the as-built `Disposal.DisposeQuietlyAsync(response, logger: context.State.Logger)` does, not propagated and not a trail entry**, see task 3.1), `Terminal`
  (attach the trail oldest-first via `ExceptionTrail.AddSuppressed`; rethrow `Failure`s with `ExceptionDispatchInfo` at the
  adapter, not here — the engine returns `Outcome`). Waits are `timeProvider.DelayAsync(delay, token)` / `Sleep` (R2; a zero
  delay skips the call and continues inline, `RETRY-31`). `catch (OperationCanceledException)` from a wait attaches the trail
  and returns `Failure(oce)` with the token still signalled. Every `catch` carries `when (!ExceptionFacts.IsFatal(ex))`
  (`RETRY-25`). The re-send gate is `RetryFacts.IsResendable(run.Request)` evaluated once before the loop.
- No `static` mutable state; `Random.Shared.NextDouble` is the default `Func<double>` supplied by the adapters.

**IDs:** as above. **Verify:** V-fast `RetryEngineTests`, `RetryBudgetTests`, `RecoveryLayerArchitectureTests` (the 1.1 fact
now has real types to check).

### Task 3.3 — `RetryPolicy` over the engine; `RetryAttemptContext`; the hooks (`RETRY-29`, `RETRY-39`, `RETRY-40`, `RETRY-44`; P6a-20) — Breaking 7

**Production.**

- New `Pipeline/Policies/RetryAttemptContext.cs` (design G record struct, with XML docs; `Response` "must not be disposed by
  the hook").
- Rewrite `Pipeline/Policies/RetryPolicy.cs` unsealed: constructor `RetryPolicy(TimeProvider? timeProvider = null)` plus an
  `internal RetryPolicy(TimeProvider?, Func<double>)` (P6a-27); `Stage`, `Process`, `ProcessAsync` are `sealed override`; the
  two `protected virtual` hooks per design G, with the XML docs stating "must be pure, stateless and fast" (risk 5) and the
  `X-Should-Retry` recipe as a `<code>` block (`RETRY-29`). `ProcessCoreAsync` is a dozen lines: build the `RetryRun` (effective
  `MaxRetries = context.RequestOptions.MaxRetries ?? options.MaxRetryAttempts`, `RetryBudget.Unbounded`, the options' status
  set, `HonorPacing = options.HonorRetryAfter`, `send = (req, n, async, ct) => drive`), call
  `OperationTelemetry.RetrySequenceStarted(context)` on entry, map the observer to `AttemptFailed`/`RetriesExhausted`
  (before the response is released, fact 14), then convert the terminal `Outcome` (`Success` → return the live response,
  trail dropped; `Failure` → `ExceptionDispatchInfo.Capture(error).Throw()`). The sync path is the same core with `async:
  false` through `SyncPath.GetCompletedResult` (4c pattern, §11 item 12). The hook calls wrap `RetryAttemptContext`; a throwing
  `ShouldRetry` becomes the `InvalidOperationException` of design G.
- **Delete** the `#pragma warning disable/restore MA0051`, the `s_retryableStatusCodes` set, `IsRetryableException`,
  `CanRetryRequest`, `ParseRetryAfter`, `DelayFor`, `SleepAsync`, `IsExhausted` (all replaced by the engine).
- `PublicAPI.Unshipped.txt`: the design's `RetryPolicy` removed/added lines (analyzer-driven).

**IDs:** `RETRY-13`, `RETRY-14` (stage), `RETRY-28`–`RETRY-35`, `RETRY-38`–`RETRY-42`, `RETRY-44`, `RETRY-45`. **Verify:** V-fast
`RetryPolicyTests`, `RetryTraceEventsTests`, `ReDriveRequestIsolationTests`, `RetryPacingOverflowTests`.

### Task 3.4 — `AttemptTimeout`, wired cooperatively (`XCUT-2`; P6a-23) — Breaking 9

In `RetryPolicy`'s send callback: when `context.Options.AttemptTimeout` is set, create `new CancellationTokenSource(timeout,
_timeProvider)` (fact 9), link it with the call's token, drive `context.ForAttempt(n - 1).WithCancellationToken(linked.Token)`,
and dispose the source when the drive returns (or, if task 0.1 fact 10 comes out the other way, when the response is released).
An `OperationCanceledException` from the drive while the attempt source fired and the call's token did not becomes
`Failure(new ServiceRequestTimeoutException(…, ex))`. Tests (in `RetryPolicyTests`): `An_attempt_timeout_surfaces_a_retried_
ServiceRequestTimeoutException` (a send that awaits the token; `fake.Advance(timeout)`; second attempt succeeds); `A_caller_
cancellation_during_an_attempt_is_not_a_timeout_and_is_not_retried`; `No_AttemptTimeout_arms_no_extra_timer` (the S7
`RecordingTimeProvider` sees none); `An_attempt_timeout_without_a_RetryPolicy_is_not_enforced` (pin, documented). XML docs
mark **Breaking** (was: read by nothing). The `Fact 10` loopback case is task 3.7's.

### Task 3.5 — Event 140 and the log-vocabulary edit (`RETRY-40`; `OBS-28`; P6a-28)

`Diagnostics/DexpaceLogEvents.cs`: `RetryDelayOverrideFailed = "dexpace.retry.delay_override_failed"` and
`RetryDelayOverrideFailedId = 140` (const lines per the design); written through `CallState.Logger` with the guard of 5b
(`LoggerMessage.Define<…>` style of the neighbouring events, `LogLevel.Warning`, the hook's exception). The adapter's
`OnOverrideFailed` observer calls it. Tests: `RetryEventTests.A_throwing_delay_override_logs_event_140_once_and_falls_back`
(via `RecordingLogger`; a disabled/throwing logger emits nothing and does not mask, `EmissionGuardTests` precedent).
Edit `LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks`: "140 is the only id in 140–149, nothing in 150–169" (fact
16); 6b and 6c each edit only their own block — re-derive if either has merged.

### Task 3.6 — Concurrency, depth and the `Security` reruns (`RETRY-30`, `RETRY-42`, `RECOV-28`)

If 3.1's depth and concurrency tests are not already green, fix the engine (never the test). Run, in this order and on their
own: `--filter-class "*RetryPacingOverflowTests"`, `--filter-class "*ReDriveRequestIsolationTests"`, then
`--filter-trait "Category=Security"` for both suites; the diff check of convention 5 must be empty.

### Task 3.7 — Fact 10 loopback case and the user-facing remarks

New `tests/Dexpace.Sdk.Http.SystemNet.Tests/…/AttemptTimeoutWireTests.cs` (`Integration`): an `AttemptTimeout` of 5 s on the
loopback server, a response whose body is streamed after the drive returned, read to the end without cancellation (design
"Tests"). Class-level XML remarks on `RetryPolicy` rewritten: the classifier rule, the re-send gate, the schedule, the
pacing headers, the trail, `AttemptTimeout`, the hooks, and a **Breaking** paragraph per items 4–7, 9, 10.

### Task 3.8 — Close-out (PR 3)

`CHANGELOG.md`: Breaking 1 (the symmetric-jitter schedule, now live) and 4, 5, 6, 7, 9, 10 lines and the additive hook/`RetryAttemptContext`/event 140 line. V-gate; coverage
gate. **Commit:** `feat!: the retry engine, the stage policy and AttemptTimeout (RETRY-26..RETRY-45)`.

---

## PR 4 — Timeouts

**Gate: PR 1** (`IsRetryable`). Rows: evidence for `XCUT-1`, `XCUT-2`. Files: `Errors/OperationTimeoutException.cs`,
`Pipeline/Policies/OperationPolicy.cs`, `Pipeline/PipelineBuilder.cs`, `Configuration/DexpaceClientOptions.cs`.

### Task 4.1 — Failing tests (`XCUT-1`, `XCUT-2`, `RETRY-34`; P6a-24)

New `OperationTimeoutExceptionTests` (three constructors, `IsRetryable == false`, derives `SdkException`). Extend
`OperationPolicyTests`:

- `A_deadline_surfaces_OperationTimeoutException_not_cancellation` (`FakeTimeProvider`; a downstream that awaits its token;
  `Advance(overall)`; the caller's token never cancelled)
- `A_caller_cancellation_still_surfaces_OperationCanceledException` (`XCUT-1`)
- `The_inner_trail_is_copied_onto_the_timeout_exception` (`RETRY-34`: retried failures appear in
  `ExceptionTrail.GetSuppressed(timeoutException)`)
- `The_timeout_is_not_retried_by_an_inner_or_outer_classifier` (`IsRetryableFailure(timeout)` false)
- `The_operation_span_records_the_failure` (`OperationTelemetry.Fail`, unchanged)
- `OperationPolicy_parameterless_construction_still_compiles_via_the_optional_parameter` (source compat, Breaking 11)

Re-derive the existing `OperationPolicyTests` broken by Breaking 8: `ProcessAsync_WithShortTimeout_ThrowsWhenTransportHangs` and
`Process_sync_with_short_timeout_throws_when_the_transport_hangs` assert `OperationCanceledException`, but `OperationTimeoutException`
derives from `SdkException`; re-point both to `OperationTimeoutException`, preferably on a `FakeTimeProvider`-driven deadline.
`ProcessAsync_WithZeroTimeout_CompletesNormally` and `ProcessAsync_WithNegativeTimeout_CompletesNormally` become `init`-rejection
tests (`ArgumentOutOfRangeException` from `DexpaceClientOptions`, task 4.3).

New `DexpaceClientOptionsTests` cases: `OverallTimeout_and_AttemptTimeout_accept_null_and_values_in_0_to_49_days` and
`…reject_zero_negative_InfiniteTimeSpan_and_above_the_limit` (P6a-24; boundary: `TimeSpan.FromDays(49)` accepted, `FromDays(49) + 1 tick` rejected, and `uint.MaxValue - 1` ms (about 49.71 days) rejected, since the accepted range is `(0, 49 days]`).

### Task 4.2 — `OperationTimeoutException` and `OperationPolicy` on `TimeProvider` (`XCUT-1`; P6a-24) — Breaking 8, 11

**Production.** New `Errors/OperationTimeoutException.cs` (design I; XML docs). `OperationPolicy(TimeProvider? timeProvider =
null)`: `new CancellationTokenSource(timeout, _timeProvider)` linked with the caller's token (R8; `CancelAfter` removed),
`catch (OperationCanceledException ex) when (deadline fired && !caller.IsCancellationRequested)` → `throw new
OperationTimeoutException(message, ex)` after copying the inner exception's trail with `ExceptionTrail.AddSuppressed` for each
of `GetSuppressed(ex)`. `PipelineBuilder.AddStandardResilience` passes its `timeProvider` through (shared-file rule,
convention 11). `PublicAPI.Unshipped.txt`: the constructor swap and the new type.

### Task 4.3 — Timeout validation (`XCUT-2`; P6a-24, closing 5a's P5a-4 deferral) — Breaking 8

`DexpaceClientOptions.OverallTimeout` and `AttemptTimeout`: `init` accepts `null` or a value in `(0, 49 days]`, else
`ArgumentOutOfRangeException`. Fix any test or sample that set a non-positive `OverallTimeout`. XML docs mark **Breaking**.

### Task 4.4 — Close-out (PR 4)

`CHANGELOG.md`: Breaking 8 and 11 lines, the additive `OperationTimeoutException` line. V-gate. **Commit:** `feat!:
OverallTimeout surfaces a timeout exception; both timeouts validate`.

---

## PR 5 — The recovery stack

**Gate: PR 3.** Rows: `RETRY-14` (equivalence), `RETRY-27`, `RETRY-36`, `RETRY-37`, `RECOV-16` (clause), `RECOV-19`, `RECOV-20`,
`RECOV-27`, `RECOV-28`, `RECOV-30`, `RECOV-31`. Files: `Recovery/{RetryRecovery,RecoveryDispatcher,ResponseRecoveryChain}.cs`.

### Task 5.1 — Failing tests (`RETRY-27`, `RETRY-36`, `RETRY-37`, `RECOV-16`, `RECOV-19`, `RECOV-20`, `RECOV-27`, `RECOV-28`, `RECOV-31`; P6a-5, P6a-6)

New `tests/Dexpace.Sdk.Core.Tests/Recovery/RetryRecoveryTests.cs` (port of Ruby's `resilience/*_test.rb` tables and Node's
`retry-dispatch`), through `RecoveryDispatcher` and the scripted transports in `TestSupport`:

- `Construction_validates_the_total_timeout` (`≥ 0`, `≤` the 292-year ceiling) and `Options_and_TotalTimeout_are_exposed`
- `503_503_200_reaches_the_200` (`RECOV-19`: no `ErrorMappingStep` installed, P6a-6; two sends discarded, the 200 returned)
- `An_exhausted_503_surfaces_the_HttpResponseException_with_the_trail` (`RECOV-20`: cap `MaxRetryAttempts + 1` sends)
- `A_non_retryable_error_status_passes_as_a_Success` (404 not in the set, `RECOV-17`'s pass-through)
- `The_configured_set_decides_a_501` (`RETRY-37`: a set with 501 retries; the baked flag is ignored)
- `A_retryable_status_is_buffered_through_ErrorBodyBuffer_once` (`RECOV-16`, `RETRY-36`: 1 MiB cap; an oversize body does not
  grow memory; the original is disposed)
- `With_ErrorMappingStep_installed_nothing_is_mapped_twice` (P6a-6)
- `The_request_chain_runs_once_and_the_recovery_steps_run_once` (`RECOV-32`/`RECOV-33` unchanged: one idempotency key and one
  client-identity line across three sends; recovery steps see only the terminal outcome)
- `Each_send_sees_the_response_steps` (a counting response step runs per send)
- `A_throwing_response_step_becomes_a_Failure_with_the_response_released` (`RECOV-7`, `RECOV-12` unchanged)
- `The_total_timeout_aborts_on_elapsed_and_on_elapsed_plus_delay_and_clamps_the_delay` (`RETRY-27`; `FakeTimeProvider`; the last
  failure is surfaced **unchanged** with the trail; zero disables)
- `A_pacing_hint_replaces_the_schedule_and_is_clamped_by_the_budget` (`RECOV-22`, `RECOV-21`)
- `A_cancelled_wait_yields_Failure_OperationCanceledException_and_the_dispatcher_rethrows_it_signalled` (`RECOV-27`, `RECOV-11`)
- `The_attempt_header_stamps_from_one_on_every_send` (`RECOV-31`, P6a-26: ordinal 1 on the initial send)
- `The_dispatcher_and_RetryRecovery_hold_no_per_call_state` (`RECOV-28`: 64 parallel `DispatchAsync` calls through one
  dispatcher, per-call send counts and trails)
- `A_dispatcher_without_RetryRecovery_behaves_exactly_as_before` (the 4b `RecoveryDispatcherTests` stay green unedited;
  `Retry` is `null`)
- `MaxRetries_zero_on_RequestOptions_disables_retries_for_the_call` (`HTTP-35`)

New `RetryBudgetEquivalenceTests` (port of Ruby's `budget_equivalence_test.rb`; `RETRY-13`, `RETRY-14`, `RECOV-30`): at the
defaults, the stage stack and the recovery stack both make three sends, wait the same delays under one scripted random
sequence, and surface equal failure types; both route through the same `RetryEngine` instance type (`typeof` check on the
engine field via an internal accessor).

Red: CS0246 (`RetryRecovery`), CS1729 (constructor).

### Task 5.2 — `ResponseRecoveryChain`'s phase entry points (`RECOV-10`, unchanged contract)

Add `internal ValueTask<Outcome> ApplyResponsePhaseAsync(Outcome, bool async, CancellationToken)` and
`internal ValueTask<Outcome> ApplyRecoveryPhaseAsync(...)` over the existing private `RunResponsePhaseAsync`/`RunRecoveryPhaseAsync`
(rename-free; `ApplyCoreAsync` composes them as today). Public `Apply`/`ApplyAsync` are unchanged; the 4b chain tests stay
green unedited.

### Task 5.3 — `RetryRecovery` and the dispatcher composition (`RETRY-27`, `RETRY-36`, `RECOV-16`–`RECOV-20`, `RECOV-27`, `RECOV-28`, `RECOV-30`, `RECOV-31`; P6a-5, P6a-6, P6a-29)

**Production.**

- New `Recovery/RetryRecovery.cs` (design H): `public sealed class RetryRecovery(RetryOptions options, TimeSpan totalTimeout =
  default, TimeProvider? timeProvider = null)` with `Options`, `TotalTimeout`, validated `0 ≤ total ≤ ceiling`, plus an
  `internal` constructor taking a `Func<double>` and an `internal RetryEngine Engine`. XML docs: configuration only.
- `RecoveryDispatcher`: the new three-argument constructor and `Retry` property (existing constructor unchanged, `Retry ==
  null`). `DispatchCoreAsync` with a `Retry`: run the request chain once, build a `RetryRun` whose `Send` is the transport call
  followed by `ApplyResponsePhaseAsync` (a null response → the existing `InvalidOperationException` failure), then the
  buffer-and-map step for a surviving `Success` in the configured set (`ErrorBodyBuffer.CaptureAsync` →
  `ErrorMapping.ToException` → `Failure`), `MaxRetries = requestOptions.MaxRetries ?? options.MaxRetryAttempts`,
  `RetryBudget.For(TotalTimeout, tp)`, `HonorPacing = true`, no hooks, no observer; then run the recovery phase once on the
  engine's terminal outcome and unwrap exactly as the as-built dispatcher does. Without a `Retry` the as-built path is
  untouched. The transport stays a per-call argument (P4b-25): no `Bind` overload exists (fact 19).
- `PublicAPI.Unshipped.txt`: the `R.` lines of the design.

**IDs:** as in the PR header. **Verify:** V-fast `RetryRecoveryTests`, `RetryBudgetEquivalenceTests`,
`RecoveryDispatcherTests`, `ResponseRecoveryChainTests`, `RecoveryLayerArchitectureTests`.

### Task 5.4 — Close-out (PR 5)

`CHANGELOG.md`: the additive `RetryRecovery` and `RecoveryDispatcher` retry-composition lines. V-gate; coverage gate.
**Commit:** `feat: RetryRecovery and the dispatcher's retry composition (RECOV-16..RECOV-31)`.

---

## PR 6 — Close-out

**Gate: PRs 1–5 merged.** Rows: all 45 + 18 carried (closing).

### Task 6.1 — NativeAOT smoke over the new surface (`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`)

`RunAllAsync` calls one more check, `CheckPhase6aRetryAsync()`, in the existing style with its `Expect` helper (no reflection;
`using` additions alphabetical):

- a `RetryPolicy` with `RetryOptions { Jitter = 0, BaseDelay = 200 ms }` over a scripted 503 → 200 transport and a
  `FakeTimeProvider`-equivalent (the smoke uses its own minimal `TimeProvider` subclass if `FakeTimeProvider` is not
  referenced): two sends, the 200 returned;
- `RetryRecovery` through `RecoveryDispatcher`: 503, 503, 200 reaches the 200;
- `OperationTimeoutException` surfaced from `OperationPolicy` under that clock;
- a custom `IRetryableError` exception is retried and a plain `Exception` is not;
- `RetryOptions` rejects a negative `MaxRetryAttempts` and a `Jitter` of 2.

A trim/AOT warning means the source is fixed, not the smoke. **Check:** `dotnet publish tests/Dexpace.Sdk.AotSmoke
--configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` prints "all checks
passed". **Commit:** `test: NativeAOT smoke over the retry surface`.

### Task 6.2 — User documentation

New `docs/sdk-documentation/retry.md`, opening "As built by phase 6a, written against source on <date>". Content, citing IDs
and never copying the design: the two stacks and the one engine; the classifier rule and `IRetryableError`; the re-send gate
and the removed option; `RetryOptions` (defaults, validation, `FixedDelay`, `RetryableStatusCodes`, `AttemptHeaderName`);
backoff and jitter; the four pacing headers and the `X-RateLimit-Reset` epoch caveat (risk 3); the hooks and the
`X-Should-Retry` sample (`RETRY-29`); the trail; `AttemptTimeout` (cooperative) and `OverallTimeout`
(`OperationTimeoutException`); `RetryRecovery` through `RecoveryDispatcher`; telemetry (event 140, the span events); the
`AddStandardResilienceHandler` guidance (set `MaxRetryAttempts = 0`, §11 item 24; the DI package's is phase 9). The migration
table covers all eleven breaking changes. `docs/README.md`'s ownership table gains the row the probe asks for.
`src/Dexpace.Sdk.Core/README.md`: the retry sample uses the new defaults. **Verify:** the probe's `links` check.

### Task 6.3 — The checklist

Write `docs/work/mvp/phase6/phase6a/<date>-phase6a-retry-checklist.md` (task 6.6 files the design and this plan beside it) from
what was built: **45 rows** with the constraint-3 legend, the **carried-rows table** (18 rows), the "existing assertions
changed" table from R9, the unedited-`Security` list with the empty-diff evidence, the vector provenance (Node `@54aeed4`,
Ruby `@5b17395`, Java; cases not ported and why), the facts 6, 8, 9, 10, 19 outcomes from task 0.1, and the deviation ledger as
built (P6a-2, 3, 4, 5, 6, 8, 9, 10, 13, 14, 17, 18, 20, 23, 24, 26, 27, 33) with the lead's rulings on the open ones.

### Task 6.4 — Dated corrections, roadmap note, hand-offs

Proposals from the design's "corrections owed", applied as dated corrections only: design §6.1 and §5.2 **As built** lines,
§10 entry 7, §11 new items (`RETRY-41` vs `RECOV-34`; tick resolution; `RETRY-21`'s list as a switch; large numerals; `RECOV-19`
"every send"), §12 rows; the 4b checklist (`RECOV-17`–`RECOV-31`, `RECOV-34` and the two clauses → the 6a carried-rows table),
the 3b checklist (`BODY-5`, `BODY-4` retry third), the 5a checklist (`CFG-35`'s "6a wires" clause), the 5c checklist (the
`IsExhausted` clause); `CLAUDE.md` (layout gains `Resilience/`, `Recovery/` gains `RetryRecovery`, `Errors/` gains the two
types, "genuinely unbuilt" drops the retry engine); the roadmap's phase 6 row cell and a dated status note. Shared files
(convention 11) are re-derived on the merged file.

### Task 6.5 — Close-out

`CHANGELOG.md` completeness check against the eleven breaking items; run V-gate and the coverage gate; confirm
`git diff --stat main...HEAD -- tests/**/Security` is empty. **Commit:** `docs: phase 6a retry user documentation and checklist`.

### Task 6.6 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 6a            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 6a --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix what the probe reports (citations of `RETRY-*`, `RECOV-*` IDs in code comments, links in the new docs).

---

## Keeping the `Security` classes green

Constraint 5: green, or moved without weakening. **No `Security` class is edited by 6a** (P6a-31); the allowed diff is empty.

| Class | Edit | Evidence in this plan |
|---|---|---|
| `RetryPacingOverflowTests` (S7) | None | run first after tasks 2.4, 2.6 and 3.3, and in 3.6 (design fact 15) |
| `ReDriveRequestIsolationTests` (S6) | None | run first after task 3.3 and in 3.6 |
| `AuthHttpsGuardTests` | None | the guard's `SdkException` is non-retryable with no I/O cause; run in every V-gate |
| `RedirectCredentialHygieneTests`, `RedirectWireTests` | None (6b's) | V-gate |
| `EnsureSuccessErrorMappingTests` | None | `ErrorBodyBuffer`/`ErrorMapping` gain callers, not changes; V-gate |
| `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, `UrlRedactionDefaultDenyTests`; SystemNet `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `MalformedContentTypeWireTests` | None | subjects untouched; V-gate |

---

## Checklist: one row per owned ID

Exactly 45 rows. Evidence names tests by class; the checklist written in task 6.3 records what was built.

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `RETRY-1` | MUST | 1 | 1.2, 1.3 | ✅ | `RetryClassifierTests.Every_status_100_to_599_matches_the_classifier`, `The_default_configured_set_is_a_subset_of_the_classifier` |
| `RETRY-2` | MUST | 1 | 1.2, 1.3 | ✅ | `RetryClassifierTests.A_raw_IO_family_exception_is_retryable`, `The_cause_chain_is_walked_to_depth_64_and_is_cycle_safe` |
| `RETRY-3` | MUST | 1 | 1.2, 1.4 | ✅ | `RetryClassifierTests.Every_status_100_to_599_…`, `IsRetryable_is_computed_once_in_the_constructor` |
| `RETRY-4` | MUST | 1 | 1.2 | ✅ | `RetryClassifierTests.A_service_request_or_response_exception_is_always_retryable` |
| `RETRY-5` | MUST | 1 | 1.2, 1.3 | ✅ | `RetryResendGateTests.IsResendable_matrix` |
| `RETRY-6` | MUST | 1 | 1.2 | ✅ | `RetryResendGateTests.IdempotentMethods_is_exactly_GET_HEAD_OPTIONS_PUT_DELETE` |
| `RETRY-7` | MUST | 1, 2 | 1.2, 2.4 | ✅ | `RetryResendGateTests.A_bare_POST_is_not_resendable_…`; `RetryOptionsTests.RetryNonIdempotentWhenReplayable_no_longer_exists` |
| `RETRY-8` | MUST | 1, 3 | 1.2, 3.1 | ✅ | `RetryEngineTests.The_condition_and_the_resend_gate_must_both_hold` |
| `RETRY-9` | MUST | 2 | 2.2, 2.5 | ✅ | `RetryBackoffTests.Matches_every_vector` |
| `RETRY-10` | MUST | 2 | 2.2, 2.5 | ✅ | `RetryBackoffTests.Jitter_is_symmetric_…`, `Zero_jitter_returns_…` |
| `RETRY-11` | MUST | 2 | 2.2, 2.5 | ✅ | `RetryBackoffTests.Saturates_and_never_overflows`, `Attempt_below_one_is_a_programmer_error` |
| `RETRY-12` | SHOULD | 2 | 2.1, 2.4 | ✅ | `RetryOptionsTests.Defaults_are_200ms_2_8s_0_2_and_two_retries` |
| `RETRY-13` | MUST | 2, 3, 5 | 2.5, 3.2, 5.1 | ✅ | `RetryBudgetEquivalenceTests`; one `RetryBackoff`/`RetryPacing`/`RetryEngine` (architecture of the namespace) |
| `RETRY-14` | MUST | 2, 3, 5 | 2.1, 3.1, 5.1 | ✅ | `RetryEngineTests.Sends_at_most_maxRetries_plus_one_times`; `RetryBudgetEquivalenceTests` |
| `RETRY-15` | MUST | 2 | 2.3, 2.6 | ✅ | `RetryPacingTests.Matches_every_vector`, `retry_after_ms_and_x_ms_…`, `X_RateLimit_Reset_is_an_epoch_with_positive_jitter` |
| `RETRY-16` | MUST | 2 | 2.3, 2.6 | ✅ | `RetryPacingTests.No_header_or_only_malformed_headers_yield_null` |
| `RETRY-17` | MUST | 2 | 2.3, 2.6 | ✅ | `RetryPacingTests.Retry_After_http_date_uses_HttpDate_and_a_past_date_is_zero` |
| `RETRY-18` | MUST | 2 | 2.3, 2.6 | ✅ | `RetryPacingTests.No_result_exceeds_365_days_in_ticks`, `A_thirty_digit_numeral_saturates_…`; S7 unedited |
| `RETRY-19` | MUST | 2 | 2.3, 2.6 | ✅ | `RetryPacingTests.The_strict_grammar_rejects_…`, `Resilience_contains_no_double_Parse` |
| `RETRY-20` | MUST | 2, 3 | 2.3, 3.1 | ✅ | `RetryPacingTests.The_hint_is_not_given_symmetric_jitter`; `RetryEngineTests.Delay_precedence_…` |
| `RETRY-21` | MUST | 2, 3 | 2.3, 3.1 | ✅ | `RetryPacingTests` precedence cases; `RetryPolicyTests.HonorRetryAfter_false_ignores_all_four_headers` |
| `RETRY-22` | MUST | 2, 3 | 2.3, 3.1 | ✅ | `RetryPacingTests.The_parser_is_total`; `RetryEngineTests.A_throwing_pacing_read_degrades_…` |
| `RETRY-23` | MUST | 1, 3 | 1.2, 3.1 | ✅ | `RetryClassifierTests.A_cancelled_call_token_is_never_retryable_…`; `RetryEngineTests.Cancellation_*` |
| `RETRY-24` | MUST | 1, 3, 4 | 1.2, 3.4 | ✅ | `RetryClassifierTests.A_TaskCanceledException_over_a_TimeoutException_…`; `RetryPolicyTests.An_attempt_timeout_surfaces_…` |
| `RETRY-25` | MUST | 1, 3 | 1.2, 3.1 | ✅ | `RetryEngineTests.A_fatal_exception_passes_every_frame_untouched` |
| `RETRY-26` | MUST | 3 | 3.1, 3.2 | ✅ | `RetryEngineTests.Both_waits_use_TimeProviderWaits`, `Cancellation_during_the_wait_…` |
| `RETRY-27` | MUST | 3, 5 | 3.1, 5.1 | ✅ | `RetryBudgetTests` (all); `RetryRecoveryTests.The_total_timeout_aborts_…` |
| `RETRY-28` | MUST | 3, 5 | 3.2, 3.3, 5.3 | ✅ | `RetryBudget.Unbounded` in the stage run; the only budget-carrying public type is `RetryRecovery` (reflection pin in `RetryPolicyTests`) |
| `RETRY-29` | MAY | 3 | 3.1, 3.3 | ✅ | `RetryPolicyTests.ShouldRetry_true_false_null_and_the_cap_still_applies` (`X-Should-Retry` recipe) |
| `RETRY-30` | MUST | 3 | 3.1, 3.6 | ✅ | `RetryEngineTests.Ten_thousand_retries_with_zero_delay_keep_constant_stack_depth` |
| `RETRY-31` | MUST | 3 | 3.1 | ✅ | `RetryEngineTests.A_zero_delay_continues_inline_without_arming_a_timer` |
| `RETRY-32` | MUST | 3 | 3.1 | ✅ | `RetryEngineTests.A_success_that_arrives_after_the_token_fired_is_disposed_…` |
| `RETRY-33` | MUST | 3 | 3.1 | ✅ | `RetryEngineTests.A_throwing_release_a_throwing_hook_and_a_throwing_wait_…` |
| `RETRY-34` | MUST | 3 | 3.1 | ✅ | `RetryEngineTests.The_trail_holds_every_prior_failure_oldest_first_…` |
| `RETRY-35` | MUST | 3 | 3.1 | ✅ | `RetryPolicyTests.A_discarded_503_body_is_drained_…`; `RetryEngineTests` release cases |
| `RETRY-36` | MUST | 5 | 5.1, 5.3 | ✅ | `RetryRecoveryTests.A_retryable_status_is_buffered_through_ErrorBodyBuffer_once` |
| `RETRY-37` | MUST | 1, 5 | 1.2, 5.1 | ✅ | `RetryClassifierTests.An_HttpResponseException_decides_by_the_configured_set_alone`; `RetryRecoveryTests.The_configured_set_decides_a_501` |
| `RETRY-38` | SHOULD | 3 | 3.1 | ✅ | `RetryPolicyTests.The_attempt_header_stamps_a_one_based_ordinal_…` |
| `RETRY-39` | MUST | 3 | 3.1, 3.3 | ✅ | `RetryEngineTests.Delay_precedence_is_override_then_pacing_then_fixed_then_backoff`; `GetDelayOverride_wins_over_everything` |
| `RETRY-40` | SHOULD | 3 | 3.1, 3.5 | ✅ | `RetryPolicyTests.ShouldRetry_throwing_aborts_…`; `RetryEventTests.A_throwing_delay_override_logs_event_140_…` |
| `RETRY-41` | MUST | 2, 3, 5 | 2.1, 3.1, 5.1 | ✅ (clamp clause vacuous, P6a-13) | `RetryOptionsTests.MaxRetryAttempts_rejects_a_negative_…`; `RetryPolicyTests.MaxRetries_on_RequestOptions_wins_…` |
| `RETRY-42` | MUST | 3 | 3.1, 3.6 | ✅ | `RetryEngineTests.Concurrent_calls_through_one_engine_…`; `RetryPolicyTests.The_policy_is_stateless_…` |
| `RETRY-43` | MAY | 2, 3 | 2.2, 3.1 | ✅ | `RetryBackoffTests.Matches_every_vector` (fixed rows); `RetryPolicyTests.FixedDelay_replaces_…` |
| `RETRY-44` | MUST | 3 | 3.1, 3.6 | ✅ | `ReDriveRequestIsolationTests` (S6, unedited); `RetryPolicyTests.Each_attempt_drives_a_fresh_ForAttempt_copy` |
| `RETRY-45` | MUST NOT | 3 | 3.1 | ✅ | `RetryEngineTests.The_TimeProvider_is_never_disposed` |

Count: **45 rows** (39 MUST, 1 MUST NOT, 3 SHOULD, 2 MAY); 45 ✅, 0 ⏳.

### The carried rows (they stay in the 4b/3b checklists; 6a's checklist holds the evidence)

| ID | PR | Task(s) | Planned | Primary evidence |
|---|---|---|---|---|
| `RECOV-16` (re-sent clause) | 5 | 5.1, 5.3 | ✅ | `RetryRecoveryTests.A_retryable_status_is_buffered_through_ErrorBodyBuffer_once` |
| `RECOV-17` | 1, 5 | 1.2, 5.1 | ✅ | `RetryClassifierTests`; `RetryRecoveryTests.A_non_retryable_error_status_passes_as_a_Success` |
| `RECOV-18` | 1 | 1.2 | ✅ | `RetryResendGateTests.IsResendable_matrix` |
| `RECOV-19` | 5 | 5.1 | ✅ | `RetryRecoveryTests.503_503_200_reaches_the_200` |
| `RECOV-20` | 5 | 5.1 | ✅ | `RetryRecoveryTests.An_exhausted_503_surfaces_…`, `The_total_timeout_aborts_…` |
| `RECOV-21` | 2, 5 | 2.2, 5.1 | ✅ | `RetryBackoffTests`; `RetryRecoveryTests.A_pacing_hint_replaces_the_schedule_…` |
| `RECOV-22` | 2, 5 | 2.3, 5.1 | ✅ | `RetryPacingTests`; `RetryRecoveryTests.A_pacing_hint_replaces_…` |
| `RECOV-23` | 2 | 2.3 | ✅ | `RetryPacingTests.The_parser_is_total`, past date → zero |
| `RECOV-24` | 2 | 2.3 | ✅ | `RetryPacingTests` precedence and grammar cases |
| `RECOV-25` | 2 | 2.3 | ✅ | `RetryPacingTests.X_RateLimit_Reset_is_an_epoch_with_positive_jitter` |
| `RECOV-26` (engine clause) | 2, 3 | 2.2, 3.1 | ✅ | `RetryBackoffTests.Every_result_is_clamped_to_365_days`; S7 |
| `RECOV-27` | 5 | 5.1 | ✅ | `RetryRecoveryTests.A_cancelled_wait_yields_Failure_OperationCanceledException_…` |
| `RECOV-28` | 3, 5 | 3.1, 5.1 | ✅ | `RetryRecoveryTests.The_dispatcher_and_RetryRecovery_hold_no_per_call_state` |
| `RECOV-29` | 2, 3 | 2.3, 3.1 | ✅ | `RetryEngineTests.A_throwing_pacing_read_degrades_…` |
| `RECOV-30` | 5 | 5.1 | ✅ | `RetryBudgetEquivalenceTests` |
| `RECOV-31` | 3, 5 | 3.1, 5.1 | ✅ | `RetryPolicyTests.The_attempt_header_…`; `RetryRecoveryTests.The_attempt_header_stamps_from_one_on_every_send` |
| `RECOV-34` | 2 | 2.1, 2.4 | ✅ | `RetryOptionsTests` (validation cases, copy of the status set) |
| `BODY-5`; `BODY-4` (retry third) | 1 | 1.2 | ✅ | `RetryResendGateTests.IsResendable_matrix` |

### Work on other owners' rows (no checklist row in 6a)

| ID (owner) | Work | Task | Evidence the owner cites |
|---|---|---|---|
| `XCUT-1`, `XCUT-2`, `XCUT-3` (10) | `OverallTimeout` → `OperationTimeoutException`; `AttemptTimeout` → retried timeout; prompt cancellable wait | 3.4, 4.2 | `OperationPolicyTests`, `RetryPolicyTests.An_attempt_timeout_…`, `RetryEngineTests.Cancellation_during_the_wait_…` |
| `XCUT-4`–`XCUT-7`, `XCUT-9`, `XCUT-10` (10) | capability, baked flag, configured set, cycle-safe walk, uniform gate | 1.2, 1.3 | `RetryClassifierTests`, `RetryResendGateTests` |
| `HTTP-9`, `HTTP-35` (2a) | moved idempotent set; `MaxRetries = 0` | 1.1, 3.1, 5.1 | `RetryFactsTests`, `MaxRetries_*` tests |
| `CFG-35` (5a) | both classifier halves wired | 1.3 | `RetryFactsTests`, `RetryClassifierTests` |
| `OBS-28`, `OBS-29` (5c) | three calls kept; final exhausted predicate | 3.1, 3.3 | `RetryTraceEventsTests` |
| `PIPE-16`, `PIPE-40` (4c) | fresh drive per attempt; the returned response not disposed | 3.1 | `RetryPolicyTests`, `ReDriveRequestIsolationTests` |
| `TRANSPORT-2` (8b) | no change | — | `docs/sdk-documentation/retry.md` states one retry layer per call path |

---

## Traceability: PR → rows → tasks

| PR | Gate | Rows | Tasks |
|---|---|---|---|
| 1 | none | `RETRY-1`–`RETRY-8`, classifier halves of `RETRY-23`–`RETRY-25`, `RECOV-17`, `RECOV-18` | 1.1–1.5 (5) |
| 2 | PR 1; S7 first | `RETRY-9`–`RETRY-22`, `RETRY-41` (config), `RETRY-43`, `RECOV-21`–`RECOV-26`, `RECOV-29`, `RECOV-34` | 2.1–2.7 (7) |
| 3 | PRs 1, 2; S7 and S6 first | `RETRY-13`, `RETRY-14`, `RETRY-26`, `RETRY-28`–`RETRY-35`, `RETRY-38`–`RETRY-42`, `RETRY-44`, `RETRY-45` | 3.1–3.8 (8) |
| 4 | PR 1 | evidence `XCUT-1`, `XCUT-2` | 4.1–4.4 (4) |
| 5 | PR 3 | `RETRY-27`, `RETRY-36`, `RETRY-37`, `RECOV-16`, `RECOV-19`, `RECOV-20`, `RECOV-27`, `RECOV-28`, `RECOV-30`, `RECOV-31` | 5.1–5.4 (4) |
| 6 | PRs 1–5 | all 45 + 18 carried (closing) | 6.1–6.6 (6) |
| pre-flight | — | — | 0.1 (1) |
| **Total** | | | **35 tasks** (34 in six PRs, plus the pre-flight) |

---

## Findings while planning

Items checked against the repository at `3a1db00` on 2026-10-08, each with what the plan did. R1–R9 above are the readings
these produced.

1. **F1 — The engine must not name `PipelineContext`.** `OperationTelemetry` takes one, so the stage adapter supplies an
   observer of three delegates (R3); the architecture fact from task 1.1 enforces the edge.
2. **F2 — `ErrorBodyBuffer`/`ErrorMapping` are internal and already callable from `Resilience`** (R1); no visibility change.
3. **F3 — `HttpDate` is in `Http/Common`** (R2), so `RetryPacing` references it by that namespace.
4. **F4 — `OperationPolicy.ProcessCoreAsync` is `static` today** (R8); the `TimeProvider` makes it an instance method.
5. **F5 — The design's PR 2 gate says `RetryOptions` status validation reads `RetryFacts`;** the plan keeps that edge and also
   requires PR 2's `RetryPolicy` interim edit (one line) so the build stays green between PR 2 and PR 3 (task 2.4).
6. **F6 — `RETRY-30`'s depth premise is verified in task 0.1** rather than assumed from §6.1.
7. **F7 — S7's backoff case relies on `HonorRetryAfter = false` and `BaseDelay = MaxDelay = 400 days`.** With `Multiplier`
   validated and the 292-year ceiling, 400 days is representable; the plan re-runs S7 after tasks 2.4, 2.6 and 3.3.
8. **F8 — The design host had no .NET SDK;** every command, every `PublicAPI` line and each to-verify fact is checked on the
   implementer's host (task 0.1) and against the analyzer's output in each task.
9. **F9 — `RetryAttemptContext.Response` makes the record struct's synthesised `ToString` print a live response;** the XML docs
   warn, and no log path formats the struct (R5).
10. **F10 — Skipping tests is never used** to bridge PR 2 and PR 3: tests that referenced the removed option are re-pointed in
    task 2.4 to the new default, then re-derived in 3.1.
