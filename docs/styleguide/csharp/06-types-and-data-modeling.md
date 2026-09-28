# 06 — Types & Data Modeling

Model the domain as data and functions, not as an object hierarchy with behaviour woven through inheritance. The discipline of this chapter is one idea: make illegal states unrepresentable, so the type checker rejects the bad value before a test ever runs. Records carry immutable data, closed hierarchies carry choice, and the only base class you write exists to back a discriminated union.

## What good looks like

```csharp
namespace Dexpace.Billing;

// A closed hierarchy: the abstract base is the only base class, every case is sealed (6.2, 6.3).
public abstract record Payment
{
    private Payment() { }                                            // private ctor closes the set (6.3)

    public sealed record Card(string Last4, ExpiryMonth Expiry) : Payment;
    public sealed record BankTransfer(Iban Account) : Payment;
    public sealed record Cash : Payment;
}

public static class PaymentFees
{
    // Closed-hierarchy switch with no `_`: once CTH001 is on, adding a case without an arm fails the build (6.3).
    public static decimal Fee(Payment payment) => payment switch
    {
        Payment.Card => 0.029m,
        Payment.BankTransfer => 0.008m,
        Payment.Cash => 0m,
    };
}

public sealed record Invoice
{
    public required InvoiceId Id { get; init; }                     // required + init forces construction (6.5)
    public required Payment Method { get; init; }
    public IReadOnlyList<LineItem> Lines { get; init; } = [];

    public Invoice WithMethod(Payment method) => this with { Method = method }; // update, never mutate (6.1)
}
```

`Payment` is the one abstract base we write, its set closed by a private constructor and three `sealed record` cases (6.2, 6.3); `Fee` matches them with a `switch` listing every case, so a new case without an arm fails the build (6.3). `Invoice` is an immutable `record` updated by `with` (6.1), forced complete at construction by `required` + `init` (6.5), and `PaymentFees` reuses no inheritance — it is a free function over the data (6.7). `InvoiceId` is a branded value, not a bare `Guid` (cross-ref [03](./03-nullability-and-the-type-system.md)).

## Rules

### 6.1 — Model immutable data as `record` types; update with `with`, never by mutation.

**Reasoning, step by step:**
1. A `record` gives you value equality, a readable `ToString`, and `with`-expression copying for free, which is exactly the contract immutable data wants. Two `Money(10, "USD")` values being equal because their contents are equal is the behaviour you mean; reference identity is not. Immutability is the default because a value that cannot change cannot be changed behind your back, by another thread or another method (root rule 3).
2. To produce a changed value, copy it with `with` into a new instance and bind the result; never reach in and reassign a property. The `with` expression is non-destructive — the original is untouched, so any reference still holding it sees a stable value. A property you would otherwise `set` becomes `init`-only (6.5), and mutation becomes the thing you have to type, which is the right way around.

**Worked example:**
```csharp
public sealed record Money(decimal Amount, string Currency);
Money discounted = price with { Amount = price.Amount * 0.9m };   // new value, price unchanged
```
**Enforcement:** review of type-kind choice; `init`-only properties make stray `set` a compile error.

### 6.2 — Make every class `sealed` by default.

**Reasoning, step by step:**
1. An unsealed class is an open invitation to inherit, and inheritance for code reuse is the coupling this guide rejects (root rule 5). `sealed` states that this type is a leaf — no one will override its members or substitute a derived type — which lets the reader reason about it in isolation and lets the JIT devirtualize its calls (chapter [15](./15-performance.md)). Seal first; unseal only with a reason.
2. The single class you leave unsealed is the `abstract` base of a closed hierarchy (6.3), and even then the leaves are `sealed`. A class that is neither sealed nor the root of a deliberate hierarchy is a design that has not decided what it is. If you want polymorphism, write the closed hierarchy; if you want reuse, inject an interface and delegate (6.7).

**Worked example:**
```csharp
public sealed class OrderRouter { /* leaf type, no one inherits */ }
public abstract record Shape { private Shape() { } /* the one unsealed type: a closed base */ }
```
**Enforcement:** `CA1052` (static holder types sealed), review requires a reason to leave a class unsealed; analyzer can require `sealed` on concrete classes.

### 6.3 — Make illegal states unrepresentable: a closed hierarchy matched exhaustively, not a nullable bag.

