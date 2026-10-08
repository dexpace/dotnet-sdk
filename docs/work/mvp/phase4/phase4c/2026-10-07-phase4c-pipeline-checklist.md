# Phase 4c — Pipeline Rework: Checklist

The execution-time checklist for sub-phase 4c of roadmap phase 4
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `phase-4c-pipeline`, following the
[design](2026-10-05-phase4c-pipeline-design.md) and the [plan](2026-10-05-phase4c-pipeline.md).

The scope is 40 rows: `PIPE-1`-`PIPE-40` (36 MUST, 4 SHOULD). Every test below is `[Trait("Category", "Unit")]` and lives in
`tests/Dexpace.Sdk.Core.Tests/Pipeline/` (namespace `…Tests.Pipeline`) unless another place is named; phase 4c adds no `Security` class. **Red
evidence:** for a new type or a changed signature the red was the compile error (plan convention 1). The plan's six pull requests were implemented in
one pass on a branch with 4a and 4b merged, and the full suite was run green; the behavioural reds were not each captured separately, and the pins
(`PIPE-20`, `PIPE-21`) were not shown able to fail.

The census: 4a 20 + 4b 34 + 4c 40 = 94.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `StageOrderTests`, `PipelineStageFactsTests`, `PillarRuleTests`, `NonPillarOrderTests`, `SurgicalEditTests`, `BulkTests`, `PolicyContractTests`,
`ForkTests`, `AsyncErrorModelTests`, `SyncPathTests`, `ConcurrencyTests`, `ReDriveLifecycleTests`, `EmptyPipelineTests`, `OptionsFlowTests`,
`PipelineAsTransportTests`, `TerminalMappingTests`, `FlattenNestTests`, `PipelineBridgeTests`, `ContextChainWiringTests`, `SeedOriginTests`,
`ResiliencePresetTests`, `RunnerTests`, `PipelineContextTests`, `HttpPipelineTests`, `DexpacePipelineTests`; `Pipeline/Policies/{ErrorMappingPolicyTests,…}.cs`;
`Internal/BlockingWaitTests.cs`; `Errors/ErrorMappingFactoryTests.cs`; `Http/Response/ResponseEnsureSuccessSyncTests.cs`; the AOT smoke check is
`CheckPipelineReworkAsync` in `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`. Doubles are in `tests/Dexpace.Sdk.TestSupport/{Pipeline,Transports}/`.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `PIPE-1` | MUST | ✅ | `StageOrderTests.One_probe_per_stage_runs_in_stage_order_whatever_the_insertion_order` (nine probes, shuffled under a pinned seed; the exit log is the exact reverse) |
| `PIPE-2` | MUST | ✅ by stated reading (P4c-6) | `StageOrderTests.A_per_call_probe_runs_once_and_sees_the_final_response_while_an_auth_probe_runs_per_hop`, `A_per_hop_probe_runs_once_while_a_per_attempt_probe_runs_per_attempt`, `PerCall_runs_outside_redirect_and_PerHop_inside_it`. `Operation` is a sixth singleton outside the spec's pre-redirect slot |
| `PIPE-3` | SHOULD | ✅ by stated reading (P4c-7) | `StageOrderTests.Stage_values_are_strictly_increasing_and_sparse`. Met in part: no user slot after `Auth`, `Diagnostics` or `Serde` |
| `PIPE-4` | MUST | ✅ | `PillarRuleTests.Each_pillar_admits_one_policy` (six pillars), `A_custom_policy_may_occupy_the_serde_pillar`; `PipelineStageFactsTests.Pillars_are_exactly_the_six` |
| `PIPE-5` | MUST | ✅ | `PillarRuleTests.A_second_distinct_policy_on_a_pillar_fails_at_add_naming_both_types` (every insertion path; the message names the stage, both types and `Replace`), `Replace_swaps_a_pillar_occupant_without_a_collision` |
| `PIPE-6` | MUST | ✅ | `PillarRuleTests.Re_adding_the_same_instance_is_idempotent`, `Two_value_equal_policies_are_distinct` (asserts `Equals` and not `ReferenceEquals`, then the second `Add` throws) |
| `PIPE-7` | MUST | ✅ | `NonPillarOrderTests.Append_adds_to_the_tail_and_prepend_to_the_head`, `Order_within_a_stage_survives_an_edit_in_another_stage`, `Re_adding_a_non_pillar_instance_appends_it_again` |
| `PIPE-8` | MUST | ✅ | `PillarRuleTests.A_policy_reporting_an_undefined_stage_is_rejected` (every insertion path; `ParamName` `policy`); `PipelineStageFactsTests.IsDefinedStage_rejects_values_outside_the_enum` |
| `PIPE-9` | MUST | ✅ | `EmptyPipelineTests.An_empty_pipeline_passes_request_options_and_token_to_the_transport` (sync and async, `Assert.Same`) |
| `PIPE-10` | MUST | ✅ | `ConcurrencyTests.Concurrent_calls_share_no_per_call_state` (64 parallel sends); `HttpPipelineTests.HttpPipeline_has_no_settable_member` |
| `PIPE-11` | MUST | ✅ | `ConcurrencyTests.Every_shipped_policy_holds_only_readonly_instance_fields`, `The_default_pipeline_holds_only_readonly_instance_fields_in_its_policies`; `ErrorMappingPolicyTests.The_policy_is_a_PerCall_stage_and_the_chain_is_static_readonly` |
| `PIPE-12` | MUST | ✅ | `PolicyContractTests.A_short_circuit_returns_its_synthetic_response_and_reaches_nothing_downstream` (sync and async), `A_policy_may_substitute_the_response_on_the_way_out` |
| `PIPE-13` | MUST | ✅ | `RunnerTests.Next_dispatches_to_the_transport_after_the_last_policy_with_the_callers_options` (replaces the 2b `The_transport_receives_RequestOptions_Empty`), `PipelineRunner_has_no_mutable_field` |
| `PIPE-14` | MUST | ✅ | `PolicyContractTests.A_substituted_request_reaches_every_downstream_policy_and_the_transport` |
| `PIPE-15` | MUST | ✅ (P4c-12) | `ForkTests.Driving_the_same_runner_twice_re_runs_the_whole_downstream_tail`; `RunnerTests.Reentrancy_PolicyCallingNextTwice_TransportInvokedTwice` |
| `PIPE-16` | MUST | ✅ (S6, now structural) | `ForkTests.Each_fork_carries_the_request_its_forker_passed`, `Forks_share_call_scoped_state_and_not_per_drive_state`; `PipelineContextTests.The_context_offers_no_way_to_write_the_request_upward`; `ReDriveRequestIsolationTests` (`Security`, moved mechanically, plus the same structural fact) |
| `PIPE-17` | MUST | ✅ | `OptionsFlowTests.The_same_request_options_instance_reaches_every_policy_every_fork_and_the_transport` (`Assert.Same` at probes either side of a fork and at the transport). *Dated correction, 2026-10-07 (phase 5a):* the "options immutable" half is now structural: `OptionsImmutabilityTests` (in `Configuration/`), `OptionsRecordArchitectureTests`, and `HttpPipelineTests.A_per_call_DexpaceClientOptions_overrides_the_captured_options_for_that_call_only`; the per-call overloads are kept (P5a-6), closing the 5a half of `P4c-15` |
| `PIPE-18` | MUST | ✅ | `SurgicalEditTests.Insert_relative_to_an_anchor_in_another_stage_is_rejected` (both directions, builder unchanged after the throw), `Insert_next_to_the_first_instance_of_T_in_the_same_stage` |
| `PIPE-19` | MUST | ✅ | `SurgicalEditTests.A_cross_stage_replace_is_rejected`, `Replace_swaps_only_the_first_instance` |
| `PIPE-20` | MUST | ✅ (pin) | `SurgicalEditTests.Remove_deletes_every_instance_and_preserves_order`, `Remove_of_an_absent_type_is_a_no_op` |
| `PIPE-21` | MUST | ✅ (pin) | `SurgicalEditTests.A_missing_anchor_throws_InvalidOperationException_naming_T` (`InsertBefore`, `InsertAfter`, `Replace`) |
| `PIPE-22` | MUST | ✅ | `SurgicalEditTests.Edited_order_equals_order_built_from_scratch`; `PillarRuleTests.A_policy_whose_stage_flips_cannot_pass_validation_in_one_slot_and_run_in_another` |
| `PIPE-23` | MUST | ✅ | `BulkTests.A_colliding_batch_leaves_the_builder_unchanged` (both methods; a collision with the builder and one inside the batch) |
| `PIPE-24` | MUST | ✅ | `ResiliencePresetTests.The_preset_installs_nothing_when_any_target_pillar_is_occupied` (four pillars), `The_preset_fills_empty_pillars_and_leaves_non_pillar_stages_alone`, `The_preset_checks_all_four_before_installing_any` |
| `PIPE-25` | MUST | ✅ | `HttpPipelineTests.Policies_is_an_ordered_read_only_view` |
| `PIPE-26` | MUST | ✅ | `PipelineAsTransportTests.A_pipeline_stands_in_for_a_transport_and_threads_options_through`, `A_pipeline_is_both_seams_and_the_seam_entry_points_thread_options_and_token`; `Architecture/SeamImplementationArchitectureTests` (public exemption and allow-list entry, reason "PIPE-26"). The paginator case is `Pagination/PageableTests`, which builds `Pageable.Create` over an `HttpPipeline` |
| `PIPE-27` | MUST | ✅ | `PipelineAsTransportTests.Disposing_the_pipeline_twice_never_disposes_the_transport` (both forms, both orders; a send after dispose still succeeds) |
| `PIPE-28` | MUST | ✅; ⏳ 8b (the real synchronous terminal) | `SyncPathTests.Sync_and_async_sends_visit_the_same_policies_in_the_same_order`, `The_sync_send_never_calls_an_async_member_of_a_shipped_policy` (every shipped policy over `SyncFirstTransport`), `Run_reaches_the_transports_Execute_when_it_implements_IHttpClient`, `Run_uses_AsBlocking_for_an_async_only_transport`, `The_default_Process_bridges_to_ProcessAsync`; `RetryPolicyTests.Sync_send_retries_through_the_sync_chain`; `BlockingWaitTests`. Residuals: `SystemNetHttpClient.Execute` blocks on the async send until 8b; `BearerTokenAuthPolicy`'s sync credential is the documented bridge until 6c |
| `PIPE-29` | MUST | ✅ | `AsyncErrorModelTests.A_shipped_policy_reports_a_failure_as_a_faulted_task` (eleven shipped policies) |
| `PIPE-30` | MUST | ✅ (§10 entry 12 residual) | `AsyncErrorModelTests.A_synchronous_throw_from_a_policy_becomes_a_faulted_task`, `An_out_of_memory_exception_is_not_caught_by_any_sdk_frame` |
| `PIPE-31` | MUST | ✅ | `TerminalMappingTests` (success disposes after the handler; a throwing handler surfaces as itself and the body is disposed; a throwing dispose is suppressed onto it; a transport failure surfaces unwrapped; each sync and async) |
| `PIPE-32` | MUST | ✅ (documentation); 🚫 (`async-redirect-pillar`, §10 entry 14) | `ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect`; the dated correction in §10 entry 14 |
| `PIPE-33` | MUST | ✅ (§10 entry 8 for the interrupt clause) | 2b `SyncToAsyncBridgeTests`; `PipelineBridgeTests.A_sync_pipeline_bridged_to_async_runs_as_one_unit_on_the_given_scheduler` |
| `PIPE-34` | MUST | ✅ (§10 entry 8) | 2b `AsyncToSyncBridgeTests`; `PipelineBridgeTests.An_async_pipeline_bridged_to_sync_preserves_options_and_surfaces_the_original_exception` |
| `PIPE-35` | SHOULD | ✅ | `FlattenNestTests.A_probe_added_after_flatten_runs_inside_the_inner_loops_and_after_nest_runs_once`, `Flatten_copies_policies_transport_and_client_options`, `Nest_starts_empty_with_the_pipeline_as_its_transport`, `Build_without_a_seed_throws_InvalidOperationException`, `Adding_a_second_pillar_policy_after_flatten_follows_PIPE_5` |
| `PIPE-36` | SHOULD | ✅ | `PillarRuleTests.Every_shipped_pillar_policy_locks_its_stage`, `Authorization_policy_entry_points_are_sealed` |
| `PIPE-37` | MUST | ✅ | `Pipeline/Policies/ErrorMappingPolicyTests.It_runs_once_outside_the_redirect_loop_and_sees_the_final_response`, `A_non_error_status_is_returned_untouched` (11 statuses; the body is neither opened nor disposed) |
| `PIPE-38` | MUST | ✅ | `BulkTests.AddRange_keeps_batch_order_and_PrependRange_reverses_it` |
| `PIPE-39` | SHOULD | ✅ | `ResiliencePresetTests.CreateEmpty_forwards_straight_to_the_transport`; `DexpacePipelineTests.CreateDefault_installs_the_standard_pillars_and_the_per_call_defaults_in_order`, `CreateDefault_without_an_auth_policy_has_no_auth_stage` |
| `PIPE-40` | MUST | ✅ (pinned, `Disposal.DisposeQuietly(Async)`) | `ReDriveLifecycleTests` (superseded responses disposed before the next drive, the last returned open, for redirect and retry, sync and async; every abandon path returns the in-flight response undisposed; a throwing dispose does not mask the next drive) |

