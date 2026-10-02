# concurrency-and-async

## Rules
- SEAM-11 (MUST) - The sync transport MUST be a single-operation contract that, given one request, produces one response, and MUST NOT pre-buffer the response body, because the caller owns reading and closing it.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:12-12` · high · sha:0adae2d6a47f</sub>
- The sync transport MAY accept per-call options, and a transport that ignores options MUST behave identically to the no-options call (SEAM-11).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:12-12` · high · sha:0adae2d6a47f</sub>
- SEAM-12 (MUST) - Transports, sync and async, MUST be safe for concurrent calls, with all per-request state confined to locals or the returned response/future graph.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:13-13` · high · sha:0adae2d6a47f</sub>
- SEAM-16 (MUST) - The async transport's returned future MUST complete either with a non-null response (caller owns closing it) or exceptionally, and MUST NOT complete successfully with a null/absent value.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:17-17` · high · sha:0adae2d6a47f</sub>
- SEAM-17 (SHOULD) - The async transport contract SHOULD be expressed in terms of one canonical, dependency-free async primitive (a future completing with a value or exceptionally) that serves as the interop pivot.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:18-18` · high · sha:0adae2d6a47f</sub>
- Ecosystem facades (coroutines, reactive streams, event-loop futures, virtual threads) SHOULD be separate adapter modules bridging to and from the pivot, preserving cancellation and error semantics with per-adapter caveats documented (SEAM-17).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:18-18` · high · sha:0adae2d6a47f</sub>
- SEAM-18 (MUST) - The provided sync-to-async and async-to-sync bridges MUST preserve semantics, and per-call options MUST be threaded through, not dropped.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:19-19` · high · sha:0adae2d6a47f</sub>
- Wrapping a blocking transport as async REQUIRES a caller-supplied executor, with intentionally no default, and a shared global fork/join-style pool is explicitly unacceptable because a blocking call would starve it (SEAM-18).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:19-19` · high · sha:0adae2d6a47f</sub>
- Wrapping an async transport as blocking MUST unwrap the async-wrapper exception so callers see the original failure, and the blocking wait MUST honor interruption by restoring the interrupt flag, cancelling the in-flight future and surfacing an interrupted-I/O error (SEAM-18).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:19-19` · high · sha:0adae2d6a47f</sub>
- SEAM-30 (MUST) - On any path where a send produces a response the returned future will NOT hand to a caller (future already cancelled or completed exceptionally, the completion race), the producer MUST close that orphaned response so its connection/descriptor is not leaked.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:20-20` · high · sha:0adae2d6a47f</sub>
- SEAM-24 (SHOULD) - Async-runtime adapters that hand work to another thread SHOULD propagate the ambient logging/diagnostic context across the thread handoff, and SHOULD map cancellation bidirectionally per that ecosystem's idiom.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:45-45` · high · sha:0adae2d6a47f</sub>
- CFG-15 (MUST): The time abstraction is an injectable seam exposing three operations (current wall-clock instant, monotonic elapsed-time counter, blocking interruptible sleep) with a shared platform-backed default, and time-dependent logic SHOULD route through the seam so tests can drive time deterministically.
  <sub>spec · `docs/product-spec/16-configuration.md:32-32` · high · sha:367e27ec6481</sub>
- CFG-16 (MUST): The monotonic counter is non-decreasing and used only for measuring elapsed durations between its own readings because its absolute value is not meaningful, while the wall-clock reading MAY move backwards and MUST NOT be used for elapsed-time measurement.
  <sub>spec · `docs/product-spec/16-configuration.md:33-33` · high · sha:367e27ec6481</sub>
- CFG-17 (MUST): Clock sleep rejects a negative duration, allows a zero duration (returning promptly), and honors cooperative cancellation by re-asserting the interrupt or cancellation status before propagating when interrupted mid-sleep.
  <sub>spec · `docs/product-spec/16-configuration.md:34-34` · high · sha:367e27ec6481</sub>
- CFG-18 (SHOULD): The async layer provides a scheduled non-blocking delay yielding a future that completes after a non-negative duration without blocking a thread; zero completes immediately, negative is rejected, and cancelling the future MUST cancel the underlying scheduled task.
  <sub>spec · `docs/product-spec/16-configuration.md:35-35` · high · sha:367e27ec6481</sub>
- CFG-19 (SHOULD): When surfacing the cause of a failed async operation, the subsystem unwraps the platform's async-completion wrapper exceptions to the original throwable, terminating on the first non-wrapper, a null cause, or a detected cycle, and returning a non-wrapper unchanged.
  <sub>spec · `docs/product-spec/16-configuration.md:36-36` · high · sha:367e27ec6481</sub>
- CFG-20 (SHOULD): The subsystem provides an interruptible-task future in which cancel-with-interrupt interrupts the running worker and cancel-without does not, a queued or finished task is never interrupted, the worker's interrupt state is cleared before it returns to its pool, and a rejected submission is delivered through the future and never thrown synchronously.
  <sub>spec · `docs/product-spec/16-configuration.md:37-37` · high · sha:367e27ec6481</sub>
- CFG-21 (MUST): When an interruptible-task future has already been cancelled and the task nonetheless produced a closeable result, that result is closed on the discard path (best-effort, swallowing close failures), and the close helper is null-safe.
  <sub>spec · `docs/product-spec/16-configuration.md:38-38` · high · sha:367e27ec6481</sub>
