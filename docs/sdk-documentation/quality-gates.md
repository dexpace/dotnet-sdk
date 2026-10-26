# Quality gates

**As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). Every blocking gate this SDK runs, what each protects, how to run it locally, and where in CI it blocks.
It describes the build as it is rather than as it was planned; the plan it implements is [design §9](../sdk-design-dotnet/09-toolchain-and-quality-gates.md), whose gate table records each gate's state at
`d45e64b` and again at phase 11, and whose rows this page keeps current. Ruby's counterpart (`ruby-sdk/docs/sdk-documentation/quality-gates.md`) was read for the layout of the page and the lesson of
[the reproducible pack](#the-reproducible-pack-and-what-one-green-run-proves); the gates themselves are this repository's.

**The build is the lint gate.** On this host most gates are not scripts: the SDK ships the compiler, the analyzers, the formatter, the trimmer and package validation, so the first block of the table below is one
`dotnet build`. A run of the whole list is `CLAUDE.md`'s command block, and `.github/workflows/ci.yml` runs the same steps. What `CiWorkflowTests` pins is that every gate with a command is a named step of that
file and that none is advisory (`continue-on-error`, a job-level `if:`, a matrix `exclude`).

## The gates

Commands run from the repository root, in the order CI runs them. "Where" is the CI job: **all** is the `build & test` job on `ubuntu-latest`, `windows-latest` and `macos-latest`; **Linux** is that job's
Ubuntu row only; the other two jobs have their own names.

| # | Gate | Protects | Run it locally | Where |
|---|---|---|---|---|
| 1 | Locked restore | `NFR-14`: every project commits a `packages.lock.json`; a dependency graph that moved without a reviewed lock-file diff fails | `dotnet restore Dexpace.Sdk.sln --locked-mode` | all |
| 2 | Build, warnings as errors | `NFR-6`: `TreatWarningsAsErrors` over the compiler, the analyzers, deprecation (`CS0618`) and NuGet audit (`NuGetAuditMode=all`) | `dotnet build Dexpace.Sdk.sln --configuration Release` | all |
| 2a | analyzers and code style | `NFR-7`: `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild` and `.editorconfig`. Every relaxation carries a rationale and a re-enable condition (`AnalyzerWaiverTests`); `CA1062` is an error for `src/`; `CA2007` is on for `src/`, so every library `await` is `ConfigureAwait(false)`; `MA0051` caps a method at 70 lines | (the build) | all |
| 2b | public API | `NFR-3`, `NFR-4`: `CS1591` on a public member without a `///` comment; `RS0016` / `RS0017` against `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` | (the build) | all |
| 2c | banned APIs | `RS0030` against `BannedSymbols.txt`, each entry carrying the reason it prints: sync-over-async outside the documented bridges, wall-clock reads, `Task.Delay` and `Thread.Sleep` (use `TimeProviderWaits`), implicit environment reads, `Console`, `AsyncLocal<T>`, weak references, `Uri.ToString`, `ConfigurationBinder`, `Thread.Abort`, `ModuleInitializerAttribute`, a new `ConcurrentDictionary` constructor and `Exception.GetBaseException` (`BoundedMap` is the one construction site, under one scoped pragma) | (the build) | all |
| 2d | license header | `NFR-13`: `IDE0073` and the two-line MIT header on every `.cs` | (the build); `LicenseHeaderTests` also scans `scripts/` and `.claude/` | all |
| 2e | trim and AOT | `NFR-8`: `IsTrimmable` and `IsAotCompatible` on every library, so the trim, single-file and AOT analyzers run at its own build | (the build) | all |
| 3 | Format | `dotnet format` agrees with `.editorconfig` | `dotnet format Dexpace.Sdk.sln --verify-no-changes` | all |
| 4 | Tests | every test carries `[Trait("Category", …)]` (`Unit`, `Integration`, `Conformance`, `AotSmoke`, `Security`; `TestCategoryTests` per suite); the architecture tests (below); the repository gates under `tests/Dexpace.Sdk.Conformance.Tests/Gates/` | `dotnet test --solution Dexpace.Sdk.sln --configuration Release` | all |
| 4a | `Security` tests | the permanent phase-1 regression tests; never deleted, never loosened | `dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"` | all (inside 4) |
| 4b | conformance, three drivers | the kit against the reference transport, the DI-built named client, and core's own composition (the invariant suite) | `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Conformance"` (likewise the DI and Core test projects) | all (inside 4) |
| 5 | Coverage floor | `NFR-5`: 80% aggregate line coverage over `src/`, merged across every test project; it fails closed when a test project left no report | `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80`, after a clean `dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results` | all |
| 5a | the floor's self-test | a missing report, an unmeasured library, a floor above the measurement and an empty directory each fail with their exit code | `scripts/ci/coverage-gate-selftest.sh artifacts/test-results` | all |
| 6 | Pack, with package validation | `EnablePackageValidation` runs inside `pack` for every library; the baseline waits for the first published version | `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` | all |
| 7 | Dependency audit | `SEAM-1`, `NFR-1`, `NFR-2`: each library's `deps.json` closure and `.nuspec` dependency groups against one policy record per library; core's allowance is a fixed set | `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages` | all |
| 7a | the audit's self-test | a package injected under the facade, under its dependency, or with no edge at all fails, naming it | `scripts/ci/dependency-audit-selftest.sh artifacts/packages` | all |
| 8 | Packaging conformance | `NFR-1`, `-2`, `-3`, `-8`, `-10`, `-13` to `-15`: the kit's packaging suite over the packed `.nupkg` files (metadata, license, versions, the assemblies' attributes), read without loading them | `dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages` | all |
| 9 | Tools | the knowledge CLI and the housekeeping skill build, format and test: restore, build, format, test of `tools/Dexpace.Tools.sln` | `dotnet restore tools/Dexpace.Tools.sln --locked-mode`, then `dotnet build tools/Dexpace.Tools.sln --configuration Release`, then `dotnet test --solution tools/Dexpace.Tools.sln --configuration Release` | all |
| 10 | Doc fences | every ` ```csharp ` fence that names `Dexpace.Sdk` in `README.md`, `docs/sdk-documentation/*.md` and `src/*/README.md` compiles against the built libraries, warnings as errors; ` ```csharp fragment ` is counted, not compiled | `dotnet run --project .claude/skills/housekeeping/src -- fences` (after the Release build) | Linux |
| 11 | Knowledge corpus structure | the harvested corpus's structure, a check over committed files | `scripts/knowledge verify-structure` | Linux |
| 12 | Requirement coverage | every appendix-C ID is owned by a checklist row under `docs/work/` with its level and a legend mark; its self-test proves each kind of finding fails | `scripts/ci/requirement-coverage-selftest.sh`, then `dotnet run scripts/ci/requirement-coverage.cs -- --check` | Linux |
| 13 | Design §12 | the coverage table of design §12 is generated from the checklists' marks, not typed; a mark that moves an ID between sets fails until `--section12` regenerates the table | `dotnet run scripts/ci/requirement-coverage.cs -- --check-section12` | Linux |
| 14 | Pack twice and compare | `NFR-12`'s build half: two packs of `HEAD` from two `git worktree`s at different paths, with `SOURCE_DATE_EPOCH` at the commit time, byte-identical (`.nupkg` and `.snupkg`) | `scripts/ci/reproducible-pack.sh artifacts/reproducible` | `reproducible packages` |
| 14a | the compare's self-test | a flipped byte in a copy of one pack fails it, naming the package; a toolchain other than the pinned one is refused | `scripts/ci/reproducible-pack.sh --self-test artifacts/reproducible-selftest` | `reproducible packages` |
| 15 | NativeAOT smoke | `NFR-9`: the smoke consumer is published with `PublishAot`, trim and AOT warnings as errors, and run; it exercises the invariant and codec suites and the DI package | `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke`, then `./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` | `NativeAOT smoke` (Linux) |

