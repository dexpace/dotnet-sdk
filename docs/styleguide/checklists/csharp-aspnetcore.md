# ASP.NET Core styleguide — full checklist

Additive over `csharp-styleguide`. Where stricter, this layer wins for ASP.NET Core. Walk every chapter on a full audit.

### 01 — Host & Configuration

- Compose the host in a thin `Program.cs`; no business logic there.
- Layer configuration in fixed precedence; keep secrets out of source.
- Bind to strongly-typed `record` options; never index raw `IConfiguration` in domain code.
- Validate options at startup with `ValidateOnStart`; misconfiguration fails the boot.
- Pick the options accessor by lifetime: `IOptions`, `IOptionsSnapshot`, or `IOptionsMonitor`.
- Gate behaviour on `IHostEnvironment`, never a hand-rolled flag; detailed errors in Development only.
- Fail fast on a missing required setting: throw at startup naming the key.
- Keep `Program.cs` ordering explicit: configure, register, build, pipeline, run.

### 02 — Dependency Injection

- Constructor injection only; no property injection, no service-locator pulls.
- Register against the role interface, not the concrete type.
- State each lifetime deliberately: Singleton, Scoped, or Transient.
- Never create a captive dependency: a Singleton must not capture a Scoped or Transient.
- Keep `ValidateScopes` and `ValidateOnBuild` on in every environment.
- Disambiguate multiple implementations with keyed services, not a factory switch.
- Register a feature's services with one typed extension method per assembly.
- Do not dispose injected dependencies; the container owns what it created.

### 03 — Minimal APIs & Endpoints

- Default to minimal APIs; use an MVC controller only when the surface needs the filter pipeline, and say why.
- Keep the handler thin: parse, call one injected service, map the result.
- Group related endpoints with `MapGroup`; attach shared metadata, filters, and auth to the group.
- Return `TypedResults`; never hand-write status codes as magic integers.
- Parse and validate at the boundary into records; bind `[AsParameters]` request records, not long parameter lists.
- Put cross-cutting concerns in endpoint filters or middleware, never copied into each handler.
- Version the API explicitly and publish OpenAPI from code; never break a shipped contract silently.
- Thread the request `CancellationToken` into every downstream call.

### 04 — Persistence with EF Core

- Register the `DbContext` Scoped (one per request); pool for throughput; never Singleton.
- Read with `AsNoTracking` by default; track only when you intend to write.
- Disable lazy loading; load related data with explicit `Include` or project with `Select`.
- Never expose or serialize an entity; project to a DTO at the boundary.
- Keep queries parameterized by construction; never concatenate user input; watch for client-side evaluation.
- Make a unit of work one `SaveChangesAsync` in one transaction; pass the `CancellationToken`.
- Apply schema changes through reviewed, checked-in migrations; never ungated `EnsureCreated`/auto-migrate in production.
- Drop to Dapper or `FromSql` only for a measured hot path, parameterized and parsed into a record.

### 05 — Serialization & Validation

- Use `System.Text.Json` exclusively; no `Newtonsoft.Json` in new code.
- Serialize through the source generator, not reflection, on hot paths.
- Configure `JsonSerializerOptions` once and reuse the instance; never per call.
- Parse a deserialized DTO into a validated domain `record` at the boundary.
- Report validation failures as RFC 9457 `ProblemDetails`, never a bare 500 or a leaked message.
- Distinguish absent from null on the wire deliberately.
- Never serialize an EF entity or a domain aggregate directly; map to a response DTO.
- Keep validation explicit at the edge; prefer guard/parse code over scattered DataAnnotations.

### 06 — Logging & Observability

- Log through `ILogger<T>` with named structural placeholders; never interpolate the message.
- Use the `LoggerMessage` source generator on hot-path log statements.
- Pick the log level deliberately; errors carry the exception object.
- Carry the request correlation id on every log and span.
- Emit traces and metrics through OpenTelemetry, exported via OTLP.
- Make health honest: liveness checks the process, readiness checks the dependencies.
- Never log secrets or PII; redact before the value reaches a sink.
- Ban `Console.WriteLine` for diagnostics; logging goes through the abstraction.

### 07 — ASP.NET Performance

- Keep the request path async end to end; never block; leave `AllowSynchronousIO` off.
- Cache deliberately with bounded size and explicit expiry; never an unbounded in-memory cache.
- Reuse `HttpClient` through `IHttpClientFactory` typed clients; never `new` one per request.
- Cap the host with rate limiting and Kestrel limits so one client cannot exhaust it.
- Keep middleware allocation-light and correctly ordered; it runs on every request.
- Stream large bodies; never buffer a whole payload into memory.
- Enable response compression for compressible types; prefer pre-compressed static assets; measure the tradeoff.
- Measure before tuning: load-test, run Server GC, watch p99 and the pool, optimize the slowest resource first.

### 08 — Build & Deployment

- Containerize on a minimal, non-root base with a multi-stage, locked build.
- Publish trimmed and, where dependencies allow, Native AOT; verify trim/AOT warnings are clean.
- Take configuration from the environment at runtime; never bake it into the image.
- Source secrets from the orchestrator or a vault; never from the image, the repo, or a build arg.
- Expose liveness and readiness probes; readiness reflects real dependency health.
- Implement graceful shutdown: handle `SIGTERM`, drain in flight, dispose, exit within a deadline.
- Make the build reproducible and the artifact immutable; the deployed artifact is the tested artifact.
- Gate every merge in CI: locked restore, warnings-as-errors build, full tests, container scan.
