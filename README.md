# dexpace .NET SDK

The .NET counterpart to [`dexpace/java-sdk`](https://github.com/dexpace/java-sdk) and
[`dexpace/python-sdk`](https://github.com/dexpace/python-sdk). It is an **HTTP-client
toolkit, not an HTTP client**: it provides immutable HTTP models, a transport SPI, a staged pipeline
with retry, redirect, auth and instrumentation policies, and pagination, with SSE and more to come.
Consuming libraries plug in a concrete transport via the `IHttpClient` / `IAsyncHttpClient` interfaces.

The public API follows .NET idioms — `record` and `readonly record struct` for immutable models,
interfaces for SPIs, `Task` / `IAsyncDisposable` for the async-first surface, and
`System.Net.Http` as the reference transport — while keeping the same architectural shape as the
Java and Python ports.

## Status

Pre-release: nothing is published yet, and every package is at `0.0.1-alpha.1`. Three NuGet packages are built:

| Package | What it is |
|---|---|
| [`Dexpace.Sdk.Core`](src/Dexpace.Sdk.Core/README.md) | The toolkit: HTTP models, bodies, the transport SPI, errors, the staged pipeline and its policies (operation timeout, redirect, retry, idempotency, `Date`, client identity, instrumentation), auth (credentials, token cache, auth policies), pagination, options, diagnostics and the `ISerde` seam |
| [`Dexpace.Sdk.Http.SystemNet`](src/Dexpace.Sdk.Http.SystemNet/README.md) | The reference transport over `System.Net.Http.HttpClient` |
| [`Dexpace.Sdk.Serialization.SystemTextJson`](src/Dexpace.Sdk.Serialization.SystemTextJson/README.md) | The `ISerde` codec over source-generated `System.Text.Json` |

What is not built yet — the domain-model rework (query parameters, request options and conditions), file,
form and multipart bodies, the execution-context and recovery chains, layered configuration and body logging,
RFC 7235 challenges and Digest auth, tri-state PATCH, SSE, the transport conformance kit and the DI package — is
scheduled by the [v1 roadmap](docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md), and what the first release needs is
in [docs/first-release.md](docs/first-release.md). The normative specification is
[docs/product-spec/](docs/product-spec.md) and the .NET design is [docs/sdk-design-dotnet/](docs/sdk-design-dotnet.md);
[docs/README.md](docs/README.md) indexes the rest.

## Layout

```
dotnet-sdk/
├── Dexpace.Sdk.sln
├── Directory.Build.props        # shared compiler, analyzer, library-gate and package settings
├── Directory.Build.targets      # packs each library README; pins the ILLink pack
├── Directory.Packages.props     # central package versions (single source of truth)
├── BannedSymbols.txt            # banned-API list for src/
├── .editorconfig                # formatting + analyzer severities
├── global.json                  # pinned .NET SDK + the Microsoft.Testing.Platform runner
├── src/
│   ├── Dexpace.Sdk.Core/                        # toolkit; no transport, no concrete codec
│   │   ├── Http/Common/             # Method, Protocol, MediaType, HttpHeaderName, Headers
│   │   ├── Http/Request/            # Request, RequestBody
│   │   ├── Http/Response/           # Response, ResponseBody, Status
│   │   ├── Client/                  # IHttpClient, IAsyncHttpClient, bridges
│   │   ├── Pipeline/                # HttpPipeline, PipelineBuilder, DexpacePipeline, Policies/
│   │   ├── Auth/                    # credentials and the access-token cache
│   │   ├── Pagination/              # AsyncPageable<T>, Page<T>, strategies
│   │   ├── Configuration/           # DexpaceClientOptions and sub-options
│   │   ├── Diagnostics/             # ActivitySource, Meter, UrlRedactor
│   │   ├── Serialization/           # the ISerde seam
│   │   └── Errors/                  # SdkException hierarchy
│   ├── Dexpace.Sdk.Http.SystemNet/              # reference transport over System.Net.Http.HttpClient
│   └── Dexpace.Sdk.Serialization.SystemTextJson/ # ISerde over source-generated System.Text.Json
├── tests/
│   ├── Dexpace.Sdk.Core.Tests/                  # core against in-memory fakes only
│   ├── Dexpace.Sdk.Http.SystemNet.Tests/        # the transport, incl. loopback wire tests
│   ├── Dexpace.Sdk.Serialization.SystemTextJson.Tests/
│   ├── Dexpace.Sdk.TestSupport/                 # shared fakes (not packed)
│   └── Dexpace.Sdk.AotSmoke/                    # NativeAOT smoke consumer
├── scripts/ci/                  # coverage gate, dependency audit, reproducible pack
├── tools/Dexpace.Tools.sln      # repository tools (knowledge CLI, housekeeping skill) and their tests
└── docs/
```

## Build & test

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release     # the build IS the lint gate (warnings-as-errors)
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
```

Requires the .NET SDK pinned in `global.json` (`10.0.401`, rolling forward to the latest patch). Every project
targets `net10.0`. Tests are xUnit v3 on Microsoft.Testing.Platform, so `dotnet test` takes `--solution` /
`--project` and the platform's options. [CONTRIBUTING.md](CONTRIBUTING.md) lists every CI gate and how to run it
locally.

## Quick start

```csharp
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();
using var pipeline = DexpacePipeline.CreateDefault(transport);   // redirect, retry, instrumentation, …

using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/health"), new DexpaceClientOptions(), CancellationToken.None);

if (response.IsSuccess)
{
    Console.WriteLine(await response.Body.ReadAsStringAsync());
}
```

## Conventions

These are enforced by the build (`TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`,
`EnforceCodeStyleInBuild`), `.editorconfig` and CI; [CLAUDE.md](CLAUDE.md) has the full list.

- **`net10.0`, C# `latest`, nullable on, implicit usings off** (a committed `GlobalUsings.cs` per project).
- **Immutable models.** `record` / `readonly record struct`; mutate via `with` expressions or the
  `With*` helpers. No builders-as-objects — C# object initializers and `with` cover it.
- **Interfaces for SPIs.** `IHttpClient`, `IAsyncHttpClient` and `ISerde` are the seams; core ships
  no transport and no concrete codec of its own.
- **Async-first, deterministic cleanup.** Bodies, responses, and transports implement
  `IDisposable` / `IAsyncDisposable`; single-use bodies throw on a second read. Library code awaits with
  `ConfigureAwait(false)`.
- **Core's one runtime dependency is `Microsoft.Extensions.Logging.Abstractions`.** Otherwise it builds
  against the shared framework only; adapters add at most one third-party library each.
- **Narrow public API, fully documented, and locked.** Every public member carries a `///` doc comment, and
  every change to the surface is a reviewed `PublicAPI.Unshipped.txt` diff.
- **MIT license header on every source file.**

## License

MIT — see [LICENSE](LICENSE).
