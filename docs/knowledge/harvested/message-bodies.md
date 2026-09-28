# message-bodies

## Rules
- A request body MUST produce bytes on demand via a single write-to-sink operation, report its media type (nullable) and content length (with -1 meaning unknown), and expose a boolean replayability property defaulting to false (single-use); replayability MUST be true only when writing more than once yields byte-for-byte identical output (HTTP-36/BODY-1).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:7-7` · high · sha:c2bf15dc8a06</sub>
- A composite body (e.g. multipart) MUST report replayable iff every part is replayable, and its declared content length MUST collapse to unknown if any part's length is unknown (BODY-2).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:8-8` · high · sha:c2bf15dc8a06</sub>
- A multipart body SHOULD derive its declared length and its written bytes from one shared framing routine so length cannot drift from bytes written, generate a spec-valid random boundary, reject a caller boundary violating the RFC 2046 grammar, and MUST quote/escape part-header parameter values so CR/LF or a quote cannot break the framing (HTTP-51).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:8-8` · high · sha:c2bf15dc8a06</sub>
- A materialize-once operation MUST return the same body unchanged when already replayable, and otherwise drain the body's write output exactly once into an in-memory buffer and return a replayable buffer-backed body, after which the original MUST be treated as consumed; a single-use body MUST fail loudly on a second write, and the consume-once guard MUST be race-safe so that under concurrent writes at most one proceeds and the losers observe a clear error (BODY-3/HTTP-37).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:9-9` · high · sha:c2bf15dc8a06</sub>
- A stream-backed body of known length SHOULD be treated as replayable only when the stream supports mark/reset and the length fits the platform's maximum single-array bound, otherwise it MUST be single-use; when replayable, each write after the first MUST rewind before reading, with a race-safe rewind allowing at most one reset between any two writes (BODY-9).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:11-11` · high · sha:c2bf15dc8a06</sub>
- An exact-length copy from a source MUST write precisely the declared count; a premature end of stream MUST raise an end-of-file error naming delivered-of-total, a zero-length read for a positive request MUST be a stream-contract violation (never an infinite spin or EOF), and a declared length of 0 MUST be a legitimate empty write (HTTP-39/BODY-10).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:12-12` · high · sha:c2bf15dc8a06</sub>
- A file-backed body MUST be replayable, open a fresh file handle per write, and validate fail-fast at construction that the file exists, is a regular file, the offset is non-negative and within the size captured at construction, and offset+count does not exceed that size (HTTP-40/BODY-11).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>
- A file transfer MUST detect a short write and raise an error naming transferred-of-total (BODY-13).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>
- A file body SHOULD stream via the platform's most efficient file-to-sink transfer and be recognizable by type so transports can dispatch a true zero-copy kernel path (BODY-12).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>
- Body factories MUST classify replayability by source — byte-array, string, buffer, file, and serialized bodies are replayable; a one-shot stream is single-use unless mark/reset applies (HTTP-38/BODY-35).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:14-14` · high · sha:c2bf15dc8a06</sub>
- A form-urlencoded body MUST be replayable and use x-www-form-urlencoded encoding ("+" for space), which is distinct from RFC 3986 query encoding (HTTP-38/BODY-35).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:14-14` · high · sha:c2bf15dc8a06</sub>
- A body's declared content length MUST report the exact count when known and the -1 sentinel otherwise; ports MUST NOT assume a known length is always present (HTTP-38/BODY-35).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:14-14` · high · sha:c2bf15dc8a06</sub>
- Retry, redirect, and authentication-challenge replay MUST all consult the body's replayability before re-sending a body-bearing request: such a request is eligible only when its body reports replayable, and a consumed single-use body MUST NOT be re-sent on any of these paths (BODY-4).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:18-18` · high · sha:c2bf15dc8a06</sub>
- On the retry path specifically, a body-less request's re-send eligibility MUST gate on method idempotency rather than replayability, so only idempotent methods are retried when there is no body — a body-less non-idempotent POST is not retried; the redirect path re-sends body-less requests per redirect semantics and does not consult idempotency (BODY-5).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:19-19` · high · sha:c2bf15dc8a06</sub>
- A response body MUST be single-use — its read handle is obtained once and, once consumed, the bytes are gone; requesting the handle repeatedly returns the same underlying handle, not a replay, so repeatable/non-destructive access requires an explicit buffering wrapper (HTTP-41/BODY-14).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:23-23` · high · sha:c2bf15dc8a06</sub>
- Closing a response body MUST release the underlying transport connection, MUST be idempotent, and MUST NOT assume the body was read — a caller that skips the body entirely still relies on close to release the connection (HTTP-41/BODY-15).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:24-24` · high · sha:c2bf15dc8a06</sub>
- A response MUST be closeable, with its close idempotent and forwarding to the body, so it can be released in a scoped block whether or not the body was consumed (HTTP-43).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:24-24` · high · sha:c2bf15dc8a06</sub>
- Convenience readers that materialize the whole body (as string or byte array) MUST close the body in a finally-style guarantee whether or not the read succeeds (BODY-16).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:25-25` · high · sha:c2bf15dc8a06</sub>
- Reading a response body as text MUST default its charset to the media type's declared charset, falling back to UTF-8 when none is declared or the declared charset is unknown (HTTP-42).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:26-26` · high · sha:c2bf15dc8a06</sub>
- A lazy typed-response wrapper MUST expose raw status/headers/protocol/reason/request without consuming the body, and MUST parse the typed value at most once on first access, memoizing the outcome so every later access returns the same value or re-throws the same failure without re-running the handler or re-reading the single-use body; both a null success and a thrown failure MUST be memoized (HTTP-44).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:30-30` · high · sha:c2bf15dc8a06</sub>
- Concurrent first accesses to a lazy typed-response wrapper MUST be serialized so the handler runs exactly once, using a lock that cooperates with lightweight/virtual-thread schedulers rather than an intrinsic monitor that pins the carrier across the parse (HTTP-45).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:30-30` · high · sha:c2bf15dc8a06</sub>
- The response-logging wrapper MUST drain the delegate at most once, lazily, on first access (read/snapshot/exception query), buffering up to a configurable byte cap, with concurrent first accesses serialized so the upstream is read exactly once via a once-latch/mutex that does not pin the carrier thread (BODY-22).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:38-38` · high · sha:c2bf15dc8a06</sub>
- When the whole body fits within the cap (EOF reached before the cap), the response-logging wrapper MUST capture it entirely, close the delegate, and thereafter serve every read as a fresh non-consuming view, fully repeatable, with each read succeeding independently (BODY-23).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:39-39` · high · sha:c2bf15dc8a06</sub>
- When the body exceeds the cap, the response-logging wrapper MUST buffer only the prefix, leave the delegate open, and serve the next read as a single-use stream that first replays the captured prefix then continues from the still-live tail so the consumer receives the complete body; a second read in this regime MUST fail (BODY-24).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:40-40` · high · sha:c2bf15dc8a06</sub>
- A delegate read returning zero bytes for a positive requested count MUST be a stream-contract violation (an error), never end-of-stream; EOF is signaled only by the explicit sentinel (BODY-25).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:41-41` · high · sha:c2bf15dc8a06</sub>
- A failure during the response-logging drain MUST NOT silently truncate — the wrapper retains bytes read before the failure and caches the error such that reads re-throw it every call, snapshot returns the partial bytes without throwing, and an exception-query accessor surfaces the cached error without triggering a drain (BODY-26).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:41-41` · high · sha:c2bf15dc8a06</sub>
- The response-logging wrapper MUST close the delegate at most once across all close paths, routing the wrapper's own close and the one-shot tail's close through a single shared close-once guard, because some transport streams throw on double-close; if the delegate's close throws it MUST still be marked closed (BODY-27).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:42-42` · high · sha:c2bf15dc8a06</sub>
- On the fits-cap path, a close failure after a successful full capture MUST NOT be reported as a drain error nor prevent serving the captured body, and the captured in-memory buffer MUST survive the wrapper's close so post-mortem snapshot logging still works (BODY-28).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:42-42` · high · sha:c2bf15dc8a06</sub>
- Reported content length SHOULD be the captured size only when fully captured, otherwise the delegate's declared length (BODY-29).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:42-42` · high · sha:c2bf15dc8a06</sub>

