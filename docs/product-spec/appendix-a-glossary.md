## Appendix A — Glossary

**Adapter unit (pay-for-what-you-use module).** A separately installable unit supplying one concrete capability by depending on the core plus at most one third-party library, keeping its public surface minimal. Consumers compose only the units they need.

**Aggregate coverage floor.** A minimum line-coverage percentage computed across all library units combined (not per-unit), excluding samples and test-support code, enforced by the default build.

**Auth challenge.** A parsed RFC 7235 WWW-Authenticate / Proxy-Authenticate directive (a scheme plus a parameter map) a server returns on a 401/407 to indicate how a client may authenticate.

**Backpressure.** Flow control in which a consumer's demand governs how fast a producer is polled; in this SDK, the blocking source read is the backpressure mechanism for SSE (SSE-39).

**BYO (bring-your-own) resource.** A dependency (native HTTP client, executor, connection pool) the caller constructs and hands to the SDK; the caller owns its lifecycle and the SDK never closes it, in contrast to an SDK-managed resource the SDK created and must release on close.

**Canonical completion future.** The single dependency-free async value type that carries exactly one success value or one failure and is the interop pivot every ecosystem adapter bridges to and from (JVM reference: CompletableFuture).

**Cold publisher / per-subscription capture.** A reusable async object that (re)issues its request and (re)captures logging context on each subscription rather than once at assembly time.

**Copy-on-write derive.** Producing a reconfigured configuration from an existing one by applying a mutator to a prefilled builder while leaving the receiver unchanged (the override map is copied up front; pure read seams are shared by reference).

**Cross-origin redirect marker.** An internal, transport-invisible sentinel the redirect step sets on a cross-origin re-issue so the auth step suppresses credential stamping onto a server-chosen foreign host; stripped before the wire and unforgeable.

**Cursor / continuation token.** An opaque string a server returns to identify the next page of a paginated result; the pagination cursor strategy folds it into the next request's query.

**Deep value equality.** Content-based equals/hashCode comparison that recurses into arrays element-by-element while falling back to ordinary equality for non-arrays, with equals and hashCode kept mutually consistent (including NaN-equals-NaN and +0.0-unequal-to-(-0.0) array semantics).

**Diagnostic-context (MDC) allow-list.** The set of thread-local logging-context keys folded into an SDK log event (default `{trace.id, span.id}`), preventing arbitrary application context from leaking into SDK-owned events.

**Dispatch (SSE).** The framing act, triggered by a blank line, of collapsing the fields accumulated since the previous boundary into one Server-Sent Event.

**Drain-to-cap bounded map.** A concurrent map whose caller/server-influenced keys are capped: after each insert it is drained in a loop back under a hard bound, converging even under concurrent insert bursts; eviction victim is arbitrary.

**Idempotent method.** An HTTP method whose repetition has the same effect as a single invocation; the SDK's idempotent set is `{GET, HEAD, OPTIONS, PUT, DELETE}`, used as the retry-safety gate for body-less requests.

**Live tail.** On the SSE / body-preview exceeds-cap path, the still-open delegate source retained after the prefix was captured, carrying the un-buffered remainder and readable exactly once.

**Origin tuple (RFC 6454).** The (scheme, host, effective-port) triple; two URLs share an origin iff all three match, with case-insensitive host and scheme-default port. Cross-origin is judged against the original seed request, not the previous hop.

**Ownership-aware lifecycle.** The close/dispose discipline where the SDK releases only resources it created and never a caller-supplied one; close is idempotent.

**Page.** One page of results wrapping the live transport response; its materialized items and derived metadata survive close, while the raw body/connection is valid only until close.

**PageInfo.** A pagination strategy's parse output: the items on this page plus the next-page request, where a null/absent next-request is the single end-of-stream signal.

**Pagination strategy.** A stateless, immutable parser that, given a response and the original request template, returns a PageInfo; three built-ins are cursor, page-number, and Link-header.

**Pooled-thread poisoning.** The failure mode where an interrupt aimed at a cancelled call reaches a worker after it has returned to its pool and picked up unrelated work; prevented by an ordering handshake.

**Protocol error.** An error meaning a complete response was received but its status is 4xx/5xx; an unchecked/runtime error carrying the response.

**Provider / seam.** A narrow abstraction (SPI) the core depends on but never implements, behind which a concrete capability (I/O, transport, serde) plugs in.

**Quality gate.** An automated, build-blocking check that fails the standard build when its condition is not met (coverage floor, API-snapshot drift, warnings, lint/static-analysis, shrink-survival, runtime-floor).

**Redaction policy.** Centralized scrubbing of secrets from anything logged: URL userinfo always removed, query/fragment values removed unless allow-listed, header values gated by an allow-list, credential objects never revealing their secret.

**Replayable body.** A request body whose write can be invoked more than once producing identical bytes; non-replayable (single-use, stream-backed) bodies trip a consume-once guard on a second write.

**Retryability.** Whether a failure condition is transient — for a protocol error decided by the configured retryable-status set at the retry step; for a transport error, always transient.

**Retry-safety.** Whether it is safe to replay a specific request, decided at the retry step from HTTP-method idempotency (body-less) or body replayability (body-bearing); orthogonal to retryability.

**Serde.** A bundle exposing one serializer, one deserializer, and the declared wire media type for one format; the SDK's format-agnostic serialization seam.

**Shrink-survival keep-configuration.** Retain/keep rules the SDK ships so a downstream whole-program shrinker does not eliminate reflectively-reached or runtime-wired surface.

**Transport error.** A failure that produced no response (connect refused, DNS/TLS failure, read timeout, peer reset); belongs to the runtime's I/O-error family and is always-retryable at the error level.

**Tristate.** A three-valued sum type — Absent / Null / Present(value) — distinguishing a missing key from an explicit null from a present value at the serialization boundary, primarily for HTTP PATCH.

**TypeRef / type witness.** An explicit runtime carrier of a target type (a raw class token or a full generic capture) passed into deserialization so a language with type erasure recovers the intended type.

**W3C Trace Context.** The interoperable trace-correlation format the instrumentation context complies with: trace id, span id, trace flags, and trace state, with reserved all-zero invalid sentinels.

---

