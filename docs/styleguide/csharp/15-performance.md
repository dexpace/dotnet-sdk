# 15 — Performance

Performance is a design decision, not a late patch: the right types and few allocations are chosen once, when they are cheap, and are expensive to retrofit. This chapter is about working with the grain of the CLR and the JIT — slicing instead of copying, sealing so calls devirtualize, sidestepping the allocations that feed the garbage collector. Every rule is downstream of one discipline: measure first, then optimize the slowest resource, and let the benchmark, not the hunch, decide.

## What good looks like

```csharp
namespace Dexpace.Parsing;

public static class CsvFields
{
    // Slices the line in place — no substrings, no intermediate List, no LINQ (15.1, 15.2).
    public static int CountNonEmpty(ReadOnlySpan<char> line)
    {
        int count = 0;
        int start = 0;
        for (int i = 0; i <= line.Length; i++)
        {
            if (i == line.Length || line[i] == ',')
            {
                ReadOnlySpan<char> field = line[start..i];   // a view, not a copy
                if (!field.IsWhiteSpace()) count++;
                start = i + 1;
            }
        }
        return count;
    }
}
```

`CountNonEmpty` takes a `ReadOnlySpan<char>` and slices it with `line[start..i]`, so a thousand-field line allocates nothing — no substrings, no `Split` array, no LINQ closure or iterator (15.1, 15.2). The `static` class and method let the JIT skip an instance load (15.7), and the bounded `for` loop keeps the work provable (root rule 9). The hot path is plain, allocation-free, and was confirmed faster by a benchmark before it earned this shape (15.8).

## Rules

### 15.1 — Slice and parse with `Span<T>`, `ReadOnlySpan<T>`, and `Memory<T>`; bound every `stackalloc`.

**Reasoning, step by step:**
1. A substring, a `Split`, or an array copy allocates a new buffer and copies bytes, and in a loop those allocations dominate. `ReadOnlySpan<T>` is a view over existing memory — a string, an array, a stack buffer — so `text[start..end]` slices without allocating or copying, and the span-based parse APIs (`int.Parse(ReadOnlySpan<char>)`, `Utf8Parser`) read straight from it. Take spans as parameters and slice inward; reach for `Memory<T>` only when the view must outlive the stack (stored in a field, captured by async, chapter [09](./09-concurrency.md)), since a `ref struct` span cannot.
2. `stackalloc` puts a small buffer on the stack with zero GC cost, but only for a *fixed, small* size — an unbounded `stackalloc length` is a stack-overflow waiting for the input that makes `length` large (root rule 9). Cap it with a constant and fall back to the heap or `ArrayPool` (15.4) above the cap, so the fast path stays on the stack and the large case stays safe.

**Worked example:**
```csharp
const int StackThreshold = 256;
Span<byte> buffer = length <= StackThreshold
    ? stackalloc byte[StackThreshold]                      // bounded: fixed size, small
    : new byte[length];                                    // large input falls back to the heap
int written = Encode(source, buffer[..length]);
```
**Enforcement:** review of hot-path slicing; an unbounded `stackalloc` is a review finding; benchmark confirms the allocation win.

### 15.2 — In a measured hot path, eliminate hidden allocations: LINQ, capturing lambdas, `params`, and boxing.

**Reasoning, step by step:**
1. The dangerous allocations are the ones you do not see. A LINQ pipeline allocates an iterator per operator and a closure per captured variable; a lambda that captures a local allocates a display class; a `params T[]` parameter allocates an array on every call that does not pass one. None of this matters on a cold path — prefer the clear LINQ pipeline there (root rule 6) — but inside a measured hot loop each is per-iteration garbage, so drop to a `foreach` or `for`, hoist the work out of the loop, and add a `ReadOnlySpan<T>` overload beside the `params` one.
2. Boxing is the quietest of all: assign a `struct` to `object`, to a non-generic interface, or to a `dynamic` (banned anyway, chapter [03](./03-nullability-and-the-type-system.md)), and the runtime silently allocates a heap box and copies the value in. Watch the conversions a value type undergoes — an `int` in a `List<object>`, a struct passed where `IComparable` (non-generic) is expected, a struct interpolated through a non-generic path — and keep value types in generic, strongly typed containers so they stay on the stack.

**Worked example:**
```csharp
int total = items.Where(i => i.Active).Sum(i => i.Cost);    // bad in a hot loop — iterators + closures
int total = 0;
foreach (var item in items)                                 // good — zero allocation
    if (item.Active) total += item.Cost;
object boxed = 42;                                          // bad — silent box; keep ints in generic containers
```
**Enforcement:** CA1860 (avoid `Enumerable.Any()` on a count); profiler/allocation evidence in review for hot paths; boxing flagged in review.

