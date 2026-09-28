# 02 — Dependency Injection

The built-in container resolves the service graph and owns the lifetime of everything in it, so the discipline here is about making that graph honest and conflict-free — this chapter embodies ASP-3. Every dependency arrives through a constructor parameter where the compiler and the reader can both see it, every registration states its lifetime as a deliberate choice, and the container validates the whole graph at build so a captive dependency is a startup crash rather than a request-state leak in production. Wiring that is cohesive, explicit, and validated is wiring you can reason about; the alternative is a service locator and a 3am incident.

## What good looks like

```csharp
public static class BillingServiceCollectionExtensions
{
    // One typed extension per feature: cohesive, testable, the only wiring surface (2.7, 2.2).
    public static IServiceCollection AddBilling(this IServiceCollection services)
    {
        services.AddScoped<OrderStore, SqlOrderStore>();          // per-request unit, register the role (2.2, 2.3)
        services.AddSingleton<RateTable, StaticRateTable>();      // stateless, thread-safe, shared (2.3)
        services.AddKeyedSingleton<Gateway, StripeGateway>("stripe");   // disambiguate by key (2.6)
        return services;
    }
}

// Constructor injection only — every dependency is visible in the signature (2.1).
public sealed class ChargeService(OrderStore orders, [FromKeyedServices("stripe")] Gateway gateway)
{
    public async Task<Receipt> Charge(OrderId id, CancellationToken cancellationToken)
    {
        var order = await orders.Find(id, cancellationToken);
        return await gateway.Charge(order, cancellationToken);
    }
}
```

`AddBilling` is the single cohesive wiring surface for the feature (2.7), registering each service against its role interface with a deliberate, reviewable lifetime — `Scoped` for the per-request `OrderStore`, `Singleton` for the stateless `RateTable` (2.2, 2.3). `ChargeService` takes every dependency as a constructor parameter, so the graph is visible in the signature with no `IServiceProvider` pull (2.1), and the two gateways are disambiguated by key rather than a factory switch (2.6). Scope and build validation (2.5) turns any captive dependency in this graph into a startup failure.

## Rules

### 2.1 — Use constructor injection only; no property injection and no service-locator pulls.

**Reasoning, step by step:**
1. A constructor parameter is the honest declaration of a dependency: it is visible in the signature, the compiler refuses to build the type without it, and a test constructs the class with explicit fakes (core [11](../csharp/11-testing.md)). Property injection hides the dependency behind a settable member that may be unset when a method runs, and a service-locator call — resolving `IServiceProvider.GetService<T>()` inside a method — hides it entirely, so the type lies about what it needs and the failure surfaces at runtime instead of at construction.
2. Domain and handler code therefore never sees `IServiceProvider`. Injecting the provider to pull services on demand defeats the graph the container built, makes lifetimes unverifiable (the container cannot see a runtime `GetService` call), and turns a compile-time dependency into a runtime lookup that throws when the registration is missing. The provider is a host-internal concern; the only sanctioned dynamic resolution is an explicit factory abstraction the feature owns, injected as a constructor parameter like anything else.

**Worked example:**
```csharp
public sealed class Reconciler(OrderStore orders, Clock clock) { /* ... */ }   // dependencies visible
public sealed class ReconcilerBad(IServiceProvider provider)                   // bad — hidden, unverifiable
{
    private OrderStore Orders => provider.GetRequiredService<OrderStore>();     // service locator
}
```
**Enforcement:** review rejects property injection and `IServiceProvider` in domain code; constructor primary parameters are the only injection point.

### 2.2 — Register against the role interface, not the concrete type.

**Reasoning, step by step:**
1. The point of the container is to let a consumer depend on what a service does, not on which class does it, so register the abstraction mapped to the implementation: `services.AddScoped<OrderStore, SqlOrderStore>()`. The consumer takes `OrderStore` in its constructor and never names `SqlOrderStore`, so swapping the SQL implementation for an in-memory one in a test, or a cached decorator in production, is a one-line registration change with no consumer touched (core [10](../csharp/10-api-design.md)).
2. The role interface carries no `I` prefix — it is `OrderStore`, not `IOrderStore` — because it is a first-party abstraction named for its role, while framework interfaces (`ILogger`, `IConfiguration`) keep theirs. Register the concrete type directly only for a leaf with no abstraction worth having (a concrete options-backed client used in one place); the default is program-to-the-interface, and a consumer depending on a concrete service class is a review finding.

