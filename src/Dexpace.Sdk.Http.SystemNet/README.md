# Dexpace.Sdk.Http.SystemNet

The reference transport for the dexpace .NET SDK. It adapts `System.Net.Http.HttpClient` to the
[`Dexpace.Sdk.Core`](https://www.nuget.org/packages/Dexpace.Sdk.Core) transport SPI, implementing both
`IAsyncHttpClient` and `IHttpClient`.

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`.

## Behaviour

- **Streaming.** Response bodies are delivered as live streams (`HttpCompletionOption.ResponseHeadersRead`),
  never buffered. Dispose the `Response` to release the connection.
- **Ownership.** A caller-supplied `HttpClient` is never disposed by the adapter; one the adapter created is.
- **Redirects.** The SDK pipeline's `RedirectPolicy` is the only redirect authority. The adapter's own client
  never follows a redirect. A caller-supplied client must set `AllowAutoRedirect = false` on its handler: if it
  follows one anyway, the call fails with an `SdkException` naming `AllowAutoRedirect`.
- **Headers.** `Host` and the framing headers (`Content-Length`, `Transfer-Encoding`, `Connection`, …) are
  dropped from the request so `HttpClient` computes them, and every other header is re-validated at the wire.
- **Errors.** Transport faults are mapped onto the SDK's `SdkException` hierarchy.
- **Trace context.** The pipeline stamps the attempt span's `traceparent` (and `tracestate`) on the request. When a
  `System.Net.Http` listener exists and runtime propagation is on (`System.Net.Http.EnableActivityPropagation`), the
  adapter drops that stamp, recognised by equality with `Activity.Current`'s id, so the wire carries the runtime's own
  child span id; a `traceparent` the caller set is sent unchanged. For a caller-supplied client whose handler chain does
  not propagate (no `SocketsHttpHandler` at its root, or an `ActivityHeadersPropagator` that injects nothing), a traced call
  then sends no `traceparent`; the remedies are to leave runtime propagation on, or not to listen to `System.Net.Http`.
- **Metrics.** Enable the `Dexpace.Sdk` meter **or** `System.Net.Http`'s for HTTP client duration, not both: each attempt is
  reported under `http.client.request.duration` by both.
- **Logging.** The constructors that take an `ILogger` log dropped headers by name, never by value.

## Usage

```csharp
using System.Net.Http;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Http.SystemNet;

// Bring your own HttpClient (proxy, mTLS, DelegatingHandlers), with redirects left to the SDK.
using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
await using var transport = new SystemNetHttpClient(client);

using var response = await transport.ExecuteAsync(Request.Get("https://api.example.com/health"));
Console.WriteLine($"{response.Status.Code}: {await response.Body.ReadAsStringAsync()}");
```

`ExecuteAsync(request, options, token)` is the interface member; the two-argument form `ExecuteAsync(request, token)` is
the option-less extension in `Dexpace.Sdk.Core.Client`, which passes `RequestOptions.Empty`. `new SystemNetHttpClient()` creates and owns a correctly configured client. Pass the transport to
`DexpacePipeline.CreateDefault` to get retry, redirect, auth and instrumentation on top of it.

## Links

- Repository and design: <https://github.com/dexpace/dotnet-sdk>
- License: MIT
