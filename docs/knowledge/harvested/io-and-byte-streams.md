# io-and-byte-streams

## Rules
- The byte-stream provider MUST expose factory operations for a new empty in-memory buffer, a buffered reader over a raw input stream, a buffered reader over a byte array, a buffered writer over a raw output stream, and wrappers adding a buffered surface to a primitive source/sink, and a reader/writer created over a caller's raw stream takes ownership of that stream so closing it closes the underlying stream (SEAM-3).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:7-7` · high · sha:0adae2d6a47f</sub>
- Byte-stream provider factory operations MUST be safe to invoke concurrently from many threads, though the buffer/reader/writer instances they return are not required to be thread-safe and are confined to a single logical operation (SEAM-4).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:8-8` · high · sha:0adae2d6a47f</sub>
- A source read MUST append bytes to the tail of a caller-provided destination buffer (never overwriting existing content) and return the number transferred: at least 1 when the requested count is positive and the source is not exhausted, exactly 0 when the requested count is 0, -1 at end-of-stream, and never more than requested (IO-1).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:7-7` · high · sha:33e67b0b29cd</sub>
- A read of count 0 MUST return 0 and MUST NOT report end-of-stream, even on an exhausted source, because underlying libraries often collapse a zero-byte read against an exhausted stream to -1 (IO-2).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:8-8` · high · sha:33e67b0b29cd</sub>
- A negative count passed to any size-taking read/write/copy operation MUST be rejected as an argument-validation error before any I/O occurs (IO-3).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:9-9` · high · sha:33e67b0b29cd</sub>
- A sink write MUST remove exactly the requested number of bytes from the head of the source buffer and push them downstream; if the source holds fewer, it MUST fail with an I/O error rather than write a partial amount (IO-4).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:10-10` · high · sha:33e67b0b29cd</sub>
- A sink MUST expose flush (pushing buffered bytes toward the destination), and both source and sink MUST be closeable; the sink surface SHOULD distinguish emit (a cheap one-level handoff) from flush (a full force-out), and a pure in-memory buffer MAY make both no-ops returning self (IO-5, IO-18).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:11-11` · high · sha:33e67b0b29cd</sub>
- A buffer snapshot MUST return a fresh, independent byte-array copy of the current contents without consuming or mutating the buffer, so later mutations do not affect a returned snapshot and vice versa (IO-8).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:16-16` · high · sha:33e67b0b29cd</sub>
- Materializing an entire buffer, or a length-bounded slice read, as one contiguous byte array SHOULD refuse sizes exceeding the host's maximum single-array allocation, failing with an actionable message pointing at streaming alternatives (IO-9).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:17-17` · high · sha:33e67b0b29cd</sub>
- Clear MUST discard every byte, and copy-to MUST copy a specified window into another buffer without consuming or mutating the source, defaulting to "from offset through end" and rejecting out-of-range windows (IO-10).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:18-18` · high · sha:33e67b0b29cd</sub>
- Closing a source, sink, or buffer MUST be idempotent: a second close MUST NOT throw, and the underlying resource is closed at most once (IO-41).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:19-19` · high · sha:33e67b0b29cd</sub>
- A buffered source/sink wrapping an external stream MUST reject read/write/flush/emit after close with an I/O error; a purely in-memory buffer is exempt on its own read/write surface (so snapshot-after-close logging still works) but its close MUST still invalidate every slice derived from it (IO-42).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:20-20` · high · sha:33e67b0b29cd</sub>
- exhausted() MUST return true exactly when no more bytes are available, a single-byte read MUST return the next byte or fail with EOF, and a count-less byte-array read MUST return all remaining bytes, empty when already exhausted (IO-11).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:24-24` · high · sha:33e67b0b29cd</sub>
- An exact-count read MUST return exactly the requested count or fail with EOF; it MUST NOT return a short result, since length-prefixed framing needs all-or-nothing exact reads (IO-12).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:25-25` · high · sha:33e67b0b29cd</sub>
- UTF-8 and explicit-charset reads MUST decode with the specified encoding, with symmetric write-side encodings (IO-13).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:26-26` · high · sha:33e67b0b29cd</sub>
- A UTF-8 line read MUST consume the next line terminator and return the preceding bytes as UTF-8, treating both "\n" and "\r\n" as terminators, returning null when exhausted before any byte, returning a final unterminated line as-is, and keeping a lone "\r" not followed by "\n" as line content (IO-14).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:27-27` · high · sha:33e67b0b29cd</sub>
- Skip MUST advance past exactly the requested count, failing with EOF if fewer remain; skip(0) MUST be a no-op even at/after EOF (IO-15).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:28-28` · high · sha:33e67b0b29cd</sub>
- A buffered source SHOULD provide a read-only host-native byte-stream bridge whose single-byte read returns 0–255 or -1 at end and whose bulk read returns the count or -1, closing the bridge closes the owning source; a buffered sink SHOULD symmetrically provide a writable-stream bridge whose close closes the sink (IO-16).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:29-29` · high · sha:33e67b0b29cd</sub>
- A peek MUST return a non-consuming view over the whole remaining source such that reads from it do not advance the original's cursor, enabling repeatable body reads for logging previews and response replay (IO-19).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:33-33` · high · sha:33e67b0b29cd</sub>
- A slice(offset, count) MUST return a non-consuming, length-bounded view exposing at most count bytes starting offset ahead of the current cursor; reads from it MUST NOT advance the parent, and reading past the window behaves as end-of-window (IO-20).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:34-34` · high · sha:33e67b0b29cd</sub>
- Slice offset overflow MUST be detected lazily — constructing a slice whose offset exceeds the source size succeeds, surfacing only on first read as empty/EOF — while a negative offset or count MUST be rejected eagerly at construction (IO-21).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:35-35` · high · sha:33e67b0b29cd</sub>
- Closing a slice MUST NOT close its parent or advance the parent's cursor; closing the parent MUST invalidate every outstanding slice so subsequent reads fail loudly rather than returning stale bytes; reading from a slice after it has been explicitly closed MUST fail loudly with a state error distinct from normal EOF (IO-22, IO-24).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:36-36` · high · sha:33e67b0b29cd</sub>
- Multiple slices and peeks of one source MUST be mutually independent (each with its own cursor and budget), and a slice-of-a-slice MUST compose offsets additively and cap at the outer slice's remaining bytes (IO-23).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:37-37` · high · sha:33e67b0b29cd</sub>
- Even though individual instances are single-threaded, the close state of a source/buffer MUST be observable across threads to slices derived from it, so a close on one thread reliably invalidates a slice being read on another (IO-38).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:38-38` · high · sha:33e67b0b29cd</sub>
- A write-all MUST pump the source to exhaustion into the sink and return the total transferred, terminating only on a -1 read; when pumping a foreign source, a read returning 0 for a non-zero requested count MUST be raised as an I/O error, never tolerated as EOF or spun on forever (IO-17).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:42-42` · high · sha:33e67b0b29cd</sub>
- A tee sink MUST mirror written bytes into an in-memory tap and forward the full, untruncated payload to its primary sink; the wire body MUST never be reduced or altered by the tap (IO-25).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- The tee sink MUST support a tap capacity limit — once reached, further writes stop copying into the tap while still forwarding the full payload; the default limit is effectively unbounded, and a limit of 0 mirrors nothing while forwarding everything (IO-26).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- The tee sink MUST mirror attempted bytes into the tap before forwarding to the primary (so a failed primary write still captures the attempted bytes), and MUST clear its staging buffer even on a failed write so a later write does not prepend stale bytes (IO-27).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- The tee sink MUST NOT expose a direct backing-buffer handle (attempting it fails, directing callers to the typed write methods), because a raw buffer write would reach only the tap or only the primary and silently corrupt the wire body (IO-28).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- The tee's own flush/close/emit MUST forward to the primary only, leaving the in-memory tap intact for later snapshotting (IO-29).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- The I/O provider seam MUST offer factories to create a fresh empty buffer, wrap a caller stream and a byte array as buffered sources, wrap a caller stream as a buffered sink, and wrap a foreign primitive source/sink with the typed surface; each buffer MUST be fresh, independent, and empty, and the byte-array-wrapping source MUST be an independent copy of the input (IO-30).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:47-47` · high · sha:33e67b0b29cd</sub>
- All streaming instances (source, sink, buffered source/sink, buffer, tee) are single-threaded contracts not required to be safe for concurrent use, so callers must serialize external access; independent views MAY be used from different threads but each individual view remains single-threaded, with the close signal being the one cross-thread-visible exception (IO-37).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:48-48` · high · sha:33e67b0b29cd</sub>
- I/O contracts MUST NOT impose their own read/write timeout — the adapter wraps foreign streams with a no-op timeout, delegating deadlines to the transport — and a mirroring/wrapping sink MUST NOT swallow or duplicate the wrapped stream's cancellation handling (IO-40).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:50-50` · high · sha:33e67b0b29cd</sub>
- The request-logging wrapper MUST mirror the exact bytes the wrapped body's single write produces into an internal tap while forwarding those same bytes to the transport sink, consuming the upstream exactly once; the full payload MUST always reach the transport regardless of any tap cap (BODY-17).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- The request-logging tap MUST be cleared at the start of every write so a post-write snapshot reflects only the most recent attempt; retries against a replayable delegate MUST NOT accumulate in the tap (BODY-18).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- The request-logging tap MUST be bounded by a configurable cap; once reached, further bytes stop being copied into the tap while the full payload continues to the transport (BODY-19).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- If the wrapped write fails partway, the request-logging snapshot SHOULD return the bytes mirrored up to the failure (BODY-20).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- The request-logging wrapper MUST expose the delegate's replayability verbatim, and its materialize-once MUST return a wrapper around the delegate's replayable form, preserving the tap cap (BODY-21).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>
- The request-logging tee MUST NOT expose a direct writable-buffer handle that bypasses the primary sink (BODY-37).
  <sub>spec · `docs/product-spec/06-request-and-response-body-lifecycle.md:34-34` · high · sha:c2bf15dc8a06</sub>

## Constraints

## Conclusions

## Reference
- A buffer MUST behave as a FIFO byte queue that is simultaneously a source and a sink — bytes written through the sink surface read back through the source surface in exact order — with a size reflecting bytes currently held (IO-7).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:15-15` · high · sha:33e67b0b29cd</sub>

## Conflicts

## Superseded
