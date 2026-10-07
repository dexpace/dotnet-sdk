# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Security

Roadmap phase 1, defects S1–S9, each pinned by a `[Trait("Category", "Security")]` regression test in
`tests/Dexpace.Sdk.Core.Tests/Security/` and, for the wire-level proofs of S1, S2, S3 and S9, in
`tests/Dexpace.Sdk.Http.SystemNet.Tests/Security/` against the loopback server.

- **Header CR/LF injection is refused before the wire** (S1; `HTTP-17`, `HTTP-18`, `HTTP-20`, `HTTP-26`, `XCUT-18`,
  `TRANSPORT-12`). `Headers.With`, `Headers.Set`, `Headers.Builder.Add`, `Headers.Builder.Set` (and so
  `Request.WithHeader`) trim surrounding SP/HTAB from the name, require an RFC 9110 token name, and accept only HTAB
  and printable ASCII in the value; `HttpHeaderName.Of` trims and validates the same way; `MediaType.Of` and
  `MediaType.Parse` apply the value rule to parameter values, and `MediaType.Parse` trims only spaces and tabs, so a
  CR, LF or Unicode whitespace around its parts is rejected. A rejection is an `ArgumentException` naming the
  offending character by code point (`U+000D`) that never echoes the value. `SystemNetHttpClient` re-checks every
  header at the wire boundary and drops one that fails, with a warning naming it. New
  `Headers.Builder.AddInbound(string, string)` is the lenient path for received headers (obs-text allowed, controls
  still rejected), and the transport uses it, dropping a response header that carries a control character.
  **Breaking:** header names and values, and media-type parameter values, that were accepted before (CR, LF, NUL,
  other controls, DEL, non-ASCII, a non-token name such as `X Trace`) now throw `ArgumentException`; so does a policy
  (`ApiKeyAuthPolicy`, `ClientIdentityPolicy`, …) stamping such a value. Error messages from `MediaType.Parse` no
  longer quote the input.
- **A caller-set `Host` and the framing headers never reach the wire** (S2; `TRANSPORT-11`). `SystemNetHttpClient`
  drops `Host`, `Content-Length`, `Transfer-Encoding`, `Connection`, `Keep-Alive`, `Upgrade`, `TE` and `Expect` from
  the request, so `HttpClient` computes them, with a `Debug` log entry naming each dropped header (never its value).
  New constructors `SystemNetHttpClient(ILogger)` and `SystemNetHttpClient(HttpClient, ILogger)` take the logger.
- **The SDK is the only redirect authority** (S3; `TRANSPORT-1`, `REDIR-7`, `REDIR-8`, `REDIR-9`, `REDIR-12`,
  `XCUT-17`). `new SystemNetHttpClient()` now builds its `HttpClient` over `SocketsHttpHandler { AllowAutoRedirect =
  false }` and returns a 3xx as is, so `RedirectPolicy` sees it. A caller-supplied `HttpClient` that follows a redirect
  anyway is detected (the final request URI differs from the one sent in scheme, host, port or path; a handler that
  only rewrites the query is not mistaken for one), its response disposed, and a non-retryable `SdkException` naming `AllowAutoRedirect` thrown (the
  client itself is still never disposed). `RedirectPolicy` strips `Authorization` before every hop, same-origin
  included; judges cross-origin against the seed request rather than the previous hop; strips `Cookie` and
  `Proxy-Authorization` on a cross-origin hop; and drops userinfo from the `Location` target. **Breaking:** the
  parameterless transport no longer follows redirects; a same-origin redirect hop no longer carries `Authorization`
  (the auth policy re-stamps it); `RedirectOptions.StripSensitiveHeadersOnCrossOrigin` no longer has any effect
  (stripping always applies); and a borrowed client that follows redirects now fails the call.
- **A malformed inbound `Content-Type` no longer fails the response or leaks it** (S9; `TRANSPORT-27`,
  `TRANSPORT-22`). New `MediaType.TryParse(string?, out MediaType?)`; the transport uses it, so an unparseable
  `Content-Type` (`text/plain; foo`) means `ResponseBody.ContentType` is `null` instead of an `ArgumentException`, and
  the native `HttpResponseMessage` is disposed if adapting the response throws for any reason.

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

