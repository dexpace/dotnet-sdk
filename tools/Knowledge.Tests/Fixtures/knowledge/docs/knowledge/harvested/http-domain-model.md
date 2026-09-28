# http-domain-model

## Rules
- A request MUST carry an immutable method token, uppercased at construction (HTTP-1).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:9` · high · sha:1111aaaa2222</sub>
- Header names MUST compare case-insensitively while preserving the casing the caller supplied (HTTP-70).
  <sub>spec · `docs/product-spec/04-core-http-domain-model.md:14` · high · sha:1111aaaa2222</sub>

## Reference
- The HTTP conformance suite verifies the status accessor (HTTP-2) and the header map, encoded as UTF-8 and digested with SHA-256 per RFC-3986.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:3333cccc4444</sub>
