# io-and-byte-streams

## Rules
- SEAM-3 (MUST) - The byte-stream provider MUST expose factory operations to create a new empty in-memory buffer, a buffered reader over a raw input stream, a buffered reader over a byte array, a buffered writer over a raw output stream, and wrappers that add the buffered surface to a primitive source/sink.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:7-7` · high · sha:0adae2d6a47f</sub>
- A reader or writer created over a caller's raw stream TAKES OWNERSHIP of that stream, so closing the reader/writer closes it (SEAM-3).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:7-7` · high · sha:0adae2d6a47f</sub>
- SEAM-4 (MUST) - Provider factory operations MUST be safe to invoke concurrently from many threads, while the buffer/reader/writer instances they return are NOT required to be thread-safe and are confined to a single logical operation.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:8-8` · high · sha:0adae2d6a47f</sub>
- IO-1 (MUST): A source read MUST append bytes to the tail of a caller-provided destination buffer without overwriting existing content and return the number transferred.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:7-7` · high · sha:33e67b0b29cd</sub>
- IO-1 (MUST): A source read returns at least 1 when the requested count is positive and the source is not exhausted, exactly 0 when the requested count is 0, -1 at end-of-stream, and never more than requested.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:7-7` · high · sha:33e67b0b29cd</sub>
- IO-2 (MUST): A read of count 0 MUST return 0 and MUST NOT report end-of-stream, even on an exhausted source.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:8-8` · high · sha:33e67b0b29cd</sub>
- IO-3 (MUST): A negative count passed to any size-taking read, write or copy MUST be rejected as an argument-validation error before any I/O, and a port MAY use whichever argument-error type is idiomatic.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:9-9` · high · sha:33e67b0b29cd</sub>
- IO-4 (MUST): A sink write MUST remove exactly the requested number of bytes from the head of the source buffer and push them downstream.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:10-10` · high · sha:33e67b0b29cd</sub>
- IO-4 (MUST): If the source buffer holds fewer bytes than requested, the sink write MUST fail with an I/O error rather than write a partial amount.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:10-10` · high · sha:33e67b0b29cd</sub>
- IO-5 (MUST): A sink MUST expose flush, pushing buffered bytes toward the destination, and both source and sink MUST be closeable.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:11-11` · high · sha:33e67b0b29cd</sub>
- IO-18 (SHOULD): The sink surface SHOULD distinguish emit (a cheap one-level handoff) from flush (a full force-out), and a pure in-memory buffer MAY make both no-ops returning self.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:11-11` · high · sha:33e67b0b29cd</sub>
- IO-7 (MUST): A buffer MUST behave as a FIFO byte queue that is simultaneously a source and a sink, where bytes written through the sink surface read back through the source surface in exact order, with a size reflecting the bytes currently held.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:15-15` · high · sha:33e67b0b29cd</sub>
- IO-8 (MUST): A buffer snapshot MUST return a fresh, independent byte-array copy of the current contents without consuming or mutating the buffer, so later mutations do not affect a returned snapshot and vice versa.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:16-16` · high · sha:33e67b0b29cd</sub>
- IO-9 (SHOULD): Materializing an entire buffer, or a length-bounded slice read, as one contiguous byte array SHOULD refuse sizes exceeding the host's maximum single-array allocation, failing with an actionable message pointing at streaming alternatives rather than a low-level allocation crash.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:17-17` · high · sha:33e67b0b29cd</sub>
- IO-10 (MUST): Clear MUST discard every byte.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:18-18` · high · sha:33e67b0b29cd</sub>
- IO-10 (MUST): Copy-to MUST copy a specified window into another buffer without consuming or mutating the source, defaulting to from-offset-through-end and rejecting out-of-range windows.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:18-18` · high · sha:33e67b0b29cd</sub>
- IO-41 (MUST): Closing a source, sink or buffer MUST be idempotent: a second close MUST NOT throw and the underlying resource is closed at most once.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:19-19` · high · sha:33e67b0b29cd</sub>
- IO-42 (MUST): A buffered source or sink wrapping an external stream MUST reject read, write, flush and emit after close with an I/O error.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:20-20` · high · sha:33e67b0b29cd</sub>
- IO-42 (MUST): A purely in-memory buffer is exempt on its own read/write surface after close, so snapshot-after-close logging still works, but its close MUST still invalidate every slice derived from it.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:20-20` · high · sha:33e67b0b29cd</sub>
- IO-11 (MUST): exhausted() MUST return true exactly when no more bytes are available and may block waiting on an upstream source.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:24-24` · high · sha:33e67b0b29cd</sub>
- IO-11 (MUST): A single-byte read MUST return the next byte or fail with EOF, and a count-less byte-array read MUST return all remaining bytes, empty when already exhausted.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:24-24` · high · sha:33e67b0b29cd</sub>
- IO-12 (MUST): An exact-count read MUST return exactly the requested count or fail with EOF and MUST NOT return a short result.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:25-25` · high · sha:33e67b0b29cd</sub>
- IO-13 (MUST): UTF-8 and explicit-charset reads MUST decode with the specified encoding, with symmetric write-side encodings, so non-ASCII text round-trips through UTF-8 and through a non-UTF-8 charset such as ISO-8859-1.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:26-26` · high · sha:33e67b0b29cd</sub>
- IO-14 (MUST): A UTF-8 line read MUST consume the next line terminator and return the preceding bytes as UTF-8, treating both \n and \r\n as terminators.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:27-27` · high · sha:33e67b0b29cd</sub>
- IO-14 (MUST): A UTF-8 line read returns null when exhausted before any byte, returns a final unterminated line as-is, and keeps a lone \r not followed by \n as line content.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:27-27` · high · sha:33e67b0b29cd</sub>
- IO-15 (MUST): Skip MUST advance past exactly the requested count, failing with EOF if fewer remain, and skip(0) MUST be a no-op even at or after EOF.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:28-28` · high · sha:33e67b0b29cd</sub>
- IO-16 (SHOULD): A buffered source SHOULD provide a read-only host-native byte-stream bridge whose single-byte read returns 0-255 or -1 at end and whose bulk read returns the count or -1, and closing the bridge closes the owning source.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:29-29` · high · sha:33e67b0b29cd</sub>
- IO-16 (SHOULD): A buffered sink SHOULD provide a writable-stream bridge whose close closes the sink.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:29-29` · high · sha:33e67b0b29cd</sub>
- IO-19 (MUST): A peek MUST return a non-consuming view over the whole remaining source such that reads from it do not advance the original's cursor.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:33-33` · high · sha:33e67b0b29cd</sub>
- IO-20 (MUST): slice(offset, count) MUST return a non-consuming, length-bounded view exposing at most count bytes starting offset ahead of the current cursor; reads from it MUST NOT advance the parent, and reading past the window behaves as end-of-window.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:34-34` · high · sha:33e67b0b29cd</sub>
- IO-21 (MUST): Slice offset overflow MUST be detected lazily: constructing a slice whose offset exceeds the source size MUST succeed and surface only on first read as empty/EOF.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:35-35` · high · sha:33e67b0b29cd</sub>
- IO-21 (MUST): A negative slice offset or count MUST be rejected eagerly at construction.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:35-35` · high · sha:33e67b0b29cd</sub>
- IO-22 (MUST): Closing a slice MUST NOT close its parent or advance the parent's cursor.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:36-36` · high · sha:33e67b0b29cd</sub>
- IO-22 (MUST): Closing the parent MUST invalidate every outstanding slice so subsequent reads fail loudly rather than returning stale bytes.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:36-36` · high · sha:33e67b0b29cd</sub>
- IO-24 (MUST): Reading from a slice after it has been explicitly closed MUST fail loudly with a state error, distinct from normal EOF.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:36-36` · high · sha:33e67b0b29cd</sub>
- IO-23 (MUST): Multiple slices and peeks of one source MUST be mutually independent, each with its own cursor and budget.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:37-37` · high · sha:33e67b0b29cd</sub>
- IO-23 (MUST): A slice-of-a-slice MUST compose offsets additively and cap at the outer slice's remaining bytes.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:37-37` · high · sha:33e67b0b29cd</sub>
- IO-38 (MUST): The close state of a source or buffer MUST be observable across threads to slices derived from it, so a close on one thread reliably invalidates a slice being read on another with no torn or stale reads, even though individual instances are single-threaded (IO-37).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:38-38` · high · sha:33e67b0b29cd</sub>
- IO-17 (MUST): A write-all MUST pump the source to exhaustion into the sink and return the total transferred, terminating only on a -1 read.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:42-42` · high · sha:33e67b0b29cd</sub>
- IO-17 (MUST): When pumping a foreign source, a read returning 0 for a non-zero requested count MUST be raised as an I/O error (a source-contract violation), never tolerated as EOF or spun on forever.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:42-42` · high · sha:33e67b0b29cd</sub>
- IO-25 (MUST): A tee sink MUST mirror written bytes into an in-memory tap and forward the full, untruncated payload to its primary sink; the wire body MUST never be reduced or altered by the tap.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- IO-26 (MUST): The tee MUST support a tap capacity limit: once reached, further writes stop copying into the tap while still forwarding the full payload; the default limit is effectively unbounded and a limit of 0 mirrors nothing while forwarding everything.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- IO-27 (MUST): The tee MUST mirror attempted bytes into the tap before forwarding to the primary, so a failed primary write still captures the attempted bytes.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- IO-27 (MUST): The tee MUST clear its staging buffer even on a failed write so a later write does not prepend stale bytes.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- IO-28 (MUST): The tee MUST NOT expose a direct backing-buffer handle, with attempts failing and directing callers to the typed write methods.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- IO-29 (MUST): The tee's own flush, close and emit MUST forward to the primary only, leaving the in-memory tap intact for later snapshotting.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- IO-30 (MUST): The provider seam MUST offer factories to create a fresh empty buffer, wrap a caller stream and a byte array as buffered sources, wrap a caller stream as a buffered sink, and wrap a foreign primitive source or sink with the typed surface.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:47-47` · high · sha:33e67b0b29cd</sub>
- IO-30 (MUST): Each provider-created buffer MUST be fresh, independent and empty, and the byte-array-wrapping source MUST be an independent copy of the input.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:47-47` · high · sha:33e67b0b29cd</sub>
- IO-37 (MUST): Independent views MAY be used from different threads, but each individual view remains single-threaded, with the close signal of IO-38 the one cross-thread-visible exception.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:48-48` · high · sha:33e67b0b29cd</sub>
- IO-39 (SHOULD): The provider registry SHOULD support lock-free reads of the active provider while serializing installs and swaps under a lock that does not pin or park carrier threads under lightweight-thread schedulers.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:49-49` · high · sha:33e67b0b29cd</sub>
- IO-40 (MUST): The I/O contracts MUST NOT impose their own read/write timeout; the adapter wraps foreign streams with a no-op timeout, delegating deadlines to the transport.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:50-50` · high · sha:33e67b0b29cd</sub>
- IO-40 (MUST): A mirroring or wrapping sink MUST NOT swallow or duplicate the wrapped stream's cancellation handling.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:50-50` · high · sha:33e67b0b29cd</sub>
- Every public byte surface in the SDK is a Stream or a ReadOnlyMemory<byte>; a parallel buffered-source type is forbidden as the parallel vocabulary P14 prohibits and a fabricated tier in P11's sense.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:27-29` · high · sha:da6000c93fc5</sub>
- For IO-40 (no own timeout), core never sets ReadTimeout or WriteTimeout on a stream, and deadlines are CancellationTokens owned by the transport.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:44-44` · high · sha:da6000c93fc5</sub>
- Core never issues a zero-count read, and every internal pump requests at least one byte, asserted in debug builds.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:61-63` · high · sha:da6000c93fc5</sub>
- Every internal read that needs a count uses ReadExactlyAsync or loops, and a single ReadAsync is only used as "give me what is available".
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:72-73` · high · sha:da6000c93fc5</sub>
- Materialising helpers refuse sizes above Array.MaxLength with a message pointing at the streaming alternative, and BODY-32's clamp uses Array.MaxLength.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:82-83` · high · sha:da6000c93fc5</sub>
- Captured body bytes are never pooled; ArrayPool<byte> is used only for transient copy buffers that no view can reach.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:96-98` · high · sha:da6000c93fc5</sub>
- If a later optimisation pools a capture buffer, its views must check a shared Volatile-read closed flag, which is IO-38's cross-thread visibility on .NET.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:98-99` · high · sha:da6000c93fc5</sub>
- There is exactly one decode boundary, ResponseBody.ReadAsStringAsync, which resolves the charset through MediaType.Charset and falls back to UTF-8 when it is absent or unknown (HTTP-42).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:125-127` · high · sha:da6000c93fc5</sub>
- Core encodes text with Encoding.GetBytes or new UTF8Encoding(false), never with a StreamWriter over Encoding.UTF8, so a request body does not start with an unrequested BOM.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:131-134` · high · sha:da6000c93fc5</sub>
- The MediaType.Charset lookup catches both ArgumentException and NotSupportedException and treats UTF-7 as unknown, so HTTP-24's "return null (not throw) when absent or unknown" holds even for a hostile server sending charset=utf-7.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:142-145` · high · sha:da6000c93fc5</sub>
- Core never calls Encoding.RegisterProvider, so charset=windows-1252 resolves to null and decodes as UTF-8 unless the host application registered the provider itself, which is HTTP-24's fall-back behaviour and is documented as such.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:148-150` · high · sha:da6000c93fc5</sub>

## Constraints
- IO-37 (MUST): All streaming instances (source, sink, buffered source/sink, buffer, tee) are single-threaded contracts, are not required to be safe for concurrent use, and callers serialize external access.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:48-48` · high · sha:33e67b0b29cd</sub>
- System.IO.Pipelines (PipeReader/PipeWriter) is in Microsoft.NETCore.App only from .NET 9, verified absent from the 8.0.31 reference pack, so on the net8.0 target it is a NuGet dependency and core does not use it.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:69-71` · high · sha:d7cea7b15cf3</sub>
- Stream.Read returns 0 both at end of stream and for a zero-count request (verified for MemoryStream and a live HttpClient response stream), so the end-of-stream sentinel is 0 rather than -1, collapsing the distinction IO-2 exists to protect.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:57-61` · high · sha:da6000c93fc5</sub>
- A source that returns 0 early on an unknown-length body cannot be detected, since no host can distinguish it from a genuine end.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:67-68` · high · sha:da6000c93fc5</sub>
- Network reads are short (a ReadAsync of 64 KiB on a chunked HttpClient response stream returned 3,974 bytes), so code assuming full reads truncates.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:71-73` · high · sha:da6000c93fc5</sub>
- StreamReader.ReadLine and StringReader.ReadLine over "a\rb\r\nc\nd" both return a|b|c|d, treating a lone \r as a terminator, which IO-14 forbids.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:74-77` · high · sha:da6000c93fc5</sub>
- StreamReader silently strips a UTF-8 BOM and owns an internal buffer that makes slice-window boundaries invisible.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:77-78` · high · sha:da6000c93fc5</sub>
- The CLR has a maximum single-array length, Array.MaxLength (2,147,483,591), so IO-9's host bound is literal on .NET.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:81-83` · high · sha:da6000c93fc5</sub>
- Encoding.UTF8.GetPreamble() and Encoding.GetEncoding("utf-8").GetPreamble() return EF BB BF, GetBytes("a") writes no BOM, new StreamWriter(stream, Encoding.UTF8) writes EF BB BF 61, and new StreamWriter(stream) writes 61.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:129-133` · high · sha:da6000c93fc5</sub>
- Encoding.UTF8.GetString over EF BB BF 68 69 returns three characters with the first being U+FEFF, while StreamReader over the same bytes returns two, so decoding does not strip a BOM.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:135-137` · high · sha:da6000c93fc5</sub>
- Encoding.GetEncoding("bogus") throws ArgumentException but Encoding.GetEncoding("utf-7") throws NotSupportedException (SYSLIB0001, UTF-7 disabled).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:140-142` · high · sha:da6000c93fc5</sub>
- Code-page encodings such as windows-1252 throw ArgumentException until Encoding.RegisterProvider(CodePagesEncodingProvider.Instance) is called, and registration is process-global.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:146-148` · high · sha:da6000c93fc5</sub>

