# 14 — Documentation

Documentation earns its place only when it says something the code cannot. This chapter holds two surfaces to one standard: XML `///` doc that forms the public contract a caller reads against, and `//` comments that record the reasoning a reader cannot recover from the syntax. Both explain *why* — constraints, units, ownership, the edge case the signature hides — and never restate the *what* the compiler already proves.

## What good looks like

```csharp
namespace Dexpace.Pricing;

/// <summary>
/// Computes the line total for <paramref name="quantity"/> units, applying the
/// volume discount tiers in <see cref="DiscountTable"/>. Amounts are in minor
/// units (cents); the result is never negative.
/// </summary>
/// <param name="unitPrice">Price per unit in cents; must be non-negative.</param>
/// <param name="quantity">Unit count; must be positive.</param>
/// <returns>The discounted total in cents, rounded toward zero.</returns>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="quantity"/> is not positive.
/// </exception>
public static long LineTotal(long unitPrice, int quantity)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
    // Round toward zero so a partial-cent discount never overcharges the customer.
    return DiscountTable.Apply(unitPrice * quantity);
}
```

The `<summary>` documents units, sign, and ownership the signature cannot express, not the obvious "computes a total" (14.1, 14.3). `<param>`, `<returns>`, and the caller-facing `<exception>` complete the contract (14.1), and `<see cref="DiscountTable"/>`/`<paramref name="quantity"/>` are compiler-checked so a rename breaks the build (14.7). The lone `//` comment states *why* it rounds toward zero, not *that* it rounds (14.4). The file carries `<GenerateDocumentationFile>true`, so a missing `<summary>` on this public member is a build error (14.6).

## Rules

### 14.1 — Document every public type and member with `<summary>`, plus the params, returns, and exceptions a caller must handle.

**Reasoning, step by step:**
1. The public surface is a contract, and a caller reads the doc instead of the body — often through IntelliSense, never having opened the source. Every public (and protected, on the one closed-hierarchy base of chapter [06](./06-types-and-data-modeling.md)) type and member therefore carries a `<summary>` stating what it is for, and the structured tags that complete the contract: `<param>` for each parameter, `<returns>` for a non-void result, `<typeparam>` for each generic parameter.
2. Document the `<exception>`s a caller is expected to catch or avoid — the `ArgumentOutOfRangeException` a precondition throws (chapter [05](./05-methods-and-functions.md)), the domain exception a failure raises (chapter [08](./08-error-handling.md)) — because an exception is part of the signature even though the type system does not list it. Omit the ones a caller can do nothing about (`OutOfMemoryException`); list the ones that shape correct calling code.

**Worked example:**
```csharp
/// <summary>Resolves the tenant for <paramref name="host"/>, or <c>null</c> if none is registered.</summary>
/// <param name="host">The request host, lowercased; never empty.</param>
/// <typeparam name="TKey">The tenant key type; must be comparable.</typeparam>
/// <returns>The matching tenant, or <c>null</c> when the host is unknown.</returns>
public Tenant<TKey>? Resolve<TKey>(string host) where TKey : IComparable<TKey> { /* ... */ }
```
**Enforcement:** CS1591 (missing XML comment on public member) promoted to error for library assemblies (14.6); review of the param/returns/exception set.

### 14.2 — Document the contract once with `<inheritdoc/>` on overrides and interface implementations.

**Reasoning, step by step:**
1. An interface method and the class that implements it share one contract, so writing the doc twice means maintaining it twice and watching the two copies drift. Put the prose on the interface — the role-named contract of chapter [10](./10-api-design.md) — and let the implementation inherit it with `<inheritdoc/>`, so the single authoritative description flows to every implementer and override automatically.
2. Reach past a bare `<inheritdoc/>` only to *add*, never to restate: a `<remarks>` noting this implementation's specific behaviour, or a cref to the source via `<inheritdoc cref="..."/>` when the tool cannot infer it. If an override changes the contract enough to need a different `<summary>`, that is a design smell — an override should honour the base contract (chapter [06](./06-types-and-data-modeling.md)), not redefine it.

**Worked example:**
```csharp
public interface WorkerQueue
{
    /// <summary>Enqueues <paramref name="job"/>; throws if the queue is at capacity.</summary>
    void Enqueue(Job job);
}

public sealed class BoundedQueue : WorkerQueue
{
    /// <inheritdoc/>
    public void Enqueue(Job job) { /* contract documented once, on the interface */ }
}
```
**Enforcement:** review requires `<inheritdoc/>` on implementations and overrides; CS1591 is satisfied by the inherited doc.

### 14.3 — Never restate the type or the obvious; document what the signature cannot say.

**Reasoning, step by step:**
1. A `<summary>` of "Gets the name" on `string Name` adds a line to maintain and zero information to read — it paraphrases the identifier the reader already sees, and CS1591 has been satisfied with noise. Doc that restates the signature is worse than none, because it lulls the reader into trusting prose that the next refactor will leave stale.
2. Spend the summary on what the type *cannot* express: the unit (`milliseconds`, `cents`), the range or invariant (`never negative`, `lowercased`), the ownership (`the caller disposes the returned stream`, chapter [13](./13-resource-management.md)), the threading guarantee, and the edge cases at the boundaries. If you cannot say something the signature does not, write a one-line summary that adds the *purpose* — why this member exists — or improve the name and move on.

**Worked example:**
```csharp
/// <summary>Gets the name.</summary>                       // bad — restates the signature
public string Name { get; init; }

/// <summary>The display name, trimmed and never empty; falls back to the email local-part.</summary>
public string Name { get; init; }                          // good — states the invariant the type can't
```
**Enforcement:** review rejects signature-restating doc; favour fewer, higher-value comments over coverage for its own sake.

### 14.4 — Write `//` comments that explain why, not what; capitalize, punctuate, and prefer `//` to block comments.

**Reasoning, step by step:**
1. The code already says *what* it does, line by line; a `// increment the counter` above `count++` is dead weight that the next edit will contradict. A comment earns its place by recording *why* — the reason this line exists, the non-obvious constraint it satisfies, the bug it works around, the algorithm choice the call site cannot reveal (root rule 7). If you cannot state a why, the comment should not ship and perhaps the line should not either.
2. Follow the Learn convention for mechanics: begin the comment with a capital letter, end it with a period, and put it on its own line above the code it explains rather than trailing it. Use `//` for everything, including multi-line blocks; avoid `/* */`, which does not nest, swallows code when a closing marker is forgotten, and reads as a relic. The comment is prose — write it as a sentence.

**Worked example:**
```csharp
buffer = buffer[..^1];                                     // bad — no comment, or one that restates the slice

// Strip the trailing newline the upstream API always appends; downstream parsers reject it.
buffer = buffer[..^1];                                     // good — records the why
```
**Enforcement:** review of comment content and form; `/* */` flagged in review; comments restating mechanics are deleted.

### 14.5 — Put a worked `<example>` with `<code>` on non-obvious public API.

**Reasoning, step by step:**
1. Some members are correct only when called in a particular shape — a builder assembled in order, a `Result` matched rather than dereferenced (chapter [08](./08-error-handling.md)), a token threaded from a `CancellationTokenSource` (chapter [09](./09-concurrency.md)). For these the fastest path to correct usage is a worked `<example>` containing a `<code>` block the reader can copy, which shows the intended call in one glance and pre-empts the wrong one.
2. Reserve the example for API where usage is non-obvious; a trivial getter does not need one, and an example on everything is the same noise as restated summaries (14.3). The code inside `<example>` is real code held to this guide — it compiles in spirit, obeys the sibling chapters, and is the canonical demonstration, so keep it short and correct.

**Worked example:**
```csharp
/// <summary>Builds an immutable request; call <see cref="WithHeader"/> before <see cref="Build"/>.</summary>
/// <example>
/// <code>
/// var request = RequestBuilder.For(url)
///     .WithHeader("Accept", "application/json")
///     .Build();
/// </code>
/// </example>
public RequestBuilder WithHeader(string name, string value) { /* ... */ }
```
**Enforcement:** review requires an `<example>` on non-obvious public API; the example must obey the guide's own rules.

### 14.6 — Turn on `<GenerateDocumentationFile>` and treat CS1591 as an error for published libraries.

**Reasoning, step by step:**
1. Documentation only stays complete if its absence breaks the build. Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in a published library's project (or `Directory.Build.props`, chapter [01](./01-formatting-and-tooling.md)), which makes the compiler emit the XML doc file and raise CS1591 for every public member that lacks a `<summary>`. With warnings-as-errors on (chapter [01](./01-formatting-and-tooling.md)), CS1591 becomes a build failure, so an undocumented public surface cannot ship.
2. Scope the gate to assemblies whose surface is consumed by others — published NuGet packages, shared libraries. An application's leaf assembly with no public API gains nothing from CS1591 and need not carry the flag; the rule follows the contract, not the file. The generated XML also feeds IntelliSense and doc-site tooling, so the same switch that enforces coverage delivers the doc to consumers.

**Worked example:**
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>   <!-- emits XML, turns on CS1591 -->
  <!-- TreatWarningsAsErrors is set in Directory.Build.props (chapter 01), so CS1591 fails the build -->
</PropertyGroup>
```
**Enforcement:** `<GenerateDocumentationFile>` set for library projects; CS1591 promoted to error by `<TreatWarningsAsErrors>` (chapter [01](./01-formatting-and-tooling.md)).

### 14.7 — Link with compiler-checked `<see cref="..."/>` and `<paramref/>`, never bare prose names.

**Reasoning, step by step:**
1. A doc comment that names another member in plain prose — "see the Resolve method" — is a string the compiler never inspects, so when `Resolve` is renamed the prose silently lies and the reader is sent to a member that no longer exists. Reference members with `<see cref="Resolve"/>` and parameters with `<paramref name="host"/>` instead; the compiler resolves the cref at build time and raises a warning when it dangles, so a rename breaks the doc loudly rather than rotting it quietly.
2. The same checking turns the doc into navigation — IDEs and doc sites render a cref as a hyperlink to the target — so compiler-checked links cost nothing over prose and buy both correctness and usability. Use `<paramref>` for parameters and type parameters within a member's own doc, and `<see langword="null"/>` / `<c>` for keywords and literals, so every named thing in the doc is either checked or marked as code.

**Worked example:**
```csharp
/// <summary>Like <see cref="TryResolve"/>, but throws instead of returning <see langword="false"/>.</summary>
/// <param name="host">Forwarded to <see cref="TryResolve"/>; see <paramref name="host"/> constraints there.</param>
public Tenant Resolve(string host) { /* cref breaks the build if TryResolve is renamed */ }
```
**Enforcement:** CA1200 (avoid using cref tags with a prefix) and the cref-resolution warning; review rejects bare prose member names.

### 14.8 — Delete commented-out code; a `// TODO` or `// HACK` ships only with a tracked issue.

**Reasoning, step by step:**
1. Commented-out code is debt wearing a comment's clothes: it never runs, never gets refactored with the code around it, and leaves the reader guessing whether it is a clue or a corpse. Version control already remembers every line you delete, so the honest place for old code is the history, not a block of `//` in the working file. Delete it; if you need it back, `git log` has it.
2. A `// TODO` or `// HACK` is a promise, and an untracked promise is technical debt that never gets paid (root rule 12). Each one ships only if it references a tracked issue — `// TODO(DEX-1234): remove once the v2 endpoint lands` — so the work is in a backlog the team can see and close, not buried in a file no one greps. A `// HACK` additionally states what the right fix is, so the next reader inherits the plan, not just the wound.

**Worked example:**
```csharp
// var legacy = OldParse(raw);                             // bad — dead code; git remembers it, delete it
// TODO: fix this later                                   // bad — untracked promise, never paid
// TODO(DEX-1042): replace with FrozenDictionary once the table is static.   // good — tracked, actionable
```
**Enforcement:** IDE0005 (remove unnecessary usings/dead code signals) and review delete commented-out code; review requires an issue reference on every `TODO`/`HACK`.

## Cross-references

- Warnings-as-errors and `Directory.Build.props` carrying `<GenerateDocumentationFile>`: [01-formatting-and-tooling.md](./01-formatting-and-tooling.md). Role-named interfaces, `nameof`, and identifier conventions the doc references: [02-naming-conventions.md](./02-naming-conventions.md).
- Role-named interface contracts documented once and inherited with `<inheritdoc/>`: [10-api-design.md](./10-api-design.md). Precondition exceptions worth documenting: [05-methods-and-functions.md](./05-methods-and-functions.md).
- Domain exceptions a caller must handle: [08-error-handling.md](./08-error-handling.md). Zero technical debt, of which untracked TODOs and dead code are instances: [README.md](./README.md).
