# Phase 10 — Cross-Cutting Invariants and Conformance: Implementation Plan

**Status:** Draft, for review. Written 2026-10-13 against `main` at `af603d2` (phases 0 to 9 merged), on branch
`99-phase-10-planning`, for issue [#99](https://github.com/dexpace/dotnet-sdk/issues/99). Design:
[phase 10 design](2026-10-13-phase10-cross-cutting-invariants-design.md), the authority for every decision below. The plan
cites its rows (`XCUT-n`, `NFR-n`), facts (`V1`..`V16`) and rulings (`P10-1`..`P10-30`) rather than restating them. **Every open ruling is
taken at its recommended default**; [Ruling dependencies](#ruling-dependencies) says what a reversal touches. Scope authority: the
roadmap's Phase 10 card and Phase List row 10 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). Format precedent: the phase 9
plan (`docs/work/mvp/phase9/2026-10-12-phase9-host-integration.md`).

**What this document is.** Roadmap step 3 for phase 10 (one phase, no sub-phases, P10-1): numbered TDD tasks in the design's four stacked
pull requests (P10-29), each with its failing test (name, `[Trait]`), the code to add, the `PublicAPI.Unshipped.txt` diff, the exact files,
the requirement IDs and the command that proves it green; a close-out group follows. It is not the checklist (step 4, written from what was
built in task 4.6) and carries no production code beyond the load-bearing sketches a task quotes. The [row traceability](#traceability-every-row-and-every-appendix-b-item-to-tasks)
maps all 41 rows (`XCUT-1`..`XCUT-24`, `NFR-1`..`NFR-17`) and all 61 appendix-B items to tasks.

**Scope.** 41 owned rows: 26 MUST and 15 SHOULD (`XCUT` 22 MUST + 2 SHOULD, `NFR` 4 MUST + 13 SHOULD). Planned exits (design tally): 39 ✅
of which 10 split, `NFR-16` ⏳ (phase 12), `NFR-17` ✅ conditional on the ruleset change, 0 🚫 rows, 0 N/A rows. **Phase 10 measures and routes; it changes no
production behaviour** (P10-2): the only edits under `src/` are `src/Dexpace.Sdk.Conformance` (the kit, the card's exception) and two `BannedSymbols.txt` bans (eight lines: `GetBaseException` and
`ConcurrentDictionary`'s seven public constructors) with the one scoped `#pragma` the constructor ban needs at `BoundedMap.cs`'s single construction site (P10-18, R7; a no-behaviour gate wiring).
The 2026-10-13 validation resolution (design, last section) amended this plan in 13 places (F1 to F13); the readings below carry the changes.

**Order and gates.** Four stacked pull requests on branches `99-phase-10-<slug>` (the first off `main`, each later one off its predecessor), each green on its own.
**PR 1** (tasks 1.1 to 1.13): the kit contract and the invariant suite. **PR 2** (2.1 to 2.6): the codec suite. **PR 3** (3.1 to 3.12): the packaging suite and the NFR gates.
**PR 4** (4.1 to 4.10): appendix B, coverage and close-out. Task 0.1 is a pre-flight with no commit.

---

## Conventions that hold for every task

1. **TDD, in this order.** For every new assertion the **negative control is written first** and must fail against the not-yet-written assertion (a compile error is the
   expected red for a new type); then the assertion; then the first-party driver green; then the group's wider gate. A test that passes before the change is a **pin** and says so;
   a pin is proven able to fail by temporarily breaking the thing it pins (never committed; `git checkout -- <file>`). **No commit is red:** a task that changes a signature edits every
   caller in the same commit. The coverage gate is per PR: the kit's new code is exercised by its controls in the same PR (design, PR segmentation).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), relies on its project's curated `GlobalUsings.cs` only (`ImplicitUsings` off), one type per file. The three
   `scripts/ci/*.cs` file-based apps carry the header too (they sit outside the solution; `LicenseHeaderTests`, task 3.8, closes that blind spot).
3. **`src/Dexpace.Sdk.Conformance` code:** `ConfigureAwait(false)` on every `await` including `await using` (`CA2007`); methods at most 70 lines (`MA0051`); `///` docs on every
   public member (CS1591); **no `Task.Delay`, no wall clock, no `Thread.Sleep`** (`BannedSymbols.txt`, and the kit's own 8a rule: time-bound assertions use a recording
   `TimeProvider` the kit implements over `TimeProvider.System`); no reflection (the kit stays `IsTrimmable`/`IsAotCompatible`; `System.Reflection.Metadata` over bytes is the one exception, trim-safe);
   references core and the BCL only (`KitDependencyArchitectureTests`); assertion names match `^[a-z]+-\d+\.[a-z0-9-]+$`, are stable public identifiers, and cite requirement IDs through `RequirementIndex`.
4. **Tests.** `[Trait("Category", …)]` on every class (`TestCategoryTests` enforces per suite); tokens from `TestContext.Current.CancellationToken`; xUnit `Assert` only; no test sleeps on the wall clock.
   Negative controls live under `tests/Dexpace.Sdk.Conformance.Tests/Controls/` in a public class named `*ControlTests` exposing `public static IReadOnlyCollection<string> Covered` (the
   existing discovery shape of `ControlClasses`, which `AssertionCatalogueTests` reads).
5. **Security tests are never deleted or loosened.** Phase 10 edits **no** existing `Security` class and adds none. The V-gate's diff check is
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Core.Tests/Security tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Security` **empty**.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` stays empty; every kit surface change is an `Unshipped` line, sorted ordinally, the build's `RS0016`/`RS0017` output the authority (apply
   the analyzer's code fix, then compare to the design's "The public surface"). **Only `src/Dexpace.Sdk.Conformance/PublicAPI.Unshipped.txt` moves.** Core's, SystemNet's, STJ's and the DI package's must not
   (V-gate checks). No overload has an optional parameter beyond what the design's signatures already show (`RS0026`/`RS0027`: where the design's sketch has `SuiteOptions? options = null`, the
   existing `TransportSuite` shape is followed and the analyzer's output wins).
7. **Breaking change.** None to a consumer library. The kit gains `TransportFace.None` (additive enum member): a `CHANGELOG.md` `[Unreleased]` line, task 4.7.
8. **Commits** follow the repository style (`feat:`, `test:`, `docs:`, `chore:`, `ci:`). **No AI attribution** anywhere (global hard rule). **The plan authorises no `git push`, no `gh` write and no
   remote action**; the implementer stops before any push and hands the lead the branch names. PR descriptions name the checklist and carry `Closes #99` on PR 4 only.
9. **Shared files.** `CHANGELOG.md`, `CLAUDE.md`, the roadmap status notes, `docs/first-release.md`, `.github/workflows/ci.yml`, `tests/Dexpace.Sdk.AotSmoke/*` and `.editorconfig` are shared with other
   phases: re-derive each hunk from the merged file; never resolve a conflict by keeping either side whole.
10. **A mutation is not a red.** Where a task says "prove the pin can fail", break the production line, see the named test fail, restore.

### Environment and command shorthands

`dotnet` 10.0.401 (`global.json`), `net10.0`; run from the repository root. On this machine no SDK is on `PATH`: `export DOTNET_ROOT=<sdk dir>` then, on a separate line, `export PATH="$DOTNET_ROOT:$PATH"`,
before any `dotnet` or `scripts/knowledge` command. The test runner is Microsoft.Testing.Platform: build first, then `dotnet test … --no-build`, **never pass MSBuild flags to `dotnet test`**;
a run reporting zero tests is a failed command.

- **V-build** = `dotnet build Dexpace.Sdk.sln --configuration Release -m:3 -nr:false --disable-build-servers`
- **V-conf-tests `<Class>`** = `dotnet test --project tests/Dexpace.Sdk.Conformance.Tests --configuration Release --no-build --filter-class "*<Class>"` (after V-build); **V-core `<Class>`**, **V-stj `<Class>`**, **V-di `<Class>`** the same against `tests/Dexpace.Sdk.Core.Tests`, `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests`, `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests`
- **V-sec** = `dotnet test --project <Core.Tests, Http.SystemNet.Tests, Extensions.DependencyInjection.Tests> --configuration Release --no-build --filter-trait "Category=Security"`
- **V-conf** = `dotnet test --project <P> --configuration Release --no-build --filter-trait "Category=Conformance"` for each project `<P>` that **has a `Conformance`-trait class at that point in the PR order**: `tests/Dexpace.Sdk.Http.SystemNet.Tests`, `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests` and `tests/Dexpace.Sdk.Conformance.Tests` from task 0.1 on (each has one today), **`tests/Dexpace.Sdk.Core.Tests` from task 1.10** (its driver is the first Conformance class there) and **`tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests` from task 2.5**. Microsoft.Testing.Platform exits 8 on a run that discovers zero tests, so a project joins the list only in the task that adds its first `Conformance` class; every task's Verify line says "V-conf" and means the list valid at that task. The transport drivers must stay green with exactly their two permanent waivers.
- **V-fmt** = `dotnet format Dexpace.Sdk.sln --verify-no-changes`
- **V-restore** = `dotnet restore Dexpace.Sdk.sln --locked-mode` and `dotnet restore tools/Dexpace.Tools.sln --locked-mode`
- **V-gate** = V-restore, V-build, V-fmt, `dotnet test --solution Dexpace.Sdk.sln --configuration Release --no-build` (V-sec first), the Security diff check of convention 5, the `PublicAPI` diff check of
  convention 6 (`git diff --stat main...HEAD -- 'src/*/PublicAPI.Unshipped.txt'` lists only the kit's), the coverage gate
  (`dotnet test --solution Dexpace.Sdk.sln --configuration Release --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`, then
  `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and `scripts/ci/coverage-gate-selftest.sh artifacts/test-results`), the dependency audit
  (`dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` then `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages`), and the closing commands of the group's last task

---

## Plan-level readings (where the design was silent, or the tree differs)

The design's rulings are not changed. **R1 to R9 correct or sharpen the design against the tree; R10 to R17 are additions or interpretations.** Items marked **flag** are for the lead.

- **R1 — The assertion body is typed to the transport's `SuiteContext` (design P10-7 says "extracted to an internal runner generic over the suite's context").** `ConformanceAssertion`'s internal
  constructor takes `Func<SuiteContext, CancellationToken, Task>`, `AssertionBuilder.Define` is `SuiteContext`-typed, and `SuiteRunner.RunAsync/RunAllAsync` take a `TransportSubject`. The plan keeps the existing
  internal constructor (so the 43 transport assertions and **every existing test body** in `ConformanceAssertionTests` and `SuiteRunnerTests` do not change; both files gain **new** tests, additions only: `Create<TContext>` and the `None` rejection) and adds an internal `ConformanceAssertion.Create<TContext>(…, Func<TContext, CancellationToken, Task> body)`
  that stores the body as an internal `Func<object, CancellationToken, Task>`. `SuiteRunner` becomes `SuiteRunner<TContext>`-shaped through one internal generic core
  (`SuiteRunnerCore.RunAsync<TContext>(assertion, face, Func<TContext> create, options, ct)`), with the transport runner a thin caller. Task 1.1 pins this with the 43-assertion catalogue and every existing driver unedited. The existing kit tests that PR 1 does edit are named: `SuiteRunnerTests` and `ConformanceAssertionTests` (additions, 1.1) and `AssertionCatalogueTests.Every_control_names_an_assertion_that_exists` (1.3: it must compare against the union of all kit catalogues).
- **R2 — `InvariantSubject.CreatePipeline` needs an `ILogger` (flag; the design's signature omits it).** `xcut-19`, `xcut-20` and `xcut-24` need a recording or throwing logger inside the pipeline's
  `InstrumentationPolicy`. The hook is fixed as `Func<IAsyncHttpClient, DexpaceClientOptions, TimeProvider, ILogger, HttpPipeline>`; the default builds
  `new PipelineBuilder().Add(new IdempotencyPolicy()).Add(new ClientIdentityPolicy()).Add(new SetDatePolicy(time)).AddStandardResilience(time, logger).Build(transport, options)` (what `DexpacePipeline.CreateDefault` does, but with
  the options captured at build, which `CreateDefault` does not take). Verified names: `PipelineBuilder.AddStandardResilience(TimeProvider?, ILogger?)`, `Build(IAsyncHttpClient, DexpaceClientOptions)`.
- **R3 — Hooks over sealed core types are delegates.** `AccessTokenCache`, `UrlRedactor`, `Headers`, `MediaType`, `AuthenticationChallenge` and `DigestChallengeHandler` are sealed or have no seam, so a
  negative control cannot substitute a broken one for the design's `CreateTokenCache`/`CreateRedactor`/`Models`/`DrawCnonce` as typed objects. The plan fixes: `CreateTokenCache: Func<TokenCredential, TimeProvider, Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>>>`
  (default wraps `new AccessTokenCache(credential, time).GetAsync`); `CreateRedactor: Func<string, string>` for a URL (default `new UrlRedactor().Redact(string)`) and `RedactHeaderValue: Func<string, string>`; `Models: ModelFactories` (a sealed record of
  delegates: `CreateHeaders`, `CreateMediaTypeParameters`, `CreateChallenge`, `CreateBytesBody`, each taking the caller's mutable input and returning the model, defaulting to the public factories); `DrawCnonce: Func<string>?`
  (null means "draw through the public `DigestChallengeHandler.Authorize`"; an optional hook, not a no-default one; the Digest cnonce source is a private field no driver can reach, so no hook supplies it from internals, R17). The exact members are fixed against the `RS0016` output in task 1.3.
- **R4 — `TransportFace.None` runs with no face factory.** `SuiteContext.Supplies(subject, face)` is transport-specific. In the invariant suite `Async` and `Blocking` are both always supplied (an `HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient`; the blocking face calls `Send` on a dedicated thread, as `BlockingFaceTransport` does) and `None` is
  supplied by definition, so face-level `NotExercised` never occurs in the invariant or codec suites; `NotExercised` comes only from an absent hook (P10-4). The codec suite's faces are `ISerde.DeserializeAsync`/`Deserialize` over the same codec instance.
- **R5 — `AppendixBMapTests` and `AppendixBMap`'s doc hard-code B.6/B.7.** `AppendixBMapTests` asserts `Equal(11, parsed.Count)` and `Equal(30 + 22, all.Length)` and its namespace is `…Tests.Catalogue` though it lives in `Requirements/`.
  Task 4.1 widens the parse to all nine sections and the totals to 61 items / 276 IDs (V8), and keeps the namespace (a rename would churn the file for nothing).
- **R6 — `.editorconfig` has seven `severity = none` lines and no `CA2000` severity, but three `dotnet_code_quality.*` lines (design V12 and P10-13 say "CA2000").** Re-verified against the tree: the `none` lines are `CA1308` (128), `CA1054`, `CA1055`, `CA1056` (132 to 134), `CA1062` (138) in library scope and `CA2007` (146), `CA1707` (150) in tests and tools. The `dotnet_code_quality.*` lines are
  **three**: line 52 `CA2000.dispose_ownership_transfer_at_constructor = true` (library scope; the line above it is the setting `dotnet_diagnostic.CA2000.severity = warning`, so its rationale comment sits above that setting), line 155 `CA2000.dispose_ownership_transfer_at_method_call = true` and line 156 `CA2000.excluded_type_names_with_derived_types = …` (test scope; line 156 has no comment of its own).
  `AnalyzerWaiverTests` (task 3.9) therefore applies **one block rule**: consecutive setting lines (no blank, no comment between them) form one block that shares the comment block above it, and the block needs a rationale and a `# Re-enable when:` line in that comment block (`CA1054`–`CA1056` and the 155 and 156 pair are each one block). Lines 155 and 156 get one `# Re-enable when: never for src/` in the comment block above them. **Line 52 is exempted by name as a hand-off rule, not a relaxation**: it makes `CA2000` follow a constructor that takes ownership (styleguide 13.1) and silences nothing; the test pins the exemption to exactly the key `dotnet_code_quality.CA2000.dispose_ownership_transfer_at_constructor` in library scope, so any other undocumented `dotnet_code_quality.*` line fails (and a control proves it).
  The design's V12 pragma counts (19 sites) were wrong: the tree has **43** `#pragma warning disable` lines under `src/` in 31 files (13 `CA1031`, 6 `CA1727`, 6 `CA2000`, 2 `CA5351`, 1 `CA5394`, 15 `RS0030`); the test counts nothing, it checks every one, and a scratch scan found no site lacking a why-comment on its line or the line above.
- **R7 — The type ban on `ConcurrentDictionary` is not one pragma; the plan bans the constructors instead (design P10-18, G; validation F1).** Measured on a scratch copy of the tree: `T:System.Collections.Concurrent.ConcurrentDictionary`2` flags **12 lines** of `BoundedMap.cs` (the `cref` at 18, and 49, 61, 81, 88, 103, 110 twice, 123, 139, 154, 171), and the existing `#pragma warning disable RS0030` / `restore` pair around the value-comparing `TryRemove` (lines 178 to 180) would re-enable the rule inside any file-scoped region. **Choice: ban `ConcurrentDictionary`'s constructors** (seven public overloads, one `M:…#ctor(…)` line each; the bare `…#ctor` is the parameterless overload's ID and matches only that one, verified: it flags `new ConcurrentDictionary<int,int>()` and not line 49), which makes line 49 the **only** site: one scoped `#pragma warning disable RS0030` / `restore` with a why-comment around that statement, and the `TryRemove` pair is left alone. Why not the file-scoped alternative: it would widen the `TryRemove` exemption to the whole file and force removing or merging the inner pair, for the same protection (a concurrent map cannot be built outside `BoundedMap`). `BannedSymbolsSiteTests` gains `ConcurrentDictionary_is_constructed_only_in_BoundedMap` beside `SuppressFlow_appears_only_in_BackgroundWork` (a source scan with the same self-test pattern).
- **R8 — `CiWorkflowTests` and the order of the PRs.** The test asserts that every gate of the design's gate table is a step of `ci.yml`; two of those steps (**Packaging conformance**, PR 3; **Requirement coverage**, PR 4) arrive in different PRs. The test is written in task 3.10 over the gates that exist at PR 3 and gains its last row in task 4.9, in the same commit that adds the step: no commit is red.
- **R9 — `scripts/ci/requirement-coverage.cs` has no test project, so it gets a shell self-test, and `--root`.** Like `coverage-gate.cs` (`coverage-gate-selftest.sh`), the script is exercised by `scripts/ci/requirement-coverage-selftest.sh`, which builds fixture trees in a temp directory and runs the script with `--root <dir>` (a flag the design does not list; the default root is the current directory) to prove each of the four finding kinds exits non-zero. The self-test is a CI step beside the check (Linux row).
- **R10 — Appendix B evidence grammar (P10-19 leaves the cell format open).** The `Evidence` cell is a `; `-separated list of items. An item `<suite>:<name>` (suite one of `transport`, `invariant`, `codec`, `packaging`) is an assertion reference and `*` is a prefix wildcard that must match at least one name; any other item is a repository-relative path (no `…/`
  abbreviations; the file is long-form) that must exist. Rule 4 applies to `suite` rows only. A `by reference` row may also carry assertion references (B.9.2, B.9.4, B.9.6, B.9.7 do), and they are checked for existence only.
- **R11 — `SuiteOptions` carries `AssertionTimeout` and `Waivers` only (design), so the invariant suite has no `ReleaseTimeout`.** Nothing in it observes a transport release; `xcut-13`'s release counting is at the body, which needs no bound beyond `AssertionTimeout`. `TransportSuiteOptions` is not touched.
- **R12 — The packaging file-based app builds the kit in Debug unless told otherwise.** `dotnet run scripts/ci/packaging-conformance.cs` compiles `#:project`-referenced code with the default configuration. That is acceptable (it reads `.nupkg` bytes and runs no packed code), and the app carries `#:property RestorePackagesWithLockFile=false` and `#:property PublishAot=false` like its siblings. V6 proved it resolves a kit type.
- **R13 — The core driver is not a transport driver and needs no `Owner` waiver.** Every first-party driver pins its expected `NotExercised` list (P10-28). The lists, fixed here: **core driver**: `xcut-23.resolution-deterministic` (needs `SeamResolution`, which only the DI driver supplies); **DI driver**: `xcut-8.strict-factory-rejects`, `xcut-9.cycle-safe-walk`, `xcut-14.bounded-under-burst`, `xcut-14.drains-after-insert`; **AOT smoke** (core defaults, no internals): those five plus nothing else. Any other `NotExercised`, and any `Failed`, fails the driver; no assertion is waived. `xcut-21` is in no list: its `DrawCnonce` hook is optional (R17).
- **R14 — `xcut-8.convenience-returns-response` exercises `ErrorMappingStep`/`EnsureSuccess`, not a constructor.** Verified: `ErrorMappingStep` is public and `Instance` returns a non-error response unchanged; the internal `ErrorMapping.ToException` is what `MapStrict` is given in the core driver. The public `HttpResponseException(Response, string? message = null)` accepting a 200 is P10-24's reading and is **not** asserted (an assertion would pin the unguarded constructor); the checklist records the reading.
- **R15 — `reproducible-pack.sh` cross-path packs `HEAD`, so uncommitted edits are not packed (flag, edge).** `git worktree add --detach <dir> HEAD` carries committed content only; a developer running the script with a dirty tree gets a pass or fail about `HEAD`, not the tree. The script prints a warning when `git status --porcelain` is non-empty. CI always runs on a clean checkout.
- **R16 — `NFR-9` is dispositioned with `NFR-8`'s assertion and the smoke (P10-17).** The row is ✅ once `CheckPhase10Async` (task 4.4) and `nfr-8.trim-and-aot-declared` (task 3.3) are green; the design lists it "once P10-17 lands", and the plan's row table says task 4.4 is the closing task.

- **R17 — `XCUT-21`'s source clause is cited by reference; the assertion judges format and no-repeat only (validation F5; corrects R3, R13 and task 1.10).** The design disagreed with itself on whether `DrawCnonce` has a default. Re-verified: `DigestChallengeHandler`'s CSPRNG factory is the private field `_cnonceFactory` (line 48), set from a lambda `static () => RandomNumberGenerator.GetHexString(32, lowercase: true)` in the public constructor (line 65), so the core driver cannot hand the kit "the source" from internals, and an output test cannot tell a CSPRNG from `System.Random`. Reading: `DrawCnonce` is `Func<string>?`, **optional**, null meaning "draw through the public `DigestChallengeHandler.Authorize`"; `xcut-21.cnonce-unpredictable` asserts 32 lower-hex characters (128 bits) and no repeat over 10 000 draws, is never `NotExercised`, and the "operating system's CSPRNG" clause is evidence **by reference** to `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator` (a `Security`-adjacent unit test, unedited). Its negative controls fail on what the output can show: a 64-bit draw (width) and a draw cycling through a short fixed sequence (repeats). A seeded `System.Random` draw of 128 bits would pass the assertion; that is the stated limit, not a defect. The no-default hooks are therefore **four** (`MapStrict`, `CreateCyclicException`, `CreateBoundedMap`, `SeamResolution`).

### Ruling dependencies

| Ruling (open for the lead) | Tasks that depend on it | If reversed |
|---|---|---|
| P10-1 one phase | all | the chapter cut (10a = PRs 1 and 2, 10b = PRs 3 and 4) is liftable verbatim; no task changes. |
| P10-2 measure and route | all of PR 1 to 3; 4.8 | Option B (small core repairs) adds test-first core tasks before 4.8; `ServiceResponseException` could then be deleted in 1.x. |
| P10-5 core driver in `Core.Tests`, `SEAM-2` allow-list widened | 1.10 | Option B: the driver moves to `Conformance.Tests` over public API; the five hook-less assertions are `NotExercised` there and cited `by reference`; task 1.10's csproj and `Seam2ArchitectureTests` edits are dropped and 4.2's rule-4 rows for `xcut-8/9/14` weaken to `by reference`. |
| P10-7 one result model with `TransportFace.None` | 1.1, 1.2, every assertion | Renaming to `ConformanceFace` is a mechanical rename in 1.1 plus a CHANGELOG line; nullable face breaks `ConformanceResult` deconstruction in 1.1. |
| P10-8 public probe models | 2.1, 2.5, 4.4 | A generic `CodecSubject<TContext>` couples the kit to one metadata model: 2.1 and 2.5 are rewritten. |
| P10-9 7c vectors not lifted | 4.2 | Lifting adds public helpers and tasks to PR 2; B.1 rows stay `by reference` otherwise. |
| P10-16 ruleset change | 3.10, 4.10 | Not made by close-out: `NFR-17` is ⏳ with a `docs/first-release.md` blocker (task 4.8). |
| P10-17 smoke keeps project references | 4.4 | A second publish over the packages adds a local feed and a CI step. |
| P10-21 / P10-22 decline Stryker and benchmarks | 4.8 | Building either adds a project, a package version and lock files; neither is scheduled. |
| P10-23 / P10-24 / P10-25 findings routed | 4.6, 4.8 | Repairs move into phase 11's scope, not this plan. |
| P10-27 phase page | 4.5 | Only extending `conformance.md`: drop `cross-cutting-invariants.md` and its `docs/README.md` row. |

---

## Task 0.1 — Pre-flight: queries, baseline and the facts to re-verify (no commit)

1. **Read what is known** (start of the phase and of every numbered task):

   ```bash
   scripts/knowledge --origin note --brief
   scripts/knowledge --section conflicts --brief
   scripts/knowledge --prefix-info XCUT      # 24 IDs; 22 MUST / 2 SHOULD
   scripts/knowledge --prefix-info NFR       # 17 IDs; 4 MUST / 13 SHOULD
   scripts/knowledge --gaps XCUT             # 0 gaps expected; then --gaps NFR
   scripts/knowledge --req XCUT-8            # and each XCUT/NFR/SERDE ID as its task starts
   ```

2. **Confirm the predecessors.** `git log --oneline main -6` shows #98 (`af603d2`). Record `dotnet --version` (10.0.401), the passing counts per test project, the aggregate coverage (design V1: 97.60%) and that V-conf is green with exactly
   each transport driver's pinned waivers. Create `99-phase-10-kit-contract-and-invariants` off `main` (no push).
3. **Re-run the design's facts as throwaway probes** in a scratch class `Scratch/FactProbe.cs` inside `tests/Dexpace.Sdk.Core.Tests` (delete before any commit); a fact that differs from the design is a **stop**: record it and amend the affected task.
   - **V2** (public-surface reachability): the `IsRetryable`-vs-classifier loop over 400, 404, 408, 429, 500 to 505, 507, 599; the `AddStandardResilience` over `DelegateHttpClient` sends (501 in the configured set 3 times, 500 outside once; body-less `POST` over `ServiceRequestException` once, `GET` 3 times; a custom `IRetryableError`); cancel during a 30 s backoff with a recording `TimeProvider` (one timer created and disposed); `EnumerateCauses` over a self-cycle; the `HttpHeaderSyntax` byte table; `ErrorMappingStep.Instance.Apply` returns a 200; `new HttpResponseException(response200)` does not throw.
   - **V13/V14** (greps): `ConcurrentDictionary` only in `BoundedMap.cs` (`grep -rn ConcurrentDictionary src --include='*.cs'`); `GetBaseException` nowhere in `src/`.
   - **V10** (reflection over core's exported members): every signature type is under `System`, `Microsoft.Extensions.Logging` or `Dexpace.Sdk.Core` (this is task 3.6's test body run once as a probe).
   - **V3** (cross-path pack, two `git worktree`s) and **V6** (the packed assembly's `AssemblyMetadata` read with `System.Reflection.Metadata`) are re-run as tasks 3.11 and 3.1's tests, not as probes.
   - **V9** (`gh api repos/dexpace/dotnet-sdk/rulesets` and `.../branches/main/protection`, **read-only**): confirm no `required_status_checks` rule. This is the only remote read in the plan.
4. **Baseline the permanent waivers.** V-conf as it stands at this task (SystemNet, DI and Conformance.Tests only: Core.Tests and the STJ tests have no `Conformance` class yet and would exit 8 on zero tests; they join in 1.10 and 2.5) and note the 22 skipped rows (V1): none must be new after any task.
5. **Open items for the lead** are listed at the end; none blocks execution.

---

# PR 1 — The kit contract and the invariant suite

Branch `99-phase-10-kit-contract-and-invariants` off `main`. Rows touched: `XCUT-1`..`XCUT-24`, `NFR-11` (behavioural half), `NFR-4`/`NFR-9` evidence (the smoke arrives in PR 4).
Green on its own: the new kit code is exercised by its controls and the two drivers in this PR.

## Task 1.1 — `TransportFace.None` and the generic runner (R1, R4; P10-7)

**Files.** Edit: `src/Dexpace.Sdk.Conformance/Reporting/TransportFace.cs` (add `None = 2`, doc: "an assertion that judges a value and drives no call surface"), `src/Dexpace.Sdk.Conformance/Reporting/ConformanceAssertion.cs` (internal `Create<TContext>` and the stored `Func<object, CancellationToken, Task>`; the existing internal constructor delegates to it), `src/Dexpace.Sdk.Conformance/Suite/SuiteRunner.cs` (extract the status, waiver and bound logic into an internal generic core; the transport methods call it),
`src/Dexpace.Sdk.Conformance/PublicAPI.Unshipped.txt`, `tests/Dexpace.Sdk.Conformance.Tests/Suite/SuiteRunnerTests.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Reporting/ConformanceAssertionTests.cs`.
New: `src/Dexpace.Sdk.Conformance/Suite/SuiteRunnerCore.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Suite/GenericRunnerTests.cs` (`Unit`).

**Tests first** (`GenericRunnerTests`; red as compile errors until the core exists).
- `A_none_face_assertion_runs_without_a_factory`: an assertion declared `[TransportFace.None]` over a trivial context reports `Passed`.
- `The_six_statuses_come_out_of_the_generic_core`: bodies throwing `ConformanceException`, `ConformanceVacuousException`, `NotExercisedException`, `InvalidOperationException` give `Failed`, `Vacuous`, `NotExercised`, `Errored`; a body that returns is `Passed`; a body exceeding `AssertionTimeout` is **`Failed`** naming the bound and the assertion (`Bounded.RunAsync` throws a `ConformanceException`, the same rule `SuiteRunnerTests.A_body_that_outlives_the_assertion_timeout_is_Failed_naming_the_bound_and_the_assertion` pins for the transport; it is not `Errored`).
- `A_waiver_turns_failed_into_waived_and_a_passing_covered_assertion_into_a_stale_waiver_failure`: the rule of `SuiteRunner.ApplyWaivers` (P8a-8) holds through the generic core, with a `Face = None` waiver scope.
- `An_unknown_waiver_target_is_a_failed_synthetic_row`.
- `TransportFace_has_three_members_with_stable_values` (pin: `Async = 0`, `Blocking = 1`, `None = 2`).

**Production change.** As R1. `SuiteRunner.RunAsync(TransportSubject, …)` and `RunAllAsync` keep their signatures and call `SuiteRunnerCore.RunAsync<SuiteContext>` with `() => new SuiteContext(subject, face, options)`; `SuiteContext.Supplies` stays the transport's pre-check. `TransportFace.None` on a transport assertion is rejected by the transport runner (`ArgumentException`: "the transport suite has no `None` face"), pinned by `SuiteRunnerTests`.

**Existing tests this task edits (additions only; no existing test body changes).** `SuiteRunnerTests` gains the `None`-rejection pin; `ConformanceAssertionTests` gains the `Create<TContext>` tests. Everything else is untouched.
**Verify.** V-build; V-conf-tests `GenericRunnerTests`; then **every existing kit test green with no existing test body edited** (`AssertionCatalogueTests` still 43 names, `SuiteRunnerTests`, `ConformanceAssertionTests`, `ConformanceReportTests`, the raw-socket driver), then V-conf (both SystemNet drivers and the DI driver unchanged and green).
**IDs.** none (contract). **Commit.** `feat: conformance kit runner generic over the suite context, TransportFace.None`.

## Task 1.2 — `SuiteOptions`, per-suite preambles and `ConformanceReport.Combine` (P10-7)

**Files.** Edit: `src/Dexpace.Sdk.Conformance/Reporting/ConformanceReport.cs` (a `Suite` name recorded by `Create`; the static `Preamble` stays the transport text; an instance `PreambleText` picks the suite's; `Combine`), `PublicAPI.Unshipped.txt`.
New: `src/Dexpace.Sdk.Conformance/Subjects/SuiteOptions.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Reporting/ConformanceReportCombineTests.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Subjects/SuiteOptionsTests.cs` (`Unit`).

**Tests first.**
- `SuiteOptionsTests`: defaults (`AssertionTimeout` 30 s, `Waivers` empty); a non-positive timeout throws `ArgumentOutOfRangeException`; `Waivers` is copied (the `TransportSuiteOptions` shape).
- `ConformanceReportCombineTests`: `Combine("subject", [a, b])` concatenates results, takes the worst status per requirement (`StatusSeverity.Worst`, 8a's rule) and per appendix-B item, `IsGreen` is false when either input has a failure, the combined waiver list is the union, `Combine` of zero reports throws `ArgumentException`, null guards; `Each_suite_report_renders_its_own_preamble` (the transport report still starts with the unchanged transport preamble; an invariant, codec and packaging report each name what a green run does not prove, e.g. the invariant text contains "NotExercised" and "never passed").

**Production change.** `public sealed record SuiteOptions { TimeSpan AssertionTimeout { get; init; } IReadOnlyList<ConformanceWaiver> Waivers { get; init; } }` (a sealed class like `TransportSuiteOptions` if the analyzer prefers; follow `TransportSuiteOptions`' validated `init` bodies); `public static ConformanceReport Combine(string subjectName, IEnumerable<ConformanceReport> reports)`; an internal `ConformanceReport.Create(subjectName, results, waivers, suite)` overload used by the new suites. The three new preamble strings are `internal static` constants of the suites (tasks 1.3, 2.1, 3.1) passed in.

**Verify.** V-build; V-conf-tests `ConformanceReportCombineTests`, `SuiteOptionsTests`, then the existing `ConformanceReportTests` unedited. **IDs.** none. **Commit.** `feat: SuiteOptions, per-suite report preambles and ConformanceReport.Combine`.

## Task 1.3 — The invariant suite's skeleton (`InvariantSubject`, hooks, recording time, the catalogue) (P10-4, R2, R3, R4)

**Files.** New: `src/Dexpace.Sdk.Conformance/Subjects/InvariantSubject.cs`, `ModelFactories.cs`, `BoundedMapProbe.cs`, `SeamResolution.cs`, `src/Dexpace.Sdk.Conformance/Suite/InvariantSuite.cs`, `src/Dexpace.Sdk.Conformance/Suite/InvariantContext.cs` (what one run owns: the subject, the face, the options, a `RecordingTimeProvider`, a `RecordingLogger` (the kit's existing `Internals/RecordingLogger`), the pipelines it built, disposed on every path), `src/Dexpace.Sdk.Conformance/Internals/RecordingTimeProvider.cs` (over `TimeProvider.System`, records created and disposed timers, advances manually), `src/Dexpace.Sdk.Conformance/Assertions/InvariantCatalogue.cs`, `src/Dexpace.Sdk.Conformance/Assertions/InvariantAssertionBuilder.cs`,
`tests/Dexpace.Sdk.Conformance.Tests/Suite/InvariantSuiteTests.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Internals/RecordingTimeProviderTests.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Assertions/InvariantCatalogueTests.cs`. Edit: `PublicAPI.Unshipped.txt`, **`tests/Dexpace.Sdk.Conformance.Tests/Assertions/AssertionCatalogueTests.cs`** (see below). New: `tests/Dexpace.Sdk.Conformance.Tests/Assertions/KitCatalogues.cs` (internal: `All` = the union of `TransportSuite`, `InvariantSuite`, and, as they arrive, `CodecSuite` and `PackagingSuite` assertions).

**Tests first.**
- `AssertionCatalogueTests.Every_control_names_an_assertion_that_exists` (validation F2): today it requires every `Controls/*ControlTests` `Covered` ID to be in `TransportSuite.Assertions` (`ControlClasses.Covered` is the union over every control class by shape), so the first invariant control class (task 1.4) would break it. **This task edits it** to compare `ControlClasses.Covered` against `KitCatalogues.All` (the union of all kit catalogues; tasks 2.1 and 3.1 add their suites to `All`), and adds `Assertion_names_are_unique_across_the_kit_catalogues`. The other rule, `Every_assertion_has_a_negative_control_found_without_any_test_having_run`, stays transport-only (each new suite has its own `Every_*_assertion_has_a_negative_control`). Red first: with a throwaway invariant control class naming `xcut-1.cancel-is-terminal` the old test fails, the edited one passes once `xcut-1…` is registered in 1.4.
- `InvariantSuiteTests`: `Assertions` is the 28 names of the [catalogue pin](#invariant-catalogue-the-28-assertions) in order with the stated level, faces and IDs (red until tasks 1.4 to 1.9 land: **this test is written in this task with the full list and the catalogue grows to meet it, task by task; until then it compares the prefix registered so far** — the test reads `InvariantCatalogue.All` and asserts the expected list's registered prefix, and task 1.9's last commit tightens it to equality; recorded in the checklist as the one deliberate staged pin);
  `RunAllAsync` over `new InvariantSubject()` returns a report whose `Suite` is invariant and whose preamble says a missing hook is `NotExercised`; an assertion whose hook is absent is `NotExercised` with a reason naming the hook, never `Passed`; `RunAsync` rejects a face the assertion does not declare; null guards.
- `RecordingTimeProviderTests`: `CreateTimer` returns a live timer, `Created`/`Disposed` counters move, `GetUtcNow`/`GetTimestamp` delegate to the system provider unless advanced, no `Task.Delay` anywhere (the kit's ban compiles).
- `InvariantCatalogueTests` (staged as above): names match the name pattern, every cited ID exists in `RequirementIndex`, no assertion is stronger than its strongest ID, `ControlClasses.Covered` ⊇ the registered names (the widened `AssertionCatalogueTests` rule, written per suite: `Every_invariant_assertion_has_a_negative_control`).

**Production change.** `InvariantSubject` (sealed record, `init` members, every default core's own component):
`Name`; `CreatePipeline` (R2); `CreateResponseException: Func<Response, SdkException>` (default `response => new HttpResponseException(response)`); `EnumerateCauses: Func<Exception, IEnumerable<Exception>>` (default `ExceptionFacts.EnumerateCauses`); `CreateTokenCache`, `CreateRedactor`/`RedactHeaderValue`, `Models`, `CreateAuthPolicy: Func<HttpPipelinePolicy-factory per credential kind>` (R3; default `BasicAuthPolicy(BasicCredential)` and `BearerTokenAuthPolicy(TokenCredential, scopes)`);
`Credentials` (a small record of the sample credentials the redaction assertion renders); and the four no-default members `MapStrict`, `CreateCyclicException`, `CreateBoundedMap`, `SeamResolution` (all `null` by default: an assertion whose member is `null` is `NotExercised`), plus the **optional** `DrawCnonce` (`Func<string>?`; null means the assertion draws through the public `DigestChallengeHandler.Authorize`, so `xcut-21` is never `NotExercised`; format and no-repeat only, R17).
`BoundedMapProbe(int capacity, Action<string> insert, Func<int> count)` and `SeamResolution(Action ResolveWithNone, Action ResolveWithTwo, Func<bool> ExplicitWins)` as the design. `InvariantSuite` mirrors `TransportSuite`: `Preamble`, `Assertions`, `RunAsync(InvariantSubject, ConformanceAssertion, TransportFace, SuiteOptions? = null, CancellationToken = default)`, `RunAllAsync(InvariantSubject, SuiteOptions? = null, CancellationToken = default)`. The suite never calls `Task.Delay`.

**Verify.** V-build; V-conf-tests `InvariantSuiteTests`, `RecordingTimeProviderTests`, `InvariantCatalogueTests`. **IDs.** none (contract). **Commit.** `feat: invariant suite skeleton and InvariantSubject`.

### Invariant catalogue: the 28 assertions

Each is asserted in a task below, each has a control row in `Controls/Invariant*ControlTests` (broken subject member in the table), and each cites the IDs shown; names are the design's table verbatim.
`xcut-1.cancel-is-terminal`, `xcut-2.timeout-is-retryable`, `xcut-3.wait-promptly-cancellable` (task 1.4); `xcut-4.two-branches`, `xcut-5.one-classifier`, `xcut-6.capability-queried`, `xcut-7.configured-set-authoritative`, `xcut-8.strict-factory-rejects`, `xcut-8.convenience-returns-response` (1.5);
`xcut-9.cycle-safe-walk`, `xcut-10.safety-gate-uniform`, `xcut-11.shared-no-crosstalk`, `xcut-12.single-flight-scoped` (1.6); `xcut-13.dispose-latched`, `xcut-13.dispose-non-blocking`, `xcut-14.bounded-under-burst`, `xcut-14.drains-after-insert`, `xcut-15.no-external-alias` (1.7);
`xcut-16.no-credential-over-http`, `xcut-17.redirect-hygiene`, `xcut-18.model-validation`, `xcut-19.redaction-default-deny` (1.8); `xcut-20.observability-never-throws`, `xcut-21.cnonce-unpredictable`, `xcut-22.core-borrows`, `xcut-23.resolution-deterministic`, `xcut-24.preview-capped-non-consuming`, `nfr-11.scheduler-agnostic` (1.9).
Levels: `xcut-12`, `xcut-24` and `nfr-11` SHOULD, the rest MUST. Faces: `None` for `xcut-5`, `xcut-8.strict`, `xcut-9`, `xcut-14` (both), `xcut-15`, `xcut-18`, `xcut-21`, `xcut-23`; both faces for the others.
Case tables port from `nodejs-sdk@54aeed4` `tests/conformance/xcut/*.conformance.test.ts` and `ruby-sdk@5b17395` `gems/dexpace-conformance/lib/dexpace/conformance/invariant_suite/*.rb`; **each ported file cites its source in its header** (roadmap constraint 10, P10-30).

## Task 1.4 — Cancellation, timeout, wait (`XCUT-1`, `XCUT-2`, `XCUT-3`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/CancellationInvariants.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/InvariantCancellationControlTests.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Controls/BrokenPipelines.cs` (shared broken-policy fixtures, `internal`). Edit: `InvariantCatalogue.cs`.

**Tests first** (`InvariantCancellationControlTests`, `Unit`; theory over `ControlRow`-shaped rows for the invariant suite: a new `InvariantControlRow` record beside `ControlRow` in `Drivers/`, same `Covered`/`Cases` shape, running `InvariantSuite.RunAsync` on the row's subject). Each row is run on both faces and must be `Failed`:
- `xcut-1.cancel-is-terminal` ← a pipeline with a policy that catches `OperationCanceledException` and retries (control 1), and one that replaces it with a `ServiceRequestException` (control 2).
- `xcut-2.timeout-is-retryable` ← a policy mapping a deadline to `OperationCanceledException`.
- `xcut-3.wait-promptly-cancellable` ← a retry wait that ignores the token (control 1) and one that leaks its armed timer (control 2: `RecordingTimeProvider.Disposed < Created`).
- Positive: the three assertions pass over `new InvariantSubject()` (V2: cancel during a 30 s backoff gives `OperationCanceledException` carrying the caller's token, one send, one timer created and disposed).

**Production change.** The assertions over `subject.CreatePipeline` + `DelegateHttpClient`: `xcut-1` sends through a transport that blocks until cancelled; asserts one send, `OperationCanceledException` whose `CancellationToken` is the caller's, the caller's token still cancelled, no retry. `xcut-2` configures `AttemptTimeout` over a hanging transport: the retryable timeout exception, caller token clear, the call re-issued (asserts ≥ 2 sends). `xcut-3` configures a 30 s `BaseDelay` over the recording `TimeProvider`, cancels during the wait, asserts completion within a bounded real-time window (`Bounded.RunAsync`, not `Task.Delay`) and every armed timer disposed. The transport-level clauses stay in `TransportSuite` (P10-6): this task does not re-implement `transport-3/-4`.

**Verify.** V-build; V-conf-tests `InvariantCancellationControlTests`; the controls are the red evidence (no mutation of core is made). **IDs.** `XCUT-1`, `XCUT-2`, `XCUT-3`. **Commit.** `feat: invariant assertions xcut-1 to xcut-3 with negative controls`.

## Task 1.5 — The error taxonomy and the classifier (`XCUT-4`, `-5`, `-6`, `-7`, `-8`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/TaxonomyInvariants.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/InvariantTaxonomyControlTests.cs`. Edit: `InvariantCatalogue.cs`, `BrokenPipelines.cs`.

**Tests first** (controls, each `Failed`):
- `xcut-4.two-branches` ← a mapping that throws a non-retryable exception for a refused connection (a `ServiceRequestException` subtype with `IsRetryable = false`), and one that surfaces a status error as a bare `SdkException` carrying no status.
- `xcut-5.one-classifier` (face `None`) ← `CreateResponseException` baking `false` for 507 (and one baking `true` for 501).
- `xcut-6.capability-queried` ← a retry policy matching concrete exception types (a custom `IRetryableError` exception is not retried).
- `xcut-7.configured-set-authoritative` ← a retry policy reading the baked `IsRetryable` flag instead of `RetryOptions.RetryableStatusCodes`.
- `xcut-8.strict-factory-rejects` (face `None`, hook `MapStrict`) ← a factory that maps a 200; `xcut-8.convenience-returns-response` ← a step that throws for a 302.
- Positive (default subject): 5xx matrix 408/429/500/502/503/504/507 retryable, 400/404/501/505 not (V2, 0 mismatches); a custom `IRetryableError` retried; widening 501 / narrowing 500 / re-classifying a re-sent status through `RetryOptions.RetryableStatusCodes` (V2); `xcut-8.strict` is `NotExercised` naming `MapStrict` when absent (`InvariantSuiteTests`' hook rule, R13).

**Production change.** The assertions over `CreatePipeline`/`CreateResponseException`/`MapStrict` as the design's rows and R14 (`xcut-8.convenience-returns-response` does **not** call `new HttpResponseException(200)`). `xcut-4` asserts: a status failure is an `HttpResponseException` (status, headers, body readable), a no-response failure is an `SdkException` with `IsRetryable` and a cause of the I/O family (`IOException`/`SocketException`; the "generic I/O error catchable" literal clause is §10 entry 7 and is **not** asserted, P10-25).

**Verify.** V-build; V-conf-tests `InvariantTaxonomyControlTests`. **IDs.** `XCUT-4`, `XCUT-5`, `XCUT-6`, `XCUT-7`, `XCUT-8`. **Commit.** `feat: invariant assertions xcut-4 to xcut-8 with negative controls`.

## Task 1.6 — Cycle-safe walk, the safety gate, concurrency, single-flight (`XCUT-9`, `-10`, `-11`, `-12`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/ResilienceInvariants.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/InvariantResilienceControlTests.cs`. Edit: `InvariantCatalogue.cs`, `BrokenPipelines.cs`.

**Tests first** (controls, `Failed`):
- `xcut-9.cycle-safe-walk` (face `None`; hooks `CreateCyclicException` + `EnumerateCauses`) ← a walk tracking by `Equals` over an exception type overriding `Equals` (the control reuses a structurally-equal exception from TestSupport's `StructurallyEqualException`); the assertion **walks** the cyclic exception and never throws one (the Windows hang `CyclicExceptions` documents; the control subject builds its cycle with the same reflection helper, `tests/Dexpace.Sdk.TestSupport/Recovery/CyclicExceptions.cs`).
- `xcut-10.safety-gate-uniform` ← a gate special-casing a transport failure (the five-way matrix of ch.19 is one assertion with five cases: body-less `POST` over a transport failure not re-sent; body-less `GET` re-sent; replayable-body `POST` per policy; single-use body not re-sent after a transport failure; `Idempotency-Key` marks re-sendable; the control fails on the case it breaks, asserted by name in `Detail`).
- `xcut-11.shared-no-crosstalk` ← a policy holding the attempt counter in a field (64 concurrent distinct requests through one shared pipeline, each response its own).
- `xcut-12.single-flight-scoped` ← a cache with one global lock (a fetch blocked on key A holds key B) and a cache that fetches per caller (N racers, N fetches).
- Positive (default subject): all four pass; `xcut-9` is `NotExercised` when `CreateCyclicException` is absent (R13).

**Production change.** As the design rows; `xcut-12` drives `CreateTokenCache` (R3's delegate form) with a scripted `TokenCredential` whose fetch blocks on a gate the kit releases (no `Task.Delay`).

**Verify.** V-build; V-conf-tests `InvariantResilienceControlTests`. **IDs.** `XCUT-9`, `XCUT-10`, `XCUT-11`, `XCUT-12`. **Commit.** `feat: invariant assertions xcut-9 to xcut-12 with negative controls`.

## Task 1.7 — Lifecycle, bounded maps, immutability (`XCUT-13`, `-14`, `-15`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/LifecycleInvariants.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/InvariantLifecycleControlTests.cs`. Edit: `InvariantCatalogue.cs`.

**Tests first** (controls, `Failed`):
- `xcut-13.dispose-latched` ← a `ResponseBody` whose release runs twice (the kit's release-counting body, counted at the resource) behind a core `Response`, `TypedResponse<T>` and `ServerSentEventStream` (the assertion disposes each twice, concurrently, and requires one release).
- `xcut-13.dispose-non-blocking` ← a dispose that waits for a parked read (`Internals/ParkedReadStream`).
- `xcut-14.bounded-under-burst` and `xcut-14.drains-after-insert` (face `None`; hook `CreateBoundedMap`) ← an unbounded probe; a single pre-insert check-then-evict probe (the post-insert drain-to-cap loop is what `BoundedMap` does).
- `xcut-15.no-external-alias` (face `None`; hook `Models`) ← a factory that keeps the caller's list (mutating the caller's inner collection after construction changes the model; every `With*` returns a new instance).
- Positive (default subject): `xcut-13` x2 and `xcut-15` pass; `xcut-14` x2 are `NotExercised` naming `CreateBoundedMap` (R13).

**Production change.** As the design rows. `xcut-15`'s default `ModelFactories` call the public constructors/factories of `Headers` (`Headers.Builder`), `MediaType`, `AuthenticationChallenge` and `RequestBody.FromBytes`; V-verified by `ModelImmutabilityArchitectureTests` for the structural half. The kit's `BoundedMapProbe` is the portable face: no internal type is named.

**Verify.** V-build; V-conf-tests `InvariantLifecycleControlTests`. **IDs.** `XCUT-13`, `XCUT-14`, `XCUT-15`. **Commit.** `feat: invariant assertions xcut-13 to xcut-15 with negative controls`.

## Task 1.8 — Security by default (`XCUT-16`, `-17`, `-18`, `-19`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/SecurityInvariants.cs`, `src/Dexpace.Sdk.Conformance/Assertions/SecurityVectors.cs` (the ch.19 byte table and the redaction vectors, ported from Node's `security-by-default`); `tests/Dexpace.Sdk.Conformance.Tests/Controls/InvariantSecurityControlTests.cs`. Edit: `InvariantCatalogue.cs`.

**Tests first** (controls, `Failed`):
- `xcut-16.no-credential-over-http` ← an auth policy that fetches before the https guard (no token may be fetched and no header written over `http://`; a credential-free cross-origin hop proceeds).
- `xcut-17.redirect-hygiene` ← a redirect policy judging origin against the previous hop instead of the seed (`Authorization` stripped on every hop; `Cookie`/`Proxy-Authorization` cross-origin against the seed; a permitted downgrade, then a retry: no re-attach).
- `xcut-18.model-validation` (face `None`; hook `Models`) ← a header model accepting HTAB in a name (the ch.19 table: C0, DEL, non-ASCII, HTAB by position, inbound obs-text).
- `xcut-19.redaction-default-deny` ← an allow-all redactor (`CreateRedactor` returning its input) and a credential whose `ToString` shows the secret (userinfo, query values, fragment key=value, header values unless allow-listed, credentials' `ToString`, a `Location` in a thrown status error).
- Positive (default subject): all four pass. The dispatch-time half of `XCUT-18` stays `TransportSuite`'s (P10-6).

**Production change.** As the design rows; the redirect assertion drives the subject pipeline over a scripted in-memory transport (no socket); the vectors cite their Node source in the file header.

**Verify.** V-build; V-conf-tests `InvariantSecurityControlTests`; V-sec (unchanged). **IDs.** `XCUT-16`, `XCUT-17`, `XCUT-18`, `XCUT-19`. **Commit.** `feat: invariant assertions xcut-16 to xcut-19 with negative controls`.

## Task 1.9 — Observability, entropy, ownership, resolution, previews, scheduler-agnostic (`XCUT-20`, `-21`, `-22`, `-23`, `-24`, `NFR-11`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/HostInvariants.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/InvariantHostControlTests.cs`. Edit: `InvariantCatalogue.cs`, `InvariantCatalogueTests.cs` (tighten the staged pin to equality of the 28), `InvariantSuiteTests.cs`.

**Tests first** (controls, `Failed`):
- `xcut-20.observability-never-throws` ← a renderer that throws on a malformed URL (expect `[malformed url]`); a throwing `ILogger` at `Body` level (R2's logger parameter), and the request still completes (`§11 item 37`: the SDK's own recording is total).
- `xcut-21.cnonce-unpredictable` (face `None`; optional hook `DrawCnonce`, R17) ← (a) a 64-bit draw (fails the 32-lower-hex-character, 128-bit width) and (b) a draw cycling through a short fixed sequence, or a constant (fails no-repeat over 10 000 draws). Both fail on what the output shows; the CSPRNG-source clause is not asserted here and is cited by reference to `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator`. The default draws through `DigestChallengeHandler.Authorize`.
- `xcut-22.core-borrows` ← a bridge that disposes what it wraps (`HttpPipeline`, `AsAsync`, `AsBlocking` over a counting transport dispose nothing; the transport still works).
- `xcut-23.resolution-deterministic` (face `None`; hook `SeamResolution`, no default) ← a resolver picking the last of two.
- `xcut-24.preview-capped-non-consuming` ← a preview that buffers the whole body (10 MiB body, 1 KiB cap, allocation under 2 MiB, the full read intact; byte-capped for error snapshots; the over-1 MiB error-snapshot clause is **declined, P10-23, and not asserted**).
- `nfr-11.scheduler-agnostic` ← a policy blocking on a `SynchronizationContext` (the same call from the thread pool, dedicated threads, an exclusive `ConcurrentExclusiveSchedulerPair` scheduler and a single-threaded `SynchronizationContext`, both faces).
- Positive (default subject): all pass except `xcut-23` (`NotExercised`, R13).

**Production change.** As the design rows. The staged catalogue pin becomes equality with the 28 names. All 28 now have controls: `Every_invariant_assertion_has_a_negative_control` passes with no exceptions.

**Verify.** V-build; V-conf-tests `InvariantHostControlTests`, `InvariantCatalogueTests`, `InvariantSuiteTests`; `dotnet test --project tests/Dexpace.Sdk.Conformance.Tests --configuration Release --no-build` whole project. **IDs.** `XCUT-20`, `XCUT-21`, `XCUT-22`, `XCUT-23`, `XCUT-24`, `NFR-11` (behavioural half). **Commit.** `feat: invariant assertions xcut-20 to xcut-24 and nfr-11 with negative controls`.

## Task 1.10 — The core driver and the `SEAM-2` allow-list (P10-5, P10-28, R13)

**Files.** Edit: `tests/Dexpace.Sdk.Core.Tests/Dexpace.Sdk.Core.Tests.csproj` (a `ProjectReference` to `..\..\src\Dexpace.Sdk.Conformance\Dexpace.Sdk.Conformance.csproj`), `tests/Dexpace.Sdk.Core.Tests/Architecture/Seam2ArchitectureTests.cs` (`s_allowed` gains `"Dexpace.Sdk.Conformance"`, with a comment citing P10-5's argument: the kit references core alone, so it brings no transport or codec), `tests/Dexpace.Sdk.Core.Tests/packages.lock.json`.
New: `tests/Dexpace.Sdk.Core.Tests/Conformance/InvariantConformanceTests.cs` (`Conformance`).

**Tests first.**
- `InvariantConformanceTests`: `Assertion_holds` (theory over `InvariantSuite.Assertions` × faces) and `The_whole_suite_is_green_and_its_report_is_written` against a subject that supplies the three internal hooks: `MapStrict = ErrorMapping.ToException` (internal; reachable by `InternalsVisibleTo`), `CreateCyclicException` from `Dexpace.Sdk.TestSupport`'s `CyclicExceptions` (**walk only, never throw**) and `CreateBoundedMap` over `BoundedMap<string, object>` (insert, `Count`, capacity). It supplies no cnonce draw: `xcut-21` runs through the public `Authorize` default (R17). `Only_xcut_23_is_not_exercised` pins the expected list (P10-28): any other `NotExercised` fails.
- `Seam2ArchitectureTests`: `Core_suite_loads_only_core_and_the_in_memory_fakes` now expects the four-name closure (**red first**: with the reference added and the allow-list not yet edited it fails, which is the argument the allow-list edit answers); `Core_suite_build_graph_holds_no_adapter` stays green (the kit brings no `Dexpace.Sdk.Http.*` or `Dexpace.Sdk.Serialization.*`). A new `The_kit_brings_no_transport_or_codec` pin names the two prefixes explicitly.
- The control that proves the driver can fail: a throwaway local mutation of `BoundedMap` capacity (never committed) makes `xcut-14.bounded-under-burst` fail on the core driver; restore.

**Production change.** None under `src/`. Lock file: `dotnet restore Dexpace.Sdk.sln` once; commit the one changed `tests/Dexpace.Sdk.Core.Tests/packages.lock.json`; V-restore (locked).

**Verify.** V-build; V-core `InvariantConformanceTests`, V-core `Seam2ArchitectureTests`; V-conf (core driver included; `Dexpace.Sdk.Core.Tests` joins the V-conf list here). **IDs.** all 28 assertions, `XCUT-8`, `-9`, `-14` (internal hooks), `SEAM-2` (the gate's allow-list). **Commit.** `test: core driver for the invariant suite; SEAM-2 allow-list admits the core-only kit`.

## Task 1.11 — The DI driver (`XCUT-23`, `XCUT-10`, `XCUT-22`; P10-5)

**Files.** New: `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Conformance/InvariantResolutionConformanceTests.cs` (`Conformance`). (The DI test project already references the kit.)

**Tests first.**
- `SeamResolution` over `AddDexpaceClient`: none registered fails with the install hint; two registered fail listing both; an explicit `UseTransport` wins (`SingleRegistrationTests`/`ProviderValidationTests` cover the package; this drives the kit's assertion over it). Each of the three delegates builds a fresh `ServiceCollection`, resolves with `ValidateOnBuild`, and disposes the provider with `DisposeAsync`.
- `The_suite_runs_over_the_DI_built_pipeline`: `CreatePipeline` replaced by the keyed `HttpPipeline` of `AddDexpaceClient("kit")` over a scripted transport (every client through the existing `TestClients.Add` helper, which supplies the stub `UseSerde`); the whole suite is green and the report written.
- `Only_the_four_hook_less_assertions_are_not_exercised`: `xcut-8.strict-factory-rejects`, `xcut-9.cycle-safe-walk`, `xcut-14.bounded-under-burst`, `xcut-14.drains-after-insert` (R13; P10-28).
- Control: a resolver choosing the last of two is `Failed` (also in task 1.9's controls); here, mutating the package's single-registration check (never committed) makes `xcut-23` fail on this driver; restore.

**Production change.** None. **Verify.** V-build; V-di `InvariantResolutionConformanceTests`; V-conf. **IDs.** `XCUT-23`, `XCUT-10`, `XCUT-22` (over the DI-built pipeline). **Commit.** `test: DI driver for the invariant suite (SeamResolution)`.

## Task 1.12 — The two `RS0030` bans (`XCUT-9`, `XCUT-14`; P10-18, R7)

**Files.** Edit: `BannedSymbols.txt` (two bans: the `GetBaseException` line and seven `ConcurrentDictionary` constructor lines, R7), `src/Dexpace.Sdk.Core/Internal/BoundedMap.cs` (**one** scoped `#pragma warning disable RS0030` / `restore` with a why-comment around line 49's `new ConcurrentDictionary<TKey, Slot>(comparer)`; the inner `TryRemove` pair at lines 178 to 180 is left alone), `tests/Dexpace.Sdk.Core.Tests/Architecture/BannedSymbolsSiteTests.cs`.

**Tests first.**
- `BannedSymbolsSiteTests.ConcurrentDictionary_is_constructed_only_in_BoundedMap` (a source scan for `new ConcurrentDictionary` and a target-typed `new` of that type: it passes already, a **pin**; prove it can fail by adding `new ConcurrentDictionary<int,int>()` to another `src/` file: the scan fails, and after the entries land the **build** fails with `RS0030` at that line, then revert).
- Red evidence for the ban itself: add the seven constructor lines **without** the pragma and V-build fails with `RS0030` at exactly `BoundedMap.cs` line 49 (verified on a scratch copy for the `(IEqualityComparer{`0})` overload, 2026-10-13: one error, nowhere else); then add the pragma and the build is green. The bare `M:…ConcurrentDictionary`2.#ctor` form matches only the parameterless overload, so it cannot stand alone.
- `BannedSymbolsSiteTests.GetBaseException_appears_nowhere_in_src` (pin, V14) and `The_scanner_can_fail` extended with both tokens.
- The build is the test of the ban: a new `src/` use of `ConcurrentDictionary<,>` outside `BoundedMap` or of `Exception.GetBaseException` is an `RS0030` error (V4's method).

**Production change.** Entries (header style of the file, with the reason RS0030 prints), one `M:System.Collections.Concurrent.ConcurrentDictionary`2.#ctor(…);<reason>` line per public constructor, the reason being `A process- or instance-lived concurrent map is a BoundedMap, or it does not land (XCUT-14, design §5.4, P10-18)`:
`#ctor` (the parameterless overload), `#ctor(System.Int32,System.Int32)`, `#ctor(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{`0,`1}})`, `#ctor(System.Collections.Generic.IEqualityComparer{`0})`, `#ctor(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{`0,`1}},System.Collections.Generic.IEqualityComparer{`0})`, `#ctor(System.Int32,System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{`0,`1}},System.Collections.Generic.IEqualityComparer{`0})` and `#ctor(System.Int32,System.Int32,System.Collections.Generic.IEqualityComparer{`0})`; and
`M:System.Exception.GetBaseException;Walks the inner-exception chain to its end with no cycle check; use ExceptionFacts.EnumerateCauses (XCUT-9, P10-18)`. The one pragma in `BoundedMap.cs` carries the why-comment "the one sanctioned construction of ConcurrentDictionary (XCUT-14, P10-18): a map outside this class must be a BoundedMap". Zero behaviour change: V-build and the full core suite stay green.

**Verify.** V-build (this is the gate); V-core `BannedSymbolsSiteTests`; `grep -rn "ConcurrentDictionary" src --include='*.cs'` shows only `BoundedMap.cs`. **IDs.** `XCUT-9`, `XCUT-14` (gates). **Commit.** `chore: RS0030 bans for Exception.GetBaseException and ConcurrentDictionary outside BoundedMap`.

## Task 1.13 — PR 1 gate

V-gate; V-conf (the three transport drivers, the raw-socket driver, the core driver and the DI driver all green; the transport drivers still carry exactly their pinned waivers); the invariant suite's `NotExercised` lists exactly R13's; `git diff --stat main...HEAD -- src/Dexpace.Sdk.Core` shows only `BoundedMap.cs` (one pragma pair, no behavioural line) and nothing in `PublicAPI.Unshipped.txt`; the only existing kit tests edited in PR 1 are the additions of 1.1 (`SuiteRunnerTests`, `ConformanceAssertionTests`) and `AssertionCatalogueTests.Every_control_names_an_assertion_that_exists` (1.3), and no existing test body besides that one rule changed; `rg "Task\.Delay|DateTime\.(Utc)?Now|Thread\.Sleep" src/Dexpace.Sdk.Conformance` empty. Hand the lead the branch name.

---

# PR 2 — The codec suite

Branch `99-phase-10-codec-suite` off PR 1. Evidence for B.3 (no row owner changes): the suite lifts `SERDE-1`..`-3`, `-9`, `-10`, `-12`, `-15`..`-17`, `-19`, `-20`, `SEAM-20` (11 assertions); `SERDE-4`, `-13`, `-14`, `-18`, `-30` judge core code, not the codec seam, and stay **by reference** to core's tests (validation F6: a negative control cannot be built for them through `CodecSubject.CreateSerde`; B.3.2, B.3.4 and B.3.5 are therefore `by reference` rows).

## Task 2.1 — `CodecSuite`, `CodecSubject`, the probe models (P10-8)

**Files.** New: `src/Dexpace.Sdk.Conformance/Subjects/CodecSubject.cs`, `src/Dexpace.Sdk.Conformance/Suite/CodecSuite.cs`, `src/Dexpace.Sdk.Conformance/Suite/CodecContext.cs`, `src/Dexpace.Sdk.Conformance/Assertions/CodecCatalogue.cs`, `src/Dexpace.Sdk.Conformance/Codec/ProbeModels.cs` (namespace `Dexpace.Sdk.Conformance.Codec`; one type per file in practice: `ProbeAddress`, `ProbePatch` (sealed records with `Tristate<string>`/`Tristate<int>` members whose declared default is `Tristate<T>.Absent`, a nested record, a `IReadOnlyList<string>`), `ProbeContract`-style names fixed at implementation and frozen), `tests/Dexpace.Sdk.Conformance.Tests/Suite/CodecSuiteTests.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Assertions/CodecCatalogueTests.cs`. Edit: `PublicAPI.Unshipped.txt`, `tests/Dexpace.Sdk.Conformance.Tests/Assertions/KitCatalogues.cs` (adds `CodecSuite.Assertions` to `All`, so the widened `Every_control_names_an_assertion_that_exists` of task 1.3 covers the codec controls).

**Tests first.** `CodecSuiteTests`: `Assertions` is the 11 names in order (staged like task 1.3 until 2.4); `RunAllAsync` over a throwing `CreateSerde` is `Errored`, not an exception; the preamble says what a green run does not prove ("the codec's own metadata for models it was not given; configuration it was not handed"); faces are `Async`/`Blocking` only (`ISerde.DeserializeAsync`/`Deserialize`) and `None` for pure-value assertions; null guards.
`CodecCatalogueTests`: names match the pattern, IDs exist, `Every_codec_assertion_has_a_negative_control`.

**Production change.** `public sealed record CodecSubject { string Name; Func<ISerde> CreateSerde; MediaType? ExpectedMediaType }`; `CodecSuite` mirrors `InvariantSuite` (`Preamble`, `Assertions`, `RunAsync`, `RunAllAsync`). The probe models' members are chosen so a System.Text.Json source-generated context needs one `[JsonSerializable]` line per type (design D). `ISerde` verified at `src/Dexpace.Sdk.Core/Serialization/ISerde.cs`; the typed helpers at `SerdeExtensions.cs`; `Tristate<T>` at `TristateOfT.cs`.

**Verify.** V-build; V-conf-tests `CodecSuiteTests`, `CodecCatalogueTests`. **IDs.** none (contract). **Commit.** `feat: codec suite skeleton, CodecSubject and probe models`.

## Task 2.2 — The round-trip, media-type and target assertions (`SERDE-1`, `-2`, `-3`, `SEAM-20`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/CodecRoundTripAssertions.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/CodecRoundTripControlTests.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Controls/BrokenSerdes.cs`. Edit: `CodecCatalogue.cs`.

**Tests first** (controls, `Failed`, both faces): `serde-1.round-trip` ← a codec that loses a field; `serde-2.declared-media-type` ← a codec declaring a media type the default `Content-Type` does not follow; `serde-3.target-not-closed` (cites `SERDE-3`, `SEAM-20`) ← a codec that disposes its target stream. (`SERDE-4`'s fixed-buffer encode is **not** a codec assertion: it is core's `SerdeExtensions.Serialize(ISerde, Span<byte>, T)`, which wraps `ISerde.Serialize(IBufferWriter<byte>, T)`; there is no codec property to break. It is `by reference` to `tests/Dexpace.Sdk.Core.Tests/Serialization/SerdeProfileTests.cs`.) Positive: a reference in-kit `ISerde` double passes all three (the STJ driver proves the real one in 2.5).

**Production change.** The assertions over `CodecSubject.CreateSerde` and the probe models. **Verify.** V-build; V-conf-tests `CodecRoundTripControlTests`. **IDs.** `SERDE-1`, `-2`, `-3`, `SEAM-20`. **Commit.** `feat: codec assertions serde-1 to serde-3 with negative controls`.

## Task 2.3 — The failure assertions (`SERDE-9`, `-10`, `-12`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/CodecFailureAssertions.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/CodecFailureControlTests.cs`. Edit: `CodecCatalogue.cs`, `BrokenSerdes.cs`.

**Tests first** (controls): `serde-9.no-library-type-escapes` ← a codec leaking `JsonException`; `serde-10.directional-subtypes` ← a codec raising a read failure as the write subtype; `serde-12.stream-error-unwrapped` ← a codec wrapping a genuine stream `IOException` in a `SerdeException`. (`SERDE-13`, a wire `null` into a non-null target naming the type, is the typed readers' check in core's `SerdeValues` after the codec has returned, so no codec can be made to fail it: `by reference` to `ReadValueTests.cs` and `SerdeValuesTests.cs`.) Positive: the in-kit double passes.

**Production change.** As the design rows; failures asserted as `SerdeException` subtypes chaining the cause (`SerdeException` verified under `src/Dexpace.Sdk.Core/Errors`). **Verify.** V-build; V-conf-tests `CodecFailureControlTests`. **IDs.** `SERDE-9`, `-10`, `-12`. **Commit.** `feat: codec assertions serde-9 to serde-12 with negative controls`.

## Task 2.4 — The `Tristate<T>` assertions (`SERDE-15`, `-16`, `-17`, `-19`, `-20`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/CodecTristateAssertions.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/CodecTristateControlTests.cs`. Edit: `CodecCatalogue.cs`, `BrokenSerdes.cs`, `CodecCatalogueTests.cs`/`CodecSuiteTests.cs` (staged pin tightened to equality of the 11).

**Tests first** (controls): `serde-15.tristate-emission` ← a codec writing `null` for an absent field; `serde-16.tristate-decode` ← `{}`/`{"x":null}`/`{"x":v}` decoding wrongly; `serde-17.missing-key-absent` ← a decoder defaulting a missing key to `Null`; `serde-19.default-wiring` ← a codec built with no Tristate wiring (the assertion uses `CreateSerde()` exactly as handed, with no extra configuration); `serde-20.top-level-degrades` ← a codec throwing for a top-level absent (graceful degrade: a wire `null` for both Absent and Null at top level and as an array element).
(`SERDE-14`, `-18` and `-30` are `Tristate<T>`'s own factories, `Present(null)` rejection and sentinel text, in core with no bypass to break: `by reference` to `TristateTests.cs` and `TristateSentinelTests.cs`.)
The 11 names, in order: `serde-1.round-trip`, `serde-2.declared-media-type`, `serde-3.target-not-closed`, `serde-9.no-library-type-escapes`, `serde-10.directional-subtypes`, `serde-12.stream-error-unwrapped`, `serde-15.tristate-emission`, `serde-16.tristate-decode`, `serde-17.missing-key-absent`, `serde-19.default-wiring`, `serde-20.top-level-degrades`. Levels: `serde-20` SHOULD, the rest MUST (each assertion's level never exceeds its strongest ID).

**Production change.** As the rows. **Verify.** V-build; V-conf-tests `CodecTristateControlTests`, `CodecCatalogueTests`. **IDs.** `SERDE-15`, `-16`, `-17`, `-19`, `-20`. **Commit.** `feat: codec assertions serde-15 to serde-20 with negative controls`.

## Task 2.5 — The STJ driver (P10-8)

**Files.** Edit: `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests.csproj` (a `ProjectReference` to the kit), its `packages.lock.json`. New: `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/Conformance/CodecConformanceTests.cs` (`Conformance`), `…/Conformance/ProbeContext.cs` (a `JsonSerializerContext` with a `[JsonSerializable]` line per probe model, default or `Metadata` generation mode: **not** fast-path-only, the CLAUDE.md rule).

**Tests first.** `Assertion_holds` (theory over `CodecSuite.Assertions` × faces) and `The_whole_suite_is_green_and_its_report_is_written` over `new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(ProbeContext.Default))`, `ExpectedMediaType` = `application/json`, **no waiver, no `NotExercised`** (the list is empty and pinned). The driver's failing evidence is the controls of 2.2 to 2.4; a local mutation (never committed) removing the Tristate wiring from the subject's options makes `serde-15` fail; restore.

**Production change.** None under `src/`. **Verify.** V-build; V-stj `CodecConformanceTests`; V-restore (the one changed lock file); V-conf (`Dexpace.Sdk.Serialization.SystemTextJson.Tests` joins the V-conf list here: its first `Conformance` class). **IDs.** `SERDE-1`..`-3`, `-9`, `-10`, `-12`, `-15`..`-17`, `-19`, `-20`, `SEAM-20`. **Commit.** `test: STJ driver for the codec suite`.

## Task 2.6 — PR 2 gate

V-gate; V-conf (the list now holds all five projects, STJ driver included); `git diff --stat main...HEAD -- 'src/*/PublicAPI.Unshipped.txt'` lists only the kit's; the kit's `KitDependencyArchitectureTests` green (no new reference). Hand the lead the branch name.

---

# PR 3 — The packaging suite and the NFR gates

Branch `99-phase-10-packaging-and-gates` off PR 2. Rows: `NFR-1`, `-2`, `-3`, `-4`, `-5`, `-6`, `-7`, `-8`, `-9`, `-10`, `-11`, `-12`, `-13`, `-14`, `-15`, `-17`.

## Task 3.1 — `PackagingSuite`, `PackagingSubject`, the BCL package readers (V6, P10-10)

**Files.** New: `src/Dexpace.Sdk.Conformance/Subjects/PackagingSubject.cs`, `PackagePolicy.cs`, `src/Dexpace.Sdk.Conformance/Suite/PackagingSuite.cs`, `src/Dexpace.Sdk.Conformance/Packaging/NupkgReader.cs` (`System.IO.Compression`), `NuspecReader.cs` (`System.Xml.Linq`), `AssemblyMetadataReader.cs` (`System.Reflection.Metadata`: `AssemblyMetadata`, `AssemblyInformationalVersion`, `InternalsVisibleTo`, `TargetFramework` attributes read from bytes), `src/Dexpace.Sdk.Conformance/Assertions/PackagingCatalogue.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Support/NupkgBuilder.cs` (synthetic `.nupkg`s built in memory: nuspec, an assembly emitted with `MetadataBuilder` carrying chosen attributes), `tests/Dexpace.Sdk.Conformance.Tests/Suite/PackagingSuiteTests.cs`, `tests/Dexpace.Sdk.Conformance.Tests/Packaging/NupkgReaderTests.cs`. Edit: `PublicAPI.Unshipped.txt`.

**Tests first.** `NupkgReaderTests`: reads the nuspec's id, version, dependency groups, license expression and copyright from a built package; reads the three assemblies' attributes from bytes (`IsTrimmable`, `IsAotCompatible`, informational version, `InternalsVisibleTo`, `TargetFrameworkAttribute`); a corrupt zip is a clear failure, not a crash. `PackagingSuiteTests`: the eight names in order (`nfr-1.core-dependencies`, `nfr-2.adapter-budget`, `nfr-3.no-internals-granted`, `nfr-8.trim-and-aot-declared`, `nfr-10.single-floor`, `nfr-13.license-declared`, `nfr-14.lockstep-version`, `nfr-15.version-resolvable`; staged until 3.4), `RunAll` over a missing path is `Errored` naming the file; the preamble reads "it reads the packages it was given; a package built from another tree is a different subject"; every assertion's face is `None`.

**Production change.** `PackagingSubject { IReadOnlyList<string> PackagePaths; string CoreId; IReadOnlyDictionary<string, PackagePolicy> Policies; string DeclaredFloor }`, `PackagePolicy(string? ThirdPartyRoot, IReadOnlyList<string> FirstPartyIds)`, `PackagingSuite.RunAll(PackagingSubject, SuiteOptions? = null)` returning a `ConformanceReport` synchronously (no async surface, nothing awaits). The readers are internal. No package is added (V6: all BCL).

**Verify.** V-build; V-conf-tests `NupkgReaderTests`, `PackagingSuiteTests`. **IDs.** none (contract). **Commit.** `feat: packaging suite skeleton and BCL package readers`.

## Task 3.2 — `nfr-1.core-dependencies` and `nfr-2.adapter-budget` (`NFR-1`, `NFR-2`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/DependencyPackagingAssertions.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/PackagingDependencyControlTests.cs`. Edit: `PackagingCatalogue.cs`.

**Tests first** (controls on synthetic packages, `Failed`): a core nuspec with an extra concrete dependency, with a second `net` group, or missing the facade (§10 entry 1: exactly `Microsoft.Extensions.Logging.Abstractions`) fails `nfr-1`; an adapter nuspec with a second third-party root, with a Dexpace dependency at the wrong lockstep version, or referencing a first-party id its policy does not name fails `nfr-2` (§10 entry 2: the hosting family counts as one root). Positive: a conforming synthetic set passes.

**Production change.** As the design rows; policies mirror `scripts/ci/dependency-audit.cs`'s record. **Verify.** V-build; V-conf-tests `PackagingDependencyControlTests`. **IDs.** `NFR-1`, `NFR-2`. **Commit.** `feat: packaging assertions nfr-1 and nfr-2 with negative controls`.

## Task 3.3 — `nfr-3`, `nfr-8`, `nfr-10` (`NFR-3`, `NFR-8`, `NFR-9`, `NFR-10`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/AssemblyPackagingAssertions.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/PackagingAssemblyControlTests.cs`. Edit: `PackagingCatalogue.cs`.

**Tests first** (controls): an assembly granting `InternalsVisibleTo("Other")` (anything but `<package id>.Tests`) fails `nfr-3`; an assembly lacking `IsTrimmable` or `IsAotCompatible` `AssemblyMetadata`, or carrying `False`, fails `nfr-8.trim-and-aot-declared` (cites `NFR-8`, `NFR-9`: §9.2's reading of the keep configuration; V6); two `lib/` folders, two dependency groups, or a `TargetFrameworkAttribute` unequal to the subject's `DeclaredFloor` (`net10.0`) fails `nfr-10.single-floor`. Positive: the synthetic conforming set passes.

**Production change.** As the design rows. **Verify.** V-build; V-conf-tests `PackagingAssemblyControlTests`. **IDs.** `NFR-3`, `NFR-8`, `NFR-9`, `NFR-10`. **Commit.** `feat: packaging assertions nfr-3, nfr-8 and nfr-10 with negative controls`.

## Task 3.4 — `nfr-13`, `nfr-14`, `nfr-15` (`NFR-13`, `NFR-14`, `NFR-15`)

**Files.** New: `src/Dexpace.Sdk.Conformance/Assertions/MetadataPackagingAssertions.cs`; `tests/Dexpace.Sdk.Conformance.Tests/Controls/PackagingMetadataControlTests.cs`. Edit: `PackagingCatalogue.cs`, `PackagingSuiteTests.cs` (staged pin to equality of the 8).

**Tests first** (controls): a package missing the license expression or with a copyright differing from its siblings fails `nfr-13.license-declared` (the package-level face; source headers are task 3.8); a package version, or an inter-package dependency version, unequal to the rest fails `nfr-14.lockstep-version`; an informational version of `unknown`, `0.0.0`, or unequal to the nuspec version (with `+` metadata stripped) fails `nfr-15.version-resolvable`. Positive: the synthetic set passes.

**Production change.** As the design rows. **Verify.** V-build; V-conf-tests `PackagingMetadataControlTests`, `PackagingSuiteTests`, `AssertionCatalogue`-style `Every_packaging_assertion_has_a_negative_control`. **IDs.** `NFR-13`, `NFR-14`, `NFR-15`. **Commit.** `feat: packaging assertions nfr-13 to nfr-15 with negative controls`.

## Task 3.5 — The packaging CI step, the file-based app and policy parity (P10-10, R12)

**Files.** New: `scripts/ci/packaging-conformance.cs` (header, `#:project ../../src/Dexpace.Sdk.Conformance/Dexpace.Sdk.Conformance.csproj`, `#:property RestorePackagesWithLockFile=false`, `#:property PublishAot=false`; usage `packaging-conformance.cs <package-dir>`; builds the subject from the `*.nupkg` in the directory with the same policies table, runs `PackagingSuite.RunAll`, prints the report, exits 0 green, 1 on a red report, 2 on a usage or input error), `tests/Dexpace.Sdk.Conformance.Tests/Gates/PackagingPolicyParityTests.cs` (`Unit`), **`tests/Dexpace.Sdk.Conformance.Tests/Gates/RepositoryRoot.cs`** (walks up to `Dexpace.Sdk.sln`; created here because the parity test needs it, and reused by `LicenseHeaderTests` (3.8), `AnalyzerWaiverTests` (3.9), `CiWorkflowTests` (3.10) and `AppendixBCoverageTests` (4.2); validation F13). Edit: `.github/workflows/ci.yml` (step **Packaging conformance** after **Dependency audit**, every OS row, `dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages`; the header comment's gate list).

**Tests first.** `PackagingPolicyParityTests`: parses the `policies` table of `scripts/ci/dependency-audit.cs` and of `scripts/ci/packaging-conformance.cs` (text, found through `RepositoryRoot`) and fails if they disagree on any library, third-party root or first-party list (negative control: a copy of the audit text with one `Projects` entry removed must produce a mismatch); `The_app_exits_non_zero_on_a_broken_package` is a manual command in the task's verify (a package with a second `lib/` folder), not an automated test (R9's reasoning: no test project for scripts).

**Production change.** As above. **Verify.** `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages`; `dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages` exits 0 and prints the report (all eight green over the five packages); then the negative: copy `artifacts/packages` to the scratch directory, edit one nuspec's dependency version, run again, exit 1 naming `nfr-14`; V-conf-tests `PackagingPolicyParityTests`. **IDs.** `NFR-1`, `-2`, `-3`, `-8`, `-9`, `-10`, `-13`, `-14`, `-15`. **Commit.** `ci: packaging conformance step over the packed packages`.

## Task 3.6 — `Nfr11PublicSurfaceArchitectureTests` (`NFR-11`; P10-11, V10)

**Files.** New: `tests/Dexpace.Sdk.Core.Tests/Architecture/Nfr11PublicSurfaceArchitectureTests.cs` (`Unit`).

**Tests first.** `Core_public_signatures_name_only_system_logging_and_core_types`: reflection over `typeof(Request).Assembly.GetExportedTypes()`, every public and protected member's parameter, return and generic-argument types, base types and interfaces (recursively through generic arguments, arrays, by-ref and nullable), each in a namespace under `System`, `Microsoft.Extensions.Logging` or `Dexpace.Sdk.Core`. `The_scan_can_fail`: a test-local type list containing `System.Net.Http.HttpRequestMessage` in a signature, and a type under `Newtonsoft.Json`, are flagged (the negative control; `System.Net.Http` is itself under `System`, so the control uses a **named allow-list of namespace prefixes with `System.Net.Http` excluded**: design V10 shows core names only `System.*` in-box types, and `System.Net.Http` appears only in adapters' surfaces; the plan therefore bans `System.Net.Http`, `System.Text.Json` and `Microsoft.Extensions.DependencyInjection` explicitly beside the allow-list, a stricter reading recorded in the checklist). Design §9.2 says "a test scanning `PublicAPI.Shipped.txt`"; that file is empty until phase 12, so the assembly is the subject (P10-11).

**Production change.** None. **Verify.** V-build; V-core `Nfr11PublicSurfaceArchitectureTests`; if the scan reports a core member (design V10 found none), it is a **stop** and a phase 11 routing, not a fix (P10-2). **IDs.** `NFR-11`. **Commit.** `test: NFR-11 public-signature scan over core's exported surface`.

## Task 3.7 — `NFR4ArchitectureTests` (`NFR-4` source half)

**Files.** New: `tests/Dexpace.Sdk.Core.Tests/Architecture/NFR4ArchitectureTests.cs` (`Unit`).

**Tests first.** `Every_library_has_both_public_api_files`: for each directory under `src/` (found by walking up to the solution), both `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` exist; `Shipped_is_empty_or_nullable_enable_only_until_phase_12`; `The_scan_can_fail` over a temp tree missing a file. V4's builds (a new public type, an `[Obsolete]` call) are the red evidence for `RS0016`/`RS0017` and `CS0618`, recorded in the checklist, not repeated as tests (they are build failures; V-build is the gate).

**Production change.** None. **Verify.** V-core `NFR4ArchitectureTests`. **IDs.** `NFR-4`, `NFR-6`, `NFR-3` (evidence). **Commit.** `test: NFR-4 public-API file presence over every library`.

## Task 3.8 — `LicenseHeaderTests` (`NFR-13`; P10-12, V11)

**Files.** New: `tests/Dexpace.Sdk.Conformance.Tests/Gates/LicenseHeaderTests.cs` (`Unit`); uses `Gates/RepositoryRoot.cs`, created in task 3.5.

**Tests first.** `Every_cs_file_starts_with_the_two_line_header`: every `*.cs` under `src/`, `tests/`, `tools/`, `scripts/` and `.claude/` outside `bin`/`obj` starts with the two-line block, an `<auto-generated>` block allowed first (`RequirementCatalog.g.cs`, V11); `The_scan_can_fail` (a temp file with no header, with a header on line 2, and an auto-generated file with the header after its block); a count assertion `Scanned_at_least_900_files` (V11: 956) guards a silently empty scan. Closes the blind spot of the three `scripts/ci/*.cs` apps (plus the ones added in this phase).

**Production change.** None. **Verify.** V-conf-tests `LicenseHeaderTests`. **IDs.** `NFR-13`. **Commit.** `test: NFR-13 license header over every C# file including scripts/ci`.

## Task 3.9 — `AnalyzerWaiverTests` and the `.editorconfig` re-enable conditions (`NFR-7`; P10-13, R6)

**Files.** Edit: `.editorconfig` (a `# Re-enable when: …` line in the comment block of each block of `severity = none` lines and of the test-scope `CA2000` pair, R6). New: `tests/Dexpace.Sdk.Conformance.Tests/Gates/AnalyzerWaiverTests.cs` (`Unit`).

**Tests first** (red until the `.editorconfig` edit lands: the test fails naming the five `none` blocks (`CA1308`; `CA1054`–`CA1056` as one; `CA1062`; `CA2007`; `CA1707`; seven lines in all) and the `CA2000` lines 155 and 156 as one block; line 52 passes through its named exemption).
- `Every_severity_none_has_a_rationale_and_a_re_enable_condition`: parses `.editorconfig`; consecutive setting lines form **one block** that shares the comment block above it (line 156's `excluded_type_names_with_derived_types` has no comment of its own and inherits the one above line 155; `CA1054`–`CA1056` are one block). For each block containing a `dotnet_diagnostic.*.severity = none` line or a `dotnet_code_quality.*` relaxing line, that comment block has a rationale and a line starting `# Re-enable when:`. **Exemption:** `dotnet_code_quality.CA2000.dispose_ownership_transfer_at_constructor` in library scope (line 52) is a named hand-off rule, not a relaxation (R6); the allow-list holds exactly that key. `The_exemption_is_exactly_one_key` pins it (a copy of `.editorconfig` with another library-scope `dotnet_code_quality.*` line and no condition fails).
- `Every_pragma_disable_under_src_has_a_why_comment`: every `#pragma warning disable` line under `src/` (43 today, R6) has a `//` comment on its line or the line above.
- `The_checks_can_fail` over temp inputs.

**Production change (config text only).** Conditions: `CA1308` "when a non-protocol string is lower-cased (HTTP tokens never)"; `CA1054`-`CA1056` "when the string-URL entry points are removed"; `CA1062` "if nullable annotations are turned off for any library"; `CA2007` and `CA1707` "never for `src/`"; the test-scope `CA2000` pair (lines 155 and 156, one block, one condition) "never for `src/`". Line 52 is untouched. `dotnet format --verify-no-changes` must stay clean.

**Verify.** V-build; V-fmt; V-conf-tests `AnalyzerWaiverTests`. **IDs.** `NFR-7`, `NFR-6`. **Commit.** `chore: re-enable conditions for every analyzer exemption, gated by AnalyzerWaiverTests`.

## Task 3.10 — `CiWorkflowTests` (`NFR-17` first half; P10-16, R8)

**Files.** New: `tests/Dexpace.Sdk.Conformance.Tests/Gates/CiWorkflowTests.cs` (`Unit`). Edit: `.github/workflows/ci.yml` (header comment only, if a gate-table gate has no step name to match).

**Tests first.** `Every_gate_of_the_gate_table_is_a_ci_step`: a table (in the test) of the gates that exist at PR 3 and the step-name or command substring that must appear in `ci.yml` (restore locked, build, format, test with coverlet, coverage floor and its self-test, pack, dependency audit, packaging conformance, knowledge structure, reproducible pack job, NativeAOT smoke job, tools build and test); `No_step_or_job_is_advisory` (no `continue-on-error`); `Only_the_documented_gates_are_conditional` (the documented conditionals are exactly `if: runner.os == 'Linux'` on the knowledge step and, from task 4.9, the requirement-coverage steps, **and `if: always()` on the "Upload test results" step** (ci.yml, after the knowledge step: it must run when a test step failed, so it is not a gate and not advisory); any other `if:` fails); `The_checks_can_fail` over a copied workflow text. The workflow is read as text (no YAML package: the test's rules are line-based and documented). **The second half of `NFR-17`** (the ruleset's `required_status_checks`) is an admin action: task 4.10 asks the lead and records the `gh api` read.

**Production change.** None. **Verify.** V-conf-tests `CiWorkflowTests`. **IDs.** `NFR-17`, `NFR-5`, `NFR-6`. **Commit.** `test: CI workflow completeness and no advisory gate (NFR-17)`.

## Task 3.11 — Cross-path reproducible pack (`NFR-12`; P10-14, V3, R15)

**Files.** Edit: `scripts/ci/reproducible-pack.sh` (header comment and body), `.github/workflows/ci.yml` (the reproducible job's comment).

**Tests first (a script has no test project, R9; the evidence is runs of one temporary flag, never committed).** The flag is `-p:DeterministicSourcePaths=false`, which makes the compiler embed absolute source paths, applied to **both** packs of a run (validation F8: on one pack only, the two packs differ even in one directory and the red proves nothing). (a) **Baseline pin**: the current same-directory script, unmodified, prints "byte-identical" on `HEAD`; and with the flag on both packs from the **same** directory it still passes (the same-path baseline must pass: the old script cannot see a path leak). (b) **Red**: with the new two-worktree script and the flag on both packs, the run **fails** naming the differing packages (the cross-path run must fail: it sees the leak). (c) **Green**: the new script with no flag passes. (Verified by the validator: same directory, both packs flagged, identical; two worktrees, both flagged, differ; two worktrees, no flag, identical.) Then remove the flag.

**Production change.** The two packs run from `git worktree add --detach <out>/wt1/repo HEAD` and `<out>/wt2/yy/zz/repo2 HEAD` (different depths, V3's shape), each `dotnet restore --locked-mode` + `dotnet pack … -p:ContinuousIntegrationBuild=true --output <out>/<run>`, worktrees removed on exit (`trap`), `SOURCE_DATE_EPOCH` as before, a warning when the working tree is dirty (R15). The compare loop is unchanged.

**Verify.** `scripts/ci/reproducible-pack.sh artifacts/reproducible` exits 0 over the five `.nupkg` (and `.snupkg` if present). **IDs.** `NFR-12` (build half; the release half stays phase 12). **Commit.** `ci: reproducible pack from two clones at different paths`.

## Task 3.12 — PR 3 gate

V-gate (including the new CI step run locally, the audit, the cross-path reproducible pack); V-conf; `PackagingPolicyParityTests` green; `git diff --stat main...HEAD -- 'src/*/PublicAPI.Unshipped.txt'` lists only the kit's. Hand the lead the branch name.

---

# PR 4 — Appendix B, coverage and close-out

Branch `99-phase-10-close-out` off PR 3. Rows closed: the 41 rows' evidence assembly; appendix B's 61 items.

## Task 4.1 — Widen `AppendixBMap` to all 61 items (P10-7, R5, V8)

**Files.** Edit: `src/Dexpace.Sdk.Conformance/Requirements/AppendixBMap.cs` (B.1 to B.9; the doc comment), `tests/Dexpace.Sdk.Conformance.Tests/Requirements/AppendixBMapTests.cs`.

**Tests first.** `AppendixBMapTests`: `The_map_equals_the_checklist_items_of_appendix_B` parses all nine sections (the section regex widens from `TRANSPORT|ASYNC` to the nineteen-prefix pattern), expects 61 items with counts 10, 6, 7, 8, 6, 5, 6, 6, 7, equal ID lists (**order-insensitive within an item**: B.1.6 lists `PAGE-20` before `PAGE-19`, V8; the existing test compares ordered, so it is changed to compare sets for every item), 276 distinct IDs, none in two items, every ID in the catalogue; `PAGE-4` and `PAGE-5` are in no item (a pin: the map's silence is stated, §11 item 9). The existing `The_drift_check_can_fail`, `An_item_is_as_bad_as_the_worst_requirement_it_covers`, `A_vacuous_requirement_among_passes…` and `Rendering_includes_the_appendix_B_table` stay green unedited. **Red first:** with the test widened and the map not, it fails on the 50 missing items.

**Production change.** `AppendixBMap.Items` gains the B.1 to B.5, B.8 and B.9 items (IDs from appendix B, ordered as the spec prints them); an `Ids(prefix, first, last)` helper is reused where ranges are contiguous, explicit lists otherwise (B.3.5 `SERDE-14..20, -30`; B.3.6 `-21..-26, -29`; B.1.6 `-16, -17, -18, -20, -19`). `ConformanceReport.ByAppendixBItem` now covers B.8 and B.9 for the combined report.

**Verify.** V-build; V-conf-tests `AppendixBMapTests`, `ConformanceReportTests`, `ConformanceReportCombineTests`. **IDs.** none (map). **Commit.** `feat: AppendixBMap covers all 61 appendix-B items`.

## Task 4.2 — `APPENDIX_B.md` and `AppendixBCoverageTests` (P10-19, R10, P10-9)

**Files.** New: `tests/Dexpace.Sdk.Conformance.Tests/APPENDIX_B.md`, `tests/Dexpace.Sdk.Conformance.Tests/Requirements/AppendixBCoverageTests.cs`. Edit: `tests/Dexpace.Sdk.Conformance.Tests/Dexpace.Sdk.Conformance.Tests.csproj` (`<None Include="APPENDIX_B.md" Link="spec\APPENDIX_B.md" CopyToOutputDirectory="PreserveNewest" />` beside the existing spec copies).

**Tests first** (`AppendixBCoverageTests`, `Unit`; red until the file exists, then red per rule until it is right). The five rules of P10-19, each with a negative control built from the parsed map with one defect (a row removed, one ID too many, a dangling path, a `suite` row naming an assertion that cites none of its IDs):
1. exactly 61 rows with per-section counts 10, 6, 7, 8, 6, 5, 6, 6, 7 against the spec copy `spec/appendix-b.md`;
2. each row's ID set equals the set parsed from that item's text (nineteen-prefix pattern; 276 distinct IDs, none in two rows);
3. every status is one of `suite`, `by reference`, `restated`, `scoped out`, and every `by reference` path exists under the repository root (`RepositoryRoot`, task 3.5) and every assertion reference exists in a kit catalogue (R10, wildcard form matching at least one name);
4. every `suite` row names at least one assertion that exists in a kit catalogue **and cites at least one of the row's IDs**;
5. `AppendixBMap` equals the map's ID column.
Plus `The_map_opens_with_the_two_limits` (the file's preamble names that appendix B covers nine of nineteen prefixes and omits `PAGE-4`/`PAGE-5`, and that a `by reference` row proves a claim and a file, never the behaviour) and `The_tally_is_17_8_36_0`.

**Production change.** The hand-written file, `| Section | Item | IDs | Evidence | Status | Notes |`, one row per appendix-B item in specification order, from the design's planned map **with the evidence paths resolved to the tree** (all existing files were verified: every `tests/…` file in the design's map exists at the directory shown there, e.g. `tests/Dexpace.Sdk.Core.Tests/Pagination/PageableTests.cs`, `…/ServerSentEvents/ServerSentEventParserTests.cs`, `…/Pipeline/Policies/InstrumentationPolicyTests.cs`, `…/Client/AsyncToSyncBridgeTests.cs`, `tests/Dexpace.Sdk.Http.SystemNet.Tests/HeaderDropLogTests.cs`, `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/ConfigurationPrecedenceTests.cs`; the design's `…/` shorthand is expanded and the item's own named assertion form `codec:`/`invariant:`/`packaging:`/`transport:` used for suite rows). Status per the design: 17 `suite` (B.3.1, B.6.1 to B.6.5, B.7.1, B.7.5, B.7.6, B.8.1 to B.8.6, B.9.1, B.9.5), 8 `restated` (B.3.3, B.4.1, B.4.5, B.5.1, B.5.2, B.5.3, B.7.2, B.7.3), 36 `by reference` (including B.3.2, B.3.4 and B.3.5: their `SERDE-4`, `-13`, `-14`, `-18`, `-30` are core code, F6; their Evidence cells carry the `codec:` references **and** the core test paths), 0 `scoped out`. Notes carry every vacuous, declined or deferred clause with its §10/§11/P10 citation (`PAGE-3`, `PAGE-29`/`-30`, `SSE-41`, `CFG-10`/`-12`/`-13`, `ASYNC-2`'s scheduler clause, `ASYNC-4`, `ASYNC-16`, `ASYNC-21`, `XCUT-4`, `XCUT-8`, `XCUT-13`, `XCUT-23`, `XCUT-24`'s snapshot clause, `NFR-4`'s baseline, `NFR-12`'s release half, `NFR-16`). B.1's 10 rows are `by reference` (7c's vectors are not lifted, P10-9). B.9.7 evidence includes `scripts/ci/reproducible-pack.sh`, `tests/Dexpace.Sdk.Conformance.Tests/Gates/LicenseHeaderTests.cs`, `packaging:nfr-14.*`, `packaging:nfr-15.*`.

**Verify.** V-build; V-conf-tests `AppendixBCoverageTests`. **IDs.** all 61 items (the map is the evidence for every item). **Commit.** `test: APPENDIX_B.md and its five-rule coverage gate`.

## Task 4.3 — `scripts/ci/requirement-coverage.cs` and its self-test (P10-20, V7, R9)

**Files.** New: `scripts/ci/requirement-coverage.cs`, `scripts/ci/requirement-coverage-selftest.sh` (both with the license header; the script is a file-based app beside `requirement-catalog.cs`, `#:property RestorePackagesWithLockFile=false`, `#:property PublishAot=false`).

**Tests first (the self-test is written before the script and fails with "script not found").** The self-test builds fixture trees under `mktemp -d` (a mini appendix C and a few checklists) and runs `dotnet run scripts/ci/requirement-coverage.cs -- --check --root <dir>`, expecting: a clean fixture → exit 0; an appendix-C ID with no owning row → exit 1; an owning row naming an unknown ID → exit 1; a level cell disagreeing with appendix C → exit 1; an owning row with no legend mark in its status cells → exit 1; a carried ID (two checklists) whose earlier row lacks ⏳ or a split → exit 1, and whose earlier row carries it → exit 0; a missing appendix → exit 2; `--report` always exits 0 over a broken fixture; the real tree reports exactly the 41 unowned IDs `XCUT-1`..`XCUT-24`, `NFR-1`..`NFR-17` (**before** the phase 10 checklist exists: V7, expected until task 4.6) and 645 owned after it.

**Production change.** Per P10-20: input appendix C (same row pattern as `requirement-catalog.cs`) and every `docs/work/**/*-checklist.md`; an owning row is a row whose first cell is a requirement ID in a table whose header starts `| ID | Level |` (other headers are contributed or carried evidence); carried IDs resolved by phase order from the checklist's `phaseN[x]` directory (the latest row's mark wins; every earlier row must carry ⏳ or a split naming the later phase); four finding kinds; the legend mark rule (✅ 🚫 ⏳ N/A, "vacuous" counted as N/A); output a tally per prefix, mark and level; `--report` (exit 0), `--check` (exit 1 on any finding in a MUST row, 2 on input errors), `--root`.

**Verify.** `scripts/ci/requirement-coverage-selftest.sh` exits 0; `dotnet run scripts/ci/requirement-coverage.cs -- --report` prints the V7 numbers (604 of 645 owned today; the 41 listed). **IDs.** the whole appendix C (the gate behind `docs/first-release.md`'s "every MUST dispositioned"). **Commit.** `ci: requirement-coverage script over appendix C and every checklist`.

## Task 4.4 — NativeAOT smoke: `CheckPhase10Async` (`NFR-8`, `NFR-9`, `NFR-15`; P10-17, V16)

**Files.** Edit: `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (call `CheckPhase10Async` after `CheckPhase9Async` in `RunAllAsync`), `tests/Dexpace.Sdk.AotSmoke/Dexpace.Sdk.AotSmoke.csproj` (header comment: phase 10 completes the smoke; project references are kept), `.github/workflows/ci.yml` (header comment past tense). New: `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.Phase10.cs`, `tests/Dexpace.Sdk.AotSmoke/SmokeProbeContext.cs` (a source-generated `JsonSerializerContext` over the kit's probe models).

**Tests first (the check is the test).** `CheckPhase10Async`: `InvariantSuite.RunAllAsync` over `new InvariantSubject()` is green with exactly R13's pinned `NotExercised` list (the five hook-less assertions: `xcut-8.strict-factory-rejects`, `xcut-9.cycle-safe-walk`, `xcut-14.bounded-under-burst`, `xcut-14.drains-after-insert`, `xcut-23.resolution-deterministic`); `CodecSuite.RunAllAsync` over `SystemTextJsonSerde` and `SmokeProbeContext` is green with no `NotExercised`; `BuildInfo.SdkVersion` equals the assembly's `AssemblyInformationalVersion` with `+` metadata stripped (`NFR-15`'s runtime check; the smoke previously checked the `User-Agent` prefix and `RuntimeVersion` only, V16). Existing checks (`CheckPhase7aSerdeAsync`, `CheckPhase9Async`, the 8a slice) stay. Red: the first publish after adding the check with the expected list one short (a deliberately wrong pin, never committed) exits non-zero; restore.
**Risk the design did not measure:** the kit's suites may use a construct NativeAOT/trim rejects (the kit is `IsAotCompatible`, so the build is warning-clean, but run-time behaviour is unmeasured); if a suite assertion fails only under AOT it is a **stop**: record it, fix the kit if the defect is the kit's (P10-2's exception) and rerun.

**Production change.** None under `src/`. **Verify.** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` exits 0, prints `aot-smoke: all checks passed` and no trim or AOT warning (they are errors). **IDs.** `NFR-8`, `NFR-9`, `NFR-15`. **Commit.** `test: AOT smoke runs the invariant and codec suites (phase 10)`.

## Task 4.5 — Documentation: `cross-cutting-invariants.md`, `conformance.md`, READMEs (P10-27)

**Files.** New: `docs/sdk-documentation/cross-cutting-invariants.md`. Edit: `docs/sdk-documentation/conformance.md` (the three suites, their subjects, hooks, catalogues and preambles; how to run each against a third-party composition; the packaging CI step), `src/Dexpace.Sdk.Conformance/README.md` (the four suites), `docs/README.md` (the `sdk-documentation/` row gains the page, "phase 10"), `tests/Dexpace.Sdk.Conformance.Tests/Docs/ConformancePageTests.cs` (the catalogue tables match all four suites; the documented sample compiles in spirit against the shipped signatures).

**Tests first.** `ConformancePageTests` widened first (red: the page lacks the three tables): every assertion name of all four catalogues appears in the page's tables with its IDs, level and faces; the invariant, codec and packaging preambles equal the code's; the documented driver sample equals a driver file read through the existing `spec/` copy mechanism (add `InvariantConformanceTests.cs` as a `.txt` copy, as `RawSocketConformanceTests.cs` is).

**Content** (house style of `conformance.md`, `retry.md`; each section cites rows and rulings): where each `XCUT` row is enforced (the 24-row table with the existing evidence and the suite assertion), the NFR gate table (the design's, 28 lines), the hook model and `NotExercised` rule, "how to run the suites against your own composition", what a green run does not prove, the cross-path reproducibility precondition (a git checkout, V3) and the declined optional gates with their post-release triggers.

**Verify.** `dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations`; V-conf-tests `ConformancePageTests`; `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` (the README is at the package root); the audit and packaging steps once more. **IDs.** all rows (user-facing statement). **Commit.** `docs: cross-cutting-invariants.md and the conformance page for the new suites`.

## Task 4.6 — The checklist

Write `docs/work/mvp/phase10/2026-10-13-phase10-cross-cutting-invariants-checklist.md` **directly at its filed path** (create the `phase10/` directory; validation F4: `requirement-coverage.cs` reads `docs/work/**/*-checklist.md` only, so a checklist drafted in `docs/superpowers/` would leave the check at 604 of 645 in tasks 4.6 and 4.9; the design and plan are the documents task 4.10 files, the checklist is never in the inbox). Until 4.10 files the design and plan, the checklist's links to them point at their filed paths and the probe's links check reports exactly those; that is expected, not drift. Write it from what was built, following `docs/work/mvp/phase9/2026-10-12-phase9-host-integration-checklist.md`'s shape: header (branches, issue #99, the four PRs), the legend, **red evidence** (the first compile of each new control class; every control is the red evidence of its assertion; the pins proven able to fail by mutation: the core driver with `BoundedMap` capacity changed, the DI driver with the single-registration check mutated, the STJ driver with the Tristate wiring removed, the cross-path pack with a path-dependent flag, the `RS0030` entries with a new `ConcurrentDictionary`, V4's `RS0016` and `CS0618` builds), the **41-row requirement table** with the header **`| ID | Level | … |`** (**exactly the owning-table header, or the coverage script will not count the rows**), one row per ID with the test class or assertion, `[Trait]` and file, statuses from [the checklist section](#checklist-one-row-per-owned-id) corrected to what was observed;
a **second table** (header not `| ID | Level |`) of contributed evidence and **every hand-off of the design's table** (4c, 4a, 5b, 5c, 6a, 6b, 6c, 7a, 7b, 7c declined, 8a, 8b, 9, phase 0, the `ASYNC-2` non-take); a **third table**, the gate table (28 rows: the design's, with the measured state after this phase); the `Security`-class statement (none edited, none added); the deviations from the plan; the totals (planned 39 ✅ with 10 split, `NFR-16` ⏳, `NFR-17` ✅ or ⏳ per task 4.10, 0 🚫, 0 N/A); the findings (P10-23, P10-24, P10-25) and the unmet-MUST count statement (unchanged; the `XCUT-4` I/O-family question for phase 11); the styleguide audit groups (the design names none new beyond tooling-and-quality-gates 11.8, recorded as a conflict note). Split rows state their clause: `XCUT-1`, `XCUT-13` (interrupt flag, §10 entry 8), `XCUT-4` (I/O family, entry 7, §11 item 23), `XCUT-23` (discovery, entry 9, §11 item 8), `XCUT-24` (error snapshot declined, P10-23, a SHOULD clause), `XCUT-8` and `XCUT-20` (readings P10-24, §11 item 37), `NFR-1` (entry 1), `NFR-4` and `NFR-12` (phase 12 halves).
After writing it run `dotnet run scripts/ci/requirement-coverage.cs -- --check`: it must report **645 of 645 owned, every MUST with a legend mark** (it can, because the file is already under `docs/work/`). **Commit.** `docs: phase 10 checklist`.

## Task 4.7 — `CHANGELOG.md`

Under `[Unreleased]`, in the file's existing structure (read 8b's and 9's entries and match them): an `### Added` block headed `XCUT-1..XCUT-24, NFR-1..NFR-17` listing the invariant suite (28 assertions, `InvariantSubject`, the hooks), the codec suite (11 assertions, the probe models), the packaging suite (8 assertions, `scripts/ci/packaging-conformance.cs`), `SuiteOptions`, `ConformanceReport.Combine`, `AppendixBMap` at 61 items with `APPENDIX_B.md` and its gate, the `NFR-11` signature scan, `NFR4ArchitectureTests`, `LicenseHeaderTests`, `AnalyzerWaiverTests` and the `.editorconfig` re-enable conditions, `CiWorkflowTests`, the cross-path reproducible pack, `scripts/ci/requirement-coverage.cs`, the two `RS0030` bans, the completed AOT smoke, the three drivers; and a `### Changed` line: `TransportFace` gains `None` (additive; an exhaustive `switch` in a third-party driver must handle it), the `SEAM-2` allow-list admits the core-only kit, and the reproducible-pack job packs from two clones. **No `<b>Breaking:</b>` mark** (the kit is unpublished). **Commit.** `docs: changelog for phase 10`.

## Task 4.8 — Design, roadmap, register, checklists, `CLAUDE.md` and knowledge corrections ("Corrections owed at close-out")

Each a dated correction (the merge date), naming its ruling; never rewrite an earlier verdict, append. The frozen trees (`docs/product-spec/`, `docs/sdk-design-dotnet/`) take a correction **only by a human's deliberate edit**: this task prepares the text and applies it only if the lead says so; otherwise it lists the corrections in the checklist as proposed.
1. **Design (`docs/sdk-design-dotnet/`)**: §9 gate table's `NFR-11` row (the scan reads the assembly, P10-11); §9.1 (the banned list gains `Exception.GetBaseException` and `ConcurrentDictionary<,>`, P10-18); §9.2 (`NFR-9`'s rule-file clause is the packed assembly's `AssemblyMetadata`, V6; the packaging suite beside the audit, P10-10; the reproducible-pack job is cross-path, V3); §9.3 (Stryker.NET not scheduled, V5, P10-21; `benchmarks/` created by the first performance-motivated change, P10-22; the Appendix B paragraph: B.5 is exercised against the DI package's reader and the map is `APPENDIX_B.md` with its gate, P10-19; the kit has four suites); §10 entry 7 (a note that `ServiceResponseException` is never raised, P10-25); §11 (one new item: `XCUT-8`'s reading, P10-24; re-read the last number on the merged file first; the `NFR-11` scan's subject is part of the §9 correction); §12 (`XCUT` and `NFR` rows' notes).
2. **Roadmap** (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`): the Phase List row 10 links the plan and checklist beside the design (the design link itself is added when this plan lands); the Phase 10 card's starting counts are superseded by the measured ones (a dated correction); an append-only dated **Phase Status Note** (10 built: 41 rows, 39 ✅ (10 split) / `NFR-16` ⏳ / `NFR-17` per the ruleset; three suites in the kit; findings routed; rulings open for the lead taken by default: P10-1, 2, 5, 7, 8, 9, 16, 17, 21, 22, 23, 24, 25, 27); the inbound list for phase 11 gains the design's bullets (`ServiceResponseException`, the `XCUT-24` snapshot repair candidate, the unmet-clause count question for `XCUT-4`, `XCUT-8`'s reading, F-A2 unchanged).
3. **`docs/first-release.md`**: *SHOULD declined* (`XCUT-24`'s error-snapshot clause above 1 MiB); *Post-release triggers* (Stryker.NET supports Microsoft.Testing.Platform; the first performance-motivated change creates `benchmarks/`); *Blockers* ("Every MUST dispositioned" is ticked by the coverage script's `--check`; the `NFR-17` ruleset line **if task 4.10's change is not made**); *Packages* (the kit's row says phase 10 completed it); the one-time public-surface choices gain the `HttpResponseException` constructor guard and `ServiceResponseException`.
4. **Prior checklists** (dated corrections): phase 9's `XCUT-10` row (⏳ 10 → the phase 10 row); 4c, 5b, 5c, 6a, 6b, 6c, 7b, 8a, 8b "rows owned elsewhere" rows gain "taken by phase 10".
5. **`CLAUDE.md`**: the layout's `Dexpace.Sdk.Conformance` line names the four suites; the Build and test block gains `dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages` and `dotnet run scripts/ci/requirement-coverage.cs -- --check`; "What is genuinely unbuilt" gains phase 10's summary (keep other phases' words) and `docs/sdk-documentation/cross-cutting-invariants.md`; "Things that will bite you" gains **one bullet** (an invariant hook left `null` is `NotExercised`, and a driver pins its expected list; a new `ConcurrentDictionary` or `GetBaseException` in `src/` fails `RS0030`); the `scripts/ci` layout line lists the two new scripts and the self-test; the test layout gains the `Gates/` folder. Root `README.md` unchanged unless it counts suites.
6. **Knowledge**: append to `docs/knowledge/notes/testing.md` (styleguide 11.8's optional mutation gate against the MTP runner, V5, as a conflict note, constraint 6; read how existing conflict notes are written first) and `docs/knowledge/notes/tooling-and-quality-gates.md` (V3's cross-path fact and the git-checkout precondition; V9's ruleset fact). Run `scripts/knowledge verify-structure`.
**Commit.** `docs: phase 10 corrections, roadmap note and CLAUDE.md`.

## Task 4.9 — Turn on the coverage step (P10-20)

**Depends on 4.3 (the script) and 4.6 (the checklist at `docs/work/mvp/phase10/`, so the check reads 645 of 645 and the step is green on the commit that adds it).**

**Files.** Edit: `.github/workflows/ci.yml` (on the Linux row, after **Knowledge corpus structure**: **Requirement coverage self-test** `scripts/ci/requirement-coverage-selftest.sh` and **Requirement coverage** `dotnet run scripts/ci/requirement-coverage.cs -- --check`; the header comment's "phase 10 completes it" and "NFR-11's public-signature type scan: phase 10" lines become past tense), `tests/Dexpace.Sdk.Conformance.Tests/Gates/CiWorkflowTests.cs` (the last gate row and the second documented conditional, R8).

**Tests first.** `CiWorkflowTests` gains the row (red: the step is not yet in the file); then the step. **Verify.** V-conf-tests `CiWorkflowTests`; `dotnet run scripts/ci/requirement-coverage.cs -- --check` exits 0 (645 of 645). **IDs.** `NFR-17`, the coverage gate. **Commit.** `ci: requirement-coverage check blocks every pull request`.

## Task 4.10 — File the phase documents, run the probe, the ruleset request, the final gate

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 10            # dry run: the design is in docs/superpowers/specs/, this plan in docs/superpowers/plans/, the checklist in docs/work/mvp/phase10/
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 10 --write
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
scripts/knowledge verify-structure
```

Fix what the last probe reports (misattributed IDs, broken links, stale counts in `CLAUDE.md`/`README.md`; the roadmap row's design link follows the file to `docs/work/mvp/phase10/`). Then the whole CI list from `CLAUDE.md` plus this phase's steps, locally, in order:

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode && dotnet restore tools/Dexpace.Tools.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"     # and the SystemNet and DI projects
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Conformance"  # and SystemNet, DI, Conformance.Tests, STJ
dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80
scripts/ci/coverage-gate-selftest.sh artifacts/test-results
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet run scripts/ci/requirement-coverage.cs -- --check && scripts/ci/requirement-coverage-selftest.sh
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet build tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
```

**The ruleset (P10-16, open for the lead; the plan authorises no remote write).** Ask the lead to add a `required_status_checks` rule to the `main` ruleset naming every job (`build & test (ubuntu-latest)`, `build & test (windows-latest)`, `build & test (macos-latest)`, `reproducible packages`, `NativeAOT smoke`). After the lead confirms, the **read-only** `gh api repos/dexpace/dotnet-sdk/rulesets` (and the ruleset's id) is recorded in the checklist's `NFR-17` row. If it is not changed by close-out, `NFR-17` is ⏳ with the `docs/first-release.md` blocker of task 4.8.

Exit check list: the three suites green on their drivers with exactly the pinned `NotExercised` lists (core: `xcut-23`; DI: four; STJ: none; AOT: five); no `Owner =` waiver anywhere in the new drivers; `git diff --stat main...HEAD -- 'src/*/PublicAPI.Unshipped.txt'` lists only the kit's; the Security diff check of convention 5 empty; `*packages.lock.json` diff is exactly `tests/Dexpace.Sdk.Core.Tests` and `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests` and nothing else; `Directory.Packages.props` unchanged; the coverage report present for every project. **No push and no PR:** those are the lead's actions; PR descriptions name the checklist and carry `Closes #99` on PR 4 only, with no attribution.

---

## Checklist: one row per owned ID

Exactly 41 rows. **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 4.6 records what was built. Levels per appendix C (26 MUST: `XCUT` 22 + `NFR` 4; 15 SHOULD).

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `XCUT-1` | MUST | 1 | 1.4, 1.10 | **Split**: ✅ cancellation; interrupt flag cooperative (§10 entry 8) | `invariant:xcut-1.cancel-is-terminal`; `transport:transport-3.cancel-is-terminal` |
| `XCUT-2` | MUST | 1 | 1.4, 1.10 | ✅ | `invariant:xcut-2.timeout-is-retryable`; `transport-4` |
| `XCUT-3` | MUST | 1 | 1.4, 1.10 | ✅ | `invariant:xcut-3.wait-promptly-cancellable` |
| `XCUT-4` | MUST | 1 | 1.5, 1.10 | **Split**: ✅ two branches; I/O-family clause judged (§10 entry 7, §11 item 23) | `invariant:xcut-4.two-branches`; `transport-20` |
| `XCUT-5` | MUST | 1 | 1.5, 1.10 | ✅ | `invariant:xcut-5.one-classifier` |
| `XCUT-6` | MUST | 1 | 1.5, 1.10 | ✅ | `invariant:xcut-6.capability-queried` |
| `XCUT-7` | MUST | 1 | 1.5, 1.10 | ✅ | `invariant:xcut-7.configured-set-authoritative` |
| `XCUT-8` | MUST | 1 | 1.5, 1.10 | ✅ on the reading P10-24 | `invariant:xcut-8.strict-factory-rejects`, `xcut-8.convenience-returns-response` |
| `XCUT-9` | MUST | 1 | 1.6, 1.10, 1.12 | ✅ | `invariant:xcut-9.cycle-safe-walk`; the `GetBaseException` ban |
| `XCUT-10` | MUST | 1 | 1.6, 1.11 | ✅ | `invariant:xcut-10.safety-gate-uniform` on core and DI drivers |
| `XCUT-11` | MUST | 1 | 1.6, 1.10 | ✅ | `invariant:xcut-11.shared-no-crosstalk` |
| `XCUT-12` | SHOULD | 1 | 1.6, 1.10 | ✅ | `invariant:xcut-12.single-flight-scoped` |
| `XCUT-13` | MUST | 1 | 1.7, 1.10 | **Split**: ✅ latched, non-blocking; flag clause vacuous (§10 entry 8, §3.7) | `invariant:xcut-13.dispose-latched`, `xcut-13.dispose-non-blocking`; `transport-16` by reference (cites `TRANSPORT-16`, `ASYNC-15`) |
| `XCUT-14` | MUST | 1 | 1.7, 1.10, 1.12 | ✅ | `invariant:xcut-14.*` (core driver); the `ConcurrentDictionary` ban |
| `XCUT-15` | MUST | 1 | 1.7, 1.10 | ✅ | `invariant:xcut-15.no-external-alias` |
| `XCUT-16` | MUST | 1 | 1.8, 1.10 | ✅ | `invariant:xcut-16.no-credential-over-http` |
| `XCUT-17` | MUST | 1 | 1.8, 1.10 | ✅ | `invariant:xcut-17.redirect-hygiene` |
| `XCUT-18` | MUST | 1 | 1.8, 1.10 | ✅ | `invariant:xcut-18.model-validation`; the transport outbound assertions as evidence **by reference** (they cite `TRANSPORT-1x`, not `XCUT-18`) |
| `XCUT-19` | MUST | 1 | 1.8, 1.10 | ✅ | `invariant:xcut-19.redaction-default-deny` |
| `XCUT-20` | MUST | 1 | 1.9, 1.10 | ✅ on §11 item 37 | `invariant:xcut-20.observability-never-throws` |
| `XCUT-21` | MUST | 1 | 1.9, 1.10 | ✅ | `invariant:xcut-21.cnonce-unpredictable` (format, no-repeat); the CSPRNG source by reference: `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator` (R17) |
| `XCUT-22` | MUST | 1 | 1.9, 1.11 | ✅ | `invariant:xcut-22.core-borrows`; `transport-15` |
| `XCUT-23` | MUST | 1 | 1.9, 1.11 | **Split**: ✅ DI half; discovery vacuous (§10 entry 9, §11 item 8) | `invariant:xcut-23.resolution-deterministic` (DI driver) |
| `XCUT-24` | SHOULD | 1 | 1.9, 1.10 | **Split**: ✅ capped and body-log non-consuming; 🚫 error snapshot above 1 MiB (P10-23) | `invariant:xcut-24.preview-capped-non-consuming` |
| `NFR-1` | MUST | 3 | 3.2, 3.5 | **Split**: ✅ zero concrete deps; compile-only facade (§10 entry 1) | `packaging:nfr-1.core-dependencies`; `dependency-audit.cs` |
| `NFR-2` | SHOULD | 3 | 3.2, 3.5 | ✅ (§10 entry 2) | `packaging:nfr-2.adapter-budget`; `PackagingPolicyParityTests` |
| `NFR-3` | SHOULD | 3 | 3.3, 3.5 | ✅ | `packaging:nfr-3.no-internals-granted`; `CS1591` |
| `NFR-4` | SHOULD | 3 | 3.7 | **Split**: ✅ snapshot and drift gate; baseline ⏳ 12 | `NFR4ArchitectureTests`; `RS0016`/`RS0017` |
| `NFR-5` | SHOULD | 3 | 3.10 | ✅ | `coverage-gate.cs` (97.60%, V1); `CiWorkflowTests` |
| `NFR-6` | SHOULD | 3 | 3.7, 3.9, 3.10 | ✅ | `TreatWarningsAsErrors`; V4 |
| `NFR-7` | SHOULD | 3 | 3.9 | ✅ once the conditions land | `AnalyzerWaiverTests` |
| `NFR-8` | MUST | 3, 4 | 3.3, 4.4 | ✅ | `packaging:nfr-8.trim-and-aot-declared`; the AOT smoke |
| `NFR-9` | SHOULD | 3, 4 | 3.3, 4.4 | ✅ once `CheckPhase10Async` lands (R16) | the same; `CheckPhase10Async` |
| `NFR-10` | MUST | 3 | 3.3 | ✅ | `packaging:nfr-10.single-floor` |
| `NFR-11` | SHOULD | 1, 3 | 1.9, 3.6 | ✅ once the scan lands | `Nfr11PublicSurfaceArchitectureTests`; `invariant:nfr-11.scheduler-agnostic` |
| `NFR-12` | SHOULD | 3 | 3.11 | **Split**: ✅ build half (cross-path); release half ⏳ 12 | `reproducible-pack.sh` cross-path |
| `NFR-13` | SHOULD | 3 | 3.4, 3.8 | ✅ | `LicenseHeaderTests`; `packaging:nfr-13.license-declared`; `IDE0073` |
| `NFR-14` | SHOULD | 3 | 3.4, 3.5 | ✅ | `packaging:nfr-14.lockstep-version` |
| `NFR-15` | SHOULD | 3, 4 | 3.4, 4.4 | ✅ | `packaging:nfr-15.version-resolvable`; the smoke's `BuildInfo.SdkVersion` check |
| `NFR-16` | SHOULD | 4 | 4.6, 4.8 | ⏳ `docs/first-release.md` *Blockers* (phase 12) | (no release path) |
| `NFR-17` | MUST | 3, 4 | 3.10, 4.9, 4.10 | ✅ conditional on the ruleset; ⏳ blocker otherwise | `CiWorkflowTests`; the ruleset read |

Count: **41 rows**, all mapped (26 MUST, 15 SHOULD, from the levels above and appendix C). By exit: 39 ✅ (of which 10 split: `XCUT-1`, `-4`, `-13`, `-23`, `-24`, `NFR-1`, `-4`, `-12`, plus `XCUT-8` and `XCUT-20` on their readings; `NFR-17` conditional), 1 ⏳ (`NFR-16`), 0 🚫 rows, 0 N/A rows.
(`XCUT-24`'s 🚫 is a clause of a split row, a SHOULD clause, not the MUST count.)

### Work on rows owned elsewhere (no phase 10 checklist row) and the non-row hand-offs

| ID / hand-off (owner) | Work | Task | Evidence |
|---|---|---|---|
| 4a: `XCUT-14` "(phase 10) not a 4a row" | taken | 1.7, 1.12 | `xcut-14.*`, the ban |
| 4c: `XCUT-8` guarded factory | taken | 1.5 | `xcut-8.*`; `ErrorMappingFactoryTests` |
| 5b/5c: `XCUT-19`(c)(e), `-20`, `-24` | taken | 1.8, 1.9 | `xcut-19/20/24` |
| 6a: audit `XCUT-1`..`-10` | each row cites the 6a test it rests on; V2 re-measures | 1.4 to 1.6 | `invariant:xcut-1..10` |
| 6b: audit `XCUT-17` | taken | 1.8 | `xcut-17` |
| 6c: `XCUT-12`, `-14`, `-16`, `-18`, `-19`(d), `-21` | taken | 1.6 to 1.9 | the assertions |
| 7a: the kit lifts `SERDE-3`, `-4`, `-9`, `-10`, `-12`, `-13`, `-15`..`-20`; the smoke keeps `CheckPhase7aSerdeAsync` | taken in part: 11 assertions (`SERDE-4`, `-13`, `-14`, `-18`, `-30` judge core code and stay by reference, F6) | 2.1 to 2.5, 4.2, 4.4 | the codec suite; `SerdeProfileTests`, `ReadValueTests`, `TristateTests` |
| 7b: `XCUT-1` evidence | cited | 1.4 | `ServerSentEventStreamFailureTests` |
| 7c: `link-header.json`, splice properties "candidates for the kit" | **declined** (P10-9) | 4.2 | B.1 `by reference` |
| 8a: three suites, `APPENDIX_B.md`, `RequirementCatalog` reuse, `AppendixBMap` widened | taken | 1.1 to 4.2 | the suites, the map |
| 8b: audit `XCUT-2`, `-18`, `-22` | taken | 1.4, 1.8, 1.9 | the assertions + `SendFailuresTests`, `HeaderDropLogTests`, `SystemNetHttpClientOwnershipTests` |
| 9: `XCUT-10`, `-22`, `-23`, `NFR-2`, `NFR-8`/`-9`, the audit policy record | taken | 1.11, 3.2, 3.5, 4.4 | `NoRetryingHandlerTests`, `PrimaryHandlerLeaseTests`, `SingleRegistrationTests`, the parity test |
| Phase 0: AOT smoke completed; `NFR` gates dispositioned; `NFR-11` scan | taken | 3.6, 4.4, 4.6 | the gate table |
| 8a F-A2 (`ASYNC-2`) | **not taken** (not a phase 10 row; P10-2) | 4.8 | phase 11's inbound list |
| P10-21 Stryker.NET, P10-22 `benchmarks/` | declined with triggers | 4.8 | `first-release.md`, knowledge note |
| P10-23/-24/-25 findings | routed | 4.6, 4.8 | checklist, roadmap note |

---

## Traceability: every row and every appendix-B item to tasks

| ID | Tasks |
|---|---|
| `XCUT-1`, `-2`, `-3` | 1.1, 1.3, 1.4, 1.10, 4.2 |
| `XCUT-4`, `-5`, `-6`, `-7`, `-8` | 1.5, 1.10, 4.2 |
| `XCUT-9` | 1.6, 1.10, 1.12, 4.2 |
| `XCUT-10` | 1.6, 1.11, 4.2 |
| `XCUT-11`, `-12` | 1.6, 1.10, 4.2 |
| `XCUT-13` | 1.7, 1.10, 4.2 |
| `XCUT-14` | 1.7, 1.10, 1.12, 4.2 |
| `XCUT-15` | 1.7, 1.10, 4.2 |
| `XCUT-16`, `-17`, `-18`, `-19` | 1.8, 1.10, 4.2 |
| `XCUT-20`, `-21`, `-24` | 1.9, 1.10, 4.2 |
| `XCUT-22`, `-23` | 1.9, 1.11, 4.2 |
| `NFR-1`, `-2` | 3.1, 3.2, 3.5, 4.2 |
| `NFR-3` | 3.3, 3.5, 3.7, 4.2 |
| `NFR-4` | 3.7, 4.2 |
| `NFR-5`, `-6` | 3.7, 3.9, 3.10, 4.2 |
| `NFR-7` | 3.9, 4.2 |
| `NFR-8`, `-9` | 3.3, 3.5, 4.4, 4.2 |
| `NFR-10` | 3.3, 4.2 |
| `NFR-11` | 1.9, 3.6, 4.2 |
| `NFR-12` | 3.11, 4.2 |
| `NFR-13` | 3.4, 3.8, 4.2 |
| `NFR-14`, `-15` | 3.4, 3.5, 4.4, 4.2 |
| `NFR-16` | 4.6, 4.8 |
| `NFR-17` | 3.10, 4.9, 4.10, 4.2 |
| `SERDE-1`..`-3`, `-9`, `-10`, `-12`, `-15`..`-17`, `-19`, `-20`, `SEAM-20` (codec evidence); `SERDE-4`, `-13`, `-14`, `-18`, `-30` by reference (F6) | 2.1 to 2.5, 4.2 |

**Appendix B (61 items), status and tasks.** The map and its gate are tasks 4.1 and 4.2; the suites they point at are built in the tasks shown.

| Items | Status | Evidence built in |
|---|---|---|
| B.1.1 to B.1.10 (10, `PAGE`) | `by reference` (P10-9) | existing tests; 4.2 |
| B.2.1 to B.2.6 (6, `SSE`) | `by reference` | existing tests; 4.2 |
| B.3.1 | `suite` (`codec:`) | 2.2 |
| B.3.3 | `restated` | existing STJ tests; 4.2 |
| B.3.2, B.3.4, B.3.5, B.3.6, B.3.7 | `by reference` (B.3.2, B.3.4, B.3.5 carry `codec:` assertion references beside the core tests that own `SERDE-4`, `-13`, `-14`, `-18`, `-30`, F6) | 2.2 to 2.4; existing tests; 4.2 |
| B.4.1, B.4.5 | `restated` (§9.3, §10 entries 22, 23) | existing tests; 4.2 |
| B.4.2, B.4.3, B.4.4, B.4.6, B.4.7, B.4.8 | `by reference` | existing tests; 4.2 |
| B.5.1, B.5.2, B.5.3 | `restated` (DI package's reader, §10 entries 25, 26) | existing DI tests; 4.2 |
| B.5.4, B.5.5, B.5.6 | `by reference` | existing tests; 4.2 |
| B.6.1 to B.6.5 (5) | `suite` (`transport:`) | 8a, 8b; 4.2 |
| B.7.1, B.7.5, B.7.6 | `suite` (`transport:`) | 8a; 4.2 |
| B.7.2, B.7.3 | `restated` (§10 entry 8, §9.3) | existing bridge tests; 4.2 |
| B.7.4 | `by reference` | existing tests; 4.2 |
| B.8.1 to B.8.6 (6) | `suite` (`invariant:`) | 1.4 to 1.9 |
| B.9.1, B.9.5 | `suite` (`packaging:`) | 3.2, 3.3 |
| B.9.2, B.9.3, B.9.4, B.9.6, B.9.7 | `by reference` | 3.3 to 3.11, 4.4 |

Tally: 17 `suite`, 8 `restated`, 36 `by reference`, 0 `scoped out` (61).

| PR | Tasks | Rows (first owner) |
|---|---|---|
| 1 — kit contract and invariant suite | 1.1 to 1.13 | `XCUT-1`..`XCUT-24`, `NFR-11` (behavioural), the two bans |
| 2 — codec suite | 2.1 to 2.6 | `SERDE` evidence for B.3 |
| 3 — packaging suite and NFR gates | 3.1 to 3.12 | `NFR-1`..`-15` (gates), `NFR-17` (first half) |
| 4 — appendix B, coverage, close-out | 4.1 to 4.10 | appendix B's 61 items, the 41 rows' checklist, `NFR-16`, `NFR-17` |

Task count: 0.1 (pre-flight) + 13 (PR 1: 1.1 to 1.13) + 6 (PR 2: 2.1 to 2.6) + 12 (PR 3: 3.1 to 3.12) + 10 (PR 4: 4.1 to 4.10) = **42 tasks**.

## Exit criteria (the design's list, mapped to tasks)

1. The checklist has **41 rows** as tabled, each with a named test or assertion, `[Trait]` and file; a second table of contributed evidence and every hand-off; a third, the gate table: tasks 4.6, 4.8.
2. The invariant suite is green on the core and DI drivers, the codec suite on the STJ driver, the packaging suite in CI over `artifacts/packages`, each with exactly its pinned `NotExercised` list and waivers; the three transport drivers stay green: tasks 1.10, 1.11, 2.5, 3.5, 1.13, 3.12.
3. `APPENDIX_B.md` maps all 61 items (17 `suite`, 8 `restated`, 36 `by reference`) and `AppendixBCoverageTests` is green: tasks 4.1, 4.2.
4. `scripts/ci/requirement-coverage.cs --check` reports 645 of 645 owned, every MUST with a legend mark, and runs in CI: tasks 4.3, 4.6, 4.9.
5. The AOT smoke publishes and runs `CheckPhase10Async`: task 4.4.
6. Every gate green (build, format, the full run with the phase-1 `Security` classes unedited, coverage at least 80%, the audit, the packaging step, the cross-path reproducible pack, `verify-structure`): tasks 1.13, 3.12, 4.10.
7. `conformance.md`, the phase page, the READMEs, `CHANGELOG.md`, the dated roadmap status note, the corrections, the knowledge notes, and a housekeeping probe that reports nothing: tasks 4.5, 4.7, 4.8, 4.10.

## Open items carried to the lead (none blocks execution; each task implements the design's chosen option)

- **The open rulings of the design** are all taken at their recommended defaults: P10-1, 2, 5, 7, 8, 9, 16, 17, 21, 22, 23, 24, 25, 27. [Ruling dependencies](#ruling-dependencies) says which tasks each reversal touches.
- **Plan-level readings flagged for the lead:** R2 (the pipeline hook gains an `ILogger`), R3 (hooks over sealed core types are delegates, so the design's typed `CreateTokenCache`/`CreateRedactor`/`Models` change shape), R5 (the `AppendixBMapTests` order-sensitivity and the 11/52 hard-codes), R6 (the design's V12 detail: seven `none` lines, no `CA2000` severity, three `dotnet_code_quality.*` lines, 43 pragma sites), R7 (the constructor ban, one `BoundedMap` site, not the type ban), R15 (the cross-path pack packs `HEAD`), R17 (`XCUT-21`'s source by reference; four no-default hooks), and the codec suite's 11 assertions (F6).
- **The one remote action** is the ruleset change (task 4.10); the plan reads it and does not write it.
- **Findings while planning** (none changes a row's disposition): (a) the design's `CreatePipeline` signature omits the logger (R2); (b) its `Models`/`CreateTokenCache`/`CreateRedactor` hooks cannot carry a negative control as typed (R3); (c) the type ban on `ConcurrentDictionary` flags 12 lines and interacts with the existing `TryRemove` pair, so the constructors are banned instead (R7); (d) V12's `CA2000` is an option, not a severity, and the pragma counts differ (R6); (e) `CiWorkflowTests` cannot name the coverage step until PR 4 (R8); (f) the coverage script needs a `--root` flag to be testable (R9); (g) `Nfr11PublicSurfaceArchitectureTests`' "System" allow-list admits `System.Net.Http` and `System.Text.Json`, so the plan adds an explicit ban list beside it (task 3.6), a stricter reading than the design's text; (h) the design's `SuiteRunner`-generic wording understates that `ConformanceAssertion`, `AssertionBuilder` and `SuiteContext` are all transport-typed (R1).
