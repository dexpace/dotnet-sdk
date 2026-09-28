# Performance

Cross-cutting performance rules for all languages and runtimes. Extends root rule #10: "Performance from the outset."

## Design-Phase Performance

- The best time for 1000x wins is the design phase. Once the architecture is set, you're fighting for 10% gains.
- Before writing code, sketch resource usage against **4 resources x 2 characteristics**:

| | Bandwidth | Latency |
|---|---|---|
| **Network** | How much data? | How many round trips? |
| **Disk** | How much I/O? | Random vs sequential? |
| **Memory** | Working set size? | Allocation rate? |
| **CPU** | Total instructions? | Cache friendliness? |

- Choose algorithms and data structures that minimize the dominant resource. A single design decision (batching, caching, compression) often matters more than all micro-optimizations combined.
- Question every network call. Question every disk write. These are orders of magnitude slower than memory and CPU.

## Resource Optimization Order

- Optimize for the slowest resource first: **network > disk > memory > CPU**.
- One eliminated network round trip beats any amount of CPU optimization. One avoided disk seek beats any memory optimization.
- Adjust for frequency: a cheap operation called 10M times may dominate an expensive operation called once.
- Measure to confirm which resource is the actual bottleneck. Intuition is unreliable.

## Batching

- Amortize costs. **N+1 queries are bugs**, not performance issues to "optimize later."
- Batch database queries, API calls, file operations, message publishes. One call with N items, not N calls with 1 item.

```
// Bug -- N+1
for hotel in hotels:
    rooms = db.query("SELECT * FROM rooms WHERE hotel_id = ?", hotel.id)

// Fixed -- single batch query
rooms = db.query("SELECT * FROM rooms WHERE hotel_id IN (?)", hotel_ids)
```

- When batching isn't possible natively, accumulate and flush: collect items, process in chunks.
- Set batch size limits. Unbounded batches become memory problems.

## Object Reuse

- Reuse expensive objects. These are singletons or long-lived pooled instances, **never** per-request allocations:
  - `ObjectMapper` / JSON serializers
  - `HttpClient` / HTTP connection pools
  - Database connection pools
  - Thread pools / executor services
  - Compiled regex patterns
  - SSL contexts

```kotlin
// Kotlin -- shared, configured once
companion object {
    val mapper: ObjectMapper = jacksonObjectMapper().apply { ... }
    val httpClient: OkHttpClient = OkHttpClient.Builder().build()
}

// Banned -- per-request allocation
fun handleRequest(req: Request): Response {
    val mapper = ObjectMapper()  // NO
    val client = OkHttpClient()  // NO
}
```

```go
// Go -- package-level singletons
var (
    httpClient = &http.Client{Timeout: 5 * time.Second}
    isoRegex   = regexp.MustCompile(`^\d{4}-\d{2}-\d{2}$`)
)

// Banned -- recompiling on every call
func parse(s string) bool {
    re := regexp.MustCompile(`^\d{4}-\d{2}-\d{2}$`)  // NO
    return re.MatchString(s)
}
```

```python
# Python -- module-level singletons (constructed once at import)
_session = requests.Session()
_iso_re = re.compile(r"^\d{4}-\d{2}-\d{2}$")

# Banned -- per-call construction
def fetch(url: str) -> Response:
    return requests.Session().get(url)  # NO -- builds a new pool each call
```

## Allocation Awareness

- Understand where allocations happen. Every allocation is future GC pressure.
- Prefer stack over heap where the language permits (Go value types, Zig comptime, Java value-based classes).
- Avoid unnecessary boxing: `int` not `Integer`, `long` not `Long` on hot paths (JVM). Use primitive-specialized collections where available.
- Reuse buffers for I/O. Pre-allocate collections when the size is known: `ArrayList(expectedSize)`, `make([]T, 0, expectedCap)`.
- Watch for hidden allocations: varargs create arrays, string concatenation creates intermediate strings, lambda captures may allocate.

## Profiling Discipline

