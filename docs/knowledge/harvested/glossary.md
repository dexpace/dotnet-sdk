# glossary

## Rules
- Cross-origin is judged against the original seed request, not the previous hop.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:35-35` · high · sha:f0b3d2058626</sub>

## Constraints

## Conclusions

## Reference
- A BYO (bring-your-own) resource is a dependency (native HTTP client, executor, connection pool) the caller constructs and hands to the SDK, whose lifecycle the caller owns and the SDK never closes, in contrast to an SDK-managed resource the SDK created and must release on close.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:11-11` · high · sha:f0b3d2058626</sub>
- The canonical completion future is the single dependency-free async value type carrying exactly one success value or one failure, serving as the interop pivot every ecosystem adapter bridges to and from (JVM reference: CompletableFuture).
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:13-13` · high · sha:f0b3d2058626</sub>
- A cold publisher / per-subscription capture is a reusable async object that (re)issues its request and (re)captures logging context on each subscription rather than once at assembly time.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:15-15` · high · sha:f0b3d2058626</sub>
- Copy-on-write derive produces a reconfigured configuration from an existing one by applying a mutator to a prefilled builder while leaving the receiver unchanged, copying the override map up front and sharing pure read seams by reference.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:17-17` · high · sha:f0b3d2058626</sub>
- The cross-origin redirect marker is an internal, transport-invisible sentinel the redirect step sets on a cross-origin re-issue so the auth step suppresses credential stamping onto a server-chosen foreign host, and it is stripped before the wire and unforgeable.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:19-19` · high · sha:f0b3d2058626</sub>
- A cursor / continuation token is an opaque string a server returns to identify the next page of a paginated result, which the pagination cursor strategy folds into the next request's query.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:21-21` · high · sha:f0b3d2058626</sub>
- Deep value equality is content-based equals/hashCode comparison that recurses into arrays element-by-element and falls back to ordinary equality for non-arrays, with equals and hashCode mutually consistent (including NaN-equals-NaN and +0.0-unequal-to-(-0.0) array semantics).
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:23-23` · high · sha:f0b3d2058626</sub>
- The diagnostic-context (MDC) allow-list is the set of thread-local logging-context keys folded into an SDK log event (default {trace.id, span.id}), preventing arbitrary application context from leaking into SDK-owned events.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:25-25` · high · sha:f0b3d2058626</sub>
- Dispatch (SSE) is the framing act, triggered by a blank line, of collapsing the fields accumulated since the previous boundary into one Server-Sent Event.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:27-27` · high · sha:f0b3d2058626</sub>
- A drain-to-cap bounded map is a concurrent map whose caller/server-influenced keys are capped, drained in a loop after each insert back under a hard bound so it converges even under concurrent insert bursts, with an arbitrary eviction victim.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:29-29` · high · sha:f0b3d2058626</sub>
- An adapter unit (pay-for-what-you-use module) is a separately installable unit supplying one concrete capability by depending on the core plus at most one third-party library, keeping its public surface minimal, so consumers compose only the units they need.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:3-3` · high · sha:f0b3d2058626</sub>
- An idempotent method is an HTTP method whose repetition has the same effect as a single invocation, and the SDK's idempotent set is {GET, HEAD, OPTIONS, PUT, DELETE}, used as the retry-safety gate for body-less requests.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:31-31` · high · sha:f0b3d2058626</sub>
- The live tail is, on the SSE / body-preview exceeds-cap path, the still-open delegate source retained after the prefix was captured, carrying the un-buffered remainder and readable exactly once.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:33-33` · high · sha:f0b3d2058626</sub>
- An origin tuple (RFC 6454) is the (scheme, host, effective-port) triple, where two URLs share an origin iff all three match, with case-insensitive host and scheme-default port.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:35-35` · high · sha:f0b3d2058626</sub>
- Ownership-aware lifecycle is the close/dispose discipline where the SDK releases only resources it created and never a caller-supplied one, and close is idempotent.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:37-37` · high · sha:f0b3d2058626</sub>
- A Page is one page of results wrapping the live transport response, whose materialized items and derived metadata survive close while the raw body/connection is valid only until close.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:39-39` · high · sha:f0b3d2058626</sub>
- PageInfo is a pagination strategy's parse output, holding the items on this page plus the next-page request, where a null/absent next-request is the single end-of-stream signal.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:41-41` · high · sha:f0b3d2058626</sub>
- A pagination strategy is a stateless, immutable parser that, given a response and the original request template, returns a PageInfo, and the three built-ins are cursor, page-number, and Link-header.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:43-43` · high · sha:f0b3d2058626</sub>
- Pooled-thread poisoning is the failure mode where an interrupt aimed at a cancelled call reaches a worker after it has returned to its pool and picked up unrelated work, prevented by an ordering handshake.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:45-45` · high · sha:f0b3d2058626</sub>
- A protocol error means a complete response was received but its status is 4xx/5xx, and it is an unchecked/runtime error carrying the response.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:47-47` · high · sha:f0b3d2058626</sub>
- A provider / seam is a narrow abstraction (SPI) the core depends on but never implements, behind which a concrete capability (I/O, transport, serde) plugs in.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:49-49` · high · sha:f0b3d2058626</sub>
- The aggregate coverage floor is a minimum line-coverage percentage computed across all library units combined (not per-unit), excluding samples and test-support code, enforced by the default build.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:5-5` · high · sha:f0b3d2058626</sub>
- A quality gate is an automated, build-blocking check that fails the standard build when its condition is not met (coverage floor, API-snapshot drift, warnings, lint/static-analysis, shrink-survival, runtime-floor).
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:51-51` · high · sha:f0b3d2058626</sub>
- The redaction policy is centralized scrubbing of secrets from anything logged, where URL userinfo is always removed, query/fragment values are removed unless allow-listed, header values are gated by an allow-list, and credential objects never reveal their secret.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:53-53` · high · sha:f0b3d2058626</sub>
- A replayable body is a request body whose write can be invoked more than once producing identical bytes, while non-replayable (single-use, stream-backed) bodies trip a consume-once guard on a second write.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:55-55` · high · sha:f0b3d2058626</sub>
- Retryability is whether a failure condition is transient, decided for a protocol error by the configured retryable-status set at the retry step and for a transport error always transient.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:57-57` · high · sha:f0b3d2058626</sub>
- Retry-safety is whether it is safe to replay a specific request, decided at the retry step from HTTP-method idempotency (body-less) or body replayability (body-bearing), and is orthogonal to retryability.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:59-59` · high · sha:f0b3d2058626</sub>
- A Serde is a bundle exposing one serializer, one deserializer and the declared wire media type for one format, and is the SDK's format-agnostic serialization seam.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:61-61` · high · sha:f0b3d2058626</sub>
- Shrink-survival keep-configuration is the retain/keep rules the SDK ships so a downstream whole-program shrinker does not eliminate reflectively-reached or runtime-wired surface.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:63-63` · high · sha:f0b3d2058626</sub>
- A transport error is a failure that produced no response (connect refused, DNS/TLS failure, read timeout, peer reset), belongs to the runtime's I/O-error family, and is always-retryable at the error level.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:65-65` · high · sha:f0b3d2058626</sub>
- Tristate is a three-valued sum type (Absent / Null / Present(value)) distinguishing a missing key from an explicit null from a present value at the serialization boundary, primarily for HTTP PATCH.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:67-67` · high · sha:f0b3d2058626</sub>
- A TypeRef / type witness is an explicit runtime carrier of a target type (a raw class token or a full generic capture) passed into deserialization so a language with type erasure recovers the intended type.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:69-69` · high · sha:f0b3d2058626</sub>
- An auth challenge is a parsed RFC 7235 WWW-Authenticate / Proxy-Authenticate directive (a scheme plus a parameter map) a server returns on a 401/407 to indicate how a client may authenticate.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:7-7` · high · sha:f0b3d2058626</sub>
- W3C Trace Context is the interoperable trace-correlation format the instrumentation context complies with, comprising trace id, span id, trace flags and trace state, with reserved all-zero invalid sentinels.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:71-71` · high · sha:f0b3d2058626</sub>
- Backpressure is flow control in which a consumer's demand governs how fast a producer is polled, and in this SDK the blocking source read is the backpressure mechanism for SSE (SSE-39).
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:9-9` · high · sha:f0b3d2058626</sub>

## Conflicts

## Superseded