## Conclusions
- A zero-count read returns 0 rather than -1 because underlying libraries often collapse a zero-byte read against an exhausted stream to -1, making callers falsely conclude EOF.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:8-8` · high · sha:33e67b0b29cd</sub>
- Oversized single-array materialization is refused with a streaming pointer because multi-GB bodies must stream.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:17-17` · high · sha:33e67b0b29cd</sub>
- The after-close rule is split between stream-backed and in-memory buffers because porters commonly err in one of two directions: leaving a closed stream-backed sink writable (a resource-safety hole) or making an in-memory buffer throw after close (breaking snapshot-after-close body logging).
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:20-20` · high · sha:33e67b0b29cd</sub>
- Exact-count reads are all-or-nothing because length-prefixed framing needs them.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:25-25` · high · sha:33e67b0b29cd</sub>
- Line reads have exact semantics because SSE and header parsing need line behavior that survives slice-window boundaries.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:27-27` · high · sha:33e67b0b29cd</sub>
- Peek views exist because repeatable body reads, such as logging previews and response replay, require reading the same bytes without disturbing the primary consumer.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:33-33` · high · sha:33e67b0b29cd</sub>
- Slice offset overflow is lazy because callers may slice speculatively before the full body length is known.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:35-35` · high · sha:33e67b0b29cd</sub>
- A misbehaving foreign source must fail loudly rather than hang or truncate a body.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:42-42` · high · sha:33e67b0b29cd</sub>
- The tee hides its backing buffer because a raw buffer write would reach only the tap or only the primary and silently corrupt the wire body.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:43-43` · high · sha:33e67b0b29cd</sub>
- Prompt cancellation of blocked I/O belongs to the transport that owns the real socket, not to the in-memory I/O layer.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:50-50` · high · sha:33e67b0b29cd</sub>
- The "no standard byte-stream type good enough" constraint DOES NOT HOLD on .NET because Stream (with ReadExactly/ReadAtLeast since .NET 7), Memory<byte>/Span<byte>, IBufferWriter<byte>, MemoryStream and ArrayPool<byte> ship with the runtime.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:65-68` · high · sha:d7cea7b15cf3</sub>
- The byte-stream provider seam retires on .NET but its behavioural contract does not, and is mapped onto the Stream contract with its differences named (§3.1).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:67-69` · high · sha:d7cea7b15cf3</sub>
- The .NET port retires the byte-stream provider seam while keeping its behavioural contract, because Stream, Memory<byte>/Span<byte>, IBufferWriter<byte>/ArrayBufferWriter<byte>, MemoryStream, ArrayPool<byte> and Encoding ship in the Microsoft.NETCore.App shared framework, so choosing them is choosing the platform rather than taking a dependency (P2).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:15-18` · high · sha:da6000c93fc5</sub>
- The retirement argument rests only on types in the net8.0 reference pack and does not lean on System.IO.Pipelines, which is a NuGet package on that floor; the logging facade does not weaken it because each package is its own SEAM-1 cost and a stream library would be a second one.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:18-21` · high · sha:da6000c93fc5</sub>
- With no factory, installation call or discovery, SEAM-5 to SEAM-10, their mirrors IO-30 to IO-36 and IO-39, and XCUT-23 as applied to the byte-stream seam are moot, recorded as design section 10 entry 3.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:21-24` · high · sha:da6000c93fc5</sub>
- The .NET port does not ship an Okio-shaped Source/Sink/Buffer vocabulary, because Stream already has exact-count reads and a captured byte[] gives non-consuming views for free, whereas the Ruby port had to build its own Buffer/BufferedSource/BufferedSink.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:24-29` · high · sha:da6000c93fc5</sub>
- The IO family's invariants are carried by the BCL Stream contract, a small set of internal core helpers (line reader, tee, capped drain, exact-length copy), and the rule that captured bytes live in arrays core owns.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:31-35` · high · sha:da6000c93fc5</sub>
- For IO-5 and IO-18, flush is carried by Stream.Flush/FlushAsync, while Stream has no emit tier so the SHOULD about emit versus flush is not reproduced and emit retires.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:41-41` · high · sha:da6000c93fc5</sub>
- IO-1, IO-2, IO-11, IO-15 and IO-16 (the -1 sentinel, zero-count read, exhausted(), skip, native-stream bridge) retire in letter because the surfaces they constrain do not exist, as the SDK's byte surface already is the host-native stream; their invariants are restated.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:53-53` · high · sha:da6000c93fc5</sub>
- Because a zero-length read on .NET is EOF by contract, a misbehaving source cannot make core spin (the loop terminates on 0) nor silently truncate a known-length body, since the exact-length copy compares delivered against declared and throws EndOfStreamException naming delivered-of-total.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:63-67` · high · sha:da6000c93fc5</sub>
- The anti-spin and anti-truncation invariants (IO-17, BODY-10, BODY-25) are satisfied differently on .NET but still satisfied (P6); the letter of the sentinel requirements is recorded as design section 10 entry 4.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:63-70` · high · sha:da6000c93fc5</sub>
- Core ships one internal UTF-8 line reader over a Stream with its own buffer instead of using StreamReader.ReadLine.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:78-79` · high · sha:da6000c93fc5</sub>
- On .NET a non-consuming view is new MemoryStream(array, index, count, writable: false, publiclyVisible: false) over bytes core already captured, giving each view an independent cursor and budget (IO-23), making a view of a view an offset calculation, and making reads of a disposed view throw ObjectDisposedException distinct from EOF (IO-24).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:91-93` · high · sha:da6000c93fc5</sub>
- Parent-close invalidation (P1) exists in the reference because its segment pool can recycle a segment so a stale slice reads someone else's bytes; a captured byte[] never returned to a pool cannot be recycled, so a stale read is impossible by construction on .NET.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:93-96` · high · sha:da6000c93fc5</sub>
- The inverse adapter (presenting an #each-shaped body as a buffered source) retires on .NET because the response body already is a Stream and every consumer (JsonSerializer.DeserializeAsync, the line reader, the capped drain) takes a Stream directly.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:114-118` · high · sha:da6000c93fc5</sub>
- The design strips a leading BOM when, and only when, it matches the resolved charset's preamble, because HTTP-42 is silent on the point; recorded as section 11 item 34.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:137-139` · high · sha:da6000c93fc5</sub>

## Reference
- SEAM-3 rationale is that the provider is the single seam between core and a concrete streams library and ownership-on-wrap lets pipeline code close one object and know the descriptor is released; conformance is to wrap a spy stream, close the returned reader/writer, and assert the spy closed exactly once.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:7-7` · high · sha:0adae2d6a47f</sub>
- SEAM-4 conformance is to hammer the factories from N threads and assert no shared-state corruption; rationale is that factories are called under concurrency but per-stream objects are cheap and confined.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:8-8` · high · sha:0adae2d6a47f</sub>
- The I/O layer is a minimal, pluggable byte-streaming abstraction on which bodies, body-logging snapshots and serde are built; the core ships only interfaces and a concrete implementation is installed or auto-discovered per section 3.6.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:3-3` · high · sha:33e67b0b29cd</sub>
- Provider resolution follows IO-31 through IO-36, which carry the same precedence, idempotence, caching, warning and de-dup rules as SEAM-5 through SEAM-10.
  <sub>spec · `docs/product-spec/05-i-o-contracts.md:47-47` · high · sha:33e67b0b29cd</sub>
- SEAM-3 demands a pluggable factory producing an empty buffer, buffered readers over a raw stream and a byte array, buffered writers, and wrappers adding a buffered surface to a primitive source or sink, with ownership transferring on wrap.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:5-7` · high · sha:da6000c93fc5</sub>
- SEAM-4 requires the byte-stream factory to be concurrency-safe while the instances it returns need not be.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:7-8` · high · sha:da6000c93fc5</sub>
- The byte-stream provider seam is pluggable in the reference platform only because that platform's adequate stream library is third-party and SEAM-1 bars core from depending on one.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:8-9` · high · sha:da6000c93fc5</sub>
- IO-3 (negative count rejected before I/O) is carried for free by Stream.Read(buf, 0, -1) throwing ArgumentOutOfRangeException.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:39-39` · high · sha:da6000c93fc5</sub>
- IO-4 (a write transfers exactly the requested bytes or fails) is carried for free because Stream.Write has no partial-write result and writes all or throws.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:40-40` · high · sha:da6000c93fc5</sub>
- IO-12 (exact-count read or EOF, never short) is carried for free by ReadExactly/ReadExactlyAsync throwing EndOfStreamException on a short source.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:42-42` · high · sha:da6000c93fc5</sub>
- IO-37 (instances single-threaded) is carried for free by the documented Stream contract.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:43-43` · high · sha:da6000c93fc5</sub>
- IO-41 (close idempotent) is carried for free because disposing a Stream twice does not throw.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:45-45` · high · sha:da6000c93fc5</sub>
- IO-42 (stream-backed rejects use after close; in-memory snapshot survives close) is carried for free because a disposed stream throws ObjectDisposedException while MemoryStream.ToArray() still returns the bytes after Dispose and ReadByte/Write throw.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:46-46` · high · sha:da6000c93fc5</sub>
- IO-8, IO-9 and IO-10 (snapshot copies, host array bound, windowed copy) are met by core helpers where snapshots are ToArray() copies and the host maximum is Array.MaxLength, 2,147,483,591, with IO-9 applied literally.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:47-47` · high · sha:da6000c93fc5</sub>
- IO-13 (charset decode, symmetric encode) is carried by Encoding, with ISO-8859-1 in-box, subject to the encoding gotchas.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:48-48` · high · sha:da6000c93fc5</sub>
- IO-14 (line read where \n and \r\n terminate and a lone \r is content) is met by a hand-written internal line reader, because StreamReader.ReadLine is the wrong tool.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:49-49` · high · sha:da6000c93fc5</sub>
- IO-17 (write-all pump) is carried by Stream.CopyToAsync plus the exact-length copy of BODY-10.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:50-50` · high · sha:da6000c93fc5</sub>
- IO-19 to IO-24 and IO-38 (peeks and slices) are met by core helpers using read-only MemoryStream views over a captured array, each view having its own cursor.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:51-51` · high · sha:da6000c93fc5</sub>
- IO-25 to IO-29 (tee sink) are met by an internal write-only TeeStream.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:52-52` · high · sha:da6000c93fc5</sub>
- IO-19 to IO-24 require peeks and slices that do not advance the parent, compose additively, and are invalidated when the parent closes, and IO-38 requires that close signal to be visible across threads.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:88-91` · high · sha:da6000c93fc5</sub>
- Lazy offset overflow (IO-21) holds because a view whose window starts past the captured length is constructed successfully and reads as empty.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:99-100` · high · sha:da6000c93fc5</sub>
- Bytes on the wire are byte[]/ReadOnlyMemory<byte> and carry no encoding.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:124-125` · high · sha:da6000c93fc5</sub>
- The as-built MediaType.Charset catches only ArgumentException, so MediaType.Parse("text/plain; charset=utf-7").Charset throws instead of returning null, violating HTTP-24.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:142-145` · high · sha:da6000c93fc5</sub>
- As built at d45e64b the body seam is partial: bodies are Stream-based with byte, string, value and single-use stream factories and a race-safe consumed flag, while the synchronous WriteTo/OpenRead, file, form, multipart and seekable-stream bodies, the exact-length copy, the line reader, the tee and both logging wrappers are missing.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:241-244` · high · sha:da6000c93fc5</sub>
- As built at d45e64b, MediaType.Charset throws on utf-7 and ReadAsStringAsync keeps a leading BOM.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:243-244` · high · sha:da6000c93fc5</sub>

## Conflicts

## Superseded

