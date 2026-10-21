# Dexpace.Sdk.Extensions.DependencyInjection

Host integration for the dexpace .NET SDK: named clients registered as keyed services over `Microsoft.Extensions.DependencyInjection`, configured from an
`IConfigurationSection`. It references [`Dexpace.Sdk.Core`](https://www.nuget.org/packages/Dexpace.Sdk.Core), the reference transport
[`Dexpace.Sdk.Http.SystemNet`](https://www.nuget.org/packages/Dexpace.Sdk.Http.SystemNet) and `Microsoft.Extensions.Http`, and no codec.

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`.

```csharp
services.AddDexpaceClient("github", builder.Configuration.GetSection("Dexpace:GitHub"))
    .UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(GitHubJsonContext.Default)))
    .UseAuth(GitHubAuth.Descriptor)
    .Configure(o => o with { Redirect = o.Redirect with { Predicate = GitHubRedirects.Allow } });

public sealed class GitHubClient([FromKeyedServices("github")] HttpPipeline pipeline, [FromKeyedServices("github")] ISerde serde) { /* ... */ }
```

## What you get

- **Four keyed singletons per client**, under its name: `HttpPipeline`, `IAsyncHttpClient`, `ISerde` and `DexpaceClientOptions`. `AddDexpaceClient` returns a sealed `DexpaceClientBuilder` (`Configure`,
  `ConfigureTransport`, `ConfigureCredentials`, `UseTransport`, `UseSerde`, `UseAuth`, `UseAuthPolicy`, `AddPolicy`, `UseProxyFromEnvironment`, and `HttpClient`, the `IHttpClientBuilder` of the named client).
- **A codec is required**: there is no default. A client without one fails validation with `client 'github' has no ISerde: call UseSerde(...) ...`.
- **Settings from the section you pass** (never the configuration root). Keys are relative to it, case-insensitive and `:`-separated (`Dexpace__Retry__MaxRetryAttempts` as an environment variable):
  `BaseAddress`, `UserAgent`, `OverallTimeout`, `AttemptTimeout`; `Retry:*`, `Redirect:*`, `Logging:*`, `Transport:*` (including `Transport:Proxy:*` and `Transport:PooledConnectionLifetime`) and `Auth:*`.
  The full table is in [`docs/sdk-documentation/dependency-injection.md`](https://github.com/dexpace/dotnet-sdk/blob/main/docs/sdk-documentation/dependency-injection.md).
- **Strict grammars**: base-10 integers, `true`/`false`, durations (`PT5S`, `250ms`, `3s`, `1500` as milliseconds, `none`), enums by name, lists that replace the defaults. An empty value is absent and falls through to the next provider that has a value when the registered `IConfiguration` is the root that holds the section (the record default applies when none does). A section from any other configuration keeps the merged read, where an empty value in the highest provider still hides the lower one: read the section from the configuration you register.
- **Failures at start, naming the key and never the value.** Every bad value, rejected setting and unknown key is reported at once as `"<path>: <rule>"` through `ValidateOnStart` (or `IStartupValidator.Validate()` with no host, or the first
  resolution).
- **No reload**: the snapshot is taken at first resolution.

## The named client

The transport is built over `IHttpClientFactory` with a redirect-off primary handler (`SystemNetHttpClient.CreatePrimaryHandler`), the factory's own loggers removed (the SDK's redacted `http.request`/`http.response` events are the
one HTTP log stream), an infinite handler lifetime with `Transport:PooledConnectionLifetime` (default 2 minutes) doing the rotation, and a container-owned lease that releases the primary when the container is disposed. Dispose
the provider with `DisposeAsync`. `HttpClient.ConfigureHttpClient(...)` has no effect.

## Proxy

Explicit `Transport:Proxy:*` keys, **or** `UseProxyFromEnvironment()` / `UseProxyFromEnvironment(key => configuration[key])` in code. No configuration value turns the environment read on (`CFG-28`); two sources fail validation.

## One retry layer

The package registers no retrying handler. Compose the standard resilience handler without a second retry engine like this (the SDK's own `MaxRetryAttempts = 0` would still leave the handler re-sending request bodies, and the
handler-side `MaxRetryAttempts = 0` is refused by its own validation):

```csharp
services.AddDexpaceClient("github", section)
    .HttpClient.AddStandardResilienceHandler(o => o.Retry.ShouldHandle = _ => ValueTask.FromResult(false));
```
