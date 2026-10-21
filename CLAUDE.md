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
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Conformance"   # the kit against the reference transport
dotnet test    --project tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests --configuration Release --filter-trait "Category=Conformance"   # the kit against the DI-built named-client transport (and the invariant suite's resolution driver)
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Conformance"   # the invariant suite against core's own composition
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
dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages  # the kit's packaging suite over the packed .nupkg files (NFR-1, -2, -3, -8, -10, -13 to -15)
dotnet run scripts/ci/requirement-coverage.cs -- --check               # every appendix-C ID owned by a checklist row, with its level and a mark (--report prints the tallies)
scripts/ci/requirement-coverage-selftest.sh                            # the coverage gate fails on each kind of finding
scripts/ci/reproducible-pack.sh                                        # pack HEAD from two git checkouts at different paths, byte-compare (NFR-12)
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
  dialled down there with a documented rationale (CA1308 lower-casing, CA1054/55/56 string URLs, CA1707 for test names;
  `CA1062` is `error` for `src/` and off for tests, tools and scripts only) — do not silence others without justification. `CA2007` is **on** for `src/`
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
  seam; `IResponseHandler<T>` is what a generated SDK implements to turn a response into a value.
  `Dexpace.Sdk.Core` ships **no** transport and no concrete codec; each adapts one library in its own
  project (`Dexpace.Sdk.Http.*`, `Dexpace.Sdk.Serialization.*`).
- **Deterministic cleanup.** `Response`, `ResponseBody`, and transports implement `IDisposable` /
  `IAsyncDisposable`. Single-use bodies (stream-backed) throw `StreamConsumedException` on a second
  *read*, while a second *open* of a response body returns the first stream; call `RequestBody.ToReplayableAsync()` before the first send if retries are needed.
