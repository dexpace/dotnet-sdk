# 07 — C# Idioms

Modern C# has a vocabulary of constructs that turn five lines of ceremony into one line of intent: pattern matching, LINQ pipelines, collection expressions, ranges, raw strings. This chapter is the working set — when to reach for each, and the one rule that governs them all: prefer the standard idiom to a clever one-liner. The point of these features is to make code denser *and* clearer, never one at the cost of the other.

## What good looks like

```csharp
namespace Dexpace.Telemetry;

public static class EventSummary
{
    public static string Describe(IReadOnlyList<Event> events, Severity floor)
    {
        // LINQ pipeline with a meaningful query variable; transform, don't loop-and-mutate (7.2).
        IReadOnlyList<string> notable = events
            .Where(e => e.Severity >= floor)
            .Select(e => e.Message)
            .ToList();

        // Collection expression + spread builds the header line in one target-typed step (7.3).
        string[] header = ["run", .. notable];

        // switch expression with relational + list patterns over an if/else ladder (7.1).
        string verdict = notable switch
        {
            [] => "clean",
            [var only] => $"one issue: {only}",                  // interpolation for short text (7.6)
            { Count: > 100 } => "flooded",
            _ => $"{notable.Count} issues",
        };

        return $"{string.Join(',', header)} -> {verdict}";
    }
}
```

The filter-and-project reads as a pipeline with a named result rather than a mutating `foreach` (7.2), and the header is built with a collection expression and spread (7.3). The `switch` expression replaces a type-and-count ladder using list and relational patterns (7.1), and string interpolation handles the short, flat text (7.6). Nothing here needs a comment to parse — the idiom *is* the explanation (7.8).

## Rules

### 7.1 — Match with patterns and `switch` expressions, not if/else chains or type-test casts.

**Reasoning, step by step:**
1. A `switch` expression evaluates to a value, so it pairs one input with one result per arm and the compiler checks coverage (chapter [06](./06-types-and-data-modeling.md)); an if/else ladder scatters the same decision across mutable assignments where a missed branch is silent. Patterns also fuse the test and the binding: `is { } user`, `is Card { Last4: var last }`, `obj is JsonElement e` test and extract in one step, replacing the `is`-then-cast or the `as`-then-null-check dance. `is not null` is the canonical presence test — it cannot be fooled by an overloaded `==` (chapter [03](./03-nullability-and-the-type-system.md)).
2. The pattern kinds compose: property (`{ Status: Active }`), positional (`(0, var y)`), relational (`> 100`, `is >= 1 and <= 12`), logical (`and`/`or`/`not`), and list (`[]`, `[var first, ..]`) patterns combine into a single readable test. Reach for them whenever a condition inspects shape, type, or structure; reserve a plain `if` for a simple boolean. The result is a flat, exhaustive expression instead of a nested ladder.

**Worked example:**
```csharp
string state = response switch
{
    { StatusCode: >= 200 and < 300 } => "ok",
    { StatusCode: 429 } or { StatusCode: >= 500 } => "retry",
    _ => "fail",
};
```
**Enforcement:** `IDE0066` (use switch expression), `IDE0078`/`IDE0260` (use pattern matching); review of type-test-then-cast.

### 7.2 — Transform with LINQ pipelines; reach for `foreach` only for side effects, early exit, or a measured hot path.

**Reasoning, step by step:**
1. A `Where`/`Select`/`Aggregate` pipeline reads top-to-bottom as the sequence of transformations the data undergoes, each stage named, with no accumulator variable to initialize and mutate and no off-by-one to make. It is "transform, don't mutate" in syntax (root rule 6): input flows in, a new sequence flows out. Give the query result a meaningful name — `activeUsers`, `notable` — not `query` or `q`, because the name documents what the pipeline produces.
2. A `foreach` earns its place where the loop body has an *effect* (writing to a channel, logging each item) or needs an *early exit* (`break`/`return` on a match), since LINQ expresses neither cleanly. The one hard exclusion is a measured hot path: LINQ allocates iterators and closures, so inside a benchmarked loop a plain `foreach` or `for` over a `Span<T>` wins, and that is the only place clarity yields to the profiler (chapter [15](./15-performance.md)).

**Worked example:**
```csharp
IReadOnlyList<string> activeEmails = users
    .Where(u => u.IsActive)
    .Select(u => u.Email)
    .ToList();                                   // good — named pipeline result
foreach (Event e in batch) channel.Writer.TryWrite(e);   // foreach for the side effect
```
**Enforcement:** review prefers a pipeline for pure transforms; chapter [15](./15-performance.md) governs hot-path loops.

### 7.3 — Build collections with collection expressions `[...]` and the spread `..`.

**Reasoning, step by step:**
1. The collection expression `[a, b, c]` is the one syntax for building a list, array, span, or any collection-builder target, and it is target-typed — the element type and the concrete collection come from the left-hand side, so there is no redundant `new List<string>` to read past. It replaces `new[] { ... }`, `new List<T> { ... }`, and `Array.Empty<T>()` (write `[]`) with a single uniform form.
2. The spread element `.. other` inlines an existing sequence into the new collection, so concatenating a header onto a body or merging two sequences is one expression rather than an `AddRange` sequence on a pre-allocated list. The compiler picks an efficient construction for the target, so the dense form is also the fast form. Use `[]` for an empty collection, including as a default for an `init`-only property (chapter [06](./06-types-and-data-modeling.md)).

**Worked example:**
```csharp
int[] window = [first, .. middle, last];          // spread inlines middle, target-typed
IReadOnlyList<string> roles = dto.Roles ?? [];    // [] is the empty default
```
**Enforcement:** `IDE0300`/`IDE0305` (use collection expression), `IDE0301` (empty collection); `IDE0090` (target-typed `new`).

### 7.4 — Slice with ranges `..` and indices `^` instead of arithmetic on `Length`.

**Reasoning, step by step:**
1. The index-from-end operator `^1` and the range operator `start..end` name the slice you mean — last element, "skip the first and drop the last" — without the `arr[arr.Length - 1]` arithmetic that invites an off-by-one and reads as noise. The endpoints follow one rule: inclusive start, exclusive end, so `0..^0` is the whole thing and the lengths subtract cleanly.
2. On arrays and strings a range allocates a copy, but over a `Span<T>` or `ReadOnlySpan<T>` it slices a view with zero allocation, which is why ranges pair with spans in the parsing and hot-path work of chapter [15](./15-performance.md). Use them wherever you take a sub-sequence; the intent-revealing form is also the one that hands the compiler a slice it can make allocation-free.

**Worked example:**
```csharp
ReadOnlySpan<char> body = line[start..^1];        // view, not a copy; drops the trailing char
char last = name[^1];                             // last element, no Length arithmetic
```
**Enforcement:** review prefers ranges/indices over `Length`-arithmetic; pairs with `Span<T>` in [15-performance.md](./15-performance.md).

### 7.5 — Flow nulls with `??`, `??=`, and `?.` where they read clearer than a verbose check.

**Reasoning, step by step:**
1. The null operators collapse a multi-line `if (x is null)` into the expression that uses the value: `?.` short-circuits a member access when the receiver is null, `??` supplies a fallback for a null left side, and `??=` assigns only when the target is currently null. Each is a single, well-known idiom for a null-handling shape that a hand-written check spells out in three or four lines (chapter [03](./03-nullability-and-the-type-system.md)).
2. They are tools, not a mandate: chain `a?.B?.C ?? fallback` when the flow genuinely is "use it if present, else default," but do not contort a branch that does different work per outcome into a nested ternary just to avoid an `if`. When the null path and the non-null path each do real work, `is not null` with two blocks is the clearer form (7.1). Reach for the operators where they shorten *and* clarify.

**Worked example:**
```csharp
string display = user?.Name ?? "anonymous";        // present-or-default in one line
_cache ??= new Dictionary<string, User>();          // initialize once, only if null
```
**Enforcement:** `IDE0031` (use null propagation), `IDE0270` (use coalesce expression); review rejects a contorted ternary.

### 7.6 — Interpolate short strings, raw-literal multi-line and quote-heavy text, and `StringBuilder` in loops.

**Reasoning, step by step:**
1. For a short, flat string built from a few values, `$"{user.Name} <{user.Email}>"` reads as the result it produces, where `+` concatenation or `string.Format` with positional holes does not. For multi-line text or any string carrying quotes, braces, or backslashes — JSON, SQL, a regex, a file path — a raw string literal `"""..."""` removes every escape, so what you type is what you get and the embedded quotes stay legible.
2. Inside a loop, repeated `+=` on a `string` reallocates the whole buffer each pass because strings are immutable, turning an `n`-item join into quadratic copying. Build it with a `StringBuilder` (or `string.Join` for a simple separator-joined sequence), which appends into one growing buffer. The rule is: interpolate the one-liner, raw-quote the literal block, `StringBuilder` the accumulation (chapter [15](./15-performance.md)).

**Worked example:**
```csharp
string greeting = $"Hello, {name}";                 // interpolation for short text
string query = """SELECT * FROM "user" WHERE id = @id""";   // raw literal keeps the quotes
var sb = new StringBuilder();
foreach (string line in lines) sb.AppendLine(line); // StringBuilder, not += in a loop
```
**Enforcement:** review prefers interpolation for short strings, raw literals for escape-heavy strings, and `StringBuilder` for loop concatenation (chapter [15](./15-performance.md) cites `CA1834`, `StringBuilder.Append(char)`).

### 7.7 — Use `nameof` over string literals; keep tuples and deconstruction to local multi-value returns only.

**Reasoning, step by step:**
1. `nameof(member)` is a compile-time constant that a rename updates and a typo fails to compile, so it replaces every string literal that echoes an identifier — argument-exception names, logging keys, property-change notifications (chapter [02](./02-naming-conventions.md)). A bare `"order"` is a duplicate of the symbol that no refactor touches, so it rots into a lie.
2. A tuple with deconstruction is the right tool for a *local* multi-value return: `(int min, int max) = Bounds(data)` names two results without a throwaway type. But `(int, string)` carries no member names a caller can rely on and no place to attach docs or invariants, so it must never appear in a *public* signature — there, return a named `record` so the contract is self-describing (chapter [10](./10-api-design.md)). Tuples are internal plumbing; records are the public face.

**Worked example:**
```csharp
ArgumentOutOfRangeException.ThrowIfNegative(hops, nameof(hops));   // nameof, not "hops"
var (min, max) = ComputeBounds(samples);            // tuple deconstruction, local only
public sealed record Bounds(int Min, int Max);      // public API returns the named record
```
**Enforcement:** `CA2208` (instantiate argument exceptions correctly); review rejects tuples in public signatures.

### 7.8 — Prefer the standard idiom to cleverness: readability beats a one-liner that needs a comment.

**Reasoning, step by step:**
1. Every construct in this chapter exists to make code denser *and* clearer, so a clever line that achieves density by sacrificing clarity defeats the purpose. A nested ternary, a LINQ chain with a side-effecting `Select`, a bit-twiddling trick that needs a comment to explain what it computes — each saves a line and costs every future reader the time to decode it. The next reader is the constraint, and they read the idiom they already know faster than the trick you invented.
2. When two correct forms differ, choose the one a competent C# reader parses without pausing: the named pipeline over the dense aggregate, the `switch` expression over the packed ternary, the two-line `if` over the contorted `??`. If a line needs a comment to say *what* it does (as opposed to *why*), the line is too clever — rewrite it into the standard idiom and delete the comment (root rule 7). Expressiveness is last in the priority order for exactly this reason.

**Worked example:**
```csharp
bool isEven = (n & 1) == 0;                         // bad enough to need explaining? rewrite it
bool isEven = n % 2 == 0;                           // good — the standard idiom, no comment needed
```
**Enforcement:** review rejects a line that requires a "what" comment; prefer the idiom a reader already knows.

## Cross-references

- `nameof` and the identifier-naming rules these idioms lean on: [02-naming-conventions.md](./02-naming-conventions.md). `is not null`, narrowing, and the null operators' nullable context: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md).
- Exhaustive `switch` over a closed hierarchy and discriminated unions kept whole: [06-types-and-data-modeling.md](./06-types-and-data-modeling.md). Result returns the pattern arms branch on: [08-error-handling.md](./08-error-handling.md).
- `Span<T>` slicing with ranges, `StringBuilder` accumulation, and LINQ in hot paths: [15-performance.md](./15-performance.md).
