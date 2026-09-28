# sdk-positioning

## Rules

## Constraints

## Conclusions
- The dexpace SDK is framed as "the machinery an HTTP client is made of," an HTTP-client toolkit rather than an HTTP client itself.
  <sub>spec · `docs/product-spec/01-product-overview.md:3-3` · high · sha:4f786c44354d</sub>
- Because the core carries no concrete transport, codec, or I/O dependency (SEAM-1), a consumer can adopt it without inheriting a transitive-dependency conflict and can swap any one concern independently.
  <sub>spec · `docs/product-spec/01-product-overview.md:7-7` · high · sha:4f786c44354d</sub>
- Correctness-sensitive decisions (idempotency classification, status ranges, body replayability, header-injection defenses, credential hygiene, cancellation semantics) are made once in the core so every transport behaves identically. (SEAM-1)
  <sub>spec · `docs/product-spec/01-product-overview.md:7-7` · high · sha:4f786c44354d</sub>
- A faithful port of the SDK must preserve the seams and their invariants rather than reimplement each concern per adapter. (SEAM-1)
  <sub>spec · `docs/product-spec/01-product-overview.md:7-7` · high · sha:4f786c44354d</sub>

## Reference
- The SDK core supplies immutable wire models, a staged request/response pipeline, and recovery-aware resilience primitives, but does not itself open sockets, encode JSON, or read bytes off a stream.
  <sub>spec · `docs/product-spec/01-product-overview.md:3-3` · high · sha:4f786c44354d</sub>
- Every capability that touches the outside world (network transport, byte-stream implementation, wire codec) plugs into the SDK behind a single-purpose interface.
  <sub>spec · `docs/product-spec/01-product-overview.md:3-3` · high · sha:4f786c44354d</sub>
- The asynchronous-runtime concern is handled through a canonical async pivot plus optional out-of-core adapter modules rather than a core interface.
  <sub>spec · `docs/product-spec/01-product-overview.md:3-3` · high · sha:4f786c44354d</sub>
- The first primary consumer audience is authors of generated or hand-written service clients who need correct, secure, observable HTTP plumbing without re-solving retry, redirect-credential-leak, and secret-logging problems per service.
  <sub>spec · `docs/product-spec/01-product-overview.md:5-5` · high · sha:4f786c44354d</sub>
- The second primary consumer audience is application teams who want to bring their own transport, JSON library, and async runtime and pay only for what they put on the classpath.
  <sub>spec · `docs/product-spec/01-product-overview.md:5-5` · high · sha:4f786c44354d</sub>

## Conflicts

## Superseded
