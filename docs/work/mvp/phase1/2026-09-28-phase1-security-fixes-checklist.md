# Phase 1 — Security and Robustness Fixes: Checklist

The execution-time checklist for roadmap phase 1
([phase card](../2026-09-27-dotnet-sdk-v1-roadmap-design.md#phase-1--security-and-robustness-fixes-p0)): one row
per defect S1–S9, in the legend of the roadmap's cross-cutting constraint 3. Written from what was built on branch
`17-phase-1-security-core-fixes` (issue [#17](https://github.com/dexpace/dotnet-sdk/issues/17), S4–S8, core-only
unit tests). S1, S2, S3 and S9 need wire-level assertions against the loopback fixture and are issue
[#18](https://github.com/dexpace/dotnet-sdk/issues/18).

Each fix is the smallest change that closes the defect. A ✅ covers the clause named in the row, never the whole
ID: what the structural owner still has to build is the ⏳ in the same row, and that phase must keep the named
`Security` test green (constraint 5). Every proving test failed against `6b416de` before its fix and passes after
it; the pre-fix failure is quoted.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred. The row names the plan task that will do it (phase, task number and path), or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Defects

The proving tests live in `tests/Dexpace.Sdk.Core.Tests/Security/`, namespace `Dexpace.Sdk.Core.Tests.Security`,
class-level `[Trait("Category", "Security")]`. The later phases named under ⏳ have no plan yet; each ⏳ names the
phase card that owns it, and that phase's plan turns it into a numbered task.

| # | IDs | Level | Status | Clause closed in phase 1 | Proven by (`Security`) | Remaining, and its owner |
|---|---|---|---|---|---|---|
| S1 | `HTTP-17`, `HTTP-18`, `HTTP-26`, `XCUT-18`, `TRANSPORT-12` | MUST | ⏳ | — | — | Header CR/LF injection: the whole row is phase 1 issue #18 (wire-level, loopback fixture); structural owner 2a, 8b |
| S2 | `TRANSPORT-11` | MUST | ⏳ | — | — | Caller-set `Host` and framing headers: phase 1 issue #18; structural owner 8b |
| S3 | `TRANSPORT-1`, `REDIR-7`, `REDIR-8`, `REDIR-9`, `REDIR-12`, `XCUT-17` | MUST | ⏳ | — | — | Transport-side redirects and `RedirectPolicy`'s credential stripping (every hop, seed-origin compare, `Cookie` / `Proxy-Authorization`, userinfo): phase 1 issue #18; structural owner 6b, 8b, 9 |
| S4 | `AUTH-28`, `XCUT-16` | MUST | ✅ ⏳ | On the credential-attaching path, `AuthorizationPolicy.ProcessAsync` rejects a non-`https` URL (case-insensitive) **before** `GetCredentialAsync`, so no token is fetched and no header written, with a non-retryable `SdkException` naming the concrete policy type and the scheme. No loopback exemption (design §11 item 25). Skipped on a cross-origin hop, which attaches no credential (`XCUT-16`'s credential-free carve-out) | `AuthHttpsGuardTests` ([file](../../../../tests/Dexpace.Sdk.Core.Tests/Security/AuthHttpsGuardTests.cs)): `Basic_auth_over_plain_http_is_rejected_before_anything_is_sent` (http, `HTTP`, localhost, 127.0.0.1, [::1]; pre-fix: "Assert.Throws() Failure: No exception was thrown"), `The_guard_runs_before_the_credential_is_resolved`, `A_bearer_token_is_never_fetched_for_a_plain_http_url`, `The_rejection_is_not_retried` (503 then 200, zero sends), `Https_is_stamped_as_before`, `A_credential_free_cross_origin_downgrade_hop_skips_the_guard` (pin; passed pre-fix) | 6c: the cross-origin decision still compares against the first origin the auth policy saw, not the seed request, and there is no redirect marker (`AUTH-29`'s marker strip and forge-proofing; design §6.2, §6.3); a typed exception for the refusal if 6c wants one |
| S5 | `OBS-11`, `OBS-12`, `OBS-13`, `OBS-14`, `OBS-15`, `XCUT-19` | MUST | ✅ ⏳ | `UrlRedactor` is default-deny with allow-list exactly `{api-version}` (decoded, case-insensitive, multi-value atomic; empty list redacts all) and marker `***`; userinfo → `***:***@` (`OBS-11`, `XCUT-19`(a)); fragment `key=value` tokens scrubbed, plain fragment kept (`OBS-13`, `XCUT-19`(b)); scheme, host, port, path, empty `?`, value-less parameters kept verbatim, nothing re-encoded, trailing `&` dropped (`OBS-14`); `[malformed url]` on failure, and new `Redact(string)` returns it for text that is not a well-formed URI reference (`OBS-15`) | `UrlRedactionDefaultDenyTests` ([file](../../../../tests/Dexpace.Sdk.Core.Tests/Security/UrlRedactionDefaultDenyTests.cs)): `A_parsed_uri_redacts_to_the_expected_form` and `Url_text_redacts_to_the_same_form` over 49 pairs, 47 ported from ruby-sdk@5b17395 `redactor_test.rb` and nodejs-sdk@54aeed4 `redaction.test.ts` and 2 derived from design §8.1 (`X-Amz-Signature`, `client_secret`) (pre-fix: "Assert.Equal() Failure: Strings differ" on 43 of 49), `An_empty_allow_list_redacts_every_value`, `A_custom_allow_list_is_matched_case_insensitively`, `A_uri_whose_original_text_is_not_canonical_still_loses_its_userinfo_and_values`, `A_backslash_authority_the_parser_accepts_is_still_masked`, `A_query_the_canonical_form_loses_yields_the_sentinel` (ftp/news/gopher fail closed), `A_query_the_canonical_form_keeps_is_still_redacted`, `Malformed_url_text_yields_the_sentinel` (pre-fix: CS1503, no string overload) | 5b: header redaction `OBS-16`–`OBS-18` and `XCUT-19`(c); `OBS-7` 8 KiB truncation; the `OBS-20` emission guard; computing the redacted URL only when a listener or logger is enabled (design §8.1). 6c: `XCUT-19`(d)'s credential-formatting test (design §6.3). `XCUT-19`(e) holds today (no body logging) and is 5b's to pin |
| S6 | `RETRY-44`, `PIPE-16` | MUST | ✅ ⏳ | `RetryPolicy` and `RedirectPolicy` hold the request they received and restore it onto `PipelineContext.Request` before every re-drive; the redirect's next hop is built from the held request, never the one a downstream policy stamped. `RETRY-44`'s "upstream steps MUST NOT mutate the shared in-flight request between attempts", and `PIPE-16`'s "carry the current in-flight request" for both re-drivers | `ReDriveRequestIsolationTests` ([file](../../../../tests/Dexpace.Sdk.Core.Tests/Security/ReDriveRequestIsolationTests.cs)): `A_retry_attempt_does_not_enter_carrying_the_previous_attempts_credential` (pre-fix: "Assert.Equal() Failure: Collections differ", expected `[null, null]`), `A_retry_after_an_exception_re_sends_the_request_it_held`, `A_redirect_hop_is_not_built_from_the_previous_hops_stamped_request`, `A_downstream_rewrite_does_not_leak_into_the_next_attempt` | 4c: the request-in/response-out policy signature, which removes the public `PipelineContext.Request` setter and makes the rule structural, plus `context.ForAttempt(n)` (design §5.1) |
| S7 | `RETRY-18`, `RECOV-26` | MUST | ✅ ⏳ | Every pacing delta in `RetryPolicy` — `Retry-After` hint or computed back-off — is clamped to 365 days, and a wait above `Task.Delay`'s ceiling (~49.7 days) runs as successive waits of at most 49 days, so a hostile hint can no longer surface `ArgumentOutOfRangeException` | `RetryPacingOverflowTests` ([file](../../../../tests/Dexpace.Sdk.Core.Tests/Security/RetryPacingOverflowTests.cs)): `A_huge_delta_seconds_hint_is_clamped_and_waited_in_bounded_chunks` (60, 365, 1000 days, `int.MaxValue` s; pre-fix: "System.ArgumentOutOfRangeException : The value needs to translate in milliseconds to -1 …"), `A_far_future_http_date_hint_is_clamped_to_the_ceiling`, `A_backoff_configured_beyond_the_timer_limit_is_clamped_and_chunked` | 6a: the hand-written pacing parser (case-insensitive RFC 1123, fractional seconds, `retry-after-ms`, `x-ms-retry-after-ms`, `X-RateLimit-Reset`) clamping in ticks before any `TimeSpan` is built, the shared `RetryWait` with its sync path (design §6.1). 4c / 6a: `RECOV-26` in the recovery-chain retry stack, which does not exist yet |
| S8 | `BODY-31`, `RECOV-15`, `HTTP-52`, `BODY-30` | MUST | ✅ ⏳ | `Response.EnsureSuccessAsync` maps only 400..599; 1xx, 2xx, 3xx (304, an unfollowed redirect) and out-of-range codes return with the body intact (`BODY-31`, `RECOV-15`). The error body is drained inside a `try`/`finally` that disposes the original response even when the drain fails (`HTTP-52`, `BODY-30` close scope), into an internal replayable in-memory body capped at 1 MiB (`HTTP-52`, `BODY-30` replayable copy) | `EnsureSuccessErrorMappingTests` ([file](../../../../tests/Dexpace.Sdk.Core.Tests/Security/EnsureSuccessErrorMappingTests.cs)): `A_non_error_status_is_returned_with_its_body_intact` (100–399, 600, 999; pre-fix: "HttpResponseException : The server returned an error response: NOT_MODIFIED(304)."), `The_buffered_error_body_is_replayable` (pre-fix: "StreamConsumedException : This response body has already been read."), `The_original_response_is_disposed_when_the_error_is_raised`, `The_original_response_is_disposed_when_the_drain_fails` (pre-fix: "Assert.True() Failure"), `An_error_status_throws` and `The_buffered_copy_is_capped_at_one_mebibyte` (pins; passed pre-fix) | 4c: `ErrorMappingPolicy` at `PerCall` (`PIPE-37`), the shared `ErrorBodyBuffer` with a pooled chunk used by the retry stack's re-classification too (`RECOV-16`, `RETRY-36`), `XCUT-8`'s factory rejection, `BODY-30`'s "a response with no body is returned unchanged" on the pipeline step (design §5.1) |
| S9 | `TRANSPORT-22`, `TRANSPORT-27` | MUST, SHOULD | ⏳ | — | — | Malformed inbound `Content-Type` and dispose-on-throw: phase 1 issue #18; structural owner 8b, 3b |

## Exit criteria

- [x] S4–S8 each have a `Security` test that failed before the fix and passes after it.
- [ ] S1, S2, S3, S9 — issue #18.
- [ ] The `SECURITY.md` advisory decision — owed by the phase 1 design, which #18 closes; nothing is published, so no
  advisory is owed.
- [x] Public-API changes are in `src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt`, with `CHANGELOG.md` `[Unreleased]`
  lines (constraint 8).

## Existing assertions changed

Each encoded the defect it now contradicts; none was loosened on behaviour it did not encode.

| Test | Was | Now | Why |
|---|---|---|---|
| `Diagnostics/UrlRedactorTests` (11 tests) | `REDACTED` marker; unlisted values (`page=2`, `q=hello`) kept; fragment dropped; `?flag` dropped; userinfo stripped; the constructor took a deny-list | `***`; unlisted values redacted, `api-version` kept; plain fragment kept; `?flag` kept; userinfo `***:***@`; the constructor takes the allow-list | `OBS-11`–`OBS-14` (S5) |
| `Pipeline/Policies/InstrumentationPolicyTests.ProcessAsync_UrlFull_IsSensitiveParamRedacted`, `…ProcessAsync_LogsStructuredEvent_WithRedactedUrl` | `REDACTED` present, `page=2` kept | `api_key=***`, `page=***` | `OBS-12` (S5) |
| `Http/EnsureSuccessTests.EnsureSuccessAsync_ErrorBodyReadableTwice_AfterBuffering` | read once; the comment recorded the body as single-use | reads twice, both equal | strengthened: `HTTP-52`, `BODY-30` (S8) |

## Styleguide audit groups

| Group | Result |
|---|---|
| Public API surface | `UrlRedactor.DefaultSensitiveParams` → `DefaultQueryAllowList`, the constructor parameter `sensitiveParams` → `queryAllowList`, new `Redact(string)`; recorded in `PublicAPI.Unshipped.txt` under `RS0016`/`RS0017`. The replayable error body is internal |
| Nullability | No new nullable surface |
| Async and `ConfigureAwait` | Every new `await` in `src/` uses `ConfigureAwait(false)` (`CA2007`) |
| Disposal | `EnsureSuccessAsync` disposes the original response in a `finally`; response disposal stays idempotent |
| Analyzers and format | Build clean under `TreatWarningsAsErrors`; one scoped `#pragma warning disable CA1031` in `UrlRedactor.Redact(Uri)` with its why-comment (`OBS-15`'s totality backstop); `RedirectPolicy`'s existing `MA0051` waiver re-counted (101 → 106 lines) |
| Test conventions | Every new test carries `[Trait("Category", "Security")]`; `TestCategoryTests` passes |
| Trim and AOT | No reflection or dynamic code added; AOT smoke consumer publishes and runs |
| Styleguide-vs-design conflicts | None touched |
