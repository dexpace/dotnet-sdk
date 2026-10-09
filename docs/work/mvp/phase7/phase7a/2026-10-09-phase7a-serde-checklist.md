# Phase 7a — Serde: Checklist

The execution-time checklist for sub-phase 7a of roadmap phase 7
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `79-phase-7a-serde` (issue #79), following the
[design](2026-10-09-phase7a-serde-design.md) and the [plan](2026-10-09-phase7a-serde.md). User page:
[`serde.md`](../../../../sdk-documentation/serde.md).

The scope is 32 rows: `SERDE-1`-`SERDE-30` (22 MUST, 7 SHOULD, 1 MAY) and the two rows 3b handed over, `HTTP-44` and `HTTP-45` (carried
from 3b, P7a-25; they sit outside the roadmap's exit count of 107 for phase 7, and the 3b checklist's ⏳ cells now point here). **32 ✅**,
0 🚫, 0 ⏳, 0 N/A. `SERDE-14`'s covariance SHOULD is unmet by design §10 entry 21 and recorded as ✅ with that citation (the MUST part is met).
Two rulings are **open for the lead** and were implemented as the design argues them: P7a-9 (`SERDE-13` does not bind `ISerde`'s own `T?`
members) and P7a-21 (the per-read materialisation cap is declined). 7a also did the work for six rows owned elsewhere, listed in the
[cross-owner table](#work-on-other-owners-rows). Every test is `[Trait("Category", "Unit")]` unless noted, and lives in
`tests/Dexpace.Sdk.Core.Tests/` (**Core**) or `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/` (**STJ**). Phase 7a **adds one `Security`
class** (`StatusAwareHandlerLocationRedactionTests`) and edits none.

**Adaptation to the as-built code.** The plan's five PRs landed as commit groups on the one branch the brief named (`79-phase-7a-serde`), in
the plan's order (plan reading R13 proposed a topic branch per PR; the branch scheme was not followed because the work was done as one
unit): `Tristate<T>` (PR 1), the System.Text.Json wiring and options (PR 2), the typed readers and the sync stream decode (PR 3), the
handlers and `TypedResponse<T>` (PR 4), the AOT smoke, documentation and close-out (PR 5). The deviations from the plan are listed
[below](#deviations-from-the-plan).

**Task 0.1 results** (SDK 10.0.401, linux-x64, a throwaway project outside the repository; nothing contradicted a ruling):

- **Fact 8 holds for the default mode.** A default-generation-mode source-generated context with a `WithAddedModifier` that sets
  `ShouldSerialize` serialises `new Patch(default, default)` as `{}` and `(Null, Present(3))` as `{"Name":null,"Size":3}`. A context generated
  with `GenerationMode = Serialization` (fast path only) **throws** `InvalidOperationException` ("did not provide property metadata for type
  'Patch'") and does so even with *no* modifier, as soon as the options object is not the context's own. The R1 mitigation is therefore a
  documented limit, not a fallback: such a context cannot carry Tristate models (and, because the serde now copies the options, cannot be
  used with the serde at all; see deviation 3).
- **Fact 9 holds.** A value-type converter's `Read` receives the `Null` token at the root (`Null`), as an array element (`[Null, String]`) and
  as a property value, with `HandleNull` overridden to `true`; a missing key never reaches it (`Name=Absent Size=Absent`). Writing: an array
  `["a", Absent, Null]` gives `["a",null,null]`, a dictionary `{"a": Absent, "b": "x"}` gives `{"a":null,"b":"x"}`.
- **Fact 10 holds.** `static T Req<T>(T? v) => v ?? throw …` and `await ReadNonNull<TPage>() ?? throw …` over a `ValueTask<T>` build with no
  warning under `TreatWarningsAsErrors` in a nullable-enabled project, so `Pageable.cs` compiled untouched. `Wrapper<string> s = null` binds the
  `T?` operator (Null), `Wrapper<int> i = 3` is Present, and the sentinel conversion is unambiguous.
- **Fact 11 holds, with numbers.** Eight threads on `Lazy<Task<int>>` (`ExecutionAndPublication`) over a factory that sleeps 500 ms each waited
  about 500 ms (`[500,500,500,500,500,500,500,500]`, `[505,504,503,505,507,508,505,500]` under NativeAOT); on the compare-and-swap of P7a-15 only the
  winner did (`[500,0,0,0,0,0,0,0]`). This is the numeric justification for P7a-15.
- **Fact 3 (AOT) holds.** `RuntimeHelpers.GetUninitializedObject(typeToConvert)` followed by the interface visitor reports `IL2067` naming
  `GetUninitializedObject` and the parameter `typeToConvert`; with the one `[UnconditionalSuppressMessage]` the `PublishAot` output has zero
  warnings, and the native binary round-trips a value-type `Wrapper<int>`.
- **R9 holds.** `new JsonSerializerOptions(ctx.Options)` is mutable (`IsReadOnly == false`), keeps the context as `TypeInfoResolver`, and still
  resolves `GetTypeInfo(typeof(Patch))` after `WithAddedModifier`; the context's own `Options.IsReadOnly` is `true`.
- **The knowledge queries matched the design:** 30 of 30 `SERDE` IDs substantive, no gaps, no serde or Tristate note in the corpus, the
  recorded conflicts unrelated.

**Red evidence.** For each new type the first compile of the test project was red (a missing type or member). Pins proven able to fail by
temporary mutation, each reverted and never committed: `TristateArchitectureTests` (a `[JsonConverter]` on `Tristate<T>` fails the attribute and
the assembly-reference tests); the copying constructor (reverting to `options.MakeReadOnly()` on the caller's instance fails five
`SerdeOptionsTests` facts); `StrictCoercionTests` (dropping `NumberHandling = Strict` from `CreateDefaultOptions` fails
`String_five_does_not_bind_to_int` and `String_1_5_does_not_bind_to_double` under the default serde, exactly the two the plan names); and
`StatusAwareHandlerLocationRedactionTests` (interpolating the raw `Location` fails the three secret and userinfo tests; the HTAB/obs-text test is a
pin on single-line rendering and stays green, as planned). `TypedResponseTests` ran 200 times in a loop with no failure (the plan's guard against a
scheduling-dependent pass).

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files (Core): `Serialization/{TristateTests,TristateSentinelTests,SerdeStreamDefaultTests,SerdeValuesTests,ReadValueTests,ResponseHandlersTests,SerdeSeamTests,SerdeProfileTests}.cs`;
`Architecture/{TristateArchitectureTests,SerdeSeamArchitectureTests,ModelConstructionArchitectureTests}.cs`; `Errors/HttpResponseExceptionGetErrorTests.cs` and
`Errors/SerdeExceptionHierarchyTests.cs`; `Http/Response/TypedResponseTests.cs`; `Security/StatusAwareHandlerLocationRedactionTests.cs`. Test files (STJ):
`{SystemTextJsonSerdeTests,TristateJsonTests,SerdeOptionsTests,StrictCoercionTests,ReadValueEndToEndTests,BodyConvenienceTests}.cs` with models and contexts in `TristateModels.cs`
(the contexts are in the default generation mode, plus one `Metadata` and one fast-path-only context for the R1 tests). The AOT smoke check is `CheckPhase7aSerdeAsync` in
`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`. Doubles added to `tests/Dexpace.Sdk.TestSupport/`: `Utf8LiteralSerde`, `DefaultImplementationSerde`, `CountingPayloadBody`,
`GateResponseHandler<T>`, `DelegateResponseHandler<T>`.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `SERDE-1` | MUST | ✅ (already met) | `ISerde` bundles both directions; STJ `SystemTextJsonSerdeTests.SerializeAsync_then_DeserializeAsync_round_trips`, `Serialize_then_Deserialize_sync_round_trips` (Unit) |
| `SERDE-2` | MUST | ✅ (already met, 2b) | Core `SerdeSeamTests.DefaultMediaType_has_no_default_implementation`, `RequestBody_FromValue_stamps_the_codecs_own_media_type`, `FromValue_lets_an_explicit_media_type_win` (Unit) |
| `SERDE-3` | MUST | ✅ (test widened) | STJ `SystemTextJsonSerdeTests.A_close_counting_stream_sees_zero_closes_across_all_three_stream_members` (`SerializeAsync`, `DeserializeAsync` and the new sync `Deserialize(Stream)`), `Sync_Deserialize_from_a_stream_decodes_and_leaves_it_open`, `SerializeAsync_leaves_the_destination_open`, `DeserializeAsync_leaves_the_source_open`; Core `SerdeStreamDefaultTests.The_default_leaves_the_stream_open`, `ResponseHandlersTests.SERDE_3_and_the_handler_dispose_bind_different_objects` (the codec closes nothing; the handler's response dispose closes the stream once) (Unit) |
| `SERDE-4` | MUST | ✅ (already met, 2b) | Core `SerdeProfileTests.Fixed_buffer_serialize_returns_the_count_and_writes_at_the_offset`, `Fixed_buffer_serialize_of_an_exact_fit_succeeds`, `Fixed_buffer_overflow_throws_ArgumentOutOfRangeException_and_leaves_every_byte_untouched` (Unit) |
| `SERDE-5` | MUST | ✅ (by construction + test) | STJ `SystemTextJsonSerdeTests.A_dto_field_access_returns_typed_values`, `A_generic_helper_decodes_into_the_closed_type`, `A_generic_helper_decodes_into_the_closed_Tristate_type`; reified generics, design §3.4 and §7.3 (Unit) |
| `SERDE-6` | MUST | ✅ (by construction + test) | STJ `SystemTextJsonSerdeTests.A_list_of_dto_decodes_typed_and_an_unregistered_parametric_target_throws_naming_it` (`List<Widget>` decodes element-typed; `List<ApiError>` is unregistered and throws a `DeserializationException` naming it: on this host a missing `JsonTypeInfo`, §7.3), `Deserialize_unknown_type_throws_DeserializationException` (Unit) |
| `SERDE-7` | MUST | ✅ (by construction) | `ReadValueAsync<T>` / `ReadValue<T>` are the reified helpers; Core `ReadValueTests.A_value_decodes_through_a_stream` (+`_sync`); STJ `ReadValueEndToEndTests.A_typed_read_returns_the_value`, `A_typed_sync_read_streams_through_the_adapters_override` (Unit) |
| `SERDE-8` | MUST | ✅ (by construction) | Core `SerdeSeamArchitectureTests.No_seam_member_takes_a_System_Type`, now over `ISerde` (six members), `ResponseBodySerdeExtensions` and `ResponseHandlers` too (plan R6) (Unit) |
| `SERDE-9` | MUST | ✅ (extended) | Core `ReadValueTests.A_codec_DeserializationException_is_the_same_instance` (+`_sync`), `A_malformed_payload_surfaces_the_codecs_own_inner_exception`; STJ `SystemTextJsonSerdeTests.Sync_Deserialize_from_a_stream_wraps_malformed_input`, `Deserialize_malformed_json_throws_DeserializationException`, `TristateJsonTests.A_value_of_the_wrong_type_is_a_DeserializationException`, `A_non_null_token_whose_inner_result_is_null_is_a_JsonException` (the Tristate converter's failures wrap like any other) (Unit) |
| `SERDE-10` | MUST | ✅ (already met, 2b) | Core `SerdeExceptionHierarchyTests` (all eight facts) (Unit) |
| `SERDE-11` | SHOULD | ✅ (by language) | C# has no checked exceptions (design §11 item 15); each reader's XML docs list its `<exception>`s |
| `SERDE-12` | MUST | ✅ (extended) | STJ `SystemTextJsonSerdeTests.An_IOException_from_the_source_propagates_unwrapped`, `…from_the_destination…`, `Sync_Deserialize_from_a_stream_propagates_an_IOException_unwrapped`; Core `SerdeStreamDefaultTests.An_IOException_from_the_stream_propagates_unwrapped`, `ReadValueTests.An_IOException_mid_stream_propagates_unwrapped` (+`_sync`), `ResponseHandlersTests.A_mid_stream_IOException_propagates_unwrapped_and_disposes` (Unit) |
| `SERDE-13` | MUST | ✅ (built; P7a-9 **open for the lead**) | Core `SerdeValuesTests.RequireNonNull_*` (four) and `The_message_names_ReadValueOrDefaultAsync`, `ReadValueTests.A_root_null_for_a_reference_type_is_a_DeserializationException_naming_T` (+`_sync`), `A_nullable_value_type_admits_a_root_null` (+`_sync`), `ReadValueOrDefault_admits_a_root_null` (+`_sync`), `ResponseHandlersTests.A_root_null_names_T`, `A_200_root_null_names_T`; STJ `ReadValueEndToEndTests.ReadValueAsync_of_a_root_null_names_T`, `ReadValue_of_a_root_null_names_T`, `A_root_null_names_T_through_both_handlers`, `ReadValueOrDefault_admits_a_root_null_through_the_real_adapter`, `SerdeOptionsTests.A_root_null_is_still_returned_as_null_under_RespectNullableAnnotations` (the codec keeps `T?`; the reader layer rejects); the AOT smoke. `ISerde`'s own members and `GetError(Async)` keep `T?` (Unit) |
| `SERDE-14` | MUST | ✅ (covariance SHOULD unmet, design §10 entry 21) | Core `TristateTests.Present_of_null_throws_ArgumentNullException`, `Tristate_Absent_and_Null_convert_in_assignment_to_a_Tristate_of_T`, `Implicit_from_the_sentinels_converts_to_any_T`; `TristateSentinelTests` (five); `TristateArchitectureTests` (three: no STJ attribute, no STJ assembly reference, a `readonly struct` and not a record) (Unit) |
| `SERDE-15` | MUST | ✅ | STJ `TristateJsonTests.Absent_omits_the_key`, `Null_emits_json_null`, `Present_emits_the_value`, `The_three_states_in_one_document`, `A_property_modifier_from_the_caller_is_composed_not_replaced`, and the headline `Absent_is_omitted_under_the_default_mode_context` (the contract of fact 8) with `Absent_is_omitted_under_a_metadata_mode_context`; the documented limit `A_fast_path_only_context_cannot_carry_Tristate_models` (Unit) |
| `SERDE-16` | MUST | ✅ | STJ `TristateJsonTests.Missing_null_and_value_decode_to_Absent_Null_Present_for_string`, `…_for_a_value_type` (`Tristate<int>`, the AOT hazard), `…_for_an_object_with_its_type_preserved`, `…_for_a_list_with_its_type_preserved`, `Mixed_document_decodes_each_property_independently`, `A_present_object_encodes_the_object_not_the_wrapper`, `Seeded_round_trip_of_10000_generated_models` (Unit) |
| `SERDE-17` | MUST | ✅ | Core `TristateTests.Default_is_Absent`; STJ `TristateJsonTests.A_positional_record_and_a_class_with_init_properties_both_default_to_Absent`, `Mixed_document_decodes_each_property_independently` (a missing key never reaches the converter, fact 7) (Unit) |
| `SERDE-18` | SHOULD | ✅ | Core `TristateTests.Match_invokes_exactly_one_arm_per_state`, `Match_validates_its_arms`, `FromNullable_never_returns_Absent`, `TryGetValue_is_true_only_for_Present`, `GetValueOrDefault_*` (two), `GetValueOrNull_returns_the_value_or_null_for_value_types`, `Tristate_Present_infers_T`, `Tristate_FromNullable_for_a_nullable_value_type_maps_null_to_Null`, `Tristate_FromNullable_for_a_reference_type_maps_null_to_Null`, `The_Is_predicates_are_mutually_exclusive_per_state`, `Switch_on_State_covers_every_named_state`, `Property_pattern_matching_reads_the_value` (Unit) |
| `SERDE-19` | MUST | ✅ (P7a-5: no opt-out, a reading of the MAY) | STJ `TristateJsonTests.Tristate_wiring_is_on_by_default_for_a_bare_options_constructor`, `The_context_constructor_is_Tristate_wired_too`, `A_caller_registered_Tristate_converter_wins`, `AddTristateSupport_*` (five); `SerdeOptionsTests.CreateDefaultOptions_is_Tristate_wired`, `CreateDefaultOptions_is_wired_once_even_after_a_serde_wires_its_copy_again` (Unit) |
| `SERDE-20` | SHOULD | ✅ | STJ `TristateJsonTests.Top_level_Absent_and_Null_both_write_null`, `A_top_level_null_decodes_to_Null`, `An_array_element_Absent_writes_null_and_indices_do_not_shift`, `A_dictionary_value_Absent_writes_null` (the documented compromise) (Unit) |
| `SERDE-21` | MUST | ✅ | STJ `StrictCoercionTests`: nine named facts, one per row (`String_five_does_not_bind_to_int` … `Int_5_does_not_bind_to_string`), each run under `CreateDefaultOptions` **and** a plain `General` context; `SerdeOptionsTests.CreateDefaultOptions_uses_Web_naming_and_strict_numbers`, `RespectNullableAnnotations_rejects_a_member_null_under_the_defaults` (Unit) |
| `SERDE-22` | MUST | ✅ | STJ `StrictCoercionTests.Integer_widens_to_double`, `Empty_string_binds_to_string`, `A_well_typed_document_binds` (both configurations) (Unit) |
| `SERDE-23` | SHOULD | ✅ (STJ default + test) | STJ `SerdeOptionsTests.An_unmapped_member_is_skipped` (Unit) |
| `SERDE-24` | SHOULD | ✅ (STJ default + test) | STJ `SerdeOptionsTests.DateTimeOffset_round_trips_as_ISO_8601_and_the_same_instant` (an offset survives as `+05:30`, the same instant decodes, a UTC `DateTime` keeps its kind) (Unit) |
| `SERDE-25` | SHOULD | ✅ | STJ `SerdeOptionsTests.CreateDefaultOptions_returns_a_fresh_mutable_instance_per_call`, `CreateDefaultOptions_requires_a_resolver` (Unit) |
| `SERDE-26` | MUST | ✅ (fix; **Breaking**) | STJ `SerdeOptionsTests.The_callers_options_are_not_frozen_by_construction`, `The_callers_options_keep_their_converter_count_and_resolver_reference`, `The_serde_injects_nothing_into_the_callers_options_beyond_a_private_Tristate_copy`, `A_later_mutation_of_the_callers_options_does_not_affect_the_serde`, `The_context_constructor_copies_the_context_options`, `Constructing_twice_from_one_options_object_works`. The copy always succeeds, so the "cannot be copied" fallback is unreachable (design §11 item 18) (Unit) |
| `SERDE-27` | MUST | ✅ | Core `ResponseHandlersTests` five-case matrix: `Valid_body_decodes_and_disposes_the_response_once`, `A_204_response_fails_naming_T_and_disposes`, `A_malformed_body_is_a_SerdeException_with_an_inner_exception_and_disposes`, `A_mid_stream_IOException_propagates_unwrapped_and_disposes`, `The_response_is_disposed_in_every_case_and_a_dispose_failure_never_replaces_the_failure` (a `[Theory]`); `ReadValueTests.An_empty_body_with_ContentLength_zero_is_a_missing_body_naming_T` (+`_sync`), `A_zero_byte_unknown_length_body_is_a_missing_body_naming_T` (+`_sync`), `The_body_and_its_stream_are_disposed_exactly_once_on_every_path` (+`_sync`); `SerdeValuesTests` (the peek and its close-once latch); STJ `ReadValueEndToEndTests.A_204_style_empty_body_names_T` (Unit) |
| `SERDE-28` | MUST | ✅ | Core `ResponseHandlersTests.A_200_decodes`, `A_204_is_a_missing_body_naming_T`, `A_200_root_null_names_T`, `A_500_throws_HttpResponseException_with_the_status_and_a_buffered_body_readable_after_the_live_response_is_disposed`, `A_599_non_canonical_status_is_still_an_error_response`, `A_400_buffers_at_most_MaxBufferedErrorBytes`, `The_live_response_is_disposed_exactly_once_on_the_error_branch`, `An_other_status_fails_with_a_DeserializationException_leading_with_the_code_and_one_dispose` (304, 103, 302), `The_third_branch_message_carries_the_raw_ETag`, `…omits_absent_parts`, `A_relative_Location_is_resolved_against_the_request_uri_then_redacted`, `An_unparseable_Location_is_replaced_by_the_placeholder`, `BuildMessage_replaces_control_characters_so_the_message_is_one_line`; **Security** `StatusAwareHandlerLocationRedactionTests` (four, [below](#the-security-classes)); STJ `ReadValueEndToEndTests.A_status_aware_handler_maps_an_error_response_to_an_HttpResponseException_with_a_readable_typed_body`; the AOT smoke (Unit, Security) |
| `SERDE-29` | SHOULD | ✅ (test) | STJ `SerdeOptionsTests.Many_workers_share_one_serde_without_cross_talk` (32 workers × 500 iterations on a fresh serde, behind a start gate) (Unit) |
| `SERDE-30` | MAY | ✅ | Core `TristateTests.ToString_is_Absent_Null_or_Present_of_the_value` (string equality: `Absent`, `Null`, `Present(5)`, `Present(a)`), `TristateSentinelTests.ToString_is_Absent_and_Null` (Unit) |
| `HTTP-44` | MUST | ✅ (carried from 3b) | Core `TypedResponseTests.Metadata_is_readable_with_the_body_never_opened`, `One_handler_call_across_three_sequential_GetValueAsync`, `A_null_success_is_memoized`, `A_failure_is_memoized_as_the_same_exception_object`, `A_cancelled_construction_token_is_a_memoized_failure`, `The_wrapped_response_is_not_exposed`; STJ `ReadValueEndToEndTests.A_lazy_typed_PATCH_response_decodes_once`; the AOT smoke (Unit) |
| `HTTP-45` | MUST | ✅ (carried from 3b; P7a-15) | Core `TypedResponseTests.Sixty_four_concurrent_first_callers_run_the_handler_once`, `A_blocked_handler_does_not_block_a_second_caller` (the observable form: a second caller's `GetValueAsync` returns, incomplete, at once), `A_callers_cancelled_wait_leaves_the_parse_running`, `Disposing_during_a_parse_memoizes_the_resulting_failure`, `Dispose_forwards_to_the_response_once`, `A_fatal_exception_is_recorded_and_rethrown`. Exactly-once is a compare-and-swap on a `TaskCompletionSource<T>`, so no lock is held (Unit) |

Count: **32 rows**. By exit: 32 ✅, 0 🚫, 0 ⏳, 0 N/A. By level: 24 MUST (all ✅), 7 SHOULD (all ✅), 1 MAY (✅). (`SERDE-1`-`SERDE-30`: 22 MUST, 7 SHOULD,
1 MAY; `HTTP-44` and `HTTP-45` are MUSTs.)

## Work on other owners' rows

These carry no exit mark in 7a's checklist; the owner's checklist cites the evidence.

| ID (owner) | Work | Evidence |
|---|---|---|
| `BODY-16` (3b) | The disposal rule extended to the typed reader: the body is disposed on every path | `ReadValueTests.The_body_and_its_stream_are_disposed_exactly_once_on_every_path` (+`_sync`) |
| `RECOV-15`, `BODY-30` (4b, 4c) | `SERDE-28`'s error branch reuses `ErrorBodyBuffer` + `ErrorMapping`: the one capture site, the 1 MiB bound | `ResponseHandlersTests.A_500_throws_…`, `A_400_buffers_at_most_MaxBufferedErrorBytes` |
| `XCUT-19` (5b) | `Location` in the third-branch message goes through `UrlRedactor` | `StatusAwareHandlerLocationRedactionTests` |
| `NFR-9` (0) | One justified `IL2067` suppression, exercised by the AOT smoke's value-type `Tristate<int>` | `CheckPhase7aSerdeAsync`; task 0.1's published-binary run |
| `HTTP-43` (3b) | `TypedResponse<T>` disposal forwards to the response's latched dispose | `TypedResponseTests.Dispose_forwards_to_the_response_once` |
| `SEAM-22` (2b) | `SerdeSeamArchitectureTests` widened over the new seams | `SerdeSeamArchitectureTests.No_seam_member_takes_a_System_Type` |

## The Security classes

The exact allowed diff of `tests/Dexpace.Sdk.Core.Tests/Security` and `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security` against `main`:
`StatusAwareHandlerLocationRedactionTests.cs` added, nothing else (`git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
shows one added file). No existing `Security` class was edited, and `EnsureSuccessGetErrorRoundTripTests` (STJ, `Unit`) and the Core error-mapping classes, which read
`GetErrorAsync`, pass unchanged.

- `StatusAwareHandlerLocationRedactionTests` (P7a-19): a 302 with `Location: https://h/p?access_token=s3cr3t` and a relative `Location` both produce a
  message without `s3cr3t`; `https://u:p4ss@h/p` produces one without the userinfo; a `Location` and `ETag` carrying HTAB and obs-text render on one line. A CR/LF fixture
  cannot be built, because `Headers.Builder.AddInbound` rejects every control character but HTAB, so the defensive `?` replacement is covered by a Unit test through an
  internal seam (`SuccessDeserializingHandler<T>.BuildMessage`).

## Deviations from the plan

1. **Branch.** One branch, `79-phase-7a-serde`, named by the brief (plan reading R13 proposed a topic branch per PR). No `git push`, no `gh` command.
2. **`CHANGELOG.md`** has a `### Phase 7a — serde` sub-heading of its own at the end of `[Unreleased]`, with `#### Added`, `#### Changed` (the Breaking entries) and
   `#### Fixed`, as the plan asks. The earlier phases' entries are inline prefixes ("Phase 6b redirect, PR 1:") with no sub-heading, so this block is new in shape.
3. **A fast-path-only context is no longer usable with the serde** (found in task 0.1 and measured again in the suite). `SystemTextJsonSerde(JsonSerializerContext)` used to
   pass `context.Options` itself; it now copies them (`SERDE-26`), and a context generated with `GenerationMode = Serialization` serves only its own options object, so a
   copy cannot serialise through it. This follows from the ruling, is documented on `AddTristateSupport` and in `serde.md`, pinned by
   `TristateJsonTests.A_fast_path_only_context_cannot_carry_Tristate_models`, and listed in the changelog's Breaking entry. The design's R1 expected a fallback; there is none.
4. **The existing pagination tests scripted empty page bodies** (`PageableTests`, `PaginationStrategiesTests.EndToEnd_CursorStrategy_TwoPagesViaFakePipeline`): under the new
   missing-body rule (`SERDE-27`, P7a-10) an empty body is a failure, so 14 tests failed after PR 3. The plan expected `Pageable.cs` to be the only call site and to need no
   change (fact 10, true); it missed the tests. They now give each page a `{}` payload (the scripted serde ignores the bytes) through a `PageResponse` helper, and
   `TrackingBody` serves two bytes. `Pageable.cs` is untouched. 7c, which owns these files, re-derives the hunks.
5. **`ModelConstructionArchitectureTests`' allow-list gained `TypedResponse<T>`'s constructor** (the plan missed this test): a new public constructor under `Http.*` is "a new
   construction route and needs a design decision", and P7a-14 is that decision. `Describe` now falls back to `Type.ToString()` for a parameter type that is built on a generic
   parameter (its `FullName` is `null`), so the entry reads `…IResponseHandler`1[T]…`; no existing entry changed.
6. **`TristateTests.Switch_on_State_is_exhaustive_by_pattern` is `Switch_on_State_covers_every_named_state`** and has a discard arm: a `switch` over an enum with all three named
   states is still `CS8524` ("an unnamed enum value is not covered"), which `TreatWarningsAsErrors` fails. Same intent, same assertions.
7. **`SerdeValues.PeekFirstByte(Async)` takes ownership of the stream** (disposes it itself when it is empty or the first read fails) instead of "the caller disposes `stream`" (plan 3.4):
   the callers cannot forget it, and the close-once latch then makes the real stream's `Dispose` count exactly one on every path (tested for success, codec failure,
   `IOException`, root null, empty and unknown-length bodies).
8. **`A_fatal_exception_is_recorded_and_rethrown` observes the unobserved task exception.** `IsFatal` is `OutOfMemoryException`; rethrowing it from the discarded parse task is what P7a-15
   specifies ("recorded, then rethrown"), and left alone it fires `TaskScheduler.UnobservedTaskException` on the finalizer thread during an unrelated test, which failed
   `LateResultTests.A_faulted_late_task_is_observed_and_raises_no_UnobservedTaskException` (that test counts the event process-wide). The test now subscribes, forces a collection from a
   separate frame, marks only its own exception observed, and asserts the rethrow happened exactly once. Planning finding 4 stands: the rethrow surfaces only as an unobserved task exception.
9. **Suppressions, all scoped and justified in place:** `CA1000` on the four generic-type static members of `Tristate<T>` (`Absent`, `Null`, `Present`, `FromNullable`) as
   `[SuppressMessage]` citing `SERDE-18` and design position A; `RS0030` around `TaskCompletionSource<T>.TrySetResult` and `Task<T>.WaitAsync` in `TypedResponse<T>` citing `SEAM-30`
   (the memoised task keeps the result, so an abandoned wait orphans nothing); the one `IL2067` (`UnconditionalSuppressMessage`) on `TristateConverterFactory.CreateConverter`.
   `_ = RunAsync(completion)` drew no analyzer finding (task 0.1 step 5 expected `CA2012`/`CS4014`/`IDE0058` might). In the tests: `xUnit1051` is disabled for `TypedResponseTests` (whether
   `GetValueAsync` gets a caller token is the behaviour under test) and `CA2201` around the simulated `OutOfMemoryException`.
10. **`ReadValueAsync_is_single_use` is unchanged (R4).** A second read of a spent `FromBytes` body still throws `StreamConsumedException`, because the body's own single-use latch is checked
    before anything else; `ReadValueTests.A_second_read_surfaces_the_as_built_exception` pins it. No extra Breaking line.
11. **The disposal of a root null is inside the guarded region.** Plan 3.5's sketch put `RequireNonNull` after `ReadCoreAsync` returned, i.e. after the body was disposed with no primary; the
    check now runs inside the `try`, so a dispose failure after a root null is attached to that `DeserializationException` (`ReadValueTests.A_dispose_failure_after_a_root_null_is_attached_to_the_root_null_failure`).
12. **Namespaces of the new tests follow the folder's existing namespace:** `Errors/HttpResponseExceptionGetErrorTests` is in `Dexpace.Sdk.Core.Tests.Exceptions` and
    `Http/Response/TypedResponseTests` in `Dexpace.Sdk.Core.Tests.Http.Responses`, because a `Dexpace.Sdk.Core.Tests.Errors` namespace shadows `Errors.*` in `PageableTests`
    (the same finding as 6b's deviation 2). `IDE0130` is not enforced.
13. **Extra doubles and tests the plan did not list:** `DelegateResponseHandler<T>` (TestSupport, to script a handler inline); `Utf8LiteralSerde`'s grammar gained `first` (one byte read, the end
    never reached) for the "codec does not close" test; `GetError_and_GetErrorAsync_agree_over_a_replayable_buffered_body`; and end-to-end tests of the handlers and a lazy typed PATCH response
    over the real adapter (`ReadValueEndToEndTests`).
14. **Pre-existing flakiness on this host, unrelated to 7a.** On the 32-core development machine `BearerPipelineTests.A_challenged_bearer_call_through_the_default_pipeline_succeeds_with_one_resend`
    (a process-wide `ActivityListener` sees another test's attempt span) and, once, `RecordingTaskSchedulerTests.It_runs_each_task_on_its_own_non_pool_thread` fail intermittently in a full
    run; both fail the same way on an unmodified `main` (verified by stashing the branch's changes), and pass in isolation. Not touched.
15. **Knowledge corpus.** A note was added under `docs/knowledge/notes/` recording what the implementation found (fact 8's fast-path-only result, fact 9, fact 11, the `CS8524` and
    unobserved-exception findings); nothing under `harvested/` was edited.

## Open rulings taken as designed

The lead had not ruled, so each landed as the design argued it:

- **P7a-9 / Q1** (open for the lead): `SERDE-13`'s "every decode overload" is read as the overloads whose result is non-null (`ReadValue(Async)`, both handlers); `ISerde`'s `T?` members and
  `GetError(Async)` keep their honest nullable return. Reversing it costs one task: a `RequireNonNull` call in `SystemTextJsonSerde` plus its `PublicAPI` return annotation.
- **P7a-21** (open for the lead): the per-read materialisation cap hand-off (5a's P5a-23) is declined; the cap stays the documented constant. Reversing it costs the twelve overloads of the design's argument.
- P7a-5 (no opt-out of the Tristate wiring) is recorded as a reading of the MAY; P7a-6 (the public `ITristate` hook and the one `IL2067` suppression) and P7a-15 (compare-and-swap in place of
  `Lazy<Task<T>>`) are settled; the design §7.3 and §3.4 correction notes carry them.

## Issue #1: the closing comment

No agent posts to GitHub (repository rule). The lead posts this text on issue [#1](https://github.com/dexpace/dotnet-sdk/issues/1) when the phase merges; the checklist rows that cover it are
`SERDE-14` to `SERDE-20` and `SERDE-30`:

> Phase 7a closes this issue, with one change to the proposal: the type is named **`Tristate<T>`**, not `Optional<T>`. Design §7.3 rules that .NET already circulates a two-state `Optional<T>`
> (Roslyn's `Microsoft.CodeAnalysis.Optional<T>`: `HasValue`, `Value`), so reusing the name for three states would contradict the ecosystem's vocabulary, and the specification's own word is
> `Tristate` (`SERDE-14`). Every other requirement here carries over unchanged: a trim- and AOT-safe `readonly struct Tristate<T> where T : notnull` whose `default` is Absent; Absent omitted on
> write through a `JsonTypeInfo` modifier and Null written as `null`, wired by default in `SystemTextJsonSerde` on a private copy of the caller's options; implicit conversion from `T` (a `null`
> becomes Null) and from `Tristate.Absent` / `Tristate.Null`; `Present`, `FromNullable`, `Match`, `TryGetValue`, `GetValueOrDefault` and the `Is*` predicates; pattern matching on `State` or the
> predicates. RFC 7386 merge-patch documents stay out of scope. Built by phase 7a; checklist: `docs/work/mvp/phase7/phase7a/2026-10-09-phase7a-serde-checklist.md`.
