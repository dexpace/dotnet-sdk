# 08 — Error Handling

Errors are values, handled explicitly (root rule 4). An exception is for the unexpected — the precondition a caller violated, the disk that vanished mid-write — and it carries a type and a chained cause so the failure is diagnosable from the throw site. Everything routine and expected — a parse that misses, a lookup that finds nothing — is a return value the signature admits, not an exception thrown for control flow.

## What good looks like

```csharp
namespace Dexpace.Pricing;

public static class PriceBook
{
    // Expected miss is a value (8.7); unexpected breakage throws a specific, chained type (8.1, 8.3).
    public static Result<Price, PriceError> Lookup(PriceBook book, Sku sku)
    {
        ArgumentNullException.ThrowIfNull(book);                  // precondition, not a caught error (8.5)

        if (!book.TryGet(sku, out var price))                    // routine absence → typed failure (8.7)
            return new Result<Price, PriceError>.Err(PriceError.Unknown(sku));

        try
        {
            return new Result<Price, PriceError>.Ok(price.Normalize());
        }
        catch (OverflowException ex)                             // catch only what we can wrap (8.2)
        {
            throw new PriceComputationException(sku, ex);        // chain the cause as inner (8.3, 8.6)
        }
    }
}
```

`Lookup` returns a closed `Result` union for the expected miss and validation failure (8.7), throws nothing for them, and reserves exceptions for the genuinely unexpected overflow (8.1). It catches exactly the one type it can act on (8.2), wraps it in a domain exception that chains the original as `InnerException` (8.3, 8.6), and never swallows anything (8.4). The precondition is a `ThrowIf`, not a `catch` (8.5).

## Rules

### 8.1 — Throw a specific exception type; never bare `Exception` or `ApplicationException`.

**Reasoning, step by step:**
1. The type *is* the error's identity: a catch site selects what to handle by type, so `throw new Exception("not found")` forces every would-be handler to either catch everything (8.2) or catch nothing. A specific type — a BCL one like `InvalidOperationException`/`ArgumentException`, or a domain one (8.6) — lets a caller catch precisely the failure it knows how to recover from and let the rest propagate.
2. `Exception` and `ApplicationException` are both too broad to mean anything; `ApplicationException` is a historical artifact the framework guidelines explicitly advise against deriving from. Pick the narrowest type that names the failure: `ArgumentOutOfRangeException` for a bad argument, `InvalidOperationException` for a bad state, a domain type for a domain rule.

**Worked example:**
```csharp
if (balance < amount) throw new Exception("insufficient");            // bad — uncatchable in practice
if (balance < amount) throw new InsufficientFundsException(account);  // good — a type a caller can catch
```
**Enforcement:** `CA2201` (do not raise reserved exception types); review rejects bare `Exception`/`ApplicationException`.

### 8.2 — Catch only what you can handle; gate any broad catch with a `when` filter.

**Reasoning, step by step:**
1. A `catch` you cannot act on is a lie — it claims to handle a failure it merely intercepts, then either rethrows (cost, no benefit) or swallows it (8.4). Catch the specific type you have a recovery for, and let everything else travel to a frame that does. A bare `catch (Exception)` catches `OutOfMemoryException` and `OperationCanceledException` (8.8) alongside the one error you meant, and almost never handles all three correctly.
2. When you genuinely must inspect before deciding — retry only on a transient code, log only a specific SQL number — use an exception filter `when (...)` rather than catch-test-rethrow. The filter runs *before* the stack unwinds, so a non-match leaves the original stack and context intact, where a catch-test-`throw;` has already unwound and re-thrown. The filter expresses "handle this case, ignore the rest" in one place the analyzer can see.

**Worked example:**
```csharp
try { return Send(request); }
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests) // filter, no unwind
{
    return Retry(request);                                            // only the case we can handle
}
```
**Enforcement:** `CA1031` (do not catch general exception types); review requires a `when` filter on any broad catch.

### 8.3 — Rethrow with `throw;`; never `throw ex;`, and chain the cause when wrapping.

