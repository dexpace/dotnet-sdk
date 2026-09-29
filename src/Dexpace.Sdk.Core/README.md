# Dexpace.Sdk.Core

The core of the dexpace .NET SDK: an **HTTP-client toolkit, not an HTTP client**. It holds the immutable
HTTP models, the request and response bodies, the transport SPI, the exception hierarchy, and the pipeline
that SDK-domain concerns run in. It ships no transport; pair it with one, such as
[`Dexpace.Sdk.Http.SystemNet`](https://www.nuget.org/packages/Dexpace.Sdk.Http.SystemNet).

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`.

## What is in the box

| Namespace | Contents |
|---|---|
| `Dexpace.Sdk.Core.Http.*` | `Method`, `Protocol`, `MediaType`, `HttpHeaderName`, `Headers`, `Request`, `RequestBody`, `Response`, `ResponseBody`, `Status` |
| `Dexpace.Sdk.Core.Client` | The transport SPI: `IAsyncHttpClient`, `IHttpClient`, and the `AsAsync` / `AsBlocking` bridges |
| `Dexpace.Sdk.Core.Pipeline` | `HttpPipeline`, `PipelineBuilder`, `HttpPipelinePolicy`, `DexpacePipeline.CreateDefault`, and the policies: operation timeout, redirect, idempotency key, client identity, retry, `Date`, auth, instrumentation |
| `Dexpace.Sdk.Core.Auth` | `TokenCredential`, `AccessTokenCache`, `ApiKeyCredential`, `BasicCredential` |
| `Dexpace.Sdk.Core.Pagination` | `AsyncPageable<T>`, `Page<T>`, `Pageable.Create`, `PaginationStrategies` |
| `Dexpace.Sdk.Core.Serialization` | The `ISerde` seam; concrete codecs live in their own packages |
| `Dexpace.Sdk.Core.Configuration` | `DexpaceClientOptions`, `RetryOptions`, `RedirectOptions` |
| `Dexpace.Sdk.Core.Diagnostics` | The `Dexpace.Sdk` `ActivitySource` and `Meter`, and the default-deny `UrlRedactor` |
| `Dexpace.Sdk.Core.Errors` | `SdkException` and its subclasses |

Its one runtime dependency is `Microsoft.Extensions.Logging.Abstractions`, for `ILogger`.

## Usage

Send a request through the default pipeline over a transport:

```csharp
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();
HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

var options = new DexpaceClientOptions { OverallTimeout = TimeSpan.FromSeconds(30) };
using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/health"), options);

await response.EnsureSuccessAsync(); // throws HttpResponseException for a 4xx or 5xx
Console.WriteLine(await response.Body.ReadAsStringAsync());
```

Dispose every `Response`: a streamed body holds its connection until then. A stream-backed body is single-use;
call `RequestBody.ToReplayableAsync()` before the first send if the request may be retried.

## Links

- Repository and design: <https://github.com/dexpace/dotnet-sdk>
- Security reports: <https://github.com/dexpace/dotnet-sdk/blob/main/SECURITY.md>
- License: MIT
