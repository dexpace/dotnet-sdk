<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/dexpace-wordmark-dark.svg">
    <img alt="dexpace" src="docs/assets/dexpace-wordmark-light.svg" width="320">
  </picture>
</p>

<h1 align="center">Dexpace .NET SDK</h1>

[![CI](https://github.com/dexpace/dotnet-sdk/actions/workflows/ci.yml/badge.svg)](https://github.com/dexpace/dotnet-sdk/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![NativeAOT: compatible](https://img.shields.io/badge/NativeAOT-compatible-blue.svg)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![Analyzers: warnings as errors](https://img.shields.io/badge/analyzers-warnings%20as%20errors-blue.svg)](./docs/sdk-documentation/quality-gates.md)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

A toolkit for building .NET HTTP client libraries. It provides immutable, validated request and response
models, a staged pipeline that runs on a genuine asynchronous and a genuine synchronous path, pluggable
transports and codecs, and the correctness-sensitive plumbing every service client needs: idempotency-aware
retry, redirects that never leak a credential cross-origin, RFC 7235/7616 authentication, pagination,
server-sent events and three-state PATCH. Every library is trimmable and NativeAOT-compatible, and every
public member is documented and locked by a public-API file.

The SDK is deliberately **not an HTTP client**, and it does not compete with `HttpClient` or Refit on the
easiest way to fetch a JSON endpoint. It is for authors of generated or hand-written service-client SDKs. It
defines the contracts — a transport, a codec, a response handler, a paging strategy — and supplies the models,
pipeline policies and observability hooks that surround them; the networking arrives through a transport
package of your choosing. A transport is any `IAsyncHttpClient` (and, for the blocking path, `IHttpClient`).

It is the .NET sibling of the [Java](https://github.com/dexpace/java-sdk),
[Python](https://github.com/dexpace/python-sdk), [Node.js](https://github.com/dexpace/nodejs-sdk) and
[Ruby](https://github.com/dexpace/ruby-sdk) SDKs, built to the same product specification, with .NET idioms:
`record` and `with` instead of builders, interfaces for the seams, `IAsyncDisposable` for cleanup and `Task`
as the async contract.

## Status

**Pre-release: the v1 roadmap is complete, and nothing is published.** All thirteen roadmap phases (0 through
12) are built — the layers, the adapters, host integration, the conformance kit, a reconciliation of every
documented deviation against as-built source (phase 11) and the release path (phase 12: strong-named
assemblies, symbol packages, a release preflight and an inert release workflow). Every package is at
`0.1.0-alpha.1`, and no tag has been cut. **The first publish is held** on actions outside the repository;
what each needs, what v1 ships without and what would trigger work after it are tracked in
[`docs/first-release.md`](./docs/first-release.md).

## Packages

Five NuGet packages, versioned in lockstep. `Dexpace.Sdk.Core` ships no transport and no codec, so a call
needs a transport package beside it.

| Package | Provides | Third-party dependencies |
|---|---|---|
| [`Dexpace.Sdk.Core`](./src/Dexpace.Sdk.Core/README.md) | Models, bodies, the transport seam, the pipeline and its policies, retry, redirect and authentication, pagination, server-sent events, configuration, logging, tracing and metrics, errors and the `ISerde` seam | `Microsoft.Extensions.Logging.Abstractions` only |
| [`Dexpace.Sdk.Http.SystemNet`](./src/Dexpace.Sdk.Http.SystemNet/README.md) | `SystemNetHttpClient` — the reference transport over `System.Net.Http.HttpClient` | none |
| [`Dexpace.Sdk.Serialization.SystemTextJson`](./src/Dexpace.Sdk.Serialization.SystemTextJson/README.md) | `SystemTextJsonSerde` — the codec over source-generated `System.Text.Json`, with `Tristate<T>` wired | none |
| [`Dexpace.Sdk.Extensions.DependencyInjection`](./src/Dexpace.Sdk.Extensions.DependencyInjection/README.md) | `AddDexpaceClient` — named clients as keyed services over `IHttpClientFactory`, configured from an `IConfigurationSection` and validated at start | `Microsoft.Extensions.Http` |
| [`Dexpace.Sdk.Conformance`](./src/Dexpace.Sdk.Conformance/README.md) | The suites every adapter is proven against — transport, cross-cutting invariants, codec and packaging — with a loopback HTTP/1.1 fixture | none |

Every adapter depends on `Dexpace.Sdk.Core` plus at most one third-party library (`NFR-2`), and the dependency
audit enforces it on every pack. Once the first version is published, install the core plus whichever
transport and codec you need:

```bash
dotnet add package Dexpace.Sdk.Core --prerelease
dotnet add package Dexpace.Sdk.Http.SystemNet --prerelease
dotnet add package Dexpace.Sdk.Serialization.SystemTextJson --prerelease
```

Until then, `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` builds the five
packages, and a folder of them is a NuGet source.

## Quick start

Every C# fence in this README is a complete program that CI compiles against the built libraries with
warnings as errors (`dotnet run --project .claude/skills/housekeeping/src -- fences`). The addresses are
placeholders.

### A minimal request

```csharp
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);   // deadline, redirect, retry, instrumentation, …

using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/pets?limit=2"), CancellationToken.None);

Console.WriteLine(response.Status.Code);                       // 200
Console.WriteLine(await response.Body.ReadAsStringAsync());    // decoded once by charset, and the body disposed
```

A response body streams from the socket until it is drained or disposed, and the caller owns it: the
convenience readers do both, and disposing the `Response` releases it unread. A response of any status is
returned as it is — a 404 is a `Response`, not an exception, until you ask for one with
`EnsureSuccessAsync` or a response handler. The pipeline never owns the transport; dispose both.

### A typed call with a JSON body

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(PetJson.Default));
await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

// The codec stamps the body's media type: application/json; charset=utf-8.
var request = Request.Post("https://api.example.com/pets", RequestBody.FromValue(new NewPet("Rex"), serde));

// The handler decodes a 2xx, throws HttpResponseException for a 4xx or 5xx, and disposes the response on every path.
var handler = ResponseHandlers.DeserializeOnSuccess<Pet>(serde);
Pet created = await pipeline.SendAsync(request, handler.HandleAsync, RequestOptions.Empty, CancellationToken.None);

Console.WriteLine($"{created.Id}: {created.Name}");

internal sealed record NewPet(string Name);

internal sealed record Pet(string Id, string Name);

[JsonSerializable(typeof(NewPet))]
[JsonSerializable(typeof(Pet))]
internal sealed partial class PetJson : JsonSerializerContext;
```

Type metadata comes from a source-generated `JsonSerializerContext`, so serialization is trim-safe and
NativeAOT-safe with no reflection fallback. The typed readers stream rather than buffer, name `T` in a
failure, and reject a wire `null` for a reference type.

### A configured pipeline

Options are sealed records, changed with `with`. Hand `DexpacePipeline.CreateDefault` an auth policy to fill
the auth stage — here a bearer token fetched by your credential, cached, refreshed before it expires, and
retried once on a `401`.

```csharp
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Http.SystemNet;

var options = new DexpaceClientOptions
{
    OverallTimeout = TimeSpan.FromSeconds(45),            // across every attempt and redirect
    AttemptTimeout = TimeSpan.FromSeconds(10),            // per attempt; expiry is retryable
    Retry = new RetryOptions { MaxRetryAttempts = 4 },
    Redirect = new RedirectOptions { MaxRedirects = 5 },
    Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers },   // off until raised; redacted by default
};

await using var transport = new SystemNetHttpClient();
var auth = new BearerTokenAuthPolicy(new EnvironmentTokenCredential("PETS_TOKEN"), "pets.read");
using HttpPipeline pipeline = PipelineBuilder.Flatten(DexpacePipeline.CreateDefault(transport, auth)).Build(transport, options);

// One call, more patient: a copy, never a mutation.
var patient = options with { Retry = options.Retry with { MaxRetryAttempts = 6 } };
using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/pets/7"), patient, CancellationToken.None);

internal sealed class EnvironmentTokenCredential(string variable) : TokenCredential
{
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
        ValueTask.FromResult(new AccessToken(
            Environment.GetEnvironmentVariable(variable) ?? throw new InvalidOperationException($"{variable} is not set."),
            DateTimeOffset.UtcNow.AddMinutes(30)));   // a real credential asks the identity provider for its real expiry
}
```

Options are validated where they are set: `new RetryOptions { MaxRetryAttempts = -1 }` throws at the
assignment, naming the property, not on the first call. The redirect loop wraps the retry loop, which wraps
auth, so a retried attempt re-stamps its credential and a redirect hop is judged cross-origin against the
**original** request. A pillar stage admits one policy, so the builder refuses a second auth policy;
`PipelineBuilder` also supports `InsertBefore`, `InsertAfter`, `Replace` and `Remove`.

### Streaming and replayable bodies

```csharp
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

RequestBody bytes = RequestBody.FromBytes(new byte[] { 1, 2, 3 });                                   // replayable
RequestBody text = RequestBody.FromString("{\"hello\":\"world\"}", MediaType.Parse("application/json")); // replayable
RequestBody form = RequestBody.FromForm([new("grant_type", "client_credentials")]);                 // replayable
RequestBody file = RequestBody.FromFile("upload.bin", offset: 0, count: 4096);                       // replayable; a fresh handle per write

await using var upload = new MemoryStream(new byte[] { 4, 5, 6 });
RequestBody once = RequestBody.FromStream(upload);              // single-use: a second write throws StreamConsumedException
RequestBody again = await once.ToReplayableAsync();             // buffered, so a retry or a redirect can re-send it

Console.WriteLine($"{bytes.IsReplayable} {text.IsReplayable} {form.IsReplayable} {file.IsReplayable} {once.IsReplayable} {again.IsReplayable}");
```

A stream body over a readable, seekable stream with a declared length is replayable: it seeks the caller's
stream before each write. Buffering an arbitrarily large upload to make it retryable is a decision for the
caller who knows how large it is, so the retry loop re-sends only a body that already said it could be re-sent,
and a method-preserving redirect over a single-use body throws `RedirectBodyNotReplayableException`.

### Pagination

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(PageJson.Default));
await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

AsyncPageable<Pet> pets = Pageable.Create(
    pipeline,                                                                    // a pipeline is a transport
    Request.Get("https://api.example.com/pets"),
    serde,                                                                       // reads each page envelope
    PaginationStrategies.Cursor<PetPage, Pet>(page => page.Items, page => page.Next),
    maxPages: 50);

await foreach (Pet pet in pets) { Console.WriteLine(pet.Name); }                 // lazy, item by item, in server order
await foreach (Page<Pet> page in pets.AsPages()) { Console.WriteLine(page.Values.Count); }   // or page by page

internal sealed record Pet(string Id, string Name);

internal sealed record PetPage(IReadOnlyList<Pet> Items, string? Next);

[JsonSerializable(typeof(PetPage))]
internal sealed partial class PageJson : JsonSerializerContext;
```

`PaginationStrategies.Cursor`, `PageNumber` and `LinkHeader` (RFC 8288 `Link: rel="next"`, refused
cross-origin) ship in core; `Pageable.CreateBlocking` walks the same strategies on the synchronous path, and
`Pageable.FromFetchers` adapts a hand-written fetch function.

### Server-sent events

```csharp
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

var request = Request.Create(Method.Get, "https://api.example.com/events",
    new Headers.Builder().Add("Accept", "text/event-stream").Build());   // the Accept header is yours: core adds nothing

await using var events = ServerSentEventStream.FromResponse(await pipeline.SendAsync(request, CancellationToken.None));
await foreach (ServerSentEvent ev in events)
{
    Console.WriteLine($"{ev.Event ?? "(unnamed)"}: {string.Join("\n", ev.Data)}");   // one Data entry per data: line
}
```

The stream owns the response from the call and releases it once on every path: at the end, on an early
dispose and on failure. Lines and events are capped (1 MiB and 8 MiB by default) and rejected rather than
truncated, and `MapAsync` maps each event to your own model with `Skip` and `Done` as the two sentinel outcomes.

### Three-state PATCH

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(PatchJson.Default));

var clear = new PetPatch(Name: Tristate.Present("Rex"), Nick: Tristate.Null);
var leave = clear with { Nick = Tristate.Absent };

Console.WriteLine(serde.SerializeToString(clear));   // {"name":"Rex","nick":null}
Console.WriteLine(serde.SerializeToString(leave));   // {"name":"Rex"}

internal sealed record PetPatch(Tristate<string> Name, Tristate<string> Nick);

[JsonSerializable(typeof(PetPatch))]
internal sealed partial class PatchJson : JsonSerializerContext;
```

`Tristate<T>` distinguishes absent, null and present, so "leave it alone" and "clear it" stop being the same
wire message. The omission is the codec's, wired by `SystemTextJsonSerde` on its own copy of your options.

### The synchronous path

```csharp
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;

using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

using var response = pipeline.Send(Request.Get("https://api.example.com/health"), CancellationToken.None);
Console.WriteLine(response.Body.ReadAsString());
```

`Send` is a genuine synchronous send — `HttpClient.Send` under the reference transport, not sync-over-async —
and runs the same policies as `SendAsync`. Every async body member has a synchronous twin.

### With dependency injection

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Extensions.DependencyInjection;
using Dexpace.Sdk.Serialization.SystemTextJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

IConfiguration configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>   // appsettings.json in a host
    {
        ["Dexpace:Pets:BaseAddress"] = "https://api.example.com/v1/",
        ["Dexpace:Pets:Retry:MaxRetryAttempts"] = "4",
        ["Dexpace:Pets:OverallTimeout"] = "30s",
    })
    .Build();

var services = new ServiceCollection();
services.AddSingleton(configuration);
services.AddDexpaceClient("pets", configuration.GetSection("Dexpace:Pets"))
    .UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(PetJson.Default)));

await using ServiceProvider provider = services.BuildServiceProvider();   // DisposeAsync: the transport is IAsyncDisposable
HttpPipeline pipeline = provider.GetRequiredKeyedService<HttpPipeline>("pets");

internal sealed record Pet(string Id, string Name);

[JsonSerializable(typeof(Pet))]
internal sealed partial class PetJson : JsonSerializerContext;
```

The configuration is read by a hand-written, AOT-safe reader, not the binder: a bad value, a rejected setting
or an unknown key fails at start, naming the key and never quoting the value. A codec is required, and there
is no reload.

## Architecture

A request flows down through policies ordered by stage, and the response flows back up through them. The
last hop is the transport.

```
caller → HttpPipeline ──┬─ Operation   100   overall deadline
                        ├─ PerCall     150   idempotency key, client identity (+ error mapping, opt-in)
                        ├─ Redirect    200   the redirect loop
                        ├─ PerHop      250
                        ├─ Retry       300   the retry loop
                        ├─ PerAttempt  400   Date
                        ├─ Auth        500   one auth policy
                        ├─ Diagnostics 600   span, metrics, http.request / http.response
                        ├─ Serde       700   reserved
                        └─ IAsyncHttpClient / IHttpClient → wire
```

`PipelineStage` is a closed set with sparse numbers. `Redirect`, `Retry` and `Auth` are **pillars**: each
admits exactly one policy and refuses a second. The stages around them stack, and are the extension slots.
A policy takes a request and the call-scoped `PipelineContext` and returns a response. `HttpPipeline`
implements both transport interfaces, so a pipeline is substitutable wherever a transport is — which is what
makes `PipelineBuilder.Nest` and `Flatten` work at all.

Bottom-up, the layers are:

1. **Bodies.** `RequestBody.WriteToAsync(Stream)` is the outgoing streaming surface; `ResponseBody.OpenReadAsync`,
   `ReadAsBytesAsync` and `ReadAsStringAsync` drain the incoming side, each with a synchronous twin. The
   convenience readers refuse a body above 64 MiB; stream it instead.
2. **Value models.** `Headers` is an immutable, case-insensitive, validated multimap — CR, LF and other controls
   throw at construction — and `MediaType`, `Method`, `Protocol` and `Status` are value types with well-known
   instances.
3. **Request and response.** `Request` is an immutable `record` over an absolute `Uri`, derived with `with`;
   `Response` is a disposable carrier of status, headers, body and protocol, and its disposal is latched.
4. **The transport seam.** `IAsyncHttpClient.ExecuteAsync(Request, RequestOptions, CancellationToken)` and its
   synchronous twin `IHttpClient`, bridged by `AsAsync` and `AsBlocking`. That is the whole contract.
5. **The pipeline.** Stages, policies, `PipelineContext`, `PipelineBuilder`, `HttpPipeline`, and the recovery layer
   the retry, idempotency and error-mapping policies share.
6. **Errors.** `SdkException` roots the hierarchy, with one exception: a transport failure,
   `ServiceRequestException`, is an `IOException`, so code written for `HttpClient` keeps catching it.

## Inside `Dexpace.Sdk.Core`

| Namespace | Surface |
|---|---|
| `Http.Common` | `Headers`, `HttpHeaderName`, `MediaType`, `CommonMediaTypes`, `Method`, `Protocol`, `HttpDate`, `ETag`, `HttpRange` |
| `Http.Request` | `Request`, `RequestBody` and its factories — bytes, string, file, stream, form, multipart, value — `Query`, `RequestOptions`, `RequestConditions` |
| `Http.Response` | `Response`, `ResponseBody`, `Status`, `IResponseHandler<T>`, `TypedResponse<T>` |
| `Client` | `IAsyncHttpClient`, `IHttpClient`, `HttpClientExtensions` (`AsAsync`, `AsBlocking`), `DelegateHttpClient`, `ResponseTaskExtensions` |
| `Operations` | `OperationDescriptor` — path templates, path parameters and the operation-input projection |
| `Pipeline` | `HttpPipeline`, `PipelineBuilder`, `HttpPipelinePolicy`, `PipelineStage`, `PipelineContext`, `PipelineRunner`, `DexpacePipeline` |
| `Pipeline.Policies` | `OperationPolicy`, `RedirectPolicy`, `RetryPolicy`, `IdempotencyPolicy`, `SetDatePolicy`, `ClientIdentityPolicy`, `InstrumentationPolicy`, `ErrorMappingPolicy`, and the `ApiKey`, `Basic`, `BearerToken`, `Challenge` and `MultiScheme` auth policies |
| `Recovery` | `Outcome`, the request and response step contracts and chains, `RecoveryDispatcher`, `RetryRecovery`, `ErrorMappingStep`, `IdempotencyKeyStep`, `ClientIdentityStep` |
| `Auth` | `TokenCredential`, `AccessToken`, `AccessTokenCache`, the `ApiKey`, `Basic` and `Digest` credentials, `AuthenticationChallenge`, the challenge handlers, `AuthDescriptor`, `AuthResolver` |
| `Pagination` | `AsyncPageable<T>`, `Pageable<T>`, `Page<T>`, `PageInfo<T>`, `IPageStrategy`, `Pageable`, `PaginationStrategies`, `PagingOptions` |
| `ServerSentEvents` | `ServerSentEventStream`, `ServerSentEvent`, `ServerSentEventReader`, `SseMapResult`, the line and event cap exceptions |
| `Serialization` | `ISerde`, `IStringSerde`, `ResponseHandlers`, the typed readers, `Tristate<T>` |
| `Configuration` | `DexpaceClientOptions`, `RetryOptions`, `RedirectOptions`, `HttpLoggingOptions`, `ProxyOptions`, `TimeProviderWaits`, `BuildInfo` |
| `Diagnostics` | `DexpaceDiagnostics` (the `ActivitySource` and `Meter`), `DexpaceLogEvents`, `DexpaceLogKeys`, `UrlRedactor`, `TraceIdGeneration` |
| `Execution` | `CallKey`, `InstrumentationContext`, the dispatch, request and exchange contexts |
| `Errors` | `SdkException` and its hierarchy, `ServiceRequestException`, `IRetryableError`, `ExceptionFacts`, `ExceptionTrail` |

## Highlights

- **One runtime dependency, and it is a gate.** `Dexpace.Sdk.Core` references the shared framework and
  `Microsoft.Extensions.Logging.Abstractions`, nothing else; each adapter adds at most one third-party library.
  `scripts/ci/dependency-audit.cs` checks every pack's `deps.json` and `.nuspec` against one policy record per
  library, and an architecture test proves core's tests never see a transport or a codec.
- **Immutable models, validated where they are built.** Every model is a `record` whose `init` accessors
  validate and name the property they reject; header names and values are validated again on the way to the
  wire, so a forged model cannot smuggle a CRLF into a request.
- **Pluggable everything, by interface.** A transport, a codec, a response handler, a paging strategy, a token
  credential and a clock (`TimeProvider`) are each a small interface or abstract class. There is no registry and
  no discovery: a conforming object is a valid implementation, and you pass it in.
- **Retry done right.** Exponential backoff with jitter, server pacing (`Retry-After` and the configured
  rate-limit headers) in a fixed precedence, idempotency and body replayability checked before any re-send, an
  `Idempotency-Key` on `POST`, `PUT` and `PATCH`, an overall deadline across every attempt, cancellation never
  retried, and deterministic tests through `TimeProvider`.
- **Redirects done right.** `GET` and `HEAD` only by default, `303` opt-in, a `POST` never rewritten, loop
  detection, a hop cap, `Authorization` stripped on every hop, `Cookie` and `Proxy-Authorization` stripped
  cross-origin, and HTTPS→HTTP refused by default. The SDK is the only redirect authority: the transport never
  follows one.
- **Real auth.** Bearer tokens behind a bounded cache with background refresh and one retry on `401`, an
  RFC 7235 challenge parser, RFC 7616 Digest (MD5, MD5-sess, SHA-256, SHA-256-sess), Basic and API-key
  credentials — refused over plaintext, and redacted in `ToString()`.
- **Sync and async from one core.** The same policies run on `SendAsync` and on a genuinely synchronous
  `Send`, and every body member has a twin. Library code awaits with `ConfigureAwait(false)`, and the
  blocking bridges are explicit.
- **Observability that costs nothing when off.** An `ActivitySource` and a `Meter` with stable attribute sets,
  `http.request` and `http.response` log events through `ILogger`, header and URL redaction by allow-list before
  any sink sees a value, and allocation tests on the disabled path.
- **Trimmable and NativeAOT-compatible.** Every library is `IsTrimmable` and `IsAotCompatible`, codecs resolve
  metadata from a source-generated context, and a NativeAOT smoke consumer is published and run in CI.
- **Proven, not asserted.** `Dexpace.Sdk.Conformance` runs 43 transport assertions against the reference
  transport and the DI-built one over a real socket, plus the invariant (28), codec (11) and packaging (8)
  suites — each assertion with a deliberately broken subject it must fail.

## Development

The .NET SDK is pinned in `global.json` (`10.0.401`, rolling forward to the latest patch), and every project
targets `net10.0`.

```bash
git clone https://github.com/dexpace/dotnet-sdk.git
cd dotnet-sdk
dotnet restore Dexpace.Sdk.sln --locked-mode   # lock files are committed; CI restores locked
```

```bash
dotnet build  Dexpace.Sdk.sln --configuration Release        # the build IS the lint gate (warnings as errors)
dotnet format Dexpace.Sdk.sln --verify-no-changes            # formatting, from .editorconfig
dotnet test   --solution Dexpace.Sdk.sln --configuration Release
dotnet test   --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet pack   Dexpace.Sdk.sln --configuration Release --output artifacts/packages
```

Tests are xUnit v3 on Microsoft.Testing.Platform, so `dotnet test` takes `--solution` / `--project` and the
platform's options (`--filter-class`, `--filter-trait`), not VSTest's `--filter`. Every gate CI runs — the
analyzers with warnings as errors, the public-API files, the banned-API list, an 80% coverage floor, the
dependency audit, packaging conformance, the reproducible pack, the release preflight, the NativeAOT smoke,
the requirement-coverage check and the documentation fence compiler — is runnable locally, and each is
listed with its command in [`docs/sdk-documentation/quality-gates.md`](./docs/sdk-documentation/quality-gates.md).
CI runs the full suite on Linux, Windows and macOS ([`.github/workflows/ci.yml`](./.github/workflows/ci.yml)).

## Conventions

The full contract is in [`CLAUDE.md`](./CLAUDE.md); the documentation map is
[`docs/README.md`](./docs/README.md). The short version:

- **Spec-driven, not feature-driven.** [`docs/product-spec/`](./docs/product-spec.md) is normative and
  numbered, and shared with the sibling SDKs; the code exists to satisfy it. Before implementing anything,
  find the requirement IDs.
- **Records are the base.** Every model is a `record` or `readonly record struct`, validated in its
  constructor and `init` accessors, and derived through `with` or a `With*` helper. `Headers.Builder` is the one
  builder.
- **Public means documented and locked.** A public member carries a `///` doc comment (a missing one fails the
  build), and every change to the surface is a reviewed `PublicAPI.Unshipped.txt` diff.
- **Typed errors only.** Every SDK failure derives from `SdkException`, except the transport failure,
  `ServiceRequestException`, which is an `IOException`.
- **Every source file opens with the MIT license header**, and a build analyzer enforces it.
- **Every gap is routed to its owner.** Work for the release goes to
  [`docs/first-release.md`](./docs/first-release.md); a deliberate divergence from the reference contract goes
  to the deviation ledger, audited by [`docs/deviations.md`](./docs/deviations.md).

As-built documentation — one page per layer and adapter, and how the packages compose — starts at
[`docs/sdk-documentation/architecture.md`](./docs/sdk-documentation/architecture.md), with
[`getting-started.md`](./docs/sdk-documentation/getting-started.md) and
[`example-end-to-end.md`](./docs/sdk-documentation/example-end-to-end.md) beside it. Each package's own README
says how to use it. [`CONTRIBUTING.md`](./CONTRIBUTING.md) is the contribution flow and
[`SECURITY.md`](./SECURITY.md) how to report a vulnerability.

## Releases

Every package is at `0.1.0-alpha.1`, and the first release is `0.1.0`. One `VersionPrefix` in
`Directory.Build.props` stamps every package in lockstep; what a version number promises is in
[`versioning.md`](./docs/sdk-documentation/versioning.md).

Publishing is held at this time. A tag-triggered [`release.yml`](./.github/workflows/release.yml) builds,
tests and packs reproducibly, runs the release preflight and attests what it would publish; its publish job
uses NuGet trusted publishing (OIDC, no long-lived key) and stays inert until:

1. nuget.org ownership is settled — an account and organisation, the `Dexpace` ID prefix reserved, and a
   trusted-publishing policy for this repository's release workflow.
2. The `nuget` environment exists with its protection, and the workflow's action pins are verified.

The assemblies are already strong-named and every package ships a symbol package; publishing artifacts
byte-identical to a rebuild from their tag (the release half of `NFR-12`) and closing `NFR-16` wait on the
first publish. [`docs/first-release.md`](./docs/first-release.md) records each condition, with every other
blocker before the first publish.

## License

MIT — see [`LICENSE`](./LICENSE).
