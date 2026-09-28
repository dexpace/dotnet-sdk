# execution-context

## Rules
- Each execution context promotion must produce a new instance without mutating the source, carrying forward the same instrumentation bundle reference and call key, and adding exactly one artifact — the request when promoting dispatch to request, or the response when promoting request to exchange. (CTX-2)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:8-8` · high · sha:5a9eacfb1c53</sub>
- A directly-constructed (off-chain) execution context without an explicit key must receive a fresh call-unique key using the same uniqueness guarantee as promoted contexts, and default construction must mint globally distinct keys across the whole process and all three context flavors. (CTX-5, CTX-6)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:14-14` · high · sha:5a9eacfb1c53</sub>
- Registration of an execution context in the store must happen at promotion time rather than at head-context construction, so a dispatch context that is never promoted leaves no store entry and its close is a harmless no-op. (CTX-17)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:15-15` · high · sha:5a9eacfb1c53</sub>
- Closing an execution context must evict its store entry conditionally on reference identity, removing the slot only when the current occupant is the closing context itself rather than by value equality, so a stale context cannot evict a structurally-identical live sibling. (CTX-9, CTX-10)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:21-21` · high · sha:5a9eacfb1c53</sub>
- Only the execution context currently occupying the shared store slot (the furthest-reached link in the promotion chain) evicts that slot on close; closing an intermediate link that was already promoted is a no-op. (CTX-9, CTX-10)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:21-21` · high · sha:5a9eacfb1c53</sub>
- Looking up an unknown key in the context store must return an explicit absent result rather than throw, and removing an unknown or already-removed key must be a no-op, making double-close and cleanup-path closes well-defined. (CTX-18)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:22-22` · high · sha:5a9eacfb1c53</sub>
- The context store's cap-draining strategy should be a post-insert drain loop that continues until at or under the cap, rather than a single check-then-evict, so concurrent insert bursts converge to the bound instead of overshooting. (CTX-12, CTX-13)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:27-27` · high · sha:5a9eacfb1c53</sub>
- A context should carry an optional operation name, must carry it forward unchanged across every promotion, and must keep it advisory only, never influencing the request, the dispatch decision, or the store key. (CTX-16, CTX-20)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:32-32` · high · sha:5a9eacfb1c53</sub>

## Constraints
- Promotion between execution context flavors advances only dispatch to request to exchange, with no reverse promotion, and the exchange stage is terminal. (CTX-1)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:7-7` · high · sha:5a9eacfb1c53</sub>
- The entire execution context promotion chain shares one call key — a promotion carries the source context's call key forward verbatim, so all three flavors register under the identical store slot and successive promotions overwrite the same entry. (CTX-3)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:9-9` · high · sha:5a9eacfb1c53</sub>
- Each call's context store key must be unique per call and must not be derived solely from the trace identifier or the trace-plus-span pair, because a disabled-tracing context shares one constant trace id across every untraced call, an inbound distributed trace shares one trace id across many spans, and a tracer may reuse a span id. (CTX-4)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:13-13` · high · sha:5a9eacfb1c53</sub>
- Execution contexts must be immutable and shareable without external synchronization, and the context store must be thread-safe such that contexts with distinct call keys can be registered, overwritten, and removed concurrently without external locking. (CTX-7)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:19-19` · high · sha:5a9eacfb1c53</sub>
- The execution context store must be bounded, enforcing a maximum tracked-entry count and draining back to at or below that cap after each insert, so a caller who fails to close a context on an exception path leaks at most the cap's worth of entries. (CTX-11, CTX-19)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:26-26` · high · sha:5a9eacfb1c53</sub>
- The execution context store must keep the referenced request-response graph reachable while a context stays registered; implementations must not hold contexts by weak or soft references, and must treat the bounded cap, not garbage collection, as the leak backstop. (CTX-11, CTX-19)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:26-26` · high · sha:5a9eacfb1c53</sub>
- Eviction victim selection in the bounded context store is arbitrary — the store guarantees no ordering and no survival of any particular entry, including the just-inserted one, only that the live set is at or below the cap once inserts quiesce. (CTX-12, CTX-13)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:27-27` · high · sha:5a9eacfb1c53</sub>
- Because the disabled-tracing instrumentation bundle shares constant identifiers across every untraced call, the call-key derivation must remain call-unique even when every bundle field is identical across contexts. (CTX-14, CTX-15, CTX-4)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:31-31` · high · sha:5a9eacfb1c53</sub>

## Conclusions
- Because the call key participates in value-equality, two default-constructed execution contexts with otherwise identical fields are not equal, so callers needing value-equality must be able to pin an explicit shared key. (CTX-5, CTX-6)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:14-14` · high · sha:5a9eacfb1c53</sub>

## Reference
- The execution context model defines three context flavors — a dispatch stage (before any request), a request stage (an outgoing request assembled), and an exchange stage (a response arrived) — forming a one-way promotion chain mirroring the call lifecycle. (CTX-1)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:7-7` · high · sha:5a9eacfb1c53</sub>
- The operation name is introduced at the request stage as an argument to the dispatch-to-request promotion, and is carried forward unchanged through promotion to the exchange stage. (CTX-2)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:8-8` · high · sha:5a9eacfb1c53</sub>
- The reference implementation derives the default context store key by appending a process-wide monotonic counter to a "traceId:spanId" rendering. (CTX-4)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:13-13` · high · sha:5a9eacfb1c53</sub>
- The context store must support an unconditional overwrite operation (install-or-replace, never throwing) used by promotion, and a reject-on-duplicate insert operation (install only if absent) that admits exactly one winner under concurrency and fails all others with an error naming the key. (CTX-8)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:20-20` · high · sha:5a9eacfb1c53</sub>
- Each execution context must carry a correlation/instrumentation bundle exposing at minimum a trace id, a span id, trace flags, trace state, a trace-id encoding flavor, validity and remoteness flags, an active span, and a per-operation tracer factory, compatible with W3C Trace Context for cross-service propagation. (CTX-14, CTX-15, CTX-4)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:31-31` · high · sha:5a9eacfb1c53</sub>
- A disabled-tracing (no-op) instrumentation bundle is the default, using reserved invalid sentinels (all-zero trace id, all-zero span id, zero flags, empty state), isValid false, isRemote false, a no-op span, and a no-op tracer factory. (CTX-14, CTX-15, CTX-4)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:31-31` · high · sha:5a9eacfb1c53</sub>
- The per-operation tracer factory should default to a no-op implementation emitting nothing so untraced call sites pay zero tracing cost, and its factory method must be safe to invoke concurrently. (CTX-16, CTX-20)
  <sub>spec · `docs/product-spec/07-execution-context-model.md:32-32` · high · sha:5a9eacfb1c53</sub>

## Conflicts

## Superseded
