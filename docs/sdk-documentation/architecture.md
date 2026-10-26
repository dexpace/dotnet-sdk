# Architecture

**Start here. As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). This tree documents the
code that exists, package by package and seam by seam. This page is the front door for the other pages, and it replaces `docs/architecture.md`,
the foundation-slice note whose banner promised this file. The requirement IDs it cites are the normative text
(`docs/product-spec/`); the decisions behind the shape are in [design §1 to §3](../sdk-design-dotnet/01-overview.md); neither is restated here.

Node's counterpart (`nodejs-sdk/docs/sdk-documentation/architecture.md`) and Ruby's were read for the layout of the page; this page is written from this
repository's source.

| Page | Covers |
|---|---|
| [`getting-started.md`](./getting-started.md) | Install, build a client with and without dependency injection, a typed call, errors, disposal |
| [`example-end-to-end.md`](./example-end-to-end.md) | One worked slice: an operation, a response handler, bearer auth, pagination, retry, redirect, the DI registration, logging |
| [`http.md`](./http.md) | The domain model: `Request`, `Response`, `Headers`, `Status`, `Query`, `RequestOptions`, `ETag`, `HttpRange` |
| [`seams.md`](./seams.md) | The transport seam, the codec seam and the operation-input projection |
| [`io.md`](./io.md), [`bodies.md`](./bodies.md) | The stream helpers and the request and response bodies |
| [`execution-context.md`](./execution-context.md), [`recovery.md`](./recovery.md) | The per-call context chain and the recovery layer |
| [`pipelines.md`](./pipelines.md) | Stages, policies, `PipelineContext`, the builder, `HttpPipeline` as a transport |
| [`configuration.md`](./configuration.md) | The options records, `ProxyOptions`, `TimeProviderWaits`, `HttpDate`, `BuildInfo` |
| [`logging-and-redaction.md`](./logging-and-redaction.md), [`tracing-and-metrics.md`](./tracing-and-metrics.md) | The `http.request` / `http.response` events and their redaction; the spans and the two instruments |
| [`retry.md`](./retry.md), [`redirect.md`](./redirect.md), [`auth.md`](./auth.md) | The three SDK-domain loops and the credential lifecycle |
| [`serde.md`](./serde.md), [`sse.md`](./sse.md), [`pagination.md`](./pagination.md) | Typed bodies and `Tristate<T>`, server-sent events, paged iteration |
| [`transport.md`](./transport.md), [`dependency-injection.md`](./dependency-injection.md) | The reference transport and the host integration |
| [`conformance.md`](./conformance.md), [`cross-cutting-invariants.md`](./cross-cutting-invariants.md) | The conformance kit and its four suites; the invariants that cut across the layers |
| `quality-gates.md`, `versioning.md` | Every blocking gate and how to run it; what a version number promises |
| `write-a-transport.md`, `write-a-serde.md`, `write-a-response-handler.md`, `write-a-paging-strategy.md` | Implementing a seam, and proving the implementation |

## What this tree is not

It is not the API reference. Every public member of every package is listed in the package's `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt`, which the build checks against the code (`RS0016`, `RS0017`); what a member *means* is in its `///` comment, which the
build requires (`CS1591`) and which ships in the package's XML file. Both are generated or gate-verified. A third hand-written copy would drift, and
the vendored styleguide's rule is one authoritative place per fact.

What those two cannot express is what this tree holds: **how the packages compose, which one to install for which job, and worked examples that cross a
package boundary.** It is also not the specification. `docs/product-spec/` is normative and numbered; every `HTTP-N`, `SEAM-N`, `RETRY-N` and `PAGE-N`
identifier here is an entry there. Where this port deliberately differs, [`docs/sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md`](../sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md)
is the ledger and [`docs/deviations.md`](../deviations.md) the as-built audit of it.

## The shape of the thing

This is a toolkit for building HTTP client libraries, not an HTTP client. `Dexpace.Sdk.Core` defines the models, the pipeline and the seams; it never opens a
socket and never picks a codec. The networking arrives through a transport package you choose, the wire format through a codec package.

```
   your library (generated or hand-written)
        │
        ▼
   HttpPipeline  ─── policies ordered by PipelineStage, itself a transport (IAsyncHttpClient and IHttpClient)
        │
        │  Operation   100   overall deadline                          ┐ outside every loop
        │  PerCall     150   idempotency key, client identity (+ error mapping, opt-in) ┘
        │  Redirect    200   the redirect loop
        │  PerHop      250
        │  Retry       300   the retry loop
        │  PerAttempt  400   Date
        │  Auth        500   one auth policy
        │  Diagnostics 600   span, metrics, http.request / http.response
        │  Serde       700   reserved
        ▼
   IAsyncHttpClient / IHttpClient     ── Dexpace.Sdk.Http.SystemNet │ yours
        │
        ▼
      wire
```

An `HttpPipeline` implements both transport interfaces, so a pipeline is substitutable wherever a transport is, which is what makes `PipelineBuilder.Nest`
and `Flatten` work at all (`PIPE-26`). The stage numbers are sparse on purpose and the table is [`pipelines.md`](./pipelines.md)'s, not restated here.

