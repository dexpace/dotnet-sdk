## 2. Architectural Principles

These are the durable principles a port must preserve even as individual subsystems evolve. They are stated here once and referenced throughout.

**Pluggable seams over embedded implementations.** Each external concern is exposed as exactly one narrow interface the core depends on but never implements, and the core never references a concrete implementation by name.

- **SEAM-1** (**MUST**): The core library MUST NOT embed a concrete HTTP transport, byte-stream I/O implementation, or wire codec, and MUST depend at runtime on nothing beyond its language's standard library plus a compile-time-only logging facade. *Rationale:* a dependency-free core lets consumers pay only for the runtimes they choose and swap any concern independently. *Conformance:* a dependency audit of the core module finds only the standard library plus the compile-scope logging facade; no transport/codec/stream symbol is referenced from core.
- **SEAM-2** (**MUST**): Each external concern that has a core-owned contract MUST be exposed as exactly one narrow interface — the enumerated seams are byte-stream provider, synchronous transport, asynchronous transport, wire codec, and operation-input→request projection — and the core MUST NOT reference any concrete implementation of a seam by name. *Rationale:* one seam per concern keeps the dependency surface flat and each capability independently replaceable. *Conformance:* substitute a fake implementation of each seam and confirm the core operates unchanged.

**Immutability of shared state.** Every value that crosses a concurrency or API boundary is immutable after construction; change is expressed by producing a new instance.

- **HTTP-1** (**MUST**): All core domain-model types MUST present an immutable value/metadata surface after construction, safe to share across threads without external synchronization; any change MUST produce a new instance. The single carve-out is a body that wraps live single-use stream state (§6). *Rationale:* the model is the shared boundary handed to concurrent transports and pipelines; a mutable metadata surface would race. *Conformance:* mutate the originating builder after construction and assert the built instance is unchanged, from multiple threads.
- **SEAM-29** / **HTTP-2** (**MUST**): Model construction MUST go through an immutable-value + Builder (or dedicated factory) pattern; there MUST be no public field-wise constructor or unchecked copy that bypasses validation. *Rationale:* validation and normalization live in the builder/factory; a bypass would let invalid instances exist. *Conformance:* verify no construction path skips builder validation (e.g. deriving a request cannot install a body-carrying GET).

**Single-sourced correctness.** A decision that must stay consistent across the SDK is computed from one definition, never duplicated.

- **HTTP-9 / RETRY-1 / RETRY-6** (**MUST**): The idempotent-method set `{GET, HEAD, OPTIONS, PUT, DELETE}` and the retryable-status classifier (408, 429, and all 5xx except 501 and 505) MUST each be single-sourced, so the retry allow-list, the inherent replay-safety gate, and the exception's baked retryable flag all derive from one place. *Rationale:* divergent copies of these classifications produce inconsistent retry behavior. *Conformance:* table-drive every method/status against the single definition and against each consumer of it.
- **RETRY-13** (**MUST**): Both retry stacks MUST compute backoff via one shared calculator using one shared set of constants; neither may carry an independent formula. *Rationale:* the explicit anti-drift guarantee. *Conformance:* feed identical settings to both stacks with jitter neutralized and assert identical delay sequences.

**Security by default.** Safe behavior is the default; exposing a secret or lowering a guarantee must be an explicit opt-in.

- Credentials are transport-scoped and never stamped over plaintext (**AUTH-28**); redirects strip credentials and never launder them cross-origin (**REDIR-7**, **REDIR-9**, **REDIR-8**); header names and values are validated against request-splitting before any transport sees them (**HTTP-17**–**HTTP-19**); Digest client nonces come from a cryptographically strong source (**AUTH-20**); and log-preview and error-body buffers are bounded (**HTTP-52**, **BODY-30**).

**Cancellation correctness.** Cancellation is distinct from timeout, propagates into the network layer, and never silently disappears.

- **SEAM-13** (**SHOULD**): Blocking transports SHOULD honor cooperative cancellation during blocking I/O; async transports SHOULD treat cancelling the returned future as a best-effort abort of the in-flight exchange. *Rationale:* cancellation must reach the network layer to free resources promptly. *Conformance:* interrupt a parked blocking send and assert it unwinds; cancel an async future and assert the transport's cancel hook fires.
- Cancellation is terminal and non-retryable, and MUST be told apart from a retryable timeout out-of-band, never by matching an error message (see §9, **RETRY-23**, **RETRY-24**).

**Minimal-footprint core.** The core stays small and dependency-free so a consumer's footprint is proportional to the features actually used (**SEAM-1**, **NFR-1**, **NFR-2**). Optional capabilities — each transport, codec, I/O backend, and async bridge — are separately installable units depending on the core plus at most one third-party library.

