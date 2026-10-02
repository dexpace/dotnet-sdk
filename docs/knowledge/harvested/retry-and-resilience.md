# retry-and-resilience

## Rules
- HTTP-9 / RETRY-1 / RETRY-6 (MUST) - The idempotent-method set and the retryable-status classifier MUST each be single-sourced so the retry allow-list, the inherent replay-safety gate and the exception's baked retryable flag all derive from one place.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:17-17` · high · sha:8014d2ec2c9d</sub>
- RETRY-13 (MUST) - Both retry stacks MUST compute backoff via one shared calculator using one shared set of constants, and neither may carry an independent formula.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:18-18` · high · sha:8014d2ec2c9d</sub>
- RECOV-1 (MUST): The response-side outcome MUST be a closed sum type with exactly two variants (a success carrying a response and a failure carrying a throwable), mutually exclusive and jointly exhaustive, with derivable accessors and a fold that applies exactly one of two branches at most once per call.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:45-45` · high · sha:33e9443472ce</sub>
- RECOV-2 (MUST): The unified orchestrator MUST catch EVERY throwable from any request-chain step and from the transport invocation, convert it into a Failure, and thread it through the response recovery chain; no throwable from the pre-request phase or the transport may bypass the recovery hooks.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:46-46` · high · sha:33e9443472ce</sub>
- RECOV-2 (MUST): A before-request throw MUST NOT skip after-error handling; this is the defining invariant of the recovery chain.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:46-46` · high · sha:33e9443472ce</sub>
- RECOV-3 (MUST): The request recovery chain MUST apply its ordered steps as a sequential left-to-right fold in which the output of step N is the input of step N+1, an empty chain returns the input unchanged, and a throwing step aborts the remainder and propagates (the orchestrator converts it per RECOV-2).
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:47-47` · high · sha:33e9443472ce</sub>
- RECOV-4 (MUST): Response steps (response to response) MUST run ONLY when the current outcome is a Success; on a Failure the entire response-step phase is skipped.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:48-48` · high · sha:33e9443472ce</sub>
- RECOV-5 (MUST): Recovery steps MUST be applied to EVERY outcome, successes and failures, sequentially and always, observing the terminal outcome including a failure a response step just produced by throwing.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:48-48` · high · sha:33e9443472ce</sub>
- RECOV-6 (MUST): The fold order MUST be all response steps first (on the success path), then all recovery steps, in declared order within each group.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:48-48` · high · sha:33e9443472ce</sub>
- RECOV-7 (MUST): If a response step throws, its throwable MUST be converted into a Failure fed to the subsequent recovery steps and never propagated out of the response chain, so error-mapping steps (status to typed exception) flow through recovery exactly like a transport error.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:49-49` · high · sha:33e9443472ce</sub>
- RECOV-8 (MUST): If a recovery step throws, its throwable MUST be wrapped into a Failure fed to the NEXT recovery step, never aborting the remaining recovery steps, and the chain's apply operation MUST NOT throw under any input.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:49-49` · high · sha:33e9443472ce</sub>
- RECOV-9 (SHOULD): Recovery steps SHOULD surface errors by returning a Failure rather than throwing.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:49-49` · high · sha:33e9443472ce</sub>
- RECOV-10 (MUST): The orchestrator's dispatch MUST unwrap the final outcome by returning the contained response on Success and rethrowing the contained throwable UNCHANGED (no wrapping, no substitution) on Failure.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:50-50` · high · sha:33e9443472ce</sub>
- RECOV-10 (MUST): Any typed-exception surfacing must be done by a recovery step constructing the error and returning a Failure.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:50-50` · high · sha:33e9443472ce</sub>
- RECOV-11 (MUST): When wrapping a cancellation or interruption throwable into a Failure, the wrapping helper MUST re-assert the cancellation signal on the current context before returning, so code later blocked on the outcome still observes the cancellation.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:50-50` · high · sha:33e9443472ce</sub>
- RECOV-12 (MUST): When a response or recovery step THROWS while holding a Success response, the pipeline MUST close/release that in-hand response before wrapping the throwable, attaching any close error as suppressed so it never masks the primary, releasing the response exactly once.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:51-51` · high · sha:33e9443472ce</sub>
- RECOV-13 (MUST): When a step handed a Success deliberately RETURNS a different outcome (a Success-to-Failure transform, or a substitute Success), the pipeline MUST NOT auto-close the discarded original, because the transforming step OWNS releasing the response it drops.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:51-51` · high · sha:33e9443472ce</sub>
- RECOV-14 (MUST): A recovery chain's step lists MUST behave as immutable after construction, and the response recovery chain MUST defensively copy BOTH its lists at construction.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:52-52` · high · sha:33e9443472ce</sub>
- RECOV-14 (MUST): Recovery-chain steps MUST be safe for concurrent invocation, with per-request state in the passed context or the value being transformed, never on the step.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:52-52` · high · sha:33e9443472ce</sub>
- RECOV-15 / RECOV-16 (MUST): The status-to-typed-exception mapping step MUST treat only status codes 400..599 as errors (mapping to the matching typed exception, which becomes a Failure per RECOV-7) and return all other statuses unchanged.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:53-53` · high · sha:33e9443472ce</sub>
- RECOV-15 / RECOV-16 (MUST): Before mapping an error-status response (both in the mapping step and on a re-sent error response) the error body MUST be buffered into a bounded (1 MiB), replayable in-memory copy so the connection is released promptly and the body remains readable on the Failure.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:53-53` · high · sha:33e9443472ce</sub>
- A port unifying retry entry points MUST make the total-timeout budget explicitly opt-in.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:59-59` · high · sha:33e9443472ce</sub>
- A port should preserve the single-sourcing, the two-axis eligibility model, interrupt-safe backoff, and the non-blocking async trampoline.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:3-3` · high · sha:9efbe276001e</sub>
- RETRY-1 (MUST): The retryable-status classifier MUST be single-sourced and treat exactly 408, 429, and all of 500-599 EXCEPT 501 and 505 as retryable.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:9-9` · high · sha:9efbe276001e</sub>
- RETRY-2 (MUST): The retryable-throwable set MUST be defined in exactly one place: any throwable that is, or has anywhere in its cause chain, an I/O error or a timeout error.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:10-10` · high · sha:9efbe276001e</sub>
- RETRY-2 (MUST): The cause-chain walk MUST be iterative and identity-tracking and MUST terminate on a cyclic chain.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:10-10` · high · sha:9efbe276001e</sub>
- RETRY-3 (MUST): A response-carrying exception MUST derive its own retryable flag from the single status classifier at construction, not from a hardcoded per-subclass constant.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:10-10` · high · sha:9efbe276001e</sub>
- RETRY-4 (MUST): A transport-level failure that produced no complete response (connection refused, TLS/DNS failure, socket read timeout, peer reset) MUST be classified retryable unconditionally at the condition level, with safety gated separately.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:10-10` · high · sha:9efbe276001e</sub>
- RETRY-5 (MUST): A request is re-sendable iff it has no body AND its method is idempotent, OR it has a body AND that body is replayable, and both retry stacks MUST apply this identical rule.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:11-11` · high · sha:9efbe276001e</sub>
- RETRY-6 (MUST): The idempotent-method set MUST be single-sourced and equal to {GET, HEAD, OPTIONS, PUT, DELETE}, and POST and PATCH are re-sendable only via the replayable-body path.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:11-11` · high · sha:9efbe276001e</sub>
- RETRY-7 (MUST): When a request is not re-sendable, the retry logic MUST perform exactly one attempt and MUST NOT retry, even when the condition is retryable and even when there is no body to physically re-send (a bare non-idempotent POST).
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:12-12` · high · sha:9efbe276001e</sub>
- RETRY-8 (MUST): Retry eligibility MUST require BOTH a retryable condition AND a re-sendable request; neither implies the other.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:12-12` · high · sha:9efbe276001e</sub>
- RETRY-9 (MUST): The unjittered exponential delay MUST be initialDelay x multiplier^(attempt-1), with attempt 1-indexed (attempt 1 is the wait before the first retry), clamped to a maximum delay cap.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- RETRY-10 (MUST): Symmetric jitter MUST draw the effective delay uniformly from [d*(1-j/2), d*(1+j/2)] with midpoint d, with j=0 returning d and j constrained to [0,1].
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- RETRY-10 (MUST): A degenerate sub-nanosecond jitter range MUST return the base delay, and a negative jitter sample MUST be floored to zero.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- RETRY-11 (MUST): Delay computation MUST be overflow-safe (saturating to the cap rather than throwing) and MUST reject attempt < 1.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- RETRY-15 (MUST): The pacing-header parser MUST recognize Retry-After as delta-seconds (integer and fractional) and as an RFC 1123 HTTP-date (tolerant of an informational weekday and a single-digit day).
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- RETRY-15 (MUST): The pacing-header parser MUST recognize retry-after-ms and x-ms-retry-after-ms as integer milliseconds.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- RETRY-15 (MUST): The pacing-header parser MUST recognize X-RateLimit-Reset as Unix epoch seconds whose delta is positively jittered to [100%,120%].
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- RETRY-16 (MUST): The pacing-header parser MUST be total (never throw), and malformed, negative or out-of-range values MUST map to no hint (null), NOT a zero delay, so the caller falls back to backoff rather than hammering the server.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- RETRY-17 (MUST): A valid HTTP-date or epoch already in the past MUST yield a zero delay (retry immediately), which is distinct from unparseable meaning no hint.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- RETRY-18 (MUST): Any computed pacing delta MUST be clamped to a finite ceiling of 365 days before nanosecond conversion.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- RETRY-19 (MUST): Numeric Retry-After parsing MUST be screened by a strict decimal grammar before any float parse, rejecting type-suffixed, hex-float, NaN and Infinity forms.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:17-17` · high · sha:9efbe276001e</sub>
- RETRY-20 (MUST): A present pacing hint MUST override (replace, not augment) the exponential schedule for that single decision.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:18-18` · high · sha:9efbe276001e</sub>
- RETRY-20 (MUST): A literal Retry-After hint MUST NOT receive additional symmetric jitter, and where a total-timeout deadline applies the hint MUST still be clamped against it.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:18-18` · high · sha:9efbe276001e</sub>
- RETRY-21 (MUST): Pacing resolution MUST honor a defined precedence and return the first parseable value.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:18-18` · high · sha:9efbe276001e</sub>
- RETRY-22 (MUST): A failure while parsing a pacing header MUST NOT mask the real upstream failure: the loop falls back to exponential backoff and the original throwable remains the surfaced error.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:18-18` · high · sha:9efbe276001e</sub>
- RETRY-23 (MUST): Thread interruption or cancellation MUST never be treated as a retryable failure.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:22-22` · high · sha:9efbe276001e</sub>
- RETRY-23 (MUST): On interrupt during a blocking backoff wait, the implementation MUST restore the cancellation flag, cancel any externally-scheduled wake, abort the retry loop, and surface an interrupted-I/O error.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:22-22` · high · sha:9efbe276001e</sub>
- RETRY-23 (MUST): A downstream interrupt surfaced as an interrupted-I/O error is treated as terminal cancellation and not retried.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:22-22` · high · sha:9efbe276001e</sub>
- RETRY-24 (MUST): A read-timeout represented as a subtype of the interrupted-I/O error MUST NOT be mistaken for cancellation and remains a retryable condition.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:22-22` · high · sha:9efbe276001e</sub>
- RETRY-25 (MUST): Non-recoverable runtime errors (out-of-memory, stack overflow) MUST NOT be retried, classified retryable, or logged, and MUST be surfaced unchanged with no suppressed-trail attachment.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:22-22` · high · sha:9efbe276001e</sub>
- RETRY-26 (MUST): The inter-attempt wait MUST be cancellable/interruptible and MUST NOT pin an execution carrier for its duration, so a naive uninterruptible sleep that cannot be cancelled is non-conforming.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:23-23` · high · sha:9efbe276001e</sub>
- RETRY-27 (MUST): The recovery stack MUST enforce an optional total-timeout budget with per-attempt deadline shrinking, aborting before each attempt if the attempt cap is reached, if elapsed >= budget, or if elapsed + next-delay would exceed the budget.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:27-27` · high · sha:9efbe276001e</sub>
- RETRY-27 (MUST): The recovery stack MUST clamp the delay so it cannot overshoot the total-timeout budget, and a zero budget disables the deadline.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:27-27` · high · sha:9efbe276001e</sub>
- RETRY-28 (MUST): The stage-based retry stack MUST NOT impose a total-timeout budget, and a port that unifies the stacks MUST make the total-timeout an explicitly opt-in feature rather than always-on.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:27-27` · high · sha:9efbe276001e</sub>
- RETRY-13 / RETRY-14 (MUST): Both retry stacks MUST compute backoff via the one shared calculator and constants.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:28-28` · high · sha:9efbe276001e</sub>
- RETRY-13 / RETRY-14 (MUST): The attempt budgets of both stacks MUST denote the same number of total wire sends under equivalent defaults.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:28-28` · high · sha:9efbe276001e</sub>
- RETRY-37 (MUST): In the recovery stack, for a failure carrying a received response, the configured retryable-status set MUST be authoritative (it can both widen and narrow relative to the built-in classifier), while a no-response transport failure falls back to its always-retryable flag.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:29-29` · high · sha:9efbe276001e</sub>
- RETRY-36 (MUST): A re-sent response whose error status is in the configured retryable set MUST be re-mapped into a typed failure (with its body buffered per RETRY-35/RECOV-16) so the loop keeps evaluating the budget (a 503,503,200 sequence reaches the 200), and all other re-sent responses pass through as Success.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:29-29` · high · sha:9efbe276001e</sub>
- RETRY-35 (MUST): A retryable response's body/connection MUST be released before the backoff wait so a socket is not pinned across the delay.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:30-30` · high · sha:9efbe276001e</sub>
- RETRY-35 (MUST): The pacing delay is computed from the still-open response first, and if the retry decision or delay computation throws, the response MUST still be closed before propagating.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:30-30` · high · sha:9efbe276001e</sub>
- RETRY-34 (MUST): On terminal failure, every prior failed attempt's exception MUST be attached to the surfaced exception as suppressed, skipping the surfaced instance itself so a reused exception instance cannot trip a self-suppression error; on eventual success the prior trail MUST be discarded.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:30-30` · high · sha:9efbe276001e</sub>
- RETRY-34 (MUST): A port MUST apply the skip-self suppression guard to both retry stacks, although only the async stack currently implements it in the reference.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:30-30` · high · sha:9efbe276001e</sub>
- RETRY-30 (MUST): The asynchronous retry loop MUST be driven by an iterative trampoline so N retries do NOT build an N-deep chain of future continuations or stack frames.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- RETRY-30 (MUST): A completion warranting another attempt MUST hand control to a single active pump via a re-arm flag rather than recursing.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- RETRY-31 (MUST): Async backoff delays MUST be scheduled non-blockingly, with a zero-length delay completing inline and re-arming the active pump.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- RETRY-32 (MUST): If the caller has already completed or cancelled the returned async result, the retry driver MUST launch no further attempts, and any response arriving from an in-flight attempt MUST be closed rather than leaked.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- RETRY-33 (MUST): Every terminal path of the async retry loop MUST complete the returned future (a throwing predicate, delay computation, log call, or synchronous scheduler rejection each completing it exceptionally), closing any open retryable response first.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:34-34` · high · sha:9efbe276001e</sub>
- RETRY-39 (MUST): The stage stack's delay resolution MUST follow the precedence caller delay-override, then server pacing headers (response path only), then fixed delay, then exponential backoff, with the exception path skipping the header step.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- RETRY-40 (SHOULD): A throwing user delay-override SHOULD be non-fatal (log and fall back), while a throwing should-retry predicate SHOULD abort the call as a well-typed error, with fatal errors rethrown unchanged in both.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- RETRY-41 (MUST): The stage stack MUST resolve the effective retry count as the present override if any (validated non-negative), else the configured value, with a negative configured value clamped to the default and zero meaning no retries.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- RETRY-42 (MUST): All retry policy components MUST be immutable and stateless after construction and safe for concurrent invocation, with every piece of per-call state on the per-call stack/driver, never the shared instance.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- RETRY-43 (MAY): A fixed-delay configuration MAY force a flat delay disabling backoff and jitter (making the backoff path unreachable).
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- RETRY-44 (MUST): Each attempt MUST re-execute the downstream chain with fresh per-attempt continuation state rather than reusing the prior attempt's in-flight chain, and upstream steps MUST NOT mutate the shared in-flight request between attempts.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- RETRY-45 (MUST NOT): The retry engine MUST NOT shut down or close a caller-supplied scheduler, and a process-wide default scheduler, when used, is likewise never shut down by the SDK.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- RETRY-29 (MAY): An opt-in server-driven override MAY let a response header force or suppress the retry classification, flipping only classification and remaining subject to the attempt cap and the re-send-safety gate.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- RETRY-38 (SHOULD): An optional per-attempt request header MAY stamp the 1-based attempt ordinal on a fresh per-attempt copy, never mutating the captured template, preserving any idempotency key, and allocating nothing when disabled.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:35-35` · high · sha:9efbe276001e</sub>
- CFG-35 (SHOULD): A shared retryability classifier treats status 408, 429 and all 5xx except 501 and 505 as retryable, and treats a throwable as retryable iff it or any cause in its chain is an IO or timeout error with cycle-safe cause-chain traversal; where implemented, this exact status set is a hard contract.
  <sub>spec · `docs/product-spec/16-configuration.md:58-58` · high · sha:367e27ec6481</sub>
