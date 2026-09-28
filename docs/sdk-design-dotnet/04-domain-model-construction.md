## 4. Domain Model Construction

**HTTP-1**, **HTTP-2**/**SEAM-29** and **XCUT-15** require every core domain type to be immutable and safe to share
after construction, constructed only through an immutable value plus a builder or factory, with no public
field-wise constructor and no unchecked copy bypassing validation. **HTTP-3** requires a pre-filled, non-aliasing
derivation; **HTTP-4** requires `build()` to validate required fields and fail with a field-named error;
**HTTP-5** requires accessors to isolate the caller from both the model's internals and a still-live builder.

**`record` is the base, and `with` is the builder — where it is not a bypass.** C# gives the value-object contract
for free: a `record` (or `readonly record struct`) generates value equality, a consistent `GetHashCode`, a readable
`ToString`, and a non-destructive copy through `with`; `init` accessors make a property settable only during
construction; `required` makes an omitted member a compile error (verified: CS9035), which is strictly stronger than
**HTTP-4**'s runtime "`<name>` is required" for the fields it covers. `CLAUDE.md`'s conventions already say records,
mutation via `with` or `With*` helpers, and no builder-as-object types, and this chapter keeps that — with one
correction that the as-built code shows is load-bearing. **`with` and object initializers run `init` accessors, not
constructors**, so validation written in a constructor is skipped by every derived copy. Verified at `d45e64b`:
`Request.Post(url, body) with { Method = Method.Get }` produces a GET carrying a body — the combination **HTTP-7**
forbids, and the very example **HTTP-2**/**SEAM-29**'s conformance clause names ("deriving a request cannot install a
body-carrying GET"); `with { Url = new Uri("rel", UriKind.Relative) }` produces a request with a relative URL; and
`with { Url = new Uri("ftp://h/x") }` an FTP one — the last two rejected by the constructor they never ran, the first
by no code at all (§4.2). The port's rule has two halves:

- **Per-member invariants live in the `init` accessor**, using C# 14's `field` keyword so the check runs on every
  assignment path — constructor, object initializer and `with`:
  `public TimeSpan? Timeout { get; init => field = RequireNullOrPositive(value); }`. Verified: this compiles for a
  `net8.0` target under the 10.0.401 SDK with `LangVersion` `latest`, so the idiom does not wait for the floor to
  rise.
