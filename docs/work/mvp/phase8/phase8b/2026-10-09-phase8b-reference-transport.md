# Phase 8b — Reference-Transport Hardening: Implementation Plan

**Status:** Draft, for review. Written 2026-10-09 against `main` at `ce0f68c` (phases 0 to 7c merged), on branch
`87-phase-8-planning`, for issue [#89](https://github.com/dexpace/dotnet-sdk/issues/89). Design:
[phase 8b reference-transport design](2026-10-09-phase8b-reference-transport-design.md), the authority for every
decision below. The plan cites its rows (`TRANSPORT-n`), facts (G1 to G8) and rulings (`P8b-1`..`P8b-24`) rather than
restating them. Scope authority: the roadmap's Phase 8 card (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`),
decision D3 and the dependency-graph line "8a then 8b". Format precedent: the phase 7 plans
(`docs/work/mvp/phase7/phase7c/2026-10-09-phase7c-pagination.md` and its 7a and 7b siblings), read first. The kit this phase is
proven by is planned in `docs/work/mvp/phase8/phase8a/2026-10-09-phase8a-conformance-kit.md` by another author; this plan touches only
8b's own files and lists the shared files it edits ([Shared files](#shared-files-with-8a)).

**What this document is.** The roadmap's step 3 for sub-phase 8b: numbered TDD tasks in four pull-request groups (P8b-22), each
with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking** marking, requirement IDs, the kit
waivers it deletes and the verification commands. It is not the checklist (step 4, written from what was built in task 4.4) and it
contains no production code beyond the load-bearing sketches the tasks quote. The [checklist section](#checklist-one-row-per-owned-id)
at the end is the plan's own row table: exactly one row per ID 8b owns.

**Scope.** 14 rows: `TRANSPORT-1`, `-4`, `-5`, `-6`, `-8`, `-9`, `-10`, `-12`, `-13`, `-14`, `-17`, `-18`, `-28`, `-30` (10 MUST,
4 SHOULD by appendix C). Planned exits: 12 ✅, 2 split rows (`TRANSPORT-14` ✅ value and obs-text, 🚫 name clause under new design
§10 entry 32; `TRANSPORT-28` ✅ MUST half, 🚫 declined zero-copy SHOULD), 0 ⏳. Twelve non-row hand-offs are dispositioned in task 4.4's
second table. 8b owns no `ASYNC` row (P8b-1).

**Order and gates.** Four stacked pull requests, each green on its own, starting **after 8a's PR 2 has merged** (P8b-22): every PR
closes rows by deleting their `Owner = "8b"` waivers from `SystemNetConformanceTests`, and 8a's stale-waiver rule fails a run that
leaves a closed row's waiver behind. **PR 1** (tasks 1.1 to 1.9): construction and proxy. **PR 2** (2.1 to 2.7): the send path.
**PR 3** (3.1 to 3.5): headers. **PR 4** (4.1 to 4.7): close-out. The design's segmentation is not changed. Task 0.1 is a
pre-flight with no commit.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile error
   counts and is the expected red for a new type or a changed signature); make the production change; run them green; then run
   the wider gate of the group's last task. A test that passes before the change is a **pin** and says so; a pin is proven able
   to fail by temporarily breaking the thing it pins (never committed). **No commit is red:** a task that changes a signature
   edits every caller in the same commit.
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the project's
   curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace, including `System.Collections.Generic`,
   `System.Linq`, `System.Net` and `Microsoft.Extensions.Logging`, is imported in the file that uses it).
3. **`src/` code:** `ConfigureAwait(false)` on every `await` including `await using` (`CA2007`); methods at most 70 lines
   (`MA0051`: `ToHttpRequestMessage` and `ExecuteAsync` are split along the design's tables); `///` XML docs on every public member
   (CS1591), each citing its `TRANSPORT-n` IDs; no new `PackageReference` anywhere (P8b-19: `Directory.Packages.props` and every
   `packages.lock.json` stay byte-identical, and the V-gate checks it); no reflection in `src/` (`IsAotCompatible`,
   `IsTrimmable`); a request or response URI, a header value, a credential or a proxy user/password never reaches a log,
   message or exception text (`XCUT-18`); header **names** go through `HttpHeaderSyntax.EscapeName`. One type per file, named for
   the type. No `Task.Delay`, wall clock, `Thread.Sleep` or environment read (`BannedSymbols.txt`; the deadline runs on the
   options' `TimeProvider`).
4. **Tests:** `[Trait("Category", …)]` on every class as the design's test table says (`TestCategoryTests` enforces it per suite):
   `Unit` for stub-handler and pure-logic classes, `Integration` for loopback wire classes, `Conformance` for the kit driver,
   `Security` for exactly the one class added in task 1.7. Tokens come from `TestContext.Current.CancellationToken`; assertions are
   xUnit `Assert` only; no test sleeps on the wall clock (condition waits and `FakeTimeProvider`; the real-time deadlines in
   `TimeoutWireTests` are at least 200 ms against a server that never answers, bounded by the kit's 30 s). Test code may construct
   `System.Net.Http.HttpClient` and `SocketsHttpHandler` directly; the `RS0030` ban is for `src/` only.
5. **Security tests are never deleted or loosened.** 8b edits **no** existing `Security` class (`HeaderInjectionWireTests`,
   `FramingHeaderDropWireTests`, `RedirectWireTests`, `RedirectCredentialLeakWireTests`, `MalformedContentTypeWireTests`, in
   `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security/`, nor any in `Dexpace.Sdk.Core.Tests/Security/`) and adds exactly one
   (`ProxyCredentialWireTests`, task 1.7). The V-gate's diff check is `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Core.Tests/Security
   tests/Dexpace.Sdk.Http.SystemNet.Tests/Security` listing that one added file and nothing else. The existing classes assert levels and
   messages, never event ids, and the default `FirstPerName` policy keeps the first drop a `Warning` (design, Prerequisites), which is
   why none needs an edit; if one fails, the production change is wrong, not the test.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in `Unshipped` and a
   removed one is a deleted line; the file is sorted ordinally and the build's `RS0016`/`RS0017` output is the authority (apply the
   analyzer's code fix, then compare against the design's "The public surface" list; the record's compiler-generated members are
   copied from the analyzer verbatim). 8b changes two files: `src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt` (additions
   only) and `src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt` (the four `DexpaceLogEvents` constants and their ids). The STJ package's
   file must not move.
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes, stating what it
   was. Task 4.5 adds the `CHANGELOG.md` `[Unreleased]` line for each of the design's nine breaking changes.
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*` (3a finding F1); SystemNet code is
   unaffected. `Dexpace.Sdk.Core.Http.Response.Response` and `System.Net.Http.HttpResponseMessage` are different types: the existing
   `using SystemHttpClient = System.Net.Http.HttpClient;` alias is kept.
9. **Commits** follow the repository style: `feat!:` for a breaking PR (all four 8b PRs except PR 4 carry a breaking change), `test:`
   for tests only, `docs:` for documentation only, `chore:` for refactors, `ci:` for CI. **No AI attribution** in a commit message,
   changelog line or document. **The plan authorises no `git push`, no `gh` command and no remote action** (global hard rule); the
   implementer stops before any push and hands the lead the branch names. Branches: `<issue>-phase-8b-<slug>` off `main` for each
   stacked PR, the first off `main` after 8a's PR 2 merged, each later one off its predecessor; #89 is the issue.
10. **Siblings own shared files too.** `CHANGELOG.md`, `CLAUDE.md`, the roadmap status notes, `docs/first-release.md`,
    `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` and `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetConformanceTests.cs` are shared with
    8a or later phases: re-derive each hunk from the merged file and never resolve a conflict by keeping either side whole.
11. **Test doubles.** Stub-handler tests use a `Func<HttpRequestMessage, HttpResponseMessage>` handler written in the test file that
    needs it (the existing `StubHandler` pattern); a handler that must be reachable from the **sync** face overrides `Send` as well as
    `SendAsync` (G1). Time-driven tests use `Microsoft.Extensions.Time.Testing.FakeTimeProvider`, already reachable through
    `Dexpace.Sdk.TestSupport`. Wire tests use the loopback fixture, in whatever namespace 8a's PR 1 left it
    (`Dexpace.Sdk.Conformance.Wire`; task 0.1 records it).
12. **A mutation is not a red.** Where a task says "prove the pin can fail", break the production line, see the named test fail, and
    restore it with `git checkout -- <file>` before the commit.

### Environment and command shorthands

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. On the shared machine:
`export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet`, one build at a time, `-m:3 -nr:false --disable-build-servers`.
Warnings are errors: a green build is the lint gate. The test runner is Microsoft.Testing.Platform: build first, then
`dotnet test … --no-build`, and **never pass MSBuild flags (`-m`, `-nr`, `-p:`) to `dotnet test`** (the platform forwards them to
the test host and zero tests run; `--filter-class`, `--filter-trait`, `--filter-method` are its options). A run that reports zero
tests is a failure of the command, not a pass.

- **V-build** = `dotnet build Dexpace.Sdk.sln --configuration Release -m:3 -nr:false --disable-build-servers`
- **V-fast `<Class>`** = `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --no-build --filter-class "*<Class>"` (after V-build)
- **V-core `<Class>`** = the same against `tests/Dexpace.Sdk.Core.Tests`
- **V-sec** = `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --no-build --filter-trait "Category=Security"` and the same for `tests/Dexpace.Sdk.Core.Tests`
- **V-conf** = V-fast `SystemNetConformanceTests`, then reads the printed report: the skipped rows are exactly the waivers that remain plus `Vacuous` rows
- **V-fmt** = `dotnet format Dexpace.Sdk.sln --verify-no-changes`
- **V-gate** = V-build, V-fmt, `dotnet test --solution Dexpace.Sdk.sln --configuration Release --no-build` (all suites, V-sec first), the Security diff check of convention 5, `git diff --stat main...HEAD -- Directory.Packages.props '*packages.lock.json'` empty, and the closing commands of the phase's last task

---

## Plan-level readings (where the design was silent, or the tree differs)

The design's rulings are not changed. **R1 to R5 are corrections of fact against the tree; R6 to R10 are additions.** Additions that add
a public member or behaviour are flagged for the lead.

- **R1 — A sync-only consumer in the AOT smoke would break under the real sync path.** `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`
  `CheckPipelineReworkAsync` drives `DefaultPipeline.Send` (sync) into `SystemNetHttpClient.Execute` over a `CannedHandler` that
  overrides only `SendAsync`. With `HttpClient.Send` that is G1 (`NotSupportedException`). Task 2.4 gives `CannedHandler` a `Send`
  override (same body), the one edit the design's test table does not list. The same grep finds `SystemNetHttpClientTests`'s
  `TokenObservingHandler` (the design lists it) and `ServerSentEventWireTests` (a real `SocketsHttpHandler`, fine).
- **R2 — 8a's plan already gives `ConformanceWaiver` the narrowing the design asks for.** 8a plan reading R3 adds optional `Assertion`
  and `Face` properties, evaluated per result. The design's request (2) is therefore met; only request (1), splitting
  `transport-14.inbound-lenient` into three assertions, is outstanding in 8a's task 2.11. Task 3.4 checks the merged kit at the start of PR 3: if the
  split is present, 8b only adds the waiver; if not, 8b makes the minimal kit edit (the split, with each assertion's negative control)
  in its own PR, which 8a's own rule permits for an assertion bug.
- **R3 — The waiver list is whatever 8a's first run measured, not the design's guess.** 8a plan task 2.13 sets the SystemNet
  `Owner = "8b"` waivers to the failing set (expected: `TRANSPORT-4`, `-5`, `-6`, `-8`, `-10`, `-12`, `-13`, `-14`, `-18`,
  `SEAM-15`). Task 0.1 prints the merged list; each 8b PR deletes the waivers its rows close and the run decides (a waiver left over is
  reported stale; a row that still fails after its task is a defect of the task, not a reason to re-add the waiver).
- **R4 — Core's `LogVocabularyTests` pins 110 to 119 as unused.** `Id_ranges_are_inside_the_reserved_blocks` asserts
  `DoesNotContain(ids.Values, id => id is (>= 110 and <= 119) or …)`; task 1.1 turns that clause into an `InRange(110, 119)` row for
  the four new ids and keeps the rest of the clause.
- **R5 — The logger's event names are the constants.** SystemNet's three `LoggerMessage.Define` fields use `new EventId(1, "FramingHeaderDropped")`
  and so on; the replacements use `new EventId(DexpaceLogEvents.TransportHeaderDroppedId, DexpaceLogEvents.TransportHeaderDropped)`,
  so a provider reports the stable dotted name (`OBS-39`).
- **R6 — `SystemNetHttpClientOptions` is a `sealed record` with explicit `init` accessors** (flagged: house shape, phase 5a). `with`
  therefore re-runs validation, and `Equals` is the generated one. `Proxy` is a reference to an immutable `ProxyOptions`.
- **R7 — `CallDeadline` is an internal `sealed class`, not a struct.** It owns two `CancellationTokenSource`s and a `TimeProvider`; a class
  keeps `IDisposable` semantics unambiguous and the allocation exists only when a deadline is armed (the null path allocates nothing,
  task 2.2 pin).
- **R8 — `HttpContent` has no synchronous `CreateContentReadStream` requirement for request content.** `RequestBodyContent` overrides
  `SerializeToStream(Stream, TransportContext?, CancellationToken)` and `TryComputeLength` only; `CreateContentReadStream` is never
  invoked by `SocketsHttpHandler` for a request, so it is not overridden (design §D).
- **R9 — Conformance hooks are test code.** `CreateWithProxy` and the handler-constructor subject live in
  `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs` (8a's file). Nothing in `src/` references the kit.
- **R10 — The owned-without-credentials "never re-sends" claim is pinned, not assumed.** Task 2.5 adds a wire row that sends a
  single-use body through the owned client with no proxy and asserts the capture object is never created (an internal **per-transport-instance** counter,
  `SystemNetHttpClient.CapturesCreated`, incremented when that instance builds a capture; never a static, because xUnit v3 runs test classes in parallel and a static would race), so the claim in design §F is a test.

---

## Task 0.1 — Pre-flight: queries, baseline and the facts to re-verify (no commit)

1. **Read what is known** (convention: start of the phase and of every numbered task):

   ```bash
   scripts/knowledge --origin note --brief
   scripts/knowledge --section conflicts --brief
   scripts/knowledge --prefix-info TRANSPORT
   scripts/knowledge --gaps TRANSPORT
   scripts/knowledge --topic transport-adapter --brief
   scripts/knowledge --req TRANSPORT-18   # and -30, -14, -5, -8 as each task starts
   ```

2. **Confirm the predecessor.** `git log --oneline main -8` shows 8a's PR 2 (the kit, the drivers). If it is absent, **stop**: 8b's
   execution cannot start (P8b-22); the plan is still valid. Record: the fixture's namespace; the exact `SystemNetConformanceTests`
   waiver list (R3); whether `transport-14.inbound-lenient` is split (R2); the member names of `TransportSubject`
   (`CreateAsync`, `CreateBlocking`, `CreateBorrowed`, `CreateWithProxy`, `AfterDispose`), `ConformanceWaiver` (`Id`, `Owner`,
   `Reason`, `Assertion`, `Face`) and `RecordedRequest` (the plan's snippets use these names; adapt to the merged kit).
3. **Baseline.** Create the stacked branch `89-phase-8b-construction-and-proxy` off `main` (no push). Run V-build and the whole suite once; record the
   passing count of `Dexpace.Sdk.Http.SystemNet.Tests`, the coverage floor and that V-conf is green with the waivers listed. Do this
   once; the machine is shared.
4. **Re-run the facts as throwaway tests** (a scratch class `Scratch/FactProbe.cs`, never committed; deleted at the end of the task),
   each as an `[Fact]` over a raw `TcpListener` peer or a stub handler on this host's SDK: **G1** (`HttpClient.Send` through a
   `SendAsync`-only handler throws `NotSupportedException`); **G2** (a `DelegatingHandler` overriding only `SendAsync` is silently skipped
   by `Send`); **G3** (disposing the token source after `SendAsync` returned with `ResponseHeadersRead` does not cut a later body read);
   **G4** (`CancelPendingRequests` during `Send` gives `TaskCanceledException` with an `IOException` inner; `HttpClient.Timeout`
   gives a `TimeoutException` inner); **G5/G6/G7** (HTTP forward proxy answering `407 Proxy-Authenticate: Basic`: the content is serialised
   twice for an `http://` target on every call, zero times for `https://`); **G8** (`RetryFacts.IsRetryableCause` treats an `IOException`
   anywhere in the `InnerException` chain as retryable; read `src/Dexpace.Sdk.Core/Resilience/RetryFacts.cs`). Also measure **R5 of the design**:
   what `authType` `SocketsHttpHandler` passes to `IWebProxy.Credentials.GetCredential` for a SOCKS5 proxy (a raw listener that
   completes the username/password sub-negotiation is enough), and whether SOCKS4 asks at all. Write the measured values into the
   task 1.5 test names. A fact that differs from the design is a **stop**: record it and amend the affected task before coding.
5. **Grep the sync consumers (R1).** `rg -n "\.Execute\(|\.Send\(" tests src --glob '*.cs'` and list every call that reaches
   `SystemNetHttpClient.Execute`; each handler under it needs a `Send` override in task 2.4.
6. **Open items for the lead** are listed at the end of this plan; none blocks execution (each task implements the design's chosen option).

---

# PR 1 — Construction and proxy

Rows closed: `TRANSPORT-1` (handler path), `TRANSPORT-30`, `SEAM-15`. Hand-offs taken: `CFG-28` scan, `DefaultProxy` ban, `Disposal`
decision. Kit: hook `CreateWithProxy` supplied on both subjects, handler-constructor subject added, `seam-15.after-dispose` waiver deleted (in task 1.6, with the change that closes it).

## Task 1.1 — Core: the four transport events in `DexpaceLogEvents` (`OBS-19`, `OBS-39`; P8b-13; R4)

**Files.** Edit: `src/Dexpace.Sdk.Core/Diagnostics/DexpaceLogEvents.cs`, `src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt`,
`tests/Dexpace.Sdk.Core.Tests/Diagnostics/LogVocabularyTests.cs`, `docs/sdk-documentation/logging-and-redaction.md` (line 65's "110-119 transport header drops (reserved for the transport conformance phase, OBS-19)" becomes "110-113 transport header drops and proxy warnings (phase 8b), 114-119 reserved").

**Tests first** (`LogVocabularyTests`, `Unit`): extend `Id_ranges_are_inside_the_reserved_blocks` with
`foreach (var name in new[] { "TransportHeaderDroppedId", "TransportFramingHeaderDroppedId", "TransportInboundHeaderDroppedId", "TransportProxyFeatureUnsupportedId" }) Assert.InRange(ids[name], 110, 119);`
plus exact values 110, 111, 112, 113, and narrow the "reserved and unused" clause to `114..119` (keep `141-149`, `155-159`, `161-169`).
`Event_names_and_ids_are_the_published_values` (no test counts `DexpaceLogEvents` names; `LogVocabularyTests` counts only `DexpaceLogKeys`, which is unchanged) gains the four names
`http.transport.header_dropped`, `http.transport.framing_header_dropped`, `http.transport.inbound_header_dropped`, `http.transport.proxy_feature_unsupported` with their ids 110 to 113. Red: compile error (missing constants).

**Production change.** Eight `const` members with `///` docs (name then id, in the file's existing style, each citing `TRANSPORT-n`/
`OBS-19`): the names and ids of the design's table (110 `TransportHeaderDropped`, 111 `TransportFramingHeaderDropped`, 112
`TransportInboundHeaderDropped`, 113 `TransportProxyFeatureUnsupported`). Remove "(reserved for the transport conformance phase, OBS-19)"
from the class remarks. SystemNet does not use them yet (tasks 1.5 and 3.3).

**PublicAPI.** Core: the eight lines from the design, ordinally sorted. **Verify.** V-build; V-core `LogVocabularyTests`. **IDs.** `OBS-19`,
`OBS-39`, `TRANSPORT-13` (vocabulary). **Commit.** `feat: reserve the transport log events 110-113 in DexpaceLogEvents`.

## Task 1.2 — Ban `HttpClient.DefaultProxy`; the `CFG-28` IL scan (`CFG-28`, `CFG-23`, `CFG-25`; P8b-4)

**Files.** New (tests): `tests/Dexpace.Sdk.Http.SystemNet.Tests/Architecture/NoImplicitEnvironmentReadArchitectureTests.cs`,
`tests/Dexpace.Sdk.Http.SystemNet.Tests/Architecture/IlCallScanner.cs`. Edit: `BannedSymbols.txt`.

**Tests first** (`Unit`).

- `IlCallScanner.FindCalls(Assembly)` walks every method and constructor body of every type (nested, generic and compiler-generated
  async state machines included, via `BindingFlags.DeclaredOnly | Instance | Static | Public | NonPublic`) with
  `MethodBody.GetILAsByteArray()`, decodes opcodes from a table built once from the `System.Reflection.Emit.OpCodes` fields (one- and
  two-byte forms; operand sizes by `OperandType`, `InlineSwitch` as `4 + 4n`), and for `call`/`callvirt`/`newobj`/`ldftn` operands
  resolves the token with `module.ResolveMethod(token, typeArgs, methodArgs)` (a failed resolution is skipped, not thrown). It returns
  `(Type Caller, MethodBase Callee)` pairs. Test-only reflection; `src/` stays reflection-free.
- `No_method_calls_the_environment_or_a_process_wide_default_proxy`: over `typeof(SystemNetHttpClient).Assembly`, the callee set
  `Environment.GetEnvironmentVariable` (both overloads), `Environment.GetEnvironmentVariables`, `ProxyOptions.FromEnvironment` (all three),
  `HttpClient.get_DefaultProxy`, `HttpClient.set_DefaultProxy`, `WebRequest.get_DefaultWebProxy`, `WebRequest.set_DefaultWebProxy` has exactly one
  permitted caller: `TraceContextStripping.ReadRuntimeSwitch`'s call to `GetEnvironmentVariable` (the runtime's own switch, P5c-11). Any other pair
  fails with `Caller.FullName -> Callee`.
- `The_scanner_finds_a_planted_call`: a nested test type calling `Environment.GetEnvironmentVariable("X")` and another reading
  `HttpClient.DefaultProxy` are found by `FindCalls(typeof(PlantedCalls).Assembly)` (proves the scanner can see a property getter and an
  async state machine body: plant one inside an `async Task` method).
- `The_permitted_caller_is_still_there`: asserts the one permitted call exists, so deleting `ReadRuntimeSwitch` or moving it cannot
  silently turn the scan vacuous.

The first class is **green on the current tree** (the owned handler reads nothing explicitly): a pin, proven able to fail by planting a
`HttpClient.DefaultProxy` read in `SystemNetHttpClient` for one run.

**Production change.** `BannedSymbols.txt`, under a new comment citing `CFG-23`/`CFG-25`/`CFG-27`/`CFG-28` and design §8.2:
`P:System.Net.Http.HttpClient.DefaultProxy;The runtime's process-wide proxy reads HTTP(S)_PROXY and NO_PROXY implicitly and diverges from the spec's bypass dialect; install a ProxyOptionsWebProxy (CFG-28, P8b-4)`.

**Verify.** V-build; V-fast `NoImplicitEnvironmentReadArchitectureTests`. **IDs.** `CFG-28` (5a's SystemNet hand-off), `CFG-23`. **Commit.** `test: scan the transport assembly for implicit environment and default-proxy reads; ban HttpClient.DefaultProxy`.

## Task 1.3 — `SystemNetHttpClientOptions` and `HeaderDropLogging` (`TRANSPORT-5`, `-13`, `-18`, `-30` settings; P8b-2; R6)

**Files.** New (src): `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClientOptions.cs`, `src/Dexpace.Sdk.Http.SystemNet/HeaderDropLogging.cs`. New
(tests): `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetHttpClientOptionsTests.cs` (`Unit`). Edit: `src/Dexpace.Sdk.Http.SystemNet/GlobalUsings.cs` only if
a namespace is needed project-wide (it should not be).

**Tests first** (`SystemNetHttpClientOptionsTests`).

- Defaults: `Logger` is `NullLogger.Instance`; `Proxy` null; `Timeout` is 100 s; `HeaderDropLogging` is `FirstPerName`; `ResendBufferLimit`
  is 1 048 576; `TimeProvider` is `TimeProvider.System`.
- Each `init` rule by theory: `Logger = null!` and `TimeProvider = null!` throw `ArgumentNullException` (param names `value` as the house
  options do; read `RetryOptions` for the exact pattern); `Timeout` of `TimeSpan.Zero`, `-1 ms`, `InfiniteTimeSpan` and `49 days + 1 ms` throw
  `ArgumentOutOfRangeException`, while `null`, `1 ms` and `49 days` are accepted; `HeaderDropLogging = (HeaderDropLogging)99` throws;
  `ResendBufferLimit = -1` throws, `0` is accepted.
- `with` cannot bypass validation: `new SystemNetHttpClientOptions() with { Timeout = TimeSpan.Zero }` throws.
- `Equals`/`GetHashCode` are value-based over all six members; two instances sharing the same `ProxyOptions` instance are equal.
- `ToString()` of an instance carrying `Proxy = new ProxyOptions { Host = "p", Port = 1, UserName = "alice", Password = "s3cret" }` contains neither
  `alice` nor `s3cret` (it delegates to `ProxyOptions.ToString`'s mask; if the generated `PrintMembers` prints the property through
  `ToString`, this passes with no code; if not, override `PrintMembers`).

**Production change.** The sealed record per the design table, with explicit `init` accessors and backing fields, `///` docs on every
member naming its rows (`Timeout`: `TRANSPORT-5`; `HeaderDropLogging`: `TRANSPORT-13`, `OBS-19`; `ResendBufferLimit`: `TRANSPORT-18`; `Proxy`:
`TRANSPORT-30`, `CFG-28`, with the migration line `Proxy = ProxyOptions.FromEnvironment()`; `TimeProvider`: `TRANSPORT-5`). The 49-day limit
is a private constant with a comment pointing at `DexpaceClientOptions`' identical limit (P6a-24). `HeaderDropLogging` is a three-member
enum with explicit values `FirstPerName = 0`, `Every = 1`, `VerboseOnly = 2` and a doc per member.

**PublicAPI.** The design's `HeaderDropLogging` and `SystemNetHttpClientOptions` lines plus the record's generated members, via the analyzer's code fix.
**Verify.** V-build; V-fast `SystemNetHttpClientOptionsTests`. **IDs.** `TRANSPORT-5`, `-13`, `-18`, `-30` (settings). **Commit.** `feat: add SystemNetHttpClientOptions`.

## Task 1.4 — The handler constructor's redirect check (`TRANSPORT-1`, `TRANSPORT-15`; P8b-3)

**Files.** New (src): `src/Dexpace.Sdk.Http.SystemNet/HandlerChain.cs` (internal static). New (tests):
`tests/Dexpace.Sdk.Http.SystemNet.Tests/HandlerConstructorTests.cs` (`Unit`, and the ctor assertions finish in task 1.6; this task tests `HandlerChain` directly).

**Tests first** (against `HandlerChain.ThrowIfFollowsRedirects(HttpMessageHandler)`).

- A `SocketsHttpHandler` with `AllowAutoRedirect = true` (the default) throws `ArgumentException` whose message contains `AllowAutoRedirect` and
  `TRANSPORT-1`, and whose `ParamName` is `handler`; with `false` it is accepted. Same for `HttpClientHandler`.
- The same two under one and two `DelegatingHandler` wrappers (test-defined subclasses of `DelegatingHandler` with `InnerHandler` set).
- A `DelegatingHandler` whose `InnerHandler` is `null` (the `IHttpClientFactory` shape) is accepted; an unknown primary (a test
  `HttpMessageHandler` subclass) is accepted.
- The check never mutates: `AllowAutoRedirect` on an accepted `SocketsHttpHandler` is still what it was; a rejected one is still `true`.
- A cyclic chain (`a.InnerHandler = b; b.InnerHandler = a` is impossible to set up with `DelegatingHandler`'s frozen-after-use rules; skip)
  is not tested; a chain longer than 32 delegating handlers is walked iteratively without recursion (a 1 000-deep chain completes).

**Production change.** An iterative walk of `DelegatingHandler.InnerHandler`; the terminal handler is tested with pattern matching
(`SocketsHttpHandler { AllowAutoRedirect: true }`, `HttpClientHandler { AllowAutoRedirect: true }`); no reflection. The message names the
member and the requirement, never a type name from user code beyond the handler's `GetType().Name` (a type name is not a secret).

**Verify.** V-build; V-fast `HandlerConstructorTests`. **IDs.** `TRANSPORT-1`, `TRANSPORT-15`. **Commit.** `feat: refuse a redirect-following primary handler`.

## Task 1.5 — The proxy adapter, the Basic-only credentials and the construction warnings (`TRANSPORT-30`, `CFG-22`, `CFG-23`, `CFG-27`; P8b-5; Q: R5)

**Files.** New (src): `ProxyOptionsWebProxy.cs`, `BasicOnlyCredentials.cs`, `ProxyWarnings.cs` (all internal sealed/static, in
`src/Dexpace.Sdk.Http.SystemNet/`). New (tests): `ProxyAdapterTests.cs` (`Unit`).

**Tests first** (`ProxyAdapterTests`).

- `GetProxy` per type: Http → `http://host:port/`, Socks4 → `socks4://host:port/`, Socks5 → `socks5://host:port/`; an IPv6 host (`::1`)
  is bracketed; a proxy configured with `UserName`/`Password` returns a URI whose `UserInfo` is empty and whose `ToString()` contains neither
  (`TRANSPORT-30`: no credential in the proxy URI).
- `IsBypassed`: `BypassAll` bypasses everything; `NonProxyHosts = ["*.internal.example.com"]` bypasses `a.internal.example.com` and not
  `internal.example.com.evil.test`; a leading-dot entry is a literal (P5a-14); the host compared is `IdnHost`; a loopback destination is not
  special-cased (the spec's dialect decides).
- `Credentials`: `ChallengeCredentials` set → that instance; `UserName` set, `Password` null → `BasicOnlyCredentials` answering a `NetworkCredential`
  with an empty password for `Basic` only; `GetCredential(uri, "Digest")`, `"NTLM"`, `"Negotiate"` return `null`; nothing configured → `null`;
  the setter throws `NotSupportedException` (nothing outside may inject a credential). For SOCKS the measured `authType` of task 0.1 is
  asserted (named in the test) and answered with the pair; for SOCKS4 only the user id is honoured.
- The three warnings (`ProxyWarnings.Emit(ILogger, ProxyOptions)`): `Password` on a SOCKS4 proxy; `ChallengeCredentials` on a SOCKS proxy;
  `ChallengeCredentials` together with `UserName`. Each logs exactly one `Warning` with `EventId.Id == 113` and
  `EventId.Name == "http.transport.proxy_feature_unsupported"` naming the feature and the `ProxyOptions` member; a recording logger's rendered text and structured
  state contain neither the user name, the password, nor the host. A clean configuration logs nothing.
- The adapter is stateless and thread-safe (one instance, `Parallel.For` 1 000 `GetProxy`/`IsBypassed` calls, no exception).

**Production change.** `ProxyOptionsWebProxy(ProxyOptions)` implementing `IWebProxy`; `BasicOnlyCredentials : ICredentials` with
`GetCredential(Uri, string)`; `ProxyWarnings` with three `LoggerMessage.Define<string>` delegates built on the core constants (R5), taking the
feature name from a closed set of string literals. All `internal`; no `PublicAPI` line. The adapter never reads the environment or
`HttpClient.DefaultProxy` (task 1.2 enforces).

**Verify.** V-build; V-fast `ProxyAdapterTests`; V-fast `NoImplicitEnvironmentReadArchitectureTests`. **IDs.** `TRANSPORT-30`, `CFG-22`, `CFG-23`, `CFG-27`, `XCUT-18`. **Commit.** `feat: add the proxy adapter over ProxyOptions`.

## Task 1.6 — Constructors, the one owned-client factory, `SEAM-15`, `NativeDisposal` (`TRANSPORT-1`, `-15`, `-16`, `-22`, `-30`, `SEAM-14`, `SEAM-15`; P8b-2..5, P8b-16, P8b-17) — **Breaking 1, 5, 8**

**Files.** Edit: `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs`, `src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt`. New (src): `NativeDisposal.cs`. Edit
(tests; also `SystemNetConformanceTests.cs`, deleting the `Owner = "8b"` waiver for `SEAM-15` in this same commit, because this task makes the row pass and 8a's stale-waiver rule would otherwise leave V-conf red): `HandlerConstructorTests.cs`, `SystemNetHttpClientOptionsTests.cs`, `ProxyAdapterTests.cs`, `SystemNetHttpClientOwnershipTests.cs`,
`SystemNetHttpClientDisposeTests.cs`, `SystemNetHttpClientSurfaceTests.cs` (read each; edit only a test that asserted the retired asymmetry).

**Tests first.**

- `HandlerConstructorTests` (now through the ctor): `new SystemNetHttpClient(handler)` over a redirect-off `SocketsHttpHandler` works and the handler is **never
  disposed** by `Dispose`/`DisposeAsync` (counting `DelegatingHandler`); the transport's own `HttpClient` is disposed once (the existing internal
  `OwnedClientReleases` counter); `new SystemNetHttpClient(redirectFollowingHandler)` throws `ArgumentException` (task 1.4's message); a null handler throws
  `ArgumentNullException`; `Proxy` set with a handler or with a borrowed `HttpClient` throws `ArgumentException` naming `Proxy` and `TRANSPORT-15`.
- `SystemNetHttpClientOptionsTests`: the options constructor stores nothing mutable the caller can change afterwards (`with` on the original after construction does
  not affect the transport: read through an internal `Settings` accessor).
- Owned-handler pins (through the internal `SystemNetHttpClient(Action<SocketsHttpHandler>, SystemNetHttpClientOptions)` seam, which is **added as an overload**; the existing one-argument
  `SystemNetHttpClient(Action<SocketsHttpHandler>)` seam stays and forwards with `new SystemNetHttpClientOptions()`, so its two callers, `Security/RedirectWireTests.cs:41` and `ServerSentEventWireTests.cs:197`, compile unedited and convention 5 holds):
  with no `Proxy`, `UseProxy == false` and `Proxy == null`, so the runtime's default is never consulted (**P8b-4**: set `HTTP_PROXY` for the test process is *not*
  needed because the seam inspects the handler); with `Proxy` set, `UseProxy == true`, `Proxy` is a `ProxyOptionsWebProxy`, `Credentials == null`,
  `UseDefaultCredentials == false`, `PreAuthenticate == false`, `AllowAutoRedirect == false`. (The owned `HttpClient.Timeout` is **not** touched here: it stays at its default until task 2.3 installs the transport deadline and the `InfiniteTimeSpan` pin, so PR 1 never ships a window with no timeout at all.)
- Construction warnings: constructing with an unhonourable `ProxyOptions` (task 1.5 combinations) logs the event-113 warning once to the options' logger.
- `SEAM-15` (`SyncExecuteTests`-style, `Unit`, new file `tests/Dexpace.Sdk.Http.SystemNet.Tests/AfterDisposeTests.cs`): after `Dispose` **every** construction (owned, handler,
  borrowed) throws `ObjectDisposedException` from `Execute`, and `ExecuteAsync` returns a **faulted task** (it does not throw synchronously; `TRANSPORT-21`); the borrowed
  `HttpClient` itself still works. Retire the `SystemNetHttpClientOwnershipTests` assertions that a borrowed *transport* serves after dispose only if one exists (the two
  existing borrowed tests call the borrowed `HttpClient` directly and stay).
- `NativeDisposal` (`Unit`): a throwing `IDisposable` disposed with a primary exception adds the secondary to `ExceptionTrail.GetSuppressed(primary)` and does not throw;
  a fatal exception (`OutOfMemoryException`) from `Dispose` is rethrown (`ExceptionFacts.IsFatal`); a clean dispose adds nothing.

**Production change.**

- Four new constructors per the design table (`(SystemNetHttpClientOptions)`, `(HttpMessageHandler)`, `(HttpMessageHandler, SystemNetHttpClientOptions)`,
  `(HttpClient, SystemNetHttpClientOptions)`); the four existing constructors become sugar (`(ILogger)` is `this(new SystemNetHttpClientOptions { Logger = logger })`;
  `(HttpClient, ILogger)` likewise); null arguments throw. One private constructor `(HttpClient, ownership, SystemNetHttpClientOptions)` stores the options-derived
  fields (logger, default timeout, drop-log mode, re-send limit, `TimeProvider`, a `_resendCapable` flag, the `Proxy` ownership). Re-send capability (design table): borrowed,
  handler-built, or owned with `Proxy.UserName`/`ChallengeCredentials` set.
- `CreateOwnedClient` is the one place `HttpClient` is constructed: a handler overload `CreateOwnedClient(HttpMessageHandler handler, bool disposeHandler)` for
  the handler constructor (`new HttpClient(handler, disposeHandler: false)`; `Timeout = Timeout.InfiniteTimeSpan` is added to both overloads in task 2.3), and the existing owned overload sets `UseProxy`/`Proxy`
  from the options. The `#pragma warning disable RS0030` stays, now around both overloads, and its comment cites P8b-3.
- `SEAM-15`: `ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);` as the first statement of `ExecuteAsync` (inside the `async` body, so it faults the
  task) and `Execute`.
- `NativeDisposal.DisposeQuietly(IDisposable, Exception primary)` (internal static) replaces the guard's `response.Dispose()` in `ExecuteAsync` (and, from task 2.4, `Execute`).
- Class remarks: replace the "After dispose (SEAM-15)" paragraph (no asymmetry any more), add a **Breaking** paragraph for the proxy (item 1) and the handler
  constructor (item 8), and a construction table. Keep the `Redirects` paragraph.

**PublicAPI.** The design's four constructor lines. **Breaking.** 1 (no implicit proxy; the owned `HttpClient.Timeout` is unchanged until 2.3), 5 (`ObjectDisposedException` for every construction), 8 (handler constructor rejects a following primary).
**Verify.** V-build; V-fast `HandlerConstructorTests`, `SystemNetHttpClientOptionsTests`, `ProxyAdapterTests`, `AfterDisposeTests`, `SystemNetHttpClientDisposeTests`, `SystemNetHttpClientOwnershipTests`,
`SystemNetHttpClientSurfaceTests`, V-conf (green: `seam-15.after-dispose` passes with its waiver already deleted), then V-sec (the redirect and header classes are unedited and green). **IDs.** `TRANSPORT-1`, `-15`, `-16`, `-21`, `-22`, `-30`, `SEAM-14`, `SEAM-15`, `CFG-28`.
**Commit.** `feat!: SystemNetHttpClientOptions, handler constructor, explicit proxy and ObjectDisposedException after dispose`.

## Task 1.7 — `ProxyCredentialWireTests`, the new `Security` class (`TRANSPORT-30`'s MUST, `XCUT-18`, `CFG-22`; P8b-5, P8b-20)

**Files.** New (tests): `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security/ProxyCredentialWireTests.cs` (`[Trait("Category", "Security")]`, loopback). Uses the existing
`RecordingLogger` in the same folder and the fixture as a **forward proxy** (an `http://` target is sent in absolute form to a loopback "proxy" that answers `407
Proxy-Authenticate: Basic realm="p"` until it sees `Proxy-Authorization`, then `401 WWW-Authenticate: Basic` for the "origin"; keep-alive replies).

**Tests first** (red until 1.6 is in; written against it, so red evidence is the pre-1.6 behaviour of the same class run on the previous commit, recorded in the checklist).

- `The_proxys_407_is_answered_with_the_configured_basic_pair`: owned transport with `Proxy = {Host, Port, UserName, Password}`, `GET http://origin.invalid/x`: the proxy
  sees a first request without `Proxy-Authorization` and a second with `Basic base64(user:password)`; the call ends with the origin's `401` as an ordinary response.
- `An_origin_401_behind_the_proxy_is_never_answered_with_the_proxy_credential`: the transport delivers the `401` to the caller; the proxy sees **no** `Authorization`
  header on any request; no request carries the pair in any header other than `Proxy-Authorization`; the fixture saw exactly two requests (no retry of the `401`).
- `A_digest_or_ntlm_407_is_not_answered_by_the_basic_fallback`: the proxy challenges `Digest`; the pair is not sent; the `407` is returned.
- `No_log_entry_at_any_level_contains_the_user_name_or_password`: a `RecordingLogger` passed through the options sees the whole exchange plus construction; none of its
  entries, rendered, contains the user name or password (use distinctive values such as `u-7f3a` and `p-91c2`).
- `A_challenge_credentials_hook_wins_over_the_pair`: `ChallengeCredentials` returning a different pair is the one sent.
- `The_proxy_uri_given_to_the_runtime_has_no_userinfo`: through the internal seam, `handler.Proxy.GetProxy(dest).UserInfo` is empty (belt and braces for task 1.5).

**Production change.** None (this task is the permanent regression net for 1.5 and 1.6). **Verify.** V-build; V-fast `ProxyCredentialWireTests`; prove one test can fail by temporarily setting
`handler.Credentials = proxyPair` in `CreateOwnedClient` (the leak variant) and watching `An_origin_401…` fail; revert. **IDs.** `TRANSPORT-30` (MUST), `XCUT-18`, `CFG-22`. **Commit.** `test: ProxyCredentialWireTests`.

## Task 1.8 — Conformance driver: `CreateWithProxy`, the handler-constructor subject, waivers (`TRANSPORT-1`, `-30`, `SEAM-15`; P8b-21)

**Files.** Edit: `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetConformanceTests.cs`, `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs` (8a's files).

**Tests first.** Re-run V-conf before editing and see `transport-30.proxy-discoverable-no-leak` `NotExercised` (the `SEAM-15` waiver was already deleted in task 1.6).

**Production change (test code).**

- Set `CreateWithProxy` on the owned subject: given the kit's forward-proxy server and credentials, return
  `new SystemNetHttpClient(new SystemNetHttpClientOptions { Logger = s.Logger, Proxy = new ProxyOptions { Host = …, Port = …, UserName = …, Password = … } })` for both faces, with the kit's expectations
  of the hook (read its XML docs; the proxy server's address comes from the hook's argument).
- Add a second subject `SystemNetSubject.CreateViaHandler()` named `Dexpace.Sdk.Http.SystemNet (handler constructor)`: `CreateAsync`/`CreateBlocking` =
  `new SystemNetHttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }, new SystemNetHttpClientOptions { Logger = s.Logger })`, `AfterDispose =
  ThrowsObjectDisposedException`, the same waivers as the owned subject (they close together), `CreateBorrowed`/hooks as the owned subject where they apply. `CreateWithProxy` on this subject is **not** supplied through the transport's `Proxy` option (a handler plus `Proxy` throws, `P8b-3`/`P8b-4`); instead it builds the transport over a test `SocketsHttpHandler { AllowAutoRedirect = false }` whose `Proxy` is a test-local `IWebProxy` pointing at the kit's forward-proxy server and whose `Credentials` are `NetworkCredential`s of the supplied pair, so `transport-30` is exercised on this subject too and the no-leak assertion still applies to it. Add the three driver
  members for it (`Assertion_holds`, `The_whole_suite_is_green…`, `Only_8b_waivers_remain`), parameterised over the two subjects rather than copied.
- The `transport-1.redirect-not-followed` row now also runs through the handler constructor.

**Verify.** V-build; V-conf: `transport-30` is `Passed` on both subjects, `transport-1` and `seam-15` pass on both subjects, the remaining skips are the other 8b waivers. **IDs.** `TRANSPORT-1`, `TRANSPORT-30`, `SEAM-15`. **Commit.** `test: conformance subjects for the handler constructor and the proxy`.

## Task 1.9 — PR 1 gate

Run V-gate; additionally `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages` after `dotnet pack` (must be unchanged: SystemNet still references only core), and `git diff --stat main...HEAD --
Directory.Packages.props '*packages.lock.json'` (empty). Check V-sec output lists the five existing classes plus `ProxyCredentialWireTests`, all passing. Update the CHANGELOG draft notes
in the PR description (the real entry is task 4.5). Stop; no push.

---

# PR 2 — The send path

Rows closed: `TRANSPORT-4`, `-5`, `-6`, `-8`, `-9`, `-17`, `-18`. Hand-offs taken: `SEAM-11`, `PIPE-28` (real sync terminal). Kit: waivers `transport-4.*`, `-5.*`, `-6.*`, `-8.*`, `-18.*` deleted.

## Task 2.1 — `SendFailures`: one classification helper for both faces (`TRANSPORT-4`, `-8`, `-20`, `XCUT-1`, `XCUT-2`; P8b-7; G4, G8)

**Files.** New (src): `src/Dexpace.Sdk.Http.SystemNet/SendFailures.cs` (internal static). New (tests): `FailureClassificationTests.cs` (`Unit`).

**Tests first** (the design's table, six rows, driven through the helper directly with constructed exceptions, then once end to end in 2.3/2.4).

`SendFailures.Map(Exception native, CancellationToken caller, bool deadlineFired)` returns the exception to throw (the same instance for table row 1).

- Row 1: caller signalled, `TaskCanceledException` → the same instance (and a call with the caller signalled **and** the deadline fired still returns the caller's: order).
- Row 2: deadline fired, `TaskCanceledException` or `HttpRequestException` → `ServiceRequestTimeoutException` (`IsRetryable`), `InnerException` is the native exception.
- Row 3: `TaskCanceledException` whose inner is a `TimeoutException`, caller unsignalled, deadline not fired → `ServiceRequestTimeoutException`.
- Row 4: `TaskCanceledException` with an `IOException` inner (G4's `CancelPendingRequests` shape), caller unsignalled, deadline not fired → a **new** `OperationCanceledException`
  whose `CancellationToken` is `None`, message contains "not a timeout", whose `InnerException` is **null**, and whose `ExceptionTrail.GetSuppressed` contains the native exception. The same for a plain
  `OperationCanceledException`.
- Row 4 being terminal under the real classifier (G8) and rows 2, 3 and 5 being retried are **pipeline-level** assertions that need the wiring of 2.3 (async) and 2.4 (sync); they are written in those tasks (see 2.3's `FailureClassificationTests` rows), not here. Task 2.1 keeps only the direct `SendFailures.Map` / `IsTransportFailure` rows.
- Row 5: `HttpRequestException` → `ServiceRequestException`, retryable, inner = native.
- Pass-through: `ObjectDisposedException`, `NotSupportedException`, `InvalidOperationException`, and `StreamConsumedException` (which `RequestBodyContent` never lets escape raw, task 2.5) are not mapped (`Map` is only called by a `catch … when (SendFailures.IsTransportFailure(ex))` filter that matches
  exactly `OperationCanceledException` and `HttpRequestException`; test the filter).
- The message text never includes the request URL or a header value (`XCUT-18`).

**Production change.** The helper per the design table, with `ExceptionTrail.AddSuppressed(mapped, native)` for row 4. Both faces will call
`catch (Exception ex) when (SendFailures.IsTransportFailure(ex)) { var mapped = SendFailures.Map(ex, ct, deadline.Fired); if (ReferenceEquals(mapped, ex)) { throw; } throw mapped; }`.

**Verify.** V-build; V-fast `FailureClassificationTests`. **IDs.** `TRANSPORT-4`, `-8`, `-20`, `XCUT-1`, `XCUT-2`. **Commit.** `feat!: classify transport failures once for both faces` — **Breaking 4** lands here in the helper but takes effect in 2.3; mark it in 2.3.

## Task 2.2 — `CallDeadline` (`TRANSPORT-5`, `-6`; P8b-6; G3; R7)

**Files.** New (src): `src/Dexpace.Sdk.Http.SystemNet/CallDeadline.cs`. New (tests): `DeadlineTests.cs` (`Unit`, `FakeTimeProvider`).

**Tests first.**

- `Clamp`: `TimeSpan.FromTicks(1000)` (100 µs) → 1 ms; `TimeSpan.FromMilliseconds(1)` → 1 ms; `49 days` stays; `50 days` → 49 days; `TimeSpan.MaxValue` → 49 days (`TRANSPORT-6`).
- `CallDeadline.Start(null, tp, token)` allocates no token source: a counting `TimeProvider` (subclass of `FakeTimeProvider` overriding `CreateTimer`) sees zero timers and `Token == callerToken`
  (reference-equal default/`CanBeCanceled` semantics asserted through `Token == token`); disposing it twice is safe.
- `Start(100 ms, fake, token)`: `Fired` is false; `fake.Advance(99 ms)` leaves `Token` unsignalled; `Advance(1 ms)` signals it and `Fired` is true; the caller's token is **untouched**
  (a separate source: `callerToken.IsCancellationRequested` stays false).
- A caller cancel before the deadline signals `Token` with `Fired == false`.
- Two concurrent deadlines (`50 ms` and `500 ms`) advance independently on one fake clock.
- After `Dispose`, advancing the clock past the deadline does not signal anything and does not throw (G3's consequence: the source is disposed when `SendAsync` returns).
- Effective-timeout selection (`CallDeadline.Effective(RequestOptions, TimeSpan? transportDefault)`): per-call value wins; per-call `null` → the transport default; both `null` → `null`.

**Production change.** `CallDeadline : IDisposable` (R7) with `Start`, `Token`, `Fired`, `Dispose`, `Clamp`, `Effective`; the timer is `new CancellationTokenSource(clamped, timeProvider)` and the linked source is
`CancellationTokenSource.CreateLinkedTokenSource(caller, timer.Token)`; no `Task.Delay`, no wall clock. Documented limit: a borrowed client's own `Timeout` stays an outer bound that a longer per-call value cannot lift (R1 of the design).

**Verify.** V-build; V-fast `DeadlineTests`. **IDs.** `TRANSPORT-5`, `-6`. **Commit.** `feat: add the per-call deadline`.

## Task 2.3 — Wire the deadline and the classification into `ExecuteAsync`; split the method (`TRANSPORT-4`, `-5`, `-6`, `-8`, `-9`, `SEAM-11`; P8b-6..8) — **Breaking 3, 4**

**Files.** Edit: `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs`; tests: `SystemNetHttpClientTests.cs` (retire `Options_are_accepted_and_ignored_until_8b`), `DeadlineTests.cs` (end-to-end rows). New
(tests): `TimeoutWireTests.cs` (`Integration`). Edit (driver): `SystemNetConformanceTests.cs`.

**Tests first.**

- `DeadlineTests` end to end over a stub handler that waits on the token it is given: a call with `RequestOptions { Timeout = 100 ms }` and a `FakeTimeProvider`-armed transport
  (`TimeProvider = fake`) completes with `ServiceRequestTimeoutException` after `fake.Advance(100 ms)`, the caller token is not cancelled; `Timeout = null` uses the transport's 100 s default (advance
  99 s: pending; 1 s: timeout); a transport built with `Timeout = null` and a per-call `null` never times out (the stub completes normally); the same options on two concurrent calls do not leak
  (call 1 `100 ms` times out, call 2 `null` still succeeds against the default).
- Remove `Options_are_accepted_and_ignored_until_8b`; its intent (options accepted) is covered above.
- `TimeoutWireTests` (`Integration`, loopback, **real** `TimeProvider`): a server that never answers plus `Timeout = 300 ms` fails as a retryable `ServiceRequestTimeoutException` within the kit's 30 s bound;
  `Timeout = 100 µs` (sub-resolution) also times out and does not hang (`TRANSPORT-6`); a call with a generous timeout against a prompt server succeeds, and the **body is still readable after the
  deadline source was disposed** (G3: a gated second chunk arrives after the call returned); a caller cancel mid-wait is an `OperationCanceledException`, not a timeout; waits use the fixture's
  condition waits, not sleeps.
- `FailureClassificationTests` pipeline rows (moved from 2.1; this is where `SendFailures` is first wired in): build the pipeline with `new PipelineBuilder().AddStandardResilience(fakeTimeProvider, logger).Build(transport, new DexpaceClientOptions { Retry = new RetryOptions { MaxRetryAttempts = 3 } })`
  (or set `RequestOptions { MaxRetries = 3 }` per call, RETRY-41; `DexpacePipeline.CreateDefault` takes no options and defaults to 2 retries, three sends; always pass a fake or instant `TimeProvider` so backoff does not run on the wall clock). Over a stub handler throwing the row-4 native shape assert **one** send (G8: terminal);
  a row-2/3 timeout and a row-5 `HttpRequestException` are retried to the limit (**four** sends with `MaxRetryAttempts = 3`). (`Dexpace.Sdk.Core.Tests` may not reference a transport, so this lives in the SystemNet test project.)
- A borrowed `HttpClient { Timeout = 150 ms }` over the same silent server: a `TaskCanceledException` with a `TimeoutException` inner is a retryable `ServiceRequestTimeoutException` (row 3).
- `CancelPendingRequests` on a borrowed client during a call (stub handler awaiting its token) completes with a plain `OperationCanceledException` that the default pipeline does not retry.

**Production change.** `ExecuteAsync` is split (`MA0051`) into a thin public method and private `SendNativeAsync` / `AdaptGuarded`:

```csharp
public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(options);
    ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    using var message = BuildMessage(request);
    var sentUri = message.RequestUri!;
    HttpResponseMessage native;
    using (var deadline = CallDeadline.Start(CallDeadline.Effective(options, _settings.Timeout), _settings.TimeProvider, cancellationToken))
    {
        try
        {
            native = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (SendFailures.IsTransportFailure(ex))
        {
            var mapped = SendFailures.Map(ex, cancellationToken, deadline.Fired);
            if (ReferenceEquals(mapped, ex))
            {
                throw;
            }

            throw mapped;
        }
    }

    return AdaptGuarded(native, sentUri, request);
}
```

(`AdaptGuarded` is the shared `ThrowIfRedirected` + `ToResponse` guard with `NativeDisposal.DisposeQuietly(native, ex)`.) The "ignored until phase 8b" sentences leave the class remarks; the `RequestOptions` remark documents the
deadline's scope (until the response headers; the body read is bounded by the caller's token) and the borrowed-client outer bound. `TRANSPORT-9`: no `WaitAsync`, `WhenAny` or `TaskCompletionSource<Response>` exists
in the package (an architecture assertion is added to `NoImplicitEnvironmentReadArchitectureTests`' sibling file in 2.6).

**HttpClient.Timeout.** In this task, and not before (task 1.6 left it alone), `CreateOwnedClient`'s two overloads set `Timeout = Timeout.InfiniteTimeSpan`, and `HandlerConstructorTests` gains the pin "the owned `HttpClient.Timeout` is `InfiniteTimeSpan`"; the transport's 100 s default (`CallDeadline`) replaces it in the same commit, so the observable bound never lapses.

**Also.** `tests/Dexpace.Sdk.Http.SystemNet.Tests/PaginationWireTests.cs`: update its remarks (lines 26 to 29 and 37) that cite the retired `Options_are_accepted_and_ignored_until_8b` and say `Execute` "still blocks on its async send until phase 8b" (non-Security file; the async half goes stale here, the sync half in 2.4).

**Driver.** Delete the `Owner = "8b"` waivers for `TRANSPORT-4`, `-5`, `-6`, `-8` in `SystemNetConformanceTests.cs`.

**Breaking.** 3 (`RequestOptions.Timeout` is enforced; owned `HttpClient.Timeout` infinite with the transport's 100 s default, taking effect in this task), 4 (internal cancel is terminal), marked in the remarks.
**Verify.** V-build; V-fast `DeadlineTests`, `FailureClassificationTests`, `TimeoutWireTests`, `SystemNetHttpClientTests`; V-conf (rows `transport-4.*`, `-5.*`, `-6.*`, `-8.*` `Passed`; `transport-9` still `Passed`); V-sec.
**IDs.** `TRANSPORT-4`, `-5`, `-6`, `-8`, `-9`, `SEAM-11`, `XCUT-2`. **Commit.** `feat!: enforce RequestOptions.Timeout and classify native cancellation`.

## Task 2.4 — The real synchronous path (`TRANSPORT-17`, `SEAM-11`, `PIPE-28`, `TRANSPORT-5`; P8b-9; G1, G2; R1, R8) — **Breaking 2**

**Files.** Edit: `SystemNetHttpClient.cs`, `RequestBodyContent.cs`. Edit (tests): `PaginationWireTests.cs` (remove the "still blocks on its async send until phase 8b" remark, now false), `SystemNetHttpClientTests.cs` (`TokenObservingHandler` gains `Send`), `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`
(`CannedHandler` gains `Send`; R1). New (tests): `SyncExecuteTests.cs` (`Unit`), `SyncExecuteWireTests.cs` (`Integration`).

**Tests first.**

- `SyncExecuteTests`: `Execute_runs_through_a_handler_that_implements_only_Send` (its `SendAsync` throws `InvalidOperationException`; a response returns, proving no sync-over-async); the converse G1 row
  (an async-only primary handler → `NotSupportedException`, **unwrapped**, not an `SdkException`); G2 documented by a test that an async-only `DelegatingHandler` is skipped on the sync face and
  honoured on the async face (so the asymmetry is pinned, not lost); the sync face applies the same deadline (`Timeout = 100 ms` + `FakeTimeProvider` over a `Send` that waits on its token → retryable timeout), the
  same classification (rows 1 to 5, reusing `FailureClassificationTests`' native shapes through `Send`), the same redirect-detection throw, the same `NativeDisposal` guard (a response whose
  `Dispose` throws after an adaptation failure keeps the primary and carries the secondary on its trail); a request body is written on the **calling thread** (`Thread.CurrentThread.ManagedThreadId` recorded in
  `WriteTo` equals the caller's) exactly once; a `RequestBody` that does not override `WriteTo` fails the send with `NotSupportedException` (P3a-5).
- `FailureClassificationTests` sync rows: the same pipeline-level rows as 2.3 (row 4 one send; rows 2, 3, 5 retried) driven through `Execute` over a `Send`-overriding stub, using `CreateBlocking`/the sync pipeline entry with a fake `TimeProvider`.
- Rewrite `The_sync_Execute_passes_the_token_through`'s handler to override `Send(HttpRequestMessage, CancellationToken)` (with the same `ThrowIfCancellationRequested`) and keep `SendAsync` for the async tests.
- `SyncExecuteWireTests` (`Integration`): a sync `GET` and a sync `POST` with a single-use stream body over the loopback server: status, headers, body match the async face; the single-use body's source stream was read
  **once** (`CountingStream`; `TRANSPORT-17`); the sync `traceparent` strip row mirrors `TracePropagationWireTests` (5c's inherit-only hand-off; uses the `TracePropagationCollection`); the blocking pager (`Pageable.CreateBlocking`)
  walks two pages through `Execute` with a per-call timeout (7c's inherit-only hand-off).

**Production change.**

- `Execute` calls `_client.Send(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token)` with the shared deadline, classification and guarded adaptation; the `#pragma warning disable RS0030, CA2000` block is
  deleted; no `GetAwaiter().GetResult()` remains in the package (V-gate greps for it).
- `RequestBodyContent` overrides `SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)` as `_body.WriteTo(stream, cancellationToken)` (R8); the existing async overrides stay.
- Class remarks and the README state G1 (loud) and G2 (silent) as the documented sync hazard; **Breaking 2** is marked.
- `SmokeChecks.CannedHandler` gains `protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => Build(request);` (R1).

**Verify.** V-build; V-fast `SyncExecuteTests`, `SyncExecuteWireTests`, `SystemNetHttpClientTests`, `ServerSentEventWireTests`, `PaginationWireTests`; V-conf (`transport-17.single-use-written-once` passes on both faces over the real sync path);
`dotnet build tests/Dexpace.Sdk.AotSmoke` is part of V-build and the AOT publish is run in task 4.3. **IDs.** `TRANSPORT-17`, `SEAM-11`, `PIPE-28`, `TRANSPORT-5`. **Commit.** `feat!: make Execute genuinely synchronous`.

## Task 2.5 — `ResendCapture`: bounded tee-and-replay of a single-use body (`TRANSPORT-18`, `TRANSPORT-17`; P8b-10; G5..G7; R10) — **Breaking 9**

**Files.** New (src): `src/Dexpace.Sdk.Http.SystemNet/ResendCapture.cs`, `CapturingStream.cs`. Edit: `RequestBodyContent.cs`, `SystemNetHttpClient.cs` (builds the content with a capture when `_resendCapable`; exposes the internal per-instance `CapturesCreated`). New
(tests): `ResendCaptureTests.cs` (`Unit`), `ResendCaptureWireTests.cs` (`Integration`).

**Tests first.**

- `ResendCaptureTests` (stub handler that calls `base.SendAsync`/`Send` twice, the native re-send shape of 8a's `CreateWithNativeResend`): a 1 MiB non-seekable stream body, limit 1 MiB: both serialisations produce
  identical bytes and the source was read **once**; the same on the sync face; a body one byte over the limit: the **first** serialisation succeeds (a first send must not fail because capture would overflow), the second
  throws an `IOException` whose message names `ResendBufferLimit` and `ToReplayableAsync` and whose `InnerException` is the `StreamConsumedException`. **Correction to the design (§F):** `HttpClient` does *not* wrap an arbitrary exception thrown from content serialisation
  (probed on SDK 10.0.401: a custom `Exception` and `NotSupportedException` arrive unwrapped on both faces; only an `IOException` becomes `HttpRequestException` with the `IOException` inner), so a raw `StreamConsumedException` would bypass `SendFailures` (its filter is `OperationCanceledException` and `HttpRequestException` only). `RequestBodyContent` therefore throws
  `new IOException(message, new StreamConsumedException(...))`; `HttpClient` wraps it, row 5 maps it to a `ServiceRequestException` (the transport-failure type, `TRANSPORT-18`; never a truncated body). Retry safety is unaffected: `RetryFacts.IsResendable` still gates on `IsReplayable`, which is false for the body.
  A test pins the exact chain `ServiceRequestException -> HttpRequestException -> IOException -> StreamConsumedException` on both faces. `ResendBufferLimit = 0` fails every re-send of a non-empty single-use body the same way, and an empty body re-sends; a source that throws
  mid-write fails the first send with the source's own exception (unwrapped if not an `IOException`, as `HttpClient` leaves it; pinned), and a re-send afterwards fails with the wrapped `StreamConsumedException` chain above; replayable bodies (`FromBytes`, `FromString`, seekable stream with a declared length, `FromFile`) are
  **never captured** (R10: the transport instance's internal `CapturesCreated` counter stays 0); the capture is released when the content is disposed (chunk list cleared; asserted through an internal
  `Released` flag).
- `Capture_is_skipped_for_the_owned_client_without_proxy_credentials` (R10): owned transport, no `Proxy`, single-use body: that instance's `CapturesCreated == 0`; owned transport with `Proxy.UserName` set: `== 1`;
  handler-built and borrowed constructions: `1`. Each assertion reads its own transport instance; no static counter exists.
- `ResendCaptureWireTests` (`Integration`): the fixture as a forward proxy answering `407 Basic` then `200` (G5/G6): an owned transport with proxy credentials sends a 100 KiB single-use `http://` upload, the proxy
  sees two identical bodies, the caller's stream was read once, the call succeeds; a body over a lowered `ResendBufferLimit` fails with the named message; the `https://` target never re-sends (G7: assert one body
  copy at the proxy's `CONNECT` stage only if the fixture can terminate TLS; otherwise cover G7 in task 0.1's probe and note it here).

**Production change.** `ResendCapture` is an unpooled chunk list (`IO-22`, the `ArrayPool` ban applies; use `byte[]` chunks of at most 80 000 bytes, never `ArrayPool`), a state (`NotStarted`, `Capturing`,
`Complete`, `Unusable`), the limit and a running total. `CapturingStream` is a write-through `Stream` wrapper (all `Write`/`WriteAsync` overloads, `CanRead` false, `CanSeek` false) that appends to the capture
until the limit, then marks it `Unusable` and keeps writing through. `RequestBodyContent` takes `(RequestBody body, ResendCapture? capture)`: with no capture or a replayable body it behaves as today; on the
first serialisation it wraps the destination and, after `WriteTo`/`WriteToAsync` returned normally, completes the capture; later serialisations write the chunks if `Complete` else throw the named
`StreamConsumedException` wrapped in an `IOException` (see the correction above). `SystemNetHttpClient` increments its internal `CapturesCreated` each time it builds a capture. The sync and async twins share one private state machine (`BeginSerialisation()` returning an enum) so the two faces cannot diverge. Both overloads respect the token.

**Verify.** V-build; V-fast `ResendCaptureTests`, `ResendCaptureWireTests`, `SyncExecuteWireTests`. **Driver.** Delete the `transport-18` waiver; V-conf: `transport-18.native-resend-identical` passes (`CreateWithNativeResend`).
**Breaking.** 9. **IDs.** `TRANSPORT-18`, `TRANSPORT-17`, `TRANSPORT-2`. **Commit.** `feat!: replay a single-use body on a native re-send, bounded`.

## Task 2.6 — `TRANSPORT-9` and `TRANSPORT-28` pins; the structural architecture test (`TRANSPORT-9`, `-28`; P8b-8, P8b-15)

**Files.** New (tests): `Architecture/NoAbandonedResponseArchitectureTests.cs` (`Unit`), `FileRequestBodyReplayTests.cs` (`Unit`). No production change.

**Tests first (pins; all green on the current tree).**

- The IL scanner of task 1.2 finds no call to `Task.WaitAsync`, `Task.WhenAny`, `Task.WhenAll`, `Task.Delay`, or any constructor of `TaskCompletionSource<Response>` (by generic argument name) in the SystemNet assembly: the
  structural reading of `TRANSPORT-9` (P8b-8). Self-test: a planted `WhenAny` is found.
- `FileRequestBodyReplayTests`: `RequestBody.FromFile(path, type, offset: 3, count: 10)` sent twice through a stub handler (both faces) puts the **same ten bytes** on both wires; `IsReplayable` is true;
  `TryComputeLength` reports 10 (`TRANSPORT-28`'s MUST half); a native re-send of it is not captured (that transport instance's `CapturesCreated` stays 0, task 2.5); zero-copy is *not* asserted (declined, P8b-15).
- Prove the architecture pin can fail by planting `Task.WhenAny` in `SystemNetHttpClient` for one run.

**Verify.** V-build; V-fast `NoAbandonedResponseArchitectureTests`, `FileRequestBodyReplayTests`; V-conf (`transport-9`, `transport-28` `Passed`). **IDs.** `TRANSPORT-9`, `TRANSPORT-28`. **Commit.** `test: pin that the transport abandons no response and replays file ranges`.

## Task 2.7 — PR 2 gate

V-gate; additionally: `rg -n "GetAwaiter\(\)\.GetResult\(\)|#pragma warning disable RS0030" src/Dexpace.Sdk.Http.SystemNet` finds nothing except the owned-client factory's `RS0030` pragma (exit criterion 3);
V-conf lists no `transport-4/5/6/8/18` waiver; the coverage gate (`dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` after a clean coverlet run, per `CLAUDE.md`); `scripts/ci/reproducible-pack.sh` (two packs,
byte-compare). Stop; no push.

---

# PR 3 — Headers

Rows closed: `TRANSPORT-10`, `-12`, `-13`, `-14` (value and obs-text half). Hand-off taken: `OBS-19`. Kit: waivers `transport-10.*`, `-12.*`, `-13.*` deleted in 3.2 (with the change that closes them), the `transport-14` value waiver deleted in 3.4, the name clause
permanently waived.

## Task 3.1 — `HeaderDropLog`: the policy and the bounded seen-set (`TRANSPORT-13`, `OBS-19`, `OBS-20`, `XCUT-18`; P8b-12; R5)

**Files.** New (src): `src/Dexpace.Sdk.Http.SystemNet/HeaderDropLog.cs` (internal sealed). New (tests): `HeaderDropLogTests.cs` (`Unit`).

**Tests first.**

- `FirstPerName`: the first drop of `X-A` is `Warning`, the second `Debug`; `x-a` (different case) is the same name; `X-B` is a new first (`Warning`). `Every`: `Warning` each time. `VerboseOnly`: `Debug` each time,
  never `Warning`.
- Events: id 110, name `http.transport.header_dropped`; the framing drop (id 111, `Debug`, name `http.transport.framing_header_dropped`) and the inbound drop (id 112, `Debug`, `http.transport.inbound_header_dropped`) are never
  subject to the policy and never touch the seen-set.
- The seen-set is capped at 128 names, oldest evicted: 10 000 distinct names leave the set at 128 (internal `SeenCount`), and after eviction the first name warns again; a repeat of a name still inside the cap stays `Debug`.
- Concurrency: 64 threads dropping the same new name concurrently produce exactly one `Warning`; 64 threads over 1 000 names do not corrupt the set (count ≤ 128, no exception).
- Names are escaped through `HttpHeaderSyntax.EscapeName` (a name carrying a control character logs its escaped form); the structured state and the rendered text carry **no value** (the drop API takes a name only).
- A throwing logger is swallowed (`OBS-20`): `Log` throws → the drop call returns, the send is unaffected.
- `IsEnabled(Debug) == false` loggers pay no formatting (the delegates use `LoggerMessage.Define`).

**Production change.** `HeaderDropLog(ILogger, HeaderDropLogging)` with `Dropped(string name)`, `FramingDropped(string)`, `InboundDropped(string)`; a lock-guarded `HashSet<string>(OrdinalIgnoreCase)` plus `Queue<string>`; the
three `LoggerMessage.Define<string>` delegates built on the core constants (R5). Per-instance state is pure logging state (`TRANSPORT-29`'s "effectively immutable" is stated in the remarks).

**Verify.** V-build; V-fast `HeaderDropLogTests`. **IDs.** `TRANSPORT-13`, `OBS-19`, `OBS-20`. **Commit.** `feat: add the header-drop log policy`.

## Task 3.2 — The header partition and `Content-Type` authority (`TRANSPORT-10`, `-11`, `-12`, `-26`; P8b-11) — **Breaking 6**

**Files.** Also edited here (moved from 3.3): `SystemNetHttpClient.cs` holds one `HeaderDropLog` (`_drops`) built from the options, and every outbound drop (framing, unsafe, content-header-on-body-less) goes through it, so Breaking 7's outbound half (ids 1-2 become 110-111, repeats at `Debug`) lands in this task. Edit (kit driver): `SystemNetConformanceTests.cs`. New (src): `src/Dexpace.Sdk.Http.SystemNet/OutboundHeaderMapper.cs` (internal sealed; the old `ToHttpRequestMessage`, `IsWireSafe`, `StripTraceContext` and `s_framingHeaders` move here,
so `SystemNetHttpClient.cs` shrinks and `MA0051` holds). Edit: `SystemNetHttpClient.cs`, `RequestBodyContent.cs` (the constructor no longer stamps `Content-Type`). New (tests): `HeaderPartitionTests.cs` (`Unit`),
`ContentTypeWireTests.cs` (`Integration`).

**Tests first.** `HeaderPartitionTests`, over a capturing handler that stores the `HttpRequestMessage` (both faces), one row per cell of the design's table:

- Framing headers dropped (event 111), every case, with and without a body.
- A header failing `IsWireSafe` (an inbound-lenient obs-text value re-used on a request, the construction `HeaderInjectionWireTests` uses) is dropped alone under the policy (event 110), the rest are sent.
- A content header (`Allow`, `Content-Language`, `Content-Encoding`, `Content-Location`, `Expires`, `Last-Modified`; **not** `Content-Length`) with a body lands on `message.Content.Headers`.
- Caller `Content-Type: application/vnd.x+json` with a body typed `application/json`: the wire/content value is the caller's and appears **once**; no caller header → the body's; body-less `POST` + caller
  `Content-Type` → an empty `ByteArrayContent` carrying it; `Content-Length: 0` on the wire (unchanged, F4); name match is case-insensitive (`content-TYPE`).
- A multi-part body with a caller type lacking the boundary: the caller's value wins (documented).
- A content header on a body-less `GET`/`HEAD`/`TRACE`/`CONNECT` is **dropped and logged** (event 110) and `message.Content` is `null` (attaching content would violate `TRANSPORT-26`).
- A header refused by the request collection and not a content header is a logged drop; a value refused by the *content* collection is a logged drop, never silent.
- The trace-context strip (5c) behaves exactly as before (copy `TracePropagationWireTests`' rows through the mapper) and the Phase-1 `Security` classes stay green.

`ContentTypeWireTests` (`Integration`): body X + header Y → Y on the wire; body X, no header → X; body-less `POST` + header Y → Y with `Content-Length: 0`.

**Production change.** `OutboundHeaderMapper.Map(Request, HeaderDropLog)` returns the `HttpRequestMessage`: it decides the `Content-Type` question once, before the loop; builds the content (`RequestBodyContent` with the
task 2.5 capture, or `ByteArrayContent.Empty`-style zero-length content when only content headers exist on a body-permitted method); then runs the loop in the design's row order, one private method per row
(`TryDropFraming`, `TryDropUnsafe`, `Place`) so each stays under 70 lines. Body-forbidden methods: `GET`, `HEAD`, `TRACE`, `CONNECT` (ordinal-ignore-case on `Method.Name`).

**Driver.** Before deleting the `transport-13` waiver, do the Task 3.4 pre-check on the kit's `transport-13` assertion (it must count only Warning-or-above entries per name; amend with its negative control if not, recorded as a kit edit). Then delete the `Owner = "8b"` waivers for `TRANSPORT-10`, `-12` and `-13` in this same commit (this task makes those rows pass; leaving them would make V-conf red under 8a's stale-waiver rule).

**Breaking.** 6, and 7 (outbound drops). **Verify.** V-build; V-fast `HeaderPartitionTests`, `ContentTypeWireTests`, `TracePropagationWireTests`, `TraceContextStrippingTests`; V-conf (green); V-sec. **IDs.** `TRANSPORT-10`, `-11`, `-12`, `-26`, `HTTP-21`. **Commit.** `feat!: partition request headers and make the caller's Content-Type authoritative`.

## Task 3.3 — The inbound path through `HeaderDropLog`; retire the legacy delegates (`TRANSPORT-14`, `OBS-19`; P8b-12, P8b-13) — **Breaking 7** (inbound half)

**Files.** Edit: `SystemNetHttpClient.cs` (delete the three private `LoggerMessage` fields with ids 1 to 3; `AddInbound` calls the `_drops` field added in 3.2 via `InboundDropped`, id 112). Edit (tests): `HeaderDropLogTests.cs`, `SystemNetHttpClientTests.cs` if it
asserted an event id. New (tests): `InboundHeaderTests.cs` (`Unit`).

**Tests first.**

- `InboundHeaderTests` (stub handler returning a response with crafted headers; `HttpResponseMessage.Headers.TryAddWithoutValidation` carries them): obs-text (`0xE9`) preserved; a value with a control byte drops that
  header alone (event 112, `Debug`, name only), the rest delivered, the response not failed (`TRANSPORT-14` value clause); the header **name** clause is *not* testable here (the native parser rejects the response first, G/F1); this is
  asserted in the kit task 3.4, not faked.
- The existing `HeaderInjectionWireTests.An_inbound_header_is_validated_leniently…` and `FramingHeaderDropWireTests` stay green **unedited** (convention 5): they assert levels and messages only.
- Event ids/names are now the core constants for all three; the drop of a name beyond the first under the default policy is `Debug`.

**Production change.** The inbound path routes through `HeaderDropLog` (outbound already did, 3.2); the three legacy delegates go. XML docs for the logger parameter on the four logger-taking constructors are rewritten
to the new levels (**Breaking 7** note: ids 1-3 became 110-112, repeated drops are `Debug`).

**Verify.** V-build; V-fast `InboundHeaderTests`, `HeaderDropLogTests`, `HeaderPartitionTests`; V-sec (all five classes plus `ProxyCredentialWireTests`). **IDs.** `TRANSPORT-11`, `-12`, `-13`, `-14`, `OBS-19`. **Commit.** `feat!: log header drops under the HeaderDropLogging policy with stable event ids`.

## Task 3.4 — The kit: split `transport-14`, the permanent assertion-scoped waiver, delete the closed waivers (`TRANSPORT-10`, `-12`, `-13`, `-14`; P8b-14; R2)

**Files.** Edit: `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetConformanceTests.cs` and `Conformance/SystemNetSubject.cs`. Conditional (kit, only if R2's split is absent from the merged 8a):
`src/Dexpace.Sdk.Conformance/Assertions/HeaderAssertions.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Controls/HeaderControlTests.cs`, `…/Assertions/AssertionCatalogueTests.cs` (count and names).

**Tests first.**

1. Run V-conf: it is green with only the `transport-14` waiver left (the `-10`/`-12`/`-13` waivers went in 3.2); `transport-14.*` still fails on the name clause.
   **Pre-check on `transport-13` (kit reading, done in 3.2 before its waiver is deleted):** 8a's `RecordingLogger` records every level and `Dropped(name)` selects entries by name in the message, while the default `FirstPerName` policy logs repeats at `Debug` with the same name-bearing message, so "at most once" would still fail. Confirm `transport-13.drop-log-once-per-name` counts only `Warning`-or-above entries per name; if it does not, amend it to "at most one Warning-level entry per name" with the negative control `logs every drop at Warning` (a subject at `Every` is `Failed`), the same way R2 handles the `transport-14` split, and record it as a kit edit in the PR description (conditional kit files: `HeaderAssertions.cs` / `HeaderControlTests.cs` / `AssertionCatalogueTests.cs`).
2. **If the merged kit still has one `transport-14.inbound-lenient`:** split it into `transport-14.value-control-dropped` (MUST), `transport-14.obs-text-preserved` (SHOULD) and
   `transport-14.name-malformed-dropped` (MUST), each with the negative control the existing assertion had (a subject that fails the whole response on that clause is `Failed`), update the catalogue test's literal list and count, and
   keep all three citing `TRANSPORT-14`. This is the only kit edit 8b makes and it is flagged in the PR description. If the split is already there, skip.
3. Write the failing expectation first: `Only_the_transport_14_name_waiver_remains` asserts the SystemNet waivers (both subjects) are exactly one
   `new ConformanceWaiver("TRANSPORT-14", "SocketsHttpHandler rejects the whole response on a malformed header name before the adapter sees it (design §10 entry 32)") { Assertion = "transport-14.name-malformed-dropped" }` (8a's record is positional `ConformanceWaiver(string RequirementId, string Reason)`; adapt `Assertion`/`Owner` to the merged kit's optional members); red until the old `TRANSPORT-14` waiver is replaced.

**Production change (test code).** Delete the `Owner = "8b"` waiver for the value/obs-text part of `-14` (`-10`, `-12`, `-13` were deleted in 3.2); add the permanent narrowed waiver; replace the old `Only_8b_waivers_remain` fact by the one above
(there is no `Owner = "8b"` waiver left: exit criterion 2); run on both subjects.

**Verify.** V-build; V-conf: every skip is the one permanent waiver or a `Vacuous` row; `transport-30` is `Passed` on both subjects (since 1.8), and the report is green. If the kit was edited: `dotnet test --project tests/Dexpace.Sdk.Conformance.Tests --configuration Release --no-build`. **IDs.** `TRANSPORT-10`, `-12`, `-13`, `-14`.
**Commit.** `test: close the header waivers; record the TRANSPORT-14 name clause as a permanent waiver`.

## Task 3.5 — PR 3 gate

V-gate; `rg -n "Owner = \"8b\"" tests/Dexpace.Sdk.Http.SystemNet.Tests` returns nothing; the Security diff check (convention 5) lists `ProxyCredentialWireTests.cs` only. Stop; no push.

---

# PR 4 — Close-out

## Task 4.1 — User documentation: `docs/sdk-documentation/transport.md` (`TRANSPORT-1`..`-30` as built)

Write `docs/sdk-documentation/transport.md` in the house style of `docs/sdk-documentation/pagination.md` and `retry.md` (read both first). Sections, each citing its rows and rulings:
(1) construction and ownership (the design's constructor table, the handler constructor's borrow-and-reject rule, why `new SocketsHttpHandler()` throws); (2) the proxy (strict reading, the one-line migration, the adapter's
guarantees, the construction warnings, adapting `IChallengeHandler` to `ICredentials` is the caller's); (3) deadlines and the failure-classification table (state first, shape second; why an internal cancel is
terminal; the borrowed-client outer bound); (4) the synchronous path and the `NotSupportedException` / skipped-`DelegatingHandler` hazard (G1, G2); (5) header mapping and the drop policy (the table, the three modes, events
110-113 and their names); (6) single-use bodies and the re-send limit (G5 to G7, `ResendBufferLimit`, how to raise it or buffer); (7) `TRANSPORT-14`'s name clause and the declined zero-copy; (8) the nine breaking changes with
migrations. Link it from `docs/README.md` (the ownership table) and from `CLAUDE.md` in task 4.6. **Verify.** `dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations`. **Commit.** `docs: transport.md`.

## Task 4.2 — The package README

Edit `src/Dexpace.Sdk.Http.SystemNet/README.md` (packed into the NuGet package by `Directory.Build.targets`): a construction table, the proxy migration line (`Proxy = ProxyOptions.FromEnvironment()`), the sync-path hazard in two sentences,
the timeout scope, and a link to the repository's `transport.md`. Keep every code sample compiling in spirit with the shipped signatures. **Verify.** `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` succeeds and the dependency audit is unchanged. **Commit.** `docs: SystemNet README`.

## Task 4.3 — NativeAOT smoke: `CheckPhase8bAsync` (exit criterion 4)

**Files.** Edit: `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (call from `RunAllAsync` after `CheckPhase7cPaginationAsync`; the smoke has no reflection, so use only the shipped API).

**Tests first (the check is the test).** `CheckPhase8bAsync` runs: a sync `Execute` and an async call against a loopback `TcpListener`-free path (the smoke already uses `CannedHandler`; use it with the handler constructor over a
`SocketsHttpHandler { AllowAutoRedirect = false }` wrapper only if the listener is available, else a `CannedHandler` with `Send` and `SendAsync`) through `new SystemNetHttpClient(handler, options)`; a deadline that fires
(`Timeout = 50 ms` over a handler that waits on its token) classified as `ServiceRequestTimeoutException`; a constructed `ProxyOptionsWebProxy` is exercised through the internal-free path by constructing the owned transport with
a `Proxy` option (proves the adapter and `BasicOnlyCredentials` survive trimming); a header drop logged under `Every`. Each `Expect(...)` has a message.

**Verify.** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` exits 0 and prints no trim/AOT warning (they are errors). **IDs.** `NFR` AOT gate, 8b's share. **Commit.** `test: AOT smoke for the phase 8b transport`.

## Task 4.4 — The checklist

Write `docs/work/mvp/phase8/phase8b/2026-10-09-phase8b-reference-transport-checklist.md` (path per task 4.7's filing; draft it where `docs/superpowers/` rules say, then file) from what was built, following
`docs/work/mvp/phase7/phase7c/2026-10-09-phase7c-pagination-checklist.md`'s shape: header (branches, issue #89, the four PRs), the legend, **red evidence** (the first compile of each new test class; the pins proven able to fail by mutation:
`NoImplicitEnvironmentReadArchitectureTests`, `ProxyCredentialWireTests`, `NoAbandonedResponseArchitectureTests`), the **14-row requirement table** (one row per ID with kit assertion, SystemNet test, `[Trait]` and file; statuses from the
[checklist section](#checklist-one-row-per-owned-id) corrected to what was observed), a **second table of the twelve non-row hand-offs** (design "Non-row hand-offs" with their dispositions and the test that pins each), the
`Security`-class statement (one added, five unedited), the deviations from the plan, and the totals. State: `TRANSPORT-14` ✅ value/obs-text and 🚫 name clause (§10 entry 32); `TRANSPORT-28` ✅ MUST and 🚫 zero-copy; the kit run is green on
both SystemNet subjects and the raw-socket driver with exactly one permanent waiver. **Commit.** `docs: phase 8b checklist`.

## Task 4.5 — `CHANGELOG.md`

Under `[Unreleased]`, one entry in the file's existing structure (read how 6c's and 7c's entries are laid out and match it) headed `TRANSPORT-1, -4, -5, -6, -8, -9, -10, -12, -13, -14, -17, -18, -28, -30`, listing the **nine breaking changes** of the design with their
migration lines (1 implicit proxy; 2 genuine sync with `NotSupportedException`/skipped handler; 3 timeout enforced; 4 internal cancel terminal; 5 `ObjectDisposedException` for borrowed; 6 `Content-Type` authority and body-less
content headers; 7 event ids 1-3 → 110-112 and `FirstPerName`; 8 handler constructor rejects a following primary; 9 native re-send replay) and the additions (`SystemNetHttpClientOptions`, `HeaderDropLogging`, four
constructors, events 110-113, `ProxyCredentialWireTests`). Re-derive the hunk from the merged file if 8a or 9 landed first. **Commit.** `docs: changelog for phase 8b`.

## Task 4.6 — Design, register, roadmap, `CLAUDE.md` and knowledge corrections ("Corrections owed at close-out")

Each a dated correction (2026-10-09 or the merge date) naming its ruling; never rewrite an earlier verdict, append:

1. **Design (`docs/sdk-design-dotnet/`)**: §3.2 "As built (8b)" (real sync path, header partition, handler constructor, `SEAM-15`; `TRANSPORT-2` paragraph gains P8b-10); §3.3 "Deadlines" (bounds until response headers, G3; runs on the
   options' `TimeProvider`); §3.7 (`SEAM-15` for every construction); §8.2 ("calls that resolver explicitly at construction" corrected to the strict reading, P8b-4); §9.1 (banned list gains `HttpClient.DefaultProxy`); §10 **new entry 32**
   (`TRANSPORT-14`'s malformed-name clause is unmet on `SocketsHttpHandler`, F1, touching `TRANSPORT-14` only; re-read §10's current last number on the merged file first); §11 item 18 (re-send measured, G5 to G7, answered by capture); §12's `TRANSPORT` row.
2. **`docs/first-release.md`**: *Unsatisfied MUSTs* gets `TRANSPORT-14` (name half, §10 entry 32), replacing "None is recorded yet" only if no sibling added one; *SHOULD declined*: `TRANSPORT-28` zero-copy; *Behavioural asymmetries*:
   `sync-borrowed-handlers` (G1, G2) and `explicit-proxy` (P8b-4); the 6c line about the `407` gains "or the Basic pair".
3. **Prior checklists** (dated corrections): phase 2b `SEAM-11` and `SEAM-15`; phase 4c `PIPE-28`; phase 5b `OBS-19`; phase 5a's F8 (the transport IL scan); phase 1's S1, S2, S3 and S9 rows' 8b clauses.
4. **Roadmap** (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`): an append-only dated Phase Status Note ("8b reference-transport hardening built: 14 rows, 12 ✅ / 2 split / 0 ⏳; the explicit-proxy reading; `LateResult` not needed, `Disposal` kept
   internal, the `CallKey` stamp moved to phase 9; the permanent `TRANSPORT-14` waiver; rulings open for the lead taken by default: P8b-2, 4, 7, 9, 10, 14, 18"), and the Phase List row 8 gains the 8b design, plan and checklist links; D3 untouched.
5. **`CLAUDE.md`**: "Transports are ownership-aware" gains the handler constructor's borrow-and-reject rule and the explicit proxy; the layout block gains no new top-level project but the SystemNet line lists `SystemNetHttpClientOptions`;
   "What is genuinely unbuilt" loses phase 8's transport half (keep 8a's words to 8a) and gains a pointer to `docs/sdk-documentation/transport.md`; test counts if the file states any.
6. **Knowledge**: append G1 to G8 to `docs/knowledge/notes/transport-adapter.md` (8a creates it; create with the notes front matter other notes use if still absent), and run `scripts/knowledge verify-structure`.

**Commit.** `docs: phase 8b corrections, roadmap note and CLAUDE.md`.

## Task 4.7 — File the phase documents, run the probe, final gate

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 8b            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 8b --write    # git mv the design, plan and checklist
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
scripts/knowledge verify-structure
```

Fix what the last probe reports (misattributed IDs, broken links, stale counts in `CLAUDE.md`/`README.md`). Then the whole CI list from `CLAUDE.md`, locally: V-build, V-fmt, full tests (V-sec first), the coverage gate and its self-test,
`dependency-audit.cs`, `reproducible-pack.sh`, the AOT publish and run, the tools solution build and test. Exit check list: no `Owner = "8b"` waiver remains; `transport-30.*` is `Passed`; `rg "GetAwaiter\(\)\.GetResult" src/Dexpace.Sdk.Http.SystemNet` is empty;
`git diff --stat main...HEAD -- Directory.Packages.props '*packages.lock.json'` empty. **No push and no PR:** those are the lead's actions (global rule); the PR descriptions name the checklist and `Closes #89` (on PR 4 only).

---

## Shared files with 8a

| File | Rule |
|---|---|
| `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetConformanceTests.cs`, `Conformance/SystemNetSubject.cs` | 8a creates; 8b deletes waivers (tasks 1.8, 2.3, 2.5, 3.4), adds the handler subject and `CreateWithProxy`, adds the one permanent waiver. Re-derive from the merged file. |
| `src/Dexpace.Sdk.Conformance/**`, `tests/Dexpace.Sdk.Conformance.Tests/**` | 8b edits only if task 3.4 finds the `transport-14` split absent; an assertion bug found by an 8b task is fixed with its negative control in that task's PR. |
| `CHANGELOG.md`, roadmap, `CLAUDE.md`, `docs/first-release.md`, `docs/knowledge/notes/transport-adapter.md` | each phase appends its own words; never resolve a conflict by keeping either side whole. |

## Checklist: one row per owned ID

Exactly 14 rows. **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 4.4 records what was built. Level counts: 10 MUST, 4 SHOULD (`TRANSPORT-6`, `-13`, `-28`, `-30` as the primary level; `-14` and `-28`
and `-30` carry a second-level clause, per appendix C).

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `TRANSPORT-1` | MUST | 1 | 1.4, 1.6, 1.8 | ✅ | `HandlerConstructorTests` (rejections/acceptances, no mutation); `transport-1.redirect-not-followed` on the owned and handler subjects; phase-1 `RedirectWireTests` unedited |
| `TRANSPORT-4` | MUST | 2 | 2.1, 2.3 | ✅ | `FailureClassificationTests` rows 2/3 (retryable through the pipeline); `TimeoutWireTests`; `transport-4.timeout-is-retryable` on both faces |
| `TRANSPORT-5` | MUST | 2 | 2.2, 2.3 | ✅ | `DeadlineTests` (per-call over default, `null` keeps default, concurrent calls); `TimeoutWireTests`; `transport-5.per-call-timeout` |
| `TRANSPORT-6` | SHOULD | 2 | 2.2, 2.3 | ✅ | `DeadlineTests` clamp table; `TimeoutWireTests` 100 µs row; `transport-6.sub-resolution-timeout` |
| `TRANSPORT-8` | MUST | 2 | 2.1, 2.3 | ✅ | `FailureClassificationTests` row 4 (terminal under the default pipeline, native on the trail); `transport-8.internal-cancel-is-terminal` |
| `TRANSPORT-9` | MUST | 2 | 2.3, 2.6 | ✅ (met structurally, pin) | `NoAbandonedResponseArchitectureTests`; `transport-9.settle-race-releases` |
| `TRANSPORT-10` | MUST | 3 | 3.2, 3.4 | ✅ | `HeaderPartitionTests`, `ContentTypeWireTests`; `transport-10.content-type-authoritative` |
| `TRANSPORT-12` | MUST | 3 | 3.2, 3.3, 3.4 | ✅ | `HeaderPartitionTests` drop rows (both faces), phase-1 `HeaderInjectionWireTests` unedited; `transport-12.native-rejected-header-dropped` |
| `TRANSPORT-13` | SHOULD | 3 | 3.1, 3.3, 3.4 | ✅ | `HeaderDropLogTests` (modes, bound, concurrency); `transport-13.drop-log-once-per-name` |
| `TRANSPORT-14` | MUST (+SHOULD) | 3 | 3.3, 3.4, 4.6 | ✅ value and obs-text; 🚫 name clause (§10 entry 32) | `InboundHeaderTests`, `HeaderInjectionWireTests`; `transport-14.value-control-dropped`, `.obs-text-preserved`; permanent waiver on `.name-malformed-dropped` |
| `TRANSPORT-17` | MUST | 2 | 2.4, 2.5 | ✅ | `SyncExecuteTests` (written once, calling thread); `SyncExecuteWireTests`; `transport-17.single-use-written-once` over the real sync path |
| `TRANSPORT-18` | MUST | 2 | 2.5 | ✅ | `ResendCaptureTests`, `ResendCaptureWireTests` (G5/G6 forward-proxy re-send, over-limit message); `transport-18.native-resend-identical` |
| `TRANSPORT-28` | SHOULD (+MUST) | 2 | 2.6, 4.6 | ✅ MUST half; 🚫 zero-copy SHOULD declined (P8b-15) | `FileRequestBodyReplayTests`; `transport-28.file-range-replayable`; `docs/first-release.md` SHOULD-declined line |
| `TRANSPORT-30` | SHOULD (+MUST) | 1 | 1.2, 1.3, 1.5, 1.6, 1.7, 1.8 | ✅ | `ProxyAdapterTests`; `ProxyCredentialWireTests` (**Security**); IL scan; `transport-30.proxy-discoverable-no-leak` moves from `NotExercised` to `Passed` |

Count: **14 rows**, all mapped (10 MUST, 4 SHOULD). By exit: 12 ✅ (`TRANSPORT-1`, `-4`, `-5`, `-6`, `-8`, `-9`, `-10`, `-12`, `-13`, `-17`, `-18`, `-30`), 2 split (`-14`, `-28`: ✅ for the MUST/value clauses, 🚫 for one clause each), 0 ⏳, 0 N/A.

### Work on rows owned elsewhere (no 8b checklist row)

| ID (owner) | Work | Task | Evidence |
|---|---|---|---|
| `SEAM-11` (2b), `PIPE-28` (4c) | the ⏳ 8b clause: the real synchronous terminal | 2.4 | `SyncExecuteTests`, `SyncExecuteWireTests`; dated corrections in 2b's and 4c's checklists |
| `SEAM-15` (2b) | `ObjectDisposedException` after dispose, every construction | 1.6, 1.8 | `AfterDisposeTests`; `seam-15.after-dispose` |
| `OBS-19` (5b) | header-drop policy and ids | 3.1, 3.3 | `HeaderDropLogTests`; 5b checklist correction |
| `TRANSPORT-11` (8a) | framing-drop event moves to id 111 | 3.3 | `FramingHeaderDropWireTests` unedited, `HeaderDropLogTests` |
| `CFG-28`, `CFG-23`, `CFG-25`, `CFG-27` (5a) | strict proxy reading; `DefaultProxy` ban; IL scan | 1.2, 1.5, 1.6 | `NoImplicitEnvironmentReadArchitectureTests`, owned-handler pins |
| `TRANSPORT-22` (8a) | dispose-on-throw through `NativeDisposal` | 1.6, 2.4 | `NativeDisposal` tests; `transport-22` assertion unchanged and green |

---

## Traceability: PR → rows → tasks

| PR | Tasks | Rows (first owner) |
|---|---|---|
| 1 — construction and proxy | 1.1–1.9 | `TRANSPORT-1`, `TRANSPORT-30`; `SEAM-15`, `CFG-28` |
| 2 — send path | 2.1–2.7 | `TRANSPORT-4`, `-5`, `-6`, `-8`, `-9`, `-17`, `-18`, `-28` (MUST half pin); `SEAM-11`, `PIPE-28` |
| 3 — headers | 3.1–3.5 | `TRANSPORT-10`, `-12`, `-13`, `-14`; `OBS-19` |
| 4 — close-out | 4.1–4.7 | documentation, checklist, `CHANGELOG.md`, corrections, `TRANSPORT-14`/`-28` declined clauses recorded |

Task count: 0.1 (pre-flight) + 9 (PR 1) + 7 (PR 2) + 5 (PR 3) + 7 (PR 4) = **29 tasks**.

## Exit criteria (the design's list, mapped to tasks)

1. Checklist of 14 rows with named tests, second table of the twelve hand-offs, dated flips of `SEAM-11`, `SEAM-15`, `PIPE-28`, `OBS-19` — tasks 4.4, 4.6.
2. Kit green on both SystemNet subjects and the raw-socket driver; no `Owner = "8b"` waiver; `transport-30` run; the only permanent SystemNet waiver is the assertion-scoped `TRANSPORT-14` name clause — tasks 1.8, 2.3, 2.5, 3.4.
3. No sync-over-async in `src/Dexpace.Sdk.Http.SystemNet`; the IL scan passes — tasks 1.2, 2.4, 2.6.
4. Every gate green (build, `RS0016`/`RS0017` for both changed `PublicAPI` files, `RS0030`, `MA0051`, `CA2007`, format, full tests with Security classes unedited, 80% coverage, dependency audit unchanged, reproducible pack, AOT smoke with `CheckPhase8bAsync`, `verify-structure`) — tasks 1.9, 2.7, 3.5, 4.3, 4.7.
5. `transport.md`, README, `CHANGELOG.md` (nine breaking changes with migrations), roadmap note, corrections, knowledge note, a probe that reports nothing — tasks 4.1, 4.2, 4.5, 4.6, 4.7.

## Open items carried to the lead (none blocks execution; each task implements the design's chosen option)

- **P8b-2** constructors plus an options record, versus static factories. **P8b-4** the strict proxy reading (overrides design §8.2; fallback is a constructor-time `FromEnvironment`, one constructor change). **P8b-7** an internal cancel as a plain
  `OperationCanceledException`, versus a new public `SdkException` subtype. **P8b-9** the real `Send` for borrowed clients (G1, G2 hazard documented), versus keeping sync-over-async for them. **P8b-10** the 1 MiB `ResendBufferLimit` default.
  **P8b-14** spending one of phase 11's five unmet MUSTs on `TRANSPORT-14`'s name clause. **P8b-18** the `CallKey` stamp handed to phase 9.
- **Plan-level additions flagged for the lead:** R1 (`CannedHandler` gains `Send`), R2 (a possible 8b edit of 8a's `transport-14` assertion), R6 (explicit-`init` record), R7 (`CallDeadline` as a class).
- **Findings while planning.** (a) `SmokeChecks.CheckPipelineReworkAsync` drives the sync face over an async-only handler and would break under the real sync path (R1); (b) core's `LogVocabularyTests` pins 110 to 119 as unused (R4); (c) 8a's plan
  already carries the waiver-narrowing the design asks for (R2); (d) design G7 (`https://` through a credentialed proxy never re-sends) needs TLS termination to test on the fixture, so task 2.5 leaves it to task 0.1's probe if the fixture cannot terminate TLS.
