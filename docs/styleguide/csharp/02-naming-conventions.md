# 02 — Naming Conventions

A name is the first documentation a reader meets and the last thing a refactor touches. C# has a near-universal casing convention; this chapter restates it, fixes the field-prefix discipline the runtime uses, and records the two places dexpace house style overrides the ecosystem: no `I` prefix on our interfaces, no `Async` suffix on our methods. Names are designed for the call site, not the declaration.

## What good looks like

```csharp
namespace Dexpace.Ordering;

public sealed class OrderRouter            // PascalCase type (2.1)
{
    private readonly Clock _clock;         // _camelCase private field; role-named interface, no `I` (2.3, 2.6)
    private static readonly TimeSpan s_maxHold = TimeSpan.FromMinutes(5); // s_ static (2.3)

    public OrderRouter(Clock clock) => _clock = clock; // camelCase parameter (2.2)

    public Task<RouteDecision> Route(Order order, CancellationToken ct) // no `Async` suffix (2.7)
    {
        ArgumentNullException.ThrowIfNull(order);
        const int MaxHops = 3;             // PascalCase constant (2.4)
        return RouteCore(order, MaxHops, ct);
    }
}
```

The type and every member are PascalCase (2.1); the parameter and locals are camelCase (2.2); the private field carries `_` and the private static carries `s_` (2.3); the constant is PascalCase (2.4). `Clock` is a first-party interface named for its role with no `I` (2.6), and `Route` returns a `Task` without an `Async` suffix (2.7) — the two recorded deviations. `int` and `Task`, not `Int32` and `System.Threading.Tasks.Task`, because language keywords win (2.5).

## Rules

### 2.1 — PascalCase every type and every member; use language keywords for built-in types.

**Reasoning, step by step:**
1. Classes, structs, records, enums, delegates, interfaces, and all public members — methods, properties, events, public fields, constants, local functions — are PascalCase. This is the convention every C# reader and every BCL surface already assumes, so deviating costs comprehension for no gain.
2. For the built-in types use the language keyword, not the BCL name: `int` not `Int32`, `string` not `String`, `float` not `Single`, including on static calls (`int.Parse`, not `Int32.Parse`). The keyword is the idiom and reads as part of the language; the BCL name reads as a library type and invites the question "is this a different type?"

**Worked example:**
```csharp
public sealed record Money(decimal Amount, string Currency);   // PascalCase record + properties
public static decimal Sum(IReadOnlyList<Money> lines) => /* … */; // int/string/decimal keywords
```
**Enforcement:** `dotnet_naming_rule` for types/members in `.editorconfig`; `IDE0049` (use language keywords).

### 2.2 — camelCase locals, parameters, and primary-constructor parameters.

**Reasoning, step by step:**
1. Local variables, method parameters, and lambda parameters are camelCase with no prefix. A primary-constructor parameter on a `class` or `struct` is also camelCase — it behaves like a parameter, so it reads like one (MS Learn). On a positional `record`, by contrast, the parameter *is* a public property, so it takes PascalCase (chapter [06](./06-types-and-data-modeling.md)).
2. The name carries semantic meaning, not type information: `iterations`, not `intCount`; `customer`, not `cust`. Reserve single letters for loop counters and the syntax-spec type-parameter conventions. Hungarian prefixes encode what the type already says and rot the moment the type changes.

**Worked example:**
```csharp
public sealed class DataService(WorkerQueue workerQueue) // class primary param: camelCase
{
    public void Enqueue(string payload) => workerQueue.Add(payload);
}
public record Person(string FirstName, string LastName); // record positional param: PascalCase
```
**Enforcement:** `.editorconfig` naming rules; review rejects Hungarian and abbreviations.

### 2.3 — Prefix instance fields with `_`, statics with `s_`, thread-statics with `t_`; make them `readonly`.

**Reasoning, step by step:**
1. The runtime's field convention is load-bearing: a leading `_` marks a private or internal instance field, `s_` a static field, `t_` a `[ThreadStatic]` field. Typing `_` in a completion-aware editor lists exactly the object-scoped state, and the prefix removes the need for `this.` to disambiguate a field from a parameter (rule 2.8 of the runtime style; we avoid `this.`).
2. `readonly` comes by default and `static readonly` in that order, never `readonly static`. A field you never reassign after construction should say so, because immutability is the choice you have to type (root rule 3). Public fields are rare and, when used, are PascalCase with no prefix — but prefer a property (chapter [10](./10-api-design.md)).

**Worked example:**
```csharp
private readonly HttpClient _client;
private static readonly JsonSerializerOptions s_json = new() { WriteIndented = false };
[ThreadStatic] private static StringBuilder? t_scratch;
```
**Enforcement:** `.editorconfig` naming rules for `private`/`internal` fields, static fields, and thread-static fields; `IDE0044` (make field readonly).

### 2.4 — PascalCase all constants, local and field alike.

**Reasoning, step by step:**
1. C# constants — `const` fields and `const` locals — are PascalCase, not the SCREAMING_CASE other languages use. A `const` is just a compile-time value with a name; the casing matches every other named member so the eye does not have to switch alphabets mid-file.
2. The lone exception is interop, where a `const` mirrors the exact name and value of the native symbol it maps to; matching the foreign name verbatim is worth more there than house casing. Everywhere else, PascalCase.

**Worked example:**
```csharp
private const int MaxRetries = 3;          // PascalCase, not MAX_RETRIES
const double DegreesPerRadian = 57.295779; // local const, same rule
```
**Enforcement:** `.editorconfig` naming rule for constants; review.

### 2.5 — Use `nameof` instead of string literals for member and parameter names.

**Reasoning, step by step:**
1. A string literal naming a parameter (`"order"` in an `ArgumentException`) or a property (in `OnPropertyChanged`) is a duplicate of the real identifier that a rename will not touch, so it silently goes stale and starts lying. `nameof(order)` is the same text the compiler resolves to the symbol, so a rename updates it and a typo fails to compile.
2. Use it for argument-exception parameter names, property-change notifications, logging field names, and anywhere a string would otherwise echo an identifier. It costs nothing at runtime — `nameof` is a compile-time constant.

**Worked example:**
```csharp
if (hops < 0) throw new ArgumentOutOfRangeException(nameof(hops), hops, "must be non-negative");
```
**Enforcement:** review; `CA2208` (instantiate argument exceptions correctly) catches the common misuse.

### 2.6 — Name first-party interfaces for their role, with no `I` prefix.

**Reasoning, step by step:**
1. An interface is a contract describing *what a thing does*, and the name should say that: `Clock`, `WorkerQueue`, `PaymentGateway`. The `I` prefix is Hungarian notation for "this is an interface," a fact the `interface` keyword and the IDE already convey, and it leaks an implementation kind into a name that exists to hide implementation. This is the dexpace house convention shared with the Go, Kotlin, and TypeScript guides, and a deliberate deviation from the runtime style — recorded in the [README](./README.md) ledger.
2. The implementation takes the qualified name: `SystemClock : Clock`, `RedisWorkerQueue : WorkerQueue`. The boundary is firm: we never rename interfaces we do not own — `IDisposable`, `IEnumerable<T>`, `ILogger<T>` keep their framework names, because consistency with the BCL at the point of use beats local purity.

**Worked example:**
```csharp
public interface Clock { DateTimeOffset UtcNow { get; } }      // role-named, no `I`
public sealed class SystemClock : Clock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
public sealed class Service(Clock clock) : IDisposable { /* BCL interface keeps its `I` */ }
```
**Enforcement:** review; the default `IDE1006`/naming analyzer that *requires* the `I` prefix is disabled in `.editorconfig` for this guide.

### 2.7 — Drop the `Async` suffix; name a method for what it does.

**Reasoning, step by step:**
1. A method's return type is part of its signature: a reader and the compiler both see `Task<User>`, so appending `Async` to the name restates type information the signature already carries, which is the same Hungarian instinct rule 2.6 rejects. House style names behaviour — `LoadUser`, `Route`, `Charge` — whether or not the method is asynchronous, so renaming a method to `async` later does not churn every call site.
2. The suffix existed to warn "you must await this," and we replace that warning with tooling: CS4014 flags a forgotten `await`, and `CA2007`/`xUnit1031` flag sync-over-async and unobserved tasks (chapter [09](./09-concurrency.md)). This is a recorded deviation from the TAP guideline. The boundary mirrors 2.6: an *override* or *interface implementation* of a framework member that ends in `Async` keeps the inherited name, because you cannot rename what you override.

**Worked example:**
```csharp
public Task<User> LoadUser(UserId id, CancellationToken ct);    // good — no `Async` suffix
public override Task DisposeAsync();                            // boundary: inherited name kept
```
**Enforcement:** review; CS4014 and CA2007 cover the await-safety the suffix used to signal.

### 2.8 — Prefix descriptive generic type parameters with `T`; use a bare `T` only when one says it all.

**Reasoning, step by step:**
1. A single, self-evident type parameter is `T` (`List<T>`, `Predicate<T>`). The moment there is more than one, or the role matters, give it a descriptive name prefixed with `T`: `TKey`/`TValue`, `TSession`, `TOutput`. The prefix marks it as a type parameter at a glance and the suffix documents the constraint it carries (chapter [06](./06-types-and-data-modeling.md)).
2. A type parameter that conveys nothing (`TItem` on a method that treats it as `object`) is ceremony; either constrain it so the name earns its place or remove the genericity. Naming is the cheapest place to document a generic's intent.

**Worked example:**
```csharp
public interface SessionChannel<TSession> { TSession Session { get; } }      // descriptive, T-prefixed
public static TOutput Convert<TInput, TOutput>(TInput from, Func<TInput, TOutput> f) => f(from);
```
**Enforcement:** `CA1715` (identifiers have correct prefix); review of multi-parameter generics.

## Cross-references

- Where the `.editorconfig` naming rules live and how they gate the build: [01-formatting-and-tooling.md](./01-formatting-and-tooling.md). Record positional parameters as PascalCase properties: [06-types-and-data-modeling.md](./06-types-and-data-modeling.md).
- `ArgumentNullException.ThrowIfNull` and the guard idiom: [05-methods-and-functions.md](./05-methods-and-functions.md). The `Async`-suffix safety net (CS4014, CA2007): [09-concurrency.md](./09-concurrency.md).
- Public-field-versus-property and the public-surface naming contract: [10-api-design.md](./10-api-design.md). The two deviations recorded: [README](./README.md).