- **Breaking:** `IHttpClient.Execute(Request)` is now `Execute(Request, RequestOptions, CancellationToken)` and
  `IAsyncHttpClient.ExecuteAsync(Request, CancellationToken = default)` is now
  `ExecuteAsync(Request, RequestOptions, CancellationToken)`, with no parameter defaults; `SystemNetHttpClient` takes
  the same signatures. Callers that import `Dexpace.Sdk.Core.Client` keep calling `ExecuteAsync(request, ct)` through
  the new extension; every implementer changes (`SEAM-11`, `SEAM-13`).
- **Breaking:** `AsAsync(this IHttpClient)` is now `AsAsync(this IHttpClient, TaskScheduler)`; neither bridge disposes
  the client it wraps any more; a response produced by `AsAsync` after the call's token is signalled is disposed and
  the call completes cancelled (`SEAM-14`, `SEAM-18`, `SEAM-30`).
- **Breaking (behaviour):** a transport returning `null` now fails at the pipeline runner with
  `PipelineAbortedException`, before any policy sees a `null` response (`SEAM-16`).
- Build: `BannedSymbols.txt` bans `TaskCompletionSource<T>.SetResult` / `TrySetResult` and `Task<T>.WaitAsync` in `src/`
  (`SEAM-30`); no public surface changes.
- **Breaking:** `SerializationException` and `DeserializationException` are unsealed and derive from the new abstract
  `SerdeException` (was: sealed, deriving from `SdkException`). Source-compatible at every throw and catch site;
  binary-incompatible for a caller compiled against the sealed types (`SEAM-23`).
- **Breaking:** `Response`'s constructor is now `(Request, Status, Protocol, Headers?, ResponseBody?, string?)` and
  `protocol` has no default (`HTTP-4`, `HTTP-6`); `Response` gains `Request`, `ReasonPhrase`, `IsRedirect`,
  `IsClientError`, `IsServerError`, `IsError`, `IsInformational` and `WithBody`.
- **Breaking:** `Request`'s `Method`, `Url`, `Headers` and `Body` are get-only, so `with { … }` no longer compiles:
  use `WithMethod`, `WithUrl`, `WithHeaders`, `WithBody`, `WithoutBody`; `Request` rejects a body on GET, HEAD, TRACE
  and CONNECT (`HTTP-7`); equality uses `Url.AbsoluteUri` ordinally, `Headers` by value, and in-memory bodies by bytes
  (`HTTP-46`); `ToString()` prints the method and the redacted URL; a URL error carries the redacted input
  (`HTTP-47`); `RedirectPolicy` returns a 3xx whose `Location` is not http(s) unfollowed instead of sending the hop.
- **Breaking:** `HttpHeaderName` is a `sealed record` (was a `readonly record struct`) and `ToString()` returns
  `Original`; `Headers` enumerates and lists the original casing in insertion order, `Names` is an
  `IReadOnlyList<string>`, `Set` takes `string?` and `null` removes the header, `Headers` has value equality, and a
  non-ASCII lookup name no longer folds (`HTTP-13`, `HTTP-14`–`HTTP-16`, `HTTP-21`); `ApiKeyCredential`'s
  `HttpHeaderName? header` parameter is now a nullable reference rather than `Nullable<HttpHeaderName>`.
- **Breaking:** `Method` is a `sealed record` (was a `readonly record struct`); `Method.IsSafe` and
  `Method.IsIdempotent` are no longer public; `Method.Of` rejects a non-token with `ArgumentException`; `RetryPolicy`
  no longer retries TRACE (`HTTP-9`).
- **Breaking:** `MediaType.Parse` rejects a parameter with an empty raw value (`a=`); `MediaType.Charset` returns
  `null` for `utf-7` instead of throwing (`HTTP-24`, `HTTP-53`). `Protocol.Parse` folds case with ASCII rules
  (`HTTP-33`).
- **The target-framework floor is now `net10.0` only** (roadmap decision D1), for every package; `net8.0` is no
  longer targeted. `Microsoft.Extensions.Logging.Abstractions` moves to the 10.0.x band (10.0.12), which drops the
  `System.Diagnostics.DiagnosticSource` package from core's dependency closure.
- Build: `global.json` pins SDK `10.0.401` with `rollForward: latestPatch`; `ImplicitUsings` is off, with a
  committed `GlobalUsings.cs` per project; every project commits a `packages.lock.json`.
- `DexpaceClientOptions.BaseAddress` is now read by `OperationDescriptor.BuildRequest(DexpaceClientOptions)`;
  `DexpaceClientOptions.AttemptTimeout` still documents that nothing reads it yet (roadmap phase 6a wires it).