- ASYNC-1 (MUST) An async transport implementation that has no response MUST complete via the failure channel rather than deliver a null or absent value.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:7-7` · high · sha:f1bf00174456</sub>
- ASYNC-2 (MUST) Every failure detectable while constructing the async operation, such as request-adaptation errors or worker-pool rejection, MUST be delivered through the future's failure channel and never thrown synchronously.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:8-8` · high · sha:f1bf00174456</sub>
- ASYNC-3 (MUST) When an async operation is backed by a blocking task on a worker thread, cancellation MUST distinguish cancel-with-interrupt (interrupts the worker running the in-flight task) from cancel-without-interrupt (cancels the logical operation without interrupting).
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:12-12` · high · sha:f1bf00174456</sub>
- ASYNC-3 (MUST) A task that is still queued or already finished MUST NOT be interrupted by cancellation.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:12-12` · high · sha:f1bf00174456</sub>
- ASYNC-4 (MUST) Interrupt delivery MUST be ordered so a stale interrupt cannot poison a pooled thread: the cancel path publishes an "interrupt in flight" marker before reading the worker, the worker's return-to-pool step blocks until that marker clears, and after the task ends the worker clears its own interrupt flag before reuse.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:13-13` · high · sha:f1bf00174456</sub>
- ASYNC-4 (MUST) A worker already returned to its pool MUST NOT receive an interrupt aimed at a completed call.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:13-13` · high · sha:f1bf00174456</sub>
- ASYNC-5 (MUST) If a worker computes a closeable result but the future was already terminated so the value can never be delivered, the adapter MUST close that orphaned value exactly once, and whoever loses the produce/terminate race performs the close.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:14-14` · high · sha:f1bf00174456</sub>
- ASYNC-6 (MUST) Cancellation MUST propagate bidirectionally across each adapter: cancelling the runtime-native primitive (subscription, promise, coroutine/job) MUST cancel the underlying canonical future, and cancelling the canonical future MUST reach the runtime primitive and native transport call.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:15-15` · high · sha:f1bf00174456</sub>
- ASYNC-7 (SHOULD) Each adapter chooses whether its native cancellation maps to interrupt-mode or non-interrupt-mode, which determines whether an in-flight blocking call is aborted, and a port SHOULD preserve and document per adapter whether cancelling through a runtime aborts a blocking transport or lets it run to completion.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:16-16` · high · sha:f1bf00174456</sub>
- ASYNC-8 (SHOULD) Adapters that move work or callbacks onto another thread SHOULD propagate the caller's diagnostic logging context across the hop (capture on the boundary thread, reinstate on the executing/callback thread) so post-hop log events retain correlation.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:20-20` · high · sha:f1bf00174456</sub>
- ASYNC-9 (MUST) When an adapter reinstates a captured logging context, it MUST first save the executing thread's prior context, install the captured context only for the work's duration, and restore the prior context afterward, including when the work throws, so a reused or pooled thread's own context is never clobbered.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:21-21` · high · sha:f1bf00174456</sub>
- ASYNC-10 (MUST) When an adapter propagates logging context, capture MUST occur at the point that identifies the logical caller (per-subscription for cold or reusable stream/promise objects, per-task-submission for executor decorators) and not at object-construction time, so a reused async object picks up the live context of each use.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:22-22` · high · sha:f1bf00174456</sub>
- ASYNC-11 (MUST) When an adapter propagates logging context, capture and restore MUST be safe when no logging-context backend is installed: an absent context captures as empty, and reinstating an empty context clears the target thread's context rather than raising.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:23-23` · high · sha:f1bf00174456</sub>
- ASYNC-12 (MUST) On runtimes where a newly created worker does not inherit the spawning thread's logging context (lightweight threads or plain thread-local contexts), an adapter that propagates logging context MUST explicitly transfer it at the thread-creation boundary, which is distinct from any carrier-hop guarantee the runtime provides.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:24-24` · high · sha:f1bf00174456</sub>
- ASYNC-13 (MUST) When surfacing a failure, adapters MUST unwrap the async framework's wrapper exceptions down to the original cause so typed handlers match the real exception.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:28-28` · high · sha:f1bf00174456</sub>
- ASYNC-13 (MUST) Wrapper-exception unwrapping MUST terminate on the first non-wrapper cause, a null cause, or a detected cycle.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:28-28` · high · sha:f1bf00174456</sub>
- ASYNC-14 (MUST) An async-to-sync blocking bridge MUST honor thread interruption while awaiting by restoring the interrupt flag, cancelling the in-flight future, and throwing an interrupted-I/O failure.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:29-29` · high · sha:f1bf00174456</sub>
- ASYNC-14 (MUST) An async-to-sync blocking bridge MUST unwrap execution-wrapper exceptions so blocking callers see the original failure, and a future cancelled independently MUST surface its cancellation as-is rather than remapped to I/O.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:29-29` · high · sha:f1bf00174456</sub>
- ASYNC-15 (MUST) An adapter that owns an executor or background threads MUST expose a close/dispose operation that is idempotent (repeated calls safe, only the first performs shutdown side-effects).
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:33-33` · high · sha:f1bf00174456</sub>
- ASYNC-15 (MUST) The adapter close/dispose operation MUST be ownership-aware, releasing only SDK-owned resources and never a caller-supplied executor or client.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:33-33` · high · sha:f1bf00174456</sub>
- ASYNC-15 (MUST) The adapter close/dispose operation MUST be interrupt-safe, honoring thread interruption on any blocking shutdown step.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:33-33` · high · sha:f1bf00174456</sub>
- ASYNC-16 (SHOULD) An adapter that owns an executor SHOULD shut it down gracefully on close (stop accepting new work and wait for in-flight tasks rather than interrupting them), escalating to forceful shutdown only if the closing thread is itself interrupted.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:34-34` · high · sha:f1bf00174456</sub>
- ASYNC-17 (SHOULD) The async transport SPI SHOULD provide a no-op default close so lightweight or functional implementations need not implement lifecycle management, while any implementation that owns resources overrides it to follow ASYNC-15.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:35-35` · high · sha:f1bf00174456</sub>
- ASYNC-18 (MUST) The non-blocking scheduled-delay primitive MUST complete after the requested delay without blocking a thread, MUST complete immediately for a zero delay, and MUST reject a negative delay.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:39-39` · high · sha:f1bf00174456</sub>
- ASYNC-18 (MUST) Cancelling the future returned by the scheduled-delay primitive MUST cancel the underlying scheduled task so no scheduler thread is held.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:39-39` · high · sha:f1bf00174456</sub>
- ASYNC-19 (MUST) Every bridge and facade overload that accepts per-call request options MUST thread those options into the wrapped send so per-request overrides survive the async boundary, rather than being dropped by the SPI's options-ignoring default overload.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:40-40` · high · sha:f1bf00174456</sub>
- ASYNC-20 (MUST) Once a Response has been delivered to the caller through the future, cancelling that future MUST NOT close the Response body, and the caller owns closing it even when discarding it.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:41-41` · high · sha:f1bf00174456</sub>
- ASYNC-21 (MUST) An adapter exposing a streaming source (SSE) as a reactive stream MUST honor downstream backpressure by polling the source at most once per unit of demand, and MUST complete on end-of-source.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:42-42` · high · sha:f1bf00174456</sub>
- ASYNC-21 (MUST) A reactive-stream adapter over a streaming source MUST propagate a source exception as an error signal while not swallowing fatal errors.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:42-42` · high · sha:f1bf00174456</sub>
- ASYNC-21 (MUST) A reactive-stream adapter over a streaming source MUST NOT close the caller-owned source on any termination, and MUST treat the source as single-subscriber (a fresh source per subscription).
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:42-42` · high · sha:f1bf00174456</sub>
- XCUT-11 (MUST) Components documented as shared or reusable across concurrent requests (pipeline steps, auth handlers, redactors, factories) MUST be safe for concurrent invocation.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:28-28` · high · sha:d6123be82c9e</sub>
- XCUT-11 (MUST) Per-call mutable state (attempt counters, deadlines, seen-URI sets) MUST live on the call's stack/local state rather than the shared instance, and any shared mutable state MUST be synchronized.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:28-28` · high · sha:d6123be82c9e</sub>
- XCUT-13 (MUST) close()/shutdown() MUST be idempotent (latched so repeats are no-ops) and MUST NOT block on interrupt-sensitive waits, using non-blocking shutdown and preserving the ambient interrupt/cancel flag as-is.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:30-30` · high · sha:d6123be82c9e</sub>
- XCUT-22 (MUST) The SDK MUST close only resources it created, and a caller-supplied (BYO) transport client, executor, or connection pool MUST NOT be closed by the SDK.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:31-31` · high · sha:d6123be82c9e</sub>
- NFR-11 (SHOULD) The core SHOULD be concurrency-model agnostic, exposing plain blocking operations correct on any scheduler and leaking no async-framework types (coroutines, reactive publishers) into the core public surface.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:33-33` · high · sha:5f4684bf7123</sub>
- NFR-11 Shared mutable state is guarded for safe concurrent access, and the synchronization primitives SHOULD avoid pinning or blocking lightweight scheduler threads where the target runtime distinguishes them.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:33-33` · high · sha:5f4684bf7123</sub>
- ASYNC-1: a single-value future is non-null on success or completes exceptionally.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:67-67` · high · sha:0451cc7f3bb4</sub>
- ASYNC-2: construction failures are reported via the failure channel.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:67-67` · high · sha:0451cc7f3bb4</sub>
- ASYNC-3: cancellation has two modes and queued or finished tasks are never interrupted.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68-68` · high · sha:0451cc7f3bb4</sub>
- ASYNC-4: ordered interrupt delivery prevents pooled-thread poisoning under stress.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68-68` · high · sha:0451cc7f3bb4</sub>
- ASYNC-5: an orphaned closeable is closed exactly once on the lost race.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68-68` · high · sha:0451cc7f3bb4</sub>
- ASYNC-6: each adapter supports bidirectional cancellation.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68-68` · high · sha:0451cc7f3bb4</sub>
- ASYNC-7: each adapter documents whether it uses interrupt mode.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:68-68` · high · sha:0451cc7f3bb4</sub>
- ASYNC-8: logging context propagates across hops.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69-69` · high · sha:0451cc7f3bb4</sub>
- ASYNC-9: context is saved, installed and restored, including on throw.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69-69` · high · sha:0451cc7f3bb4</sub>
- ASYNC-10: context is captured per subscription or per submission.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69-69` · high · sha:0451cc7f3bb4</sub>
- ASYNC-11: context propagation is safe with no logging backend.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69-69` · high · sha:0451cc7f3bb4</sub>
- ASYNC-12: on lightweight-thread runtimes, context transfers explicitly at the thread-creation boundary.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:69-69` · high · sha:0451cc7f3bb4</sub>
- ASYNC-13: wrapper-exception unwrapping is cycle-safe.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:70-70` · high · sha:0451cc7f3bb4</sub>
- ASYNC-14: the blocking bridge honors interruption and unwraps.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:70-70` · high · sha:0451cc7f3bb4</sub>
- ASYNC-15: closing an owned executor is idempotent, ownership-aware and interrupt-safe.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:71-71` · high · sha:0451cc7f3bb4</sub>
- ASYNC-16: close shuts the executor down gracefully.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:71-71` · high · sha:0451cc7f3bb4</sub>
- ASYNC-17: functional implementations have a no-op default close.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:71-71` · high · sha:0451cc7f3bb4</sub>
- ASYNC-18: a scheduled delay is non-blocking, zero is immediate, negative is rejected and cancel cancels the task.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72-72` · high · sha:0451cc7f3bb4</sub>
- ASYNC-19: per-call options are threaded through every bridge.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72-72` · high · sha:0451cc7f3bb4</sub>
- ASYNC-20: a delivered Response is not closed on late cancel.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72-72` · high · sha:0451cc7f3bb4</sub>
- ASYNC-21: reactive SSE honors backpressure, does not close the source and is single-subscriber.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72-72` · high · sha:0451cc7f3bb4</sub>
- ASYNC-22: the async transport is concurrent-safe.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:72-72` · high · sha:0451cc7f3bb4</sub>
- The design is async-first, and the sync seam is a real sync path where the transport supports one, never sync-over-async dressed as a seam.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:57-58` · high · sha:d7cea7b15cf3</sub>
- Every library await uses ConfigureAwait(false) (docs/styleguide/csharp/09-concurrency.md rule 9.4), and the async-to-blocking bridge is a documented hazard rather than a free convenience (§3.3).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:93-95` · high · sha:d7cea7b15cf3</sub>
- The SystemNet adapter's Execute must call HttpClient.Send and not ExecuteAsync(...).GetAwaiter().GetResult(), which styleguide rule 9.1 (docs/styleguide/csharp/09-concurrency.md) bans and which deadlocks under a single-threaded SynchronizationContext.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:292-295` · high · sha:da6000c93fc5</sub>
- The request-content adapter must override the synchronous HttpContent.SerializeToStream.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:295-296` · high · sha:da6000c93fc5</sub>
- A transport over a library with no synchronous API implements only IAsyncHttpClient and callers bridge explicitly; the SDK never manufactures a synchronous path by blocking.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:299-300` · high · sha:da6000c93fc5</sub>
- The return type is the non-nullable Task<Response>, so SEAM-16/ASYNC-1/TRANSPORT-23 (MUST NOT complete successfully with a null/absent value) is enforced at compile time by nullable-reference-type warnings being build errors.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:444-446` · high · sha:da6000c93fc5</sub>
- The pipeline's terminal runner asserts the transport's result and converts a null into a failure, because NRT is compile-time only and a transport compiled without annotations or writing return null! can still produce a null success (P7).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:446-448` · high · sha:da6000c93fc5</sub>
- Transport failures (ASYNC-2/TRANSPORT-21) are delivered through the task's failure channel and never thrown synchronously; async methods do this for free, including for argument-validation exceptions.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:451-453` · high · sha:da6000c93fc5</sub>
- No SDK path abandons a Task<Response>: deadlines are enforced by cancelling the token the producer observes, never by WaitAsync or WhenAny around a send.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:476-477` · high · sha:da6000c93fc5</sub>
- Where an SDK component must race sends (hedged requests, pagination prefetch), the loser's task gets a continuation that disposes its result.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:477-478` · high · sha:da6000c93fc5</sub>
- AsAsync(this IHttpClient) must not offload a blocking call to the shared thread pool, per SEAM-18 (no default; a shared global fork/join-style pool is explicitly not acceptable because a blocking call would starve it).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:480-483` · high · sha:da6000c93fc5</sub>
- AsAsync takes a mandatory scheduler, AsAsync(this IHttpClient client, TaskScheduler scheduler), with no zero-argument overload.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:486-487` · high · sha:da6000c93fc5</sub>
- AsAsync passes the options and the token into Execute so cancellation reaches the blocking call cooperatively, and documents per ASYNC-7 that cancellation can abort only as far as the blocking transport honours its token.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:487-489` · high · sha:da6000c93fc5</sub>
- AsBlocking(this IAsyncHttpClient) waits with GetAwaiter().GetResult(), which unwraps the original exception (verified: .Result throws AggregateException, GetAwaiter().GetResult() throws the original InvalidOperationException), satisfying SEAM-18's unwrap rule and ASYNC-13.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:489-492` · high · sha:da6000c93fc5</sub>
- The AsBlocking bridge is documented as a last resort for genuinely synchronous call sites, not a way to get a sync API.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:495-496` · high · sha:da6000c93fc5</sub>
- Both bridges thread RequestOptions through (ASYNC-19).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:496-496` · high · sha:da6000c93fc5</sub>
- The Reactive bridge satisfies ASYNC-21's backpressure clause by construction: IAsyncEnumerable<T> is pull-based, the bridge requests the next element only when the previous OnNext has returned, never eagerly, and never disposes the caller-owned source.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:527-529` · high · sha:da6000c93fc5</sub>
- Each shipped policy implements one private ProcessCoreAsync(request, context, next, bool async) in which every I/O call branches `async ? await next.RunAsync(...) : next.Run(...)`; ProcessAsync calls it with async: true, and Process calls it with async: false and asserts the returned ValueTask is already completed before reading its result.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:273-277` · high · sha:1608fcd4b329</sub>
- Process has a default implementation for third-party policies that is the documented blocking bridge (a virtual Process blocking on ProcessAsync), while shipped policies override it.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:278-281` · high · sha:1608fcd4b329</sub>
- PIPE-33's sync-to-async bridge MUST require a caller-supplied executor with no default, because per SEAM-18 a shared global fork/join-style pool is explicitly not acceptable since a blocking call would starve it.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:307-309` · high · sha:1608fcd4b329</sub>
- The sync seam's Execute(Request) must take a CancellationToken (section 3.2's addition), a precondition of the sync-to-async bridge, because the as-built Execute takes none.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:317-318` · high · sha:1608fcd4b329</sub>
- PIPE-34's async-to-sync bridge blocks with GetAwaiter().GetResult(), which unwraps exceptions (ASYNC-13) and honours the token it threads through.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:318-320` · high · sha:1608fcd4b329</sub>
- A bridge creates nothing, so its Dispose touches nothing and must not dispose the client it wraps (XCUT-22, section 3.7); both as-built bridges dispose the wrapped client, which fails XCUT-22 for a caller-supplied client.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:323-324` · high · sha:1608fcd4b329</sub>
- Tests use FakeTimeProvider from the test-only package Microsoft.Extensions.TimeProvider.Testing; section 8.3 owns the seam and RetryPolicy, SetDatePolicy and AccessTokenCache already consume it.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:111-112` · high · sha:27a0a46f35a8</sub>
- ExecutionContext.SuppressFlow and ThreadPool.UnsafeQueueUserWorkItem are banned in SDK code by section 9.1's banned-API gate.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:116-118` · high · sha:ddf8f695ff61</sub>
- Concurrency is a resource that must be bounded: every fan-out is capped so concurrency never becomes an unbounded resource (root rule 9).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:3-3` · high · sha:7a7bb42c6872</sub>
- Async all the way down: never block on async work with .Result, .Wait() or GetAwaiter().GetResult() (rule 9.1).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:38-38` · high · sha:7a7bb42c6872</sub>
- Any synchronous API that truly must call async code is a documented, reviewed exception, never a casual .Result.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:42-42` · high · sha:7a7bb42c6872</sub>
- Ban async void: async methods return Task, Task<T> or ValueTask (rule 9.2).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:51-51` · high · sha:7a7bb42c6872</sub>
- The single sanctioned async void is a top-level event handler whose signature the framework fixes, and its body must be a thin try/catch delegating to an awaitable method so no exception escapes.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:55-55` · high · sha:7a7bb42c6872</sub>
- Every async method takes a CancellationToken as its last parameter (after any optional defaults) and forwards it into every awaited call (rule 9.3).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:68-71` · high · sha:7a7bb42c6872</sub>
- In tight CPU loops with no awaitable to carry the token, call cancellationToken.ThrowIfCancellationRequested() periodically.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:71-71` · high · sha:7a7bb42c6872</sub>
- A method that accepts a CancellationToken must honour it, because accepting and ignoring it is worse than omitting it.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:71-71` · high · sha:7a7bb42c6872</sub>
- External I/O gets a mandatory timeout, implemented by linking the caller's token with new CancellationTokenSource(TimeSpan.FromSeconds(n)) via CancellationTokenSource.CreateLinkedTokenSource so a hung socket cannot block forever.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:72-72` · high · sha:7a7bb42c6872</sub>
- Link the caller's token with the timeout token rather than replacing it, so both caller cancellation and the timeout fire.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:72-72` · high · sha:7a7bb42c6872</sub>
- Call ConfigureAwait(false) on every await in library code (rule 9.4).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:86-86` · high · sha:7a7bb42c6872</sub>
- The ConfigureAwait split is by layer: library and shared infrastructure code applies it on every await, while application and host code omits it.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:90-90` · high · sha:7a7bb42c6872</sub>
- Apply ConfigureAwait(false) uniformly within a library so the rule is mechanical rather than judged await by await.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:90-90` · high · sha:7a7bb42c6872</sub>
- Use ValueTask/ValueTask<T> only for hot, often-synchronous paths where allocation shows up in a profile, and await it exactly once (rule 9.5).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:101-104` · high · sha:7a7bb42c6872</sub>
- Task/Task<T> is the default return type for async methods because it is simpler and freely composable.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:104-104` · high · sha:7a7bb42c6872</sub>
- A ValueTask must be awaited exactly once, never blocked on, never awaited concurrently and never stored to await later; if any of those is needed, convert it once with .AsTask().
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:105-105` · high · sha:7a7bb42c6872</sub>
- Fan out concurrent work with Task.WhenAll but bound the degree of parallelism, and never fire-and-forget (rule 9.6).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:119-119` · high · sha:7a7bb42c6872</sub>
- Use Parallel.ForEachAsync with MaxDegreeOfParallelism set to a deliberate cap so at most n operations run together, rather than unbounded Task.WhenAll over a large input.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:122-122` · high · sha:7a7bb42c6872</sub>
- Awaiting tasks in a loop runs them serially; the concurrent form is to start them, collect the tasks and await Task.WhenAll, which surfaces every result and propagates the first fault.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:122-122` · high · sha:7a7bb42c6872</sub>
- Never fire-and-forget a task, because its exception goes unobserved and its lifetime outlives the owning scope; a bare _ = DoWork() is forbidden.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:123-123` · high · sha:7a7bb42c6872</sub>
- Work that must outlive the request is handed to an owned, bounded background processor that tracks and awaits it on shutdown.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:123-123` · high · sha:7a7bb42c6872</sub>
- Route producer/consumer handoff through a bounded Channel<T> created with Channel.CreateBounded<T>(capacity), and stream with IAsyncEnumerable<T> (rule 9.7).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:135-138` · high · sha:7a7bb42c6872</sub>
- Never use an unbounded channel or a raw uncapped ConcurrentQueue for cross-task handoff.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:138-138` · high · sha:7a7bb42c6872</sub>
- To stream results as produced, return IAsyncEnumerable<T> consumed with await foreach, carrying the CancellationToken via [EnumeratorCancellation] so cancellation reaches the producer.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:139-139` · high · sha:7a7bb42c6872</sub>
- Prefer immutable data and Interlocked/SemaphoreSlim to lock, and keep any lock tiny and await-free (rule 9.8).
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:154-154` · high · sha:7a7bb42c6872</sub>
- Immutable data (record, readonly) shared across threads needs no lock; a single counter or reference swap uses Interlocked.Increment/CompareExchange; an async critical section uses SemaphoreSlim(1, 1) with WaitAsync.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:157-157` · high · sha:7a7bb42c6872</sub>
- Reach for lock last, only for a small synchronous critical section over multiple fields.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:157-157` · high · sha:7a7bb42c6872</sub>
- Keep every lock scope to the few statements that must be atomic, take locks in a consistent order to avoid deadlock, and document every remaining race with a why-comment explaining why it is benign.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:158-158` · high · sha:7a7bb42c6872</sub>
- Timeouts are mandatory on external I/O via a CancellationToken from CancellationTokenSource(TimeSpan).
  <sub>styleguide · `docs/styleguide/csharp/README.md:67-67` · high · sha:1e6ba36fc337</sub>

