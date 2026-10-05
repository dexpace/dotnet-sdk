# Phase 4b — Recovery Chain: Implementation Plan

**Status:** Draft, for review. Written 2026-10-05 against `main` at `0332cef` (2a, 2b, 3a and 3b merged).
Design: [phase 4b recovery design](2026-10-05-phase4b-recovery-design.md), the authority for every decision below.
The plan cites its rows, positions (A–G), facts (1–10) and rulings (`P4b-1`…`P4b-26`) rather than restating them, and
changes none. Scope authority: the roadmap's Phase 4 card and Phase List row 4
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). Format precedent: the
[3a plan](../../phase3/phase3a/2026-10-02-phase3a-io.md) and the
[3b plan](../../phase3/phase3b/2026-10-02-phase3b-bodies.md). This file is filed to
`docs/work/mvp/phase4/phase4b/` by the housekeeping `apply` step of task 7.6.

**What this document is.** The roadmap's step 3 for sub-phase 4b: numbered TDD tasks in the design's landing order (seven
pull-request-sized steps), each with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking**
markings, `CHANGELOG.md` entry, requirement IDs and verification commands. It is not the checklist (step 4, written from
what was built in task 7.3) and it writes no production code.

**Scope.** 34 rows: `RECOV-1`–`RECOV-34` (30 MUST, 3 SHOULD, 1 MAY). Built: `RECOV-1`–`RECOV-16`, `RECOV-32`,
`RECOV-33` (18; `RECOV-16` carries one ⏳ 6a clause). ⏳ 6a: `RECOV-17`–`RECOV-30`, `RECOV-34` (15), and `RECOV-31` (1, with its
`RETRY-38` twin, P4b-2). Census: 4a 20 + 4b 34 + 4c 40 = 94 (design, "Scope, and the phase 4 census"). `RECOV-32`/`RECOV-33`
are 4b's rows; 4c cites them and owns none. P4b-1, P4b-2, P4b-3 and P4b-16 are **open for the lead**; the plan builds on the
design's choices and names, per task, what changes if the lead decides otherwise. Every row maps to a task in the
[traceability table](#traceability-id--pr--task).

**Hand-offs.** 4b depends on nothing from 4a. 4c depends on 4b (design, "The interface 4b hands to 4c"): 4c's
`ErrorMappingPolicy` waits for PR 5; 4c's signature rework of `IdempotencyPolicy`/`ClientIdentityPolicy` waits for PR 6;
`SyncPath` (task 2.2) is added by whichever of 4b's PR 2 and 4c's first PR lands first, and the other reuses it
(task 2.2's entry step).

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or a changed signature); make the production change; run them
   green; then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a
   pin is proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off; Core's set is `System`, `System.Collections.Generic`,
   `System.IO`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`; every other namespace is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await`, `await using` and `await foreach` (`CA2007`); methods at most
   70 lines (`MA0051`); `///` XML docs on every public member (CS1591); no new `PackageReference` in `Dexpace.Sdk.Core`
   (constraint 2: `FrozenSet`, `ExceptionDispatchInfo`, `ReferenceEqualityComparer`, `Guid` are shared framework). A broad
   catch is always `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` (fact 3: gate-clean, no `CA1031` pragma). No
   reflection in `src/`, so `IsAotCompatible` and `IsTrimmable` hold unannotated.
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); `AotSmoke` for the
   smoke checks. 4b adds **no** `Security` class and edits none (design, "`Security` classes kept green"). New core test
   classes live under `tests/Dexpace.Sdk.Core.Tests/Recovery/` (namespace `Dexpace.Sdk.Core.Tests.Recovery`) and
   `…/Errors/` (namespace `Dexpace.Sdk.Core.Tests.Errors`, folder exists). `Dexpace.Sdk.Core.Tests` references Core and
   `TestSupport` only (SEAM-2): no transport. Every chain, dispatcher and step test is a `[Theory]` over `bool async`, so one
   body proves both forms.
5. **Security tests are never deleted or loosened, and 4b edits none.** `EnsureSuccessErrorMappingTests` (S8) must stay
   byte-identical and green through the `ErrorBodyBuffer` re-home; `ReDriveRequestIsolationTests` (S6) and
   `RetryPacingOverflowTests` (S7) are untouched. Each PR's close-out runs
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
   and expects empty output. A change that seems to force an edit there is a signal to re-read design position F and
   `P4b-16`, not to edit the file.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in
   `Unshipped`. The build's `RS0016`/`RS0017` output is the authority (apply the analyzer's code fix and compare). Adding
   PRs: 1 (`ExceptionFacts`, `ExceptionTrail`, `SdkException`), 2 (`Outcome`), 3 (steps, chains), 4 (dispatcher), 5
   (`ErrorMappingStep`), 6 (the two steps, `ClientIdentityMode`, the two changed constructors). Each close-out task (1.5, 2.3, 3.6, 4.2, 5.4, 6.5) runs `git diff --stat main...HEAD -- src/*/PublicAPI.Unshipped.txt` and states the PR's expected files. `Dexpace.Sdk.Http.SystemNet`'s and
   `Dexpace.Sdk.Serialization.SystemTextJson`'s API files never change.
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes,
   stating what it was. The PR's last task adds the matching `CHANGELOG.md` `[Unreleased]` line prefixed **Breaking:**
   (design, "Breaking changes"; constraint 8).
8. **Namespace hazard.** The new public namespace is `Dexpace.Sdk.Core.Recovery`. Inside `Dexpace.Sdk.Core.*` a simple name
   `Recovery` now binds to it; no existing source uses a bare `Recovery.X` (verify with
   `grep -rnE "(^|[^.A-Za-z])Recovery\." src tests` at task 1.1; expect empty). `Outcome` nested types are referred to as
   `Outcome.Success`/`Outcome.Failure`; a test file that also uses `Xunit` imports no `Success`-named type.
9. **One PR per step, code and tests together**; every commit inside a PR is green too. Commit style `feat:` / `feat!:`
   (PRs 1, 5 and 6 carry breaking changes) / `fix:` / `chore:` / `test:` / `docs:`; no AI attribution. **This plan
   authorises no push and no `gh` call**; the lead pushes.
10. **Ports cite their source.** Each ported test carries a header comment naming its source path (constraint 10):
    `nodejs-sdk@c0ff3fd packages/core/src/recovery/<file>.test.ts`; the Ruby case lists come from
    `ruby-sdk@90075b1 docs/work/mvp/phase4/phase4b/2026-09-08-phase4b-recovery-primitives-design.md` ("Testing strategy"),
    because the Ruby gem tests are not in the clone. Node's `cancellation.test.ts` is not ported (a JS-runtime fact).

### Environment

Use the pinned SDK (`global.json`, 10.0.401). If it is not on `PATH`, export `DOTNET_ROOT` and `PATH` for the session's
scratchpad install, as earlier phases did.

### Verification blocks

