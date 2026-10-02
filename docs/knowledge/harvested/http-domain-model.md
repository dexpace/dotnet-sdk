# http-domain-model

## Rules
- HTTP-3 (MUST): Each builder-based model (request, response, headers, query params, request options, request conditions, multipart body) MUST expose a newBuilder()-style derivation returning a builder pre-populated from the instance that does not alias the original's internal collections, with each value list copied.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:7-7` · high · sha:22d100d5bc94</sub>
- HTTP-3 (MUST): Value-based types with no builder (media type, status, typed header name, ETag, HTTP range, method, protocol) are derived by re-constructing through their factories.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:7-7` · high · sha:22d100d5bc94</sub>
- HTTP-4 (MUST): build() MUST validate required fields and fail with a field-named error when one is missing (a request requires its URL; a response requires request, protocol and status), never silently substituting defaults except where explicitly specified.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:8-8` · high · sha:22d100d5bc94</sub>
- HTTP-5 (MUST): Accessors returning collections of header or query names, values or entries MUST NOT let a caller mutate the model through the returned value and MUST NOT surface later mutations of a live builder.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:9-9` · high · sha:22d100d5bc94</sub>
- HTTP-5: Isolation from a builder is guaranteed by a build-time deep copy of every value list, and isolation from mutation-through-the-collection is guaranteed by returning read-only-typed collections; a port in a language without read-only views MUST use unmodifiable wrappers or per-call defensive copies.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:9-9` · high · sha:22d100d5bc94</sub>
- HTTP-6 (MUST): A request MUST carry exactly method, target URL, headers (non-null, possibly empty) and an optional body.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:13-13` · high · sha:22d100d5bc94</sub>
- HTTP-6 (MUST): A response MUST carry the originating request, negotiated protocol, status, an optional reason phrase, headers (non-null, possibly empty) and an optional body.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:13-13` · high · sha:22d100d5bc94</sub>
- HTTP-6: Operational knobs such as timeout and retries live outside the wire model (section 4.5).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:13-13` · high · sha:22d100d5bc94</sub>
- HTTP-7 (MUST): The request builder MUST reject a non-null body on any method whose classification forbids one (GET, HEAD, TRACE, CONNECT), failing at construction rather than deferring to the transport.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:14-14` · high · sha:22d100d5bc94</sub>
- HTTP-8 (SHOULD): With no method set, build() SHOULD default to GET only if no body is present.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:15-15` · high · sha:22d100d5bc94</sub>
- HTTP-8 (SHOULD): A body with no method SHOULD fail reporting a missing method rather than defaulting to GET and then tripping the no-body-on-GET rule.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:15-15` · high · sha:22d100d5bc94</sub>
- HTTP-9 (MUST): The model MUST define an idempotency classification, the set {GET, HEAD, OPTIONS, PUT, DELETE}, as the single source from which both the configurable retry allow-list and the inherent replay-safety gate derive.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:16-16` · high · sha:22d100d5bc94</sub>
- HTTP-9 (MUST): Each method's canonical wire token MUST equal its uppercase name.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:16-16` · high · sha:22d100d5bc94</sub>
- HTTP-46 (MUST): Request URL equality and hashing MUST NOT perform blocking work or DNS resolution; URLs MUST be compared by textual external form.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:17-17` · high · sha:22d100d5bc94</sub>
- HTTP-46 (MUST): Request equality otherwise compares method, headers and body by value.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:17-17` · high · sha:22d100d5bc94</sub>
- HTTP-47 (SHOULD): Building a request from a malformed URL string or non-absolute URI SHOULD fail with an argument error carrying the offending input.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:18-18` · high · sha:22d100d5bc94</sub>
- HTTP-10 (MUST): Status MUST be a total function of the integer code: mapping any code returns a Status and never throws, with a canonical named instance for recognized codes and a raw-code, null-named instance for unrecognized ones.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:22-22` · high · sha:22d100d5bc94</sub>
- HTTP-10 (MUST): A separate lookup MUST let callers distinguish recognized codes, returning absent for unknown codes.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:22-22` · high · sha:22d100d5bc94</sub>
- HTTP-11 (MUST): Status MUST classify by range: informational 100-199, success 200-299, redirect 300-399, client-error 400-499, server-error 500-599, and error 400-599.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:23-23` · high · sha:22d100d5bc94</sub>
- HTTP-11 (MUST): A response MUST expose the status range classifications derived from its status.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:23-23` · high · sha:22d100d5bc94</sub>
- HTTP-12 (MUST): Two Status values MUST be equal if and only if their numeric codes are equal, with the name not participating.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:24-24` · high · sha:22d100d5bc94</sub>
- HTTP-13 (MUST): Header names MUST be treated case-insensitively for storage, lookup, containment, mutation, removal, equality and hashing, folding to lower case with an ASCII/invariant rule and never a locale-sensitive fold.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:28-28` · high · sha:22d100d5bc94</sub>
- HTTP-14 (MUST): The header model MUST support multiple values per name, where add appends and set replaces the whole list, preserving per-name insertion order.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:29-29` · high · sha:22d100d5bc94</sub>
- HTTP-15 (MUST): Setting a header value to null MUST remove the header entirely.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:30-30` · high · sha:22d100d5bc94</sub>
- HTTP-16 (SHOULD): The header model SHOULD preserve insertion order of distinct names for deterministic serialization, caching, signing and test stability.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:31-31` · high · sha:22d100d5bc94</sub>
- HTTP-17 (MUST): Outbound (caller-set) header names MUST be validated at construction, rejecting a blank name, any C0 control (0x00-0x1F including CR/LF/NUL), DEL (0x7F) and any non-ASCII byte (>= 0x80).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:32-32` · high · sha:22d100d5bc94</sub>
- HTTP-17 (MUST): Surrounding whitespace on an outbound header name is trimmed before validation, so "  X-Trace  " is stored as X-Trace.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:32-32` · high · sha:22d100d5bc94</sub>
- HTTP-18 (MUST): Outbound header values MUST reject any control character (C0 and DEL) except horizontal tab (0x09) and MUST reject any non-ASCII byte, so the accepted set is HTAB plus printable ASCII 0x20-0x7E.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:33-33` · high · sha:22d100d5bc94</sub>
- HTTP-19 (MUST): The model MUST provide a distinct lenient path for inbound (response) header values that relaxes the non-ASCII rule (obs-text >= 0x80 permitted) while still rejecting control characters (C0 except HTAB, plus DEL).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:34-34` · high · sha:22d100d5bc94</sub>
- HTTP-19 (MUST): Inbound header names remain strictly validated.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:34-34` · high · sha:22d100d5bc94</sub>
- HTTP-20 (MUST): Header validation error messages MUST NOT echo the offending header value verbatim and MUST escape any control characters in an echoed header name.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:35-35` · high · sha:22d100d5bc94</sub>
- HTTP-21 (MUST): A typed header-name abstraction MUST compare and hash by its case-folded form while preserving original casing for wire emission, MUST interoperate with the string-keyed API, and MUST enforce the same name validation as HTTP-17.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:36-36` · high · sha:22d100d5bc94</sub>
- HTTP-22 (MAY): The typed header name MAY intern instances process-wide with the first casing winning; interning is an optimization and the observable contract is value equality by case-folded name.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:36-36` · high · sha:22d100d5bc94</sub>
- HTTP-23 (MUST): Media-type construction MUST lower-case the type, subtype and every parameter key while preserving each parameter value's case.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:37-37` · high · sha:22d100d5bc94</sub>
- HTTP-23 (MUST): Media-type equality is case-insensitive on type, subtype and keys and case-sensitive on values, so Application/JSON;Charset=UTF-8 yields application/json with key charset and value UTF-8.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:37-37` · high · sha:22d100d5bc94</sub>
- HTTP-24 (MUST): Media type MUST resolve its charset parameter case-insensitively and return null rather than throw when the charset is absent or unknown, so callers fall back to a default.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:38-38` · high · sha:22d100d5bc94</sub>
- HTTP-25 (MUST): Media-type parsing MUST split parameters respecting quoted-strings (a ; or = inside quotes is not a separator), split each parameter on its first = only, strip quotes and unescape quoted-pairs.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- HTTP-25 (MUST): Media-type rendering MUST emit a value bare when it is a valid token and quoted-and-escaped otherwise, so that parse(render(x)) == x.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- HTTP-53 (MUST): Media-type parsing MUST reject blank input, require a non-empty type before and non-empty subtype after a single /, and require each parameter to contain = with a non-empty key and value.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- HTTP-26 (MUST): Media-type construction MUST reject a control character (C0 except HTAB, plus DEL) or non-ASCII byte anywhere, using the same predicate as outbound header-value validation, so a media type is always header-safe.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- HTTP-27 (SHOULD): Media-type wildcard matching SHOULD permit a wildcard type only with a wildcard subtype (bare */*), with a wildcard in either position matching any value and parameters ignored.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- HTTP-28 (MUST): Query-parameter names MUST be case-sensitive with no folding, preserve insertion order and support multiple values, so page and Page are distinct.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:40-40` · high · sha:22d100d5bc94</sub>
- HTTP-28 (MUST): A value-less query parameter (?flag) MUST be modeled as a single empty-string value distinct from an absent name, so add("flag", null) gives get("flag") == "" with contains true and an absent name gives get null.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:40-40` · high · sha:22d100d5bc94</sub>
- HTTP-29 (MUST): Query encoding MUST render each name and value with RFC 3986 percent-encoding (space to %20, literal + to %2B, / to %2F, * to %2A), encoding everything except the unreserved set A-Z a-z 0-9 - . _ ~.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:41-41` · high · sha:22d100d5bc94</sub>
- HTTP-29 (MUST): Query encoding preserves insertion order, emits a repeated name once per value, omits the leading ?, and returns empty when empty.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:41-41` · high · sha:22d100d5bc94</sub>
- HTTP-32 (SHOULD): A single query component's encoding SHOULD follow RFC 3986 independent of stdlib quirks: space is %20 never +, a literal + is %2B, decoding leaves + as +, ~ stays unencoded and * is encoded, so {q:["a b"], plus:["c+d"]} yields q=a%20b&plus=c%2Bd.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:41-41` · high · sha:22d100d5bc94</sub>
- HTTP-30 (MUST): Query equality MUST be order-sensitive, with two instances equal if and only if they encode identically.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:42-42` · high · sha:22d100d5bc94</sub>
- HTTP-30 (MUST): A query name whose value list is empty MUST be dropped at build time so it cannot leave a phantom contains-true entry invisible to encode.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:42-42` · high · sha:22d100d5bc94</sub>
- HTTP-31 (MUST): Query parsing MUST invert encode and be lenient: null or blank query gives empty, a leading ? is tolerated, a segment with no = or a trailing = gives an empty-string value, stray & is skipped, and malformed percent-encoding falls back to raw text rather than throwing.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:42-42` · high · sha:22d100d5bc94</sub>
- HTTP-33 (MUST): Protocol MUST expose a canonical lower-case wire form (http/1.1, http/2).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:43-43` · high · sha:22d100d5bc94</sub>
- HTTP-33 (MUST): Protocol MUST provide a locale-invariant, case-insensitive parse accepting the canonical forms plus the aliases HTTP/2 and HTTP/2.0, and MUST throw on an unrecognized identifier.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:43-43` · high · sha:22d100d5bc94</sub>
- HTTP-34 (MUST): Request options MUST model per-call operational overrides that are not part of the wire form, at minimum a per-call timeout, a per-call max-retries and opaque string-keyed tags.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:47-47` · high · sha:22d100d5bc94</sub>
- HTTP-34 (MUST): Every request-options field defaults to a null or empty use-the-default sentinel, a canonical EMPTY override-nothing instance MUST be provided (null timeout, null max-retries, empty tags), and tags MUST be defensively copied at build.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:47-47` · high · sha:22d100d5bc94</sub>
- HTTP-35 (MUST): The request-options builder MUST reject a non-null timeout that is zero or negative and MUST reject a negative max-retries.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:48-48` · high · sha:22d100d5bc94</sub>
- HTTP-35 (MUST): A max-retries of 0 MUST be accepted and means disable retries for this call, and a null timeout is accepted.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:48-48` · high · sha:22d100d5bc94</sub>
- HTTP-48 (SHOULD): An ETag helper SHOULD model strong ("opaque"), weak (W/"opaque") and the any singleton (*) forms.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:52-52` · high · sha:22d100d5bc94</sub>
- HTTP-48 (SHOULD): The ETag helper SHOULD validate permitted etagc characters (rejecting a literal quote, control chars and DEL; permitting obs-text), reject an empty strong opaque, permit an empty weak opaque, round-trip its raw form, reject unterminated forms and return absent for blank input.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:52-52` · high · sha:22d100d5bc94</sub>
- HTTP-49 (SHOULD): An HTTP-range helper SHOULD provide validated factories for a bounded range (rejecting negative offset and non-positive length, detecting overflow), a suffix range and an open-ended range.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:53-53` · high · sha:22d100d5bc94</sub>
- HTTP-49 (SHOULD): The HTTP-range helper SHOULD support only the bytes unit and a single range (rejecting multi-range commas) and store a parsed value verbatim.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:53-53` · high · sha:22d100d5bc94</sub>
- HTTP-50 (SHOULD): A conditional-requests aggregator SHOULD emit If-Match and If-None-Match as one comma-separated header and If-Modified-Since and If-Unmodified-Since as RFC 1123 dates.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:54-54` · high · sha:22d100d5bc94</sub>
- HTTP-50 (SHOULD): The conditional-requests aggregator SHOULD be idempotent when applied (using set, not add) and enforce that the any-tag (*) is mutually exclusive with concrete entity-tags, collapsing repeated * to one.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:54-54` · high · sha:22d100d5bc94</sub>
- XCUT-18 (MUST) Header names and outbound header values MUST be validated at the transport-agnostic model layer before reaching any transport.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46-46` · high · sha:d6123be82c9e</sub>
- XCUT-18 (MUST) Header names MUST reject all C0 control bytes (0x00-0x1F, including CR, LF, NUL and HTAB) and DEL (0x7F).
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46-46` · high · sha:d6123be82c9e</sub>
- XCUT-18 (MUST) Outbound header values MUST reject the same control set as names except horizontal tab (0x09), so an outbound value with HTAB is accepted while a name with HTAB is rejected.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46-46` · high · sha:d6123be82c9e</sub>
- XCUT-18 (MUST) Both header names and outbound values MUST reject non-ASCII bytes (>= 0x80).
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46-46` · high · sha:d6123be82c9e</sub>
- XCUT-18 (MAY/MUST) Inbound response header values MAY be validated leniently (obs-text permitted) but MUST still reject control bytes except HTAB.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46-46` · high · sha:d6123be82c9e</sub>
- HTTP-17, HTTP-18 and XCUT-18 are enforced in the model and re-checked at the model-to-wire boundary in the adapter, as mitigation for construction bypass (P8).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:348-350` · high · sha:da6000c93fc5</sub>
- The adapter drops the TRANSPORT-11 framing set (Content-Length, Host, Transfer-Encoding, Connection, Keep-Alive, Upgrade, TE, Expect) with a Debug log naming each.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:350-351` · high · sha:da6000c93fc5</sub>
- A header the native client still refuses is dropped individually rather than failing the send (TRANSPORT-12, TRANSPORT-13).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:351-352` · high · sha:da6000c93fc5</sub>
- The caller's explicit request Content-Type header remains authoritative over the body's media type (TRANSPORT-10).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:357-358` · high · sha:da6000c93fc5</sub>
- The adapter partitions headers by name against the fixed set of content headers (Allow, Content-*, Expires, Last-Modified) and gives the caller's Content-Type precedence over the body's.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:359-360` · high · sha:da6000c93fc5</sub>
- Original header-name casing from the model is emitted on HTTP/1.1 (HTTP-21).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:362-362` · high · sha:da6000c93fc5</sub>
- The model rejects a GET with a body at construction, so the adapter never sees the combination (HTTP-7).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:363-365` · high · sha:da6000c93fc5</sub>
- Every core domain type is immutable and safe to share after construction, built only through an immutable value plus a builder or factory, with no public field-wise constructor and no unchecked copy that bypasses validation (HTTP-1, HTTP-2, SEAM-29, XCUT-15).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:3-5` · high · sha:3aa554d9287d</sub>
- HTTP-3 requires a pre-filled, non-aliasing derivation; HTTP-4 requires build() to validate required fields and fail with a field-named error; HTTP-5 requires accessors to isolate the caller from both the model's internals and a still-live builder.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:5-8` · high · sha:3aa554d9287d</sub>
- Per-member invariants live in the init accessor using C# 14's field keyword (for example `public TimeSpan? Timeout { get; init => field = RequireNullOrPositive(value); }`) so the check runs on every assignment path: constructor, object initializer and with.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:23-25` · high · sha:3aa554d9287d</sub>
- A type with a cross-member invariant exposes no public init accessor, because an init accessor cannot check a relationship between members and with assigns one member at a time.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:28-31` · high · sha:3aa554d9287d</sub>
- A model is a readonly record struct only when its default value is a legitimate instance; otherwise it is a sealed record class, whose default is null and which nullable reference types already police.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:36-44` · high · sha:3aa554d9287d</sub>
- Public members never return a List<T> typed as IReadOnlyList<T>, because a caller can cast it back and mutate it (styleguide csharp/10-api-design.md rule 10.3, CA1002).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:88-90` · high · sha:3aa554d9287d</sub>
- Every transport adapter re-checks exploitable invariants at the model-to-wire boundary immediately before dispatch: header name and outbound value validation (HTTP-17, HTTP-18, XCUT-18) and the method token, so a forged model cannot smuggle CR/LF into a header.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:104-108` · high · sha:3aa554d9287d</sub>
- Headers equality is over the ordered sequence of (folded name, values) entries and original casing does not participate, satisfying HTTP-13 and HTTP-46.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:127-130` · high · sha:3aa554d9287d</sub>
- Original header-name casing is retained and emitted on HTTP/1.1, while HTTP/2 and HTTP/3 lower-case names on the wire by protocol rule, which the handler does.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:131-133` · high · sha:3aa554d9287d</sub>
- Header names are folded with an ASCII-only fold, as HTTP-13 requires an ASCII/invariant rule and never a locale-sensitive fold.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:135-136` · high · sha:3aa554d9287d</sub>
- Outbound header names are trimmed of surrounding whitespace and then validated (HTTP-17).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:146-147` · high · sha:3aa554d9287d</sub>
- Outbound header values accept only HTAB plus printable ASCII (HTTP-18).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:147-147` · high · sha:3aa554d9287d</sub>
- The inbound lenient path, Headers.Builder.AddInbound (public because third-party transports need it), relaxes the non-ASCII rule for values while still rejecting controls and keeps names strict (HTTP-19).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:147-149` · high · sha:3aa554d9287d</sub>
- Header validation error messages name the offending character by code point (for example U+000D) and never include a value, to avoid the log-injection vector HTTP-20 names.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:154-157` · high · sha:3aa554d9287d</sub>
- Headers.Set(string name, string? value) removes the name when value is null (HTTP-15), documented through nullable annotations.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:159-160` · high · sha:3aa554d9287d</sub>
- HttpHeaderName interoperates with the string-keyed Headers API through overloads that take it directly, not through its ToString() (HTTP-21).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:162-163` · high · sha:3aa554d9287d</sub>
- Request carries exactly method, absolute URL, headers (never null) and an optional body (HTTP-6, HTTP-7, HTTP-8, HTTP-46, HTTP-47).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:171-173` · high · sha:3aa554d9287d</sub>
- The Request constructor rejects a body on GET, HEAD, TRACE and CONNECT (HTTP-7).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:173-175` · high · sha:3aa554d9287d</sub>
- A Request URL must be absolute and http or https, and malformed input fails with an ArgumentException naming the input (HTTP-47).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:175-177` · high · sha:3aa554d9287d</sub>
- Request overrides Equals to compare Url.AbsoluteUri ordinally (including userinfo and fragment), satisfying HTTP-46's comparison by textual external form, and nobody may substitute Uri.Equals.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:181-184` · high · sha:3aa554d9287d</sub>
- The method token is validated as an RFC 9110 token in Method.Of.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:184-185` · high · sha:3aa554d9287d</sub>
- Response (HTTP-6, HTTP-4, HTTP-43) carries the originating request, negotiated protocol, status, an optional reason phrase, headers and body, with request, protocol and status as required constructor parameters.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:188-190` · high · sha:3aa554d9287d</sub>
- Response is disposable, and its dispose is latched and forwards to the body (section 3.7).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:190-191` · high · sha:3aa554d9287d</sub>
- Only 4xx/5xx statuses map to an exception (BODY-31, XCUT-8); a 304 or an unfollowed 3xx is returned intact.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:192-195` · high · sha:3aa554d9287d</sub>
- Buffering of an error body MUST occur inside the original body's close scope (HTTP-52), so the original response must be disposed.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:195-197` · medium · sha:3aa554d9287d</sub>
- Status range classification gains IsError (400-599, HTTP-11) alongside the existing IsSuccess.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:196-197` · high · sha:3aa554d9287d</sub>
- Status (HTTP-10 to HTTP-12) is a readonly record struct whose identity is its code, with the canonical name looked up from a static table.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:205-206` · high · sha:3aa554d9287d</sub>
- Status construction is total over any int (HTTP-10; 599 and -1 yield nameless statuses without throwing), and the recognised-code lookup is Status.TryGetKnown(int, out Status).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:210-212` · high · sha:3aa554d9287d</sub>
- Status range classification is derived rather than stored (HTTP-11).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:212-213` · high · sha:3aa554d9287d</sub>
- Method (HTTP-9) is classified by one idempotent set, exactly {GET, HEAD, OPTIONS, PUT, DELETE}, from which both the retry allow-list and the replay gate derive.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:215-216` · high · sha:3aa554d9287d</sub>
- HTTP-9 states there is no separate safe-method classification, and the idempotent set is an internal constant rather than a public accessor on the method type.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:217-219` · high · sha:3aa554d9287d</sub>
- Each well-known method's token is its upper-case name, and Method.Of upper-cases the well-known verbs and keeps other tokens verbatim, because RFC 9110 methods are case-sensitive.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:223-225` · high · sha:3aa554d9287d</sub>
- The SDK's Method never delegates equality to System.Net.Http.HttpMethod, because HttpMethod compares case-insensitively (`new HttpMethod("get") == HttpMethod.Get`).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:225-226` · high · sha:3aa554d9287d</sub>
- Protocol (HTTP-33) is an enum with a lower-case wire form (http/1.0, http/1.1, http/2) and a case-insensitive, culture-invariant parse that accepts the canonical forms plus HTTP/2 and HTTP/2.0 and throws on anything else.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:228-230` · high · sha:3aa554d9287d</sub>
- MediaType (HTTP-23 to HTTP-27, HTTP-53) uses a hand-written parser that splits parameters respecting quoted strings, splits each parameter on the first `=` only, strips quotes, unescapes quoted pairs, and renders so that parse(render(x)) == x.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:239-242` · high · sha:3aa554d9287d</sub>
- If a regular expression is ever introduced into the media-type parser it must be [GeneratedRegex] with a timeout, for AOT and for ReDoS.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:243-246` · high · sha:3aa554d9287d</sub>
- HTTP-53 requires a non-empty raw value for a media-type parameter, so `text/plain; a=` must be rejected (the as-built parser accepts it with an empty value).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:247-249` · high · sha:3aa554d9287d</sub>
- HTTP-26 requires MediaType construction to reject a control or non-ASCII byte anywhere using the same predicate as outbound header-value validation, so a media type is always header-safe, and the port validates values with the HTTP-18 predicate at construction.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:249-252` · high · sha:3aa554d9287d</sub>
- MediaType gains a TryParse alongside Parse for the transport's lenient inbound path (TRANSPORT-27, section 3.2), and the charset lookup catches NotSupportedException as well as ArgumentException (section 3.1).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:252-254` · high · sha:3aa554d9287d</sub>
- RequestOptions (HTTP-34, HTTP-35) is a sealed record with a nullable TimeSpan? Timeout, a nullable int? MaxRetries and an ImmutableDictionary<string, string> Tags, plus a static Empty so "override nothing" allocates nothing per call.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:265-268` · high · sha:3aa554d9287d</sub>
- RequestOptions invariants live in field-backed init accessors: a non-null Timeout must be positive (which rejects Timeout.InfiniteTimeSpan, whose value is -1 ms), and MaxRetries must be non-negative with 0 meaning no retries for that call.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:268-270` · high · sha:3aa554d9287d</sub>
- RequestOptions Tags are held as an immutable dictionary so no caller-side map can alias them (HTTP-34's defensive copy).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:270-271` · high · sha:3aa554d9287d</sub>
- RequestConditions applies itself with Set, not With, so applying it twice is idempotent (HTTP-50), and it emits dates through the RFC 1123 formatter of section 8.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:279-280` · high · sha:3aa554d9287d</sub>

## Constraints
- HTTP-29: Query encoding is NOT application/x-www-form-urlencoded.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:41-41` · high · sha:22d100d5bc94</sub>
- On .NET 10.0.401, HttpRequestHeaders.TryAddWithoutValidation("X-Custom", "a\r\nInjected: yes") returns true and HttpClient writes both lines to the socket verbatim.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:39-42` · high · sha:d7cea7b15cf3</sub>
- TryAddWithoutValidation does not reject CR/LF: HttpRequestHeaders.TryAddWithoutValidation("X-Custom", "a\r\nInjected: yes") returns true and HttpClient writes two header lines on the wire (verified with a raw TcpListener).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:341-343` · high · sha:da6000c93fc5</sub>
- HttpRequestMessage.Headers.TryAddWithoutValidation returns false for Content-Type and Content-Length (verified), because request headers and content headers are two separate collections.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:353-355` · high · sha:da6000c93fc5</sub>
- HttpClient sends a body on GET (verified: a GET with StringContent reached the server with a 3-byte body), a divergence HTTP-7's rationale names.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:363-364` · high · sha:da6000c93fc5</sub>
- A with-expression and object initializers run init accessors and not constructors, so validation written only in a constructor is skipped by every derived copy.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:15-16` · high · sha:3aa554d9287d</sub>
- Every C# struct has a parameterless construction path the author cannot remove: `new Method()` compiles and yields a Method whose Name is null, and `default(HttpHeaderName).GetHashCode()` throws NullReferenceException.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:39-42` · high · sha:3aa554d9287d</sub>
- Reflection, RuntimeHelpers.GetUninitializedObject (yielding an instance with all-null fields) and Unsafe.As bypass constructors and readonly fields, and no library can prevent this.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:95-99` · high · sha:3aa554d9287d</sub>
- The body hierarchy is open by design, so a subclass of RequestBody can report IsReplayable for a body that writes different bytes each time, and the replay gate of section 3.1 trusts that property.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:99-101` · high · sha:3aa554d9287d</sub>
- Under the tr-TR culture, "TITLE".ToLower() returns "tıtle" and CurrentCultureIgnoreCase says title differs from TITLE, whereas ToLowerInvariant() and OrdinalIgnoreCase behave correctly.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:136-139` · high · sha:3aa554d9287d</sub>
- ToLowerInvariant is Unicode-aware and folds U+212A KELVIN SIGN to ASCII `k`, so a header named with the Kelvin sign would be treated as the same header as `key`.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:139-141` · high · sha:3aa554d9287d</sub>
- On Linux, `Uri.TryCreate("/rel", UriKind.Absolute, ...)` succeeds as a file: URI, so checking absoluteness alone never validates a request URL.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:177-178` · high · sha:3aa554d9287d</sub>
- Uri.Equals treats `https://a@h/` and `https://b@h/` as equal and ignores the fragment, so record-generated equality over Url would make requests with different userinfo credentials equal.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:178-181` · high · sha:3aa554d9287d</sub>
- HTTP-12 requires Status equality over the code only, and a naive `record struct Status(int Code, string? Name)` would generate equality over both members and violate it.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:206-209` · high · sha:3aa554d9287d</sub>

## Conclusions
- Rejecting a body on body-forbidding methods once at construction yields one portable behavior because reference transports diverge, with one throwing and one silently dropping the body.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:14-14` · high · sha:22d100d5bc94</sub>
- URLs are compared textually because some platforms' native URL equality resolves the host, which is blocking and wrong for virtual hosts sharing an IP.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:17-17` · high · sha:22d100d5bc94</sub>
- Status is total over integer codes because transports must faithfully surface vendor codes such as nginx 499 and Cloudflare 520-526/530.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:22-22` · high · sha:22d100d5bc94</sub>
- Outbound header names reject controls and non-ASCII because an embedded CR/LF is a header-splitting vector and non-ASCII names cannot be encoded by any reference transport.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:32-32` · high · sha:22d100d5bc94</sub>
- The outbound header value grammar takes the most restrictive shipped-transport stance and blocks CR/LF splitting.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:33-33` · high · sha:22d100d5bc94</sub>
- Inbound header values use a lenient grammar because RFC 7230 allows obs-text in field values (for example a Latin-1 Content-Disposition filename) and applying the outbound grammar to responses would silently drop legitimate headers.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:34-34` · high · sha:22d100d5bc94</sub>
- Validation messages avoid echoing header values to prevent log injection and secret leakage through error messages.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:35-35` · high · sha:22d100d5bc94</sub>
- Zero or negative timeouts and negative retry counts are rejected because zero-timeout means no timeout in one transport but is an error in another, and a negative retry count would be silently reinterpreted.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:48-48` · high · sha:22d100d5bc94</sub>
- The model-layer header check is the only line of defence for a transport built on the "without validation" API (§3.2, §4).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:39-42` · high · sha:d7cea7b15cf3</sub>
- For a body-less POST/PUT/PATCH the adapter relies on HttpClient's own verified behaviour of sending Content-Length: 0 for null content (TRANSPORT-26).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:360-362` · high · sha:da6000c93fc5</sub>
- The port uses record (or readonly record struct) as the base, with init accessors and with-expressions as the builder, and uses required members so an omitted member is a compile error (CS9035), which is stronger than HTTP-4's runtime "<name> is required".
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:9-14` · high · sha:3aa554d9287d</sub>
- Request, whose HTTP-7/HTTP-8 rules relate method and body, has get-only properties (a with-expression assigning one is compile error CS0200), a validating constructor, and With* methods (WithMethod, WithUrl, WithHeaders, WithHeader, WithBody, WithoutBody) that route every derivation back through that constructor.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:31-34` · high · sha:3aa554d9287d</sub>
- Status is a readonly record struct because its default (code 0) is admitted by HTTP-10's total mapping as an unrecognised status, and its equality is overridden to the code.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:48-48` · high · sha:3aa554d9287d</sub>
- Protocol is an enum with an explicit underlying type because it is a closed set with a total wire mapping, and an out-of-range cast such as (Protocol)42 is rejected by ToWireString with ArgumentOutOfRangeException.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:49-49` · high · sha:3aa554d9287d</sub>
- Method becomes a sealed record (overturning the as-built struct) because default would be a method with no token.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:50-50` · high · sha:3aa554d9287d</sub>
- HttpHeaderName becomes a sealed record (overturning the as-built struct) because default would be a name whose hash throws.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:51-51` · high · sha:3aa554d9287d</sub>
- MediaType, RequestOptions, ETag and HttpRange are sealed records because their defaults are invalid or meaningless, with per-member invariants in init or no public setter.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:52-52` · high · sha:3aa554d9287d</sub>
- Request is a sealed record with get-only properties, a validating constructor and With* methods because of the cross-member invariant of HTTP-7.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:53-53` · high · sha:3aa554d9287d</sub>
- Response is a sealed class implementing IDisposable/IAsyncDisposable rather than a record, because it owns a live body and a record's with would copy the disposal obligation into two objects.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:54-54` · high · sha:3aa554d9287d</sub>
- Headers and Query are each a sealed immutable class plus a nested Builder, for batched multimap edits.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:55-55` · high · sha:3aa554d9287d</sub>
- RequestBody and ResponseBody are public abstract classes with static factories and private sealed variants, the one deliberately open hierarchy, because transports and generated code implement bodies.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:56-56` · high · sha:3aa554d9287d</sub>
- A mutable builder object is kept only where edits are batched over a multimap, namely Headers.Builder (seeded by ToBuilder(), whose ImmutableDictionary builder copies on write so it never aliases the source) and a Query.Builder, making the builder exception a family of two.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:61-71` · high · sha:3aa554d9287d</sub>
- An object initializer with required members acts as the builder whose build() is the closing brace, and with acts as newBuilder() followed by build(), non-aliasing for any member of immutable type, which XCUT-15 makes every collection member.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:63-67` · high · sha:3aa554d9287d</sub>
- The multipart body gets a factory taking an immutable part list rather than a builder, since it has no batched-edit use.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:71-72` · high · sha:3aa554d9287d</sub>
- SEAM-29's shared generic Builder contract (build() producing the target type) is not ported, because with no builder objects an IBuilder<T> would have no implementers (the fabricated tier P11 forbids) and generic composition in C# takes a Func<T, T> instead.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:72-75` · high · sha:3aa554d9287d</sub>
- A missing required value is reported with ArgumentNullException.ThrowIfNull (field name in ParamName, message "Value cannot be null. (Parameter 'url')") rather than HTTP-4's "url is required", so callers' ArgumentException handling and analyzer CA2208 apply; recorded as section 10 entry 10.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:76-80` · high · sha:3aa554d9287d</sub>
- Collections are stored as System.Collections.Immutable types built once at construction and the same instance is returned from every accessor, so HTTP-5 needs no per-access wrapper.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:82-85` · high · sha:3aa554d9287d</sub>
- The encapsulation gap is mitigated by closed official construction paths, concrete sealed public types, and not by a fake proof; a caller using reflection or a lying subclass knowingly opts out.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:101-104` · high · sha:3aa554d9287d</sub>
- The residual encapsulation gap is a correctness-of-shape gap rather than a request-splitting gap, recorded as section 10 entry 11; the re-check is not optional on .NET because of the wire-level behaviour of TryAddWithoutValidation (section 3.2).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:106-109` · high · sha:3aa554d9287d</sub>
- Headers uses an ordered entry array with an ordinal index built alongside it, because ImmutableDictionary is a hash map (names added as z, a, m enumerate as a, z, m) and so does not meet HTTP-16's insertion-order requirement; the index keeps lookups constant-time.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:123-126` · high · sha:3aa554d9287d</sub>
- The port folds header names with System.Text.Ascii.ToLower (in-box on net8.0), which returns OperationStatus.InvalidData on non-ASCII input, so the fold and HTTP-17's non-ASCII rejection are one operation and relaxing HTTP-17 cannot silently break HTTP-13.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:141-144` · high · sha:3aa554d9287d</sub>
- Header names are held to the RFC 9110 token grammar, stricter than HTTP-17's floor (also rejecting separators such as space, `(` and `:`), because HttpClient refuses exactly those names anyway (TryAddWithoutValidation("a b", ...) returns false) and accepting them in the model would only move the failure to a silent transport drop; recorded as section 11 item 36.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:149-154` · high · sha:3aa554d9287d</sub>
- HTTP-22's optional header-name interning (a MAY) is not implemented, because the observable contract is value equality by folded name and well-known names are already cached statics on HttpHeaderName.WellKnown.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:160-162` · high · sha:3aa554d9287d</sub>
- HTTP-8's "no method set" case is unrepresentable rather than defaulted because Method is not optional in any Request constructor.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:174-175` · high · sha:3aa554d9287d</sub>
- The port drops TRACE from the idempotent set and removes both public IsSafe and IsIdempotent before 1.0, rather than keep an RFC-meaning accessor beside the SDK's specified set (section 11 item 22), even though RFC 9110 would agree TRACE is idempotent.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:221-223` · high · sha:3aa554d9287d</sub>
- System.Net.Http.Headers.MediaTypeHeaderValue is rejected as the model because it is mutable (CharSet is settable after parse) and lenient where HTTP-53 is strict (it accepts `text/plain; foo`, a parameter with no `=`).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:241-243` · high · sha:3aa554d9287d</sub>
- RequestOptions Timeout is a TimeSpan matching every .NET timeout API, where the Ruby port used float seconds for the same reason.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:271-272` · high · sha:3aa554d9287d</sub>
- ETag, HttpRange and RequestConditions (HTTP-48 to HTTP-50) are sealed records with validating factories, and the BCL EntityTagHeaderValue and RangeHeaderValue are unsuitable as model types because RangeHeaderValue.Ranges is a mutable collection that accepts a second range, whereas HTTP-49 supports only a single range.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:275-278` · high · sha:3aa554d9287d</sub>

## Reference
- The core HTTP domain model is the immutable, transport-agnostic, security-critical boundary between application code and the wire, fixing case-insensitivity, multi-value semantics, ordering, header-injection defenses, method/body legality and total status handling once so every transport behaves identically.
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:3-3` · high · sha:22d100d5bc94</sub>
- The rationale for header validation is that a bare CR/LF is the request/header-splitting vector.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46-46` · high · sha:d6123be82c9e</sub>
- Through the as-built adapter a model header value with CR/LF reached the socket the same way, a caller-set Host: evil.example replaced the real host line, and header names went out lower-cased (x-trace).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:343-345` · high · sha:da6000c93fc5</sub>
- TryAddWithoutValidation returns false for a header name with a space or non-ASCII byte (which the as-built adapter silently drops), accepts a non-ASCII value and lets the send fail later with HttpRequestException ("Request headers must contain only ASCII characters"), and accepts Transfer-Encoding.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:345-348` · high · sha:da6000c93fc5</sub>
- The as-built adapter tries request headers first and falls back to content headers only when content exists, so a Content-Type on a body-less request vanishes (verified on the wire) and the model's Content-Type is skipped whenever a body is present in favor of the body-derived media type, opposite to TRANSPORT-10.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:355-358` · high · sha:da6000c93fc5</sub>
- At commit d45e64b, `Request.Post(url, body) with { Method = Method.Get }` yields a GET carrying a body (forbidden by HTTP-7), `with { Url = new Uri("rel", UriKind.Relative) }` yields a request with a relative URL, and `with { Url = new Uri("ftp://h/x") }` yields an FTP request.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:16-21` · high · sha:3aa554d9287d</sub>
- The field-keyword init idiom compiles for a net8.0 target under the 10.0.401 SDK with LangVersion latest, so it does not wait for the target-framework floor to rise.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:25-27` · high · sha:3aa554d9287d</sub>
- The styleguide rule 3.8 (csharp/03-nullability-and-the-type-system.md) says to choose record, record struct or class by value semantics, and rule 6.6 of 06-types-and-data-modeling.md prefers readonly struct for small immutable values.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:36-38` · high · sha:3aa554d9287d</sub>
- Making Method and HttpHeaderName reference types costs an allocation only for an unrecognised verb or header name, because the well-known instances are cached statics.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:58-59` · high · sha:3aa554d9287d</sub>
- HTTP-3 distinguishes builder-based models (request, response, headers, query params, request options, request conditions, multipart body) from value-based types with no builder (media type, status, typed header name, ETag, HTTP range, method, protocol), which are derived by re-constructing through their factories.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:61-64` · high · sha:3aa554d9287d</sub>
- HTTP-5's conformance probe (a returned value list cannot be downcast-and-mutated) passes because downcasting the IReadOnlyList<string> returned by Headers.GetAll yields an ImmutableArray<string>, which has no mutating members and is immutable all the way down.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:85-91` · high · sha:3aa554d9287d</sub>
- Section 4 as built at d45e64b is partial: records, With* helpers and immutable collections exist; Request exposes public init properties that bypass its constructor, Method and HttpHeaderName are structs with invalid defaults, and there is no Query or RequestOptions type.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:111-113` · high · sha:3aa554d9287d</sub>
- Headers (HTTP-13 to HTTP-22) are an insertion-ordered immutable multimap with one entry per distinct name, holding the folded name (for lookup, containment, mutation, removal, equality and hashing), the original casing of the first insertion (for wire emission, HTTP-21), and an ImmutableArray<string> of values in insertion order (HTTP-14).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:117-120` · high · sha:3aa554d9287d</sub>
- The as-built Headers is an ImmutableDictionary<string, ImmutableArray<string>> keyed by the lower-cased name, which is immutable and case-insensitive but wrong on order, equality and casing.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:120-122` · high · sha:3aa554d9287d</sub>
- The as-built Headers does not override Equals, so two header sets with identical content are unequal, and so are two Requests built from them.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:127-129` · high · sha:3aa554d9287d</sub>
- The as-built transport emits `x-trace` for a model header `X-Trace` because only the folded name is stored.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:131-132` · high · sha:3aa554d9287d</sub>
- In the as-built code, HttpHeaderName.Of("a\rb") throws "Invalid header-name character '\r'" with the raw carriage return in the message.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:154-156` · high · sha:3aa554d9287d</sub>
- Headers as built at d45e64b diverges: no validation, non-ASCII-aware fold, enumeration not in insertion order, no value equality, original casing lost, no inbound lenient path; HttpHeaderName rejects surrounding whitespace instead of trimming, echoes raw control characters in its message, and is a struct.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:165-167` · high · sha:3aa554d9287d</sub>
- The as-built Request constructor does not check HTTP-7 at all: `new Request(Method.Get, url, null, body)` succeeds.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:173-175` · high · sha:3aa554d9287d</sub>
- The as-built message "Request URL must be an absolute URI." omits the offending input.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:176-177` · high · sha:3aa554d9287d</sub>
- System.Uri never resolves DNS, so the JVM URL hazard that HTTP-46's textual comparison guards against does not exist on .NET.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:181-184` · high · sha:3aa554d9287d</sub>
- As built, `Method.Of("GET\r\nX: y")` is accepted and only `new HttpMethod(...)` in the transport rejects it with a FormatException that escapes the transport unmapped.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:185-186` · high · sha:3aa554d9287d</sub>
- The as-built EnsureSuccessAsync throws HttpResponseException for any non-2xx including a 304, contradicting BODY-31 and XCUT-8.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:192-195` · high · sha:3aa554d9287d</sub>
- The as-built EnsureSuccessAsync caps the error body at 1 MiB but never disposes the original response, so the live connection outlives the throw.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:195-197` · high · sha:3aa554d9287d</sub>
- Request and Response as built at d45e64b diverge: Request has public init properties, no HTTP-7 check, Uri.Equals equality and a message without the input; Response lacks request and reason phrase; EnsureSuccessAsync throws on 3xx and does not dispose the original response.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:199-201` · high · sha:3aa554d9287d</sub>
- The as-built Status stores the name but overrides Equals(Status) and GetHashCode to the code, and the generated == routes through that override (`Status.FromCode(200) == Status.Ok`).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:209-211` · high · sha:3aa554d9287d</sub>
- The idempotent set is the FrozenSet<Method> in the internal RetryFacts (section 6.1), read by an internal member of Method.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:216-217` · high · sha:3aa554d9287d</sub>
- The as-built Method has a public IsIdempotent defined as safe methods plus PUT and DELETE over a public IsSafe that includes TRACE, so it answers true for TRACE.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:219-221` · high · sha:3aa554d9287d</sub>
- Protocol's H2PriorKnowledge and Quic members mirror the reference's protocol list, and .NET handlers report HTTP/3 as version 3.0, which the adapter maps to Quic.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:230-231` · high · sha:3aa554d9287d</sub>
- Status, Method and Protocol as built at d45e64b diverge: Status lacks IsError and a recognised-code lookup; Method is a struct, has public IsSafe/IsIdempotent (the latter including TRACE), and Method.Of accepts any non-blank string.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:233-235` · high · sha:3aa554d9287d</sub>
- The as-built media-type parser is a character scanner with no regular expression, so the Ruby port's regex-timeout hardening has no counterpart, and it is correct on quoting, first-`=` splitting and case normalisation (`Application/JSON;Charset=UTF-8` becomes `application/json`, key `charset`, value `UTF-8`).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:243-247` · high · sha:3aa554d9287d</sub>
- The as-built MediaType accepts a parameter value containing CR/LF and renders it inside quotes, so it can carry a line break into a Content-Type header.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:250-252` · high · sha:3aa554d9287d</sub>
- RequestOptions are the per-call options threaded through both transport interfaces (sections 3.2, 3.3) and are distinct from DexpaceClientOptions, which is client-level configuration (section 8.2).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:272-273` · high · sha:3aa554d9287d</sub>
- Section 4.4 as built at d45e64b is partial: MediaType accepts empty parameter values and control characters in values and has no TryParse; Query, RequestOptions, ETag, HttpRange and RequestConditions are not built.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:282-283` · high · sha:3aa554d9287d</sub>

## Conflicts

## Superseded

