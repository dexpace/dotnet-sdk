# 01 — Host & Configuration

The host is the one place the process composes itself: it reads configuration from layered, partly untrusted sources, wires the service graph, and builds the pipeline that every request runs through. This chapter is about doing that composition honestly — `Program.cs` stays thin and declarative, configuration binds into validated `record` options that fail the boot when wrong, and behaviour gates on the real environment rather than a hand-rolled flag. Get this right and a misconfigured deployment dies at startup with a named key; get it wrong and it dies at 3am on the first request that touches the missing setting.

## What good looks like

```csharp
var builder = WebApplication.CreateBuilder(args);

// Bind, then validate at boot: a bad value fails the host, not the first request (1.3, 1.4).
builder.Services
    .AddOptions<BillingOptions>()
    .Bind(builder.Configuration.GetSection("Billing"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddBilling();                       // one typed extension per feature (1.1)

var app = builder.Build();

// Detailed errors only in Development; production stays opaque (1.6).
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.MapBilling();                                    // pipeline order is load-bearing (1.8)
app.Run();
```

```csharp
public sealed record BillingOptions
{
    [Required, Url] public required string GatewayUrl { get; init; }
    [Range(1, 60)] public int TimeoutSeconds { get; init; } = 10;
}
```

`Program.cs` is composition only — configure, bind, build, map, run — with no business logic and an explicit order (1.1, 1.8). `BillingOptions` is an immutable `record` bound from a section and validated at startup, so a missing or malformed `GatewayUrl` fails the boot rather than the first charge (1.3, 1.4). Handler code reads `IOptions<BillingOptions>`, never `IConfiguration["Billing:GatewayUrl"]` (1.3, 1.5), and the dev-only exception page gates on `IHostEnvironment`, not a literal (1.6).

## Rules

### 1.1 — Compose the host in a thin `Program.cs`; put no business logic in it.

**Reasoning, step by step:**
1. `Program.cs` exists to assemble the application: create the `WebApplicationBuilder`, register services, build the `WebApplication`, lay out the middleware pipeline, and call `Run`. That is wiring, not behaviour. The moment a branch on domain state or a data-access call lands here, it is logic you cannot unit-test without booting the whole host, and it embodies ASP-1 broken at the root of the tree.
2. Keep the file declarative and use top-level statements — no ceremony `Main`, no partial `Startup` split. Group registrations behind one typed extension method per feature (`AddBilling`, `MapBilling`) so the file reads as a table of contents and each feature owns its own wiring in its own assembly (1.8, chapter [02](./02-dependency-injection.md)). What `Program.cs` does, it does in order and out loud; what it means, lives behind the extension methods.

