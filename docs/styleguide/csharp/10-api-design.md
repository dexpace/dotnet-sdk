# 10 — API Design

A public surface is a promise you cannot quietly take back: every type, member, and signature you expose becomes a contract a caller depends on and an obligation you maintain for the life of the assembly. This chapter is about shaping that surface so it stays small, honest, and evolvable — minimal by default, immutable where it carries data, narrow at the parameters and read-only at the returns, with the nullable annotations doing the work of the contract. What you do not expose, you are free to change; what you expose, you owe.

## What good looks like

```csharp
namespace Dexpace.Catalog;

// Immutable record DTO: init + required force a complete value at construction (10.2, 10.5).
public sealed record ProductView
{
    public required ProductId Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];          // read-only return, never List<T> (10.3)
}

// Internal by default; widened to public only because callers across the assembly boundary need it (10.1).
public sealed class ProductCatalog(ProductStore store)
{
    // Narrow input (IEnumerable), read-only output (IReadOnlyList), token last, non-null contract (10.3, 10.4, 10.5).
    public async Task<IReadOnlyList<ProductView>> Search(
        IEnumerable<string> terms, CancellationToken cancellationToken)
    {
        var hits = await store.Query(terms, cancellationToken).ConfigureAwait(false);
        return [.. hits.Select(ToView)];
    }

    // A named factory beats a thicket of constructor overloads (10.8).
    public static ProductCatalog ForTenant(TenantId tenant, ProductStore store) => new(store);

    private static ProductView ToView(Product p) => new() { Id = p.Id, Name = p.Name, Tags = p.Tags };
}
```

`ProductCatalog` is the only type widened past `internal`, and only because callers across the assembly need it (10.1); `ProductView` is an immutable `record` forced complete by `required` + `init` (10.2, 10.5). `Search` accepts the narrowest useful interface and returns a read-only list, never a `List<T>` (10.3), takes its `CancellationToken` last (10.5), and its non-nullable parameters and return state the null contract directly (10.4). `ForTenant` is a named factory rather than another constructor overload (10.8), and every public addition lands as a reviewed diff in `PublicAPI.Unshipped.txt` (10.7).

## Rules

### 10.1 — Default every type and member to `internal`; widen to `public` only for a caller that needs it.

**Reasoning, step by step:**
1. Accessibility is the boundary between what you can change freely and what you owe forever. An `internal` type lives inside the assembly, so you can rename, reshape, or delete it in one commit; the moment it becomes `public` it is a contract some other assembly depends on, and changing it is a breaking change you cannot see from here. Start closed: `internal` is the default, `public` is the deliberate exception with a named caller behind it.
2. Test projects do not justify going `public` — they read internals through `[assembly: InternalsVisibleTo("Dexpace.Catalog.Tests")]`, which grants exactly that one assembly access without widening the surface to the whole world (chapter [12](./12-project-organization.md)). Reserve `public` for the genuine cross-assembly contract; everything supporting it stays `internal` and stays yours to change.

**Worked example:**
```csharp
[assembly: InternalsVisibleTo("Dexpace.Catalog.Tests")]            // tests see internals, surface stays small

internal sealed record SearchPlan(IReadOnlyList<string> Terms);    // implementation detail, never public
public sealed record ProductView { /* the contract callers actually consume */ }
```
**Enforcement:** `CA1515` (consider making public types internal); review requires a named cross-assembly caller to justify `public`; `InternalsVisibleTo` for test access.

### 10.2 — Make DTOs and return types immutable `record`s with `init` and `required`.

**Reasoning, step by step:**
1. A value you hand a caller, or accept from one, is data — it has no identity and no lifecycle, so it is a `record` with value equality, not a mutable `class` (chapter [06](./06-types-and-data-modeling.md)). Making it immutable means the caller cannot mutate the instance you returned and corrupt state you still reference, and you cannot mutate the instance they passed and surprise them. The DTO crossing the boundary is frozen on both sides.
2. `required` + `init` force the value complete at construction and frozen after, so there is no half-built window and no post-construction setter to forget (chapter [06](./06-types-and-data-modeling.md)). A caller constructs the DTO with an object initializer that reads as named arguments, the compiler rejects a missing `required` member, and the result is a value that is either fully formed or does not compile.

**Worked example:**
```csharp
public sealed record CreateOrder
{
    public required CustomerId Customer { get; init; }            // omitting it fails to compile
    public required IReadOnlyList<LineItem> Lines { get; init; }
    public string? Note { get; init; }                           // optional is the typed exception
}
```
**Enforcement:** review of DTO type-kind; `required` checked by the compiler; `init`-only makes a stray `set` a compile error.

