# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Security

Roadmap phase 1, defects S4–S8, each pinned by a `[Trait("Category", "Security")]` regression test in
`tests/Dexpace.Sdk.Core.Tests/Security/`.

- **Credentials are never sent over plain `http`** (S4; `AUTH-28`, `XCUT-16`). Every `AuthorizationPolicy`
  (`BasicAuthPolicy`, `ApiKeyAuthPolicy`, `BearerTokenAuthPolicy`) now throws a non-retryable `SdkException` naming
  the policy and the scheme when it would attach a credential to a non-`https` URL, before the credential is resolved.
  There is no loopback exemption: `http://localhost` is refused too. A cross-origin redirect hop, which carries no
  credential, is not checked.
- **`UrlRedactor` is default-deny** (S5; `OBS-11`–`OBS-15`, `XCUT-19`). Every query value and fragment `key=value`
  token becomes `***` unless its name is allow-listed; the default allow-list is exactly `{api-version}`. Userinfo
  becomes `***:***@` instead of being removed; plain fragments, value-less parameters and an empty `?` are kept; nothing
  is re-encoded. **Breaking:** `UrlRedactor.DefaultSensitiveParams` is replaced by `DefaultQueryAllowList`, and the
  `UrlRedactor(IEnumerable<string>)` constructor now takes the allow-list (`queryAllowList`) rather than a deny-list.
  New `UrlRedactor.Redact(string)` returns the sentinel `[malformed url]` for text that is not a well-formed URI
  reference. Span tags and log lines from `InstrumentationPolicy` change accordingly (`REDACTED` becomes `***`).
- **A retry or redirect no longer re-sends what a downstream policy stamped** (S6; `RETRY-44`, `PIPE-16`).
  `RetryPolicy` and `RedirectPolicy` restore the request they hold before every re-drive, so attempt n+1 and hop n+1
  no longer enter the chain carrying attempt n's `Authorization` header.
- **A huge `Retry-After` no longer crashes the call** (S7; `RETRY-18`, `RECOV-26`). Every retry delay, hinted or
  computed, is clamped to 365 days, and a wait longer than `Task.Delay` accepts (~49.7 days) runs as successive shorter
  waits instead of throwing `ArgumentOutOfRangeException`.
- **`Response.EnsureSuccessAsync` maps only 400–599** (S8; `BODY-31`, `RECOV-15`, `HTTP-52`, `BODY-30`). A 304, an
  unfollowed 3xx, a 1xx or a status outside 100–599 no longer throws, and its body is left intact. For an error, the
  buffered body on the `HttpResponseException` (still capped at 1 MiB) can now be read more than once, and the original
  response is disposed, even when draining its body fails. **Breaking:** after catching the exception, read the error
  body from `HttpResponseException.Response`, not from the original response.

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