- **The dependency rule for core** (roadmap constraint 2; design §2.4 is the authority). `Dexpace.Sdk.Core`
  may reference only assemblies in the `Microsoft.NETCore.App` reference pack of its target framework,
  `Microsoft.Extensions.Logging.Abstractions` (band-matched to the target, 10.0.x), and build-only packages
  marked `PrivateAssets="all"`. The logging facade is a recorded deviation (`logging-abstractions-dependency`,
  design §10); nobody re-litigates it, and nothing else is added. Adapters are held to the same rule plus
  NFR-2's "at most one third-party library", where a root package whose closure is one publisher's lock-stepped family counts as one (the DI package's is `Microsoft.Extensions.Http`, design §10 entry 2).
  `scripts/ci/dependency-audit.cs` enforces it on every pack, from one policy record per library (its one third-party root and the first-party projects it may reference).
- **Narrow, fully-documented public API.** `GenerateDocumentationFile` is on, so every public member
  needs a `///` XML doc comment (missing docs are CS1591 → build error). Implementation helpers are
  `internal`, with `InternalsVisibleTo` granted to the library's own test project only.
- **Central package versions.** `Directory.Packages.props` is the single source of truth (the
  `libs.versions.toml` analog), with transitive pinning on. `PackageReference`s carry no `Version` attribute.
- **Tests** carry `[Trait("Category", …)]`: `Unit`, `Integration`, `Conformance`, `AotSmoke`, or `Security`
  (the permanent phase-1 regression tests — never delete or loosen one). Each suite's `TestCategoryTests`
  enforces it. `Dexpace.Sdk.Core.Tests` references core, the core-only conformance kit and in-memory fakes, never a transport or a codec (SEAM-2,
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
│   │   ├── Http/Common/             # Method, Protocol, MediaType, CommonMediaTypes, HttpHeaderName, Headers, HttpDate
│   │   ├── Http/Request/            # Request, RequestBody
│   │   ├── Http/Response/           # Response, ResponseBody, Status, IResponseHandler, TypedResponse (the lazy typed response)
│   │   ├── Client/                  # IHttpClient, IAsyncHttpClient, HttpClientExtensions, DelegateHttpClient
│   │   ├── Operations/              # OperationDescriptor, the operation-input projection
│   │   ├── Resilience/              # internal: RetryFacts (classifier, re-send gate), RetryBackoff, RetryPacing, RetryBudget, RetryEngine (the one retry loop)
│   │   ├── Pipeline/                # HttpPipeline (also a transport), PipelineBuilder, HttpPipelinePolicy,
│   │   │   └── Policies/            #   PipelineContext, PipelineRunner, DexpacePipeline; operation, redirect, retry,
│   │   │                            #   idempotency, set-date, client-identity, instrumentation, error-mapping policies; the auth base
│   │   │                            #   AuthorizationPolicy + ApiKey, Basic, BearerToken, Challenge and MultiScheme policies;
│   │   │                            #   Redirect/ holds the internal decider, chain, location resolver and reissue behind RedirectPolicy
│   │   ├── Recovery/                # Outcome, the step contracts, request/response recovery chains, RecoveryDispatcher, RetryRecovery,
│   │   │                            #   ErrorMappingStep, IdempotencyKeyStep, ClientIdentityStep
│   │   ├── Auth/                    # AuthScheme, AuthRequirement, AuthDescriptor, AuthResolver, AuthCredentials, TokenCredential, AccessToken, AccessTokenCache,
│   │   │                            #   ApiKey/Basic/DigestCredential, AuthenticationChallenge, IChallengeHandler + Basic/Digest/Composite handlers
│   │   ├── Pagination/              # AsyncPageable<T>, Pageable<T>, Page<T>, PageInfo<T>, IPageStrategy, Pageable (Create, CreateBlocking, FromFetchers), PaginationStrategies, PagingOptions, FetchedPage<T>
│   │   ├── ServerSentEvents/        # ServerSentEvent, ServerSentEventReader, ServerSentEventStream, SseMapResult, ServerSentEventLineTooLongException; internal EventAccumulator, RetryField
│   │   ├── Configuration/           # DexpaceClientOptions, RetryOptions, RedirectOptions, HttpLoggingOptions (sealed records), HttpLogLevel, ProxyOptions, TimeProviderWaits, BuildInfo
│   │   ├── Diagnostics/             # DexpaceDiagnostics (ActivitySource + Meter), UrlRedactor, DexpaceLogEvents/Keys, HttpLogEmitter,
│   │   │                            #   HttpSemanticConventions, HttpClientMetrics, AttemptTelemetry, OperationTelemetry (5c, internal)
│   │   ├── Execution/               # CallKey, InstrumentationContext, the three context records, DexpaceCallContexts
│   │   ├── Serialization/           # ISerde, IStringSerde, SerdeExtensions, ResponseBodySerdeExtensions (the typed readers), ResponseHandlers,
│   │   │                            #   Tristate (+ TristateSentinel, ITristate hook)
│   │   ├── IO/                      # internal copy, tee, capture and line-reading helpers
│   │   ├── Internal/                # Disposal, SdkVersion, TextDecoding, BoundedMap (the one bounded map)
│   │   └── Errors/                  # SdkException hierarchy, IRetryableError, OperationTimeoutException, RedirectException (+ downgrade / not-replayable leaves), SerdeException, ExceptionFacts, ExceptionTrail
│   ├── Dexpace.Sdk.Http.SystemNet/              # reference transport over System.Net.Http.HttpClient; SystemNetHttpClientOptions, HeaderDropLogging
│   ├── Dexpace.Sdk.Serialization.SystemTextJson/ # ISerde over source-generated System.Text.Json
│   ├── Dexpace.Sdk.Extensions.DependencyInjection/ # host integration: AddDexpaceClient, DexpaceClientBuilder, the configuration reader and its grammars, validation, the named client over IHttpClientFactory
│   └── Dexpace.Sdk.Conformance/                 # the conformance kit (core only), four suites: TransportSuite, InvariantSuite, CodecSuite, PackagingSuite, their subjects, the one report and waivers, Wire/ (the loopback fixture)
├── tests/
│   ├── Dexpace.Sdk.Core.Tests/                  # core + fakes only; Architecture/ holds SEAM-1, SEAM-2, SEAM-22, SSE-37, SSE-38, NFR-4 and NFR-11; Conformance/ is the invariant suite's core driver
│   ├── Dexpace.Sdk.Http.SystemNet.Tests/        # the transport, incl. wire tests over the kit's Wire/ server and SystemNetConformanceTests (the kit's driver over both constructions, with its two permanent waivers)
│   ├── Dexpace.Sdk.Conformance.Tests/           # the kit's own tests, a negative control per assertion (Controls/), the raw-socket client (RawSocket/) and its driver (Drivers/), APPENDIX_B.md and its gate (Requirements/), the repository gates (Gates/)
│   ├── Dexpace.Sdk.Serialization.SystemTextJson.Tests/
│   ├── Dexpace.Sdk.Extensions.DependencyInjection.Tests/ # the reader, registration, the named client, the third transport driver and the invariant suite's resolution driver (Conformance/) and NamedClientRedirectWireTests (Security/)
│   ├── Dexpace.Sdk.TestSupport/                 # fake transports, time, diagnostics listeners (not packed)
│   └── Dexpace.Sdk.AotSmoke/                    # NativeAOT smoke consumer, published and run in CI
├── scripts/
│   ├── ci/                          # coverage-gate{.cs,-selftest.sh}, dependency-audit.cs (one policy record per library), packaging-conformance.cs, requirement-coverage{.cs,-selftest.sh}, reproducible-pack.sh
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
6. **Errors** — `SdkException` roots the hierarchy, except the transport failure: `ServiceRequestException`
   derives from `IOException` (so `catch (SdkException)` does not catch it) and keeps `IRetryableError` (retry-safe on
   idempotent methods; its message says whether the request could not be sent or no response was received; there is no
   `ServiceResponseException`). `HttpResponseException` (4xx/5xx received intact), plus lifecycle/serialization/pipeline failures.

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
  `ResponseBody.FromStream` raise `StreamConsumedException` the second time they are *read*; a second *open* of a response body
  returns the first handle (an open after dispose throws `StreamClosedException`). Buffer first
  (`ToReplayableAsync`) when retries are in play. The exception: a `RequestBody.FromStream` over a readable, seekable
  stream with a declared length is replayable (it seeks the caller's stream before each write). Disposal is latched: a
  `Response` or `ResponseBody` releases at most once, and a `ResponseBody` subclass overrides `Dispose(bool)`, not `Dispose()`.
- **Typed reads close the body and reject a wire `null`.** `ResponseBody.ReadValueAsync<T>` / `ReadValue<T>` stream, dispose the body on every path, throw a
  `DeserializationException` naming `T` for a missing payload and for a `null` into a reference type (`ReadValueOrDefault*` admits the `null`); the response handlers
  own and dispose the response they are given. `SystemTextJsonSerde` copies the options it is handed (yours is never frozen), wires `Tristate<T>` on the copy, and
  needs a source-generated context in the default or `Metadata` mode: a fast-path-only (`GenerationMode = Serialization`) context cannot be used with it.
- **Transports are ownership-aware, and the SDK is the only redirect authority.** A caller-supplied
  `System.Net.Http.HttpClient` is never disposed by `SystemNetHttpClient`; only an internally created one
  is. A caller-supplied client must not follow redirects (`AllowAutoRedirect = false`), or the call fails. `SystemNetHttpClient(HttpMessageHandler)`
  builds an owned `HttpClient` over a handler it **borrows** (never disposed or changed) and rejects a primary `SocketsHttpHandler`/`HttpClientHandler` whose `AllowAutoRedirect` is `true`, its default
  (`new SystemNetHttpClient(new SocketsHttpHandler())` throws). A proxy is **explicit**: the owned transport goes direct and never reads `HTTP_PROXY`/`NO_PROXY` itself; write
  `new SystemNetHttpClientOptions { Proxy = ProxyOptions.FromEnvironment() }` to ask for the environment, and `Proxy` with a handler or a client throws. `Execute` is a genuine `HttpClient.Send`, so a custom handler that
  implements only `SendAsync` throws (primary) or is skipped (delegating) on the sync face; every call after `Dispose` throws `ObjectDisposedException`, borrowed clients included
  (`docs/sdk-documentation/transport.md`).
- **A transport failure is an `IOException`, not an `SdkException`; a second open of a response body is not an error.** `catch (SdkException)` does not catch `ServiceRequestException`
  (`catch (IOException)` does, and `IRetryableError` still marks it retryable). `ResponseBody.OpenReadAsync` / `OpenRead` return the first stream on the second call and throw `StreamClosedException` only after dispose; a `StreamConsumedException`
  is the second *read* of a single-use body. A caller that abandons a bridged blocking call awaits it with `ResponseTaskExtensions.WaitOrDisposeAsync`, never `Task.WaitAsync(token)`, or a response that lands in the same instant is never closed.
  The ledger of every departure from the spec is `docs/deviations.md`; a retained entry names a reason class, and `LedgerAuditTests` fails a cited `path:line` that no longer exists in its file and a negative scan whose paths read no file; it does not check that a cited line still holds what the ledger says, so a change that moves a cited line re-derives the citation by hand in the same pull request.
- **Redirects follow only `GET` and `HEAD` by default, and 303 is opt-in.** `RedirectOptions` defaults to 3 hops, `AllowedMethods = {GET, HEAD}`
  and `FollowSeeOther = false`; a `POST` is never rewritten to a `GET`. An https to http hop throws `RedirectSchemeDowngradeException` and a
  method-preserving hop over a single-use body throws `RedirectBodyNotReplayableException` (`ToReplayableAsync` first); a revisited URI returns the 3xx.
  `Authorization` is stripped on every hop and `Cookie` / `Proxy-Authorization` cross-origin, against the seed, with no option to turn it off.
- **`ServerSentEventStream.FromResponse` owns the response from the call, even when it throws.** A bodyless response (204, 205, 304, a `HEAD` request, a zero `Content-Length`) is disposed and rejected with `ArgumentException`;
  the facade releases the response once on every path, a release failure being swallowed and reported out of band on a clean end or an early dispose and attached to the primary on a failure. There is no reconnecting client
  (`SSE-38`), the sentinel and `Accept` are the caller's, and the line cap bounds a line, not an event (`sse.md`).
- **Headers are validated.** CR, LF and other controls in a header name or value throw
  `ArgumentException` at construction; received headers take the lenient `Headers.Builder.AddInbound` path.
- **A conformance assertion needs a negative control, and a waiver must stay needed.** Every assertion in `Dexpace.Sdk.Conformance` has a deliberately broken subject in
  `tests/Dexpace.Sdk.Conformance.Tests/Controls/` that it must fail (`AssertionCatalogueTests` fails an assertion with none); a leaked-socket control must root what it leaks, because a finalizer
  closes it otherwise. A `ConformanceWaiver` whose assertion now passes fails the run, so fixing a requirement means deleting its waiver in the same change. The kit never calls `Task.Delay`, and a release check
  needs a keep-alive reply, never a `Connection: close` one.
- **An invariant hook left `null` is `NotExercised`, and a driver pins the list it expects.** The invariant suite reads each subject through a hook that defaults to core; four have no default (`MapStrict`, `CreateCyclicException`, `CreateBoundedMap`,
  `SeamResolution`) and an assertion whose hook is absent reports `NotExercised`, which is not a pass. Each first-party driver (`InvariantConformanceTests` in the core tests, `InvariantResolutionConformanceTests` in the DI tests, `CheckPhase10Async` in the AOT smoke) asserts the exact
  list it leaves unexercised and fails on any other, so supplying a hook means deleting its name from the pin. A new `ConcurrentDictionary` constructor or `Exception.GetBaseException` in `src/` fails `RS0030` (`BoundedMap` is the one construction site, under one scoped pragma). A suite
  must walk a cyclic exception chain and never throw one (Windows hangs), and the repository gates under `tests/Dexpace.Sdk.Conformance.Tests/Gates/` (license header on every `.cs`, a re-enable condition on every analyzer relaxation, every CI gate a blocking step) fail in the pull request that breaks them (`docs/sdk-documentation/cross-cutting-invariants.md`).
- **Host integration is configured by a reader, not the binder, and fails at start naming the key.** `Dexpace.Sdk.Extensions.DependencyInjection` reads the `IConfigurationSection` the caller hands `AddDexpaceClient` with its own grammars
  (`ConfigurationBinder` is banned in `src/`: the generator binds no `init` accessor, the reflection mode is not AOT-safe); every bad value, rejected setting and unknown key fails `ValidateOnStart` at once as `"<path>: <rule>"`, never quoting a configured
  value (a numeric accessor message may echo the number), and with no host `IStartupValidator.Validate()` or the first resolution fails the same way. An empty value is absent and falls through to the next provider (`CFG-2`); there is no reload; a codec is required
  (`UseSerde`); dispose the provider with `DisposeAsync` (a synchronous dispose throws for an async-only transport); no configuration value turns the environment proxy read on. Compose `AddStandardResilienceHandler` with
  `o.Retry.ShouldHandle = _ => ValueTask.FromResult(false)`, never with the SDK's `MaxRetryAttempts = 0` (`docs/sdk-documentation/dependency-injection.md`).
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
client-identity steps, `ExceptionFacts` and `ExceptionTrail` — see `docs/sdk-documentation/recovery.md`; and phase 4c's
pipeline rework — the request-in/response-out policy signature, the call-scoped `PipelineContext`, the builder rules, `HttpPipeline` as a transport,
`ErrorMappingPolicy` and the real sync path — see `docs/sdk-documentation/pipelines.md`; and phase 5a's configuration — the options as sealed records, `ProxyOptions`
and `FromEnvironment`, `TimeProviderWaits`, `HttpDate`, `BuildInfo` and the clock, delay and environment bans — see `docs/sdk-documentation/configuration.md`;
and phase 5b's logging and redaction — `HttpLoggingOptions`,
the `http.request`/`http.response` events, header and URL redaction, the emission guard and body previews — see
`docs/sdk-documentation/logging-and-redaction.md`; and phase 5c's tracing and metrics — the operation span, the attempt span rework, the
`dexpace.*` span events, the two instruments with their stable attribute sets and `SystemNetHttpClient`'s `traceparent` rule — see
`docs/sdk-documentation/tracing-and-metrics.md`; and phase 6a's retry — the classifier and `IRetryableError`, the re-send gate, `RetryOptions`' validation, the one `RetryEngine` under `RetryPolicy` and `RetryRecovery`, the pacing
headers, the suppressed trail, `OperationTimeoutException` and the enforced `AttemptTimeout` — see `docs/sdk-documentation/retry.md`; phase 6b's redirect —
the pure decider behind `RedirectPolicy`, `RedirectOptions` (`AllowedMethods`, `FollowSeeOther`, `Predicate`), the `RedirectException` family, loop detection, the five
`http.redirect.*` events and the end-to-end credential-leak tests — see `docs/sdk-documentation/redirect.md`; and phase 6c's authentication — the
descriptor and resolver with their `RequestOptions` tiers, the redacting credentials, the lenient challenge parser, the `401` lifecycle in `AuthorizationPolicy`, the
bounded token cache with its background refresh, Digest with the MD5 probe, and `MultiSchemeAuthPolicy` — see `docs/sdk-documentation/auth.md`; and phase 7a's
serde — `Tristate<T>` and its System.Text.Json wiring, `CreateDefaultOptions`, the options-copying constructors, the streaming typed readers, the two response handlers and
`TypedResponse<T>` — see `docs/sdk-documentation/serde.md`; and phase 7b's server-sent events —
the 1 MiB line-capped reader over the WHATWG line reader, the immutable `ServerSentEvent`, the `ServerSentEventStream` facade that owns a response (four single-use views, one release rule per path) and the typed
`MapAsync` / `Map` adapter — see `docs/sdk-documentation/sse.md`; and phase 7c's pagination — the
`IPageStrategy` contract and `PageInfo<T>`, the three factories (`Pageable.Create`, `CreateBlocking`, `FromFetchers`), the single-use page view, the `Link` strategy's cross-origin guard and the fetcher
form — see `docs/sdk-documentation/pagination.md`; and phase 8a's transport conformance kit, `Dexpace.Sdk.Conformance` — 43 named assertions for the `TRANSPORT` and `ASYNC` requirements,
the six result statuses and the by-ID waivers that must stay needed, the loopback `Wire` fixture promoted into it, the two drivers (the reference transport and a test-only raw-socket client) and the requirement
catalogue generated from appendix C — see `docs/sdk-documentation/conformance.md`; and phase 8b's hardening of the reference transport — the options record and four constructors, the handler check, the
installed proxy, the per-call deadline with its one failure classification, the real synchronous send, the header partition and drop policy, the bounded re-send capture and the `ObjectDisposedException` latch, with the kit green against both constructions and, at phase 8b, two permanent assertion-scoped
waivers (`TRANSPORT-14`'s malformed-name clause and `TRANSPORT-2`'s native retry for a body-less `GET`/`HEAD`/`OPTIONS`/`DELETE`/`TRACE`, design §10 entries 32 and 33; phase 11 retired the second, so only entry 32's remains) — see `docs/sdk-documentation/transport.md`; and phase 9's host integration — the DI package `Dexpace.Sdk.Extensions.DependencyInjection`: `AddDexpaceClient` and the builder, the hand-written configuration reader and its grammars, the validator with `ValidateOnStart`, the named client over
`IHttpClientFactory` with its redirect-off primary and container-owned lease, the single-registration check, `DexpaceConfigurationKeys`, the audit's policy record, the third conformance driver — see `docs/sdk-documentation/dependency-injection.md`; and phase 10's cross-cutting invariants — the kit's invariant (28), codec (11) and packaging (8) suites with a negative control per assertion and three first-party drivers, `TransportFace.None`, the one combined report, the packaging and
requirement-coverage scripts, the appendix-B map and its gate, the cross-path reproducible pack, the `NFR-11`, `NFR-4`, license-header, analyzer-waiver and CI-completeness tests, the `GetBaseException` and `ConcurrentDictionary` constructor bans and `CheckPhase10Async`, with `NFR-17`'s repository ruleset the one open action — see
`docs/sdk-documentation/cross-cutting-invariants.md`; and phase 11's deviation audit — every design §10 entry and §11 reading re-measured against the code in `docs/deviations.md`, the 3 counted unmet clauses
(`AUTH-15`, `PAGE-21`, `TRANSPORT-14`'s name half), the repairs (transport failures derive from `IOException`, a second open returns the same stream, the plaintext filter that ends the native re-send, an empty configuration value that falls through,
`WaitOrDisposeAsync`, the bounded SSE event, the `Uri` check), the `LedgerAuditTests` gate and the generated design §12 table — see `docs/deviations.md` and `docs/work/mvp/phase11/`): only the release path (12).
