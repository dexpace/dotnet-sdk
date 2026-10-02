# transport-adapter

## Rules
- TRANSPORT-3 (MUST): On the sync path a caller-initiated cancellation surfaces as a terminal, non-retryable interrupt-shaped I/O exception with the runtime's cancellation signal preserved, is never repackaged as the retryable transport-failure exception, and is discriminated out-of-band via the runtime's cancellation state rather than by matching messages.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:12-12` · high · sha:2d5843c58993</sub>
- TRANSPORT-4 (MUST): A read or response timeout is classified as a retryable transport failure (the canonical NetworkException) and does not set the caller's cancellation flag, even when the runtime represents a timeout with the same exception family as an interrupt.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:13-13` · high · sha:2d5843c58993</sub>
- TRANSPORT-5 (MUST): A per-call timeout override applies to that single call only, overriding the configured default for that call and leaving the shared native client untouched, and a null override leaves the configured default in force.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:14-14` · high · sha:2d5843c58993</sub>
- TRANSPORT-6 (SHOULD): A transport does not let a positive per-call timeout be silently reduced to zero by unit truncation; where the native timeout API is coarser than the requested duration and treats zero as no timeout, a positive sub-resolution duration MUST be clamped up to the smallest finite deadline.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:15-15` · high · sha:2d5843c58993</sub>
- TRANSPORT-7 (MUST): Cancelling the async response future propagates cancellation into the in-flight native exchange so its connection and resources are released promptly.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:16-16` · high · sha:2d5843c58993</sub>
- TRANSPORT-8 (MUST): Where the native client can surface a cancellation originating inside it while the SDK future is still live, that cancellation completes the future with a terminal, non-retryable cancellation-shaped exception, while a genuine timeout on the same path still completes retryable; the OkHttp reference implements this and a transport with no internal-cancel path need not.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:17-17` · high · sha:2d5843c58993</sub>
- TRANSPORT-9 (MUST): If a native response is delivered after the SDK future has already completed or been cancelled (the adaptation race), the adapted response is closed so its connection is returned to the pool.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:18-18` · high · sha:2d5843c58993</sub>
- TRANSPORT-10 (MUST): The caller's explicit request Content-Type remains authoritative and is never overwritten by a body-derived media type, and a body-derived Content-Type is emitted only when the caller set none (matched case-insensitively).
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:22-22` · high · sha:2d5843c58993</sub>
- TRANSPORT-11 (MUST): Headers the native client computes from the body or connection (at minimum Content-Length, Host, Transfer-Encoding, plus any the native client rejects outright such as Connection, Expect and Upgrade on java.net.http) are dropped before dispatch, the transport SHOULD log each drop at verbose, and the exact drop set is transport-specific (OkHttp does not drop Connection).
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:23-23` · high · sha:2d5843c58993</sub>
- TRANSPORT-12 (MUST): A header valid at the SDK model layer but rejected by the native client's stricter wire grammar is dropped for that header only, the resulting native exception does not escape the send contract, and the remaining headers and body are still dispatched on both sync and async paths.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:24-24` · high · sha:2d5843c58993</sub>
- TRANSPORT-13 (SHOULD): A transport exposes a configurable policy for logging header drops (every drop loudly; first per name loudly then quiet, the default; all quiet), and the per-name dedup mode MUST be case-insensitive and bounded so an attacker cannot grow it without limit.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:25-25` · high · sha:2d5843c58993</sub>
- TRANSPORT-14 (MUST): Inbound response headers are copied leniently so one malformed header does not fail the whole response: a control byte in a value, or a control or non-ASCII byte in a name, drops only that header (logged at verbose) while the body and remaining headers are still delivered, and a transport SHOULD preserve a non-ASCII obs-text byte in a value rather than stripping it.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:26-26` · high · sha:2d5843c58993</sub>
- Where a transport behavior exists in only one reference transport (OkHttp, java.net.http), the requirement is scoped to that transport accordingly.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:3-3` · medium · sha:2d5843c58993</sub>
- TRANSPORT-15 (MUST): Transport close() is ownership-aware and releases only resources the transport itself created (native client, dispatcher or executor, pool, cache, any SDK-created executor), while a BYO native client and its resources are never shut down or mutated so the caller may keep using it after the transport is closed.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:30-30` · high · sha:2d5843c58993</sub>
- TRANSPORT-16 (MUST): Transport close() is idempotent and does not block on native shutdown in a way that discards the caller's cancellation or interrupt state, using non-blocking shutdown with no unbounded await.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:31-31` · high · sha:2d5843c58993</sub>
- TRANSPORT-17 (MUST): A non-replayable (single-use) request body is written to the wire exactly once, the transport prevents the native client from re-writing it (for example by reporting the body as one-shot) and does not itself trigger a second write, while a replayable body MAY be re-written.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:32-32` · high · sha:2d5843c58993</sub>
- TRANSPORT-18 (MUST): When the native body API drives writes through a re-subscribable producer (so a native internal resend such as proxy-auth 407 or GOAWAY re-reads the body), the transport makes each subscription produce identical bytes by buffering a non-replayable body once into a replayable copy, and if that buffering fails mid-write the send fails with the transport-failure type rather than shipping a truncated body.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:33-33` · high · sha:2d5843c58993</sub>
- TRANSPORT-19 (SHOULD): When a streaming-body subscription is acquired but abandoned (connect failure, early cancellation), the transport cancels or unblocks its producer so no writer thread or file handle is stranded, and teardown MUST be idempotent.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:34-34` · high · sha:2d5843c58993</sub>
- TRANSPORT-20 (MUST): Any transport failure that produced no HTTP response (connection refused, DNS or TLS failure, peer reset, connect or read timeout) surfaces as the SDK's canonical retryable transport-failure exception, which MUST be a subtype of the platform I/O-error type and MUST report itself retryable.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:38-38` · high · sha:2d5843c58993</sub>
- TRANSPORT-21 (MUST): On the async path, a failure before dispatch (request adaptation rejecting a request, a synchronous dispatch rejection, an adapter bug) is delivered through the returned future and not thrown synchronously, and only truly fatal runtime errors may propagate synchronously.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:39-39` · high · sha:2d5843c58993</sub>
- TRANSPORT-22 (MUST): If adapting a live native response throws at any point after the native response and its socket are live, the transport closes the native response before propagating, on both sync and async paths.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:40-40` · high · sha:2d5843c58993</sub>
- TRANSPORT-23 (MUST): The async send never completes its future with a null response on success, and a transport with no response completes exceptionally.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:41-41` · high · sha:2d5843c58993</sub>
- TRANSPORT-24 (MUST): The response status code is mapped totally: any code the server returns, including vendor or non-standard codes (499, 520-526, 530), is surfaced faithfully, and such a response and its body remain readable and closeable.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:42-42` · high · sha:2d5843c58993</sub>
- TRANSPORT-25 (MUST): The response body is exposed as a lazily-read stream and not pre-buffered, closing the SDK response cascades to close the native body and release the connection, and the caller owns closing the response.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:43-43` · high · sha:2d5843c58993</sub>
- TRANSPORT-26 (MUST): A body-less request is valid for any method the model permits; where the native client rejects a null body for a body-requiring method (POST, PUT, PATCH) the transport substitutes a zero-length body with Content-Length: 0 instead of failing, and for body-forbidden methods (GET, HEAD, TRACE, CONNECT) it never attaches a body.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:44-44` · high · sha:2d5843c58993</sub>
- TRANSPORT-27 (SHOULD): An unparseable or absent inbound Content-Type is downgraded to no media type rather than failing the response, and an absent or invalid Content-Length maps to the unknown-length sentinel -1.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:45-45` · high · sha:2d5843c58993</sub>
- TRANSPORT-28 (SHOULD): A transport streams a file-backed request body directly from the file (honoring start position and byte count) on a zero-copy path where supported, and MUST treat a file body as replayable so it can be re-sent.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:46-46` · high · sha:2d5843c58993</sub>
- TRANSPORT-29 (MUST): A transport instance is safe for concurrent send calls from multiple threads and effectively immutable after construction, with all per-request state confined to local scope or the returned response graph.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:47-47` · high · sha:2d5843c58993</sub>
- TRANSPORT-30 (SHOULD): When the SDK's proxy configuration carries a feature the native client cannot honor, the transport makes the limitation discoverable rather than silently misbehaving: a custom (non-Basic) proxy challenge handler is surfaced with a WARN and proxy auth falls back to Basic.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:48-48` · high · sha:2d5843c58993</sub>
- TRANSPORT-30 (MUST NOT leak): Proxy credentials MUST NOT be logged and MUST NOT be answered to an origin-server (401) challenge, only to a matching proxy (407).
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:48-48` · high · sha:2d5843c58993</sub>
- TRANSPORT-1 (MUST): An SDK-managed (builder-constructed) transport disables the native client's automatic redirect following, and the follow-redirects knob's default is off.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:7-7` · high · sha:2d5843c58993</sub>
- TRANSPORT-2 (MUST): Where the native client has a built-in connection-failure or automatic retry feature, an SDK-managed transport disables it.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:8-8` · high · sha:2d5843c58993</sub>
- TRANSPORT-1 and TRANSPORT-2: native redirects and native auto-retry are disabled on SDK-managed transports.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:59-59` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-3: synchronous cancellation yields the terminal interrupt exception type with the interrupt flag preserved, not the retryable type.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60-60` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-4: a timeout yields the retryable exception type with a clear interrupt flag, checking the timeout subtype first.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60-60` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-5: a per-call timeout applies to that one call only.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60-60` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-6: a positive timeout below the native resolution is clamped rather than truncated.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60-60` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-7: cancelling an async future propagates into the native exchange.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60-60` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-8: a native-internal cancel yields the terminal type while a timeout stays retryable.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60-60` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-9: a response lost in an adaptation race is closed.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:60-60` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-10: an explicit Content-Type wins and a body-derived one applies only when absent.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61-61` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-11: framing headers are dropped and recomputed.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61-61` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-12: a header valid in the model but rejected by the native client is dropped rather than thrown, in sync and async.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61-61` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-13: the dropped-header logging policy is bounded and case-insensitive.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61-61` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-14: a malformed inbound header is dropped rather than the response, and obs-text is preserved.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:61-61` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-15: ownership-aware close leaves a bring-your-own client usable.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62-62` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-16: transport close is idempotent, non-blocking and interrupt-safe.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62-62` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-17: a single-use body is written once.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62-62` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-18: a re-subscribable producer replays identical bytes or fails on buffering failure.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62-62` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-19: an abandoned streaming subscription unblocks its producer.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:62-62` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-20: a failure with no response yields a retryable I/O-subtype exception.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-21: a pre-dispatch async failure is delivered through the future rather than thrown synchronously.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-22: a throw during response adaptation closes the native response.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-23: an async transport never completes with null on success.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-24: vendor status codes are surfaced with a readable body.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-25: the streaming body is lazy with a close cascade.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-26: a body-less request is valid for any method and a zero-length body is substituted where required.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-27: a malformed inbound Content-Type or Content-Length is downgraded.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-28: a file body is zero-copy where supported and replayable.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-29: the transport is concurrent-safe and immutable.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- TRANSPORT-30: unsupported proxy features are discoverable and credentials are never leaked.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:63-63` · high · sha:0451cc7f3bb4</sub>
- Conformance checks are plain methods that throw a ConformanceException on failure, with no xUnit, NUnit or MSTest dependency, so the package imposes no test framework on its consumers; each first-party adapter's test project drives it from its own framework.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:40-40` · high · sha:c9a7834ab04d</sub>
- A transport that ignores RequestOptions behaves identically with and without them, because options are inert data (SEAM-11).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:285-286` · high · sha:da6000c93fc5</sub>
- The adapter sends with HttpCompletionOption.ResponseHeadersRead so the response body is the live content stream (SEAM-11, TRANSPORT-25).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:302-303` · high · sha:da6000c93fc5</sub>

