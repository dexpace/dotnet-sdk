# Phase 6b — Redirect: Checklist

The execution-time checklist for sub-phase 6b of roadmap phase 6
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `73-phase-6b-redirect` (issue #73), following the
[design](2026-10-08-phase6b-redirect-design.md) and the [plan](2026-10-08-phase6b-redirect.md). User page:
[`redirect.md`](../../../../sdk-documentation/redirect.md).

The scope is 28 rows, `REDIR-1`-`REDIR-28` (23 MUST, 4 SHOULD, 1 MAY): **26 ✅, 1 🚫 (`REDIR-25`), 1 ⏳ (`REDIR-27`)**, 0 N/A. 6b also did
the work for nine rows owned elsewhere, listed in the [cross-owner table](#work-on-other-owners-rows). Every test is
`[Trait("Category", "Unit")]` and lives in `tests/Dexpace.Sdk.Core.Tests/` unless another place is named. Phase 6b **edits one `Security`
class** (`RedirectCredentialHygieneTests`, three hunks, [below](#the-security-classes)) and **adds two** (`RedirectCredentialLeakTests`,
`RedirectCredentialLeakWireTests`).

**Adaptation to the as-built code.** The plan's three PRs landed as commits on the one branch, in the plan's order: the decision
function, options, exceptions and the rewritten policy (PR 1, four commits); the events (PR 2); the convergence test, the AOT check
and the close-out (PR 3). The deviations from the plan are listed [below](#deviations-from-the-plan).

**Red evidence.** For each new type the first compile of the test project was red (a missing type or member), and the rewritten
`RedirectPolicyTests` was written against the as-built policy: eight of the old facts failed the moment the policy was rewritten
(`302`/`301`/`303` rewrites, the silent downgrade, the 20-hop default), which is why they were deleted rather than kept. Pins proven
able to fail by temporary mutation: `RedirectCredentialLeakTests` (with `RedirectReissue` keeping `Cookie` cross-origin, the
cross-origin rows of both auth kinds failed; the mutation was reverted and never committed). The matrix and `HttpOriginTests` pass on
first run and were not mutated.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `Pipeline/Policies/Redirect/{RedirectDecisionMatrixTests,RedirectCases,RedirectCase,RedirectLocationTests,RedirectChainTests,RedirectDecisionTests,RedirectReissueTests,RedirectFixtures}.cs`;
`Pipeline/Policies/RedirectPolicyTests.cs`; `Pipeline/ReDriveLifecycleTests.cs`; `Http/Common/HttpOriginTests.cs`; `Configuration/RedirectOptionsTests.cs`;
`Errors/RedirectExceptionTests.cs`; `Diagnostics/RedirectLogTests.cs`; `Security/{RedirectCredentialHygieneTests,RedirectCredentialLeakTests}.cs`;
`tests/Dexpace.Sdk.Http.SystemNet.Tests/Security/{RedirectWireTests,RedirectCredentialLeakWireTests}.cs`; the AOT smoke check is `CheckPhase6bRedirectAsync` in
`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`. "The matrix" is `RedirectDecisionMatrixTests.Decide_matches_the_table`, one `[Theory]` over the pure decider, 130 named rows.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `REDIR-1` | MUST | ✅ | the matrix, `REDIR-1: a {100,200,204,300,304,305,306,399} with a Location is not a redirect` (predicate never called) (Unit) |
| `REDIR-2` | MUST | ✅ | the matrix, `REDIR-2: a 300 / 304 / 305 …`; `RedirectPolicyTests.A_200_is_passed_through` (Unit) |
| `REDIR-3` | MUST | ✅ (P6b-4: the original method, literally) | the matrix, `REDIR-3: a {301,302,307,308} on a POST in AllowedMethods keeps POST and the body`, `…not eligible under the default set`, `eligibility is judged on the ORIGINAL method` (the Node row, inverted); `RedirectOptionsTests.Defaults_are_three_hops_…`; `RedirectPolicyTests.A_POST_302_is_returned_not_rewritten_to_a_GET`, `An_allowed_POST_307_keeps_the_method_and_the_body` (Unit) |
| `REDIR-4` | MUST | ✅ | the matrix, `REDIR-3: a 307/308 on …` and `REDIR-4: a {301,302,307,308} on a HEAD follows as a HEAD`; `REDIR-3: a 307 carries the body instance` (Unit) |
| `REDIR-5` | MUST | ✅ | `RedirectReissueTests.A_303_becomes_a_GET_from_any_method_with_no_body`, `A_303_removes_every_Content_header_by_case_insensitive_prefix`; the matrix `REDIR-5:` rows (six methods, the not-by-default rows, the predicate rebuild); `RedirectPolicyTests.An_opted_in_303_follows_as_a_bodiless_GET` (Unit) |
| `REDIR-6` | MUST | ✅ | `RedirectExceptionTests.The_replay_message_names_replayability_and_ToReplayableAsync`; the matrix `REDIR-6:` rows (single-use fails on 301/302/307/308, 303 exempt, bytes and seekable `FromStream` follow); `RedirectPolicyTests.A_replay_failure_disposes_the_response_once_and_throws_the_exception_unchanged`; `ReDriveLifecycleTests.A_redirect_over_a_non_replayable_body_throws_and_disposes_the_in_flight_response_once` (Unit) |
| `REDIR-7` | MUST | ✅ | `RedirectReissueTests.Authorization_is_removed_every_value_on_a_same_origin_hop`; the matrix `REDIR-7:` rows; `RedirectCredentialHygieneTests.Authorization_is_stripped_on_every_hop_even_same_origin`; `RedirectCredentialLeakTests`; `RedirectCredentialLeakWireTests` (Unit, Security) |
| `REDIR-8` | MUST | ✅ | `HttpOriginTests` (all eight); `RedirectChainTests.The_seed_origin_is_the_context_seed_requests_not_the_first_drive`; the matrix `REDIR-8:` rows (port, scheme, IDN, seed not previous hop); `RedirectCredentialLeakTests` "a port change only" (Unit, Security) |
| `REDIR-9` | MUST | ✅ | `RedirectReissueTests.Cookie_and_Proxy_Authorization_are_removed_every_value_cross_origin`, `A_cross_origin_303_also_loses_Cookie_and_Proxy_Authorization`; `RedirectCredentialHygieneTests`; `RedirectCredentialLeakTests`; `RedirectCredentialLeakWireTests.A_cross_origin_hop_then_a_retry_carries_no_credential_on_either_attempt` (Unit, Security) |
| `REDIR-10` | SHOULD | ✅ | `RedirectReissueTests.Cookie_and_Proxy_Authorization_survive_a_same_origin_hop`; the matrix `REDIR-10:`; `RedirectCredentialLeakTests` same-origin rows; `RedirectCredentialLeakWireTests.A_same_origin_hop_keeps_cookie` (Unit, Security) |
| `REDIR-11` | MUST | ✅ (no marker; design §10 entry 15, P6b-23) | `RedirectReissueTests.A_marker_shaped_header_is_an_ordinary_header`; the matrix `REDIR-11: a marker-shaped header is an ordinary header`; `RedirectChainTests.The_seed_origin_is_…` (clause a to c are vacuous: nothing is added, so nothing is cleared, sent on or stripped) (Unit) |
| `REDIR-12` | MUST | ✅ | `RedirectLocationTests.Userinfo_is_dropped`; the matrix `REDIR-12:`; `RedirectCredentialHygieneTests.Userinfo_in_the_location_is_dropped_before_re_issue` (Unit, Security) |
| `REDIR-13` | MUST | ✅ (residue, P6b-30: an explicit scheme-default port is elided by `AbsoluteUri`) | `RedirectLocationTests.Reserved_escapes_survive_resolution_and_stripping` (`%2F`, `%26`, fragment escape, IPv6 literal, `:8443`), `An_explicit_default_port_is_elided`; the matrix `REDIR-13:` rows. This row does not assert that `:443` survives. Origin and wire meaning are unchanged (Unit) |
| `REDIR-14` | MUST | ✅ | `RedirectLocationTests.A_relative_location_resolves_against_the_current_hop_not_the_seed`, `A_protocol_relative_location_keeps_the_current_scheme`, `An_absolute_location_is_used_as_is`; the matrix `REDIR-14:` rows (Unit) |
| `REDIR-15` | MUST | ✅ | the matrix `REDIR-15:` rows (fails without the opt-in; follows flagged with it; http to https; https to http to https flags only the middle hop; downgrade beats replay); `RedirectPolicyTests.A_downgrade_failure_disposes_the_response_once_and_throws_the_exception_unchanged`, `A_followed_https_to_http_hop_under_the_opt_in_is_sent`; `RedirectExceptionTests.The_downgrade_message_names_both_urls_redacted_and_the_opt_in`; `RedirectLogTests.A_permitted_downgrade_…`, `A_rejected_downgrade_…` (Unit) |
| `REDIR-16` | MUST | ✅ | `RedirectChainTests` (key rows: userinfo-free seed key, ordinal with fragment, re-spelled equivalents collide); the matrix `REDIR-16:` rows (A to B to A, userinfo seed, self-redirect, case and default-port re-spelling); `RedirectPolicyTests.A_to_B_to_A_returns_Bs_3xx_open` (Unit) |
| `REDIR-17` | MUST | ✅ | `RedirectOptionsTests.Defaults_…`, `A_negative_MaxRedirects_throws_at_init`, `Zero_is_accepted`; the matrix `REDIR-17:` rows (caps 0, 1, 3); `RedirectPolicyTests.The_cap_at_0_1_and_3_returns_the_last_3xx_without_throwing` (Unit) |
| `REDIR-18` | MUST | ✅ | `RedirectLocationTests.A_non_http_scheme_is_malformed`, `An_empty_host_is_malformed`, `Two_location_values_are_malformed`, `The_resolver_never_throws_for_hostile_values`; the matrix `REDIR-18:` rows; `RedirectLogTests.An_unusable_location_and_a_loop_each_emit_their_warning` (Unit) |
| `REDIR-19` | MUST | ✅ | `RedirectLocationTests.A_missing_header_is_absent`, `An_empty_header_is_absent`, `An_all_whitespace_header_is_absent`; the matrix `REDIR-19:` rows; `RedirectLogTests.An_absent_location_emits_no_event` (Unit) |
| `REDIR-20` | MUST | ✅ (P6b-7: eligibility only) | the matrix `REDIR-20:` rows (true follows a non-allowed method, false stops, the snapshot, cannot defeat loop, downgrade, replay or a malformed `Location`); `RedirectPolicyTests.A_predicate_cannot_reach_the_live_visited_set`, `A_throwing_predicate_disposes_the_response_and_propagates_unchanged`; `RedirectChainTests.Snapshot_copies_the_visited_list`, `Snapshot_carries_the_response_the_count_and_the_target`; AOT smoke (Unit) |
| `REDIR-21` | SHOULD | ✅ | the matrix `REDIR-21:` rows (no `Location`, unusable `Location`, at the cap); `RedirectPolicyTests.The_predicate_is_called_once_per_recognised_3xx_and_never_otherwise` (Unit) |
| `REDIR-22` | MUST | ✅ | `RedirectPolicyTests.The_superseded_response_is_disposed_before_the_next_send` (a), `A_downgrade_failure_…`, `A_replay_failure_…`, `A_throwing_predicate_…`, `A_dispose_failure_on_a_failing_hop_rides_the_primary_exceptions_suppressed_trail` (b), `Every_stop_reason_returns_the_response_undisposed` (c, all five reasons); the sync twins of each; `ReDriveLifecycleTests` (split) (Unit) |
| `REDIR-23` | SHOULD | ✅ | `RedirectPolicyTests.A_thousand_hop_chain_completes_under_a_raised_cap` (async and sync, 1 001 sends) (Unit) |
| `REDIR-24` | MUST | ✅ (pin on stage order, new behavioural test) | `StageOrderTests` (existing); `RedirectPolicyTests.An_auth_stage_probe_runs_once_per_hop`; `RedirectCredentialLeakTests` (a real `AuthorizationPolicy` re-stamps per hop and withholds off the seed origin) (Unit, Security) |
| `REDIR-25` | MUST | 🚫 (design §10 entry 14, topic `async-redirect-pillar`; P6b-22) | the remarks on `RedirectPolicy` and `DexpacePipeline`; `RedirectPolicyTests.The_cap_at_0_1_and_3_returns_the_last_3xx_without_throwing` (the `MaxRedirects = 0` recipe); `redirect.md` "Getting the 3xx yourself". The async standard pipeline follows redirects by design; the documentation clause is met |
| `REDIR-26` | MUST | ✅ | `RedirectOptionsTests.AllowedMethods_is_copied_at_init`, `The_default_set_is_one_shared_frozen_instance`, `A_null_set_or_a_null_element_is_rejected`, `An_empty_set_is_accepted`, `Equality_compares_the_set_by_content_and_the_predicate_by_delegate`; AOT smoke `CheckPhase6bRedirectAsync` (published and run under NativeAOT) (Unit) |
| `REDIR-27` | MAY | ⏳ (declined for v1, P6b-21; `docs/first-release.md`) | `RedirectOptionsTests.RedirectOptions_has_no_LocationHeader_member`. A configurable `Location` header name has no consumer; the header is `Location`. No trigger is named |
| `REDIR-28` | SHOULD | ✅ (P6b-20: the malformed value is redacted, not raw) | `RedirectLogTests` (all fifteen): `Each_event_has_its_id_name_level_and_keys`, `The_hop_number_is_one_based_…`, `A_userinfo_and_token_bearing_target_is_redacted`, `A_malformed_location_carrying_userinfo_is_redacted_by_RedactHeaderValue`, `A_throwing_logger_…`, `A_disabled_logger_builds_nothing`, `The_events_are_not_gated_by_HttpLoggingOptions_Level`, `A_followed_hop_adds_a_dexpace_redirect_hop_span_event_…`, `The_public_constants_have_the_published_values`; `LogVocabularyTests` (Unit) |

Count: **28 rows**. By exit: 26 ✅, 1 🚫, 1 ⏳, 0 N/A. By level: 23 MUST (22 ✅, `REDIR-25` 🚫), 4 SHOULD (all ✅), 1 MAY (⏳).

## Work on other owners' rows

These carry no exit mark in 6b's checklist; the owner's checklist cites the evidence.

| ID (owner) | Work | Evidence |
|---|---|---|
| `BODY-4` (3b) | The redirect branch of the replay gate declines **loudly**; a seekable `FromStream` body follows | the matrix `REDIR-6:` rows; `ReDriveLifecycleTests.A_redirect_over_a_non_replayable_body_throws_and_disposes_the_in_flight_response_once` |
| `BODY-5` (3b) | A body-less `POST` under `AllowedMethods` is followed; idempotency is not consulted | the matrix `REDIR-3: AllowedMethods containing POST follows a body-less POST` |
| `PIPE-15`, `PIPE-16` (4c) | Each hop is a fresh drive with the policy's own request | `RedirectPolicyTests.Each_hop_is_driven_with_the_policys_own_request`; `ReDriveRequestIsolationTests` (unedited) |
| `PIPE-40` (4c) | Intermediate dispose order; "return current" left open; the non-replayable abandon path read as retry's (P6b-17) | `RedirectPolicyTests` lifecycle cases; `ReDriveLifecycleTests` |
| `XCUT-17` (10) | (a) to (d) end to end, including (d)'s rejection and "logging the deviation" | the matrix downgrade rows; `RedirectLogTests`; `RedirectCredentialLeakTests` "a permitted downgrade, then a retry" |
| `AUTH-29`, `XCUT-16` (6c, 10) | The credential-free downgrade hop passes the HTTPS guard | `RedirectCredentialLeakTests` "a permitted downgrade, then a retry" (no `SdkException`); `AuthHttpsGuardTests` (unedited) |
| `HTTP-46` (2a) | Visited keys compare textual forms ordinally; `Uri.Equals` is never used | `RedirectChainTests.Keys_compare_ordinally_and_keep_the_fragment`, `Re_spelled_equivalents_collide` |
| `OBS-20`, `OBS-39` (5b) | Every redirect emission is guarded; the five names and ids and four keys are public constants | `RedirectLogTests.A_throwing_logger_…`, `The_public_constants_…`; `LogVocabularyTests` |
| `TRANSPORT-1` (8b) | Unchanged; `RedirectPolicy` is the single authority | `RedirectCredentialLeakWireTests`; `RedirectWireTests` (unedited) |

## The Security classes

The exact allowed diff of `tests/Dexpace.Sdk.Core.Tests/Security` and `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security` against `main`:
`RedirectCredentialHygieneTests.cs` edited, `RedirectCredentialLeakTests.cs` and `RedirectCredentialLeakWireTests.cs` added, nothing else.

- `RedirectCredentialHygieneTests`, three hunks (P6b-24): the `303` row of `Authorization_is_stripped_on_every_hop_even_same_origin` runs with
  `FollowSeeOther = true` (without it the 303 would not be followed and the assertion would be vacuous); `Stripping_is_on_by_default_regardless_of_the_legacy_switch`
  is replaced by `Stripping_holds_under_every_redirect_option` (every option at its most permissive, the same three headers absent: a strictly larger claim, since the
  switch no longer exists); the header comment names the new matrix and leak classes. The class's private `FollowAsync` helper gained an overload that takes the
  `RedirectOptions` (an edit of the helper, not of an assertion).
- `RedirectCredentialLeakTests` and `RedirectCredentialLeakWireTests` are new and tagged `Security` (P6b-25, open for the lead on the tag).
- `RedirectWireTests`, `AuthHttpsGuardTests`, `ReDriveRequestIsolationTests` and every other `Security` class pass unedited.

## Deviations from the plan

1. **Task 0.1 (pre-flight) results.** Facts 7, 9 and 10 hold on 10.0.401: `HTTPS://EXAMPLE.COM:443/a` and `https://example.com/a` render identically; spaces, `é` and a tab in a
   relative `Location` are accepted and percent-encoded; `IdnHost` renders `bücher.example` and its punycode alike while `Host` does not. Fact 8: all five of `http:///p`,
   `https://`, `http:foo`, `//` and `http://` fail `Uri.TryCreate` outright against an https base, so the empty-host screen is defence in depth. Fact 11 is covered by the AOT
   publish, which ran. A `%41` escape is decoded to `A` in the fragment too (RFC 3986's own equivalence). No ruling was reopened.
2. **`RedirectExceptionTests` lives in `Errors/` but its namespace is `Exceptions`**, the namespace the folder's other files already use; a `Dexpace.Sdk.Core.Tests.Errors`
   namespace would shadow `Errors.*` in an existing test. Its relative-URL case asserts totality, not the `[malformed url]` sentinel (that is `UrlRedactor.Redact(string)`'s; the
   `Uri` overload renders a relative `Uri` as its text).
3. **`The_resolver_never_throws_for_hostile_values` omits `"\0"`**: an inbound header value cannot carry NUL (`Headers.Builder.AddInbound` rejects controls), so the value cannot reach
   the resolver.
4. **`RedirectDecision.Fail` carries the target** (an optional parameter, internal) so the policy can emit `scheme_downgrade_rejected` with it (R6/R8).
5. **`EmissionGuard` was extracted** (`Reportable`, `ReportFailure` and the `log_failed` delegate) from `HttpLogEmitter`, as the plan allowed; `HttpLogEmitterTests` and
   `EmissionGuardTests` pass unchanged except the next item.
6. **One 5b test was narrowed:** `HttpLogEmitterTests.The_resend_count_is_the_transmission_ordinal_across_a_redirect_hop_as_on_the_span` now filters the logger's entries to those
   that carry a resend count, because the pipeline's logger now also receives `http.redirect.hop` (Breaking 9). Not a `Security` class; the assertion is the same.
7. **`RedirectCase` is a property record, not a positional one** (`new RedirectCase(name, status, expect) { … }` with `with`), and the matrix has 130 rows (the Node sections
   in order, plus the added rows). The row type stays internal and the theory is keyed by name (R9).
8. **The leak test's per-hop probe sits at `PerHop` (250), which orders before `Retry` (300)**, not between retry and auth as the plan's prose says; stage order, not insertion
   order, places it. The probe is constructed and in the stack; the assertions are on headers only, as the plan requires.
9. **PR 3 ran against 6a as merged and 6c as it stood on `main` (not yet merged).** The constructors used are `RetryPolicy(TimeProvider?)`, `BasicAuthPolicy(BasicCredential)`
   and `BearerTokenAuthPolicy(TokenCredential, params string[])`. If 6c changes them, adapt construction, never an assertion.
10. **`IDE0130` is not enforced**, so the `Pipeline/Policies/Redirect/` folder keeps the namespace `Dexpace.Sdk.Core.Pipeline.Policies` as designed.
11. **Knowledge corpus.** Nothing found contradicts a harvested entry, so no note was added under `docs/knowledge/notes/`.

## Open rulings taken as designed

The lead had not ruled, so each landed as the design argued it: P6b-2, P6b-3 (the names `FollowSeeOther` and `AllowedMethods`), P6b-4, P6b-7, P6b-8, P6b-10 (two `Location`
values), P6b-16, P6b-19 (the hop at `Information`), P6b-21, P6b-25 (the `Security` tag).