**Reasoning, step by step:**
1. `throw ex;` resets the exception's stack trace to the current frame, erasing the line that actually failed — the single most useful fact in a bug report. A bare `throw;` rethrows the caught instance with its original trace preserved, so the stack still points at the throw site. There is no case where `throw ex;` is the right rethrow.
2. When you wrap a low-level failure in a higher-level one, pass the original as the `innerException` argument so the cause chains: `new RepositoryException(msg, ex)`. The chain is what lets a reader walk from "save failed" down to the `SqlException` that caused it; dropping the inner exception throws that diagnostic ladder away. Wrap to add context, never to hide the cause.

**Worked example:**
```csharp
catch (SqlException ex)
{
    // throw ex;                                            // bad — trace reset to here
    throw new RepositoryException("save failed", ex);       // good — cause chained as InnerException
}
```
**Enforcement:** `CA2200` (rethrow to preserve stack details); review requires `innerException` on every wrap.

### 8.4 — Never swallow: no empty `catch`, no catch-and-ignore.

**Reasoning, step by step:**
1. An empty `catch {}` converts a failure into silence — the program keeps running on a broken assumption, and the bug surfaces later as corrupted state with no trace of its origin. Swallowing is strictly worse than crashing, because a crash at least names the failure where it happened. Every caught exception must be handled (recovered, translated, or surfaced) or not caught at all (8.2).
2. "Handled" means an action a reader can point to: a fallback value, a retry, a translated domain error, a logged-and-rethrown record. Catching to log and then continuing as if nothing failed is still swallowing unless continuing is genuinely correct. The only sanctioned no-op catch is a documented one with a why-comment explaining why the failure is provably irrelevant here.

**Worked example:**
```csharp
try { Flush(); } catch { }                                  // bad — failure vanishes silently
try { cache.Evict(key); }
catch (CacheMissException) { /* benign: entry already gone, nothing to evict */ } // good — documented no-op
```
**Enforcement:** `CA1031`; review rejects any empty or commentless ignore-catch.

### 8.5 — Validate preconditions with the `ThrowIf` family, not a defensive `try`/`catch`.

**Reasoning, step by step:**
1. A precondition is the caller's contract, so a violation is a *bug in the caller*, not a runtime condition to recover from. Assert it at the top with `ArgumentNullException.ThrowIfNull` and the `ArgumentException`/`ArgumentOutOfRangeException.ThrowIf*` family (chapter [05](./05-methods-and-functions.md)); these throw the right specific type, capture the parameter name via `[CallerArgumentExpression]`, and read as one line. Wrapping the work in a `try`/`catch` to turn a `NullReferenceException` into a message instead is catching your own bug after the fact.
2. Guard first means the failure lands at the boundary with the offending argument named, not three frames deep where the `null` was finally dereferenced (8.3 keeps that trace, but the `ThrowIf` makes it unnecessary). The `ThrowIf` helpers run in every build — they are preconditions, not `Debug.Assert` invariants — so the contract holds in release too.

**Worked example:**
```csharp
public Order Place(Order order, int quantity)
{
    ArgumentNullException.ThrowIfNull(order);                        // precondition, throws ArgumentNullException
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);     // names `quantity` automatically
    return order with { Quantity = quantity };
}
```
**Enforcement:** `CA1062` (validate public arguments); review prefers `ThrowIf` over defensive catch.

### 8.6 — Make custom exceptions derive from `Exception`, carry context, and drop `[Serializable]`.

**Reasoning, step by step:**
1. A domain exception exists to be caught by type and to carry the data a handler or a log needs, so give it strongly typed context properties — the `Sku`, the `AccountId`, the offending value — set in the constructor, not concatenated into the message string where they cannot be queried. Derive directly from `Exception` (the framework guidelines retired the three-deep `SystemException`/`ApplicationException` split), seal it, and provide the standard constructors: a message overload and a `(message, innerException)` overload so 8.3's chaining works.
2. Do not mark it `[Serializable]` or implement the `(SerializationInfo, StreamingContext)` constructor: binary serialization of exceptions is obsolete and `BinaryFormatter` is removed from .NET, so that machinery is dead weight that the analyzer no longer even asks for. A custom type is justified only when callers will catch *this* failure distinctly; otherwise a BCL type (8.1) is enough.

