# Architecture

> **Superseded (2026-09-29).** This is the original foundation-slice note, kept for its idiom table. The
> architecture is now owned by [design chapter 1](sdk-design-dotnet/01-overview.md) and the rest of
> [`sdk-design-dotnet/`](sdk-design-dotnet.md); roadmap phase 12 replaces this file with the as-built
> `docs/sdk-documentation/architecture.md`. The text below is not maintained, and its "Planned" list is stale:
> the pipeline and its policies, auth (credentials, token cache, auth policies) and pagination shipped in
> PRs #6, #8 and #9. What is genuinely unbuilt is scheduled by the
> [v1 roadmap](work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md).

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
   first send when retries are needed. Each async member also has a synchronous twin (`WriteTo`,
   `ToReplayable`, `OpenRead`, `ReadAsBytes`, `ReadAsString`), added by phase 3a. Phase 3b adds the form, file and
   multipart request bodies, makes a seekable stream with a known length replayable, latches `Dispose` on `Response` and
   `ResponseBody`, and strips a matching byte-order mark when decoding text.
2. **HTTP value models** (`Http/Common`) — immutable `Method`, `Protocol`, `MediaType`,
   `HttpHeaderName`, `Headers`, plus `Status` in `Http/Response`. `Headers` is a case-insensitive
   multimap with non-destructive `With` / `Set` / `Without` and a `Builder` for batched edits.
3. **Request / Response** (`Http/Request`, `Http/Response`) — `Request` is an immutable `record`
   (method, absolute `Uri`, `Headers`, optional `RequestBody`); `Response` is a disposable carrier
   of `Status`, `Headers`, `ResponseBody`, and the negotiated `Protocol`.
4. **Transport SPI** (`Client`) — `IAsyncHttpClient.ExecuteAsync(Request, RequestOptions, CancellationToken)` is the
   async-first seam; `IHttpClient.Execute(Request, RequestOptions, CancellationToken)` is the synchronous variant.
   Neither declares parameter defaults; `HttpClientExtensions.ExecuteAsync` / `Execute` are the option-less calls that
   pass `RequestOptions.Empty`. The same class bridges between the two seams: `AsAsync(TaskScheduler)` runs each
   blocking call on the caller's scheduler, and `AsBlocking` blocks on the async call as a last resort; neither
   disposes what it wraps. `core` ships no transport.
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
