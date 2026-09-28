# concurrency-and-async

## Rules
- An async-runtime adapter that owns an executor MUST implement close as an idempotent, ownership-aware release where only the first close shuts the owned executor and emits the lifecycle event; closing MUST NOT be required to cancel in-flight requests (a graceful drain is acceptable), and an adapter over a caller-supplied executor MUST NOT shut it down (SEAM-25).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:44-44` · high · sha:0adae2d6a47f</sub>
- Async-runtime adapters that hand work to another thread SHOULD propagate the ambient logging/diagnostic context across the thread handoff and SHOULD map cancellation bidirectionally per that ecosystem's idiom (SEAM-24).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:45-45` · high · sha:0adae2d6a47f</sub>
- The time abstraction is an injectable seam exposing three operations—current wall-clock instant, a monotonic elapsed-time counter, and a blocking interruptible sleep—with a shared platform-backed default provided, and time-dependent logic should route through this seam so tests can drive time deterministically. (CFG-15)
  <sub>spec · `docs/product-spec/16-configuration.md:32-32` · high · sha:367e27ec6481</sub>
- The monotonic counter must be non-decreasing and used only for measuring elapsed durations between its own readings (its absolute value is not meaningful), while the wall-clock reading may move backwards and must not be used for elapsed-time measurement. (CFG-16)
  <sub>spec · `docs/product-spec/16-configuration.md:33-33` · high · sha:367e27ec6481</sub>
- sleep must reject a negative duration, must allow a zero duration by returning promptly, and must honor cooperative cancellation by re-asserting the interrupt/cancellation status before propagating when interrupted mid-sleep. (CFG-17)
  <sub>spec · `docs/product-spec/16-configuration.md:34-34` · high · sha:367e27ec6481</sub>
- The async layer should provide a scheduled non-blocking delay yielding a future that completes after a non-negative duration on a scheduler without blocking a thread, completing immediately for zero, rejecting negative durations, and cancelling the future must cancel the underlying scheduled task. (CFG-18)
  <sub>spec · `docs/product-spec/16-configuration.md:35-35` · high · sha:367e27ec6481</sub>
- When surfacing the cause of a failed async operation, the subsystem should unwrap the platform's async-completion wrapper exceptions to the original throwable, terminating on the first non-wrapper cause, a null cause, or a detected cycle, and returning a non-wrapper unchanged. (CFG-19)
  <sub>spec · `docs/product-spec/16-configuration.md:36-36` · high · sha:367e27ec6481</sub>
- The subsystem should provide an interruptible-task future that runs a task on an executor such that cancel-with-interrupt interrupts the running worker while cancel-without-interrupt does not, never interrupting a queued or finished task, clearing the worker's interrupt state before it returns to its pool, and delivering rejected submission through the future rather than throwing synchronously. (CFG-20)
  <sub>spec · `docs/product-spec/16-configuration.md:37-37` · high · sha:367e27ec6481</sub>
- When an interruptible-task future has already been cancelled and the task nonetheless produced a closeable result, that result must be closed on the discard path on a best-effort basis, swallowing close failures, and the close helper must be null-safe. (CFG-21)
  <sub>spec · `docs/product-spec/16-configuration.md:38-38` · high · sha:367e27ec6481</sub>