**Worked example:**
```csharp
public sealed class InsufficientFundsException : Exception
{
    public AccountId Account { get; }                                       // queryable context, not in the string
    public InsufficientFundsException(AccountId account)
        : base($"insufficient funds for {account}") => Account = account;
    public InsufficientFundsException(string message, Exception innerException) // enables chaining (8.3)
        : base(message, innerException) { }
}
```
**Enforcement:** `CA1032` (provide standard constructors), `CA1064` (exceptions should be public), `CA2229`/`CA2237` no longer required since `[Serializable]` is dropped; review.

### 8.7 — Return a `Result` or use the Try-pattern for expected, routine failures; never throw for control flow.

**Reasoning, step by step:**
1. A failure that is part of normal operation — a key not found, a parse that misses, a validation that rejects user input — is data the signature should admit, not an exception. Exceptions are expensive to throw and invisible in the type, so using them for the expected case hides the failure mode from the caller and pays the unwind cost on a hot path. Return an opt-in `Result<T, TError>` as a closed `record` hierarchy (chapter [06](./06-types-and-data-modeling.md)) the caller must pattern-match, or expose the `Try`-pattern — `bool TryX(out T value)` with `[NotNullWhen(true)]` so the analyzer proves the `out` non-null on success (chapter [03](./03-nullability-and-the-type-system.md)).
2. Choose one dialect per module and hold it: a module that returns `Result` does not also throw for the same class of failure, because a caller cannot defend against both at once. `Result` shines when the error carries structured detail the caller acts on; the `Try`-pattern shines for a plain present/absent answer on a hot path with no allocation. Reserve exceptions for the genuinely unexpected (8.1), and never `throw` to unwind a loop or signal an ordinary branch.

**Worked example:**
```csharp
public abstract record Result<T, TError>                             // the one Result type (06); caller must match
{
    private Result() { }                                             // closed base: only Ok/Err exist (6.3)
    public sealed record Ok(T Value) : Result<T, TError>;
    public sealed record Err(TError Error) : Result<T, TError>;
}
public bool TryParseAmount(string text, [NotNullWhen(true)] out Money? amount) // Try-pattern, proven out (03)
{
    amount = Money.TryFrom(text);
    return amount is not null;
}
```
**Enforcement:** review forbids exceptions for expected failures and forbids mixing `Result` and throwing in one module; `[NotNullWhen]` on every `Try` (chapter [03](./03-nullability-and-the-type-system.md)).

### 8.8 — Treat `OperationCanceledException` as cooperative cancellation, not an error.

**Reasoning, step by step:**
1. When a `CancellationToken` fires, the awaited operation throws `OperationCanceledException` (or its `TaskCanceledException` subtype) — this is the cancellation working as designed, not a failure to log, alarm on, or wrap (8.3). A broad catch that scoops it up and reports an error (the danger 8.2 warns of) turns an orderly shutdown into noise and may even suppress the cancellation the caller requested.
2. Let `OperationCanceledException` propagate so the cancellation flows back to whoever owns the token, or catch it *specifically* only to perform cleanup and then rethrow. Distinguish it from a real fault: if you must catch broadly, exclude it with a filter — `catch (Exception ex) when (ex is not OperationCanceledException)` — so genuine errors are handled while cancellation passes through untouched (chapter [09](./09-concurrency.md)).

**Worked example:**
```csharp
try { await Process(item, cancellationToken); }
catch (OperationCanceledException) { throw; }                         // good — cancellation passes through
catch (Exception ex) when (ex is not OperationCanceledException)      // real faults only
{
    _log.Error(ex, "processing failed");
    throw;
}
```
**Enforcement:** review; `CA1031` filter must exclude `OperationCanceledException` where a broad catch is justified (chapter [09](./09-concurrency.md)).

## Cross-references

- The `ThrowIf` family, guard clauses, and assertion density: [05-methods-and-functions.md](./05-methods-and-functions.md). Closed `record` hierarchies for `Result` and `switch` matching: [06-types-and-data-modeling.md](./06-types-and-data-modeling.md).
- The Try-pattern, `[NotNullWhen]`, and parse-don't-validate boundaries: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). `CancellationToken` propagation, timeouts, and cooperative cancellation: [09-concurrency.md](./09-concurrency.md).
