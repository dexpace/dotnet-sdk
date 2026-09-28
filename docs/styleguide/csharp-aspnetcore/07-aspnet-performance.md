# 07 — ASP.NET Performance

The runtime serves every request from one shared, finite thread pool, so performance here is a property of the host, not of any single handler: a thread one request blocks is a thread every other request waits for. This chapter is about working with that constraint — staying async end to end, caching deliberately with bounds, pooling the clients and buffers that exhaust under load, and letting the orchestrator's limits and the profiler's numbers, not a hunch, decide where to spend effort. It embodies ASP-4 and layers the runtime's edges onto the core's performance discipline (core [15](../csharp/15-performance.md)).

## What good looks like

```csharp
namespace Dexpace.Api;

// Typed client (7.3) reused through the factory; the handler is async end to end (7.1).
public sealed class RatesClient(HttpClient http)
{
    public Task<Rates> Get(CurrencyCode code, CancellationToken ct) =>
        http.GetFromJsonAsync<Rates>($"/rates/{code}", RatesContext.Default.Rates, ct)!;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<RatesClient>(c => c.BaseAddress = new Uri("https://fx.internal"));
builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("api", l =>   // limits on everything (7.4)
{
    l.PermitLimit = 100;
    l.Window = TimeSpan.FromSeconds(1);
}));
builder.Services.AddOutputCache(o => o.AddBasePolicy(p => p.Expire(TimeSpan.FromSeconds(30))));  // 7.2
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 1 * 1024 * 1024);            // 7.4

var app = builder.Build();
app.UseRateLimiter();
app.UseOutputCache();
app.MapGet("/rates/{code}", (CurrencyCode code, RatesClient client, CancellationToken ct) =>
    client.Get(code, ct)).CacheOutput().RequireRateLimiting("api");
```

The handler is async to the leaf and threads the request's `CancellationToken` through every call, so a client disconnect cancels the work it abandoned (7.1). The client is a typed `HttpClient` resolved through the factory, never `new`-ed per request (7.3); the response is cached with an explicit 30-second expiry (7.2); and Kestrel and the rate limiter cap body size and request rate so no one client can exhaust the host (7.4). Nothing here was tuned on a hunch — the shape was confirmed under a load test watching p99 and the thread-pool counters first (7.8).

## Rules

### 7.1 — Keep the request path async end to end; never block, and leave `AllowSynchronousIO` off.

**Reasoning, step by step:**
1. A thread blocked on I/O is a thread removed from the pool that serves every other request; enough blocked threads and the server thread-starves and stops responding under exactly the load it was built for. `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, and synchronous file or network I/O each park a pool thread on work that should have suspended and freed it.
2. So the path is async from the endpoint to the leaf I/O call (core [9.1](../csharp/09-concurrency.md)): no sync-over-async anywhere, and `AllowSynchronousIO` stays at its default `false` on both Kestrel and the server options, turning a stray synchronous read into an exception in test rather than a stalled thread in production. Every async call carries the request's `CancellationToken`, so a disconnect cancels the abandoned work instead of holding the thread to completion.

**Worked example:**
```csharp
var rates = httpClient.GetFromJsonAsync<Rates>(url).Result;   // bad — blocks a pool thread
var rates = await httpClient.GetFromJsonAsync<Rates>(url, ct); // good — suspends, frees the thread, cancels on disconnect
```
**Enforcement:** CA1849 (call the async method in an async context); `AllowSynchronousIO` defaults left off, asserted in a startup test; review rejects `.Result`/`.Wait()` on the request path.

### 7.2 — Cache deliberately with bounded size and explicit expiry; never an unbounded in-memory cache.

**Reasoning, step by step:**
1. The cheapest request is the one already answered, but an unbounded cache is a memory leak that grows until the host is OOM-killed under load — exactly when caching was supposed to help. Reach for output caching for cacheable HTTP responses (varying by the keys that actually distinguish them), and `HybridCache` for the read-through in-process-plus-distributed pattern, which collapses the stampede where many requests miss the same key at once.
2. Every cache entry carries an explicit expiry and the cache a bounded size or entry cap, so eviction is a designed behaviour, not a crash (root rule 9, limits on everything; core [13](../csharp/13-resource-management.md)). The bound and the expiry are chosen from the data's staleness tolerance and the host's memory budget, and both are reviewable at the registration site.

**Worked example:**
```csharp
builder.Services.AddHybridCache(o => o.DefaultEntryOptions = new()
{
    Expiration = TimeSpan.FromMinutes(5),                 // explicit expiry — entries do not live forever
    LocalCacheExpiration = TimeSpan.FromMinutes(1),
});
// read-through: one miss populates the cache, concurrent misses for the same key coalesce
var rates = await cache.GetOrCreateAsync(key, ct => loader.Load(key, ct), cancellationToken: token);
```
**Enforcement:** review rejects a cache with no size bound and no expiry; `MemoryCache` requires `SizeLimit` and per-entry `Size`; load test confirms memory stays bounded under sustained miss traffic.

### 7.3 — Reuse `HttpClient` through `IHttpClientFactory` typed clients; never `new` one per request.

**Reasoning, step by step:**
1. A `new HttpClient()` per request leaks sockets: each instance holds connections that linger in `TIME_WAIT` after disposal, and under load the host exhausts its ephemeral ports and new connections fail. A single long-lived static client avoids that but pins stale DNS, so it never sees a failover or a rotated endpoint. The factory resolves both — it pools and recycles the underlying handlers on a schedule, so sockets are reused and DNS is picked up again.
2. Register a typed client (`AddHttpClient<TClient>`) so the configured `HttpClient` is injected into a purpose-built class, the base address and default headers live in one place, and resilience handlers (retry, timeout, circuit breaker) attach to the pipeline once. This is core [13.8](../csharp/13-resource-management.md) applied to the web host's outbound edge — the factory owns the handler lifetime, the typed client owns the call shape.

**Worked example:**
```csharp
var http = new HttpClient();                              // bad — per-request: socket exhaustion + stale DNS
var rates = await http.GetFromJsonAsync<Rates>(url, ct);

