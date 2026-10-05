# Phase 4a — Execution Context: Implementation Plan

**Status:** Draft, for review. Written 2026-10-05 against `main` at `0332cef` (2a, 2b, 3a and 3b merged).
Design: [phase 4a execution-context design](../specs/2026-10-05-phase4a-context-design.md), the authority for every
decision below. The plan cites its rows, positions (A–E), facts (1–8) and rulings (`P4a-1`…`P4a-16`) rather than
restating them. Scope authority: the roadmap's Phase 4 card and Phase List row 4. Format precedent: the
[3a plan](../../work/mvp/phase3/phase3a/2026-10-02-phase3a-io.md) and the
[3b plan](../../work/mvp/phase3/phase3b/2026-10-02-phase3b-bodies.md).

**What this document is.** The roadmap's step 3 for sub-phase 4a: numbered TDD tasks in the design's landing order (five
pull-request-sized steps), each with its failing tests, production change, `PublicAPI.Unshipped.txt` diff,
`CHANGELOG.md` entry, requirement IDs and verification commands. It is not the checklist (step 4, written from what was
built in task 5.2; the [Checklist](#checklist) section below is the planned shape, one row per ID) and it writes no
production code.

**Scope.** 20 rows: `CTX-1`–`CTX-20`, all ✅ (design, "Census"). 4a + 4b (34) + 4c (40) = 94. `OBS-25`/`OBS-26`
(phase 5c), `REDIR-11`/`REDIR-24`/`REDIR-25` (6b), `SEAM-28` (2b) and `XCUT-14` (phase 10) are **not** 4a rows and get no
checklist row here; the notes column of the relevant rows mentions them.

**Hand-off assumed from and to 4b/4c.** Nothing in 4a needs anything from 4b or 4c. 4c consumes `DispatchContext`,
`RequestContext`, `ExchangeContext`, `CallContext`, `CallKey`, `InstrumentationContext` (design, "The interface 4a hands to
4c"), so **4c's wiring waits for PR 3**. 4a changes no pipeline, policy, response or error-mapping file; if a step seems to
require it, stop and re-read design "Migration plan". The three sub-phase plans each edit `CLAUDE.md`'s "genuinely unbuilt"
paragraph and `CHANGELOG.md`; re-read both immediately before editing (task 5.3).

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type); make the production change; run them green; then run the wider
   gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is proven able to fail
   by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off; `System.Diagnostics`, `System.Collections.Concurrent`
   and every other namespace are imported in the file that uses them).
3. **`src/` code**: `ConfigureAwait(false)` on every `await` (4a has none), methods at most 70 lines (`MA0051`), `///` XML
   docs on every public member (CS1591), no new `PackageReference` (constraint 2: `ConcurrentDictionary`,
   `System.Diagnostics.Activity` are in the shared framework). No reflection, so `IsAotCompatible`/`IsTrimmable` hold.
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it); `AotSmoke` for the smoke check.
   4a adds **no** `Security` class and edits none. Context tests live under `tests/Dexpace.Sdk.Core.Tests/Execution/`
   (namespace `Dexpace.Sdk.Core.Tests.Execution`), `BoundedMap` tests under `…/Internal/` (namespace `…Tests.Internal`),
   architecture tests under `…/Architecture/`. `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only (SEAM-2).
   Tests that start an `ActivityListener` carry `[Collection("Instrumentation")]` (as `InstrumentationPolicyTests` does) and
   use `Dexpace.Sdk.TestSupport.Diagnostics.ActivityRecorder("Dexpace.Sdk")`.
5. **Security tests are never deleted or loosened, and 4a edits none.** S6 (`PIPE-16`, the re-drive restore) and S8
   (`HTTP-52`, `EnsureSuccessErrorMappingTests`) stay green because no pipeline, policy, response or error-mapping path is
   touched. Each PR's close-out runs
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
   and expects empty output. Task 5.4 runs the `Security` filter on both test projects and records the result.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so every new public member is a line in `Unshipped`.
   The build's `RS0016`/`RS0017` output is the authority (apply the analyzer's code fix and compare). Only **PRs 2 and 3**
   add lines; PRs 1, 4 and 5 leave the file unchanged, and their last task proves it with
   `git diff --stat main...HEAD -- 'src/*/PublicAPI.Unshipped.txt'` (expect empty; the plain `git diff` compares against the
   index and proves nothing once earlier tasks are committed). The two adapter projects' API files never change in 4a.
7. **No breaking change.** 4a is additive (design, "Migration plan"); no `<b>Breaking:</b>` marker, no **Breaking:**
   changelog line.
8. **Namespace and name hazards.** (a) `Dexpace.Sdk.Core.Http.Request` is both a namespace and a type name. In
   `Dexpace.Sdk.Core.Execution` (not nested under `Http`) the simple name `Request` binds to the type through `using
   Dexpace.Sdk.Core.Http.Request;`, and a property `Request` of type `Request` is legal ("Color Color"). If CS0118 or
   CS0119 appears in a test namespace, add `using Request = Dexpace.Sdk.Core.Http.Request.Request;` rather than qualifying.
   (b) The `Dexpace.Sdk.Core.Execution` namespace shares nothing with `System.Threading.ExecutionContext`; the plan checks
   `grep -rn "namespace .*Execution" src tests` before creating it and expects no hit.
9. **One PR per step, code and tests together**; every commit inside a PR is green too. Commit style `feat:` (PRs 1–4),
   `docs:` (PR 5), `test:` for tests-only commits; no AI attribution. **This plan authorises no push and no `gh` call**;
   the lead pushes.

### Environment

Use the pinned SDK (`global.json`, 10.0.401). The host's system `dotnet` has no SDK (design, phase-start queries); export
`DOTNET_ROOT=$HOME/.dotnet` and `PATH=$HOME/.dotnet:$PATH` for the session, and run `scripts/knowledge` with
`DOTNET=~/.dotnet/dotnet`.

### Verification blocks

**V-fast** (inner loop, one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-gate** (the last task of every PR; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # expect empty
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`,
then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and
`scripts/ci/coverage-gate-selftest.sh artifacts/test-results`) runs before the push of PR 3 and of PR 5. No
`PackageReference` changes, so no `packages.lock.json` change is expected; if a locked restore complains, run `dotnet
restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info CTX
scripts/knowledge --gaps CTX                # none expected; appendix C's text for CTX-4, CTX-8, CTX-13 is the extra reading
scripts/knowledge --req CTX-9               # per row in scope of the PR
```

---

## Plan-level readings (where the design was ambiguous)

The design decisions are not changed. Where it left a point open the plan took the reading most consistent with it:

- **R1 — The one internal abstract member is `AppendDetail`.** The design closes the hierarchy with "an `internal abstract`
  member" (P4a-5) and seals `ToString` (P4a-14) without naming either member's body. The plan makes the closing member
  `internal abstract void AppendDetail(StringBuilder builder)`: each sealed record appends its request, operation name and
  status, so `CallContext.ToString` is `"{StageName}[{Key}] {detail}"` and the abstract member has a purpose beyond the
  closing trick. A second `internal abstract string StageName { get; }` renders the flavour.
