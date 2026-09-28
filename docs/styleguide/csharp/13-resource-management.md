# 13 — Resource Management

A managed runtime collects memory but not handles, so files, sockets, connections, and rented buffers are released only when the code says so. This chapter is about saying so deterministically: own a resource and you dispose it, on every path, with `using` doing the work instead of a hand-rolled `try/finally`. It also bounds the pools and caches that recycle resources, because an unbounded one is a leak that waits for load to expose it (root rule 9).

## What good looks like

```csharp
namespace Dexpace.Billing.Invoices;

public sealed class InvoiceExporter : IAsyncDisposable    // owns async cleanup (13.1)
{
    private readonly Stream _output;                       // created here, so disposed here (13.4)

    public InvoiceExporter(string path) => _output = File.Create(path);

    public async Task Export(Invoice invoice, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);   // rented, bounded, returned (13.6)
        try
        {
            using var writer = new Utf8JsonWriter(_output); // using declaration, block-scoped (13.2)
            await Serialize(invoice, writer, buffer, cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);          // returned on every path
        }
    }

    public ValueTask DisposeAsync() => _output.DisposeAsync(); // simple, no finalizer (13.3)
}
```

The exporter owns a `Stream` it created, so it implements `IAsyncDisposable` and disposes that stream and nothing else (13.1, 13.4). The `Utf8JsonWriter` is scoped with a `using` declaration that reads cleaner than a nested block (13.2), the rented buffer has a fixed size and is returned in a `finally` (13.6), and because the class is `sealed` and holds only managed disposables it needs a one-line `DisposeAsync` with no finalizer or `Dispose(bool)` ceremony (13.3). Cleanup is async, so it lives in `DisposeAsync` rather than blocking a synchronous `Dispose` (13.5).

## Rules

### 13.1 — Implement `IDisposable` for anything owning disposables, `IAsyncDisposable` when cleanup is async; prefer `using` to manual `try/finally`.

**Reasoning, step by step:**
1. A type that holds an unmanaged handle or another disposable owns a release obligation, and the language has one vocabulary for that obligation: `IDisposable.Dispose` for synchronous cleanup, `IAsyncDisposable.DisposeAsync` for cleanup that itself awaits (flushing a stream, closing a connection). Declaring the interface lets every caller release the resource the same way and lets `using` do it automatically.
2. Hand-written `try/finally { x.Dispose(); }` is what `using` compiles to, and writing it by hand invites the mistake `using` cannot make — forgetting the `finally` on an early return, or disposing twice. Reach for `using` and `await using` first; drop to explicit `try/finally` only when the lifetime genuinely cannot match a lexical scope, and say why.

**Worked example:**
```csharp
await using var connection = await _source.Open(cancellationToken);  // DisposeAsync runs on scope exit
using var command = connection.CreateCommand();                      // Dispose runs on scope exit
return await command.Execute(cancellationToken);
```
**Enforcement:** `CA2000` (dispose before losing scope); review that an owning type implements the right disposable interface.

### 13.2 — Scope a disposable with a `using` declaration where it reads clearer than a nested statement.

**Reasoning, step by step:**
1. A `using` *declaration* (`using var x = ...;`) disposes `x` at the end of the enclosing block, which flattens the common case where a disposable lives for the rest of the method: no extra brace level, no rightward drift, and the disposal point is unambiguous. The older `using` *statement* with its own block is still right when the lifetime must end before the method does or when several disposables nest.
2. Choose the form by what reads clearly, not by habit. One disposable held to the end of the method wants the declaration; a disposable that must close early, or a tight pair where the inner must outlive nothing, wants the explicit block. Either way the resource is released deterministically — the choice is only about which scope the reader can see at a glance.

**Worked example:**
```csharp
using var reader = file.OpenText();          // good — lives to end of method, no nesting
string content = reader.ReadToEnd();

using (var temp = OpenScratch())             // explicit block — temp must close before the next step
{
    temp.Fill(content);
}
ProcessWithoutScratch();
```
**Enforcement:** `IDE0063` (use simple `using`); review of nesting depth.

