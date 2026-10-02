# methods-and-functions

## Rules
- A method taking three or more parameters takes a single immutable options record instead; two well-named parameters need no such ceremony.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:111-115` · high · sha:cabfc31de871</sub>
- Options records use required members for what the caller must supply and init defaults for the rest, and are the home for validation and defaults.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:115-125` · high · sha:cabfc31de871</sub>
- Never pass a boolean flag that selects behaviour; split into two named methods such as Save(order) and SaveAndNotify(order).
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:129-133` · high · sha:cabfc31de871</sub>
- When a method has more than two modes, an enum replaces the behaviour-selecting bool.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:133-133` · high · sha:cabfc31de871</sub>
- A bool that is genuine data the method stores or returns, such as caseSensitive passed straight to a comparer, is permitted.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:133-140` · high · sha:cabfc31de871</sub>
- Files follow the step-down rule: the public method comes first and the private helpers it calls sit directly below it in call order.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:144-147` · high · sha:cabfc31de871</sub>
- A helper used by exactly one method that captures nothing is a static local function inside that method.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:148-148` · high · sha:cabfc31de871</sub>
- A helper shared by several methods becomes a private method placed below its first caller.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:148-148` · high · sha:cabfc31de871</sub>
- Recursion is forbidden in library code; use bounded iteration instead.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:162-166` · high · sha:cabfc31de871</sub>
- Recursion is rewritten as iteration over an explicit bounded structure such as a capped Stack<T> or Queue<T>, a while loop with a depth counter, or a worklist with a maximum, failing by return or a thrown limit-exceeded exception rather than StackOverflowException.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:166-166` · high · sha:cabfc31de871</sub>
- Every traversal must carry a mandatory depth bound, and review rejects recursion in library assemblies.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:186-186` · high · sha:cabfc31de871</sub>
- A method is capped at 70 lines, with a working target of 10 to 30 lines at one level of abstraction.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:35-38` · high · sha:cabfc31de871</sub>
- A method either orchestrates named steps or performs one step, never both; mixed high-level flow and low-level detail is fixed by extracting a named helper rather than compressing under the line cap.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:39-39` · high · sha:cabfc31de871</sub>
- Review rejects methods that mix abstraction levels.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:51-51` · high · sha:cabfc31de871</sub>
- Guard clauses go first in a method, validating inputs and rejecting the impossible by early return or throw, so the happy path stays flush left without an else or nested if.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:53-56` · high · sha:cabfc31de871</sub>
- Nest only when logic is genuinely conditional, aim for two levels of nesting and stop at three.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:57-57` · high · sha:cabfc31de871</sub>
- Methods assert aggressively, averaging at least two assertions per method, typically a precondition at entry and a postcondition at exit.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:72-75` · high · sha:cabfc31de871</sub>
- Public preconditions use ArgumentNullException.ThrowIfNull and the ArgumentException/ArgumentOutOfRangeException.ThrowIf* family, which run in every build.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:75-75` · high · sha:cabfc31de871</sub>
- Internal invariants that should be impossible use Debug.Assert, which runs in debug builds and documents the assumption.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:75-75` · high · sha:cabfc31de871</sub>
- Compound assertions are split so a failure names the exact clause, using Debug.Assert(a) then Debug.Assert(b) rather than Debug.Assert(a && b).
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:76-76` · high · sha:cabfc31de871</sub>
- Assertions cover both positive space (the expected value is present) and negative space (the forbidden value is absent).
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:76-76` · high · sha:cabfc31de871</sub>
- An expression body (=>) is used only for a genuine one-liner whose block form would contain exactly one return or one statement; any body needing a guard clause, local, assertion or two statements uses a block.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:92-96` · high · sha:cabfc31de871</sub>
- A guard must not be smuggled into a ternary inside an expression body; use a block to host the guard.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:95-107` · medium · sha:cabfc31de871</sub>
- Nesting aims for two levels and three at most.
  <sub>styleguide · `docs/styleguide/csharp/README.md:67-67` · high · sha:1e6ba36fc337</sub>
- Functions aim for 10-30 lines at one level of abstraction each, with guard clauses first so the happy path stays flush left and blank lines separating logical sections.
  <sub>styleguide · `docs/styleguide/csharp/README.md:68-68` · high · sha:1e6ba36fc337</sub>

## Constraints
- C# does not guarantee tail-call elimination, so recursion depth equals call-stack depth and deep or hostile input overflows the stack and crashes the process unrecoverably.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:165-165` · high · sha:cabfc31de871</sub>
- The 70-line method ceiling is enforced by analyzer MA0051 configured to 70 lines and promoted to an error.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:51-51` · high · sha:cabfc31de871</sub>
- Methods cap at 70 lines, analyzer-enforced.
  <sub>styleguide · `docs/styleguide/csharp/README.md:67-67` · high · sha:1e6ba36fc337</sub>

## Conclusions
- Options records are preferred over positional parameter lists because they name each argument at the call site, prevent silently transposed arguments, allow object-initializer or with syntax, and make adding a field a non-breaking change.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:114-114` · high · sha:cabfc31de871</sub>
- The static modifier on local functions is required because it forbids accidental capture of enclosing locals, which would allocate a closure and create a hidden dependency on mutable state.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:148-148` · high · sha:cabfc31de871</sub>
- The 70-line ceiling was deliberately set at the Go guide's level, and is a ceiling rather than a target.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:38-38` · high · sha:cabfc31de871</sub>
- Early return is not treated as a violation of single-exit dogma; it is preferred over arrow-shaped nesting.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:57-57` · high · sha:cabfc31de871</sub>
- The 70-line method cap has no upstream basis; it is an owner decision following Tiger Style discipline, deliberately set at Go's level.
  <sub>styleguide · `docs/styleguide/csharp/README.md:81-81` · high · sha:1e6ba36fc337</sub>

## Reference
- Expression-body style is enforced by IDE0022 and IDE0025 set to when_on_single_line, plus review.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:109-109` · high · sha:cabfc31de871</sub>
- The options-record threshold is enforced by review at three or more parameters, with the public-API analyzer flagging churn from positional growth.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:127-127` · high · sha:cabfc31de871</sub>
- The boolean-flag ban is enforced by CA1026-style review, where the reviewer rejects a behaviour-selecting bool and suggests a split or an enum.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:142-142` · high · sha:cabfc31de871</sub>
- Step-down and static local functions are enforced by IDE0062 (make local function static) and review of ordering.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:160-160` · high · sha:cabfc31de871</sub>
- The chapter cross-references chapter 01 for the 70-line cap and warnings-as-errors, 02 for nameof and method naming, 03 for ThrowIfNull and the Try-pattern, 06 for options records, and 08 for exception types and the Result alternative.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:188-192` · high · sha:cabfc31de871</sub>
- Guard-first layout is enforced by review, with CA1502 (avoid excessive complexity) and the 70-line cap pushing back on deep nesting.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:70-70` · high · sha:cabfc31de871</sub>
- Assertion practice is enforced by CA1062 (validate arguments of public methods), Debug.Assert for invariants, and review of assertion density and split compounds.
  <sub>styleguide · `docs/styleguide/csharp/05-methods-and-functions.md:90-90` · high · sha:cabfc31de871</sub>
- Chapter 05 covers the 70-line cap, expression-bodied one-liners, guard clauses with ArgumentNullException.ThrowIfNull, the step-down rule, an options record for three or more parameters, two or more assertions, and local functions.
  <sub>styleguide · `docs/styleguide/csharp/README.md:37-37` · medium · sha:1e6ba36fc337</sub>

## Conflicts
- **CA1062 enforcement of public preconditions (5.3) vs CA1062 dialled to none (design §9.4)** — The styleguide names CA1062 as the enforcement of asserting public preconditions with ThrowIfNull, but the SDK dials CA1062 down to none in .editorconfig because nullable reference types make a non-nullable parameter a compile-time contract and ThrowIfNull still guards the public entry points by hand; the port KEEPS this departure, with its rationale, and records it in a note.
  <sub>styleguide `docs/styleguide/csharp/05-methods-and-functions.md:72-90` · design `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:322-331` · unresolved 2026-10-02</sub>

## Superseded

