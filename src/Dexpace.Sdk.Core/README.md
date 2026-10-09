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
| `Dexpace.Sdk.Core.Client` | The transport SPI: `IAsyncHttpClient`, `IHttpClient`, the `AsAsync` / `AsBlocking` bridges, and `DelegateHttpClient` |
| `Dexpace.Sdk.Core.Pipeline` | `HttpPipeline` (itself an `IAsyncHttpClient` / `IHttpClient`), `PipelineBuilder` (`Flatten` / `Nest`, `AddStandardResilience`), `HttpPipelinePolicy` (request in, response out; `ProcessAsync` / `Process`), `PipelineContext`, `DexpacePipeline.CreateDefault` / `CreateEmpty`, and the policies: operation timeout, redirect, idempotency key, client identity, retry, `Date`, auth, instrumentation, error mapping |
| `Dexpace.Sdk.Core.Auth` | `AuthScheme`, `AuthRequirement`, `AuthDescriptor`, `AuthResolver` (per-call, operation and client tiers), `TokenCredential`, `AccessToken`, `AccessTokenCache` (30 s margin, background refresh, bounded), `ApiKeyCredential`, `BasicCredential`, `DigestCredential`, `AuthCredentials`, `AuthenticationChallenge` (lenient RFC 7235 parser), `IChallengeHandler` with `BasicChallengeHandler`, `DigestChallengeHandler` (RFC 7616) and `CompositeChallengeHandler` |
| `Dexpace.Sdk.Core.Pagination` | `AsyncPageable<T>`, `Pageable<T>`, `Page<T>`, `PageInfo<T>`, `IPageStrategy<TPage,T>`, `Pageable` (`Create`, `CreateBlocking`, `FromFetchers`), `PaginationStrategies`, `PagingOptions`, `FetchedPage<T>` (see `docs/sdk-documentation/pagination.md`) |
| `Dexpace.Sdk.Core.ServerSentEvents` | `ServerSentEvent`, `ServerSentEventReader` (`ReadNext{,Async}`, `ReadAll{,Async}`), `ServerSentEventStream` (`FromResponse` owns the response; `await foreach`, `AsEnumerable`, `MapAsync` / `Map`), `SseMapResult`, and `ServerSentEventLineTooLongException` for the 1 MiB line cap; no reconnecting client (see `docs/sdk-documentation/sse.md`) |
| `Dexpace.Sdk.Core.Serialization` | The `ISerde` seam; concrete codecs live in their own packages |
| `Dexpace.Sdk.Core.Configuration` | The options as sealed records (`DexpaceClientOptions`, `RetryOptions`, `RedirectOptions`, `HttpLoggingOptions`), `HttpLogLevel` (logging is off by default), `ProxyOptions` with `FromEnvironment`, `TimeProviderWaits` and `BuildInfo` |
| `Dexpace.Sdk.Core.Diagnostics` | The `Dexpace.Sdk` `ActivitySource` and `Meter`, the default-deny `UrlRedactor` (including `RedactHeaderValue`), and the stable log vocabulary `DexpaceLogEvents` / `DexpaceLogKeys`; the operation span, attempt spans, span events and the two HTTP client instruments are emitted through them (see `docs/sdk-documentation/tracing-and-metrics.md`) |
| `Dexpace.Sdk.Core.Recovery` | The recovery layer: `Outcome`, `IRequestStep` / `IResponseStep` / `IRecoveryStep`, `RequestRecoveryChain`, `ResponseRecoveryChain`, `RecoveryDispatcher`, and the shipped steps `ErrorMappingStep`, `IdempotencyKeyStep` and `ClientIdentityStep` |
| `Dexpace.Sdk.Core.Errors` | `SdkException` and its subclasses, `ExceptionFacts` (`IsFatal`, `EnumerateCauses`) and `ExceptionTrail` (`AddSuppressed`, `GetSuppressed`) |

Its one runtime dependency is `Microsoft.Extensions.Logging.Abstractions`, for `ILogger`.

## Usage

Send a request through the default pipeline over a transport:

```csharp
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

var options = new DexpaceClientOptions { OverallTimeout = TimeSpan.FromSeconds(30) };
using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/health"), options, CancellationToken.None);

// Options are immutable records: derive a copy for one call with `with`. A call is retried twice by default
// (three sends); see docs/sdk-documentation/retry.md.
var patient = options with { Retry = options.Retry with { MaxRetryAttempts = 5 } };

await response.EnsureSuccessAsync(); // throws HttpResponseException for a 4xx or 5xx
Console.WriteLine(await response.Body.ReadAsStringAsync());
```