- XCUT-5 (MUST) The baked retryability flag of a protocol error MUST be computed once at construction from a single shared status classifier and never hardcoded per subclass.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:16-16` · high · sha:d6123be82c9e</sub>
- XCUT-6 (MUST) A transport-family or custom error type that declares itself retryable via the retryability capability MUST participate in retry decisions without editing the classifier, because for such errors the classifier queries the capability rather than matching a concrete type.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:17-17` · high · sha:d6123be82c9e</sub>
- XCUT-7 (MUST) For a protocol error, retry eligibility MUST be decided by a configurable retryable-status set that is authoritative: the retry step consults this set rather than the baked flag, and the set MAY widen or narrow the built-in classification.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:18-18` · high · sha:d6123be82c9e</sub>
- XCUT-7 (MUST) The configurable retryable-status set MUST also govern whether a freshly re-sent error-status response is re-classified as a failure for the next attempt.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:18-18` · high · sha:d6123be82c9e</sub>
- XCUT-10 (MUST) Retry-safety MUST be decided at the retry step, independently of retryability, and applied uniformly to both protocol and transport failures; the safety gate MUST NOT special-case transport errors.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:24-24` · high · sha:d6123be82c9e</sub>
- XCUT-10 (MUST) A request without a body is retry-safe only if its method is idempotent, so a bare POST MUST NOT be retried even when the failure is a transport error that never reached the server.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:24-24` · high · sha:d6123be82c9e</sub>
- XCUT-10 (MUST) A request with a body is retry-safe only if that body is replayable, so a single-use/streaming body MUST NOT be re-sent.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:24-24` · high · sha:d6123be82c9e</sub>
- A body-less request's retry eligibility gates on method idempotency instead of replayability (BODY-5), and that gate is retry-only.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:195-196` · high · sha:da6000c93fc5</sub>
- The adapter guarantees TRANSPORT-17 and TRANSPORT-18 from the body side: a second SerializeToStream call on a single-use body throws StreamConsumedException, failing the send rather than shipping an empty body.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:323-325` · high · sha:da6000c93fc5</sub>
- Inter-attempt waits use Task.Delay(delay, timeProvider, token) (verified: a 30-second delay under a 50 ms token completed with TaskCanceledException after 98 ms), satisfying XCUT-3's prompt, cancellable, non-pinning wait with no thread held.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:518-520` · high · sha:da6000c93fc5</sub>
- The synchronous path waits on a TimeProvider.CreateTimer that signals a ManualResetEventSlim, waited with the call's token (sections 6.1, 8.3), and never uses Thread.Sleep.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:520-521` · high · sha:da6000c93fc5</sub>
- Match is the one fold every chain uses, and any switch expression over Outcome carries a `_ => throw new UnreachableException()` arm.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:192-193` · high · sha:1608fcd4b329</sub>
- RECOV-3 through RECOV-10 are plain folds over arrays copied at construction ([.. steps]), which resolves RECOV-14's reference asymmetry in the stricter direction by copying both lists of both chains.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:224-226` · high · sha:1608fcd4b329</sub>
- Request steps fold left to right and a throw aborts the rest; response steps run only on Success; recovery steps run on every outcome in declared order.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:226-228` · high · sha:1608fcd4b329</sub>
- A throwing response step becomes a Failure for the recovery steps (RECOV-7) and a throwing recovery step becomes a Failure for the next one, so the chain's apply never throws (RECOV-8).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:227-228` · high · sha:1608fcd4b329</sub>
- Dispatch unwraps a Failure with ExceptionDispatchInfo.Capture(error).Throw() (RECOV-10), rethrowing the same instance with its original stack trace, whereas `throw error;` would reset the trace and `throw new ...` would substitute; the as-built RetryPolicy already rethrows this way.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:228-231` · high · sha:1608fcd4b329</sub>
- The pipeline closes the response held by a step that throws, while a step that returns a different outcome owns the response it dropped (RECOV-12/RECOV-13); this asymmetry lives in one internal helper so it cannot be implemented twice.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:231-233` · high · sha:1608fcd4b329</sub>
- A test asserts that a configured status set containing 501 retries a 501 whose baked retryable flag is false.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:34-35` · high · sha:27a0a46f35a8</sub>
- Per XCUT-2 the timeout branch must be checked before the cancellation branch even when the timeout type is a subtype of the cancellation type.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:58-59` · high · sha:27a0a46f35a8</sub>
- Cancellation is never caught by the retry filter (XCUT-1, RETRY-23), and restoring the cancellation flag is free because a cancelled token cannot be un-cancelled.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:65-66` · high · sha:27a0a46f35a8</sub>
- ThreadInterruptedException is treated as cancellation.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:67-67` · high · sha:27a0a46f35a8</sub>
- OperationPolicy must catch the cancellation when its own source fired and the caller's token did not, and throw a non-retryable timeout SdkException carrying it as InnerException; nothing retries it because the policy is outermost.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:71-73` · high · sha:27a0a46f35a8</sub>
- A request is re-sendable if and only if it has no body and an idempotent method, or has a replayable body (XCUT-10, RETRY-5, RETRY-7).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:77-78` · high · sha:27a0a46f35a8</sub>
- A bare POST MUST NOT be retried even when the failure is a transport error that never reached the server (XCUT-10(a)).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:80-82` · high · sha:27a0a46f35a8</sub>
- Retry configuration validates at construction (RECOV-34, appendix C only), against the specification's bound explicitly rather than the one inherited from TimeSpan, since TimeSpan.MaxValue (~29,000 years) replaces the reference's ~292-year nanosecond ceiling.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:84-87` · high · sha:27a0a46f35a8</sub>
- Retry configuration requires a multiplier of at least 1.0, a jitter fraction in [0, 1], and non-negative retries with 0 disabling retry (RETRY-41).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:87-88` · high · sha:27a0a46f35a8</sub>
- Retry status and method sets are copied into FrozenSet<T> when the pipeline is built so that a caller mutating the configured collection cannot change a running client.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:88-89` · high · sha:27a0a46f35a8</sub>
- Overflow saturation of backoff (RETRY-11) is done in ticks before any TimeSpan is constructed.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:99-100` · high · sha:27a0a46f35a8</sub>
- Tests inject a seeded random generator wrapped in a lock behind a Func<double> rather than passing new Random(42) into a shared policy, which would yield flat zero jitter under load.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:104-106` · high · sha:27a0a46f35a8</sub>
- The synchronous wait blocks the caller using a TimeProvider.CreateTimer that sets a ManualResetEventSlim waited with the call's token, never Thread.Sleep and never Task.Delay(...).Wait().
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:124-125` · high · sha:27a0a46f35a8</sub>
- The decimal screen also covers retry-after-ms, x-ms-retry-after-ms and X-RateLimit-Reset (RETRY-15), none of which the as-built parser reads.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:143-144` · high · sha:27a0a46f35a8</sub>
- Every pacing delta is clamped to 365 days in ticks before a TimeSpan is built, since TimeSpan.FromSeconds(1e20) throws OverflowException.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:144-145` · high · sha:27a0a46f35a8</sub>
- A past Retry-After date yields zero delay, distinct from an unparseable value yielding no hint (RETRY-17).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:146-147` · high · sha:27a0a46f35a8</sub>
- A pacing hint replaces the backoff schedule and gets no symmetric jitter (RETRY-20).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:147-147` · high · sha:27a0a46f35a8</sub>
- The retry delay stage stack resolves caller override, then headers, then fixed delay, then exponential (RETRY-39).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:147-148` · high · sha:27a0a46f35a8</sub>
- A pacing parse failure never masks the upstream failure (RETRY-22).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:148-149` · high · sha:27a0a46f35a8</sub>
- Both retry stacks share RetryFacts, the pacing parser and RetryWait, and RETRY-14's three-sends equivalence is asserted by test.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:157-158` · high · sha:27a0a46f35a8</sub>
- There is one retry layer per call path, and it is the SDK's, because only the SDK sees idempotency keys, replayability, typed errors and the configured status set.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:172-174` · high · sha:27a0a46f35a8</sub>
- The SDK-created client and Dexpace.Sdk.Extensions.DependencyInjection register no retrying handler.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:175-176` · high · sha:27a0a46f35a8</sub>
- The DI package's documentation directs consumers of AddStandardResilienceHandler to either set the SDK's MaxRetryAttempts to 0 or build a resilience handler without a retry strategy, keeping the circuit breaker, rate limiter and attempt timeout that the SDK does not replicate.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:176-179` · high · sha:27a0a46f35a8</sub>
- A retryable response is disposed before the wait, with pacing read from it first (RETRY-35; built).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:182-183` · high · sha:27a0a46f35a8</sub>
- The failed-attempt trail is attached with the section 5.2 helper and discarded on success (RETRY-34; not built, the as-built exception path discards prior failures silently).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:183-184` · high · sha:27a0a46f35a8</sub>
- Each retry attempt re-drives with the same request the policy received, which the signature of section 5.1 makes structural (RETRY-44).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:184-186` · high · sha:27a0a46f35a8</sub>
- The async retry loop is a while loop inside one async method so N retries reuse one state machine and build no continuation chain (RETRY-30), and the house no-recursion rule forbids writing it as recursion.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:186-188` · high · sha:27a0a46f35a8</sub>
- An async retry method faults its task on every terminal path (RETRY-33).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:188-188` · high · sha:27a0a46f35a8</sub>
- The retry loop checks the call's token before each attempt and disposes any response that arrives after it fired (RETRY-32).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:189-190` · high · sha:27a0a46f35a8</sub>
- TimeProvider is never disposed by the SDK (RETRY-45).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:190-190` · high · sha:27a0a46f35a8</sub>

## Constraints
- RECOV-15 / RECOV-16: The 1 MiB error-body buffering bound is shared across all buffering paths and the cap is a hard truncation with no marker.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:53-53` · high · sha:33e9443472ce</sub>
- The recovery layer is synchronous; its async equivalent is expressed through the stage-based async pipeline.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:59-59` · high · sha:33e9443472ce</sub>
- The two retry stacks are deliberately built on ONE status classifier, ONE backoff calculator, ONE pacing-header parser and ONE set of tuning constants so behavior cannot drift.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:3-3` · high · sha:9efbe276001e</sub>
- Retry happens only when BOTH a retryable condition and a re-sendable request hold, and the two axes are orthogonal.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:7-7` · high · sha:9efbe276001e</sub>
- SocketsHttpHandler has no public switch for its internal re-send on a connection that fails before the response begins (per runtime source, not reproduced), so the adapter cannot disable it (TRANSPORT-2).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:321-323` · medium · sha:da6000c93fc5</sub>
- The Outcome hierarchy is genuinely closed because no type outside Outcome can call its private constructor, so mutual exclusion and joint exhaustiveness hold by construction, but C# 14 does not prove switch exhaustiveness over it and reports CS8509 on a switch expression lacking a discard arm, which TreatWarningsAsErrors turns into a build break.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:189-192` · high · sha:1608fcd4b329</sub>
- On .NET 10.0.401 neither HttpRequestException nor SocketException derives from IOException, and the runtime reports connection refused, DNS failure and peer reset as HttpRequestException over a SocketException.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:43-46` · high · sha:27a0a46f35a8</sub>
- OperationPolicy implements OverallTimeout with a linked CancellationTokenSource, so when it fires the transport rethrows OperationCanceledException and a caller who never cancelled sees TaskCanceledException, a timeout reported as cancellation that XCUT-1 forbids.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:67-71` · high · sha:27a0a46f35a8</sub>
- System.Random is not thread-safe and fails silently: one seeded Random shared across Parallel.For returned 0 on 1,995,605 of 2,000,000 calls once corrupted, while Random.Shared returned no zeros in a million concurrent draws.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:102-104` · high · sha:27a0a46f35a8</sub>
- Task.Delay accepts 49 days and throws ArgumentOutOfRangeException for 50 days, with or without a TimeProvider, because the timer limit is uint.MaxValue - 1 milliseconds (~49.7 days), while RETRY-18 clamps pacing deltas to 365 days.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:117-120` · high · sha:27a0a46f35a8</sub>
- As built, Retry-After: 5184000 (60 days) makes SendAsync throw an unclassified System.ArgumentOutOfRangeException from a retry decision, which also breaks RETRY-22's rule that pacing must never mask the real failure.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:120-123` · high · sha:27a0a46f35a8</sub>
- RetryConditionHeaderValue.TryParse accepts the single-digit day and both zones but rejects lower case, rejects a date with an inconsistent weekday (CFG-30 says the weekday MUST NOT be validated), rejects fractional delta-seconds (RETRY-15 requires them) and accepts RFC 850 and asctime forms that CFG-31 requires to fail (RFC 9110 obliges recipients to accept them, section 11 item 27).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:131-136` · high · sha:27a0a46f35a8</sub>
- int.TryParse with NumberStyles.None rejects +5, ' 5', 1e3 and 0x10 and reads 08 as 8; double.TryParse with NumberStyles.Float accepts NaN, Infinity and 1e3, which RETRY-19 requires be screened out; and double.TryParse("1.5") under de-DE returns 15.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:136-140` · high · sha:27a0a46f35a8</sub>
- Stacking the SDK's three sends over a standard resilience handler's retry strategy of up to four sends each lets one logical call reach a flaky server about a dozen times, with two independent backoff schedules and pacing honoured twice.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:164-166` · high · sha:27a0a46f35a8</sub>
- A DelegatingHandler retry re-serialises the HttpContent, which for a single-use body is the second write BODY-3 forbids, bypassing the SDK's safety gate; verified, a RequestBody.FromStream body surfaces StreamConsumedException on the second send, and a handler configured to retry POST would do the same for a non-idempotent request.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:166-173` · high · sha:27a0a46f35a8</sub>
- The transport cannot inspect a caller-supplied HttpClient's handler chain because HttpMessageInvoker keeps it private, so the one-retry-layer rule is enforced by construction and documentation.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:174-176` · high · sha:27a0a46f35a8</sub>