- **R2 — `Store` is `private protected`.** The design says the base holds `private readonly ContextStore _store`; promotions
  in the derived records need it. The base keeps the readonly field and exposes `private protected ContextStore Store =>
  _store;`. The field stays `readonly` (task 4.1's check) and no public surface changes.
- **R3 — An internal `EntryCount` on `BoundedMap`.** `CTX-11`'s test "the tracked count matches the dictionary after
  quiescence" needs the dictionary's own count, which the public-facing `Count` deliberately is not. `BoundedMap` gains
  `internal int EntryCount => _map.Count` (diagnostic: `ConcurrentDictionary.Count` takes every lock; the remarks say so and
  say production code never calls it).
- **R4 — `StartActivity` validates before the `None` shortcut.** `InstrumentationContext.StartActivity` throws
  `ArgumentException` for a null/blank name on every bundle, `None` included; the `None` shortcut then returns `null`
  regardless of listeners (`CTX-15`). A name error is a programming error and must not depend on the bundle.
- **R5 — The `AsyncLocal` reflection test covers Core in `Dexpace.Sdk.Core.Tests`; the adapters get a two-line mirror.**
  `Dexpace.Sdk.Core.Tests` may not reference a transport (SEAM-2), so P4a-11's "every `src/` assembly" is met by the
  `RS0030` ban at compile time for all three libraries, the reflection test over Core in the Core suite, and the same test
  shape in `Dexpace.Sdk.Http.SystemNet.Tests` and `Dexpace.Sdk.Serialization.SystemTextJson.Tests` (task 4.1).
- **R6 — Zero-id probe before relying on hex text.** `CallKey.ToString` renders `TraceId.ToHexString()` and
  `SpanId.ToHexString()`. Whether `default(ActivityTraceId).ToHexString()` returns the 32-zero string or `null` is not among
  the design's eight verified facts; task 2.1 starts with a scratchpad probe and, if the default renders `null`, renders
  `new string('0', 32)` / `new string('0', 16)` for the zero case. `IsValid` compares the ids with `default`; the same probe
  confirms `ActivityTraceId` equality against `default`.
- **R7 — `<Clone>$` is excluded from the "exchange returns no context" check.** The compiler-synthesized `<Clone>$` of
  `ExchangeContext` returns `ExchangeContext`; task 4.1's reflection check skips compiler-generated members (names starting
  `<`) and `Deconstruct`-style special names.

---

## PR 1 — `BoundedMap` and the bans

**Gate: none.** Internal only; no public API. Rows: `CTX-8` (map half), `CTX-9` (map half), `CTX-11`, `CTX-12`, `CTX-13`,
`CTX-19` (map half). 6c's `AUTH-19` store and token cache later build on this map.

### Task 1.1 — `BoundedMap<TKey, TValue>` and its `Slot` (`CTX-8`, `CTX-9`, `CTX-11`, `CTX-12`, `CTX-13`, `CTX-19`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Internal/BoundedMapTests.cs`, class `BoundedMapTests`, `Unit`
(`BoundedMap<string, object>` or `<int, string>`; reference-type values; internal access through the existing
`InternalsVisibleTo`):

- `A_capacity_below_one_is_rejected` (`CTX-11`; `[Theory]` over 0, −1, `int.MinValue`; `ArgumentOutOfRangeException`,
  `ParamName` `capacity`)