- **A type with a cross-member invariant exposes no public `init`.** An `init` accessor cannot check a relationship
  between members, because `with { Method = Method.Get, Body = null }` assigns one member at a time: the `Method`
  setter would see the *old* body and reject a perfectly legal result, or, checking the other way, accept an illegal
  one. So `Request`, whose **HTTP-7**/**HTTP-8** rules relate method and body, has get-only properties (a `with`
  expression that assigns one is a compile error, verified CS0200), a validating constructor, and `With*` methods
  (`WithMethod`, `WithUrl`, `WithHeaders`, `WithHeader`, `WithBody`, `WithoutBody`) that route every derivation back
  through that constructor.

**Which kind of type, per model.** `docs/styleguide/csharp/03-nullability-and-the-type-system.md` rule 3.8 says to
choose `record`, `record struct` or `class` by value semantics, and rule 6.6 of `06-types-and-data-modeling.md`
prefers `readonly struct` for small immutable values. For value types the deciding question on this host is a P13 one:
**what does `default(T)` mean?** Every struct has a parameterless construction path the author cannot remove —
verified, `new Method()` compiles and yields a `Method` whose `Name` is `null`, and
`default(HttpHeaderName).GetHashCode()` throws `NullReferenceException` — so a struct whose all-zero value is
*invalid* reopens exactly the unvalidated construction path **HTTP-2** closes. The rule is: a model is a `readonly
record struct` only when its default value is a legitimate instance; otherwise it is a `sealed record` class, whose
default is `null`, which nullable reference types already police.

| Model | Kind | Why |
|---|---|---|
| `Status` | `readonly record struct` | `default` is code 0, which **HTTP-10**'s total mapping already admits as an unrecognised status; equality is overridden to the code |
| `Protocol` | `enum` (explicit underlying type) | closed set with a total wire mapping; an out-of-range cast is rejected by `ToWireString` (verified `ArgumentOutOfRangeException` for `(Protocol)42`) |
| `Method` | `sealed record` (overturns the as-built struct) | `default` would be a method with no token |
| `HttpHeaderName` | `sealed record` (overturns the as-built struct) | `default` would be a name whose hash throws |
| `MediaType`, `RequestOptions`, `ETag`, `HttpRange` | `sealed record` | invalid or meaningless defaults; per-member invariants in `init` or no public setter |
| `Request` | `sealed record`, get-only properties, validating constructor, `With*` methods | cross-member invariant (**HTTP-7**) |
| `Response` | `sealed class`, `IDisposable`/`IAsyncDisposable` | owns a live body; a record's `with` would copy the disposal obligation into two objects |
| `Headers`, `Query` | sealed immutable class plus a nested `Builder` | batched multimap edits; §4's one builder family, below |
| `RequestBody`, `ResponseBody` | public abstract classes with static factories and private sealed variants | the one deliberately open hierarchy: transports and generated code implement bodies (§3.1) |

The cost of making `Method` and `HttpHeaderName` reference types is an allocation for an *unrecognised* verb or
header name; the well-known instances are cached statics, as they are today.

**Where a real builder is kept, and why the generic builder contract is not.** **HTTP-3** distinguishes builder-based
models ("request, response, headers, query params, request options, request conditions, multipart body") from
"Value-based types with no builder (media type, status, the typed header name, ETag, HTTP range, method, protocol)
[that] are derived by re-constructing through their factories". On .NET the language already *is* the builder for most
of the first list: an object initializer with `required` members is a builder whose `build()` is the closing brace,
and `with` is `newBuilder()` followed by `build()` — non-aliasing by construction for any member whose type is
immutable, which **XCUT-15**'s "hold an immutable collection type" makes every collection member. A mutable builder
object earns its place only where edits are *batched* over a multimap: `Headers.Builder` (already built, seeded by
`ToBuilder()`, whose `ImmutableDictionary<,>.Builder` copies on write so the pre-filled builder never aliases the
source) and, by the same argument, a `Query.Builder` for the query multimap. That makes the platform's "Headers is the
one mutable-builder exception" a family of two, for one reason. The multipart body gets a factory taking an immutable
part list rather than a builder, since it has no batched-edit use. What is **not** ported is **SEAM-29**'s "shared
generic Builder contract (build() producing the target type) ... so generic composition/folding helpers can accept any
builder": with no builder objects there is nothing for an `IBuilder<T>` to abstract, generic composition in C# takes a
`Func<T, T>` transformation instead, and an interface with no implementers is the fabricated tier P11 forbids.
**HTTP-4**'s message form is the other letter-level difference: .NET's idiom for a missing required value is
`ArgumentNullException.ThrowIfNull(url)`, whose field name travels in `ParamName` and whose message is "Value cannot
be null. (Parameter 'url')", not "url is required"; the port keeps the platform's form so callers' existing
`ArgumentException` handling and analyzers (CA2208) apply. Both are recorded as §10
entry 10.

**Read-only collection exposure, computed once.** Because models are genuinely immutable after construction,
**HTTP-5** needs no per-access wrapper: collections are stored as `System.Collections.Immutable` types, built once at
construction, and the same instance is returned from every accessor. The specification grants the latitude — "A port
in a language without read-only views MUST reproduce this with unmodifiable wrappers or per-call defensive copies" —
and immutable collections exceed it: **HTTP-5**'s conformance probe, "confirm a returned value list cannot be
downcast-and-mutated", passes because downcasting the `IReadOnlyList<string>` returned by `Headers.GetAll` yields an
`ImmutableArray<string>`, which has no mutating members. The trap the rule exists for is the .NET-typical one of
returning a `List<T>` typed as `IReadOnlyList<T>`, which a caller can cast back and mutate; public members never do
that (`docs/styleguide/csharp/10-api-design.md` rule 10.3, CA1002). Unlike Ruby there is no shallow-freeze caveat:
an `ImmutableArray<string>` of `string`s is immutable all the way down.

**The encapsulation gap, stated honestly (P8).** C# can close **HTTP-2**/**SEAM-29**'s "no public field-wise
constructor" far better than Ruby: constructors can be private, types sealed, and the `default`/`with` holes above are
closed by type choice and by the `init` rule. What remains open is below the language's access rules. *(i) Reflection
and the runtime helpers bypass constructors:* `RuntimeHelpers.GetUninitializedObject(typeof(Request))` yields an
instance whose fields are all `null` (documented behaviour), and reflection can invoke a private constructor or set a
`readonly` backing field; `Unsafe.As` can reinterpret one type as another. These are deliberate platform features, not
oversights, and no library can prevent them. *(ii) The body hierarchy is open by design:* anyone can subclass
`RequestBody` and report `IsReplayable` for a body that writes different bytes each time, and the replay gate of §3.1
trusts that property. Neither hole can be closed. The mitigation is the Ruby port's, and it is deliberately not a fake
proof: the official construction paths are closed; the public API names concrete sealed types, so a caller who reaches
for reflection or a lying subclass is knowingly opting out; and — the part that matters for security rather than
tidiness — **the invariants whose violation is exploitable are re-checked at the model-to-wire boundary inside every
transport adapter**: header name and outbound value validation (**HTTP-17**, **HTTP-18**, **XCUT-18**) and the method
token run again immediately before dispatch, so a forged model cannot smuggle a CR/LF into a header even if it never
met a constructor. §3.2's wire-level verification of `TryAddWithoutValidation` is why that re-check is not optional on
.NET. The residual gap is a correctness-of-shape gap, not a request-splitting gap; recorded as §10
entry 11.

