# 09 — Concurrency & Async

Async correctness is the cheapest performance win and the most expensive bug. This chapter is about getting the await chain right end to end, threading cancellation through every call, and bounding every fan-out so concurrency never becomes an unbounded resource (root rule 9). Blocking on async work and firing tasks into the void are the two failures that exhaust a thread pool; both are banned outright.

## What good looks like

```csharp
namespace Dexpace.Ingest;

public static class Importer
{
    // Token threaded last and honoured; bounded fan-out; ConfigureAwait in library code (9.3, 9.4, 9.6).
    public static async Task<int> Import(
        IReadOnlyList<Uri> sources,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var count = 0;
        await Parallel.ForEachAsync(                                  // bounded degree, not unbounded loop (9.6)
            sources,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
            async (source, token) =>
            {
                using var response = await client.GetAsync(source, token).ConfigureAwait(false); // honour + CAF
                Interlocked.Increment(ref count);                    // lock-free shared counter (9.8)
            }).ConfigureAwait(false);
        return count;
    }
}
```

`Import` returns a `Task<int>` and never blocks (9.1, 9.2), takes the `CancellationToken` as its last parameter and passes it into every awaited call (9.3), and calls `ConfigureAwait(false)` on each await because this is library code (9.4). The fan-out is bounded with `MaxDegreeOfParallelism` rather than an unbounded loop of detached tasks (9.6), and the shared counter uses `Interlocked` instead of a `lock` (9.8).

## Rules

### 9.1 — Async all the way down; never block on async with `.Result`, `.Wait()`, or `GetAwaiter().GetResult()`.

**Reasoning, step by step:**
1. Blocking a thread on an incomplete `Task` — `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` — holds that thread idle until the work finishes, and under a synchronization context (a request thread, a UI thread) the continuation needs the very thread you are blocking, which deadlocks. Even without a context, sync-over-async burns a pool thread per blocked call, and a burst of them starves the pool so that ready work cannot run.
2. The fix is structural, not a workaround: make the caller `async` and `await`, propagating async up the call chain to the top (an entry point, a controller, a `Main` that can be `async Task`). There is no safe place to block on async in application code; if a synchronous API truly must call async, that bridge is a documented, reviewed exception, never a casual `.Result`.

**Worked example:**
```csharp
var user = LoadUser(id).Result;                  // bad — deadlock risk + pool starvation
var user = await LoadUser(id);                   // good — async flows up to the caller
```
**Enforcement:** `VSTHRD002`/`VSTHRD103` (avoid sync-over-async) where `Microsoft.VisualStudio.Threading.Analyzers` is referenced; review rejects `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`.

### 9.2 — Ban `async void`; async methods return `Task`, `Task<T>`, or `ValueTask`.

**Reasoning, step by step:**
1. An `async void` method cannot be awaited, so its caller has no handle to know when it finished or whether it threw. An exception escaping an `async void` is not caught by the caller's `try`/`catch` — it is posted to the synchronization context and crashes the process. The method also cannot be composed: you cannot `WhenAll` it, time it out, or cancel-and-wait on it.
2. Return `Task` (or `Task<T>` when there is a result), so the caller can await, observe exceptions, and compose. Use `ValueTask`/`ValueTask<T>` only for the hot, often-synchronous path 9.5 describes. The single sanctioned `async void` is a top-level event handler whose signature the framework fixes — and even there the body should be a thin `try`/`catch` that delegates to an awaitable method so no exception escapes.

**Worked example:**
```csharp
public async void Process(Item item) { await Save(item); }       // bad — unawaitable, exceptions crash
public async Task Process(Item item) { await Save(item); }       // good — awaitable, composable
private async void OnClick(object? s, EventArgs e)               // sanctioned: event handler signature
{
    try { await Save(_current); } catch (Exception ex) { _log.Error(ex, "save failed"); } // no escape (08)
}
```
**Enforcement:** review bans `async void` outside event handlers; `VSTHRD100` (avoid `async void`) where the threading analyzer is referenced.

### 9.3 — Thread a `CancellationToken` through every async method as the last parameter and honour it.

