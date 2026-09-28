# http-domain-model

## Rules
- The request builder MUST reject a non-null body on any method whose classification forbids one (GET, HEAD, TRACE, CONNECT), failing at construction rather than deferring to the transport (HTTP-7).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:14-14` · high · sha:22d100d5bc94</sub>
- With no method set, build() SHOULD default to GET only if no body is present; a body with no method SHOULD fail reporting a missing method rather than defaulting to GET and then tripping the no-body-on-GET rule (HTTP-8).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:15-15` · high · sha:22d100d5bc94</sub>
- Request URL equality/hashing MUST NOT perform blocking work or DNS resolution: URLs are compared by textual external form, and request equality otherwise compares method, headers, and body by value (HTTP-46).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:17-17` · high · sha:22d100d5bc94</sub>
- Building a request from a malformed URL string or non-absolute URI SHOULD fail with an argument error carrying the offending input (HTTP-47).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:18-18` · high · sha:22d100d5bc94</sub>
- Status MUST be a total function of the integer code: mapping any code returns a Status (never throws), with a canonical named instance for recognized codes and a raw-code, null-named instance for unrecognized ones, and a separate lookup lets callers distinguish recognized codes (HTTP-10).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:22-22` · high · sha:22d100d5bc94</sub>
- Two Status values MUST be equal if and only if their numeric codes are equal; the name does not participate in equality or hashing (HTTP-12).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:24-24` · high · sha:22d100d5bc94</sub>
- Header names MUST be treated case-insensitively for storage, lookup, containment, mutation, removal, equality, and hashing, folding to lower case with an ASCII/invariant rule, never a locale-sensitive fold (HTTP-13).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:28-28` · high · sha:22d100d5bc94</sub>
- The header model MUST support multiple values per name — add appends, set replaces the whole list — preserving per-name insertion order (HTTP-14).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:29-29` · high · sha:22d100d5bc94</sub>
- Setting a header value to null MUST remove the header entirely (HTTP-15).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:30-30` · high · sha:22d100d5bc94</sub>
- The header model SHOULD preserve insertion order of distinct names for deterministic serialization, caching, signing, and test stability (HTTP-16).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:31-31` · high · sha:22d100d5bc94</sub>
- Outbound (caller-set) header names MUST be validated at construction: reject a blank name, any C0 control (0x00–0x1F, including CR/LF/NUL) or DEL (0x7F), and any non-ASCII byte (≥0x80); surrounding whitespace is trimmed before validation (HTTP-17).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:32-32` · high · sha:22d100d5bc94</sub>
- Outbound header values MUST reject any control character (C0 and DEL) except horizontal tab (0x09) and reject any non-ASCII byte, accepting only HTAB plus printable ASCII 0x20–0x7E (HTTP-18).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:33-33` · high · sha:22d100d5bc94</sub>
- The model MUST provide a distinct lenient path for inbound (response) header values that relaxes the non-ASCII rule (permitting obs-text ≥0x80) while still rejecting control characters (C0 except HTAB, plus DEL); inbound names remain strictly validated (HTTP-19).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:34-34` · high · sha:22d100d5bc94</sub>
- Validation error messages MUST NOT echo the offending header value verbatim and MUST escape any control characters in an echoed header name, to prevent log injection and secret leakage (HTTP-20).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:35-35` · high · sha:22d100d5bc94</sub>
- A typed header-name abstraction MUST compare/hash by its case-folded form while preserving original casing for wire emission, MUST interoperate with the string-keyed API, and MUST enforce the same name validation as outbound header names (HTTP-21).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:36-36` · high · sha:22d100d5bc94</sub>
- Media-type construction MUST lower-case the type, subtype, and every parameter key while preserving each parameter value's case; equality is case-insensitive on type/subtype/keys and case-sensitive on values (HTTP-23).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:37-37` · high · sha:22d100d5bc94</sub>
- Media type MUST resolve its charset parameter case-insensitively and return null (not throw) when the charset is absent or unknown, so callers fall back to a default (HTTP-24).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:38-38` · high · sha:22d100d5bc94</sub>
- Media-type parsing MUST split parameters respecting quoted-strings, split each parameter on its first "=" only, strip quotes, and unescape quoted-pairs, while rendering MUST emit a value bare when it is a valid token and quoted-and-escaped otherwise, so that parse(render(x)) == x (HTTP-25).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- Media-type parsing MUST reject blank input and require a non-empty type before and non-empty subtype after a single "/", and each parameter must contain "=" with a non-empty key and value (HTTP-53).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- Media-type construction MUST reject a control character (C0 except HTAB, plus DEL) or non-ASCII byte anywhere, using the same predicate as outbound header-value validation, so a media type is always header-safe (HTTP-26).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- Media-type wildcard matching SHOULD permit a wildcard type only paired with a wildcard subtype (bare */*), with a wildcard in either position matching any value and parameters ignored (HTTP-27).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:39-39` · high · sha:22d100d5bc94</sub>
- Query-parameter names MUST be case-sensitive (no folding), preserve insertion order, support multiple values, and model a value-less parameter (?flag) as a single empty-string value distinct from an absent name (HTTP-28).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:40-40` · high · sha:22d100d5bc94</sub>
- Query encoding MUST render each name/value with RFC 3986 percent-encoding (space to %20, literal "+" to %2B, "/" to %2F, "*" to %2A), encoding everything except the unreserved set A–Z a–z 0–9 - . _ ~, preserving insertion order, emitting a repeated name once per value, omitting the leading "?", and returning empty when empty, which is explicitly not application/x-www-form-urlencoded (HTTP-29).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:41-41` · high · sha:22d100d5bc94</sub>
- A single query component's encoding SHOULD follow RFC 3986 independent of standard-library quirks: space is %20 never "+", a literal "+" is %2B, decoding leaves "+" as "+", "~" stays unencoded, and "*" is encoded (HTTP-32).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:41-41` · high · sha:22d100d5bc94</sub>
- Query equality MUST be order-sensitive — two instances equal iff they encode identically — and a name whose value list is empty MUST be dropped at build time so it cannot leave a phantom contains-true entry invisible to encode (HTTP-30).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:42-42` · high · sha:22d100d5bc94</sub>
- Query parsing MUST invert encoding and be lenient — a null/blank query becomes empty, a leading "?" is tolerated, a segment with no "=" or a trailing "=" becomes an empty-string value, a stray "&" is skipped, and malformed percent-encoding falls back to raw text rather than throwing (HTTP-31).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:42-42` · high · sha:22d100d5bc94</sub>
- Request options MUST model per-call operational overrides that are not part of the wire form — at minimum a per-call timeout, a per-call max-retries, and opaque string-keyed tags — with every field defaulting to a null/empty "use the default" sentinel, a canonical EMPTY "override nothing" instance, and tags defensively copied at build (HTTP-34).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:47-47` · high · sha:22d100d5bc94</sub>
- The request-options builder MUST reject a non-null timeout that is zero or negative and MUST reject a negative max-retries, while a max-retries of 0 MUST be accepted and means "disable retries for this call" (HTTP-35).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:48-48` · high · sha:22d100d5bc94</sub>
- An ETag helper SHOULD model strong ("opaque"), weak (W/"opaque"), and the any singleton (*) forms, validate permitted etagc characters (rejecting a literal quote, control chars, DEL; permitting obs-text), reject an empty strong opaque, permit an empty weak opaque, round-trip its raw form, reject unterminated forms, and return absent for blank input (HTTP-48).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:52-52` · high · sha:22d100d5bc94</sub>
- An HTTP-range helper SHOULD provide validated factories for a bounded range (rejecting negative offset/non-positive length, detecting overflow), a suffix range, and an open-ended range, supporting only the "bytes" unit and a single range (rejecting multi-range commas), and storing a parsed value verbatim (HTTP-49).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:53-53` · high · sha:22d100d5bc94</sub>
- A conditional-requests aggregator SHOULD emit If-Match/If-None-Match as one comma-separated header, emit If-Modified-Since/If-Unmodified-Since as RFC 1123 dates, be idempotent when applied (using set, not add), and enforce that the any-tag (*) is mutually exclusive with concrete entity-tags, collapsing repeated "*" to one (HTTP-50).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:54-54` · high · sha:22d100d5bc94</sub>
- Header names and outbound header values MUST be validated at the transport-agnostic model layer before reaching any transport. (XCUT-18)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46` · high · sha:d6123be82c9e</sub>
- Inbound response header values MAY be validated leniently, permitting obs-text, but MUST still reject control bytes other than horizontal tab. (XCUT-18)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46` · high · sha:d6123be82c9e</sub>

## Constraints

## Conclusions

## Reference
- A request MUST carry exactly method, target URL, headers (non-null, possibly empty), and an optional body; a response MUST carry the originating request, negotiated protocol, status, an optional reason phrase, headers (non-null, possibly empty), and an optional body, with operational knobs like timeout and retries living outside the wire model (HTTP-6).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:13-13` · high · sha:22d100d5bc94</sub>
- The model defines an idempotency classification set {GET, HEAD, OPTIONS, PUT, DELETE}, and each method's canonical wire token MUST equal its uppercase name (HTTP-9).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:16-16` · high · sha:22d100d5bc94</sub>
- Status classifies by range: informational 100–199, success 200–299, redirect 300–399, client-error 400–499, server-error 500–599, and error 400–599, and a response exposes these classifications derived from its status (HTTP-11).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:23-23` · high · sha:22d100d5bc94</sub>
- A typed header name MAY intern instances process-wide with first casing winning, since interning is an optimization and the observable contract is value equality by case-folded name (HTTP-22).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:36-36` · high · sha:22d100d5bc94</sub>
- Protocol exposes a canonical lower-case wire form (http/1.1, http/2) and a locale-invariant, case-insensitive parse accepting the canonical forms plus the aliases HTTP/2 and HTTP/2.0, throwing on an unrecognized identifier (HTTP-33).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:43-43` · high · sha:22d100d5bc94</sub>
- Header names MUST reject all C0 control bytes (0x00-0x1F, including CR, LF, NUL, and HTAB) and DEL (0x7F). (XCUT-18)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46` · high · sha:d6123be82c9e</sub>
- Outbound header values MUST reject the same control-byte set as header names except horizontal tab (0x09), which is accepted. (XCUT-18)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46` · high · sha:d6123be82c9e</sub>
- Both header names and outbound header values MUST reject non-ASCII bytes (0x80 and above). (XCUT-18)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:46` · high · sha:d6123be82c9e</sub>

## Conflicts

## Superseded