- **Never optimize without measuring first.** Intuition about performance is wrong more often than right.
- Profile before optimizing. Measure after optimizing. If you can't measure the improvement, it didn't happen.
- Tools by platform:
  - JVM: async-profiler (flamegraphs), JFR (allocation + GC), VisualVM
  - Go: `pprof` (CPU + memory + goroutine), `runtime/trace`
  - Python: `py-spy` (sampling, no instrumentation), `cProfile`, `memray` (allocation tracking), `scalene` (CPU + memory)
  - Node: `--prof`, Chrome DevTools profiler, `clinic.js`
  - General: flamegraphs, heap dumps, GC logs
- Profile in conditions that approximate production. Profiling in dev with toy data proves nothing.

## Benchmarking

- Use proper benchmarking tools. Microbenchmarks without proper tooling produce noise, not data.
  - JVM: **JMH** (handles JIT warmup, dead code elimination, loop optimization)
  - Go: `testing.B` (handles iteration count, timer reset)
  - Python: `pytest-benchmark`, `timeit` for inline microbench; `asv` for tracking over time
  - JS: `Benchmark.js` or `mitata`
- Warm up the JIT before measuring (JVM, V8). Run enough iterations to reach steady state.
- Report **percentiles** (p50, p95, p99), not averages. Averages hide tail latency.
- Control for variance: run multiple times, report standard deviation. Reject results with high variance.
- Benchmark the thing that matters: end-to-end latency, throughput, allocation rate -- not isolated function call time.

## Hot Path Optimization

- Identify hot paths through profiling. Optimize them relentlessly. Ignore cold paths.
- Keep hot paths allocation-free where possible. Pre-allocate. Reuse. Pool.
- Move validation, logging, and debug checks out of hot paths (or guard them):

```kotlin
// Logging guarded on hot path
if (logger.isDebugEnabled) {
    logger.debug("Processing item: {}", item)
}
```

- Pre-compute what you can. Lookup tables over runtime calculation. Compiled patterns over runtime compilation.
- Avoid virtual dispatch on hot paths where the language allows. Concrete types, not interfaces, on the critical path.

## Caching

- Cache expensive computations and remote call results. Caching is the highest-leverage performance tool after design.
- **Always bounded.** Use LRU, LFU, or size-bounded caches. Unbounded caches are memory leaks.
- **Always with TTL.** Prefer TTL-based expiry over event-based invalidation. Invalidation is a distributed systems problem; TTL is a clock read.
- Set cache sizes explicitly based on expected working set. Monitor hit rates. A cache with a low hit rate is wasted memory.
- Cache immutable data aggressively. Cache mutable data cautiously with short TTLs.
- Never cache errors long-term. Cache negative results briefly (to prevent thundering herds) or not at all.

## Connection Pooling

- Reuse connections for HTTP, database, and any persistent protocol. Connection establishment is expensive (TCP handshake, TLS negotiation, auth).
- Set pool sizes **explicitly**. Never rely on library defaults -- they are tuned for "works," not for your workload.
- Set idle timeouts to reclaim unused connections. Set max lifetime to rotate connections and avoid stale server-side state.
- Monitor pool utilization: if the pool is always exhausted, increase it or reduce hold time. If it's always idle, shrink it.

## Lazy vs Eager

- **Lazy initialization** for expensive resources that may not be needed on every code path. Avoid paying the cost until first use.
- **Eager initialization** for resources needed on every request. Pay the cost once at startup, not on the first request (which adds latency to a real user).
- Lazy is not free -- it adds synchronization cost and complexity. Use it deliberately, not by default.
- On the JVM, use `lazy {}` (Kotlin) or `Suppliers.memoize()` (Guava) for thread-safe lazy init. Avoid double-checked locking by hand.

## String Performance

- Avoid string concatenation in loops. Use `StringBuilder` (JVM), `strings.Builder` (Go), `"".join(parts)` (Python), template literals (JS), `fmt.Sprintf` (Go for complex formatting).

```kotlin
// Good
val sb = StringBuilder(estimatedSize)
for (item in items) {
    sb.append(item.name).append(", ")
}

// Bad -- O(n^2) allocation
var result = ""
for (item in items) {
    result += item.name + ", "
}
```

- Intern frequently-compared strings on the JVM when the set is bounded and known.
- Prefer `toByteArray()` / byte buffers over string manipulation for binary protocols.
- Use `contentEquals` / `regionMatches` for partial comparisons instead of creating substrings.
