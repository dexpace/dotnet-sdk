# 04 — Variables & Declarations

How a value is declared and bound is the smallest unit of style and the one a reader meets most often. This chapter fixes when the type is written and when `var` carries it, when target-typed `new()` is allowed, the `readonly`/`const` defaults, and the modifier order. Every binding either names its type or stands next to a constructor that does — there is no third option.

## What good looks like

```csharp
namespace Dexpace.Pricing;

public sealed class PriceBook
{
    private readonly Dictionary<Sku, decimal> _prices; // _ field, readonly default (4.3)
    private const int MaxLines = 256;                  // compile-time constant, PascalCase (4.4)
    private static readonly TimeSpan s_cacheTtl = TimeSpan.FromMinutes(5); // runtime constant (4.4)

    public PriceBook(IReadOnlyList<PriceLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Dictionary<Sku, decimal> prices = new(lines.Count); // target-typed new, type on LHS (4.2)
        foreach (PriceLine line in lines)                   // explicit type, no RHS to name it (4.1)
            prices[line.Sku] = line.Amount;
        _prices = prices;
    }

    public bool TryGet(Sku sku, out decimal price) => _prices.TryGetValue(sku, out price); // out var at call (4.9)

    public IEnumerable<Sku> Expensive(decimal floor) =>
        from kv in _prices where kv.Value > floor select kv.Key; // var-required carve-out lives in callers (4.1)
}
```

Each field carries `_` and is `readonly` by default (4.3); the modifiers read `static readonly`, never reversed (4.4). The local uses target-typed `new()` because its type is named on the left (4.2), and the `foreach` variable is written out because nothing on the right names it (4.1). `MaxLines` is a true `const`; the runtime `TimeSpan` is `static readonly` (4.4). Visibility is stated on every member (4.5), and the field prefix means no `this.` is needed (4.6).

## Rules

### 4.1 — Use `var` only when the type is named on the right; write the type otherwise.

**Reasoning, step by step:**
1. The runtime style (rule 10) allows `var` only when the type is "abundantly clear from the right-hand side" — a `new`, an explicit cast, or a typed literal. `var user = new User(...)`, `var count = (int)raw`, `var name = "dexpace"` each spell their type once on the line, so the `var` adds no ambiguity. When the right side is a method call, an indexer, or a LINQ-less expression, the type is invisible, and `var total = Compute(order)` forces the reader to chase `Compute`'s signature; write `decimal total = Compute(order)` instead.
2. The carve-out runs the other way: an anonymous type and a LINQ query that projects one (`select new { kv.Key, kv.Value }`) have no nameable type, so `var` is *required*, not merely allowed. Outside that case the rule is mechanical — if you cannot point at the type on the right, name it on the left.

**Worked example:**
```csharp
var client = new HttpClient();                 // good — `new` names the type
var id = (UserId)token;                         // good — cast names the type
decimal total = order.Sum(l => l.Amount);       // good — method result, type written out
var projected = users.Select(u => new { u.Id, u.Name }); // var required — anonymous type
```
**Enforcement:** `IDE0007`/`IDE0008` (`var`/explicit-type preferences) configured `for_built_in_types` and `when_type_is_apparent` in `.editorconfig`; review.

### 4.2 — Use target-typed `new()` only when the type is named on the left of a declaration.

**Reasoning, step by step:**
1. `Dictionary<Sku, decimal> prices = new(capacity)` names the type once, on the left, and the `new()` reads as "the same, constructed" — no repetition, no loss of clarity. The same holds for a field initializer (`private readonly List<Order> _orders = new();`), where the declared field type carries the name. This is the inverse of 4.1: there, the type lives on the right; here, on the left. Exactly one side always names it.
2. Never use target-typed `new()` where no nearby declaration names the type — not on a reassignment (`prices = new(...)`), not as a bare method argument (`Process(new())`), not as a `return new()` where the reader must consult the signature. In those positions the type is invisible at the call site, which is the same defect 4.1 forbids; write `new Dictionary<Sku, decimal>(...)` so the construction states its own type.

**Worked example:**
```csharp
StringBuilder sb = new();                       // good — type on the left
private readonly List<Order> _orders = new();   // good — field type names it
Process(new());                                  // bad — argument, type invisible at call site
return new(id, name);                            // bad — reader must read the signature
```
**Enforcement:** `IDE0090` (use target-typed `new`) with `prefer_simplified_object_creation` scoped to declarations; review rejects target-typed `new()` in argument and `return` position.

### 4.3 — Make every binding `readonly` by default; justify reassignment.

