# Phase 9 — Host Integration: Implementation Plan

**Status:** Draft, for review. Written 2026-10-12 against `main` at `9a66c15` (phases 0 to 8b merged), on branch
`94-phase-9-planning`, for issue [#94](https://github.com/dexpace/dotnet-sdk/issues/94). Design:
[phase 9 host-integration design](2026-10-12-phase9-host-integration-design.md), the authority for every decision below. The
plan cites its rows (`CFG-n`, `SEAM-n`, `OBS-35`), facts (`V1`..`V9`) and rulings (`P9-1`..`P9-29`) rather than restating them; **every
open ruling is taken at its recommended default**, and the places where a task depends on a ruling going a particular way are collected in
[Ruling dependencies](#ruling-dependencies). Scope authority: the roadmap's Phase 9 card and Phase List row 9
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). Format precedent: the phase 8b plan
(`docs/work/mvp/phase8/phase8b/2026-10-09-phase8b-reference-transport.md`) and the phase 5a plan
(`docs/work/mvp/phase5/phase5a/2026-10-07-phase5a-configuration.md`), read first.

**What this document is.** The roadmap's step 3 for phase 9 (which has no sub-phases, P9-1): numbered TDD tasks in the design's four
stacked pull requests (P9-25), each with its failing tests (name and `[Trait]`), production change, `PublicAPI.Unshipped.txt` diff, files
touched, requirement IDs and a verify step, ending in a close-out group. It is not the checklist (step 4, written from what was built in
task 4.6) and it carries no production code beyond the load-bearing sketches a task quotes. The
[traceability table](#traceability-requirement-id-to-tasks) maps every ID to tasks; the [checklist section](#checklist-one-row-per-owned-id)
is the plan's own row table: exactly one row per ID phase 9 owns.

**Scope.** 15 owned rows: `CFG-1`, `-2`, `-3`, `-4`, `-5`, `-6`, `-7`, `-10`, `-11`, `-14`, `-37`, `-38`, plus `SEAM-5`, `SEAM-6`, `OBS-35`
(13 MUST, 2 SHOULD). Planned exits: 13 ✅ (5 of them split with a 🚫 clause on an existing §10 entry: `CFG-2`, `-5`, `-6`, `-7`, `OBS-35`),
2 N/A (`CFG-4`, `CFG-10`), 0 🚫, 0 ⏳ (the design's tally was corrected to 13 ✅ by its validation resolution: see the findings at the end). Evidence is added to three rows other phases own (`SEAM-7`, `TRANSPORT-1`, `XCUT-10`), and the
design's non-row hand-offs are dispositioned in task 4.6's second table.

**Order and gates.** Four stacked pull requests, each green on its own, on branches `94-phase-9-<slug>` off `main` (the first), each later
one off its predecessor. **PR 1** (tasks 1.1 to 1.3): seams and gates. **PR 2** (2.0 to 2.10): the package and the reader. **PR 3** (3.1 to 3.9):
registration. **PR 4** (4.1 to 4.9): drivers and close-out. Task 0.1 is a pre-flight with no commit.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile error counts
   and is the expected red for a new type or a changed signature); make the production change; run them green; then run the group's
   wider gate. A test that passes before the change is a **pin** and says so; a pin is proven able to fail by temporarily breaking the thing
   it pins (never committed; restore with `git checkout -- <file>`). **No commit is red:** a task that changes a signature edits every
   caller in the same commit. The coverage gate is the one exception by nature: it is a per-PR check that needs an instrumented line in every `src/` library, so PR 2's scaffold and ban commits (tasks 2.0, 2.0a) are exempt from it; every PR passes it (V-gate).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on its project's
   curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace is imported at the top of the file that uses it, including
   `System.Linq` and `Microsoft.Extensions.*`). One type per file, named for the type.
3. **`src/` code** (the package): `ConfigureAwait(false)` on every `await` including `await using` (`CA2007`); methods at most 70 lines
   (`MA0051`: the reader is split per record, never one long method); `///` docs on every public member (CS1591), each citing its IDs; no
   reflection (`IsAotCompatible`, `IsTrimmable`); no `ConfigurationBinder` (task 2.0a); no `Environment.GetEnvironmentVariable(s)`, no
   `Task.Delay`, wall clock or `Thread.Sleep` (`BannedSymbols.txt`); a configured string, URL or credential never reaches a message, log line or exception
   text (`XCUT-18`, `XCUT-19`; an accessor's numeric range message may echo the rejected number, P9-28): a failure reads `"<path>: <rule>"`; no new `PackageReference` in core or any other library (P9-3: the
   one new reference is `Microsoft.Extensions.Http` in the new package).
4. **Tests.** `[Trait("Category", …)]` on every class as the design's test table says (`TestCategoryTests` enforces it per suite).
   Tokens come from `TestContext.Current.CancellationToken`; assertions are xUnit `Assert` only; no test sleeps on the wall clock (condition
   waits and `FakeTimeProvider`). Test code may use reflection (`KeyTableTests`, `NullGuardTests`, the IL scanner) and may construct
   `HttpClient`/`SocketsHttpHandler`; the `RS0030` bans are for `src/` only. The DI test project references the package, core, SystemNet,
   `Dexpace.Sdk.Serialization.SystemTextJson`, `Dexpace.Sdk.Conformance` and `TestSupport`; **`Dexpace.Sdk.Core.Tests` is not touched**
   (SEAM-2's partition, an architecture test, stays as it is). **Client helper.** Every test that registers a client and resolves or validates it builds the client through one shared helper, `TestClients.Add(services, name, …)`, with `StubSerde` (introduced in task 3.2); from task 3.3 the helper also calls `UseSerde(StubSerde.Instance)`, because a client with no codec fails validation and every keyed resolution (design D). A test about the missing codec bypasses the helper. Providers are disposed with `DisposeAsync` (a synchronous dispose throws for an async-only transport, design G).
5. **Security tests are never deleted or loosened.** Phase 9 edits **no** existing `Security` class and adds exactly one
   (`NamedClientRedirectWireTests`, task 3.8, P9-24). The V-gate's diff check is `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Core.Tests/Security
   tests/Dexpace.Sdk.Http.SystemNet.Tests/Security` listing that one added file under the DI test project's `Security/` folder and nothing under
   either listed path (the new class lives in `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Security/`, so the diff is empty).
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` stays empty, so every surface change is an `Unshipped` line; the file is sorted
   ordinally and the build's `RS0016`/`RS0017` output is the authority (apply the analyzer's code fix, then compare against the design's
   "The public surface" list). Phase 9 changes two files: the new package's, and `src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt`
   (one added line). **Core's and the STJ package's files must not move** (the V-gate checks). No overload has an optional parameter
   (`RS0026`/`RS0027`).
7. **No breaking change.** The package is new and `CreatePrimaryHandler` is additive (design, "Breaking changes"); no `<b>Breaking:</b>` mark
   is needed. Task 4.7 writes one `### Added` block and one `### Changed` line (the guidance correction).
8. **Commits** follow the repository style (`feat:` for a new feature, `test:`, `docs:`, `chore:`, `ci:`). **No AI attribution** in a commit
   message, changelog line or document (global hard rule). **The plan authorises no `git push`, no `gh` command and no remote action**; the
   implementer stops before any push and hands the lead the branch names. PR descriptions name the checklist and carry `Closes #94` on
   PR 4 only.
9. **Shared files.** `CHANGELOG.md`, `CLAUDE.md`, the roadmap status notes, `docs/first-release.md`, `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`
   and `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs` are shared with other phases: re-derive each hunk from the merged
   file and never resolve a conflict by keeping either side whole.
10. **A mutation is not a red.** Where a task says "prove the pin can fail", break the production line, see the named test fail, and restore.

### Environment and command shorthands

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green build is the lint
gate. The test runner is Microsoft.Testing.Platform: build first, then `dotnet test … --no-build`, and **never pass MSBuild flags to
`dotnet test`** (the platform forwards them to the test host and zero tests run; `--filter-class`, `--filter-trait`, `--filter-method` are its
options). A run that reports zero tests is a failure of the command, not a pass. On this machine no SDK is on `PATH`: export `DOTNET_ROOT` and
`PATH` to the SDK in use before any `dotnet` or `scripts/knowledge` command.

- **V-build** = `dotnet build Dexpace.Sdk.sln --configuration Release -m:3 -nr:false --disable-build-servers`
- **V-di `<Class>`** = `dotnet test --project tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests --configuration Release --no-build --filter-class "*<Class>"` (after V-build)
- **V-sn `<Class>`** = the same against `tests/Dexpace.Sdk.Http.SystemNet.Tests`
- **V-sec** = `dotnet test --project <each of the two Security-bearing suites and the DI suite> --configuration Release --no-build --filter-trait "Category=Security"`
- **V-conf** = `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --no-build --filter-trait "Category=Conformance"` and the same for the DI suite and `tests/Dexpace.Sdk.Conformance.Tests`
- **V-fmt** = `dotnet format Dexpace.Sdk.sln --verify-no-changes`
- **V-restore** = `dotnet restore Dexpace.Sdk.sln --locked-mode` and `dotnet restore tools/Dexpace.Tools.sln --locked-mode`
- **V-gate** = V-restore, V-build, V-fmt, `dotnet test --solution Dexpace.Sdk.sln --configuration Release --no-build` (V-sec first), the Security diff check of convention 5, `git diff --stat main...HEAD -- src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt` empty, the coverage gate (`dotnet test --solution Dexpace.Sdk.sln --configuration Release --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`, then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and `scripts/ci/coverage-gate-selftest.sh artifacts/test-results`; it fails closed on a `src/` library with no instrumented line), and the closing commands of the group's last task

---

## Plan-level readings (where the design was silent, or the tree differs)

The design's rulings are not changed. **R1 to R4 are corrections of fact against the tree; R5 to R12 are additions or interpretations.**
Items marked **flag** are for the lead.

- **R1 — The conformance hook handlers are nested private classes, not files (flag; the design says "shared by linked source").**
  `FaultingAdaptationHandler`, `ResendingHandler` and `CancelOnDemandHandler` are `private sealed class` members of
  `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs` (and `ExplodingContent` beside them); a `<Compile Include … Link>` of that file
  would drag the whole subject. Task 4.1 first extracts each into its own internal top-level file under
  `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/Handlers/` (namespace unchanged, a `chore:` move, plus, for each of the three `DelegatingHandler` subclasses, a rewrite onto two explicit constructors (they use primary constructors today, `(HttpMessageHandler inner) : DelegatingHandler(inner)`; a parameterless one must chain through `this(...)`, CS8862, and `DelegatingHandler(null)` throws), with the SystemNet driver green), then
  links those four files into the DI test project. `Dexpace.Sdk.TestSupport` and `Dexpace.Sdk.Core.Tests` do not change (the design's reason).
  `ResendingHandler` is the design's "ResendingHandler"; the design's list of three omits `ExplodingContent`, which the faulting handler needs.
- **R2 — The drift guard covers seven records, not six (wording only; the design now agrees).** The design said "the six records"; the init-property census
  of the key table is `DexpaceClientOptions`, `RetryOptions`, `RedirectOptions`, `HttpLoggingOptions`, `SystemNetHttpClientOptions`,
  `ProxyOptions` and `AuthCredentials`, plus the three credential classes (`ApiKeyCredential`, `BasicCredential`, `DigestCredential`), whose members are
  keyed under `Auth:*`. The credentials are **not** records with `init` members (they are classes with get-only properties set by their constructors), so for them `KeyTableTests` enumerates the public constructor parameters (a property census would be vacuous). `Transport:PooledConnectionLifetime` maps to the primary handler, not to a record property, so the table carries an explicit **handler-level** category for it and "every key maps to a real member" excludes that category.
- **R3 — PR 2 needs an internal registration seam before the public API exists.** The reader, the state, the setup and the validator are
  tested through `DexpaceClientRegistrations.AddClientState(IServiceCollection, string name, IConfigurationSection?, …)` (internal; task 2.6), which the
  public `AddDexpaceClient` of PR 3 calls. No public member lands before PR 3 apart from `DexpaceConfigurationKeys` (task 2.9), so PR 2 has a one-type public surface (the package has no other public type before PR 3).
- **R4 — `DexpaceClientState` is internal but has a public parameterless constructor.** `OptionsFactory<T>` creates `TOptions` with
  `Activator.CreateInstance<T>()`, which needs the public constructor to survive trimming; the class itself stays `internal` (the design's
  "mutable, internal"). V5 and V7 found no analyzer or NativeAOT problem with this shape; task 4.4's smoke is the standing proof.
- **R5 — The `Microsoft.Extensions.Http.Resilience` `PackageVersion` lands in task 4.3, not PR 1 (flag).** The design lists it with the
  `Directory.Packages.props` change; an unreferenced `PackageVersion` moves no lock file, but the plan adds it where it is first referenced so each PR's
  lock-file diff explains itself. **Risk the design did not measure:** the package's own closure on the `10.10.0` line, combined with
  `CentralPackageTransitivePinningEnabled` and the `10.0.12` pins, may raise `NU1109` (a pinned transitive below what a dependency asks for).
  Task 0.1 measures this inside the repository (not a scratch project); if it fails, task 4.3 pins the offending transitives at the version the
  resilience package asks for in the `Tests` group of `Directory.Packages.props` (a test-only pin) and, failing that, falls back to P9-19's
  documented alternative (an untested documentation snippet), which the design names. The `Runtime` band rule of `dependency-audit.cs` does not reach test projects.
- **R6 — `CreatePrimaryHandler` takes over the construction warnings from the constructor.** Today the private constructor emits
  `ProxyWarnings.Emit(options.Logger, proxy)` for the owned construction (`SystemNetHttpClient.cs`, around line 313). With the handler built in a
  public static method, the emission moves into `CreatePrimaryHandler` (so a DI-built primary warns exactly once, through `options.Logger`) and out of the
  constructor (so the owned construction, which now calls `CreatePrimaryHandler`, still warns exactly once). Task 1.1 pins "exactly once" on both paths.
- **R7 — The primary handler lease is bound through the factory's delegate.** The design says the lease "records the instance step 1 built". The
  mechanism taken here: `ConfigurePrimaryHttpMessageHandler(sp => …)` resolves the client's `PrimaryHandlerLease` from the keyed singletons, calls
  `lease.Track(handler)` on the handler it just built from `CreatePrimaryHandler`, and returns it; the keyed singleton factory for the lease is the first thing the
  transport factory resolves, so the container creates it before the transport and disposes it after it. `Track` ignores a second call's
  instance only by disposing the earlier, never-used one (a caller who replaced the primary leaves the package's discarded instance; the lease owns exactly the
  instances it was handed).
- **R8 — A bare-number duration is digits only (flag, edge).** The design's grammar says "digits alone, as milliseconds". `1.5` bare is therefore a
  failure (the ISO and shorthand forms carry fractions), as is a bare value above `TimeSpan.MaxValue` in milliseconds. `DurationGrammarTests` pins it. Both siblings accept a bare fraction (Ruby `\d+(?:\.\d+)?`, Node `/^\d+(?:\.\d+)?$/`); this is a departure, recorded with the other grammar readings as a §11 resolution (P9-29, open).
- **R9 — Unknown keys are failures at every depth under the section, including under `Transport:Proxy:` and `Auth:`.** The design says "unknown keys under the
  section fail too"; the reader applies it recursively against the key table, and a key table entry present without its sibling requirements
  (a proxy key without `Host`/`Port`) is a different, named failure. This is **P9-9's open half** (unknown keys), taken as designed; if the lead
  reverses it, task 2.3's `UnknownKeys` pass and its tests are deleted and nothing else moves.
- **R10 — Per-client section keys are read case-insensitively through `IConfiguration`'s own comparer.** `IConfigurationSection.GetChildren()`
  yields keys in provider order with their original casing; the reader compares with `StringComparer.OrdinalIgnoreCase` against the key table
  and reports the **configuration's** spelling in an unknown-key failure (a key is not a secret; a value is never quoted).
- **R11 — The audit has no test project.** `scripts/ci/dependency-audit.cs` is exercised by running it against the packed artefacts, so the evidence is
  the audit run itself: task 1.2's pin (the four existing libraries still conform under the policy record) and, once the package exists, task 2.0's red
  and green for it (`no dependency policy`, then, with a `null` policy, `references project Dexpace.Sdk.Http.SystemNet`); the negative controls are
  mutations of the policy (never committed).
- **R12 — `ValidateOnStart` is wired per name through `OptionsBuilder<T>`, and `IStartupValidator` is the host-less entry.** The tests call
  `provider.GetRequiredService<IStartupValidator>().Validate()` (V4) for the host-less case (there is no host: `Microsoft.Extensions.Hosting`
  is **not** referenced, so no test builds or starts one): "a host fails at start" is proven by the validator being the very service the hosting
  startup service calls, which task 2.6 states in the test's comment and `docs/sdk-documentation/dependency-injection.md` documents.

### Ruling dependencies

The plan is written to the design's chosen option for every ruling. Where reversing one changes tasks:

| Ruling (open for the lead) | Tasks that depend on it | If reversed |
|---|---|---|
| P9-1 segmentation (one phase, recorded in the design) | all | A file of its own is a `docs:` move of the design's "Segmentation" section; no task changes. |
| P9-2 package references `Dexpace.Sdk.Http.SystemNet` | 1.2, 2.0, 3.2, 4.2, 4.4 | "core only" removes the named client: tasks 1.1, 3.2, 3.8, 4.2 and the audit's `Projects` entry are dropped; `TRANSPORT-1`'s clause stays with 8b. |
| P9-4 hand-written reader, `ConfigurationBinder` banned | all of PR 2 (incl. 2.0a) | The binder route re-opens V1: not viable without a staging type; the plan has no alternative path. |
| P9-5 no generic typed-client overload | 3.1 | Additive later; nothing to build now. |
| P9-9 unknown keys fail | 2.3, 2.7 | Delete the `UnknownKeys` pass and its rows (R9). |
| P9-7 empty value is absent (recommended default A) | 2.3, 4.5 | Option B: `Section.TryGetScalar` turns an empty value into the failure `"<path>: empty value"`, `EmptyValueTests` flips to expect it, `CFG-2` becomes 🚫 whole on §10 entry 25 (13 ✅ becomes 12 ✅, 1 🚫), and the user page drops the shadowing paragraph. |
| P9-28 numeric accessor echo allowed (recommended default) | 2.3, 2.6 | Strip the BCL text: a per-key rule table in 2.3 and the numeric-echo test becomes a no-echo test. |
| P9-29 grammar readings recorded (recommended default) | 2.1, 2.2, 4.5, 4.8 | Accept bare fractions (one scanner branch and rows in 2.1) and/or leave booleans untrimmed (2.2); the §11 item shrinks accordingly. |
| P9-10 no cross-property failure | 2.6, 4.5 | A 170 to 179 event block in core and three warnings: a separate, core-touching PR 2 task. |
| P9-12 `CreatePrimaryHandler` | 1.1, 3.2, 3.6, 4.2 | The alternative is rejected by the design (`InternalsVisibleTo` grants one test project only); no fallback in the plan. |
| P9-15 no configuration switch for the environment proxy | 2.5, 3.6 | A `Transport:Proxy:FromEnvironment` key would be an `CFG-28` regression; the scan test (task 3.6) is written to fail on it. |
| P9-17 no codec default | 3.3, 3.7, 4.5 | An STJ default adds a project reference, a second audit `Projects` entry and a `UseSerde`-optional branch in 3.3. |
| P9-19 test-only `Microsoft.Extensions.Http.Resilience` | 4.3 | An untested documentation snippet; `ResilienceCompositionTests` and the package version are dropped (R5). |
| P9-20 `CallKey` stamp not built | 4.8 | A reader would be built before this phase can test it: out of scope. |

---

## Task 0.1 — Pre-flight: queries, baseline and the facts to re-verify (no commit)

1. **Read what is known** (start of the phase and of every numbered task):

   ```bash
   scripts/knowledge --origin note --brief
   scripts/knowledge --section conflicts --brief
   scripts/knowledge --prefix-info CFG        # 38 IDs; 12 are this phase's, 26 are 5a's
   scripts/knowledge --gaps CFG               # 0 gaps expected
   scripts/knowledge --prefix-info SEAM       # SEAM-5, SEAM-6 (carried from 2b)
   scripts/knowledge --req CFG-2              # and CFG-5, -6, -7, -14, OBS-35, SEAM-6, XCUT-10 as each task starts
   ```

2. **Confirm the predecessors.** `git log --oneline main -6` shows 8b (`9a66c15`). Record the SDK (`dotnet --version`: 10.0.401), the passing counts of
   `Dexpace.Sdk.Http.SystemNet.Tests` and `Dexpace.Sdk.Conformance.Tests`, the coverage floor, and that V-conf is green with exactly the two permanent waivers. Create
   `94-phase-9-seams-and-gates` off `main` (no push).
3. **Re-run the design's facts as throwaway tests** in a scratch class `Scratch/FactProbe.cs` inside the new DI test project (created by task 2.0; run this step
   immediately after that task's scaffold builds, then delete the class before its commit). A fact that differs from the design is a **stop**: record it
   and amend the affected task before coding.
   - **V1** (the binder over the core records): `Get<RetryOptions>` with `MaxRetryAttempts = "5"` binds nothing under the generator and mis-converts under reflection (enum numeric `"7"` accepted, `TimeSpan` `"1000"` = 1000 days, a set member silently not bound). Only the *claim* is re-run; the plan builds no binder code, so the result only decides whether the design's P9-4 rationale still holds on the installed packages.
   - **V2** (`XmlConvert.ToTimeSpan`): `pt5s` throws, `-PT5S` is −5 s, `P5M` is 150 days, `PT0.00000001S` is zero; `int.TryParse(…, AllowLeadingSign, Invariant)` on `010`, `-5`, `+3`, `" 5 "`, `0x10`, `1_000`, `1e3`, `1.5`, `٣`, overflow. These become the divergence rows of tasks 2.1 and 2.2.
   - **V3** (`IHttpClientFactory` chain): the default chain's primary has `AllowAutoRedirect = true` and `new SystemNetHttpClient(factory.CreateHandler(name))` throws naming `AllowAutoRedirect`; with a redirect-off primary and `RemoveAllLoggers()` the chain walks to it and is accepted.
   - **V4** (validation): `ValidateOnStart()` registers `IStartupValidator`; `ValidateScopes` + `ValidateOnBuild` does not run options validation; two keyed registrations resolve silently to the last.
   - **V6** (`Microsoft.Extensions.Http.Resilience` 10.10.0, only after R5's lock check): `MaxRetryAttempts = 0` fails its own validation; `ShouldHandle = _ => ValueTask.FromResult(false)` keeps a `POST` at one wire send per SDK attempt.
   - **V8** (lease): the kit over a DI-built transport is red on `transport-15.owned-released` without the lease. This is re-run in task 4.2 as the lease's negative control (task 3.2 carries a unit-level equivalent).
   - **V9** (factory logging handlers) at `Information`: path verbatim in the factory's `Start processing HTTP request` line.
4. **R5's lock check.** In a scratch copy of `Directory.Packages.props` (never committed), add `Microsoft.Extensions.Http.Resilience` 10.10.0 and reference it from the scaffolded DI test project; `dotnet restore` (not locked) and read the result. Record `NU1109` or its absence in the task 4.3 notes. Revert. Do the same for `Microsoft.Extensions.Configuration.EnvironmentVariables` 10.0.12 (the `Tests` group; task 2.8), a package outside V5's closure that the offline cache may not hold.
5. **Open items for the lead** are listed at the end of this plan; none blocks execution (each task implements the design's chosen option).

---

# PR 1 — Seams and gates

Rows touched: `TRANSPORT-1` (the primary-handler seam), `TRANSPORT-15`, `TRANSPORT-30`, `NFR-2` (the audit record). Green with **no** new package: the CI coverage gate
(`scripts/ci/coverage-gate.cs`) fails closed with "library X has no coverage data in any report" for any `src/` library with no instrumented line, and a package that
lands empty, or with only the constants class (six `const` fields have no sequence point), would turn the PR red. The package is therefore scaffolded at the start of
PR 2, which brings the first covered code. The audit's policy change is safe to land first: the audit iterates the directories under `src/`, so a policy entry for a
library not yet on disk is unused.

## Task 1.1 — `SystemNetHttpClient.CreatePrimaryHandler` (`TRANSPORT-1`, `TRANSPORT-15`, `TRANSPORT-30`, `CFG-28`; P9-12; R6)

**Files.** Edit: `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs`, `src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt`,
`tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetHttpClientSurfaceTests.cs`. New (tests): `tests/Dexpace.Sdk.Http.SystemNet.Tests/PrimaryHandlerTests.cs`.

**Tests first** (`PrimaryHandlerTests`, `Unit`; each red as a compile error until the method exists).

- `CreatePrimaryHandler_goes_direct_and_never_follows_redirects`: `SystemNetHttpClient.CreatePrimaryHandler(new SystemNetHttpClientOptions())` returns a
  `SocketsHttpHandler` with `AllowAutoRedirect == false`, `UseProxy == false`, `Proxy == null`, `Credentials == null` (the environment's `HTTP_PROXY` is never consulted, `CFG-28`).
- `CreatePrimaryHandler_installs_the_proxy_adapter_and_leaves_server_credentials_unset`: with `Proxy = new ProxyOptions { Host = "proxy.test", Port = 3128, UserName = "u", Password = "p" }`,
  `UseProxy == true`, `Proxy` is non-null (assert on the behaviour: `Proxy.GetProxy(new Uri("http://example.test/"))` is `http://proxy.test:3128/` and `Proxy.Credentials` answers `Basic` only, no value reaches `ToString()`),
  `Credentials == null` (`TRANSPORT-30`).
- `CreatePrimaryHandler_warns_once_through_the_options_logger`: a `RecordingLogger` (TestSupport) given as `options.Logger` and a SOCKS proxy with credentials records exactly one event 113
  (`DexpaceLogEvents.TransportProxyFeatureUnsupportedId`) for the call; the same options through the **owned constructor** `new SystemNetHttpClient(options)` also record exactly one (R6's pin: the warning moved, it did not double).
- `The_returned_handler_is_accepted_by_the_handler_constructor`: `new SystemNetHttpClient(handler, optionsWithoutProxy)` does not throw (and disposing the transport leaves the handler undisposed, `TRANSPORT-15`); the caller disposes the handler.
- `CreatePrimaryHandler_rejects_null`: `ArgumentNullException` for a null `options`.
- `SystemNetHttpClientSurfaceTests.The_public_surface_gains_exactly_CreatePrimaryHandler` (pin): the static method exists with the one parameter and a `SocketsHttpHandler` return.

**Production change.** Add `public static SocketsHttpHandler CreatePrimaryHandler(SystemNetHttpClientOptions options)`, with the `<remarks>` the design asks for (the caller owns the
handler; pass it to the handler constructor, never with `options.Proxy` again; it is `TRANSPORT-1`'s primary for a factory-built chain). It builds exactly what `CreateOwnedClient` built
today (`AllowAutoRedirect = false`, `UseProxy = proxy is not null`, `Proxy = ProxyOptionsWebProxy`), emits `ProxyWarnings.Emit(options.Logger, proxy)` when a proxy is present (R6), and is `#pragma warning disable RS0030`-free (it constructs a `SocketsHttpHandler`, not an `HttpClient`).
Refactor `CreateOwnedClient` onto it (the `configure` seam applies after, as now) and delete the constructor's `ProxyWarnings.Emit` branch. Single-source the invariants in one method.

**PublicAPI.** `static Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient.CreatePrimaryHandler(Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions! options) -> System.Net.Http.SocketsHttpHandler!`.

**Verify.** V-build; V-sn `PrimaryHandlerTests`; then **every other SystemNet test unedited and green** (`V-sn` over the whole project, then V-sec for the SystemNet Security folder, then V-conf: the kit stays green with its two waivers; `NoImplicitEnvironmentReadArchitectureTests`
stays green: the new method adds no environment read). **IDs.** `TRANSPORT-1`, `TRANSPORT-15`, `TRANSPORT-30`, `CFG-28`. **Commit.** `feat: SystemNetHttpClient.CreatePrimaryHandler for factory-built chains`.

## Task 1.2 — The audit's policy record (`NFR-2`; P9-3; R11)

**Files.** Edit: `scripts/ci/dependency-audit.cs` (header comment included).

**Tests first (R11: there is no test project for the script).** The package does not exist in this PR, so this task's evidence is a refactor pin: before the change, `dotnet build Dexpace.Sdk.sln -c Release`, `dotnet pack … --output artifacts/packages` and `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages` print `4 libraries conform`; after it, the same. The DI-specific red and green (`no dependency policy` before the entry, `references project Dexpace.Sdk.Http.SystemNet` with a `null` policy, then `5 libraries conform`) and the three negative controls run in task 2.0, once the package is scaffolded.

**Production change.** As the design's "The audit": the policies table becomes `Dictionary<string, Policy>` over `record Policy(string? ThirdParty, string[] Projects)`; the four existing libraries are `new(null, [])`; the DI package is `new("Microsoft.Extensions.Http", ["Dexpace.Sdk.Http.SystemNet"])`.
`AuditDepsJson`: the allowed packages are `Closure(Facade) ∪ Closure(ThirdParty)` (the 13 of V5, band-matched by the existing rule); the allowed projects are `Closure(Core) ∪ ⋃ Closure(p) for p in Projects ∪ {library}`.
`AuditNuspec`: the expected set is `{Core} ∪ Projects ∪ {ThirdParty}` (the facade stays optional) and **every listed Dexpace project carries the library's own lockstep version** (the check core already gets, generalised from `Core` to `Core ∪ Projects`).
The header comment names the reading: "at most one third-party library" counts a root package whose closure is one publisher's lock-stepped family as one library (§10 entry 2), and first-party Dexpace packages are not third-party.

**Negative controls (never committed), run in task 2.0.** (a) Delete `Projects` from the DI policy: the audit names the project. (b) Add a second `PackageReference` (a scratch `Microsoft.Extensions.Hosting`... or any package not in the closure) to the package: the audit names it. (c) Change the nuspec's lockstep version of SystemNet (edit the packed nuspec): the lockstep check fires. Each restored.

**Verify.** `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages` exits 0 and prints `4 libraries conform`. **IDs.** `NFR-2`. **Commit.** `ci: dependency audit holds a library to its policy record`.

## Task 1.3 — PR 1 gate

V-gate (with the coverage gate: PR 1 changes only code the SystemNet suite covers, and the DI package is not yet in `src/`); the audit (task 1.2) and `dotnet pack` once more; `git diff --stat main...HEAD -- '*packages.lock.json' Directory.Packages.props` is **empty** (PR 1 moves no package). Hand the lead the branch name.

---

# PR 2 — The reader

Branch `94-phase-9-reader` off PR 1. Rows closed: `CFG-1` (the tiers), `CFG-2`, `CFG-3`, `CFG-4`, `CFG-5`, `CFG-6`, `CFG-7`, `CFG-10` (N/A pin), `CFG-14` (the constants, task 2.9), `CFG-38`, `OBS-35`; `NFR-2` (the scaffold). `CFG-11` and `CFG-37` close in PR 3 (they need the public surface).
Everything here is `internal` except `DexpaceConfigurationKeys` (task 2.9; R3). The coverage gate needs an instrumented line in the package, so the first commit that carries one is task 2.1: the scaffold commit (2.0) and the ban commit (2.0a) are not required to pass it, the PR is (task 2.10).

## Task 2.0 — The package and test-project scaffold, `Directory.Packages.props`, solution entries, lock files, the audit's controls (P9-2, P9-21; design "Packages, projects and dependencies")

**Files.** New: `src/Dexpace.Sdk.Extensions.DependencyInjection/Dexpace.Sdk.Extensions.DependencyInjection.csproj`, `GlobalUsings.cs`, `README.md` (stub: one paragraph; task 4.5 writes it),
`PublicAPI.Shipped.txt` (empty), `PublicAPI.Unshipped.txt` (empty until 2.9), `packages.lock.json`;
`tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests.csproj`, `GlobalUsings.cs`, `TestCategoryTests.cs`, `packages.lock.json`.
Edit: `Directory.Packages.props`, `Dexpace.Sdk.sln`; `tests/Dexpace.Sdk.AotSmoke/Dexpace.Sdk.AotSmoke.csproj` is **not** touched here (task 4.4).

**Tests first.** `TestCategoryTests.Every_test_carries_a_design_category` (`Unit`, the same body as the SystemNet suite's: `Assert.Empty(TestCategories.FindUncategorized(typeof(TestCategoryTests).Assembly))`) is the first test; red is the restore/compile error of a project that does not exist yet.

**Production change.**

1. `Directory.Packages.props`: `Runtime` group gains `<PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.12" />` with a comment naming §10 entry 2, `scripts/ci/dependency-audit.cs` and "the one third-party root of the hosting family; its closure is the 13 packages of V5, all band-matched". (`Microsoft.Extensions.Http.Resilience` waits for task 4.3, R5.)
2. The package csproj, modelled on `Dexpace.Sdk.Http.SystemNet.csproj`: `RootNamespace`/`AssemblyName`/`PackageId` `Dexpace.Sdk.Extensions.DependencyInjection`, a `Description` and `PackageTags` (`http;sdk;dependency-injection;configuration;dexpace`), `IsPackable`, `PackageReadmeFile` `README.md`;
   `ProjectReference` to core **and** `Dexpace.Sdk.Http.SystemNet` (P9-2); one `PackageReference` `Microsoft.Extensions.Http` (no `Version`); `InternalsVisibleTo Include="Dexpace.Sdk.Extensions.DependencyInjection.Tests"` only. `GlobalUsings.cs` with the curated set
   (`System`, `System.Collections.Generic`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`; trim any the build flags as unused).
3. The test csproj, modelled on `Dexpace.Sdk.Http.SystemNet.Tests.csproj` (`xunit.v3`, `coverlet.MTP`, `TrxReport`, `HangDump`, `IsTestProject`, `GenerateDocumentationFile=false`): project references to the package, core, SystemNet, `Dexpace.Sdk.Serialization.SystemTextJson`, `Dexpace.Sdk.Conformance`, `Dexpace.Sdk.TestSupport`.
   `GlobalUsings.cs` as the SystemNet suite's plus `Dexpace.Sdk.Core.Client`. (The test package `Microsoft.Extensions.Configuration.EnvironmentVariables` arrives with its first use, task 2.8.)
4. `Dexpace.Sdk.sln`: both projects added under the `src` and `tests` solution folders (`dotnet sln Dexpace.Sdk.sln add …`, then check the nested-project section is as the other entries').
5. Locks: `dotnet restore Dexpace.Sdk.sln` (unlocked, once) then commit the **two new** `packages.lock.json` files. Every existing project's lock file must be byte-identical (`git diff --stat -- '*packages.lock.json'` lists only the two new files); the AOT smoke has no lock file (its csproj comment). Then `V-restore` (locked) is green. Later tasks that add a test package (2.8, 4.3) change the DI test project's lock only, so the PR-4 diff against `main` is still exactly these two files.
6. Confirm `scripts/ci/coverage-gate.cs` discovers the new suite by its prefix (read the script's project discovery; no CI step changes, per the design) and that `.github/workflows/ci.yml` needs only a comment edit for the third driver (task 4.2).

**Verify.** V-restore; V-build (an empty-surfaced library under every gate: `RS0016` is silent on an empty `Unshipped`); V-di `TestCategoryTests`; `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` produces a `Dexpace.Sdk.Extensions.DependencyInjection.0.0.1-alpha.1.nupkg` with the README at its root.
**The audit's controls (task 1.2's policy record, R11).** Red: with the policy entry temporarily removed, `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages` fails with `Dexpace.Sdk.Extensions.DependencyInjection: no dependency policy`; with a `null` policy it fails with `references project Dexpace.Sdk.Http.SystemNet` and `runtime closure carries package Microsoft.Extensions.Http … which the dependency rule does not allow`; with the real entry it exits 0 and prints `5 libraries conform`. Then task 1.2's three negative controls, each restored.
Run task 0.1 step 3's probes and step 4's lock check here, delete the scratch class, then commit. **IDs.** `NFR-2` (scaffold), `NFR-8`/`NFR-9` (library gates apply from the first build). **Commit.** `feat: scaffold Dexpace.Sdk.Extensions.DependencyInjection and its tests`.

## Task 2.0a — Ban `ConfigurationBinder` in `src/` (P9-4)

**Files.** Edit: `BannedSymbols.txt`.

**Tests first.** A pin, proven by mutation (spike `validate9/binderlib`): add a scratch file in the package calling `new ConfigurationBuilder().Build().GetValue<int>("x")` and `.Get<RetryOptions>()` (the binder's extension methods live on `ConfigurationBinder`). V-build **already fails** before any ban, because `IsAotCompatible` plus warnings-as-errors turns the binder's trim and AOT warnings into errors (`IL2026` on both calls, `IL3050` on `Get<T>`), so the red is not pass versus fail: it is the diagnostic diff. The build output has **no `RS0030`** before the entry and **`RS0030` at both sites** after it (the `IL2026`/`IL3050` errors are present in both runs). Delete the scratch file.

**Production change.** One entry under a new comment block in the file's existing style: `T:Microsoft.Extensions.Configuration.ConfigurationBinder;Bind through the DI package's reader; the binder's generator binds no init accessor and its reflection mode is not AOT-safe (P9-4, design phase 9 V1)`, citing `CFG-5`–`CFG-7` and `OBS-35`.
Note in the comment that the type arrives transitively with `Microsoft.Extensions.Http` (V5) and is banned, not removed.

**Verify.** V-build over the package without the scratch file (no other `src/` project is affected: none references the binder); V-fmt. **IDs.** none owned (the guard for `CFG-5`–`CFG-7`). **Commit.** `chore: ban ConfigurationBinder in src (P9-4)`.

## Task 2.1 — `DurationGrammar` (`CFG-7`; P9-8, P9-27; V2)

**Files.** New: `src/Dexpace.Sdk.Extensions.DependencyInjection/DurationGrammar.cs`, `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/DurationGrammarTests.cs`.

**Tests first** (`DurationGrammarTests`, `Unit`; all red as a compile error). The surface is an internal `static bool TryParse(string text, out TimeSpan value, out string? rule)` (`rule` is the failure's grammar text, never the input). The case tables are the sibling ports of the design:

- **`Accepts_the_ISO_8601_forms`** (ports `configuration_test.rb` "CFG-7" and `duration.test.ts` "accepts an ISO-8601 duration"): `PT5S` 5 s; `P1D` 24 h; `P1DT2H3M4S` 93 784 s; `PT0S` zero; `pt5s` 5 s (lower-case, upper-cased first; V2's first divergence); `PT0.5S` 500 ms; `PT1M` 60 s (`M` after `T` is minutes); `PT0.0000001S` one tick (the seven-digit limit).
- **`Accepts_the_shorthand_forms`** (ports `configuration_test.rb`: `250ms`, `3s`, `2m`, `1h`, `1d`, `250MS`, `250 ms`, `1.5s`; `duration.test.ts`: `500ms`, `2S`, `3m`, `4h`, `5d`): the units `ms`, `s`, `m`, `h`, `d`, case-insensitive, an optional single space between number and unit, a fractional number.
- **`Reads_a_bare_integer_as_milliseconds`**: `1500` 1.5 s; `0` zero; `1000` one second (V1's `TimeSpan` "1000" = 1000 days is the divergence this pins).
- **`Tolerates_surrounding_whitespace`**: `"  PT5S  "`, `" 250ms "` (P9-27, ports `duration.test.ts`).
- **`Rejects_each_of_V2s_four_XmlConvert_divergences`** (explicit rows, the design's requirement): (1) case: accepted, not a failure (`pt5s` above); (2) sign: `-PT5S` fails (`XmlConvert` gives −5 s); (3) calendar units: `P5M`, `P1Y`, `P1W`, `P1Y2M` fail (`XmlConvert` gives 150/365 days); (4) truncation: `PT0.00000001S` (eight fractional digits) fails (`XmlConvert` gives zero).
- **`Rejects_malformed_input`** (ports `configuration_test.rb`: `PT-5S`, `P`, `P1X`, `-3s`, `-1`, `5w`, `soon`; `duration.test.ts`: `PT`, `5x`, `-500ms`): plus `P1DT` (a `T` with no time component), `PT1.5H` (a fraction on a non-seconds designator), `PT1S2M` (order), `1.5` (a fractional bare number, R8: both siblings accept it; P9-29), `1 5s`, `5 s ` followed by junk, the empty and whitespace-only string (callers never pass these: absent is the reader's, but the grammar fails them rather than returning zero), `P99999999D` (overflow past `TimeSpan.MaxValue` fails; no `OverflowException` escapes), and a `-` or `+` anywhere.
- **`The_three_grammars_land_on_one_scale`** (ports the `duration.test.ts` property): for every whole `s` in `0..100_000` (a loop, no property-testing dependency), `PT{s}S`, `{s}s` and `{s*1000}` parse to the same `TimeSpan`.
- **`Never_throws_for_an_arbitrary_string`**: a table of 200 pseudo-random strings (a fixed-seed `Random`, ASCII, Latin-1 and combining characters) never throws and never returns `true` with a negative value.
- **`A_failure_never_quotes_the_input`**: for a rejected input containing the marker `s3cr3t`, the returned rule does not contain it.

**Production change.** `DurationGrammar` as the design's grammar: `none` is **not** handled here (the reader maps it; the target's nullability decides); a leading `P`/`p` selects the ISO path over `P[nD][T[nH][nM][n[.f]S]]` (upper-cased first; `n` ASCII digits; `f` one to seven digits; at least one component; `T` only with a time component; `Y`, date-part `M` and `W` fail); otherwise `<number>[ ]<unit>` with `ms`/`s`/`m`/`h`/`d`; otherwise ASCII digits alone as ms; a result over `TimeSpan.MaxValue` fails; arithmetic in `decimal` or ticks as `long` with checked overflow (no float rounding). Hand-written scanners, no `Regex` (keeps `MA0051` and AOT trivial), methods under 70 lines.

**Verify.** V-build; V-di `DurationGrammarTests`. **IDs.** `CFG-7`. **Commit.** `feat: duration grammar for configuration values (CFG-7)`.

## Task 2.2 — `ScalarGrammar`: integer, boolean, decimal, enum, list (`CFG-5`, `CFG-6`, `OBS-35`; P9-8, P9-26, P9-27; V1, V2)

**Files.** New: `ScalarGrammar.cs` (package), `IntegerGrammarTests.cs`, `BooleanGrammarTests.cs`, `ScalarGrammarTests.cs` (tests).

**Tests first.** All red as compile errors (`Unit`).

- **`IntegerGrammarTests`** (ports `configuration_test.rb` "CFG-5" and `configuration.test.ts` `getInt`): `010` -> 10 (decimal; the live Ruby defect the table names); `-5` -> -5 (a negative is parsed and handed on, `CFG-5`'s "returned as-is"); `+3` -> 3; `" 5 "` -> 5 (trimmed, P9-27); `0` and `-0` -> 0 (`-0` normalised, the Node `negative zero` row); `2147483647` for `int` and `9223372036854775807` for `long` accepted; **rejected:** `1_000` (**Ruby tolerates it; this port rejects it, P9-26**), `0x10`, `1e3`, `1.5`, `5x`, `٣` (Arabic-Indic digit), `２` (full-width), `2147483648` for `int` (overflow), `""` and `"+"` and `"-"`, a value with an embedded space (`1 000`). Plus the `int`/`long` target split and "the rule text names the target type, never the input".
- **`BooleanGrammarTests`** (ports "CFG-6"): `true`, `TRUE`, `False`, `FaLsE` accepted; `" true "` accepted (**Ruby does not trim; P9-27 trims, recorded as a §11 resolution, P9-29**); `1`, `0`, `yes`, `no`, `on`, `off`, `t`, `f` each **rejected** (the eight tokens of the Ruby table), plus `truee`, `tru`, empty.
- **`ScalarGrammarTests`** (design "The grammars"): *decimal*: `0.5`, `2`, `-1` parse (sign left to the record's range), `1e2` is `NumberStyles.Float` so parses to 100, `NaN`, `Infinity`, `-Infinity` and `1,5` (comma, invariant culture) fail; the parse is culture-invariant under `CultureInfo.CurrentCulture = de-DE` (set and restored per test inside a `try/finally`; the tests of this class are `[Collection]`-serialised if the suite shares culture state, else not needed since `CurrentCulture` is per thread/async-flow: assert and note). *Enum*: `Body`, `body`, `" BODY "` -> `HttpLogLevel.Body`; `2` and `7` and `-1` **fail** (V1: the binder accepts numeric and undefined values; this reader does not, `OBS-35`); `Verbose` fails; the failure text **lists the names** (`None`, `Headers`, `Body`) and never the input; the same for `HeaderDropLogging` and `ProxyType` (a three-enum theory). *List*: scalar `a, b ,c` -> `[a, b, c]`; `1,2,3` for an integer element grammar; children `Key:0`, `Key:1` in configuration order; both a scalar and children present fails; `none` -> empty; a trailing comma or an empty element fails (not silently dropped); an element that fails its grammar fails the list naming the **index**, never the value.
- **`Every_failure_is_value_free`**: a theory over every grammar with the marker `s3cr3t-value` as input; no returned rule contains it.

**Production change.** `ScalarGrammar` with `TryParseInt32`, `TryParseInt64`, `TryParseBoolean`, `TryParseDecimal` (a `double`-returning `TryParseFinite`; `RetryOptions.Multiplier`/`Jitter` are `double`), `TryParseEnum<TEnum>` over `Enum.GetNames<TEnum>()` (AOT-safe; a `where TEnum : struct, Enum`), and a `SplitList` helper; every method returns a rule string, never an exception, and never reads the culture (`CultureInfo.InvariantCulture`, `NumberStyles.AllowLeadingSign` for integers and `NumberStyles.Float` for decimals, a pre-check that every character is an ASCII digit or the sign).

**Verify.** V-build; V-di `IntegerGrammarTests`, `BooleanGrammarTests`, `ScalarGrammarTests`. **IDs.** `CFG-5`, `CFG-6`, `OBS-35`. **Commit.** `feat: integer, boolean, decimal, enum and list grammars (CFG-5, CFG-6)`.

## Task 2.3 — `ConfigurationReader`: sections, absent, failures, unknown keys (`CFG-2`, `CFG-38`, `CFG-1`; P9-7, P9-9; R9, R10)

**Files.** New: `ConfigurationReader.cs`, `ConfigurationFailure.cs` (an internal readonly record struct `ConfigurationFailure(string Path, string Rule)` whose `ToString` is `"<Path>: <Rule>"`), `KeyTable.cs` (the known keys, one table the reader and `KeyTableTests` share); tests `EmptyValueTests.cs`, `TypedLookupTests.cs`, `ConfigurationPrecedenceTests.cs` (the first two now; the transform row of precedence in task 2.6).

**Tests first.** (`Unit`; red as compile errors.) The reader's internal surface is `ConfigurationReader.Read(IConfigurationSection? section) -> ClientConfiguration` where `ClientConfiguration` carries the built records and a `List<ConfigurationFailure>`; this task covers the scalar mechanics through a minimal read of two keys (`UserAgent`, `Retry:MaxRetryAttempts`) wired to the table, the rest of the table arriving in 2.4 and 2.5.

- **`EmptyValueTests`** (`CFG-2`; ports `configuration.test.ts` "empty environment values"): over `ConfigurationBuilder` with an in-memory provider with `Retry:MaxRetryAttempts = ""` layered above a second in-memory provider with `= "5"` (standing for env over JSON), the reader returns the **record default** (`RetryOptions` default, not 5, not `""`: the shadowed lower provider is not consulted, `CFG-2`'s 🚫 clause and §10 entry 25); the same for a whitespace-only value, and for one key of each kind (string `UserAgent`, integer `Retry:MaxRetryAttempts`, duration `Retry:BaseDelay`, list `Retry:RetryableStatusCodes`). A string key is never `""` (the `User-Agent` stays the record default).
- **`TypedLookupTests`** (`CFG-38`): an integer, a boolean (`Retry:HonorRetryAfter`) and a duration (`Retry:BaseDelay`) supplied **only by a lower provider** resolve (the one reader over the one `IConfiguration`); the record default applies only when no provider has the key; the case-insensitive spelling `retry:maxretryattempts` resolves alike.
- **`ConfigurationPrecedenceTests`** (`CFG-1`; ports `configuration.test.ts` "layered precedence"), first three rows here: three in-memory providers standing for appsettings, environment and an override: removing each from the top resolves the next, and the record default resolves when none has the key. (The fourth tier, the `Configure` transform, lands with its mechanism in task 2.6.)
- **`Failures_name_the_path_and_the_rule_and_never_the_value`**: `Retry:MaxRetryAttempts = "abc-s3cr3t"` fails with a path equal to `section.Path + ":Retry:MaxRetryAttempts"` (a section at `Dexpace:GitHub` gives `Dexpace:GitHub:Retry:MaxRetryAttempts`), a rule that names the grammar ("a base-10 integer"), and no part of the failure contains `s3cr3t`.
- **`The_reader_reports_every_failure_at_once`**: two bad keys plus one good one yield two failures in table order, the good value still applied (the design: "one boot reports every bad key").
- **`Unknown_keys_fail_naming_the_key`** (P9-9, R9, R10): `Retry:MaxRetries = 3` and a stray `Wat` fail with `"<path>:Retry:MaxRetries: unknown key"` in the configuration's own spelling; a case-variant of a **known** key is not unknown; a `Retry:` key under the wrong parent is unknown.
- **`A_null_section_binds_nothing`**: `Read(null)` returns the defaults and no failures (`AddDexpaceClient(name)` with no configuration, P9-5).
- **`A_root_section_is_rejected`** (F16): a section with an empty `Path` (`configuration.GetSection("")`, the root, whose children in a default host include every process variable) yields the one failure "pass the client's own section, not the root" and reads nothing; `AddDexpaceClient` throws the same message at registration (task 3.1).
- **`A_numeric_accessor_rejection_may_echo_the_number_and_nothing_else`** (P9-28, F7): `Retry:MaxRetryAttempts = -7` fails under its key; its rule may contain `-7` (the BCL helper's text), and with the marker `s3cr3t` in a sibling key no failure contains the marker. The test pins which accessors echo, so a change is deliberate.

**Production change.** The reader's skeleton: a `Section` wrapper over `IConfigurationSection` (path, `TryGetScalar(key)` that trims, treats empty/whitespace as absent, and leaves `none`-handling to the typed reader), a `Failures` collector, the `Collect`-style helper that applies a typed value to a record member under a `catch (ArgumentException)` (the record's accessor rejection becomes a failure `"<path>: <accessor message>"` — the core messages never echo a configured string, URL or credential, which `CredentialRedactionTests` in task 3.5 pins end to end; the BCL range helpers do echo a rejected **number**, which is allowed, P9-28), the child-key/scalar list reader, and the unknown-key pass (a walk of the section's `GetChildren()` against `KeyTable`).
No generic reflection-based mapping; each record gets its own method in 2.4 and 2.5. Methods stay under 70 lines by splitting per record.

**Verify.** V-build; V-di `EmptyValueTests`, `TypedLookupTests`, `ConfigurationPrecedenceTests`. **IDs.** `CFG-2`, `CFG-38`, `CFG-1`. **Commit.** `feat: configuration reader core, failures and absent values (CFG-2, CFG-38)`.

## Task 2.4 — The records: client, retry, redirect, logging (`CFG-5`–`CFG-7`, `OBS-35`; P9-8, P9-9; key table)

**Files.** New: `ConfigurationReader.Client.cs`, `.Retry.cs`, `.Redirect.cs`, `.Logging.cs` (partials of the one internal sealed class), `LogLevelBindingTests.cs`, `RecordBindingTests.cs`. Edit: `KeyTable.cs`.

**Tests first** (`Unit`).

- **`RecordBindingTests`**, one row per key of the design's table for these four records (each row builds a section with one key, reads, and asserts the record member; each also has a bad-value row that fails under the key):
  `BaseAddress` (`https://api.test/` -> `Uri`; `not a uri` and `/relative` fail; a URI with a fragment fails with the record's message, and the failure **does not contain the URL** (`UrlRedactor`, `XCUT-19`)); `UserAgent` (a value carrying CR, LF or another control fails under the key through the reader's header-value check and the failure does not echo it, F15: the record's accessor alone checks only for `null`, so it would pass start-up and fail on every call; the check may be a throwaway `Headers.Builder().Add("User-Agent", value)` under a `catch (ArgumentException)`, to be confirmed against the shipped `Headers` API); `OverallTimeout` and `AttemptTimeout` (`PT30S`, `30s`, `none` -> `null`, `0` and `-1s` fail through the record's `(0, 49 days]` range, `none` on a non-nullable target fails);
  `Retry:MaxRetryAttempts` (`3`; `-1` is the **record's rejection**, reported under the key: `CFG-5`'s "negative returned as-is, the accessor decides"; the huge `2147483648` fails the grammar), `Retry:BaseDelay`, `Retry:MaxDelay`, `Retry:FixedDelay` (`none` -> `null`), `Retry:Multiplier`, `Retry:Jitter` (decimal), `Retry:HonorRetryAfter`, `Retry:RetryableStatusCodes` (`429, 503`, children form, `none` -> empty set; **replace semantics**, V1's silent non-binding answered; a code of `99` or `600` fails through the record), `Retry:AttemptHeaderName`;
  `Redirect:MaxRedirects`, `Redirect:AllowHttpsToHttpDowngrade`, `Redirect:FollowSeeOther`, `Redirect:AllowedMethods` (`GET, HEAD, POST` through `Method.Of`; a token with a space fails and the failure does not echo it, `HTTP-20`; `none` -> empty set; replace semantics);
  `Logging:Level` (below), `Logging:BodyPreviewSize`, `Logging:AllowedHeaderNames`, `Logging:AllowedQueryParameters`, `Logging:UrlValuedHeaderNames` (**replace, not append**: V1's `IReadOnlyCollection<string>` appended to the default, so the test asserts the result does not contain a default member that was not listed), `Logging:OmitDisallowedHeaders`.
- **`LogLevelBindingTests`** (`OBS-35`): `Logging:Level = Body`, `body`, `" Headers "` bind; `2`, `7` and `Verbose` fail naming `Logging:Level` and listing `None`, `Headers`, `Body` (V1: the binder admits both numeric and undefined values; this reader does not, §10 entry 26); the section is always the caller's argument: a section **at a different path** (`Acme:Http`) binds the same, and a section that does **not** contain `Logging:Level` leaves `Level` at the record default (no baked key, `OBS-35`'s MUST NOT).
- **`Redirect_predicate_is_code_only`**: `Redirect:Predicate` is **not** in the table (reading it is an unknown-key failure).

**Production change.** The four partials, each a method of at most 70 lines that reads its keys through the 2.3 helpers and builds the record with object initialisers and `with` (P9-4); `KeyTable` gains the entries. The `with` application order is table order so failures come out in a stable order.

**Verify.** V-build; V-di `RecordBindingTests`, `LogLevelBindingTests`. **IDs.** `CFG-5`, `CFG-6`, `CFG-7`, `OBS-35`, `CFG-38`. **Commit.** `feat: bind client, retry, redirect and logging options (CFG-5..CFG-7, OBS-35)`.

## Task 2.5 — Transport, proxy and auth keys (`CFG-28`, `CFG-5`–`CFG-7`; P9-15, P9-18)

**Files.** New: `ConfigurationReader.Transport.cs`, `.Proxy.cs`, `.Auth.cs`, `TransportSnapshot.cs` (internal record: the `SystemNetHttpClientOptions` fields the reader owns without logger and clock, the `PooledConnectionLifetime`, and `ProxyOptions?`), tests `TransportBindingTests.cs`, `ProxyKeyTests.cs`, `AuthKeyTests.cs`. Edit: `KeyTable.cs`.

**Tests first** (`Unit`).

- **`TransportBindingTests`**: `Transport:Timeout` (`PT10S`, `none` -> `null` meaning "no transport default", an out-of-range value through the record), `Transport:HeaderDropLogging` (enum by name; numeric fails), `Transport:ResendBufferLimit` (`long`; `-1` through the record), `Transport:PooledConnectionLifetime` (duration or `none`; **default 2 minutes** when absent; `none` -> infinite).
- **`ProxyKeyTests`** (`CFG-28`): `Transport:Proxy:Type` (`Http`, `socks5`), `Host`, `Port`, `NonProxyHosts` (list, replace), `UserName`, `Password`, `BypassAll` -> a `ProxyOptions`; **any proxy key present with `Host` or `Port` missing** fails `"…:Transport:Proxy:Host: required when any proxy key is present"` (and `Port`); a bad `Port` (`0`, `70000`) fails through the record; no proxy key at all -> `Proxy == null`; **no key exists that turns the environment read on** (`Transport:Proxy:FromEnvironment` is an unknown key, the P9-15 pin the lead's open ruling would break); the password never appears in any failure or `ToString`.
- **`AuthKeyTests`** (P9-18): `Auth:ApiKey:Key` (with `HeaderName`, `Scheme`) -> `ApiKeyCredential`; `Auth:Basic:Username`/`Password` -> `BasicCredential`; `Auth:Digest:Username`/`Password` -> `DigestCredential`; `Auth:TokenRefreshMargin` (duration; through the record's range); each credential **requires its mandatory members** (a `Key` without `HeaderName` follows `ApiKeyCredential`'s own defaults/rejections; a lone `Auth:Basic:Username` fails naming `Password`), `AuthCredentials.Token` is **not** readable (unknown key); `AuthCredentials.ToString()` lists schemes only; and with `s3cr3t` as a password and as an API key, **no failure message and no `ToString` of the result contains it** (the reader's half of `CredentialRedactionTests`, finished end to end in task 3.5).

**Production change.** The three partials; the proxy reader builds the `ProxyOptions` only when a proxy key is present; the auth reader builds an `AuthCredentials` only when an `Auth:*` key is present (so an absent section is "no credentials", distinct from empty credentials, which task 3.5's validator needs).

**Verify.** V-build; V-di over the three classes. **IDs.** `CFG-28` (no implicit environment), `CFG-5`–`CFG-7`. **Commit.** `feat: bind transport, proxy and auth options`.

## Task 2.6 — `DexpaceClientState`, the setup with the `Configure` transforms, the validator, `ValidateOnStart` (`CFG-1`, `CFG-8`; P9-6, P9-10, P9-11; R3, R4, R12; V4)

**Files.** New: `DexpaceClientState.cs`, `DexpaceClientStateSetup.cs` (`IConfigureNamedOptions<DexpaceClientState>`), `DexpaceClientValidator.cs` (`IValidateOptions<DexpaceClientState>`), `DexpaceClientRegistration.cs` (the per-name record the builder writes: name, section, transforms, credential transforms, the proxy source, the captured `IServiceCollection`), `DexpaceClientRegistrations.cs` (internal `AddClientState`); tests `ValidationTests.cs`; edit `ConfigurationPrecedenceTests.cs`.

**Tests first** (`Unit`).

- **`ValidationTests`** (the design's row): through `DexpaceClientRegistrations.AddClientState(services, "github", section, registration)` and a provider:
  `Every_failure_is_reported_at_once` (two bad keys and an accessor rejection: one `OptionsValidationException` whose `Failures` list holds all three, each `"<path>: <rule>"`); `No_value_is_quoted` (marker `s3cr3t` across a bad password, API key, URL and header name: absent from `Message`, `Failures`, and the exception's `ToString()`; a numeric accessor echo is allowed, P9-28); `Unknown_keys_fail`; `A_host_less_provider_fails_at_first_resolution` (resolving `IOptionsMonitor<DexpaceClientState>.Get("github")` throws `OptionsValidationException`, V4); `IStartupValidator_runs_the_named_validators` (`provider.GetRequiredService<IStartupValidator>().Validate()` throws the same exception; R12: this is the service the hosting startup calls, so "fails at host start" is the same call); `Two_names_validate_independently` (a bad `a` does not fail `b`); `A_valid_client_validates_clean`; `ValidateOnStart_is_registered_once_per_name`.
  `Cross_property_combinations_the_records_accept_do_not_fail` (P9-10): `Retry:BaseDelay = 10s` over `Retry:MaxDelay = 1s`; `AttemptTimeout` above `OverallTimeout`; `Transport:Timeout` below `AttemptTimeout`: each validates clean (the combinations keep their core meaning, documented in task 4.5).
  `No_reload` (P9-11): after resolution, changing the in-memory provider and calling `IConfigurationRoot.Reload()` does not change `IOptionsMonitor.Get(name)` (no `IOptionsChangeTokenSource` is registered: assert `provider.GetServices<IOptionsChangeTokenSource<DexpaceClientState>>()` is empty).
- **`ConfigurationPrecedenceTests`** gains the fourth tier (`CFG-1`): the `Configure` transform applied **last, in call order**: two transforms (`o => o with { UserAgent = "a" }` then `o => o with { UserAgent = "b" }`) resolve `b`; a transform overrides a configuration value; removing the transform resolves the configuration; **a transform returning `null` fails naming the client** (`"github: Configure returned null"`); a transform that throws surfaces as a failure naming the client, not an unhandled exception from the options factory.
- **`CFG-10`'s pin** in the same class: `Precedence_has_no_removal_operation` asserts the internal registration type exposes no removal member (a reflection assertion on the registration record: no member named `Remove*`/`Clear*`), the vacuous-N/A row's evidence (design: "a transform is code, and a record has no removal"). A second `CFG-4` pin follows in task 2.8.

**Production change.** `DexpaceClientState` (R4: `internal sealed class`, public parameterless constructor, mutable members: the options snapshot `DexpaceClientOptions`, `TransportSnapshot`, `ProxyOptions?`, `AuthCredentials?`, the pooled-connection lifetime, the failure list). `DexpaceClientStateSetup.Configure(string? name, DexpaceClientState)` runs the reader for the registration's section, then the transforms in call order (the credential transform, which needs `IServiceProvider`, runs here with the provider the setup was constructed with). `DexpaceClientValidator.Validate(string? name, DexpaceClientState)` returns `ValidateOptionsResult.Fail(failures)` (each failure a `"<path>: <rule>"` string; **the wiring checks of tasks 3.3 to 3.6 are added to the same validator**, `SEAM-5`, the proxy sources and the auth rules, so the one validator owns every start-up failure). `AddClientState` registers `AddOptions<DexpaceClientState>(name)` with the setup (`IConfigureNamedOptions`, `TryAddEnumerable`), the validator (`TryAddEnumerable`) and `.ValidateOnStart()`, **and no `IOptionsChangeTokenSource`**. No public member is added (R3). Task 3.3 adds the zero-codec rule to this validator, so the tests of this task, which have no codec, are edited there (F1).

**Verify.** V-build; V-di `ValidationTests`, `ConfigurationPrecedenceTests`. **IDs.** `CFG-1`, `CFG-8` (immutability preserved), `CFG-10` (N/A pin). **Commit.** `feat: validated client state with ValidateOnStart and Configure transforms (CFG-1)`.

## Task 2.7 — The key-table drift guard (R1 of the design's residuals; R2)

**Files.** New: `KeyTableTests.cs`. Edit: `KeyTable.cs` (a `CodeOnly` list and a `HandlerLevel` list).

**Tests first** (`KeyTableTests`, `Unit`).

- `Every_public_init_property_is_in_the_key_table_or_on_the_code_only_list`: reflection (test-only) over the **seven** records of R2 (every public `init` property) and the three credential classes (every public constructor parameter: they have no `init` members, so a property census would be vacuous there): each is a key in `KeyTable` or in `KeyTable.CodeOnly` (`Redirect:Predicate`, `SystemNetHttpClientOptions.Logger`, `SystemNetHttpClientOptions.TimeProvider`, `SystemNetHttpClientOptions.Proxy` (read as `Transport:Proxy:*`), `ProxyOptions.ChallengeCredentials`, `AuthCredentials.Token`), and every `KeyTable` entry maps to a real member or sits in the explicit `KeyTable.HandlerLevel` category (`Transport:PooledConnectionLifetime`, which configures the primary handler and no record). The failure message names the missing member.
- `A_new_member_would_fail_the_guard` (the guard's own pin): a **scratch nested record type** in the test with an extra `init` property (and a scratch class whose constructor takes an extra parameter) is fed to the same checking function and the function reports it (so the guard is proven able to fail without editing a core record).
- `Every_key_is_documented_once`: the table has no duplicate keys (case-insensitive).

**Production change.** None beyond the `CodeOnly` list (the table is the production artefact). If the guard finds a record member 2.4 or 2.5 missed, that task's reader is fixed here, not the guard.

**Verify.** V-build; V-di `KeyTableTests`. **IDs.** drift guard for `CFG-38` (every typed value reaches the one reader). **Commit.** `test: key-table drift guard over the option records`.

## Task 2.8 — `EnvironmentNormalizationTests` (`CFG-3`, `CFG-4`, `CFG-11`'s production default; P9-26)

**Files.** New: `EnvironmentNormalizationTests.cs`. Edit: `Directory.Packages.props` (`Tests` group: `Microsoft.Extensions.Configuration.EnvironmentVariables` 10.0.12, with a comment naming `CFG-3`; `AddEnvironmentVariables(prefix)` lives in that package, which is not in V5's closure), the DI test csproj (one `PackageReference`) and its `packages.lock.json` (restore unlocked once and commit the change; it is one of the two DI lock files, so the PR's lock diff keeps its file count). The alternative, a test-local `IConfigurationSource` over a prefixed environment snapshot, is rejected: it would no longer prove the host's own `__` rule.

**Tests first** (`Integration`: the only test class that reads the real process environment; `CFG-11` says no other test does).

- `A_double_underscore_environment_variable_resolves_under_the_colon_key`: a process variable `DEXPACE_TEST_{guid}__Retry__MaxRetryAttempts=4` (a per-test GUID prefix; removed in `finally`; the class is `[Collection]`-serialised against any other environment-touching class) through `AddEnvironmentVariables($"DEXPACE_TEST_{guid}__")` resolves `Retry:MaxRetryAttempts` to 4 through the reader (`CFG-3`: .NET's own rule, `__` -> `:`, case-insensitive).
- `A_camel_case_and_an_upper_case_spelling_resolve_alike` (`CFG-4`, N/A: nothing normalises casing away, so there is no raw accessor to provide): `DEXPACE_TEST_{guid}__RETRY__MAXRETRYATTEMPTS` and `…__retry__maxretryattempts` resolve to the same value as the canonical spelling.
- `The_production_default_delegates_to_the_environment` (`CFG-11`): `AddEnvironmentVariables` over a variable set in the process is what an unconfigured host would read; a variable removed in `finally` is absent afterwards (the test leaves no residue: assert after cleanup).
- `A_variable_outside_the_prefix_is_never_read`: another GUID-prefixed variable is invisible to the section.

**Production change.** None (the behaviour is the host's; the test proves the reader sits on it). **Verify.** V-restore; V-build; V-di `EnvironmentNormalizationTests` twice (cleanup is order-independent). **IDs.** `CFG-3`, `CFG-4`, `CFG-11`. **Commit.** `test: environment-variable normalisation reaches the reader (CFG-3, CFG-4)`.

## Task 2.9 — `DexpaceConfigurationKeys` (`CFG-14`; P9-23)

**Files.** (Last in PR 2 so the package already has covered code; the class itself has no instrumented line.) New: `src/Dexpace.Sdk.Extensions.DependencyInjection/DexpaceConfigurationKeys.cs`, `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/KeyConstantsTests.cs`. Edit: the package's `PublicAPI.Unshipped.txt`.

**Tests first** (`KeyConstantsTests`, `Unit`).

- `The_published_values_are_exact`: `SectionName == "Dexpace"`, `MaxRetryAttempts == "Retry:MaxRetryAttempts"`, `LogLevel == "Logging:Level"`, `HttpProxy == "HTTP_PROXY"`, `HttpsProxy == "HTTPS_PROXY"`, `NoProxy == "NO_PROXY"`, and the class declares exactly those six public constants (reflection, test-only; the `keys_test.rb` "frozen, non-empty" analogue, the set is closed so a seventh constant is a reviewed change).
- `The_proxy_names_are_what_ProxyOptions_FromEnvironment_reads`: a recording lookup passed to `ProxyOptions.FromEnvironment(lookup, NullLogger.Instance)` observes the names `HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY` (and lower-case spellings if 5a's resolver reads them; assert the **upper-case three are among them**), and each equals the corresponding constant (ports the `keys_test.rb` "published, not read" naming rule).
- The third clause of the design's `KeyConstantsTests` ("no member of the package reads `SectionName`", an IL scan) needs the scanner and lands in task 3.6, where the scanner is copied and given its `ldstr` reader.

**Production change.** The static class with `///` docs citing `CFG-14` and `OBS-35`'s "no baked key" clause (the constants are published, never read; the section is always the caller's argument). **PublicAPI.** The `DexpaceConfigurationKeys` block of the design's surface (class, six `const` fields, implicit constructor line if the analyzer asks).

**Verify.** V-build; V-di `KeyConstantsTests`. **IDs.** `CFG-14`, `OBS-35` (no-baked-key half). **Commit.** `feat: DexpaceConfigurationKeys (CFG-14)`.

## Task 2.10 — PR 2 gate

V-gate (V-sec first: the three Security-bearing suites unedited); `scripts/knowledge --req CFG-5` ... re-read for each split row to confirm the planned 🚫 clause is the §10 entry 26 one. **Coverage:** the reader's grammars are pure code; run the coverage gate locally (`dotnet test --solution Dexpace.Sdk.sln --configuration Release --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`, then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and `scripts/ci/coverage-gate-selftest.sh artifacts/test-results`): the DI package must appear in a report and the 80% floor hold on this PR alone. `git diff --stat main...HEAD -- '*packages.lock.json'` lists exactly the two new DI lock files. Hand the lead the branch name.

---

# PR 3 — Registration

Branch `94-phase-9-registration` off PR 2. Rows closed: `CFG-11`, `CFG-37`, `SEAM-5`, `SEAM-6`; evidence for `SEAM-7`, `TRANSPORT-1`, `XCUT-10`. The public surface of the design's "The public surface" lands here, task by task, each task's `PublicAPI.Unshipped.txt` lines added with its members.

## Task 3.1 — `AddDexpaceClient`, `DexpaceClientBuilder`, one registration per name, `Configure*` (`CFG-37`, `CFG-1`; P9-5, P9-6)

**Files.** New: `DexpaceServiceCollectionExtensions.cs`, `DexpaceClientBuilder.cs`; tests `NullGuardTests.cs`, `AddDexpaceClientTests.cs`. Edit: the package's `PublicAPI.Unshipped.txt`, `DexpaceClientRegistrations.cs`.

**Tests first.**

- **`AddDexpaceClientTests`** (`Unit`): `AddDexpaceClient("github")` returns a `DexpaceClientBuilder` whose `Name`, `Services` and `HttpClient` are set (`HttpClient` is an `IHttpClientBuilder` named `"Dexpace.github"`); `AddDexpaceClient("github", section)` is the section overload (the parameter is an `IConfigurationSection`: a root `IConfiguration` does not compile, and a section with an empty `Path`, `GetSection("")`, throws `ArgumentException` at the call with the reader's pointed message, F16); **a second call for the same name** returns the existing builder when the configuration argument is the same instance (or both are absent) and throws `InvalidOperationException` naming the client otherwise (the design's "one name registers once"); two names are independent; `Configure` and `ConfigureTransport` record their transforms **in call order** on the registration; a blank, whitespace-only or null name is `ArgumentException`/`ArgumentNullException` (a name is a key and an `HttpClient` name, `CFG-37`); names are case-sensitive (`GitHub` ≠ `github`, the keyed-services rule) and the test says so.
- **`NullGuardTests`** (`CFG-37`; `Unit`): by reflection over the **public surface** of `DexpaceServiceCollectionExtensions` and `DexpaceClientBuilder` (a test-only use of reflection), every public method, every reference-type parameter: passing `null` for that parameter (with valid values for the rest, built from a table of one example value per parameter type) throws `ArgumentNullException` whose `ParamName` equals the parameter name; a blank `name` throws `ArgumentException`. The test **fails if a public method has a parameter type for which the table has no example** (a new method cannot silently skip the guard), so every later task keeps it green by adding its parameter type's example.

**Production change.** The extension class (two overloads; `ArgumentNullException.ThrowIfNull` first) and the sealed builder (`Name`, `Services`, `HttpClient`, `Configure`, `ConfigureTransport`, the later `Use*` members arriving with their tasks); the builder writes into the registration of task 2.6 and calls `DexpaceClientRegistrations.AddClientState`. One registration per name is a check on the `IServiceCollection` (a marker singleton descriptor keyed by name).

**PublicAPI.** `DexpaceServiceCollectionExtensions` (two methods), `DexpaceClientBuilder` (`Name`, `Services`, `HttpClient`, `Configure`, `ConfigureTransport`).

**Verify.** V-build; V-di `AddDexpaceClientTests`, `NullGuardTests`. **IDs.** `CFG-37`, `CFG-1` (tier 1 mechanism), `SEAM-6` (the one-registration-per-name rule applied to the client). **Commit.** `feat: AddDexpaceClient and DexpaceClientBuilder (CFG-37)`.

## Task 3.2 — The named client: factory handler chain, `RemoveAllLoggers`, infinite lifetime, the primary-handler lease, the keyed transport (`TRANSPORT-1`, `XCUT-10`, `XCUT-22`, `OBS-11`; P9-12, P9-13, P9-14; R7; V3, V8, V9)

**Files.** New: `PrimaryHandlerLease.cs`, `NamedClientTransport.cs` (the keyed transport factory); tests `NamedClientTests.cs`, `PrimaryHandlerLeaseTests.cs`, `NoRetryingHandlerTests.cs`, and the shared helpers `TestClients.cs` and `StubSerde.cs`. Edit: `DexpaceClientBuilder.cs` / `DexpaceClientRegistrations.cs`, the package `PublicAPI.Unshipped.txt` (no new public member).

**Tests first.** (The three classes build their clients through `TestClients.Add`. Until task 3.3 gives it `UseSerde` the helper registers no codec; task 3.3 changes only the helper, so these tests stay green when the zero-codec rule lands, F1.)

- **`NamedClientTests`** (`Unit`): for `AddDexpaceClient("github")`: the keyed `IAsyncHttpClient` resolves to a `SystemNetHttpClient` (the 8b handler construction over `IHttpMessageHandlerFactory.CreateHandler("Dexpace.github")`); the chain, read through the factory, is `<LifetimeTracking> -> <caller's handlers> -> SocketsHttpHandler` with **`AllowAutoRedirect == false`** on the primary (V3; the test reads the chain with a `DelegatingHandler` spy added through `builder.HttpClient.AddHttpMessageHandler` that walks `InnerHandler`); **`RemoveAllLoggers()` took effect** (V9: with an `ILoggerProvider` recording everything at `Trace`, a call through the transport produces no `System.Net.Http.HttpClient.Dexpace.github.*` category line, only the SDK's `Dexpace.Sdk` events); **the handler lifetime is infinite** (resolving the factory options `IOptionsMonitor<HttpClientFactoryOptions>.Get("Dexpace.github").HandlerLifetime == Timeout.InfiniteTimeSpan`); **`PooledConnectionLifetime`** on the primary equals the default 2 minutes, `Transport:PooledConnectionLifetime = 10s` sets it, `none` -> infinite; **`ConfigureHttpClient` is ignored** (setting `BaseAddress` and `DefaultRequestHeaders` on `builder.HttpClient` changes nothing about what the transport sends: a recording `DelegatingHandler` sees no extra header, and the request URI is the SDK's); **a substituted redirect-following primary is refused at first resolution** (`builder.HttpClient.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler())` makes `GetRequiredKeyedService<IAsyncHttpClient>("github")` throw `ArgumentException` naming `AllowAutoRedirect`, V3's backstop, `TRANSPORT-1`); the container never builds the transport from the factory-created `HttpClient` (`CreateClient` is never called: assert with a `IHttpClientFactory` decorator that records `CreateClient` calls).
- **`PrimaryHandlerLeaseTests`** (`Integration`; the loopback server of the kit): after one call through the transport and a provider disposal, **the pooled connection is released** (the kit's `WaitForConnectionReleasedAsync` pattern, as `transport-15.owned-released` uses it; without the lease the test is red, V8: the plan's red is "run first with the lease's `Track` call commented out", never committed); **a caller's `DelegatingHandler` is not disposed** by the provider's disposal (a counting handler's `Dispose` count stays 0: `XCUT-22`, the factory owns it); **the lease disposes only the package's instance**: a caller who replaces the primary (with a custom non-Sockets `HttpMessageHandler` that counts `Dispose`: `SocketsHttpHandler` is sealed, so it cannot be subclassed to count) leaves the package's discarded, never-used instance, which the lease disposes (observed through the lease's internal tracked list under `InternalsVisibleTo`: using that handler after the provider's disposal throws `ObjectDisposedException`), and the caller's replacement is untouched (its count stays 0); the lease is created before and disposed after the transport (an ordering probe through a spy transport factory); a provider built but never resolved disposes nothing and throws nothing.
- **`NoRetryingHandlerTests`** (`Unit`; `XCUT-10`'s DI clause): the named client's handler chain contains **no handler the package added** besides the primary (a walk of the chain finds only `LifetimeTrackingHttpMessageHandler`, any handler the test itself added, and the `SocketsHttpHandler`), and the package never calls `AddStandardResilienceHandler` or any `Polly` registration (the service collection has no descriptor from `Microsoft.Extensions.Http.Resilience` or `Polly`).

**Production change.** Per client the package calls `services.AddHttpClient("Dexpace." + name)` and: (1) `ConfigurePrimaryHttpMessageHandler(sp => …)` builds the primary with `SystemNetHttpClient.CreatePrimaryHandler(transportOptions with { Logger = …, TimeProvider = … })` from the **validated state** (the reader's snapshot, including the proxy source of task 3.6; `options.Proxy` here, never on the transport), sets `PooledConnectionLifetime`, resolves the keyed `PrimaryHandlerLease` and calls `lease.Track(handler)` (R7), returns it; (2) `.RemoveAllLoggers()`; (3) `.SetHandlerLifetime(Timeout.InfiniteTimeSpan)`; (4) the keyed `IAsyncHttpClient` **default** (kept as a `ServiceDescriptor` for task 3.3) is a factory that resolves, **in this order**, the lease, `IOptionsMonitor<DexpaceClientState>.Get(name)` (which fails host-less, task 2.6), `IHttpMessageHandlerFactory.CreateHandler("Dexpace." + name)`, then returns `new SystemNetHttpClient(chain, transportOptions with { Proxy = null, … })` (8b's handler construction; its redirect check is the backstop). `ILoggerFactory` supplies the categories `Dexpace.Sdk` (pipeline) and `Dexpace.Sdk.Http.SystemNet` (transport); `TimeProvider` is `GetService<TimeProvider>() ?? TimeProvider.System`.
`PrimaryHandlerLease : IDisposable` (internal) holds the tracked handlers and disposes them once (latched); registered per client as a keyed singleton **factory** so the container disposes what it returns.
Factories run on the root provider (design B), so a scoped dependency fails under `ValidateScopes` (task 3.7).

**Verify.** V-build; V-di `NamedClientTests`, `PrimaryHandlerLeaseTests`, `NoRetryingHandlerTests`; V-sec (SystemNet's Security folder unedited). **IDs.** `TRANSPORT-1` (named-client clause), `XCUT-10` (DI clause), `XCUT-22`, `OBS-11`..`OBS-18` (the one HTTP log authority), `TRANSPORT-15`. **Commit.** `feat: named client over IHttpClientFactory with a redirect-off primary and a handler lease`.

## Task 3.3 — Single registration: `UseTransport`, `UseSerde`, the descriptor scan (`SEAM-5`, `SEAM-6`, `SEAM-7`; P9-16, P9-17; V4)

**Files.** New: `ImplementationNames.cs` (the instance/implementation/factory-method naming helper); tests `SingleRegistrationTests.cs`, `KeyedSingletonTests.cs`. Edit: `DexpaceClientBuilder.cs`, `DexpaceClientValidator.cs`, `PublicAPI.Unshipped.txt`; `TestClients.cs` (the helper now calls `UseSerde(StubSerde.Instance)`, so 3.2's three classes need no edit); `ValidationTests.cs` and `ConfigurationPrecedenceTests.cs` (task 2.6's classes, which have no codec and use `AddClientState` directly: they register a stub keyed `ISerde` descriptor through the helper in this same commit, because the validator now fails a client with no codec, F1).

**Tests first** (`Unit`).

- **`SingleRegistrationTests`** (`SEAM-5`, `SEAM-6`): **explicit install wins** (`UseTransport(instance)` removes the default descriptor and adds an **instance** descriptor; `UseTransport(factory)` adds a **factory** descriptor; the resolved keyed transport is the caller's, the package's `SystemNetHttpClient` is never created: assert the lease tracked nothing); **the container never disposes an instance it did not create** (`await provider.DisposeAsync()` leaves a caller's instance transport undisposed and disposes a factory's product: `XCUT-22`, `XCUT-23`; a synchronous `Dispose()` throws for an async-only transport, design G, F21); **`UseSerde`** likewise (instance and factory forms); **no codec** fails `ValidateOnStart` with `client 'github' has no ISerde: call UseSerde(...) on AddDexpaceClient("github")` (exact text pinned, naming the client and `UseSerde`; the install hint); **`SEAM-6`: the same instance twice, or the same delegate twice, is a no-op** (one descriptor remains); **a different one throws `InvalidOperationException` at the `UseTransport`/`UseSerde` call** naming the client, the incumbent's and the rejected implementation's type names (P9-16: earlier than §3.6's validator timing; the test asserts the throw happens at the call, not at start); **`SEAM-5`'s multi-candidate branch**: a raw `services.AddKeyedSingleton<IAsyncHttpClient>("github", …)` beside the package's descriptor (and the same for `ISerde`) fails at start with a failure **listing every registered implementation** (instance type, implementation type, or the factory's declaring type and method, V4: `GetRequiredKeyedService` would otherwise silently return the last), the failure appears in the one `OptionsValidationException` with the reader's failures; a raw descriptor under **another** client's key is not counted.
- **`KeyedSingletonTests`** (`SEAM-7`'s evidence; `Unit`): 64 concurrent first resolutions of the keyed `IAsyncHttpClient`, `ISerde`, `HttpPipeline` and `DexpaceClientOptions` run each factory **once** (a counting factory; the tasks are released from dedicated threads, `TaskCreationOptions.LongRunning` and a `ManualResetEventSlim`, never a pool-starving barrier: the 8b finding recorded in the `transport-adapter` note); all 64 get the same instance. "An unresolved state stays re-evaluable" has nothing to apply to (a built container gains no registrations): the test comment says so and the checklist records `SEAM-7` as N/A with this test cited.

**Production change.** `UseTransport`/`UseSerde` (instance and factory overloads; `ArgumentNullException.ThrowIfNull`), the descriptor bookkeeping on the registration (the default descriptor is kept so the explicit one can replace it), the `SEAM-6` comparison (same instance by reference, same delegate by `Equals`/`Delegate` equality), the validator's descriptor scan over the captured `IServiceCollection` for keyed `IAsyncHttpClient` and `ISerde` under the client's key, and the zero-candidate `ISerde` message. **PublicAPI.** `UseTransport` ×2, `UseSerde` ×2. `NullGuardTests` gains the example values for `IAsyncHttpClient`, `ISerde` and the two `Func`s.

**Verify.** V-build; V-di `SingleRegistrationTests`, `KeyedSingletonTests`, `NullGuardTests`. **IDs.** `SEAM-5`, `SEAM-6`, `SEAM-7`, `XCUT-22`, `XCUT-23`, `CFG-37`. **Commit.** `feat: single-registration check and explicit transport/codec install (SEAM-5, SEAM-6)`.

## Task 3.4 — The keyed pipeline and options, `AddPolicy` (`PIPE-27`, `PIPE-35`, `CFG-8`; P9-5; V3)

**Files.** New: `NamedClientPipeline.cs`; tests `NamedPipelineTests.cs`. Edit: `DexpaceClientBuilder.cs`, `PublicAPI.Unshipped.txt`.

**Tests first** (`Unit`).

- `The_keyed_pipeline_is_the_default_seven_policies_over_the_keyed_transport_with_the_bound_options`: with `Retry:MaxRetryAttempts = 1` and `Redirect:MaxRedirects = 5` in configuration, a call through the keyed `HttpPipeline` over a `ScriptedTransport` (TestSupport; the client's `UseTransport(scripted)`) retries once and follows at most 5 hops: the bound options reached `Build(transport, options)` (V3: no core change needed).
- `AddPolicy_places_a_callers_policy_by_stage_sort` (`PIPE-35`): a custom policy at a stage between two defaults lands where the stage sort puts it, observed through a probe policy's recorded order; several `AddPolicy` calls keep call order within a stage.
- `The_keyed_DexpaceClientOptions_is_the_validated_snapshot`: the keyed `DexpaceClientOptions` is the one the pipeline was built with (reference-equal), immutable (`with` yields a copy; the registration's snapshot unchanged, `CFG-8`).
- `The_pipeline_does_not_own_the_transport` (`PIPE-27`): disposing the keyed pipeline does not dispose the keyed transport; the container disposes the transport once.
- `A_factory_resolved_from_the_root_provider_cannot_take_a_scoped_service`: the negative at unit level (the full `ValidateScopes` proof is task 3.7).

**Production change.** The keyed `HttpPipeline` factory: `PipelineBuilder.Flatten(DexpacePipeline.CreateDefault(transport, authPolicy, logger, timeProvider))`, then each `AddPolicy` factory's policy, then `.Build(transport, options)` (`DexpacePipeline.CreateDefault(IAsyncHttpClient transport, HttpPipelinePolicy? authPolicy = null, ILogger? logger = null, TimeProvider? timeProvider = null)`; `PipelineBuilder.Build(IAsyncHttpClient transport, DexpaceClientOptions options)`); the keyed `DexpaceClientOptions` factory returns the state's snapshot. `AddPolicy(Func<IServiceProvider, HttpPipelinePolicy>)`.

**PublicAPI.** `AddPolicy`. **Verify.** V-build; V-di `NamedPipelineTests`, `NullGuardTests`. **IDs.** `PIPE-27`, `PIPE-35`, `CFG-8`. **Commit.** `feat: keyed pipeline and options per named client, AddPolicy`.

## Task 3.5 — Auth: `UseAuth`, `UseAuthPolicy`, `ConfigureCredentials`; no secret anywhere (`XCUT-18`, `XCUT-19`; P9-18)

**Files.** New: tests `AuthRegistrationTests.cs`, `CredentialRedactionTests.cs`. Edit: `DexpaceClientBuilder.cs`, `NamedClientPipeline.cs`, `DexpaceClientValidator.cs`, `PublicAPI.Unshipped.txt`.

**Tests first** (`Unit`).

- **`AuthRegistrationTests`**: `Auth:*` keys plus `UseAuth(descriptor)` install a `MultiSchemeAuthPolicy(descriptor, credentials, timeProvider)` (a call through the pipeline carries the configured API key header, with an `AuthDescriptor` the test builds); `ConfigureCredentials((sp, credentials) => credentials with { Token = sp.GetRequiredService<TokenCredential>() })` supplies what configuration cannot hold, **after** the configured values, with the container available; `UseAuthPolicy(sp => new ChallengeAuthPolicy(handler))` installs any other policy; **credentials with neither `UseAuth` nor `UseAuthPolicy` fail `ValidateOnStart`** naming the client (credentials silently not sent are the misconfiguration nobody sees); **`UseAuth` and `UseAuthPolicy` together throw `InvalidOperationException` at the second call** (`SEAM-6`'s rule for the one auth authority); `UseAuth` with no credentials is legal (a descriptor with `NoAuth` requirements) and installs nothing sent; a `ConfigureCredentials` returning `null` fails naming the client.
- **`CredentialRedactionTests`**: configure `Auth:ApiKey:Key = s3cr3t-key`, `Auth:Basic:Password = s3cr3t-pass`, `Auth:Digest:Password = s3cr3t-digest`, plus a deliberately bad sibling key so validation fails: **no failure message, no `OptionsValidationException.ToString()`, no `AuthCredentials.ToString()` (schemes only, P6c-20) and no log line captured from the whole start-up and a failing first call contains any of the three secrets**; and the same for a bad `Transport:Proxy:Password`.

**Production change.** The three builder methods (`UseAuth(AuthDescriptor)`, `UseAuthPolicy(Func<IServiceProvider, HttpPipelinePolicy>)`, `ConfigureCredentials(Func<IServiceProvider, AuthCredentials, AuthCredentials>)`), the registration state, the policy factory in the pipeline (the authority passed to `DexpacePipeline.CreateDefault`) and the validator's two rules. **PublicAPI.** the three members; `NullGuardTests` examples for `AuthDescriptor` and the `Func`s.

**Verify.** V-build; V-di `AuthRegistrationTests`, `CredentialRedactionTests`, `NullGuardTests`. **IDs.** `XCUT-18`, `XCUT-19` (evidence), `SEAM-6` (auth authority). **Commit.** `feat: auth registration over redacting credentials`.

## Task 3.6 — Proxy: `UseProxyFromEnvironment`; the environment-read scan (`CFG-11`, `CFG-14`, `CFG-28`; P9-15)

**Files.** New: `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Architecture/IlCallScanner.cs` (a copy of 8b's scanner, header and all, **plus an `ldstr` reader**: 8b's scanner matches only `call`, `callvirt`, `newobj`, `ldftn` and `ldvirtftn` (`IlCallScanner.cs:120`), and the `SectionName` clause scans string operands, so the copy gains an enumerator of `ldstr` operands (resolved through the module) and a pin, `The_scanner_finds_a_planted_ldstr`; the SystemNet suite's file is **not** edited or shared: a different test assembly), `Architecture/NoImplicitEnvironmentReadArchitectureTests.cs`; tests `ProxyRegistrationTests.cs`, `ConfigurationSeamTests.cs`. Edit: `DexpaceClientBuilder.cs`, `DexpaceClientValidator.cs`, `KeyConstantsTests.cs`, `PublicAPI.Unshipped.txt`.

**Tests first.**

- **`ProxyRegistrationTests`** (`Unit`): explicit `Transport:Proxy:*` keys reach the primary handler (`UseProxy == true`, the adapter answers for the configured host; through the chain walk of task 3.2); **`UseProxyFromEnvironment(lookup)`** calls `ProxyOptions.FromEnvironment(lookup, logger)` with the caller's lookup and the result reaches the primary; the documented `IConfiguration` form `UseProxyFromEnvironment(key => configuration[key])` serves `HTTPS_PROXY` from an in-memory provider; **resolution is lazy**: a recording lookup observes **no call at registration** or at `AddDexpaceClient`, only at the transport's first resolution (the design: a registration-time read would run before the host's configuration exists); **two proxy sources fail `ValidateOnStart`** ("two proxy sources": explicit keys plus `UseProxyFromEnvironment`, or `ConfigureTransport` setting `Proxy` plus either); `ConfigureTransport(o => o with { Proxy = … })` counts as an explicit source; a client with no proxy source goes direct (`UseProxy == false`, `HTTP_PROXY` in the real environment is ignored: a variable set under a GUID name cannot affect it, and the test asserts `HttpClient.DefaultProxy` is never consulted via the scanner below); `UseProxyFromEnvironment()` (no lookup) reads the process environment **only** through `ProxyOptions.FromEnvironment(ILogger)`: covered by the scan, not by touching real variables.
- **`ConfigurationSeamTests`** (`CFG-11`): the recording lookup observes **every proxy variable the resolver reads, and no other** (5a's resolver's names; the set equality is asserted, which is what "the seam" means).
- **`NoImplicitEnvironmentReadArchitectureTests`** (`Unit`; the scan of design F, copied from 8b's with the DI assembly as subject): over `typeof(DexpaceClientBuilder).Assembly`, the callee set (`Environment.GetEnvironmentVariable` both overloads, `GetEnvironmentVariables`, `ProxyOptions.FromEnvironment` all three overloads, `HttpClient.get_DefaultProxy`/`set_DefaultProxy`, `WebRequest.get_DefaultWebProxy`/`set_DefaultWebProxy`) has **exactly two permitted callers of `ProxyOptions.FromEnvironment`: the two `UseProxyFromEnvironment` code paths** (the no-argument form's lambda or method and the lookup form's), and `GetEnvironmentVariable(s)` has **no caller at all**; `The_scanner_finds_a_planted_call` (a nested type planted with the forbidden calls is found, the copied scanner's own pin); `The_scan_would_catch_a_configuration_switch` (a planted type calling `ProxyOptions.FromEnvironment` from a method named `Read…` fails the permitted-caller rule: P9-15's open half has a standing guard).
- **`KeyConstantsTests`** gains the third clause of task 2.9: `No_member_of_the_package_reads_SectionName` (the IL scan finds no `ldstr "Dexpace"` load in any method other than `DexpaceConfigurationKeys`' own constant, and no reference to the constant's field from the package: a `const` is inlined, so the scan is over `ldstr` operands of the exact string, the `keys_test.rb` "published, not read" rule and `OBS-35`'s no-baked-key clause).

**Production change.** `UseProxyFromEnvironment()` and `UseProxyFromEnvironment(Func<string, string?> lookup)` on the builder (recorded on the registration, resolved when the primary is built, task 3.2's delegate, logger = the container's), and the validator's proxy-source rule. **PublicAPI.** the two members. `NullGuardTests` example for `Func<string,string?>`.

**Verify.** V-build; V-di `ProxyRegistrationTests`, `ConfigurationSeamTests`, `NoImplicitEnvironmentReadArchitectureTests`, `KeyConstantsTests`. **IDs.** `CFG-11`, `CFG-14` (the not-read clause), `CFG-28`, `OBS-35` (no baked key). **Commit.** `feat: explicit proxy sources and the environment-read scan (CFG-11, CFG-28)`.

## Task 3.7 — `ProviderValidationTests` and `SerdeRegistrationTests` (the card's test; `SEAM-5`; P9-17, P9-21)

**Files.** New: `ProviderValidationTests.cs`, `SerdeRegistrationTests.cs`.

**Tests first** (`Unit`).

- **`ProviderValidationTests`** (the roadmap card's "a test that builds the provider with `ValidateScopes` and `ValidateOnBuild`"; styleguide `02-dependency-injection.md` 2.5): a `ServiceProvider` built with `ValidateScopes = true, ValidateOnBuild = true` over two clients (each with a `UseSerde`, the second also with an `UseTransport(instance)`: a client with no codec fails validation and every keyed resolution, design D, F1) resolves **every keyed registration** (`HttpPipeline`, `IAsyncHttpClient`, `ISerde`, `DexpaceClientOptions`, under each client's name) **from the root and from a scope** without an `InvalidOperationException`; a factory that captures a **scoped** service (a `UseSerde(sp => new Serde(sp.GetRequiredService<ScopedThing>()))`) **fails** under `ValidateScopes` at resolution (the house rule 2.4, the negative control); `ValidateOnBuild` builds with a bad configuration **without** failing (V4: it does not run options validation: the test pins that the failure arrives at `IStartupValidator.Validate()` or first resolution, so the plan's validation-timing claim stays honest) and the same provider's `IStartupValidator.Validate()` then throws.
- **`SerdeRegistrationTests`** (the 7a hand-off; P9-17): `CreateDefaultOptions_with_a_context_round_trips_through_the_registered_serde`: `UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(TestJsonContext.Default)))` (a small source-generated `JsonSerializerContext` in the test project, `[JsonSerializable]` over a `record`) resolves keyed under the client's name and round-trips a value including a `Tristate<T>` member (the codec wires it on the copy); the DI package does not reference the codec (an assembly-reference assertion: `typeof(DexpaceClientBuilder).Assembly.GetReferencedAssemblies()` contains no `Dexpace.Sdk.Serialization.SystemTextJson`).

**Production change.** None expected; if a factory captured a scoped service by accident, fix the factory (they run on the root, design B). **Verify.** V-build; V-di over both. **IDs.** `SEAM-5`, `NFR-8` (provider validation), P9-17. **Commit.** `test: provider validation and the serde composition`.

## Task 3.8 — `NamedClientRedirectWireTests`, the one new `Security` class (`TRANSPORT-1`, `REDIR-7`..`REDIR-9`, `XCUT-17`; P9-24; phase 1 S3 row 9)

**Files.** New: `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Security/NamedClientRedirectWireTests.cs`.

**Tests first** (`Security`; two loopback origins from `Dexpace.Sdk.Conformance.Wire`, modelled on `Dexpace.Sdk.Http.SystemNet.Tests/Security/RedirectWireTests.cs`, which is **not** edited).

- `The_named_client_returns_the_3xx_and_never_contacts_the_target`: through the keyed `IAsyncHttpClient` of `AddDexpaceClient("kit")`, a request carrying `Authorization`, `Cookie`, `Proxy-Authorization` and a custom header to origin A (answering `302 Location: <origin B>`) returns the `302` **unchanged**; origin B records **zero** requests; no credential bytes or the seed secret appear in anything B received.
- `Through_the_pipeline_Authorization_is_stripped_on_the_hop`: the keyed `HttpPipeline` over the same origins with `Redirect:MaxRedirects = 3` and `Redirect:AllowedMethods = GET` follows the hop and **B's captured request carries none of the three credential headers** (`RedirectPolicy`, `REDIR-7`..`REDIR-9`), while a same-origin hop strips `Authorization` too.
- `A_substituted_redirect_following_primary_fails_the_call_not_the_origin`: the same substitution as task 3.2's, driven on the wire: first resolution throws and **neither origin is contacted** (the structural form of `TRANSPORT-1`).
- Every client here goes through `TestClients.Add` (a stub `UseSerde`, F1): without it `A_substituted_redirect_following_primary_fails_the_call_not_the_origin` would meet the missing-codec failure first, and it expects the `ArgumentException` naming `AllowAutoRedirect`.
- Each test uses `UseProxy`-free handlers (the package's primary has `UseProxy = false`, so `HTTP_PROXY` cannot reroute the loopback exchange); the file's header comment cites S3 and says it is permanent (roadmap constraint 5).

**Production change.** None (task 3.2 built the mechanism). The red is: with the `AllowAutoRedirect = false` assignment removed from `CreatePrimaryHandler` (mutation, never committed), `The_named_client_returns_the_3xx_and_never_contacts_the_target` fails because B is contacted and `Authorization` leaks; restore.

**Verify.** V-build; V-sec; the Security diff check of convention 5. **IDs.** `TRANSPORT-1`, `REDIR-7`, `REDIR-8`, `REDIR-9`, `XCUT-17`. **Commit.** `test: named-client redirect wire tests (S3, TRANSPORT-1)`.

## Task 3.9 — PR 3 gate

V-gate; V-conf (the two SystemNet drivers and the raw-socket driver still green: nothing here changes them); `rg "Environment\.GetEnvironmentVariable|ConfigurationBinder" src/Dexpace.Sdk.Extensions.DependencyInjection` empty; the public surface of the package equals the design's list exactly (`diff` of `PublicAPI.Unshipped.txt` against the design block; the design's "No overload has an optional parameter"). Hand the lead the branch name.

---

# PR 4 — Drivers and close-out

Branch `94-phase-9-close-out` off PR 3. Rows closed: the third driver's proof of `TRANSPORT-1`; `XCUT-10`'s composition test; the documents.

## Task 4.1 — Share the conformance hook handlers (R1; design "Packages, projects and dependencies")

**Files.** New: `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/Handlers/FaultingAdaptationHandler.cs`, `ExplodingContent.cs`, `ResendingHandler.cs`, `CancelOnDemandHandler.cs` (internal sealed, top-level, namespace `Dexpace.Sdk.Http.SystemNet.Tests.Conformance`, each with explicit `using System.Net.Http;` and header). Edit: `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs` (the four nested classes are removed; their uses are unchanged), `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/*.csproj` (`<Compile Include="..\Dexpace.Sdk.Http.SystemNet.Tests\Conformance\Handlers\*.cs" Link="Conformance\Handlers\%(Filename)%(Extension)" />`).

**Tests first.** A move plus a constructor rewrite: the pin is the SystemNet suite unedited and green (`SystemNetConformanceTests` both subjects, `The_whole_suite_is_green_and_its_report_is_written`, the two waivers). Red evidence: none by design; the first build after the move fails if the extraction missed a member (the compile error is the check).

**Production change.** The extraction, plus a rewrite of each of the three `DelegatingHandler` subclasses (`FaultingAdaptationHandler`, `ResendingHandler`, `CancelOnDemandHandler`) onto **two explicit constructors**, not merely a move and one more constructor: they declare primary constructors today (`(HttpMessageHandler inner) : DelegatingHandler(inner)`, `SystemNetSubject.cs:197, 225, 253`), a parameterless constructor on a primary-constructor class must chain through `this(...)` (CS8862), and `DelegatingHandler(null)` throws, so each becomes a class with a parameterless `: base()` constructor (which `IHttpClientFactory` needs: `AddHttpMessageHandler(() => new X())` leaves `InnerHandler` for the factory to assign) and an `(HttpMessageHandler inner) : base(inner)` constructor, which the SystemNet subject keeps using, so nothing it does changes. `CancelOnDemandHandler`'s `CancelInFlight` stays `internal`. Nothing moves into `Dexpace.Sdk.TestSupport` (the design's reason: `Dexpace.Sdk.Core.Tests`' SEAM-2 position does not change).

**Verify.** V-build; V-conf (SystemNet). **IDs.** none. **Commit.** `chore: share the conformance hook handlers by linked source`.

## Task 4.2 — The third conformance driver (`TRANSPORT-1`, `TRANSPORT-15`, `ASYNC-*`, `XCUT-22`; P9-22; V8)

**Files.** New: `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Conformance/DependencyInjectionSubject.cs`, `DependencyInjectionConformanceTests.cs`. Edit: `.github/workflows/ci.yml` (header comment only: the third driver), `docs/sdk-documentation/conformance.md` is touched in task 4.5.

**Tests first** (`DependencyInjectionConformanceTests`, `Conformance`; the same shape as `SystemNetConformanceTests`).

- `Assertion_holds` (theory over `TransportSuite.Assertions` × faces) and `The_whole_suite_is_green_and_its_report_is_written` against `DependencyInjectionSubject`, with **exactly the two permanent assertion-scoped waivers** (`TRANSPORT-14` / `transport-14.name-malformed-dropped`, §10 entry 32; `TRANSPORT-2` / `transport-2.bodyless-not-resent`, entry 33), each turning stale if its assertion passes (the kit's rule), copied by reference from `SystemNetSubject.Options` text (the same reasons) rather than shared (a different test assembly).
- `Only_the_two_recorded_permanent_decisions_remain` and `Only_known_waiver_targets_remain` (the SystemNet driver's two guards).
- `transport-1.redirect-not-followed` passes on **both faces** (the proof the 8a hand-off names); `transport-15.owned-released` passes (V8) and is **red without the lease** (mutation, never committed: comment out `lease.Track(handler)`; the assertion fails as V8 measured; restore); the `Skipped`/`NotExercised` rows are exactly `transport-15.borrowed-survives` with the reason in the driver's remarks (the package never borrows a client).

**Production change.** `DependencyInjectionSubject` builds a **fresh `ServiceCollection` per transport**, the kit's `TransportSettings.Logger` forwarded through a single-logger `ILoggerFactory`, `AddDexpaceClient("kit")` over an in-memory configuration and a `UseSerde` stub, resolves the keyed transport and hands the kit a wrapper (`IAsyncHttpClient`, `IHttpClient`-capable like `HandlerBackedTransport` if the face needs it: the keyed transport is a `SystemNetHttpClient`, so the wrapper forwards `Execute`/`ExecuteAsync`) whose disposal **disposes the provider**. Hooks: `CreateWithInternalCancel`, `CreateWithNativeResend`, `CreateWithFaultingAdaptation` through `builder.HttpClient.AddHttpMessageHandler(() => new …Handler())` with the linked handlers of task 4.1 (their parameterless constructors leave `InnerHandler` for the factory to assign); `CreateWithProxy` through the `Transport:Proxy:*` keys (exercising `CreatePrimaryHandler`'s proxy path); `CreateBorrowed` not supplied.
`AfterDispose = ThrowsObjectDisposedException` (disposal of the provider disposes the transport; the wrapper latches).

**Verify.** V-build; V-conf (the DI driver, both SystemNet drivers and the raw-socket driver all green, the kit green on both faces). **IDs.** `TRANSPORT-1`, `TRANSPORT-15`, `XCUT-22`, `SEAM-15` (after-dispose), the `ASYNC` rows the kit covers (evidence only). **Commit.** `test: conformance driver over the DI-built transport`.

## Task 4.3 — `ResilienceCompositionTests` and the test-only `Microsoft.Extensions.Http.Resilience` (`XCUT-10`; P9-19; V6; R5)

**Files.** New: `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/ResilienceCompositionTests.cs`. Edit: `Directory.Packages.props` (`Tests` group: `Microsoft.Extensions.Http.Resilience` 10.10.0 with a comment naming P9-19, V6 and "test-only: nothing here reaches a library's closure"), the DI test project csproj (one `PackageReference`), its `packages.lock.json`.

**Tests first** (`Integration`; a loopback server answering `503`, counted).

- `The_documented_composition_sends_once_per_SDK_attempt`: `AddDexpaceClient("kit", section).HttpClient.AddStandardResilienceHandler(o => o.Retry.ShouldHandle = _ => ValueTask.FromResult(false))` with `Retry:MaxRetryAttempts = 2` and a `POST` with a **replayable** body reaches the server exactly `MaxRetryAttempts + 1` = 3 times; a **single-use** body reaches it once (the SDK's re-send gate, behind the composition); the rate limiter, total and attempt timeouts and circuit breaker are still registered (the resilience options for the named pipeline resolve with their defaults for those strategies).
- `The_standard_handlers_default_arrangement_multiplies_the_attempts` (the regression the guidance exists for, V6): with `AddStandardResilienceHandler(o => o.Retry.Delay = TimeSpan.FromMilliseconds(1))` (the handler's backoff shortened: under its 2 s exponential default each SDK attempt costs about 14 s, which the SDK's own three attempts would multiply into a test of 40 s or more on the real clock, F23; confirm at implementation that 1 ms passes the handler's options validation, else use the smallest value it accepts) and the SDK's `Retry:MaxRetryAttempts = 0`, a `POST` reaches the server **4 times for that one SDK attempt** (the default retry re-sends the body) — the test asserts `> MaxRetryAttempts + 1` and comments why; it is the measured `XCUT-10`(b) hazard (§11 item 24).
- `MaxRetryAttempts_zero_is_refused_by_the_standard_handlers_own_validation` (V6): `AddStandardResilienceHandler(o => o.Retry.MaxRetryAttempts = 0)` throws `OptionsValidationException` at first handler creation, so the guidance's old wording cannot work (the pin behind the correction of 6a's note).
- Needs the factory handler to be **in the chain**: it composes underneath because the package builds the transport from `IHttpMessageHandlerFactory` (V3), which the first test asserts by counting through a spy.

**Production change.** None in `src/`; the guidance text lands in task 4.5. **If R5's lock check failed** (task 0.1 step 4): apply the pinned-transitive fallback in the `Tests` group; failing that, **drop this task's tests and package version** and write task 4.5's snippet as an explicitly untested example (the design's named alternative), recording the choice in the checklist.

**Verify.** `dotnet restore Dexpace.Sdk.sln` once, commit the one changed lock file, V-restore (locked); V-build; V-di `ResilienceCompositionTests`; `git diff --stat main...HEAD -- '*packages.lock.json'` lists the two new DI lock files of task 2.0 and no other (this task and task 2.8 change the test project's, already one of the two). Every client in these tests goes through `TestClients.Add` (a stub `UseSerde`). **IDs.** `XCUT-10` (the clause). **Commit.** `test: the documented resilience composition sends once per SDK attempt (XCUT-10)`.

## Task 4.4 — NativeAOT smoke: `CheckPhase9Async` (`NFR-8`, `NFR-9`; P9-21; V7)

**Files.** Edit: `tests/Dexpace.Sdk.AotSmoke/Dexpace.Sdk.AotSmoke.csproj` (a `ProjectReference` to the package; update the header comment: phase 9 exercises the DI package), `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (call `CheckPhase9Async` after `CheckPhase8bAsync` in `RunAllAsync`). New: `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.DependencyInjection.cs`.

**Tests first (the check is the test).** `CheckPhase9Async` (no reflection; shipped API only; every `Expect(...)` with a message): a client registered with a stub `UseSerde` (a client with no codec fails `IStartupValidator.Validate()` and every keyed resolution, design D, F1) over an in-memory `IConfiguration` with a shorthand duration (`Retry:BaseDelay = 250ms`), a level by name (`Logging:Level = Headers`), a method list (`Redirect:AllowedMethods = GET, HEAD`) and a status list (`Retry:RetryableStatusCodes = 429, 503`); a `ServiceProvider` built with `ValidateScopes` and `ValidateOnBuild`; `IStartupValidator.Validate()` passes; the keyed `HttpPipeline` and `IAsyncHttpClient` resolve; a loopback `302` (the kit's `LoopbackServer`, as `CheckPhase8aConformanceAsync` uses it) is **returned unchanged** by the keyed transport; a bad value (`Retry:MaxRetryAttempts = abc`) fails with `OptionsValidationException` and the message names the key and not the value. Red: the first publish after adding the reference before the check exists is the baseline; the new check is added and must pass.

**Verify.** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` exits 0 and prints no trim or AOT warning (they are errors). **IDs.** `NFR-8`, `NFR-9` (the package's share; phase 10 keeps the check). **Commit.** `test: AOT smoke for the DI package`.

## Task 4.5 — Documentation: `docs/sdk-documentation/dependency-injection.md`, the package README, the SystemNet README, `docs/README.md`

**Files.** New: `docs/sdk-documentation/dependency-injection.md`. Edit: `src/Dexpace.Sdk.Extensions.DependencyInjection/README.md`, `src/Dexpace.Sdk.Http.SystemNet/README.md`, `docs/README.md` (the `sdk-documentation/` row gains the page, "phase 9"), `docs/sdk-documentation/conformance.md` (a paragraph: the third driver and the lease's role in `transport-15.owned-released`), `docs/sdk-documentation/transport.md` (a `CreatePrimaryHandler` paragraph and the factory handler composition), `docs/sdk-documentation/retry.md` (lines 40–41, the dated correction of task 4.8 item 9).

**Content** (house style of `pagination.md`, `retry.md`, `transport.md`; read all three first; each section cites its rows and rulings):
(1) registration (`AddDexpaceClient`, the builder, keyed services, the generated-SDK pattern with `[FromKeyedServices]`, why there is no generic overload, one name registers once; the section parameter is never the root; dispose the provider with `DisposeAsync`, since a synchronous dispose throws for an async-only transport); (2) configuration: the key table verbatim from the design, the tiers (`CFG-1`, the `Configure` transform), the grammars with examples (`CFG-5`–`CFG-7`, enum by name, lists with replace semantics, `none`), empty-is-absent and what it means for shadowed providers (`CFG-2`, §10 entry 25; **stated explicitly: an empty environment value silently shadows a lower provider's value and the record default applies**, P9-7), environment-variable spelling (`Dexpace__Retry__MaxRetryAttempts`, `CFG-3`), unknown keys fail, **no configured string, URL or credential is ever quoted** (a numeric accessor message may echo the number, P9-28), **no reload** (the snapshot is taken at first resolution); (3) validation and timing (`ValidateOnStart`, host-less `IStartupValidator`, the failure format); (4) the named client (the 8b handler construction, `CreatePrimaryHandler`, `RemoveAllLoggers`, infinite handler lifetime with `PooledConnectionLifetime`, the lease, `ConfigureHttpClient` is ignored, the substituted-primary refusal); (5) proxy (explicit keys or `UseProxyFromEnvironment`, never a configuration switch, `CFG-28`); (6) auth and codec (`UseAuth`, `UseAuthPolicy`, `ConfigureCredentials`; `UseSerde` is required; the STJ composition with `CreateDefaultOptions(context)`, P9-17); (7) **one retry layer: the `AddStandardResilienceHandler` composition with `Retry.ShouldHandle` returning `false`, why the handler-side `o.Retry.MaxRetryAttempts = 0` does not work (V6), why the SDK-side `MaxRetryAttempts = 0` option is withdrawn (the handler would still re-send bodies behind the SDK's gate, `XCUT-10`) and what the default handler does to a `POST` body (§11 item 24)** — this refines 6a's wording; (8) cross-property combinations the records accept (`BaseDelay > MaxDelay` caps, `AttemptTimeout` above `OverallTimeout` never fires, a `Transport:Timeout` below `AttemptTimeout` bounds the attempt at the transport; P9-10); (9) the single-registration rules and the install hint (`SEAM-5`, `SEAM-6`, `SEAM-7`); (10) what ships and what does not (the `CallKey` reader, P9-20).
The package README: registration, the key table, the grammars in one table, the resilience composition, proxy, auth, the no-reload rule, a link to the page. The SystemNet README: one row for `CreatePrimaryHandler` and the factory-chain sentence. Every code sample compiles in spirit against the shipped signatures (the lead may ask for a `docs/` snippet test; none is planned).

**Verify.** `dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations`; `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` (the README is at the package root); the audit (task 1.2) once more. **IDs.** all of the owned rows (user-facing statement). **Commit.** `docs: dependency-injection.md and the package READMEs`.

## Task 4.6 — The checklist

Write `docs/work/mvp/phase9/2026-10-12-phase9-host-integration-checklist.md` (drafted where `docs/superpowers/` rules say, then filed by task 4.9) from what was built, following `docs/work/mvp/phase8/phase8b/2026-10-09-phase8b-reference-transport-checklist.md`'s shape: header (branches, issue #94, the four PRs), the legend, **red evidence** (the first compile of each new test class; the pins proven able to fail by mutation: `NamedClientRedirectWireTests` with `AllowAutoRedirect = false` removed, `PrimaryHandlerLeaseTests` and `transport-15.owned-released` with `lease.Track` removed, the `ConfigurationBinder` ban, the audit's three negative controls, `NoImplicitEnvironmentReadArchitectureTests`, `KeyTableTests`' planted member), the **15-row requirement table** (one row per ID with the test class, `[Trait]` and file, statuses from the [checklist section](#checklist-one-row-per-owned-id) corrected to what was observed), a **second table** of the three contributed rows (`SEAM-7`, `TRANSPORT-1`, `XCUT-10`) and **every non-row hand-off** of the design's table, the `Security`-class statement (one added, none edited), the deviations from the plan, and the totals. State: `CFG-2`, `-5`, `-6`, `-7`, `OBS-35` ✅ with their 🚫 clauses on §10 entries 25 and 26; `CFG-4`, `CFG-10` N/A; the kit green on the DI driver, both SystemNet drivers and the raw-socket driver with exactly the two permanent waivers per SystemNet-family driver. Include the styleguide audit groups the design names for `01-host-and-configuration.md` and `02-dependency-injection.md`, the 1.3 departure (P9-4) as a recorded conflict, and the 2.7 builder-return-type departure (`AddDexpaceClient` returns a `DexpaceClientBuilder`, not the collection). `CFG-6`'s ✅ clause reads "after an ASCII trim". **Commit.** `docs: phase 9 checklist`.

## Task 4.7 — `CHANGELOG.md`

Under `[Unreleased]`, in the file's existing structure (read how 6c's and 8b's entries are laid out and match it): an `### Added` block headed `CFG-1, -2, -3, -4, -5, -6, -7, -10, -11, -14, -37, -38, SEAM-5, SEAM-6, OBS-35` listing the package (`AddDexpaceClient`, the builder, `DexpaceConfigurationKeys`, the reader and its grammars, validation, the named client, proxy and auth, the single-registration check), `SystemNetHttpClient.CreatePrimaryHandler`, the `ConfigurationBinder` ban, the audit's policy record, the third conformance driver and `NamedClientRedirectWireTests`; and one `### Changed` line: the guidance for composing `AddStandardResilienceHandler` is `Retry.ShouldHandle` returning `false`; the SDK-side `MaxRetryAttempts = 0` option is withdrawn because the handler would still re-send bodies behind the SDK's gate (`XCUT-10`; documentation only; V6). Re-derive the hunk from the merged file if another phase landed first. **Commit.** `docs: changelog for phase 9`.

## Task 4.8 — Design, roadmap, register, checklists, `CLAUDE.md` and knowledge corrections ("Corrections owed at close-out")

Each a dated correction (2026-10-12 or the merge date) naming its ruling; never rewrite an earlier verdict, append. In the design's own order:

1. **Design (`docs/sdk-design-dotnet/`)**: §2.1 (the package is built: core, `Dexpace.Sdk.Http.SystemNet`, `Microsoft.Extensions.Http` the family's one root, V5; **As built**); §3.2's `TRANSPORT-1` paragraph (the named client's primary through `CreatePrimaryHandler`, one chain per client, infinite lifetime plus pooled-connection lifetime); §3.6 **As built** (`SEAM-6`'s different-provider failure at the `Use*` call, the validator scans keyed descriptors, `SEAM-7` is the keyed singleton); §6.1 "The layer underneath" (the guidance is `Retry.ShouldHandle` returning `false`; the SDK-side `MaxRetryAttempts = 0` option is withdrawn because the handler would re-send a `POST` body behind the SDK's gate, `XCUT-10`; the handler-side `MaxRetryAttempts = 0` is refused by the handler's validation: V6 replacing "cited from its documentation, not verified here"); §8.2 (binding is the package's reader, V1, V2's four `XmlConvert` divergences, `CFG-2` reads empty as absent, tier 1 is a `with` transform, no reload, the proxy sources, `OBS-35`'s key is the caller's section); §9.1 (the banned list gains `ConfigurationBinder`); §9.2 (the audit's policy record; the AOT smoke covers the DI package); §9.3 (the third conformance driver); §10 entry 2 (measured closure and the SystemNet reference), entry 9 (**As built**), entry 25 (`CFG-2`: an empty value is absent and the record default applies, the shadowed lower provider is not consulted, replacing "fails binding at startup"), entry 26 (touches `OBS-35`; the binder evidence); §11 item 8 (measured, V4), item 24 (the corrected composition), item 30 (the reader is unbuilt, P9-20), **two new items** (re-read the last number on the merged file first): P9-29's grammar readings (ISO-8601 is the day-time subset, a bare number is digits only, scalars ASCII-trimmed first, booleans included) and P9-7's empty-value shadow; the **styleguide** 2.7 departure goes to the checklist and a knowledge note; §12's `CFG`, `SEAM` and `OBS` rows. **No new §10 entry** (P9-9; re-read §10's current last number on the merged file first, as 8b did).
2. **Roadmap** (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`): the Phase List row 9 links the design, plan and checklist; the 6a status note's and issue #94's ambiguous "`MaxRetryAttempts = 0`" wording clarified to the `ShouldHandle` composition with the SDK-side option withdrawn (the Phase 9 card itself says only "without its retry", which stands); the card's two build-list departures recorded as dated corrections ("`IConfiguration` binding under section `Dexpace`": the section is the caller's, P9-23; "the TimeSpan converter … and the base-10 integer converter": the reader, P9-4); an append-only dated **Phase Status Note** ("9 host integration built: 15 rows, 13 ✅ (5 split) / 2 N/A / 0 ⏳; the reader replaces the binder; the lease; the `ShouldHandle` correction; rulings open for the lead taken by default: P9-1, 2, 4, 5, 7, 9, 10, 12, 15, 17, 19, 20, 28, 29"); the phase-1 hand-off table's row 9 names `NamedClientRedirectWireTests`.
3. **Prior checklists** (dated corrections): 2b `SEAM-5`, `SEAM-6` (DI half ✅), `SEAM-7` (cites `KeyedSingletonTests`); 5b `OBS-35` (✅, V4 answered); 8a (the third driver); 8b `TRANSPORT-1` (the named-client clause) and P8b-18's disposition; phase 1 S3 (row 9's ⏳ closed).
4. **`docs/first-release.md`**: *Packages*: the DI row (exists, unpublished, phase 12); *Behavioural asymmetries*: `di-no-reload`, `di-handler-lifetime` (one chain per client for the process, `PooledConnectionLifetime` instead of rotation, `ConfigureHttpClient` ignored, the factory's loggers removed) and `di-empty-shadows` (P9-7: an empty environment value shadows a lower provider's value and the record default applies); *Post-release triggers*: the `CallKey` reader (P9-20) and, on P9-5, a typed-client overload on the first generated SDK that asks for one.
5. **`CLAUDE.md`**: the layout block gains the package and its test project with one line each; "What is genuinely unbuilt" loses the DI package and gains a pointer to `docs/sdk-documentation/dependency-injection.md` (keep other phases' words); "Things that will bite you" gains **one bullet** (configuration is read by the package, never by the binder; it fails at start naming the key and never the value; no reload; a codec is required; the resilience composition is `ShouldHandle`, not `MaxRetryAttempts = 0`); the commands block gains the third driver's filter (`dotnet test --project tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests --configuration Release --filter-trait "Category=Conformance"`); the repository-layout `src/` and `tests/` trees and the `scripts/ci` description of the audit ("policy record") are updated. Root `README.md` lists the package.
6. **`docs/README.md`**: done in task 4.5.
7. **READMEs**: done in task 4.5.
8. **Knowledge**: append to `docs/knowledge/notes/configuration.md` (create with the front matter other notes use if absent; `scripts/knowledge --origin note --brief` shows the shape): V1, V2, **overriding `configuration/bfbcbf8c`** (an empty value is absent, not a `ValidateOnStart` failure: the binder maps it to `null`) **and `observability/4997350a`** (the binder accepts numeric and undefined levels, so the satisfaction of `OBS-35` is the reader's), and the **styleguide 1.3 departure (P9-4) as a `Conflicts` note** (constraint 6; read how existing conflict notes are written first); append V3, V8, V9 to `docs/knowledge/notes/transport-adapter.md`. Run `scripts/knowledge verify-structure`.

9. **`docs/sdk-documentation/retry.md`** lines 40–41 ("turn its retries off or set `MaxRetryAttempts = 0` here. The DI package's guidance (phase 9) says the same"): a dated correction (the file's own style: an italic *Dated correction* line): the SDK-side option is withdrawn (a handler retry would re-send bodies behind the SDK's gate, `XCUT-10`), the composition is `Retry.ShouldHandle` returning `false`, and the page points to `dependency-injection.md`. Applied with task 4.5's page; the corrections commit checks it.

**Commit.** `docs: phase 9 corrections, roadmap note and CLAUDE.md`.

## Task 4.9 — File the phase documents, run the probe, final gate

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 9            # dry run: the design, plan and checklist already sit in docs/work/mvp/phase9/
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 9 --write
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
scripts/knowledge verify-structure
```

Fix what the last probe reports (misattributed IDs, broken links, stale counts in `CLAUDE.md`/`README.md`). Then the whole CI list from `CLAUDE.md`, locally, in order:

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode && dotnet restore tools/Dexpace.Tools.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"          # and the SystemNet and DI projects
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Conformance"   # and the DI project
dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80
scripts/ci/coverage-gate-selftest.sh artifacts/test-results
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet build tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
```

Exit check list: the DI coverage report is present by project-name prefix (the gate fails closed on a missing one); `git diff --stat main...HEAD -- src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt` empty; the Security diff check of convention 5; no `Owner =` waiver anywhere in the three drivers; `*packages.lock.json` diff is exactly the two new DI lock files (the test project's changes in 2.8 and 4.3 land in one of them) and nothing else; `Directory.Packages.props` diff is the three package lines (`Microsoft.Extensions.Http`, `Microsoft.Extensions.Configuration.EnvironmentVariables`, `Microsoft.Extensions.Http.Resilience`). **No push and no PR:** those are the lead's actions (global rule); the PR descriptions name the checklist and carry `Closes #94` on PR 4 only, and carry no attribution.

---

## Checklist: one row per owned ID

Exactly 15 rows. **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 4.6 records what was built. Levels: 13 MUST, 2 SHOULD (`CFG-14`, `OBS-35`).

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `CFG-1` | MUST | 2, 3 | 2.3, 2.6, 3.1 | ✅ (mechanism per §10 entry 25) | `ConfigurationPrecedenceTests` (`Unit`): three providers plus the `Configure` transform, removing each from the top |
| `CFG-2` | MUST | 2 | 2.3 | **Split**: ✅ empty is absent; 🚫 fall-through (§10 entry 25) | `EmptyValueTests` (`Unit`) |
| `CFG-3` | MUST | 2 | 2.8 | ✅ (normalisation per entry 25) | `EnvironmentNormalizationTests` (`Integration`) |
| `CFG-4` | MUST | 2 | 2.8 | N/A (vacuous) | `EnvironmentNormalizationTests` casing rows |
| `CFG-5` | MUST | 2 | 2.2, 2.4 | **Split**: ✅ base-10; 🚫 never-throw (entry 26) | `IntegerGrammarTests` (`Unit`), `RecordBindingTests` |
| `CFG-6` | MUST | 2 | 2.2 | **Split**: ✅ only `true`/`false`; 🚫 fall-back (entry 26) | `BooleanGrammarTests` (`Unit`) |
| `CFG-7` | MUST | 2 | 2.1, 2.4 | **Split**: ✅ the grammar; 🚫 return-the-default (entry 26) | `DurationGrammarTests` (`Unit`) |
| `CFG-10` | MUST | 2 | 2.6 | N/A (vacuous) | `ConfigurationPrecedenceTests.Precedence_has_no_removal_operation` |
| `CFG-11` | MUST | 2, 3 | 2.8, 3.6 | ✅ | `ConfigurationSeamTests` (`Unit`), `EnvironmentNormalizationTests` |
| `CFG-14` | SHOULD | 2, 3 | 2.9, 3.6 | ✅ | `KeyConstantsTests` (`Unit`) incl. the IL "published, not read" clause |
| `CFG-37` | MUST | 3 | 3.1, 3.3..3.6 | ✅ | `NullGuardTests` (`Unit`) over the whole public surface |
| `CFG-38` | MUST | 2 | 2.3, 2.4, 2.7 | ✅ | `TypedLookupTests` (`Unit`), `KeyTableTests` |
| `SEAM-5` | MUST | 3 | 3.3, 3.7 | DI half ✅; DI-less half N/A (2b) | `SingleRegistrationTests` (`Unit`), `ProviderValidationTests` |
| `SEAM-6` | MUST | 3 | 3.1, 3.3, 3.5 | DI half ✅; DI-less half N/A (2b) | `SingleRegistrationTests`, `AddDexpaceClientTests` |
| `OBS-35` | SHOULD | 2 | 2.2, 2.4 | **Split**: ✅ layered, by name, no baked key; 🚫 fall-back (entry 26) | `LogLevelBindingTests` (`Unit`) |

Count: **15 rows**, all mapped (13 MUST, 2 SHOULD). By exit: 13 ✅ (`CFG-1`, `-2`, `-3`, `-5`, `-6`, `-7`, `-11`, `-14`, `-37`, `-38`, `SEAM-5`, `SEAM-6` (✅ for their DI half), `OBS-35`; of which 5 split: `CFG-2`, `-5`, `-6`, `-7`, `OBS-35`), 2 N/A (`CFG-4`, `CFG-10`), 0 🚫, 0 ⏳. (The design's tally line said "12 ✅"; its own table has 13 ✅ rows, and the validation resolution corrected the line to 13, which the checklist records.)

### Work on rows owned elsewhere (no phase 9 checklist row) and the non-row hand-offs

| ID / hand-off (owner) | Work | Task | Evidence |
|---|---|---|---|
| `SEAM-7` (2b) | each client's transport, codec and pipeline are keyed singletons created once | 3.3 | `KeyedSingletonTests`; dated correction in 2b's checklist |
| `TRANSPORT-1` (8b) | the named client's primary is redirect-off; a substituted primary is refused | 1.1, 3.2, 3.8, 4.2 | `PrimaryHandlerTests`, `NamedClientTests`, `NamedClientRedirectWireTests` (`Security`), `transport-1.redirect-not-followed` on the third driver |
| `XCUT-10` (10) | the package registers no retrying handler; the documented composition sends once per SDK attempt | 3.2, 4.3 | `NoRetryingHandlerTests`, `ResilienceCompositionTests` |
| 5a: bind the records | taken by a hand-written reader | 2.1–2.7 | the reader's tests |
| 5a (P5a-4): `IValidateOptions` | the reader's failures plus wiring checks in one validator | 2.6, 3.3–3.6 | `ValidationTests` |
| 5a (P5a-16), 8b (P8b-4): `FromEnvironment` | code-only `UseProxyFromEnvironment` | 3.6 | `ProxyRegistrationTests`, the IL scan |
| 5b (P5b-16): V4 | answered by V1 | 2.2, 2.4 | `ScalarGrammarTests`, `LogLevelBindingTests` |
| 6a: `RetryableStatusCodes` | a list key | 2.4 | `RecordBindingTests` |
| 6a, #94, §6.1, §11 item 24: the resilience guidance | refined (SDK-side option withdrawn; the `ShouldHandle` spelling) | 4.3, 4.5, 4.8 | `ResilienceCompositionTests`, the documentation page |
| 6b: `AllowedMethods`, `Predicate` | `Method.Of`; code-only | 2.4 | `RecordBindingTests` |
| 6c: auth registration | `Auth:*`, `UseAuth`, `UseAuthPolicy`, `ConfigureCredentials` | 2.5, 3.5 | `AuthKeyTests`, `AuthRegistrationTests`, `CredentialRedactionTests` |
| 7a: `ISerde` from `CreateDefaultOptions` | composition, not a reference | 3.7 | `SerdeRegistrationTests` |
| 7c: nothing | confirmed | none | (design) |
| 8a: the third driver | built | 4.1, 4.2 | `DependencyInjectionConformanceTests` |
| 8b: handler constructor as the seam; `CreatePrimaryHandler` | built | 1.1, 3.2 | `PrimaryHandlerTests` |
| 8b: `SystemNetHttpClientOptions` as the binding target | `Transport:*` keys | 2.5 | `TransportBindingTests` |
| 8b (P8b-18), 4a (P4a-13): `CallKey` stamp | not built (P9-20) | 4.8 | `docs/first-release.md` trigger; phase 11 bullet |
| Phase 1 (S3) | `NamedClientRedirectWireTests` | 3.8 | the class |
| 2b: `SEAM-7`'s singleton lifetime | cited | 3.3 | `KeyedSingletonTests` |

---

## Traceability: requirement ID to tasks

| ID | Tasks |
|---|---|
| `CFG-1` | 2.3, 2.6, 3.1 |
| `CFG-2` | 2.3 |
| `CFG-3` | 2.8 |
| `CFG-4` | 2.8 |
| `CFG-5` | 2.2, 2.4, 2.5 |
| `CFG-6` | 2.2, 2.4, 2.5 |
| `CFG-7` | 2.1, 2.4, 2.5 |
| `CFG-8` (preserved) | 2.6, 3.4 |
| `CFG-10` | 2.6 |
| `CFG-11` | 2.8, 3.6 |
| `CFG-14` | 2.9, 3.6 |
| `CFG-28` (strict reading kept) | 1.1, 2.5, 3.6 |
| `CFG-37` | 3.1, 3.3, 3.4, 3.5, 3.6 |
| `CFG-38` | 2.3, 2.4, 2.7 |
| `SEAM-5` | 3.3, 3.7 |
| `SEAM-6` | 3.1, 3.3, 3.5 |
| `SEAM-7` | 3.3 |
| `OBS-35` | 2.2, 2.4, 2.9, 3.6 |
| `TRANSPORT-1` | 1.1, 3.2, 3.8, 4.2 |
| `TRANSPORT-15`, `TRANSPORT-30` | 1.1, 3.2, 4.2 |
| `XCUT-10` | 3.2, 4.3, 4.5 |
| `XCUT-18`, `XCUT-19` (evidence) | 2.3, 2.5, 3.5 |
| `XCUT-22`, `XCUT-23` (evidence) | 3.2, 3.3, 4.2 |
| `NFR-2` | 1.2, 2.0 |
| `NFR-8`, `NFR-9` (evidence) | 2.0, 3.7, 4.4 |
| `PIPE-27`, `PIPE-35`, `REDIR-7`..`REDIR-9`, `XCUT-17` (evidence) | 3.4, 3.8 |

| PR | Tasks | Rows (first owner) |
|---|---|---|
| 1 — seams and gates | 1.1–1.3 | `TRANSPORT-1` (seam), `NFR-2` (the audit's policy record) |
| 2 — the package and the reader | 2.0–2.10 | `CFG-1`..`CFG-7`, `CFG-10`, `CFG-14` (constants), `CFG-38`, `OBS-35`; `CFG-11` (production default); `NFR-2` (scaffold) |
| 3 — registration | 3.1–3.9 | `CFG-11`, `CFG-37`, `SEAM-5`, `SEAM-6`; `SEAM-7`, `TRANSPORT-1`, `XCUT-10` evidence |
| 4 — drivers and close-out | 4.1–4.9 | third driver, `ResilienceCompositionTests`, AOT smoke, documents, checklist, corrections |

Task count: 0.1 (pre-flight) + 3 (PR 1: 1.1 to 1.3) + 12 (PR 2: 2.0, 2.0a, 2.1 to 2.10) + 9 (PR 3) + 9 (PR 4) = **34 tasks**.

## Exit criteria (the design's list, mapped to tasks)

1. The checklist has 15 rows as tabled, each with a named test, `[Trait]` and file; a second table records the three contributed rows and every non-row hand-off; `SEAM-5`, `SEAM-6`, `SEAM-7`, `OBS-35`, `TRANSPORT-1` and phase 1's S3 flip in their own checklists by dated correction: tasks 4.6, 4.8.
2. The kit is green against the DI-built transport on both faces with exactly the two permanent assertion-scoped waivers; the two SystemNet drivers and the raw-socket driver stay green: tasks 4.1, 4.2.
3. The package is packable with its README; the audit passes with the policy record; the AOT smoke publishes and runs `CheckPhase9Async`: tasks 1.2, 2.0, 4.4, 4.5.
4. Every gate green (build with `RS0016`/`RS0017` for the two changed `PublicAPI` files, `RS0030` with the new ban, `MA0051`, `CA2007`; format; full tests with the phase-1 `Security` classes unedited; the 80% coverage gate; reproducible pack; `verify-structure`): tasks 1.3, 2.10, 3.9, 4.9.
5. `dependency-injection.md`, the READMEs, `CHANGELOG.md`, the dated roadmap status note, the corrections, the knowledge notes, and a housekeeping probe that reports nothing: tasks 4.5, 4.7, 4.8, 4.9.

## Open items carried to the lead (none blocks execution; each task implements the design's chosen option)

- **The open rulings of the design** are all taken at their recommended defaults: P9-1, P9-2, P9-4, P9-5, P9-7 (empty-value shadow), P9-9 (unknown keys), P9-10, P9-12, P9-15, P9-17, P9-19, P9-20, and the two raised by validation, P9-28 (numeric echo) and P9-29 (grammar readings). [Ruling dependencies](#ruling-dependencies) says which tasks each reversal touches.
- **Plan-level additions flagged for the lead:** R1 (the four hook handlers are extracted to files, and gain parameterless constructors, in task 4.1/4.2: a SystemNet-suite edit the design's "linked source" wording does not mention), R2 (seven records, not six), R5 (`Microsoft.Extensions.Http.Resilience` added in task 4.3 and the unmeasured `NU1109` risk with the transitive pins), R8 (a bare fractional duration fails).
- **Findings while planning.** (a) The design's third-driver text says the handlers are "shared by linked source", but they are private nested classes (R1). (b) The design says "the six records"; the census is seven records plus three credential classes (R2; the design now says seven). (c) `SystemNetHttpClient`'s handler construction is always marked re-send-capable (`ResendCapable` is true for every construction but a proxy-less owned one), so a DI-built transport carries the 8b capture even with no proxy; the capture is allocated only when a body is re-sent, so no task changes, but `dependency-injection.md` should not promise "no capture overhead". (d) The design's tally ("12 ✅ ... 2 N/A") summed to 14 against 15 rows; the design now says 13 ✅, 2 N/A, and task 4.6 records that. (g) PR 1 cannot scaffold the package: the CI coverage gate fails closed on a `src/` library with no instrumented line, so the scaffold moved to PR 2 and the audit's policy change stays in PR 1. (h) `AddDexpaceClient`'s configuration parameter is an `IConfigurationSection`, not an `IConfiguration`, so a root cannot be passed. (e) `Microsoft.Extensions.Http.Resilience` 10.10.0 is on a different version line from the 10.0.x band; its compatibility with `CentralPackageTransitivePinningEnabled` was spiked in a scratch project, not in this repository (R5). (f) The design ties `CFG-37` to "every public method"; `NullGuardTests` is written to fail on a public parameter type it has no example for, so the guarantee holds for later additions.
