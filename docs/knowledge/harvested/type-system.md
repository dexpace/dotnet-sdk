# type-system

## Rules
- Nullable annotations must be honoured, and a nullable warning must never be silenced through the project file, `#nullable disable`, a blanket `<WarningsNotAsErrors>`, or the `!` operator.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:29-34` · high · sha:b01c80abec79</sub>
- The only acceptable responses to a nullable warning are to handle the null, narrow it away, or teach the analyzer the fact with a nullable attribute.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:33-33` · high · sha:b01c80abec79</sub>
- Untrusted input crossing a boundary (deserialized DTO, config value, database row, query-string parameter) is received as a nullable type such as `UserDto?` or `string?`.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:43-46` · high · sha:b01c80abec79</sub>
- At the boundary, nullable loosely typed input is parsed into a non-null, fully validated domain type (a record or branded primitive) so the interior never handles maybe-null values or repeats checks.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:47-47` · high · sha:b01c80abec79</sub>
- Parse, do not validate: a boundary parse returns the proven type rather than a bool.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:47-47` · high · sha:b01c80abec79</sub>
- The null-forgiving operator `!` is banned outside declared interop/bridge sites to un-annotated third-party code and tests asserting a value is present, and each such site needs a why-comment.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:59-63` · high · sha:b01c80abec79</sub>
- Before reaching for `!`, handle the null, narrow with `is not null`, or declare the fact with a nullable attribute.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:63-63` · high · sha:b01c80abec79</sub>
- Null narrowing uses the weakest tool that works, in order: `is null` / `is not null`, then property or positional pattern, then a typed check.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:72-75` · high · sha:b01c80abec79</sub>
- Prefer `is null` / `is not null` over `== null` / `!= null` because they cannot be fooled by an overloaded `==`.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:75-76` · high · sha:b01c80abec79</sub>
- Do not re-fetch a value that has already been null-checked; bind it with a pattern such as `is { Length: > 0 } s`.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:75-76` · high · sha:b01c80abec79</sub>
- A null arriving from an external contract is converted with `??` at the boundary into the interior's chosen absence value, usually an empty collection or domain default, so the interior speaks one absence dialect.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:76-76` · high · sha:b01c80abec79</sub>
- `dynamic` is banned; it is disallowed in domain assemblies and permitted only in declared interop bridges.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:86-98` · high · sha:b01c80abec79</sub>
- A value that genuinely arrives untyped (JsonElement, COM object, reflection result) is received as `object?` or the specific weak carrier such as `JsonElement`, then pattern-matched or parsed into a domain type before the interior sees it.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:90-90` · high · sha:b01c80abec79</sub>
- When a method establishes a nullability fact the compiler cannot infer, declare it once on the method with a nullable attribute (`[NotNullWhen(true)]`, `[MemberNotNull(nameof(_field))]`, `[NotNullIfNotNull]`) instead of asserting with `!` at call sites.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:100-104` · high · sha:b01c80abec79</sub>
- Domain primitives are branded by wrapping them in a `readonly record struct` with a private constructor and a validating factory so UserId, OrderId and raw Guid are nominally distinct types.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:117-121` · high · sha:b01c80abec79</sub>
- Type kind is chosen by value semantics: a `record` is the default for immutable data with value equality and `with` updates.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:135-138` · high · sha:b01c80abec79</sub>
- A `readonly record struct` is used for small (about 16 bytes or less), immutable, hot-path values where avoiding heap allocation matters and copying is cheap.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:138-138` · high · sha:b01c80abec79</sub>
- A `class` is reserved for entities with identity and lifecycle, such as things opened, mutated under control, and closed; a plain mutable class is used only when identity and in-place mutation are the point.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:138-139` · high · sha:b01c80abec79</sub>
- Large mutable structs are avoided, and a public mutable struct field is never exposed because the copy is mutated rather than the original.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:139-139` · high · sha:b01c80abec79</sub>
- Nullable annotations must be honest, so ! is never used to paper over a possible null.
  <sub>styleguide · `docs/styleguide/csharp/README.md:60-60` · high · sha:1e6ba36fc337</sub>
- The null-forgiving ! is banned outside declared bridges and [MemberNotNull]-style proofs, whereas the runtime uses it where flow analysis cannot follow.
  <sub>styleguide · `docs/styleguide/csharp/README.md:80-80` · high · sha:1e6ba36fc337</sub>

## Constraints

## Conclusions
- The styleguide deliberately tightens the runtime's more pragmatic stance on `!`, treating it as the nullable analog of an unverified `as` cast because a wrong `!` surfaces as a NullReferenceException far from its cause.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:62-62` · medium · sha:b01c80abec79</sub>
- `dynamic` is rejected because it disables the type system for the value and everything derived from it, turning typos into RuntimeBinderException at runtime, and one `dynamic` infects its call chain.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:89-89` · high · sha:b01c80abec79</sub>
- Branding costs one allocation-free struct and earns compile errors on swapped arguments plus a single sanctioned validation point in the factory; it pairs with parsing at the boundary.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:121-121` · high · sha:b01c80abec79</sub>
- The null-forgiving ! is banned because it is an unchecked compiler assertion that rots silently, so dexpace parses and proves instead.
  <sub>styleguide · `docs/styleguide/csharp/README.md:80-80` · high · sha:1e6ba36fc337</sub>

## Reference
- The styleguide's nullable discipline depends on the nullable flag and warnings-as-errors configured in chapter 01.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:3-3` · medium · sha:b01c80abec79</sub>
- With `<Nullable>enable</Nullable>` the compiler warns with CS8600-CS8655 when code dereferences, returns, or assigns something it cannot prove non-null.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:32-32` · high · sha:b01c80abec79</sub>
- Enforcement for rule 3.1 is CS86xx promoted to errors by `<TreatWarningsAsErrors>`, and `#nullable disable` is a review finding.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:41-41` · high · sha:b01c80abec79</sub>
- Enforcement for rule 3.2 is review at boundary modules and analyzer CA1062 (validate public arguments); the example asserts presence once with `ArgumentNullException.ThrowIfNull(dto)`.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:51-57` · high · sha:b01c80abec79</sub>
- Enforcement for rule 3.3 is review rejecting `!` without a why-comment; an analyzer (e.g. CA1508 or a custom rule) can flag bare null-forgiving operators.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:70-70` · high · sha:b01c80abec79</sub>
- Enforcement for rule 3.4 is IDE0041 (use `is null`) and review of `== null` / `!= null` in new code.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:84-84` · high · sha:b01c80abec79</sub>
- A nullable attribute is propagated by the analyzer to every caller and the analyzer checks that the implementation upholds it; review prefers an attribute over `!`.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:104-115` · high · sha:b01c80abec79</sub>
- Enforcement for rule 3.7 is convention in domain modules; the distinct type makes mismatches compile errors.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:133-133` · high · sha:b01c80abec79</sub>
- Enforcement for rule 3.8 is review of type-kind choice, CA1815/CA1822, and the immutability rules of chapter 06.
  <sub>styleguide · `docs/styleguide/csharp/03-nullability-and-the-type-system.md:147-147` · high · sha:b01c80abec79</sub>
- Nullable-reference flow analysis is treated as the first test suite, and it must not be lied to with the null-forgiving !.
  <sub>styleguide · `docs/styleguide/csharp/README.md:20-20` · high · sha:1e6ba36fc337</sub>
- Chapter 03 covers NRT as law, banned ! null-forgiving, no #nullable disable, ? annotations, required, struct vs class vs record, banned dynamic, and default! only at proven boundaries.
  <sub>styleguide · `docs/styleguide/csharp/README.md:35-35` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded

