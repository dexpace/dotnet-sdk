# Phase 4c — Pipeline Rework: Implementation Plan

**Status:** Draft, for review. Written 2026-10-05 against `main` at `0332cef` (phases 2a, 2b, 3a and 3b merged; 4a and 4b
are not in the tree). Design: [phase 4c pipeline design](2026-10-05-phase4c-pipeline-design.md), the authority for
every decision below. The plan cites its rows, positions (A–I), facts (1–12) and rulings (`P4c-1`…`P4c-23`) rather than
restating them. Scope authority: the roadmap's Phase 4 card and Phase List row 4. Format precedent: the
[3a plan](../../phase3/phase3a/2026-10-02-phase3a-io.md) and the
[3b plan](../../phase3/phase3b/2026-10-02-phase3b-bodies.md) (both read first).

**What this document is.** The roadmap's step 3 for sub-phase 4c: numbered TDD tasks in the design's landing order (six
pull-request-sized steps), each with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking**
markings, `CHANGELOG.md` entry, requirement IDs and verification commands. It is not the checklist (step 4, written from what
was built in task 6.3) and it writes no production code. The [checklist section](#checklist-one-row-per-owned-id) below is the
plan's own row table: exactly one row per ID 4c owns.

**Scope.** 40 rows: `PIPE-1`–`PIPE-40` (36 MUST, 4 SHOULD). 4a owns `CTX-1`–`CTX-20` and 4b `RECOV-1`–`RECOV-34`
(20 + 34 + 40 = 94, no overlap; design, "Scope and the 94-row census"). `RECOV-15`, `RECOV-16`, `RECOV-32`, `RECOV-33`,
`BODY-30`, `BODY-31`, `HTTP-52`, `RETRY-44`, `REDIR-11`, `REDIR-24`, `REDIR-25`, `XCUT-8`, `SEAM-2`, `SEAM-28` are other
owners' rows on which 4c does work; they appear in [the cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-4c)
and carry no exit mark in 4c's checklist.

**Order and gates.** 4a → 4b → 4c is a **dependency** for 4c (P4c-2, open for the lead). **PR 1 (the builder) touches no 4a or
4b type and may land first.** PR 2 onward waits for 4a and 4b to merge; PR 4 additionally needs 4b's `ErrorMappingStep` and
`ErrorBodyBuffer` (P4c-17). If the lead rejects P4c-17, task 4.2's fallback branch applies (stated there).

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile error
   counts and is the expected red for a new type or changed signature); make the production change; run them green; then run
   the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is proven able to
   fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await` including `await using` and `await foreach` (`CA2007`), methods at
   most 70 lines (`MA0051`; the existing waivers on `RedirectPolicy`, `InstrumentationPolicy` and the new one on `RetryPolicy`
   cite the phase that rewrites them), `///` XML docs on every public member (CS1591), no new `PackageReference` in
   `Dexpace.Sdk.Core` (constraint 2; `FrozenSet`, `ManualResetEventSlim`, `TimeProvider.CreateTimer` are in the shared framework).
   No reflection in `src/`, so `IsAotCompatible` and `IsTrimmable` hold unannotated. **Sync-over-async** is confined to two
   `#pragma warning disable RS0030` sites citing design §5.3: `SyncPath.GetCompletedResult` and the default
   `HttpPipelinePolicy.Process` (and, until 6c, `AuthorizationPolicy.GetCredential`'s default). Grep proves no third.
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); `AotSmoke` for the smoke
   checks. 4c adds **no** `Security` class. Core tests live under `tests/Dexpace.Sdk.Core.Tests/Pipeline/` (namespace
   `Dexpace.Sdk.Core.Tests.Pipeline`); doubles under `tests/Dexpace.Sdk.TestSupport/Pipeline/` (namespace
   `Dexpace.Sdk.TestSupport.Pipeline`). `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only (SEAM-2).
5. **Security tests are never deleted or loosened.** 4c edits exactly one `Security` class, `ReDriveRequestIsolationTests`
   (P4c-22, open for the lead): two policy bodies move to the new signature, one enum token (`PerCall` → `PerHop`) changes, one
   fact is added. Every other `Security` class in both test projects stays byte-identical. The close-out check is therefore
   not "empty diff" but the exact allowed diff:
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
   lists **only** `ReDriveRequestIsolationTests.cs`, and `git diff main...HEAD -- tests/Dexpace.Sdk.Core.Tests/Security`
   shows only the hunks the task 1.1 and 2.10 notes enumerate. A change that seems to force any other edit there is a signal to
   re-read design "Keeping the `Security` classes green", not to edit the file.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in `Unshipped`,
   and a removed one is a deleted line. Lines are listed per task; the build's `RS0016`/`RS0017` output is the authority (apply
   the analyzer's code fix and compare). PR 1 adds the builder and enum lines; PR 2 the signature lines; PR 3 the pipeline
   lines; PR 4 `ErrorMappingPolicy` and `EnsureSuccess`; PR 5 `CreateEmpty` and `AddStandardResilience`. Only core's file
   changes; the SystemNet and STJ files must not (the V-gate checks).
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes, stating
   what it was. The PR's last task adds the `CHANGELOG.md` `[Unreleased]` line for each (design "Breaking changes", items 1–10).
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*` (3a finding F1); 4c uses
   `StreamCopy` only through 4b's buffer. A new namespace-qualified `IO.X` is not written.
9. **Commits** follow the repository style: `feat!:` for a breaking feature PR (as 2a–3b used), `test:` for tests only, `docs:`
   for documentation only. The plan authorises no `git push`, no `gh` command and no remote action.
10. **Names from 4a and 4b are placeholders.** The design's consumed-types table fixes the contract, not the member names;
    tasks 2.1 and 4.1 re-derive every call site from the merged code. Wherever this plan writes `DispatchContext`,
    `PromoteToRequest`, `ExceptionFacts.IsFatal` and kin, the merged code wins.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green build is the
lint gate. Test runner is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`, not VSTest's `--filter`).

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
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # only ReDriveRequestIsolationTests.cs may appear (convention 5)
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
runs before the push of PR 2 and PR 6 (they rewrite the most code). No `PackageReference` changes, so no `packages.lock.json`
change is expected; if a locked restore complains, run `dotnet restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info PIPE
scripts/knowledge --gaps PIPE               # 0 of 40: no PIPE ID is a gap ID
scripts/knowledge --req PIPE-5              # per row in scope of the PR (also REDIR-11, REDIR-24, REDIR-25, RECOV-15, XCUT-8 for cited work)
```

---

## Plan-level readings (where the design was ambiguous or internally inconsistent)

The design's decisions are not changed. Where it left a point open the plan took the reading most consistent with it, and
flagged it for the lead where the reading moves work between PRs.

- **R1 — The stage enum change lands in PR 1, not PR 2 (open for the lead).** The design's landing table puts `PIPE-1`,
  `PIPE-3` and `PIPE-4` (whose tests need nine stages, `PerHop` and `Serde`) in PR 1 and the enum change (`PerCall = 150`,
  `PerHop = 250`, `Serde = 700`) in PR 2. Both cannot hold: `StageOrderTests.One_probe_per_stage…` and
  `Stage_values_are_strictly_increasing_and_sparse` cannot pass on the as-built six-member enum. The plan moves the enum
  change, the two `PerCall` source sites (`IdempotencyPolicy`, `ClientIdentityPolicy`: unchanged source, new value) and the
  `ReDriveRequestIsolationTests` enum token (`PerCall` → `PerHop`, fact 8) into **task 1.1**. This is safe on the as-built
  signature: `IdempotencyPolicy` and `ClientIdentityPolicy` stamp `context.Request` once above `RedirectPolicy`, which reads
  `context.Request` as its held request and builds every hop from it, so each hop carries the stamp (P4c-23). PR 1's gate then
  has no 4a/4b dependency, as the design intends. The PR 2 row for `PIPE-2` keeps its loop-boundary tests. The **Breaking 3**
  CHANGELOG line moves to PR 1; the policy-signature edits to the S6 class stay in PR 2 (task 2.10).
- **R2 — `PipelineContext` test factory.** The constructor is `internal` (P4c-4). Tests build contexts through
  `internal static PipelineContext PipelineContext.Create(Request seed, DexpaceClientOptions options, RequestOptions requestOptions,
  CancellationToken token, DispatchContext dispatch)` plus the test helper `tests/Dexpace.Sdk.Core.Tests/Pipeline/TestContexts.cs`
  (`TestContexts.For(Request?, DexpaceClientOptions?)`), which uses the `InternalsVisibleTo` Core already grants `Core.Tests`.
  `TestSupport` cannot see internals, so no double that needs a context lives there.
- **R3 — Internal terminal type.** The runner needs the async transport, the sync transport and the sync/async decision. The
  plan names one internal sealed class `PipelineTerminal` (constructor takes the `IAsyncHttpClient` and decides at build whether
  `transport is IHttpClient` or `transport.AsBlocking()`; P4c-13). The design names no such type; `PipelineRunner`'s internal
  constructor takes `(PipelineEntry[] entries, int index, PipelineTerminal terminal)`.
- **R4 — `SyncPath.GetCompletedResult` shape.** `internal static Response GetCompletedResult(ValueTask<Response> task, string policyName)`:
  if `!task.IsCompleted` throw `InvalidOperationException` naming the policy; else `task.GetAwaiter().GetResult()` under the one
  `#pragma` (facts 1 and 2: a completed faulted task rethrows the original exception, unwrapped).
- **R5 — `HttpPipelinePolicy.Process` default.** Calls `ProcessAsync(request, context, next)` and passes the result through
  `SyncPath.GetCompletedResult`'s blocking sibling: because a third-party policy may genuinely suspend, the default uses
  `task.AsTask().GetAwaiter().GetResult()` (not the completed-only helper). That is the documented bridge (§5.3) and the
  second `#pragma` site.
- **R6 — Overload analyzers.** Position F (as corrected) lists the `SendAsync`/`Send` overloads with **no optional token anywhere**: two
  defaulted overloads trip `RS0026`, and a default only on the shortest overload trips `RS0027` (the defaulted overload must have the most
  parameters). The as-built `SendAsync(Request, DexpaceClientOptions, CancellationToken = default)` therefore loses its default, which breaks
  its two-argument callers; task 3.1 edits them (`SmokeChecks.cs:112`, `InstrumentationPolicyTests.cs:49`, the two READMEs). If the analyzer
  still rejects a shape, stop and re-read position F rather than renaming.
- **R7 — Test file layout.** The existing `PipelineRunnerTests.cs` becomes `RunnerTests.cs` (`git mv`, class renamed), the
  design's `RunnerTests` name; `DexpacePipelineTests.cs`, `HttpPipelineTests.cs`, `PipelineBuilderTests.cs`,
  `PipelineContextTests.cs` keep their names and are migrated in place, with the row ID in the name of any test rewritten to a
  new rule (design "Migration plan", the `PipelineBuilder` row).
- **R8 — Ported test provenance.** Each ported case carries a header comment with path and sha, `nodejs-sdk@c0ff3fd
  packages/core/src/pipeline/{builder,cursor,runtime,stage}.test.ts`, as the design's "Tests, vectors and ports" requires. Ruby's
  `pipeline_test.rb`, `test/dexpace/pipeline/` and `docs/sdk-documentation/pipelines.md` are **absent locally** (as 2a–3b found:
  `docs/sdk-documentation/` holds `bodies.md`, `http.md`, `io.md`, `seams.md`); Ruby's cases are taken from
  `ruby-sdk/docs/work/mvp/phase4/phase4c/2026-09-08-phase4c-stage-pipeline-design.md` ("Testing strategy"). The close-out page
  records the absence.

---

## PR 1 — The builder, the stage enum, the doubles