builder.Services.AddHttpClient<RatesClient>(c =>          // good — pooled handler, DNS rotation, one config site
    c.BaseAddress = new Uri("https://fx.internal"));
```
**Enforcement:** review rejects `new HttpClient()` on the request path; CA2000 on undisposed handlers; typed-client registration is the reviewed default for outbound HTTP.

### 7.4 — Cap the host with rate limiting and Kestrel limits so one client cannot exhaust it.

**Reasoning, step by step:**
1. An uncapped server trusts every client to behave, and one that does not — a runaway loop, a slow-loris, a multi-gigabyte upload — consumes memory, connections, or thread-pool capacity until the host falls over for everyone. Limits convert that from an outage into a bounded, observable rejection (root rule 9, limits on everything).
2. So configure the rate limiter (`AddRateLimiter` with a fixed-window, sliding-window, or concurrency policy per endpoint group) to reject excess requests with `429` before they reach a handler, and set Kestrel's limits — `MaxRequestBodySize`, `MaxConcurrentConnections`, `KeepAliveTimeout`, and request timeouts — so a body, a connection, or a slow read has a ceiling. Each limit is chosen from a measured budget, not a guess, and a rejected request is logged so the cap is observable (chapter [06](./06-logging-and-observability.md)).

**Worked example:**
```csharp
builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = 1 * 1024 * 1024;        // 1 MB ceiling, not unbounded
    k.Limits.MaxConcurrentConnections = 1000;
    k.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddRateLimiter(o =>
    o.AddSlidingWindowLimiter("api", l => { l.PermitLimit = 100; l.Window = TimeSpan.FromSeconds(1); l.SegmentsPerWindow = 4; }));
```
**Enforcement:** Kestrel limits and rate-limiter policies present in startup config and reviewed; a load test confirms excess traffic returns `429`/`503` rather than crashing the host.

### 7.5 — Keep middleware allocation-light and correctly ordered; it runs on every request.

**Reasoning, step by step:**
1. A middleware component runs once per request, so a per-request allocation inside it — a captured closure, a buffered copy of the body, a `new` logger scope object — is multiplied across all traffic and shows up as steady GC pressure under load. Allocate the expensive state once at construction (middleware is effectively a singleton in the pipeline), capture nothing per request, and avoid reading or buffering the body unless the component's job genuinely requires it.
2. Order is correctness and cost both: the pipeline runs top to bottom, so a misplaced expensive component runs for requests it should never have seen. Put the cheap, high-rejection middleware first — exception handling, then HSTS/HTTPS redirect, then rate limiting, then auth, then output caching, then routing — so a request that will be rejected or served from cache does the least work before it is.

**Worked example:**
```csharp
public sealed class CorrelationMiddleware(RequestDelegate next)   // next captured once, not per request
{
    public Task Invoke(HttpContext context)                       // no per-request allocation in the hot path
    {
        context.Response.Headers["X-Correlation-Id"] = Activity.Current?.Id ?? context.TraceIdentifier;
        return next(context);
    }
}
app.UseExceptionHandler();      // cheap, first
app.UseRateLimiter();           // reject excess before auth or routing runs
app.UseOutputCache();
```
**Enforcement:** review of middleware allocation and pipeline order; allocation profiler on the request path; CA1812/CA1822 on middleware members.

### 7.6 — Stream large bodies; never buffer a whole payload into memory.

**Reasoning, step by step:**
1. Materializing a large request or response fully in memory — a `List<T>` of every row, a `byte[]` of the whole file — scales the host's memory with the payload size and the concurrent request count, and anything ≥ 85 KB lands on the Large Object Heap, collected only by expensive full GCs (core [15.8](../csharp/15-performance.md)). The fix is to never hold the whole payload: produce and consume it incrementally.
2. Return an `IAsyncEnumerable<T>` so the serializer streams items as the query yields them, or `Results.Stream` to write straight to the response body; read large uploads through the request `PipeReader` and write through the response `PipeWriter` (or `BodyWriter`), processing each chunk and releasing it. Peak memory then tracks the buffer size, not the payload size, and stays flat as payloads grow.

**Worked example:**
```csharp
app.MapGet("/export", (IOrderQuery query, CancellationToken ct) =>
    query.StreamAll(ct));                                  // IAsyncEnumerable — streamed, never buffered whole