- `An_insert_burst_past_the_capacity_ends_at_or_under_it` (`CTX-11`; capacity 10, 1 000 sequential `Set`s; `Count <= 10`
  after every call, not only at the end — Ruby's R4 "maximum size observed")
- `The_tracked_count_matches_the_dictionary_after_quiescence` (`CTX-11`; interleaved `Set`, `TryRemove`, `TryRemoveIfSame`
  from 8 threads, then `Assert.Equal(map.EntryCount, map.Count)`; R3)
- `One_drain_call_removes_every_excess_entry` (`CTX-12`; deterministic: build a `ConcurrentDictionary<int,
  BoundedMap<int,object>.Slot>` with 100 slots, `ref int count = 100`, call the `internal static` `Drain(map, ref count, 10)`
  once; afterwards `count == 10` and `map.Count == 10`)
- `Concurrent_inserts_from_16_threads_converge_to_the_capacity` (`CTX-12`; `Barrier`, 16 threads × 500 distinct keys,
  capacity 64; after join `Count <= 64` and `Count == EntryCount`)
- `A_capacity_one_map_holds_exactly_one_of_two_inserts` (`CTX-13`; asserts `Count == 1` and the survivor is one of the two
  keys, never which)
- `Set_installs_then_replaces_without_changing_the_count` (`CTX-8`; same key twice, `Count == 1`, `TryGetValue` returns
  the second value by reference)
- `TryAdd_installs_only_if_absent` (`CTX-8`; second `TryAdd` on the key returns `false`, the incumbent stays, `Count`
  unchanged)
- `TryGetValue_of_an_unknown_key_returns_false_and_null` (`CTX-18` support)
- `TryRemove_of_an_unknown_key_returns_false_and_leaves_the_count` (pin)
- `TryRemoveIfSame_compares_by_reference_not_value` (`CTX-9`; two distinct `string` values built with `new string('a', 3)`
  that are `==` but not the same reference: removing with the equal-but-distinct one returns `false` and leaves the
  entry; removing with the stored reference returns `true`; `Count` 0)
- `TryRemoveIfSame_after_a_replacement_is_a_no_op_for_the_replaced_value` (`CTX-9`; `Set(k, a)`, `Set(k, b)`,
  `TryRemoveIfSame(k, a)` is `false`, `b` still resolves)
- `The_slot_type_declares_no_equality_override` (Position C; reflection: `typeof(BoundedMap<,>.Slot)` declares neither
  `Equals(object)` nor `GetHashCode`, so a later "tidy-up" into a record fails here)
- `A_re_set_of_the_same_value_gets_a_new_slot` (fact 2: no ABA; `TryRemoveIfSame` of a stale view cannot remove the
  re-inserted entry — exercise through `Set`, remove, `Set` of the same reference, assert the removal count)

Red: CS0246 (no `BoundedMap`).

**Production.** New `src/Dexpace.Sdk.Core/Internal/BoundedMap.cs`, `internal sealed class BoundedMap<TKey, TValue> where
TKey : notnull where TValue : class`, namespace `Dexpace.Sdk.Core.Internal`, signatures per the design's "`ContextStore`
and `BoundedMap`" shape plus `internal int EntryCount` (R3):

- State: `ConcurrentDictionary<TKey, Slot> _map` (constructed with the optional comparer), `int _count` (touched only
  through `Interlocked`), `int Capacity`. Constructor: `ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1)`.
- `Slot`: `internal sealed class Slot { internal Slot(TValue value); internal TValue Value { get; } }` — no `Equals`, no
  `GetHashCode` override, a comment saying why (facts 1, 2; position C).
- `Set(key, value)`: allocate a fresh `Slot`; loop: `TryAdd` → on success `Interlocked.Increment`, drain, return; else
  `TryGetValue(key, out var current)` and `TryUpdate(key, newSlot, current)` (slot-reference comparand) → on success return
  **without** touching the count; on failure of either (concurrent removal or replacement) retry. The count moves exactly
  once per entry added and once per entry removed (design, "Type shapes").
- `TryAdd(key, value)`: a fresh slot; success increments and drains; failure returns `false`.
- `TryRemove(key)`: `_map.TryRemove(key, out _)`; on success decrement.
- `TryRemoveIfSame(key, value)`: `TryGetValue(key, out slot)`, `ReferenceEquals(slot.Value, value)`, then
  `_map.TryRemove(new KeyValuePair<TKey, Slot>(key, slot))`; on success decrement. The one call to the value-comparing
  overload sits under a **scoped `#pragma warning disable RS0030`** with a why-comment citing design §5.4, P13 and P4a-12
  (the slot has no `Equals`, so `EqualityComparer<Slot>.Default` is reference identity, fact 2).
- `internal static void Drain(ConcurrentDictionary<TKey, Slot> map, ref int count, int capacity)`: walk the live
  enumerator (never `Keys`, which takes every lock, fact 7), `TryRemove(key, out _)` each visited key and decrement
  `count` only on a successful remove, stop when `Volatile.Read(ref count) <= capacity`; re-enter the walk while the count
  is still over after a full pass (concurrent inserters may be ahead). The loop is load-bearing on .NET (`CTX-12`): there is
  no global lock. Victim order is the enumerator's and is documented as arbitrary (`CTX-13`); the just-inserted entry may
  be evicted. The instance calls `Drain(_map, ref _count, Capacity)` after every successful add.
- `XML docs` on the internal members are encouraged but not required (`CS1591` covers public only); the class remarks state
  the single-writer-free design, the arbitrary eviction and that values are held **strongly** (`CTX-19`: no weak table, no
  weak reference; the cap, not the collector, is the backstop).

Each method stays under the 70-line cap; `Set` and `Drain` are the two to watch. **`PublicAPI.Unshipped.txt`:** unchanged.
**IDs:** `CTX-8`, `CTX-9`, `CTX-11`, `CTX-12`, `CTX-13`, `CTX-19` (map halves). **Verify:** V-fast `BoundedMapTests`.

### Task 1.2 — The `AsyncLocal` ban and the `TryRemove` ban message (`CTX-19`, `CTX-9`; `P4a-11`, `P4a-12`)

**Failing test first (a probe, never committed).** Add a temporary, uncommitted file under `src/Dexpace.Sdk.Core/` in the working tree
(or in a git worktree of the repository; a copy outside the repository loses `Directory.Build.props`, so no analyzer or
`BannedSymbols.txt` applies and every probe would be silent), build, record the `RS0030` output, then delete the file. Add, one at a time: `new AsyncLocal<int>()`, `new WeakReference<object>(new
object())`, `new ConditionalWeakTable<object, object>()`, and a stray `ConcurrentDictionary<int,int>.TryRemove(new
KeyValuePair<int,int>(1, 1))` outside `BoundedMap`. Expect `RS0030` for each; record the four results in the checklist
(task 5.2). The `AsyncLocal` probe is expected **red before** the ban line exists (no diagnostic) and green after.
Document-comment IDs are easy to mistype and `RS0030` silently ignores an unmatched entry; an entry that does not fire is a
bug in the entry.

**Production.** In `BannedSymbols.txt`:

1. Add, under the "Context-store and lifetime discipline in core" group:
   `T:System.Threading.AsyncLocal`1;The SDK holds no ambient per-call state; correlate through CallKey and the context chain (design §5.4 P13)`.
2. **Edit** (never delete) the `TryRemove(KeyValuePair{`0,`1})` entry's reason to
   `The value-comparing TryRemove belongs only in BoundedMap's slot (design §5.4, P4a-12)`.
3. Leave the `ConditionalWeakTable`2`, `WeakReference`1` and `WeakReference` lines unchanged (they exist since phase 0).

`grep -rn "AsyncLocal" src` must show only doc-comment mentions (the design records `HttpClientExtensions.cs`); doc comments
do not trigger `RS0030`. **IDs:** `CTX-19`, `CTX-9`. **Verify:** `dotnet build Dexpace.Sdk.sln --configuration Release`
(the ban's evidence).

### Task 1.3 — Close-out (PR 1)

`CHANGELOG.md` `[Unreleased]` `### Added`: "`RS0030` entry for `AsyncLocal<T>` in the library projects (design §5.4); internal
`BoundedMap`, the SDK's one bounded-map implementation (`CTX-11`, `CTX-12`)." Run **V-gate** (no coverage gate).
`git diff --stat main...HEAD -- 'src/*/PublicAPI.Unshipped.txt'` expects empty. **Commit:** `feat: BoundedMap and the AsyncLocal ban (CTX-8..CTX-13, CTX-19)`.

---

## PR 2 — `CallKey` and `InstrumentationContext`

**Gate: none.** Independent of PR 1. Public API lines. Rows: `CTX-4`, `CTX-6`, `CTX-14`, `CTX-15`, `CTX-20`; (`CTX-2`'s
"same reference" half rides the sealed class built here).

### Task 2.1 — `CallKey` (`CTX-4`, `CTX-6`, `CTX-15`; `P4a-2`)

**Probe first (R6, not committed).** A scratch program: `default(ActivityTraceId).ToHexString()` and
`default(ActivitySpanId).ToHexString()`; `default(ActivityTraceId) == default(ActivityTraceId)`; `default(ActivityContext)`'s
ids (fact 3). Record the results in the task's commit message; the production code adapts to them.

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Execution/CallKeyTests.cs`, class `CallKeyTests`, `Unit`:

- `Two_keys_minted_from_identical_trace_and_span_differ` (`CTX-4`; one `ActivityContext`, two `Next(in ctx)`; unequal,
  `Sequence` differs by at least 1)
- `Keys_minted_under_None_are_distinct` (`CTX-15`/`CTX-4`; two `CallKey.Next()` zero-id keys differ)
- `A_minted_key_has_a_positive_sequence_and_default_has_zero` (`CTX-5` support; `default(CallKey).Sequence == 0`)
- `Keys_minted_concurrently_across_threads_are_pairwise_distinct` (`CTX-6`; `Barrier`, 16 threads × 10 000, a
  `HashSet<long>` of `Sequence` over the merged results has 160 000 members)
- `ToString_renders_trace_span_and_sequence_with_colons` (`CTX-4`; `"{32 hex}:{16 hex}:{n}"`, exact shape through a
  regex; the zero key renders 32 and 16 zeros)
- `A_key_is_a_value_type_with_field_wise_equality` (`CTX-4`; a copy of a key is equal, hashes equal; two minted keys are
  not)
- `CallKey_declares_no_public_constructor` (`P4a-2`; reflection: `typeof(CallKey).GetConstructors(Public | Instance)` empty
  apart from the implicit parameterless struct constructor — assert there is no public constructor with parameters)

Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Execution/CallKey.cs`, namespace `Dexpace.Sdk.Core.Execution`, `public readonly
record struct CallKey` per the design's shape: private static `long s_sequence`, a private constructor
`(ActivityTraceId, ActivitySpanId, long)`, get-only `TraceId`, `SpanId`, `Sequence`; `Next()` (zero ids) and `Next(in
ActivityContext context)` both `Interlocked.Increment(ref s_sequence)`; `ToString()` the only place the text is built.
`///` docs on every public member; the remarks state that `default(CallKey)` is the never-minted sentinel (`Sequence` 0) and
that a context constructor rejects it.

**`PublicAPI.Unshipped.txt` (core):** lines for `CallKey` (the three properties, both `Next` overloads, `ToString`, and the
record-struct synthesized members). Take them from the analyzer. **IDs:** `CTX-4`, `CTX-6`, `CTX-15`. **Verify:** V-fast
`CallKeyTests`.

### Task 2.2 — `InstrumentationContext` (`CTX-14`, `CTX-15`, `CTX-20`; `P4a-3`, `P4a-16`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Execution/InstrumentationContextTests.cs`, class
`InstrumentationContextTests`, `Unit`, `[Collection("Instrumentation")]`:

- `FromActivity_exposes_the_activitys_identifiers_flags_state_and_span` (`CTX-14`; under an `ActivityRecorder("Dexpace.Sdk")`,
  start an activity on `DexpaceDiagnostics.ActivitySource`, build the bundle; `TraceId`, `SpanId`, `TraceFlags`,
  `ActivityContext`, `IsValid` true, `IsRemote` false, `ActiveSpan` is the same `Activity` reference, `TraceIdFormat`
  `W3C`)
- `FromActivity_with_no_trace_state_reports_the_empty_string_not_null` (fact 3, `CTX-15`/`OBS-26`'s empty state)
- `FromActivity_keeps_a_set_trace_state_verbatim` (an activity with `TraceStateString = "a=b"`)
- `FromContext_of_a_remote_parent_is_remote_and_has_no_active_span` (`CTX-14`; an `ActivityContext(traceId, spanId,
  Recorded, "k=v", isRemote: true)`; `IsRemote` true, `ActiveSpan` null, `TraceState` `"k=v"`)
- `FromContext_of_a_started_context_reports_its_trace_flags`
- `The_tracer_factory_starts_a_child_of_the_bundle_under_a_listener` (`CTX-14`, `P4a-16`; the started activity's
  `TraceId` equals the bundle's and `ParentSpanId` equals the bundle's `SpanId`, even when `Activity.Current` is a different
  activity at the call site)
- `A_legacy_hierarchical_activity_reports_the_hierarchical_format_and_is_not_valid` (Risks: `Activity.DefaultIdFormat`
  forced to `Hierarchical` inside a try/finally restoring it; `TraceIdFormat` `Hierarchical`, `IsValid` false; the test
  restores the default so it cannot leak into other classes — it is in the `Instrumentation` collection)
- `None_reserves_the_invalid_sentinels` (`CTX-15`; zero ids, `ActivityTraceFlags.None`, `TraceState == ""`, `IsValid` and
  `IsRemote` false, `ActiveSpan` null, `TraceIdFormat` `Unknown`)
- `None_never_starts_an_activity_even_with_a_listener` (`CTX-15`; a listener is attached, `None.StartActivity(...)` returns
  `null`, the recorder saw nothing)
- `FromActivity_null_and_FromContext_default_return_None_itself` (`CTX-15`; `Assert.Same`)
- `A_default_ActivityContext_keeps_the_key_call_unique_under_None` (`CTX-15`/`CTX-4`; two `CallKey.Next(None.ActivityContext)`
  differ)
- `StartActivity_with_a_blank_name_throws_on_every_bundle_including_None` (R4; null, empty, whitespace)
- `With_no_listener_the_factory_returns_null` (`CTX-20`, fact 4; run with no `ActivityRecorder` live — the test sits in the
  `Instrumentation` collection so no other class's listener is attached; asserts `ActivitySource.HasListeners()` false first
  and skips the assertion body, via an early-return guard that **fails** rather than silently passes if a listener is
  present, so the premise is checked, not assumed)
- `The_tracer_factory_is_safe_under_concurrent_invocation` (`CTX-20`; `Barrier`, 16 threads × 50 starts under the
  `ActivityRecorder`; every returned activity is distinct, non-null, stopped by the caller; recorder counts match)
- `The_class_is_sealed_and_has_no_public_constructor` (`CTX-14`/`P4a-3`: built only through the two factories)

Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Execution/InstrumentationContext.cs`, `public sealed class InstrumentationContext`
per the design's shape: private constructor over `(ActivityContext, Activity?, ActivityIdFormat)`; static `None`;
`FromActivity(Activity? activity)` (`null` → `None`; else `activity.Context`, `activity`, `activity.IdFormat`, trace state
`activity.TraceStateString ?? ""`); `FromContext(in ActivityContext context)` (`default` → `None`; else the context, `null`
span, `TraceIdFormat` `W3C` because an `ActivityContext` is always W3C-shaped, trace state `context.TraceState ?? ""`); the
derived `TraceId`, `SpanId`, `TraceFlags`, `TraceState`, `IsValid` (ids both non-zero, R6), `IsRemote`; `StartActivity(string
operationName, ActivityKind kind = ActivityKind.Internal)`: validate the name (R4), return `null` when `this` is `None`,
else `DexpaceDiagnostics.ActivitySource.StartActivity(operationName, kind, ActivityContext)` (explicit parent, `P4a-16`).
No `Equals` override. The remarks state the span-model residuals (design §10 entries 23 and 24), that no Datadog flavour
exists, that `TraceIdFormat` is `Hierarchical` for a legacy activity, and that adding a member after 4a is a dated design
correction, never a silent change (Position A, "What 5c may and may not do").

**`PublicAPI.Unshipped.txt` (core):** the `InstrumentationContext` lines (properties, `None`, both factories,
`StartActivity`). **IDs:** `CTX-14`, `CTX-15`, `CTX-20`; shape for `OBS-25`/`OBS-26`. **Verify:** V-fast
`InstrumentationContextTests`.

### Task 2.3 — Close-out (PR 2)

`CHANGELOG.md` `### Added`: "`CallKey` and `InstrumentationContext` in `Dexpace.Sdk.Core.Execution`: the call key and the
correlation bundle (`CTX-4`, `CTX-6`, `CTX-14`, `CTX-15`, `CTX-20`)." Run **V-gate**. **Commit:** `feat: CallKey and the
InstrumentationContext correlation bundle (CTX-4, CTX-6, CTX-14, CTX-15, CTX-20)`.

---

## PR 3 — The context records, the store, and `DexpaceCallContexts`

**Gate: PRs 1 and 2.** Public API lines. **4c's wiring waits for this PR.** Rows: `CTX-1`–`CTX-3`, `CTX-5`, `CTX-7`,
`CTX-8`–`CTX-10`, `CTX-16`–`CTX-18`.

### Task 3.1 — `ContextStore` (`CTX-3`, `CTX-8`, `CTX-9`, `CTX-18`; `P4a-9`, `P4a-10`)

`ContextStore` references `CallContext`, which task 3.2 creates, so this task's tests are written first but compile only
after 3.2's types exist; to keep every commit green, the plan lands 3.1's production and 3.2's types **in one commit** (the
test files may be committed first only as `#if false`-free pins that do not reference `CallContext`). Practically: write
`ContextStoreTests` (red: CS0246), then 3.2's types and 3.1's store together.

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Execution/ContextStoreTests.cs`, class `ContextStoreTests`,
`Unit`; every test builds its own `new ContextStore(capacity)` and binds contexts through `DispatchContext`'s internal
overload, so none depends on `ContextStore.Shared`:

- `Set_overwrites_and_never_throws` (`CTX-8`; two contexts, one key, `Set` twice; the latest resolves by reference)
- `Add_on_an_occupied_key_throws_naming_the_key` (`CTX-8`, `P4a-9`; `ArgumentException`, `ParamName` `context`, message
  contains `key.ToString()`)
- `A_rejected_Add_leaves_the_incumbent` (`CTX-8`)
- `Concurrent_Add_of_one_key_admits_exactly_one_winner` (`CTX-8`; 32 threads behind a `Barrier`; exactly one success, 31
  `ArgumentException`s each naming the key)
- `TryGet_of_an_unknown_key_returns_false_and_null` (`CTX-18`)
- `A_value_equal_stale_context_does_not_evict_the_live_one` (`CTX-9`; the live context and `live with { }` — fact 5 —
  under one pinned key; `Release(clone)` is `false` and the live one still resolves; `Release(live)` is `true` and it is
  gone)
- `Release_of_an_unknown_context_is_a_no_op_returning_false` (`CTX-18`)
- `Release_of_a_replaced_context_is_a_no_op` (`CTX-9`/`CTX-10`)
- `The_default_capacity_is_ten_thousand` (`P4a-10`; `ContextStore.DefaultCapacity == 10_000`)
- `Shared_is_one_instance_with_the_default_capacity`
- `Distinct_keys_register_overwrite_and_release_concurrently_without_loss` (`CTX-7`; 16 threads, each owning its own keys,
  register → overwrite → release, then exact final state: `Count == 0` and `EntryCount == 0`)

**Production.** New `src/Dexpace.Sdk.Core/Execution/ContextStore.cs`, `internal sealed class ContextStore` per the design:
`internal const int DefaultCapacity = 10_000;`, `internal static ContextStore Shared { get; } = new(DefaultCapacity);`
(initialised once; a `static readonly` field behind the property), `ContextStore(int capacity)` over a
`BoundedMap<CallKey, CallContext>`, `Set` → `map.Set(context.Key, context)`, `Add` → `map.TryAdd` else
`throw new ArgumentException($"A context is already registered under key {context.Key}.", nameof(context))`, `TryGet`,
`Release(context)` → `map.TryRemoveIfSame(context.Key, context)`, `Count`/`EntryCount` pass-throughs. The remarks state the
strong-reference holding (`CTX-19`) and the arbitrary eviction (`CTX-13`). **IDs:** `CTX-8`, `CTX-9`, `CTX-18`, `CTX-7`.

### Task 3.2 — `CallContext`, the three records, promotion and registration (`CTX-1`, `CTX-2`, `CTX-3`, `CTX-5`, `CTX-7`, `CTX-10`, `CTX-16`, `CTX-17`; `P4a-4`..`P4a-7`, `P4a-14`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Execution/PromotionChainTests.cs`, class `PromotionChainTests`,
`Unit` (isolated store; a helper builds `new Request(Method.Get, new Uri("https://api.example.test/widgets"))` and `new
Response(request, Status.Ok, Protocol.Http11)` — check the exact `Status`/`Protocol` member names against the code at task
start, 2a's public surface is the authority):

- `Each_stage_exposes_exactly_its_artifacts` (`CTX-1`; reflection over **public instance properties** declared on each
  type chain: Dispatch {`Key`, `Instrumentation`}; Request {+`Request`, `OperationName`}; Exchange {+`Response`})
- `Promoting_dispatch_adds_exactly_the_request_and_carries_key_and_bundle_by_reference` (`CTX-2`; `Assert.Same` on
  `Instrumentation`, `Assert.Equal` on `Key`, `Assert.Same` on the supplied `Request`)
- `Promoting_request_to_exchange_carries_request_and_operation_name_by_reference` (`CTX-2`)
- `The_source_is_unchanged_by_promotion` (`CTX-2`; the dispatch context is `==` its pre-promotion clone)
- `Default_construction_binds_None_and_mints_a_fresh_key` (`CTX-5`/`CTX-15`)
- `Two_default_constructed_contexts_with_identical_fields_are_not_equal` (`CTX-5`; same request, different minted keys)
- `A_pinned_shared_key_makes_them_equal` (`CTX-5`; one `CallKey.Next()` passed to both, bound to one isolated store because
  fact 6 includes the store in equality)
- `The_default_key_is_rejected` (`CTX-5`, `P4a-2`; `default(CallKey)` → `ArgumentException`, `ParamName` `key`, on all three
  constructors)
- `Default_keys_are_distinct_across_all_three_flavours` (`CTX-6`)
- `The_operation_name_is_absent_at_dispatch` (`CTX-16`; `DispatchContext` has no `OperationName`: reflection pin plus the
  promotion supplies it)
- `The_operation_name_is_carried_forward_unchanged` / `…does_not_change_the_key` / `A_blank_operation_name_is_rejected`
  (`CTX-16`; `ArgumentException` for `""` and `"  "`, `null` accepted; same rule as `OperationDescriptor.OperationId`)
- `A_null_request_or_response_is_rejected` (`ArgumentNullException`)
- `ToString_names_the_stage_key_operation_and_status_and_never_prints_headers_or_bodies` (`P4a-14`; an `Authorization`
  header value and a secret query value on the request appear nowhere in the text; a response over a stream body that
  throws if read is not read)
- `The_ToString_is_sealed` (`P4a-14`; reflection: `CallContext.ToString` is `sealed`)

New `tests/Dexpace.Sdk.Core.Tests/Execution/ContextRegistrationTests.cs`, class `ContextRegistrationTests`, `Unit`:

- `A_constructed_dispatch_context_is_not_registered` (`CTX-17`)
- `An_off_chain_request_context_is_not_registered` (`CTX-17`; a directly constructed `RequestContext` and `ExchangeContext`)
- `The_first_promotion_installs_the_first_entry` (`CTX-17`; `TryGet(key)` returns the `RequestContext` by reference)
- `Every_flavour_of_one_chain_occupies_the_same_slot_in_turn` (`CTX-3`; after each promotion `TryGet(key)` returns the
  newest link by reference and `Count == 1`)
- `Two_contexts_with_identical_trace_and_span_both_register` (`CTX-4`; two chains, `Count == 2`)
- `Closing_an_intermediate_link_leaves_the_successor_registered` (`CTX-10`)
- `Closing_the_terminal_link_evicts_the_chain` (`CTX-10`)
- `Closing_an_unpromoted_dispatch_is_a_no_op` (`CTX-17`)
- `Promoting_the_same_dispatch_twice_is_legal_and_the_later_one_takes_the_slot` (design, "The interface 4a hands to 4c",
  item 2; closing the earlier request link is a no-op)
- `A_double_close_is_a_no_op` (`CTX-18`)
- `Closing_does_not_dispose_the_response` (a response over a `DisposeCountingStream` body: dispose count 0 after `Close()`)
- `A_clone_is_value_equal_reference_distinct_and_closing_it_is_a_no_op` (`CTX-9`/fact 5)

Red: CS0246. Also run the closed-hierarchy probe (below) once the types exist.

**Production.** New files under `src/Dexpace.Sdk.Core/Execution/`:

- `CallContext.cs`: `public abstract record CallContext`; `private readonly ContextStore _store;` `private protected
  ContextStore Store => _store;` (R2); `private protected CallContext(InstrumentationContext? instrumentation, CallKey? key,
  ContextStore store)` — `instrumentation ??= InstrumentationContext.None`; `key ??= CallKey.Next(instrumentation.ActivityContext)`;
  reject `default(CallKey)` with `ArgumentException(…, nameof(key))`; `ArgumentNullException.ThrowIfNull(store)`;
  `public CallKey Key { get; }`, `public InstrumentationContext Instrumentation { get; }`; `public void Close() =>
  _store.Release(this);` (never throws; no latch, `P4a-7`); `public sealed override string ToString()` per R1;
  `internal abstract string StageName { get; }` and `internal abstract void AppendDetail(StringBuilder builder)` (closes the
  hierarchy, `P4a-5`). Not `IDisposable` (`P4a-7`; the remarks give the `using var` reason).
- `DispatchContext.cs`: `public sealed record DispatchContext : CallContext` with the public `(InstrumentationContext?
  instrumentation = null, CallKey? key = null)` constructor binding `ContextStore.Shared`, the `internal (…, ContextStore
  store)` overload, and `public RequestContext PromoteToRequest(Request request, string? operationName = null)`: build the
  next link through `RequestContext`'s internal constructor carrying `Instrumentation`, `Key` and `Store`, then
  `Store.Set(next)` (registration, `CTX-17`, `P4a-6`), return it. Constructor registers nothing.
- `RequestContext.cs`: public `(Request request, string? operationName = null, InstrumentationContext? instrumentation = null,
  CallKey? key = null)` and an internal store-binding overload; get-only `Request`, `OperationName`; operation-name
  validation shared with `ExchangeContext` through one `internal static` helper in this file
  (`OperationNames.Validate`) citing `CTX-16`; `public ExchangeContext PromoteToExchange(Response response)` (registers).
- `ExchangeContext.cs`: public `(Request request, Response response, string? operationName = null, InstrumentationContext?
  instrumentation = null, CallKey? key = null)` plus the internal overload; get-only `Request`, `Response`,
  `OperationName`; **no promotion member** (`CTX-1`: absence of a method is the guarantee).

Every file: license header, `///` docs on all public members, `Request`/`Response` references per convention 8a. The
`Close()` remarks state that it does not dispose the `Response`.

**Closed-hierarchy probe (not committed).** In the scratchpad, a throwaway console project referencing the built Core
assembly declares `public sealed record Fourth : CallContext { }`; and `public sealed record Fifth : CallContext { public Fifth(CallContext c) : base(c) { } }`; expect CS0534 only (does not
implement the inherited internal abstract member). No CS0122 fires: the protected synthesized copy constructor stays reachable
from other assemblies, so the internal abstract members, not the `private protected` constructor, close the hierarchy (state
this in the spec's P4a-5 note). Add an architecture test that `CallContext` keeps at least one internal abstract member. Record the error codes in the checklist. If the compiler
accepts it, stop and re-read `P4a-5` before relaxing the claim.

**`PublicAPI.Unshipped.txt` (core):** the `CallContext` lines (`Key`, `Instrumentation`, `Close`, `ToString`, and the
synthesized record members `Equals`, `GetHashCode`, `EqualityContract`, `PrintMembers`, `<Clone>$`, the copy constructor,
`operator ==`/`!=`), plus the three sealed records' constructors, properties and promotions. Take them from the analyzer.
**IDs:** `CTX-1`, `CTX-2`, `CTX-3`, `CTX-5`, `CTX-7`, `CTX-10`, `CTX-16`, `CTX-17`. **Verify:** V-fast
`PromotionChainTests`, `ContextRegistrationTests`, `ContextStoreTests`.

### Task 3.3 — `DexpaceCallContexts` (`CTX-18`; the reader of design §11 item 30)

**Failing test first.** New `tests/Dexpace.Sdk.Core.Tests/Execution/DexpaceCallContextsTests.cs`, class
`DexpaceCallContextsTests`, `Unit`:

- `TryGet_of_an_unknown_key_returns_false_and_null` (`CTX-18`; a freshly minted key never registered)
- `TryGet_resolves_a_promoted_context_registered_in_the_shared_store` (`CTX-18`; mints its own key, builds public
  constructors over the shared store, asserts only on its own key; closes in a `finally`)
- `TryGet_after_Close_returns_false` (`CTX-18`)

Red: CS0246. **Production.** New `src/Dexpace.Sdk.Core/Execution/DexpaceCallContexts.cs`: `public static class
DexpaceCallContexts` with `public static bool TryGet(CallKey key, [NotNullWhen(true)] out CallContext? context)` over
`ContextStore.Shared`, `///` docs stating that the store is bounded (10 000, arbitrary eviction) and that a miss does not
mean the call never existed. **`PublicAPI.Unshipped.txt`:** the `TryGet` line. **IDs:** `CTX-18`. **Verify:** V-fast
`DexpaceCallContextsTests`.

### Task 3.4 — Close-out (PR 3)

`CHANGELOG.md` `### Added`: "The execution-context chain: `DispatchContext`, `RequestContext`, `ExchangeContext`
(`PromoteToRequest`/`PromoteToExchange`, `Close`), the bounded context store and `DexpaceCallContexts.TryGet`
(`CTX-1`–`CTX-3`, `CTX-5`, `CTX-7`–`CTX-10`, `CTX-16`–`CTX-18`)." Run **V-gate** and the coverage gate with its self-test.
**Commit:** `feat: execution-context chain, bounded store and DexpaceCallContexts (CTX-1..CTX-3, CTX-5, CTX-7..CTX-10, CTX-16..CTX-18)`.

---

## PR 4 — Architecture rules, the collection test, and the AOT smoke

**Gate: PR 3.** No public API change. Rows: `CTX-7`, `CTX-19` (closing); `CTX-1`'s architecture tests.

### Task 4.1 — Architecture tests (`CTX-1`, `CTX-7`, `CTX-19`; `P4a-5`, `P4a-11`)

**Failing tests first (pins; each proven able to fail by a temporary break, never committed).** New
`tests/Dexpace.Sdk.Core.Tests/Architecture/ExecutionContextArchitectureTests.cs`, class `ExecutionContextArchitectureTests`,
`Unit`, in the shape of `ModelImmutabilityArchitectureTests`:

- `Every_instance_field_is_readonly` (`CTX-7`; over `CallContext`, the three records and `InstrumentationContext`, declared
  instance fields via `DeclaredOnly | Instance | Public | NonPublic`, each `IsInitOnly`, so the base record's `_store` is
  checked and a plain mutable field fails)
- `No_property_has_a_public_setter` (`CTX-7`; `init` accessors count as non-settable only if absent — the contexts declare
  none)
- `The_hierarchy_has_exactly_three_concrete_flavours` (`CTX-1`; every `CallContext`-assignable type in the Core assembly that
  is not abstract is `DispatchContext`, `RequestContext` or `ExchangeContext`, each `IsSealed`)
- `The_exchange_context_declares_no_method_returning_a_context` (`CTX-1`; declared methods of `ExchangeContext`, excluding
  compiler-generated members whose names start `<` and special-name accessors, R7: none returns a `CallContext`-assignable
  type)
- `Promotion_methods_exist_only_on_dispatch_and_request` (`CTX-1`; `PromoteToRequest` only on `DispatchContext`,
  `PromoteToExchange` only on `RequestContext`)
- `Contexts_are_not_disposable` (`P4a-7`; no `IDisposable`/`IAsyncDisposable` on any of the four)

New `tests/Dexpace.Sdk.Core.Tests/Architecture/NoAmbientStateArchitectureTests.cs`, class `NoAmbientStateArchitectureTests`,
`Unit` (`P4a-11`, R5): `No_field_in_the_core_assembly_is_an_AsyncLocal` (every field of every type, including nested and
compiler-generated, of `typeof(CallKey).Assembly`, whose type, unwrapped through generics, is `AsyncLocal<>` — none; with a
positive control: the check's helper recognises a hand-built `AsyncLocal<int>` field type, so a broken scanner fails). Mirror
the test, same helper copy and class name `NoAmbientStateArchitectureTests`, in
`tests/Dexpace.Sdk.Http.SystemNet.Tests/Architecture/` (create the folder only if absent; namespace per that project's
convention) over `typeof(SystemNetHttpClient).Assembly`, and in `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/Architecture/`
over its library assembly (read each project's existing marker types before writing the mirrors).

**Production:** none. **IDs:** `CTX-1`, `CTX-7`, `CTX-19` (the ban's second layer). **Verify:** V-fast the two classes; the
adapter test projects' own runs.

### Task 4.2 — The collection test and the concurrency tests (`CTX-19`, `CTX-7`)

New `tests/Dexpace.Sdk.Core.Tests/Execution/ContextRetentionTests.cs`, class `ContextRetentionTests`, `Unit`:

- `Registered_contexts_survive_a_full_collection_with_no_other_reference` (`CTX-19`; 1 000 `ExchangeContext`s with undisposed
  responses created in a `[MethodImpl(MethodImplOptions.NoInlining)]` helper into an isolated `ContextStore`, only the keys
  retained; the helper builds each chain through `DispatchContext`'s internal store-binding overload, then `PromoteToRequest`
  and `PromoteToExchange` (direct construction registers nothing, `CTX-17`), and the isolated store's capacity is 1 000 or more
  so the `CTX-11` drain evicts none; three rounds of `GC.Collect()` + `GC.WaitForPendingFinalizers()`; all 1 000 resolve with `TryGet`)
- `A_burst_above_the_capacity_never_exceeds_it_and_never_throws` (`CTX-11`; isolated store of capacity 100, 16 threads × 500
  promotions; `Count <= 100` after join; no exception escaped any thread; Ruby's R4 measurement: record the maximum `Count`
  observed during the run and assert it stayed `<= 100 + threadCount`, the documented transient overshoot bound — if this
  bound proves flaky, assert only the quiescent bound and note it; never loosen the quiescent assertion)

`Distinct_keys_register_overwrite_and_release_concurrently_without_loss` already lives in `ContextStoreTests` (3.1).
**Production:** none. **IDs:** `CTX-19`, `CTX-11`, `CTX-7`. **Verify:** V-fast `ContextRetentionTests`.

### Task 4.3 — NativeAOT smoke (closing evidence for `CTX-1`, `CTX-4`, `CTX-17`, `CTX-18`)

Extend `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`: `RunAllAsync` calls one more check, `CheckExecutionContext()`, after
`CheckPhase3bBodiesAsync`, in the existing style with its `Expect` helper: mint a `CallKey`, build a `DispatchContext` with
`InstrumentationContext.None`, `PromoteToRequest` and `PromoteToExchange` (a small request and response from the existing
smoke helpers), assert `DexpaceCallContexts.TryGet` returns the exchange link by reference, `Close()` it, assert `TryGet`
absent, assert a second `Close()` does not throw, and assert `InstrumentationContext.None.StartActivity("smoke")` is `null`.
Add the new `using Dexpace.Sdk.Core.Execution;`. No reflection; `AotSmoke` gets no `InternalsVisibleTo`. If the publish
emits a trim/AOT warning (a `ConcurrentDictionary<CallKey, …>` over a struct key is the likely one), fix the source, not the
smoke. **Check:** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke &&
./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` prints "aot-smoke: all checks passed".

### Task 4.4 — Close-out (PR 4)

`CHANGELOG.md` `### Added`: "The AOT smoke covers the execution-context chain." Run **V-gate**. `git diff --stat main...HEAD --
'src/*/PublicAPI.Unshipped.txt'` expects empty. **Commit:** `test: architecture rules, retention test and AOT smoke for the execution context`.

---

## PR 5 — Close-out

**Gate: PRs 1–4 merged.** The docs close the sub-phase (roadmap step 7). Rows: all 20 (closing).

### Task 5.1 — User documentation

New `docs/sdk-documentation/execution-context.md` (the directory holds `http.md`, `seams.md`, `io.md`, `bodies.md`). It
opens "As built by phase 4a, written against source on <date>". Content: the three stages and the one-way promotion; the
call key and why a pinned key needs `CallKey.Next`; the correlation bundle and `None`; registration at promotion and `Close()`
semantics (only the terminal link evicts; a stale or value-equal context never evicts the live one); the bounded store
(10 000, arbitrary eviction, strong references: a leaked `Response` pins its context until the cap evicts it, and an undisposed
body pins a connection); `DexpaceCallContexts.TryGet` and that its production reader arrives with 8b; what 4a deliberately
does not ship (the option key and the transport stamp, `P4a-13`). Cite requirement IDs; do not copy design §5.4.
`docs/README.md`'s ownership table gains the row the probe asks for. **Verify:** the probe's `links` check.

### Task 5.2 — The checklist

Write `docs/work/mvp/phase4/phase4a/<date>-phase4a-context-checklist.md` (create the directory; the housekeeping `apply`
step files the design and this plan beside it, task 5.5) from what was built, one row per requirement ID, phase-1 legend,
mirroring the design's decision table: 20 rows, all ✅ if built as planned (any row that ends ⏳ or N/A says why). Include
the "existing assertions changed" table (expected: none), each `Security` class listed as "unedited" with the empty diff
check as evidence, the four `RS0030` probe results from task 1.2, the closed-hierarchy probe error codes from task 3.2, the
zero-id probe results from task 2.1, and the deviation ledger P4a-3, P4a-6, P4a-10, P4a-12, P4a-13 as built. The
`OBS-25`/`OBS-26`/`REDIR-*`/`SEAM-28`/`XCUT-14` cross-references go in the notes column only and are not rows.

### Task 5.3 — Dated corrections, roadmap note, repository docs

Frozen documents change only by dated correction. In `docs/sdk-design-dotnet/` (re-read each file immediately before
writing; 4b and 4c may edit the same files):

- **§5.4**: the bundle is `InstrumentationContext` (`P4a-3`); promotion registers and the context binds its store
  (`P4a-6`); the slot lives in `BoundedMap` (`P4a-12`); the cap is 10 000 (`P4a-10`); the `CallKey` has no public constructor
  (`P4a-2`); the promotion names (`P4a-4`); the `CallKey`'s route to the transport is open and lands with 4c/8b (`P4a-13`);
  `default(ActivityContext).TraceState` is `null`, not empty (fact 3), and `InstrumentationContext` normalises it to `""` for
  `CTX-15`/`OBS-26`; and the **As built** line.
- **§8.1**: "The correlation bundle is `Activity`" names `InstrumentationContext` as the composing type (`P4a-3`).
- **§9.1**: the banned list gains `AsyncLocal<T>` (`P4a-11`) and the `TryRemove` entry names `BoundedMap`'s slot (`P4a-12`).
- **§11 item 30**: the reader's route (`P4a-13`), landing with 8b.
- **§12**: the `CTX` row's notes cite the design.
- **No new §10 entry** (design, "Design corrections owed at close-out"; the `async-redirect-pillar` entry is 4c's).
- **2b checklist** (`docs/work/mvp/phase2/phase2b/2026-10-02-phase2b-seams-checklist.md`), `SEAM-28` row: a dated
  correction re-pointing "✅; ⏳ 4a" to "✅ (carrier, 4a's `PromotionChainTests`); ⏳ 4c (wiring)" (`P4a-15`). Open for the lead
  per the design; 4c's plan must pick the wiring up. Record in the 4a checklist that the lead accepted or has not yet
  accepted the move.

Roadmap: the Phase List row 4's `sdk-design refs` cell gains this design's link (appended, never replacing), and a dated
Phase Status Note for 4a (what landed, the five PRs, the rulings — noting P4a-3, P4a-6, P4a-8, P4a-10, P4a-13 and P4a-15 as
open for the lead until ruled — and the hand-offs: 4c's wiring and the `Response`-closes-its-`ExchangeContext` hook, 5a's
configurable capacity, 5c's population of the bundle and `OBS-25`/`OBS-26`, 6c's `AUTH-19` store over `BoundedMap`, 8b's
`HttpRequestOptionsKey<CallKey>` stamp).

`CLAUDE.md` (re-read the file first): the layout tree gains `Execution/` under `src/Dexpace.Sdk.Core/`
(`# CallKey, InstrumentationContext, the three context records, DexpaceCallContexts`) and `Internal/` mentions `BoundedMap`;
"What is genuinely unbuilt" drops "the execution-context chain" from the phase 4 item while keeping the recovery chain
(4b) and the pipeline rework (4c). `src/Dexpace.Sdk.Core/README.md` gains a short execution-context paragraph.
`docs/architecture.md` is updated if its layering paragraph needs it. If the implementation found anything the knowledge
corpus should hold, record a note under `docs/knowledge/notes/` (never edit `harvested/`).
**Verify:** the probe's `links` and `citations` checks.

### Task 5.4 — Close-out

`CHANGELOG.md` `### Added`: "`docs/sdk-documentation/execution-context.md`." Run **V-gate**, the coverage gate with its
self-test, and the `Security` filter on both test projects, recording the result in the checklist (S6 and S8 stay green).
**Commits:** `docs: phase 4a checklist, user page and dated corrections`.

### Task 5.5 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 4a            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 4a --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix whatever the final probe reports (counts in `CLAUDE.md`/`README.md`, package READMEs, broken links, misattributed IDs).
`apply --write` performs `git mv` only; the plan authorises no commit beyond those named above and **no push**.

---

## Checklist

The planned shape of the 4a checklist (task 5.2 rewrites it from what was built). One row per requirement ID, exactly the
20 the design says 4a owns; no ⏳, no N/A planned. Legend: ✅ built and tested.

| ID | Level | Planned exit | Evidence (tests) | PR / task |
|---|---|---|---|---|
| `CTX-1` | MUST | ✅ | `PromotionChainTests.Each_stage_exposes_exactly_its_artifacts`; `ExecutionContextArchitectureTests.The_hierarchy_has_exactly_three_concrete_flavours`, `…The_exchange_context_declares_no_method_returning_a_context`; closed-hierarchy probe | 3 / 3.2; 4 / 4.1 |
| `CTX-2` | MUST | ✅ | `PromotionChainTests.Promoting_dispatch_adds_exactly_the_request_and_carries_key_and_bundle_by_reference`, `…Promoting_request_to_exchange_carries_request_and_operation_name_by_reference`, `…The_source_is_unchanged_by_promotion` | 3 / 3.2 |
| `CTX-3` | MUST | ✅ | `ContextRegistrationTests.Every_flavour_of_one_chain_occupies_the_same_slot_in_turn` | 3 / 3.2 |
| `CTX-4` | MUST | ✅ | `CallKeyTests.Two_keys_minted_from_identical_trace_and_span_differ`, `…ToString_renders_trace_span_and_sequence_with_colons`; `ContextRegistrationTests.Two_contexts_with_identical_trace_and_span_both_register` | 2 / 2.1; 3 / 3.2 |
| `CTX-5` | MUST | ✅ | `PromotionChainTests.Two_default_constructed_contexts_with_identical_fields_are_not_equal`, `…A_pinned_shared_key_makes_them_equal`, `…The_default_key_is_rejected` | 3 / 3.2 |
| `CTX-6` | MUST | ✅ | `CallKeyTests.Keys_minted_concurrently_across_threads_are_pairwise_distinct`; `PromotionChainTests.Default_keys_are_distinct_across_all_three_flavours` | 2 / 2.1; 3 / 3.2 |
| `CTX-7` | MUST | ✅ | `ExecutionContextArchitectureTests.Every_instance_field_is_readonly`; `ContextStoreTests.Distinct_keys_register_overwrite_and_release_concurrently_without_loss` | 3 / 3.1; 4 / 4.1 |
| `CTX-8` | MUST | ✅ | `ContextStoreTests.Set_overwrites_and_never_throws`, `…Add_on_an_occupied_key_throws_naming_the_key`, `…A_rejected_Add_leaves_the_incumbent`, `…Concurrent_Add_of_one_key_admits_exactly_one_winner`; `BoundedMapTests.Set_installs_then_replaces_without_changing_the_count`, `…TryAdd_installs_only_if_absent` | 1 / 1.1; 3 / 3.1 |
| `CTX-9` | MUST | ✅ | `ContextStoreTests.A_value_equal_stale_context_does_not_evict_the_live_one`; `BoundedMapTests.TryRemoveIfSame_compares_by_reference_not_value`, `…The_slot_type_declares_no_equality_override`; `RS0030` probe | 1 / 1.1, 1.2; 3 / 3.1 |
| `CTX-10` | MUST | ✅ | `ContextRegistrationTests.Closing_an_intermediate_link_leaves_the_successor_registered`, `…Closing_the_terminal_link_evicts_the_chain` | 3 / 3.2 |
| `CTX-11` | MUST | ✅ | `BoundedMapTests.An_insert_burst_past_the_capacity_ends_at_or_under_it`, `…A_capacity_below_one_is_rejected`, `…The_tracked_count_matches_the_dictionary_after_quiescence`; `ContextRetentionTests.A_burst_above_the_capacity_never_exceeds_it_and_never_throws` | 1 / 1.1; 4 / 4.2 |
| `CTX-12` | SHOULD | ✅ | `BoundedMapTests.One_drain_call_removes_every_excess_entry`, `…Concurrent_inserts_from_16_threads_converge_to_the_capacity` | 1 / 1.1 |
| `CTX-13` | MAY | ✅ | `BoundedMapTests.A_capacity_one_map_holds_exactly_one_of_two_inserts` | 1 / 1.1 |
| `CTX-14` | MUST | ✅ | `InstrumentationContextTests.FromActivity_exposes_the_activitys_identifiers_flags_state_and_span`, `…FromContext_of_a_remote_parent_is_remote_and_has_no_active_span`, `…The_tracer_factory_starts_a_child_of_the_bundle_under_a_listener` | 2 / 2.2 |
| `CTX-15` | MUST | ✅ | `InstrumentationContextTests.None_reserves_the_invalid_sentinels`, `…None_never_starts_an_activity_even_with_a_listener`, `…FromActivity_null_and_FromContext_default_return_None_itself`; `CallKeyTests.Keys_minted_under_None_are_distinct` | 2 / 2.1, 2.2 |
| `CTX-16` | SHOULD | ✅ | `PromotionChainTests.The_operation_name_is_absent_at_dispatch`, `…The_operation_name_is_carried_forward_unchanged`, `…does_not_change_the_key`, `…A_blank_operation_name_is_rejected` | 3 / 3.2 |
| `CTX-17` | MUST | ✅ | `ContextRegistrationTests.A_constructed_dispatch_context_is_not_registered`, `…An_off_chain_request_context_is_not_registered`, `…Closing_an_unpromoted_dispatch_is_a_no_op`, `…The_first_promotion_installs_the_first_entry` | 3 / 3.2 |
| `CTX-18` | MUST | ✅ | `ContextStoreTests.TryGet_of_an_unknown_key_returns_false_and_null`; `ContextRegistrationTests.A_double_close_is_a_no_op`; `DexpaceCallContextsTests` | 3 / 3.1, 3.2, 3.3 |
| `CTX-19` | MUST | ✅ | `ContextRetentionTests.Registered_contexts_survive_a_full_collection_with_no_other_reference`; the `RS0030` probes and the build; `NoAmbientStateArchitectureTests` | 1 / 1.1, 1.2; 4 / 4.1, 4.2 |
| `CTX-20` | SHOULD | ✅ | `InstrumentationContextTests.The_tracer_factory_is_safe_under_concurrent_invocation`, `…With_no_listener_the_factory_returns_null` | 2 / 2.2 |

Count: 20 rows, `CTX-1`–`CTX-20`, all mapped.

---

## Traceability: ID → PR → task

| ID | PR | Task(s) | ID | PR | Task(s) |
|---|---|---|---|---|---|
| `CTX-1` | 3, 4 | 3.2, 4.1 | `CTX-11` | 1, 4 | 1.1, 4.2 |
| `CTX-2` | 3 | 3.2 | `CTX-12` | 1 | 1.1 |
| `CTX-3` | 3 | 3.2 | `CTX-13` | 1 | 1.1 |
| `CTX-4` | 2, 3 | 2.1, 3.2 | `CTX-14` | 2 | 2.2 |
| `CTX-5` | 3 | 3.2 | `CTX-15` | 2 | 2.1, 2.2 |
| `CTX-6` | 2, 3 | 2.1, 3.2 | `CTX-16` | 3 | 3.2 |
| `CTX-7` | 3, 4 | 3.1, 4.1 | `CTX-17` | 3 | 3.2 |
| `CTX-8` | 1, 3 | 1.1, 3.1 | `CTX-18` | 3 | 3.1, 3.2, 3.3 |
| `CTX-9` | 1, 3 | 1.1, 1.2, 3.1 | `CTX-19` | 1, 4 | 1.1, 1.2, 4.1, 4.2 |
| `CTX-10` | 3 | 3.2 | `CTX-20` | 2 | 2.2 |

Count: 20 rows, all mapped. Not 4a's rows: `OBS-25`/`OBS-26` (5c), `REDIR-11`/`REDIR-24`/`REDIR-25` (6b),
`SEAM-28` (2b; re-pointed in task 5.3), `XCUT-14` (phase 10).

---

## Tasks per PR

| PR | Gate | Tasks |
|---|---|---|
| 1 | none | 1.1–1.3 (3) |
| 2 | none | 2.1–2.3 (3) |
| 3 | PRs 1, 2 | 3.1–3.4 (4) |
| 4 | PR 3 | 4.1–4.4 (4) |
| 5 | PRs 1–4 | 5.1–5.5 (5) |
| **Total** | | **19 tasks** |

PRs 1 and 2 are independent and may land in either order. PR 3 needs both. PR 4 needs PR 3. PR 5 closes. Every PR stays
green in any order these gates allow.

---

## Findings while planning

Items checked against the repository at `0332cef` on 2026-10-05, each with what the plan did.

1. **F1 — `ContextStore` and `CallContext` reference each other.** Task 3.1's store cannot compile without task 3.2's base
   type; the plan lands them in one commit (task 3.1's opening note), tests first.
2. **F2 — `Request` is both a namespace and a type.** Convention 8a: bind through `using` inside `Execution`, alias in test
   namespaces if CS0118/CS0119 appears.
3. **F3 — `Core.Tests` may not reference an adapter.** The design's "every `src/` assembly" check is met by the compile-time
   ban plus a Core reflection test and two adapter mirrors (R5).
4. **F4 — The design's `CTX-1` architecture test would trip over `<Clone>$`.** R7 filters compiler-generated members.
5. **F5 — Record equality includes the private store field (fact 6).** `A_pinned_shared_key_makes_them_equal` binds both
   contexts to one isolated store; a test against the shared store would be equal only because both bind it.
6. **F6 — `ActivityListener` is process-wide.** Every test that starts a listener, or asserts "no listener", is in the
   `Instrumentation` collection; `With_no_listener_the_factory_returns_null` fails rather than passes vacuously if a
   listener is live.
7. **F7 — Transient overshoot.** Between an insert and its drain the count may briefly exceed the cap; the burst test
   asserts the quiescent bound strictly and the transient bound with a documented slack (task 4.2).
8. **F8 — `ContextStore.Shared` is never asserted on beyond a test's own keys.** Arbitrary eviction (`CTX-13`) would make any
   shared-state assertion flaky; only `DexpaceCallContextsTests` touches it.
9. **F9 — Open rulings.** `P4a-3`, `P4a-6`, `P4a-8`, `P4a-10`, `P4a-13` and `P4a-15` are open for the lead. The plan builds
   them as the design states; if the lead rules otherwise, tasks 2.2, 3.2, 3.3, 3.1 and 5.3 are the places that move (the
   design's fallback for `P4a-6` is Node's pure promotions with registration in 4c, moving `CTX-3`/`CTX-17`'s tests with it).
