# Phase 2a — Domain Model: Design

**Status:** Draft, for review. Written 2026-09-30 against `main` at `bf8f4ae`, on branch
`36-phase-2a-domain-model-design`. GitHub issue [#36](https://github.com/dexpace/dotnet-sdk/issues/36). The scope
authority is the [phase 2 segmentation design](../2026-09-29-phase2-segmentation-design.md), which gives 2a 42
requirement rows and names four things this document must argue.

**What this document is.** The sub-phase design for 2a: one explicit decision per requirement row, the public shape
of every model type 2a builds or rebuilds, the three argued positions the lead asked for, a migration plan from the
as-built code, the breaking-changes list, and a landing order in pull-request-sized steps with `RequestOptions`
first.

**What this document is not.** It is not the [plan](2026-09-30-phase2a-domain-model.md) (numbered TDD tasks) and not the checklist. It does not restate
the domain-model construction pattern, the `record`/`with`/`init` rule or the struct-default rule. Those are
[design §4](../../../../sdk-design-dotnet/04-domain-model-construction.md), cited here and never copied (roadmap
constraint 9). Where design §4 already decides something, this document records the decision against its row and
cites the section. It argues only what §4 and the segmentation design leave open.

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience**, as the roadmap's step 2 requires. The segmentation
design argues the phase-level edges. This section restates the ones 2a stands on.

| Predecessor | Kind | State at `bf8f4ae` |
|---|---|---|
| Phase 0 tasks 1–4 (green build, CI and analyzers, test partition) | **dependency** | Met. `PublicAPI.Unshipped.txt` under `RS0016`/`RS0017` is how every surface change below is reviewed, and 2a's `HTTP-21` wire proof needs the loopback fixture in `tests/Dexpace.Sdk.Http.SystemNet.Tests`. |
| Phase 0 task 8, the design and styleguide harvest ([#29](https://github.com/dexpace/dotnet-sdk/issues/29)) | **convenience** | Open. The consequence is the segmentation design's: phase 0 has not literally exited, and 2a proceeds anyway. So this document cites the design and the styleguide by path, not by corpus key. |
| Phase 1 S1, header and media-type injection | **entry criterion**, convenience in kind | Met. Its `Security` classes exist, so the `Headers` rebuild has something to keep green (constraint 5). |
| Phase 1 S2–S9 | **convenience** | Met. 2a edits their tests mechanically only (see [Migration](#migration-plan-from-the-as-built-code)). |
| 2b | **none** | Nothing in 2a needs anything from 2b. The edges run the other way: 2b is hard-gated on `RequestOptions` and prefers `Query`. Hence [the landing order](#landing-order). |

---

## Governing documents, and the phase-start queries

- **Normative.** The canonical text of every row is its appendix-C row
  (`docs/product-spec/appendix-c-consolidated-normative-requirement-index.md`). The chapters with conformance
  clauses are `docs/product-spec/04-core-http-domain-model.md` §4.1–§4.6, and
  `docs/product-spec/02-architectural-principles.md` for `HTTP-1`, `HTTP-2` and `SEAM-29`.
- **Design.** §4 and §4.1–§4.5, with their **As built (d45e64b)** lines. §3.2 and §3.5 for the transport and URL
  facts. §6.1 for `RetryFacts`. In §10, the topics `no-builder-objects` and `construction-bypass`. In §11, items 6,
  22 and 36. The deferred list in §12.
- **Styleguide.** `docs/styleguide/csharp/03-nullability-and-the-type-system.md` (rule 3.8, choosing the kind),
  `06-types-and-data-modeling.md` (rule 6.6, sealed by default), `10-api-design.md` (rule 10.3, no mutable
  collection behind a read-only type; minimal surface) and `14-documentation.md`. The SDK overlay's `I`-prefix and
  `Async`-suffix departures do not touch 2a's types.

| Query, run 2026-09-30 | Result |
|---|---|
| `scripts/knowledge --origin note --brief` | exit 1: no notes exist |
| `scripts/knowledge --section conflicts --brief` | exit 1: no Conflicts entry, because neither the design nor the styleguide role is harvested |
| `scripts/knowledge --prefix-info HTTP` | 53 IDs: 43 MUST, 9 SHOULD, 1 MAY; 53 of 53 substantive |
| `scripts/knowledge --prefix-info SEAM` | 30 IDs: 23 MUST, 5 SHOULD, 2 MAY; 25 substantive, 5 uncited |
| `scripts/knowledge --gaps HTTP,SEAM` | `HTTP`: none. `SEAM`: `SEAM-15`, `SEAM-20`, `SEAM-22`, `SEAM-23`, `SEAM-28`, all 2b's. **2a has no gap ID**, so every 2a row is stated in a chapter as well as in appendix C |

The comma form was used on purpose, because the space form silently drops prefixes
([#31](https://github.com/dexpace/dotnet-sdk/issues/31)). Every row below was read from its appendix-C text.

**Scope.** 42 rows: `HTTP-1`–`HTTP-35` (35), `HTTP-46`–`HTTP-50` (5), `HTTP-53` (1) and `SEAM-29` (1). That is 33
MUST, 8 SHOULD (`HTTP-8`, `HTTP-16`, `HTTP-27`, `HTTP-32`, `HTTP-47`–`HTTP-50`) and 1 MAY (`HTTP-22`).

---

## Decisions, one per requirement row

**How to read the table.**

- **Exit** uses the roadmap's constraint-3 legend.
- **Tests** are `[Trait("Category", "Unit")]` unless another category is named.
- Test classes live in `tests/Dexpace.Sdk.Core.Tests/Http/{Common,Request,Response}/`, mirroring the source folders.
  The one exception is the architecture tests, which go in `tests/Dexpace.Sdk.Core.Tests/Architecture/`.
- A "keep" row cites an existing phase-1 `Security` test that must stay green (constraint 5).
- A "pin" row adds a test for behaviour that is already correct.
- **PR** points to [the landing order](#landing-order).

| ID | Level | Decision | Rationale | Type / member | Test approach | PR | Exit |
|---|---|---|---|---|---|---|---|
| `HTTP-1` | MUST | Every model type is immutable after construction. `Method` and `HttpHeaderName` become `sealed record`s, `Request` becomes get-only, and `Headers` and `Query` hold `ImmutableArray`s. The new records either validate in `field`-backed `init` accessors or have no setters. The body carve-out stays as it is (§4.5). The tag carve-out is unused, because tag values are strings. | Design §4's type table and struct-default rule. | all model types | `ModelImmutabilityArchitectureTests` (Architecture). By reflection: every instance field of the model types is `readonly`, except the documented body state; no public property has a non-`init` setter; the builders are excluded by name. | 4, 5, 9 | ✅ |
| `HTTP-2` | MUST | There are four construction routes. Private constructors behind factories (`Method.Of`, `HttpHeaderName.Of`, `MediaType.Of`/`Parse`, `Status.FromCode`, `ETag.*`, `HttpRange.*`, `Query.Parse`). Validating constructors (`Request`, `Response`). Parameterless records whose every `init` validates (`RequestOptions`, `RequestConditions`). The two multimap builders. The record-generated `<Clone>$` copies already-validated state and cannot assign a get-only member, so `with` is no bypass. The builder clause is 🚫 via §10 `no-builder-objects`, and the reflection residue is §10 `construction-bypass`. | Design §4, §11 item 6. | constructors and factories | The same architecture test lists every public constructor in `Http.*` against an allow-list: `Request`, `Response`, `RequestOptions()`, `RequestConditions()`, `Headers.Builder()`, `Query.Builder()`. `RequestTests.Deriving_a_request_cannot_install_a_body_carrying_GET` is the spec's own conformance example. | 6, 7, 9 | ✅, citing both §10 topics |
| `HTTP-3` | MUST | Non-aliasing derivations. `Headers.ToBuilder()` and `Query.ToBuilder()` copy into a fresh builder. `Request` derives through `With*`, `Response` through `WithBody`, and `RequestOptions`/`RequestConditions` through `with`. The value types re-construct through their factories. `newBuilder()` for the non-multimap models is covered by §10 `no-builder-objects`. | Design §4 ("`with` is `newBuilder()` followed by `build()`"). | `ToBuilder`, `With*`, `with` | For each (including `RequestOptions.WithTag` and a `RequestConditions` `with`): derive, mutate the builder or copy, then assert the original is unchanged. | 1, 2, 5, 6, 7, 8 | ✅, citing §10 |
| `HTTP-4` | MUST | A missing required field throws `ArgumentNullException` with the field in `ParamName`: `Request` (`method`, `url`) and `Response` (`request`). `Response.protocol` becomes **required** (the silent `Http11` default is removed) and is checked with `Enum.IsDefined`, throwing `ArgumentOutOfRangeException` with `ParamName` `protocol`. `status` is a required positional parameter. The message form is covered by §10 `no-builder-objects`. | The spec forbids a default that is not "explicitly specified". Today's `Http11` default is exactly that. | `Request..ctor`, `Response..ctor` | A `[Theory]` over each missing field, asserting the exception type and `ParamName`. | 6, 7 | ✅ |
| `HTTP-5` | MUST | Already met: collections are stored once as `ImmutableArray` and returned as `IReadOnlyList<string>`. The rebuild keeps it, and `Names` becomes an `ImmutableArray` snapshot built with the instance. | Design §4, "Read-only collection exposure". | `Headers`, `Query` | Pin with a downcast probe: cast `GetAll(...)` to `IList<string>` and assert that `Add` throws `NotSupportedException`, and that the value is not a `List<string>`. Also assert that a snapshot is unaffected by later builder edits. | 5, 2 | ✅ |
| `HTTP-6` | MUST | `Request` carries exactly `Method`, `Url`, `Headers` and `Body?`. `Response` gains `Request` and `ReasonPhrase` (`string?`). `Headers` is never `null` on either. `Request.Body` is nullable. An absent response body is an empty buffered body: optional at construction, never `null` on read (P2a-4). | Design §4.2. | `Request`, `Response` | Reflection asserts that `Request`'s public instance properties are exactly those four. `ResponseTests` cover the request and reason phrase round trip, and a reason phrase with a control character throwing. | 6, 7 | ✅ |
| `HTTP-7` | MUST | The `Request` constructor throws `ArgumentException` (`ParamName` `body`) when `method.ForbidsBody && body is not null`. The message names the method and says to clear the body first. The new internal `Method.ForbidsBody` covers exactly GET, HEAD, TRACE and CONNECT. Every `With*` routes through the constructor. | Design §4.2. The as-built constructor checks nothing. | `Request..ctor`, `Method.ForbidsBody` | A `[Theory]` over the four methods × (constructor, `Create`, `WithMethod`, `WithBody`). `WithoutBody().WithMethod(Method.Get)` succeeds. | 6 | ✅ |
| `HTTP-8` | SHOULD | **N/A.** "No method set" is unrepresentable: `method` is a non-optional constructor parameter, and `null` is rejected under `HTTP-4`. | Design §4.2: "unrepresentable rather than defaulted". | — | None of its own; the row cites §4.2 and `HTTP-4`'s null test. | — | N/A |
| `HTTP-9` | MUST | Remove the public `IsSafe` and `IsIdempotent`. The single source is `internal static class RetryFacts` (`Pipeline/Policies/RetryFacts.cs`), whose `FrozenSet<Method> IdempotentMethods` is {GET, HEAD, OPTIONS, PUT, DELETE}. It is read by the internal `Method.IsIdempotent`, and `RetryPolicy`'s replay gate calls that. 6a grows `RetryFacts` and derives its allow-list from the same set. `Method.Of` trims SP/HTAB, validates an RFC 9110 token, maps the nine well-known verbs to their cached instances with an ASCII case-insensitive match (so `get` becomes `Method.Get`, as today and as design §4.3 states: "upper-cases the well-known verbs") and keeps every other token verbatim. The fold stays, despite RFC 9110's case-sensitive methods: ruled by the lead on 2026-09-30 (ruling 4 below). TRACE stops being retried. | Design §4.3, §6.1, §11 item 22. Segmentation item 3: 2a creates the source, and 6a grows it. | `Method`, `RetryFacts`, `RetryPolicy` | `MethodTests`: membership for all nine methods through `InternalsVisibleTo`; each well-known token equals its upper-case name; `Of` rejects `"GET\r\nX: y"`, `"FOO BAR"` and `""` without echoing the input; reflection finds no public `bool` property on `Method`. `RetryPolicyTests.A_TRACE_request_is_not_retried`. | 4 | ✅ |
| `HTTP-10` | MUST | Add `Status.TryGetKnown(int, out Status)`. `FromCode` stays total. | Design §4.3. | `Status` | `StatusTests`: 200 maps to the named `Ok`; 599, 799 and −1 are nameless and do not throw; `TryGetKnown(599)` is false and `TryGetKnown(200)` is true. | 3 | ✅ |
| `HTTP-11` | MUST | Add `Status.IsError` (400–599). `Response` gains `IsInformational`, `IsRedirect`, `IsClientError`, `IsServerError` and `IsError` beside `IsSuccess`, each delegating to `Status`. | Design §4.2, §4.3. | `Status`, `Response` | A `[Theory]` of boundary codes (99, 100, 199, 200, 299, 300, 399, 400, 499, 500, 599, 600) against `Status` and `Response`. | 3, 7 | ✅ |
| `HTTP-12` | MUST | Already met. Keep the code-only `Equals`/`GetHashCode`. | Design §4.3. | `Status` | Cite `Status_EqualityIsByCode`, moved into `StatusTests` and given a hash-equality assertion. | 3 | ✅ |
| `HTTP-13` | MUST | Fold with `System.Text.Ascii.ToLower` for writes **and** lookups. A lookup with a non-ASCII name finds nothing and does not throw. `Headers` gains value equality and a hash over the ordered (folded name, values) entries, with original casing excluded. `HttpHeaderName` compares by its folded form. | Design §4.1. Phase 1 routed the lookup fold here. | `Headers`, `HttpHeaderName` | `HeadersTests`: a name added under one casing resolves under every other; folding is culture-invariant under `tr-TR` (`"TITLE"`/`"title"`); a header named `key` is not found by `"Key"`; equal content gives equal instances and hashes; different casing still gives equal instances. | 5 | ✅ |
| `HTTP-14` | MUST | Already met. The rebuild keeps add-appends and set-replaces. | Design §4.1. | `Headers`, `Headers.Builder` | Pin: add `a`, add `b` gives `[a, b]`; set `c` gives `[c]`, on both the instance and the builder. | 5 | ✅ |
| `HTTP-15` | MUST | `Set(string, string?)` and `Set(HttpHeaderName, string?)` remove the header on `null`, on `Headers` and on `Headers.Builder`. | Design §4.1. | `Headers.Set`, `Builder.Set` | Set, then set `null`, and assert it is no longer contained. `Set(absent, null)` returns the same instance. | 5 | ✅ |
| `HTTP-16` | SHOULD | Keep an ordered entry array with an ordinal index. `Set` on an existing name keeps its position and its first casing, because a replacement is not an insertion. `Without` followed by `With` moves the name to the end. | Design §4.1. | `Headers` | Insert `z`, `a`, `m` and assert enumeration and `Names` give `z`, `a`, `m`. Assert the position rule for `Set`. | 5 | ✅ |
| `HTTP-17` | MUST | Keep. The rebuild carries the trim-then-token validation unchanged, and the typed overloads take an already-validated `HttpHeaderName`. | Phase 1 S1; §11 item 36. | `HeaderSyntax` (internal), `HttpHeaderSyntax` (public, position B) | Cite `HeaderInjectionValidationTests` (Security). The one changed expected value is argued in position C. | 5 | ✅ |
| `HTTP-18` | MUST | Keep. | Phase 1 S1. | as above | Cite the same class. | 5 | ✅ |
| `HTTP-19` | MUST | Keep. The rebuild carries `Headers.Builder.AddInbound`, which now also keeps the sender's casing. | Phase 1 S1. | `Headers.Builder.AddInbound` | Cite `The_inbound_path_*` in the same class. | 5 | ✅ |
| `HTTP-20` | MUST | Keep. Model messages name the character as `U+XXXX` and never echo a value or an invalid name. The adapter's log escaping moves onto the public `HttpHeaderSyntax.EscapeName` (`\uXXXX`). | Phase 1 S1; position B. | `HeaderSyntax`, `HttpHeaderSyntax.EscapeName` | Cite the Security class. `HttpHeaderSyntaxTests` covers `EscapeName`. | 5 | ✅ |
| `HTTP-21` | MUST | `HttpHeaderName` is a `sealed record`: equality and hash over `CanonicalName` (ASCII-folded), with `Original` kept for the wire. It gains typed overloads on `Headers`, `Headers.Builder` and `Request.WithHeader`. `Headers` **enumerates and lists original casing** (the first insertion's). `ToString()` returns `Original`. | Design §4.1; segmentation item 2. | `HttpHeaderName`, `Headers`, `Request.WithHeader` | `HttpHeaderNameTests`: equality and hash across casings; validation parity over `HeaderInjectionValidationTests.InvalidNames`. `HeadersTests`: a name added through one form is visible through the other. `HeaderCasingWireTests` (Integration, SystemNet tests): the loopback's raw bytes show `X-Trace-Id:` for a custom header (verified 2026-09-30 on the pinned runtime: `TryAddWithoutValidation("X-Trace-Id", …)` and `("x-lower", …)` reach the wire as written, while `aCCept` and `user-agent` go out as `Accept` and `User-Agent`). | 5 | ✅ |
| `HTTP-22` | MAY | Not implemented. The `WellKnown` statics already share the hot names, and the observable contract is value equality (`HTTP-21`). | Design §4.1; §12's deferred list. | — | none | 5 | ⏳, owned by a `docs/first-release.md` entry under "SHOULD- and MAY-level requirements declined for v1" that PR 5 writes |
| `HTTP-23` | MUST | Already met. | Design §4.4. | `MediaType` | Pin with the ported case table (`Application/JSON;Charset=UTF-8`), extracted to `tests/vectors/http/media-type.json`. | 3 | ✅ |
| `HTTP-24` | MUST | `Charset` also catches `NotSupportedException`, so `utf-7` returns `null` (verified: `Encoding.GetEncoding("utf-7")` throws `NotSupportedException`). The parameter key is already folded, so the lookup is case-insensitive. | Design §4.4, §3.1. | `MediaType.Charset` | Assert `utf-8` and `UTF-8` resolve to UTF-8, `Charset=` is found, and `bogus`, `utf-7` and an absent parameter each give `null`. | 3 | ✅ |
| `HTTP-25` | MUST | Already met. | Design §4.4. | `MediaType.Parse`/`ToString` | Pin the ported round-trip table (vectors), including boundaries holding `;` and `=`. | 3 | ✅ |
| `HTTP-26` | MUST | Keep. Parse and construction already use the `HTTP-18` predicate. | Phase 1 S1. | `MediaType` | Cite `HeaderInjectionValidationTests`'s media-type rows (Security). | 3 | ✅ |
| `HTTP-27` | SHOULD | Already met: `*/json` is rejected, and a receiver wildcard matches concrete values but not the reverse. | Design §4.4. | `MediaType.Of`, `Includes` | Pin: `Of("*", "json")` throws; `text/*` includes `text/plain` but not the reverse; `*/*` includes everything; parameters are ignored. | 3 | ✅ |
| `HTTP-28` | MUST | New `Query`: case-sensitive (ordinal) names, insertion order by first appearance, multiple values per name, and `Add(name, null)` stored as `""`. | Design §4.4. | `Query`, `Query.Builder` | `QueryTests`: `page` and `Page` are distinct; after `Add("flag", null)`, `Get` returns `""` and `Contains` is true; an absent name's `Get` is `null`. | 2 | ✅ |
| `HTTP-29` | MUST | `Encode()` renders each component through the internal `Rfc3986.EncodeComponent` (`Uri.EscapeDataString`, verified exact: `a b+c/d*e~f` becomes `a%20b%2Bc%2Fd%2Ae~f`). A repeated name is emitted once per value; there is no leading `?`; an empty query gives `""`. | Design §4.4, §3.5. | `Query.Encode` | Vectors in `tests/vectors/http/query.json`, including `{q:["a b"], plus:["c+d"]}` → `q=a%20b&plus=c%2Bd`. | 2 | ✅ |
| `HTTP-30` | MUST | Equality is order-sensitive over the grouped entries. It is equivalent to equal `Encode()` output, because per-component percent-encoding is injective over well-formed strings and the builder rejects lone surrogates. `Builder.Set(name, [])` drops the name at `Build`. | Design §4.4. | `Query.Equals`, `Builder.Build` | A `[Theory]` asserting that `a.Equals(b)` holds exactly when `a.Encode() == b.Encode()`. The phantom-entry test: `Set("x", [])` leaves `Contains("x")` false. | 2 | ✅ |
| `HTTP-31` | MUST | `Parse(string?)` is total and lenient. `null` or blank gives `Empty`; a leading `?` is stripped; `a` and `a=` both give `""`; stray `&` and empty-name segments are skipped; a malformed escape stays raw and `+` stays `+` (verified: `Uri.UnescapeDataString("a%zzb+c%2")` is unchanged). An escaped invalid UTF-8 sequence, including an encoded lone surrogate such as `%ED%A0%80`, or `%FF`, also stays raw: `UnescapeDataString` leaves it unchanged (verified 2026-09-30), so decoding never produces a lone surrogate. A lone surrogate given *literally* in the input string survives decoding, so `Parse` replaces it with U+FFFD; otherwise `"a=\uD800"` and `"a=�"` would encode identically (`%EF%BF%BD`) yet be unequal, breaking `HTTP-30`'s equivalence and the round trip. | Design §4.4. | `Query.Parse` | Vectors, plus `Parse(q.Encode()) == q` for every vector; `%ED%A0%80` and `%FF` stay raw; a literal `\uD800` becomes U+FFFD. | 2 | ✅ |
| `HTTP-32` | SHOULD | One internal component encoder: `internal static class Rfc3986` with `EncodeComponent` and `DecodeComponent`. 2b's projection (`SEAM-27`) and 7c's splice reuse it. `Query.Builder` rejects lone surrogates, because `EscapeDataString` silently turns one into `%EF%BF%BD` (verified). | Design §3.5. The tests pin the behaviour, so a runtime change is caught. | `Rfc3986` (internal) | `Rfc3986Tests` vectors: space → `%20`, `+` → `%2B`, `~` unencoded, `*` → `%2A`, and decoding `+` gives `+`. | 2 | ✅ |
| `HTTP-33` | MUST | Already met in behaviour. `Parse` moves from `ToUpperInvariant` (Unicode-aware) to `System.Text.Ascii.EqualsIgnoreCase`, so the fold is ASCII-only, like `HTTP-13`'s. | Design §4.3. | `ProtocolExtensions.Parse` | Cite `Protocol_ParseAndWireRoundTrip`, widened to `HTTP/2`, `HTTP/2.0` and mixed case, under `tr-TR`, with `http/3` throwing. | 3 | ✅ |
| `HTTP-34` | MUST | New `sealed record RequestOptions`: `TimeSpan? Timeout`, `int? MaxRetries`, `ImmutableDictionary<string, string> Tags` (never `null`, keys normalised to `StringComparer.Ordinal`), and `static Empty`. Equality compares `Tags` by content. | Design §4.4. | `RequestOptions` | `Empty` has `null`, `null` and no tags. Build from an `ImmutableDictionary<,>.Builder`, mutate the builder, and assert the options are unchanged. `new RequestOptions() == RequestOptions.Empty`. | **1** | ✅ |
| `HTTP-35` | MUST | Validation lives in `field`-backed `init` accessors, so `with` cannot bypass it. A non-null `Timeout` must be positive, which also rejects `Timeout.InfiniteTimeSpan`. `MaxRetries` must be at least 0, and 0 means "no retries for this call". | Design §4.4. | `RequestOptions` | `Timeout` of zero, −1 tick or `InfiniteTimeSpan` throws `ArgumentOutOfRangeException`, through the initializer and through `with`; `null` is accepted; `MaxRetries` −1 throws and 0 is accepted. | **1** | ✅ |
| `HTTP-46` | MUST | `Request.Equals` compares `Method`, `Url.AbsoluteUri` (ordinal: userinfo and fragment count, with no DNS), `Headers` by value, and `Body` under position A. `GetHashCode` is consistent with it. | Design §4.2; position A. | `Request.Equals`, `RequestBody` variants | Same text gives equal requests. `https://a@h/` vs `https://b@h/` and fragment-only differences are unequal. `localhost` vs `127.0.0.1` are unequal. The body cases are listed under position A. | 6 | ✅ |
| `HTTP-47` | SHOULD | `Create(string)` and the constructor throw `ArgumentException` (`ParamName` `url`) for a malformed, relative, non-`http(s)` or Linux-`file:` input. The message carries the input **through `UrlRedactor`** (P2a-2), using one shared internal default instance, because `UrlRedactor.Redact` is an instance method. For the conformance inputs this still names the input: `::bad`, `/rel` and `ftp://h/x` carry no userinfo or query and redact to themselves, while `https://user:secret@h:bad/` becomes `[malformed url]` (all verified 2026-09-30). | Design §4.2 (the Linux `"/rel"` → `file:` trap). | `Request.Create`, `Request..ctor` | `::bad`, `/rel` and `ftp://h/x` each throw, and the message contains the redacted input. `https://user:secret@h:bad/` never shows `secret`. | 6 | ✅ |
| `HTTP-48` | SHOULD | New `sealed record ETag` with `Any`, `Strong(opaque)` and `Weak(opaque)`. Characters are `etagc`: `0x21`, `0x23`–`0x7E` and obs-text (`0x80`–`0xFF`). A strong opaque must not be empty; a weak one may be. `Parse` returns `null` for blank input and throws for an unterminated form or a lower-case `w/`. It renders exactly the accepted spelling, so the text round-trips. | Design §4.4. | `ETag` | Ported table in `tests/vectors/http/etag.json`. The obs-text interaction is P2a-3. | 8 | ✅ |
| `HTTP-49` | SHOULD | New `sealed record HttpRange` with `Bounded(offset, length)`, `Suffix(length)` and `From(offset)`. `Bounded` rejects a negative offset, a non-positive length and overflow of `offset + length - 1`. `Parse` accepts only the `bytes` unit (compared case-insensitively) and one range, rejects a comma, and keeps the text verbatim for `ToString`. Equality is semantic, over `Offset` and `Length`. | Design §4.4. | `HttpRange` | Ported table in `tests/vectors/http/http-range.json`, with `Bytes=0-9` round-tripping verbatim. | 8 | ✅ |
| `HTTP-50` | SHOULD | New `sealed record RequestConditions` with `IfMatch`/`IfNoneMatch` (`ImmutableArray<ETag>`) and `IfModifiedSince`/`IfUnmodifiedSince` (`DateTimeOffset?`). Its `init` accessors enforce that `*` is exclusive with concrete tags and collapse a repeated `*`. `ApplyTo` writes with `Set` (one comma-joined header per list, dates in `"R"` format, converted to UTC) and leaves unset members' headers untouched, so applying it twice is idempotent. | Design §4.4; `HTTP-50` needs no shared date parser from 5a. | `RequestConditions` | Assert the comma join, the `"R"` rendering of a `+03:00` time, that applying twice gives equal headers, that `[*, "a"]` throws, and that `[*, *]` becomes `[*]`. | 8 | ✅ |
| `HTTP-53` | MUST | `Parse` rejects a parameter with an empty raw value (`a=`). A quoted empty value (`a=""`) has a non-empty raw form and is accepted. The rejection of blank input, `text`, `/json` and `text/` stays. An empty segment (`text/plain;`) is still skipped, because RFC 9110's `*( OWS ";" OWS [ parameter ] )` allows it; it is not a parameter segment. | Design §4.4. | `MediaType.Parse` | A `[Theory]` of rejections and acceptances. The rows of `MediaTypeTryParseTests` and `MalformedContentTypeWireTests` keep their outcomes (verified: no accepted row has an empty value). | 3 | ✅ |
| `SEAM-29` | MUST | Immutability, factories and uniform required-field errors, exactly as `HTTP-1`, `HTTP-2` and `HTTP-4`. The shared generic `Builder` contract is **not built**: composition takes `Func<T, T>`. | Design §4; §10 `no-builder-objects`. | as `HTTP-2` | Cite the architecture tests of `HTTP-1`/`HTTP-2`, and the `HTTP-4` theory. | 6, 7, 9 | ✅ for immutability, factories and required fields; 🚫 for `IBuilder<T>`, citing §10 `no-builder-objects` |

---

## The three argued positions

### A. `HTTP-46`: what "body by value" means

**Position.** Request equality compares bodies **by value where the body's bytes are a construction-time fact, and
by identity where they are not**. Concretely:

- `RequestBody` itself keeps `object`'s reference equality. It is an open abstract class (design §4.5), and a
  third-party subclass about which the SDK knows nothing gets the only safe default.
- The private in-memory variant behind `FromBytes`, `FromString`, `FromValue<T>` and `ToReplayableAsync()`
  overrides `Equals` and `GetHashCode`. Two such bodies are equal when they are the same variant, their `ContentType`
  values are equal (`MediaType`'s value equality) and their bytes are `SequenceEqual`. The hash combines
  `ContentType` and `ContentLength` only. That is consistent, because equal bytes have equal lengths, and it keeps
  hashing O(1) even for a 10 MB payload. The O(n) byte comparison runs only after the type, media type and length
  already match.
- The single-use stream variant behind `FromStream` keeps identity. Two bodies over two open streams are two values
  even if they would yield the same bytes, and one body is equal to itself.
- Phase 3's variants (file, form-urlencoded, multipart, logging wrappers) each implement equality over the facts that
  fix their bytes, and identity over live sources. `RequestBody`'s XML remarks state this contract, so 3a and 3b
  inherit it as a written rule rather than rediscover it.

**Why not the alternatives.**

- **Identity for every body.** This is the as-built behaviour. It fails the requirement outright for the common
  case: `Request.Post(u, RequestBody.FromString("{}"))` built twice would be unequal. `HTTP-46` exists so that
  requests behave as values, for caching, retry bookkeeping and test assertions.
- **Content equality for every body, draining streams to compare.** Impossible without violating the body contract:
  a stream body is single-use (`StreamConsumedException` on the second read), and `Equals` would perform blocking
  I/O. The requirement's own rationale ("MUST NOT perform blocking work") forbids exactly that for URLs, and the
  same reasoning applies to bodies.
- **Value equality capped at a size, identity beyond it.** Ruby did this, because its `#hash` folds the bytes in.
  Here the hash does not read the bytes, and the comparison is a vectorised `SequenceEqual`, so a cap would add a
  surprising discontinuity with nothing to buy.

**Is it a deviation?** It reads the clause rather than departing from it. A stream body has no value except its
identity until it is consumed, and consuming it destroys it. It is recorded as P2a-1 so that design §11 can take
it as a spec-ambiguity resolution, as Ruby's phase 3b did for the same clause.

**Tests** (`RequestTests`, PR 6):

- two `FromString("{}")` requests are equal and hash equal, and work as a `Dictionary` key;
- different bytes, or equal bytes with different `ContentType`s, are unequal;
- `FromBytes` and `FromString` over the same bytes and media type are equal;
- two `FromStream` bodies over identical `MemoryStream`s are unequal;
- a request is equal to itself when it carries a stream body;
- `await body.ToReplayableAsync()` produces a body equal to a `FromBytes` over the same bytes.

### B. Whether to expose a public header-syntax check

**Position: yes, one narrow, non-throwing, allocation-free public predicate class. The throwing validators stay
internal.**

```csharp
namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>The header-syntax predicates the model validates with, for transports that re-check at the wire.</summary>
public static class HttpHeaderSyntax
{
    public static bool IsValidName(ReadOnlySpan<char> name);            // non-empty RFC 9110 token; no trimming
    public static bool IsValidOutboundValue(ReadOnlySpan<char> value);  // HTAB and 0x20–0x7E only (HTTP-18)
    public static bool IsValidInboundValue(ReadOnlySpan<char> value);   // no C0 but HTAB, no DEL; obs-text allowed (HTTP-19)
    public static string EscapeName(string name);                       // every non-token char as \uXXXX, for a log line (HTTP-20)
}
```

**For.**

1. Design §10 `construction-bypass` makes the wire re-check **mandatory** in every transport, because reflection can
   forge a model and `TryAddWithoutValidation` writes CR/LF to the wire. A mandatory check that each transport must
   restate is a drift bug. The reference adapter already restates both predicates and the escaper
   (`SystemNetHttpClient.HeaderSyntaxOnWire`, about 40 lines). Phase 1 routed "one public syntax predicate the adapter
   can call" to 2a for exactly this reason.
2. Third-party transports are the audience of the SPI (`SEAM-2`). `InternalsVisibleTo` cannot reach them, and
   `CLAUDE.md` restricts it to the library's own test project anyway.
3. The response side needs it too. The adapter must decide whether a received reason phrase is safe to put on
   `Response` (`HTTP-6`) without catching exceptions. With `IsValidInboundValue` the answer is one call.
4. Later phases gain one quotable source: phase 3's multipart part headers (`HTTP-51`), and 8a's conformance kit
   asserting that a transport drops what the predicate rejects.

**Against, and why it does not outweigh.**

- **It widens the public surface** (styleguide 10, "minimal public surface"). The widening is four static members
  with no state and no configuration, and each is the literal text of a MUST.
- **It becomes a contract that is hard to change.** That is the point: if the rule ever changes, every transport that
  calls it changes with it, which is what `HTTP-17`/`HTTP-18` single-sourcing wants.
- **Throwing variants would be more convenient for model-building callers.** Those callers already have `Headers`,
  `Headers.Builder` and `HttpHeaderName.Of`, which throw with the `HTTP-20`-safe messages. A second public throwing
  path would be two ways to do one thing.

**Consequences.** `SystemNetHttpClient.IsWireSafe` and `HeaderSyntaxOnWire` are replaced by calls to
`HttpHeaderSyntax`. The wire re-check itself stays, because design §10 requires it; only the source of its predicate
changes. The internal `HeaderSyntax` keeps the throwing validators and delegates the character classes to the same
private helpers, so there is one character table.

### C. The `Security` assertion `"x-trace"` → `"X-Trace"`

`HeaderInjectionValidationTests.Surrounding_whitespace_is_trimmed_from_a_name_before_validation` asserts
`Assert.Equal("x-trace", Assert.Single(headers.Names))` for the inputs `"  X-Trace  "` and `"\tX-Trace\t"`. After
the rebuild, `Names` yields original casing (`HTTP-21`), and the expected value becomes `"X-Trace"`. Constraint 5
makes deleting or loosening a `Security` test review-blocking. This change does neither, for five reasons.

1. **The property the test protects is unchanged and still asserted.** The test's name states it: trim, then
   validate. Its four assertions are:
   - lookup by the trimmed name finds the value (unchanged);
   - exactly one name is stored, with no whitespace-padded duplicate (unchanged, `Assert.Single`);
   - that name is exactly the trimmed text (changed expected value, same ordinal `Assert.Equal`);
   - `HttpHeaderName.Of(name).Original == "X-Trace"` (unchanged).

   No assertion is removed, and no comparison is weakened (no ordinal→ignore-case, no `Equal`→`Contains`).
2. **The new expected value is the specification's own.** `HTTP-17`'s conformance clause reads: "accept
   `"  X-Trace  "` storing `X-Trace`". The old value `"x-trace"` encoded the as-built lower-casing that `HTTP-21`
   removes, and the segmentation design names it as such.
3. **The new value is more discriminating, not less.** The security-relevant fact is that no whitespace survives
   into the stored name, and both values assert it. Against `"x-trace"`, an implementation that trims correctly and
   one that trims as a side effect of a lossy normalisation were indistinguishable. Against `"X-Trace"`, only an
   exact trim of the caller's text passes. Casing itself is not a security property: header names are
   case-insensitive (`HTTP-13`).
4. **No input the test rejected before is accepted now.** The test feeds only valid names. Every rejection lives in
   the `InvalidNames` theories, which the rebuild leaves byte-identical and which PR 5 extends to the typed
   overloads. That is a strengthening.
5. **It is made visibly.**
   - It lands in PR 5, the `Headers` rebuild's own change, which constraint 5 and the segmentation design ask to be
     separately reviewable.
   - It is the only `Security` assertion that changes value in 2a.
   - The PR description calls it out.
   - 2a's checklist records it in its "existing assertions changed" table, with this argument, as phase 1 did for
     its own.
   - PR 5 also **adds** `Assert.Equal(HttpHeaderName.Of("x-trace"), HttpHeaderName.Of(name))` and
     `Assert.Equal("v", headers.Get("x-trace"))` to the same test, so the folded lookup path is pinned beside the
     casing.

The wire-level Security classes (`HeaderInjectionWireTests`, `FramingHeaderDropWireTests`, `RedirectWireTests`)
already compare names case-insensitively (verified), so original casing on the wire changes none of their
assertions.

---

## Questions the segmentation design left open, answered

1. **What `Headers` enumerates.** Original casing: the casing of the first insertion under that folded name, in
   insertion order. `HTTP-21` needs original casing at the wire, and the adapter reads the enumeration. A caller who
   wants the folded form has `HttpHeaderName.Of(name).CanonicalName`. The enumerator stays
   `KeyValuePair<string, IReadOnlyList<string>>`, which is what a transport needs and avoids allocating an
   `HttpHeaderName` per entry. On the wire, `HttpClient` substitutes its own casing for the header names it knows
   (`Accept`, `Content-Type`, …), so the loopback proof uses a custom name. Mapping the known names is 8b's
   (`TRANSPORT-10`).
2. **Where `HTTP-9`'s internal set lives.** In `RetryFacts`, created by 2a with one member (`IdempotentMethods`) and
   grown by 6a (design §6.1), and read through the internal `Method.IsIdempotent`. The model therefore reads an
   internal type from `Pipeline/Policies`. That is an inward reference inside one assembly, accepted in order to
   keep design §6.1's single class rather than create a second home that 6a would have to merge.
3. **`HTTP-8`'s N/A.** Argued in the table from design §4.2.

---

## Type shapes

These are the public surfaces 2a leaves behind. Every member carries `///` XML docs, and every breaking member says
**Breaking** in them. Implementation-only members are omitted, except where a row depends on an `internal` one.

### `Headers` and `Headers.Builder`

```csharp
namespace Dexpace.Sdk.Core.Http.Common;

public sealed class Headers : IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>, IEquatable<Headers>
{
    public static Headers Empty { get; }
    public int Count { get; }                                  // distinct names
    public IReadOnlyList<string> Names { get; }                // Breaking: was IEnumerable<string>; original casing, insertion order
    public bool Contains(string name);
    public bool Contains(HttpHeaderName name);
    public string? Get(string name);
    public string? Get(HttpHeaderName name);
    public IReadOnlyList<string> GetAll(string name);
    public IReadOnlyList<string> GetAll(HttpHeaderName name);
    public Headers With(string name, string value);            // append
    public Headers With(HttpHeaderName name, string value);
    public Headers Set(string name, string? value);            // Breaking: value nullable; null removes (HTTP-15)
    public Headers Set(HttpHeaderName name, string? value);
    public Headers Without(string name);
    public Headers Without(HttpHeaderName name);
    public Builder ToBuilder();
    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator();  // original casing, insertion order
    public bool Equals(Headers? other);                        // ordered (folded name, values); casing excluded
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public override string ToString();                         // "Headers[Accept, X-Trace]": names only, never values
    public static bool operator ==(Headers? left, Headers? right);
    public static bool operator !=(Headers? left, Headers? right);

    public sealed class Builder
    {
        public Builder();
        public Builder Add(string name, string value);
        public Builder Add(HttpHeaderName name, string value);
        public Builder AddInbound(string name, string value);  // HTTP-19; keeps the sender's casing
        public Builder Set(string name, string? value);        // Breaking: value nullable
        public Builder Set(HttpHeaderName name, string? value);
        public Builder Remove(string name);
        public Builder Remove(HttpHeaderName name);
        public Headers Build();                                // deep copy; later edits never reach the result
    }
}
```

**Storage.** An `ImmutableArray` of entries `(string Original, string Folded, ImmutableArray<string> Values)`, plus a
private `Dictionary<string, int>` (ordinal) from the folded name to the entry index, built once and never exposed.
`Names` is an `ImmutableArray<string>` built with the instance. Equality is order-sensitive across names, as design
§4.1 fixes: two sets are equal when they would serialise identically, apart from name casing. Header signing and
caching need that determinism, and it matches `Query`'s `HTTP-30` rule.

### `HttpHeaderName`

```csharp
namespace Dexpace.Sdk.Core.Http.Common;

public sealed record HttpHeaderName                            // Breaking: was readonly record struct
{
    public string Original { get; }                            // trimmed caller spelling; the wire form
    public string CanonicalName { get; }                       // ASCII-folded lower case; equality and hash
    public static HttpHeaderName Of(string name);              // trim SP/HTAB, RFC 9110 token (HTTP-17)
    public bool Equals(HttpHeaderName? other);                 // ordinal over CanonicalName
    public override int GetHashCode();
    public override string ToString();                         // Breaking: returns Original (was CanonicalName)

    public static class WellKnown
    {
        // existing: Accept, Authorization, ContentLength, ContentType, Date, ETag, Location, RetryAfter, UserAgent
        public static HttpHeaderName IdempotencyKey { get; }
        public static HttpHeaderName IfMatch { get; }
        public static HttpHeaderName IfNoneMatch { get; }
        public static HttpHeaderName IfModifiedSince { get; }
        public static HttpHeaderName IfUnmodifiedSince { get; }
        public static HttpHeaderName Range { get; }
    }
}
```

The member names `Original` and `CanonicalName` are kept, so that no call site churns for a rename. Five of the six
new `WellKnown` entries are the ones 2a's own code stamps (`IdempotencyPolicy`, `RequestConditions`'s four `If-*`
names). `Range` is stamped by nothing in 2a, because `HttpRange` has no `ApplyTo`; it is added beside `HttpRange` so a
caller writes `headers.Set(HttpHeaderName.WellKnown.Range, range.ToString())`, and it could be dropped without
touching any row.

### `HttpHeaderSyntax`

The surface is in [position B](#b-whether-to-expose-a-public-header-syntax-check).

### `Method` and the internal `RetryFacts`

```csharp
namespace Dexpace.Sdk.Core.Http.Common;

public sealed record Method                                    // Breaking: was readonly record struct
{
    public string Name { get; }                                // wire token; well-known = upper-case name
    public static Method Get { get; }                          // … Head, Post, Put, Patch, Delete, Options, Trace, Connect
    public static Method Of(string token);                     // Breaking: rejects a non-token (was: any non-blank string)
    public override string ToString();                         // Name
    internal bool IsIdempotent { get; }                        // RetryFacts.IdempotentMethods.Contains(this)
    internal bool ForbidsBody { get; }                         // GET, HEAD, TRACE, CONNECT (HTTP-7)
    // Breaking: the public IsSafe and IsIdempotent are removed (HTTP-9, §11 item 22)
}

namespace Dexpace.Sdk.Core.Pipeline.Policies;

internal static class RetryFacts
{
    internal static FrozenSet<Method> IdempotentMethods { get; }   // {GET, HEAD, OPTIONS, PUT, DELETE}; 6a grows this class
}
```

### `Status`

```csharp
namespace Dexpace.Sdk.Core.Http.Response;

public readonly record struct Status                           // unchanged kind: default (code 0) is a legitimate unrecognised status
{
    // existing: Code, Name, IsInformational, IsSuccess, IsRedirect, IsClientError, IsServerError, FromCode, the named statics
    public bool IsError { get; }                               // 400–599 (HTTP-11)
    public static bool TryGetKnown(int code, out Status status);   // HTTP-10's separate lookup
}
```

### `Request`

```csharp
namespace Dexpace.Sdk.Core.Http.Request;

public sealed record Request
{
    public Request(Method method, Uri url, Headers? headers = null, RequestBody? body = null);  // HTTP-4, HTTP-7, HTTP-47
    public Method Method { get; }                              // Breaking: init removed (all four)
    public Uri Url { get; }
    public Headers Headers { get; }
    public RequestBody? Body { get; }
    public static Request Create(Method method, string url, Headers? headers = null, RequestBody? body = null);
    public static Request Get(string url);
    public static Request Post(string url, RequestBody body);
    public Request WithMethod(Method method);                  // throws if the current body is forbidden for method
    public Request WithUrl(Uri url);
    public Request WithHeaders(Headers headers);
    public Request WithHeader(string name, string value);      // append
    public Request WithHeader(HttpHeaderName name, string value);
    public Request WithBody(RequestBody body);
    public Request WithoutBody();
    public bool Equals(Request? other);                        // HTTP-46; position A
    public override int GetHashCode();
    public override string ToString();                         // "GET https://h/p?q=***": method + UrlRedactor output; never headers or body
}
```

`ToString` is overridden because the record-generated one prints the raw `Url`, userinfo and query tokens included.
Overriding it costs one line, and `Equals` has to be overridden anyway. No requirement row owns this, so it is called
out for review.

### `Response`

```csharp
namespace Dexpace.Sdk.Core.Http.Response;

public sealed class Response : IAsyncDisposable, IDisposable
{
    public Response(                                           // Breaking: new signature (HTTP-4, HTTP-6)
        Request.Request request,
        Status status,
        Protocol protocol,
        Headers? headers = null,
        ResponseBody? body = null,
        string? reasonPhrase = null);                          // null, or passes HttpHeaderSyntax.IsValidInboundValue
    public Request.Request Request { get; }
    public Status Status { get; }
    public Protocol Protocol { get; }
    public string? ReasonPhrase { get; }
    public Headers Headers { get; }
    public ResponseBody Body { get; }                          // never null; absent = empty buffered body (P2a-4)
    public bool IsInformational { get; }                       // + IsSuccess (existing), IsRedirect, IsClientError, IsServerError, IsError
    public Response WithBody(ResponseBody body);               // new Response owning body; this one keeps its own
    // unchanged: EnsureSuccessAsync, MaxBufferedErrorBytes, Dispose, DisposeAsync
}
```

`Response` is the one model type that offers only `WithBody` as a derivation. A `WithHeaders` would leave two
`Response` objects owning one body, and both would dispose it. Every other field is re-derivable through the
constructor from the accessors, which is what §10 `no-builder-objects` covers for `HTTP-3`. The property `Request`
shares its name with the namespace `Dexpace.Sdk.Core.Http.Request`. Inside `Response`, the type is written
`Request.Request`, as above. That is legal, and `RetryPolicy` already spells the type `Http.Request.Request` for the
same reason.

### `Query`

```csharp
namespace Dexpace.Sdk.Core.Http.Request;

public sealed class Query : IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>, IEquatable<Query>
{
    public static Query Empty { get; }
    public int Count { get; }                                  // distinct names
    public IReadOnlyList<string> Names { get; }                // first-appearance order
    public bool Contains(string name);                         // ordinal
    public string? Get(string name);                           // first value; "" for ?flag; null when absent
    public IReadOnlyList<string> GetAll(string name);
    public string Encode();                                    // RFC 3986, no leading '?', "" when empty
    public static Query Parse(string? query);                  // total, lenient (HTTP-31)
    public Builder ToBuilder();
    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator();
    public bool Equals(Query? other);                          // order-sensitive; ⇔ Encode() equal (HTTP-30)
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public override string ToString();                         // names only; values may be secrets, and Encode() is explicit
    public static bool operator ==(Query? left, Query? right);
    public static bool operator !=(Query? left, Query? right);

    public sealed class Builder
    {
        public Builder();
        public Builder Add(string name, string? value);        // null → ""; rejects an empty name or a lone surrogate
        public Builder Set(string name, string? value);        // null → "" (a flag), as Add; unlike Headers.Set, null never removes: use Remove
        public Builder Set(string name, IEnumerable<string?> values);  // an empty sequence drops the name at Build (HTTP-30)
        public Builder Remove(string name);
        public Query Build();
    }
}

internal static class Rfc3986                                  // Http/Request/Rfc3986.cs; the one component encoder (HTTP-32)
{
    internal static string EncodeComponent(string value);
    internal static string DecodeComponent(string value);      // lenient; '+' stays '+'
}
```

`Query` is **not** a member of `Request`: `HTTP-6` says a request carries exactly method, URL, headers and body.
Splicing a query into a `Uri` is 2b's projection's job (`SEAM-27`). `Uri`'s own canonicalisation unescapes `%7E`
to `~` (verified), and that belongs in the one place that assembles URLs.

### `RequestOptions`

This is PR 1, and 2b's only hard gate.

```csharp
namespace Dexpace.Sdk.Core.Http.Request;

public sealed record RequestOptions
{
    public static RequestOptions Empty { get; }                // override nothing
    public TimeSpan? Timeout { get; init; }                    // null, or > TimeSpan.Zero (HTTP-35)
    public int? MaxRetries { get; init; }                      // null, or >= 0; 0 = no retries for this call
    public ImmutableDictionary<string, string> Tags { get; init; }  // never null; keys re-based on StringComparer.Ordinal
    public RequestOptions WithTag(string key, string value);
    public bool Equals(RequestOptions? other);                 // Tags compared by content
    public override int GetHashCode();
}
```

The three accessors are `field`-backed: `init => field = RequireNullOrPositive(value)`, and so on (design §4). The
upper bound of `Timeout` against a transport's ceiling (`CancelAfter`'s about 24.8 days) is 8b's `TRANSPORT-5`, not a
model rule.

### `ETag`, `HttpRange`, `RequestConditions`

```csharp
namespace Dexpace.Sdk.Core.Http.Common;

public sealed record ETag
{
    public static ETag Any { get; }                            // *
    public static ETag Strong(string opaque);                  // non-empty etagc
    public static ETag Weak(string opaque);                    // etagc, may be empty
    public static ETag? Parse(string? value);                  // null for null/blank; ArgumentException when malformed
    public static bool TryParse(string? value, [NotNullWhen(true)] out ETag? etag);
    public bool IsAny { get; }
    public bool IsWeak { get; }
    public string Opaque { get; }                              // without quotes; "" for Any
    public override string ToString();                         // *, "x", W/"x"
}

public sealed record HttpRange
{
    public static HttpRange Bounded(long offset, long length); // bytes=o-(o+l-1)
    public static HttpRange Suffix(long length);               // bytes=-l
    public static HttpRange From(long offset);                 // bytes=o-
    public static HttpRange Parse(string value);               // bytes only, one range, verbatim text kept
    public static bool TryParse(string? value, [NotNullWhen(true)] out HttpRange? range);
    public long? Offset { get; }                               // null for a suffix range
    public long? Length { get; }                               // null for an open-ended range
    public bool Equals(HttpRange? other);                      // semantic: Offset and Length
    public override int GetHashCode();
    public override string ToString();                         // the verbatim or canonical text
}

namespace Dexpace.Sdk.Core.Http.Request;

public sealed record RequestConditions
{
    public static RequestConditions None { get; }
    public ImmutableArray<ETag> IfMatch { get; init; }         // default → empty; * exclusive; repeated * collapsed
    public ImmutableArray<ETag> IfNoneMatch { get; init; }
    public DateTimeOffset? IfModifiedSince { get; init; }
    public DateTimeOffset? IfUnmodifiedSince { get; init; }
    public Headers ApplyTo(Headers headers);                   // Set, never With; unset members untouched
    public Request ApplyTo(Request request);
    public bool Equals(RequestConditions? other);              // sequence equality over the arrays
    public override int GetHashCode();
}
```

`ImmutableArray<T>`'s own equality compares the underlying array reference, so a record's generated equality would
be wrong for both lists. Hence the explicit `Equals`. For the same reason, `HttpRange` overrides the generated
equality, which would otherwise include its private verbatim text.

---

## Migration plan from the as-built code

The migration follows [the landing order](#landing-order). Counts are at `bf8f4ae` and are recounted by the plan.

| Change | Sites | Mechanical rewrite |
|---|---|---|
| `Request` `with { … }` stops compiling (CS0200) | 10 in `src/`: `AuthorizationPolicy.cs` (2), `ClientIdentityPolicy.cs`, `IdempotencyPolicy.cs`, `InstrumentationPolicy.cs`, `RedirectPolicy.cs`, `SetDatePolicy.cs`, `PaginationStrategies.cs` (3). 19 in `tests/`, in the nine files listed in the segmentation design's as-built fact 2 (5 single-line, 14 multi-line) | `with { Headers = h }` → `WithHeaders(h)`; `with { Url = u }` → `WithUrl(u)`. `PaginationStrategies.cs:138`'s comment about `with` bypassing the constructor is deleted, because `WithUrl` now validates, but the scheme guard under it **stays**: it ends pagination (`null`) on a hostile `Link`, where `WithUrl` would throw. `RedirectPolicy.cs:162` sets all four fields and is the row below |
| `RedirectPolicy`'s hop rewrite | `RedirectPolicy.cs:162` (`with { Url, Method, Headers, Body }`) | One `new Request(newMethod, newUrl, newHeaders, dropBody ? null : request.Body)`, never a `With*` chain: `WithMethod(Method.Get)` before the body is cleared would throw `HTTP-7` on a 303 or a 301/302-on-POST. And the policy has no scheme check on `Location` (only `Uri.TryCreate` and the downgrade guard), so today `Location: ftp://…` or `mailto:` builds a non-http request through `with`; after PR 6 the validating constructor would throw `ArgumentException` **after** the 3xx has been disposed. PR 6 therefore adds the http(s) check before the dispose and returns the 3xx unfollowed, as it already does for a malformed `Location`. Before 2a the non-http hop reached the transport and failed there (`HttpClient` rejects the scheme); returning the 3xx is `REDIR-18`'s behaviour, so it is listed as breaking item 8's behaviour change, and the row itself stays 6b's |
| `new Response(…)` gains `request` and a required `protocol` | 2 in `src/` (`SystemNetHttpClient.cs:332`, `Response.EnsureSuccessAsync`); 138 `new Response(` in `tests/` across `Dexpace.Sdk.Core.Tests`, `Dexpace.Sdk.Serialization.SystemTextJson.Tests` (2 files), `TestSupport` (`RecordingTransport`, `RecordingSyncTransport`) and `AotSmoke/SmokeChecks.cs`, plus 3 target-typed `new(Status…)` sites (`PaginationStrategiesTests.cs:32`, `:36`, and `TestSupport/Transports/TestResponses.cs:16`) that a `new Response(` grep misses; the compiler finds them all | The adapter threads the `Request` it is executing into `ToResponse` and passes it, with `message.ReasonPhrase` when `HttpHeaderSyntax.IsValidInboundValue` accepts it, otherwise `null`. `EnsureSuccessAsync` uses `WithBody(buffered)`. The existing `TestSupport` class `TestResponses` (today one factory, `Redirect`, on the old constructor) gains `Create(Status, Request? request = null, …)`, which supplies `Request.Get("https://example.test/")` when the test does not care, and `Redirect` moves to the new constructor. Tests that do care, and every `Security` class, pass the real request |
| `Method.IsIdempotent` becomes internal | `RetryPolicy.cs:171` | Unchanged source (same name, now internal). `MethodAndStatusTests.Method_SafetyAndIdempotency` is rewritten against the internal set and its `IsSafe` lines are removed. It is a `Unit` test, so constraint 5 does not apply |
| `HttpHeaderName` becomes a class | `ApiKeyCredential(HttpHeaderName? header = null, …)`: the `?` changes meaning from `Nullable<T>` to a nullable reference, which is a public-API line change; `AuthorizationPolicy.WithheldHeaderName` and its three overrides | Source-compatible (`ApiKeyCredential` already writes `header ?? HttpHeaderName.WellKnown.Authorization`). Stamping and lookup sites move from `HttpHeaderName.WellKnown.X.Original` to the typed overloads: `SetDatePolicy`, `ClientIdentityPolicy`, `RedirectPolicy`, `RetryPolicy`, and `AuthorizationPolicy`'s `Without(WithheldHeaderName)`. `IdempotencyPolicy` moves to `WellKnown.IdempotencyKey`. `BasicAuthPolicy`, `BearerTokenAuthPolicy` and `ApiKeyAuthPolicy` keep `.Original`: they return the name through the protected `GetCredentialAsync`'s `(string HeaderName, string HeaderValue)` tuple, public surface that 2a does not change (the auth resolver is 6c's) |
| `Headers` enumerates original casing | `SystemNetHttpClient.ToHttpRequestMessage` | Its framing set and its `content-type` check are already `OrdinalIgnoreCase` (verified), so there is no change beyond the predicate swap (position B) |
| Adapter predicates | `SystemNetHttpClient.IsWireSafe`, `HeaderSyntaxOnWire` | Replaced by `HttpHeaderSyntax`; the private class is deleted |
| `MediaType.Parse` rejects `a=` | none in `src/` | Inbound `Content-Type` values with an empty parameter become "no media type" through `TryParse`, the phase-1 lenient path |

**`Security` classes kept green** (constraint 5):

- **By behaviour:** `HeaderInjectionValidationTests` (one expected value changes, position C) and
  `HeaderInjectionWireTests` (no change).
- **By mechanical edit only**, with no assertion changed:
  - `EnsureSuccessErrorMappingTests`, `RetryPacingOverflowTests`, `AuthHttpsGuardTests`,
    `RedirectCredentialHygieneTests` and `ReDriveRequestIsolationTests`, where `new Response(…)` gains its request;
  - `ReDriveRequestIsolationTests` again, where `with` becomes a `With*` call;
  - `MediaTypeTryParseTests` and `MalformedContentTypeWireTests`, whose rows all keep their outcomes.

The 2a checklist lists every edited `Security` file, with the diff kind for each.

---

## Breaking changes

Each item is marked **Breaking** in the XML docs of the member it changes, and appears as a `CHANGELOG.md`
`[Unreleased]` line in the PR that makes it (constraint 8).

| # | Change | Kind | PR |
|---|---|---|---|
| 1 | `Method` changes from `readonly record struct` to `sealed record`, so `default(Method)` and `Method?` as `Nullable<T>` no longer exist | binary and source | 4 |
| 2 | `Method.IsSafe` and `Method.IsIdempotent` are removed from the public surface | source | 4 |
| 3 | TRACE is no longer retried by `RetryPolicy` (it is no longer in the idempotent set) | behaviour | 4 |
| 4 | `Method.Of` rejects a non-token (`"GET\r\nX: y"`, `"FOO BAR"`) with `ArgumentException` | behaviour | 4 |
| 5 | `HttpHeaderName` changes from `readonly record struct` to `sealed record`; `ToString()` returns `Original`; `ApiKeyCredential`'s `HttpHeaderName?` parameter becomes a nullable reference | binary, source and behaviour | 5 |
| 6 | `Headers` enumerates and lists original casing in insertion order; `Names` becomes `IReadOnlyList<string>`; `Set` takes `string?` and `null` removes; `Equals`/`==` become value equality; a non-ASCII lookup name no longer folds (`K` ≠ `k`) | binary and behaviour | 5 |
| 7 | `Request`'s four properties lose `init`, so `with { X = … }` no longer compiles | source | 6 |
| 8 | `Request` rejects a body on GET, HEAD, TRACE and CONNECT; equality uses `Url.AbsoluteUri` ordinally with by-value bodies; `ToString()` redacts; URL errors carry the redacted input; `RedirectPolicy` returns a 3xx whose `Location` is not http(s) unfollowed instead of sending the hop | behaviour | 6 |
| 9 | `Response`'s constructor takes `(Request, Status, Protocol, Headers?, ResponseBody?, string?)`, and `protocol` has no default | binary and source | 7 |
| 10 | `MediaType.Parse` rejects a parameter with an empty raw value; `MediaType.Charset` returns `null` for `utf-7` instead of throwing | behaviour | 3 |

Additive only, with no **Breaking** marker: `RequestOptions`, `Query`, `ETag`, `HttpRange`, `RequestConditions`,
`HttpHeaderSyntax`, `Status.IsError`/`TryGetKnown`, the `Response` classifications, the typed overloads, the new
`WellKnown` names, and `Protocol.Parse`'s ASCII fold (no input that parsed before stops parsing).

---

## Landing order

Each step is one pull request carrying its code **and** its tests together. Splitting code from tests would leave a
red commit for every breaking change, which is the exception roadmap step 5 allows (so the stack is code-and-tests →
docs, not code → tests → docs). Each PR carries its `PublicAPI.Unshipped.txt` diff (core, and `SystemNet` where
touched) and its `CHANGELOG.md` line.

| PR | Content | Rows | Depends on | Notes |
|---|---|---|---|---|
| **1** | `RequestOptions` | `HTTP-34`, `HTTP-35` | — | **First.** 2b's only hard gate: once merged, 2b's SPI signature work may start. Purely additive |
| **2** | `Query`, `Query.Builder`, internal `Rfc3986`; `tests/vectors/http/{query,rfc3986}.json`, plus the vector infrastructure none of which exists yet (a `TestSupport` loader and the `Core.Tests` csproj item that copies `tests/vectors/**` to the output directory) | `HTTP-28`–`HTTP-32` | — | Early, so 2b's projection can use one RFC 3986 renderer (a convenience for 2b) |
| **3** | `Status` (`IsError`, `TryGetKnown`), `Protocol.Parse`'s ASCII fold, `MediaType` (`HTTP-24`, `HTTP-53`, pins); `tests/vectors/http/media-type.json` | `HTTP-10`–`HTTP-12`, `HTTP-23`–`HTTP-27`, `HTTP-33`, `HTTP-53` | — | Independent of 1 and 2, and may be reviewed in parallel |
| **4** | `Method` as a `sealed record`, token-validated `Of`, `ForbidsBody`, `RetryFacts`, `RetryPolicy`'s read; the TRACE behaviour test | `HTTP-9` | — | Breaking 1–4 |
| **5** | `HttpHeaderName` as a `sealed record`; the `Headers` rebuild with typed overloads; `HttpHeaderSyntax`; the adapter's predicate swap; `HeaderCasingWireTests`; the position C change; the `docs/first-release.md` `HTTP-22` entry | `HTTP-13`–`HTTP-22` | — | Separately reviewable, carrying phase 1's S1 obligations (constraint 5). Breaking 5–6 |
| **6** | `Request`: get-only, validating constructor, `With*`, equality with position A's body semantics, `HTTP-47` messages, `ToString`; `RequestBody` equality contract; the `with` migrations in `Pipeline/`, `Pagination/` and tests, including `RedirectPolicy`'s scheme check (see the migration table) | `HTTP-6` (request half), `HTTP-7`, `HTTP-8`, `HTTP-46`, `HTTP-47` | 4 (`ForbidsBody`), 5 (`Headers` equality) | Breaking 7–8 |
| **7** | `Response`: new constructor, `Request`, `ReasonPhrase`, classifications, `WithBody`; the adapter; `EnsureSuccessAsync`; `TestResponses`; the 138 test sites | `HTTP-4`, `HTTP-6` (response half), `HTTP-11` (response half) | 6 (convenience: the same test files) | Breaking 9. **Sequenced against 2b's SPI PR**, which edits the same files (`SystemNetHttpClient`, `TestSupport`, `AotSmoke`). Whichever is ready first lands first, and the other rebases. That is a convenience, not a gate |
| **8** | `ETag`, `HttpRange`, `RequestConditions`; `tests/vectors/http/{etag,http-range}.json` | `HTTP-48`–`HTTP-50` | 5 (`Set` with typed names), 6 (`ApplyTo(Request)`) | May land in parallel with 7 |
| **9** | `ModelImmutabilityArchitectureTests` and `ModelConstructionArchitectureTests`; the user page `docs/sdk-documentation/http.md`; the 2a checklist; the roadmap status note | `HTTP-1`–`HTTP-3`, `HTTP-5`, `SEAM-29` (closing) | 1–8 | The architecture tests need every type to exist. The docs close the phase (roadmap step 7) |

---

## Tests, vectors and ports

- **Vectors extracted** to `tests/vectors/http/`: `media-type.json` (`HTTP-23`, `HTTP-25`, `HTTP-53`),
  `query.json` and `rfc3986.json` (`HTTP-29`–`HTTP-32`), `etag.json` (`HTTP-48`), and `http-range.json`
  (`HTTP-49`). They are loaded by `System.Text.Json` in the test project and copied to the output directory. Each
  file names its sibling source (path and sha) in a top-level `"source"` field. Header tables stay inline: S1
  already ported them into `HeaderInjectionValidationTests`, and they are not ported twice.
- **Ported, with a header comment citing path and sha:**
  - from `nodejs-sdk@54aeed4`, `packages/core/src/http/{headers,media-type,query-params,rfc3986,etag,http-range,request-conditions,request-options,method,status,protocol,request,response,ascii-validation}.test.ts` (the last is
    the source for `HttpHeaderSyntax`'s tables);
  - from `ruby-sdk@5b17395`, `gems/dexpace-core/test/dexpace/http/{headers_test,header_name_test,header_syntax_test,media_type_test,method_test,protocol_test,query_test,percent_encoding_test,request_test,request_options_test,response_test,status_test}.rb`
    and the `builder_test.rb` files under `http/{headers,query,request,request_options,response}/`. Ruby has no
    `ETag`, `HttpRange` or `RequestConditions` tests, so Node is the only source for those three;
  - `model_test.rb`, less its generic-builder cases.

  Node's `requireWellFormed` surrogate cases port onto `Query.Builder`.
- **Not ported:** host-language facts (Ruby encoding tags, Node runtime probes), and every generic-`Builder` case
  (§10 `no-builder-objects`).
- **Categories:** `Unit` throughout; `Integration` for `HeaderCasingWireTests`; `Security` only for the existing
  classes. 2a adds no new `Security` class, because it fixes no phase-1 defect, and each suite's
  `TestCategoryTests` enforces the trait.

---

## Coupling with later phases

These are the facts 2a fixes now that a later phase builds on. The segmentation design lists them. The 2a-specific
additions are:

- **3a and 3b** inherit position A's `RequestBody` equality contract for the file, form, multipart and logging
  variants. They also inherit `HttpHeaderSyntax`, for multipart part headers (`HTTP-51`).
- **4c** carries caller `RequestOptions` on the call-scoped context (`PIPE-26`).
- **6a** grows `RetryFacts`, and consumes `RequestOptions.MaxRetries = 0`.
- **7c** may reuse `Rfc3986` for `PAGE-22`'s splice.
- **8a** asserts `HttpHeaderSyntax` against third-party transports.
- **8b** maps well-known header casing (`TRANSPORT-10`) and bounds `RequestOptions.Timeout` (`TRANSPORT-5`).

---

## Rulings (2026-09-30)

The lead ruled on the four judgement calls this design left open. Each design default stands:

1. **P2a-2**: `HTTP-47`'s "carrying the offending input" is satisfied by the input as `UrlRedactor` renders it. The
   verbatim form was declined because it leaks userinfo passwords and query tokens into exception messages. The design
   §10 entry is written in PR 9.
2. **`Request.ToString()`**: overridden to print the method and the redacted URL. The record-generated `ToString`, which
   prints the raw URL, was declined. No requirement row owns this; `OBS-11` and `XCUT-19` are its spirit.
3. **P2a-3**: the `HTTP-48`/`HTTP-18` interaction becomes a numbered design §11 item. An obs-text `ETag` parses, and
   `RequestConditions.ApplyTo` throws when asked to send it, because the outbound rule wins.
4. **`Method.Of` case folding** (`HTTP-9`): `Of` keeps mapping `get`, `Get` and so on to the cached upper-case
   instances, as the as-built code, design §4.3 and .NET's `HttpMethod.Parse` do. Every other token stays verbatim.
   `Method`'s class remark, which cites case sensitivity beside a folding `Of`, is reworded in PR 4 to state this rule.

---

## Deviation Ledger

| ID | Decision | Touches | Kind | Argued in | Route |
|---|---|---|---|---|---|
| P2a-1 | "Body by value" is read as value equality over the bytes of an in-memory body, and identity for single-use stream bodies and for unknown subclasses. The hash excludes the bytes | `HTTP-46` | a reading, not a departure: a spec ambiguity resolution | [Position A](#a-http-46-what-body-by-value-means) | Design §11 candidate, dated correction |
| P2a-2 | `HTTP-47`'s "argument error carrying the offending input" carries the input as `UrlRedactor` renders it: userinfo and query values masked, and `[malformed url]` when it cannot be parsed safely | `HTTP-47` (SHOULD), `OBS-11`, `XCUT-19` | judged (P10): log and secret hygiene outrank the letter of a SHOULD | this document's `HTTP-47` row | Design §10 entry, dated correction (ruled 2026-09-30) |
| P2a-3 | An `ETag` holding obs-text is valid (`HTTP-48`), but `RequestConditions.ApplyTo` cannot emit it: `Headers.Set` enforces `HTTP-18`'s outbound ASCII rule and throws. The outbound rule wins | `HTTP-48`, `HTTP-50`, `HTTP-18` | a spec interaction the chapters do not address | this document's `HTTP-48` and `HTTP-50` rows | Design §11 numbered item (ruled 2026-09-30) |
| P2a-4 | `HTTP-6`'s "optional body" on a response is optional at construction and never `null` on read: an absent body is an empty buffered body | `HTTP-6` | mechanism (the as-built behaviour, kept) | this document's `HTTP-6` row | Design §10 entry, or dropped if the lead reads "optional" as met |
