# Host integration: `Dexpace.Sdk.Extensions.DependencyInjection`

**As built by phase 9, written against source on 2026-10-13** (branch `96-phase-9-host-integration`). This page describes how a host registers a
named SDK client with `Microsoft.Extensions.DependencyInjection`, how the package reads the client's settings from an `IConfigurationSection`, when and how a
bad setting fails, how the named client's transport is built over `IHttpClientFactory`, and how to compose a resilience handler without a second retry
engine. The requirement IDs it cites are the normative text (appendix C of the [product spec](../product-spec.md) indexes them: `CFG-1` to `CFG-38`, `SEAM-5` to `SEAM-7`, `OBS-35`, `TRANSPORT-1`, `XCUT-10`); the decisions behind each behaviour are in the [phase 9 design](../work/mvp/phase9/2026-10-12-phase9-host-integration-design.md) (rulings `P9-1` to
`P9-29`, facts `V1` to `V9`) and in [design §3.6 and §8.2](../sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md). Neither is restated here.

## Registration (`CFG-37`, `SEAM-6`; P9-5)

```csharp
services.AddDexpaceClient("github", builder.Configuration.GetSection("Dexpace:GitHub"))
    .UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(GitHubJsonContext.Default)))
    .UseAuth(GitHubAuth.Descriptor)
    .Configure(o => o with { Redirect = o.Redirect with { Predicate = GitHubRedirects.Allow } });

public sealed class GitHubClient(
    [FromKeyedServices("github")] HttpPipeline pipeline,
    [FromKeyedServices("github")] ISerde serde,
    [FromKeyedServices("github")] DexpaceClientOptions options) { /* ... */ }
```

- **A client is four keyed singletons**, under the client's name: `HttpPipeline`, `IAsyncHttpClient` (the transport), `ISerde` and `DexpaceClientOptions` (the snapshot a
  generated client needs for `OperationDescriptor.BuildRequest`). A generated SDK ships its own `AddGitHubClient(this IServiceCollection, IConfiguration)` that calls
  `AddDexpaceClient` and registers its client against the keyed services. There is **no generic typed-client overload**: it would need `ActivatorUtilities` with
  trimming annotations for a convenience that is additive later, and the first release locks every name (`NFR-4`).
- **The builder is a sealed class**, not an interface, so an adapter extends it with extension methods. `AddDexpaceClient` returns it instead of the collection (the
  `IHttpClientBuilder` precedent; `builder.Services` chains back).
- **One name registers once.** A second `AddDexpaceClient` with the same section instance (or both with none) returns the first builder; a different section throws
  `InvalidOperationException` naming the client. Names are case-sensitive: `GitHub` and `github` are two clients.
- **The section parameter is the client's own section, never the configuration root.** A root holds every process variable (`PATH`, `HOME`, `AllowedHosts`), which the
  unknown-key rule would reject, so the call throws `ArgumentException` with a pointed message. The package never reads a key the caller did not hand it: `SectionName`
  (`"Dexpace"`) is published in `DexpaceConfigurationKeys` and never read (`CFG-14`, `OBS-35`).
- **Dispose the provider with `DisposeAsync`.** `IAsyncHttpClient` is `IAsyncDisposable` and not necessarily `IDisposable`, so a synchronous `ServiceProvider.Dispose()` throws for
  an async-only transport. A host disposes asynchronously.
- Factories (`UseSerde(sp => ...)`, `UseTransport(sp => ...)`, `AddPolicy(sp => ...)`) run on the **root** provider: a scoped dependency in one fails under `ValidateScopes`.

## Configuration (`CFG-1` to `CFG-7`, `CFG-38`; P9-4, P9-6 to P9-9)

Tiers, lowest first: the record's initializer, the caller's `IConfiguration` providers (environment variables over `appsettings.{Environment}.json` over `appsettings.json` in
a default host), then the `Configure` / `ConfigureTransport` / `ConfigureCredentials` transforms in call order (`CFG-1`). The option records are immutable, so a transform
returns a modified copy (`o => o with { ... }`); one that returns `null` or throws fails validation naming the client.

**The package reads the section itself, not through the configuration binder.** The binder's source generator binds no `init` accessor and says nothing, and its reflection
mode is not AOT-safe and mis-converts durations, integers and enums (design V1). `ConfigurationBinder` is banned in `src/`.

Keys are relative to the section, case-insensitive and `:`-separated (`Dexpace__Retry__MaxRetryAttempts` as an environment variable, `CFG-3`):

