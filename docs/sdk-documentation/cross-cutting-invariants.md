# Cross-cutting invariants and the quality bar

**As built by phase 10, written against source on 2026-10-14** (branch `99-phase-10-cross-cutting-invariants`). This page says where each of the 24 cross-cutting rows
(`XCUT-1` to `XCUT-24`) and the 17 non-functional rows (`NFR-1` to `NFR-17`) is enforced, which gate fails when one is broken, how to run the invariant, codec and packaging suites
against a composition of your own, and what the phase decided not to build. The requirement IDs it cites are the normative text (chapters 19 and 20 of the
[product spec](../product-spec.md), indexed by its appendix C); the decisions behind the suites are in the
[phase 10 design](../work/mvp/phase10/2026-10-13-phase10-cross-cutting-invariants-design.md) (rulings `P10-1` to `P10-30`, facts `V1` to `V16`) and in
[design §9](../sdk-design-dotnet/09-toolchain-and-quality-gates.md). Neither is restated here. The suites themselves, their subjects, hooks and catalogues are in
[conformance.md](./conformance.md); the row-by-row verdicts, with the test that proves each, are in the
[phase 10 checklist](../work/mvp/phase10/2026-10-13-phase10-cross-cutting-invariants-checklist.md).

Phase 10 **measures and routes**; it changes no production behaviour (P10-2). Every `XCUT` row already had a mechanism and a passing test when it started; what it added is the portable
suite form, a test that cites each row's ID, two mechanical gates, and the gates of the quality bar that no one had dispositioned.

## Where each `XCUT` row is enforced

The suite assertions are `invariant:` (the kit's `InvariantSuite`); a **split** row has a clause that rests on an existing deviation or judged reading instead of an assertion.

| Row | Enforced by | Also proven by | Split clause |
|---|---|---|---|
| `XCUT-1` | `xcut-1.cancel-is-terminal` | `transport-3.cancel-is-terminal`; 6a `OperationPolicyTests`; 7b `ServerSentEventStreamFailureTests` | re-asserting an interrupt flag is cooperative: the caller's token stays signalled (design §10 entry 8) |
| `XCUT-2` | `xcut-2.timeout-is-retryable` | `transport-4.timeout-is-retryable`; 8b `SendFailuresTests` | none |
| `XCUT-3` | `xcut-3.wait-promptly-cancellable` | 6a `TimeProviderWaitsTests` | none |
| `XCUT-4` | `xcut-4.two-branches` | `transport-20.no-response-is-retryable` | "catchable as a generic I/O error" is a judged mechanism (design §10 entry 7, §11 item 23) |
| `XCUT-5` | `xcut-5.one-classifier` | `RetryFactsTests` | none |
| `XCUT-6`, `XCUT-7` | `xcut-6.capability-queried`, `xcut-7.configured-set-authoritative` | `RetryClassifierTests` | none |
| `XCUT-8` | `xcut-8.strict-factory-rejects` (hook `MapStrict`), `xcut-8.convenience-returns-response` | 4c `ErrorMappingFactoryTests` | the public `HttpResponseException` constructor accepts a 200: read as the SDK's mapping path, not a constructor a caller invokes (P10-24) |
| `XCUT-9` | `xcut-9.cycle-safe-walk` (hook `CreateCyclicException`); `Exception.GetBaseException` is banned in `src/` (`RS0030`) | `ExceptionFactsTests` | none |
| `XCUT-10` | `xcut-10.safety-gate-uniform` on the core and the DI drivers | 6a `RetryResendGateTests`; 9 `NoRetryingHandlerTests` | none |
| `XCUT-11`, `XCUT-12` | `xcut-11.shared-no-crosstalk`, `xcut-12.single-flight-scoped` | 4c `ConcurrencyTests`; 6c `AccessTokenCacheTests` | none |
| `XCUT-13` | `xcut-13.dispose-latched`, `xcut-13.dispose-non-blocking` | `transport-16.close-idempotent-nonblocking` (by reference) | preserving an ambient interrupt flag is vacuous (design §10 entry 8) |
| `XCUT-14` | `xcut-14.bounded-under-burst`, `xcut-14.drains-after-insert` (hook `CreateBoundedMap`); a second `ConcurrentDictionary` constructor outside `BoundedMap` is banned in `src/` (`RS0030`) | 4a `BoundedMapTests`; 6c `AccessTokenCacheTests` | none |
| `XCUT-15` | `xcut-15.no-external-alias` | 2a `ModelImmutabilityArchitectureTests` | none |
| `XCUT-16`, `XCUT-17` | `xcut-16.no-credential-over-http`, `xcut-17.redirect-hygiene` | phase 1 `AuthHttpsGuardTests`, `RedirectWireTests` (`Security`); 6b `RedirectCredentialLeakTests` | none |
| `XCUT-18` | `xcut-18.model-validation` | phase 1 `HeaderInjectionValidationTests` (`Security`); 8b `HeaderPartitionTests` | surrounding HTAB or space on a name is trimmed before validation (`HTTP-17`); the dispatch-time re-check is the transport suite's (P10-6) |
| `XCUT-19` | `xcut-19.redaction-default-deny` | phase 1 `UrlRedactionDefaultDenyTests` (`Security`); 5b `HttpLoggingDefaultsTests` | none |
| `XCUT-20` | `xcut-20.observability-never-throws` | 5b `EmissionGuardTests` | the SDK's own recording code is total; a listener's callbacks are `OBS-30`'s party (design §11 item 37) |
| `XCUT-21` | `xcut-21.cnonce-unpredictable` (optional hook `DrawCnonce`) | 6c `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator` | the operating-system CSPRNG source is cited by reference: an output test cannot tell it from `System.Random` |
| `XCUT-22` | `xcut-22.core-borrows` | `transport-15.borrowed-survives`; 9 `PrimaryHandlerLeaseTests` | none |
| `XCUT-23` | `xcut-23.resolution-deterministic` (hook `SeamResolution`, supplied by the DI driver) | 9 `SingleRegistrationTests` | the discovery half is vacuous: a seam is a non-nullable parameter (design §10 entry 9, §11 item 8) |
| `XCUT-24` | `xcut-24.preview-capped-non-consuming` | 5b `BodyPreviewTests` | the non-consuming clause is **declined** for an error-body snapshot above 1 MiB, which is drained, truncated without a marker and replaces the original (P10-23, `docs/first-release.md` *SHOULD declined*) |

A finding the measurement made and no phase fixed (P10-25): `ServiceResponseException` is public, documented as part of `XCUT-4`'s taxonomy, and never thrown. It is routed to phase 11 (delete, or argue it as part of
branch (b) in design §3.2) and to phase 12's public-surface choices.

