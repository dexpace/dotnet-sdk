# declarations

## Rules
- Instance fields carry a `_` prefix and are `readonly` by default, while static readonly fields use an `s_` prefix as shown in the chapter's model example.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:12-14` · medium · sha:2874996a58cb</sub>
- Use `var` only when the type is named on the right-hand side (a `new`, an explicit cast, or a typed literal); otherwise write the type explicitly on the left.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:36-40` · high · sha:2874996a58cb</sub>
- A method-call, indexer or other expression whose type is invisible must not be assigned to `var`; write `decimal total = Compute(order)` rather than `var total = Compute(order)`.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:39-39` · high · sha:2874996a58cb</sub>
- Target-typed `new()` is used only when the type is named on the left of a declaration, including a field initializer whose declared type names it.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:51-54` · high · sha:2874996a58cb</sub>
- Target-typed `new()` is never used on a reassignment, as a bare method argument (`Process(new())`), or in `return new(...)`; write the type explicitly in those positions.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:55-55` · high · sha:2874996a58cb</sub>
- Every field and local is `readonly` or bound once by default; reassignment must be justified, such as a running total or retry counter.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:66-70` · high · sha:2874996a58cb</sub>
- Prefer rebuilding a value with a `with` expression or LINQ `Aggregate` over in-place mutation, and keep any mutable local's scope as small as its driving loop.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:70-70` · high · sha:2874996a58cb</sub>
- `const` is reserved for compile-time constants (literal int, string, bool, enum) written in PascalCase; runtime-computed values (TimeSpan.FromMinutes(5), new Regex(...), arrays) use `static readonly`.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:81-85` · high · sha:2874996a58cb</sub>
- Keep public `const` to genuinely eternal values and prefer `static readonly` across an assembly boundary.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:84-84` · high · sha:2874996a58cb</sub>
- The modifier order is `static readonly`, never `readonly static`.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:85-85` · high · sha:2874996a58cb</sub>
- Visibility is stated explicitly on every type and member as the first modifier, ahead of `static`, `readonly`, `sealed`, `async` and the rest.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:96-100` · high · sha:2874996a58cb</sub>
- Types default to `internal` and members to `private`, widening only with a reason.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:99-99` · high · sha:2874996a58cb</sub>
- `this.` is not used to qualify member access because the `_` field prefix disambiguates fields from parameters and locals.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:110-113` · high · sha:2874996a58cb</sub>
- `this` is used only when passing or returning the current instance as a value, such as registering with an event source, a fluent return, or an extension call on `this`.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:114-114` · high · sha:2874996a58cb</sub>
- Declare one thing per line and one statement per line: no `int x, y;` multi-declarators and no `if (x) DoA(); DoB();` single-line statement chains.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:124-128` · high · sha:2874996a58cb</sub>
- `unsafe` and pointers are quarantined to one declared, named interop or performance module, never sprinkled inline to shave a bounds check.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:139-142` · high · sha:2874996a58cb</sub>
- `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` is set only in that single project, never in `Directory.Build.props` or solution-wide.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:142-142` · high · sha:2874996a58cb</sub>
- Unsafe code is considered only when a measured hot path needs raw pointers or `stackalloc` beyond the safe `Span<T>` surface.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:142-142` · high · sha:2874996a58cb</sub>
- An unsafe module carries a why-comment stating the benchmark or native contract justifying it, and its unsafe surface is wrapped in a safe, validated API so callers never touch a pointer.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:143-143` · high · sha:2874996a58cb</sub>
- The Try-pattern's `out` parameter is declared inline at the call site with `out var`, which is the one idiomatic `var` without a named right-hand side.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:158-161` · high · sha:2874996a58cb</sub>
- Prefer `in` (or `ref readonly`) to pass a large `readonly struct` without copying, and reserve `ref` for genuine in-place mutation of a caller's value.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:162-162` · high · sha:2874996a58cb</sub>
- Do not use `out` parameters to return multiple results; return a tuple or a `record` instead (CA1021).
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:162-162` · high · sha:2874996a58cb</sub>

## Constraints
- `var` is required, not merely allowed, for anonymous types and LINQ projections of anonymous types because they have no nameable type.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:40-40` · high · sha:2874996a58cb</sub>
- A `const` is inlined into every referencing assembly, so changing a public `const` has no effect on consumers until they recompile.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:84-84` · high · sha:2874996a58cb</sub>

## Conclusions
- Exactly one side of a declaration always names the type: `var` rules put it on the right, and target-typed `new()` rules put it on the left.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:54-54` · medium · sha:2874996a58cb</sub>
- One declaration per line is chosen so diffs, breakpoints and version-control attribution target exactly one binding.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:127-127` · medium · sha:2874996a58cb</sub>

## Reference
- Enforcement for rule 4.1 is IDE0007/IDE0008 configured with `for_built_in_types` and `when_type_is_apparent` in `.editorconfig`, plus review.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:49-49` · high · sha:2874996a58cb</sub>
- Enforcement for rule 4.2 is IDE0090 with `prefer_simplified_object_creation` scoped to declarations; review rejects target-typed `new()` in argument and return position.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:64-64` · high · sha:2874996a58cb</sub>
- Enforcement for rule 4.3 is IDE0044 (make field readonly), CA1805, and review flagging unjustified mutable locals.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:79-79` · high · sha:2874996a58cb</sub>
- Enforcement for rule 4.4 is CA1802 (use literals where appropriate), review for the public-const versioning trap, and the IDE0036 modifier-order rule in `.editorconfig`.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:94-94` · high · sha:2874996a58cb</sub>
- Enforcement for rule 4.5 is IDE0040 (add accessibility modifiers) and IDE0036 (order modifiers), both promoted to errors by warnings-as-errors.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:108-108` · high · sha:2874996a58cb</sub>
- Enforcement for rule 4.6 is IDE0003/IDE0009 with `dotnet_style_qualification_for_*` set to false, plus review.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:122-122` · high · sha:2874996a58cb</sub>
- Enforcement for rule 4.7 is `dotnet format` line layout, and review rejects multi-declarator and comma-spliced lines.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:137-137` · high · sha:2874996a58cb</sub>
- Enforcement for rule 4.8 is per-project `<AllowUnsafeBlocks>`, CA1401/CA2101 on P/Invoke, and review requiring the why-comment.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:156-156` · high · sha:2874996a58cb</sub>
- Enforcement for rule 4.9 is IDE0018 (inline variable declaration), CA1021 (avoid `out` parameters), and review of `ref`/`in` usage.
  <sub>styleguide · `docs/styleguide/csharp/04-variables-and-declarations.md:170-170` · high · sha:2874996a58cb</sub>
- Chapter 04 covers var only when the type is named on the right-hand side, target-typed new when the type is named on the left, readonly by default, const, explicit visibility, no this., and unsafe quarantined.
  <sub>styleguide · `docs/styleguide/csharp/README.md:36-36` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded

