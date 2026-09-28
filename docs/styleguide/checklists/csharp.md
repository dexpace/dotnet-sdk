# C# styleguide — review checklist

One section per chapter. Walk top to bottom for a full audit; for a quick edit, the SKILL.md digest is enough.

### 01 — Formatting & Tooling

- Centralize build config in `Directory.Build.props`; keep `.csproj` files thin.
- Treat every warning as an error (`<TreatWarningsAsErrors>true`).
- Run Roslyn analyzers at build with code style enforced (`<AnalysisLevel>latest-Recommended</AnalysisLevel>`).
- Set `<Nullable>enable</Nullable>` solution-wide; never weaken it per file.
- Format with `.editorconfig` + `dotnet format`: Allman braces, four spaces, file-scoped namespaces.
- Pin the SDK; keep the toolchain reproducible.
- Cap method length at 70 lines via the analyzer.

### 02 — Naming Conventions

- PascalCase every type and member; language keywords for built-in types.
- camelCase locals, parameters, and primary-constructor parameters.
- Prefix instance fields `_`, statics `s_`, thread-statics `t_`; make them `readonly`.
- PascalCase all constants, local and field alike.
- Use `nameof` instead of string literals for member and parameter names.
- Name first-party interfaces for their role, no `I` prefix (BCL interfaces keep theirs).
- Drop the `Async` suffix; name a method for what it does.
- Prefix descriptive generic type parameters with `T`; bare `T` only when one says it all.

### 03 — Nullability & the Type System

- Honour the nullable annotations; never silence a nullable warning in the project file.
- Receive untrusted input as nullable; parse into a non-null domain type at the boundary.
- Ban the null-forgiving `!` outside declared bridges and proven post-conditions.
- Narrow with the weakest tool that works: `is null`/`is not null`, then pattern, then a typed check.
- Ban `dynamic`; accept `object` only at the edge and pattern-match it inward.
- Teach flow analysis with nullable attributes (`[MemberNotNull]`, `[NotNullWhen]`), not `!`.
- Brand domain primitives so the compiler refuses to confuse them.
- Choose `record`, `record struct`, or `class` by value semantics, not habit.

### 04 — Variables & Declarations

- Use `var` only when the type is named on the right; write the type otherwise.
- Use target-typed `new()` only when the type is named on the left.
- Make every binding `readonly` by default; justify reassignment.
- `const` for compile-time constants, `static readonly` for runtime ones, in that modifier order.
- State visibility explicitly, as the first modifier on every declaration.
- Avoid `this.`; let the `_` field prefix disambiguate.
- One thing per line, one statement per line.
- Quarantine `unsafe` and pointers to one declared, justified module.
- Inline `out var` at the call site for the Try-pattern; keep `ref`/`in`/`out` disciplined.

### 05 — Methods & Functions

- Cap a method at 70 lines; aim 10–30 at one level of abstraction.
- Guard clauses first so the happy path stays flush left.
- Assert aggressively: at least two assertions per method on average.
- Expression body only for a genuine one-liner; a block otherwise.
- Pass an options record once a method takes three or more parameters.
- Never pass a boolean flag selecting behaviour; split into two named methods.
- Follow the step-down rule; make single-use helpers `static` local functions.
- Forbid recursion in library code; use bounded iteration.

### 06 — Types & Data Modeling

- Model immutable data as `record`; update with `with`, never by mutation.
- Make every class `sealed` by default.
- Make illegal states unrepresentable: a closed hierarchy matched exhaustively, not a nullable bag.
- Parse, don't validate: a factory returns the proven type or a typed failure, never a bool plus raw data.
- Force construction-time completeness with `required` and `init`, not multi-step setters.
- `readonly struct` for small immutable values; never a large or public mutable struct.
- Reuse code by composing injected interfaces, never by inheriting a base class.
- Give every enum an explicit underlying type and the right singular/plural name; promote to a hierarchy when behaviour attaches.

### 07 — C# Idioms

- Match with patterns and `switch` expressions, not if/else chains or type-test casts.
- Transform with LINQ pipelines; `foreach` only for side effects, early exit, or a measured hot path.
- Build collections with collection expressions `[...]` and the spread `..`.
- Slice with ranges `..` and indices `^` instead of arithmetic on `Length`.
- Flow nulls with `??`, `??=`, and `?.` where they read clearer.
- Interpolate short strings, raw-literal multi-line and quote-heavy text, `StringBuilder` in loops.
- Use `nameof` over string literals; keep tuples and deconstruction to local multi-value returns only.
- Prefer the standard idiom to cleverness: readability beats a one-liner needing a comment.

### 08 — Error Handling

- Throw a specific exception type; never bare `Exception` or `ApplicationException`.
- Catch only what you can handle; gate any broad catch with a `when` filter.
- Rethrow with `throw;`; never `throw ex;`; chain the cause when wrapping.
- Never swallow: no empty `catch`, no catch-and-ignore.
- Validate preconditions with the `ThrowIf` family, not a defensive `try`/`catch`.
- Custom exceptions derive from `Exception`, carry context, drop `[Serializable]`.
- Return a `Result` or use the Try-pattern for expected failures; never throw for control flow.
- Treat `OperationCanceledException` as cooperative cancellation, not an error.