**As built (d45e64b):** partial: records, `With*` helpers and immutable collections are in place; `Request` exposes
public `init` properties that bypass its constructor, `Method` and `HttpHeaderName` are structs with invalid
defaults, and there is no `Query` or `RequestOptions` type.

### 4.1 Headers

**Headers** (**HTTP-13**–**HTTP-22**) are an insertion-ordered immutable multimap: one entry per distinct name,
holding the folded name used for lookup, containment, mutation, removal, equality and hashing, the original casing of
the first insertion for wire emission (**HTTP-21**), and an `ImmutableArray<string>` of values in insertion order
(**HTTP-14**). The as-built type is an `ImmutableDictionary<string, ImmutableArray<string>>` keyed by the lower-cased
name, which is immutable and case-insensitive but gets three requirements wrong, each verified:

- **Order.** `ImmutableDictionary` is a hash map; names added as `z`, `a`, `m` enumerate as `a`, `z`, `m`, so
  **HTTP-16**'s "preserve insertion order of distinct names for deterministic serialization, caching, signing" is
  unmet. The replacement is an ordered entry array with an ordinal index built alongside it; header sets are small,
  and the index keeps lookups constant-time.
- **Equality.** `Headers` does not override `Equals`, so two header sets with identical content are unequal, and so
  are two `Request`s built from them — verified — although **HTTP-13** makes equality and hashing part of the
  case-insensitive contract and **HTTP-46** says request equality "compares method, headers, and body by value".
  Equality is over the ordered sequence of (folded name, values) entries; original casing does not participate.
- **Casing.** Only the folded name is stored, so the transport emits `x-trace` for a model header `X-Trace` (verified
  on the wire, §3.2). Original casing is retained and emitted on HTTP/1.1; HTTP/2 and HTTP/3 lower-case on the wire
  by protocol rule, which the handler does.

**The fold must be ASCII-only, and `ToLowerInvariant` is not.** **HTTP-13** requires "an ASCII/invariant rule
(never a locale-sensitive fold)". The locale half is the famous one and .NET's invariant APIs handle it: verified
under `tr-TR`, `"TITLE".ToLower()` returns `tıtle` (dotless ı) and `string.Equals(..., CurrentCultureIgnoreCase)` says
`title` ≠ `TITLE`, while `ToLowerInvariant()` and `OrdinalIgnoreCase` behave. The less famous half is that
`ToLowerInvariant` is *Unicode*-aware: verified, it folds U+212A KELVIN SIGN to ASCII `k`, so the as-built `Headers`,
which validates nothing, treats a header named `Key` as the same header as `key`. The port folds with
`System.Text.Ascii.ToLower` (in-box on `net8.0`, verified), which refuses non-ASCII input outright — verified, it
returns `OperationStatus.InvalidData` for `Key` — so the fold and **HTTP-17**'s non-ASCII rejection are one
operation rather than two that must be kept in agreement. The Ruby port had to state that relaxing **HTTP-17** would
silently break **HTTP-13**; on .NET the ASCII fold makes that dependency impossible to break by accident.