Dispose every `Response`: a streamed body holds its connection until then. A stream-backed body is single-use;
call `RequestBody.ToReplayableAsync()` before the first send if the request may be retried.

Every async body member has a synchronous twin (`RequestBody.WriteTo` / `ToReplayable`, `ResponseBody.OpenRead` /
`ReadAsBytes` / `ReadAsString`); the base members of the two open classes throw `NotSupportedException`, and every body
the SDK creates overrides them. `RequestBody.FromStream` with a known length writes exactly that many bytes, and
`ReadAsBytesAsync` / `ReadAsStringAsync` refuse a body above `ResponseBody.DefaultMaxMaterializedBytes` (64 MiB) with
`BodyTooLargeException`; stream a larger body through `OpenReadAsync`.

`RequestBody` also has `FromForm` (WHATWG form encoding), `FromFile` (a byte range of a file, a fresh handle per write) and
`Multipart`; `FromStream` over a seekable stream with a known length is replayable. `Response` and `ResponseBody` dispose at
most once, the readers dispose the body, and a leading byte-order mark matching the charset is stripped from text.

## Server-sent events

`ServerSentEventStream.FromResponse` takes ownership of a streaming response (from the call, even when it throws) and releases it exactly
once however you stop. Set `Accept: text/event-stream` on the request yourself; core adds no header and ships no reconnecting client.

```csharp
await using var events = ServerSentEventStream.FromResponse(await pipeline.SendAsync(request, ct));
await foreach (var chunk in events.MapAsync<Chunk>((name, data) =>
    data == "[DONE]" ? SseMapResult.Done : SseMapResult.Value(Parse(data))))   // the sentinel is yours
{
    Handle(chunk);
}
```

A line over `maxLineBytes` (1 MiB by default) throws `ServerSentEventLineTooLongException`. See `docs/sdk-documentation/sse.md` in the repository.

## Execution context

`Dexpace.Sdk.Core.Execution` holds the per-call context chain: `DispatchContext`, promoted one way to `RequestContext` and
`ExchangeContext` (`PromoteToRequest`, `PromoteToExchange`), a `CallKey` shared by every link, the `InstrumentationContext`
correlation bundle, and `DexpaceCallContexts.TryGet` over a bounded registry. Call `Close()` on the furthest link when the
call ends.
A call can also run through the recovery layer: a `RecoveryDispatcher` applies a `RequestRecoveryChain`, sends over any
transport, folds the `Outcome` through a `ResponseRecoveryChain` (response steps, then recovery steps; a throwing step becomes a
failure, never an escape), and rethrows the terminal failure as the same exception instance. Every step contract has a sync and an
async form. A failure while closing a response lands on the primary exception's trail, readable with
`ExceptionTrail.GetSuppressed`. See `docs/sdk-documentation/recovery.md` in the repository.

## Authentication

Every auth policy derives from `AuthorizationPolicy`, which owns the HTTPS guard, the cross-origin withholding and the single
`401` replay. Pick the policy for the scheme; `MultiSchemeAuthPolicy` serves several at once, chosen per call by
`RequestOptions.Auth` / `OperationAuth` over the policy's client `AuthDescriptor`.

```csharp
// OAuth 2.0 bearer: the cache refreshes 30 s early in the background and retries once on a Bearer 401.
var bearer = new BearerTokenAuthPolicy(tokenCredential, "files.read");

// Digest (RFC 7616) with a Basic fallback, answered on the server's challenge.
var challenge = new ChallengeAuthPolicy(new CompositeChallengeHandler(
    new DigestChallengeHandler(new DigestCredential(user, password)),
    new BasicChallengeHandler(new BasicCredential(user, password))));

// One call anonymous, whatever the policy.
using var response = await pipeline.SendAsync(
    request, new RequestOptions { Auth = new AuthDescriptor(AuthRequirement.NoAuth) }, ct);
```

Credentials redact their secrets in `ToString`. See `docs/sdk-documentation/auth.md` in the repository.

## Links

- Repository and design: <https://github.com/dexpace/dotnet-sdk>
- Security reports: <https://github.com/dexpace/dotnet-sdk/blob/main/SECURITY.md>
- License: MIT
