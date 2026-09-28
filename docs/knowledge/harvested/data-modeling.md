# data-modeling

## Rules
- All core domain-model types MUST present an immutable value/metadata surface after construction, safe to share across threads without external synchronization, with any change producing a new instance, with the single carve-out of a body wrapping live single-use stream state (HTTP-1).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:12-12` · high · sha:8014d2ec2c9d</sub>
- Model construction MUST go through an immutable-value plus Builder (or dedicated factory) pattern; there MUST be no public field-wise constructor or unchecked copy that bypasses validation (SEAM-29/HTTP-2).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:13-13` · high · sha:8014d2ec2c9d</sub>
- A shared generic Builder contract (build() producing the target type) MUST exist so generic composition helpers can accept any builder, and required-field validation MUST be uniform: a missing required field fails at build() with a message of the form "<name> is required" (SEAM-29, restated).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:49-49` · high · sha:0adae2d6a47f</sub>
- Each builder-based model (request, response, headers, query params, request options, request conditions, multipart body) MUST expose a newBuilder()-style derivation returning a builder pre-populated from the instance, and that pre-filled builder MUST NOT alias the original's internal collections — each value list is copied (HTTP-3).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:7-7` · high · sha:22d100d5bc94</sub>
- build() MUST validate required fields and fail with a field-named error when one is missing — a request requires its URL, a response requires request, protocol, and status — never silently substituting defaults except where explicitly specified (HTTP-4).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:8-8` · high · sha:22d100d5bc94</sub>
- Accessors returning collections of header/query names, values, or entries MUST NOT let a caller mutate the model through the returned value, and MUST NOT surface later mutations of a live builder, guaranteed by a build-time deep copy of every value list plus read-only-typed collection returns (HTTP-5).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:9-9` · high · sha:22d100d5bc94</sub>

## Constraints

## Conclusions

## Reference
- Value-based types with no builder (media type, status, the typed header name, ETag, HTTP range, method, protocol) are derived by re-constructing through their factories. (HTTP-3)
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:7-7` · high · sha:22d100d5bc94</sub>

## Conflicts

## Superseded
