# message-bodies

## Rules
- BODY-8 (MUST): A single-use body that owns a closeable source MUST release that source as part of its single write, so that skipping materialization does not leak it.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:10-10` · high · sha:c2bf15dc8a06</sub>
- BODY-8 (MUST): A port MUST decide its stream-ownership rule deliberately rather than assume every single-use body closes its input.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:10-10` · high · sha:c2bf15dc8a06</sub>
- BODY-9 (SHOULD): A stream-backed body of known length SHOULD be treated as replayable when and only when the stream supports mark/reset and the length fits the platform's maximum single-array bound; otherwise it MUST be single-use.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:11-11` · high · sha:c2bf15dc8a06</sub>
- BODY-9 (SHOULD): When a stream-backed body is replayable, each write after the first MUST rewind before reading, with a race-safe rewind permitting at most one reset between any two writes.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:11-11` · high · sha:c2bf15dc8a06</sub>
- HTTP-39 / BODY-10 (MUST): An exact-length copy from a source MUST write precisely the declared count of bytes.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:12-12` · high · sha:c2bf15dc8a06</sub>
- HTTP-39 / BODY-10 (MUST): A premature end of stream during an exact-length copy MUST raise an end-of-file error naming delivered-of-total.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:12-12` · high · sha:c2bf15dc8a06</sub>
- HTTP-39 / BODY-10 (MUST): A zero-length read for a positive requested count MUST be treated as a stream-contract violation, never as an infinite spin or EOF.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:12-12` · high · sha:c2bf15dc8a06</sub>
- HTTP-39 / BODY-10 (MUST): A declared length of 0 MUST be a legitimate empty write.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:12-12` · high · sha:c2bf15dc8a06</sub>
- HTTP-40 / BODY-11 (MUST): A file-backed body MUST be replayable and MUST open a fresh file handle per write.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>
- HTTP-40 / BODY-11 (MUST): A file-backed body MUST validate fail-fast at construction: the file exists, is a regular file, the offset is non-negative and within the size captured at construction, and offset plus count does not exceed that size.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>
- BODY-13 (MUST): A file transfer MUST detect a short write and raise an error naming transferred-of-total.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>
- BODY-12 (SHOULD): A file body SHOULD stream via the platform's most efficient file-to-sink transfer and be recognizable by type so that transports can dispatch a true zero-copy kernel path.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>
- BODY-36 (MAY): A file body MAY expose a read-only memory-mapped view of its byte range for local hashing or signing without heap copying, and in that case it rejects a range larger than a single addressable buffer.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:13-13` · high · sha:c2bf15dc8a06</sub>
- HTTP-38 / BODY-35 (MUST): Body factories MUST classify replayability by source: byte-array, string, buffer, file and serialized bodies are replayable, and a one-shot stream is single-use unless mark/reset applies.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:14-14` · high · sha:c2bf15dc8a06</sub>
- HTTP-38 / BODY-35 (MUST): A form-urlencoded body MUST be replayable and use x-www-form-urlencoded encoding (plus sign for space), which is distinct from RFC 3986 query encoding.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:14-14` · high · sha:c2bf15dc8a06</sub>
- HTTP-38 / BODY-35 (MUST): A body's declared content length MUST report the exact count when known and the -1 sentinel otherwise, and ports MUST NOT assume a known length is always present.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:14-14` · high · sha:c2bf15dc8a06</sub>
- BODY-4 (MUST): Retry, redirect and authentication-challenge replay MUST all consult the body's replayability before re-sending a body-bearing request, and such a request is eligible only when its body reports replayable.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:18-18` · high · sha:c2bf15dc8a06</sub>
- BODY-4 (MUST): A consumed single-use body MUST NOT be re-sent on any retry, redirect or auth-replay path.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:18-18` · high · sha:c2bf15dc8a06</sub>
- BODY-5 (MUST): On the retry path, a body-less request's re-send eligibility MUST gate on method idempotency rather than replayability, so only idempotent methods are retried when there is no body and a body-less non-idempotent POST is not retried.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:19-19` · high · sha:c2bf15dc8a06</sub>
- HTTP-41 / BODY-14 (MUST): A response body MUST be single-use: its read handle is obtained once, and once consumed the bytes are gone.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:23-23` · high · sha:c2bf15dc8a06</sub>
- HTTP-41 / BODY-14 (MUST): Requesting a response body's read handle repeatedly MUST return the same underlying handle, not a replay.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:23-23` · high · sha:c2bf15dc8a06</sub>
- HTTP-41 / BODY-14 (MUST): Repeatable or non-destructive access to a response body MUST require an explicit buffering wrapper.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:23-23` · high · sha:c2bf15dc8a06</sub>
- HTTP-41 / BODY-15 (MUST): Closing a response body MUST release the underlying transport connection and MUST be idempotent.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:24-24` · high · sha:c2bf15dc8a06</sub>
- HTTP-41 / BODY-15 (MUST): Closing a response body MUST NOT assume the body was read, because a caller that skips the body entirely still relies on close to release the connection.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:24-24` · high · sha:c2bf15dc8a06</sub>
- HTTP-43 (MUST): A response MUST be closeable with an idempotent close that forwards to the body, so it can be released in a scoped block whether or not the body was consumed.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:24-24` · high · sha:c2bf15dc8a06</sub>
- HTTP-16-body / BODY-16 (MUST): Convenience readers that materialize the whole body as a string or byte array MUST close the body with a finally-style guarantee whether or not the read succeeds.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:25-25` · high · sha:c2bf15dc8a06</sub>
- HTTP-42 (MUST): Reading a response body as text MUST default its charset to the media type's declared charset, falling back to UTF-8 when none is declared or the declared charset is unknown.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:26-26` · high · sha:c2bf15dc8a06</sub>
- Body handling must never silently emit zero bytes or truncate, never double-consume or double-close, never let logging alter the wire bytes, and always bound in-memory capture.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:3-3` · high · sha:c2bf15dc8a06</sub>
- HTTP-44 (MUST): A lazy typed-response wrapper MUST expose raw status, headers, protocol, reason and request without consuming the body.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:30-30` · high · sha:c2bf15dc8a06</sub>
- HTTP-44 (MUST): A lazy typed-response wrapper MUST parse the typed value at most once on first access and memoize the outcome, so every later access returns the same value or re-throws the same failure without re-running the handler or re-reading the single-use body.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:30-30` · high · sha:c2bf15dc8a06</sub>
- HTTP-44 (MUST): A lazy typed-response wrapper MUST memoize both a null success and a thrown failure.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:30-30` · high · sha:c2bf15dc8a06</sub>
- HTTP-45 (MUST): Concurrent first accesses to a lazy typed-response wrapper MUST be serialized so the handler runs exactly once, using a lock that cooperates with lightweight/virtual-thread schedulers rather than an intrinsic monitor that pins the carrier across the parse.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:30-30` · high · sha:c2bf15dc8a06</sub>
- BODY-17 (MUST): The request-logging wrapper MUST mirror the exact bytes the wrapped body's single write produces into an internal tap while forwarding those same bytes to the transport sink, consuming the upstream exactly once.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- BODY-17 (MUST): The full request payload MUST always reach the transport regardless of any tap cap.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- BODY-18 (MUST): The request-logging tap MUST be cleared at the start of every write so a post-write snapshot reflects only the most recent attempt, and retries against a replayable delegate MUST NOT accumulate.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- BODY-19 (MUST): The request-logging tap MUST be bounded by a configurable cap; once the cap is reached, further bytes stop being copied into the tap while the full payload continues to the transport.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- BODY-20 (SHOULD): If the wrapped write fails partway, the request-logging snapshot SHOULD return the bytes mirrored up to the failure.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- BODY-21 (MUST): The request-logging wrapper MUST expose the delegate's replayability verbatim.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- BODY-21 (MUST): The request-logging wrapper's materialize-once operation MUST return a wrapper around the delegate's replayable form, preserving the tap cap.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- BODY-37 (MUST): The request-logging tee MUST NOT expose a direct writable-buffer handle that bypasses the primary sink (restating IO-28 at the body layer).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- BODY-22 (MUST): The response-logging wrapper MUST drain the delegate at most once, lazily, on first access (read, snapshot or exception query), buffering up to a configurable byte cap.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:38-38` · high · sha:c2bf15dc8a06</sub>
- BODY-22 (MUST): Concurrent first accesses to the response-logging wrapper MUST be serialized so the upstream is read exactly once, via a once-latch or mutex that does not pin the carrier thread.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:38-38` · high · sha:c2bf15dc8a06</sub>
- BODY-23 (MUST): When the whole response body fits within the cap (EOF reached before the cap), the logging wrapper MUST capture it entirely and close the delegate.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:39-39` · high · sha:c2bf15dc8a06</sub>
- BODY-23 (MUST): After fully capturing a body within the cap, the logging wrapper MUST serve every read as a fresh non-consuming view, fully repeatable with each read succeeding independently.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:39-39` · high · sha:c2bf15dc8a06</sub>
- BODY-24 (MUST): When the response body exceeds the cap, the logging wrapper MUST buffer only the prefix and leave the delegate open.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:40-40` · high · sha:c2bf15dc8a06</sub>
- BODY-24 (MUST): When the response body exceeds the cap, the logging wrapper MUST serve the next read as a single-use stream that first replays the captured prefix then continues from the still-live tail, so the consumer receives the complete body.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:40-40` · high · sha:c2bf15dc8a06</sub>
- BODY-24 (MUST): In the over-cap regime of the response-logging wrapper, a second read MUST fail.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:40-40` · high · sha:c2bf15dc8a06</sub>
- BODY-25 (MUST): A delegate read returning zero bytes for a positive requested count MUST be treated as a stream-contract violation (error), never as end-of-stream, and EOF is signaled only by the explicit sentinel.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:41-41` · high · sha:c2bf15dc8a06</sub>
- BODY-26 (MUST): A failure during the response-logging drain MUST NOT silently truncate; the wrapper retains bytes read before the failure and caches the error.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:41-41` · high · sha:c2bf15dc8a06</sub>
- BODY-26 (MUST): After a drain failure, reads MUST re-throw the cached error on every call, the snapshot MUST return the partial bytes without throwing, and an exception-query accessor MUST surface the cached error without triggering a drain.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:41-41` · high · sha:c2bf15dc8a06</sub>
- BODY-27 (MUST): The response-logging wrapper MUST close the delegate at most once across all close paths, with the wrapper's own close and the one-shot tail's close routing through a single shared close-once guard, because some transport streams throw on double-close.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:42-42` · high · sha:c2bf15dc8a06</sub>
- BODY-27 (MUST): If the delegate's close throws, the response-logging wrapper MUST still mark it closed.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:42-42` · high · sha:c2bf15dc8a06</sub>
- BODY-28 (MUST): On the fits-cap path, a close failure after a successful full capture MUST NOT be reported as a drain error nor prevent serving the captured body.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:42-42` · high · sha:c2bf15dc8a06</sub>
- BODY-28 (MUST): The captured in-memory buffer MUST survive the response-logging wrapper's close so that post-mortem snapshot logging still works.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:42-42` · high · sha:c2bf15dc8a06</sub>
- BODY-29 (SHOULD): The response-logging wrapper's reported content length SHOULD be the captured size only when the body is fully captured, and otherwise the delegate's declared length.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:42-42` · high · sha:c2bf15dc8a06</sub>
- HTTP-52 / BODY-30 (MUST): Turning an error response into an exception MUST buffer at most a fixed cap of 1 MiB of the error body into memory and re-serve it as a replayable body, dropping bytes beyond the cap.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>
- HTTP-52 / BODY-30 (MUST): The buffered error-body copy MUST be readable independently and repeatably after the transport connection is released.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>
- HTTP-52 / BODY-30 (MUST): Error-body buffering MUST occur inside the original body's close-guaranteeing scope, so a provider or buffer-allocation failure still releases the connection.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>
- HTTP-52 / BODY-30 (MUST): A response with no body MUST be returned unchanged by error-body buffering.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>
- BODY-31 (MUST): Error-to-exception mapping MUST apply only to 4xx/5xx responses, and a non-error non-success response such as 304 or an unfollowed 3xx MUST be returned with its body intact.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>
- BODY-32 (MUST): Byte-capped snapshot and preview operations MUST reject a negative cap, silently clamp the cap to the platform's maximum single-array size, and return whatever bytes are available up to the clamped cap.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:47-47` · high · sha:c2bf15dc8a06</sub>
- BODY-32 (MUST): A capless snapshot MUST fail loudly when the captured size exceeds the platform maximum rather than attempt an impossible allocation.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:47-47` · high · sha:c2bf15dc8a06</sub>
- BODY-33 (SHOULD): An exception-side error-body preview SHOULD be non-consuming, reading from a fresh peek view, returning null when there is no body and empty when the body is exhausted.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:47-47` · high · sha:c2bf15dc8a06</sub>
- BODY-34 (MUST): Body logging on both request and response sides MUST engage only when body-level logging is enabled.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:48-48` · high · sha:c2bf15dc8a06</sub>
- BODY-34 (MUST): In-memory body capture on both request and response sides MUST be bounded by one shared preview-size configuration.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:48-48` · high · sha:c2bf15dc8a06</sub>
- BODY-34 (MUST): The consumer MUST still receive every byte of an over-preview body; only the logged preview and size fields are bounded.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:48-48` · high · sha:c2bf15dc8a06</sub>
- HTTP-36 / BODY-1 (MUST): A request body MUST produce bytes on demand via a single write-to-sink operation, report its media type (nullable) and its content length (with -1 meaning unknown).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:7-7` · high · sha:c2bf15dc8a06</sub>
- HTTP-36 / BODY-1 (MUST): A request body MUST expose a boolean replayability property that defaults to false (single-use), and replayability MUST be true only when writing more than once yields byte-for-byte identical output.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:7-7` · high · sha:c2bf15dc8a06</sub>
- BODY-2 (MUST): A composite body such as multipart MUST report replayable if and only if every part is replayable.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:8-8` · high · sha:c2bf15dc8a06</sub>
- BODY-2 (MUST): A composite body's declared content length MUST collapse to unknown if any part's length is unknown.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:8-8` · high · sha:c2bf15dc8a06</sub>
- HTTP-51 (SHOULD): A multipart body SHOULD derive its declared length and its written bytes from one shared framing routine so that length cannot drift from bytes written.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:8-8` · high · sha:c2bf15dc8a06</sub>
- HTTP-51 (SHOULD): A multipart body SHOULD generate a spec-valid random boundary and reject a caller-supplied boundary that violates the RFC 2046 grammar.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:8-8` · high · sha:c2bf15dc8a06</sub>
- HTTP-51 (MUST): A multipart body MUST quote or escape part-header parameter values so that CR/LF or a quote character cannot break the framing.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:8-8` · high · sha:c2bf15dc8a06</sub>
- BODY-3 / HTTP-37 (MUST): A materialize-once operation MUST return the same body unchanged when it is already replayable.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:9-9` · high · sha:c2bf15dc8a06</sub>
- BODY-3 / HTTP-37 (MUST): When the body is not replayable, the materialize-once operation MUST drain the body's write output exactly once into an in-memory buffer and return a replayable buffer-backed body, after which the original MUST be treated as consumed.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:9-9` · high · sha:c2bf15dc8a06</sub>
- BODY-3 / HTTP-37 (MUST): A single-use body MUST fail loudly on a second write and never silently emit zero bytes.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:9-9` · high · sha:c2bf15dc8a06</sub>
- BODY-3 / HTTP-37 (MUST): The consume-once guard of a single-use body MUST be race-safe so that under concurrent writes at most one proceeds and the losers observe a clear error.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:9-9` · high · sha:c2bf15dc8a06</sub>
- A composite (multipart or aggregate) body reports IsReplayable as the conjunction over its parts and ContentLength -1 if any part's length is unknown (BODY-2).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:167-169` · high · sha:da6000c93fc5</sub>
- Declared length and written bytes of a composite body come from one framing routine (HTTP-51), and part-header parameter values are quoted and escaped so a CR/LF or quote cannot break the framing.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:169-170` · high · sha:da6000c93fc5</sub>
- The form-urlencoded body is a separate type that is always replayable and uses the form encoder of design section 3.5 (HTTP-38, BODY-35).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:170-171` · high · sha:da6000c93fc5</sub>
- A file-backed body is replayable by opening a fresh FileStream (with FileOptions.SequentialScan) per write.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:172-173` · high · sha:da6000c93fc5</sub>
- File-backed body validation is fail-fast at construction: the file exists and is a regular file, offset is non-negative, count is non-negative or the rest-of-file sentinel, and offset plus count is within the length captured at construction (BODY-11).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:173-175` · high · sha:da6000c93fc5</sub>
- A short file-body transfer throws naming transferred-of-total through the same helper as the exact-length copy of BODY-10/HTTP-39, so the message form cannot diverge (BODY-13).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:175-176` · high · sha:da6000c93fc5</sub>
- The file-backed body type is a sealed, public FileRequestBody so a transport can recognise it (BODY-12).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:176-177` · high · sha:da6000c93fc5</sub>
- A stream-backed body over a seekable stream (Stream.CanSeek, BODY-9) with a known length within Array.MaxLength reports replayable and seeks back to its captured start position before every write after the first.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:181-184` · high · sha:da6000c93fc5</sub>
- The rewind of a seekable stream-backed body is guarded by the same Interlocked flag as the single-use guard so at most one rewind happens between two writes, and any other stream-backed body is single-use.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:183-184` · high · sha:da6000c93fc5</sub>
- ToReplayableAsync returns this when the body already is replayable, and otherwise drains the body's write output exactly once into memory and returns a byte-backed body, leaving the original consumed (BODY-3, HTTP-37).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:185-187` · high · sha:da6000c93fc5</sub>
- The single-use guard is race-safe: a second write throws StreamConsumedException rather than emitting zero bytes (BODY-6), and under concurrent writes at most one proceeds (BODY-7).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:187-189` · high · sha:da6000c93fc5</sub>
- Retry, redirect and the 401 challenge all consult the same IsReplayable before re-sending a body-bearing request but decline differently: retry stops and surfaces the last outcome, auth returns the challenge response unchanged and undisposed, and redirect throws (BODY-4).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:192-195` · high · sha:da6000c93fc5</sub>
- Response bodies are single-use handles: the read handle is obtained once and repeatable access requires the explicit buffering wrapper (BODY-14).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:197-198` · high · sha:da6000c93fc5</sub>
- Closing a response body releases the transport resource, is idempotent, and does not assume the body was read (BODY-15).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:198-199` · high · sha:da6000c93fc5</sub>
- RequestBody needs a synchronous WriteTo(Stream) to back the SerializeToStream override, and ResponseBody needs a synchronous OpenRead() over HttpContent.ReadAsStream().
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:297-299` · high · sha:da6000c93fc5</sub>
- RequestBody and ResponseBody are public abstract classes (the one open hierarchy in the model) whose common variants are private sealed classes reached only through static factories (FromBytes, FromString, FromValue, FromStream, and the planned FromFile, FromForm, Multipart), so invariants are established in one place.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:287-291` · high · sha:3aa554d9287d</sub>
- Body metadata (ContentType, ContentLength, IsReplayable) is immutable, and the only mutable state is the single-use consumption flag and, for response bodies, the live stream, which is HTTP-1's declared carve-out for a body wrapping live single-use stream state.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:292-294` · high · sha:3aa554d9287d</sub>
- FromBytes copies its input (ReadOnlyMemory<byte>.ToArray()) so a caller's later buffer mutation cannot reach the body (XCUT-15).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:294-295` · high · sha:3aa554d9287d</sub>