Totals: 40 rows, 36 MUST and 4 SHOULD. `PIPE-2` and `PIPE-3` are ✅ by stated reading; `PIPE-28` is ✅ with a ⏳ 8b clause; `PIPE-32` is ✅ for its
documentation clause and 🚫 for the async no-follow. `PIPE-30`, `PIPE-33` and `PIPE-34` are ✅ with their residual in the existing §10 entries 12 and 8.

## Work on other owners' rows

These carry no exit mark in 4c; the owner's row cites 4c's tests.

| ID (owner) | Work done | Evidence |
|---|---|---|
| `RECOV-15` (4b) | `ErrorMappingPolicy` installs 4b's `ErrorMappingStep` at `PerCall` through the response fold | `ErrorMappingPolicyTests`; 4b's `ErrorMappingStepTests` |
| `RECOV-16` (4b) | The synchronous `Response.EnsureSuccess` uses 4b's `ErrorBodyBuffer` (P4c-17 split taken; the fallback was not needed) | `ResponseEnsureSuccessSyncTests`; 4b's `ErrorBodyBufferTests` |
| `RECOV-32`, `RECOV-33` (4b) | Both policies moved to the new signature and installed in `CreateDefault` | `DexpacePipelineTests.CreateDefault_installs_…` (presence, type and stage only) |
| `BODY-30`, `BODY-31`, `HTTP-52` (3b; ⏳ 4c) | The policy form of the mapping; the empty-body clause. The ⏳ 4c half is closed | `ErrorMappingPolicyTests`; `EnsureSuccessErrorMappingTests` (`Security`, unedited) |
| `RETRY-44` (6a) | Structural: there is no shared in-flight request | `ReDriveRequestIsolationTests`, `ForkTests` |
| `REDIR-11`, `REDIR-24` (6b) | The seed request on the call-scoped context; the `"dexpace.auth.origin"` bag key retired | `SeedOriginTests`; `RedirectCredentialHygieneTests`, `AuthHttpsGuardTests` (`Security`, unedited) |
| `REDIR-25` (6b) | The reversal recorded under §10 `async-redirect-pillar`; the preset serves both paths | `ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect` |
| `XCUT-8` (phase 10) | `ErrorMapping.ToException`, the guarded factory; 4b's step and both `EnsureSuccess` forms route through it | `Errors/ErrorMappingFactoryTests.A_non_error_status_is_rejected` |
| `SEAM-2` (2b) | `HttpPipeline` public exemption and allow-list entry | `Architecture/SeamImplementationArchitectureTests` |
| `SEAM-28` (2b; 4a attaches) | ⏳ The dispatch is promoted with a null operation name: neither 4a nor `RequestOptions` defines a carrier for the operation id (4a's P4a-13 option 1 was not taken), so there is nothing to thread yet. The wiring itself is built and tested | `ContextChainWiringTests` |

## Existing assertions changed

| Test | Change | Why |
|---|---|---|
| `PipelineBuilderTests.Build_TwoPoliciesInPillarStage_Throws` | Removed; the rule moved to `Add` time and is `PillarRuleTests.A_second_distinct_policy_on_a_pillar_fails_at_add_naming_both_types` | `PIPE-5`, Breaking 1 |
| `PipelineBuilderTests` stubs, `PipelineRunnerTests` (renamed `RunnerTests`), `PipelineContextTests`, `HttpPipelineTests`, `DexpacePipelineTests`'s `MarkingPolicy` | Moved to the new signature and the `TestContexts` factory; `The_transport_receives_RequestOptions_Empty` replaced by `Next_dispatches_to_the_transport_after_the_last_policy_with_the_callers_options` (the 2b hand-off) | Breaking 4, 5 |
| `InstrumentationPolicyTests` | The two tests that used `(PipelineStage)650` and `(PipelineStage)500` use `Serde` and `Auth` (an undefined stage is now rejected, `PIPE-8`); `ProcessAsync_RestoresPreviousActivity_AfterCompletion` is replaced by `The_downstream_sees_the_activity_and_the_upstream_does_not` (there is nothing to restore) | `PIPE-8`, `PIPE-16` |
| `BasicAuthPolicyTests`, `ApiKeyAuthPolicyTests`, `BearerTokenAuthPolicyTests` | The cross-origin and same-origin re-run cases drive the policy through `TestContexts` against a seed request, with the same assertions on the header the transport receives | Breaking 4, 8 |
| `IdempotencyPolicyTests.DoubleCallPolicy`, `RetryPolicyTests.CapturingAttemptPolicy` | New signature | Breaking 4 |
| `SeamImplementationArchitectureTests` | The public exemption and the allow-list entry for `HttpPipeline` | `PIPE-26`, fact 11 |
| `ReDriveRequestIsolationTests` (`Security`) | Exactly: the probe's `PipelineStage.PerCall` becomes `PipelineStage.PerHop` in `A_redirect_hop_is_not_built_from_the_previous_hops_stamped_request` (fact 8); `ProbePolicy` and `MarkingPolicy` move to the new signature; one added fact, `The_context_offers_no_way_to_write_the_request_upward`. The `MarkingPolicy` summary no longer cites the removed `PipelineContext.Request` | P4c-22 |

## Unedited `Security` classes

`EnsureSuccessErrorMappingTests`, `RedirectCredentialHygieneTests`, `AuthHttpsGuardTests`, `RetryPacingOverflowTests`, `HeaderInjectionValidationTests`,
`MediaTypeTryParseTests`, `UrlRedactionDefaultDenyTests`, and SystemNet's `RedirectWireTests`. Evidence:
`git diff --stat main -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` lists only `ReDriveRequestIsolationTests.cs`,
and `git diff -U0 main -- tests/Dexpace.Sdk.Core.Tests/Security` shows exactly the hunks in the row above. Both `Security` categories pass.

## Deviation ledger as built

| ID | As built |
|---|---|
| P4c-6 | `Operation` is a sixth singleton stage outside `PIPE-2`'s pre-redirect slot; a §11 item records it |
| P4c-7 | No user slot after `Auth`, `Diagnostics` or `Serde`; a §11 item records it |
| P4c-12 | The fork primitive is the immutable `PipelineRunner` value; no single-use latch (`PipelineRunner_has_no_mutable_field`) |
| P4c-13 | The terminal prefers the transport's own `Execute` and falls back to `AsBlocking()`, decided once at `Build`; `BlockingWait` for the retry sleep; `AuthorizationPolicy.GetCredential` with the documented bridge until 6c |
| P4c-18 | "A response with no body" is the empty replayable body (`ResponseBody.IsEmptyReplayable`, internal), which the policy maps without a drain |
| P4c-20 | Both standard pipelines follow redirects; §10 entry 14 carries the topic `async-redirect-pillar` |

## Plan departures worth knowing

- The policy parameter is named `continuation`, not `next` (the plan's name): `CA1716` rejects `next` on a virtual member. The `PipelineContext` factory
  is `PipelineContext.Create(seed, options, requestOptions, dispatch, cancellationToken)` (the token last, `CA1068`); `TestContexts.For` does not take a token.
- 4b already shipped `SyncPath.GetResult`; 4c added `SyncPath.GetCompletedResult(task, owner)` to the same internal class instead of a second helper, so the
  `ValueTask<T>.Result` ban keeps one sanctioned site.
- `HttpPipeline.SendAsync<T>` / `Send<T>` let a dispose failure on the success path surface (an ordinary `await`), and suppress it onto the handler's
  exception on the failure path.
- `RecordingTransport`, `ScriptedTransport` and `RecordingSyncTransport` stay as they were or gain `IHttpClient` (the first two); `RecordingSyncTransport` is still
  sync-only, so the sync-path tests use `SyncFirstTransport` and an adapter.
- **Provenance of the ported cases.** The test cases follow the plan's case lists, which cite `nodejs-sdk@c0ff3fd` `packages/core/src/pipeline/` and the Ruby 4c
  design's testing strategy; the sibling sources were not re-read while the tests were written, so each header says "case list from the plan", not "ported".
  Ruby's `pipeline_test.rb`, `test/dexpace/pipeline/` and `docs/sdk-documentation/pipelines.md` are absent locally, as in 2a to 3b.

## Correction 2026-10-08 (phase 6b)

`PIPE-40` is now also evidenced for the rewritten redirect policy: `RedirectPolicyTests` (the superseded response disposed before the next send; the two refusals and a throwing
predicate dispose first, a failing dispose riding the exception's suppressed trail; every one of the five stop reasons returns the response open) and the split
`ReDriveLifecycleTests`. The "non-replayable body" abandon path in `PIPE-40`'s list is read as retry's; redirect's is governed by `REDIR-6` and `REDIR-22`(b), which throw after
disposing (design §11 item 59, P6b-17). The row above stands as written at 4c's exit.
