# api-design

## Rules
- NFR-3 (SHOULD) The public API surface SHOULD be explicit and minimal, with every exported declaration deliberately public with a declared type and implementation details kept non-exported.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:12-12` · high · sha:5f4684bf7123</sub>
- NFR-3 Each adapter SHOULD keep its public surface as small as its capability allows (a single entry point where possible, a small cohesive cluster where genuinely needed), and nothing internal leaks by default.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:12-12` · high · sha:5f4684bf7123</sub>
- NFR-4 (SHOULD) The public API of every published unit SHOULD be captured in a checked-in, machine-comparable snapshot, and the build SHOULD fail on any drift.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:13-13` · high · sha:5f4684bf7123</sub>
- NFR-4 An intentional API change is landed by regenerating and committing the snapshot in the same change, and the regeneration tool MUST NOT be used to silence an unintentional break.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:13-13` · high · sha:5f4684bf7123</sub>
- Implementation helpers are internal (styleguide rule 10.1) and each library grants InternalsVisibleTo only to its own test project.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:124-126` · high · sha:c9a7834ab04d</sub>
- Adapters are built against core's public surface exactly as an outside author would be; if an adapter needs something, that something becomes public and reviewed.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:131-133` · high · sha:c9a7834ab04d</sub>
- Every type and member defaults to `internal` and is widened to `public` only when a named caller across the assembly boundary needs it.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:40-44` · high · sha:fb3523c956a0</sub>
- Test projects do not justify making a type `public`; they access internals through `[assembly: InternalsVisibleTo("<TestAssembly>")]`, which grants exactly one assembly access without widening the surface.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:44-52` · high · sha:fb3523c956a0</sub>
- DTOs and return types are immutable `record`s using `init` and `required` members, not mutable classes.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:55-59` · high · sha:fb3523c956a0</sub>
- `required` plus `init` force a DTO to be complete at construction and frozen afterwards, so the compiler rejects a missing `required` member and no post-construction setter exists.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:59-59` · high · sha:fb3523c956a0</sub>
- Optional DTO members are expressed as nullable types such as `string? Note { get; init; }` while mandatory members use `required`.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:63-68` · medium · sha:fb3523c956a0</sub>
- Public method parameters accept the narrowest useful interface, namely `IEnumerable<T>` when iterating once, `IReadOnlyList<T>` when indexing or counting, and `IReadOnlyDictionary<TKey, TValue>` for keyed lookup, rather than `List<T>` or a concrete type.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:72-75` · high · sha:fb3523c956a0</sub>
- Public returns are read-only types such as `IReadOnlyList<T>` or `IReadOnlyDictionary<TKey, TValue>`; a mutable collection, array, or `List<T>` is never exposed as a field or return value, and settable collection properties are not exposed.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:76-76` · high · sha:fb3523c956a0</sub>
- With nullable reference types enabled, the `?` annotation on a public parameter or return is part of the contract, so a non-nullable parameter is never handed null and a non-nullable return is never null.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:86-89` · high · sha:fb3523c956a0</sub>
- An optional value or absent result is expressed as `T?` in the signature.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:89-99` · high · sha:fb3523c956a0</sub>
- Changing nullability annotations on a shipped signature, such as loosening a parameter from `T` to `T?` or a return from `T` to `T?`, is treated as a breaking contract change.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:90-90` · high · sha:fb3523c956a0</sub>
- Non-nullable reference parameters on public methods are validated at the boundary with `ArgumentNullException.ThrowIfNull`, and a possible null is never papered over with the `!` operator.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:90-98` · high · sha:fb3523c956a0</sub>
- Every public async or long-running method that does I/O or runs unbounded accepts a `CancellationToken`, and the token is the last parameter.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:103-106` · high · sha:fb3523c956a0</sub>
- The `CancellationToken` is a required parameter on a genuinely cancellable operation and is not defaulted to `default`, so the caller must decide what to pass; an overload without a token is the explicit way to declare an operation non-cancellable.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:107-107` · high · sha:fb3523c956a0</sub>
- The `CancellationToken` is threaded unchanged to the innermost call and never swallowed, so cancellation propagates all the way down; a method with no other arguments still takes the token.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:107-113` · high · sha:fb3523c956a0</sub>
- A shipped public member is removed or changed only through deprecation, marked `[Obsolete("Use NewMethod; removed in vN.")]` with a message naming the replacement and removal version, given a release to migrate, and removed only in a major version.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:117-120` · high · sha:fb3523c956a0</sub>
- Versioning follows semver: additive backward-compatible changes are a minor bump, and a removal or signature change after the deprecation window is a major bump.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:121-121` · high · sha:fb3523c956a0</sub>
- `[Obsolete(error: true)]` is applied for the final release before removal so the warning becomes a build error and no caller reaches the cutover unaware.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:121-121` · high · sha:fb3523c956a0</sub>
- The public surface is tracked with `Microsoft.CodeAnalysis.PublicApiAnalyzers`, so every public member is listed in `PublicAPI.Shipped.txt` (committed) or `PublicAPI.Unshipped.txt` (staged for the next release).
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:130-133` · high · sha:fb3523c956a0</sub>
- Named static factory methods such as `FromJson`, `ForTenant` or `Parse` are preferred to overloaded constructors, and the constructor is reserved for the one canonical, fully-specified way to build the value.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:143-146` · high · sha:fb3523c956a0</sub>
- Paired public operations are designed symmetrically so each has a discoverable inverse, for example Open/Close, Subscribe/Unsubscribe, Acquire/Release, Parse/ToString.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:147-147` · high · sha:fb3523c956a0</sub>
- Make `internal` the default visibility for every type and widen to `public` only when another assembly must call it as a deliberate API boundary (styleguide 12.5).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:95-108` · high · sha:a44b6f9eaba9</sub>

## Constraints
- A consumer who pins core below an adapter's floor gets a package downgrade error (NU1605, an error by default in SDK-style projects), and otherwise version skew surfaces as a MissingMethodException or TypeLoadException at the first call into a changed member rather than at load.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:153-156` · medium · sha:c9a7834ab04d</sub>
- RS0016 errors on any public member missing from both PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt, and RS0017 flags an API-file entry for a member that no longer exists.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:133-134` · high · sha:fb3523c956a0</sub>

## Conclusions
- The design overturns the as-built convention of granting Core's InternalsVisibleTo to the transport assembly because a first-party adapter reaching core internals proves nothing about the sufficiency of the public SPI that third-party adapter authors depend on, and it turns an internal member into a cross-package contract the public-API snapshot (NFR-4) does not track.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:126-131` · high · sha:c9a7834ab04d</sub>
- Lockstep is chosen because the packages are co-designed and share one SPI, and because NuGet expresses a ProjectReference as a floor-only dependency (>= x.y.z) with .NET convention discouraging upper bounds, so independently versioned adapters would admit a core/adapter pair never built together.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:146-151` · high · sha:c9a7834ab04d</sub>
- Versioning is lockstep: one VersionPrefix in Directory.Build.props stamps every package, every assembly's AssemblyInformationalVersion (which SdkVersion reads for the User-Agent, NFR-15) and every package's dependency on core, so a release is one coherent set, overturning the Ruby port's independent per-gem versioning.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:146-153` · high · sha:c9a7834ab04d</sub>
- Version-skew risk is accepted before 1.0, and from 1.0 SemVer plus the public-API snapshot of NFR-4 makes the floor sufficient.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:156-157` · high · sha:c9a7834ab04d</sub>
- Ruby's registration-time version assertion has no .NET counterpart because it would be a runtime check duplicating what the package graph already enforces (P11 applies).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:157-159` · high · sha:c9a7834ab04d</sub>
- Accessibility is treated as the boundary between what can be changed freely and what is owed forever, because a public type becomes a contract that other assemblies depend on and changing it is an invisible breaking change.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:43-43` · high · sha:fb3523c956a0</sub>
- A value handed to or accepted from a caller is data without identity or lifecycle, so it is a `record` with value equality, frozen on both sides so neither party can mutate the other's instance.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:58-58` · high · sha:fb3523c956a0</sub>
- Returning `List<T>`, `T[]`, or a settable collection property is rejected because it hands the caller a live handle to internal state that they can mutate undetected.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:76-76` · high · sha:fb3523c956a0</sub>
- The token is placed last because that is the framework-wide convention across the BCL, ASP.NET Core and EF Core, so callers find it where expected and tooling that appends a token slots it correctly.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:106-106` · high · sha:fb3523c956a0</sub>
- Public API growth is made an explicit reviewed diff in PublicAPI.Unshipped.txt because public members otherwise grow by accident, such as a helper bumped to `public` or a field exposed in passing.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:133-134` · high · sha:fb3523c956a0</sub>
- Named factories are preferred because they state at the call site what is built and from what, can return cached or derived instances, and give each construction path a distinct name, whereas constructor overloads force disambiguation by parameter types alone.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:146-146` · high · sha:fb3523c956a0</sub>
- API symmetry is required because a caller who finds one half can guess the other and a leak such as a Subscribe without Unsubscribe shows up as a missing member.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:147-147` · high · sha:fb3523c956a0</sub>
- Internal-by-default is chosen because each public type is a promise of stability to every other assembly and widens the surface to keep stable and analyze, while internal costs nothing within the assembly and keeps the cross-assembly contract intentional.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:97-99` · high · sha:a44b6f9eaba9</sub>