**Reasoning, step by step:**
1. Immutability is the choice you have to type (root rule 3), so a field is `readonly` unless it is proven to need reassignment, and a local is bound once unless a loop or accumulator genuinely requires rebinding. A `readonly` field is a guarantee the constructor sets it and nothing else moves it, which the reader gets for free and the JIT can sometimes exploit. The default removes a whole class of "who changed this?" questions.
2. When a binding must change — a running total, a retry counter, a swapped reference — that mutation is the exception, so it is localized, named, and obvious. Prefer rebuilding a new value with a `with`-expression or a LINQ `Aggregate` over mutating in place (root rule 6); reach for a mutable local only when the transform genuinely needs one, and keep its scope as small as the loop that drives it.

**Worked example:**
```csharp
private readonly Clock _clock;                  // good — set once in the constructor
decimal subtotal = lines.Sum(l => l.Amount);    // good — bound once, never reassigned
int attempts = 0;                                // justified mutation — a bounded retry counter
while (attempts < MaxRetries && !TrySend()) attempts++;
```
**Enforcement:** `IDE0044` (make field readonly); `CA1805` pairs in; review flags an unjustified mutable local.

### 4.4 — Reserve `const` for compile-time constants; use `static readonly` for runtime ones, in that modifier order.

**Reasoning, step by step:**
1. `const` is for a value the compiler can bake into the IL at the point of use — a literal `int`, `string`, `bool`, or `enum`. It is PascalCase (chapter [02](./02-naming-conventions.md)) and it is inlined into every referencing assembly, which is why a `const` in a public API is a versioning hazard: changing it does nothing until every consumer recompiles. Keep public `const` to genuinely eternal values and prefer `static readonly` across an assembly boundary.
2. A value computed at runtime — `TimeSpan.FromMinutes(5)`, `new Regex(...)`, an array — cannot be `const`; it is `static readonly`, written in that order (`static readonly`, never `readonly static`), and it is initialized once at type load. Choosing `const` versus `static readonly` is choosing inlining versus a single shared instance; pick the one whose semantics you actually want, not whichever compiles.

**Worked example:**
```csharp
private const int MaxHops = 3;                          // compile-time, inlined
private const string SchemeName = "dexpace";            // compile-time literal
private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30); // runtime value
private static readonly char[] s_separators = [',', ';'];              // runtime array
```
**Enforcement:** `CA1802` (use literals where appropriate) flags a `static readonly` that should be `const`; review for the public-`const` versioning trap; `.editorconfig` modifier-order rule (`IDE0036`).

### 4.5 — State visibility explicitly, as the first modifier on every declaration.

**Reasoning, step by step:**
1. C# defaults an un-prefixed member to `private` (or `internal` at the top level), but relying on the default makes the reader compute the accessibility instead of reading it. Write `private`, `internal`, `public`, or `protected` on every type and member so the surface is visible at a glance and a future edit cannot silently widen it. Default to `internal` for types and `private` for members, and widen only with a reason (chapter [10](./10-api-design.md)).
2. Visibility comes first in the modifier list, ahead of `static`, `readonly`, `sealed`, `async`, and the rest, matching the runtime style and `.editorconfig`'s required order. A consistent order means the eye finds the accessibility in the same column every time, so scanning a file for its public surface is a vertical read, not a search.

**Worked example:**
```csharp
public static readonly Encoding Utf8 = new UTF8Encoding(false); // visibility first, then static readonly
internal sealed class Router { }                                // explicit internal, not the default
private async Task Drain(CancellationToken ct) { }              // private stated, then async
```
**Enforcement:** `IDE0040` (add accessibility modifiers); `IDE0036` (order modifiers); both promoted to errors by warnings-as-errors (chapter [01](./01-formatting-and-tooling.md)).

### 4.6 — Avoid `this.`; let the `_` field prefix do the disambiguating.

**Reasoning, step by step:**
1. The runtime style does not use `this.` to qualify member access, because the field prefixes already separate the namespaces: `_count` is a field, `count` is a parameter or local, and no `this.` is needed to tell them apart (chapter [02](./02-naming-conventions.md)). Sprinkling `this.` adds noise that the prefix convention exists precisely to avoid, and an inconsistent `this.` invites the reader to wonder what the unqualified accesses refer to.
2. The single legitimate `this` is when you genuinely pass the current instance as a value — registering `this` with an event source, returning `this` for a fluent step, or calling an extension method on `this`. That is using the reference, not qualifying a member, and it reads as such; a constructor that assigns `_field = argument` never needs it.

**Worked example:**
```csharp
public OrderRouter(Clock clock) => _clock = clock;  // good — prefix disambiguates, no this.
public void Attach(EventBus bus) => bus.Register(this); // good — `this` is the value passed
this._clock = clock;                                 // bad — redundant qualification
```
**Enforcement:** `IDE0003`/`IDE0009` (`dotnet_style_qualification_for_*` set to `false`); review.

### 4.7 — Declare one thing per line and one statement per line.

**Reasoning, step by step:**
1. A line that declares two fields (`int x, y;`) or chains two statements behind a semicolon hides the second behind the first — a diff touches both, a breakpoint cannot sit on one, and a reader scanning the left margin misses it. One declaration per line means each name, type, and initializer occupies its own row, so version control attributes a change to exactly the binding it touched.
2. The same applies to statements: no `if (x) DoA(); DoB();` smuggled onto one line, no comma-spliced side effects. Whitespace is free and cramming is never worth the saved row (root rule 10); the loop variable and its body each get their own line so the structure is visible without parsing.

**Worked example:**
```csharp
int width = 0;                                  // good — one declaration per line
int height = 0;
int x = 0, y = 0;                                // bad — two bindings, one diff target
if (ready) Start(); Log();                       // bad — Log() runs unconditionally, easy to misread
```
**Enforcement:** `dotnet format` line layout; review rejects multi-declarator and comma-spliced lines.

### 4.8 — Quarantine `unsafe` and pointers to one declared, justified module.

**Reasoning, step by step:**
1. `unsafe` switches off the memory-safety guarantees that make the rest of this guide hold, so it is never sprinkled inline to shave a bounds check. When a measured hot path genuinely needs raw pointers or `stackalloc` beyond the safe `Span<T>` surface (chapter [15](./15-performance.md)), confine it to a single, named interop or performance module that sets `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in that one project — never in `Directory.Build.props` for the whole solution.
2. The module carries a why-comment stating the benchmark or the native contract that justifies dropping into `unsafe`, and the unsafe surface is wrapped in a safe, validated API so callers never touch a pointer. Containment means a `NullReferenceException`-class memory bug has exactly one place to live, and a reviewer auditing safety reads one file, not the whole tree.

**Worked example:**
```csharp
// In Dexpace.Interop.csproj only: <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
internal static unsafe class NativeHash // why: P/Invoke to libxxhash, benchmarked 8x over managed
{
    public static ulong Hash(ReadOnlySpan<byte> data) // safe wrapper — callers never see a pointer
    {
        fixed (byte* p = data) return xxh3_64(p, (nuint)data.Length);
    }
}
```
**Enforcement:** `<AllowUnsafeBlocks>` scoped per-project, never solution-wide; `CA1401`/`CA2101` on P/Invoke; review requires the justifying why-comment.

### 4.9 — Inline `out var` at the call site for the Try-pattern; keep `ref`/`in`/`out` disciplined.

**Reasoning, step by step:**
1. The Try-pattern's `out` parameter reads cleanest declared inline — `if (dict.TryGetValue(key, out var value))` binds `value` exactly where the `bool` decides it is valid, and a `[NotNullWhen(true)]` annotation (chapter [03](./03-nullability-and-the-type-system.md)) makes it non-null inside the branch. The `out var` is the one place `var` is idiomatic without a named right-hand side, because the method signature supplies the type and the call site cannot repeat it.
2. Beyond `out` on the Try-pattern, by-reference parameters are a deliberate, rare choice. Prefer `in` (or `ref readonly`) to pass a large `readonly struct` without copying (chapter [15](./15-performance.md)); reserve `ref` for genuine in-place mutation of a caller's value; and avoid `out` as a way to return multiple results — return a tuple or a `record` instead, because a method that fills several `out` parameters is harder to call and to reason about (`CA1021`).

**Worked example:**
```csharp
if (_cache.TryGetValue(key, out var hit)) return hit; // good — out var inline with the check
public static decimal Dot(in Vector3 a, in Vector3 b) => /* … */; // good — in avoids copying
public void Parse(string s, out int value, out bool ok) { } // bad — multiple out; return a record
```
**Enforcement:** `IDE0018` (inline variable declaration); `CA1021` (avoid `out` parameters); review of `ref`/`in` usage.

## Cross-references

- The `.editorconfig` `var` preferences, modifier order, and `<AllowUnsafeBlocks>` placement: [01-formatting-and-tooling.md](./01-formatting-and-tooling.md). Field prefixes, the no-`this.` convention, and constant casing: [02-naming-conventions.md](./02-naming-conventions.md).
- `[NotNullWhen(true)]` on the Try-pattern and nullable narrowing: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). `Span<T>`, `in`/`ref readonly`, and `stackalloc`: [13-resource-management.md](./13-resource-management.md).
