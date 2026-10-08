# Phase 5a — Configuration: Checklist

The execution-time checklist for sub-phase 5a of roadmap phase 5
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `64-phase-5a-configuration` (issue #64), following the
[design](2026-10-07-phase5a-configuration-design.md) and the [plan](2026-10-07-phase5a-configuration.md).

The scope is 26 rows: `CFG-8`, `CFG-9`, `CFG-12`, `CFG-13`, `CFG-15`-`CFG-36` (18 MUST, 7 SHOULD, 1 MAY): **23 ✅, 1 N/A, 2 🚫, 0 ⏳.**
Every test is `[Trait("Category", "Unit")]` under `tests/Dexpace.Sdk.Core.Tests/` unless another place is named; the AOT checks are
`AotSmoke`. Phase 5a adds no `Security` class and edits none. **Red evidence:** for a new type or a changed signature the red was the compile
error (plan convention 1); the behavioural reds of the options, wait and date tests were seen (`with` on a class, `Sleep` and `HttpDate`
missing, five `RetryPolicy` date cases failing on the BCL parser). The pins (`CFG-19`, `CFG-32`, the `RetryPolicy` timer pin, the per-call
overload pin) were **not** shown able to fail by temporarily breaking the thing they pin.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files (`Configuration/` unless a folder is named): `OptionsImmutabilityTests`, `DexpaceClientOptionsTests`,
`Architecture/OptionsRecordArchitectureTests`, `TimeProviderWaitsTests`, `Internal/LateResultTests`, `Pipeline/OriginalExceptionSurfaceTests`,
`Http/Common/HttpDateTests`, `ProxyOptionsTests`, `ProxyGlobTests`, `ProxyUrlParserTests`, `NoProxyListTests`, `ProxyFromEnvironmentTests`,
`NoImplicitProxyReadTests`, `BuildInfoTests`, `Pipeline/Policies/RetryFactsTests`, `Internal/DeepValueTests`,
`Recovery/IdempotencyKeyStepTests`. Vectors are under `tests/vectors/config/`. The AOT check is `CheckPhase5aConfigurationAsync` in
`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `CFG-8` | MUST | ✅ | `OptionsImmutabilityTests.Records_are_sealed_and_expose_init_only_properties`; `OptionsRecordArchitectureTests` (sealed, no public setter, no mutable collection); `ProxyOptionsTests.NonProxyHosts_is_copied_at_init` |
| `CFG-9` | MUST | ✅ (source-seam clause vacuous in core) | `OptionsImmutabilityTests.With_derives_a_copy_and_leaves_the_source_unchanged` |
| `CFG-12` | SHOULD | N/A | No configuration builder type exists: an object initializer runs on an instance no other code can reach (P5a-25; design §11 item 51). `OptionsRecordArchitectureTests` shows no builder in the namespace |
| `CFG-13` | SHOULD | 🚫 | No process-wide slot; design §10 entry 25, topic `configuration-via-iconfiguration`. `NoImplicitProxyReadTests.No_method_in_the_Core_assembly_calls_FromEnvironment` |
| `CFG-15` | MUST | ✅ | `TimeProviderWaitsTests` (all); the `RS0030` bans in `BannedSymbols.txt` (negative checks run: all five groups fail the build); `RetryPolicyTests.The_sync_and_async_waits_use_the_policys_TimeProvider_and_the_same_delay` |
| `CFG-16` | MUST | ✅ | the `DateTime(Offset).Now/UtcNow/Today` bans; no site in `src/` needed a pragma; `Stopwatch` is monotonic and not banned (P5a-8) |
| `CFG-17` | MUST | ✅ | `TimeProviderWaitsTests.A_negative_delay_is_rejected`, `An_infinite_delay_is_rejected`, `A_zero_wait_returns_at_once_without_arming_a_timer`, `Cancellation_surfaces_with_the_callers_token_and_leaves_it_signalled`, `A_sub_millisecond_delay_is_passed_to_the_timer_unrounded` |
| `CFG-18` | SHOULD | ✅ | `TimeProviderWaitsTests.DelayAsync_*` (completes on advance, zero synchronous, already-cancelled, timer disposed on cancel, chunking) |
| `CFG-19` | SHOULD | ✅ (pin) | `OriginalExceptionSurfaceTests.A_transport_failure_surfaces_as_the_original_exception_through_Send_and_SendAsync` (`Assert.Same`) |
| `CFG-20` | SHOULD | 🚫 | Retired: no interruptible-task future, `Thread.Interrupt` banned (as built); design §10 entry 8, topic `cooperative-cancellation` (P5a-10) |
| `CFG-21` | MUST | ✅ | `LateResultTests.A_result_delivered_after_cancellation_is_disposed_exactly_once`, `A_result_completed_between_the_token_firing_and_the_catch_is_still_disposed`, `A_late_null_result_is_ignored_without_throwing`, `A_faulted_late_task_is_observed_and_raises_no_UnobservedTaskException` |
| `CFG-22` | MUST | ✅ | `ProxyOptionsTests.ToString_masks_credentials`, `ToString_text_is_independent_of_the_credential_values`, `Defaults_are_Http_no_credentials_no_bypass`, `Equality_compares_the_pattern_list_by_content_and_credentials_by_value` |
| `CFG-23` | MUST | ✅ | `ProxyGlobTests.IsBypassed_matches_every_vector` (`proxy-globs.json`), `A_star_dense_pattern_matches_in_linear_time`, `An_oversized_pattern_is_accepted_and_matches`, `Patterns_are_compiled_once_at_init_not_per_call`, `Glob_metacharacters_other_than_star_and_question_are_literals` |
| `CFG-24` | MUST | ✅ (system-property layer substituted by the explicit `ProxyOptions`, §10 entry 25) | `ProxyFromEnvironmentTests.Resolves_every_vector` (`proxy-resolution.json`), `A_rejection_warns_once_naming_the_variable_and_the_rule_and_never_the_value`, `The_resolver_never_throws_for_arbitrary_values`, `A_very_long_NO_PROXY_token_never_throws`, `The_httpoxy_guard_holds_for_a_case_insensitive_lookup`, `A_throwing_logger_does_not_make_FromEnvironment_throw` |
| `CFG-25` | MUST | ✅ | `ProxyUrlParserTests.Parses_every_vector` (port rows), `The_port_is_read_from_the_raw_text_not_Uri_Port`, `ProxyOptionsTests.Port_must_be_in_0_to_65535` |
| `CFG-26` | MUST | ✅ | `NoProxyListTests.Splits_every_vector` (`no-proxy-split.json`), `Each_token_round_trips_through_the_escape` |
| `CFG-27` | MUST | ✅ | `NoProxyListTests.A_lone_star_is_reported_as_bypass_all_not_as_a_token`; the `NO_PROXY=*` vector in `ProxyFromEnvironmentTests`; `ProxyGlobTests.BypassAll_short_circuits_regardless_of_the_list` |
| `CFG-28` | MAY | ✅ (prohibitive half; the convenience half has no slot, `CFG-13`) | the `Environment` bans; `NoImplicitProxyReadTests` (default lookup, IL routing, no caller of `FromEnvironment`, scanner self-test); `ProxyFromEnvironmentTests.FromEnvironment_reads_only_the_variables_it_documents` |
| `CFG-29` | MUST | ✅ | `HttpDateTests.Format_*`; `SetDatePolicyTests` and the `RequestConditions` tests (byte-identical pin) |
| `CFG-30` | MUST | ✅ | `HttpDateTests.TryParse_matches_every_vector` (tolerance rows: month, zone, weekday) |
| `CFG-31` | MUST | ✅ | `HttpDateTests.TryParse_matches_every_vector` (rejection rows), `Parse_error_never_echoes_the_input`, `TryParse_never_throws_for_arbitrary_text` |
| `CFG-32` | MUST | ✅ (pin; §8.2: stronger than required) | `IdempotencyKeyStepTests.The_default_key_strategy_mints_a_version_4_IETF_variant_uuid`, `…yields_no_collision_across_parallel_minting` |
| `CFG-33` | MUST | ✅ | `DeepValueTests` (arrays, nesting, multi-dimensional, null, hashing, random properties) |
| `CFG-34` | MUST | ✅ | `DeepValueTests.NaN_equals_NaN_inside_an_array_and_as_boxed_elements`, `Positive_zero_differs_from_negative_zero_inside_an_array_and_as_boxed_elements`, `Arrays_of_different_runtime_types_are_unequal` |
| `CFG-35` | SHOULD | ✅ (both halves internal; 6a wires `XCUT-5`/`XCUT-6`) | `RetryFactsTests.IsRetryableStatus_is_exactly_408_429_and_5xx_except_501_and_505`, `IsRetryableCause_*`, `The_configured_RetryPolicy_set_is_not_the_classifier` |
| `CFG-36` | SHOULD | ✅ | `BuildInfoTests` (all); `DexpaceClientOptionsTests.The_default_user_agent_is_the_identity_tokens_joined`; the AOT check |

Work on other owners' rows (no exit mark here): `PIPE-17` (`OptionsImmutabilityTests`, `HttpPipelineTests.A_per_call_DexpaceClientOptions_overrides_the_captured_options_for_that_call_only`);
`RETRY-15` (`RetryPolicyTests.A_retry_after_http_date_the_BCL_parser_rejected_is_now_honoured`); `RETRY-26`, `XCUT-3` (`RetryPolicyTests.The_sync_and_async_waits_use_…`);
`ASYNC-5`, `TRANSPORT-9` (`LateResultTests`); `XCUT-5`, `XCUT-6` (`RetryFactsTests`); `XCUT-21` (`IdempotencyKeyStepTests`); `TRANSPORT-15`, `TRANSPORT-30` (`ProxyOptionsTests.ToString_masks_credentials`); `RECOV-33` (`ClientIdentityPolicyTests`).

## Existing assertions changed

| Test | Change |
|---|---|
| `ClientIdentityPolicyTests.The_option_is_read_per_call` | renamed `A_per_call_options_value_wins_over_the_captured_one`; the mutation became `options with { UserAgent = "second/2" }`; the two assertions are unchanged |
| `RetryPolicyTests.Sync_send_honours_cancellation_during_the_wait` | the three property assignments became a `with` expression; no assertion changed |
| `OperationBuildRequestTests.BuildRequest_from_options_applies_the_same_base_rules` | removed; its inputs live in `DexpaceClientOptionsTests.BaseAddress_rejects_what_BuildRequest_used_to_reject` and throw at construction (plan R1) |
| `BlockingWaitTests` | moved to `Configuration/TimeProviderWaitsTests`; the `-5` row of the zero-or-negative theory now throws (plan R2); the other cases are unchanged in value |
| `Seam1ArchitectureTests` | see the rulings below: the blanket ban on `System.Net.Http` and `System.Net.Sockets` became an exact allow-list |
| `ConcurrencyTests`, `SeamParameterTests`, `AsyncErrorModelTests`, `ResiliencePresetTests` | gained `[Collection("Instrumentation")]` (see the rulings below) |
| `DexpaceClientOptionsTests.Logging_defaults_to_HttpLoggingOptions_Default` (5b) | after the rebase onto 5b, `Logging` is `init` (P5b-5): the null assignment became `opts with { Logging = null! }` plus an object-initialiser case; both still throw `ArgumentNullException` |

The `Security` classes are unedited: `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` is empty.

## Where the build differs from the plan

- **Task 0.1 (pre-flight).** The throwaway program was one NativeAOT project in the scratchpad covering facts 7, 10 and 11 and the `UnescapeDataString` and boundary-date extras: `Uri.Port` is `80` for `http://proxy` and `http://proxy:80` alike; the `NonBacktracking` regex with `\A…\z`, `IgnoreCase` and `CultureInvariant` ran under NativeAOT, returned at once on `*a*a*a*a*a*a*a*a*a*b` against sixty `a`, and rejected `localhost\n` and a newline-crossing `.`; `Environment.Version` is `10.0.12` and `FrameworkDescription` is `.NET 10.0.12` (a space); `Uri.UnescapeDataString("pa%ss")` is `pa%ss`; `MinValue` and `MaxValue` format. Fact 8 (`with` recomputes only an assigned member's `init`) is covered by `ProxyOptionsTests.With_on_an_unrelated_member_keeps_the_compiled_patterns_and_with_on_NonProxyHosts_recompiles`; fact 9 by the analyzer (`RS0016` listed exactly the synthesised members). The knowledge CLI queries were not re-run as a separate step; the one tools build was part of the final gate. **The AOT fallback of task 4.2 was not taken** for AOT reasons (`NonBacktracking` works under NativeAOT), but the review's finding F1 led to it anyway: a `NonBacktracking` pattern over 10,000 automaton nodes throws `NotSupportedException` at construction (a `NO_PROXY` token of about 2,000 characters), which breaks `CFG-24`, so `ProxyGlob` is now the hand-written linear matcher, with the same semantics.
- **Tasks 1.x to 5.x.** Each PR's tasks were executed in order; the six "PRs" are six commits on the one branch. Facts and tests are as the plan lists them, with these differences: the vectors for glob and resolution cases carry stable names rather than the plan's table rows verbatim; `TimeProviderWaitsTests` splits the negative and infinite cases; `ProxyType` and the internal `ProxyUrlParser` produce the reason keywords from the `ProxyUrlError` enum names; `ProxyResolution.DefaultLookup` is a static property (the `s_` naming rule rejects an `internal static readonly` field), so the IL test checks a call to its getter rather than an `ldsfld`.
- **Mutation proofs.** Convention 1's "pin is proven able to fail by temporarily breaking the thing" was not carried out for the pins listed above.
- **Knowledge corpus.** No note was added.

## Rulings taken in building

1. **`Seam1ArchitectureTests` (new, P5a-21).** `RetryFacts.IsRetryableCause` names `HttpRequestException` and `SocketException` (design §11 item 23), which that test forbade core to reference (it banned the whole `System.Net.Http` and `System.Net.Sockets` assemblies). The design is the authority, so the blanket ban became an exact allow-list read from the assembly's type references: the only type core may use from `System.Net.Http` is `HttpRequestException` and from `System.Net.Sockets` is `SocketException` (`Core_uses_only_the_exception_types_of_System_Net_Http_and_Sockets`). `HttpClient`, a handler or a socket still fail it, and `BannedSymbols.txt` still bans constructing an `HttpClient`.
2. **The `Instrumentation` xUnit collection (new).** Once 5a's tests changed scheduling, `InstrumentationPolicyTests` intermittently counted activities leaked by concurrently running classes that build a default pipeline. The four such classes joined the existing `Instrumentation` collection; eight consecutive core runs and four solution runs were then clean.
3. **`LateResult` as an async helper (plan R3)**, **`DeepValue` depth bound 128 (R5)**, **bare IPv6 hosts (R6)**, **malformed percent escapes invalid and an empty user name meaning no credentials (R7)**, **trimmed environment values (R8)**, **one `IsValidHost` predicate (R9)**, **`EventId(20, "ProxyConfigurationIgnored")` (R10)** and **vectors recording the .NET expectation (R11)** were taken as the plan states; all stay open for the lead.
4. **CGI guard continues.** When upper-case `HTTP_PROXY` is skipped under `GATEWAY_INTERFACE`, resolution continues to `http_proxy` (the design says "skipped"); with only `HTTP_PROXY` set the result is `null` and one `cgi` warning. After the review's finding F2, `http_proxy` is also skipped with the same `cgi` warning when `HTTP_PROXY` is present and equal to it (what a case-insensitive lookup returns, as on Windows or over `IConfiguration`), Ruby `find_proxy`'s rule; a genuinely distinct lower-case value is still honoured.
5. **An unbracketed IPv6 address in a proxy URL** (`http://2001:db8::1:8080`) is a `host` error rather than guessed.
6. **`NO_PROXY` is read only after a proxy variable parses**, so nothing is read for it when no proxy is configured.

## Deviation ledger as built

P5a-2, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 17, 19, 20, 21, 22, 23, 24, 25 and 28 landed as the design states; P5a-2, 4, 6, 7, 11, 13, 14, 15, 20, 21, 22 and 23 remain open for the lead, and P5a-28 stays open until 5b and 5c confirm their interfaces.

## Correction 2026-10-08 (phase 6a)

`CFG-35`'s "6a wires `XCUT-5`/`XCUT-6`" clause is closed: `HttpResponseException.IsRetryable` is baked from `RetryFacts.IsRetryableStatus` and the `IRetryableError`
capability widens the cause walk (`RetryClassifierTests`). Also, `RetryOptions` and the two timeouts now validate where they are set (5a deferred them to
6a); `configuration.md` carries the matching note. The rows above stand as written at 5a's exit.