**Worked example:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBilling().AddCatalog();          // each feature's wiring lives in its own extension
var app = builder.Build();
app.MapBilling().MapCatalog();                        // not a hundred inline MapGet calls here
app.Run();
```
**Enforcement:** review rejects domain logic in `Program.cs`; feature wiring lives in typed `IServiceCollection`/`IEndpointRouteBuilder` extensions.

### 1.2 — Layer configuration in a fixed precedence and keep secrets out of source.

**Reasoning, step by step:**
1. `CreateBuilder` establishes a deterministic precedence — `appsettings.json`, then `appsettings.{Environment}.json`, then environment variables, then command-line arguments — where each later source overrides the earlier. Rely on that order rather than re-adding providers in an ad-hoc sequence, because a surprise in precedence is a value that is right in one environment and silently wrong in another. The base file holds defaults; the environment file and the environment variables hold the per-deployment overrides.
2. A secret never enters source control. In development, secrets live in user-secrets (`dotnet user-secrets`), keyed by the same configuration paths so binding is identical; in production they come from the orchestrator's secret store or a vault (Azure Key Vault, a mounted secret), injected as environment variables or a registered provider. `appsettings.json` carries structure and non-secret defaults only — a connection string with a password in it is a leak the moment it is committed (see [security.md](../security.md)).

**Worked example:**
```json
{
  "Billing": { "TimeoutSeconds": 10 },
  "ConnectionStrings": { "Orders": "Host=localhost;Database=orders" }
}
```
**Enforcement:** secret scanning in CI; review rejects credentials in `appsettings*.json`; user-secrets in dev, vault/orchestrator in prod.

### 1.3 — Bind configuration to strongly-typed `record` options; never read raw `IConfiguration` indexing in domain code.

**Reasoning, step by step:**
1. `IConfiguration["Billing:TimeoutSeconds"]` returns a `string?` keyed by a stringly-typed path: a typo in the key compiles, a missing value returns `null`, and every reader re-parses the same raw text into the same `int` with the same chance of disagreeing. Bind the section once into an immutable `record` with `init` properties and typed members (chapter [03](../csharp/03-nullability-and-the-type-system.md)), and the configuration becomes a domain value that the type system checks — embodying ASP-2, configuration is a boundary that gets parsed.
2. Domain and handler code then depends on the options type through DI (1.5), not on `IConfiguration`. The raw configuration object is a host concern that stays in `Program.cs` and the binding call; the interior never sees a magic string key. One bind, one source of truth, one place a default lives — the `init` property's initializer — instead of a `?? "10"` fallback scattered across every read site.

**Worked example:**
```csharp
builder.Services.AddOptions<BillingOptions>().Bind(builder.Configuration.GetSection("Billing"));
// domain code, later:
public sealed class BillingClient(IOptions<BillingOptions> options)   // typed, not IConfiguration
{
    private readonly BillingOptions _options = options.Value;
}
```
**Enforcement:** review rejects `IConfiguration` string indexing outside `Program.cs`; the Options pattern is the only sanctioned reader.

### 1.4 — Validate options at startup with `ValidateOnStart` so misconfiguration fails the boot.

**Reasoning, step by step:**
1. Binding is shape-mapping, not validation: a missing `GatewayUrl` binds to `null`, a negative timeout binds to a negative `int`, and both sail through until the first request dereferences them and throws somewhere far from the cause. The honest place to catch a bad setting is the boot, where the deployment is still rolling and an orchestrator will hold back traffic from an instance that failed its readiness — this embodies ASP-2, every boundary parsed, applied to configuration.
2. Chain `ValidateDataAnnotations` (or `Validate(...)` for cross-field rules) and `ValidateOnStart` onto the registration so the container eagerly resolves and validates the options before the server accepts connections. The failure message names the offending key and constraint, so the operator reading the crash log fixes the right line of the right file. A startup that succeeds is then a promise that every required setting is present and well-formed.

**Worked example:**
```csharp
builder.Services
    .AddOptions<BillingOptions>()
    .Bind(builder.Configuration.GetSection("Billing"))
    .ValidateDataAnnotations()
    .Validate(o => o.TimeoutSeconds <= 60, "Billing:TimeoutSeconds must be <= 60")
    .ValidateOnStart();                               // bad config crashes the boot, naming the key
```
**Enforcement:** `ValidateOnStart` required on every bound options type; review checks each `AddOptions<T>` chains validation.

### 1.5 — Choose the options accessor by lifetime: `IOptions`, `IOptionsSnapshot`, or `IOptionsMonitor`.

**Reasoning, step by step:**
1. The three accessors differ in when they read and in what lifetime they live, and picking the wrong one is a subtle bug. `IOptions<T>` is a singleton computed once at first resolution — correct for static configuration that never changes after boot, and safe to inject anywhere. `IOptionsSnapshot<T>` is scoped, recomputed once per request, so it picks up a reloaded `appsettings.json` on the next request — correct for values an operator may retune live. `IOptionsMonitor<T>` is a singleton that exposes the current value and a change callback — the only correct choice for change-reactive code inside a singleton.
2. The lifetime trap is injecting the scoped `IOptionsSnapshot<T>` into a singleton: the container either refuses it (with scope validation on, 2.5) or captures the first request's snapshot forever (a captive dependency, chapter [02](./02-dependency-injection.md)). A singleton that must react to configuration changes uses `IOptionsMonitor<T>` and reads `.CurrentValue` per use, or subscribes via `OnChange`; it never holds a scoped accessor. State the reason for the chosen accessor where it is injected.

**Worked example:**
```csharp
// Singleton client reacting to live config: monitor, read CurrentValue per call (never snapshot).
public sealed class GatewayClient(IOptionsMonitor<BillingOptions> options)
{
    public Uri Endpoint => new(options.CurrentValue.GatewayUrl);
}
```
**Enforcement:** review of accessor choice against the consumer's lifetime; scope validation (2.5) catches a scoped accessor in a singleton.

### 1.6 — Gate behaviour on `IHostEnvironment`, never on a hand-rolled flag; detailed errors only in Development.

**Reasoning, step by step:**
1. The framework already knows the environment from `ASPNETCORE_ENVIRONMENT`, and exposes it as `IHostEnvironment` with `IsDevelopment`, `IsStaging`, and `IsProduction`. A hand-rolled `bool isDev` read from configuration is a second source of truth that drifts from the first — set wrong in one place and a developer convenience ships to production. Gate every environment-conditional behaviour on the framework's environment so there is exactly one answer to "where am I running."
2. The most dangerous thing to leak is a detailed error: the developer exception page and verbose stack traces expose internals and aid an attacker, so they are mounted only under `IsDevelopment`. Production returns an opaque `ProblemDetails` (chapter [03](./03-minimal-apis-and-endpoints.md)) and logs the detail server-side. The default posture is production-safe; Development is the explicit opt-in to verbosity, never the other way round (see [security.md](../security.md)).

**Worked example:**
```csharp
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();                  // verbose page is dev-only
else
    app.UseExceptionHandler();                        // production returns opaque ProblemDetails
