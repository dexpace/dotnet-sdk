# Dexpace.Sdk.Http.SystemNet

The reference transport for the dexpace .NET SDK. It adapts `System.Net.Http.HttpClient` to the
[`Dexpace.Sdk.Core`](https://www.nuget.org/packages/Dexpace.Sdk.Core) transport SPI, implementing both
`IAsyncHttpClient` and `IHttpClient`.

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`.

## Construction and ownership

What a constructor takes says who owns what:

| Constructor | Native client and handler | Proxy |
|---|---|---|
| `new SystemNetHttpClient()`, `(ILogger)`, `(SystemNetHttpClientOptions)` | owned; disposed with the transport | `SystemNetHttpClientOptions.Proxy`, else direct |
| `(HttpMessageHandler)`, `(HttpMessageHandler, SystemNetHttpClientOptions)` | the transport owns its `HttpClient`; **your handler is never disposed or changed** | yours |
| `(HttpClient)`, `(HttpClient, ILogger)`, `(HttpClient, SystemNetHttpClientOptions)` | **your client is never disposed or changed** | yours |

A handler whose primary `SocketsHttpHandler`/`HttpClientHandler` follows redirects (`AllowAutoRedirect` defaults to `true`) is
rejected with `ArgumentException`: the SDK's `RedirectPolicy` is the only redirect authority. Set `AllowAutoRedirect = false`.

## Behaviour

- **Streaming.** Response bodies are delivered as live streams (`HttpCompletionOption.ResponseHeadersRead`),
  never buffered. Dispose the `Response` to release the connection.
- **Redirects.** The adapter's own client never follows a redirect. A caller-supplied client that follows one anyway fails the
  call with an `SdkException` naming `AllowAutoRedirect`.
- **Proxy.** The owned transport goes **direct** unless you give it one: it never reads `HTTP_PROXY`, `HTTPS_PROXY` or `NO_PROXY`
  on its own. To use the environment, ask for it once, explicitly:
  `new SystemNetHttpClient(new SystemNetHttpClientOptions { Proxy = ProxyOptions.FromEnvironment() })`. A `407` is answered with
  your `ProxyOptions` credentials (Basic from `UserName`/`Password`, or your `ChallengeCredentials`), never an origin's `401`, and a
  credential is never logged.
- **Deadlines.** `RequestOptions.Timeout`, else `SystemNetHttpClientOptions.Timeout` (100 seconds), bounds the exchange until the
  response headers arrive; reading the body is bounded by the token you pass to the read. An elapsed deadline is a retryable
  `ServiceRequestTimeoutException`; your cancellation is an `OperationCanceledException`; a cancellation raised from inside the native
  client is a terminal `OperationCanceledException` that is not retried.
- **Synchronous calls.** `Execute` is genuinely synchronous (`HttpClient.Send`). A custom primary handler that overrides only
  `SendAsync` makes it throw `NotSupportedException`, and a `DelegatingHandler` that overrides only `SendAsync` is **skipped** on
  that face: implement `Send` in such handlers, or call `ExecuteAsync`.
- **Headers.** `Host` and the framing headers (`Content-Length`, `Transfer-Encoding`, `Connection`, …) are dropped from the request
  so `HttpClient` computes them, every other header is re-validated at the wire, and the **caller's `Content-Type` wins** over the
  body's. A header the transport cannot send is dropped alone and logged by name (events 110 to 112), never by value; the level
  follows `HeaderDropLogging` (`FirstPerName`, `Every`, `VerboseOnly`).
- **Single-use bodies.** Where the native client can send a request again (your handler or client, or an owned transport behind an
  authenticating HTTP proxy), the first write of a single-use stream body is kept, up to `ResendBufferLimit` (1 MiB), and replayed
  byte for byte; beyond it the send fails with a retryable `ServiceRequestException` naming the limit. Buffer with
  `ToReplayableAsync()` or raise the limit for larger uploads.
- **After dispose.** Every call throws `ObjectDisposedException` (a faulted task on the async face), borrowed clients included.
- **Errors.** Transport faults are mapped onto the SDK's `SdkException` hierarchy.
- **Trace context.** The pipeline stamps the attempt span's `traceparent` (and `tracestate`) on the request. When a
  `System.Net.Http` listener exists and runtime propagation is on (`System.Net.Http.EnableActivityPropagation`), the
  adapter drops that stamp, recognised by equality with `Activity.Current`'s id, so the wire carries the runtime's own
  child span id. A `traceparent` the caller set reaches the wire unchanged only when the call is untraced or the transport
  is used directly; on a traced pipeline call the stamp replaces it. For a caller-supplied client whose handler chain does
  not propagate (no `SocketsHttpHandler` at its root, or an `ActivityHeadersPropagator` that injects nothing), a traced call
  then sends no `traceparent`; the remedies are to leave runtime propagation on, or not to listen to `System.Net.Http`.
- **Metrics.** Enable the `Dexpace.Sdk` meter **or** `System.Net.Http`'s for HTTP client duration, not both: each attempt is
  reported under `http.client.request.duration` by both.
- **Two known gaps.** `SocketsHttpHandler` rejects a response with a malformed header *name* outright, before this adapter sees it
  (`TRANSPORT-14`'s name clause), and it re-sends a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` or `TRACE` whose connection ends
  before any response byte (`TRANSPORT-2`'s native retry; a body-less `POST`, `PUT` and `PATCH` are sent once). The
  [transport page](https://github.com/dexpace/dotnet-sdk/blob/main/docs/sdk-documentation/transport.md) says why.

## Usage

```csharp
using System.Net.Http;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Http.SystemNet;

// The simplest form: the transport owns a correctly configured client.
await using var transport = new SystemNetHttpClient();

// Bring your own handler (proxy, mTLS, DelegatingHandlers) with redirects left to the SDK; you keep ownership of it.
using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
await using var viaHandler = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Timeout = TimeSpan.FromSeconds(30) });

using var response = await transport.ExecuteAsync(Request.Get("https://api.example.com/health"));
Console.WriteLine($"{response.Status.Code}: {await response.Body.ReadAsStringAsync()}");
```

`ExecuteAsync(request, options, token)` is the interface member; the two-argument form `ExecuteAsync(request, token)` is
the option-less extension in `Dexpace.Sdk.Core.Client`, which passes `RequestOptions.Empty`. Pass the transport to
`DexpacePipeline.CreateDefault` to get retry, redirect, auth and instrumentation on top of it.

## Links

- Repository and design: <https://github.com/dexpace/dotnet-sdk>
- License: MIT
