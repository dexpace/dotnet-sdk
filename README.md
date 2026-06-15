# dexpace .NET SDK

The .NET counterpart to [`dexpace/java-sdk`](https://github.com/dexpace/java-sdk) and
[`dexpace/python-sdk`](https://github.com/dexpace/python-sdk). It is an **HTTP-client
toolkit, not an HTTP client**: it provides immutable HTTP models, a transport SPI, and (over
time) a staged pipeline, auth, SSE, pagination, and instrumentation. Consuming libraries plug in
a concrete transport via the `IHttpClient` / `IAsyncHttpClient` interfaces.

The public API follows .NET idioms — `record` and `readonly record struct` for immutable models,
interfaces for SPIs, `Task` / `IAsyncDisposable` for the async-first surface, and
`System.Net.Http` as the reference transport — while keeping the same architectural shape as the
Java and Python ports.

## Status

Alpha. This is the **foundation slice**: the multi-project layout plus a working vertical slice of
`Dexpace.Sdk.Core` (HTTP common models, request/response, bodies, the transport SPI, and the
exception hierarchy) and one reference transport. Pipeline, context chain, auth, SSE, pagination,
and instrumentation are planned — see [docs/architecture.md](docs/architecture.md).

## Layout

```
dotnet-sdk/
├── Dexpace.Sdk.sln
├── Directory.Build.props        # shared compiler + package settings (the libs.versions analog)
├── Directory.Packages.props     # central package versions (single source of truth)
├── .editorconfig                # formatting + analyzer severities
├── global.json                  # pinned .NET SDK
├── src/
│   ├── Dexpace.Sdk.Core/            # toolkit; no transport
│   │   ├── Http/Common/             # Method, Protocol, MediaType, HttpHeaderName, Headers
│   │   ├── Http/Request/            # Request, RequestBody
│   │   ├── Http/Response/           # Response, ResponseBody, Status
│   │   ├── Client/                  # IHttpClient, IAsyncHttpClient, bridges
│   │   └── Errors/                  # SdkException hierarchy
│   └── Dexpace.Sdk.Http.SystemNet/  # reference transport over System.Net.Http.HttpClient
└── tests/
    └── Dexpace.Sdk.Core.Tests/      # xUnit suite
```

## Build & test

```bash
dotnet restore
dotnet build   --configuration Release    # build IS the lint gate (warnings-as-errors)
dotnet test    --configuration Release
```

Requires the .NET SDK pinned in `global.json` (10.0.100, rolling forward to the latest feature
band). The library projects target `net8.0`.

## Quick start

```csharp
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();

var request = Request.Get("https://api.example.com/health");
await using var response = await transport.ExecuteAsync(request);

if (response.IsSuccess)
{
    Console.WriteLine(await response.Body.ReadAsStringAsync());
}
```

## Conventions

These are enforced by the build (`TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`,
`EnforceCodeStyleInBuild`) and `.editorconfig`:

- **net8.0 libraries, C# `latest`, nullable + implicit usings on.** Modern idioms: records,
  `readonly record struct`, file-scoped namespaces, pattern matching, `init` accessors.
- **Immutable models.** `record` / `readonly record struct`; mutate via `with` expressions or the
  `With*` helpers. No builders-as-objects — C# object initializers and `with` cover it.
- **Interfaces for SPIs.** `IHttpClient`, `IAsyncHttpClient` are the transport seams; `core` ships
  no transport of its own.
- **Async-first, deterministic cleanup.** Bodies, responses, and transports implement
  `IDisposable` / `IAsyncDisposable`; single-use bodies throw on a second read.
- **No runtime dependencies in `core`.** It builds against the BCL only; transports adapt one HTTP
  library each.
- **Narrow public API, fully documented.** Every public member carries a `///` doc comment
  (`GenerateDocumentationFile` is on). Implementation helpers are `internal`.
- **MIT license header on every source file.**

## License

MIT — see [LICENSE](LICENSE).
