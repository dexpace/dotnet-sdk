# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Release notes (draft for 0.1.0)

*Draft. This block becomes `## [0.1.0]` at the held release commit, when the version suffix is emptied and the date is set; `ReleaseNotesTests` fails if it stops naming an item of `docs/first-release.md`'s
"What v1 ships without". Nothing is published yet.*

`0.1.0` is the first release of the dexpace .NET SDK: an HTTP-client toolkit, not an HTTP client. Five packages ship at one version (lockstep SemVer, `docs/sdk-documentation/versioning.md`):
`Dexpace.Sdk.Core` (models, bodies, the staged pipeline, auth, pagination, server-sent events, the serde seam and diagnostics; no transport and no codec), `Dexpace.Sdk.Http.SystemNet` (the reference transport over
`System.Net.Http.HttpClient`), `Dexpace.Sdk.Serialization.SystemTextJson` (an `ISerde` over source-generated `System.Text.Json`), `Dexpace.Sdk.Extensions.DependencyInjection` (`AddDexpaceClient` over
`IHttpClientFactory`) and `Dexpace.Sdk.Conformance` (the transport, invariant, codec and packaging suites). Everything targets `net10.0`, is trimmable and AOT-compatible, and is strong-named
(`PublicKeyToken` `fb3fcc3028a3989e`). Before 1.0 a minor version may break the public API, and adding a conformance assertion is at least a minor.

#### Breaking changes to know first

Nothing was published before this release, so these are differences from the pre-release trees (phases 1 to 11), listed because a reader of those trees will meet them. The full text of each is under
"Phase 11" and "Phase 12" below.

- **A transport failure is an `IOException`, not an `SdkException`.** `ServiceRequestException` (and `ServiceRequestTimeoutException`) derives from `IOException` and implements `IRetryableError`, so `catch (SdkException)` no longer
  catches it: catch `ServiceRequestException` or `IOException`. `ServiceResponseException` is removed (it was never thrown).
- **A second open of a response body returns the first stream**; the first convenience read (`ReadAsBytes*`, `ReadAsString*`, `ReadValue*`) disposes a stream-backed body, so a second read throws `StreamClosedException`.
- **A validating `init` accessor names its property in `ParamName`** for a null, and now for a range or format rejection too (it was `"value"`).
- **`HttpResponseException` refuses a `null` or non-error response; `Request` refuses a `Uri` built without canonicalization; `RequestBody.FromFile` refuses a Unix special file; `Headers.Builder.AddInbound` no longer trims a
  header name; `Page<T>` copies its list.**