## Constraints
- The idempotency gate for body-less requests is retry-specific: the redirect path re-sends body-less requests per redirect semantics and does not consult idempotency.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:19-19` · high · sha:c2bf15dc8a06</sub>
- SocketsHttpHandler offers no kernel file-to-socket path for request content, so the most efficient file transfer on this transport is a large-buffer FileStream copy, which is stated rather than claimed as zero-copy.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:177-179` · high · sha:da6000c93fc5</sub>

## Conclusions
- Error bodies are buffered with a cap so that an error payload outlives the connection for inspection while an adversarial gigabyte body cannot exhaust memory.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:46-46` · high · sha:c2bf15dc8a06</sub>
- A request body produces bytes by writing to a Stream (RequestBody.WriteToAsync(Stream, CancellationToken), plus a synchronous WriteTo(Stream) for the real sync path), and a response body is read as a Stream (OpenReadAsync/OpenRead) or drained into byte[]/string; this push-to-a-sink shape matches HttpContent.SerializeToStreamAsync so adapting it needs one small HttpContent subclass (P14).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:102-106` · high · sha:da6000c93fc5</sub>
- Using HttpContent itself as the SDK body type was considered and rejected because it carries a mutable HttpContentHeaders collection (breaking HTTP-1's immutable metadata), ties the core model to one transport library's types (P3, a coupling cost), and conflates single-use and replayable content with no replayability property (HTTP-36).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:106-110` · high · sha:da6000c93fc5</sub>
- A pull-shaped IAsyncEnumerable<ReadOnlyMemory<byte>> body was considered and rejected because the push shape already lets a lazy generator (pagination, streaming multipart) write chunks as it produces them, so pull would add a second representation without a new capability.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:110-112` · high · sha:da6000c93fc5</sub>
- BODY-36's memory-mapped view is a MAY and is offered through MemoryMappedFile only if a signing use case asks for it.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:179-180` · high · sha:da6000c93fc5</sub>
- The single-use guard uses Interlocked.Exchange(ref _consumed, 1), the .NET form of the reference's atomic compare-and-set, with no fiber-owned mutex needed because the Interlocked flip holds nothing across the drain.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:189-191` · high · sha:da6000c93fc5</sub>
- The port preserves the differing decline behaviours of the replay gate rather than unifying them, because BODY-4 says a port need not unify them.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:195-196` · high · sha:da6000c93fc5</sub>
- A second ResponseBody.OpenReadAsync throws StreamConsumedException instead of returning the same Stream again, because returning the same partially read Stream to a second caller lets two consumers silently interleave reads; this deliberate letter-level difference is recorded as design section 10 entry 6.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:199-202` · high · sha:da6000c93fc5</sub>

## Reference
- In the reference implementation the buffered-source-backed body drains and closes its source on write, while raw byte-stream-backed bodies do not close their stream during write (the rewindable variant keeps it open to replay; the one-shot variant leaves the caller-supplied stream unclosed per its documented ownership).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:10-10` · high · sha:c2bf15dc8a06</sub>
- The three re-send paths decline differently and a port need not unify the decline behavior: the retry path stops and surfaces the last outcome, the auth path returns the original challenge response unchanged and does NOT close it, and the redirect path fails loudly.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:18-18` · high · sha:c2bf15dc8a06</sub>
- Conformance for BODY-4: drive a retryable 5xx, a preserved-method redirect and a 401 challenge each with a non-replayable body and assert the documented decline, then repeat with a replayable body and assert re-send.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:18-18` · high · sha:c2bf15dc8a06</sub>
- Conformance for BODY-5: retrying a body-less POST versus a body-less GET against a retryable status re-sends only the GET.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:19-19` · high · sha:c2bf15dc8a06</sub>
- Conformance for BODY-1: a byte-array body is replayable with exact length, a stream-backed body is single-use, and an unknown-length body reports -1; retry, redirect and auth-replay query replayability before deciding whether to buffer or re-send.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:7-7` · high · sha:c2bf15dc8a06</sub>
- In the reference implementation the single-use consume-once guard is an atomic compare-and-set; conformance: writing a stream body twice makes the second throw, materializing then writing twice yields identical output, and a concurrent double-write lets exactly one succeed.
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:9-9` · high · sha:c2bf15dc8a06</sub>
- FromString encodes with GetBytes, which emits no BOM (UTF-8 "a" is the single byte 61).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:295-296` · high · sha:3aa554d9287d</sub>

## Conflicts

## Superseded

