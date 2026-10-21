# Deviations

The as-built audit of design §10 (the deviation ledger) and §11 (the reference-ambiguity resolutions), and the collection point for deviations found outside a phase. It is the second of this
repository's two registers: [`first-release.md`](./first-release.md) holds what the release must know, this file holds whether the design's claims are true of the code. Roadmap phase 11 created it
([design](./work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation-design.md), [plan](./work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation.md),
[checklist](./work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation-checklist.md)). A judgment, not a mechanical append: a row changes only with a verdict a human has read against the source.

**The numbering belongs to design §10.** Entries keep their numbers (a repaired entry is marked *retired*, never renumbered), and this file is renumbered with §10 in the same commit if §10 ever is.
`LedgerAuditTests` (`tests/Dexpace.Sdk.Conformance.Tests/Gates/`) fails a missing or extra number.

**Audit commit.** `eb3facc`, the merged phase 11 tip when the evidence below was re-derived: every `path:line` in this file is a line of that tree, checked to exist by the gate (rule L3), and the
later commits of the phase touch documents, the ledger gate, `BannedSymbols.txt`, two scripts and `ci.yml` only. A claim of the design that the source contradicts is corrected in design §10 by a dated
correction, never by editing the entry (P11-3). The review fixes made after that commit moved some cited lines; each citation they moved was re-derived against the tree of the fix, by hand, because rule L3 checks that a line exists and not what it holds.

**The counting rule (P11-2).** A clause counts against the roadmap's parity target ("at most five clauses unsatisfied or unmet on a stated domain, each argued") when the **observable outcome** the
clause protects is not delivered on some stated domain. A **mechanism substitution**, where the letter names a mechanism the port replaces and the protected outcome is delivered or strengthened, is
retained and argued below but not counted. A clause the specification sanctions changing (`REDIR-25`, `BODY-8`, `SSE-19`) is never counted, and neither is one the specification makes
unsatisfiable (`AUTH-16`/`AUTH-17`/`AUTH-22`, design §11 item 62), which is an ambiguity.

Unmet clauses: **4** of at most **5**. They are `AUTH-15` on a host whose crypto refuses MD5 (entry 16), `PAGE-21` for percent-encoded unreserved characters (entry 18), `TRANSPORT-14`'s malformed
header *name* (entry 32) and `CFG-2` for a section read from a configuration other than the container's registered root (entry 25); `docs/first-release.md` *Unsatisfied MUSTs* lists the same four, and rule L6 holds the two registers and this line to one another.
The fourth was found by the review after the audit commit: the first count wrote entry 25 off as repaired, but the repair needs the providers of the root the section came from, which no public API reaches from an `IConfigurationSection`, so for a section of any
other configuration the observable outcome (an empty value does not hide the lower layer) is still not delivered, and the counting rule counts it.

**Reading the tables.** *Status* is one of `as-built: confirmed` (the code does what the entry says), `as-built: narrowed` (true, but broader or narrower than the code), `as-built: contradicted → doc corrected`
(the code does otherwise and the document is corrected) or `retired (repaired, task n.m)` (the code was changed so the departure no longer exists). *Disposition* is `retain`, `repaired` or `doc-corrected`,
with `, unmet-clause: <ID>` on a retained row that is one of the counted clauses and an optional `(+…)` naming a repair made on the way. *Reason* is a class from the closed set `platform`, `security`,
`aot-trim`, `measured-cost`, `styleguide`, `spec-sanctioned`, or `—`; "already built", "easier" and "a lot of work" are not reasons. *Evidence* is `path:line` citations and test names (`Class.Method`).

## The audit

