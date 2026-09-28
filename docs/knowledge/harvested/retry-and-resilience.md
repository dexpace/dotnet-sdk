# retry-and-resilience

## Rules
- The idempotent-method set and the retryable-status classifier MUST each be single-sourced so the retry allow-list, the inherent replay-safety gate, and the exception's baked retryable flag all derive from one definition (HTTP-9/RETRY-1/RETRY-6).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:17-17` · high · sha:8014d2ec2c9d</sub>
- Both retry stacks MUST compute backoff via one shared calculator using one shared set of constants, with neither carrying an independent formula (RETRY-13).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:18-18` · high · sha:8014d2ec2c9d</sub>
- A response-carrying exception must derive its own retryable flag from the single status classifier at construction time, not from a hardcoded per-subclass constant. (RETRY-2, RETRY-3, RETRY-4)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:10-10` · high · sha:9efbe276001e</sub>
- A transport-level failure that produced no complete response — connection refused, TLS/DNS failure, socket read timeout, or peer reset — must be classified retryable unconditionally at the condition level, with safety gated separately. (RETRY-2, RETRY-3, RETRY-4)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:10-10` · high · sha:9efbe276001e</sub>
- A request is re-sendable if and only if it has no body and its method is idempotent, or it has a body and that body is replayable, and both retry stacks must apply this identical rule. (RETRY-5, RETRY-6)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:11-11` · high · sha:9efbe276001e</sub>
- When a request is not re-sendable, the retry logic must perform exactly one attempt and must not retry, even when the failure condition is retryable and even when there is no body to physically re-send, such as a bare non-idempotent POST. (RETRY-7, RETRY-8)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:12-12` · high · sha:9efbe276001e</sub>
- The pacing-header parser must be total and never throw; malformed, negative, or out-of-range values must map to "no hint" (null) rather than a zero delay, so the caller falls back to backoff instead of hammering the server. (RETRY-15, RETRY-16, RETRY-17, RETRY-18, RETRY-19)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- A valid HTTP-date or epoch pacing value already in the past must yield a zero delay (retry immediately), distinct from an unparseable value which yields no hint. (RETRY-15, RETRY-16, RETRY-17, RETRY-18, RETRY-19)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- Numeric Retry-After parsing must be screened by a strict decimal grammar before any float parse, rejecting type-suffixed, hex-float, NaN, and Infinity forms. (RETRY-15, RETRY-16, RETRY-17, RETRY-18, RETRY-19)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- A present pacing hint must override, not augment, the exponential retry schedule for that single decision; a literal Retry-After hint must not receive additional symmetric jitter, and where a total-timeout deadline applies the hint must still be clamped against it. (RETRY-20, RETRY-21, RETRY-22)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:18-18` · high · sha:9efbe276001e</sub>
- A failure while parsing a pacing header must not mask the real upstream failure; the retry loop falls back to exponential backoff and the original throwable remains the surfaced error. (RETRY-20, RETRY-21, RETRY-22)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:18-18` · high · sha:9efbe276001e</sub>
- Thread interruption or cancellation must never be treated as a retryable failure; on interrupt during a blocking backoff wait the implementation must restore the cancellation flag, cancel any externally-scheduled wake, abort the retry loop, and surface an interrupted-I/O error, and a downstream interrupt surfaced as an interrupted-I/O error is treated as terminal cancellation, not retried. (RETRY-23, RETRY-24, RETRY-25)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:22-22` · high · sha:9efbe276001e</sub>
- A read-timeout represented as a subtype of the interrupted-I/O error must not be mistaken for cancellation; it remains a retryable condition. (RETRY-23, RETRY-24, RETRY-25)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:22-22` · high · sha:9efbe276001e</sub>
- Non-recoverable runtime errors, such as out-of-memory or stack overflow, must not be retried, classified retryable, or logged, and must be surfaced unchanged with no suppressed-trail attachment. (RETRY-23, RETRY-24, RETRY-25)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:22-22` · high · sha:9efbe276001e</sub>
- The recovery retry stack must enforce an optional total-timeout budget with per-attempt deadline shrinking, aborting before each attempt if the attempt cap is reached, if elapsed time is at least the budget, or if elapsed time plus the next delay would exceed the budget, clamping the delay so it cannot overshoot, with a zero budget disabling the deadline. (RETRY-27, RETRY-28)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:27-27` · high · sha:9efbe276001e</sub>
- In the recovery stack, for a failure carrying a received response, the configured retryable-status set is authoritative and can both widen and narrow relative to the built-in classifier, while a no-response transport failure falls back to its always-retryable flag; the configured set is authoritative-contains rather than an intersection with the baked-in flag. (RETRY-37, RETRY-36, RETRY-35, RECOV-16)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:29-29` · high · sha:9efbe276001e</sub>
- A re-sent response whose error status is in the configured retryable-status set must be re-mapped into a typed failure with its body buffered, so the retry loop keeps evaluating the budget across a sequence such as 503, 503, 200, reaching the 200; all other re-sent responses pass through as Success. (RETRY-37, RETRY-36, RETRY-35, RECOV-16)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:29-29` · high · sha:9efbe276001e</sub>
- A retryable response's body/connection must be released before the backoff wait so a socket is not pinned across the delay; the pacing delay is computed from the still-open response first, and if the retry decision or delay computation throws, the response must still be closed before propagating. (RETRY-35, RETRY-34)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:30-30` · high · sha:9efbe276001e</sub>
- On terminal retry failure, every prior failed attempt's exception must be attached to the surfaced exception as suppressed, skipping the surfaced instance itself so a reused exception instance cannot trip a self-suppression error, and on eventual success the prior trail must be discarded. (RETRY-35, RETRY-34)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:30-30` · high · sha:9efbe276001e</sub>
- The asynchronous retry loop must be driven by an iterative trampoline — N retries must not build an N-deep chain of future continuations or stack frames — where a completion warranting another attempt hands control to a single active pump via a re-arm flag rather than recursing. (RETRY-30, RETRY-31, RETRY-32, RETRY-33)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- Async backoff delays must be scheduled non-blockingly, with a zero-length delay completing inline and re-arming the active pump. (RETRY-30, RETRY-31, RETRY-32, RETRY-33)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- If the caller has already completed or cancelled the returned async retry result, the driver must launch no further attempts, and any response arriving from an in-flight attempt must be closed rather than leaked. (RETRY-30, RETRY-31, RETRY-32, RETRY-33)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- Every terminal path of the async retry loop must complete the returned future — a throwing predicate, delay computation, log call, or synchronous scheduler rejection each completing it exceptionally — and must close any open retryable response first. (RETRY-30, RETRY-31, RETRY-32, RETRY-33)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- A throwing user delay-override should be non-fatal, logging and falling back, while a throwing should-retry predicate should abort the call as a well-typed error, with fatal errors rethrown unchanged in both cases. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- The stage stack must resolve the effective retry count as present-override-wins (validated non-negative), else the configured value, with a negative configured value clamped to the default and zero meaning no retries. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- Each retry attempt must re-execute the downstream chain with fresh per-attempt continuation state rather than reusing the prior attempt's in-flight chain, and upstream steps must not mutate the shared in-flight request between attempts. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- A shared retryability classifier should treat HTTP 408, 429, and all 5xx statuses except 501 and 505 as retryable, and should treat a throwable as retryable if it or any cause in its chain is an IO/timeout error via cycle-safe cause-chain traversal, with this exact status set being a hard contract wherever implemented. (CFG-35)
  <sub>spec · `docs/product-spec/16-configuration.md:58-58` · high · sha:367e27ec6481</sub>
