# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository

The .NET counterpart to [`dexpace/java-sdk`](https://github.com/dexpace/java-sdk) and
[`dexpace/python-sdk`](https://github.com/dexpace/python-sdk). The architecture follows the same
shape (immutable HTTP models, transport SPI, body abstractions, typed errors) but the public API
uses .NET idioms — `record` / `readonly record struct` instead of builder objects, `interface`
instead of Kotlin `fun interface` / Python `Protocol`, `IDisposable` / `IAsyncDisposable` instead
of `AutoCloseable` / context managers, `Task<T>` as the async contract. The pluggable I/O seam that
exists in the Java SDK (`IoProvider` over Okio) was intentionally **not** ported: .NET's
`System.IO.Stream`, `Memory<byte>`, and `IAsyncDisposable` cover the same surface natively, exactly
as the Python port leans on `bytes` / `BinaryIO`.

## Build & test (from the repository root)

```bash
dotnet restore
dotnet build  --configuration Release   # the build IS the lint gate (warnings-as-errors)
dotnet test   --configuration Release
dotnet format --verify-no-changes       # formatting gate (uses .editorconfig)
```

The .NET SDK is pinned in `global.json` (10.0.100, `rollForward: latestFeature`). Library projects
target `net8.0`.

## Conventions (enforced — match these when adding code)

- **net8.0 libraries, C# `latest`, `Nullable` + `ImplicitUsings` enabled.** Modern idioms: file-scoped
  namespaces, records, `readonly record struct`, pattern matching, `init` accessors, collection
  expressions where they fit.
- **`TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended` + `EnforceCodeStyleInBuild`.** The
  build is the lint gate. Rule severities live in `.editorconfig`; a handful of analyzer rules are
  deliberately dialled down there with a documented rationale (CA1308 lower-casing, CA1054/55/56
  string URLs, CA1062, CA2007) — do not silence others without justification.
- **Immutable models.** `record` / `readonly record struct`; mutate via `with` expressions or `With*`
  helpers. No builder-as-object types — object initializers and `with` make them redundant. `Headers`
  is the one mutable-builder exception (`Headers.Builder`) for batched edits.
- **Interfaces for SPIs.** `IHttpClient`, `IAsyncHttpClient` are the transport seams.
  `Dexpace.Sdk.Core` ships **no** transport; transports adapt one HTTP library each and live in their
  own project (`Dexpace.Sdk.Http.*`).
- **Deterministic cleanup.** `Response`, `ResponseBody`, and transports implement `IDisposable` /
  `IAsyncDisposable`. Single-use bodies (stream-backed) throw `StreamConsumedException` on a second
  read; call `RequestBody.ToReplayableAsync()` before the first send if retries are needed.
- **No runtime dependencies in `core`.** It builds against the BCL only. `SourceLink` is the only
  build-time package. Transports may depend on their HTTP library; `core` may not.
- **Narrow, fully-documented public API.** `GenerateDocumentationFile` is on, so every public member
  needs a `///` XML doc comment (missing docs are CS1591 → build error). Implementation helpers are
  `internal` (with `InternalsVisibleTo` for the test and transport assemblies).
- **Central package versions.** `Directory.Packages.props` is the single source of truth (the
  `libs.versions.toml` analog). `PackageReference`s carry no `Version` attribute.
- **MIT license header on every `.cs` file** — the two-line block, src and tests alike:

  ```csharp
  // Copyright (c) 2026 dexpace and Omar Aljarrah.
  // Licensed under the MIT License. See LICENSE in the repository root for details.
  ```

- **Commit style:** `chore:` for refactors/cleanup; `feat:` for new features; `fix:` for bug fixes;
  `docs:` for documentation-only changes.

## Repository Layout

A single solution (`Dexpace.Sdk.sln`) with central build/package configuration at the root. Each
distribution is its own project under `src/`; tests under `tests/`.

```
dotnet-sdk/
├── Dexpace.Sdk.sln
├── Directory.Build.props            # shared compiler + package metadata
├── Directory.Packages.props         # central package versions
├── .editorconfig                    # formatting + analyzer severities
├── global.json                      # pinned .NET SDK
├── nuget.config
├── docs/architecture.md
└── src/
    ├── Dexpace.Sdk.Core/            # toolkit; no transport, BCL-only
    │   ├── Http/Common/             # Method, Protocol, MediaType, CommonMediaTypes,
    │   │                            # HttpHeaderName, Headers
    │   ├── Http/Request/            # Request, RequestBody
    │   ├── Http/Response/           # Response, ResponseBody, Status
    │   ├── Client/                  # IHttpClient, IAsyncHttpClient, HttpClientExtensions
    │   └── Errors/                  # SdkException + ServiceRequest/Response, HttpResponse,
    │                                # streaming, serialization, pipeline exceptions
    └── Dexpace.Sdk.Http.SystemNet/  # reference transport over System.Net.Http.HttpClient
└── tests/
    └── Dexpace.Sdk.Core.Tests/      # xUnit suite (references core + transport)
```

## Architecture — Big Picture

The SDK is an **HTTP-client toolkit, not an HTTP client**. `Dexpace.Sdk.Core` provides abstractions,
models, and (over time) pipelines; consuming libraries plug in a concrete transport via
`IHttpClient` / `IAsyncHttpClient`.

Layered, bottom-up:

1. **Bodies** — `RequestBody.WriteToAsync(Stream)` is the outgoing streaming surface;
   `ResponseBody.OpenReadAsync` / `ReadAsBytesAsync` / `ReadAsStringAsync` drain the incoming side.
   Bytes/string bodies are replayable; stream bodies are single-use.
2. **HTTP value models** (`Http/Common`, `Http/Response/Status`) — immutable, case-insensitive
   `Headers` multimap; `MediaType` with quote-aware parse/round-trip; `Method`, `Protocol`, `Status`
   value types with well-known instances.
3. **Request / Response** — `Request` is an immutable `record` (absolute `Uri`); `Response` is a
   disposable carrier of status/headers/body/protocol.
4. **Transport SPI** (`Client`) — async-first `IAsyncHttpClient` plus a synchronous `IHttpClient`,
   with `AsAsync` / `AsBlocking` bridges.
5. **Errors** — `SdkException` roots the hierarchy: `ServiceRequestException` (never sent, retry-safe
   on idempotent methods), `ServiceResponseException` (sent, response unreadable),
   `HttpResponseException` (4xx/5xx received intact), plus lifecycle/serialization/pipeline failures.

## Things That Will Bite You

- **The build is the lint gate.** A missing `///` doc comment on a public member, an unused `using`,
  or an unsuppressed analyzer finding fails the build (`TreatWarningsAsErrors`). Build before
  declaring done.
- **`Dexpace.Sdk.Core` must stay BCL-only.** Do not add a runtime `PackageReference` to it — model
  third-party needs behind an interface and implement them in an adapter project.
- **Single-use bodies throw on second consumption.** `RequestBody.FromStream` /
  `ResponseBody.FromStream` raise `StreamConsumedException` the second time. Buffer first
  (`ToReplayableAsync`) when retries are in play.
- **Transports are ownership-aware.** A caller-supplied `System.Net.Http.HttpClient` is never disposed
  by `SystemNetHttpClient`; only an internally created one is.
- **Central Package Management is on.** Add new dependency versions to `Directory.Packages.props`, and
  reference them without a `Version` attribute.

## Planned (not yet implemented)

Mirroring the Java/Python ports: pipeline (staged policies — redirect, retry, idempotency, set-date,
client-identity, logging, tracing), context promotion chain, auth (token credentials, bearer/basic,
RFC 7235 challenges), SSE, pagination, webhooks, and instrumentation. See `docs/architecture.md`.