- The async transport contract is a single-value completion future that yields exactly one Response on success or completes with exactly one failure, delivering a non-null Response on success, and an implementation with no response must complete via the failure channel rather than deliver a null or absent value. (ASYNC-1)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:7-7` · high · sha:f1bf00174456</sub>
- Every failure detectable while constructing the async operation, such as request-adaptation errors or worker-pool rejection, must be delivered through the future's failure channel and never thrown synchronously. (ASYNC-2)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:8-8` · high · sha:f1bf00174456</sub>
- Adapters that move work/callbacks onto another thread should propagate the caller's diagnostic logging context across the hop by capturing it on the boundary thread and reinstating it on the executing/callback thread, though this is an observability guarantee only—an adapter that omits it still executes exchanges correctly. (ASYNC-8)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:20-20` · high · sha:f1bf00174456</sub>
- When an adapter reinstates a captured logging context, it must first save the executing thread's prior context, install the captured context only for the work's duration, and restore the prior context afterward including when the work throws, so a reused or pooled thread's own context is never clobbered. (ASYNC-9)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:21-21` · high · sha:f1bf00174456</sub>
- When an adapter propagates logging context, capture must occur at the point that identifies the logical caller—per-subscription for cold/reusable stream or promise objects, per-task-submission for executor decorators—not at object-construction time, so a reused async object picks up the live context of each use. (ASYNC-10)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:22-22` · high · sha:f1bf00174456</sub>
- When an adapter propagates logging context, capture and restore must be safe when no logging-context backend is installed: an absent context captures as empty, and reinstating an empty context clears the target thread's context rather than raising. (ASYNC-11)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:23-23` · high · sha:f1bf00174456</sub>
- On runtimes where a newly created worker does not inherit the spawning thread's logging context (lightweight threads or plain thread-local contexts), an adapter that propagates logging context must explicitly transfer it at the thread-creation boundary, distinct from any carrier-hop guarantee the runtime provides. (ASYNC-12)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:24-24` · high · sha:f1bf00174456</sub>
- When surfacing a failure, adapters must unwrap the async framework's wrapper exceptions down to the original cause so typed handlers match the real exception, terminating unwrapping on the first non-wrapper cause, a null cause, or a detected cycle. (ASYNC-13)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:28-28` · high · sha:f1bf00174456</sub>
- An async-to-sync blocking bridge must honor thread interruption while awaiting by restoring the interrupt flag, cancelling the in-flight future, and throwing an interrupted-I/O failure; it must unwrap execution-wrapper exceptions so blocking callers see the original failure, and a future cancelled independently must surface its cancellation as-is rather than remapped to I/O. (ASYNC-14)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:29-29` · high · sha:f1bf00174456</sub>
- An adapter that owns an executor or background threads must expose a close/dispose operation that is idempotent (repeated calls safe, only the first performs shutdown), ownership-aware (releases only SDK-owned resources, never a caller-supplied executor/client), and interrupt-safe (honors thread interruption on any blocking shutdown step). (ASYNC-15)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:33-33` · high · sha:f1bf00174456</sub>
- An adapter that owns an executor should shut it down gracefully on close, stopping new work and waiting for in-flight tasks rather than interrupting them, escalating to forceful shutdown only if the closing thread is itself interrupted, with callers needing eager abort using the interrupt/structured-cancellation path instead. (ASYNC-16)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:34-34` · high · sha:f1bf00174456</sub>
- The async transport SPI should provide a no-op default close so lightweight/functional implementations need not implement lifecycle management, while any implementation that owns resources overrides it to follow the executor lifecycle contract; behavior of executeAsync after close is undefined. (ASYNC-17, ASYNC-15)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:35-35` · high · sha:f1bf00174456</sub>
- The non-blocking scheduled-delay primitive must complete after the requested delay without blocking a thread, complete immediately for a zero delay, reject a negative delay, and cancelling the returned future must cancel the underlying scheduled task so no scheduler thread is held. (ASYNC-18)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:39-39` · high · sha:f1bf00174456</sub>
- Every bridge and facade overload that accepts per-call request options must thread those options into the wrapped send so per-request overrides survive the async boundary, rather than being dropped by the SPI's options-ignoring default overload. (ASYNC-19)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:40-40` · high · sha:f1bf00174456</sub>
- Once a Response has been delivered to the caller through the future, cancelling that future must not close the Response body, since the caller owns closing it even when discarding it. (ASYNC-20, ASYNC-5)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:41-41` · high · sha:f1bf00174456</sub>
- An adapter exposing a streaming source such as SSE as a reactive stream must honor downstream backpressure by polling the source at most once per unit of demand, must complete on end-of-source, must propagate a source exception as an error signal without swallowing fatal errors, must not close the caller-owned source on any termination, and must treat the source as single-subscriber with a fresh source per subscription. (ASYNC-21)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:42-42` · high · sha:f1bf00174456</sub>
- Async transport implementations must be safe for concurrent calls from multiple threads, with all per-call mutable state confined to the returned future's completion graph. (ASYNC-22)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:43-43` · high · sha:f1bf00174456</sub>
- Components documented as shared/reusable across concurrent requests (pipeline steps, auth handlers, redactors, factories) MUST be safe for concurrent invocation, with per-call mutable state living on the call's stack/local state rather than the shared instance, and any shared mutable state synchronized. (XCUT-11)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:28` · high · sha:d6123be82c9e</sub>
- Hot-path reads of a credential/token cache SHOULD be wait-free, refresh SHOULD be single-flight so only one concurrent caller fetches an expiring token while others reuse the result, and any lock guarding refresh MUST be scoped to that cache so it never serializes unrelated in-flight requests or the global scheduler. (XCUT-12)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:29` · high · sha:d6123be82c9e</sub>
- The core SHOULD be concurrency-model agnostic, exposing plain blocking operations correct on any scheduler and leaking no async-framework types into the core public surface, with shared mutable state guarded for safe concurrent access. (NFR-11)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:33` · high · sha:5f4684bf7123</sub>