### 13.3 — Implement the full dispose pattern only when directly holding an unmanaged resource.

**Reasoning, step by step:**
1. The ceremonial pattern — `protected virtual void Dispose(bool disposing)`, a finalizer, and `GC.SuppressFinalize(this)` — exists for exactly one situation: a type holds a raw unmanaged handle (a `SafeHandle` is the better answer) that the GC would otherwise leak if `Dispose` is never called. The finalizer is the last-resort backstop, and the `bool` distinguishes deterministic disposal from finalization. That machinery is the right tool there and only there.
2. A `sealed` class that owns only *managed* disposables needs none of it: there is nothing for a finalizer to back up because the GC already collects managed objects, so write a plain `public void Dispose()` (or `DisposeAsync`) that disposes the fields, and skip the finalizer and `SuppressFinalize` entirely. Adding the full pattern by reflex pays the finalization cost — a slower allocation, a trip through the finalizer queue — for a guarantee you do not need.

**Worked example:**
```csharp
public sealed class FrameCache : IDisposable   // only managed disposables → simple Dispose
{
    private readonly SemaphoreSlim _gate = new(1);
    public void Dispose() => _gate.Dispose();  // no Dispose(bool), no finalizer, no SuppressFinalize
}
```
**Enforcement:** `CA1816` (call `GC.SuppressFinalize` correctly); review rejects a finalizer on a type holding only managed state.

### 13.4 — Dispose what you create; never dispose a dependency injected into you.

**Reasoning, step by step:**
1. Disposal follows ownership, and ownership follows creation: the code that `new`s a `Stream` or opens a connection holds its lifetime and must release it. A field your type constructs and keeps is yours to dispose; a local you create and finish with is yours to dispose. This is the single question to ask of every disposable — did I create it? — and the answer decides who calls `Dispose`.
2. A dependency handed to you through the constructor is owned by whoever created it — the DI container, or the caller — and they will dispose it on their schedule. Disposing it yourself closes a resource still in use elsewhere and turns a shared singleton into a use-after-dispose bug. Leave injected disposables alone; the container disposes registered services at the end of their scope (see the [csharp-aspnetcore](../csharp-aspnetcore/) guide for the lifetimes).

**Worked example:**
```csharp
public sealed class Reporter : IDisposable
{
    private readonly HttpClient _client;        // injected — NOT mine to dispose
    private readonly FileStream _log;           // created here — mine to dispose

    public Reporter(HttpClient client) { _client = client; _log = File.Create("report.log"); }
    public void Dispose() => _log.Dispose();    // disposes only what it created
}
```
**Enforcement:** `CA2213` (dispose owned fields); review rejects disposing an injected dependency.

### 13.5 — Never block in `Dispose`; expose `IAsyncDisposable` when cleanup is asynchronous.

**Reasoning, step by step:**
1. `Dispose` is synchronous, so cleanup that is truly async — flushing a network buffer, committing a transaction — can only run inside it by blocking on the task with `.Result` or `.Wait()`, which is the sync-over-async deadlock chapter [09](./09-concurrency.md) bans. The block ties up a thread and, in the wrong context, hangs the process at the worst possible moment.
2. When cleanup awaits, the type's release vocabulary is `IAsyncDisposable`: implement `DisposeAsync`, do the awaiting cleanup there, and let callers `await using`. A type may implement both interfaces, but the synchronous `Dispose` must then avoid the async work, not smuggle it in behind a blocking wait. Asynchronous teardown belongs on the asynchronous path.

**Worked example:**
```csharp
public sealed class Ledger : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();   // awaited, not _writer.FlushAsync().Wait()
        await _writer.DisposeAsync();
    }
}
await using var ledger = new Ledger();   // caller awaits the teardown
```
**Enforcement:** `CA2215`; review rejects `.Result`/`.Wait()` inside `Dispose` (chapter [09](./09-concurrency.md)).

### 13.6 — Rent transient buffers from a pool, bound their size, and return them in a `finally`.

**Reasoning, step by step:**
1. A short-lived `byte[]` allocated per call is per-call garbage, and on a hot path that pressure shows up as GC pauses (chapter [15](./15-performance.md)). `ArrayPool<T>.Shared.Rent` (or `MemoryPool<T>`) hands back a reused array instead, so the allocation happens once and amortizes — provided every rent is matched by a return, which only a `finally` guarantees across exceptions and early exits.
2. The rented size must have a fixed upper bound, never the caller's untrusted length, or a single large request becomes an unbounded allocation (root rule 9); rent a capped chunk and loop. A pooled buffer also comes back dirty with the previous tenant's bytes, so clear it before returning anything that held sensitive data, lest the secret leak to the next renter (see the [security](../security.md) guide).

**Worked example:**
```csharp
byte[] buffer = ArrayPool<byte>.Shared.Rent(MaxChunk);   // bounded size, not request.Length
try
{
    int read = await source.ReadAsync(buffer, cancellationToken);
    await sink.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer, clearArray: true);  // cleared — held sensitive bytes
}
```
**Enforcement:** `CA2000`-adjacent review; a rent without a `finally` return, or an unbounded rent size, is a finding.

### 13.7 — Bound every pool, channel, cache, and queue.

**Reasoning, step by step:**
1. Any structure that accumulates work or retains objects is a memory leak the moment its inflow can outpace its outflow, and under load it eventually can. An unbounded `Channel<T>` grows until the producer outruns the consumer and the process dies; an unbounded cache grows until it holds everything ever seen. Bounding is not tuning — it is the difference between backpressure and a crash (root rule 9, chapter [09](./09-concurrency.md)).
2. Give each one an explicit cap: a `BoundedChannelOptions` with a capacity and a `FullMode`, a size- or time-capped cache, an `ObjectPool<T>` with a maximum retained count. The cap forces an early, deliberate decision about what happens when the limit is hit — block the producer, drop the oldest, evict the least-used — instead of deferring that decision to the out-of-memory killer.

**Worked example:**
```csharp
var channel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(capacity: 1024)
{
    FullMode = BoundedChannelFullMode.Wait,   // backpressure, not unbounded growth
});
var pool = new DefaultObjectPoolProvider { MaximumRetained = 64 }.Create<StringBuilder>();
```
**Enforcement:** review rejects `Channel.CreateUnbounded`, an unbounded cache, or a pool without a max; chapter [09](./09-concurrency.md) covers backpressure.

### 13.8 — Reuse `HttpClient` through `IHttpClientFactory`; dispose a `CancellationTokenSource`.

**Reasoning, step by step:**
1. `new HttpClient()` per call is the canonical resource bug: each instance holds its own connection pool, and disposing it leaves sockets in `TIME_WAIT`, so a busy loop exhausts the ephemeral port range and requests start failing under load. `IHttpClientFactory` pools and rotates the underlying handlers, giving long-lived connection reuse and periodic DNS refresh, so application code asks the factory for a client and never news one up.
2. The mirror-image mistake is forgetting that `CancellationTokenSource` *is* disposable — it can register with a timer and a linked-token callback that hold references until disposed. A long-running source, especially one created with a timeout (chapter [09](./09-concurrency.md)), leaks those registrations if it is not disposed, so scope it with `using` like any other resource.

**Worked example:**
```csharp
public sealed class RatesApi(IHttpClientFactory factory)   // injected factory, never new HttpClient()
{
    public async Task<Rates> Fetch(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));       // disposed by the using
        HttpClient client = factory.CreateClient("rates");
        return await client.GetFromJsonAsync<Rates>("/today", timeout.Token);
    }
}
```
**Enforcement:** review rejects `new HttpClient()` in application code; `CA2000`/`CA2213` flag an undisposed `CancellationTokenSource`.

## Cross-references

- The `using` placement and build gates this chapter relies on: [01-formatting-and-tooling.md](./01-formatting-and-tooling.md). The async rules behind no-blocking-in-`Dispose` and bounded channels: [09-concurrency.md](./09-concurrency.md).
- Pooling, `Span<T>`, and the allocation pressure buffers exist to relieve: [15-performance.md](./15-performance.md). DI-owned lifetimes that you must not dispose: [csharp-aspnetcore](../csharp-aspnetcore/).
