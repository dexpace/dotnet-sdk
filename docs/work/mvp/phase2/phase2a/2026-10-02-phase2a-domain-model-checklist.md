# Phase 2a — Domain Model: Checklist

The execution-time checklist for sub-phase 2a of roadmap phase 2
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md), [segmentation design](../2026-09-29-phase2-segmentation-design.md)):
one row per requirement ID, in the legend of the roadmap's cross-cutting constraint 3. Written from what was built on
branch `phase-2a-domain-model`, following the [design](2026-09-29-phase2a-domain-model-design.md) and the
[plan](2026-09-30-phase2a-domain-model.md) (GitHub issue [#36](https://github.com/dexpace/dotnet-sdk/issues/36)).

The scope is 42 rows: `HTTP-1`–`HTTP-35`, `HTTP-46`–`HTTP-50`, `HTTP-53` and `SEAM-29`. Every test below is
`[Trait("Category", "Unit")]` unless another category is named, and lives in `tests/Dexpace.Sdk.Core.Tests/` unless a
project is named. **Red evidence:** for a new type or member the red was the compile error (the plan's convention 1).
Behavioural reds were observed for `MediaType` (task 3.4: `utf-7` threw `NotSupportedException`; task 3.5: `a=` parsed,
seven failing rows) and are the stated reason elsewhere; a test marked "pin" holds behaviour that was already correct.
The proving test classes for the `Http/Request/` and `Http/Response/` folders use the namespaces
`Dexpace.Sdk.Core.Tests.Http.Requests` and `…Responses`: a namespace named `…Tests.Http.Request` would shadow the
`Request` type in the older tests of `Dexpace.Sdk.Core.Tests.Http`.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `Http/Common/{HeadersTests,HeadersBuilderTests,HttpHeaderNameTests,HttpHeaderSyntaxTests,MediaTypeTests,MethodTests,ProtocolTests,ETagTests,HttpRangeTests}.cs`,
`Http/Request/{RequestTests,RequestOptionsTests,QueryTests,QueryParseTests,Rfc3986Tests,RequestConditionsTests}.cs`,
`Http/Response/{ResponseTests,StatusTests,TestResponsesTests}.cs`, `Architecture/Model{Immutability,Construction}ArchitectureTests.cs`,
`Pipeline/Policies/{RetryFactsTests,RetryPolicyTests,RedirectPolicyTests}.cs`, `Security/HeaderInjectionValidationTests.cs`
and, in `tests/Dexpace.Sdk.Http.SystemNet.Tests/`, `HeaderCasingWireTests.cs`, `ReasonPhraseWireTests.cs`,
`SystemNetHttpClientTests.cs`.

| ID | Level | Status | What was built | Proven by |
|---|---|---|---|---|
| `HTTP-1` | MUST | ✅ | Every model type is immutable: `Method` and `HttpHeaderName` are `sealed record`s, `Request` is get-only, `Headers` and `Query` hold `ImmutableArray` storage, the new records validate in `field`-backed `init` accessors. | `ModelImmutabilityArchitectureTests` (`Every_instance_field_is_readonly_except_the_documented_body_state`, `No_public_property_has_a_non_init_setter`, `Every_model_type_is_sealed`, over 12 types; first run green, a pin; confirmed to fail by temporarily adding `public int X { get; set; }` to `ETag`: two reds) |
| `HTTP-2` | MUST | ✅ | The four construction routes; `with` cannot bypass a get-only member. 🚫 for the builder clause per design §10 `no-builder-objects`; the reflection residue is §10 `construction-bypass`. | `ModelConstructionArchitectureTests` (`Public_constructors_in_Http_namespaces_are_exactly_the_allow_list`, `Value_types_are_created_through_factories`, `A_derived_request_cannot_bypass_validation`); `RequestTests.Deriving_a_request_cannot_install_a_body_carrying_GET` |
| `HTTP-3` | MUST | ✅ | `ToBuilder` copies, `Request` derives through `With*` (every one through the constructor), `Response` through `WithBody`, the records through `with`/`WithTag`. | `RequestOptionsTests.WithTag_adds_and_replaces_without_mutating_the_original`; `QueryTests.ToBuilder_copies_into_a_fresh_builder`, `A_built_query_is_unaffected_by_later_builder_edits`; `HeadersBuilderTests.Build_is_a_deep_copy_and_later_edits_never_reach_the_result`, `ToBuilder_is_a_fresh_builder`; `RequestTests.Each_With_method_returns_a_new_request_and_leaves_the_original_unchanged`; `ResponseTests.WithBody_returns_a_new_response_owning_the_new_body_and_keeps_the_rest`, `The_original_keeps_its_own_body_and_can_still_be_disposed`, `There_is_no_WithHeaders`; `RequestConditionsTests.A_with_derivation_leaves_the_original_unchanged` |
| `HTTP-4` | MUST | ✅ | `ArgumentNullException` with the field as `ParamName` for `Request` (`method`, `url`) and `Response` (`request`); `Response.protocol` is required and checked with `Enum.IsDefined` (`ArgumentOutOfRangeException`). | `RequestTests.A_missing_required_field_throws_ArgumentNullException_with_its_name`; `ResponseTests.A_missing_request_throws_ArgumentNullException_with_ParamName_request`, `An_undefined_protocol_throws_ArgumentOutOfRangeException_with_ParamName_protocol`, `The_protocol_parameter_has_no_default_value`, `Status_is_a_required_positional_parameter` |
| `HTTP-5` | MUST | ✅ | Storage is immutable and the accessors return read-only lists; `Names` is an `ImmutableArray` snapshot. | `QueryTests.GetAll_result_is_read_only`; `HeadersTests.Names_is_a_read_only_list_snapshot` (pins for the downcast probe); `ModelImmutabilityArchitectureTests.Collections_exposed_are_read_only_types` |
| `HTTP-6` | MUST | ✅ | `Request` carries exactly method, URL, headers, body; `Response` gains `Request` and `ReasonPhrase`; the reference transport threads both. | `RequestTests.A_request_carries_exactly_method_url_headers_and_body`, `Headers_is_never_null_and_Body_is_nullable`; `ResponseTests.Request_and_reason_phrase_round_trip`, `A_reason_phrase_with_a_control_character_throws_ArgumentException` (U+0000, 0001, 000A, 000D, 007F), `A_null_reason_phrase_is_accepted`, `Obs_text_in_a_reason_phrase_is_accepted`, `Headers_is_never_null`, `An_absent_body_is_an_empty_buffered_body_never_null`; `SystemNetHttpClientTests.The_response_carries_the_request_that_was_sent`, `The_reason_phrase_is_carried_when_it_is_header_safe`, `An_unsafe_reason_phrase_from_a_handler_is_dropped_to_null_not_thrown` (`Unit`, handler stub); `ReasonPhraseWireTests` (`Integration`, loopback: a safe phrase, `0x01` and DEL dropped to `null`, obs-text kept) |
| `HTTP-7` | MUST | ✅ | The constructor rejects a body when `method.ForbidsBody` (internal; GET, HEAD, TRACE, CONNECT) with `ParamName` `body`; every `With*` routes through it. | `RequestTests.A_body_on_GET_HEAD_TRACE_CONNECT_is_rejected` (4 methods × constructor, `Create`, `WithMethod`, `WithBody`), `Deriving_a_request_cannot_install_a_body_carrying_GET`, `POST_PUT_PATCH_DELETE_OPTIONS_accept_a_body` (pin); `MethodTests.ForbidsBody_is_true_for_exactly_GET_HEAD_TRACE_CONNECT` |
| `HTTP-8` | SHOULD | N/A | "No method set" is unrepresentable: `method` is a required constructor parameter and `null` is rejected under `HTTP-4`. Design §4.2. | a cross-reference comment in `RequestTests.A_missing_required_field_throws_ArgumentNullException_with_its_name` |
| `HTTP-9` | MUST | ✅ | `Method` is a `sealed record`; `Of` trims SP/HTAB, validates an RFC 9110 token without echoing it, folds the nine verbs ASCII-case-insensitively and keeps other tokens verbatim (ruled 2026-09-30); `IsSafe`/`IsIdempotent` are no longer public; one internal source, `RetryFacts.IdempotentMethods`; TRACE is no longer retried. | `MethodTests` (`Well_known_tokens_equal_their_upper_case_names…`, `Of_folds_case_for_the_nine_well_known_verbs…_known`/`_other`, `Of_trims_SP_and_HTAB_only`, `Of_rejects_a_non_token_without_echoing_it`, `Method_has_no_public_bool_property`, `Method_is_a_reference_type_and_never_default`, `Method_SafetyAndIdempotency` rewritten against the internal set); `RetryFactsTests` (3); `RetryPolicyTests.A_TRACE_request_is_not_retried` |
| `HTTP-10` | MUST | ✅ | `Status.TryGetKnown`; `FromCode` stays total. | `StatusTests.FromCode_maps_200_to_the_named_Ok_and_599_799_minus1_to_nameless_values_without_throwing`, `TryGetKnown_is_true_for_200_and_false_for_599` |
| `HTTP-11` | MUST | ✅ | `Status.IsError`; `Response` gains `IsInformational`, `IsRedirect`, `IsClientError`, `IsServerError`, `IsError`. | `StatusTests.Classification_by_boundary_code` and `ResponseTests.Classification_by_boundary_code` (12 boundary codes each) |
| `HTTP-12` | MUST | ✅ | Code-only equality kept. | `StatusTests.Equality_is_by_code_and_the_hash_agrees` (pin, with the hash assertion added), `A_default_Status_is_code_zero_and_unrecognised` |
| `HTTP-13` | MUST | ✅ | ASCII-only fold (`System.Text.Ascii`) for writes and lookups; a non-ASCII lookup finds nothing and does not throw; value equality ignoring casing. | `HeadersTests.A_name_added_under_one_casing_resolves_under_every_other`, `Folding_is_culture_invariant_under_tr_TR`, `A_non_ASCII_lookup_finds_nothing_and_does_not_throw` (the Kelvin sign), `Equal_content_gives_equal_instances_and_hashes_and_casing_is_ignored`, `Differently_ordered_names_are_unequal`, `Operators_equal_and_not_equal_handle_null` |
| `HTTP-14` | MUST | ✅ | Add appends and set replaces, on `Headers` and `Headers.Builder`. | `HeadersTests.Add_appends_and_Set_replaces_on_the_instance`; `HeadersBuilderTests.Builder_add_appends_and_set_replaces` (pins) |
| `HTTP-15` | MUST | ✅ | `Set(name, null)` removes; `Set(absent, null)` returns the same instance. The name is still validated. | `HeadersTests.Set_with_null_removes_the_header_and_Set_of_an_absent_name_returns_the_same_instance`, `Set_with_null_still_validates_the_name`; `HeadersBuilderTests.Builder_set_null_removes_the_header` |
| `HTTP-16` | SHOULD | ✅ | Ordered entry array with an ordinal index; `Set` keeps position and first casing. | `HeadersTests.Enumeration_and_Names_are_in_insertion_order_z_a_m`, `Set_on_an_existing_name_keeps_its_position_and_first_casing`, `Without_then_With_moves_the_name_to_the_end`; `HeadersBuilderTests.Builder_set_keeps_position_and_first_casing_and_remove_then_add_moves_to_the_end` |
| `HTTP-17` | MUST | ✅ | Trim-then-token validation kept; the typed overloads take an already-validated `HttpHeaderName`. | `HeaderInjectionValidationTests` (`Security`; kept green, one expected value changed, see below); `HttpHeaderNameTests.Of_validates_like_the_headers_entry_points` |
| `HTTP-18` | MUST | ✅ | Outbound value rule kept (HTAB, 0x20–0x7E); now also the public `HttpHeaderSyntax.IsValidOutboundValue`. | `HeaderInjectionValidationTests` (`Security`); `HttpHeaderSyntaxTests.IsValidOutboundValue_*`, `The_predicates_agree_with_the_throwing_validators` |
| `HTTP-19` | MUST | ✅ | Inbound lenient path kept; `AddInbound` now keeps the sender's casing; public `IsValidInboundValue`. | `HeaderInjectionValidationTests.The_inbound_path_*` (`Security`); `HeadersBuilderTests.AddInbound_keeps_the_senders_casing_and_stays_lenient`; `HttpHeaderSyntaxTests.IsValidInboundValue_*` |
| `HTTP-20` | MUST | ✅ | Messages name the code point and never echo; the adapter's `HeaderSyntaxOnWire` is gone and its log escaping is `HttpHeaderSyntax.EscapeName`. | `HeaderInjectionValidationTests` and `HeaderInjectionWireTests` (`Security`, unedited); `HttpHeaderSyntaxTests.EscapeName_*` |
| `HTTP-21` | MUST | ✅ | `HttpHeaderName` is a `sealed record` (equality over `CanonicalName`, `Original` for the wire, `ToString()` is `Original`); typed overloads on `Headers`, `Headers.Builder`, `Request.WithHeader`; `Headers` enumerates original casing; six new `WellKnown` names; the adapter sends a custom name as written. | `HttpHeaderNameTests` (7); `HeadersTests.Enumeration_and_Names_carry_the_first_insertions_casing`, `A_name_added_through_a_string_is_visible_through_HttpHeaderName_and_back`; `HeaderCasingWireTests.A_custom_header_goes_out_with_its_original_casing` (`Integration`, raw bytes) |
| `HTTP-22` | MAY | ⏳ | Not built: the `WellKnown` statics share the hot names and the observable contract is value equality. | owned by the `docs/first-release.md` entry "SHOULD- and MAY-level requirements declined for v1" |
| `HTTP-23` | MUST | ✅ | Already met; pinned against `tests/vectors/http/media-type.json`. | `MediaTypeTests.Parse_case_table_matches_the_vectors` (pin) |
| `HTTP-24` | MUST | ✅ | `Charset` also catches `NotSupportedException`, so `utf-7` is `null`. | `MediaTypeTests.Charset_resolves_utf8_in_any_case_and_key_case` (pin), `Charset_is_null_for_bogus_utf_7_and_absent` (`utf-7` was red) |
| `HTTP-25` | MUST | ✅ | Already met; round-trip table in the vectors. | `MediaTypeTests.Round_trip_table_matches_the_vectors` (pin) |
| `HTTP-26` | MUST | ✅ | Kept: parse and construction use the `HTTP-18` predicate. | `HeaderInjectionValidationTests` media-type rows (`Security`, cited and unedited); `MediaTypeTryParseTests`, `MalformedContentTypeWireTests` re-run unedited |
| `HTTP-27` | SHOULD | ✅ | Already met: `*/json` rejected, receiver wildcards. | `MediaTypeTests.Of_rejects_a_wildcard_subtype_under_a_concrete_type`, `Includes_honours_receiver_wildcards_and_ignores_parameters` (pins) |
| `HTTP-28` | MUST | ✅ | `Query`: ordinal names, first-appearance order, multi-value, `Add(name, null)` stores `""`. | `QueryTests.Names_are_case_sensitive`, `Insertion_order_is_first_appearance`, `Multiple_values_per_name_are_kept_in_order`, `Add_null_is_stored_as_the_empty_string`, `Set_null_stores_a_single_empty_string_and_does_not_remove`, `Get_of_an_absent_name_is_null`, `GetAll_of_an_absent_name_is_empty`, `Builder_rejects_an_empty_name`, `Builder_rejects_a_lone_surrogate_in_a_name_or_value` |
| `HTTP-29` | MUST | ✅ | `Encode()` through `Rfc3986.EncodeComponent`; no leading `?`; `""` when empty. | `QueryTests.Encode_matches_the_vectors` (9 rows of `query.json`), `Encode_has_no_leading_question_mark_and_is_empty_for_an_empty_query` |
| `HTTP-30` | MUST | ✅ | Order-sensitive equality, equivalent to equal `Encode()`; `Set(name, [])` drops the name. | `QueryTests.Equality_holds_exactly_when_Encode_is_equal` (9 pairs), `Equality_is_order_sensitive_across_names`, `Set_with_an_empty_sequence_drops_the_name_at_Build`, `A_query_works_as_a_Dictionary_key` |
| `HTTP-31` | MUST | ✅ | Total, lenient `Parse`; an escaped invalid UTF-8 sequence stays raw; a literal lone surrogate becomes U+FFFD. | `QueryParseTests.Parse_matches_the_vectors` (16 rows), `Parse_of_Encode_round_trips_for_every_vector`, `An_escaped_invalid_utf8_sequence_stays_raw`, `A_literal_lone_surrogate_in_the_input_becomes_U_FFFD`, `Parse_never_throws_on_arbitrary_input` |
| `HTTP-32` | SHOULD | ✅ | One internal component encoder, `Rfc3986` (`Uri.EscapeDataString` / `UnescapeDataString`). | `Rfc3986Tests.EncodeComponent_matches_the_vectors`, `DecodeComponent_matches_the_vectors`, `EncodeComponent_turns_a_lone_surrogate_into_the_replacement_bytes` (pin), `DecodeComponent_leaves_an_escaped_invalid_utf8_sequence_raw` (pin) |
| `HTTP-33` | MUST | ✅ | `Protocol.Parse` compares with `System.Text.Ascii.EqualsIgnoreCase`. | `ProtocolTests.Parse_accepts_HTTP_2_HTTP_2_0_and_mixed_case`, `Parse_is_culture_invariant_under_tr_TR` (expected to be a pin: `ToUpperInvariant` was already invariant; not run against the old code), `Parse_rejects_http_3_and_unknown_text`, `Wire_string_round_trips` |
| `HTTP-34` | MUST | ✅ | `sealed record RequestOptions` (`Timeout`, `MaxRetries`, `Tags`, `Empty`); keys re-based on `StringComparer.Ordinal`; equality compares tags by content. | `RequestOptionsTests` (`Empty_overrides_nothing_and_has_no_tags`, `A_new_instance_equals_Empty`, `Tags_*`, `Equality_compares_tags_by_content_and_hashes_agree`, `Different_timeouts_retries_or_tags_are_unequal`, `WithTag_*`) |
| `HTTP-35` | MUST | ✅ | Validation in `field`-backed `init` accessors, so `with` cannot bypass it. | `RequestOptionsTests.Timeout_that_is_not_positive_is_rejected`, `A_positive_or_null_timeout_is_accepted`, `MaxRetries_below_zero_is_rejected`, `MaxRetries_of_zero_or_null_is_accepted` |
| `HTTP-46` | MUST | ✅ | `Request.Equals` over method, `Url.AbsoluteUri` (ordinal), headers by value, body under position A; the in-memory body variant compares by bytes and content type, hash over content type and length only. | `RequestTests.Equal_text_gives_equal_requests_and_hashes`, `Userinfo_and_fragment_count`, `Host_names_are_not_resolved`, `Header_value_equality_is_used`, `Different_bytes_or_different_content_types_are_unequal`, `FromBytes_and_FromString_over_the_same_bytes_are_equal`, `Two_FromStream_bodies_over_identical_MemoryStreams_are_unequal`, `A_request_is_equal_to_itself_with_a_stream_body`, `A_replayable_copy_equals_a_FromBytes_body`, `An_unknown_RequestBody_subclass_keeps_reference_equality` |
| `HTTP-47` | SHOULD | ✅ | `Create` and the constructor throw `ArgumentException` (`ParamName` `url`) for malformed, relative, non-http(s) and Linux-`file:` input, carrying the input through `UrlRedactor` (ruling P2a-2); `RedirectPolicy` returns a 3xx with a non-http(s) `Location` unfollowed. | `RequestTests.A_malformed_relative_non_http_or_file_url_is_rejected` (`::bad`, `/rel`, `ftp://h/x`, `file:///etc/passwd`, via `Create` and the constructor), `The_url_error_carries_the_redacted_input`, `A_password_in_the_url_never_reaches_the_message`, `WithUrl_validates_like_the_constructor`; `RedirectPolicyTests.A_Location_that_is_not_http_or_https_returns_the_3xx_unfollowed`, `A_303_on_a_POST_with_a_body_follows_as_a_bodiless_GET` (pin) |
| `HTTP-48` | SHOULD | ✅ | `sealed record ETag` (`Any`, `Strong`, `Weak`, `Parse`, `TryParse`); `etagc` characters; exact-spelling round trip. | `ETagTests` (`Parse_matches_the_vectors` 7 rows, `Parse_of_a_malformed_form_throws` 16 rows, `Parse_of_null_or_blank_is_null`, `TryParse_never_throws`, `Strong_rejects_empty_and_non_etagc_characters`, `Weak_may_be_empty`, `Characters_are_etagc`, `Any_is_star_and_has_empty_opaque`, `Equality_is_by_kind_and_opaque`, `ToString_renders_star_quoted_and_weak`); P2a-3 in `RequestConditionsTests` |
| `HTTP-49` | SHOULD | ✅ | `sealed record HttpRange` (`Bounded`, `Suffix`, `From`, `Parse`, `TryParse`); semantic equality; verbatim text kept. | `HttpRangeTests` (`Parse_matches_the_vectors`, `Parse_accepts_only_the_bytes_unit_case_insensitively_and_one_range` 16 invalid rows, `Bounded_rejects_a_negative_offset_a_non_positive_length_and_overflow`, `Suffix_and_From_render_canonically`, `Equality_is_semantic_over_Offset_and_Length`, `TryParse_never_throws`) |
| `HTTP-50` | SHOULD | ✅ | `sealed record RequestConditions`; `init` accessors enforce `*` exclusivity and collapse a repeated `*`; `ApplyTo` writes with `Set`, dates as UTC in `"R"` format. | `RequestConditionsTests` (`ApplyTo_joins_each_list_into_one_header_with_commas`, `Dates_render_in_R_format_converted_to_UTC`, `Applying_twice_gives_equal_headers`, `Unset_members_leave_existing_headers_untouched`, `Star_is_exclusive_with_concrete_tags`, `A_repeated_star_collapses`, `Equality_is_sequence_equality_over_the_arrays`, `ApplyTo_Request_returns_a_request_with_the_headers_and_leaves_the_original_unchanged`) |
| `HTTP-53` | MUST | ✅ | `MediaType.Parse` rejects a parameter with an empty raw value; `a=""` is accepted; an empty segment after `;` is still skipped. | `MediaTypeTests.Parse_rejects_a_parameter_with_an_empty_raw_value` (red), `Parse_accepts_a_quoted_empty_value`, `Parse_still_rejects_blank_text_slash_json_and_text_slash`, `An_empty_segment_after_a_semicolon_is_still_skipped`, `Parse_rejection_table_matches_the_vectors` (red for the `a=` rows); `MediaTypeTryParseTests` and `MalformedContentTypeWireTests` (`Security`) re-run unedited and green |
| `SEAM-29` | MUST | ✅ 🚫 | ✅ for immutability, factories and uniform required-field errors, as `HTTP-1`, `HTTP-2`, `HTTP-4`. 🚫 for the shared generic `Builder`/`IBuilder<T>`: design §10 `no-builder-objects`. | `ModelImmutabilityArchitectureTests`, `ModelConstructionArchitectureTests` (`No_generic_builder_contract_exists`), `RequestTests` and `ResponseTests` required-field theories |

## Exit criteria

- [x] All 42 rows carry a mark; 40 ✅ (`SEAM-29` also 🚫 for the generic builder), `HTTP-8` N/A, `HTTP-22` ⏳ owned by
  `docs/first-release.md`.
- [x] The one `Security` assertion whose value changed is recorded below (position C); every other edit to a `Security`
  file is mechanical and listed.
- [x] `PublicAPI.Unshipped.txt` diffs for core are in each commit under `RS0016`/`RS0017`/`RS0036`; `PublicAPI.Shipped.txt`
  is still empty. The SystemNet package's surface did not change.
- [x] `CHANGELOG.md` `[Unreleased]` carries a line per breaking change, prefixed **Breaking:** (constraint 8).
- [x] User page `docs/sdk-documentation/http.md` is written ("As built by phase 2a … written against source on
  2026-10-02").
- [x] Roadmap Phase Status Note appended; the housekeeping probe reports no drift.

## Existing assertions changed

### `Security` classes (constraint 5)

| File | Diff kind | Detail |
|---|---|---|
| `Security/HeaderInjectionValidationTests.cs` | **the position-C change** | `Surrounding_whitespace_is_trimmed_from_a_name_before_validation`: `Assert.Equal("x-trace", Assert.Single(headers.Names))` became `Assert.Equal("X-Trace", …)` (same ordinal `Assert.Equal`, nothing weakened; `HTTP-17`'s own conformance text is "storing `X-Trace`"; `HTTP-21` removes the lower-casing). Two assertions **added** to the same test: `Assert.Equal(HttpHeaderName.Of("x-trace"), HttpHeaderName.Of(name))` and `Assert.Equal("v", headers.Get("x-trace"))`. No other assertion, theory row or trait changed. Committed with the `Headers` rebuild (design position C, reasons 1–5). |
| `Security/AuthHttpsGuardTests.cs` | constructor call only | `new Response(...)` became `TestResponses.Create(..., scenarioRequest)`; the literal `Request.Get(...)` of each scenario is hoisted into a local and passed to the responses. No assertion, theory row or trait changed. |
| `Security/EnsureSuccessErrorMappingTests.cs` | constructor call only | `new Response(...)` became `TestResponses.Create(..., s_request)` over one `Request.Get("https://api.example.com/v1/items")` field; a `using` for `Http.Request` was added. |
| `Security/ReDriveRequestIsolationTests.cs` | constructor call, and `with` → `WithHeaders` | `new Response(...)` as above; `MarkingPolicy` writes `context.Request.WithHeaders(...)` instead of `with { Headers = … }`. |
| `Security/RedirectCredentialHygieneTests.cs` | constructor call only | the scripted redirects and the final 200 pass `seed`. |
| `Security/RetryPacingOverflowTests.cs` | constructor call only | `new Response(...)` as above, including the `SendWithHintAsync` helper. |
| `MediaTypeTryParseTests`, `MalformedContentTypeWireTests`, `HeaderInjectionWireTests`, `FramingHeaderDropWireTests`, `RedirectWireTests`, `UrlRedactionDefaultDenyTests` | unedited | re-run and green after every PR. |

### Other `Unit` / `Integration` tests

| Test | Was | Now | Why |
|---|---|---|---|
| `Http/MethodAndStatusTests` | one class | split into `Http/Common/MethodTests`, `Http/Response/StatusTests`, `Http/Common/ProtocolTests`; `Method_Of_NormalisesKnownVerbs` and `Method_Of_PreservesUnknownVerb` kept; `Method_SafetyAndIdempotency` rewritten against the internal set (its `IsSafe` lines removed) | `HTTP-9` (the public `IsSafe`/`IsIdempotent` it read are deleted) |
| `Http.SystemNet.Tests/Loopback/LoopbackServerTests.Records_the_request_line_and_header_bytes_exactly_as_sent` (`Integration`) | `Assert.Contains("x-odd-spacing: a  b\t c", …)` with a comment that the name arrives lower-cased | `"X-Odd-Spacing: a  b\t c"` | `HTTP-21`: the model keeps the original casing, and the adapter sends it |
| `Pipeline/*`, `Pagination/*`, `Pipeline/Policies/*` tests (~140 sites) | `new Response(...)`; `request with { Headers = … }` / `{ Url = … }` | `TestResponses.Create(...)`; `WithHeaders(...)` / `WithUrl(...)` | mechanical; the `Response` constructor and the get-only `Request` (`HTTP-3`, `HTTP-4`) |

## Deviation ledger, as built

| ID | As built | Route |
|---|---|---|
| P2a-1 | "Body by value" is read as value equality over the bytes of an in-memory body (bytes, string, value, replayable) with equal content types, and identity for single-use stream bodies and unknown `RequestBody` subclasses; the hash covers content type and length only. Documented on `RequestBody`'s remarks. | design §11 item, task 9.6 |
| P2a-2 | `HTTP-47`'s offending input is carried as `UrlRedactor` renders it, through one shared internal `UrlRedactor.Default`; `https://user:secret@h:bad/` becomes `[malformed url]`. | design §10 entry, task 9.6 |
| P2a-3 | An obs-text `ETag` is valid under `HTTP-48`, and `RequestConditions.ApplyTo` throws `ArgumentException` for it because `Headers.Set` enforces `HTTP-18`'s outbound rule. | design §11 item, task 9.6 |
| P2a-4 | An absent response body is an empty buffered body, never `null`. The empty body is **replayable** (`ResponseBody.FromReplayableBytes`), where the as-built default was `FromBytes` (single-use): the plan's `An_absent_body_is_an_empty_buffered_body_never_null` reads it twice and expects empty both times. | design §10 entry, task 9.6 |

## Findings while building

The plan's own "Findings" section was resolved at review and is not repeated. What building added:

1. **Vector sources.** `nodejs-sdk@54aeed4` and `ruby-sdk@5b17395` are not in the local clones. The vectors cite
   `nodejs-sdk@c0ff3fd`, the checkout the cases were read from; `ruby-sdk@90075b1` carries documents only (no test
   code), so Node is the only source of the ported tables.
2. **Test namespaces.** `Dexpace.Sdk.Core.Tests.Http.Requests` and `…Responses` (folders `Http/Request/` and
   `Http/Response/`): the plan's mirrored namespace shadows the `Request` and `Response` types for the older tests in
   `Dexpace.Sdk.Core.Tests.Http`.
3. **`ReasonPhraseWireTests`.** The plan puts the loopback reason-phrase tests in `SystemNetHttpClientTests`, whose
   header says it is socket-free and `Unit`. They are a new `Integration` class beside `HeaderCasingWireTests`; the
   handler-stub equivalents are in `SystemNetHttpClientTests`.
4. **AotSmoke.** `tests/Dexpace.Sdk.AotSmoke` does not reference `TestSupport`, so its fake transport passes the request
   to `new Response(request, Status.Ok, Protocol.Http11, …)` directly instead of through `TestResponses.Create`.
5. **`Set(name, null)` on `Query.Builder`** is ambiguous between `Set(string, string?)` and
   `Set(string, IEnumerable<string?>)` for a bare `null` literal; callers write `(string?)null`, as the tests do. The
   shape is the design's.
6. **`Request`'s URL error helper.** `CA2208` wants a real parameter name, so the URL error
   helper takes `nameof(url)` from each caller.
7. **`dotnet build` and `dotnet format` disagreed once** (an `xUnit2017` finding that an incremental build did not
   report); the local gate builds with `--no-incremental`.

## 2b hand-off

`RequestOptions` is merged (the only hard gate of 2b); `Query` and the internal `Rfc3986` are available for the
projection (`SEAM-27`); `Response`'s constructor changed (`Request`, required `Protocol`, `ReasonPhrase`), as did the
files 2b's SPI PR also edits (`SystemNetHttpClient.cs`, `TestSupport/Transports/*`, `AotSmoke/SmokeChecks.cs`). A
rebase re-derives the `ToResponse(HttpResponseMessage, Request)` change rather than resolving hunks.