**Reasoning, step by step:**
1. A token nobody passes down is decorative: cancellation only works if it reaches the lowest blocking call, so every async method takes a `CancellationToken` as its last parameter (after any optional defaults) and forwards it into every awaited call. Honour it — pass it to `GetAsync`, `ReadAsync`, `Task.Delay`; for a tight CPU loop with no awaitable to carry it, call `cancellationToken.ThrowIfCancellationRequested()` periodically. A method that accepts a token and ignores it is worse than one that omits it, because it lies.
2. External I/O gets a *mandatory* timeout (root rule 9): wrap or link the caller's token with `new CancellationTokenSource(TimeSpan.FromSeconds(n))` so a hung socket cannot block forever. Link rather than replace — `CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token)` — so both the caller's cancellation and the timeout fire. The resulting `OperationCanceledException` is cooperative cancellation, not an error to swallow (chapter [08](./08-error-handling.md)).

**Worked example:**
```csharp
public async Task<Doc> Fetch(Uri uri, HttpClient client, CancellationToken cancellationToken) // token last
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));                 // mandatory I/O timeout
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
    using var response = await client.GetAsync(uri, linked.Token).ConfigureAwait(false);       // honoured
    return await Parse(response, linked.Token).ConfigureAwait(false);
}
```
**Enforcement:** review requires a token parameter and its propagation; `CA2016` (forward `CancellationToken` to methods that take one); timeout mandatory on external I/O (root rule 9).

### 9.4 — Call `ConfigureAwait(false)` on every await in library code.

**Reasoning, step by step:**
1. By default an `await` captures the current synchronization context and resumes the continuation on it. Library code does not know and must not care which context its caller runs on, and recapturing it costs a context switch and risks the 9.1 deadlock if the caller later blocks. `ConfigureAwait(false)` says "resume anywhere", which is correct for any code that does not touch UI or a request-affine resource.
2. The split is by layer, not preference: library and shared infrastructure code calls `ConfigureAwait(false)` on every await; application and host code (which sits at the top, owns the context, and may touch context-affine state) does not need it because there is no caller above it to protect. Apply it uniformly within a library so the rule is mechanical, not judged await by await.

**Worked example:**
```csharp
// In a library assembly:
var rows = await _db.Query(sql, cancellationToken).ConfigureAwait(false); // resume anywhere
return Map(rows);
// In application/host code, ConfigureAwait(false) is unnecessary and omitted.
```
**Enforcement:** `CA2007` (do not directly await a Task without `ConfigureAwait`) enabled in library projects, suppressed in app/host projects; review.

### 9.5 — Use `ValueTask`/`ValueTask<T>` for hot, often-synchronous paths, awaited exactly once.

**Reasoning, step by step:**
1. `Task<T>` is a heap allocation per call; on a path that frequently completes synchronously — a cache hit, a buffered read — that allocation is pure waste, and `ValueTask<T>` lets the synchronous result return without one. Reserve it for measured hot paths where the allocation shows up in a profile; for everything else `Task`/`Task<T>` is the default, because it is simpler and freely composable.
2. A `ValueTask` carries sharp constraints: await it exactly once, never block on it, never await it concurrently, and never store it and await later. If you need to do any of those, convert it once with `.AsTask()` and use the resulting `Task`. The single-await rule is what makes `ValueTask` safe to pool internally; breaking it reads a recycled or torn result.

**Worked example:**
```csharp
public ValueTask<Config> Get(ConfigKey key)                      // hot path, usually a cache hit
{
    if (_cache.TryGetValue(key, out var config))
        return new ValueTask<Config>(config);                    // synchronous, zero allocation
    return new ValueTask<Config>(Load(key));                     // slow path falls back to Task
}
var config = await Get(key);                                     // awaited exactly once
```
**Enforcement:** `CA2012` (use ValueTasks correctly); review limits `ValueTask` to measured hot paths.

### 9.6 — Fan out with `Task.WhenAll`; bound the degree of parallelism, never fire-and-forget.

