# The reference transport

**As built by phase 8b, written against source on 2026-10-11** (branch `89-phase-8b-reference-transport`). This page describes how
`Dexpace.Sdk.Http.SystemNet` sends a request after phase 8b: the four ways to construct `SystemNetHttpClient` and who owns what, the
proxy it installs, the per-call deadline and the one table that classifies a native failure, the genuinely synchronous `Execute`
and the handler hazard it carries, how a request header is mapped and what happens to one that cannot be sent, and what the
transport does with a single-use body the native client sends twice. The requirement IDs it cites are the normative text
(`docs/product-spec/17-transport-adapter-conformance-contract.md`, `TRANSPORT-1` to `TRANSPORT-30`); the decisions behind each
behaviour are in the [phase 8b design](../work/mvp/phase8/phase8b/2026-10-09-phase8b-reference-transport-design.md) (rulings
`P8b-1` to `P8b-24`) and in [design §3.2 and §3.3](../sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md). Neither is restated
here. The transport is proven by the [conformance kit](./conformance.md): both of its constructions run the whole suite, with one
permanent, narrowed waiver for the owned construction and two for the handler-taking one (below; *corrected 2026-10-20, phase 11*:
phase 8b left two for both, and phase 11 retired the second for the owned construction).

## Construction and ownership (`TRANSPORT-1`, `-15`; P8b-2, P8b-3)

What a constructor takes states who owns what. Pass a handler and you keep it; pass nothing and the transport owns everything.

| Constructor | Native client | Handler | Proxy | Can the native layer re-send? (`TRANSPORT-18`) |
|---|---|---|---|---|
| `()`, `(ILogger)`, `(SystemNetHttpClientOptions)` | owned, built by the transport | owned `SocketsHttpHandler { AllowAutoRedirect = false }`, disposed with the transport | `SystemNetHttpClientOptions.Proxy`, else direct | only when the proxy is an HTTP proxy that carries credentials |
| `(HttpMessageHandler)`, `(HttpMessageHandler, SystemNetHttpClientOptions)` | owned, an `HttpClient` the transport builds over your handler | **yours**: never disposed, never changed | yours; `Proxy` in the options throws | yes (the chain is opaque) |
| `(HttpClient)`, `(HttpClient, ILogger)`, `(HttpClient, SystemNetHttpClientOptions)` | **yours**: never disposed, never changed; its `Timeout` stays an outer bound, and the transport's 100 s default cuts a longer one | invisible | yours; `Proxy` in the options throws | yes |

```csharp
// Owned: nothing to dispose but the transport. No proxy, and no environment variable is read.
await using var owned = new SystemNetHttpClient();

// Your handler (an IHttpClientFactory primary handler, say). It must not follow redirects; the SDK is the only redirect authority.
var handler = new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
await using var viaHandler = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Timeout = TimeSpan.FromSeconds(30) });
```

**The handler check.** The handler constructor walks the `DelegatingHandler` chain to its primary handler and throws
`ArgumentException` (naming `AllowAutoRedirect` and `TRANSPORT-1`) when that primary is a `SocketsHttpHandler` or an
`HttpClientHandler` with `AllowAutoRedirect == true`, which is the default of both: `new SystemNetHttpClient(new SocketsHttpHandler())`
therefore throws. It does not flip the setting, because a caller's handler is never mutated. A primary of any other type is accepted
(inspecting it would take reflection) and the last-line check still applies to every construction: a final request URI that differs
from the one sent in scheme, host, port or path fails the call with a non-retryable `SdkException`, because a credential may already
have crossed an origin. A chain whose innermost `DelegatingHandler` has no inner handler yet is
accepted (`IHttpClientFactory` assigns it later).

`SystemNetHttpClientOptions` is a sealed record validated where it is set (a `with` expression cannot bypass it) and read once, at
construction:

| Member | Default | Rule |
|---|---|---|
| `Logger` | `NullLogger.Instance` | not `null`; receives header-drop entries and the construction-time proxy warnings |
| `Proxy` | `null` (direct) | owned construction only (below) |
| `Timeout` | 100 seconds | `null` for no transport default, else `(0, 49 days]` |
| `HeaderDropLogging` | `FirstPerName` | `FirstPerName`, `Every`, `VerboseOnly` |
| `ResendBufferLimit` | 1 MiB | `>= 0`; `0` turns the replay capture off |
| `TimeProvider` | `TimeProvider.System` | arms the per-call deadline; tests pass a fake |

