# Phase 11 — Deviation Reconciliation: Design

**Status:** Draft, for review. Written 2026-10-19 against `main` at `eb9b758` (phases 0 to 10 merged). The phase has no issue yet;
the plan names the branch `<issue>-phase-11-deviation-reconciliation` and the lead files the issue. Brainstormed without a human in
the loop, at the lead's instruction ("I want all the deviations reconciled. Let the subagent do the decision making. I understand some
things are better off not following the rules, but there should be a really good reason if it happens."). Every judgement the
brainstorming skill would have put to the lead is taken here as a numbered decision (`P11-n`, see [Decisions](#decisions)) with its
rationale. The scope authority is the roadmap's Phase 11 card (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`), every
Phase Status Note bullet addressed to 11 (8a, 8b, 9, 10), and `docs/first-release.md`. The format follows the phase 10 design
(`docs/work/mvp/phase10/2026-10-13-phase10-cross-cutting-invariants-design.md`).

**What this document is.** The phase design for 11, which has no sub-phases (P11-1). It holds:

- the audit method, and the counting rule for clauses unmet on a stated domain (P11-2);
- a source-derived verdict for every design §10 entry (33), every §11 item (68), and the §12 handling;
- the "Retained deviations and why" table: every deviation that survives the phase, with the reason class and what breaks if it were
  conformed;
- the shape of `docs/deviations.md`, the ledger-audit gate and its negative scans;
- the spec amendment set (recorded, not applied to the frozen spec, P11-4);
- the disposition of every inbound bullet, the `docs/first-release.md` walk, the as-built line refresh, and the reproducible-pack finding;
- the decisions.

**What this document is not.** It is not the plan and not the checklist, and it edits nothing. Every correction it owes is listed in
[Corrections owed at close-out](#corrections-owed-at-close-out) and executed by the plan's last pull request.

**The headline.** Of 33 §10 entries, **19 are confirmed, 10 narrowed and 4 contradicted** by the source; of 68 §11 items, **49 are
confirmed, 13 narrowed and 6 contradicted**. Eight §10 entries are **repaired** (the deviation goes away), 24 are **retained** with a
stated reason, and 1 is a documentation correction. The count of MUST clauses unmet on a stated domain falls from **seven to three**
(`AUTH-15` on a FIPS host, `PAGE-21`'s unreserved residual, `TRANSPORT-14`'s name clause), inside the roadmap's parity target of five.
*Amended 2026-10-21 (review).* The count is **four**, not three: `CFG-2` (§10 entry 25, P11-17) is repaired only for a section of the container's registered `IConfiguration`, because no public API reaches the root of an `IConfigurationSection`, and for a section of any other configuration the outcome is not delivered, which P11-2 counts. The tables below stand as written; `docs/deviations.md` and the checklist carry the corrected count.
The audit also found **twelve defects no document recorded** (among them a synchronous throw from an async bridge, a response leak in
the documented cancel-without-interruption mode, two catch-alls that swallow `OutOfMemoryException`, a span write on non-recording and
foreign activities, and a `NullReferenceException` in a public constructor), each a TDD task.

---

## Prerequisites

| Predecessor | Kind | State at `eb9b758` |
|---|---|---|
| **10** — the conformance suites, `APPENDIX_B.md`, `scripts/ci/requirement-coverage.cs`, the `Gates/` tests, the cross-path reproducible pack | **dependency** (card "Entry") | Merged (#102). The `reproducible packages` job was red at merge and the PR was admin-merged (see [The reproducible-pack finding](#the-reproducible-pack-finding)). |
| **8b** — the two permanent waivers (§10 entries 32, 33), `OutboundHeaderMapper`, `CreatePrimaryHandler` | **dependency** (entries 32, 33 are re-audited) | Merged. |
| **9** — the DI reader, `ValidateOnStart`, the empty-value shadow | **dependency** (entries 25, 26) | Merged. |
| **0–7** | **dependency** (the code every other entry describes) | Merged. |
| **12** | **dependent** | Takes the one-time public-surface list this phase writes into `docs/first-release.md`. |

## Governing documents, and the phase-start queries

Read for this design: roadmap constraints 1–10, "How Phases Get Executed", the Phase 10, 11 and 12 cards, the Maturity Definition and
every Phase Status Note; design §10 (all 33 entries and every dated correction), §11 (all 68 items), §12, and every **As built** line in
§§2–9; `docs/first-release.md`; `docs/README.md`; the styleguide overlay (`docs/styleguide/README.md`) and
`docs/styleguide/csharp-aspnetcore/01-host-and-configuration.md` 1.4; appendix C rows for every ID a §10 entry touches; the phase 10
design, plan and checklist; the phase 8b and 9 checklists; `scripts/ci/*`; `.github/workflows/ci.yml`; `BannedSymbols.txt`; the
`tests/*/Architecture/` and `tests/Dexpace.Sdk.Conformance.Tests/Gates/` classes; the source each verdict below cites.

Queries run (2026-10-19): `scripts/knowledge --origin note --brief` (41 notes; none contradicts a verdict here, two are re-derived:
`configuration/…` "An empty environment value is absent" is reversed by P11-17, and the knowledge note on the HTAB edge is settled by
P11-14); `--section conflicts --brief` (the kept styleguide conflicts: `I` prefix, `Async` suffix, `CA1062`, Shouldly, `LangVersion`,
the `LoggerMessage` generator, `Outcome`, the configuration binder, `AddDexpaceClient`'s return type, Stryker); `--prefix-info` for
`CFG`, `TRANSPORT`, `OBS`, `PAGE`, `AUTH`; `--gaps XCUT,NFR` (no gaps).

**Siblings.** The roadmap names `ruby-sdk/tools/ledger_audit.rb` and Ruby's phase-10 reconciliation design. Neither exists: `ruby-sdk`
at its tip (`90075b1`) is a documents-only tree at phase 9, whose `docs/deviations.md` reads `design only — not yet built` for all 19
rows and has no tool. Node's phase 10 (`nodejs-sdk/docs/work/mvp/phase10/2026-07-28-phase10-deviation-reconciliation-design.md`, and its
`docs/deviations.md`) is the only executed reconciliation, and its corrected scope note is the method's real source: *audit against
as-built source, not the phase specs; correctable items are fixed, not listed; `file:line` per claim*. Node built no gate ("There is no
automated test for this phase"). So the **method** is ported from Node, the **columns** from Ruby's `docs/deviations.md`
(`# | Title | IDs touched | Status`), and the **gate** is designed here (P11-5).

## Facts the design rests on

Each was run for this design on .NET SDK 10.0.401, Linux x64, against `eb9b758`. Probe sources are kept out of the tree.

| # | Fact | Consequence |
|---|---|---|
| V1 | `dotnet build Dexpace.Sdk.sln -c Release`: 0 warnings. `dotnet test --solution`: 7356 tests, 7329 passed, 27 skipped, 0 failed. | The baseline is green locally. |
| V2 | `scripts/ci/reproducible-pack.sh` exits 0 in four layouts: output outside the repository; output at `artifacts/reproducible` inside it (CI's layout); a `git clone --depth 1` with an `https` origin (CI's checkout); and `SOURCE_DATE_EPOCH` 30 days in the future. Five `.nupkg` byte-identical; runs 1–3 even share hashes. NuGet writes `SOURCE_DATE_EPOCH` as a UTC DOS time. | The CI failure is not reproduced locally (P11-22). |
| V3 | `requirement-coverage.cs --report`: MUST rows N/A 21, ⏳ 16, ✅ 490, 🚫 5; `--check` exits 0 and never fails on ⏳. | The 16 ⏳ MUST rows are 15 closed elsewhere plus `NFR-17`; the "every MUST dispositioned" line is not gated. |
| V4 | A `TaskScheduler` whose `QueueTask` throws makes `AsAsync(scheduler).ExecuteAsync` throw `TaskSchedulerException` synchronously (`HttpClientExtensions.cs:149`); a pre-cancelled token does the same. | `ASYNC-2` is unmet (F-A2). |
| V5 | The documented cancel-without-interruption mode (`task.WaitAsync(token)` on the bridge's task) leaked an undisposed response in 44 of 20 000 runs. | §11 item 43 is contradicted; `SEAM-30` is unmet in that mode. |
| V6 | A `SocketsHttpHandler.PlaintextStreamFilter` that turns "end of stream while a request awaits its first response byte" into an `IOException` (idle closes still read 0) makes body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE` arrive **once**, on both faces; the failure still maps to `ServiceRequestException`. Without it each arrives four times. | §10 entry 33 is repairable (P11-12). |
| V7 | A `PlaintextStreamFilter` that drops a header line with a malformed name before the runtime's parser turns "Received an invalid header name" into a 200 with the other headers. | §10 entry 32's "only workaround is replacing the parser" is contradicted; P11-11 still retains it. |
| V8 | `System.Formats.Tar.TarWriter.WriteEntry(path, …)` classifies a path without opening a special file: type 0 regular, 3 character device (`/dev/null`, `/dev/zero`), 6 FIFO (does not block), 5 directory; a Unix socket throws `IOException`. NativeAOT-published with no warning; the assembly is in `Microsoft.NETCore.App.Ref`. A completed write on a size-0 path leaks no handle. | §10 entry 31 is repairable (P11-13). |
| V9 | `new Uri(s, new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true })` keeps `%7E%41`, and `HttpClient` sends it verbatim, **but** sends the fragment on the wire and writes a CR/LF in the query raw into the request line; `GetComponents(Path \| Query)` throws `InvalidOperationException`. `Request` accepts such a `Uri` today. | Entry 18 stays (P11-15); `Request` must reject such a `Uri` (task 3.4). |
| V10 | 8 000 000 lines of `data:x\n` with no blank line (56 MB on the wire) are retained as one 323 MB event. | §10 entry 20's residual R1 is an unbounded allocation (task 5.6). |
| V11 | With the configuration root, walking `IConfigurationRoot.Providers` from the top, skipping a blank `TryGet`, returns the lower provider's value under an empty environment variable; a chained provider exposes its own root. | §11 item 68's "cannot be" is contradicted (P11-17). |
| V12 | Turning `CA1062` on for every `src/` project yields **one** finding: `HttpResponseException(Response, string?)` dereferences `response` in its `base(...)` call, so `null` throws `NullReferenceException`. | Entry 28 is retired (P11-20). |
| V13 | `ActivityTraceId.CreateRandom` draws two `RandomNumberGenerator` values with no zero test (IL read). | `OBS-27`'s zero-draw clause is shown unmet by the runtime, not merely "not shown". |
| V14 | On an `Activity` whose `IsAllDataRequested` is `false`, `AddException` and `AddEvent` still record. `Disposal.cs:105-110` calls `Activity.Current.AddException` unguarded. | §10 entry 23's 5c correction is contradicted at one site. |
| V15 | `Headers.Builder.AddInbound` trims SP and HTAB at either end of a name (`HeaderSyntax.cs:25`); `HttpHeaders.TryAddWithoutValidation` refuses such names, so the reference transport never reaches it. | `HTTP-19` and `XCUT-18` are unmet for third-party transports (P11-14). |
| V16 | `CA1031` is silent on `catch (Exception ex) when (true)` and on a rethrowing catch. Two catch-alls swallow `OutOfMemoryException` (`UrlRedactor.cs:99`, `:166`). | The fatal filter needs its own gate (task 1.4). |
| V17 | `new HttpResponseException(response200)` builds "The server returned an error response: OK(200)." | `XCUT-8` is unmet on the one public mapping route (P11-9). |
| V18 | `Dexpace.Sdk.Core`'s runtime closure is `Microsoft.Extensions.Logging.Abstractions` 10.0.12 → `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12, nothing else; `dependency-audit.cs:119` allows `Closure(Facade)`, so a future facade dependency would pass. | Entry 1's measurement is stale; the audit is widened to a fixed set (task 1.3). |

---

## The audit method

1. **Claim from the document, evidence from the source.** For each §10 entry and §11 item the claim is read with every dated
   correction applied, and then re-derived from `src/`, `tests/` and a probe where the claim is a runtime fact. A document is never
   evidence. Six auditors worked in parallel over disjoint entry sets; every verdict below was read back against the cited lines.
2. **Three verdicts.** `confirmed` (the code does what the claim says), `narrowed` (true, but the claim is broader or narrower than the
   code; the table says how), `contradicted` (the code does otherwise).
3. **Three dispositions, conformance first.** `repair` (the code changes to meet the specification, a TDD task whose test fails
   first), `retain` (the deviation stays, with a reason from a closed set — a .NET **platform** constraint, a **security** property,
   **AOT/trim**, a **measured cost**, a **styleguide** mandate, or a **spec sanction** — and a sentence naming what breaks if it were
   conformed), `doc-correct` (the code is right, the claim is wrong). "Already built", "easier" and "a lot of work" are not reasons, and
   no row below uses them. An entry may split (retain one clause, repair another).
4. **Absence is scanned, and the scan becomes a gate.** Every claim that something does not exist is backed by a negative scan with a
   positive probe that proves the scan is not vacuous; the scans are rows of `docs/deviations.md` and run in the ledger-audit gate
   (P11-6).
5. **The counting rule (P11-2).** A clause counts against the roadmap's parity target ("at most 5 unsatisfied clauses, each argued")
   when the **observable outcome** the clause protects is not delivered on some stated domain. A **mechanism substitution** — the letter
   names a mechanism the port replaces, and the protected outcome is delivered or strengthened (P9 of the porting method) — is retained
   and argued in the table below but is not counted, and each one names the outcome it keeps. A clause the specification sanctions
   changing (`REDIR-25`, `BODY-8`, `SSE-19`) is never counted. A clause the specification makes unsatisfiable (`AUTH-16`/`AUTH-17`/
   `AUTH-22`, §11 item 62) is an ambiguity, not a deviation.

## The verdict table: design §10

Columns: entry; the IDs it touches; the claim (as amended); what the source shows; verdict; disposition. Paths are under `src/` unless
they start with `tests/` or a root file. "+repair" and "+doc" are attached actions on an entry whose primary disposition is different.

| # | IDs | Claim | What the source shows | Verdict | Disposition |
|---|---|---|---|---|---|
| 1 | SEAM-1, NFR-1 | Core takes `Logging.Abstractions` at run time; on net8.0 / 9.0.5 it also brings `DI.Abstractions` and a `DiagnosticSource` copy; a deps.json allow-list test is the audit | `Dexpace.Sdk.Core/packages.lock.json`: the facade 10.0.12 → `DI.Abstractions` 10.0.12 only, no `DiagnosticSource` (V18); the audit is `scripts/ci/dependency-audit.cs:119`, a CI script allowing `Closure(Facade)`, not a test; `Seam1ArchitectureTests.cs:12-15` defers to it | narrowed | **retain** (+repair: the allowance becomes the fixed set {facade, `DI.Abstractions`} with a self-test, task 1.3; +doc: the measurement) |
| 2 | NFR-2 | The DI package's one root `Microsoft.Extensions.Http` and its 13-package band-matched closure count as one library | `Dexpace.Sdk.Extensions.DependencyInjection/packages.lock.json` (13 packages, 10.0.12); `dependency-audit.cs:50` | confirmed | **retain** |
| 3 | SEAM-3–10, IO-30–36, IO-39, XCUT-23 | The byte-stream provider seam retires; no `Source`/`Sink`/`Buffer` vocabulary; "the net8.0 floor" | every `Core/IO/*.cs` type is `internal` (`CapturedBytes.cs:22`, `StreamCopy.cs:23`, `TeeStream.cs:31`); no `Dexpace.Sdk.Core.IO` line in `PublicAPI.Unshipped.txt`; scan N1 empty | confirmed | **retain** (+doc: net10.0, decision D1) |
| 4 | IO-1, IO-2, IO-11, IO-15–18, BODY-10, BODY-25 | End of stream is a zero read; core never issues a zero-count read; truncation detected on a known length | every read requests ≥ 1 byte (`IO/StreamCopy.cs:101,170`, `IO/Utf8LineReader.cs:107`, `Http/Response/LoggingResponseBody.cs:301`, `PrefixedReadStream.cs:65-68`); `StreamCopy.cs:103-105` throws `EndOfStreamException`; `tests/…/IO/ZeroCountReadRuleTests.cs:46-133` | confirmed | **retain** |
| 5 | BODY-8, IO-6 | A request body closes what it opened; `ResponseBody.FromStream` takes ownership | `Http/Request/RequestBody.cs:397-498` (never disposes the source); `FileRequestBody.cs:78,93,100-108`; `LoggingRequestBody.cs:92` (`leaveOpen`); `ResponseBody.cs:351-369` | confirmed | **retain** |
| 6 | BODY-14, HTTP-41 | A second open throws `StreamConsumedException` instead of returning the same handle | `Http/Response/ResponseBody.cs:285-291,333-348`; `Http.SystemNet/HttpResponseMessageBody.cs:42-75` | confirmed | **repair** (P11-8, task 3.1) |
| 7 | TRANSPORT-20, XCUT-4, RETRY-2, RETRY-4, RECOV-17 | Transport failures are `SdkException`s chaining the native exception; single inheritance forbids `IOException` | `Errors/TransportExceptions.cs:14,70` derive from `SdkException`; `Http.SystemNet/SendFailures.cs:75`; no appendix-C row requires a single SDK root, and `TRANSPORT-20` says "MUST be a subtype of the platform IOException" | confirmed (its premise is self-imposed) | **repair** (P11-7, task 2.1) |
| 8 | TRANSPORT-3, XCUT-1, ASYNC-3, ASYNC-4, ASYNC-14, SEAM-14, SEAM-18, PIPE-33, PIPE-34, XCUT-13, CFG-20, CFG-21 | Cancellation is a cooperative token; "transports rethrow `OperationCanceledException` untouched"; `AsAsync` takes a scheduler with no default; `LateResult` serves `CFG-21` | `Client/HttpClientExtensions.cs:97-101,161-170`; `BannedSymbols.txt:10`; `SendFailures.cs:59,67-72,81-86` **replaces** the exception when a deadline is armed or the cancel is internal; `Internal/LateResult.cs` has no caller | narrowed | **retain** (+repair `ASYNC-2`, task 2.3; +repair item 43, task 2.4; +doc "untouched", `LateResult`) |
| 9 | SEAM-5–10, XCUT-23 | No registry; DI single-registration validation; "`TryAdd` gives `SEAM-6`" | `Extensions.DependencyInjection/ServiceSlot.cs:27-36` (identity check, throws at `UseTransport`/`UseSerde`); `DexpaceClientValidator.cs:36-57`; `TryAdd` is used nowhere; scan N2 empty | narrowed | **retain** (+doc: `ServiceSlot`, not `TryAdd`) |
| 10 | SEAM-29, HTTP-2, HTTP-3, HTTP-4 | No builder objects beyond the multimap builders, no `IBuilder<T>`; a missing value throws `ArgumentNullException` naming the field | public `*Builder` types are four: `Headers.Builder`, `Query.Builder`, `Pipeline/PipelineBuilder.cs:36`, `Extensions.DependencyInjection/DexpaceClientBuilder.cs:22`; validating `init` accessors throw with `ParamName = "value"` (`Operations/OperationDescriptor.cs:43` and peers) | narrowed | **retain** (+repair `ParamName`, task 2.5; +doc: "no **model** builders"; scan N5 allow-lists the four) |
| 11 | HTTP-2, SEAM-29, XCUT-15 | Reflection and open bodies can bypass validation; every transport re-checks headers and the method token at the wire | `Http.SystemNet/OutboundHeaderMapper.cs:95,196-210`; the method token is checked by `new HttpMethod(...)` at `:75` (`FormatException`); `TryAddWithoutValidation` writes CR/LF raw (verified) | confirmed | **retain** |
| 12 | RECOV-2, RECOV-8, RETRY-25, PIPE-30 | Catches filter `!ExceptionFacts.IsFatal`; amendment: "exactly four sites" | six recovery sites (`Recovery/RecoveryDispatcher.cs:145,174,195,235`, `ResponseRecoveryChain.cs:127,148`), 53 filtered catch-alls in total; **two catch-alls swallow OOM** (`Diagnostics/UrlRedactor.cs:99,166`, V16) | contradicted | **doc-correct** (+repair: the two sites and a fatal-filter gate, task 1.4) |
| 13 | RECOV-12, RETRY-34, PAGE-13, SSE-29, SSE-36 | The suppressed trail lives on `SdkException.Suppressed` or under a `Data` key on a foreign exception | `Errors/ExceptionTrail.cs:19,31,46-59,81-84`; `SdkException.cs:59-71,85-112`; `tests/…/ExceptionTrailTests.cs:97` | confirmed | **retain** |
| 14 | PIPE-32, REDIR-25 | Both standard pipelines follow redirects | `Pipeline/PipelineBuilder.cs:217-223`; `Http.SystemNet/SystemNetHttpClient.cs:510` (`AllowAutoRedirect = false`); `tests/…/ResiliencePresetTests.cs:85` | confirmed | **retain** |
| 15 | REDIR-11, AUTH-29, REDIR-8, XCUT-17 | No cross-origin marker; redirect and auth compare against the immutable seed origin | `Pipeline/PipelineContext.cs:116`; `Pipeline/Policies/Redirect/RedirectChain.cs:25`; `AuthorizationPolicy.cs:156,218`; scan N7 (one `PipelinePropertyKey`, the idempotency key) | confirmed | **retain** |
| 16 | AUTH-15, AUTH-16 | Digest MD5 only where the platform crypto provides it; probed once | `Auth/Md5Availability.cs:20,26-42`; `DigestChallengeHandler.cs:65,94`; the mapping has never run on a real FIPS host, and an unexpected exception type would be cached by the `Lazy` and fail every construction | confirmed | **retain** (+repair: the probe treats any non-fatal exception as refused, task 5.8; +doc: "honours the host's crypto policy") |
| 17 | PAGE-1, PAGE-3, PAGE-12, PAGE-15, PAGE-34 | Pages are materialized immutable values; no page owns a response | `Pagination/PageStep.cs:52,56,81,114`; `Page.cs:31`; but `Page.cs:44` stores the caller's list as given, so a `FromFetchers` page mutates after it is yielded (`FetcherPageable.cs:37`) | narrowed | **retain** (+repair: the constructor copies, task 3.5; +doc: `PAGE-1`/`-3`/`-34` letters named) |
| 18 | PAGE-21, PAGE-24 | The splice is verbatim relative to `System.Uri`'s canonical form; residual is unreserved characters | `Http/Request/Request.cs:75,100`; `Pagination/QuerySplice.cs:111-112` **drops empty `&` segments** (`RemoveEmptyEntries`), `:126-143`; `Uri` also escapes `\| { } ^` and a stray `%` (V9) | narrowed | **retain** the unreserved residual (+repair: empty segments, task 3.6; +repair: `Request` rejects a non-canonicalizing `Uri`, task 3.4) |
| 19 | PAGE-29, PAGE-30 | One pull-based engine; "the consumer's `await foreach` owns its context" | `Pagination/StrategyPageable.cs:23-49`; no scheduler in any factory (`PublicAPI.Unshipped.txt:975-977`); without a `SynchronizationContext` the consumer resumes inline on the completing thread | narrowed | **retain** (+doc: the inline default and `await Task.Yield()`) |
| 20 | SSE-19 | Lines over a 1 MiB cap fail; residual R1 (per-event accumulation) left open | `ServerSentEvents/ServerSentEventReader.cs:49,85-88`; `IO/Utf8LineReader.cs:152-155,205-208`; `EventAccumulator.cs:71` appends without bound (V10) | confirmed | **repair** R1 (P11-19, task 5.6) |
| 21 | SERDE-14 | `Tristate<T>` is an invariant struct; non-generic markers cover covariance's use | `Serialization/TristateOfT.cs:29,47,84,105,110`; `Tristate.cs:20,23`; `tests/…/TristateTests.cs:169-172` | confirmed | **retain** |
| 22 | OBS-1, OBS-3, OBS-4, OBS-5, OBS-8, OBS-9, OBS-40 | No log-event object; a null renders `(null)` in text and stays null in state | 16 `LoggerMessage.Define` sites, 0 `[LoggerMessage]`; `Diagnostics/HttpLogEmitter.cs:74,222,247,265`; `tests/…/DisabledPathAllocationTests.cs:49-72`; 6 `Define` sites use plain names, so the generator departure's reason does not cover them | narrowed | **retain** the mechanism (+repair `OBS-3` literal, task 5.2; +repair: the 6 plain-name sites move to the generator, task 5.3; +doc: the generator departure joins the styleguide list) |
| 23 | OBS-25, OBS-21, OBS-23, OBS-10, OBS-28 | `Activity` tracing, `null` no-op; every SDK write guarded by `IsAllDataRequested` | `Execution/InstrumentationContext.cs:40-41,95-101`; `OperationTelemetry.cs:43,…,174`; **`Internal/Disposal.cs:105-110` writes unguarded to `Activity.Current`** (V14) | narrowed | **retain** (+repair, task 1.5) |
| 24 | OBS-27 | Only W3C ids and the sentinel; no Datadog flavour; zero draw "not shown" to be coerced | `BannedSymbols.txt:130`; `tests/…/TraceIdTests.cs:23-81`; the runtime provably does not coerce (V13); a Datadog flavour needs no process hook | confirmed | **repair** (P11-18, task 5.1) |
| 25 | CFG-1–4, CFG-10, CFG-13, CFG-14, CFG-24 | Layered lookup in the DI package; an empty value is absent and silently shadows a lower provider; no global slot | `Extensions.DependencyInjection/ConfigSection.cs:25-35` reads the merged view; the setup holds the `IServiceProvider` (`DexpaceClientStateSetup.cs:14,24-27`); the provider walk works (V11); scan N10 (no global slot) empty | narrowed | **repair** `CFG-2` (P11-17, task 5.4); **retain** the rest (no property tier, no raw accessor, no global slot) |
| 26 | CFG-5, CFG-6, CFG-7, OBS-35 | Unparseable configuration fails at startup instead of falling back | the grammars never throw and leave the record default (`ScalarGrammar.cs:18,62-103`, `DurationGrammar.cs:25-46`, `RecordBinder.cs:44-51`); a separate validator fails `ValidateOnStart` (`DexpaceClientRegistrations.cs:27`) | confirmed | **retain**, reclassified (P11-10): the accessor letter is met; the boot failure is a stricter host rule |
| 27 | — (styleguide 2.6, 2.7) | Public API keeps `I` and `Async` | 13 public interfaces carry `I`; the only suffix-less `Task` member is the framework's `DisposeAsyncCore` | confirmed | **retain** |
| 28 | — (styleguide 1.3, `CA1062`) | `CA1062` is dialled to `none` | `.editorconfig:138-141`; turning it on finds one site, a real bug (V12) | confirmed (rationale contradicted) | **repair** (P11-20, task 1.6): the departure retires |
| 29 | HTTP-47, OBS-11, XCUT-19 | A URL error carries the redacted input | `Http/Request/Request.cs:60,102,209-212` (probe: `ftp://h/x` echoed, `https://user:secret@h:bad/` → `[malformed url]`) | confirmed | **retain** |
| 30 | HTTP-6 | An absent response body is an empty replayable body | `Http/Response/Response.cs:67`; `tests/…/ResponseTests.cs:142` | confirmed | **retain** (a mechanism, not a departure) |
| 31 | BODY-11, HTTP-40 | The shared framework cannot tell a FIFO or device from a regular file, so special files upload as empty | `Http/Request/RequestBody.cs:283-308`; `FileRequestBody.cs:72-75,88-91`; `TarWriter` tells them apart, AOT-clean (V8) | contradicted | **repair** (P11-13, task 3.3) |
| 32 | TRANSPORT-14 | A malformed inbound header name fails the whole response; "the only workaround is replacing the parser" | `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs:139-142`; a `PlaintextStreamFilter` can drop the line (V7) | contradicted (its reason) | **retain**, with a corrected reason (P11-11) |
| 33 | TRANSPORT-2 | No switch exists, so `GET`/`HEAD`/`OPTIONS`/`DELETE`/`TRACE` are re-sent natively | `Http.SystemNet/OutboundHeaderMapper.cs:116-147`; `tests/…/BodylessRetryWireTests.cs:44-86`; a 20-line filter makes all five arrive once (V6) | contradicted | **repair** (P11-12, task 4.1) |

**Tally (§10).** Verdicts: **19 confirmed** (2, 3, 4, 5, 6, 7, 11, 13, 14, 15, 16, 20, 21, 24, 26, 27, 28, 29, 30), **10 narrowed** (1, 8, 9, 10,
17, 18, 19, 22, 23, 25), **4 contradicted** (12, 31, 32, 33). Primary dispositions: **8 repair** (6, 7, 20, 24, 25, 28, 31, 33), **24 retain**,
**1 doc-correct** (12). Attached repairs on retained entries: 1, 8 (two), 10, 12, 16, 17, 18 (two), 22 (two), 23. Every entry gets a dated
correction (close-out).

## The verdict table: design §11

| Item | IDs | Claim (short) | What the source shows | Verdict | Disposition |
|---|---|---|---|---|---|
| 1 | CFG-15, RETRY-26 | Waits hold no thread; long waits chunked | `Configuration/TimeProviderWaits.cs:43-55,91-122` | confirmed | retain |
| 2 | NFR-11, SEAM-17 | No framework leak; a real sync path | `tests/…/Nfr11PublicSurfaceArchitectureTests.cs:28`; two documented sync-over-async defaults remain (`Pipeline/HttpPipelinePolicy.cs:67`, `Auth/TokenCredential.cs:48`) | narrowed | doc-correct |
| 3 | SEAM-2 | Retiring a seam reads `SEAM-2` as constraining a retained concern | as entry 3 | confirmed | retain |
| 4 | CFG-1 | appsettings is the third tier | the DI package reads the caller's section; tier order is the host's provider order; the `Configure` transforms are `CFG-1`'s tier (1) (`DexpaceClientRegistration.cs:32`) | narrowed | doc-correct |
| 5 | SEAM-30 | Orphan close tested with a fake transport | `tests/…/SyncToAsyncBridgeTests.cs:221-251` (only a token signalled before `Execute` returns) | confirmed | retain |
| 6 | HTTP-2, SEAM-29 | Constructor privacy is enforced | `Http/Common/Method.cs:31`; `ModelConstructionArchitectureTests.cs:49` | confirmed | retain |
| 7 | BODY-3 | `Interlocked` compare-and-set | `RequestBody.cs:92-100,437-445` | confirmed | retain |
| 8 | XCUT-23 | DI resolves the last registration silently; the package detects duplicates | `DexpaceClientValidator.cs:34-57`; `SingleRegistrationTests.cs:125` | confirmed | retain |
| 9 | — | Appendix B is a weaker claim; worst-of view | `Conformance/Reporting/ConformanceReport.cs:26-27,61-68,206` | confirmed | retain |
| 10 | RETRY-38, RETRY-45 | `RETRY-38` deferred | built: `Configuration/RetryOptions.cs:182-199`, `Resilience/RetryEngine.cs:111,132-133`; side finding: the attempt header is appended (`Headers.cs:126-130`) and `"Idempotency-Key"` is accepted as its name | contradicted | doc-correct (+repair, task 5.7) |
| 11 | CTX-16, CTX-20 | Embedded MUSTs of shipped SHOULD features are met | `InstrumentationContextTests.cs:193`; `PromotionChainTests.cs:137-165` | confirmed | retain |
| 12 | BODY-8, RECOV-14, RETRY-34, AUTH-31 | One `bool async` implementation per policy | control flow is one core; the auth credential hooks are sync/async twins (`BearerTokenAuthPolicy.cs:86-110`) | narrowed | doc-correct |
| 13 | — | Citation hygiene | no code cites "HTTP-16-body" | confirmed | retain |
| 14 | SEAM-6, SEAM-8 | `TryAdd` gives idempotence | `ServiceSlot` does | narrowed | doc-correct |
| 15 | CFG-34, SERDE-11 | Deep equality compares zero sign explicitly | `Internal/DeepValue.cs:39,74-75,103-104`, with **no production caller** | narrowed | doc-correct |
| 16 | PIPE-32 | Reversed | as entry 14 | confirmed | retain |
| 17 | SSE preamble | Deviations replicated; runtime `SseParser` violates `SSE-6/8/10/13/14/16` | probe confirms; `SSE-16` breaks on the buffer clause only; scan N13 (no `SseParser` in `src/`) | confirmed | retain (+doc: the `SSE-16` nuance) |
| 18 | SERDE-26, TRANSPORT-18 | Options copied; bounded re-send capture | `Http.SystemNet/ResendCapture.cs:74-154`; `SystemNetHttpClientOptions.cs:108-123`; the refusal chain has an `HttpRequestException` layer the text omits, and "a limit of 0 disables the capture" is wrong (`ResendCaptureTests.cs:168`) | confirmed | retain (+doc: two wordings) |
| 19 | RETRY-28 | Two stacks, neither sanction invoked | `RetryPolicy.cs:142,195`; `RetryRecovery.cs:69`; `RecoveryDispatcher.cs:140` | confirmed | retain |
| 20 | RECOV-31, RETRY-38 | One deferred feature | built (as item 10) | contradicted | doc-correct |
| 21 | ASYNC-21 | No reactive adapter; property on the pull path | scan N12 empty; `ServerSentEventReader.cs:76-100` | confirmed | retain |
| 22 | RETRY-6, HTTP-9 | TRACE dropped; `IsSafe`/`IsIdempotent` not public | `Resilience/RetryFacts.cs:28-29`; not in `PublicAPI.Unshipped.txt` | confirmed | retain |
| 23 | XCUT-4, RETRY-2 | The I/O family for classification | `RetryFacts.cs:103-104` | confirmed | retain (the family reading stays; the type clause is repaired under entry 7) |
| 24 | — | One retry layer per call path | nothing the SDK adds retries, but the SDK-created `SocketsHttpHandler` does (entry 33) | narrowed | doc-correct (moot after task 4.1) |
| 25 | XCUT-16, AUTH-28 | No loopback exemption | `AuthorizationPolicy.cs:177,252-257`; `Security/AuthHttpsGuardTests.cs:36-38` | confirmed | retain (security) |
| 26 | AUTH-11, AUTH-37 | Background refresh in the expiring zone | `Auth/AccessTokenCache.cs:179-246,332` | confirmed | retain |
| 27 | CFG-31 | RFC 850/asctime rejected | `Configuration/HttpDate.cs:79-92,137-142` (probe table) | confirmed | retain |
| 28 | RETRY-15, CFG-30 | Weekday not validated | `HttpDate.cs:79` | confirmed | retain |
| 29 | PIPE-32, REDIR-25 | Async redirect step sanctioned | as entry 14 | confirmed | retain |
| 30 | CTX-7–13, CTX-17–19 | Kept with a .NET reader; corrections: 4c carries the key, policies read `TryGet` | no `CallKey` on `RequestOptions` (`Http/Request/RequestOptions.cs:26-93`); nothing in `src/` calls `TryGet`; the only reader is the public `DexpaceCallContexts` itself | contradicted | doc-correct |
| 31 | REDIR-11 | Marker removed | as entry 15 | confirmed | retain |
| 32 | XCUT-2 | Classify by token, never by type | token first (`RetryFacts.cs:75`, `SendFailures.cs:57`); a timeout is then also read from the inner `TimeoutException` type | narrowed | doc-correct |
| 33 | RECOV-17–34 | Stated in appendix C only | no chapter body; no code cites the chapter | confirmed | retain (amendment N11-6) |
| 34 | HTTP-42 | BOM strip by preamble | `Internal/TextDecoding.cs:22-32`; scan N14 (no `StreamWriter`) empty | confirmed | retain |
| 35 | SEAM-27 | `.`/`..` rejected | `OperationDescriptor.cs:267-270`; `PathTemplateSyntax.cs:182-184`; `OperationUrlComposer.cs:100-107` | confirmed | retain |
| 36 | HTTP-17, HTTP-19, HTTP-21, TRANSPORT-12 | Names trimmed then token-validated "on both paths" | `HeaderSyntax.cs:25` trims inbound too (V15); `HTTP-19` keeps inbound names strict and `XCUT-18` rejects HTAB in a name | narrowed | **repair** (P11-14, task 3.2) |
| 37 | XCUT-20, OBS-20, OBS-30 | SDK recording never throws; listeners unwrapped | `HttpLogEmitter.cs:72-84,107-117`; `UrlRedactor.cs:88-101`; `Disposal.cs:109` (entry 23) | confirmed | retain |
| 38 | OBS-32 | Duration in `s`, no count instrument | `Diagnostics/HttpClientMetrics.cs:33-44`; scan N11 | confirmed | retain |
| 39 | HTTP-46 | Body equality by value or identity | `RequestBody.cs:132-206,386-392` | confirmed | retain |
| 40 | HTTP-48, HTTP-18 | Obs-text `ETag` cannot be sent | `Http/Common/RequestConditions.cs:62-70`; `RequestConditionsTests.cs:179` | confirmed | retain (security) |
| 41 | SEAM-2 | Core seam implementers are non-public | one exception, the public `HttpPipeline` (`SeamImplementationArchitectureTests.cs:37`) | narrowed | doc-correct |
| 42 | SEAM-22 | No `Type`-taking seam member | `SerdeSeamArchitectureTests.cs:31-51` | confirmed | retain |
| 43 | SEAM-30 | Cancel without interruption closes the orphan exactly once | leaks 44/20 000 (V5); `LateResult` closes it but is internal and unused | contradicted | **repair** (task 2.4) |
| 44 | IO-10, IO-21 | One windowing operation whose "consumers all want…" | `IO/CapturedBytes.cs` has **no production caller** | contradicted | doc-correct |
| 45 | IO-14 | U+FFFD, BOM as content | `Utf8LineReader.cs:257,262`; `ServerSentEventReader.cs:210-212` | confirmed | retain |
| 46 | — | Recovery is synchronous in the reference; `.NET` has both | `IRequestStep.cs:28,34` and peers; `SyncPath.cs:29-31`; `BannedSymbols.txt:26` | confirmed | retain |
| 47 | PIPE-2, PIPE-4 | `Operation` stage holds the deadline and the operation span | the span is opened by `HttpPipeline.cs:262`, not `OperationPolicy.cs:39` | narrowed | doc-correct |
| 48 | PIPE-3 | Sparse stage keys | `PipelineStage.cs:36,49` | confirmed | retain |
| 49 | CFG-31, RETRY-15 | One- or two-digit day | `HttpDate.cs:84` | confirmed | retain |
| 50 | CFG-23 | Literal leading-dot glob | `Internal/ProxyGlob.cs:41-75` | confirmed | retain |
| 51 | CFG-12 | No configuration builder in core | true for core; `DexpaceClientBuilder` is one, used single-threaded | narrowed | doc-correct |
| 52 | RETRY-41, RECOV-34, HTTP-35 | Negative count rejected | `RetryOptions.cs:54-61`; `RequestOptions.cs:44-48` | confirmed | retain |
| 53 | RETRY-10, RETRY-15 | Sub-tick jitter returns the base | `Resilience/RetryBackoff.cs:54`; `RetryPacing.cs:30,146` | confirmed | retain |
| 54 | RETRY-21 | One fixed precedence; `HonorRetryAfter` is the configurability | `RetryPacing.cs:43-46`; the stage stack's caller-configurable ordered list (a MUST) is absent; a delta-seconds `X-RateLimit-Reset` reads as epoch 60 and retries at once (`:100-104`) | narrowed | **repair** (task 5.5) |
| 55 | RETRY-16, RETRY-18 | A huge numeral saturates and clamps | `RetryPacing.cs:25,118-126`; `RetryBackoff.cs:80` | confirmed | retain |
| 56 | RECOV-19 | Every send classified | `RecoveryDispatcher.cs:182-196` | confirmed | retain |
| 57 | REDIR-3 | The original method | `RedirectChain.cs:28`; `RedirectDecider.cs:102` | confirmed | retain |
| 58 | REDIR-20 | Predicate replaces eligibility only | `RedirectDecider.cs:55-100` | confirmed | retain (security) |
| 59 | PIPE-40, REDIR-6, REDIR-22 | Union reading of the abandon paths | `RetryEngine.cs:172,365`; `RedirectPolicy.cs:117-118` | confirmed | retain |
| 60 | — | Several `Location` values are malformed | `RedirectLocation.cs:30-35` | confirmed | retain (security) |
| 61 | REDIR-28 | The malformed `Location` log is redacted | `RedirectLog.cs:135` | confirmed | retain (security) |
| 62 | AUTH-16, AUTH-17, AUTH-22 | `-sess` without `qop` declined | `DigestChallengeHandler.cs:176-179` | confirmed | retain (spec contradiction; amendment N11-3) |
| 63 | AUTH-28 | Guard on a reactive scheme's outbound pass | `AuthorizationPolicy.cs:177`; `ChallengeAuthPolicyTests.cs:161` | confirmed | retain (security) |
| 64 | AUTH-8, AUTH-9 | Named-key clauses vacuous | scan: no `NamedKey` | confirmed | retain |
| 65 | AUTH-36 | Cross-origin `401` not challenge-handled | `AuthorizationPolicy.cs:156-165` | confirmed | retain (security) |
| 66 | PAGE-19 | `Link` cross-origin guard | `LinkHeaderStrategy.cs:37`; `LinkTarget.cs:61-67`; `Security/PaginationLinkOriginTests.cs` | confirmed | retain (security) |
| 67 | CFG-7, CFG-5 | Day-time ISO subset, digits-only bare number, trim | also accepts `"250 ms"` and `"1.5s"` (`DurationGrammar.cs:124-144`) | narrowed | doc-correct |
| 68 | CFG-2 | The fall-through half cannot be met | it can (V11) | contradicted | **repair** (P11-17, task 5.4) |

**Tally (§11).** Verdicts: **49 confirmed, 13 narrowed** (2, 4, 12, 14, 15, 24, 32, 36, 41, 47, 51, 54, 67), **6 contradicted** (10, 20, 30, 43,
44, 68). Dispositions: **4 repair** (36, 43, 54, 68), **48 retain**, **16 doc-correct** (2, 4, 10, 12, 14, 15, 18, 20, 24, 30, 32, 41, 44, 47, 51, 67).

**New §11 items (close-out).** 69 — `XCUT-24`'s "non-consuming" error snapshot is the `BODY-30` buffered copy (P11-16). 70 — the HTAB
edge: `HTTP-17`'s trim is outbound-only, inbound names are refused (P11-14). 71 — `TRANSPORT-2`'s "SDK-managed transport" is a transport
whose primary handler the SDK built; a caller's handler or `HttpClient` is the caller's native client (P11-12). 72 — `CFG-5`–`CFG-7` bind
the accessor; a host validator that refuses to start on a recorded parse failure is stricter, not contrary (P11-10).

## §12: how the coverage index is handled

§12 maps the **design**, by citation; the exit criterion asks that "every §12 row states its satisfied, vacuous, not-satisfied and
deferred IDs" — a statement about the **build**. The only source of truth for the build is the checklists' owning rows, which phase 10's
`requirement-coverage.cs` already resolves (by phase order, latest row wins). So §12 is **not re-derived by hand**: the script gains a
`--section12` mode that prints, per prefix, the four ID sets from the resolved marks (✅ → satisfied; N/A or "vacuous" → vacuous; 🚫 →
not satisfied, with the §10 entry the row cites; ⏳ → deferred, with its owner), and a `--check-section12` mode that fails when §12's
appended table differs from that output. §12 receives one dated correction carrying the generated table at the phase-11 commit, and
the check is a Linux CI step beside the existing coverage step (P11-21). The five 🚫 MUST rows today stay 🚫, each citing its retained
§10 entry (17, 19, 14), and the 16 ⏳ are resolved by the phase 11 checklist's owning rows (task 6.3) — 15 to ✅, `NFR-17` stays ⏳ — so the
generated table is what the build is, not what the design hoped.

Also corrected in the same pass: §12's preamble list of clauses "not met on a stated domain" becomes the three of P11-2; the *Deferred or
declined* list gains `BODY-12`, `OBS-28` (SHOULD) and `SEAM-28` (MAY), each declined with a reason (task 6.5), and loses `RETRY-38`/`RECOV-31`
(built in 6a).

---

## Retained deviations and why

Every deviation that survives phase 11. **Counted** = against the parity target (P11-2). Reason class from the closed set; "breaks" is
what a conforming implementation would cost.

| # | Deviation | IDs | Counted | Reason class | Why it stays | What breaks if conformed |
|---|---|---|---|---|---|---|
| 1 | Core references `Microsoft.Extensions.Logging.Abstractions` at run time | NFR-1 | no (sanctioned by `SEAM-1`'s row) | spec-sanctioned | NuGet has no compile-only scope for an assembly called at run time; every .NET host speaks `ILogger`. Closure fixed by task 1.3 | `ILogger` leaves core's public surface; logging moves to an adapter every consumer must add |
| 2 | One hosting family counts as one library | NFR-2 (SHOULD) | no | platform | `Microsoft.Extensions.Http`'s closure is one publisher's lock-stepped family; splitting does not shrink it | four packages to wire one feature, same closure |
| 3 | No byte-stream provider seam | SEAM-3–10, IO-30–36, IO-39 | no (mechanism) | platform | `Stream`, `Memory<byte>`, `ArrayPool<byte>` are in the shared framework; nothing to plug in | an empty registry with one candidate |
| 4 | End of stream is a zero read | IO-17, BODY-10, BODY-25 (zero-read clauses) | no (mechanism) | platform | the `Stream` contract defines 0 as EOF | every unknown-length copy would throw at its true end |
| 5 | One stream-ownership rule | BODY-8 | no | spec-sanctioned | `BODY-8` asks the port to decide | — |
| 8 | Cooperative cancellation | ASYNC-3, ASYNC-4, PIPE-33, PIPE-34, CFG-20, XCUT-13 (interrupt clauses) | no (mechanism) | platform, security | `Thread.Abort` is unsupported; `Thread.Interrupt` interrupts arbitrary lock waits inside library code; the token aborts the socket. The spec's own residual ("a blocking call that ignores interrupts then runs to completion") is identical | state corruption from interrupting foreign waits |
| 9 | No discovery; DI single-registration check | SEAM-5–10, XCUT-23 | no (mechanism) | aot-trim | assembly scanning and `[ModuleInitializer]` are invisible to ILLink and NativeAOT (`NFR-8`) | trimmed or AOT apps silently lose providers |
| 10 | No model builder objects, no `IBuilder<T>` | SEAM-29, HTTP-2–4 (builder clauses) | no (mechanism) | styleguide | records, `with` and validating constructors are the language's builder (styleguide 06, CLAUDE.md "no builder-as-object types"); `required` makes a missing field a compile error | a parallel mutable type per model and a generic contract with no implementer |
| 11 | Reflection can bypass construction checks | HTTP-2, XCUT-15 | no | platform | `GetUninitializedObject` cannot be closed; the wire re-check is mandatory | — |
| 12 | Fatal set is OOM | RECOV-2, PIPE-30 | no (mechanism) | platform | `StackOverflowException` is uncatchable, `AccessViolationException` undelivered | — |
| 13 | Suppressed trail in `Exception.Data` | RECOV-12, RETRY-34, PAGE-13, SSE-29, SSE-36 | no (mechanism) | platform | .NET has no suppressed list; `AggregateException` changes the thrown type and breaks `RECOV-10` | callers' typed catches stop matching |
| 14 | Both standard pipelines follow redirects | PIPE-32 | no | spec-sanctioned (`REDIR-25`) | one policy implementation; the single-layer invariant is the transport's | `SendAsync` and `Send` on one pipeline return different results |
| 15 | No cross-origin marker | REDIR-11 | no (mechanism, stronger) | security | a forgeable or leakable marker reintroduces `REDIR-11`'s own trap | a pipeline with no auth step forwards the marker |
| 16 | Digest MD5 declined where the host refuses MD5 | AUTH-15, AUTH-16 | **yes** | security | on a FIPS host the refusal is the operator's compliance control; a vendored MD5 over the password would silently take the deployment out of its boundary. The SDK already refuses to override a host hook (`TraceIdGenerator`) | FIPS deployments become non-compliant without anyone knowing |
| 17 | Pages are materialized | PAGE-1, PAGE-3, PAGE-12, PAGE-15, PAGE-34 | no (mechanism, stronger) | platform | an abandoned async iterator never runs its `finally`, so a page holding a response strands a connection | connection leaks for hand-driven enumerators |
| 18 | Query splice relative to `Uri`'s canonical form | PAGE-21 | **yes** | security | the only byte-preserving API disables `Uri`'s validation, which puts CR/LF and the fragment on the wire (V9); the residual is RFC 3986 §6.2.2.2-equivalent | an injection surface plus rewrites of every `GetComponents` call |
| 19 | One pull-based paging engine | PAGE-29 (executor clause), PAGE-30 | no (mechanism) | platform | a pull iterator invokes no callback; `await Task.Yield()` moves the consumer | a scheduler parameter with nothing to schedule |
| 21 | `Tristate<T>` is an invariant struct | SERDE-14 (SHOULD) | no | platform | variance exists only on interfaces and delegates; a class makes `default` null, breaking `SERDE-17` | `SERDE-17` |
| 22 | `ILogger` instead of a log-event object | OBS-1, OBS-4, OBS-8, OBS-40 | no (mechanism) | platform | the logging convergence already gives a zero-allocation disabled path | a second event model beside every host's |
| 23 | `Activity` tracing, `null` no-op | OBS-25, OBS-21 (form), OBS-10, OBS-23 | no (mechanism) | platform | OpenTelemetry .NET's tracing API is `Activity` | a shim the OTel SDK does not subscribe to |
| 25 | No system-property tier, raw accessor or global slot | CFG-3, CFG-4, CFG-10, CFG-13 (SHOULD) | no | platform, styleguide | .NET has no system properties; a global slot is the service locator the guide rejects | — |
| 26 | Boot fails on a recorded parse failure | CFG-5, CFG-6, CFG-7, OBS-35 (SHOULD) | no (P11-10) | styleguide | the accessor letter is met (default kept, never throws); `csharp-aspnetcore` 1.4 requires `ValidateOnStart` on every bound options type | `Transport:Proxy:BypassAll=on` silently routes through the proxy; `Logging:Level=Header` silently logs nothing |
| 27 | `I` prefix, `Async` suffix | — | no | styleguide (Framework Design Guidelines outrank for a public NuGet surface) | a prefix-less `HttpClient` collides with `System.Net.Http.HttpClient`; `Execute` ships beside `ExecuteAsync` | — |
| 29 | URL error carries the redacted input | HTTP-47 (SHOULD) | no | security | a userinfo password or query token must not reach an exception message (`XCUT-19`) | credentials in logs |
| 30 | Absent body is an empty replayable body | HTTP-6 | no | (mechanism) | consumers never branch on null | — |
| 32 | Malformed inbound header **name** fails the response | TRANSPORT-14 (name half) | **yes** | security | the only repair is a hand-written HTTP/1.1 head rewriter in the byte path of every response the reference transport reads, a second parser ahead of the runtime's (1xx heads, folded lines, lenient line endings); a defect there corrupts all traffic, to tolerate a non-compliant server. Contrast entry 33's filter, which never inspects or alters a byte | every response head passes through SDK-owned parsing |
| — | Style: `Outcome` abstract class; `LoggerMessage` generator for dotted keys; the DI reader replaces the binder; `AddDexpaceClient` returns its builder; Shouldly not adopted; `LangVersion latest`; Stryker not scheduled | — | no | platform, aot-trim | as each knowledge note argues (closed records are not closed; `SYSLIB1014` rejects dotted placeholders; the binder binds no `init` and is not AOT-safe; the `IHttpClientBuilder` precedent; xUnit `Assert` suffices; …) | — |

**Count after phase 11: three** (16, 18, 32), each argued. The roadmap's target ("at most 5") is met without re-ruling it.

## Repairs at a glance

| Task | Repair | IDs | Size |
|---|---|---|---|
| 1.1 | The reproducible-pack job | NFR-12 | S–M |
| 1.3 | Core's dependency allowance is a fixed set, with a self-test | SEAM-1, NFR-1 | S |
| 1.4 | Two OOM-swallowing catches; a fatal-filter gate | RECOV-2, RETRY-25 | S |
| 1.5 | `Disposal` writes only to a recording activity | OBS-21 | S |
| 1.6 | `CA1062` on for `src/`; `HttpResponseException` null and non-error guards | NFR-7, XCUT-8 | S |
| 2.1 | `ServiceRequestException : IOException` | TRANSPORT-20, XCUT-4 | M |
| 2.2 | `ServiceResponseException` deleted; `SendFailures` message by `HttpRequestError` | XCUT-4, TRANSPORT-20 | S |
| 2.3 | `AsAsync` faults the task on scheduler rejection and pre-cancellation | ASYNC-2 | S |
| 2.4 | Public `WaitOrDisposeAsync` (the cancel-without-interruption mode) | SEAM-30, ASYNC-3 | S–M |
| 2.5 | Validating `init` accessors name their field | SEAM-29 | S |
| 3.1 | A second open returns the same stream | BODY-14, HTTP-41 | M |
| 3.2 | Inbound header names are not trimmed | HTTP-19, XCUT-18, TRANSPORT-14 | S |
| 3.3 | `FromFile` rejects a Unix special file | BODY-11, HTTP-40 | S–M |
| 3.4 | `Request` rejects a non-canonicalizing `Uri` | XCUT-18 (defence), HTTP-7 | S |
| 3.5 | `Page` copies its values | PAGE-2 | S |
| 3.6 | The splice keeps empty segments | PAGE-21 | S |
| 4.1 | The native-retry stop filter on SDK-built handlers | TRANSPORT-2 | S–M |
| 5.1 | `TraceIdGeneration` (W3C, Datadog, zero coercion) | OBS-27 | S |
| 5.2 | A null log value is the literal `null` | OBS-3 | S |
| 5.3 | Six plain-name `Define` sites move to the generator | styleguide 6.2 | S |
| 5.4 | An empty value falls through to the lower provider | CFG-2 | M |
| 5.5 | `RetryOptions.PacingHeaders` (the stage stack's ordered list) | RETRY-21 | S–M |
| 5.6 | An SSE per-event data cap | SSE-19 | M |
| 5.7 | The attempt header is set, not appended, and may not be `Idempotency-Key` | RETRY-38 | S |
| 5.8 | The MD5 probe treats any non-fatal exception as refused | AUTH-15 | S |

---

## The shape of `docs/deviations.md`

Ported from Node's file (method, the "outside a phase" collection point) with Ruby's columns. `docs/README.md` already reserves the
entry ("Created by roadmap phase 11"; housekeeping may not write it). Sections, in order:

1. **Preamble.** What the file is (the as-built audit of design §10 and §11, and the collection point for deviations found outside a
   phase); that §10 owns the numbering and this file is renumbered with it in the same commit; the audit commit; the counting rule (P11-2);
   the current count and the parity target, stated **once** and checked by the gate (L6).
2. **`## The audit`** — one row per §10 entry, in Ruby's columns plus three:
   `| # | Title | IDs touched | Status | Disposition | Reason | Evidence |`. `Status` ∈ {`as-built: confirmed`, `as-built: narrowed`,
   `as-built: contradicted → doc corrected`, `retired (repaired, task n.m)`} (Ruby's `design only — not yet built` is gone for good). `Disposition`
   ∈ {`retain`, `retain, unmet-clause`, `repaired`, `doc-corrected`}. `Reason` is a class from the closed set, or `—`. `Evidence` holds
   `path:line` citations and test names.
3. **`## Retained deviations`** — one `### N. <title>` block per retained entry: *Verified* (the evidence) and *Why it stays* (the reason and
   what breaks), Node's two-paragraph form.
4. **`## Readings (design §11)`** — `| Item | IDs | Verdict | Disposition | Evidence |`, one row per item 1–72.
5. **`## Negative scans`** — `| Scan | Entry | Pattern | Paths | Allowed | Probe |`: a .NET regex, a glob list under the repository root,
   the allow-listed `path` set (usually empty), and a string the pattern must match (the scan's own negative control).
6. **`## Amendment set N11`** — the spec amendments (below), each `N11-n`, recorded and not applied.
7. **`## Deviations recorded outside a phase`** — Node's collection point: `| Deviation | Found by | Date | IDs | Evidence | §10 status |`,
   empty at creation, deliberately uncounted.

## The ledger-audit gate (P11-5, P11-6)

**Where it lives.** `tests/Dexpace.Sdk.Conformance.Tests/Gates/LedgerAuditTests.cs`, `[Trait("Category", "Unit")]`, beside `CiWorkflowTests`,
`LicenseHeaderTests` and `AnalyzerWaiverTests`, reading repository files through `Gates/RepositoryRoot.cs`. Three reasons it is a test and
not a `scripts/ci/*.cs` app: it runs in the existing `Test` step on all three OS rows, so `ci.yml` gains no step and `CiWorkflowTests`
needs no new row (a dropped step cannot retire it); its self-tests are `[Theory]` cases with the kit's red/green discipline; and the
file-based apps need a shell self-test each, a second harness. Absence claims about the **compiled** surface stay architecture tests in
`tests/Dexpace.Sdk.Core.Tests/Architecture/`; the gate checks that every test a row cites exists.

**The rules.** Each is a `[Fact]` or `[Theory]` over the real files, and each has a fixture case (synthetic file text held in the test) that
must fail it.

| Rule | Asserts | Fails when |
|---|---|---|
| L1 numbering | one audit row per §10 entry 1..N (N read from §10's `^\d+\. \*\*` items, dated additions included) | a missing, extra or repeated number |
| L2 status grammar | every `Status` and `Disposition` cell is in its closed set | `design only`, empty, anything else |
| L3 evidence | every `path:line` (or `path:line-line`) resolves to an existing file and a line within it | a moved file or a line past EOF |
| L4 retained reason | every `retain` row names a reason class from the closed set, and its `### N.` block has a *Why it stays* paragraph | a retain without a class or argument |
| L5 IDs | every ID in `IDs touched` is in appendix C | a typo or a retired ID |
| L6 count | rows marked `unmet-clause` equal, by ID, the *Unsatisfied MUSTs* bullets of `docs/first-release.md`, and number at most 5 | the registers disagree, or the count passes the target |
| L7 readings | one `Readings` row per §11 item number | an item without a row |
| L8 waiver parity | every permanent conformance waiver (`Owner = null`) in a first-party driver names an assertion whose ID is on an `unmet-clause` row or a reading that scopes it out (item 71), and every such row that cites a waiver finds it | a waiver with no ledger row, or a row citing a deleted waiver |
| L9 negative scans | each scan's regex over its globs matches only its allow-list | a match outside the allow-list |
| L10 scan self-test | each scan's probe string matches its regex | a vacuous scan |
| L11 cited tests | every `Class.Method` a row cites is a method declaration under `tests/` | a renamed or deleted test |

**The negative scans** (rows of `docs/deviations.md`; regexes are .NET, globs exclude `bin/` and `obj/`). Already enforced elsewhere is
noted; the gate re-runs every row anyway, so the ledger is one place to read.

| Scan | Entry/item | Pattern | Paths | Allowed |
|---|---|---|---|---|
| N1 | §10 3 | `\b(class\|interface\|struct\|record)\s+(I?(Buffered)?(Source\|Sink)\|Buffer\|I?IoProvider\|ByteString)\b` | `src/**/*.cs` | — |
| N2 | §10 9 | `ModuleInitializer\|AppDomain\.CurrentDomain\.GetAssemblies\|Assembly\.(Load\|LoadFrom\|LoadFile)\b\|GetExportedTypes\|AssemblyLoadContext` | `src/**/*.cs` | — |
| N3 | §10 7 (after 2.1) | `class\s+ServiceResponseException\b` | `src/**/*.cs` | — |
| N4 | §10 8 | `\bThread\.(Abort\|Interrupt)\b\|ThreadInterruptedException\|ThreadAbortException` | `src/**/*.cs` | — (`Interrupt` also RS0030) |
| N5 | §10 10 | `\b(class\|record\|struct\|interface)\s+I?\w*Builder\b` | `src/Dexpace.Sdk.{Core,Http.SystemNet,Serialization.SystemTextJson,Extensions.DependencyInjection}/**/*.cs` | `Headers.cs`, `Query.cs`, `PipelineBuilder.cs`, `DexpaceClientBuilder.cs` |
| N6 | §10 13 | `new\s+AggregateException\b` | `src/**/*.cs` | — |
| N7 | §10 15 | `new\s+PipelinePropertyKey<` | `src/**/*.cs` | `IdempotencyPolicy.cs` |
| N8 | §10 16 | `0x67452301\|0xefcdab89\|0x98badcfe\|0x10325476` | `src/**/*.cs` | — (no vendored MD5) |
| N9 | §10 19 | `TaskScheduler\|SynchronizationContext\|ThreadPool\.\|ContinueWith` | `src/Dexpace.Sdk.Core/Pagination/**/*.cs` | — |
| N10 | §10 25 | `public\s+static\s+(?!readonly)[^(=;]*\{\s*get;\s*(set\|init);` | `src/**/*.cs` | — (no global slot) |
| N11 | §11 38 | `Create(Counter\|Histogram\|UpDownCounter\|Observable\w+\|Gauge)\b` | `src/**/*.cs` | `HttpClientMetrics.cs` |
| N12 | §11 21 | `IObservable<\|IObserver<\|System\.Reactive` | `src/**/*.cs` | — |
| N13 | §11 17 | `System\.Net\.ServerSentEvents\|\bSseParser\b\|\bSseItem<` | `src/**/*.cs` | — |
| N14 | §11 34 | `\bStreamWriter\b\|new\s+UTF8Encoding\(\s*true` | `src/**/*.cs` | — |
| N15 | §11 24 | `AddStandardResilienceHandler\|AddResilienceHandler\|\bPolly\b` (code, not `///`) | `src/**/*.{cs,csproj}` | — |
| N16 | §11 25 | `IsLoopback\|"localhost"\|127\.0\.0\.1` (code, not `///`) | `src/Dexpace.Sdk.Core/**/*.cs` | — |
| N17 | §10 31 | `\[(DllImport\|LibraryImport)\b\|static\s+extern\b` | `src/**/*.cs` | — |
| N18 | §11 1 | `Task\.Run\(\|ThreadPool\.QueueUserWorkItem` | `src/Dexpace.Sdk.{Core,Http.SystemNet,Serialization.SystemTextJson,Extensions.DependencyInjection}/**/*.cs` | `Internal/BackgroundWork.cs` |
| N19 | §11 30 | `\bCallKey\b` | `src/Dexpace.Sdk.{Http.SystemNet,Extensions.DependencyInjection}/**/*.cs` | — (the stamp is a post-release trigger; when it lands, this row is deleted with the trigger) |
| N20 | §10 12 | `catch\s*(\(\s*(System\.)?Exception(\s+\w+)?\s*\))?\s*$` | runtime libraries | the handler must end in `throw;` — this one is the fatal-filter gate of task 1.4, a source-scan architecture test, and the ledger row cites it |

Two scans are promoted to `BannedSymbols.txt` as well (task 6.2), because RS0030 fails at the call site with a message: `M:System.Threading.Thread.Abort`
(not banned today) and `T:System.Runtime.CompilerServices.ModuleInitializerAttribute`.

## The spec amendment set (P11-4)

`docs/product-spec/` is byte-identical to the Ruby and Node copies, and `docs/README.md` freezes it. So, as Ruby's "C11" set lives in
Ruby's `docs/deviations.md`, the .NET set **N11** lives in `docs/deviations.md`'s *Amendment set N11* section as recommendations to the
specification's author, and no file under `docs/product-spec/` is edited. Each names the row, the problem, and the proposed text.

| # | Rows | Problem | Proposed |
|---|---|---|---|
| N11-1 | XCUT-24, BODY-30, BODY-33, RECOV-16 | `XCUT-24` asks error snapshots to be non-consuming while `BODY-30` and `RECOV-16` require the cap to drain and release | "For an error snapshot, the primary read path is the `BODY-30` buffered copy; the original stream is released per `RECOV-16`." |
| N11-2 | HTTP-17, HTTP-19, XCUT-18 | the trim and "reject HTAB" meet at the edge of a name; `HTTP-19`'s strictness is easy to read as trimming too | "Surrounding whitespace is trimmed for outbound names only; an inbound name with surrounding whitespace is invalid." |
| N11-3 | AUTH-16, AUTH-17, AUTH-22 | a `-sess` challenge without `qop` cannot satisfy all three | "A `-sess` algorithm without `qop` MUST be declined." |
| N11-4 | TRANSPORT-2 | "SDK-managed transport" is undefined when the caller supplies the native client | "…a transport whose native client the SDK constructed; a caller-supplied client's retry is the caller's." |
| N11-5 | RETRY-38, RECOV-31 | one feature, two IDs, two modal levels; `RETRY-38`'s tag contradicts its prose | merge under one ID at SHOULD, with "the stamp replaces any previous attempt header and MUST NOT reuse the idempotency key's name". |
| N11-6 | RECOV-17 … RECOV-34 | stated in appendix C only | give them a chapter body in `08-execution-pipelines.md` §8.2. |
| N11-7 | CFG-31, RFC 9110 §5.6.7 | rejects date forms RFC 9110 obliges recipients to accept | state the intent ("a rejected date is no hint") explicitly. |
| N11-8 | CFG-5, CFG-6, CFG-7 | "never throw" is unclear for configuration read once at composition | "The accessor never throws; a host MAY refuse to start on a recorded parse failure." |
| N11-9 | PAGE-21 | "byte-for-byte" is unattainable through a URL type that canonicalizes unreserved escapes | permit RFC 3986 §6.2.2.2 normalization of unreserved characters. |
| N11-10 | OBS-32 | names `http.client.request.count` and `ms` while deferring to OpenTelemetry, which has neither | follow the OTel stable HTTP client metrics. |
| N11-11 | XCUT-20, OBS-20, OBS-30 | "observability MUST NEVER throw" vs unwrapped listener calls | "…the SDK's own recording code; listener callbacks are the `OBS-30` party." |
| N11-12 | SEAM-2 | "never implements" forbids the bridges `SEAM-18` requires | "never supplies the external concern behind a seam". |

## Inbound list: every bullet addressed to phase 11

| Source | Bullet | Disposition | Task |
|---|---|---|---|
| 8a status note | F-A2: `ASYNC-2`'s worker-pool rejection, "accept ⏳ or rule it into a phase that may touch core" | **repaired** | 2.3 |
| 8b status note | entries 32, 33 take the count to seven against five; "retire two, re-rule, or argue" | **resolved**: 33, 31 and 24 repaired, 26 reclassified, 32 retained with a corrected reason; count three | 3.3, 4.1, 5.1, 6.1 |
| 8b status note | the `TRACE` question (a zero-length content closes its retry) | **superseded** by the filter, which closes all five methods without changing a framing byte | 4.1 |
| 8b status note | the Core flake `OperationSpanLifecycleTests.An_attempt_span_exists_only_under_an_operation_span` | **measured first**: task 0.1 runs the Core suite ten times; reproduced → task 1.2 isolates it into `NoDiagnosticListeners` or fixes the leak; not reproduced in ten → recorded as not reproduced, with the run log | 0.1, 1.2 |
| 9 status note | §10 entries 25 and 26 gain the as-built facts | **done** in the verdict table and their dated corrections | 6.4 |
| 9 status note | "the auditor decides whether `CFG-2`'s shadowing clause belongs in the unmet-MUST count" | **repaired**, so not counted | 5.4 |
| 9 status note | §11 item 30's reader is unbuilt (P9-20) | **doc-correct** (no SDK reader; the store's public lookup is for consumers; `CTX` rows stay met); the `first-release.md` trigger stays; scan N19 | 6.4 |
| 10 status note | `ServiceResponseException`: delete, or argue it into §3.2 | **deleted** | 2.2 |
| 10 status note | `XCUT-24`'s error-snapshot repair candidate | **doc-correct**: the decline misread `BODY-30`/`RECOV-16`; new §11 item 69; a pin test | 6.3, 6.4 |
| 10 status note | the unmet-clause count for `XCUT-4`'s "catchable as a generic I/O error" | **repaired** (`IOException` base) | 2.1 |
| 10 status note | `XCUT-8`'s reading as a §11 item | **repaired** instead (the constructor guards), so no reading is needed | 1.6 |
| 10 status note | the HTAB edge as a §11 item, the inbound trim a gap or a reading | **gap, repaired**; §11 item 70; amendment N11-2 | 3.2 |
| 10 status note | F-A2, `TRACE`, parity over-budget unchanged | as above | — |
| 10 status note | 16 ⏳ MUST rows; give them dated corrections and tick *Every MUST dispositioned* | 15 get **owning rows in the phase 11 checklist** (✅, citing the 6a, 6b, 6c and 3a evidence), which the script resolves by phase order without editing a closed checklist; `NFR-17` stays ⏳ (the ruleset). The line stays open naming `NFR-17` alone | 6.3 |
| 10 checklist | the frozen-design corrections proposed for a human: §9, §9.1–§9.3, §10 entry 7, three §11 items, §12 notes | **applied** as dated corrections | 6.4 |
| 10 status note (to 12) | one-time public-surface choices gain the constructor guard and `ServiceResponseException` | both **decided here** (repaired, deleted); the list for phase 12 is rewritten with this phase's additions | 6.5 |

## The `docs/first-release.md` walk

| Entry | Finding | Action (task 6.5) |
|---|---|---|
| Header ("phase 11 creates `deviations.md`") | — | point at the file |
| *Packages* | accurate | none |
| Blocker: CI green on every row | not green: the `reproducible packages` job | stays open; task 1.1 |
| Blocker: the ruleset | the lead's admin action | stays open; unchanged |
| Blocker: the nine P0 defects | closed by phase 1 (2026-09-29) with permanent `Security` tests | **tick**, dated, pointing at the phase 1 checklist |
| Blocker: every MUST dispositioned | 15 of 16 ⏳ resolved by task 6.3 | stays open, rewritten: "`NFR-17` only" |
| Blocker: conformance green | green locally; tied to CI green | stays open |
| Blocker: deviations audited | `docs/deviations.md` exists, every row `as-built` or `retired` | **tick** at task 6.6 |
| Blocker: one-time public-surface choices | phase 11 decides the two candidates and adds its own surface | rewrite the list: `ServiceRequestException`'s base type; `TraceIdGeneration`; `ResponseTaskExtensions.WaitOrDisposeAsync`; `RetryOptions.PacingHeaders`; the SSE event cap parameter and its exception; `ServiceResponseException` removed; the guarded constructor |
| Blockers: `SECURITY.md`, NuGet, signing, versioning, docs, `CHANGELOG.md` | phase 12 | unchanged |
| *Unsatisfied MUSTs* | lists 32 and 33 only; misses 16, 18 (and 24, 26, 31) | rewrite to the three: `AUTH-15` (entry 16), `PAGE-21` (entry 18), `TRANSPORT-14` name (entry 32); delete the over-budget paragraph with a dated note; `TRANSPORT-2` line removed |
| *SHOULD/MAY declined* | `XCUT-24` line is a misreading; `BODY-12`, `OBS-28` (SHOULD) and `SEAM-28` (MAY) are declined nowhere | delete the `XCUT-24` line (dated); add `BODY-12` (`HttpClient` writes to its own stream, as `TRANSPORT-28`), `OBS-28`'s byte-count milestones (the response count exists only after the consumer drains the body, after the attempt span has ended), `SEAM-28` (no carrier on `RequestOptions` and no consumer; adding one is additive later) |
| *Asymmetries* | `di-empty-shadows` is repaired | delete it (dated); add `borrowed-handler-native-retry` (a caller's handler keeps `SocketsHttpHandler`'s retry, item 71); `async-redirect-pillar`, `two-meters`, `borrowed-client-traceparent`, `link-cross-origin-guard`, `sync-borrowed-handlers`, `explicit-proxy`, `borrowed-client-default-deadline`, `di-no-reload`, `di-handler-lifetime`, `public-query-splice` stay |
| *Post-v1 packages* | accurate | none |
| *Release path*, *After the first publish* | phase 12 | none |
| *Post-release triggers* | the `CallKey` trigger stays (scan N19 is tied to it) | none |

## The as-built line refresh (P11-23)

Every **As built** line in §§2–9 gains one dated line, **"As built (2026-10-<dd>, phase 11, `<commit>`)"**, appended under the last
existing one (never replacing it), stating `built`, `built, with §10 entry N`, `partial: …` or `not built: …, owner …`. The audit's
table of 64 lines is in the plan (task 6.4) with the proposed text. Ten lines are **contradicted** by the code and the refresh corrects
them explicitly: 03:806 (`SEAM-28`'s operation id is never attached), 05:446-449 and 05:488 (no `CallKey` stamp), 03:942 (the quiet-dispose
reporting gap of P3b-3 is not closed for a pipeline without `InstrumentationPolicy`), 08:432 (`dexpace.redirect.hop` **is** wired), 03:984
(DI binding is built), 02:62 (the cell named is the kit's, and the DI cell lists four packages for one root), 08:23 and 08:31 and 02:290
(the 9.0.5/`DiagnosticSource` closure), 05:388 (the credential bridge is `TokenCredential.GetToken`), 09:254 (the kit **is** measured by
the coverage gate, `scripts/ci/coverage-gate.cs:57`), and §9's gate table (09:10-30), which still reads "State at d45e64b" throughout and
gains a phase-11 column. Remaining genuine gaps after the refresh: none at MUST; the declined SHOULD/MAYs above; design-only items with no
requirement (webhooks §7.4, the Rx bridge, the package-validation baseline, strong naming, `NFR-16`), each already owned by phase 12 or a
trigger.

## The reproducible-pack finding (P11-22)

**What is known.** `scripts/ci/reproducible-pack.sh` passes locally in every layout CI uses (V2), with identical hashes across runs. The
two phase-10 commits that landed last on the branch (`0ae87bd`, `f1db46f`) added mismatch diagnostics that name each differing package
entry and print the differing **zip metadata** (timestamps, version-needed), which says the CI mismatch was investigated down to container
metadata and was still red at merge. The failing job's log — which those diagnostics write — is the evidence, and reading it is a remote
action this design may not take (the global rule: no `gh` without the user's explicit instruction).

**Ruled out locally.** Path leakage (two worktree depths, in-repo and out), a shallow checkout, the CI output location, timezone (NuGet
writes `SOURCE_DATE_EPOCH` as UTC), a future `SOURCE_DATE_EPOCH` (no mtime clamp: entries carry the epoch, not file times).

**Remaining hypotheses**, in the order the plan tests them: (a) the runner resolves a different SDK patch than `global.json`'s 10.0.401
under `rollForward: latestPatch` (an image-installed 10.0.4xx), and two invocations differ in a pack task's defaults; (b) a `.snupkg` or a
`.psmdcp` part produced only on the runner; (c) a NuGet client difference that ignores `SOURCE_DATE_EPOCH` for some part.

**Why it is phase 11's.** Roadmap constraint 7 routes audit-or-repair work against a closed phase to phase 11's inbound list; the phase's
own exit ("the ledger-audit gate is in CI") needs a green CI; and `docs/first-release.md`'s first blocker is CI green on every row. So it is
**task 1.1, first in the first pull request**, and every later pull request lands on a green job. The task begins with the one action this
design cannot take: the lead runs (or authorises) the read-only `gh run view <run-id> --log-failed` for the failing job and pastes the
`reproducible-pack:` lines. If the lead declines, the task proceeds on hypothesis (a) by pinning the job's SDK (`setup-dotnet` already
reads `global.json`; the task adds `dotnet --version` to the job log and fails the job when it differs from `global.json`'s) and re-runs.

---

## Decisions

| ID | Decision | Rationale | Second-guess? |
|---|---|---|---|
| P11-1 | One phase, no sub-phases, six stacked pull requests (gates; error taxonomy and bridges; bodies, headers and URLs; transport; observability, configuration, retry and SSE; the ledger and close-out) | The repairs are small and independent; the ledger PR reads the final tree | — |
| P11-2 | The counting rule: only an undelivered observable outcome counts; mechanism substitutions are retained, argued and listed, uncounted; sanctioned changes and spec contradictions never count | The roadmap's "unsatisfied clauses" means behaviour a consumer does not get; counting every letter of a substituted mechanism would make the budget meaningless and hide the three that matter | yes |
| P11-3 | §10 is **not** rewritten (Node rewrote theirs); every entry keeps its number and gains a dated correction; a repaired entry is marked *retired* | The roadmap cites deviations by topic so renumbering cannot stale citations, but checklists cite entry numbers; frozen-tree rule | — |
| P11-4 | The amendment set N11 is recorded in `docs/deviations.md`, not applied to `docs/product-spec/` | byte-identity with the siblings' copy; the spec is the yardstick | — |
| P11-5 | The ledger gate is a test in `Conformance.Tests/Gates/`, not a script | runs on all rows with no new CI step; self-tests are theories | — |
| P11-6 | Negative scans are data rows in `docs/deviations.md`, each with a positive probe | a new absence claim is one row; a vacuous regex fails | — |
| P11-7 | `ServiceRequestException` derives from `IOException` (spec letter), not `HttpRequestException`; it keeps `IRetryableError` and the trail through `ExceptionTrail` | `TRANSPORT-20` says "MUST be a subtype of the platform IOException"; single inheritance only forced the choice because the port chose one SDK root, which no row requires. Cost: `catch (SdkException)` no longer catches a transport failure (a breaking change, documented; pre-1.0) | **yes — the one most worth a look** |
| P11-8 | A second open returns the same stream (`BODY-14`); open after dispose still throws `StreamClosedException` | the spec says so and the BCL agrees (`ReadAsStreamAsync` returns the same stream); "louder" is not a platform reason | yes |
| P11-9 | `HttpResponseException`'s public constructor rejects `null` (`ArgumentNullException`) and a non-error status (`ArgumentException`) | `XCUT-8`; it is the one public mapping route; V17 | — |
| P11-10 | Entry 26 is retained and reclassified: the accessor letter of `CFG-5`–`CFG-7` is met, and the boot failure is a stricter host rule from styleguide 1.4 | the grammars never throw and keep the default; the validator is a separate rule the clause does not forbid; conforming would silently mis-route traffic on a typo | yes |
| P11-11 | Entry 32 is retained with a corrected reason, not repaired with a head-rewriting filter | a hand-written parser in front of the runtime's on every response head is a security and correctness risk out of proportion to tolerating malformed servers; entry 33's filter never touches a byte, so the asymmetry is principled. Trigger: a consumer whose server sends such names | yes |
| P11-12 | Entry 33 is repaired with the end-of-stream filter on handlers the SDK builds (`CreatePrimaryHandler`, so the DI named client too); a caller's handler is out of `TRANSPORT-2`'s scope (item 71) | V6; the filter changes no byte and only turns a pre-response EOF into the I/O failure the runtime already raises otherwise | — |
| P11-13 | Entry 31 is repaired with `TarWriter`'s classification on non-Windows for a size-0 path | V8: shared framework, AOT-clean, does not open a special file | — |
| P11-14 | Inbound names are refused when not exactly a token (no trim); the transport's existing catch drops the header | `HTTP-19`, `XCUT-18`, `TRANSPORT-14`; unreachable through `SocketsHttpHandler`, live for third-party transports and direct callers | — |
| P11-15 | Entry 18 is retained; `Request` rejects a `Uri` built with `DangerousDisablePathAndQueryCanonicalization` | V9: the only verbatim route writes CR/LF to the wire | — |
| P11-16 | `XCUT-24`'s decline (P10-23) is withdrawn: the snapshot's primary read path is the `BODY-30` copy, and reads of it are non-consuming | `BODY-30` and `RECOV-16` require the drain; option "keep the live stream" would loosen a permanent `Security` test | — |
| P11-17 | `CFG-2`: when the registered `IConfiguration` is the root that holds the caller's section, a blank merged value falls through to the next provider with a value; otherwise today's read stands | V11; reverses P9-7's silent shadow | — |
| P11-18 | `OBS-27`: a public static `TraceIdGeneration` (W3C and Datadog generators, re-draw on zero, `FormatDatadog`); the SDK still never sets the process hook | the app opts in with one line; a library must not set the hook | — |
| P11-19 | SSE per-event cap: `maxEventBytes`, default **8 MiB**, failing as a new `ServerSentEventTooLargeException` (a `StreamingException`) | V10; `SSE-19`'s chapter sanction covers "lines/values"; 8× the line cap leaves room for large events while bounding amplification to tens of MB | yes (the default) |
| P11-20 | `CA1062` is turned on for `src/`; entry 28 and its overlay row retire | V12: one finding, and it was a bug | — |
| P11-21 | §12 is regenerated from the checklists by `requirement-coverage.cs --section12`, checked in CI | a hand-maintained index drifts; the checklists are the build's record | — |
| P11-22 | The reproducible-pack job is task 1.1, diagnostic-first, with one lead action (read the failing log) | V2 rules out the local causes; evidence over guessing | — |
| P11-23 | The as-built refresh appends a dated line per section; it never rewrites the old ones | frozen-tree rule | — |
| P11-24 | `NFR-17` stays ⏳: it needs an admin ruleset change no phase can make | honest; the `first-release.md` line names it alone | — |
| P11-25 | `CapturedBytes`, `DeepValue`: kept and documented as tested internal helpers with no production caller (they carry `IO-21` and `CFG-33`/`CFG-34`); `LateResult` is promoted by task 2.4 | deleting them would retire rows the helpers exist to meet; their status is now stated | — |
| P11-26 | `BODY-12`, `OBS-28` (SHOULD) and `SEAM-28` (MAY) are declined in `docs/first-release.md` with reasons | declining a SHOULD or MAY is the specification's own latitude; none is a deviation | — |
| P11-27 | Probes are re-run as tests, never committed as probes; each verification above becomes a test where it pins behaviour | the evidence must outlive the design | — |

## Corrections owed at close-out

Executed by PR 6 (tasks 6.3 to 6.7): `docs/deviations.md` (new); design §10 (a dated correction per entry, the preface's count, the
styleguide list gains the `LoggerMessage` generator departure and loses `CA1062`), §11 (dated corrections for the 16 doc-correct items, items
69–72), §12 (the generated table), §§2–9 (the as-built refresh), §9's gate table; `docs/first-release.md` (the walk); `docs/README.md` (the
`deviations.md` row loses "(planned)"); the styleguide overlay (the xUnit/Shouldly row: "xUnit v3 on MTP, conformed; Shouldly not adopted";
the `MA0051` row: "no waiver remains"; the `CA1062` row: conformed; the `LoggerMessage` row narrowed to dotted keys); `CLAUDE.md` (the
"What is genuinely unbuilt" paragraph; transport failures are `IOException`s; the reader no longer shadows); the roadmap (the Phase 11 row's
design link, a dated status note); `CHANGELOG.md`; the knowledge notes (`configuration/…` empty-value note superseded; a transport-taxonomy
note; the HTAB edge); the user pages `transport.md`, `retry.md`, `sse.md`, `configuration.md`, `dependency-injection.md`, `bodies.md`,
`http.md`, `tracing-and-metrics.md`, `cross-cutting-invariants.md`, `conformance.md`.

## Breaking changes (all pre-1.0, each a `CHANGELOG.md` `[Unreleased]` line)

`ServiceRequestException` derives from `IOException` (not `SdkException`); `ServiceResponseException` is removed; `HttpResponseException`'s
constructor rejects a non-error status and `null`; a second `OpenRead`/`OpenReadAsync` returns the same stream; `AddInbound` rejects a name
with surrounding whitespace; `FromFile` rejects a Unix special file; `Request` rejects a non-canonicalizing `Uri`; `AsAsync` faults instead
of throwing; an empty configuration value falls through; the attempt header is set and may not be `Idempotency-Key`; an SSE event above the
cap fails; the owned transport no longer re-sends a body-less idempotent request natively; validating `init` accessors name their field.

## Exit criteria

1. `docs/deviations.md` exists; every audit row reads `as-built: …` or `retired (repaired, task n.m)`.
2. `LedgerAuditTests` (L1–L11, with N1–N20) is green in CI on every row.
3. `requirement-coverage.cs --check-section12` is green; every §12 row states its four sets.
4. The one-time public-surface choices are listed in `docs/first-release.md`.
5. Every inbound bullet above has its disposition carried out; every `first-release.md` action above is done.
6. The count of clauses unmet on a stated domain is three, and the gate's L6 holds it.
7. Every gate green, the `reproducible packages` job included; no `Security` test deleted or loosened (three are added: tasks 3.2, 3.4,
   5.6); the housekeeping probe reports nothing.

## Deviation Ledger

Roadmap constraint 7's entries for this phase. **No new §10 entry is opened.** Four §10 entries are retired by repair (24, 28, 31, 33) and
four lose their departure (6, 7, 20 R1, 25's `CFG-2`); entry 26 is reclassified (P11-10); entry 32's reason is replaced (P11-11). Four §11
items are added (69–72). One departure from the roadmap card: §12 is regenerated from the checklists rather than re-derived cell by cell
from source (P11-21), because the checklists already carry the per-ID source evidence and the card's aim — each row's four sets — is what
the generation produces. One departure from the siblings: Node rewrote its §10 in place; this port appends dated corrections (P11-3).