### 10.3 — Accept the narrowest useful interface; return a read-only type, never a mutable collection.

**Reasoning, step by step:**
1. A parameter typed `IEnumerable<T>` or `IReadOnlyList<T>` accepts the widest set of callers — an array, a `List<T>`, a LINQ query — and promises you will only read it, so the caller knows their collection is safe in your hands. Take exactly the capability you use: `IEnumerable<T>` when you iterate once, `IReadOnlyList<T>` when you index or need a count, `IReadOnlyDictionary<TKey, TValue>` for keyed lookup. A `List<T>` parameter demands more than you need and a concrete type more than you should.
2. A return of `List<T>`, `T[]`, or a settable collection property hands the caller a live handle to your internal state — they can `Add`, `Clear`, or reorder it and mutate you from the outside, and you cannot tell. Return `IReadOnlyList<T>` or `IReadOnlyDictionary<TKey, TValue>` so the contract says "read this, do not change it," and the caller cannot cast it back to mutate without an obvious, reviewable cast. Never expose a mutable collection, array, or `List<T>` field or return.

**Worked example:**
```csharp
public IReadOnlyList<Receipt> Process(IEnumerable<Order> orders) =>   // narrow in, read-only out
    [.. orders.Select(Charge)];
public List<Receipt> ProcessBad(List<Order> orders) { /* demands too much, leaks too much */ }
```
**Enforcement:** `CA1002` (do not expose generic `List<T>`), `CA1819` (properties should not return arrays), `CA2227` (collection properties read-only); review of public signatures.

### 10.4 — Treat the nullable annotations as the contract; a non-nullable signature is a promise.

**Reasoning, step by step:**
1. With nullable reference types on (chapter [03](./03-nullability-and-the-type-system.md)), the `?` on a parameter or return is part of the public contract, not a hint. A non-nullable parameter promises the callee that it will never be handed `null`, so the implementation may dereference it without re-checking; a non-nullable return promises the caller they will never receive `null`, so they need no guard. An optional value or an absent result is `T?`, and that nullability is the documentation — honest, compiler-checked, and visible at the call site.
2. Because the annotation is a promise, breaking it is a breaking change even though the type name is unchanged: tightening a return from `T?` to `T` can be safe, but loosening a parameter from `T` to `T?` or a return from `T` to `T?` changes what callers must handle and is treated as such (10.6). Validate the promise at the boundary — `ArgumentNullException.ThrowIfNull` on a non-nullable reference parameter guards against callers who disabled the analysis (chapter [05](./05-methods-and-functions.md)) — and never paper over a possible null with `!`.

**Worked example:**
```csharp
public Receipt Charge(Order order)                               // non-null in and out: a real guarantee
{
    ArgumentNullException.ThrowIfNull(order);                    // guards a caller who ignored the contract
    return _gateway.Charge(order);
}
public Receipt? FindReceipt(OrderId id);                         // T? states "may be absent" honestly
```
**Enforcement:** `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>` (chapter [01](./01-formatting-and-tooling.md)); `CA1062` (validate public arguments); review treats annotation changes as contract changes.

### 10.5 — Put `CancellationToken` last on every async or long-running public method.

**Reasoning, step by step:**
1. Every public method that does I/O or runs unbounded must accept a `CancellationToken` so the caller can impose a timeout or cancel a request (chapter [09](./09-concurrency.md)); a method that cannot be cancelled cannot be bounded. Placing the token last, after the meaningful arguments, is the framework-wide convention — the BCL, ASP.NET Core, and EF Core all do it — so callers find it where they expect and tooling that appends a token slots it correctly.
2. Make the token a required parameter on a genuinely cancellable operation rather than defaulting it to `default`, so the caller has to decide what to pass and cannot silently opt out of cancellation; an overload taking no token is the explicit way to say "this one cannot be cancelled." The token threads through to the innermost call unchanged, never swallowed, so cancellation propagates the whole way down.

**Worked example:**
```csharp
public Task<IReadOnlyList<Order>> ListOrders(
    CustomerId customer, int limit, CancellationToken cancellationToken);   // token last, required
public Task Drain(CancellationToken cancellationToken);                     // even with no other args
```
**Enforcement:** `CA1068` (`CancellationToken` parameters must come last); review requires a token on public async/I/O methods.

### 10.6 — Evolve compatibly: mark removed or changed members `[Obsolete]` with a message, and follow semver.