- **The owned `SystemNetHttpClient` and the DI named client no longer let the runtime re-send a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` or `TRACE` natively**; the retry layer decides (see `idle-connection-native-retry`
  and `borrowed-handler-native-retry` below).
- **An empty configuration value falls through to the next provider** for a section of the registered `IConfiguration`; **an SSE event is bounded** (8 MiB of `data` by default, `ServerSentEventTooLargeException`); **the retry
  attempt header is set, not appended**, and may not be `Idempotency-Key`.

#### Guidance for adopters

- **Compose `AddStandardResilienceHandler` with `o.Retry.ShouldHandle = _ => ValueTask.FromResult(false)`, never with the SDK's `MaxRetryAttempts = 0`.** The first leaves the SDK's retry the only one; the second leaves the handler's default
  retry in place, and it re-sends a `POST` body behind the SDK's re-send gate (measured: a `POST` answered `503` reached the server four times; `docs/sdk-documentation/dependency-injection.md`).
- **A named client's settings are read once, at first resolution; there is no reload.** A reloaded `appsettings.json` does not reach a built client (see `di-no-reload`).
- **Dispose the provider with `DisposeAsync`** (a synchronous dispose throws for an async-only transport), and **a configuration value cannot turn the environment proxy read on**: write
  `Proxy = ProxyOptions.FromEnvironment()` (see `explicit-proxy`).

#### What v1 ships without

Each item is stated here because the register (`docs/first-release.md`) says the release notes must state every one; the reasons are there and in `docs/deviations.md`.

Unsatisfied MUSTs (four clauses, each argued in design section 10):

- `AUTH-15`: Digest MD5 and MD5-sess where the host's crypto refuses MD5 (a FIPS provider); an MD5-only challenge is declined and a SHA-256 offer is answered.
- `PAGE-21`: a byte-for-byte query splice for percent-encoded unreserved characters; `Request.Url` is a `System.Uri`, which decodes `%7E` before the splice sees it.
- `TRANSPORT-14`: the malformed inbound header name clause; `SocketsHttpHandler` rejects the whole response on such a name, and the conformance drivers carry one permanent waiver for it.
- `CFG-2`: an empty configuration value falls through only for a section of the `IConfiguration` registered in the container; any other section keeps the merged read.

SHOULD- and MAY-level requirements declined:

- `HTTP-22`: the header-name interning pool is not built (the well-known names are shared statics; equality is by value).
- `BODY-36`: no memory-mapped view of a file body.
- `REDIR-27`: the redirect target header is `Location` and not configurable.
- `TRANSPORT-28`: no zero-copy file path (`HttpClient` writes to its own connection stream).
- `BODY-12`: the kernel file-to-socket path is not built, for the same reason.
- `OBS-28`: the request and response byte-count milestones are not emitted as span events.
- `SEAM-28`: the operation id is not attached to the context chain.
- Authentication features outside the requirements: claims challenges, an `apiKey` in a query string or cookie, a preemptive Digest, `407` challenges in the pipeline and a `NamedKeyCredential` are not built.

Behavioural asymmetries a consumer must know:

- `async-redirect-pillar`: one `RedirectPolicy` serves both the synchronous and asynchronous standard pipelines, so both follow redirects (the reference contract has the async one not follow).
- `two-meters`: enabling both the `Dexpace.Sdk` meter and `System.Net.Http`'s measures each attempt twice under `http.client.request.duration`; enable one.
- `borrowed-client-traceparent`: a caller-supplied `HttpClient` whose handlers do not propagate sends no `traceparent` for a traced call while a `System.Net.Http` listener exists.
- `link-cross-origin-guard`: the `Link` pagination strategy ends the walk at another origin unless `allowCrossOrigin: true`, so the bearer token is not handed to a server-chosen host.
- `sync-borrowed-handlers`: `Execute` is a real `HttpClient.Send`; a handler overriding only `SendAsync` throws (primary) or is skipped (delegating) on the synchronous face.
- `explicit-proxy`: the owned transport goes direct and never reads `HTTP_PROXY`/`NO_PROXY`; ask for the environment with `ProxyOptions.FromEnvironment()`.
- `borrowed-client-default-deadline`: the transport's 100 second default deadline applies to a borrowed `HttpClient` too (the shorter bound wins; `Timeout = null` leaves the client's own).
- `di-no-reload`: a named client's settings are read once; no change-token source is registered.
- `di-handler-lifetime`: one handler chain per named client for the process, rotated inside by `PooledConnectionLifetime`; `ConfigureHttpClient` has no effect and the factory's own loggers are removed.
- `borrowed-handler-native-retry`: a handler or `HttpClient` you supply keeps the runtime's native re-send, so a body-less request can arrive up to four times; retry through the SDK's pipeline.
- `idle-connection-native-retry`: on the SDK-built transport, "reaches the server once" means not up to four times, not never twice (one idle-connection window remains, reasoned and not reproduced).
- `di-empty-shadows`: for a section from an unregistered configuration an empty value still shadows the lower provider (the counted `CFG-2` residual); read the section from the configuration you register.
- `public-query-splice`: the query splice behind `Cursor` and `PageNumber` is internal; a custom strategy builds its own query with `Query` and `Request.WithUrl`.

Not built, each with a trigger (`docs/first-release.md`, "Post-v1 packages"):

- `Dexpace.Sdk.Reactive`: an `IAsyncEnumerable<T>` to `IObservable<T>` bridge for pagination and SSE (trigger: a consumer composing with Rx operators).
- `Dexpace.Sdk.Serialization.NewtonsoftJson` (trigger: DTOs modelled with Newtonsoft attributes) and `Dexpace.Sdk.Serialization.Xml` (trigger: a real consumer of a second wire format).
- A second first-party transport (none planned: alternative stacks plug in under `HttpMessageHandler`).
- Webhook verification (post-v1 by roadmap decision D3; trigger: the first consuming SDK that receives webhooks).
- A published test-doubles package, `Dexpace.Sdk.Testing` (trigger: an author outside this repository asks for the fakes).

#### Held

The first publish itself is held on the lead's actions (nuget.org organisation and `Dexpace.*` prefix, trusted publishing, the `nuget` environment, the `main` ruleset): `docs/first-release.md`.

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

Roadmap phase 7b, issue #10 (a later addition, not one of S1–S9), pinned by a `[Trait("Category", "Security")]` regression test in
`tests/Dexpace.Sdk.Core.Tests/Security/`:

- **A server-sent-events line is capped at 1 MiB by default** (issue #10; `SSE-19`, design §10 entry 20). `ServerSentEventReader` holds at most `maxLineBytes` content bytes of a line and throws
  `ServerSentEventLineTooLongException` (a `StreamingException`) after reading at most one cap plus two read buffers, and stays failed; the message names the cap and never a byte of the line.
  Pinned permanently by `Security/ServerSentEventLineCapTests`. The cap bounds a line, not an event: bound a stream from an untrusted server with a token or `OverallTimeout` (`sse.md`).

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
- **Breaking:** a pipeline pillar collision throws at `PipelineBuilder.Add` (it used to throw at `Build`), naming both
  policy types and `Replace<T>`; re-adding the same instance to its pillar, which used to throw, is a no-op decided by
  reference identity (phase 4c, `PIPE-5`, `PIPE-6`). The builder reads a policy's `Stage` once, at insertion, and
  rejects a stage value that is not a `PipelineStage` member (`PIPE-8`, `PIPE-22`).
- **Breaking:** a cross-stage `PipelineBuilder.InsertBefore` / `InsertAfter` / `Replace` throws `ArgumentException`; it
  used to re-bucket the policy silently or accept it (`PIPE-18`, `PIPE-19`).
- **Breaking:** `PipelineStage.PerCall` is 150 and runs once per call, outside the redirect and retry loops; the old slot
  (inside the redirect loop, 250) is the new `PipelineStage.PerHop`. Source that says `PerCall` keeps compiling and
  changes loop (`PIPE-2`). `IdempotencyPolicy` and `ClientIdentityPolicy` therefore run once per call instead of once
  per redirect hop; every hop still carries what they stamped.
- **Breaking:** the policy signature is `HttpPipelinePolicy.ProcessAsync(Request, PipelineContext, PipelineRunner)
  -> ValueTask<Response>`, and `PipelineRunner.RunAsync(Request, PipelineContext) -> ValueTask<Response>`; a policy
  receives the request and returns the response instead of mutating `PipelineContext.Request` / `Response`, which makes
  `RETRY-44` and the S6 isolation structural (`PIPE-12`, `PIPE-14`, `PIPE-16`).
- **Breaking:** `PipelineContext` has no public constructor, no `Request` or `Response`, and a property bag keyed by
  `PipelinePropertyKey<T>` instances instead of strings; its per-drive values (`CancellationToken`, `Activity`,
  `AttemptNumber`, `HopNumber`) travel by copy (`ForAttempt`, `ForHop`, `WithActivity`, `WithCancellationToken`).
- **Breaking:** `HttpPipeline.Send` drives `HttpPipelinePolicy.Process` and the transport's synchronous member instead of
  blocking on the async chain; a third-party policy without `Process` runs through the documented blocking bridge
  (`PIPE-28`). The retry policy's synchronous wait is a genuine blocking wait over the `TimeProvider`.
- **Breaking:** `AuthorizationPolicy` compares against the seed request's origin (`PipelineContext.SeedRequest`, fixed at
  call entry), not the first origin it saw stored under the public string key `"dexpace.auth.origin"`; both its entry
  points are sealed (`REDIR-24`, `AUTH-28`).
- `HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient` and is disposable; disposal never touches the
  transport and the pipeline stays usable (`PIPE-26`, `PIPE-27`). `PipelineBuilder.Build` captures the client options, so
  `SendAsync(Request, CancellationToken)` and the seam entry points have options to run with.
- **Breaking:** the `HttpPipeline.SendAsync(Request, DexpaceClientOptions, CancellationToken)` and `Send` overloads lose
  their `= default` token (no overload of the family carries an optional token); pass `CancellationToken.None`.
- `DexpacePipeline.CreateDefault` is built from `PipelineBuilder.AddStandardResilience`; the async and the sync standard
  pipelines both follow redirects (`PIPE-32`, `REDIR-25`; design §10 entry 14, topic `async-redirect-pillar`).
- The `url.full` span tag is redacted with the call's `HttpLoggingOptions.AllowedQueryParameters`, the same redactor the log
  events use (`OBS-12`, P5b-10); the default (`api-version` only) is unchanged, so this is behaviour-preserving until a
  caller widens the list.
- **Breaking:** `InstrumentationPolicy` emits no request or response log event unless
  `DexpaceClientOptions.Logging.Level` is `Headers` or `Body` (it used to log at `Debug` on every call); at the default
  `None` it also allocates nothing and computes no redacted URL, while spans and instruments still record (`OBS-1`,
  `OBS-34`).
- **Breaking:** the HTTP log events are renamed and re-keyed to `http.request` (id 100) and `http.response` (ids 101 and
  102) with OpenTelemetry keys (`http.request.method`, `url.full`, `http.response.status_code`,
  `http.response.duration_ms`, `error.type`, one `http.request.header.<name>` / `http.response.header.<name>` key per
  logged header, body sizes) instead of the generated names (ids 1 to 3) and `{Method}`, `{Url}`, `{StatusCode}` keys; they
  are written at `Information` (a failed attempt at `Warning`) instead of `Debug`; `error.type` is the full type name
  (`OBS-2`, `OBS-39`, P5b-4, P5b-7). Headers outside the 26-name allow-list are logged as `REDACTED`, and URL-valued
  ones are redacted as URLs (`OBS-16` to `OBS-18`).
- **Breaking:** a logger that throws no longer fails the request it describes; the failure is reported as one
  `http.instrumentation.log_failed` event (id 120) and a second failure is swallowed. A fatal exception and the cancellation
  of the call's own token still propagate (`OBS-20`, `XCUT-20`).
- **Breaking:** the dispose-suppressed warning is `dexpace.dispose.suppressed` (id 130, keys
  `dexpace.dispose.resource_type` and `error.type`) instead of `DisposeSuppressed` (id 1), and it now reaches the
  pipeline's logger from the retry and redirect policies (the logger of the pipeline's `InstrumentationPolicy`; P5b-6).
- **Breaking, opt-in:** at `HttpLogLevel.Body` a response with a known-length body that is not `text/event-stream` comes back with a
  wrapped body: up to the preview size is read before `SendAsync`/`Send` returns (added latency), a body that fits is served
  from memory and can be opened again, a larger one is served as the captured prefix then the live remainder (the caller still
  receives every byte), and `ContentLength` follows `BODY-29`. Unknown-length and `text/event-stream` bodies are never wrapped
  (`OBS-36`, `OBS-37`, `OBS-38`, `BODY-34`, P5b-12). `Body` logs payloads verbatim: it is for diagnosis, not for production.
- **Breaking:** `DexpaceClientOptions`, `RetryOptions` and `RedirectOptions` are sealed records with `init` accessors
  (`CFG-8`, `CFG-9`). Assigning a property after construction no longer compiles (derive with `with`); equality and
  `GetHashCode` are by value (was: by reference); `ToString` renders the members, the base address redacted.
- **Breaking:** `DexpaceClientOptions.BaseAddress` is validated when it is set (absolute `http`/`https`, no fragment): an
  `ArgumentException` at the assignment, not at `OperationDescriptor.BuildRequest`; `UserAgent`, `Retry` and `Redirect`
  reject `null`. Phase 5b's `DexpaceClientOptions.Logging` is an `init` property too (ruling P5b-5), still rejecting `null`.
- Internal gate: `BannedSymbols.txt` bans `Thread.Sleep`, every `Task.Delay` overload, `DateTime`/`DateTimeOffset`
  `Now`/`UtcNow`/`Today` and the `Environment` variable readers in `src/` (`CFG-15`, `CFG-16`, `CFG-28`); no
  consumer-visible change.
- **Breaking (behaviour):** `RetryPolicy` honours HTTP-date `Retry-After` values it used to ignore (lower case, a weekday
  inconsistent with the date, the `UTC`/`+0000`/`+00:00` zones, a single-digit day). `SetDatePolicy` and
  `RequestConditions` format through `HttpDate`; their output is unchanged.
- **Breaking (behaviour):** the default `User-Agent` is `dexpace-dotnet/<version> dotnet/<runtime>` (the
  `BuildInfo.IdentityTokens` joined; was the one token `dexpace-dotnet/<version>`), and an undeterminable SDK version
  reads `unknown` (was `0.0.0`), which also changes the `ActivitySource` and `Meter` version in that case.
- **Breaking (behaviour):** every `HttpPipeline` call opens an `Internal` operation span on `Dexpace.Sdk` when the source is
  listened to, ended exactly once when the response is returned (at headers) or the call throws; the attempt spans are its
  children, no longer children of the caller's `Activity.Current`. `PipelineContext.Instrumentation` is the operation span's
  bundle, or `InstrumentationContext.None` when the source has no listener, even under an ambient activity (it was built from
  the ambient activity), so `CallKey.TraceId` is zero for an untraced call (`OBS-21`-`OBS-23`, `OBS-25`, `OBS-26`, `OBS-29`,
  `CTX-14`, `CTX-15`). Success leaves the operation span's status `Unset`; a failure sets `Error` with an `exception` event and
  `error.type`. An attempt span exists only under an operation span.
- **Breaking (behaviour):** new events on the operation span: `dexpace.attempt.failed` (an attempt failed and another follows;
  `http.request.resend_count`, `error.type`, `http.response.status_code`, `dexpace.retry.delay` in seconds),
  `dexpace.retry.exhausted` (immediately before the `exception` event of an operation that fails after its retry budget was
  spent, carrying the same exception type) and `exception` (`OBS-28`, `OBS-29`). `dexpace.redirect.hop` is defined and unwired
  until phase 6b.
- **Breaking (behaviour):** attempt spans: `server.port` is the port number (was `-1` for a default port);
  `http.request.resend_count` counts redirect hops as well as retries and is absent on the first transmission (was `0`); a
  4xx/5xx response sets `error.type` to the status code and the status to `Error`; a method outside RFC 9110 plus `PATCH` is
  `_OTHER` with `http.request.method_original`, and the span is named `HTTP`.
- **Breaking (behaviour):** `http.client.request.duration` and `http.client.active_requests` carry the stable attribute sets
  (`server.address`, `server.port`, `url.scheme`, and on the histogram `network.protocol.version` and `error.type` for a
  failure or a 4xx/5xx); unknown methods are `_OTHER`; the histogram carries OpenTelemetry's bucket advice. Dashboards keyed on
  the old tag set see new series (`OBS-31`-`OBS-33`).
- **Breaking (behaviour):** a listener whose `ActivityStopped` (or a meter whose callback) throws after a response exists now has
  that response disposed before the exception propagates; it was leaked. Listener and meter callbacks are still never wrapped
  (`OBS-20`, `OBS-30`).
- **Breaking (behaviour):** `SystemNetHttpClient` drops the SDK's own `traceparent` and `tracestate` stamp when it equals the
  current activity's, a `System.Net.Http` listener exists and runtime propagation is on, so the wire carries the runtime's
  child span id; a caller-supplied client without runtime propagation sends no `traceparent` for such a traced call. Enable
  the `Dexpace.Sdk` meter or `System.Net.Http`'s, not both.
- Internal gate: `BannedSymbols.txt` bans `Activity.TraceIdGenerator` in `src/` (`OBS-27`); no consumer-visible change.
- **Breaking:** `RetryOptions` defaults change to `MaxRetryAttempts = 2` (was 3: three sends, was four) and `MaxDelay` = 8 s
  (was 30 s) (`RETRY-12`). It also gains `Multiplier` (2.0), `Jitter` (0.2), `FixedDelay`, `RetryableStatusCodes`
  (`{408, 429, 500, 502, 503, 504}`) and `AttemptHeaderName`, and compares and hashes by value (the status set by content).
- **Breaking:** `RetryOptions.RetryNonIdempotentWhenReplayable` is removed. A request is re-sent when it has no body and an
  idempotent method, or when its body is replayable, whatever the method: a POST or PATCH with a replayable body is now
  retried by default and a POST with no body never is (`RETRY-5`, `RETRY-7`).
- **Breaking:** `RetryOptions` validates every member when it is set (`RECOV-34`): a negative count or duration, a duration
  above about 292 years, a multiplier below 1 or not finite, a jitter outside `[0, 1]`, a status outside 400 to 599 and an
  attempt-header name that is not an HTTP token throw `ArgumentOutOfRangeException` or `ArgumentException`.
- **Breaking:** the retry schedule is now live: `BaseDelay × Multiplier^(n−1)` capped at `MaxDelay`, with symmetric jitter of
  `±Jitter/2` around the capped delay (was full jitter over `[0, min(BaseDelay × 2^n, MaxDelay)]`), and a server pacing hint
  replaces it with no jitter (`RETRY-9` to `RETRY-11`, `RETRY-20`).
- **Breaking:** `RetryPolicy` classifies by one rule: a response is retried when its status is in
  `RetryOptions.RetryableStatusCodes`; an exception is retried when it or any cause reports `IRetryableError.IsRetryable` or
  is in the I/O family (`IOException`, `SocketException`, `TimeoutException`, an `HttpRequestException` with no status), so a
  third-party transport's raw `HttpRequestException` or `IOException` is now retried (was: only `ServiceRequestException`
  and `ServiceResponseException`); an `HttpResponseException` anywhere in the chain is decided by the configured set alone
  (`RETRY-2`, `RETRY-37`).
- **Breaking:** `RetryOptions.HonorRetryAfter` now governs `retry-after-ms`, `x-ms-retry-after-ms` and `X-RateLimit-Reset`
  as well as a fractional `Retry-After`, in that fixed order (`RETRY-15`, `RETRY-21`).
- **Breaking:** a retried error response is drained (at most 1 MiB) before it is disposed (was: disposed unread), and the
  exception a failed call throws carries every earlier attempt's failure in `SdkException.Suppressed` /
  `ExceptionTrail` (`RETRY-34`, `RETRY-35`).
- **Breaking:** `RetryPolicy` is no longer `sealed`; `Stage`, `Process` and `ProcessAsync` are sealed overrides, and the two
  new protected hooks `ShouldRetry` and `GetDelayOverride` are its extension points.
- **Breaking:** `DexpaceClientOptions.AttemptTimeout` is enforced by `RetryPolicy` (was: read by nothing); an attempt that
  exceeds it is a retried `ServiceRequestTimeoutException`, cooperatively (`XCUT-2`).
- **Breaking:** a response that arrives after the caller's token fired is disposed and the call throws
  `OperationCanceledException` (`RETRY-32`).
- **Breaking:** an expired `DexpaceClientOptions.OverallTimeout` surfaces `OperationTimeoutException` (was
  `OperationCanceledException`/`TaskCanceledException`), a caller-cancelled call still surfaces
  `OperationCanceledException`, and `OverallTimeout` and `AttemptTimeout` reject zero, a negative value (including
  `Timeout.InfiniteTimeSpan`) and anything above 49 days where they are set (a non-positive `OverallTimeout` used to mean
  "none"; use `null`) (`XCUT-1`, `XCUT-2`).
- **Breaking:** `OperationPolicy`'s parameterless constructor becomes `OperationPolicy(TimeProvider? timeProvider = null)`
  (source-compatible, binary-incompatible); `AddStandardResilience` passes its `timeProvider` to it, so a fake clock drives
  the deadline.
- **Breaking:** `RequestOptions` equality now includes the new `Auth` and `OperationAuth` descriptors (`AUTH-4`); two
  options differing only in a descriptor were equal before.
- **Breaking:** `AccessToken.ExpiresOn` is `DateTimeOffset?` (a token may never expire); the token must be non-blank; equality
  is by value (token, expiry, refresh) with `==`/`!=`; `ToString` redacts the token (`AUTH-8`..`AUTH-10`).
- **Breaking:** `ApiKeyCredential` rejects a whitespace-only key, a blank or whitespace-containing `Scheme`, and a key or
  scheme the outbound header grammar refuses, at construction (was: an empty key only); `ToString` redacts the key
  (`AUTH-26`, `XCUT-18`).
- **Breaking:** `BasicCredential` rejects an empty username or password and a `:` in the username (was: `null` only);
  `ToString` redacts the password (`AUTH-14`).
- **Breaking:** `TokenRequestContext.Scopes` is a copy of the caller's list (was: the list itself).
- **Breaking:** `AuthorizationPolicy`'s protected surface: a constructor taking the client `AuthDescriptor` and the available
  `AuthScheme`s; `WithheldHeaderNames` replaces `WithheldHeaderName`; `GetCredentialAsync` / `GetCredential` take the
  resolved `AuthRequirement` and the request, may return `null` ("nothing to attach on this pass") and are both abstract
  (the sync-over-async default is gone); new `OnChallengeAsync` / `OnChallenge` hooks (`AUTH-27`..`AUTH-33`).
- **Breaking:** the HTTPS refusal is `HttpsRequiredException` (was a plain `SdkException`, which it still derives from);
  it now also covers the outbound pass of a reactive scheme (`AUTH-28`).
- **Breaking:** every auth policy honours `RequestOptions.Auth` / `OperationAuth` (a per-call `NoAuth` sends the call
  anonymously, an unservable scheme throws `AuthResolutionException` before anything is sent) and answers a `401`
  carrying `WWW-Authenticate` through its hook, replaying once when the replacement is same-origin and replayable.
- **Breaking:** `AccessTokenCache` refreshes 30 seconds before expiry by default (was: at expiry); stamps a still-valid token and
  refreshes it in the background, one refresh per key (was: awaited the refresh on the request path); logs a failed
  background refresh as event 160 `dexpace.auth.token_refresh_failed` (was: silent); rejects a default or already-expired
  provider token with `TokenProviderException`; lets the waiters of a failed fetch share its outcome; and is bounded to
  1024 keys (`AUTH-11`, `AUTH-34`..`AUTH-37`, `XCUT-12`, `XCUT-14`).
- **Breaking:** `BearerTokenAuthPolicy` retries a call once with a freshly fetched token when the server answers `401` with
  a `Bearer` challenge (was: returned the `401`), and its synchronous path no longer blocks on the asynchronous one
  (`AUTH-30`, `AUTH-31`, `AUTH-36`).

- **Breaking (phase 6b redirect, PR 1):** `RedirectOptions` is reshaped (`REDIR-3`, `REDIR-4`, `REDIR-5`, `REDIR-15`, `REDIR-17`,
  `REDIR-20`, `REDIR-26`). `StripSensitiveHeadersOnCrossOrigin` is removed (it has had no effect since phase 1; stripping always
  applies); `MaxRedirects` defaults to `3` (was `20`) and a negative value throws `ArgumentOutOfRangeException` at `init`. New
  `AllowedMethods` (default `{GET, HEAD}`, copied to a frozen set), `FollowSeeOther` (default `false`) and `Predicate`
  (`Func<RedirectCondition, bool>`, with the new `RedirectCondition` snapshot).
- **Breaking (phase 6b):** a `301` or `302` on a `POST` is no longer rewritten to a body-less `GET`. A 301, 302, 307 or 308 is followed
  only when the original method is in `AllowedMethods`, with the method and body preserved; otherwise the 3xx is returned. Add the
  method to `AllowedMethods` (or set a `Predicate`) to follow it; nothing restores the `POST` to `GET` rewrite (`REDIR-3`).
- **Breaking (phase 6b):** a `303` is no longer followed by default; set `FollowSeeOther`. A followed 303 is a body-less `GET` and every
  `Content-*` header is removed from it (`REDIR-5`).
- **Breaking (phase 6b):** an https to http redirect without `AllowHttpsToHttpDowngrade` throws the new
  `RedirectSchemeDowngradeException` (was: the 3xx returned), and a method-preserving redirect over a body that cannot be re-sent
  throws `RedirectBodyNotReplayableException` (was: the 3xx returned). Both derive from the new `RedirectException`, dispose the
  response first, carry no URL property and name only redacted URLs in the message (`REDIR-6`, `REDIR-15`).
- **Breaking (phase 6b):** a redirect to a URI already visited on the call returns that 3xx (loop detection, `REDIR-16`), and a
  response with more than one `Location` value is returned unfollowed (`REDIR-18`). `Location` resolution is total: an empty host, a
  non-http(s) scheme or an unparseable value is returned unfollowed and never throws; an explicit default port in a target is
  normalised away (`https://h:443/y` is sent as `https://h/y`).
