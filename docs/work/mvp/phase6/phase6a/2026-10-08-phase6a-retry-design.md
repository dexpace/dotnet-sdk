# Phase 6a — Retry: Design

**Status:** Draft, for review. Written 2026-10-08 against `main` at `3a1db00` (phases 0 to 5c merged), on branch
`70-phase-6-planning` (issue #70). Brainstormed without a human in the loop: every judgement call the brainstorming skill
would have put to the lead is taken here as a numbered ruling (`P6a-n`) with the options and the rationale, and the
judgement calls are marked **open for the lead**. 6b (redirect) and 6c (authentication) are designed in parallel by other
authors; the interfaces this design assumes of them are stated in
[the cross-sub-phase interfaces](#cross-sub-phase-interfaces-assumed-of-6b-and-6c) and in P6a-33. The scope authority is the
roadmap's Phase 6 card and Phase List row 6 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). The format follows
the phase 5 designs (`docs/work/mvp/phase5/phase5a/2026-10-07-phase5a-configuration-design.md` and its 5b and 5c siblings).

**What this document is.** The sub-phase design for 6a: one disposition per requirement row (45 `RETRY` rows), the
dispositions of the fifteen recovery-stack rows phase 4b carried here as ⏳ (plus `RECOV-31` and the 6a clauses of
`RECOV-16` and `RECOV-26`), the public surface 6a adds, changes or removes, the internal types behind it, the breaking
changes, the hand-offs from 2a, 3b, 4b, 4c, 5a, 5b and 5c that 6a takes or declines, the pull-request segmentation, the
`Security` classes that must stay green, and the rulings, which double as the deviation-ledger IDs (roadmap constraint 7).

**What this document is not.** It is not the plan and not the checklist. It does not restate design §6.1's arguments
(roadmap constraint 9); it cites them and records a decision against each row. It does not design the redirect loop (6b),
the auth challenge replay (6c), the transport's own timeout (`RequestOptions.Timeout`, `TRANSPORT-5`, 8b) or the DI
package's guidance on `AddStandardResilienceHandler` (phase 9). It edits no design chapter, roadmap cell or `CLAUDE.md`
line: the corrections it owes are listed as proposals in
[Corrections owed at close-out](#design-roadmap-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2). The card's
entry criterion is "4b, 4c and 5a have exited, and 5c's per-attempt event shape is fixed".

| Predecessor or sibling | Kind | State at `3a1db00` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030` over `BannedSymbols.txt`, `MA0051`, `CA2007`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. The rewritten `RetryPolicy` drops its `MA0051` waiver (P6a-20): the engine is split so no method passes 70 lines. |
| Phase 1, S6 (`ReDriveRequestIsolationTests`) and S7 (`RetryPacingOverflowTests`), and every other `Security` class | **dependency** (constraint 5) | Met. 6a rewrites both subjects; [Keeping the `Security` classes green](#keeping-the-security-classes-green) says why no class is edited (P6a-31). |
| 2a (`Method`, the internal idempotent set, `Request.WithHeader`, `RequestOptions.MaxRetries`, `HttpHeaderSyntax`) | **dependency** | Met. 2a's hand-off "6a grows `RetryFacts` and consumes `RequestOptions.MaxRetries = 0`" is taken (P6a-29). |
| 3b (`RequestBody.IsReplayable` truthful on every variant; seekable stream promotion) | **dependency** | Met. 3b's hand-off "6a/6b/6c own `BODY-4`'s three gates and `BODY-5`" is taken for the retry gate (P6a-1, P6a-10). |
| 4b (`Outcome`, the two chains, `RecoveryDispatcher`, `ErrorBodyBuffer`, `ErrorMapping.ToException`, `ExceptionFacts`, `ExceptionTrail`, `SdkException.Suppressed`) | **dependency** | Met. The recovery-stack engine composes them (P6a-5); 4b's open question for 6a ("a recovery step sees only an `Outcome`") is answered by P6a-5. |
| 4c (`HttpPipelinePolicy`'s request-in/response-out signature, `PipelineContext.ForAttempt`, `CallState.Logger`, `SyncPath`, the `Retry` pillar at 300, `AddStandardResilience`) | **dependency** | Met. 4c's hand-off "6a rewrites `RetryPolicy` on `ProcessCoreAsync` and re-classifies a re-sent error through `ErrorBodyBuffer`" is taken (P6a-20, P6a-5). |
| 5a (`RetryOptions` and `DexpaceClientOptions` as sealed records, `TimeProviderWaits`, `LateResult`, `HttpDate`, `RetryFacts.IsRetryableStatus`/`IsRetryableCause`, the clock and delay bans) | **dependency** | Met. Every 5a hand-off is taken or declined by name: `HttpDate` (P6a-17), `TimeProviderWaits` (P6a-20), `LateResult` declined for `AttemptTimeout` (P6a-23), the classifier wired (P6a-7, P6a-8), the defaults (P6a-12), the removal (P6a-11), numeric validation (P6a-13, P6a-24). |
| 5b (`CallState.Logger`, the emission guard, event ids 140–149 reserved for retry) | **dependency** | Met. One event, id 140 (P6a-28). |
| 5c (`OperationTelemetry.RetrySequenceStarted`, `AttemptFailed`, `RetriesExhausted`; the interim `IsExhausted`) | **dependency** | Met. 5c's hand-off is taken: the three calls stay at the same decisions and `IsExhausted` is replaced by the final predicate (P6a-28). |
| A phase 6 segmentation design | **convenience**, with a stated substitute | Absent. The roadmap's segmentation rule applies (three chapters). The card names each sub-phase's build list, the census below and P6a-1 state 6a's rows, and P6a-2 records the substitute. **Open for the lead.** |
| **6b** (redirect) | **convenience**, both ways | Designed in parallel. `RedirectPolicy` (200) sits above `RetryPolicy` (300) and each hop starts a fresh retry sequence; 6a changes nothing the redirect loop calls. Shared files are listed under [PR segmentation](#pr-segmentation). |
| **6c** (authentication) | **convenience**, both ways | Designed in parallel. Auth (500) sits below retry, so every attempt is re-stamped from the unstamped request (`RETRY-44`, S6); 6a changes nothing auth calls. |

**No dependency edge inverts the roadmap's order.** 6a reads nothing 6b or 6c builds. The end-to-end credential-leak test
the card names ("survives neither a cross-origin hop nor a retry across a hop") is 6b's; 6a's share is that a retry re-drives
the request its policy received, which S6 already pins.

---

## Governing documents, and the phase-start queries

- **Normative.** `docs/product-spec/09-retry-and-resilience.md` §9.1–§9.5 and appendix C rows `RETRY-1`–`RETRY-45`;
  appendix C rows `RECOV-16`–`RECOV-31` and `RECOV-34`, which have **no chapter body** (design §11 item 33): every `RECOV`
  ruling below is argued against appendix C's text, quoted where a decision turns on it. Cited because a 6a type touches
  them: `XCUT-1`–`XCUT-7`, `XCUT-9`, `XCUT-10` (phase 10's rows), `BODY-4`, `BODY-5` (3b's rows, ⏳ 6a), `HTTP-9`, `HTTP-35`
  (2a's), `CFG-35` (5a's), `PIPE-16`, `PIPE-40` (4c's), `OBS-28`, `OBS-29` (5c's), `TRANSPORT-2` (8b's).
- **Design.** §6.1 (retry, entire), §8.3 (the clock, the wait, the prohibition), §5.2 (the recovery-chain primitives, the
  fatal filter, the trail, the cause walk), §5.1 (re-drive), §3.2 (failure mapping); §10 entries 7 (transport failures
  are `SdkException`s), 8 (`cooperative-cancellation`), 12 (the orchestrator's "every throwable"), 13 (the suppressed
  trail); §11 items 1, 10, 12, 19, 20, 22, 23, 24, 32, 33, 46; §12's `RETRY` and `RECOV` rows (35 of 45 and 21 of 34
  cited, with `RETRY-29`, `RETRY-38`, `RETRY-43` and `RECOV-31` on the deferred list this design overturns, P6a-16,
  P6a-20, P6a-26).
- **Prior phase designs.** 4b (`docs/work/mvp/phase4/phase4b/2026-10-05-phase4b-recovery-design.md`: P4b-2, the 6a
  hand-off and its open question), 4c (the re-drive rules, P4c-4), 5a (P5a-4, P5a-7, P5a-9, P5a-12, P5a-21), 5b (the
  event-id partition, P5b-21), 5c (P5c-6, P5c-7, P5c-20).
- **Styleguide.** `csharp/08-error-handling.md` (8.1 BCL exception types, 8.7 the `Result` pattern behind `Outcome`);
  `csharp/09-concurrency.md` 9.1 (no blocking on async outside a documented bridge), 9.4 (cancellation tokens);
  `csharp/10-api-design.md` 10.1 (internal by default), 10.2 (immutable records), 10.5 (token last);
  `csharp/06-types-and-data-modeling.md` 6.1, 6.2 (sealed by default, with the stated exception P6a-20 takes), 6.5. The
  overlay's `I`-prefix (`IRetryableError`) and `Async`-suffix departures stand.
- **Siblings.** `nodejs-sdk@54aeed4` (local `HEAD`): `packages/core/src/retry/{classify,backoff,pacing,engine,settings,
  retry-step,retry-dispatch,attempt-stamp,attempt-trail}{,.test}.ts` — the one-loop-two-adapters shape P6a-3 adopts and
  the test tables the plan ports. `ruby-sdk@5b17395`: `docs/work/mvp/phase6/phase6a/2026-09-09-phase6a-retry-design.md`
  (read for its ledger P6-1–P6-12, especially P6-3's transport-decorator engine, P6-5's budget split and P6-9's
  raise-versus-return split) and, now local, `gems/dexpace-core/test/dexpace/resilience/*_test.rb`. The card's
  `test/support/retry_fixtures.rb` **does not exist** in that tree (only `auth_fixtures.rb` and `challenge_fixtures.rb`);
  the plan ports from the test files directly. `java-sdk` (local): `sdk-core/.../pipeline/step/retry/RetryRecovery.kt`,
  `BackoffCalculator.kt`, `RetryAfterParser.kt` and `http/pipeline/steps/ServerOverrideRetryPredicate.kt`, read for the
  reference's recovery-hook shape and its truthy/falsy tables.

**The knowledge queries could not be run on this host.** `scripts/knowledge` is a .NET program and the host has no .NET SDK
("A compatible .NET SDK was not found", 2026-10-08; the 5a design met the same). The step-1 reading was done on the
corpus files directly:

| Query (intended) | Done instead | Result |
|---|---|---|
| `--origin note --brief` | read `docs/knowledge/notes/*.md` | seven files. `retry-and-resilience.md` carries one note (`Outcome` is an abstract class, P4b-3); it binds P6a-5's use of `Outcome`. The `Async`-suffix note binds the `Process`/`ProcessAsync` pair. None contradicts a ruling here. |
| `--section conflicts --brief` | read `harvested/retry-and-resilience.md` `## Conflicts` | empty. |
| `--prefix-info RETRY` | appendix C rows 263–307 and `harvested/retry-and-resilience.md` | 45 IDs: 39 MUST, 1 MUST NOT (`RETRY-45`), 3 SHOULD (`RETRY-12`, `RETRY-38`, `RETRY-40`), 2 MAY (`RETRY-29`, `RETRY-43`). The harvested design entries all cite §6.1 and agree with it; one names `RetryWait.DelayAsync`, already corrected to `TimeProviderWaits.DelayAsync` by 5a. |
| `--gaps RECOV` | the roadmap's gap table | `RECOV-17`–`RECOV-34` are gap IDs: appendix C is the only statement. All eighteen rows were read there (rows 245–262). |
| `--req` for the cited IDs | appendix C rows for `XCUT-1`–`XCUT-10`, `BODY-4`, `BODY-5`, `HTTP-9`, `HTTP-35`, `CFG-35`, `PIPE-16`, `PIPE-40`, `OBS-28`, `OBS-29` | read; 6a owns none of them. |

The plan re-runs the queries on a host with the pinned SDK and records any difference; a difference that contradicts a
ruling below reopens that ruling.

---

## Scope and the census

**6a owns 45 rows: `RETRY-1`–`RETRY-45`** (39 MUST, 1 MUST NOT, 3 SHOULD, 2 MAY), Phase List row 6's ch.09 range exactly.
6b owns `REDIR-1`–`REDIR-28` and 6c `AUTH-1`–`AUTH-38`; phase 6's exit is 111 rows. **6a also does the work for sixteen
recovery-stack rows whose rows stay in phase 4b's checklist** (roadmap: "whose rows stay in phase 4 as ⏳"): the fifteen
the card names, `RECOV-17`–`RECOV-30` and `RECOV-34`, plus `RECOV-31`, which 4b carried as "⏳ 6a with `RETRY-38`" so 6a
decides both together (P4b-2). Two more 4b rows carry a 6a clause: `RECOV-16`'s re-sent-response half and `RECOV-26`'s
engine half. 6a's checklist lists all eighteen in a "carried rows" table, and the 4b checklist gets one dated correction
pointing at it (P6a-1). Two 3b rows are closed the same way: `BODY-5` (⏳ 6a) and the retry third of `BODY-4` (⏳ 6a, 6b, 6c).

Planned status, one line per ID. ✅ = built and tested in 6a. Section letters point at [Argued positions](#argued-positions).

| ID | Level | Planned | One line |
|---|---|---|---|
| `RETRY-1` | MUST | ✅ | `RetryFacts.IsRetryableStatus` (moved, unchanged): 408, 429, 500–599 but 501 and 505; the default configured set is a subset of it, asserted by test (B, P6a-9). |
| `RETRY-2` | MUST | ✅ | `RetryFacts.IsRetryableFailure` is the one throwable classifier: the I/O family or an advertised capability anywhere in the cause chain, walked by `ExceptionFacts.EnumerateCauses` (cycle-safe, bounded) (B, P6a-8). |
| `RETRY-3` | MUST | ✅ | `HttpResponseException.IsRetryable` is computed once in the constructor from `IsRetryableStatus`; a test checks every code 100–599 against the live classifier (B, P6a-7). |
| `RETRY-4` | MUST | ✅ | `ServiceRequestException` and `ServiceResponseException` report `IsRetryable == true`, sealed; a raw `HttpRequestException`/`SocketException`/`IOException`/`TimeoutException` is in the I/O family (B). |
| `RETRY-5` | MUST | ✅ | `RetryFacts.IsResendable(Request)`: no body and an idempotent method, or a replayable body; the one predicate both stacks call (C). |
| `RETRY-6` | MUST | ✅ | `RetryFacts.IdempotentMethods` (moved, unchanged): GET, HEAD, OPTIONS, PUT, DELETE (C). |
| `RETRY-7` | MUST | ✅ | A bare POST is sent once even on a transport failure that never reached the server; `RetryNonIdempotentWhenReplayable` is removed (C, P6a-11). |
| `RETRY-8` | MUST | ✅ | The engine retries only when the condition and the re-send gate both hold; a matrix test crosses them (A, C). |
| `RETRY-9` | MUST | ✅ | `RetryBackoff.Compute`: `BaseDelay × Multiplier^(attempt−1)`, capped at `MaxDelay`, attempt 1-indexed (E). |
| `RETRY-10` | MUST | ✅ | Symmetric jitter over `[d(1−j/2), d(1+j/2)]`; `j = 0` returns `d`; a sub-tick width returns `d`; a negative sample floors to zero (E, P6a-14). |
| `RETRY-11` | MUST | ✅ | Computed in `double` over ticks and compared against the cap before any cast, so it saturates and never throws; `attempt < 1` is `ArgumentOutOfRangeException` (E, P6a-15). |
| `RETRY-12` | SHOULD | ✅ | Defaults 200 ms, 2.0, 8 s, 0.2 and `MaxRetryAttempts = 2` (three sends) (D, P6a-12). |
| `RETRY-13` | MUST | ✅ | One calculator and one set of constants, reached only through the one engine both stacks drive (A, E, P6a-3). |
| `RETRY-14` | MUST | ✅ | The recovery stack's attempt cap is `MaxRetryAttempts + 1` by construction, not a second default; a test runs both stacks to three sends (D, P6a-29). |
| `RETRY-15` | MUST | ✅ | `RetryPacing`: `Retry-After` integer and fractional seconds, `Retry-After` HTTP-date through 5a's `HttpDate`, `retry-after-ms`, `x-ms-retry-after-ms`, `X-RateLimit-Reset` with `[100%, 120%)` jitter (F). |
| `RETRY-16` | MUST | ✅ | The parser is total and maps malformed input to "no hint", never zero (F, P6a-18). |
| `RETRY-17` | MUST | ✅ | A valid past date or epoch yields `TimeSpan.Zero`, distinct from `null` (F). |
| `RETRY-18` | MUST | ✅ | Every delta, hinted or computed, is clamped to 365 days in ticks before a `TimeSpan` is built; S7's `Security` test is the evidence and stays unedited (F). |
| `RETRY-19` | MUST | ✅ | A hand-written strict grammar `digits [ "." digits ]` screens before any numeric parse; no `double.Parse` anywhere (F). |
| `RETRY-20` | MUST | ✅ | A hint replaces the schedule for that decision and gets no symmetric jitter; the recovery stack still clamps it against the budget (F, H). |
| `RETRY-21` | MUST | ✅ | One fixed precedence for both stacks; the stage stack's "caller-configurable list" is read as `HonorRetryAfter` on or off (F, P6a-17, **open**). |
| `RETRY-22` | MUST | ✅ | The parser cannot throw, and the engine still treats a throwing pacing read as "no hint" so the upstream failure stays the surfaced one (F). |
| `RETRY-23` | MUST | ✅ | The call's token is tested first and a cancelled call is never retried; cancellation during the wait surfaces `OperationCanceledException` with the token still signalled (§10 entry 8) (B, G). |
| `RETRY-24` | MUST | ✅ | A `TaskCanceledException` over a `TimeoutException` with the call's token unsignalled is a retryable timeout; `AttemptTimeout` maps to `ServiceRequestTimeoutException` (B, I). |
| `RETRY-25` | MUST | ✅ | Every catch carries `when (!ExceptionFacts.IsFatal(ex))`; a fatal exception is never classified, retried, logged or given a trail (B). |
| `RETRY-26` | MUST | ✅ | Both waits are `TimeProviderWaits` (`DelayAsync`, `Sleep`): a timer, cancellable, chunked past 49 days (G). |
| `RETRY-27` | MUST | ✅ | `RetryRecovery.TotalTimeout`: abort on cap, on `elapsed ≥ budget`, on `elapsed + delay > budget`; clamp the delay; zero disables (H). |
| `RETRY-28` | MUST | ✅ | The stage policy has no budget: the only budget-carrying type is `RetryRecovery`, and `RetryPolicy` hands the engine `RetryBudget.Unbounded` (A, H, P6a-3). |
| `RETRY-29` | MAY | ✅ | The `ShouldRetry` hook flips classification only and stays under the cap and the re-send gate; the server-header recipe is a test and a documented sample (G, P6a-20, **open**). |
| `RETRY-30` | MUST | ✅ | One `while` loop in one `async` method per call; a 10 000-retry run with zero delays completes with a constant stack depth (G). |
| `RETRY-31` | MUST | ✅ | A zero delay returns a completed task from `DelayAsync` and the loop continues inline (G). |
| `RETRY-32` | MUST | ✅ | The call's token is checked before every send and after every send; a response that arrives after it fired is disposed (G, P6a-25). |
| `RETRY-33` | MUST | ✅ | An `async` method faults its task on every terminal path; a throwing hook, a throwing release and a throwing wait each fault it with any live response disposed first (G). |
| `RETRY-34` | MUST | ✅ | `ExceptionTrail.AddSuppressed` attaches every prior failure to the surfaced exception, skipping the instance itself; discarded on success (J, P6a-22). |
| `RETRY-35` | MUST | ✅ | The pacing hint is read from the live response, then the response is released before the wait; a throw in between disposes it first (G, P6a-21). |
| `RETRY-36` | MUST | ✅ | The recovery stack maps an arriving response whose status is in the configured set through `ErrorBodyBuffer` and `ErrorMapping.ToException` (H). |
| `RETRY-37` | MUST | ✅ | The configured `RetryableStatusCodes` decides alone for an `HttpResponseException` anywhere in the chain; a test retries a configured 501 whose baked flag is `false` (B, P6a-9). |
| `RETRY-38` | SHOULD | ✅ | `RetryOptions.AttemptHeaderName` stamps the 1-based ordinal on a per-attempt copy; disabled returns the same instance (G, P6a-26). |
| `RETRY-39` | MUST | ✅ | Stage precedence: `GetDelayOverride` → pacing (response path only) → `FixedDelay` → backoff (G, P6a-16, P6a-20). |
| `RETRY-40` | SHOULD | ✅ | A throwing or negative delay override logs event 140 and falls back; a throwing `ShouldRetry` aborts with `InvalidOperationException`; fatal exceptions pass both unchanged (G, P6a-20). |
| `RETRY-41` | MUST | ✅ | `RequestOptions.MaxRetries` wins when present (2a validates it non-negative), else `MaxRetryAttempts`; zero means no retries; a negative configured value is rejected at `init`, so the clamp clause is unreachable (D, P6a-13, **open**). |
| `RETRY-42` | MUST | ✅ | The options are immutable records; `RetryPolicy`, `RetryRecovery` and the engine hold only readonly configuration; all per-call state is local (A). |
| `RETRY-43` | MAY | ✅ | `RetryOptions.FixedDelay`: when set, the backoff path is unreachable (D, P6a-16). |
| `RETRY-44` | MUST | ✅ | Every attempt drives `continuation` with `context.ForAttempt(n)` and the request the policy received; S6's `ReDriveRequestIsolationTests` is the evidence, unedited (G). |
| `RETRY-45` | MUST NOT | ✅ | There is no scheduler object; the `TimeProvider` is caller-owned and never disposed (G). |

**The carried rows** (they stay in the 4b checklist; 6a's checklist holds the evidence and the 4b checklist a dated
correction pointing at it):

| ID | Level | Planned | One line |
|---|---|---|---|
| `RECOV-16` (re-sent clause) | MUST | ✅ | The recovery stack's re-classification calls the same `ErrorBodyBuffer` (H). |
| `RECOV-17` | MUST | ✅ | Classification by the configured set for an HTTP failure, by the capability or the I/O family otherwise (B, P6a-8). |
| `RECOV-18` | MUST | ✅ | `RetryFacts.IsResendable` (C, P6a-10). |
| `RECOV-19` | MUST | ✅ | Every send's surviving response in the configured set becomes a `Failure` over a buffered body; 503, 503, 200 reaches the 200 (H, P6a-6). |
| `RECOV-20` | MUST | ✅ | Bounded by the cap (initial send = attempt 1) and the optional total timeout; an overshooting delay surfaces the last failure unchanged (H). |
| `RECOV-21` | MUST | ✅ | The one calculator, then the deadline clamp (E, H). |
| `RECOV-22` | MUST | ✅ | A hint replaces the schedule and is still clamped by the budget (F, H). |
| `RECOV-23` | MUST | ✅ | Total parser; past absolute time → zero (F). |
| `RECOV-24` | MUST | ✅ | The fixed precedence and the strict grammar (F). |
| `RECOV-25` | SHOULD | ✅ | `X-RateLimit-Reset` positive jitter (F, P6a-19). |
| `RECOV-26` (engine clause) | MUST | ✅ | Every duration saturates in ticks and is clamped to 365 days (E, F). |
| `RECOV-27` | MUST | ✅ | The wait is `TimeProviderWaits`; cancellation aborts the loop as a `Failure(OperationCanceledException)` (H; §10 entry 8). |
| `RECOV-28` | MUST | ✅ | `RetryRecovery` and `RecoveryDispatcher` hold no per-call state (H). |
| `RECOV-29` | MUST | ✅ | A pacing failure never masks the upstream failure (F). |
| `RECOV-30` | SHOULD | ✅ | One calculator, one parser, one default schedule, one loop (A, P6a-3). |
| `RECOV-31` | MAY | ✅ | Built with `RETRY-38`; the ordinal semantics of the dispatcher composition are P6a-26's. |
| `RECOV-34` | MUST | ✅ | `RetryOptions` validates every member in its `init` accessor; the status set is copied into a `FrozenSet<int>` (D, P6a-13). |

**Cited, not owned** (each row lives in another phase's checklist; 6a supplies the evidence named):

| IDs (owner) | What 6a supplies |
|---|---|
| `XCUT-1`, `XCUT-2`, `XCUT-3` (10) | `OverallTimeout` surfaces `OperationTimeoutException`, not cancellation; `AttemptTimeout` surfaces a retryable timeout; the wait is promptly cancellable (I, G). |
| `XCUT-4`, `XCUT-5`, `XCUT-6`, `XCUT-7`, `XCUT-9`, `XCUT-10` (10) | The capability, the baked flag, the configured set, the cycle-safe walk, the safety gate applied uniformly (B, C). `XCUT-4`(b)'s I/O-family clause stays §10 entry 7. |
| `BODY-4` retry third, `BODY-5` (3b, ⏳ 6a) | `IsResendable`, with a test over every `RequestBody` variant including the replayable seekable stream (C). |
| `HTTP-9`, `HTTP-35` (2a) | `Method.IsIdempotent` reads the moved set; `MaxRetries = 0` disables retries for the call (C, D). |
| `CFG-35` (5a) | Both classifier halves wired: the baked flag and the capability (B). |
| `OBS-28`, `OBS-29` (5c) | The three calls kept; the final exhausted predicate (K). |
| `PIPE-16`, `PIPE-40` (4c) | Fresh drive per attempt; superseded responses released, the returned one not (G). |
| `TRANSPORT-2` (8b) | No change: one retry layer per call path, the SDK's (§11 item 24). |

---

## Facts the design rests on

The host has no .NET SDK, so nothing was run for this design. Each fact is **verified** (cited, run on 10.0.401 by the
design's authors), **read** (from the tree at `3a1db00`), or **to verify in the plan's first task** (a throwaway program in
the scratchpad, as 4c and 5a did). A to-verify fact that comes out the other way reopens the ruling that cites it.

| # | Fact | Status | Used by |
|---|---|---|---|
| 1 | Neither `HttpRequestException` nor `SocketException` derives from `IOException`; connection refused, DNS failure and peer reset surface as `HttpRequestException` over `SocketException`. | verified, §6.1 | P6a-8 |
| 2 | `HttpClient.Timeout` surfaces `TaskCanceledException` with an inner `TimeoutException`; a cancelled caller token surfaces `TaskCanceledException` with no `TimeoutException` in the chain. | verified, §6.1, §5.2 | P6a-8, P6a-23 |
| 3 | A 300 ms `OverallTimeout` against a slow server surfaces `TaskCanceledException` to a caller who never cancelled. | verified, §6.1 | P6a-24 |
| 4 | A timer rejects a due time above `uint.MaxValue − 1` ms; `TimeProviderWaits` chunks past 49 days and rejects a negative delay. | verified, §6.1; built, 5a | P6a-15, P6a-20 |
| 5 | One seeded `Random` shared across threads returns zeros once corrupted; `Random.Shared` does not. | verified, §6.1 | P6a-27 |
| 6 | `double.TryParse` accepts `NaN`, `Infinity`, `1e3` and reads `1.5` as `15` under `de-DE`; `long.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, …)` rejects a sign, whitespace and an exponent. | verified, §6.1 (the `long` half: to verify) | P6a-18 |
| 7 | `TimeSpan.FromSeconds(1e20)` throws `OverflowException`. | verified, §6.1 | P6a-15 |
| 8 | An unchecked cast of a `double` above `long.MaxValue` to `long` is not a saturation (x64 yields `long.MinValue`). | to verify | P6a-15 (compare before casting) |
| 9 | `new CancellationTokenSource(TimeSpan, TimeProvider)` arms its timer through `TimeProvider.CreateTimer` (so `FakeTimeProvider` fires it) and throws `ArgumentOutOfRangeException` for a delay above `uint.MaxValue − 1` ms. | to verify | P6a-23, P6a-24 |
| 10 | With `HttpCompletionOption.ResponseHeadersRead`, disposing the token source whose token was passed to `HttpClient.SendAsync`, after it returned, neither cancels nor faults a later read of the response stream. | to verify (a `SystemNet.Tests` loopback case) | P6a-23 |
| 11 | `ExceptionTrail.AddSuppressed` on a foreign exception (`OperationCanceledException`) stores the list under a namespaced `Exception.Data` key and skips self-attachment. | verified, 4b | P6a-22 |
| 12 | `ErrorMapping.ToException` rejects a status outside 400–599 (`XCUT-8`); `ErrorBodyBuffer.Capture`/`CaptureAsync` drain at most 1 MiB and dispose the original whether or not the drain completed. | read | P6a-9, P6a-21 |
| 13 | `RecoveryLayerArchitectureTests` forbids any type in `Dexpace.Sdk.Core.Recovery` to reference `Dexpace.Sdk.Core.Pipeline`, and `RetryFacts` lives in `Dexpace.Sdk.Core.Pipeline.Policies` today. | read | P6a-4 |
| 14 | `PipelineContext.AttemptNumber` is 0-based (`ForAttempt(0)` is the first send); `OperationTelemetry.AttemptFailed` reads only `response.Status`, so it is safe after the response is released. | read | P6a-26, P6a-28 |
| 15 | `RetryPacingOverflowTests` builds `RetryOptions { MaxRetryAttempts = 3, BaseDelay = 400 days, MaxDelay = 400 days, HonorRetryAfter = false }` and asserts each due time ≤ the timer limit and the total ≤ 3 × 365 days; its hint cases assert the exact hinted total. | read | P6a-17, P6a-31 |
| 16 | `LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` asserts that no event id lies in 140–169. | read | P6a-28 |
| 17 | A record's synthesised equality compares a collection member by reference (5a fact 5); `RequestOptions` hand-writes `Equals`/`GetHashCode` for its tags. | verified, 5a; read | P6a-9 |
| 18 | Several non-`Security` tests assert send counts under the as-built default of four sends (`RetryPolicyTests.ProcessAsync_Repeated503_ReturnsLastResponseAfterMaxAttempts`, others the plan greps for). | read | Breaking change 1 |
| 19 | C# overload resolution prefers a generic method whose inferred argument is an identity conversion over two interface overloads, and reports `CS0121` when only the two interface overloads exist for an argument implementing both. | to verify | P6a-5 (why no public `Bind` overload pair) |

---

## Argued positions

### A. One engine, two entry points (`RETRY-13`, `RETRY-14`, `RETRY-28`, `RETRY-42`, `RECOV-28`, `RECOV-30`; P6a-3, P6a-4)

The specification describes two stacks that must not drift: "ONE status classifier, ONE backoff calculator, ONE
pacing-header parser, and ONE set of tuning constants". Design §6.1 keeps both stacks, because the port keeps both layers,
and invokes neither unification sanction (§11 item 19). 6a goes one step further than the facts the stacks must share: it
shares **the loop**, as Node does (`retry/engine.ts`'s `runWithRetry` under `retryStep` and `dispatchWithRetry`). One
internal engine performs every send, runs both gates, resolves the delay, waits, keeps the trail and decides the terminal
outcome; each stack is an adapter that supplies what differs:

| | Stage stack: `RetryPolicy` | Recovery stack: `RetryRecovery` through `RecoveryDispatcher` |
|---|---|---|
| One send | `continuation.RunAsync(stamped, context.ForAttempt(n))`, under the attempt timeout | the transport, then the response steps of the response chain |
| Budget | `RetryBudget.Unbounded` (`RETRY-28`) | `TotalTimeout`, zero disabling (`RETRY-27`) |
| A response in the configured set | kept live; released only when discarded; returned live when the loop stops (`PIPE-40`) | mapped to `Failure(HttpResponseException)` on arrival (`RECOV-19`); surfaced as that failure when the loop stops (`RECOV-20`) |
| Hooks | `ShouldRetry`, `GetDelayOverride` (`RETRY-39`, `RETRY-40`) | none |
| Pacing on/off | `HonorRetryAfter` | always on (`RECOV-22`) |
| Telemetry | the three `OperationTelemetry` calls, event 140 | none (no `PipelineContext`) |

**Sharing the loop is single-sourcing, not unification.** `RECOV-30` asks a port with more than one retry entry point to
"single-source the retry math likewise"; both entry points, their distinct contracts and the budget's absence from the stage
stack survive, so `RETRY-28`'s "a port that unifies the stacks" clause is still not invoked, and §11 item 19 stands. What the
shared loop buys is that `RETRY-13`/`RETRY-14`/`RECOV-30` become structural: there is one place a schedule, a gate or a
trail can be written.

**Where it lives (P6a-4).** The engine cannot live in `Pipeline.Policies` (the recovery layer may not reference the pipeline
layer, fact 13) and should not live in `Recovery` (the pipeline's `RetryPolicy` would then depend on recovery types for its
loop, which is allowed but inverts nothing useful). A new internal namespace, `Dexpace.Sdk.Core.Resilience` (Ruby's
`Dexpace::Resilience`), holds:

```text
src/Dexpace.Sdk.Core/Resilience/
  RetryFacts.cs      (moved from Pipeline/Policies; + DefaultRetryableStatusCodes, IsRetryableFailure, IsResendable)
  RetryBackoff.cs    (the one calculator)
  RetryPacing.cs     (the one pacing parser)
  RetryBudget.cs     (readonly struct: Unbounded, or a total timeout over TimeProvider timestamps)
  RetryEngine.cs     (the loop: one bool-async core, split under MA0051)
  RetryAttempt.cs    (internal per-send record the adapters hand the engine; not public)
```

All of it is `internal`. The engine works over 4b's `Outcome` (it references `Recovery`, never `Pipeline`), and its two
adapter callbacks are internal delegates, so `Resilience` → `Pipeline` is an edge no type draws.
`RecoveryLayerArchitectureTests` gains a second fact, "no `Resilience` type references a `Pipeline` type". `Method.cs` and
`RetryFactsTests` follow the move.

**The engine's per-call state is local (`RETRY-42`, `RECOV-28`).** Attempt count, start timestamp, trail and the live
response are locals of one `RunAsync` frame; `RetryPolicy`, `RetryRecovery`, `RecoveryDispatcher` and the engine hold only
readonly configuration (`TimeProvider`, the random source, the budget). A shared instance invoked concurrently, or invoked
again after an exhausted call, starts from a clean budget; a test runs 64 concurrent calls through one policy and one
dispatcher and checks each call's send count.

**The loop, in order** (both adapters; differences in brackets):

```text
start = timeProvider.GetTimestamp()                         [budget origin]
for sends = 1, 2, …:
    token.ThrowIfCancellationRequested()                     RETRY-32 (trail attached, J)
    stamped = Stamp(request, sends)                          RETRY-38 / RECOV-31; same instance when disabled
    outcome = adapter.Send(stamped, sends)                   non-fatal throws become Failure (RETRY-25 filter)
    [recovery: Success whose status ∈ set → buffer, map → Failure]                     RECOV-19
    if outcome is Success and token is cancelled: dispose it, throw OCE              RETRY-32
    retryable = condition(outcome) && IsResendable(request) && sends ≤ maxRetries    RETRY-8, RETRY-14
                [stage: condition = ShouldRetry hook ?? classifier]                  RETRY-29, RETRY-40
    if !retryable: return Terminal(outcome, trail)                                   RETRY-34, RETRY-7
    delay = ResolveDelay(outcome, sends)                     override → pacing → fixed → backoff; 365-day clamp
    [recovery: if elapsed ≥ budget or elapsed + delay > budget: return Terminal; delay = min(delay, remaining)]
    observer.AttemptFailed(outcome, delay)                   OBS-28 (stage only)
    trail.Add(Release(outcome))                              RETRY-35, P6a-21
    wait(delay)                                              RETRY-26, RETRY-31; OCE → trail attached, rethrown
```

"`sends ≤ maxRetries`" is the cap: with `maxRetries = MaxRetryAttempts` (or the per-call override), the loop makes at most
`maxRetries + 1` sends. The terminal step returns a `Success` live (stage) or as-is (recovery) and surfaces a `Failure`'s
exception with the trail attached; the exhausted record is written only when the loop stopped on the cap or the budget
with the attempt otherwise retryable (K).

### B. Classification (`RETRY-1`–`RETRY-4`, `RETRY-23`–`RETRY-25`, `RETRY-37`, `RECOV-17`; `XCUT-4`–`XCUT-7`, `XCUT-9`; P6a-7, P6a-8, P6a-9)

**The capability (P6a-7).** As design §6.1 states, `XCUT-6`'s capability is an interface:

```csharp
namespace Dexpace.Sdk.Core.Errors;

public interface IRetryableError
{
    bool IsRetryable { get; }
}
```

`SdkException` implements it with `public virtual bool IsRetryable => false`. `ServiceRequestException` and
`ServiceResponseException` override it as `public sealed override bool IsRetryable => true` (`XCUT-4`: a transport error
"MUST report itself as always-retryable"; a response that could not be read is a failure "before a complete response was
received", `RETRY-4`). `HttpResponseException` overrides it with a value computed once in its constructor,
`RetryFacts.IsRetryableStatus(response.Status.Code)`, held in a readonly field (`XCUT-5`, `RETRY-3`). The new
`OperationTimeoutException` (I) keeps the base `false`. A third-party transport's exception implements `IRetryableError`
without touching core.

**The classifier (P6a-8).** One internal function decides the condition for a failure:

```csharp
internal static bool IsRetryableFailure(Exception failure, IReadOnlySet<int> retryableStatuses, CancellationToken callToken)
```

1. **Cancellation first.** If `callToken.IsCancellationRequested`, the answer is `false`, whatever the exception's type
   (`XCUT-1`, `RETRY-23`; §5.2's "test the call's token, never the type"). `ThreadInterruptedException` is not in the I/O
   family, so it is never retryable either (§6.1).
2. **Walk** `ExceptionFacts.EnumerateCauses(failure)` (breadth-first, reference-identity visited set, depth cap 64:
   `XCUT-9`, `RETRY-2`'s cycle clause). At each node, in this order:
   - an `HttpResponseException` **decides the walk**: the answer is `retryableStatuses.Contains(status)`, and the walk
     stops (`RETRY-37`, `RECOV-17`, `XCUT-7`: the configured set is authoritative and the baked flag is not consulted,
     even when the response-bearing exception is wrapped);
   - a node implementing `IRetryableError` with `IsRetryable == true` makes the answer `true`;
   - a node in the I/O family — `IOException`, `SocketException`, `TimeoutException`, `HttpRequestException` with no
     `StatusCode` (§10 entry 7, §11 item 23) — makes the answer `true`.
3. Otherwise `false`.

**The capability widens; it does not veto.** An `SdkException` whose `IsRetryable` is `false` does not stop the walk: a
`DeserializationException` over an `IOException` (a connection reset mid-body) is retryable because `RETRY-2` makes "any
throwable that … has anywhere in its cause chain an I/O error" retryable, and `RETRY-2` is a MUST stated in the chapter.
`RECOV-17`'s "for a non-HTTP failure, retry iff it advertises retryability via the capability flag" is satisfied on the
reading that a BCL I/O exception, which cannot implement the interface, advertises retryability by its type — the reading
§6.1 already takes ("classification runs off the capability first") and §10 entry 7 records. **Open for the lead**: the
alternative (a `false` capability is authoritative and stops the walk) is Ruby's P6-4 and would make every SDK wrapper
non-retryable unless it opts in.

**`CFG-35`'s two halves are wired.** `IsRetryableStatus` stays the status half; `IsRetryableCause` (5a) becomes the
capability-plus-family walk without the `HttpResponseException` branch and without the token (it answers "is this
throwable transient", the classifier's question without a configuration), and `IsRetryableFailure` is the retry engine's
call. `IsRetryableCause`'s existing tests keep passing; a test adds a custom `IRetryableError` to each.

**Fatal exceptions (`RETRY-25`).** No engine `catch` lacks the `when (!ExceptionFacts.IsFatal(ex))` filter, so an
`OutOfMemoryException` passes every frame unwound-free, is never classified, never added to a trail and never logged. A
test throws one from the transport and asserts the same instance, no second send, and an empty `ExceptionTrail`.

**The configured set (P6a-9).** `RetryOptions.RetryableStatusCodes` is the set both stacks consult, typed
`IReadOnlySet<int>`, copied at `init` into a `FrozenSet<int>`, defaulting to `RetryFacts.DefaultRetryableStatusCodes`
= `XCUT-7`'s `{408, 429, 500, 502, 503, 504}`. Every member must lie in **400–599**, or `init` throws
`ArgumentOutOfRangeException`: a status outside that band cannot be mapped to an `HttpResponseException` (`XCUT-8`,
fact 12), so the recovery stack's `RECOV-19` mapping and the stage stack's trail entry are total over the set. `RETRY-1`'s
"the stage stack's default predicate derive[s] from" the classifier is read as "the default set is a subset of the
classifier", which a test asserts member by member; `XCUT-5`'s own note says the configured default "is a subset of this
classifier". **Open for the lead** on the member type (5a's rule A.3 says collections are `IReadOnlyList<T>`; a status set
has no order and no meaningful duplicates, so a set is the honest type) and on the 400–599 range.

### C. The re-send gate (`RETRY-5`–`RETRY-8`, `RECOV-18`; `XCUT-10`, `BODY-4`, `BODY-5`; P6a-10, P6a-11)

```csharp
internal static bool IsResendable(Request request) =>
    request.Body is null ? IdempotentMethods.Contains(request.Method) : request.Body.IsReplayable;
```

It is the only predicate either stack calls, it is applied to every candidate whatever the failure (`XCUT-10`: "MUST NOT
special-case transport errors"), and it reads the request the policy received (stage) or the prepared request (recovery),
which is the request a retry would actually re-send. A POST carrying an empty but present body is "a body that is
replayable", so it is re-sendable; a POST with no body is not. A test crosses five methods × {no body, bytes, string,
form, file, seekable stream with length, single-use stream} × {transport failure, retryable status} against the expected
send count (`RETRY-8`'s matrix), which is also `BODY-4`'s retry third and `BODY-5`'s evidence.

**`RetryNonIdempotentWhenReplayable` is removed (P6a-11).** Design §6.1 shows it wrong both ways; the rule as written
retries a POST with a replayable body by default (Breaking 2).

**No configurable method set (P6a-10).** `RECOV-18` and `XCUT-10` speak of "the configured idempotent-method set" and
`RECOV-34` of copying "retryable methods"; `HTTP-9` says the set "MUST be the single source both the configurable retry
allow-list and the inherent replay-safety gate derive from" and is "an internal constant". 6a ships no method allow-list:
the "configured" set is `RetryFacts.IdempotentMethods`, and `RECOV-34`'s method-copy clause has no subject. A widening
allow-list would re-open exactly the bare-POST retry `XCUT-10`(a) forbids; a narrowing one has no requirement and no
requester. **Open for the lead.**

### D. `RetryOptions`: members, defaults, validation (`RETRY-12`, `RETRY-14`, `RETRY-41`, `RETRY-43`, `RECOV-34`; P6a-12, P6a-13, P6a-14, P6a-16)

The record after 6a (5a's rule A.1–A.5 holds; the members marked **new** are additive, the one marked **removed** is
Breaking 2):

| Member | Type | Default | `init` validation |
|---|---|---|---|
| `MaxRetryAttempts` | `int` | **2** (was 3) | `≥ 0`, else `ArgumentOutOfRangeException` |
| `BaseDelay` | `TimeSpan` | 200 ms | `≥ 0` and `≤ MaxRepresentable` |
| `Multiplier` **new** | `double` | 2.0 | finite and `≥ 1.0` (rejects `NaN`, `∞`) |
| `MaxDelay` | `TimeSpan` | **8 s** (was 30 s) | `≥ 0` and `≤ MaxRepresentable` |
| `Jitter` **new** | `double` | 0.2 | in `[0, 1]` (rejects `NaN`) |
| `FixedDelay` **new** | `TimeSpan?` | `null` | `null`, or `≥ 0` and `≤ MaxRepresentable` |
| `HonorRetryAfter` | `bool` | `true` | — (now covers every pacing header, P6a-17) |
| `RetryableStatusCodes` **new** | `IReadOnlySet<int>` | `{408, 429, 500, 502, 503, 504}` | non-null; each in 400–599; copied to `FrozenSet<int>` |
| `AttemptHeaderName` **new** | `string?` | `null` | `null`, or a valid token (`HttpHeaderSyntax.IsValidName`) |
| `RetryNonIdempotentWhenReplayable` **removed** | — | — | — |

`MaxRepresentable` is `RECOV-34`'s "representable in nanoseconds (~292-year ceiling)": `TimeSpan.FromTicks(long.MaxValue / 100)`,
checked explicitly because `TimeSpan.MaxValue` is ~29 000 years (§6.1). The record hand-writes `Equals(RetryOptions?)` and
`GetHashCode` so the status set compares by content (fact 17, the `RequestOptions` precedent) and its `PrintMembers` renders
the set in ascending order.

**The names (P6a-12).** `MaxRetryAttempts` keeps Polly's meaning, retries not sends (§6.1, P14); `BaseDelay` and
`MaxDelay` keep their as-built names; `Multiplier` and `Jitter` are the specification's terms. `RETRY-12`'s values land as
the defaults: 200 ms, 2.0, 8 s, 0.2 and two retries, three sends.

**Validation is total at `init` (P6a-13).** `RECOV-34` is a MUST: "durations must be non-negative and representable …
the delay multiplier must be >= 1.0; maximum attempts must be >= 1 (1 disables retries); and the jitter fraction must
lie in [0.0, 1.0]". `MaxRetryAttempts ≥ 0` is that rule in Polly's vocabulary. This collides with `RETRY-41`'s "a
negative configured value MUST be clamped to the default (and the clamp logged)" and agrees with `HTTP-35`'s reasoning
that a negative count "would be silently reinterpreted as 'use default'". 6a takes `RECOV-34` and `HTTP-35`'s line, as
Node did: a negative value cannot be configured, so `RETRY-41`'s clamp clause is unreachable, and the row is ✅ on its
override and zero clauses with the clamp clause read as vacuous (a proposed §11 item). No cross-property rule is checked
(`BaseDelay > MaxDelay` simply caps at `MaxDelay`); phase 9's `IValidateOptions` may add warnings (P5a-4). **Open for
the lead.**

**Resolution (P6a-14).** `RETRY-10` ("sub-nanosecond range") and `RETRY-15` ("honored to nanosecond resolution") are read
at `TimeSpan`'s resolution, one tick (100 ns): a jitter width below one tick returns the base delay, and a fractional
`Retry-After` is honoured to seven decimal places and truncated beyond. A proposed §11 item records it.

**The per-call override (`RETRY-41`, `HTTP-35`; P6a-29).** The effective retry count is
`context.RequestOptions.MaxRetries ?? options.MaxRetryAttempts` (stage) and `requestOptions.MaxRetries ??
options.MaxRetryAttempts` (recovery); `RequestOptions` already rejects a negative value at `init` (2a). Zero is "no
retries": one send, and never "exhausted" (K). The recovery stack's attempt cap is that count plus one, derived, never a
second default (`RETRY-14`, Ruby's P6-6).

**`FixedDelay` (P6a-16, `RETRY-43`).** When set, the backoff path is unreachable: no growth, no jitter, no `MaxDelay` cap
(Node's reading of "zeroing the base and cap so only the fixed delay applies": the cap belongs to the schedule this mode
replaces). It still sits below the pacing hint in `RETRY-39`'s precedence and still meets the 365-day clamp. §12 listed
`RETRY-43` as deferred; it costs one member and four lines, and it gives `RETRY-39`'s third step a subject.

### E. Backoff (`RETRY-9`–`RETRY-11`, `RETRY-13`, `RECOV-21`, `RECOV-26`; P6a-15)

```csharp
internal static TimeSpan Compute(int attempt, RetryOptions options, Func<double> random)
```

1. `attempt < 1` → `ArgumentOutOfRangeException` (`RETRY-11`; a programmer error, unreachable from the engine).
2. `FixedDelay is { } fixed` → `fixed` (then step 6).
3. `BaseDelay == TimeSpan.Zero` → `TimeSpan.Zero` (before the power: `0 × ∞` is `NaN`, Node's audit #78).
4. `growth = BaseDelay.Ticks × Math.Pow(Multiplier, attempt − 1)` in `double`; `d = growth ≥ MaxDelay.Ticks || double.IsInfinity(growth) ? MaxDelay.Ticks : (long)growth`.
   The comparison happens in `double` **before** any cast (fact 8), so the result saturates at the cap and never throws
   (`RETRY-11`, `RECOV-26`).
5. Jitter: `j == 0` → `d`; `width = d × j`; `width < 1` tick → `d` (P6a-14); else `sample = d − width/2 + u × width` with
   `u = random()`, and `sample < 0` → `0`; the sample is again compared against `long.MaxValue` in `double` before the
   cast. A hostile random source outside `[0, 1)` therefore yields a value in `[0, cap × 1.5]`, never a throw.
6. Clamp to 365 days (`RECOV-26`'s "server-supplied hints **and computed deltas**"); S7's third case is the evidence.

The default random source is `Random.Shared.NextDouble` (fact 5); P6a-27 says how a test injects one.

### F. Pacing (`RETRY-15`–`RETRY-22`, `RECOV-22`–`RECOV-25`, `RECOV-29`; P6a-17, P6a-18, P6a-19)

```csharp
internal static TimeSpan? TryGetHint(Headers headers, DateTimeOffset now, Func<double> random)
```

Total by construction (no `throw`, no `Parse`, no culture), first usable value wins, fixed precedence (`RETRY-21`,
`RECOV-24`):

1. `Retry-After` as delta-seconds: the first value, trimmed of SP/HTAB, must match `digits [ "." digits ]` (hand-written
   span scan; `RETRY-19` rejects `+5`, ` 5`, `30d`, `0x1p4`, `1e3`, `NaN`, `Infinity`, `.5`, `5.`). The integer part is
   accumulated with saturation, the fraction to seven digits (P6a-14), giving ticks.
2. `Retry-After` as an HTTP-date: `HttpDate.TryParse` (5a: case-insensitive, informational weekday, one- or two-digit
   day, `GMT`/`UTC`/`+0000`/`+00:00`; RFC 850 and asctime rejected, §11 item 27). Delta = date − `now`; negative → zero
   (`RETRY-17`).
3. `retry-after-ms`, then `x-ms-retry-after-ms`: `digits` only, milliseconds.
4. `X-RateLimit-Reset`: `digits` only, Unix epoch seconds; delta = epoch − `now`; negative → zero; a positive delta is
   multiplied by `1 + 0.2 × u` with `u = random()` (`RECOV-25`, P6a-19: `[100%, 120%)`; a zero delta stays zero).
5. None usable → `null` (`RETRY-16`).

Every result is clamped to 365 days in ticks before a `TimeSpan` exists (`RETRY-18`). `now` is
`TimeProvider.GetUtcNow()` (a wall-clock instant, because a date is one; `CFG-16` governs elapsed time, which the budget
measures with `GetTimestamp`).

**Magnitude saturates; malformation is "no hint" (P6a-18).** `RETRY-16` says "malformed, negative, or out-of-range values
MUST map to 'no hint'" and `RETRY-18` says computed deltas are clamped to 365 days. The two are reconciled by reading
"out-of-range" as a value outside its grammar or its field ranges (a day of 32, an epoch with a sign), never as a large
well-formed numeral: a 30-digit `Retry-After` is a server asking for a very long wait, and treating it as "no hint" would
retry it after 200 ms — the opposite of what `RETRY-16` exists to prevent. An all-digit numeral saturates and clamps to 365
days; `RetryPacingOverflowTests`' `int.MaxValue` case is the evidence. Negative values cannot pass the grammar.

**A hint replaces the schedule (`RETRY-20`, `RECOV-22`).** No symmetric jitter on a hint; `X-RateLimit-Reset`'s positive
jitter is inside the parser. The recovery stack clamps a hint against the remaining budget exactly as it clamps backoff.

**One precedence for both stacks (P6a-17).** `RETRY-21`'s last sentence describes the reference: "the stage stack walks a
caller-configurable ordered header list". 6a gives both stacks the recovery stack's fixed precedence and keeps the stage
stack's configurability as the existing `HonorRetryAfter` switch (now "honour the server's pacing headers", all four);
the recovery stack ignores the switch (`RECOV-22` is a MUST with no opt-out). The normative core — "a defined precedence
and … the first parseable value" — holds on both stacks. Keeping `HonorRetryAfter` also keeps S7's third case compiling
unedited (fact 15). Options: a `PacingHeaders` list on `RetryOptions` (a member one stack ignores, a closed vocabulary of
four names to validate, and a mechanical edit to a `Security` class); Node's fixed parser with no switch at all (rejected:
drops an as-built opt-out with no requirement asking for its removal). **Open for the lead**; a proposed §11 item records
the reading.

**A pacing failure never masks the failure (`RETRY-22`, `RECOV-29`).** The parser cannot throw; the engine still calls it
inside `try … catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` and treats a throw as `null`, so a future edit that
introduces a throw degrades to backoff, never to a replaced exception.

### G. The stage stack: `RetryPolicy` (`RETRY-26`, `RETRY-29`–`RETRY-35`, `RETRY-38`–`RETRY-40`, `RETRY-44`, `RETRY-45`; P6a-20, P6a-21, P6a-25, P6a-26)

```csharp
namespace Dexpace.Sdk.Core.Pipeline.Policies;

public class RetryPolicy : HttpPipelinePolicy
{
    public RetryPolicy(TimeProvider? timeProvider = null);

    public sealed override PipelineStage Stage => PipelineStage.Retry;
    public sealed override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation);
    public sealed override Response Process(Request request, PipelineContext context, PipelineRunner continuation);

    protected virtual bool? ShouldRetry(RetryAttemptContext attempt) => null;
    protected virtual TimeSpan? GetDelayOverride(RetryAttemptContext attempt) => null;
}

public readonly record struct RetryAttemptContext(
    int Attempt, Request Request, Response? Response, Exception? Failure, PipelineContext Context);
```

**Hooks are protected virtuals on an unsealed policy (P6a-20).** `RETRY-39` and `RETRY-40` describe a caller
delay-override provider and a should-retry predicate. The .NET idiom for a customisable pipeline policy is Azure.Core's
`RetryPolicy` (`protected virtual bool ShouldRetry(HttpMessage)`, `CalculateNextDelay`), not delegates on an options
record (delegates are not bindable by phase 9 and break the record's value equality). The class is unsealed with every
override `sealed`, so a subclass can change the two decisions and nothing else: not the stage (the builder reads `Stage`
once, P4c-8, and a sealed override cannot be moved out of its pillar, `PIPE-36`), not the loop. `RetryAttemptContext.Attempt`
is the 1-based retry ordinal about to be scheduled (`RETRY-9`'s attempt); `Response` is the live response, which the hook
must not dispose; `Failure` is the exception otherwise. **Open for the lead** (styleguide 6.2's "sealed by default"
admits a type designed for inheritance; this is one).

- **`ShouldRetry`** returns `null` to defer to the classifier, `true` or `false` to force the condition. It flips the
  condition only: the re-send gate and the cap still apply (`RETRY-29`'s rule, and `RETRY-8`). It is never asked about a
  cancelled call or a fatal exception. A throw aborts the call: the live response is disposed, and
  `InvalidOperationException("RetryPolicy.ShouldRetry threw …", thrown)` is surfaced with the attempt's failure (if any)
  attached as suppressed (`RETRY-40`'s "well-typed illegal-state error"; fatal exceptions pass the filter unchanged).
- **`GetDelayOverride`** returns `null` to fall through, or the delay to use. A throw, or a negative value, is non-fatal:
  event 140 is logged through `CallState.Logger` under 5b's guard and the loop falls back to the next source (`RETRY-40`).
- **`RETRY-29` is met by `ShouldRetry`.** A subclass that reads a response header and returns `true`/`false` is exactly
  the "opt-in server-driven override [that] flip[s] only classification and remain[s] subject to the attempt cap and the
  re-send-safety gate". The plan ships it as a test and a sample in the user page using Java's `X-Should-Retry` tables
  (`true`/`1`/`yes`/`retry`, `false`/`0`/`no`/`stop`), not as a built-in header option. **Open for the lead** (the
  alternative is a `RetryOptions.RetryOverrideHeaderName` member read by both stacks).

**One send (the adapter's callback).** Per attempt `n` (0-based, fact 14): if `context.Options.AttemptTimeout` is set,
a `CancellationTokenSource(attemptTimeout, timeProvider)` is linked with the call's token and the drive runs on
`context.ForAttempt(n).WithCancellationToken(linked)` (I); otherwise on `context.ForAttempt(n)`. The request is the one the
policy received, stamped (P6a-26). A non-fatal throw becomes a `Failure`; an `OperationCanceledException` raised because
the attempt source fired while the call's token did not becomes `Failure(new ServiceRequestTimeoutException(…, ex))`.

**Delay resolution, stage (`RETRY-39`).** `GetDelayOverride` → pacing hint (response path, `HonorRetryAfter`) →
`FixedDelay` → `RetryBackoff.Compute(attempt)`. The exception path skips pacing (no headers). Each result is clamped to
365 days.

**Releasing a discarded response (P6a-21, `RETRY-35`).** In order: (1) the hook and the pacing hint read the live
response; (2) `OperationTelemetry.AttemptFailed` reads its status; (3) the response is released: when its status is
400–599 it is drained through `ErrorBodyBuffer` (≤ 1 MiB, the original disposed) and mapped with `ErrorMapping.ToException`
into an `HttpResponseException` that becomes its trail entry; otherwise (a status forced by `ShouldRetry`) it is disposed
and leaves no entry (`XCUT-8` forbids a "successful exception"). Draining a small error body rather than disposing it
unread also lets an HTTP/1.1 connection return to the pool. A drain failure is non-fatal: the original is already
disposed (fact 12), the drain's exception becomes the trail entry, and the loop continues. If anything in (1)–(3) throws,
the response is disposed before the exception propagates (`RETRY-33`, `RETRY-35`'s second clause).

**Stopping (`PIPE-40`, `RETRY-34`).** A `Success` that is not retried (non-retryable, not re-sendable, or the cap spent)
is returned live and unread, and the trail is discarded: the stage did not fail. A `Failure` that is not retried is
rethrown with `ExceptionDispatchInfo` (the same instance, its stack intact) with the trail attached (J).

**Cancellation (`RETRY-23`, `RETRY-26`, `RETRY-32`, P6a-25).** Before each send the call's token is checked; a cancelled
call throws `OperationCanceledException` with the trail attached and never sends again. During the wait,
`TimeProviderWaits.DelayAsync`/`Sleep` surface `OperationCanceledException` carrying the token, which stays signalled
(the "restore the flag" clause is free, §8.3); the trail is attached and it is rethrown. A response that arrives after
the call's token fired is disposed and `OperationCanceledException` is thrown (design §6.1's reading of `RETRY-32`: the
caller abandoned the call, and a response nobody will read is a leak). Only the call's token counts, never the attempt
token, so an attempt timeout is a retryable failure and not a cancellation.

**The async loop (`RETRY-30`, `RETRY-31`, `RETRY-33`).** One `while` loop inside one `async ValueTask<Response>` method
per call: N retries reuse one state machine and build no continuation chain. `DelayAsync(TimeSpan.Zero)` returns a
completed task, so a zero delay continues inline. An `async` method faults its task on every path. The synchronous path is
the same core with `async: false` (4c's `ProcessCoreAsync` pattern, §11 item 12), waiting with `TimeProviderWaits.Sleep`,
so the two cannot drift.

**`RETRY-44` and `RETRY-45`.** Every attempt drives a fresh `ForAttempt` copy with the request held at entry; nothing
upstream can mutate it (S6, unedited). There is no scheduler: the `TimeProvider` belongs to the caller and the SDK never
disposes it (`TimeProvider` is not `IDisposable`; a test passes a disposable subclass and asserts it is never disposed).

**`MA0051`.** The waiver on today's `ProcessCoreAsync` is retired: the policy's method is a dozen lines over the engine, and
the engine is split into send, decide, resolve-delay, release and terminal helpers, none over 70 lines.

### H. The recovery stack: `RetryRecovery` (`RETRY-27`, `RETRY-36`, `RETRY-37`, `RECOV-16`–`RECOV-20`, `RECOV-27`, `RECOV-28`, `RECOV-31`; P6a-5, P6a-6)

```csharp
namespace Dexpace.Sdk.Core.Recovery;

public sealed class RetryRecovery
{
    public RetryRecovery(RetryOptions options, TimeSpan totalTimeout = default, TimeProvider? timeProvider = null);
    public RetryOptions Options { get; }
    public TimeSpan TotalTimeout { get; }
}

public sealed class RecoveryDispatcher
{
    public RecoveryDispatcher(RequestRecoveryChain requestChain, ResponseRecoveryChain responseChain);            // unchanged
    public RecoveryDispatcher(RequestRecoveryChain requestChain, ResponseRecoveryChain responseChain, RetryRecovery retry);
    public RetryRecovery? Retry { get; }
    // Dispatch / DispatchAsync unchanged in signature
}
```

**Where the engine sits (P6a-5).** 4b handed 6a an open question: "a recovery step sees only an `Outcome`, so a retrying
step needs the prepared request and the transport; 6a decides whether it constructs the step per call (capturing both) or
asks for a dispatcher entry point that hands them over". The reference installs `RetryRecovery` as a recovery step built
per logical request around the client and the request (Java), Ruby decorates the transport (P6-3), and Node composes the
orchestrator's halves (`dispatchWithRetry`). 6a takes the dispatcher entry point, in Node's layering:

1. The request chain runs **once**, above the loop: one idempotency key and one client-identity line per logical call
   (`RECOV-32`, `RECOV-33`), never one per attempt.
2. **Each send** is the transport, then the **response steps** of the response chain over that send's outcome (a throwing
   step becomes a `Failure` with the response released, `RECOV-7`, `RECOV-12`).
3. The engine classifies, gates, waits and re-sends, as in A, with `RetryRecovery.TotalTimeout` as the budget.
4. The **recovery steps** run **once**, on the engine's terminal outcome, and the dispatcher unwraps as today (`RECOV-10`).

`ResponseRecoveryChain` gains two internal entry points, its response phase alone and its recovery phase alone, over the
same private bodies; the public `Apply`/`ApplyAsync` still run both. `RetryRecovery` holds only configuration
(`RECOV-28`), and the dispatcher stays stateless with the transport a per-call argument (P4b-25), so no transport is bound
at construction and the sync/async overload problem of a public `Bind(IHttpClient)`/`Bind(IAsyncHttpClient)` pair (fact 19)
never arises. Options considered: a public per-call `IRecoveryStep` (Java's shape; it must capture the prepared request, which
only the dispatcher has, and it runs re-sent responses past the response steps); a transport decorator (Ruby's P6-3; clean,
but a decorated transport runs the response steps once on the final response and cannot see a response step's mapping
per attempt); a public static helper beside the dispatcher (two orchestrators doing one job). **Open for the lead**:
4b's hand-off said "as an `IRecoveryStep`", and 4b explicitly left the shape to 6a.

**Every send is classified (P6a-6, `RECOV-19`, `RETRY-36`).** After a send's response steps, a `Success` whose status is in
`RetryableStatusCodes` is drained through `ErrorBodyBuffer` (the one capture, 1 MiB, `RECOV-16`) and mapped with
`ErrorMapping.ToException` into `Failure(HttpResponseException)`; every other `Success` — including a non-retryable error
status — passes on as a `Success`. With `ErrorMappingStep` among the response steps, a 4xx/5xx already arrives as a
`Failure` and nothing is mapped twice. The reference re-classifies only **re-sent** responses (`RECOV-19`'s "each RE-SENT
attempt's response") because in its shape the initial send passed the response steps and the retry hook saw only their
outcome, so an initial 503 with no error-mapping step is a `Success` the hook passes through (`RECOV-17`: "a Success is
always a pass-through"). In 6a's shape the engine performs the initial send itself, so it applies the same rule to every
send: an initial 503 is retried whether or not an `ErrorMappingStep` is installed, and a re-sent response sees the same
response steps the initial one did. `RECOV-17`'s pass-through holds for the outcome the engine classifies (a `Success`
that survives the mapping is never retried). A proposed §11 item records the reading. **Open for the lead.**

**The budget (`RETRY-27`, `RECOV-20`, `RECOV-21`, `RECOV-22`).** `TotalTimeout` defaults to `TimeSpan.Zero`, "unbounded";
validated `≥ 0` and `≤ MaxRepresentable`. Elapsed time is `timeProvider.GetElapsedTime(start)` from a timestamp taken before
the first send (`CFG-16`). Before scheduling each retry: stop if the cap is spent; stop if `elapsed ≥ budget`; compute the
delay (hint or backoff); stop if `elapsed + delay > budget`, surfacing the last failure **unchanged** (with the trail);
otherwise wait `min(delay, budget − elapsed)` (the belt-and-braces clamp, which narrows only across the clock read between
the two checks, as Node documents). It lives on `RetryRecovery` and not on `RetryOptions`, so the stage policy cannot read
it (`RETRY-28` by construction, Ruby's P6-5 reasoning). It is not `DexpaceClientOptions.OverallTimeout`, which is the
pipeline's whole-call deadline enforced by cancellation outside both loops (§6.1).

**Terminal outcomes (`RECOV-20`).** When retries are exhausted or disallowed the terminal `Failure`'s exception is surfaced
with the trail attached; a `Success` is returned. That is Ruby's P6-9 split, and in this shape it falls out of the mapping:
an exhausted 503 is a `Failure` because it was mapped on arrival.

**Cancellation (`RECOV-27`).** A cancelled wait yields `Failure(OperationCanceledException)` with the trail attached, which
aborts the loop; the recovery steps see it, and the dispatcher rethrows it (`RECOV-11`: the token stays signalled). §10 entry
8 covers "surface an interrupted-I/O failure".

### I. Timeouts (`XCUT-1`, `XCUT-2`; P6a-23, P6a-24)

**`OverallTimeout` is a non-retryable timeout `SdkException` (P6a-24).** A new public type:

```csharp
namespace Dexpace.Sdk.Core.Errors;

public sealed class OperationTimeoutException : SdkException   // IsRetryable: false (inherited)
{
    public OperationTimeoutException();
    public OperationTimeoutException(string message);
    public OperationTimeoutException(string message, Exception? innerException);
}
```

`OperationPolicy` arms its deadline as `new CancellationTokenSource(overallTimeout, timeProvider)` linked with the caller's
token (fact 9), so a fake clock drives it; it gains `OperationPolicy(TimeProvider? timeProvider = null)`, and
`AddStandardResilience` passes its `timeProvider` through. It catches `OperationCanceledException` `when` its own deadline
source fired and the caller's token did not, and throws `OperationTimeoutException("The operation exceeded its overall
timeout of {timeout}.", ex)`; it copies the inner exception's `ExceptionTrail` onto the new exception, so `RETRY-34`'s trail
is on what the caller catches. A caller-cancelled call still surfaces `OperationCanceledException` (`XCUT-1`). Because the
policy is outermost, nothing retries the timeout; its `IsRetryable == false` says so to any classifier that walks it. The
operation span sees it as a failure through `OperationTelemetry.Fail` (5c), unchanged.

**`AttemptTimeout` is wired, cooperatively (P6a-23, `XCUT-2`).** `RetryPolicy` links a per-attempt source to the call's
token (G) and maps its firing to `ServiceRequestTimeoutException`, which is retryable (`XCUT-4`). It bounds the attempt's
drive until it returns a response (headers), not the caller's later body read (fact 10); the source is disposed when the
drive returns. 5a offered `LateResult` "if `AttemptTimeout` abandons a task"; 6a does not abandon: an abandoned attempt would
still be writing a replayable body while the next attempt writes the same body, a concurrency hazard `BODY-3`'s consume
latch exists to prevent. A transport that ignores the token is not bounded by `AttemptTimeout`; `TRANSPORT-9`'s abandoning
form is 8b's. An `AttemptTimeout` with no `RetryPolicy` in the pipeline is not enforced; its XML docs say so.

**Validation (P6a-24, closing 5a's P5a-4 deferral for these two).** `OverallTimeout` and `AttemptTimeout` accept `null`
("none") or a value in `(0, 49 days]`; zero, negative (including `Timeout.InfiniteTimeSpan`) and anything a timer cannot arm
throw `ArgumentOutOfRangeException` in `init` (fact 9). As built, a non-positive `OverallTimeout` silently meant "none"; that
is Breaking 8, the same reasoning as `HTTP-35`'s for `RequestOptions.Timeout` (a zero carrying "no timeout" in one place and
"expire now" in another). **Open for the lead.**

### J. The suppressed trail (`RETRY-34`; P6a-22)

Every failed attempt's exception (a thrown failure, or the `HttpResponseException` a discarded response was mapped into,
G) is appended to a per-call list. When the loop surfaces an exception — a non-retried failure, an exhausted failure, a
cancellation, a `ShouldRetry` abort, an overshot budget — each prior entry is attached with `ExceptionTrail.AddSuppressed(surfaced,
prior)`, oldest first, skipping any entry that is the surfaced instance (`ReferenceEquals`; the helper also guards it), so a
transport that reuses one exception instance cannot attach it to itself. On an `SdkException` the trail is
`SdkException.Suppressed` and renders in `ToString()`; on a foreign exception it is in `Exception.Data`, read through
`ExceptionTrail.GetSuppressed` (§10 entry 13). On success, and when the stage stack returns a live response, the list is
dropped. A fatal exception gets nothing (`RETRY-25`). Both stacks use the one engine, so §11 item 12's sync/async and
two-stack drift cannot occur.

### K. Observability (`OBS-28`, `OBS-29`, `RETRY-40`; P6a-28)

The stage adapter keeps 5c's three calls at the same decisions (5c's hand-off): `RetrySequenceStarted` on entry,
`AttemptFailed(context, response, failure, delay)` once per scheduled retry (before the response is released, fact 14),
and `RetriesExhausted(context, sends)` with the final predicate replacing the interim `IsExhausted`: **the loop stopped
because the cap was spent while the condition and the re-send gate both held and the effective retry count was above
zero**. A zero budget is "retries off", never "exhausted" (5c's rule); a non-retryable condition or a non-re-sendable
request is never exhausted. 5c's `RetryTraceEventsTests` keep their names; the one that pins "returns and throws exactly what
it did" is re-derived against the new semantics.

One log event: `DexpaceLogEvents.RetryDelayOverrideFailed = "dexpace.retry.delay_override_failed"`, id 140, `Warning`, with
the hook's exception, written through `CallState.Logger` under 5b's emission guard. Ids 141–149 stay reserved for retry.
`LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` changes its "nothing in 140–169" assertion to "140 is the only
id in 140–149, nothing in 150–169" (fact 16; 6b and 6c each edit their own block, see the shared-file list).

The recovery stack emits no span event and no log event: it has no `PipelineContext`, and `OBS-29`'s lifecycle belongs to
the pipeline's operation span. A consumer driving `RecoveryDispatcher` directly gets the trail on the surfaced exception.

---

## The public surface (`PublicAPI.Unshipped.txt`)

Core only; `Dexpace.Sdk.Http.SystemNet` and `Dexpace.Sdk.Serialization.SystemTextJson` are untouched. The plan takes the
exact lines from the analyzer's code fix. Abbreviations: `C.` = `Dexpace.Sdk.Core.Configuration.`, `E.` =
`Dexpace.Sdk.Core.Errors.`, `P.` = `Dexpace.Sdk.Core.Pipeline.Policies.`, `R.` = `Dexpace.Sdk.Core.Recovery.`.

**Removed:**

```text
C.RetryOptions.RetryNonIdempotentWhenReplayable.get -> bool
C.RetryOptions.RetryNonIdempotentWhenReplayable.init -> void
P.RetryPolicy (sealed class line; re-added unsealed below)
override P.RetryPolicy.{Stage, Process, ProcessAsync} (re-added as sealed override)
P.OperationPolicy.OperationPolicy() -> void (replaced by the optional-parameter constructor)
```

**Added:**

```text
E.IRetryableError
E.IRetryableError.IsRetryable.get -> bool
virtual E.SdkException.IsRetryable.get -> bool
sealed override E.ServiceRequestException.IsRetryable.get -> bool
sealed override E.ServiceResponseException.IsRetryable.get -> bool
sealed override E.HttpResponseException.IsRetryable.get -> bool
E.OperationTimeoutException (sealed; the three standard constructors)

C.RetryOptions.{Multiplier, Jitter}.get -> double / .init -> void
C.RetryOptions.FixedDelay.get -> System.TimeSpan? / .init -> void
C.RetryOptions.RetryableStatusCodes.get -> System.Collections.Generic.IReadOnlySet<int>! / .init -> void
C.RetryOptions.AttemptHeaderName.get -> string? / .init -> void

P.RetryPolicy (unsealed) ; P.RetryPolicy.RetryPolicy(System.TimeProvider? timeProvider = null) -> void (unchanged)
sealed override P.RetryPolicy.Stage.get / Process(...) / ProcessAsync(...)
virtual P.RetryPolicy.ShouldRetry(P.RetryAttemptContext attempt) -> bool?
virtual P.RetryPolicy.GetDelayOverride(P.RetryAttemptContext attempt) -> System.TimeSpan?
P.RetryAttemptContext (readonly record struct: Attempt, Request, Response, Failure, Context; the synthesised members)
P.OperationPolicy.OperationPolicy(System.TimeProvider? timeProvider = null) -> void

R.RetryRecovery ; R.RetryRecovery.RetryRecovery(C.RetryOptions! options, System.TimeSpan totalTimeout = default, System.TimeProvider? timeProvider = null) -> void
R.RetryRecovery.Options.get -> C.RetryOptions! ; R.RetryRecovery.TotalTimeout.get -> System.TimeSpan
R.RecoveryDispatcher.RecoveryDispatcher(R.RequestRecoveryChain! requestChain, R.ResponseRecoveryChain! responseChain, R.RetryRecovery! retry) -> void
R.RecoveryDispatcher.Retry.get -> R.RetryRecovery?

const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.RetryDelayOverrideFailed = "dexpace.retry.delay_override_failed" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.RetryDelayOverrideFailedId = 140 -> int
```

**Unchanged in signature, changed in behaviour:** `C.RetryOptions.Equals(C.RetryOptions?)` and `GetHashCode()` (now
hand-written, comparing the status set by content; the API lines already exist); `C.RetryOptions.MaxRetryAttempts`/`MaxDelay` defaults;
`C.DexpaceClientOptions.OverallTimeout`/`AttemptTimeout` `init` validation; `P.RetryPolicy.ProcessAsync`/`Process`;
`P.OperationPolicy`'s timeout exception; `R.RecoveryDispatcher.Dispatch`/`DispatchAsync` with a retry.

**Internal** (not in the API file): `Dexpace.Sdk.Core.Resilience.{RetryFacts, RetryBackoff, RetryPacing, RetryBudget,
RetryEngine, RetryAttempt}`; `ResponseRecoveryChain`'s two phase entry points; `RetryPolicy`'s and `RetryRecovery`'s
internal constructors taking a `Func<double>` (P6a-27).

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes and gets a `CHANGELOG.md` `[Unreleased]` line in the PR
that makes it (constraint 8).

| # | Change | Kind | PR |
|---|---|---|---|
| 1 | `RetryOptions` defaults: `MaxRetryAttempts` 2 (was 3: three sends, was four), `MaxDelay` 8 s (was 30 s); the backoff is `BaseDelay × 2^(n−1)` with ±10% symmetric jitter (was full jitter over `[0, min(BaseDelay × 2^n, MaxDelay)]`) | behaviour | 2 |
| 2 | `RetryOptions.RetryNonIdempotentWhenReplayable` is removed: a POST or PATCH with a replayable body is now retried by default, and a POST with no body is never retried | source + behaviour | 2 |
| 3 | `RetryOptions` validates at `init`: a negative duration or count, a duration above ~292 years, a multiplier below 1 or not finite, a jitter outside `[0, 1]`, a status outside 400–599, an invalid attempt-header name throw | behaviour | 2 |
| 4 | Retry classification: an exception is retried when it, or any cause, is in the I/O family or reports `IRetryableError.IsRetryable` (was: only `ServiceRequestException` and `ServiceResponseException`), so a third-party transport's raw `HttpRequestException` or `IOException` is now retried; the configured status set decides for a wrapped `HttpResponseException` | behaviour | 1, 3 |
| 5 | Pacing: fractional `Retry-After`, `retry-after-ms`, `x-ms-retry-after-ms` and `X-RateLimit-Reset` are honoured, and `HonorRetryAfter` governs all four | behaviour | 3 |
| 6 | A retried error response's body is drained (at most 1 MiB) before it is disposed (was: disposed unread), and the surfaced exception carries every earlier attempt's failure in `Suppressed`/`ExceptionTrail` | behaviour | 3 |
| 7 | `RetryPolicy` is no longer `sealed` (its overrides are); `RetryPolicyTests`-style code that relied on `sealed` is unaffected, but reflection over `IsSealed` changes | source (binary-compatible) | 3 |
| 8 | `OverallTimeout` surfaces `OperationTimeoutException` (was `OperationCanceledException`/`TaskCanceledException`); `OverallTimeout` and `AttemptTimeout` reject zero, negative and above-49-day values at `init` (a non-positive `OverallTimeout` used to mean "none") | behaviour | 4 |
| 9 | `AttemptTimeout` is enforced by `RetryPolicy` (was: read by nothing) and surfaces as a retried `ServiceRequestTimeoutException` | behaviour | 3 |
| 10 | A response that arrives after the caller's token fired is disposed and the call throws `OperationCanceledException` | behaviour | 3 |
| 11 | `OperationPolicy`'s parameterless constructor becomes `OperationPolicy(TimeProvider? timeProvider = null)` | binary (source-compatible) | 4 |

Additive, with no **Breaking** marker: `IRetryableError`, `SdkException.IsRetryable` and its overrides,
`OperationTimeoutException`, the new `RetryOptions` members, `RetryAttemptContext`, the two hooks, `RetryRecovery`, the new
`RecoveryDispatcher` constructor and `Retry` property, event 140.

---

## Keeping the `Security` classes green

Constraint 5: green, or moved without weakening. **No `Security` class is edited by 6a** (P6a-31).

| Class | Edit | Why |
|---|---|---|
| `RetryPacingOverflowTests` (S7) | None | Its three cases hold under 6a (fact 15). The hint cases: the `Retry-After` values parse as delta-seconds (`5184000` → 60 days, `31536000` → 365, `86400000` and `2147483647` saturate to 365) and receive no jitter (`RETRY-20`), so the exact totals hold; the HTTP-date case clamps to 365 days. The backoff case keeps `HonorRetryAfter = false` (kept, P6a-17) and `MaxRetryAttempts = 3`; 400 days ± 10% is 360–440 days, clamped to ≤ 365, so each due time is within the timer limit (chunked) and the total ≤ 3 × 365. `RecordingTimeProvider` sees no extra timer: no `AttemptTimeout` is set, so no attempt source is armed. Run first after PR 2 and PR 3. |
| `ReDriveRequestIsolationTests` (S6) | None | It retries a GET on 503 and on `ServiceRequestException` with `BaseDelay`/`MaxDelay` of 1 ms; the engine drives each attempt with the request held at entry. Two sends fit the new default of two retries. |
| `AuthHttpsGuardTests` | None | The guard's plain `SdkException` reports `IsRetryable == false` and has no I/O cause, so it is still not retried (`Assert.Equal(0, transport.CallCount)` holds). Its redirect case sends two requests through no retry. |
| `RedirectCredentialHygieneTests`, `RedirectWireTests` | None (6b's) | 6a changes nothing the redirect policy calls. |
| `EnsureSuccessErrorMappingTests` | None | `ErrorBodyBuffer` and `ErrorMapping` gain callers, not changes; `HttpResponseException`'s constructor signature is unchanged (it computes one more field). |
| `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, `UrlRedactionDefaultDenyTests`; SystemNet `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `MalformedContentTypeWireTests` | None | Subjects untouched. (`FramingHeaderDropWireTests`' known port-number flake is not 6a's.) |

No `Security` class is added: 6a fixes no phase-1 defect beyond S7's, which is already pinned.

---

## Migration plan from the as-built code

| Change | Sites | Rewrite |
|---|---|---|
| `RetryFacts` to `Resilience` | `Pipeline/Policies/RetryFacts.cs` → `Resilience/RetryFacts.cs`; `Http/Common/Method.cs`; `RetryFactsTests` | `git mv` and namespace; additions per B, C |
| The capability | `Errors/SdkException.cs`, `Errors/TransportExceptions.cs`, new `Errors/IRetryableError.cs`, new `Errors/OperationTimeoutException.cs` | per B, I |
| `RetryOptions` | `Configuration/RetryOptions.cs` | members, defaults, `init` validation, hand-written equality (D) |
| The engine | new `Resilience/{RetryBackoff,RetryPacing,RetryBudget,RetryEngine,RetryAttempt}.cs` | per A, E, F |
| `RetryPolicy` | `Pipeline/Policies/RetryPolicy.cs`; new `Pipeline/Policies/RetryAttemptContext.cs` | rewritten over the engine; `MA0051` waiver removed (G) |
| `OperationPolicy`, timeouts | `Pipeline/Policies/OperationPolicy.cs`, `Pipeline/PipelineBuilder.cs` (`AddStandardResilience`), `Configuration/DexpaceClientOptions.cs` | per I |
| The recovery stack | new `Recovery/RetryRecovery.cs`; `Recovery/RecoveryDispatcher.cs`; `Recovery/ResponseRecoveryChain.cs` | per H |
| Log event | `Diagnostics/DexpaceLogEvents.cs`; `LogVocabularyTests` | per K |
| Architecture | `RecoveryLayerArchitectureTests` | the `Resilience` → no `Pipeline` fact |
| Tests that assumed four sends or `RetryNonIdempotentWhenReplayable` | `RetryPolicyTests` (three tests named for the option), `ProcessAsync_Repeated503_*`, others by grep (fact 18) | re-derived to the new semantics; none is `Security` |
| Docs | `src/Dexpace.Sdk.Core/README.md` (retry sample), `docs/sdk-documentation/retry.md` (new) | close-out PR |

---

## PR segmentation

Each step is one pull request carrying its code **and** its tests (the one-PR allowance 2a through 5c used), with its
`PublicAPI.Unshipped.txt` diff and `CHANGELOG.md` line.

| PR | Content | Rows | Gate |
|---|---|---|---|
| **1** | `Resilience` namespace; `RetryFacts` moved and grown (`DefaultRetryableStatusCodes`, `IsRetryableFailure`, `IsResendable`); `IRetryableError`, `SdkException.IsRetryable` and the three overrides; the architecture fact | `RETRY-1`–`RETRY-8`, `RETRY-23`–`RETRY-25` (classifier halves), `RECOV-17`, `RECOV-18`; evidence for `XCUT-4`–`XCUT-7`, `XCUT-9`, `XCUT-10`, `BODY-4`, `BODY-5`, `CFG-35` | none |
| **2** | `RetryOptions` (members, defaults, validation, equality, removal); `RetryBackoff`; `RetryPacing` and its vectors (`tests/vectors/retry/pacing.json`, `backoff.json`) | `RETRY-9`–`RETRY-22`, `RETRY-41` (config half), `RETRY-43`, `RECOV-21`–`RECOV-26`, `RECOV-29`, `RECOV-34` | PR 1 (status validation reads `RetryFacts`); `RetryPacingOverflowTests` run first |
| **3** | `RetryEngine`, `RetryBudget`; `RetryPolicy` rewritten over it, the hooks, `RetryAttemptContext`, `AttemptTimeout`, the attempt header, the trail, event 140, the final exhausted predicate | `RETRY-13`, `RETRY-14` (stage half), `RETRY-26`, `RETRY-28`–`RETRY-35`, `RETRY-38`–`RETRY-42`, `RETRY-44`, `RETRY-45` | PRs 1, 2; `RetryPacingOverflowTests` and `ReDriveRequestIsolationTests` run first |
| **4** | `OperationTimeoutException`; `OperationPolicy` on `TimeProvider`; `OverallTimeout`/`AttemptTimeout` validation | evidence for `XCUT-1`, `XCUT-2` | PR 1 (`IsRetryable`) |
| **5** | `RetryRecovery`; `RecoveryDispatcher`'s retry composition; `ResponseRecoveryChain`'s phase entry points | `RETRY-14` (equivalence), `RETRY-27`, `RETRY-36`, `RETRY-37`, `RECOV-16` (clause), `RECOV-19`, `RECOV-20`, `RECOV-27`, `RECOV-28`, `RECOV-30`, `RECOV-31` | PR 3 (the engine) |
| **6** | Close-out: the AOT smoke extended (a `RetryPolicy` 503→200 with a fake clock and zero jitter, `RetryRecovery` through `RecoveryDispatcher`, `OperationTimeoutException` under a fake clock, `IRetryableError` on a custom exception); `docs/sdk-documentation/retry.md`; the 6a checklist with its carried-rows table; the dated corrections; `CHANGELOG.md`; `CLAUDE.md` and README drift; the roadmap status note | all 45 + 18 carried (closing) | 1–5 |

PRs 1 and 4 are independent of 2; 3 needs 1 and 2; 5 needs 3. **Files shared with 6b and 6c** (whichever lands second
re-derives its hunks on the merged file rather than resolving them, the 2a/2b precedent): `PublicAPI.Unshipped.txt`,
`CHANGELOG.md`, `DexpaceLogEvents.cs` and `LogVocabularyTests` (one block each: 140–149, 150–159, 160–169),
`PipelineBuilder.AddStandardResilience` (6a passes `timeProvider` to `OperationPolicy`; 6b may change `RedirectPolicy`'s
constructor in the same array), `DexpaceClientOptions.cs` (6a: the two timeouts; 6b: none expected, `RedirectOptions` is its
own file), `src/Dexpace.Sdk.Core/README.md`, `CLAUDE.md`, and the roadmap's status notes.

---

## Tests, vectors and ports

- **Vectors.** Ported as JSON under `tests/vectors/retry/`, each file citing its source, loaded through `TestSupport`'s
  `VectorFile`: `pacing.json` from `nodejs-sdk@54aeed4 packages/core/src/retry/pacing.test.ts` plus Ruby's
  `test/dexpace/resilience/policy_test.rb` pacing cases and Java's `RetryAfterParser` cases; `backoff.json` from
  `backoff.test.ts` (with a pinned `u` per case). Cases that assert a Node or Ruby host fact (`Number()`'s exponent reading,
  `Float()`'s underscores) are not ported (constraint 10). Where a ported expectation differs from a ruling here (Node's
  millisecond resolution, P6a-14; Ruby's two-digit day, 5a's P5a-11), the vector records the .NET value and a `note` naming
  the ruling.
- **Ports.** `classify.test.ts` → `RetryClassifierTests` (one case per exception class, including a wrapped
  `HttpResponseException` under a narrowing set and a custom `IRetryableError`); `engine.test.ts`'s budget, trail,
  cancellation and override tables → `RetryEngineTests`, `RetryPolicyTests`, `RetryRecoveryTests`;
  `attempt-stamp.test.ts` → `AttemptHeaderTests`; Ruby's `budget_equivalence_test.rb` → `RetryBudgetEquivalenceTests`
  (`RETRY-14`: both stacks, three sends at the defaults, the same delays under one injected random sequence, `RETRY-13`).
- **Deterministic time and randomness.** `FakeTimeProvider` drives every wait, the budget and both timeouts; the random
  source is injected through the internal constructors (P6a-27), a locked `Random(seed)` wrapper for statistical cases and
  a scripted sequence for exact ones (§6.1's thread-safety trap).
- **Stack depth (`RETRY-30`).** 10 000 retries with zero delays through a transport that records
  `new StackTrace().FrameCount`; the maximum equals the first attempt's.
- **Concurrency (`RETRY-42`, `RECOV-28`).** 64 parallel calls through one `RetryPolicy` and one `RecoveryDispatcher`, each
  with its own scripted transport, each asserting its own send count and trail.
- **Fact 10** is a `SystemNet.Tests` loopback case (`Integration`): an `AttemptTimeout` of 5 s, a response whose body is
  streamed after the drive returned, read to the end.
- **Every test** carries `[Trait("Category", "Unit")]` except the AOT-smoke checks (`AotSmoke`), the loopback case
  (`Integration`) and the architecture fact (`Unit`, in `Architecture/`).

---

## Cross-sub-phase interfaces assumed of 6b and 6c

Stated so each sibling's design can confirm or reject them; a rejection reopens the 6a ruling named.

| With | 6a assumes | 6a provides | Ruling |
|---|---|---|---|
| **6b** | (Checked against the 6b draft, 2026-10-08: it assumes `RetryPolicy` keeps a public constructor taking a `TimeProvider`, which `RetryPolicy(TimeProvider? timeProvider = null)` does, and that `RedirectException` is thrown outside `Retry`, which 6a never classifies.) `RedirectPolicy` stays at `PipelineStage.Redirect` (200), above `Retry` (300), and each hop's downstream drive enters `RetryPolicy` afresh, so each hop gets its own retry sequence and budget (5c: `RetrySequenceStarted` already does). The redirect path's decline on a non-replayable body (`BODY-4`'s "fail loudly") reads the same `RequestBody.IsReplayable`. | A `RetryPolicy` that never follows a 3xx (3xx is outside 400–599, so outside any configurable set) and never mutates the request it received. | P6a-9, P6a-33 |
| **6b** | 6b's event ids are 150–159 and it edits only its own block of `LogVocabularyTests`. | Event 140 only. | P6a-28 |
| **6b** | The end-to-end credential-leak test ("nor a retry across a hop") is 6b's and drives the standard pipeline; it may use `RetryOptions` with zero delays. | `RETRY-44`'s isolation; `RetryOptions { BaseDelay = TimeSpan.Zero }` is valid. | P6a-33 |
| **6c** | Auth stays at `PipelineStage.Auth` (500), below retry, so every attempt is re-stamped from the unstamped request; the 401 challenge replay is inside the auth policy and is not a retry (401 is not in the default set). 6c's waits go through `TimeProviderWaits`. | Nothing auth calls changes. | P6a-33 |
| **6c** | 6c's event ids are 160–169. A credential failure is not classified retryable unless it advertises `IRetryableError` or carries an I/O-family cause (6c decides whether any of its exceptions does). Checked against the 6c draft (2026-10-08): its `HttpsRequiredException`, `AuthResolutionException` and `TokenProviderException` (built with a message and no inner I/O exception) classify non-retryable, as it assumes; a raw `HttpRequestException` that a token provider throws and the policy propagates unchanged is retryable under `RETRY-2` (P6a-8), which 6c's "a throwing provider propagates" leaves to the classifier. | `IRetryableError` as a public, implementable capability. | P6a-7, P6a-8, P6a-33 |
| **6b, 6c** | Neither adds a retry loop of its own, a second backoff formula or a second pacing parser (`RETRY-13`). Neither edits `RetryOptions` or the `Resilience` namespace. | The `Resilience` namespace as the single source. | P6a-3, P6a-33 |

---

## Hand-offs to later phases

- **7c:** `Pageable` may run over an `HttpPipeline` with `RetryPolicy`; a paging failure surfaces with its retry trail, and
  `PAGE-13` uses the same `ExceptionTrail` (4b).
- **8a:** the conformance kit asserts a transport maps a pre-response failure to an exception the classifier calls retryable
  (`ServiceRequestException`, an I/O-family inner, or `IRetryableError`), and that it honours the attempt-linked token.
- **8b:** `TRANSPORT-2` (the native client's retries off) and `TRANSPORT-9`'s abandoning per-call timeout through `LateResult`
  (6a declined it for `AttemptTimeout`, P6a-23); `RequestOptions.Timeout` (`TRANSPORT-5`) stays the transport's, distinct from
  `AttemptTimeout`.
- **9:** the DI package's documentation directs consumers of `AddStandardResilienceHandler` to set `MaxRetryAttempts = 0` or
  drop the handler's retry strategy (§6.1, §11 item 24); `IValidateOptions` may add `BaseDelay ≤ MaxDelay` as a warning; the
  binder must handle `IReadOnlySet<int> RetryableStatusCodes` (a staging type if it cannot).
- **10:** audits `XCUT-1`–`XCUT-10` against 6a's evidence and records the rows.
- **11:** the deviation ledger receives P6a-8, P6a-13, P6a-17 and P6a-6 if the lead keeps them (the §11 items proposed below).

---

## Design, roadmap and `CLAUDE.md` corrections owed at close-out

Proposals only: this design edits none of these files. Each is a dated correction in PR 6, naming the ruling that causes it.

- **§6.1:** the **As built** line (classifier, capability, gate, backoff, pacing, trail, timeouts and the recovery stack
  built; one engine under both stacks); "`RetryFacts` … one internal static class" gains its namespace,
  `Dexpace.Sdk.Core.Resilience` (P6a-4); "Both stacks share `RetryFacts`, the pacing parser and `RetryWait`" → "… and one
  loop" (P6a-3); `OperationTimeoutException` named (P6a-24); `AttemptTimeout` cooperative, not abandoning (P6a-23); the
  recovery stack's position (P6a-5).
- **§5.2:** the **As built** line gains "the recovery-stack engine composes the dispatcher's halves (request chain once;
  transport and response steps per send; recovery steps once), and is not an `IRecoveryStep`" (P6a-5).
- **§10 entry 7:** the classifier's capability widens and does not veto (P6a-8).
- **§11, new items** (numbered at the next free number when they land; 6b and 6c may add items concurrently):
  `RETRY-41`'s clamp versus `RECOV-34` and `HTTP-35`'s rejection (P6a-13); tick resolution for `RETRY-10` and `RETRY-15`
  (P6a-14); `RETRY-21`'s stage-stack list read as an on/off switch (P6a-17); "out-of-range" versus a large numeral in
  `RETRY-16`/`RETRY-18` (P6a-18); `RECOV-19`'s "re-sent" read as "every send" in the dispatcher composition (P6a-6).
- **§12:** the `RETRY` row (45 of 45 cited; nothing deferred: `RETRY-29`, `RETRY-38`, `RETRY-43` built); the `RECOV` row
  (`RECOV-31` built with `RETRY-38`).
- **Roadmap:** the phase 6 row's `sdk-design refs` cell gains this design's link (appended); a dated status note at close.
- **4b checklist:** one dated correction flipping `RECOV-17`–`RECOV-31` and `RECOV-34` and the two 6a clauses, citing the 6a
  checklist's carried-rows table. **3b checklist:** `BODY-5` and `BODY-4`'s retry third closed, citing the gate matrix test.
  **5a checklist:** `CFG-35`'s "6a wires" clause closed. **5c checklist:** the `RETRY-*` row's "`IsExhausted` is the one
  method 6a replaces" closed.
- **`CLAUDE.md`:** the layout gains `Resilience/` (`RetryFacts`, the calculator, the pacing parser, the engine);
  `Pipeline/Policies/` drops `RetryFacts`; `Recovery/` gains `RetryRecovery`; `Errors/` gains `IRetryableError` and
  `OperationTimeoutException`; "What is genuinely unbuilt" drops "the retry engine over the recovery chain (6a)".

---

## Risks and open questions

1. **Retrying more exceptions (Breaking 4).** A consumer whose transport throws raw `IOException` for a deterministic
   failure will now see retries. Mitigation: the classifier's rule is documented on `RetryPolicy` and the user page, and
   `ShouldRetry` can veto.
2. **Draining error bodies (Breaking 6).** A server that streams a huge error body costs up to 1 MiB per retried attempt.
   The cap is the shared `RECOV-16` bound; draining also returns the connection to the pool.
3. **`X-RateLimit-Reset` as a delta.** Some APIs send seconds-until-reset rather than an epoch; the specification says
   epoch, so a small value reads as a past instant and retries at once (zero, `RETRY-17`). The user page names the case.
4. **The recovery stack's position (P6a-5, P6a-6)** departs from 4b's "as an `IRecoveryStep`" hand-off; the lead may prefer
   the reference's hook shape, at the cost of the request-chain-per-attempt and the response-steps-skipped inconsistencies
   argued in H.
5. **Unsealing `RetryPolicy` (P6a-20).** A subclass with state breaks `RETRY-42`; the XML docs say the hooks must be pure.
6. **6b/6c concurrency.** The shared-file list above. Mitigation: re-derive, never resolve hunks.
7. **The knowledge CLI was not run, and nothing was executed.** Facts 6 (the `long` half), 8, 9, 10 and 19 are the plan's
   first task.

---

## Rulings

Taken by this design in the absence of a human reviewer. Each lists the options considered. **Open for the lead:** P6a-2,
P6a-5, P6a-6, P6a-8, P6a-9, P6a-10, P6a-13, P6a-17, P6a-20, P6a-24, P6a-27. The rest are taken.

1. **P6a-1 — Row ownership.** 6a owns `RETRY-1`–`RETRY-45` (45). It does the work for `RECOV-17`–`RECOV-31` and `RECOV-34`
   and the 6a clauses of `RECOV-16` and `RECOV-26`, whose rows stay in the 4b checklist and are flipped there by one dated
   correction citing 6a's carried-rows table; it closes 3b's `BODY-5` and the retry third of `BODY-4` the same way. `XCUT`
   rows are phase 10's; 6a supplies evidence. Options: give the `RECOV` rows their own rows in 6a's checklist, as Ruby did
   (rejected: the roadmap says the rows "stay in phase 4"; a second row per ID breaks the "each ID exactly once" census).
2. **P6a-2 — No phase 6 segmentation design; 6a/6b/6c are conveniences. Open for the lead.** The card names the build
   lists and the roadmap already calls the segments "independent … over contracts that 4c has already fixed"; this census
   and P6a-1 state 6a's rows. Options: write one first (rejected: it would restate the card; the lead may still require it).
3. **P6a-3 — One internal engine performs every send for both stacks; each stack is an adapter.** Single-sourcing the loop,
   not unifying the stacks: the budget stays recovery-only and §11 item 19 stands. Options: two loops over shared functions,
   as Ruby (rejected: the trail, the gate order and the cap arithmetic would each be written twice, and `RETRY-34`'s
   reference drift is exactly a two-loop bug).
4. **P6a-4 — A new internal `Dexpace.Sdk.Core.Resilience` namespace** holds `RetryFacts` (moved), the calculator, the parser,
   the budget and the engine; it references `Recovery`, never `Pipeline`, and an architecture fact says so. Options: keep
   `RetryFacts` in `Pipeline.Policies` (rejected: the recovery layer may not reference it, fact 13); put the engine in
   `Recovery` (rejected: `Recovery` is a public namespace of contracts, and the engine is internal machinery).
5. **P6a-5 — The recovery stack is `RetryRecovery` (configuration only) composed by `RecoveryDispatcher`: the request chain
   once, transport plus response steps per send, recovery steps once. Open for the lead.** Answers 4b's open question.
   Options: a per-call public `IRecoveryStep` (Java); a transport decorator (Ruby P6-3); a public static helper (all argued
   in H).
6. **P6a-6 — In that composition every send's surviving response is classified (`RECOV-19` read as "every send"), so an
   initial 503 is retried without an `ErrorMappingStep`. Open for the lead.** Options: re-classify only re-sent responses
   and pass an initial `Success` through, as the reference (rejected: in this shape the engine performs the initial send, so
   the distinction would make one 503 retried and another not, depending only on its position).
7. **P6a-7 — `IRetryableError` in `Errors`; `SdkException.IsRetryable` virtual, default `false`; the two transport
   exceptions sealed `true`; `HttpResponseException` baked once; `OperationTimeoutException` `false`.** Options: a property
   on `SdkException` with no interface (rejected: a third-party exception could not participate, `XCUT-6`); name the baked
   flag differently from the capability, as Ruby's P6-10 (rejected: in the .NET walk the `HttpResponseException` branch runs
   before the capability branch and stops the walk, so the baked flag can never be consulted by mistake).
8. **P6a-8 — The classifier: call token first; an `HttpResponseException` anywhere decides by the configured set; else a
   `true` capability or the I/O family anywhere is retryable; a `false` capability does not veto. Open for the lead.**
   Options: a `false` capability is authoritative (Ruby's P6-4; rejected: it contradicts `RETRY-2`'s "anywhere in its cause
   chain" for every SDK wrapper of an `IOException`).
9. **P6a-9 — `RetryableStatusCodes` is an `IReadOnlySet<int>` on `RetryOptions`, copied to `FrozenSet<int>`, 400–599 only,
   defaulting to `XCUT-7`'s set; both stacks consult it. Open for the lead** on the type (rule A.3 says lists) and the range.
   Options: `IReadOnlyList<int>` (rejected: order and duplicates are meaningless); the full `RETRY-1` classifier as the
   default, as Node (rejected: `XCUT-7` names the default and design §6.1 adopts it); any 100–599 code (rejected: a non-error
   status cannot become an `HttpResponseException`, `XCUT-8`).
10. **P6a-10 — No configurable method set; the gate reads `RetryFacts.IdempotentMethods`. Open for the lead.** Options: a
    `RetryableMethods` member (rejected: a widening re-opens the bare-POST retry; a narrowing has no requester).
11. **P6a-11 — `RetryNonIdempotentWhenReplayable` is removed** (Breaking 2), as design §6.1 directs.
12. **P6a-12 — Names and defaults:** `MaxRetryAttempts` (retries) = 2, `BaseDelay` = 200 ms, `Multiplier` = 2.0,
    `MaxDelay` = 8 s, `Jitter` = 0.2. Options: rename to the specification's `InitialDelay`/`MaxAttempts` (rejected: §6.1
    keeps Polly's vocabulary, P14, and the as-built names).
13. **P6a-13 — `RetryOptions` validates every member at `init` (`RECOV-34`), rejecting a negative `MaxRetryAttempts`;
    `RETRY-41`'s clamp clause is unreachable. Open for the lead.** Options: clamp a negative to the default and log (rejected:
    `RECOV-34` and `HTTP-35` are MUSTs that reject, and a record cannot log); validate in `RetryPolicy` at call time (rejected:
    a bad value would live for the client's lifetime, P5a-4's reasoning).
14. **P6a-14 — Nanosecond clauses are read at tick (100 ns) resolution, and the duration ceiling is checked explicitly
    against ~292 years.** Options: a `long` nanosecond representation internally (rejected: every wait and `TimeSpan` is
    tick-based; the extra precision would be discarded at the timer).
15. **P6a-15 — The calculator computes in `double` over ticks, short-circuits a zero base, compares before casting, floors a
    negative sample, returns the base for a sub-tick width, and clamps every result to 365 days.**
16. **P6a-16 — `FixedDelay` is built (`RETRY-43`)**, below the pacing hint, unjittered and uncapped by `MaxDelay`. Options:
    defer to `docs/first-release.md` (rejected: four lines, and it gives `RETRY-39`'s third step a subject).
17. **P6a-17 — One fixed pacing precedence for both stacks; `HonorRetryAfter` kept as the stage stack's on/off switch for all
    four headers; the recovery stack ignores it. Open for the lead.** Options: a configurable `PacingHeaders` list (rejected:
    a stage-only member with a closed vocabulary, and a `Security` edit); no switch (rejected: drops an opt-out nobody asked to
    drop).
18. **P6a-18 — A well-formed numeral saturates and clamps to 365 days; "out of range" means outside the grammar or a date
    field's range.** Only the first value of a repeated header is read; surrounding SP/HTAB is trimmed. Options: an overflowing
    numeral is "no hint" (rejected: it turns a server's longest request for patience into a 200 ms retry).
19. **P6a-19 — `X-RateLimit-Reset` is digits-only epoch seconds; a positive delta is multiplied by `1 + 0.2u` from the
    injected random source; a zero delta stays zero.**
20. **P6a-20 — `RetryPolicy` is unsealed with sealed overrides and two protected virtual hooks, `ShouldRetry` and
    `GetDelayOverride`, over `RetryAttemptContext`; `RETRY-29` is met by `ShouldRetry` with a documented recipe. Open for the
    lead.** Options: delegates on `RetryOptions` (rejected: unbindable, and break value equality); delegates in the
    constructor (viable; rejected for the Azure.Core precedent and a smaller surface); a built-in override-header member
    (viable for `RETRY-29`; rejected as surface for a MAY the hook already serves).
21. **P6a-21 — A discarded retryable response is read for pacing first, then drained through `ErrorBodyBuffer` into an
    `HttpResponseException` trail entry; a forced non-error status is disposed with no entry; a drain failure becomes the
    entry and the loop continues.** Options: dispose unread (as built; rejected: it leaves `RETRY-34` nothing to attach for a
    status retry and closes the connection).
22. **P6a-22 — The trail is attached only to a surfaced exception, oldest first, skipping self, and dropped on success or a
    returned response.**
23. **P6a-23 — `AttemptTimeout` is a per-attempt linked `CancellationTokenSource` on the `TimeProvider` inside `RetryPolicy`,
    cooperative (no `LateResult`), bounding the drive until it returns, and mapping to `ServiceRequestTimeoutException`.**
    Options: abandon the attempt through `LateResult` (rejected: two attempts could write one replayable body at once);
    enforce it in `InstrumentationPolicy` or the transport (rejected: an attempt is the retry policy's unit, §6.1).
24. **P6a-24 — `OverallTimeout` surfaces the new `OperationTimeoutException` (non-retryable, the inner exception's trail copied);
    `OperationPolicy` arms it on its `TimeProvider`; both timeouts accept `null` or `(0, 49 days]`. Open for the lead.**
    Options: reuse `ServiceRequestTimeoutException` (rejected: it is retryable and names the request, not the call); keep
    "non-positive means none" (rejected: `HTTP-35`'s reasoning applies).
25. **P6a-25 — The call's token is checked before and after every send; a late response is disposed and the call throws
    `OperationCanceledException`.**
26. **P6a-26 — `RETRY-38` and `RECOV-31` are built together as `RetryOptions.AttemptHeaderName`.** The stamp is
    `request.WithHeader(name, ordinal)` on a per-attempt copy (the idempotency key, stamped upstream, is preserved); disabled
    returns the same instance. Both stacks stamp from 1, because in both the engine performs the initial send: `RECOV-31`'s
    "only the retries (2, 3, …) are stamped when the engine runs as a recovery hook" has no subject in the P6a-5
    composition, and its direct-dispatch clause (ordinal 1 stamped) is the one that applies. Options: `⏳` to
    `docs/first-release.md` (rejected: P4b-2 asked 6a to decide both together, and the feature is ten lines).
27. **P6a-27 — The random source is injectable only through internal constructors (`InternalsVisibleTo` the test project);
    the public constructors use `Random.Shared.NextDouble`. Open for the lead.** Options: a public `Func<double>` parameter
    (rejected: test-only surface; a consumer testing its SDK sets `Jitter = 0`).
28. **P6a-28 — 5c's three calls kept; the final exhausted predicate (cap spent, condition and gate held, effective count above
    zero); one log event, 140 `dexpace.retry.delay_override_failed`; the recovery stack emits nothing.** Options: a per-retry
    debug log event (rejected: the span event carries it, and 5b's level contract would make it noise).
29. **P6a-29 — `RETRY-14` by derivation: the recovery cap is the effective retry count plus one; `RequestOptions.MaxRetries`
    is honoured by both stacks.**
30. **P6a-30 — One retry layer per call path stays the SDK's** (§11 item 24); 6a adds no handler-level retry and no guidance
    beyond the user page; the DI package's guidance is phase 9's.
31. **P6a-31 — No `Security` class is edited**; `RetryPacingOverflowTests` and `ReDriveRequestIsolationTests` run first after
    PRs 2 and 3.
32. **P6a-32 — Six PRs, each code and tests; PR 6 the close-out.** Options: one PR (rejected: five subjects that review
    better apart); the recovery stack first (rejected: it needs the engine).
33. **P6a-33 — The interfaces assumed of 6b and 6c** are the table in
    [Cross-sub-phase interfaces](#cross-sub-phase-interfaces-assumed-of-6b-and-6c).

---

## Deviation Ledger

Every entry is a `P6a-n` ruling above that departs from a requirement's letter, reads an ambiguous clause, or decides a
judgement call the lead may reverse. **Open** means the lead has not ruled; **taken** means the design argues it from an
existing design section and it lands as built unless reversed.

| ID | Decision | Touches | Kind | State | Route |
|---|---|---|---|---|---|
| P6a-2 | No phase 6 segmentation design; 6a/6b/6c are conveniences | roadmap segmentation rule | process | **open** | roadmap status note (PR 6) |
| P6a-3 | One engine under both stacks; not a unification | `RETRY-13`, `RETRY-28`, `RECOV-30`, §11 item 19 | mechanism | taken | §6.1 dated correction (PR 6) |
| P6a-4 | The internal `Resilience` namespace; `RetryFacts` moved | `HTTP-9`, `CFG-35` (no clause) | layout | taken | `CLAUDE.md`, §6.1 (PR 6) |
| P6a-5 | The recovery stack composed by the dispatcher, not an `IRecoveryStep` | `RECOV-17`–`RECOV-20`, 4b's hand-off | shape judgement | **open** | §5.2 dated correction (PR 6) |
| P6a-6 | Every send classified in the composition | `RECOV-17`, `RECOV-19` | reading | **open** | new §11 item (PR 6) |
| P6a-8 | The capability widens and does not veto | `RETRY-2`, `RECOV-17`, `XCUT-6` | reading | **open** | §10 entry 7 dated correction (PR 6) |
| P6a-9 | A status set, 400–599, `XCUT-7`'s default | `RETRY-1`, `RETRY-37`, `XCUT-7`, 5a rule A.3 | surface judgement | **open** | §6.1 note; 5a rule A.3 note (PR 6) |
| P6a-10 | No configurable method set | `RECOV-18`, `RECOV-34`, `XCUT-10` | reading | **open** | checklist cites `HTTP-9` |
| P6a-13 | Negative counts rejected; `RETRY-41`'s clamp unreachable | `RETRY-41`, `RECOV-34`, `HTTP-35` | conflict resolution | **open** | new §11 item (PR 6) |
| P6a-14 | Tick resolution | `RETRY-10`, `RETRY-15` | reading | taken | new §11 item (PR 6) |
| P6a-17 | Fixed precedence; `HonorRetryAfter` as the stage switch | `RETRY-21`, `RETRY-20`, `RECOV-22` | reading | **open** | new §11 item (PR 6) |
| P6a-18 | Large numerals clamp; malformation is no hint | `RETRY-16`, `RETRY-18` | reading | taken | new §11 item (PR 6) |
| P6a-20 | Unsealed `RetryPolicy` with protected virtual hooks; `RETRY-29` by the hook | `RETRY-29`, `RETRY-39`, `RETRY-40`, styleguide 6.2 | surface judgement | **open** | SDK overlay row if kept (PR 6) |
| P6a-23 | `AttemptTimeout` cooperative, not abandoning | `XCUT-2`, `CFG-21` | mechanism (§10 entry 8 already sanctions the form) | taken | §6.1 dated correction (PR 6) |
| P6a-24 | `OperationTimeoutException`; timeout validation `(0, 49 days]` | `XCUT-1`, `XCUT-2` | surface + behaviour judgement | **open** | §6.1 dated correction (PR 6) |
| P6a-26 | Both stacks stamp from ordinal 1 | `RECOV-31`, `RETRY-38`, §11 items 10 and 20 | reading | taken | §12 dated correction (PR 6) |
| P6a-27 | Internal random seam | design §6.1 ("injectable") | surface judgement | **open** | §6.1 dated correction (PR 6) |
| P6a-33 | The interfaces assumed of 6b and 6c | — | cross-sub-phase contract | **open** until 6b's and 6c's designs confirm | each sibling's Prerequisite section |

No ruling leaves a MUST's letter unmet beyond what §10 entries 7, 8, 12 and 13 already record: P6a-6, P6a-13, P6a-14, P6a-17
and P6a-18 are readings proposed as §11 items, and P6a-8 extends entry 7 by dated correction. No new §10 entry is opened.
