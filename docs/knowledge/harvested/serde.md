# serde

## Rules
- SEAM-19 (MUST) - The wire-codec seam MUST bundle a serializer, a deserializer, and the media type its serializer produces.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:24-24` · high · sha:0adae2d6a47f</sub>
- The media type MUST NOT be defaulted at the seam level, and each codec declares its own (SEAM-19).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:24-24` · high · sha:0adae2d6a47f</sub>
- SEAM-21 (MUST) - Deserialization MUST require an explicit runtime type token rather than an erased/inferred generic, so a language with type erasure recovers the intended type instead of silently producing a generic map/list.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:25-25` · high · sha:0adae2d6a47f</sub>
- Parametric deserialization targets MUST be expressible through a full generic type capture (SEAM-21).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:25-25` · high · sha:0adae2d6a47f</sub>
- SERDE-3 (MUST) - When encoding into or decoding from a caller-supplied stream, the serializer/deserializer must read/write the payload fully (to EOF on the read side) but must not close or take ownership of the caller's stream, even when the codec's own auto-close feature is enabled.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:12-12` · high · sha:c6bc7789c3a9</sub>
- SERDE-3 (MUST) - The encode-into-buffer profile touches only the target region and never assumes ownership of the buffer.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:12-12` · high · sha:c6bc7789c3a9</sub>
- SERDE-4 (MUST) - The encode-into-buffer profile must return the number of bytes written, must honor a start offset, and must leave bytes before the offset untouched.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:13-13` · high · sha:c6bc7789c3a9</sub>
- SERDE-4 (MUST) - The encode-into-buffer profile must throw a range/overflow error, distinct from the serde exception type and not chaining one, when the offset is out of range or the payload does not fit.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:13-13` · high · sha:c6bc7789c3a9</sub>
- SERDE-5 (MUST) - Every decode operation must take an explicit runtime type witness for the target type and must not rely on erased compile-time generics, because on an erasure-based runtime that silently yields an untyped map/list which fails as a cast error on first field access.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:17-17` · high · sha:c6bc7789c3a9</sub>
- SERDE-6 (MUST) - Parametric decode targets (e.g. List<Dto>) must be expressible through a full-generic type carrier that preserves element types across erasure.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:18-18` · high · sha:c6bc7789c3a9</sub>
- SERDE-6 (MUST) - A format-agnostic decoder that cannot resolve type arguments must fail loudly with a serde exception for a genuinely parametric carrier rather than silently decoding into the raw type, while a carrier wrapping a plain class must still decode via the raw path.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:18-18` · high · sha:c6bc7789c3a9</sub>
- SERDE-7 (MUST) - An ergonomic reified/inline decode helper, where the host language offers one, must capture the full generic type and route through the generic carrier rather than forwarding only the raw class.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:19-19` · high · sha:c6bc7789c3a9</sub>
- SERDE-8 (MUST) - The generic type carrier must capture a concrete, fully-resolved type at construction and must reject construction with no type argument or with an unresolved type variable (e.g. created inside a generic function or subclass where the argument erases to its bound), failing fast with an actionable message.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:20-20` · high · sha:c6bc7789c3a9</sub>
- SERDE-9 (MUST) - Encode/decode failures must surface as the SDK's stable serde exception type or a subtype.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:24-24` · high · sha:c6bc7789c3a9</sub>
- SERDE-9 (MUST) - Adapters must catch the backing codec's processing failures and rethrow them as the serde type with the original chained as the cause, and must not allow a backing-library exception type to escape the SPI.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:24-24` · high · sha:c6bc7789c3a9</sub>
- SERDE-10 (MUST) - Write-path failures must be a serialization-specific subtype and read-path failures a deserialization-specific subtype, both of the common serde root, so callers can distinguish direction while catching one base type.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:25-25` · high · sha:c6bc7789c3a9</sub>
- SERDE-11 (SHOULD) - Serde failures should be unchecked/runtime rather than checked/declared, so callers are not forced to wrap every round-trip; on languages without checked exceptions the intent is a normal error that is not part of the declared signature.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:26-26` · high · sha:c6bc7789c3a9</sub>
- SERDE-12 (MUST) - A genuine stream I/O error raised while reading/writing a caller-owned stream must propagate unwrapped as an I/O error and must not be re-wrapped as a serde exception; only malformed-input, shape-mismatch and unencodable-value failures are wrapped.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:27-27` · high · sha:c6bc7789c3a9</sub>
- SERDE-13 (MUST) - Decoding a wire null literal into a non-null target type must fail with a deserialization exception naming the target type across every decode overload, and must not return a null that flows through the non-null result and fails later.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:28-28` · high · sha:c6bc7789c3a9</sub>
- SERDE-14 (SHOULD) - The tri-state type should be covariant in its value type.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:32-32` · high · sha:c6bc7789c3a9</sub>
- SERDE-15 (MUST) - When serializing a tri-state field within an object, Absent must omit the key entirely, Null must emit the key with a wire null, and Present must emit the key with the encoded inner value, because a PATCH server treats an omitted key as "leave unchanged" and an explicit null as "clear".
  <sub>spec · `docs/product-spec/14-serialization-serde.md:33-33` · high · sha:c6bc7789c3a9</sub>
- SERDE-16 (MUST) - When deserializing a tri-state field, a missing key yields Absent, a present explicit null yields Null, and a present value yields Present(value) with the inner value's declared element type preserved.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:34-34` · high · sha:c6bc7789c3a9</sub>
- SERDE-17 (MUST) - A tri-state field with no key on the wire must resolve to Absent, which requires the field's declared default to be Absent and the decoder's empty-value fallback to yield Absent, because the codec short-circuits a missing key before the decoder's null hook runs.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:35-35` · high · sha:c6bc7789c3a9</sub>
- SERDE-18 (SHOULD) - The tri-state type should provide factories for absent, explicit-null and present(non-null), a nullable-to-(present|null) mapper that can never yield Absent, a three-way fold, a value-or-null accessor, and is-absent/is-null/is-present predicates.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:36-36` · high · sha:c6bc7789c3a9</sub>
- SERDE-19 (MUST) - The default codec configuration must wire the tri-state PATCH semantics, and an adapter building a serde around a caller-supplied codec must register that wiring by default.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:37-37` · high · sha:c6bc7789c3a9</sub>
- SERDE-19 (MAY) - An adapter may allow opting out of the tri-state wiring only for a caller that already installed equivalent wiring; without the wiring, Absent and Null become indistinguishable on the wire.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:37-37` · high · sha:c6bc7789c3a9</sub>
- SERDE-20 (SHOULD) - When a tri-state value is serialized with no enclosing object able to omit a key (a top-level value or an array element), the implementation should degrade gracefully by emitting a wire null for both Absent and Null, and for an array element emitting null for Absent rather than throwing.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:38-38` · high · sha:c6bc7789c3a9</sub>
- SERDE-30 (MAY) - The absent and explicit-null tri-state sentinels may provide a stable, identity-free textual representation (e.g. "Absent", "Null") so logs and assertions do not leak an identity hash.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:39-39` · high · sha:c6bc7789c3a9</sub>
- SERDE-21 (MUST) - The default decoder configuration must reject cross-shape scalar coercions (string to integer, string to float, string to boolean, empty-string to integer/float/boolean, float to integer, boolean to integer, integer to boolean, boolean to float, and any non-string scalar to string) and a rejected coercion must surface as a deserialization failure.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:43-43` · high · sha:c6bc7789c3a9</sub>
- SERDE-22 (MUST) - The strict-coercion policy must still permit representation-preserving conversions: numeric widening of an integer into a floating-point target, an empty string into a textual target, and any well-typed value binding to its matching target.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:44-44` · high · sha:c6bc7789c3a9</sub>
- SERDE-23 (SHOULD) - The default decoder configuration should ignore unknown/unexpected fields rather than failing, so a server can add backward-compatible fields ahead of a client model update.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:45-45` · high · sha:c6bc7789c3a9</sub>
- SERDE-24 (SHOULD) - The default encoder configuration should emit date/time values as ISO-8601 strings rather than numeric epoch timestamps, and whichever form is chosen must round-trip to the same instant.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:46-46` · high · sha:c6bc7789c3a9</sub>
- SERDE-25 (SHOULD) - A factory building the default codec configuration should return a fresh, independent instance on each call rather than a shared mutable singleton, because codec instances carry mutable caches that interact poorly with post-construction reconfiguration.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:47-47` · high · sha:c6bc7789c3a9</sub>
- SERDE-26 (MUST) - When a serde is built around a caller-supplied codec instance, the SDK must not mutate the caller's instance during normal construction and must operate on a private copy of the codec engine.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:48-48` · high · sha:c6bc7789c3a9</sub>
- SERDE-26 (MAY) - If the codec cannot be copied, the implementation may fall back to using the supplied instance directly, but this mutating fallback must be documented behavior, not silent.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:48-48` · high · sha:c6bc7789c3a9</sub>
- SERDE-29 (SHOULD) - A configured serde should be safe to share across concurrent threads/tasks once configuration is complete and no longer mutated, and any per-type sub-serializer caches should use non-blocking, publication-safe updates rather than coarse locks.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:49-49` · high · sha:c6bc7789c3a9</sub>
- SERDE-27 (MUST) - A response-decoding handler must stream the response body directly through the deserializer into the target value without first materializing the whole body, and must consume and close the response on every path.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:53-53` · high · sha:c6bc7789c3a9</sub>
- SERDE-27 (MUST) - A response-decoding handler must surface a missing body (e.g. 204) as a serde exception naming the target type, and must surface a codec/parse failure as a serde exception chaining the original while letting a genuine mid-stream I/O error propagate unwrapped.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:53-53` · high · sha:c6bc7789c3a9</sub>
- SERDE-28 (MUST) - A status-aware handler must decode the body only on a 2xx status.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:54-54` · high · sha:c6bc7789c3a9</sub>
- SERDE-28 (MUST) - On 4xx/5xx a status-aware handler must throw the mapped HTTP-error exception carrying a bounded, buffered in-memory copy of the error body (readable after the live response closes) instead of decoding the error payload as the success type.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:54-54` · high · sha:c6bc7789c3a9</sub>
- SERDE-28 (MUST) - On any other non-2xx status (1xx, or an unfollowed 3xx such as 304) a status-aware handler must close the response and raise a serde exception whose message leads with the status code and preserves conditional/redirect context (ETag / Location).
  <sub>spec · `docs/product-spec/14-serialization-serde.md:54-54` · high · sha:c6bc7789c3a9</sub>
- SERDE-2 (MUST NOT) - The Serde media type must not be defaulted to a format-agnostic constant at the SPI level, because such a default would let a non-JSON serde silently stamp the wrong Content-Type.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:8-8` · high · sha:c6bc7789c3a9</sub>
- SERDE-1: a serde bundle round-trips through its own serializer and deserializer.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:29-29` · high · sha:0451cc7f3bb4</sub>
- SERDE-2: the declared media type is the default Content-Type and is not defaulted at the SPI.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:29-29` · high · sha:0451cc7f3bb4</sub>
- SERDE-3: streaming and buffer targets are not closed by the serde.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:30-30` · high · sha:0451cc7f3bb4</sub>
- SERDE-4: encode-into-buffer returns the length, honors the offset and throws a non-serde range error on overflow.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:30-30` · high · sha:0451cc7f3bb4</sub>
- SERDE-5: decode requires an explicit type witness.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:31-31` · high · sha:0451cc7f3bb4</sub>
- SERDE-6: parametric carriers preserve element types, and a no-codec decoder fails loudly on a parametric ref but decodes a plain-class carrier.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:31-31` · high · sha:0451cc7f3bb4</sub>
- SERDE-7: the reified helper routes through the carrier.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:31-31` · high · sha:0451cc7f3bb4</sub>
- SERDE-8: the carrier rejects an unresolved type variable at construction.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:31-31` · high · sha:0451cc7f3bb4</sub>
- SERDE-9: failures surface the stable serde exception type chaining the original cause, and no library type escapes.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:32-32` · high · sha:0451cc7f3bb4</sub>
- SERDE-10: write and read failures have directional subtypes.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:32-32` · high · sha:0451cc7f3bb4</sub>
- SERDE-11: serde failures are unchecked.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:32-32` · high · sha:0451cc7f3bb4</sub>
- SERDE-12: a genuine stream I/O error propagates unwrapped.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:32-32` · high · sha:0451cc7f3bb4</sub>
- SERDE-13: a wire null into a non-null target fails naming the type across overloads.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:32-32` · high · sha:0451cc7f3bb4</sub>
- SERDE-14: the tristate has three states and Present(null) is unrepresentable.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:33-33` · high · sha:0451cc7f3bb4</sub>
- SERDE-15: Absent omits the key, Null emits null and Present emits the value.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:33-33` · high · sha:0451cc7f3bb4</sub>
- SERDE-16: round-trip decoding works for `{}`, `{"x":null}` and `{"x":v}`.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:33-33` · high · sha:0451cc7f3bb4</sub>
- SERDE-17: a missing key decodes to Absent via the field default.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:33-33` · high · sha:0451cc7f3bb4</sub>
- SERDE-18: construction and consumption helpers exist and ofNullable never yields Absent.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:33-33` · high · sha:0451cc7f3bb4</sub>
- SERDE-19: the default codec auto-registers the tristate wiring.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:33-33` · high · sha:0451cc7f3bb4</sub>
- SERDE-20: tristate degrades at the top level and for array elements.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:33-33` · high · sha:0451cc7f3bb4</sub>
- SERDE-30: the tristate sentinel strings are stable.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:33-33` · high · sha:0451cc7f3bb4</sub>
- SERDE-21: strict cross-shape coercion is rejected.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:34-34` · high · sha:0451cc7f3bb4</sub>
- SERDE-22: widening and empty-string-to-string coercion are permitted.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:34-34` · high · sha:0451cc7f3bb4</sub>
- SERDE-23: unknown fields are ignored.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:34-34` · high · sha:0451cc7f3bb4</sub>
- SERDE-24: ISO-8601 dates round-trip.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:34-34` · high · sha:0451cc7f3bb4</sub>
- SERDE-25: each factory call returns a fresh codec.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:34-34` · high · sha:0451cc7f3bb4</sub>
- SERDE-26: a caller-supplied codec is not mutated.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:34-34` · high · sha:0451cc7f3bb4</sub>
- SERDE-29: a codec is thread-safe when shared after configuration.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:34-34` · high · sha:0451cc7f3bb4</sub>
- SERDE-27: the streaming response handler closes on all paths and handles a missing body, missing codec and I/O errors.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:35-35` · high · sha:0451cc7f3bb4</sub>
- SERDE-28: the status-aware handler decodes only 2xx, buffers a bounded 4xx/5xx error body and raises a status-naming serde exception for other non-2xx.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:35-35` · high · sha:0451cc7f3bb4</sub>
- Core must not name any concrete codec implementation (SEAM-2, restated by SERDE-1/SERDE-2).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:538-540` · high · sha:da6000c93fc5</sub>
- The codec seam bundles a serializer, a deserializer and the media type its serializer produces, and that media type is never defaulted at the seam level (SEAM-19, SERDE-1, SERDE-2).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:538-542` · high · sha:da6000c93fc5</sub>
- The codec seam must offer the common allocation profiles: produce a fresh string, produce a fresh byte array, stream into a caller-owned output, and encode into a caller-owned scratch buffer at an offset (SEAM-20).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:546-549` · high · sha:da6000c93fc5</sub>
- The buffer-encoding profile MUST return the number of bytes written, MUST honor a start offset, and MUST throw a range/overflow error that is distinct from the serde exception type and does not chain one (SERDE-4).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:548-550` · high · sha:da6000c93fc5</sub>
- The string serialization profile decodes the UTF-8 buffer, and a codec whose wire format is not UTF-8 text overrides it through an optional interface rather than the seam guessing.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:567-569` · high · sha:da6000c93fc5</sub>
- Neither caller-owned serialization variant closes the caller's target; an adapter over a TextWriter-based library (such as Newtonsoft's JsonTextWriter over a StreamWriter) must configure away the leaveOpen trap, asserted per adapter in Dexpace.Sdk.Conformance.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:569-572` · high · sha:da6000c93fc5</sub>
- DeserializeAsync reads the stream to EOF and does not dispose the caller's stream (SEAM-21, SERDE-3).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:572-573` · high · sha:da6000c93fc5</sub>
- The adapter must not freeze the caller's options object (SERDE-26) and so copies first with new JsonSerializerOptions(callerOptions) and freezes only its private copy.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:621-624` · high · sha:da6000c93fc5</sub>
- SERDE-26 requires that the SDK MUST NOT mutate the caller's options instance during normal construction and MUST operate on a private copy.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:375-376` · high · sha:68af5c6bf0ea</sub>
- CFG-29's canonical date format is DateTimeOffset.ToString("r", CultureInfo.InvariantCulture) (verified: Sun, 06 Nov 1994 08:49:37 GMT).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:294-296` · high · sha:ddf8f695ff61</sub>
- The date parser strips the weekday and comma, normalises the zone, and parses the rest with ParseExact over an explicit invariant format (month names already match case-insensitively); section 6.1 uses the same parser for Retry-After.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:299-301` · high · sha:ddf8f695ff61</sub>

## Constraints
- SERDE-14 (MUST) - The PATCH tri-state type must model exactly three states (Absent for a missing key, Null for an explicit null, Present carrying a value) and must make the illegal fourth state, Present of a null value, unrepresentable through the public API by bounding Present to non-null values.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:32-32` · high · sha:c6bc7789c3a9</sub>
- SERDE-1 (MUST) - A Serde must be a single bundle exposing exactly one encoder and one decoder for one wire format, so consumers acquire both through one reference.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:7-7` · high · sha:c6bc7789c3a9</sub>
- SERDE-2 (MUST) - A Serde must declare the wire media type it produces, and that media type must be used as the default Content-Type when a request body is created from a value plus a Serde.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:8-8` · high · sha:c6bc7789c3a9</sub>
- The free generic path is reflection over typeof(T), and reflection is exactly what trimming removes, so the witness that works under trimming is a JsonTypeInfo<T> from a source-generated context (hidden precondition per P7).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:74-77` · high · sha:d7cea7b15cf3</sub>
- Verified on 10.0.401, a file-based app defaults to AOT-compatible settings, and a reflection-based JsonSerializer.SerializeAsync(stream, 1) in it throws InvalidOperationException ("Reflection-based serialization has been disabled").
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:75-78` · high · sha:d7cea7b15cf3</sub>
- Under trimming or NativeAOT reflection-based System.Text.Json is disabled, so JsonSerializer.SerializeAsync(stream, 1) throws InvalidOperationException ("Reflection-based serialization has been disabled") in an AOT-defaulted app; the working type witness on .NET is JsonTypeInfo<T> from a source-generated JsonSerializerContext.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:583-587` · high · sha:da6000c93fc5</sub>
- Reflection is what trimming and NativeAOT remove, so the free type token holds only when metadata comes from a source-generated JsonSerializerContext as a JsonTypeInfo<T>.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:282-286` · high · sha:68af5c6bf0ea</sub>
- Nullable reference annotations are erased at run time (typeof(Dto?) == typeof(Dto)), so no serializer can know whether the caller of DeserializeAsync<Dto> wanted null to be legal (SERDE-13).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:310-313` · high · sha:68af5c6bf0ea</sub>
- System.Text.Json's RespectNullableAnnotations exists only in STJ 9+, so it is available only on the net10.0 target because the .NET 8 shared framework carries STJ 8.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:312-314` · high · sha:68af5c6bf0ea</sub>
- SERDE-14's covariance SHOULD cannot be met because .NET variance exists only on interfaces and delegates, never on a struct (recorded as section 10 entry 21).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:329-332` · high · sha:68af5c6bf0ea</sub>
- A converter cannot make Absent omit the key because the property name is written before the converter runs, which is why the ShouldSerialize modifier is needed (SERDE-15).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:343-345` · high · sha:68af5c6bf0ea</sub>
- As built, SystemTextJsonSerde's constructor calls options.MakeReadOnly() on the caller's JsonSerializerOptions, so the caller's later options.WriteIndented = true throws InvalidOperationException (verified), violating SERDE-26.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:375-378` · high · sha:68af5c6bf0ea</sub>
- ParseExact(s, "r", ...) validates the weekday (Mon, 06 Nov 1994 throws FormatException because that date was a Sunday) and rejects the UTC zone token (both verified), whereas CFG-30 says the leading weekday is informational only and MUST NOT be validated and requires GMT/UTC/+0000/+00:00.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:296-299` · high · sha:ddf8f695ff61</sub>

## Conclusions
- The generic-erasure constraint DOES NOT HOLD on .NET because the CLR reifies generics, so inside Deserialize<T> typeof(T) is the full closed type including List<Dto>, and SERDE-5 through SERDE-8's type-witness machinery is nearly free (§3.4, §7.3).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:72-74` · high · sha:d7cea7b15cf3</sub>
- The .NET codec seam is the ISerde interface in Dexpace.Sdk.Core.Serialization, with a required DefaultMediaType property each codec declares (the seam supplies no value), an asynchronous Stream pair, and a synchronous IBufferWriter<byte>/ReadOnlySpan<byte> pair, exposing no serializer-specific type so it stays format-neutral.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:538-544` · high · sha:da6000c93fc5</sub>
- The ISerde seam carries two allocation-profile primitives (Stream-destination serialize and IBufferWriter<byte> serialize), and core derives the other two profiles (fresh string/byte array and fixed buffer) as extension methods so no adapter can implement them subtly wrong.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:546-561` · high · sha:da6000c93fc5</sub>
- The fixed-buffer profile expresses SERDE-4's offset by having the caller pass buffer.AsSpan(offset) so bytes before the offset are untouchable by construction, returns the count written, and throws ArgumentOutOfRangeException when the payload does not fit.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:563-566` · high · sha:da6000c93fc5</sub>
- The fixed-buffer overflow error is ArgumentOutOfRangeException, which is not an SdkException, chains nothing and is what the BCL throws for a too-short destination, chosen over IndexOutOfRangeException because that type is reserved for the runtime (CA2201).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:564-567` · high · sha:da6000c93fc5</sub>
- The CLR reifies generics, so typeof(T) inside DeserializeAsync<T> is the full closed type; DeserializeAsync<List<Pet>> is itself the explicit type witness (SEAM-21, SERDE-5), T is SERDE-6's full-generic type carrier, and every generic method is SERDE-7's reified helper.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:575-578` · high · sha:da6000c93fc5</sub>
- SEAM-22/SERDE-8's rejection of an unresolved type variable is vacuous for the generic API, and its only .NET analogue is an open generic Type (typeof(List<>)) reachable through a Type-taking overload, which the seam does not have today; if added, it rejects type.ContainsGenericParameters with an actionable message.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:579-583` · high · sha:da6000c93fc5</sub>
- The System.Text.Json adapter requires a TypeInfoResolver, resolves JsonTypeInfo<T> per call, and turns a missing registration into a SerializationException/DeserializationException naming the type and pointing at the context, satisfying SERDE-6's fail-loudly rule rather than silently decoding into a raw type.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:587-590` · high · sha:da6000c93fc5</sub>
- The System.Text.Json codec ships as the separate package Dexpace.Sdk.Serialization.SystemTextJson rather than in core, even though it is in the shared framework and would cost no dependency, because SEAM-2 is about coupling and SEAM-19 is about not defaulting policy.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:603-606` · high · sha:da6000c93fc5</sub>
- Core cannot configure the codec even in principle because its AOT-safe form needs the application's own source-generated context, so a core-embedded default would be either reflection-based (unsafe under NFR-8) or empty.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:606-608` · high · sha:da6000c93fc5</sub>
- Keeping the codec separate leaves room for a Dexpace.Sdk.Serialization.NewtonsoftJson adapter without a second code path in core.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:608-609` · high · sha:da6000c93fc5</sub>
- SERDE-25's concern (one part of an application reconfiguring a codec another part uses) is closed by the adapter calling MakeReadOnly() on its options, after which any mutation throws, so sharing one instance is safe.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:619-621` · high · sha:da6000c93fc5</sub>
- The .NET runtime reifies generics, so ISerde.DeserializeAsync<List<Dto>> carries List<Dto> to run time in typeof(T) and SERDE-8's unresolved-type-variable state is unreachable because an open generic can never be a run-time type argument.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:276-281` · high · sha:68af5c6bf0ea</sub>
- SystemTextJsonSerde resolves JsonTypeInfo<T> through the configured resolver on every call and, when the resolver has no metadata for T, throws the serde exception naming T and telling the caller to add it to a context.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:286-290` · high · sha:68af5c6bf0ea</sub>
- SERDE-6's fail-loudly requirement takes the form of a missing JsonTypeInfo on .NET, surfaced at the first call rather than deep inside a parse, instead of silently decoding into a raw type.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:288-291` · high · sha:68af5c6bf0ea</sub>
- ISerde bundles both directions with DefaultMediaType and RequestBody.FromValue<T> stamps it when no media type is given, so the SPI never defaults to a format-agnostic constant (SERDE-1, SERDE-2).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:293-295` · high · sha:68af5c6bf0ea</sub>
- SERDE-4's encode-into-buffer profile (return bytes written, honor a start offset, throw a range/overflow error) is met by the core-derived Serialize<T>(Span<byte>, T): the caller passes buffer.AsSpan(offset), the count is returned, and a payload that does not fit throws ArgumentOutOfRangeException.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:296-299` · high · sha:68af5c6bf0ea</sub>
- The seam's own primitive is the growable IBufferWriter<byte>, and the fixed-buffer profile is derived from it once in core so that no adapter implements or bends it (P14).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:299-301` · high · sha:68af5c6bf0ea</sub>
- The root null check is the SDK's job: ISerde keeps its honest T? return, and the convenience layer (ReadValueAsync<T>, the pager, the typed SSE adapter) rejects a wire null for a non-nullable target with a DeserializationException naming T, with an explicit nullable-returning overload for callers who want null.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:315-318` · high · sha:68af5c6bf0ea</sub>
- RespectNullableAnnotations is enabled in the default serializer options on net10.0.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:318-319` · high · sha:68af5c6bf0ea</sub>
- Tristate is a readonly struct Tristate<T> where T : notnull (SERDE-14 to SERDE-20, SERDE-30), and choosing a struct makes SERDE-17's default-must-be-Absent clause free.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:321-325` · high · sha:68af5c6bf0ea</sub>
- The illegal fourth Tristate state is excluded twice: Tristate<T>.Present(value) throws ArgumentNullException on null at run time, and the notnull constraint makes Tristate<string?> a compile-time nullability warning (an error only in the SDK's own build).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:327-329` · high · sha:68af5c6bf0ea</sub>
- Absent and Null remain assignable to any Tristate parameterization through the non-generic Tristate.Absent and Tristate.Null markers and implicit conversions, which is the usage covariance was for.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:330-332` · high · sha:68af5c6bf0ea</sub>
- SERDE-30's stable strings are an overridden ToString returning Absent, Null and Present(value), because a record struct's synthesized form would print its private fields.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:334-335` · high · sha:68af5c6bf0ea</sub>
- The type is named Tristate rather than Optional<T> (used by the PR #3 serde design and dexpace/dotnet-sdk#1) because Roslyn's Microsoft.CodeAnalysis.Optional<T> is a widely circulated two-state type (HasValue and Value) and the specification's own word is Tristate.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:337-341` · high · sha:68af5c6bf0ea</sub>
- Tristate is wired into System.Text.Json on a private copy of the caller's options by adding a converter factory for Tristate<> to the copy's Converters and wrapping the copy's resolver with JsonTypeInfoResolver.WithAddedModifier, whose modifier sets ShouldSerialize to not-Absent on every property of a Tristate<> type (SERDE-15, SERDE-19).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:343-349` · high · sha:68af5c6bf0ea</sub>
- SERDE-20's top-level and array-element degradation falls out of the design: with no enclosing property to omit, the converter writes null for Absent.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:349-350` · high · sha:68af5c6bf0ea</sub>
- The adopted Tristate converter factory dispatches through an interface the closed Tristate<T> implements, obtaining an uninitialized instance of the requested type and asking it for its converter, so new TristateConverter<T>() is emitted from statically typed generic code; this leaves one IL2067 that the factory suppresses with a written justification.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:354-357` · high · sha:68af5c6bf0ea</sub>
- The Tristate struct carries no System.Text.Json attribute: although STJ is in the shared framework and an attribute would cost no dependency (P2), it would couple a core model to one codec's wiring, which policy default P3 keeps out of core.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:359-362` · high · sha:68af5c6bf0ea</sub>
- The strict-coercion policy (SERDE-21, SERDE-22) is System.Text.Json's General default, not its Web default.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:364-365` · high · sha:68af5c6bf0ea</sub>
- The PR #3 serde design made Web the default (camelCase, case-insensitive); that is overturned to Web naming with NumberHandling = Strict forced back, though the as-built serde takes its options from the caller's context.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:368-370` · high · sha:68af5c6bf0ea</sub>
- The SERDE-26 fix is the copy constructor new JsonSerializerOptions(callerOptions) followed by the Tristate wiring and MakeReadOnly() on the copy, verified to work over a source-generated context's options.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:378-381` · high · sha:68af5c6bf0ea</sub>
- The specification's SERDE-26 fallback clause for codecs that cannot be copied is never needed because STJ options are always copyable.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:381-382` · high · sha:68af5c6bf0ea</sub>

## Reference
- SEAM-19 rationale is that an undefaulted content type prevents a class of silent mislabeling bugs at the request-body edge; conformance is that a JSON codec reports the JSON media type and the core uses it as the default request-body content type.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:24-24` · high · sha:0adae2d6a47f</sub>
- SEAM-21 rationale is defense against erasure-driven heap pollution; conformance is that decoding into a concrete type via the token yields a typed value and a parametric type on a no-codec deserializer fails loudly.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:25-25` · high · sha:0adae2d6a47f</sub>
- Serde is the SDK's format-agnostic serialization seam, a small SPI bundling an encoder, a decoder and a declared wire media type so every subsystem round-trips typed values through one injection point without naming a concrete codec.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:3-3` · high · sha:c6bc7789c3a9</sub>
- The core ships only serialization abstractions (Serde, Serializer, Deserializer, the generic type carrier, the three-state Tristate sum type for PATCH, and a stable exception hierarchy), while a concrete codec such as Jackson plugs in at the edge.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:3-3` · high · sha:c6bc7789c3a9</sub>
- The error-body buffer bound used by status-aware handlers is the shared 1 MiB error-body cap defined at section 11 / BODY-30.
  <sub>spec · `docs/product-spec/14-serialization-serde.md:54-54` · medium · sha:c6bc7789c3a9</sub>
- The ISerde extension methods in core are SerializeToUtf8Bytes<T> (fresh byte array), SerializeToString<T> (fresh string) and Serialize<T>(Span<byte> destination, T value) returning int (fixed buffer), all over the IBufferWriter<byte> Serialize primitive; the stream primitive is ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:554-561` · high · sha:da6000c93fc5</sub>
- JsonSerializer.SerializeAsync leaves the destination stream open (verified).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:569-571` · high · sha:da6000c93fc5</sub>
- The System.Text.Json adapter uses the in-box System.Text.Json with no PackageReference, so its security floor is the runtime's servicing.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:609-610` · high · sha:da6000c93fc5</sub>
- System.Text.Json renders DateTime/DateTimeOffset as ISO-8601 and parses the same form, so SERDE-24's round-trip holds by default, and SERDE-23's tolerant decode is its default because unmapped members are skipped.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:617-619` · high · sha:da6000c93fc5</sub>
- As built (d45e64b), the serde seam diverges from the design: the fresh-string, fresh-byte-array and fixed-buffer profiles are not offered, there is no SerdeException base and both failure types are sealed, and the adapter freezes the caller's JsonSerializerOptions instead of a private copy.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:626-628` · high · sha:da6000c93fc5</sub>
- The reified helper that SERDE-7 asks for is the ordinary C# signature ResponseBody.ReadValueAsync<T>(ISerde), so the type-token apparatus is free.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:280-282` · high · sha:68af5c6bf0ea</sub>
- System.Text.Json's DefaultJsonTypeInfoResolver constructor carries [RequiresUnreferencedCode] and [RequiresDynamicCode] (verified).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:283-285` · high · sha:68af5c6bf0ea</sub>
- SERDE-3 holds because JsonSerializer.Serialize into a caller's stream does not dispose it (verified).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:295-295` · high · sha:68af5c6bf0ea</sub>
- Serde adapters catch JsonException and NotSupportedException (plus InvalidOperationException on the synchronous writer path) and rethrow with the original as InnerException.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:303-305` · high · sha:68af5c6bf0ea</sub>
- IOException is outside the adapter catch filter and propagates unwrapped (SERDE-12), and C# has no checked exceptions (SERDE-11).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:305-306` · high · sha:68af5c6bf0ea</sub>
- With RespectNullableAnnotations on, STJ throws for {"Name":null} into a non-nullable member but still returns null for a bare top-level null into Dto (verified).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:313-315` · high · sha:68af5c6bf0ea</sub>
- SERDE-17 notes that a missing key is short-circuited by the codec before the decoder's null hook runs, so the field default must be Absent.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:323-324` · high · sha:68af5c6bf0ea</sub>
- For the struct, default(Tristate<T>) is Absent, so every property, field and positional-record parameter of type Tristate<T> that the JSON omits is Absent without a default initializer (verified: {"B":null,"C":"v"} gave A=Absent B=Null C=Present(v)).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:324-327` · high · sha:68af5c6bf0ea</sub>
- SERDE-18's helpers are FromNullable(T?) (Present or Null, never Absent), Match, GetValueOrDefault and the three Is* predicates.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:333-334` · high · sha:68af5c6bf0ea</sub>
- Verified over a source-generated context with no attribute on the struct, Absent, Null and Present serialize as omitted, null and "x".
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:348-349` · high · sha:68af5c6bf0ea</sub>
- With JsonSerializerDefaults.General, "5" to int, 1.5 to int, true to double, 5 to string, "true" to bool and 1 to bool all throw JsonException while 5 to double widens (verified).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:365-367` · high · sha:68af5c6bf0ea</sub>
- JsonSerializerDefaults.Web sets NumberHandling = AllowReadingFromString so "5" decodes to 5, which is the first coercion SERDE-21 forbids (verified).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:367-369` · high · sha:68af5c6bf0ea</sub>
- SERDE-23 (unknown members ignored) and SERDE-24 (ISO-8601 round-trip) are System.Text.Json defaults (verified).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:370-371` · high · sha:68af5c6bf0ea</sub>
- SERDE-25's fresh instance per factory call applies to a SystemTextJsonSerde.CreateDefaultOptions() factory that the adapter does not have yet.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:371-373` · high · sha:68af5c6bf0ea</sub>
- SERDE-29 holds because read-only JsonSerializerOptions are thread-safe and STJ's metadata cache is publication-safe.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:382-383` · high · sha:68af5c6bf0ea</sub>
- As built (d45e64b), serialization is partial: ISerde, SystemTextJsonSerde, FromValue, ReadValueAsync and GetErrorAsync are built.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:398-399` · high · sha:68af5c6bf0ea</sub>
- As built (d45e64b), serialization diverges from the spec in three places: caller options made read-only (SERDE-26), no common serde root (SERDE-10), and root null accepted (SERDE-13).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:399-400` · high · sha:68af5c6bf0ea</sub>
- As built (d45e64b), Tristate<T> and its wiring, the default-options factory and the typed-response wrapper are missing.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:400-400` · high · sha:68af5c6bf0ea</sub>

## Conflicts

## Superseded