- Inter-attempt retry waits MUST be promptly cancellable, aborting near-immediately on cancellation, surfacing the cancellation signal rather than a spurious timeout, and cancelling any timer/future the wait armed. (XCUT-3)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:11` · high · sha:d6123be82c9e</sub>
- For a protocol error, retry eligibility MUST be decided by a configurable retryable-status set that is authoritative over the baked retryability flag, and the same set MUST also govern whether a freshly re-sent error-status response is re-classified as a failure for the next attempt. (XCUT-7)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:18` · high · sha:d6123be82c9e</sub>
- Retry-safety MUST be decided at the retry step independently of retryability and applied uniformly to both protocol and transport failures — a body-less request is retry-safe only if its method is idempotent, so a bare POST MUST NOT be retried even on a transport error that never reached the server, and a request with a body is retry-safe only if that body is replayable. (XCUT-10)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:24` · high · sha:d6123be82c9e</sub>

## Constraints
- Automatic retry requires both a retryable condition and a re-sendable request to hold, since the two eligibility axes are orthogonal.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:7-7` · high · sha:9efbe276001e</sub>
- The retryable-throwable set is defined in exactly one place: any throwable that is, or has anywhere in its cause chain, an I/O error or a timeout error, determined via an iterative, identity-tracking cause-chain walk that terminates on a cyclic chain. (RETRY-2, RETRY-3, RETRY-4)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:10-10` · high · sha:9efbe276001e</sub>
- The single-sourced idempotent-method set equals exactly {GET, HEAD, OPTIONS, PUT, DELETE}; POST and PATCH are re-sendable only via the replayable-body path. (RETRY-5, RETRY-6)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:11-11` · high · sha:9efbe276001e</sub>
- Retry eligibility requires both a retryable condition and a re-sendable request; neither condition implies the other. (RETRY-7, RETRY-8)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:12-12` · high · sha:9efbe276001e</sub>
- Retry delay computation must be overflow-safe, saturating to the cap rather than throwing, and must reject an attempt number less than 1. (RETRY-9, RETRY-10, RETRY-11, RETRY-12)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- Any computed pacing delta must be clamped to a finite ceiling of 365 days before nanosecond conversion. (RETRY-15, RETRY-16, RETRY-17, RETRY-18, RETRY-19)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- The inter-attempt retry wait must be cancellable/interruptible and must not pin an execution carrier for its duration; a naive uninterruptible sleep that cannot be cancelled is non-conforming. (RETRY-26)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:23-23` · high · sha:9efbe276001e</sub>
- The stage-based retry stack must not impose a total-timeout budget, and a port that unifies the two retry stacks must make the total-timeout an explicitly opt-in feature rather than always-on. (RETRY-27, RETRY-28)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:27-27` · high · sha:9efbe276001e</sub>
- Both retry stacks compute backoff via one shared calculator and shared constants, and their attempt budgets denote the same number of total wire sends under equivalent defaults: the recovery stack's max-attempts (default 3) equals the stage stack's max-retries (default 2) plus one initial send. (RETRY-13, RETRY-14)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:28-28` · high · sha:9efbe276001e</sub>
- All retry policy components must be immutable and stateless after construction and safe for concurrent invocation, with every piece of per-call state kept on the per-call stack/driver, never on the shared instance. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- The retry engine must not shut down or close a caller-supplied scheduler, and a process-wide default scheduler, when used, is likewise never shut down by the SDK. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>