## Conclusions
- A closed two-variant outcome is used because it lets one code path handle a throwable and a response identically.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:45-45` · high · sha:33e9443472ce</sub>
- The recovery-chain primitives are the resilience layer used when a concern must observe every outcome uniformly (error-mapping, retry or rescue logic that sees a transport failure and a response failure through one code path and never lets a pre-transport throw bypass it).
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:59-59` · high · sha:33e9443472ce</sub>
- Statuses 501 and 505 are excluded from retryable because they mean the server cannot fulfill the request regardless of retry.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:9-9` · high · sha:9efbe276001e</sub>
- RECOV-1's closed two-variant outcome is a record hierarchy closed by a private constructor (abstract record Outcome with sealed records Success(Response) and Failure(Exception) and a Match<T> fold), following the styleguide's Result pattern (08-error-handling 8.7).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:176-187` · high · sha:1608fcd4b329</sub>
- The same Outcome type is reused, not re-invented, by the SSE typed adapter (section 7.2).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:194-194` · high · sha:1608fcd4b329</sub>
- The four reference drifts the specification pushes onto porters (RECOV-14, BODY-8, RETRY-34, AUTH-31) are resolved as Ruby resolved them (section 11 item 12), and each is additionally unable to drift between sync and async because section 5.3 gives every policy one implementation for both.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:233-236` · high · sha:1608fcd4b329</sub>
- The .NET port designs retry, redirect and authentication from platform idioms rather than from the specification, with the siblings informing what to build and not how.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:3-5` · high · sha:27a0a46f35a8</sub>
- Because the SDK pipeline sits above an HttpClient whose handler chain can already retry and follow redirects, the port decides which layer owns each behaviour and makes the other layer stand down.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:6-9` · high · sha:27a0a46f35a8</sub>
- The idempotent method set {GET, HEAD, OPTIONS, PUT, DELETE} (HTTP-9/RETRY-6), the RETRY-1 classifier (408, 429, and 500-599 except 501 and 505), the XCUT-7 default configurable set {408, 429, 500, 502, 503, 504} and the RETRY-13 backoff calculator live in one internal static class, RetryFacts, as FrozenSet constants and pure functions so that nothing can drift.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:20-25` · high · sha:27a0a46f35a8</sub>
- Method.IsIdempotent is IsSafe || PUT || DELETE and IsSafe includes TRACE, so the model's idempotent set has six members where RETRY-6 requires a set equal to five; the port follows the specification's set everywhere and Method keeps no public RFC-meaning accessor beside it.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:25-29` · high · sha:27a0a46f35a8</sub>
- The baked retryable flag on a protocol error (XCUT-5, HttpResponseException.IsRetryable, not yet built) is computed once from the classifier, while the retry step consults only the configured RetryOptions.RetryableStatusCodes, authoritative in both directions (RETRY-37).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:32-35` · high · sha:27a0a46f35a8</sub>
- XCUT-6's open retryable capability is the interface IRetryableError { bool IsRetryable { get; } } because .NET cannot duck-type a property; SdkException implements it virtually (ServiceRequestException returns true, XCUT-4) and a third-party transport exception can implement it without touching core.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:37-41` · high · sha:27a0a46f35a8</sub>
- The non-capability branch of the classifier looks for an I/O error or timeout error anywhere in the cause chain (RETRY-2), walked with the cycle-safe enumerator of section 5.2, and classification consults the capability first as RECOV-17 (stated in appendix C only) requires.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:41-48` · high · sha:27a0a46f35a8</sub>
- The port's I/O family for retry classification is IOException, HttpRequestException without a status code, SocketException and TimeoutException, rejecting an is-IOException test that would call the most common transient failures non-retryable, so every pre-response failure listed in RETRY-4 is retryable at the condition level.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:46-49` · high · sha:27a0a46f35a8</sub>
- XCUT-4(b)'s requirement that the wrapper belong to the runtime's I/O-error family is not met, because ServiceRequestException derives from SdkException and cannot also be an HttpRequestException, so an existing catch (HttpRequestException) site does not match it; the original exception is preserved as InnerException and the classifier walks it (section 10 entry 7, section 11 item 23).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:49-55` · high · sha:27a0a46f35a8</sub>
- The SystemNet transport distinguishes timeout from cancellation by the caller token's state, not the exception type, and maps a timeout to ServiceRequestTimeoutException, which the retry policy retries (a 200 ms HttpClient.Timeout against a 3 s handler produced three server hits for two configured retries).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:61-65` · high · sha:27a0a46f35a8</sub>
- DexpaceClientOptions.AttemptTimeout, declared but read by nothing, belongs in the retry policy as a per-attempt linked source, classified like the overall timeout but retryable (XCUT-2).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:73-75` · high · sha:27a0a46f35a8</sub>
- The as-built predicate (body is null || body.IsReplayable) && (method.IsIdempotent || RetryNonIdempotentWhenReplayable) is wrong in both directions, not retrying a POST with a replayable body by default and retrying a bodyless POST when the option is on; the port drops the option and implements the rule as written.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:78-82` · high · sha:27a0a46f35a8</sub>
- Backoff follows the specification: the as-built full-jitter formula, uniform over [0, min(base x 2^attempt, max)], violates RETRY-9 (initialDelay x multiplier^(attempt-1)) and RETRY-10 (symmetric jitter over [d(1-j/2), d(1+j/2)]).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:92-95` · high · sha:27a0a46f35a8</sub>
- The port keeps Polly's option name MaxRetryAttempts, which counts retries not sends (P14), and sets it to 2 so that three sends hold, preserving RETRY-14's equivalence of three sends across the two stacks.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:96-98` · high · sha:27a0a46f35a8</sub>
- The random source for jitter is an injectable Func<double> defaulting to Random.Shared.NextDouble, beside an injectable TimeProvider, which together make backoff tests deterministic.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:100-102` · high · sha:27a0a46f35a8</sub>
- On the async path the inter-attempt wait is a Task.Delay over TimeProvider, which is a timer pinning no thread and which on cancellation disposes its timer and faults with TaskCanceledException (an hour-long delay cancelled after 50 ms completed in 53 ms) (RETRY-26, XCUT-3, P7).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:114-117` · high · sha:27a0a46f35a8</sub>
- The port's single RetryWait.DelayAsync performs any delay above 49 days as successive bounded waits, at most eight for 365 days.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:123-123` · high · sha:27a0a46f35a8</sub>
- Pacing headers need a hand-written parser because the BCL's is both too strict and too lenient: the as-built DateTimeOffset.TryParseExact(value, "r") is case-sensitive, rejects the single-digit day and rejects UTC and +0000 zones, all of which CFG-30 and RETRY-15 require be accepted.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:127-131` · high · sha:27a0a46f35a8</sub>
- The port ships one span-based RFC 1123 parser shared with CFG-29 to CFG-31 and one strict decimal screen (digits [ "." digits ]) applied before any numeric parse, and parses with CultureInfo.InvariantCulture explicitly, never null.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:140-143` · high · sha:27a0a46f35a8</sub>
- The port keeps both retry stacks because it keeps both layers: the stage RetryPolicy, which imposes no total-timeout budget (RETRY-28), and the recovery-chain retry with a deadline-shrinking budget (RETRY-27), which is not built; the port invokes neither of the quoted unification sanctions (RETRY-28 in 09-retry-and-resilience.md and the pipelines chapter 08-execution-pipelines.md; section 11 item 19).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:151-156` · high · sha:27a0a46f35a8</sub>
- OperationPolicy.OverallTimeout is not a stack unification: it is a whole-call deadline enforced by cancellation, outside both loops, off by default.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:156-157` · high · sha:27a0a46f35a8</sub>
- The connection-layer split routes connection-level resilience into the transport's HttpClient so that an enterprise's DelegatingHandler/Polly chain composes underneath the SDK, though the specification has no clause for a second retry engine below the transport and composing two unchanged produces two failures (section 11 item 24).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:160-164` · high · sha:27a0a46f35a8</sub>

