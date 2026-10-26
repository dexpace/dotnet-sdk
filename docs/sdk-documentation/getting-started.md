# Getting started

**As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). This page takes you from nothing to a typed call: install the
packages, build a client with and without dependency injection, send a request and read a typed answer, handle the failures a call can raise, and dispose what you own. Every
C# fence below is a complete program that the documentation build compiles against the built libraries with warnings as errors (`dotnet run --project
.claude/skills/housekeeping/src -- fences`, `quality-gates.md`); the addresses are placeholders. [`architecture.md`](./architecture.md) is the map of the
packages, and [`example-end-to-end.md`](./example-end-to-end.md) is the same material grown into one SDK slice.

## Install

Nothing is published yet: the first publish is held (`docs/first-release.md`), so the commands below are the ones that work once it happens. Every package targets `net10.0`, and
the version is the pre-release `0.1.0-alpha.1`, so a package reference needs `--prerelease`.

```bash
dotnet add package Dexpace.Sdk.Core --prerelease                           # models, bodies, pipeline, auth, pagination, server-sent events, the ISerde seam
dotnet add package Dexpace.Sdk.Http.SystemNet --prerelease                 # the reference transport over System.Net.Http.HttpClient
dotnet add package Dexpace.Sdk.Serialization.SystemTextJson --prerelease   # ISerde over source-generated System.Text.Json
dotnet add package Dexpace.Sdk.Extensions.DependencyInjection --prerelease # AddDexpaceClient: a named client over IHttpClientFactory
dotnet add package Dexpace.Sdk.Conformance --prerelease                    # test projects only: the conformance kit
```

Until then, build from a clone: `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` produces the five `.nupkg` files, and a folder of them is a NuGet source.

| You want | Install |
|---|---|
| To send requests and walk the SDK's guarantees | `Dexpace.Sdk.Core` and a transport: `Dexpace.Sdk.Http.SystemNet` |
| Typed JSON bodies | `Dexpace.Sdk.Serialization.SystemTextJson` (or your own `ISerde`, `write-a-serde.md`) |
| Named clients configured from `appsettings.json` | `Dexpace.Sdk.Extensions.DependencyInjection` (it brings the transport) |
| To prove your own transport or codec | `Dexpace.Sdk.Conformance` in a test project, never in production code |

`Dexpace.Sdk.Core` ships no transport and no codec, on purpose ([`architecture.md`](./architecture.md#the-five-packages)), so a call needs at least two packages.

## A client without dependency injection

A transport, a pipeline over it, and a request:

```csharp
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/health"), CancellationToken.None);

Console.WriteLine($"{response.Status.Code} over {response.Protocol}");
Console.WriteLine(await response.Body.ReadAsStringAsync());
```

- **The transport is yours.** `SystemNetHttpClient` owns the `HttpClient` it creates and never disposes one you give it, and the pipeline never owns the transport:
  disposing the pipeline does nothing to it. Dispose both, the transport last.
- **`DexpacePipeline.CreateDefault(transport)`** puts the overall deadline, an `Idempotency-Key` on `POST`, `PUT` and `PATCH`, the client identity (`User-Agent`), a `Date` header, redirect
  following, retry and instrumentation between your code and the wire. A response of any status is returned as it is: a 404 is a `Response`, not an exception, unless you ask
  for one ([Errors](#errors)).
- **`SendAsync` has no optional token.** Pass `CancellationToken.None` or a real one. `HttpPipeline` also has `Send`, the genuinely synchronous twin
  ([`pipelines.md`](./pipelines.md)), and is itself an `IAsyncHttpClient` and an `IHttpClient`.
- **Reading a body consumes it.** `ReadAsStringAsync` disposes the body when it is done, so a second read throws `StreamClosedException`. Buffer first when you need two reads.
  A body above 64 MiB is refused by the convenience readers with `BodyTooLargeException`; stream it with `OpenReadAsync` ([`bodies.md`](./bodies.md)).

### Options

Behaviour is a record, and a record is changed with `with`:

```csharp
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();

// Baked in: every call through this pipeline starts from these options.
var options = new DexpaceClientOptions
{
    BaseAddress = new Uri("https://api.example.com/v1/"),
    OverallTimeout = TimeSpan.FromSeconds(30),
    Retry = new RetryOptions { MaxRetryAttempts = 4 },
    Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers },
};
using HttpPipeline pipeline = PipelineBuilder.Flatten(DexpacePipeline.CreateDefault(transport)).Build(transport, options);

// Per call: a copy, never a mutation. The defaults retry twice (three sends in all) and follow three redirects.
var patient = options with { Retry = options.Retry with { MaxRetryAttempts = 6 } };
using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/v1/health"), patient, CancellationToken.None);