**After dispose (`SEAM-15`).** Every call made after `Dispose`/`DisposeAsync` throws `ObjectDisposedException` for every construction,
a borrowed client included; the asynchronous face returns a faulted task and never throws from the call (`TRANSPORT-21`). Your own
`HttpClient` keeps working. Disposal is idempotent (`SEAM-14`) and never waits for a call in flight (`TRANSPORT-16`): a call
in flight when an owned transport is disposed ends as an internal cancellation (row 4 below).

## The proxy (`TRANSPORT-30`, `CFG-22`, `CFG-23`, `CFG-27`, `CFG-28`; P8b-4, P8b-5)

The owned handler goes **direct** unless `SystemNetHttpClientOptions.Proxy` is set: its `UseProxy` is `false` and its `Proxy`
`null`, so the runtime's process-wide default proxy, which reads `HTTP_PROXY`, `HTTPS_PROXY` and `NO_PROXY` on first use, is never
consulted (`HttpClient.DefaultProxy` is banned in `src/`, and an IL scan of the assembly pins that nothing reads the environment
or a process-wide default). To use the environment, resolve it yourself, once, explicitly:

```csharp
var options = new SystemNetHttpClientOptions { Proxy = ProxyOptions.FromEnvironment() };   // null when no proxy is configured
await using var transport = new SystemNetHttpClient(options);
```

A `Proxy` passed with a handler or a client throws `ArgumentException`: the proxy of something you own is yours to configure.

An internal `IWebProxy` over the `ProxyOptions` is installed on the owned handler:

- **The proxy URI never carries userinfo** (`http://host:port/`, `socks4://…`, `socks5://…`; an IPv6 host is bracketed), so no runtime
  log line or exception message can print a credential.
- **Bypass is the specification's glob dialect** (`ProxyOptions.IsBypassed`, compared against the destination's IDN host): `*`
  any run, `?` one character, a leading dot is a literal, `BypassAll` bypasses everything. Loopback is not special-cased.