## Constraints
- Cancelling an already-completed success future does NOT close the delivered response body, so the caller MUST still close it (SEAM-16).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:17-17` · high · sha:0adae2d6a47f</sub>
- ASYNC-1 (MUST) The async transport contract is a single-value completion future that yields exactly one Response on success or completes with exactly one failure, and on success it MUST deliver a non-null Response.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:7-7` · high · sha:f1bf00174456</sub>
- ASYNC-22 (MUST) Async transport implementations MUST be safe for concurrent calls from multiple threads, with all per-call mutable state confined to the returned future's completion graph.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:43-43` · high · sha:f1bf00174456</sub>
- Under a single-threaded SynchronizationContext (WinForms, WPF, legacy ASP.NET, some test hosts), blocking on a task whose continuation wants that context deadlocks.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:91-93` · high · sha:d7cea7b15cf3</sub>
- HttpClient.Send with a custom HttpContent that overrides only the async serialization method throws NotSupportedException telling the caller to override SerializeToStream (verified).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:295-297` · high · sha:da6000c93fc5</sub>
- Only async methods convert exceptions into a faulted task; a non-async method returning a Task that throws before returning one throws synchronously.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:453-454` · high · sha:da6000c93fc5</sub>
- Settling a task through TaskCompletionSource<Response> reintroduces the orphan race, because TrySetResult returns false when a cancellation callback already called TrySetCanceled (P7).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:465-467` · high · sha:da6000c93fc5</sub>
- Task.WaitAsync(TimeSpan) throws TimeoutException while the producer keeps running, and a disposable result produced later is never disposed (verified), which is the one way a .NET caller can orphan a response and lies outside the future contract.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:472-476` · high · sha:da6000c93fc5</sub>
- The .NET thread pool's starvation heuristic injects threads gradually, so a burst of blocking sends stalls every other async continuation in the process for seconds.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:482-484` · high · sha:da6000c93fc5</sub>
- AsBlocking can deadlock a blocking caller on a UI thread when a third-party async transport does not use ConfigureAwait(false), although core's own awaits all do.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:493-495` · high · sha:da6000c93fc5</sub>
- Verified that .Result on a faulted task throws AggregateException while await and GetAwaiter().GetResult() throw the original exception.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:294-295` · high · sha:1608fcd4b329</sub>
- Thread.Abort throws PlatformNotSupportedException on .NET Core and Thread.Interrupt only wakes a thread in a managed wait, so PIPE-33's interruption clause maps onto CancellationToken: cancelling with interruption passes the call's token into the worker's IHttpClient.Execute (where HttpClient.Send aborts the in-flight socket operation), and cancelling without interruption is the caller awaiting task.WaitAsync(token), which completes as cancelled while the worker runs on.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:312-317` · high · sha:1608fcd4b329</sub>
- ExecutionContext.SuppressFlow() and ThreadPool.UnsafeQueueUserWorkItem each delivered null for a set AsyncLocal (verified), so context propagation depends on SDK code not using them (precondition P7).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:115-117` · high · sha:ddf8f695ff61</sub>
- Awaiting inside a lock is a compile error (CS1996) because the Monitor a lock lowers to is thread-affine and the continuation could resume on another thread.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:158-158` · high · sha:7a7bb42c6872</sub>

