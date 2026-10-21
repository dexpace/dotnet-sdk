# Phase 11 — Deviation Reconciliation: Checklist

The execution-time checklist for roadmap phase 11 ([phase card](../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID the phase changed or closed, in the legend of the roadmap's cross-cutting constraint 3.
Written from what was built on the merged branch `104-phase-11-deviation-audit` (issue #104; the three implementation branches `104-p11-a-gates-taxonomy`, `104-p11-b-bodies-transport` and `104-p11-c-obs-config` and this
branch's close-out, landing as one pull request), following the [design](2026-10-19-phase11-deviation-reconciliation-design.md) and the [plan](2026-10-19-phase11-deviation-reconciliation.md). The deliverable is
[`docs/deviations.md`](../../../deviations.md), gated by `LedgerAuditTests`; the user pages are `transport.md`, `retry.md`, `sse.md`, `configuration.md`, `dependency-injection.md`, `bodies.md`, `http.md`,
`tracing-and-metrics.md`, `cross-cutting-invariants.md` and `conformance.md` under `docs/sdk-documentation/`.

The phase owns **no requirement of its own** (the roadmap: "Phases 11 and 12 re-audit rows. They do not own any"). The rows below are the phase's *dated corrections*: an ID a repair changed, whose earlier closed
checklist row now misreports the build, and an ID a closed checklist left deferred to a later phase whose closing evidence sat in a table the coverage script does not read as owning. The script resolves an ID by phase
order, so a row here replaces the earlier row without editing a closed checklist; where the earlier row was not an hourglass the new row says **supersedes**, which `requirement-coverage.cs` reads as the declaration that it knows
it is overwriting (a rule added by this phase, with a self-test). The scope is **53 rows** (42 MUST, 7 SHOULD, 4 MAY by appendix C): **51 ✅ / 1 🚫 / 0 ⏳ / 1 N/A**. Five of the 51 ✅ carry a clause that stays unmet or
declined and say so (`SEAM-29`: the generic builder contract, a mechanism; `OBS-28`: the byte-count milestones, a declined SHOULD; `TRANSPORT-14`, `PAGE-21` and `AUTH-15`: the **three counted clauses**). The 15 MUST hand-off rows of phase 10's list are `RECOV-17` to `-24`, `-27`,
`-28`, `-29`, `-34`, `BODY-4`, `BODY-5` and `BODY-10`; five more hand-offs the same measurement found at SHOULD and MAY level (`RECOV-25`, `RECOV-30`, `RECOV-31`, `SEAM-15`, `OBS-19`), one mark that read "🚫 vacuous"
(`ASYNC-4`) and two declined features whose rows read ✅ (`SEAM-28`, `OBS-28`) are given the same treatment, so the generated design §12 does not list a built requirement as deferred, a vacuous one as unmet or a declined one as satisfied. The one MUST row still ⏳ is `NFR-17`, which phase 10's row owns and which
names its owner (`docs/first-release.md`, the `main` ruleset): MUST ⏳ 16 before, **1** after.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification or an unmet clause on a stated domain, with the reason and the design §10 entry. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the owner. |
| N/A | Not applicable in this port. |

## Requirement rows

Tests are named `Class.Method` as they exist in the tree (the ledger gate checks the ones `deviations.md` cites; the rest were checked mechanically against the tree when this table was written).

| ID | Level | Status | Task(s) | What was built, where proven |
|---|---|---|---|---|
| `NFR-12` | SHOULD | ✅ split (supersedes the phase 10 row; the release half stays after the first publish) | 1.1 | `scripts/ci/reproducible-pack.sh` pins each checkout's toolchain with a job-local `global.json` (`rollForward: disable`), prints `dotnet --version` from inside it and exits 1 naming both versions when it is not the pinned one (hypothesis (a): the runner's SDK patch); `--self-test` packs once, flips one byte in one `.nupkg` of a copy and requires the compare to exit 1 naming it, and requires the toolchain check to refuse another version. CI runs it as the step "Pack twice and compare - self-test", held by `CiWorkflowTests.Every_gate_of_the_gate_table_is_a_ci_step`. The failing CI log was not read (a remote read), so whether the job is green is the lead's first check (*Open items*). |
| `SEAM-1` | MUST | ✅ (supersedes the phase 2b row) | 1.3 | Core's allowance is a fixed set, `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions` (band-matched), not the facade's closure (`scripts/ci/dependency-audit.cs`); `scripts/ci/dependency-audit-selftest.sh` injects a package under the facade, under its dependency and with no edge into copies of core's `deps.json` and requires exit 1 naming it (CI step "Dependency audit - self-test"); `Seam1ArchitectureTests` holds the no-concrete-capability half; design §10 entry 1. |
| `NFR-1` | MUST | ✅ split (supersedes the phase 10 row) | 1.3 | As `SEAM-1`: the packed nuspec and `deps.json` allow exactly the two packages, and a third fails the audit with a self-test; the facade itself is `SEAM-1`'s sanctioned letter, `NFR-1`'s "compile-time-only" wording the split (design §10 entry 1). `packaging:nfr-1.core-dependencies` is unchanged and green over the five packages. |
| `RECOV-2` | MUST | ✅ (supersedes the phase 4b row) | 1.4 | `FatalFilterArchitectureTests.No_catch_all_in_a_runtime_library_swallows_a_fatal_exception` (`Unit`) scans the four runtime libraries: an unfiltered catch-all must end in `throw;` and a filtered one must exclude the fatal exceptions in its `when`; the two `UrlRedactor` catch-alls that swallowed `OutOfMemoryException` now filter; `The_scanner_flags_a_swallowing_catch_and_passes_a_compliant_one` proves the scanner can fail. Design §10 entry 12 (the "exactly four sites" claim was wrong; ledger scan N20). |
| `RETRY-25` | MUST | ✅ (supersedes the phase 6a row) | 1.4 | As `RECOV-2`: the retry loops' filters are held by the same scan (`FatalFilterArchitectureTests.The_scan_sees_both_kinds_of_catch_all_in_the_real_sources`). |
| `OBS-21` | MUST | ✅ (supersedes the phase 5c row) | 1.5 | `Disposal` writes a quiet-dispose failure only to an activity whose `IsAllDataRequested` is true: `DisposalTests.A_suppressed_dispose_failure_is_not_recorded_on_a_non_recording_activity` (`Unit`, `NoDiagnosticListeners`), red against the unguarded `AddException` (design §10 entry 23, V14). |
| `XCUT-8` | MUST | ✅ (supersedes the phase 10 row) | 1.6 | `HttpResponseException`'s public constructor rejects `null` (`ArgumentNullException`, `ParamName` `response`) and a non-error status (`ArgumentException`): `HttpResponseExceptionTests.The_constructor_rejects_a_null_response` and `The_constructor_rejects_a_non_error_status` (`Unit`); `CA1062` is `error` for `src/` in `.editorconfig` and its one finding was this constructor (V12), retiring design §10 entry 28; the two kit controls that built a successful exception use a bare `SdkException` and stay red for `xcut-8`. |
| `TRANSPORT-20` | MUST | ✅ (supersedes the phase 8a row) | 2.1 | `ServiceRequestException` and `ServiceRequestTimeoutException` derive from `IOException` and implement `IRetryableError`: `TransportExceptionTaxonomyTests.ServiceRequestException_is_an_IOException`, `ServiceRequestException_reports_itself_retryable`, `A_suppressed_secondary_is_readable_through_the_trail` (`Unit`); the shared `SuppressedTrailRenderer` renders the trail for both families; the kit's `FailureAssertions`, `TaxonomyInvariants`, `RedirectAndTimeoutAssertions`, `CancellationAssertions`, `ConcurrencyAssertions` and `ResendAssertions` now require `IOException` and read `IRetryableError` in their negative forms; new control `RefusalNotAnIoError` (design §10 entry 7, retired). |
| `XCUT-4` | MUST | ✅ (supersedes the phase 10 row) | 2.1, 2.2 | The generic I/O error clause is met (`IOException` base); `ServiceResponseException`, public and thrown by nothing, is deleted (`TransportExceptionTaxonomyTests.Core_exports_one_transport_failure_family`, ledger scan N3); `SendFailures` names an ended response truthfully: `SendFailuresTests.A_response_that_ended_after_the_request_arrived_is_not_called_unsent`. The retry classifier reads `IRetryableError` and the cause chain, so classification is unchanged. |
| `ASYNC-2` | MUST | ✅ (supersedes the phase 8a row) | 2.3 | `AsAsync` delivers a rejecting scheduler's failure through the returned task and a signalled token as a cancelled task before the scheduler is asked: `SyncToAsyncBridgeTests.A_scheduler_that_rejects_the_work_faults_the_returned_task_instead_of_throwing` and `A_pre_cancelled_call_completes_cancelled_without_touching_the_scheduler` (`Unit`, over `RejectingTaskScheduler` in `Dexpace.Sdk.TestSupport`). Finding F-A2 of the 8a note closed. |
| `SEAM-30` | MUST | ✅ (supersedes the phase 2b row) | 2.4 | The cancel-without-interruption mode is the public `ResponseTaskExtensions.WaitOrDisposeAsync`: `WaitOrDisposeTests.Every_caller_gets_a_live_response_or_a_cancellation_and_every_produced_response_is_closed_once` runs the V5 race 20 000 times over the bridge and requires every produced response to be closed exactly once (the harness leaked 5 responses in each of 3 runs with a raw `WaitAsync` in its place); `A_response_that_arrives_after_the_caller_stopped_waiting_is_closed_exactly_once` (design §11 item 43 repaired). |
| `SEAM-29` | MUST | ✅ (supersedes the phase 2a row); 🚫 the generic builder contract (design §10 entry 10) | 2.5 | Every null-rejecting `init` accessor in `src/` passes `nameof(Property)`: `InitAccessorParamNameTests.Every_validating_accessor_in_the_libraries_names_its_property` and `The_table_covers_every_validating_accessor` (`Unit`) hold one row per accessor and re-scan the sources, so a bare `ThrowIfNull(value)` fails. About fifteen range and format checks inside accessors still report `ParamName` `value`: open, listed in *Open items*. |
| `BODY-14` | MUST | ✅ (supersedes the phase 3b row) | 3.1 | A second open returns the same stream in either form: `ResponseBodyLifecycleTests.A_second_open_returns_the_same_stream` (stream and bytes variants, both orders), `An_open_after_dispose_still_throws_StreamClosedException`; `HttpResponseMessageBodyTests.A_second_open_returns_the_same_stream`. The response-logging wrapper keeps its single-use throw past its capture cap, because `BODY-24` (MUST) says a second read in that regime fails (`LoggingResponseBodyTests`). Design §10 entry 6, retired. |
| `HTTP-41` | MUST | ✅ (supersedes the phase 3b row) | 3.1 | As `BODY-14`: the transport body caches the handle it obtained and flips its latch before it touches `HttpContent`. |
| `HTTP-19` | MUST | ✅ (supersedes the phase 2a row) | 3.2 | `Headers.Builder.AddInbound` validates a received name with `HeaderSyntax.ValidateInboundName` (the token grammar, no trim): `HeaderInjectionValidationTests.The_inbound_path_does_not_trim_a_name` (`Security`, added by this phase, never edited afterwards); the outbound trim tests are unedited (design §11 item 70, amendment N11-2). |
| `XCUT-18` | MUST | ✅ (supersedes the phase 10 row) | 3.2, 3.4 | The HTAB edge is decided: outbound names are trimmed (`HTTP-17`), inbound names are refused with surrounding whitespace; kit `xcut-18.model-validation` now requires the inbound refusal, with a new control that trims an inbound name (`InvariantSecurityControlTests`); `Request` also refuses a `Uri` built without canonicalization, whose CR/LF would reach the request line: `HeaderInjectionValidationTests.A_uri_created_without_canonicalization_is_rejected` (`Security`, added by this phase). |
| `TRANSPORT-14` | MUST | ✅ value and obs-text (supersedes the phase 8b row); 🚫 name clause (design §10 entry 32) | 3.2, 6.1 | The value half is met (`InboundHeaderTests`, kit `transport-14.value-control-dropped`, `.obs-text-preserved`); the name clause stays unmet, now with a corrected reason: a `PlaintextStreamFilter` could drop such a line, but only by parsing every response head ahead of the runtime's parser (V7), which phase 11 declines (P11-11). The drivers carry the permanent waiver for `transport-14.name-malformed-dropped` alone, which fails the run the day the runtime stops rejecting. **Counted** (one of three). |
| `BODY-11` | MUST | ✅ (supersedes the phase 3b row) | 3.3 | `FromFile` classifies a size-0 path on a non-Windows host with `System.Formats.Tar` (`UnixFileKind.SpecialKindOf`) without opening it and rejects a FIFO, device or socket with an `ArgumentException` naming the kind: `FileRequestBodyTests.A_character_device_is_rejected_at_construction`, `A_fifo_is_rejected_at_construction_without_blocking`, `An_empty_regular_file_is_accepted` (`Unit`); the AOT smoke's `CheckPhase11Async` rejects `/dev/null`. Verified on Linux only: the macOS CI row is the first to run it elsewhere. Design §10 entry 31, retired. |
| `HTTP-40` | MUST | ✅ (supersedes the phase 3b row) | 3.3 | As `BODY-11`. |
| `PAGE-2` | MUST | ✅ (supersedes the phase 7c row) | 3.5 | `Page<T>` copies the list it is given: `PageTests.Values_is_a_snapshot_of_the_list_passed_in`, `Values_cannot_be_changed_through_a_cast` (`Unit`); `PageStep` drops its duplicate copy. |
| `PAGE-21` | MUST | ✅ empty segments (supersedes the phase 7c row); 🚫 the unreserved residual (design §10 entry 18) | 3.4, 3.6 | The splice copies an empty `&` segment verbatim: `QuerySpliceTests.Stray_empty_segments_are_copied_verbatim`; `Request` refuses a non-canonicalizing `Uri` (the only verbatim route writes CR/LF to the wire). The residual, RFC 3986 section 6.2.2.2 equivalence of unreserved characters, stays: amendment N11-9. **Counted** (one of three). |
| `TRANSPORT-2` | MUST | ✅ (supersedes the phase 8b row) | 4.1 | A `PlaintextStreamFilter` installed by `SystemNetHttpClient.CreatePrimaryHandler` (the owned construction and the DI named client) turns a connection that ends before any response byte into the `HttpIOException(ResponseEnded)` the runtime raises itself, so a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE` arrives once and the failure maps to `ServiceRequestException`: `BodylessRetryWireTests.A_bodyless_request_reaches_the_server_once_on_an_sdk_built_handler` (five methods, both faces, both constructions), `FirstResponseByteStreamTests`. A handler or `HttpClient` the caller supplies keeps its native retry: `A_caller_handler_keeps_its_native_retry` (design §11 item 71, amendment N11-4, asymmetry `borrowed-handler-native-retry`); the waiver survives for that subject alone. HTTP/1.x only. Design §10 entry 33, retired. |
| `OBS-27` | MUST | ✅ (supersedes the phase 5c row) | 5.1 | `TraceIdGeneration.W3C()`, `Datadog()` and `FormatDatadog(ActivityTraceId)`, each re-drawing an all-zero draw: `TraceIdGenerationTests` (`Unit`, 8 tests: 10 000 draws each, the zero source, the decimal rendering); the SDK still never sets `Activity.TraceIdGenerator` (`BannedSymbols.txt`; `TraceIdTests.The_sdk_does_not_install_a_trace_id_generator`); the AOT smoke draws a Datadog id. Design §10 entry 24, retired; the runtime's own generator tests no zero draw (V13). |
| `OBS-3` | MUST | ✅ (supersedes the phase 5b row) | 5.2, 5.3 | A null field value in the header-bearing events' state is the literal `null`: `HttpLogRecordTests.A_null_value_is_emitted_as_the_literal_null`; no SDK site passes a null today, and the six plain-name `LoggerMessage.Define` sites moved to the generator with their event ids, names and levels pinned (`HeaderDropLogTests`, `ProxyAdapterTests`, `ProxyFromEnvironmentTests`, `DisabledPathAllocationTests`). |
| `CFG-2` | MUST | ✅ (supersedes the phase 9 row) | 5.4 | When the registered `IConfiguration` is the root that holds the client's section, the reader walks its providers from the top and takes the first non-blank value (a chained provider and a list item included): `EmptyValueTests.An_empty_value_falls_through_to_the_lower_provider`, `A_chained_configuration_is_walked`, `An_empty_list_item_falls_through_to_the_lower_items_value`, `A_ConfigurationManager_registered_as_the_configuration_is_walked`; a section from an unregistered configuration keeps the merged read (`A_section_from_an_unregistered_configuration_keeps_the_merged_read`). Design §10 entry 25, §11 item 68; `di-empty-shadows` leaves the register. |
| `RETRY-21` | MUST | ✅ (supersedes the phase 6a row) | 5.5 | `RetryOptions.PacingHeaders` is a validated, copied, ordered subset of the four pacing headers, read by the stage stack in that order, each by its own semantics: `RetryPolicyTests.The_stage_stack_walks_the_configured_pacing_header_order`, `RetryOptionsTests.PacingHeaders_rejects_an_unknown_name`, `RetryPacingTests.A_header_that_is_not_in_the_list_is_not_read` (`Unit`); the recovery stack keeps the fixed precedence (`RetryRecoveryTests.The_recovery_stack_keeps_the_fixed_precedence`); configuration key `Retry:PacingHeaders` (`RecordBindingTests.Pacing_headers_replace_the_default_order_in_both_list_forms`). Design §11 item 54. |
| `RETRY-38` | SHOULD | ✅ (supersedes the phase 6a row) | 5.7 | The attempt header replaces any value the request already carries and `AttemptHeaderName` may not be `Idempotency-Key`: `RetryEngineTests.The_attempt_header_replaces_its_previous_value`, `RetryOptionsTests.AttemptHeaderName_rejects_the_idempotency_key_name` (`Unit`); a configured `IdempotencyKeyStep` header of the same name is refused where each stack is assembled (`PipelineBuilder.Build`, a per-call options override on `HttpPipeline`, `RecoveryDispatcher`'s constructor): `AttemptHeaderKeyCollisionTests` (`Unit`, review fix). Design §11 item 10. |
| `SSE-19` | MAY | ✅ (supersedes the phase 7b row; sanctioned divergence, design §10 entry 20) | 5.6 | A per-event data cap closes residual R1: the data lines of an event cost their UTF-8 value bytes plus one, and an event over `maxEventBytes` (8 MiB by default) fails with `ServerSentEventTooLargeException`, the reader staying failed: `ServerSentEventLineCapTests.Data_lines_that_each_fit_but_never_end_the_event_fail_once_the_event_cap_is_passed` (`Security`, added by this phase), `ServerSentEventEventCapTests` (`Unit`), `ServerSentEventTooLargeExceptionTests`; the AOT smoke trips the cap. |
| `AUTH-15` | MUST | ✅ (supersedes the phase 6c row); 🚫 MD5 on a host whose crypto refuses it (design §10 entry 16) | 5.8 | The once-per-process probe counts any non-fatal failure as a refusal and the handler drops MD5 from its preference (`Md5AvailabilityTests.An_unexpected_probe_failure_means_refused`, `A_fatal_probe_failure_still_propagates`); the clause stays unmet on a FIPS host, where a vendored MD5 would take the deployment out of its compliance boundary, and the mapping has never run on a real FIPS host. **Counted** (one of three). |
| `XCUT-24` | SHOULD | ✅ (supersedes the phase 10 row; design §11 item 69) | 6.3, 6.4 | The phase 10 decline misread `BODY-30` and `RECOV-16`, which require the cap to drain and release: the error snapshot's primary read path is the buffered copy, and reads of it are non-consuming (`ErrorBodyBufferTests.The_buffered_copy_is_replayable`, `ErrorBodyPreviewTests.Reading_the_error_body_twice_yields_the_same_bytes_and_GetErrorAsync_still_works`); the body-log preview stays byte-capped and non-consuming (`invariant:xcut-24.preview-capped-non-consuming`). Amendment N11-1; the `first-release.md` decline is struck. |
| `SEAM-28` | MAY | 🚫 declined (supersedes the phase 2b row, which left the wiring for 4c) | 6.3, 6.5 | `OperationDescriptor.OperationId` is carried and never reaches the URL, headers or body (`OperationDescriptorTests.The_operation_id_never_reaches_the_url_headers_or_body`); attaching it to the context chain was planned for 4c and is not built, because nothing reads it (no `CallKey` on `RequestOptions`, no SDK reader; ledger scan N19, design §11 item 30). A MAY, declined in `docs/first-release.md`; adding the carrier later is additive. |
| `OBS-28` | SHOULD | ✅ in part (supersedes the phase 5c row); 🚫 the byte-count milestones (design §10 entry 23) | 6.3, 6.5 | The span vocabulary is built (`OperationEventTests`, `RetryTraceEventsTests`, `OperationSpanLifecycleTests`, unchanged); the request and response byte-count milestones are not emitted: the response count exists only after the consumer drains the body, after the attempt span has ended, and the transport's own milestones are the runtime's spans. A SHOULD, declined in `docs/first-release.md`. |
| `ASYNC-4` | MUST | N/A vacuous (supersedes the phase 8a row) | 6.3 | No interrupt is ever delivered on .NET (`Thread.Interrupt` and now `Thread.Abort` are banned in `src/`, `BannedSymbols.txt`; ledger scan N4); the phase 8a row read "🚫 vacuous", which the coverage tally counted as a failure: the clause is vacuous, as design §12 always said. |
| `RECOV-17` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryClassifierTests.An_HttpResponseException_decides_by_the_configured_set_alone`; `RetryRecoveryTests.A_non_retryable_error_status_passes_as_a_Success`. |
| `RECOV-18` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryResendGateTests.IsResendable_matrix`. |
| `RECOV-19` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryRecoveryTests.A_503_503_200_reaches_the_200`, `With_ErrorMappingStep_installed_nothing_is_mapped_twice` (every send is classified, P6a-6). |
| `RECOV-20` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryRecoveryTests.An_exhausted_503_surfaces_the_HttpResponseException_with_the_trail`, `The_total_timeout_aborts_on_elapsed_and_on_elapsed_plus_delay_and_clamps_the_delay`. |
| `RECOV-21` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryBackoffTests.Matches_every_vector`; `RetryRecoveryTests.A_pacing_hint_replaces_the_schedule_and_is_clamped_by_the_budget`. |
| `RECOV-22` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryPacingTests.Matches_every_vector`; `RetryRecoveryTests.A_pacing_hint_replaces_the_schedule_and_is_clamped_by_the_budget`. |
| `RECOV-23` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryPacingTests.The_parser_is_total`, `Retry_After_http_date_uses_HttpDate_and_a_past_date_is_zero`. |
| `RECOV-24` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryPacingTests.Matches_every_vector` (the precedence and grammar rows). |
| `RECOV-25` | SHOULD | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryPacingTests.X_RateLimit_Reset_is_an_epoch_with_positive_jitter` (P6a-19). |
| `RECOV-27` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryRecoveryTests.A_cancelled_wait_yields_Failure_OperationCanceledException_and_the_dispatcher_rethrows_it_signalled`. |
| `RECOV-28` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryRecoveryTests.The_dispatcher_and_RetryRecovery_hold_no_per_call_state`; `RetryEngineTests.Concurrent_calls_through_one_engine_do_not_share_state`. |
| `RECOV-29` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryEngineTests.A_throwing_pacing_read_degrades_to_no_hint_and_the_upstream_failure_is_still_the_one_surfaced`. |
| `RECOV-30` | SHOULD | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryBudgetEquivalenceTests`. |
| `RECOV-31` | MAY | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryPolicyEngineTests.The_attempt_header_stamps_a_one_based_ordinal_on_a_per_attempt_copy`; `RetryRecoveryTests.The_attempt_header_stamps_from_one_on_every_send` (one feature with `RETRY-38`, design §11 item 20). |
| `RECOV-34` | MUST | ✅ (hand-off from phase 4b to 6a, closed by the later phase's evidence) | 6.3 | `RetryOptionsTests` (validation, the defensive copy of the status set, equality). |
| `BODY-4` | MUST | ✅ (hand-off from phase 3b to 6a, 6b, 6c, closed by the later phase's evidence) | 6.3 | The three thirds: retry, `RetryResendGateTests.IsResendable_matrix`; redirect (declines loudly), the `REDIR-6:` rows of `RedirectDecisionMatrixTests` and `ReDriveLifecycleTests.A_redirect_over_a_non_replayable_body_throws_and_disposes_the_in_flight_response_once`; auth (declines by returning the original `401` undisposed), `AuthorizationPolicyChallengeTests.A_replacement_with_a_non_replayable_body_is_not_sent_and_the_original_401_is_returned_undisposed`. |
| `BODY-5` | MUST | ✅ (hand-off from phase 3b to 6a, 6b, closed by the later phase's evidence) | 6.3 | The retry-only idempotency gate, `RetryResendGateTests.IsResendable_matrix`; the redirect path re-sends a body-less request per redirect semantics and consults no idempotency, the matrix row `REDIR-3: AllowedMethods containing POST follows a body-less POST`. |
| `BODY-10` | MUST | ✅ (hand-off from phase 3a to 3b, closed by the later phase's evidence) | 6.3 | The exact-length copy is `HTTP-39`: `StreamCopyTests.CopyExactly_on_a_short_source_throws_EndOfStreamException_naming_both_numbers`, `ExactLengthBodyWireTests`; its consumers in 3b, `FileRequestBodyTests.A_file_that_shrinks_after_construction_fails_naming_transferred_of_total` and `SeekableStreamBodyTests.A_short_seekable_source_fails`. |
| `SEAM-15` | MAY | ✅ (hand-off from phase 2b to 8b, closed by the later phase's evidence) | 6.3 | `ObjectDisposedException` after dispose, owned and borrowed, both faces: `AfterDisposeTests`; kit `seam-15.after-dispose`. |
| `OBS-19` | SHOULD | ✅ (hand-off from phase 5b to 8b, closed by the later phase's evidence) | 6.3 | Event ids 110 to 113 in core's `DexpaceLogEvents` for the transport's dropped headers: `LogVocabularyTests`, `HeaderDropLogTests`, `InboundHeaderTests`. |

**Totals.** 53 rows: 42 MUST, 7 SHOULD, 4 MAY. 51 ✅ (of which 5 carry an unmet or declined clause: `SEAM-29`, `OBS-28`, `TRANSPORT-14`, `PAGE-21`, `AUTH-15`), 1 🚫 (`SEAM-28`, a declined MAY), 0 ⏳, 1 N/A (`ASYNC-4`): 51 + 1 + 1 = 53. After this checklist the
coverage script's tally of resolved MUST rows reads ⏳ 1 (`NFR-17`); it read 16 at `eb9b758`.

## Rows owned elsewhere and work on them

Work phase 11 did on rows another phase owns and this checklist leaves as they are (the header `ID (owner)` owns nothing, the 6a/6b/6c/8a convention).

| ID (owner) | What phase 11 supplies | Evidence |
|---|---|---|
| `HTTP-7` (2a) | `Request` refuses a `Uri` built with `DangerousDisablePathAndQueryCanonicalization`, whose CR/LF would reach the request line | `HeaderInjectionValidationTests.A_uri_created_without_canonicalization_is_rejected` (`Security`); `RequestTests.A_url_the_default_parser_made_is_accepted` |
| `XCUT-20` (10) | the two redaction backstops no longer swallow a fatal exception | `FatalFilterArchitectureTests` |
| `NFR-7` (10) | `CA1062` is `error` for `src/` and relaxed for applications only; the rationale and re-enable line follow the file's rule | `AnalyzerWaiverTests`; `.editorconfig` |
| `NFR-5`, `NFR-6` (10) | the gates stay blocking and the new ones join them: the dependency-audit self-test, the reproducible-pack self-test and the section-12 check are `CiWorkflowTests` rows | `CiWorkflowTests.Every_gate_of_the_gate_table_is_a_ci_step` |
| `ASYNC-3` (8a) | the cancel-without-interruption mode is a public helper, `WaitOrDisposeAsync` | `WaitOrDisposeTests` |
| `HTTP-47`, `OBS-11` (2a, 5b) | unchanged: a URL error still carries the redacted input | design §10 entry 29 |
| `CFG-5`, `CFG-6`, `CFG-7`, `OBS-35` (9) | reclassified, not changed: the accessor letter is met and the boot failure is a stricter host rule | design §10 entry 26, §11 item 72, amendment N11-8 |
| `PIPE-32`, `REDIR-25`, `PAGE-3`, `PAGE-29`, `PAGE-30` (4c, 6b, 7c) | re-audited and retained as 🚫 rows with their §10 entries (14, 17, 19); none is a counted clause | `docs/deviations.md` entries 14, 17, 19 |
| `TRANSPORT-28` (8b) | unchanged: the zero-copy SHOULD stays declined | `docs/first-release.md` |

## Tasks and commits

Six stacked pull requests were planned and land as one. A task with no commit is a verification. "Folded" means the task's change is in another task's commit, for the reason given.

| Task | Commit | Note |
|---|---|---|
| 0.1 pre-flight | none | the design's facts V10, V11 and V13 were re-verified for PR 5 by throwaway probes (V10: a 384 MB heap delta for the 8 000 000-line event where the design says 323 MB; same finding) |
| 1.1 reproducible-pack | `dde8715` | on hypothesis (a), without the CI log |
| 1.2 Core flake | `13fd56c`, `b748f11` | **applied** to two other flakes; the named `OperationSpanLifecycleTests` test never reproduced in more than 70 runs |
| 1.3 dependency allowance | `45ab21f` | |
| 1.4 fatal filter | `190a491` | |
| 1.5 `Disposal` | `5cdc4e4` | |
| 1.6 `CA1062`, `HttpResponseException` | `5fe97b4` | also fixed `RetryClassifierTests.Every_status_100_to_599_matches_the_classifier`, not on the plan's list |
| 2.1 `IOException` base | `331b389` | eight kit sites, not five; a new control and a shared trail renderer |
| 2.2 `ServiceResponseException` | `2c8c8ab` | |
| 2.3 `AsAsync` | `c26fcb1` | |
| 2.4 `WaitOrDisposeAsync` | `70d98d8` | |
| 2.5 `init` accessors | `ede4cd2`, `eb3facc` | 37 `ThrowIfNull(value)` sites and 3 other spellings; the merge added `PacingHeaders` to the table |
| 3.1 same stream | `502e398` | the response-logging wrapper keeps its single-use throw (`BODY-24`) |
| 3.2 inbound names | `2967f48` | |
| 3.3 special files | `1886dca` | |
| 3.4 non-canonical `Uri` | `82a21bb` | |
| 3.5 `Page` snapshot | `439ab66` | |
| 3.6 empty segments | `704f304` | |
| 4.1 native-retry filter | `bd51e81` | |
| 4.2 waiver scope | folded into 4.1 | convention 10: a stale waiver fails the run, so it goes in the change that fixes its clause |
| 5.1 `TraceIdGeneration` | `b77480a` | |
| 5.2 null log field | `341d540` | |
| 5.3 generator | `25cacdf` | the two levels of event 110 share one generated method with a `LogLevel` parameter (`SYSLIB1006`) |
| 5.4 empty value | `c741942` | |
| 5.5 `PacingHeaders` | `c287e7b` | |
| 5.6 SSE event cap | `6367632` | the optional `maxLineBytes` parameters became overloads (`RS0027`) |
| 5.7 attempt header | `848db52` | |
| 5.8 MD5 probe | `79958b9` | |
| 5.9 AOT smoke | `d885c3f` | `CheckPhase11ObservabilityAsync`, beside the other branch's `CheckPhase11Async` |
| 6.1 ledger | `5da9edd` | |
| 6.2 gate | `061e6e9` | |
| 6.3 this checklist | the commit that adds this file | |
| 6.4 to 6.9 | see `git log` | |

## What the audit found that no document recorded

Twelve defects, each a task with a test that failed first: the synchronous throw from an async bridge (F-A2, `ASYNC-2`); the response leak in the documented cancel-without-interruption mode (V5, `SEAM-30`); two
catch-alls that swallowed `OutOfMemoryException` (`UrlRedactor`); a span write on a non-recording activity (`Disposal`); a `NullReferenceException` and a non-error status accepted by a public constructor
(`HttpResponseException`); validating `init` accessors that reported `ParamName` `value`; a query splice that dropped empty segments; a `Page` that aliased the caller's list; an attempt header that was appended
and could be named `Idempotency-Key`; an MD5 probe that cached an unexpected exception type; an SSE event accumulated without bound (V10); and a `Request` that accepted a `Uri` whose CR/LF reaches the request line (V9).

## Counts

**The parity target.** The count of clauses unmet on a stated domain is **3** (`AUTH-15`, `PAGE-21`, `TRANSPORT-14`'s name half) against the roadmap's target of at most 5, from **7** that design §10 counted at `eb9b758`
(entries 16, 18, 24, 26, 31, 32 and 33). Entries 24, 31 and 33 were repaired, 26 is a stricter host rule that meets the accessor letter, and `LedgerAuditTests` rule L6 holds the ledger, its count line and
`docs/first-release.md` to the three. **Verdicts.** Of 33 §10 entries: 19 confirmed, 10 narrowed, 4 contradicted; of 68 §11 items: 49 confirmed, 13 narrowed, 6 contradicted; items 69 to 72 are added.
**Entries retired by repair:** 6, 7, 24, 28, 31, 33 (the plan names 24, 28, 31, 33; 6 and 7 lose their whole departure the same way).

## Deviations from the plan

1. **Order inside PR 6.** The `ci.yml` "Section 12" step and its `CiWorkflowTests` row land with the generated table (6.4), not with the gate (6.2), so no commit has a step that fails; and `docs/first-release.md`'s
   *Unsatisfied MUSTs* is rewritten in 6.2, because rule L6 reads it. The ⏳-owner rule of the coverage script lands here (6.3), with the rows it holds, so `--check` is green at every commit.
2. **A rule the plan did not know it needed.** Re-owning an ID whose earlier row is not an hourglass is reported by `requirement-coverage.cs` as a *carried* finding, so the plan's rows for repaired IDs could not
   be added; a later row that says "supersedes" is now accepted (a self-test case proves both directions).
3. **More rows than the plan's list,** found by generating §12 from the resolved marks (otherwise §12 lists them wrongly): five SHOULD/MAY hand-offs (`RECOV-25`, `RECOV-30`, `RECOV-31`, `SEAM-15`, `OBS-19`), `ASYNC-4`
   (a vacuous mark that read 🚫) and the declined `SEAM-28` and `OBS-28` (rows that read ✅).
4. **Entries 6 and 7 are marked retired** with 24, 28, 31 and 33 (the plan retires four); and **entry 20 has a retained block** (the design's retained table omitted it although the sanctioned divergence survives).
5. **Task 1.1** was done on hypothesis (a) without reading the CI log (a remote read); **task 1.2** was applied to two other flakes; **task 2.1** needed eight kit sites; **task 3.1** keeps `LoggingResponseBody`'s throw
   (`BODY-24`); **task 4.2** is folded into 4.1; **task 5.3**'s two levels share one generated method; **task 5.6** turned optional parameters into overloads; **task 5.9** has its own `CheckPhase11ObservabilityAsync`.
   Each is in the table above and in its commit message.

## Open items

Registered where their owner reads them (`docs/first-release.md`); listed here so the hand-off is complete.

- **The `main` ruleset (`NFR-17`).** An admin-only setting no phase can make; the line stays open in `docs/first-release.md`, naming this row alone.
- **The `reproducible packages` job.** Task 1.1 shipped a toolchain check and a self-test on hypothesis (a); whether the job is green on CI is unconfirmed, and if it is red its `reproducible-pack:` lines say which hypothesis
  was wrong.
- **`TarWriter` classification** (`BODY-11`) was verified on Linux only; the macOS CI row is the first to run it elsewhere.
- **About fifteen range and format validations inside `init` accessors** still report `ParamName` `value` (task 2.5 fixed the null-rejecting ones); a candidate for phase 12 or a later cleanup.
- **Roadmap decision D3** (webhook verification as a post-v1 package) is unchanged and the lead's.

## Styleguide audit

| Group | Result |
|---|---|
| Public surface | Every change is a reviewed `PublicAPI.Unshipped.txt` diff and `PublicAPI.Shipped.txt` stays empty (`NFR4ArchitectureTests`): added `TraceIdGeneration`, `RetryOptions.PacingHeaders`, `DexpaceConfigurationKeys.PacingHeaders`, `ResponseTaskExtensions.WaitOrDisposeAsync`, `ServerSentEventTooLargeException`, `ServerSentEventReader.DefaultMaxEventBytes` and the SSE overload sets; changed `ServiceRequestException`'s base type; removed `ServiceResponseException` and the optional `maxLineBytes` parameters (now overloads). The one-time public-surface list for phase 12 is in `docs/first-release.md`. |
| Nullability | No `!` added to `src/`; the new members are annotated and the accessors reject `null` with the property named. |
| Async | `ConfigureAwait(false)` on every new `await` in `src/` (`CA2007` is an error); the sync twins are real, not sync-over-async; `AsAsync` faults instead of throwing. |
| Disposal | `WaitOrDisposeAsync` closes an orphaned response exactly once; a second open returns the same handle and an open after dispose throws `StreamClosedException`; the SSE facade releases once on the new failure path. |
| Analyzers | 0 warnings; `CA1062` conformed for `src/`; no new `#pragma`; no analyzer relaxation added (`AnalyzerWaiverTests`); `MA0051` holds. |
| Tests | Every class carries a `[Trait]`; the phase adds three `Security` tests (`The_inbound_path_does_not_trim_a_name`, `A_uri_created_without_canonicalization_is_rejected`, `Data_lines_that_each_fit_but_never_end_the_event_fail_once_the_event_cap_is_passed`) and edits none (two comment lines of one `Security` class summary were rewritten; no assertion was touched). |
| Trim / AOT | The NativeAOT publish prints no warning and the smoke runs `CheckPhase11Async` and `CheckPhase11ObservabilityAsync`; the `PlaintextStreamFilter`, `TarWriter` and `LoggerMessage` generator are AOT-clean. |
| Styleguide conflicts | The `LoggerMessage` generator departure narrows to dotted keys; the `CA1062` departure retires; xUnit v3 on Microsoft.Testing.Platform is conformed (Shouldly is not adopted). The overlay is updated in task 6.5. |

## The Security tests

`git diff --stat main...HEAD -- '**/Security/*.cs'` shows additions only (the three tests above), apart from the two comment lines noted in the *Tests* row; no `Security` test was deleted or loosened.

## Gate results

Run on 2026-10-21 at `dc4bc0c` (the tip before this section was written), .NET SDK 10.0.401, Linux, from the repository root, in the order of `CLAUDE.md`'s *Build & test*.

| Gate | Result |
|---|---|
| `dotnet restore Dexpace.Sdk.sln --locked-mode` | ok |
| `dotnet build Dexpace.Sdk.sln --configuration Release` | 0 warnings, 0 errors |
| `dotnet format Dexpace.Sdk.sln --verify-no-changes` | exit 0 |
| `dotnet test --solution Dexpace.Sdk.sln --configuration Release` with coverlet | 7631 tests: 7608 passed, 0 failed, 23 skipped (the pinned `NotExercised` rows of the first-party invariant drivers and the permanent waivers: `transport-14.name-malformed-dropped` on three subjects and both faces, `transport-2.bodyless-not-resent` on the handler subject alone) |
| `coverage-gate.cs` (floor 80) and its self-test | aggregate 97.51%; self-test ok |
| `dotnet pack`, `dependency-audit.cs`, its self-test, `packaging-conformance.cs` | 5 packages; 5 libraries conform; self-test ok; packaging verdict GREEN (8 passed) |
| `requirement-coverage.cs --check` / `--check-section12` / `--report` / self-test | no finding in a MUST row; section 12's generated table is current; MUST rows N/A 22, **⏳ 1**, ✅ 505, 🚫 4 (532); SHOULD N/A 11, ⏳ 1, ✅ 85, 🚫 2; MAY N/A 2, ⏳ 3, ✅ 8, 🚫 1; self-test ok |
| `reproducible-pack.sh` and `--self-test` | 5 packages byte-identical across two checkouts at different paths; self-test passed |
| NativeAOT publish and run | no warning; `aot-smoke: all checks passed` |
| `tools/Dexpace.Tools.sln` build and test | 0 warnings; 438 passed |
| `scripts/knowledge verify-structure` | OK: 3346 harvested entries, 66 notes |
| housekeeping `probe` | no drift found |
| `Category=Security`, core tests | 316 passed; the diff against `main` is additions only (`HeaderInjectionValidationTests`, `ServerSentEventLineCapTests`) apart from the two comment lines noted above |
| `Category=Conformance`: core, SystemNet, DI tests | 50 passed / 1 skipped; 136 / 6; 119 / 7 |

**One red, then fixed.** The first full run failed `UntracedAllocationTests.The_untraced_path_does_not_compute_the_redacted_url` (short 665 B, long 890 B per call against a 200 B bound). It passed alone and in 18 of 21 full `Dexpace.Sdk.Core.Tests` runs;
the sibling tests of the class take the best of six rounds and this one took one reading. It now takes the best of six per URL (`dc4bc0c`, 0 failures in 30 runs afterwards). On `main` (`7919190`) the same project failed 2 of 10 runs in two other tests (`InstrumentationContextTests`, twice, and `RecordingTaskSchedulerTests`, which task 1.2 fixed in `b748f11`; `InstrumentationContextTests` did not recur in 21 runs on this branch and is not touched), so the Core suite's load sensitivity is not new;
whether CI's `build & test` rows hit it is for the first CI log to say.