## Conclusions
- Statuses 501 and 505 are excluded from the retryable classifier because they indicate the server cannot fulfill the request regardless of retry. (RETRY-1, RETRY-37)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:9-9` · high · sha:9efbe276001e</sub>

## Reference
- The idempotent-method set is {GET, HEAD, OPTIONS, PUT, DELETE}. (HTTP-9, RETRY-1, RETRY-6)
  <sub>spec · `docs/product-spec/02-architectural-principles.md:17-17` · high · sha:8014d2ec2c9d</sub>
- The retryable-status classifier covers 408, 429, and all 5xx status codes except 501 and 505. (HTTP-9, RETRY-1, RETRY-6)
  <sub>spec · `docs/product-spec/02-architectural-principles.md:17-17` · high · sha:8014d2ec2c9d</sub>
- The SDK ships two cooperating retry stacks — the recovery-chain retry, which enforces a total-timeout budget, and the stage-based retry step — built on one status classifier, one backoff calculator, one pacing-header parser, and one set of tuning constants so behavior cannot drift between them.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:3-3` · high · sha:9efbe276001e</sub>
- The single-sourced retryable-status classifier treats exactly HTTP 408, 429, and all of 500-599 except 501 and 505 as retryable, and both the response-carrying exception's retryable flag and the stage stack's default predicate derive from this classifier, while the recovery stack layers its own configurable status allow-list on top. (RETRY-1, RETRY-37)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:9-9` · high · sha:9efbe276001e</sub>
- The unjittered exponential retry delay is computed as initialDelay times multiplier to the power of (attempt minus 1), with attempt 1-indexed (attempt 1 is the wait before the first retry), clamped to a maximum delay cap. (RETRY-9, RETRY-10, RETRY-11, RETRY-12)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- Symmetric retry jitter draws the effective delay uniformly from [d×(1−j/2), d×(1+j/2)] with midpoint d, where j=0 returns d, j is constrained to [0,1], a degenerate sub-nanosecond range returns the base delay, and a negative sample is floored to zero. (RETRY-9, RETRY-10, RETRY-11, RETRY-12)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- The default retry tuning values are an initial delay of 200 ms, a multiplier of 2.0, a max delay of 8 s, a jitter of 0.2, and a budget of 3 sends. (RETRY-9, RETRY-10, RETRY-11, RETRY-12)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- The pacing-header parser recognizes Retry-After as delta-seconds (integer and fractional), Retry-After as an RFC 1123 HTTP-date tolerant of an informational weekday and single-digit day, retry-after-ms and x-ms-retry-after-ms as integer milliseconds, and X-RateLimit-Reset as Unix epoch seconds whose delta is positively jittered to [100%,120%]. (RETRY-15, RETRY-16, RETRY-17, RETRY-18, RETRY-19)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- Pacing header resolution returns the first parseable value under a defined precedence — the recovery stack scans the whole header map in fixed precedence order (Retry-After numeric then date, then retry-after-ms, then x-ms-retry-after-ms, then X-RateLimit-Reset), while the stage stack walks a caller-configurable ordered header list. (RETRY-20, RETRY-21, RETRY-22)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:18-18` · high · sha:9efbe276001e</sub>
- In the reference implementation, the recovery stack schedules the retry wake on a shared scheduler and blocks on the resulting future, the stage-sync stack performs an interruptible sleep that unmounts a virtual-thread carrier, and async implementations schedule the delay without blocking a thread. (RETRY-26)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:23-23` · high · sha:9efbe276001e</sub>
- Only the async retry stack currently implements the skip-self suppression guard in the reference implementation; a port must apply it to both stacks. (RETRY-35, RETRY-34)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:30-30` · high · sha:9efbe276001e</sub>
- The stage stack's retry delay resolution follows the precedence caller delay-override, then server pacing headers (response path only), then fixed delay, then exponential backoff, with the exception path skipping the header step. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- A fixed-delay retry configuration may force a flat delay that disables backoff and jitter entirely, making the backoff path unreachable. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- An opt-in server-driven override may let a response header force or suppress the retry classification, flipping only classification while remaining subject to the attempt cap and the re-send-safety gate. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- An optional per-attempt request header may stamp the 1-based attempt ordinal onto a fresh per-attempt copy of the request, never mutating the captured template, preserving any idempotency key, and allocating nothing when disabled. (RETRY-39, RETRY-40, RETRY-41, RETRY-42, RETRY-43, RETRY-44, RETRY-45, RETRY-29, RETRY-38)
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- The default retryable-status set for protocol-error retry eligibility is {408, 429, 500, 502, 503, 504}. (XCUT-7)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:18` · high · sha:d6123be82c9e</sub>
- The SDK's idempotent method set, used as the retry-safety gate for body-less requests, is {GET, HEAD, OPTIONS, PUT, DELETE}.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:31` · high · sha:f0b3d2058626</sub>
- A replayable body is a request body whose write can be invoked more than once producing identical bytes, whereas a non-replayable (single-use, stream-backed) body trips a consume-once guard on a second write.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:55` · high · sha:f0b3d2058626</sub>
- Retryability is whether a failure condition is transient — for a protocol error decided by the configured retryable-status set at the retry step, and for a transport error always transient.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:57` · high · sha:f0b3d2058626</sub>
- Retry-safety is whether it is safe to replay a specific request, decided at the retry step from HTTP-method idempotency for body-less requests or body replayability for body-bearing requests, and is orthogonal to retryability.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:59` · high · sha:f0b3d2058626</sub>

## Conflicts

## Superseded