// Per request: RequestOptions travel with one call and reach every policy and the transport.
var bounded = new RequestOptions { Timeout = TimeSpan.FromSeconds(5), MaxRetries = 0 };
using var quick = await pipeline.SendAsync(Request.Get("https://api.example.com/v1/health"), bounded, CancellationToken.None);
```

`DexpaceClientOptions` is the client's (base address, deadlines, `Retry`, `Redirect`, `Logging`), `RequestOptions` is one call's; the two are not interchangeable and the pipeline takes
either. Logging is **off** until you raise `HttpLoggingOptions.Level`, and what it writes is redacted by default ([`logging-and-redaction.md`](./logging-and-redaction.md)). The options are
validated where they are set, so `new RetryOptions { MaxRetryAttempts = -1 }` throws an `ArgumentOutOfRangeException` at the assignment, not on the first call
([`configuration.md`](./configuration.md), [`retry.md`](./retry.md), [`redirect.md`](./redirect.md)).

## A typed call

The codec seam is `ISerde`; `SystemTextJsonSerde` implements it over a source-generated `JsonSerializerContext`, so serialization is trim-safe and NativeAOT-safe with no reflection.

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(WidgetJson.Default));
await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

// A request body from a value: the codec stamps its media type (application/json; charset=utf-8).
var request = Request.Post("https://api.example.com/widgets", RequestBody.FromValue(new NewWidget("gear", 9), serde));

// The handler turns the response into a value and owns it: it is disposed on every path.
var handler = ResponseHandlers.DeserializeOnSuccess<Widget>(serde);
Widget created = await pipeline.SendAsync(request, handler.HandleAsync, RequestOptions.Empty, CancellationToken.None);

Console.WriteLine($"{created.Id}: {created.Name} ({created.Teeth} teeth)");

internal sealed record NewWidget(string Name, int Teeth);

internal sealed record Widget(string Id, string Name, int Teeth);

[JsonSerializable(typeof(NewWidget))]
[JsonSerializable(typeof(Widget))]
internal sealed partial class WidgetJson : JsonSerializerContext;
```

- **`ResponseHandlers.DeserializeOnSuccess<T>(serde)`** decodes a 2xx, throws `HttpResponseException` for a 4xx or 5xx, and throws a `DeserializationException` for anything else. `ResponseHandlers.Deserialize<T>`
  decodes whatever the status. The handler streams, names `T` in a failure, and rejects a wire `null` for a reference type. To write your own, see `write-a-response-handler.md`.
- **`SendAsync<T>(request, handler, options, token)`** disposes the response after the handler runs, in every outcome. When you hold a `Response` yourself, `await response.Body.ReadValueAsync<Widget>(serde)` is the same read, and
  both stream rather than buffer.
- **The context is the contract.** `SystemTextJsonSerde` takes a private copy of the options you give it and wires `Tristate<T>` (absent, `null` or present, for a PATCH) on the copy; a
  context generated in the fast-path-only mode cannot be used with it ([`serde.md`](./serde.md)).

## Errors

A call raises a small, closed set, and the split between them matters:

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(ProblemJson.Default));
await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);
using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));

try
{
    using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/widgets/7"), cancel.Token);
    await response.EnsureSuccessAsync(cancel.Token);   // a 4xx or 5xx becomes an HttpResponseException
    Console.WriteLine(await response.Body.ReadAsStringAsync(cancel.Token));
}
catch (HttpResponseException ex)
{
    // The server answered: the status, and the error body it sent, buffered up to 1 MiB.
    Problem? problem = await ex.GetErrorAsync<Problem>(serde, cancel.Token);
    Console.WriteLine($"{ex.Status.Code}: {problem?.Title} (retryable: {ex.IsRetryable})");
}
catch (IOException ex) when (ex is IRetryableError)
{
    // The transport failed: no response arrived, or the request could not be sent. A ServiceRequestException is an IOException,
    // not an SdkException, so `catch (SdkException)` below does not see it.
    Console.WriteLine($"transport failure: {ex.Message}");
}
catch (SdkException ex)
{
    // Everything else the SDK raises: a serde failure, a redirect refusal, an auth failure, a lifecycle error.
    Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
}
catch (OperationCanceledException) when (cancel.IsCancellationRequested)
{
    Console.WriteLine("cancelled by the caller");
}

internal sealed record Problem(string Title);

