# Phase 2a — Domain Model: Implementation Plan

**Status:** Draft, for review. Written 2026-09-30 against `main` at `bf8f4ae`, on branch
`36-phase-2a-domain-model-design`. GitHub issue [#36](https://github.com/dexpace/dotnet-sdk/issues/36).
Design: [phase 2a domain-model design](2026-09-29-phase2a-domain-model-design.md), the
authority for every decision below (the plan cites its row, position or ledger entry rather than restating it).
Scope authority: the [phase 2 segmentation design](../2026-09-29-phase2-segmentation-design.md).
Roadmap: [v1 roadmap](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md), phase 2 card.

**What this document is.** The roadmap's step 3 for sub-phase 2a: the numbered TDD tasks, in the design's landing
order (nine pull-request-sized steps, `RequestOptions` first), each with its failing tests, its production change,
its `PublicAPI.Unshipped.txt` diff, its **Breaking** markings, its `CHANGELOG.md` entry, its requirement IDs and its
verification commands. It is not the checklist (step 4, written from what was built) and it writes no production
code.

**Scope.** 42 rows: `HTTP-1`–`HTTP-35`, `HTTP-46`–`HTTP-50`, `HTTP-53`, `SEAM-29`. Every row maps to a task in the
[traceability table](#traceability-id--pr--task).

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason
   (a compile error counts, and is the expected red for a new type); make the production change; run them green;
   then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so.
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and a curated
   `GlobalUsings.cs` set only (`ImplicitUsings` is off; every other namespace is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await` (`CA2007`), methods at most 70 lines (`MA0051`),
   `///` XML docs on every public member (CS1591), no new `PackageReference` in `Dexpace.Sdk.Core` (constraint 2;
   `System.Collections.Immutable`, `System.Collections.Frozen` and `System.Text.Ascii` are in the shared framework).
4. **Tests**: `[Trait("Category", …)]` on every class (`TestCategoryTests` enforces it). Test classes live in
   `tests/Dexpace.Sdk.Core.Tests/Http/{Common,Request,Response}/`, mirroring `src/`; the architecture tests go in
   `tests/Dexpace.Sdk.Core.Tests/Architecture/`. `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only,
   never a transport (SEAM-2). Wire tests go in `tests/Dexpace.Sdk.Http.SystemNet.Tests/`.
5. **Security tests are never deleted or loosened.** The only `Security` assertion whose value changes in 2a is the
   position-C one (committed with task 5.3, reviewed in task 5.7). Every other edit to a `Security` file is a mechanical compile fix, listed in the task
   that makes it, and recorded in the checklist's "existing assertions changed" table.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed or removed member is an *edit or
   deletion of its line in `Unshipped`*, never a `*REMOVED*` marker. The lines listed per task are the expected
   hand-authored diff; the build's `RS0016`/`RS0017` output is the authority, and it also lists the members the
   compiler synthesises for a record (`<Clone>$`, `operator ==`/`!=`, `Equals(T?)`, `GetHashCode`, `ToString`),
   which the task does not spell out. Namespace aliases in the diff blocks: `[C]` = `Dexpace.Sdk.Core.Http.Common`,
   `[Q]` = `Dexpace.Sdk.Core.Http.Request`, `[S]` = `Dexpace.Sdk.Core.Http.Response`; expand them when pasting.
7. **XML docs mark a breaking change** with a `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it
   changes, stating what it was. Each PR's last task adds the matching `CHANGELOG.md` `[Unreleased]` line, prefixed
   **Breaking:** where design "Breaking changes" lists it (constraint 8).
8. **One PR per step, code and tests together.** Splitting them would leave a red commit per breaking change, which is
   why roadmap step 5's one-PR allowance applies and the design's landing order says so. Every commit inside a PR is
   green too: where a change turns an existing assertion red, the assertion is updated in the same commit.
   Commit style `feat:` / `fix:` / `chore:` / `test:` / `docs:`; no AI attribution anywhere.

### Environment

```bash
export DOTNET_ROOT=/tmp/claude-1000/-home-mohammad-Projects-dotnet-sdk/036600bc-088e-406d-b30d-fae2f01ee087/scratchpad/dotnet
export PATH=$DOTNET_ROOT:$PATH
```

### Verification blocks

**V-fast** (inner loop, one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-gate** (the last task of every PR; the whole of CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages   # after `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages`
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The AOT smoke runs in every PR (each changes public surface). The coverage gate
(`dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80`, after a clean coverlet run) is run before the
push of PR 9, and of any PR whose diff removes tests.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info HTTP
scripts/knowledge --gaps HTTP,SEAM          # comma form; the space form drops prefixes (#31)
scripts/knowledge --req HTTP-46             # per row in scope of the PR
```

---

## Rulings

The design left four judgement calls open (its "Rulings" section). The lead ruled on 2026-09-30, and every design
default stands. Each is marked `RULED` at the task it affects.

| Ruling | Outcome | Task |
|---|---|---|
| **P2a-2** URL errors carry the input passed through `UrlRedactor` (`HTTP-47`) | Redacted, with a design §10 entry | 6.1 (tests, messages), 9.6 (ledger) |
| **`Request.ToString()`** | Overridden to method + redacted URL | 6.4 |
| **P2a-3** an obs-text `ETag` is valid under `HTTP-48` but unsendable through `RequestConditions.ApplyTo` (`HTTP-18` wins) | Recorded as a numbered design §11 item | 8.3 (test pin), 9.6 (appendix entry) |
| **`Method.Of` case folding** (`HTTP-9`) | Keep the as-built fold: `get` → `Method.Get` (ASCII case-insensitive match on the nine verbs; design §4.3). 4.1 rewords `Method`'s class remark to state the rule | 4.1 |

---

## PR 1 — `RequestOptions`

The first step, and the only hard gate of 2b (its SPI signature carries the type). Purely additive.

### Task 1.1 — `RequestOptions` shape and validation (`HTTP-34`, `HTTP-35`)

**Failing tests first.** New file `tests/Dexpace.Sdk.Core.Tests/Http/Request/RequestOptionsTests.cs`, class
`RequestOptionsTests`, `[Trait("Category", "Unit")]`:

- `Empty_overrides_nothing_and_has_no_tags` (`Timeout` null, `MaxRetries` null, `Tags` empty and non-null)
- `A_new_instance_equals_Empty`
- `Timeout_that_is_not_positive_is_rejected` (`[Theory]`: `TimeSpan.Zero`, `TimeSpan.FromTicks(-1)`,
  `Timeout.InfiniteTimeSpan`; each through the object initializer **and** through `with`; asserts
  `ArgumentOutOfRangeException`; assert the type only)
- `A_positive_or_null_timeout_is_accepted` (1 tick, `TimeSpan.MaxValue`, `null`; pins that the upper bound is 8b's
  `TRANSPORT-5`, not a model rule)
- `MaxRetries_below_zero_is_rejected` (−1 via initializer and `with`) and `MaxRetries_of_zero_or_null_is_accepted`
- `Tags_default_is_never_null` and `Assigning_null_to_Tags_throws_ArgumentNullException`

Red: CS0246, `RequestOptions` does not exist.

**Production.** New `src/Dexpace.Sdk.Core/Http/Request/RequestOptions.cs`:

```csharp
public sealed record RequestOptions
{
    public static RequestOptions Empty { get; }
    public TimeSpan? Timeout { get; init; }                        // init => field = RequireNullOrPositive(value)
    public int? MaxRetries { get; init; }                          // init => field = RequireNullOrNonNegative(value)
    public ImmutableDictionary<string, string> Tags { get; init; } // never null; see 1.2
}
```

Validation in `field`-backed `init` accessors so `with` cannot bypass it (design §4; `field` is available at
`LangVersion latest` on the pinned SDK). Private static helpers throw `ArgumentOutOfRangeException` with a message
that names the rule, not a value beyond the number the caller passed. `Empty` is a `static readonly` instance.

**`PublicAPI.Unshipped.txt`** (`src/Dexpace.Sdk.Core`; sorted into place):

```text
[Q].RequestOptions
[Q].RequestOptions.<Clone>$() -> [Q].RequestOptions!
[Q].RequestOptions.Equals([Q].RequestOptions? other) -> bool
[Q].RequestOptions.MaxRetries.get -> int?
[Q].RequestOptions.MaxRetries.init -> void
[Q].RequestOptions.RequestOptions() -> void
[Q].RequestOptions.Timeout.get -> System.TimeSpan?
[Q].RequestOptions.Timeout.init -> void
[Q].RequestOptions.Tags.get -> System.Collections.Immutable.ImmutableDictionary<string!, string!>!
[Q].RequestOptions.Tags.init -> void
static [Q].RequestOptions.Empty.get -> [Q].RequestOptions!
static [Q].RequestOptions.operator !=([Q].RequestOptions? left, [Q].RequestOptions? right) -> bool
static [Q].RequestOptions.operator ==([Q].RequestOptions? left, [Q].RequestOptions? right) -> bool
override [Q].RequestOptions.GetHashCode() -> int
override [Q].RequestOptions.ToString() -> string!
```

**Docs.** No Breaking marker (additive). XML docs state "null means: do not override".
**IDs:** `HTTP-34` (shape), `HTTP-35`. **Verify:** V-fast `RequestOptionsTests`.

### Task 1.2 — `Tags` normalisation, content equality, `WithTag` (`HTTP-34`, `HTTP-3`)

**Failing tests first**, same class (append):

- `Tags_built_from_an_ImmutableDictionary_builder_are_unaffected_by_later_builder_edits`
- `Tag_keys_are_ordinal` (assign a dictionary made with `StringComparer.OrdinalIgnoreCase`; `Tags["A"]` and `["a"]`
  are distinct after assignment: keys are re-based on `StringComparer.Ordinal`)
- `Equality_compares_tags_by_content_and_hashes_agree` (two separately built dictionaries with equal content)
- `Different_timeouts_retries_or_tags_are_unequal`
- `WithTag_adds_and_replaces_without_mutating_the_original` (`HTTP-3`: derivation never aliases)
- `WithTag_validates_the_key_and_value_are_not_null`

**Production.** `Tags` initializer defaults to `ImmutableDictionary<string,string>.Empty.WithComparers(StringComparer.Ordinal)`;
its `init` re-bases any assigned dictionary on ordinal keys (`value.WithComparers(StringComparer.Ordinal)` if the
comparer differs) and rejects `null`. Explicit `Equals(RequestOptions?)` compares `Timeout`, `MaxRetries` and `Tags`
by content (count, then every key/value); `GetHashCode` combines the two scalars with an order-independent sum of
`HashCode.Combine(key, value)` over tags. `WithTag(string, string)` returns `this with { Tags = Tags.SetItem(k, v) }`.

**`PublicAPI.Unshipped.txt`:** `[Q].RequestOptions.WithTag(string! key, string! value) -> [Q].RequestOptions!`.
**IDs:** `HTTP-34`, `HTTP-3` (this type). **Verify:** V-fast `RequestOptionsTests`.

### Task 1.3 — Close-out

- `CHANGELOG.md` `[Unreleased]` → `### Added`: "`RequestOptions` — per-call `Timeout`, `MaxRetries` and `Tags`
  (`HTTP-34`, `HTTP-35`); the type the phase 2b transport SPI carries."
- Run **V-gate**. **Commit:** `feat: add RequestOptions (HTTP-34, HTTP-35)`.

---

## PR 2 — `Query`, `Rfc3986` and the vector infrastructure

Additive. Early, so that 2b's projection (`SEAM-27`) and 7c can share one RFC 3986 encoder.

### Task 2.1 — Vector loading infrastructure (roadmap constraint 10)

**Failing test first.** New `tests/Dexpace.Sdk.Core.Tests/Vectors/VectorFileTests.cs`, class `VectorFileTests`,
`[Trait("Category", "Unit")]`: `Every_vector_file_names_its_source_path_and_sha` (enumerates
`AppContext.BaseDirectory/vectors/**/*.json`, asserts a top-level `"source"` string containing `@` and a hex sha) and
`Load_returns_the_cases_array_of_a_file` (over `vectors/http/rfc3986.json`, created in 2.2 — write this test in 2.2 if
red ordering demands; here it fails because the loader does not exist).

**Production.**
- `tests/vectors/http/` directory (created by 2.2's first file).
- `tests/Dexpace.Sdk.TestSupport/Vectors/VectorFile.cs`: `public static class VectorFile` with
  `Load<TCase>(string relativePath)` reading `Path.Combine(AppContext.BaseDirectory, "vectors", relativePath)` and
  deserialising `{ "source": "...", "cases": [...] }` with `System.Text.Json` (tests are not AOT; reflection is fine).
- `tests/Dexpace.Sdk.Core.Tests/Dexpace.Sdk.Core.Tests.csproj`: 
  `<None Include="..\vectors\**\*.json" Link="vectors\%(RecursiveDir)%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />`.

`TestSupport` is not packed and not AOT-gated. No `packages.lock.json` change is expected (`System.Text.Json` is in the
framework); if `dotnet restore` reports one, run it on both solutions and commit the lock files.

**IDs:** infrastructure for `HTTP-23`, `HTTP-25`, `HTTP-29`–`HTTP-32`, `HTTP-48`, `HTTP-49`. **Verify:** V-fast
`VectorFileTests`.

### Task 2.2 — `Rfc3986` (`HTTP-32`)

**Failing tests first.** `tests/vectors/http/rfc3986.json` (source: `nodejs-sdk@54aeed4
packages/core/src/http/rfc3986.test.ts`, `ruby-sdk@5b17395 …/percent_encoding_test.rb`), and
`tests/Dexpace.Sdk.Core.Tests/Http/Request/Rfc3986Tests.cs`, class `Rfc3986Tests`, `[Trait("Category", "Unit")]`
(internal type, reached through `InternalsVisibleTo`):

- `EncodeComponent_matches_the_vectors` (`[Theory]` over the file: space → `%20`, `+` → `%2B`, `/` → `%2F`,
  `*` → `%2A`, `~` unencoded, `a b+c/d*e~f` → `a%20b%2Bc%2Fd%2Ae~f`, non-ASCII → UTF-8 percent bytes)
- `DecodeComponent_matches_the_vectors` (`+` stays `+`; `%2B` → `+`; a malformed escape such as `a%zzb+c%2` stays raw)
- `EncodeComponent_turns_a_lone_surrogate_into_the_replacement_bytes` (pin: documents *why* `Query.Builder` rejects
  lone surrogates upstream of the encoder, design `HTTP-32`)
- `DecodeComponent_leaves_an_escaped_invalid_utf8_sequence_raw` (pin: `%ED%A0%80`, an encoded lone surrogate, and
  `%FF` come back unchanged; verified 2026-09-30 against `Uri.UnescapeDataString`, design `HTTP-31`)

**Production.** `src/Dexpace.Sdk.Core/Http/Request/Rfc3986.cs`: `internal static class Rfc3986` with
`EncodeComponent(string)` (`Uri.EscapeDataString`) and `DecodeComponent(string)` (`Uri.UnescapeDataString`, lenient).
**PublicAPI:** none (internal). **Docs:** internal remarks cite design §3.5. **IDs:** `HTTP-32`.
**Verify:** V-fast `Rfc3986Tests`.

### Task 2.3 — `Query` and `Query.Builder`: reads, `Encode`, immutability (`HTTP-28`, `HTTP-29`, `HTTP-5`, `HTTP-3`)

**Failing tests first.** `tests/vectors/http/query.json` (source as 2.2, `query-params.test.ts`, `query_test.rb`) and
`tests/Dexpace.Sdk.Core.Tests/Http/Request/QueryTests.cs`, class `QueryTests`, `[Trait("Category", "Unit")]`:

- `Names_are_case_sensitive` (`page` vs `Page`) ; `Insertion_order_is_first_appearance`; `Multiple_values_per_name_are_kept_in_order`
- `Add_null_is_stored_as_the_empty_string` (`Get` returns `""`, `Contains` is true);
  `Set_null_stores_a_single_empty_string_and_does_not_remove` (design "Type shapes": unlike `Headers.Set`);
  `Get_of_an_absent_name_is_null`;
  `GetAll_of_an_absent_name_is_empty`
- `Encode_matches_the_vectors` (`{q:["a b"], plus:["c+d"]}` → `q=a%20b&plus=c%2Bd`; a repeated name is emitted once
  per value; no leading `?`; empty query gives `""`)
- `Builder_rejects_an_empty_name`, `Builder_rejects_a_lone_surrogate_in_a_name_or_value` (`ArgumentException`; message
  names the position or `U+D800`, never echoes the text)
- `GetAll_result_is_read_only` (`HTTP-5` pin: cast to `IList<string>`; `Add` throws `NotSupportedException`; the value
  is not a `List<string>`)
- `A_built_query_is_unaffected_by_later_builder_edits` and `ToBuilder_copies_into_a_fresh_builder` (`HTTP-3`, `HTTP-5`)
- `Query_is_not_a_member_of_Request` is *not* written here (it lives in 6.1's property-reflection test)

Red: CS0246, no `Query`.

**Production.** `src/Dexpace.Sdk.Core/Http/Request/Query.cs` (`sealed class Query`, `IEnumerable<KeyValuePair<string,
IReadOnlyList<string>>>`, `IEquatable<Query>`) and nested `Query.Builder`, per design "Type shapes". Storage: grouped
entries (`ImmutableArray` of `(string Name, ImmutableArray<string> Values)`) plus an ordinal
`Dictionary<string,int>` index built once and never exposed; `Names` an `ImmutableArray<string>` built with the
instance. `Encode()` uses `Rfc3986.EncodeComponent` for each name and value. Equality is added in 2.4; until then the
`Equals`/`==` members exist as stubs that are not part of this task's tests (write the whole class body in 2.3/2.4 in
one commit if the analyzers object to the stubs).

**PublicAPI** (full set for `Query`, given here once; 2.4 and 2.5 add no new lines except `Parse`/`Set` overload):

```text
[Q].Query
[Q].Query.Builder
[Q].Query.Builder.Add(string! name, string? value) -> [Q].Query.Builder!
[Q].Query.Builder.Build() -> [Q].Query!
[Q].Query.Builder.Builder() -> void
[Q].Query.Builder.Remove(string! name) -> [Q].Query.Builder!
[Q].Query.Builder.Set(string! name, string? value) -> [Q].Query.Builder!
[Q].Query.Builder.Set(string! name, System.Collections.Generic.IEnumerable<string?>! values) -> [Q].Query.Builder!
[Q].Query.Contains(string! name) -> bool
[Q].Query.Count.get -> int
[Q].Query.Encode() -> string!
[Q].Query.Equals([Q].Query? other) -> bool
[Q].Query.Get(string! name) -> string?
[Q].Query.GetAll(string! name) -> System.Collections.Generic.IReadOnlyList<string!>!
[Q].Query.GetEnumerator() -> System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string!, System.Collections.Generic.IReadOnlyList<string!>!>>!
[Q].Query.Names.get -> System.Collections.Generic.IReadOnlyList<string!>!
[Q].Query.ToBuilder() -> [Q].Query.Builder!
override [Q].Query.Equals(object? obj) -> bool
override [Q].Query.GetHashCode() -> int
override [Q].Query.ToString() -> string!
static [Q].Query.Empty.get -> [Q].Query!
static [Q].Query.Parse(string? query) -> [Q].Query!
static [Q].Query.operator !=([Q].Query? left, [Q].Query? right) -> bool
static [Q].Query.operator ==([Q].Query? left, [Q].Query? right) -> bool
```

**Docs.** `ToString` remarks: names only, because values may be secrets and `Encode()` is the explicit form. No
Breaking marker. **IDs:** `HTTP-28`, `HTTP-29`, `HTTP-5` (Query half), `HTTP-3` (Query half).
**Verify:** V-fast `QueryTests`.

### Task 2.4 — `Query` equality and the `Set(name, [])` rule (`HTTP-30`)

**Failing tests first**, `QueryTests` (append):

- `Equality_holds_exactly_when_Encode_is_equal` (`[Theory]` over pairs, including reordered names, reordered values,
  differing case, `?flag` vs `flag=`; asserts `a.Equals(b) == (a.Encode() == b.Encode())` and hash equality when equal)
- `Equality_is_order_sensitive_across_names`
- `Set_with_an_empty_sequence_drops_the_name_at_Build` (phantom-entry test: `Contains("x")` false, `Count` 0)
- `A_query_works_as_a_Dictionary_key`

**Production.** `Equals(Query?)`, `GetHashCode`, operators; `Builder.Set(name, IEnumerable<string?>)` empty sequence
removes; `Build` omits names with no values. **IDs:** `HTTP-30`. **Verify:** V-fast `QueryTests`.

### Task 2.5 — `Query.Parse` (`HTTP-31`)

**Failing tests first**, `QueryParseTests` (new class in `Http/Request/QueryParseTests.cs`, `Unit`):

- `Parse_matches_the_vectors` (`query.json` parse cases; `null`/blank → `Empty`; leading `?` stripped; `a` and `a=`
  both give `""`; stray `&` and empty-name segments skipped; malformed escape stays raw; `+` stays `+`)
- `Parse_of_Encode_round_trips_for_every_vector` (`Parse(q.Encode()) == q`)
- `An_escaped_invalid_utf8_sequence_stays_raw` (`a=%ED%A0%80` gives the value `%ED%A0%80`; `Parse` never throws)
- `A_literal_lone_surrogate_in_the_input_becomes_U_FFFD` (`"a=\uD800"` gives `\uFFFD`, so `Parse("a=\uD800")` equals
  `Parse("a=\uFFFD")`, as their identical `Encode()` output requires under `HTTP-30`)
- `Parse_never_throws_on_arbitrary_input` (small fixed fuzz corpus incl. `"%"`, `"%%"`, `"=&="`, 4 KiB of `&`)

**Production.** `Query.Parse(string?)` splitting on `&`, first `=`, decoding with `Rfc3986.DecodeComponent`, then
replacing any lone surrogate left in the decoded text (only a literal one can remain: an escaped one stays raw) with
U+FFFD, and skipping empty-name segments, so the result always satisfies the builder's rules.
**IDs:** `HTTP-31`. **Verify:** V-fast `QueryParseTests`.

### Task 2.6 — Close-out

`CHANGELOG.md` → `### Added`: "`Query` and `Query.Builder` — RFC 3986 query multimap with ordinal names, total lenient
`Parse` and deterministic `Encode` (`HTTP-28`–`HTTP-31`); internal `Rfc3986` component encoder (`HTTP-32`)."
Run **V-gate**. **Commit:** `feat: add Query and the RFC 3986 component encoder (HTTP-28..HTTP-32)`.

---

## PR 3 — `Status`, `Protocol`, `MediaType`

Independent of PRs 1 and 2; may be reviewed in parallel.

### Task 3.1 — `Status`: `IsError`, `TryGetKnown` (`HTTP-10`, `HTTP-11`, `HTTP-12`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Response/StatusTests.cs`, class `StatusTests`,
`[Trait("Category", "Unit")]`. Move the three `Status_*` tests out of `Http/Common/MethodAndStatusTests.cs`
(`Status_FromCode_ResolvesKnownAndUnknown`, `Status_RangeHelpers`, `Status_EqualityIsByCode`); these are `Unit`, so the
move is not a constraint-5 matter. Names in the new class:

- `FromCode_maps_200_to_the_named_Ok_and_599_799_minus1_to_nameless_values_without_throwing`
- `TryGetKnown_is_true_for_200_and_false_for_599` (out value default when false)
- `Classification_by_boundary_code` (`[Theory]`: 99, 100, 199, 200, 299, 300, 399, 400, 499, 500, 599, 600 against
  `IsInformational`, `IsSuccess`, `IsRedirect`, `IsClientError`, `IsServerError`, `IsError`)
- `Equality_is_by_code_and_the_hash_agrees` (`HTTP-12` pin, with the added hash assertion)
- `default_Status_is_code_zero_and_unrecognised` (pin; design: `Status` stays a `readonly record struct`)

Red: CS1061 (`IsError`, `TryGetKnown`).

**Production.** `src/Dexpace.Sdk.Core/Http/Response/Status.cs`: `public bool IsError => Code is >= 400 and <= 599;`
and `public static bool TryGetKnown(int code, out Status status)`; `FromCode` stays total.

**PublicAPI:**

```text
[S].Status.IsError.get -> bool
static [S].Status.TryGetKnown(int code, out [S].Status status) -> bool
```

**IDs:** `HTTP-10`, `HTTP-11` (Status half), `HTTP-12`. **Verify:** V-fast `StatusTests`.

### Task 3.2 — `Protocol.Parse` ASCII fold (`HTTP-33`)

**Failing tests first.** New `tests/…/Http/Common/ProtocolTests.cs`, class `ProtocolTests`, `Unit`; move
`Protocol_ParseAndWireRoundTrip` here and widen it:

- `Parse_accepts_HTTP_2_HTTP_2_0_and_mixed_case` (`[Theory]`)
- `Parse_is_culture_invariant_under_tr_TR` (set `CultureInfo.CurrentCulture` to `tr-TR` inside the test and restore it
  in `finally`; `"HTTP/1.1"` and `"http/1.1"` both parse; `"HTTPİ"`-style input does not)
- `Parse_rejects_http_3_and_unknown_text` (`ArgumentException`, as today)
- `Wire_string_round_trips`

Pin-or-red: the `tr-TR` case is the one that can fail today (`ToUpperInvariant` is invariant already, so it is likely
a pin; state the observed result in the PR). **Production.** `ProtocolExtensions.Parse` compares with
`System.Text.Ascii.EqualsIgnoreCase` against each wire token. **PublicAPI:** none. **IDs:** `HTTP-33`.
**Verify:** V-fast `ProtocolTests`.

### Task 3.3 — `MediaType` vectors and pins (`HTTP-23`, `HTTP-25`, `HTTP-26`, `HTTP-27`)

**Failing tests first.** `tests/vectors/http/media-type.json` (source: `nodejs-sdk@54aeed4 media-type.test.ts`,
`ruby-sdk@5b17395 media_type_test.rb`), extracted from the table in `Http/Common/MediaTypeTests.cs` plus the ported
rows. In `tests/…/Http/Common/MediaTypeTests.cs` (existing class) add, all `Unit` pins that pass today:

- `Parse_case_table_matches_the_vectors` (`Application/JSON;Charset=UTF-8` → `application/json`, charset `UTF-8`)
- `Round_trip_table_matches_the_vectors` (boundaries holding `;` and `=`, quoted values)
- `Of_rejects_a_wildcard_subtype_under_a_concrete_type` (`Of("*", "json")` throws)
- `Includes_honours_receiver_wildcards_and_ignores_parameters` (`text/*` includes `text/plain`, not the reverse; `*/*`
  includes everything)
- `A_media_type_that_is_not_header_safe_is_rejected` (`HTTP-26`) is **cited, not rewritten**: the Security class
  `HeaderInjectionValidationTests` already holds it; the task adds a comment cross-reference only, no edit to the
  Security file.

**Production.** None expected (pins). If a vector row fails, the fix is in `MediaType.cs` and is stated in the PR.
**IDs:** `HTTP-23`, `HTTP-25`, `HTTP-26` (citation), `HTTP-27`. **Verify:** V-fast `MediaTypeTests`;
`--filter-class "*HeaderInjectionValidationTests"` stays green.

### Task 3.4 — `MediaType.Charset` and `utf-7` (`HTTP-24`)

**Failing tests first**, `MediaTypeTests` (append): `Charset_resolves_utf8_in_any_case_and_key_case`
(`utf-8`, `UTF-8`, `Charset=`), `Charset_is_null_for_bogus_utf_7_and_absent` (`utf-7` is the red one:
`Encoding.GetEncoding("utf-7")` throws `NotSupportedException`, which escapes today).

**Production.** `MediaType.Charset` catches `NotSupportedException` alongside `ArgumentException`.
**Docs:** none Breaking-marked in XML beyond the remark "Breaking (behaviour): returned `null` instead of throwing for
`utf-7`". **IDs:** `HTTP-24`. **Verify:** V-fast `MediaTypeTests`.

### Task 3.5 — `MediaType.Parse` rejects an empty parameter value (`HTTP-53`)

**Failing tests first**, `MediaTypeTests` (append): `Parse_rejects_a_parameter_with_an_empty_raw_value` (`a=`),
`Parse_accepts_a_quoted_empty_value` (`a=""`), `Parse_still_rejects_blank_text_slash_json_and_text_slash` and
`An_empty_segment_after_a_semicolon_is_still_skipped` (`text/plain;` accepted; pin). Then re-run, unedited,
`MediaTypeTryParseTests` and `MalformedContentTypeWireTests` (**Security**; no accepted row has an empty value, per
the design's verification; if one fails, stop and report, do not edit the row).

**Production.** `MediaType.Parse` rejects a segment with `=` and an empty raw value.
**Docs:** "Breaking (behaviour)" remark on `Parse`. **IDs:** `HTTP-53`.
**Verify:** V-fast `MediaTypeTests`; Security filter on both projects.

### Task 3.6 — Close-out

`CHANGELOG.md`: `### Added` "`Status.IsError`, `Status.TryGetKnown` (`HTTP-10`, `HTTP-11`)"; `### Changed`
"**Breaking:** `MediaType.Parse` rejects a parameter with an empty raw value (`a=`); `MediaType.Charset` returns `null`
for `utf-7` instead of throwing (`HTTP-24`, `HTTP-53`). `Protocol.Parse` folds case with ASCII rules (`HTTP-33`)."
Run **V-gate**. **Commit:** `feat: Status.IsError/TryGetKnown and MediaType parsing rules (HTTP-10..12, 23..27, 33, 53)`.

---

## PR 4 — `Method` as a `sealed record`, `RetryFacts`

Breaking items 1–4.

### Task 4.1 — `Method` becomes a `sealed record` (`HTTP-9`, `HTTP-1`)

**Failing tests first.** `git mv tests/…/Http/Common/MethodAndStatusTests.cs tests/…/Http/Common/MethodTests.cs`
(after 3.1 only Method tests remain), class `MethodTests`, `[Trait("Category", "Unit")]`:

- `Well_known_tokens_equal_their_upper_case_names_and_resolve_to_cached_instances` (`Same(Method.Get, Method.Of("GET"))`)
- `Of_folds_case_for_the_nine_well_known_verbs_and_keeps_other_tokens_verbatim` (`get` → `Method.Get`; `Foo` stays `Foo`)
- `Of_trims_SP_and_HTAB_only`
- `Of_rejects_a_non_token_without_echoing_it` (`"GET\r\nX: y"`, `"FOO BAR"`, `""`, whitespace; `ArgumentException`; the
  message contains neither the CR nor the text)
- `ForbidsBody_is_true_for_exactly_GET_HEAD_TRACE_CONNECT` (internal member)
- `Method_has_no_public_bool_property` (reflection: `IsSafe`/`IsIdempotent` gone, `HTTP-9`)
- `Method_is_a_reference_type_and_never_default` (`typeof(Method).IsClass`)
- update `Method_Of_NormalisesKnownVerbs` and `Method_Of_PreservesUnknownVerb` to the new class as-is
- rewrite `Method_SafetyAndIdempotency` (a `Unit` test that reads the public `IsSafe`/`IsIdempotent` this task deletes,
  so it stops compiling here): drop its `IsSafe` lines and assert the idempotent set through the internal
  `IsIdempotent`, per the design's migration table

**Production.** `src/Dexpace.Sdk.Core/Http/Common/Method.cs` → `public sealed record Method` with a private
constructor, `Name`, the nine statics, `Of` (trim SP/HTAB; RFC 9110 token check through the internal `HeaderSyntax`
token predicate, message names the code point, never the input; nine-verb fold via `Ascii.EqualsIgnoreCase`),
`override ToString() => Name`, `internal bool ForbidsBody`. `IsSafe` and the public `IsIdempotent` are deleted.
`internal bool IsIdempotent` is added in 4.2 (same PR; the intermediate build keeps `RetryPolicy` compiling by adding
it in this task as `Name is "GET" or …` and re-pointing it in 4.2).

> **RULED (`Method.Of` case folding, 2026-09-30).** Design §4.3 and the as-built code fold the nine verbs
> case-insensitively (`get` → `GET`), while the class remark cites HTTP's case sensitivity. The fold stays (no
> behaviour change), and this task rewords the remark to state it.

**PublicAPI.** Edit in place: `[C].Method` gains `<Clone>$`, `Equals([C].Method? other)`, `operator ==`/`!=`,
`GetHashCode`, and every `[C].Method` member loses its struct form (`Method()` default constructor line is
**deleted**). Delete:

```text
[C].Method.IsIdempotent.get -> bool
[C].Method.IsSafe.get -> bool
[C].Method.Method() -> void
```

Annotated reference lines (`-> [C].Method!`) replace every `-> [C].Method` in `Unshipped` (grep `Common.Method$`
and `Common.Method ` — the `Request.Method` property, `Request.Request(...)` constructor and the `Method.Get…Connect`
statics).

**Docs.** `Method` `<remarks>` **Breaking:** was a `readonly record struct`; `default(Method)` and `Nullable<Method>`
no longer exist; `Of` rejects non-tokens. **IDs:** `HTTP-9` (the `Of`/fold/removal half), `HTTP-1` (Method).
**Verify:** V-fast `MethodTests`; then whole-solution build (call sites use `Method` by value only; if the compiler
reports a `default` or `Method?` site, fix it in this task).

### Task 4.2 — `RetryFacts` and the TRACE behaviour (`HTTP-9`)

**Failing tests first.**
- New `tests/…/Pipeline/Policies/RetryFactsTests.cs`, class `RetryFactsTests`, `Unit`:
  `IdempotentMethods_is_exactly_GET_HEAD_OPTIONS_PUT_DELETE`, `Method_IsIdempotent_reads_the_set_for_all_nine_methods`
  (`[Theory]`, through `InternalsVisibleTo`), `A_vendor_method_is_not_idempotent`.
- `tests/…/Pipeline/Policies/RetryPolicyTests.cs` (existing class): `A_TRACE_request_is_not_retried` (503 then 200 with
  a replayable TRACE request: exactly one send; red today because TRACE is "safe"), and re-run the existing
  idempotent-retry tests unchanged.

**Production.** New `src/Dexpace.Sdk.Core/Pipeline/Policies/RetryFacts.cs`:
`internal static class RetryFacts { internal static FrozenSet<Method> IdempotentMethods { get; } }` (design §6.1;
6a grows this class); `Method.IsIdempotent` (internal) `=> RetryFacts.IdempotentMethods.Contains(this)`;
`RetryPolicy.cs:171` keeps the same source line. **PublicAPI:** none (internal).
**Docs:** XML remark on `RetryPolicy`'s replay gate: **Breaking (behaviour):** TRACE is no longer retried.
**IDs:** `HTTP-9`. **Verify:** V-fast `RetryFactsTests`, `--filter-class "*RetryPolicyTests"`.

### Task 4.3 — Close-out

`CHANGELOG.md` → `### Changed`:
"**Breaking:** `Method` is a `sealed record` (was a `readonly record struct`); `Method.IsSafe` and `Method.IsIdempotent`
are no longer public; `Method.Of` rejects a non-token with `ArgumentException`; `RetryPolicy` no longer retries TRACE
(`HTTP-9`)."
Run **V-gate**. **Commit:** `feat!: Method as a sealed record with token validation and one idempotency source (HTTP-9)`.

---

## PR 5 — `HttpHeaderName`, the `Headers` rebuild, `HttpHeaderSyntax`

Carries phase 1's S1 obligations (constraint 5); separately reviewable. Breaking items 5–6. The `Security` classes
`HeaderInjectionValidationTests` and `HeaderInjectionWireTests` are run after **every** task of this PR.

### Task 5.1 — `HttpHeaderSyntax`, the public predicates (position B; `HTTP-17`, `HTTP-18`, `HTTP-19`, `HTTP-20`)

**Failing tests first.** New `tests/…/Http/Common/HttpHeaderSyntaxTests.cs`, class `HttpHeaderSyntaxTests`, `Unit`:

- `IsValidName_accepts_tokens_and_rejects_empty_space_colon_and_controls` (no trimming: `" X"` is invalid; reuses the
  `InvalidNames` set from `HeaderInjectionValidationTests` through a shared `MemberData`, read-only)
- `IsValidOutboundValue_accepts_only_HTAB_and_0x20_to_0x7E` (rejects CR, LF, NUL, DEL, obs-text)
- `IsValidInboundValue_allows_obs_text_and_rejects_C0_but_HTAB_and_DEL`
- `EscapeName_renders_every_non_token_char_as_backslash_u_XXXX` (`"a b\r"` → `a\u0020b\u000D`: a space is not a
  `tchar` either; a valid token comes back unchanged)
- `The_predicates_agree_with_the_throwing_validators` (for every character 0..0xFF: predicate result equals "internal
  validator does not throw" — one character table, position B)

**Production.** New `src/Dexpace.Sdk.Core/Http/Common/HttpHeaderSyntax.cs` (`public static class`, four members from
position B); the internal `HeaderSyntax` keeps its throwing validators and delegates its character classes to the same
private helpers (`ReadOnlySpan<char>`, no allocation).

**PublicAPI:**

```text
[C].HttpHeaderSyntax
static [C].HttpHeaderSyntax.EscapeName(string! name) -> string!
static [C].HttpHeaderSyntax.IsValidInboundValue(System.ReadOnlySpan<char> value) -> bool
static [C].HttpHeaderSyntax.IsValidName(System.ReadOnlySpan<char> name) -> bool
static [C].HttpHeaderSyntax.IsValidOutboundValue(System.ReadOnlySpan<char> value) -> bool
```

**IDs:** `HTTP-17`, `HTTP-18`, `HTTP-19`, `HTTP-20` (public predicate). **Verify:** V-fast `HttpHeaderSyntaxTests`,
`*HeaderInjectionValidationTests`.

### Task 5.2 — `HttpHeaderName` as a `sealed record` (`HTTP-21`)

**Failing tests first.** New `tests/…/Http/Common/HttpHeaderNameTests.cs`, class `HttpHeaderNameTests`, `Unit`:

- `Equality_and_hash_ignore_casing` (`X-Trace`, `x-trace`, `X-TRACE`); `Original_keeps_the_trimmed_caller_spelling`;
  `CanonicalName_is_ASCII_folded` (including under `tr-TR`: `"TITLE"` → `"title"`)
- `Of_validates_like_the_headers_entry_points` (`[Theory]` over the shared `InvalidNames`; parity with the Security data)
- `ToString_returns_Original` (**Breaking**, was `CanonicalName`)
- `WellKnown_includes_IdempotencyKey_IfMatch_IfNoneMatch_IfModifiedSince_IfUnmodifiedSince_Range` (canonical names
  `idempotency-key`, `if-match`, …, and `Original` in the conventional casing)
- `HttpHeaderName_is_a_reference_type` (`IsClass`, so the `?` is a nullable reference)

**Production.** `HttpHeaderName.cs` → `sealed record` (private constructor, `Original`, `CanonicalName`, `Of`,
`Equals(HttpHeaderName?)` ordinal over `CanonicalName`, `GetHashCode`, `ToString`); six new `WellKnown` entries.
Fold with `System.Text.Ascii.ToLower` into a `stackalloc`/pooled span for names up to 128 chars, string otherwise.

**PublicAPI.** Edit `[C].HttpHeaderName` block: delete `HttpHeaderName.HttpHeaderName() -> void` and the struct
`Equals([C].HttpHeaderName other)` line; add `Equals([C].HttpHeaderName? other)`, `<Clone>$`, operators, `GetHashCode`,
`ToString`; add `static [C].HttpHeaderName.WellKnown.IdempotencyKey.get -> [C].HttpHeaderName!` and the five siblings.
`ApiKeyCredential`'s constructor line is textually unchanged (`[C].HttpHeaderName? header`), but its meaning changes
from `Nullable<T>` to a nullable reference; `ApiKeyCredential.HeaderName.get` gains `!`; `AuthorizationPolicy.WithheldHeaderName.get`
gains `!`. **Docs:** `HttpHeaderName` `<remarks>` **Breaking** (was a `readonly record struct`; `ToString()` returns
`Original`); `ApiKeyCredential`'s `header` parameter doc notes it is now a nullable reference. **IDs:** `HTTP-21`,
`HTTP-1` (this type). **Verify:** V-fast `HttpHeaderNameTests`; build the solution. `ApiKeyCredential`'s body needs no
edit: it already reads `header ?? HttpHeaderName.WellKnown.Authorization`, which compiles for either kind.

### Task 5.3 — `Headers`: ordered, casing-preserving, ASCII fold, value equality, typed overloads (`HTTP-13`, `HTTP-14`, `HTTP-15`, `HTTP-16`, `HTTP-21`)

**Failing tests first.** `tests/…/Http/Common/HeadersTests.cs` (existing class; keep every existing test, they are
`Unit`), append:

- `A_name_added_under_one_casing_resolves_under_every_other` (`HTTP-13`); `Folding_is_culture_invariant_under_tr_TR`
- `A_non_ASCII_lookup_finds_nothing_and_does_not_throw` (the Kelvin sign `K` does not fold to `k`; breaking item 6)
- `Equal_content_gives_equal_instances_and_hashes_and_casing_is_ignored`; `Differently_ordered_names_are_unequal`;
  `Operators_equal_and_not_equal_handle_null`
- `Add_appends_and_Set_replaces_on_the_instance` (`HTTP-14`: `a`,`b` → `[a,b]`; set `c` → `[c]`)
- `Set_with_null_removes_the_header_and_Set_of_an_absent_name_returns_the_same_instance` (`HTTP-15`)
- `Enumeration_and_Names_are_in_insertion_order_z_a_m` (`HTTP-16`); `Set_on_an_existing_name_keeps_its_position_and_first_casing`;
  `Without_then_With_moves_the_name_to_the_end`
- `Enumeration_and_Names_carry_the_first_insertions_casing` (`HTTP-21`, breaking item 6)
- `A_name_added_through_a_string_is_visible_through_HttpHeaderName_and_back` (typed overloads on `Contains`, `Get`,
  `GetAll`, `With`, `Set`, `Without`)
- `Names_is_a_read_only_list_snapshot` (`HTTP-5` pin: not a `List<string>`, `IList<string>.Add` throws
  `NotSupportedException`, unaffected by later `ToBuilder` edits)
- `ToString_lists_names_only` (`"Headers[Accept, X-Trace]"`, never values)

Red: CS1061/CS1503 on the typed overloads, and assertion failures for casing and equality.

**Production.** `src/Dexpace.Sdk.Core/Http/Common/Headers.cs`: storage per design "Headers and Headers.Builder"
(`ImmutableArray` of `(Original, Folded, ImmutableArray<string> Values)` plus a private ordinal
`Dictionary<string,int>`; `Names` an `ImmutableArray<string>` built with the instance); `IEquatable<Headers>`;
order-sensitive equality over (folded name, values), hash over the same excluding casing; `Set(string, string?)`
(**Breaking:** nullable) and the `HttpHeaderName` overloads; every string entry point keeps trim-then-token validation
through `HeaderSyntax` (`HTTP-17`, `HTTP-18` unchanged). Folding via `Ascii.ToLower`, no `ToLowerInvariant`.
Keep each method within 70 lines by extracting `FindIndex`, `WithEntryReplaced`, `WithEntryAppended`.

**PublicAPI.** Edit the `[C].Headers` block: `Names.get -> System.Collections.Generic.IReadOnlyList<string!>!` (was
`IEnumerable`), `Set(string! name, string? value)`, and add:

```text
[C].Headers.Contains([C].HttpHeaderName! name) -> bool
[C].Headers.Equals([C].Headers? other) -> bool
[C].Headers.Get([C].HttpHeaderName! name) -> string?
[C].Headers.GetAll([C].HttpHeaderName! name) -> System.Collections.Generic.IReadOnlyList<string!>!
[C].Headers.Set([C].HttpHeaderName! name, string? value) -> [C].Headers!
[C].Headers.With([C].HttpHeaderName! name, string! value) -> [C].Headers!
[C].Headers.Without([C].HttpHeaderName! name) -> [C].Headers!
override [C].Headers.Equals(object? obj) -> bool
override [C].Headers.GetHashCode() -> int
override [C].Headers.ToString() -> string!
static [C].Headers.operator !=([C].Headers? left, [C].Headers? right) -> bool
static [C].Headers.operator ==([C].Headers? left, [C].Headers? right) -> bool
```

(`Headers` implements `IEquatable<Headers>`, which changes no listed line.) **Docs.** `Headers`, `Names`, `Set`, the
enumerator and the equality members carry **Breaking** notes (items 6). **IDs:** `HTTP-13`, `HTTP-14`, `HTTP-15`,
`HTTP-16`, `HTTP-21`, `HTTP-5` (Headers half). **Verify:** V-fast `HeadersTests`,
`*HeaderInjectionValidationTests` (green: see below).

> **Position C lands in this task's commit.** This task is what turns
> `Surrounding_whitespace_is_trimmed_from_a_name_before_validation` red (`Names` now yields `X-Trace`), so step 1 of
> task 5.7, the expected-value change and nothing else, is made in the same commit, and every commit in PR 5 stays
> green (convention 8). The commit message names the Security file and cites design position C, so the change is
> visible in the commit as well as in the PR description. Task 5.7 then adds the two new assertions and does the
> review bookkeeping.

### Task 5.4 — `Headers.Builder`: typed overloads, nullable `Set`, casing on `AddInbound` (`HTTP-14`, `HTTP-15`, `HTTP-19`, `HTTP-3`)

**Failing tests first**, `HeadersTests` and a new `HeadersBuilderTests` class
(`tests/…/Http/Common/HeadersBuilderTests.cs`, `Unit`):

- `Builder_add_appends_and_set_replaces` (`HTTP-14`); `Builder_set_null_removes_the_header` (`HTTP-15`)
- `Builder_typed_overloads_match_the_string_ones` (`Add`, `Set`, `Remove`)
- `Build_is_a_deep_copy_and_later_edits_never_reach_the_result` (`HTTP-3`); `ToBuilder_is_a_fresh_builder`
- `AddInbound_keeps_the_senders_casing_and_stays_lenient` (obs-text kept, control rejected; mirrors, does not replace,
  the Security `The_inbound_path_*` tests, `HTTP-19`)

**Production.** `Headers.Builder` on the same entry storage (mutable list plus index, `Build()` copies).
**PublicAPI:** edit `Builder.Set(string!, string?)`; add `Builder.Add([C].HttpHeaderName!, string!)`,
`Builder.Set([C].HttpHeaderName!, string?)`, `Builder.Remove([C].HttpHeaderName!)`, each `-> [C].Headers.Builder!`.
**Docs:** `Builder.Set` **Breaking** (nullable value). **IDs:** `HTTP-14`, `HTTP-15`, `HTTP-19`, `HTTP-3` (multimaps).
**Verify:** V-fast `HeadersBuilderTests`.

### Task 5.5 — Call-site migration to the typed overloads

No new behaviour; the `HttpHeaderName` reference-type change and the casing change make these edits worthwhile and
safe. **Tests:** existing policy tests stay green unedited (any that assert on lower-cased names are `Unit` and updated
in the PR with a note; `Security` classes are unedited).

**Production.** Replace `HttpHeaderName.WellKnown.X.Original` stamping and lookups with typed overloads in
`SetDatePolicy`, `ClientIdentityPolicy`, `RedirectPolicy`, `RetryPolicy` and `AuthorizationPolicy`'s
`Without(WithheldHeaderName)`; `IdempotencyPolicy` uses `WellKnown.IdempotencyKey`. `BasicAuthPolicy`,
`BearerTokenAuthPolicy` and `ApiKeyAuthPolicy` keep `.Original`, because they hand the name back through the
protected `GetCredentialAsync`'s `(string HeaderName, string HeaderValue)` tuple, public surface 2a does not change
(design migration table; the auth resolver is 6c's); `Request.WithHeader(HttpHeaderName, string)` added (the `Request`
type is otherwise untouched until PR 6; add it as an additive member on the still-`init` record so the policies can
switch now, and PR 6 keeps it). **PublicAPI:** `[Q].Request.WithHeader([C].HttpHeaderName! name, string! value) -> [Q].Request!`.
**IDs:** `HTTP-21`. **Verify:** `dotnet test --solution Dexpace.Sdk.sln --configuration Release`.

### Task 5.6 — Adapter: predicate swap and the original-casing wire proof (`HTTP-20`, `HTTP-21`)

**Failing tests first.** New `tests/Dexpace.Sdk.Http.SystemNet.Tests/HeaderCasingWireTests.cs`, class
`HeaderCasingWireTests`, `[Trait("Category", "Integration")]`: `A_custom_header_goes_out_with_its_original_casing`
(loopback raw bytes show `X-Trace-Id: v` for a custom header set as `X-Trace-Id`, and not `x-trace-id`),
`Well_known_names_are_not_asserted` (a comment-only note that `HttpClient` substitutes its own casing for `Accept`,
`Content-Type`; mapping them is 8b's `TRANSPORT-10`). Also `HttpHeaderSyntaxAdapterTests` are **not** needed: the
existing `HeaderInjectionWireTests` (Security) is the behaviour proof and stays unedited.

> **Verified, not a risk.** A throwaway loopback check on the pinned runtime (2026-09-30) showed `HttpClient` sending
> `TryAddWithoutValidation("X-Trace-Id", …)` and `("x-lower", …)` exactly as written, and rewriting only the names it
> knows (`aCCept` → `Accept`, `user-agent` → `User-Agent`). So the red today is the model's lower-casing, and the test
> goes green with this PR. If a later runtime changes that, report it: the requirement would then rest on 8b.

**Production.** `src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClient.cs`: replace the private `IsWireSafe` and
`HeaderSyntaxOnWire` with `HttpHeaderSyntax.IsValidName` / `IsValidOutboundValue` / `EscapeName` (the re-check stays;
design §10 `construction-bypass`); the request mapping enumerates `Headers` (now original casing) with no other change
(its framing set and `content-type` check are already `OrdinalIgnoreCase`). Delete the private class.
**PublicAPI (SystemNet):** none. **IDs:** `HTTP-20`, `HTTP-21`.
**Verify:** `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release` (all, then
`--filter-trait "Category=Security"`).

### Task 5.7 — The position-C `Security` assertion (`HTTP-17`, `HTTP-18`, `HTTP-19`, `HTTP-20` kept)

The one `Security` change in 2a. In `tests/Dexpace.Sdk.Core.Tests/Security/HeaderInjectionValidationTests.cs`,
`Surrounding_whitespace_is_trimmed_from_a_name_before_validation`:

1. change `Assert.Equal("x-trace", Assert.Single(headers.Names))` to `Assert.Equal("X-Trace", Assert.Single(headers.Names))`
   (same ordinal `Assert.Equal`; nothing weakened). This step is committed with task 5.3, which makes it necessary;
   it is listed here so the one Security change is reviewed in one place;
2. **add** `Assert.Equal(HttpHeaderName.Of("x-trace"), HttpHeaderName.Of(name))` and
   `Assert.Equal("v", headers.Get("x-trace"))` to the same test;
3. **do not** edit any other assertion, theory data or trait in the class. The `InvalidNames`/`InvalidOutboundValues`
   theories are extended (added rows only, if any) by *typed-overload* variants in `HttpHeaderNameTests` (5.2), not here.

The PR description calls the change out and cites design position C; the checklist's "existing assertions changed"
table records it. **IDs:** `HTTP-17`, `HTTP-18`, `HTTP-19`, `HTTP-20` (all four: the class keeps them green).
**Verify:** `--filter-class "*HeaderInjectionValidationTests"` and `*HeaderInjectionWireTests`, then both Security
filters.

### Task 5.8 — `HTTP-22` declined (`HTTP-22`)

Edit `docs/first-release.md`, "SHOULD- and MAY-level requirements declined for v1": replace "None is recorded yet."
with an entry: `HTTP-22` (MAY): the header-name interning pool is not built; the `WellKnown` statics share the hot
names and the observable contract is value equality (`HTTP-21`); design §4.1, §12's deferred list. Row exit: ⏳ owned
by this entry. **Verify:** `dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations`.

### Task 5.9 — Close-out

`CHANGELOG.md` → `### Changed`:
"**Breaking:** `HttpHeaderName` is a `sealed record` (was a `readonly record struct`) and `ToString()` returns
`Original`; `Headers` enumerates and lists the original casing in insertion order, `Names` is an `IReadOnlyList<string>`,
`Set` takes `string?` and `null` removes the header, `Headers` has value equality, and a non-ASCII lookup name no
longer folds (`HTTP-13`, `HTTP-14`–`HTTP-16`, `HTTP-21`); `ApiKeyCredential`'s `HttpHeaderName? header` parameter is
now a nullable reference rather than `Nullable<HttpHeaderName>`." and `### Added`: "`HttpHeaderSyntax` — the public
header-syntax predicates transports re-check with (`HTTP-17`–`HTTP-20`); typed `HttpHeaderName` overloads; six
`HttpHeaderName.WellKnown` names; the adapter sends custom header names in their original casing."
Run **V-gate**. **Commit:** `feat!: rebuild Headers and HttpHeaderName; add HttpHeaderSyntax (HTTP-13..HTTP-22)`.

---

## PR 6 — `Request`

Depends on PR 4 (`ForbidsBody`) and PR 5 (`Headers` equality, typed overloads). Breaking items 7–8.

### Task 6.1 — Construction, required fields, forbidden bodies, URL errors (`HTTP-2`, `HTTP-4`, `HTTP-6`, `HTTP-7`, `HTTP-8`, `HTTP-47`, `SEAM-29`)

**Failing tests first.** New `tests/…/Http/Request/RequestTests.cs`, class `RequestTests`, `[Trait("Category", "Unit")]`
(the `Request` tests currently in `Http/BodiesAndRequestTests.cs` stay there; the new class is for 2a rows):

- `A_missing_required_field_throws_ArgumentNullException_with_its_name` (`[Theory]`, `HTTP-4`: `method` (null
  `Method`), `url` (null `Uri`), and `Create` with a null string `url`; asserts type and `ParamName`)
- `A_request_carries_exactly_method_url_headers_and_body` (reflection over public instance properties, `HTTP-6`; the
  reflection excludes compiler-generated members)
- `Headers_is_never_null_and_Body_is_nullable`
- `A_body_on_GET_HEAD_TRACE_CONNECT_is_rejected` (`[Theory]` 4 methods × constructor, `Create`, `WithMethod`, `WithBody`;
  `ArgumentException`, `ParamName` `body`; the message names the method and says to clear the body first) (`HTTP-7`)
- `Deriving_a_request_cannot_install_a_body_carrying_GET` (the spec's own example: a POST with a body, `WithMethod(Get)`
  throws; `WithoutBody().WithMethod(Method.Get)` succeeds)
- `POST_PUT_PATCH_DELETE_OPTIONS_accept_a_body` (pin)
- `A_malformed_relative_non_http_or_file_url_is_rejected` (`[Theory]`: `::bad`, `/rel` (the Linux `file:` trap),
  `ftp://h/x`, `file:///etc/passwd`; `ArgumentException` with `ParamName` `url`; via `Create` and via the constructor
  with `new Uri(input, UriKind.RelativeOrAbsolute)`, because a plain `new Uri("::bad")` throws `UriFormatException`
  before the constructor runs)
- **RULED (P2a-2):** `The_url_error_carries_the_redacted_input` — asserts the message contains
  `new UrlRedactor().Redact(input)`, computed rather than hard-coded so a redactor change cannot drift from it. For
  `::bad`, `/rel` and `ftp://h/x` that is the input itself (no userinfo, no query: verified 2026-09-30), so the
  message names the input as `HTTP-47`'s conformance clause asks; and `A_password_in_the_url_never_reaches_the_message` (`https://user:secret@h:bad/` → the
  message does not contain `secret`).
- `HTTP_8_no_method_is_unrepresentable` — no test; a `// HTTP-8: N/A, see design §4.2` comment in the theory of the
  first bullet. The checklist row is N/A.
- (`HTTP-2` route) `Constructors_are_the_only_route` — this Request-side assertion is in 9.2; here, only the
  `Deriving_…` test above.

Red: CS1061 (`WithMethod`, typed `WithHeader` exists from 5.5), and behavioural failures for the body and URL rules.

**Production.** `src/Dexpace.Sdk.Core/Http/Request/Request.cs`: the four properties lose `init` (get-only);
constructor validates in this order (null checks → `ArgumentNullException` with `ParamName`; body/method rule →
`ArgumentException` `body`; URL rule → `ArgumentException` `url`); `Create`, `Get`, `Post` route through it; the URL
message helper is one private method taking the input string (**RULED P2a-2**: it calls the shared redactor
— add `internal static UrlRedactor Default` to `UrlRedactor` if none exists, an internal change with no public-API
line; an instance is needed because `UrlRedactor.Redact` is not static). `Request.Create`'s `Uri.TryCreate` failure
and the Linux `file:` result are both rejected by the scheme test.

**PublicAPI.** Delete:

```text
[Q].Request.Body.init -> void
[Q].Request.Headers.init -> void
[Q].Request.Method.init -> void
[Q].Request.Url.init -> void
```

(the four `get` lines stay; `Request.Request(...)` constructor gains `!` annotations as reported). **Docs:**
`Request`'s `<remarks>` and each of the four properties carry **Breaking** (was `init`; `with { X = … }` no longer
compiles). **IDs:** `HTTP-4` (request half), `HTTP-6` (request half), `HTTP-7`, `HTTP-8` (N/A), `HTTP-47`,
`HTTP-2` (Request), `SEAM-29` (Request). **Verify:** V-fast `RequestTests` (compiles only once 6.2 lands: implement
6.1 and 6.2 in one working tree).

### Task 6.2 — `With*` derivations and the `with` migration (`HTTP-3`)

**Failing tests first**, `RequestTests` (append): `Each_With_method_returns_a_new_request_and_leaves_the_original_unchanged`
(`WithMethod`, `WithUrl`, `WithHeaders`, `WithHeader` (string and typed), `WithBody`, `WithoutBody`);
`WithUrl_validates_like_the_constructor` (a `ftp` URL throws; this replaces the comment at
`PaginationStrategies.cs:138`); `Every_With_routes_through_the_constructor` (a GET request `WithBody(...)` throws).
In `RedirectPolicyTests` (`Unit`): `A_303_on_a_POST_with_a_body_follows_as_a_bodiless_GET` (pin through the new
constructor) and `A_Location_that_is_not_http_or_https_returns_the_3xx_unfollowed` (`ftp://…` and `mailto:`; no
second send, no exception, the 3xx not disposed; red after 6.1 without the guard).

**Production.** Add `WithMethod`, `WithUrl`, `WithHeaders`, `WithBody`, `WithoutBody` (each `new Request(...)`, never
`this with`), and keep both `WithHeader` overloads, whose `this with` bodies (and the old `WithBody`'s) stop compiling
once the properties are get-only and are rewritten the same way. Migrate every `with` site (recount first:
`grep -rnE "\bwith\s*(\{|$)" src tests --include='*.cs' | grep -vE ':[0-9]+:\s*//'`, which catches the
multi-line form and drops comment lines, leaving one false positive, the log template at
`InstrumentationPolicy.cs:241` ("failed with {ErrorType}"); at `bf8f4ae` that is 10
in `src/` outside `Request.cs` and 19 in `tests/`, 5 single-line and 14 multi-line, in nine files):

| Site | Rewrite |
|---|---|
| `src/…/Pipeline/Policies/{AuthorizationPolicy (2), ClientIdentityPolicy, IdempotencyPolicy, InstrumentationPolicy, SetDatePolicy}.cs` | `with { Headers = h }` → `WithHeaders(h)` |
| `src/…/Pipeline/Policies/RedirectPolicy.cs:162` (all four fields) | one `new Request(newMethod, newUrl, newHeaders, dropBody ? null : request.Body)`, never a `With*` chain (`WithMethod(Method.Get)` before the body is cleared throws `HTTP-7`). Before the response is disposed, add the http(s) check on `newUrl` and `return` the 3xx unfollowed when it fails, as the policy already does for a malformed `Location` (design migration table) |
| `src/…/Pagination/PaginationStrategies.cs` (3 sites, lines ~55, 94, 147) | `WithUrl(...)`; delete the comment about `with` bypassing the constructor, but **keep** the scheme guard at :139–144: it ends pagination with `null` on a hostile `Link`, where `WithUrl` would throw (`PaginationStrategiesTests`' `mailto:`/`ftp:` cases pin it) |
| `tests/…` nine files in the segmentation design's as-built fact 2, including `Security/ReDriveRequestIsolationTests.cs` | mechanical only; no assertion changes (recorded in the checklist) |

**PublicAPI:** add
`WithBody`(already listed, now validating; unchanged text), `[Q].Request.WithHeaders([C].Headers! headers) -> [Q].Request!`,
`[Q].Request.WithMethod([C].Method! method) -> [Q].Request!`, `[Q].Request.WithUrl(System.Uri! url) -> [Q].Request!`,
`[Q].Request.WithoutBody() -> [Q].Request!`. **IDs:** `HTTP-3` (Request). **Verify:** build the whole solution, then
`dotnet test --solution …`; the Security filter on Core.Tests must be green (mechanical edits only).

### Task 6.3 — Equality, and the `RequestBody` equality contract (`HTTP-46`; P2a-1)

**Failing tests first.** `RequestTests` (append), the position-A list:

- `Equal_text_gives_equal_requests_and_hashes` (`Get` twice; `Post` with `FromString("{}")` twice; usable as a
  `Dictionary` key)
- `Userinfo_and_fragment_count` (`https://a@h/` ≠ `https://b@h/`; a fragment-only difference is unequal);
  `Host_names_are_not_resolved` (`localhost` ≠ `127.0.0.1`)
- `Header_value_equality_is_used` (uses PR 5's `Headers` equality; casing ignored)
- `Different_bytes_or_different_content_types_are_unequal`; `FromBytes_and_FromString_over_the_same_bytes_are_equal`
- `Two_FromStream_bodies_over_identical_MemoryStreams_are_unequal` and `A_request_is_equal_to_itself_with_a_stream_body`
- `A_replayable_copy_equals_a_FromBytes_body` (`await body.ToReplayableAsync()` versus `FromBytes`)
- `An_unknown_RequestBody_subclass_keeps_reference_equality` (a test-local subclass)

**Production.** `RequestBody.cs`: `BytesRequestBody` overrides `Equals`/`GetHashCode` (same variant, equal
`ContentType`, `SequenceEqual` bytes; hash over `ContentType` and `ContentLength` only); `StreamRequestBody` and the
abstract class keep reference equality; the class `<remarks>` state the contract 3a and 3b inherit.
`Request.Equals(Request?)`: `Method`, `Url.AbsoluteUri` (`StringComparison.Ordinal`), `Headers`, `Body` (via
`EqualityComparer<RequestBody?>.Default`); `GetHashCode` consistent. **PublicAPI:** no new lines beyond the existing
`Equals`/`GetHashCode`. **Docs:** `Request.Equals` **Breaking** (behaviour: bodies by value where in-memory).
**IDs:** `HTTP-46`. **Verify:** V-fast `RequestTests`.

### Task 6.4 — `Request.ToString()` (RULED)

**Failing tests first**, `RequestTests`: `ToString_is_method_and_redacted_url` (`Get("https://u:p@h/p?token=abc&api-version=1")`
→ `GET https://***:***@h/p?token=***&api-version=1`, computed from `UrlRedactor` to avoid drift);
`ToString_never_contains_header_values_or_the_body`; `ToString_of_a_malformed_url_cannot_occur` (pin: the constructor
guarantees an absolute http(s) `Uri`).

**Production.** `public override string ToString() => $"{Method} {redactor.Redact(Url)}"` (one line plus the shared
redactor). **PublicAPI:** `override [Q].Request.ToString() -> string!`. **Docs:** remarks note the redaction.
**IDs:** none (no requirement owns it; `OBS-11`, `XCUT-19` are the spirit). **Verify:** V-fast `RequestTests`.

### Task 6.5 — Close-out

`CHANGELOG.md` → `### Changed`:
"**Breaking:** `Request`'s `Method`, `Url`, `Headers` and `Body` are get-only, so `with { … }` no longer compiles: use
`WithMethod`, `WithUrl`, `WithHeaders`, `WithBody`, `WithoutBody`; `Request` rejects a body on GET, HEAD, TRACE and
CONNECT (`HTTP-7`); equality uses `Url.AbsoluteUri` ordinally, `Headers` by value, and in-memory bodies by bytes
(`HTTP-46`); `ToString()` prints the method and the redacted URL; a URL error carries the redacted input (`HTTP-47`);
`RedirectPolicy` returns a 3xx whose `Location` is not http(s) unfollowed instead of sending the hop."
Run **V-gate**. **Commit:** `feat!: immutable, validating Request with value equality (HTTP-4, 6, 7, 46, 47)`.

---

## PR 7 — `Response`

Depends on PR 6 (convenience: the same test files). Breaking item 9.

> **Coordination with 2b's SPI PR.** 2b's transport-SPI signature change (taking `RequestOptions`) edits the same
> files as this PR: `SystemNetHttpClient.cs`, `tests/Dexpace.Sdk.TestSupport/Transports/*`, and
> `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`. It is a convenience, not a gate: whichever PR is ready first lands
> first, and the other rebases. To make the rebase cheap, **task 7.1 is a pure refactor with no behaviour change**, so
> it can land early, alone, as a `chore:` PR on top of PR 6, and 2b can build on it. Tasks 7.2–7.5 carry the
> breaking constructor. Whoever rebases re-runs the whole of V-gate and re-derives the `ToResponse` change from 7.4
> rather than resolving conflict hunks textually.

### Task 7.1 — `TestResponses.Create` and the 138 test sites (chore; no behaviour change)

**Failing test first.** New `tests/Dexpace.Sdk.TestSupport/…` is not a test project; the check is the compiler: add
`tests/Dexpace.Sdk.Core.Tests/Http/Response/TestResponsesTests.cs`, class `TestResponsesTests`, `Unit`:
`Create_defaults_the_request_to_a_GET_on_example_test`, `Create_passes_status_headers_body_and_protocol_through`.
Red: CS0117.

**Production (test infrastructure).** `tests/Dexpace.Sdk.TestSupport/Transports/TestResponses.cs` (a `Redirect` factory
already exists and uses the old constructor) gains
`Create(Status status, Request? request = null, Headers? headers = null, ResponseBody? body = null, Protocol protocol = Protocol.Http11, string? reasonPhrase = null)`
implemented over the **current** constructor for now (`request` and `reasonPhrase` are accepted and ignored until 7.2),
supplying `Request.Get("https://example.test/")` when the caller does not care. Migrate every `new Response(...)` site
(138 in `tests/`, recount with `grep -rn "new Response(" tests --include='*.cs' | grep -v /obj/`): the files are
`Core.Tests/{Http/EnsureSuccessTests, Pagination/PageableTests, Pagination/PaginationStrategiesTests,
Pipeline/HttpPipelineTests, Pipeline/DexpacePipelineTests, Pipeline/Policies/{OperationPolicyTests, RetryPolicyTests,
RedirectPolicyTests, InstrumentationPolicyTests}, Security/{RetryPacingOverflowTests, AuthHttpsGuardTests,
ReDriveRequestIsolationTests, EnsureSuccessErrorMappingTests, RedirectCredentialHygieneTests}}`,
`Serialization.SystemTextJson.Tests/{EnsureSuccessGetErrorRoundTripTests, BodyConvenienceTests}`,
`TestSupport/Transports/{RecordingTransport, RecordingSyncTransport}` and `AotSmoke/SmokeChecks.cs`, plus three
target-typed `new(Status…)` constructions the grep misses (`PaginationStrategiesTests.cs:32` and `:36`, and
`TestResponses.Redirect` itself at `TestResponses.cs:16`); the build is the final count.

Rules for the rewrite: tests that do not care use the default request; **every `Security` class passes the real request
of the scenario it exercises** (design migration table). The `Security` edits are mechanical (a constructor call
changes), no assertion, theory row or trait changes; the checklist lists each file with "constructor call only".
**PublicAPI:** none. **IDs:** none (enabling). **Verify:** `dotnet test --solution …` green, and
`git diff --stat -- tests/**/Security` reviewed line by line.

### Task 7.2 — New constructor: `request`, required `protocol`, `reasonPhrase` (`HTTP-4`, `HTTP-6`)

**Failing tests first.** New `tests/…/Http/Response/ResponseTests.cs`, class `ResponseTests`, `Unit`:

- `A_missing_request_throws_ArgumentNullException_with_ParamName_request` (`HTTP-4`)
- `An_undefined_protocol_throws_ArgumentOutOfRangeException_with_ParamName_protocol` (`(Protocol)999`; the silent
  `Http11` default is gone: a compile-time check is a reflection assertion that the `protocol` parameter has no default
  value)
- `Request_and_reason_phrase_round_trip` (`HTTP-6`); `A_reason_phrase_with_a_control_character_throws_ArgumentException`
  (`ParamName` `reasonPhrase`, via `HttpHeaderSyntax.IsValidInboundValue`; the message names the code point and does not
  echo it); `A_null_reason_phrase_is_accepted`; `obs_text_in_a_reason_phrase_is_accepted`
- `Headers_is_never_null`; `An_absent_body_is_an_empty_buffered_body_never_null` (P2a-4: `Body` non-null, length 0,
  `ReadAsBytesAsync` twice returns empty both times)
- `Status_is_a_required_positional_parameter` (reflection)

**Production.** `src/Dexpace.Sdk.Core/Http/Response/Response.cs`: constructor
`Response(Request.Request request, Status status, Protocol protocol, Headers? headers = null, ResponseBody? body = null, string? reasonPhrase = null)`
with `Enum.IsDefined`; `Request` and `ReasonPhrase` properties. Inside the class the type is `Request.Request`.
`TestResponses.Create` now passes `request` and `reasonPhrase` through. The two `src/` sites: `EnsureSuccessAsync`
(see 7.4) and `SystemNetHttpClient` (7.5).

**PublicAPI.** Replace the `[S].Response.Response(...)` constructor line with the new signature and add
`[S].Response.Request.get -> [Q].Request!` and `[S].Response.ReasonPhrase.get -> string?`.
**Docs.** The constructor **Breaking** (new signature; `protocol` has no default). **IDs:** `HTTP-4` (response half),
`HTTP-6` (response half), `HTTP-2` (Response), `SEAM-29` (Response). **Verify:** V-fast `ResponseTests`.

### Task 7.3 — Classification helpers (`HTTP-11`)

**Failing tests first**, `ResponseTests`: `Classification_by_boundary_code` (`[Theory]` on the same twelve codes as
3.1, over `Response`: `IsInformational`, `IsSuccess`, `IsRedirect`, `IsClientError`, `IsServerError`, `IsError`).
**Production.** Each delegates to `Status`. **PublicAPI:**

```text
[S].Response.IsClientError.get -> bool
[S].Response.IsError.get -> bool
[S].Response.IsInformational.get -> bool
[S].Response.IsRedirect.get -> bool
[S].Response.IsServerError.get -> bool
```

(`IsSuccess` already exists.) **IDs:** `HTTP-11` (Response half). **Verify:** V-fast `ResponseTests`.

### Task 7.4 — `WithBody` and `EnsureSuccessAsync` (`HTTP-3`)

**Failing tests first**, `ResponseTests`: `WithBody_returns_a_new_response_owning_the_new_body_and_keeps_the_rest`
(request, status, protocol, reason phrase, headers carried); `The_original_keeps_its_own_body_and_can_still_be_disposed`;
`There_is_no_WithHeaders` (reflection: `Response` offers only `WithBody` as a derivation; guards the ownership rule).
Re-run, unedited, `EnsureSuccessErrorMappingTests` (**Security**) and `EnsureSuccessTests`: they prove
`EnsureSuccessAsync` still buffers and disposes correctly through `WithBody`.

**Production.** `Response.WithBody(ResponseBody)`; `EnsureSuccessAsync` builds its buffered copy with `WithBody`
(replaces `new Response(Status, Headers, bufferedBody, Protocol)` at `Response.cs:100`; the `try`/`finally` disposal
is untouched). **PublicAPI:** `[S].Response.WithBody([S].ResponseBody! body) -> [S].Response!`.
**IDs:** `HTTP-3` (Response). **Verify:** V-fast `ResponseTests`, `*EnsureSuccessErrorMappingTests`.

### Task 7.5 — The adapter threads the request and the reason phrase (`HTTP-6`)

**Failing tests first.** `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetHttpClientTests.cs` (existing class):
`The_response_carries_the_request_that_was_sent`, `The_reason_phrase_is_carried_when_it_is_header_safe`,
`An_unsafe_reason_phrase_is_dropped_to_null_not_thrown` (loopback replies with `HTTP/1.1 200 Fine\x01X` and, as a
second case, `\x7F`; the fixture writes it byte-for-byte; the response is still returned with `ReasonPhrase` null).
Use 0x01 or DEL, not CR, LF or NUL: `HttpClient` itself rejects those in a status line with an
`HttpRequestException` ("reason phrase must not contain new-line or NUL characters"), verified 2026-09-30, so the
adapter never sees them and the test would fail for the wrong reason. `obs_text_in_a_reason_phrase_is_kept`
(`Caf\xE9`, which `HttpClient` passes through as Latin-1) pins the lenient side.

**Production.** `SystemNetHttpClient.ToResponse(HttpResponseMessage, Request)` (line ~321; its one caller,
`ExecuteAsync`, passes the request it is executing, and the sync `Execute` delegates to `ExecuteAsync`) constructs `new Response(request, status, protocol, headers, body,
reason)`, where `reason` is `message.ReasonPhrase` when `HttpHeaderSyntax.IsValidInboundValue` accepts it, else
`null`. `AotSmoke/SmokeChecks.cs`'s fake transport passes `request` (mechanical, done in 7.1).
**PublicAPI (SystemNet):** none. **IDs:** `HTTP-6`. **Verify:**
`dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release`, both Security filters.

### Task 7.6 — Close-out

`CHANGELOG.md` → `### Changed`: "**Breaking:** `Response`'s constructor is now
`(Request, Status, Protocol, Headers?, ResponseBody?, string?)` and `protocol` has no default (`HTTP-4`, `HTTP-6`);
`Response` gains `Request`, `ReasonPhrase`, `IsRedirect`, `IsClientError`, `IsServerError`, `IsError`,
`IsInformational` and `WithBody`." Run **V-gate**. **Commit:** `feat!: Response carries its request and reason phrase (HTTP-4, 6, 11)`.

---

## PR 8 — `ETag`, `HttpRange`, `RequestConditions`

Depends on PR 5 (`Set` with typed names) and PR 6 (`ApplyTo(Request)`). May land in parallel with PR 7. Additive.

### Task 8.1 — `ETag` (`HTTP-48`)

**Failing tests first.** `tests/vectors/http/etag.json` (source: `nodejs-sdk@54aeed4 etag.test.ts`; Ruby has no
`ETag` test), and `tests/…/Http/Common/ETagTests.cs`, class `ETagTests`, `Unit`:

- `Parse_matches_the_vectors` (strong, weak, `*`, quoted forms; exact-spelling round trip through `ToString`)
- `Parse_of_null_or_blank_is_null_and_a_malformed_form_throws` (`"abc"` unterminated `"abc`, lower-case `w/"x"`)
- `TryParse_never_throws`
- `Strong_rejects_empty_and_non_etagc_characters`; `Weak_may_be_empty`; `Characters_are_etagc` (`0x21`, `0x23`–`0x7E`,
  obs-text `0x80`–`0xFF`; `"` and space rejected)
- `Any_is_star_and_has_empty_opaque`; `Equality_is_by_kind_and_opaque`; `ToString_renders_star_quoted_and_weak`

**Production.** `src/Dexpace.Sdk.Core/Http/Common/ETag.cs` (`sealed record`, private constructor, factories per design
"Type shapes"). **PublicAPI:**

```text
[C].ETag
[C].ETag.<Clone>$() -> [C].ETag!
[C].ETag.Equals([C].ETag? other) -> bool
[C].ETag.IsAny.get -> bool
[C].ETag.IsWeak.get -> bool
[C].ETag.Opaque.get -> string!
static [C].ETag.Any.get -> [C].ETag!
static [C].ETag.Parse(string? value) -> [C].ETag?
static [C].ETag.Strong(string! opaque) -> [C].ETag!
static [C].ETag.TryParse(string? value, out [C].ETag? etag) -> bool
static [C].ETag.Weak(string! opaque) -> [C].ETag!
override [C].ETag.GetHashCode() -> int
override [C].ETag.ToString() -> string!
```

(+ synthesised operators.) **IDs:** `HTTP-48`. **Verify:** V-fast `ETagTests`.

### Task 8.2 — `HttpRange` (`HTTP-49`)

**Failing tests first.** `tests/vectors/http/http-range.json`; `tests/…/Http/Common/HttpRangeTests.cs`, class
`HttpRangeTests`, `Unit`: `Parse_matches_the_vectors` (`Bytes=0-9` round-trips verbatim via `ToString`),
`Bounded_rejects_a_negative_offset_a_non_positive_length_and_overflow` (`offset + length - 1` overflow), `Suffix_and_From_render_canonically`,
`Parse_accepts_only_the_bytes_unit_case_insensitively_and_one_range` (a comma throws), `Equality_is_semantic_over_Offset_and_Length`
(`Bytes=0-9` equals `bytes=0-9` built by `Bounded(0, 10)`, and the verbatim text is excluded from equality and hash),
`TryParse_never_throws`.

**Production.** `src/Dexpace.Sdk.Core/Http/Common/HttpRange.cs`; overrides the record-generated equality (the private
verbatim text must be excluded). **PublicAPI:** the members of "Type shapes" (`Bounded`, `Suffix`, `From`, `Parse`,
`TryParse`, `Offset`, `Length`, `Equals`, `GetHashCode`, `ToString`, `<Clone>$`, operators).
**IDs:** `HTTP-49`. **Verify:** V-fast `HttpRangeTests`.

### Task 8.3 — `RequestConditions` (`HTTP-50`; P2a-3)

**Failing tests first.** `tests/…/Http/Request/RequestConditionsTests.cs`, class `RequestConditionsTests`, `Unit`:

- `ApplyTo_joins_each_list_into_one_header_with_commas` (`If-Match: "a", "b"`)
- `Dates_render_in_R_format_converted_to_UTC` (a `+03:00` value)
- `Applying_twice_gives_equal_headers` (idempotent: `Set`, never `With`) and `Unset_members_leave_existing_headers_untouched`
- `Star_is_exclusive_with_concrete_tags` (`[*, "a"]` throws `ArgumentException` through `init` and `with`) and
  `A_repeated_star_collapses` (`[*, *]` → `[*]`)
- `Equality_is_sequence_equality_over_the_arrays` and hash agreement (the generated equality would compare array
  references)
- `ApplyTo_Request_returns_a_request_with_the_headers_and_leaves_the_original_unchanged` (uses `WithHeaders`)
- `A_with_derivation_leaves_the_original_unchanged` (`HTTP-3` for this type: `c with { IfMatch = [..] }`; the
  original's arrays and dates are untouched)
- **RULED (P2a-3):** `A_non_ASCII_ETag_is_valid_but_ApplyTo_cannot_send_it` — `ETag.Strong("é")` (obs-text)
  constructs, and `new RequestConditions { IfMatch = [thatTag] }.ApplyTo(Headers.Empty)` throws `ArgumentException`
  (the `HTTP-18` outbound rule wins, because `Headers.Set` enforces it; the message does not echo the value).

**Production.** `src/Dexpace.Sdk.Core/Http/Request/RequestConditions.cs` per design; `init` accessors normalise;
`ApplyTo(Headers)` uses `Set` with `HttpHeaderName.WellKnown.IfMatch` etc.; `ApplyTo(Request)` delegates through
`request.WithHeaders(ApplyTo(request.Headers))`. **PublicAPI:** the members of "Type shapes"
(`None`, `IfMatch`/`IfNoneMatch` get and init, `IfModifiedSince`/`IfUnmodifiedSince` get and init,
`ApplyTo([C].Headers!)`, `ApplyTo([Q].Request!)`, `Equals`, `GetHashCode`, `<Clone>$`, operators).
**IDs:** `HTTP-50`, `HTTP-3` (this type). **Verify:** V-fast `RequestConditionsTests`.

### Task 8.4 — Close-out

`CHANGELOG.md` → `### Added`: "`ETag`, `HttpRange` and `RequestConditions` (`HTTP-48`–`HTTP-50`)." Run **V-gate**.
**Commit:** `feat: add ETag, HttpRange and RequestConditions (HTTP-48..HTTP-50)`.

---

## PR 9 — Architecture tests, user page, checklist, closing docs

Depends on PRs 1–8 (the architecture tests need every type). Documentation closes the phase (roadmap step 7).

### Task 9.1 — `ModelImmutabilityArchitectureTests` (`HTTP-1`, `HTTP-5`)

**Failing test first.** `tests/Dexpace.Sdk.Core.Tests/Architecture/ModelImmutabilityArchitectureTests.cs`, class
`ModelImmutabilityArchitectureTests`, `[Trait("Category", "Unit")]`, over a fixed list of the model types
(`Method`, `HttpHeaderName`, `Headers`, `Headers.Builder` excluded by name, `Query`, `MediaType`, `Status`, `Request`,
`Response`, `RequestOptions`, `RequestConditions`, `ETag`, `HttpRange`; use the existing `TypeReferences` helper for
loading):

- `Every_instance_field_is_readonly_except_the_documented_body_state` (allow-list: `ResponseBody`/`RequestBody`
  single-use state, `Response`'s disposal state)
- `No_public_property_has_a_non_init_setter` (builders excluded by name)
- `Collections_exposed_are_read_only_types` (`HTTP-5`: no public member returns `List<>`, `Dictionary<,>` or an array)
- `Every_model_type_is_sealed` (styleguide 6.6)

Expected first run: red only if a type slipped (e.g. an `init`-less setter); a green first run is a pin, and the test is
then confirmed to fail by temporarily adding a `public int X { get; set; }` to one type (do not commit that).
**IDs:** `HTTP-1`, `HTTP-5`. **Verify:** V-fast.

### Task 9.2 — `ModelConstructionArchitectureTests` (`HTTP-2`, `SEAM-29`)

`tests/Dexpace.Sdk.Core.Tests/Architecture/ModelConstructionArchitectureTests.cs`, class
`ModelConstructionArchitectureTests`, `Unit`:

- `Public_constructors_in_Http_namespaces_are_exactly_the_allow_list` (`Request`, `Response`, `RequestOptions()`,
  `RequestConditions()`, `Headers.Builder()`, `Query.Builder()`; anything else fails)
- `Value_types_are_created_through_factories` (`Method`, `HttpHeaderName`, `MediaType`, `ETag`, `HttpRange`, `Query`
  expose no public constructor)
- `No_generic_builder_contract_exists` (`SEAM-29`'s 🚫: no public type named `IBuilder<T>` or `Builder<T>` in Core;
  cites design §10 `no-builder-objects`)
- `A_derived_request_cannot_bypass_validation` (`with` is not compilable: the type has no public setter; the
  reflection form of `RequestTests.Deriving_a_request_cannot_install_a_body_carrying_GET`)

**IDs:** `HTTP-2`, `SEAM-29`. **Verify:** V-fast.

### Task 9.3 — AOT smoke for the new surface

Extend `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (the consumer is published and run in CI) with one check that
touches every new public type in an AOT-safe way: `RequestOptions` `with`, `Query` parse/encode round trip, `Headers`
typed overloads and equality, `Request` `With*` and equality, `Response.WithBody`, `ETag`/`HttpRange`/`RequestConditions.ApplyTo`.
No reflection, no `field`-related trimming risk expected; if the publish emits a trim/AOT warning, fix the source, not
the smoke. **Verify:** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke`.

### Task 9.4 — User documentation

New `docs/sdk-documentation/http.md` (the directory does not exist yet: also add nothing else to it). It opens
"As built by phase 2a … written against source on <date>", as the roadmap's universal exit criteria require. Content: the
model types and how to construct and derive them, `Headers` casing and equality, `Query`, `RequestOptions`,
`RequestConditions` with `ETag`/`HttpRange`, `HttpHeaderSyntax` for transport authors, the breaking-change migration
table (`with` → `With*`, `Response` constructor, `Method`/`HttpHeaderName` are classes). Cite requirement IDs; do not
copy design §4. Update `docs/README.md`'s ownership table if the probe asks. **Verify:** the probe's `links` check.

### Task 9.5 — The checklist

Write `docs/work/mvp/phase2/phase2a/<date>-phase2a-domain-model-checklist.md` from what was built (one row per
requirement ID, phase-1 legend), including: the "existing assertions changed" table (the position-C row; the mechanical
`Security` edits with their diff kind per file), the deviation ledger P2a-1 to P2a-4 as built, and the design gaps in
this plan's "Findings" section that the lead resolved. Add the `CHANGELOG.md` line "Phase 2a closed" only if the phase
workflow requires (it does not add a second entry per requirement). **IDs:** all 42 (closing).

### Task 9.6 — Dated corrections and the ambiguity appendix (RULED)

Frozen documents change only by dated correction. Following the lead's 2026-09-30 rulings:

- design `docs/sdk-design-dotnet/11-appendix-reference-spec-ambiguities-and-how-this-port-resolves-them.md`: numbered
  item for **P2a-1** (body by value), and for **P2a-3** (non-ASCII `ETag` versus `HTTP-18`: valid under `HTTP-48`,
  unsendable through `RequestConditions`, the outbound rule wins).
- design `docs/sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md`: entry for **P2a-2**
  (`HTTP-47` carries the redacted input) and **P2a-4** (absent response body is an empty buffered body).
- Both files: "As built" lines updated for §4's affected sections in the same dated correction.

**Verify:** the probe's `links` and `citations` checks.

### Task 9.7 — Roadmap status note and knowledge notes

Append a dated Phase Status Note for 2a to the roadmap (what landed, the nine PRs, the four rulings, the
2b hand-off: `RequestOptions` merged, `Query` and `Rfc3986` available, `Response` constructor changed). If the
implementation found anything the knowledge corpus should hold, record it as a note under `docs/knowledge/notes/`
(never edit `harvested/`). Run the housekeeping probe once more.

### Task 9.8 — Close-out

`CHANGELOG.md`: `### Added` "`docs/sdk-documentation/http.md`; architecture tests pinning `HTTP-1`, `HTTP-2`, `HTTP-5` and
`SEAM-29`." Run **V-gate** and the coverage gate. **Commit:** `test: architecture tests for model immutability and construction (HTTP-1, HTTP-2, SEAM-29)`, then `docs: phase 2a checklist and user page`.

---

## Traceability: ID → PR → task

| ID | PR | Task(s) | ID | PR | Task(s) |
|---|---|---|---|---|---|
| `HTTP-1` | 4, 5, 9 | 4.1, 5.2, 9.1 | `HTTP-27` | 3 | 3.3 |
| `HTTP-2` | 6, 7, 9 | 6.1, 7.2, 9.2 | `HTTP-28` | 2 | 2.3 |
| `HTTP-3` | 1, 2, 5, 6, 7, 8 | 1.2, 2.3, 5.4, 6.2, 7.4, 8.3 | `HTTP-29` | 2 | 2.3 |
| `HTTP-4` | 6, 7 | 6.1, 7.2 | `HTTP-30` | 2 | 2.4 |
| `HTTP-5` | 2, 5, 9 | 2.3, 5.3, 9.1 | `HTTP-31` | 2 | 2.5 |
| `HTTP-6` | 6, 7 | 6.1, 7.2, 7.5 | `HTTP-32` | 2 | 2.2 |
| `HTTP-7` | 6 | 6.1 | `HTTP-33` | 3 | 3.2 |
| `HTTP-8` | 6 | 6.1 (N/A note; checklist row N/A) | `HTTP-34` | 1 | 1.1, 1.2 |
| `HTTP-9` | 4 | 4.1, 4.2 | `HTTP-35` | 1 | 1.1 |
| `HTTP-10` | 3 | 3.1 | `HTTP-46` | 6 | 6.3 |
| `HTTP-11` | 3, 7 | 3.1, 7.3 | `HTTP-47` | 6 | 6.1 (P2a-2 ruled) |
| `HTTP-12` | 3 | 3.1 | `HTTP-48` | 8 | 8.1, 8.3 (P2a-3 ruled) |
| `HTTP-13` | 5 | 5.3 | `HTTP-49` | 8 | 8.2 |
| `HTTP-14` | 5 | 5.3, 5.4 | `HTTP-50` | 8 | 8.3 |
| `HTTP-15` | 5 | 5.3, 5.4 | `HTTP-53` | 3 | 3.5 |
| `HTTP-16` | 5 | 5.3 | `SEAM-29` | 6, 7, 9 | 6.1, 7.2, 9.2 |
| `HTTP-17` | 5 | 5.1, 5.7 | | | |
| `HTTP-18` | 5 | 5.1, 5.7 | | | |
| `HTTP-19` | 5 | 5.1, 5.4, 5.7 | | | |
| `HTTP-20` | 5 | 5.1, 5.6, 5.7 | | | |
| `HTTP-21` | 5 | 5.2, 5.3, 5.5, 5.6 | | | |
| `HTTP-22` | 5 | 5.8 | | | |
| `HTTP-23` | 3 | 3.3 | | | |
| `HTTP-24` | 3 | 3.4 | | | |
| `HTTP-25` | 3 | 3.3 | | | |
| `HTTP-26` | 3 | 3.3 (Security class cited, unedited) | | | |

Count: `HTTP-1`–`HTTP-35` (35) + `HTTP-46`–`HTTP-50` (5) + `HTTP-53` (1) + `SEAM-29` (1) = 42 rows, all mapped.

---

## Tasks per PR

| PR | Tasks |
|---|---|
| 1 | 1.1–1.3 (3) |
| 2 | 2.1–2.6 (6) |
| 3 | 3.1–3.6 (6) |
| 4 | 4.1–4.3 (3) |
| 5 | 5.1–5.9 (9) |
| 6 | 6.1–6.5 (5) |
| 7 | 7.1–7.6 (6) |
| 8 | 8.1–8.4 (4) |
| 9 | 9.1–9.8 (8) |
| **Total** | **50 tasks** |

---

## Findings while planning, as resolved at review (2026-09-30)

Each item was checked against the repository at `bf8f4ae`. Where it was a real gap, the design was corrected in the
same change, and the plan now follows the corrected design.

1. **`TestResponses` already exists** (with a `Redirect` factory on the old constructor). *Confirmed;* the design's
   migration table now says so, and task 7.1 extends the class and moves `Redirect`.
2. **The `new Response(` sites span four test projects.** *Confirmed in part:* the design's count of 138 already
   covered them but did not name them, and neither document counted the three target-typed `new(Status…)` sites.
   The design's table names the projects and the three sites; task 7.1 lists them.
3. **`with` counts.** *Rejected:* there are 19 sites in nine test files (5 single-line, 14 multi-line), matching the
   segmentation design's as-built fact 2; "about 20" was right. The design now says 19, and task 6.2's recount grep
   catches both forms.
4. **`Method.Of` case folding.** *Not a design defect:* design §4.3 states the fold ("upper-cases the well-known
   verbs"). It was kept as the design's open question 4, because the class remark and RFC 9110 pull the other way;
   the lead ruled on 2026-09-30 to keep the fold.
5. **`UrlRedactor` is an instance class.** *Confirmed:* the design's `HTTP-47` row now names one shared internal
   instance. The second half, that `::bad`, `/rel` and `ftp://…` redact to the sentinel, is *rejected*: they redact
   to themselves (verified), so the message does name the input. Task 6.1 is corrected.
6. **`Range` in `WellKnown`** is stamped by nothing in 2a. *Confirmed;* the design's rationale is corrected (added
   beside `HttpRange` for callers). It stays in the plan.
7. **Position C's red order.** *Confirmed:* a red commit inside PR 5 breaks convention 8. The expected-value change
   is now committed with task 5.3; task 5.7 keeps the additions and the review record.
8. **Vector infrastructure** does not exist. *Confirmed;* the design's PR 2 row now includes the loader and the csproj
   wiring that task 2.1 adds.
9. **The 2b coordination** is bidirectional. Kept as planned: 7.1 is split out as a behaviour-free refactor.
10. **Where the P2a-3 entry lives.** *Rejected:* the design's ledger routes P2a-3 to design §11, and its ruling 3
    names §11. Task 9.6 follows it.
11. **`HeaderCasingWireTests`' dependence on `HttpClient`'s casing.** *Rejected as a risk:* verified on the pinned
    runtime (task 5.6's note); the design's `HTTP-21` row records it.
