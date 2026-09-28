## C# Code Style

Binding C# rules for all dexpace projects; extends the [.NET Runtime C# Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md). Target **.NET 10 (LTS)** and **C# 14**, `<Nullable>enable</Nullable>` everywhere, `dotnet format` for tooling. Prioritize **correctness**, **explicitness**, **simplicity** — never cleverness, never a silenced warning, never inheritance for code reuse.

This guide is platform-agnostic. It covers the C# language, its nullable-reference type system, and runtime-neutral idioms. Hosting-specific rules live in the companion guide: web and service concerns in [csharp-aspnetcore](../csharp-aspnetcore/).

### Authorities

This guide extends and defers to an ordered chain. Where they conflict, the higher authority wins:

1. **[.NET Runtime C# Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)** — canonical. The runtime is the most-scrutinized, most performance-critical C# codebase in existence, and its 20 rules are battle-tested over a decade of open-source review. Where our guidance collides with it, **the runtime style wins**, except for the deliberate deviations recorded in the ledger below.
2. **[MS Learn C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)** + **[identifier names](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)** — the docs-team adaptation of the runtime style. They **fill the gaps** the runtime guide leaves open (LINQ layout, `using` placement, modern-feature adoption). Defer to them only where the runtime is silent; where the docs guide relaxes a rule for *teaching* reasons (it forbids no construct), the runtime's stricter line holds.
3. **`.editorconfig` + `dotnet format` + Roslyn analyzers** — the tooling embodiment of the above. The analyzer baseline (`<AnalysisLevel>latest-Recommended</AnalysisLevel>`, `<TreatWarningsAsErrors>true`) and the formatter are final; formatting is a non-discussion.
4. **This guide's overlay** — the dexpace layer on top: the Tiger Style discipline (assertion density, bounded everything, no recursion, zero debt), the 70-line method cap, nullable-reference types as law with the null-forgiving `!` banned, and records-and-functions over class hierarchies.

### Values

**Correctness > performance > developer experience.** This is the root README's ordering; this guide refines "developer experience" into **simplicity > expressiveness**. When rules conflict, that order decides.

- **Correctness** comes first because a fast, simple, expressive program that computes the wrong answer is worthless. It implies every feasible kind of testing: unit, property-based, mutation, integration, end-to-end. The nullable-reference flow analysis is the first test suite, and most of chapter 03 is about not lying to it with `!`.
- **Performance** comes before simplicity because the right architecture is chosen once, at design time, and is expensive to retrofit. Work with the grain of the CLR and the JIT: stable types, few allocations, `Span<T>` over copies. Optimize the slowest resource first: network > disk > memory > CPU.
- **Simplicity** is the simplest approach that accomplishes the goal: no abstraction for its own sake, no premature interface, no cleverness. When two correct, fast-enough designs differ, the simpler one wins.
- **Expressiveness** comes last because clarity is worth nothing if the code is wrong, slow, or needlessly complex; rule 2 makes it concrete.

All four are held to one standard of elegant, well-structured code, enforced through the chapters' rules and exemplars rather than aspired to as a rank in the priority order.

---

## Table of Contents

| # | Document | Scope |
|---|----------|-------|
| 01 | [Formatting & Tooling](./01-formatting-and-tooling.md) | `dotnet format` + `.editorconfig`; Allman, four spaces, file-scoped namespaces; `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>`, `<AnalysisLevel>`; C# 14 / .NET 10; 70-line cap |
| 02 | [Naming Conventions](./02-naming-conventions.md) | PascalCase types/members, `_camelCase`/`s_`/`t_` fields, camelCase locals/params, `nameof`, language keywords over BCL types, no `I` prefix, no `Async` suffix |
| 03 | [Nullability & the Type System](./03-nullability-and-the-type-system.md) | NRT as law, `!` null-forgiving banned, no `#nullable disable`, `?` annotations, `required`, struct vs class vs record, `dynamic` banned, `default!` only at proven boundaries |
| 04 | [Variables & Declarations](./04-variables-and-declarations.md) | `var` only when the type is named on the RHS, target-typed `new` when named on the LHS, `readonly` default, `const`, explicit visibility, no `this.`, `unsafe` quarantined |
| 05 | [Methods & Functions](./05-methods-and-functions.md) | 70-line cap, expression-bodied one-liners, guard clauses + `ArgumentNullException.ThrowIfNull`, step-down rule, options record for ≥3 params, 2+ assertions, local functions |
| 06 | [Types & Data Modeling](./06-types-and-data-modeling.md) | Records for data; `sealed` by default; make illegal states unrepresentable via closed hierarchies + pattern matching; `readonly struct` for small values; `init`-only; no inheritance for reuse |
| 07 | [C# Idioms](./07-csharp-idioms.md) | Pattern matching + `switch` expressions, LINQ pipelines, collection expressions `[..]`, ranges/indices, `is not null`, `??`/`?.`, raw strings + interpolation, `nameof` |
| 08 | [Error Handling](./08-error-handling.md) | Specific exception types, no bare `catch (Exception)` without a filter, mandatory `throw;` + inner-exception chaining, `ThrowIf` helpers, opt-in `Result` for expected failures, no control-flow exceptions |
| 09 | [Concurrency & Async](./09-concurrency.md) | async/await throughout, `async void` banned, `CancellationToken` + timeout everywhere, `ConfigureAwait(false)` in libraries, no `.Result`/`.Wait()`, `Channel<T>`, bounded parallelism |
| 10 | [API Design](./10-api-design.md) | Minimal public surface, immutable record DTOs, `IReadOnlyList` returns, nullable annotations as contract, `required` + `init`, `[Obsolete]` + semver, public-API analyzer, `internal` by default |
| 11 | [Testing](./11-testing.md) | xUnit v3 on Microsoft.Testing.Platform, FsCheck property tests, fakes over mocks (NSubstitute when needed), Shouldly not FluentAssertions, `WebApplicationFactory` + Testcontainers, `TimeProvider`, determinism |
| 12 | [Project & Assembly Organization](./12-project-organization.md) | File-scoped namespaces matching folders, one top-level type per file, `Directory.Build.props`, curated global usings, acyclic project references, `internal` default |
| 13 | [Resource Management](./13-resource-management.md) | `IDisposable`/`IAsyncDisposable`, `using` declarations, dispose pattern only when owning, `ArrayPool`/`MemoryPool`, bounded pools/channels/caches, `CancellationTokenSource` disposal |
| 14 | [Documentation](./14-documentation.md) | XML `///` doc on public API, `<inheritdoc/>`, never restate types, why-comments, `<example>` on non-obvious publics, `GenerateDocumentationFile`, no commented-out code |
| 15 | [Performance](./15-performance.md) | `Span<T>`/`ReadOnlySpan<T>`, bounded `stackalloc`, avoid boxing/closures/LINQ in hot paths, `in`/`ref readonly`, `ValueTask`, `ArrayPool`, BenchmarkDotNet, network > disk > memory > CPU |

---

## Cross-Cutting Concerns

Security, performance, and git practices are covered in the [root-level code style guide](../README.md). The cross-cutting docs are language-agnostic; this guide adapts them to C#. Hosting-specific concerns — minimal APIs, dependency injection, EF Core, configuration — are in [csharp-aspnetcore](../csharp-aspnetcore/).

## The 12 Rules in C#

The root README defines twelve rules for every dexpace project. Here they are in C#'s vocabulary.

1. **Data and functions, not objects.** Model state as `record` types and `readonly struct`s; group behaviour into `static` classes of pure methods and small interfaces. Reserve a stateful `class` for lifecycle resources — things you open and close (chapter 13). No inheritance for code reuse: `sealed` is the default and the only base type you write is an abstract closed hierarchy for a discriminated union.
2. **Explicit over implicit.** Code says what it does at the call site and does nothing it did not say. No `dynamic`. No reflection-driven magic in domain code. Nullable annotations are honest — no `!` to paper over a possible null. Every dependency is a constructor parameter, visible in the signature; library options follow their documented defaults, and callers pass only what differs.
3. **Immutable by default.** `readonly` fields, `init`-only properties, `record` over mutable `class`, `IReadOnlyList<T>`/`ReadOnlySpan<T>` in public signatures. Update by `with`-expression into a new value, never by mutation. Mutability is the choice you have to type — that's the right way around.
4. **Errors are values, handled explicitly.** Specific exception types per domain, with the original cause chained as `InnerException` and context attached on rethrow. `catch` only what you can handle, and never bare `Exception` without a `when` filter. Opt-in `Result<T, TError>` where a failure is expected and routine, never mixed within a module. No empty `catch`; a discarded `Task` is a build error.
5. **Composition over inheritance.** `abstract`/`virtual` is for a closed hierarchy backing a discriminated union and nothing else. Closed polymorphism is a sealed hierarchy matched with a `switch` expression; code reuse is delegation through an injected interface. Small interfaces composed together, never a deep class tree.
6. **Transform, don't mutate.** Build pipelines from LINQ (`Select`/`Where`/`Aggregate`); reach for `foreach` only for effects or early exit, and never inside a measured hot path (chapter 15). Methods take input and return new output. State changes are explicit, localized, and named.
7. **Always say why.** XML doc and `//` comments explain reasoning, not mechanics; `<example>` on non-obvious public API. Enforcement notes in the guides name their rule. If you can't say why a line exists, question whether it should.
8. **Assert aggressively.** `Debug.Assert` for internal invariants and the `ArgumentNullException.ThrowIfNull` / `ArgumentOutOfRangeException.ThrowIf*` family for public preconditions. Minimum two per method on average: preconditions at entry, postconditions at exit. Split compound assertions; assert positive and negative space.
9. **Limits on everything.** Methods cap at 70 lines (analyzer-enforced); nesting aims for two levels, three at most. Bound every loop, queue, retry, pool, cache, and fan-out. Timeouts are mandatory on external I/O via a `CancellationToken` from `CancellationTokenSource(TimeSpan)`. No recursion in library code — all execution provably bounded.
10. **Small functions, breathing room.** Aim for 10–30 lines, one level of abstraction each. Guard clauses first so the happy path stays flush left. Separate logical sections with blank lines — whitespace is free, cramped code is unreadable.
11. **Performance from the outset.** Design-time is when 1000× improvements are cheap. Work with the grain of the JIT — stable types, few allocations, `Span<T>` over copies, `ValueTask` where it pays. Optimize the slowest resource first: network > disk > memory > CPU.
12. **Zero technical debt.** What exists meets the design goals. **Perfection over technical debt — debt never gets paid.** Do it right the first time; the second chance may never come.

## Deviations from Upstream

This guide takes the .NET Runtime C# Coding Style as canonical. Two entries below are genuine deviations — places the runtime takes a position we override (the `I` prefix and a stricter line on the null-forgiving operator); the other two are additions the runtime does not address (the 70-line cap and the dropped `Async` suffix). Each is recorded so it can be revisited surgically.

| Rule | Upstream position | Our position | Why |
|---|---|---|---|
| Interface `I` prefix | Runtime/Learn: prefix interfaces with `I` (`IWorkerQueue`) | Dropped on first-party interfaces — name the role (`WorkerQueue`) | House style across all dexpace guides (Go, Kotlin, TS) names the abstraction, not its kind; an interface is a contract, not a Hungarian tag. **Cost, accepted:** diverges from near-universal C# convention and IDE completion habits. **Boundary:** BCL and framework interfaces (`IDisposable`, `IEnumerable<T>`, `ILogger`) keep their names — we never rename what we don't own. See [02-naming-conventions.md](./02-naming-conventions.md). |
| `Async` method suffix | TAP guideline / Learn: suffix `Task`-returning methods with `Async` | Dropped — name the method for what it does (`LoadUser`, not `LoadUserAsync`) | House style names behaviour, not return-type mechanics; a method's `Task<T>` return is visible in its signature. **Cost, accepted:** loses the at-a-glance "must await" cue. **Mitigation:** CS4014 and `CA2007`/`xUnit1031` analyzers flag unawaited and sync-over-async misuse, restoring the safety the suffix bought. See [02-naming-conventions.md](./02-naming-conventions.md). |
| Null-forgiving `!` | Runtime: used where flow analysis can't follow | Banned outside declared bridges and `[MemberNotNull]`-style proofs | `!` is an unchecked assertion to the compiler that rots silently; we parse and prove instead. An addition tightening the runtime's pragmatic stance. See [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). |
| Method size | No upstream cap | 70-line hard cap, analyzer-enforced | Owner decision; Tiger Style discipline; deliberately set at Go's level. See [05-methods-and-functions.md](./05-methods-and-functions.md). |

## Influences

- **[.NET Runtime C# Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)** — canonical authority. Where our guidance collides, the runtime guide wins (save the deviations above).
- **[MS Learn C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)** and **[identifier names](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)** — the docs-team adaptation; fills the gaps the runtime guide leaves open.
- **[.NET Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)** — the canon for public-API shape, immutability, and exception design (chapter 10).
- **[Roslyn analyzers / .NET code-quality rules (CA)](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/)** — the enforcement substrate; most rules below cite a CA or IDE diagnostic.
- **TigerBeetle Tiger Style** — assertion density, the 70-line method cap, limits on everything, no recursion, zero technical debt.

## Applying Style Changes

When adopting a new rule or migrating away from a deprecated pattern, apply the change at the **assembly / namespace level or larger** — never mix two styles within the same project. A half-migrated assembly is more confusing than either end state.