## The six layers

Read from the bottom, as a request is built:

1. **Bodies** (`Dexpace.Sdk.Core.Http.Request` / `.Response`). `RequestBody.WriteToAsync(Stream)` is the outgoing streaming surface; `ResponseBody.OpenReadAsync`,
   `ReadAsBytesAsync` and `ReadAsStringAsync` drain the incoming side. Bytes and string bodies are replayable; a stream body is single-use and throws
   `StreamConsumedException` on a second *write*. A stream-backed *response* body is spent by its first convenience read, which disposes it. Call
   `RequestBody.ToReplayableAsync()` before the first send when retries are in play. Every async member has a synchronous twin. See [`bodies.md`](./bodies.md) and [`io.md`](./io.md).
2. **HTTP value models** (`Dexpace.Sdk.Core.Http.Common`, `Status`). Immutable and case-insensitive: `Headers` is a validated multimap (CR, LF and other controls throw
   `ArgumentException` at construction; received headers take the lenient `Headers.Builder.AddInbound` path), `MediaType` round-trips quoted parameters, and `Method`,
   `Protocol` and `Status` are value types with well-known instances. See [`http.md`](./http.md).
3. **Request and response** (`Request`, `Response`). `Request` is an immutable `record` over an absolute `Uri`; mutate it with `with` or the `With*` helpers. `Response` is a
   disposable carrier of `Status`, `Headers`, `Body` and `Protocol`. Dispose every response: a streamed body holds its connection until then.
4. **The transport SPI** (`Dexpace.Sdk.Core.Client`). `IAsyncHttpClient.ExecuteAsync(Request, RequestOptions, CancellationToken)` is the async-first seam and
   `IHttpClient.Execute(...)` the synchronous one, bridged by `AsAsync(TaskScheduler)` and `AsBlocking`. See [`seams.md`](./seams.md).
5. **The pipeline** (`Dexpace.Sdk.Core.Pipeline`). `DexpacePipeline.CreateDefault(transport)` assembles the overall deadline, idempotency key, client identity, `Date`, redirect, retry and
   instrumentation over a transport, plus an auth policy if you pass one; error mapping is opt-in (a response of any status is returned untouched). Pagination, server-sent events and
   the codec sit on top of it. See [`pipelines.md`](./pipelines.md).
6. **Errors** (`Dexpace.Sdk.Core.Errors`). `SdkException` roots the hierarchy, with one exception: a transport failure, `ServiceRequestException`, derives from `IOException`,
   so `catch (SdkException)` does not catch it and `catch (IOException)` does; it keeps `IRetryableError`, which the retry classifier reads. `HttpResponseException` is a 4xx or
   5xx received intact. There is no `ServiceResponseException`. The reasoning is in [`docs/deviations.md`](../deviations.md).

## The five packages

All five target `net10.0` and are versioned together (`versioning.md`).

| Package | Provides | Third-party dependencies |
|---|---|---|
| `Dexpace.Sdk.Core` | The models, bodies, transport SPI, pipeline and policies, auth, pagination, server-sent events, configuration, diagnostics, errors and the `ISerde` seam | `Microsoft.Extensions.Logging.Abstractions` (for `ILogger`; a recorded deviation, design §10) |
| `Dexpace.Sdk.Http.SystemNet` | `SystemNetHttpClient`, the reference transport over `System.Net.Http.HttpClient` ([`transport.md`](./transport.md)) | none |
| `Dexpace.Sdk.Serialization.SystemTextJson` | `SystemTextJsonSerde`, the `ISerde` over source-generated `System.Text.Json`, with `Tristate<T>` wired ([`serde.md`](./serde.md)) | none |
| `Dexpace.Sdk.Extensions.DependencyInjection` | `AddDexpaceClient`, the named client over `IHttpClientFactory` ([`dependency-injection.md`](./dependency-injection.md)) | `Microsoft.Extensions.Http` (one publisher's lock-stepped family counts as one) |
| `Dexpace.Sdk.Conformance` | The conformance kit: the transport, invariant, codec and packaging suites ([`conformance.md`](./conformance.md)) | none |

```
Dexpace.Sdk.Core ◄── Dexpace.Sdk.Http.SystemNet ◄── Dexpace.Sdk.Extensions.DependencyInjection
       ▲
       ├────────── Dexpace.Sdk.Serialization.SystemTextJson
       └────────── Dexpace.Sdk.Conformance
```

### Two rules the layout enforces mechanically

**Core's dependencies are exact** (`SEAM-1`, `NFR-1`; roadmap constraint 2, design §2.4). `Dexpace.Sdk.Core` may reference only the `Microsoft.NETCore.App` reference pack of its
target framework, the logging abstractions band-matched to it, and build-only packages marked `PrivateAssets="all"`. An adapter takes core plus at most one third-party library
(`NFR-2`). `scripts/ci/dependency-audit.cs` enforces it on every pack from one policy record per library, and `packaging-conformance.cs` re-reads the packed `.nuspec` files.

**Core never references a transport or a codec** (`SEAM-2`). `Dexpace.Sdk.Core.Tests` references core, the core-only conformance kit and in-memory fakes, and an architecture test pins it;
so the claim "swap any one concern independently" is a build property and not a promise.

## The seams

A seam is an interface core defines and does not implement. Four matter, and each is small enough to quote:

```csharp
public interface IAsyncHttpClient : IAsyncDisposable
{
    Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
}

public interface ISerde
{
    MediaType DefaultMediaType { get; }
    ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default);
    ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default);
    void Serialize<T>(IBufferWriter<byte> destination, T value);
    T? Deserialize<T>(ReadOnlySpan<byte> utf8);
    T? Deserialize<T>(Stream source);   // a default interface member
}

public interface IResponseHandler<T>
{
    ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken);
}

public interface IPageStrategy<in TPage, T>
{
    PageInfo<T> Parse(TPage page, Response response, Request first);
}
```

`IHttpClient` is the synchronous twin of the first. There is no registration step and no discovery mechanism: a conforming object is a valid implementation, and you pass it in
(the DI package registers named clients as keyed services, which is host integration and not a plugin registry). `IResponseHandler<T>` is what a generated SDK implements to turn a
response into a value, and it *owns* the response it is given. Each seam has a guide that implements it and runs the conformance suite where one exists:

| Seam | Guide | Proven by |
|---|---|---|
| `IAsyncHttpClient` / `IHttpClient` | `write-a-transport.md` | `TransportSuite` |
| `ISerde` | `write-a-serde.md` | `CodecSuite` |
| `IResponseHandler<T>` | `write-a-response-handler.md` | ordinary tests |
| `IPageStrategy<TPage, T>` | `write-a-paging-strategy.md` | ordinary tests |

## A call, end to end

This program is complete: the documentation build compiles it (`dotnet run --project .claude/skills/housekeeping/src -- fences`, `quality-gates.md`).

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(AppJsonContext.Default));

