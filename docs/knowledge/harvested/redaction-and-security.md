# redaction-and-security

## Rules
- Credentials are transport-scoped and never stamped over plaintext (AUTH-28).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- Redirects strip credentials and never launder them cross-origin (REDIR-7, REDIR-9, REDIR-8).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- Header names and values are validated against request-splitting before any transport sees them (HTTP-17–HTTP-19).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- The Digest authentication client's nonces come from a cryptographically strong random source (AUTH-20).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- Log-preview and error-body buffers are bounded in size (HTTP-52, BODY-30).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- URL userinfo (user:password@) is always redacted to a fixed placeholder (***:***@), unconditionally and independent of any allow-list. (OBS-11)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:24-24` · high · sha:1b678eca176d</sub>
- URL query-parameter values are redacted to *** unless the parameter name, decoded and compared case-insensitively, is allow-listed; the default query allow-list is exactly {api-version}, an empty allow-list redacts every value, multi-value keys are treated atomically, and parameter names and the "=" separator are preserved. (OBS-12)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:25-25` · high · sha:1b678eca176d</sub>
- A URL fragment is scrubbed under the same allow-list as query parameters, redacting key=value tokens like query values while preserving a plain fragment with no "=" verbatim, because OAuth implicit-flow access tokens ride in the fragment. (OBS-13)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:26-26` · high · sha:1b678eca176d</sub>
- URL redaction must not alter scheme, host, port, or path, must preserve a present-but-empty query (trailing "?"), must not treat a "?" inside the fragment as a query delimiter, and may drop a trailing "&" empty final pair. (OBS-14)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:27-27` · high · sha:1b678eca176d</sub>
- URL redaction is total, returning a fixed sentinel ([malformed url]) rather than throwing on any parse or rebuild failure. (OBS-15)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:28-28` · high · sha:1b678eca176d</sub>
- A URL arriving as a header value is redacted: a parseable absolute value is redacted like a request URL, a relative or unparseable value keeps the path and drops everything after it while appending a fixed ?*** marker whenever the value carried a query or fragment, and a value with neither is returned verbatim. (OBS-16)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:29-29` · high · sha:1b678eca176d</sub>
- When logging header values, values of URL-valued response headers (at minimum Location and Content-Location) are redacted through the URL-value redactor while other header values pass through unchanged, and this redaction policy is shared by the sync and async logging paths so it cannot drift. (OBS-17)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:30-30` · high · sha:1b678eca176d</sub>
- Header logging gates which header names are logged by an allow-list, so a non-allow-listed header's value is never logged (either emitted with a fixed REDACTED marker or omitted entirely per a boolean policy), and the default allow-list contains only diagnostic, non-credential headers. (OBS-18)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:31-31` · high · sha:1b678eca176d</sub>
- A transport that drops a caller-set request header it cannot encode should surface the drop via a configurable verbosity policy offering at least warn-every-occurrence, warn-first-per-header-name-then-verbose, or verbose-only, defaulting to once-per-header-name. (OBS-19)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:32-32` · high · sha:1b678eca176d</sub>
- A credential MUST NOT be stamped over a non-secure (non-HTTPS) transport; the auth layer MUST reject before any token fetch or header write when about to attach a credential and the scheme is not https, though a deliberately credential-free re-issue such as a marker-suppressed cross-origin redirect MAY proceed over any scheme. (XCUT-16)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:44` · high · sha:d6123be82c9e</sub>
- URL userinfo MUST always be redacted from logging/telemetry output and can never be allow-listed. (XCUT-19)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47` · high · sha:d6123be82c9e</sub>
- URL query-parameter values and key=value fragment tokens MUST be redacted from logging/telemetry unless the parameter name (case-insensitive) is allow-listed. (XCUT-19)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47` · high · sha:d6123be82c9e</sub>
- Header logging MUST be default-deny, emitting only an explicit allow-list of non-credential headers verbatim. (XCUT-19)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47` · high · sha:d6123be82c9e</sub>
- Credential objects MUST NOT reveal their secret in string/serialized form. (XCUT-19)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47` · high · sha:d6123be82c9e</sub>
- Full request/response body logging MUST be OFF by default. (XCUT-19)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47` · high · sha:d6123be82c9e</sub>
- Any security-relevant random value, such as auth client nonces/cnonce and similar unpredictability-dependent tokens, MUST be drawn from a cryptographically-strong PRNG with sufficient entropy, never a non-cryptographic RNG. (XCUT-21)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:49` · high · sha:d6123be82c9e</sub>

## Constraints

## Conclusions

## Reference
- The reference implementation uses at least 128 bits of entropy for the Digest authentication cnonce. (XCUT-21)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:49` · high · sha:d6123be82c9e</sub>
- The redaction policy is centralized scrubbing of secrets from anything logged — URL userinfo always removed, query/fragment values removed unless allow-listed, header values gated by an allow-list, and credential objects never revealing their secret.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:53` · high · sha:f0b3d2058626</sub>

## Conflicts

## Superseded
