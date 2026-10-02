# csharp-idioms

## Rules
- Prefer the standard idiom to a clever one-liner: modern C# constructs must make code denser and clearer, never one at the cost of the other.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:3-3` · high · sha:4438ba065221</sub>
- Match with patterns and switch expressions, not if/else chains or type-test casts.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:41-41` · high · sha:4438ba065221</sub>
- Patterns such as is { } user, is Card { Last4: var last } and obj is JsonElement e test and bind in one step, replacing the is-then-cast and as-then-null-check forms.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:44-44` · high · sha:4438ba065221</sub>
- is not null is the canonical presence test because it cannot be fooled by an overloaded ==.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:44-44` · high · sha:4438ba065221</sub>
- Patterns are used whenever a condition inspects shape, type or structure, and a plain if is reserved for a simple boolean.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:45-45` · high · sha:4438ba065221</sub>
- Transform with LINQ pipelines and reach for foreach only for side effects, early exit or a measured hot path.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:58-58` · high · sha:4438ba065221</sub>
- A LINQ query result is given a meaningful name such as activeUsers or notable, not query or q.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:61-61` · high · sha:4438ba065221</sub>
- foreach is used when the loop body has an effect, such as writing to a channel or logging each item, or needs an early exit by break or return.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:62-62` · high · sha:4438ba065221</sub>
- Inside a benchmarked hot path, a plain foreach or for over a Span<T> is used instead of LINQ, because LINQ allocates iterators and closures; this is the only place clarity yields to the profiler.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:62-62` · high · sha:4438ba065221</sub>
- Collections are built with collection expressions [...] and the spread operator ..
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:74-74` · high · sha:4438ba065221</sub>
- The spread element .. other inlines an existing sequence into a new collection instead of AddRange on a pre-allocated list.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:78-78` · high · sha:4438ba065221</sub>
- [] is used for an empty collection, including as the default for an init-only property.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:78-78` · high · sha:4438ba065221</sub>
- Slice with ranges .. and indices ^ instead of arithmetic on Length.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:87-87` · high · sha:4438ba065221</sub>
- Ranges and indices are used wherever a sub-sequence is taken, and they pair with spans in parsing and hot-path work.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:91-98` · medium · sha:4438ba065221</sub>
- Null flow uses ??, ??= and ?. where they read clearer than a verbose check.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:100-100` · high · sha:4438ba065221</sub>
- A branch that does different work per null/non-null outcome is not contorted into a nested ternary to avoid an if; use is not null with two blocks instead.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:104-104` · high · sha:4438ba065221</sub>
- Short flat strings use interpolation, multi-line or quote/brace/backslash-heavy text (JSON, SQL, regex, paths) uses a raw string literal """...""", and accumulation in a loop uses StringBuilder.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:113-117` · high · sha:4438ba065221</sub>
- A simple separator-joined sequence is built with string.Join or StringBuilder rather than += in a loop.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:117-117` · high · sha:4438ba065221</sub>
- nameof(member) is used instead of any string literal that echoes an identifier, such as argument-exception names, logging keys and property-change notifications.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:128-131` · high · sha:4438ba065221</sub>
- Tuples with deconstruction are used only for local multi-value returns.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:128-132` · high · sha:4438ba065221</sub>
- Tuples never appear in a public signature; a named record is returned instead so the contract is self-describing.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:132-132` · high · sha:4438ba065221</sub>
- Readability beats a one-liner that needs a comment; the standard idiom is preferred to cleverness.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:142-142` · high · sha:4438ba065221</sub>
- When two correct forms differ, the one a competent C# reader parses without pausing is chosen: named pipeline over dense aggregate, switch expression over packed ternary, two-line if over contorted ??.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:146-146` · high · sha:4438ba065221</sub>
- A line that needs a comment to say what it does, as opposed to why, is rewritten into the standard idiom and the comment is deleted.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:146-146` · high · sha:4438ba065221</sub>
- The C# guide binds all dexpace projects and extends the .NET Runtime C# Coding Style.
  <sub>styleguide · `docs/styleguide/csharp/README.md:3-3` · high · sha:1e6ba36fc337</sub>
- dexpace C# projects target .NET 10 (LTS) and C# 14 with <Nullable>enable</Nullable> everywhere and use dotnet format for tooling.
  <sub>styleguide · `docs/styleguide/csharp/README.md:3-3` · high · sha:1e6ba36fc337</sub>
- C# code prioritizes correctness, explicitness and simplicity, and never uses cleverness, a silenced warning, or inheritance for code reuse.
  <sub>styleguide · `docs/styleguide/csharp/README.md:3-3` · high · sha:1e6ba36fc337</sub>
- Where dexpace guidance collides with the .NET Runtime C# Coding Style, the runtime style wins, except for the deliberate deviations recorded in the deviations ledger.
  <sub>styleguide · `docs/styleguide/csharp/README.md:11-11` · high · sha:1e6ba36fc337</sub>
- MS Learn C# conventions apply only where the runtime style is silent (LINQ layout, using placement, modern-feature adoption), and where Learn relaxes a rule for teaching reasons the runtime's stricter line holds.
  <sub>styleguide · `docs/styleguide/csharp/README.md:12-12` · high · sha:1e6ba36fc337</sub>
- Simplicity means the simplest approach that accomplishes the goal, with no abstraction for its own sake, no premature interface and no cleverness; when two correct, fast-enough designs differ, the simpler wins.
  <sub>styleguide · `docs/styleguide/csharp/README.md:22-22` · high · sha:1e6ba36fc337</sub>
- Correctness, performance, simplicity and expressiveness are all held to one standard of elegant, well-structured code, enforced through chapter rules and exemplars rather than as a rank in the priority order.
  <sub>styleguide · `docs/styleguide/csharp/README.md:25-25` · high · sha:1e6ba36fc337</sub>
- Code says what it does at the call site and does nothing it did not say; no dynamic and no reflection-driven magic in domain code.
  <sub>styleguide · `docs/styleguide/csharp/README.md:60-60` · high · sha:1e6ba36fc337</sub>
- Every dependency is a constructor parameter visible in the signature, and library options follow their documented defaults with callers passing only what differs.
  <sub>styleguide · `docs/styleguide/csharp/README.md:60-60` · high · sha:1e6ba36fc337</sub>
- Build pipelines from LINQ (Select/Where/Aggregate) and use foreach only for effects or early exit, never inside a measured hot path.
  <sub>styleguide · `docs/styleguide/csharp/README.md:64-64` · high · sha:1e6ba36fc337</sub>
- Methods take input and return new output, and state changes are explicit, localized and named.
  <sub>styleguide · `docs/styleguide/csharp/README.md:64-64` · high · sha:1e6ba36fc337</sub>

## Constraints
- Repeated += on a string in a loop reallocates the whole buffer each pass because strings are immutable, producing quadratic copying.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:117-117` · high · sha:4438ba065221</sub>