**Worked example:**
```csharp
services.AddScoped<OrderStore, SqlOrderStore>();      // depend on the role, swap the implementation freely
// consumer:
public sealed class Ledger(OrderStore orders) { /* never names SqlOrderStore */ }
```
**Enforcement:** review requires registration against the role interface (no `I` prefix on first-party roles); consumers depend on the abstraction.

### 2.3 — State each lifetime deliberately: Singleton, Scoped, or Transient.

**Reasoning, step by step:**
1. The three lifetimes encode how an instance is shared, and the choice is a correctness decision, not a default to accept. `Singleton` is one instance for the process — correct only for a stateless or genuinely thread-safe shared service, because every request touches it concurrently (core [09](../csharp/09-concurrency.md)). `Scoped` is one instance per request — correct for a per-request unit like a `DbContext` that must not be shared across requests (chapter [04](./04-persistence-ef-core.md) and EF Core). `Transient` is a fresh instance per resolution — correct for a cheap, stateless helper that holds no state worth reusing.
2. Make the reasoning reviewable at the registration site: the lifetime is right there next to the type, so a reviewer reads `AddSingleton<RateTable, StaticRateTable>()` and checks that `StaticRateTable` is in fact thread-safe and stateless. A `Singleton` that holds mutable per-request state is a data leak across requests; a `Transient` that is expensive to build is a performance leak. The deliberate choice, visible where it is made, is what ASP-3 asks for.

**Worked example:**
```csharp
services.AddSingleton<RateTable, StaticRateTable>();  // stateless + thread-safe → shared for the process
services.AddScoped<OrderStore, SqlOrderStore>();      // per-request unit → one per request
services.AddTransient<ReceiptFormatter>();            // cheap + stateless → fresh each time
```
**Enforcement:** review of each lifetime against the service's state and thread-safety; a mutable `Singleton` is a finding.

### 2.4 — Never create a captive dependency: a Singleton must not capture a Scoped or Transient.

**Reasoning, step by step:**
1. A `Singleton` is constructed once and lives for the process, so any service it takes in its constructor is captured for the process too. Hand a `Singleton` a `Scoped` dependency and that scoped instance — built for one request — never gets released; it serves every subsequent request from inside the singleton, leaking the first request's state across all of them. A captured `Transient` is the same trap: the "fresh each time" promise is broken because the singleton holds one instance forever.
2. The fix is to not capture across lifetimes. A singleton that needs a scoped service resolves it per use from an injected scope factory (`IServiceScopeFactory`, creating a scope and disposing it around the work), or the would-be singleton is itself made scoped. Reading configuration is the common case: a singleton uses `IOptionsMonitor<T>`, not the scoped `IOptionsSnapshot<T>` (chapter [01](./01-host-and-configuration.md)). The lifetimes line up or the work moves into a scope — there is no third option that is correct.

**Worked example:**
```csharp
public sealed class CacheWarmer(IServiceScopeFactory scopes)   // singleton resolves scoped work per use
{
    public async Task Warm(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<OrderStore>();  // scoped, released with the scope
    }
}
```
**Enforcement:** scope validation (2.5) turns a captive dependency into a startup throw; review checks singleton constructors for scoped/transient parameters.

### 2.5 — Turn on `ValidateScopes` and `ValidateOnBuild` in every environment.

**Reasoning, step by step:**
1. The container can prove two things at build time: that the graph is fully resolvable (`ValidateOnBuild` eagerly walks every registration and throws on a missing or ambiguous dependency) and that no captive dependency exists (`ValidateScopes` rejects a scoped service resolved from the root provider). With these off, a broken graph compiles and boots and fails on the request that first touches the bad edge — which in production is a captive-dependency data leak, not a clean error.
2. Both validations are on in *every* environment, not just Development, because the graph you validate must be the graph you ship: a registration that differs by environment can be sound in dev and captive in prod, and the only place that matters is prod. `CreateBuilder` enables scope validation in Development by default; force both on everywhere so a bad graph is a deterministic startup failure on every deploy, caught by the readiness probe before traffic arrives (ASP-3, ASP-6).

