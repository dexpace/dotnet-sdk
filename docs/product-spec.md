# dexpace SDK — Language-Agnostic Product and Porting Specification

**Status:** Draft for implementation. Normative. This document specifies observable behavior and contracts, not any single language's API. It is versioned alongside the reference (Kotlin/JVM) implementation and supersedes prose scattered across per-subsystem design notes.

**Audience:** Engineers porting the dexpace SDK to another language or runtime; maintainers of the reference implementation who need a single normative reference; and reviewers auditing a port for behavioral parity. Readers are assumed fluent in HTTP semantics (RFC 9110/9111), RFC 3986 URIs, and their target language's concurrency and resource-management model.

**How to read this document.** Requirements use the RFC 2119 keywords **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, and **MAY** with their standard meanings. A **MUST** is a correctness or safety invariant; a port that violates it is non-conforming. A **SHOULD** is a strong recommendation whose deviation must be deliberate and documented, typically because the behavior is a reference-implementation choice rather than an interop invariant. A **MAY** marks an optional capability or an explicitly permitted latitude.

Every normative statement carries a stable requirement identifier of the form `PREFIX-N` (for example `HTTP-3`, `RETRY-7`, `SEAM-30`). The prefix names the subsystem the requirement originates in: `SEAM` (extension model), `HTTP` (wire model), `IO` (streaming), `BODY` (payload lifecycle), `CTX` (execution context), `PIPE` (stage pipeline), `RECOV` (recovery chain), `RETRY` (resilience), `REDIR` (redirects), `AUTH` (authentication), and `NFR` (quality bar). These identifiers are load-bearing: cross-references between subsystems use them, and a conformance suite should tag each test with the identifier it exercises. Each requirement is presented as its identifier and level, a normative statement, a one-line rationale, and a one-line note on how to conformance-test it. Where the Kotlin/JVM SDK realizes a requirement in a way worth knowing, a short *Reference implementation:* note is added in italics; that note is informative only, and the requirement above it is the portable contract.

**Scope.** This document covers the SDK's product thesis, its durable architectural principles, its pluggable extension seams, the immutable HTTP domain model, the byte-streaming and body-lifecycle contracts, the execution-context model, both execution-pipeline layers, and the resilience concerns layered on top of them: retry, redirect following, and authentication. A companion document (produced separately) covers pagination, server-sent events, serialization, instrumentation, configuration and utilities, the transport and async-runtime adapter contracts, cross-cutting invariants, and the non-functional quality bar; requirements from those subsystems are cited here where they cross a boundary.

**Non-goals of this specification.** This document does not prescribe an implementation language, a concurrency framework, a build system, or a wire library. It does not define a code-generation layer or DTO model; it specifies the runtime primitives generated code would rely on. It does not restate HTTP, TLS, or URI semantics that the referenced RFCs already define, except where the SDK deliberately narrows or tolerates them. It does not mandate the reference implementation's exact class names, package layout, or numeric tuning constants except where a constant is itself part of the contract (those are called out explicitly). Finally, it contains no "Consolidated Normative Requirement Index"; that index is generated mechanically and appended after this text.

## Table of Contents

- [1. Product Overview](./product-spec/01-product-overview.md)
- [2. Architectural Principles](./product-spec/02-architectural-principles.md)
- [3. Pluggable Seams and Extension Model](./product-spec/03-pluggable-seams-and-extension-model.md)
- [4. Core HTTP Domain Model](./product-spec/04-core-http-domain-model.md)
- [5. I/O Contracts](./product-spec/05-i-o-contracts.md)
- [6. Request and Response Body Lifecycle](./product-spec/06-request-and-response-body-lifecycle.md)
- [7. Execution Context Model](./product-spec/07-execution-context-model.md)
- [8. Execution Pipelines](./product-spec/08-execution-pipelines.md)
- [9. Retry and Resilience](./product-spec/09-retry-and-resilience.md)
- [10. Redirect Handling](./product-spec/10-redirect-handling.md)
- [11. Authentication](./product-spec/11-authentication.md)
- [12. Pagination](./product-spec/12-pagination.md)
- [13. Server-Sent Events and Streaming](./product-spec/13-server-sent-events-and-streaming.md)
- [14. Serialization (Serde)](./product-spec/14-serialization-serde.md)
- [15. Instrumentation and Observability](./product-spec/15-instrumentation-and-observability.md)
- [16. Configuration](./product-spec/16-configuration.md)
- [17. Transport Adapter Conformance Contract](./product-spec/17-transport-adapter-conformance-contract.md)
- [18. Asynchronous Runtime Adapter Contract](./product-spec/18-asynchronous-runtime-adapter-contract.md)
- [19. Cross-Cutting Invariants and Policies](./product-spec/19-cross-cutting-invariants-and-policies.md)
- [20. Non-Functional Requirements and Quality Bar](./product-spec/20-non-functional-requirements-and-quality-bar.md)
- [Appendix A — Glossary](./product-spec/appendix-a-glossary.md)
- [Appendix B — Conformance Test Checklist](./product-spec/appendix-b-conformance-test-checklist.md)
- [Appendix C — Consolidated Normative Requirement Index](./product-spec/appendix-c-consolidated-normative-requirement-index.md)