### 15.3 — Pass large readonly structs by `in` and return by `ref readonly`; mark value types `readonly struct`.

**Reasoning, step by step:**
1. A struct passed by value is copied in full at every call, so a large struct (a matrix, a wide options value) copies a lot of bytes per call. Passing it by `in` hands the callee a readonly reference instead of a copy, eliminating the per-call duplication while keeping the value immutable to the callee; returning a large readonly value by `ref readonly` does the same on the way out. Use these for structs above a handful of words; small structs (chapter [06](./06-types-and-data-modeling.md)) copy cheaply and need no `in`.
2. The compiler only skips the *defensive* copy at an `in` parameter when it can prove no member mutates `this`, and the proof is the `readonly` modifier on the struct itself. An ordinary (non-`readonly`) struct passed by `in` is defensively copied on every member access — the opposite of the intent — so always declare value types `readonly struct` (chapter [06](./06-types-and-data-modeling.md)) to make `in` actually cheap.

**Worked example:**
```csharp
public readonly struct Matrix4x4 { /* 16 floats — large, immutable */ }

// `in` avoids copying 64 bytes per call; `readonly struct` lets the JIT skip defensive copies.
public static Vector4 Transform(in Matrix4x4 m, Vector4 v) { /* ... */ }
```
**Enforcement:** review of large-struct passing; CA1815 (override equality on value types); `readonly struct` required on value types (chapter [06](./06-types-and-data-modeling.md)).

### 15.4 — Use `ValueTask` for hot, often-synchronous async, and rent buffers from `ArrayPool<T>`.

**Reasoning, step by step:**
1. A `Task<T>` is a heap object, so an `async` method that usually completes synchronously — a cache hit, a buffered read — allocates a `Task` on every call to return a value it already has. `ValueTask<T>` (chapter [09](./09-concurrency.md)) wraps the synchronous result without allocating and only falls back to a `Task` when it genuinely suspends, so the common fast path is allocation-free. Reserve it for hot, frequently-synchronous paths and obey its one-await rule; a plain `Task` remains the default elsewhere.
2. A large transient buffer allocated per operation churns the GC and, above 85 KB, lands on the Large Object Heap (15.8). Rent it from `ArrayPool<T>.Shared` instead, use it, and return it in a `finally` so it is reused rather than collected (chapter [13](./13-resource-management.md)). The pool is bounded by construction (root rule 9), and the rented array may be larger than asked, so slice it to the length you need.

**Worked example:**
```csharp
byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    int read = await stream.ReadAsync(buffer.AsMemory(0, size), ct);   // reuse, don't allocate per call
    Process(buffer.AsSpan(0, read));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);                             // always return what you rent
}
```
**Enforcement:** CA1849/review for hot async paths; `ArrayPool` returns paired in `finally` (chapter [13](./13-resource-management.md)); benchmark confirms the allocation reduction.

### 15.5 — Presize collections, prefer hashed lookups over linear scans, and reach through spans to avoid re-hashing.

**Reasoning, step by step:**
1. A `List<T>` or `Dictionary` grown from default capacity reallocates and copies its backing array on each resize, so when the final size is known, pass it to the constructor (`new List<T>(count)`) and pay one allocation instead of a logarithmic series. The known capacity comes free from a source collection's `Count`, and presizing turns an O(n)-amortized-with-copies fill into a single buffer.
2. Repeatedly searching a list with `Contains`, `First`, or `Any` is an O(n) scan each time, so a loop of lookups is O(n·m); build a `HashSet<T>` or `Dictionary` once and the lookups become O(1), and a `FrozenDictionary`/`FrozenSet` optimizes the read side further for a table built once and read forever. Where a value type's hashing dominates, `CollectionsMarshal.GetValueRefOrAddDefault` and span access let you read or update an entry without re-hashing or copying the struct.

