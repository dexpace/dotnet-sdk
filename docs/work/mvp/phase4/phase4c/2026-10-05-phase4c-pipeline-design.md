# Phase 4c — Pipeline Rework: Design

**Status:** Draft, for review. Written 2026-10-05 against `main` at `0332cef` (phases 2a, 2b, 3a and 3b merged; 4a and 4b
are being brainstormed in parallel and nothing of theirs is in the tree). Brainstormed without a human in the loop: every
judgement call the brainstorming skill would have put to the lead is taken here as a numbered ruling (`P4c-n`) with the
options and the rationale. Five rulings are **open for the lead** (P4c-2, P4c-6, P4c-15, P4c-17, P4c-22); the rest are
taken. The scope authority is the roadmap's Phase 4 card and Phase List row 4
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). No phase 4 segmentation design exists; [P4c-1](#rulings)
and [the census](#scope-and-the-94-row-census) stand in for its row split. The format follows the 3a and 3b designs
(`docs/work/mvp/phase3/phase3a/2026-10-02-phase3a-io-design.md`, `docs/work/mvp/phase3/phase3b/2026-10-02-phase3b-bodies-design.md`).

**What this document is.** The sub-phase design for 4c: one explicit disposition per requirement row (40 rows,
`PIPE-1`–`PIPE-40`), the shape of every type 4c adds or changes, the 4a and 4b types it consumes and the contract it
assumes of each, argued positions on what design §5.1 and §5.3 leave open, the work 4c does on rows other sub-phases and
phases own, a migration plan from the as-built code, the breaking changes, a landing order in pull-request-sized steps,
the test strategy, how the S6 and S8 `Security` classes stay green, the proposed dated §10 text for the `PIPE-32`/`REDIR-25`
reversal, and the rulings (`P4c-1`…`P4c-23`), which double as the deviation-ledger IDs (roadmap constraint 7).

**What this document is not.** It is not the plan (numbered TDD tasks) and not the checklist. It does not restate
design §5.1's stage pipeline, §5.3's single-implementation rule or §6.2's seed-origin argument (roadmap constraint 9);
it cites them and records the decision against each row. It does not design 4a's context chain or 4b's recovery
primitives; it names the types it consumes and the contract it relies on, and **4a's and 4b's names win** wherever
they differ (the plan re-derives them from their merged code, the 3a/3b precedent). It does not rewrite the redirect or
retry behaviour (6b, 6a) or auth stamping (6c); it moves those policies onto the new signature and the seed origin.

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor | Kind | State at `0332cef` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030` with `BannedSymbols.txt`, `MA0051` at 70 lines, `CA2007` on `src/`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. 4c is written under every gate. The sync-over-async entries in `BannedSymbols.txt` (`ValueTaskAwaiter.GetResult` and kin) bind the real sync path: every blocking site is a documented bridge behind a `#pragma` citing design §5.3. |
| Phase 1, S3, S6, S8 | **dependency** (constraint 5) | Met. `RedirectCredentialHygieneTests`, `ReDriveRequestIsolationTests` and `EnsureSuccessErrorMappingTests` (Core, `Security`) and `RedirectWireTests` (SystemNet, `Security`) are the classes 4c keeps green (roadmap phase-1 hand-off table, row 4c). [Keeping the `Security` classes green](#keeping-the-security-classes-green) says how, class by class. |
| 2b (the card's entry criterion: "4c needs the new SPI signature") | **dependency** | Met. `IAsyncHttpClient.ExecuteAsync(Request, RequestOptions, CancellationToken)` and `IHttpClient.Execute(…)`; `RequestOptions`; the bridges `AsAsync(TaskScheduler)`/`AsBlocking` whose tests `PIPE-33`/`PIPE-34` cite; `SeamImplementationArchitectureTests`, whose allow-list 4c extends with `HttpPipeline` (2b hand-off); `PipelineRunnerTests.The_transport_receives_RequestOptions_Empty`, which 4c replaces (2b hand-off). |
| 3a | **dependency** | Met. `StreamCopy.DrainUpTo`/`DrainUpToAsync` (the pooled, capped drain `ErrorBodyBuffer` uses), the sync body members (`OpenRead`, `ReadAsBytes`, `WriteTo`, `ToReplayable`) the real sync path calls. |
| 3b (the card's entry criterion: "the dispose latches") | **dependency** | Met. `Response`/`ResponseBody` dispose at most once (so `PIPE-31`'s "idempotent double-close tolerated" and `RECOV-12`'s "exactly once" are free), `Disposal.DisposeQuietly(Async)` (the 3b hand-off moving `EnsureSuccessAsync`'s `finally` onto it is taken by 4b with the re-home, P4b-16). |
| **4a** (context chain) | **dependency** for PR 2 onward | Not in the tree. `PipelineContext` is the call-scoped carrier over 4a's chain and store ([consumed types](#the-4a-and-4b-types-4c-consumes)). |
| **4b** (recovery primitives) | **dependency** for PR 2 (`ExceptionFacts`, `ExceptionTrail`) and PR 4 (`Outcome`, the folds, `ErrorMappingStep`, `ErrorBodyBuffer`) | Not in the tree. `ErrorMappingPolicy` sits over 4b's `ErrorMappingStep` and `Outcome` folds; the runtime's fatal filter and the quiet-dispose trail are 4b's; `RECOV-32`/`RECOV-33`'s policy defaults are 4b's and 4c only wires them. |
| A phase 4 segmentation design | **convenience**, with a stated substitute | Absent. The segmentation rule applies (phase 4 spans ch.07 and ch.08). The card already names each sub-phase's build list; P4c-1 states the row split and P4c-2 the order. |

**The order is 4a → 4b → 4c, and for 4c it is a dependency, not a convenience (P4c-2, open for the lead).** The roadmap's
segmentation bullet says "4a / 4b / 4c. This is a convenience, as Ruby found: no `RECOV` or `PIPE` requirement consumes
`CTX`", with one caution that 4c's `PipelineContext` carries 4a's bundle. That is true of the *requirements*, and Ruby's
segmentation design proved it for Ruby. It is not true of the *.NET design*, which makes 4c build against two concrete
artefacts:

- **4a:** design §5.4 puts the correlation context on `PipelineContext` ("the correlation context travels explicitly on
  `PipelineContext`"), and §5.4's store needs someone to promote the chain at call start and close it at call end. The
  only code that sees a call start and end is `HttpPipeline.SendAsync`, which 4c rewrites. 4c consumes `DispatchContext`,
  `RequestContext`, `ExchangeContext` (and their base `CallContext`), `CallKey`, `InstrumentationContext` and the store over
  `BoundedMap` (through the promotions only).
- **4b:** the 4c card says `ErrorMappingPolicy` "makes S8 structural", and the orchestration contract is 4b's: it sits
  over 4b's `ErrorMappingStep` through the `Outcome` response fold, its dispatch unwraps with 4b's rethrow, and the runtime's
  fatal filter (`ExceptionFacts.IsFatal`, `PIPE-30`) and quiet-dispose trail (`ExceptionTrail.AddSuppressed`, `PIPE-40`,
  `PIPE-31`) are 4b's types.

The roadmap's own rule applies: "If a phase finds a dependency edge that inverts this order, it records the edge in a dated
status note here **and** in each affected phase's Prerequisite section, and the phases run in the real order." This section
is 4c's half; the roadmap status note is a [close-out correction](#design-and-roadmap-corrections-owed-at-close-out).
4a and 4b keep their own independence of each other and of 4c. **4c's PR 1 (the builder, which touches no 4a or 4b type)
may land before 4a and 4b exit;** every later PR waits ([landing order](#landing-order)).

### The 4a and 4b types 4c consumes

Each is stated as the **contract** 4c relies on. The names are the card's; the member names are placeholders; **4a's and
4b's merged code wins** wherever it differs, and the plan re-derives every call site from it. If 4a or 4b declines an
item, the row that consumes it here reopens.

| Type (owner) | Contract 4c relies on | Consumed by |
|---|---|---|
| `CallKey` (4a, `readonly record struct`, design §5.4) | A per-call unique key minted once per call (`CTX-4`); cheap to copy; no string allocated unless rendered. | `PipelineContext.CallKey` |
| `InstrumentationContext` (4a, the bundle over `ActivityContext`, `CTX-14`/`CTX-15`) | `InstrumentationContext.None` and `FromActivity(Activity?)`; 5c populates, nobody redefines. | `PipelineContext.Instrumentation`; the dispatch at call entry |
| `CallContext` (4a, the abstract base record of the three links) | `Key`, `Instrumentation`, and `Close()`: evicts iff this link is the store's occupant (`CTX-9`, `CTX-10`), never throws. **The name is 4a's**, so `PipelineContext` exposes no member called `CallContext`. | `HttpPipeline`'s failure path |
| `DispatchContext` (4a) | Constructible at call start **without** registering in the store (`CTX-17`); carries the key and the bundle. `PromoteToRequest(Request, string? operationName)` registers the returned link (`CTX-2`, `CTX-16`, `CTX-17`, `SEAM-28`); promoting the same dispatch again is legal and the later link takes the slot. | `HttpPipeline` call entry; `PipelineRunner`'s terminal |
| `RequestContext` (4a) | The promoted link, carrying the request actually sent; `PromoteToExchange(Response)` registers the terminal link. | `PipelineRunner`'s terminal |
| `ExchangeContext` (4a) | Terminal link (no promotion API, `CTX-1`); disposing the `Response` it carries closes it (design §5.4) through an internal hook on `Response` **that 4c adds** (4a's proposal, item 3). | `PipelineRunner`'s terminal; `Response` |
| The store over `BoundedMap` (4a) | Process-wide, bounded (`CTX-11`–`CTX-13`), single-winner insert at promotion, identity-conditional remove at close. **4c never calls the store** (4a's P4a-6): promotion registers. | — (through the links) |
| `Outcome` with `Success(Response)` / `Failure(Exception)` and `Match` (4b, design §5.2) | Closed hierarchy; `Match` is the one fold every chain uses. No `Outcome` crosses into a `PIPE` signature: a policy returns a `Response` or throws. | `ErrorMappingPolicy` |
| `IResponseStep`, `ResponseRecoveryChain` (4b) | A response chain over a defensively copied step list (`RECOV-14`) whose apply runs response steps on `Success` only (`RECOV-4`), turns a throwing step into `Failure` (`RECOV-7`), never throws (`RECOV-8`), and closes the in-hand response exactly once when a step throws while holding it (`RECOV-12`), through the one helper; sync and async apply. A dispatch that returns the response on `Success` and rethrows the contained exception **unchanged** on `Failure` (`RECOV-10`). | `ErrorMappingPolicy` ([position G](#g-errormappingpolicy-over-4bs-outcome-folds)) |
| `ErrorMappingStep : IResponseStep` and the internal `ErrorBodyBuffer` (4b, P4b-16, open there) | `ErrorMappingStep.Instance` maps 400..599 to `HttpResponseException` over a buffered copy and returns every other status by reference with the body untouched (`RECOV-15`); `ErrorBodyBuffer.Capture`/`CaptureAsync` drain at most `Response.MaxBufferedErrorBytes` through `StreamCopy`, dispose the original through `Disposal`, and return a replayable copy (`RECOV-16`). 4b also re-homes `Response.EnsureSuccessAsync` onto it. | `ErrorMappingPolicy`; `Response.EnsureSuccess` |
| `ExceptionFacts.IsFatal` / `EnumerateCauses` (4b) | `IsFatal(ex)` is `true` for `OutOfMemoryException` and subtypes (design §10 entry 12); `EnumerateCauses` is the bounded, cycle-safe walk (`XCUT-9`). | the runtime's catch filters (`PIPE-30`, `PIPE-31`), `HttpPipeline`'s context-close-on-failure |
| `ExceptionTrail.AddSuppressed` / `GetSuppressed` (4b) | Attaches a secondary failure to a primary one without wrapping (design §10 entry 13); self-suppression guarded. `Disposal`'s primary branch is repointed onto it by 4b (3b hand-off). | `PIPE-40`'s superseded-response release, `PIPE-31`'s handler-failure dispose, through `Disposal` |
| `IdempotencyPolicy`, `ClientIdentityPolicy` with `RECOV-32`/`RECOV-33`'s defaults (4b) | Parameterless constructors give the specification's defaults (methods POST/PUT/PATCH and respect-existing; Append mode). 4b may change them on the as-built signature; 4c moves them onto the new signature mechanically in PR 2 and wires them in PR 5. | `DexpacePipeline.CreateDefault` |

**4c's hand-offs back to 4a and 4b: none that block them.** 4c consumes. Two files are touched by both 4b and 4c
(`IdempotencyPolicy.cs`, `ClientIdentityPolicy.cs`): 4b changes their defaults, 4c changes their signature; 4b lands
first and 4c's PR 2 re-derives the signature change on 4b's merged code, never resolving hunks (the 2a/2b precedent).
`Response.cs` is touched by 4b (the `EnsureSuccessAsync` re-home) and 4c (the exchange-close hook and `EnsureSuccess`), in
that order.

### Observed conflicts with the 4a and 4b designs, 2026-10-05 (read at the end of this brainstorm)

The 4a and 4b designs appeared in the inbox (`docs/superpowers/specs/2026-10-05-phase4a-context-design.md`,
`…-phase4b-recovery-design.md`) while this one was being written. They were read **only** to check the interface above;
nothing in them is re-decided here. Three points changed this design, and the rest agreed:

1. **The promotion is per transmission, not per call.** 4a's "interface 4a hands to 4c" proposes one `DispatchContext` per
   call, `PromoteToRequest` per transmission (each attempt or hop, with the request actually sent), `PromoteToExchange` on the
   response with disposal closing it through a hook 4c adds on `Response`, the furthest link closed on failure, and the
   pipeline never calling the store. This design first had one promotion per call with the seed request; it **confirms 4a's
   proposal instead** ([position I](#i-the-context-chain-wiring-p4c-21)), because a `DelegatingHandler` resolving the live call
   by key (design §5.4's reader) should see the request actually on the wire.
2. **`CallContext` is 4a's abstract base record**, so `PipelineContext` exposes `CallKey` and `Instrumentation`, not a member
   of that name, and no link with a public promotion API (a policy that could promote would corrupt the chain).
3. **The error-body buffer and the mapping step are 4b's (4b's P4b-16, open for the lead there).** 4b builds `ErrorBodyBuffer`
   (sync and async), `ErrorMappingStep` and the `EnsureSuccessAsync` re-home, because its step needs a sync capture and
   `RECOV-16` wants one shared bound; it leaves 4c `ErrorMappingPolicy` as a thin `PerCall` wrapper, `XCUT-8`'s factory
   rejection, `PIPE-37`'s placement and `BODY-30`'s no-body clause on the policy. **This design adopts that split** (P4c-17,
   open for the lead jointly with P4b-16). Its fallback, if the lead keeps P3a-13's routing, is stated in P4c-17.

Both agree with this design on the census (20 + 34 + 40), on `RECOV-32`/`RECOV-33` being 4b's rows that 4c wires, on
4c depending on 4a and 4b and not the reverse, and on `PIPE-32`'s reversal being recorded on 4c's row under
`async-redirect-pillar`.

---

## Governing documents, and the phase-start queries

- **Normative.** Every row's canonical text is its appendix-C row; `docs/product-spec/08-execution-pipelines.md` §8.1
  states `PIPE-1`–`PIPE-40` (no `PIPE` ID is a gap ID: `--gaps PIPE` reports 0 of 40).
- **Design.** §5 (the two-layer prohibition), §5.1 (the step class, the stage table, composition rules, the
  request-in/response-out signature, `PipelineContext`'s call-scoped shape, the three non-pillar steps, the bounded error
  copy, `PIPE-40`), §5.3 (one implementation per policy, `PIPE-29`–`PIPE-31`, the `PIPE-32` reversal, the bridges,
  flatten/nest, `PIPE-26`/`PIPE-27`), §5.4 (the correlation context on `PipelineContext`, no SDK `AsyncLocal`), §6.2 (the seed
  origin, `REDIR-11`/`REDIR-24`), §10 entries 8 (`cooperative-cancellation`), 12 (the fatal set), 13 (the trail), 14 (the
  async redirect pillar), 15 (no marker), §11 items 16, 29, 31, 33, §12's `PIPE` row.
- **Styleguide.** `09-concurrency.md` 9.1 (no blocking on async outside a documented bridge), `10-api-design.md` 10.1
  (minimal surface) and 10.5 (token last), `13-resource-management.md` 13.4 (never dispose an injected dependency) and 13.6,
  `08-error-handling.md` (BCL exception types for argument and state errors). The overlay's `Async`-suffix and `I`-prefix
  departures stand (the sync members are the un-suffixed twins).
- **Siblings.** `nodejs-sdk@c0ff3fd` `packages/core/src/pipeline/` (local: `stage.ts`, `builder.ts`, `cursor.ts`,
  `runtime.ts`, `errors.ts` and their tests). `ruby-sdk`'s gems are **not** in the local clone (as 2a, 2b, 3a and 3b found):
  `ruby-sdk/gems/dexpace-core/test/dexpace/pipeline/`, `pipeline_test.rb` and `ruby-sdk/docs/sdk-documentation/pipelines.md`
  do not exist locally (`docs/sdk-documentation/` holds only `architecture.md`). Ruby's case lists are taken from
  `ruby-sdk/docs/work/mvp/phase4/phase4c/2026-09-08-phase4c-stage-pipeline-design.md` ("Testing strategy") and its phase 4
  segmentation design, both local. The plan records the absence, as 3a did.

| Query, run 2026-10-05 | Result |
|---|---|
| `scripts/knowledge --origin note --brief` | five notes (`CA1062`, `I` prefix, `Async` suffix, Shouldly, `LangVersion`); none touches the pipeline. The `Async`-suffix note is load-bearing here: `Process`/`Send`/`Run` are the sync twins of `ProcessAsync`/`SendAsync`/`RunAsync`. |
| `scripts/knowledge --section conflicts --brief` | fifteen entries across nine topics; every one `[conformed]` or `[kept]` with a note. None is open, so 4c inherits no conflict. |
| `scripts/knowledge --prefix-info PIPE` | 40 canonical IDs, 36 MUST, 4 SHOULD (`PIPE-3`, `PIPE-35`, `PIPE-36`, `PIPE-39`); owning chapter `08-execution-pipelines.md`; 40 of 40 substantive; topics `pipeline`, `concurrency-and-async`, `redirect-handling`. |
| `scripts/knowledge --gaps PIPE` | 0 of 40 IDs without a substantive entry. |
| appendix C, read directly | `PIPE-1`–`PIPE-40`, `REDIR-11`, `REDIR-24`, `REDIR-25`, `RETRY-44`, `RECOV-1`–`RECOV-16`, `RECOV-32`, `RECOV-33`, `BODY-30`, `BODY-31`, `HTTP-52`, `RETRY-36`, `XCUT-8`, `SEAM-14`–`SEAM-16`, `XCUT-22`, `ASYNC-13`. |
| design §10, read directly | entry 14 is the async-redirect-pillar deviation; it has **no topic label yet**. This design coins nothing new: the card names the topic `async-redirect-pillar`, and P4c-20 attaches it to entry 14 by dated correction (the 3a precedent with `eof-is-zero-read` on entry 4). |

---

## Scope and the 94-row census

**4c owns 40 rows: `PIPE-1`–`PIPE-40` (36 MUST, 4 SHOULD).** Nothing else.

The phase 4 exit is 94 rows (card). Derived from Phase List row 4 and appendix C, with no overlap:

| Sub-phase | Rows | Count | Notes |
|---|---|---|---|
| 4a | `CTX-1`–`CTX-20` | 20 | ch.07. The card's "one shape" note (`CTX-14`/`CTX-15` with `OBS-25`/`OBS-26`) adds **no** row to phase 4: `OBS-1`–`OBS-40` are phase 5's (Phase List row 5), so 4a fixes the shape on its `CTX` rows and 5c's `OBS` rows populate it (coupling obligation 1). |
| 4b | `RECOV-1`–`RECOV-34` | 34 | ch.08 §8.2 and appendix C. `RECOV-17`–`RECOV-30` and `RECOV-34` (15 rows) are ⏳ against 6a (card exit). `RECOV-32`/`RECOV-33` (the idempotency and client-identity defaults) are **4b's** rows; 4c consumes them in `CreateDefault` and owns neither. `RECOV-15`/`RECOV-16` are 4b's rows, built by 4b (`ErrorMappingStep`, `ErrorBodyBuffer`); 4c's policy wraps them (P4c-17). |
| 4c | `PIPE-1`–`PIPE-40` | 40 | ch.08 §8.1. |
| **Total** | | **94** | 20 + 34 + 40 = 94, each ID once. |

**No non-`CTX`/non-`RECOV` row besides `PIPE` falls in phase 4.** The orchestrator's candidates were checked against the
Phase List: `REDIR-11`, `REDIR-24` and `REDIR-25` are ch.10, which row 6 gives to 6b (`REDIR-1`–`REDIR-28`). 4c does the
seed-origin work coupling obligation 2 assigns it and records the `REDIR-25` reversal under §10, and 6b's rows cite 4c's
tests ([work 4c does on other rows](#work-4c-does-on-rows-other-phases-own)). The arithmetic therefore reaches 94 exactly
with this partition, and nothing about it is open.

---

## Verified facts that shape the decisions

Verified on 2026-10-05 on the pinned SDK (10.0.401) with a throwaway program in the scratchpad, never in the repository,
or by reading the as-built code at `0332cef`. Rows and rulings cite them by number.

1. **An `async ValueTask<T>` method that never suspends completes synchronously.** Called with its `async` flag `false`, a
   method whose only `await` sits behind that flag returned a `ValueTask` with `IsCompletedSuccessfully == true`. This is
   the precondition of §5.3's `ProcessCoreAsync(…, bool async)` idiom: the sync `Process` can read the result of an
   already-completed task without ever blocking.
2. **The same method throwing on the non-suspending path returns a faulted task; it does not throw at the call.**
   `IsFaulted == true`. The sync wrapper must therefore surface the fault (rethrow the original via `GetAwaiter().GetResult()`
   on a *completed* task, which unwraps), not assume success.
3. **`Enum.IsDefined` is `false` for an undefined value of the enum type.** `(PipelineStage)800` is a legal value of the
   type and is not a stage; the builder must reject it explicitly (`PIPE-8`), because "`SEND` is not an enum member" only
   holds for named members.
4. **A policy that overrides `Equals` is `Equals` and not `ReferenceEquals`.** `PIPE-6`'s "reference identity, not value
   equality" is a live trap for any policy that overrides `Equals`/`GetHashCode`. It cannot be a `record`: `HttpPipelinePolicy`
   is a plain abstract class and a record may inherit only from `object` or another record (CS8864; design §5.1's "written as
   a record" is not a compilable shape). The test fixture is two instances of a sealed class with value-based `Equals`.
5. **`Enumerable.OrderBy` is stable.** Equal keys keep insertion order (`a,d,b,c` from keys `2,1,2,1` over `b,a,c,d`), which
   is the as-built `Build`'s mechanism for `PIPE-7` and `PIPE-22`.
6. **A virtual `Stage` getter can return a different value on each read.** A policy whose `Stage` flips between reads is
   legal C#. The as-built builder reads `Stage` at sort time and again per pillar during validation; a flipping policy can
   pass validation in one slot and run in another. The builder must read it once.
7. **A `default` literal binds to the `CancellationToken` overload.** With `M(string, CancellationToken)`,
   `M(string, Opt, CancellationToken)` and `M(string, Cli, CancellationToken)` in scope (two reference-type
   option parameters), `M("r", default)` compiled and chose the first. The public-API analyzers forbid the optional token on
   more than one overload (`RS0026`) and on any overload that does not have the most parameters (`RS0027`), so no overload carries one. Adding `RequestOptions` overloads beside the existing
   `DexpaceClientOptions` one does not make an existing call ambiguous.
8. **The `PerCall` rename moves every existing `PipelineStage.PerCall` user outside the redirect loop.** Read from the code:
   the as-built `PerCall = 250` sits between `Redirect = 200` and `Retry = 300`; design §5.1 gives `PerCall` the value 150
   (outside redirect) and the old slot the new name `PerHop = 250`. Source that says `PipelineStage.PerCall` keeps compiling
   and silently changes loop. Source sites: `IdempotencyPolicy` and `ClientIdentityPolicy` (intended: they move, P4c-23). Test sites: `PipelineBuilderTests` (lines 35, 58, 70, 82), `IdempotencyPolicyTests:31`, `ClientIdentityPolicyTests:31` (the plan enumerates them by `grep -rn "PipelineStage.PerCall" tests`, task 1.1), and
   `ReDriveRequestIsolationTests.A_redirect_hop_is_not_built_from_the_previous_hops_stamped_request`'s probe, which must sit
   **inside** the redirect loop to observe two hops: left as `PerCall` it would record one entry and fail
   `Assert.Equal([null, null], …)` (P4c-22).
9. **Every core `Security` class that builds a pipeline calls `SendAsync(Request, DexpaceClientOptions, CancellationToken)`**
   (`ReDriveRequestIsolationTests`, `RedirectCredentialHygieneTests`, `AuthHttpsGuardTests`), as do SystemNet's
   `RedirectWireTests`, `Pageable.Create` and the AOT smoke. Keeping that overload keeps all of them compiling (P4c-15), but only with its token argument: it cannot keep `= default` (position F, `RS0026`/`RS0027`), so the two-argument callers change in PR 3: `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs:112`, `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/InstrumentationPolicyTests.cs:49`, `src/Dexpace.Sdk.Core/README.md:40` and `src/Dexpace.Sdk.Serialization.SystemTextJson/README.md:34`. None is a Security class.
10. **`AuthHttpsGuardTests.CountingAuthPolicy` overrides `AuthorizationPolicy.GetCredentialAsync(PipelineContext)`.** Keeping
    that protected signature keeps the class unedited (P4c-13, P4c-22).
11. **`SeamImplementationArchitectureTests` rejects any *public* core type implementing a seam before it consults the
    allow-list** ("`{name}` is public"). `HttpPipeline` is public by design, so the test needs a public exemption, not only an
    allow-list entry (P4c-14).
12. **`ArrayPool<T>.Rent` is banned in `src/` (P3a-8).** `ErrorBodyBuffer` (4b's) cannot rent its own chunk as design §5.1 says; it
    drains through `StreamCopy.DrainUpTo(Async)`, which already rents through the sanctioned `PooledChunk`.

---

## Decisions, one per requirement row

**How to read the table.**

- **Exit** uses the roadmap's constraint-3 legend. A combined mark (`✅; ⏳ 8b`) is used as 2b and 3a used it: each clause
  has its own mark and the row says which is which.
- **Tests** are `[Trait("Category", "Unit")]` in `tests/Dexpace.Sdk.Core.Tests/Pipeline/` (namespace `…Tests.Pipeline`)
  unless another category or place is named. "Pin" adds a test for behaviour that is already correct.
- **Doubles** (new, `tests/Dexpace.Sdk.TestSupport/Pipeline/`): `ProbePolicy` (records enter/exit per stage into a shared
  log, sync and async), `ForkingProbe` (a probe that drives `next` *n* times, disposing each superseded response, returning
  the last unclosed), `ShortCircuitPolicy`, `ValueEqualPolicy` (a sealed class with value-based `Equals`/`GetHashCode`, fact 4), `TrackingResponseBody` (records open and dispose events into a shared ordered log; overrides `Dispose(bool)`), `FlippingStagePolicy` (fact 6),
  `ThrowingSynchronouslyPolicy` (a non-`async` override that throws before returning). The as-built
  `TestSupport/Transports` fakes (`ScriptedTransport`, `RecordingTransport`) are reused; they gain the sync `Execute`.
- **PR** points to [the landing order](#landing-order).

| ID | Level | Decision | Rationale / source | Types affected | Test approach | PR | Exit |
|---|---|---|---|---|---|---|---|
| `PIPE-1` | MUST | **Built** (as built: stable sort; now over entries recorded at `Add`). Cross-stage order is the enum's numeric order, independent of insertion; within a non-pillar stage, insertion order. | §5.1 "Stages, reconciled"; facts 5, 6; P4c-8. | `PipelineBuilder`, `PipelineEntry` | `StageOrderTests.One_probe_per_stage_runs_in_stage_order_whatever_the_insertion_order`: one `ProbePolicy` per stage (nine), added in a shuffled order under a pinned seed; the entry log equals the enum order and the exit log is its exact reverse (the Ruby 4c design's `PIPE-1` conformance case). | 1 | ✅ |
| `PIPE-2` | MUST | **Built.** Outer to inner: `Operation`, `PerCall` (the spec's pre-redirect slot), `Redirect`, `PerHop`, `Retry`, `PerAttempt`, `Auth`, `Diagnostics` (LOGGING), `Serde`, then the transport. `PerCall` runs outside both loops and sees only the terminal response. `Operation` is a .NET singleton outside `PerCall` (P4c-6). | §5.1 table; P4c-6. | `PipelineStage` | `StageOrderTests.A_per_call_probe_runs_once_and_sees_the_final_response_while_an_auth_probe_runs_per_hop`: a `ForkingProbe` at `Redirect` driving twice; a probe at `PerCall` is entered once and observes the final response by identity; a probe at `Auth` is entered twice. Same shape with the forker at `Retry` and a probe at `PerHop` (once) versus `PerAttempt` (twice). | 1, 2 | ✅ (P4c-6) |
| `PIPE-3` | SHOULD | **Met in part, by reading.** Keys are sparse (100 apart, with 150/250 between); a user slot sits outside both loops (`PerCall`), between redirect and retry (`PerHop`, which is both the spec's `POST_REDIRECT` and `PRE_RETRY`, adjacent with nothing between them) and between retry and auth (`PerAttempt`). No user slot after `Auth`, `Diagnostics` or `Serde`. Adding one later is additive because the keys are sparse. | §5.1; P4c-7. | `PipelineStage` | `StageOrderTests.Stage_values_are_strictly_increasing_and_sparse` (reflection over the enum: declaration order equals numeric order, gaps ≥ 50). | 1 | ✅ (P4c-7) |
| `PIPE-4` | MUST | **Built.** Pillars: `Operation`, `Redirect`, `Retry`, `Auth`, `Diagnostics`, `Serde` (new, reserved, no shipped policy). Each admits one policy. | §5.1; P4c-6, P4c-9. | `PipelineStage`, `PipelineStageFacts` (renamed from `PipelineStageHelper`) | `PillarRuleTests.Each_pillar_admits_one_policy` (`[Theory]` over the six); `…A_custom_policy_may_occupy_the_serde_pillar`. | 1 | ✅ |
| `PIPE-5` | MUST | **Built, at `Add`** (as built: at `Build`, naming the stage and a count). A distinct second policy on an occupied pillar, via `Add`, `Prepend`, `InsertBefore`/`InsertAfter`, `AddRange`/`PrependRange` or `Flatten`-then-add, throws `InvalidOperationException` naming the stage, **both runtime types**, and `Replace<T>`. `Replace<T>` itself never collides (1:1 within the stage; cross-stage is `PIPE-19`'s error). | §5.1 "Composition rules"; P4c-9, P4c-10. | `PipelineBuilder` | `PillarRuleTests.A_second_distinct_policy_on_a_pillar_fails_at_add_naming_both_types` (`[Theory]` over every insertion path; message contains both type names and `Replace`); `…Replace_swaps_a_pillar_occupant_without_a_collision`. | 1 | ✅ |
| `PIPE-6` | MUST | **Built** (as built: throws at `Build`). Re-adding the **same instance** to its pillar is a no-op, decided by `ReferenceEquals`. | §5.1; fact 4; P4c-9. | `PipelineBuilder` | `PillarRuleTests.Re_adding_the_same_instance_is_idempotent` (entries unchanged); `…Two_value_equal_policies_are_distinct` (`ValueEqualPolicy` pair: asserts `Equals` and `!ReferenceEquals` in the same test, then the second `Add` throws — the half that fails against an `Equals`-based check). | 1 | ✅ |
| `PIPE-7` | MUST | **Built.** `Add` appends within the stage; **`Prepend` is new** and puts the policy at the head of its stage. Order within a stage survives `Build` and every edit. Re-adding the same instance to a non-pillar stage appends it again (the letter of `PIPE-7`; `PIPE-6` speaks of pillars only). | §12 notes `PIPE-7` is not cited by §5.1; P4c-9, P4c-10. | `PipelineBuilder` | `NonPillarOrderTests.Append_adds_to_the_tail_and_prepend_to_the_head`; `…Order_within_a_stage_survives_an_edit_in_another_stage`. | 1 | ✅ |
| `PIPE-8` | MUST | **✅ by type, plus one check.** `SEND` is not an enum member, so no named stage can hold a user policy; the transport is the fixed terminal captured at `Build`. `Add` and every insertion path **reject an undefined `PipelineStage` value** with `ArgumentException` (fact 3). Flattening never sees a transport entry (there is none). | §5.1; fact 3; P4c-8. | `PipelineBuilder` | `PillarRuleTests.A_policy_reporting_an_undefined_stage_is_rejected` (`(PipelineStage)800`, `(PipelineStage)0`). | 1 | ✅ |
| `PIPE-9` | MUST | **Built.** An empty pipeline dispatches straight to the transport, threading the caller's `RequestOptions` and token, and returns its result. The SHOULD half: the runner is a struct, and the call allocates one `PipelineContext` and its shared state, which §5.1 already accepts. | §5.1; P4c-14. | `PipelineRunner`, `HttpPipeline` | `EmptyPipelineTests.An_empty_pipeline_passes_request_options_and_token_to_the_transport` (`Assert.Same` on the request, the options and equality on the token), sync and async. Replaces `PipelineRunnerTests.The_transport_receives_RequestOptions_Empty` (2b hand-off). | 3 | ✅ |
| `PIPE-10` | MUST | **Built.** `HttpPipeline` holds an immutable entry array, the two terminal transports and the build-time client options; each send creates its own `PipelineContext`; the runner is a struct. | §5.1; P4c-11. | `HttpPipeline` | `ConcurrencyTests.Concurrent_calls_share_no_per_call_state` (64 parallel sends through one pipeline with a probe that records `context` identity and the attempt ordinal; no context instance is seen by two calls). Pin: `HttpPipeline` has no settable member (reflection). | 2 | ✅ |
| `PIPE-11` | MUST | **Built.** Policies are shared across calls; per-call state lives on `PipelineContext` (its typed property bag and per-drive values), never on the policy. Every shipped policy keeps only `readonly` fields. | §5.1; P4c-4. | every shipped policy | `ConcurrencyTests.Every_shipped_policy_holds_only_readonly_instance_fields` (reflection over `Dexpace.Sdk.Core.Pipeline.Policies`); the `PIPE-10` concurrency test drives the default pipeline. | 2 | ✅ |
| `PIPE-12` | MUST | **Built, structurally.** A policy receives the request, may call `next`, and returns the response it chooses; a short-circuit is returning a synthetic `Response` without calling `next`. | §5.1 signature; P4c-3. | `HttpPipelinePolicy` | `PolicyContractTests.A_short_circuit_returns_its_synthetic_response_and_reaches_nothing_downstream` (downstream probes entered zero times, transport `CallCount == 0`, `Assert.Same` on the synthetic response), sync and async; `…A_policy_may_substitute_the_response_on_the_way_out`. | 2 | ✅ |
| `PIPE-13` | MUST | **Built.** `next.RunAsync(request, context)` invokes the policy at the next index; past the last, the transport, with `context.RequestOptions` and `context.CancellationToken`. Forward-only within one drive by construction (the struct's index is immutable). | §5.1 "The runner already forks for free"; §12 notes `PIPE-13` is not cited. | `PipelineRunner` | `RunnerTests.Next_dispatches_to_the_transport_after_the_last_policy_with_the_callers_options`; pin: `PipelineRunner` has no mutable field (reflection). | 2 | ✅ |
| `PIPE-14` | MUST | **Built, structurally.** The request a policy passes to `next` is the request every downstream policy and the transport receive. | §5.1. | `PipelineRunner` | `PolicyContractTests.A_substituted_request_reaches_every_downstream_policy_and_the_transport` (`Assert.Same` at each probe and at the transport; the original is never seen downstream). | 2 | ✅ |
| `PIPE-15` | MUST | **✅ by construction.** The fork primitive is the `PipelineRunner` value itself: each `RunAsync`/`Run` call on the same value is an independent drive from the same position, so there is no advanced handle to misuse. Every shipped re-driver (redirect, retry) calls `next` once per drive with the request it holds. No single-use latch and no pillar-only restriction (P4c-12). | §5.1 (P7's hidden precondition, resolved by the signature); P4c-12. | `PipelineRunner`, `RedirectPolicy`, `RetryPolicy` | `ForkTests.Driving_the_same_runner_twice_re_runs_the_whole_downstream_tail` (`ForkingProbe` at `Retry`, counting probes at `PerAttempt` and `Auth`: each entered exactly twice). | 2 | ✅ (P4c-12) |
| `PIPE-16` | MUST | **✅, now structural (S6).** A fork resumes at the same position, carries the request the forker passes (never a downstream mutation, because nothing downstream can write upward), shares the call-scoped state, and advances independently. Per-drive values travel by copy (`context.ForAttempt(n)`, `ForHop(n)`). | §5.1; S6; P4c-3, P4c-4. | `PipelineRunner`, `PipelineContext` | `ForkTests.Each_fork_carries_the_request_its_forker_passed` and `…Forks_share_call_scoped_state_and_not_per_drive_state`. The `Security` evidence is `ReDriveRequestIsolationTests` (S6; moved mechanically, P4c-22) plus its new structural fact `The_context_offers_no_way_to_write_the_request_upward`. | 2 | ✅ |
| `PIPE-17` | MUST | **Built** (as built: the runner passes `RequestOptions.Empty`). The caller's `RequestOptions` are fixed on the call-scoped state at entry, carried by reference across every fork, readable as `context.RequestOptions`, and threaded into the terminal dispatch. | §5.1, §5.3; 2b position D; P4c-15. | `PipelineContext`, `PipelineRunner`, `HttpPipeline` | `OptionsFlowTests.The_same_request_options_instance_reaches_every_policy_every_fork_and_the_transport` (`Assert.Same` at probes on both sides of a `ForkingProbe` and at the transport; `Assert.Equal` would pass against a per-fork copy). | 3 | ✅ |
| `PIPE-18` | MUST | **Built** (as built: a cross-stage insert is silently re-bucketed). `InsertBefore<T>`/`InsertAfter<T>` place the policy next to the **first** instance of `T`; a policy whose stage differs from the anchor's throws `ArgumentException` naming both stages. | §5.1; P4c-10. | `PipelineBuilder` | `SurgicalEditTests.Insert_relative_to_an_anchor_in_another_stage_is_rejected` (both directions; builder entries unchanged after the throw); pins for the same-stage placement (as built). | 1 | ✅ |
| `PIPE-19` | MUST | **Built** (as built: `Replace<T>` accepts any stage). `Replace<T>` swaps the first instance of `T`; a cross-stage replacement throws `ArgumentException`. A pillar replace never trips `PIPE-5`. | §5.1; P4c-10. | `PipelineBuilder` | `SurgicalEditTests.A_cross_stage_replace_is_rejected`; `…Replace_swaps_only_the_first_instance`. | 1 | ✅ |
| `PIPE-20` | MUST | **Already met**; pinned. `Remove<T>` deletes every instance and keeps the rest in order; a no-op when absent. | as built. | `PipelineBuilder` | Pin: `SurgicalEditTests.Remove_deletes_every_instance_and_preserves_order`; `…Remove_of_an_absent_type_is_a_no_op`. | 1 | ✅ |
| `PIPE-21` | MUST | **Already met**; pinned. A missing anchor throws `InvalidOperationException` naming `T`. | as built. | `PipelineBuilder` | Pin (existing `InsertAfter_TypeNotPresent_Throws` and kin, renamed into `SurgicalEditTests`, extended to `Replace<T>`). | 1 | ✅ |
| `PIPE-22` | MUST | **Built.** Every edit re-derives order from the recorded stage; the order after edits equals the order of a builder seeded from scratch with the resulting set. | §5.1; fact 6; P4c-8. | `PipelineBuilder` | `SurgicalEditTests.Edited_order_equals_order_built_from_scratch` (an `InsertAfter`, a `Remove` and a `Replace`; compares `Build(...).Policies` by identity, element by element). | 1 | ✅ |
| `PIPE-23` | MUST | **Built.** `AddRange`/`PrependRange` validate the whole batch against the builder **and against itself** before committing; a collision leaves the builder unchanged. `Flatten` seeds a fresh builder, so it cannot collide; adding to it afterwards follows `PIPE-5`. | §5.1; P4c-10. | `PipelineBuilder` | `BulkTests.A_colliding_batch_leaves_the_builder_unchanged` (captures `entries` before; asserts the throw; asserts equality by identity after) for a collision with an existing pillar and for two distinct pillar policies inside one batch. | 1 | ✅ |
| `PIPE-24` | MUST | **Built** (as built: `CreateDefault` builds from scratch only). `PipelineBuilder.AddStandardResilience(…)` installs `OperationPolicy`, `RedirectPolicy`, `RetryPolicy` and `InstrumentationPolicy` only into empty pillars, checks all four up front, and installs nothing if any is occupied. | §5.1; P4c-19. | `PipelineBuilder`, `DexpacePipeline` | `ResiliencePresetTests.The_preset_installs_nothing_when_any_target_pillar_is_occupied` (`[Theory]` over each of the four pre-occupied; entries unchanged); `…The_preset_fills_empty_pillars_and_leaves_non_pillar_stages_alone`. | 5 | ✅ |
| `PIPE-25` | MUST | **Built.** `Build` flattens in stage order (no `SEND` entry exists to skip) into an immutable array; `HttpPipeline.Policies` is a read-only ordered view (`ReadOnlyCollection<HttpPipelinePolicy>` over a private copy). | §5.1; §12. | `HttpPipeline` | `HttpPipelineTests.Policies_is_an_ordered_read_only_view` (order equals build order; the view does not implement a mutable `IList` setter path; mutating the builder after `Build` leaves the view unchanged). | 1 | ✅ |
| `PIPE-26` | MUST | **Built.** `HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient` (explicitly), delegating to its own `SendAsync`/`Send` with the seam's `RequestOptions` and token; options survive the indirection. 2b's allow-list gains a public exemption for it. | §5.3; 2b hand-off; fact 11; P4c-14. | `HttpPipeline`, `SeamImplementationArchitectureTests` | `PipelineAsTransportTests.A_pipeline_stands_in_for_a_transport_and_threads_options_through` (an outer pipeline over an inner one, `Assert.Same` on the inner transport's received `RequestOptions`); `…A_pipeline_backs_a_pageable` (through `Pageable.Create`). Architecture: the allow-list entry with the reason "PIPE-26". | 3 | ✅ |
| `PIPE-27` | MUST | **Built.** `Dispose`/`DisposeAsync` are no-ops toward the transport: the pipeline never owns it. No latch: a disposed pipeline stays usable (`SEAM-15` is a MAY; the pipeline documents its mode). | §5.3; §3.7; `XCUT-22`; P4c-14. | `HttpPipeline` | `PipelineAsTransportTests.Disposing_the_pipeline_twice_never_disposes_the_transport` (`DisposeCountingTransport`, both forms, both orders; a send after dispose still succeeds). | 3 | ✅ |
| `PIPE-28` | MUST | **✅ for the shared staging:** one `PipelineStage`, one sorted entry array, and `Run`/`RunAsync` index the same array; one implementation per shipped policy (`ProcessCoreAsync(…, bool async)`). **⏳ 8b for the real synchronous terminal** (coupling obligation 5): until 8b, `SystemNetHttpClient.Execute` is sync-over-async, so the sync path is real down to the transport and no further. | §5.3; roadmap coupling obligation 5; P4c-13. | `PipelineRunner`, every shipped policy | `SyncPathTests.Sync_and_async_sends_visit_the_same_policies_in_the_same_order` (one pipeline, probes recording which entry point ran: `Process` on the sync send, `ProcessAsync` on the async one); `…The_sync_send_never_calls_an_async_member_of_a_shipped_policy` (each shipped policy over a sync-only fake transport whose `ExecuteAsync` throws). | 2 | ✅; ⏳ 8b (task named by 8b's plan) |
| `PIPE-29` | MUST | **✅ by construction.** Policies written `async` cannot throw synchronously; every shipped policy is. Argument validation in a public entry (`ArgumentNullException.ThrowIfNull`) happens inside the `async` body and so faults the task, which `PIPE-29` permits either way. | §5.3. | shipped policies | `AsyncErrorModelTests.A_shipped_policy_reports_a_failure_as_a_faulted_task`. | 2 | ✅ |
| `PIPE-30` | MUST | **✅, with §10 entry 12's residual.** `PipelineRunner.RunAsync` is `async`, so a policy's synchronous throw (`ThrowingSynchronouslyPolicy`) and a null-returning transport both become faulted tasks. A fatal `OutOfMemoryException` is captured into the task by the runtime; no SDK frame catches it (every SDK catch is filtered with `ExceptionFacts.IsFatal`), which is the sense entry 12 records. | §5.3; §10 entry 12. | `PipelineRunner` | `AsyncErrorModelTests.A_synchronous_throw_from_a_policy_becomes_a_faulted_task` (calling `RunAsync` does not throw; awaiting it does, with the same instance); `…An_out_of_memory_exception_is_not_caught_by_any_sdk_frame` (the instance surfaces by identity with no `ExceptionTrail` entry). | 2 | ✅ (§10 entry 12) |
| `PIPE-31` | MUST | **Built.** `SendAsync<T>(request, handler, …)` applies the handler, then disposes the response (`await using`; the 3b latch makes a second dispose harmless); on a failure the original exception surfaces (`await` unwraps, verified in §5.3); a response produced before a handler failure is disposed, and a dispose failure is attached to the handler's exception through `ExceptionTrail` (4b). Sync twin `Send<T>`. | §5.3; P4c-16. | `HttpPipeline` | `TerminalMappingTests`: success disposes after the handler (`TrackingBody`); a throwing handler surfaces its own instance and the body is disposed; a throwing dispose after a throwing handler leaves the handler's exception primary with the dispose failure in `ExceptionTrail.GetSuppressed`; a transport failure surfaces unwrapped (not `AggregateException`); sync and async. | 3 | ✅ |
| `PIPE-32` | MUST | **Reversed, as designed.** The async standard pipeline installs `RedirectPolicy`, exactly as the sync one does: one `RedirectPolicy` serves both paths. **✅ for the documentation clause** ("a port MUST document this asymmetry"); **🚫 for the async no-follow**, recorded under design §10 entry 14, topic `async-redirect-pillar` (P4c-20). The invariant it protected, one layer follows redirects, is kept by the transport (phase 1 S3, 8b). | §5.3; §10 entry 14; §11 items 16, 29; P4c-20. | `DexpacePipeline`, `PipelineBuilder.AddStandardResilience` | `ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect` (a 307 then 200 through `CreateDefault`, sent with `SendAsync` and `Send`; both return the 200 and the transport saw two requests). | 5 | ✅ (documentation); 🚫 (`async-redirect-pillar`) |
| `PIPE-33` | MUST | **✅, citing 2b** (roadmap card, corrected 2026-09-29): the bridge is `AsAsync(this IHttpClient, TaskScheduler)` with no scheduler default; it runs the whole sync send as one opaque unit and threads options and token. 4c adds one end-to-end test over a pipeline. The interrupt clause is the cooperative reading, §10 entry 8 (`cooperative-cancellation`). | 2b checklist `SEAM-18`; §5.3; §10 entry 8. | — | Cite `SyncToAsyncBridgeTests` (2b). New: `PipelineBridgeTests.A_sync_pipeline_bridged_to_async_runs_as_one_unit_on_the_given_scheduler` (`RecordingTaskScheduler` counts one `QueueTask` for a five-policy pipeline; `Assert.Same` on the options at the transport). | 3 | ✅ (§10 entry 8 for the interrupt clause) |
| `PIPE-34` | MUST | **✅, citing 2b:** `AsBlocking(this IAsyncHttpClient)` blocks with `GetAwaiter().GetResult()` (unwraps, `ASYNC-13`) and threads options and token. Interrupt clause as `PIPE-33`. | 2b checklist; §5.3; §10 entry 8. | — | Cite `AsyncToSyncBridgeTests` (2b). New: `PipelineBridgeTests.An_async_pipeline_bridged_to_sync_preserves_options_and_surfaces_the_original_exception`. | 3 | ✅ (§10 entry 8) |
| `PIPE-35` | SHOULD | **Built.** Two named factories, never one overload: `PipelineBuilder.Flatten(HttpPipeline)` copies the policies, the transport and the client options into a fresh builder (they run in the same loops); `PipelineBuilder.Nest(HttpPipeline)` starts an empty builder whose transport is the pipeline itself (its loops are opaque). `Build()` without an argument builds over the seeded transport. | §5.3; P4c-11. | `PipelineBuilder` | `FlattenNestTests.A_probe_added_after_flatten_runs_inside_the_inner_loops_and_after_nest_runs_once` (inner pipeline with a `ForkingProbe` at `Redirect` driving twice; a new probe at `PerHop`: entered twice under `Flatten`, once under `Nest`). | 3 | ✅ |
| `PIPE-36` | SHOULD | **Built** (as built for auth). Every shipped policy class is `sealed`, and the one designed for extension, `AuthorizationPolicy`, seals `Stage`. A custom re-driving policy honours the fork contract by construction (`PIPE-15`). | §5.1. | shipped policies | `PillarRuleTests.Every_shipped_pillar_policy_locks_its_stage` (reflection: each public type in `…Pipeline.Policies` deriving from `HttpPipelinePolicy` is sealed or its `Stage` override is sealed). | 2 | ✅ |
| `PIPE-37` | MUST | **Built.** `ErrorMappingPolicy` (a thin wrapper over 4b's `ErrorMappingStep`) declares `Stage => PerCall`: outside redirect and retry, it observes only the terminal response. On a non-error status it returns the response **untouched** (body not read, consumed or disposed) without entering the fold. `Operation` sits outside it, which does not change what it observes (P4c-6). | §5.1; P4c-17, P4c-18. | `ErrorMappingPolicy` | `ErrorMappingPolicyTests.It_runs_once_outside_the_redirect_loop_and_sees_the_final_response` (a 307 then 404 through a pipeline with `RedirectPolicy`: one invocation, the 404 mapped); `…A_non_error_status_is_returned_untouched` (`[Theory]` over 100, 199, 200, 204, 300–308, 399, 600, 999; `TrackingBody` neither opened nor disposed). | 4 | ✅ |
| `PIPE-38` | MUST | **Built.** `AddRange` keeps the batch's order within each stage; `PrependRange` prepends each element in turn, so the batch ends up **reversed** within each stage. Documented on both members. | §5.1; P4c-10. | `PipelineBuilder` | `BulkTests.AddRange_keeps_batch_order_and_PrependRange_reverses_it` (`[a, b, c]` → `a, b, c` and `c, b, a`, with a pillar policy in the batch landing in its own slot). | 1 | ✅ |
| `PIPE-39` | SHOULD | **Built.** Two convenience shapes: `DexpacePipeline.CreateEmpty(transport)` (step-less, forwards to the transport) and `DexpacePipeline.CreateDefault(transport, …)` (the standard resilience pillars plus the non-pillar defaults). The async half's "caller-supplied scheduler for non-blocking backoff" is moot on .NET: the async backoff is `Task.Delay` over the `TimeProvider`, and the sync path's wait is a genuine blocking wait. | §5.1; §6.1; P4c-19. | `DexpacePipeline` | `ResiliencePresetTests.CreateEmpty_forwards_straight_to_the_transport`; `DexpacePipelineTests` (as built, extended): `CreateDefault_installs_the_standard_pillars_and_the_per_call_defaults_in_order`. | 5 | ✅ |
| `PIPE-40` | MUST | **Already met by both re-drivers** (as built, they dispose each superseded response before the next drive and return the in-flight one on every abandon path); **now pinned** against the new signature, and the dispose goes through `Disposal.DisposeQuietlyAsync` so a throwing dispose cannot mask the next drive's outcome. | §5.1 "PIPE-40's lifecycle rule". | `RedirectPolicy`, `RetryPolicy` | `ReDriveLifecycleTests`: superseded responses disposed before the next drive (`TrackingBody` order log), the returned one open; each abandon path (non-replayable body, hop or attempt budget exhausted, missing `Location`) returns the in-flight response undisposed. | 2 | ✅ |

**Totals, as designed.** 40 rows: 36 ✅ outright; 4 with a second mark (`PIPE-2`, `PIPE-3` ✅ by stated reading; `PIPE-28`
✅; ⏳ 8b; `PIPE-32` ✅; 🚫 under `async-redirect-pillar`). `PIPE-30`, `PIPE-33` and `PIPE-34` are ✅ with their residual
in an existing §10 entry. No N/A row: none of the gap analysis's four `PIPE` N/A candidates survives the design (the
fork's "copy()" is the struct value; the executor is a `TaskScheduler`; interruption is the token).

### Work 4c does on rows other phases own

These rows are **not** in 4c's 40. 4c does the work below, in the PR named, and the owning checklist's row cites 4c's
tests (the 3a precedent with `HTTP-36`/`HTTP-52`). They carry no exit mark in 4c's checklist.

| ID (owner) | Work 4c does | Evidence the owner cites | PR |
|---|---|---|---|
| `RECOV-15` (4b) | Installs 4b's `ErrorMappingStep` in the pipeline as `ErrorMappingPolicy` at `PerCall` (P4c-17). The step and its 400..599 predicate are 4b's. | `ErrorMappingPolicyTests` (the pipeline form); 4b's `ErrorMappingStepTests` | 4 |
| `RECOV-16` (4b) | None of the building (4b's `ErrorBodyBuffer`, per P4b-16). 4c's sync `Response.EnsureSuccess` uses the same buffer, so the bound stays single-sourced. | 4b's `ErrorBodyBufferTests` | 4 |
| `RECOV-32`, `RECOV-33` (4b) | Moves both policies onto the new signature (PR 2, mechanical) and installs them at `PerCall` in `CreateDefault` (PR 5). Their defaults and behaviour are 4b's. | `DexpacePipelineTests.CreateDefault_installs_the_standard_pillars_and_the_per_call_defaults_in_order` (presence, type and stage only) | 2, 5 |
| `BODY-30`, `BODY-31`, `HTTP-52` (3b; "✅, ⏳ 4c") | The policy form of the error mapping (`BODY-31`'s "returned with its body intact" through the pipeline) and `BODY-30`'s "a response with no body MUST be returned unchanged" on the policy (P4c-18). With 4b's buffer, this closes their ⏳ 4c clause. | `ErrorMappingPolicyTests`; `EnsureSuccessErrorMappingTests` | 4 |
| `RETRY-44` (6a) | The request-in/response-out signature makes "upstream steps MUST NOT mutate the shared in-flight request between attempts" structural: there is no shared in-flight request. | `ReDriveRequestIsolationTests` (`Security`, moved, P4c-22); `ForkTests` | 2 |
| `REDIR-11`, `REDIR-24` (6b) | Fixes the seed request on the call-scoped context (coupling obligation 2); `RedirectPolicy` and `AuthorizationPolicy` read it, and the public `"dexpace.auth.origin"` bag key retires (P4c-5). Stage order keeps auth inside redirect (`REDIR-24`). The redirect rewrite stays 6b's; stamping stays 6c's. | `SeedOriginTests`; `RedirectCredentialHygieneTests`, `AuthHttpsGuardTests` (`Security`, unedited) | 2 |
| `REDIR-25` (6b) | Records the reversal under §10 `async-redirect-pillar` and builds the preset that installs `RedirectPolicy` on both paths. 6b's row cites §10 and 4c's test. | `ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect` | 5, 6 |
| `XCUT-8` (phase 10) | An internal factory, `ErrorMapping.ToException(Response)`, is the one place core turns a buffered error response into `HttpResponseException`; it throws `ArgumentException` for a non-error status. 4c routes 4b's step and `EnsureSuccess(Async)` through it (a minimal edit to 4b's merged code, flagged in PR 4). The public `HttpResponseException` constructor is unchanged (7a/10 decide whether it gains the guard). | `ErrorMappingFactoryTests.A_non_error_status_is_rejected` | 4 |
| `SEAM-2` (2b) | `HttpPipeline` joins the allow-list with a public exemption (fact 11). | `SeamImplementationArchitectureTests` | 3 |
| `SEAM-28` (2b; 4a attaches) | 4c threads the operation id, through whatever carrier 4a defines, into the dispatch→request promotion at call entry. | 4a's test | 2 |

---

## Argued positions

### A. The request-in/response-out signature (S6 structural)

Design §5.1 fixes the shape; this position records what the card leaves to 4c.

```csharp
public abstract class HttpPipelinePolicy
{
    public abstract PipelineStage Stage { get; }
    public abstract ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner next);
    public virtual Response Process(Request request, PipelineContext context, PipelineRunner next);
}

public readonly struct PipelineRunner
{
    public ValueTask<Response> RunAsync(Request request, PipelineContext context);
    public Response Run(Request request, PipelineContext context);
}
```

- **`Process` is virtual, with the documented blocking bridge as its default** (§5.3: "`Process` has a default
  implementation for third-party policies, and *that* default is the documented blocking bridge"). The default calls
  `ProcessAsync` and blocks on it behind one `#pragma warning disable RS0030` citing §5.3; every shipped policy overrides it
  through the `ProcessCoreAsync(…, bool async)` idiom (facts 1, 2). Options: abstract `Process` (a compile-time guarantee,
  breaking every third-party policy and every test policy, including a `Security` class); no `Process` (the as-built, which
  keeps the double sync-over-async §5.3 condemns). Rejected for the reason P3a-5 rejected the abstract body member.
- **The null guard survives.** A transport or a policy can return `null` despite the annotations; `PipelineRunner` asserts
  the transport's result (`SEAM-16`, as built) **and** each policy's result, throwing `PipelineAbortedException`. The
  as-built "chain completed without producing a response" path disappears, as §5.1 says; the exception type stays for
  `SEAM-16`.
- **`PipelineContext.Request` and `Response` are gone.** Nothing can write the request upward, so `RETRY-44`'s rule and S6's
  defect are structural; the response is a return value, so `PIPE-40`'s "superseded" is "a local about to be overwritten".

### B. The call-scoped, read-mostly `PipelineContext`, over 4a's chain

`PipelineContext` is a `sealed class` with an **internal** constructor (P4c-4), made of one shared, call-scoped state object
and a few per-drive values. Copies share the call-scoped object by reference.

| Member | Scope | Source |
|---|---|---|
| `Request SeedRequest` | call | the request passed to `SendAsync`/`Send`, fixed at entry (§6.2; P4c-5) |
| `RequestOptions RequestOptions` | call | the caller's per-call options (`PIPE-17`) |
| `DexpaceClientOptions Options` | call | the pipeline's build-time options, or the per-call override (P4c-15); name kept from the as-built |
| `CallKey CallKey` | call | 4a; minted at entry |
| `InstrumentationContext Instrumentation` | call | 4a's bundle, from the call's `DispatchContext` (P4c-21) |
| `TryGetProperty<T>(PipelinePropertyKey<T>, out T)` / `SetProperty<T>(…)` | call | the typed bag (P4c-4) |
| `CancellationToken CancellationToken` | drive | the caller's token, or `OperationPolicy`'s deadline-linked token |
| `Activity? Activity` | drive | set by `InstrumentationPolicy` for its downstream |
| `int AttemptNumber`, `int HopNumber` | drive | set by retry and redirect |
| `ForAttempt(int)`, `ForHop(int)`, `WithActivity(Activity?)`, `WithCancellationToken(CancellationToken)` | — | return a copy with one per-drive value changed, sharing every call-scoped reference |

The per-drive copies replace four `internal set`s (`CancellationToken`, `Activity`, `AttemptNumber`, the auth origin) that
let a downstream policy's write reach an upstream one. They are public because a third-party policy at a pillar (a custom
retry) needs `ForAttempt`.

### C. Stages

The enum is design §5.1's table, verbatim: `Operation = 100`, `PerCall = 150`, `Redirect = 200`, `PerHop = 250`,
`Retry = 300`, `PerAttempt = 400`, `Auth = 500`, `Diagnostics = 600`, `Serde = 700`. Two readings are recorded rather than
argued away:

- **`Operation` is a sixth singleton, outside the spec's pre-redirect slot (P4c-6, open).** `PIPE-2` says the runtime
  preserves the five-pillar chain "plus an outermost pre-redirect slot"; `PIPE-4` names five configurable pillars. The .NET
  `Operation` pillar (the once-per-call deadline and, in 5c, the operation span) sits outside `PerCall`. It does not change
  anything `PIPE-2`'s boundary clause protects: both `Operation` and `PerCall` run outside both loops and see only the
  terminal response, and `PIPE-37`'s step still observes exactly one response. Design §5.1 lists `Operation` as a pillar but
  no §10 entry or §11 item records the reading against `PIPE-2`/`PIPE-4`; P4c-6 proposes a §11 item.
- **No user slot after `Auth`, `Diagnostics` or `Serde` (P4c-7).** `PIPE-3` is a SHOULD and its second sentence (sparse keys)
  exists so a slot can be added later without renumbering. A request signer that must run after auth has no home today;
  the first such consumer adds `PostAuth = 550`, additively.

`PipelineStageHelper` is renamed `PipelineStageFacts` (internal) and holds the pillar set as a `FrozenSet<PipelineStage>`
and `IsDefinedStage` (fact 3).

### D. The builder: entries, collisions, edits, bulk operations

- **The builder records `(policy, stage)` entries at insertion and never reads `Stage` again** (fact 6, P4c-8). `Build`
  sorts the entries stably by the recorded stage. This is also what makes `PIPE-22` deterministic for a policy whose
  `Stage` is not constant.
- **Collision at insertion** (P4c-9). Every path that adds (`Add`, `Prepend`, `InsertBefore`/`After`, `AddRange`,
  `PrependRange`, `AddStandardResilience`) runs one internal `CheckPillar(entry)`: same instance on its pillar → no-op;
  distinct instance → `InvalidOperationException("Pipeline stage 'Retry' already holds RetryPolicy; cannot add
  MyRetryPolicy. Use Replace<RetryPolicy>(…) to swap it.")`. The `Build`-time count check goes (it can no longer fire).
- **Cross-stage edits are argument errors** (P4c-10): `ArgumentException` with `paramName: "policy"`, naming the anchor's
  stage and the policy's. A missing anchor stays `InvalidOperationException` (as built, `PIPE-21`). No new public exception
  type: the Node sibling's five error classes buy a typed catch nobody needs at composition time, and the BCL split
  (state versus argument) is the styleguide's.
- **Bulk operations are all-or-nothing** (`PIPE-23`): validate the batch (against the builder and within itself) into a
  scratch list, then commit with one `AddRange`.
- **`Flatten`/`Nest`** (P4c-11) are static factories returning a builder; the builder gains an optional seeded transport and
  seeded client options, and a parameterless `Build()` that throws `InvalidOperationException` when nothing is seeded.

### E. The real synchronous path

Coupling obligation 5's second strand. Three parts:

1. **`PipelineRunner.Run`** calls `policy.Process` and, past the last policy, the **sync terminal**: the transport itself
   when it implements `IHttpClient` (as `SystemNetHttpClient` does), otherwise `transport.AsBlocking()` (2b's documented
   bridge). `Build(IAsyncHttpClient)` decides once, at build time (P4c-13). A sync-only transport is not accepted directly:
   reaching the async path from it needs `AsAsync(scheduler)`, and `PIPE-33` forbids a default scheduler; the caller wraps it.
2. **Every shipped policy** implements one private `ProcessCoreAsync(Request, PipelineContext, PipelineRunner, bool async)`,
   branching each I/O call `async ? await next.RunAsync(…) : next.Run(…)`. `Process` calls it with `false` and reads the
   result of the completed `ValueTask` through one internal helper, `SyncPath.GetCompletedResult`, which asserts
   `IsCompleted` (throwing `InvalidOperationException` naming the policy if a sync drive ever suspended) and then
   `GetAwaiter().GetResult()` behind a single `#pragma` citing §5.3 (facts 1, 2). There is one body of retry, redirect and
   auth logic.
3. **Blocking waits that are not sync-over-async.** `RetryPolicy`'s sync sleep is an internal `BlockingWait.Wait(TimeSpan,
   TimeProvider, CancellationToken)`: a `TimeProvider.CreateTimer` that sets a `ManualResetEventSlim`, waited with the token.
   That is a genuine blocking wait (§6.1) with no task under it, keeps the fake clock usable in tests, and keeps the 49-day
   chunking of S7. 5a's `CFG-15`/`CFG-17` may take it over. `BearerTokenAuthPolicy`'s sync credential goes through
   `TokenCredential.GetToken`, but `AccessTokenCache` has no sync path, so until 6c gives it one, the sync `GetCredential`
   default is the documented bridge (P4c-13). `AuthorizationPolicy` gains `protected virtual GetCredential(PipelineContext)`
   with that bridge as its default; `BasicAuthPolicy` and `ApiKeyAuthPolicy` override it trivially.

`Response` gains `EnsureSuccess(CancellationToken)`, the sync twin over 4b's `ErrorBodyBuffer.Capture` (3a hand-off).

### F. `HttpPipeline` as a transport, and where options come from

```csharp
public sealed class HttpPipeline : IAsyncHttpClient, IHttpClient
{
    public IReadOnlyList<HttpPipelinePolicy> Policies { get; }
    public ValueTask<Response> SendAsync(Request request, CancellationToken cancellationToken);
    public ValueTask<Response> SendAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
    public ValueTask<Response> SendAsync(Request request, DexpaceClientOptions options, CancellationToken cancellationToken); // kept, loses its `= default`
    public ValueTask<T> SendAsync<T>(Request request, Func<Response, CancellationToken, ValueTask<T>> handler, RequestOptions options, CancellationToken cancellationToken);
    public Response Send(Request request, CancellationToken cancellationToken);
    public Response Send(Request request, RequestOptions options, CancellationToken cancellationToken);
    public Response Send(Request request, DexpaceClientOptions options, CancellationToken cancellationToken);   // kept, now real, loses its `= default`
    public T Send<T>(Request request, Func<Response, T> handler, RequestOptions options, CancellationToken cancellationToken);
    Task<Response> IAsyncHttpClient.ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
    Response IHttpClient.Execute(Request request, RequestOptions options, CancellationToken cancellationToken);
    public void Dispose();              // no-op toward the transport (PIPE-27)
    public ValueTask DisposeAsync();    // no-op
}
```

- **Two kinds of options.** The seam carries `RequestOptions` (per call, 2b); the policies read `DexpaceClientOptions`
  (retry, redirect, user agent, deadline). An `ExecuteAsync(request, requestOptions, token)` call has nowhere to get client
  options from, so **the pipeline captures them at `Build`** (`Build(transport)` uses `new DexpaceClientOptions()`;
  `Build(transport, options)` takes the caller's; P4c-11). That is also what makes `Nest` and `PIPE-26` honest: an inner
  pipeline runs with its own configuration.
- **The per-call `DexpaceClientOptions` overloads are kept** (P4c-15, open): they are what every `Security` class, `Pageable`
  and the AOT smoke call (fact 9), and removing them would turn a structural rework into an edit of four `Security` classes.
  Their documentation says they override the build-time options for one call, and that 5a (which makes the options record
  immutable) decides their future.
- **Overload hygiene.** No overload in the `SendAsync`/`Send` families carries an optional token. Two defaulted overloads trip
  `RS0026`, and a default on the shortest overload beside longer ones trips `RS0027` (the defaulted overload must have the most
  parameters); verified against `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0. The kept `DexpaceClientOptions` overloads
  therefore lose their `= default`, and the callers that passed no token change in PR 3 (fact 9). A `default` literal still
  binds to the token overload (fact 7).
- **Explicit interface implementations** keep `ExecuteAsync`'s `Task<Response>` off the public surface beside the
  `ValueTask` family. `ExecuteAsync` returns `SendAsync(…).AsTask()`.

### G. `ErrorMappingPolicy` over 4b's `Outcome` folds

`public sealed class ErrorMappingPolicy : HttpPipelinePolicy`, `Stage => PipelineStage.PerCall`: a thin wrapper that installs
4b's `ErrorMappingStep` (the class that "serves both layers", §5.1) in the stage pipeline, through 4b's response fold.

```text
ProcessCoreAsync(request, context, next, async):
    response = async ? await next.RunAsync(request, context) : next.Run(request, context)
    if status not in 400..599: return response                        # untouched, fold not entered (PIPE-37, BODY-31)
    if body is the empty replayable body: throw ErrorMapping.ToException(response)   # no drain (BODY-30, P4c-18)
    outcome  = s_chain.Apply(Async)(Outcome.Success(response), token)  # s_chain = ResponseRecoveryChain([ErrorMappingStep.Instance], [])
    return   Dispatch(outcome)                                         # 4b: Success -> response; Failure -> rethrow unchanged (RECOV-10)
```

4b's step buffers through `ErrorBodyBuffer` (disposing the original, `RECOV-13`'s "the step owns the response it drops") and
throws `HttpResponseException`; the fold turns the throw into `Failure` (`RECOV-7`); `RECOV-12`'s close of the in-hand response
is a latched no-op because the step already disposed it (3b's latch: released exactly once). Dispatch rethrows the same
instance with its stack. The chain is a `static readonly` field (immutable, `RECOV-14`, `PIPE-11`). Running the fold rather
than calling the step directly is deliberate: it is S8's status predicate and buffering behind the same fold 6a's
re-classification will use, so the policy, `EnsureSuccess(Async)` and the retry stack cannot drift (S8 structural). No
`Outcome` appears in any `PIPE` signature.

**What 4c adds to 4b's mapping path.** (1) `ErrorMapping.ToException(Response)`, the internal factory with `XCUT-8`'s guard
(see [work on other rows](#work-4c-does-on-rows-other-phases-own)). (2) **`BODY-30`'s "a response with no body MUST be
returned unchanged"** on the policy: a response whose body is the empty replayable body (`ContentLength == 0 && IsReplayable`,
the shape design §10 entry 30 gives an absent body) holds no connection, so the policy maps it without a drain: the
exception carries the response as it is (P4c-18). Whether 4b's `ErrorBodyBuffer` itself short-cuts that case is 4b's call;
the policy's behaviour is the same either way. (3) `Response.EnsureSuccess(CancellationToken)`, the sync twin of 4b's
re-homed `EnsureSuccessAsync`, over `ErrorBodyBuffer.Capture`.

**`ErrorMappingPolicy` is not in `CreateDefault`** (P4c-18): design §5.1 keeps "return the `Response` for any status" as the
default, the .NET idiom; callers who want throw-by-default add the policy.

### H. The `PIPE-32`/`REDIR-25` reversal

Design §5.3, §6.2, §10 entry 14 and §11 item 29 already decide it; 4c builds it and records it. The proposed dated
correction to §10, to land in PR 6 (P4c-20):

> **Correction (2026-MM-DD, phase 4c): entry 14 gains the topic label `async-redirect-pillar`.** Entry 14 stands as written.
> Phase 4c built it: `PipelineBuilder.AddStandardResilience` installs one `RedirectPolicy` at `PipelineStage.Redirect`
> whether the pipeline is driven through `SendAsync` or `Send`, and both `DexpacePipeline.CreateDefault` paths follow
> redirects (`ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect`). `PIPE-32`'s checklist row
> is ✅ for its documentation clause and 🚫 for the async no-follow, citing this entry; `REDIR-25`'s row (6b) cites it the
> same way. The single-layer invariant stays the transport's (phase 1 S3's `AllowAutoRedirect = false` and the borrowed-client
> detection; 8b, 9). A caller who wants the 3xx verbatim sets `MaxRedirects = 0` (`REDIR-17`, 6b) or omits the policy.

And in entry 14's own heading line: `*Topic label `async-redirect-pillar`, added by dated correction, 2026-MM-DD (phase 4c).*`
(the 3a precedent on entry 4). `docs/first-release.md`'s "Behavioural asymmetries a consumer must know" gains its first entry,
which that section already anticipates.

### I. The context-chain wiring (P4c-21)

This confirms the interface 4a's design proposes to 4c ([observed conflicts](#observed-conflicts-with-the-4a-and-4b-designs-2026-10-05-read-at-the-end-of-this-brainstorm), item 1).
The pipeline is the only code that touches 4a's chain, and it never calls the store (promotion registers, 4a's P4a-6):

1. **Call entry (`HttpPipeline`).** Construct one `DispatchContext` with `InstrumentationContext.FromActivity(Activity.Current)`
   (or `None`) until 5c replaces it with the operation span; it registers nothing (`CTX-17`). Its key becomes
   `PipelineContext.CallKey` and its bundle `PipelineContext.Instrumentation`. The operation id (`SEAM-28`) is read from the
   carrier 4a defines.
2. **Each transmission (`PipelineRunner`'s terminal, sync and async).** `PromoteToRequest(request, operationName)` with the
   request actually sent, then the transport call, then `PromoteToExchange(response)` and attach the `ExchangeContext` to the
   response through a new internal hook on `Response`, so disposing that response closes its link. Each later attempt or hop
   promotes the same dispatch again and takes the slot; the superseded response's dispose closes a non-occupant, a no-op
   (`CTX-10`). The furthest link reached is recorded on the call-scoped state (the terminal is the only writer, and a call's
   drives are sequential).
3. **Failure (`HttpPipeline`).** `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` → close the furthest link →
   `throw;` (same instance and stack). A fatal exception skips the close; the store's bound is the backstop (`CTX-11`, §5.4).
4. **If the lead takes 4a's P4a-13 option 1** (`RequestOptions.CallKey` as the transport route), the pipeline derives the
   call's `RequestOptions` once at entry (`options with { CallKey = key }`), and that one instance is what every policy, every
   fork and the transport see; `PIPE-17`'s identity test compares against `context.RequestOptions`.

5. **A pipeline used as another pipeline's transport (`PIPE-26`, `Nest`/`PIPE-35`).** The inner `HttpPipeline` is reached through
   `IAsyncHttpClient.ExecuteAsync`/`IHttpClient.Execute` from the outer terminal and mints its **own** `DispatchContext` and
   `CallKey` (an inner pipeline runs with its own configuration, position F). The outer terminal then promotes its own chain on the
   same response, so the response carries **two** exchange links: the `Response` hook holds a list of links and its latched
   dispose closes every one, in reverse order of attachment (never a single slot, which would leave one link in the store until the
   bound evicts it). The transport at the bottom of the inner pipeline sees the inner call's `CallKey`; the inner pipeline's
   own transport call is the outer's transport call and sees the outer key. Under P4a-13 option 1 each pipeline derives
   `options with { CallKey = … }` as a copy for its own policies and transport; the caller's instance is never overwritten, and
   `PIPE-17`'s identity test compares each pipeline's `context.RequestOptions`. Test: `ContextChainWiringTests` gains a nested
   case (a `Nest`ed pipeline call leaves no store entry after the response is disposed).

Policies see `context.CallKey` and `context.Instrumentation` and never a link with a public promotion API. 4c's tests of this
wiring are five (`ContextChainWiringTests`: nothing registered before the first transmission; one store entry per call
across a retried and redirected call; the returned response's dispose closes the occupant; a failure closes it before the
exception surfaces); the `CTX` rows that need more evidence are 4a's.

---

## Type shapes

### The public surface (`PublicAPI.Unshipped.txt`, core only; the plan confirms the exact lines from the analyzer's code fix)

**Removed** (breaking): `HttpPipelinePolicy.ProcessAsync(PipelineContext, PipelineRunner) -> ValueTask`;
`PipelineRunner.RunAsync(PipelineContext) -> ValueTask`; `PipelineContext.PipelineContext(Request, DexpaceClientOptions,
CancellationToken)`; `PipelineContext.Request.get/set`; `PipelineContext.Response.get`; `PipelineContext.GetProperty<T>(string)`;
`PipelineContext.SetProperty<T>(string, T)`; `PipelineStage.PerCall = 250`; every shipped policy's
`override ProcessAsync(PipelineContext, PipelineRunner)`.

**Added:**

```text
abstract Dexpace.Sdk.Core.Pipeline.HttpPipelinePolicy.ProcessAsync(Request! request, PipelineContext! context, PipelineRunner next) -> ValueTask<Response!>
virtual Dexpace.Sdk.Core.Pipeline.HttpPipelinePolicy.Process(Request! request, PipelineContext! context, PipelineRunner next) -> Response!
Dexpace.Sdk.Core.Pipeline.PipelineRunner.RunAsync(Request! request, PipelineContext! context) -> ValueTask<Response!>
Dexpace.Sdk.Core.Pipeline.PipelineRunner.Run(Request! request, PipelineContext! context) -> Response!
Dexpace.Sdk.Core.Pipeline.PipelineStage.PerCall = 150, PerHop = 250, Serde = 700
Dexpace.Sdk.Core.Pipeline.PipelineContext.SeedRequest / RequestOptions / CallKey / Instrumentation / HopNumber .get
Dexpace.Sdk.Core.Pipeline.PipelineContext.ForAttempt(int) / ForHop(int) / WithActivity(Activity?) / WithCancellationToken(CancellationToken) -> PipelineContext!
Dexpace.Sdk.Core.Pipeline.PipelineContext.TryGetProperty<T>(PipelinePropertyKey<T>!, out T) -> bool ; SetProperty<T>(PipelinePropertyKey<T>!, T) -> void
Dexpace.Sdk.Core.Pipeline.PipelinePropertyKey<T> (sealed; ctor(string! name); Name.get; ToString())
Dexpace.Sdk.Core.Pipeline.PipelineBuilder.Prepend / AddRange / PrependRange / AddStandardResilience / Build() / Build(transport, DexpaceClientOptions!)
static Dexpace.Sdk.Core.Pipeline.PipelineBuilder.Flatten(HttpPipeline!) / Nest(HttpPipeline!) -> PipelineBuilder!
Dexpace.Sdk.Core.Pipeline.HttpPipeline : IAsyncHttpClient, IHttpClient ; Policies.get ; the SendAsync/Send overloads of position F ; Dispose ; DisposeAsync
static Dexpace.Sdk.Core.Pipeline.DexpacePipeline.CreateEmpty(IAsyncHttpClient!) -> HttpPipeline!
Dexpace.Sdk.Core.Pipeline.Policies.ErrorMappingPolicy (sealed; ctor(); Stage; ProcessAsync; Process)
virtual Dexpace.Sdk.Core.Pipeline.Policies.AuthorizationPolicy.GetCredential(PipelineContext!) -> (string! HeaderName, string! HeaderValue)
Dexpace.Sdk.Core.Http.Response.Response.EnsureSuccess(CancellationToken = default) -> void
override <each shipped policy>.ProcessAsync(Request!, PipelineContext!, PipelineRunner) ; override <each>.Process(…)
override sealed Dexpace.Sdk.Core.Pipeline.Policies.AuthorizationPolicy.ProcessAsync(Request!, PipelineContext!, PipelineRunner) ; override sealed …AuthorizationPolicy.Process(…)   // both sealed: a subclass cannot skip the HTTPS guard (AUTH-28)
```

`PipelineContext`'s retained members (`Options`, `CancellationToken`, `Activity`, `AttemptNumber`) keep their lines (their
setters were already `internal`). `HttpPipelinePolicy`'s protected constructor is unchanged. `Dexpace.Sdk.Http.SystemNet`'s and
`Dexpace.Sdk.Serialization.SystemTextJson`'s API files do not change. Each new public member carries a `///` summary; the
breaking ones carry a **Breaking** remark.

### Internal types

| Type | Purpose | Rows |
|---|---|---|
| `PipelineEntry` (`readonly record struct (HttpPipelinePolicy Policy, PipelineStage Stage)`) | the stage read once at insertion | `PIPE-1`, `PIPE-22` |
| `PipelineStageFacts` | pillar set, `IsDefinedStage` | `PIPE-4`, `PIPE-8` |
| `CallState` | the call-scoped half of `PipelineContext` | `PIPE-10`, `PIPE-11`, `PIPE-17` |
| `SyncPath` | `GetCompletedResult` for the `bool async` idiom; the one `RS0030` pragma for it | `PIPE-28` |
| `BlockingWait` | the sync retry sleep over `TimeProvider` | `PIPE-28` |
| `ErrorMapping` | `ToException(Response)`, the `XCUT-8`-guarded factory every mapping path goes through | `XCUT-8` (10) |
| `CallState` gains the furthest promoted link | the failure-path close of position I | 4a's `CTX` rows |

---

## Migration plan from the as-built code

| Change | Sites | Mechanical rewrite |
|---|---|---|
| The policy signature | `HttpPipelinePolicy`, `PipelineRunner`, every shipped policy (eleven classes: `AuthorizationPolicy` and its three subclasses included), every test policy (`PipelineBuilderTests`, `PipelineRunnerTests`, `HttpPipelineTests`, `DexpacePipelineTests`, `RetryPolicyTests`, `InstrumentationPolicyTests`, `IdempotencyPolicyTests`, `ReDriveRequestIsolationTests`) | `context.Request` → the `request` parameter; `context.Request = x; await continuation.RunAsync(context)` → `return await next.RunAsync(x, context)`; `context.Response` after the call → the returned value; `context.AttemptNumber = n` → `next.RunAsync(request, context.ForAttempt(n))` |
| `OperationPolicy` | `Policies/OperationPolicy.cs` | `context.CancellationToken = cts.Token` → `context.WithCancellationToken(cts.Token)` passed downstream |
| `InstrumentationPolicy` | `Policies/InstrumentationPolicy.cs` (keeps its `MA0051` waiver) | `context.Activity = activity` → `context.WithActivity(activity)`; the `traceparent` stamp goes on the request it passes down |
| `RedirectPolicy`, `RetryPolicy` | `RedirectPolicy` keeps its `MA0051` waiver (6b rewrites it); `RetryPolicy` has none today and gains one citing 6a's rewrite | the held-request restore becomes passing the held request; the redirect's local `seedUrl` becomes `context.SeedRequest.Url`; dispose of a superseded response through `Disposal.DisposeQuietlyAsync`; the sync sleep through `BlockingWait` |
| `AuthorizationPolicy` | `Policies/AuthorizationPolicy.cs` | the `"dexpace.auth.origin"` bag key retires; origin compared against `context.SeedRequest.Url` (P4c-5); `GetCredentialAsync(PipelineContext)` kept; `GetCredential` added |
| `IdempotencyPolicy`, `ClientIdentityPolicy` | on 4b's merged code | signature only; `Stage => PipelineStage.PerCall` unchanged in source, now value 150 (fact 8, P4c-23); the idempotency bag key, if 4b keeps it, becomes a `PipelinePropertyKey<string>` |
| `PipelineContext` | rewritten (position B) | internal constructor; tests build contexts through an internal factory (`InternalsVisibleTo` to `Core.Tests` already exists) |
| `PipelineBuilder` | rewritten over entries (position D) | existing tests keep their assertions except `Add`-twice-of-a-pillar (now idempotent or an `Add`-time throw) and the cross-stage `InsertBefore` re-bucketing case (now rejected), each rewritten to the new rule with the row ID in the test name |
| `HttpPipeline` | rewritten (position F) | `Send` stops being `SendAsync(…).AsTask().GetAwaiter().GetResult()`; the `RS0030` pragma moves to `SyncPath` |
| `DexpacePipeline.CreateDefault` | recomposed over `AddStandardResilience` | same parameters; `IdempotencyPolicy` and `ClientIdentityPolicy` now at value 150 |
| `Response` | `Response.cs`, on 4b's merged re-home of `EnsureSuccessAsync` | `EnsureSuccess` added over `ErrorBodyBuffer.Capture`; the internal exchange-close hook (4a's proposal, item 3) called from the latched dispose; both mapping methods go through `ErrorMapping.ToException` |
| Test support | `tests/Dexpace.Sdk.TestSupport/Pipeline/` (new), `Transports/` (fakes gain `IHttpClient`) | non-packable; `TestCategoryTests` unaffected |
| `SeamImplementationArchitectureTests` | allow-list | public exemption for `HttpPipeline`, reason "PIPE-26" |
| Docs | `CLAUDE.md` (layout line for `Pipeline/`; "What is genuinely unbuilt" drops 4c), `src/Dexpace.Sdk.Core/README.md` (the `Pipeline` row; the sample passes a token, fact 9), user page `docs/sdk-documentation/pipelines.md` | close-out PR |

---

## Keeping the `Security` classes green

Constraint 5: green, or moved without weakening. Class by class (P4c-22, open for the lead):

| Class | Edit | Why it is not a weakening |
|---|---|---|
| `EnsureSuccessErrorMappingTests` (S8) | **None.** | `EnsureSuccessAsync` keeps its signature and behaviour (4b re-homes it onto `ErrorBodyBuffer`, behaviour-preserving, P4b-16; 4c only routes it through `ErrorMapping.ToException`). Its private `TrackingBody` overrides `Dispose(bool)`, unchanged since 3b. The policy form gets its own `Unit` class mirroring all six methods (`ErrorMappingPolicyTests`), and the sync `EnsureSuccess` gets the same six through a `[Theory]` over both entry points. |
| `ReDriveRequestIsolationTests` (S6) | **Mechanical, in two PRs.** The enum token (2) lands in **PR 1** with the stage enum (the PR 1 stage tests need it, and the token is safe on the as-built signature); the policy-signature edits (1) and the added fact (3) land in PR 2. (1) `ProbePolicy` and `MarkingPolicy` move to the new signature: the probe records `request.Headers.Get(header)` and returns `next.RunAsync(request, context)`; the marker returns `next.RunAsync(request.WithHeaders(request.Headers.Set("X-Attempt-Marker", "set")), context)`. (2) In `A_redirect_hop_is_not_built_from_the_previous_hops_stamped_request`, `PipelineStage.PerCall` → `PipelineStage.PerHop` (fact 8: the same slot, between `Redirect` and `Auth`, under its new name). (3) One **added** fact, `The_context_offers_no_way_to_write_the_request_upward` (reflection: `PipelineContext` has no member of type `Request` with a setter and no method taking a `Request` that returns `void`). | Every test method's arrange-act-assert is byte-identical except the one enum token; every assertion keeps its value. The added fact is what makes S6 structural and permanent. |
| `RedirectCredentialHygieneTests` (S3) | **None.** | Calls `SendAsync(Request, DexpaceClientOptions, CancellationToken)` (kept, fact 9) over `RedirectPolicy`, whose cross-origin judgement now reads `context.SeedRequest` instead of a local copy of the same value. |
| `AuthHttpsGuardTests` (S4) | **None.** | `CountingAuthPolicy` overrides `GetCredentialAsync(PipelineContext)`, kept (fact 10); the HTTPS guard runs before the credential in both paths. |
| `RetryPacingOverflowTests` (S7) | **None.** | The async sleep is unchanged; `BlockingWait` reuses the same clamp and 49-day chunking on the sync path. |
| SystemNet `RedirectWireTests` (S3) | **None.** | Same `SendAsync` overload, same `RedirectPolicy` behaviour. |

No `Security` class is added: 4c fixes no new phase-1 defect.

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes and gets a `CHANGELOG.md` `[Unreleased]` line in the PR
that makes it (constraint 8).

| # | Change | Kind | PR |
|---|---|---|---|
| 1 | A pillar collision throws at `Add` (was: `Build`), naming both types; re-adding the same instance is a no-op (was: a throw at `Build`) | behaviour | 1 |
| 2 | A cross-stage `InsertBefore`/`InsertAfter`/`Replace` throws `ArgumentException` (was: silently re-bucketed or accepted) | behaviour | 1 |
| 3 | `PipelineStage.PerCall` is 150 and runs outside the redirect loop; the old slot is `PerHop = 250` (source using `PerCall` changes loop silently, fact 8) | source + behaviour | 1 |
| 4 | The policy signature: `ProcessAsync(Request, PipelineContext, PipelineRunner) -> ValueTask<Response>`; `RunAsync(Request, PipelineContext)`; `Process`/`Run` added | source | 2 |
| 5 | `PipelineContext`: no public constructor, no `Request`/`Response`, typed property keys instead of strings, per-drive copies | source | 2 |
| 6 | `HttpPipeline.Send` runs the policies synchronously (was: blocked on the async chain); a third-party policy without `Process` runs through the documented bridge | behaviour | 2 |
| 7 | `IdempotencyPolicy` and `ClientIdentityPolicy` run once per call, outside the redirect loop (was: per hop) | behaviour | 1 |
| 8 | `AuthorizationPolicy` compares against the seed request's origin (was: the first origin it saw, stored under a public string key) | behaviour | 2 |
| 9 | `HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient`, so it is disposable; disposal never touches the transport | additive surface with a new obligation for `using` analyzers | 3 |
| 10 | `PipelineBuilder.Build` captures the client options; `SendAsync(Request, CancellationToken)` and the `RequestOptions` overloads use them | additive | 3 |

Additive, with no **Breaking** marker: `Prepend`, the range methods, `Flatten`/`Nest`, `Build()`, `AddStandardResilience`,
`CreateEmpty`, `Policies`, `SendAsync<T>`/`Send<T>`, `ErrorMappingPolicy`, `EnsureSuccess`, `GetCredential`.

---

## Landing order

Each step is one pull request carrying its code **and** its tests (roadmap step 5's one-PR allowance, as 2a, 2b, 3a and 3b
used it), with its `PublicAPI.Unshipped.txt` diff and `CHANGELOG.md` line where it has one. **PR 2 cannot be split**: the
signature change touches every policy and every test policy at once, and any cut leaves a red build; it lands as one PR
(roadmap step 5's stated exception).

| PR | Content | Rows | Gate |
|---|---|---|---|
| **1** | The stage enum (`PerCall = 150`/`PerHop`/`Serde`, `PipelineStageFacts` with the `Serde` pillar, the S6 enum token); the builder over `PipelineEntry`: read-once stage, undefined-stage rejection, collision at insertion, `ReferenceEquals` idempotence, `Prepend`, `AddRange`/`PrependRange`, cross-stage rejection, the `Policies` view; `PipelineStageFacts`; `TestSupport/Pipeline/` (on the as-built signature: the probes adapt in PR 2) | `PIPE-1`, `PIPE-3`–`PIPE-8`, `PIPE-18`–`PIPE-23`, `PIPE-25`, `PIPE-38` (and the enum half of `PIPE-2`) | none (no 4a/4b type; the enum change needs none) |
| **2** | The signature; `PipelineRunner.Run`; `PipelineContext` over 4a's chain (position B, I); `CallState`, `PipelinePropertyKey<T>`; every shipped policy on `ProcessCoreAsync(…, bool async)`; `SyncPath`, `BlockingWait`; the seed origin; real `Send`; `ReDriveRequestIsolationTests` moved | `PIPE-2`, `PIPE-10`–`PIPE-16`, `PIPE-28`–`PIPE-30`, `PIPE-36`, `PIPE-40` | PR 1; **4a and 4b merged** |
| **3** | `HttpPipeline` as a transport; options captured at `Build`; `RequestOptions` on the context; `SendAsync<T>`/`Send<T>`; `Flatten`/`Nest`/`Build()`; the allow-list exemption; the bridge end-to-end tests | `PIPE-9`, `PIPE-17`, `PIPE-26`, `PIPE-27`, `PIPE-31`, `PIPE-33`–`PIPE-35` | PR 2 |
| **4** | `ErrorMappingPolicy` over 4b's `ErrorMappingStep` and fold; `ErrorMapping.ToException` (`XCUT-8`); the empty-body clause; `Response.EnsureSuccess` | `PIPE-37` (and 3b's `BODY-30`/`BODY-31`/`HTTP-52` ⏳ 4c clauses, with 4b's buffer) | PR 2; 4b merged |
| **5** | `AddStandardResilience`; `CreateDefault` recomposed; `CreateEmpty`; `RECOV-32`/`RECOV-33` wiring | `PIPE-24`, `PIPE-32`, `PIPE-39` | PR 3 |
| **6** | Close-out: the AOT smoke extended (sync `Send` over `CreateDefault`, `ErrorMappingPolicy`, `HttpPipeline` as `IAsyncHttpClient`); `docs/sdk-documentation/pipelines.md`; the 4c checklist; the dated design corrections; `docs/first-release.md`; `CLAUDE.md`, READMEs; the roadmap status note | all 40 (closing) | 1–5 |

---

## Tests, vectors and ports

- **Categories.** `Unit` throughout `tests/Dexpace.Sdk.Core.Tests` (fakes only; `SEAM-2`'s partition holds). `AotSmoke` for
  PR 6's checks. No `Integration` test is needed: nothing in 4c is wire-visible that S3's `RedirectWireTests` does not already
  cover, and that class runs unedited.
- **No vectors.** Every `PIPE` rule is about ordering, cursors and composition; a JSON table would restate the enum.
- **Ported, each with a header comment citing path and sha:** from `nodejs-sdk@c0ff3fd` `packages/core/src/pipeline/`:
  `builder.test.ts` (collision, idempotence, anchor-not-found, cross-stage, bulk all-or-nothing, append/prepend asymmetry,
  reserved stage), `cursor.test.ts` (fork independence, options identity across forks, substitution sticking, short-circuit;
  **not** the one-shot `Next` reuse cases, which test a mutable cursor .NET does not have, P4c-12), `runtime.test.ts`
  (empty pipeline, runtime as transport, options threading, close is a no-op toward the transport), `stage.test.ts` (order).
  From the Ruby 4c design's testing strategy (local; the gem tests are not): its `PIPE-1`, `PIPE-2`, `PIPE-6`, `PIPE-16`,
  `PIPE-22`, `PIPE-23`/`PIPE-24`, `PIPE-35` and `PIPE-38` cases, which the decision table above adopts by name. The plan
  records that Ruby's `pipeline_test.rb`, `test/dexpace/pipeline/` and `docs/sdk-documentation/pipelines.md` are absent locally.
- **Not ported:** Ruby's cursor-scoped state map and its write restriction (R11 there; retired here by §5.1 "Cursor-scoped
  state retires with the marker"), Ruby's single-use `#call` latch and Node's `CursorAlreadyAdvancedError` (P4c-12), Node's
  `exchangeSource`, any case asserting a host-language fact (constraint 10).
- **NativeAOT.** PR 6 extends `AotSmoke`: a sync `Send` through `CreateDefault` over the existing in-process handler, an
  `ErrorMappingPolicy` mapping a 404 into `HttpResponseException` with a readable buffered body, and an `HttpPipeline` used as
  an `IAsyncHttpClient` under a nested builder.

---

## Coupling with 4a, 4b and later phases

- **4a.** Shape agreement (the card's caution): `PipelineContext` carries `CallKey` and `Instrumentation`; 4c never calls
  the store. 4c promotes per transmission, adds `Response`'s internal exchange-close hook, and closes the furthest link on
  failure (4a's proposal, confirmed). If 4a's merged API differs, the plan maps the call sites of position I and nothing
  else changes. 4a's P4a-13 (the `CallKey` route) is the lead's; position I item 4 says what 4c does under option 1.
- **4b.** `ErrorMappingPolicy` installs 4b's `ErrorMappingStep` through 4b's fold; `RECOV-15`/`RECOV-16` are 4b's rows, built
  by 4b (P4c-17, with P4b-16). 4b's `RECOV-32`/`RECOV-33` defaults are consumed unchanged. 4b repoints `Disposal` onto
  `ExceptionTrail`/`ExceptionFacts` (3b hand-off); 4c's dispose sites inherit that.
- **5a** makes `DexpaceClientOptions` immutable (`PIPE-17`'s "immutable" half is §8.2's obligation), decides the per-call
  `DexpaceClientOptions` overloads (P4c-15), and may take over `BlockingWait` as `CFG-15`/`CFG-17`'s sync wait.
- **5b/5c** restructure `InstrumentationPolicy` (its waiver stands); 5c opens the operation span at `Operation` and populates
  the bundle the `DispatchContext` carries.
- **6a** rewrites `RetryPolicy` on `ProcessCoreAsync`, builds the recovery-stack engine (`RECOV-17`–`RECOV-30`, `RECOV-34`) and
  re-classifies a re-sent error through 4b's `ErrorBodyBuffer` (`RETRY-36`).
- **6b** rewrites `RedirectPolicy` over `context.SeedRequest` and removes `StripSensitiveHeadersOnCrossOrigin`; its `REDIR-11`,
  `REDIR-24`, `REDIR-25` rows cite 4c.
- **6c** stamps against `context.SeedRequest`'s origin (`AUTH-29`) and gives `AccessTokenCache` a sync path, retiring
  `BearerTokenAuthPolicy`'s bridged `GetCredential`.
- **7c** may take `HttpPipeline` as an `IAsyncHttpClient` in `Pageable` (`PIPE-26`'s "backing a paginator").
- **8b** makes the sync terminal real end to end (`PIPE-28`'s ⏳ clause).

---

## Design and roadmap corrections owed at close-out

Dated corrections, in PR 6, each naming the ruling that caused it:

- **§5.1:** the **As built** line; `PipelineContext`'s member list as built (position B, including the typed bag replacing
  the string bag, P4c-4); the builder reads `Stage` once (P4c-8); cross-stage errors are `ArgumentException` (P4c-10);
  `ErrorMappingPolicy` wraps 4b's step (P4c-17); the empty-body reading of `BODY-30` (P4c-18). (The `ErrorBodyBuffer`
  correction, fact 12, is 4b's to make with P4b-16.)
- **§5.3:** the **As built** line; the sync terminal choice (P4c-13); the options capture at `Build` (P4c-11).
- **§10 entry 14:** the topic label `async-redirect-pillar` and the as-built note (position H, P4c-20).
- **§11:** a new item for `Operation` as a sixth singleton outside `PIPE-2`'s pre-redirect slot (P4c-6) and for `PIPE-3`'s
  absent post-pillar slots (P4c-7), numbered at the next free number when they land (4a and 4b may add items concurrently).
- **§12:** the `PIPE` row's notes that `PIPE-1`, `PIPE-7`, `PIPE-13` and `PIPE-39` are not cited by ID are closed (this design
  argues each by ID).
- **Roadmap:** the phase 4 row's `sdk-design refs` cell gains this design's link (appended); a dated status note that records
  the 4a → 4b → 4c dependency edge (P4c-2) as the roadmap's ordering rule requires, alongside the phase close note.
- **`docs/first-release.md`:** the first "Behavioural asymmetries" entry, `async-redirect-pillar`.
- **Phase 1 checklist:** S6's and S8's ⏳ 4c clauses point at 4c's checklist rows (`PIPE-16`, `PIPE-37`) and the classes above.
- **3b checklist:** `BODY-30`, `BODY-31`, `HTTP-52` lose their ⏳ 4c half, citing `ErrorMappingPolicyTests` and 4b's
  `ErrorBodyBufferTests` (4b's dated correction names 4b for the buffer, P4b-16).

---

## Risks and open questions

1. **4a/4b API drift.** 4c is designed against names. Mitigation: the consumed-types table is a contract; the plan's first
   task in PR 2 and PR 4 re-derives every call site from the merged code. A declined item reopens the consuming row.
2. **The error-body buffer's owner** (P4c-17). The phase 1 checklist, the 3a and 3b designs and the card route
   `ErrorBodyBuffer` to 4c; 4b's design claims it (P4b-16) and this design adopts that. If the lead keeps P3a-13 instead,
   P4c-17's fallback applies and PR 4 grows by the buffer and the re-home.
3. **PR 2's size.** It is the whole signature change. Mitigation: PR 1 lands the builder first; PR 2's diff is mechanical in
   the eleven policy classes and the test policies, and its review checklist is the migration table.
4. **Sync path holes before 6c and 8b.** `BearerTokenAuthPolicy.GetCredential` and the SystemNet terminal block on async
   until 6c and 8b. They are documented bridges, counted in `PIPE-28`'s ⏳ clause, and `SyncPath.GetCompletedResult` fails
   loudly if a shipped policy's sync drive ever suspends.
5. **Two option types on the surface** (P4c-15). Acceptable until 5a; the overload documentation says which wins.

---

## Rulings

Taken by this design in the absence of a human reviewer. Each lists the options considered. Five are **open for the lead**.

1. **P4c-1 — Row ownership.** 4c owns `PIPE-1`–`PIPE-40` (40: 36 MUST, 4 SHOULD) and nothing else; 4a owns `CTX-1`–`CTX-20`
   (20), 4b `RECOV-1`–`RECOV-34` (34, including `RECOV-32`/`RECOV-33`, and `RECOV-17`–`RECOV-30`/`RECOV-34` ⏳ 6a). 20 + 34 +
   40 = 94. `REDIR-11`/`REDIR-24`/`REDIR-25` are 6b's rows on which 4c works; `OBS-25`/`OBS-26` are 5c's. Options: taking the
   `REDIR` rows into 4c (rejected: Phase List row 6 places them, and 94 would become 97).
2. **P4c-2 — The order 4a → 4b → 4c is a dependency for 4c. Open for the lead.** It inverts the roadmap's "convenience" label
   for the 4a→4c and 4b→4c edges, because the .NET design makes `PipelineContext` carry 4a's chain and `ErrorMappingPolicy` run
   4b's fold (Prerequisites). PR 1 alone is independent. Options: keep the convenience and build 4c against stubs (rejected:
   two throwaway shapes and a second migration); move the context wiring into 4a (rejected: 4a would edit `HttpPipeline`,
   which 4c rewrites). The roadmap's rule requires a dated status note, which is the lead's to accept.
3. **P4c-3 — The signature is design §5.1's**, with `Process` virtual and the documented bridge as its default, and the null
   guard kept for both transport and policy results (position A).
4. **P4c-4 — `PipelineContext`.** Sealed class, internal constructor, call-scoped shared state plus per-drive copies
   (`ForAttempt`, `ForHop`, `WithActivity`, `WithCancellationToken`); no `Request`/`Response`; a typed bag keyed by
   `PipelinePropertyKey<T>` instances (reference identity), replacing the public string-keyed bag, whose `"dexpace.auth.origin"`
   key was overwritable by any policy (§6.2). Options: keep the string bag (rejected: the hijack §6.2 describes); drop the bag
   (rejected: §5.1 keeps it for legitimately per-call state).
5. **P4c-5 — The seed origin.** `HttpPipeline` fixes `SeedRequest` (the request handed to `SendAsync`/`Send`) on the call-scoped
   state at entry; `RedirectPolicy` and `AuthorizationPolicy` both read it (coupling obligation 2, §10 entry 15). The as-built
   first-seen origin differs only when a policy above auth rewrites the URL, which no shipped policy does; the seed is the
   design's definition.
6. **P4c-6 — `Operation` is a sixth singleton outside `PIPE-2`'s pre-redirect slot. Open for the lead.** Design §5.1's table
   lists it; no §10 or §11 entry records the reading against `PIPE-2`/`PIPE-4`, which name five pillars. Proposed as a new §11
   item: both `Operation` and `PerCall` run outside both loops and see one response, so every boundary property `PIPE-2` and
   `PIPE-37` protect holds. Options: make `Operation` a non-pillar (rejected: two deadline policies would nest); fold it into
   `PerCall` (rejected: the deadline must wrap the error mapping).
7. **P4c-7 — `PIPE-3` is met in part.** No slot after `Auth`, `Diagnostics` or `Serde`; the sparse keys make adding one
   additive. Options: add `PostAuth = 550` now (rejected: no consumer, and design §5.1's table is frozen to routine work).
8. **P4c-8 — The builder reads `Stage` once**, recording `PipelineEntry`s, and rejects an undefined `PipelineStage` value
   (facts 3, 6).
9. **P4c-9 — Collisions at insertion**, `InvalidOperationException` naming the stage, both types and `Replace<T>`;
   `ReferenceEquals` idempotence on pillars only; a non-pillar re-add appends (the letter of `PIPE-6`/`PIPE-7`).
10. **P4c-10 — The edit surface.** `Prepend`, `AddRange`, `PrependRange` (all-or-nothing, the `PIPE-38` asymmetry documented);
    cross-stage insert/replace throws `ArgumentException`; a missing anchor stays `InvalidOperationException`; no new public
    exception types. Options: Node's five error classes (rejected: no caller catches composition errors by type).
11. **P4c-11 — Options and seeding.** `Build(transport)`/`Build(transport, options)` capture `DexpaceClientOptions`;
    `Flatten`/`Nest` are named static factories; `Build()` builds over the seeded transport.
12. **P4c-12 — The fork is the runner value.** No single-use latch, no `Fork()` method, no pillar-only restriction on
    re-driving: each restriction in the siblings guards a mutable cursor this port does not have (P11).
13. **P4c-13 — The real sync path.** `ProcessCoreAsync(…, bool async)` in every shipped policy; the sync terminal is the
    transport when it implements `IHttpClient`, else `AsBlocking()`, decided at `Build`; `BlockingWait` for the retry sleep;
    `AuthorizationPolicy.GetCredential` with the documented bridge as default until 6c. `PIPE-28` ✅; ⏳ 8b.
14. **P4c-14 — `HttpPipeline` implements both seams explicitly**; its dispose is a no-op toward the transport and leaves it
    usable (`SEAM-15` documented); the allow-list gains a public exemption. Options: an `ObjectDisposedException` latch
    (rejected: it suggests a release that did not happen, which `PIPE-27` forbids).
15. **P4c-15 — Keep `SendAsync`/`Send(Request, DexpaceClientOptions, CancellationToken)` beside the new `RequestOptions`
    overloads. Open for the lead.** They keep four `Security` classes and `Pageable` compiling unedited (the AOT smoke, `InstrumentationPolicyTests` and two README samples take a one-token edit in PR 3, because the overloads lose `= default`, fact 9), at the
    price of two option types on one surface until 5a. Options: remove them (four `Security` classes and `Pageable` edited);
    `[Obsolete]` them now (a warning-as-error in every caller, i.e. the same edits).
16. **P4c-16 — `SendAsync<T>`/`Send<T>`** take the handler, the `RequestOptions` and the token; the response is disposed after
    the handler in every outcome; a dispose failure is suppressed onto the primary through `ExceptionTrail`.
17. **P4c-17 — The error-mapping split follows 4b's P4b-16. Open for the lead, jointly with P4b-16.** 4b builds
    `ErrorBodyBuffer` (sync and async), `ErrorMappingStep` and the `EnsureSuccessAsync` re-home; `RECOV-15`/`RECOV-16` stay 4b's
    rows on 4b's tests. 4c builds `ErrorMappingPolicy` as a thin `PerCall` wrapper over that step through 4b's fold, the
    `XCUT-8` factory, `BODY-30`'s no-body clause on the policy, and the sync `Response.EnsureSuccess`. This reassigns the
    buffer from the routing every earlier document recorded (phase 1 checklist S8, P3a-13, the 3b hand-off). Options: keep
    P3a-13 (**the fallback**: 4c builds `ErrorBodyBuffer` with a sync and an async capture over `StreamCopy.DrainUpTo(Async)`
    and re-homes `EnsureSuccessAsync`, 4b's step calls it once PR 4 lands, and `RECOV-16`'s row cites 4c's `ErrorBodyBufferTests`);
    move `RECOV-15`/`RECOV-16` into 4c (rejected: breaks the prefix partition the census rests on).
18. **P4c-18 — The error-mapping defaults.** Not in `CreateDefault` (design §5.1's .NET idiom); a sync `EnsureSuccess`;
    `EnsureSuccessAsync`'s re-home is 4b's (P4c-17); "a response with no body" is the empty replayable body,
    which the policy maps without a drain (the exception carries the response as it is, holding no connection).
19. **P4c-19 — The preset.** `PipelineBuilder.AddStandardResilience(TimeProvider? timeProvider = null, ILogger? logger = null)`
    installs `Operation`, `Redirect`, `Retry` and `Diagnostics` into empty pillars only, checking all four first;
    `CreateDefault` is a new builder plus the `PerCall`/`PerAttempt` defaults plus the preset plus the optional auth policy;
    `CreateEmpty` is the step-less shape.
20. **P4c-20 — `PIPE-32`/`REDIR-25`.** Built reversed, recorded by dated correction under §10 entry 14 with the topic label
    `async-redirect-pillar` (position H); `PIPE-32` ✅ documentation, 🚫 behaviour; `docs/first-release.md` records the asymmetry.
21. **P4c-21 — The context-chain wiring confirms 4a's proposal** (position I): one `DispatchContext` per call at entry,
    `PromoteToRequest`/`PromoteToExchange` per transmission at the runner's terminal, the exchange closed by the response's
    dispose through a hook 4c adds, the furthest link closed on non-fatal failure; the pipeline never calls the store; policies
    see the key and the bundle only. Options: one promotion per call with the seed request (this design's first draft;
    rejected because the store's reader, a `DelegatingHandler`, should see the request actually sent).
22. **P4c-22 — The `Security` classes.** `EnsureSuccessErrorMappingTests`, `RedirectCredentialHygieneTests`,
    `AuthHttpsGuardTests`, `RetryPacingOverflowTests` and `RedirectWireTests` unedited; `ReDriveRequestIsolationTests` moved
    mechanically (two policy bodies, one enum token `PerCall` → `PerHop`, one added structural fact). **Open for the lead**,
    because constraint 5 makes any edit to a `Security` class review-blocking, and the `PerHop` token is a semantic edit whose
    absence would fail the test (fact 8).
23. **P4c-23 — `IdempotencyPolicy` and `ClientIdentityPolicy` stay at `PerCall`**, which now means once per call, outside the
    redirect loop: the idempotency key is stamped once and every hop carries it because the redirect follower builds each hop
    from its held request; `User-Agent` likewise. Options: move them to `PerHop` (rejected: per-hop stamping is what the
    as-built property bag existed to paper over).

---

## Deviation Ledger

| ID | Decision | Touches | Kind | Argued in | Route |
|---|---|---|---|---|---|
| P4c-6 | `Operation` is a sixth singleton stage outside the spec's pre-redirect slot | `PIPE-2`, `PIPE-4` | a reading: §5.1 decides it, no §10/§11 entry records it | [Position C](#c-stages) | New design §11 item (PR 6) |
| P4c-7 | No user slot after `Auth`, `Diagnostics` or `Serde` | `PIPE-3` (a SHOULD, met in part) | a reading | [Position C](#c-stages) | New design §11 item (PR 6) |
| P4c-12 | The fork primitive is the immutable runner value; no single-use latch | `PIPE-15` | mechanism (§5.1 already argues it) | [Decisions](#decisions-one-per-requirement-row), `PIPE-15` | Design §5.1 **As built** line (PR 6) |
| P4c-13 | The sync terminal is the transport's own `Execute` when it has one, else the documented `AsBlocking` bridge; the sync path is real down to the transport until 8b | `PIPE-28` | mechanism | [Position E](#e-the-real-synchronous-path) | Design §5.3, dated correction (PR 6) |
| P4c-18 | "A response with no body" is the empty replayable body, which the policy maps without a drain | `BODY-30` (3b's row) | a reading | [Position G](#g-errormappingpolicy-over-4bs-outcome-folds) | Design §5.1, dated correction (PR 6) |
| P4c-20 | The async standard pipeline follows redirects | `PIPE-32`, `REDIR-25` | the existing §10 entry 14, built and labelled | [Position H](#h-the-pipe-32redir-25-reversal) | Design §10 entry 14, dated correction with the topic label `async-redirect-pillar` (PR 6) |

No ruling leaves a MUST's letter unmet on a stated domain beyond what §10 entries 8, 12 and 14 already record, so no new §10
entry is opened; P4c-20 labels entry 14.

**Correction 2026-10-07 (phase 5c, P5c-2).** "5c opens the operation span at `Operation`" is built at the outer edge of that stage, in
`HttpPipeline.SendCoreAsync` at call entry, not inside `OperationPolicy`: the bundle and `CallKey` are fixed when the `DispatchContext` is built and
the `Operation` pillar is replaceable. The span therefore exists for every pipeline shape and encloses `OperationPolicy`'s deadline. The interim
"`FromActivity(Activity.Current)` (or `None`)" bundle is gone. See the
[5c design](../../phase5/phase5c/2026-10-07-phase5c-tracing-design.md), position A.
