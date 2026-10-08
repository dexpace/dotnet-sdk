# Phase 6a — Retry: Checklist

The execution-time checklist for sub-phase 6a of roadmap phase 6
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `72-phase-6a-retry` (issue #72), following the
[design](2026-10-08-phase6a-retry-design.md) and the [plan](2026-10-08-phase6a-retry.md). User page:
[`retry.md`](../../../../sdk-documentation/retry.md).

The scope is 45 rows, `RETRY-1`-`RETRY-45` (39 MUST, 1 MUST NOT, 3 SHOULD, 2 MAY), all ✅. 6a also did the work for eighteen rows that stay in the
4b and 3b checklists, listed in the [carried-rows table](#carried-rows); each of those checklists gets one dated correction (task 6.4). Every test is
`[Trait("Category", "Unit")]` and lives in `tests/Dexpace.Sdk.Core.Tests/` unless another place is named; the loopback case is `Integration`. Phase 6a adds and edits **no**
`Security` class (P6a-31).

**Adaptation to the as-built code.** The six planned PRs landed as commits on the one branch, in the plan's order (classifier, options and backoff and pacing, engine and
policy, timeouts, recovery stack, close-out). The deviations from the plan are listed [below](#deviations-from-the-plan).

**Red evidence.** Tests were written beside the production change in each commit, and for most new types the first compile of the test project was red (a missing type
or member). The pins that were **not** shown able to fail by temporary mutation (plan task 1.4 asked for it): `RetryClassifierTests.IsRetryable_is_computed_once_in_the_constructor`
and `The_overrides_are_sealed` were not mutated; they were not run red.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `Resilience/{RetryClassifierTests,RetryResendGateTests,RetryFactsTests,RetryBackoffTests,RetryPacingTests,RetryEngineTests,RetryBudgetTests}.cs` (+ `EngineHarness.cs`);
`Configuration/RetryOptionsTests.cs`; `Pipeline/Policies/{RetryPolicyEngineTests,RetryPolicyTests,RetryTraceEventsTests,OperationPolicyTests}.cs`; `Recovery/{RetryRecoveryTests,RetryBudgetEquivalenceTests}.cs`;
`Architecture/RecoveryLayerArchitectureTests.cs`; `tests/Dexpace.Sdk.Http.SystemNet.Tests/AttemptTimeoutWireTests.cs` (`Integration`); the AOT smoke check is `CheckPhase6aRetryAsync` in
`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`. Vectors: `tests/vectors/retry/{backoff,pacing}.json`.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `RETRY-1` | MUST | ✅ | `RetryClassifierTests.Every_status_100_to_599_matches_the_classifier`, `The_default_configured_set_is_a_subset_of_the_classifier`; `RetryFactsTests.IsRetryableStatus_is_exactly_408_429_and_5xx_except_501_and_505` (Unit) |
| `RETRY-2` | MUST | ✅ (P6a-8: the capability widens, never vetoes) | `RetryClassifierTests.A_raw_IO_family_exception_is_retryable`, `The_cause_chain_is_walked_to_depth_64_and_is_cycle_safe`, `A_capability_that_is_false_does_not_veto_an_IO_cause`; `RetryPolicyEngineTests.A_service_request_exception_is_retried_a_raw_IOException_is_retried_and_an_InvalidOperationException_is_not` (Unit) |
| `RETRY-3` | MUST | ✅ | `RetryClassifierTests.Every_status_100_to_599_matches_the_classifier`, `IsRetryable_is_computed_once_in_the_constructor` (Unit) |
| `RETRY-4` | MUST | ✅ | `RetryClassifierTests.A_service_request_or_response_exception_is_always_retryable`, `The_overrides_are_sealed` (Unit) |
| `RETRY-5` | MUST | ✅ | `RetryResendGateTests.IsResendable_matrix` (Unit) |
| `RETRY-6` | MUST | ✅ | `RetryResendGateTests.IdempotentMethods_is_exactly_GET_HEAD_OPTIONS_PUT_DELETE`; `RetryFactsTests` (Unit) |
| `RETRY-7` | MUST | ✅ (P6a-11: `RetryNonIdempotentWhenReplayable` removed) | `RetryResendGateTests.A_bare_POST_is_not_resendable_but_a_POST_with_a_replayable_body_is`; `RetryOptionsTests.RetryNonIdempotentWhenReplayable_no_longer_exists`; `RetryPolicyTests.ProcessAsync_Post_ReplayableBody_503_IsRetried`, `ProcessAsync_Post_NoBody_503_IsSentOnce` (Unit) |
| `RETRY-8` | MUST | ✅ | `RetryEngineTests.The_condition_and_the_resend_gate_must_both_hold`; `RetryPolicyEngineTests.ShouldRetry_cannot_override_the_resend_gate` (Unit) |
| `RETRY-9` | MUST | ✅ | `RetryBackoffTests.Matches_every_vector` (`tests/vectors/retry/backoff.json`) (Unit) |
| `RETRY-10` | MUST | ✅ (P6a-14: tick resolution) | `RetryBackoffTests.Jitter_is_symmetric_over_d_minus_w_over_2_to_d_plus_w_over_2`, `Zero_jitter_returns_the_unjittered_delay_and_a_sub_tick_width_returns_the_base`, `A_negative_sample_floors_to_zero_and_a_hostile_random_never_throws`, `Every_sample_in_the_unit_interval_stays_inside_the_symmetric_window` (Unit) |
| `RETRY-11` | MUST | ✅ | `RetryBackoffTests.Saturates_and_never_overflows`, `Attempt_below_one_is_a_programmer_error` (Unit) |
| `RETRY-12` | SHOULD | ✅ | `RetryOptionsTests.Defaults_are_200ms_2_8s_0_2_and_two_retries`; `DexpaceClientOptionsTests.RetryOptions_Defaults_AreCorrect` (Unit) |
| `RETRY-13` | MUST | ✅ (P6a-3: one engine under both stacks) | `RetryBudgetEquivalenceTests.At_the_defaults_both_stacks_make_three_sends_wait_the_same_delays_and_surface_the_same_failure_type` (both stacks hold a `RetryEngine`); the single `RetryBackoff`, `RetryPacing`, `RetryEngine` in `Resilience/`; `RecoveryLayerArchitectureTests.No_resilience_type_references_a_pipeline_type` (Unit) |
| `RETRY-14` | MUST | ✅ | `RetryEngineTests.Sends_at_most_maxRetries_plus_one_times`; `RetryBudgetEquivalenceTests` (three sends, the same delays, both stacks) (Unit) |
| `RETRY-15` | MUST | ✅ | `RetryPacingTests.Matches_every_vector` (`pacing.json`), `Retry_After_delta_seconds_integer_and_fractional`, `Retry_after_ms_and_x_ms_retry_after_ms_are_digits_only_milliseconds`, `X_RateLimit_Reset_is_an_epoch_with_positive_jitter`; `RetryPolicyEngineTests.Each_pacing_header_is_honoured_when_the_switch_is_on` (Unit) |
| `RETRY-16` | MUST | ✅ (P6a-18) | `RetryPacingTests.No_header_or_only_malformed_headers_yield_null`, `The_strict_grammar_rejects_what_the_BCL_would_accept` (Unit) |
| `RETRY-17` | MUST | ✅ | `RetryPacingTests.Retry_After_http_date_uses_HttpDate_and_a_past_date_is_zero` (zero, not `null`) (Unit) |
| `RETRY-18` | MUST | ✅ | `RetryPacingTests.No_result_exceeds_365_days_in_ticks`, `A_thirty_digit_numeral_saturates_to_365_days`; `RetryBackoffTests.Every_result_is_clamped_to_365_days`; `RetryPacingOverflowTests` (S7, unedited) (Unit, Security) |
| `RETRY-19` | MUST | ✅ | `RetryPacingTests.The_strict_grammar_rejects_what_the_BCL_would_accept`, `Resilience_contains_no_double_Parse` (Unit) |
| `RETRY-20` | MUST | ✅ | `RetryPacingTests.The_hint_is_not_given_symmetric_jitter`; `RetryEngineTests.Delay_precedence_is_override_then_pacing_then_fixed_then_backoff` (Unit) |
| `RETRY-21` | MUST | ✅ (P6a-17: `HonorRetryAfter` is the stage stack's on/off switch) | `RetryPacingTests.Matches_every_vector` (precedence rows); `RetryPolicyEngineTests.HonorRetryAfter_false_ignores_all_four_headers` (Unit) |
| `RETRY-22` | MUST | ✅ | `RetryPacingTests.The_parser_is_total`; `RetryEngineTests.A_throwing_pacing_read_degrades_to_no_hint_and_the_upstream_failure_is_still_the_one_surfaced` (Unit) |
| `RETRY-23` | MUST | ✅ | `RetryClassifierTests.A_cancelled_call_token_is_never_retryable_whatever_the_exception`; `RetryEngineTests.Cancellation_before_a_send_throws_with_the_trail_and_does_not_send`, `Cancellation_during_the_wait_surfaces_OperationCanceledException_with_the_token_still_signalled`; `RetryPolicyTests.ProcessAsync_Cancellation_Propagates_NotSwallowed` (Unit) |
| `RETRY-24` | MUST | ✅ | `RetryClassifierTests.A_TaskCanceledException_over_a_TimeoutException_with_an_unsignalled_token_is_retryable`, `A_TaskCanceledException_with_no_TimeoutException_is_not`; `RetryPolicyEngineTests.An_attempt_timeout_surfaces_a_retried_ServiceRequestTimeoutException` (Unit) |
| `RETRY-25` | MUST | ✅ | `RetryClassifierTests.A_fatal_exception_is_never_classified`; `RetryEngineTests.A_fatal_exception_passes_every_frame_untouched`; `RetryPolicyEngineTests.ShouldRetry_throwing_aborts_with_InvalidOperationException_and_suppressed_failure` (fatal case) (Unit) |
| `RETRY-26` | MUST | ✅ | `RetryEngineTests.Both_waits_use_TimeProviderWaits` (async chunks 49 d + 11 d, sync `Sleep`), `Cancellation_during_the_wait_surfaces_OperationCanceledException_with_the_token_still_signalled` (Unit) |
| `RETRY-27` | MUST | ✅ | `RetryBudgetTests` (all five); `RetryRecoveryTests.The_total_timeout_aborts_on_elapsed_and_on_elapsed_plus_delay_and_clamps_the_delay`, `A_pacing_hint_replaces_the_schedule_and_is_clamped_by_the_budget`; `RetryEngineTests.The_budget_aborts_an_overshooting_retry_and_surfaces_the_last_failure_with_the_trail` (Unit) |
| `RETRY-28` | MUST | ✅ | `RetryPolicyEngineTests.RetryPolicy_carries_no_budget` (reflection); `RetryPolicy` hands the engine `RetryBudget.Unbounded` (Unit) |
| `RETRY-29` | MAY | ✅ (P6a-20: the `ShouldRetry` hook, with a documented recipe) | `RetryPolicyEngineTests.ShouldRetry_true_false_null_and_the_cap_still_applies` (the `X-Should-Retry` recipe, ten rows), `ShouldRetry_cannot_override_the_resend_gate` (Unit) |
| `RETRY-30` | MUST | ✅ | `RetryEngineTests.Ten_thousand_retries_with_zero_delay_keep_constant_stack_depth` (Unit) |
| `RETRY-31` | MUST | ✅ | `RetryEngineTests.A_zero_delay_continues_inline_without_arming_a_timer`; `RetryPolicyEngineTests.No_AttemptTimeout_arms_no_extra_timer` (Unit) |
| `RETRY-32` | MUST | ✅ | `RetryEngineTests.A_success_that_arrives_after_the_token_fired_is_disposed_and_the_call_throws`, `A_token_cancelled_before_the_first_send_sends_nothing` (Unit) |
| `RETRY-33` | MUST | ✅ | `RetryEngineTests.A_throwing_release_a_throwing_hook_and_a_throwing_wait_each_fault_the_task_with_the_response_disposed` (Unit) |
| `RETRY-34` | MUST | ✅ | `RetryEngineTests.The_trail_holds_every_prior_failure_oldest_first_and_never_the_surfaced_instance`; `RetryPolicyEngineTests.A_discarded_503_body_is_drained_into_an_HttpResponseException_trail_entry`; `RetryRecoveryTests.An_exhausted_503_surfaces_the_HttpResponseException_with_the_trail` (Unit) |
| `RETRY-35` | MUST | ✅ | `RetryEngineTests.A_discarded_error_response_is_drained_into_an_HttpResponseException_trail_entry`, `A_forced_non_error_status_leaves_no_trail_entry`, `A_drain_failure_becomes_the_entry_and_the_loop_continues`; `ReDriveLifecycleTests.A_retry_disposes_the_superseded_response_before_the_next_drive_and_returns_the_last_open`, `A_throwing_dispose_of_a_retried_response_reaches_the_pipelines_logger` (Unit) |
| `RETRY-36` | MUST | ✅ | `RetryRecoveryTests.A_retryable_status_is_buffered_through_ErrorBodyBuffer_once` (1 MiB cap) (Unit) |
| `RETRY-37` | MUST | ✅ | `RetryClassifierTests.An_HttpResponseException_decides_by_the_configured_set_alone`; `RetryRecoveryTests.The_configured_set_decides_a_501`; `RetryPolicyEngineTests.A_wrapped_HttpResponseException_is_decided_by_the_configured_status_set` (Unit) |
| `RETRY-38` | SHOULD | ✅ | `RetryPolicyEngineTests.The_attempt_header_stamps_a_one_based_ordinal_on_a_per_attempt_copy`, `A_disabled_attempt_header_adds_no_header`; `RetryEngineTests.A_stamped_attempt_header_carries_the_one_based_send_number`, `A_disabled_attempt_header_sends_the_same_request_instance` (Unit) |
| `RETRY-39` | MUST | ✅ | `RetryEngineTests.Delay_precedence_is_override_then_pacing_then_fixed_then_backoff`; `RetryPolicyEngineTests.GetDelayOverride_wins_over_everything` (Unit) |
| `RETRY-40` | SHOULD | ✅ | `RetryPolicyEngineTests.ShouldRetry_throwing_aborts_with_InvalidOperationException_and_suppressed_failure`, `A_throwing_delay_override_logs_event_140_once_and_falls_back`, `A_logger_that_throws_while_reporting_a_failed_override_does_not_mask_the_call`; `RetryEngineTests.A_throwing_or_negative_override_is_logged_and_falls_back` (Unit) |
| `RETRY-41` | MUST | ✅ (clamp clause vacuous: P6a-13) | `RetryOptionsTests.MaxRetryAttempts_rejects_a_negative_and_accepts_zero`; `RetryPolicyEngineTests.MaxRetries_on_RequestOptions_wins_and_zero_means_no_retries`; `RetryRecoveryTests.MaxRetries_zero_on_RequestOptions_disables_retries_for_the_call`; `RetryEngineTests.Zero_retries_sends_once_and_is_never_exhausted` (Unit) |
| `RETRY-42` | MUST | ✅ | `RetryEngineTests.Concurrent_calls_through_one_engine_do_not_share_state`; `RetryPolicyEngineTests.The_policy_is_stateless_across_calls_and_concurrency` (Unit) |
| `RETRY-43` | MAY | ✅ (P6a-16) | `RetryBackoffTests.Matches_every_vector` (fixed-delay rows); `RetryPolicyEngineTests.FixedDelay_replaces_the_backoff_and_still_sits_below_the_pacing_hint` (Unit) |
| `RETRY-44` | MUST | ✅ | `ReDriveRequestIsolationTests` (S6, unedited); `RetryPolicyEngineTests.Each_attempt_drives_a_fresh_ForAttempt_copy` (Unit, Security) |
| `RETRY-45` | MUST NOT | ✅ | `RetryEngineTests.The_TimeProvider_is_never_disposed` (Unit) |

Count: **45 rows**, all ✅ (0 ⏳, 0 🚫, 0 N/A).

## Carried rows

These rows stay in the 4b and 3b checklists; 6a's evidence is here, and each of those checklists carries one dated correction pointing at this table.

| ID | Mark | Evidence |
|---|---|---|
| `RECOV-16` (re-sent clause) | ✅ | `RetryRecoveryTests.A_retryable_status_is_buffered_through_ErrorBodyBuffer_once`; `RetryEngineTests.A_discarded_error_response_is_drained_into_an_HttpResponseException_trail_entry` |
| `RECOV-17` | ✅ | `RetryClassifierTests.An_HttpResponseException_decides_by_the_configured_set_alone`; `RetryRecoveryTests.A_non_retryable_error_status_passes_as_a_Success` |
| `RECOV-18` | ✅ | `RetryResendGateTests.IsResendable_matrix` |
| `RECOV-19` | ✅ (P6a-6: every send, not only re-sends) | `RetryRecoveryTests.A_503_503_200_reaches_the_200`, `With_ErrorMappingStep_installed_nothing_is_mapped_twice` |
| `RECOV-20` | ✅ | `RetryRecoveryTests.An_exhausted_503_surfaces_the_HttpResponseException_with_the_trail`, `The_total_timeout_aborts_on_elapsed_and_on_elapsed_plus_delay_and_clamps_the_delay` |
| `RECOV-21` | ✅ | `RetryBackoffTests.Matches_every_vector`; `RetryRecoveryTests.A_pacing_hint_replaces_the_schedule_and_is_clamped_by_the_budget` |
| `RECOV-22` | ✅ | `RetryPacingTests`; `RetryRecoveryTests.A_pacing_hint_replaces_the_schedule_and_is_clamped_by_the_budget` |
| `RECOV-23` | ✅ | `RetryPacingTests.The_parser_is_total`, `Retry_After_http_date_uses_HttpDate_and_a_past_date_is_zero` |
| `RECOV-24` | ✅ | `RetryPacingTests.Matches_every_vector` (precedence and grammar rows) |
| `RECOV-25` | ✅ (P6a-19) | `RetryPacingTests.X_RateLimit_Reset_is_an_epoch_with_positive_jitter` |
| `RECOV-26` (engine clause) | ✅ | `RetryBackoffTests.Every_result_is_clamped_to_365_days`; `RetryPacingOverflowTests` (S7, unedited) |
| `RECOV-27` | ✅ | `RetryRecoveryTests.A_cancelled_wait_yields_Failure_OperationCanceledException_and_the_dispatcher_rethrows_it_signalled` |
| `RECOV-28` | ✅ | `RetryRecoveryTests.The_dispatcher_and_RetryRecovery_hold_no_per_call_state`; `RetryEngineTests.Concurrent_calls_through_one_engine_do_not_share_state` |
| `RECOV-29` | ✅ | `RetryEngineTests.A_throwing_pacing_read_degrades_to_no_hint_and_the_upstream_failure_is_still_the_one_surfaced` |
| `RECOV-30` | ✅ | `RetryBudgetEquivalenceTests` |
| `RECOV-31` | ✅ (P6a-26: both stacks stamp from 1) | `RetryPolicyEngineTests.The_attempt_header_stamps_a_one_based_ordinal_on_a_per_attempt_copy`; `RetryRecoveryTests.The_attempt_header_stamps_from_one_on_every_send` |
| `RECOV-34` | ✅ | `RetryOptionsTests` (validation, copy of the status set, equality) |
| `BODY-5`; `BODY-4` (retry third) | ✅ | `RetryResendGateTests.IsResendable_matrix` (no body, bytes, empty bytes, string, form, file, a seekable stream with a declared length, a single-use stream, and multipart with replayable and with single-use parts, each with a hard-coded expectation; `FromValue` is not in the matrix); `RetryRecoveryTests.The_resend_gate_decides_the_send_count_for_every_method_body_and_failure_kind` (send counts across five methods, seven bodies and two failure kinds) |

### Work on other owners' rows (no row in this checklist)

| ID (owner) | What 6a supplies | Evidence |
|---|---|---|
| `XCUT-1`, `XCUT-2`, `XCUT-3` (phase 10) | `OverallTimeout` throws `OperationTimeoutException`; `AttemptTimeout` is a retried `ServiceRequestTimeoutException`; the wait is promptly cancellable | `OperationPolicyTests.A_deadline_surfaces_OperationTimeoutException_not_cancellation`, `A_caller_cancellation_still_surfaces_OperationCanceledException`, `The_inner_trail_is_copied_onto_the_timeout_exception`; `RetryPolicyEngineTests.An_attempt_timeout_*`; `AttemptTimeoutWireTests`; `DexpaceClientOptionsTests.OverallTimeout_and_AttemptTimeout_*` |
| `XCUT-4` to `XCUT-7`, `XCUT-9`, `XCUT-10` (phase 10) | the capability, the baked flag, the configured set, the cycle-safe walk, the uniform gate | `RetryClassifierTests`, `RetryResendGateTests` |
| `HTTP-9`, `HTTP-35` (2a) | the moved idempotent set; `MaxRetries = 0` | `RetryFactsTests`, `RetryResendGateTests.Method_IsIdempotent_reads_the_moved_set`, the `MaxRetries_*` tests |
| `CFG-35` (5a) | both classifier halves wired | `RetryClassifierTests.IsRetryableCause_still_passes_its_5a_cases_and_accepts_a_custom_IRetryableError`, `RetryFactsTests` |
| `OBS-28`, `OBS-29` (5c) | the three calls kept; the final exhausted predicate | `RetryTraceEventsTests` (incl. `The_exhausted_event_follows_the_final_predicate`), `RetryEngineTests.The_exhausted_callback_follows_the_final_predicate` |
| `PIPE-16`, `PIPE-40` (4c) | a fresh drive per attempt; the returned response is not disposed | `RetryPolicyEngineTests.Each_attempt_drives_a_fresh_ForAttempt_copy`, `A_503_after_the_cap_is_returned_live_and_unread`, `ReDriveRequestIsolationTests` |
| `TRANSPORT-2` (8b) | no change: one retry layer per call path | [`retry.md`](../../../../sdk-documentation/retry.md) |

## Existing assertions changed (plan reading R9)

None is a `Security` test. Each was re-derived in the commit that broke it.

| Test | Change | Why |
|---|---|---|
| `DexpaceClientOptionsTests.RetryOptions_Defaults_AreCorrect`, `ToString_of_the_nested_records_renders_every_member` | defaults 2 / 8 s plus the new members; member list without `RetryNonIdempotentWhenReplayable` | Breaking 1, 2 |
| `OptionsImmutabilityTests.With_derives_a_copy_and_leaves_the_source_unchanged` | `3` becomes `2` | Breaking 1 |
| `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (`CheckPhase5aConfigurationAsync`) | `3` becomes `2` | Breaking 1 |
| `RetryPolicyTests` (`MakeOptions`, the three tests named for the removed option) | the parameter removed; the "enabled" test renamed `ProcessAsync_Post_ReplayableBody_503_IsRetried`; the "disabled" pair became `ProcessAsync_Post_NoBody_503_IsSentOnce` | Breaking 2 |
| `RetryPolicyTests.ProcessAsync_OverflowGuard_AllRecordedDelaysAreNonNegativeAndBoundedByMaxDelay`, `ProcessAsync_DelayBound_LaterAttemptsArePinnedToMaxDelay` | the bound is `MaxDelay × (1 + Jitter / 2)` | design E: jitter follows the cap |
| `RetryPolicyTests.ProcessAsync_Cancellation_Propagates_NotSwallowed` | the token is cancelled mid-send, not before the call | a token cancelled before the first send now sends nothing (RETRY-32) |
| `RetryTraceEventsTests.A_non_idempotent_request_is_not_exhausted` | renamed `A_request_that_cannot_be_resent_is_not_exhausted`, using a bare POST | a POST with a replayable body is re-sent now |
| `ReDriveLifecycleTests.A_retry_disposes_the_superseded_response_before_the_next_drive_and_returns_the_last_open`, `The_sync_paths_dispose_the_same_way` | the recorded sequence gains `retried:open` | P6a-21: a discarded error response is drained before it is disposed |
| `LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` | 140 is the only id in 140-149; 141-169 stay reserved | P6a-28 |
| `OperationPolicyTests.ProcessAsync_WithShortTimeout_ThrowsWhenTransportHangs`, `Process_sync_with_short_timeout_throws_when_the_transport_hangs` | expect `OperationTimeoutException` | Breaking 8 |
| `OperationPolicyTests.ProcessAsync_WithZeroTimeout_CompletesNormally`, `ProcessAsync_WithNegativeTimeout_CompletesNormally` | replaced by `A_zero_or_negative_timeout_is_rejected_where_it_is_set` | Breaking 8 |
| `ActivityScopeTests.Sdk_writes_to_a_non_recording_span_are_skipped` | filters the started spans by a test-owned parent's trace id | the global non-recording listener saw spans of pipelines running in parallel tests and failed intermittently; the assertions are unchanged |

Plan items that needed **no** edit: `LogCatalogueTests` (its retried-503 case and the `DisposeSuppressedId` count stay at one, because the drain logs a dispose failure through the call's logger),
`RetryTraceEventsTests`' "returns and throws exactly what it did", `ReDriveLifecycleTests.A_throwing_dispose_of_a_retried_response_reaches_the_pipelines_logger` and
`The_sync_paths_report_a_throwing_dispose_to_the_pipelines_logger`.

## `Security` classes (convention 5)

No `Security` class was edited or added. `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` is empty, and
`RetryPacingOverflowTests` (S7) and `ReDriveRequestIsolationTests` (S6) were run on their own after the options change, the engine and at the end; all `Category=Security` tests pass in both suites.

## Vector provenance

| File | Source | Not ported, and why |
|---|---|---|
| `tests/vectors/retry/backoff.json` | `nodejs-sdk@54aeed4 packages/core/src/retry/backoff.test.ts` | Node's fast-check properties became the loop in `RetryBackoffTests.Every_sample_in_the_unit_interval_stays_inside_the_symmetric_window` and the hostile-random theory |
| `tests/vectors/retry/pacing.json` | `nodejs-sdk@54aeed4 packages/core/src/retry/pacing.test.ts`, with `ruby-sdk@5b17395` `policy_test.rb` pacing cases and Java's `RetryAfterParser` cases | cases asserting a host fact of Node or Ruby (`Number()` reading an exponent, `Float()` reading underscores), roadmap constraint 10; Node's padded-value rejection differs (RFC 9110 trims SP and HTAB), noted in the vector |

Ruby's `test/support/retry_fixtures.rb` named by the card does not exist in the local tree (design "Siblings"); the ports are from the test files.

## To-verify facts (plan task 0.1), run on 10.0.401

| Fact | Outcome |
|---|---|
| 6 (the `long` half) | `long.TryParse(NumberStyles.None, Invariant)` rejects `+5`, ` 5`, `1e3`, `5 `; accepts `0005` |
| 8 | On this host (.NET 10, x64) `(long)1e20` and `(long)double.PositiveInfinity` **saturate** to `long.MaxValue` and `(long)double.NaN` is `0`; the design's premise ("not a saturation") is hardware-dependent, so `RetryBackoff` still compares in `double` before any cast |
| 9 | `new CancellationTokenSource(TimeSpan, TimeProvider)` arms its timer through `TimeProvider.CreateTimer`; a due time of `uint.MaxValue` ms throws `ArgumentOutOfRangeException`; `uint.MaxValue - 1` ms and 49 days plus one tick are accepted by the source, so the options' `(0, 49 days]` rule is a deliberate margin |
| 10 | Disposing the token source after `SendAsync(..., ResponseHeadersRead, token)` returned does not cancel the later read of a slowly streamed body; `AttemptTimeoutWireTests` pins the shipped path |
| 19 | not run: nothing in the shipped design depends on it (no public `Bind` pair exists) |
| `RETRY-30` premise | `new StackTrace().FrameCount` is constant (6) across 10 000 awaited iterations |

The knowledge queries (`scripts/knowledge --origin note`, `--section conflicts`, `--prefix-info RETRY`, `--gaps RECOV`) were run on this host and agree with the design's file-based reading.

## Deviations from the plan

1. **`ErrorBodyBuffer` gained an optional `ILogger`** (task 3.1 said to raise it to the lead if the dispose-suppressed log could not be kept without it). The change is internal and additive:
   with a logger the post-drain dispose goes through `Disposal` and a failure is logged as `dexpace.dispose.suppressed`; with none the as-built plain dispose is unchanged. The lead had not ruled; this keeps the OBS contract, so the choice was taken as the plan's stated preference.
2. **`IsRetryableFailure` walks twice.** Design B lists the checks per node, root first, which would let a wrapper's `true` capability decide before the `HttpResponseException` under it; the plan's own test ("a `ServiceResponseException` wrapping a 503 under `{{429}}` is not retryable") requires the response-bearing node to decide first. A first pass finds an `HttpResponseException`; the second is `IsRetryableCause`.
3. **`RetryBudget` exposes `IsSpent`, `Allows` and `Clamp`** instead of one `TryClamp(ref delay)`: the abort rule ("elapsed plus delay exceeds the budget") and the clamp rule ("narrow to the remainder") cannot both be one step, so the clamp is applied at the wait.
4. **The condition hook is not consulted** when `MaxRetries` is zero or the request is not re-sendable: it could not change the outcome, and it keeps `ShouldRetry` side-effect free in those cases.
5. **`RetryAttemptContext.Attempt`** is the number of sends performed so far (the failed send's 1-based ordinal), which is the retry ordinal about to be scheduled.
6. **Plan task 1.4's "prove each pin able to fail"** was not done (see Red evidence).
7. **`ActivityScopeTests` was stabilised** (table above); it is not part of 6a's scope but failed intermittently in the full run before and after the change.
8. **No `Security`-class edit and no `PackageReference` change**, as planned; no lock file changed.

## Deviation ledger as built

The rulings below are the design's, taken as designed because the lead has not ruled on the open ones.

| ID | Decision | State |
|---|---|---|
| P6a-2 | no phase 6 segmentation design | taken as designed (open for the lead) |
| P6a-3 | one engine under both stacks | built |
| P6a-4 | the internal `Resilience` namespace; `RetryFacts` moved | built; architecture fact `No_resilience_type_references_a_pipeline_type` |
| P6a-5 | the recovery stack composed by the dispatcher, not an `IRecoveryStep` | built as designed (open for the lead) |
| P6a-6 | every send classified in the composition | built as designed (open for the lead) |
| P6a-8 | the capability widens and never vetoes | built as designed (open for the lead) |
| P6a-9 | a status set, 400-599, `XCUT-7`'s default | built as designed (open for the lead) |
| P6a-10 | no configurable method set | built as designed (open for the lead) |
| P6a-13 | negative counts rejected; `RETRY-41`'s clamp clause vacuous | built as designed (open for the lead) |
| P6a-14 | tick resolution | built |
| P6a-17 | fixed precedence; `HonorRetryAfter` as the stage switch | built as designed (open for the lead) |
| P6a-18 | large numerals clamp; malformation is no hint | built |
| P6a-20 | unsealed `RetryPolicy` with two protected virtual hooks | built as designed (open for the lead) |
| P6a-23 | `AttemptTimeout` cooperative | built |
| P6a-24 | `OperationTimeoutException`; `(0, 49 days]` validation | built as designed (open for the lead) |
| P6a-26 | both stacks stamp from ordinal 1 | built |
| P6a-27 | the internal random seam | built as designed (open for the lead) |
| P6a-33 | the interfaces assumed of 6b and 6c | unchanged by 6a; to be confirmed when 6b and 6c land |
