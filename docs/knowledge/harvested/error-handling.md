# error-handling

## Rules
- The buffered copy of an error body MUST be readable independently and repeatably after the transport connection is released, and buffering MUST occur inside the original body's close-guaranteeing scope so a provider/buffer-allocation failure still releases the connection; a response with no body MUST be returned unchanged (HTTP-52/BODY-30).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>
- Error-to-exception mapping MUST apply only to 4xx/5xx responses; a non-error non-success response (e.g. 304, an unfollowed 3xx) MUST be returned with its body intact (BODY-31).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>
- Byte-capped snapshot/preview operations MUST reject a negative cap, silently clamp the cap to the platform's maximum single-array size, and return whatever bytes are available up to the clamped cap; a capless snapshot MUST fail loudly when the captured size exceeds the platform maximum rather than attempt an impossible allocation (BODY-32).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:47-47` · high · sha:c2bf15dc8a06</sub>
- An exception-side error-body preview SHOULD be non-consuming, reading from a fresh peek view and returning null when there is no body and empty when exhausted (BODY-33).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:47-47` · high · sha:c2bf15dc8a06</sub>
- Body logging on both request and response sides MUST engage only when body-level logging is enabled, and the in-memory capture on both sides MUST be bounded by one shared preview-size configuration; the consumer MUST still receive every byte of an over-preview body since only the logged preview and size fields are bounded (BODY-34).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:48-48` · high · sha:c2bf15dc8a06</sub>
- The error taxonomy MUST have exactly two top-level branches — protocol errors carrying a fully-received response raised as an unchecked/runtime error, and transport errors carrying no response that belong to the runtime's I/O-error family — with a transport error always reporting itself retryable at the error level. (XCUT-4)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:15` · high · sha:d6123be82c9e</sub>
- The baked retryability flag of a protocol error MUST be computed once at construction from a single shared status classifier rather than hardcoded per subclass. (XCUT-5, XCUT-7)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:16` · high · sha:d6123be82c9e</sub>
- A transport-family or custom error type that declares itself retryable via the retryability capability MUST participate in retry decisions without any edit to the classifier, since the classifier queries the capability rather than matching a concrete type. (XCUT-6, XCUT-7)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:17` · high · sha:d6123be82c9e</sub>
- The status-to-exception mapping factory MUST reject being asked to map a non-error status (1xx/2xx/3xx) by raising an argument error rather than fabricating a successful exception, though a convenience form MAY return an absent/null value for non-error statuses instead. (XCUT-8)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:19` · high · sha:d6123be82c9e</sub>
- Any classification that walks an error's cause chain MUST be cycle-safe, tracking visited causes by reference identity and terminating on a self-referential or cyclic chain. (XCUT-9)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:20` · high · sha:d6123be82c9e</sub>

## Constraints
- Turning an error response into an exception MUST buffer at most a fixed cap of 1 MiB of the error body into memory and re-serve it as a replayable body, dropping bytes beyond the cap (HTTP-52/BODY-30).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>

## Conclusions

## Reference
- A protocol error's baked retryability flag and the retry step's configurable retryable-status set are distinct notions with distinct default membership, and the configured set is what the retry step actually consults. (XCUT-5, XCUT-7)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:5` · high · sha:d6123be82c9e</sub>
- The shared status classifier treats HTTP status 408, 429, and all 5xx statuses except 501 and 505 as retryable, and everything else as not retryable. (XCUT-5, XCUT-7)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:16` · high · sha:d6123be82c9e</sub>
- A protocol error is an error meaning a complete response was received but its status is 4xx/5xx, expressed as an unchecked/runtime error carrying the response.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:47` · high · sha:f0b3d2058626</sub>
- A transport error is a failure that produced no response, such as connect refused, DNS/TLS failure, read timeout, or peer reset, belonging to the runtime's I/O-error family and always-retryable at the error level.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:65` · high · sha:f0b3d2058626</sub>

## Conflicts

## Superseded