Phase 12 adds the gates of the release path. Each is a step of `ci.yml` and a row of `CiWorkflowTests`, except where it is a test:

| Gate | Protects | Run it locally |
|---|---|---|
| Strong naming | `NFR-16`'s strong-naming half: the five libraries and their friend test assemblies are signed with the committed key, and `StrongNameKeyTests` pins the public key, its algorithm (`CALG_RSA_SIGN`, not the `CALG_RSA_KEYX` that produces `CS0281`) and the key on every `InternalsVisibleTo`. The tools stay unsigned | `dotnet test --project tests/Dexpace.Sdk.Conformance.Tests --configuration Release --filter-class "*StrongNameKeyTests"` |
| Symbol packages | `NFR-12`: `IncludeSymbols` with `snupkg` gives a `.snupkg` per `.nupkg`, and the pack compare covers them | `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` |
| One `[LoggerMessage]` type per assembly | the generator orders classes nondeterministically ([below](#the-reproducible-pack-and-what-one-green-run-proves)) | `dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*LoggerMessageDeterminismArchitectureTests"` |
| Release preflight | `NFR-16`, `NFR-12`: the packed set is five packages at one version with a `.snupkg` beside each, every packed assembly carries the committed key's token, and, given a `--tag`, the tag equals the version, no `PublicAPI.Unshipped.txt` holds a member, `CHANGELOG.md` has `## [<version>]`, and the tag commit is an ancestor of `main` | `dotnet run scripts/ci/release-preflight.cs -- artifacts/packages`, and its self-test `scripts/ci/release-preflight-selftest.sh artifacts/packages` |
| Release workflow | `ReleaseWorkflowTests` pins `.github/workflows/release.yml`: two triggers, minimum permissions, no secret, a guard before the push. It is inert until the first publish | `dotnet test --project tests/Dexpace.Sdk.Conformance.Tests --configuration Release --filter-class "*ReleaseWorkflowTests"` |

`scripts/ci/release-preflight.cs` and `release.yml` belong to the release path in [`docs/first-release.md`](../first-release.md); this page lists them with the other gates because they run in CI or against its
output.

### The architecture tests

Several gates are tests that read the source, the assemblies or the repository, so they run with gate 4 and fail it by name. They are the ones to know when a change is refused for a reason the compiler did
not give:

| Test | Holds |
|---|---|
| `Seam1ArchitectureTests`, `Seam2ArchitectureTests`, `SeamImplementationArchitectureTests` | core references no transport and no codec and names no concrete implementation of a seam (`SEAM-1`, `SEAM-2`) |
| `Sse37ArchitectureTests`, `Sse38ArchitectureTests` | streaming and paging do not depend on a serializer (`SSE-37`); no reconnecting client (`SSE-38`) |
| `NFR4ArchitectureTests`, `Nfr11PublicSurfaceArchitectureTests` | every library has both `PublicAPI` files; the public signatures name only `System.*`, the logging abstractions and `Dexpace.*` types (`NFR-4`, `NFR-11`) |
| `FatalFilterArchitectureTests` | every broad `catch` filters `when (!ExceptionFacts.IsFatal(ex))` or ends in `throw;` |
| `ModelImmutabilityArchitectureTests`, `OptionsRecordArchitectureTests`, `NoAmbientStateArchitectureTests` | every HTTP model is sealed, immutable after construction and exposes read-only collections (`HTTP-1`, `HTTP-5`); an options record is sealed, `init`-only and holds no mutable collection (`CFG-8`); the library holds no ambient per-call state, by a reflection scan over every field |
| `CiWorkflowTests` | every gate above with a command is a named, blocking step of `ci.yml`; only the documented steps are conditional; the workflow triggers on `push` and `pull_request` to `main` |
| `LicenseHeaderTests`, `AnalyzerWaiverTests` | the header on every `.cs`; a re-enable condition on every analyzer relaxation |
| `LedgerAuditTests` | `docs/deviations.md` still describes design §10 and §11, cites lines that exist, and the negative scans read files |

A conformance assertion is a gate too, and it has its own rule: every assertion in `Dexpace.Sdk.Conformance` has a deliberately broken subject under `tests/Dexpace.Sdk.Conformance.Tests/Controls/` that it
must fail (`AssertionCatalogueTests`), and a waiver whose assertion now passes fails the run ([`conformance.md`](./conformance.md)).

## Where they run

`.github/workflows/ci.yml` has three jobs, on every push and pull request to `main`:

- **`build & test`** on `ubuntu-latest`, `windows-latest` and `macos-latest` (`fail-fast` off, a 30-minute timeout, `bash` as the shell). It restores locked, builds, formats, tests with coverage, applies the
  coverage floor, packs, runs the two package audits and the tools, then, on the Ubuntu row only, the repository checks (doc fences, corpus structure, requirement coverage, design §12). A test host in which no
  test finished for five minutes is dumped and killed (`--hangdump`), so a hang names its project instead of running into the timeout.
- **`reproducible packages`** on `ubuntu-latest`: the compare and its self-test.
- **`NativeAOT smoke`** on `ubuntu-latest`: installs `clang` and `zlib1g-dev`, publishes and runs the smoke consumer.

The operating-system matrix is a gate: a test that passes on Linux and fails on Windows is a red job, and the only conditional steps are the Linux-only structural checks and the upload of the test results,
which must run after a failure.

**That a merge requires these checks is not in the file.** `NFR-17`'s second half is a repository ruleset (`required_status_checks`) that the repository cannot make for itself, so it is recorded as the
one open action in [`docs/first-release.md`](../first-release.md). Until the ruleset exists the gates above fail a pull request without stopping its merge.

## Running a gate against its failing input

A gate you have not watched go red is a gate you assume. Each script gate has a self-test that alters a copy of its own input and requires the right failure, and each conformance assertion has a negative
control:

```bash
scripts/ci/coverage-gate-selftest.sh artifacts/test-results                    # gate 5, over this run's reports
scripts/ci/dependency-audit-selftest.sh artifacts/packages                     # gate 7, over the packed set
scripts/ci/requirement-coverage-selftest.sh                                     # gate 12
scripts/ci/reproducible-pack.sh --self-test artifacts/reproducible-selftest    # gate 14
```

A gate that is a test is run by name and seen to fail by breaking the line it pins and reverting it (`git checkout -- <file>`); a gate added without a case that fails is not reviewed as done.

## Facts worth knowing before you fight a gate

- **Tests are xUnit v3 on Microsoft.Testing.Platform.** `global.json`'s `"test": { "runner": … }` puts `dotnet test` in the platform's mode, so it takes `--solution` and `--project` and the platform's
  options (`--filter-class`, `--filter-trait`, `--report-trx`, `--coverlet`), not VSTest's `--filter`. Build first and pass `--no-build`; never pass MSBuild flags to `dotnet test`; a run that reports
  zero tests is a failed command. Run suites through `dotnet test`, not `dotnet run`: `Directory.Build.targets` supplies the coverlet file prefix, which xUnit's own runner rejects.
- **Lock files are committed.** After changing `Directory.Packages.props` or a reference, run `dotnet restore` on **both** solutions and commit every `packages.lock.json` that moved. `DexpaceToolchainPackVersion`
  moves with `global.json`.
- **The pinned SDK is part of the input.** `global.json` pins `10.0.401` with `rollForward: latestPatch`, so a machine with a newer patch installed would pack with it. The reproducible-pack script writes a
  job-local `global.json` with `"rollForward": "disable"` into each checkout and fails naming both versions when `dotnet --version` is not the pinned one.
- **Core's dependency rule is exact.** A new runtime `PackageReference` in `Dexpace.Sdk.Core` fails gate 7; model a third-party need behind an interface and implement it in an adapter. The logging abstractions
  are a recorded deviation (design §10), and nothing else joins them.
- **The probe is a hand-run gate.** `dotnet run --project .claude/skills/housekeeping/src -- probe` reads the documentation for drift (stale counts, broken links, a README missing from a package, a
  misattributed requirement ID). It is not a CI step; run it before handing over any change that touches `docs/`, `CLAUDE.md` or a README.

## The reproducible pack, and what one green run proves

`NFR-12`'s build half asks that packing the same commit twice produces the same bytes. Gate 14 packs `HEAD` from two checkouts at different paths, because packing twice in one directory cannot see an absolute
path leaking into an artefact (an assembly embeds the path of its PDB when nothing maps it). The compiler is already deterministic (`Deterministic` and, in CI, `ContinuousIntegrationBuild`), and
`SOURCE_DATE_EPOCH` fixes the zip-entry timestamps of the package containers.

The `reproducible packages` job failed at the phase 10 merge, and the cause was not in the script. Reproduced locally, on an unmodified tree and one run in five, `Dexpace.Sdk.Http.SystemNet.dll` differed between two packs of one commit in 72 bytes: the MVID, the CodeView PDB GUID, the PE time stamp
and the PDB checksum. The build was feeding the compiler a different input. The `Microsoft.Extensions.Logging` source generator writes every `[LoggerMessage]` partial class of an assembly into one
`LoggerMessage.g.cs`, and it emitted the two classes of that project (`HeaderDropLog` and `ProxyWarnings`) in an order that varied from build to build: eight builds with `EmitCompilerGeneratedFiles` gave two different
files, six to two. Every other library had a single `[LoggerMessage]` type and was unaffected.

Two lessons carry:

1. **A generator that orders its output nondeterministically breaks a byte-for-byte compare.** The fix is not a retry or a waiver: it is to give the generator one class to order. Each assembly now holds at most one
   type with `[LoggerMessage]` methods, and `LoggerMessageDeterminismArchitectureTests` fails a second.
2. **One green run proves nothing.** At the time, the script passed most of its runs (four of five locally) even with the defect in place. A fix for a nondeterminism is judged by the generator's output (one
   distinct `LoggerMessage.g.cs` across repeated builds), not by a count of green runs of the job.

The release half of `NFR-12` (a published artifact rebuilt from its tag) waits for the first publish ([`docs/first-release.md`](../first-release.md)).

## Gates that wait

| Gate | Waits for |
|---|---|
| `PackageValidationBaselineVersion` (binary compatibility against the previous release) | the first published version: the baseline cannot be set before a package of that id exists on nuget.org, and setting it earlier would break every build |
| `NFR-12`'s release half and `NFR-16`'s closure (the build attestation verified with `gh attestation verify`) | the first publish |
| The `main` ruleset (`NFR-17`'s second half) | a repository setting, applied by a maintainer |