- **Breaking (phase 6b):** `RedirectPolicy` is rewritten as one loop over a pure decision function: the superseded response is
  disposed before the next drive, every stop returns the in-flight response open, a cancelled call token is honoured between hops, and
  a throwing predicate disposes the response and propagates unchanged (`REDIR-22` to `REDIR-24`, `PIPE-40`).

- **Breaking (phase 6b redirect, PR 2):** the redirect policy writes five `http.redirect.*` events to the pipeline's logger (the
  logger of its `InstrumentationPolicy`), whether or not `HttpLoggingOptions.Level` is `None`: `http.redirect.hop` (150,
  `Information`) for every followed hop, and `loop_detected` (151), `scheme_downgrade_rejected` (152), `scheme_downgrade_permitted`
  (153) and `location_malformed` (154) at `Warning`. Every URL is redacted, and a malformed `Location` goes through
  `UrlRedactor.RedactHeaderValue`. A logger that watched `http.request` and `http.response` only will now see these too (`REDIR-28`).

- **Breaking (phase 7c pagination, `PAGE-1`..`PAGE-36`):** `Pageable.Create` takes the transport seam (`IAsyncHttpClient`; an `HttpPipeline` still
  converts), an `IPageStrategy<TPage, T>` and `RequestOptions?` instead of an `HttpPipeline`, a `DexpaceClientOptions` and the two delegates
  `selectItems` and `nextRequest` (`PAGE-4`, `PAGE-5`, `PAGE-36`). `PaginationStrategies.Create(selectItems, nextRequest)` wraps the old delegates; a
  caller who passed different client options builds a pipeline with them.
- **Breaking (phase 7c):** the `PaginationStrategies` factories return an `IPageStrategy<TPage, T>` and take the item selector, so each gains a
  second type parameter. `Cursor`'s parameter name defaults to `"cursor"`; `PageNumber` loses `hasMore` and gains `startPage` (default `1`, `0`
  allowed); `LinkHeader` loses `rel` and gains `headerName` and `allowCrossOrigin` (`PAGE-16`..`PAGE-20`).
- **Breaking (phase 7c):** `PageNumber` ends on an empty page only, so a non-empty last page now costs one more, empty, exchange where a
  `hasMore` predicate used to stop early; the current page is read from the executed request with invariant, sign-less ASCII-digit parsing, and a
  page at `int.MaxValue` ends the stream (`PAGE-17`). Write `PaginationStrategies.Create` to keep a predicate.
- **Breaking (phase 7c):** the `Link` strategy ends the stream, quietly, on a target on another origin than the first request (a scheme downgrade
  included) unless `allowCrossOrigin: true`, on a target holding a space, control character, `<`, `>` or `"` (`not a url` used to resolve as a
  relative path), and follows a userinfo-bearing target with the userinfo removed; it reads every instance of the header and resolves against the
  response URL, not the current request (`PAGE-18`..`PAGE-20`). The guard exists because each page is a fresh pipeline call whose auth policy
  stamps the credential for any origin it is same-origin with; `allowCrossOrigin: true` re-opens that, deliberately.
- **Breaking (phase 7c):** the query splice matches parameter names case-sensitively (`?Page=1` is no longer replaced when the parameter is
  `page`) and drops later duplicates of the parameter it sets; it rebuilds the URL from its components, so no explicit default port is written
  (`PAGE-21`..`PAGE-24`).
- **Breaking (phase 7c):** `AsyncPageable<T>.AsPages()` loses `pageSizeHint`, becomes non-virtual and returns a single-use view (a second
  `GetAsyncEnumerator` throws `InvalidOperationException`); `GetAsyncEnumerator` becomes non-virtual and subclasses override the protected
  abstract `WalkPagesAsync` (`PAGE-14`).
- **Breaking (phase 7c):** `Page<T>`'s constructor requires the `Request` the walk sent for the page (`PAGE-2`), exposed as `Page<T>.Request`.
- **Breaking (phase 7c):** `maxPages` of zero or less, and a first request whose body cannot be written twice, throw at construction (`PAGE-9`).
- **Breaking (phase 7c):** a `null` page envelope throws `DeserializationException` naming the page type, where it threw
  `InvalidOperationException`; a `null` response from the client and a `null` `PageInfo` from a strategy throw `InvalidOperationException` naming
  the client or strategy type, after the response is closed (`PAGE-4`, `PAGE-28`).

- **Breaking (phase 8b reference transport, `TRANSPORT-1`, `-4`, `-5`, `-6`, `-8`, `-9`, `-10`, `-12`, `-13`, `-14`, `-17`, `-18`, `-28`, `-30`; design and plan under
  `docs/work/mvp/phase8/phase8b/`), PR 1:** `new SystemNetHttpClient()` and `SystemNetHttpClient(ILogger)` no longer inherit the runtime's process-wide proxy, which read `HTTP_PROXY`,
  `HTTPS_PROXY` and `NO_PROXY` on first use: the owned handler goes direct unless `SystemNetHttpClientOptions.Proxy` is set (`CFG-28`, `TRANSPORT-30`; `HttpClient.DefaultProxy` is banned in `src/`).
  **Migration:** `new SystemNetHttpClient(new SystemNetHttpClientOptions { Proxy = ProxyOptions.FromEnvironment() })`.
- **Breaking (phase 8b):** every call made after `Dispose`/`DisposeAsync` throws `ObjectDisposedException`, a transport over a borrowed `HttpClient` included (a faulted task on the asynchronous face); it used to keep
  serving (`SEAM-15`). **Migration:** do not use a disposed transport; the borrowed `HttpClient` itself is unaffected.
- **Breaking (phase 8b):** the new `SystemNetHttpClient(HttpMessageHandler)` constructor rejects a handler whose primary `SocketsHttpHandler`/`HttpClientHandler` follows redirects with `ArgumentException`
  (`AllowAutoRedirect` defaults to `true`, so `new SystemNetHttpClient(new SocketsHttpHandler())` throws; `TRANSPORT-1`). **Migration:** set `AllowAutoRedirect = false` on the handler.
- **Breaking (phase 8b), PR 2:** `RequestOptions.Timeout` is enforced (it was accepted and ignored): per call, on `SystemNetHttpClientOptions.TimeProvider`, until the response headers arrive, clamped to `[1 ms, 49 days]`.
  The owned `HttpClient.Timeout` is now infinite and the transport's own 100 second default (`SystemNetHttpClientOptions.Timeout`) replaces it, so the owned bound is unchanged (`TRANSPORT-5`, `-6`).
  **A borrowed `HttpClient` is bounded differently:** the default applies to every construction, so a transport over a caller's `HttpClient` whose own `Timeout` is longer than 100 seconds (or infinite) now cuts a call that sets
  no `RequestOptions.Timeout` at 100 seconds with a retryable `ServiceRequestTimeoutException`; before, that client's own `Timeout` alone bounded it. **Migration:** none for an owned transport or a call that sets a
  timeout; for a borrowed client with a longer `Timeout`, `new SystemNetHttpClient(client, new SystemNetHttpClientOptions { Timeout = null })` (its own `Timeout` is then the only bound) or a longer
  `RequestOptions.Timeout`. Set `SystemNetHttpClientOptions.Timeout` to change the default.
- **Breaking (phase 8b):** an internal cancellation (`CancelPendingRequests` on a borrowed client, or the native client or the transport disposed mid-call) is a terminal `OperationCanceledException` with the native
  exception on its `ExceptionTrail`; it used to surface as a retryable `ServiceRequestTimeoutException` and was retried. A real timeout stays a retryable `ServiceRequestTimeoutException`, and a cancellation by the
  caller now always carries the caller's own token (`TRANSPORT-4`, `-8`, `XCUT-1`, `XCUT-2`). **Migration:** treat the `OperationCanceledException` as a cancellation; it is not retried.
- **Breaking (phase 8b):** `SystemNetHttpClient.Execute` is genuinely synchronous (`HttpClient.Send`; it used to block on the asynchronous send), and writes the request body through `RequestBody.WriteTo`
  (`SEAM-11`, `PIPE-28`, `TRANSPORT-17`). Over a handler whose primary overrides only `SendAsync` it throws `NotSupportedException`, and a `DelegatingHandler` that overrides only `SendAsync` is skipped on that
  face. **Migration:** implement `Send` in the handler, or call `ExecuteAsync`.
- **Breaking (phase 8b):** a native re-send of a single-use request body (a proxy's `407` answered, on a client the native layer can re-send through) is replayed from a capture of at most
  `SystemNetHttpClientOptions.ResendBufferLimit` bytes (1 MiB; it used to fail with a wrapped `StreamConsumedException`); beyond the limit, or after a failed first write, the send fails with a retryable
  `ServiceRequestException` whose cause chain ends in a `StreamConsumedException` naming `ResendBufferLimit` and `ToReplayableAsync` (`TRANSPORT-18`). **Migration:** raise `ResendBufferLimit` or
  `RequestBody.ToReplayableAsync()` a larger single-use stream.
- **Breaking (phase 8b), PR 3:** the caller's `Content-Type` header is the one sent; the body's media type used to override it and is now used only when the caller set none (`TRANSPORT-10`). A content header
  (`Allow`, `Content-*`, `Expires`, `Last-Modified`) on a body-less request is carried by an empty content on `POST`/`PUT`/`PATCH`/`DELETE`/`OPTIONS` and is dropped and logged on `GET`/`HEAD`/`TRACE`; it used to vanish
  unlogged (`TRANSPORT-12`, `-26`). **Migration:** stop relying on the body's type overriding your header.
- **Breaking (phase 8b):** the transport's header-drop log events are `http.transport.header_dropped` (110), `framing_header_dropped` (111) and `inbound_header_dropped` (112) in `DexpaceLogEvents` (they were ids 1 to 3,
  `FramingHeaderDropped`, `UnsafeHeaderDropped`, `InboundHeaderDropped`), and under the default `HeaderDropLogging.FirstPerName` a repeated drop of one header name is a `Debug` entry, not another `Warning`
  (`TRANSPORT-13`, `OBS-19`). **Migration:** filter on the new names; `HeaderDropLogging = Every` restores a `Warning` per drop.
- **`TRANSPORT-2` (phase 8b):** a body-less `POST`, `PUT`, `PATCH` or extension-method request is sent with a zero-length content, which is what the native client writes for these methods anyway
  (`Content-Length: 0`, byte for byte), so `SocketsHttpHandler`'s own connection-failure retry cannot fire and the request reaches the server once. A body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE` is
  still re-sent by the native client (up to three more times), which is recorded as design §10 entry 33 and waived permanently for `transport-2.bodyless-not-resent`.