```
**Enforcement:** review rejects a configuration-driven environment flag in place of `IHostEnvironment`; detailed-error middleware must be guarded by `IsDevelopment`.

### 1.7 — Fail fast on a missing required setting: throw at startup with a message naming the key.

**Reasoning, step by step:**
1. A required setting that is absent is a deployment that cannot work, so the only question is whether it fails now, loudly, or later, obscurely. Guard the requirement at startup — `ValidateOnStart` for bound options (1.4), an explicit guard for a value read directly during composition — so the host throws before it ever accepts a request. This is the guard-clause discipline of core [05](../csharp/05-methods-and-functions.md) applied to the host's own preconditions, embodying ASP-2.
2. The exception message names the configuration key and what was expected, because the person reading it is an operator looking at a crash log, not the author. `Configuration value 'ConnectionStrings:Orders' is required` tells them exactly which line of which file to fix; a bare `NullReferenceException` from somewhere downstream tells them nothing. Never substitute a silent default for a required secret or endpoint — a default connection string that points at localhost in production is worse than a crash.

**Worked example:**
```csharp
var connection = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException(
        "Configuration value 'ConnectionStrings:Orders' is required.");   // names the key, fails the boot
```
**Enforcement:** review requires a named-key failure for each required setting; guard clauses follow core [05](../csharp/05-methods-and-functions.md); `ValidateOnStart` for bound options.

### 1.8 — Keep `Program.cs` ordering explicit: configure, register, build, pipeline, run.

**Reasoning, step by step:**
1. The host has two distinct phases split by `builder.Build()`: before it, you configure the builder and register services into the collection; after it, the collection is frozen and you compose the request pipeline on the built `app`. Registering a service after `Build` throws, and ordering the middleware wrong is silent — authentication after authorization, exception handling after the endpoint, CORS in the wrong place — so the pipeline order is load-bearing and must read in execution order, top to bottom.
2. Keep the structure the same in every service so a reader navigates by position: configuration and options first, service registrations second, `Build` third, then the pipeline (exception handling outermost, then routing, auth, custom middleware), then endpoint mapping, then `Run`. This mirrors ASP-6 — startup composes outward and the ordering is explicit, never implicit in a static constructor or an import side effect. A comment marking each phase costs nothing and saves the next reader from guessing where the seam is.

**Worked example:**
```csharp
var builder = WebApplication.CreateBuilder(args);    // 1. configuration + options
builder.Services.AddBilling();                        // 2. service registration
var app = builder.Build();                            // 3. build (collection frozen)
app.UseExceptionHandler();                             // 4. pipeline, in execution order
app.UseAuthentication();
app.UseAuthorization();
app.MapBilling();                                     // 5. endpoints
app.Run();                                            // 6. run
```
**Enforcement:** review checks the phase ordering and middleware sequence; registering after `Build` is a runtime throw the build surfaces.

## Cross-references

- Binding configuration into immutable `record` options and the typed boundary: [03-nullability-and-the-type-system.md](../csharp/03-nullability-and-the-type-system.md). Guard clauses and fail-fast on a missing required value: [05-methods-and-functions.md](../csharp/05-methods-and-functions.md).
- Typed feature-wiring extensions, lifetimes, and the captive-dependency trap behind the options accessors: [02-dependency-injection.md](./02-dependency-injection.md).
- Secrets out of source, vault/orchestrator injection, and dev-only error verbosity: [security.md](../security.md).