**Worked example:**
```csharp
var seen = new HashSet<UserId>(incoming.Count);            // presized; O(1) membership
foreach (var id in incoming)
    if (!seen.Add(id)) DropDuplicate(id);                  // one hash per id, not a linear scan
```
**Enforcement:** CA1860 (don't use `Any()`/`Count()` where cheaper exists); review of repeated linear scans and unsized collections in hot paths.

### 15.6 — Build strings with `StringBuilder`, interpolated-string handlers, or `string.Create`; never concatenate in a loop.

**Reasoning, step by step:**
1. Strings are immutable, so `result += part` in a loop allocates a brand-new string and copies the whole accumulated text on every iteration — O(n²) bytes copied for n parts, the classic accidental quadratic. Accumulate with a `StringBuilder` (presized when the length is known), which appends into a growable buffer and materializes the string once at the end. For a known shape, the interpolated-string handler (`$"..."`) and `string.Create` write directly into a single allocated buffer with no intermediate strings (chapter [07](./07-csharp-idioms.md)).
2. Outside a loop, plain interpolation and concatenation are fine and clearer — the rule targets accumulation, not every `+`. When the final length and content are computable up front, `string.Create(length, state, callback)` is the floor: one allocation, filled in place through a span, no builder overhead. Reach for it only where a benchmark shows the builder is the bottleneck (15.8).

**Worked example:**
```csharp
var sb = new StringBuilder(lines.Count * 16);             // presized; one final allocation
foreach (var line in lines)
    sb.Append(line).Append('\n');
string report = sb.ToString();                            // not `report += line` in the loop
```
**Enforcement:** CA1834 (use `StringBuilder.Append(char)`); review rejects string concatenation inside loops.

### 15.7 — Seal types to let the JIT devirtualize, cache delegates with `static` lambdas, and favour AOT-friendly code.

**Reasoning, step by step:**
1. A virtual or interface call goes through a dispatch table the JIT cannot inline. When a type is `sealed` (the default, chapter [06](./06-types-and-data-modeling.md)) the JIT knows the exact target, so it devirtualizes the call and often inlines the body — sealing is a correctness-and-performance default that costs nothing. A delegate created inline allocates per call; mark a non-capturing lambda `static` so the compiler caches one instance, and hoist a captured one into a cached field rather than re-allocating it each invocation.
2. Reflection and runtime code generation defeat trimming and Native AOT — the trimmer cannot see a type reached only by name, and the AOT compiler cannot emit code generated at runtime. Prefer source generators (System.Text.Json source-gen over its reflection serializer, `[GeneratedRegex]` over `new Regex`, the logging source generator) so the work happens at build time, the code is trim-safe, and the startup reflection cost disappears.

**Worked example:**
```csharp
items.Where(static i => i.Active);                         // static lambda: cached, no per-call closure

[JsonSerializable(typeof(Order))]
internal sealed partial class OrderContext : JsonSerializerContext { }   // source-gen: AOT- and trim-safe
```
**Enforcement:** CA1822 (mark members static), CA1859 (use concrete types where possible), `sealed` default (chapter [06](./06-types-and-data-modeling.md)); review favours source generators over reflection.

### 15.8 — Measure before optimizing: benchmark with BenchmarkDotNet, profile real workloads, optimize the slowest resource first.

**Reasoning, step by step:**
1. Intuition about C# performance is routinely wrong — the JIT inlines, the GC is generational, and the bottleneck is rarely where it feels. So no optimization in this chapter is applied on a hunch: measure the suspected hot path with BenchmarkDotNet (which handles warmup, statistics, and the `[MemoryDiagnoser]` allocation count), and profile a realistic workload to find where time actually goes before changing a line. An optimization without a before-and-after number is a guess that traded clarity for nothing.
2. When a number justifies acting, spend the effort on the slowest resource first — network > disk > memory > CPU (root rule 11) — because shaving CPU cycles off a path that waits on a network round-trip changes nothing measurable. On the memory tier, configure Server GC for throughput-bound services and avoid Large Object Heap allocations (arrays ≥ 85 KB), which are collected only on expensive full GCs; pool or stream large buffers (15.1, 15.4) instead of allocating them.

**Worked example:**
```csharp
[MemoryDiagnoser]
public class ParseBenchmark
{
    [Benchmark(Baseline = true)] public int Substring() => SlowParse(_input);
    [Benchmark] public int Spans() => FastParse(_input);   // commit only if this is measurably faster
}
```
**Enforcement:** BenchmarkDotNet results and profiler evidence required in review for any performance-motivated change; Server GC configured for services; LOH allocations flagged in review.

## Cross-references

- `ReadOnlySpan<T>` views, boxing, and the banned `dynamic`: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). `readonly struct` for small values and `sealed`-by-default for devirtualization: [06-types-and-data-modeling.md](./06-types-and-data-modeling.md).
- `StringBuilder`, interpolated strings, and LINQ-versus-`foreach` tradeoffs: [07-csharp-idioms.md](./07-csharp-idioms.md). `ValueTask` semantics and the one-await rule: [09-concurrency.md](./09-concurrency.md).
- `ArrayPool`/`MemoryPool` renting and bounded pools: [13-resource-management.md](./13-resource-management.md). The priority order placing performance after correctness and the slowest-resource-first rule: [README.md](./README.md).