## Conclusions
- The asynchronous-runtime concern is handled through a canonical async pivot plus optional out-of-core adapter modules.
  <sub>spec · `docs/product-spec/01-product-overview.md:3-3` · high · sha:4f786c44354d</sub>
- The async-runtime concern is deliberately not a core interface; it is decoupled through a canonical, dependency-free async future type that every ecosystem adapter bridges to and from (SEAM-17).
  <sub>spec · `docs/product-spec/01-product-overview.md:9-9` · high · sha:4f786c44354d</sub>
- The two-execution-model constraint (blocking versus asynchronous I/O) HOLDS on .NET but weakly, since .NET has a real blocking path (HttpClient.Send, Stream.Read, HttpContent.SerializeToStream) and a real asynchronous path (SendAsync, ReadAsync).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:47-50` · high · sha:d7cea7b15cf3</sub>
- The only reason to keep the blocking seam is genuinely synchronous call sites (console tools, synchronous interface implementations, Dispose) that would otherwise be forced into .GetAwaiter().GetResult().
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:55-57` · high · sha:d7cea7b15cf3</sub>
- The async-ecosystem fragmentation constraint DOES NOT HOLD on .NET because Task/Task<T>, ValueTask<T>, CancellationToken, IAsyncEnumerable<T> and IAsyncDisposable ship with the runtime and every framework, library and test runner speaks them.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:59-62` · high · sha:d7cea7b15cf3</sub>
- SEAM-17's canonical pivot on .NET is simply Task<Response> and its per-ecosystem adapter apparatus collapses (P2, P4; §3.3).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:62-64` · high · sha:d7cea7b15cf3</sub>
- An IObservable<T> bridge for pagination and SSE is an optional convenience package, not a seam.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:64-64` · high · sha:d7cea7b15cf3</sub>
- The global-interpreter-lock constraint DOES NOT APPLY on .NET, which has true parallel threads and a JVM-like memory model, with Volatile, Interlocked and immutable publication as the tools.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:96-98` · high · sha:d7cea7b15cf3</sub>
- HttpClient.Send has driven a genuinely synchronous SocketsHttpHandler exchange since .NET 5 (verified against a local server), so the synchronous transport path can be real rather than sync-over-async.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:290-292` · high · sha:da6000c93fc5</sub>
- The async pivot is Task<Response>, reversing the Ruby port's answer of a custom future, because Task<T>, CancellationToken and IAsyncEnumerable<T> are in the runtime and there is no second async ecosystem (ValueTask is a Task optimisation, F# Async/task consume Task, IObservable bridges in one call, UniTask is outside a server SDK's audience).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:414-423` · high · sha:da6000c93fc5</sub>
- SEAM-17 is satisfied verbatim by Task<Response>, and its ecosystem-adapter sentence has nothing to apply to (P4: a split the host does not have is not preserved), matching the Node port's conclusion with Promise.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:423-427` · high · sha:da6000c93fc5</sub>
- The pivot is Task rather than ValueTask because a transport rarely completes synchronously so ValueTask's allocation saving does not apply, ValueTask may be awaited only once (CA2012), cannot go to Task.WhenAll/WhenAny without AsTask(), and is the wrong shape for a value a paginator or hedging caller may observe more than once.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:436-439` · high · sha:da6000c93fc5</sub>
- ASYNC-2 is followed over the Framework Design Guidelines' preference for synchronously thrown usage errors; PIPE-30's normalisation of policies is the same rule one layer up (section 5).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:455-457` · high · sha:da6000c93fc5</sub>
- SEAM-30/ASYNC-5 (orphaned response) is largely structurally free because a Task can be completed only by its producer, so the future-already-cancelled branch does not exist for an async method.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:461-465` · high · sha:da6000c93fc5</sub>
- The one ecosystem bridge worth shipping is for multi-value streams: Dexpace.Sdk.Reactive (section 2.2) exposes pagination's and SSE's IAsyncEnumerable<T> as IObservable<T>.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:524-526` · high · sha:da6000c93fc5</sub>
- In Dexpace.Sdk.Reactive, disposing the subscription cancels the token the enumeration observes and a token cancelled from the SDK side completes the observable with the cancellation (ASYNC-6).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:526-528` · high · sha:da6000c93fc5</sub>
- The Azure SDK's single-implementation idiom was adopted so that nothing blocks on an incomplete task, the retry wait is a genuine blocking wait on the sync path (section 6.1), and there is exactly one body of retry, redirect and auth logic, avoiding the AUTH-31 and RETRY-34 sync/async drift.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:268-278` · high · sha:1608fcd4b329</sub>
- The port's sync-to-async bridge takes a TaskScheduler as the executor, with no overload that defaults it, and a caller wanting dedicated threads passes a scheduler that creates them.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:311-313` · high · sha:1608fcd4b329</sub>
- Interruption-as-token-cancellation and executor-as-scheduler fall under the cancellation mapping argued in section 3.3 and recorded in section 10 entry 8, extended to PIPE-33 and PIPE-34; the residual (P6) is a sync transport that ignores its token, which no bridge on any runtime can pre-empt.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:320-323` · high · sha:1608fcd4b329</sub>
- CFG-15's time seam retires into the runtime's TimeProvider (P2): GetUtcNow() is the wall clock, GetTimestamp()/GetElapsedTime() is the monotonic counter (CFG-16), and Task.Delay(TimeSpan, TimeProvider, CancellationToken) is the non-blocking wait.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:108-111` · high · sha:27a0a46f35a8</sub>
- OBS-24 and ASYNC-8 to ASYNC-12 are nearly free in .NET: AsyncLocal<T> flows through ExecutionContext across every await, Task.Run and thread-pool hop, is copy-on-write (verified: a child's assignment did not leak to the parent), and the thread pool restores the worker's own context afterwards, so ASYNC-9's save/install/restore is the runtime's behaviour.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:111-115` · high · sha:ddf8f695ff61</sub>
- One sanctioned exception to the SuppressFlow ban is the internal helper that launches SDK-owned background work (the token cache's refresh), which suppresses flow on purpose so that the work does not pin the triggering call's context.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:117-119` · high · sha:ddf8f695ff61</sub>
- Blocking on an incomplete Task deadlocks under a synchronization context and, even without one, starves the thread pool, so the fix is making callers async and propagating await up to the entry point.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:41-42` · high · sha:7a7bb42c6872</sub>
- async void is banned because it cannot be awaited, composed, timed out or cancelled, and an exception escaping it is posted to the synchronization context and crashes the process.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:54-54` · high · sha:7a7bb42c6872</sub>
- Library code uses ConfigureAwait(false) because it must not care which synchronization context its caller runs on, and recapturing the context costs a context switch and risks deadlock if the caller later blocks.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:89-89` · high · sha:7a7bb42c6872</sub>
- The single-await rule exists because it makes ValueTask safe to pool internally; breaking it reads a recycled or torn result.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:105-105` · high · sha:7a7bb42c6872</sub>
- A bounded channel is used because WriteAsync awaits when it is full, throttling the producer to the consumer's rate by construction and avoiding an unbounded queue.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:138-138` · high · sha:7a7bb42c6872</sub>

## Reference
- SEAM-11 rationale is that keeping the seam to "send one, get one" is what lets pipelines, retry, auth and logging be built above it; conformance is that a bare send lambda works as a transport and passing options to an options-ignoring transport matches omitting them.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:12-12` · high · sha:0adae2d6a47f</sub>
- SEAM-12 conformance is to fire many concurrent requests through one transport and assert no cross-talk.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:13-13` · high · sha:0adae2d6a47f</sub>
- SEAM-16 rationale is that a null success is a latent dereference at every call site; an implementation returning a null-completed future is non-conformant, and a delivered response still requires explicit close.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:17-17` · high · sha:0adae2d6a47f</sub>
- SEAM-17 rationale is that a lowest-common-denominator pivot lets every async ecosystem interoperate through one point instead of the core depending on any one runtime; conformance is that each adapter translates success, exceptional completion and cancellation of the pivot into its ecosystem's equivalents and back.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:18-18` · high · sha:0adae2d6a47f</sub>
- In the reference implementation the async pivot is java.util.concurrent.CompletableFuture.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:18-18` · high · sha:0adae2d6a47f</sub>
- SEAM-18 conformance is that the async-wrapping bridge has no zero-arg overload, the blocking bridge surfaces the original exception, and interrupting it cancels the future.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:19-19` · high · sha:0adae2d6a47f</sub>
- SEAM-30 rationale is that the caller-closes rule (SEAM-16) cannot apply to a value no caller receives; conformance is to cancel the future before an in-flight send completes, let it produce a response, and assert the response is closed exactly once and never surfaces.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:20-20` · high · sha:0adae2d6a47f</sub>
- SEAM-24 conformance is to set a diagnostic value, run an async request through the adapter, assert a worker log carries it, and cancel from each side asserting the other observes it.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:45-45` · high · sha:0adae2d6a47f</sub>
- The async-runtime adapter contract defines what an adapter must honor when bridging the SDK's async HTTP transport SPI to a host runtime's concurrency primitives, with a single canonical completion future (SEAM-17) carrying exactly one success value or one failure as the interchange point that every ecosystem facade (coroutines, reactive Mono/Flux, event-loop futures, virtual threads) bridges to and from.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:3-3` · high · sha:f1bf00174456</sub>
- Logging-context propagation across threads is an observability guarantee rather than a functional one, so an adapter that omits it still executes exchanges correctly.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:20-20` · high · sha:f1bf00174456</sub>
- Callers that need eager abort of an in-flight request use the interrupt or structured-cancellation path rather than adapter close.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:34-34` · high · sha:f1bf00174456</sub>
- The behavior of executeAsync after the async transport is closed is undefined.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:35-35` · high · sha:f1bf00174456</sub>
- ASYNC-20 is distinct from ASYNC-5 in that ASYNC-5 applies only to a value the future never delivered.
  <sub>spec · `docs/product-spec/18-asynchronous-runtime-adapter-contract.md:41-41` · high · sha:f1bf00174456</sub>
- The caller owns the lifecycle of a caller-supplied client, executor or connection pool and may keep using it after the SDK component built around it is closed.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:31-31` · high · sha:d6123be82c9e</sub>
- NFR-11 conformance is confirming no async-framework type in the core public surface and driving the same core call concurrently from multiple scheduler kinds asserting race-free results.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:33-33` · high · sha:5f4684bf7123</sub>
- The sync/async split is weak because the .NET ecosystem is overwhelmingly async-first, the styleguide forbids sync-over-async outright (docs/styleguide/csharp/09-concurrency.md rule 9.1), and ASP.NET Core's server rejects synchronous body I/O unless AllowSynchronousIO is set (its documented default, not exercised here).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:52-55` · high · sha:d7cea7b15cf3</sub>
- System.Threading.Channels ships in the shared framework, and IObservable<T> is a BCL interface while only the Rx operators live in the System.Reactive package.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:61-63` · high · sha:d7cea7b15cf3</sub>
- XCUT-11, SEAM-12, TRANSPORT-29 and ASYNC-22 read on .NET as they read on the JVM.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:98-98` · high · sha:d7cea7b15cf3</sub>
- SEAM-16 requires the async transport's future to complete with a non-null response (caller owns closing it) or exceptionally with the transport failure, never with a null success, and cancelling an already-succeeded future must not close the delivered response.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:405-408` · high · sha:da6000c93fc5</sub>
- SEAM-17 (a SHOULD) wants the async contract expressed through one canonical dependency-free future pivot with per-ecosystem facades as separate bridging adapters.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:407-410` · high · sha:da6000c93fc5</sub>
- SEAM-30 and ASYNC-5 require the producer to close an orphaned response when the future is already settled; ASYNC-1/ASYNC-2 restate single-value completion and the failure channel; ASYNC-6 requires bidirectional cancellation; ASYNC-20 requires that cancelling a future whose response was already delivered not close it.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:409-412` · high · sha:da6000c93fc5</sub>
- IAsyncHttpClient : IAsyncDisposable declares Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:429-434` · high · sha:da6000c93fc5</sub>
- The residual SEAM-30 cases are producer-internal: an adaptation that throws after the native response is live (TRANSPORT-22, section 3.2) and a policy that supersedes a response (PIPE-40), and they are covered by the same disposal guard.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:469-471` · high · sha:da6000c93fc5</sub>
- The as-built AsAsync calls Task.Run(() => inner.Execute(request), ct) on the shared pool with no scheduler parameter, and the token only prevents the delegate from starting: Task.Run(fn, ct) cancelled mid-run still returns the delegate's result (verified).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:484-486` · high · sha:da6000c93fc5</sub>
- As built, there is no Process method, HttpPipeline.Send is SendAsync(...).AsTask().GetAwaiter().GetResult() over a transport whose Execute is itself ExecuteAsync(...).GetAwaiter().GetResult(), which is sync-over-async twice, forbidden by the house style (09-concurrency 9.1) and burning a pool thread per blocked call.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:269-272` · high · sha:1608fcd4b329</sub>
- The as-built IHttpClient.AsAsync() is Task.Run(() => inner.Execute(request), ct) on the shared ThreadPool, the exact pool the requirement names; hill-climbing thread injection softens but does not remove starvation.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:309-311` · high · sha:1608fcd4b329</sub>
- As built for section 5.3: partial, with async runtime only, a sync path that is double sync-over-async, bridges that default to the shared pool, take no token and dispose what they wrap, HttpPipeline not a transport, and no flatten/nest.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:332-333` · high · sha:1608fcd4b329</sub>
- Rule 9.1 is enforced by VSTHRD002/VSTHRD103 where Microsoft.VisualStudio.Threading.Analyzers is referenced, and review rejects .Result, .Wait() and .GetAwaiter().GetResult().
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:49-49` · high · sha:7a7bb42c6872</sub>
- Rule 9.2 is enforced by review and by VSTHRD100 (avoid async void) where the threading analyzer is referenced.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:66-66` · high · sha:7a7bb42c6872</sub>
- Rule 9.3 is enforced by review, CA2016 (forward CancellationToken to methods that take one), and the mandatory timeout on external I/O from root rule 9.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:84-84` · high · sha:7a7bb42c6872</sub>
- Rule 9.4 is enforced by CA2007 enabled in library projects and suppressed in app/host projects, plus review.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:99-99` · high · sha:7a7bb42c6872</sub>
- Rule 9.5 is enforced by CA2012 (use ValueTasks correctly), and review limits ValueTask to measured hot paths.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:117-117` · high · sha:7a7bb42c6872</sub>
- Rule 9.6 is enforced by CS4014 (unawaited task) promoted to an error, and review requires a bound on every fan-out.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:133-133` · high · sha:7a7bb42c6872</sub>
- Rule 9.7 is enforced by review, which requires bounded channels and token-carrying await foreach and rejects unbounded channels.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:152-152` · high · sha:7a7bb42c6872</sub>
- Rule 9.8 is enforced by CS1996 and by review, which requires tiny lock scopes, consistent lock ordering and a why-comment on every documented race.
  <sub>styleguide · `docs/styleguide/csharp/09-concurrency.md:167-167` · high · sha:7a7bb42c6872</sub>
- Chapter 09 covers async/await throughout, async void banned, CancellationToken plus timeout everywhere, ConfigureAwait(false) in libraries, no .Result/.Wait(), Channel<T>, and bounded parallelism.
  <sub>styleguide · `docs/styleguide/csharp/README.md:41-41` · medium · sha:1e6ba36fc337</sub>

## Conflicts
- **ConfigureAwait(false) in library code (9.4) vs CA2007 dialled down (design §9.4)** — The styleguide requires ConfigureAwait(false) on every await in library code, enforced by CA2007, while the design recorded CA2007 dialled to none with a rationale later shown to be factually wrong; CA2007 is re-enabled at warning (an error under warnings-as-errors) under src/ and relaxed only for tests, tools and the AOT smoke consumer, so the port CONFORMS and no note is owed.
  <sub>styleguide `docs/styleguide/csharp/09-concurrency.md:86-100` · design `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:322-358` · conformed 2026-10-02</sub>

## Superseded

