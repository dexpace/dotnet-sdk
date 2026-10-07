# Phase 5b — Logging and Redaction: Checklist

The execution-time checklist for sub-phase 5b of roadmap phase 5
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `65-phase-5b-logging` (issue #65), following the
[design](2026-10-07-phase5b-logging-design.md) and the [plan](2026-10-07-phase5b-logging.md). User page:
[`logging-and-redaction.md`](../../../../sdk-documentation/logging-and-redaction.md).

The scope is 28 rows: `OBS-1`-`OBS-20`, `OBS-24` and `OBS-34`-`OBS-40` (22 MUST, 6 SHOULD). 5c owns `OBS-21`-`OBS-23` and `OBS-25`-`OBS-33` (12); 28 + 12 = 40.
Every test below is `[Trait("Category", "Unit")]` and lives in `tests/Dexpace.Sdk.Core.Tests/` unless another place is named; phase 5b adds and edits no `Security`
class. The sub-phase was built in five steps on one branch, one commit each: the `InstrumentationPolicy` split (`chore:`), redaction and options,
the emitter, body level, and the close-out (smoke check, then docs). 5a and 5c were being built in parallel elsewhere; nothing of theirs was in this
tree, so PR 1 landed (not dropped) and `DexpaceClientOptions.Logging` is a `{ get; set; }` property on today's class (P5b-5).

**Red evidence.** For a new type or a changed signature the red was the compile error (plan convention 1). Tests were written before the production
code for the redaction vectors, the vocabulary, the options, the renderer, the cache, the emitter events, the guard, the allocation tests, the flow
tests, the catalogue, the body engagement and `Response.ReplaceBody` (each seen failing for the stated reason). They were written **after** the
production code for the `AttemptScope` split (task 1.1/1.2: the split is behaviour-preserving and the existing `InstrumentationPolicyTests` were the
red-to-green guard), for `LoggingResponseBody`'s sync `Snapshot` and logger parameter (needed by the emitter's compile, tested in task 4.2), for
`BodyPreviewRenderer` (written and first run together, all green) and for the logger doubles' self-tests. **Pins proven able to fail** (temporarily broken,
never committed): the guard's `catch` (`EmissionGuardTests.An_exception_whose_Message_throws_…` failed), state built before the level check
(`DisabledPathAllocationTests` failed three ways), the preview cap lifted (`BodyPreviewTests` failed both ways). **Pins not shown able to fail:**
`InstrumentationPolicyTests.The_default_url_full_tag_is_unchanged`, `ResponseReplaceBodyTests.WithBody_is_unchanged`,
`UrlRedactorHeaderValueTests.Existing_members_are_unchanged`, `DiagnosticContextFlowTests.No_SDK_source_suppresses_execution_context_flow`, and the
`BodyPreviewRendererTests` BOM and cut-multibyte facts.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `Diagnostics/{AttemptScopeTests,UrlRedactorHeaderValueTests,LogVocabularyTests,LogTextTests,HeaderLogRendererTests,RedactionCacheTests,HttpLogRecordTests,LogCatalogueTests,BodyPreviewRendererTests}.cs`;
`Configuration/{HttpLoggingOptionsTests,DexpaceClientOptionsTests}.cs`; `Pipeline/CallLoggerTests.cs`;
`Pipeline/Policies/{InstrumentationPolicyTests,HttpLogEmitterTests,EmissionGuardTests,DisabledPathAllocationTests,DiagnosticContextFlowTests,HttpLoggingDefaultsTests,BodyLoggingTests,BodyPreviewTests}.cs`;
`Http/Response/{ResponseReplaceBodyTests,LoggingResponseBodyTests}.cs`; `Internal/DisposalTests.cs`; `Pipeline/ReDriveLifecycleTests.cs`; `Support/LoggerDoublesTests.cs`;
vectors `tests/vectors/redaction/{header-values,body-previews}.json`; the AOT smoke check is `CheckHttpLoggingAsync` in `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`.
Doubles are in `tests/Dexpace.Sdk.TestSupport/Diagnostics/` (`RecordingLogger`, `ProviderLikeLogger`, `ThrowingLogger`, `DisabledLogger`, `RecordedLog`).

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `OBS-1` | MUST | ✅ (V3 held: the pipeline-delta form is in) | `AttemptScopeTests.Construction_allocates_nothing`, `The_redacted_url_is_not_computed_until_read_and_is_computed_once`; `DisabledPathAllocationTests.Every_emitter_method_allocates_nothing_at_None` (over `DisabledLogger`, `NullLogger`, `RecordingLogger`), `…_when_the_logger_disables_the_events` (`Headers` and `Body`), `A_pipeline_over_a_synchronous_transport_adds_zero_bytes_per_call_over_a_pass_through_policy`, `The_redacted_url_is_not_computed_on_the_disabled_path` (a 20 KB URL costs the same as a short one). The shared-inert-event conformance step has no referent (§10 entry 22) |
| `OBS-2` | MUST | ✅ | `HttpLogEmitterTests.At_Headers_a_request_and_a_response_event_are_emitted_at_Information`, `A_failure_emits_one_Warning_event_…`; `LogCatalogueTests.Every_recorded_event_belongs_to_the_catalogue` (every level is one of `Error`/`Warning`/`Information`/`Debug`) |
| `OBS-3` | MUST | ✅ (structured form; the rendered `(null)` is §10 entry 22's residual) | `LogVocabularyTests.No_key_or_event_name_is_empty_or_collides`; `HttpLogRecordTests.A_null_value_is_carried_as_null`; `LogCatalogueTests` (no empty key) |
| `OBS-4` | MUST | ✅ (duplicate-suppression vacuous, §10 entry 22) | `LogVocabularyTests.The_header_prefixes_end_with_a_dot_and_no_key_is_named_event`; `HttpLogRecordTests.No_key_is_named_event_and_none_is_empty`; `LogCatalogueTests` |
| `OBS-5` | MUST | N/A (§10 entry 22, `no-log-event-object`) | Precedence is the logging provider's; the SDK contributes one key set per event |
| `OBS-6` | MUST | ✅ | `HttpLogEmitterTests.SDK_owned_state_values_are_strings_ints_longs_doubles_or_null`; `EmissionGuardTests.An_exception_whose_Message_throws_does_not_break_the_failure_path` (a `ProviderLikeLogger` renders the exception inside `Log`) |
| `OBS-7` | SHOULD | ✅ | `LogTextTests` (exact limit, 8,193, surrogate pair, null/empty); `HeaderLogRendererTests.A_very_long_value_is_truncated_after_redaction`. `url.full` is cut by `LogText.Truncate` at every emission |
| `OBS-8` | MUST | N/A (§10 entry 22) | A log call is one call; there is no event instance to emit twice |
| `OBS-9` | MUST | N/A (§10 entry 22) | Global context is the host's (`BeginScope`, OpenTelemetry resource attributes) |
| `OBS-10` | MUST | N/A (§10 entry 23, `activity-as-tracing-model`) | The fold is `ActivityTrackingOptions`, a host allow-list; the user page recommends `TraceId \| SpanId`. Supporting pin: `DiagnosticContextFlowTests.The_attempt_span_is_current_when_http_request_is_emitted` |
| `OBS-11` | MUST | ✅ | Phase 1's `UrlRedactionDefaultDenyTests` (`Security`, unedited); `UrlRedactorHeaderValueTests.Userinfo_is_masked_on_every_route`, `Vectors_match` (`//user:secret@h/x`, the surgery cases and the backslash-opened authorities); `HttpLogEmitterTests.A_backslash_opened_authority_in_a_Location_never_logs_its_userinfo` |
| `OBS-12` | MUST | ✅ (configurable list) | `UrlRedactionDefaultDenyTests`; `RedactionCacheTests.A_with_copy_that_changes_AllowedQueryParameters_…`; `InstrumentationPolicyTests.The_url_full_tag_honours_the_calls_allowed_query_parameters`, `The_default_url_full_tag_is_unchanged` (pin) |
| `OBS-13` | MUST | ✅ | `UrlRedactionDefaultDenyTests` (`Security`, unedited) |
| `OBS-14` | MUST | ✅ | `UrlRedactionDefaultDenyTests` (`Security`, unedited) |
| `OBS-15` | MUST | ✅ | `UrlRedactionDefaultDenyTests.Malformed_url_text_yields_the_sentinel`; `UrlRedactorHeaderValueTests.The_malformed_sentinel_is_never_returned`, `The_method_is_total` |
| `OBS-16` | MUST | ✅ | `UrlRedactorHeaderValueTests.Vectors_match` (49 cases from `redaction/header-values.json`), `An_absolute_value_is_redacted_like_a_request_url`, `The_scheme_prefix_decides_not_IsAbsoluteUri` |
| `OBS-17` | MUST | ✅ | `HeaderLogRendererTests.A_url_valued_header_goes_through_RedactHeaderValue`, `A_second_location_value_meets_the_redactor_on_its_own`, `A_custom_URL_valued_name_is_honoured`; `HttpLogEmitterTests.A_url_valued_response_header_is_redacted_on_both_paths` (`SendAsync` and `Send`) |
| `OBS-18` | MUST | ✅ | `HeaderLogRendererTests.A_disallowed_header_is_REDACTED_by_default`, `…omitted_when_OmitDisallowedHeaders_is_true`; `HttpLoggingOptionsTests.The_default_header_allow_list_contains_no_credential_name`, `The_default_allow_list_is_the_26_names`; `HttpLoggingDefaultsTests.A_secret_header_value_never_appears_in_any_event_field_or_message` |
| `OBS-19` | SHOULD | ⏳ 8b | `TRANSPORT-13` owns the drop policy (phase-1 hand-off row 8b); ids 110-119 are reserved: `LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` |
| `OBS-20` | MUST | ✅ | `EmissionGuardTests` (all nine: a throwing `Log`, a throwing `IsEnabled`, a swallowed second failure, a fatal exception propagates, the call's own cancellation propagates, an unrelated `OperationCanceledException` is swallowed, a throwing `Message`, a throwing `ActivityListener`, a throwing `MeterListener`); `CallLoggerTests`; `LoggingResponseBodyTests.A_throwing_delegate_dispose_during_a_quiet_close_is_reported_to_the_supplied_logger`; `BodyLoggingTests.Cancellation_of_the_calls_token_during_the_preview_drain_propagates_and_disposes_the_response` |
| `OBS-24` | MUST | ✅ (by the runtime, pinned) | `DiagnosticContextFlowTests.A_caller_activity_and_scope_are_visible_at_the_response_event_after_the_transport_completes_on_another_thread`, `No_SDK_source_suppresses_execution_context_flow` (pin on `BannedSymbols.txt`) |
| `OBS-34` | MUST | ✅ | `HttpLoggingDefaultsTests.The_default_options_emit_no_http_event`, `Spans_and_instruments_still_record_at_None`, `Headers_level_logs_headers_only_and_never_a_body_preview`; `HttpLogEmitterTests.At_None_nothing_is_emitted_and_IsEnabled_is_not_consulted`; `BodyLoggingTests.At_Headers_and_None_no_wrapper_is_constructed`, `A_logger_that_disables_Information_constructs_no_wrapper_at_Body` |
| `OBS-35` | SHOULD | ⏳ 9 | Binding is the DI package's; the section name is its argument. An unrecognised level fails at startup (§10 entry 26; P5b-16). **V4 was not run** (the `Microsoft.Extensions.Configuration.Binder` package is not in the local cache and core must not reference it); phase 9's checklist owns that evidence |
| `OBS-36` | MUST | ✅ | `HttpLoggingOptionsTests.A_negative_preview_size_throws_and_an_oversized_one_clamps`; `BodyLoggingTests.At_Body_the_response_event_carries_a_preview_and_its_size`, `The_consumer_still_receives_every_byte_of_an_over_cap_body`, `A_zero_preview_size_captures_nothing_and_still_serves_every_byte`; `BodyPreviewTests.A_10_MB_body_with_a_small_cap_is_captured_in_the_cap_and_delivered_whole` |
| `OBS-37` | SHOULD | ✅ (stronger than the SHOULD: both paths, P5b-12) | `BodyLoggingTests.An_unknown_length_body_is_never_wrapped`, `A_text_event_stream_body_is_never_wrapped_even_when_it_declares_a_length` (each over `SendAsync` and `Send`) |
| `OBS-38` | SHOULD | ✅ | `BodyPreviewRendererTests.Vectors_match` (39 cases from `redaction/body-previews.json`), `Every_text_media_type_in_the_design_list_is_text`, `A_cut_multibyte_sequence_yields_the_replacement_character_and_does_not_throw`, `A_matching_BOM_is_stripped`, `The_renderer_is_total`; `BodyLoggingTests.A_binary_response_renders_the_size_only_marker` |
| `OBS-39` | MUST | ✅ | `LogVocabularyTests` (every name, id and key; the 16 keys equal the design's literals); `LogCatalogueTests` (headers and body level); `HttpLogEmitterTests.The_request_event_carries_method_url_resend_count_and_allowed_headers`, `The_response_event_carries_status_duration_and_response_headers`, `Body_size_keys_appear_only_for_a_declared_length`; `DisposalTests.The_suppressed_event_is_dexpace_dispose_suppressed_id_130_at_Warning` |
| `OBS-40` | SHOULD | N/A (§10 entry 22) | No reserved `event` key can collide, so the collision diagnostic has nothing to diagnose |

Totals: 28 rows, 22 MUST and 6 SHOULD (`OBS-7`, `OBS-19`, `OBS-35`, `OBS-37`, `OBS-38`, `OBS-40`). **21 ✅** (`OBS-1`-`OBS-4`, `OBS-6`, `OBS-7`, `OBS-11`-`OBS-18`, `OBS-20`, `OBS-24`,
`OBS-34`, `OBS-36`-`OBS-39`), **5 N/A** (`OBS-5`, `OBS-8`, `OBS-9`, `OBS-10`, `OBS-40`), **2 ⏳** (`OBS-19` → 8b, `OBS-35` → 9): 21 + 5 + 2 = 28. `OBS-11`, `OBS-13`, `OBS-14`
and `OBS-15` were fixed in phase 1 and are re-evidenced, not rebuilt. `docs/first-release.md` gets no entry: every SHOULD 5b owns is built or ⏳ to a named phase.

## Work on other owners' rows

These carry no exit mark in 5b; the owner's row cites 5b's tests.

| ID (owner) | Work done | Evidence |
|---|---|---|
| `XCUT-19`(c), (e) (phase 10) | Header logging default-deny; body logging off by default, no wrapper constructed | `HeaderLogRendererTests`, `HttpLoggingDefaultsTests`, `BodyLoggingTests.At_Headers_and_None_no_wrapper_is_constructed` |
| `XCUT-20` (phase 10) | The emission guard and the total header redactor; listener callbacks deliberately not wrapped (§11 item 37) | `EmissionGuardTests`, `UrlRedactorHeaderValueTests.The_method_is_total` |
| `XCUT-24` (phase 10) | Previews byte-capped and non-consuming by construction of 3b's wrappers, now engaged | `BodyPreviewTests` (10 MiB, 1 KiB cap, under 2 MiB allocated) |
| `BODY-34` (3b; ⏳ 5b clause closed) | Engagement only at `Body`, one shared preview size, the wrapper swap moves ownership and the exchange links | `BodyLoggingTests`, `HttpLoggingOptionsTests`, `ResponseReplaceBodyTests` |
| `RETRY-40`, `REDIR-18`, `REDIR-28` (6a, 6b) | The guarded emitter, the event-id ranges (140-159) and `CallState.Logger`; 5b emits none of their events | `CallLoggerTests`, `LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` |
| P3b-3, 4b hand-off (3b, 4b) | `Disposal` reaches the pipeline's logger from retry, redirect and the response wrapper | `ReDriveLifecycleTests` (retry, redirect, sync), `LoggingResponseBodyTests`, `DisposalTests` |
| `PIPE-11` (4c) | The policy cache keeps every policy field `readonly` (plan R3): the `volatile` slot lives in `RedactionCache` | `ConcurrencyTests.Every_shipped_policy_holds_only_readonly_instance_fields` (unedited) |
| `CTX-9`, `CTX-10` (4a) | The exchange link survives the response-body swap | `ResponseReplaceBodyTests.Exchange_links_move_to_the_new_response`; `BodyLoggingTests.Disposing_the_returned_response_closes_the_exchange_link_once` |
| `OBS-25`, `OBS-27`-`OBS-33` (5c) | None; `AttemptScope`'s lazy URL is what 5c's span tag reads | 5c's checklist |

## Existing assertions changed

| Test | Change | Why |
|---|---|---|
| `InstrumentationPolicyTests.ProcessAsync_LogsStructuredEvent_WithRedactedUrl` | Asserts nothing is logged at the default level, then sets `Logging.Level = Headers` and asserts every `url.full` carries `api_key=***` and no message holds the secret; its private `RecordingLogger` is replaced by the `TestSupport` one | Breaking 1 (R6) |
| `DisposalTests`, `LoggingResponseBodyTests`, `ReDriveLifecycleTests` | Additions only (id 130, keys, the logger parameter, retry/redirect reach the logger); no assertion changed | Breaking 4 |
| `TestContexts.For` | Gains an optional `ILogger? logger` | `PipelineContext.Create` takes the logger (task 3.2) |

No other existing assertion changed; the gate proves it.

## Unedited `Security` classes

All of them. Evidence: `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` prints nothing, and both
`Category=Security` runs pass. `git diff --stat main...HEAD -- src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt` prints nothing.

## Runtime facts (V1 to V7)

| Fact | Result | Consequence |
|---|---|---|
| V1 `CA1848` and a direct `ILogger.Log<TState>` | Did not fire. `CA1873` (an expensive argument) did, on `new HttpLogRecord(…)` passed inline | The record is built into a local first; no `CA1848` pragma. `CA1727` (PascalCase placeholders) fires on the dotted `Define` templates and carries a scoped, justified suppression in `Disposal` and `HttpLogEmitter` |
| V2 `Uri.TryCreate("/cb?code=x", RelativeOrAbsolute)` on Linux | Either result is correct: the scheme-prefix classification does not consult it | `UrlRedactorHeaderValueTests.The_scheme_prefix_decides_not_IsAbsoluteUri` |
| V3 instruments allocate nothing without a listener | Held (and `Histogram.Enabled`/`UpDownCounter.Enabled` guard the tag lists, which would otherwise box the status code) | The pipeline-delta test is in, not skipped |
| V4 binder enum conversion | **Not run**: binder package unavailable offline | Phase 9 |
| V5 `with` copies private fields | Language-certain; the cache is keyed by reference and is not a field of the record | `RedactionCacheTests.A_with_copy_that_changes_AllowedQueryParameters_…`, `HttpLoggingOptions_has_no_derived_instance_field` |
| V6 `Encoding.UTF8.GetString([0xE2, 0x82])` | `"�"`, no throw | Permanent test `A_cut_multibyte_sequence_…` |
| V7 throwing `ActivityListener` / `MeterListener` | Both propagate out of the SDK call | `EmissionGuardTests.A_throwing_ActivityListener_…`, `A_throwing_MeterListener_…` |

The phase-start queries (`--origin note`, `--section conflicts`, `--prefix-info OBS`, `--gaps OBS`) were run: eight notes now (seven before this phase's), no OBS conflict,
`OBS` 40 of 40 substantive, 0 gaps. One note was added: `docs/knowledge/notes/observability.md`.

## Styleguide audit

| Group | Verdict |
|---|---|
| 6.1 structured templates | Met: named placeholders only; the HTTP events carry structured key/value state, never interpolation |
| 6.2 source-generated `LoggerMessage` | **Departed** (P5b-3): `ILogger.Log<TState>` for the header-bearing events, `Define` for the fixed-key ones; knowledge note and overlay row added |
| 6.3 levels, exception attached | Met: `Information` milestones, `Warning` recoverable anomalies, the exception object is `ILogger`'s exception argument |
| 6.4 correlation via `Activity` | Met: the attempt `Activity` starts before `http.request`; `ActivityTrackingOptions` is the host's |
| 6.7 redact before the sink | Met: URL, header and body-preview handling happen before `Log` is called; defaults deny |

## Deviation ledger as built

| ID | As built |
|---|---|
| P5b-1 | The 28/12 split stands; the 5c design on disk claims exactly the other 12 and hands `OBS-24` to 5b |
| P5b-3 | `Log<TState>` plus an internal key/value record for `http.request`/`http.response`; `Define` for the rest; styleguide 6.2 departure recorded (§8.1 dated correction, knowledge note, overlay row) |
| P5b-4 | `Information` and `Warning`; recorded on the user page and in the §8.1 correction |
| P5b-5 | `HttpLoggingOptions` is a sealed record; `DexpaceClientOptions.Logging` is `{ get; set; }` (null rejected) because 5a was not merged |
| P5b-6 | The pipeline's logger is its `InstrumentationPolicy`'s, resolved in the `HttpPipeline` constructor (plan R4), carried as `CallState.Logger` |
| P5b-7 | Vocabulary as public `const`s; `*.body.size` is the declared length, `*.body.preview.size` the capture |
| P5b-12 | Eager snapshot before `http.response`, skipped for unknown-length and `text/event-stream` bodies on both paths |
| P5b-13 | Internal `Response.ReplaceBody`; `WithBody` unchanged (pinned) |
| P5b-16 | `OBS-35` ⏳ 9; V4 not run |
| P5b-20 | PR 1 landed (5c had not merged a split): `AttemptScope`, `AttemptTelemetry` as an `internal struct` (plan R1), `HttpLogEmitter`; the `MA0051` waiver is retired |

## Plan departures worth knowing

- `AttemptScope` is a mutable struct passed by `ref` (plan R8, already proposed to the lead as a correction to design position A). Because the response preview is
  awaited, the async completion takes the scope **by value** (an async method cannot hold a `ref`); the redacted URL is cached in it by then whenever anything
  consumed it. `Stop()` freezes the attempt's elapsed time when the response (or failure) arrives, so `http.response.duration_ms` and the duration histogram agree
  and exclude the preview drain.
- The two instruments are `private static readonly` fields of `AttemptTelemetry`, not `internal static readonly` fields of `InstrumentationPolicy` (plan task 1.2): nothing
  outside `AttemptTelemetry` records into them. 5c moves them into its `HttpClientMetrics` regardless.
- `HttpLogEmitter` is split in two files (`HttpLogEmitter.cs`, `HttpLogEmitter.Body.cs`) and a `RequestLog` struct (the request to send, the tap, the options and whether
  the events are enabled) and a `ResponseCapture` struct carry state between the request and response halves; the plan named only the emitter methods. The policy calls
  the request half **inside** its `try`/`finally`, so a fatal exception or a cancellation that propagates out of a throwing logger still ends the activity and
  decrements `http.client.active_requests`.
- The request tap is built when `Information` **or** `Warning` is enabled at `Body` (so the failure event can carry the request preview when only `Warning` is on);
  `IsEnabled(Information)` is consulted once per attempt (`HttpLogEmitterTests.A_logger_that_disables_Information_receives_nothing_and_is_asked_once_per_attempt`).
- `Disposal`'s `error.type` is the exception's full type name (the plan's key table said `error.type`; the earlier event used the short name).
- `HttpLoggingOptions` uses the C# 14 `field` keyword for its validating accessors; `ImmutableArray<string>` backs the three collections.
- `DiagnosticContextFlowTests.No_SDK_source_suppresses_execution_context_flow` walks up from the test binary to find `BannedSymbols.txt`.
- The disposal-reaches-the-logger tests for retry and redirect live in `ReDriveLifecycleTests` (which already holds their helpers and the throwing-dispose body), not in
  `RetryPolicyTests` and `RedirectPolicyTests`.
- `EmissionGuardTests.An_exception_whose_Message_throws_…` matches the thrown exception by hand: xUnit's own `ThrowsAsync<T>` reads `Message` while matching, which is the
  very getter under test.
- The pipeline-delta allocation test takes the best of six 1,000-call rounds after a 500-call warm-up for each pipeline: with the plan's three warm-up calls the instrumented
  pipeline measured about 100 bytes a call above the pass-through one, which was JIT and type-loading, not steady state.
- `application/csv` is not in the text set (the design's list does not name it; Ruby's does); `text/csv` is text through `text/*`. The vector file lists the cases it
  does not port: Ruby's stubbed-policy cases, the nil/Integer stringification cases and the raw-byte (`.b`) cases. One header vector departs from Ruby on purpose:
  `/http://user:secret@h/p` masks the userinfo (the `//` scan is unconditional, `OBS-11`), where Ruby's parser accepted it as a path.
  Likewise `\\user:secret@h/p` is masked, not written back as given: `System.Uri` and `RedirectPolicy` read a backslash as a slash, so any `/` or `\` pair opens an authority.
- **Provenance of the ported cases.** `ruby-sdk@5b17395` (`redactor_test.rb`, `preview_test.rb`) and `nodejs-sdk@54aeed4` (`redaction.test.ts`, `logging-step.test.ts`)
  were read from the local clones; each vector case names its origin in `note`.
- The coverage gate and its self-test, the dependency audit, the reproducible pack, the tools solution and the housekeeping probe were run at the close-out
  (results in the hand-over report).