- **Guidance (phase 9, `XCUT-10`; documentation only):** composing `AddStandardResilienceHandler` under a named client is `o.Retry.ShouldHandle = _ => ValueTask.FromResult(false)`. The older guidance to set the SDK's own
  `MaxRetryAttempts = 0` is withdrawn: the handler would still re-send a `POST` body behind the SDK's re-send gate, which measurably multiplies the attempts, and the handler-side `o.Retry.MaxRetryAttempts = 0` is refused by
  the handler's own options validation. See `docs/sdk-documentation/dependency-injection.md`.
- **Phase 10, additive (`XCUT-1`..`XCUT-24`, `NFR-1`..`NFR-17`):** `TransportFace` gains `None` (value `2`), the face of an assertion that has none; `SuiteRunner` rejects it, the new suite runners accept it, and no existing value moves.
  `Seam2ArchitectureTests` admits `Dexpace.Sdk.Conformance` as a reference of `Dexpace.Sdk.Core.Tests` (the core driver of the invariant suite), the kit itself staying core-only. `scripts/ci/reproducible-pack.sh` now packs `HEAD` from two
  `git worktree`s at different paths, so an absolute path embedded in a package fails the job (a git checkout is the reproducible input; it warns on a dirty tree). `BannedSymbols.txt` bans `Exception.GetBaseException` and the seven public
  `ConcurrentDictionary` constructors in `src/` (`RS0030`; `BoundedMap` is the one construction site, under one scoped pragma). Every `severity = none` block of `.editorconfig` states when to re-enable it. No production behaviour changed.

### Added

- **Phase 10 cross-cutting invariants and non-functional requirements (`XCUT-1`..`XCUT-24`, `NFR-1`..`NFR-17`; design and plan under `docs/work/mvp/phase10/`):** `Dexpace.Sdk.Conformance` gains three suites beside the transport suite, with one result model
  (`TransportFace.None`, `SuiteRunnerCore`, `SuiteOptions`, a per-suite preamble, `ConformanceReport.Combine`). `InvariantSuite` runs 28 assertions over an `InvariantSubject` whose hooks default to core (a hook left `null` is `NotExercised`): cancellation and
  timeout (`XCUT-1`..`-3`), the error taxonomy and the one classifier (`XCUT-4`..`-9`), retry safety, concurrency and the single-flight cache (`XCUT-10`..`-12`), lifecycle, bounded maps and no external aliasing (`XCUT-13`..`-15`), security by default
  (`XCUT-16`..`-19`) and the host invariants (`XCUT-20`..`-24`, `NFR-11`'s scheduler-agnostic call). `CodecSuite` runs 11 assertions over an `ISerde` and the public probe models in `Dexpace.Sdk.Conformance.Codec` (`SERDE-1`..`-3`, `-9`, `-10`, `-12`, `-15`..`-17`,
  `-19`, `-20`); `PackagingSuite` runs 8 assertions over the packed `.nupkg` files with BCL readers (`NFR-1`, `-2`, `-3`, `-8`, `-10`, `-13`, `-14`, `-15`). Each assertion has a deliberately broken subject in `tests/Dexpace.Sdk.Conformance.Tests/Controls/` that it must
  fail. Three first-party drivers run them: the core driver, the dependency-injection driver (`xcut-23`) and the System.Text.Json driver, each pinning its expected `NotExercised` list; the NativeAOT smoke runs the invariant and codec suites (`CheckPhase10Async`).
  Gates: `scripts/ci/packaging-conformance.cs` (a CI step after Pack); `scripts/ci/requirement-coverage.cs --check` with its self-test (every one of the 645 requirements of appendix C is owned by a checklist row, with its level and a legend mark; a Linux CI step);
  `AppendixBCoverageTests` over the new `tests/Dexpace.Sdk.Conformance.Tests/APPENDIX_B.md` (61 items); `Nfr11PublicSurfaceArchitectureTests`, `NFR4ArchitectureTests`, `LicenseHeaderTests` (the `scripts/ci` apps included), `AnalyzerWaiverTests` and `CiWorkflowTests`.
  Review resolution (2026-10-14): `xcut-17` composes the subject's Basic and bearer auth steps under the redirect loop and covers a permitted downgrade followed by a retry; `xcut-18` accepts a refusal or a trim for HTAB at the edge of a name
  (`XCUT-18` and `HTTP-17` meet there; core's inbound trim is a possible `HTTP-19` gap, routed to phase 11); `xcut-13.dispose-non-blocking` fails a subject with nothing to close; `xcut-21` accepts any nonce of at least 128 bits in hex or base64; `xcut-24` requires a
  body preview in the log and a non-empty prefix of the body as the error snapshot; `CiWorkflowTests` also pins the triggers and rejects a job-level `if:`, `continue-on-error` or matrix `exclude:`.
  `AppendixBMap` covers all 61 appendix-B items. `docs/sdk-documentation/cross-cutting-invariants.md` is the phase page (where each `XCUT` row is enforced, the gate table, how to run the suites over your own composition); `conformance.md` describes the suites; the checklist
  `docs/work/mvp/phase10/2026-10-13-phase10-cross-cutting-invariants-checklist.md` has the 41 rows. Declined with triggers: Stryker.NET (it refuses Microsoft.Testing.Platform projects) and `benchmarks/`; `XCUT-24`'s non-consuming clause for an error-body snapshot above 1 MiB
  is a recorded SHOULD decline.

- **Phase 9 host integration (`CFG-1`, `-2`, `-3`, `-4`, `-5`, `-6`, `-7`, `-10`, `-11`, `-14`, `-37`, `-38`, `SEAM-5`, `SEAM-6`, `OBS-35`; design and plan under `docs/work/mvp/phase9/`):** the new package
  `Dexpace.Sdk.Extensions.DependencyInjection`. `AddDexpaceClient(name[, IConfigurationSection])` returns a sealed `DexpaceClientBuilder` (`Configure`, `ConfigureTransport`, `ConfigureCredentials`, `UseTransport`, `UseSerde`,
  `UseAuth`, `UseAuthPolicy`, `AddPolicy`, `UseProxyFromEnvironment` with and without a lookup, and `HttpClient`, the `IHttpClientBuilder` of the named client) and registers the client as keyed singletons (`HttpPipeline`,
  `IAsyncHttpClient`, `ISerde`, `DexpaceClientOptions`). Settings are read by the package's own reader, not by the configuration binder (neither mode can serve: the generator binds no `init` accessor, the reflection mode is
  not AOT-safe): base-10 integers, strict booleans, `CFG-7` durations (ISO-8601 day-time, shorthand, bare milliseconds, `none`), enums by name, lists that replace the defaults; an empty value is absent; every bad key, accessor
  rejection and unknown key fails `ValidateOnStart` at once as `"<path>: <rule>"`, never quoting a configured value, and a host-less provider fails at first resolution. The named client is built over `IHttpClientFactory` with a
  redirect-off primary, no factory loggers and an infinite handler lifetime with a pooled-connection lifetime, and a container-owned lease releases the primary. `DexpaceConfigurationKeys` publishes the conventional keys
  (never read). One transport and one codec per client, explicit installs replace the defaults, a different second install throws at the call and keyed duplicates fail at start. `SystemNetHttpClient.CreatePrimaryHandler(options)`
  is the one place the owned-handler invariants are built. `BannedSymbols.txt` bans `ConfigurationBinder` in `src/`; `scripts/ci/dependency-audit.cs` holds each library to a policy record (one third-party root, plus the
  first-party projects it names). A third conformance driver runs the kit over the DI-built transport on both faces with the two permanent waivers; `NamedClientRedirectWireTests` is phase 1 S3's row 9; the AOT smoke covers
  the package; `docs/sdk-documentation/dependency-injection.md`.

- `docs/sdk-documentation/tracing-and-metrics.md`; the AOT smoke covers the operation span, its attempt child and the two
  metrics (`OBS-31`, `OBS-32`).
- `docs/sdk-documentation/pipelines.md`; the AOT smoke covers the synchronous pipeline, `ErrorMappingPolicy` and
  `HttpPipeline` as a seam.
- Phase 4c pipeline surface: `PipelineBuilder.Prepend`, `AddRange`, `PrependRange`, `AddStandardResilience`, `Flatten`,
  `Nest` and `Build()`; `HttpPipeline.Policies`, `SendAsync<T>` / `Send<T>`, the `RequestOptions` overloads and the
  synchronous `Send` family; `HttpPipelinePolicy.Process` and `PipelineRunner.Run`; `PipelinePropertyKey<T>`;
  `PipelineContext.SeedRequest`, `RequestOptions`, `CallKey`, `Instrumentation`, `HopNumber`, `ForAttempt`, `ForHop`,
  `WithActivity`, `WithCancellationToken`, `TryGetProperty` and `SetProperty`; the `PipelineStage.Serde` pillar and
  `PerHop`; `AuthorizationPolicy.GetCredential`; `DexpacePipeline.CreateEmpty`; `ErrorMappingPolicy` and the synchronous
  `Response.EnsureSuccess` (`PIPE-1`-`PIPE-40`).

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
- Phase 5b logging surface, step 2: `HttpLogLevel`, `HttpLoggingOptions` (level, body preview size, header allow-list,
  query allow-list, URL-valued header names, omit-versus-redact switch) and `DexpaceClientOptions.Logging`;
  `UrlRedactor.RedactHeaderValue` (`OBS-16`, total and never the malformed-URL sentinel); the stable `DexpaceLogEvents` and
  `DexpaceLogKeys` vocabulary (`OBS-39`). Only `AllowedQueryParameters` is read so far (by the `url.full` span tag); the emitter lands in the next step.
- Phase 5b logging step 3: the `http.request` / `http.response` events, the emission guard and the logger carried on the call
  (`OBS-1` to `OBS-4`, `OBS-6`, `OBS-20`, `OBS-24`, `OBS-34`, `OBS-39`); `RecordingLogger`, `ProviderLikeLogger`,
  `ThrowingLogger` and `DisabledLogger` in `Dexpace.Sdk.TestSupport`.
- Phase 5b body-level logging step: bounded request and response body previews (`http.request.body.preview`,
  `http.response.body.preview` and their `.preview.size`), the `http.instrumentation.body_capture_failed` diagnostic, and an
  internal ownership-moving `Response.ReplaceBody` that keeps the exchange link across the swap (P5b-13).
- `docs/sdk-documentation/logging-and-redaction.md`; the AOT smoke covers body-level logging; `docs/knowledge/notes/observability.md`
  records the departure from styleguide 6.2.