| # | Title | IDs touched | Status | Disposition | Reason | Evidence |
|---|---|---|---|---|---|---|
| 1 | Core takes a runtime dependency on the logging facade | `SEAM-1`, `NFR-1` | as-built: narrowed | retain (+repaired: the audit's allowance is a fixed set, task 1.3) | spec-sanctioned | The runtime closure is the facade 10.0.12 and `DependencyInjection.Abstractions` 10.0.12 only, no `DiagnosticSource` (`src/Dexpace.Sdk.Core/packages.lock.json:23`), not the wider net8.0 closure the entry measured; the audit allows exactly those two (`scripts/ci/dependency-audit.cs:124`) and fails a third (`scripts/ci/dependency-audit-selftest.sh:98`); the audit is a CI script, not a `deps.json` test (`tests/Dexpace.Sdk.Core.Tests/Architecture/Seam1ArchitectureTests.cs:24` defers to it) |
| 2 | The DI package's hosting family counts as one library | `NFR-2` | as-built: confirmed | retain | platform | One third-party root, `Microsoft.Extensions.Http`, with a 13-package band-matched closure (`src/Dexpace.Sdk.Extensions.DependencyInjection/packages.lock.json:23`); the audit's policy record names it (`scripts/ci/dependency-audit.cs:53`) |
| 3 | The byte-stream provider seam is retired; its contract is not | `SEAM-3..10`, `IO-30..36`, `IO-39`, `XCUT-23` | as-built: confirmed | retain | platform | Every `IO/` type is `internal` (`src/Dexpace.Sdk.Core/IO/CapturedBytes.cs:22`, `src/Dexpace.Sdk.Core/IO/StreamCopy.cs:23`, `src/Dexpace.Sdk.Core/IO/TeeStream.cs:31`); scan N1 finds no `Source`/`Sink`/`Buffer` vocabulary in `src/` |
| 4 | End of stream is `Stream.Read` returning 0 | `IO-1`, `IO-2`, `IO-11`, `IO-15..18`, `BODY-10`, `BODY-25` | as-built: confirmed | retain | platform | Every read asks for at least one byte (`src/Dexpace.Sdk.Core/IO/StreamCopy.cs:45`); a known length is checked (`src/Dexpace.Sdk.Core/IO/StreamCopy.cs:104`); `ZeroCountReadRuleTests` (`tests/Dexpace.Sdk.Core.Tests/IO/ZeroCountReadRuleTests.cs:20`) scans for a zero-count read |
| 5 | One stream-ownership rule for request bodies | `BODY-8`, `IO-6` | as-built: confirmed | retain | spec-sanctioned | A request body closes what it opened (`src/Dexpace.Sdk.Core/Http/Request/FileRequestBody.cs:101`); `FromStream` never disposes the caller's stream (`src/Dexpace.Sdk.Core/Http/Request/RequestBody.cs:360`); the logging tee leaves the destination open (`src/Dexpace.Sdk.Core/Http/Request/LoggingRequestBody.cs:92`); `ResponseBody.FromStream` takes ownership (`src/Dexpace.Sdk.Core/Http/Response/ResponseBody.cs:218`) |
| 6 | A second open throws instead of returning the same handle | `BODY-14`, `HTTP-41` | retired (repaired, task 3.1) | repaired | — | A second open returns the first handle in both forms (`src/Dexpace.Sdk.Core/Http/Response/ResponseBody.cs:331`, `src/Dexpace.Sdk.Core/Http/Response/ResponseBody.cs:272`, `src/Dexpace.Sdk.Http.SystemNet/HttpResponseMessageBody.cs:45`); an open after dispose throws `StreamClosedException`; `ResponseBodyLifecycleTests.A_second_open_returns_the_same_stream`, `HttpResponseMessageBodyTests.A_second_open_returns_the_same_stream`; the transport's handle reports its own disposal, so a second `GetError*` over a wire response throws `ResponseNotReadException` (`ResponseBodyReopenWireTests`, `src/Dexpace.Sdk.Http.SystemNet/DisposalAwareStream.cs:19`); the first open runs under one gate for both forms, so concurrent first opens are handed one stream (`HttpResponseMessageBodyOpenRaceTests`, `ResponseBodyReopenWireTests.Concurrent_first_opens_over_real_responses_agree`). The logging wrapper keeps its own `BODY-24` throw for a read past a finished capture (`src/Dexpace.Sdk.Core/Http/Response/LoggingResponseBody.cs:353`), which is that clause, not this one |
| 7 | Transport failures are not members of an I/O-error family | `TRANSPORT-20`, `XCUT-4`, `RETRY-2`, `RETRY-4`, `RECOV-17` | retired (repaired, task 2.1) | repaired | — | `ServiceRequestException` derives from `IOException` and keeps `IRetryableError` (`src/Dexpace.Sdk.Core/Errors/TransportExceptions.cs:23`, `src/Dexpace.Sdk.Core/Errors/TransportExceptions.cs:56`); the mapping chains the native exception (`src/Dexpace.Sdk.Http.SystemNet/SendFailures.cs:54`) and names an ended response truthfully (`src/Dexpace.Sdk.Http.SystemNet/SendFailures.cs:86`); the classifier reads `IRetryableError` and the cause chain (`src/Dexpace.Sdk.Core/Resilience/RetryFacts.cs:106`); `TransportExceptionTaxonomyTests.ServiceRequestException_is_an_IOException`; `ServiceResponseException` is removed (scan N3) |
| 8 | Cancellation is a cooperative token; interrupts have no counterpart | `TRANSPORT-3`, `XCUT-1`, `ASYNC-3`, `ASYNC-4`, `ASYNC-14`, `SEAM-14`, `SEAM-18`, `PIPE-33`, `PIPE-34`, `XCUT-13`, `CFG-20`, `CFG-21` | as-built: narrowed | retain (+repaired: `AsAsync` faults on rejection, `WaitOrDisposeAsync`, tasks 2.3 and 2.4) | platform, security | The bridge takes a scheduler with no default (`src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs:103`), faults the task when the scheduler rejects the work (`src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs:171`) and returns a cancelled task for a signalled token (`src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs:158`); the cancel-without-interruption mode is the public `WaitOrDisposeAsync` (`src/Dexpace.Sdk.Core/Client/ResponseTaskExtensions.cs:42`, `src/Dexpace.Sdk.Core/Internal/LateResult.cs:42`); `Thread.Interrupt` is banned (`BannedSymbols.txt:10`); the transport replaces the native exception when a deadline is armed or the cancel is internal (`src/Dexpace.Sdk.Http.SystemNet/SendFailures.cs:54`), so "rethrown untouched" is narrower than the entry says; `SyncToAsyncBridgeTests.A_scheduler_that_rejects_the_work_faults_the_returned_task_instead_of_throwing`, `WaitOrDisposeTests.Every_caller_gets_a_live_response_or_a_cancellation_and_every_produced_response_is_closed_once`; scan N4 |
| 9 | No provider registry or auto-discovery: explicit construction, plus single-registration validation | `SEAM-5..10`, `XCUT-23` | as-built: narrowed | retain | aot-trim | Identity-checked slots throw at `UseTransport`/`UseSerde` (`src/Dexpace.Sdk.Extensions.DependencyInjection/ServiceSlot.cs:25`) and the validator scans the keyed descriptors (`src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientValidator.cs:34`); `TryAdd` is used nowhere, so "`TryAdd` gives `SEAM-6`" is the document's error; scan N2 finds no discovery |
| 10 | No builder objects beyond the multimap builders, and no `IBuilder<T>` | `SEAM-29`, `HTTP-2`, `HTTP-3`, `HTTP-4` | as-built: narrowed | retain (+repaired: validating accessors name their field, task 2.5) | styleguide | The public `*Builder` types are four, none of them a model builder (`src/Dexpace.Sdk.Core/Pipeline/PipelineBuilder.cs:36`, `src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientBuilder.cs:22`, plus `Headers.Builder` and `Query.Builder`); a validating accessor throws with `ParamName` set to the property (`src/Dexpace.Sdk.Core/Operations/OperationDescriptor.cs:43`); `InitAccessorParamNameTests.Every_validating_accessor_in_the_libraries_names_its_property`; scan N5. Open: about fifteen range and format checks inside accessors still report `ParamName` `value` |
| 11 | Reflection and open body subclassing can bypass construction-time validation | `HTTP-2`, `SEAM-29`, `XCUT-15` | as-built: confirmed | retain | platform | The wire re-checks every header (`src/Dexpace.Sdk.Http.SystemNet/OutboundHeaderMapper.cs:194`) and the method token (`src/Dexpace.Sdk.Http.SystemNet/OutboundHeaderMapper.cs:76`) because `TryAddWithoutValidation` writes CR/LF raw |
| 12 | The orchestrator catches every non-fatal exception | `RECOV-2`, `RECOV-8`, `RETRY-25`, `PIPE-30` | as-built: contradicted → doc corrected | doc-corrected (+repaired: two catch-alls swallowed `OutOfMemoryException`, task 1.4) | platform | The filter sits at six recovery sites (`src/Dexpace.Sdk.Core/Recovery/RecoveryDispatcher.cs:156`, `src/Dexpace.Sdk.Core/Recovery/ResponseRecoveryChain.cs:127`) among many filtered catch-alls, not "exactly four"; two catch-alls in `UrlRedactor` (`src/Dexpace.Sdk.Core/Diagnostics/UrlRedactor.cs:100`) had no fatal filter; `FatalFilterArchitectureTests.No_catch_all_in_a_runtime_library_swallows_a_fatal_exception` (`tests/Dexpace.Sdk.Core.Tests/Architecture/FatalFilterArchitectureTests.cs:16`) now holds every catch-all; scan N20 |
| 13 | Suppressed exceptions are an SDK-owned trail, stored in `Exception.Data` on foreign exceptions | `RECOV-12`, `RETRY-34`, `PAGE-13`, `SSE-29`, `SSE-36` | as-built: confirmed | retain | platform | `ExceptionTrail.AddSuppressed` and `GetSuppressed` (`src/Dexpace.Sdk.Core/Errors/ExceptionTrail.cs:28`, `src/Dexpace.Sdk.Core/Errors/ExceptionTrail.cs:67`); `ExceptionTrailTests.A_secondary_added_to_a_foreign_exception_lands_in_GetSuppressed`; scan N6 finds no `AggregateException` |
| 14 | The async standard pipeline follows redirects, as the sync one does | `PIPE-32`, `REDIR-25` | as-built: confirmed | retain | spec-sanctioned | One `RedirectPolicy` in the preset (`src/Dexpace.Sdk.Core/Pipeline/PipelineBuilder.cs:222`); the SDK-built transport turns the runtime's follow off (`src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs:516`) |
| 15 | No cross-origin marker: redirect and auth compare against the immutable seed origin | `REDIR-11`, `AUTH-29`, `REDIR-8`, `XCUT-17` | as-built: confirmed | retain | security | The seed origin is taken once (`src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectChain.cs:25`); the authorization policy judges against it (`src/Dexpace.Sdk.Core/Pipeline/Policies/AuthorizationPolicy.cs:154`); the one `PipelinePropertyKey` left is the idempotency key (`src/Dexpace.Sdk.Core/Pipeline/Policies/IdempotencyPolicy.cs:42`); scan N7 |
| 16 | Digest MD5 only where the platform crypto provides it | `AUTH-15`, `AUTH-16` | as-built: confirmed | retain, unmet-clause: AUTH-15 (+repaired: the probe treats any non-fatal failure as refused, task 5.8) | security | One probe per process (`src/Dexpace.Sdk.Core/Auth/Md5Availability.cs:22`) that counts every non-fatal failure as a refusal (`src/Dexpace.Sdk.Core/Auth/Md5Availability.cs:31`); the handler drops MD5 from its preference (`src/Dexpace.Sdk.Core/Auth/DigestChallengeHandler.cs:94`); `Md5AvailabilityTests.An_unexpected_probe_failure_means_refused`; scan N8 finds no vendored MD5 |
| 17 | Pages are materialized values; no page owns a live response | `PAGE-1`, `PAGE-3`, `PAGE-12`, `PAGE-15`, `PAGE-34` | as-built: narrowed | retain (+repaired: `Page` copies its values, task 3.5) | platform | The step closes the response before it yields (`src/Dexpace.Sdk.Core/Pagination/PageStep.cs:30`, `src/Dexpace.Sdk.Core/Pagination/FetcherPageable.cs:28`); `Page` stores an immutable snapshot of the list it is given (`src/Dexpace.Sdk.Core/Pagination/Page.cs:32`); `PageTests.Values_is_a_snapshot_of_the_list_passed_in` |
| 18 | The query splice is verbatim relative to `System.Uri`'s canonical form | `PAGE-21`, `PAGE-24` | as-built: narrowed | retain, unmet-clause: PAGE-21 (+repaired: empty segments and the `Uri` check, tasks 3.4 and 3.6) | security | The splice copies an empty segment (`src/Dexpace.Sdk.Core/Pagination/QuerySplice.cs:115`), `QuerySpliceTests.Stray_empty_segments_are_copied_verbatim`; `Request` refuses a `Uri` built without canonicalization (`src/Dexpace.Sdk.Core/Http/Request/Request.cs:218`), `HeaderInjectionValidationTests.A_uri_created_without_canonicalization_is_rejected`; the residual is RFC 3986 §6.2.2.2 equivalence of unreserved characters |
| 19 | One pull-based paging engine: no executor mode and no rejection path | `PAGE-29`, `PAGE-30` | as-built: narrowed | retain | platform | One engine (`src/Dexpace.Sdk.Core/Pagination/StrategyPageable.cs:20`) and no scheduler in any factory; a consumer with no `SynchronizationContext` resumes inline, so `await Task.Yield()` is the way to move; scan N9 |
| 20 | SSE lines longer than a cap fail the read | `SSE-19` | as-built: confirmed | retain (+repaired: the per-event cap closes residual R1, task 5.6) | spec-sanctioned | A line over `src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventReader.cs:50` fails with `src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventLineTooLongException.cs:32`; an event whose data passes `src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventReader.cs:56` fails with `src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventTooLargeException.cs:26` (`src/Dexpace.Sdk.Core/ServerSentEvents/EventAccumulator.cs:107`); `ServerSentEventLineCapTests.Data_lines_that_each_fit_but_never_end_the_event_fail_once_the_event_cap_is_passed` |
| 21 | `Tristate<T>` is an invariant struct, so the covariance SHOULD cannot be met | `SERDE-14` | as-built: confirmed | retain | platform | `src/Dexpace.Sdk.Core/Serialization/TristateOfT.cs:29`; variance exists only on interfaces and delegates |
| 22 | No structured log-event object; `ILogger` delivers the guarantees | `OBS-1`, `OBS-3`, `OBS-4`, `OBS-5`, `OBS-8`, `OBS-9`, `OBS-40` | as-built: narrowed | retain (+repaired: a null field is the literal `null`; the plain-name events use the generator, tasks 5.2 and 5.3) | platform | The header-bearing events go through a record that maps a null value to `null` (`src/Dexpace.Sdk.Core/Diagnostics/HttpLogRecord.cs:35`, `src/Dexpace.Sdk.Core/Diagnostics/HttpLogEmitter.cs:221`); the fixed-key events keep `LoggerMessage.Define` for dotted keys (`src/Dexpace.Sdk.Core/Diagnostics/RedirectLog.cs:28`) and the plain-name ones use the generator (`src/Dexpace.Sdk.Http.SystemNet/HeaderDropLog.cs:123`); `HttpLogRecordTests.A_null_value_is_emitted_as_the_literal_null`; `DisabledPathAllocationTests` (`tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/DisabledPathAllocationTests.cs:27`) |
| 23 | Tracing is `System.Diagnostics.Activity`; its no-op is `null` | `OBS-25`, `OBS-21`, `OBS-23`, `OBS-10`, `OBS-28` | as-built: narrowed | retain (+repaired: `Disposal` writes only to a recording activity, task 1.5) | platform | Every SDK tag, event and status write is guarded (`src/Dexpace.Sdk.Core/Diagnostics/OperationTelemetry.cs:43`), and so is the one that was not (`src/Dexpace.Sdk.Core/Internal/Disposal.cs:106`); the context carries an optional activity (`src/Dexpace.Sdk.Core/Execution/InstrumentationContext.cs:73`); `DisposalTests.A_suppressed_dispose_failure_is_not_recorded_on_a_non_recording_activity` |
| 24 | Only the W3C trace-id flavour and the invalid sentinel | `OBS-27` | retired (repaired, task 5.1) | repaired | — | `TraceIdGeneration.W3C`, `Datadog` and `FormatDatadog`, each re-drawing a zero draw (`src/Dexpace.Sdk.Core/Diagnostics/TraceIdGeneration.cs:34`); the SDK still never sets the process hook (`BannedSymbols.txt:130`); `TraceIdGenerationTests.Datadog_ids_have_a_zero_high_half_a_non_zero_low_half_and_render_as_a_decimal_ulong` |
| 25 | Layered lookup is `Microsoft.Extensions.Configuration` in the DI package | `CFG-1..4`, `CFG-10`, `CFG-13`, `CFG-14`, `CFG-24` | as-built: narrowed | retain, unmet-clause: CFG-2 (+repaired: an empty value falls through for a section of the registered configuration, task 5.4) | platform, styleguide | When the registered configuration is the root that holds the section, the reader walks its providers from the top and takes the first non-blank value (`src/Dexpace.Sdk.Extensions.DependencyInjection/ConfigSection.cs:51`, `src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientStateSetup.cs:27`, `src/Dexpace.Sdk.Extensions.DependencyInjection/ConfigurationReader.cs:45`); `EmptyValueTests.An_empty_value_falls_through_to_the_lower_provider`; for a section of any other configuration the merged read stands, where an empty value in the highest provider hides the lower one (`src/Dexpace.Sdk.Extensions.DependencyInjection/ConfigSection.cs:84`), `EmptyValueTests.A_section_from_an_unregistered_configuration_keeps_the_merged_read`; no property tier, raw accessor or global slot (scan N10) |
| 26 | Unparseable configuration fails at startup instead of falling back | `CFG-5`, `CFG-6`, `CFG-7`, `OBS-35` | as-built: confirmed | retain | styleguide | The grammars never throw and keep the default (`src/Dexpace.Sdk.Extensions.DependencyInjection/ScalarGrammar.cs:87`, `src/Dexpace.Sdk.Extensions.DependencyInjection/RecordBinder.cs:73`); a separate validator fails `ValidateOnStart` (`src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientRegistrations.cs:27`). Reclassified (P11-10): the accessor letter is met, and the boot failure is a stricter host rule |
| 27 | Public API keeps the `I` prefix and the `Async` suffix | — | as-built: confirmed | retain | styleguide | `src/Dexpace.Sdk.Core/Client/IAsyncHttpClient.cs:53`; `src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs:43` (a sync `Execute` ships beside it) |
| 28 | `CA1062` is dialled to `none` | — | retired (repaired, task 1.6) | repaired | — | `CA1062` is `error` for `src/` (`.editorconfig:148`) and relaxed for applications only (`.editorconfig:138`); its one finding was a real null dereference in `src/Dexpace.Sdk.Core/Errors/TransportExceptions.cs:83`, now guarded; `HttpResponseExceptionTests.The_constructor_rejects_a_null_response` |
| 29 | A URL error carries the redacted input | `HTTP-47`, `OBS-11`, `XCUT-19` | as-built: confirmed | retain | security | `src/Dexpace.Sdk.Core/Http/Request/Request.cs:238` |
| 30 | An absent response body is an empty replayable body | `HTTP-6` | as-built: confirmed | retain | styleguide | `src/Dexpace.Sdk.Core/Http/Response/Response.cs:66`; a mechanism, not a departure |
| 31 | "Regular file" is enforced only where the platform can see it | `BODY-11`, `HTTP-40` | retired (repaired, task 3.3) | repaired | — | A zero-length `FromFile` on Unix is classified by `TarWriter` and a FIFO, device or socket is refused (`src/Dexpace.Sdk.Core/Http/Request/RequestBody.cs:313`, `src/Dexpace.Sdk.Core/IO/UnixFileKind.cs:33`); `FileRequestBodyTests.A_character_device_is_rejected_at_construction`, `FileRequestBodyTests.An_empty_regular_file_the_process_may_not_read_is_accepted` (the writer opens a regular file, so a refusal to open reads as regular), `UnixFileKindTests`; the classification is verified on Linux only |
| 32 | A malformed inbound header *name* fails the whole response | `TRANSPORT-14` | as-built: contradicted → doc corrected | retain, unmet-clause: TRANSPORT-14 | security | A filter could drop the line, but only by parsing the head ahead of the runtime's parser, so the entry's "only workaround is replacing the parser" is corrected and the clause is retained for a different reason; the value half is met (`src/Dexpace.Sdk.Core/Http/Common/Headers.cs:427`); the driver carries the permanent waiver (`tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs:139`, `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs:141`) |
| 33 | `SocketsHttpHandler` re-sends a body-less idempotent request | `TRANSPORT-2` | retired (repaired, task 4.1) | repaired | — | A pass-through `PlaintextStreamFilter` turns "the connection ended before any response byte" into an I/O failure (`src/Dexpace.Sdk.Http.SystemNet/FirstResponseByteStream.cs:54`, `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs:521`); the zero-length content for body-less `POST`/`PUT`/`PATCH` stays (`src/Dexpace.Sdk.Http.SystemNet/OutboundHeaderMapper.cs:143`); `BodylessRetryWireTests.A_bodyless_request_reaches_the_server_once_on_an_sdk_built_handler`; a caller's own handler keeps its native retry, `BodylessRetryWireTests.A_caller_handler_keeps_its_native_retry`. Not claimed: "never twice". A server close that lands after the runtime's reuse check of a pooled connection and before the first write of the request completes the idle read with 0 while the filter is not armed, and the runtime may re-send once on a new connection, a request that was written to an already-closed connection (reasoned from the runtime's source, not reproduced; `docs/sdk-documentation/transport.md`, asymmetry `idle-connection-native-retry`) |

## Retained deviations

Every deviation that survives phase 11, in the order of §10, each with what was verified against the source and why it stays. The six entries whose departure is gone (6, 7, 24, 28, 31, 33) have no
block: the audit row above is their record. A clause counted against the parity target says so in its first line.

### 1. Core takes a runtime dependency on the logging facade

**Verified.** The closure is the facade and `DependencyInjection.Abstractions`, both band-matched (`src/Dexpace.Sdk.Core/packages.lock.json:23`), and the audit fails any third package (`scripts/ci/dependency-audit.cs:124`, `scripts/ci/dependency-audit-selftest.sh:98`). Not counted: `SEAM-1`'s own appendix-C row allows "a logging facade".

**Why it stays.** NuGet has no compile-only scope for an assembly that is called at run time, and every .NET host and sink already speaks `ILogger`. Conforming would take `ILogger` off core's public surface and move logging into an adapter every consumer must add.

### 2. The DI package's hosting family counts as one library

**Verified.** One root, `Microsoft.Extensions.Http`, and a closure of one publisher's lock-stepped family (`src/Dexpace.Sdk.Extensions.DependencyInjection/packages.lock.json:23`, `scripts/ci/dependency-audit.cs:53`).

**Why it stays.** `NFR-2` is a SHOULD and splitting the family does not shrink the closure; it would hand the consumer four packages to wire one feature.

### 3. The byte-stream provider seam is retired

**Verified.** Every `IO/` type is `internal` (`src/Dexpace.Sdk.Core/IO/CapturedBytes.cs:22`, `src/Dexpace.Sdk.Core/IO/StreamCopy.cs:23`, `src/Dexpace.Sdk.Core/IO/TeeStream.cs:31`) and scan N1 finds no provider vocabulary.

**Why it stays.** `Stream`, `Memory<byte>` and `ArrayPool<byte>` are in the shared framework, so there is nothing to plug in. Conforming would be an empty registry with one candidate. A mechanism substitution; not counted.

### 4. End of stream is `Stream.Read` returning 0

**Verified.** Core issues no zero-count read (`src/Dexpace.Sdk.Core/IO/StreamCopy.cs:45`) and checks a known length (`src/Dexpace.Sdk.Core/IO/StreamCopy.cs:104`); `tests/Dexpace.Sdk.Core.Tests/IO/ZeroCountReadRuleTests.cs:20` holds it.

**Why it stays.** The `Stream` contract defines 0 as the end, and a zero-count request also returns 0, so the spec's separate sentinel cannot exist. A mechanism substitution; not counted.

### 5. One stream-ownership rule for request bodies

**Verified.** `src/Dexpace.Sdk.Core/Http/Request/FileRequestBody.cs:101`, `src/Dexpace.Sdk.Core/Http/Request/RequestBody.cs:360`, `src/Dexpace.Sdk.Core/Http/Request/LoggingRequestBody.cs:92`, `src/Dexpace.Sdk.Core/Http/Response/ResponseBody.cs:218`.

**Why it stays.** `BODY-8` itself asks the port to decide: "A port MUST decide its stream-ownership/close rule deliberately". A sanctioned choice; never counted.

### 8. Cancellation is a cooperative token

**Verified.** `src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs:103`, `src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs:171`, `src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs:158`, `src/Dexpace.Sdk.Core/Client/ResponseTaskExtensions.cs:42`, `BannedSymbols.txt:10`; the transport's replacement of the native exception is `src/Dexpace.Sdk.Http.SystemNet/SendFailures.cs:54`.

**Why it stays.** `Thread.Abort` is unsupported on .NET, `Thread.Interrupt` interrupts arbitrary lock waits inside library code, and the token aborts the socket operation. The spec's own residual, a blocking call that ignores interrupts running to completion, is identical here. Conforming risks state corruption from interrupting foreign waits. A mechanism substitution; not counted.

### 9. No provider registry or auto-discovery

**Verified.** `src/Dexpace.Sdk.Extensions.DependencyInjection/ServiceSlot.cs:25`, `src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientValidator.cs:34`; scan N2.

**Why it stays.** Assembly scanning and `[ModuleInitializer]` are invisible to ILLink and NativeAOT (`NFR-8`), so a trimmed or AOT application would silently lose providers.

### 10. No model builder objects, no `IBuilder<T>`

**Verified.** Four public `*Builder` types, none a model builder (`src/Dexpace.Sdk.Core/Pipeline/PipelineBuilder.cs:36`, `src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientBuilder.cs:22`, `Headers.Builder`, `Query.Builder`); scan N5 allow-lists them.

**Why it stays.** Records, `with` and validating constructors are the language's builder (styleguide 06), and `required` makes a missing field a compile error. Conforming adds a parallel mutable type per model and a generic contract with no implementer.

### 11. Reflection can bypass construction checks

**Verified.** `src/Dexpace.Sdk.Http.SystemNet/OutboundHeaderMapper.cs:194`, `src/Dexpace.Sdk.Http.SystemNet/OutboundHeaderMapper.cs:76`.

**Why it stays.** `GetUninitializedObject` cannot be closed from inside a library, so the wire re-check is the defence and is mandatory.

### 12. The fatal set is `OutOfMemoryException`

**Verified.** `src/Dexpace.Sdk.Core/Recovery/RecoveryDispatcher.cs:156`, `src/Dexpace.Sdk.Core/Recovery/ResponseRecoveryChain.cs:127`, `src/Dexpace.Sdk.Core/Diagnostics/UrlRedactor.cs:100`, `tests/Dexpace.Sdk.Core.Tests/Architecture/FatalFilterArchitectureTests.cs:16`.

**Why it stays.** `StackOverflowException` is uncatchable and `AccessViolationException` is not delivered on .NET, so no port can catch them.

### 13. The suppressed trail lives in `Exception.Data`

**Verified.** `src/Dexpace.Sdk.Core/Errors/ExceptionTrail.cs:28`, `src/Dexpace.Sdk.Core/Errors/ExceptionTrail.cs:67`; scan N6.

**Why it stays.** .NET has no suppressed list, and `AggregateException` changes the thrown type and breaks `RECOV-10`'s "rethrow unchanged"; callers' typed catches would stop matching.

### 14. Both standard pipelines follow redirects

**Verified.** `src/Dexpace.Sdk.Core/Pipeline/PipelineBuilder.cs:222`, `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs:516`.

**Why it stays.** `REDIR-25` lets callers install their own async redirect step and asks that the asymmetry be preserved *or explicitly documented if changed*; one policy implementation serves both paths, and the single-layer invariant is the transport's. A sanctioned change; not counted.

### 15. No cross-origin marker

**Verified.** `src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectChain.cs:25`, `src/Dexpace.Sdk.Core/Pipeline/Policies/AuthorizationPolicy.cs:154`, `src/Dexpace.Sdk.Core/Pipeline/Policies/IdempotencyPolicy.cs:42`; scan N7.

**Why it stays.** A forgeable or leakable marker reintroduces the trap `REDIR-11` itself flags, and a pipeline with no auth step would forward it. Stronger than the letter; not counted.

### 16. Digest MD5 declined where the host refuses MD5 (counted)

**Verified.** `src/Dexpace.Sdk.Core/Auth/Md5Availability.cs:22`, `src/Dexpace.Sdk.Core/Auth/Md5Availability.cs:31`, `src/Dexpace.Sdk.Core/Auth/DigestChallengeHandler.cs:94`; scan N8.

**Why it stays.** On a FIPS host the refusal is the operator's compliance control, and a vendored MD5 over the password would silently take the deployment out of its boundary. The SDK already refuses to override a host hook (`TraceIdGenerator`). The mapping has never run on a real FIPS host. Conforming makes FIPS deployments non-compliant without anyone knowing. **Counted:** `AUTH-15`.

### 17. Pages are materialized

**Verified.** `src/Dexpace.Sdk.Core/Pagination/PageStep.cs:30`, `src/Dexpace.Sdk.Core/Pagination/FetcherPageable.cs:28`, `src/Dexpace.Sdk.Core/Pagination/Page.cs:32`.

**Why it stays.** An abandoned async iterator never runs its `finally`, so a page holding a response strands a connection for a hand-driven enumerator. Stronger than the letter; not counted.

### 18. The query splice is relative to `Uri`'s canonical form (counted)

**Verified.** `src/Dexpace.Sdk.Core/Pagination/QuerySplice.cs:115`, `src/Dexpace.Sdk.Core/Http/Request/Request.cs:218`.

**Why it stays.** The only byte-preserving API disables `Uri`'s validation, which puts CR/LF and the fragment on the wire. The residual is RFC 3986 §6.2.2.2 equivalence of unreserved characters. Conforming adds an injection surface and rewrites every `GetComponents` call. **Counted:** `PAGE-21`.

### 19. One pull-based paging engine

**Verified.** `src/Dexpace.Sdk.Core/Pagination/StrategyPageable.cs:20`; scan N9.

**Why it stays.** A pull iterator invokes no callback, so an executor has nothing to schedule and `await Task.Yield()` moves the consumer. A mechanism substitution; not counted.

### 20. SSE lines and events are capped

**Verified.** `src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventReader.cs:50`, `src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventReader.cs:56`, `src/Dexpace.Sdk.Core/ServerSentEvents/EventAccumulator.cs:107`, `src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventTooLargeException.cs:26`, `src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventLineTooLongException.cs:32`.

**Why it stays.** `SSE-19`'s chapter text says "a port MAY add a configurable cap and reject/truncate oversized lines, documenting the divergence"; an unbounded line or event from a hostile server is an unbounded allocation. A sanctioned divergence; never counted. (The design's retained table omitted this entry; the audit adds it because the divergence survives.)

### 21. `Tristate<T>` is an invariant struct

**Verified.** `src/Dexpace.Sdk.Core/Serialization/TristateOfT.cs:29`.

**Why it stays.** Variance exists only on interfaces and delegates, and a class would make `default` null, breaking `SERDE-17`.

### 22. `ILogger` instead of a log-event object

**Verified.** `src/Dexpace.Sdk.Core/Diagnostics/HttpLogRecord.cs:35`, `src/Dexpace.Sdk.Core/Diagnostics/HttpLogEmitter.cs:221`, `src/Dexpace.Sdk.Core/Diagnostics/RedirectLog.cs:28`, `src/Dexpace.Sdk.Http.SystemNet/HeaderDropLog.cs:123`, `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/DisabledPathAllocationTests.cs:27`.

**Why it stays.** The logging convergence already gives a zero-allocation disabled path; a second event model would sit beside every host's. The dotted keys the specification fixes cannot be generator placeholders (`SYSLIB1014`), which is the one departure from the styleguide's generator rule that remains.

### 23. `Activity` tracing, `null` as the no-op

**Verified.** `src/Dexpace.Sdk.Core/Diagnostics/OperationTelemetry.cs:43`, `src/Dexpace.Sdk.Core/Internal/Disposal.cs:106`, `src/Dexpace.Sdk.Core/Execution/InstrumentationContext.cs:73`.

**Why it stays.** OpenTelemetry .NET's tracing API is `Activity`; a shim would not be subscribed by the OTel SDK.

### 25. No system-property tier, raw accessor or global slot; a blank value shadows in a section of an unregistered configuration (counted)

**Verified.** `src/Dexpace.Sdk.Extensions.DependencyInjection/ConfigSection.cs:51`, `src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientStateSetup.cs:27`; scan N10.

**Why it stays.** .NET has no system properties, and a global slot is the service locator the styleguide rejects. The `CFG-2` fall-through is repaired for a section of the container's registered `IConfiguration`, and stays unmet for a section of any other: the fall-through walks the providers of the root the section was taken from, `IConfigurationSection` exposes no way to that root (`ConfigurationSection` keeps it private, and reading it by reflection is not AOT-safe, `NFR-8`), so the reader walks the registered root's providers when that root's slice at the section's path holds the same pairs as the section, and otherwise reads the merged view. Conforming would need a public API that takes the root (an `AddDexpaceClient` overload over an `IConfiguration` and a path, a post-v1 candidate) or a reflection read the trimmed and AOT builds cannot do. **Counted:** `CFG-2` (the section of an unregistered configuration; register the root the section is read from).

### 26. The boot fails on a recorded parse failure

**Verified.** `src/Dexpace.Sdk.Extensions.DependencyInjection/ScalarGrammar.cs:87`, `src/Dexpace.Sdk.Extensions.DependencyInjection/RecordBinder.cs:73`, `src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientRegistrations.cs:27`.

**Why it stays.** The accessor letter of `CFG-5` to `CFG-7` is met (the default is kept, nothing throws); `csharp-aspnetcore` 1.4 requires `ValidateOnStart` on every bound options type. Conforming would let `Transport:Proxy:BypassAll=on` silently route through the proxy and `Logging:Level=Header` silently log nothing. Reclassified from an unmet clause to a stricter host rule (P11-10); not counted.

### 27. `I` prefix and `Async` suffix

**Verified.** `src/Dexpace.Sdk.Core/Client/IAsyncHttpClient.cs:53`, `src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs:43`.

**Why it stays.** The Framework Design Guidelines outrank the vendored guide for a public NuGet surface; a prefix-less `HttpClient` collides with `System.Net.Http.HttpClient`.

### 29. A URL error carries the redacted input

**Verified.** `src/Dexpace.Sdk.Core/Http/Request/Request.cs:238`.

**Why it stays.** A userinfo password or a query token must not reach an exception message that is logged and surfaced (`XCUT-19`).

### 30. An absent response body is an empty replayable body

**Verified.** `src/Dexpace.Sdk.Core/Http/Response/Response.cs:66`.

**Why it stays.** Consumers never branch on a missing body (styleguide 03: prefer an empty object to `null`). A mechanism, not a departure.

### 32. A malformed inbound header name fails the response (counted)

**Verified.** `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs:139`, `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs:141`, `src/Dexpace.Sdk.Core/Http/Common/Headers.cs:427`.

**Why it stays.** The only repair is a filter that parses the HTTP/1.1 head ahead of the runtime's parser on every response (1xx heads, folded lines, lenient line endings); a defect there corrupts all traffic, to tolerate a non-compliant server. Contrast entry 33's filter, which never inspects or alters a byte. Conforming would put SDK-owned parsing in front of every response head. **Counted:** `TRANSPORT-14`'s name half. The trigger to revisit is a consumer whose server sends such names.

## Readings (design §11)

One row per item of §11, 1 to 72 (items 69 to 72 are the phase 11 additions, written into §11 by its dated correction). *Verdict* is `confirmed`, `narrowed`, `contradicted` or `added`;
*Disposition* is `retain`, `doc-corrected`, `repaired` or `added`, with an optional `(+…)`. A reading is not a departure: the letter of the specification is met or the clause has no subject, and the
item says how the port read it.

| Item | IDs | Verdict | Disposition | Evidence |
|---|---|---|---|---|
| 1 | `CFG-15`, `RETRY-26` | confirmed | retain | Waits hold no thread and long ones are chunked (`src/Dexpace.Sdk.Core/Configuration/TimeProviderWaits.cs:69`) |
| 2 | `NFR-11`, `SEAM-17` | narrowed | doc-corrected | No framework leaks (`tests/Dexpace.Sdk.Core.Tests/Architecture/Nfr11PublicSurfaceArchitectureTests.cs:19`); two documented sync-over-async defaults remain (`src/Dexpace.Sdk.Core/Pipeline/HttpPipelinePolicy.cs:63`, `src/Dexpace.Sdk.Core/Auth/TokenCredential.cs:47`) |
| 3 | `SEAM-2` | confirmed | retain | Retiring a seam reads `SEAM-2` as constraining a retained concern; as entry 3 (`src/Dexpace.Sdk.Core/IO/CapturedBytes.cs:22`) |
| 4 | `CFG-1` | narrowed | doc-corrected | The DI package reads the caller's section; tier order is the host's provider order, and the `Configure` transforms are the first tier (`src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientRegistration.cs:33`) |
| 5 | `SEAM-30` | confirmed | retain | An orphaned response is closed once and never surfaces (`tests/Dexpace.Sdk.Core.Tests/Client/SyncToAsyncBridgeTests.cs:265`) |
| 6 | `HTTP-2`, `SEAM-29` | confirmed | retain | Constructor privacy is compiler-enforced (`src/Dexpace.Sdk.Core/Http/Common/Method.cs:31`, `tests/Dexpace.Sdk.Core.Tests/Architecture/ModelConstructionArchitectureTests.cs:19`) |
| 7 | `BODY-3` | confirmed | retain | The compare-and-set is `Interlocked` (`src/Dexpace.Sdk.Core/Http/Request/RequestBody.cs:450`) |
| 8 | `XCUT-23` | confirmed | retain | The package detects duplicate registrations itself (`src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientValidator.cs:34`, `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/SingleRegistrationTests.cs:15`) |
| 9 | — | confirmed | retain | Appendix B is a weaker claim; the report is the worst-of view (`src/Dexpace.Sdk.Conformance/Reporting/ConformanceReport.cs:61`) |
| 10 | `RETRY-38`, `RETRY-45` | contradicted | doc-corrected (+repaired: the attempt header is set and may not be `Idempotency-Key`, task 5.7) | Built, not deferred (`src/Dexpace.Sdk.Core/Configuration/RetryOptions.cs:225`); the stamp replaces (`src/Dexpace.Sdk.Core/Resilience/RetryEngine.cs:134`); `RetryEngineTests.The_attempt_header_replaces_its_previous_value`, `RetryOptionsTests.AttemptHeaderName_rejects_the_idempotency_key_name`; a configured idempotency header of the same name is refused where each stack is assembled (`src/Dexpace.Sdk.Core/Resilience/AttemptHeaderCollision.cs:30`, `src/Dexpace.Sdk.Core/Pipeline/PipelineBuilder.cs:309`), `AttemptHeaderKeyCollisionTests` |
| 11 | `CTX-16`, `CTX-20` | confirmed | retain | Embedded MUSTs of shipped SHOULD features are met (`tests/Dexpace.Sdk.Core.Tests/Execution/InstrumentationContextTests.cs:19`, `tests/Dexpace.Sdk.Core.Tests/Execution/PromotionChainTests.cs:19`) |
| 12 | `BODY-8`, `RECOV-14`, `RETRY-34`, `AUTH-31` | narrowed | doc-corrected | One control flow per policy; the credential hooks are sync/async twins (`src/Dexpace.Sdk.Core/Pipeline/Policies/BearerTokenAuthPolicy.cs:86`) |
| 13 | — | confirmed | retain | Citation hygiene: no code or test cites `HTTP-16-body` |
| 14 | `SEAM-6`, `SEAM-8` | narrowed | doc-corrected | `ServiceSlot`, not `TryAdd`, gives the idempotence (`src/Dexpace.Sdk.Extensions.DependencyInjection/ServiceSlot.cs:25`) |
| 15 | `CFG-34`, `SERDE-11` | narrowed | doc-corrected | The deep-equality helper compares zero sign explicitly (`src/Dexpace.Sdk.Core/Internal/DeepValue.cs:29`) and has no production caller |
| 16 | `PIPE-32` | confirmed | retain | Reversed; as entry 14 (`src/Dexpace.Sdk.Core/Pipeline/PipelineBuilder.cs:222`) |
| 17 | — | confirmed | retain | The SSE preamble's deviations are replicated; scan N13 finds no `SseParser` in `src/` |
| 18 | `SERDE-26`, `TRANSPORT-18` | confirmed | retain (+doc: two wordings corrected in §11) | Options are copied; the bounded re-send capture is `src/Dexpace.Sdk.Http.SystemNet/ResendCapture.cs:40` with its limit `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClientOptions.cs:116` |
| 19 | `RETRY-28` | confirmed | retain | Two stacks, neither unification sanction invoked (`src/Dexpace.Sdk.Core/Pipeline/Policies/RetryPolicy.cs:121`, `src/Dexpace.Sdk.Core/Recovery/RetryRecovery.cs:35`) |
| 20 | `RECOV-31`, `RETRY-38` | contradicted | doc-corrected | One feature under two IDs, built; as item 10 |
| 21 | `ASYNC-21` | confirmed | retain | No reactive adapter; the property is on the pull path (`src/Dexpace.Sdk.Core/ServerSentEvents/ServerSentEventReader.cs:109`); scan N12 |
| 22 | `RETRY-6`, `HTTP-9` | confirmed | retain | `TRACE` is dropped and the set is internal (`src/Dexpace.Sdk.Core/Resilience/RetryFacts.cs:28`) |
| 23 | `XCUT-4`, `RETRY-2` | confirmed | retain | The I/O family reading stays; the type clause is repaired under entry 7 (`src/Dexpace.Sdk.Core/Resilience/RetryFacts.cs:106`) |
| 24 | — | narrowed | doc-corrected | Nothing the SDK adds retries below it, and since task 4.1 neither does the handler the SDK builds (`src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs:521`); scan N15 |
| 25 | `XCUT-16`, `AUTH-28` | confirmed | retain | No loopback exemption (`src/Dexpace.Sdk.Core/Pipeline/Policies/AuthorizationPolicy.cs:252`, `tests/Dexpace.Sdk.Core.Tests/Security/AuthHttpsGuardTests.cs:25`); scan N16 |
| 26 | `AUTH-11`, `AUTH-37` | confirmed | retain | Background refresh in the expiring zone (`src/Dexpace.Sdk.Core/Auth/AccessTokenCache.cs:43`) |
| 27 | `CFG-31` | confirmed | retain | RFC 850 and asctime dates are rejected (`src/Dexpace.Sdk.Core/Http/Common/HttpDate.cs:33`) |
| 28 | `RETRY-15`, `CFG-30` | confirmed | retain | The weekday is not validated (`src/Dexpace.Sdk.Core/Http/Common/HttpDate.cs:33`) |
| 29 | `PIPE-32`, `REDIR-25` | confirmed | retain | The async redirect step is sanctioned; as entry 14 (`src/Dexpace.Sdk.Core/Pipeline/PipelineBuilder.cs:222`) |
| 30 | `CTX-7..13`, `CTX-17..19` | contradicted | doc-corrected | No `CallKey` on `RequestOptions` and no SDK reader; the store's lookup is for consumers (`src/Dexpace.Sdk.Core/Execution/DexpaceCallContexts.cs:9`); scan N19 |
| 31 | `REDIR-11` | confirmed | retain | The marker is removed; as entry 15 (`src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectChain.cs:25`) |
| 32 | `XCUT-2` | narrowed | doc-corrected | Classification tests the token first, then the inner `TimeoutException` (`src/Dexpace.Sdk.Core/Resilience/RetryFacts.cs:22`, `src/Dexpace.Sdk.Http.SystemNet/SendFailures.cs:54`) |
| 33 | `RECOV-17..34` | confirmed | retain | Stated in appendix C only; amendment N11-6 |
| 34 | `HTTP-42` | confirmed | retain | The BOM strip follows the charset's preamble (`src/Dexpace.Sdk.Core/Internal/TextDecoding.cs:16`); scan N14 |
| 35 | `SEAM-27` | confirmed | retain | A path value of `.` or `..` is rejected (`src/Dexpace.Sdk.Core/Operations/OperationDescriptor.cs:267`) |
| 36 | `HTTP-17`, `HTTP-19`, `HTTP-21`, `TRANSPORT-12` | narrowed | repaired | The inbound path no longer trims a name (`src/Dexpace.Sdk.Core/Http/Common/HeaderSyntax.cs:37`); `HeaderInjectionValidationTests.The_inbound_path_does_not_trim_a_name` |
| 37 | `XCUT-20`, `OBS-20`, `OBS-30` | confirmed | retain | The SDK's own recording is total; listener callbacks are the other party (`src/Dexpace.Sdk.Core/Diagnostics/EmissionGuard.cs:13`) |
| 38 | `OBS-32` | confirmed | retain | Duration in seconds, no count instrument (`src/Dexpace.Sdk.Core/Diagnostics/HttpClientMetrics.cs:25`); scan N11 |
| 39 | `HTTP-46` | confirmed | retain | Body equality is by value or by identity (`src/Dexpace.Sdk.Core/Http/Request/RequestBody.cs:397`) |
| 40 | `HTTP-48`, `HTTP-18` | confirmed | retain | An obs-text `ETag` parses and cannot be sent (`src/Dexpace.Sdk.Core/Http/Request/RequestConditions.cs:62`) |
| 41 | `SEAM-2` | narrowed | doc-corrected | One public seam implementer, `HttpPipeline`; `SeamImplementationArchitectureTests.Core_implements_a_seam_only_through_the_allow_listed_adapters` |
| 42 | `SEAM-22` | confirmed | retain | No seam member takes a `Type` (`tests/Dexpace.Sdk.Core.Tests/Architecture/SerdeSeamArchitectureTests.cs:17`) |
| 43 | `SEAM-30` | contradicted | repaired | The documented cancel-without-interruption mode leaked 44 of 20 000 responses; `WaitOrDisposeAsync` closes the orphan (`src/Dexpace.Sdk.Core/Client/ResponseTaskExtensions.cs:42`, `src/Dexpace.Sdk.Core/Internal/LateResult.cs:42`) |
| 44 | `IO-10`, `IO-21` | contradicted | doc-corrected | `CapturedBytes` has no production caller (`src/Dexpace.Sdk.Core/IO/CapturedBytes.cs:22`) |
| 45 | `IO-14` | confirmed | retain | Malformed bytes decode to U+FFFD and a BOM is content (`src/Dexpace.Sdk.Core/IO/Utf8LineReader.cs:35`) |
| 46 | — | confirmed | retain | Both `Apply` and `ApplyAsync` are required (`src/Dexpace.Sdk.Core/Recovery/IRequestStep.cs:34`) |
| 47 | `PIPE-2`, `PIPE-4` | narrowed | doc-corrected | The operation span is opened by the pipeline, not by `OperationPolicy` (`src/Dexpace.Sdk.Core/Pipeline/HttpPipeline.cs:263`) |
| 48 | `PIPE-3` | confirmed | retain | Stage keys are sparse (`src/Dexpace.Sdk.Core/Pipeline/PipelineStage.cs:29`) |
| 49 | `CFG-31`, `RETRY-15` | confirmed | retain | A one- or two-digit day (`src/Dexpace.Sdk.Core/Http/Common/HttpDate.cs:33`) |
| 50 | `CFG-23` | confirmed | retain | A leading dot is a literal glob (`src/Dexpace.Sdk.Core/Internal/ProxyGlob.cs:17`) |
| 51 | `CFG-12` | narrowed | doc-corrected | Core has no configuration builder; `DexpaceClientBuilder` is one, used single-threaded (`src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientBuilder.cs:22`) |
| 52 | `RETRY-41`, `RECOV-34`, `HTTP-35` | confirmed | retain | A negative count is rejected (`src/Dexpace.Sdk.Core/Configuration/RetryOptions.cs:60`) |
| 53 | `RETRY-10`, `RETRY-15` | confirmed | retain | A sub-tick jitter returns the base (`src/Dexpace.Sdk.Core/Resilience/RetryBackoff.cs:54`) |
| 54 | `RETRY-21` | narrowed | repaired | The stage stack walks a caller-configured ordered list (`src/Dexpace.Sdk.Core/Configuration/RetryOptions.cs:178`, `src/Dexpace.Sdk.Core/Resilience/RetryPacing.cs:52`); `RetryPolicyTests.The_stage_stack_walks_the_configured_pacing_header_order` |
| 55 | `RETRY-16`, `RETRY-18` | confirmed | retain | A huge numeral saturates and clamps (`src/Dexpace.Sdk.Core/Resilience/RetryPacing.cs:27`) |
| 56 | `RECOV-19` | confirmed | retain | Every send is classified (`src/Dexpace.Sdk.Core/Recovery/RecoveryDispatcher.cs:167`) |
| 57 | `REDIR-3` | confirmed | retain | The original method (`src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectChain.cs:37`) |
| 58 | `REDIR-20` | confirmed | retain | The predicate replaces eligibility only (`src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectDecider.cs:16`) |
| 59 | `PIPE-40`, `REDIR-6`, `REDIR-22` | confirmed | retain | The union reading of the abandon paths (`src/Dexpace.Sdk.Core/Pipeline/Policies/RedirectPolicy.cs:76`) |
| 60 | — | confirmed | retain | Several `Location` values are malformed (`src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectLocation.cs:12`) |
| 61 | `REDIR-28` | confirmed | retain | The malformed-`Location` log is redacted (`src/Dexpace.Sdk.Core/Diagnostics/RedirectLog.cs:135`) |
| 62 | `AUTH-16`, `AUTH-17`, `AUTH-22` | confirmed | retain | `-sess` without `qop` is declined (`src/Dexpace.Sdk.Core/Auth/DigestChallengeHandler.cs:176`); amendment N11-3 |
| 63 | `AUTH-28` | confirmed | retain | The guard also runs on a reactive scheme's outbound pass (`src/Dexpace.Sdk.Core/Pipeline/Policies/AuthorizationPolicy.cs:252`) |
| 64 | `AUTH-8`, `AUTH-9` | confirmed | retain | The named-key clauses are vacuous; no `NamedKey` type exists |
| 65 | `AUTH-36` | confirmed | retain | A cross-origin `401` is not challenge-handled (`src/Dexpace.Sdk.Core/Pipeline/Policies/AuthorizationPolicy.cs:199`) |
| 66 | `PAGE-19` | confirmed | retain | The `Link` cross-origin guard (`src/Dexpace.Sdk.Core/Pagination/LinkHeaderStrategy.cs:21`) |
| 67 | `CFG-7`, `CFG-5` | narrowed | doc-corrected | The duration grammar also accepts `"250 ms"` and `"1.5s"` (`src/Dexpace.Sdk.Extensions.DependencyInjection/DurationGrammar.cs:12`) |
| 68 | `CFG-2` | contradicted | repaired | The fall-through half can be met for a section of the registered root (`src/Dexpace.Sdk.Extensions.DependencyInjection/ConfigSection.cs:51`); `EmptyValueTests.An_empty_value_falls_through_to_the_lower_provider`. It cannot for a section of any other configuration, which is the counted residual of entry 25 (`EmptyValueTests.A_section_from_an_unregistered_configuration_keeps_the_merged_read`) |
| 69 | `XCUT-24`, `BODY-30`, `BODY-33`, `RECOV-16` | added | added | The error snapshot's primary read path is the `BODY-30` buffered copy, which is replayable (`src/Dexpace.Sdk.Core/Http/Response/ErrorBodyBuffer.cs:22`); `ErrorBodyBufferTests.The_buffered_copy_is_replayable`, `ErrorBodyPreviewTests.Reading_the_error_body_twice_yields_the_same_bytes_and_GetErrorAsync_still_works` |
| 70 | `HTTP-17`, `HTTP-19`, `XCUT-18` | added | added | The trim of `HTTP-17` is outbound-only; an inbound name with surrounding whitespace is refused (`src/Dexpace.Sdk.Core/Http/Common/HeaderSyntax.cs:37`) |
| 71 | `TRANSPORT-2` | added | added | "SDK-managed transport" is a transport whose primary handler the SDK built; a caller's handler or `HttpClient` is the caller's native client (`tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs:144`, `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs:521`) |
| 72 | `CFG-5`, `CFG-6`, `CFG-7` | added | added | The clauses bind the accessor; a host validator that refuses to start on a recorded parse failure is stricter, not contrary (`src/Dexpace.Sdk.Extensions.DependencyInjection/RecordBinder.cs:73`, `src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceClientRegistrations.cs:27`) |

## Negative scans


A claim that something does not exist is backed by a scan with a probe that proves the scan is not vacuous (P11-6). *Pattern* is a .NET regular expression (`\|` is a literal bar inside a table cell and
stands for the regex alternation bar), run line by line over the files of *Paths* (`<dir>/**/*.<ext>` or `*.{a,b}`; `bin/` and `obj/` excluded; a line that starts with `//` is a comment and is not read); *Allowed* is the
file names that may match, else none; *Probe* is a line the pattern must match, which rule L10 checks; L10 also fails a *Paths* cell with no glob, a glob that is not of the two shapes above, and a glob whose directory holds no file of its extension (a scan that reads nothing proves nothing, so a renamed directory cannot make a scan pass). `LedgerAuditTests` rule L9 runs every row over the tree, and the scan N20 below is the one enforced by an
architecture test instead. Absence claims about the compiled surface are architecture tests under `tests/Dexpace.Sdk.Core.Tests/Architecture/`, and the gate checks that every test a row cites exists.


| Scan | Entry/item | Claim | Pattern | Paths | Allowed | Probe |
|---|---|---|---|---|---|---|
| N1 | 3 | absence of the provider vocabulary | `\b(class\|interface\|struct\|record)\s+(I?(Buffered)?(Source\|Sink)\|Buffer\|I?IoProvider\|ByteString)\b` | `src/**/*.cs` | — | `internal sealed class Buffer` |
| N2 | 9 | no discovery | `ModuleInitializer\|AppDomain\.CurrentDomain\.GetAssemblies\|Assembly\.(Load\|LoadFrom\|LoadFile)\b\|GetExportedTypes\|AssemblyLoadContext` | `src/**/*.cs` | — | `var all = Assembly.Load(name);` |
| N3 | 7 | the unused failure type is gone | `class\s+ServiceResponseException\b` | `src/**/*.cs` | — | `public class ServiceResponseException : Exception` |
| N4 | 8 | no thread interruption | `\bThread\.(Abort\|Interrupt)\b\|ThreadInterruptedException\|ThreadAbortException` | `src/**/*.cs` | — | `worker.Interrupt(); Thread.Abort();` |
| N5 | 10 | the builders are the four listed | `\b(class\|record\|struct\|interface)\s+I?\w*Builder\b` | `src/Dexpace.Sdk.Core/**/*.cs` `src/Dexpace.Sdk.Http.SystemNet/**/*.cs` `src/Dexpace.Sdk.Serialization.SystemTextJson/**/*.cs` `src/Dexpace.Sdk.Extensions.DependencyInjection/**/*.cs` | `Headers.cs` `Query.cs` `PipelineBuilder.cs` `DexpaceClientBuilder.cs` | `public sealed class RequestBuilder` |
| N6 | 13 | no wrapper exception for the trail | `new\s+AggregateException\b` | `src/**/*.cs` | — | `throw new AggregateException(errors);` |
| N7 | 15 | one property key | `PipelinePropertyKey<[^>]+>\s+\w+\s*=` | `src/**/*.cs` | `IdempotencyPolicy.cs` | `PipelinePropertyKey<string> s_k = new("x");` |
| N8 | 16 | no vendored MD5 | `(?i)0x67452301\|0xefcdab89\|0x98badcfe\|0x10325476` | `src/**/*.cs` | — | `uint a = 0x67452301;` |
| N9 | 19 | the paging engine schedules nothing | `TaskScheduler\|SynchronizationContext\|ThreadPool\.\|ContinueWith` | `src/Dexpace.Sdk.Core/Pagination/**/*.cs` | — | `task.ContinueWith(next);` |
| N10 | 25 | no global slot | `public\s+static\s+(?!readonly)[^(=;]*\{\s*get;\s*(set\|init);` | `src/**/*.cs` | — | `public static ClientOptions Current { get; set; }` |
| N11 | 11 / 38 | one metrics file | `Create(Counter\|Histogram\|UpDownCounter\|Observable\w+\|Gauge)\b` | `src/**/*.cs` | `HttpClientMetrics.cs` | `var c = meter.CreateCounter<long>("x");` |
| N12 | 21 | no reactive adapter | `IObservable<\|IObserver<\|System\.Reactive` | `src/**/*.cs` | — | `class Bridge : IObservable<int>` |
| N13 | 17 | core ships its own SSE parser | `System\.Net\.ServerSentEvents\|\bSseParser\b\|\bSseItem<` | `src/**/*.cs` | — | `var parser = SseParser.Create(stream);` |
| N14 | 34 | nothing writes a BOM | `\bStreamWriter\b\|new\s+UTF8Encoding\(\s*true` | `src/**/*.cs` | — | `using var w = new StreamWriter(stream);` |
| N15 | 24 | no second retry engine | `AddStandardResilienceHandler\|AddResilienceHandler\|\bPolly\b` | `src/**/*.{cs,csproj}` | — | `services.AddResilienceHandler("x", b => {});` |
| N16 | 25 | no loopback exemption in core | `IsLoopback\|"localhost"\|127\.0\.0\.1` | `src/Dexpace.Sdk.Core/**/*.cs` | — | `if (host == "localhost") return;` |
| N17 | 31 | no native stat | `\[(DllImport\|LibraryImport)\b\|static\s+extern\b` | `src/**/*.cs` | — | `[DllImport("libc")] static extern int stat();` |
| N18 | 1 | background work has one site | `Task\.Run\(\|ThreadPool\.QueueUserWorkItem` | `src/Dexpace.Sdk.Core/**/*.cs` `src/Dexpace.Sdk.Http.SystemNet/**/*.cs` `src/Dexpace.Sdk.Serialization.SystemTextJson/**/*.cs` `src/Dexpace.Sdk.Extensions.DependencyInjection/**/*.cs` | `BackgroundWork.cs` | `return Task.Run(work);` |
| N19 | 30 | no CallKey stamp or reader outside core | `\bCallKey\b` | `src/Dexpace.Sdk.Http.SystemNet/**/*.cs` `src/Dexpace.Sdk.Extensions.DependencyInjection/**/*.cs` | — | `var key = new CallKey();` |
| N20 | 12 | every broad catch filters the fatal exceptions | `catch\s*(\(\s*(System\.)?Exception(\s+\w+)?\s*\))?\s*$` | `src/Dexpace.Sdk.Core/**/*.cs` `src/Dexpace.Sdk.Http.SystemNet/**/*.cs` `src/Dexpace.Sdk.Serialization.SystemTextJson/**/*.cs` `src/Dexpace.Sdk.Extensions.DependencyInjection/**/*.cs` | gate: `FatalFilterArchitectureTests.No_catch_all_in_a_runtime_library_swallows_a_fatal_exception` | `catch (Exception ex)` |

## Amendment set N11

Recommendations to the specification's author, recorded here and not applied: `docs/product-spec/` is byte-identical to the Ruby and Node copies and `docs/README.md` freezes it (P11-4), so, as Ruby's
"C11" set lives in Ruby's `docs/deviations.md`, the .NET set lives in this file. Each names the rows, the problem and the proposed text.

| # | Rows | Problem | Proposed |
|---|---|---|---|
| N11-1 | `XCUT-24`, `BODY-30`, `BODY-33`, `RECOV-16` | `XCUT-24` asks error snapshots to be non-consuming while `BODY-30` and `RECOV-16` require the cap to drain and release | "For an error snapshot, the primary read path is the `BODY-30` buffered copy; the original stream is released per `RECOV-16`." |
| N11-2 | `HTTP-17`, `HTTP-19`, `XCUT-18` | the trim and "reject HTAB" meet at the edge of a name; `HTTP-19`'s strictness is easy to read as trimming too | "Surrounding whitespace is trimmed for outbound names only; an inbound name with surrounding whitespace is invalid." |
| N11-3 | `AUTH-16`, `AUTH-17`, `AUTH-22` | a `-sess` challenge without `qop` cannot satisfy all three | "A `-sess` algorithm without `qop` MUST be declined." |
| N11-4 | `TRANSPORT-2` | "SDK-managed transport" is undefined when the caller supplies the native client | "…a transport whose native client the SDK constructed; a caller-supplied client's retry is the caller's." |
| N11-5 | `RETRY-38`, `RECOV-31` | one feature, two IDs, two modal levels; `RETRY-38`'s tag contradicts its prose | merge under one ID at SHOULD, with "the stamp replaces any previous attempt header and MUST NOT reuse the idempotency key's name". |
| N11-6 | `RECOV-17..34` | stated in appendix C only | give them a chapter body in `08-execution-pipelines.md` §8.2. |
| N11-7 | `CFG-31` | rejects date forms RFC 9110 §5.6.7 obliges recipients to accept | state the intent ("a rejected date is no hint") explicitly. |
| N11-8 | `CFG-5`, `CFG-6`, `CFG-7` | "never throw" is unclear for configuration read once at composition | "The accessor never throws; a host MAY refuse to start on a recorded parse failure." |
| N11-9 | `PAGE-21` | "byte-for-byte" is unattainable through a URL type that canonicalizes unreserved escapes | permit RFC 3986 §6.2.2.2 normalization of unreserved characters. |
| N11-10 | `OBS-32` | names `http.client.request.count` and `ms` while deferring to OpenTelemetry, which has neither | follow the OpenTelemetry stable HTTP client metrics. |
| N11-11 | `XCUT-20`, `OBS-20`, `OBS-30` | "observability MUST NEVER throw" vs unwrapped listener calls | "…the SDK's own recording code; listener callbacks are the `OBS-30` party." |
| N11-12 | `SEAM-2` | "never implements" forbids the bridges `SEAM-18` requires | "never supplies the external concern behind a seam". |

## Deviations recorded outside a phase

The collection point for a deviation found by a review, an incident or a consumer between phases: add the row here, with the evidence, and route the repair to its owner. Deliberately uncounted until a
phase or the lead rules on it.

| Deviation | Found by | Date | IDs | Evidence | §10 status |
|---|---|---|---|---|---|
