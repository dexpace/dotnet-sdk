# Phase 4b — Recovery Chain: Checklist

The execution-time checklist for sub-phase 4b of roadmap phase 4
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `phase-4b-recovery`, following the
[design](2026-10-05-phase4b-recovery-design.md) and the [plan](2026-10-05-phase4b-recovery.md).

The scope is 34 rows: `RECOV-1`–`RECOV-34`. Every test below is `[Trait("Category", "Unit")]` and lives in `tests/Dexpace.Sdk.Core.Tests/`
unless a project is named; phase 4b adds no `Security` class and edits none. **Red evidence:** for a new type or a changed signature the red was
the compile error (plan convention 1). The plan's seven pull requests were implemented in one pass and the full suite was run green; the
behavioural reds were not each captured separately. The `ValueTask<T>.Result` ban was shown to fire with a throwaway probe (an unmatched documentation
ID is silently ignored); the other pins were not shown able to fail.

The census: 4a 20 + 4b 34 + 4c 40 = 94.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `Recovery/{OutcomeTests,DelegateStepTests,RequestRecoveryChainTests,StepFailureTests,ResponseRecoveryChainTests,RecoveryDispatcherTests,ErrorMappingStepTests,IdempotencyKeyStepTests,ClientIdentityStepTests}.cs`;
`Exceptions/{RecoveryFixtureTests,ExceptionFactsTests,ExceptionTrailTests}.cs`; `Internal/{SyncPathTests,DisposalTests}.cs`;
`Http/Response/ErrorBodyBufferTests.cs`; `Architecture/RecoveryLayerArchitectureTests.cs`; `Pipeline/Policies/{IdempotencyPolicyTests,ClientIdentityPolicyTests}.cs`;
the AOT smoke check is `CheckPhase4bRecoveryAsync` in `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `RECOV-1` | MUST | ✅ | `OutcomeTests` (no non-private constructor; exactly two sealed derived types; accessors never both true; `Match` once; seeded property tests) |
| `RECOV-2` | MUST | ✅ | `RecoveryDispatcherTests.A_throwing_request_step_reaches_a_recording_recovery_step_as_a_failure`, `A_throwing_transport_…`; `ExceptionFactsTests` |
| `RECOV-3` | MUST | ✅ | `RequestRecoveryChainTests` |
| `RECOV-4` | MUST | ✅ | `ResponseRecoveryChainTests.The_response_phase_runs_only_while_the_outcome_is_a_success` |
| `RECOV-5` | MUST | ✅ | `ResponseRecoveryChainTests.Recovery_steps_run_on_every_outcome_in_order`, `Recovery_steps_observe_a_failure_a_response_step_just_produced` |
| `RECOV-6` | MUST | ✅ | `ResponseRecoveryChainTests.The_response_phase_runs_first_then_the_recovery_phase_in_declared_order` |
| `RECOV-7` | MUST | ✅ | `ResponseRecoveryChainTests.A_throwing_response_step_becomes_a_failure_the_rest_are_skipped_and_nothing_propagates` |
| `RECOV-8` | MUST | ✅ | `ResponseRecoveryChainTests` (throwing recovery step, `null` return, no token check, fatal propagates) |
| `RECOV-9` | SHOULD | ✅ | `ResponseRecoveryChainTests.A_returned_failure_reaches_the_next_recovery_step`; `IRecoveryStep` docs. Core ships no recovery step until 6a |
| `RECOV-10` | MUST | ✅ | `RecoveryDispatcherTests` (`Assert.Same` on a constructed, never-thrown exception and on a thrown one) |
| `RECOV-11` | MUST | ✅ | `RecoveryDispatcherTests.Cancellation_survives_the_failure_conversion` (asserted on the token) |
| `RECOV-12` | MUST | ✅ | `StepFailureTests`; `ResponseRecoveryChainTests` (release count 1, trail, failure in hand releases nothing); `DisposalTests` |
| `RECOV-13` | MUST | ✅ | `ResponseRecoveryChainTests.A_recovery_step_returning_a_substitute_success_leaves_the_original_undisposed` (count 0) |
| `RECOV-14` | MUST | ✅ | `RequestRecoveryChainTests`, `ResponseRecoveryChainTests` (copies at construction, null elements, concurrent applies) |
| `RECOV-15` | MUST | ✅ | `ErrorMappingStepTests` (non-error statuses by reference, 4xx/5xx mapped); `EnsureSuccessErrorMappingTests` (`Security`, unedited) |
| `RECOV-16` | MUST | ✅ re-sent-response clause ⏳ 6a | `ErrorBodyBufferTests`; `EnsureSuccessErrorMappingTests` (`Security`, unedited). The re-sent error response is `RETRY-36`'s re-classification, which calls the same buffer (6a: "the recovery-stack engine (the 15 `RECOV` IDs)") |
| `RECOV-17` | MUST | ⏳ 6a | The recovery-stack engine (the 15 `RECOV` IDs), 6a card |
| `RECOV-18` | MUST | ⏳ 6a | As `RECOV-17` |
| `RECOV-19` | MUST | ⏳ 6a | As `RECOV-17`; calls 4b's `ErrorBodyBuffer` and `ErrorMappingStep` |
| `RECOV-20` | MUST | ⏳ 6a | As `RECOV-17` |
| `RECOV-21` | MUST | ⏳ 6a | As `RECOV-17` |
| `RECOV-22` | MUST | ⏳ 6a | As `RECOV-17` |
| `RECOV-23` | MUST | ⏳ 6a | As `RECOV-17` |
| `RECOV-24` | MUST | ⏳ 6a | As `RECOV-17` |
| `RECOV-25` | SHOULD | ⏳ 6a | As `RECOV-17` |
| `RECOV-26` | MUST | ✅ S7 clause ⏳ 6a | `RetryPacingOverflowTests` (`Security`, cited, unedited); the engine's saturation is 6a's |
| `RECOV-27` | MUST | ⏳ 6a | As `RECOV-17` (with `CFG-15`/`CFG-17` from 5a) |
| `RECOV-28` | MUST | ⏳ 6a | As `RECOV-17` |
| `RECOV-29` | MUST | ⏳ 6a | As `RECOV-17` |
| `RECOV-30` | SHOULD | ⏳ 6a | As `RECOV-17` |
| `RECOV-31` | MAY | ⏳ 6a with `RETRY-38` | One feature under two IDs (design §11 item 20); 6a decides both together (P4b-2) |
| `RECOV-32` | MUST | ✅ | `IdempotencyKeyStepTests`; `IdempotencyPolicyTests` |
| `RECOV-33` | MUST | ✅ | `ClientIdentityStepTests`; `ClientIdentityPolicyTests` |
| `RECOV-34` | MUST | ⏳ 6a | As `RECOV-17`; `RetryOptions` validation waits for 5a's records |

Count: 34 rows; 17 ✅ (`RECOV-1`–`RECOV-15`, `RECOV-32`, `RECOV-33`), `RECOV-16` ✅ with a ⏳ clause, `RECOV-26` ✅ for its S7 clause and ⏳ for the
engine, and fifteen ⏳ 6a (`RECOV-17`–`RECOV-25`, `RECOV-27`–`RECOV-31`, `RECOV-34`).

## Existing assertions changed

| Test | Change |
|---|---|
| `DisposalTests.With_a_primary_in_flight_the_report_carries_the_primary_type_and_the_primary_is_untouched` | Rewritten and renamed `…the_failure_lands_on_the_primary_trail_and_nothing_else` (P4b-15); three new cases pin "never a third way" |
| `IdempotencyPolicyTests` | Constructor sites moved to `new IdempotencyPolicy(new IdempotencyKeyStep { Methods = … })` with assertions unchanged; PUT, PATCH, DELETE, strategy-once and step-configuration cases added |
| `ClientIdentityPolicyTests.ProcessAsync_ReplacesExistingUserAgentHeader` | Replaced by the Append expectation and a `Replace` mode test (Breaking 3); blank-option and per-call cases added |

## Security classes

| Class | State |
|---|---|
| `EnsureSuccessErrorMappingTests` (S8) | unedited, green over the `ErrorBodyBuffer` re-home |
| `ReDriveRequestIsolationTests` (S6) | unedited |
| `RetryPacingOverflowTests` (S7) | unedited |

Evidence: `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` is empty.

## Deviation ledger as built

| Ruling | As built |
|---|---|
| P4b-3 | `Outcome` is an abstract class with nested sealed classes; a styleguide 6.3 departure in the SDK overlay and `notes/data-modeling.md`, and `notes/retry-and-resilience.md` supersedes `retry-and-resilience/302d143d`. Open for the lead |
| P4b-6 | Every step contract has `Apply` and `ApplyAsync`; chains and the dispatcher run one `bool async` body; the sync path reads through internal `SyncPath`, with a `ValueTask<T>.Result` entry in `BannedSymbols.txt`. Design §11 item 46 |
| P4b-8 | `when (!ExceptionFacts.IsFatal(ex))` at the dispatcher's request-chain and transport calls and the response chain's two step calls; cancellation is converted; `null` from a step or transport is an `InvalidOperationException` |
| P4b-14 | Snapshot trail; read-only `Data` drops the secondary; `ToString` renders with a cycle guard and an 8-level cap |
| P4b-16 | `ErrorBodyBuffer` (sync and async) and `ErrorMappingStep` are 4b's; `Response.EnsureSuccessAsync` re-homed onto the buffer. Open for the lead |

## Departures from the plan

- The new core test classes for the exception types live in namespace `Dexpace.Sdk.Core.Tests.Exceptions` (the existing `Errors/` folder's namespace), because
  `Dexpace.Sdk.Core.Tests.Errors` would shadow `Dexpace.Sdk.Core.Errors` for `Errors.StreamConsumedException` in `Pagination/PageableTests.cs`.
- `RecoveryDispatcher`'s private core takes a two-field `TransportCall` struct as the plan's task 4.1 describes; its private methods put `bool async` before the token
  (`CA1068`).
- `TestSupport/Recovery/` also holds `ProbeResponseBody`, a response body that supports both read forms and fails its read or release on demand, because
  `DisposalCountingBody` has no synchronous `OpenRead`.

## Correction 2026-10-08 (phase 6a)

`RECOV-17`–`RECOV-25`, `RECOV-27`–`RECOV-31` and `RECOV-34` are no longer ⏳: phase 6a built them, and the evidence is in the
[carried-rows table of the 6a checklist](../../phase6/phase6a/2026-10-08-phase6a-retry-checklist.md#carried-rows), which also covers the re-sent-response
clause of `RECOV-16` and the engine clause of `RECOV-26`. The rows above stand as written at 4b's exit.