| Key | Target | Grammar |
|---|---|---|
| `BaseAddress` | `DexpaceClientOptions.BaseAddress` | absolute `http`/`https` URI, no fragment |
| `UserAgent` | `.UserAgent` | printable ASCII header value |
| `OverallTimeout`, `AttemptTimeout` | `.OverallTimeout`, `.AttemptTimeout` | duration or `none` |
| `Retry:MaxRetryAttempts` | `RetryOptions.MaxRetryAttempts` | integer |
| `Retry:BaseDelay`, `Retry:MaxDelay` | `.BaseDelay`, `.MaxDelay` | duration |
| `Retry:FixedDelay` | `.FixedDelay` | duration or `none` |
| `Retry:Multiplier`, `Retry:Jitter` | `.Multiplier`, `.Jitter` | decimal (invariant, finite) |
| `Retry:HonorRetryAfter` | `.HonorRetryAfter` | boolean |
| `Retry:RetryableStatusCodes` | `.RetryableStatusCodes` | list of integers, or `none` |
| `Retry:AttemptHeaderName` | `.AttemptHeaderName` | header name; `Idempotency-Key`, in any case, fails (phase 11) |
| `Retry:PacingHeaders` | `.PacingHeaders` | list of the four pacing header names (`Retry-After`, `retry-after-ms`, `x-ms-retry-after-ms`, `X-RateLimit-Reset`), a subset in any order, no duplicate, not empty; replaces the default (phase 11) |
| `Redirect:MaxRedirects` | `RedirectOptions.MaxRedirects` | integer |
| `Redirect:AllowHttpsToHttpDowngrade`, `Redirect:FollowSeeOther` | the two flags | boolean |
| `Redirect:AllowedMethods` | `.AllowedMethods` | list of method tokens, or `none` |
| `Logging:Level` | `HttpLoggingOptions.Level` | `None`, `Headers` or `Body`, by name |
| `Logging:BodyPreviewSize` | `.BodyPreviewSize` | integer |
| `Logging:AllowedHeaderNames`, `Logging:AllowedQueryParameters`, `Logging:UrlValuedHeaderNames` | the three lists | list of strings, or `none` |
| `Logging:OmitDisallowedHeaders` | `.OmitDisallowedHeaders` | boolean |
| `Transport:Timeout` | `SystemNetHttpClientOptions.Timeout` | duration or `none` |
| `Transport:HeaderDropLogging` | `.HeaderDropLogging` | `FirstPerName`, `Every` or `VerboseOnly`, by name |
| `Transport:ResendBufferLimit` | `.ResendBufferLimit` | integer (64-bit) |
| `Transport:PooledConnectionLifetime` | the primary handler | duration or `none`; default 2 minutes |
| `Transport:Proxy:Type`, `Host`, `Port`, `NonProxyHosts`, `UserName`, `Password`, `BypassAll` | `ProxyOptions` | name; string; integer; list; string; string; boolean |
| `Auth:ApiKey:Key`, `HeaderName`, `Scheme` | `ApiKeyCredential` | strings |
| `Auth:Basic:Username`, `Password`; `Auth:Digest:Username`, `Password` | `BasicCredential`, `DigestCredential` | strings |
| `Auth:TokenRefreshMargin` | `AuthCredentials.TokenRefreshMargin` | duration |

Code-only, never read from configuration: `RedirectOptions.Predicate`, `SystemNetHttpClientOptions.Logger` and `TimeProvider` (the container supplies them),
`ProxyOptions.ChallengeCredentials` and `AuthCredentials.Token` (use `ConfigureCredentials`). A drift test fails the build when a record gains a member that is neither keyed nor code-only.

**Grammars** (`CFG-5` to `CFG-7`; P9-8, P9-27, P9-29). Every scalar is trimmed of ASCII whitespace first, and an empty value is then absent.

- *Integer*: `[+-]?[0-9]+` in ASCII, base 10 (`010` is 10). Hex, separators (`1_000`), exponents, fractions, non-ASCII digits and overflow fail. A negative is parsed and handed to the record, whose accessor decides.
- *Boolean*: `true` or `false`, in any case. `1`, `yes`, `on` and the rest fail.
- *Duration*: ISO-8601 day-time `P[nD][T[nH][nM][n[.f]S]]` (any case; `f` has at most seven digits, the tick), a number with a unit (`250ms`, `3s`, `2m`, `1h`, `1d`, a single space allowed), or digits alone as
  milliseconds. A sign, years, months, weeks, a bare fraction and anything above `TimeSpan.MaxValue` fail. This narrows `CFG-7`'s "ISO-8601" to the day-time subset (`XmlConvert` is not used: it reads
  `P5M` as 150 days, `-PT5S` as minus five seconds and truncates `PT0.00000001S` to zero).