**Validation.** Outbound names are trimmed of surrounding whitespace and then validated (**HTTP-17**); outbound
values accept HTAB plus printable ASCII only (**HTTP-18**); an inbound, lenient path — `Headers.Builder.AddInbound`,
public because third-party transports need it — relaxes the non-ASCII rule for values while still rejecting
controls, and keeps names strict (**HTTP-19**). Two .NET-specific decisions sit inside that. First, names are held
to the RFC 9110 token grammar, which is stricter than **HTTP-17**'s floor (it also rejects separators such as space,
`(` and `:`); the extra rejections are exactly names `HttpClient` refuses anyway — verified,
`TryAddWithoutValidation("a b", ...)` returns `false` — so accepting them in the model would only move the failure to
a silent drop in the transport. The specification's floor does not say whether a stricter grammar is permitted, so
this is recorded as §11 item 36. Second, **HTTP-20**'s message hygiene is a real finding in
the as-built code: `HttpHeaderName.Of("a\rb")` throws `"Invalid header-name character '\r'"` with the raw carriage
return in the message (verified), which is the log-injection vector the requirement names. Messages name the
offending character by code point (`U+000D`) and never include a value.

**Null removes (HTTP-15)** is expressed in the signature: `Headers.Set(string name, string? value)` removes the name
when `value` is `null`, and nullable annotations document it. **HTTP-22**'s optional interning is not implemented: it
is a **MAY**, the observable contract is value equality by folded name, and the well-known names are already cached
statics on `HttpHeaderName.WellKnown`. `HttpHeaderName` interoperates with the string-keyed API through overloads that
take it directly, not through its `ToString()` (**HTTP-21**).

**As built (d45e64b):** built — diverges: no validation on `Headers`; non-ASCII-aware fold; enumeration order not
insertion order; no value equality; original casing lost; no inbound lenient path; `HttpHeaderName` rejects
surrounding whitespace instead of trimming, echoes raw control characters in its message, and is a struct.

### 4.2 Request and Response

**Request** (**HTTP-6**, **HTTP-7**, **HTTP-8**, **HTTP-46**, **HTTP-47**) carries exactly method, absolute URL,
headers (never `null`) and an optional body. The constructor rejects a body on GET, HEAD, TRACE and CONNECT
(**HTTP-7**) — which the as-built constructor does not check at all, verified: `new Request(Method.Get, url, null,
body)` succeeds — and, because the port's `Method` is not optional in any constructor, **HTTP-8**'s "no method set"
case is unrepresentable rather than defaulted. The URL must be absolute and `http`/`https`; malformed input fails with
an `ArgumentException` naming the input (**HTTP-47**), which the as-built message ("Request URL must be an absolute
URI.") omits. Two `System.Uri` behaviours from §3.5 shape the rest. First, on Linux `Uri.TryCreate("/rel",
UriKind.Absolute, ...)` succeeds as a `file:` URI, so absoluteness alone is never the check. Second, **request
equality must not use `Uri.Equals`**: verified, `Uri.Equals` treats `https://a@h/` and `https://b@h/` as equal and
ignores the fragment, so the record-generated equality over `Url` makes two requests with different credentials in
their userinfo equal. **HTTP-46** asks for comparison "by textual external form"; `Request` overrides `Equals` to
compare `Url.AbsoluteUri` ordinally, which includes userinfo and fragment and is still free of DNS — `System.Uri`
never resolves, the JVM `URL` hazard the requirement guards against does not exist here (P7: free, with the
precondition that nobody substitutes `Uri.Equals`). The method token is validated as an RFC 9110 token in
`Method.Of`: as built, `Method.Of("GET\r\nX: y")` is accepted and only `new HttpMethod(...)` in the transport rejects
it (verified `FormatException`), which escapes the transport unmapped.

**Response** (**HTTP-6**, **HTTP-4**, **HTTP-43**) carries the originating request, the negotiated protocol, the
status, an optional reason phrase, headers and the body; the request, protocol and status are required constructor
parameters. As built it has neither the request nor the reason phrase. `Response` is disposable and its dispose is
latched and forwards to the body (§3.7). Two behaviours of the as-built `EnsureSuccessAsync` belong to the error
mapping of §5.1 but are recorded here because they live on the model: it throws for *any* non-2xx — verified,
`EnsureSuccessAsync` on a 304 throws `HttpResponseException` — which contradicts **BODY-31** and **XCUT-8** (only
4xx/5xx map to an exception; a 304 or an unfollowed 3xx is returned intact); and it caps the error body at 1 MiB
correctly but never disposes the *original* response, so the live connection outlives the throw, against
**HTTP-52**'s "buffering MUST occur inside the original body's close scope". Range classification gains
`IsError` (400–599, **HTTP-11**) alongside the existing `IsSuccess`.