[JsonSerializable(typeof(Problem))]
internal sealed partial class ProblemJson : JsonSerializerContext;
```

| You catch | It means | Retried by the SDK (a retry also needs a request that can be sent again) |
|---|---|---|
| `HttpResponseException` | a 4xx or 5xx was received intact; `Status`, the bounded error body and `GetErrorAsync<T>` | when the status is retryable (408, 429, 500, 502, 503 and 504 by default) |
| `ServiceRequestException` (an `IOException`) | the request could not be sent or no response arrived; `ServiceRequestTimeoutException` is the elapsed-deadline leaf | yes |
| `OperationTimeoutException` | `OverallTimeout` elapsed across all attempts | no |
| `SerializationException` / `DeserializationException` (under `SerdeException`) | the codec failed; its own exception is the `InnerException` | no |
| `RedirectException` leaves, `HttpsRequiredException`, `AuthResolutionException`, `TokenProviderException` | a redirect or credential rule refused the call before anything unsafe was sent | no |
| `OperationCanceledException` | your token was cancelled | no |

`catch (SdkException)` does **not** catch a transport failure: `ServiceRequestException` derives from `IOException` so that code written for `HttpClient` keeps catching it, and it carries
`IRetryableError` for the retry classifier. The decision, and the one place it departs from the reference contract, is in [`docs/deviations.md`](../deviations.md). A caught exception's
`ToString()` includes the failures that happened while the SDK was cleaning up (`ExceptionTrail`, [`recovery.md`](./recovery.md)); an `HttpResponseException` message carries the status
and not the URL (`XCUT-19`), and a credential's `ToString()` redacts its secret.

## With dependency injection

`Dexpace.Sdk.Extensions.DependencyInjection` registers a **named** client as four keyed singletons (`HttpPipeline`, `IAsyncHttpClient`, `ISerde`, `DexpaceClientOptions`) and reads its
settings from the configuration section you hand it:

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Extensions.DependencyInjection;
using Dexpace.Sdk.Serialization.SystemTextJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// In a host this is builder.Configuration; an in-memory source stands in for appsettings.json here.
IConfiguration configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Dexpace:Widgets:BaseAddress"] = "https://api.example.com/v1/",
        ["Dexpace:Widgets:Retry:MaxRetryAttempts"] = "4",
        ["Dexpace:Widgets:OverallTimeout"] = "30s",
    })
    .Build();

var services = new ServiceCollection();
services.AddSingleton(configuration);
services.AddDexpaceClient("widgets", configuration.GetSection("Dexpace:Widgets"))
    .UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(WidgetJson.Default)));

// Dispose the provider with DisposeAsync: the transport is IAsyncDisposable.
await using ServiceProvider provider = services.BuildServiceProvider();
var pipeline = provider.GetRequiredKeyedService<HttpPipeline>("widgets");
var options = provider.GetRequiredKeyedService<DexpaceClientOptions>("widgets");

Console.WriteLine($"{options.BaseAddress} retries {options.Retry.MaxRetryAttempts}");
using var response = await pipeline.SendAsync(Request.Get(new Uri(options.BaseAddress!, "widgets/7").AbsoluteUri), CancellationToken.None);

internal sealed record Widget(string Id, string Name);

[JsonSerializable(typeof(Widget))]
internal sealed partial class WidgetJson : JsonSerializerContext;
```

- **Pass the client's own section, never the configuration root.** The root holds every process variable, which the unknown-key rule would reject. A bad value, a rejected setting and an
  unknown key all fail at start, naming the key (`ValidateOnStart`, or `IStartupValidator.Validate()` with no host), and never quoting the value.
- **A codec is required.** There is no default; a client without `UseSerde` fails validation.
- **There is no reload.** The snapshot is taken at first resolution.
- **One retry layer.** If you compose `AddStandardResilienceHandler`, switch its retry off with `o.Retry.ShouldHandle = _ => ValueTask.FromResult(false)` rather than setting the SDK's
  `MaxRetryAttempts` to zero.

The keys, the grammars and the transport the named client builds are in [`dependency-injection.md`](./dependency-injection.md).

## Disposal

| You hold | Dispose it | Notes |
|---|---|---|
| a `Response` | always | a streamed body holds its connection until then; disposal is latched and happens at most once |
| a `ResponseBody` you opened | when done | the convenience readers (`ReadAsBytesAsync`, `ReadAsStringAsync`, `ReadValueAsync`) dispose it for you |
| a `TypedResponse<T>` | if you never call `GetValueAsync` | the handler owns the response on its own paths |
| a `SystemNetHttpClient` | `await using` | an `HttpClient` you passed in is never disposed by it |
| an `HttpPipeline` | `using`, but it owns nothing | disposing it does not dispose the transport |
| a DI `ServiceProvider` | `DisposeAsync` | a synchronous dispose throws for an async-only transport |

A **request** body made from a stream is single-use: it throws `StreamConsumedException` on a second write. Call `RequestBody.ToReplayableAsync()` before the first send when the call may be retried
or redirected with the same method: a single-use body is never sent twice, so the retry is not attempted, and a method-preserving redirect over it throws `RedirectBodyNotReplayableException`.

## Where next

- The shape of the toolkit: [`architecture.md`](./architecture.md). A whole slice (an operation, a handler, bearer auth, pagination): [`example-end-to-end.md`](./example-end-to-end.md).
- Behaviour you will want to tune: [`retry.md`](./retry.md), [`redirect.md`](./redirect.md), [`auth.md`](./auth.md), [`configuration.md`](./configuration.md).
- Streaming: [`sse.md`](./sse.md) for `text/event-stream`, [`pagination.md`](./pagination.md) for list endpoints.
- Extending it: the four `write-a-*` guides, linked from [`architecture.md`](./architecture.md#the-seams).
- What the version number promises: `versioning.md`.