## Constraints

## Conclusions
- The async-runtime concern is deliberately not a core interface; instead it is decoupled through a canonical, dependency-free async future type that every ecosystem adapter bridges to and from (SEAM-17).
  <sub>spec · `docs/product-spec/01-product-overview.md:9-9` · high · sha:4f786c44354d</sub>
- The async transport contract SHOULD be expressed in terms of one canonical, dependency-free async primitive (a future completing with a value or exceptionally) as an interop pivot, with ecosystem facades (coroutines, reactive streams, event-loop futures, virtual threads) implemented as separate adapter modules bridging to and from that pivot (SEAM-17).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:18-18` · high · sha:0adae2d6a47f</sub>
- The reference implements the inter-attempt retry wait as a scheduled timer completing an awaitable future so a virtual-thread carrier can unmount without monopolizing a shared pool thread, and a port SHOULD preserve that non-pinning property where its runtime has an equivalent concern. (XCUT-3)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:11` · medium · sha:d6123be82c9e</sub>

## Reference
- The reference implementation of the canonical async interop pivot is java.util.concurrent.CompletableFuture. (SEAM-17)
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:18-18` · high · sha:0adae2d6a47f</sub>
- The async-runtime adapter contract's interchange point is a single canonical completion future carrying exactly one success value or one failure, to which every ecosystem facade (coroutines, reactive Mono/Flux, event-loop futures, virtual threads) bridges. (SEAM-17)
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:3-3` · high · sha:f1bf00174456</sub>
- The canonical completion future is the single dependency-free async value type that carries exactly one success value or one failure and is the interop pivot every ecosystem adapter bridges to and from, with the JVM reference being CompletableFuture.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:13` · high · sha:f0b3d2058626</sub>
- Pooled-thread poisoning is the failure mode where an interrupt aimed at a cancelled call reaches a worker after it has returned to its pool and picked up unrelated work, prevented by an ordering handshake.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:45` · high · sha:f0b3d2058626</sub>
- The async adapter's single-value future is never null, completing either with a value or exceptionally. (ASYNC-1, ASYNC-2)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:67` · high · sha:0451cc7f3bb4</sub>
- A construction-time failure in the async adapter is delivered through the future's failure channel rather than thrown synchronously. (ASYNC-1, ASYNC-2)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:67` · high · sha:0451cc7f3bb4</sub>
- Cancellation has two modes in the async adapter, and a queued or already-finished task is never interrupted. (ASYNC-3, ASYNC-4, ASYNC-5, ASYNC-6, ASYNC-7)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68` · high · sha:0451cc7f3bb4</sub>
- Interrupt delivery in the async adapter is ordered so that pooled-thread poisoning does not occur under stress. (ASYNC-3, ASYNC-4, ASYNC-5, ASYNC-6, ASYNC-7)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68` · high · sha:0451cc7f3bb4</sub>
- An orphaned closeable resource on the losing side of a race in the async adapter is closed exactly once. (ASYNC-3, ASYNC-4, ASYNC-5, ASYNC-6, ASYNC-7)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68` · high · sha:0451cc7f3bb4</sub>
- Cancellation is bidirectional per async adapter, meaning cancelling either side cancels the other. (ASYNC-3, ASYNC-4, ASYNC-5, ASYNC-6, ASYNC-7)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68` · high · sha:0451cc7f3bb4</sub>
- Each async adapter documents whether it uses interrupt-mode cancellation or not. (ASYNC-3, ASYNC-4, ASYNC-5, ASYNC-6, ASYNC-7)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68` · high · sha:0451cc7f3bb4</sub>
- Logging context propagates across asynchronous hops. (ASYNC-8, ASYNC-9, ASYNC-10, ASYNC-11, ASYNC-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69` · high · sha:0451cc7f3bb4</sub>
- Logging context is saved, installed, and restored across an async hop, including when the operation throws. (ASYNC-8, ASYNC-9, ASYNC-10, ASYNC-11, ASYNC-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69` · high · sha:0451cc7f3bb4</sub>
- Logging context is captured per-subscription or per-submission in the async adapter, not once at assembly time. (ASYNC-8, ASYNC-9, ASYNC-10, ASYNC-11, ASYNC-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69` · high · sha:0451cc7f3bb4</sub>
- Async logging-context propagation is safe when no logging backend is present. (ASYNC-8, ASYNC-9, ASYNC-10, ASYNC-11, ASYNC-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69` · high · sha:0451cc7f3bb4</sub>
- On lightweight-thread runtimes, logging-context transfer at the thread-creation boundary is explicit. (ASYNC-8, ASYNC-9, ASYNC-10, ASYNC-11, ASYNC-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69` · high · sha:0451cc7f3bb4</sub>
- Async wrapper-exception unwrapping is cycle-safe. (ASYNC-13, ASYNC-14)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:70` · high · sha:0451cc7f3bb4</sub>
- The blocking bridge onto an async result honors interruption and unwraps the wrapper exception. (ASYNC-13, ASYNC-14)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:70` · high · sha:0451cc7f3bb4</sub>
- Closing an SDK-owned executor is idempotent, ownership-aware, and interrupt-safe. (ASYNC-15, ASYNC-16, ASYNC-17)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:71` · high · sha:0451cc7f3bb4</sub>
- Closing the async adapter gracefully shuts down its owned executor. (ASYNC-15, ASYNC-16, ASYNC-17)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:71` · high · sha:0451cc7f3bb4</sub>
- A functional-interface-based async implementation defaults its close operation to a no-op. (ASYNC-15, ASYNC-16, ASYNC-17)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:71` · high · sha:0451cc7f3bb4</sub>
- A scheduled delay in the async adapter is non-blocking, resolves immediately for a zero delay, rejects a negative delay, and cancelling it cancels the underlying task. (ASYNC-18, ASYNC-19, ASYNC-20, ASYNC-21, ASYNC-22)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72` · high · sha:0451cc7f3bb4</sub>
- Per-call options are threaded through every async bridge. (ASYNC-18, ASYNC-19, ASYNC-20, ASYNC-21, ASYNC-22)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72` · high · sha:0451cc7f3bb4</sub>
- A Response already delivered to the caller is not closed by a late cancellation. (ASYNC-18, ASYNC-19, ASYNC-20, ASYNC-21, ASYNC-22)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72` · high · sha:0451cc7f3bb4</sub>
- The reactive SSE bridge honors backpressure, does not close the source itself, and supports only a single subscriber. (ASYNC-18, ASYNC-19, ASYNC-20, ASYNC-21, ASYNC-22)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72` · high · sha:0451cc7f3bb4</sub>
- The async transport implementation is concurrency-safe. (ASYNC-18, ASYNC-19, ASYNC-20, ASYNC-21, ASYNC-22)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72` · high · sha:0451cc7f3bb4</sub>

## Conflicts

## Superseded
