# cross-cutting-invariants

## Rules
- A decision that must stay consistent across the SDK is computed from one definition and never duplicated.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:15-15` · high · sha:8014d2ec2c9d</sub>
- OBS-20 (MUST) Emitting log events around a request MUST NOT fail the request: every log-emission site (request/response/failure events and the body-drain feeding them) MUST catch any exception and re-surface it as a best-effort `http.instrumentation.*` diagnostic, and a secondary failure while emitting that diagnostic MUST be swallowed.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:36-36` · high · sha:1b678eca176d</sub>
- XCUT-1: cancellation is terminal and non-retryable with the interrupt flag preserved.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:76-76` · high · sha:0451cc7f3bb4</sub>
- XCUT-2: a timeout is retryable with a clear flag, discriminated out-of-band, checking the subtype first.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:76-76` · high · sha:0451cc7f3bb4</sub>
- XCUT-3: the inter-attempt wait is promptly cancellable.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:76-76` · high · sha:0451cc7f3bb4</sub>
- XCUT-4: the error taxonomy has two branches and a transport error is always retryable and in the I/O family.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:77-77` · high · sha:0451cc7f3bb4</sub>
- XCUT-5: a baked protocol-error flag comes from one classifier covering 408, 429 and all 5xx except 501 and 505.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:77-77` · high · sha:0451cc7f3bb4</sub>
- XCUT-6: non-protocol errors are classified by capability.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:77-77` · high · sha:0451cc7f3bb4</sub>
- XCUT-7: a configurable authoritative retryable-status set drives protocol-error retries.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:77-77` · high · sha:0451cc7f3bb4</sub>
- XCUT-8: the status factory rejects a non-error status.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:77-77` · high · sha:0451cc7f3bb4</sub>
- XCUT-9: cause-chain walks are cycle-safe.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:77-77` · high · sha:0451cc7f3bb4</sub>
- XCUT-10: the retry-safety gate applies uniformly including transport errors, so a bare POST is not retried and a non-replayable body is not re-sent.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:78-78` · high · sha:0451cc7f3bb4</sub>
- XCUT-11: shared components are concurrent-safe with per-call state kept local.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:79-79` · high · sha:0451cc7f3bb4</sub>
- XCUT-12: credential reads are wait-free with a scoped single-flight refresh.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:79-79` · high · sha:0451cc7f3bb4</sub>
- XCUT-13: close is idempotent and non-blocking.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:79-79` · high · sha:0451cc7f3bb4</sub>
- XCUT-22: the SDK closes only what it created.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:79-79` · high · sha:0451cc7f3bb4</sub>
- XCUT-23: a pluggable seam resolves explicit choice first, then auto-discovery, and fails loudly on zero or ambiguous candidates.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:79-79` · high · sha:0451cc7f3bb4</sub>
- XCUT-14: caller-keyed and server-keyed maps are bounded with a drain-to-cap loop.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:80-80` · high · sha:0451cc7f3bb4</sub>
- XCUT-15: public wire models are immutable with no externally mutable alias.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:80-80` · high · sha:0451cc7f3bb4</sub>
- XCUT-16: no credential is sent over non-HTTPS.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:81-81` · high · sha:0451cc7f3bb4</sub>
- XCUT-17: redirects keep credential hygiene: Authorization is always stripped, a cross-origin redirect strips Cookie and Proxy-Authorization judged against the seed, userinfo is dropped and a downgrade is denied by default.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:81-81` · high · sha:0451cc7f3bb4</sub>
- XCUT-18: header names and values are validated against splitting at the model layer.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:81-81` · high · sha:0451cc7f3bb4</sub>
- XCUT-19: logging redaction is default-deny for userinfo, query, fragment, headers, credentials and bodies.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:81-81` · high · sha:0451cc7f3bb4</sub>
- XCUT-20: observability never throws into the request path.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:81-81` · high · sha:0451cc7f3bb4</sub>
- XCUT-21: a CSPRNG is used for security-relevant randomness.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:81-81` · high · sha:0451cc7f3bb4</sub>
- XCUT-24: diagnostic previews are byte-capped and non-consuming.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:81-81` · high · sha:0451cc7f3bb4</sub>
- Bound every loop, queue, retry, pool, cache and fan-out.
  <sub>styleguide · `docs/styleguide/csharp/README.md:67-67` · high · sha:1e6ba36fc337</sub>
- Recursion is forbidden in library code so that all execution is provably bounded.
  <sub>styleguide · `docs/styleguide/csharp/README.md:67-67` · high · sha:1e6ba36fc337</sub>

## Constraints
- The runtime does NOT defensively wrap tracer (span start, scope activation, end) or metrics (counter/histogram) calls, so a throwing tracer or meter WILL propagate and can fail the request.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:36-36` · high · sha:1b678eca176d</sub>

## Conclusions

## Reference
- The cross-cutting invariants are universal, subsystem-independent contracts spanning transports, I/O, pipeline, auth and instrumentation, and a port that violates any of them is incorrect or unsafe even if each subsystem individually works.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:3-3` · high · sha:d6123be82c9e</sub>

## Conflicts

## Superseded

