# Phase 5c — Tracing and Metrics: Checklist

The execution-time checklist for sub-phase 5c of roadmap phase 5
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `66-phase-5c-tracing` (issue #66), following the
[design](2026-10-07-phase5c-tracing-design.md) and the [plan](2026-10-07-phase5c-tracing.md). User page:
[`tracing-and-metrics.md`](../../../../sdk-documentation/tracing-and-metrics.md).

The scope is 12 rows: `OBS-21`-`OBS-23` and `OBS-25`-`OBS-33` (10 MUST, 2 SHOULD). 5b owns the other 28 (`OBS-1`-`OBS-20`, `OBS-24`, `OBS-34`-`OBS-40`;
its checklist confirms it holds `OBS-24`, so the plan's contingent R5 test was not written); 28 + 12 = 40. Every test below is
`[Trait("Category", "Unit")]` and lives in `tests/Dexpace.Sdk.Core.Tests/` unless another place is named; the wire tests are `Integration`. Phase 5c adds and
edits no `Security` class and no `PublicAPI.Unshipped.txt` line.

**Adaptation to the as-built code (P5c-17).** The design and plan were written before 5a (#68) and 5b (#67) merged. 5b had already performed the
behaviour-preserving split (`InstrumentationPolicy` as an orchestrator over `AttemptScope`, `AttemptTelemetry` and `HttpLogEmitter`; the `MA0051` waiver
retired), so 5c built on it and did **not** redo it. Plan tasks 1.6 and 1.7 became: extend 5b's `AttemptTelemetry` struct in place (new tags, the
transmission ordinal, idempotent `End`, metrics through `HttpClientMetrics`) and keep the orchestrator under 70 lines. Plan reading R2 (the interim
attempt-span fallback) was **not needed**: the whole sub-phase landed on one branch, so the attempt span is parented through the bundle from the first
commit. 5a's immutable options are used as they are (`context.Options.Logging.AllowedQueryParameters` feeds the operation span's `url.full`, through 5b's
`RedactionCache`); 5a's `TimeProviderWaits` made the retry wait testable with `InstantTimeProvider` and needed no 5c change.

**Red evidence.** The adapter (plan PR 3) was done red-first: `TracePropagationWireTests` failed three ways on the as-built adapter (the runtime-child wire
parent, the borrowed-client treatment and the no-output-propagator residual), the pure-function tests failed to compile until `TraceContextStripping`
existed, and the first green run exposed the .NET 10 behaviour recorded in the design correction (a fourth test failed until the strip was conditioned on a
recorded runtime span). For the tracing core (plan tasks 1.3 to 1.7 and 2.2 to 2.4) the production code was written **before** its tests, in one stretch;
the tests were then written against it and the pins that carry the requirements were proven able to fail by temporary mutation, never committed: removing
`RetrySequenceStarted` (`A_new_retry_sequence_clears_an_earlier_exhaustion` failed), removing both dispose-on-throw calls (five `ListenerContractTests`
failed), computing the redacted URL unguarded (all four `UntracedAllocationTests` failed), putting `Activity.TraceIdGenerator` in `src/` (`RS0030` failed the
build with the entry's message). The scoped-recorder tests, `HttpSemanticConventionsTests`, `CallStateTransmissionTests` and `HttpClientMetricsTests` were not
mutation-checked. **Pins not shown able to fail:** `TraceIdTests` (a sample check), `HttpSemanticConventionsTests.The_attribute_name_constants_are_the_stable_convention_keys`.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `Diagnostics/{ScopedRecorderTests,HttpSemanticConventionsTests,HttpClientMetricsTests,AttemptTelemetryTests,OperationTelemetryTests,OperationEventTests,ActivityScopeTests,ListenerContractTests,TraceIdTests,UntracedAllocationTests}.cs`
(+ `TracingFixtures.cs`); `Pipeline/{CallStateTransmissionTests,OperationSpanLifecycleTests}.cs`; `Pipeline/Policies/{InstrumentationPolicyTests,RetryTraceEventsTests}.cs`;
`tests/Dexpace.Sdk.Http.SystemNet.Tests/{TraceContextStrippingTests,TracePropagationWireTests}.cs`; the AOT smoke check is `CheckTracingAndMetricsAsync` in
`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`. Recorders: `tests/Dexpace.Sdk.TestSupport/Diagnostics/{ActivityRecorder,MetricRecorder,TestHosts}.cs`.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `OBS-21` | MUST | ✅ (§10 entry 23: the SDK's own writes are inert; `SetTag` itself is not) | `ActivityScopeTests.Sdk_writes_to_a_non_recording_span_are_skipped`, `Stop_is_idempotent`; `OperationTelemetryTests.Every_mutator_skips_a_non_recording_span`, `Stop_is_idempotent_and_notifies_the_listener_once`; `AttemptTelemetryTests.A_non_recording_span_is_not_written_to`, `End_is_idempotent_and_stops_the_span_once` |
| `OBS-22` | MUST | ✅ | `ActivityScopeTests.Activity_Current_is_restored_after_the_call_including_on_throw` (sync/async × success/failure), `The_attempt_span_is_Activity_Current_inside_the_transport`, `The_operation_span_is_Activity_Current_in_a_PerCall_policy`, `Activity_Current_survives_a_thread_hop_in_the_transport` |
| `OBS-23` | MUST | ✅ (§10 entry 23: host `ActivityTrackingOptions`, keys `TraceId`/`SpanId`) | `ActivityScopeTests.The_attempt_span_is_current_at_the_log_site` (request, response and failure events), `An_untraced_call_leaves_the_callers_Activity_Current_untouched_and_log_sites_see_it`; wire half: `TracePropagationWireTests` |
| `OBS-25` | MUST | ✅ (§10 entry 23: no shared no-op span object) | `UntracedAllocationTests.Untraced_tracing_primitives_allocate_nothing`, `An_untraced_sync_send_through_InstrumentationPolicy_allocates_no_more_than_a_pass_through`, `Untraced_retry_and_redirect_event_calls_allocate_nothing`, `The_untraced_path_does_not_compute_the_redacted_url` (class in `NoDiagnosticListeners`; 5b's `DisabledPathAllocationTests` stays green); `OperationSpanLifecycleTests.An_untraced_call_bundle_is_None_even_under_an_ambient_activity` |
| `OBS-26` | MUST | ✅ | `OperationSpanLifecycleTests.A_traced_call_bundle_is_the_operation_span`, `A_traced_bundle_has_W3C_ids_and_is_valid`, `The_dispatch_context_is_not_re_keyed`; `TraceIdTests.Generated_trace_ids_are_32_lowercase_hex_and_non_zero`, `Generated_span_ids_are_16_lowercase_hex_and_non_zero` |
| `OBS-27` | MUST | ✅ W3C and no-op; 🚫 Datadog (§10 entry 24); zero-draw clause **admitted** (V8, P5c-14) | `TraceIdTests.The_W3C_flavour_is_reported_and_the_no_op_flavour_is_the_zero_id`, `The_sdk_does_not_install_a_trace_id_generator`, `A_hierarchical_parent_yields_a_bundle_that_reports_its_real_format`; the `Activity.TraceIdGenerator` ban in `BannedSymbols.txt` (build gate, `RS0030` shown firing) |
| `OBS-28` | SHOULD | ✅ in part (§10 entry 23: byte-count milestones not emitted; transport milestones are the runtime's own spans) | `OperationEventTests.Attempt_failed_carries_resend_count_error_type_status_and_delay_in_seconds`, `Attempt_failed_is_emitted_on_the_operation_span_not_the_attempt_span`; `RetryTraceEventsTests.A_retried_failure_emits_attempt_failed_with_the_next_delay`, `A_retried_exception_emits_attempt_failed_with_the_exception_type`, `The_resend_count_of_a_later_failure_follows_the_transmission_ordinal`; `OperationSpanLifecycleTests.Attempt_spans_are_children_of_the_operation_span` |
| `OBS-29` | MUST | ✅ | `OperationSpanLifecycleTests.A_retry_exhausted_call_ends_with_exhausted_then_exception_carrying_the_same_type` (listener stop order attempt, attempt, attempt, operation), `A_succeeding_call_ends_one_operation_span_without_error`, `A_returned_503_without_error_mapping_emits_no_exhausted_event`, `A_returned_503_with_error_mapping_emits_exhausted_then_exception`, `A_deadline_cancellation_after_exhaustion_does_not_pair_the_wrong_exception`, `A_fatal_exception_ends_the_operation_span_in_error_without_an_exception_event`; `OperationEventTests.Fail_emits_exhausted_immediately_before_the_exception_event`, `Exhaustion_on_an_earlier_hop_does_not_leak_into_a_later_one`; `RetryTraceEventsTests.A_new_retry_sequence_clears_an_earlier_exhaustion` |
| `OBS-30` | MUST | ✅ by contract (§11 item 37) | `ListenerContractTests.A_throwing_ActivityStarted_propagates_out_of_SendAsync_and_Send` (operation and attempt, sync and async), `A_throwing_ActivityStopped_disposes_the_response_before_propagating`; `ActivityScopeTests.Concurrent_calls_keep_their_spans_in_their_own_traces` (64 calls) |
| `OBS-31` | MUST | ✅ | `HttpClientMetricsTests.The_instruments_manufacture_the_three_kinds_with_per_measurement_tags`; AOT smoke `CheckTracingAndMetricsAsync` (NativeAOT) |
| `OBS-32` | SHOULD | ✅ (§11 item 38; V1: the bucket advice compiles, no `[Experimental]`) | `HttpClientMetricsTests.Duration_has_the_stable_attribute_set`, `Duration_is_in_seconds_and_the_unit_is_s`, `Active_requests_has_the_start_attribute_set_and_the_same_tags_for_plus_and_minus_one`, `The_histogram_carries_the_bucket_advice`, `Server_port_is_the_port_number_for_a_default_port`, `A_4xx_sets_error_type_to_the_status_code_string_on_the_duration`, `An_unknown_method_is_OTHER_on_both_instruments`; `InstrumentationPolicyTests.The_sync_path_fills_the_same_tags_as_the_async_path` |
| `OBS-33` | MUST | ✅ | `HttpClientMetricsTests.The_histogram_tolerates_NaN_and_infinity`, `Active_requests_returns_to_zero_after_success_and_after_failure`, `Active_requests_does_not_decrement_what_it_did_not_increment` |

Totals: 12 rows, 10 MUST (`OBS-21`, `OBS-22`, `OBS-23`, `OBS-25`, `OBS-26`, `OBS-27`, `OBS-29`, `OBS-30`, `OBS-31`, `OBS-33`) and 2 SHOULD (`OBS-28`, `OBS-32`).
**12 ✅** (`OBS-27` with a 🚫 Datadog clause and an admitted zero-draw clause, `OBS-28` in part), 0 N/A, 0 ⏳. `docs/first-release.md` gains two behavioural-asymmetry
entries (`two-meters`, `borrowed-client-traceparent`) and no ⏳ entry.

## Work on other owners' rows

These carry no exit mark in 5c; the owner's row cites 5c's tests.

| ID (owner) | Work done | Evidence |
|---|---|---|
| `CTX-14`, `CTX-15` (4a) | The bundle is the operation span when traced, `None` when untraced; no member added, renamed or retyped | `OperationSpanLifecycleTests.A_traced_call_bundle_is_the_operation_span`, `An_untraced_call_bundle_is_None_even_under_an_ambient_activity` |
| `OBS-20` (5b) | "A throwing tracer/meter is NOT caught" | `ListenerContractTests` (all), `AttemptTelemetry.End` throw test |
| `OBS-34` (5b) | Span lifecycle and metrics run at the default log level | `InstrumentationPolicyTests.Spans_and_metrics_record_at_the_default_log_level`; 5b's `HttpLoggingDefaultsTests.Spans_and_instruments_still_record_at_None` |
| `OBS-24`, `OBS-1` (5b) | 5b's census holds `OBS-24` (so the plan's contingent test R5 was not written); the joint zero-allocation assertion holds with 5b's lazy log path | `ActivityScopeTests.Activity_Current_survives_a_thread_hop_in_the_transport`; `UntracedAllocationTests.An_untraced_sync_send_through_InstrumentationPolicy_allocates_no_more_than_a_pass_through` over `NullLogger` |
| `XCUT-19` (phase 10) | Span `url.full` and event `url.full` are the redacted form (the call's allow-list) | `OperationTelemetryTests.Start_sets_the_seed_tags_with_the_redacted_url_only_when_recording`, `Start_honours_the_calls_allowed_query_parameters`; `OperationEventTests.RedirectHop_redacts_the_target`; `AttemptTelemetryTests.A_traced_begin_starts_a_client_span_under_the_bundle_with_the_start_tags` |
| `XCUT-20` (phase 10) | §11 item 37's reading: tag computation is total, callbacks are not wrapped | as `OBS-20` |
| `RETRY-*` (6a) | The three event calls (`RetrySequenceStarted`, `AttemptFailed`, `RetriesExhausted`) at today's decisions; `IsExhausted` is the one method 6a replaces | `RetryTraceEventsTests` |
| `REDIR-*` (6b) | `OperationTelemetry.RedirectHop` ships with no production caller | `OperationEventTests.RedirectHop_carries_hop_status_the_redacted_target_and_cross_origin` |
| `SEAM-28` (2b; ⏳) | The operation span is named by the method until a carrier for the operation id exists | `OperationTelemetryTests.Start_opens_an_internal_span_named_by_the_normalised_method` |

## Existing assertions changed

| Test | Change | Why |
|---|---|---|
| `InstrumentationPolicyTests` (class) | Recorders are `ActivityRecorder.Scoped`, created in each test body; assertions read `ClientSpans(recorder)` (the `Client`-kind spans) because every `HttpPipeline` call now also opens an operation span; the class lost `IDisposable` | Breaking 1; plan task 1.2 |
| `…Resend_count_is_absent_on_the_first_transmission_and_counts_retries` (was `ProcessAsync_AttemptNumber_SetOnResendCountTag`) | The first attempt span has no `http.request.resend_count`, the second `1` (was `0` and `1`) | Breaking 3 |
| `…ProcessAsync_DurationHistogram_CarriesMethodAndStatusTags` | Asserts the full stable attribute set and no `error.type`; the duration and active-request tests use `MetricRecorder.ForServer` over `TestHosts.Unique()` | Breaking 4 |
| `HttpLoggingDefaultsTests.Spans_and_instruments_still_record_at_None`, `DiagnosticContextFlowTests.The_attempt_span_is_current_when_http_request_is_emitted` | Scoped recorder; the single span is the `Client`-kind one; the first uses a unique host (a parallel class's measurement of `api.example.com` made the old form flaky) | Breaking 1; isolation |
| `InstrumentationContextTests.The_tracer_factory_is_safe_under_concurrent_invocation` | Counts only activities in the test's own trace (a scoped recorder would drop them: the bundle is built from an explicit remote context) | isolation (P5c-15) |
| `ProcessAsync_ActivitySetOnContext_DuringContinuation`, `The_downstream_sees_the_activity_and_the_upstream_does_not`, `ProcessAsync_W3CActivity_InjectsTracestateHeader_WhenNonEmpty` | Add a scoped recorder (an attempt span exists only under an operation span, which needs a listener) | Position B |
| `DexpacePipelineTests` | Unchanged: no assertion read a span count | |

No other existing assertion changed; the gate proves it. The plan's `With_no_ambient_activity_the_attempt_span_is_still_started` (R2's fallback fact) was never written.

## Unedited `Security` classes

All of them. Evidence: `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` prints nothing, and both
`Category=Security` runs pass. `git diff --stat main...HEAD -- src/*/PublicAPI.Unshipped.txt` prints nothing.

## Pre-flight facts (V1 to V8)

| Fact | Result | Consequence |
|---|---|---|
| V1 `CreateHistogram` with `InstrumentAdvice<double>` | Compiles on SDK 10.0.401 under the repository's analyzers; no `[Experimental]` | The advice is in; `The_histogram_carries_the_bucket_advice` |
| V2 `Activity.AddException(Exception, in TagList, DateTimeOffset)` | Exists (`Disposal.Report` already calls it) | Not used for the operation's event, because of V3 |
| V3 does `exception.stacktrace` render a suppressed trail? | Yes: `SdkException.ToString()` renders `Suppressed`, which can carry a URL | The `exception` event is written by hand with `Exception.StackTrace`; `OperationTelemetryTests.The_exception_event_never_renders_the_suppressed_trail` |
| V4 `Activity.Stop()` twice | A no-op the second time; the listener is notified once | Both `OperationTelemetry.Stop` (guards on `IsStopped`) and `AttemptTelemetry.End` (its own flag) are idempotent regardless |
| V5 xUnit v3 `CollectionDefinition(DisableParallelization = true)` | Exists in `xunit.v3` 4.0.1 | `NoDiagnosticListeners` (the type is named `…Definition`: `CA1711` forbids a `Collection` suffix) |
| V6 the runtime's switch name, env fallback, caching | **Not verified against runtime source** (none on the host); names taken from the design (`System.Net.Http.EnableActivityPropagation`, `DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION`, default on); the default behaviour is proven by the wire tests | Pure-function tests for the decision (R4); **review fix (F2):** `RuntimeInjects(runtimeSwitch, hasListeners, propagatorInjects)` is a pure overload with a test per branch, and the propagator check now asks the propagator to inject (`PropagatorInjectsTraceparent`) because the no-output propagator still lists `traceparent` in `Fields` (found while testing; tested against the real default and no-output propagators); the static switch itself is still not toggled in-process; `Environment.GetEnvironmentVariable` is read once under a scoped `RS0030` pragma citing P5c-11 |
| V7 the runtime span's source name | `System.Net.Http` (a listener on it recorded the runtime span whose id is the wire parent). **New fact:** with an ambient activity and no listener on it, the runtime writes a fresh span id for an unrecorded span | The strip is conditioned on a recorded runtime span (design correction 2; knowledge note) |
| V8 zero-draw coercion in the runtime's id generator | **Not verifiable** (no runtime source); the public contract does not promise it | The clause is admitted (§10 entry 24 correction); the SDK never sets the generator and the build bans it |

The phase-start queries could not run `scripts/knowledge` (a `dotnet run` wrapper); the same content was read directly: the OBS conflicts (none), `notes/observability.md` and
appendix C rows `OBS-21`-`OBS-33`. One note entry was added to `docs/knowledge/notes/observability.md` (the strip condition).

## Deviation ledger as built

| ID | As built |
|---|---|
| P5c-1 | The 12/28 split stands; 5b's census holds `OBS-24` |
| P5c-2 | Operation span opens in `HttpPipeline.SendCoreAsync` before the dispatch context; `PipelineStage.Operation`, `OperationPolicy` and `HttpPipeline` documentation corrected |
| P5c-3 | Untraced bundle is `InstrumentationContext.None` under an ambient activity; `CallKey.TraceId` is zero |
| P5c-4 | `Internal`, named by the normalised method (`HTTP` for unknown), ends when the response is returned; `SEAM-28` has no carrier |
| P5c-5 | Success leaves `Unset`; failure is `Error` plus a hand-written `exception` event (V3); fatal ends `Error` with no event |
| P5c-6 | Exhaustion recorded in `CallState`, emitted by `Fail` immediately before `exception`; interim predicate in `RetryPolicy.IsExhausted` |
| P5c-7 | Event shape as designed; delays in seconds |
| P5c-8 | Attempt spans through the bundle; `resend_count` is the call's transmission ordinal, absent on the first. **Review fix (F3):** 5b's `http.request`/`http.response` log events now carry the same ordinal (`AttemptScope.ResendCount`, set by `AttemptTelemetry.Begin`), so span and log agree across a redirect hop; pinned by `HttpLogEmitterTests.The_resend_count_is_the_transmission_ordinal_across_a_redirect_hop_as_on_the_span` |
| P5c-9 | `_OTHER` / `HTTP` |
| P5c-10 | Two instruments, bucket advice, enabled-guarded tag lists, cached boxed status codes |
| P5c-11 | **Changed:** the strip requires a recorded runtime span (`HasListeners()` on an unused `System.Net.Http` source) plus the switch and a propagator that really injects (review fix F2: probed by injecting, not by listing fields); owned and borrowed alike |
| P5c-12 | Core keeps stamping |
| P5c-13 | **Extended:** also covers a throwing meter callback after a response exists (`InstrumentationPolicy` releases the held response, wrapped or not) |
| P5c-14 | Clause admitted; ban in `BannedSymbols.txt` |
| P5c-15 | `NoDiagnosticListeners` for the allocation tests and every throwing/non-recording listener test; scoped recorders for the rest |
| P5c-16 | No public API; no `PublicAPI.Unshipped.txt` line |
| P5c-17 | 5b had landed: extended its `AttemptTelemetry` in place; no waiver left to retire |
| P5c-18 | The URL allow-list is 5b's (`RedactionCache`, the call's `Logging.AllowedQueryParameters`); `HttpSemanticConventions` constants are available to 5b's templates but 5b's keys were not rewritten |
| P5c-19 | 5a's records used as they are; no option added |
| P5c-20 | Three calls in `RetryPolicy`, no restructure (its `MA0051` waiver stands for 6a) |
| P5c-21 | One branch; commits: the test-support commit, the tracing core, the adapter, the documentation |

Plan readings: R1 `Stop(Activity?, bool settled)` as the plan proposed. R2 not needed (one branch). R3 as planned (`Transmissions - 1`; `0` without `InstrumentationPolicy`). R4 as planned,
plus the new wire test `Without_a_runtime_listener_the_sdk_stamp_survives_even_a_handler_that_injects_nothing`. R5 not applicable. R6 `RedirectHop` has no production caller. R7 is the table above.

## Plan departures worth knowing

- The plan's eight-plus commits became four code and documentation commits on the branch (see the hand-over report).
- `UntracedAllocationTests` takes the best of six 1,000-call rounds for every zero assertion: a one-off 24-byte allocation (tiered JIT) appeared in a single round after a large-allocation test.
- `ListenerContractTests` holds every throwing-listener test (the plan spread them over `AttemptTelemetryTests`, `HttpClientMetricsTests` and `ActivityScopeTests`): a throwing `MeterListener`
  callback in a parallel class made unrelated `ReDriveLifecycleTests` fail intermittently, which is how the process-wide leakage was found.
- `ActivityRecorder.Scoped` clears `Activity.Current` before starting its root: a default parent context falls back to the ambient activity, so a root started under another test's activity would join its trace.
- The plan's `A_borrowed_client_gets_the_same_treatment…` and the residual test both list `System.Net.Http` in the recorder, because the strip depends on a listener there.
- `TracePropagationWireTests` is its own `DisableParallelization` collection (`TracePropagation`): it installs listeners and reads headers that depend on an ambient activity.
- The AOT smoke check (`CheckTracingAndMetricsAsync`) saw no trim or AOT warning; `InternalsVisibleTo` was not extended.
- `DexpaceDiagnostics`, `HttpPipeline`, `PipelineStage.Operation`, `OperationPolicy`, `PipelineContext.Instrumentation` and `SystemNetHttpClient` gained documentation only (no signature).