## Reference
- The idempotent-method set is {GET, HEAD, OPTIONS, PUT, DELETE}.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:17-17` · high · sha:8014d2ec2c9d</sub>
- The retryable-status classifier is 408, 429, and all 5xx except 501 and 505.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:17-17` · high · sha:8014d2ec2c9d</sub>
- HTTP-9 / RETRY-1 / RETRY-6 rationale is that divergent copies of these classifications produce inconsistent retry behavior; conformance is to table-drive every method and status against the single definition and each consumer of it.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:17-17` · high · sha:8014d2ec2c9d</sub>
- RETRY-13 is the explicit anti-drift guarantee; conformance is to feed identical settings to both stacks with jitter neutralized and assert identical delay sequences.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:18-18` · high · sha:8014d2ec2c9d</sub>
- The recovery-chain primitives thread a closed two-variant outcome through a fold so every failure (pre-request, transport, or post-response) is observed uniformly.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:3-3` · high · sha:33e9443472ce</sub>
- RECOV-2 conformance: a throwing request step and a throwing transport each surface as a Failure to a recovery hook.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:46-46` · high · sha:33e9443472ce</sub>
- In the reference implementation the request recovery chain does NOT copy its list (it retains the caller's read-only list reference directly), so a port SHOULD copy there too and must not assume the asymmetry away.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:52-52` · high · sha:33e9443472ce</sub>
- RECOV-15/16 conformance: statuses 400..599 produce a Failure with a typed exception, and a sub-cap error body survives whole while an over-cap body is truncated to the cap with the connection released.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:53-53` · high · sha:33e9443472ce</sub>
- The recovery-aware retry stack enforces a total-timeout budget that the stage-based retry step omits (see RETRY-27, RETRY-28).
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:59-59` · high · sha:33e9443472ce</sub>
- Retry is automatic, safety-gated re-execution of a failed exchange, and the SDK ships two cooperating stacks (the recovery-chain retry with a total-timeout budget and the stage-based retry step).
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:3-3` · high · sha:9efbe276001e</sub>
- The single retryable-status classifier is the definition from which the response-carrying exception flag and the stage stack's default predicate derive, and the recovery stack layers its own configurable status allow-list on top (RETRY-37).
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:9-9` · high · sha:9efbe276001e</sub>
- RETRY-12 (SHOULD): Retry defaults SHOULD be initial delay 200 ms, multiplier 2.0, max delay 8 s, jitter 0.2, and a budget of 3 sends.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:16-16` · high · sha:9efbe276001e</sub>
- The recovery stack scans the whole header map with fixed pacing precedence (Retry-After numeric then date, then retry-after-ms, then x-ms-retry-after-ms, then X-RateLimit-Reset), whereas the stage stack walks a caller-configurable ordered header list.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:18-18` · high · sha:9efbe276001e</sub>
- Reference implementation for RETRY-26: the recovery stack schedules the wake on a shared scheduler and blocks on the resulting future, the stage-sync stack performs an interruptible sleep that unmounts a virtual-thread carrier, and async implementations schedule the delay without blocking a thread.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:23-23` · high · sha:9efbe276001e</sub>
- The recovery stack's max-attempts (default 3) equals the stage stack's max-retries (default 2) plus one initial send.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:28-28` · high · sha:9efbe276001e</sub>
- The reference code for RETRY-37 is authoritative-contains, not an intersection with the baked flag, and a port should follow that.
  <sub>spec · `docs/product-spec/09-retry-and-resilience.md:29-29` · high · sha:9efbe276001e</sub>
- A protocol error carries a baked retryability flag while the retry step gates protocol errors on a separate configurable status set; the configured set is what the retry step actually consults, and the two are distinct notions with distinct default membership (XCUT-5 versus XCUT-7).
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:5-5` · high · sha:d6123be82c9e</sub>
- The shared status classifier treats 408, 429, and all 5xx except 501 and 505 as retryable and everything else as not retryable, so a 5xx outside the tested list such as 507 bakes retryable true.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:16-16` · high · sha:d6123be82c9e</sub>
- The baked retryability flag is a queryable property, while the retry step's actual eligibility gate for a protocol error is the configured retryable-status set (XCUT-7).
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:16-16` · high · sha:d6123be82c9e</sub>
- Protocol errors are handled by XCUT-7 and their baked flag is not consulted by the retry step.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:17-17` · high · sha:d6123be82c9e</sub>
- The default retryable-status set is {408, 429, 500, 502, 503, 504}.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:18-18` · high · sha:d6123be82c9e</sub>
- Ensuring that a re-sent body-bearing request on a non-idempotent method is safe, for example via an idempotency key, is the caller's responsibility.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:24-24` · high · sha:d6123be82c9e</sub>
- As built for section 5.2: not built, with no Outcome, no recovery chain, no suppressed trail and no shared cause walker, and the fatal filter and dispatch-info rethrow appear only inside RetryPolicy.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:261-262` · high · sha:1608fcd4b329</sub>
- A static in one assembly is one copy per load context, so a host loading Dexpace.Sdk.Core into two AssemblyLoadContexts gets two copies of identical constants.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:23-25` · high · sha:27a0a46f35a8</sub>
- As built, RetryPolicy hardcodes its status set in a private field, so XCUT-7's configurable set and RETRY-1's classifier do not yet exist as separate things.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:29-30` · high · sha:27a0a46f35a8</sub>
- HttpClient.Timeout raises TaskCanceledException with an inner TimeoutException, while cancelling the caller's token raises TaskCanceledException with no TimeoutException in the chain (verified on .NET 10.0.401).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:57-61` · high · sha:27a0a46f35a8</sub>
- As built, RetryOptions is an unvalidated mutable class read live on every call (section 8.2).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:89-90` · high · sha:27a0a46f35a8</sub>
- RETRY-12 defaults are 200 ms initial delay, multiplier 2.0, 8 s maximum delay, jitter 0.2 and three sends, whereas as built MaxDelay is 30 s and MaxRetryAttempts is 3, which is four sends.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:95-97` · high · sha:27a0a46f35a8</sub>
- Microsoft.Extensions.Http.Resilience is not in the offline NuGet cache of the authoring machine, so its default strategy composition is cited from documentation and not verified.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:179-180` · high · sha:27a0a46f35a8</sub>
- As built at d45e64b, retry diverges: full-jitter formula and non-spec defaults (four sends), hardcoded status set, no classifier or IsRetryable, safety predicate wrong both ways, case-sensitive integer-only unclamped Retry-After parser (a 60-day hint throws), no -ms or rate-limit headers, no suppressed trail, overall deadline surfacing as cancellation, unused AttemptTimeout, and no recovery-chain stack.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:192-195` · high · sha:27a0a46f35a8</sub>

## Conflicts

## Superseded