**Reasoning, step by step:**
1. A class with one nullable field per variant — `CardNumber?`, `BankAccount?`, an `IsCash` bool — lets the compiler accept a card payment that also has a bank account, a state your code must defend against at every read. Model the choice instead as an `abstract record` whose set of cases is closed by a private constructor and whose every case is a `sealed record`. Now "a payment is exactly one of card, transfer, or cash" is a fact the type system enforces, and the impossible combinations cannot be constructed.
2. Consume the hierarchy with a `switch` expression carrying one arm per case and no `_` default. C# 14 does not prove a record or class hierarchy exhaustive on its own — a record's implicit copy constructor means the compiler never treats the set as closed, and the `closed` modifier that fixes this natively is a C# 15 feature — so a no-`_` switch raises CS8509 by itself. Recover the guarantee with the `SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression` suppressor (`CTH001`), which silences CS8509 only while every case is covered and lets it re-fire the instant a teammate adds a case, handing you the list of sites to fix. A discriminated union kept whole, matched this way, turns a class of runtime bugs into build failures (chapter [07](./07-csharp-idioms.md)).

**Worked example:**
```csharp
public abstract record Shape
{
    private Shape() { }
    public sealed record Circle(double Radius) : Shape;
    public sealed record Rect(double W, double H) : Shape;
}
public static double Area(Shape s) => s switch    // no `_`: with CTH001 on, a new Shape case re-fires CS8509 and breaks the build (6.3)
{
    Shape.Circle c => Math.PI * c.Radius * c.Radius,
    Shape.Rect r => r.W * r.H,
};
```
**Enforcement:** `CS8509` promoted to error, plus the `SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression` suppressor with `dotnet_diagnostic.CTH001.suppress_on_record_hierarchies = true` in `.editorconfig` (records need the opt-in; under `<Nullable>enable</Nullable>` a non-nullable input needs no `null` arm). The dependency-free alternative is a `_ => throw new UnreachableException()` arm with a per-case test. Review rejects nullable-bag modeling.

### 6.4 — Parse, don't validate: a factory returns the proven type or a typed failure, never a bool plus raw data.

**Reasoning, step by step:**
1. A `bool Validate(input)` that returns `true` leaves you holding the same unvalidated type you started with, so the knowledge "this is valid" lives only in control flow and evaporates the moment the value is passed on. The next reader cannot tell a checked value from an unchecked one, so they check again — or forget to. Instead, a factory consumes the raw input and returns the proven domain type, so possessing the type *is* the proof of validity (chapter [03](./03-nullability-and-the-type-system.md)).
2. When failure is expected and routine, the factory returns a `Result` carrying either the parsed value or a typed error rather than throwing (chapter [08](./08-error-handling.md)); when an invalid input is a caller bug, it throws. Either way the only path to the domain type runs through validation, so the interior receives values it can trust without re-checking, and the check lives in exactly one place.

**Worked example:**
```csharp
public readonly record struct ExpiryMonth
{
    public int Year { get; }
    public int Month { get; }
    private ExpiryMonth(int year, int month) => (Year, Month) = (year, month);
    public static Result<ExpiryMonth, string> Parse(int year, int month) =>
        month is >= 1 and <= 12
            ? new Result<ExpiryMonth, string>.Ok(new ExpiryMonth(year, month)) // proven type, not a bool
            : new Result<ExpiryMonth, string>.Err("month out of range");
}
```
**Enforcement:** review of boundary modules; the private constructor makes the factory the only generator.

### 6.5 — Force construction-time completeness with `required` and `init`, not multi-step setters.

**Reasoning, step by step:**
1. A type built by constructing it and then calling a sequence of setters has a window — between `new` and the last setter — where it is half-formed and invariant-violating, and nothing stops a caller from forgetting the last step. `required` closes the window: the compiler refuses to compile a construction expression that omits a `required` member, so a fully formed value is the only kind you can make.
2. Pair `required` with `init`-only accessors so the members can be set in the object initializer but never reassigned afterward, giving you immutability (6.1) and mandatory initialization together without a hand-written constructor for every field. The object initializer reads as a named-argument list, and a missing field is a compile error rather than a `null` discovered at runtime.

**Worked example:**
```csharp
public sealed record HttpRequestSpec
{
    public required Uri Url { get; init; }                     // omitting Url fails to compile
    public required HttpMethod Method { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = ImmutableDictionary<string, string>.Empty;
}
var spec = new HttpRequestSpec { Url = endpoint, Method = HttpMethod.Get }; // complete or it won't build
```
**Enforcement:** `IDE0250`/`required` checked by the compiler; review prefers `required` + `init` over post-construction setters.

### 6.6 — Use `readonly struct` for small immutable values; never a large mutable struct, never a public mutable struct field.

