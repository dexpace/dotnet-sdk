# performance

## Rules
- Presize collections when the final size is known (for example `new List<T>(count)`) to avoid repeated reallocation and copying on resize (rule 15.5).
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:103-106` · high · sha:d9e3bb5add69</sub>
- Replace repeated linear scans (`Contains`, `First`, `Any` in a loop) with a `HashSet<T>` or `Dictionary` built once, and use `FrozenDictionary`/`FrozenSet` for tables built once and read forever.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:107-107` · high · sha:d9e3bb5add69</sub>
- Never concatenate strings with `+=` in a loop (O(n^2) copying); accumulate with a `StringBuilder` (presized when length is known), or use interpolated-string handlers or `string.Create` for a known shape (rule 15.6).
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:117-120` · high · sha:d9e3bb5add69</sub>
- Outside a loop, plain interpolation and concatenation are fine; the string-building rule targets accumulation, not every `+`.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:121-121` · high · sha:d9e3bb5add69</sub>
- Reach for `string.Create(length, state, callback)` only where a benchmark shows the StringBuilder is the bottleneck.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:121-121` · high · sha:d9e3bb5add69</sub>
- Seal types (sealed is the default) so the JIT can devirtualize and inline calls (rule 15.7).
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:132-135` · high · sha:d9e3bb5add69</sub>
- Mark non-capturing lambdas `static` so the compiler caches one delegate instance, and hoist captured delegates into a cached field rather than re-allocating them per invocation.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:135-135` · high · sha:d9e3bb5add69</sub>
- Prefer source generators over reflection and runtime code generation (System.Text.Json source-gen, `[GeneratedRegex]` over `new Regex`, the logging source generator) because reflection defeats trimming and Native AOT.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:136-143` · high · sha:d9e3bb5add69</sub>
- Measure before optimizing: benchmark suspected hot paths with BenchmarkDotNet (warmup, statistics, `[MemoryDiagnoser]` allocation counts) and profile a realistic workload; no optimization is applied on a hunch, and a performance change needs a before-and-after number (rule 15.8).
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:147-150` · high · sha:d9e3bb5add69</sub>
- Optimize the slowest resource first, in the order network > disk > memory > CPU.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:151-151` · high · sha:d9e3bb5add69</sub>
- Configure Server GC for throughput-bound services, avoid Large Object Heap allocations (arrays of 85 KB or more), and pool or stream large buffers instead of allocating them.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:151-151` · high · sha:d9e3bb5add69</sub>
- Slice and parse with `Span<T>`, `ReadOnlySpan<T>` and span-based parse APIs instead of substrings, `Split` or array copies, taking spans as parameters and slicing inward (rule 15.1).
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:35-38` · high · sha:d9e3bb5add69</sub>
- Use `Memory<T>` only when the view must outlive the stack (stored in a field or captured by async), since a `ref struct` span cannot.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:38-38` · high · sha:d9e3bb5add69</sub>
- Bound every `stackalloc` with a fixed small constant size and fall back to the heap or ArrayPool above the cap, because an unbounded `stackalloc length` risks stack overflow.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:39-48` · high · sha:d9e3bb5add69</sub>
- In a measured hot path, eliminate hidden allocations from LINQ pipelines (iterator per operator, closure per captured variable), capturing lambdas (display class) and `params T[]` (array per call) by using `foreach`/`for`, hoisting work out of the loop, and adding a `ReadOnlySpan<T>` overload beside the `params` one (rule 15.2).
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:51-54` · high · sha:d9e3bb5add69</sub>
- On cold paths prefer the clear LINQ pipeline; the hidden-allocation rule applies only inside measured hot loops.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:54-54` · high · sha:d9e3bb5add69</sub>
- Avoid boxing by keeping value types in generic strongly typed containers; assigning a struct to `object`, a non-generic interface or `dynamic` silently allocates a heap box.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:55-55` · high · sha:d9e3bb5add69</sub>
- Pass large readonly structs (above a handful of words) by `in` and return them by `ref readonly`; small structs copy cheaply and need no `in` (rule 15.3).
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:67-70` · high · sha:d9e3bb5add69</sub>
- Always declare value types `readonly struct`, because the compiler skips the defensive copy at an `in` parameter only when the struct is readonly; a non-readonly struct passed by `in` is defensively copied on every member access.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:71-71` · high · sha:d9e3bb5add69</sub>
- Use `ValueTask<T>` for hot, frequently-synchronous async paths (cache hits, buffered reads) to avoid a Task allocation per call, and obey its one-await rule; plain `Task` remains the default elsewhere (rule 15.4).
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:82-85` · high · sha:d9e3bb5add69</sub>
- Rent large transient buffers from `ArrayPool<T>.Shared` instead of allocating per operation, return them in a `finally`, and slice the rented array to the needed length because it may be larger than requested.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:86-100` · high · sha:d9e3bb5add69</sub>
- Work with the grain of the CLR and JIT (stable types, few allocations, Span<T> over copies) and optimize the slowest resource first: network > disk > memory > CPU.
  <sub>styleguide · `docs/styleguide/csharp/README.md:21-21` · high · sha:1e6ba36fc337</sub>
- Design for performance from the outset because 1000x improvements are cheap at design time: stable types, few allocations, Span<T> over copies, ValueTask where it pays.
  <sub>styleguide · `docs/styleguide/csharp/README.md:69-69` · high · sha:1e6ba36fc337</sub>
- Optimize the slowest resource first: network > disk > memory > CPU.
  <sub>styleguide · `docs/styleguide/csharp/README.md:69-69` · high · sha:1e6ba36fc337</sub>

## Constraints
- Allocations of 85 KB or more land on the Large Object Heap, which is collected only on expensive full GCs.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:86-86` · high · sha:d9e3bb5add69</sub>