### 09 — Concurrency & Async

- Async all the way down; never block with `.Result`, `.Wait()`, or `GetAwaiter().GetResult()`.
- Ban `async void`; async methods return `Task`, `Task<T>`, or `ValueTask`.
- Thread a `CancellationToken` through every async method as the last parameter and honour it.
- Call `ConfigureAwait(false)` on every await in library code.
- Use `ValueTask`/`ValueTask<T>` for hot, often-synchronous paths, awaited exactly once.
- Fan out with `Task.WhenAll`; bound the degree of parallelism, never fire-and-forget.
- Route producer/consumer through a bounded `Channel<T>`; stream with `IAsyncEnumerable<T>`.
- Prefer immutable data and `Interlocked`/`SemaphoreSlim` to `lock`; keep any lock tiny and `await`-free.

### 10 — API Design

- Default every type and member to `internal`; widen to `public` only for a caller that needs it.
- Make DTOs and return types immutable `record`s with `init` and `required`.
- Accept the narrowest useful interface; return a read-only type, never a mutable collection.
- Treat the nullable annotations as the contract; a non-nullable signature is a promise.
- Put `CancellationToken` last on every async or long-running public method.
- Evolve compatibly: mark removed or changed members `[Obsolete]` with a message; follow semver.
- Track the public surface with the public-API analyzer so every addition is a reviewed diff.
- Prefer named static factory methods to overloaded constructors; design for API symmetry.

### 11 — Testing

- Run xUnit v3 on Microsoft.Testing.Platform, one isolated test project per production assembly.
- Structure each test Arrange-Act-Assert; name it for the behaviour under test.
- Write property-based tests with FsCheck for invariants and round-trips.
- Prefer hand-written fakes to mocks; NSubstitute only when a fake is impractical, never Moq.
- Assert with xUnit's `Assert` plus Shouldly; do not adopt FluentAssertions v8+.
- Make every test deterministic: inject `TimeProvider`, seed randomness, no `Thread.Sleep` or real network.
- Make integration tests exercise the real thing: `WebApplicationFactory`, Testcontainers for the database.
- Cover the negative space (invalid input, cancellation, boundaries), not only the happy path.

### 12 — Project & Assembly Organization

- Use file-scoped namespaces mirroring the folder path exactly.
- One top-level type per file; name the file after the type.
- Centralize package versions with Central Package Management; declare a version once.
- Organize by feature folder, not technical layer.
- Make `internal` the default visibility; widen to `public` only across an intended API boundary.
- Keep project references a directed acyclic graph pointing inward toward the domain.
- Curate global usings in one committed `GlobalUsings.cs`; never auto-generate them.
- Split assemblies along bounded-context seams; keep the host free of business logic.
- Enforce the dependency direction with an architecture test.

### 13 — Resource Management

- Implement `IDisposable` for anything owning disposables, `IAsyncDisposable` when cleanup is async; prefer `using` to manual `try/finally`.
- Scope a disposable with a `using` declaration where it reads clearer than nesting.
- Implement the full dispose pattern only when directly holding an unmanaged resource.
- Dispose what you create; never dispose a dependency injected into you.
- Never block in `Dispose`; expose `IAsyncDisposable` when cleanup is asynchronous.
- Rent transient buffers from a pool, bound their size, return them in a `finally`.
- Bound every pool, channel, cache, and queue.
- Reuse `HttpClient` through `IHttpClientFactory`; dispose a `CancellationTokenSource`.

### 14 — Documentation

- Document every public type and member with `<summary>`, plus the params, returns, and exceptions a caller must handle.
- Document the contract once with `<inheritdoc/>` on overrides and interface implementations.
- Never restate the type or the obvious; document what the signature cannot say.
- Write `//` comments that explain why, not what; capitalize, punctuate, prefer `//` to block comments.
- Put a worked `<example>` with `<code>` on non-obvious public API.
- Turn on `<GenerateDocumentationFile>` and treat CS1591 as an error for published libraries.
- Link with compiler-checked `<see cref="..."/>` and `<paramref/>`, never bare prose names.
- Delete commented-out code; a `// TODO` or `// HACK` ships only with a tracked issue.

### 15 — Performance

- Slice and parse with `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`; bound every `stackalloc`.
- In a measured hot path, eliminate hidden allocations: LINQ, capturing lambdas, `params`, boxing.
- Pass large readonly structs by `in`, return by `ref readonly`; mark value types `readonly struct`.
- Use `ValueTask` for hot, often-synchronous async; rent buffers from `ArrayPool<T>`.
- Presize collections, prefer hashed lookups over linear scans, reach through spans to avoid re-hashing.
- Build strings with `StringBuilder`, interpolated-string handlers, or `string.Create`; never concatenate in a loop.
- Seal types to let the JIT devirtualize, cache delegates with `static` lambdas, favour AOT-friendly code.
- Measure before optimizing: benchmark with BenchmarkDotNet, profile real workloads, optimize the slowest resource first.
