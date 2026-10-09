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

### Added

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