**As built (d45e64b):** built — diverges: `Request` has public `init` properties, no **HTTP-7** check, `Uri.Equals`
equality and a message without the input; `Response` lacks request and reason phrase; `EnsureSuccessAsync` throws on
3xx and does not dispose the original response.

### 4.3 Status, Method, Protocol

**Status** (**HTTP-10**–**HTTP-12**) is a `readonly record struct` with the code as its identity and the canonical
name looked up from a static table. **HTTP-12** requires equality over the code only, and a naive
`record struct Status(int Code, string? Name)` would generate equality over both members and quietly violate it —
the same trap the Ruby port avoided with `Data.define(:code)`. The as-built type stores the name but overrides
`Equals(Status)` and `GetHashCode` to the code, and C#'s generated `==` routes through that override (verified:
`Status.FromCode(200) == Status.Ok`). Construction is total over any `int` (**HTTP-10**; verified for 599 and −1,
both nameless, neither throwing), and the separate recognised-code lookup the requirement asks for is
`Status.TryGetKnown(int, out Status)`. Range classification is derived, not stored (**HTTP-11**), and gains
`IsError`.

**Method** (**HTTP-9**) is classified by one idempotent set, exactly `{GET, HEAD, OPTIONS, PUT, DELETE}`, that both
the retry allow-list and the replay gate derive from; the set is the `FrozenSet<Method>` in the internal `RetryFacts`
(§6.1), read by an `internal` member of `Method`. **HTTP-9** is specific about the shape as well as the members:
"There is no separate safe-method classification, and the idempotent set is an internal constant rather than a
public accessor on the method type." The as-built type has both things that sentence excludes — a public
`IsIdempotent` defined as "safe methods plus PUT and DELETE", over a public `IsSafe` that includes TRACE, so it
answers `true` for TRACE. RFC 9110 would agree about TRACE, but the specification's set is the one every consumer
derives from, so the port drops TRACE and removes both public properties before 1.0 rather than keep an RFC-meaning
accessor beside the SDK's set (§11 item 22). Each well-known method's token is its upper-case name, and `Method.Of`
upper-cases the well-known verbs and keeps other tokens verbatim, because RFC 9110 methods are case-sensitive; one
.NET oddity is worth knowing so it is not copied: `System.Net.Http.HttpMethod` compares case-*insensitively*
(verified, `new HttpMethod("get") == HttpMethod.Get`), so the SDK's `Method` never delegates its equality to it.

**Protocol** (**HTTP-33**) is an `enum` with a lower-case wire form (`http/1.0`, `http/1.1`, `http/2`) and a
case-insensitive, culture-invariant parse accepting the canonical forms plus `HTTP/2` and `HTTP/2.0`, throwing on
anything else — built as specified. The `H2PriorKnowledge` and `Quic` members mirror the reference's protocol list;
.NET's handlers report HTTP/3 as version 3.0, which the adapter maps to `Quic`.

**As built (d45e64b):** built — diverges: `Status` lacks `IsError` and a recognised-code lookup; `Method` is a
struct, has public `IsSafe`/`IsIdempotent` properties and the latter includes TRACE, and `Method.Of` accepts any
non-blank string.

### 4.4 Media type, query, and conditional helpers

**Media type** (**HTTP-23**–**HTTP-27**, **HTTP-53**) needs a hand-written parser: it must split parameters
respecting quoted strings, split each parameter on the *first* `=` only, strip quotes and unescape quoted pairs, and
render so that `parse(render(x)) == x`. .NET ships `System.Net.Http.Headers.MediaTypeHeaderValue`, which is the
obvious-but-wrong tool twice over: it is mutable (verified: `CharSet` is settable after parse), and it is lenient
where **HTTP-53** is strict (verified: it accepts `text/plain; foo`, a parameter with no `=`). The as-built parser is a
character scanner with no regular expression, so the Ruby port's regex-timeout hardening has no counterpart to add; if
a regex is ever introduced it is `[GeneratedRegex]` with a timeout, for AOT and for ReDoS. The parser is right on
quoting and first-`=` splitting (verified: a value `x;y="z"` renders quoted and round-trips to an equal instance) and
on case normalisation (`Application/JSON;Charset=UTF-8` → `application/json`, key `charset`, value `UTF-8`). It has
two gaps, both verified. **HTTP-53** requires "a non-empty raw value", but `text/plain; a=` parses to a parameter with
an empty value. And **HTTP-26** requires construction to "reject a control ... or non-ASCII byte anywhere, using the
same predicate as outbound header-value validation, so a media type is always header-safe", but a parameter value
containing CR/LF is accepted and rendered inside quotes, so a `MediaType` can carry a line break into a
`Content-Type` header. Values are validated with the **HTTP-18** predicate at construction. A `TryParse` joins
`Parse` for the transport's lenient inbound path (**TRANSPORT-27**, §3.2), and the charset lookup catches
`NotSupportedException` as well as `ArgumentException` (§3.1).

**Query** (**HTTP-28**–**HTTP-32**) is an insertion-ordered immutable list of name/value pairs with a nested
`Builder`, not a dictionary, because **HTTP-28** requires multi-value support with order preserved and a value-less
parameter modelled as a single empty-string value distinct from an absent name — a shape a dictionary cannot express.
Names are case-sensitive (ordinal). A name whose value list is empty is dropped at build time (**HTTP-30**), and
equality is order-sensitive — two instances are equal iff they encode identically. Encoding uses
`Uri.EscapeDataString` per component (§3.5, verified RFC 3986 exact), and parsing (**HTTP-31**) is lenient and total
through `Uri.UnescapeDataString`, which already leaves a malformed escape as raw text and `+` as `+` (verified), the
opposite of `WebUtility.UrlDecode`'s form behaviour.

**Request options** (**HTTP-34**, **HTTP-35**) are a `sealed record RequestOptions` with a nullable `TimeSpan?
Timeout`, a nullable `int? MaxRetries` and an `ImmutableDictionary<string, string> Tags`, plus a static `Empty` so
"override nothing" allocates nothing per call. All three invariants are per-member, so they live in `field`-backed
`init` accessors and `with` is safe: a non-null timeout must be positive (which also rejects
`Timeout.InfiniteTimeSpan`, whose value is −1 ms), `MaxRetries` must be non-negative with 0 meaning "no retries for
this call", and tags are held as an immutable dictionary so no caller-side map can alias them (**HTTP-34**'s
defensive copy). Timeout is a `TimeSpan`, matching every .NET timeout API, where the Ruby port used float seconds for
the same reason. These are the per-call options threaded through both transport interfaces (§3.2, §3.3); they are not
`DexpaceClientOptions`, which is client-level configuration (§8.2).

**Conditional-request helpers** (**HTTP-48**–**HTTP-50**) are `ETag`, `HttpRange` and a `RequestConditions`
aggregator, all `sealed record`s with validating factories. The BCL's `EntityTagHeaderValue` and `RangeHeaderValue`
are again tempting and in-box, and again unsuitable as model types: verified, `RangeHeaderValue.Ranges` is a mutable
collection that accepts a second range after construction, where **HTTP-49** supports "only ... a single range".
`RequestConditions` applies itself with `Set`, not `With`, so applying it twice is idempotent (**HTTP-50**), and
emits dates through the RFC 1123 formatter of §8.

**As built (d45e64b):** partial: `MediaType` is built but accepts empty parameter values and control characters in
values, and has no `TryParse`; `Query`, `RequestOptions`, `ETag`, `HttpRange` and `RequestConditions` are not built.

### 4.5 Bodies

The body types are specified with the byte-stream contract in §3.1, because on .NET they *are* that contract's public
face; construction is summarised here. `RequestBody` and `ResponseBody` are public abstract classes — the one open
hierarchy in the model, because transports and generated code must be able to implement bodies — whose common
variants are private sealed classes reached only through static factories (`FromBytes`, `FromString`, `FromValue`,
`FromStream`, and the planned `FromFile`, `FromForm`, `Multipart`), so their invariants are established in one place.
Metadata (`ContentType`, `ContentLength`, `IsReplayable`) is immutable; the only mutable state is the single-use
consumption flag and, for response bodies, the live stream — **HTTP-1**'s declared carve-out ("a body that wraps
live single-use stream state"). `FromBytes` copies its input (`ReadOnlyMemory<byte>.ToArray()`), so a caller's later
mutation of its buffer cannot reach the body (**XCUT-15**), and `FromString` encodes with `GetBytes`, which emits no
BOM (verified: UTF-8 `"a"` is the single byte `61`).

**As built (d45e64b):** partial: see §3.1's status line.

---