## Constraints

## Conclusions
- A single-use body that owns a closeable source MUST release that source as part of its single write so skipping materialization does not leak it, and a port must decide its stream-ownership rule deliberately rather than assume every single-use body closes its input (BODY-8).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:10-10` · high · sha:c2bf15dc8a06</sub>
- The retry, auth, and redirect paths query the same replayability property but decline differently: the retry path stops and surfaces the last outcome, the auth path returns the original challenge response unchanged without closing it, and the redirect path fails loudly (BODY-4).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:18-18` · high · sha:c2bf15dc8a06</sub>

## Reference
- The reference implementation of the consume-once guard for materializing a body is an atomic compare-and-set. (BODY-3, HTTP-37)
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:9-9` · high · sha:c2bf15dc8a06</sub>
- In the reference implementation, the buffered-source-backed body's write drains and closes the source, while the raw byte-stream-backed bodies do not close their stream during write — the rewindable variant keeps it open to replay and the one-shot variant leaves the caller-supplied stream unclosed per its documented ownership. (BODY-8)
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:10-10` · high · sha:c2bf15dc8a06</sub>
- A file body MAY expose a read-only memory-mapped view of its byte range for local hashing/signing without heap copying, rejecting a range larger than a single addressable buffer (BODY-36).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>

## Conflicts

## Superseded