**Gate: none (no 4a or 4b type).** Rows: `PIPE-1`, `PIPE-3`–`PIPE-8`, `PIPE-18`–`PIPE-23`, `PIPE-25`, `PIPE-38` (and the enum half of `PIPE-2`).
Files: `src/Dexpace.Sdk.Core/Pipeline/{PipelineStage,PipelineStageFacts (renamed from PipelineStageHelper),PipelineEntry,PipelineBuilder,HttpPipeline}.cs`,
`Policies/{IdempotencyPolicy,ClientIdentityPolicy}.cs` (docs only), `tests/Dexpace.Sdk.TestSupport/Pipeline/*`,
`tests/Dexpace.Sdk.Core.Tests/Pipeline/*`, `tests/Dexpace.Sdk.Core.Tests/Security/ReDriveRequestIsolationTests.cs` (one token).

### Task 1.1 — The stage enum and `PipelineStageFacts` (`PIPE-2`, `PIPE-3`, `PIPE-4`, `PIPE-8`; R1) — Breaking 3

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/StageOrderTests.cs`, class `StageOrderTests`:

- `Stage_values_are_strictly_increasing_and_sparse` (`PIPE-3`; reflection over `Enum.GetValues<PipelineStage>()`: declaration
  order equals numeric order, adjacent gaps >= 50; asserts the nine members and values `Operation 100, PerCall 150, Redirect 200,
  PerHop 250, Retry 300, PerAttempt 400, Auth 500, Diagnostics 600, Serde 700`)
- `PerCall_runs_outside_redirect_and_PerHop_inside_it` (`PIPE-2`; asserts `PerCall < Redirect < PerHop < Retry` as numbers; the
  runtime loop-boundary tests are task 2.11)

New `tests/Dexpace.Sdk.Core.Tests/Pipeline/PipelineStageFactsTests.cs` (internal access): `Pillars_are_exactly_the_six`
(`Operation, Redirect, Retry, Auth, Diagnostics, Serde`; `PIPE-4`), `IsDefinedStage_rejects_values_outside_the_enum` (`(PipelineStage)0`,
`(PipelineStage)800`, `(PipelineStage)(-1)`; fact 3; `PIPE-8`), `IsDefinedStage_accepts_every_member`.

Red: CS0117 (`PipelineStage.PerHop`, `PipelineStage.Serde`), CS0103 (`PipelineStageFacts`).

**Production.**

- `PipelineStage.cs`: `PerCall = 150`, new `PerHop = 250` (the old `PerCall` slot, between `Redirect` and `Retry`), new
  `Serde = 700` (pillar, reserved, no shipped policy). Reorder declarations to numeric order. XML docs on `PerCall`:
  "<b>Breaking:</b> was 250, inside the redirect loop; now 150, outside both loops, one invocation per call. Source that says
  `PerCall` keeps compiling and changes loop" (fact 8); `PerHop`'s doc names the spec's `POST_REDIRECT`/`PRE_RETRY` adjacency.
  The type remarks list the six pillars and state "no user slot after `Auth`, `Diagnostics` or `Serde`" (P4c-7).
- `git mv Pipeline/PipelineStageHelper.cs Pipeline/PipelineStageFacts.cs`; class `PipelineStageFacts`;
  `PillarStages` becomes `FrozenSet<PipelineStage>` (`Operation`, `Redirect`, `Retry`, `Auth`, `Diagnostics`, `Serde`);
  `IsPillar(stage)` uses it; new `IsDefinedStage(PipelineStage)` is `Enum.IsDefined`-equivalent through a `FrozenSet` of the nine
  members (no reflection; AOT-safe).
- `IdempotencyPolicy` and `ClientIdentityPolicy`: **source unchanged**; their XML remarks gain the **Breaking** note (once per
  call, outside the redirect loop; was per hop; design Breaking 7).
- `tests/Dexpace.Sdk.Core.Tests/Security/ReDriveRequestIsolationTests.cs`, method
  `A_redirect_hop_is_not_built_from_the_previous_hops_stamped_request`: the probe's `PipelineStage.PerCall` becomes
  `PipelineStage.PerHop`. **This single token is the only Security-class change of task 1.1** (fact 8, P4c-22). Re-derive: run the
  method before the edit and see it fail for the stated reason (one recorded entry, `Assert.Equal([null, null], …)` fails), then green.
- Every other `PipelineStage.PerCall` use in tests (`grep -rn "PipelineStage.PerCall" tests`) is read: a stub placed to observe two hops
  moves to `PerHop`; a stub that only needs "non-pillar, before Retry" stays. List each decision in the commit message.

**`PublicAPI.Unshipped.txt`:** `PipelineStage.PerCall = 150`, `PerHop = 250`, `Serde = 700` (the old `PerCall = 250` line is replaced).
**IDs:** `PIPE-2` (enum half), `PIPE-3`, `PIPE-4`, `PIPE-8` (the predicate). **Verify:** V-fast `StageOrderTests`, `PipelineStageFactsTests`,
`IdempotencyPolicyTests`, `ClientIdentityPolicyTests`, `DexpacePipelineTests`, `ReDriveRequestIsolationTests`.

### Task 1.2 — Test doubles (additive, on the as-built signature)

New files under `tests/Dexpace.Sdk.TestSupport/Pipeline/`, each a `public sealed class` with XML docs:

- `ProbePolicy` — constructor `(string name, PipelineStage stage, List<string> log)`; records `name:in` / `name:out`.
- `ValueEqualPolicy` — `public sealed class ValueEqualPolicy(PipelineStage stage) : HttpPipelinePolicy` overriding `Equals`/`GetHashCode` to compare by
  stage, so two distinct instances are `Equals` and not `ReferenceEquals` (fact 4). It is a class, not a `record`: a record cannot inherit from the
  non-record `HttpPipelinePolicy` (CS8864).
- `tests/Dexpace.Sdk.TestSupport/Transports/TrackingResponseBody.cs` — a `ResponseBody` double that records `open` and `dispose` events into a shared ordered
  `List<string>` log, overrides `Dispose(bool)` (3b latch rule, never `Dispose()`) and carries an XML doc comment. Tasks 2.11, 3.2 and 4.3 use it; the private
  `TrackingBody` nested classes in `ResponseTests`, `PageableTests` and the S8 class are left alone.
- `FlippingStagePolicy` — a `Stage` getter returning a different stage on each read (fact 6).
- `ThrowingSynchronouslyPolicy` — a non-`async` override that throws before returning a task (used in task 2.11).
- `ShortCircuitPolicy`, `ForkingProbe` — added in task 2.3 against the new signature (they need `Request`/`Response` in the
  override and are written once, in their final form).
- `ScriptedTransport` and `RecordingTransport` (as-built, `IAsyncHttpClient` only) gain `IHttpClient.Execute`, sharing their script and request log with
  `ExecuteAsync`, as the design requires, so sync-path tests run over the real sync terminal and not `AsBlocking()`. Existing tests over them are unaffected.
- `tests/Dexpace.Sdk.TestSupport/Transports/DisposeCountingTransport.cs` (`IAsyncHttpClient` + `IHttpClient`, counts disposals,
  both forms) and `SyncFirstTransport.cs` (`IAsyncHttpClient` + `IHttpClient`, `Execute` answers, `ExecuteAsync` throws
  `InvalidOperationException`; `PIPE-28`'s "never calls an async member").

All of these compile against the as-built `ProcessAsync(PipelineContext, PipelineRunner)` and are adapted in task 2.3 (a
mechanical edit listed there). **IDs:** none (support). **Verify:** `dotnet build tests/Dexpace.Sdk.TestSupport --configuration Release`.

### Task 1.3 — The builder over entries: read-once stage, collisions at insertion, undefined-stage rejection (`PIPE-1`, `PIPE-4`, `PIPE-5`, `PIPE-6`, `PIPE-7`, `PIPE-8`, `PIPE-22`) — Breaking 1

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/PillarRuleTests.cs`, class `PillarRuleTests` (header comment cites
`nodejs-sdk@c0ff3fd packages/core/src/pipeline/builder.test.ts`):

- `Each_pillar_admits_one_policy` (`[Theory]` over the six pillars; `PIPE-4`)
- `A_custom_policy_may_occupy_the_serde_pillar` (`PIPE-4`)
- `A_second_distinct_policy_on_a_pillar_fails_at_add_naming_both_types` (`[Theory]` over `Add`, `Prepend`, `InsertBefore`,
  `InsertAfter`, `AddRange`, `PrependRange`; the message contains the stage name, both runtime type names and `Replace`; `PIPE-5`)
- `Replace_swaps_a_pillar_occupant_without_a_collision` (`PIPE-5`)
- `Re_adding_the_same_instance_is_idempotent` (entries unchanged; `PIPE-6`)
- `Two_value_equal_policies_are_distinct` (`ValueEqualPolicy` pair: asserts `Equals` **and** `!ReferenceEquals` in the same test,
  then the second `Add` throws; the half that fails against an `Equals`-based check; `PIPE-6`, fact 4)
- `A_policy_reporting_an_undefined_stage_is_rejected` (`(PipelineStage)800`, `(PipelineStage)0`; `ArgumentException`, `ParamName` `policy`;
  every insertion path; `PIPE-8`, fact 3)
- `A_policy_whose_stage_flips_cannot_pass_validation_in_one_slot_and_run_in_another` (`FlippingStagePolicy`: the builder reads once; the
  built order matches the stage recorded at `Add`; `PIPE-22`, fact 6)

New `tests/Dexpace.Sdk.Core.Tests/Pipeline/NonPillarOrderTests.cs`:
`Append_adds_to_the_tail_and_prepend_to_the_head` (`PIPE-7`), `Order_within_a_stage_survives_an_edit_in_another_stage` (`PIPE-7`),
`Re_adding_a_non_pillar_instance_appends_it_again` (`PIPE-7`; the letter, P4c-9).

Extend `StageOrderTests` with `One_probe_per_stage_runs_in_stage_order_whatever_the_insertion_order` (`PIPE-1`: nine `ProbePolicy`, shuffled
under a pinned seed through the as-built `ProcessAsync` signature; the `in` log equals the enum order, the `out` log its exact reverse;
the Ruby 4c design's `PIPE-1` conformance case).

Migrate `PipelineBuilderTests`: `Build_TwoPoliciesInPillarStage_Throws` becomes `PIPE-5`'s `Add`-time rule (rewritten into
`PillarRuleTests`, the old name deleted with a note in the commit); the others keep their assertions.

Red: CS1061 (`Prepend`, `PipelineEntry`) and the as-built collision firing at `Build`, not `Add`.

**Production.**

- New `Pipeline/PipelineEntry.cs`: `internal readonly record struct PipelineEntry(HttpPipelinePolicy Policy, PipelineStage Stage)`.
- `PipelineBuilder`: the list becomes `List<PipelineEntry> _entries`; `Add`, `Prepend` (new: head of its stage), `InsertBefore<T>`,
  `InsertAfter<T>`, `Replace<T>`, `AddRange`, `PrependRange` read `policy.Stage` **once**, validate it with `PipelineStageFacts.IsDefinedStage`
  (`ArgumentException`, `paramName: "policy"`), build the entry, then run one internal `CheckPillar(PipelineEntry)`: same instance on its
  pillar (by `ReferenceEquals`) is a no-op; a distinct instance throws `InvalidOperationException("Pipeline stage 'Retry' already holds
  RetryPolicy; cannot add MyRetryPolicy. Use Replace<RetryPolicy>(…) to swap it.")`. `Replace<T>` of a pillar occupant never collides.
  `Build` stable-sorts the entries by the **recorded** stage (`OrderBy` is stable, fact 5); the `Build`-time count check is deleted.
