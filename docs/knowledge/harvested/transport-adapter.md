# transport-adapter

## Rules
- The synchronous transport MUST be a single-operation contract — given one request, produce one response — and MUST NOT pre-buffer the response body, leaving the caller to own reading and closing it; it MAY accept per-call options, and a transport that ignores options MUST behave identically to the no-options call (SEAM-11).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:12-12` · high · sha:0adae2d6a47f</sub>
- Both synchronous and asynchronous transports MUST be safe for concurrent calls, with all per-request state confined to locals or the returned response/future graph (SEAM-12).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:13-13` · high · sha:0adae2d6a47f</sub>
- An async transport's returned future MUST complete either with a non-null response, which the caller owns and must close, or exceptionally, and MUST NOT complete successfully with a null/absent value; cancelling an already-completed success future does not close the delivered response body, so the caller must still close it (SEAM-16).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:17-17` · high · sha:0adae2d6a47f</sub>
- Wrapping an async transport as blocking MUST unwrap the async-wrapper exception so callers see the original failure, and the blocking wait MUST honor interruption by restoring the interrupt flag, cancelling the in-flight future, and surfacing an interrupted-I/O error (SEAM-18).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:19-19` · high · sha:0adae2d6a47f</sub>
- Per-call options MUST be threaded through the sync/async bridges, never dropped (SEAM-18).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:19-19` · high · sha:0adae2d6a47f</sub>
- On any path where a send produces a response that the returned future will not hand to a caller — because the future was already cancelled or completed exceptionally — the producer MUST close that orphaned response so its connection/descriptor is not leaked (SEAM-30).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:20-20` · high · sha:0adae2d6a47f</sub>
- Both transport seams MUST be closeable, and close MUST be idempotent, ownership-aware (only resources the transport itself created are released, a caller-supplied client/executor is never touched), and interrupt-safe; a lightweight transport MAY have a no-op close (SEAM-14).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:43-43` · high · sha:0adae2d6a47f</sub>
- An SDK-managed (builder-constructed) transport must disable the native client's automatic redirect following, with the follow-redirects knob defaulting to off. (TRANSPORT-1)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:7-7` · high · sha:2d5843c58993</sub>
- Where the native client has a built-in connection-failure/automatic retry feature, an SDK-managed transport must disable it. (TRANSPORT-2)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:8-8` · high · sha:2d5843c58993</sub>
- The caller's explicit request Content-Type remains authoritative and must not be overwritten by a body-derived media type; a body-derived Content-Type is emitted only when the caller set none, matched case-insensitively. (TRANSPORT-10)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:22-22` · high · sha:2d5843c58993</sub>
- Headers the native client computes from the body/connection—at minimum Content-Length, Host, Transfer-Encoding, plus any the native client rejects outright such as Connection/Expect/Upgrade on java.net.http—must be dropped before dispatch, with the transport also logging each drop at verbose; the exact drop set is transport-specific, e.g. OkHttp does not drop Connection. (TRANSPORT-11)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:23-23` · high · sha:2d5843c58993</sub>
- A header valid at the SDK model layer but rejected by the native client's stricter wire grammar must be dropped for that header only, without letting the resulting native exception escape the send contract, and the rest of the headers and body must still be dispatched on both sync and async paths. (TRANSPORT-12)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:24-24` · high · sha:2d5843c58993</sub>
- A transport should expose a configurable policy for how header drops are logged—every drop loudly, first-per-name loudly then quiet as the default, or all quiet—with the per-name dedup mode being case-insensitive and bounded so it cannot be grown without limit. (TRANSPORT-13)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:25-25` · high · sha:2d5843c58993</sub>
- Inbound response headers must be copied leniently enough that a single malformed header does not fail the whole response: a control byte in a value, or a control/non-ASCII byte in a name, drops only that header (logged at verbose) while the body and remaining headers are still delivered, and a transport should preserve a non-ASCII/obs-text byte in a value rather than stripping it. (TRANSPORT-14)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:26-26` · high · sha:2d5843c58993</sub>
- close() is ownership-aware, releasing only resources the transport itself created (native client, dispatcher/executor, pool, cache, any SDK-created executor) and must never shut down or mutate a BYO native client and its resources, so the caller may keep using it after the transport is closed. (TRANSPORT-15)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:30-30` · high · sha:2d5843c58993</sub>
- close() must be idempotent and must not block on native shutdown in a way that discards the caller's cancellation/interrupt state, using non-blocking shutdown with no unbounded await. (TRANSPORT-16)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:31-31` · high · sha:2d5843c58993</sub>
- A non-replayable (single-use) request body must be written to the wire exactly once, with the transport preventing the native client from re-writing it (e.g. by reporting the body as one-shot) and never itself triggering a second write, while a replayable body may be re-written. (TRANSPORT-17)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:32-32` · high · sha:2d5843c58993</sub>
- When the native body API drives writes through a re-subscribable producer so a native internal resend (proxy-auth 407, GOAWAY) re-reads the body, the transport must make each subscription produce identical bytes by buffering a non-replayable body once into a replayable copy, and if that buffering fails mid-write the send must fail with the transport-failure type rather than shipping a truncated body. (TRANSPORT-18)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:33-33` · high · sha:2d5843c58993</sub>
- When a streaming-body subscription is acquired but abandoned (connect failure, early cancellation), the transport should cancel/unblock its producer so no writer thread or file handle is stranded, with idempotent teardown. (TRANSPORT-19)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:34-34` · high · sha:2d5843c58993</sub>
- Any transport failure that produced no HTTP response—connection refused, DNS/TLS failure, peer reset, connect/read timeout—must surface as the SDK's canonical retryable transport-failure exception, which must be a subtype of the platform I/O-error type and must report itself retryable. (TRANSPORT-20)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:38-38` · high · sha:2d5843c58993</sub>
- On the async path, a failure before dispatch (request adaptation rejecting a request, a synchronous dispatch rejection, an adapter bug) must be delivered through the returned future rather than thrown synchronously, with only truly fatal runtime errors permitted to propagate synchronously. (TRANSPORT-21)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:39-39` · high · sha:2d5843c58993</sub>
- If adapting a live native response throws at any point after the native response and its socket are live, the transport must close the native response before propagating, on both sync and async paths. (TRANSPORT-22)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:40-40` · high · sha:2d5843c58993</sub>
- The async send must not complete its future with a null response on success; a transport with no response must complete exceptionally. (TRANSPORT-23)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:41-41` · high · sha:2d5843c58993</sub>
- The response status code must be mapped totally—any code the server returns, including vendor/non-standard codes such as 499, 520-526, and 530, must be surfaced faithfully with the response and its body remaining readable and closeable. (TRANSPORT-24)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:42-42` · high · sha:2d5843c58993</sub>
- The response body is exposed as a lazily-read stream rather than pre-buffered, and closing the SDK response must cascade to close the native body and release the connection, with the caller owning closing the response. (TRANSPORT-25)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:43-43` · high · sha:2d5843c58993</sub>
- A body-less request must be valid for any method the model permits; where the native client rejects a null body for a body-requiring method (POST/PUT/PATCH), the transport must substitute a zero-length body with Content-Length: 0 instead of failing, and for body-forbidden methods (GET/HEAD/TRACE/CONNECT) it must not attach a body. (TRANSPORT-26)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:44-44` · high · sha:2d5843c58993</sub>
- An unparseable or absent inbound Content-Type should be downgraded to "no media type" rather than failing the response, and an absent or invalid Content-Length should map to the unknown-length sentinel of -1. (TRANSPORT-27)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:45-45` · high · sha:2d5843c58993</sub>
- A transport should stream a file-backed request body directly from the file, honoring start position and byte count, on a zero-copy path where supported, and must treat a file body as replayable so it can be re-sent. (TRANSPORT-28)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:46-46` · high · sha:2d5843c58993</sub>
- A transport instance must be safe for concurrent send calls from multiple threads and must be effectively immutable after construction, with all per-request state confined to local scope or the returned response graph. (TRANSPORT-29)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:47-47` · high · sha:2d5843c58993</sub>
- When the SDK's proxy configuration carries a feature the native client cannot honor, the transport should make the limitation discoverable rather than silently misbehaving and must not leak credentials—a custom non-Basic proxy challenge handler should be surfaced with a WARN with proxy auth falling back to Basic, and proxy credentials must not be logged and must not be answered to an origin-server 401 challenge, only to a matching proxy 407. (TRANSPORT-30)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:48-48` · high · sha:2d5843c58993</sub>

## Constraints

## Conclusions
- Wrapping a blocking transport as async requires a caller-supplied executor with intentionally no default, because a shared global fork/join-style pool is explicitly unacceptable since a blocking call would starve it (SEAM-18).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:19-19` · high · sha:0adae2d6a47f</sub>
- The SDK is designed as an HTTP-client toolkit rather than an HTTP client itself, owning redirect, retry, auth, and logging in its own pipeline and delegating to a transport only the single operation of sending one request and getting one response. (SEAM-11, SEAM-16)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:3-3` · high · sha:2d5843c58993</sub>

## Reference
- Some transport-adapter requirements are scoped to only one reference transport (OkHttp or java.net.http) where the described behavior exists in just that implementation. (SEAM-11, SEAM-16)
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:3-3` · high · sha:2d5843c58993</sub>
- SDK-managed transports disable the native HTTP client's built-in redirect following and built-in auto-retry. (TRANSPORT-1, TRANSPORT-2)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:59` · high · sha:0451cc7f3bb4</sub>
- A synchronous cancellation surfaces the terminal interrupt error type with the cancellation flag preserved, not the retryable timeout type. (TRANSPORT-3, TRANSPORT-4, TRANSPORT-5, TRANSPORT-6, TRANSPORT-7, TRANSPORT-8, TRANSPORT-9)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60` · high · sha:0451cc7f3bb4</sub>
- A timeout surfaces the retryable error type with a clear cancellation flag, checking the timeout subtype before the cancellation type. (TRANSPORT-3, TRANSPORT-4, TRANSPORT-5, TRANSPORT-6, TRANSPORT-7, TRANSPORT-8, TRANSPORT-9)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60` · high · sha:0451cc7f3bb4</sub>
- A per-call timeout applies only to that one call. (TRANSPORT-3, TRANSPORT-4, TRANSPORT-5, TRANSPORT-6, TRANSPORT-7, TRANSPORT-8, TRANSPORT-9)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60` · high · sha:0451cc7f3bb4</sub>
- A positive timeout below the clock's resolution is clamped rather than truncated to zero. (TRANSPORT-3, TRANSPORT-4, TRANSPORT-5, TRANSPORT-6, TRANSPORT-7, TRANSPORT-8, TRANSPORT-9)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60` · high · sha:0451cc7f3bb4</sub>
- Cancelling the async future propagates the cancellation into the underlying native exchange. (TRANSPORT-3, TRANSPORT-4, TRANSPORT-5, TRANSPORT-6, TRANSPORT-7, TRANSPORT-8, TRANSPORT-9)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60` · high · sha:0451cc7f3bb4</sub>
- A cancellation originating inside the native client surfaces the terminal error type, while a timeout still surfaces as retryable. (TRANSPORT-3, TRANSPORT-4, TRANSPORT-5, TRANSPORT-6, TRANSPORT-7, TRANSPORT-8, TRANSPORT-9)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60` · high · sha:0451cc7f3bb4</sub>
- A response arriving during an adaptation race is still closed. (TRANSPORT-3, TRANSPORT-4, TRANSPORT-5, TRANSPORT-6, TRANSPORT-7, TRANSPORT-8, TRANSPORT-9)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60` · high · sha:0451cc7f3bb4</sub>
- An explicit Content-Type header wins over a body-derived one, which is only used when Content-Type is absent. (TRANSPORT-10, TRANSPORT-11, TRANSPORT-12, TRANSPORT-13, TRANSPORT-14)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61` · high · sha:0451cc7f3bb4</sub>
- Transport framing headers are dropped from caller input and recomputed by the transport. (TRANSPORT-10, TRANSPORT-11, TRANSPORT-12, TRANSPORT-13, TRANSPORT-14)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61` · high · sha:0451cc7f3bb4</sub>
- A header that is valid at the model layer but rejected by the native client is dropped rather than thrown, consistently for sync and async. (TRANSPORT-10, TRANSPORT-11, TRANSPORT-12, TRANSPORT-13, TRANSPORT-14)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61` · high · sha:0451cc7f3bb4</sub>
- Logging of dropped headers follows a bounded, case-insensitive policy. (TRANSPORT-10, TRANSPORT-11, TRANSPORT-12, TRANSPORT-13, TRANSPORT-14)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61` · high · sha:0451cc7f3bb4</sub>
- A malformed inbound header is dropped rather than failing the whole response, while obs-text is preserved. (TRANSPORT-10, TRANSPORT-11, TRANSPORT-12, TRANSPORT-13, TRANSPORT-14)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61` · high · sha:0451cc7f3bb4</sub>
- Ownership-aware transport close leaves a caller-supplied (BYO) client still usable. (TRANSPORT-15, TRANSPORT-16, TRANSPORT-17, TRANSPORT-18, TRANSPORT-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62` · high · sha:0451cc7f3bb4</sub>
- Transport close is idempotent, non-blocking, and interrupt-safe. (TRANSPORT-15, TRANSPORT-16, TRANSPORT-17, TRANSPORT-18, TRANSPORT-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62` · high · sha:0451cc7f3bb4</sub>
- A single-use request body is written exactly once. (TRANSPORT-15, TRANSPORT-16, TRANSPORT-17, TRANSPORT-18, TRANSPORT-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62` · high · sha:0451cc7f3bb4</sub>
- A re-subscribable body producer either replays identical bytes on retry or fails cleanly on a buffering failure. (TRANSPORT-15, TRANSPORT-16, TRANSPORT-17, TRANSPORT-18, TRANSPORT-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62` · high · sha:0451cc7f3bb4</sub>
- An abandoned streaming subscription unblocks its producer rather than leaving it hung. (TRANSPORT-15, TRANSPORT-16, TRANSPORT-17, TRANSPORT-18, TRANSPORT-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62` · high · sha:0451cc7f3bb4</sub>
- A transport failure that received no response surfaces as a retryable I/O-family exception. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- A failure occurring before dispatch in the async transport path is delivered through the future, not thrown synchronously. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- A throw during response adaptation closes the native response. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- The async transport never completes successfully with a null response. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- Non-standard (vendor) HTTP status codes are surfaced with a readable body. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- A streaming response body is read lazily with a close-cascade to its underlying resource. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- A body-less request is valid for any HTTP method, substituting a zero-length body where the native client requires one. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- A malformed inbound Content-Type or Content-Length header is downgraded rather than failing the response. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- A file-backed request body uses zero-copy transfer where the transport supports it and remains replayable. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- The transport implementation is concurrency-safe and immutable. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>
- Unsupported proxy features are discoverable, and proxy credentials are never leaked. (TRANSPORT-20, TRANSPORT-21, TRANSPORT-22, TRANSPORT-23, TRANSPORT-24, TRANSPORT-25, TRANSPORT-26, TRANSPORT-27, TRANSPORT-28, TRANSPORT-29, TRANSPORT-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63` · high · sha:0451cc7f3bb4</sub>

## Conflicts

## Superseded
