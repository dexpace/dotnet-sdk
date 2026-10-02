# data-modeling

## Rules
- HTTP-1 (MUST) - All core domain-model types MUST present an immutable value/metadata surface after construction, safe to share across threads without external synchronization, and any change MUST produce a new instance.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:10-12` · high · sha:8014d2ec2c9d</sub>
- SEAM-29 / HTTP-2 (MUST) - Model construction MUST go through an immutable-value plus Builder (or dedicated factory) pattern, and there MUST be no public field-wise constructor or unchecked copy that bypasses validation.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:13-13` · high · sha:8014d2ec2c9d</sub>
- SEAM-29 (restated for the seam layer, MUST) - A shared generic Builder contract (build() producing the target type) MUST exist so generic composition helpers can accept any builder.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:49-49` · high · sha:0adae2d6a47f</sub>
- Required-field validation MUST be uniform, so a missing required field fails at build() with a consistent message of the form "<name> is required" (SEAM-29).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:49-49` · high · sha:0adae2d6a47f</sub>
- CFG-29 (MUST): RFC 1123 date formatting emits the canonical HTTP-date form with a zero-padded two-digit day-of-month and a literal GMT, rendered in UTC (for example Sun, 06 Nov 1994 08:49:37 GMT).
  <sub>spec · `docs/product-spec/16-configuration.md:52-52` · high · sha:367e27ec6481</sub>
- CFG-30 (MUST): RFC 1123 date parsing is tolerant in three ways: month names are case-insensitive, the zone token accepts GMT, UTC, +0000 and +00:00 (all normalized to zero offset), and the leading weekday token is informational only, stripped rather than parsed and never validated against the date.
  <sub>spec · `docs/product-spec/16-configuration.md:53-53` · high · sha:367e27ec6481</sub>
- CFG-31 (MUST): RFC 1123 parsing is strict on the day-of-month-onward grammar: blank input fails, and a form missing the comma after the weekday fails.
  <sub>spec · `docs/product-spec/16-configuration.md:54-54` · high · sha:367e27ec6481</sub>
- CFG-32 (MUST): The non-blocking UUID generator produces type-4 UUIDs with the correct RFC 4122 layout (version 4, IETF variant), is usable concurrently without shared mutable state, uses a non-blocking per-thread PRNG, and callers MUST treat its output as non-cryptographic.
  <sub>spec · `docs/product-spec/16-configuration.md:55-55` · high · sha:367e27ec6481</sub>
- CFG-33 (MUST): Deep value-equality helpers compare by content (arrays element-by-element, object arrays recursing for nested or multi-dimensional arrays, primitive arrays by element value, non-arrays by ordinary equality), are null-safe (two nulls equal, null hashes to zero), and keep equals and hashCode mutually consistent.
  <sub>spec · `docs/product-spec/16-configuration.md:56-56` · high · sha:367e27ec6481</sub>
- CFG-34 (MUST): Deep equality follows floating-point array semantics where NaN equals NaN and +0.0 does not equal -0.0 (for primitive and boxed float/double arrays) with matching hashing, and an object array and a primitive array of the same numeric values are never equal.
  <sub>spec · `docs/product-spec/16-configuration.md:57-57` · high · sha:367e27ec6481</sub>
- XCUT-15 (MUST) Public wire models (request, response, headers, media type, status, etc.) MUST be immutable after construction and safe to share across threads, with mutation expressed as producing a new instance.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:40-40` · high · sha:d6123be82c9e</sub>
- XCUT-15 (MUST) A public wire model MUST NOT retain an alias to externally-mutable state that a post-construction mutation could use to alter it, and instead either defensively copies an ingested mutable collection or holds an immutable collection type.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:40-40` · high · sha:d6123be82c9e</sub>
- A deep-equality helper comparing doubles through BitConverter.DoubleToInt64Bits ships when a model first carries an array.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:307-308` · high · sha:ddf8f695ff61</sub>
- Construction-time completeness is forced with required and init members, not with multi-step setters.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:114-114` · high · sha:5b533a9851b7</sub>
- required is paired with init-only accessors so members can be set in an object initializer but never reassigned, giving immutability and mandatory initialization without a hand-written constructor per field.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:118-118` · high · sha:5b533a9851b7</sub>
- Small immutable values use readonly struct or readonly record struct; never a large mutable struct and never a public mutable struct field.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:132-132` · high · sha:5b533a9851b7</sub>
- Structs are kept small and readonly; a large or mutable value is made a record class, and struct data is exposed through init-only properties rather than public mutable fields.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:136-136` · high · sha:5b533a9851b7</sub>
- Code is reused by composing injected interfaces, never by inheriting a base class.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:149-149` · high · sha:5b533a9851b7</sub>
- Reuse depends on a small interface named for its role, receives an implementation through the constructor, and delegates to it.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:152-152` · high · sha:5b533a9851b7</sub>
- The only abstract or virtual members written belong to the closed hierarchy, which models a choice between cases and not a sharing of code.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:153-153` · high · sha:5b533a9851b7</sub>
- Every enum has an explicit underlying type such as byte or int, so a serialized or interop value has a defined width that a reorder cannot shift.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:165-168` · high · sha:5b533a9851b7</sub>
- A non-flags enum takes a singular noun name, such as OrderState or LogLevel.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:168-168` · high · sha:5b533a9851b7</sub>
- A [Flags] enum takes a plural noun name, such as FileAccessRights, and assigns explicit power-of-two values.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:168-168` · high · sha:5b533a9851b7</sub>
- Enum types and every enum member are named in PascalCase.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:168-168` · high · sha:5b533a9851b7</sub>
- When behaviour attaches to enum cases, such as scattered switches computing fees, handlers or labels, the enum is promoted to a closed hierarchy where each case carries its own data and behaviour lives with it or in one exhaustive switch.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:169-169` · high · sha:5b533a9851b7</sub>
- Enums are reserved for a plain, behaviour-free tag.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:169-169` · high · sha:5b533a9851b7</sub>
- Domain models are expressed as data and functions rather than an object hierarchy with behaviour woven through inheritance, so that illegal states are unrepresentable and the type checker rejects bad values before a test runs.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:3-3` · high · sha:5b533a9851b7</sub>
- The only base class written is an abstract base that backs a closed discriminated-union hierarchy; records carry immutable data and closed hierarchies carry choice.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:3-3` · high · sha:5b533a9851b7</sub>
- Immutable data is modelled as record types and updated with a with-expression, never by mutation.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:45-45` · high · sha:5b533a9851b7</sub>
- A changed value is produced by copying with a with-expression into a new instance and binding the result, and a property is never reassigned in place.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:49-49` · high · sha:5b533a9851b7</sub>
- A property that would otherwise have a setter is declared init-only.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:49-49` · high · sha:5b533a9851b7</sub>
- Every class is sealed by default.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:58-58` · high · sha:5b533a9851b7</sub>
- A class is unsealed only with a stated reason, and the single permitted unsealed class is the abstract base of a closed hierarchy whose leaves are sealed.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:61-62` · high · sha:5b533a9851b7</sub>
- Polymorphism is written as a closed hierarchy and reuse is achieved by injecting an interface and delegating.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:62-62` · high · sha:5b533a9851b7</sub>
- Illegal states are made unrepresentable by a closed hierarchy matched exhaustively, not by a class with a nullable field per variant.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:71-71` · high · sha:5b533a9851b7</sub>
- A choice between variants is modelled as an abstract record whose cases are closed by a private constructor and whose every case is a sealed record.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:74-74` · high · sha:5b533a9851b7</sub>
- A closed hierarchy is consumed with a switch expression carrying one arm per case and no _ default arm.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:75-75` · high · sha:5b533a9851b7</sub>
- Exhaustiveness over closed hierarchies is recovered with the SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression suppressor (CTH001), which silences CS8509 only while every case is covered and re-fires when a case is added.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:75-75` · high · sha:5b533a9851b7</sub>
- CS8509 is promoted to error and dotnet_diagnostic.CTH001.suppress_on_record_hierarchies = true is set in .editorconfig, because records need the opt-in for the suppressor.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:91-91` · high · sha:5b533a9851b7</sub>
- Review rejects nullable-bag modeling of variants.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:91-91` · high · sha:5b533a9851b7</sub>
- Parse, don't validate: a factory returns the proven domain type or a typed failure, never a bool plus the raw data.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:93-93` · high · sha:5b533a9851b7</sub>
- The only path to a validated domain type runs through its validating factory, enforced by a private constructor.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:97-112` · medium · sha:5b533a9851b7</sub>
- When failure is expected and routine, a parsing factory returns a Result carrying either the parsed value or a typed error rather than throwing.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:97-97` · high · sha:5b533a9851b7</sub>
- When an invalid input is a caller bug, the parsing factory throws.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:97-97` · high · sha:5b533a9851b7</sub>
- Model state as record types and readonly structs, group behaviour into static classes of pure methods and small interfaces, and reserve a stateful class for lifecycle resources that are opened and closed.
  <sub>styleguide · `docs/styleguide/csharp/README.md:59-59` · high · sha:1e6ba36fc337</sub>
- Never use inheritance for code reuse: sealed is the default and the only base type written is an abstract closed hierarchy for a discriminated union.
  <sub>styleguide · `docs/styleguide/csharp/README.md:59-59` · high · sha:1e6ba36fc337</sub>
- Use readonly fields, init-only properties, record over mutable class, and IReadOnlyList<T>/ReadOnlySpan<T> in public signatures.
  <sub>styleguide · `docs/styleguide/csharp/README.md:61-61` · high · sha:1e6ba36fc337</sub>
- Update state by a with-expression into a new value, never by mutation; mutability is the choice that has to be typed.
  <sub>styleguide · `docs/styleguide/csharp/README.md:61-61` · high · sha:1e6ba36fc337</sub>
- abstract/virtual is for a closed hierarchy backing a discriminated union and nothing else; closed polymorphism is a sealed hierarchy matched with a switch expression.
  <sub>styleguide · `docs/styleguide/csharp/README.md:63-63` · high · sha:1e6ba36fc337</sub>
- Code reuse is achieved by delegation through an injected interface, using small composed interfaces and never a deep class tree.
  <sub>styleguide · `docs/styleguide/csharp/README.md:63-63` · high · sha:1e6ba36fc337</sub>

## Constraints
- The single carve-out from model immutability is a body that wraps live single-use stream state (section 6).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:12-12` · high · sha:8014d2ec2c9d</sub>
- .NET's defaults are wrong for CFG-33 and CFG-34: records compare array members by reference (verified), and double.Equals (used by SequenceEqual) treats 0.0 and -0.0 as equal (verified) where CFG-34 requires them distinct, while its NaN-equals-NaN behaviour happens to match.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:304-308` · high · sha:ddf8f695ff61</sub>
- A mutable struct copies on every assignment, argument pass and collection access, so a mutation lands on a copy and is lost, and a large struct copies many bytes on each of those, erasing the allocation win.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:136-136` · high · sha:5b533a9851b7</sub>
- C# 14 does not prove a record or class hierarchy exhaustive because a record's implicit copy constructor means the compiler never treats the set as closed, so a switch with no _ arm raises CS8509; the closed modifier that fixes this natively is a C# 15 feature.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:75-75` · high · sha:5b533a9851b7</sub>

## Conclusions
- CFG-32's identifier is Guid.NewGuid(), a version-4 UUID (verified) drawn from the OS cryptographic generator, stronger than the non-cryptographic generator the requirement permits; XCUT-21's CSPRNG path is RandomNumberGenerator, kept separate as the specification insists.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:301-303` · high · sha:ddf8f695ff61</sub>
- CFG-35 is satisfied by section 6.1's classifier; CFG-36's descriptor is SdkVersion plus RuntimeInformation.FrameworkDescription, with fallback "0.0.0" where the requirement says a non-blank "unknown".
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:308-310` · high · sha:ddf8f695ff61</sub>
- required is used because a type built by new followed by setters is half-formed and invariant-violating between construction and the last setter, whereas the compiler refuses a construction expression that omits a required member.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:117-117` · high · sha:5b533a9851b7</sub>
- The readonly modifier on a struct is preferred because it guarantees no member mutates this, lets the JIT skip defensive copies when passed by in, and signals the value is frozen.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:135-135` · high · sha:5b533a9851b7</sub>
- Composition is chosen over inheritance because inheriting binds the subclass to the base's internals, protected surface and construction order, and that coupling tightens as the base grows.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:152-152` · high · sha:5b533a9851b7</sub>
- Records are chosen for immutable data because they provide value equality, a readable ToString and with-expression copying for free, and because a value that cannot change cannot be altered by another thread or method.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:48-48` · high · sha:5b533a9851b7</sub>
- Classes are sealed by default because an unsealed class invites inheritance for code reuse, which the guide rejects, and because sealing marks a leaf type and lets the JIT devirtualize calls.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:61-61` · high · sha:5b533a9851b7</sub>
- A factory that consumes raw input and returns the proven domain type is preferred over a bool Validate because possessing the type is the proof of validity and the check lives in one place.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:96-97` · high · sha:5b533a9851b7</sub>

## Reference
- HTTP-1 rationale is that the model is the shared boundary handed to concurrent transports and pipelines, and a mutable metadata surface would race; conformance is to mutate the originating builder after construction and assert the built instance is unchanged, from multiple threads.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:12-12` · high · sha:8014d2ec2c9d</sub>
- SEAM-29 / HTTP-2 rationale is that validation and normalization live in the builder/factory and a bypass would let invalid instances exist; conformance is verifying that no construction path skips builder validation, for example deriving a request cannot install a body-carrying GET.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:13-13` · high · sha:8014d2ec2c9d</sub>
- SEAM-29 seam-layer conformance is that omitting a required field yields exactly "<field> is required" and a builder is assignable where the generic Builder contract is expected.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:49-49` · high · sha:0adae2d6a47f</sub>
- A closed hierarchy is written as an abstract record with a private constructor whose cases are nested sealed records, with the abstract base being the only base class.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:10-18` · high · sha:5b533a9851b7</sub>
- Required-member enforcement is by the compiler (IDE0250 and required), and review prefers required plus init over post-construction setters.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:130-130` · high · sha:5b533a9851b7</sub>
- Struct design is enforced by CA1815 (override equality on value types) and CA1051 (no visible instance fields), and review rejects large or mutable structs.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:147-147` · high · sha:5b533a9851b7</sub>
- Reuse is delegation and polymorphism is a sealed hierarchy matched with a switch; review rejects inheritance for code reuse, and abstract/virtual are permitted only for a closed hierarchy.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:153-163` · high · sha:5b533a9851b7</sub>
- Enum conventions are enforced by CA1714 (flags enums plural), CA1717 (non-flags singular), CA1027 and CA2217 (flags values), and review promotes behaviour-bearing enums to hierarchies.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:177-177` · high · sha:5b533a9851b7</sub>
- A closed-hierarchy switch expression with no discard arm fails the build when a new case is added without an arm once CTH001 is enabled.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:20-29` · high · sha:5b533a9851b7</sub>
- An immutable record type uses required plus init properties, a collection-expression default of [] for list members, and a With* method that returns this with { ... } to update.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:31-38` · high · sha:5b533a9851b7</sub>
- Identifiers such as InvoiceId are branded value types rather than bare Guid values.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:41-41` · high · sha:5b533a9851b7</sub>
- Enforcement of record immutability is by review of type-kind choice, and init-only properties make a stray set a compile error.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:56-56` · high · sha:5b533a9851b7</sub>
- Sealing is enforced by CA1052 (static holder types sealed), by review requiring a reason to leave a class unsealed, and by an analyzer that can require sealed on concrete classes.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:69-69` · high · sha:5b533a9851b7</sub>
- Under Nullable enabled, a non-nullable input to an exhaustive switch needs no null arm.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:91-91` · high · sha:5b533a9851b7</sub>
- The dependency-free alternative to the CTH001 suppressor is a _ => throw new UnreachableException() arm combined with a per-case test.
  <sub>styleguide · `docs/styleguide/csharp/06-types-and-data-modeling.md:91-91` · high · sha:5b533a9851b7</sub>
- Chapter 06 covers records for data, sealed by default, illegal states made unrepresentable via closed hierarchies and pattern matching, readonly struct for small values, init-only, and no inheritance for reuse.
  <sub>styleguide · `docs/styleguide/csharp/README.md:38-38` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded

