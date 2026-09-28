# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **The target-framework floor is now `net10.0` only** (roadmap decision D1), for every package; `net8.0` is no
  longer targeted. `Microsoft.Extensions.Logging.Abstractions` moves to the 10.0.x band (10.0.12), which drops the
  `System.Diagnostics.DiagnosticSource` package from core's dependency closure.
- Build: `global.json` pins SDK `10.0.401` with `rollForward: latestPatch`; `ImplicitUsings` is off, with a
  committed `GlobalUsings.cs` per project; every project commits a `packages.lock.json`.

### Added

- Quality gates (design §9): public-API files (`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`) per package,
  banned-API list (`BannedSymbols.txt`), license-header, method-length (`MA0051`), naming, `CA1031`, `CA2000` and
  `CA2007` rules; trim/AOT analyzers and package validation on every library; a cross-OS CI matrix with locked
  restore, `dotnet format`, an 80% line-coverage floor, a dependency audit, a reproducible-pack check and a
  NativeAOT smoke consumer (`tests/Dexpace.Sdk.AotSmoke`).
- Initial repository structure: `Dexpace.Sdk.sln`, central build/package configuration
  (`Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json`),
  `.gitignore`, and a GitHub Actions CI workflow (build + test).
- `Dexpace.Sdk.Core` foundation slice:
  - `Http/Common`: `Method`, `Protocol` (+ wire-form conversions), `MediaType` (+ `CommonMediaTypes`),
    `HttpHeaderName` (+ well-known names), and the immutable case-insensitive `Headers` multimap.
  - `Http/Request`: `Request` and the `RequestBody` abstraction (bytes / string / stream factories,
    replayability).
  - `Http/Response`: `Response`, the `ResponseBody` abstraction, and `Status` (+ well-known codes).
  - `Client`: `IHttpClient` / `IAsyncHttpClient` transport SPIs and sync/async bridges.
  - `Errors`: the `SdkException` hierarchy.
- `Dexpace.Sdk.Http.SystemNet`: reference transport adapting `System.Net.Http.HttpClient` to the SPI.
- `Dexpace.Sdk.Core.Tests`: xUnit coverage for media types, headers, methods, statuses, bodies,
  request building, and the transport.

[Unreleased]: https://github.com/dexpace/dotnet-sdk/commits/main