**V-fast** (inner loop, one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-gate** (the last task of every PR; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # expect empty
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`,
then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and
`scripts/ci/coverage-gate-selftest.sh artifacts/test-results`) runs before the push of PR 3, PR 5, PR 6 and PR 7. No
`PackageReference` changes, so no `packages.lock.json` change is expected; if a locked restore complains, run
`dotnet restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info RECOV
scripts/knowledge --gaps RECOV                 # RECOV-18..RECOV-31: appendix C is their only statement
scripts/knowledge --req RECOV-1                # per row in scope of the PR (PR 1: also XCUT-9, RETRY-25, RETRY-34)
```

---

## Plan-level readings (where the design was ambiguous)

The design's decisions are not changed. Where it left a point open the plan took the reading most consistent with it.

- **R1 — The sync path's shape.** The design's position B gives each chain and the dispatcher one private
  `…CoreAsync(…, bool async)`. The plan names them `ApplyCoreAsync` (chains) and `DispatchCoreAsync` (dispatcher); the sync
  entry point calls `SyncPath.GetResult(ApplyCoreAsync(…, async: false))`. In the `async: false` branch no step's
  `ApplyAsync` and no transport's `ExecuteAsync` is called, so the returned `ValueTask` is always already completed
  (the helper's assertion).
- **R2 — `ErrorBodyBuffer`'s dispose on the success path.** The design says it "disposes the original whether or not the
  drain completed", with the drain's exception primary on failure. The plan keeps today's `EnsureSuccessAsync` behaviour on
  the **success** path (a dispose failure after a *successful* drain propagates from the plain `Dispose`/`DisposeAsync`,
  exactly as the current `finally` does) and uses `Disposal` (primary attached to the trail) only on the **failed-drain**
  path. This is what makes the re-home behaviour-preserving for `EnsureSuccessErrorMappingTests` and is the only observable
  change the design lists (Breaking 5).
- **R3 — `StepFailure`'s signature.** The design gives `StepFailure.ConvertAsync(Exception thrown, Outcome current, bool async)`.
  The plan returns `ValueTask<Outcome>` and adds the token-free sync convenience `Convert(Exception, Outcome)` that calls it
  with `async: false` through `SyncPath`. `current` is the outcome in hand *before* the step ran; the helper releases it
  only when it is a `Success` whose response is **not** the one the step may have returned (a step that throws returned
  nothing, so the in-hand response is always the one to release).
- **R4 — The `IdempotencyKeyStep` key-source seam.** The design's internal `Apply(Request, Func<string> keySource)`
  is `internal Request Apply(Request request, Func<string> keySource)` on the step; the public `Apply(Request,
  CancellationToken)` calls it with `KeyStrategy`. Applicability and the respect-existing rule live in the internal
  method, so the policy cannot drift from the step.
- **R5 — `ClientIdentityStep` composition seam.** Likewise `internal Request Compose(Request request, string tokenLine)`
  holds the Append/Replace logic; the public `Apply` calls it with `string.Join(' ', Tokens)`, and `ClientIdentityPolicy`
  calls it with `context.Options.UserAgent` (read per call).
- **R6 — `Disposal` primary-branch and the activity.** With a primary in flight the failure goes onto the trail **only**
  (P4b-15); the plan therefore also drops `dexpace.dispose.primary_type` from the `Report` method (it can only be set when a
  primary exists, and that branch no longer reports). The no-primary branch is untouched.
- **R7 — Test-support fixtures are public in `TestSupport`.** `TestSupport` is not packable but public to its consumers,
  so every fixture has `///` docs and the two-line header, as the existing `IO/` doubles do.

---

## PR 1 — Exception groundwork: `ExceptionFacts`, `ExceptionTrail`, the `Disposal` repoint

**Gate: none.** Breaking item 4. Rows: groundwork for `RECOV-2`, `RECOV-8`, `RECOV-12` (no row flips here).
Ruling coverage: `P4b-13`, `P4b-14`, `P4b-15`.

### Task 1.1 — Test fixtures (`TestSupport/Recovery/`; additive, green against the old code)

First run the namespace-hazard grep of convention 8. New folder `tests/Dexpace.Sdk.TestSupport/Recovery/`, namespace
`Dexpace.Sdk.TestSupport.Recovery`:

- `CyclicExceptions`: static factories `SelfCycle()` (an exception whose `_innerException` is itself) and
  `TwoNodeCycle()` (a, b with `a.InnerException == b` and `b.InnerException == a`), both closed by reflection on the
  private `_innerException` field (design §5.2; reflection is allowed in `tests/`).
- `StructurallyEqualException`: overrides `Equals`/`GetHashCode` by message (fact 5's discriminator).
- `ReadOnlyDataException`: overrides `Data` to return a read-only dictionary whose indexer setter throws
  `NotSupportedException` (fact 4).
- `ThrowingBodyFactory` is **not** added; `DisposalCountingBody` and the `Recording*Transport`s are reused.

**Failing tests first.** `tests/Dexpace.Sdk.Core.Tests/Errors/RecoveryFixtureTests.cs` (`RecoveryFixtureTests`, `Unit`; the
doubles are trusted only once tested): `SelfCycle_points_at_itself`, `TwoNodeCycle_closes`,
`StructurallyEqualException_equals_a_distinct_instance_with_the_same_message`,
`ReadOnlyDataException_throws_NotSupportedException_on_a_data_write`. Red: CS0246. **Production:** none.
**IDs:** infrastructure. **Verify:** V-fast `RecoveryFixtureTests`.

### Task 1.2 — `ExceptionFacts` (`RECOV-2`, `RECOV-8` groundwork; `XCUT-9`, `RETRY-25` cited; P4b-13)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Errors/ExceptionFactsTests.cs`, class `ExceptionFactsTests`,
`Unit`:

- `IsFatal_is_true_for_OutOfMemoryException_and_its_subtypes` (`OutOfMemoryException`, `InsufficientMemoryException`; fact 8)
- `IsFatal_is_false_for_other_exceptions_including_OperationCanceledException` (`[Theory]`)
- `IsFatal_tests_the_exception_itself_not_its_chain` (an `OutOfMemoryException` inside an `IOException` is not fatal)
- `IsFatal_rejects_null` (`ArgumentNullException`)
- `EnumerateCauses_yields_the_exception_itself_first` (a single exception yields exactly one element, `Assert.Same`)
- `EnumerateCauses_walks_inner_exceptions_breadth_first` (a → b → c order)
- `EnumerateCauses_walks_aggregate_inner_exceptions` (every element of `InnerExceptions`, in order)
- `EnumerateCauses_yields_an_aggregates_first_inner_once` (fact 6: `InnerException` and `InnerExceptions[0]` are one instance;
  the walk's count is exact)
- `EnumerateCauses_terminates_on_a_self_cycle` (count 1, `Assert.Same`) and `…_on_a_two_node_cycle` (count 2, each
  `Assert.Same`)
- `EnumerateCauses_uses_reference_identity` (two distinct `StructurallyEqualException`s chained through an
  `AggregateException` yield as two; fact 5)
- `EnumerateCauses_stops_below_depth_64` (a 100-deep chain yields 65 nodes: the root and 64 levels)
- `EnumerateCauses_is_lazy_and_rejects_null` (`ArgumentNullException` on enumeration or call, whichever the iterator
  design uses; the test pins the chosen one)

Red: CS0103 (no `ExceptionFacts`).

**Production.** New `src/Dexpace.Sdk.Core/Errors/ExceptionFacts.cs`: `public static class ExceptionFacts` with
`IsFatal(Exception)` (`exception is OutOfMemoryException`) and `EnumerateCauses(Exception)` (iterator over a `Queue` of
`(Exception, depth)` and a `HashSet<Exception>(ReferenceEqualityComparer.Instance)`; enqueue `InnerException` and, for an
`AggregateException`, each of `InnerExceptions`, skipping visited nodes; cap depth at 64 for the children enqueued). XML docs
state "root first" in the summary (the name reads the other way, design position E) and that `IsFatal` tests the exception
only. **`PublicAPI.Unshipped.txt`:** add the type and its two methods (take lines from the analyzer fix). **IDs:** groundwork
for `RECOV-2`, `RECOV-8`; cited by `XCUT-9`, `RETRY-25`. **Verify:** V-fast `ExceptionFactsTests`.

### Task 1.3 — `SuppressedTrail`, `ExceptionTrail`, `SdkException.Suppressed`/`ToString` (`RECOV-12` groundwork; `RETRY-34`, `PAGE-13` cited; P4b-14) — Breaking 4

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Errors/ExceptionTrailTests.cs`, class `ExceptionTrailTests`,
`Unit` (a header comment cites `nodejs-sdk@c0ff3fd packages/core/src/suppress.test.ts`):

- `A_secondary_added_to_an_SdkException_lands_on_Suppressed` (`Assert.Same`)
- `A_secondary_added_to_a_foreign_exception_lands_in_GetSuppressed` (a plain `InvalidOperationException`; the trail is
  readable only through `ExceptionTrail.GetSuppressed`)
- `GetSuppressed_of_an_exception_with_no_trail_is_empty`
- `AddSuppressed_to_itself_is_a_no_op` (`RETRY-34`; both an `SdkException` and a foreign one)
- `A_duplicate_secondary_is_added_once` (reference identity)
- `A_fatal_primary_gets_no_attachment` (`OutOfMemoryException` primary: `GetSuppressed` stays empty; `RETRY-25`)
- `A_fatal_secondary_is_ignored`
- `A_read_only_Data_drops_the_secondary_and_does_not_throw` (`ReadOnlyDataException`, fact 4; asserts the primary is
  untouched and `GetSuppressed` is empty)
- `A_snapshot_taken_before_an_attach_is_unchanged_after_it` (the returned list's count and elements)
- `Concurrent_attaches_from_16_tasks_all_land` (distinct secondaries onto one primary, both an `SdkException` and a foreign
  primary; count is exactly 16)
- `Argument_null_checks` (`[Theory]` over both parameters)
- `SdkException_ToString_renders_each_suppressed_exception_as_a_numbered_block` (`(Suppressed Exception #0)`; the base
  rendering comes first)
- `A_mutual_two_exception_trail_renders_cycle_and_terminates` (each `SdkException` suppressed on the other: the output
  contains `(cycle)`)
- `Nesting_is_capped_at_8_levels` (a 20-deep suppression chain terminates; the output has no more than 8 nested
  `(Suppressed Exception` openings)
- `A_foreign_exceptions_ToString_does_not_render_its_trail` (§10 entry 13's residual, pinned)

Red: CS0103/CS1061 (no `ExceptionTrail`, no `Suppressed`).

**Production.**

- New `src/Dexpace.Sdk.Core/Errors/SuppressedTrail.cs`: `internal sealed class SuppressedTrail` holding a private
  `Exception[]` replaced on each attach under a lock (a private `object`); `Add(Exception)` (reference-identity duplicate
  check), `Snapshot()` returns an `IReadOnlyList<Exception>` over the current array (never the live array).
- New `src/Dexpace.Sdk.Core/Errors/ExceptionTrail.cs`: `public static class ExceptionTrail` with `AddSuppressed(Exception
  primary, Exception secondary)` and `GetSuppressed(Exception)`. Rules in order: null checks; `ReferenceEquals` → return;
  either fatal → return; `SdkException` primary → its `SuppressedTrail`; otherwise the foreign path under a static lock
  (first attach creates the `SuppressedTrail` and stores it in `primary.Data["Dexpace.Sdk.Core.Suppressed"]`, inside
  `try { … } catch (Exception ex) when (!ExceptionFacts.IsFatal(ex)) { /* drop the secondary: throwing here would replace
  the primary, the one failure the trail exists to prevent (design §5.2, P4b-14) */ }` — the one deliberate swallow, with
  its why-comment and `NotSupportedException` named).
- `src/Dexpace.Sdk.Core/Errors/SdkException.cs`: add `public IReadOnlyList<Exception> Suppressed { get; }` (backed by a lazily
  created `SuppressedTrail` field; empty list when none) and `public override string ToString()` appending, after
  `base.ToString()`, one block per suppressed exception `---> (Suppressed Exception #n) {inner.ToString()}<---`, with a
  `[ThreadStatic]` `HashSet<Exception>` (reference identity) of exceptions currently rendering (render `(cycle)` for a
  member already in it) and a depth counter capped at 8. The `ToString` remarks carry `<b>Breaking:</b>` (the log output
  changes when a trail exists).

**`PublicAPI.Unshipped.txt`:** add `ExceptionTrail`, its two methods, `SdkException.Suppressed.get`,
`override SdkException.ToString()`. `SuppressedTrail` is `internal`. **IDs:** groundwork for `RECOV-12`; cited by `RETRY-34`,
`PAGE-13`, `SSE-29`/`SSE-36`. **Verify:** V-fast `ExceptionTrailTests`.

### Task 1.4 — Repoint `Disposal` and `LoggingResponseBody` (P4b-15, R6; closes the 3b hand-off)

**Failing tests first.** Edit `tests/Dexpace.Sdk.Core.Tests/Internal/DisposalTests.cs`: the existing
`With_a_primary_in_flight_the_report_carries_the_primary_type_and_the_primary_is_untouched` (line 135) is **rewritten** to
the new contract and renamed `With_a_primary_in_flight_the_failure_lands_on_the_primary_trail_and_nothing_else`: the primary
is not replaced (`Assert.Same`), `ExceptionTrail.GetSuppressed(primary)` holds the dispose failure (`Assert.Same`), and no
`Activity` event is recorded. Add `With_a_primary_in_flight_and_a_logger_nothing_is_logged` (pins the "never a third way"
rule, §3.7), `A_fatal_dispose_exception_still_propagates_with_a_primary` and
`The_no_primary_branch_is_unchanged` (the existing activity-event and logger tests pass untouched). Red: the rewritten test
fails (the trail is empty, an activity event is present). No `Security` class is affected.

**Production.** `src/Dexpace.Sdk.Core/Internal/Disposal.cs`: delete the private `IsFatal` and the "Phase 4b repoints"
remarks; both catch filters become `when (!ExceptionFacts.IsFatal(ex))`; in each catch, `if (primary is not null)
{ ExceptionTrail.AddSuppressed(primary, ex); } else { Report(ex, …); }`; `Report` loses its `primary` parameter and the
`dexpace.dispose.primary_type` tag (R6); the class remarks are updated ("exactly one of two ways", §3.7).
`src/Dexpace.Sdk.Core/Http/Response/LoggingResponseBody.cs` lines 185 and 211:
`when (ex is not OutOfMemoryException)` → `when (!ExceptionFacts.IsFatal(ex))` (same set, fact 8; add `using
Dexpace.Sdk.Core.Errors;` if absent). Grep `ex is OutOfMemoryException` and `is not OutOfMemoryException` across `src/`
afterwards: the only remaining occurrence is `ExceptionFacts` itself. **IDs:** groundwork for `RECOV-12`. **Verify:**
V-fast `DisposalTests`, `LoggingResponseBodyTests`, `ResponseDisposeLatchTests`.

### Task 1.5 — Close-out (PR 1)

`CHANGELOG.md` `[Unreleased]`: under `### Added`: "`ExceptionFacts` and `ExceptionTrail`; `SdkException.Suppressed`
(phase 4b, `RECOV-12`)."; under `### Changed`: "**Breaking:** `SdkException.ToString()` renders the suppressed trail." Run **V-gate**.
**Commit:** `feat!: exception facts and the suppressed trail (RECOV groundwork)`.

---

## PR 2 — `Outcome`, `SyncPath` and the `ValueTask<T>.Result` ban

**Gate: PR 1.** Rows: `RECOV-1`. Rulings: `P4b-3`, `P4b-4`, `P4b-7`. **Open for the lead:** `P4b-3` (the class-not-record
correction); the plan builds the class, which is the only choice that satisfies `RECOV-1`'s "jointly exhaustive".

### Task 2.1 — `Outcome` (`RECOV-1`; P4b-3, P4b-4; facts 1, 2)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Recovery/OutcomeTests.cs`, class `OutcomeTests`, `Unit` (header
cites `nodejs-sdk@c0ff3fd packages/core/src/recovery/outcome.test.ts`):

- `Outcome_has_no_non_private_constructor` (reflection over `typeof(Outcome).GetConstructors(BindingFlags.Instance |
  Public | NonPublic)`: every one is private)
- `The_assembly_holds_exactly_two_types_deriving_from_Outcome` (`Success` and `Failure`, both sealed; scans
  `typeof(Outcome).Assembly.GetTypes()`)
- `A_success_reports_IsSuccess_and_TryGetResponse` and `A_failure_reports_IsFailure_and_TryGetError`
- `The_accessor_pairs_are_never_both_true` (a seeded `[Theory]` over 64 generated outcomes, fixed seed)
- `Match_invokes_exactly_one_branch_exactly_once` (counting delegates, both variants)
- `Match_folds_agree_with_the_predicates` (seeded property test over generated outcomes)
- `Constructors_reject_null` (`ArgumentNullException`, `ParamName` `response` / `error`)
- `ToString_names_the_status_or_the_exception_type_and_never_a_message` (`"Success(NOT_FOUND(404))"`, i.e. via `Status.ToString()`;
  `"Failure(System.IO.IOException)"`; an exception message containing `https://secret.example/token` is absent)
- `TryGet_out_values_are_non_null_when_true` (a compile-time check through `[NotNullWhen]`)

Red: CS0246 (no `Outcome`).

**Production.** New `src/Dexpace.Sdk.Core/Recovery/Outcome.cs` (namespace `Dexpace.Sdk.Core.Recovery`): `public abstract class
Outcome` with `private Outcome() { }`, nested `public sealed class Success : Outcome` (ctor `Success(Response response)`,
`Response` property) and `public sealed class Failure : Outcome` (ctor `Failure(Exception error)`, `Error`), accessors
`IsSuccess`, `IsFailure`, `TryGetResponse([NotNullWhen(true)] out Response?)`, `TryGetError([NotNullWhen(true)] out
Exception?)`, `Match<T>(Func<Response,T>, Func<Exception,T>)` and `ToString` per the design's shape. The one `switch` over
`this` carries `_ => throw new UnreachableException()` (fact 2: `CS8509` still fires without it). `CA1034` does not fire
(fact 3); if it does at build, record the finding rather than adding a pragma without a why-comment. The class remarks cite
P4b-3 and carry no "Breaking" (new type).

**`PublicAPI.Unshipped.txt`:** add `Outcome`, `Outcome.Success`, `Outcome.Failure` and every member (the nested types'
constructors are listed; `Outcome` has no public constructor, so no `Outcome.Outcome()` line). **IDs:** `RECOV-1`. **Verify:**
V-fast `OutcomeTests`.

**If the lead keeps the record (rejecting P4b-3):** the plan stops here: `RECOV-1`'s closed-ness test would fail against
fact 1, so the lead must accept either the class or a documented residual deviation.

### Task 2.2 — `SyncPath` and the `ValueTask<T>.Result` ban (P4b-7; fact 10)

**Entry step.** If 4c's first PR already added `src/Dexpace.Sdk.Core/Internal/SyncPath.cs` and the `BannedSymbols.txt`
entry, reuse them and write only the tests below that are missing (`grep -n "SyncPath" src/Dexpace.Sdk.Core/Internal` and
`grep -n "ValueTask\`1.Result" BannedSymbols.txt`). The result is the same either way.

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Internal/SyncPathTests.cs`, class `SyncPathTests`, `Unit`:

- `A_completed_ValueTask_returns_its_result`
- `A_completed_ValueTask_of_a_faulted_async_method_rethrows_the_original_exception` (fact 10: an `async` method whose sync
  branch threw; `Assert.Same` on the exception, not an `AggregateException`)
- `A_non_completed_ValueTask_throws_InvalidOperationException` (a `ValueTask<int>` over a `TaskCompletionSource` never
  completed; the message names the core-defect nature)
- `The_non_generic_overload_behaves_the_same`

Red: CS0103. `InternalsVisibleTo` for the core test project already exists.

**Production.** New `src/Dexpace.Sdk.Core/Internal/SyncPath.cs`: `internal static class SyncPath` with `GetResult<T>(ValueTask<T>)`
and `GetResult(ValueTask)`: assert `IsCompleted` (else `InvalidOperationException`), read `.Result` (generic) or
`GetAwaiter().GetResult()`-free `IsCompletedSuccessfully`/rethrow path for the non-generic form, inside **one** scoped
`#pragma warning disable RS0030` with a why-comment citing design §5.3 and P4b-7. `BannedSymbols.txt` gains
``P:System.Threading.Tasks.ValueTask`1.Result;Sync-over-async: read a ValueTask<T> only through SyncPath (design §5.3, P4b-7)``
under the "Sync-over-async" group. **Verify the entry fires** with a throwaway probe in the scratchpad (a scratch copy of the
project with one `.Result` read, never committed), as 2b and 3a did: an unmatched documentation ID is silently ignored, so an
entry that does not fire is a bug in the entry. `src/` has no other `.Result` read on a `ValueTask<T>` (grep at planning:
none). **`PublicAPI.Unshipped.txt`:** unchanged. **IDs:** infrastructure for `RECOV-2`–`RECOV-8`. **Verify:** V-fast
`SyncPathTests`; `dotnet build Dexpace.Sdk.sln --configuration Release`.

### Task 2.3 — Close-out (PR 2)

`CHANGELOG.md` `### Added`: "`Dexpace.Sdk.Core.Recovery.Outcome`, the closed success-or-failure carrier (`RECOV-1`)."; `###
Added`: "`RS0030` entry for `ValueTask<T>.Result` outside `SyncPath`." Run **V-gate**. **Commit:** `feat: the closed Outcome and the
sync path helper (RECOV-1)`.

---

## PR 3 — Step contracts, the two chains, `StepFailure`

**Gate: PR 2.** Rows: `RECOV-3`–`RECOV-9`, `RECOV-12`–`RECOV-14`. Rulings: `P4b-5`, `P4b-6`, `P4b-8`, `P4b-9`, `P4b-12`,
`P4b-22`, `P4b-26`.

### Task 3.1 — Step contracts and the delegate fakes (`RECOV-3`, `RECOV-4`, `RECOV-9`, `RECOV-13`; P4b-6, P4b-26)

New `src/Dexpace.Sdk.Core/Recovery/IRequestStep.cs`, `IResponseStep.cs`, `IRecoveryStep.cs`, shapes per the design ("Step
contracts"): each with required `Apply` and `ApplyAsync`, a `CancellationToken` and nothing else (no context, P4b-6).
`IRecoveryStep`'s docs state the preference "return a `new Outcome.Failure(…)` rather than throw" (`RECOV-9`) and that a
step that returns a different outcome owns what it dropped (`RECOV-13`); every step contract's docs state the concurrency
contract (`RECOV-14`: one instance may be applied concurrently; keep per-call state in the value).

New `tests/Dexpace.Sdk.TestSupport/Recovery/`: `DelegateRequestStep`, `DelegateResponseStep`, `DelegateRecoveryStep` —
lambda-backed, each constructed from one sync delegate and one optional async delegate (defaulting to wrapping the sync one),
recording call count and the last token (P4b-26: not in the public surface).

**Failing tests first.** `tests/Dexpace.Sdk.Core.Tests/Recovery/DelegateStepTests.cs` (`DelegateStepTests`, `Unit`): each
fake invokes the sync delegate on `Apply`, the async delegate on `ApplyAsync`, and counts calls. Red: CS0246.
**`PublicAPI.Unshipped.txt`:** the three interfaces' members. **IDs:** infrastructure. **Verify:** V-fast `DelegateStepTests`.

### Task 3.2 — `RequestRecoveryChain` (`RECOV-3`, `RECOV-14`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Recovery/RequestRecoveryChainTests.cs`, class
`RequestRecoveryChainTests`, `Unit`, every test a `[Theory]` over `bool async` (header cites `request-chain.test.ts`):

- `An_empty_chain_returns_the_input_by_reference`
- `Steps_fold_left_to_right` (one log: `a b c`)
- `A_throwing_step_aborts_the_rest_and_propagates` (`Assert.Same` on the exception; later steps not called)
- `The_chain_copies_its_steps_at_construction` (mutate the caller's list after construction: the chain is unchanged;
  `Steps` is a read-only view of the copy)
- `A_null_element_is_rejected_at_construction` (`ArgumentException`, `ParamName` `steps`)
- `A_null_steps_argument_is_rejected`
- `One_chain_serves_concurrent_applies` (`Parallel.ForEachAsync` over one chain with per-call recording steps; every call
  sees its own request)
- `A_cancelled_token_reaches_each_step` (the token the step saw is the one passed)

Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Recovery/RequestRecoveryChain.cs`: per the design's shape (`Empty`, ctor, `Steps`,
`Apply`, `ApplyAsync`), with one private `ApplyCoreAsync(Request, CancellationToken, bool async)` (R1) and the sync entry
point reading it through `SyncPath.GetResult`. `Empty` returns the input by reference without iterating. The array copy is
`[.. steps]`. **IDs:** `RECOV-3`, `RECOV-14` (request half). **Verify:** V-fast `RequestRecoveryChainTests`.

### Task 3.3 — `StepFailure` (`RECOV-12`, `RECOV-13`; P4b-12, R3)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Recovery/StepFailureTests.cs`, class `StepFailureTests`, `Unit`,
`[Theory]` over `bool async`:

- `A_success_in_hand_is_released_exactly_once_and_the_thrown_exception_is_the_failure` (`DisposalCountingBody` count is 1;
  the returned `Outcome` is a `Failure` whose `Error` is `Assert.Same` the thrown exception)
- `A_failing_release_lands_on_the_thrown_exceptions_trail_and_never_replaces_it` (a body whose dispose throws: the returned
  failure's error is the thrown one; `ExceptionTrail.GetSuppressed(thrown)` holds the dispose failure)
- `A_failure_in_hand_releases_nothing` (no response exists to release)
- `A_second_conversion_over_the_same_response_does_not_release_twice` (3b's latch: the count stays 1; `RECOV-12`'s
  "exactly once")
- `A_fatal_dispose_exception_propagates` (`OutOfMemoryException` from the dispose: the helper throws it)

Red: CS0246 (`StepFailure` is `internal`; the test project sees it through `InternalsVisibleTo`).

**Production.** New `src/Dexpace.Sdk.Core/Recovery/StepFailure.cs`: `internal static class StepFailure` with
`ConvertAsync(Exception thrown, Outcome current, bool async)` and the sync `Convert(Exception, Outcome)`. When `current`
is a `Success`, release its response through `Disposal.DisposeQuietly`/`DisposeQuietlyAsync` with `thrown` as `primary`
(the trail attach of task 1.4); return `new Outcome.Failure(thrown)`. Documented as the **one** place the release happens
(design §5.2). **IDs:** `RECOV-12`, `RECOV-13` (by construction: nothing here runs on a returned outcome). **Verify:** V-fast
`StepFailureTests`.

### Task 3.4 — `ResponseRecoveryChain` (`RECOV-4`–`RECOV-9`, `RECOV-12`–`RECOV-14`; P4b-8, P4b-9, P4b-22)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Recovery/ResponseRecoveryChainTests.cs`, class
`ResponseRecoveryChainTests`, `Unit`, `[Theory]` over `bool async` (headers cite `response-chain.test.ts` and the Ruby R5–R7
case lists):

- `The_response_phase_runs_only_while_the_outcome_is_a_success` (`RECOV-4`: a `Failure` input skips every response step)
- `Recovery_steps_run_on_every_outcome_in_order` (`RECOV-5`: success and failure inputs)
- `Recovery_steps_observe_a_failure_a_response_step_just_produced` (`RECOV-5`: a throwing response step followed by a
  recording recovery step)
- `The_response_phase_runs_first_then_the_recovery_phase_in_declared_order` (`RECOV-6`: one log `r1 r2 c1 c2`)
- `A_throwing_response_step_becomes_a_failure_the_rest_are_skipped_and_nothing_propagates` (`RECOV-7`)
- `A_throwing_recovery_step_becomes_a_failure_fed_to_the_next_recovery_step` (`RECOV-8`: asserted on the returned `Outcome`
  and on the next step having run, never `Record.Exception` alone)
- `A_step_returning_null_is_treated_as_an_InvalidOperationException_naming_the_step_type` (P4b-8: the next recovery step
  sees a `Failure` with that exception; message names the step's type)
- `The_chain_never_checks_the_token_itself` (a pre-cancelled token and non-throwing steps: apply returns normally)
- `A_returned_failure_reaches_the_next_recovery_step` (`RECOV-9`: `A_returned_failure_reaches_the_next_recovery_step`, the
  exact name the design lists)
- `A_throwing_step_with_a_success_in_hand_releases_it_exactly_once` (`RECOV-12`: `DisposalCountingBody` count 1 across the
  error-mapping-shaped path where a response step disposes itself first and then throws: the chain's dispose is latched)
- `A_failing_release_lands_on_the_primarys_trail_and_the_primary_is_surfaced` (`RECOV-12`)
- `A_failure_in_hand_releases_nothing` (`RECOV-12`)
- `A_recovery_step_returning_a_substitute_success_leaves_the_original_undisposed` (`RECOV-13`: the original's dispose count
  is **0**)
- `Both_lists_are_copied_at_construction` (`RECOV-14`: mutate the caller's response list and recovery list after
  construction; plus the null-element rejection for each)
- `One_chain_serves_concurrent_applies` (`RECOV-14`)
- `A_fatal_exception_from_a_step_propagates` (`OutOfMemoryException`; the only exception apply throws, P4b-9)

Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Recovery/ResponseRecoveryChain.cs`: per the design's shape. One private
`ApplyCoreAsync(Outcome, CancellationToken, bool async)` composed of two private methods, `RunResponsePhaseAsync` and
`RunRecoveryPhaseAsync`, each under the 70-line cap; each step call is wrapped in `try { … } catch (Exception ex) when
(!ExceptionFacts.IsFatal(ex))` that calls `StepFailure.ConvertAsync`. A `null` return from a step is converted as a thrown
`InvalidOperationException` naming `step.GetType()`. No `ThrowIfCancellationRequested` anywhere (position C).
**`PublicAPI.Unshipped.txt`:** both chains and `Empty`/ctor/properties/`Apply`/`ApplyAsync` lines. **IDs:** `RECOV-4`–`RECOV-9`,
`RECOV-12`–`RECOV-14`. **Verify:** V-fast `ResponseRecoveryChainTests`, `StepFailureTests`.

### Task 3.5 — `RecoveryLayerArchitectureTests` (P4b-5, §8.3's prohibition)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Architecture/RecoveryLayerArchitectureTests.cs`, `Unit`:
`No_recovery_type_references_a_pipeline_type` (every type in namespace `Dexpace.Sdk.Core.Recovery`: its fields, properties,
method parameter and return types and base types name no type whose namespace starts with `Dexpace.Sdk.Core.Pipeline`; use
the existing `TypeReferences` helper in the folder). The reverse edge (a policy calling a step) is allowed and needs no test.
The test is written after tasks 3.1-3.4 (it passes vacuously before the types exist) and is proven able to fail by
temporarily adding a `PipelineContext` field to `ResponseRecoveryChain` (never committed). **IDs:** the §8.3 two-layer prohibition cited by `RECOV-1`–`RECOV-16`. **Verify:** V-fast `RecoveryLayerArchitectureTests`.

### Task 3.6 — Close-out (PR 3)

`CHANGELOG.md` `### Added`: "The recovery step contracts and chains (`RECOV-3`–`RECOV-9`, `RECOV-12`–`RECOV-14`)." Run
**V-gate** and the coverage gate. **Commit:** `feat: request and response recovery chains (RECOV-3..RECOV-9, RECOV-12..RECOV-14)`.

---

## PR 4 — `RecoveryDispatcher`

**Gate: PR 3.** Rows: `RECOV-2`, `RECOV-10`, `RECOV-11`. Rulings: `P4b-8`, `P4b-10`, `P4b-11`, `P4b-25`.

### Task 4.1 — `RecoveryDispatcher` (`RECOV-2`, `RECOV-10`, `RECOV-11`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Recovery/RecoveryDispatcherTests.cs`, class
`RecoveryDispatcherTests`, `Unit`, `[Theory]` over `bool async` (the sync arm uses `RecordingSyncTransport`, the async arm
`RecordingTransport`; headers cite `orchestrator.test.ts`):

- `A_throwing_request_step_reaches_a_recording_recovery_step_as_a_failure` (`RECOV-2`: the request side, which a transport-only
  suite would miss)
- `A_throwing_transport_reaches_a_recording_recovery_step_as_a_failure` (`RECOV-2`: a sync throw, and for the async arm a
  faulted task, both the same instance)
- `A_null_response_from_a_transport_is_a_failure_with_InvalidOperationException` (P4b-8)
- `A_success_returns_the_response_by_reference` (`RECOV-10`)
- `A_failure_from_a_recovery_step_is_rethrown_as_the_same_constructed_never_thrown_instance` (`RECOV-10`: `Assert.Same` on an
  exception built with `new` and returned in `new Outcome.Failure(…)`)
- `A_failure_from_the_transport_is_rethrown_as_the_same_thrown_instance` (`RECOV-10`, fact 7)
- `A_recovery_step_may_substitute_a_success_for_a_failure` (the dispatcher returns the substitute)
- `Cancellation_survives_the_failure_conversion` (`RECOV-11`: cancel a source, the transport throws
  `OperationCanceledException`; the token is still cancelled after the fold and after the rethrow; the surfaced exception is
  the same instance; asserted on the **token**, P4b-11)
- `The_dispatcher_does_not_dispose_the_transport` (P4b-25: `IsDisposed` stays false)
- `The_request_options_and_token_reach_the_transport` (`RecordedCall.Options`, the token)
- `A_fatal_exception_propagates_without_conversion` (`OutOfMemoryException` from the transport is not converted)
- `One_dispatcher_serves_both_transport_forms` (P4b-25)

Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Recovery/RecoveryDispatcher.cs`: per the design's shape. One private
`DispatchCoreAsync(…, bool async)` is **not** generic over the transport form: the two public entry points each take their
own transport type, and a shared private method takes `Func<Request, ValueTask<Response>>`-free logic via an internal
two-transport holder (`TransportCall` struct with `IHttpClient?` and `IAsyncHttpClient?`); the sync entry point passes
`async: false` and reads through `SyncPath`. The four conversion sites (request-chain call, transport call, and — through
`ResponseRecoveryChain` — the two step sites) use `when (!ExceptionFacts.IsFatal(ex))`. After the fold, `Success` returns the
response; `Failure` calls `ExceptionDispatchInfo.Capture(error).Throw()` (P4b-10). Constructor `ThrowIfNull`s both chains.
**`PublicAPI.Unshipped.txt`:** the dispatcher's members. **IDs:** `RECOV-2`, `RECOV-10`, `RECOV-11`. **Verify:** V-fast
`RecoveryDispatcherTests`.

### Task 4.2 — Close-out (PR 4)

`CHANGELOG.md` `### Added`: "`RecoveryDispatcher` (`RECOV-2`, `RECOV-10`, `RECOV-11`)." Run **V-gate**. **Commit:** `feat:
recovery dispatcher (RECOV-2, RECOV-10, RECOV-11)`.

---

## PR 5 — `ErrorBodyBuffer`, `ErrorMappingStep`, the `EnsureSuccessAsync` re-home

**Gate: PR 3.** Independent of PRs 4 and 6. Breaking item 5. Rows: `RECOV-15`, `RECOV-16`. Rulings: `P4b-16` (**open for the
lead**), `P4b-24`. **4c's `ErrorMappingPolicy` waits for this PR.** **If the lead keeps P3a-13** (design position F
fallback): task 5.1 is skipped, `ErrorMappingStep.ApplyAsync` is written over `Response.EnsureSuccessAsync`, `Apply` throws
`NotSupportedException`, `RECOV-16` becomes ✅ (S8) ⏳ 4c, and 4c's spec owns the sync capture; the plan's other tasks do not
change.

### Task 5.1 — `ErrorBodyBuffer` (`RECOV-16`; P4b-16, R2)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Response/ErrorBodyBufferTests.cs`, class
`ErrorBodyBufferTests`, `Unit`, `[Theory]` over `bool async`:

- `A_body_below_the_cap_survives_whole`
- `A_body_of_cap_plus_one_truncates_to_the_constant` (asserted against `Response.MaxBufferedErrorBytes`, never a literal;
  markerless)
- `The_buffered_copy_is_replayable` (two reads of the returned body agree)
- `The_original_is_disposed_after_a_successful_drain` (`DisposalCountingBody`-style probe: count 1)
- `The_original_is_disposed_after_a_failing_drain_and_the_drains_exception_is_primary` (a body whose stream throws mid-read:
  the surfaced exception is `Assert.Same` the drain's; the original's dispose count is 1)
- `A_dispose_failure_after_a_failed_drain_lands_on_the_drains_trail_and_does_not_replace_it` (Breaking 5, pinned by this test)
- `A_dispose_failure_after_a_successful_drain_propagates` (R2: the unchanged behaviour)
- `The_result_keeps_status_headers_and_request` (`response.WithBody` semantics)
- `A_cancelled_token_stops_the_drain`
- `The_content_type_survives_the_copy`

Red: CS0103.

**Production.** New `src/Dexpace.Sdk.Core/Http/Response/ErrorBodyBuffer.cs`: `internal static class ErrorBodyBuffer` with
`Capture(Response, CancellationToken)` and `CaptureAsync(Response, CancellationToken)`; each moves the body of today's
`Response.EnsureSuccessAsync` drain (open the stream, `StreamCopy.DrainUpTo`/`DrainUpToAsync` into an `ArrayBufferWriter<byte>`
up to `Response.MaxBufferedErrorBytes`, `ToArray`), disposing the stream with `await using … ConfigureAwait(false)`; on a
drain failure: `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex)) { Disposal.DisposeQuietly(response, ex); throw; }`
(async form uses `DisposeQuietlyAsync`); on success: plain `Dispose`/`DisposeAsync` of the original (R2), then return
`response.WithBody(ResponseBody.FromReplayableBytes(bytes, response.Body.ContentType))` — the content type is read **before**
the dispose. Each method well under 70 lines. **`PublicAPI.Unshipped.txt`:** unchanged (`internal`). **IDs:** `RECOV-16`.
**Verify:** V-fast `ErrorBodyBufferTests`.

### Task 5.2 — Re-home `Response.EnsureSuccessAsync` (`RECOV-16`; Breaking 5)

**Failing tests first.** None new beyond task 5.1; the evidence is that `EnsureSuccessErrorMappingTests` (`Security`, all
six methods, **unedited**), `EnsureSuccessTests` and `ErrorBodyPreviewTests` pass before and after. Run them first to record
the green baseline.

**Production.** `src/Dexpace.Sdk.Core/Http/Response/Response.cs` `EnsureSuccessAsync` becomes: `if (!error) return; throw new
HttpResponseException(await ErrorBodyBuffer.CaptureAsync(this, cancellationToken).ConfigureAwait(false));` — the status
check keeps its two `Status.IsClientError`/`IsServerError` clauses; the XML docs keep their text and gain a
`<b>Breaking:</b>` remark (a dispose failure after a failed drain is attached to the drain's exception instead of replacing
it). **IDs:** `RECOV-16`. **Verify:** V-fast `EnsureSuccessErrorMappingTests`, `EnsureSuccessTests`, `ErrorBodyPreviewTests`.

### Task 5.3 — `ErrorMappingStep` (`RECOV-15`; P4b-16)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Recovery/ErrorMappingStepTests.cs`, class `ErrorMappingStepTests`,
`Unit`, `[Theory]` over `bool async` (header cites `status-mapping.test.ts`):

- `A_non_error_status_returns_the_same_instance_with_an_unopened_body` (`[Theory]` over 100, 199, 200, 204, 299, 304, 399,
  600, 999; a body that records any `OpenRead`/`Dispose`; none is seen)
- `A_4xx_or_5xx_status_becomes_HttpResponseException_with_the_status` (`[Theory]` over 400, 404, 429, 499, 500, 503, 599; the
  exception's `Response.Status.Code` equals the input; the response's body is the replayable buffered copy)
- `The_step_throws_HttpResponseException_it_does_not_return_it`
- `The_step_is_a_singleton_and_stateless` (`ErrorMappingStep.Instance` identity; concurrent applies)
- `The_step_runs_inside_a_response_chain_and_the_failure_reaches_recovery_steps` (`ResponseRecoveryChain` over the step and a
  recording recovery step: it sees `Outcome.Failure(HttpResponseException)`, and the original response is not released
  twice — the buffer disposed it, `StepFailure`'s dispose is latched)

Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Recovery/ErrorMappingStep.cs`: `public sealed class ErrorMappingStep : IResponseStep`
with `public static ErrorMappingStep Instance { get; }`; `Apply`/`ApplyAsync` return the input for statuses outside 400..599
(**reference, body untouched**) and otherwise `throw new HttpResponseException(ErrorBodyBuffer.Capture[Async](…))`. A private
constructor keeps `Instance` the only form (the design's shape). **`PublicAPI.Unshipped.txt`:** the step's members. **IDs:**
`RECOV-15`. **Verify:** V-fast `ErrorMappingStepTests`, `EnsureSuccessErrorMappingTests`.

### Task 5.4 — Close-out (PR 5)

`CHANGELOG.md`: `### Added` "`ErrorMappingStep` (`RECOV-15`)."; `### Changed` "**Breaking:** `Response.EnsureSuccessAsync`: a
dispose failure after a failed drain is attached to the drain's exception instead of replacing it (`RECOV-16`)." Run **V-gate**
(the `Security` diff check is the evidence S8 is unedited) and the coverage gate. **Commit:** `feat!: error-body buffer and
error-mapping step (RECOV-15, RECOV-16)`.

---

## PR 6 — The two shipped request steps and their policies

**Gate: PR 3.** Independent of PRs 4 and 5. Breaking items 1–3. Rows: `RECOV-32`, `RECOV-33`. Rulings: `P4b-17`, `P4b-18`,
`P4b-19`, `P4b-20`. **4c's policy-signature rework of these two policies waits for this PR.** 4b touches neither
`HttpPipelinePolicy`, `PipelineRunner`, `PipelineContext`, `PipelineStage`, `HttpPipeline` nor `DexpacePipeline`
(task 6.5 runs the explicit six-file pipeline-shape check, which expects only the two `Policies/` files changed).

### Task 6.1 — `IdempotencyKeyStep` (`RECOV-32`; P4b-17, R4) — Breaking 1

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Recovery/IdempotencyKeyStepTests.cs`, class
`IdempotencyKeyStepTests`, `Unit`, `[Theory]` over `bool async` (header cites `idempotency-key.test.ts`):

- `The_default_methods_are_POST_PUT_and_PATCH` (`[Theory]` over all nine well-known methods: stamped for the three, passed
  through **by reference** for the rest)
- `The_default_header_is_Idempotency_Key_and_the_default_key_is_a_GUID`
- `RespectExisting_true_leaves_a_present_header_untouched_and_does_not_invoke_the_strategy` (`RECOV-32`: strategy counter 0)
- `RespectExisting_false_overwrites_a_present_header`
- `The_strategy_runs_at_most_once_per_applicable_request` (counter 1)
- `A_strategy_returning_null_empty_or_whitespace_throws_InvalidOperationException_naming_the_step` (`[Theory]`)
- `A_strategy_value_failing_header_validation_throws_the_ArgumentException_Headers_raises` (a value with CR/LF)
- `Methods_are_copied_on_init` (mutate the source set after assignment; behaviour unchanged)
- `A_custom_header_name_and_method_set_are_honoured`
- `The_internal_key_source_overload_uses_the_given_source_and_not_the_strategy` (R4's seam)

Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Recovery/IdempotencyKeyStep.cs` per the design's shape: `init` properties
`HeaderName` (default `HttpHeaderName.WellKnown.IdempotencyKey`), `Methods` (default `{POST, PUT, PATCH}`, init copies into
a `FrozenSet<Method>`), `RespectExisting` (default `true`), `KeyStrategy` (default `() => Guid.NewGuid().ToString("D")`;
docs: must be thread-safe). `internal Request Apply(Request, Func<string> keySource)` carries the logic (R4); the public
`Apply`/`ApplyAsync` call it with `KeyStrategy`. **`PublicAPI.Unshipped.txt`:** the step's members. **IDs:** `RECOV-32`.
**Verify:** V-fast `IdempotencyKeyStepTests`.

### Task 6.2 — `IdempotencyPolicy` delegates (`RECOV-32`; P4b-17, P4b-20) — Breaking 1, 2

**Failing tests first.** Edit `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/IdempotencyPolicyTests.cs` (**not** a `Security`
class): existing assertions about POST stay; add `ProcessAsync_Put_SetsIdempotencyKeyHeader` and
`ProcessAsync_Patch_SetsIdempotencyKeyHeader` (Breaking 1), `ProcessAsync_Delete_DoesNotSetIdempotencyKeyHeader`, and
`A_redirect_hop_or_retry_re_entering_the_policy_reuses_the_call_scoped_key_and_the_strategy_runs_once` (a strategy counter
of 1 across two `ProcessAsync` passes over one `PipelineContext`: the bag-first key source of design position G), and
`The_constructor_accepting_a_step_uses_that_steps_configuration`. Any existing test that constructs
`new IdempotencyPolicy(new[] { Method.Post })` is rewritten to `new IdempotencyPolicy(new IdempotencyKeyStep { Methods =
… })` with its **assertions unchanged**. Run `grep -rn "IdempotencyPolicy(" src tests` first and list every site in the
task's notes. Red: CS1503/CS7036 on the changed constructor, plus the PUT/PATCH assertions.

**Production.** `src/Dexpace.Sdk.Core/Pipeline/Policies/IdempotencyPolicy.cs`: constructors become `IdempotencyPolicy()`
(default step) and `IdempotencyPolicy(IdempotencyKeyStep step)` (null-checked); `ProcessAsync` calls
`_step.Apply(context.Request, keySource)` where `keySource` reads `context.GetProperty<string>(PropertyKey)` first and
otherwise mints through `_step.KeyStrategy()` and stores it with `context.SetProperty(PropertyKey, key)`. Signature and stage
(`ProcessAsync(PipelineContext, PipelineRunner)`, `PerCall`) unchanged; `PropertyKey` kept. The class remarks and the old
constructor's removal carry `<b>Breaking:</b>`. `DexpacePipeline.CreateDefault` is **not edited** (it constructs the policy
parameterlessly). **`PublicAPI.Unshipped.txt`:** replace line 381's `IdempotencyPolicy(IEnumerable<Method!>? methods = null)`
with the two new constructors. **IDs:** `RECOV-32`. **Verify:** V-fast `IdempotencyPolicyTests`, `DexpacePipelineTests`.

### Task 6.3 — `ClientIdentityStep`, `ClientIdentityMode` (`RECOV-33`; P4b-18, R5) — Breaking 3

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Recovery/ClientIdentityStepTests.cs`, class
`ClientIdentityStepTests`, `Unit`, `[Theory]` over `bool async` (header cites `client-identity-step.test.ts`):

- `Tokens_are_joined_by_one_space_and_trimmed`
- `Append_composes_the_line_after_the_first_existing_value_and_keeps_every_other_value` (`RECOV-33`: two existing values)
- `Append_with_no_existing_value_sets_the_line_as_the_sole_value`
- `An_empty_first_existing_value_counts_as_absent_with_no_leading_space`
- `Replace_overwrites_every_existing_value`
- `A_blank_or_whitespace_only_line_is_a_no_op_and_emits_no_header` (`[Theory]`: empty tokens, `""`, `"   "`; the request is
  returned by reference)
- `The_default_header_is_User_Agent_and_the_default_mode_is_Append`
- `Tokens_are_copied_at_construction`
- `A_custom_header_name_is_honoured`
- `The_internal_compose_seam_takes_a_token_line` (R5)

Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Recovery/ClientIdentityStep.cs` and `ClientIdentityMode.cs`
(`Append = 0, Replace = 1`) per the design's shape; `internal Request Compose(Request, string tokenLine)` holds the logic.
**`PublicAPI.Unshipped.txt`:** the step and the enum. **IDs:** `RECOV-33`. **Verify:** V-fast `ClientIdentityStepTests`.

### Task 6.4 — `ClientIdentityPolicy` delegates (`RECOV-33`; P4b-18) — Breaking 3

**Failing tests first.** Edit `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/ClientIdentityPolicyTests.cs`: existing
sets-the-header assertions stay; add `A_caller_supplied_User_Agent_is_followed_by_the_sdk_line_by_default` (Breaking 3: the
caller's value first, then a space and the SDK line), `Replace_mode_overwrites_the_callers_value` (`new
ClientIdentityPolicy(ClientIdentityMode.Replace)`; the old behaviour), `A_blank_UserAgent_option_emits_no_header`,
`The_option_is_read_per_call` (change `DexpaceClientOptions.UserAgent` between two sends). Any existing test asserting the
replace-by-default behaviour is rewritten to the Append expectation or to `Replace`, and each such edit is listed in the
checklist's "existing assertions changed" table (task 7.3). Red: the Append assertions fail on the as-built replace.

**Production.** `src/Dexpace.Sdk.Core/Pipeline/Policies/ClientIdentityPolicy.cs`: constructors `()` and
`(ClientIdentityMode mode)`; `ProcessAsync` calls an internal `ClientIdentityStep` built with `Mode` and empty tokens via
`step.Compose(context.Request, context.Options.UserAgent)`; signature and stage untouched; the remarks carry
`<b>Breaking:</b>` and say how to get the old behaviour. **`PublicAPI.Unshipped.txt`:** add the
`ClientIdentityPolicy(ClientIdentityMode)` line. **IDs:** `RECOV-33`. **Verify:** V-fast `ClientIdentityPolicyTests`,
`DexpacePipelineTests`.

### Task 6.5 — Close-out (PR 6)

`CHANGELOG.md` `### Changed`: "**Breaking:** `IdempotencyPolicy` stamps PUT and PATCH as well as POST by default, and its
constructor is `IdempotencyPolicy()` / `IdempotencyPolicy(IdempotencyKeyStep)` (`RECOV-32`)." and "**Breaking:**
`ClientIdentityPolicy` appends the SDK line after a caller-supplied `User-Agent` by default (`new
ClientIdentityPolicy(ClientIdentityMode.Replace)` restores the old behaviour); a blank `UserAgent` emits no header
(`RECOV-33`)."; `### Added` "`IdempotencyKeyStep`, `ClientIdentityStep`, `ClientIdentityMode`." Run **V-gate**, the
pipeline-shape check
`git diff --stat main...HEAD -- src/Dexpace.Sdk.Core/Pipeline/HttpPipelinePolicy.cs src/Dexpace.Sdk.Core/Pipeline/PipelineRunner.cs src/Dexpace.Sdk.Core/Pipeline/PipelineContext.cs src/Dexpace.Sdk.Core/Pipeline/PipelineStage.cs src/Dexpace.Sdk.Core/Pipeline/HttpPipeline.cs src/Dexpace.Sdk.Core/Pipeline/DexpacePipeline.cs`
(expect empty, P4b-20) and the coverage gate. **Commit:** `feat!: idempotency and client-identity steps and policy defaults
(RECOV-32, RECOV-33)`.

---

## PR 7 — Close-out

**Gate: PRs 1–6.** Rows: all 34 (closing). The docs close the sub-phase (roadmap step 7).

### Task 7.1 — NativeAOT smoke

Extend `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` with `CheckPhase4bRecoveryAsync` (called beside `CheckPhase3bBodiesAsync`
in the runner, line ~45): an `Outcome` fold, a `RecoveryDispatcher.DispatchAsync` over a fake transport through an
`ErrorMappingStep`, and `ExceptionTrail.AddSuppressed`/`GetSuppressed` on a foreign exception through `Data`. Smoke
categories are `AotSmoke`. Publish and run it (V-gate). **Verify:** `dotnet publish tests/Dexpace.Sdk.AotSmoke …` and the
binary exits 0. **IDs:** cited by `RECOV-1`, `RECOV-10`, `RECOV-15`.

### Task 7.2 — User documentation

New `docs/sdk-documentation/recovery.md` ("As built by phase 4b, written against source on <date>"): the outcome type and its
folds; the three step contracts and both forms; the dispatcher and its transport-per-call shape; the error-mapping step and
the 1 MiB cap; the two shipped request steps and the policy defaults; `ExceptionFacts` and `ExceptionTrail` (read a foreign
exception's trail with `ExceptionTrail.GetSuppressed`); the breaking-change migration table (the five items of the design's
"Breaking changes", each with before/after, including `new ClientIdentityPolicy(ClientIdentityMode.Replace)`). Cite
requirement IDs; do not copy design §5.2. `docs/README.md`'s ownership table gains the row the probe asks for. **Verify:** the
probe's `links` check.

### Task 7.3 — The checklist

Write `docs/work/mvp/phase4/phase4b/<date>-phase4b-recovery-checklist.md` (create the directory) from what was built, one row
per requirement ID, phase-1 legend, mirroring the design's decision table: **34 rows** — 17 ✅ (`RECOV-1`–`RECOV-15`,
`RECOV-32`, `RECOV-33`), `RECOV-16` ✅ with its re-sent-response clause ⏳ 6a, `RECOV-26` ✅ (S7 clause) ⏳ 6a, and the other
fifteen ⏳ 6a (`RECOV-17`–`RECOV-25`, `RECOV-27`–`RECOV-31`, `RECOV-34`), each naming the 6a card bullet "the
recovery-stack engine (the 15 `RECOV` IDs)" (or the 6a plan's task once it exists) and `RECOV-31` naming its `RETRY-38`
twin. Include the "existing assertions changed" table (expected: the `DisposalTests` primary test; the
`IdempotencyPolicyTests`/`ClientIdentityPolicyTests` edits of tasks 6.2 and 6.4), each `Security` class listed as "unedited"
with the empty diff check as evidence, and the deviation ledger P4b-3, P4b-6, P4b-8, P4b-14, P4b-16 as built. The census
sentence: 4a 20 + 4b 34 + 4c 40 = 94.

### Task 7.4 — Dated corrections, hand-offs, routed entries

Frozen documents change only by dated correction. In `docs/sdk-design-dotnet/`:

- **§5.2** (`05-…`): `Outcome` is an abstract class with nested sealed classes (P4b-3, fact 1); the Try-pattern accessors
  (P4b-4); both forms over one core (P4b-6); `EnumerateCauses` yields the root first (P4b-13); the trail's snapshot,
  swallow-on-read-only-`Data`, fatal and rendering rules (P4b-14); the **As built** line.
- **§5.1**: "Three shipped steps": the logic lives in the recovery steps and the policies delegate (P4b-17, P4b-18); "The
  bounded error-body copy": `ErrorBodyBuffer` is built by 4b (P4b-16, if accepted); the public sync `Response.EnsureSuccess` stays 4c's over `ErrorBodyBuffer.Capture`.
- **§3.7**: `Disposal`'s repoint is done (P4b-15); the **As built (phase 4b)** line.
- **§10 entry 12** gains the topic label `fatal-exception-filter`; **§10 entry 13** gains `suppressed-trail` and the
  read-only-`Data` residual. **No new §10 entry** (P4b-21). (4c's `async-redirect-pillar` entry is 4c's, not written here.)
- **§11**: one new item at the next free number (re-read the file immediately before writing; 4a and 4c may have added
  items): the "recovery layer is synchronous" reading (P4b-6).
- **§12**: the `RECOV` row's deferred list moves `RECOV-31` to "⏳ 6a with `RETRY-38`" (P4b-2, if accepted).

Roadmap: the Phase List row 4's `sdk-design refs` cell gains this design's link (appended, never replacing); phase 1's S8
row and the 3b checklist's `HTTP-52`/`BODY-30` cells name 4b for `ErrorBodyBuffer`, and the 3a status note's Hand-offs line ("4c: builds `ErrorBodyBuffer` and `ErrorMappingPolicy` over `DrainUpTo`, and a sync `EnsureSuccess`") and P3a-13 get a dated correction (4b builds the buffer; 4c keeps the policy and the public sync `EnsureSuccess`) (P4b-16, if accepted); a dated Phase
Status Note for 4b (what landed, the seven PRs, the rulings and which the lead accepted, the hand-offs: 4c's
`ErrorMappingPolicy` over `ErrorMappingStep`/`ErrorBodyBuffer`, 4c porting the two policies and reusing `SyncPath`, the
`InstrumentationPolicy` fatal-filter finding for 4c or 5c (P4b-23), 5b's `Disposal` logger plumbing, 6a's engine as an
`IRecoveryStep` and its open per-call-step question, 7b's reuse of `Outcome`). `CLAUDE.md`: the layout tree gains `Recovery/`
and "What is genuinely unbuilt" drops the recovery chain; update `src/Dexpace.Sdk.Core/README.md` (a recovery paragraph) and
`docs/architecture.md` if it lists core namespaces. **Verify:** the probe's `links` and `citations` checks.

### Task 7.5 — The corpus note

File a `## Superseded` note in `docs/knowledge/notes/retry-and-resilience.md` (create the file if absent, following the
neighbouring notes' structure) that **Supersedes** `retry-and-resilience/302d143d` (the false "record hierarchy closed by a
private constructor"), citing fact 1 as confirmed against the implementation's own build (task 2.1). Also file a `notes/data-modeling.md` entry (Conflicts or Superseded) against the harvested `data-modeling` rule (styleguide 6.3, closed choice as abstract record), and add a dated SDK-overlay row to `docs/styleguide/README.md` recording the 6.3 departure with fact 1 as the reason (P4b-3, open for the lead). Never edit
`docs/knowledge/harvested/`. **Verify:** `scripts/knowledge verify-structure`.

### Task 7.6 — Close-out, file the phase documents, run the probe

`CHANGELOG.md` `### Added`: "`docs/sdk-documentation/recovery.md`; the AOT smoke covers the recovery layer." Run **V-gate**
and the coverage gate with its self-test. Then:

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 4b            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 4b --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix whatever the final probe reports (counts in `CLAUDE.md`/`README.md`, package READMEs, broken links, misattributed IDs).
`apply --write` performs `git mv` only. **Commits:** `test: NativeAOT smoke over the recovery layer`, then `docs: phase 4b
checklist, user page and dated corrections`. **No push.**

---

## Checklist rows (one per requirement ID owned by 4b: 34)

Disposition column uses the phase-1 legend; task numbers are in this plan.

| ID | Level | Disposition | Task(s) |
|---|---|---|---|
| `RECOV-1` | MUST | ✅ | 2.1 |
| `RECOV-2` | MUST | ✅ | 1.2, 4.1 |
| `RECOV-3` | MUST | ✅ | 3.1, 3.2 |
| `RECOV-4` | MUST | ✅ | 3.1, 3.4 |
| `RECOV-5` | MUST | ✅ | 3.4 |
| `RECOV-6` | MUST | ✅ | 3.4 |
| `RECOV-7` | MUST | ✅ | 3.4 |
| `RECOV-8` | MUST | ✅ | 1.2, 3.4 |
| `RECOV-9` | SHOULD | ✅ | 3.1, 3.4 |
| `RECOV-10` | MUST | ✅ | 4.1 |
| `RECOV-11` | MUST | ✅ | 4.1 |
| `RECOV-12` | MUST | ✅ | 1.3, 1.4, 3.3, 3.4 |
| `RECOV-13` | MUST | ✅ | 3.1, 3.3, 3.4 |
| `RECOV-14` | MUST | ✅ | 3.2, 3.4 |
| `RECOV-15` | MUST | ✅ | 5.3 |
| `RECOV-16` | MUST | ✅ (re-sent-response clause ⏳ 6a, `RETRY-36`) | 5.1, 5.2 |
| `RECOV-17` | MUST | ⏳ 6a | 7.3 |
| `RECOV-18` | MUST | ⏳ 6a | 7.3 |
| `RECOV-19` | MUST | ⏳ 6a (calls 4b's `ErrorBodyBuffer`/`ErrorMappingStep`) | 7.3 |
| `RECOV-20` | MUST | ⏳ 6a | 7.3 |
| `RECOV-21` | MUST | ⏳ 6a | 7.3 |
| `RECOV-22` | MUST | ⏳ 6a | 7.3 |
| `RECOV-23` | MUST | ⏳ 6a | 7.3 |
| `RECOV-24` | MUST | ⏳ 6a | 7.3 |
| `RECOV-25` | SHOULD | ⏳ 6a | 7.3 |
| `RECOV-26` | MUST | ✅ (S7 clause, `RetryPacingOverflowTests` cited, unedited) ⏳ 6a (engine saturation) | 7.3 |
| `RECOV-27` | MUST | ⏳ 6a | 7.3 |
| `RECOV-28` | MUST | ⏳ 6a | 7.3 |
| `RECOV-29` | MUST | ⏳ 6a | 7.3 |
| `RECOV-30` | SHOULD | ⏳ 6a | 7.3 |
| `RECOV-31` | MAY | ⏳ 6a with `RETRY-38` (P4b-2, open for the lead) | 7.3, 7.4 (§12) |
| `RECOV-32` | MUST | ✅ | 6.1, 6.2 |
| `RECOV-33` | MUST | ✅ | 6.3, 6.4 |
| `RECOV-34` | MUST | ⏳ 6a | 7.3 |

Count: 34 rows; 18 built (17 ✅ plus `RECOV-16` ✅ with a ⏳ clause), 16 ⏳ 6a (15 plus `RECOV-31`; `RECOV-26` is counted among
the 15 and is ✅ for its S7 clause).

---

## Traceability: ID → PR → task

| ID | PR | Task(s) | ID | PR | Task(s) |
|---|---|---|---|---|---|
| `RECOV-1` | 2 | 2.1 | `RECOV-18` | 7 | 7.3 |
| `RECOV-2` | 1, 4 | 1.2, 4.1 | `RECOV-19` | 7 | 7.3 |
| `RECOV-3` | 3 | 3.1, 3.2 | `RECOV-20` | 7 | 7.3 |
| `RECOV-4` | 3 | 3.1, 3.4 | `RECOV-21` | 7 | 7.3 |
| `RECOV-5` | 3 | 3.4 | `RECOV-22` | 7 | 7.3 |
| `RECOV-6` | 3 | 3.4 | `RECOV-23` | 7 | 7.3 |
| `RECOV-7` | 3 | 3.4 | `RECOV-24` | 7 | 7.3 |
| `RECOV-8` | 1, 3 | 1.2, 3.4 | `RECOV-25` | 7 | 7.3 |
| `RECOV-9` | 3 | 3.1, 3.4 | `RECOV-26` | 7 | 7.3 |
| `RECOV-10` | 4 | 4.1 | `RECOV-27` | 7 | 7.3 |
| `RECOV-11` | 4 | 4.1 | `RECOV-28` | 7 | 7.3 |
| `RECOV-12` | 1, 3 | 1.3, 1.4, 3.3, 3.4 | `RECOV-29` | 7 | 7.3 |
| `RECOV-13` | 3 | 3.1, 3.3, 3.4 | `RECOV-30` | 7 | 7.3 |
| `RECOV-14` | 3 | 3.2, 3.4 | `RECOV-31` | 7 | 7.3, 7.4 |
| `RECOV-15` | 5 | 5.3 | `RECOV-32` | 6 | 6.1, 6.2 |
| `RECOV-16` | 5 | 5.1, 5.2 | `RECOV-33` | 6 | 6.3, 6.4 |
| `RECOV-17` | 7 | 7.3 | `RECOV-34` | 7 | 7.3 |

Count: `RECOV-1`–`RECOV-34` = 34 rows, all mapped. Not 4b's rows, but cited by its work: `RETRY-25`, `RETRY-34`, `RETRY-36`,
`RETRY-38`, `XCUT-8`, `XCUT-9`, `PAGE-13`, `BODY-30`, `BODY-31`, `HTTP-52`, `PIPE-37` (4c installs `ErrorMappingStep` as
`ErrorMappingPolicy`; 4b's tests are the evidence).

---

## Tasks per PR

| PR | Gate | Tasks |
|---|---|---|
| 1 | none | 1.1–1.5 (5) |
| 2 | PR 1 | 2.1–2.3 (3) |
| 3 | PR 2 | 3.1–3.6 (6) |
| 4 | PR 3 | 4.1–4.2 (2) |
| 5 | PR 3 | 5.1–5.4 (4) |
| 6 | PR 3 | 6.1–6.5 (5) |
| 7 | PRs 1–6 | 7.1–7.6 (6) |
| **Total** | | **31 tasks** |

PRs 4, 5 and 6 are independent of one another and may land in any order after PR 3. 4a and 4b are independent and may land
in either order; 4c lands after 4b (its `ErrorMappingPolicy` after PR 5, its policy-signature rework after PR 6). Every PR
stays green in any order these gates allow.

---

## Findings while planning

Items checked against the repository at `0332cef` on 2026-10-05, each with what the plan did.

1. **F1 — `EnsureSuccessAsync` already drains through `StreamCopy.DrainUpToAsync`** (3a's task 1.5) and still has its own
   `try`/`finally` dispose; the re-home (task 5.2) therefore moves a body that exists, rather than writing a new one, and
   R2 keeps the success-path dispose behaviour as-is.
2. **F2 — `Disposal`'s `Report` sets `dexpace.dispose.primary_type`**, which the new primary branch can no longer reach
   (R6); the tag is removed with the rewritten `DisposalTests` case, and no other test in the tree asserts it (verify with
   `grep -rn "primary_type" src tests` at task 1.4).
3. **F3 — `IdempotencyPolicy`'s current constructor is in `PublicAPI.Unshipped.txt` at line 381** with a default argument;
   the replacement is a signature change, edited in place rather than added (task 6.2).
4. **F4 — `DexpaceClientOptions.UserAgent` has a public setter and a default** (`BuildDefaultUserAgent`), read per call by
   the policy; 4b does not change either (P4b-18; 5a's job).
5. **F5 — `docs/knowledge/notes/` holds no `retry-and-resilience.md` today**; task 7.5 creates it, following the existing
   notes' structure, rather than appending.
6. **F6 — `RecoveryLayerArchitectureTests` passes vacuously until the types exist**; task 3.5 orders it last in PR 3 and
   requires the temporary-break proof.
7. **F7 — The dispatcher's shared core has two transport types.** Task 4.1 fixes the shape (an internal holder over the
   two SPIs) so the single `bool async` body does not need a generic transport parameter.
8. **F8 — A census caveat for the lead (design P4b-1).** If 4a or 4c counts `OBS-25`/`OBS-26`, `REDIR-11`/`REDIR-24`/`REDIR-25`
   or any `RECOV` row as its own, the 94 is exceeded; 4b's 34 rows are exactly `RECOV-1`–`RECOV-34` and the plan adds none.