- `TimeProviderWaits.Sleep` and `DelayAsync`: the blocking interruptible sleep and the awaitable delay over
  `TimeProvider`, both rejecting a negative delay (the BCL's `-1 ms` means "wait forever") and chunking past ~49.7 days
  (`CFG-15`, `CFG-17`, `CFG-18`). `RetryPolicy` waits through them. Internal: `LateResult`, the cooperative form of
  `CFG-21` and the one `Task<T>.WaitAsync` site.
- `HttpDate` (`Format`, `Parse`, `TryParse`): the RFC 1123 HTTP-date formatter and a hand-written span parser (zone and
  month case-insensitive; `GMT`, `UTC`, `+0000`, `+00:00`; the weekday is stripped, never validated; RFC 850 and
  asctime rejected) (`CFG-29`, `CFG-30`, `CFG-31`).
- `ProxyOptions`, `ProxyType` and `ProxyOptions.FromEnvironment`: an immutable proxy model (host, port, ordered glob
  bypass list, credentials, a challenge-credentials slot, a credential-masking `ToString`) and an environment resolver
  that reads `HTTPS_PROXY`/`HTTP_PROXY` (either case) and `NO_PROXY`, never throws, and warns without echoing the value
  (`CFG-22`..`CFG-28`). Installing it in the transport is phase 8b's.
- `BuildInfo`: the SDK version and runtime identity (`SdkVersion`, `RuntimeVersion`, `RuntimeDescription`, `OSName`,
  `IdentityTokens`), resolved once, each field falling back to `unknown` and every token header-safe (`CFG-36`).
  Internal: `RetryFacts.IsRetryableStatus` and `IsRetryableCause` (`CFG-35`, wired by phase 6a) and `DeepValue`
  (`CFG-33`, `CFG-34`); no consumer-visible change.
- `docs/sdk-documentation/configuration.md`; the AOT smoke covers the options records, `HttpDate`, `ProxyOptions` and `BuildInfo`.
- Phase 6a retry, PR 1: `IRetryableError` (the capability a custom exception implements to be retried, `XCUT-6`),
  `SdkException.IsRetryable` (`false`), the sealed overrides on `ServiceRequestException` and `ServiceResponseException`
  (`true`) and `HttpResponseException` (baked once from the status classifier, `XCUT-5`, `RETRY-3`). Internal: `RetryFacts`
  moves to the `Dexpace.Sdk.Core.Resilience` namespace and gains `DefaultRetryableStatusCodes`, `IsRetryableFailure` and
  `IsResendable` (`RETRY-1` to `RETRY-8`, `RECOV-17`, `RECOV-18`).
- Phase 6a retry, PR 2 (internal): `RetryBackoff` (the one calculator: `BaseDelay × Multiplier^(n−1)`, capped, symmetric jitter,
  saturating, clamped to 365 days) and `RetryPacing` (the strict `Retry-After`, `retry-after-ms`, `x-ms-retry-after-ms` and
  `X-RateLimit-Reset` parser), with the vectors `tests/vectors/retry/backoff.json` and `pacing.json`
  (`RETRY-9` to `RETRY-22`, `RECOV-21` to `RECOV-26`, `RECOV-29`, `RECOV-34`). The live scheduler changes with the engine in PR 3.
- Phase 6a retry, PR 3: the shared retry engine (`RetryEngine`, `RetryBudget`, internal) under `RetryPolicy`;
  `RetryAttemptContext` and the protected hooks `RetryPolicy.ShouldRetry` and `RetryPolicy.GetDelayOverride` (`RETRY-29`,
  `RETRY-39`, `RETRY-40`); the attempt header (`RetryOptions.AttemptHeaderName`, `RETRY-38`); the log event
  `dexpace.retry.delay_override_failed` (`DexpaceLogEvents.RetryDelayOverrideFailed`, id 140). The `MA0051` waiver on the old
  `ProcessCoreAsync` is gone (`RETRY-8`, `RETRY-13`, `RETRY-14`, `RETRY-23` to `RETRY-35`, `RETRY-38` to `RETRY-42`, `RETRY-44`,
  `RETRY-45`).
- Phase 6a retry, PR 4: `OperationTimeoutException`, the non-retryable `SdkException` an expired overall deadline throws,
  with the failed attempts' trail copied onto it (`XCUT-1`, `RETRY-34`).
- Phase 6a retry, PR 5: `RetryRecovery` (retry options, an optional total-time budget and a clock) and a `RecoveryDispatcher`
  constructor that takes it, with `RecoveryDispatcher.Retry`. A retrying dispatcher runs the request chain once, sends through
  the transport and the response steps, retries on the same engine as `RetryPolicy` (every surviving response whose status is
  in the configured set is buffered and mapped to an `HttpResponseException` on arrival), and runs the recovery steps once on
  the terminal outcome (`RETRY-14`, `RETRY-27`, `RETRY-36`, `RETRY-37`, `RECOV-16` to `RECOV-20`, `RECOV-27`, `RECOV-28`,
  `RECOV-30`, `RECOV-31`).
- Phase 6b redirect, PR 1: `RedirectCondition` (the predicate's read-only snapshot), `RedirectOptions.AllowedMethods`, `FollowSeeOther` and `Predicate`, and the
  `RedirectException`, `RedirectSchemeDowngradeException` and `RedirectBodyNotReplayableException` family; internal `HttpOrigin`, `RedirectLocation`, `RedirectChain`, `RedirectDecider`
  and `RedirectReissue` behind a rewritten `RedirectPolicy` (`REDIR-1`-`REDIR-24`, `REDIR-26`).
- Phase 6b redirect, PR 2: `DexpaceLogEvents.RedirectHop`, `RedirectLoopDetected`, `RedirectSchemeDowngradeRejected`, `RedirectSchemeDowngradePermitted`, `RedirectLocationMalformed` and
  their ids 150 to 154, and `DexpaceLogKeys.RedirectHop`, `RedirectTarget`, `RedirectCrossOrigin` and `RedirectLocation` (`REDIR-28`, `OBS-39`); internal `RedirectLog` and `EmissionGuard`.
- Phase 6b redirect, PR 3: the permanent `Security` tests `RedirectCredentialLeakTests` and `RedirectCredentialLeakWireTests` (the phase 6 convergence exit: no credential survives a
  cross-origin hop or a retry across one), the NativeAOT smoke check `CheckPhase6bRedirectAsync`, and `docs/sdk-documentation/redirect.md`.
- `docs/sdk-documentation/retry.md`; the AOT smoke covers `RetryPolicy`, `RetryRecovery` through `RecoveryDispatcher`, `OperationTimeoutException`, a custom `IRetryableError`
  and the `RetryOptions` validation.
- Phase 6c descriptor and resolver: `AuthScheme`, `AuthRequirement`, `AuthDescriptor`, `AuthResolver` (per-call, operation and
  client tiers, no fall-through), `AuthResolutionException`, and the `RequestOptions.Auth` / `OperationAuth` carriers
  (`AUTH-1`..`AUTH-7`).
- `DigestCredential` and `AuthCredentials` (with a `TokenRefreshMargin`); `CredentialRedactionTests` (`Security`) pins that no
  credential type formats its secret (`XCUT-19`(d)).
- Phase 6c challenge model: `AuthenticationChallenge` and its lenient, linear RFC 7235 parser (`Parse(string?)`,
  `Parse(ReadOnlySpan<char>)`; a duplicate parameter keeps the first value), the `IChallengeHandler` SPI,
  `BasicChallengeHandler`, `CompositeChallengeHandler`, and `HttpHeaderName.WellKnown.WwwAuthenticate`,
  `ProxyAuthenticate` and `ProxyAuthorization` (`AUTH-12`, `AUTH-13`, `AUTH-14`, `AUTH-23`, `AUTH-25`).
- `ChallengeAuthPolicy` (Basic or Digest answered on a `401`, never preemptive), `AuthChallengeContext` and
  `HttpsRequiredException` (`AUTH-26`..`AUTH-33`, `AUTH-38`).
- `AccessTokenCache.Get` (a real synchronous path), `AccessTokenCache.DefaultRefreshMargin` and `RefreshMargin`,
  `BearerTokenAuthPolicy(AccessTokenCache, params string[])`, `TokenProviderException`, and log event 160
  `DexpaceLogEvents.TokenRefreshFailed` (`AUTH-35`, `AUTH-37`).
- RFC 7616 Digest authentication: `DigestChallengeHandler` (MD5, MD5-sess, SHA-256, SHA-256-sess, `qop=auth` or the legacy
  no-qop form; a configurable algorithm preference, SHA-256 first by default; `username*` for a non-ASCII user name; a
  per-nonce counter bounded to 1024 entries) and `DigestAlgorithm`. A host whose crypto provider refuses MD5 (FIPS) drops the
  MD5 algorithms instead of failing, so an MD5-only challenge is declined and a server offering both gets SHA-256
  (`AUTH-15`..`AUTH-22`, `AUTH-24`). No **Breaking** change.
- `MultiSchemeAuthPolicy`: the descriptor-driven, multi-credential auth step (`AuthCredentials` for OAuth2, API key, Basic and
  Digest) for APIs that declare several security schemes, sharing its stamping logic with the single-scheme policies;
  the OpenAPI mapping table is in `docs/sdk-documentation/auth.md` (`AUTH-4`, `AUTH-5`).
- `docs/sdk-documentation/auth.md`; the AOT smoke covers the challenge parser, the resolver, Digest (SHA-256 and the CSPRNG cnonce), the redacting credentials and the bearer policy with its background refresh.
- Phase 7b server-sent events (additive; **no Breaking change**), all in the new `Dexpace.Sdk.Core.ServerSentEvents` namespace: `ServerSentEvent` (immutable, `Data` an always-present list copied at `init`, validating `Id` and `Retry`, hand-written
  equality and a lossless string form; `SSE-4`, `SSE-9`, `SSE-11`, `SSE-20` to `SSE-22`); `ServerSentEventReader` over the WHATWG line reader (`ReadNext{,Async}`, the lazy single-use `ReadAll{,Async}`, a 1 MiB default line cap, a BOM consumed once, no
  last-event-id carried; `SSE-1` to `SSE-19`, `SSE-39`, `SSE-40`); `ServerSentEventLineTooLongException`.
- `ServerSentEventStream` (`SSE-23` to `SSE-32`): `FromResponse` owns the response from the call, even when it throws, and rejects a bodyless one (204, 205, 304, `HEAD`, zero length); four single-use views (`await foreach`, `AsEnumerable`, `MapAsync`, `Map`) share one
  latch; the response is released once on every path, a release failure being swallowed and reported out of band on a clean end or an early dispose, attached to the primary on a failure and propagated on an explicit dispose; a close from another thread
  surfaces as an `IOException` from a read in flight. The typed adapter's `SseMapResult<T>` (`Value`, `Skip`, `Done`; `default` is a mapper failure) joins the data with LF and runs lazily (`SSE-33` to `SSE-36`).
- `Dexpace.Sdk.Http.SystemNet`: the response body gained a synchronous `OpenRead` (internal class, no API change), sharing the open latch with `OpenReadAsync`, so the blocking `ServerSentEventStream` views work over the reference transport.
- Tests and docs: `Sse37ArchitectureTests` now enforces (and scans for a sentinel or `"message"` literal), `Sse38ArchitectureTests` (no `Last-Event-ID`, no transport or `Send` in an SSE type), `tests/vectors/sse/grammar.json` (105 cases from Node `c0ff3fd` and chapter 13), an exhaustive
  chunk-split property and a seeded round trip, a streamed `LoopbackResponse` and wire tests, the NativeAOT smoke check `CheckServerSentEventsAsync`, and `docs/sdk-documentation/sse.md` (including the reconnect recipe: core ships no reconnecting client, `SSE-38`).
- Phase 7c pagination: `Pageable.CreateBlocking` and `Pageable<T>` (the blocking pager, over the same fetch-and-parse step as the async one),
  `PageInfo<T>`, `IPageStrategy<in TPage, T>`, `Pageable.FromFetchers`, `FetchedPage<T>` and `PagingOptions` (the fetcher front-end, where a fetcher
  disposes its own response), `Page<T>.Request` (`PAGE-1`..`PAGE-36`; `PAGE-3`, `PAGE-29` and `PAGE-30` are permanent simplifications under design section 10
  entries 17 and 19). The page is closed before it is yielded, so no response is live at any `yield`; a release failure rides the parse error's
  `ExceptionTrail`; a response delivered after a cancel is disposed and discarded. `Pageable.CreateBlocking` works over `SystemNetHttpClient`, directly or
  through a pipeline, because its response body reads synchronously (phase 7b's `OpenRead`); the transport's `Execute` still blocks on the async send until
  phase 8b. The query splice strips only the delimiting `?` of the raw query and keeps an IPv6 zone id (`PAGE-21`, `PAGE-24`).
- Phase 7c tests: the permanent `Security` test `PaginationLinkOriginTests` (a server cannot take a credential with one `Link` header, and a
  first page that redirected cannot launder an origin), the shared vector `tests/vectors/pagination/link-header.json`, seeded property tests for
  the splice, a loopback wire class `PaginationWireTests`, `PaginationArchitectureTests`, and the NativeAOT smoke check
  `CheckPhase7cPaginationAsync` (a cursor walk, a `Link` walk, the single-use page view and the blocking pager).
- `docs/sdk-documentation/pagination.md`.
- **Transport conformance kit** (phase 8a; `TRANSPORT-3`, `-7`, `-11`, `-15`, `-16`, `-19` to `-27`, `-29`, `ASYNC-1` to `ASYNC-22`, and `TRANSPORT-2`'s single-use-body sentence; the other 14 `TRANSPORT` rows are phase 8b's, and
  the kit asserts them too, and so does `TRANSPORT-2`'s native-retry clause, which is unmet: see the last bullet of this entry). New package **`Dexpace.Sdk.Conformance`**, referencing `Dexpace.Sdk.Core` only: a framework-free `TransportSuite` with 43 named assertions (each is a plain method that throws
  `ConformanceException`, never an `SdkException`), `TransportSubject` (two faces and five optional capability hooks), `ConformanceReport` with per-requirement and appendix B.6/B.7 views, six result
  statuses including `NotExercised` for a missing hook and `Vacuous` measured from inside, and waivers by requirement ID that must stay needed (a waived assertion that passes fails the run). The loopback
  wire fixture is promoted into it as `Dexpace.Sdk.Conformance.Wire` and gains request-arrival and connection-release observation and the `Gated`, `HeadersThenGate`, `Abort`, `Hang`, `Large` and `EchoId`
  replies. The requirement catalogue is generated from appendix C by `scripts/ci/requirement-catalog.cs`. The kit runs against `SystemNetHttpClient` (with the eleven waivers of the rows phase 8b owns, each `Owner = "8b"`)
  and, through `DelegateHttpClient`, against a test-only raw-socket client that shares no code with `HttpClient`; a slice of it runs in the NativeAOT smoke. **Test-only move:**
  `Dexpace.Sdk.Http.SystemNet.Tests.Loopback.*` is now `Dexpace.Sdk.Conformance.Wire.*`. No change to any published package surface.
- Phase 8a tests: a negative control for every kit assertion (79 deliberately broken subjects), the kit's own unit and fixture tests, and `ASYNC` pins in `Dexpace.Sdk.Core.Tests` (`ASYNC-7`, `-8`, `-9`,
  `-10`, `-11`, `-12`, `-13`, `-14`). `ASYNC-2`'s worker-pool-rejection clause is not met: a scheduler whose `QueueTask` throws makes `AsAsync(...).ExecuteAsync` throw `TaskSchedulerException`
  synchronously instead of faulting the task; the fix is a core change left for a later phase.
- **`TRANSPORT-2` is not met by the reference transport.** `SocketsHttpHandler` has no switch for its own connection-failure retry and re-sends a request whose connection ends before any response byte up to
  three more times, so a body-less `POST`, `DELETE` or `GET` reached the server four times for one SDK attempt (`transport-2.bodyless-not-resent`, waived for phase 8b on every platform: it either turns
  the retry off or records the MUST as unmet on a stated domain). Its server closes each exchange in order: `LoopbackResponse.Abort` is a FIN and then a reset, which macOS usually reports as the reset, and a
  reset is not re-sent, so under `Abort` the count was the platform's. A single-use body is never re-written, and `transport-2.no-silent-resend` (the specification's own sentence) passes. `transport-20.retried-by-the-pipeline` now counts pipeline attempts, because the native
  retry had been delivering its 200 without the pipeline retrying; `transport-13` is `Vacuous` for a transport that drops no header; `transport-3` requires the caller's token on the cancellation and `transport-7.attempt-timeout-aborts`
  a timeout on the failure.
- `docs/sdk-documentation/conformance.md`.
- **Reference-transport hardening** (phase 8b; `TRANSPORT-1`, `-4`, `-5`, `-6`, `-8`, `-9`, `-10`, `-12`, `-13`, `-14`, `-17`, `-18`, `-28`, `-30`, and `TRANSPORT-2`'s native-retry clause for body-less `POST`, `PUT` and `PATCH`). New
  `SystemNetHttpClientOptions` (`Logger`, `Proxy`, `Timeout`, `HeaderDropLogging`, `ResendBufferLimit`, `TimeProvider`), the `HeaderDropLogging` enum (`FirstPerName`, `Every`, `VerboseOnly`), and four constructors:
  `(SystemNetHttpClientOptions)`, `(HttpMessageHandler)`, `(HttpMessageHandler, SystemNetHttpClientOptions)` and `(HttpClient, SystemNetHttpClientOptions)`. `ProxyOptions` is installed on the owned handler through an
  internal `IWebProxy` (spec-correct bypass globs, a proxy URI without userinfo, `ChallengeCredentials` or a Basic-only pair, the handler's own credentials never set), with a once-at-construction
  `Warning` (event 113) for a setting the native client cannot honour. Core's `DexpaceLogEvents` gains `TransportHeaderDropped` (110), `TransportFramingHeaderDropped` (111),
  `TransportInboundHeaderDropped` (112) and `TransportProxyFeatureUnsupported` (113), with their ids. `BannedSymbols.txt` bans `HttpClient.DefaultProxy`.
- Phase 8b tests: the permanent `Security` class `ProxyCredentialWireTests` (the proxy's `407` is answered, an origin's `401` behind it is not, no log carries the pair), architecture scans of the transport assembly
  (`NoImplicitEnvironmentReadArchitectureTests`, `NoAbandonedResponseArchitectureTests`), unit and wire classes for the handler constructor, options, proxy adapter, deadlines, failure classification, the synchronous path,
  header partition and drop log, inbound headers, the re-send capture and the body-less retry, and a handler-constructor subject in the conformance driver. The driver runs the whole kit against both constructions with two
  permanent waivers (`transport-14.name-malformed-dropped`, design §10 entry 32, and `transport-2.bodyless-not-resent`, entry 33) and no `Owner = "8b"` waiver; `transport-30` now runs. NativeAOT smoke check
  `CheckPhase8bAsync`.
- `docs/sdk-documentation/transport.md`.

### Phase 12 — first release

Roadmap phase 12 prepares the first release (design and plan under `docs/work/mvp/phase12/`). The first NuGet publish is held: nothing here publishes, tags or pushes.

#### Fixed

- **`Dexpace.Sdk.Http.SystemNet` packs the same bytes from the same commit** (`NFR-12`, build half; design V6). The assembly held `[LoggerMessage]` methods in two classes
  (`HeaderDropLog`, `ProxyWarnings`), and the `LoggerMessage` source generator orders the classes of one assembly differently from one compilation to the next, so two
  builds of one commit differed and the `reproducible packages` job could go red. All four generated methods now live in one internal `TransportLog`; event ids, names,
  levels and message templates are unchanged. `LoggerMessageDeterminismArchitectureTests` fails a second type with a generated log method in any library. No behaviour change.

#### Changed

- **Breaking (phase 12):** a value rejected for its range or format by a validating `init` accessor now reports the property in `ArgumentException.ParamName`; it was `"value"`,
  the accessor's parameter (`SEAM-29`, `HTTP-2` to `HTTP-4`; design V11 and its amendment). Phase 11 did this for a null; this completes it. The properties are
  `ProxyOptions.Type`, `Host` and `Port`; `RetryOptions.MaxRetryAttempts`, `BaseDelay`, `MaxDelay`, `FixedDelay`, `Multiplier`, `Jitter`, `RetryableStatusCodes` and
  `AttemptHeaderName`; `DexpaceClientOptions.BaseAddress`, `OverallTimeout` and `AttemptTimeout`; `RequestOptions.Timeout` and `MaxRetries`; `HttpLoggingOptions.BodyPreviewSize`;
  `RedirectOptions.MaxRedirects`; `AuthRequirement.Scopes`; `AuthCredentials.TokenRefreshMargin`; `AccessTokenCache.RefreshMargin`; `SystemNetHttpClientOptions.Timeout`,
  `HeaderDropLogging` and `ResendBufferLimit`; and, in the conformance kit, the `Name` of the transport, codec, invariant and packaging subjects, `PackagingSubject.CoreId`
  and `DeclaredFloor`, and the timeouts of `SuiteOptions` and `TransportSuiteOptions`. The exception types and messages are unchanged, so code that catches by type is unaffected;
  code that compared `ParamName` to `"value"` must compare it to the property name. `InitAccessorScanner` now recognises both spellings of the defect, so a new accessor of either is
  caught by the coverage tests.
- **The five assemblies are strong-named with a committed key** (`NFR-16`'s strong-naming half; P12-9, P12-10; design §9.2): `Dexpace.Sdk.snk` (2048-bit RSA, public by design, nothing trusts it),
  `PublicKeyToken` `fb3fcc3028a3989e`, applied to every project under `src/` and `tests/` through `Directory.Build.props`; each library's `InternalsVisibleTo` grant to its own test project carries the key
  (`DexpacePublicKey`), so a strong-named consumer no longer meets `CS8002` against these libraries. An assembly-identity change recorded before the first publish, not a breaking change: nothing was
  published unsigned. `StrongNameKeyTests` pins the property against each built library; the tools solution stays unsigned. Author signing with a certificate is declined; `NFR-16` rests on the
  build-provenance attestation `release.yml` makes at the held first publish (P12-13).
- **The next version is `0.1.0`** (P12-7): `VersionPrefix` `0.1.0`, `VersionSuffix` `alpha.1`, so the packages on `main` are `0.1.0-alpha.1`; the held release commit empties the suffix and dates `## [0.1.0]`.
  Lockstep SemVer and the pre-1.0 rules are in `docs/sdk-documentation/versioning.md`.
- **The public surface is recorded as the first release's** (`NFR-4`, source half; P12-8). Each library's `PublicAPI.Unshipped.txt` moved into `PublicAPI.Shipped.txt` (`#nullable enable` first, then every
  member in order: Core 1 369, Conformance 475, SystemNet 39, DI 27, System.Text.Json 12) and `Unshipped` is `#nullable enable` alone, so from here a surface change is a reviewed `Unshipped` diff against a
  recorded baseline. A pure move: no member was added, removed or reshaped. `PackageValidationBaselineVersion` stays unset until the first publish (`docs/first-release.md`, *After the first publish*).

#### Added

- **`.github/workflows/release.yml`, inert, pinned by `ReleaseWorkflowTests`** (`NFR-16`, `NFR-12`'s release half; design "The release workflow", P12-2 to P12-4). Two triggers: a `v*` tag
  push, and `workflow_dispatch` with a boolean `dry_run` that defaults to `true`; only a tag publishes. The `build` job (ubuntu) restores locked, builds, tests, packs the commit twice at two
  paths with `SOURCE_DATE_EPOCH` set to the commit time (`scripts/ci/reproducible-pack.sh`), runs the dependency audit, the packaging conformance and the release preflight over pack 1, and
  uploads the packages with a `SHA256SUMS` file. The `publish` job (`needs: build`, `environment: nuget`, the only job with `id-token: write` and `attestations: write`) stops at its first step
  while the repository variable `NUGET_USER` is empty, naming `docs/first-release.md`; past it, it verifies the checksums, attests every package (GitHub artifact attestation, in place of
  author signing), logs in with `NuGet/login` (no long-lived key) and pushes with `--skip-duplicate`. Nothing is published: the nuget.org organisation, the `Dexpace.*` prefix, the
  trusted-publishing policy and the `nuget` environment are the lead's, and the action inputs are marked unverified in the file's header.
- **`scripts/ci/release-preflight.cs` and its self-test** (`NFR-16`, `NFR-12`, `NFR-14`, `NFR-15`; P12-4): over a packed set it fails, naming the cause, unless the five packages are present at one version, each `.nupkg`
  has a `.snupkg` beside it, and every packed `lib/net10.0` assembly (read in memory, never loaded) is strong-named with the committed key's token (`fb3fcc3028a3989e`); with `--tag` it also requires a tag equal to `v<version>`,
  every `PublicAPI.Unshipped.txt` back to `#nullable enable` alone, a `## [<version>]` section in this file, and a tag commit that is an ancestor of `main`. Two blocking steps in `ci.yml` run it and `release-preflight-selftest.sh`
  on all three rows. Without `--tag` a tag-dependent finding prints `would fail:` and the exit stays 0, so the dry run on `main` reports the missing `## [0.1.0-alpha.1]` section (this file's section is `## [0.1.0]`
  from the held release commit, whose suffix is empty) without failing.
- **A `.snupkg` symbol package beside every `.nupkg`** (`NFR-12`; P12-11): five libraries, five symbol packages, compared byte for byte by `reproducible-pack.sh` and its self-test, which flips a byte in one to prove the compare can fail.
  The audits read `.nupkg` only.
- **`housekeeping fences`** (P12-18): `dotnet run --project .claude/skills/housekeeping/src -- fences` compiles every ` ```csharp ` fence of `README.md`, `docs/sdk-documentation/*.md` and `src/*/README.md` that names a
  `Dexpace.Sdk` namespace, as its own generated project outside the repository against the built Release libraries with warnings as errors; ` ```csharp fragment ` is counted and never compiled, and a fence needing an
  unpinned optional package is reported, not failed. A blocking Linux `Doc fences` step in `ci.yml` runs it.
- **Nine user-documentation pages under `docs/sdk-documentation/`** (P12-17): `architecture.md` (replaces the retired `docs/architecture.md`), `getting-started.md`, `example-end-to-end.md`, `quality-gates.md`,
  `versioning.md` and the four guides `write-a-transport.md`, `write-a-serde.md`, `write-a-response-handler.md` and `write-a-paging-strategy.md`; `conformance.md` gains "What a green run does not prove";
  `SECURITY.md`, `README.md` and `CONTRIBUTING.md` are refreshed for five packages and the release path.

#### Audit back-fill

The phase 12 audit (P12-19) walked the 55 first-parent commits of `main` since the foundation (`git log --first-parent main --oneline`; the earlier entries are not rewritten) and found these without an entry. Everything else is
covered by the entries above and below, by phase or by subject.

- **Test partition, loopback fixture and xUnit v3** (#22, roadmap phase 0, `SEAM-2`): `tests/Dexpace.Sdk.Http.SystemNet.Tests` split out of `Core.Tests`, which now references core and `TestSupport` only; a BCL-only `TcpListener`
  HTTP/1.1 loopback fixture; the non-packable `tests/Dexpace.Sdk.TestSupport`; SEAM-2 and SSE-37 architecture tests; xUnit v3 on Microsoft.Testing.Platform (`global.json`'s `test.runner`), coverlet.MTP and TRX reports; a `Category`
  trait on every test class, enforced by each suite's `TestCategoryTests`.
- **Fixed:** the build restores again under `NuGetAudit` (#20): the explicit `Microsoft.SourceLink.GitHub` reference, whose `Microsoft.Build.Tasks.Git` carried advisory GHSA-23fw-v26w-5fgq (`NU1902` under warnings-as-errors), is removed
  (the .NET 8+ SDK embeds Source Link), and `NuGetAuditMode=all` audits transitive packages for every target.
- **Fixed:** the coverage gate keeps every report and fails closed (#35): reports are named per test project (`--coverlet-file-prefix`), and `scripts/ci/coverage-gate.cs` exits 3, before checking the floor, when a test project left no
  report or more than one or a `src/` library has no coverage data; `coverage-gate-selftest.sh` proves each exit code.
- **Fixed:** `scripts/knowledge --gaps` takes every prefix (`--gaps HTTP SEAM` read HTTP alone) and `--phase` credits every ID inside a cited range (#42); `--prefix-info` refuses a second prefix.
- **Added:** the organisation label sync (#11, #12): `.github/workflows/labels.yml` calls the reusable workflow in `dexpace/.github` with the `common`, `docs`, `package` and `sdk` profiles (42 labels), on push, on a Monday
  schedule and on dispatch.
- **Changed:** dependency updates (#26, #27, #34, #53): the `actions/checkout`, `actions/setup-dotnet` and `actions/upload-artifact` majors in `ci.yml`, `Microsoft.CodeAnalysis.BannedApiAnalyzers` and `PublicApiAnalyzers` 4.14.0 to 5.6.0, and
  the minor-and-patch group (`Meziantou.Analyzer`). Build-only; no package a consumer restores changed.

### Phase 11 — deviation reconciliation

Roadmap phase 11 re-measured every design §10 entry and §11 reading against the code (`docs/deviations.md`, the as-built ledger, held by `LedgerAuditTests`), repaired what could be repaired
for free before the first release, and corrected what could not; the clauses still unmet on a stated domain are four (`AUTH-15`, `PAGE-21`, `TRANSPORT-14`'s malformed-name half and `CFG-2` for a section of an unregistered configuration), from seven. Design, plan and checklist under
`docs/work/mvp/phase11/`. Every public-surface change is a reviewed `PublicAPI.Unshipped.txt` diff, nothing is shipped, and the one-time choices that phase 12 decides before the first
`PublicAPI.Shipped.txt` are listed in `docs/first-release.md`. The breaking entries below are breaking against the phase 10 tree, not against a release.


#### Changed

- **Breaking (phase 11):** `HttpResponseException(Response, string?)` throws an `ArgumentNullException` for a `null` response (was: a
  `NullReferenceException`) and an `ArgumentException` for a response whose status is not 400 to 599 (was: it built "The server returned an error
  response: OK(200)."), so no successful "error" can be constructed through the one public mapping route (`XCUT-8`, `NFR-7`; design §10 entry 28).
- **Breaking (phase 11):** `ServiceRequestException` (and so `ServiceRequestTimeoutException`) derives from `IOException` and implements `IRetryableError`;
  it is no longer an `SdkException` (`TRANSPORT-20`, `XCUT-4`; design §10 entry 7). `catch (SdkException)` no longer catches a transport failure: catch
  `ServiceRequestException` or `IOException`. A transport failure has no `Suppressed` property; read its suppressed trail with
  `ExceptionTrail.GetSuppressed(exception)`, and `ToString()` still renders it. The retry classifier reads `IRetryableError` and the cause chain, so
  retry behaviour is unchanged. The conformance kit's transport assertions now require an `IOException` that reports itself retryable.
- **Breaking (phase 11):** `ServiceResponseException` is removed. It was public, documented as part of `XCUT-4`'s taxonomy and never thrown by any
  transport; a failure with no response is a `ServiceRequestException`. The reference transport's message now follows the native error kind: "The request
  could not be sent to the server." for a name-resolution, connect, TLS or proxy-tunnel failure, and "The request failed and no response was received."
  for the rest (a response that ended after the request arrived is no longer called unsent).
- **Breaking (behaviour, phase 11):** the `ExecuteAsync` of an `AsAsync(IHttpClient, TaskScheduler)` bridge no longer throws a `TaskSchedulerException`
  from the call when the scheduler rejects the work; the returned task faults with it (`ASYNC-2`). A token that is already signalled returns a cancelled
  task without touching the scheduler.
- **Breaking (behaviour, phase 11):** an `ArgumentNullException` from a validating `init` accessor now carries the property's name as its `ParamName`
  (`"Retry"`, `"Methods"`, `"Headers"`, ...) instead of the accessor's implicit `"value"`, which named nothing (`SEAM-29`, `HTTP-2` to `HTTP-4`; design §10
  entry 10). It covers `DexpaceClientOptions`, `RetryOptions`, `RedirectOptions`, `ProxyOptions`, `RequestOptions`, `OperationDescriptor`, `AuthRequirement`,
  `FetchedPage<T>`, `IdempotencyKeyStep`, `SystemNetHttpClientOptions` and the conformance kit's subjects and options, including a null element of
  `RedirectOptions.AllowedMethods` and `ProxyOptions.NonProxyHosts`.
- **Breaking (phase 11):** a response body returns the same stream on a second open (`BODY-14`, `HTTP-41`). `ResponseBody.OpenRead` and
  `OpenReadAsync`, in either order, hand back the one stream the first open obtained (the stream-backed, in-memory and `SystemNetHttpClient`
  bodies); the second open used to throw `StreamConsumedException`. The convenience readers (`ReadAsBytes*`, `ReadAsString*`, `ReadValue*`) after a manual
  open now read the remaining bytes of that same stream. A body disposed after it was opened now reports `StreamClosedException` on a later open (it
  reported `StreamConsumedException` first), so a second typed read after a first one throws `StreamClosedException`, and `HttpResponseException.GetError`
  twice over a plain, non-buffered body still throws `ResponseNotReadException`, now over an `ObjectDisposedException`. The response-logging wrapper past its
  capture cap keeps `BODY-24`'s rule: its tail is single-use and a second open throws `StreamConsumedException`. The buffered error body is unchanged.
- **Breaking (phase 11):** `Headers.Builder.AddInbound` no longer trims a received header name (`HTTP-19`, `XCUT-18`; design §11 item 70). A name with SP or HTAB at either
  end is not a token and throws `ArgumentException` (it used to be stored under the trimmed name); the outbound path (`Add`, `Set`, `With`, `HttpHeaderName.Of`) still trims
  (`HTTP-17`). `SystemNetHttpClient` already drops a header that fails the inbound path, and the runtime never hands it such a name. The conformance kit's `xcut-18.model-validation`
  requires an inbound refusal where it used to accept a refusal or a trim, with a negative control for a model that trims.
- **Breaking (phase 11):** `RequestBody.FromFile` refuses a Unix special file at construction (`BODY-11`, `HTTP-40`; design §10 entry 31). A FIFO, a character or block
  device or a socket reports a length of 0, so it used to upload as an empty body; a size-0 path is now classified by its file type through `System.Formats.Tar` (in the shared framework,
  which opens no special file, so a FIFO does not block) and throws `ArgumentException` naming the kind, never the path. An empty regular file, readable or not, and a symbolic link to a regular file are unchanged;
  a link to a special file is refused as its target. Windows keeps its device-attribute check. The NativeAOT smoke refuses `/dev/null`.
- **Breaking (phase 11):** `Request` (the constructor and so `WithUrl`, `WithMethod`, `WithHeaders`, `WithBody`) rejects a `Uri` created with
  `UriCreationOptions.DangerousDisablePathAndQueryCanonicalization` with an `ArgumentException` (`ParamName` `url`; the message names the cause and not the input). Such a `Uri` keeps
  its path and query exactly as written and `HttpClient` writes a CR or LF in the query raw into the request line (design §10 entry 18). A `Uri` the default parser made, from a
  string or `new Uri(string)`, is unaffected, and `Request.Create` takes a string.
- **Breaking (phase 11):** the `Page<T>` constructor copies the list it is given (`PAGE-2`; design §10 entry 17). `Values` is a read-only snapshot, not the caller's list: a list a
  `Pageable.FromFetchers` fetcher goes on to change (or a live view a strategy returned) no longer alters a page after it was yielded, and `ReferenceEquals(page.Values, list)` is
  no longer true.
- **Breaking (phase 11):** the owned `SystemNetHttpClient` and the dependency-injection named client no longer let `SocketsHttpHandler` re-send a body-less `GET`, `HEAD`, `OPTIONS`,
  `DELETE` or `TRACE` natively (`TRANSPORT-2`; design §10 entry 33). `SystemNetHttpClient.CreatePrimaryHandler` installs a pass-through `PlaintextStreamFilter` (HTTP/1.x only) that
  turns an end of stream arriving after a request was written and before any response byte into the `HttpIOException` (`ResponseEnded`) the runtime raises itself, so the request
  reaches the server once (it used to arrive four times) and the SDK's retry layer decides whether to send it again. No byte is inspected or changed. One window remains, reasoned from how the runtime reuses a pooled connection and not reproduced: a read of 0 before a request's first write passes through (an idle
  close is not a failed request), so a server close that lands after the runtime's reuse check of a pooled connection and before that write can still make the runtime re-send once, a request whose first copy went to an already-closed connection;
  "reaches the server once" means not up to four times, not never twice (`docs/sdk-documentation/transport.md`). A handler or `HttpClient` you
  supply is your native client and keeps its own retry (design §11 item 71); a caller who replaces the filter on a handler from `CreatePrimaryHandler` removes the guarantee. The
  conformance drivers' `transport-2.bodyless-not-resent` waiver is deleted for the owned and DI subjects and kept, rescoped, for the handler-taking subject.
- **Breaking (behaviour, phase 11): an empty configuration value falls through to the next provider** (`CFG-2`, design §10 entry 25, §11 item 68, P11-17). When the
  `IConfiguration` registered in the container is the root that holds the section handed to `AddDexpaceClient`, the reader walks that root's providers from the
  highest and takes the first value that is not empty or whitespace-only (a `ChainedConfigurationProvider` is walked into, and a list item falls through the same
  way), so `FOO__RETRY__MAXRETRYATTEMPTS=` in the environment no longer hides the value `appsettings.json` holds. Was: the merged read, where the empty value won
  and the record default applied. A section from a configuration that is not registered, or not a root, keeps the merged read, where the empty value still wins: no public API reaches the root of an `IConfigurationSection`, so this is a counted unmet clause (`CFG-2`; `docs/first-release.md`, `di-empty-shadows` narrowed), and the fix is to read the section from the configuration you register. No `ConfigurationBinder` is involved.
- **Breaking (behaviour, phase 11): an SSE event is bounded in the data it accumulates** (`SSE-19`, design §10 entry 20 residual R1, P11-19). The line cap bounded a line; lines that each fit and never
  end the event were retained without limit (8 000 000 lines of `data:x`, 56 MB on the wire, became one 323 MB event). The `data` lines of one event now cost their UTF-8 value bytes plus one
  each, and an event whose data passes `maxEventBytes` (default `ServerSentEventReader.DefaultMaxEventBytes`, 8 MiB) fails at the line that passed it with the new
  `ServerSentEventTooLargeException` (a `StreamingException` carrying `MaxEventBytes`), and the reader stays failed. `ServerSentEventReader`'s constructor, `ReadAll`, `ReadAllAsync` and
  `ServerSentEventStream.FromResponse` gain `maxEventBytes` overloads; the existing `maxLineBytes = 1048576` default parameters become overloads (`RS0026`/`RS0027` refuse an optional
  parameter beside a longer overload), so every call that compiled still compiles. A permanent `Security` test pins the hostile stream.
- **Breaking (phase 11): the retry attempt header is set, not appended, and `AttemptHeaderName` may not be `Idempotency-Key`** (`RETRY-38`, design §11 item 10). The stamp
  `RetryOptions.AttemptHeaderName` names now replaces any value the request already carries under that name, so a send holds exactly one (it was appended after a caller's or an outer layer's
  value). `AttemptHeaderName = "Idempotency-Key"`, in any case, now throws `ArgumentException` (and fails `Retry:AttemptHeaderName` at start): the stamp has to preserve the idempotency key,
  and one that replaced it would send the attempt number as the key.

#### Added

- **`ResponseTaskExtensions.WaitOrDisposeAsync(this Task<Response>, CancellationToken)`** (phase 11; `SEAM-30`, `ASYNC-3`; design §11 item 43): the
  cancel-without-interruption mode of an `AsAsync` bridge's task. A caller that stops waiting through `Task.WaitAsync(token)` could orphan an undisposed
  `Response` when the call completed at the instant the token was signalled (44 of 20 000 runs of the design's probe); through this method every caller gets
  a live response or an `OperationCanceledException`, and a response the task delivers after the caller stopped waiting is disposed exactly once and quietly.
- **`TraceIdGeneration`** (`OBS-27`, design §10 entry 24, P11-18). New public static class `Dexpace.Sdk.Core.Diagnostics.TraceIdGeneration` with `W3C()` and
  `Datadog()` (both return an `ActivityTraceId`, drawn from `RandomNumberGenerator` and drawn again on an all-zero draw, which the runtime's own generator does not
  do) and `FormatDatadog(ActivityTraceId)` (the low 64 bits as an unsigned decimal string; the default id and a zero low half throw `ArgumentException`). The SDK still
  never sets the process-wide hook: the application opts in with `Activity.TraceIdGenerator = TraceIdGeneration.Datadog;`. Additive.
- **`RetryOptions.PacingHeaders`: the stage stack's pacing headers are an ordered list** (`RETRY-21`, design §11 item 54). New `IReadOnlyList<string> PacingHeaders { get; init; }`,
  defaulting to `Retry-After`, `retry-after-ms`, `x-ms-retry-after-ms`, `X-RateLimit-Reset` (the fixed order, so nothing changes by default). `RetryPolicy` reads the headers it
  lists, in that order, each by its own semantics; a header left out is not read, which is how a service that sends a delta-seconds `X-RateLimit-Reset` (read as an epoch
  time long past, so the retry fires at once) opts that header out. The list is a validated subset of the four names, matched ignoring case and stored in the spelling above, with
  no duplicate and at least one name (`HonorRetryAfter = false` stays the off switch), copied and read-only, and takes part in equality and `ToString`. The recovery stack keeps the
  fixed precedence. Configuration key `Retry:PacingHeaders` (a list that replaces the default) and `DexpaceConfigurationKeys.PacingHeaders`. Additive.

#### Fixed

- **Fixed (phase 11):** the pagination query splice copies empty segments verbatim (`PAGE-21`; design §10 entry 18). A stray `&` in a next-page URL (`?a=1&&b=2&`) used to be dropped when a
  cursor or page-number parameter was set (`?a=1&b=2&cursor=…`); it is now an untargeted parameter and is carried through (`?a=1&&b=2&&cursor=…`). A query with no characters still has no segments.
- **A null log field value is the literal `null`** (`OBS-3`, design §10 entry 22). The structured state of the `http.request` and `http.response` events (the latter also reports a failed attempt, id 102)
  keeps a pair whose value is `null` and emits the value as the string `null`, where the framework's own formatting would print `(null)`. No SDK site passes a
  null today (every `LoggerMessage.Define` argument is a non-nullable `string`), so this closes the rule structurally rather than changing an output.
- **An unexpected MD5 probe failure declines MD5 instead of failing construction** (`AUTH-15`, design §10 entry 16). The once-per-process probe that decides whether this host computes MD5
  treated only `CryptographicException` and `PlatformNotSupportedException` as a refusal; any other exception escaped and the `Lazy` cached it, so every `DigestChallengeHandler` would have
  thrown. Any non-fatal failure now means "refused" and drops the MD5 algorithms from the handler's preference; a fatal one still propagates.
- **Fixed (phase 11):** `UrlRedactor`'s two backstops no longer turn a fatal exception into the redaction sentinel (`RECOV-2`, `RETRY-25`, `XCUT-20`; design §10 entry 12). They caught
  `Exception` unfiltered, so an `OutOfMemoryException` while redacting a URL was swallowed; they now filter with `ExceptionFacts.IsFatal`. `FatalFilterArchitectureTests` scans the four runtime
  libraries and fails a catch-all that neither ends in a rethrow nor excludes fatal exceptions in its `when`.
- **Fixed (phase 11):** the quiet dispose records its failure only on a recording activity (`OBS-21`; design §10 entry 23). An `Activity` whose sampler only propagates context
  (`IsAllDataRequested` false) still stored an `exception` event, so a span that must be inert was written to; the write is now guarded.
- **Fixed (phase 11):** `RequestBody.FromFile` accepts an empty regular file the process may not read again (`BODY-11`, `HTTP-40`; design §10 entry 31). The Unix type check wrote the path into an
  in-memory tar archive, and the tar writer opens a regular file to attach its content, so a mode-000 empty file failed construction with an `UnauthorizedAccessException` whose message is the full path
  (it was accepted before phase 11, since a count of 0 never opens the file). A refusal to open, or any other system-call failure inside the writer, now classifies the path as regular; only the managed
  `IOException` the writer throws for a file type it has no tar type for (a Unix domain socket, told by its `HResult` carrying no errno) is reported as a `socket`, and no operating-system message
  reaches the caller. A FIFO and a device are classified without being opened, mode 000 or not.
- **Fixed (phase 11):** the ledger-audit gate no longer lets a negative scan pass by reading nothing (`LedgerAuditTests`, rule L10). A scan whose *Paths* cell holds no glob, a glob that is not `<dir>/**/*.<ext>` or `*.{a,b}`, or a glob whose directory
  holds no file of its extension was skipped silently, so a renamed directory made its absence claim true vacuously; each is now a finding, with a fixture for each that must fail. Rule L3 is unchanged and checks that a cited line exists in its file, not that it
  still holds what the ledger says; `CLAUDE.md` now says so instead of promising to fail a stale citation.
- **Fixed (phase 11):** two conformance-kit assertions no longer accept a raw `IOException` escaping a third-party transport (`TRANSPORT-18`, `TRANSPORT-7`, `TRANSPORT-20`; design §10 entry 7). Moving the transport failure under `IOException` had loosened
  `transport-18.native-resend-identical` (a body source that fails mid-write) and `transport-7.attempt-timeout-aborts` (an attempt that outlives its deadline) to "an `IOException`"; both now require the transport-failure type, an `IOException` that is an
  `IRetryableError` (the attempt-timeout assertion still accepts any `SdkException`). A transport that lets the body source's own exception, or a raw `IOException` wrapping a timeout, reach the caller fails them; each clause has a negative control.
- **Fixed (phase 11):** a second `HttpResponseException.GetError` / `GetErrorAsync` over a `SystemNetHttpClient` response throws `ResponseNotReadException` again (`BODY-14`, `HTTP-41`; design §10 entry 6). The body returns the first stream on a
  second open, and the content stream `SocketsHttpHandler` hands out reads 0 bytes after it is disposed instead of throwing, so the second call decoded an empty payload (the CHANGELOG entry above said it threw). The transport's body now hands out a
  stream that reports its own disposal: once a consumer disposed it, the same handle comes back from a later open and every read throws `ObjectDisposedException`, as the stream of every other body does, which `GetError*` already reads as "already
  consumed". A manual open, drain and dispose followed by a convenience read now fails the same way instead of returning an empty array.
- **Fixed (phase 11):** two concurrent first opens of a `SystemNetHttpClient` response body no longer race inside `HttpContent` (`BODY-14`, `HTTP-41`; design §10 entry 6). Both opens called `ReadAsStream` / `ReadAsStreamAsync`, and a response
  content can be consumed once, so the second caller got an `InvalidOperationException` ("The stream was already consumed") from `SocketsHttpHandler`'s content (4 threads, 40 bodies: reproduced on every run), or an
  `HttpRequestException` for a synchronous read of a stream first obtained asynchronously. The first open now runs under a gate taken by both forms, so every open of either form is handed the one stream; a queued open takes the
  caller's token and gives up alone, a failed opener leaves the next open to try, and a body disposed while its first open is in flight reports `StreamClosedException`. Every open after the first is still one volatile read.
- **Fixed (phase 11):** the retry attempt header can no longer overwrite a configured idempotency key (`RETRY-38`; design §11 item 10). `RetryOptions.AttemptHeaderName` refused only the literal
  `Idempotency-Key`, but `IdempotencyKeyStep.HeaderName` is configurable, so `HeaderName = "X-Request-Key"` with `AttemptHeaderName = "X-Request-Key"` replaced the key with the attempt number
  on every send of a retried `POST`. The pair is now refused where the stack that holds both is assembled: `PipelineBuilder.Build` (an `InvalidOperationException`) and a per-call
  `DexpaceClientOptions` override passed to `HttpPipeline.SendAsync`/`Send` (an `ArgumentException`, before anything is sent) for an `IdempotencyPolicy` beside a `RetryPolicy`, and the
  `RecoveryDispatcher` constructor (an `ArgumentException`) for an `IdempotencyKeyStep` in the request chain beside a `RetryRecovery`. Names compare ignoring case; a custom policy or step that
  stamps a key of its own is opaque to the check.

### Phase 7a — serde

Sub-phase 7a of roadmap phase 7 (`SERDE-1`..`SERDE-30`, plus `HTTP-44` and `HTTP-45` carried from 3b); design and plan under
`docs/work/mvp/phase7/phase7a/`.

#### Added

- `Tristate<T>`, the three-state PATCH field (Absent, Null, Present; `default` is Absent), with the static `Tristate` helper
  (`Absent`, `Null`, `Present`, `FromNullable`, `GetValueOrNull`), `TristateSentinel`, `TristateState`, and the codec-adapter hook
  `ITristate` / `ITristateVisitor<TResult>`. `Present(null)` throws, an implicit conversion from `T` maps `null` to Null,
  `ToString` is `Absent`, `Null` or `Present(<value>)` (`SERDE-14`, `SERDE-17`, `SERDE-18`, `SERDE-30`). The type carries no
  System.Text.Json attribute (an architecture test pins it).
- `SystemTextJsonSerde` Tristate wiring (`SERDE-15`, `SERDE-16`, `SERDE-19`, `SERDE-20`): an Absent `Tristate<T>` property is omitted
  through a `JsonTypeInfo` modifier (composed with any `ShouldSerialize` the caller set), Null is written as `null`, a JSON `null`
  reads as Null and a missing key as Absent, for reference and value-type `T` alike and with no reflection (a public
  `ITristate` visitor hook and one justified `IL2067` suppression, exercised by the AOT smoke's `Tristate<int>`). Where the wire has no
  key (the document root, an array element, a dictionary value) an Absent degrades to `null`. New public
  `TristateJsonSerializerOptionsExtensions.AddTristateSupport(JsonSerializerOptions)` for callers who use `JsonSerializer` directly on SDK models
  (without it `JsonSerializer` throws `InvalidOperationException` for an Absent or Null field and writes a Present one as an object of the struct's properties).
- `SystemTextJsonSerde.CreateDefaultOptions(IJsonTypeInfoResolver)`: a fresh, mutable instance per call with `Web` naming, strict numbers
  (`NumberHandling.Strict`, forced back from `Web`'s `AllowReadingFromString`) and `RespectNullableAnnotations`, Tristate-wired
  (`SERDE-21`, `SERDE-25`). Tests pin the nine strict-coercion rows individually, the two permitted widenings, unmapped-member skipping,
  ISO-8601 dates and 32 concurrent workers on one serde (`SERDE-22`, `SERDE-23`, `SERDE-24`, `SERDE-29`).
- The typed readers on `ResponseBody` (`SERDE-7`, `SERDE-13`, `SERDE-27`): `ReadValueOrDefaultAsync<T>` (admits a wire `null`),
  `ReadValue<T>` and `ReadValueOrDefault<T>` (the synchronous twins), beside `ReadValueAsync<T>`. Each streams (one byte is peeked
  to tell a missing payload from a present one, then replayed to the codec; nothing is materialised) and disposes the body on every
  path, attaching a dispose failure to the failure in flight. A body with no payload (a 204, a zero-length body) fails with a
  `DeserializationException` naming `T`.
- `ISerde.Deserialize<T>(Stream)`, the synchronous stream decode, as a **default interface member**: source- and
  binary-compatible for every implementer. The default reads the stream under `ResponseBody.DefaultMaxMaterializedBytes`
  (a seekable source that declares more is refused before any read) and calls the span decode; `SystemTextJsonSerde` overrides it
  and streams. `HttpResponseException.GetError<T>(ISerde)`, the synchronous twin of `GetErrorAsync` (`SERDE-3`, `SERDE-9`, `SERDE-12`).
- `IResponseHandler<T>` (the SPI a generated SDK implements; it owns and disposes the response) and `ResponseHandlers.Deserialize<T>(ISerde)` /
  `ResponseHandlers.DeserializeOnSuccess<T>(ISerde)` (`SERDE-27`, `SERDE-28`). The status-aware handler decodes a 2xx; throws
  `HttpResponseException` over the bounded error-body buffer for a 400 to 599 (the one capture site, with the 1 MiB bound); and for anything else
  (a 1xx, an unfollowed 3xx, a 304) disposes the response and fails with a `DeserializationException` that leads with the status code and carries the
  raw `ETag` and the `Location` resolved against the request URL and redacted through `UrlRedactor` (the one-line rewrite of control characters runs after the
  redaction, so a tab inside the userinfo cannot move the credential into the query). A new `Security` class pins the redaction.
- `TypedResponse<T>` (`HTTP-44`, `HTTP-45`): a response whose metadata is readable at once and whose body is parsed lazily, once, on the first
  `GetValueAsync`. Success (a `null` included) and failure are memoized, a failure as the same exception object; exactly-once is a compare-and-swap on a
  `TaskCompletionSource<T>`, so no caller blocks a thread behind another's parse, and a caller's token cancels only its own wait. The wrapped response is not exposed.
- `docs/sdk-documentation/serde.md`; the AOT smoke performs the Tristate PATCH round trip (the wire bytes `{"name":null,"size":3}`, a lazy `TypedResponse<WidgetPatch>`
  through the status-aware handler, the root-null rule and the synchronous stream decode), which exercises the converter factory's `IL2067` suppression with the value-type `Tristate<int>`.

#### Changed

- **Breaking (phase 7a):** `SystemTextJsonSerde(JsonSerializerOptions)` and `SystemTextJsonSerde(JsonSerializerContext)` work on a private
  copy of the options, wire `Tristate<T>` on the copy and freeze the copy. The caller's `JsonSerializerOptions` is no longer made
  read-only, gains no converter and keeps its resolver; a later change to it does not affect the serde (was: the caller's instance was
  frozen) (`SERDE-19`, `SERDE-26`). A context generated with `GenerationMode = Serialization` (the fast-path-only mode) carries no property
  metadata and serves only its own options object, so it can no longer be passed to the serde; use the default or `Metadata` mode.
- **Breaking (phase 7a):** `ResponseBodySerdeExtensions.ReadValueAsync<T>` returns `ValueTask<T>` (was: `ValueTask<T?>`) and throws a
  `DeserializationException` naming `T` for a wire `null` into a reference-type target (was: returned `null`). Use
  `ReadValueOrDefaultAsync<T>` to accept `null`; a `Nullable<>` target still decodes `null` (`SERDE-13`).
- **Breaking (phase 7a):** `ReadValueAsync<T>` disposes the **body** on every path (was: only the stream it opened), so a body is
  spent after a typed read whether or not it succeeded.
- **Breaking (phase 7a):** `ReadValueAsync<T>` on an empty body (a 204, a zero-length body) throws a `DeserializationException`
  `The response has no body to deserialize as '{T}'.` with no inner exception (was: a `DeserializationException` wrapping the
  codec's "no JSON tokens" failure) (`SERDE-27`).
- **Breaking (phase 7a), binary-compatible:** `ISerde` gains `Deserialize<T>(Stream)` with a default implementation, so every
  existing implementer keeps compiling and loading; it is listed because the public API snapshot diffs it.

#### Fixed

- `SERDE-26`: constructing a serde no longer freezes the caller's `JsonSerializerOptions` (see the Breaking entry above).

[Unreleased]: https://github.com/dexpace/dotnet-sdk/commits/main
