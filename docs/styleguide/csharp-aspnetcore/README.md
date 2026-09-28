## ASP.NET Core on .NET

Binding style rules for **server-side C# on ASP.NET Core**, target **.NET 10 (LTS)** and **ASP.NET Core 10**. This guide *extends* [csharp/](../csharp/) for the web and service runtime. It is additive: where this guide is stricter, it wins for ASP.NET Core code; the core guide remains the canonical language baseline.

The core guide is platform-agnostic. ASP.NET Core has its own contract — a built-in dependency-injection container with request-scoped lifetimes, a middleware pipeline, a configuration system that layers untrusted sources, EF Core's change tracker, and a host that an orchestrator can restart. Those impose rules the language guide cannot address.

### Authorities

This guide extends and defers to an ordered chain. Where they conflict, the higher authority wins:

1. **[ASP.NET Core documentation](https://learn.microsoft.com/en-us/aspnet/core/)** + **[.NET Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)** — canonical. The hosting model, the DI container semantics, the configuration and options contracts, EF Core behaviour. Where our guidance collides with documented framework behaviour, the framework wins.
2. **[dotnet/aspnetcore](https://github.com/dotnet/aspnetcore) repo conventions + production canon** — the practices the platform team and the field have settled on (minimal APIs, `TypedResults`, source-generated logging and JSON). They **fill the gaps** the docs leave open on production operation: error policy, observability, deployment. Defer to them only where the docs are silent.
3. **This guide's overlay** — the dexpace ASP.NET layer: thin endpoints over a transformation core, every boundary parsed, explicit DI lifetimes with no captive dependencies, request-scoped correlation, named lifecycles, and crash-clean restartability.

The core [csharp/](../csharp/) authority chain (.NET Runtime style → MS Learn conventions → analyzers → its overlay) still governs the *language* layer. This guide adds the runtime layer on top; it does not replace it.

### When ASP.NET Core, and when not

ASP.NET Core is the dexpace default web and service stack. Two facts shape this guide. First, **minimal APIs are the default** — endpoints are functions wired in `Program.cs`, not MVC controllers, and controllers are the exception reserved for surfaces that genuinely need the MVC filter pipeline or model-binding machinery. Second, **the host is supervised** — the process pins its SDK (core [1.6](../csharp/01-formatting-and-tooling.md)), starts and stops through named lifecycle phases, and an orchestrator (Kubernetes, systemd) owns liveness; a clean restart is a design requirement, not an afterthought.

---

## Table of Contents

| # | Document | Scope |
|---|----------|-------|
| 01 | [Host & Configuration](./01-host-and-configuration.md) | `WebApplicationBuilder`, layered `IConfiguration`, the Options pattern with `ValidateOnStart`, `IOptions`/`IOptionsSnapshot`/`IOptionsMonitor`, environments, secrets out of source |
| 02 | [Dependency Injection](./02-dependency-injection.md) | Constructor injection, explicit lifetimes, no captive dependencies, keyed services, register the interface, no service-locator, `ValidateScopes`/`ValidateOnBuild` |
| 03 | [Minimal APIs & Endpoints](./03-minimal-apis-and-endpoints.md) | Minimal APIs over controllers, route groups, `TypedResults`, endpoint filters for cross-cutting, parse-at-boundary, `ProblemDetails`, versioning, OpenAPI |
| 04 | [Persistence with EF Core](./04-persistence-ef-core.md) | Scoped `DbContext` (or `DbContextPool`), `AsNoTracking` reads, no lazy loading, explicit `Include`, project to DTOs, parameterized queries, migrations, transactions as units, Dapper for hot paths |
| 05 | [Serialization & Validation](./05-serialization-and-validation.md) | `System.Text.Json` + source generation, no Newtonsoft, options configured once, parse into records at the edge, `ProblemDetails` errors, null-vs-absent, never serialize EF entities |
| 06 | [Logging & Observability](./06-logging-and-observability.md) | `ILogger<T>` structured logging, `LoggerMessage` source-gen on hot paths, OpenTelemetry traces/metrics, health checks, correlation via `Activity`, PII redaction, no `Console.WriteLine` |
| 07 | [ASP.NET Performance](./07-aspnet-performance.md) | Kestrel limits, output caching + `HybridCache`, response compression, `IHttpClientFactory` pooling, rate limiting, allocation-free middleware, async throughout, Server GC, streaming |
| 08 | [Build & Deployment](./08-build-and-deployment.md) | Chiseled/distroless containers, Native AOT or ReadyToRun, trimming, health/readiness probes, env-based config, graceful shutdown via `IHostApplicationLifetime`, locked-restore CI |

---

## Runtime Substitutions

ASP.NET Core replaces parts of the core family's tooling for web code. Each row records a core rule, the ASP.NET substitution, and the intent preserved unchanged. The *intent* is the contract; the tool is the implementation.

| Core rule | ASP.NET substitution | Preserved intent |
|---|---|---|
| Fakes over mocks, integration over in-memory (core [11.7](../csharp/11-testing.md)) | `WebApplicationFactory<TEntryPoint>` + Testcontainers | Exercise the real host and real dependencies; substitutes that lie about behaviour are banned |
| Parse at the boundary (core [03.2](../csharp/03-nullability-and-the-type-system.md)) | `System.Text.Json` source-gen + endpoint-level parsing into records (chapter [05](./05-serialization-and-validation.md)) | Untrusted input proven into a domain type before any handler logic runs |
| `ConfigureAwait(false)` in libraries (core [09.4](../csharp/09-concurrency.md)) | Omitted in app/host code — no `SynchronizationContext` to capture | The rule is library-only; ASP.NET request code does not need it |

`IHttpClientFactory` over `new HttpClient()` is a core rule (core [13.8](../csharp/13-resource-management.md)), recorded there, not a substitution here.

---

## The ASP rules

These add to [the 12 rules in the core guide](../csharp/README.md#the-12-rules-in-c). Each extends one core rule to the web runtime's edges. When an ASP rule conflicts with a core one, the ASP rule wins for ASP.NET Core code.

### ASP-1. Endpoints are thin; the work lives in the core.

**Step-by-step reasoning:**
1. An endpoint's only jobs are to parse the request into a domain type, call one application service, and map the result to an HTTP response. Business logic in a handler is logic you cannot unit-test without spinning up the pipeline.
2. The handler therefore reads top to bottom as parse, dispatch, map — no branching on domain state, no data access, no orchestration. Those live behind an injected service interface (chapter [03](./03-minimal-apis-and-endpoints.md)).
3. This keeps the transformation core (root rule 6) free of `HttpContext`, so it is testable in isolation and reusable from a background worker or a different transport.
4. This extends [core 10](../csharp/10-api-design.md): the HTTP surface is just another API boundary, kept minimal and honest.

### ASP-2. Every boundary is parsed.

**Step-by-step reasoning:**
1. Everything crossing into the application is untrusted until proven: route and query values, request bodies, headers, configuration, and the rows a query returns. A model-bound object is shape-checked, not domain-valid.
2. Each boundary value is parsed into a validated domain type — a `record`, a branded primitive (core [03.7](../csharp/03-nullability-and-the-type-system.md)) — at the edge, before any handler logic runs, and invalid input is rejected with `ProblemDetails` (chapter [05](./05-serialization-and-validation.md)).
3. The domain type, not the DTO, flows inward; the DTO never reaches the core. One parse, one source of truth, no re-validation scattered downstream.
4. This extends [core 03.2](../csharp/03-nullability-and-the-type-system.md), parse at the boundary, from one API surface to every edge the host touches, configuration included.

### ASP-3. DI lifetimes are explicit and conflict-free.

**Step-by-step reasoning:**
1. The container resolves a graph, and a lifetime mismatch corrupts it silently: a `Scoped` service captured by a `Singleton` (a captive dependency) outlives its scope and leaks request state across requests.
2. Every registration states its lifetime deliberately — `Singleton` for stateless or thread-safe shared services, `Scoped` for per-request units like a `DbContext`, `Transient` for cheap stateless helpers — and the reasoning is reviewable at the registration site (chapter [02](./02-dependency-injection.md)).
3. The container validates the graph at build: `ValidateScopes` and `ValidateOnBuild` are on in every environment, turning a captive dependency into a startup failure rather than a 3am data-leak incident.
4. This extends root rule 2, explicit over implicit — every dependency is a constructor parameter, every lifetime a typed choice — and [core 09](../csharp/09-concurrency.md) on shared mutable state.

### ASP-4. The request path does no blocking and no sync-over-async.

**Step-by-step reasoning:**
1. A thread blocked on I/O is a thread removed from the pool that serves other requests; enough of them and the server thread-starves and stops responding under exactly the load you built it for.
2. So the request path is async end to end (core [09.1](../csharp/09-concurrency.md)): no `.Result`, no `.Wait()`, no synchronous file or network I/O, and `AllowSynchronousIO` stays off. Every async call carries the request's `CancellationToken` so a client disconnect cancels the work it abandoned.
3. CPU-bound work in a handler is bounded and, when heavy, moved off the request path to a background queue or worker; the request thread stays free to dispatch.
4. This extends [core 09](../csharp/09-concurrency.md), async correctness, applied to the runtime's defining constraint — a shared, finite thread pool.

### ASP-5. Observability is structured and correlated.

**Step-by-step reasoning:**
1. A production request you cannot trace is a request you cannot debug. Logs that interpolate values into message strings are unsearchable, and a log line with no correlation id is an orphan.
2. Logging is structured (`ILogger<T>` with named placeholders, source-generated `LoggerMessage` on hot paths), traces and metrics flow through OpenTelemetry, and every log and span carries the request's correlation id from `Activity` (chapter [06](./06-logging-and-observability.md)).
3. Health is honest: a readiness probe checks the dependencies the instance actually needs, so the orchestrator routes traffic only to instances that can serve it. Secrets and PII are redacted before they reach a sink (see [security.md](../security.md)).
4. This extends [core 07](../csharp/07-csharp-idioms.md) and [core 14](../csharp/14-documentation.md), say why — at runtime the "why" is the trace, and it must be there before the incident, not added after.

### ASP-6. Named lifecycles and clean shutdown.

**Step-by-step reasoning:**
1. Startup and shutdown are explicit and ordered, never implicit in static constructors or import side effects. The host composes configuration, then clients and pools, then the server that depends on them.
2. Shutdown is the reverse, triggered by `SIGTERM` through `IHostApplicationLifetime`: stop accepting connections, drain in-flight requests, dispose pools and consumers, then exit — within a bounded deadline, after which it force-exits rather than hang.
3. Every resource (connection pool, `HttpClient` via the factory, channel, hosted service) binds its open and close to these phases; a resource with no owning phase is a leak waiting for load (core [13](../csharp/13-resource-management.md)).
4. Verify it: send `SIGTERM` and confirm clean drain logs. This extends [core 13](../csharp/13-resource-management.md), resource management, to the process scope.

---

Zero technical debt holds here as everywhere: what ships meets the design goals. **Perfection over technical debt — debt never gets paid.** A service runs unattended for months, and the shortcut taken today is the page at 3am later.

## Influences

- **[ASP.NET Core documentation](https://learn.microsoft.com/en-us/aspnet/core/)** — canonical hosting, DI, configuration, and middleware reference.
- **[.NET Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)** — public-API and library shape.
- **[EF Core documentation](https://learn.microsoft.com/en-us/ef/core/)** — change-tracking, query, and migration behaviour.
- **[OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)** — traces, metrics, and correlation.
- **TigerBeetle Tiger Style** — limits on everything, bounded resources, clean restart, zero technical debt.
