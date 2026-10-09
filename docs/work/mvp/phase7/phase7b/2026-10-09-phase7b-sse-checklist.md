# Phase 7b — Server-Sent Events: Checklist

The execution-time checklist for sub-phase 7b of roadmap phase 7
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `80-phase-7b-sse` (issue #80), following the
[design](2026-10-09-phase7b-sse-design.md) and the [plan](2026-10-09-phase7b-sse.md). User page:
[`sse.md`](../../../../sdk-documentation/sse.md).

The scope is 41 rows, `SSE-1`-`SSE-41` (36 MUST, 3 SHOULD, 2 MAY): **40 ✅, 0 🚫, 0 ⏳, 1 N/A (`SSE-41`)**. `SSE-2` is ✅ with its line layer met by 3a and
its event layer built, `SSE-18` is ✅ as a documented contract, and `SSE-19` is ✅ as the sanctioned divergence of design §10 entry 20. 7b also did work for five
rows owned elsewhere, listed in the [cross-owner table](#work-on-other-owners-rows). Every test is `[Trait("Category", "Unit")]` and lives in
`tests/Dexpace.Sdk.Core.Tests/` unless another category or place is named. Phase 7b **adds one `Security` class**
(`ServerSentEventLineCapTests`, [below](#the-security-class)) and edits none. **No breaking change**: every `PublicAPI.Unshipped.txt` line is an addition under
`Dexpace.Sdk.Core.ServerSentEvents`, and the SystemNet and STJ `PublicAPI` files and every package and lock file are untouched.

**Ruby gap (P7b-22).** The roadmap card names `ruby-sdk/gems/dexpace-core/test/dexpace/sse/`, `sse_test.rb` and `test/support/sse_fixtures.rb`. None of them exists at
`ruby-sdk@fa402cd` (`mvp`), `ruby-sdk@90075b1` or on `main` (re-checked at the start of the PR, plan task 0.1). The rows therefore cite the Node port
(`nodejs-sdk@c0ff3fd`, `packages/core/src/sse/`) and chapter 13, and `tests/vectors/sse/grammar.json` ports no Ruby case.

**Adaptation to the as-built code.** The plan's six stages landed as commits on the one branch, in the plan's order: the event value and the exception (stage 1); `RetryField`,
`EventAccumulator` with the vectors, the reader, the `ReadAll` views, the split property and the `Security` regression (stage 2); the facade (stage 3: ownership and the latch,
the asynchronous view, the blocking view, the failure paths, the release table); `SseMapResult` and the typed adapter (stage 4); the architecture tests, the streamed loopback
reply, the wire tests (with a prerequisite commit, D1 below) and the NativeAOT check (stage 5); the user page, `CLAUDE.md` and the README, then this checklist, the changelog, the
dated corrections and the roadmap note (stage 6). The deviations from the plan are listed [below](#deviations-from-the-plan).

**Red evidence.** For each new type the first compile of the test project was red (a missing type or member). Pins proven able to fail by temporary mutation, never committed:
constructing the reader's `Utf8LineReader` with `LineFeedOrCrLf` instead of `Whatwg` turned 38 of the 155 split and line tests red; passing `int.MaxValue` as the line reader's
cap turned 9 of the 10 `ServerSentEventLineCapTests` red; a close that is never recognised (`_closed` compared with 5) turned 6 failure and lifecycle tests red; making the
early-dispose release propagate turned 5 lifecycle and release-table tests red; passing `null` instead of the exception to the mapper-throw release turned 2 typed and
release-table tests red; adding a `"[DONE]"` literal to `EventAccumulator` turned the sentinel scan red. The wire tests were run twelve times in a row without a failure.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files (all under `tests/Dexpace.Sdk.Core.Tests/` unless noted): `ServerSentEvents/{ServerSentEventTests,ServerSentEventLineTooLongExceptionTests,RetryFieldTests,EventAccumulatorTests,ServerSentEventParserTests,
ServerSentEventLineTests,ServerSentEventReaderTests,ServerSentEventChunkSplitTests,ServerSentEventStreamLifecycleTests,ServerSentEventStreamFailureTests,ServerSentEventStreamReleaseTableTests,
SseMapResultTests,ServerSentEventStreamTypedTests}.cs` (helpers in `SseTestSupport.cs` and `SseVectors.cs`); `Security/ServerSentEventLineCapTests.cs`; `Architecture/{Sse37ArchitectureTests,Sse38ArchitectureTests,
ModelImmutabilityArchitectureTests}.cs`; `IO/SplitReadStreamTests.cs`; `tests/Dexpace.Sdk.Http.SystemNet.Tests/{ServerSentEventWireTests,HttpResponseMessageBodyTests}.cs` and
`Loopback/LoopbackServerTests.cs`; the AOT check is `CheckServerSentEventsAsync` in `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.ServerSentEvents.cs`. "Parser" is
`ServerSentEventParserTests.Reader_matches_the_vector_table`, one `[Theory]` over the 105 cases of `tests/vectors/sse/grammar.json` (the Node `parser.test.ts` cases and the chapter's conformance
fixtures), each run through the synchronous and the asynchronous reader; its case names below are the vector's.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `SSE-1` | MUST | ✅ | Parser (7 cases: `blank-line-dispatches-exactly-one-event-each`, `per-event-id-does-not-carry-forward`, `every-accumulator-is-fresh-after-a-dispatch`, `two-blank-lines-dispatch-once`); `EventAccumulatorTests.Accept_returns_null_until_a_blank_line`, `Every_accumulator_is_reset_after_dispatch`, `Every_accumulator_is_reset_after_a_flush` (Unit) |
| `SSE-2` | MUST | ✅ (line layer met by 3a, event layer built) | `Utf8LineReaderTests.Whatwg_mode_terminates_on_CR_immediately_and_skips_the_following_LF` (3a, unchanged); Parser (10 cases: each terminator, mixed, `cr-cr-is-two-terminators`, `cr-lf-lf-is-a-line-end-then-a-blank-line`); `ServerSentEventLineTests.The_same_event_under_each_terminator_and_mixed_terminators_is_identical`, `A_CR_at_a_chunk_end_then_LF_at_the_next_chunk_start_is_one_terminator`, `A_CR_terminated_event_is_delivered_before_the_next_bytes_arrive`; `ServerSentEventChunkSplitTests` (every two-way split of all 105 vectors, every three-way split of those up to 24 bytes, seeds 1 to 25); `ServerSentEventWireTests.A_CR_terminated_event_is_delivered_before_the_server_sends_more` (Integration, a real socket with the server gated on the next chunk) |
| `SSE-3` | MUST | ✅ | Parser (8 cases: `colon-less-data-is-one-empty-data-line`, `trailing-colon-is-an-empty-value`, `colon-less-unknown-field-dispatches-nothing`, `the-first-colon-splits-and-later-colons-are-value`); `EventAccumulatorTests.The_value_starts_after_the_first_colon_and_one_space`, `A_colon_less_line_is_a_name_with_an_empty_value` (Unit) |
| `SSE-4` | MUST | ✅ | `ServerSentEventTests.A_present_but_empty_data_line_is_distinct_from_no_data`, `Data_defaults_to_an_empty_list_and_the_other_four_to_null`; Parser (10 cases: `empty-event-is-present-but-empty-not-absent`, `an-empty-id-alone-dispatches-an-event`, `present-but-empty-data-differs-from-no-data`, `colon-less-event-is-present-but-empty`) (Unit) |
| `SSE-5` | MUST | ✅ | Parser (10 cases: `one-leading-space-is-stripped`, `only-one-of-several-leading-spaces-is-stripped`, `a-tab-is-not-stripped`, `two-leading-spaces-of-a-comment-keep-one`); `EventAccumulatorTests.A_comment_keeps_the_latest_text_with_one_space_stripped` (Unit) |
| `SSE-6` | MUST | ✅ | Parser (10 cases: `a-comment-only-block-dispatches`, `comment-latest-wins`, `an-empty-comment-is-present-but-empty`, `a-comment-after-data-still-counts`); `EventAccumulatorTests.Accept_returns_null_until_a_blank_line` (a comment is a field seen) (Unit) |
| `SSE-7` | MUST | ✅ | Parser (12 cases: `an-unknown-field-sets-no-state-and-causes-no-dispatch`, `field-names-are-case-sensitive`, `field-names-are-not-trimmed`, `a-bom-on-a-later-line-makes-it-an-unknown-field`); `EventAccumulatorTests.Field_names_are_ordinal_and_case_sensitive` (8 names), `An_ignored_field_does_not_mark_the_block_seen` (Unit) |
| `SSE-8` | MUST | ✅ | Parser (5 cases: `data-lines-accumulate-unjoined-in-wire-order`, `empty-data-lines-are-kept-in-order`); `EventAccumulatorTests.Data_lines_accumulate_unjoined_in_order` (Unit) |
| `SSE-9` | MUST | ✅ | Parser (6 cases: `an-id-containing-nul-is-ignored-entirely`, `a-nul-id-does-not-overwrite-a-valid-id`, `a-nul-id-alone-is-not-a-field-seen`); `EventAccumulatorTests.An_ignored_id_and_an_ignored_retry_leave_earlier_valid_values_alone`; `ServerSentEventTests.Id_rejects_a_NUL_and_the_message_never_echoes_the_value` (construction half) (Unit) |
| `SSE-10` | MUST | ✅ | Parser (4 cases: `an-absent-event-is-null-never-message`, `event-is-latest-wins`, `event-is-stored-raw`); `Sse37ArchitectureTests.Sse_types_hold_no_sentinel_literal_and_name_no_default_event_type` (no `"message"` literal anywhere in the namespace) (Unit) |
| `SSE-11` | MUST | ✅ (cap `int.MaxValue` ms, P7b-6) | `RetryFieldTests` (all four: digits, non-digits including Arabic-Indic and fullwidth, 1000 digits without overflow, leading zeros); Parser (20 cases: valid, zero, leading zeros, the cap, one over, twenty digits, signed, fractional, `an-invalid-retry-does-not-overwrite-a-prior-valid-one`, `a-retry-only-block-dispatches`, `an-invalid-retry-alone-dispatches-nothing`); `ServerSentEventTests.Retry_rejects_negative_and_over_cap_values`, `Retry_rejects_sub_millisecond_values`, `Retry_accepts_zero_the_cap_and_null` (Unit) |
| `SSE-12` | MUST | ✅ | `ServerSentEventLineTests.A_leading_BOM_is_removed_once`, `A_partial_BOM_is_content_and_swallows_nothing`, `A_doubled_BOM_keeps_the_second_as_data`, `A_BOM_split_across_reads_is_still_removed`, `A_BOM_on_a_later_line_is_not_removed`, `A_BOM_after_a_non_BOM_first_line_is_data_not_a_second_chance`, `A_BOM_costs_three_bytes_of_the_first_lines_cap`; Parser (13 cases incl. the byte-level `a-bom-from-bytes`, `a-doubled-bom-from-bytes`, `a-partial-bom-is-content-not-a-bom`, `a-bom-inside-a-data-value-survives`); the split property (a BOM cut at any byte) (Unit) |
| `SSE-13` | MUST | ✅ | Parser (12 cases: `an-id-only-block-dispatches`, `an-event-only-block-dispatches`, `a-block-with-no-field-set-is-skipped`, `a-block-of-only-ignored-fields-is-skipped`, `a-retry-only-block-dispatches`, `a-comment-only-block-dispatches`); `EventAccumulatorTests.A_blank_line_with_nothing_seen_returns_null` (Unit) |
| `SSE-14` | MUST | ✅ | Parser (10 cases: `eof-dispatches-a-pending-unterminated-block`, `an-empty-stream-ends-immediately`, `eof-after-a-cr-terminated-line`, `eof-dispatches-an-unterminated-comment`, `eof-with-several-pending-fields-dispatches-one-event`); `ServerSentEventReaderTests.A_pending_block_at_EOF_dispatches_once_then_null`; `EventAccumulatorTests.Flush_dispatches_a_pending_block_once`; the split property (Unit) |
| `SSE-15` | MUST | ✅ | `ServerSentEventReaderTests.ReadNext_returns_null_at_the_end_and_keeps_returning_null` (sync and async, three calls after the end); Parser asserts a further `null` after every case (Unit) |
| `SSE-16` | MUST | ✅ | `ServerSentEventReaderTests.The_reader_holds_exactly_the_reviewed_instance_fields` (`_lines`, `_accumulator`, `_bomChecked`, `_maxLineBytes`; only `_bomChecked` is cross-call event state, plan reading R1), `No_event_state_survives_a_dispatch` (id, retry, event, comment); Parser `per-event-id-does-not-carry-forward` (Unit) |
| `SSE-17` | MUST | ✅ | `ServerSentEventReaderTests.The_source_is_never_disposed_or_closed`, `The_reader_is_not_disposable`, `The_view_does_not_close_the_source` (Unit) |
| `SSE-18` | MUST | ✅ (documented contract) | The `<remarks>` on `ServerSentEventReader` and `ServerSentEventStream` (single-threaded, IO-37; the facade's close is the one sanctioned cross-thread call); `ServerSentEventStreamFailureTests.Close_from_another_thread_while_a_read_is_parked_surfaces_IOException_and_one_release`, `The_blocking_view_normalises_a_close_the_same_way`. No behavioural test can prove an absence of thread-safety |
| `SSE-19` | MAY | ✅ (sanctioned divergence, design §10 entry 20) | `Security/ServerSentEventLineCapTests` (all eight, **Security**); `ServerSentEventLineTooLongExceptionTests` (all seven); `ServerSentEventStreamFailureTests.A_line_cap_failure_releases_and_rethrows_the_exception_unchanged`; `ServerSentEventReaderTests.A_line_over_the_cap_surfaces_from_the_view_as_the_SSE_exception`, `A_source_that_throws_InvalidDataException_is_not_mislabelled_as_a_line_cap`; residual R1 documented by `A_hostile_stream_of_many_capped_lines_with_no_blank_line_is_the_documented_residual_R1` (Unit, on purpose, P7b-5). Closes #10 |
| `SSE-20` | MUST | ✅ | `ServerSentEventTests.Data_is_copied_at_init_and_through_with`, `The_exposed_data_list_cannot_be_written_through`, `A_null_data_list_or_a_null_element_is_rejected`; `ModelImmutabilityArchitectureTests` (`ServerSentEvent` added to its list: readonly fields, no plain setters, no mutable collection, sealed) (Unit) |
| `SSE-21` | SHOULD | ✅ | `ServerSentEventTests.Equality_compares_all_five_fields_with_data_element_wise_and_ordinal`, `Equality_handles_null_and_foreign_objects`, `ToString_is_lossless_so_unequal_events_never_print_alike`, `Distinct_events_from_a_generated_set_never_share_a_string` (Unit) |
| `SSE-22` | SHOULD | ✅ | `ServerSentEventTests.IsEmpty_is_true_only_when_every_field_is_absent` (a comment-only event is not empty) (Unit) |
| `SSE-23` | MUST | ✅ | `ServerSentEventStreamLifecycleTests.Dispose_and_DisposeAsync_in_any_mix_release_exactly_once`, `Construction_does_no_io_and_a_never_enumerated_stream_only_releases_the_response`, `A_GetAsyncEnumerator_that_is_never_disposed_leaves_release_to_the_facade`; `ServerSentEventStreamReleaseTableTests` (each path releases once) (Unit) |
| `SSE-24` | MUST | ✅ | `ServerSentEventStreamLifecycleTests.Natural_end_releases_once_and_a_release_failure_is_reported_not_thrown`, `AsEnumerable_yields_the_events_and_releases_once_at_the_end`; release-table rows `natural-end` (Unit) |
| `SSE-25` | MUST | ✅ | `ServerSentEventStreamLifecycleTests.Breaking_out_early_releases_once_and_swallows_a_release_failure`, `Breaking_out_early_swallows_a_release_failure_and_does_not_mask_the_consumer`, `A_consumers_own_exception_in_the_loop_body_is_not_replaced_by_a_release_failure`; release-table rows `early-enumerator-dispose`, `blocking-early-enumerator-dispose` (Unit) |
| `SSE-26` | MUST | ✅ | `ServerSentEventStreamLifecycleTests.A_second_GetAsyncEnumerator_throws_InvalidOperationException_at_the_call`, `A_second_view_throws_InvalidOperationException_at_the_call_in_either_order`, `The_same_view_object_enumerated_twice_throws`, `The_facade_is_not_an_IEnumerable`; `ServerSentEventStreamTypedTests.The_four_views_share_one_latch` (all 12 ordered pairs), `A_mapper_view_object_enumerated_twice_throws` (Unit) |
| `SSE-27` | MUST | ✅ | `ServerSentEventStreamLifecycleTests.Taking_a_view_after_close_throws_ObjectDisposedException`, `Taking_the_blocking_view_after_close_throws_ObjectDisposedException`, `A_close_between_pulls_ends_the_iterator_cleanly`, `A_close_between_pulls_ends_a_blocking_iteration_cleanly`, `A_view_taken_before_close_but_first_pulled_after_it_ends_cleanly_without_opening`; `ServerSentEventStreamTypedTests.After_close_every_view_throws_ObjectDisposedException` (Unit) |
| `SSE-28` | MUST | ✅ | `ServerSentEventStreamLifecycleTests.Dispose_and_DisposeAsync_in_any_mix_release_exactly_once`, `Explicit_dispose_propagates_a_release_failure` (only the first call propagates); release-table row `call-after-first-release` (Unit) |
| `SSE-29` | MUST | ✅ | `ServerSentEventStreamFailureTests.A_mid_stream_failure_releases_first_then_rethrows_the_primary_unchanged` (async and blocking: released before the catch runs, same object, stack names the failing read), `A_release_failure_on_a_failure_path_is_attached_to_the_primary_and_reported_nowhere_else`, `A_body_open_failure_is_attached_to_the_primary`; release-table rows `mid-stream-failure`, `blocking-mid-stream-failure` (Unit) |
| `SSE-30` | MUST | ✅ | `ServerSentEventStreamLifecycleTests.Natural_end_releases_once_and_a_release_failure_is_reported_not_thrown` (activity event plus id-130 log, no exception text), `Explicit_dispose_propagates_a_release_failure`, `ServerSentEventStreamFailureTests.A_close_owns_the_release_so_its_failure_propagates_though_the_torn_down_read_unwinds_first` and `A_blocking_close_owns_the_release_so_its_failure_propagates_though_the_torn_down_read_unwinds_first` (an explicit close from another thread still propagates), `On_an_explicit_dispose_the_streams_failure_is_primary_and_the_responses_is_attached`; `ServerSentEventStreamTypedTests.Done_releases_the_stream_quietly_and_completes`; the release table (all 12 rows) (Unit) |
| `SSE-31` | MUST | ✅ | `ServerSentEventStreamFailureTests.Close_from_another_thread_while_a_read_is_parked_surfaces_IOException_and_one_release`, `Close_cancels_the_internal_token_so_a_cooperative_stream_unblocks_at_once`, `The_blocking_view_normalises_a_close_the_same_way`, `A_close_during_the_open_is_an_IOException_and_one_release`, `A_stream_that_arrives_after_the_close_is_disposed_and_reported_as_closed`, `A_close_owns_the_release_so_its_failure_propagates_though_the_torn_down_read_unwinds_first` (async, with and without a slow stream dispose) and `A_blocking_close_owns_the_release_so_its_failure_propagates_though_the_torn_down_read_unwinds_first`; `ServerSentEventWireTests.A_close_from_another_thread_while_the_transport_read_is_parked_surfaces_IOException_with_one_release` (Integration) |
| `SSE-32` | MUST | ✅ | `ServerSentEventStreamLifecycleTests.FromResponse_rejects_a_bodyless_response_and_disposes_it` (204, 205, 304, HEAD, zero length, absent body; the message never holds the URL), `FromResponse_disposes_the_response_when_the_cap_is_invalid`, `FromResponse_rejects_a_null_response`, `A_throw_while_disposing_a_rejected_response_is_attached_not_thrown`; `ServerSentEventWireTests.A_204_fails_loudly_and_releases_the_response` (Integration); the AOT check (Unit) |
| `SSE-33` | MUST | ✅ | `ServerSentEventStreamTypedTests.The_mapper_receives_the_event_name_and_the_data_lines_joined_with_LF`, `No_data_lines_give_an_empty_string_and_an_absent_name_gives_null` (Unit) |
| `SSE-34` | MUST | ✅ | `SseMapResultTests` (all ten); `ServerSentEventStreamTypedTests.Value_yields_Skip_advances_silently_and_Done_ends`, `Done_releases_the_stream_quietly_and_completes`; release-table rows `typed-done`, `blocking-typed-done` (Unit) |
| `SSE-35` | MUST | ✅ | `ServerSentEventStreamTypedTests.The_adapter_is_lazy_one_mapper_call_per_raw_event_pulled`, `Events_after_Done_are_never_parsed_or_mapped` (one byte per read: not a byte beyond the sentinel's block), `MapAsync_forwards_the_enumeration_token` (Unit) |
| `SSE-36` | MUST | ✅ | `ServerSentEventStreamTypedTests.A_throwing_mapper_releases_first_then_propagates_unchanged` (and `…_on_the_blocking_path`), `A_default_result_is_a_mapper_failure_released_first`, `A_default_signal_converted_inside_the_mapper_is_an_ArgumentException_released_first` (the conversion throws inside the mapper and is an ordinary mapper throw), `A_fatal_mapper_exception_propagates_without_an_attach_and_the_enumerator_still_releases`; release-table rows `typed-mapper-throw`, `blocking-typed-mapper-throw` (Unit) |
| `SSE-37` | MUST | ✅ (gate turned on) | `Sse37ArchitectureTests.Sse_types_reference_nothing_in_the_serialization_namespace` (from skipped to enforcing with no edit, the moment `ServerSentEvent` landed), `Sse_types_hold_no_sentinel_literal_and_name_no_default_event_type`, `Sentinel_scan_sees_a_sentinel_and_a_message_default`, `Sentinel_scan_ignores_a_type_without_one`; the mappers in the tests and the AOT check hold the `[DONE]` sentinel, never core (Unit) |
| `SSE-38` | MUST | ✅ | `Sse38ArchitectureTests` (all six): no core type holds a `Last-Event-ID` literal; no SSE type refers to `IHttpClient`, `IAsyncHttpClient`, `HttpPipeline` or `DelegateHttpClient`, or uses a `Send` / `SendAsync` member; each scanner proven against fixtures; `sse.md` "Reconnecting is your loop" (Unit) |
| `SSE-39` | MUST | ✅ | `ServerSentEventReaderTests.Reading_is_pull_based` (exact byte counts per pull), `A_pull_over_a_large_source_reads_at_most_one_buffer_ahead`, `A_zero_byte_read_is_never_issued`; `ServerSentEventStreamLifecycleTests.Construction_does_no_io_and_a_never_enumerated_stream_only_releases_the_response`; `ServerSentEventStreamTypedTests.The_adapter_is_lazy_one_mapper_call_per_raw_event_pulled`; `ServerSentEventWireTests.A_CR_terminated_event_is_delivered_before_the_server_sends_more` (Unit, Integration) |
| `SSE-40` | SHOULD | ✅ | `ServerSentEventReaderTests.ReadAll_is_lazy_and_reads_nothing_until_enumerated`, `ReadAll_yields_every_event_then_ends`, `Each_view_builds_its_own_reader_so_the_BOM_is_per_stream`, `A_view_enumerated_twice_throws_InvalidOperationException`, `A_read_failure_surfaces_at_the_failing_pull_after_the_earlier_events`, `The_enumeration_token_reaches_every_read`, `ReadAll_validates_its_arguments_at_the_call_not_at_the_first_pull` (Unit) |
| `SSE-41` | MAY | N/A (design §11 item 21; §12's SSE row) | No reactive adapter ships (`Dexpace.Sdk.Reactive` is a `docs/first-release.md` post-v1 package), so the clause is adapter-scoped and vacuous, reported as that and never as satisfied or deferred. Its SHOULD half, "apply the runtime's fatal/non-fatal split and document source ownership", is honoured by the facade anyway: every catch site filters with `ExceptionFacts.IsFatal` (`A_fatal_exception_is_never_wrapped_or_swallowed`) and ownership is documented on both types. `ASYNC-21` travels with any later adapter |

Count: **41 rows**. By exit: 40 ✅, 0 🚫, 0 ⏳, 1 N/A. By level: 36 MUST (all ✅), 3 SHOULD (`SSE-21`, `SSE-22`, `SSE-40`, all ✅), 2 MAY (`SSE-19` ✅, `SSE-41` N/A).

## Work on other owners' rows

These carry no exit mark in 7b's checklist; the owner's checklist cites the evidence.

| ID (owner) | Work | Evidence |
|---|---|---|
| `IO-14` (3a) | `Utf8LineReader`'s `Whatwg` mode is reused unchanged by a second caller; its cap now surfaces as the SSE leaf (one internal accessor added, D2) | `ServerSentEventLineTests`; `Security/ServerSentEventLineCapTests`; `ServerSentEventReaderTests.A_source_that_throws_InvalidDataException_is_not_mislabelled_as_a_line_cap` |
| `OBS-37` (5b) | A live event stream reaches the facade unbuffered: the response-preview wrapper skips `text/event-stream` and unknown-length bodies | `ServerSentEventWireTests.A_CR_terminated_event_is_delivered_before_the_server_sends_more` (default pipeline, server gated on the next chunk) |
| `XCUT-1` (10) | The caller's cancellation is never rewritten to `IOException` | `ServerSentEventStreamFailureTests.A_cancelled_caller_token_surfaces_OperationCanceledException_and_is_not_rewritten` |
| `RECOV-10` / design §10 entry 13 (4b) | The primary is rethrown unchanged with a release failure attached | `ServerSentEventStreamFailureTests.A_mid_stream_failure_releases_first_then_rethrows_the_primary_unchanged`; `ServerSentEventStreamTypedTests.A_throwing_mapper_releases_first_then_propagates_unchanged` |
| `ASYNC-21` (8/9) | Travels with any later reactive adapter; none ships | the `SSE-41` row |

## The Security class

The exact allowed diff of `tests/Dexpace.Sdk.Core.Tests/Security` and `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security` against `main`: `ServerSentEventLineCapTests.cs` added, nothing else. Every existing
`Security` class passes unedited. `ServerSentEventLineCapTests` is the permanent regression for issue #10 (`SSE-19`): an unterminated 8 MiB line fails with `ServerSentEventLineTooLongException` after at most
`maxLineBytes + 2 × 4096` bytes were read, the message names the cap and holds no content byte, the reader stays failed, a line of exactly the cap passes and one byte more fails, a custom cap is honoured. It deliberately does **not**
pin the aggregate per-event residual R1 (P7b-5 is open for the lead; a `Security` test is never loosened), which `ServerSentEventReaderTests` documents as a Unit test.

## Verified facts

Plan task 0.1, on SDK 10.0.401, `net10.0`, `ImplicitUsings` off:

- **Fact 7 (LINQ ambiguity) holds.** A type implementing both `IEnumerable<int>` and `IAsyncEnumerable<int>`, with `System.Linq` imported, fails `.Where(…)` and `.Select(…)` with `CS0121` (ambiguous between `System.Linq.Enumerable` and
  `System.Linq.AsyncEnumerable`). An `IAsyncEnumerable`-only type binds `System.Linq.AsyncEnumerable.Where` and `await foreach` works. P7b-15 stands; `ServerSentEventStreamLifecycleTests.The_facade_is_not_an_IEnumerable` pins it.
- **Fact 2 (BOM equivalence) holds.** `Encoding.UTF8.GetString` gives U+FFFD for `EF BB` and for `EF BB 41`, and U+FEFF as the first character for `EF BB BF` and for `EF BB BF EF BB BF` (length 2, the second BOM kept). P7b-3 stands; the line tests pin the partial, doubled and split cases.
- **CS1626 / CS8424 shape holds.** Every iterator keeps `yield return` out of any `try` that has a `catch`: open and read are helpers that own the catch clauses, the iterator has only `try`/`finally`. The build raised neither diagnostic.
- **Fact 4 and fact 5 hold.** An abandoned enumerator never releases (`A_GetAsyncEnumerator_that_is_never_disposed_leaves_release_to_the_facade`); a throwing release in the early-dispose `finally` would replace the consumer's exception, so that release is quiet
  (`A_consumers_own_exception_in_the_loop_body_is_not_replaced_by_a_release_failure`, proven able to fail).
- **Fact 1 holds.** The reader fails after at most one cap plus two read buffers (`Security/ServerSentEventLineCapTests`).
- **NativeAOT.** `dotnet publish tests/Dexpace.Sdk.AotSmoke -c Release` raised no trim or AOT warning and the published binary printed `aot-smoke: all checks passed` with the new check (`MapAsync`, `Map`, the iterators, the latches, the record-struct conversion and a source-generated `JsonTypeInfo`).

## Deviations from the plan

1. **D1 — the SystemNet response body gained a synchronous `OpenRead`** (`HttpResponseMessageBody`, internal, no public API change). The plan's task 5.3 requires `A_blocking_Map_over_HttpPipeline_Send_round_trips` over the real transport, but P3a-5 left
   `HttpResponseMessageBody.OpenRead` to phase 8b, so the blocking facade views threw `NotSupportedException` over the reference transport. The override shares the open latch with `OpenReadAsync` (one `Claim` helper, same messages) and reads
   `HttpContent.ReadAsStream`; three tests in `HttpResponseMessageBodyTests` pin it and the `TrackingContent` double gained a synchronous `SerializeToStream`. `io.md` carries the dated correction. The design's prerequisite table called 4c's sync
   path "met"; it was met in core, not in the transport. 8b inherits a smaller job (the `RequestBodyContent` half is still its own).
2. **D2 — `Utf8LineReader` gained one internal accessor, `IsFailed`.** P7b-2 said 3a is unchanged. The reader maps the line reader's cap `InvalidDataException` to `ServerSentEventLineTooLongException`, but a *source* stream (a corrupt gzip body) also raises
   `InvalidDataException`, which would have been mislabelled as a line cap with a lying message. `catch (InvalidDataException) when (_lines.IsFailed)` maps only the cap; `A_source_that_throws_InvalidDataException_is_not_mislabelled_as_a_line_cap` pins it. No behaviour of the line reader changed.
3. **D3 — two cap constructors, and `Exception?`.** `ServerSentEventLineTooLongException(int)` and `(int, Exception?)` replace the plan's `(int, Exception? = null)`: `RS0027` rejects an optional parameter on an overload that does not have the most parameters, next to `(string, Exception?)`.
   The inner parameter is `Exception?` like `BodyTooLargeException`'s, not `Exception!`.
4. **D4 — `CA2225` is met by `SseMapResult.ToSseMapResult<T>()`**, the named alternative on the non-generic type (the design foresaw it). A static `FromSseMapResult` on the generic type was the first try and trips `CA1000`. `SseMapResult<T>.PrintMembers` is hand-written because the synthesized one reads `Value`,
   which throws for a signal.
5. **D5 — the AOT check lives in `SmokeChecks.ServerSentEvents.cs`**, a second part of the now-`partial` `SmokeChecks`, so the shared `SmokeChecks.cs` gains only the `partial` keyword and one call (fewer collision hunks against 7a and 7c). `SmokeModels.cs` gains `SmokeChunk` and its `[JsonSerializable]`.
6. **D6 — one iterator guard the plan's shape lacked.** `IterateRawAsync` / `IterateRaw` end cleanly, without opening, when the stream was closed before the first pull (a view taken, then `Dispose`, then `MoveNext`): the plan's `OpenAsync` would have raised an `IOException` for what `SSE-27` calls an in-flight
   iterator observing the closed state. `A_view_taken_before_close_but_first_pulled_after_it_ends_cleanly_without_opening` pins it. A close that races the open itself is still an `IOException` (`SSE-31`).
7. **D7 — wire-test mechanics.** The close-from-another-thread wire test cannot see the socket's close from the server, so the transport is decorated by a `CountingTransport` whose body wrapper counts each release, and a read-start signal armed after the first event (`ArmReadSignal`) replaces a sleep (plan convention 11).
   The plan's alternative (`server.ConnectionCount` and a socket EOF) was not needed.
8. **D8 — test placement and shape.** the plan's "a stream nobody enumerates is released by explicit dispose only" test moved from task 3.1 to 3.2 and became `A_GetAsyncEnumerator_that_is_never_disposed_leaves_release_to_the_facade` (it needs a view); the three-way split test is one `[Fact]` over every vector of up to 24 bytes, not a `[Theory]` per case (a `Skip` per long case would report 21 skips); the stage-3 doubles
   live with the other SSE helpers and `CountingReadStream` moved to `SseTestSupport.cs` when the typed tests needed it; `ReadAll_validates_its_arguments_at_the_call_not_at_the_first_pull` covers null, non-positive cap and unreadable source.
9. **D9 — `PublicAPI.Unshipped.txt` was produced from the build's `RS0016` messages** by a throwaway script (the IDE code fix is not available from the command line), appended as one block sorted case-insensitively at the end of the file. Every line is reviewed in the diff; the file's last block is the only hunk, which is also where 7a and 7c append.
10. **D10 — `dotnet test` cannot take the resource flags.** In Microsoft.Testing.Platform mode `dotnet test … -m:3` is forwarded to the test application and ends in "Zero tests ran"; every run therefore built under the limits first and tested `--no-build`.
11. **D11 — `CA2201`** is waived in two tests with a scoped `#pragma` (a test double throws `OutOfMemoryException` to prove a fatal exception is never wrapped, swallowed or attached to); the two library waivers below are the plan's. No other analyzer is waived.
12. **D12 — the closer takes the release latch before it cancels (review finding on `SSE-30` / `SSE-31`).** The plan's sketch routed an explicit close through the same latch-taking `ReleaseAsync(ReleaseKind.Propagate, null)` the iterators use, after `BeginClose` had cancelled the internal token. The cancel
    unblocks the parked read, and for a read that completes synchronously the iterator's own quiet release ran inline inside `Cancel()`, took the latch first and swallowed a release failure that the explicit close was owed (with an asynchronous release, `Dispose`/`DisposeAsync` also returned before the response
    was released). `BeginClose` now returns a `CloseRole` and the first closer takes `_released` before cancelling, so the iterator's release is the latched no-op P7b-13 describes; the closer releases through `ReleasePropagating{,Async}` directly, and `ReleaseKind` is gone (`Release`/`ReleaseAsync` take only the
    primary). Pinned for the async and the blocking close by two new tests in `ServerSentEventStreamFailureTests`, both failing on the previous code. A dated correction sits under P7b-13 in the design.
13. **Knowledge corpus.** The new `docs/knowledge/notes/sse-streaming.md` supersedes six harvested design entries (`ArrayPool` and the three-byte BOM check, `ReadAsync(Stream)`, the `SseMapResult` shape, "every absent field null", the facade's logger, the net8.0 floor) and records two conclusions (the LINQ ambiguity, the SystemNet `OpenRead` gap); `scripts/knowledge verify-structure` passes.

## Analyzer waivers

Both are scoped `SuppressMessage` attributes, the plan's two documented exceptions: `CA1001` on `ServerSentEventReader` (`SSE-17`: the reader never owns its source, so there is nothing to dispose) and `CA1711` on `ServerSentEventStream` (design E: the facade is a stream of events and deliberately does not derive
from `System.IO.Stream`). `CA1716` on the property name `Event`, `CA1000` on the generic result and `CA2225` on the conversion did not need a waiver.

## Deviation ledger

No new specification deviation: `SSE-19`'s cap is §10 entry 20, the absent strict mode is §11 item 17, and `SSE-41` is §11 item 21. The port-level departures from design §7.2's text are P7b-2 (the line reader is 3a's, a 4 KiB array, no `ArrayPool`), P7b-3 (the BOM is stripped at the string level, byte-equivalent), P7b-7 (`Data` is an empty list,
not `null`), P7b-8 (`ReadAllAsync(Stream)`, not `ReadAsync(Stream)`), P7b-19 (the facade's logger comes from `FromResponse`); from the roadmap card, P7b-21 (no FsCheck: exhaustive splits and seeded round trips in plain xUnit) and P7b-22 (the Ruby paths do not exist). Dated corrections to design §7.2, §10 entry 20, §12's SSE row and the roadmap card are filed
(see the roadmap note).

## Open rulings taken as designed

The lead had not ruled, so each landed as the design argued it: **P7b-5** (no aggregate per-event cap; residual R1 documented in `sse.md` and the reader's remarks, pinned only by a Unit test) and **P7b-21** (no FsCheck; the round trip moves to it unchanged if FsCheck is adopted repository-wide). Reversing either is local: an optional parameter and a moved test respectively.

## Hand-offs

- **8a** — the conformance kit inherits `LoopbackResponse.Streamed` (a chunk framed and flushed as the test's async sequence yields it) and may lift `ServerSentEventWireTests` into a per-transport script (streamed body delivery is `TRANSPORT` territory, not `SSE`).
- **8b** — `HttpResponseMessageBody.OpenRead` already exists (D1); the remaining 8b work on the body is the `RequestBodyContent` side. The transport's own streamed-body conformance (delivery before the next byte) has its first wire evidence here.
- **First release / an adapter package** — `Dexpace.Sdk.Reactive` (`IObservable<T>`) would carry `SSE-41` and `ASYNC-21`; an optional `SseItem<T>` bridge for strict-WHATWG callers (§11 item 17). Neither is scheduled.
- **The lead** — P7b-5 (an aggregate per-event cap) and P7b-21 (FsCheck).
- **7a, 7c** — the shared files (`PublicAPI.Unshipped.txt`, `CHANGELOG.md`, `CLAUDE.md`, the roadmap's status notes, `SmokeChecks.cs` and `SmokeModels.cs`, `Sse37ArchitectureTests.cs`) take append-only hunks from 7b; 7c owns the paging half of `Sse37ArchitectureTests`.

## Closing comment for issue #10 (drafted, for the lead to post; this branch posts nothing)

> Closed by the phase 7b pull request. The SSE reader's line buffer is bounded: `ServerSentEventReader` holds at most `maxLineBytes` content bytes of a line (1 MiB by default, configurable per reader and per `ServerSentEventStream.FromResponse`) and throws
> `ServerSentEventLineTooLongException`, a `StreamingException`, after reading at most one cap plus two 4 KiB buffers; it stays failed because the source is mid-line, and the message names the cap and never a byte of the line (checklist row `SSE-19`, design §10 entry 20).
> The regression is `Security/ServerSentEventLineCapTests` (an unterminated 8 MiB line, a line of exactly the cap, a custom cap), permanent. The line layer under it is the byte-level reader the issue's `StreamReader.ReadLineAsync` sketch lacked: a CR ends a line at once
> and a CRLF split across reads is one terminator (row `SSE-2`, proven on a real socket), and a leading BOM is consumed once (row `SSE-12`). One residual is documented and left open: the cap bounds a line, not an event (`sse.md`, P7b-5).

