# resource-management

## Rules
- SEAM-14 (MUST) - Both transport seams MUST be closeable, and close MUST be idempotent, ownership-aware (only resources the transport itself created are released, and a caller-supplied client/executor is NEVER touched) and interrupt-safe.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:43-43` · high · sha:0adae2d6a47f</sub>
- A lightweight transport MAY have a no-op close (SEAM-14).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:43-43` · high · sha:0adae2d6a47f</sub>
- SEAM-25 (MUST) - An async-runtime adapter that OWNS an executor MUST implement close as an idempotent, ownership-aware release in which only the first close shuts the owned executor and emits the lifecycle event.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:44-44` · high · sha:0adae2d6a47f</sub>
- Closing an async-runtime adapter MUST NOT be required to cancel in-flight requests (a graceful drain is acceptable), and an adapter built over a caller-supplied executor MUST NOT shut it down (SEAM-25).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:44-44` · high · sha:0adae2d6a47f</sub>
- XCUT-14 (MUST) Every process- or instance-lived map whose key space is influenced by callers or servers (context registries, per-nonce counters, similar caches) MUST be bounded by a hard cap.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:36-36` · high · sha:d6123be82c9e</sub>
- XCUT-14 (MUST) A bounded caller/server-keyed map MUST drain back under its cap after each insert using a loop rather than a single pre-insert check-then-evict, so a concurrent insert burst converges to the bound.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:36-36` · high · sha:d6123be82c9e</sub>
- A consumer downstream of the owner of a response must not close it, expressed in .NET through the leaveOpen parameter.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:117-119` · high · sha:da6000c93fc5</sub>
- Every core wrapper over a stream core did not open is constructed with leaveOpen: true (or its library-specific equivalent), as SEAM-21 and SERDE-3 require of the codec.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:120-122` · high · sha:da6000c93fc5</sub>
- At the I/O-helper layer, wrapping takes ownership unless told otherwise, as the BCL's own wrappers do (IO-6), so an internal line reader or tee built over a stream core opened disposes it.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:152-154` · high · sha:da6000c93fc5</sub>
- At the body layer a body closes exactly the sources it opened itself: a file-backed body opens and disposes a fresh FileStream per write (BODY-11), and RequestBody.FromStream(stream) never disposes the caller's stream.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:154-156` · high · sha:da6000c93fc5</sub>
- ResponseBody.FromStream is the one factory that takes ownership of the stream, because its callers are transports handing over a stream they opened for that purpose and the response is the object the caller disposes.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:156-158` · high · sha:da6000c93fc5</sub>
- Disposing the SDK Response disposes the underlying HttpResponseMessage, returning the connection to the pool.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:303-304` · high · sha:da6000c93fc5</sub>
- The transport MUST close the native response before a throwable propagates (TRANSPORT-22).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:369-371` · high · sha:da6000c93fc5</sub>
- SEAM-14's close contract on the transport seam is specified in design section 3.7 with the rest of the lifecycle rules.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:391-392` · high · sha:da6000c93fc5</sub>
- The SystemNet adapter disposes an HttpClient it constructed and never disposes a borrowed one.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:392-393` · high · sha:da6000c93fc5</sub>
- Every TaskCompletionSource<Response>.TrySetResult (or TrySetResult on any disposable) whose return value is false disposes the value through section 3.7's quiet-dispose helper.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:467-469` · high · sha:da6000c93fc5</sub>
- Anything the SDK can release implements IDisposable, and additionally IAsyncDisposable when release can be asynchronous; a type holding an async-releasable resource implements both and its Dispose does the synchronous release rather than blocking on the asynchronous one (styleguide 13.1, 13.5).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:746-749` · high · sha:da6000c93fc5</sub>
- Close must be idempotent and latched so repeat calls are no-ops (XCUT-13), on both transport seams (SEAM-14), on any adapter owning a pool (SEAM-25), and on Response, whose close MUST be idempotent and forward to the body (HTTP-43), which delegates to the body's idempotent close (HTTP-41, BODY-15).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:755-758` · high · sha:da6000c93fc5</sub>
- A release that throws still leaves the latch flipped so no second release is attempted (BODY-27), and the failure propagates once.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:760-761` · high · sha:da6000c93fc5</sub>
- A caller-supplied (bring-your-own) transport client, executor, or connection pool MUST NOT be closed or shut down by the SDK, so the SDK disposes exactly what the SDK created (XCUT-22, SEAM-14, SEAM-25, ASYNC-15, TRANSPORT-15).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:767-769` · high · sha:da6000c93fc5</sub>
- An HttpClient obtained from IHttpClientFactory is always borrowed, and the stream-level form of the ownership rule is the leaveOpen convention of section 3.1.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:772-774` · high · sha:da6000c93fc5</sub>
- Dispose never waits for in-flight calls, never sleeps and never joins; XCUT-13's interrupt-flag preservation reduces to its non-blocking half because .NET has no interrupt flag and a CancellationToken cannot be un-cancelled.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:777-781` · high · sha:da6000c93fc5</sub>
- A dispose failure on a cleanup path must never replace the failure that caused the cleanup.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:785-787` · high · sha:da6000c93fc5</sub>
- DisposeQuietly is null-safe (CFG-21), catches only non-fatal exceptions, and handles each failure in exactly one of two ways: attached to the primary exception via ExceptionTrail.AddSuppressed when a primary is in flight, or reported through diagnostics (a Warning log and an exception event on the current SDK Activity) when none is.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:790-794` · high · sha:da6000c93fc5</sub>
- Quiet disposal never throws over a primary exception and never swallows silently, because a swallowed dispose failure is a leaked connection nobody can diagnose.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:794-795` · high · sha:da6000c93fc5</sub>
- An explicit Dispose by the caller propagates its failure (SSE-30), and a release throwing inside the latched dispose propagates once (BODY-27); these are the only loud exceptions to quiet disposal.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:795-798` · high · sha:da6000c93fc5</sub>
- The TaskCompletionSource losers, CFG-21's discard close, TRANSPORT-22's adaptation-failure close, the superseded-response closes of PIPE-40 and REDIR-22, RETRY-35's pre-wait release, and the drop paths of PAGE-12 and PAGE-27 all go through DisposeQuietly.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:798-800` · high · sha:da6000c93fc5</sub>
- Never block in Dispose; expose IAsyncDisposable and implement DisposeAsync when cleanup is asynchronous, so callers use `await using` (styleguide 13.5).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:106-123` · high · sha:ce7621949da8</sub>
- A type implementing both IDisposable and IAsyncDisposable must have its synchronous Dispose avoid the async work rather than smuggle it in behind a blocking .Result or .Wait().
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:110-110` · high · sha:ce7621949da8</sub>
- Rent transient buffers from ArrayPool<T>.Shared (or MemoryPool<T>) instead of allocating per call, and return every rented buffer in a finally block so the return happens across exceptions and early exits (styleguide 13.6).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:126-145` · high · sha:ce7621949da8</sub>
- A rented buffer size must have a fixed upper bound and never be the caller's untrusted length; rent a capped chunk and loop.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:130-130` · high · sha:ce7621949da8</sub>
- Clear a pooled buffer (Return with clearArray: true) before returning it when it held sensitive data, because pooled buffers come back dirty with the previous tenant's bytes.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:130-142` · high · sha:ce7621949da8</sub>
- Bound every pool, channel, cache and queue with an explicit cap, such as a BoundedChannelOptions with a capacity and FullMode, a size- or time-capped cache, or an ObjectPool<T> with a maximum retained count (styleguide 13.7).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:147-161` · high · sha:ce7621949da8</sub>
- Reuse HttpClient through IHttpClientFactory and never construct `new HttpClient()` per call in application code (styleguide 13.8).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:163-171` · high · sha:ce7621949da8</sub>
- Dispose every CancellationTokenSource, scoping it with `using`, because a long-running or timeout-created source holds timer and linked-token callback registrations until disposed.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:167-167` · high · sha:ce7621949da8</sub>
- Implement IDisposable for any type owning disposables and IAsyncDisposable when cleanup is itself asynchronous, and prefer `using` / `await using` to hand-written try/finally (styleguide 13.1).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:38-50` · high · sha:ce7621949da8</sub>
- Drop to explicit try/finally for disposal only when the lifetime genuinely cannot match a lexical scope, and state why.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:42-42` · high · sha:ce7621949da8</sub>
- Scope a disposable with a `using` declaration (`using var x = ...;`) when it lives to the end of the method, and use the explicit `using` statement block when the lifetime must end earlier or several disposables nest (styleguide 13.2).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:52-69` · high · sha:ce7621949da8</sub>
- Implement the full dispose pattern (protected virtual Dispose(bool), a finalizer, GC.SuppressFinalize) only when a type directly holds a raw unmanaged resource, preferring SafeHandle (styleguide 13.3).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:71-85` · high · sha:ce7621949da8</sub>
- A sealed class owning only managed disposables writes a plain public Dispose() or DisposeAsync() that disposes its fields, with no finalizer, no Dispose(bool) and no SuppressFinalize.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:75-84` · high · sha:ce7621949da8</sub>
- Dispose what you create and never dispose a dependency injected into you; a field or local your code constructs is yours to dispose, a constructor-injected one is owned by its creator (styleguide 13.4).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:87-104` · high · sha:ce7621949da8</sub>

## Constraints
- new StreamReader(stream) closes the caller's stream when the reader is disposed, and only new StreamReader(stream, leaveOpen: true) does not (verified; the P13 trap).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:118-120` · high · sha:da6000c93fc5</sub>
- Dispose is synchronous, so truly async cleanup (flushing a network buffer, committing a transaction) can run inside it only by blocking with .Result or .Wait(), which is the sync-over-async deadlock banned by chapter 09.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:109-109` · high · sha:ce7621949da8</sub>

## Conclusions
- BODY-8 requires the stream-ownership/close rule to be decided deliberately, and making it one uniform way for all request bodies resolves the reference's admitted per-variant inconsistency, recorded as design section 10 entry 5.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:158-161` · high · sha:da6000c93fc5</sub>
- The adapter throws ObjectDisposedException after dispose in both owned and borrowed cases (SEAM-15's option) rather than inheriting the inner client's behaviour (verified: an owned disposed HttpClient throws ObjectDisposedException, a borrowed one keeps working).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:393-395` · high · sha:da6000c93fc5</sub>
- The full dispose pattern with a finalizer is used only where an unmanaged handle is held directly, which in this SDK is nowhere (styleguide rule 13.3), so the as-built GC.SuppressFinalize calls are harmless noise.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:752-753` · high · sha:da6000c93fc5</sub>
- The single idempotence mechanism is the latch `if (Interlocked.Exchange(ref _disposed, 1) != 0) return;` followed by the release; whoever flips the latch runs the release and everyone else returns.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:758-760` · high · sha:da6000c93fc5</sub>
- An Interlocked latch holds nothing across the release, avoiding the Ruby port's problem of a fiber-owned mutex held across a suspension deadlocking.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:761-763` · high · sha:da6000c93fc5</sub>
- Ownership is a construction-time fact stored in a readonly bool: the parameterless SystemNetHttpClient() builds and owns its HttpClient, SystemNetHttpClient(HttpClient) borrows and never disposes it (styleguide 13.4), so ownership is not re-decided differently at each dispose site.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:769-775` · high · sha:da6000c93fc5</sub>
- SEAM-15 is a MAY that the port takes explicitly: a send after dispose throws ObjectDisposedException (via ObjectDisposedException.ThrowIf), documented rather than left to the inner client.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:781-783` · high · sha:da6000c93fc5</sub>
- Suppressed cleanup failures are handled by one internal helper Disposal.DisposeQuietly(IDisposable? resource, Exception? primary = null) with an async twin rather than AggregateException, which would change the thrown type and break catch (HttpResponseException); .NET exceptions have no suppressed list unlike the JVM and Ruby ports.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:786-790` · high · sha:da6000c93fc5</sub>
- The context store shares one BoundedMap helper with the Digest nonce counter (design section 6.3) and every other caller- or server-keyed map (XCUT-14).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:400-400` · high · sha:1608fcd4b329</sub>
- Bounding is mandated because an unbounded Channel<T> or cache grows until the process dies, and an explicit cap forces an early decision (block the producer, drop the oldest, evict the least-used) instead of deferring it to the out-of-memory killer; bounding is the difference between backpressure and a crash.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:150-151` · high · sha:ce7621949da8</sub>
- Per-call HttpClient construction is rejected because each instance holds its own connection pool and disposing it leaves sockets in TIME_WAIT, exhausting the ephemeral port range under load, whereas IHttpClientFactory pools and rotates handlers for connection reuse and periodic DNS refresh.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:166-166` · high · sha:ce7621949da8</sub>
- The full dispose pattern is rejected by default because adding it by reflex pays the finalization cost (slower allocation, a trip through the finalizer queue) for a guarantee that managed-only types do not need.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:75-75` · high · sha:ce7621949da8</sub>
- Disposal follows ownership and ownership follows creation, so disposing an injected dependency closes a resource still in use elsewhere and turns a shared singleton into a use-after-dispose bug, while the DI container disposes registered services at the end of their scope.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:90-91` · high · sha:ce7621949da8</sub>

## Reference
- SEAM-14 rationale is that closing a caller's shared client would break the rest of their application; conformance is that closing a transport over a BYO client leaves the BYO object usable, and closing an SDK-managed transport releases the owned pool/executor.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:43-43` · high · sha:0adae2d6a47f</sub>
- SEAM-25 conformance is that closing twice shuts the executor once with one event, in-flight tasks complete rather than being force-interrupted, and a BYO executor is untouched.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:44-44` · high · sha:0adae2d6a47f</sub>
- Arbitrary-victim eviction is acceptable for bounded maps because the cap is a memory backstop and not the primary cleanup mechanism, and unbounded caller/server-keyed maps are a memory-exhaustion/DoS vector.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:36-36` · high · sha:d6123be82c9e</sub>
- Transports implement IDisposable (IHttpClient) or IAsyncDisposable (IAsyncHttpClient) through the seam interfaces, satisfying SEAM-14(1)'s "closeable", and ASYNC-17's no-op default is supplied by DelegateHttpClient.Create.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:749-751` · high · sha:da6000c93fc5</sub>
- As built, Response.Dispose and the body Dispose overrides are not latched and rely on the documented idempotence of HttpResponseMessage and Stream (Stream's verified); the latch makes the guarantee the SDK's rather than the inner object's.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:763-765` · high · sha:da6000c93fc5</sub>
- ASYNC-16's graceful drain is a SHOULD that applies only to an adapter owning worker threads, and none of the MVP packages owns any.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:780-781` · high · sha:da6000c93fc5</sub>
- As built (d45e64b), lifecycle is partial: ownership-aware transport disposal is built, but there is no dispose latch on Response/bodies/transport, no ObjectDisposedException after dispose, and no quiet-dispose helper.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:802-803` · high · sha:da6000c93fc5</sub>
- Rule 13.4 is enforced by CA2213 (dispose owned fields), and review rejects disposing an injected dependency.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:104-104` · high · sha:ce7621949da8</sub>
- Rule 13.5 is enforced by CA2215 and review rejecting .Result or .Wait() inside Dispose.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:124-124` · high · sha:ce7621949da8</sub>
- Rule 13.6 is enforced by CA2000-adjacent review; a rent without a finally return, or an unbounded rent size, is a finding.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:145-145` · high · sha:ce7621949da8</sub>
- Rule 13.7 is enforced by review rejecting Channel.CreateUnbounded, an unbounded cache, or a pool without a max.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:161-161` · high · sha:ce7621949da8</sub>
- Rule 13.8 is enforced by review rejecting `new HttpClient()` in application code, with CA2000 and CA2213 flagging an undisposed CancellationTokenSource.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:182-182` · high · sha:ce7621949da8</sub>
- A managed runtime collects memory but not handles, so files, sockets, connections and rented buffers are released only when code does so explicitly; unbounded pools and caches are leaks that load exposes (root rule 9).
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:3-3` · high · sha:ce7621949da8</sub>
- Rule 13.1 is enforced by CA2000 (dispose before losing scope) and review that an owning type implements the right disposable interface.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:50-50` · high · sha:ce7621949da8</sub>
- Rule 13.2 is enforced by IDE0063 (use simple using) and review of nesting depth.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:69-69` · high · sha:ce7621949da8</sub>
- Rule 13.3 is enforced by CA1816 (call GC.SuppressFinalize correctly), and review rejects a finalizer on a type holding only managed state.
  <sub>styleguide · `docs/styleguide/csharp/13-resource-management.md:85-85` · high · sha:ce7621949da8</sub>
- Chapter 13 covers IDisposable/IAsyncDisposable, using declarations, the dispose pattern only when owning, ArrayPool/MemoryPool, bounded pools/channels/caches, and CancellationTokenSource disposal.
  <sub>styleguide · `docs/styleguide/csharp/README.md:45-45` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded

