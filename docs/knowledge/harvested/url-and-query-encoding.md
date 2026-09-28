# url-and-query-encoding

## Rules
- The operation-input projection MUST let generated code declare, per operation, an HTTP method, a path template with named placeholders, and typed projections of inputs onto path/query/header/body, with only method and path template required and the four projections defaulting to empty, and the body carried but not encoded by this seam (SEAM-26).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:29-29` · high · sha:0adae2d6a47f</sub>
- When assembled against a base URL, path-parameter values MUST be percent-encoded as single path segments so a value cannot inject an extra slash (SEAM-27).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:30-30` · high · sha:0adae2d6a47f</sub>
- Every path-template placeholder MUST have a supplied value, and the query MUST be RFC-3986 rendered (SEAM-27).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:30-30` · high · sha:0adae2d6a47f</sub>
- Base-URL composition follows fixed rules: a trailing slash normalizes to one separator, an empty operation path leaves the base untouched, an existing base query is preserved with the operation query appended after it, and a base carrying a fragment or resolving to a malformed URL is rejected with a context-bearing error (SEAM-27).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:30-30` · high · sha:0adae2d6a47f</sub>

## Constraints

## Conclusions

## Reference

## Conflicts

## Superseded
