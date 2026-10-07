# Phase 4a — Execution Context: Design

**Status:** Draft, for review. Written 2026-10-05 against `main` at `0332cef` (2a, 2b, 3a and 3b merged). The scope
authority is the roadmap's Phase 4 card and Phase List row 4 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`).
Brainstormed without a reviewer: every question the brainstorm would have asked is answered as a numbered ruling
(`P4a-1` … `P4a-16`, [Rulings](#rulings)), and the six that need the lead's sign-off say **open for the lead**. 4b
(recovery) and 4c (pipeline) were brainstormed in parallel; their documents were not available to this one, so the
[interface 4a hands to 4c](#the-interface-4a-hands-to-4c) is stated as a proposal that 4c confirms or contests.

**What this document is.** The sub-phase design for 4a. It gives one decision per requirement row (20 rows), the shape of
every type 4a adds, the census that shows how the three sub-phases add up to the card's 94 rows, the migration from the
as-built code, the landing order in pull-request-sized steps, the tests, the interface 4c and 5c build on, the design
corrections owed at close-out, and the rulings, which double as the deviation-ledger IDs (roadmap constraint 7).

**What this document is not.** It is not the plan (numbered TDD tasks) and not the checklist. It does not restate design
§5.4's argument for keeping a store, for refusing an SDK `AsyncLocal`, or for comparing by slot reference; it cites them
(roadmap constraint 9) and records the decision against each row. It argues only what §5.4 leaves open and the places
where verification on the pinned runtime changed the obvious answer. It does not design 4c's `PipelineContext`, its
promotion points, or 5c's population of the bundle.

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor | Kind | State at `0332cef` |
|---|---|---|
| Phase 0 (warnings as errors, `RS0016`/`RS0017`, `RS0030` with `BannedSymbols.txt`, `MA0051` at 70 lines, `CA2007` on `src/`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. The `RS0030` entries for `ConditionalWeakTable<,>`, `WeakReference<T>`, `WeakReference` and the value-comparing `ConcurrentDictionary.TryRemove(KeyValuePair<,>)` **already exist** (phase 0, PR #21). 4a builds the first code those entries were written for, and adds one (`AsyncLocal<T>`, P4a-11). |
| 2a (`Request` as a sealed record with value equality and a redacting `ToString`; `Response` as a disposable sealed class) | **dependency** | Met. The promotions add exactly these two artifacts (`CTX-2`). |
| 2b (`OperationDescriptor.OperationId`, `string?`, non-blank when set) | **dependency** for `SEAM-28`'s carrier | Met. The 2b checklist's `SEAM-28` row reads "✅; ⏳ 4a" for the attachment to the context chain (P4a-15). |
| 3b (the `Response` dispose latch) | **convenience** | Met. 4a stores a `Response` and never reads or disposes one; closing a context never disposes the response it carries. |
| 4b | **none** | `CTX` consumes no `RECOV` requirement, and `RECOV` consumes no `CTX` requirement. `Close` cannot throw (`CTX-18`), so 4a has nothing to hand `Disposal` or `ExceptionTrail`. |
| 4c | **none in this direction**; 4c depends on 4a | The roadmap's segmentation rule calls 4a / 4b / 4c a convenience ordering, with one caution: 4c's call-scoped `PipelineContext` carries 4a's bundle, so the two must agree on its shape. The orchestrator's constraint for this run states the dependency explicitly — **4c consumes 4a** (`DispatchContext`, `RequestContext`, `ExchangeContext`, `CallKey`, the store over `BoundedMap`) — and nothing in 4a consumes 4c. 4a can land first and alone. |

**Entry criterion of the card** ("2b and 3b have exited") is met.

---

## Governing documents, and the phase-start queries

- **Normative.** `docs/product-spec/07-execution-context-model.md` (read in full; it is 33 lines) and the twenty `CTX`
  rows of appendix C, which are the canonical text. Read alongside, because they fix values 4a carries or shares:
  `OBS-25`, `OBS-26`, `OBS-27` (the bundle's sentinels and flavours), `XCUT-11`, `XCUT-14` (the bounded-map rule
  `CTX-11` is the first instance of) and `AUTH-19` (the second instance), and `SEAM-28`.
- **Design.** §5.4 in full (the chain, P13 against `AsyncLocal`, the call key, the store and its .NET reader, P13 against
  the value-comparing `TryRemove`, the bound); §8.1 ("The correlation bundle is `Activity`", the `null` no-op, the
  `SuppressFlow` exception); §9.1 (the banned-API gate); §10 entries 23 (`activity-as-tracing-model`) and 24
  (`trace-id-flavours`); §11 items 11 (embedded MUSTs in `CTX-16`/`CTX-20`) and 30 (the store's reader); §12's `CTX` row.
- **Roadmap.** The Phase 4 card; coupling obligation 1 ("`CTX-14`/`CTX-15` and `OBS-25`/`OBS-26` are one shape. Phase 4a
  fixes the correlation bundle (`ActivityContext`, §5.4). Phase 5c populates it. Phase 5c may not redefine it."); the
  segmentation rule's phase 4 paragraph; the 2026-10-02 status note's hand-off "4a: attaches
  `OperationDescriptor.OperationId` to the context chain (`SEAM-28`)".
- **Styleguide.** `10-api-design.md` (10.1 minimal surface), `06` (records, immutability), `09-concurrency.md` (9.4
  `Interlocked` for shared counters, 9.6 no fire-and-forget), `13-resource-management.md` (why a context is not
  `IDisposable`, P4a-7).
- **Siblings, read locally.** `nodejs-sdk@c0ff3fd` `packages/core/src/context/` (`context.ts`, `store.ts`,
  `instrumentation.ts`, `errors.ts` and their tests). `ruby-sdk@90075b1` holds documents only (as 2a, 2b, 3a and 3b
  found): `docs/work/mvp/phase4/phase4a/2026-09-08-phase4a-execution-context-design.md` and
  `docs/knowledge/notes/execution-context.md`. The card's `ruby-sdk/gems/dexpace-core/test/dexpace/context/`,
  `context_store_test.rb` and `bounded_map_test.rb` are not in the local clone; their case lists are taken from the Ruby
  design's testing strategy and cross-checked against the Node tests. `ruby-sdk/docs/sdk-documentation/execution-context.md`
  does not exist in the clone (only `architecture.md` does).

**Phase-start queries** (`scripts/knowledge`, run 2026-10-05 with `DOTNET=~/.dotnet/dotnet`, because the system `dotnet` on
this host has no SDK):

- `--origin note --brief`: five notes across four topic files (`CA1062`, the `I` prefix, the `Async` suffix, xUnit
  `Assert` only, `LangVersion latest`). None touches the context model.
- `--section conflicts --brief`: fifteen entries across nine files; every harvested conflict is either conformed or
  overridden by one of the five notes. **No open conflict reaches 4a.**
- `--prefix-info CTX`: 20 canonical IDs, 16 MUST / 3 SHOULD (`CTX-12`, `CTX-16`, `CTX-20`) / 1 MAY (`CTX-13`); owning
  chapter ch.07; **20 of 20 substantive**, 0 roll-up only, 0 uncited.
- `--gaps CTX`: none. The appendix-B roll-up hazard does not fire for this prefix; the only extra reading budgeted is
  appendix C's text for `CTX-4`, `CTX-8` and `CTX-13`, whose chapter forms are abbreviated.
- `--req OBS-25`, `--req OBS-26`: both are phase 5 rows (Phase List row 5, `OBS-1`–`OBS-40`). 4a fixes the shape they
  test; 5c owns the rows (see [Census](#census-how-4a--4b--4c--94)).

---

## Verified facts that shape the decisions

Verified on 2026-10-05 on SDK 10.0.401 with a throwaway file-based program in the session scratchpad, never in the
repository. Rows and rulings cite them by number. Facts 1, 3 and 4 re-verify claims design §5.4 and §8.1 already make.

1. **`ConcurrentDictionary.TryRemove(KeyValuePair<,>)` compares values with `EqualityComparer<T>.Default`.** With a live
   record in the slot, removing a distinct but value-equal record returned `true` and evicted the live one (count 0).
   This is `CTX-9`'s trap, as §5.4 says.
2. **The same overload, and `TryUpdate`'s comparand, compare by reference for a sealed class with no `Equals` override.**
   A fresh slot wrapping the *same* context did not remove (`false`); the stored slot did (`true`); `TryUpdate` with a
   distinct comparand slot returned `false`. So a per-insert slot object turns both into reference-identity
   compare-and-swap operations, and a re-insert of the same context under the same key gets a new slot (no ABA).
3. **`default(ActivityContext)`** has trace id `00000000000000000000000000000000`, span id `0000000000000000`, flags
   `None`, `IsRemote` false — and **`TraceState` `null`, not empty.** A started W3C `Activity` with no trace state also
   reports `Context.TraceState == null`. `CTX-15` and `OBS-26` require *empty*, so the bundle normalises `null` to `""`.
4. **With no listener, `ActivitySource.StartActivity` returns `null`** and `HasListeners()` is `false`.
5. **`r with { }` on a sealed record whose properties are get-only compiles and clones**: the clone is `==` the source
   and not the same reference. This is the cheapest construction of `CTX-9`'s value-equal stale sibling.
6. **A record's synthesized equality includes non-public instance fields.** Two records differing only in a private
   field are unequal. A context that binds its store in a private field is therefore unequal to an otherwise identical
   context bound to a different store (P4a-6 accepts this).
7. **Enumerating a `ConcurrentDictionary` while removing and adding from inside the loop does not throw**, and visits a
   finite set. The drain may walk the live enumerator (it takes no lock), not `Keys` (which takes every lock and copies).
8. **`ActivityIdFormat` has exactly `Unknown`, `Hierarchical`, `W3C`.** An un-started `Activity` reports `Unknown`.
   `CTX-14`'s "trace-id encoding flavor" maps onto it without a new enum (P4a-3).

---

## Census: how 4a + 4b + 4c = 94

The card's exit is "94 rows". Phase List row 4 names three ID ranges and nothing else: ch.07 `CTX-1`–`CTX-20` (20), ch.08
§8.2 and appendix C `RECOV-1`–`RECOV-34` (34), ch.08 §8.1 `PIPE-1`–`PIPE-40` (40). 20 + 34 + 40 = **94**, exactly.

| Sub-phase | Owns (checklist rows) | Count | Notes |
|---|---|---|---|
| **4a** | `CTX-1`–`CTX-20` | **20** | every row ✅ (below); no ⏳, no N/A |
| 4b | `RECOV-1`–`RECOV-34`, including `RECOV-17`–`RECOV-30` and `RECOV-34` as ⏳ 6a, and `RECOV-32`/`RECOV-33` (the idempotency and client-identity defaults) | 34 | 4c consumes `RECOV-32`/`RECOV-33` in `DexpacePipeline.CreateDefault` and cites 4b's rows |
| 4c | `PIPE-1`–`PIPE-40` | 40 | the `PIPE-32`/`REDIR-25` reversal is recorded on the `PIPE-32` row and in §10 `async-redirect-pillar` |
| **Total** | | **94** | |

**Rows the roadmap ties to phase 4 that are not phase 4 rows**, so that nobody adds them to the count:

- `OBS-25`, `OBS-26` — coupling obligation 1 ties them to 4a's **shape**, but Phase List row 5 places the rows in phase 5
  (`OBS-1`–`OBS-40`, 40). 4a's bundle is the evidence 5c's rows cite; 4a's checklist mentions them in its notes column only.
- `REDIR-11`, `REDIR-24`, `REDIR-25` — coupling obligation 2 ties them to 4c's seed origin; Phase List row 6 places the rows
  in 6b.
- `SEAM-28` — a 2b row marked "✅; ⏳ 4a"; 4a supplies the carrier (P4a-15). The row stays in 2b's checklist.
- `XCUT-14` — 4a builds the first bounded map; the row is phase 10's audit.

The orchestrator's partition allowed for 4a to own non-`CTX` rows "only if the roadmap places that row in phase 4". The
roadmap places none, so **4a owns exactly 20**, and the arithmetic closes at 94 with the partition as given. Nothing here is
open for the lead.

---

## Decisions, one per requirement row

**How to read the table.**

- **Exit** uses the roadmap's constraint-3 legend.
- **Tests** are `[Trait("Category", "Unit")]` unless another category is named. Context tests live under
  `tests/Dexpace.Sdk.Core.Tests/Execution/` (namespace `…Tests.Execution`, which shadows no type); `BoundedMap` tests
  under `tests/Dexpace.Sdk.Core.Tests/Internal/`; architecture tests under `…/Architecture/`. Test names are the plan's
  to finalise; the ones below fix intent.
- **PR** points to [the landing order](#landing-order).

| ID | Level | Decision | Rationale / source | Types | Test approach | PR | Exit |
|---|---|---|---|---|---|---|---|
| `CTX-1` | MUST | Three **sealed records** over one closed abstract record `CallContext`: `DispatchContext`, `RequestContext`, `ExchangeContext`. Promotion is `DispatchContext.PromoteToRequest(…)` and `RequestContext.PromoteToExchange(…)`; `ExchangeContext` declares no promotion member, so "the exchange type exposes no method promoting back" is the absence of a method. The hierarchy is closed to other assemblies (P4a-5). | §5.4 "three sealed records … not one type with a stage field". | the three records, `CallContext` | `PromotionChainTests.Each_stage_exposes_exactly_its_artifacts` (reflection over public instance properties: Dispatch {`Key`, `Instrumentation`}; Request {+`Request`, `OperationName`}; Exchange {+`Response`}); `ExecutionContextArchitectureTests.The_exchange_context_declares_no_method_returning_a_context`; `…The_hierarchy_has_exactly_three_concrete_flavours` | 3 | ✅ |
| `CTX-2` | MUST | Each promotion returns a **new** instance carrying the **same** `InstrumentationContext` reference (a sealed class, so "same reference" is literal, P4a-3) and the same `CallKey`, and adds exactly one artifact; request→exchange also carries the same `Request` reference and `OperationName`. The operation name enters as an argument of `PromoteToRequest`; `DispatchContext` has no such property. The source is unchanged because records are immutable. | §5.4; Node `context.ts`. | the three records | `PromotionChainTests.Promoting_dispatch_adds_exactly_the_request_and_carries_key_and_bundle_by_reference` (`Assert.Same` on the bundle, `Assert.Equal` on the key), `…request_to_exchange_carries_request_and_operation_name_by_reference`, `…The_source_is_unchanged` | 3 | ✅ |
| `CTX-3` | MUST | One key for the whole chain; promotion copies it verbatim. All three flavours register under one slot, and each promotion overwrites the last. | §5.4. | the records, `ContextStore` | `ContextRegistrationTests.Every_flavour_of_one_chain_occupies_the_same_slot_in_turn` (isolated store; after each promotion `TryGet(key)` returns the newest link by reference) | 3 | ✅ |
| `CTX-4` | MUST | `readonly record struct CallKey` with `TraceId`, `SpanId` and `Sequence`; `Sequence` comes from `Interlocked.Increment` on one process-wide `long`, so the key is call-unique even when trace and span are equal or all-zero. Rendering `{trace}:{span}:{sequence}` (the reference's format) happens only in `ToString`. | §5.4 ("no string allocated unless rendered"); appendix C `CTX-4` "a port MAY key differently". | `CallKey` | `CallKeyTests.Two_keys_minted_from_identical_trace_and_span_differ`; `ContextRegistrationTests.Two_contexts_with_identical_trace_and_span_both_register` | 2, 3 | ✅ |
| `CTX-5` | MUST | Every context constructor takes `CallKey? key = null`; `null` mints a fresh key, an explicit key pins it. **`CallKey` has no public constructor** (P4a-2): a caller pins a shared key by minting one with `CallKey.Next(…)` and passing it to both constructors. `default(CallKey)` (sequence 0, never minted) is rejected with `ArgumentException`. Two default-constructed contexts are unequal because their keys differ (records include the key in equality). | §5.4; P4a-2. | the records, `CallKey` | `PromotionChainTests.Two_default_constructed_contexts_with_identical_fields_are_not_equal`, `…A_pinned_shared_key_makes_them_equal`, `…The_default_key_is_rejected` | 3 | ✅ |
| `CTX-6` | MUST | One counter, in `CallKey`, serves every flavour's default construction and every promotion-free mint. | §5.4. | `CallKey` | `CallKeyTests.Keys_minted_concurrently_across_threads_are_pairwise_distinct` (16 threads × 10 000, `HashSet` of `Sequence`); `PromotionChainTests.Default_keys_are_distinct_across_all_three_flavours` | 2, 3 | ✅ |
| `CTX-7` | MUST | Contexts are records with get-only properties and `readonly` fields; `InstrumentationContext` is a sealed class with get-only properties set at construction (its `ActiveSpan` reference is immutable; the `Activity` it points at is a live span by nature, P4a-3). The store is a `ConcurrentDictionary` under `BoundedMap`, so register, overwrite and remove on distinct keys need no external lock. | §5.4. | records, `InstrumentationContext`, `BoundedMap` | `ExecutionContextArchitectureTests.Every_instance_field_is_readonly` (the `ModelImmutabilityArchitectureTests` shape over the four types; the base record's private store field is `readonly`); `ContextStoreTests.Distinct_keys_register_overwrite_and_release_concurrently_without_loss` (parallel, then exact final state) | 2, 3, 4 | ✅ |
| `CTX-8` | MUST | `ContextStore.Set` (install-or-replace, never throws; promotion's only path) and `ContextStore.Add` (install only if absent; the loser gets `ArgumentException` whose message carries the key's rendering and whose `ParamName` is `context`, the `Dictionary.Add` precedent, P4a-9). Both are `internal`: `CTX-8` asks the store to **support** the strict affordance, and nothing public needs it (P4a-8). | appendix C `CTX-8`. | `ContextStore`, `BoundedMap` | `ContextStoreTests.Set_overwrites_and_never_throws`; `…Add_on_an_occupied_key_throws_naming_the_key`; `…A_rejected_Add_leaves_the_incumbent`; `…Concurrent_Add_of_one_key_admits_exactly_one_winner` (32 threads behind a `Barrier`; exactly one success, 31 `ArgumentException`s each naming the key) | 1, 3 | ✅ |
| `CTX-9` | MUST | `CallContext.Close()` removes the slot only when the occupant **is** this context: `BoundedMap.TryRemoveIfSame` reads the slot, compares `ReferenceEquals(slot.Value, value)`, then removes through the slot-reference `TryRemove(KeyValuePair<,>)` (facts 1, 2). An unknown or already-replaced slot is a no-op returning normally. | §5.4 P13; facts 1, 2. | `BoundedMap`, `ContextStore`, `CallContext` | `ContextStoreTests.A_value_equal_stale_context_does_not_evict_the_live_one` (the live context and `live with { }` — fact 5 — under one pinned key; closing the clone leaves the live one registered; closing the live one evicts); `BoundedMapTests.TryRemoveIfSame_compares_by_reference_not_value` | 1, 3 | ✅ |
| `CTX-10` | MUST | Only the furthest-reached link evicts: closing a promoted dispatch or request is a no-op, because the slot holds its successor. | §5.4. | as `CTX-9` | `ContextRegistrationTests.Closing_an_intermediate_link_leaves_the_successor_registered`; `…Closing_the_terminal_link_evicts_the_chain` | 3 | ✅ |
| `CTX-11` | MUST | `BoundedMap` takes a required capacity (≥ 1, else `ArgumentOutOfRangeException`) and drains after every insert; the context store's capacity is `ContextStore.DefaultCapacity = 10_000` (P4a-10). The count is an `Interlocked` field beside the dictionary, not `ConcurrentDictionary.Count` (§5.4). | §5.4; `XCUT-14`. | `BoundedMap`, `ContextStore` | `BoundedMapTests.An_insert_burst_past_the_capacity_ends_at_or_under_it`; `…A_capacity_below_one_is_rejected`; `…The_tracked_count_matches_the_dictionary_after_quiescence` | 1 | ✅ |
| `CTX-12` | SHOULD | **Built**: a post-insert drain **loop** over the dictionary's lock-free enumerator, re-entered until the count is at or under the capacity. Unlike Ruby (whose store sits behind one mutex, so the loop is degenerate — Ruby's corpus note), **the loop is load-bearing on .NET**: there is no global lock, so several inserters can each push the count over before any drains. | `XCUT-14` (a MUST of the same shape); fact 7. | `BoundedMap` | `BoundedMapTests.One_drain_call_removes_every_excess_entry` (deterministic: the drain is an `internal static` over the dictionary and the counter, seeded 100 entries over a capacity of 10, P4a-10); `…Concurrent_inserts_from_16_threads_converge_to_the_capacity` | 1 | ✅ |
| `CTX-13` | MAY | Victim selection is **arbitrary** (the enumerator's bucket order); no order, no LRU, and the just-inserted entry may itself be evicted. No test asserts that any particular entry survives. | appendix C `CTX-13`. | `BoundedMap` | `BoundedMapTests.A_capacity_one_map_holds_exactly_one_of_two_inserts` (asserts the count and that the survivor is one of the two, never which) | 1 | ✅ |
| `CTX-14` | MUST | The bundle is **`InstrumentationContext`**, a sealed class composing `System.Diagnostics.ActivityContext` (trace id, span id, flags, trace state, remoteness), a derived `IsValid`, a `TraceIdFormat` of type `ActivityIdFormat` (the encoding flavour, fact 8), the active span as `Activity?` (`null` is the no-op span, §10 entry 23), and the per-operation tracer factory as `StartActivity(string operationName, ActivityKind kind)` over `DexpaceDiagnostics.ActivitySource`. **Open for the lead** (P4a-3): it binds 5c. | §5.4, §8.1 "ActivityContext plus `PipelineContext.Activity` plus the static `ActivitySource`"; roadmap obligation 1. | `InstrumentationContext` | `InstrumentationContextTests.FromActivity_exposes_the_activitys_identifiers_flags_state_and_span`, `…FromContext_of_a_remote_parent_is_remote_and_has_no_active_span`, `…The_tracer_factory_starts_a_child_of_the_bundle_under_a_listener` | 2 | ✅ |
| `CTX-15` | MUST | `InstrumentationContext.None`, one static instance and the default of every constructor: all-zero ids, `ActivityTraceFlags.None`, `TraceState` `""` (fact 3 normalises `null`), `IsValid` and `IsRemote` false, `ActiveSpan` `null`, `TraceIdFormat` `Unknown`, and a `StartActivity` that returns `null` **whatever listeners exist**. `FromActivity(null)` and `FromContext(default)` return `None` (no allocation). The key stays call-unique under it (`CTX-4`). | §5.4 "`default(ActivityContext)`"; §10 entries 23, 24. | `InstrumentationContext`, `CallKey` | `InstrumentationContextTests.None_reserves_the_invalid_sentinels`, `…None_never_starts_an_activity_even_with_a_listener`, `…FromActivity_null_and_FromContext_default_return_None_itself` (`Assert.Same`); `CallKeyTests.Keys_minted_under_None_are_distinct` | 2 | ✅ |
| `CTX-16` | SHOULD | **Built**, with its embedded MUSTs (§11 item 11): `RequestContext.OperationName`/`ExchangeContext.OperationName` are `string?`, introduced by `PromoteToRequest(request, operationName)`, `null` or non-blank (`ArgumentException` otherwise, matching `OperationDescriptor.OperationId`), carried forward verbatim, and **advisory**: the key is minted before the name exists, and nothing in 4a reads it except `ToString`. | §5.4; §11 item 11; 2b's `OperationId` rule. | `RequestContext`, `ExchangeContext` | `PromotionChainTests.The_operation_name_is_absent_at_dispatch`, `…is_carried_forward_unchanged`, `…does_not_change_the_key`, `…blank_is_rejected` | 3 | ✅ |
| `CTX-17` | MUST | **Promotion registers; construction does not** (P4a-6). `DispatchContext`'s constructor touches no store; `PromoteToRequest` calls `store.Set(next)`, `PromoteToExchange` calls `store.Set(next)`. A directly constructed `RequestContext` or `ExchangeContext` (off-chain) is not registered either. **Open for the lead**: 4c must not register separately. | appendix C `CTX-17`, `CTX-8` "set … used by promotion". | the records, `ContextStore` | `ContextRegistrationTests.A_constructed_dispatch_context_is_not_registered`, `…An_off_chain_request_context_is_not_registered`, `…Closing_an_unpromoted_dispatch_is_a_no_op`, `…The_first_promotion_installs_the_first_entry` | 3 | ✅ |
| `CTX-18` | MUST | `DexpaceCallContexts.TryGet(CallKey, out CallContext?)` (public) and `ContextStore.TryGet` (internal) return `false`/`null` for an unknown key; `Close()` on an unknown or removed key is a no-op. A second `Close()` is a no-op through the identity check — **no latch** (P4a-7). | §5.4 ("`CTX-18`'s explicit-absent lookup"). | `DexpaceCallContexts`, `ContextStore`, `CallContext` | `ContextStoreTests.TryGet_of_an_unknown_key_returns_false_and_null`; `ContextRegistrationTests.A_double_close_is_a_no_op` | 3 | ✅ |
| `CTX-19` | MUST | The store holds contexts **strongly** (`ConcurrentDictionary` values are strong); `ConditionalWeakTable<,>`, `WeakReference<T>` and `WeakReference` stay banned in `src/` by `RS0030` (already present since phase 0); the cap, not the collector, is the backstop. | §5.4; §9.1. | `BoundedMap`, `BannedSymbols.txt` | `ContextStoreTests.Registered_contexts_survive_a_full_collection_with_no_other_reference` (1 000 exchange contexts with undisposed responses created in a non-inlined helper, three `GC.Collect()` + `WaitForPendingFinalizers`, all 1 000 resolvable); the ban probe (a throwaway `WeakReference<object>` in `src/` fires `RS0030`, recorded in the checklist, as 3a recorded `IO-22`); the build | 1, 4 | ✅ |
| `CTX-20` | SHOULD | **Built**, with its embedded MUST: the factory is `ActivitySource.StartActivity`, which returns `null` and allocates nothing with no listener (fact 4) and is documented thread-safe; `None`'s factory returns `null` unconditionally. | §5.4; §11 item 11; fact 4. | `InstrumentationContext` | `InstrumentationContextTests.The_tracer_factory_is_safe_under_concurrent_invocation` (parallel calls under the `TestSupport` `ActivityRecorder`, every activity distinct and stopped); `…With_no_listener_the_factory_returns_null` | 2 | ✅ |

**Twenty ✅, no ⏳, no N/A.** Design §12's `CTX` row reads 20 of 20 cited and defers nothing; 4a defers nothing of its own
scope. What 4a hands on (the transport stamp, 4c's wiring, 5c's population) is design work behind rows 4a already closes,
not part of any `CTX` row.

---

## Argued positions

### A. The bundle is one sealed class, not a bare `ActivityContext`

Coupling obligation 1 says "Phase 4a fixes the correlation bundle (`ActivityContext`, §5.4)", and §8.1 says the bundle "is
`ActivityContext` plus `PipelineContext.Activity` plus the static `ActivitySource`". Those are three things in two places,
and `CTX-14` wants nine members exposed by "each context". Three readings:

1. **The context records carry an `ActivityContext` only**; the active span lives on 4c's `PipelineContext`, the factory is
   the static source. Fails `CTX-14`'s "each context MUST carry … an active span, and a per-operation tracer factory" for
   any context outside a pipeline, and fails `CTX-2`'s "the same InstrumentationContext **reference**" in letter (a struct
   copy has no reference).
2. **The records carry three properties** (`ActivityContext`, `Activity?`, the source). Meets the letter, triples what
   every promotion copies and what 5c must keep consistent, and gives no single "the no-op bundle" value for `CTX-15`.
3. **One sealed class, `InstrumentationContext`, composing the three** (chosen). `CTX-2`'s same-reference test is
   `Assert.Same`; `CTX-15`'s default is one static instance, which is also `OBS-25`'s "no per-call allocation" on the
   untraced path; the class is built only through two factories (`FromActivity`, `FromContext`), so the identifiers and
   the active span cannot disagree. Its `ActivityContext` property keeps the obligation's literal type one dot away.

The name follows the specification's own term (`CTX-2`: "the same InstrumentationContext reference"). It collides with
nothing in the BCL. **Open for the lead** (P4a-3), because coupling obligation 1 forbids 5c to redefine it and §8.1's
wording is corrected by it.

**What 5c may and may not do.** 5c populates the bundle (calls `FromActivity` with the operation span it opens, or
`FromContext` with an extracted remote parent) and owns `OBS-25`/`OBS-26`'s rows. It may not rename, remove or retype a
member. Adding a member is a dated correction to this design, not a silent change.

### B. Promotion registers, and the context knows its store

`CTX-17` says registration "MUST happen at promotion time", and `CTX-8` says the overwrite is "used by promotion". Node
read that as "promotions are pure; the runtime installs after promoting" (`store.ts`: "Nothing in 4a calls this … 4c's
pipeline is the first caller"). That reading leaves `CTX-3` and `CTX-17` untestable in 4a and makes correctness depend on
every promotion site remembering a second call. Ruby gave each context a `store` member. 4a follows Ruby: each context
holds a `private readonly ContextStore _store`, the public constructors bind `ContextStore.Shared`, an `internal`
constructor overload binds an isolated store (tests only), and promotions carry the store forward. Fact 6 means contexts
bound to different stores are unequal; that is harmless, because production binds one store, and is stated in the
remarks. **Open for the lead** (P4a-6), because it constrains 4c: the pipeline promotes and never calls the store.

### C. The identity remove lives in `BoundedMap`, behind its own slot

§5.4 puts the identity compare in a `ContextSlot` type and the ban's message says "belongs only in the context store's slot
type". 4a generalises the slot into `BoundedMap<TKey, TValue>` (`where TValue : class`): every stored value is wrapped in
a fresh internal `Slot` (sealed class, no `Equals`), so `Set` is a slot-reference `TryUpdate`, and `TryRemoveIfSame` is a
reference check followed by a slot-reference `TryRemove(KeyValuePair<,>)` (fact 2). One scoped `#pragma warning disable
RS0030` in `BoundedMap` covers the one call; the ban's reason text is edited (not deleted) to name `BoundedMap`'s slot
(P4a-12). Costs: one 24-byte allocation per insert. Gains: 6c's `AUTH-19` nonce store and token cache (§6.3) reuse the
map without re-deriving the trap, and `XCUT-14`'s phase 10 audit finds one implementation.

A test pins the slot's shape: `BoundedMapTests.The_slot_type_declares_no_equality_override` (reflection: `Slot` declares
neither `Equals(object)` nor `GetHashCode`), so a later "tidy-up" into a record fails a test rather than silently
re-opening fact 1.

### D. The public surface

The design names one public member (`DexpaceCallContexts.TryGet`). 4a needs more, because `CTX-5` says "callers … MUST be
able to pin an explicit shared key at construction", 4c will expose the chain on `PipelineContext` to public policy
authors, and a policy's unit test has to build contexts. The surface ([Type shapes](#the-public-surface-publicapiunshippedtxt-core-only))
is: the four context records, `CallKey` (no public constructor), `InstrumentationContext`, and `DexpaceCallContexts`.
`ContextStore` and `BoundedMap` are internal (`Set`, `Add`, `TryGet`, `Release`, the cap). Promotion and `Close()` are
public because a context built outside a pipeline must be closable, and `NFR-4` would lock an accidental surface either
way; this one is chosen. **Open for the lead** (P4a-8), as every new public name is locked at the first release.

### E. The route of the `CallKey` to a `DelegatingHandler`

§5.4 and §11 item 30 keep the store because a `DelegatingHandler` below the SystemNet transport can resolve the live call
from a `CallKey` stamped into `HttpRequestMessage.Options` under a public `HttpRequestOptionsKey<CallKey>`. The design does
not say how the key reaches the transport: the SPI is `ExecuteAsync(Request, RequestOptions, CancellationToken)` (2b),
and none of the three carries a call key. Options:

1. **`RequestOptions` gains a `CallKey?`** that the pipeline sets before the transport (2b's hand-off already has 4c
   carry the caller's `RequestOptions` on the context). Visible, typed, no ambient state; a SEAM-surface change.
2. **`Request` carries it.** Rejected: the request is the wire value, value-equal by content; a per-call key would make
   two identical requests unequal and leak call identity into `Request`'s equality (`HTTP-46`).
3. **Ambient (`AsyncLocal`).** Rejected by §5.4 P13 and by P4a-11.

4a ships **neither the option key nor the stamp**: both live in `Dexpace.Sdk.Http.SystemNet` (the key is an
`HttpRequestOptionsKey<T>`, a `System.Net.Http` type core does not otherwise need), and the stamp needs option 1 first.
4a recommends option 1, for 4c to add when it carries `RequestOptions` on `PipelineContext`, and 8b to stamp.
**Open for the lead** (P4a-13): a SEAM-surface change, a route §5.4 leaves unstated, and the store has no production
reader until it lands. Until then the store is exercised by its own tests and by `DexpaceCallContexts.TryGet`.

---

## Type shapes

All new code is in `Dexpace.Sdk.Core`, namespace `Dexpace.Sdk.Core.Execution` (folder `src/Dexpace.Sdk.Core/Execution/`,
P4a-1), except `BoundedMap`, which joins `Disposal` and `SdkVersion` in `Dexpace.Sdk.Core.Internal`. Every file carries the
license header; every public member a `///` summary; every method stays under `MA0051`'s 70 lines.

### `CallKey` (`CTX-4`, `CTX-5`, `CTX-6`, `CTX-15`)

```csharp
public readonly record struct CallKey
{
    private static long s_sequence;                       // process-wide, all flavours (CTX-6)

    private CallKey(ActivityTraceId traceId, ActivitySpanId spanId, long sequence) { … }

    public ActivityTraceId TraceId { get; }
    public ActivitySpanId SpanId { get; }
    public long Sequence { get; }                         // ≥ 1 for a minted key; 0 only for default(CallKey)

    public static CallKey Next();                          // zero ids (the untraced sentinel)
    public static CallKey Next(in ActivityContext context);// ids from the context, sequence from Interlocked.Increment

    public override string ToString();                     // "{traceId:32 hex}:{spanId:16 hex}:{sequence}"
}
```

Equality is the synthesized field-wise equality (all three). `long` cannot wrap in practice (2^63 increments). `Next` is
allocation-free; `ToString` allocates one string and is called only for messages and diagnostics.

### `InstrumentationContext` (`CTX-2`, `CTX-14`, `CTX-15`, `CTX-20`; shape for `OBS-25`/`OBS-26`)

```csharp
public sealed class InstrumentationContext
{
    public static InstrumentationContext None { get; }               // CTX-15; one instance
    public static InstrumentationContext FromActivity(Activity? activity);       // null -> None
    public static InstrumentationContext FromContext(in ActivityContext context);// default -> None; no active span

    public ActivityContext ActivityContext { get; }
    public ActivityTraceId TraceId { get; }
    public ActivitySpanId SpanId { get; }
    public ActivityTraceFlags TraceFlags { get; }
    public string TraceState { get; }            // never null; "" when absent (fact 3)
    public ActivityIdFormat TraceIdFormat { get; }// Unknown for None; the activity's IdFormat otherwise (fact 8)
    public bool IsValid { get; }                 // TraceId and SpanId both non-zero (OBS-26)
    public bool IsRemote { get; }
    public Activity? ActiveSpan { get; }         // null = the no-op span (§10 entry 23)

    public Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal);
}
```

`StartActivity` returns `null` on `None`; otherwise it calls `DexpaceDiagnostics.ActivitySource.StartActivity(operationName,
kind, parentContext)` with this bundle's `ActivityContext` as the explicit parent, so the span parents to the call and not to
whatever `Activity.Current` is at the call site. It validates `operationName` (non-blank). No `Equals` override: equality is
reference identity, which is what `CTX-2` asks to be carried.

**No Datadog flavour.** `TraceIdFormat` never reports one; §10 entry 24 (`trace-id-flavours`) already records `OBS-27`'s
Datadog clause as unmet on .NET, and 4a adds nothing to it.

### The context records (`CTX-1`–`CTX-3`, `CTX-5`, `CTX-9`, `CTX-10`, `CTX-16`, `CTX-17`)

```csharp
public abstract record CallContext
{
    private readonly ContextStore _store;
    private protected CallContext(InstrumentationContext? instrumentation, CallKey? key, ContextStore store);

    public CallKey Key { get; }
    public InstrumentationContext Instrumentation { get; }

    public void Close();                         // CTX-9/10/18: evict iff this is the occupant; never throws
    public sealed override string ToString();    // P4a-14
    internal abstract …;                         // closes the hierarchy to other assemblies (P4a-5)
}

public sealed record DispatchContext : CallContext
{
    public DispatchContext(InstrumentationContext? instrumentation = null, CallKey? key = null);
    internal DispatchContext(InstrumentationContext? instrumentation, CallKey? key, ContextStore store);
    public RequestContext PromoteToRequest(Request request, string? operationName = null);   // registers (CTX-17)
}

public sealed record RequestContext : CallContext
{
    public RequestContext(Request request, string? operationName = null,
        InstrumentationContext? instrumentation = null, CallKey? key = null);
    public Request Request { get; }
    public string? OperationName { get; }
    public ExchangeContext PromoteToExchange(Response response);                             // registers
}

public sealed record ExchangeContext : CallContext
{
    public ExchangeContext(Request request, Response response, string? operationName = null,
        InstrumentationContext? instrumentation = null, CallKey? key = null);
    public Request Request { get; }
    public Response Response { get; }
    public string? OperationName { get; }
}
```

- An explicit `key` of `default(CallKey)` throws `ArgumentException`; `null` mints `CallKey.Next(instrumentation.ActivityContext)`.
- `instrumentation: null` means `InstrumentationContext.None`.
- Properties are get-only, so `with { X = … }` does not compile and `with { }` clones (fact 5). A clone is value-equal,
  reference-distinct, and its `Close()` is a no-op — the `CTX-9` test vector.
- The `internal abstract` members (`StageName`/`AppendDetail`) are what close the hierarchy: a record in another assembly
  cannot implement an internal abstract member, so a fourth flavour cannot be declared outside core (CS0534). The
  `private protected` constructor closes nothing: the record's synthesized copy constructor is protected, so another
  assembly can still chain to it (`: base(original)`). The plan verifies the compiler error with a throwaway probe and adds
  an architecture test that `CallContext` keeps at least one internal abstract member.
- `Close()` does not dispose `Response` (the caller or 4c's pipeline owns the response's lifetime) and never throws.
- **Not `IDisposable`** (P4a-7).

### `DexpaceCallContexts` (`CTX-18`; the reader of §11 item 30)

```csharp
public static class DexpaceCallContexts
{
    public static bool TryGet(CallKey key, [NotNullWhen(true)] out CallContext? context);   // over ContextStore.Shared
}
```

### `ContextStore` and `BoundedMap` (internal; `CTX-7`–`CTX-13`, `CTX-18`, `CTX-19`)

```csharp
internal sealed class ContextStore
{
    internal const int DefaultCapacity = 10_000;            // P4a-10
    internal static ContextStore Shared { get; }
    internal ContextStore(int capacity);
    internal void Set(CallContext context);                  // CTX-8 set
    internal void Add(CallContext context);                  // CTX-8 put; ArgumentException naming the key
    internal bool TryGet(CallKey key, [NotNullWhen(true)] out CallContext? context);
    internal bool Release(CallContext context);              // identity remove; false when not the occupant
    internal int Count { get; }
}

internal sealed class BoundedMap<TKey, TValue> where TKey : notnull where TValue : class
{
    internal BoundedMap(int capacity, IEqualityComparer<TKey>? comparer = null);
    internal int Capacity { get; }
    internal int Count { get; }                              // Interlocked counter, not ConcurrentDictionary.Count
    internal void Set(TKey key, TValue value);               // TryAdd, else slot-reference TryUpdate; loop; drain
    internal bool TryAdd(TKey key, TValue value);            // drain on success
    internal bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value);
    internal bool TryRemove(TKey key);
    internal bool TryRemoveIfSame(TKey key, TValue value);   // reference identity (Position C)
    internal static void Drain(ConcurrentDictionary<TKey, Slot> map, ref int count, int capacity);   // CTX-12
    internal sealed class Slot { internal Slot(TValue value); internal TValue Value { get; } }
}
```

`Set`'s replace path re-reads the current slot and retries until either `TryAdd` or `TryUpdate` succeeds, so the counter
moves exactly once per entry added and once per entry removed. `Drain` walks the live enumerator, removing and decrementing
until the count is at or under the capacity, and re-enters the walk if concurrent inserts are still ahead of it.

### The public surface (`PublicAPI.Unshipped.txt`, core only)

The plan takes the exact lines from the analyzer's code fix. In outline: `Dexpace.Sdk.Core.Execution.CallKey` (the three
properties, both `Next` overloads, `ToString`, and the synthesized record-struct members); `InstrumentationContext` (the
members above); `CallContext` (`Key`, `Instrumentation`, `Close`, `ToString`, the synthesized record members such as
`Equals`, `GetHashCode`, `EqualityContract`, `PrintMembers`, `<Clone>$`, the copy constructor); the three sealed records
(constructors, properties, promotions); `DexpaceCallContexts.TryGet`. `Dexpace.Sdk.Http.SystemNet`'s and
`Dexpace.Sdk.Serialization.SystemTextJson`'s API files do not change in 4a.

---

## Migration plan from the as-built code

4a is **additive**. As built, there is no promotion chain, no call key and no store; correlation is `Activity.Current`
plus the mutable `PipelineContext` with its string-keyed property bag (§5.4 **As built**).

- **`PipelineContext`, `HttpPipeline`, `PipelineRunner`, `InstrumentationPolicy`: untouched by 4a.** 4c reworks
  `PipelineContext` into the call-scoped carrier and wires the chain; 5c reworks the instrumentation policy onto the bundle.
- **`DexpaceDiagnostics.ActivitySource`**: read by `InstrumentationContext.StartActivity`; not changed.
- **`BannedSymbols.txt`**: one new entry, `T:System.Threading.AsyncLocal`1` (P4a-11); the `TryRemove(KeyValuePair<,>)`
  entry's reason text names `BoundedMap`'s slot (P4a-12). The weak-reference and weak-table entries are unchanged.
- **No `Security` class is added, edited or touched.** S6's (`PIPE-16`, the re-drive restore) and S8's (`HTTP-52`'s
  error-body close scope, `EnsureSuccessErrorMappingTests`) `Security` tests are in code 4a does not edit; they stay green
  because 4a changes no pipeline, policy, response or error-mapping path. 4a's last step runs the `Security` filter
  (`--filter-trait "Category=Security"`) and records the result.

**Breaking changes: none.** No public member changes signature or behaviour. The `AsyncLocal` ban breaks no build: no file
under `src/` declares one (the only mention is a doc comment in `HttpClientExtensions.cs`).

---

## Landing order

Each step is one pull request carrying its code **and** its tests (the one-PR allowance 2a, 2b and 3a used), with its
`PublicAPI.Unshipped.txt` diff and `CHANGELOG.md` line where it has one.

| PR | Content | Rows | Gate | Notes |
|---|---|---|---|---|
| **1** | `BoundedMap` and its `Slot`; the `AsyncLocal` ban; the ban-message edit; `BoundedMapTests` | `CTX-8` (map half), `CTX-9` (map half), `CTX-11`, `CTX-12`, `CTX-13`, `CTX-19` (map half) | none | Internal only; no public API. 6c's `AUTH-19` store and token cache build on it |
| **2** | `CallKey`, `InstrumentationContext`; their tests | `CTX-4`, `CTX-6`, `CTX-14`, `CTX-15`, `CTX-20` | none | Independent of PR 1. Public API lines |
| **3** | `CallContext` and the three records; `ContextStore`; `DexpaceCallContexts`; promotion, registration and close; their tests | `CTX-1`–`CTX-3`, `CTX-5`, `CTX-7`, `CTX-8`–`CTX-10`, `CTX-16`–`CTX-18` | PRs 1, 2 | Public API lines. **4c's wiring waits for this PR** |
| **4** | `ExecutionContextArchitectureTests` (immutability, closed hierarchy, the exchange has no promotion), `NoAmbientStateArchitectureTests` (no `AsyncLocal<>` field in any `src/` assembly), the `CTX-19` collection test, the concurrency tests; the AOT smoke extension (mint, promote twice, `TryGet`, close, `TryGet` absent) | `CTX-7`, `CTX-19` (closing) | PR 3 | |
| **5** | Close-out: the user page `docs/sdk-documentation/execution-context.md`; the 4a checklist; the dated design corrections; the 2b checklist's `SEAM-28` correction; `CLAUDE.md`'s layout tree (the `Execution/` folder) and the "genuinely unbuilt" paragraph; `CHANGELOG.md` `[Unreleased]`; the roadmap status note; the `Security` filter run | all 20 (closing) | 1–4 | The docs close the sub-phase (roadmap step 7) |

---

## Tests, vectors and ports

- **Categories.** `Unit` throughout `tests/Dexpace.Sdk.Core.Tests` (core and fakes only; `SEAM-2`'s partition holds — no
  transport). `AotSmoke` for the smoke extension. **No `Security` class is added or edited.**
- **Isolation.** Every store test constructs its own `ContextStore` or `BoundedMap` through the internal constructors and
  binds contexts through `DispatchContext`'s internal overload, so no test depends on `ContextStore.Shared`'s contents or
  on another test's leaks (which the shared store's arbitrary eviction, `CTX-13`, would make flaky). The one test that
  reads `DexpaceCallContexts.TryGet` mints its own keys and asserts only on them.
- **Listeners.** Tests that need a listener (`CTX-14`, `CTX-20`) use `TestSupport`'s existing `ActivityRecorder` and its
  collection, as the instrumentation-policy tests do, because an `ActivityListener` is process-wide.
- **Concurrency tests** start their workers behind a `Barrier`, use bounded thread counts (16–32), and assert on exact
  final state after joining, never on timing.
- **No vector file.** The context model has no wire format; nothing here is upstreamable as data.
- **Ported, each with a header comment citing path and sha:** from `nodejs-sdk@c0ff3fd` `packages/core/src/context/`:
  `context.test.ts` (the promotion-chain, one-way, off-chain construction, pinned-key, operation-name and immutability
  cases; **not** the rendering-of-`Symbol` cases, which are a JavaScript fact), `store.test.ts` (install / install-if-absent,
  identical-bundle keys, one slot per chain, no auto-registration, close, lookup, bounded drain, cap below one; **not** the
  `clear` and singleton cases, since 4a ships neither a `Clear` nor a public singleton), `instrumentation.test.ts` (the
  sentinel, invalid/not-remote, no-op span and tracer-factory cases, mapped onto `None`). From
  `ruby-sdk@90075b1`'s 4a design (documents only): its R2 `CTX-9` trap (value-equal, reference-distinct), its R4
  discriminating measurement (the maximum size observed and the drain iterations per call, not the aggregate count), and its
  `CTX-19` collection test. The plan records that Ruby's gem tests are not in the local clone, as earlier phases did.
- **Not ported:** Ruby's custom cop (the `RS0030` entries already do its job), any test of a host-language fact (constraint 10:
  e.g. that a `WeakReference` loses its target after a collection).

---

## Coupling with 4c and later phases

### The interface 4a hands to 4c

4c consumes, by name: `DispatchContext`, `RequestContext`, `ExchangeContext`, `CallContext`, `CallKey`,
`InstrumentationContext`, `ContextStore` (through the promotions, not directly) and `BoundedMap` (if 4c needs a bounded map
of its own). 4a proposes, for 4c to confirm or contest in its design:

1. **One `DispatchContext` per call**, built at pipeline entry and held by the call-scoped `PipelineContext`;
   `PipelineContext` exposes the key and the bundle (read-mostly), so policies correlate without seeing the store.
2. **`PromoteToRequest` per transmission** (each attempt or hop), passing the request actually sent and the operation name
   (from `OperationDescriptor.OperationId` where the call came through a descriptor, P4a-15). Promoting the same dispatch
   twice is legal: the later promotion takes the slot and closing the earlier link is a no-op (`CTX-10`).
3. **`PromoteToExchange` on the response**; on success the `Response` carries its `ExchangeContext` and disposing the
   response closes it (§5.4: "Disposing the `Response` closes its `ExchangeContext`"), which is an internal hook on
   `Response` that 4c adds; on failure the pipeline closes the furthest link before the exception propagates.
4. **The pipeline never calls the store** (P4a-6): promotion registers.
5. **The bundle before 5c**: `InstrumentationContext.FromActivity(Activity.Current)` at dispatch, or `None`; 5c replaces
   this with the operation span.
6. **The `CallKey` route to the transport**, if 4c takes P4a-13's option 1: `RequestOptions.CallKey`, set by the pipeline.

### Later phases

- **5c**: populates the bundle and owns `OBS-25`/`OBS-26` (and `OBS-27` with §10 entry 24), citing 4a's tests for the
  shape; repoints `InstrumentationPolicy` onto `InstrumentationContext.StartActivity`; may not redefine the bundle
  (Position A).
- **5a**: may make `ContextStore.DefaultCapacity` configurable (the value is internal until then).
- **6c**: builds the `AUTH-19` nonce counter and moves `AccessTokenCache` onto `BoundedMap` (§6.3), adding an
  atomic per-key increment if the nonce store needs one; owns the one sanctioned `SuppressFlow` background-launch helper.
- **8b**: stamps the `CallKey` into `HttpRequestMessage.Options` under a public `HttpRequestOptionsKey<CallKey>` declared in
  `Dexpace.Sdk.Http.SystemNet` (P4a-13), with a wire test that a `DelegatingHandler` resolves the live `RequestContext`
  (the exchange stage does not exist yet while the handler runs).
- **10**: `XCUT-14`'s audit finds `BoundedMap` as the one implementation; `XCUT-11` is met by the store's lock-free design.

---

## Design corrections owed at close-out

Dated corrections, in PR 5, each naming the ruling that caused it:

- **§5.4**: the bundle is `InstrumentationContext` (P4a-3); promotion registers and the context binds its store (P4a-6);
  the slot lives in `BoundedMap` (P4a-12); the cap is 10 000 (P4a-10); the `CallKey` has no public constructor (P4a-2);
  the promotion names (P4a-4); the `CallKey`'s route to the transport is open and lands with 4c/8b (P4a-13);
  `default(ActivityContext).TraceState` is `null`, not empty (fact 3), so `InstrumentationContext` normalises it to `""` for
  `CTX-15`/`OBS-26` (§5.4 states "state empty" as verified); the **As built** line.
- **§8.1**: "The correlation bundle is `Activity`" names `InstrumentationContext` as the composing type (P4a-3); and
  "CTX-15's shared untraced sentinel is `default(ActivityContext)`" gains the same `TraceState` null-to-`""` note (fact 3).
- **§9.1**: the banned list gains `AsyncLocal<T>` (P4a-11), and the `TryRemove` entry names `BoundedMap`'s slot (P4a-12).
- **§11 item 30**: the reader's route (P4a-13), and that it lands with 8b.
- **§12**: the `CTX` row's notes cite this design.
- **2b checklist**, `SEAM-28` row: a dated correction re-pointing "⏳ 4a" (P4a-15).
- **Roadmap**: the phase 4 row's `sdk-design refs` cell gains this design's link (appended), and a dated status note.

No new §10 entry: no ruling leaves a `CTX` MUST's letter unmet, and the bundle's span-model residuals are already §10 entries
23 and 24.

---

## Risks and open questions

- **Leaked contexts pin responses until evicted.** A caller who never disposes a `Response` leaves its `ExchangeContext` in
  the store until the cap evicts it; with an undisposed body that pins a connection. That is `CTX-11`'s stated purpose, not a
  new risk; the user page says so.
- **Eviction of a live context** under a burst above 10 000 concurrent calls makes that call's `TryGet` return absent. No
  correctness path in core reads the store, so the only consequence is a `DelegatingHandler` that cannot resolve the call
  (8b documents it). The cap value is open for the lead (P4a-10).
- **4c may contest P4a-6** (promotion registers). If it does, the fallback is Node's: promotions become pure, 4c installs,
  and `CTX-3`/`CTX-17`'s tests move to 4c. Either way the rows stay 4a's.
- **The store has no production reader until 8b** (P4a-13). The roadmap and §11 item 30 kept it for that reader; until it
  lands the store is correct but unread.
- **`TraceIdFormat` reports `Hierarchical` for a legacy-format activity**, whose `TraceId` is zero, so `IsValid` is false.
  Correct, and stated in the remarks.

---

## Rulings

Taken by this design in the absence of a human reviewer. Each lists the options considered. Six are **open for the lead**:
P4a-3, P4a-6, P4a-8, P4a-10, P4a-13 and P4a-15.

1. **P4a-1 — Placement.** Namespace `Dexpace.Sdk.Core.Execution`, folder `src/Dexpace.Sdk.Core/Execution/`; `BoundedMap`
   in `Dexpace.Sdk.Core.Internal`. Options: `Dexpace.Sdk.Core.Pipeline` (rejected: the roadmap keeps 4a and 4c separable,
   and the contexts exist outside a pipeline); `…Core.Context` (rejected: a namespace named like the types' common suffix
   reads badly, `Context.RequestContext`). The test namespace `…Tests.Execution` shadows no type (the 2b lesson).
2. **P4a-2 — `CallKey` is mint-only.** `readonly record struct` with a private constructor and `Next()` / `Next(in
   ActivityContext)`; `default` is the never-minted sentinel and is rejected by every context constructor. `CTX-5`'s pin is
   "mint once, pass twice". Options: a public positional constructor (rejected: forgeable keys that collide with minted
   sequences, and no requirement needs one); a `Guid` (rejected: §5.4's struct keeps the trace and span for debugging and
   allocates nothing).
3. **P4a-3 — The bundle is `InstrumentationContext`, a sealed class composing `ActivityContext`, the active `Activity?`
   and the factory over `DexpaceDiagnostics.ActivitySource`, with `None` as the shared default. Open for the lead.**
   Options and reasons in [Position A](#a-the-bundle-is-one-sealed-class-not-a-bare-activitycontext). Binds 5c (coupling
   obligation 1) and corrects §8.1's wording.
4. **P4a-4 — Promotion names.** `PromoteToRequest` and `PromoteToExchange`, naming the target stage so the one-way chain
   reads at the call site (Ruby's reason). Option: an overloaded `Promote` (rejected: the argument type alone would carry
   the direction).
5. **P4a-5 — A closed hierarchy of records.** An abstract record base with a `private protected` constructor and an
   `internal abstract` member; three sealed records. Options: one record with a stage enum (rejected by §5.4); unrelated
   records with no base (rejected: the store and `TryGet` need one type); a public open base (rejected: `CTX-1` names three
   flavours).
6. **P4a-6 — Promotion registers; each context binds its store. Open for the lead.** Public constructors bind
   `ContextStore.Shared`; an internal overload binds an isolated store for tests. Constrains 4c (the pipeline never calls
   the store). Options in [Position B](#b-promotion-registers-and-the-context-knows-its-store).
7. **P4a-7 — A context is not `IDisposable`; `Close()` has no latch.** The identity check makes a second close a no-op
   (`CTX-18`). `IDisposable` is rejected because `using var dispatch = …` would close the head — a `CTX-10` no-op — and
   leave the terminal link registered (Ruby's P4-4), and because a context does not own the response it carries.
8. **P4a-8 — The public surface** is the four records, `CallKey`, `InstrumentationContext` and
   `DexpaceCallContexts.TryGet`; the store, its `Set`/`Add`/`Release` and the map are internal. **Open for the lead**,
   because `NFR-4` locks every name at the first release. [Position D](#d-the-public-surface).
9. **P4a-9 — `CTX-8`'s loser gets `ArgumentException`** with the key's rendering in the message and `ParamName`
   `context`, the BCL's `Dictionary.Add` behaviour. Options: a new public `ContextConflictException : SdkException`
   (rejected: a duplicate registration is a programming error on an internal affordance, not a call failure, and a new
   public exception type for an internal method is surface for nothing); `InvalidOperationException` (rejected: the
   argument is what is wrong).
10. **P4a-10 — The context store's capacity is 10 000. Open for the lead.** No `CTX` row gives a number. Options: 1 024
    (`AUTH-19`'s number, Ruby's choice; rejected for the context store because a busy server can have more than 1 024
    calls in flight, and evicting a live context makes its `TryGet` fail); 10 000 (Node's; chosen: above realistic
    in-flight concurrency, and still a hard bound); unbounded (forbidden). `BoundedMap` itself has no default capacity —
    each consumer names its own, so `AUTH-19`'s 1 024 and this 10 000 do not make one implementation two-valued. Internal
    until 5a decides on configuration.
11. **P4a-11 — "No SDK `AsyncLocal`" is mechanised.** `T:System.Threading.AsyncLocal`1` joins `BannedSymbols.txt`
    (reason: design §5.4 P13), and `NoAmbientStateArchitectureTests` checks every `src/` assembly by reflection for a field
    of type `AsyncLocal<>`. `Activity.Current` stays the one ambient value (it is the runtime's). Option: rely on review
    (rejected: §5.4's two verified failure modes are invisible in review).
12. **P4a-12 — The slot lives in `BoundedMap`**, with one scoped `RS0030` pragma for the slot-reference `TryRemove`, and
    the ban's reason text names `BoundedMap`'s slot. A reflection test pins that the slot declares no equality override.
    [Position C](#c-the-identity-remove-lives-in-boundedmap-behind-its-own-slot).
13. **P4a-13 — The `CallKey`'s route to a `DelegatingHandler` is deferred: 4a ships neither the option key nor the stamp,
    and recommends `RequestOptions.CallKey` (set by 4c's pipeline, stamped by 8b under a key declared in
    `Dexpace.Sdk.Http.SystemNet`). Open for the lead**: it is a SEAM-surface change §5.4 does not state, and the store has
    no production reader until it lands. [Position E](#e-the-route-of-the-callkey-to-a-delegatinghandler).
14. **P4a-14 — `ToString` never prints the request or response graph.** `CallContext.ToString` is sealed and renders the
    flavour, the key and the operation name, plus `Request.ToString()` (method and redacted URL, 2a) and the response's
    status code. Contexts get logged; a record's synthesized `ToString` would print every public property, which is a
    redaction hazard (`OBS-11`) and an accidental body read.
15. **P4a-15 — `SEAM-28`'s "⏳ 4a" is met by the carrier and re-pointed for the wiring. Open for the lead.** 4a supplies
    `OperationName` and its validation (the same rule as `OperationId`); the step that passes `OperationDescriptor.OperationId`
    into `PromoteToRequest` belongs to whoever runs a descriptor through the pipeline, which is 4c. The 2b checklist row gets
    a dated correction: "✅ (carrier, 4a's `PromotionChainTests`); ⏳ 4c (wiring)". Needs the lead because it moves a ⏳
    between two sub-phases brainstormed in parallel, and 4c's plan must pick it up.
16. **P4a-16 — The tracer factory parents explicitly.** `StartActivity` passes the bundle's `ActivityContext` as the parent
    instead of relying on `Activity.Current`, so a span started by a policy belongs to its call even when the ambient value
    has drifted (a background continuation, P13's second failure mode). `None` returns `null` regardless of listeners.

---

## Deviation Ledger

| ID | Decision | Touches | Kind | Argued in | Route |
|---|---|---|---|---|---|
| P4a-3 | The bundle is one sealed class composing `ActivityContext`, the active `Activity?` and the source-backed factory | `CTX-2`, `CTX-14`, `CTX-15`, `CTX-20`; shape for `OBS-25`/`OBS-26` | design-level: §8.1 names three things, §5.4 names `ActivityContext` | [Position A](#a-the-bundle-is-one-sealed-class-not-a-bare-activitycontext) | Design §5.4 and §8.1, dated corrections (PR 5) |
| P4a-6 | Promotion registers into the store the context is bound to | `CTX-3`, `CTX-8`, `CTX-17` | design-level: §5.4 says "registration happens at promotion" without saying who calls the store | [Position B](#b-promotion-registers-and-the-context-knows-its-store) | Design §5.4, dated correction (PR 5) |
| P4a-10 | The context store's capacity is 10 000 | `CTX-11` | a substituted constant (no `CTX` row gives one) | Ruling 10 | Design §5.4, dated correction (PR 5) |
| P4a-12 | The identity remove's slot lives in `BoundedMap`, not a context-specific `ContextSlot` | `CTX-9`, `XCUT-14` | mechanism: §5.4's slot, generalised | [Position C](#c-the-identity-remove-lives-in-boundedmap-behind-its-own-slot) | Design §5.4 and §9.1, dated corrections (PR 5) |
| P4a-13 | The `CallKey`'s route to the transport is deferred to 4c/8b | §11 item 30 (no requirement ID) | design gap: §5.4 names the reader, not the route | [Position E](#e-the-route-of-the-callkey-to-a-delegatinghandler) | Design §5.4 and §11 item 30, dated corrections (PR 5) |

No ruling leaves a `CTX` MUST's letter unmet, so no §10 entry is opened. The `PIPE-32`/`REDIR-25` reversal under §10
`async-redirect-pillar` is 4c's, and 4a does not touch it.

**Correction 2026-10-07 (phase 5c, P5c-2, P5c-3).** "5c replaces `FromActivity(Activity.Current)` at dispatch with the operation span" is built as
stated: `HttpPipeline.SendCoreAsync` opens the operation span at call entry, before the `DispatchContext`, and builds the bundle from it
(`InstrumentationContext.FromActivity(operationSpan)`), or `InstrumentationContext.None` when the source has no listener, even under an ambient
activity. No member of the bundle was added, renamed or retyped. See the
[5c design](../../phase5/phase5c/2026-10-07-phase5c-tracing-design.md), positions A and B.