**Reasoning, step by step:**
1. Awaiting tasks in a loop runs them one at a time; the concurrent form is to start them, collect the tasks, and `await Task.WhenAll(...)`, which surfaces every result and propagates the first fault. But unbounded `WhenAll` over a large input launches thousands of concurrent operations at once, exhausting connections, sockets, or memory — concurrency is a resource and must be bounded (root rule 9). Use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` set to a deliberate cap so at most *n* run together.
2. Never fire-and-forget: a task you start and do not await is unobserved, so its exception is lost (chapter [08](./08-error-handling.md)) and its lifetime outlives the scope that owns it, which is also a `CS4014` warning the build rejects. If work genuinely must outlive the request, hand it to an owned, bounded background processor (9.7) that tracks and awaits it on shutdown — never a bare `_ = DoWork()`.

**Worked example:**
```csharp
foreach (var id in ids) await Fetch(id, cancellationToken);      // bad — serial, one at a time
var results = await Task.WhenAll(ids.Select(id => Fetch(id, cancellationToken))); // concurrent, but unbounded
await Parallel.ForEachAsync(ids,                                 // good — bounded fan-out
    new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
    async (id, token) => await Process(id, token).ConfigureAwait(false));
```
**Enforcement:** `CS4014` (unawaited task) promoted to error (chapter [01](./01-formatting-and-tooling.md)); review requires a bound on every fan-out.

### 9.7 — Route producer/consumer through a bounded `Channel<T>`; stream with `IAsyncEnumerable<T>`.

**Reasoning, step by step:**
1. A producer faster than its consumer needs backpressure, or it fills memory with a growing queue — another unbounded resource (root rule 9). A bounded `Channel<T>` (`Channel.CreateBounded<T>(capacity)`) gives exactly that: once full, `WriteAsync` awaits until the consumer drains a slot, so the producer is throttled to the consumer's rate by construction. Never use an unbounded channel or a raw `ConcurrentQueue` with no cap for cross-task handoff.
2. To stream results as they are produced rather than buffering them all, return `IAsyncEnumerable<T>` and `await foreach` over it, carrying the `CancellationToken` via `[EnumeratorCancellation]` so cancellation reaches the producer. The consumer pulls one item at a time, the producer's `yield return` suspends until pulled, and memory stays flat regardless of how much data flows through.

**Worked example:**
```csharp
public static async IAsyncEnumerable<Row> Read(                  // streaming, flat memory
    Stream source, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    using var reader = new StreamReader(source);
    while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        yield return Row.Parse(line);
}
await foreach (var row in Read(source, cancellationToken).ConfigureAwait(false)) Handle(row); // token carried
```
**Enforcement:** review requires bounded channels and token-carrying `await foreach`; unbounded channels rejected.

### 9.8 — Prefer immutable data and `Interlocked`/`SemaphoreSlim` to `lock`; keep any lock tiny and `await`-free.

**Reasoning, step by step:**
1. The cheapest synchronization is none: immutable data (`record`, `readonly`, chapter [03](./03-nullability-and-the-type-system.md)) shared across threads needs no lock because nothing mutates it. When shared mutation is unavoidable, a single counter or reference swap is a lock-free `Interlocked.Increment`/`CompareExchange`, and an async critical section is a `SemaphoreSlim(1, 1)` whose `WaitAsync` does not block a thread. Reach for `lock` last, only for a small synchronous critical section over multiple fields.
2. Never `await` inside a `lock`: it is a compile error (CS1996), because the `Monitor` a `lock` lowers to is thread-affine and the continuation could resume on another thread. Holding a lock across slow work also serializes everything behind it, so use `SemaphoreSlim` for an async critical section instead. Keep every lock scope to the few statements that must be atomic, take locks in a consistent order to avoid deadlock, and document every remaining race with a why-comment explaining why it is benign.

**Worked example:**
```csharp
lock (_gate) { await Save(); }                                   // bad — CS1996 compile error: await is illegal in a lock
Interlocked.Increment(ref _hits);                                // good — lock-free counter
await _slot.WaitAsync(cancellationToken).ConfigureAwait(false);  // good — async-safe critical section
try { _state = Compute(); } finally { _slot.Release(); }
```
**Enforcement:** `CS1996` makes `await` inside a `lock` a compile error; review requires tiny lock scopes, consistent lock ordering, and a why-comment on every documented race.

## Cross-references

- `OperationCanceledException` as cooperative cancellation and not swallowing it: [08-error-handling.md](./08-error-handling.md). Guard clauses, `ThrowIf`, and the token-last parameter shape: [05-methods-and-functions.md](./05-methods-and-functions.md).
- Immutable `record`/`readonly` data that needs no lock, and the Try-pattern: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). `CancellationTokenSource` disposal and bounded pools/channels/caches: [13-resource-management.md](./13-resource-management.md).