- *Enum*: a name, in any case. A number or an undefined value fails and the failure lists the names (`OBS-35`).
- *List*: child keys (`Key:0`, `Key:1`, in the configuration's order) or one comma-separated value, never both; `none` is the empty list. A list **replaces** the record's default.

**An empty value is absent, and it falls through to the next provider** (`CFG-2`, design §10 entry 25, §11 item 68, P9-7, P11-17). *Corrected 2026-10-20 (phase 11): this page said an empty
environment value over a value in `appsettings.json` resolved the record default, silently, because `IConfiguration` merges its providers before the package sees a key.* When the `IConfiguration` registered
in the container is the root that holds the section handed to `AddDexpaceClient`, the reader walks that root's providers from the highest and takes the first value that is not empty or whitespace-only
(a `ChainedConfigurationProvider` is walked into, and a list item falls through the same way), so `FOO__RETRY__MAXRETRYATTEMPTS=` in the environment no longer hides the value `appsettings.json` holds, and
the record default applies only when no provider has a value. A section taken from a configuration that is **not** the registered root (a standalone `ConfigurationBuilder().Build()` handed to `AddDexpaceClient`, or
a section at the root with no path) keeps the merged read, where a blank value in the highest provider still hides the lower ones and the default applies: register the root you read the section from (`builder.Configuration.GetSection(...)`, the case a host produces). That case is a counted unmet clause: `IConfigurationSection` exposes no way to its root (`ConfigurationSection` keeps it private, and a reflection read is not AOT-safe), so the package cannot walk the providers of a configuration it was not
given; `docs/first-release.md` records it as `di-empty-shadows` (narrowed) and in *Unsatisfied MUSTs*, and `EmptyValueTests.A_section_from_an_unregistered_configuration_keeps_the_merged_read` pins it. The
package still reads the keys itself and never touches `ConfigurationBinder`. `EmptyValueTests.An_empty_value_falls_through_to_the_lower_provider` pins it.

**No failure quotes a configured string, URL or credential.** A failure reads `"<path>: <rule>"` (`Dexpace:GitHub:Retry:MaxRetryAttempts: not a base-10 integer ...`). An accessor's numeric range message may echo the
rejected number (`value ('-7') must be a non-negative value`); no numeric option carries a secret (P9-28).

**Unknown keys fail** the boot, naming the key in the configuration's own spelling. A misspelt key is a misconfiguration nobody otherwise sees; a generated SDK's own settings live outside the client's section. A value set on a group key (`Transport:Proxy`, `Auth`, `Retry`) fails too, as "a group of keys, not a value", without quoting it.

## Validation and timing (P9-9, P9-11; V4)

One validator per client reports every failure at once as an `OptionsValidationException`: the reader's, the records' accessor rejections, the unknown keys and the wiring checks (below). It runs at host start through
`ValidateOnStart`; with no host, `IStartupValidator.Validate()` runs it, and the first resolution of every keyed service the package builds (the transport included) fails the same way; an instance you install with `UseSerde` or `UseTransport` is returned as given and never consults the validated state. `ValidateOnBuild` does **not** run
options validation. A client with no codec fails both.

**There is no reload.** The snapshot is taken once, at first resolution; the package registers no change-token source, so a reloaded `appsettings.json` does not reach a built client. A built client is immutable (`CFG-8`),
and a pipeline rebuilt mid-flight would orphan the token cache and the transport's per-instance state.

**Combinations the records accept do not fail** (P9-10): `BaseDelay` above `MaxDelay` waits `MaxDelay`; an `AttemptTimeout` above `OverallTimeout` never fires; a `Transport:Timeout` below `AttemptTimeout` bounds the
attempt at the transport. Failing them only under DI would give one record two validities.

## The named client and its transport (`TRANSPORT-1`, `-15`; P9-12 to P9-14; V3, V8, V9)

Per client the package calls `AddHttpClient("Dexpace.<name>")` and builds the transport from `IHttpMessageHandlerFactory.CreateHandler`, with the handler constructor
`new SystemNetHttpClient(chain, options)`:

- the **primary** is `SystemNetHttpClient.CreatePrimaryHandler(options)`: `AllowAutoRedirect = false`, direct unless a proxy was asked for, server credentials unset. The handler constructor walks the chain to the
  primary and refuses one that follows redirects, so a caller who substitutes a default `SocketsHttpHandler` through `builder.HttpClient.ConfigurePrimaryHttpMessageHandler` fails at first resolution naming `AllowAutoRedirect`;
- **`RemoveAllLoggers()`**: the factory's logging handlers write the request URI with its path verbatim at `Information`, beside the SDK's own redacted `http.request`/`http.response` events (`OBS-11` to `OBS-18`). The SDK's events
  are the one HTTP log stream; add the factory's back on `builder.HttpClient` if you want them;
- **an infinite handler lifetime** with `Transport:PooledConnectionLifetime` (default 2 minutes): the transport is a singleton holding one chain for the process, so the factory's rotation could never reach it, and the pooled-connection
  lifetime does rotation's job (DNS) inside the one handler;
- **a container-owned lease** releases the primary the package created when the container is disposed: the factory never releases a handler whose lifetime is infinite, so without the lease the pooled connections outlive the container
  (`transport-15.owned-released` failed until it existed). The caller's `DelegatingHandler`s are the factory's and are never touched (`XCUT-22`);
- `builder.HttpClient.ConfigureHttpClient(...)` has no effect: the package uses the handler and never `CreateClient`. `BaseAddress` is `DexpaceClientOptions.BaseAddress`.

`ILoggerFactory` supplies the categories `Dexpace.Sdk` (pipeline) and `Dexpace.Sdk.Http.SystemNet` (transport); `TimeProvider` is `GetService<TimeProvider>() ?? TimeProvider.System`.

## Proxy (`CFG-28`, `CFG-11`; P9-15)

At most one source per client. Either the **explicit keys** `Transport:Proxy:*` (`Host` and `Port` are required when any is present), or **code**:

```csharp
builder.UseProxyFromEnvironment();                                  // HTTPS_PROXY, HTTP_PROXY, NO_PROXY from the process environment
builder.UseProxyFromEnvironment(key => configuration[key]);        // the same names, served from any configuration provider
```

Both are resolved when the transport is first resolved, never at registration. **No configuration value switches the environment read on**: a value that makes the package read the environment is the implicit read `CFG-28` forbids by another
route, and an IL scan test guards it. Two sources (including `ConfigureTransport` setting `Proxy`) fail validation.

## Authentication and the codec (P9-17, P9-18)

- **`UseSerde` is required.** The package has no default codec and does not reference one. With the System.Text.Json adapter:
  `UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(MyContext.Default)))` (the composition the 7a hand-off documents; a test pins it).
- `Auth:*` binds the redacting credentials. `UseAuth(descriptor)` installs a `MultiSchemeAuthPolicy` over the descriptor a generated SDK owns and those credentials; `UseAuthPolicy(sp => policy)` installs any other
  (a `ChallengeAuthPolicy`, your own); the two cannot be combined. `ConfigureCredentials((sp, credentials) => credentials with { Token = ... })` adds what configuration cannot hold. Credentials with neither
  fail validation: credentials that are silently not sent are the misconfiguration nobody sees. No failure message, `ToString` or log line contains a configured secret.

## One retry layer (`XCUT-10`, design §6.1, §11 item 24; P9-19; V6)

The package registers no handler besides the primary. To add the standard resilience handler without a second retry engine:

```csharp
services.AddDexpaceClient("github", section)
    .HttpClient.AddStandardResilienceHandler(o => o.Retry.ShouldHandle = _ => ValueTask.FromResult(false));
```

This keeps the rate limiter, the total and attempt timeouts and the circuit breaker and removes the handler's retry; the SDK's retry is the only one, so a request reaches the server `MaxRetryAttempts + 1` times at most
for a replayable body and once for a single-use one. Two spellings that look right are not:

- the **handler-side** `o.Retry.MaxRetryAttempts = 0` is refused by the handler's own options validation ("must be between 1 and 2147483647");
- the **SDK-side** `Retry:MaxRetryAttempts = 0` (the older guidance) leaves the handler's default retry in place, and that retry re-sends a `POST` body behind the SDK's re-send gate: measured, a `POST` answered `503` reached the
  server four times under the handler's defaults. That option is withdrawn.

`ResilienceCompositionTests` pins all three.

## Single registration (`SEAM-5`, `SEAM-6`, `SEAM-7`; P9-16; V4)

`UseTransport` and `UseSerde` take an instance (the container never disposes an instance it did not create, `XCUT-22`) or a factory (the container disposes what it returns), and replace the package's default. The same instance or
delegate again is a no-op; a different one **throws at the call**, naming the client and both implementations. At start the validator scans the collection for keyed `IAsyncHttpClient` and `ISerde` descriptors under the client's key,
because `GetRequiredKeyedService` silently resolves the last of several: more than one fails listing each implementation, and none fails with the install hint
`client 'github' has no ISerde: call UseSerde(...) on AddDexpaceClient("github")`. Every keyed registration is a singleton created once, whatever the concurrency (`SEAM-7`).

## What ships and what does not (P9-20)

The `CallKey` stamp (a carrier on `RequestOptions`, a stamp from the transport and a reader below it) is **not built**: phase 9 registers no `DelegatingHandler` that reads a call's context, so it has no reader. It is a post-release trigger in
[`first-release.md`](../first-release.md). The package carries one ordinary overhead the page does not hide: the handler construction is always re-send-capable, so a single-use body is teed into the bounded capture only when it is actually re-sent.