## Conclusions
- A switch expression is preferred over an if/else ladder because it evaluates to a value, pairs one input with one result per arm, and has compiler-checked coverage, whereas a ladder scatters the decision across mutable assignments where a missed branch is silent.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:44-44` · high · sha:4438ba065221</sub>
- A Where/Select/Aggregate pipeline is preferred because it reads as the named sequence of transformations with no accumulator to initialize and mutate and no off-by-one.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:61-61` · high · sha:4438ba065221</sub>
- nameof is chosen over bare literals because it is a compile-time constant that a rename updates and a typo fails to compile, whereas a bare literal duplicates the symbol and rots into a lie.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:131-131` · high · sha:4438ba065221</sub>
- Expressiveness is last in the priority order because clever dense lines cost every future reader decoding time.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:145-146` · medium · sha:4438ba065221</sub>
- Value ordering is correctness > performance > developer experience, with developer experience refined as simplicity > expressiveness, and this order decides when rules conflict.
  <sub>styleguide · `docs/styleguide/csharp/README.md:18-18` · high · sha:1e6ba36fc337</sub>
- Correctness ranks first because a fast, simple, expressive program that computes the wrong answer is worthless, and it implies every feasible kind of testing: unit, property-based, mutation, integration, end-to-end.
  <sub>styleguide · `docs/styleguide/csharp/README.md:20-20` · high · sha:1e6ba36fc337</sub>
- Expressiveness ranks last because clarity is worth nothing if the code is wrong, slow or needlessly complex.
  <sub>styleguide · `docs/styleguide/csharp/README.md:23-23` · high · sha:1e6ba36fc337</sub>

## Reference
- A LINQ pipeline with a meaningfully named query variable is used for filter-and-project, and a collection expression with spread builds a header line in one target-typed step.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:14-21` · high · sha:4438ba065221</sub>
- Property, positional, relational, logical (and/or/not) and list patterns compose into a single readable test.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:45-45` · high · sha:4438ba065221</sub>
- Pattern-matching style is enforced by IDE0066 (use switch expression), IDE0078 and IDE0260 (use pattern matching), and by review of type-test-then-cast.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:56-56` · high · sha:4438ba065221</sub>
- Review prefers a pipeline for pure transforms, and the performance chapter governs hot-path loops.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:72-72` · high · sha:4438ba065221</sub>
- A collection expression is target-typed, taking element type and concrete collection from the left-hand side, and replaces new[] { ... }, new List<T> { ... } and Array.Empty<T>().
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:77-77` · high · sha:4438ba065221</sub>
- The compiler picks an efficient construction for a collection expression target, so the dense form is also the fast form.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:78-78` · medium · sha:4438ba065221</sub>
- Collection-expression style is enforced by IDE0300 and IDE0305 (use collection expression), IDE0301 (empty collection) and IDE0090 (target-typed new).
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:85-85` · high · sha:4438ba065221</sub>
- Range endpoints take an inclusive start and exclusive end, so 0..^0 is the whole sequence.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:90-90` · high · sha:4438ba065221</sub>
- A range over an array or string allocates a copy, whereas a range over a Span<T> or ReadOnlySpan<T> slices a view with zero allocation.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:91-91` · high · sha:4438ba065221</sub>
- ?. short-circuits member access on a null receiver, ?? supplies a fallback for a null left side, and ??= assigns only when the target is currently null.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:103-103` · high · sha:4438ba065221</sub>
- Null-operator style is enforced by IDE0031 (use null propagation) and IDE0270 (use coalesce expression), and review rejects a contorted ternary.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:111-111` · high · sha:4438ba065221</sub>
- String-building style is enforced by review, with the performance chapter citing CA1834 and StringBuilder.Append(char).
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:126-126` · high · sha:4438ba065221</sub>
- Tuple and nameof style is enforced by CA2208 (instantiate argument exceptions correctly), and review rejects tuples in public signatures.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:140-140` · high · sha:4438ba065221</sub>
- Idiom style is enforced by review, which rejects a line that requires a what-comment.
  <sub>styleguide · `docs/styleguide/csharp/07-csharp-idioms.md:153-153` · high · sha:4438ba065221</sub>
- The C# style guide is platform-agnostic, covering the language, the nullable-reference type system and runtime-neutral idioms, while web and service rules live in the companion csharp-aspnetcore guide.
  <sub>styleguide · `docs/styleguide/csharp/README.md:5-5` · high · sha:1e6ba36fc337</sub>
- The authority chain, highest first, is the .NET Runtime C# Coding Style, MS Learn C# conventions and identifier names, .editorconfig plus dotnet format plus Roslyn analyzers, then the guide's own overlay; the higher authority wins on conflict.
  <sub>styleguide · `docs/styleguide/csharp/README.md:9-14` · high · sha:1e6ba36fc337</sub>
- The dexpace overlay consists of Tiger Style discipline (assertion density, bounded everything, no recursion, zero debt), the 70-line method cap, nullable-reference types as law with null-forgiving ! banned, and records-and-functions over class hierarchies.
  <sub>styleguide · `docs/styleguide/csharp/README.md:14-14` · high · sha:1e6ba36fc337</sub>
- The C# guide has 15 chapters: 01 Formatting & Tooling, 02 Naming Conventions, 03 Nullability & the Type System, 04 Variables & Declarations, 05 Methods & Functions, 06 Types & Data Modeling, 07 C# Idioms, 08 Error Handling, 09 Concurrency & Async, 10 API Design, 11 Testing, 12 Project & Assembly Organization, 13 Resource Management, 14 Documentation, 15 Performance.
  <sub>styleguide · `docs/styleguide/csharp/README.md:31-47` · high · sha:1e6ba36fc337</sub>
- Chapter 07 covers pattern matching and switch expressions, LINQ pipelines, collection expressions, ranges and indices, is not null, ?? and ?., raw strings with interpolation, and nameof.
  <sub>styleguide · `docs/styleguide/csharp/README.md:39-39` · medium · sha:1e6ba36fc337</sub>
- Security, performance and git practices are covered in the root-level code style guide, whose cross-cutting docs are language-agnostic and adapted to C# by this guide; hosting-specific concerns (minimal APIs, DI, EF Core, configuration) are in csharp-aspnetcore.
  <sub>styleguide · `docs/styleguide/csharp/README.md:53-53` · high · sha:1e6ba36fc337</sub>
- The root README defines twelve rules for every dexpace project, restated in C# vocabulary in this guide.
  <sub>styleguide · `docs/styleguide/csharp/README.md:57-57` · high · sha:1e6ba36fc337</sub>
- The deviations ledger records four entries: the I-prefix drop and the stricter null-forgiving line are genuine overrides of the runtime, while the 70-line cap and the dropped Async suffix are additions the runtime does not address.
  <sub>styleguide · `docs/styleguide/csharp/README.md:74-74` · high · sha:1e6ba36fc337</sub>
- Influences are the .NET Runtime C# Coding Style (canonical), MS Learn conventions and identifier names, the .NET Framework Design Guidelines (public-API shape, immutability, exception design, chapter 10), Roslyn CA analyzers (the enforcement substrate), and TigerBeetle Tiger Style (assertion density, 70-line cap, limits on everything, no recursion, zero debt).
  <sub>styleguide · `docs/styleguide/csharp/README.md:85-89` · high · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded

