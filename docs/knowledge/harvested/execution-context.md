# execution-context

## Rules
- CTX-1 (MUST): The model MUST provide three context flavors forming a one-way promotion chain mirroring the call lifecycle: a dispatch stage (before any request), a request stage (an outgoing request assembled), and an exchange stage (a response arrived).
  <sub>spec · `docs/product-spec/07-execution-context-model.md:7-7` · high · sha:5a9eacfb1c53</sub>
- CTX-2 (MUST): Each context promotion MUST be additive and non-mutating, producing a NEW instance and never modifying the source.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:8-8` · high · sha:5a9eacfb1c53</sub>
- CTX-2 (MUST): Each promotion MUST carry forward the same instrumentation bundle reference and the same call key (and, for request-to-exchange, the request and operation name), and add exactly one artifact: the request when promoting dispatch to request, the response when promoting request to exchange.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:8-8` · high · sha:5a9eacfb1c53</sub>
- CTX-3 (MUST): The whole context chain MUST share ONE call key, with each promotion carrying the source's call key forward verbatim, so all three flavors register under the identical store slot and successive promotions overwrite one entry.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:9-9` · high · sha:5a9eacfb1c53</sub>
- CTX-4 (MUST): Each call's store key MUST be unique per call and MUST NOT be derived from the trace identifier, or the trace-plus-span pair, alone, so two concurrent calls sharing a trace id (and even a span id) receive distinct keys and never evict each other.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:13-13` · high · sha:5a9eacfb1c53</sub>
- CTX-5 / CTX-6 (MUST): A directly-constructed (off-chain) context without an explicit key MUST receive a fresh call-unique key with the same uniqueness guarantee, and default construction MUST mint globally distinct keys across the whole process and all three flavors.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:14-14` · high · sha:5a9eacfb1c53</sub>
- CTX-5 / CTX-6 (MUST): Callers needing value-equality of contexts MUST be able to pin an explicit shared key.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:14-14` · high · sha:5a9eacfb1c53</sub>
- CTX-17 (MUST): Context registration MUST happen at promotion time, not head-context construction, so constructing the initial dispatch context MUST NOT auto-register it.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:15-15` · high · sha:5a9eacfb1c53</sub>
- CTX-7 (MUST): Contexts MUST be immutable and shareable without external synchronization.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:19-19` · high · sha:5a9eacfb1c53</sub>
- CTX-7 (MUST): The context store MUST be thread-safe such that contexts with distinct call keys can be registered, overwritten and removed concurrently without external locking.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:19-19` · high · sha:5a9eacfb1c53</sub>
- CTX-8 (MUST): The context store MUST support unconditional overwrite (install-or-replace, never throwing), which promotion uses.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:20-20` · high · sha:5a9eacfb1c53</sub>
- CTX-8 (MUST): The context store MUST support a reject-on-duplicate insert (install only if absent) that admits exactly one winner under concurrency and fails all others with an error naming the key.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:20-20` · high · sha:5a9eacfb1c53</sub>
- CTX-9 (MUST): Closing a context MUST evict the store entry conditionally on reference identity, removing the slot only when the current occupant IS the closing context and never by value equality.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:21-21` · high · sha:5a9eacfb1c53</sub>
- CTX-9 (MUST): Removing a non-existent or already-replaced context-store slot MUST be a well-defined no-op.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:21-21` · high · sha:5a9eacfb1c53</sub>
- CTX-10 (MUST): Only the context currently occupying the shared store slot (the furthest-reached link) evicts on close, and closing an intermediate link that was promoted is a no-op.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:21-21` · high · sha:5a9eacfb1c53</sub>
- CTX-18 (MUST): Looking up an unknown key in the context store MUST return an explicit absent result and never throw.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:22-22` · high · sha:5a9eacfb1c53</sub>
- CTX-18 (MUST): Removing an unknown or already-removed key MUST be a no-op, so double-close and cleanup-path closes are well-defined.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:22-22` · high · sha:5a9eacfb1c53</sub>
- CTX-11 (MUST): The context store MUST be bounded, enforcing a maximum number of tracked entries and draining back to at or below that cap after each insert, as a backstop so a caller who fails to close a context on an exception path leaks at most the cap's worth of entries.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:26-26` · high · sha:5a9eacfb1c53</sub>
- CTX-19 (MUST): The context store MUST keep the request-plus-response graph reachable while the context stays in the store, and reimplementations MUST NOT hold contexts by weak or soft references, since that would let an in-flight context be collected mid-call.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:26-26` · high · sha:5a9eacfb1c53</sub>
- CTX-19 (MUST): Reimplementations MUST treat the bounded cap, not garbage collection, as the leak backstop for contexts.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:26-26` · high · sha:5a9eacfb1c53</sub>
- CTX-12 (SHOULD): The context-store cap-draining strategy SHOULD be a post-insert drain loop (drain until at or under the cap) rather than a single check-then-evict, so concurrent insert bursts converge to the bound instead of overshooting.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:27-27` · high · sha:5a9eacfb1c53</sub>
- CTX-13 (MAY): Context-store eviction victim selection is arbitrary; the store provides NO ordering and NO guarantee that any particular entry survives an insert that trips the cap, including the just-inserted entry.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:27-27` · high · sha:5a9eacfb1c53</sub>
- CTX-14 (MUST): Each context MUST carry a correlation/instrumentation bundle exposing at minimum a trace id, a span id, trace flags, trace state, a trace-id encoding flavor, validity and remoteness flags, an active span, and a per-operation tracer factory.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:31-31` · high · sha:5a9eacfb1c53</sub>
- CTX-14 (MUST): The instrumentation bundle MUST be W3C Trace Context compatible for cross-service propagation.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:31-31` · high · sha:5a9eacfb1c53</sub>
- CTX-15 (MUST): A disabled-tracing / no-op instrumentation bundle MUST be available as the default, with reserved invalid sentinels (all-zero trace id, all-zero span id, zero flags, empty state), isValid false, isRemote false, a no-op span and a no-op tracer factory.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:31-31` · high · sha:5a9eacfb1c53</sub>
- CTX-15 (MUST): Because the no-op bundle shares constant identifiers across every untraced call, call-key derivation (CTX-4) MUST remain call-unique even when every bundle field is identical.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:31-31` · high · sha:5a9eacfb1c53</sub>
- CTX-16 (SHOULD): A context SHOULD carry an optional operation name (a schema-defined operation id, or absent) and MUST carry it forward unchanged across every promotion.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:32-32` · high · sha:5a9eacfb1c53</sub>
- CTX-16 (MUST): The operation name MUST be advisory only: exposed to the tracing seam to label the operation, and never influencing the request, dispatch decision or store key.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:32-32` · high · sha:5a9eacfb1c53</sub>
- CTX-20 (SHOULD): The per-operation tracer factory SHOULD default to a no-op emitting nothing, so untraced call sites pay zero tracing cost.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:32-32` · high · sha:5a9eacfb1c53</sub>
- CTX-20 (MUST): The per-operation tracer factory's factory method MUST be safe to invoke concurrently.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:32-32` · high · sha:5a9eacfb1c53</sub>
- The port treats ambient state as read-mostly and threads per-call state explicitly (§5).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:89-90` · high · sha:d7cea7b15cf3</sub>
- The correlation context travels explicitly on PipelineContext, and Activity.Current is the only ambient value because it is the runtime's own and every OpenTelemetry collector reads it.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:353-355` · high · sha:1608fcd4b329</sub>
- Background work the SDK starts runs under ExecutionContext.SuppressFlow(), which was verified to leave a started task observing no AsyncLocal value.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:355-357` · high · sha:1608fcd4b329</sub>
- Each Promote returns a new instance carrying the same key and bundle and adding exactly one artifact (CTX-2), and because the contexts are records each source instance is unchanged.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:361-363` · high · sha:1608fcd4b329</sub>
- The operation name is introduced at the dispatch-to-request promotion, carried forward, and reaches only the tracing seam (CTX-16).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:363-363` · high · sha:1608fcd4b329</sub>
- The SystemNet transport stamps the CallKey into HttpRequestMessage.Options under a public HttpRequestOptionsKey<CallKey>, and a handler resolves the live call through DexpaceCallContexts.TryGet(key, out context), the explicit-absent lookup of CTX-18.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:379-381` · high · sha:1608fcd4b329</sub>
- Context registration in the store happens at promotion, never at dispatch construction (CTX-17).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:384-385` · high · sha:1608fcd4b329</sub>
- CTX-9 requires store eviction conditionally on reference identity and never by value equality.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:387-388` · high · sha:1608fcd4b329</sub>
- Only the furthest-reached link of the promotion chain occupies the store slot, so closing a promoted intermediate context is a no-op (CTX-10).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:394-395` · high · sha:1608fcd4b329</sub>
- Disposing the Response closes its ExchangeContext, and the pipeline closes a failed call's context before the exception propagates.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:395-396` · high · sha:1608fcd4b329</sub>
- After each store insert a drain loop evicts arbitrary victims until the live count is at or under the cap (CTX-11, CTX-12, CTX-13), and the bound is a backstop.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:398-400` · high · sha:1608fcd4b329</sub>

## Constraints
- CTX-1 (MUST): Context promotion advances dispatch to request to exchange only; there is no reverse promotion and the exchange stage is terminal.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:7-7` · high · sha:5a9eacfb1c53</sub>
- Because the call key participates in value-equality, two default-constructed contexts with otherwise identical fields are NOT equal.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:14-14` · high · sha:5a9eacfb1c53</sub>
- The first context-store entry is installed by the first promotion, so a dispatch context that is never promoted leaves no store entry and its close is a harmless no-op.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:15-15` · high · sha:5a9eacfb1c53</sub>
- The only context-store retention guarantee is that after inserts quiesce the live set is at or below the cap, and a port MUST NOT rely on any specific entry surviving.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:27-27` · high · sha:5a9eacfb1c53</sub>
- Ambient ExecutionContext state leaks into places that outlive the call (timers, cached tasks), and a context the SDK sets is visible to the caller's continuation only if set before the first await.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:88-90` · high · sha:d7cea7b15cf3</sub>
- Because the call key participates in the context records' equality, two default-constructed contexts are unequal unless a caller pins an explicit key (CTX-5, CTX-6).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:373-374` · high · sha:1608fcd4b329</sub>
- ConcurrentDictionary.TryRemove(KeyValuePair<TKey,TValue>) compares values with EqualityComparer<TValue>.Default, which is value equality for a record, so removing a distinct but value-equal stale record evicts the live one (verified on .NET 10.0.401), the failure CTX-9's rationale describes.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:388-393` · high · sha:1608fcd4b329</sub>

## Conclusions
- A single in-flight call's correlation state is modeled as a one-way promotion chain of three immutable context flavors, each carrying a shared instrumentation bundle and a single call-unique key, and each registered in a bounded process-wide store keyed by that call key.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:3-3` · high · sha:5a9eacfb1c53</sub>
- Call keys are not derived from trace ids because trace ids are not call-unique: a disabled-tracing context shares one constant trace id across every untraced call, an inbound distributed trace shares one across many spans, and a tracer may reuse a span id.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:13-13` · high · sha:5a9eacfb1c53</sub>
- Context-store eviction uses reference identity because contexts are value-equal, so a value-equality remove could let a stale context evict a structurally-identical live sibling.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:21-21` · high · sha:5a9eacfb1c53</sub>
- The context store is bounded because a registered context strongly pins the full request-plus-response graph, possibly an unread body holding a connection.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:26-26` · high · sha:5a9eacfb1c53</sub>
- The ambient-execution-context constraint is NEW on .NET and cuts both ways, because ExecutionContext carries every AsyncLocal<T> (including Activity.Current) across await, Task.Run and thread-pool hand-offs automatically, making SEAM-24/ASYNC-8 through ASYNC-12's diagnostic-context propagation largely free (§8.1).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:85-88` · high · sha:d7cea7b15cf3</sub>
- The execution context keeps the explicit-context half of the earlier PR #4 answer (a context value threaded through the pipeline, no ambient SDK state) but overturns its mutability for the correlation context and overturns its "no ContextStore" decision, because CTX-7 to CTX-13 and CTX-17 to CTX-19 are MUSTs and the store has real .NET substance.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:337-342` · high · sha:1608fcd4b329</sub>
- The SDK does not use an SDK-owned AsyncLocal to hold the promotion chain, because AsyncLocal flow is copy-on-write per async method, so a value set inside an awaited async callee is not visible to its caller while a value set in a synchronous callee is, making a promotion's visibility timing-dependent.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:344-350` · high · sha:1608fcd4b329</sub>
- The SDK does not use an SDK-owned AsyncLocal for the promotion chain because AsyncLocal flow is capture-by-default, so a background task such as the token cache refresh that outlives its call would pin the context and the request and response graph that CTX-19 says must be released.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:350-355` · high · sha:1608fcd4b329</sub>
- The promotion chain (CTX-1 to CTX-3) is three sealed records, DispatchContext, RequestContext and ExchangeContext, rather than one type with a stage field, so that CTX-1's "the exchange type exposes no method promoting back" is satisfied by the absence of a method.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:359-362` · high · sha:1608fcd4b329</sub>
- The instrumentation bundle is System.Diagnostics.ActivityContext, which matches CTX-14's W3C-compatible fields (trace id, span id, flags, state, validity and remoteness flags) field for field, and the no-op bundle is its default value.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:365-367` · high · sha:1608fcd4b329</sub>
- CTX-20's zero-cost no-op tracer factory is ActivitySource.StartActivity returning null when no listener is registered, which the as-built InstrumentationPolicy already relies on.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:368-370` · high · sha:1608fcd4b329</sub>
- The call key (CTX-4) is a readonly record struct CallKey(ActivityTraceId TraceId, ActivitySpanId SpanId, long Sequence) with the sequence taken from Interlocked.Increment on a process-wide counter, making it call-unique under the shared all-zero sentinel and allocating no string unless rendered.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:370-373` · high · sha:1608fcd4b329</sub>
- The specification never says who reads the context store (design section 11 item 30), and the .NET reader is the enterprise DelegatingHandler chain that the connection-layer split places underneath the transport, which sees only an HttpRequestMessage.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:376-379` · high · sha:1608fcd4b329</sub>
- The transport carries the CallKey rather than the context in HttpRequestMessage so that the request and response graph stays out of HttpRequestMessage, which handlers routinely retain in logs.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:382-383` · high · sha:1608fcd4b329</sub>
- The store holds each context in a ContextSlot, a sealed class with no Equals override, so the TryRemove(KeyValuePair) overload compares slot references and satisfies CTX-9's reference-identity eviction.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:392-394` · high · sha:1608fcd4b329</sub>
- The store's live count is an Interlocked counter maintained beside the dictionary, because ConcurrentDictionary.Count acquires every bucket lock and would serialise the hot path.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:401-402` · high · sha:1608fcd4b329</sub>

## Reference
- Conformance for CTX-1: each stage exposes exactly its expected artifacts, and the exchange type exposes no method promoting back.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:7-7` · high · sha:5a9eacfb1c53</sub>
- The operation name is introduced at the request stage as an argument to the dispatch-to-request promotion; conformance: promote and assert the carried-forward fields are identical instances and each source is unchanged.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:8-8` · high · sha:5a9eacfb1c53</sub>
- Conformance for CTX-4: two contexts with identical trace and span id have differing keys and both register; the reference implementation's default key appends a process-wide monotonic counter to a traceId:spanId rendering.
  <sub>spec · `docs/product-spec/07-execution-context-model.md:13-13` · high · sha:5a9eacfb1c53</sub>
- The AsyncLocal flow findings (copy-on-write per async method, capture-by-default) were verified on .NET 10.0.401.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:346-348` · high · sha:1608fcd4b329</sub>
- CTX-15's reserved sentinels are default(ActivityContext), whose trace id is 32 zeros, span id 16 zeros, flags None, state empty and IsRemote false (verified).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:367-368` · high · sha:1608fcd4b329</sub>
- The context store is a process-wide ConcurrentDictionary<CallKey, ContextSlot>, with TryAdd as CTX-8's single-winner insert and the indexer as its unconditional overwrite.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:383-384` · high · sha:1608fcd4b329</sub>
- As built at d45e64b the promotion chain, call key and store are not built; correlation is Activity.Current plus a mutable PipelineContext with an untyped string-keyed property bag.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:407-408` · high · sha:1608fcd4b329</sub>

## Conflicts

## Superseded

