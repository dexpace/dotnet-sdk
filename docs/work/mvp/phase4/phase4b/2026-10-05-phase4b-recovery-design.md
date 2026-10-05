# Phase 4b — Recovery Chain: Design

**Status:** Draft, for review. Written 2026-10-05 against `main` at `0332cef` (2a, 2b, 3a and 3b merged). The scope
authority is the roadmap's Phase 4 card and Phase List row 4 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`).
No phase 4 segmentation design exists; [P4b-1](#rulings) records the row partition in its place and is **open for the
lead**. The structural precedent is the 3a design (`docs/work/mvp/phase3/phase3a/2026-10-02-phase3a-io-design.md`).

**What this document is.** The sub-phase design for 4b. It gives one decision per requirement row (34 rows), the shape of
every type 4b adds or changes, argued positions on what the card and design §5.2 leave open, the places where a check on
the pinned runtime changed an obvious answer, a migration plan from the as-built code, the breaking changes, a landing
order, the test strategy, the interface 4c builds on, and the rulings (`P4b-1`…`P4b-26`). The rulings double as the
deviation-ledger IDs (roadmap constraint 7). Nobody answered the brainstorming questions, so each question became a ruling;
the ones that need the lead's sign-off say **open for the lead**.

**What this document is not.** It is not the plan and not the checklist. It does not restate design §5.2's mapping of the
recovery chain, §3.7's lifecycle rules or §6.1's retry design; it cites them (roadmap constraint 9) and argues only what
they leave open or what verification overturned. It does not design 4a (context) or 4c (pipeline), which sibling
workflows are brainstorming at the same time; it states the interface it hands to 4c and the row partition it assumes.

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor | Kind | State at `0332cef` |
|---|---|---|
| Phase 0 (warnings as errors, `RS0016`/`RS0017`, `RS0030` with `BannedSymbols.txt`, `MA0051` at 70 lines, `CA2007` and `CA1031` on `src/`, `IsAotCompatible`, the AOT smoke consumer, the test partition and `TestCategoryTests`) | **dependency** | Met. 4b is written under every gate from its first line. |
| Phase 1, S7 and S8 | **dependency** | Met. `EnsureSuccessErrorMappingTests` (S8, `Security`) is the evidence `RECOV-15`/`RECOV-16` start from; 4b re-homes the drain under it and keeps the class **unedited** (constraint 5). `RetryPacingOverflowTests` (S7) is the evidence `RECOV-26`'s ⏳ row cites; 4b does not touch it. |
| 2b (the transport SPI with `RequestOptions` and a token on both forms) | **dependency** | Met. The dispatcher calls `IAsyncHttpClient.ExecuteAsync(Request, RequestOptions, CancellationToken)` and `IHttpClient.Execute(…)` as 2b shaped them. |
| 3b (the `Response`/`ResponseBody` dispose latches and the internal `Disposal`) | **dependency** | Met. `RECOV-12`'s "released exactly once" is discharged by 3b's latch (P4b-12), and 3b hands 4b the `Disposal` repoint (roadmap status note of 2026-10-03, "Hand-offs. 4b: …"). |
| 3a (`StreamCopy.DrainUpTo`/`DrainUpToAsync` over `PooledChunk`) | **dependency** | Met. `ErrorBodyBuffer` (P4b-16) moves the existing drain; it writes no second drain. |
| 4a (context) | **none** | The roadmap's segmentation rule calls phase 4's cut a convenience ("no `RECOV` or `PIPE` requirement consumes `CTX`"), and 4b confirms it: no 4b type names a 4a type, and no 4a type needs a 4b one. 4a and 4b may land in either order. |
| 4c (pipeline) | **4c depends on 4b**, not the reverse | 4c's `ErrorMappingPolicy` sits over 4b's `ErrorMappingStep` and the `Outcome` folds, and 4c ports 4b's two reworked policies to its new signature. 4b lands first and changes no pipeline type's shape (P4b-20). |
| A phase 4 segmentation design | **convenience**, with a stated substitute | Absent. The segmentation rule applies (phase 4 is the largest ID count yet and spans two chapters), but the card already fixes the 4a/4b/4c cut and names each build list. P4b-1 states the partition; it is open for the lead. |

---

## Governing documents, and the phase-start queries

- **Normative.** Every row's canonical text is its appendix-C row (`docs/product-spec/appendix-c-consolidated-normative-requirement-index.md`,
  lines 229–262). `docs/product-spec/08-execution-pipelines.md` §8.2 states `RECOV-1`–`RECOV-16` in condensed form; §8.3
  carries the two-layer prohibition and the sentence "the recovery layer is synchronous" (P4b-6). `RECOV-17`–`RECOV-34` are
  stated in appendix C **only** (design §11 item 33). Also read: `RETRY-13`, `RETRY-25`, `RETRY-34`, `RETRY-36`, `XCUT-8`,
  `XCUT-9`, `BODY-30`, `BODY-31`, `HTTP-52` (the rows 4b leans on but does not own).
- **Design.** §5.2 in full; §5.1's "Three shipped steps that are not pillars" and "The bounded error-body copy" paragraphs;
  §5.3 (one implementation per policy, the `bool async` core); §3.7 (`Disposal`, its two ways and "never a third"); §6.1
  (`RECOV-17`, `RECOV-34`, `RetryFacts`); §10 entries 7, 12, 13, 14; §11 items 10, 12, 19, 20, 33; §12's `RECOV` row.
- **Roadmap.** The Phase 4 card; coupling obligation 3 (`RETRY` needs `RECOV`; the engine is 6a's); the 3b status note's
  hand-offs; phase 1's S7/S8 rows and the "Keeps green" table; constraint 3's legend; constraint 5; constraint 7.
- **Styleguide.** `08-error-handling.md` (8.2 the `when` filter; 8.7 the closed `Result` and the Try-pattern; 8.8
  cancellation), `03-nullability-and-the-type-system.md` (`[NotNullWhen]`), `06-types-and-data-modeling.md` (closed
  hierarchies), `09-concurrency.md` (9.1 no blocking on async outside a documented bridge), `10-api-design.md` (10.1
  minimal surface), `13-resource-management.md` (13.4 never dispose what you do not own).
- **Siblings.** `nodejs-sdk@c0ff3fd` `packages/core/src/recovery/` (`outcome`, `request-chain`, `response-chain`,
  `orchestrator`, `release`, `cancellation`, `status-mapping`, `idempotency-key`, each with its `.test.ts`),
  `packages/core/src/config/client-identity-step{,.test}.ts` and `packages/core/src/suppress{,.test}.ts`, all local.
  `ruby-sdk`'s gems are **not** in the local clone (the checkout is the docs branch at `90075b1`, as 2a–3b found), so
  `gems/dexpace-core/test/dexpace/{recovery,outcome}/`, `each_cause_test.rb` and `suppressible_test.rb` cannot be read;
  their case lists come from `ruby-sdk/docs/work/mvp/phase4/phase4b/2026-09-08-phase4b-recovery-primitives-design.md`
  ("Testing strategy", R5–R8), which is local. `ruby-sdk/docs/sdk-documentation/recovery.md` does not exist in the clone.

| Query, run 2026-10-05 | Result |
|---|---|
| `scripts/knowledge --origin note --brief` | five notes (`CA1062`, `I` prefix, `Async` suffix, Shouldly, `LangVersion`); none touches recovery or errors |
| `scripts/knowledge --section conflicts --brief` | ten Conflicts, every one `[conformed]` or `[kept]` with a note; none open, so 4b inherits no conflict |
| `scripts/knowledge --prefix-info RECOV` | 34 IDs (`RECOV-1`..`RECOV-34`): 30 MUST, 3 SHOULD, 1 MAY; owning chapter `08-execution-pipelines.md`; 20 substantive, 0 roll-up only, 14 uncited |
| `scripts/knowledge --gaps RECOV` | `RECOV-18`–`RECOV-31` uncited: "appendix C is their only normative statement" |
| `scripts/knowledge --req RECOV-1,RECOV-2,RECOV-12,RECOV-32,RECOV-33,RECOV-34,RETRY-13` | design-role entries agree with §5.1/§5.2/§6.1: `pipeline/6ead43f9` (`RECOV-32`), `pipeline/e35ebb4c` (`RECOV-33`), `pipeline/e5afbdb3` (appendix-C-only), `retry-and-resilience/e364a690` (`RECOV-34`), `retry-and-resilience/c52c2728` (`RETRY-13`'s single calculator), `error-handling/9681dcb5` (no host suppressed facility), `error-handling/c4e054b9` (§10 entry 12). **`retry-and-resilience/302d143d`** states §5.2's "record hierarchy closed by a private constructor", which verified fact 1 shows is not closed (P4b-3) |
| `scripts/knowledge --req XCUT-9 --brief` | `error-handling/5fb5045b`, `error-handling/4054ee11`: reference identity, terminate on cycles |

**The reading budget.** `RECOV-17`–`RECOV-34` have no chapter body; their only statement is appendix C (lines 245–262).
4b read all eighteen rows. Fifteen of them (`RECOV-17`–`RECOV-30`, `RECOV-34`) are the 6a engine's and are carried as ⏳
rows; `RECOV-31` is a MAY carried with its `RETRY-38` twin (P4b-2); `RECOV-32` and `RECOV-33` are built here.

**One corpus finding, for the implementation to record.** `retry-and-resilience/302d143d` is false about the type it
describes (fact 1). Per the knowledge-lookup skill's step 6, a brainstorm writes no note; the plan's close-out task files a
`## Superseded` note in `docs/knowledge/notes/retry-and-resilience.md` that **Supersedes** `retry-and-resilience/302d143d`
once the implementation has confirmed the fact against its own build.

---

## Scope, and the phase 4 census

**4b owns 34 rows: `RECOV-1`–`RECOV-34`** (30 MUST, 3 SHOULD, 1 MAY).

| Disposition | IDs | Count | Levels |
|---|---|---|---|
| Built in 4b | `RECOV-1`–`RECOV-16`, `RECOV-32`, `RECOV-33` | 18 | 17 MUST, 1 SHOULD (`RECOV-9`) |
| ⏳ against 6a (the recovery-stack engine; roadmap card and coupling obligation 3) | `RECOV-17`–`RECOV-30`, `RECOV-34` | 15 | 13 MUST, 2 SHOULD (`RECOV-25`, `RECOV-30`) |
| ⏳ against 6a, with its `RETRY-38` twin (P4b-2, **open for the lead**) | `RECOV-31` | 1 | 1 MAY |
| **Total** | | **34** | 30 MUST, 3 SHOULD, 1 MAY |

`RECOV-32` and `RECOV-33` (the idempotency and client-identity policy defaults) are **4b's rows**. 4c does not own them; 4c
wires the policies into `DexpacePipeline.CreateDefault` and its rows cite 4b's (P4b-19).

**The census: 4a + 4b + 4c = 94.** The roadmap's Phase List row 4 places exactly three ID ranges in phase 4: ch.07 —
`CTX-1`–`CTX-20` (20); ch.08 §8.2 and appendix C — `RECOV-1`–`RECOV-34` (34); ch.08 §8.1 — `PIPE-1`–`PIPE-40` (40).
20 + 34 + 40 = **94**, the card's exit count, and the partition is one prefix per sub-phase:

| Sub-phase | Owns | Rows |
|---|---|---|
| 4a | `CTX-1`–`CTX-20` | 20 |
| 4b | `RECOV-1`–`RECOV-34` | 34 |
| 4c | `PIPE-1`–`PIPE-40` | 40 |
| **Phase 4** | | **94** |

No non-prefix row is placed in phase 4 by the roadmap, so none is added to any sub-phase's count. The rows the card and
its coupling notes *touch* belong elsewhere: `OBS-25`/`OBS-26` are 5c's (coupling obligation 1: 4a fixes the shape, 5c
owns the rows); `REDIR-11`, `REDIR-24` and `REDIR-25` are 6b's 28 (4c fixes the seed origin and records the `PIPE-32`/
`REDIR-25` reversal under `PIPE-32`'s row); `SEAM-28`'s attachment is 2b's row; `BODY-30`, `BODY-31` and `HTTP-52` are 3b's
rows. If either sibling design counts one of those as its own, the census exceeds 94; P4b-1 asks the lead to confirm the
partition.

**Also shipped by 4b without owning a new ID** (each cited by the row it serves):

- `ExceptionFacts.IsFatal` and `ExceptionFacts.EnumerateCauses` (§10 entry 12; `XCUT-9` is phase 10's row, `RETRY-25` 6a's).
- `ExceptionTrail.AddSuppressed`/`GetSuppressed` and `SdkException.Suppressed` (§10 entry 13; `RETRY-34` 6a's, `PAGE-13` 7c's,
  `SSE-29`/`SSE-36` 7b's).
- The `Disposal` repoint the 3b status note hands 4b (§3.7).
- The internal `ErrorBodyBuffer` (P4b-16, **open for the lead**: it reassigns a 3a/3b hand-off from 4c to 4b).
- The internal sync-path helper and one `BannedSymbols.txt` entry (P4b-7), which 4c reuses.

**Out of scope, explicitly.** No backoff calculator, no pacing-header parser, no retry engine, no retryability capability
(`IRetryableError` is 6a's, design §6.1), no `RetryFacts` change, no `RetryOptions` validation (`RECOV-34` is ⏳ 6a): any of
these would be a second copy `RETRY-13` forbids. No pipeline signature, stage, runner, `PipelineContext` or
`ErrorMappingPolicy` (4c's). No context type (4a's). No `XCUT-8` factory rejection (4c's, per phase 1's S8 row). No change to
`DexpacePipeline.CreateDefault`.

---

## Verified facts that shape the decisions

Each was verified on 2026-10-05 on the pinned SDK (10.0.401) with a throwaway program in the scratchpad, never in the
repository. Rows and rulings cite them by number.

1. **An abstract `record` closed by a private constructor is not closed.** The compiler synthesises a `protected` copy
   constructor `Outcome(Outcome original)` on a non-sealed record, and a non-sealed record may not make it private (C#
   requires it to be `public` or `protected`). An outside `public sealed record Evil : Outcome { public Evil() : base(new
   Outcome.Success("x")) { } }` compiled and ran, printing `Evil`. Design §5.2's claim that "no type outside `Outcome` can
   call the private constructor" is true of the declared constructor and false of the type.
2. **An abstract `class` with a private constructor and nested sealed subclasses is closed.** The same outside subclass
   fails with `CS0122: 'Outcome.Outcome()' is inaccessible due to its protection level`. A `switch` expression over the
   two nested types still reports `CS8509` (not exhaustive), as §5.2 says, so the discard arm stays.
3. **`CA1034` (nested public types) does not fire at `AnalysisLevel=latest-recommended`**, and `CA1031` does not fire on
   `catch (Exception ex) when (!IsFatal(ex))`. A filtered broad catch is gate-clean without a pragma.
4. **`Exception.Data` is `virtual`, and an override can return a read-only dictionary**, whose indexer then throws
   `NotSupportedException`. A helper that writes a suppressed trail into a *foreign* exception's `Data` can therefore throw,
   on exactly the error path where it must not.
5. **A `HashSet<Exception>` with the default comparer treats two distinct exceptions as one** when the type overrides
   `Equals`/`GetHashCode` (`Contains(b)` was `true` for a distinct `b`); with `ReferenceEqualityComparer.Instance` it was
   `false`. §5.2's reason for reference identity holds.
6. **`AggregateException.InnerException` is the same instance as `InnerExceptions[0]`.** A walk over both edges reaches
   the first inner exception twice; only the visited set stops it being yielded twice.
7. **`ExceptionDispatchInfo.Capture(ex).Throw()` rethrows the same instance** (`ReferenceEquals` true).
8. **`InsufficientMemoryException` is an `OutOfMemoryException`**, so "OOM and its subtypes" is one `is` test.
9. **A cancelled `CancellationTokenSource`'s token stays cancelled after its `OperationCanceledException` is stored in a
   value**; nothing about converting the exception to data touches the token (`RECOV-11`, §5.2's "free").
10. **A `ValueTask<T>` returned by an `async` method whose sync branch threw is `IsCompleted` and `IsFaulted`, and
    `.Result` rethrows the original exception** (its stack trace still names the throwing method). `ValueTask<T>.Result`
    is **not** in `BannedSymbols.txt` today (only `Task<T>.Result` and the awaiters' `GetResult` are), and no `src/` file
    reads `.Result` (grep at `0332cef`).

---

## Decisions, one per requirement row

**How to read the table.**

- **Exit** uses constraint 3's legend. A combined mark is used as 2b and 3b used it: each clause has its own mark and the
  row says which is which.
- **Tests** are `[Trait("Category", "Unit")]` in `tests/Dexpace.Sdk.Core.Tests` unless another category is named. New test
  classes live under `tests/Dexpace.Sdk.Core.Tests/Recovery/` (namespace `…Tests.Recovery`) and `…/Errors/`. Every chain,
  dispatcher and step test is a `[Theory]` over `bool async`, so the sync and async forms are proven by one test body.
- **PR** points to [the landing order](#landing-order).

| ID | Level | Exit | Decision | Tests | PR |
|---|---|---|---|---|---|
| `RECOV-1` | MUST | ✅ | `Outcome` is an abstract **class** with a private constructor and two nested sealed classes, `Outcome.Success(Response)` and `Outcome.Failure(Exception)` (P4b-3, fact 1). Accessors `IsSuccess`, `IsFailure`, `TryGetResponse(out Response)`, `TryGetError(out Exception)` with `[NotNullWhen(true)]` (P4b-4); one fold, `Match<T>(Func<Response,T>, Func<Exception,T>)`, invoking exactly one branch at most once | `OutcomeTests`: a reflection test that `Outcome` has no non-private constructor and that the assembly holds exactly two types deriving from it; the accessor pairs never both true; `Match` with counting delegates (one branch, one call); a seeded property test over generated outcomes (fold agrees with the predicates) | 2 |
| `RECOV-2` | MUST | ✅ | `RecoveryDispatcher` wraps the request chain and the transport call each in `try … catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` and turns the catch into `Outcome.Failure`; a `null` response from a transport is a `Failure(InvalidOperationException)` (P4b-8). Fatal exceptions are §10 entry 12's named exclusion | `RecoveryDispatcherTests`: a throwing **request step** and a throwing transport (sync throw and faulted task) each reach a recording recovery step as a `Failure` carrying the same instance | 4 |
| `RECOV-3` | MUST | ✅ | `RequestRecoveryChain` folds left to right over an array copied at construction; empty returns the input by reference; a throw aborts the rest and propagates (the dispatcher converts it) | `RequestRecoveryChainTests` | 3 |
| `RECOV-4` | MUST | ✅ | `ResponseRecoveryChain`'s response phase runs only while the outcome is a `Success`; a `Failure` skips the whole phase | `ResponseRecoveryChainTests` | 3 |
| `RECOV-5` | MUST | ✅ | Recovery steps run on every outcome, always, in order, observing the terminal outcome — including a `Failure` a response step just produced | `ResponseRecoveryChainTests`: a throwing response step followed by a recording recovery step | 3 |
| `RECOV-6` | MUST | ✅ | Response phase first, then recovery phase, declared order within each | `ResponseRecoveryChainTests`: one log, `r1 r2 c1 c2` | 3 |
| `RECOV-7` | MUST | ✅ | A throwing response step becomes a `Failure`, the remaining response steps are skipped, the recovery steps see it; nothing propagates out of `Apply`/`ApplyAsync` | `ResponseRecoveryChainTests` | 3 |
| `RECOV-8` | MUST | ✅ | A throwing recovery step becomes a `Failure` fed to the **next** recovery step; the chain's apply never throws except for a fatal exception (§10 entry 12, P4b-9). A `null` returned by a step is treated as a throw of `InvalidOperationException` naming the step type (P4b-8). The chain never checks the token itself, so a cancelled token cannot make apply throw | `ResponseRecoveryChainTests`: asserted on the returned `Outcome` and on the next step having run (never `Record.Exception` alone) | 3 |
| `RECOV-9` | SHOULD | ✅ | 4b ships no recovery step (the three shipped steps are request and response steps), so the SHOULD has no subject in core yet; `IRecoveryStep`'s XML docs state the preference and name `new Outcome.Failure(…)` as the way to honour it, and the chain demonstrably honours a returned `Failure` exactly as a thrown one minus the release (P4b-22). 6a's engine is the first core recovery step | `ResponseRecoveryChainTests.A_returned_failure_reaches_the_next_recovery_step` | 3 |
| `RECOV-10` | MUST | ✅ | `Dispatch`/`DispatchAsync` return the `Success` response, or rethrow the `Failure`'s exception with `ExceptionDispatchInfo.Capture(error).Throw()`: same instance, no wrapping, no substitution (fact 7, P4b-10) | `RecoveryDispatcherTests`: `Assert.Same` on a **constructed, never-thrown** exception returned by a recovery step, and on a transport's thrown one | 4 |
| `RECOV-11` | MUST | ✅ | Free on .NET (fact 9, §5.2): no wrapper re-asserts anything, because a cancelled token cannot be un-cancelled. Asserted on the **token**, not the exception (P4b-11) | `RecoveryDispatcherTests.Cancellation_survives_the_failure_conversion`: cancel a source, the transport throws `OperationCanceledException`, the token is still cancelled after the fold and after the rethrow, and the surfaced exception is the same instance | 4 |
| `RECOV-12` | MUST | ✅ | When a response or recovery step **throws** while a `Success` is in hand, one internal helper (`StepFailure`) disposes that response through `Disposal` with the thrown exception as primary — the close error lands on the primary's trail and never replaces it — then returns `Failure(thrown)`. "Exactly once" is 3b's `Response` latch (P4b-12) | `ResponseRecoveryChainTests`: a `DisposalCountingBody` count of exactly 1 across the error-mapping path (which disposes once itself, then the chain's dispose is latched); a throwing dispose lands on `ExceptionTrail.GetSuppressed(primary)` and `Assert.Same` the surfaced primary; a `Failure` in hand disposes nothing | 3 |
| `RECOV-13` | MUST | ✅ | A step that **returns** a different outcome owns what it dropped: the chain never disposes the discarded original (the same helper, by construction, only runs on a throw) | `ResponseRecoveryChainTests`: a recovery step returning a substitute `Success` leaves the original's dispose count at **0** | 3 |
| `RECOV-14` | MUST | ✅ | Both chains copy every list at construction (`[.. steps]`, rejecting a `null` element), exposing `IReadOnlyList<…>` views of the copies — the stricter direction §5.2 takes for the request chain. Chains and the dispatcher are immutable; the shipped steps hold no per-call state; a step contract documents concurrency | `RequestRecoveryChainTests`, `ResponseRecoveryChainTests`: mutate the caller's list after construction (all three lists); `Parallel.ForEachAsync` over one chain with per-call recording steps | 3 |
| `RECOV-15` | MUST | ✅ | `ErrorMappingStep : IResponseStep` maps only 400..599 to `HttpResponseException` over the buffered copy, and returns every other status **by reference, body untouched** (never opened, read or disposed). 4c installs it as `ErrorMappingPolicy` at `PerCall` (`PIPE-37`, 4c's row) | `ErrorMappingStepTests`: `[Theory]` over 100, 199, 200, 204, 299, 304, 399, 600, 999 returning the same instance with an unopened body; 400, 404, 429, 499, 500, 503, 599 becoming `HttpResponseException` with the status. `EnsureSuccessErrorMappingTests` (`Security`, unedited) | 5 |
| `RECOV-16` | MUST | ✅ ⏳ 6a | ✅: one internal `ErrorBodyBuffer` (sync and async) is the only error-body capture in core, used by `ErrorMappingStep` and `Response.EnsureSuccessAsync`; it drains at most `Response.MaxBufferedErrorBytes` (1 MiB) as a hard, markerless truncation into a replayable body and disposes the original whether or not the drain completed (P4b-16). ⏳ 6a: the "re-sent error response" path is `RETRY-36`'s re-classification, which calls the same `ErrorBodyBuffer` (6a plan, the recovery-stack engine task) | `ErrorBodyBufferTests`: sub-cap survives whole; cap + 1 truncates to the constant (asserted against `Response.MaxBufferedErrorBytes`, not a literal); the original is disposed after a failing drain and the drain's exception is primary; the copy is replayable. `EnsureSuccessErrorMappingTests` (`Security`, unedited) | 5 |
| `RECOV-17` | MUST | ⏳ 6a | Retryability capability classification: `IRetryableError` and the configured-status rule are 6a's (design §6.1, §10 entry 7). 4b builds nothing | — | — |
| `RECOV-18` | MUST | ⏳ 6a | Re-sendability gate (`RETRY-5`, `XCUT-10`'s twin) | — | — |
| `RECOV-19` | MUST | ⏳ 6a | Re-classification of a re-sent response; it calls 4b's `ErrorBodyBuffer` and `ErrorMappingStep` and writes no second mapping | — | — |
| `RECOV-20` | MUST | ⏳ 6a | Max attempts and total-timeout budget | — | — |
| `RECOV-21` | MUST | ⏳ 6a | Backoff formula; `RETRY-13`'s one calculator | — | — |
| `RECOV-22` | MUST | ⏳ 6a | Pacing hint replaces the computed delay | — | — |
| `RECOV-23` | MUST | ⏳ 6a | Total pacing parser | — | — |
| `RECOV-24` | MUST | ⏳ 6a | Pacing-header forms and precedence | — | — |
| `RECOV-25` | SHOULD | ⏳ 6a | `X-RateLimit-Reset` positive jitter | — | — |
| `RECOV-26` | MUST | ✅ (S7 clause) ⏳ 6a | The 365-day clamp is phase 1's S7 (`RetryPacingOverflowTests`, `Security`); the saturation of every duration in the recovery engine is 6a's | `RetryPacingOverflowTests` (cited, untouched) | — |
| `RECOV-27` | MUST | ⏳ 6a | Cancellable inter-attempt wait (with `CFG-15`/`CFG-17`'s wait from 5a) | — | — |
| `RECOV-28` | MUST | ⏳ 6a | Per-call attempt state | — | — |
| `RECOV-29` | MUST | ⏳ 6a | A malformed pacing header never masks the upstream failure | — | — |
| `RECOV-30` | SHOULD | ⏳ 6a | One calculator, one parser, one default schedule (`RETRY-13`) | — | — |
| `RECOV-31` | MAY | ⏳ 6a | The attempt-ordinal header: one feature with `RETRY-38` (§11 item 20, §12's deferred list). 6a decides both IDs together — build, or file one `docs/first-release.md` "What v1 ships without" entry naming both (P4b-2) | — | — |
| `RECOV-32` | MUST | ✅ | `IdempotencyKeyStep : IRequestStep`: methods default **POST, PUT, PATCH**; `RespectExisting` default `true` (a present header leaves the request untouched **and the strategy is not invoked**), `false` overwrites; the strategy runs at most once per applicable request; other methods pass through by reference. `IdempotencyPolicy` delegates to it and keeps the per-call key reuse across redirect hops (P4b-17) | `IdempotencyKeyStepTests` (ported from Node's `idempotency-key.test.ts`), `IdempotencyPolicyTests` (rewritten for the new defaults) | 6 |
| `RECOV-33` | MUST | ✅ | `ClientIdentityStep : IRequestStep`: tokens joined by one space and trimmed; **Append** (default) composes the line after the **first** existing value and keeps every other value, or sets it as the sole value when absent; an empty first value counts as absent (no leading space); **Replace** overwrites every value; a blank or whitespace-only line is a no-op that emits no header. `ClientIdentityPolicy` delegates to it with `DexpaceClientOptions.UserAgent` as its token line, read per call (P4b-18) | `ClientIdentityStepTests` (ported from Node's `client-identity-step.test.ts`), `ClientIdentityPolicyTests` (rewritten for Append) | 6 |
| `RECOV-34` | MUST | ⏳ 6a | `RetryOptions` validation at construction (design §6.1; 5a turns the options into records) | — | — |

---

## Argued positions

### A. The closed outcome is a class, not a record (P4b-3, open for the lead)

Design §5.2 writes `public abstract record Outcome { private Outcome() { } … }` and calls the hierarchy "genuinely closed".
Fact 1 shows it is not: the synthesised `protected` copy constructor is reachable from any record outside the assembly, and
C# forbids making it private on a non-sealed record. An outside `Evil : Outcome` would then be a third variant reaching
every chain, which is the exact failure `RECOV-1`'s "jointly exhaustive" forbids and the reason the private constructor was
written. Options:

1. **An abstract class with a private constructor and nested sealed classes** (chosen). Closed by fact 2. It loses
   record value equality, which `Outcome` should not have anyway: two `Success` values over the same `Response` are equal
   by reference, and value equality over a `Response` (itself a disposable class with reference equality) would mean
   nothing more. It loses `with`, which no caller needs (a substitute outcome is constructed). It keeps the shape every
   caller sees (`Outcome.Success`, `Outcome.Failure`, `Match`), so the design's code samples change in one keyword.
2. Keep the record and seal the base. Impossible: the variants derive from it.
3. Keep the record and add a reflection-based architecture test that no other type derives from it. Rejected: it guards
   only this repository, while the hole is in the public surface.

The design's text is frozen to routine work, so this is a dated correction to §5.2 at close-out, and the plan files the
corpus note against `retry-and-resilience/302d143d`. It goes to the lead because it changes a shape the design states.

### B. The recovery layer has a synchronous and an asynchronous form, one implementation (P4b-6, P4b-7)

The specification's §8.3 says "the recovery layer is synchronous; its async equivalent is expressed through the stage-based
async pipeline". That sentence describes the reference, where the sync runtime is primary. On .NET the async path is
primary (design §5, opening), the transport is `IAsyncHttpClient` first, and 4c's sync `Process` path must be able to
install 4b's `ErrorMappingStep` without blocking on an async one (design §5.3's rule). Options:

1. **Both forms, one body** (chosen). Every step contract declares `Apply` and `ApplyAsync`, both required (no default
   that blocks). Each chain and the dispatcher implement one private `…CoreAsync(…, bool async)` in which every step and
   transport call branches `async ? await step.ApplyAsync(…) : step.Apply(…)`; the async entry point awaits it, and the
   sync one passes `async: false` and reads the already-completed `ValueTask` through one internal helper,
   `SyncPath.GetResult(ValueTask<T>)`, which asserts `IsCompleted` (throwing `InvalidOperationException` if not, which
   can only be a core defect) and reads `.Result` (fact 10: the original exception, not an `AggregateException`). This is
   §5.3's idiom, applied to the recovery layer.
2. Async only. Rejected: 4c's sync pipeline would have to block on the error-mapping step (banned) or carry a second,
   sync-only status mapping (`RECOV-15`/`RECOV-16` single-sourcing broken).
3. Sync only, as the specification describes. Rejected: it would block on `IAsyncHttpClient` in the dispatcher.

The reading is recorded as a new design §11 item at close-out (the specification's "synchronous" describes the reference's
primary runtime; the two-layer prohibition, which is the normative part of the sentence's paragraph, is kept by P4b-5).
Because the helper reads `ValueTask<T>.Result`, which no ban covers (fact 10), 4b adds
`P:System.Threading.Tasks.ValueTask`1.Result` to `BannedSymbols.txt` with a message citing §5.3, and the helper carries the
one scoped `#pragma warning disable RS0030`. 4c's shipped policies use the same helper for their `ProcessCoreAsync(…, bool
async)`; whichever sub-phase lands first adds it, and the other reuses it (Coupling).

### C. Where the conversion boundary sits (P4b-8, P4b-9)

`RECOV-2` and `RECOV-8` say "every throwable" and "MUST NOT throw under any input". The port's boundary is §10 entry 12's
filter, `when (!ExceptionFacts.IsFatal(ex))`, at four sites and no others: the request-chain call and the transport call in
the dispatcher, and the response-step and recovery-step calls in the response chain. Three consequences are decided here:

- **`OperationCanceledException` is converted like any other exception.** Styleguide 8.8 says a broad catch should exclude
  it, but 8.8's concern is swallowing the signal, and `RECOV-2`'s letter requires the recovery hooks to observe it (a
  recovery step deciding not to retry a cancelled call must see the cancellation). The signal is not swallowed: the token
  stays cancelled (fact 9) and `RECOV-10` rethrows the same instance. The filter is gate-clean (fact 3).
- **A `null` where a value is required is a throw.** A step that returns `null` (a caller's bug that nullable annotations
  only warn about) or a transport that returns `null` is treated exactly as if it had thrown `InvalidOperationException`
  naming the offending type: the in-hand response is released (`RECOV-12`) and a `Failure` continues the fold. Ruby chose to
  escape the chain with a named defect error (its R6) because its trigger was a core exhaustiveness defect; here the
  trigger is a caller's step, and `RECOV-8`'s totality is the stronger rule. A core defect has no route here: the only
  `switch` over `Outcome` carries `_ => throw new UnreachableException()`, which is unreachable by fact 2.
- **The chain never checks the token itself.** A `ThrowIfCancellationRequested` between steps would make `Apply` throw on
  a cancelled token, which `RECOV-8` forbids. Steps and the transport observe the token they are handed.

### D. The suppressed trail (P4b-14)

Design §5.2 fixes the mechanism (`SdkException.Suppressed`; a namespaced `Exception.Data` key on a foreign exception; the
self-suppression guard). Four details are left open, and each is decided:

1. **Snapshots, not a live list.** `SdkException.Suppressed` and `ExceptionTrail.GetSuppressed` return an immutable
   snapshot (`IReadOnlyList<Exception>` over a copied array); each attach replaces the stored array under a lock. A caller
   who read the trail earlier holds a stable list, and concurrent attaches (a dispose failure racing a retry's terminal
   attach) cannot tear it. Ruby reached the same rule (its P4-14). The foreign-exception store is an internal sealed
   `SuppressedTrail` object under the key `"Dexpace.Sdk.Core.Suppressed"`, written under a static lock (the first attach to
   a foreign exception races on `Exception.Data`, whose dictionary is not thread-safe).
2. **Attaching never throws over a primary.** Fact 4: a foreign exception's `Data` can be read-only. `AddSuppressed`
   catches the `NotSupportedException` (and any other non-fatal exception) from the `Data` write and drops the secondary —
   the one deliberate swallow, with a why-comment, because throwing here would replace the primary, which is the single
   failure the trail exists to prevent. The loss is stated in the XML docs and asserted in a test.
3. **No attachment involving a fatal exception, and none to itself.** A fatal primary is "surfaced unchanged with no
   suppressed-trail attachment" (`RETRY-25`); a fatal secondary never reaches the helper from SDK code (every catch filters
   it) and is ignored if a caller passes one. `AddSuppressed(e, e)` is a no-op (`RETRY-34`), and so is a second attach of a
   secondary already on the trail (reference identity).
4. **Rendering.** `SdkException.ToString()` appends, after the base rendering, one block per suppressed exception in the
   shape the runtime already uses for `AggregateException`: `---> (Suppressed Exception #n) {inner.ToString()}<---`. A
   suppressed exception whose own trail leads back to an exception being rendered would recurse forever; a `[ThreadStatic]`
   set of the exceptions currently rendering (reference identity) breaks the cycle by rendering `(cycle)`, and nesting is
   capped at 8 levels. A foreign exception's `ToString()` does not render its trail (§10 entry 13's residual); the user page
   says to read it with `ExceptionTrail.GetSuppressed`.

### E. The cause walk (P4b-13)

`ExceptionFacts.EnumerateCauses(Exception)` yields **the exception itself first**, then its causes breadth-first over
`InnerException` and `AggregateException.InnerExceptions`, tracking visited nodes in a `HashSet<Exception>` over
`ReferenceEqualityComparer.Instance` (facts 5, 6), stopping below depth 64 (§5.2). Root-first is Ruby's P4-16 for the same
reason: every consumer is a classification ("is anything in here retryable, a timeout, a cancellation?") that must test the
head too, and a head-skipping walk would put the same preamble at every call site. The method's XML docs say so, because the
name reads the other way. `IsFatal` tests the exception itself, not its chain: `await` unwraps, so an `OutOfMemoryException`
reaches a core catch unwrapped, and a fatal exception wrapped by a caller in a non-fatal one is the caller's decision.

Both are public, in `Dexpace.Sdk.Core.Errors`, because `XCUT-9`'s "any classification" includes third-party transports and
custom recovery steps, and design §5.2 makes the filter the rule "every orchestrator and every retry loop" uses, including
ones the SDK does not write.

### F. The error-body buffer moves to 4b (P4b-16, open for the lead)

P3a-13 and phase 1's S8 row assign `ErrorBodyBuffer` (and `ErrorMappingPolicy`, `XCUT-8`'s factory rejection, `BODY-30`'s
no-body clause on the pipeline step) to 4c, and 3b's status note asks 4c to move `EnsureSuccessAsync`'s `finally` onto
`Disposal`. But `RECOV-15` and `RECOV-16` are 4b's rows, the orchestrator's partition puts `ErrorMappingPolicy` *over* 4b's
folds, and 4b's `ErrorMappingStep` needs a sync capture that does not exist (only `EnsureSuccessAsync` drains). Options:

1. **4b builds `ErrorBodyBuffer` (sync and async) and `ErrorMappingStep`, and re-homes `EnsureSuccessAsync` onto it,
   including the failed-drain half of the `Disposal` move** (chosen). One capture site from the first commit that needs one; 4c keeps
   `ErrorMappingPolicy` (a thin wrapper at `PerCall`), `XCUT-8`'s factory, `PIPE-37`'s placement and `BODY-30`'s no-body
   clause on the policy. The 3b checklist's `HTTP-52`/`BODY-30` rows (⏳ 4c) and phase 1's S8 hand-off cell get a dated
   correction naming 4b for the buffer.
2. Keep P3a-13: 4b's step is async-only over `EnsureSuccessAsync`, and 4c adds the sync capture. Rejected: either 4c's sync
   policy blocks, or a second drain appears, and `RECOV-16`'s "the same bound MUST be shared across all error-body-buffering
   paths" becomes a promise kept by review.
3. Move `RECOV-15`/`RECOV-16` to 4c's checklist. Rejected: it breaks the one-prefix partition the census rests on.

This reassigns a recorded hand-off, so it goes to the lead. **Fallback if the lead keeps P3a-13:** 4b ships
`ErrorMappingStep.ApplyAsync` over `EnsureSuccessAsync`, marks `RECOV-16` ✅ (S8) ⏳ 4c, and `ErrorMappingStep.Apply` throws
`NotSupportedException` until 4c lands the buffer; 4c's spec then owns the sync capture.

`EnsureSuccessErrorMappingTests` stays unedited either way: the re-home is behaviour-preserving (same cap, same
`HttpResponseException`, same disposal of the original, the original disposed before the method completes), and the only
observable change (on the success path the original is disposed with a plain `Dispose`/`DisposeAsync` and a failure there propagates as today; `Disposal` with the drain's exception as primary is used only on the failed-drain path) — a dispose failure after a failed drain no longer *replaces* the drain's exception but lands on its
trail — is a case none of its six tests exercises. A new `ErrorBodyBufferTests` case pins it.

### G. The two shipped request steps, and how the policies keep their per-call behaviour (P4b-17, P4b-18, P4b-19)

Design §5.1 says each of `RECOV-32`/`RECOV-33` "is written once against the one policy shape, so the same class serves both
layers". There is no one shape: a recovery request step is `Request → Request`, and a pipeline policy (in 4c's rework)
is `(Request, PipelineContext, PipelineRunner) → Response`. Ruby found the same and resolved it with a pure transform that
each layer reaches through one mechanism (its R8). Here:

- The **logic lives once, in the recovery step** (`IdempotencyKeyStep`, `ClientIdentityStep`), as a pure transform of the
  request: no I/O, no per-call state, configuration fixed at construction.
- The **pipeline policy delegates** to it. `IdempotencyPolicy` holds an `IdempotencyKeyStep` and calls its internal
  `Apply(Request, Func<string> keySource)` with a key source that reads the call-scoped property bag first and mints through
  the step's strategy only when the bag is empty — so the strategy runs at most once per applicable request (`RECOV-32`) and
  once per call, and a redirect hop reuses the key (design §5.1's call-scoped bag). `ClientIdentityPolicy` calls the step's
  internal composition with `DexpaceClientOptions.UserAgent` as the token line, read per call (options are 5a's to make
  immutable).
- **4b keeps both policies on today's signature and stage** (`ProcessAsync(PipelineContext, PipelineRunner)`, `PerCall`).
  4c's signature rework ports them mechanically (each becomes a few lines over the same step call) and renames the stage;
  4b's policy tests carry over with their assertions unchanged. 4b adds no adapter class: a generic "transform to policy"
  adapter is a `PIPE` construct, and whether 4c wants one is 4c's choice.
- `DexpacePipeline.CreateDefault` constructs both policies parameterlessly, so the new defaults reach the default pipeline
  with no edit to it; 4c's `CreateDefault` rows cite `RECOV-32`/`RECOV-33` rather than owning them.

---

## N/A candidates, decided

None. The roadmap's card counts no `RECOV` N/A candidate, and none of the 34 rows has an antecedent this port cannot reach.
`RECOV-11` is the nearest: it is not N/A (the conversion happens and the property is asserted), it is free (fact 9).

---

## Type shapes

Everything public is in `src/Dexpace.Sdk.Core/Recovery/` (namespace `Dexpace.Sdk.Core.Recovery`, new) or
`src/Dexpace.Sdk.Core/Errors/` (existing). Every public member carries a `///` summary (`CS1591`).

### `Outcome` (`RECOV-1`)

```csharp
public abstract class Outcome
{
    private Outcome() { }

    public sealed class Success : Outcome { public Success(Response response); public Response Response { get; } }
    public sealed class Failure : Outcome { public Failure(Exception error); public Exception Error { get; } }

    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public bool TryGetResponse([NotNullWhen(true)] out Response? response);
    public bool TryGetError([NotNullWhen(true)] out Exception? error);
    public T Match<T>(Func<Response, T> onSuccess, Func<Exception, T> onFailure);
    public override string ToString();   // "Success(NOT_FOUND(404))" (via Status.ToString) / "Failure(System.IO.IOException)" — a type, never a message
}
```

Constructors `ThrowIfNull`. `ToString` never renders an exception message (a message can carry a URL; `OBS-11`'s concern).
The same type is reused by 7b's SSE typed adapter (design §5.2), which adds its third state in its own namespace, never here.

### Step contracts (`RECOV-3`–`RECOV-9`, `RECOV-14`)

```csharp
public interface IRequestStep
{
    Request Apply(Request request, CancellationToken cancellationToken);
    ValueTask<Request> ApplyAsync(Request request, CancellationToken cancellationToken);
}

public interface IResponseStep
{
    Response Apply(Response response, CancellationToken cancellationToken);
    ValueTask<Response> ApplyAsync(Response response, CancellationToken cancellationToken);
}

public interface IRecoveryStep   // XML docs: SHOULD return a Failure rather than throw (RECOV-9); owns any response it drops (RECOV-13)
{
    Outcome Apply(Outcome outcome, CancellationToken cancellationToken);
    ValueTask<Outcome> ApplyAsync(Outcome outcome, CancellationToken cancellationToken);
}
```

No context parameter (P4b-6): `RECOV-14`'s "per-request state … in the passed context or the value being transformed" is
met by the value, and adding 4a's context here would create the 4a→4b edge the roadmap says does not exist.

### `RequestRecoveryChain`, `ResponseRecoveryChain` (`RECOV-3`–`RECOV-8`, `RECOV-12`–`RECOV-14`)

```csharp
public sealed class RequestRecoveryChain
{
    public static RequestRecoveryChain Empty { get; }
    public RequestRecoveryChain(IEnumerable<IRequestStep> steps);
    public IReadOnlyList<IRequestStep> Steps { get; }
    public Request Apply(Request request, CancellationToken cancellationToken = default);              // throws (RECOV-3)
    public ValueTask<Request> ApplyAsync(Request request, CancellationToken cancellationToken = default);
}

public sealed class ResponseRecoveryChain
{
    public static ResponseRecoveryChain Empty { get; }
    public ResponseRecoveryChain(IEnumerable<IResponseStep> responseSteps, IEnumerable<IRecoveryStep> recoverySteps);
    public IReadOnlyList<IResponseStep> ResponseSteps { get; }
    public IReadOnlyList<IRecoveryStep> RecoverySteps { get; }
    public Outcome Apply(Outcome outcome, CancellationToken cancellationToken = default);              // never throws but fatal (RECOV-8)
    public ValueTask<Outcome> ApplyAsync(Outcome outcome, CancellationToken cancellationToken = default);
}
```

The response chain's two phases are two private methods, each well under `MA0051`'s 70 lines; the throw-while-holding path
is the internal static `StepFailure.ConvertAsync(Exception thrown, Outcome current, bool async)`, the one place
`RECOV-12`'s release happens (design §5.2: "one internal helper so the asymmetry cannot be implemented twice").

### `RecoveryDispatcher` (`RECOV-2`, `RECOV-10`, `RECOV-11`)

```csharp
public sealed class RecoveryDispatcher
{
    public RecoveryDispatcher(RequestRecoveryChain requestChain, ResponseRecoveryChain responseChain);
    public RequestRecoveryChain RequestChain { get; }
    public ResponseRecoveryChain ResponseChain { get; }
    public Response Dispatch(IHttpClient transport, Request request, RequestOptions options, CancellationToken cancellationToken = default);
    public ValueTask<Response> DispatchAsync(IAsyncHttpClient transport, Request request, RequestOptions options, CancellationToken cancellationToken = default);
}
```

The transport is a per-call argument (P4b-25): the dispatcher owns no transport, disposes none (styleguide 13.4), holds no
state, and one instance serves every transport — including 4c's `HttpPipeline` once it implements both SPIs (`PIPE-26`).

### The shipped steps (`RECOV-15`, `RECOV-32`, `RECOV-33`)

```csharp
public sealed class ErrorMappingStep : IResponseStep { public static ErrorMappingStep Instance { get; } /* Apply, ApplyAsync */ }

public sealed class IdempotencyKeyStep : IRequestStep
{
    public HttpHeaderName HeaderName { get; init; }            // default Idempotency-Key
    public IReadOnlySet<Method> Methods { get; init; }         // default {POST, PUT, PATCH}; init copies into a FrozenSet
    public bool RespectExisting { get; init; }                 // default true
    public Func<string> KeyStrategy { get; init; }             // default Guid.NewGuid().ToString("D"); must be thread-safe
    /* Apply, ApplyAsync */
}

public enum ClientIdentityMode { Append = 0, Replace = 1 }

public sealed class ClientIdentityStep : IRequestStep
{
    public ClientIdentityStep(IEnumerable<string> tokens);     // copied
    public IReadOnlyList<string> Tokens { get; }
    public HttpHeaderName HeaderName { get; init; }            // default User-Agent
    public ClientIdentityMode Mode { get; init; }              // default Append
    /* Apply, ApplyAsync */
}
```

`init` accessors with defensive copies, not builder objects (CLAUDE.md, §10 entry 10). A `KeyStrategy` that returns `null`,
empty or whitespace throws `InvalidOperationException` naming the step (a request-step throw, `RECOV-3`); a value that fails
header validation throws the `ArgumentException` `Headers` already raises.

### `ExceptionFacts`, `ExceptionTrail`, `SdkException` (§10 entries 12, 13)

```csharp
public static class ExceptionFacts
{
    public static bool IsFatal(Exception exception);                        // OutOfMemoryException and subtypes
    public static IEnumerable<Exception> EnumerateCauses(Exception exception); // root first, BFS, reference identity, depth ≤ 64
}

public static class ExceptionTrail
{
    public static void AddSuppressed(Exception primary, Exception secondary);
    public static IReadOnlyList<Exception> GetSuppressed(Exception exception);
}

public class SdkException : Exception
{
    public IReadOnlyList<Exception> Suppressed { get; }    // new
    public override string ToString();                     // new: renders the trail (position D)
}
```

### Internal types

| Type | Where | Role |
|---|---|---|
| `StepFailure` | `Recovery/` | `RECOV-12`'s one release-and-convert helper over `Disposal` |
| `SyncPath` | `Internal/` | `GetResult<T>(ValueTask<T>)` / `GetResult(ValueTask)`: assert completed, read the result; the one `RS0030` pragma for `ValueTask<T>.Result` (P4b-7). 4c reuses it |
| `ErrorBodyBuffer` | `Http/Response/` | `Capture(Response, CancellationToken)` / `CaptureAsync(…)`: drain ≤ `MaxBufferedErrorBytes` through `StreamCopy`, after a successful drain dispose the original with a plain `Dispose`/`DisposeAsync` (a failure propagates, as today); on a failed drain dispose it through `Disposal` with the drain's exception as primary; return `response.WithBody(ResponseBody.FromReplayableBytes(…))` |
| `SuppressedTrail` | `Errors/` | the lock-guarded immutable array behind both trail stores |

### The public surface (`PublicAPI.Unshipped.txt`, core only)

The plan takes the exact lines from the analyzer's code fix; the shape is: the `Dexpace.Sdk.Core.Recovery` namespace's
`Outcome` (and its two nested classes), `IRequestStep`, `IResponseStep`, `IRecoveryStep`, `RequestRecoveryChain`,
`ResponseRecoveryChain`, `RecoveryDispatcher`, `ErrorMappingStep`, `IdempotencyKeyStep`, `ClientIdentityStep`,
`ClientIdentityMode`; `Dexpace.Sdk.Core.Errors.ExceptionFacts`, `ExceptionTrail`, `SdkException.Suppressed.get` and
`override SdkException.ToString()`; and the changed constructors of `IdempotencyPolicy` and `ClientIdentityPolicy`
(P4b-17, P4b-18). `Dexpace.Sdk.Http.SystemNet`'s and `Dexpace.Sdk.Serialization.SystemTextJson`'s API files do not change.

---

## Migration plan from the as-built code

| Change | Sites | Mechanical rewrite |
|---|---|---|
| `Disposal`'s filter → `ExceptionFacts.IsFatal`; its primary branch → `ExceptionTrail.AddSuppressed(primary, ex)` **only** (design §3.7: "exactly one of two ways and never a third"); the no-primary branch keeps the `Activity` event and the optional logger | `Internal/Disposal.cs` | The private `IsFatal` and the "Phase 4b repoints" remarks go. `DisposalTests.With_a_primary_in_flight_the_report_carries_the_primary_type_and_the_primary_is_untouched` is rewritten: the primary is not replaced (`Assert.Same`), its trail holds the dispose failure, and no `Activity` event is recorded (P4b-15) |
| `LoggingResponseBody`'s two `when (ex is not OutOfMemoryException)` filters → `when (!ExceptionFacts.IsFatal(ex))` | `Http/Response/LoggingResponseBody.cs` | Same set (fact 8); no behaviour change |
| `SdkException` gains `Suppressed` and `ToString` | `Errors/SdkException.cs` | Additive; every subclass inherits it |
| `Response.EnsureSuccessAsync`'s drain → `ErrorBodyBuffer.CaptureAsync` | `Http/Response/Response.cs` | `if (!error) return; throw new HttpResponseException(await ErrorBodyBuffer.CaptureAsync(this, ct));` The XML docs keep their text; `EnsureSuccessErrorMappingTests` unedited |
| `IdempotencyPolicy` delegates to `IdempotencyKeyStep`; constructors become `()` and `(IdempotencyKeyStep step)`; default methods POST, PUT, PATCH | `Pipeline/Policies/IdempotencyPolicy.cs` | Signature and stage unchanged; `PropertyKey` reuse kept |
| `ClientIdentityPolicy` delegates to `ClientIdentityStep`'s composition; constructors `()` and `(ClientIdentityMode mode)`; default Append | `Pipeline/Policies/ClientIdentityPolicy.cs` | Signature and stage unchanged |
| `BannedSymbols.txt` gains `P:System.Threading.Tasks.ValueTask`1.Result` | repository root | The plan verifies the documentation ID fires with a throwaway probe, as 2b and 3a did; `src/` has no current use (fact 10) |
| `TestSupport` gains `Recovery/`: `DelegateRequestStep`, `DelegateResponseStep`, `DelegateRecoveryStep` (lambda-backed, sync and async), `CyclicExceptions` (a self-cycle and a two-node cycle closed by reflection on `_innerException`, design §5.2; a `StructurallyEqualException` overriding `Equals`/`GetHashCode`), `ReadOnlyDataException` (fact 4) | `tests/Dexpace.Sdk.TestSupport/Recovery/` | New, non-packable. The existing `RecordingTransport`, `RecordingSyncTransport`, `ScriptedTransport` and `DisposalCountingBody` are reused, not copied |
| Documentation | `CLAUDE.md` (layout tree gains `Recovery/`; "What is genuinely unbuilt" drops the recovery chain), `src/Dexpace.Sdk.Core/README.md` (a recovery paragraph), `docs/architecture.md` (if it lists core namespaces) | Close-out PR |

**`Security` classes kept green** (constraint 5), **no file edited**: `EnsureSuccessErrorMappingTests` (S8) runs against the
re-homed drain; `ReDriveRequestIsolationTests` (S6) is untouched because 4b changes no runner, context or retry code — S6's
structural form is 4c's; `RetryPacingOverflowTests` (S7) is untouched. 4b adds no `Security` class: it fixes no phase-1
defect.

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes and gets a `CHANGELOG.md` `[Unreleased]` line in the
PR that makes it (constraint 8).

| # | Change | Kind | Evidence | PR |
|---|---|---|---|---|
| 1 | `IdempotencyPolicy` stamps PUT and PATCH as well as POST by default (was: POST only) | behaviour | `IdempotencyPolicyTests` | 6 |
| 2 | `IdempotencyPolicy(IEnumerable<Method>?)` is replaced by `IdempotencyPolicy()` and `IdempotencyPolicy(IdempotencyKeyStep)` | signature | `PublicAPI.Unshipped.txt` diff | 6 |
| 3 | `ClientIdentityPolicy` appends the SDK line after a caller-supplied `User-Agent` value by default (was: replaced it); a blank `UserAgent` emits no header | behaviour | `ClientIdentityPolicyTests` | 6 |
| 4 | `SdkException.ToString()` renders the suppressed trail | behaviour (log output) | `ExceptionTrailTests` | 1 |
| 5 | `Response.EnsureSuccessAsync`: a dispose failure after a failed drain is attached to the drain's exception instead of replacing it | behaviour | `ErrorBodyBufferTests` | 5 |

Additive, with no **Breaking** marker: the `Recovery` namespace, `ExceptionFacts`, `ExceptionTrail`,
`SdkException.Suppressed`, the `BannedSymbols.txt` entry (a build rule, not public surface). `Disposal`'s change is
internal; its observable effect is the trail on a primary that was already being reported.

---

## Landing order

Each step is one pull request carrying its code **and** its tests (roadmap step 5's one-PR allowance, as 2a–3b used it),
with its `PublicAPI.Unshipped.txt` diff and `CHANGELOG.md` line where it has one.

| PR | Content | Rows | Gate | Notes |
|---|---|---|---|---|
| **1** | `ExceptionFacts`, `ExceptionTrail`, `SuppressedTrail`, `SdkException.Suppressed`/`ToString`; the `Disposal` and `LoggingResponseBody` repoints; `TestSupport/Recovery/` exception fixtures | (groundwork for `RECOV-2`, `RECOV-8`, `RECOV-12`) | none | Breaking 4. The 3b hand-off is closed here |
| **2** | `Outcome`; `SyncPath`; the `ValueTask<T>.Result` ban | `RECOV-1` | PR 1 | If 4c's PR 1 has already added `SyncPath`, this PR reuses it |
| **3** | The step contracts; `RequestRecoveryChain`, `ResponseRecoveryChain`, `StepFailure`; the delegate step fakes; `RecoveryLayerArchitectureTests` | `RECOV-3`–`RECOV-9`, `RECOV-12`–`RECOV-14` | PR 2 | |
| **4** | `RecoveryDispatcher` | `RECOV-2`, `RECOV-10`, `RECOV-11` | PR 3 | |
| **5** | `ErrorBodyBuffer`; `ErrorMappingStep`; `EnsureSuccessAsync` re-homed | `RECOV-15`, `RECOV-16` | PR 3 | Breaking 5. **4c's `ErrorMappingPolicy` waits for this PR.** `EnsureSuccessErrorMappingTests` must pass unedited |
| **6** | `IdempotencyKeyStep`, `ClientIdentityStep`, `ClientIdentityMode`; both policies delegate | `RECOV-32`, `RECOV-33` | PR 3 | Breaking 1–3. **4c's policy-signature rework of these two policies waits for this PR** |
| **7** | Close-out: the ⏳ rows; the AOT smoke extended (an `Outcome` fold, a dispatch over a fake transport, `ExceptionTrail` on a foreign exception through `Data`); `docs/sdk-documentation/recovery.md`; the 4b checklist; the dated design corrections; the corpus note; `CLAUDE.md`, READMEs; the roadmap status note | all 34 (closing) | 1–6 | The docs close the sub-phase (roadmap step 7) |

PRs 5 and 6 are independent of each other and of PR 4.

---

## Tests, vectors and ports

- **Categories.** `Unit` throughout `tests/Dexpace.Sdk.Core.Tests` (fakes only; `SEAM-2`'s partition holds — no transport in
  the core suite). `AotSmoke` for the extended smoke checks. **No `Security` class is added or edited.** No `Integration`
  test: nothing in 4b touches the wire.
- **Both forms, one test.** Every chain, dispatcher and step test is a `[Theory]` over `bool async` that calls `Apply` or
  `ApplyAsync` (or `Dispatch`/`DispatchAsync` over `RecordingSyncTransport`/`RecordingTransport`), so a sync/async drift
  fails the same test twice.
- **The tests a reader would otherwise write wrong** (from Ruby's 4b "Testing strategy", transcribed):
  - `RECOV-2` driven from the **request** side as well as the transport side: a suite that only throws from the transport
    passes against a dispatcher that wraps the transport alone.
  - `RECOV-10` with `Assert.Same` on a **constructed, never-thrown** exception returned by a recovery step — `RECOV-10`'s own
    case — and on a thrown one.
  - `RECOV-11` asserted on the token, not on the exception type.
  - `RECOV-12` as three tests: a release **count** of 1 (`DisposalCountingBody` counts the body's release, so the latched
    second dispose is visible as "not counted"); the dispose failure on the trail with the primary `Assert.Same`; a `Failure`
    in hand releases nothing.
  - `RECOV-13` as a **zero** count, through a recovery step (only a recovery step can return a different outcome).
  - `RECOV-8` asserted on the returned `Outcome` and on the next step having run; xUnit's `Record.Exception` being `null` is
    never the whole assertion.
  - `RECOV-6`'s order with one log; `RECOV-14` by mutating the caller's list after construction, on all three lists.
  - The cause walk's three cases: a self-cycle and a two-node cycle (closed by reflection, as design §5.2 verified; asserted
    by **count** and by `Assert.Same` per element), and two distinct `StructurallyEqualException`s chained through an
    `AggregateException` that the walk yields as two (fact 5's discriminator); plus the aggregate-duplicate case (fact 6)
    and the depth cap (a 100-deep chain yields 65 nodes: the root and 64 levels).
  - `ExceptionTrail`: the self guard; a duplicate secondary; a fatal primary; a read-only `Data` (fact 4) drops the
    secondary and does not throw; concurrent attaches from 16 tasks all land; a snapshot taken before an attach is
    unchanged after it; `SdkException.ToString` renders `(Suppressed Exception #0)`; a two-exception mutual trail renders
    `(cycle)` and terminates.
- **Ports.** Each ported test cites its source path in a header comment (constraint 10):
  `nodejs-sdk@c0ff3fd packages/core/src/recovery/{outcome,request-chain,response-chain,orchestrator,release,status-mapping,idempotency-key}.test.ts`,
  `packages/core/src/config/client-identity-step.test.ts`, `packages/core/src/suppress.test.ts`; and the case lists of
  `ruby-sdk@90075b1 docs/work/mvp/phase4/phase4b/2026-09-08-phase4b-recovery-primitives-design.md` ("Testing strategy",
  R7), since the Ruby gem tests are not in the clone. Node's `cancellation.test.ts` asserts a JS-runtime fact (no
  re-assertion exists) and is not ported; `RECOV-11`'s token test replaces it.
- **Vectors.** None. The status ranges are a short `[Theory]` table; the two step suites are small enough to stay inline.
- **Architecture.** `RecoveryLayerArchitectureTests` (`Unit`, under `Architecture/`): no type in `Dexpace.Sdk.Core.Recovery`
  references a type in `Dexpace.Sdk.Core.Pipeline` (the §8.3 prohibition on collapsing the layers, P4b-5). The reverse edge
  (a policy calling a step) is allowed.

---

## Coupling with 4a, 4c and later phases

### The interface 4b hands to 4c

4c **depends on 4b**. It consumes, and may cite by name:

| 4b type | What 4c does with it | The obligation |
|---|---|---|
| `Outcome` and the two folds (`RequestRecoveryChain`, `ResponseRecoveryChain`), `IResponseStep` | `ErrorMappingPolicy` sits over `ErrorMappingStep` | No `Outcome` crosses into a `PIPE` signature; a policy returns a `Response` or throws |
| `ErrorMappingStep`, `ErrorBodyBuffer` (P4b-16) | `ErrorMappingPolicy` at `PerCall` calls `ErrorMappingStep.Apply`/`ApplyAsync`; `XCUT-8`'s factory rejection and `BODY-30`'s no-body clause stay 4c's | No second status mapping and no second drain |
| `IdempotencyKeyStep`, `ClientIdentityStep`, the two reworked policies | 4c ports both policies to its request-in/response-out signature and the renamed stage; `CreateDefault` keeps constructing them parameterlessly | 4b's policy tests keep their assertions; 4c's rows cite `RECOV-32`/`RECOV-33` and do not own them |
| `ExceptionFacts.IsFatal`, `ExceptionFacts.EnumerateCauses` | Every new catch in a policy filters with `IsFatal`; any cause test walks with `EnumerateCauses` | No private fatal predicate, no hand-written `InnerException` loop |
| `ExceptionTrail` (`SdkException.Suppressed`) | `PIPE-40`'s superseded-response closes go through `Disposal`, which now attaches to the trail | No `AggregateException` wrapping |
| `SyncPath` (P4b-7) | The shipped policies' `Process` reads its completed `ProcessCoreAsync(…, async: false)` through it | One sanctioned `ValueTask<T>.Result` read in core |
| `RecoveryDispatcher` | Optional: nothing in 4c needs it. `HttpPipeline` implementing `IAsyncHttpClient`/`IHttpClient` (`PIPE-26`) makes a pipeline a valid dispatcher transport | — |

**A finding routed to 4c (constraint 7).** `InstrumentationPolicy`'s two `catch (Exception ex)` blocks have no fatal filter,
so an `OutOfMemoryException` is tagged on the activity and logged, which `RETRY-25` ("MUST NOT be … logged") forbids. 4b
does not touch the policy (4c rewrites every policy's signature, and 5c rewrites instrumentation); 4c's rework adds
`when (!ExceptionFacts.IsFatal(ex))`, or 5c does if 4c lands first. The 4b status note names both.

### 4a

None. No 4b type names a 4a type, and the step contracts carry no context (P4b-6). If 4a's `RequestContext` later wants to
carry an idempotency key or an outcome, that is a 4a/4c question over 4b's public types.

### Later phases

- **5b** plumbs the client's logger into `Disposal`'s no-primary branch (unchanged by 4b) and may render `ExceptionTrail`
  in log events; it does not replace the trail.
- **6a** builds the recovery-stack engine as an `IRecoveryStep` (the first core recovery step, `RECOV-9`'s subject) over
  `ResponseRecoveryChain` and `RecoveryDispatcher`, flips the fifteen ⏳ rows and `RECOV-31`, calls `ErrorBodyBuffer` and
  `ErrorMappingStep` for `RECOV-19`/`RETRY-36` re-classification, uses `ExceptionTrail.AddSuppressed` for `RETRY-34` on both
  stacks, and `EnumerateCauses` for `RETRY-2`'s I/O-family walk. **Open question for 6a (not for the lead):** a recovery step
  sees only an `Outcome`, so a retrying step needs the prepared request and the transport; 6a decides whether it constructs
  the step per call (capturing both) or asks for a dispatcher entry point that hands them over. 4b's types admit the first
  without change.
- **7b** reuses `Outcome` for the SSE typed adapter (design §5.2), in its own namespace. **7c** uses `ExceptionTrail` for
  `PAGE-13`.
- **Phase 10** audits `XCUT-9` repository-wide against `EnumerateCauses`.

---

## Design corrections owed at close-out

Dated corrections, in PR 7, each naming the ruling that caused it:

- **§5.2**: `Outcome` is an abstract class with nested sealed classes, not a record (P4b-3, fact 1); the accessors are the
  Try-pattern (P4b-4); the recovery layer has both forms over one core (P4b-6); `EnumerateCauses` yields the root first
  (P4b-13); the trail's snapshot, swallow-on-read-only-`Data`, fatal and rendering rules (P4b-14); the **As built** line.
- **§5.1**: "Three shipped steps": the logic lives in the recovery steps and the policies delegate (P4b-17, P4b-18);
  "The bounded error-body copy": `ErrorBodyBuffer` is built by 4b (P4b-16, if accepted).
- **§3.7**: `Disposal`'s repoint is done (P4b-15); the **As built (phase 4b)** line.
- **§10 entry 12** gains the topic label **`fatal-exception-filter`**; **§10 entry 13** gains **`suppressed-trail`** and the
  read-only-`Data` residual (P4b-14). Both under the roadmap's rule of citing §10 by topic, as 3a coined `eof-is-zero-read`.
- **§11**: a new item for the "recovery layer is synchronous" reading (P4b-6), at the next free number when it lands
  (4a and 4c may add items concurrently).
- **§12**: the `RECOV` row's deferred list moves `RECOV-31` to "⏳ 6a with `RETRY-38`" (P4b-2, if accepted).
- **Roadmap**: the phase 4 row's `sdk-design refs` cell gains this design's link (appended); phase 1's S8 row and the 3b
  checklist's `HTTP-52`/`BODY-30` cells name 4b for `ErrorBodyBuffer`, and the 3a status note's hand-off ("4c: builds `ErrorBodyBuffer` and `ErrorMappingPolicy` over `DrainUpTo`, and a sync `EnsureSuccess`") and P3a-13 get a dated correction: 4b builds the buffer, 4c keeps the policy and the public sync `EnsureSuccess` over `ErrorBodyBuffer.Capture` (P4b-16, if accepted); a dated status note.

---

## Rulings

Taken by this design in the absence of a human reviewer. Each lists the options considered where there were any. Those
marked **open for the lead** need sign-off before the plan is executed; the rest are defaults the plan may take as written.

1. **P4b-1 — The phase 4 partition, in place of a segmentation design. Open for the lead.** 4b owns `RECOV-1`–`RECOV-34`
   (34 rows); 4a owns `CTX-1`–`CTX-20` (20); 4c owns `PIPE-1`–`PIPE-40` (40); 20 + 34 + 40 = 94, the card's exit count. No
   non-prefix row is placed in phase 4, so the census needs none; `RECOV-32`/`RECOV-33` are 4b's and 4c cites them. The
   segmentation rule asks for a segmentation design first; the card's cut plus this census stands in for it, as P3a-1 did
   for phase 3. The order is 4a and 4b in either order, then 4c (4c **depends on** 4b; 4a↔4b is **none**).
2. **P4b-2 — Dispositions. Partly open for the lead.** 18 rows built; 15 ⏳ 6a as the card states (`RECOV-17`–`RECOV-30`,
   `RECOV-34`), each naming the 6a plan's recovery-stack engine task (the 6a plan does not exist yet; the row cites the
   roadmap's 6a card bullet "the recovery-stack engine (the 15 `RECOV` IDs)" until it does). **The open part:** `RECOV-31`
   (MAY) is not in the card's list but is one feature with `RETRY-38` (§11 item 20), which is 6a's row; 4b marks it ⏳ 6a so
   6a decides both together. Options: ⏳ 6a (chosen); 🚫 declined now (rejected: deciding a `RETRY` twin from 4b); ⏳ against
   a `docs/first-release.md` entry written now (rejected: 6a may build it).
3. **P4b-3 — `Outcome` is an abstract class, not a record. Open for the lead.** Position A; facts 1 and 2. A dated
   correction to §5.2, a corpus note against `retry-and-resilience/302d143d`, and a **styleguide 6.3 departure** (6.3 says closed choices are abstract records with sealed-record cases; fact 1 is the reason): a dated SDK-overlay row in `docs/styleguide/README.md` and a `notes/data-modeling.md` entry (Conflicts or Superseded) against the harvested `data-modeling` rule. Open for the lead with this ruling.
4. **P4b-4 — Accessors.** `IsSuccess`/`IsFailure` and the Try-pattern `TryGetResponse`/`TryGetError` with `[NotNullWhen]`
   stand for `RECOV-1`'s "response-or-null, error-or-null" (styleguide 8.7 and chapter 03); one fold, `Match<T>`. No
   `ResponseOrNull` property: the Try-pattern is the .NET spelling of the same derivable accessor.
5. **P4b-5 — Placement and the two-layer wall.** A new public namespace `Dexpace.Sdk.Core.Recovery` (folder `Recovery/`)
   for the outcome, the step contracts, the chains, the dispatcher and the three steps; `ExceptionFacts` and `ExceptionTrail`
   in `Dexpace.Sdk.Core.Errors`. An architecture test forbids `Recovery` → `Pipeline` references (§8.3); `Pipeline` →
   `Recovery` is allowed (a policy delegating to a step).
6. **P4b-6 — Step contracts.** Three interfaces, each with a required `Apply` and `ApplyAsync`, taking the value and a
   `CancellationToken` and nothing else; the recovery layer has both forms over one `bool async` core (position B). A new
   design §11 item records the reading of §8.3's "the recovery layer is synchronous".
7. **P4b-7 — `SyncPath` and the `ValueTask<T>.Result` ban.** One internal helper reads a completed `ValueTask` behind the
   one scoped `RS0030` pragma; `BannedSymbols.txt` gains `P:System.Threading.Tasks.ValueTask`1.Result` (fact 10). 4c reuses
   the helper; whichever sub-phase lands first adds it.
8. **P4b-8 — The conversion boundary.** `when (!ExceptionFacts.IsFatal(ex))` at exactly four sites; cancellation is converted
   (not excluded), because the token keeps the signal and `RECOV-10` rethrows the instance; a `null` from a step or a
   transport is a throw of `InvalidOperationException` naming the type (position C).
9. **P4b-9 — `RECOV-8`'s one exception is §10 entry 12's.** The response chain's apply throws only a fatal exception (and,
   through the release helper, a fatal exception from a dispose, which `Disposal` already lets propagate). No named
   defect error is introduced (Ruby's `OutcomeError` has no .NET trigger: fact 2 makes the discard arm unreachable).
10. **P4b-10 — The unwrap.** `ExceptionDispatchInfo.Capture(error).Throw()`; the same instance (fact 7), with the runtime's
    rethrow boundary appended to its stack trace, which adds no wrapper and substitutes nothing.
11. **P4b-11 — `RECOV-11` is free and asserted on the token** (fact 9; §5.2). No wrapper, no token mutation.
12. **P4b-12 — `RECOV-12`/`RECOV-13` in one helper, "exactly once" by 3b's latch.** `StepFailure` runs only on a throw and
    releases through `Disposal` with the thrown exception as primary; a returned outcome is never released by the chain.
    `ErrorBodyBuffer` disposes the original itself, and the chain's later dispose of the same `Response` is a latched no-op.
13. **P4b-13 — `ExceptionFacts`.** Public. `IsFatal`: `OutOfMemoryException` and subtypes, the exception itself only.
    `EnumerateCauses`: root first, breadth-first over `InnerException` and `AggregateException.InnerExceptions`, reference
    identity, depth ≤ 64 (position E).
14. **P4b-14 — `ExceptionTrail` and `SdkException.Suppressed`.** Public. Snapshots; a lock-guarded store; the foreign key
    `"Dexpace.Sdk.Core.Suppressed"`; a read-only `Data` drops the secondary rather than throw (the one deliberate swallow);
    no attachment to or from a fatal exception, none to itself, none twice; `ToString` renders `(Suppressed Exception #n)`
    blocks with a cycle guard and an 8-level cap (position D).
15. **P4b-15 — The `Disposal` repoint follows §3.7 literally.** With a primary in flight, the failure goes onto the
    primary's trail **only** ("exactly one of two ways and never a third"); without one, the `Activity` event and optional
    logger stay as 3b built them (5b's to complete). The filter is `ExceptionFacts.IsFatal`. One non-`Security` test is
    rewritten to the new contract.
16. **P4b-16 — `ErrorBodyBuffer` and `ErrorMappingStep` are 4b's. Open for the lead.** Position F, with its fallback. It
    reassigns P3a-13's and phase 1's S8 hand-off of the buffer (and 3b's `EnsureSuccessAsync`/`Disposal` move) from 4c to 4b;
    `ErrorMappingPolicy`, `XCUT-8`'s factory, `PIPE-37` and `BODY-30`'s policy clause stay 4c's, as does the public sync `Response.EnsureSuccess` (the 3a status-note hand-off): 4c builds it over 4b's `ErrorBodyBuffer.Capture`.
17. **P4b-17 — `IdempotencyKeyStep` and `IdempotencyPolicy`.** Defaults per `RECOV-32` (POST, PUT, PATCH; respect existing;
    `Idempotency-Key`; a `Guid` strategy); `init` configuration with a `FrozenSet<Method>` copy; the policy keeps the
    call-scoped key reuse by handing the step a bag-first key source, so the strategy runs at most once per call. Stage and
    signature untouched (4c's).
18. **P4b-18 — `ClientIdentityStep` and `ClientIdentityPolicy`.** Composition per `RECOV-33`; Append by default; the policy's
    token line is `DexpaceClientOptions.UserAgent`, read per call, so a blank value emits nothing. Where the token list
    comes from by default (an `SdkVersion`/runtime token, `CFG-36`) is 5a's; 4b does not change `UserAgent`'s default.
19. **P4b-19 — `RECOV-32`/`RECOV-33` ownership.** 4b's rows, evidenced by step-level and policy-level tests. 4c's
    `DexpacePipeline.CreateDefault` wiring and stage placement cite them; 4c owns no row for them.
20. **P4b-20 — 4b lands before 4c and changes no pipeline type's shape.** Only the two policies' constructors and internals
    change, on today's signature; 4c's rework ports them mechanically. 4b touches neither `HttpPipelinePolicy`,
    `PipelineRunner`, `PipelineContext`, `PipelineStage`, `HttpPipeline` nor `DexpacePipeline`.
21. **P4b-21 — No new §10 entry.** Entries 12 and 13 already argue the two departures 4b implements; they gain the topic
    labels `fatal-exception-filter` and `suppressed-trail` and dated amendments. P4b-3 and P4b-6 are a design correction and
    a §11 reading, not deviations from a requirement.
22. **P4b-22 — `RECOV-9`.** ✅ on the chain's behaviour and the documented preference; core ships no recovery step until 6a.
23. **P4b-23 — The `InstrumentationPolicy` fatal-filter finding goes to 4c** (or 5c), not into 4b's diff (Coupling).
24. **P4b-24 — Split rows.** `RECOV-16` is ✅ with its re-sent-response half ⏳ 6a (`RETRY-36`); `RECOV-26` is ✅ for phase 1's
    S7 clamp and ⏳ 6a for the engine's saturation.
25. **P4b-25 — `RecoveryDispatcher` takes the transport per call** and the caller's `RequestOptions`, owns and disposes
    nothing, and exposes only `Dispatch`/`DispatchAsync`; the chains' public `Apply`/`ApplyAsync` are the lower-level entry
    for 6a.
26. **P4b-26 — No lambda adapters in the public surface.** A step is an interface implementation; the delegate-backed steps
    live in `TestSupport` (styleguide 10.1, minimal surface). A public `RequestStep.From(Func<…>)` can be added later
    without breaking anything if a consumer asks.

---

## Risks and open questions

- **Concurrent sibling designs.** 4c is being brainstormed in parallel and may also claim `ErrorBodyBuffer`, `SyncPath` or
  the two policies' rework. The reconciliation rule: `RECOV-*` rows and the recovery types are 4b's; the policy signature,
  stage, `ErrorMappingPolicy` and `CreateDefault` are 4c's; a helper both need is added by whichever lands first. P4b-1 and
  P4b-16 put the two contested points to the lead.
- **The sync dispatch path is only as real as the transport.** `RecoveryDispatcher.Dispatch` over the SystemNet transport is
  still sync-over-async inside the adapter until 8b (coupling obligation 5); 4b's sync path adds no blocking of its own.
- **6a's engine shape** (Later phases): a retrying recovery step needs the request and the transport; 4b's surface admits a
  per-call step, and 6a decides.
- **Breaking 3** changes what reaches the server for a caller who sets `User-Agent` per request. It is `RECOV-33`'s default,
  and the user page says how to get the old behaviour (`new ClientIdentityPolicy(ClientIdentityMode.Replace)`).

---

## Deviation Ledger

| ID | Decision | Touches | Kind | Argued in | Route |
|---|---|---|---|---|---|
| P4b-3 | `Outcome` is an abstract class closed by a private constructor, not an abstract record, because a record's synthesised protected copy constructor leaves the hierarchy open | `RECOV-1` | design correction: §5.2's shape fails its own stated property (fact 1) | [Position A](#a-the-closed-outcome-is-a-class-not-a-record-p4b-3-open-for-the-lead) | Design §5.2, dated correction (PR 7); corpus note superseding `retry-and-resilience/302d143d`; styleguide 6.3 departure: dated SDK-overlay row in `docs/styleguide/README.md` plus a `notes/data-modeling.md` entry (open for the lead) |
| P4b-6 | The recovery layer has a sync and an async form over one core, where the specification's §8.3 calls it synchronous | `RECOV-1`–`RECOV-16` (the layer), §8.3 | a reading: the sentence describes the reference's primary runtime | [Position B](#b-the-recovery-layer-has-a-synchronous-and-an-asynchronous-form-one-implementation-p4b-6-p4b-7) | New design §11 item (PR 7) |
| P4b-8 | `OperationCanceledException` is converted to a `Failure` by the recovery boundary, against styleguide 8.8's exclusion; a `null` from a step or transport is converted as a throw | `RECOV-2`, `RECOV-8`; styleguide 8.8 | mechanism: §10 entry 12's filter, applied | [Position C](#c-where-the-conversion-boundary-sits-p4b-8-p4b-9) | Design §10 entry 12, dated amendment (PR 7) |
| P4b-14 | A suppressed secondary is dropped when a foreign exception's `Data` is read-only; the trail is a snapshot; `ToString` renders with a cycle guard | `RECOV-12`, `RETRY-34` | mechanism, with a residual (fact 4) | [Position D](#d-the-suppressed-trail-p4b-14) | Design §10 entry 13, dated amendment (PR 7) |
| P4b-16 | `ErrorBodyBuffer` is built by 4b, not 4c | `RECOV-16`, `HTTP-52`, `BODY-30` | ownership change of a recorded hand-off | [Position F](#f-the-error-body-buffer-moves-to-4b-p4b-16-open-for-the-lead) | Design §5.1, roadmap S8 row and 3b checklist, dated corrections (PR 7) |

No ruling leaves a MUST's letter unmet on a stated domain beyond what §10 entries 12 and 13 already record, so no new §10
entry is opened.