await using var transport = new SystemNetHttpClient();   // yours to dispose: the pipeline never owns the transport
var apiKey = new ApiKeyAuthPolicy(new ApiKeyCredential("secret", HttpHeaderName.Of("X-Api-Key")));
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport, apiKey);

var options = new DexpaceClientOptions { OverallTimeout = TimeSpan.FromSeconds(30) };
using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/v1/things/7"), options, CancellationToken.None);

await response.EnsureSuccessAsync();                      // a 4xx or 5xx throws HttpResponseException
Thing thing = await response.Body.ReadValueAsync<Thing>(serde);
Console.WriteLine(thing.Name);

internal sealed record Thing(string Name, int Size);

[JsonSerializable(typeof(Thing))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

Reading that stack outward from the wire:

1. **Transport.** `SystemNetHttpClient` is one `IAsyncHttpClient`. It streams the response body, maps native faults onto the SDK's exceptions, and never follows a redirect:
   the SDK is the only redirect authority.
2. **Pipeline.** `DexpacePipeline.CreateDefault` puts the SDK-domain concerns between your code and the transport, and the auth policy at `PipelineStage.Auth`.
   A pillar stage admits one policy, so the builder refuses a second auth policy.
3. **Options.** `DexpaceClientOptions` and its siblings are sealed records; derive a copy for one call with `with`. Logging is off until you raise `HttpLoggingOptions.Level`.
4. **Codec.** `ISerde` is the one place a wire format lives. `ReadValueAsync<T>` streams, disposes the body on every path, and rejects a wire `null` for a reference type.
5. **Cleanup.** `Response`, `ResponseBody` and transports are `IDisposable` / `IAsyncDisposable`, and disposal is latched: each releases at most once.

## Where the .NET idioms replace the reference's

| Concept | Reference (Java, Kotlin) | .NET |
|---|---|---|
| Immutable model | `data class` and a `Builder` | `record` / `readonly record struct`, `with` and `With*` helpers; `Headers.Builder` is the one builder |
| SPI seam | `fun interface` | `interface` |
| Resource cleanup | `AutoCloseable` | `IDisposable` / `IAsyncDisposable` |
| Async contract | `CompletableFuture` | `Task<T>` (`ValueTask<T>` inside the pipeline) |
| Body streaming | Okio `Source` / `Sink` | `Stream`, `Memory<byte>` and `IBufferWriter<byte>` |
| Dependency versions | `libs.versions.toml` | `Directory.Packages.props`, central package management |

The pluggable I/O seam that exists in the Java SDK (`IoProvider` over Okio) is **not** ported: `System.IO.Stream`, `Memory<byte>` and `IAsyncDisposable` cover the same surface natively (design §3.1).

## Runtime floor

`net10.0` only, set once in `Directory.Build.props` (roadmap decision D1). Every library is `IsTrimmable` and `IsAotCompatible`, and a NativeAOT smoke consumer is published and run in CI
(`tests/Dexpace.Sdk.AotSmoke`). The codec resolves type metadata from a source-generated `JsonSerializerContext` for that reason; there is no reflection fallback.
