# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository

The .NET counterpart to [`dexpace/java-sdk`](https://github.com/dexpace/java-sdk) and
[`dexpace/python-sdk`](https://github.com/dexpace/python-sdk), and a sibling of the Ruby and Node ports
that share its product spec. The architecture follows the same shape (immutable HTTP models, transport
SPI, body abstractions, typed errors, a staged pipeline) but the public API uses .NET idioms — `record` /
`readonly record struct` instead of builder objects, `interface` instead of Kotlin `fun interface` /
Python `Protocol`, `IDisposable` / `IAsyncDisposable` instead of `AutoCloseable` / context managers,
`Task<T>` as the async contract. The pluggable I/O seam that exists in the Java SDK (`IoProvider` over
Okio) was intentionally **not** ported: .NET's `System.IO.Stream`, `Memory<byte>`, and
`IAsyncDisposable` cover the same surface natively (design §3.1).

## Build & test (from the repository root)

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode                 # lock files are committed; CI restores locked
dotnet build   Dexpace.Sdk.sln --configuration Release        # the build IS the lint gate (warnings-as-errors)
dotnet format  Dexpace.Sdk.sln --verify-no-changes            # formatting gate (uses .editorconfig)
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
```

Tests are xUnit v3 on **Microsoft.Testing.Platform**: `global.json`'s `"test": { "runner": … }` puts
`dotnet test` in the platform's mode, so it takes `--solution` / `--project` and the platform's options
(`--filter-class`, `--filter-trait`, `--report-trx`, `--coverlet`), not VSTest's `--filter`.

The rest of CI (`.github/workflows/ci.yml`) is runnable locally, and should be before a push:

```bash
dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80     # after a clean `dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`
scripts/ci/coverage-gate-selftest.sh artifacts/test-results            # the gate fails closed on a missing report (#33)
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages  # constraint 2, over deps.json and nuspecs
scripts/ci/reproducible-pack.sh                                        # pack twice, byte-compare (NFR-12)
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet build tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
```

The .NET SDK is pinned in `global.json` (`10.0.401`, `rollForward: latestPatch`). Every project — libraries,
tests and tools — targets **`net10.0` only**, set once in `Directory.Build.props` (roadmap decision D1).

## Conventions (enforced — match these when adding code)

- **net10.0, C# `latest`, `Nullable` on, `ImplicitUsings` off.** Each project commits a `GlobalUsings.cs`
  with its curated set; every other namespace is imported at the top of the file that uses it. Modern
  idioms: file-scoped namespaces, records, `readonly record struct`, pattern matching, `init` accessors,
  collection expressions where they fit.
- **`TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended` + `EnforceCodeStyleInBuild`.** The build
  is the lint gate. Rule severities live in `.editorconfig`; a handful of analyzer rules are deliberately
  dialled down there with a documented rationale (CA1308 lower-casing, CA1054/55/56 string URLs, CA1062,
  CA1707 for test names) — do not silence others without justification. `CA2007` is **on** for `src/`
  (every library `await` uses `ConfigureAwait(false)`, including `await using` and `await foreach`) and off
  for tests, tools and the AOT smoke consumer.
- **Library gates** (every project under `src/`, from `Directory.Build.props`): `IsTrimmable` +
  `IsAotCompatible`, `EnablePackageValidation`, the public-API files (`PublicAPI.Shipped.txt` stays empty
  until the first release; every surface change is a reviewed `PublicAPI.Unshipped.txt` diff, RS0016/RS0017),
  the banned-API list (`BannedSymbols.txt`, RS0030), `MA0051`'s 70-line method cap, and the `IDE0073`
  license header.
- **Immutable models.** `record` / `readonly record struct`; mutate via `with` expressions or `With*`
  helpers. No builder-as-object types — object initializers and `with` make them redundant. `Headers`
  is the one mutable-builder exception (`Headers.Builder`) for batched edits.
- **Interfaces for SPIs.** `IHttpClient`, `IAsyncHttpClient` are the transport seams; `ISerde` is the codec
  seam. `Dexpace.Sdk.Core` ships **no** transport and no concrete codec; each adapts one library in its own
  project (`Dexpace.Sdk.Http.*`, `Dexpace.Sdk.Serialization.*`).
- **Deterministic cleanup.** `Response`, `ResponseBody`, and transports implement `IDisposable` /
  `IAsyncDisposable`. Single-use bodies (stream-backed) throw `StreamConsumedException` on a second
  read; call `RequestBody.ToReplayableAsync()` before the first send if retries are needed.
- **The dependency rule for core** (roadmap constraint 2; design §2.4 is the authority). `Dexpace.Sdk.Core`
  may reference only assemblies in the `Microsoft.NETCore.App` reference pack of its target framework,
  `Microsoft.Extensions.Logging.Abstractions` (band-matched to the target, 10.0.x), and build-only packages
  marked `PrivateAssets="all"`. The logging facade is a recorded deviation (`logging-abstractions-dependency`,
  design §10); nobody re-litigates it, and nothing else is added. Adapters are held to the same rule plus
  NFR-2's "at most one third-party library". `scripts/ci/dependency-audit.cs` enforces it on every pack.
- **Narrow, fully-documented public API.** `GenerateDocumentationFile` is on, so every public member
  needs a `///` XML doc comment (missing docs are CS1591 → build error). Implementation helpers are
  `internal`, with `InternalsVisibleTo` granted to the library's own test project only.
- **Central package versions.** `Directory.Packages.props` is the single source of truth (the
  `libs.versions.toml` analog), with transitive pinning on. `PackageReference`s carry no `Version` attribute.
- **Tests** carry `[Trait("Category", …)]`: `Unit`, `Integration`, `Conformance`, `AotSmoke`, or `Security`
  (the permanent phase-1 regression tests — never delete or loosen one). Each suite's `TestCategoryTests`
  enforces it. `Dexpace.Sdk.Core.Tests` references core and in-memory fakes only, never a transport (SEAM-2,
  an architecture test).
- **MIT license header on every `.cs` file** — the two-line block, src and tests alike:

  ```csharp
  // Copyright (c) 2026 dexpace and Omar Aljarrah.
  // Licensed under the MIT License. See LICENSE in the repository root for details.
  ```

- **Commit style:** `chore:` for refactors/cleanup; `feat:` for new features; `fix:` for bug fixes;
  `docs:` for documentation-only changes; `test:` for tests only; `ci:` for CI configuration.

## Repository Layout

A single solution (`Dexpace.Sdk.sln`) with central build/package configuration at the root, plus a separate
tools solution. Each NuGet package is its own project under `src/`, with a `README.md` packed into it; tests
are under `tests/`.

```
dotnet-sdk/
├── Dexpace.Sdk.sln
├── Directory.Build.props            # shared compiler, analyzer, library-gate and package settings
├── Directory.Build.targets          # packs each library's README; pins the ILLink pack to the SDK band
├── Directory.Packages.props         # central package versions
├── BannedSymbols.txt                # RS0030 banned-API list for src/
├── .editorconfig                    # formatting + analyzer severities
├── global.json                      # pinned .NET SDK + the Microsoft.Testing.Platform test runner
├── nuget.config
├── src/
│   ├── Dexpace.Sdk.Core/                        # toolkit; no transport, no concrete codec
│   │   ├── Http/Common/             # Method, Protocol, MediaType, CommonMediaTypes, HttpHeaderName, Headers
│   │   ├── Http/Request/            # Request, RequestBody
│   │   ├── Http/Response/           # Response, ResponseBody, Status
│   │   ├── Client/                  # IHttpClient, IAsyncHttpClient, HttpClientExtensions, DelegateHttpClient
│   │   ├── Operations/              # OperationDescriptor, the operation-input projection
│   │   ├── Pipeline/                # HttpPipeline, PipelineBuilder, HttpPipelinePolicy, PipelineContext,
│   │   │   └── Policies/            #   DexpacePipeline; operation, redirect, retry, idempotency, set-date,
│   │   │                            #   client-identity, instrumentation and auth policies
│   │   ├── Recovery/                # Outcome, the step contracts, request/response recovery chains, RecoveryDispatcher,
│   │   │                            #   ErrorMappingStep, IdempotencyKeyStep, ClientIdentityStep
│   │   ├── Auth/                    # TokenCredential, AccessTokenCache, ApiKeyCredential, BasicCredential
│   │   ├── Pagination/              # AsyncPageable<T>, Page<T>, Pageable, PaginationStrategies
│   │   ├── Configuration/           # DexpaceClientOptions, RetryOptions, RedirectOptions
│   │   ├── Diagnostics/             # DexpaceDiagnostics (ActivitySource + Meter), UrlRedactor
│   │   ├── Execution/               # CallKey, InstrumentationContext, the three context records, DexpaceCallContexts
│   │   ├── Serialization/           # ISerde, IStringSerde, SerdeExtensions, ResponseBodySerdeExtensions
│   │   ├── IO/                      # internal copy, tee, capture and line-reading helpers
│   │   ├── Internal/                # Disposal, SdkVersion, TextDecoding, BoundedMap (the one bounded map)
│   │   └── Errors/                  # SdkException hierarchy, SerdeException, ExceptionFacts, ExceptionTrail
│   ├── Dexpace.Sdk.Http.SystemNet/              # reference transport over System.Net.Http.HttpClient
│   └── Dexpace.Sdk.Serialization.SystemTextJson/ # ISerde over source-generated System.Text.Json
├── tests/
│   ├── Dexpace.Sdk.Core.Tests/                  # core + fakes only; Architecture/ holds SEAM-1, SEAM-2, SEAM-22 and SSE-37
│   ├── Dexpace.Sdk.Http.SystemNet.Tests/        # the transport, incl. wire tests over a Loopback/ server
│   ├── Dexpace.Sdk.Serialization.SystemTextJson.Tests/
│   ├── Dexpace.Sdk.TestSupport/                 # fake transports, time, diagnostics listeners (not packed)
│   └── Dexpace.Sdk.AotSmoke/                    # NativeAOT smoke consumer, published and run in CI
├── scripts/
│   ├── ci/                          # coverage-gate{.cs,-selftest.sh}, dependency-audit.cs, reproducible-pack.sh
│   └── knowledge                    # the knowledge-lookup CLI
├── tools/Dexpace.Tools.sln          # tools/Knowledge{,.Tests} + .claude/skills/{housekeeping,knowledge-harvest}/{src,tests}
├── .github/                         # ci.yml, labels.yml, dependabot.yml, CODEOWNERS, issue/PR templates
└── docs/                            # see docs/README.md
```

## Architecture — Big Picture

The SDK is an **HTTP-client toolkit, not an HTTP client**. `Dexpace.Sdk.Core` provides abstractions,
models and the pipeline; consuming libraries plug in a concrete transport via `IHttpClient` /
`IAsyncHttpClient`, and a codec via `ISerde`.

Layered, bottom-up:

1. **Bodies** — `RequestBody.WriteToAsync(Stream)` is the outgoing streaming surface;
   `ResponseBody.OpenReadAsync` / `ReadAsBytesAsync` / `ReadAsStringAsync` drain the incoming side.
   Bytes/string bodies are replayable; stream bodies are single-use.
2. **HTTP value models** (`Http/Common`, `Http/Response/Status`) — immutable, case-insensitive
   `Headers` multimap with validated names and values; `MediaType` with quote-aware parse/round-trip;
   `Method`, `Protocol`, `Status` value types with well-known instances.
3. **Request / Response** — `Request` is an immutable `record` (absolute `Uri`); `Response` is a
   disposable carrier of status/headers/body/protocol.
4. **Transport SPI** (`Client`) — async-first `IAsyncHttpClient` plus a synchronous `IHttpClient`,
   with `AsAsync` / `AsBlocking` bridges.
5. **Pipeline** (`Pipeline`) — staged `HttpPipelinePolicy`s over the transport, assembled by
   `DexpacePipeline.CreateDefault`; auth, pagination and serde sit on top of it.
6. **Errors** — `SdkException` roots the hierarchy: `ServiceRequestException` (never sent, retry-safe
   on idempotent methods), `ServiceResponseException` (sent, response unreadable),
   `HttpResponseException` (4xx/5xx received intact), plus lifecycle/serialization/pipeline failures.

## Things That Will Bite You

- **The build is the lint gate.** A missing `///` doc comment on a public member, an unused `using`,
  a new public member missing from `PublicAPI.Unshipped.txt`, a method over 70 lines, a missing
  `ConfigureAwait(false)` in `src/`, or an unsuppressed analyzer finding fails the build
  (`TreatWarningsAsErrors`). Build before declaring done.
- **Core's dependency rule is exact** (above). A new runtime `PackageReference` in `Dexpace.Sdk.Core`
  fails the dependency audit; model a third-party need behind an interface and implement it in an adapter.
- **Lock files are committed.** After changing `Directory.Packages.props` or a reference, run
  `dotnet restore` (both solutions) and commit every changed `packages.lock.json`; CI's locked restore
  fails otherwise. `DexpaceToolchainPackVersion` moves with `global.json`.
- **Single-use bodies throw on second consumption.** `RequestBody.FromStream` /
  `ResponseBody.FromStream` raise `StreamConsumedException` the second time. Buffer first
  (`ToReplayableAsync`) when retries are in play. The exception: a `RequestBody.FromStream` over a readable, seekable
  stream with a declared length is replayable (it seeks the caller's stream before each write). Disposal is latched: a
  `Response` or `ResponseBody` releases at most once, and a `ResponseBody` subclass overrides `Dispose(bool)`, not `Dispose()`.
- **Transports are ownership-aware, and the SDK is the only redirect authority.** A caller-supplied
  `System.Net.Http.HttpClient` is never disposed by `SystemNetHttpClient`; only an internally created one
  is. A caller-supplied client must not follow redirects (`AllowAutoRedirect = false`), or the call fails.
- **Headers are validated.** CR, LF and other controls in a header name or value throw
  `ArgumentException` at construction; received headers take the lenient `Headers.Builder.AddInbound` path.
- **Central Package Management is on.** Add new dependency versions to `Directory.Packages.props`, and
  reference them without a `Version` attribute.

## Documentation, specification and workflow

Read [`docs/README.md`](docs/README.md) first — it is the ownership table for everything under `docs/`.

- **`docs/product-spec/`** — the normative, language-agnostic specification shared with the Ruby and Node
  siblings: 645 requirement IDs across 19 prefixes (`SEAM`, `HTTP`, `IO`, `BODY`, `CTX`, `PIPE`, `RECOV`,
  `RETRY`, `REDIR`, `AUTH`, `PAGE`, `SSE`, `SERDE`, `OBS`, `CFG`, `TRANSPORT`, `ASYNC`, `XCUT`, `NFR`).
  Appendix C is the canonical ID index. Cite IDs in code comments, tests and phase documents.
- **`docs/sdk-design-dotnet/`** — how each spec area maps to idiomatic .NET (retrofitted from
  `sdk-design-ruby`). Every section ends with an **As built (d45e64b)** verdict; §10 is the deviation ledger,
  §11 the spec-ambiguity resolutions, §12 the coverage index. Where it and this file disagree about the code's
  target shape, the design wins and this file is drift. Frozen to routine work: a change is a dated correction.
- **`docs/styleguide/`** — the vendored dexpace C# styleguide, binding for every `.cs` file. Its
  [SDK overlay](docs/styleguide/README.md#sdk-overlay--where-this-repository-departs) lists the departures
  (public API keeps the `I` prefix and `Async` suffix; see design §10).
- **`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`** — the v1 roadmap (phases 0–12), its
  cross-cutting constraints and its Phase Status Notes. `docs/first-release.md` is the release register.
- **Skills** (`.claude/skills/`): `knowledge-lookup`, `knowledge-harvest` (with the `knowledge-extractor` agent in
  `.claude/agents/`) and `housekeeping`. Their tools and tests build from `tools/Dexpace.Tools.sln`, separate from
  `Dexpace.Sdk.sln`.

**The phase workflow** (roadmap, "How Phases Get Executed"). Each phase or sub-phase runs
brainstorm → design → plan → checklist, on a branch `<issue>-phase-<N[x]>-<slug>` off `main`:

```bash
# 1. Read what is known — at the start of the phase and of every numbered task.
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info RETRY        # per prefix in scope
scripts/knowledge --gaps RETRY               # IDs to read out of appendix C itself
scripts/knowledge --req RETRY-5
# 2–3. The brainstorming and writing-plans skills write into the docs/superpowers/ inbox.
# 4. File them under docs/work/mvp/phaseN[/phaseNx]/ and fix what the last probe reports.
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5a            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5a --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Then implement the plan's numbered tasks TDD, write the checklist from what was built (one row per
requirement ID), add a `CHANGELOG.md` `[Unreleased]` entry, append a dated status note to the roadmap, and
run the probe once more before handing over.

**What is genuinely unbuilt** (the roadmap schedules each; the phase 2a domain-model rework — the `Headers` rebuild,
`Request`/`Response` validation, `Query`, `RequestOptions`, `ETag`, `HttpRange` and `RequestConditions` — and the phase 2b
seams — the transport SPI taking `RequestOptions`, `DelegateHttpClient`, the serde profiles and `OperationDescriptor`,
with `BaseAddress` now read — and phase 3a's I/O — the exact-length stream body, the sync body twins, the 64 MiB materialisation
cap and the internal `IO/` helpers — are built, see `docs/sdk-documentation/http.md`, `docs/sdk-documentation/seams.md` and
`docs/sdk-documentation/io.md`; and phase 3b's bodies — the file, form-urlencoded and multipart bodies, the seekable stream
promotion, the dispose latches, the BOM strip and the two internal logging wrappers — see `docs/sdk-documentation/bodies.md`; phase 4a's
execution-context chain, see `docs/sdk-documentation/execution-context.md`; and phase 4b's
recovery layer — `Outcome`, the step contracts, the two chains, `RecoveryDispatcher`, `ErrorBodyBuffer`/`ErrorMappingStep`, the idempotency and
client-identity steps, `ExceptionFacts` and `ExceptionTrail` — see `docs/sdk-documentation/recovery.md`):
the pipeline rework (4c), the retry engine over the recovery chain (6a), layered configuration and body/header logging (5), the auth resolver with RFC 7235
challenges and Digest (6c), tri-state PATCH, SSE and the remaining pagination surface (7), the transport
conformance kit (8), the DI package `Dexpace.Sdk.Extensions.DependencyInjection` (9), and the release
path (12).