**Reasoning, step by step:**
1. A small value type — a point, a money amount, a branded id (chapter [03](./03-nullability-and-the-type-system.md)) — earns its keep as a `readonly struct` or `readonly record struct`: no heap allocation, cheap copying, and value semantics. The `readonly` modifier on the struct itself guarantees no member mutates `this`, which lets the JIT skip defensive copies when the value is passed by `in` (chapter [15](./15-performance.md)) and tells the reader the value is frozen.
2. A mutable struct is a trap: it copies on every assignment, argument pass, and collection access, so a mutation you write lands on a copy and vanishes, and the bug is invisible at the call site. A large struct copies a lot of bytes on each of those, erasing the allocation win. Keep structs small and `readonly`; if you need a large or mutable value, make it a `record` class (6.1). A public mutable field on any struct compounds both faults — expose data through `init`-only properties.

**Worked example:**
```csharp
public readonly record struct Point(double X, double Y);            // small, immutable, no heap
public readonly struct Temperature(double celsius)                  // readonly: no member mutates this
{
    public double Celsius { get; } = celsius;
    public double Fahrenheit => Celsius * 9 / 5 + 32;
}
```
**Enforcement:** `CA1815` (override equality on value types), `CA1051` (do not declare visible instance fields); review rejects large or mutable structs.

### 6.7 — Reuse code by composing injected interfaces, never by inheriting a base class.

**Reasoning, step by step:**
1. Inheriting from a base class to share its methods binds the subclass to the base's internals, its protected surface, and its construction order, and that coupling only tightens as the base grows. The shared behaviour you wanted becomes a hierarchy you cannot change without auditing every descendant. Composition keeps the pieces separable: depend on a small interface (named for its role, chapter [02](./02-naming-conventions.md)), receive an implementation through the constructor, and delegate to it (root rule 5).
2. Small interfaces compose where deep trees do not — a type can hold a `Clock`, a `WorkerQueue`, and a `PaymentGateway` without contorting into a single lineage, and each collaborator is swappable in a test. The only `abstract`/`virtual` you write is the closed hierarchy of 6.3, which models a *choice between cases*, not a *sharing of code*. Reuse is delegation; polymorphism is a sealed hierarchy matched with a `switch`.

**Worked example:**
```csharp
public interface Clock { DateTimeOffset UtcNow { get; } }
public sealed class OrderService(Clock clock, PaymentGateway gateway)  // compose, don't inherit
{
    public Receipt Charge(Order order) => gateway.Charge(order, clock.UtcNow);
}
```
**Enforcement:** review rejects inheritance for code reuse; `abstract`/`virtual` permitted only for a closed hierarchy.

### 6.8 — Give every enum an explicit underlying type and the right singular or plural name; promote to a hierarchy when behaviour attaches.

**Reasoning, step by step:**
1. A non-flags enum names a single choice, so it takes a singular noun (`OrderState`, `LogLevel`); a `[Flags]` enum names a combinable set, so it takes a plural noun (`FileAccessRights`) and assigns explicit power-of-two values. Give every enum an explicit underlying type (`: byte`, `: int`) so a serialized or interop value has a defined width that a later reorder cannot shift. The casing is PascalCase on the type and every member (chapter [02](./02-naming-conventions.md)).
2. The moment behaviour starts attaching to enum cases — a `switch` on `OrderState` here to compute a fee, another there to pick a handler, a third somewhere to format a label — the logic for one case is scattered across the codebase and the next case must be added in every spot. That is the signal to promote the enum to a closed hierarchy (6.3), where each case is a type that carries its own data and the behaviour lives with it or in one exhaustive `switch`. Reserve enums for a plain, behaviour-free tag.

**Worked example:**
```csharp
public enum LogLevel : byte { Trace, Debug, Info, Warning, Error }   // singular, explicit width
[Flags]
public enum FileAccessRights : int { None = 0, Read = 1, Write = 2, Execute = 4 } // plural, powers of two
```
**Enforcement:** `CA1714` (flags enums plural) / `CA1717` (non-flags singular), `CA1027`/`CA2217` (flags values); review promotes behaviour-bearing enums to hierarchies.

## Cross-references

- Branded primitives, parse-don't-validate at the boundary, and `record`-vs-`struct`-vs-`class`: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). Positional record parameters as PascalCase properties: [02-naming-conventions.md](./02-naming-conventions.md).
- Composition through injected interfaces and the no-inheritance-for-reuse rule: [05-methods-and-functions.md](./05-methods-and-functions.md). Exhaustive `switch` expressions and pattern matching: [07-csharp-idioms.md](./07-csharp-idioms.md).
- The opt-in `Result` type a factory returns on expected failure: [08-error-handling.md](./08-error-handling.md). Immutable record DTOs and the minimal public surface: [10-api-design.md](./10-api-design.md).