- **Breaking:** `RequestBody.FromStream` with a known `contentLength` writes exactly that many bytes (a short source
  throws `EndOfStreamException`; a longer source's remainder is left unread); `FromStream` rejects `contentLength`
  below -1 on both bodies and a non-readable request source (`HTTP-39`, `IO-3`).
- **Breaking:** `ResponseBody.ReadAsBytesAsync` and `ReadAsStringAsync` throw `BodyTooLargeException` for a body larger
  than `ResponseBody.DefaultMaxMaterializedBytes` (64 MiB), before reading when the declared length is already above it;
  they were unbounded. `RequestBody.ToReplayableAsync` throws `BodyTooLargeException` above `Array.MaxLength`, before
  writing when the length is known; it failed with an `IOException` or `OutOfMemoryException` after consuming the body.
  A response body's `OpenRead` after `OpenReadAsync` (or the reverse) throws `StreamConsumedException` (`IO-9`, `IO-11`).
- **Breaking:** `ResponseBody.Dispose()` and `DisposeAsync()` are no longer virtual (`HTTP-41`, `BODY-15`); a subclass
  overrides the new protected `Dispose(bool)` and `DisposeAsyncCore()`. Both are latched, so the release runs at most once
  across any mix of the two, and a release that throws propagates once.
- **Breaking:** `Response.Dispose()` and `DisposeAsync()` share one latch and forward to the body once (`HTTP-43`).
- **Breaking:** `ReadAsBytesAsync`, `ReadAsStringAsync` and their sync twins dispose the body when they finish, on
  success and on failure (`BODY-16`).
- **Breaking:** `ReadAsStringAsync` and `ReadAsString` strip a leading byte-order mark that matches the resolved
  charset's preamble (UTF-8, UTF-16, UTF-32); a mark that does not match the declared charset is kept (`HTTP-42`).
- **Breaking:** a stream-backed response body opened after it was disposed throws `StreamClosedException`; the
  second-open `StreamConsumedException` message now names the buffering route (`BODY-14`).
- **Breaking (behaviour):** `AsAsync`: a throwing dispose of a response produced after cancellation no longer faults the
  task; the task completes cancelled and the failure is reported on the current activity (design P3b-16).
- **Breaking:** `RequestBody.FromStream` over a readable, seekable stream with a declared `contentLength` in
  `[0, Array.MaxLength]` is replayable: it captures the stream's position at construction and seeks to it before every
  write (the caller's stream position moves; the stream stays open). It used to be single-use. A length of `-1` is still
  single-use (`BODY-9`, `BODY-35`).
- **Breaking:** `SdkException.ToString()` renders the suppressed trail: after the base rendering it appends one
  `---> (Suppressed Exception #n) ...<---` block per suppressed exception, with a `(cycle)` guard and an 8-level cap
  (phase 4b, `RECOV-12`).
- **Breaking:** `Response.EnsureSuccessAsync`: a dispose failure after a failed drain is attached to the drain's exception
  instead of replacing it (`RECOV-16`).
- **Breaking:** `IdempotencyPolicy` stamps PUT and PATCH as well as POST by default, and its constructor is
  `IdempotencyPolicy()` / `IdempotencyPolicy(IdempotencyKeyStep)` (`RECOV-32`).
- **Breaking:** `ClientIdentityPolicy` appends the SDK line after a caller-supplied `User-Agent` by default
  (`new ClientIdentityPolicy(ClientIdentityMode.Replace)` restores the old behaviour); a blank `UserAgent` emits no header
  (`RECOV-33`).

### Added

- `docs/sdk-documentation/execution-context.md`.
- The AOT smoke covers the execution-context chain.
- The execution-context chain: `DispatchContext`, `RequestContext`, `ExchangeContext` (`PromoteToRequest`/`PromoteToExchange`,
  `Close`), the bounded context store and `DexpaceCallContexts.TryGet` (`CTX-1`-`CTX-3`, `CTX-5`, `CTX-7`-`CTX-10`,
  `CTX-16`-`CTX-18`).
- `CallKey` and `InstrumentationContext` in `Dexpace.Sdk.Core.Execution`: the call key and the correlation bundle (`CTX-4`,
  `CTX-6`, `CTX-14`, `CTX-15`, `CTX-20`).
- `RS0030` entry for `AsyncLocal<T>` in the library projects (design §5.4); internal `BoundedMap`, the SDK's one
  bounded-map implementation (`CTX-11`, `CTX-12`).
- `docs/sdk-documentation/recovery.md`; the AOT smoke covers the recovery layer.
- `IdempotencyKeyStep`, `ClientIdentityStep`, `ClientIdentityMode` (`RECOV-32`, `RECOV-33`).
- `ErrorMappingStep` (`RECOV-15`); the one error-body capture in core is the internal `ErrorBodyBuffer` (`RECOV-16`).
- `RecoveryDispatcher` (`RECOV-2`, `RECOV-10`, `RECOV-11`).
- The recovery step contracts and chains (`RECOV-3`–`RECOV-9`, `RECOV-12`–`RECOV-14`).
- `Dexpace.Sdk.Core.Recovery.Outcome`, the closed success-or-failure carrier (`RECOV-1`).
- `RS0030` entry for `ValueTask<T>.Result` outside the internal `SyncPath`.
- `ExceptionFacts` and `ExceptionTrail`; `SdkException.Suppressed` (phase 4b, `RECOV-12`).
- `docs/sdk-documentation/bodies.md`; the AOT smoke covers the body surface (file, form, multipart, seekable replay, latched dispose).
- `RequestBody.Multipart` and `MultipartPart`: a `multipart/form-data` body whose framing is computed once, with a random or
  RFC 2046-validated boundary, a length guard per part, and part names that cannot break the framing (`HTTP-51`, `BODY-2`).
- `RequestBody.FromFile` and `FileRequestBody`: a replayable body over a byte range of a file, with a fresh read-only handle
  per write and the exact length (`HTTP-40`, `BODY-11`–`BODY-13`). On Unix a FIFO or device uploads as an empty body.
- `RequestBody.FromForm`: a replayable `application/x-www-form-urlencoded` body from name/value pairs, encoded with the
  WHATWG serializer (`HTTP-38`).
- `docs/sdk-documentation/io.md`; the AOT smoke covers the sync body surface.
- Synchronous body twins `RequestBody.WriteTo` / `ToReplayable` and `ResponseBody.OpenRead` / `ReadAsBytes` /
  `ReadAsString` (virtual; the base throws `NotSupportedException`, every SDK body overrides), `BodyTooLargeException`,
  `ResponseBody.DefaultMaxMaterializedBytes` (`HTTP-36`, `IO-9`, `IO-11`).
- `RS0030` entries for pooled-array rents, stream timeouts and `TextReader.ReadLine` (`IO-14`, `IO-22`, `IO-38`,
  `IO-40`).
- `docs/sdk-documentation/seams.md`; the AOT smoke covers the seam surface.
- `OperationDescriptor` and `BuildRequest` — the operation-input projection, RFC 3986 composed over the base address
  (`SEAM-26`–`SEAM-28`); `DexpaceClientOptions.BaseAddress` is now read.
- `DelegateHttpClient.Create` / `CreateBlocking` — a bare send function as a transport (`SEAM-11`).
- `HttpClientExtensions.Execute` / `ExecuteAsync` — option-less calls that pass `RequestOptions.Empty` (`SEAM-11`).
- `SerdeException` (abstract), the optional `IStringSerde`, and `SerdeExtensions` — `SerializeToUtf8Bytes`,
  `SerializeToString` and a fixed-buffer `Serialize(Span<byte>, T)` over any `ISerde` (`SEAM-20`, `SEAM-23`).
- Roadmap phase 0, task 8 (issue #29): the `knowledge-harvest` skill ported to C#
  (`.claude/skills/knowledge-harvest/{src,tests}`, in `tools/Dexpace.Tools.sln`, with the `knowledge-extractor` agent
  vendored under `.claude/agents/`), and the first real harvest of `docs/knowledge/harvested/`: the `spec`, `design`
  and `styleguide` roles, 3,336 entries in 41 topics, replacing the Ruby-seeded spec-only corpus. The ten
  styleguide-versus-design overlay rows are recorded as Conflicts entries; the five the port keeps (the `I` prefix,
  the `Async` suffix, `CA1062`, `LangVersion latest` and xUnit `Assert` without Shouldly) are overridden by `review`
  notes under `docs/knowledge/notes/`, and the five it conforms to read `conformed`. A merge replaces everything
  cited from a re-harvested source, `drift` also checks each entry's own sha, and `scripts/knowledge` tags settled
  conflicts `[kept]` / `[conformed]`.
- `docs/sdk-documentation/http.md`; architecture tests pinning `HTTP-1`, `HTTP-2`, `HTTP-5` and `SEAM-29`.
- `ETag`, `HttpRange` and `RequestConditions` (`HTTP-48`–`HTTP-50`).
- `HttpHeaderSyntax` — the public header-syntax predicates transports re-check with (`HTTP-17`–`HTTP-20`); typed
  `HttpHeaderName` overloads; six `HttpHeaderName.WellKnown` names; the adapter sends custom header names in their
  original casing.
- `Status.IsError`, `Status.TryGetKnown` (`HTTP-10`, `HTTP-11`).
- `Query` and `Query.Builder` — RFC 3986 query multimap with ordinal names, total lenient `Parse` and deterministic
  `Encode` (`HTTP-28`–`HTTP-31`); internal `Rfc3986` component encoder (`HTTP-32`).
- `RequestOptions` — per-call `Timeout`, `MaxRetries` and `Tags` (`HTTP-34`, `HTTP-35`); the type the phase 2b
  transport SPI carries.
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
- Serialization (#3): the serializer-agnostic `ISerde` seam in `Dexpace.Sdk.Core`, and
  `Dexpace.Sdk.Serialization.SystemTextJson`, a trim- and NativeAOT-safe implementation over source-generated
  `JsonTypeInfo<T>` metadata. Serializer failures map to `SerializationException` / `DeserializationException` on
  both the sync and async paths; cancellation propagates unwrapped. Conveniences `RequestBody.FromValue<T>`,
  `ResponseBody.ReadValueAsync<T>` and `HttpResponseException.GetErrorAsync<T>` route through `ISerde`. Request URLs
  must be absolute `http`/`https` URLs; anything else throws `ArgumentException`.
- Options, diagnostics and the pipeline context (#4): `DexpaceClientOptions` with `RetryOptions` and
  `RedirectOptions` (plain, container-agnostic option types); `DexpaceDiagnostics`, one `ActivitySource` and one
  `Meter` named `Dexpace.Sdk`; `UrlRedactor`, a log-safe URL renderer; and `PipelineContext`, the per-call state
  that flows through the pipeline.
- The pipeline and its core policies (#6, which also re-lands the pipeline spine first merged as #5):
  `HttpPipelinePolicy`, the immutable re-entrant `PipelineRunner`, the staged `PipelineBuilder`
  (`Add` / `InsertBefore` / `InsertAfter` / `Replace` / `Remove`, pillar validation), and `HttpPipeline` with a
  blocking `Send` bridge; `OperationPolicy` (overall timeout), `RedirectPolicy`, `RetryPolicy` (typed errors and
  5xx, jittered backoff capped at `MaxDelay`, `Retry-After`, idempotency and body-replayability gating),
  `IdempotencyPolicy`, `SetDatePolicy`, `ClientIdentityPolicy` and `InstrumentationPolicy` (per-attempt `Activity`
  with W3C `traceparent` / `tracestate` propagation, duration and active-request metrics, redacted structured logs);
  `Response.EnsureSuccessAsync`; and `DexpacePipeline.CreateDefault`. `Dexpace.Sdk.Core` takes its one runtime
  dependency, `Microsoft.Extensions.Logging.Abstractions`.
- Authentication (#8): `TokenCredential` with `AccessToken` and `TokenRequestContext`, `ApiKeyCredential`,
  `BasicCredential`; `AccessTokenCache` (per-context caching, proactive refresh, per-key single flight, tolerance of
  a refresh failure while the token is still valid, `TimeProvider`-driven); and the `ApiKeyAuthPolicy`,
  `BasicAuthPolicy` and `BearerTokenAuthPolicy` policies over an `AuthorizationPolicy` base that withholds
  credentials on a cross-origin hop.
- Pagination (#9): `AsyncPageable<T>` with `AsPages`, `Page<T>`, `Pageable.Create` (typed selectors, one request
  per page through the `HttpPipeline`, a `maxPages` cap, every response disposed on every path), and
  `PaginationStrategies` (`Cursor`, `PageNumber`, and RFC 8288 `LinkHeader`).
- Repository hygiene (roadmap phase 0): `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CODEOWNERS`,
  issue and pull-request templates, and Dependabot for NuGet and GitHub Actions. Each package now ships its
  `README.md` (`PackageReadmeFile`).

[Unreleased]: https://github.com/dexpace/dotnet-sdk/commits/main