## Conclusions
- Performance is treated as a design decision made when cheap rather than a late patch, and every performance rule is subordinate to the discipline of measuring first.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:3-3` · high · sha:d9e3bb5add69</sub>
- Performance ranks before simplicity because the right architecture is chosen once at design time and is expensive to retrofit.
  <sub>styleguide · `docs/styleguide/csharp/README.md:21-21` · high · sha:1e6ba36fc337</sub>

## Reference
- Rule 15.4 is enforced by CA1849 and review for hot async paths, ArrayPool returns paired in `finally`, and a benchmark confirming the allocation reduction.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:101-101` · high · sha:d9e3bb5add69</sub>
- `CollectionsMarshal.GetValueRefOrAddDefault` and span access let code read or update a dictionary entry without re-hashing or copying a struct value.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:107-107` · high · sha:d9e3bb5add69</sub>
- Rule 15.5 is enforced by CA1860 and review of repeated linear scans and unsized collections in hot paths.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:115-115` · high · sha:d9e3bb5add69</sub>
- Rule 15.6 is enforced by CA1834 (use `StringBuilder.Append(char)`) and review rejecting string concatenation inside loops.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:130-130` · high · sha:d9e3bb5add69</sub>
- Rule 15.7 is enforced by CA1822 (mark members static), CA1859 (use concrete types where possible), the sealed default, and review favouring source generators over reflection.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:145-145` · high · sha:d9e3bb5add69</sub>
- Rule 15.8 is enforced by BenchmarkDotNet results and profiler evidence required in review for any performance-motivated change, Server GC configured for services, and LOH allocations flagged in review.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:162-162` · high · sha:d9e3bb5add69</sub>
- Rule 15.1 is enforced by review of hot-path slicing; an unbounded `stackalloc` is a review finding; a benchmark confirms the allocation win.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:49-49` · high · sha:d9e3bb5add69</sub>
- Rule 15.2 is enforced by CA1860, profiler/allocation evidence in review for hot paths, and boxing flagged in review.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:65-65` · high · sha:d9e3bb5add69</sub>
- Rule 15.3 is enforced by review of large-struct passing, CA1815 (override equality on value types), and `readonly struct` required on value types.
  <sub>styleguide · `docs/styleguide/csharp/15-performance.md:80-80` · high · sha:d9e3bb5add69</sub>
- Chapter 15 covers Span<T>/ReadOnlySpan<T>, bounded stackalloc, avoiding boxing/closures/LINQ in hot paths, in/ref readonly, ValueTask, ArrayPool, BenchmarkDotNet, and network > disk > memory > CPU.
  <sub>styleguide · `docs/styleguide/csharp/README.md:47-47` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded

