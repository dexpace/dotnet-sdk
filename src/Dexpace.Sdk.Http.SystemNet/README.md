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
