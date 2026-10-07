# Phase 5a — Configuration: Design

**Status:** Draft, for review. Written 2026-10-07 against `main` at `4130f7b` (phases 0 to 4c merged), on branch
`62-phase-5-planning` (issue #62). Brainstormed without a human in the loop: every judgement call the brainstorming skill
would have put to the lead is taken here as a numbered ruling (`P5a-n`) with the options and the rationale, and the
judgement calls are marked **open for the lead**. 5b (logging and redaction) and 5c (tracing and metrics) are designed in
parallel by other authors; the interfaces this design assumes of them are stated in
[the cross-sub-phase interfaces](#cross-sub-phase-interfaces-assumed-of-5b-and-5c) and in P5a-28. The scope authority is the
roadmap's Phase 5 card and Phase List row 5 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). The format follows
the 4c design (`docs/work/mvp/phase4/phase4c/2026-10-05-phase4c-pipeline-design.md`) and its 4a and 4b siblings.

**What this document is.** The sub-phase design for 5a: one disposition per requirement row (26 rows), the public surface
5a adds or changes, the internal types behind it, the breaking changes, the hand-offs from 2b, 3a, 4a and 4c that 5a takes
or declines, the pull-request segmentation, the `Security` classes that must stay green, and the rulings, which double as
the deviation-ledger IDs (roadmap constraint 7).

**What this document is not.** It is not the plan and not the checklist. It does not restate design §8.2's tier mapping or
§8.3's prohibition (roadmap constraint 9); it cites them and records a decision against each row. It does not design the
binding tier (`CFG-1`–`CFG-7`, `CFG-10`, `CFG-11`, `CFG-14`, `CFG-37`, `CFG-38`), which travels to phase 9, nor the
transport's installation of a proxy, which the phase 8 card gives to 8b. It edits no design chapter, roadmap cell or
`CLAUDE.md` line: the corrections it owes are listed as proposals in
[Corrections owed at close-out](#design-roadmap-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor or sibling | Kind | State at `4130f7b` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030` over `BannedSymbols.txt`, `MA0051`, `CA2007`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. Every type below is written under them; the new `RS0030` entries (P5a-8) are added by 5a itself, each with its one sanctioned `#pragma` site. |
| Phase 1, S7 (`RetryPacingOverflowTests`) and every other `Security` class | **dependency** (constraint 5) | Met. 5a touches `RetryPolicy`'s HTTP-date branch and its wait; [Keeping the `Security` classes green](#keeping-the-security-classes-green) says why no class is edited. |
| 2a (`HttpHeaderSyntax`, `RequestConditions`, `Headers`) | **dependency** | Met. `HttpHeaderSyntax.IsValidName` is the token predicate `BuildInfo` uses (P5a-22); `RequestConditions` formats its dates through 5a's `HttpDate` (P5a-12). |
| 2b (`OperationDescriptor.BuildRequest(DexpaceClientOptions)` and its `BaseAddress` rules) | **dependency** | Met. 2b's hand-off "5a may move `BaseAddress` validation to construction" is taken (P5a-4). |
| 3a (`ResponseBody.DefaultMaxMaterializedBytes`) | **convenience** | Met. 3a's hand-off "5a makes the cap configurable" is **declined and re-routed to 7a** (P5a-23). Nothing of 5a waits on it. |
| 4a (`ContextStore.DefaultCapacity`) | **convenience** | Met. 4a's "5a may make the capacity configurable" is **declined** (P5a-24). |
| 4b (`ExceptionFacts.EnumerateCauses`, `Disposal`, `ClientIdentityStep`) | **dependency** | Met. The `CFG-35` cause-chain half walks `EnumerateCauses` (P5a-21); `LateResult` disposes through `Disposal` (P5a-9); the default `User-Agent` stays a `ClientIdentityPolicy` concern, only its default line changes (P5a-22). |
| 4c (`HttpPipeline`'s per-call `DexpaceClientOptions` overloads, `BlockingWait`, `CallState.Options`, `PipelineContext.Options`) | **dependency** | Met. The three 4c hand-offs are decided here: the options become immutable (P5a-3), the per-call overloads are kept (P5a-6), and `BlockingWait` is taken over as the sync wait (P5a-7). |
| A phase 5 segmentation design | **convenience**, with a stated substitute | Absent. The roadmap's segmentation rule applies to phase 5 (it spans ch.15 and ch.16). The card already names each sub-phase's build list, and the census below plus P5a-1 and P5a-2 stand in for the row split, as P4c-1 did for phase 4. **Open for the lead** (P5a-2). |
| **5b** (logging and redaction) | **convenience**, both ways | Designed in parallel. 5a owns the options-record rule 5b's `HttpLoggingOptions` follows, and 5a's proxy warning goes through 5b's event scheme and emission guard once they exist (P5a-28). Neither blocks the other: 5a ships an interim guard; 5b's options record compiles against today's `DexpaceClientOptions` shape if 5b lands first, and is re-derived onto the record if 5a lands first. The roadmap's "5a leading 5b is soft" stands. |
| **5c** (tracing and metrics) | **convenience** | Designed in parallel. 5c's only contact with 5a is the clock rule (P5a-8) and `BuildInfo`'s version (P5a-22). |
| **Phase 6 enters on 5a** (roadmap: "Entry. 4b, 4c and 5a have exited") | 5a is a **dependency of 6** | 6a consumes `HttpDate`, `TimeProviderWaits`, `LateResult`, the `CFG-35` classifier and the records; 6b and 6c consume the records. [Hand-offs](#hand-offs-to-later-phases) lists each. |
| **Phase 9 enters on 5a** (roadmap ordering rationale 7) | 5a is a **dependency of 9** | Phase 9 binds the records 5a makes immutable, and may feed `ProxyOptions.FromEnvironment(Func<string, string?>, ILogger)` from `IConfiguration`. |

**No dependency edge inverts the roadmap's order.** 5a reads nothing 5b or 5c builds; the phase 5 boundaries stay
conveniences, and no dated status note is owed for an inverted edge.

---

## Governing documents, and the phase-start queries

- **Normative.** `docs/product-spec/16-configuration.md` §16.3–§16.6 and appendix C rows `CFG-8`, `CFG-9`, `CFG-12`,
  `CFG-13`, `CFG-15`–`CFG-36`. Cited because a 5a type touches them: `PIPE-17` (immutable options), `RETRY-15` (the
  date tolerances), `RETRY-26`, `XCUT-3` (the wait), `ASYNC-5`, `ASYNC-18`, `TRANSPORT-9` (the late result), `XCUT-5`,
  `XCUT-6` (the classifier), `XCUT-21` (UUID versus CSPRNG), `TRANSPORT-15`, `TRANSPORT-30` (the proxy's consumer),
  `RECOV-33` (the identity tokens).
- **Design.** §3.8 (the seam table), §8.2 (configuration, the proxy model, dates, identifiers, equality, identity), §8.3
  (the clock, the wait, the prohibition), §6.1 (the pacing parser shares the RFC 1123 parser), §10 entries 8
  (`cooperative-cancellation`), 25 (`configuration-via-iconfiguration`) and 26 (`typed-accessors-fail-fast`), §11 items 1,
  4, 15, 23, 27 and 28, §12's `CFG` row (38 of 38 cited; `CFG-13` declined, `CFG-20` retired).
- **Styleguide.** `csharp/06-types-and-data-modeling.md` 6.1 (records, `with`), 6.2 (sealed), 6.5 (`init`), 6.8 (explicit
  enum values); `csharp/10-api-design.md` 10.1 (internal by default), 10.2 (immutable records), 10.5 (token last), 10.8
  (named factories); `csharp/09-concurrency.md` 9.1 (no blocking on async outside a documented bridge);
  `csharp/08-error-handling.md` (BCL exception types); `csharp-aspnetcore/01-host-and-configuration.md` 1.4 and
  `02-dependency-injection.md` 2.1 (no static configuration slot). The overlay's `I`-prefix and `Async`-suffix departures
  stand.
- **Siblings.** `nodejs-sdk@54aeed4` is now the local `HEAD` (2a, 2b and 4c found it absent and used `c0ff3fd`; the plan
  pins whichever `HEAD` it reads): `packages/core/src/config/{http-date,proxy,clock,build-info,equality,identifiers,retryable}{,.test}.ts`.
  `ruby-sdk`'s design documents are local (`docs/work/mvp/phase5/phase5a/2026-09-09-phase5a-configuration-design.md`,
  read for its rulings R1–R7 and its proxy and date grammar), but its `gems/` tree is not, as every earlier .NET phase found;
  the card's `ruby-sdk/gems/dexpace-core/test/dexpace/{http_date_test.rb,proxy/,proxy_test.rb}` are therefore taken from
  that design's case lists and from Node's tables.

**The knowledge queries could not be run on this host.** `scripts/knowledge` is a .NET program and the host has no .NET SDK
(the CLI reports "A compatible .NET SDK was not found", 2026-10-07). The step-1 reading was done on the corpus files
directly, which is what the CLI prints from:

| Query (intended) | Done instead | Result |
|---|---|---|
| `--origin note --brief` | read `docs/knowledge/notes/*.md` | six files, seven notes: `CA1062`, the `I` prefix, the `Async` suffix, Shouldly, `LangVersion`, and the two 4b notes on `Outcome` as an abstract class. None touches configuration. The `Async`-suffix note binds `TimeProviderWaits.DelayAsync` and `Sleep`. |
| `--section conflicts --brief` | read every harvested file's `## Conflicts` section | none open for configuration (`configuration.md`'s section is empty); the overlay's rows are all settled by the notes above. |
| `--prefix-info CFG` | appendix C rows 521–558 and `harvested/configuration.md` | 38 IDs (29 MUST, 8 SHOULD, 1 MAY); 26 in 5a's scope; owning chapter `16-configuration.md`; the harvested topic `configuration` carries 83 `CFG` citations. |
| `--gaps CFG` | the roadmap's gap table | no `CFG` ID is among the 30 gap IDs; every in-scope ID was nonetheless read from appendix C, which states several clauses the chapter abbreviates (`CFG-17`'s sub-millisecond SHOULD, `CFG-24`'s "warning log", `CFG-29`'s single-digit padding). |
| `--prefix-info XCUT` (cited only) | appendix C rows for `XCUT-3`, `XCUT-5`, `XCUT-6`, `XCUT-21` | read; 5a owns none of them. |

The plan re-runs the five queries on a host with the pinned SDK and records any difference; a difference that contradicts a
ruling below reopens that ruling.

---

## Scope and the 26-row census

**5a owns 26 rows: `CFG-8`, `CFG-9`, `CFG-12`, `CFG-13`, `CFG-15`–`CFG-36`** (18 MUST, 7 SHOULD, 1 MAY), the card's list
exactly. The other twelve `CFG` IDs are phase 9's (Phase List row 9). No `OBS` row is 5a's: `OBS-1`–`OBS-40` divide between 5b
and 5c, so the phase 5 exit is 26 + 40 = 66 rows.

Planned status, one line per ID. ✅ = built and tested in 5a; N/A and 🚫 as the roadmap's constraint 3 legend.

| ID | Level | Planned | One line |
|---|---|---|---|
| `CFG-8` | MUST | ✅ | `DexpaceClientOptions`, `RetryOptions`, `RedirectOptions` (and every later options record) are sealed records with `init` accessors; collections are copied to immutable storage at `init` (P5a-3). |
| `CFG-9` | MUST | ✅ | Derivation is `with`, copy-on-write over nested records; the source-seam clause is vacuous in core, which holds no seam (§8.2; the seams are phase 9's `CFG-11`). |
| `CFG-12` | SHOULD | N/A | No configuration builder type exists: an object initializer has no in-progress instance that another thread can see (§8.2; P5a-25). |
| `CFG-13` | SHOULD | 🚫 | No process-wide configuration slot; design §10 entry 25, topic `configuration-via-iconfiguration` (the house guide's service-locator rule). |
| `CFG-15` | MUST | ✅ | The seam is `TimeProvider` (§3.8, §8.3); the blocking interruptible sleep is `TimeProviderWaits.Sleep`, taking over 4c's `BlockingWait` (P5a-7). |
| `CFG-16` | MUST | ✅ | Elapsed time is measured with `TimeProvider.GetTimestamp`/`GetElapsedTime` only; wall-clock reads outside `TimeProvider` are banned in `src/` (P5a-8). |
| `CFG-17` | MUST | ✅ | `Sleep` rejects a negative duration with `ArgumentOutOfRangeException`, returns at once for zero, and surfaces `OperationCanceledException` with the token still signalled (§8.3: the re-assert is free). |
| `CFG-18` | SHOULD | ✅ | `TimeProviderWaits.DelayAsync`: a timer, no thread held; zero completes synchronously; negative rejected (closing the `-1 ms` = infinite trap of §8.3); cancellation disposes the timer. |
| `CFG-19` | SHOULD | ✅ | Free on `await` and on `SyncPath`'s completed-task read, which rethrow the original exception (§8.3); pinned by a test, no helper (P5a-10). |
| `CFG-20` | SHOULD | 🚫 | Retired: no interruptible-task future, `Thread.Interrupt` is banned; design §10 entry 8, topic `cooperative-cancellation` (P5a-10). |
| `CFG-21` | MUST | ✅ | Cooperative form: the internal `LateResult` helper disposes a `Response` produced after the caller stopped awaiting, exactly once and quietly (§8.3; P5a-9). |
| `CFG-22` | MUST | ✅ | `ProxyOptions`: sealed record carrying `ProxyType`, host and port, the ordered glob list, user name and password, a challenge-credentials slot and `BypassAll`; `ToString` masks credentials (P5a-13). |
| `CFG-23` | MUST | ✅ | `ProxyOptions.IsBypassed(host)`: `BypassAll` short-circuits; otherwise full-string, case-insensitive glob match, patterns compiled once at `init` (P5a-14). |
| `CFG-24` | MUST | ✅ | `ProxyOptions.FromEnvironment`: `HTTPS_PROXY` over `HTTP_PROXY`, never throws, invalid yields `null` and a warning; the system-property layer is substituted by the explicit `ProxyOptions` argument (§10 entry 25; P5a-15). |
| `CFG-25` | MUST | ✅ | The port must be explicit and in 0..65535, read from the raw authority (never `Uri.Port`, which defaults); otherwise `null` and a warning (P5a-15). |
| `CFG-26` | MUST | ✅ | `NO_PROXY` split on unescaped commas, drop empty, unescape, trim, in that order; the pipe-separated property form has no .NET source (P5a-15). |
| `CFG-27` | MUST | ✅ | A resolved list that is exactly one bare `*` returns `null` (route directly); `BypassAll` is the explicit flag on a constructed instance; `*` in a longer list is a glob (P5a-15). |
| `CFG-28` | MAY | ✅ | Taken in its prohibitive half: nothing in core reads proxy configuration; `Environment.GetEnvironmentVariable` is banned in `src/` with one pragma in the resolver's default lookup (P5a-17). The convenience half (a global configuration) has no slot to read (`CFG-13`). |
| `CFG-29` | MUST | ✅ | `HttpDate.Format`: `DateTimeOffset.ToString("r", InvariantCulture)` on the UTC instant (§8.2), zero-padded day, literal `GMT`. |
| `CFG-30` | MUST | ✅ | `HttpDate.Parse`/`TryParse`: hand-written span parser; month and zone case-insensitive; `GMT`, `UTC`, `+0000`, `+00:00`; the weekday is stripped, never validated (P5a-11). |
| `CFG-31` | MUST | ✅ | The same parser rejects blank input and a missing comma after the weekday, and every form outside the grammar, RFC 850 and asctime included (§11 item 27; P5a-11). |
| `CFG-32` | MUST | ✅ | `Guid.NewGuid()`: version 4, IETF variant, thread-safe, from the OS CSPRNG (stronger than required, §8.2); pinned by tests on `IdempotencyKeyStep`'s default strategy; no new type (P5a-19). |
| `CFG-33` | MUST | ✅ | The internal `DeepValue.Equals`/`GetHashCode`: arrays by content, recursive for object arrays, null-safe, consistent hashing (P5a-20). |
| `CFG-34` | MUST | ✅ | The same helper compares `double`/`float` elements by bit pattern after NaN canonicalisation: NaN equals NaN, `+0.0` differs from `-0.0`; `object[]` and `double[]` of equal values are unequal (§11 item 15; P5a-20). |
| `CFG-35` | SHOULD | ✅ | `RetryFacts.IsRetryableStatus` (408, 429, 5xx except 501 and 505) and `RetryFacts.IsRetryableCause` (cycle-safe walk over the §11 item 23 I/O family); 6a wires `XCUT-5` and `XCUT-6` onto it (P5a-21). |
| `CFG-36` | SHOULD | ✅ | `BuildInfo`: the SDK version and runtime identity resolved once, each falling back to `unknown`, and `IdentityTokens` = `[dexpace-dotnet/<v>, dotnet/<runtime>]`, every token non-blank and header-safe (P5a-22). |

**Totals: 26 rows — 23 ✅, 1 N/A (`CFG-12`), 2 🚫 (`CFG-13`, `CFG-20`), 0 ⏳.** By level: 18 MUST (all ✅), 7 SHOULD
(`CFG-12` N/A, `CFG-13` and `CFG-20` 🚫, `CFG-18`, `CFG-19`, `CFG-35` and `CFG-36` ✅), 1 MAY (`CFG-28` ✅).

**Rows other phases own on which 5a works** (their checklist rows cite 5a's tests; 5a adds no row for them):

| Row (owner) | 5a's contribution |
|---|---|
| `PIPE-17` (4c, ✅) | Its "options MUST be immutable/shared" half was carried by the documentation of a mutable class; the records make it structural. 4c's row gains a citation of 5a's immutability test at close-out. |
| `RETRY-15` (6a) | The HTTP-date clause (tolerant weekday, single-digit day) is met by `HttpDate.TryParse`, which `RetryPolicy` adopts in 5a (P5a-12); the fractional, `-ms` and rate-limit clauses stay 6a's. |
| `RETRY-26`, `XCUT-3` (6a, 10) | The wait both rows describe is `TimeProviderWaits`. |
| `ASYNC-5`, `TRANSPORT-9` (8a/8b) | The orphaned-result rule has its shared helper (`LateResult`); 8b's per-call timeout is its first transport consumer. |
| `XCUT-5`, `XCUT-6` (10, built in 6a) | The single classifier exists; 6a bakes `HttpResponseException.IsRetryable` from it and adds the capability check. |
| `TRANSPORT-15`, `TRANSPORT-30` (8b) | `ProxyOptions` is the model 8b installs; its masked `ToString` is `TRANSPORT-30`'s "MUST NOT log credentials" at the source. |
| `RECOV-33` (4b) | The default token line becomes `BuildInfo.IdentityTokens` joined; the step's behaviour is unchanged. |

---

## Facts the design rests on

The host has no .NET SDK, so nothing was run for this design. Each fact below is either **verified by the design** (cited,
run on 10.0.401 by the design's authors) or **to verify in the plan's first task** (a throwaway program in the scratchpad, as
4c did). A to-verify fact that comes out the other way reopens the ruling that cites it.

| # | Fact | Status | Used by |
|---|---|---|---|
| 1 | `DateTimeOffset.ToString("r", InvariantCulture)` emits `Sun, 06 Nov 1994 08:49:37 GMT`. | verified, §8.2 | P5a-11 |
| 2 | `DateTimeOffset.ParseExact(s, "r", …)` validates the weekday and rejects `UTC`, `+0000` and lower case; `RetryConditionHeaderValue.TryParse` accepts RFC 850 and asctime and rejects an inconsistent weekday and lower case. | verified, §6.1, §8.2 | P5a-11 (why a hand-written parser) |
| 3 | `Task.Delay(TimeSpan.FromMilliseconds(-1))` never completes (`-1 ms` is infinite); `-2 ms` throws; the `TimeProvider` overload behaves the same. A timer due time above `uint.MaxValue − 1` ms throws. | verified, §8.3, §6.1 | P5a-7 |
| 4 | `Task<T>.WaitAsync(token)` abandons the task: a result delivered after the wait was cancelled is disposed by nobody. | verified, §8.3 | P5a-9 |
| 5 | `0.0.Equals(-0.0)` is `true`; `double.NaN.Equals(double.NaN)` is `true`; records compare array members by reference. | verified, §8.2, §11 item 15 | P5a-20 |
| 6 | `WebProxy.BypassList` entries are regular expressions; `HttpClient.DefaultProxy` does not bypass on `NO_PROXY=*`, defaults an absent port, and returns credentials in `GetProxy`'s URI. | verified, §8.2 | P5a-13 to P5a-15 (why core owns the model) |
| 7 | `new Uri("http://proxy").Port` is 80 and `new Uri("http://proxy:80").IsDefaultPort` is `true`, so neither `Port` nor `IsDefaultPort` can tell an explicit port from an absent one. | to verify | P5a-15 (the raw-authority port read) |
| 8 | A `with` expression on a record copies the backing fields and then runs the `init` accessor of each assigned member only, so a derived field computed in an `init` accessor (the compiled globs) is recomputed exactly when its source member is reassigned. | to verify | P5a-13, P5a-14 |
| 9 | A sealed record may declare `public override string ToString()` and a private `PrintMembers(StringBuilder)`; the synthesised `EqualityContract` and copy constructor are private on a sealed record and do not appear in `PublicAPI.Unshipped.txt` (as `RequestOptions` shows). | to verify (the API-file half is visible in the tree) | P5a-3, P5a-5 |
| 10 | `Regex` with `RegexOptions.NonBacktracking \| IgnoreCase \| CultureInvariant` constructs under trimming and NativeAOT (the interpreter path), and `\A…\z` rejects a trailing `\n` where `$` accepts it. | to verify (the AOT smoke covers it) | P5a-14 |
| 11 | `Environment.Version` reports the runtime version (`10.0.x`) under NativeAOT; `RuntimeInformation.FrameworkDescription` contains a space (`.NET 10.0.x`) and so is not a product token. | to verify | P5a-22 |
| 12 | The as-built `RetryPolicy` waits only for `delay > TimeSpan.Zero`, so rejecting a negative delay inside the wait changes none of its behaviour. | read, `RetryPolicy.cs` | P5a-7 |
| 13 | `RetryPacingOverflowTests`' `RecordingTimeProvider` observes due times through `TimeProvider.CreateTimer`, which `Task.Delay(TimeSpan, TimeProvider, …)` and `BlockingWait` both call. | read, the test file | Keeping the `Security` classes green |
| 14 | Three sites format or parse HTTP dates today: `SetDatePolicy` (`ToString("r")`), `RequestConditions.SetDate` (`ToString("R", InvariantCulture)`), `RetryPolicy.ParseRetryAfter` (`TryParseExact(…, "r", …)`). | read | P5a-12 |
| 15 | Two test sites mutate options after construction: `ClientIdentityPolicyTests` (`options.UserAgent = …`) and `RetryPolicyTests` (`options.Retry.BaseDelay = …`, three lines). Every `Security` class and README sample uses object initializers. | read (`grep`) | Breaking change 1 |

---

## Argued positions

### A. The options records (`CFG-8`, `CFG-9`, `CFG-12`; P5a-3 to P5a-6)

`DexpaceClientOptions`, `RetryOptions` and `RedirectOptions` become `public sealed record`s whose every property has an
`init` accessor; defaults stay in property initializers (design §8.2's tier 4). Nothing about their *values* changes in 5a:
6a owns the retry defaults (`MaxRetryAttempts = 2`, 8 s, jitter) and the removal of `RetryNonIdempotentWhenReplayable`;
6b owns `StripSensitiveHeadersOnCrossOrigin`'s removal and the hop cap. 5a changes the type kind, not the knobs.

**The rule every options record follows** (5a owns it; 5b's `HttpLoggingOptions` and any later record follow it):

1. `public sealed record` in `Dexpace.Sdk.Core.Configuration`, every property `{ get; init; }`, no `required` member unless
   the record has no meaningful default (only `ProxyOptions.Host`/`Port` qualify).
2. A nested options record is a property with a non-null default instance (`= new()`); assigning `null` throws
   `ArgumentNullException` in the `init` accessor. `with` is shallow, so nesting *records* is what makes a derived copy share
   nothing mutable (§8.2's precondition, P7).
3. A collection member is exposed as `IReadOnlyList<T>` and copied into an immutable array in the `init` accessor, so a
   caller's list mutated after construction cannot reach the record (`CFG-8`'s defensive copy). Equality over it is by
   content (a custom `Equals`/`GetHashCode` pair, the `RequestOptions` precedent).
4. Single-property invariants are checked in the `init` accessor and throw `ArgumentException`/`ArgumentNullException`;
   cross-property and range rules are not checked by the record (P5a-4).
5. A member that can carry a secret or a URL is rendered by a hand-written `ToString`, never the synthesised one (P5a-5).

**`CFG-9`'s derivation is `with`.** `options with { Retry = options.Retry with { MaxRetryAttempts = 5 } }` leaves `options`
and `options.Retry` unchanged. `CFG-9`'s second sentence (env and property seams inherited by reference) has no subject in core:
the records hold no source seam, and phase 9's `IConfiguration` sits outside them. `CFG-10` (removal) is vacuous on a record and
is phase 9's row.

**`CFG-12` is N/A, argued.** The requirement is about a builder object that may be mutated while unfinished. Core ships no
configuration builder: an object initializer is compiled into one constructor call plus `init` calls on an instance no other
code can reach until the expression completes. `PipelineBuilder` and `Headers.Builder` are not configuration builders, and both
already document single-threaded use.

**Validation (P5a-4).** `BaseAddress` moves from `OperationDescriptor.BuildRequest` to its `init` accessor (2b's hand-off): an
absolute `http`/`https` URI with no fragment, else `ArgumentException` naming the redacted value (§10 entry 29's rule).
`UserAgent`, `Retry` and `Redirect` reject `null`. Nothing numeric is checked: what a negative `MaxRetryAttempts` or a
zero `OverallTimeout` means is 6a's and 6b's to rule with the behaviour they rebuild, and phase 9's `IValidateOptions` is
where cross-property rules (`BaseDelay ≤ MaxDelay`) and fail-at-boot belong (§10 entry 26). `BuildRequest` keeps its null check
for an unset `BaseAddress` and drops its duplicate scheme check.

**`ToString` (P5a-5).** The synthesised record `ToString` would print `BaseAddress` through `Uri.ToString()` (which unescapes and
is banned on wire and log paths, design §3.5) and would print any future secret. `DexpaceClientOptions` declares its own
`PrintMembers` that renders `BaseAddress` through `UrlRedactor` and the nested records through their own `ToString`.

**Per-call overloads (P5a-6, closing P4c-15's 5a half).** `HttpPipeline.SendAsync`/`Send(Request, DexpaceClientOptions,
CancellationToken)` are **kept**. With a mutable class they were a hazard (one instance shared by two calls could be edited
between them); with an immutable record they are a coherent feature: "this call runs with these client options", the natural
partner of `with`. `RequestOptions` remains the seam's per-call carrier (timeout, retry cap, tags); `DexpaceClientOptions`
remains the policies' configuration. The remarks on `HttpPipeline` that say "phase 5a decides their future" are rewritten to
state the rule, and four `Security` classes, `Pageable` and the AOT smoke stay unedited.

### B. The clock and the waits (`CFG-15`–`CFG-21`; P5a-7 to P5a-10)

`TimeProvider` is the seam (§3.8). 5a adds the operation it lacks, the blocking interruptible sleep, and makes every SDK wait
go through one place.

```csharp
namespace Dexpace.Sdk.Core.Configuration;

public static class TimeProviderWaits
{
    public static void Sleep(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken);
    public static Task DelayAsync(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken);
}
```

- **Semantics, both forms.** `null` provider → `ArgumentNullException`; `delay < TimeSpan.Zero` (including
  `Timeout.InfiniteTimeSpan`) → `ArgumentOutOfRangeException` (`CFG-17`, `CFG-18`, fact 3); zero → returns, or a completed
  task, without arming a timer; an already-signalled token → `OperationCanceledException` before any timer; a delay above 49
  days runs as successive bounded waits (S7's chunking, unchanged). Cancellation surfaces `OperationCanceledException` carrying
  the caller's token; the token stays signalled, which is `CFG-17`'s "re-assert" (§8.3, P7).
- **`Sleep`** is 4c's `BlockingWait` moved and published: a `TimeProvider.CreateTimer` setting a `ManualResetEventSlim`
  waited with the token. It blocks only the caller's thread, which is what a sync call asked for (§11 item 1). `BlockingWait`
  and `BlockingWaitTests` are renamed into it; `MaxSingleWait` stays internal.
- **`DelayAsync`** wraps `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` with the same guard and chunking.
  `RetryPolicy`'s private chunk loop collapses into one call per path.
- **Sub-millisecond precision** (`CFG-17`'s SHOULD) is what `TimeProvider`'s timer gives; nothing rounds.

**Why public (P5a-7, open for the lead).** `CFG-15` requires the seam to *expose* a sleep; `TimeProvider` exposes none, so
an internal helper leaves third-party sync policies and consumer SDKs' synchronous pollers to `Thread.Sleep`, which no fake
clock advances. Two extension methods on a BCL type, in the `Configuration` namespace a caller already imports for the
options, is the smallest public answer. The alternative is to keep it internal and mark `CFG-15` ✅ on `TimeProvider` alone.

**Clock discipline, enforced (P5a-8).** `CFG-15`'s "SHOULD route through this seam" and `CFG-16`'s "MUST NOT be used for
elapsed-time measurement" become `RS0030` entries in `BannedSymbols.txt`:

| Banned | Reason line | Sanctioned site |
|---|---|---|
| `Thread.Sleep` (all overloads) | not cancellable, no fake clock; use `TimeProviderWaits.Sleep` (§8.3, `CFG-15`) | none |
| every `Task.Delay` overload | `-1 ms` waits forever and no overload chunks past ~49.7 days; use `TimeProviderWaits.DelayAsync` (§8.3, `CFG-17`, `CFG-18`) | `TimeProviderWaits`, one `#pragma` |
| `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow` | wall clock outside the seam; use `TimeProvider.GetUtcNow` or `GetTimestamp` (`CFG-15`, `CFG-16`) | none (no site today) |
| `Environment.GetEnvironmentVariable(string)`, `(string, EnvironmentVariableTarget)`, `GetEnvironmentVariables()` | nothing reads the environment implicitly (`CFG-28`) | `ProxyOptions`' default lookup, one `#pragma` |

`Stopwatch` is not banned: it is monotonic, so it does not violate `CFG-16`, and its one site (`InstrumentationPolicy`) is
5c's to restructure. 5c is asked to measure through `TimeProvider.GetTimestamp` so a fake clock drives durations (P5a-28).

**The late result (`CFG-21`; P5a-9).** `CFG-20`'s interruptible future retires (§10 entry 8), and its `CFG-21` obligation
survives cooperatively: SDK code that stops awaiting a task that can still produce a `Response` must dispose a late result.
`Task<T>.WaitAsync` is already banned for exactly this reason, with "needs a #pragma citing SEAM-30". 5a ships the one helper
that makes the pragma unnecessary everywhere else:

```csharp
namespace Dexpace.Sdk.Core.Internal;

internal static class LateResult
{
    // Waits for the task until the token fires. On cancellation, attaches one continuation that disposes the result if the
    // task later completes successfully (quietly, through Disposal), then throws OperationCanceledException.
    internal static Task<T> WaitOrDisposeAsync<T>(Task<T> task, CancellationToken cancellationToken) where T : class, IDisposable;

    // Attaches the disposing continuation without waiting: for a caller that abandons a task for any other reason.
    internal static void DisposeWhenCompleted<T>(Task<T> task) where T : class, IDisposable;
}
```

The continuation runs `TaskContinuationOptions.ExecuteSynchronously | OnlyOnRanToCompletion`; a faulted late task has its
exception observed so it cannot surface as `TaskScheduler.UnobservedTaskException`. "Exactly once" holds twice over: one
continuation is attached per abandonment, and `Response`'s dispose is latched (3b). The `WaitAsync` pragma lives here only, and
the `BannedSymbols.txt` reason lines for the `WaitAsync` and `TaskCompletionSource<T>.SetResult` entries gain "or go through
`LateResult`". **No as-built site needs it today:** `AsAsync` already disposes a response produced after cancellation inside its
worker (2b), and nothing else abandons a `Response`-producing task. Its consumers are 6a (`AttemptTimeout`, if 6a implements it
with an abandoning wait) and 8b (`TRANSPORT-9`'s per-call timeout). It is internal because no caller outside core has the
problem without also owning the task (P5a-9).

**`CFG-19` (P5a-10).** No unwrapping helper ships: `await` and `GetAwaiter().GetResult()` on a completed task rethrow the
original, and `.Result`/`.Wait()` (the only `AggregateException` producers) are banned (§8.3, §9.1). One `Unit` test pins it:
a policy whose transport throws `IOException` surfaces `IOException`, not `AggregateException`, through `Send` and `SendAsync`.

### C. RFC 1123 dates (`CFG-29`–`CFG-31`; P5a-11, P5a-12)

```csharp
namespace Dexpace.Sdk.Core.Http.Common;

public static class HttpDate
{
    public static string Format(DateTimeOffset instant);                      // CFG-29
    public static DateTimeOffset Parse(string value);                         // CFG-30, CFG-31: FormatException on failure
    public static bool TryParse(string? value, out DateTimeOffset instant);   // the no-throw twin RetryPolicy uses
}
```

**Format** is `instant.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)` (fact 1).

**Parse is hand-written over a span** (fact 2 rules out both BCL routes; design §6.1 asks for "one span-based RFC 1123
parser shared with `CFG-29`–`CFG-31`"). The grammar, applied to the caller's string with no trimming or normalisation first:

```text
weekday  = 3 ASCII letters, then ", "     ; required; matched and discarded, never compared with the date (CFG-30)
day      = 1*2 DIGIT                       ; a single-digit day is accepted (RETRY-15; P5a-11)
SP month = 3 letters, case-insensitive against Jan..Dec
SP year  = 4 DIGIT
SP time  = 2DIGIT ":" 2DIGIT ":" 2DIGIT    ; 00-23, 00-59, 00-59 (no leap second: 60 is rejected)
SP zone  = "GMT" / "UTC" / "+0000" / "+00:00", the letters case-insensitive
end of input
```

Every number is read digit by digit (no `int.Parse`, so culture cannot enter), the date is assembled with `new DateTimeOffset(…,
TimeSpan.Zero)` inside a range check (31 February fails), and any mismatch is a failure: `Parse` throws `FormatException`
whose message names the expected form and **does not echo the input** (a header value can carry anything); `TryParse` returns
`false`. Blank input and `Mon 01 Jan 2024 00:00:00 GMT` fail at the first rule (`CFG-31`). RFC 850 and asctime fail at the
first rule as well, which §11 item 27 records against RFC 9110: a rejected date is "no hint" to the retry policy, never a wrong
wait.

**P5a-11 (open for the lead): `HttpDate` is public.** Generated SDKs format and parse date-valued headers (`If-Modified-Since`
through `RequestConditions` already, `Last-Modified`, `Expires`, `Date` on responses), and the BCL's two parsers are wrong in
opposite directions (fact 2); a consumer left without this one will reach for `"r"`. The single-digit day is accepted because
`RETRY-15` requires it and design §6.1 counts it among the four forms the parser must accept; `CFG-31`'s "strict on the
day-of-month-onward grammar" names blank input and the missing comma as its failures and does not demand two digits. That
reading is proposed as a §11 item. The case-folding of the zone is a superset of `CFG-30` (month names), in the direction it
sets.

**P5a-12: the three as-built sites move onto it in 5a.** `SetDatePolicy` and `RequestConditions` call `HttpDate.Format`
(byte-identical output: no behaviour change). `RetryPolicy.ParseRetryAfter`'s HTTP-date branch calls `HttpDate.TryParse`, so
it now accepts a lower-case date, a weekday inconsistent with the date, `UTC`/`+0000`/`+00:00` and a single-digit day — a
behaviour change, marked **Breaking (behaviour)** and logged in `CHANGELOG.md`. The rest of the pacing parser (fractional
seconds, `retry-after-ms`, `x-ms-retry-after-ms`, `X-RateLimit-Reset`, the decimal screen) is 6a's. The alternative — leave
`RetryPolicy` alone and let 6a switch — ships a shared parser with no consumer and leaves `RETRY-15`'s date clause failing for a
phase; the swap is one call.

### D. The proxy model (`CFG-22`–`CFG-28`; P5a-13 to P5a-18)

```csharp
namespace Dexpace.Sdk.Core.Configuration;

public enum ProxyType { Http = 0, Socks4 = 1, Socks5 = 2 }

public sealed record ProxyOptions
{
    public ProxyType Type { get; init; }                        // default Http
    public required string Host { get; init; }                 // non-blank; validated at init
    public required int Port { get; init; }                    // 0..65535; validated at init
    public IReadOnlyList<string> NonProxyHosts { get; init; }   // ordered globs; copied and compiled at init
    public string? UserName { get; init; }                      // documented-nullable (CFG-37)
    public string? Password { get; init; }                      // documented-nullable; never rendered
    public ICredentials? ChallengeCredentials { get; init; }    // the challenge-handler slot (P5a-13)
    public bool BypassAll { get; init; }                        // CFG-27's explicit flag

    public bool IsBypassed(string host);                        // CFG-23
    public override string ToString();                          // CFG-22: credentials masked
    public bool Equals(ProxyOptions? other);                    // NonProxyHosts by content; credentials by value
    public override int GetHashCode();

    public static ProxyOptions? FromEnvironment();                                              // CFG-24..CFG-27
    public static ProxyOptions? FromEnvironment(ILogger logger);
    public static ProxyOptions? FromEnvironment(Func<string, string?> environment, ILogger logger);
}
```

**Shape (P5a-13, open for the lead on the challenge slot).** Host and port replace `CFG-22`'s "socket address" (`DnsEndPoint`
would add nothing the transport uses, and splitting them puts `CFG-25`'s range check on one member, as Ruby did). `ProxyType`
carries `CFG-22`'s three kinds and nothing more; `socks4a` maps to `Socks4` and `socks5h` to `Socks5` at resolution, because
`SocketsHttpHandler` accepts those schemes (to verify in 8b, not here). **The challenge-handler slot is typed `ICredentials?`**:
.NET's native proxy authentication asks an `ICredentials` for `(proxy URI, auth scheme)` when a 407 arrives, which is the
challenge-driven hook the reference's slot exists for; `UserName`/`Password` stay as the Basic fallback `TRANSPORT-30` names.
Options considered: an SDK challenge-handler interface (rejected: 6c has not built `AUTH`'s challenge model, and a proxy type
that waits for it blocks 8b); omitting the slot as N/A (rejected: `CFG-22` is a MUST and the slot has a native meaning).
`ProxyOptions` is **not** a property of `DexpaceClientOptions`: it configures the transport, which `DexpaceClientOptions` does
not reach (§8.2: "an SDK-managed `Http.SystemNet` transport calls that resolver"). Phase 9 binds it beside the client options.

**`ToString` and equality.** `ToString` renders `ProxyOptions { Type = Http, Host = proxy.internal, Port = 3128,
NonProxyHosts = [*.internal, localhost], UserName = ***, Password = ***, ChallengeCredentials = set, BypassAll = False }`: a
present credential prints `***`, an absent one `(none)`, so a log line tells "configured" from "not configured" without the
value (`CFG-22`; `TRANSPORT-30`'s "MUST NOT be logged"). Equality is hand-written because the record's synthesised equality
would compare the compiled-pattern array by reference (fact 8): `NonProxyHosts` by ordinal content, `ChallengeCredentials` by
reference, everything else by value.

**Globs (P5a-14, open for the lead on the curl divergence).** Each pattern is compiled once, in `NonProxyHosts`' `init`
accessor (fact 8: `with` recompiles only when the list is reassigned), into `new Regex(@"\A" + body + @"\z",
NonBacktracking | IgnoreCase | CultureInvariant)` where `body` is `Regex.Escape(glob)` with the escaped `\*` replaced by `.*`
and `\?` by `.` (`CFG-23`'s letter: metacharacters escaped, full-string, case-insensitive). `\z`, not `$`, so a host with a
trailing newline is not a match (fact 10); `NonBacktracking`, so a pattern like `*a*a*a*b` cannot backtrack catastrophically and
no timeout is needed; `.` without `Singleline` does not cross a newline, so `*.example.com` cannot match
`evil.com\n.example.com` (Ruby's reasoning, kept). `IsBypassed(host)` returns `true` when `BypassAll`, else when any pattern
matches; a null host throws, an empty host matches only an empty-matching pattern. **This follows the specification where it
differs from curl and the platform**: `NO_PROXY=.internal.example.com` is, to curl and `HttpClient.DefaultProxy`, a domain
suffix; here it is a literal glob that matches only the host `.internal.example.com`. The documentation of `FromEnvironment`
says to write `*.internal.example.com`, and the divergence is proposed as a §11 item. The alternative — also treat a leading
`.` as `*.` plus the apex — is a superset of `CFG-23` that the conformance case ("matches subdomains … but not the apex")
forbids for the `*` form only; it is the lead's call whether to add it.

**Resolution (P5a-15, open for the lead).** `FromEnvironment` reads, through the lookup it is given (the default is
`Environment.GetEnvironmentVariable`, the one sanctioned site, P5a-17):

1. **Which variable.** `HTTPS_PROXY`, then `https_proxy`, then `HTTP_PROXY`, then `http_proxy`; the first present and
   non-empty value is chosen (an empty value is absent, `CFG-2`'s rule applied here). Upper case first is the specification's
   exact key; lower case is the curl and platform convention, a superset. **The `httpoxy` guard:** when `GATEWAY_INTERFACE` is
   present (a CGI host), upper-case `HTTP_PROXY` is skipped with a warning, because a CGI server copies the client's `Proxy:`
   request header into it. One proxy serves every target: `HTTPS_PROXY` is preferred for `http://` targets too, as `CFG-24`
   says, which diverges from the platform's per-scheme choice (part of the divergence §8.2 records).
2. **No fallthrough on failure.** If the chosen value is malformed, resolution returns `null` and warns; it does not try the
   next variable. A malformed `HTTPS_PROXY` silently replaced by `HTTP_PROXY` would route traffic through a proxy the operator
   meant to replace.
3. **The URL grammar.** `scheme://[user[:password]@]host:port[/]`. The scheme is required and one of `http`, `socks4`,
   `socks4a`, `socks5`, `socks5h` (case-insensitive); `https` (TLS to the proxy) and anything else is invalid rather than
   silently downgraded. The port is read from the raw authority text, after the last `@` and outside IPv6 brackets, never from
   `Uri.Port` (fact 7): it must be present, all ASCII digits, and in 0..65535 (`CFG-25`). User and password are
   percent-decoded with `Uri.UnescapeDataString` (a `+` stays a `+`). A path other than `/`, a query or a fragment is invalid.
4. **The bypass list.** `NO_PROXY`, then `no_proxy`; split on commas not preceded by a backslash, drop empty fragments,
   replace `\,` with `,`, trim — in that order, so a whitespace-only fragment survives as an empty token (`CFG-26`'s observable
   order). Exactly one bare `*` returns `null` (`CFG-27`); `*` in a longer list is a glob. `CFG-26`'s pipe-separated
   system-property form has no .NET source.
5. **Never throws** (`CFG-24`). Every failure is `null` plus one warning naming the variable and the rule broken, never the
   value (it may hold a password). The lookup function is the caller's; if it throws, that exception propagates — the
   never-throw promise covers the input, not a broken seam (`CFG-37`: a null lookup or logger throws `ArgumentNullException`).

**`CFG-24`'s system-property layer** has no .NET source; the explicit `ProxyOptions` value a caller constructs stands in for it,
the substitution §10 entry 25 already records. **The environment seam is public** (P5a-16): `FromEnvironment(Func<string,
string?>, ILogger)` gives tests hermetic input without touching the process environment and gives phase 9 a way to resolve
from `IConfiguration` (`key => configuration[key]`), which is `CFG-28`'s MAY ("a convenience resolver MAY read proxy options
from the global configuration") in the container-scoped form.

**Overloads (P5a-16).** `()`, `(ILogger)`, `(Func<string, string?>, ILogger)`, with no optional parameters (`RS0026`/`RS0027`,
the 4c finding). The parameterless form logs nothing: a warning with no logger to receive it is the as-built `NullLogger`
default, which 5b may revisit.

**The warning, before 5b (P5a-28).** One `LoggerMessage.Define<string, string>` at `Warning` ("Proxy configuration in {Variable}
was ignored: {Reason}."), with a provisional `EventId` that 5b's event scheme renumbers. The call is wrapped in `catch
(Exception ex) when (!ExceptionFacts.IsFatal(ex))`, because a throwing `ILogger` must not make `FromEnvironment` throw
(`CFG-24`, and `OBS-20`'s rule ahead of 5b's guard); 5b replaces the wrapper with its emission guard.

**`CFG-28` (P5a-17).** Nothing in core or in `Http.SystemNet` calls `FromEnvironment` in 5a, and the `RS0030` ban on
`Environment.GetEnvironmentVariable` (P5a-8) makes any other environment read a reviewed exception. The prohibition's other
half — "nothing may read proxy configuration implicitly at construction" — meets the transport in 8b, which inherits a live
question: the as-built `new SystemNetHttpClient()` builds a `SocketsHttpHandler` whose `UseProxy` default makes the runtime read
`HTTP(S)_PROXY` implicitly on the first request (fact 6), and design §8.2 has the SDK-managed transport call `FromEnvironment`
at construction instead. 5a records the reading it assumes 8b takes: a documented call of the resolver by the transport's
parameterless constructor is "a resolver explicitly invoked", and a constructor taking `ProxyOptions?` lets a caller opt out
(P5a-18 hands this to 8b; it is proposed as a §11 item there, not here).

**The `IWebProxy` adapter is 8b's (P5a-18).** The phase 8 card lists "installing `ProxyOptions`" under 8b. 5a ships the model
and the resolver only, and edits nothing in `Dexpace.Sdk.Http.SystemNet`. Options considered: a public
`ProxyOptions.ToWebProxy()` in core (rejected: `IWebProxy` is in the reference pack, so core could, but the adapter's behaviour —
`GetProxy` must not return userinfo, `Credentials` from `ChallengeCredentials` or a Basic `NetworkCredential`, `TRANSPORT-30`'s
warnings — is transport policy, and a second transport would want its own).

### E. Identity, identifiers, equality and the classifier (`CFG-32`–`CFG-36`; P5a-19 to P5a-22)

**`CFG-32` (P5a-19).** `Guid.NewGuid()` is a version-4 UUID (verified, §8.2) from the operating system's CSPRNG, stronger than
the non-cryptographic generator the requirement permits, and thread-safe without shared state. Its one consumer is
`IdempotencyKeyStep.KeyStrategy`'s default. 5a adds no type: two `Unit` tests on the default strategy assert the version nibble
(`4`), the variant bits (`10`), and no collision across 100 000 keys minted from parallel tasks. `XCUT-21`'s security path stays
`RandomNumberGenerator`, separate as the specification insists (6c's cnonce).

**`CFG-33`/`CFG-34` (P5a-20, open for the lead on visibility).** An internal `DeepValue` with `Equals(object?, object?)` and
`GetHashCode(object?)`: two nulls are equal and `null` hashes to zero; two arrays are equal when their runtime array types are
identical, their ranks and lengths match and their elements are deep-equal (object arrays recurse, so nested and
multi-dimensional arrays compare structurally); `double` and `float` elements, boxed or not, compare by
`BitConverter.DoubleToInt64Bits`/`SingleToInt32Bits` after canonicalising every NaN to one pattern, so NaN equals NaN and `+0.0`
differs from `-0.0` (fact 5); `object[] { 1.0 }` and `double[] { 1.0 }` differ (distinct array kinds); a non-array falls back
to `object.Equals`. Hashing mirrors each rule. **It is internal**, because no as-built model carries an array of floating-point
values (the models compare strings and bytes, which ordinary equality already gets right), and design §8.2 says the helper
"ships when a model first carries an array". It ships now because `CFG-33`/`CFG-34` are this sub-phase's MUSTs and a helper is
what they require; a public promotion is 7a's call when generated models with array members meet serde. The alternative,
public now, adds a surface no caller in the tree needs (styleguide 10.1).

**`CFG-35` (P5a-21, open for the lead).** Two internal predicates on the existing `RetryFacts` (2a's single source):

- `IsRetryableStatus(int code)`: 408, 429, and 500–599 except 501 and 505. This is `XCUT-5`'s single classifier, and it is
  **not** `RetryPolicy`'s configured set (`408, 429, 500, 502, 503, 504`), which is `XCUT-7`'s data and 6a's to rebuild; the
  two stay separate, as `XCUT-5`'s closing note requires.
- `IsRetryableCause(Exception exception)`: `true` iff the exception or any cause from `ExceptionFacts.EnumerateCauses` (bounded
  and cycle-safe, `XCUT-9`) is an `IOException`, a `SocketException`, a `TimeoutException`, or an `HttpRequestException` whose
  `StatusCode` is `null` — §11 item 23's reading of "the I/O family". A caller-cancelled `TaskCanceledException` is not
  retryable; an `HttpClient` timeout is, because its `InnerException` is a `TimeoutException`.

Ruby built only the status half, because its core could not name the I/O classes (its R1). .NET's reference pack holds all four,
so both halves are buildable correctly here. What 5a does **not** build is `XCUT-6`'s capability (`IRetryableError`) and the
baked `HttpResponseException.IsRetryable`; 6a adds the capability check to `IsRetryableCause` (a widening) and bakes the flag from
`IsRetryableStatus`. No as-built behaviour changes in 5a. Options: status half only, as Ruby (rejected: the reason does not hold on
.NET); defer both to 6a (rejected: `CFG-35` is 5a's row and 6a's `RetryFacts` work then has two jobs).

**`CFG-36` (P5a-22, open for the lead on the `User-Agent` change).**

```csharp
namespace Dexpace.Sdk.Core.Configuration;

public static class BuildInfo
{
    public const string Unknown = "unknown";
    public static string SdkVersion { get; }            // informational version, "+build" stripped; else Unknown
    public static string RuntimeVersion { get; }        // Environment.Version; else Unknown
    public static string RuntimeDescription { get; }    // RuntimeInformation.FrameworkDescription; else Unknown
    public static string OSName { get; }                // "windows" / "linux" / "macos" / "freebsd" / ... ; else Unknown
    public static IReadOnlyList<string> IdentityTokens { get; } // ["dexpace-dotnet/<SdkVersion>", "dotnet/<RuntimeVersion>"]
}
```

- **Resolved once**, in the static constructor's field initializers, each in its own non-fatal `try`, so one failing probe (an
  attribute read under trimming, a runtime API that throws on an exotic host) yields `unknown` for that field only.
- **Header-safe tokens.** A version is kept only if it is a valid RFC 9110 token (`HttpHeaderSyntax.IsValidName`, the token
  predicate 2a made public); anything else becomes `unknown`. An informational version carrying a space or a non-ASCII character
  would otherwise make every request's `User-Agent` invalid at `Headers`' outbound validation — the defect Node's
  `build-info.ts` records for `navigator.userAgent`.
- **The fallback is `unknown`** where the as-built `SdkVersion` falls back to `0.0.0` (§8.2's recorded divergence, closed).
  The internal `SdkVersion` is kept as a forwarder to `BuildInfo.SdkVersion`, so `DexpaceDiagnostics`' `ActivitySource` and
  `Meter` versions read the same value (5c's interface).
- **Vendor.** `CFG-36` lists "runtime version, vendor, OS name"; `RuntimeDescription` (`.NET 10.0.x`) carries the vendor's product
  name, and no separate vendor field is invented (one runtime, one vendor).
- **The default `User-Agent` becomes `string.Join(' ', BuildInfo.IdentityTokens)`**, `dexpace-dotnet/<v> dotnet/<runtime>`:
  `CFG-36`'s "default ordered identity-token list … for User-Agent-style composition". This is a visible behaviour change
  (Breaking 3). The OS is not put in the default line: `RuntimeInformation.OSDescription` is free text, and the stable
  `OSName` is exposed for a consumer who wants it. Options: keep the one-token default (rejected: the descriptor would exist
  and the default would not use it); add an OS comment (rejected: more fingerprinting surface in every request for no
  requirement).

### F. Declined and re-routed hand-offs (P5a-23, P5a-24)

**The materialisation cap (3a's hand-off; P5a-23, open for the lead).** 3a shipped `ResponseBody.DefaultMaxMaterializedBytes`
(64 MiB) and said "5a makes it configurable through the options surface". The options surface cannot reach it: a `ResponseBody`
is constructed by the transport, and its readers (`ReadAsBytesAsync`, `ReadAsStringAsync` and their sync twins) are called by
the caller or by serde, neither of which holds a `DexpaceClientOptions`. The knob that would work is a per-read limit, and 3a
already found why it cannot be an overload (`RS0026`/`RS0027` against the existing `CancellationToken = default` members);
reshaping those members is the breaking batch 7a will make anyway when it adds `ReadValue`/`ReadValueAsync` over the same
readers. **Decision:** the cap stays a constant in 5a; the per-read limit is handed to 7a, which reshapes the reader family once.
The 3a checklist's hand-off row is corrected to name 7a. Options: a `DexpaceClientOptions.MaxMaterializedBytes` read by nothing
(rejected: a dead option, the very thing phase 0 had to flag); a process-wide static (rejected: `CFG-13`'s declined slot by
another name). The SSE line cap's configuration, which 3a also routed to "5a with 7b", goes to 7b alone for the same reason.

**The context-store capacity (4a's hand-off; P5a-24).** `ContextStore` is process-wide (`s_shared`), so a per-client option
cannot size it coherently (two clients, two capacities, one store), and a process-wide setter is the global slot `CFG-13`
declines. **Declined:** the capacity stays `ContextStore.DefaultCapacity` (10 000). No requirement asks for it to be
configurable (`CTX-11`–`CTX-13` require a bound, not a knob). The 4a checklist's hand-off is closed as declined.

---

## The public surface (`PublicAPI.Unshipped.txt`)

Core only; `Dexpace.Sdk.Http.SystemNet` and `Dexpace.Sdk.Serialization.SystemTextJson` are untouched. The plan confirms
the exact lines from the analyzer's code fix (fact 9). Abbreviation: `C.` is `Dexpace.Sdk.Core.Configuration.`.

**Removed** (each a `.set` becoming `.init`):

```text
C.DexpaceClientOptions.{AttemptTimeout,BaseAddress,OverallTimeout,Redirect,Retry,UserAgent}.set -> void
C.RetryOptions.{BaseDelay,HonorRetryAfter,MaxDelay,MaxRetryAttempts,RetryNonIdempotentWhenReplayable}.set -> void
C.RedirectOptions.{AllowHttpsToHttpDowngrade,MaxRedirects,StripSensitiveHeadersOnCrossOrigin}.set -> void
```

**Added — the records** (for each of `DexpaceClientOptions`, `RetryOptions`, `RedirectOptions`):

```text
C.<T>.<each property>.init -> void
C.<T>.<Clone>$() -> C.<T>!
C.<T>.Equals(C.<T>? other) -> bool
override C.<T>.Equals(object? obj) -> bool
override C.<T>.GetHashCode() -> int
override C.<T>.ToString() -> string!
static C.<T>.operator ==(C.<T>? left, C.<T>? right) -> bool
static C.<T>.operator !=(C.<T>? left, C.<T>? right) -> bool
```

**Added — new types:**

```text
C.ProxyType (Http = 0, Socks4 = 1, Socks5 = 2)
C.ProxyOptions  (sealed record: ProxyOptions(); Type, Host [required], Port [required], NonProxyHosts, UserName, Password,
                 ChallengeCredentials, BypassAll .get/.init; IsBypassed(string! host) -> bool; Equals(C.ProxyOptions? other);
                 the record overrides and operators as above)
static C.ProxyOptions.FromEnvironment() -> C.ProxyOptions?
static C.ProxyOptions.FromEnvironment(Microsoft.Extensions.Logging.ILogger! logger) -> C.ProxyOptions?
static C.ProxyOptions.FromEnvironment(System.Func<string!, string?>! environment, Microsoft.Extensions.Logging.ILogger! logger) -> C.ProxyOptions?
C.TimeProviderWaits
static C.TimeProviderWaits.Sleep(this System.TimeProvider! timeProvider, System.TimeSpan delay, System.Threading.CancellationToken cancellationToken) -> void
static C.TimeProviderWaits.DelayAsync(this System.TimeProvider! timeProvider, System.TimeSpan delay, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.Task!
C.BuildInfo
const C.BuildInfo.Unknown = "unknown" -> string!
static C.BuildInfo.{SdkVersion,RuntimeVersion,RuntimeDescription,OSName}.get -> string!
static C.BuildInfo.IdentityTokens.get -> System.Collections.Generic.IReadOnlyList<string!>!
Dexpace.Sdk.Core.Http.Common.HttpDate
static Dexpace.Sdk.Core.Http.Common.HttpDate.Format(System.DateTimeOffset instant) -> string!
static Dexpace.Sdk.Core.Http.Common.HttpDate.Parse(string! value) -> System.DateTimeOffset
static Dexpace.Sdk.Core.Http.Common.HttpDate.TryParse(string? value, out System.DateTimeOffset instant) -> bool
```

**Unchanged signatures, changed contracts:** `HttpPipeline.SendAsync`/`Send(Request, DexpaceClientOptions, CancellationToken)`
(documentation only, P5a-6); `OperationDescriptor.BuildRequest(DexpaceClientOptions)` (no longer validates the scheme, P5a-4);
`PipelineContext.Options` (now an immutable value).

**Internal types:** `TimeProviderWaits`' chunking constant (from `BlockingWait`), `LateResult`, `DeepValue`, `RetryFacts`'s two
predicates, the `ProxyOptions` resolver and glob compiler (private nested types or `Internal/ProxyResolution`), and the
`LoggerMessage` holder for the proxy warning.

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes and gets a `CHANGELOG.md` `[Unreleased]` line in the PR
that makes it (constraint 8).

| # | Change | Kind | PR |
|---|---|---|---|
| 1 | `DexpaceClientOptions`, `RetryOptions` and `RedirectOptions` are sealed records with `init` accessors: assigning a property after construction no longer compiles (use `with`); equality and hash code are by value (was: reference); `ToString` renders the members, the base address redacted | source + behaviour | 1 |
| 2 | `DexpaceClientOptions.BaseAddress` is validated when set (absolute `http`/`https`, no fragment) and throws `ArgumentException` there (was: at `BuildRequest`); `UserAgent`, `Retry` and `Redirect` reject `null` | behaviour | 1 |
| 3 | The default `User-Agent` is `dexpace-dotnet/<version> dotnet/<runtime-version>` (was: one token), and an undeterminable version reads `unknown` (was: `0.0.0`), which also changes the `ActivitySource` and `Meter` version in that case | behaviour | 5 |
| 4 | `RetryPolicy` honours HTTP-date `Retry-After` values it used to ignore: lower case, a weekday inconsistent with the date, the `UTC`/`+0000`/`+00:00` zones, a single-digit day | behaviour | 3 |

Additive, with no **Breaking** marker: `ProxyType`, `ProxyOptions`, `TimeProviderWaits`, `BuildInfo`, `HttpDate`. `SetDatePolicy` and
`RequestConditions` now format through `HttpDate.Format`, byte-identical to before, so they carry no marker.

---

## Keeping the `Security` classes green

Constraint 5: green, or moved without weakening. **No `Security` class is edited by 5a** (P5a-26).

| Class | Edit | Why |
|---|---|---|
| `AuthHttpsGuardTests`, `ReDriveRequestIsolationTests`, `RedirectCredentialHygieneTests` | None | They build options with object initializers, which compile against `init` accessors unchanged (fact 15); they call the kept per-call overloads (P5a-6). `RedirectCredentialHygieneTests` still sets `StripSensitiveHeadersOnCrossOrigin = false` in an initializer, which compiles until 6b removes the property. |
| `RetryPacingOverflowTests` (S7) | None | The delta-seconds cases are untouched by P5a-12. The HTTP-date case (`Fri, 31 Dec 9999 23:59:59 GMT`) parses under `HttpDate.TryParse` (a well-formed date; year 9999 is in range), and the clamp and chunking are the same code moved into `TimeProviderWaits`, which still arms its timers through `TimeProvider.CreateTimer`, where `RecordingTimeProvider` observes them (fact 13). The plan runs this class after PR 2 and after PR 3 before anything else. |
| `EnsureSuccessErrorMappingTests`, `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, `UrlRedactionDefaultDenyTests` | None | 5a touches none of their subjects. `UrlRedactor` gains a caller (the options' `ToString`), not a change; 5b rewrites the redactor. |
| SystemNet `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `MalformedContentTypeWireTests`, `RedirectWireTests` | None | 5a edits nothing in `Dexpace.Sdk.Http.SystemNet`. `HeaderInjectionWireTests` sees the new default `User-Agent` on the wire; it asserts on the injected header, not on the agent line (the plan confirms by reading it before PR 5). |

No `Security` class is added: 5a fixes no phase-1 defect. The new `RS0030` entries (P5a-8) are a gate, not a test, and are listed
in the checklist's `CFG-16`/`CFG-28` rows.

---

## Migration plan from the as-built code

| Change | Sites | Rewrite |
|---|---|---|
| Options to records | `Configuration/DexpaceClientOptions.cs` (split into one file per record per styleguide 12, or kept as one file if the plan finds the precedent; the plan follows `Http/Request/`) | `class` → `sealed record`; `set` → `init`; the `BaseAddress` and null guards in `init` accessors over backing fields; `PrintMembers` |
| Post-construction mutation in tests | `ClientIdentityPolicyTests` (the "read on every call" test becomes "a per-call `options with { UserAgent = … }` wins"), `RetryPolicyTests` (three lines into an initializer) | mechanical; no assertion changes value |
| `BuildRequest`'s scheme check | `Operations/OperationDescriptor.cs` | removed; the null check stays; `OperationDescriptorTests`' scheme cases move to `DexpaceClientOptionsTests` with the same inputs |
| `BlockingWait` → `TimeProviderWaits.Sleep` | `Internal/BlockingWait.cs`, `RetryPolicy.SleepAsync`, `Internal/BlockingWaitTests.cs` | renamed and published; the negative guard added; `RetryPolicy` calls `Sleep`/`DelayAsync` |
| Date sites | `SetDatePolicy`, `RequestConditions.SetDate`, `RetryPolicy.ParseRetryAfter` | through `HttpDate` |
| `SdkVersion` | `Internal/SdkVersion.cs`, `DexpaceClientOptions`' default user agent | forwards to `BuildInfo`; the default line joins `IdentityTokens` |
| `BannedSymbols.txt` | the four groups of P5a-8; the `WaitAsync`/`SetResult` reason lines | additions only; no line deleted |
| `HttpPipeline` remarks | `Pipeline/HttpPipeline.cs` | "phase 5a … decides their future" → the per-call rule (P5a-6) |
| Docs | `src/Dexpace.Sdk.Core/README.md` (an options sample with `with`), user page `docs/sdk-documentation/configuration.md` | close-out PR |

---

## PR segmentation

Each step is one pull request carrying its code **and** its tests (the one-PR allowance 2a through 4c used), with its
`PublicAPI.Unshipped.txt` diff and `CHANGELOG.md` line. Code-then-tests-then-docs as three PRs is not used: every step's tests
are its proof, and a code-only PR would land red under `TreatWarningsAsErrors` the moment a test site stops compiling (fact 15).

| PR | Content | Rows | Gate |
|---|---|---|---|
| **1** | The options records (rule A.1–A.5), `BaseAddress` validation moved, `PrintMembers`, the per-call overload remarks; an architecture test that no public type in `Dexpace.Sdk.Core.Configuration` has a public setter and every options record is sealed | `CFG-8`, `CFG-9`, `CFG-12` (N/A argument in the checklist), and `PIPE-17`'s immutability citation | none |
| **2** | `TimeProviderWaits` (absorbing `BlockingWait`), `RetryPolicy` on it, `LateResult`, the `RS0030` entries of P5a-8 with their two pragmas, the `CFG-19` pin test | `CFG-15`–`CFG-21` (`CFG-20` 🚫 in the checklist) | none; `RetryPacingOverflowTests` run first |
| **3** | `HttpDate`, its vectors (`tests/vectors/config/http-date.json`), the three date sites moved | `CFG-29`–`CFG-31` | none |
| **4** | `ProxyType`, `ProxyOptions`, the glob compiler, `FromEnvironment` and its warning, the vectors (`tests/vectors/config/proxy-resolution.json`, `proxy-globs.json`, `no-proxy-split.json`) | `CFG-22`–`CFG-28` | PR 2 (the `Environment` ban lands there) |
| **5** | `BuildInfo` and the default `User-Agent`; `RetryFacts.IsRetryableStatus`/`IsRetryableCause`; `DeepValue`; the `CFG-32` pins | `CFG-32`–`CFG-36` | none |
| **6** | Close-out: the AOT smoke extended (records with `with`, `HttpDate` round trip, `ProxyOptions` glob match under NativeAOT, `BuildInfo` non-blank, `TimeProviderWaits.Sleep` with zero); `docs/sdk-documentation/configuration.md`; the 5a checklist; the dated design corrections; `CHANGELOG.md`; `CLAUDE.md` and README drift; the roadmap status note | all 26 (closing) | 1–5 |

PRs 1, 2, 3 and 5 are independent of each other; 4 follows 2. The likely conflicts with 5b and 5c are in
`DexpaceClientOptions.cs` (5b adds its logging property), `BannedSymbols.txt`, `PublicAPI.Unshipped.txt` and `CHANGELOG.md`;
whichever lands second re-derives its hunks on the merged file rather than resolving them (the 2a/2b precedent).

---

## Tests, vectors and ports

- **Vectors.** Ported as JSON under `tests/vectors/config/`, each file citing its source (`nodejs-sdk@<HEAD>
  packages/core/src/config/http-date.test.ts`, `proxy.test.ts`; the Ruby 5a design's case lists by section), loaded through
  `TestSupport`'s `VectorFile`. Cases that assert a Node or Ruby host fact (`process.version`, `Time.httpdate`'s RFC 850
  acceptance) are not ported (constraint 10). Where a ported case's expectation differs from a ruling here (Node's or Ruby's
  two-digit day; a curl-style leading dot), the vector records the .NET expectation and a `note` naming the ruling.
- **Hermetic.** No test reads the process environment: every `FromEnvironment` test uses the `Func<string, string?>`
  overload, and one `Unit` test asserts the parameterless overload routes through `Environment` by reflection over the
  pragma'd call site, not by setting a variable (tests run in parallel).
- **Clock.** `FakeTimeProvider` (the pinned `Microsoft.Extensions.TimeProvider.Testing`) drives `Sleep` and `DelayAsync`;
  `Sleep` is driven from a second thread advancing the fake clock, with a token-cancel case and a 400-day chunking case.
- **Late result.** A `TaskCompletionSource<TrackingDisposable>` completed after the wait was cancelled asserts exactly one
  dispose; a faulted late task asserts no `UnobservedTaskException` (a `GC.Collect`/`WaitForPendingFinalizers` probe under a
  scoped handler).
- **Every test** carries `[Trait("Category", "Unit")]` except the AOT-smoke checks (`AotSmoke`) and the architecture test
  (`Unit`, in `Architecture/`).

---

## Cross-sub-phase interfaces assumed of 5b and 5c

Stated so each sibling's design can confirm or reject them; a rejection reopens the 5a ruling named.

| With | 5a assumes | 5a provides | Ruling |
|---|---|---|---|
| **5b** | `HttpLoggingOptions` (5b names it, places it, and owns its members) is a `sealed record` in `Dexpace.Sdk.Core.Configuration`, hung off `DexpaceClientOptions` as a non-null nested property with a default instance, its header allow-list an `IReadOnlyList<string>` copied at `init`, compared by content. | Rule A.1–A.5, the record shape of `DexpaceClientOptions` to hang it on, and the architecture test that enforces the rule on every type in the namespace. | P5a-3, P5a-28 |
| **5b** | 5b owns the SDK's `EventId` scheme and event names, and its emission guard (`OBS-20`) is a reusable internal helper. | One provisional `EventId` for the proxy warning, and an interim non-fatal `catch` around the log call that 5b replaces with its guard. | P5a-16, P5a-28 |
| **5b** | 5b's redactor rewrite keeps `UrlRedactor`'s public entry point (or renames it in one place). | `DexpaceClientOptions.PrintMembers` as a new caller of the redactor. | P5a-5 |
| **5c** | 5c measures operation and attempt durations through the pipeline's `TimeProvider` (`GetTimestamp`/`GetElapsedTime`), not `Stopwatch`, and reads no wall clock outside `TimeProvider` (5a's `RS0030` bans `DateTime(Offset).Now/UtcNow`). | The ban list; `CFG-16`'s rule. | P5a-8, P5a-28 |
| **5c** | 5c keeps `DexpaceDiagnostics`' `ActivitySource`/`Meter` version on `SdkVersion.Value` (or moves it to `BuildInfo.SdkVersion`); either reads the same value. | `BuildInfo.SdkVersion` with the `unknown` fallback. | P5a-22 |
| **5b, 5c** | Neither adds a configuration lookup, environment read or static options slot to core. | The `Environment` ban; `CFG-13`'s 🚫. | P5a-8, P5a-17 |

---

## Hand-offs to later phases

- **6a:** consumes `HttpDate.TryParse` (the date half of the pacing parser is done), `TimeProviderWaits` for both waits,
  `LateResult` if `AttemptTimeout` abandons a task, `RetryFacts.IsRetryableStatus` to bake `HttpResponseException.IsRetryable`
  (`XCUT-5`) and `IsRetryableCause` to widen with `IRetryableError` (`XCUT-6`); changes `RetryOptions`' defaults and removes
  `RetryNonIdempotentWhenReplayable` (a record member removal); rules on numeric validation of `RetryOptions` (P5a-4).
- **6b:** removes `RedirectOptions.StripSensitiveHeadersOnCrossOrigin` and rules on `MaxRedirects`' validation.
- **6c:** `AccessTokenCache` already reads `TimeProvider`; its background refresh must not use `Task.Delay` directly (banned).
- **7a:** the per-read materialisation cap (P5a-23) and any public promotion of `DeepValue` (P5a-20).
- **7b:** the SSE line cap's configuration (P5a-23).
- **8b:** the `IWebProxy` adapter over `ProxyOptions`, the parameterless constructor's `FromEnvironment` call and its `CFG-28`
  reading, `TRANSPORT-30`'s warnings, and `LateResult` for `TRANSPORT-9` (P5a-17, P5a-18).
- **9:** binds the records (if the configuration-binding source generator cannot set `init` accessors, the staging-type fallback
  of §8.2 applies); `IValidateOptions` carries the cross-property and range rules 5a does not check (P5a-4); `ProxyOptions` is
  bound beside the client options, or resolved through `FromEnvironment(key => configuration[key], logger)` (P5a-16).

---

## Design, roadmap and `CLAUDE.md` corrections owed at close-out

Proposals only: this design edits none of these files. Each is a dated correction in PR 6, naming the ruling that causes it.

- **§8.2:** the **As built** line (records built; `ProxyOptions` and `FromEnvironment` built in core, installation 8b's; the
  date parser shared; the version fallback `unknown`); "**CFG-36**'s descriptor is `SdkVersion` plus
  `RuntimeInformation.FrameworkDescription`" → `BuildInfo`, whose runtime token is `Environment.Version` because the
  description is not a token (P5a-22); the challenge slot typed `ICredentials?` (P5a-13).
- **§8.3:** the **As built** line (the sync wait built as `TimeProviderWaits.Sleep`; the late-result helper `LateResult`; the
  banned-API gate extended, P5a-7 to P5a-9).
- **§6.1:** "The port's one `RetryWait.DelayAsync`" → `TimeProviderWaits.DelayAsync` (P5a-7).
- **§3.1:** the configurable materialisation cap is 7a's per-read limit, not an options member (P5a-23).
- **§10 entry 25:** `FromEnvironment` reads the upper- and lower-case names with the CGI guard, one proxy for every target
  (P5a-15).
- **§11, new items** (numbered at the next free number when they land; 5b and 5c may add items concurrently): the single-digit
  day accepted under `CFG-31` because `RETRY-15` requires it (P5a-11); `CFG-23`'s globs versus the curl/platform leading-dot
  suffix convention (P5a-14); `CFG-12` read as vacuous without a builder type (P5a-25).
- **§12:** the `CFG` row's notes (`CFG-12` N/A argued; `CFG-21` built as `LateResult`).
- **Roadmap:** the phase 5 row's `sdk-design refs` cell gains this design's link (appended); a dated status note at close.
- **3a checklist / 4a checklist:** the materialisation-cap hand-off names 7a (P5a-23); the capacity hand-off is closed as
  declined (P5a-24). **4c checklist:** `PIPE-17` cites PR 1's immutability test.
- **`CLAUDE.md`:** the layout line for `Configuration/` (`DexpaceClientOptions`, `RetryOptions`, `RedirectOptions`,
  `ProxyOptions`, `TimeProviderWaits`, `BuildInfo`) and `Http/Common/` (`HttpDate`); "What is genuinely unbuilt" drops
  "layered configuration" for core and keeps the binding tier under phase 9. `src/Dexpace.Sdk.Core/README.md`: the options sample
  uses `with`.

---

## Risks and open questions

1. **Phase 9's binder and `init`.** If the configuration-binding source generator does not set `init` accessors, phase 9 binds
   into a staging type (§8.2). 5a's records do not depend on the answer, but a `required` member would complicate staging, which
   is why only `ProxyOptions` carries any (P5a-3).
2. **Validation in `init` under binding.** A bad `BaseAddress` bound from `appsettings.json` throws `ArgumentException` during
   binding rather than an `OptionsValidationException`; under `ValidateOnStart` both fail the boot. Phase 9 may want to wrap it
   (P5a-4).
3. **The `User-Agent` change** is visible to every server log; a consuming SDK that composes its own line through
   `ClientIdentityPolicy` is unaffected (P5a-22).
4. **5b/5c concurrency.** Four shared files (PR segmentation). Mitigation: re-derive, never resolve hunks.
5. **The knowledge CLI was not run.** The corpus files were read directly; the plan re-runs the queries (Governing documents).

---

## Rulings

Taken by this design in the absence of a human reviewer. Each lists the options considered. **Open for the lead:** P5a-2,
P5a-4, P5a-6, P5a-7, P5a-11, P5a-13, P5a-14, P5a-15, P5a-20, P5a-21, P5a-22, P5a-23. The rest are taken.

1. **P5a-1 — Row ownership.** 5a owns `CFG-8`, `CFG-9`, `CFG-12`, `CFG-13`, `CFG-15`–`CFG-36` (26) and nothing else; phase 9
   owns the other twelve `CFG` IDs; 5b and 5c divide `OBS-1`–`OBS-40`. Phase 5's exit is 66 rows. Options: taking `CFG-37`'s
   null guards into 5a because the records apply them (rejected: Phase List row 9 places the row; 5a's guards are cited by it).
2. **P5a-2 — No phase 5 segmentation design; the phase 5 boundaries are conveniences. Open for the lead.** The roadmap's
   segmentation rule asks for one (two chapters). The card names the build lists, this census and P5a-1 state the split, and no
   5a type consumes a 5b or 5c type, so no edge inverts. Options: write one before 5a's plan (rejected: it would restate the card;
   the lead may still require it).
3. **P5a-3 — Options are sealed records with `init`, nested records, collections copied at `init`, no builder** (rule A.1–A.5),
   for every options record in core, 5b's included. Options: immutable classes with `With*` methods (rejected: design §8.2 and
   styleguide 6.1/10.2 name records); `required` members for the client options (rejected: every member has a default, and
   phase 9's staging fallback is simpler without them).
4. **P5a-4 — Validation at `init` for single-property invariants only. Open for the lead.** `BaseAddress` (2b's hand-off) and
   null guards in `init`; numeric and cross-property rules left to 6a, 6b and phase 9's `IValidateOptions`. Options: no
   validation in the records (rejected: an options value that `BuildRequest` would refuse can then live for the life of a
   client); full validation including ranges (rejected: it fixes semantics 6a and 6b are about to change, and cross-property
   rules cannot run in `init`, whose order is the caller's).
5. **P5a-5 — A hand-written `ToString` for `DexpaceClientOptions`**, rendering `BaseAddress` through `UrlRedactor`.
   Options: the synthesised one (rejected: `Uri.ToString()` on a log path, design §3.5).
6. **P5a-6 — Keep the per-call `DexpaceClientOptions` overloads (closes P4c-15's 5a half). Open for the lead.** Immutable
   options make "this call, these options" coherent; four `Security` classes, `Pageable` and the AOT smoke stay unedited.
   Options: remove them (an edit to four `Security` classes for no requirement); `[Obsolete]` (the same edits through
   warnings-as-errors).
7. **P5a-7 — `BlockingWait` becomes the public `TimeProviderWaits.Sleep`, beside `DelayAsync`; both reject negative delays.
   Open for the lead on visibility.** Options: keep it internal and satisfy `CFG-15` with `TimeProvider` alone (smaller surface;
   consumer sync pollers then use `Thread.Sleep`); a dedicated `ISleeper` seam (rejected: a second time seam beside
   `TimeProvider`).
8. **P5a-8 — Clock and environment discipline as `RS0030` entries**: `Thread.Sleep`, every `Task.Delay`,
   `DateTime(Offset).Now/UtcNow`, `Environment.GetEnvironmentVariable(s)`; two sanctioned pragmas. `Stopwatch` is not banned
   (monotonic; 5c's file). Options: review only (rejected: §9.1's gate is the house mechanism); banning `Stopwatch` too
   (rejected: a cross-sub-phase edit to `InstrumentationPolicy`).
9. **P5a-9 — `LateResult`, internal, the single sanctioned `Task<T>.WaitAsync` site.** `CFG-21` ✅ by the helper and its
   exactly-once test; consumers are 6a and 8b. Options: public (rejected: no caller outside core abandons an SDK-produced task it
   does not own); no helper until 8b (rejected: §8.3 names it a 5a deliverable and the ban already points at it).
10. **P5a-10 — `CFG-19` ✅ by `await` semantics with a pin test; `CFG-20` 🚫 under `cooperative-cancellation`.** Options: an
    `Unwrap(Exception)` helper for `AggregateException` (rejected: nothing in `src/` can produce one past the bans).
11. **P5a-11 — `HttpDate` is public, hand-written, accepts a one- or two-digit day and case-insensitive zones, rejects RFC 850
    and asctime. Open for the lead.** Options: internal (rejected: consumers need a correct HTTP-date parser and the BCL offers
    none); two-digit day only, as Ruby (rejected: `RETRY-15` and design §6.1); RFC 850/asctime accepted per RFC 9110 (rejected:
    `CFG-31`, §11 item 27).
12. **P5a-12 — The three as-built date sites move to `HttpDate` in 5a**, `RetryPolicy`'s parse included (Breaking 4). Options:
    leave `RetryPolicy` to 6a (rejected: a shared parser with no consumer for a phase).
13. **P5a-13 — `ProxyOptions` shape: host and port, `ProxyType` of three, `ICredentials?` as the challenge slot, not on
    `DexpaceClientOptions`. Open for the lead on the slot's type.** Options as in position D.
14. **P5a-14 — Globs by the specification's letter (`NonBacktracking` regex, `\A…\z`, compiled at `init`), not curl's leading-dot
    suffix. Open for the lead.** Options: a hand-written wildcard matcher (equivalent; rejected only because the letter names the
    regex conversion); also honour a leading `.` as a suffix (a superset the lead may prefer for operator familiarity).
15. **P5a-15 — `FromEnvironment`'s reading: upper then lower case, the CGI `httpoxy` guard, one proxy for every target, no
    fallthrough on a malformed value, a required scheme from a closed set (`https` rejected), the port from the raw authority,
    `NO_PROXY`'s split order, `*` → `null`. Open for the lead.** Options per item in position D; the two most consequential
    alternatives are falling through to `HTTP_PROXY` on a malformed `HTTPS_PROXY` (rejected: silent re-routing) and accepting a
    scheme-less value as `http` like the platform (rejected: `CFG-24` names `scheme://`).
16. **P5a-16 — Three `FromEnvironment` overloads with no optional parameters; the environment seam public; an interim non-fatal
    `catch` around the warning until 5b's guard.** Options: an internal seam with `InternalsVisibleTo` (rejected: phase 9 needs
    it from another assembly).
17. **P5a-17 — `CFG-28` by absence of call sites plus the `Environment` ban**; the transport-construction reading is 8b's.
    Options: a reflection test that no type calls `FromEnvironment` (kept as a second line of defence, not the mechanism).
18. **P5a-18 — The `IWebProxy` adapter and its installation are 8b's**; 5a edits nothing in `Http.SystemNet`. Options:
    `ProxyOptions.ToWebProxy()` in core (rejected: transport policy).
19. **P5a-19 — `CFG-32` by `Guid.NewGuid()` with pin tests; no UUID type.**
20. **P5a-20 — `DeepValue` internal, built now with `CFG-33`/`CFG-34`'s conformance cases. Open for the lead.** Options:
    public now; ⏳ to the first array-carrying model (rejected: the MUSTs are 5a's rows).
21. **P5a-21 — Both halves of `CFG-35` on `RetryFacts`, internal; `XCUT-5`/`XCUT-6` wiring is 6a's; `RetryPolicy`'s configured
    set unchanged. Open for the lead.** Options: Ruby's status-only half (its reason does not apply); defer to 6a.
22. **P5a-22 — `BuildInfo` public; `unknown` fallback per field; header-safe tokens; the default `User-Agent` joins
    `IdentityTokens` (Breaking 3). Open for the lead.** Options: keep the one-token default; add an OS comment (both rejected in
    position E).
23. **P5a-23 — The materialisation cap is not an options member; the per-read limit goes to 7a, the SSE line cap to 7b. Open
    for the lead.** Options: a dead `DexpaceClientOptions.MaxMaterializedBytes`; a process-wide static (both rejected).
24. **P5a-24 — The context-store capacity stays a constant (4a's hand-off declined).** Options: an options member (incoherent for
    a process-wide store); an `AppContext` switch (the declined global slot by another name).
25. **P5a-25 — `CFG-12` N/A (no configuration builder type), `CFG-13` 🚫 (`configuration-via-iconfiguration`).**
26. **P5a-26 — No `Security` class is edited**; `RetryPacingOverflowTests` runs first after PRs 2 and 3.
27. **P5a-27 — Six PRs, each code and tests; PR 6 the close-out.** Options: code → tests → docs as three PRs (rejected: a red
    build between them, fact 15); one PR (rejected: five independent subjects that review better apart).
28. **P5a-28 — The interfaces assumed of 5b and 5c** are the table in
    [Cross-sub-phase interfaces](#cross-sub-phase-interfaces-assumed-of-5b-and-5c): the options-record rule binds 5b's
    `HttpLoggingOptions`; 5b owns event IDs and the emission guard; 5c measures through `TimeProvider`; both leave the
    environment and global slots alone.

---

## Deviation Ledger

Every entry is a `P5a-n` ruling above that departs from a requirement's letter, reads an ambiguous clause, or decides a
judgement call the lead may reverse. **Open** means the lead has not ruled; **taken** means the design argues it from an
existing design section and it lands as built unless reversed.

| ID | Decision | Touches | Kind | State | Route |
|---|---|---|---|---|---|
| P5a-2 | No phase 5 segmentation design; 5a/5b/5c are conveniences | roadmap segmentation rule | process | **open** | roadmap status note (PR 6) |
| P5a-4 | Single-property validation at `init`; ranges and cross-property rules to 6a/6b/9 | `CFG-8` (no clause), phase 9's `CFG-37` | judgement | **open** | §8.2 dated correction (PR 6) |
| P5a-6 | Per-call `DexpaceClientOptions` overloads kept | `PIPE-17` | judgement (closes P4c-15's 5a half) | **open** | `HttpPipeline` remarks; §5.3 note (PR 6) |
| P5a-7 | The sync sleep is the public `TimeProviderWaits.Sleep` | `CFG-15`, `CFG-17` | surface judgement | **open** | §8.3 and §6.1 dated corrections (PR 6) |
| P5a-8 | `RS0030` bans for clock, delay and environment reads | `CFG-15`, `CFG-16`, `CFG-28` | mechanism | taken | `BannedSymbols.txt`; §9.1 note (PR 6) |
| P5a-9 | `LateResult` is the cooperative form of `CFG-21` and the only `WaitAsync` site | `CFG-21`, `ASYNC-5`, `TRANSPORT-9` | mechanism (§10 entry 8 already sanctions the form) | taken | §8.3 dated correction (PR 6) |
| P5a-10 | `CFG-20` retired; `CFG-19` free on `await` | `CFG-19`, `CFG-20` | existing §10 entry 8 | taken | checklist cites §10 `cooperative-cancellation` |
| P5a-11 | `HttpDate` public; one- or two-digit day under `CFG-31`; RFC 850/asctime rejected | `CFG-30`, `CFG-31`, `RETRY-15` | reading + surface judgement | **open** | new §11 item (PR 6); §11 item 27 stands |
| P5a-13 | Host/port for the socket address; `ICredentials?` as the challenge slot; not on the client options | `CFG-22`, `TRANSPORT-30` | reading | **open** | §8.2 dated correction (PR 6) |
| P5a-14 | Spec globs, not curl's leading-dot suffix | `CFG-23` | reading against platform convention | **open** | new §11 item (PR 6) |
| P5a-15 | Upper then lower case, CGI guard, one proxy for all targets, no fallthrough, closed scheme set, raw-authority port | `CFG-24`–`CFG-27` | reading (superset in the names; divergence from the platform §8.2 already records) | **open** | §10 entry 25 dated correction (PR 6) |
| P5a-17 | `CFG-28` by absence plus the ban; transport-construction call is 8b's reading | `CFG-28` | reading | taken (8b decides its half) | 8b's design |
| P5a-19 | `CFG-32` by the OS CSPRNG, stronger than required | `CFG-32` | §8.2 already argues it | taken | checklist cites §8.2 |
| P5a-20 | `DeepValue` internal | `CFG-33`, `CFG-34` | surface judgement | **open** | §8.2 dated correction (PR 6) |
| P5a-21 | Both classifier halves in 5a, internal; wiring 6a | `CFG-35`, `XCUT-5`, `XCUT-6` | ownership judgement | **open** | 6a's design |
| P5a-22 | `unknown` per field; header-safe tokens; two-token default `User-Agent` | `CFG-36`, `RECOV-33` | behaviour judgement | **open** | §8.2 dated correction (PR 6) |
| P5a-23 | No options member for the materialisation cap; per-read limit to 7a, SSE cap to 7b | 3a hand-off, design §3.1 | ownership judgement | **open** | §3.1 dated correction; 3a checklist (PR 6) |
| P5a-24 | Context-store capacity stays constant | 4a hand-off | ownership | taken | 4a checklist (PR 6) |
| P5a-25 | `CFG-12` N/A without a builder; `CFG-13` 🚫 | `CFG-12`, `CFG-13` | reading + existing §10 entry 25 | taken | new §11 item for `CFG-12` (PR 6) |
| P5a-28 | The interfaces assumed of 5b and 5c | — | cross-sub-phase contract | **open** until 5b's and 5c's designs confirm | each sibling's Prerequisite section |

No ruling leaves a MUST's letter unmet beyond what §10 entries 8 and 25 already record, so no new §10 entry is opened; P5a-15
extends entry 25 by dated correction, and P5a-11, P5a-14 and P5a-25 propose §11 items.