- XML docs: `Add`, `Prepend`, `Build` remarks carry **Breaking 1** ("a collision threw at `Build`, naming a count; now at `Add`,
  naming both types; re-adding the same instance threw, now a no-op").

**`PublicAPI.Unshipped.txt`:** `PipelineBuilder.Prepend`. **IDs:** `PIPE-1`, `PIPE-4`–`PIPE-8`, `PIPE-22`.
**Verify:** V-fast `PillarRuleTests`, `NonPillarOrderTests`, `StageOrderTests`, `PipelineBuilderTests`.

### Task 1.4 — Surgical edits and bulk operations (`PIPE-7`, `PIPE-18`–`PIPE-23`, `PIPE-38`) — Breaking 2

**Failing tests first.** New `SurgicalEditTests.cs` (builder entries read through the internal `Entries` accessor, `InternalsVisibleTo`):

- `Insert_relative_to_an_anchor_in_another_stage_is_rejected` (`[Theory]` over `InsertBefore`/`InsertAfter`; `ArgumentException`
  naming both stages; builder entries unchanged after the throw; `PIPE-18`)
- `Insert_next_to_the_first_instance_of_T_in_the_same_stage` (pin; `PIPE-18`)
- `A_cross_stage_replace_is_rejected` (`PIPE-19`) and `Replace_swaps_only_the_first_instance` (`PIPE-19`)
- `Remove_deletes_every_instance_and_preserves_order` and `Remove_of_an_absent_type_is_a_no_op` (pins; `PIPE-20`)
- `A_missing_anchor_throws_InvalidOperationException_naming_T` (`[Theory]` over `InsertBefore`, `InsertAfter`, `Replace`; the old
  `InsertAfter_TypeNotPresent_Throws` and kin are renamed into this class; `PIPE-21`)
- `Edited_order_equals_order_built_from_scratch` (an `InsertAfter`, a `Remove` and a `Replace`; `Build(...).Policies` compared by
  identity, element by element, with a builder seeded from scratch; `PIPE-22`)

New `BulkTests.cs` (header cites `builder.test.ts`):

- `A_colliding_batch_leaves_the_builder_unchanged` (`[Theory]`: a batch colliding with an existing pillar, and a batch holding two
  distinct policies for one pillar; entries captured before, asserted equal by identity after; `PIPE-23`)
- `AddRange_keeps_batch_order_and_PrependRange_reverses_it` (`[a, b, c]` → `a, b, c` and `c, b, a`, with a pillar policy in the batch
  landing in its own slot; `PIPE-38`)

Red: the as-built cross-stage insert re-buckets silently and `Replace<T>` accepts any stage.

**Production.** `PipelineBuilder`: `InsertBefore<T>`/`InsertAfter<T>`/`Replace<T>` compare the new policy's recorded stage with the anchor's
and throw `ArgumentException(paramName: "policy")` naming both; `AddRange`/`PrependRange` validate the whole batch against the builder and
against itself into a scratch list, then commit once (all-or-nothing); `PrependRange` prepends each element in turn (the documented
reversal). `Remove<T>` unchanged. Docs on `PrependRange` state the reversal and **Breaking 2** sits on `InsertBefore`/`InsertAfter`/`Replace`.

**`PublicAPI.Unshipped.txt`:** `AddRange`, `PrependRange`. **IDs:** `PIPE-7`, `PIPE-18`–`PIPE-23`, `PIPE-38`.
**Verify:** V-fast `SurgicalEditTests`, `BulkTests`, `PipelineBuilderTests`.

### Task 1.5 — `HttpPipeline.Policies` (`PIPE-25`)

**Failing test first.** `HttpPipelineTests.Policies_is_an_ordered_read_only_view` (order equals build order by identity; the view's runtime
type is `ReadOnlyCollection<HttpPipelinePolicy>` over a private copy; a cast to `IList<HttpPipelinePolicy>` reports `IsReadOnly`;
mutating the builder after `Build` leaves the view unchanged). Red: CS1061 (`Policies`).

**Production.** `HttpPipeline` gains `public IReadOnlyList<HttpPipelinePolicy> Policies { get; }` over a `ReadOnlyCollection` of a copy of the
built policies, and its internal constructor keeps the `HttpPipelinePolicy[]` for the as-built runner (task 2.3 moves it to entries).

**`PublicAPI.Unshipped.txt`:** `HttpPipeline.Policies.get`. **IDs:** `PIPE-25`. **Verify:** V-fast `HttpPipelineTests`.

### Task 1.6 — Close-out (PR 1)

`CHANGELOG.md` `[Unreleased]` `### Changed`: **Breaking** lines for items 1 (collision at `Add`, `ReferenceEquals` idempotence), 2 (cross-stage
edits throw), 3 (`PerCall` is 150 and runs once per call; `PerHop` is the old slot; source that says `PerCall` changes loop), 7 (idempotency
and client identity run once per call), `### Added`: `Prepend`, `AddRange`, `PrependRange`, `HttpPipeline.Policies`, the `Serde` stage.
Run **V-gate** (the Security diff lists only `ReDriveRequestIsolationTests.cs`, one hunk). **Commit:** `feat!: phase 4c builder, stage
enum and surgical edits (PIPE-1, PIPE-3..PIPE-8, PIPE-18..PIPE-23, PIPE-25, PIPE-38)`.

---

## PR 2 — The signature, the context, the policies, the real sync path

**Gate: PR 1, and 4a and 4b merged.** Rows: `PIPE-2`, `PIPE-10`–`PIPE-17`, `PIPE-28`–`PIPE-30`, `PIPE-36`, `PIPE-40` (and the `PIPE-17` carriage half).
**PR 2 cannot be split** (roadmap step 5's exception): the build is red between tasks 2.2 and 2.9 by design. Task order keeps `src/` compiling
as early as possible: after task 2.9 `dotnet build src/Dexpace.Sdk.Core --configuration Release` is green; after task 2.10 the tests compile. Per-task
"red" below means the test class written in that task, run once the test project compiles (task 2.10); within 2.2–2.9 the interim check is
`dotnet build src/Dexpace.Sdk.Core --configuration Release`.

### Task 2.1 — Pre-flight: re-derive the 4a and 4b surface (no commit)

At the start of PR 2 and again before PR 4, read the merged 4a and 4b code and write the mapping from the design's consumed-types table to real
names into the PR description (scratch, not a file). Rows to fill: `CallKey`, `InstrumentationContext.None`/`FromActivity`, `CallContext` and its
`Close()`, `DispatchContext` and its construction, `PromoteToRequest(Request, string? operationName)`, `PromoteToExchange(Response)`,
`ExchangeContext`, the carrier of the operation id (`SEAM-28`), `ExceptionFacts.IsFatal`/`EnumerateCauses`, `ExceptionTrail.AddSuppressed`/
`GetSuppressed`, `Outcome`, `ResponseRecoveryChain`, `IResponseStep`, `ErrorMappingStep.Instance`, `ErrorBodyBuffer.Capture(Async)`, and the shapes
of `IdempotencyPolicy`/`ClientIdentityPolicy` after 4b's change. Confirm each contract line of the design's table; a line the merged code does not
meet **reopens the consuming row** (design Risk 1) and stops the task, it is not worked around. If 4a took P4a-13 option 1
(`RequestOptions.CallKey`), apply position I item 4 in task 2.4. **Verify:** `git log --oneline main -8` shows 4a and 4b merged;
`dotnet build Dexpace.Sdk.sln --configuration Release` green before any 4c edit.

### Task 2.2 — `PipelineContext`, `CallState`, `PipelinePropertyKey<T>` (`PIPE-10`, `PIPE-11`, `PIPE-16`, `PIPE-17`) — Breaking 5

**Failing tests first.** Rewrite `tests/Dexpace.Sdk.Core.Tests/Pipeline/PipelineContextTests.cs` over `TestContexts` (R2):

- `Copies_share_the_call_scoped_state_and_not_the_per_drive_state` (`ForAttempt(2)` and `ForHop(1)` return new instances whose `AttemptNumber` /
  `HopNumber` differ while `SeedRequest`, `RequestOptions`, `Options`, `CallKey` and the property bag are shared by reference; `PIPE-16`)
- `The_property_bag_is_keyed_by_reference_identity` (two `PipelinePropertyKey<string>("k")` instances do not collide; a get on a key of the same
  name but another instance is a miss; `P4c-4`)
- `The_property_bag_is_shared_across_copies` (`PIPE-11`: per-call state lives on the context)
- `WithCancellationToken_and_WithActivity_change_one_drive_value_only`
- `The_seed_request_is_the_request_passed_at_entry` (`PIPE-17`, P4c-5)
- `The_context_offers_no_way_to_write_the_request_upward` (reflection over `typeof(PipelineContext)`: no property of type `Request` with a
  setter, no method taking a `Request` that returns `void`; also placed in `ReDriveRequestIsolationTests` in task 2.10, S6)
- `There_is_no_public_constructor` (`P4c-4`)
- `RequestOptions_defaults_to_Empty_until_the_pipeline_sets_it` (carriage seeded here, flow tested in 3.1)

Red: CS0117/CS1061 on every new member.

**Production.**

- `Pipeline/CallState.cs`: `internal sealed class CallState` holding `SeedRequest`, `RequestOptions`, `Options` (`DexpaceClientOptions`), `CallKey`,
  `InstrumentationContext Instrumentation`, the property bag (`Dictionary<object, object?>` keyed by `PipelinePropertyKey` identity, lazily created)
  and the furthest promoted `CallContext?` (position I item 2; the terminal is its only writer).
- `Pipeline/PipelinePropertyKey.cs`: `public sealed class PipelinePropertyKey<T>(string name)`, `Name`, `ToString()`; no `Equals` override.
- `PipelineContext.cs` rewritten (position B table): `sealed class` with an **internal** constructor and `internal static Create(…)` (R2);
  properties `SeedRequest`, `RequestOptions`, `Options`, `CallKey`, `Instrumentation`, `CancellationToken`, `Activity`, `AttemptNumber`,
  `HopNumber`; `TryGetProperty<T>`, `SetProperty<T>`; `ForAttempt`, `ForHop`, `WithActivity`, `WithCancellationToken`. **Removed:** the public
  constructor, `Request`, `Response`, `GetProperty<T>(string)`, `SetProperty<T>(string, T)`, and the four `internal set`s. Remarks carry
  **Breaking 5** and say the retired string key `"dexpace.auth.origin"` was overwritable by any policy (§6.2).

**`PublicAPI.Unshipped.txt`:** remove the lines for the removed members; add the position-B members and `PipelinePropertyKey<T>`.
**IDs:** `PIPE-10`, `PIPE-11`, `PIPE-16`, `PIPE-17`. **Verify:** interim build only (see PR 2 header).

### Task 2.3 — The signature, `PipelineRunner`, `SyncPath`, the terminal (`PIPE-12`, `PIPE-13`, `PIPE-14`, `PIPE-15`, `PIPE-28`, `PIPE-29`, `PIPE-30`) — Breaking 4, 6

**Failing tests first.** New and rewritten classes (compiled in task 2.10; written here):

- `RunnerTests` (`git mv PipelineRunnerTests.cs RunnerTests.cs`; header cites `cursor.test.ts` and `runtime.test.ts`):
  `Next_dispatches_to_the_transport_after_the_last_policy_with_the_callers_options` (`PIPE-13`; replaces
  `The_transport_receives_RequestOptions_Empty`, the 2b hand-off), `A_null_from_the_transport_fails_with_PipelineAbortedException` (`SEAM-16`, pinned),
  `A_null_from_a_policy_fails_with_PipelineAbortedException`, the migrated order test, and `PipelineRunner_has_no_mutable_field` (reflection; `PIPE-13`).
- `PolicyContractTests`: `A_short_circuit_returns_its_synthetic_response_and_reaches_nothing_downstream` (downstream probes entered zero times,
  `transport.CallCount == 0`, `Assert.Same` on the synthetic response; sync and async; `PIPE-12`),
  `A_policy_may_substitute_the_response_on_the_way_out` (`PIPE-12`),
  `A_substituted_request_reaches_every_downstream_policy_and_the_transport` (`Assert.Same` at each probe and at the transport; `PIPE-14`).
- `ForkTests`: `Driving_the_same_runner_twice_re_runs_the_whole_downstream_tail` (`ForkingProbe` at `Retry`; counting probes at `PerAttempt` and
  `Auth`, each entered exactly twice; `PIPE-15`), `Each_fork_carries_the_request_its_forker_passed` and
  `Forks_share_call_scoped_state_and_not_per_drive_state` (`PIPE-16`).
- `AsyncErrorModelTests`: `A_synchronous_throw_from_a_policy_becomes_a_faulted_task` (`ThrowingSynchronouslyPolicy`; calling `RunAsync` does not throw,
  awaiting does, with the same instance; `PIPE-30`), `An_out_of_memory_exception_is_not_caught_by_any_sdk_frame` (`PIPE-30`; the instance surfaces by
  identity with no `ExceptionTrail` entry), `A_shipped_policy_reports_a_failure_as_a_faulted_task` (`PIPE-29`; written here over a stand-in, extended
  over every shipped policy in task 2.11).
- `SyncPathTests`: `Run_reaches_the_transports_Execute_when_it_implements_IHttpClient`, `Run_uses_AsBlocking_for_an_async_only_transport` (P4c-13),
  `GetCompletedResult_rethrows_the_original_exception_of_a_faulted_completed_task` (fact 2), `GetCompletedResult_throws_when_the_task_has_not_completed`
  (R4), `The_default_Process_bridges_to_ProcessAsync` (R5).

Red: CS7036/CS0115 on the new signature everywhere.

**Production.**

- `HttpPipelinePolicy`: `public abstract PipelineStage Stage { get; }`; `public abstract ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner next)`;
  `public virtual Response Process(Request request, PipelineContext context, PipelineRunner next)` whose default is the documented blocking bridge
  (R5, the one `#pragma warning disable RS0030` with the §5.3 citation). Remarks: **Breaking 4** and **Breaking 6**.
- `PipelineRunner`: `readonly struct`, internal constructor `(PipelineEntry[] entries, int index, PipelineTerminal terminal)` (R3), public
  `ValueTask<Response> RunAsync(Request request, PipelineContext context)` and `Response Run(Request request, PipelineContext context)`. Past the last
  policy: the terminal (task 2.4 fills the promotion around it); otherwise `entries[index].Policy.ProcessAsync(request, context, next)` / `.Process(…)`.
  `RunAsync` is `async`, so a policy's synchronous throw becomes a faulted task (`PIPE-30`). The result of the terminal **and** of each policy is
  checked for `null` (`PipelineAbortedException`, `SEAM-16`).
- New `Pipeline/PipelineTerminal.cs` (internal, R3): holds the async transport, the sync transport (`transport as IHttpClient ?? transport.AsBlocking()`,
  decided once at build) and the `ExecuteAsync`/`Execute` calls with `context.RequestOptions` and `context.CancellationToken`.
- New `Pipeline/SyncPath.cs` (internal static): `GetCompletedResult` (R4) with the second `#pragma`.
- `HttpPipeline`'s internal constructor takes `PipelineEntry[]` and a `PipelineTerminal`; `PipelineBuilder.Build(IAsyncHttpClient)` builds both.
- The shared test doubles adapt: `ProbePolicy`, `ValueEqualPolicy`, `FlippingStagePolicy`, `ThrowingSynchronouslyPolicy` move to
  `ProcessAsync(Request, PipelineContext, PipelineRunner)` (mechanical: `return await next.RunAsync(request, context)`); add `ShortCircuitPolicy` and
  `ForkingProbe` (drives `next` *n* times, disposes each superseded response, returns the last unclosed).

**`PublicAPI.Unshipped.txt`:** remove `ProcessAsync(PipelineContext, PipelineRunner) -> ValueTask`, `RunAsync(PipelineContext) -> ValueTask`; add the four lines of
design "Type shapes". **IDs:** `PIPE-12`–`PIPE-15`, `PIPE-28`, `PIPE-29`, `PIPE-30`. **Verify:** interim build.

### Task 2.4 — `HttpPipeline` entry, the seed, the context-chain wiring (position I; `PIPE-10`, `PIPE-16`; `SEAM-28`)

**Failing tests first.** New `ContextChainWiringTests` (the four tests of position I; fakes from the merged 4a surface found in task 2.1):

- `Nothing_is_registered_before_the_first_transmission` (a `ProbePolicy` above the transport observes the store empty for the call's key; `CTX-17`)
- `One_store_entry_per_call_across_a_retried_and_redirected_call` (a 307 then 503 then 200 through `Redirect` and `Retry`; the key has at most one occupant at any
  time; the occupant at the end is the exchange link)
- `The_returned_responses_dispose_closes_the_occupant` (design §5.4: disposing the response closes the exchange link; the store no longer holds the key)
- `A_failure_closes_the_furthest_link_before_the_exception_surfaces` (transport throws; the key is gone when the caller's `catch` runs; the same exception
  instance and stack surface)
- `A_fatal_exception_skips_the_close` (`OutOfMemoryException`; the store's bound is the backstop; position I item 3)
- `A_nested_pipeline_call_leaves_no_store_entry_after_the_response_is_disposed` (an inner pipeline as the outer's transport, position I item 5: two exchange links on one
  response; disposing it closes both, in reverse attachment order; the store holds neither key afterwards; the transport sees the inner call's `CallKey`; added to this class in
  task 3.3 once `Nest` exists)

New `SeedOriginTests`: `The_seed_request_is_fixed_at_entry_and_not_the_held_request` (`context.SeedRequest` is the request handed to `Send`, after a policy at `PerCall` stamped
a header; `P4c-5`).

**Production.** `HttpPipeline`: internal `SendCoreAsync(Request, DexpaceClientOptions, RequestOptions, CancellationToken, bool async)` creating one `DispatchContext`
(`InstrumentationContext.FromActivity(Activity.Current)` or `None`; the operation id from 4a's carrier, `SEAM-28`), the `CallState` and the first `PipelineContext`;
the existing `SendAsync(Request, DexpaceClientOptions, CancellationToken)` and `Send(…)` delegate to it with `RequestOptions.Empty` until task 3.1. `Send` stops being
`SendAsync(…).AsTask().GetAwaiter().GetResult()` and drives `Run` (the `#pragma` there is deleted; `SyncPath` carries it). `PipelineTerminal`'s two paths wrap the
transport call in `PromoteToRequest(request, operationName)` / `PromoteToExchange(response)` and attach the exchange link to the response through the internal hook
`Response.AttachExchange(ExchangeContext)` (added in `Response.cs`; the hook **appends** to a small list of links and the latched dispose closes every link in reverse order, because a nested pipeline attaches a second one, position I item 5; if 4a already shipped a single-slot hook, widen it). The failure path is
`catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` → close the furthest link → `throw;`. The pipeline never calls the store.

**IDs:** `PIPE-10`, `PIPE-16`; cites `SEAM-28`, `CTX-2`, `CTX-10`, `CTX-17` (4a's rows). **Verify:** interim build.

### Task 2.5 — The stamping policies on `ProcessCoreAsync` (`PIPE-11`, `PIPE-28`, `PIPE-29`, `PIPE-36`)

`OperationPolicy`, `IdempotencyPolicy`, `ClientIdentityPolicy`, `SetDatePolicy`: each gains `private async ValueTask<Response> ProcessCoreAsync(Request, PipelineContext, PipelineRunner, bool async)`
branching each I/O call `async ? await next.RunAsync(…) : next.Run(…)`; `ProcessAsync` returns `ProcessCoreAsync(…, async: true)`; `Process` returns
`SyncPath.GetCompletedResult(ProcessCoreAsync(…, async: false), nameof(Policy))`. Rewrites by the migration table: `context.Request` becomes the `request` parameter; a stamped request is the one
passed to `next`; `OperationPolicy` passes `context.WithCancellationToken(cts.Token)` downstream and disposes the linked source after the downstream returns. `IdempotencyPolicy`'s
bag key becomes `static readonly PipelinePropertyKey<string>` (4b's shape wins if it changed). Argument validation (`ArgumentNullException.ThrowIfNull`) sits inside the `async` body (`PIPE-29`).
Every shipped policy keeps only `readonly` fields (`PIPE-11`).

**Failing tests first** (migrate each `…PolicyTests` over `TestContexts`, adding a sync twin per behaviour test): `OperationPolicyTests`, `IdempotencyPolicyTests`,
`ClientIdentityPolicyTests`, `SetDatePolicyTests` each gain `Sync_and_async_agree` (same request in, same stamped request at the transport) and keep their existing assertions.
**IDs:** `PIPE-11`, `PIPE-28`, `PIPE-29`. **Verify:** interim build.

### Task 2.6 — `InstrumentationPolicy` (`PIPE-28`, `PIPE-29`)

Keeps its `MA0051` waiver (5b/5c restructure it). Moves onto `ProcessCoreAsync(…, bool async)`: `context.Activity = activity` becomes
`context.WithActivity(activity)` passed downstream (no restore in `finally`: the copy dies with the call); the `traceparent` stamp goes on the request passed
down; the status code read after the call is the returned response's (not `context.Response?.Status`). Tests: migrate `InstrumentationPolicyTests` over `TestContexts`;
add `The_downstream_sees_the_activity_and_the_upstream_does_not` (the write cannot reach upward, `PIPE-16`) and a sync twin. **IDs:** `PIPE-28`, `PIPE-29`. **Verify:** interim build.

### Task 2.7 — `RetryPolicy` and `BlockingWait` (`PIPE-16`, `PIPE-28`, `PIPE-40`)

**Failing tests first.** `RetryPolicyTests` migrated; new `BlockingWaitTests` (internal): `Waits_the_requested_time_on_a_fake_clock` (`InstantTimeProvider`),
`Cancels_when_the_token_is_cancelled`, `Chunks_a_wait_longer_than_49_days` (S7's clamp reused; no `Task` under it), `A_zero_or_negative_wait_returns_at_once`.
`RetryPolicyTests.Sync_send_retries_through_the_sync_chain` (a pipeline built over `SyncFirstTransport`, the dual-interface double: `PipelineBuilder.Build(IAsyncHttpClient)` does not accept a sync-only transport, position E; an async member of a shipped policy is never called).

**Production.** `RetryPolicy.ProcessAsync`/`Process` delegate to `ProcessCoreAsync(…, bool async)`; the held request is passed to `next.RunAsync(request, context.ForAttempt(n))`
(the restore is gone: there is nothing to restore); a retried response is disposed through `Disposal.DisposeQuietlyAsync` (sync: `DisposeQuietly`) **before** the sleep, and the
in-flight response is returned undisposed on every abandon path (`PIPE-40`). The sleep is `await Task.Delay(…, TimeProvider, token)` on the async path and
`BlockingWait.Wait(TimeSpan, TimeProvider, CancellationToken)` on the sync path. New `Internal/BlockingWait.cs`: `TimeProvider.CreateTimer` setting a `ManualResetEventSlim`,
waited with the token (design position E part 3); the 49-day chunking and 365-day clamp stay shared with the async sleep (one helper, S7 unchanged). `RetryPolicy`
gains `#pragma warning disable MA0051` with the comment "6a rewrites the retry policy on this signature (design §6.1)". **IDs:** `PIPE-16`, `PIPE-28`, `PIPE-40`; cites `RETRY-44`. **Verify:** interim build.

### Task 2.8 — `RedirectPolicy` and the seed origin (`PIPE-16`, `PIPE-28`, `PIPE-40`; `REDIR-11` work)

**Failing tests first.** `RedirectPolicyTests` migrated; `SeedOriginTests.A_redirect_judges_cross_origin_against_the_seed_not_the_previous_hop` (an A → B → A chain: the second
hop to A is same-origin with the seed, so `Cookie` is kept where the as-built local `seedUrl` agrees; and a chain whose first hop leaves the seed origin strips `Cookie` and
`Proxy-Authorization` at every later hop; `REDIR-9`, `REDIR-11` seed definition) and `A_policy_above_redirect_that_rewrites_the_url_does_not_move_the_seed` (P4c-5).

**Production.** `ProcessCoreAsync(…, bool async)`; the local `seedUrl` becomes `context.SeedRequest.Url`; each hop is `next.RunAsync(request, context.ForHop(n))`; the
response is the returned local, never `context.Response`; the superseded response is disposed through `Disposal.DisposeQuietlyAsync` before the next drive; every abandon
path (non-replayable body, hop budget exhausted, missing or malformed `Location`, unsupported scheme, downgrade) returns the in-flight response undisposed. The existing `MA0051`
waiver's comment is kept. **IDs:** `PIPE-16`, `PIPE-28`, `PIPE-40`; cites `REDIR-11`. **Verify:** interim build.

### Task 2.9 — `AuthorizationPolicy` and its subclasses (`PIPE-28`; `REDIR-24` work) — Breaking 8

**Failing tests first.** `AuthHttpsGuardTests` stays unedited and is the red/green witness (it must compile and pass unedited after this task: `CountingAuthPolicy` overrides
`GetCredentialAsync(PipelineContext)`, fact 10). New `SeedOriginTests.Auth_compares_the_origin_against_the_seed_request` (a hop that left the seed origin gets the credential
header stripped and the credential not resolved; a hop back to the seed origin is stamped; `REDIR-24`, `AUTH-29`), `…The_retired_bag_key_cannot_be_hijacked`
(a policy above auth that writes a property named `dexpace.auth.origin` changes nothing; P4c-4), and a sync twin per subclass (`BasicAuthPolicy`, `ApiKeyAuthPolicy`,
`BearerTokenAuthPolicy`).

**Production.** `AuthorizationPolicy` declares `public sealed override` for **both** `ProcessAsync` and `Process` (as-built it seals `ProcessAsync`; a plain `Process` override would let a subclass skip the HTTPS guard, `AUTH-28`, on the sync path). `OriginKey` constant and the bag lookup are deleted; the origin is compared with `context.SeedRequest.Url`; both entry points run
`ProcessCoreAsync(…, bool async)`; the HTTPS guard still runs before any credential resolution on both paths (AUTH-28). `protected abstract GetCredentialAsync(PipelineContext)` keeps its
signature; new `protected virtual (string HeaderName, string HeaderValue) GetCredential(PipelineContext context)` whose default is the documented bridge over `GetCredentialAsync`
(the third `#pragma`, citing §5.3 and 6c's `AccessTokenCache` sync path); `BasicAuthPolicy` and `ApiKeyAuthPolicy` override it trivially. **PublicAPI:** add `AuthorizationPolicy.GetCredential`
and the policy overrides, with `override sealed …AuthorizationPolicy.Process(…)` and `override sealed …AuthorizationPolicy.ProcessAsync(Request!, …)`. Test: `PillarRuleTests.Authorization_policy_entry_points_are_sealed` (reflection: `AuthorizationPolicy.Process` and `ProcessAsync` are both `IsFinal`; `PIPE-36`, `AUTH-28`). XML remarks: **Breaking 8**. **IDs:** `PIPE-28`; cites `REDIR-24`. **Verify:** `dotnet build src/Dexpace.Sdk.Core --configuration Release` green (the src half is done).

### Task 2.10 — Migrate every remaining test and move S6 (`PIPE-16`; P4c-22)

Mechanical edits by the design's migration table to `PipelineBuilderTests`, `HttpPipelineTests`, `DexpacePipelineTests`, `PageableTests`, `PaginationStrategiesTests` (compile-only; they
call the kept `DexpaceClientOptions` overloads), and the two policy bodies in `tests/Dexpace.Sdk.Core.Tests/Security/ReDriveRequestIsolationTests.cs`:

- `ProbePolicy` records `request.Headers.Get(header)` and returns `next.RunAsync(request, context)`;
- `MarkingPolicy` returns `next.RunAsync(request.WithHeaders(request.Headers.Set("X-Attempt-Marker", "set")), context)`;
- **added** fact `The_context_offers_no_way_to_write_the_request_upward` (S6's structural proof; reflection as in task 2.2).

Every other method's arrange-act-assert is byte-identical (the one enum token moved in task 1.1). Prove it: `git diff -U0 main...HEAD -- tests/Dexpace.Sdk.Core.Tests/Security`
lists exactly the two policy bodies, the one token and the added fact. Run the whole Security category before and after. **IDs:** `PIPE-16`. **Verify:** V-fast `ReDriveRequestIsolationTests`,
then `dotnet build Dexpace.Sdk.sln --configuration Release` and `dotnet test --solution Dexpace.Sdk.sln --configuration Release` (the first full compile since task 2.2).

### Task 2.11 — The cross-cutting classes (`PIPE-2`, `PIPE-10`, `PIPE-11`, `PIPE-15`, `PIPE-28`–`PIPE-30`, `PIPE-36`, `PIPE-40`)

New classes, each over the shipped policies:

- `StageOrderTests.A_per_call_probe_runs_once_and_sees_the_final_response_while_an_auth_probe_runs_per_hop` (`ForkingProbe` at `Redirect` driving twice; a `PerCall` probe entered
  once and observing the final response by identity; an `Auth` probe entered twice; same shape with the forker at `Retry`, `PerHop` once versus `PerAttempt` twice; `PIPE-2`).
- `ConcurrencyTests`: `Concurrent_calls_share_no_per_call_state` (64 parallel sends through one default pipeline; a probe records the `context` identity and attempt ordinal;
  no context instance seen by two calls; `PIPE-10`), `HttpPipeline_has_no_settable_member` (reflection), `Every_shipped_policy_holds_only_readonly_instance_fields` (reflection
  over `Dexpace.Sdk.Core.Pipeline.Policies`; `PIPE-11`).
- `SyncPathTests.Sync_and_async_sends_visit_the_same_policies_in_the_same_order` (probes record which entry point ran; `PIPE-28`) and
  `…The_sync_send_never_calls_an_async_member_of_a_shipped_policy` (each shipped policy over `SyncFirstTransport`, whose `ExecuteAsync` throws; one policy per `[Theory]` row; `PIPE-28`).
- `AsyncErrorModelTests.A_shipped_policy_reports_a_failure_as_a_faulted_task` extended to every shipped policy (`[Theory]`; `PIPE-29`).
- `PillarRuleTests.Authorization_policy_entry_points_are_sealed` (task 2.9) and `PillarRuleTests.Every_shipped_pillar_policy_locks_its_stage` (reflection: each public type in `…Pipeline.Policies` deriving from `HttpPipelinePolicy` is sealed or its `Stage`
  override is sealed; `PIPE-36`).
- `ReDriveLifecycleTests` (`TrackingResponseBody`'s shared order log, task 1.2): superseded responses disposed before the next drive and the returned one open, for both `RedirectPolicy` and `RetryPolicy`;
  each abandon path returns the in-flight response undisposed; a throwing dispose of a superseded response (via `DisposeQuietlyAsync`) does not mask the next drive's outcome (`PIPE-40`).

**Pins** in this task are named in the commit message. **Verify:** V-fast each class, then the full suite.

### Task 2.12 — Close-out (PR 2)

`CHANGELOG.md` `[Unreleased]` `### Changed`: **Breaking** lines for items 4, 5, 6 and 8 (the signature; `PipelineContext`; `Send` runs the policies synchronously; the seed origin
replaces the first-seen origin and the public string key), `### Added`: `Process`/`Run`, `PipelinePropertyKey<T>`, `ForAttempt`/`ForHop`/`WithActivity`/`WithCancellationToken`,
`AuthorizationPolicy.GetCredential`. Run **V-gate** and the coverage gate with its self-test. **Commit:** `feat!: phase 4c request-in/response-out signature, call-scoped context and
real sync path (PIPE-2, PIPE-10..PIPE-16, PIPE-28..PIPE-30, PIPE-36, PIPE-40)`.

---

## PR 3 — `HttpPipeline` as a transport, options, `Flatten`/`Nest`, the bridges

**Gate: PR 2.** Rows: `PIPE-9`, `PIPE-17`, `PIPE-26`, `PIPE-27`, `PIPE-31`, `PIPE-33`–`PIPE-35`.

### Task 3.1 — The seam implementation, options capture, the overload family (`PIPE-9`, `PIPE-17`, `PIPE-26`, `PIPE-27`; `SEAM-2` work) — Breaking 9, 10

**Failing tests first.**

- `EmptyPipelineTests.An_empty_pipeline_passes_request_options_and_token_to_the_transport` (`Assert.Same` on the request and the options, equality on the token; sync and async;
  `PIPE-9`).
- `OptionsFlowTests.The_same_request_options_instance_reaches_every_policy_every_fork_and_the_transport` (`Assert.Same` at probes either side of a `ForkingProbe` and at the transport;
  `Assert.Equal` would pass against a per-fork copy; `PIPE-17`). If 4a took P4a-13 option 1, the identity compared is `context.RequestOptions` (position I item 4).
- `PipelineAsTransportTests.A_pipeline_stands_in_for_a_transport_and_threads_options_through` (an outer pipeline over an inner one; `Assert.Same` on the inner transport's received
  `RequestOptions`; `PIPE-26`), `…A_pipeline_backs_a_pageable` (through `Pageable.Create`; `PIPE-26`),
  `…Disposing_the_pipeline_twice_never_disposes_the_transport` (`DisposeCountingTransport`, both forms, both orders; a send after dispose still succeeds; `PIPE-27`),
  `…A_pipeline_without_a_scheduler_default` is not written (`PIPE-33` forbids a default scheduler; covered in 3.4).
- `HttpPipelineTests` overloads: `SendAsync_with_a_default_literal_token_binds_to_the_token_overload` (fact 7, no optional parameter anywhere), `The_DexpaceClientOptions_overloads_override_the_build_time_options_for_one_call` (P4c-15),
  `Build_transport_captures_default_client_options` and `Build_transport_options_captures_the_callers` (P4c-11).
- `SeamImplementationArchitectureTests`: the exemption test `A_public_pipeline_may_implement_the_seams` expects `HttpPipeline` public and allow-listed (fact 11).

Red: CS0535 (`HttpPipeline` is not an `IAsyncHttpClient`), CS1501 on the overloads.

**Production.** `HttpPipeline : IAsyncHttpClient, IHttpClient` per position F: the nine public overloads of the listed shapes, the two explicit interface members (`ExecuteAsync` returns
`SendAsync(…).AsTask()`), `Dispose`/`DisposeAsync` as documented no-ops toward the transport (no latch; `SEAM-15` documented, `XCUT-22`). `PipelineBuilder.Build(IAsyncHttpClient)` captures
`new DexpaceClientOptions()`; new `Build(IAsyncHttpClient, DexpaceClientOptions)`. `CallState.RequestOptions` is the caller's instance, fixed at entry and carried by reference across every fork;
`DexpacePipeline.CreateDefault` is untouched until PR 5. `SeamImplementationArchitectureTests` gains a **public exemption** for `HttpPipeline` ahead of the allow-list lookup (reason
`"PIPE-26"`, fact 11) and the allow-list entry; the existing test bodies otherwise stay. R6 applies: no overload carries a token default. **Breaking 9** (now disposable) and **10** (additive) in the XML docs.

**Callers of the two-argument form (edit in this task).** Removing the token default breaks every `SendAsync(request, options)` call without a token: `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs:112`
(`pipeline.SendAsync(request, new DexpaceClientOptions())`), `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/InstrumentationPolicyTests.cs:49`, and the README samples
`src/Dexpace.Sdk.Core/README.md:40` and `src/Dexpace.Sdk.Serialization.SystemTextJson/README.md:34`. Each passes `CancellationToken.None` (or uses the new `SendAsync(Request, CancellationToken)`).
None is a Security class (P4c-22 unaffected). `grep -rn "SendAsync(.*, .*)" tests src docs` after the edit; **verify** with `dotnet build Dexpace.Sdk.sln --configuration Release` and the AOT smoke publish.

**PublicAPI:** the `HttpPipeline` interface base, the overload lines, `Build(transport, DexpaceClientOptions)`, `Dispose`, `DisposeAsync`. **IDs:** `PIPE-9`, `PIPE-17`, `PIPE-26`, `PIPE-27`;
cites `SEAM-2`. **Verify:** V-fast the four classes above and `SeamImplementationArchitectureTests`.

### Task 3.2 — `SendAsync<T>` and `Send<T>` (`PIPE-31`)

**Failing tests first.** `TerminalMappingTests` (sync and async rows):

- `A_successful_handler_disposes_the_response_after_it_runs` (`TrackingResponseBody`'s order log, task 1.2)
- `A_throwing_handler_surfaces_its_own_instance_and_the_body_is_disposed`
- `A_throwing_dispose_after_a_throwing_handler_leaves_the_handlers_exception_primary` (the dispose failure appears in `ExceptionTrail.GetSuppressed`, never wrapped)
- `A_transport_failure_surfaces_unwrapped_not_as_AggregateException`

**Production.** `HttpPipeline.SendAsync<T>(Request, Func<Response, CancellationToken, ValueTask<T>>, RequestOptions, CancellationToken)` and `Send<T>(Request, Func<Response, T>, RequestOptions,
CancellationToken)`: send, apply the handler, dispose the response in every outcome (`await using`; the 3b latch makes a double dispose harmless); a handler failure is primary; a dispose failure
attaches through `ExceptionTrail.AddSuppressed` (4b's merged name). Every `catch` is filtered with `ExceptionFacts.IsFatal`. **IDs:** `PIPE-31`. **Verify:** V-fast `TerminalMappingTests`.

### Task 3.3 — `Flatten`, `Nest`, `Build()` (`PIPE-35`)

**Failing tests first.** `FlattenNestTests` (header cites `builder.test.ts`):
`A_probe_added_after_flatten_runs_inside_the_inner_loops_and_after_nest_runs_once` (an inner pipeline with a `ForkingProbe` at `Redirect` driving twice; a new probe at `PerHop`: entered twice under
`Flatten`, once under `Nest`; `PIPE-35`), `Flatten_copies_policies_transport_and_client_options` , `Nest_starts_empty_with_the_pipeline_as_its_transport`,
`Build_without_a_seed_throws_InvalidOperationException`, `Adding_a_second_pillar_policy_after_flatten_follows_PIPE_5`.

**Production.** `static PipelineBuilder Flatten(HttpPipeline)` copies the inner entries, terminal transport and client options into a fresh builder (it cannot collide; later adds follow `PIPE-5`);
`static PipelineBuilder Nest(HttpPipeline)` starts empty with the pipeline as its transport; the builder gains the optional seeded transport and options, and a parameterless `Build()` that throws
when nothing is seeded (P4c-11). Add the `ContextChainWiringTests` nested case from task 2.4 here (`Nest`-built outer over an inner pipeline: no store entry after the response's dispose). **PublicAPI:** `Flatten`, `Nest`, `Build()`. **IDs:** `PIPE-35`. **Verify:** V-fast `FlattenNestTests`.

### Task 3.4 — The bridge end-to-end tests (`PIPE-33`, `PIPE-34`)

**Failing tests first.** `PipelineBridgeTests`: `A_sync_pipeline_bridged_to_async_runs_as_one_unit_on_the_given_scheduler` (`RecordingTaskScheduler` counts one `QueueTask` for a five-policy pipeline;
`Assert.Same` on the options at the transport; `PIPE-33`), `An_async_pipeline_bridged_to_sync_preserves_options_and_surfaces_the_original_exception` (`PIPE-34`, `ASYNC-13`).
Cite 2b's `SyncToAsyncBridgeTests` and `AsyncToSyncBridgeTests` in the checklist; no production change expected (pins). **IDs:** `PIPE-33`, `PIPE-34`. **Verify:** V-fast `PipelineBridgeTests`.

### Task 3.5 — Close-out (PR 3)

`CHANGELOG.md`: `### Added` for the seam implementation, options capture, `SendAsync<T>`/`Send<T>`, `Flatten`/`Nest`/`Build()`; `### Changed` **Breaking** for items 9 and 10 (and the removed `= default` on the `DexpaceClientOptions` overloads). Run **V-gate**, including the AOT smoke publish (task 3.1's caller edits).
**Commit:** `feat!: phase 4c HttpPipeline as a transport, options capture, Flatten and Nest (PIPE-9, PIPE-17, PIPE-26, PIPE-27, PIPE-31, PIPE-33..PIPE-35)`.

---

## PR 4 — `ErrorMappingPolicy`

**Gate: PR 2 and 4b merged.** Row: `PIPE-37` (and the ⏳ 4c clauses of `BODY-30`, `BODY-31`, `HTTP-52`; cited work on `RECOV-15`, `RECOV-16`, `XCUT-8`).

### Task 4.1 — Pre-flight: re-derive 4b's mapping surface (no commit)

As task 2.1, for `ErrorMappingStep`, `ErrorBodyBuffer`, `ResponseRecoveryChain.Apply(Async)`, the `Outcome` constructors and dispatch, and `Response.EnsureSuccessAsync` after 4b's re-home. **Fallback
(P4c-17, if the lead keeps P3a-13's routing):** 4b has not built the buffer or the step. Then task 4.2 grows by `ErrorBodyBuffer` (sync `Capture` and async `CaptureAsync` over `StreamCopy.DrainUpTo(Async)`,
disposing the original through `Disposal`, returning a replayable copy; `ErrorBodyBufferTests` cited by `RECOV-16`'s row) and the `EnsureSuccessAsync` re-home, and the policy calls the buffer directly.
The S8 class stays unedited either way. **Verify:** `dotnet build Dexpace.Sdk.sln --configuration Release` green.

### Task 4.2 — `ErrorMapping.ToException` (`XCUT-8`)

**Failing tests first.** `ErrorMappingFactoryTests`: `A_non_error_status_is_rejected` (`[Theory]` over 100, 199, 200, 204, 308, 399, 600, 999: `ArgumentException`), `A_buffered_error_response_maps_to_HttpResponseException`
(400, 404, 500, 599 carry the response as given), `The_public_HttpResponseException_constructor_is_unchanged` (reflection pin).

**Production.** New `Errors/ErrorMapping.cs` (internal static): `ToException(Response)` is the one place core turns a buffered error response into `HttpResponseException`, throwing `ArgumentException`
for a status outside 400..599. Route 4b's `ErrorMappingStep` and both `EnsureSuccess` forms through it: a **minimal edit to 4b's merged code**, one call site each, flagged in the PR description
(design, `XCUT-8` row). `EnsureSuccessErrorMappingTests` (S8) is not edited and stays green; run it before and after. **IDs:** cites `XCUT-8`, `RECOV-15`, `RECOV-16`. **Verify:** V-fast `ErrorMappingFactoryTests`,
`EnsureSuccessErrorMappingTests`, 4b's `ErrorMappingStepTests`.

### Task 4.3 — `ErrorMappingPolicy` and the sync `EnsureSuccess` (`PIPE-37`; `BODY-30`, `BODY-31`, `HTTP-52` clauses)

**Failing tests first.** `ErrorMappingPolicyTests` (`Unit`; mirrors all six methods of `EnsureSuccessErrorMappingTests` through the policy form, plus):

- `It_runs_once_outside_the_redirect_loop_and_sees_the_final_response` (a 307 then 404 through a pipeline with `RedirectPolicy`: one invocation, the 404 mapped; `PIPE-37`)
- `A_non_error_status_is_returned_untouched` (`[Theory]` over 100, 199, 200, 204, 300–308, 399, 600, 999; `TrackingResponseBody` neither opened nor disposed; the fold is not entered; `PIPE-37`, `BODY-31`)
- `An_error_response_is_mapped_with_a_replayable_buffered_copy` (`BODY-31`)
- `A_response_with_an_empty_replayable_body_is_mapped_without_a_drain` (`BODY-30`, P4c-18: the exception carries the response as it is; no read issued)
- `The_policy_is_a_PerCall_stage_and_the_chain_is_static_readonly` (`PIPE-11`)
- `The_original_exception_instance_and_stack_surface` (`RECOV-10`)
- `Sync_and_async_agree` (both entry points)

`ResponseEnsureSuccessSyncTests` (or `EnsureSuccessErrorMappingTests`' sibling `Unit` class, never the Security file): `[Theory]` over `EnsureSuccessAsync` and the new sync `EnsureSuccess`, the same six behaviours.

**Production.** New `Pipeline/Policies/ErrorMappingPolicy.cs`: `public sealed class`, `Stage => PipelineStage.PerCall`, `ProcessCoreAsync(…, bool async)` per position G pseudo-code over 4b's
`static readonly` response chain (`[ErrorMappingStep.Instance]`); no `Outcome` in any signature; not added to `CreateDefault` (P4c-18). `Response.EnsureSuccess(CancellationToken = default)` over
`ErrorBodyBuffer.Capture`, routed through `ErrorMapping.ToException`. XML docs state the status predicate and name the S8 class.

**PublicAPI:** `ErrorMappingPolicy` (constructor, `Stage`, `ProcessAsync`, `Process`), `Response.EnsureSuccess`. **IDs:** `PIPE-37`; closes `BODY-30`/`BODY-31`/`HTTP-52` ⏳ 4c.
**Verify:** V-fast `ErrorMappingPolicyTests`, `ResponseEnsureSuccessSyncTests`, `EnsureSuccessErrorMappingTests`.

### Task 4.4 — Close-out (PR 4)

`CHANGELOG.md` `### Added`: `ErrorMappingPolicy`, `Response.EnsureSuccess`. Run **V-gate**. **Commit:** `feat: phase 4c ErrorMappingPolicy and sync EnsureSuccess (PIPE-37)`.

---

## PR 5 — The preset, `CreateDefault`, `CreateEmpty`

**Gate: PR 3.** Rows: `PIPE-24`, `PIPE-32`, `PIPE-39` (and the wiring of `RECOV-32`/`RECOV-33`).

### Task 5.1 — `AddStandardResilience` (`PIPE-24`)

**Failing tests first.** `ResiliencePresetTests` (header cites the Ruby 4c design's `PIPE-23`/`PIPE-24` cases):
`The_preset_installs_nothing_when_any_target_pillar_is_occupied` (`[Theory]` over each of `Operation`, `Redirect`, `Retry`, `Diagnostics` pre-occupied; entries unchanged),
`The_preset_fills_empty_pillars_and_leaves_non_pillar_stages_alone`, `The_preset_checks_all_four_before_installing_any`.

**Production.** `PipelineBuilder.AddStandardResilience(TimeProvider? timeProvider = null, ILogger? logger = null)` installs `OperationPolicy`, `RedirectPolicy`, `RetryPolicy` and `InstrumentationPolicy`
into empty pillars only (P4c-19), through `CheckPillar` for all four first, committing none when any is occupied (all-or-nothing, as `PIPE-23`). **PublicAPI:** the member. **IDs:** `PIPE-24`.
**Verify:** V-fast `ResiliencePresetTests`.

### Task 5.2 — `CreateDefault` recomposed and `CreateEmpty` (`PIPE-39`; `RECOV-32`, `RECOV-33` wiring)

**Failing tests first.** `DexpacePipelineTests.CreateDefault_installs_the_standard_pillars_and_the_per_call_defaults_in_order` (presence, type and stage only: `Operation`, `PerCall` idempotency and client
identity, `Redirect`, `Retry`, `PerAttempt` set-date, optional `Auth`, `Diagnostics`; defaults and behaviour are 4b's), `ResiliencePresetTests.CreateEmpty_forwards_straight_to_the_transport` (`PIPE-39`; the request
and options reach the transport by identity).

**Production.** `DexpacePipeline.CreateDefault` becomes: a new builder + the `PerCall` defaults (4b's parameterless `IdempotencyPolicy`/`ClientIdentityPolicy`) + `SetDatePolicy` at `PerAttempt` + `AddStandardResilience` + the
optional auth policy; same parameters, same order as the as-built remarks. New `CreateEmpty(IAsyncHttpClient)`: a step-less pipeline. The method remarks document that the async backoff is `Task.Delay` over the `TimeProvider`
and the sync wait a genuine blocking wait (`PIPE-39`'s moot clause). **PublicAPI:** `CreateEmpty`. **IDs:** `PIPE-39`; cites `RECOV-32`, `RECOV-33`. **Verify:** V-fast `DexpacePipelineTests`, `ResiliencePresetTests`.

### Task 5.3 — Both standard pipelines follow a redirect (`PIPE-32`; `REDIR-25` work)

**Failing test first.** `ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect` (a 307 then 200 through `CreateDefault`, sent with `SendAsync` and with `Send`; both return the 200 and the
transport saw two requests). The assertion is a pin on the new composition (the as-built async path already installs `RedirectPolicy`); prove it can fail by temporarily removing `RedirectPolicy` from the preset.
S3's `RedirectCredentialHygieneTests` and SystemNet's `RedirectWireTests` stay green and unedited. **IDs:** `PIPE-32` (documentation and 🚫 clauses; §10 text lands in 6.4); cites `REDIR-25`. **Verify:** V-fast
`ResiliencePresetTests`, then the Security category.

### Task 5.4 — Close-out (PR 5)

`CHANGELOG.md` `### Added`: `AddStandardResilience`, `CreateEmpty`; `### Changed`: `CreateDefault` is built from the preset (no behaviour change beyond items 3 and 7). Run **V-gate**. **Commit:**
`feat: phase 4c standard resilience preset, CreateDefault and CreateEmpty (PIPE-24, PIPE-32, PIPE-39)`.

---

## PR 6 — Close-out

**Gate: PRs 1–5 merged.** The docs close the sub-phase (roadmap step 7). Rows: all 40 (closing).

### Task 6.1 — NativeAOT smoke over the new surface (`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`)

`RunAllAsync` calls one more check, `CheckPipelineRework()`, in the existing style with its `Expect` helper: a sync `Send` through `CreateDefault` over the existing in-process handler; an `ErrorMappingPolicy` mapping a 404
into `HttpResponseException` with a readable buffered body; an `HttpPipeline` used as an `IAsyncHttpClient` under `PipelineBuilder.Nest`. No reflection; `AotSmoke` gets no `InternalsVisibleTo`. A trim/AOT warning means the
source is fixed, not the smoke. **Check:** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` prints "all checks passed".

### Task 6.2 — User documentation

New `docs/sdk-documentation/pipelines.md` (the directory holds `bodies.md`, `http.md`, `io.md`, `seams.md`), opening "As built by phase 4c, written against source on <date>". Content: the stage table and the loop each
stage sits in; the request-in/response-out signature and writing a policy (sync and async twins, `Process` default bridge); `PipelineContext` and the typed property keys; the builder (collisions, `Prepend`, bulk, surgical edits,
`Flatten`/`Nest`); `HttpPipeline` as a transport, the two kinds of options, the kept `DexpaceClientOptions` overloads and their future; `ErrorMappingPolicy` versus `EnsureSuccess`; the async-redirect asymmetry; the sync-path
residuals (⏳ 8b, 6c); the migration table for the ten breaking changes. Records Ruby's absent `pipelines.md`. Cite requirement IDs; do not copy design §5.1. `docs/README.md`'s ownership table gains the row the probe asks for.
**Verify:** the probe's `links` check.

### Task 6.3 — The checklist

Write `docs/work/mvp/phase4/phase4c/<date>-phase4c-pipeline-checklist.md` (the housekeeping `apply` step files the design and this plan beside it, task 6.6) from what was built: **40 rows**, phase-1 legend, mirroring the design's
decision table and this plan's checklist section: 36 ✅ outright, `PIPE-2` and `PIPE-3` ✅ by stated reading (P4c-6, P4c-7), `PIPE-28` ✅; ⏳ 8b, `PIPE-32` ✅ (documentation); 🚫 (`async-redirect-pillar`), `PIPE-30`, `PIPE-33`, `PIPE-34`
✅ with their residual in the existing §10 entries 12 and 8. Include the "existing assertions changed" table (expected: the `Add`-twice and cross-stage cases of `PipelineBuilderTests`; the `PerCall` token and two policy bodies of
`ReDriveRequestIsolationTests`), the unedited-Security list with the diff evidence of convention 5, the cross-owner table's cited tests, and the deviation ledger P4c-6, P4c-7, P4c-12, P4c-13, P4c-18, P4c-20 as built.

### Task 6.4 — Dated corrections, roadmap note, first-release

Frozen documents change only by dated correction. In `docs/sdk-design-dotnet/`:

- **§5.1** (`05-pipeline-architecture.md`): the **As built** line; `PipelineContext`'s member list as built (P4c-4); the builder reads `Stage` once (P4c-8); cross-stage errors are `ArgumentException` (P4c-10);
  `ErrorMappingPolicy` wraps 4b's step (P4c-17); the empty-body reading of `BODY-30` (P4c-18).
- **§5.3:** the **As built** line; the sync terminal choice (P4c-13); the options capture at `Build` (P4c-11).
- **§10 entry 14** (`10-deliberate-deviations-from-the-reference-contract.md`): the topic label `async-redirect-pillar` and the as-built note, **using the design's proposed text verbatim** (design position H) with the
  date filled in; the heading line gains `*Topic label `async-redirect-pillar`, added by dated correction, <date> (phase 4c).*` (the 3a precedent on entry 4).
- **§11**: new items at the next free number (re-read the file immediately before writing; 4a and 4b may have added items): `Operation` as a sixth singleton outside `PIPE-2`'s pre-redirect slot (P4c-6) and `PIPE-3`'s
  absent post-pillar slots (P4c-7).
- **§12**: the `PIPE` row's notes that `PIPE-1`, `PIPE-7`, `PIPE-13` and `PIPE-39` are not cited by ID are closed.

Append to the roadmap: the Phase List row 4's `sdk-design refs` cell gains this design's link (appended, never replacing), and a dated Phase Status Note that records the 4a → 4b → 4c dependency edge (P4c-2, as the
roadmap's ordering rule requires), the six PRs, the rulings the lead accepted or changed (P4c-2, P4c-6, P4c-15, P4c-17, P4c-22) and the hand-offs: 5a (immutable `DexpaceClientOptions`, the per-call overloads, `BlockingWait`),
5b/5c (`InstrumentationPolicy`, the operation span), 6a (`RetryPolicy` on `ProcessCoreAsync`, re-classification via `ErrorBodyBuffer`), 6b (`RedirectPolicy` over `SeedRequest`, `StripSensitiveHeadersOnCrossOrigin`), 6c
(`AccessTokenCache` sync path), 7c (`HttpPipeline` in `Pageable`), 8b (the real sync terminal). `docs/first-release.md`'s "Behavioural asymmetries a consumer must know" gains its first entry, `async-redirect-pillar`.
Phase 1 checklist: S6's and S8's ⏳ 4c clauses point at `PIPE-16`, `PIPE-37` and the classes named in convention 5. 3b checklist: `BODY-30`, `BODY-31`, `HTTP-52` lose their ⏳ 4c half, citing `ErrorMappingPolicyTests`
and 4b's `ErrorBodyBufferTests`. `CLAUDE.md`: the `Pipeline/` layout line and "What is genuinely unbuilt" (drop 4c). `src/Dexpace.Sdk.Core/README.md`: the `Pipeline` row. If the implementation found anything the
knowledge corpus should hold, record it as a note under `docs/knowledge/notes/` (never edit `harvested/`). **Verify:** the probe's `links` and `citations` checks.

### Task 6.5 — Close-out

`CHANGELOG.md` `### Added`: "`docs/sdk-documentation/pipelines.md`; the AOT smoke covers the sync pipeline, `ErrorMappingPolicy` and `HttpPipeline` as a seam." Run **V-gate** and the coverage gate with its self-test.
**Commits:** `test: NativeAOT smoke over the pipeline rework`, then `docs: phase 4c checklist, user page and dated corrections`.

### Task 6.6 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 4c            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 4c --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix whatever the final probe reports (counts in `CLAUDE.md`/`README.md`, package READMEs, broken links, misattributed IDs). `apply --write` performs `git mv` only; the plan authorises no commit beyond those in 6.5 and **no push**.

---

## Checklist: one row per owned ID

Exactly 40 rows (`PIPE-1`–`PIPE-40`). **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 6.3 records what was built. "Pin" means the test already passes on the as-built
behaviour and is proven able to fail (convention 1). ⏳ marks a clause with a later owner.

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `PIPE-1` | MUST | 1 | 1.3 | ✅ | `StageOrderTests.One_probe_per_stage_runs_in_stage_order_whatever_the_insertion_order` |
| `PIPE-2` | MUST | 1, 2 | 1.1, 2.11 | ✅ (P4c-6) | `StageOrderTests.A_per_call_probe_runs_once_and_sees_the_final_response…`, `PerCall_runs_outside_redirect…` |
| `PIPE-3` | SHOULD | 1 | 1.1 | ✅ (P4c-7, met in part by reading) | `StageOrderTests.Stage_values_are_strictly_increasing_and_sparse` |
| `PIPE-4` | MUST | 1 | 1.1, 1.3 | ✅ | `PillarRuleTests.Each_pillar_admits_one_policy`, `PipelineStageFactsTests` |
| `PIPE-5` | MUST | 1 | 1.3 | ✅ | `PillarRuleTests.A_second_distinct_policy_on_a_pillar_fails_at_add_naming_both_types` |
| `PIPE-6` | MUST | 1 | 1.3 | ✅ | `PillarRuleTests.Two_value_equal_policies_are_distinct` |
| `PIPE-7` | MUST | 1 | 1.3, 1.4 | ✅ | `NonPillarOrderTests.Append_adds_to_the_tail_and_prepend_to_the_head` |
| `PIPE-8` | MUST | 1 | 1.1, 1.3 | ✅ | `PillarRuleTests.A_policy_reporting_an_undefined_stage_is_rejected` |
| `PIPE-9` | MUST | 3 | 3.1 | ✅ | `EmptyPipelineTests.An_empty_pipeline_passes_request_options_and_token_to_the_transport` |
| `PIPE-10` | MUST | 2 | 2.2, 2.4, 2.11 | ✅ | `ConcurrencyTests.Concurrent_calls_share_no_per_call_state` |
| `PIPE-11` | MUST | 2 | 2.2, 2.5, 2.11 | ✅ | `ConcurrencyTests.Every_shipped_policy_holds_only_readonly_instance_fields` |
| `PIPE-12` | MUST | 2 | 2.3 | ✅ | `PolicyContractTests.A_short_circuit_returns_its_synthetic_response_and_reaches_nothing_downstream` |
| `PIPE-13` | MUST | 2 | 2.3 | ✅ | `RunnerTests.Next_dispatches_to_the_transport_after_the_last_policy_with_the_callers_options` |
| `PIPE-14` | MUST | 2 | 2.3 | ✅ | `PolicyContractTests.A_substituted_request_reaches_every_downstream_policy_and_the_transport` |
| `PIPE-15` | MUST | 2 | 2.3, 2.11 | ✅ (P4c-12) | `ForkTests.Driving_the_same_runner_twice_re_runs_the_whole_downstream_tail` |
| `PIPE-16` | MUST | 2 | 2.2, 2.3, 2.7, 2.8, 2.10 | ✅ (S6 structural) | `ForkTests`; `ReDriveRequestIsolationTests` (Security, moved) |
| `PIPE-17` | MUST | 2, 3 | 2.2, 3.1 | ✅ | `OptionsFlowTests.The_same_request_options_instance_reaches_every_policy_every_fork_and_the_transport` |
| `PIPE-18` | MUST | 1 | 1.4 | ✅ | `SurgicalEditTests.Insert_relative_to_an_anchor_in_another_stage_is_rejected` |
| `PIPE-19` | MUST | 1 | 1.4 | ✅ | `SurgicalEditTests.A_cross_stage_replace_is_rejected` |
| `PIPE-20` | MUST | 1 | 1.4 | ✅ (pin) | `SurgicalEditTests.Remove_deletes_every_instance_and_preserves_order` |
| `PIPE-21` | MUST | 1 | 1.4 | ✅ (pin) | `SurgicalEditTests.A_missing_anchor_throws_InvalidOperationException_naming_T` |
| `PIPE-22` | MUST | 1 | 1.3, 1.4 | ✅ | `SurgicalEditTests.Edited_order_equals_order_built_from_scratch` |
| `PIPE-23` | MUST | 1 | 1.4 | ✅ | `BulkTests.A_colliding_batch_leaves_the_builder_unchanged` |
| `PIPE-24` | MUST | 5 | 5.1 | ✅ | `ResiliencePresetTests.The_preset_installs_nothing_when_any_target_pillar_is_occupied` |
| `PIPE-25` | MUST | 1 | 1.5 | ✅ | `HttpPipelineTests.Policies_is_an_ordered_read_only_view` |
| `PIPE-26` | MUST | 3 | 3.1 | ✅ | `PipelineAsTransportTests.A_pipeline_stands_in_for_a_transport_and_threads_options_through`; allow-list reason "PIPE-26" |
| `PIPE-27` | MUST | 3 | 3.1 | ✅ | `PipelineAsTransportTests.Disposing_the_pipeline_twice_never_disposes_the_transport` |
| `PIPE-28` | MUST | 2 | 2.3, 2.5–2.9, 2.11 | ✅; ⏳ 8b (the real sync terminal) | `SyncPathTests.Sync_and_async_sends_visit_the_same_policies_in_the_same_order`, `…The_sync_send_never_calls_an_async_member_of_a_shipped_policy` |
| `PIPE-29` | MUST | 2 | 2.3, 2.5, 2.6, 2.11 | ✅ | `AsyncErrorModelTests.A_shipped_policy_reports_a_failure_as_a_faulted_task` |
| `PIPE-30` | MUST | 2 | 2.3, 2.11 | ✅ (§10 entry 12 residual) | `AsyncErrorModelTests.A_synchronous_throw_from_a_policy_becomes_a_faulted_task`, `An_out_of_memory_exception_is_not_caught_by_any_sdk_frame` |
| `PIPE-31` | MUST | 3 | 3.2 | ✅ | `TerminalMappingTests` (sync and async) |
| `PIPE-32` | MUST | 5, 6 | 5.3, 6.4 | ✅ (documentation); 🚫 (`async-redirect-pillar`, §10 entry 14) | `ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect` |
| `PIPE-33` | MUST | 3 | 3.4 | ✅ (§10 entry 8 for the interrupt clause) | 2b `SyncToAsyncBridgeTests`; `PipelineBridgeTests.A_sync_pipeline_bridged_to_async_runs_as_one_unit_on_the_given_scheduler` |
| `PIPE-34` | MUST | 3 | 3.4 | ✅ (§10 entry 8) | 2b `AsyncToSyncBridgeTests`; `PipelineBridgeTests.An_async_pipeline_bridged_to_sync_preserves_options_and_surfaces_the_original_exception` |
| `PIPE-35` | SHOULD | 3 | 3.3 | ✅ | `FlattenNestTests.A_probe_added_after_flatten_runs_inside_the_inner_loops_and_after_nest_runs_once` |
| `PIPE-36` | SHOULD | 2 | 2.11 | ✅ | `PillarRuleTests.Every_shipped_pillar_policy_locks_its_stage` |
| `PIPE-37` | MUST | 4 | 4.3 | ✅ | `ErrorMappingPolicyTests.It_runs_once_outside_the_redirect_loop_and_sees_the_final_response`, `…A_non_error_status_is_returned_untouched` |
| `PIPE-38` | MUST | 1 | 1.4 | ✅ | `BulkTests.AddRange_keeps_batch_order_and_PrependRange_reverses_it` |
| `PIPE-39` | SHOULD | 5 | 5.2 | ✅ | `ResiliencePresetTests.CreateEmpty_forwards_straight_to_the_transport`, `DexpacePipelineTests.CreateDefault_installs_…` |
| `PIPE-40` | MUST | 2 | 2.7, 2.8, 2.11 | ✅ (pinned, `DisposeQuietlyAsync`) | `ReDriveLifecycleTests` |

Count: `PIPE-1`–`PIPE-40` = **40 rows**, all mapped (36 MUST, 4 SHOULD: `PIPE-3`, `PIPE-35`, `PIPE-36`, `PIPE-39`). The design's PR column for `PIPE-2` is 1, 2 and for `PIPE-17` 3; this plan lists the tasks where each
part lands (R1 moves `PIPE-2`'s enum half to task 1.1).

### Work on other owners' rows (no checklist row in 4c)

These carry no exit mark in 4c's checklist (3a precedent with `HTTP-36`/`HTTP-52`). 4c's tests are cited by the owner's row.

| ID (owner) | Work | Task | Evidence the owner cites |
|---|---|---|---|
| `RECOV-15` (4b) | `ErrorMappingPolicy` installs 4b's step at `PerCall` (P4c-17) | 4.3 | `ErrorMappingPolicyTests`; 4b's `ErrorMappingStepTests` |
| `RECOV-16` (4b) | None of the building; sync `EnsureSuccess` uses 4b's buffer (fallback: 4c builds it, 4.1) | 4.3 | 4b's `ErrorBodyBufferTests` |
| `RECOV-32`, `RECOV-33` (4b) | Both policies move onto the new signature (2.5) and are installed in `CreateDefault` (5.2) | 2.5, 5.2 | `DexpacePipelineTests.CreateDefault_installs_…` (presence, type, stage only) |
| `BODY-30`, `BODY-31`, `HTTP-52` (3b; ⏳ 4c) | The policy form of the mapping; the empty-body clause | 4.3 | `ErrorMappingPolicyTests`; `EnsureSuccessErrorMappingTests` |
| `RETRY-44` (6a) | Structural: there is no shared in-flight request | 2.7, 2.10 | `ReDriveRequestIsolationTests`, `ForkTests` |
| `REDIR-11`, `REDIR-24` (6b) | The seed request on the call-scoped context; the `"dexpace.auth.origin"` bag key retires | 2.8, 2.9 | `SeedOriginTests`; `RedirectCredentialHygieneTests`, `AuthHttpsGuardTests` (unedited) |
| `REDIR-25` (6b) | The reversal recorded under §10 `async-redirect-pillar`; the preset serves both paths | 5.3, 6.4 | `ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect` |
| `XCUT-8` (phase 10) | `ErrorMapping.ToException`, the guarded factory | 4.2 | `ErrorMappingFactoryTests.A_non_error_status_is_rejected` |
| `SEAM-2` (2b) | `HttpPipeline` public exemption and allow-list entry | 3.1 | `SeamImplementationArchitectureTests` |
| `SEAM-28` (2b; 4a attaches) | The operation id threaded into the dispatch → request promotion | 2.4 | 4a's test; `ContextChainWiringTests` |

---

## Traceability: PR → rows → tasks

| PR | Gate | Rows | Tasks |
|---|---|---|---|
| 1 | none | `PIPE-1`, `PIPE-3`–`PIPE-8`, `PIPE-18`–`PIPE-23`, `PIPE-25`, `PIPE-38`; enum half of `PIPE-2` | 1.1–1.6 (6) |
| 2 | PR 1; 4a and 4b merged | `PIPE-2`, `PIPE-10`–`PIPE-16`, `PIPE-17` (carriage), `PIPE-28`–`PIPE-30`, `PIPE-36`, `PIPE-40` | 2.1–2.12 (12) |
| 3 | PR 2 | `PIPE-9`, `PIPE-17`, `PIPE-26`, `PIPE-27`, `PIPE-31`, `PIPE-33`–`PIPE-35` | 3.1–3.5 (5) |
| 4 | PR 2; 4b merged | `PIPE-37` | 4.1–4.4 (4) |
| 5 | PR 3 | `PIPE-24`, `PIPE-32`, `PIPE-39` | 5.1–5.4 (4) |
| 6 | PRs 1–5 | all 40 (closing) | 6.1–6.6 (6) |
| **Total** | | | **37 tasks** |

Rows by the PR that first lands them: PR 1: 15 (`PIPE-1`, `PIPE-3`–`PIPE-8`, `PIPE-18`–`PIPE-23`, `PIPE-25`, `PIPE-38`); PR 2: 14 (`PIPE-2`, `PIPE-10`–`PIPE-17`, `PIPE-28`–`PIPE-30`, `PIPE-36`, `PIPE-40`); PR 3: 7 (`PIPE-9`, `PIPE-26`, `PIPE-27`, `PIPE-31`, `PIPE-33`–`PIPE-35`); PR 4: 1 (`PIPE-37`); PR 5: 3 (`PIPE-24`, `PIPE-32`, `PIPE-39`). 15 + 14 + 7 + 1 + 3 = 40. `PIPE-2` and `PIPE-17` finish in a later PR (task 2.11 and task 3.1 respectively). The authoritative count is the 40-row checklist above.

PR 1 is independent of 4a and 4b and may land at any time. PRs 3 and 4 both need PR 2 and are independent of each other; PR 5 needs PR 3 (the builder, the preset's `CreateDefault` over `HttpPipeline` options) and not PR 4
(`ErrorMappingPolicy` is not in `CreateDefault`, P4c-18). Every PR stays green in any order these gates allow.

---

## Findings while planning

Items checked against the repository at `0332cef` on 2026-10-05, each with what the plan did.

1. **F1 — The design's PR table and its PR 1 tests disagree about the stage enum (R1).** `PIPE-1`/`PIPE-3`/`PIPE-4` sit in PR 1, the enum change in PR 2. The plan moves the enum, the `PerCall` consumers and the S6
   enum token into task 1.1. The spec now agrees (landing, Breaking and Security tables); no longer a divergence.
2. **F2 — `ReDriveRequestIsolationTests` is edited in two PRs.** Task 1.1 (the enum token) and task 2.10 (two policy bodies and one added fact). Convention 5's V-gate line expects exactly one file in the Security diff and the
   hunks enumerated; the design's "Keeping the `Security` classes green" table remains the authority and P4c-22 is open for the lead.
3. **F3 — `HttpPipeline` has two sync-over-async sites today and the design wants three confined ones.** The as-built `Send` pragma goes (task 2.4); `SyncPath.GetCompletedResult`, `HttpPipelinePolicy.Process`'s default and
   `AuthorizationPolicy.GetCredential`'s default replace it. R4 and R5 separate the completed-only helper from the genuinely blocking default so a third-party policy that suspends is bridged, not rejected.
4. **F4 — `RetryPolicy` has no `MA0051` waiver and its `ProcessAsync` is already near 70 lines.** Task 2.7 adds one citing 6a's rewrite, as `RedirectPolicy` and `InstrumentationPolicy` already do.
5. **F5 — `DexpacePipelineTests`, `PageableTests`, `PaginationStrategiesTests` and the SystemNet `RedirectWireTests` call `SendAsync(Request, DexpaceClientOptions, CancellationToken)`.** Those call the three-argument form with an explicit token, which the kept overload (P4c-15, fact 9) keeps compiling. The two-argument callers
   (`SmokeChecks.cs:112`, `InstrumentationPolicyTests.cs:49`, two READMEs) do break once the token default goes (R6) and are edited in task 3.1.
6. **F6 — `PipelineContext.Create` cannot live in `TestSupport`** (internal access). `TestContexts` lives in `Core.Tests` (R2); doubles that need only the public `ProcessAsync` surface live in `TestSupport`.
7. **F7 — `RecordingSyncTransport` exists (2b) but implements only `IHttpClient`**, which `Build(IAsyncHttpClient)` cannot take. `PIPE-28`'s sync-only fake is `SyncFirstTransport` (dual-interface, `ExecuteAsync` throws); task 1.2 also gives `ScriptedTransport` and `RecordingTransport` the sync `Execute`.
8. **F8 — Ruby's pipeline tests and `pipelines.md` are absent locally** (as 2a–3b found). The plan ports from Node's `packages/core/src/pipeline/` and the Ruby 4c design's testing strategy (R8) and records the absence in
   task 6.2.
9. **F9 — Names from 4a and 4b are unverified.** Tasks 2.1 and 4.1 are the plan's gates: each re-derives the call sites from merged code and stops, rather than working around, a contract the merged code does not meet.
10. **F10 — `HttpPipeline` is the first public core type to implement a seam.** `SeamImplementationArchitectureTests` rejects a public implementer before consulting the allow-list (fact 11); task 3.1 adds the exemption,
    not only the entry.
