# Phase 2b — Seams: Checklist

The execution-time checklist for sub-phase 2b of roadmap phase 2
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md), [segmentation design](../2026-09-29-phase2-segmentation-design.md)):
one row per requirement ID, in the legend of the roadmap's cross-cutting constraint 3. Written from what was built on
branch `40-phase-2b-seams`, stacked on `39-phase-2a-domain-model`, following the
[design](2026-09-30-phase2b-seams-design.md) and the [plan](2026-09-30-phase2b-seams.md) (GitHub issue
[#40](https://github.com/dexpace/dotnet-sdk/issues/40)).

The scope is 29 rows: `SEAM-1`–`SEAM-28` and `SEAM-30`. Every test below is `[Trait("Category", "Unit")]` and lives in
`tests/Dexpace.Sdk.Core.Tests/` unless a project is named; phase 2b adds no `Security` class. **Red evidence:** for a new
type or a changed signature in PRs 1 to 4 the red was the compile error (the plan's convention 1). PR 5 is the exception:
its production types (`OperationDescriptor`, `PathTemplateSyntax`, `OperationUrlComposer`, `BuildRequest`) were written before
their tests, so `OperationDescriptorTests` and `OperationBuildRequestTests` were never seen red. Break-proofs (temporary breaks, never committed) were run afterwards for the rendered-dot-segment check, the
lone-surrogate check (5 failures), the ordinal re-basing (1 failure) and the `PathTemplate` null check (1 failure); the
other PR 5 tests are pins whose failure on a break was not demonstrated. Behavioural reds were observed for
`AsBlocking` (task 3.3: three failures on the as-built adapter), for the `SEAM-1`, `SEAM-2` and `SEAM-30` guards and for
a sample of the pins (below). A test marked "pin" holds behaviour that was already correct.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `Client/{HttpClientExtensionsTests,SeamSurfaceTests,SeamParameterTests,AsyncToSyncBridgeTests,SyncToAsyncBridgeTests,DelegateHttpClientTests,ConcurrencyTests}.cs`,
`Errors/SerdeExceptionHierarchyTests.cs`, `Serialization/{SerdeProfileTests,SerdeSeamTests}.cs`,
`Operations/{OperationDescriptorTests,OperationBuildRequestTests}.cs`, `Architecture/{Seam1,SeamImplementation,SerdeSeam}ArchitectureTests.cs`,
`Pipeline/PipelineRunnerTests.cs`, `Support/{RecordingTaskScheduler,DisposalCountingBody}Tests.cs`, `Configuration/DexpaceClientOptionsTests.cs`;
in `tests/Dexpace.Sdk.Http.SystemNet.Tests/`: `SystemNetHttpClientOwnershipTests.cs`, `SystemNetHttpClientSurfaceTests.cs`,
`SystemNetHttpClientTests.cs`; in `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/`: `SystemTextJsonSerdeTests.cs`.

| ID | Level | Status | What was built | Proven by |
|---|---|---|---|---|
| `SEAM-1` | MUST | ✅ | Already met; the "no concrete capability" half is pinned over the compiled assembly, the allow-list half is `scripts/ci/dependency-audit.cs` on every pack. Design §2.4, §10 `logging-abstractions-dependency`. | `Seam1ArchitectureTests.Core_references_no_transport_or_codec_assembly` (pin; proven by a scratch `System.Net.Http.HttpMethod` reference in core: red) |
| `SEAM-2` | MUST | ✅ (reading P2b-1) | Core ships adapters over a seam, never the concern behind one; every core type implementing a seam is non-public and on a named allow-list: `HttpClientExtensions+SyncToAsyncAdapter`, `+AsyncToSyncAdapter`, `DelegateHttpClient+AsyncAdapter`, `+BlockingAdapter`. 4c adds `HttpPipeline` (`PIPE-26`). | `SeamImplementationArchitectureTests.Core_implements_a_seam_only_through_the_allow_listed_adapters` (proven by a scratch `ISerde` implementer: red), `The_allow_list_names_only_types_that_exist`; cites `Seam2ArchitectureTests` |
| `SEAM-3` | MUST | N/A | The byte-stream provider seam is retired; `Stream`, `Memory<byte>`, `IBufferWriter<byte>` and `ArrayPool<byte>` are the framework's. Design §3.1, §10 `byte-stream-provider-retired`. | none of its own |
| `SEAM-4` | MUST | N/A | As `SEAM-3`: there is no factory to make concurrency-safe. Design §3.1, §10 `byte-stream-provider-retired`. | none |
| `SEAM-5` | MUST | N/A ⏳ | N/A for the DI-less half: a transport or codec is a non-nullable parameter, so zero candidates is a compile error and a null one is rejected by name. The DI half is ⏳ phase 9 (`ValidateOnStart`). Design §3.6, §10 `no-provider-registry`, §11 item 14. | `SeamParameterTests.CreateDefault_rejects_a_null_transport_naming_the_parameter`, `RequestBody_FromValue_rejects_a_null_serde_naming_the_parameter` (pins) |
| `SEAM-6` | MUST | N/A ⏳ | N/A for the DI-less half (no install call to be idempotent); ⏳ phase 9 for `TryAdd` and the validator. Design §3.6, §10 `no-provider-registry`. | none in 2b |
| `SEAM-7` | MUST | N/A | No discovery scan exists to cache; under DI, caching is the singleton lifetime (phase 9). Design §3.6, §10 `no-provider-registry`. | none |
| `SEAM-8` | SHOULD | N/A (vacuous) | No auto-resolved provider exists to be replaced. Design §3.6, §12's vacuous list. | none |
| `SEAM-9` | MUST | N/A | No resolution, install or swap state to publish. Design §3.6, §10 `no-provider-registry`. | none |
| `SEAM-10` | SHOULD | N/A (vacuous) | One default load context, no registry to de-duplicate. Design §3.6, §12's vacuous list. | none |
| `SEAM-11` | MUST | ✅; ⏳ 8b | `Execute` / `ExecuteAsync` take `RequestOptions` and a token, no defaults; the option-less extensions pass `RequestOptions.Empty`; a bare send function is a transport through `DelegateHttpClient`. The body is never pre-buffered (documented). The real synchronous path is ⏳ 8b (`SystemNetHttpClient.Execute` is sync-over-async, options ignored); the per-transport no-pre-buffering proof is ⏳ 8a (`TRANSPORT-25`). | `HttpClientExtensionsTests` (`The_option_less_ExecuteAsync_passes_RequestOptions_Empty_and_the_token`, `The_option_less_Execute_…`, `The_option_less_calls_default_the_token_to_none`, `The_option_less_Execute_works_over_a_DelegateHttpClient`); `SeamSurfaceTests.The_sync_seam_takes_a_token_and_options`; `DelegateHttpClientTests.A_bare_send_lambda_works_as_a_transport`, `A_bare_blocking_lambda_works_as_a_blocking_transport`, `An_options_ignoring_transport_returns_the_same_response_with_and_without_options`; `SyncToAsyncBridgeTests.An_options_ignoring_client_behaves_identically_with_and_without_options`; `SystemNetHttpClientTests.Options_are_accepted_and_ignored_until_8b` (`Unit`, SystemNet) |
| `SEAM-12` | MUST | ✅; ⏳ 8a | Both interfaces document the concurrency contract; core's own implementations keep per-call state in locals. The per-transport proof is ⏳ 8a's kit. | `ConcurrencyTests.No_cross_talk_through_the_bridges`, `No_cross_talk_through_both_DelegateHttpClient_forms` (64 concurrent calls, each response paired with its own request and options; pin, proven red by a bridge caching the last request in a field) |
| `SEAM-13` | SHOULD | ✅; ⏳ 8a, 8b | The synchronous seam gains its token; both bridges pass the call's token into the wrapped call. Honouring it inside blocking socket I/O is ⏳ 8b; the per-transport async abort proof is ⏳ 8a. §10 `cooperative-cancellation`. | `AsyncToSyncBridgeTests.Cancelling_the_token_cancels_the_in_flight_task_and_surfaces_OperationCanceledException`, `Options_and_token_reach_ExecuteAsync_by_reference`; `SyncToAsyncBridgeTests.The_exact_options_and_token_instances_reach_Execute`; `SystemNetHttpClientTests.The_sync_Execute_passes_the_token_through` (`Unit`, SystemNet) |
| `SEAM-14` | MUST | ✅ *(⏳ 3b closed by dated correction, 2026-10-03: see below)* | Ownership: the adapter never disposes a caller-supplied `HttpClient` and disposes its owned one; neither bridge disposes what it wraps; `DelegateHttpClient`'s dispose is a no-op. Repeated dispose is safe today through `HttpClient`'s own idempotence; the SDK-owned latch is ⏳ 3b. Clause (3), interrupt-safety, maps to cancellation (§10 `cooperative-cancellation`). | `SystemNetHttpClientOwnershipTests` (5 tests, `Unit`, SystemNet; proven red by disposing a borrowed client); `AsyncToSyncBridgeTests.Disposing_the_bridge_never_disposes_the_wrapped_client`; `SyncToAsyncBridgeTests.Disposing_the_bridge_disposes_neither_the_scheduler_nor_the_client` |
| `SEAM-15` | MAY | ⏳ 8b | The post-dispose mode is documented on both interfaces and on each implementation: `DelegateHttpClient` and the bridges keep working, an owned `HttpClient` throws `ObjectDisposedException`, a borrowed one keeps working. 8b's latch makes both throw. | `DelegateHttpClientTests.It_keeps_working_after_dispose`; `SystemNetHttpClientOwnershipTests.An_owned_client_is_disposed_with_the_transport`, `A_borrowed_client_still_sends_after_the_transport_is_disposed` |
| `SEAM-16` | MUST | ✅ | The async seam returns the non-nullable `Task<Response>`; `PipelineRunner` converts a `null` result into `PipelineAbortedException` before any policy sees it; `DelegateHttpClient`, `AsAsync` and `AsBlocking` reject a `null` task or result with `InvalidOperationException`. A signalled token after delivery touches nothing. | `PipelineRunnerTests.A_null_response_from_the_transport_fails_at_the_runner_before_any_policy_sees_it`; `DelegateHttpClientTests.A_null_result_faults_the_task_with_InvalidOperationException`, `A_null_task_faults_with_InvalidOperationException`, `CreateBlocking_null_result_throws_InvalidOperationException`, `Cancelling_after_delivery_leaves_the_response_readable`; `AsyncToSyncBridgeTests.A_null_Task_or_a_null_Response_from_the_wrapped_client_fails_with_InvalidOperationException`; `SyncToAsyncBridgeTests.A_null_result_from_the_wrapped_client_faults_with_InvalidOperationException`, `Cancelling_after_delivery_leaves_the_response_readable` |
| `SEAM-17` | SHOULD | ✅ (second sentence vacuous, P2b-3) | The pivot is `Task<Response>`, verbatim. The second sentence (ecosystem facades as separate adapter modules) is vacuous: .NET has one async ecosystem, so no facade module ships. | `SeamSurfaceTests.The_async_seam_returns_Task_of_Response` |
| `SEAM-18` | MUST | ✅ | `AsAsync(IHttpClient, TaskScheduler)`, no zero-scheduler overload, `LongRunning \| DenyChildAttach`, options and token threaded in; `AsBlocking` blocks with `GetAwaiter().GetResult()` (unwraps) and threads options and token; neither disposes what it wraps. The interrupt clause maps to the token (§10 `cooperative-cancellation`). `TaskScheduler.Default` accepted (ruling 2). | `SyncToAsyncBridgeTests` (`No_AsAsync_overload_lacks_a_TaskScheduler`, `A_null_scheduler_throws_ArgumentNullException`, `The_call_runs_on_the_given_scheduler`, `TaskScheduler_Default_is_accepted`, `Argument_errors_are_delivered_through_the_returned_task_not_thrown_synchronously`); `AsyncToSyncBridgeTests.A_faulted_InvalidOperationException_surfaces_as_itself_not_as_an_AggregateException`; `SyncToAsyncBridgeTests` ran 20 times in a loop, no failure |
| `SEAM-19` | MUST | ✅ | Already met: `DefaultMediaType` is a required property with no default implementation; `FromValue` stamps it when no media type is given. | `SerdeSeamTests.DefaultMediaType_has_no_default_implementation`, `RequestBody_FromValue_stamps_the_codecs_own_media_type` (proven red by ignoring the codec's type), `FromValue_lets_an_explicit_media_type_win` |
| `SEAM-20` | MUST | ✅ | The seam keeps its two primitives; `SerdeExtensions` adds `SerializeToUtf8Bytes`, `SerializeToString` (UTF-8, or the codec's own string through the optional `IStringSerde`, ruling 4) and the fixed-buffer `Serialize(Span<byte>, T)` (overflow: `ArgumentOutOfRangeException`, destination untouched). Encode failures come from the primitive and are not re-wrapped; a stream `IOException` propagates. | `SerdeProfileTests` (11); `SystemTextJsonSerdeTests.SerializeAsync_leaves_the_destination_open` (proven red by disposing the destination), `An_IOException_from_the_destination_propagates_unwrapped`, and the cycle and malformed-JSON tests now assert the chained cause (`Unit`, STJ) |
| `SEAM-21` | MUST | ✅ (no-codec clause vacuous, P2b-3) | Already met: `DeserializeAsync<T>` is the explicit, reified type token; decode failures are `DeserializationException` chaining the cause; a read `IOException` propagates; the stream stays open. Core ships no format-agnostic deserializer (`SEAM-1`), so the no-codec clause is vacuous. | `SystemTextJsonSerdeTests.DeserializeAsync_leaves_the_source_open`, `An_IOException_from_the_source_propagates_unwrapped`, `Deserialize_malformed_json_throws_DeserializationException` (`InnerException` is `JsonException`) (pins) |
| `SEAM-22` | MUST | ✅ by construction (P2b-2) | The capture is the generic argument itself, which the CLR binds to a closed type; no `Type`-taking decode overload exists, and a tripwire keeps it so. | `SerdeSeamArchitectureTests.No_seam_member_takes_a_System_Type`; `SystemTextJsonSerdeTests.A_generic_helper_decodes_into_the_closed_type` (`Unit`, STJ) |
| `SEAM-23` | MUST | ✅ | New abstract `SerdeException : SdkException` with protected constructors; both subtypes unsealed and re-parented under it (ruling 1). Breaking item 1. | `SerdeExceptionHierarchyTests` (8) |
| `SEAM-24` | SHOULD | ✅ (adapter clause vacuous, P2b-3) | `ExecutionContext` flows through `Task.Factory.StartNew` on any scheduler, so `AsyncLocal<T>` and `Activity.Current` reach the worker; cancellation maps both ways through the one token. | `SyncToAsyncBridgeTests.The_callers_AsyncLocal_and_Activity_reach_the_worker_thread` |
| `SEAM-25` | MUST | ✅ (first sentence vacuous, P2b-3) | `AsAsync` takes the caller's scheduler and nothing in the SDK owns one, so the owning-adapter sentence is vacuous; disposing the bridge touches neither the scheduler nor the wrapped client. | `SyncToAsyncBridgeTests.Disposing_the_bridge_disposes_neither_the_scheduler_nor_the_client` |
| `SEAM-26` | MUST | ✅ | `sealed record OperationDescriptor`: `required` `Method` and `PathTemplate`, four typed projections defaulting to empty (ruling 5), validation in `field`-backed `init` accessors, body by reference, literal dot-segments rejected (P2b-7). | `OperationDescriptorTests` (`Method_and_PathTemplate_are_required_members`, `A_parameterless_GET_needs_only_Method_and_PathTemplate`, `PathTemplate_validation_throws_ArgumentException_naming_the_template` (20 rows, initializer and `with`), `Dots_inside_a_segment_are_not_dot_segments`, `Placeholder_names_are_any_brace_free_text_compared_ordinally`, `PathParameters_reject_*`, `PathParameters_are_re_based_on_ordinal_keys`, `WithPathParameter_adds_and_replaces_without_mutating_the_original`, `Equality_compares_PathParameters_by_content_and_delegates_the_rest`, `ToString_shows_method_and_template_never_values`); `OperationBuildRequestTests.The_built_request_carries_method_headers_and_the_same_body_instance`, `Each_projection_lands_in_its_request_part`; `ModelImmutabilityArchitectureTests` (now also over `OperationDescriptor`) |
| `SEAM-27` | MUST | ✅ | `BuildRequest(Uri)` and `BuildRequest(DexpaceClientOptions)` compose over the base's components with the internal `Rfc3986` encoder, never `new Uri(base, relative)`; a rendered dot-segment, a missing placeholder and a stray parameter are `InvalidOperationException` (ruling 6); a rejected base carries `UrlRedactor`'s text. `BaseAddress` is now read. | `OperationBuildRequestTests.BuildRequest_matches_the_composition_vectors` (31 cases of `tests/vectors/seam/operation-compose.json`; proven red by removing the rendered-dot-segment check), `A_missing_placeholder_message_names_the_placeholder_and_never_a_value`, `A_rejected_base_message_carries_the_base_through_UrlRedactor`, `BuildRequest_rejects_a_null_baseAddress`, `BuildRequest_from_options_*` (four tests, six cases); `DexpaceClientOptionsTests.BaseAddress_is_read_by_BuildRequest`; `VectorFileTests.Every_vector_file_names_its_source_path_and_sha` |
| `SEAM-28` | MAY | ✅; ⏳ 4a | `OperationId` (`string?`; non-blank when set) never reaches the URL, headers or body. Attaching it to the context chain is ⏳ 4a. | `OperationDescriptorTests.OperationId_null_is_accepted_and_blank_is_rejected`, `The_operation_id_never_reaches_the_url_headers_or_body` |
| `SEAM-30` | MUST | ✅ | Core has no `TaskCompletionSource<Response>`; `AsAsync` checks after return, disposing a response produced once the token is signalled and completing cancelled (ruling 3, P2b-4); `BannedSymbols.txt` bans `TaskCompletionSource<T>.SetResult`/`TrySetResult` and the five `Task<T>.WaitAsync` overloads (the non-generic forms stay allowed). The producer-internal half is phase 1 S9's guard. | `SyncToAsyncBridgeTests.A_response_produced_after_cancellation_is_disposed_exactly_once_and_never_surfaces`, `A_token_cancelled_before_the_send_never_runs_it`, `A_throwing_dispose_after_cancellation_faults_the_task_with_that_exception` (3b replaces it); the ban probe (7 `RS0030` for 7 symbols, none for `SemaphoreSlim.WaitAsync` or the non-generic forms); cites `MalformedContentTypeWireTests` (`Security`, S9) |

> **Dated correction, 2026-10-03 (phase 3b).** The ⏳ 3b clause of `SEAM-14` is closed: `SystemNetHttpClient.Dispose` is latched, so the owned `HttpClient` is released at most once across `Dispose` and `DisposeAsync`, proven by `SystemNetHttpClientDisposeTests` (`An_owned_client_disposes_its_HttpClient_once_across_Dispose_and_DisposeAsync`, `A_borrowed_client_is_never_disposed_by_either_form`, `Concurrent_disposes_are_safe`) and, for the adapters, `ClientDisposeIdempotenceTests.Bridge_and_delegate_disposes_are_idempotent`. `SEAM-15` (`ObjectDisposedException` after dispose) stays 8b's.

## Exit criteria

- [x] All 29 rows carry a mark: 20 ✅ (several with an ⏳ half owned by 3b, 4a, 8a or 8b), `SEAM-15` ⏳ 8b (its mode is
  documented and pinned in 2b), `SEAM-3`, `SEAM-4`, `SEAM-7` and `SEAM-9` N/A, `SEAM-5` and `SEAM-6` N/A with the DI half
  ⏳ phase 9, `SEAM-8` and `SEAM-10` N/A vacuous; the vacuous clauses of `SEAM-17`, `SEAM-21`, `SEAM-24` and `SEAM-25` are
  named inside their ✅ rows.
- [x] No `Security` class was edited (below); the four wire classes and the core `Security` classes are byte-identical.
- [x] `PublicAPI.Unshipped.txt` diffs for core and `SystemNet` are in the commits that make each change, under
  `RS0016`/`RS0017`; `PublicAPI.Shipped.txt` is still empty.
- [x] `CHANGELOG.md` `[Unreleased]` carries a line per change, prefixed **Breaking:** where design "Breaking changes" lists
  it (items 1 to 7).
- [x] User page `docs/sdk-documentation/seams.md` is written ("As built by phase 2b … written against source on
  2026-10-02").
- [x] Dated corrections to design §3.2–§3.5, §11 (items 41–43 and the correction to item 35) and §12; roadmap Phase Status
  Note appended; the housekeeping probe reports no drift.

## Existing assertions changed

### `Security` classes (constraint 5)

| File | Diff |
|---|---|
| `Http.SystemNet.Tests/Security/FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `RedirectWireTests`, `MalformedContentTypeWireTests` | **unedited.** They keep calling `transport.ExecuteAsync(request, ct)`, which binds to the new option-less extension once the project's `GlobalUsings.cs` imports `Dexpace.Sdk.Core.Client` (design position I) |
| `Core.Tests/Security/*` (all eight classes) | **unedited.** The fakes they drive changed signature inside `TestSupport` only |

Evidence: `git diff --stat 39-phase-2a-domain-model...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
is empty (against `main` it lists 2a's own mechanical edits, recorded in the phase 2a checklist).

### Other tests

| Test | Was | Now | Why |
|---|---|---|---|
| `SystemTextJsonSerdeTests.Deserialize_malformed_json_throws_DeserializationException`, `SerializeAsync_reference_cycle_throws_SerializationException` | no assertion on the cause | `Assert.IsType<JsonException>(ex.InnerException)` added | `SEAM-20`/`SEAM-21` chained cause (task 1.3) |
| `SystemTextJsonSerdeTests.Serialize_reference_cycle_throws_SerializationException` | no assertion on the cause | `Assert.NotNull(ex.InnerException)` added | the synchronous writer reports a cycle as `InvalidOperationException`, not `JsonException`; the clause under test is that the cause is chained |
| `TestJsonContext` (STJ tests) | | gains `[JsonSerializable(typeof(List<Widget>))]` | the generic-helper test needs the closed type registered |
| `OperationPolicyTests.HangingTransport`, `AotSmoke` `EchoTransport` | `ExecuteAsync(Request, CancellationToken = default)` | `ExecuteAsync(Request, RequestOptions, CancellationToken)` | signature only (design fact 2) |
| `AsyncToSyncBridgeTests.Cancelling_the_token_…` | a test-local delegate transport | `DelegateHttpClient.Create` | task 4.2; the null-task and null-result tests keep test-local transports, see finding 7 |
| `Http.SystemNet.Tests/GlobalUsings.cs` | | `global using Dexpace.Sdk.Core.Client;` | design position I; no other file in that project was edited for the SPI change |
| `TestSupport` fakes (`RecordingTransport`, `RecordingSyncTransport`, `ScriptedTransport`, `RequestLog`) | recorded requests | record `RecordedCall`s; `Requests` projects them, so no existing test changed; new `Calls`, `LastCall` | tasks 3.2 |
| `ModelImmutabilityArchitectureTests.ModelTypes` | 12 types | `OperationDescriptor` added | task 5.5 (2a's PR 9 had already landed) |
| `PipelineRunnerTests` | | three tests added | task 3.2 |

## Deviation ledger, as built

| ID | As built | Route |
|---|---|---|
| P2b-1 | `SEAM-2`'s "never implements" is read as "never supplies the external concern"; the four private adapters are on the allow-list. | design §11 item 41 |
| P2b-2 | `SEAM-22` is ✅ by construction, pinned by a tripwire. | design §11 item 42 |
| P2b-3 | The vacuous clauses of `SEAM-17`, `SEAM-21`, `SEAM-24` and `SEAM-25` are named inside ✅ rows. | design §12, dated correction |
| P2b-4 | `AsAsync` disposes a response produced after the token is signalled and completes cancelled; a throwing `Dispose` faults the task until 3b. | design §11 item 43 |
| P2b-5 | Withdrawn by ruling 4: the string profile follows design §3.4 (UTF-8, optional `IStringSerde`). | design §3.4 "As built" names the interface |
| P2b-6 | `DelegateHttpClient.Create` and `CreateBlocking`, not two `Create` overloads (`CS0121`). | design §3.2 "As built" |
| P2b-7 | A dot-segment is rejected in the template's literal text at construction and in a rendered segment at `BuildRequest`. | design §11 item 35, dated correction |

## Findings while building

The plan's own "Findings" section was resolved at review and is not repeated. What building added:

1. **2a had landed all nine steps before 2b started**, so every gate of the plan was already met: `RequestOptions`, `Query`,
   `Rfc3986`, `VectorFile`, a reference-type `Method`, `Headers` and `RequestBody` value equality, the validating `Request`,
   `UrlRedactor.Default` and `TestResponses.Create` all existed. The fallbacks of the plan's gate paragraphs (the deferred
   `Method` null check, reference equality over `Headers` and `Body`, adding `UrlRedactor.Default`) were not needed; the
   `Method` null check and value equality are built, and PR 3's interleaving rule did not apply.
2. **Vector source.** `tests/vectors/seam/operation-compose.json` cites `nodejs-sdk@c0ff3fd`
   (`packages/core/src/seams/operation.test.ts`): the plan's `nodejs-sdk@54aeed4` and `ruby-sdk@5b17395` are not in the
   local clones, and the Ruby repository holds documents only. Cases beyond Node's come from the plan's required list, and
   every expected value was checked against `System.Uri` on 10.0.401 by the passing suite.
3. **Test namespaces.** `Dexpace.Sdk.Core.Tests.Errors`, `…Client`, `…Serialization` would shadow the `Errors` and
   `Serialization` namespaces and the `Client` folder's types for older tests, so the new classes use `…Tests.Exceptions`,
   `…Tests.Clients` and `…Tests.Serdes` (the folders mirror the source). `…Tests.Operations` is unchanged.
4. **`BytesSerde` is sealed**, as the plan says, so `StringOverrideSerde` composes one instead of subclassing it.
5. **`DisposalCountingBody` overrides only `Dispose`**: the base class routes `DisposeAsync` through it, and `CA2215`
   rejects an override that does not call the base.
6. **The synchronous cycle failure** in `SystemTextJsonSerde.Serialize` chains an `InvalidOperationException`, not a
   `JsonException`, so that one assertion checks the cause is present rather than its type.
7. **Masking.** The null-task and null-result tests of the two bridges keep test-local transports: `DelegateHttpClient`
   rejects a null itself, so over it the bridge's own check would never run. Only the cancellation transport moved.
8. **`HttpClientExtensions.ExecuteAsync` and `ASYNC-2`.** The option-less async extension throws for a null client (nothing
   to deliver through) and delivers a null request through a faulted task; the synchronous `Execute` throws for both.
9. **xUnit theory data cannot carry a lone surrogate** (its serialisation replaces it with U+FFFD), so the lone-surrogate
   tests build the value in the test body from a case name.
10. **Pin proofs.** Per the plan's convention 1, a sample of the pins was proven able to fail by a temporary mutation
    (never committed): the `SEAM-1` and `SEAM-2` guards, `ConcurrencyTests`, `SerdeSeamTests`, the STJ `leaves_the_destination_open`
    pin, the ownership tests and the rendered-dot-segment vector. The remaining pins were not individually mutated.
11. **PR 5 order.** The production types for `OperationDescriptor` were written before their tests, so those tests were not
    observed red; the reds that were observed are listed above.
12. **The ban probe** fired seven `RS0030` for the seven symbols, and none for `SemaphoreSlim.WaitAsync`
    (`AccessTokenCache`) or the non-generic `Task.WaitAsync` and `TaskCompletionSource`.
13. **`Task.Factory.StartNew`** with an explicit scheduler passes the repository analyzers with no suppression (`CA2008`).

## Hand-offs

3b replaces `AsAsync`'s direct `Dispose()` with `DisposeQuietly` and its test, and builds the dispose latches; 4a attaches
`OperationId`; 4c adds `HttpPipeline` to the allow-list, carries a caller's `RequestOptions` on the context and replaces
`The_transport_receives_RequestOptions_Empty`; 5a may move `BaseAddress` validation to construction; 7a builds on
`SerdeException` and the profiles; 8a drives the kit against `DelegateHttpClient` and owns the per-transport proofs; 8b
builds the real synchronous send, the `ObjectDisposedException` latch and `RequestOptions.Timeout`; 9 flips the DI half of
`SEAM-5`/`SEAM-6`.