**Reasoning, step by step:**
1. A shipped public signature is depended upon by code you cannot recompile, so changing or deleting it silently breaks callers at their next build or, worse, at runtime. The path off a member is deprecation, not deletion: mark it `[Obsolete("Use NewMethod; removed in v3.")]` so every caller gets a compile warning naming the replacement and the removal version, give them a release to migrate, and only then remove it in a major version. A hard break with no warning is debt shipped to your callers.
2. Versioning communicates the shape of the change before anyone reads the diff: additive, backward-compatible changes are a minor bump; a removal or a signature change after the deprecation window is a major bump (chapter [14](./14-documentation.md) documents the `<remarks>` migration note). `[Obsolete(error: true)]` turns the warning into a build error for the final release before removal, so no caller can reach the cutover unaware.

**Worked example:**
```csharp
[Obsolete("Use Search(IEnumerable<string>, CancellationToken); removed in v3.", error: false)]
public IReadOnlyList<ProductView> Search(string term) => Search([term], CancellationToken.None);
```
**Enforcement:** `[Obsolete]` with a message and replacement; semver discipline in release notes; the public-API analyzer (10.7) flags the signature change for review.

### 10.7 — Track the public surface with the public-API analyzer so every addition is a reviewed diff.

**Reasoning, step by step:**
1. A public API grows by accident — a helper bumped to `public` to share it, a field exposed in passing — and each accidental addition is a contract you did not mean to sign. `Microsoft.CodeAnalysis.PublicApiAnalyzers` makes the surface explicit: every public member must be listed in `PublicAPI.Shipped.txt` (what you have committed to) or `PublicAPI.Unshipped.txt` (what is staged for the next release), and `RS0016` errors on any public member missing from both. The surface is now data under version control, not an emergent property of access modifiers.
2. Because adding a `public` member forces a line into `PublicAPI.Unshipped.txt`, the addition shows up in the pull request diff where a reviewer decides whether the contract should grow; `RS0017` flags an entry for a member that no longer exists, catching a removal the same way. The analyzer turns "what is our public API and did this change touch it" from a manual audit into a mechanical, reviewable check at the exact moment of change.

**Worked example:**
```text
# PublicAPI.Unshipped.txt — this line appears in the PR diff for review (RS0016/RS0017)
Dexpace.Catalog.ProductCatalog.ForTenant(Dexpace.Catalog.TenantId tenant, Dexpace.Catalog.ProductStore store) -> Dexpace.Catalog.ProductCatalog
```
**Enforcement:** `RS0016` (declared public member not in the API file), `RS0017` (API file entry for a removed member); `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` reviewed on every change.

### 10.8 — Prefer named static factory methods to overloaded constructors, and design for API symmetry.

**Reasoning, step by step:**
1. A thicket of constructor overloads forces the caller to disambiguate by parameter types alone, and two overloads that differ only by argument order or a nullable are an invitation to call the wrong one. A named static factory — `FromJson`, `ForTenant`, `Parse` — says at the call site what it builds and from what, can return a cached or derived instance instead of always allocating, and can carry a different name for each construction path so none of them collide. Reserve the constructor for the one canonical, fully-specified way to build the value.
2. Design the surface so paired operations are symmetric and discoverable: `Open`/`Close`, `Subscribe`/`Unsubscribe`, `Acquire`/`Release`, `Parse`/`ToString`. Symmetry means a caller who finds one half can guess the other exists and behaves as the inverse, and it makes a leak (a `Subscribe` with no `Unsubscribe`) visible as a missing member. An asymmetric API is one the caller cannot reason about by analogy, so they reach for the docs or guess.

**Worked example:**
```csharp
public sealed class Subscription
{
    private Subscription(Topic topic) { /* ... */ }
    public static Subscription Subscribe(Topic topic) => new(topic);   // named factory, clear intent
    public void Unsubscribe() { /* the symmetric counterpart — Subscribe has an inverse */ }
}
```
**Enforcement:** `CA1707` (no underscores in identifiers) on factory names; review prefers a named factory over a fourth constructor overload and rejects asymmetric pairs.

## Cross-references

- Immutable record DTOs, `required` + `init`, and `record`-vs-`class`: [06-types-and-data-modeling.md](./06-types-and-data-modeling.md). Nullable annotations as contract and the banned `!`: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md).
- `CancellationToken` placement, timeouts, and bounded async: [09-concurrency.md](./09-concurrency.md). `ArgumentNullException.ThrowIfNull` and public-argument validation: [05-methods-and-functions.md](./05-methods-and-functions.md).
- `internal` default, `InternalsVisibleTo`, and assembly boundaries: [12-project-organization.md](./12-project-organization.md). XML doc and migration `<remarks>` on the public surface: [14-documentation.md](./14-documentation.md).