## Reference
- NFR-3 conformance is enumerating exported symbols per unit so that no symbol documented as internal appears, and a public declaration lacking explicit type/visibility is a build error where the language enforces it.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:12-12` · high · sha:5f4684bf7123</sub>
- NFR-4 excludes unpublished, sample and test-only units from the API snapshot, and its conformance is that changing a public signature without updating the snapshot fails the gate while regenerating passes with a reviewable diff.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:13-13` · high · sha:5f4684bf7123</sub>
- At d45e64b the InternalsVisibleTo grant from core to the transport was unused (no internal core member was referenced from the transport), and the transport's own grant named Dexpace.Sdk.Http.SystemNet.Tests, a project that did not yet exist because its tests lived in Dexpace.Sdk.Core.Tests/Transport/.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:131-134` · high · sha:c9a7834ab04d</sub>
- The chapter's model example shows an immutable record DTO with required init members and a read-only list defaulting to an empty collection expression, a catalog type taking a CancellationToken last and returning IReadOnlyList via ConfigureAwait(false), and a named factory.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:7-34` · medium · sha:fb3523c956a0</sub>
- Rule 10.1 is enforced by analyzer CA1515 (consider making public types internal), by review requiring a named cross-assembly caller to justify `public`, and by `InternalsVisibleTo` for test access.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:53-53` · high · sha:fb3523c956a0</sub>
- Rule 10.2 is enforced by review of the DTO type kind, by the compiler checking `required`, and by `init`-only making a stray `set` a compile error.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:70-70` · high · sha:fb3523c956a0</sub>
- Rule 10.3 is enforced by CA1002 (do not expose generic List<T>), CA1819 (properties should not return arrays), CA2227 (collection properties read-only), and review of public signatures.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:84-84` · high · sha:fb3523c956a0</sub>
- Rule 10.4 is enforced by `<Nullable>enable</Nullable>` with `<TreatWarningsAsErrors>`, by CA1062 (validate public arguments), and by review treating annotation changes as contract changes.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:101-101` · high · sha:fb3523c956a0</sub>
- Rule 10.5 is enforced by CA1068 (CancellationToken parameters must come last) and by review requiring a token on public async and I/O methods.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:115-115` · high · sha:fb3523c956a0</sub>
- Migration notes for obsolete members are documented in `<remarks>` as described in the documentation chapter 14.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:121-121` · medium · sha:fb3523c956a0</sub>
- Rule 10.6 is enforced by `[Obsolete]` with message and replacement, semver discipline in release notes, and the public-API analyzer flagging signature changes for review.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:128-128` · high · sha:fb3523c956a0</sub>
- Rule 10.7 is enforced by RS0016 and RS0017, with PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt reviewed on every change.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:141-141` · high · sha:fb3523c956a0</sub>
- Rule 10.8 is enforced by CA1707 (no underscores in identifiers) on factory names and by review preferring a named factory over a fourth constructor overload and rejecting asymmetric pairs.
  <sub>styleguide · `docs/styleguide/csharp/10-api-design.md:158-158` · high · sha:fb3523c956a0</sub>
- InternalsVisibleTo opens internal types to a test assembly without making them public.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:99-99` · high · sha:a44b6f9eaba9</sub>
- Rule 12.5 is enforced by review of `public` on new types and by the public-API analyzer of chapter 10, which tracks the exported surface.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:108-108` · high · sha:a44b6f9eaba9</sub>
- Chapter 10 covers minimal public surface, immutable record DTOs, IReadOnlyList returns, nullable annotations as contract, required plus init, [Obsolete] with semver, the public-API analyzer, and internal by default.
  <sub>styleguide · `docs/styleguide/csharp/README.md:42-42` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded

