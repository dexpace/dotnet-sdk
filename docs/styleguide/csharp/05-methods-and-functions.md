# 05 — Methods & Functions

A method is the unit of abstraction, the place assertions live, and the thing a reader holds in their head all at once. This chapter sets its shape: a hard size cap, guard clauses first, dense assertions, and a layout that lets the eye step down from public to private. The body of every method does one thing at one level of abstraction, and proves it as it goes.

## What good looks like

```csharp
namespace Dexpace.Settlement;

public static class Settler
{
    public static SettlementResult Settle(SettleOptions options) // ≥3 inputs → options record (5.5)
    {
        ArgumentNullException.ThrowIfNull(options);              // guard clause first (5.2)
        ArgumentOutOfRangeException.ThrowIfNegative(options.Amount); // public precondition (5.3)

        var net = ApplyFees(options.Amount, options.FeeRate);    // step-down: helper below (5.7)
        Debug.Assert(net <= options.Amount, "fees cannot increase the amount"); // postcondition (5.3)
        return SettlementResult.Cleared(net);
    }

    private static decimal ApplyFees(decimal amount, decimal feeRate) // helper sits below its caller (5.7)
    {
        Debug.Assert(amount >= 0, "amount guarded upstream");    // internal invariant (5.3)
        Debug.Assert(feeRate is >= 0 and <= 1, "rate is a fraction"); // split, positive + negative (5.3)
        return amount * (1 - feeRate);
    }
}
```

`Settle` takes a single `SettleOptions` record because the call would otherwise need three loose arguments (5.5), guards its preconditions before any work (5.2, 5.3), and stays well under the cap (5.1). The happy path runs flush left with no `else` (5.2), each method holds one level of abstraction (5.1), and the private helper sits directly below the public method that calls it (5.7). Both methods carry two or more assertions covering entry and exit (5.3).

## Rules

### 5.1 — Cap a method at 70 lines; aim for 10–30 at one level of abstraction.

**Reasoning, step by step:**
1. Seventy lines is the analyzer-enforced ceiling, deliberately set at the Go guide's level (root rule 9; recorded in the [README](./README.md) ledger), and it is a ceiling, not a target — the working range is 10 to 30 lines. A method you cannot see without scrolling is one you cannot hold in your head, and length is the most reliable proxy for the tangle of branches and state that makes a method hard to test and to change.
2. The deeper rule is one level of abstraction per method: a method either orchestrates named steps or performs one step, never both. When a body mixes high-level flow with byte-twiddling, the fix is not to compress it under the line cap but to extract the low-level part into a named helper (5.7), which usually drops the caller well under 30 lines and gives the extracted logic a place to be asserted and tested.

**Worked example:**
```csharp
public Report Build(ReportRequest request)      // orchestrates — one level of abstraction
{
    ArgumentNullException.ThrowIfNull(request);
    var rows = LoadRows(request.Range);          // each step is a named helper, not inline detail
    var totals = Summarize(rows);
    return Render(totals, request.Format);
}
```
**Enforcement:** `MA0051` (method is too long) configured to 70 lines, promoted to an error (chapter [01](./01-formatting-and-tooling.md)); review rejects mixed abstraction levels.

### 5.2 — Put guard clauses first so the happy path stays flush left.

**Reasoning, step by step:**
1. Validate inputs and reject the impossible at the top, returning or throwing early, so the body below a guard runs only when its preconditions hold. The happy path then flows down the left margin without an `else` or a nested `if`, which is the layout root rule 10 asks for — the reader follows the success case as a straight vertical line and finds every rejection collected at the entrance.
2. An early return is not a violation of single-exit dogma; it is the readable opposite of arrow-shaped nesting, where each `if` pushes the real work another tab to the right until the meaningful code sits in a gutter. Guard, return, and the code that matters keeps its place; nest only when the logic is genuinely conditional, and even then aim for two levels and stop at three (root rule 9).

**Worked example:**
```csharp
public Receipt Charge(Card card, decimal amount)
{
    ArgumentNullException.ThrowIfNull(card);
    if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "must be positive");
    if (card.IsExpired) return Receipt.Declined(DeclineReason.Expired); // reject early, flush left

    return _gateway.Authorize(card, amount);     // happy path, no else, no nesting
}
```
**Enforcement:** review for guard-first layout; `CA1502` (avoid excessive complexity) and the 70-line cap (chapter [01](./01-formatting-and-tooling.md)) push back on the deep nesting guard clauses remove.

### 5.3 — Assert aggressively: at least two assertions per method on average.

**Reasoning, step by step:**
1. Assertions are the cheapest tests and the earliest (root rule 8). Use `ArgumentNullException.ThrowIfNull` and the `ArgumentException`/`ArgumentOutOfRangeException.ThrowIf*` family for public preconditions — these run in every build and protect the caller (5.4 of chapter [08](./08-error-handling.md)) — and `Debug.Assert` for internal invariants that should be impossible, which run in debug and document the assumption for free. Average two per method: a precondition at entry and a postcondition at exit, more where the logic earns it.
2. Split a compound assertion so a failure names the exact clause — `Debug.Assert(a)` then `Debug.Assert(b)`, not `Debug.Assert(a && b)`. Assert both positive and negative space: that the value you expect is present *and* that the value you forbid is absent, because a bug often satisfies one and violates the other. An assertion that has never failed still pays for itself as an executable claim about what the code believes.

**Worked example:**
```csharp
public Window Slice(IReadOnlyList<Sample> samples, int start, int length)
{
    ArgumentNullException.ThrowIfNull(samples);                       // public precondition
    ArgumentOutOfRangeException.ThrowIfNegative(start);              // split — one clause each
    ArgumentOutOfRangeException.ThrowIfGreaterThan(start + length, samples.Count);
    var window = new Window(samples, start, length);
    Debug.Assert(window.Count == length, "slice length must match request"); // postcondition at exit
    return window;
}
```
**Enforcement:** `CA1062` (validate arguments of public methods); `Debug.Assert` for invariants; review checks assertion density and split compounds.

### 5.4 — Use an expression body only for a genuine one-liner; use a block otherwise.

**Reasoning, step by step:**
1. `=>` is for a method or property whose whole body is a single expression — a projection, a delegation, a computed property — where the arrow is shorter and clearer than `{ return …; }`. The moment a body needs a guard clause, a local, an assertion, or two statements, it is not a one-liner, and forcing it into an expression body either drops the guard (5.2) or smuggles logic into a ternary that a block would state plainly.
2. The test is whether the block form would contain exactly one `return` (or one statement) and nothing else; if so, prefer the expression body for its lower ceremony. If the honest block has a guard or an assertion above the work — which 5.2 and 5.3 push most non-trivial methods toward — write the block, because a method that should assert its inputs must have somewhere to put the assertion.

**Worked example:**
```csharp
public decimal WithTax(decimal net) => net * (1 + _taxRate);   // good — one expression
public string Describe() =>                                     // bad — guard belongs above, use a block
    _name is null ? throw new InvalidOperationException() : _name;
public string Describe()                                        // good — block hosts the guard
{
    if (_name is null) throw new InvalidOperationException("name not set");
    return _name;
}
```
**Enforcement:** `IDE0022`/`IDE0025` (expression-body preferences) set to `when_on_single_line`; review.

### 5.5 — Pass an options record once a method takes three or more parameters.

**Reasoning, step by step:**
1. A call with three or more positional arguments is a row of unlabelled values at the call site — `Settle(amount, rate, account, true)` — where a reader cannot tell which `decimal` is which and a transposed pair compiles silently. Collecting them into an immutable `record` gives every argument a name at the call site, lets the caller use object-initializer or `with` syntax, and makes adding a field a non-breaking change instead of another positional slot (chapter [06](./06-types-and-data-modeling.md)).
2. The record also becomes the home for the validation and the defaults: `required` members force the caller to supply what matters, `init` defaults fill the rest, and the parse-don't-validate boundary (chapter [03](./03-nullability-and-the-type-system.md)) can generate a proven options value once. Two well-named parameters need no ceremony; the threshold is three, where the loss of labelling starts to cost correctness.

**Worked example:**
```csharp
public sealed record SettleOptions          // options record replaces a 3+ parameter list
{
    public required decimal Amount { get; init; }
    public required decimal FeeRate { get; init; }
    public AccountId Account { get; init; }
}
public SettlementResult Settle(SettleOptions options) { /* … */ } // one named argument at the call site
```
**Enforcement:** review at three or more parameters; the public-API analyzer flags churn from positional growth; chapter [06](./06-types-and-data-modeling.md) for the record shape.

### 5.6 — Never pass a boolean flag that selects behaviour; split into two named methods.

**Reasoning, step by step:**
1. A `bool` argument that switches what a method does — `Save(order, true)` where `true` means "and notify" — is invisible at the call site, so the reader must open the method to learn what `true` selected, and a careless caller flips the meaning by passing the wrong literal. The flag also means the body has an `if` that does two different jobs, which violates the one-thing-per-method rule (5.1).
2. Split it into two methods named for what each does — `Save(order)` and `SaveAndNotify(order)` — so the call site reads as a sentence and each method has a single responsibility to test and assert. A `bool` that is genuine *data* the method stores or returns (`isEnabled`, `caseSensitive` passed straight to a comparer) is fine; the ban is on the flag that *branches behaviour*, and an `enum` is the answer when there are more than two modes.

**Worked example:**
```csharp
public void Export(Report report, bool asPdf) { if (asPdf) … else … } // bad — flag selects behaviour
public void ExportPdf(Report report) { /* … */ }   // good — two named methods, no flag
public void ExportCsv(Report report) { /* … */ }
public bool Contains(string s, bool caseSensitive); // fine — bool is data passed to a comparer
```
**Enforcement:** `CA1026`-style review; reviewer rejects a behaviour-selecting `bool`, suggests a split or an `enum`.

### 5.7 — Follow the step-down rule; make single-use helpers `static` local functions.

**Reasoning, step by step:**
1. Order a file top-down: the public method comes first, and the private helpers it calls sit directly below it, in call order, so a reader meets each name as an abstraction before meeting its implementation and never scrolls up to find a definition. The file reads like prose — intent, then detail — and the step-down means the most important method is the first one a reader sees.
2. When a helper is used by exactly one method and captures nothing, make it a `static` local function inside that method: it documents the single-use scope, sits next to its only caller, and the `static` keyword forbids accidental capture of the enclosing locals, which would otherwise allocate a closure and create a hidden dependency on mutable state (chapter [15](./15-performance.md)). A helper shared by several methods graduates to a private method, placed below its first caller.

**Worked example:**
```csharp
public IReadOnlyList<Tick> Smooth(IReadOnlyList<Tick> ticks)
{
    ArgumentNullException.ThrowIfNull(ticks);
    return [.. ticks.Select(Normalize)];        // public method first

    static Tick Normalize(Tick t) => t with { Price = Math.Round(t.Price, 2) }; // static — no capture
}
```
**Enforcement:** `IDE0062` (make local function static); review enforces step-down ordering.

### 5.8 — Forbid recursion in library code; use bounded iteration instead.

**Reasoning, step by step:**
1. C# does not guarantee tail-call elimination, so a recursive method's depth is the call stack's depth, and an input deeper than expected — a pathological tree, a cyclic graph, a hostile payload — overflows the stack and crashes the process with no chance to recover (root rule 9). A recursive function also has an unbounded resource (stack frames) that nothing in the signature limits, which is precisely the kind of limit this guide refuses to leave implicit.
2. Rewrite the recursion as iteration over an explicit, bounded structure — a `Stack<T>` or `Queue<T>` you size and cap, a `while` with a depth counter, a worklist with a maximum. The bound becomes a visible constant the reviewer can check, the failure mode becomes a handled `return` or a thrown limit-exceeded exception instead of a `StackOverflowException`, and the traversal is now testable at its limit.

**Worked example:**
```csharp
public IReadOnlyList<Node> Flatten(Node root, int maxDepth) // bound is explicit and checked
{
    ArgumentNullException.ThrowIfNull(root);
    var result = new List<Node>();
    var stack = new Stack<(Node Node, int Depth)>();         // iteration, not recursion
    stack.Push((root, 0));
    while (stack.Count > 0)
    {
        var (node, depth) = stack.Pop();
        if (depth > maxDepth) throw new InvalidOperationException("tree exceeds max depth");
        result.Add(node);
        foreach (var child in node.Children) stack.Push((child, depth + 1));
    }
    return result;
}
```
**Enforcement:** review rejects recursion in library assemblies; a depth bound is mandatory on every traversal.

## Cross-references

- The 70-line cap, the method-length analyzer, and warnings-as-errors: [01-formatting-and-tooling.md](./01-formatting-and-tooling.md). `nameof` in argument exceptions and method naming: [02-naming-conventions.md](./02-naming-conventions.md).
- `ArgumentNullException.ThrowIfNull`, nullable boundaries, and the Try-pattern: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). The options `record`, `required`/`init`, and making illegal states unrepresentable: [06-types-and-data-modeling.md](./06-types-and-data-modeling.md).
- Specific exception types, the `ThrowIf` family, and the `Result` alternative: [08-error-handling.md](./08-error-handling.md).
