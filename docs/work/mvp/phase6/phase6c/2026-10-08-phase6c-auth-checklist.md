# Phase 6c — Authentication: Checklist

The execution-time checklist for sub-phase 6c of roadmap phase 6
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `74-phase-6c-auth` (issue #74), following the
[design](2026-10-08-phase6c-auth-design.md) and the [plan](2026-10-08-phase6c-auth.md). User page:
[`auth.md`](../../../../sdk-documentation/auth.md).

The scope is 38 rows, `AUTH-1`-`AUTH-38` (36 MUST, 2 SHOULD: `AUTH-19`, `AUTH-38`), all ✅. 6c also did the work for the rows of other owners listed in the
[carried-rows table](#work-on-other-owners-rows); each of those owners' checklists gets one dated correction (task 8.4). Every test is
`[Trait("Category", "Unit")]` and lives in `tests/Dexpace.Sdk.Core.Tests/` unless another place is named; `CredentialRedactionTests` and `AuthHttpsGuardTests` are `Security`,
`DigestWireTests` is `Integration` (`tests/Dexpace.Sdk.Http.SystemNet.Tests/`), and the smoke check is `AotSmoke`.

**Adaptation to the as-built code.** The plan's eight PRs landed as commits on the one branch in the plan's order: descriptor and resolver, credentials, challenge parser
with the Basic and composite handlers, the `401` lifecycle, the token cache (PR 5), Digest, the shared stampers (`chore:`) with `MultiSchemeAuthPolicy`, and the close-out
(the AOT smoke, the user page, this checklist, the dated corrections). The deviations from the plan are listed [below](#deviations-from-the-plan).

**Red evidence.** Tests were written beside the production change in each commit, and for every new type the first compile of the test project was red (a missing type
or member). The plan's request to prove each *pin* able to fail by temporary mutation (convention 1) was **not** done for the pins that pass on unchanged behaviour
(`The_stage_order_is_Redirect_then_Retry_then_Auth`, `Stage_is_Auth_and_sealed`, `A_second_auth_policy_in_one_pipeline_is_rejected_by_the_builder`,
`SuppressFlow_appears_only_in_BackgroundWork` has a scanner self-test instead).

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files (all under `tests/Dexpace.Sdk.Core.Tests/` unless named): `Auth/{AuthDescriptorTests,AuthResolverTests,CredentialTypesTests,AuthenticationChallengeTests,BasicChallengeHandlerTests,
DigestChallengeHandlerTests,DigestComputationTests,Md5AvailabilityTests,NonceCounterTests,AccessTokenCacheTests}.cs`; `Http/Request/RequestOptionsTests.cs`;
`Pipeline/Policies/{AuthorizationPolicyChallengeTests,ApiKeyAuthPolicyTests,BasicAuthPolicyTests,BearerTokenAuthPolicyTests,ChallengeAuthPolicyTests,MultiSchemeAuthPolicyTests}.cs`;
`Pipeline/BearerPipelineTests.cs`; `Security/{CredentialRedactionTests,AuthHttpsGuardTests}.cs`; `Exceptions/{HttpsRequiredExceptionTests,TokenProviderExceptionTests}.cs` (folder `Errors/`);
`Diagnostics/AuthLogTests.cs`; `Internal/{AuthOriginTests,BackgroundWorkTests,BoundedMapExtensionsTests}.cs`; `Architecture/BannedSymbolsSiteTests.cs`;
`tests/Dexpace.Sdk.Http.SystemNet.Tests/DigestWireTests.cs` (`Integration`); the AOT smoke check is `CheckPhase6cAuthAsync` in `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`.
Vectors: `tests/vectors/auth/{challenges,digest}.json`.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `AUTH-1` | MUST | ✅ | `AuthDescriptorTests.AuthScheme_values_are_explicit_and_exactly_five`, `A_requirement_rejects_an_undefined_scheme`, `NoAuth_is_the_anonymous_sentinel` (Unit) |
| `AUTH-2` | MUST | ✅ | `AuthDescriptorTests.Scopes_and_parameters_are_copied_at_init`, `Scopes_and_parameters_are_preserved_and_never_interpreted`, `Requirements_with_equal_content_are_equal_and_hash_alike`, `A_requirement_does_not_print_a_parameter_value` (Unit) |
| `AUTH-3` | MUST | ✅ (P6c-4: `ImmutableArray<T>` implements `IList<T>` explicitly, so read-only is asserted by the mutator throwing, not by an uncastable type) | `AuthDescriptorTests.A_descriptor_copies_its_requirements_and_exposes_them_read_only`, `An_empty_descriptor_or_a_null_element_throws_ArgumentException`, `AllowsAnonymous_is_true_iff_any_requirement_is_NoAuth`, `A_descriptor_has_no_init_member_so_with_cannot_empty_it` (Unit) |
| `AUTH-4` | MUST | ✅ (P6c-6, P6c-7) | `AuthResolverTests.Per_call_wins_over_operation_and_client`, `Operation_wins_over_client`, `Client_is_used_when_the_others_are_absent`, `A_higher_tier_that_cannot_be_satisfied_does_not_fall_through`; `RequestOptionsTests.Auth_and_OperationAuth_default_to_null` and the equality cases; `AuthorizationPolicyChallengeTests.The_operation_tier_is_used_when_no_per_call_tier_is_set`; `MultiSchemeAuthPolicyTests` (Unit) |
| `AUTH-5` | MUST | ✅ | `AuthResolverTests.The_first_satisfiable_requirement_in_preference_order_wins`, `NoAuth_is_always_satisfiable`, `No_credential_is_inspected`; `MultiSchemeAuthPolicyTests.The_client_descriptor_order_decides` (Unit) |
| `AUTH-6` | MUST | ✅ | `AuthResolverTests.All_tiers_absent_throws_ArgumentException`, `Nothing_satisfiable_throws_AuthResolutionException_with_required_and_available`; `AuthorizationPolicyChallengeTests.A_per_call_scheme_the_policy_cannot_serve_throws_AuthResolutionException_before_sending` (Unit) |
| `AUTH-7` | MUST | ✅ | `AuthResolverTests.Resolve_is_safe_under_concurrent_calls_on_one_descriptor` (a static class with no field) (Unit) |
| `AUTH-8` | MUST | ✅ (P6c-19: the named-key clause is vacuous in a port with one key type) | `CredentialRedactionTests` (all four), `AccessTokenTests.AccessToken_equality_is_by_value_over_token_expiry_and_refresh`, `AccessToken_ToString_redacts_the_token`, `ApiKeyCredentialTests.ApiKeyCredential_ToString_redacts_the_key`, `BasicCredentialTests.BasicCredential_ToString_redacts_the_password` (Unit, Security) |
| `AUTH-9` | MUST | ✅ (the named-key clause is vacuous) | `AccessTokenTests.AccessToken_rejects_null_empty_and_whitespace_tokens`, `ApiKeyCredentialTests.ApiKeyCredential_rejects_a_whitespace_only_key` (Unit) |
| `AUTH-10` | MUST | ✅ | `AccessTokenTests.AccessToken_without_expiry_never_expires`, `IsExpired_is_strictly_after` (Unit) |
| `AUTH-11` | MUST | ✅ (met before; now with a sync-throw arm) | `AccessTokenCacheTests.A_provider_exception_propagates_unwrapped_and_nothing_is_cached`, `A_synchronous_throw_from_a_non_async_GetTokenAsync_override_surfaces_through_the_returned_task` (Unit) |
| `AUTH-12` | MUST | ✅ | `AuthenticationChallengeTests.Parse_matches_every_vector` (46 rows of `tests/vectors/auth/challenges.json`, both overloads), `Scheme_and_parameter_names_are_lower_cased_with_ASCII_folding_only` (en-US and tr-TR), `Values_are_never_folded`, `The_constructor_folds_parameter_names_and_copies_them`, `Token68_is_Parameters_token68` (Unit) |
| `AUTH-13` | MUST | ✅ | `AuthenticationChallengeTests.Parse_never_throws_for_arbitrary_text` (5 000 seeded inputs), `Parse_never_throws_over_the_metacharacter_alphabet` (100 000 inputs), `Parse_is_linear_in_the_input`, and the leniency rows of the vectors (Unit) |
| `AUTH-14` | MUST | ✅ (P6c-17: the `:` rule is stricter than both siblings) | `BasicChallengeHandlerTests.Value_is_Basic_plus_base64_of_UTF8_user_colon_password`, `The_value_equals_the_one_BasicAuthPolicy_stamps_preemptively`; `BasicCredentialTests.BasicCredential_rejects_empty_username_or_password_but_allows_whitespace`, `BasicCredential_rejects_a_colon_in_the_username`; `BasicAuthPolicyTests.Preemptive_Basic_is_stamped_on_the_first_request` (Unit) |
| `AUTH-15` | MUST | ✅ (MD5 is platform-dependent: design §10 entry 16, P6c-30) | `DigestChallengeHandlerTests` FIPS cases (`With_MD5_unavailable_an_MD5_only_challenge_is_declined`, `…_falls_through_to_SHA256`, `Construction_never_fails_on_a_host_without_MD5_…`), `Every_vector_row_produces_its_expected_Authorization_or_null`, `No_rspauth_verification`; `Md5AvailabilityTests`; `DigestAlgorithmTests` (Unit) |
| `AUTH-16` | MUST | ✅ (a `-sess` challenge without qop is declined: P6c-31, design §11 new item) | `DigestChallengeHandlerTests.Chosen_by_configured_preference_not_wire_order`, `Sess_without_qop_is_declined`, `Satisfiable_requires_digest_realm_nonce`, `Qop_absent_or_a_list_containing_auth_is_accepted`, `Algorithm_absent_means_MD5`, `A_caller_preference_is_any_non_empty_duplicate_free_subset` (Unit) |
| `AUTH-17` | MUST | ✅ | `DigestComputationTests.The_RFC_2617_vectors_reproduce`, `The_RFC_7616_inputs_give_the_re_derived_SHA256_values`, `HA1_sess_folds_nonce_and_cnonce`, `Hex_is_lower_case`; the 38 rows of `tests/vectors/auth/digest.json` (Unit) |
| `AUTH-18` | MUST | ✅ | `NonceCounterTests.First_use_of_a_nonce_is_nc_00000001`, `Reuse_of_the_same_nonce_increments`, `A_different_nonce_starts_at_one`, `Nc_is_eight_lower_case_hex_digits_of_the_low_32_bits` (Unit) |
| `AUTH-19` | SHOULD | ✅ | `NonceCounterTests.The_store_is_a_BoundedMap_of_1024` (Unit) |
| `AUTH-20` | MUST | ✅ | `DigestChallengeHandlerTests.The_cnonce_default_is_32_lower_case_hex_characters_and_fresh_each_time`; `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator` (Unit) |
| `AUTH-21` | MUST | ✅ (P6c-32: an unrepresentable credential is declined, not hashed lossily) | `DigestComputationTests.Encoding_is_UTF8_under_charset_UTF8_else_Latin1`, `A_Latin1_unrepresentable_credential_is_reported_as_not_encodable_not_hashed_with_question_marks`; vector rows `utf8_charset_*`, `latin1_*`; `DigestChallengeHandlerTests.A_declined_attempt_does_not_advance_nc` (Unit) |
| `AUTH-22` | MUST | ✅ (P6c-33: `username*`; P6c-34: the escaped request-target) | `DigestChallengeHandlerTests.Username_realm_nonce_uri_response_cnonce_opaque_are_quoted_qop_nc_algorithm_are_bare`, `Algorithm_is_always_emitted_in_full_spelling`, `A_username_with_non_ascii_goes_out_as_username_star`, `An_unechoable_realm_nonce_or_opaque_is_declined`; `DigestWireTests.The_uri_parameter_equals_the_request_target_the_server_received` (Integration) (Unit, Integration) |
| `AUTH-23` | MUST | ✅ | `CompositeChallengeHandlerTests` (all), `ChallengeAuthPolicyTests.A_composite_digest_then_basic_falls_back_to_basic_when_digest_declines`, `A_composite_prefers_digest_when_the_server_offers_both` (Unit) |
| `AUTH-24` | MUST | ✅ | `NonceCounterTests.Sixty_four_parallel_Authorize_calls_on_one_nonce_yield_nc_1_to_64_without_duplicates`, `Handlers_hold_no_state_except_the_counters`; `CompositeChallengeHandlerTests.Is_stateless_and_safe_under_parallel_use` (Unit) |
| `AUTH-25` | MUST | ✅ | `ChallengeHandlerContractTests` (Basic, composite, two Digest challenges), `BasicChallengeHandlerTests.Sets_Proxy_Authorization_when_proxy_is_true`, `DigestChallengeHandlerTests.Proxy_true_sets_Proxy_Authorization_only` (Unit) |
| `AUTH-26` | MUST | ✅ (met but unreconciled in §12 before; now addressed, P6c-19) | `ApiKeyCredentialTests.ApiKeyCredential_scheme_must_be_null_or_non_blank_without_whitespace`, `ApiKeyCredential_rejects_a_value_the_outbound_header_grammar_refuses`, `ApiKeyCredential_exposes_the_stamped_value_computed_once`; `ApiKeyAuthPolicyTests.The_header_value_is_stamped_from_the_credential_computed_once`, `A_custom_header_is_withheld_cross_origin` (Unit) |
| `AUTH-27` | MUST | ✅ (pin) | `AuthorizationPolicyChallengeTests.Stage_is_Auth_and_sealed`, `The_stage_order_is_Redirect_then_Retry_then_Auth`, `A_second_auth_policy_in_one_pipeline_is_rejected_by_the_builder`; `BearerPipelineTests` (Unit) |
| `AUTH-28` | MUST | ✅ (the guard covers a reactive scheme's outbound pass: P6c-8) | `AuthHttpsGuardTests` (strengthened: `HttpsRequiredException`, scheme `http`), `AuthorizationPolicyChallengeTests.A_non_https_url_throws_HttpsRequiredException_before_any_credential_is_resolved`, `The_guard_also_covers_a_reactive_schemes_outbound_pass`, `Case_insensitive_scheme_compare`; `ChallengeAuthPolicyTests.The_guard_refuses_http_before_the_first_request`; `HttpsRequiredExceptionTests` (Unit, Security) |
| `AUTH-29` | MUST | ✅ (pin; no marker, §10 entry 15; P6c-12: a cross-origin `401` is not challenge-handled) | `AuthorizationPolicyChallengeTests.A_cross_origin_hop_has_its_credential_header_removed_and_is_not_guarded`, `A_cross_origin_401_is_returned_without_challenge_handling`, `No_marker_property_is_set_on_the_context`; `BearerPipelineTests.A_pipeline_with_redirect_forwards_no_bearer_cross_origin`; `AuthOriginTests` (Unit) |
| `AUTH-30` | MUST | ✅ | `AuthorizationPolicyChallengeTests.A_401_with_WWW_Authenticate_calls_the_hook_with_the_parsed_challenges`, `A_non_null_replacement_is_driven_once_through_the_continuation`, `No_further_challenge_handling_after_the_replay`, `The_default_hook_yields_null_and_the_401_is_returned`, `The_401_is_disposed_before_the_replay_and_the_returned_response_is_the_replays`, `Each_WWW_Authenticate_field_is_parsed_on_its_own`; `ChallengeAuthPolicyTests`; `BearerTokenAuthPolicyTests.A_second_401_after_the_retry_is_returned_without_another_retry` (Unit) |
| `AUTH-31` | MUST | ✅ | `AuthorizationPolicyChallengeTests.A_replacement_with_a_non_replayable_body_is_not_sent_and_the_original_401_is_returned_undisposed` (both paths), `A_replacement_with_a_replayable_or_no_body_is_sent`, `The_gate_applies_to_every_replacement_regardless_of_which_hook_built_it`; `BearerTokenAuthPolicyTests.A_stream_body_is_not_retried_and_the_401_is_returned_undisposed`, `The_retry_is_method_agnostic` (Unit) |
| `AUTH-32` | MUST | ✅ | `AuthorizationPolicyChallengeTests.A_hook_that_throws_disposes_the_401_and_propagates_the_exception` (sync hook, async hook, synchronous throw from the async override), `A_failing_dispose_is_suppressed_onto_the_hook_exception` (Unit) |
| `AUTH-33` | MUST | ✅ | `AuthorizationPolicyChallengeTests.A_401_without_WWW_Authenticate_is_returned_unchanged_and_the_hook_is_not_called`, `A_non_401_with_the_header_is_returned_unchanged` (Unit) |
| `AUTH-34` | MUST | ✅ | `AccessTokenCacheTests.The_default_margin_is_30_seconds_and_configurable`, `The_hot_path_takes_no_lock`, `A_negative_margin_throws`, `N_concurrent_requests_at_expiry_cost_one_fetch`, `With_a_failing_provider_N_waiters_fail_together_after_one_attempt`, `A_waiters_own_cancellation_leaves_the_fetch_running`, `A_fetchers_own_cancellation_is_not_adopted_by_the_waiters` (Unit) |
| `AUTH-35` | MUST | ✅ | `AccessTokenCacheTests.A_default_token_from_the_provider_throws_TokenProviderException`, `A_token_already_expired_at_fetch_throws_TokenProviderException_and_never_contains_the_token`, `A_request_arriving_after_a_failure_fetches_again`; `TokenProviderExceptionTests` (Unit) |
| `AUTH-36` | MUST | ✅ | `AccessTokenCacheTests.A_rejection_of_the_current_token_evicts_it_and_fetches_fresh`, `A_rejection_of_a_stale_header_keeps_a_token_another_request_already_refreshed`, `Eviction_and_fetch_happen_under_one_critical_section`; `BearerTokenAuthPolicyTests.A_401_with_a_Bearer_challenge_evicts_fetches_fresh_and_retries_once` (both paths), `A_401_with_a_non_bearer_challenge_is_returned`, `A_provider_that_hands_back_the_rejected_token_yields_no_retry` (Unit) |
| `AUTH-37` | MUST | ✅ | `AccessTokenCacheTests.A_token_inside_the_margin_is_stamped_now_and_refreshed_in_the_background`, `Only_one_background_refresh_runs_per_key_however_many_requests_arrive`, `The_background_refresh_sees_no_ambient_Activity`, `A_failing_background_refresh_logs_event_160_once_and_the_triggering_call_is_unaffected`, `The_refresh_task_is_observed_and_completes_without_faulting`; `AuthLogTests`; `BackgroundWorkTests` (Unit) |
| `AUTH-38` | SHOULD | ✅ (by construction, pinned) | `AuthorizationPolicyChallengeTests.ProcessAsync_returns_a_faulted_task_instead_of_throwing_for_an_http_url`, `ProcessAsync_faults_for_a_resolver_failure_and_a_credential_that_throws_synchronously`, `Process_throws_the_original_exception_on_the_sync_path` (Unit) |

Count: **38 rows**, all ✅ (0 ⏳, 0 🚫, 0 N/A).

### Work on other owners' rows

These carry no mark in this checklist. 6c's tests are cited by the owner's row.

| ID (owner) | Work | Evidence |
|---|---|---|
| `XCUT-12` (10) | Per-key semaphore, never global | `AccessTokenCacheTests.Different_keys_do_not_block_each_other` |
| `XCUT-14` (10) | The token cache and the Digest nonce store are `BoundedMap`s | `AccessTokenCacheTests.The_cache_holds_at_most_1024_keys_once_quiescent`, `An_evicted_live_entry_costs_one_extra_fetch_and_no_failure`; `NonceCounterTests.The_store_is_a_BoundedMap_of_1024` |
| `XCUT-16` (10) | The typed guard and the reactive reading | `AuthHttpsGuardTests`, `HttpsRequiredExceptionTests` |
| `XCUT-18` (10) | The echoed Digest values and the key value pass the outbound grammar | `ApiKeyCredentialTests.ApiKeyCredential_rejects_a_value_the_outbound_header_grammar_refuses`, `DigestChallengeHandlerTests.An_unechoable_realm_nonce_or_opaque_is_declined` |
| `XCUT-19`(d) (10; phase 1 S5 remainder) | Credentials never reveal secrets in string form | `CredentialRedactionTests` (Security) |
| `XCUT-21` (10) | The cnonce is from `RandomNumberGenerator` | `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator` |
| `BODY-4` (3b) | The `AUTH-31` gate, auth's third of the hand-off | the `AUTH-31` tests |
| `PIPE-5` (4c) | One policy per pillar | `AuthorizationPolicyChallengeTests.A_second_auth_policy_in_one_pipeline_is_rejected_by_the_builder` |
| `PIPE-28` (4c) | One fewer documented bridge in core | `BearerTokenAuthPolicyTests.Process_sync_stamps_through_the_real_sync_path`, `The_sync_and_async_paths_share_one_cache`; `AuthorizationPolicyChallengeTests.The_sync_and_async_paths_produce_identical_requests` |
| `CTX-19` (4a) | Background work does not pin a call | `BackgroundWorkTests.The_work_observes_no_Activity_current` |
| `OBS-39` (5b) | Event id 160 inside the reserved auth range | `AuthLogTests.TokenRefreshFailed_is_id_160_named_dexpace_auth_token_refresh_failed_at_Warning`; `LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` |
| `REDIR-7`, `REDIR-11`, `REDIR-24` (6b/4c) | The seed origin the policy compares against; no marker | `AuthorizationPolicyChallengeTests` cross-origin cases |

## Existing assertions changed

None was deleted without a replacement.

| Test | Was | Now |
|---|---|---|
| `BasicAuthPolicyTests.ProcessAsync_EmptyPassword_StampsCorrectly` | an empty password stamps `Basic dXNlcjo=` | `BasicCredential_with_an_empty_password_is_rejected_at_construction` (`AUTH-14`, breaking 3) |
| `AccessTokenCacheTests.GetAsync_PastRefreshOn_RefreshesOnce` | the refresh was awaited on the request path and the call returned the new token | `Past_RefreshOn_the_valid_token_is_stamped_and_one_background_refresh_runs` (breaking 5) |
| `AccessTokenCacheTests.GetAsync_RefreshThrows_WhileStillValid_ReturnsCachedToken` | a failed refresh was swallowed silently | `A_failed_background_refresh_while_valid_stamps_the_valid_token_and_logs` (event 160) |
| `BearerTokenAuthPolicyTests.Process_sync_stamps_through_the_documented_bridge` | the sync path blocked on the async one | `Process_sync_stamps_through_the_real_sync_path` (the credential's `GetTokenAsync` throws; `GetToken` answers) |
| `TokenRequestContextTests.Ctor_SetsScopes` | `Assert.Same(scopes, ctx.Scopes)` | `Assert.Equal` (the scopes are a copy, breaking 4) |
| `LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` (not `Security`) | 160 to 169 reserved and unused | 160 is `TokenRefreshFailedId`; 161 to 169 stay reserved |
| `AuthHttpsGuardTests` (`Security`) | `typeof(SdkException)`, `ThrowsAsync<SdkException>`, the old `CountingAuthPolicy` signature | the exact diff of plan task 4.5: the re-signatured probe, `typeof(HttpsRequiredException)`, `ThrowsAsync<HttpsRequiredException>`, `Assert.Equal("http", ex.Scheme)`; nothing removed or loosened |

## Security evidence

`git diff --stat origin/main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` lists exactly two files: `CredentialRedactionTests.cs` (added)
and `AuthHttpsGuardTests.cs` (modified as above). Every other `Security` class is untouched, and the two Security suites pass (see the gate results below). The SystemNet and
System.Text.Json `PublicAPI.Unshipped.txt` files are unchanged.

## Vector provenance

| File | Source | Not ported, and why |
|---|---|---|
| `tests/vectors/auth/challenges.json` (46 rows) | `ruby-sdk gems/dexpace-core/test/dexpace/auth/{challenge,challenges}_test.rb` and `test/support/challenge_fixtures.rb` (the eight strings are `ChallengeFixtures`); `nodejs-sdk@54aeed4 packages/core/src/auth/challenge.test.ts`; RFC 7235 section 2.1; RFC 6750; RFC 7617 | Ruby's frozen-ness and `.build` construction cases (a different language's object model); the 100 KB and `Process.clock_gettime` timing tests (replaced by the allocation-ratio and 1 MiB tests); the per-pattern `Regexp#timeout` pins (no regular expression exists here); Node's fast-check properties (replaced by two seeded loops). Node's duplicate-last-wins differs from P6c-15 and the `duplicate_parameter_first_wins` row says so |
| `tests/vectors/auth/digest.json` (38 rows) | RFC 2617 section 3.5; RFC 7616 section 3.9.1; `ruby-sdk .../digest_handler_test.rb`; `nodejs-sdk@54aeed4 packages/core/src/auth/digest.test.ts` | The expected `Authorization` values were generated by an **independent Python implementation** (`hashlib`), not by the C# code, and the four published values were asserted equal in that script. Ruby's `UnencodableCredentialError` cases became declines (P6c-32); Node's `-sess` cnonce-without-qop differs (P6c-31); Node's `md5.test.ts` is not ported (it tests Node's own MD5) |

## To-verify facts (plan task 0.1), run on 10.0.401

The note is `phase6c-preflight.md` in the implementer's scratch directory (not committed); the outcomes:

| Fact | Outcome |
|---|---|
| 4 | `Convert.ToHexStringLower([0xAB, 0x0F])` is `ab0f` |
| 7 | `MD5.HashData([])` succeeds on this host and `MD5.IsSupported` does **not** exist on `net10.0`, so the probe tries a hash (R13). No FIPS-mode OpenSSL was available, so the FIPS exception type was not observed; the probe catches `CryptographicException` and `PlatformNotSupportedException` |
| 8 | A task started inside `ExecutionContext.SuppressFlow()` while `Activity.Current` is non-null sees `Activity.Current == null` |
| 9 | `SocketsHttpHandler` writes exactly `Uri.GetComponents(PathAndQuery, UriEscaped)` as the request-target for `/a%2Fb?x=%26y`, an empty path (`/`), an empty path with a query (`/?q=1`) and `/p%20q/r?a=b%20c&d=%7E` (both write `d=~`). P6c-34 stands; `DigestWireTests` pins it |
| 13 | The RFC 2617 vectors and the re-derived SHA-256 and SHA-256-sess values equal the design's |
| extras | `SemaphoreSlim.Wait(0)` returns true then false; `Encoding.Latin1.GetBytes("é€")` is `E9 3F`; `RandomNumberGenerator.GetHexString(32, lowercase: true)` is 32 characters; an exception-fallback ISO-8859-1 encoding throws `EncoderFallbackException` on an unrepresentable character |

The knowledge queries named in the plan were not run: `scripts/knowledge` is a `dotnet run` wrapper over the repository's own tools, and the implementer worked with the
SDK from a scratch install rather than the global `dotnet`. The design's file-based reading of the corpus stands.

**Flake guard (plan task 5.3).** `AccessTokenCacheTests` (42 tests, including every concurrency case) was run 200 times in a row against the Release binary: 200 runs, 0 failures.

## Deviations from the plan

1. **`AuthDescriptor.Requirements` is castable to `IList<T>`** (task 1.1's `…not castable to IList` assertion): `ImmutableArray<T>` implements `IList<T>` explicitly. The test asserts what the requirement
   needs (every mutator throws `NotSupportedException`).
2. **Two `#pragma warning disable CA5351` sites** (`DigestComputation.HashHex`, `Md5Availability`'s probe) with a why-comment: MD5 is the Digest protocol's own hash. The plan said to add no waiver for `MA0051`; this is a different rule and the only one added.
3. **`HttpsRequiredExceptionTests`, `TokenProviderExceptionTests` live in `tests/…/Errors/` but declare `namespace …Tests.Exceptions`**: a `Tests.Errors` namespace would shadow `Dexpace.Sdk.Core.Errors` for the rest of the test project.
4. **`A_401_without_a_stamped_Authorization_header_is_returned` (task 5.5) was not written**: no public path produces a bearer `401` whose stamped request lacks the header (a `NoAuth` call and a cross-origin hop never reach the hook). The case is replaced by `A_401_without_WWW_Authenticate_is_returned_without_a_retry`.
5. **`No_marker_property_is_set_on_the_context`** asserts through reflection that `CallState`'s lazily created property dictionary is still `null` (the dictionary is keyed by reference, so keys cannot be enumerated).
6. **The Digest handler tries the next candidate when the best one cannot carry the credential** (a Latin-1 challenge preferred over a UTF-8 one for a password with a character above U+00FF): the plan said "decline"; the handler declines only when no candidate can be answered. The vector `latin1_unrepresentable_falls_to_a_utf8_challenge` pins it.
7. **`nc` is consumed only when `qop` is negotiated**: the legacy no-`qop` form has no `nc` (the plan was silent).
8. **`AccessTokenCache.GetAfterRejection(Async)` takes no logger** (the plan listed `(context, rejectedHeaderValue, ct)`; the design block had a logger overload, which nothing needs).
9. **`BackgroundWork.Run` skips `SuppressFlow` when flow is already suppressed** (a nested call would throw `InvalidOperationException`).
10. **`AccessTokenCache` test doubles**: the cache tests share an `internal sealed class GatedCredential` (a per-call `TaskCompletionSource` gate) in `AccessTokenCacheTests.cs`; `file`-scoped types cannot appear in a member signature.
11. **The `LogVocabularyTests` id-range test was edited** (listed above); the plan did not name it.
12. **`ChallengeHandlerContractTests` covers Basic, composite and two Digest challenges** (the plan listed two shipped handlers plus Digest "added in task 6.4").
13. **The five-PR→commit mapping**: PR 5 and PR 6 were developed together and split into two commits by file; the PR 5 commit was built on its own and is green.
14. **No `PackageReference` change and no lock file changed.**

## Deviation ledger as built

The rulings below are the design's. The lead has not ruled on the open ones, so each was **taken as designed**; the state column says what was built.

| ID | Decision | State |
|---|---|---|
| P6c-2 | the resolver and carriers in core, not in a new package | built as designed (open for the lead) |
| P6c-6 | per-call and operation tiers on `RequestOptions.Auth` / `OperationAuth` | built as designed (open for the lead) |
| P6c-7 | every auth policy resolves; a per-call `NoAuth` sends anonymously | built as designed (open for the lead) |
| P6c-8 | `HttpsRequiredException`; the guard also covers a reactive scheme's outbound pass | built as designed (open for the lead) |
| P6c-12 | a cross-origin `401` is returned without challenge handling | built as designed (open for the lead) |
| P6c-17 | `BasicCredential` rejects empty parts and a `:` in the username | built as designed (open for the lead) |
| P6c-19 | no `NamedKeyCredential`; the named-key clauses of `AUTH-8`/`AUTH-9` are vacuous | built as designed (open for the lead) |
| P6c-21 | `AccessToken` equality includes `RefreshOn` | built as designed (open for the lead) |
| P6c-25 | a background refresh under flow suppression, never cancelled | built as designed (open for the lead) |
| P6c-29 | the default Digest preference is SHA-256 first | built as designed (open for the lead) |
| P6c-30 | the MD5 probe, once per process | built |
| P6c-31 | a `-sess` challenge without `qop` is declined | built as designed (open for the lead) |
| P6c-33 | a non-ASCII username goes out as `username*` | built as designed (open for the lead) |
| P6c-36 | `MultiSchemeAuthPolicy` over shared stampers | built as designed (open for the lead; R12: if dropped, PR 7 and its end-to-end share go, `AUTH-4`/`AUTH-5` stay ✅ through the single-scheme policies and `AuthResolverTests`) |
| P6c-38 | `Convert.ToHexStringLower` on `net10.0` | built |

Plan readings: **R3** (the temporary bearer sync bridge in PR 4) was built as written and removed in PR 5; **R12** as above.

## Gate results

Recorded when the branch was closed.

Run on the branch head before the close-out commit, .NET SDK 10.0.401:

| Gate | Result |
|---|---|
| `dotnet restore Dexpace.Sdk.sln --locked-mode` | ok, no lock file changed |
| `dotnet build Dexpace.Sdk.sln -c Release` (warnings as errors) | 0 warnings, 0 errors |
| `dotnet format Dexpace.Sdk.sln --verify-no-changes` | clean |
| `dotnet test --solution Dexpace.Sdk.sln -c Release` | 4 269 tests, 4 268 passed, 0 failed, 1 skipped (a pre-existing skip) |
| `Security` suites (core, SystemNet) | 263 and 30 tests, all passed |
| Coverage gate (floor 80 %) and its self-test | aggregate 97.56 % (Core 97.76 %, SystemNet 93.75 %, System.Text.Json 88.00 %); the three self-test cases ok |
| NativeAOT smoke (`dotnet publish tests/Dexpace.Sdk.AotSmoke` and run) | "aot-smoke: all checks passed", no trim or AOT warning |
| `dotnet pack`, `scripts/ci/dependency-audit.cs`, `scripts/ci/reproducible-pack.sh` | three libraries conform; the three packages are byte-identical across two packs |
| `tools/Dexpace.Tools.sln` build and test | 0 warnings; 438 tests passed |
| `scripts/knowledge verify-structure` | OK (12 notes) |
| `housekeeping probe` (full, then `--only links,citations`) | no drift found |