**Worked example:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider((_, options) =>
{
    options.ValidateScopes = true;                    // captive dependency → startup throw
    options.ValidateOnBuild = true;                   // unresolvable graph → startup throw
});                                                   // on in every environment, not just Development
```
**Enforcement:** `ValidateScopes` and `ValidateOnBuild` required on in all environments; review checks the provider configuration.

### 2.6 — Disambiguate multiple implementations with keyed services, not a factory switch.

**Reasoning, step by step:**
1. When one role has several implementations chosen at runtime — a `Gateway` per payment provider, a `Store` per tenant tier — the wrong answer is a factory that injects `IServiceProvider` and switches on a string, because that hides the registrations from the container and reintroduces the service locator 2.1 bans. Keyed services solve it natively: `AddKeyedSingleton<Gateway, StripeGateway>("stripe")` registers each implementation under a key the container knows about and can validate.
2. The consumer requests the specific one with `[FromKeyedServices("stripe")]` on a constructor parameter, so the dependency stays visible in the signature and the choice is a typed, reviewable key rather than a runtime string lookup that throws when it misses. When the key itself is data — a tenant id resolved per request — inject the keyed-service resolver abstraction the feature owns, still a constructor parameter, still validated. Keyed services keep the disambiguation inside the graph the container builds and checks.

**Worked example:**
```csharp
services.AddKeyedSingleton<Gateway, StripeGateway>("stripe");
services.AddKeyedSingleton<Gateway, AdyenGateway>("adyen");
public sealed class Checkout([FromKeyedServices("stripe")] Gateway gateway) { /* typed key, no switch */ }
```
**Enforcement:** review rejects a service-locator factory switch where keyed services apply; keyed registrations stay in the validated graph (2.5).

### 2.7 — Register a feature's services with one typed extension method per assembly.

**Reasoning, step by step:**
1. Scattering a feature's registrations across `Program.cs` makes the wiring incohesive — no one place tells you what `Billing` needs — and couples the host to every implementation detail. Collect them into one extension on `IServiceCollection`, `services.AddBilling(...)`, that lives in the feature's own assembly next to the code it wires. The host then composes features as a short list of `AddBilling().AddCatalog()` calls (chapter [01](./01-host-and-configuration.md), 1.1), and each feature owns and tests its own graph in isolation.
2. The extension is typed and self-contained: it takes whatever it needs (often `IConfiguration` to bind its options, 1.3) and returns the collection so calls chain. This makes the wiring a unit you can assert on in a test — register the feature into a fresh collection, build the provider with validation, and confirm the graph resolves — and keeps a feature's lifetimes and keyed registrations reviewed together rather than buried among unrelated lines in the host.

**Worked example:**
```csharp
public static IServiceCollection AddBilling(this IServiceCollection services, IConfiguration config)
{
    services.AddOptions<BillingOptions>().Bind(config.GetSection("Billing")).ValidateOnStart();
    services.AddScoped<OrderStore, SqlOrderStore>();
    return services;                                  // chainable, cohesive, testable in isolation
}
```
**Enforcement:** review requires per-feature registration extensions; `Program.cs` composes features, it does not register their internals.

### 2.8 — Do not dispose injected dependencies; let the container own what it created.

**Reasoning, step by step:**
1. The container tracks the lifetime of every instance it creates and disposes the `IDisposable`/`IAsyncDisposable` ones at the end of their scope — a scoped service when the request scope ends, a singleton when the host stops. Disposing an injected dependency yourself disposes it out from under the container and every other consumer still holding it, and the second disposal at scope end is a double-dispose. The rule from core [13](../csharp/13-resource-management.md) holds: you dispose what *you* create, and you did not create an injected dependency.
2. To get correct disposal, register correctly rather than disposing manually. A service that holds an unmanaged or pooled resource is registered so the container owns it — `AddSingleton<T>` for a process-lifetime resource, a registered factory for one whose disposal the container should drive — and the container disposes it on shutdown (ASP-6). An instance you genuinely own because *you* `new`ed it (rare in handler code) follows the `using` discipline of core [13](../csharp/13-resource-management.md); an injected one never gets a `using` and never a manual `Dispose`.

**Worked example:**
```csharp
public sealed class Exporter(OrderStore orders) : IDisposable      // orders is injected — do NOT dispose it
{
    private readonly FileStream _output = File.Create("export.tmp");   // this, you own
    public void Dispose() => _output.Dispose();                    // dispose only what you created
}
```
**Enforcement:** `CA2000` scoped to owned resources; review rejects `Dispose`/`using` on an injected dependency; container-owned resources registered for container disposal.

## Cross-references

- Shared-state and thread-safety reasoning behind Singleton, and async correctness in resolved services: [09-concurrency.md](../csharp/09-concurrency.md).
- Ownership, `using`, and "dispose only what you create" that 2.8 extends to the container: [13-resource-management.md](../csharp/13-resource-management.md).
- Feature-wiring extensions composed in the host, options accessors, and the captive-dependency trap: [01-host-and-configuration.md](./01-host-and-configuration.md).
- Scoped `DbContext` and the per-request services these lifetimes serve: [03-minimal-apis-and-endpoints.md](./03-minimal-apis-and-endpoints.md).
