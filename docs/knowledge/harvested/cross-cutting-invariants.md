# cross-cutting-invariants

## Rules
- close()/shutdown() MUST be idempotent, latched so repeats are no-ops, and MUST NOT block on interrupt-sensitive waits, using non-blocking shutdown and preserving the ambient interrupt/cancel flag as-is. (XCUT-13)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:30` · high · sha:d6123be82c9e</sub>
- The SDK MUST close only resources it created; a caller-supplied (BYO) transport client, executor, or connection pool MUST NOT be closed by the SDK, and the caller retains ownership and may keep using it after the SDK component is closed. (XCUT-22)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:31` · high · sha:d6123be82c9e</sub>
- Every process/instance-lived map whose key space is influenced by callers or servers MUST be bounded by a hard cap and MUST drain back under the cap after each insert using a loop rather than a single pre-insert check-then-evict, so a concurrent insert burst converges to the bound. (XCUT-14)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:36` · high · sha:d6123be82c9e</sub>
- Public wire models (request, response, headers, media type, status, etc.) MUST be immutable after construction and safe to share across threads, expressing mutation as producing a new instance, and MUST NOT retain an alias to externally-mutable state that a post-construction mutation could use to alter the model. (XCUT-15)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:40` · high · sha:d6123be82c9e</sub>
- Diagnostic/preview reads of caller- or server-controlled payloads (error-body snapshots, request/response body log previews) MUST be byte-capped and SHOULD be non-consuming, never materializing an unbounded payload into memory and never disturbing the primary read path the consumer will use. (XCUT-24)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:53` · high · sha:d6123be82c9e</sub>

## Constraints

## Conclusions
- A reimplementation that violates any cross-cutting invariant is considered incorrect or unsafe even if each subsystem individually appears to work.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:1-3` · high · sha:d6123be82c9e</sub>
- Unbounded caller/server-keyed maps are treated as a memory-exhaustion/DoS vector, which is the rationale for requiring a hard cap with drain-to-cap eviction. (XCUT-14)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:36` · high · sha:d6123be82c9e</sub>

## Reference
- A BYO (bring-your-own) resource is a dependency such as a native HTTP client, executor, or connection pool that the caller constructs and hands to the SDK, and the caller owns its lifecycle since the SDK never closes it, in contrast to an SDK-managed resource that the SDK created and must release on close.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:11` · high · sha:f0b3d2058626</sub>
- A drain-to-cap bounded map is a concurrent map whose caller/server-influenced keys are capped, drained in a loop back under a hard bound after each insert, converging even under concurrent insert bursts, with an arbitrary eviction victim.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:29` · high · sha:f0b3d2058626</sub>
- Ownership-aware lifecycle is the close/dispose discipline where the SDK releases only resources it created and never a caller-supplied one, with close being idempotent.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:37` · high · sha:f0b3d2058626</sub>

## Conflicts

## Superseded