- **Credentials are `ChallengeCredentials` when set** (the native client asks it per scheme when the proxy answers `407`, which
  is `CFG-22`'s challenge hook), **else a Basic-only pair** built from `UserName`/`Password`: it answers `Basic` and nothing else, so
  a proxy that challenges with Digest, NTLM or Negotiate gets no credential it did not ask for. For a SOCKS proxy the runtime asks
  once per connection with the proxy scheme as the authentication type (`socks5`, `socks4`: measured on .NET 10.0.401), and SOCKS4
  carries a user id only, so its answer has an empty password.
- **The handler's own `Credentials` stay unset**, so a proxy credential is only ever answered to a `407` from that proxy and never to an
  origin's `401`. This is the `MUST` half of `TRANSPORT-30`, pinned by the permanent `Security` class `ProxyCredentialWireTests`
  (a forward-proxy fixture: the `407` is answered, the origin's `401` behind it is not, no `Authorization` header carries the pair,
  and no log entry at any level contains the user name or password).
- **Settings the native client cannot honour as written are warned about once, at construction** (`Warning`, event 113,
  `http.transport.proxy_feature_unsupported`), naming the feature and the `ProxyOptions` member and never a value: a `Password` on a
  SOCKS4 proxy (ignored), `ChallengeCredentials` on a SOCKS proxy (there is no challenge; the hook is asked once per connection), and
  `ChallengeCredentials` together with a `UserName` (the hook wins, the pair is ignored).
- **SOCKS4 resolves the destination host locally** (the runtime's `socks4` scheme), including for a `socks4a` URL, which
  `ProxyOptions.FromEnvironment` maps to the same type. Use SOCKS5 where the proxy should resolve names.

Adapting a core `IChallengeHandler` (phase 6c) to an `ICredentials` for `ChallengeCredentials` is the caller's, not shipped.

## Deadlines and the failure classification (`TRANSPORT-3`, `-4`, `-5`, `-6`, `-8`, `-20`; `XCUT-1`, `XCUT-2`; P8b-6, P8b-7)

A call's deadline is `RequestOptions.Timeout` when it sets one, else `SystemNetHttpClientOptions.Timeout`, else none. It is armed per
call on the options' `TimeProvider`, linked with the caller's token, and never changes the shared `HttpClient` (the transport's own
`HttpClient.Timeout` is infinite; the 100 second default above replaces it). A deadline below 1 ms is raised to 1 ms and one above
49 days lowered to 49 days: never refused, and never read as "no deadline" (`TRANSPORT-6`). It bounds the exchange **until the response
headers arrive**, as `AttemptTimeout` (phase 6a) does; the body read is bounded by the token you pass to the read, because the deadline's
source is disposed when `SendAsync` returns, which does not affect the body (measured: disposing the token after a `ResponseHeadersRead`
send leaves a later chunk readable). A borrowed client's own `Timeout` remains an outer bound that a longer per-call value cannot lift,
since lengthening a client shared with others would mean changing it.

The transport's default applies to a **borrowed** client too, so the shorter of the default and the client's own `Timeout` bounds a call
that sets none: a borrowed `HttpClient` with an infinite or longer `Timeout` is cut at 100 seconds with a retryable
`ServiceRequestTimeoutException`, where before phase 8b its own `Timeout` alone bounded the call (breaking change 3, below).
`new SystemNetHttpClient(client, new SystemNetHttpClientOptions { Timeout = null })` leaves the client's own `Timeout` the only bound; a
per-call `RequestOptions.Timeout` replaces the default for that call (`DeadlineTests` pins all three).

One helper classifies every native failure, on both faces, state first and the shape of the runtime's exception second:

| # | Condition | Surfaces as | Rows |
|---|---|---|---|
| 1 | the caller's token is signalled | an `OperationCanceledException` that carries **the caller's token** (the native one as it is when it already does; otherwise a new one with the native exception on its `ExceptionTrail`, because a linked deadline token is not the caller's) | `TRANSPORT-3`, `XCUT-1` |
| 2 | the transport's deadline fired | `ServiceRequestTimeoutException` (retryable), the native exception as its cause | `TRANSPORT-4`, `-5`, `XCUT-2` |
| 3 | a cancellation whose cause is a `TimeoutException` (a borrowed client's `HttpClient.Timeout`) | `ServiceRequestTimeoutException` | `TRANSPORT-4` |
| 4 | any other cancellation: `CancelPendingRequests`, the native client or this transport disposed mid-call | a **new, terminal** `OperationCanceledException` with the native exception on its `ExceptionTrail`, **not** its `InnerException` | `TRANSPORT-8` |
| 5 | a native request failure (`HttpRequestException`) | `ServiceRequestException` (retryable), the native exception as its cause; it is an `IOException`, not an `SdkException` (phase 11) | `TRANSPORT-20` |
| – | `ObjectDisposedException`, `NotSupportedException`, anything else | propagates unwrapped: a configuration error or a defect, not a transport failure | |

Row 1 comes first, so a call the caller cancelled is cancelled even if its deadline also fired. Row 4 keeps the native exception off
`InnerException` on purpose: the retry classifier walks the cause chain and calls any `IOException` in it retryable, and the runtime's
internal cancellation is a `TaskCanceledException` over an `IOException`, so a cause there would have the SDK retry a cancellation
nobody asked for. A new SDK exception type for row 4 was considered and not built (ruling P8b-7); it would still need the trail trick.

*Corrected 2026-10-20 (phase 11, design §10 entry 7, retired).* `ServiceRequestException` and `ServiceRequestTimeoutException` derive from `IOException` and keep `IRetryableError`
(`TRANSPORT-20`: a transport failure is catchable as a generic I/O error), so `catch (SdkException)` does not catch one: catch `ServiceRequestException` or `IOException`. A
transport failure has no `Suppressed` property; read the trail with `ExceptionTrail.GetSuppressed(exception)`, and `ToString()` still renders it. The retry classifier reads
`IRetryableError` and the cause chain, never the SDK root, so no retry decision changed. Row 5's message follows what the runtime says happened: "The request could not be sent to
the server." for a name-resolution, connect, TLS or proxy-tunnel failure (the request never left), and "The request failed and no response was received." for the rest, including a
connection that ended after the request arrived. Neither carries a URL, a header value or a credential (`XCUT-18`). `ServiceResponseException`, which no transport ever threw, is
removed; a failure with no response is a `ServiceRequestException`.

**Breaking** (phase 8b): `RequestOptions.Timeout` was ignored; an internal cancellation used to surface as a retryable
`ServiceRequestTimeoutException`.

## The synchronous path and its handler hazard (`SEAM-11`, `PIPE-28`, `TRANSPORT-17`; P8b-9)

`Execute` calls `HttpClient.Send` with `ResponseHeadersRead` on the calling thread, for every construction, and writes the request
body through `RequestBody.WriteTo`; nothing in the package blocks on an asynchronous operation. The deadline, the classification and
the guarded adaptation are the asynchronous face's, and both faces build the message with the one mapper, so the `traceparent` rule
of [`tracing-and-metrics.md`](./tracing-and-metrics.md) holds on both. A `RequestBody` subclass that does not override `WriteTo` fails
the sync send with `NotSupportedException`; every body the SDK creates overrides it.

The hazard is the runtime's, not the SDK's, and it only concerns a handler **you** supply:

- A primary handler that overrides only `SendAsync` makes `Execute` throw `NotSupportedException` (loudly): *"The synchronous method is
  not supported by '…'. … you must override its 'Send' virtual method."*
- A `DelegatingHandler` in the chain that overrides only `SendAsync` is **skipped silently** on the sync face: the runtime forwards the
  synchronous call straight to the inner handler, so a header-adding or logging handler written that way never runs. The async face
  runs it.

Implement `Send` in such a handler, or call `ExecuteAsync`. The runtime's `SocketsHttpHandler` and `HttpClientHandler` implement both;
check any handler you add from a library. The behaviour is recorded in
[`docs/first-release.md`](../first-release.md) as `sync-borrowed-handlers`. `HttpClient.Send` is HTTP/1.1 only, and the transport never
sets `HttpRequestMessage.Version`, so requests stay HTTP/1.1 on both faces.

**Breaking** (phase 8b): over a borrowed `HttpClient` or a handler whose primary implements only `SendAsync`, `Execute` used to work
(it blocked on the asynchronous send) and now throws.

## Request headers and the drop policy (`TRANSPORT-10`, `-11`, `-12`, `-13`, `-26`; `OBS-19`, `XCUT-18`; P8b-11 to P8b-13)

`System.Net.Http` keeps request headers and content headers in two collections and refuses a header in the wrong one. The mapper puts
each header in its place, per header, after the trace-context strip:

| Header | With a body | Body-less `POST`, `PUT`, `PATCH`, extension methods, `DELETE`, `OPTIONS` | Body-less `GET`, `HEAD`, `TRACE` |
|---|---|---|---|
| a framing header (`Host`, `Content-Length`, `Transfer-Encoding`, `Connection`, `Keep-Alive`, `Upgrade`, `TE`, `Expect`) | dropped, `Debug`, event 111 | same | same |
| fails the wire re-check (a value accepted on the inbound path, re-used on a request) | dropped under the policy, event 110 | same | same |
| a content header (`Allow`, `Content-*`, `Expires`, `Last-Modified`) | the content; the caller's `Content-Type` wins over the body's | an empty content carries it; the wire shows `Content-Length: 0` | dropped under the policy, event 110 (content would break `TRANSPORT-26`) |
| any other | the request | the request | the request |

The body's media type is stamped only when the caller set no `Content-Type` (the name matches ignoring case); a multipart body's own
boundary is part of its type, so a caller type without it wins and the caller owns the consequence. A body-less `DELETE` or `OPTIONS`
carries an empty content only to carry a content header the caller set.

**The drop policy** (`SystemNetHttpClientOptions.HeaderDropLogging`): a drop is never silent and only the header **name** is logged,
escaped for a log line (`XCUT-18`). `FirstPerName` (default) logs the first drop of a name at `Warning` and later ones at `Debug`;
`Every` logs each at `Warning`; `VerboseOnly` logs each at `Debug`. The names already seen are a lock-guarded, case-insensitive set of
at most 128 names with the oldest evicted first, per transport instance and pure logging state. A framing drop and an inbound drop are
always `Debug`. A logger that throws is swallowed (`OBS-20`). The events are in core's `DexpaceLogEvents`:

| Id | Name | Level | When |
|---|---|---|---|
| 110 | `http.transport.header_dropped` | per policy | a request header the native client cannot send |
| 111 | `http.transport.framing_header_dropped` | `Debug` | a caller-set framing header the transport computes |
| 112 | `http.transport.inbound_header_dropped` | `Debug` | a response header whose value carries a control character |
| 113 | `http.transport.proxy_feature_unsupported` | `Warning` | a proxy setting the native client cannot honour, once at construction |

**Inbound** (`TRANSPORT-14`): a response header whose value carries a control character is dropped alone and the rest delivered;
obs-text in a value is preserved. A malformed header **name** is not reachable: see the last section.

**Breaking** (phase 8b): the body's `Content-Type` used to override the caller's header; a content header on a body-less request used to
vanish unlogged; the drop events were ids 1 to 3, and a repeated drop of one name was a `Warning` each time.

## Single-use bodies and the re-send limit (`TRANSPORT-17`, `-18`, `-2`; P8b-10)

The native client sends a request again behind the transport's back in some cases, and each send serialises the content again. With a
forward proxy that answers `407 Proxy-Authenticate: Basic`, `SocketsHttpHandler` re-sends an `http://` request's body with the
credential every time (measured: the challenge is not cached, so a second request on the same connection is re-sent too); an `https://`
target is challenged at the `CONNECT`, before any body byte, and never re-sends. A single-use body (a non-seekable
`RequestBody.FromStream`) cannot be read twice, so where the native layer can re-send (the table at the top) the transport tees the
**first** write into an unpooled capture of at most `ResendBufferLimit` bytes (1 MiB by default) and replays it byte for byte. The
source is read once. A replayable body (bytes, string, a seekable stream with a declared length, a file range) is never captured, and an
owned transport with no proxy credentials never captures at all: it cannot re-send.

If the first write did not complete, or the body outgrew the limit, a later serialisation is refused: the send fails with a retryable
`ServiceRequestException` whose cause chain is `HttpRequestException` → `IOException` → `StreamConsumedException`, the `IOException`'s
message naming `ResendBufferLimit` and `ToReplayableAsync`. The transport never ships a truncated body, and a body of any size is still
sent once. To upload a larger single-use stream through a client that can re-send, raise `ResendBufferLimit` or call
`RequestBody.ToReplayableAsync()` first.

**`TRANSPORT-2` (the native retry is off for the transport the SDK builds).** `SocketsHttpHandler` has no switch for its own connection-failure retry. Per runtime source
(`HttpConnection.SendAsync`), a request is retryable on a connection that ended before any response byte only while it has no content or
an `Expect: 100-continue`, and the transport therefore gives a body-less `POST`, `PUT`, `PATCH` or extension-method request a
zero-length content: the native client writes `Content-Length: 0` for these methods anyway, so the wire is byte for byte the same, and with
content present the retry cannot fire (a caller `Expect` is a framing header and is dropped, so it cannot bring the retry back). The
conformance kit and `BodylessRetryWireTests` pin it with a server that reads each request and closes in order. A body-less `GET`, `HEAD`,
`OPTIONS`, `DELETE` and `TRACE` is idempotent (RFC 9110 section 9.2.2 permits the retry) and cannot be given content without breaking
`TRANSPORT-26` or RFC 9110 section 8.6.

*Corrected 2026-10-20 (phase 11, design §10 entry 33, retired; §11 item 71).* Phase 8b recorded those five methods as re-sent up to three more times and waived the kit's
`transport-2.bodyless-not-resent` for it. They no longer are, on the handler the SDK builds. `SystemNetHttpClient.CreatePrimaryHandler`, which the owned construction and the DI named
client use, sets the handler's `PlaintextStreamFilter` to a pass-through stream (`FirstResponseByteStream`, HTTP/1.x only) that tracks one fact, a write with no response byte read since,
and turns a read of 0 in that state into the `HttpIOException` (`HttpRequestError.ResponseEnded`) the runtime raises itself when a response ends prematurely. The runtime takes its
retry only on a zero-byte read and not on an exception, so the request fails once, reaches the server once (bar the idle-connection window below), and the failure maps to a `ServiceRequestException` that the SDK's own
retry layer classifies and may send again under its re-send gate. No byte is inspected, buffered or changed, and an idle connection the server closes is untouched, because the
state is only set by a write. **The idle-connection window (reasoned from the runtime's source, not reproduced).** The filter's state is set by a request's first write, so a read of 0 before it passes through: an idle connection the server closes is not a failed request, and the pool relies on that read to discard a dead connection. The runtime reuses a pooled connection after checking that the read it keeps outstanding has not completed. A server close that lands after that check and before the first write completes that read with 0 while the state is clear; the runtime writes the request into the closed connection, takes the 0 as the first read of the response, and may re-send the request once on a new connection. This is the stale-pooled-connection retry every HTTP client has, on a request that usually never reached the server's application because the connection was already closed there (a server that half-closed and still reads could have acted on the first copy). "Reaches the server once" on this page therefore means not up to four times and not again after a write to a connection that was open when it began; it does not mean never twice under that race. The SDK's own retry layer is unaffected: it classifies and re-sends under its re-send gate, once per attempt, whatever the runtime does inside an attempt.
**What it does not cover.** A handler or `HttpClient` you supply is your native client: the SDK does not wrap it and it keeps the runtime's retry, so
such a request can still arrive up to four times (retry through the SDK's pipeline, or build the handler with `CreatePrimaryHandler`); a caller who replaces `PlaintextStreamFilter`
on a handler from `CreatePrimaryHandler` removes the guarantee. The kit's `transport-2.bodyless-not-resent` waiver is therefore deleted for the owned and DI subjects and kept,
rescoped, for the handler-taking subject alone (`SystemNetSubject.OptionsFor`), where it turns stale if the runtime ever stops the retry. `TRACE` natively writes
`Content-Length: 0`, so a zero-length content there would be wire-identical; it is left out deliberately (`TRANSPORT-26`).

## What the transport does not do (`TRANSPORT-9`, `-14`, `-28`; P8b-8, P8b-14, P8b-15)

- **No abandoned response (`TRANSPORT-9`).** `ExecuteAsync` is an `async` method that returns the response `SendAsync` produced; the deadline
  is enforced by cancelling the token `SendAsync` observes, never by racing the call against a timer. The primitives that build a
  settle race (`Task.WaitAsync`, `WhenAny`, `WhenAll`, `Task.Delay`, a `TaskCompletionSource<Response>`) appear nowhere in the assembly
  (an IL scan pins it), so there is nothing for `LateResult` to dispose after.
- **`TRANSPORT-14`'s malformed header *name* clause is unmet.** `SocketsHttpHandler` rejects the **whole response** on a header name
  with a space, a control byte or a non-ASCII byte before the adapter sees any header, and has no leniency switch. The value and obs-text
  clauses are met. Recorded as
  [design §10 entry 32](../sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md) and a `docs/first-release.md`
  *Unsatisfied MUSTs* line; the kit's `transport-14.name-malformed-dropped` carries the permanent waiver, for every subject.
  *Corrected 2026-10-20 (phase 11, V7).* This page said no handler-level workaround exists short of replacing the parser. A `PlaintextStreamFilter` that dropped a bad line would turn
  the failure into a 200 with the other headers, but only by parsing every response head ahead of the runtime's parser (1xx heads, folded lines, lenient line endings), which is a
  second HTTP parser in the byte path of every response and not the pass-through of the stream filter above. The transport declines it; the clause stays unmet for that reason.
- **`TRANSPORT-28`'s zero-copy SHOULD is declined.** The MUST half holds: a file range (`RequestBody.FromFile(path, type, offset, count)`) is
  replayable, each write opens a fresh handle and copies exactly the range, so a retry, a redirect or a re-send carries the same bytes. But
  `HttpClient` writes content to its own connection stream (possibly an `SslStream`), so no sendfile path is reachable through it.

## Breaking changes and migrations (all phase 8b, all in the `CHANGELOG.md` `[Unreleased]` entry)

| # | Change | Migration |
|---|---|---|
| 1 | the owned transport no longer reads `HTTP_PROXY`/`HTTPS_PROXY`/`NO_PROXY` | `new SystemNetHttpClient(new SystemNetHttpClientOptions { Proxy = ProxyOptions.FromEnvironment() })` |
| 2 | `Execute` is genuinely synchronous | implement `Send` in a custom handler, or call `ExecuteAsync` |
| 3 | `RequestOptions.Timeout` is enforced; the owned `HttpClient.Timeout` is infinite and the 100 s default replaces it (the owned bound is unchanged). **A borrowed `HttpClient` is now also cut at the 100 s default** where only its own `Timeout` bounded it | set `SystemNetHttpClientOptions.Timeout` (`null` for none); for a borrowed client with a longer `Timeout`, `new SystemNetHttpClient(client, new SystemNetHttpClientOptions { Timeout = null })` or a longer `RequestOptions.Timeout` |
| 4 | an internal cancellation is terminal | treat the `OperationCanceledException` as a cancellation; it is not retried |
| 5 | every call after `Dispose` throws `ObjectDisposedException`, borrowed clients included | do not use a disposed transport; your `HttpClient` is unaffected |
| 6 | the caller's `Content-Type` wins; a content header on a body-less request is sent (on `POST`/`PUT`/`PATCH`/`DELETE`/`OPTIONS`) or dropped with a log (`GET`/`HEAD`/`TRACE`) | stop relying on the body's type overriding your header |
| 7 | drop events are ids 110 to 112 with `http.transport.*` names; repeated drops are `Debug` under `FirstPerName` | filter on the new names; set `HeaderDropLogging = Every` for the old loudness |
| 8 | the handler constructor rejects a primary that follows redirects | set `AllowAutoRedirect = false` on the handler |
| 9 | a native re-send of a single-use body is replayed up to `ResendBufferLimit` (it used to fail), and fails with the limit named beyond it | raise `ResendBufferLimit`, or `ToReplayableAsync()` first |

A body-less `POST`, `PUT`, `PATCH` or extension-method request now carries a zero-length content (the same bytes on the wire) and is no
longer re-sent by the native client; that is a fix of `TRANSPORT-2`, not a break.

**Phase 11 (2026-10-20)**, in the same `CHANGELOG.md` entry:

| # | Change | Migration |
|---|---|---|
| 10 | `ServiceRequestException` is an `IOException`, not an `SdkException`; `ServiceResponseException` is removed | catch `ServiceRequestException` or `IOException`; `ExceptionTrail.GetSuppressed(exception)` for the trail |
| 11 | the owned transport and the DI named client no longer let `SocketsHttpHandler` re-send a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` or `TRACE`; it reaches the server once, bar the idle-connection window (it used to arrive up to four times) | nothing to do; retry through the SDK's pipeline. A handler you supply keeps the native retry |
| 12 | the message of a native request failure says whether the request could not be sent or no response was received | do not parse the message |

## Factory-built chains (`TRANSPORT-1`, `-15`, `-30`; phase 9, P9-12)

*Added 2026-10-13 (phase 9).* `SystemNetHttpClient.CreatePrimaryHandler(SystemNetHttpClientOptions)` returns the `SocketsHttpHandler` the owned construction builds: `AllowAutoRedirect = false`, `UseProxy` only when `Proxy` is set (through the
internal adapter over `ProxyOptions`), server `Credentials` unset, and the `TRANSPORT-30` warnings through `options.Logger`. The owned construction is built on it, so the invariants have one source. The caller owns the returned handler: pass it as the
primary of a chain given to `new SystemNetHttpClient(chain, options)` with `options.Proxy` left `null` (that constructor refuses a proxy: a handler you own keeps its own). The DI package builds each named client's primary this way and
composes it with `IHttpClientFactory`'s chain; see [`dependency-injection.md`](./dependency-injection.md). Since phase 11 the handler also carries the `PlaintextStreamFilter` that ends the native re-send (`TRANSPORT-2`, above), so a
chain built on it has the guarantee and a chain built on a handler of your own does not. The handler constructor's redirect check walks the chain to the primary, so a factory chain whose primary was left at its default
(`AllowAutoRedirect = true`) is refused.