public async IAsyncEnumerable<OrderDto> StreamAll([EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var order in _db.Orders.AsAsyncEnumerable().WithCancellation(ct))
        yield return order.ToDto();                        // one row in flight, not the whole table
}
```
**Enforcement:** review rejects fully-buffered large payloads; load test confirms memory stays flat as payload size grows; `[EnumeratorCancellation]` present on streamed endpoints.

### 7.7 — Enable response compression for compressible types; prefer pre-compressed static assets, and measure the tradeoff.

**Reasoning, step by step:**
1. On a network-bound path, compressing a text response (JSON, HTML, SVG) trades a little CPU for a large reduction in bytes on the wire, and bytes are usually the slowest resource (root rule 11; core [15.8](../csharp/15-performance.md)). Enable response compression scoped to compressible MIME types only — compressing an already-compressed image or video burns CPU for nothing and can grow the payload — and never compress over TLS for content reflecting request data without considering the BREACH class of attack (see [security.md](../security.md)).
2. For static assets, compress once at build time and serve the pre-compressed `.br`/`.gz` variant, so the CPU cost is paid once rather than per request. The compression level is a measured choice, not a default: a higher level costs more CPU per response, so benchmark the size-versus-CPU tradeoff under representative load before committing to it.

**Worked example:**
```csharp
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;                              // only with the BREACH tradeoff understood
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);  // compressible only
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
app.UseResponseCompression();
```
**Enforcement:** compression scoped to compressible types and reviewed; pre-compressed static assets produced in the build; benchmark records the size/CPU tradeoff for the chosen level.

### 7.8 — Measure before tuning: load-test, run Server GC, watch p99 and the pool, optimize the slowest resource first.

**Reasoning, step by step:**
1. Intuition about a server under concurrency is routinely wrong — the bottleneck is rarely the line that feels hot, and a single-request benchmark hides the thread-pool and GC behaviour that only appears under load. So no tuning in this chapter is applied on a hunch: load-test a realistic workload, watch the tail (p99/p99.9, not the mean, since the tail is what users feel), and read the thread-pool and GC counters (`ThreadPool` queue length, GC pause time, allocation rate) before changing a line.
2. When a number justifies acting, spend effort on the slowest resource first — network > disk > memory > CPU (root rule 11; core [15.8](../csharp/15-performance.md)) — because shaving CPU off a path that waits on a database round-trip changes nothing measurable. Run Server GC (the default for ASP.NET Core, confirmed not overridden) for throughput-bound services, since it parallelizes collection across cores and keeps pause time off the request tail.

**Worked example:**
```xml
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>   <!-- throughput GC for servers; confirm not overridden -->
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
</PropertyGroup>
```
**Enforcement:** load-test results and profiler evidence (p99, thread-pool and GC counters) required in review for any performance-motivated change; Server GC confirmed enabled; the slowest-resource-first order applied.

## Cross-references

- Async correctness, `CancellationToken` flow, and `ValueTask` on hot async paths: [../csharp/09-concurrency.md](../csharp/09-concurrency.md). Bounded caches, `ArrayPool`/`FrozenDictionary`, and the `HttpClient` factory rule: [../csharp/13-resource-management.md](../csharp/13-resource-management.md).
- Spans, hidden allocations, LOH, and BenchmarkDotNet: [../csharp/15-performance.md](../csharp/15-performance.md). The priority order and the slowest-resource-first rule: [../performance.md](../performance.md).
- Structured logging of rejected requests and limit breaches: [06-logging-and-observability.md](./06-logging-and-observability.md). The BREACH tradeoff for compression over TLS: [../security.md](../security.md).