## The gate table

"Blocking" means the gate fails its CI job. Whether a failing job also blocks a **merge** is a repository setting (a ruleset's required status checks) outside the workflow file: `CiWorkflowTests` pins that every gate below
is a step of `ci.yml` and that no step or job is advisory, and the checklist records the setting as read.

| Gate | Requirement | Mechanism | State after phase 10 |
|---|---|---|---|
| Warnings as errors | `NFR-6` | `TreatWarningsAsErrors`, NuGet audit; `[Obsolete]` use is `CS0618`, an error | blocking |
| Analyzers, code style, format | `NFR-7` | `AnalysisLevel`, `EnforceCodeStyleInBuild`, `dotnet format --verify-no-changes`; every `none` severity in `.editorconfig` carries a rationale and a `Re-enable when:` line, every `#pragma warning disable` under `src/` a why-comment (`AnalyzerWaiverTests`) | blocking |
| Documentation gate | `NFR-3` | `GenerateDocumentationFile`, `CS1591` as an error; `InternalsVisibleTo` only to a library's own test project (`nfr-3.no-internals-granted`) | blocking |
| Public-API files | `NFR-4` | `RS0016`/`RS0017` over five libraries; `NFR4ArchitectureTests` (both files exist, `Shipped` empty until phase 12) | blocking; the package-validation baseline waits for the first publish |
| Coverage floor | `NFR-5` | `scripts/ci/coverage-gate.cs` at 80%, with its self-test | blocking |
| Dependency audit and packaging suite | `NFR-1`, `NFR-2` | `scripts/ci/dependency-audit.cs` (the build output's closure) and `scripts/ci/packaging-conformance.cs` (the published metadata) | blocking, both |
| Trim and AOT | `NFR-8`, `NFR-9` | `IsTrimmable`/`IsAotCompatible` analyzers; `nfr-8.trim-and-aot-declared` over the packed assemblies; the NativeAOT smoke runs the invariant and codec suites | blocking |
| Single floor | `NFR-10` | `net10.0` once in `Directory.Build.props`; `nfr-10.single-floor` | blocking |
| Public signatures | `NFR-11` | `Nfr11PublicSurfaceArchitectureTests` scans core's compiled public surface (the files `PublicAPI.Shipped.txt` describe are empty until phase 12, and are not a type model); `nfr-11.scheduler-agnostic` runs the same call from the thread pool, a dedicated thread, an exclusive scheduler and a single-threaded context | blocking |
| Reproducible pack | `NFR-12` | `scripts/ci/reproducible-pack.sh` packs `HEAD` from two `git worktree`s at different paths | blocking; the release half waits for the first publish |
| License headers | `NFR-13` | `IDE0073` for compiled files; `LicenseHeaderTests` for the `scripts/ci` apps no project compiles | blocking |
| Lock-step versions | `NFR-14`, `NFR-15` | `Directory.Packages.props`, `--locked-mode`; `nfr-14.lockstep-version`, `nfr-15.version-resolvable`; the smoke checks `BuildInfo.SdkVersion` against the assembly's own version | blocking |
| CI completeness | `NFR-17` | `CiWorkflowTests`; the ruleset's required checks are recorded in the checklist | blocking per job |
| Appendix-B map | (§9.3) | `APPENDIX_B.md` and `AppendixBCoverageTests` (five rules) | blocking |
| Appendix-C coverage | (roadmap) | `scripts/ci/requirement-coverage.cs --check`, with `requirement-coverage-selftest.sh` | blocking (Linux row) |
| Banned APIs | §9.1 | `RS0030`, including `Exception.GetBaseException` and every `ConcurrentDictionary` constructor outside `BoundedMap` | blocking |
| Mutation testing, benchmarks | (§9.3, optional) | not scheduled | see below |
| Signing | `NFR-16` | the release job | phase 12 |

## Running the suites over your own composition

```csharp
// The invariant suite: any member you leave alone judges core's own component.
var subject = new InvariantSubject
{
    Name = "MyClient",
    CreatePipeline = (transport, options, time, logger) => MyPipeline.Build(transport, options, time, logger),
    Models = new ModelFactories { /* only the factories you replace */ },
};
var invariants = await InvariantSuite.RunAllAsync(subject);

// The codec suite: your codec, built the way a consumer builds it, with the probe models registered in your context.
var codec = await CodecSuite.RunAllAsync(new CodecSubject { Name = "MyCodec", CreateSerde = () => new MyCodec(MyContext.Default) });

// The packaging suite: the .nupkg files a pack produced.
var packaging = PackagingSuite.RunAll(new PackagingSubject { PackagePaths = Directory.GetFiles("artifacts/packages", "*.nupkg") });

var report = ConformanceReport.Combine("MyClient", [invariants, codec, packaging]);
Assert.True(report.IsGreen, report.ToString());
```

An assertion whose hook you did not supply is `NotExercised`, and a green report with such rows does not say it conforms: pin the list you expect and fail on any other, as the first-party drivers do (P10-28).
Reproducibility holds for a **git checkout**: a source archive with no `.git` embeds absolute paths in the assemblies and is not a reproducible input.

## The checks that count

- **Appendix B** has 61 items. `tests/Dexpace.Sdk.Conformance.Tests/APPENDIX_B.md` maps each to its evidence (17 `suite`, 8 `restated`, 36 `by reference`, none scoped out), and `AppendixBCoverageTests` checks that
  the row count and the IDs are the specification's, that every named path and assertion exists, that a `suite` row names an assertion citing one of its IDs, and that `AppendixBMap` agrees. A `by reference` row proves a
  claim and a file, never the behaviour.
- **Appendix C** has 645 requirements. `dotnet run scripts/ci/requirement-coverage.cs -- --check` reads every `docs/work/**/*-checklist.md` and fails on a requirement with no owning row, a row naming an unknown ID or a
  different level, a row with no legend mark, or a carried ID whose earlier row does not defer. `--report` prints the tallies.

```bash
dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages
dotnet run scripts/ci/requirement-coverage.cs -- --check
scripts/ci/requirement-coverage-selftest.sh
scripts/ci/reproducible-pack.sh
```

## What the phase did not build

- **Stryker.NET (P10-21).** Version 5.0.0 refuses every test project here: they run on Microsoft.Testing.Platform, which Stryker.NET does not support yet (stryker-net#3094). A VSTest shadow of each project would be a
  second harness to keep in step for an optional, non-blocking report. Post-release trigger: Stryker.NET supports Microsoft.Testing.Platform (`docs/first-release.md`).
- **`benchmarks/` (P10-22).** Design §9.3 makes BenchmarkDotNet results a requirement of a performance-motivated change; there has been none, and the allocation guarantees that matter are already blocking tests.
  The first performance-motivated change creates the project.
- **Lifting 7c's pagination vectors into the kit (P10-9).** The paging engine has one implementation and is not a seam.
