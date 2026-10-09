# Phase 7a — Serialization (Serde): Design

**Status:** Draft, for review. Written 2026-10-09 against `main` at `8dc8ee4` (phases 0 to 6c merged). Brainstormed without a
human in the loop: every judgement call the brainstorming skill would have put to the lead is taken here as a numbered ruling
(`P7a-n`) with its options and rationale, and the calls a lead may reasonably overturn are marked **open for the lead**. 7b (SSE)
and 7c (pagination) are designed in parallel by other authors in the same working tree; the interfaces this design offers them
are in [Cross-sub-phase interfaces](#cross-sub-phase-interfaces-offered-to-7b-and-7c). The scope authority is the roadmap's
Phase 7 card (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`, "Phase 7 — Serde, SSE and Pagination"). The format
follows the phase 6 designs (`docs/work/mvp/phase6/phase6c/2026-10-08-phase6c-auth-design.md`).

**What this document is.** The sub-phase design for 7a: one disposition per requirement row (`SERDE-1`–`SERDE-30`, plus the
two `HTTP-44`/`HTTP-45` rows that phase 3b handed over), the public surface 7a adds or changes, the internal types behind it,
the breaking changes, the hand-offs from phases 3a, 3b and 5a that 7a takes or declines, the pull-request segmentation, the
test strategy and the sibling ports, the exit criteria, and the rulings, which double as deviation-ledger IDs (roadmap
constraint 7). It closes issue [#1](https://github.com/dexpace/dotnet-sdk/issues/1) (`Tristate<T>`).

**What this document is not.** It is not the plan and not the checklist. It does not restate design §3.4's allocation-profile
argument, §7.3's erasure argument or §10 entry 21; it cites them and records a decision against each row. It does not design
the SSE typed adapter (7b), the pager (7c), the conformance kit's serde assertions (8a), DI registration of an `ISerde`
(phase 9) or a Newtonsoft adapter (unscheduled). It edits no design chapter, roadmap cell or `CLAUDE.md` line: the corrections
it owes are listed as proposals in [Corrections owed at close-out](#design-roadmap-and-claudemd-corrections-owed-at-close-out).

**A note on verification.** The environment this design was written in had no .NET SDK (`dotnet` reports no SDK for
`global.json`'s `10.0.401`), so `scripts/knowledge` could not run and nothing below was executed here. The corpus was read
directly from `docs/knowledge/harvested/serde.md` (157 entries, zero conflicts) and `docs/knowledge/notes/`. Facts marked
**(design-verified)** were verified by design §3.4/§7.3 on 10.0.401; facts marked **(plan step 0)** are claims this design
relies on that the plan's first task must confirm with a file-based app before any code lands.

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor or sibling | Kind | State at `8dc8ee4` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`/`RS0026`/`RS0027`, `RS0030`, `MA0051`, `CA2007`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. 7a adds one `IL2067` suppression with a written justification (P7a-6); no banned symbol is touched. |
| 2b (`SerdeException` root, both subtypes unsealed beneath it, `SerdeExtensions` profiles, `IStringSerde`, `SerdeSeamArchitectureTests`, `SerdeExceptionHierarchyTests`) | **dependency** | Met. `SERDE-4`, `SERDE-9`, `SERDE-10` cite it (roadmap card, corrected 2026-09-29). |
| 3a (`ResponseBody.OpenRead`, the bounded readers, `DefaultMaxMaterializedBytes`) | **dependency** | Met. 3a's hand-off "7a adds the sync serde readers (`ReadValue` over `OpenRead`, `HttpResponseException.GetError<T>`)" is **taken** (P7a-13). |
| 3b (`ResponseBody`/`Response` dispose latches, `PrefixedReadStream`, `HTTP-44`/`HTTP-45` ⏳ 7a) | **dependency** | Met. 3b's hand-off "7a decides whether `ReadValueAsync` closes the body" is **taken: it does** (P7a-11). |
| 4b (`Disposal.DisposeQuietly(Async)`, `ExceptionFacts.IsFatal`, `ExceptionTrail`) | **dependency** | Met. The handlers dispose through `Disposal` with the primary failure attached (P7a-17). |
| 4c (`ErrorBodyBuffer`, `ErrorMapping.ToException`, the synchronous path) | **dependency** | Met. `SERDE-28`'s 4xx/5xx branch is `ErrorBodyBuffer` + `ErrorMapping`, the one capture site (P7a-18). |
| 5a (P5a-23: the per-read materialisation limit routed to 7a; P5a-20: `DeepValue` promotion is 7a's call) | **convenience** | Both **declined** (P7a-21, P7a-22), open for the lead. |
| 5b (`UrlRedactor`) | **dependency** | Met. `SERDE-28`'s third-branch message redacts `Location` through it (P7a-19). |
| 7b, 7c | **none** (independent by `SSE-37` and ch.12) | Designed in parallel. 7a offers them two contracts and depends on neither. |

---

## Governing documents, and the phase-start reading

- **Specification.** `docs/product-spec/14-serialization-serde.md` (read in full), appendix C rows `SERDE-1`–`SERDE-30`, and
  `docs/product-spec/06-request-and-response-body-lifecycle.md` §6.4 for `HTTP-44`/`HTTP-45`.
- **Design.** §3.4 (the serde seam, the four profiles, failure types, adapter defaults, and its 2026-10-02 phase 2b as-built
  note), §7.3 (reified witness, the failure model, `Tristate`, the STJ wiring and its AOT hazard, strict coercion, `SERDE-26`,
  the response handlers, `HTTP-44`/`HTTP-45`), §10 entry 21 (covariance), §11 items 15, 18 and 42, §12's SERDE row (30/30).
- **Roadmap.** The Phase 7 card; the phase 2 segmentation design's "Phase 7" coupling (7a needs `Response`'s request and
  reason phrase for `HTTP-44`, both built in 2a); the 3a, 3b and 5a status notes' hand-offs.
- **Corpus.** `docs/knowledge/harvested/serde.md` — every entry agrees with §7.3; **no conflicts** recorded for the topic.
  `docs/knowledge/notes/` holds no serde or Tristate note. The gap list (`--gaps`) names no `SERDE` ID: every row has a
  substantive corpus entry, so no ID must be read out of appendix C alone. `HTTP-44`/`HTTP-45` are read from ch.06 §6.4,
  whose text equals appendix C's.
- **Siblings.** The Ruby 7a design (`ruby-sdk/docs/work/mvp/phase7/phase7a/2026-09-10-phase7a-serialization-design.md`,
  §R3 and "Testing strategy"); the Node codec (`nodejs-sdk/packages/codec-json/src/{tristate-replacer,tristate-schema,
  json-serde,conformance}.test.ts`); Node's `docs/sdk-documentation/{write-a-serde,write-a-response-handler}.md`. The
  Ruby gem sources the card names (`ruby-sdk/.../serde/`) are **not in the local checkout** (`ruby-sdk/` holds only `docs/`
  and `scripts/`), so the Ruby port is taken from the design's test inventory, not from test files.

---

## Scope and the 32-row census

The card's scope is `SERDE` (30 rows; 22 MUSTs, 7 SHOULDs, 1 MAY) plus `HTTP-44`/`HTTP-45`, which are **3b's rows** (the 3b
checklist marks both ⏳ 7a) built here. The roadmap's exit count of 107 for phase 7 is `SERDE` 30 + `SSE` 41 + `PAGE` 36 and
does **not** include them; 7a's checklist carries them as two extra rows marked "carried from 3b" so the 3b checklist's ⏳
cells can point at evidence (P7a-25). Legend: ✅ met; 🔨 built by 7a; ⚖ met with a recorded ruling; → travels elsewhere.

| ID | Level | Disposition | Evidence / what 7a builds |
|---|---|---|---|
| `SERDE-1` | MUST | ✅ already met | `ISerde` bundles both directions; `SystemTextJsonSerdeTests.SerializeAsync_then_DeserializeAsync_round_trips`. |
| `SERDE-2` | MUST | ✅ already met (2b) | `SerdeSeamTests.DefaultMediaType_has_no_default_implementation`, `RequestBody_FromValue_stamps_the_codecs_own_media_type`. |
| `SERDE-3` | MUST | ✅ met; 🔨 test widened | Existing leave-open tests; 7a adds a close-counting tracker over all three stream members, including the new sync `Deserialize<T>(Stream)` (P7a-13). |
| `SERDE-4` | MUST | ✅ already met (2b) | `SerdeProfileTests.Fixed_buffer_*` (offset via `AsSpan(offset)`, `ArgumentOutOfRangeException`, untouched destination). |
| `SERDE-5` | MUST | ✅ by construction | Reified generics (§3.4, §7.3); `A_generic_helper_decodes_into_the_closed_type`; 7a adds a typed-field-access DTO test. |
| `SERDE-6` | MUST | ✅ by construction; 🔨 test | `List<Dto>` decodes element-typed; an unregistered parametric target throws `DeserializationException` naming it (the "fail loudly" form on this host is a missing `JsonTypeInfo`, §7.3). |
| `SERDE-7` | MUST | ✅ by construction | `ReadValueAsync<T>` / `ReadValue<T>` are the reified helpers. |
| `SERDE-8` | MUST | ✅ by construction | §11 item 42: no `Type`-taking member exists; `SerdeSeamArchitectureTests.No_seam_member_takes_a_System_Type` stays green over the widened seam. |
| `SERDE-9` | MUST | ✅ met; 🔨 extended | Adapter filter wraps `JsonException`/`NotSupportedException`; 7a extends it to the sync stream member and the Tristate converter, and tests both. |
| `SERDE-10` | MUST | ✅ already met (2b) | `SerdeExceptionHierarchyTests`. |
| `SERDE-11` | SHOULD | ✅ by language | C# has no checked exceptions (§11 item 15). |
| `SERDE-12` | MUST | ✅ met; 🔨 extended | Existing `An_IOException_from_the_{source,destination}_propagates_unwrapped`; 7a adds the sync stream member and the response handler. |
| `SERDE-13` | MUST | 🔨 build, ⚖ P7a-9 | `ReadValue(Async)<T>` and both handlers reject a wire `null` for a non-`Nullable<>` target with `DeserializationException` naming `T`; `ReadValueOrDefault(Async)` is the explicit nullable route; `ISerde` keeps its honest `T?`. |
| `SERDE-14` | MUST | 🔨 build, ⚖ §10 entry 21 | `readonly struct Tristate<T> where T : notnull`; `Present(null)` throws; covariance SHOULD unmet (entry 21), served by `Tristate.Absent`/`Tristate.Null`. |
| `SERDE-15` | MUST | 🔨 build | STJ `JsonTypeInfo` modifier: `ShouldSerialize` = "not Absent" on every `Tristate<>` property. |
| `SERDE-16` | MUST | 🔨 build | `TristateConverter<T>`: `null` → Null; value → Present via `JsonTypeInfo<T>`. |
| `SERDE-17` | MUST | 🔨 build | `default(Tristate<T>)` is Absent; a missing key never reaches the converter (design-verified). |
| `SERDE-18` | SHOULD | 🔨 build | `Absent`/`Null`/`Present`, `FromNullable` (never Absent), `Match`, `GetValueOrDefault`, `TryGetValue`, `IsAbsent`/`IsNull`/`IsPresent`, `State`. |
| `SERDE-19` | MUST | 🔨 build, ⚖ P7a-5 | Both constructors wire Tristate on the private copy; no opt-out (a caller converter registered earlier wins by list order). |
| `SERDE-20` | SHOULD | 🔨 build | The converter writes `null` for Absent where there is no property to omit (top level, array element, dictionary value). |
| `SERDE-21` | MUST | 🔨 build | `CreateDefaultOptions` = `Web` naming with `NumberHandling = Strict` forced back (§7.3); nine named coercion tests. |
| `SERDE-22` | MUST | 🔨 test | Integer → `double` widens; `""` → `string` binds. |
| `SERDE-23` | SHOULD | ✅ STJ default; 🔨 test | Unmapped members skipped. |
| `SERDE-24` | SHOULD | ✅ STJ default; 🔨 test | `DateTimeOffset`/`DateTime` ISO-8601 round trip to the same instant. |
| `SERDE-25` | SHOULD | 🔨 build | `SystemTextJsonSerde.CreateDefaultOptions(IJsonTypeInfoResolver)` returns a fresh instance per call. |
| `SERDE-26` | MUST | 🔨 build (fix) | `new JsonSerializerOptions(caller)` → wire → `MakeReadOnly()` on the copy; the caller's instance is never frozen or wired. The copy always succeeds, so the "cannot be copied" fallback is unreachable (§11 item 18). |
| `SERDE-27` | MUST | 🔨 build | `ResponseHandlers.Deserialize<T>(ISerde)` over the reshaped `ReadValueAsync`: streams, disposes on every path, names `T` on a missing body, chains codec failures, lets `IOException` through. |
| `SERDE-28` | MUST | 🔨 build | `ResponseHandlers.DeserializeOnSuccess<T>(ISerde)`: 2xx → decode; 4xx/5xx → `HttpResponseException` over `ErrorBodyBuffer`; other → dispose and `DeserializationException` leading with the code, carrying `ETag` and redacted `Location`. |
| `SERDE-29` | SHOULD | ✅ met; 🔨 test | Read-only `JsonSerializerOptions` is thread-safe and STJ's cache is publication-safe (design-verified); concurrent-workers test. |
| `SERDE-30` | MAY | 🔨 build | `ToString()` → `Absent`, `Null`, `Present(<value>)`; `TristateSentinel.ToString()` → `Absent`, `Null`. |
| `HTTP-44` | MUST | 🔨 build (carried from 3b) | `TypedResponse<T>`: metadata without touching the body; one parse; success (including `null`) and failure memoized. |
| `HTTP-45` | MUST | 🔨 build (carried from 3b), ⚖ P7a-15 | Exactly-once by a compare-and-swap on a `TaskCompletionSource<T>`; no lock is held at all, so nothing blocks or pins a thread. |

Count: 15 already met (`SERDE-1`–`SERDE-12`, `-23`, `-24`, `-29`; eight of them gain or widen a test), 15 built (`SERDE-13`–`-22`, `-25`–`-28`, `-30`), plus the 2 carried rows. Every MUST has a row and a test.

---

## As built at `8dc8ee4`

- `Dexpace.Sdk.Core.Serialization`: `ISerde` (five members: `DefaultMediaType`, `SerializeAsync`, `DeserializeAsync`,
  `Serialize(IBufferWriter)`, `Deserialize(ReadOnlySpan)`), `IStringSerde`, `SerdeExtensions` (three profiles),
  `ResponseBodySerdeExtensions.ReadValueAsync<T>` returning `ValueTask<T?>`, disposing only the **stream**, not the body.
- `Dexpace.Sdk.Core.Errors`: abstract `SerdeException`; unsealed `SerializationException`, `DeserializationException`.
- `HttpResponseException.GetErrorAsync<T>(ISerde)` → `T?` over the buffered error body; no sync twin.
- `Pagination/Pageable.cs` calls `ReadValueAsync<TPage>` and adds its own `?? throw new InvalidOperationException(...)`.
- `SystemTextJsonSerde`: two constructors; the options constructor calls `options.MakeReadOnly()` **on the caller's
  instance** (`SERDE-26` violation, §7.3); no Tristate wiring; no default-options factory.
- No `Tristate`, no typed-response wrapper, no response handlers.
- AOT smoke: `Widget` + `SmokeJsonContext`; `CheckPipelineJsonRoundTripAsync` and `CheckSerdeProfiles`.

---

## Facts the design rests on

1. **(design-verified)** STJ's reflection resolver is `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`; the AOT-safe
   witness is a source-generated `JsonTypeInfo<T>`.
2. **(design-verified)** A converter cannot omit a property; a `JsonTypeInfoResolver.WithAddedModifier` modifier setting
   `ShouldSerialize` can, and works over a source-generated context with no attribute on the struct.
3. **(design-verified)** An open-generic `[JsonConverter(typeof(X<>))]` is unsupported by the source generator (`SYSLIB1220`);
   `MakeGenericType` + `Activator.CreateInstance` raises `IL3050`; the interface-dispatch factory leaves one `IL2067`.
4. **(design-verified)** `JsonSerializerDefaults.Web` sets `NumberHandling = AllowReadingFromString` (`"5"` → `5`); `General`
   rejects all nine `SERDE-21` coercions and widens `5` → `double`.
5. **(design-verified)** `RespectNullableAnnotations` rejects a member `null` but still returns `null` for a root `null`.
6. **(design-verified)** `new JsonSerializerOptions(options)` yields an independent mutable copy, including of a
   source-generated context's (read-only) options.
7. **(design-verified)** `default(Tristate<T>)` absent from the wire is never passed to the converter: decoding
   `{"B":null,"C":"v"}` gave `A=Absent B=Null C=Present(v)`.
8. **(plan step 0)** Source-generation **fast-path** serialization (`JsonSourceGenerationMode.Serialization`) does not run
   when a modifier has customised the `JsonTypeInfo`, so `ShouldSerialize` is honoured for a context generated in the default
   mode. If this is false, P7a-4's mitigation applies (R1).
9. **(plan step 0)** STJ passes a JSON `null` token to a **value-type** converter's `Read` (so `TristateConverter<T>` sees
   `null` at the root and in arrays); `HandleNull` is overridden to `true` regardless, to make it explicit.
10. **(plan step 0)** C# permits `??` on an unconstrained type parameter (since C# 8), so `Pageable`'s
    `await ReadValueAsync<TPage>(...) ?? throw ...` still compiles after the return type changes from `T?` to `T`.
11. **(documented `Lazy<T>` behaviour; plan step 0 measures it)** `Lazy<Task<T>>` in `ExecutionAndPublication` holds a `Monitor` for the duration of the factory's
    **synchronous** prefix; for a handler whose body stream completes synchronously (a buffered body), that prefix is the
    whole parse. Consequence: §7.3's `Lazy<Task<T>>` shape blocks concurrent first callers on a monitor, which P7a-15 avoids.
12. The local checkout carries no Ruby serde tests and no FsCheck package (`Directory.Packages.props`); 7b may add FsCheck.

---

## Argued positions

### A. `Tristate<T>` in core (`SERDE-14`, `SERDE-17`, `SERDE-18`, `SERDE-30`; issue #1; P7a-1 to P7a-3, P7a-7)

**The shape is design §7.3's, made concrete.** Three approaches were weighed:

| Approach | Verdict |
|---|---|
| (a) `readonly struct Tristate<T> where T : notnull` | **Chosen.** `default` is Absent, so `SERDE-17` is free for every field, property and record parameter (fact 7). Allocation-free. |
| (b) an abstract record class hierarchy `Tristate<T>` with `Absent`/`Null`/`Present` subclasses | Rejected. Gets covariance no closer (classes are invariant too), and `default` is `null` — the fourth state `SERDE-14` forbids — so every field would need an initializer, exactly `SERDE-17`'s trap. |
| (c) an `interface ITristate<out T>` (covariant) with struct implementations | Rejected. Meets the covariance SHOULD only for reference `T`, and a property typed as the interface defaults to `null` again. |

**Public shape** (namespace `Dexpace.Sdk.Core.Serialization`):

```csharp
public enum TristateState { Absent = 0, Null = 1, Present = 2 }

public readonly struct Tristate<T> : IEquatable<Tristate<T>>, ITristate where T : notnull
{
    public static Tristate<T> Absent { get; }                  // == default
    public static Tristate<T> Null { get; }
    public static Tristate<T> Present(T value);                // ArgumentNullException on null
    public static Tristate<T> FromNullable(T? value);          // reference T: null -> Null, else Present; never Absent
                                                               // (for a value-type T, T? is T, so this is Present; use Tristate.FromNullable(int?))

    public TristateState State { get; }
    public bool IsAbsent { get; }  public bool IsNull { get; }  public bool IsPresent { get; }
    public T Value { get; }                                    // InvalidOperationException unless Present; names the state
    public bool TryGetValue([MaybeNullWhen(false)] out T value);
    public T? GetValueOrDefault();
    public T GetValueOrDefault(T defaultValue);
    public TResult Match<TResult>(Func<TResult> onAbsent, Func<TResult> onNull, Func<T, TResult> onPresent);

    public static implicit operator Tristate<T>(T? value);     // FromNullable semantics (P7a-2)
    public static implicit operator Tristate<T>(TristateSentinel sentinel);
    // Equals(Tristate<T>), Equals(object?), GetHashCode, ==, !=, ToString
}

public static class Tristate
{
    public static TristateSentinel Absent { get; }
    public static TristateSentinel Null { get; }
    public static Tristate<T> Present<T>(T value) where T : notnull;
    public static Tristate<T> FromNullable<T>(T? value) where T : class;
    public static Tristate<T> FromNullable<T>(T? value) where T : struct;   // Nullable<T> parameter: a distinct signature
    public static T? GetValueOrNull<T>(this Tristate<T> value) where T : struct;   // SERDE-18's value-or-null for value types
}

public readonly struct TristateSentinel : IEquatable<TristateSentinel> { /* Absent or Null only; ToString */ }

public interface ITristate                                   // the codec-adapter hook (P7a-6)
{
    TristateState State { get; }
    TResult Accept<TResult>(ITristateVisitor<TResult> visitor);
}

public interface ITristateVisitor<out TResult>
{
    TResult Visit<T>(Tristate<T> value) where T : notnull;
}
```

- **P7a-1 — The name is `Tristate<T>`, not `Optional<T>`.** Design §7.3's ruling, recorded on issue #1 before building
  (the comment text is in [Issue #1](#issue-1-the-ruling-comment)). Rationale: Roslyn's two-state
  `Microsoft.CodeAnalysis.Optional<T>` (`HasValue`, `Value`) is the ecosystem's vocabulary (P14), and the specification's own
  word is `Tristate`. Every other requirement of #1 carries over: trim/AOT-safe; Absent omitted through a `JsonTypeInfo`
  modifier; implicit conversion from `T`; factory helpers; pattern matching (`t is { IsPresent: true, Value: var v }`, and
  `switch (t.State)` exhaustively); RFC 7386 merge-patch documents out of scope.
- **P7a-2 — The implicit conversion from `T` maps `null` to Null, never throws.** Options: (a) `Present` semantics, throwing on
  `null`; (b) `FromNullable` semantics. **(b)**: an implicit conversion must not throw (Framework Design Guidelines; `CA2225`'s
  family), `Tristate<string> s = maybeNull;` reading as "set or clear" is what a PATCH builder means, and it can never yield
  Absent, matching `SERDE-18`'s mapper. The explicit `Present(value)` is the throwing, fourth-state-excluding factory
  `SERDE-14` tests. With `T : notnull`, `Tristate<string?>` remains a nullability warning in a consumer's build.
- **P7a-3 — `ToString` is `Absent`, `Null`, `Present(<value>)`** (`SERDE-30`, design §7.3). A record struct is not used: its
  synthesized `ToString` prints private fields and it would add a public positional constructor that bypasses the null check.
  Equality: state equal and, for Present, `EqualityComparer<T>.Default`.
- **P7a-7 — `Tristate.Absent`/`Tristate.Null` return a `TristateSentinel`**, a two-state struct implicitly convertible to every
  `Tristate<T>`. This is §10 entry 21's "non-generic markers and implicit conversions" made concrete: `patch with { Name =
  Tristate.Null }` compiles for any `T`. `default` also reads as Absent. Options rejected: a conversion from `TristateState`
  (would admit `Present` with no value); generic-only factories (`Tristate<string>.Null` everywhere, the ergonomics
  covariance was for).

**Covariance (`SERDE-14`'s SHOULD).** Unmet, recorded as §10 entry 21 (*judged*, P8); 7a adds nothing to the ledger here.

### B. Wiring Tristate into System.Text.Json (`SERDE-15`, `SERDE-16`, `SERDE-19`, `SERDE-20`; P7a-4 to P7a-6)

All of it lives in `Dexpace.Sdk.Serialization.SystemTextJson`; core's `Tristate` carries **no STJ attribute** (design §7.3,
P3), enforced by an architecture test.

- **Converter** (`internal sealed class TristateConverter<T> : JsonConverter<Tristate<T>>`): `HandleNull => true`. `Read`:
  token `Null` → `Tristate<T>.Null`; otherwise `Present(JsonSerializer.Deserialize(ref reader, innerInfo)!)`, where
  `innerInfo` is `(JsonTypeInfo<T>)options.GetTypeInfo(typeof(T))`, resolved once per converter instance; an inner result of
  `null` from a non-null token is a `JsonException` (wrapped as `DeserializationException` by the serde). `Write`: Absent or
  Null → `WriteNullValue()` (`SERDE-20`'s degradation — the modifier has already omitted a property-held Absent); Present →
  `JsonSerializer.Serialize(writer, value, innerInfo)`. The inner `JsonTypeInfo<T>` is present in any source-generated context
  that registers a model holding `Tristate<T>`, because the generator walks the struct's public `Value` property.
- **Factory** (`internal sealed class TristateConverterFactory : JsonConverterFactory`): `CanConvert` is
  `IsGenericType && GetGenericTypeDefinition() == typeof(Tristate<>)`; `CreateConverter` obtains
  `RuntimeHelpers.GetUninitializedObject(typeToConvert)` (a default, i.e. Absent, boxed instance), casts to `ITristate`, and
  calls `Accept(ConverterVisitor.Instance)`, whose `Visit<T>` returns `new TristateConverter<T>()` from statically typed
  generic code. **P7a-6** — this is the shape §7.3 found correct by construction under NativeAOT; it costs one public
  interface pair in core (`ITristate`, `ITristateVisitor<TResult>`), which is the price of an adapter in another assembly
  reaching the closed type without `MakeGenericType` (`InternalsVisibleTo` to an adapter is barred by `CLAUDE.md`). The pair
  is documented as the codec-adapter hook, so a future Newtonsoft adapter uses the same door. The `IL2067` on
  `GetUninitializedObject` is suppressed with `[UnconditionalSuppressMessage]` and the justification "the type is a closed
  `Tristate<>` constructed by the caller's source-generated metadata; the AOT smoke consumer's value-type `Tristate<int>`
  proves it" (`NFR-9`).
- **Modifier** (`internal static class TristateModifier.Apply(JsonTypeInfo)`): for each property whose `PropertyType` is a
  closed `Tristate<>`, set `ShouldSerialize` to `(_, v) => v is ITristate { State: not TristateState.Absent }`, **composed
  with** (AND) any `ShouldSerialize` already present, so a caller's predicate is kept. The value arrives boxed through the
  public `ShouldSerialize` signature; one box per Tristate property per write, accepted (R5).
- **P7a-4 — Wiring entry point.** One public, explicitly mutating extension,
  `TristateJsonSerializerOptionsExtensions.AddTristateSupport(this JsonSerializerOptions options)`: requires a non-null
  `TypeInfoResolver` (`ArgumentException` otherwise, since `WithAddedModifier` needs a resolver), appends the factory if no
  `TristateConverterFactory` is already present, and wraps `TypeInfoResolver` with the modifier (idempotent in effect: a
  second wrap sets the same predicate). Options: (a) keep all wiring internal; (b) expose the factory and modifier types;
  (c) one `AddTristateSupport` method. **(c)**: a caller who uses `JsonSerializer` directly on SDK models (an ASP.NET
  endpoint, a test) would otherwise serialize `Tristate`'s public properties as an object — exactly the silent PATCH
  corruption `SERDE-19` warns of — and (b) would make two implementation types public for one use. Node exports its replacer
  for the same reason (`tristate-replacer.test.ts`, "the replacer is exported so a caller can compose their own
  JSON.stringify call"). *Dated correction,
  2026-10-09 (phase 7a review):* "silent" was too strong. Unwired, an Absent or Null field makes `JsonSerializer` throw
  `InvalidOperationException` (the struct's `Value` getter throws for both states); only a Present field is written as an object of the
  struct's properties. The rationale for (c) stands; `TristateJsonTests.Without_AddTristateSupport_an_Absent_or_Null_field_throws_and_a_Present_field_is_written_as_an_object` pins the measured behaviour.
- **P7a-5 — `SERDE-19`: always on, no opt-out.** Both constructors call the wiring on the private copy. The spec's MAY
  ("opting out only for a caller that already installed equivalent wiring") is not needed: a caller's own `Tristate<>`
  converter, registered in its options before construction, precedes ours in `Converters` and wins; our modifier on top only
  omits Absent, which is the caller's intent if it wired "equivalently". Recorded as a reading, not a deviation.

**Where the wire has no key** (`SERDE-20`): top-level Absent and Null both write `null`; an array element Absent writes
`null` (indices never shift); a `Dictionary<string, Tristate<T>>` value Absent writes `null` (STJ has no per-entry
`ShouldSerialize`) — the documented compromise, tested. A top-level `null` decodes to Null.

### C. `SystemTextJsonSerde` options (`SERDE-21`–`SERDE-26`, `SERDE-29`; P7a-8)

- **Constructor fix (`SERDE-26`).** `SystemTextJsonSerde(JsonSerializerOptions options)`: guard `TypeInfoResolver` as today;
  then `var copy = new JsonSerializerOptions(options); copy.AddTristateSupport(); copy.MakeReadOnly(); _options = copy;`. The
  context constructor copies `context.Options` the same way. The caller's instance is never frozen, never gains a converter,
  and keeps its resolver reference (fact 6). The XML doc's "The supplied options are made read-only by this constructor" is
  replaced with the copy statement — a **breaking** behavioural fix in the caller's favour.
- **P7a-8 — The default configuration** is a new factory,
  `public static JsonSerializerOptions SystemTextJsonSerde.CreateDefaultOptions(IJsonTypeInfoResolver typeInfoResolver)`,
  returning a **fresh, mutable** instance per call (`SERDE-25`): `new(JsonSerializerDefaults.Web) { NumberHandling =
  JsonNumberHandling.Strict, RespectNullableAnnotations = true, TypeInfoResolver = typeInfoResolver }` followed by
  `AddTristateSupport()`. Design §7.3's ruling: `Web` naming (camelCase, case-insensitive read) with `Strict` forced back,
  because `Web`'s `AllowReadingFromString` is the first coercion `SERDE-21` forbids (fact 4); `RespectNullableAnnotations` on,
  as §7.3 records. The resolver is **required**: the AOT-safe path needs one and the modifier wraps it. Usage:
  `new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(MyContext.Default))`.
  - **What "default" means for a caller-supplied codec.** The context constructor keeps the context's own declared options
    (`[JsonSourceGenerationOptions]`, `General` by default, which is also strict). A caller who passes options with
    `AllowReadingFromString` gets that: `SERDE-21` binds the *default* configuration, and overriding a caller's explicit
    choice on the copy would be the "SDK-injected behaviour change" `SERDE-26` forbids in spirit. Only Tristate wiring is
    injected, because `SERDE-19` requires it. Options rejected: force `Strict` on every copy (overrides an explicit caller
    decision); make the serde's parameterless constructor the default path (there is no AOT-safe resolver to default to).
- `SERDE-23` (unmapped members skipped) and `SERDE-24` (ISO-8601) are STJ defaults under both `Web` and `General`; tested.
- `SERDE-29`: a read-only `JsonSerializerOptions` and STJ's metadata cache are thread-safe; tested with many concurrent
  workers on one serde.

### D. The typed readers and the failure model (`SERDE-9`, `SERDE-12`, `SERDE-13`, `SERDE-27` body half; P7a-9 to P7a-13)

```csharp
public static class ResponseBodySerdeExtensions
{
    public static ValueTask<T> ReadValueAsync<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default);
    public static ValueTask<T?> ReadValueOrDefaultAsync<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default);
    public static T ReadValue<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default);
    public static T? ReadValueOrDefault<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default);
}
```

- **P7a-9 — `SERDE-13`'s root `null` check lives in the reader layer; `ISerde` keeps `T?`.** Nullable reference annotations
  are erased (`typeof(Dto?) == typeof(Dto)`, §7.3), so no codec can know the caller's intent; the seam's `T?` return already
  tells the compiler `null` is possible, so no null "flows through a non-null result" there. `ReadValue(Async)<T>` returns
  `T` and, when the codec returns `null` and `T` is not a value type (`!typeof(T).IsValueType`, i.e. a reference type —
  `Nullable<>` targets legitimately decode `null`), throws `DeserializationException("The response body is the JSON literal
  null, but '{T}' is not nullable; use ReadValueOrDefaultAsync to accept null.")`. A non-nullable **value** type never gets
  here: STJ itself throws for `null` → `int`, wrapped naming `T`. `ReadValueOrDefault(Async)` returns `T?` and admits the
  wire `null` (Node's `admitsNull` opt-in, `json-serde.test.ts`). Every non-null-returning decode overload the SDK exposes —
  the two readers and both handlers — rejects; `GetErrorAsync`/`GetError` keep `T?`, documented as "possibly null", because an
  error model is read opportunistically. **Open for the lead**: whether the spec's "every decode overload" also binds
  `ISerde`'s `T?` members; this design reads it as binding overloads whose result is non-null, as §7.3 does.
  - The type constraint is **not** `where T : notnull`: it would warn on `ReadValueAsync<int?>` and on `Pageable`'s
    unconstrained `TPage`, both legitimate.
- **P7a-10 — A missing body is detected by peeking one byte.** `SERDE-27` requires a missing body (204) to fail naming `T`;
  today an empty stream surfaces as STJ's generic "no JSON tokens" `JsonException`. The reader returns early when
  `ContentLength == 0`, else reads one byte; zero bytes → `DeserializationException("The response has no body to deserialize
  as '{T}'.")`; otherwise the codec reads through a `PrefixedReadStream` (3b's internal type) that replays the byte and
  continues from the live stream — **no materialisation**. A zero-byte body counts as missing, as in Ruby (a caller cannot
  tell them apart). `ReadValueOrDefault` treats a missing body the same way: it admits a wire `null`, not an absent payload.
- **P7a-11 — The readers dispose the body on every path** (3b's hand-off, `BODY-16`'s rule extended to the typed reader).
  Options: leave the body open (today: only the stream is disposed); dispose. **Dispose**: a single-use body is useless after
  a consuming read, `SERDE-27` requires "close on every path", and 3b made every other consuming reader do the same. Disposal
  goes through `Disposal.DisposeQuietly(Async)(body, primary)` so a dispose failure after a decode failure is attached, not
  substituted (RECOV-16's pattern). `Pageable` already disposes the response afterwards; both latches make the second call a
  no-op. **Breaking**, recorded.
- **P7a-12 — The failure model is unchanged in kind.** Codec failures surface from the codec already wrapped
  (`SerdeException` subtypes are never re-wrapped); `IOException` from the body stream propagates unwrapped (`SERDE-12`); the
  readers' own two failures (missing body, root `null`) are `DeserializationException` with no inner exception.
  `StreamConsumedException` on a second read propagates unchanged.
- **P7a-13 — The sync path: one new seam member, with a default implementation.** 3a handed 7a the sync readers. The seam has
  no synchronous stream decode, so a sync `ReadValue` would have to materialise the body — the very thing `SERDE-27` forbids.
  Options: (a) materialise (`ReadAsBytes` + `Deserialize(span)`); (b) add `T? Deserialize<T>(Stream source)` to `ISerde` as an
  abstract member; (c) add it as a **default interface member** whose default reads the stream to EOF bounded by
  `ResponseBody.DefaultMaxMaterializedBytes` (throwing `BodyTooLargeException` above it, `IOException` unwrapped) and calls
  `Deserialize<T>(ReadOnlySpan<byte>)`. **(c)**: source- and binary-compatible for every existing implementer (test fakes,
  7b/7c's fakes written in parallel), and `SystemTextJsonSerde` overrides it with `JsonSerializer.Deserialize(Stream,
  JsonTypeInfo<T>)`, which streams and leaves the stream open (`SERDE-3`). The default's materialisation is documented on the
  member, not silent. No sync **encode** stream member is added: `RequestBody.FromValue` and the sync pipeline already use the
  `IBufferWriter` primitive. No `CancellationToken` parameter: STJ's sync stream overload takes none, and `RS0027` would then
  require it to be the longest overload. `SEAM-22`'s architecture test still holds (no `Type` parameter).
  `HttpResponseException` gains `public T? GetError<T>(ISerde serde)` over `Response.Body.OpenRead()` + the new member,
  mapping `StreamConsumedException` to `ResponseNotReadException` as its async twin does.

### E. The response handlers (`SERDE-27`, `SERDE-28`; P7a-16 to P7a-19)

```csharp
namespace Dexpace.Sdk.Core.Http.Response;
public interface IResponseHandler<T>
{
    ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken);   // owns and disposes `response`
}

namespace Dexpace.Sdk.Core.Serialization;
public static class ResponseHandlers
{
    public static IResponseHandler<T> Deserialize<T>(ISerde serde);            // SERDE-27
    public static IResponseHandler<T> DeserializeOnSuccess<T>(ISerde serde);   // SERDE-28
}
```

- **P7a-16 — An SPI interface plus a factory class, implementations internal.** Options: a `Func<Response, CancellationToken,
  ValueTask<T>>` (undiscoverable, no XML contract); two public handler classes (Ruby's `DecodingHandler`/`StatusAwareHandler`);
  an interface with internal implementations. **The interface**: `CLAUDE.md`'s "interfaces for SPIs"; a generated SDK supplies
  its own typed-error handler by implementing it; the narrow-API rule keeps the two implementations internal. Async-only: there
  is no sync `TypedResponse` (P7a-15), so a sync handler would have no consumer.
- **P7a-17 — `Deserialize<T>`** is `try { return await response.Body.ReadValueAsync<T>(serde, ct); } finally { dispose the
  response through Disposal with the primary failure }`. It therefore inherits all of `SERDE-27` from P7a-10 to P7a-12 and
  adds the **response** dispose. `SERDE-3` (the codec does not close the caller's stream) and this rule (the handler disposes
  the response it owns) bind different objects at different layers, as Ruby's design warns; both behaviours are tested.
- **P7a-18 — `DeserializeOnSuccess<T>`** branches on status:
  - 2xx → delegates to `Deserialize<T>` (one decode path; a 204 is a missing body naming `T`);
  - 400–599 (including a non-canonical 599) → `throw ErrorMapping.ToException(await ErrorBodyBuffer.CaptureAsync(response,
    ct))` — the one capture site 4c built, the same `MaxBufferedErrorBytes` (1 MiB, `BODY-30`) bound, and the capture releases
    the live response itself, so this branch adds no second dispose. The "mapped" exception is `HttpResponseException`, which
    is what `RECOV-15`'s mapping produces in this port (there is no typed-error factory in core);
  - anything else (1xx, an unfollowed 3xx, 304) → dispose, then `DeserializationException` (P7a-19).
- **P7a-19 — The third branch's message leads with the code and redacts `Location`.** Form:
  `"304 Not Modified: expected a 2xx response to deserialize as '{T}'. ETag: \"abc\". Location: https://host/path?token=REDACTED."`
  `ETag` is copied raw (it is an opaque validator, and parsing it in an error path would turn a diagnostic into a second
  failure — Ruby's `DEF-2` argument). `Location` is resolved against `Response.Request.Uri` when relative and passed through
  `UrlRedactor` (5b's default-deny query redaction, `XCUT-19`), because an exception message is logged and a redirect target
  can carry a credential in its query; a `Location` that does not parse is replaced by `<unparseable>`. Each part is omitted
  when its header is absent. This is a `Security`-category test.

### F. `TypedResponse<T>` (`HTTP-44`, `HTTP-45`; P7a-14, P7a-15)

```csharp
namespace Dexpace.Sdk.Core.Http.Response;
public sealed class TypedResponse<T> : IAsyncDisposable, IDisposable
{
    public TypedResponse(Response response, IResponseHandler<T> handler, CancellationToken cancellationToken = default);
    public Request.Request Request { get; }
    public Status Status { get; }
    public Headers Headers { get; }
    public Protocol Protocol { get; }
    public string? ReasonPhrase { get; }
    public ValueTask<T> GetValueAsync(CancellationToken cancellationToken = default);
    public void Dispose();
    public ValueTask DisposeAsync();
}
```

- **P7a-14 — Name and surface.** `TypedResponse<T>` (Ruby's name, the spec's "typed-response wrapper"). Rejected:
  `Response<T>` (collides in reading with the `Response` class and namespace, and with Azure's eager `Response<T>.Value`,
  whose semantics differ); `LazyResponse<T>`. The metadata is copied from the response at construction, so reading it can
  never touch the body. **The wrapped `Response` is not exposed**: a `Body` accessor would let a caller consume the single-use
  body behind the memo, which `HTTP-44` forbids. Disposal forwards to the response's latched dispose (`HTTP-43`).
- **P7a-15 — Exactly-once without a lock: a compare-and-swap on a `TaskCompletionSource<T>`.** Design §7.3 named
  `Lazy<Task<T>>`. Fact 11 shows its monitor is held across the factory's synchronous prefix, which for a buffered body is the
  whole parse, so concurrent first callers **block a thread** — the .NET form of what `HTTP-45` forbids. The adopted shape:

  ```csharp
  private Task<T>? _value;
  public ValueTask<T> GetValueAsync(CancellationToken ct = default)
  {
      var task = Volatile.Read(ref _value) ?? Start();
      return ct.CanBeCanceled ? new(task.WaitAsync(ct)) : new(task);
  }
  private Task<T> Start()
  {
      var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
      if (Interlocked.CompareExchange(ref _value, tcs.Task, null) is { } winner) return winner;
      _ = RunAsync(tcs);            // never throws: every outcome lands in tcs
      return tcs.Task;
  }
  ```

  `RunAsync` awaits `handler.HandleAsync(response, _cancellationToken)` and completes the source with the result (a `null`
  included) or with `TrySetException(ex)` for **every** exception, cancellation included, so awaiting re-throws the **same
  exception object** on every later access (`HTTP-44`); a fatal exception (`ExceptionFacts.IsFatal`) is recorded and then
  rethrown. Nothing holds a lock, so no caller blocks or pins a thread (`HTTP-45` satisfied by construction, not by a
  primitive's documentation). The handler runs under the **construction** token (the call's), never a caller's: a caller's
  `GetValueAsync(token)` cancels only its own wait (`WaitAsync`), so one impatient reader cannot poison the memo for the
  others. A cancellation of the construction token is a memoized failure, as the requirement says of every failure. Disposing
  the wrapper while a parse runs releases the body, the handler fails, and that failure is memoized. The `_ = RunAsync(...)`
  is not a background launch (it runs inline on the winning caller's thread until its first await), so `BannedSymbols.txt`'s
  background-launch rule does not apply; the plan confirms no analyzer flags it. A dated correction to §7.3 is owed.
  Options rejected: `Lazy<Task<T>>` (fact 11); `SemaphoreSlim.WaitAsync` (works, but a lock where none is needed, and its
  waiters' cancellation semantics are more code); a sync `Value` property (sync-over-async).

### G. Declined hand-offs (P7a-21, P7a-22)

- **P7a-21 — The per-read materialisation limit (5a's P5a-23) is declined. Open for the lead.** 5a routed "a per-read limit"
  to 7a "which reshapes the reader family once". 7a does reshape the *typed* readers, but they stream and are not bounded by
  the cap. Adding a limit to `ReadAsBytes(Async)`/`ReadAsString(Async)` without tripping `RS0026`/`RS0027` takes three
  overloads per reader (`()`, `(CancellationToken)`, `(long maxBytes, CancellationToken = default)`) — twelve members, a
  binary break of four virtual members that `LoggingResponseBody` and transport bodies override, and a change to 3a/3b-owned
  types that SSE and paging also call. No requirement asks for it (`IO-9` requires a bound, which the constant is), and a
  larger read already has a route: `OpenRead(Async)`. The cap stays the documented constant; the hand-off is closed as "not
  built, by decision", reopened if a consumer asks. Options rejected: the twelve overloads; a `ResponseReadOptions` record
  (a new type for one knob).
- **P7a-22 — `DeepValue` stays internal** (5a's P5a-20 left the public promotion to 7a). No generated model with a
  floating-point array member exists yet, and serde does not need it: STJ's round trip is value-level, not equality-level.

### H. What 7a does not change

`ISerde`'s five existing members, `IStringSerde`, `SerdeExtensions`, the exception hierarchy, `RequestBody.FromValue`
(`SERDE-2`), `GetErrorAsync`'s nullable contract, and the `Pageable` source (7c's) are untouched apart from the members listed
in the next section.

---

## The public surface (`PublicAPI.Unshipped.txt`)

**`src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt` — added** (exact analyzer spellings are the plan's; this is the inventory):

- `Dexpace.Sdk.Core.Serialization.TristateState` (enum: `Absent = 0`, `Null = 1`, `Present = 2`).
- `Dexpace.Sdk.Core.Serialization.Tristate<T>` (struct) and every member of §A: `Absent`, `Null`, `Present(T)`,
  `FromNullable(T?)`, `State`, `IsAbsent`, `IsNull`, `IsPresent`, `Value`, `TryGetValue`, both `GetValueOrDefault`, `Match`,
  two implicit operators, `Equals` ×2, `GetHashCode`, `==`, `!=`, `ToString`, and the explicit `ITristate.Accept`.
- `Dexpace.Sdk.Core.Serialization.Tristate` (static): `Absent`, `Null`, `Present<T>`, `FromNullable<T>` ×2, `GetValueOrNull<T>`.
- `Dexpace.Sdk.Core.Serialization.TristateSentinel` (struct): `Equals` ×2, `GetHashCode`, `==`, `!=`, `ToString`.
- `Dexpace.Sdk.Core.Serialization.ITristate` (`State`, `Accept<TResult>`), `ITristateVisitor<TResult>` (`Visit<T>`).
- `Dexpace.Sdk.Core.Serialization.ISerde.Deserialize<T>(System.IO.Stream! source) -> T?` (default interface member).
- `ResponseBodySerdeExtensions.ReadValueOrDefaultAsync<T>`, `ReadValue<T>`, `ReadValueOrDefault<T>`.
- `Dexpace.Sdk.Core.Serialization.ResponseHandlers` (`Deserialize<T>`, `DeserializeOnSuccess<T>`).
- `Dexpace.Sdk.Core.Http.Response.IResponseHandler<T>` (`HandleAsync`).
- `Dexpace.Sdk.Core.Http.Response.TypedResponse<T>` and every member of §F.
- `Dexpace.Sdk.Core.Errors.HttpResponseException.GetError<T>(ISerde!) -> T?`.

**Changed:** `ResponseBodySerdeExtensions.ReadValueAsync<T>` → `ValueTask<T>` (was `ValueTask<T?>`).

**`src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt` — added:**
`static SystemTextJsonSerde.CreateDefaultOptions(IJsonTypeInfoResolver! typeInfoResolver) -> JsonSerializerOptions!`;
`SystemTextJsonSerde.Deserialize<T>(System.IO.Stream! source) -> T?`;
`TristateJsonSerializerOptionsExtensions` and `static ...AddTristateSupport(this JsonSerializerOptions! options) -> void`.
Constructor signatures are unchanged; their documented behaviour changes (copy, not freeze).

Internal: `TristateConverter<T>`, `TristateConverterFactory`, `TristateModifier`, the two handler implementations, the
`ConverterVisitor`, and an internal `SerdeValues.RequireNonNull<T>(T? value)` used by the readers and offered to 7b.

---

## Breaking changes

All go in `CHANGELOG.md` `[Unreleased]` under 7a; the package is pre-1.0.

1. `ReadValueAsync<T>` returns `ValueTask<T>` and throws `DeserializationException` naming `T` for a wire `null` into a
   reference-type target (was: returned `null`). Use `ReadValueOrDefaultAsync<T>` to accept `null`.
2. `ReadValueAsync<T>` disposes the **body** on every path (was: only the stream).
3. `ReadValueAsync<T>` on an empty body throws `DeserializationException` "has no body to deserialize as '{T}'" with no inner
   exception (was: a generic `DeserializationException` wrapping STJ's "no JSON tokens").
4. `SystemTextJsonSerde(JsonSerializerOptions)` no longer makes the caller's options read-only; the caller's later mutations
   are allowed and do not affect the serde (fix of `SERDE-26`).
5. `ISerde` gains a member (with a default implementation): source- and binary-compatible for implementers; listed because
   `PublicAPI` diffs it.

---

## Keeping the `Security` classes green

No phase-1 `Security` class is edited. `EnsureSuccessGetErrorRoundTripTests` (STJ tests) and the Core error-mapping classes
read `GetErrorAsync`, whose contract does not change. 7a **adds** one `Security` class,
`StatusAwareHandlerLocationRedactionTests` (P7a-19). The plan runs `--filter-trait "Category=Security"` after each PR.

---

## Migration plan from the as-built code

1. `SystemTextJsonSerde`'s constructor: copy, wire, freeze the copy; update its XML doc and the package README.
2. `ReadValueAsync`: reshape return type, add the missing-body peek, the root-`null` check, the body dispose; add the three
   sibling readers. The 3 existing `BodyConvenienceTests` keep passing (`ReadValueAsync_is_single_use` now sees the
   `StreamConsumedException` before the dispose; the plan checks the exception type is unchanged).
3. `Pageable` (7c's file) is not edited by 7a. Its `?? throw` becomes dead but compiles (fact 10). If 7c lands first and has
   restructured the call, nothing changes for 7a; if 7a lands first, 7c deletes the check (hand-off).
4. The AOT smoke's `SmokeJsonContext` gains the new model types; `CheckPipelineJsonRoundTripAsync` keeps working.

---

## PR segmentation

Five PRs on branch `<issue>-phase-7a-serde`, each code plus tests, each green alone:

| PR | Content | Rows |
|---|---|---|
| 1 | `Tristate<T>`, `Tristate`, `TristateSentinel`, `TristateState`, `ITristate`, `ITristateVisitor<TResult>`; Core unit tests; the no-STJ-attribute architecture test | `SERDE-14`, `-17`, `-18`, `-30` |
| 2 | STJ: converter, factory, modifier, `AddTristateSupport`, the copying constructor, `CreateDefaultOptions`; coercion, options and concurrency tests | `SERDE-15`, `-16`, `-19`–`-26`, `-29` |
| 3 | Readers: `ISerde.Deserialize(Stream)` DIM + STJ override, `ReadValue(Async)`/`ReadValueOrDefault(Async)`, missing body, body dispose, `GetError<T>`; `SERDE-3`/`-5`/`-6`/`-9`/`-12`/`-13` tests | `SERDE-3`, `-5`–`-13` |
| 4 | `IResponseHandler<T>`, `ResponseHandlers`, `TypedResponse<T>`; handler and memo tests, the `Security` class | `SERDE-27`, `-28`, `HTTP-44`, `-45` |
| 5 | AOT smoke `CheckPhase7aSerdeAsync`; `docs/sdk-documentation/serde.md`; checklist; `CHANGELOG`; corrections; status note | exit criteria |

**Parallel-work hotspots.** 7b and 7c edit `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` and `SmokeModels.cs`,
`src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt`, `CHANGELOG.md` and the roadmap's status notes. 7a adds **one** new check
method (`CheckPhase7aSerdeAsync`) and one call line, appends to the models file and the API file (both sorted by the
analyzer's fixer, so conflicts are line-local), and writes its changelog entry under its own sub-heading.

---

## Test strategy, vectors and ports

Every test class carries `[Trait("Category", …)]` (`TestCategoryTests` enforces it). `Dexpace.Sdk.Core.Tests` uses fakes only
(SEAM-2): a `FakeSerde` over `System.Text.Json` would be a codec in Core.Tests, so Core-side handler tests use a small
in-test fake codec that decodes a fixed byte grammar and returns `null` for `null`. No new package (no FsCheck; see P7a-24).

**Core (`tests/Dexpace.Sdk.Core.Tests`), Unit:**

- `Serialization/TristateTests` — `default` is Absent (`SERDE-17`); `Present(null!)` throws `ArgumentNullException`
  (`SERDE-14`); `Tristate.Absent`/`Tristate.Null` convert to `Tristate<string>`, `Tristate<int>` and `Tristate<Dto>`
  (covariance's use, §10 entry 21); implicit from `T` and from `null` (P7a-2); `FromNullable` ×2 never Absent (`SERDE-18`);
  `Match` hits exactly one arm per state; `Value` throws naming the state; `TryGetValue`, `GetValueOrDefault` ×2,
  `GetValueOrNull`; equality and hash per state; `ToString` asserted by **string equality** (`"Absent"`, `"Null"`,
  `"Present(5)"`) (`SERDE-30`); `switch` on `State` exhaustive.
- `Serialization/ReadValueTests` — async and sync twins of: typed value; root `null` → `DeserializationException` naming
  `T` (reference type); `Nullable<int>` admits `null`; `ReadValueOrDefault` admits `null`; empty body (`ContentLength 0` and
  unknown-length zero bytes) → "no body" naming `T`, with `ReadValueOrDefault` too; the codec receives a non-materialised
  stream (the counting body's `ReadAsBytes*` overrides throw if called); body disposed exactly once on success, codec failure,
  `IOException`, root-`null`; `IOException` mid-stream propagates unwrapped (`SERDE-12`); codec `DeserializationException`
  is the same instance (`SERDE-9`).
- `Serialization/ResponseHandlersTests` — `SERDE-27`'s five-case matrix as five tests (valid → value + one response dispose;
  204 → names `T`; malformed → `SerdeException` with non-null `InnerException`; mid-stream `IOException` unwrapped; disposed
  in every case); `SERDE-28`: 200 decodes; 500 and 599 → `HttpResponseException` with the status and a buffered error body
  readable **after** the live response is disposed; 304 and 103 → `DeserializationException` whose message starts with the
  code, carries the raw `ETag`, and one dispose.
- `Http/TypedResponseTests` — metadata readable with the body never opened (a body whose open throws); one handler call
  across three sequential `GetValueAsync`; **64 concurrent first callers** released by a gate → one handler call, all see the
  same value; a `null` success memoized; a failure re-thrown as `Assert.Same` the same exception object, handler not re-run;
  a handler blocked on a gate does **not** block a second caller's thread (its `GetValueAsync` returns an incomplete task
  synchronously — the observable form of `HTTP-45`); a caller's cancelled wait leaves the parse running and a third caller
  gets the value; a cancelled construction token is memoized; `Dispose` forwards once.
- `Errors/HttpResponseExceptionGetErrorTests` — the sync `GetError<T>` mirrors the async tests, including
  `ResponseNotReadException` on a consumed body.
- `Architecture/TristateArchitectureTests` — `Tristate<T>` and `TristateSentinel` carry no attribute from
  `System.Text.Json*` (P3); `SerdeSeamArchitectureTests` re-run unchanged over the six-member seam.

**Core, Security:** `Security/StatusAwareHandlerLocationRedactionTests` — a 302 with `Location:
https://h/p?access_token=s3cr3t` and a relative `Location` both produce a message without `s3cr3t` (P7a-19).

**STJ adapter (`tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests`), Unit,** over a test `JsonSerializerContext` in the
default generation mode (fact 8):

- `TristateJsonTests` — ported from Node `tristate-replacer.test.ts` and `tristate-schema.test.ts`: Absent omits the key,
  Null emits `null`, Present emits the value (`SERDE-15`); `{}`/`{"x":null}`/`{"x":v}` → Absent/Null/Present with the element
  type preserved, for `string`, `int` (value type, the AOT hazard), a nested DTO and `List<Dto>` (`SERDE-16`, `-17`); a
  positional record and a class with init properties; a Present carrying an object encodes the object, not the wrapper; a
  nested `Tristate` inside a Present DTO is still rewritten; on by default from a bare caller codec (`SERDE-19`); top-level
  Absent/Null → `null`, top-level `null` → Null, array-element Absent → `null` with indices intact, dictionary-value Absent →
  `null` (`SERDE-20`); a property named `""` and a property with a caller `ShouldSerialize` (composition); a caller-registered
  `Tristate<>` converter wins (P7a-5); `AddTristateSupport` without a resolver throws; a seeded round-trip loop over
  10 000 generated Absent/Null/Present models (`Random(seed)`, seed logged — Ruby's mandated property test, P7a-24).
- `StrictCoercionTests` — the nine `SERDE-21` coercions as **nine explicit `[Fact]`s** under `CreateDefaultOptions` and again
  under a `General` context (Ruby: "a loop that drops one is invisible"): `"5"`→`int`, `"1.5"`→`double`, `"true"`→`bool`,
  `""`→`int`, `1.5`→`int`, `true`→`int`, `1`→`bool`, `true`→`double`, `5`→`string`; and `SERDE-22`'s two permissions plus a
  well-typed binding as their own tests.
- `SerdeOptionsTests` — `SERDE-25` (two `CreateDefaultOptions` calls are distinct and mutable); `SERDE-26` (after
  construction the caller's options accept `WriteIndented = true`, keep their `Converters.Count` and resolver reference, and
  serializing a Tristate model through them shows **no** omission — no SDK-injected change); `SERDE-23` extra member ignored;
  `SERDE-24` `DateTimeOffset` with offset → ISO-8601 string → equal instant; `RespectNullableAnnotations` rejects a member
  `null` under the defaults; `SERDE-29` 32 workers × 500 encode/decode of distinct values on one serde, no cross-talk.
- `SystemTextJsonSerdeTests` additions — the sync `Deserialize(Stream)` leaves the source open, propagates `IOException`
  unwrapped, wraps malformed input (`SERDE-3`, `-9`, `-12`); a close-counting tracker shows 0 closes across all three stream
  members (`SERDE-3`); `List<Dto>` decodes typed, an unregistered `List<Other>` throws naming it (`SERDE-6`); a DTO's field
  access returns typed values (`SERDE-5`).
- `ReadValueEndToEndTests` — `ReadValueAsync` and `ResponseHandlers` over the real adapter: root `null` naming `T`
  (`SERDE-13` through every overload), a Tristate PATCH model decoded from a response.

**AotSmoke (`[Trait]`-free executable; `AotSmoke` category for any test-project mirror):** `CheckPhase7aSerdeAsync` —
a `WidgetPatch(Tristate<string> Name, Tristate<int> Size, Tristate<string> Note)` sent as a `PATCH` body through
`RequestBody.FromValue` over the in-process transport, asserting the wire bytes are `{"name":null,"size":3}` (Absent `note`
omitted, camelCase from `CreateDefaultOptions`), echoed back and decoded through `TypedResponse<WidgetPatch>` +
`ResponseHandlers.DeserializeOnSuccess`, asserting Null/Present/Absent, a second `GetValueAsync` returning the same instance,
and `ReadValueAsync` of `null` throwing `DeserializationException`. The value-type `Tristate<int>` is what proves P7a-6's
suppression (`NFR-9`).

**Ports, named.** Node: `tristate-replacer.test.ts` (16 cases → `TristateJsonTests`), `tristate-schema.test.ts` (the
decode cases; the prototype-pollution cases have no .NET referent and are not ported), `json-serde.test.ts` (`SERDE-3`,
`-4`, `-9`, `-12`, `-13` incl. `admitsNull`, `-25`; the abort-signal cases map to the existing cancellation tests),
`conformance.test.ts` (`SERDE-22`–`SERDE-24`, `-29`). Ruby: the design's six test groups (handler matrix against a fake
codec, nine coercion fixtures, sentinel string equality, the round-trip property test, `SERDE-4`'s four-part matrix already
in 2b); the gem test files are not in the checkout. Docs: Ruby `serde.md` and Node `write-a-serde.md` /
`write-a-response-handler.md` shape `docs/sdk-documentation/serde.md`.

---

## Cross-sub-phase interfaces offered to 7b and 7c

7a depends on neither sibling. It offers:

- **To 7b (typed SSE adapter).** The `SERDE-13` rule is one internal helper, `SerdeValues.RequireNonNull<T>(T? value)`,
  which throws the same `DeserializationException` wording; design §7.3 says the typed SSE adapter rejects a wire `null` the
  same way. If 7b lands first with its own check, 7a's PR 3 converges both on the helper.
- **To 7c (pager).** `ReadValueAsync<T>` returns non-null `T` or throws `DeserializationException` naming `T`; 7c may delete
  `Pageable`'s `?? throw new InvalidOperationException(...)` (which also wrongly throws a non-serde type). The blocking
  `Pageable<T>` may use `ReadValue<T>`. Both readers now dispose the body; the pager's own response dispose stays and is a
  latched no-op.

---

## Hand-offs to later phases

- **8a** — the conformance kit's serde assertions lift `SERDE-3`, `-4`, `-9`, `-10`, `-12`, `-13`, `-15`–`-20` from these
  tests into framework-free form, run against `SystemTextJsonSerde` as the first driver.
- **8b** — `HttpResponseMessageBody.OpenRead` (the sync twin) makes `ReadValue<T>` work over the real transport; until then
  it throws `NotSupportedException` on transport bodies, as every sync reader does. *Dated correction, 2026-10-09 (phase 7b
  rebase):* phase 7b built `HttpResponseMessageBody.OpenRead` (its checklist's D1), so `ReadValue<T>` already works over
  `SystemNetHttpClient` (`HttpResponseMessageBodyTests.ReadValue_decodes_the_transport_body_synchronously`); this hand-off is
  discharged and 8b keeps only the `RequestBodyContent` side.
- **9** — DI registers `ISerde` from `CreateDefaultOptions(context)`; `ValidateOnStart` keeps one `ISerde` per client.
- **10** — the AOT smoke consumer's completion keeps `CheckPhase7aSerdeAsync`.
- **12** — `PublicAPI.Shipped.txt` moves.
- **Unscheduled** — a Newtonsoft adapter implements Tristate through `ITristate`/`ITristateVisitor` and pins
  `TypeNameHandling.None` (§3.4).

---

## Design, roadmap and `CLAUDE.md` corrections owed at close-out

Proposals only; 7a's close-out PR applies them as dated corrections.

- **§7.3:** the lazy wrapper is a CAS on a `TaskCompletionSource<T>`, not `Lazy<Task<T>>` (P7a-15, fact 11); the root-`null`
  route is `ReadValueOrDefault(Async)` (P7a-9); "only on the `net10.0` target; the .NET 8 shared framework" is moot under D1;
  the As-built line becomes "built".
- **§3.4:** `ISerde` has a sixth member, the sync stream decode with a default implementation (P7a-13); the materialisation
  cap stays a constant (P7a-21, correcting the 2026-10-07 sentence "a per-read limit that phase 7a adds").
- **§12:** the SERDE row cites the 7a checklist.
- **3b checklist:** `HTTP-44`/`HTTP-45` ⏳ 7a → ✅, pointing at the 7a checklist's carried rows.
- **3a checklist / 5a design:** the P5a-23 hand-off row names its closure (P7a-21).
- **Roadmap:** the Phase 7 card's 7a bullets get a dated status note; issue #1's closing comment names `SERDE-14`–`SERDE-20`
  and `SERDE-30`.
- **`CLAUDE.md`:** "What is genuinely unbuilt" drops "tri-state PATCH" and gains phase 7a's sentence pointing at
  `docs/sdk-documentation/serde.md`; the layout's `Serialization/` line lists `Tristate` and `ResponseHandlers`, and
  `Http/Response/` lists `TypedResponse`.

---

## Issue #1: the ruling comment

7a's design records the name ruling on #1 before building (roadmap card). No agent posts to GitHub (repository rule); the lead
posts this text:

> Phase 7a will close this issue, with one change to the proposal: the type is named **`Tristate<T>`**, not `Optional<T>`.
> Design §7.3 rules that .NET already circulates a two-state `Optional<T>` (Roslyn's `Microsoft.CodeAnalysis.Optional<T>`:
> `HasValue`, `Value`), so reusing the name for three states would contradict the ecosystem's vocabulary, and the
> specification's own word is `Tristate` (`SERDE-14`). Every other requirement here carries over unchanged: a trim- and
> AOT-safe `readonly struct Tristate<T> where T : notnull` whose `default` is Absent; Absent omitted on write through a
> `JsonTypeInfo` modifier and Null written as `null`, wired by default in `SystemTextJsonSerde` on a private copy of the
> caller's options; implicit conversion from `T` (a `null` becomes Null) and from `Tristate.Absent` / `Tristate.Null`;
> `Present`, `FromNullable`, `Match`, `TryGetValue`, `GetValueOrDefault` and the `Is*` predicates; pattern matching on
> `State` or the predicates. RFC 7386 merge-patch documents stay out of scope. Design:
> `docs/work/mvp/phase7/phase7a/2026-10-09-phase7a-serde-design.md`.

---

## Exit criteria

From the card (7a's share) and the universal list:

1. `Tristate<T>` in core, wired into STJ by the converter factory and the modifier; issue #1 closed by 7a's pull request with
   a comment naming the checklist rows that cover it (`SERDE-14`–`SERDE-20`, `SERDE-30`).
2. `SERDE-9`/`SERDE-10` rows cite 2b's `SerdeException` hierarchy; root-`null` rejection names `T`; `CreateDefaultOptions()`
   never mutates a caller's options (`SERDE-26`) and returns a fresh instance (`SERDE-25`); source-generated `JsonTypeInfo<T>`
   remains the only trim-safe path (no reflection fallback is added); the lazy typed-response wrapper is built (`HTTP-44`,
   `HTTP-45`).
3. The NativeAOT smoke consumer performs the JSON and **Tristate PATCH** round trip on Linux CI (7a's share of the phase's
   "JSON, Tristate, paged and SSE round trip"); the `IL2067` suppression is exercised by `Tristate<int>`.
4. A checklist exists with 30 `SERDE` rows plus the 2 carried `HTTP` rows, no blank cells.
5. CI green on every matrix row, including `--filter-trait "Category=Security"`, coverage ≥ 80 %, the dependency audit (no new
   `PackageReference` anywhere), reproducible pack, and the AOT smoke.
6. The housekeeping probe is clean for 7a's files; `docs/sdk-documentation/serde.md` opens "As built by phase 7a … written
   against source on <date>"; a `CHANGELOG.md` `[Unreleased]` entry; a dated status note in the roadmap.

---

## Risks and open questions (resolved)

| # | Risk or question | Resolution |
|---|---|---|
| R1 | Source-gen fast path ignores `ShouldSerialize`, so Absent would serialize as `null` | Fact 8, confirmed in plan step 0. If false: the modifier also clears the fast path by setting a property-level customisation STJ treats as "customised", or the default-mode context test fails and the doc tells callers to use `GenerationMode = Metadata`. The PATCH omission test runs against a default-mode context precisely to catch it. |
| R2 | `GetUninitializedObject` on a value-type `Tristate<T>` fails under NativeAOT when the type was never boxed statically | Design-verified on 10.0.401; the AOT smoke's `Tristate<int>` is the regression guard. |
| R3 | `Pageable` (7c, parallel) breaks on `ReadValueAsync`'s new return type | Fact 10; if it does not compile, the fix is the one-line deletion 7c is handed anyway, coordinated in whichever PR lands second. |
| R4 | `RespectNullableAnnotations` in the defaults turns a server's unexpected member `null` into a failure | Intended (§7.3): the model said non-null. Callers whose server is loose declare the member nullable. Documented in `serde.md`. |
| R5 | `ShouldSerialize` boxes each Tristate value per write | Accepted: PATCH payloads are small; a typed predicate would need `JsonPropertyInfo<T>` internals. |
| R6 | A caller replaces `TypeInfoResolver` after `AddTristateSupport`, dropping the modifier | The serde's constructor always re-wraps on its copy; `AddTristateSupport`'s doc says "call after setting the resolver". |
| R7 | Merge conflicts with 7b/7c in shared files | PR segmentation hotspots above. |
| Q1 | Does `SERDE-13` bind `ISerde`'s `T?` members? | No: they declare `T?`. **Open for the lead** (P7a-9). |
| Q2 | Does a zero-byte 200 count as "missing body"? | Yes, as Ruby (P7a-10). |
| Q3 | Should `ReadValueOrDefault` return `default` on a missing body? | No: it admits a wire `null`, not an absent payload (P7a-10). |
| Q4 | Sync typed response? | No (P7a-15): sync-over-async. Sync callers use `ReadValue<T>`. |
| Q5 | Does the serde force `Strict` on a caller's options? | No (P7a-8); only the default factory is strict. |
| Q6 | Opt-out of Tristate wiring? | No (P7a-5). |
| Q7 | Tristate in a dictionary value? | Degrades to `null` (`SERDE-20`), tested. |

---

## Rulings

1. **P7a-1 — `Tristate<T>`, not `Optional<T>`** (§7.3's ruling, recorded on #1). Options: keep #1's name (rejected, P14).
2. **P7a-2 — Implicit conversion from `T` is `FromNullable`** (null → Null). Options: throwing `Present` semantics (rejected:
   implicit conversions must not throw).
3. **P7a-3 — A hand-written `readonly struct`, not a record struct**, with stable `ToString`.
4. **P7a-4 — One public wiring entry point, `AddTristateSupport`**; factory and modifier internal.
5. **P7a-5 — Tristate wiring always on; no opt-out.** A reading of `SERDE-19`'s MAY.
6. **P7a-6 — Interface-dispatch converter factory over a public `ITristate`/`ITristateVisitor<TResult>` hook**, one
   justified `IL2067` suppression. Options: `MakeGenericType` (rejected, `IL3050`); `InternalsVisibleTo` (barred).
7. **P7a-7 — `Tristate.Absent`/`Tristate.Null` return `TristateSentinel`.**
8. **P7a-8 — `CreateDefaultOptions(IJsonTypeInfoResolver)`: `Web` naming, `Strict`, `RespectNullableAnnotations`, wired;
   caller options are not made strict.**
9. **P7a-9 — Root-`null` rejection in the readers and handlers; `ReadValueOrDefault(Async)` is the opt-in; `ISerde` keeps
   `T?`.** Open for the lead (Q1).
10. **P7a-10 — Missing body by a one-byte peek through `PrefixedReadStream`; zero bytes is missing.**
11. **P7a-11 — The typed readers dispose the body on every path** (3b's hand-off). Breaking.
12. **P7a-12 — The failure model's kinds are unchanged**; the readers' own failures carry no inner exception.
13. **P7a-13 — `ISerde.Deserialize<T>(Stream)` as a default interface member; sync `ReadValue`/`GetError`** (3a's hand-off).
14. **P7a-14 — `TypedResponse<T>`; the wrapped `Response` is not exposed.**
15. **P7a-15 — Exactly-once by CAS on a `TaskCompletionSource<T>`; the construction token drives the parse; a caller token
    cancels only its wait.** Overturns §7.3's `Lazy<Task<T>>` (fact 11); a dated correction is owed.
16. **P7a-16 — `IResponseHandler<T>` SPI + `ResponseHandlers` factory; async-only.**
17. **P7a-17 — `Deserialize<T>` handler = reader + response dispose through `Disposal`.**
18. **P7a-18 — `DeserializeOnSuccess<T>` reuses `ErrorBodyBuffer` + `ErrorMapping` for 4xx/5xx.**
19. **P7a-19 — Third-branch message leads with the code; `ETag` raw; `Location` resolved and redacted.** New `Security` test.
20. **P7a-20 — No new test package**; the round-trip property test is a seeded loop. (Superseded if 7b adds FsCheck first; then
    the loop may move to it without changing its assertions.)
21. **P7a-21 — The per-read materialisation limit (P5a-23) is declined.** Open for the lead.
22. **P7a-22 — `DeepValue` stays internal** (P5a-20's call).
23. **P7a-23 — `Location`/`ETag` are read from the response headers raw, never through `HTTP-48`'s validating helpers.**
24. **P7a-24 — Ruby's mandated round-trip property test is ported as a seeded deterministic loop** (seed logged).
25. **P7a-25 — `HTTP-44`/`HTTP-45` are carried in 7a's checklist as two extra rows** (32 rows), outside the roadmap's 107.

---

## Deviation Ledger

| ID | Ruling | Touches | Kind | Status | Recorded in |
|---|---|---|---|---|---|
| P7a-1 | `Tristate<T>` name | #1 | ruling carried from §7.3 | settled | issue #1 comment; checklist |
| P7a-5 | no opt-out of wiring | `SERDE-19` (MAY) | reading | settled | checklist |
| P7a-6 | public `ITristate` hook, `IL2067` suppression | `NFR-9` | judgement | settled | source justification; checklist |
| P7a-9 | root-`null` in readers only | `SERDE-13` | reading | **open** | checklist; §7.3 correction |
| P7a-13 | DIM sync stream decode | `SEAM-20`, `SERDE-27` | judgement | settled | §3.4 correction |
| P7a-15 | CAS instead of `Lazy<Task<T>>` | `HTTP-45` | design correction | settled | §7.3 correction |
| P7a-21 | per-read cap declined | P5a-23, `IO-9` | ownership judgement | **open** | §3.4 correction; 3a checklist |
| — | covariance unmet | `SERDE-14` | §10 entry 21 (existing) | settled | checklist cites entry 21 |