## Constraints

## Conclusions
- The SDK is an HTTP-client toolkit rather than an HTTP client: it owns redirect, retry, auth and logging in its pipeline and delegates only sending one request and getting one response to a transport (SEAM-11, SEAM-16).
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:3-3` · high · sha:2d5843c58993</sub>
- Both transport seams (SEAM-11, SEAM-16) survive on .NET, and by P4's converse the port must not collapse a split the host genuinely has (§3.2, §3.3).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:50-52` · high · sha:d7cea7b15cf3</sub>
- Dexpace.Sdk.Conformance depends on Dexpace.Sdk.Core only and is published from day one because a third-party adapter author has no other way to prove an adapter against the assertions the first-party adapters run.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:40-40` · high · sha:c9a7834ab04d</sub>
- SEAM-11 (response body not pre-buffered by the transport, caller owns reading and closing it) and SEAM-12 (transports safe for concurrent calls, per-request state confined to locals or the returned response graph) are pure intent and hold on .NET unchanged.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:248-251` · high · sha:da6000c93fc5</sub>
- The synchronous transport seam is the nominal interface IHttpClient and the async one IAsyncHttpClient, because C# dispatches through declared interfaces whereas Ruby's seam was a duck type (#call).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:253-255` · high · sha:da6000c93fc5</sub>
- Core ships DelegateHttpClient.Create(...) factories wrapping a Func<Request, RequestOptions, CancellationToken, Response> (and the Task<Response> form) in a sealed adapter with a no-op dispose, because a C# lambda cannot implement an interface and SEAM-11's conformance clause says a bare send lambda works as a transport.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:260-263` · high · sha:da6000c93fc5</sub>
- The option-less Execute overload is an extension method rather than a default interface member, because a default interface member is invisible through a class-typed reference (verified: calling a DIM overload on a variable typed as the implementing class fails with CS7036), so a consumer holding a SystemNetHttpClient would not see it.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:282-285` · high · sha:da6000c93fc5</sub>

## Reference
- TRANSPORT-10 conformance: body media type X with explicit Content-Type Y sends Y on the wire, and the same body with no explicit header sends X.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:22-22` · high · sha:2d5843c58993</sub>
- A conforming transport disables the native client's own redirect and retry, maps faithfully between SDK and native models, classifies cancellation, timeout and no-response failures into the SDK's canonical exception contract, propagates cancellation bidirectionally, keeps the caller's Content-Type authoritative, drops headers the native client cannot encode instead of failing the send, never touches a BYO client's lifecycle, and writes request bodies replay-safely.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:3-3` · high · sha:2d5843c58993</sub>
- TRANSPORT-26 conformance: a body-less POST/PUT/PATCH dispatches with an empty body and Content-Length: 0, and a body-less GET dispatches with no body.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:44-44` · high · sha:2d5843c58993</sub>
- TRANSPORT-1 conformance: enqueue a 302 with Location and assert the returned response is the raw 302, not the redirected target.
  <sub>spec · `docs/product-spec/17-transport-adapter-conformance-contract.md:7-7` · high · sha:2d5843c58993</sub>
- IHttpClient : IDisposable declares the single abstract member Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken), threading per-call options through it (SEAM-11, TRANSPORT-5, ASYNC-19).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:266-273` · high · sha:da6000c93fc5</sub>
- HttpClientExtensions provides the option-less convenience Execute(this IHttpClient client, Request request, CancellationToken cancellationToken = default), which passes RequestOptions.Empty.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:275-279` · high · sha:da6000c93fc5</sub>
- TRANSPORT-24's total status mapping is free on .NET because (int)StatusCode accepts any code.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:388-389` · high · sha:da6000c93fc5</sub>

## Conflicts

## Superseded

