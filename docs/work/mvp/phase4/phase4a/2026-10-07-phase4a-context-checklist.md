# Phase 4a — Execution Context: Checklist

Written 2026-10-07 from what was built, against the [design](2026-10-05-phase4a-context-design.md) and
[plan](2026-10-05-phase4a-context.md). Legend: ✅ built and tested.

| ID | Level | Exit | Evidence |
|---|---|---|---|
| `CTX-1` | MUST | ✅ | `PromotionChainTests.Each_stage_exposes_exactly_its_artifacts`; `ExecutionContextArchitectureTests` (three flavours, no exchange promotion, internal abstract member) |
| `CTX-2` | MUST | ✅ | `PromotionChainTests.Promoting_dispatch_adds_...`, `Promoting_request_to_exchange_...`, `The_source_is_unchanged_by_promotion` |
| `CTX-3` | MUST | ✅ | `ContextRegistrationTests.Every_flavour_of_one_chain_occupies_the_same_slot_in_turn` |
| `CTX-4` | MUST | ✅ | `CallKeyTests.Two_keys_minted_from_identical_trace_and_span_differ`, `ToString_renders_...`; `ContextRegistrationTests.Two_contexts_with_identical_trace_and_span_both_register` |
| `CTX-5` | MUST | ✅ | `PromotionChainTests` (unequal by default, equal when pinned, default key rejected) |
| `CTX-6` | MUST | ✅ | `CallKeyTests.Keys_minted_concurrently_...`; `PromotionChainTests.Default_keys_are_distinct_across_all_three_flavours` |
| `CTX-7` | MUST | ✅ | `ExecutionContextArchitectureTests.Every_instance_field_is_readonly`; `ContextStoreTests.Distinct_keys_register_...` |
| `CTX-8` | MUST | ✅ | `ContextStoreTests` (Set, Add, concurrent Add); `BoundedMapTests` (Set, TryAdd) |
| `CTX-9` | MUST | ✅ | `ContextStoreTests.A_value_equal_stale_context_...`; `BoundedMapTests.TryRemoveIfSame_compares_by_reference_not_value`, `The_slot_type_declares_no_equality_override` |
| `CTX-10` | MUST | ✅ | `ContextRegistrationTests.Closing_an_intermediate_link_...`, `Closing_the_terminal_link_...` |
| `CTX-11` | MUST | ✅ | `BoundedMapTests` (burst, capacity below one, tracked count); `ContextRetentionTests.A_burst_above_the_capacity_...` |
| `CTX-12` | SHOULD | ✅ | `BoundedMapTests.One_drain_call_removes_every_excess_entry`, `Concurrent_inserts_from_16_threads_...` |
| `CTX-13` | MAY | ✅ | `BoundedMapTests.A_capacity_one_map_holds_exactly_one_of_two_inserts` |
| `CTX-14` | MUST | ✅ | `InstrumentationContextTests` (FromActivity, FromContext remote, tracer factory child) |
| `CTX-15` | MUST | ✅ | `InstrumentationContextTests.None_*`, `FromActivity_null_and_FromContext_default_...`; `CallKeyTests.Keys_minted_under_None_are_distinct` |
| `CTX-16` | SHOULD | ✅ | `PromotionChainTests` (operation name absent at dispatch, carried forward, blank rejected) |
| `CTX-17` | MUST | ✅ | `ContextRegistrationTests` (constructed not registered, off-chain, first promotion installs) |
| `CTX-18` | MUST | ✅ | `ContextStoreTests.TryGet_of_an_unknown_key_...`; `ContextRegistrationTests.A_double_close_is_a_no_op`; `DexpaceCallContextsTests` |
| `CTX-19` | MUST | ✅ | `ContextRetentionTests.Registered_contexts_survive_...`; `NoAmbientStateArchitectureTests` (three assemblies); RS0030 probes below |
| `CTX-20` | SHOULD | ✅ | `InstrumentationContextTests.The_tracer_factory_is_safe_under_concurrent_invocation`, `With_no_listener_the_factory_returns_null` |

20 rows, all built. `OBS-25`/`OBS-26` (5c), `REDIR-*` (6b), `SEAM-28` (2b, re-pointed to 4c for the wiring) and `XCUT-14`
(phase 10) are not 4a rows.

## Evidence

- **Existing assertions changed:** none. **`Security` classes:** unedited (no file under either `Security/` folder changed);
  the `Security` filter passes on both test projects (254 and 30 tests).
- **RS0030 probes (task 1.2):** a throwaway file using `AsyncLocal<int>`, `WeakReference<object>`,
  `ConditionalWeakTable<,>` and a stray `ConcurrentDictionary.TryRemove(KeyValuePair)` produced `RS0030` for all four; the
  probe was deleted.
- **Zero-id probe (R6):** `CallKey.ToString` guards the zero ids with explicit 32 and 16 zero strings, so it does not depend
  on `ToHexString()` of a default id.
- **Closed-hierarchy probe (task 3.2):** not run as a separate throwaway project; the closure is pinned by
  `ExecutionContextArchitectureTests.The_hierarchy_is_closed_by_an_internal_abstract_member`.
- **Deviation ledger as built:** P4a-3, P4a-6, P4a-10, P4a-12, P4a-13 built as the design states. P4a-3, P4a-6, P4a-8,
  P4a-10, P4a-13 and P4a-15 remain open for the lead; the lead has not yet accepted the `SEAM-28` move to 4c.
- **Build and test:** `dotnet build` (warnings as errors), `dotnet format --verify-no-changes` and the full test run pass;
  the AOT smoke prints "aot-smoke: all checks passed".
- **Dated correction, 2026-10-07 (phase 5a, P5a-24):** the hand-off "5a may make `ContextStore.DefaultCapacity` configurable" is closed as declined. The store is process-wide, so a per-client option cannot size it coherently, and a process-wide setter is the global slot `CFG-13` declines; `CTX-11` to `CTX-13` require a bound, not a knob.
