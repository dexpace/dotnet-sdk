# Architecture

The dexpace .NET SDK is an **HTTP-client toolkit, not an HTTP client**. `Dexpace.Sdk.Core`
provides abstractions, models, and (over time) pipelines; consuming libraries plug in a concrete
transport via the `IHttpClient` / `IAsyncHttpClient` interfaces. This mirrors the `dexpace/java-sdk`
and `dexpace/python-sdk` ports, translated into .NET idioms.

## Idiom mapping

| Concept            | Java (Kotlin)                  | Python                         | .NET                                       |
|--------------------|--------------------------------|--------------------------------|--------------------------------------------|
| Immutable model    | `data class` + `Builder`       | `@dataclass(frozen, slots)`    | `record` / `readonly record struct` + `with` |
| SPI seam           | `fun interface`                | `typing.Protocol`              | `interface`                                |
| Resource cleanup   | `AutoCloseable`                | context manager (`__enter__`)  | `IDisposable` / `IAsyncDisposable`         |
| Async contract     | `CompletableFuture`            | `async`/`await` coroutine      | `Task<T>`                                  |
| Body streaming     | Okio `Source`/`Sink`           | `iter_bytes` / `BinaryIO`      | `Stream` + `WriteToAsync`/`OpenReadAsync`  |
| Single source of truth for deps | `libs.versions.toml`  | `pyproject.toml` / `uv.lock`   | `Directory.Packages.props`                 |

The pluggable I/O seam that exists in the Java SDK (`IoProvider` over Okio) is **not** ported:
.NET's `System.IO.Stream`, `Memory<byte>`, and `IAsyncDisposable` cover the same surface natively,
exactly as the Python port leans on `bytes` / `BinaryIO` instead of an Okio analog.

## Layers (bottom-up)

1. **Bodies** — `RequestBody` / `ResponseBody` are typed abstractions over outgoing and incoming
   payloads. `RequestBody.WriteToAsync(Stream)` is the primary streaming surface;
   `ResponseBody.OpenReadAsync()` / `ReadAsBytesAsync()` / `ReadAsStringAsync()` drain the response.
   Byte- and string-backed bodies are replayable; stream-backed bodies are single-use and throw
   `StreamConsumedException` on a second pass. Call `RequestBody.ToReplayableAsync()` before the
   first send when retries are needed.
2. **HTTP value models** (`Http/Common`) — immutable `Method`, `Protocol`, `MediaType`,
   `HttpHeaderName`, `Headers`, plus `Status` in `Http/Response`. `Headers` is a case-insensitive
   multimap with non-destructive `With` / `Set` / `Without` and a `Builder` for batched edits.
3. **Request / Response** (`Http/Request`, `Http/Response`) — `Request` is an immutable `record`
   (method, absolute `Uri`, `Headers`, optional `RequestBody`); `Response` is a disposable carrier
   of `Status`, `Headers`, `ResponseBody`, and the negotiated `Protocol`.
4. **Transport SPI** (`Client`) — `IAsyncHttpClient.ExecuteAsync(Request, CancellationToken)` is the
   async-first seam; `IHttpClient.Execute(Request)` is the synchronous variant.
   `HttpClientExtensions` bridges between them (`AsAsync`, `AsBlocking`). `core` ships no transport.
5. **Errors** (`Errors`) — `SdkException` roots a hierarchy distinguishing the three transport
   failure shapes (`ServiceRequestException`, `ServiceResponseException`, `HttpResponseException`)
   from body/stream lifecycle, serialization, and pipeline failures.

## Transports

`Dexpace.Sdk.Http.SystemNet` adapts `System.Net.Http.HttpClient` to the SPI. It streams response
bodies (`HttpCompletionOption.ResponseHeadersRead`) rather than buffering, translates transport
faults into the SDK exception hierarchy, and is ownership-aware: a caller-supplied `HttpClient` is
never disposed by the adapter. Additional transports (e.g. a gRPC-web or socket-level transport)
would each adapt one library to the same interfaces.

## Planned (not yet implemented)

Mirroring the Java/Python ports, the following land in later slices:

- **Pipeline** — staged policies (redirect, retry, idempotency, set-date, client-identity, logging,
  tracing) composed over the transport.
- **Context chain** — dispatch → request → exchange promotion carrying an instrumentation context.
- **Auth** — token credentials, bearer/basic policies, RFC 7235 challenge handling.
- **SSE, pagination, webhooks, instrumentation** — server-sent events, paged iteration, webhook
  signature verification, and tracing/metrics abstractions.
