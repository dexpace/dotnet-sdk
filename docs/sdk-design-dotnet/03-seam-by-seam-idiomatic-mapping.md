## 3. Seam-by-Seam Idiomatic Mapping

### 3.1 The byte-stream provider seam → retired; its contract mapped onto `Stream`

**What the reference requires and why.** **SEAM-3** demands a pluggable factory producing an empty buffer, buffered
readers over a raw stream and a byte array, buffered writers, and wrappers adding a buffered surface to a primitive
source or sink, with ownership transferring on wrap; **SEAM-4** requires the factory be concurrency-safe while the
returned instances need not be. The pluggability exists for one reason: the reference platform's adequate stream
library is third-party and **SEAM-1** bars core from depending on one.

**The porter's question.** Does .NET ship, with the runtime, a byte abstraction good enough to build a wire protocol
on — exact-count reads, charset decode, line reads honouring every terminator (**IO-14**), non-consuming views, a
tee mirroring writes into a bounded tap — and is the full behavioural contract still implementable over it?

**The .NET answer.** Yes, so the *seam* retires while the *contract* stays. `Stream` (with `ReadExactly` and
`ReadAtLeast`), `Memory<byte>`/`Span<byte>`, `IBufferWriter<byte>`/`ArrayBufferWriter<byte>`, `MemoryStream`,
`ArrayPool<byte>` and `Encoding` are in the `Microsoft.NETCore.App` shared framework on every supported target;
choosing them is choosing the platform, not taking a dependency (P2). The argument rests only on types in the
`net8.0` reference pack: it does not lean on `System.IO.Pipelines`, which is a NuGet package on that floor (§1, §2.4),
and the one package core does take — the logging facade of §2.4 — does not weaken it, because each package is its own
**SEAM-1** cost and a stream library would be a second one. There is no factory, no installation call and no
discovery, which makes **SEAM-5**–**SEAM-10**, their mirrors **IO-30**–**IO-36** and **IO-39**, and **XCUT-23** as
applied to this seam moot — recorded as §10 entry 3. It is the same simplification the
Ruby port makes, for the same mechanical reason, and on .NET it goes one step further than in Ruby: the Ruby port
still had to build its own `Buffer`/`BufferedSource`/`BufferedSink` because `IO` lacks exact-count reads and
non-consuming views, whereas `Stream` has the first and a captured `byte[]` gives the second for free. So this port
does **not** ship an Okio-shaped `Source`/`Sink`/`Buffer` vocabulary. Every public byte surface in the SDK is a
`Stream` or a `ReadOnlyMemory<byte>`; inventing a parallel buffered-source type would be exactly the parallel
vocabulary P14 forbids and a fabricated tier in P11's sense.

**How the IO contract maps onto `Stream`.** The IO family was written against a source/sink/buffer API; its
invariants survive and are carried by three things: the BCL `Stream` contract itself, a small set of `internal` core
helpers where core is the consumer (the line reader, the tee, the capped drain, the exact-length copy), and the rule
that captured bytes live in arrays core owns. Every row below was verified on 10.0.401 unless the row says
otherwise.

| Requirement(s) | .NET carrier | Status |
|---|---|---|
| **IO-3** negative count rejected before I/O | `Stream.Read(buf, 0, -1)` throws `ArgumentOutOfRangeException` | free |
| **IO-4** a write transfers exactly the requested bytes or fails | `Stream.Write` has no partial-write result: it writes all or throws | free |
| **IO-5**, **IO-18** flush; emit vs flush | `Stream.Flush`/`FlushAsync`; `Stream` has no emit tier, and the SHOULD is not reproduced | flush free; emit retires |
| **IO-12** exact-count read or EOF, never short | `ReadExactly`/`ReadExactlyAsync` throw `EndOfStreamException` on a short source | free |
| **IO-37** instances single-threaded | the documented `Stream` contract | free |
| **IO-40** no own timeout | core never sets `ReadTimeout`/`WriteTimeout`; deadlines are `CancellationToken`s owned by the transport | by rule |
| **IO-41** close idempotent | `Stream.Dispose` twice does not throw | free |
| **IO-42** stream-backed rejects use after close; in-memory snapshot survives close | a disposed stream throws `ObjectDisposedException`; `MemoryStream.ToArray()` still returns the bytes after `Dispose` while `ReadByte`/`Write` throw | free, and exactly the split the requirement asks for |
| **IO-8**, **IO-9**, **IO-10** snapshot copies, host array bound, windowed copy | snapshots are `ToArray()` copies; the host maximum is `Array.MaxLength`, 2,147,483,591 | core helpers; **IO-9** literal (below) |
| **IO-13** charset decode, symmetric encode | `Encoding`; ISO-8859-1 is in-box | free, with the gotchas below |
| **IO-14** line read: `\n` and `\r\n` terminate, lone `\r` is content | hand-written internal line reader — `StreamReader.ReadLine` is the wrong tool (below) | core helper |
| **IO-17** write-all pump | `Stream.CopyToAsync`, plus the exact-length copy of **BODY-10** | free / core helper |
| **IO-19**–**IO-24**, **IO-38** peeks and slices | read-only `MemoryStream` views over a captured array; each view has its own cursor | core helper (below) |
| **IO-25**–**IO-29** tee sink | internal write-only `TeeStream` | core helper (below) |
| **IO-1**, **IO-2**, **IO-11**, **IO-15**, **IO-16** −1 sentinel, zero-count read, `exhausted()`, `skip`, native-stream bridge | the surfaces they constrain do not exist: the SDK's byte surface already *is* the host-native stream | letter retires; invariants restated (below) |

Four notes on the .NET specifics, each the result of verification rather than documentation:

- **The end-of-stream sentinel is 0, not −1, and that collapses the distinction IO-2 exists to protect (P13).**
  `Stream.Read` returns 0 at end of stream *and* for a zero-count request: verified, a `MemoryStream` at EOF returns 0
  for both `Read(buf, 0, 0)` and `Read(buf, 0, 1)`, and a live `HttpClient` response stream returns 0 for a zero-count
  read mid-body. **IO-2**'s rationale ("underlying libraries often collapse a zero-byte read against an exhausted
  stream to −1, making callers falsely conclude EOF") describes the BCL contract exactly. The port cannot change
  `Stream`, so it moves the guarantee to the caller side: **core never issues a zero-count read**, and every internal
  pump requests at least one byte, asserted in debug builds. The consequence for **IO-17**, **BODY-10** and
  **BODY-25** (a zero-length read for a positive request count is a stream-contract violation, never end-of-stream) is
  that on .NET that reading *is* EOF by contract, so a misbehaving source cannot make core spin — the loop terminates
  on 0 — and cannot make core truncate a *known-length* body silently, because the exact-length copy compares
  delivered against declared and throws `EndOfStreamException` naming delivered-of-total. What cannot be detected is a
  source that returns 0 early on an unknown-length body, which no host can distinguish from a genuine end. This
  changes how the anti-spin and anti-truncation invariants are satisfied, not whether (P6); the letter of the sentinel
  requirements is recorded as §10 entry 4.
- **Network reads are short; code that assumes otherwise truncates.** Verified: `ReadAsync` of 64 KiB on a chunked
  `HttpClient` response stream returned 3,974 bytes. Every internal read that needs a count uses `ReadExactlyAsync`
  or loops; a single `ReadAsync` is only ever used as "give me what is available".
- **IO-14's line semantics are hand-implemented, and `StreamReader.ReadLine` is the obvious-but-wrong tool.**
  Verified: `StreamReader.ReadLine` and `StringReader.ReadLine` over `"a\rb\r\nc\nd"` both return `a|b|c|d` — a lone
  `\r` is treated as a terminator, which **IO-14** forbids ("keeping a lone `\r` not followed by `\n` as line
  content"). `StreamReader` also strips a UTF-8 BOM silently and owns an internal buffer that makes slice-window
  boundaries invisible. Core therefore ships one internal UTF-8 line reader over a `Stream` with its own buffer. The
  one place in the SDK that *wants* a lone `\r` to terminate is the WHATWG SSE parser (§7.2), which uses the same
  reader in its SSE mode rather than borrowing `StreamReader`'s accidental agreement.
- **IO-9's host bound exists on .NET, so it is literal.** Unlike Ruby, the CLR has a maximum single-array length,
  `Array.MaxLength` (2,147,483,591, verified), and materialising helpers refuse sizes above it with a message
  pointing at the streaming alternative, and **BODY-32**'s clamp uses it. Because 2 GiB is far above anything a
  client SDK should hold in one array, a separate configurable materialisation cap (default 64 MiB, the Ruby port's
  figure and its argument: an order of magnitude above a sane payload, an order below a typical worker's heap)
  applies first and is part of the options surface (§8.2).

**Non-consuming views and why parent-close invalidation reduces to a pooling rule.** **IO-19**–**IO-24** require
peeks and slices that do not advance the parent, compose additively, and are invalidated when the parent closes;
**IO-38** requires that close signal to be visible across threads. On .NET a view is
`new MemoryStream(array, index, count, writable: false, publiclyVisible: false)` over bytes core has already
captured: each view has an independent cursor and budget (**IO-23**), a view of a view is an offset calculation, and
reading a disposed view throws `ObjectDisposedException`, distinct from EOF (**IO-24**). The reason the reference
needs parent-close invalidation (P1) is its segment pool: after close, a segment may be recycled and a stale slice
would read someone else's bytes. A captured `byte[]` that is never returned to a pool cannot be recycled, so a stale
read is impossible by construction. The design therefore states the invariant as a rule: **captured body bytes are
never pooled** — `ArrayPool<byte>` is used only for transient copy buffers that no view can reach — and if a later
optimisation pools a capture buffer, its views must check a shared `Volatile`-read closed flag, which is **IO-38**'s
cross-thread visibility on .NET. Lazy offset overflow (**IO-21**) falls out: a view whose window starts past the
captured length is constructed successfully and reads as empty.

**The canonical body representation.** A request body produces its bytes by writing to a `Stream`
(`RequestBody.WriteToAsync(Stream, CancellationToken)`, with a synchronous `WriteTo(Stream)` for the real sync path
of §3.2); a response body is read as a `Stream` (`OpenReadAsync`/`OpenRead`) or drained into `byte[]`/`string`. This
is the push-to-a-sink shape `HttpContent.SerializeToStreamAsync` already uses, so adapting it is one small
`HttpContent` subclass (P14). The alternative considered and rejected is to use `HttpContent` itself as the SDK's
body type: it is in-box, so **SEAM-1** would allow it, but it carries a mutable `HttpContentHeaders` collection
(breaking **HTTP-1**'s immutable metadata surface), ties the core model to one transport library's types (P3: this
is a coupling cost, not a dependency cost), and conflates single-use and replayable content behind one type with no
replayability property (**HTTP-36**). A pull-shaped `IAsyncEnumerable<ReadOnlyMemory<byte>>` body was also
considered; the push shape already lets a lazy generator (pagination, streaming multipart) write chunks as it
produces them, so the pull shape would add a second representation without adding a capability.

**The inverse adapter retires, and `leaveOpen` replaces its ownership rule.** The Ruby port needed one adapter to
present an `#each`-shaped body as a buffered source for SSE, serde and error buffering. On .NET the response body
already is a `Stream`, and every consumer — `JsonSerializer.DeserializeAsync`, the line reader, the capped drain —
takes a `Stream` directly. What survives is the ownership rule the Ruby adapter carried: a consumer downstream of
the owner of a response must not close it. The .NET idiom for that is the `leaveOpen` parameter, and its default is
the P13 trap: verified, `new StreamReader(stream)` **closes the caller's stream when the reader is disposed**, and
only `new StreamReader(stream, leaveOpen: true)` does not. Every core wrapper over a stream core did not open is
constructed with `leaveOpen: true` (or its library-specific equivalent), which is also what **SEAM-21** and
**SERDE-3** require of the codec (§3.4).

**Encoding, stated once.** Bytes on the wire are `byte[]`/`ReadOnlyMemory<byte>` and carry no encoding. There is
exactly one decode boundary, `ResponseBody.ReadAsStringAsync`, which resolves the charset through
`MediaType.Charset` and falls back to UTF-8 when it is absent or unknown (**HTTP-42**). Four verified .NET facts make
that boundary more than one line:

- **`Encoding.UTF8` carries a BOM preamble, and whether it is written depends on the API.**
  `Encoding.UTF8.GetPreamble()` and `Encoding.GetEncoding("utf-8").GetPreamble()` are `EF BB BF`; `GetBytes("a")`
  writes no BOM (`61`); but `new StreamWriter(stream, Encoding.UTF8)` writes `EF BB BF 61`, while `new
  StreamWriter(stream)` writes `61`. A request body written through a `StreamWriter` with the "obvious" encoding
  therefore starts with three bytes the server did not ask for. Core encodes text with `Encoding.GetBytes` or with
  `new UTF8Encoding(false)`, never with a `StreamWriter` over `Encoding.UTF8`.
- **Decoding does not strip a BOM.** `Encoding.UTF8.GetString` over `EF BB BF 68 69` returns three characters, the
  first U+FEFF; `StreamReader` over the same bytes returns two. So a UTF-8 response with a BOM decodes, today, to a
  string that starts with an invisible character and fails string comparison. The design strips a leading BOM when
  — and only when — it matches the resolved charset's preamble; **HTTP-42** is silent on the point, so the
  resolution is recorded as §11 item 34.
- **An unknown charset is not always an `ArgumentException`.** `Encoding.GetEncoding("bogus")` throws
  `ArgumentException`, but `Encoding.GetEncoding("utf-7")` throws `NotSupportedException` ("Support for UTF-7 is
  disabled", SYSLIB0001). The as-built `MediaType.Charset` catches only `ArgumentException`, so
  `MediaType.Parse("text/plain; charset=utf-7").Charset` throws instead of returning `null` — a violation of
  **HTTP-24**'s "return null (not throw) when absent or unknown" that a hostile server can trigger on any text
  response. The lookup catches both, and treats UTF-7 as unknown.
- **The code-page encodings are opt-in per process, and a library must not opt in.** `windows-1252` throws
  `ArgumentException` until someone calls `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`, after which
  it resolves (both verified). Registration is process-global, so core never calls it; `charset=windows-1252`
  therefore resolves to `null` and decodes as UTF-8 unless the host application registered the provider itself, which
  is **HTTP-24**'s fall-back behaviour and is documented as such.

**Two ownership rules, deliberately different.** At the I/O-helper layer, wrapping takes ownership unless told
otherwise, exactly as the BCL's own wrappers do (**IO-6**, and the `leaveOpen` default above): an internal line
reader or tee built over a stream core opened disposes it. At the body layer the rule is inverted and uniform: **a
body closes exactly the sources it opened itself** — a file-backed body opens and disposes a fresh `FileStream` per
write (**BODY-11**), and `RequestBody.FromStream(stream)` never disposes the caller's stream. `ResponseBody.FromStream`
is the one factory documented as *taking* ownership, because its callers are transports handing over a stream they
opened for exactly that purpose, and the response is the object the caller disposes. **BODY-8** requires this choice
to be made deliberately ("A port MUST decide its stream-ownership/close rule deliberately rather than assume every
single-use body closes its input"); making it one way for all request bodies resolves the reference's admitted
per-variant inconsistency and is recorded as §10 entry 5.

**Body variants and the two logging wrappers.** The body contract is one abstract type per direction, but the BODY
family draws five distinctions inside it and adds two wrappers on top. Naming them here keeps them from being
rediscovered per call site.

- **Composite bodies.** A multipart or otherwise aggregate body reports `IsReplayable` as the conjunction over its
  parts and reports `ContentLength` −1 if any part's is unknown (**BODY-2**). Declared length and written bytes come
  from one framing routine (**HTTP-51**), and part-header parameter values are quoted and escaped so a CR/LF or quote
  cannot break the framing. The form-urlencoded body is separate, always replayable, and uses §3.5's form encoder
  (**HTTP-38**, **BODY-35**).
- **File-backed bodies.** Replayable by opening a fresh `FileStream` (with `FileOptions.SequentialScan`) per write;
  validation is fail-fast at construction — the file exists and is a regular file, offset non-negative, count
  non-negative or the rest-of-file sentinel, offset plus count within the length captured at construction
  (**BODY-11**). A short transfer throws naming transferred-of-total, through the same helper as the exact-length
  copy of **BODY-10**/**HTTP-39**, so the message form cannot diverge (**BODY-13**). The type is a sealed, public
  `FileRequestBody` so a transport can recognise it (**BODY-12**); `SocketsHttpHandler` offers no kernel
  file-to-socket path for request content, so the "most efficient transfer" on this transport is a large-buffer
  `FileStream` copy, which is stated rather than claimed as zero-copy. **BODY-36**'s memory-mapped view is a MAY
  and is offered through `MemoryMappedFile` only if a signing use case asks for it.
- **Seekable streams (BODY-9).** Java's mark/reset is .NET's `Stream.CanSeek`. A stream-backed body over a seekable
  stream with a known length within `Array.MaxLength` reports replayable and seeks back to its captured start
  position before every write after the first, with the rewind guarded by the same `Interlocked` flag as the
  single-use guard so at most one rewind happens between two writes; any other stream-backed body is single-use.
- **Materialise-once.** `ToReplayableAsync` returns `this` when the body already is replayable, and otherwise drains
  the body's write output exactly once into memory and returns a byte-backed body, leaving the original consumed
  (**BODY-3**, **HTTP-37**). The single-use guard is race-safe: a second write throws `StreamConsumedException`
  rather than emitting zero bytes (**BODY-6**), and under concurrent writes at most one proceeds (**BODY-7**). The
  reference's mechanism — an atomic compare-and-set — is exactly `Interlocked.Exchange(ref _consumed, 1)`, which is
  what the as-built stream body uses; unlike the Ruby port there is no fiber-owned mutex to avoid, because an
  `Interlocked` flip holds nothing across the drain.
- **The replay gate.** Retry, redirect and the 401 challenge all consult the same `IsReplayable` before re-sending a
  body-bearing request and decline differently — retry stops and surfaces the last outcome, auth returns the
  challenge response unchanged and undisposed, redirect throws (**BODY-4**) — which the port preserves rather than
  unifies, because **BODY-4** says a port need not unify the decline behaviour. A body-less request's retry
  eligibility gates on method idempotency instead (**BODY-5**), and that gate is retry-only.
- **Response bodies are single-use handles.** The read handle is obtained once and repeatable access requires the
  explicit buffering wrapper below (**BODY-14**). Close releases the transport resource, is idempotent, and does not
  assume the body was read (**BODY-15**, §3.7). One letter-level difference is deliberate: a second `OpenReadAsync`
  throws `StreamConsumedException` instead of returning the same `Stream` again (verified as built). Returning the
  same, partially read `Stream` to a second caller is how two consumers silently interleave reads; throwing is the
  louder form of the same "not a replay" guarantee. Recorded as §10 entry 6.

Two wrappers sit above the variants, and between them they are what the internal `TeeStream` exists to serve.

- **The request-logging wrapper** wraps the destination `Stream` handed to the delegate's single write in a
  write-only `TeeStream` that mirrors each chunk into a capped tap *before* forwarding it, so the wire receives every
  byte and the upstream is consumed exactly once (**BODY-17**, **IO-25**). The tap is cleared at the start of every
  write so a snapshot reflects only the most recent attempt (**BODY-18**, **IO-27**); it stops copying at the cap
  while forwarding continues, so a multi-gigabyte upload mirrors only a preview (**BODY-19**, **IO-26**); and because
  mirroring precedes forwarding, a primary-side failure still leaves the failing chunk captured (**BODY-20**).
  `Flush` and `Dispose` go to the primary only (**IO-29**). The tap is never a `MemoryStream` handed to anyone:
  verified, `new MemoryStream().TryGetBuffer(out _)` is `true` and exposes the backing array, while a stream built
  with `publiclyVisible: false` refuses (`TryGetBuffer` is `false`, `GetBuffer` throws
  `UnauthorizedAccessException`). Snapshots are copies, so no caller can write around the primary (**IO-28**,
  **BODY-37**). The wrapper reports the delegate's replayability verbatim and its `ToReplayableAsync` wraps the
  delegate's replayable form with the cap preserved (**BODY-21**).
- **The response-logging wrapper** drains the delegate at most once, lazily, on first access — a read, a snapshot,
  or the cached-error query — buffering up to the cap, with concurrent first accesses serialised so the upstream is
  read exactly once (**BODY-22**). The .NET latch is a `SemaphoreSlim(1, 1)` awaited with `WaitAsync`, because C#
  forbids `await` inside `lock` (CS1996) and a `SemaphoreSlim` wait parks no thread — the .NET reading of the
  requirement's "does not pin the carrier thread". The drain runs under the wrapper's own lifetime token, not the
  first caller's, so one caller cancelling cannot poison the shared drain for the others. Two regimes follow:
  within the cap the wrapper captures everything, disposes the delegate, and serves every later read as a fresh
  read-only view — fully repeatable (**BODY-23**); over the cap it buffers only the prefix, leaves the delegate
  open, and serves the next read as a single-use concatenating stream that replays the prefix and then continues
  from the live tail, with a second read failing (**BODY-24**). A mid-drain failure retains the bytes already read
  and caches the exception, so reads rethrow it (via `ExceptionDispatchInfo`, preserving the original stack), a
  snapshot returns the partial bytes without throwing, and the error query never triggers a drain (**BODY-26**).
  Every close path — the wrapper's and the tail stream's — routes through one `Interlocked` close-once guard, and a
  delegate whose `Dispose` throws is still marked closed (**BODY-27**); on the fits-cap path a close failure after a
  complete capture is reported through §3.7's quiet-dispose helper, never as a drain error, and the captured array
  outlives the wrapper (**BODY-28**). Reported length is the captured size only when capture was complete
  (**BODY-29**).

Both wrappers engage only when body-level logging is enabled and share one preview-size setting (**BODY-34**).
Error-to-exception mapping is a separate concern that applies only to error statuses: a 304, or a 3xx no redirect
step followed, is returned with its body intact (**BODY-31**); the bounded error-body copy it needs is specified once
in §5.1.

**As built (d45e64b):** partial: request/response bodies are `Stream`-based with byte, string, value and
single-use stream factories and a race-safe consumed flag; missing are the synchronous `WriteTo`/`OpenRead`, the
file, form, multipart and seekable-stream bodies, the exact-length copy, the line reader, the tee and both logging
wrappers; `MediaType.Charset` throws on `utf-7`; `ReadAsStringAsync` keeps a leading BOM.

**As built (2026-10-03, phase 3a):** partial. Built: the exact-length copy behind `RequestBody.FromStream` (**HTTP-39**), the
synchronous `WriteTo`, `ToReplayable`, `OpenRead`, `ReadAsBytes` and `ReadAsString`, and the internal helpers in
`Dexpace.Sdk.Core.IO` (`StreamCopy`, `TeeStream`, `CapturedBytes`, `Utf8LineReader`, `BodyMaterializer`,
`BoundedBufferStream`, `PooledChunk`). Still unbuilt, phase 3b's: the file, form, multipart and seekable-stream bodies,
both logging wrappers, the dispose latches and the BOM strip. Four wordings above are corrected, each by the ruling named;
the text above stands as written. *Dated correction*.

- **The materialisation cap applies to the response-side readers only (P3a-12).** "Applies first to materialising helpers"
  is read as `ReadAsBytes(Async)` and `ReadAsString(Async)`, which refuse more than `ResponseBody.DefaultMaxMaterializedBytes`
  (64 MiB, a constant until phase 5a) with the new public `BodyTooLargeException`. `RequestBody.ToReplayable(Async)` is
  bounded only by `Array.MaxLength` (**IO-9**), because a request's size is the caller's own data and refusing a deliberate
  100 MiB upload-with-retries after consuming it would leave the caller with neither the stream nor a copy.
- **The sync members are virtual with a `NotSupportedException` default (P3a-5).** `WriteTo` and `OpenRead` throw, naming the
  subclass, unless overridden; `ToReplayable`, `ReadAsBytes` and `ReadAsString` are built over them. The precedent is
  `HttpContent.SerializeToStream`. Every SDK variant overrides both forms and shares one consume latch between them.
- **"Lazy offset overflow falls out" needs a clamp.** `new MemoryStream(array, index, count)` rejects `index > array.Length`
  eagerly, so `CapturedBytes.Slice` clamps the offset and count to the window (**IO-21**).
- **"Never pooled" is mechanised (P3a-8).** `BannedSymbols.txt` bans `ArrayPool<T>.Rent` and `MemoryPool<T>.Rent` in `src/`;
  the one sanctioned rent is the internal `PooledChunk`, behind a scoped pragma. Two further bans (stream timeouts, P3a-14;
  `TextReader.ReadLine`, **IO-14**) keep **IO-40** and the line-reader rule honest.

**As built (2026-10-03, phase 3b):** built. Request bodies: `RequestBody.FromForm` (the WHATWG serializer, **HTTP-38**), `FromFile` and the public sealed
`FileRequestBody` (**HTTP-40**, **BODY-11**–**BODY-13**), `Multipart` and `MultipartPart` (**HTTP-51**, **BODY-2**), and the seekable promotion of `FromStream`
(**BODY-9**). Response side: the latched `ResponseBody` and `Response` disposal (**HTTP-41**, **HTTP-43**, **BODY-15**), readers that dispose the body
(**BODY-16**), the BOM strip in one shared `TextDecoding` routine (**HTTP-42**), and `StreamClosedException` for a stream body opened after dispose. The two logging
wrappers are built and internal (`LoggingRequestBody`, `LoggingResponseBody`, **BODY-17**–**BODY-29**, **BODY-32**, **BODY-37**); phase 5b engages them. Not built, by
design: the kernel file-to-socket path of **BODY-12** (🚫), a memory-mapped view (**BODY-36**, declined). Three wordings above are corrected, each by the ruling named; the
text above stands as written. *Dated correction*.

- **A readable, seekable stream with a declared length is replayable (P3b-4).** `RequestBody.FromStream` captures the stream's position at construction; every write,
  the first included, seeks to it and copies exactly the declared length through the exact-length copy, under an in-flight latch that makes a concurrent second write
  throw `InvalidOperationException`. A length of `-1` stays single-use: the length is never inferred from `Length - Position`, which would turn a chunked upload into a
  `Content-Length` one behind the caller's back. The caller's stream position moves and the stream stays the caller's (**BODY-8**).
- **The response-logging drain runs under a linked token (P3b-10).** The sentence "runs under the wrapper's own lifetime token, not the first caller's" is replaced by: a
  token linked from the accessor that starts the drain and the wrapper's lifetime (cancelled by `Dispose`). Read literally, a hung body would block the first reader with no
  way to cancel it, and a call's deadline would stop working whenever body logging is on. A cancelled starter fails the drain like any other failure: the partial bytes are
  kept, the `OperationCanceledException` is cached (**BODY-26**), and later reads rethrow it. "Upstream read exactly once" (**BODY-22**) is kept. The sync and async
  drains are twins over the same capture state, because core bans blocking on a task.
- **The variants.** The file body opens a fresh read-only `FileStream` per write with `FileShare.ReadWrite | FileShare.Delete` and `bufferSize: 0`; the multipart body
  computes its framing once at construction and guards each part's length with an internal non-disposing `BoundedWriteStream`; the over-cap response stream reads at most
  one byte past the cap, so the overflow is one staged byte.

### 3.2 The synchronous transport seam → kept, as `IHttpClient`, with a real synchronous path

**SEAM-11** specifies a single-operation contract whose response body "MUST NOT be pre-buffered by the transport — the
caller owns reading and closing it" and **SEAM-12** requires transports be safe for concurrent calls with all
per-request state confined to locals or the returned response graph. Both are pure intent and both hold on .NET
unchanged.

**The seam is a nominal interface, because .NET has no other kind.** Ruby's seam was a duck type (`#call`); C#
dispatches through declared interfaces, so the seam is `IHttpClient`, and the async one `IAsyncHttpClient` (§3.3).
The interfaces keep the `I` prefix, and the async member keeps the `Async` suffix, although
`docs/styleguide/csharp/02-naming-conventions.md` says to drop both; the Framework Design Guidelines are the stronger
authority for a public NuGet surface, a prefix-less `HttpClient` would collide with `System.Net.Http.HttpClient` in
every consumer's scope, and the SDK ships `Execute` next to `ExecuteAsync`, so the suffix carries information. That
departure is indexed in the `docs/styleguide/README.md` overlay and recorded as §10 entry 27.
**SEAM-11**'s conformance clause — "a bare send lambda works as a transport" — needs a small concession to nominal
typing: a C# lambda cannot implement an interface, so core ships `DelegateHttpClient.Create(...)` factories that wrap
a `Func<Request, RequestOptions, CancellationToken, Response>` (and the `Task<Response>` form) in a sealed adapter
with a no-op dispose. A `HttpPipeline` implements both interfaces so a configured pipeline can stand in wherever a
transport is expected (**PIPE-26**, §5).

The shape, with per-call options threaded through the single abstract member (**SEAM-11**, **TRANSPORT-5**,
**ASYNC-19**):

```csharp
public interface IHttpClient : IDisposable
{
    Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken);
}

public static class HttpClientExtensions
{
    public static Response Execute(this IHttpClient client, Request request,
        CancellationToken cancellationToken = default);                 // passes RequestOptions.Empty
}
```

The option-less convenience is an **extension method, not a default interface member**, and that choice is a P13
catch: a default interface member is invisible through a class-typed reference — verified, calling a DIM overload
on a variable typed as the implementing class fails to compile (CS7036) — so a consumer holding a
`SystemNetHttpClient` would not see the overload. Options are inert data (§4), so an options-ignoring transport
behaves identically with and without them, as **SEAM-11** requires. The `CancellationToken` parameter is new relative
to the as-built `Execute(Request)`: without it the blocking seam has no cancellation vehicle at all, and **SEAM-13**
("Blocking transport implementations SHOULD honor cooperative cancellation ... during blocking I/O") is unanswerable.

**The synchronous path must be real, and on .NET it can be.** `HttpClient.Send` has driven a genuinely synchronous
`SocketsHttpHandler` exchange since .NET 5 — verified, a synchronous `Send` of a GET completes against a local
server. Three conditions make the whole path synchronous rather than sync-over-async, and all three are missing as
built. The adapter's `Execute` must call `HttpClient.Send`, not `ExecuteAsync(...).GetAwaiter().GetResult()` (as
built it does the latter, which is what `docs/styleguide/csharp/09-concurrency.md` rule 9.1 bans and which deadlocks
under a single-threaded `SynchronizationContext`). The request-content adapter must override the synchronous
`HttpContent.SerializeToStream`: verified, `HttpClient.Send` with a custom `HttpContent` that overrides only the
async method throws `NotSupportedException` ("... you must override its 'SerializeToStream' virtual method"). And
`RequestBody` needs a synchronous `WriteTo(Stream)` to back that override, with `ResponseBody.OpenRead()` over
`HttpContent.ReadAsStream()` on the way back. A transport over a library with no synchronous API implements only
`IAsyncHttpClient`, and callers bridge explicitly (§3.3) — the SDK never manufactures a synchronous path by blocking.

Streaming is preserved end to end: the adapter sends with `HttpCompletionOption.ResponseHeadersRead`, so the body is
the live content stream (**SEAM-11**, **TRANSPORT-25**), and disposing the SDK `Response` disposes the
`HttpResponseMessage`, returning the connection to the pool.

**Six obvious-but-wrong behaviours of `HttpClient`, each verified against a local peer (P13).**

- **It follows redirects by default, which silently deletes the SDK's redirect authority and leaks credentials the
  SDK would have stripped.** Verified: `HttpClientHandler.AllowAutoRedirect` and `SocketsHttpHandler.AllowAutoRedirect`
  both default to `true` (`MaxAutomaticRedirections` 50). Through the as-built `new SystemNetHttpClient()`, a 302
  from one origin turned a POST into a GET and a 307 re-POSTed the 7-byte body to a *different* origin; on both hops
  the handler stripped `Authorization`, but it forwarded a caller-set `Cookie` and a custom header to the second
  origin, and the SDK's redirect policy never saw a 3xx at all. That is a direct violation of **TRANSPORT-1** and of
  **XCUT-17**(b) ("on a CROSS-ORIGIN redirect ... additionally strip origin-scoped credentials (Cookie,
  Proxy-Authorization)"). The SDK-managed constructor therefore builds its own `SocketsHttpHandler` with
  `AllowAutoRedirect = false`. A borrowed `HttpClient` does not expose its handler, so the adapter cannot inspect
  it; the adapter offers an `HttpMessageHandler`-taking constructor that rejects a `SocketsHttpHandler` or
  `HttpClientHandler` with redirects on, the DI package configures named clients' primary handler with redirects off,
  and as a last line the adapter compares `HttpResponseMessage.RequestMessage.RequestUri` with the URI it sent —
  verified, after an automatic redirect that property holds the *final* URI — and fails loudly when they differ,
  disposing the response. **TRANSPORT-2** is the harder sibling: `SocketsHttpHandler` has no public switch for its
  internal re-send on a connection that fails before the response begins (per runtime source; not reproduced here),
  so the adapter cannot disable it and instead guarantees **TRANSPORT-17**/**TRANSPORT-18** from the body side — a
  second `SerializeToStream` call on a single-use body throws `StreamConsumedException`, which fails the send rather
  than shipping an empty body.
- **Its timeout is a cancellation, and the cancellation carries the timeout as a cause.** Verified: when
  `HttpClient.Timeout` elapses, `SendAsync` throws `TaskCanceledException` whose `InnerException` is a
  `TimeoutException` and which `is OperationCanceledException`. That is **XCUT-2**'s edge case word for word —
  "even when the timeout type is a SUBTYPE of the cancellation type — the timeout branch must be checked first to
  stay reachable". The discrimination is out of band, by the caller's token: `catch (OperationCanceledException)
  when (!callerToken.IsCancellationRequested)` is a timeout, and the as-built adapter already classifies that way.
  A second trap sits one step further: a per-call deadline implemented by `CancelAfter` on the *caller's* linked
  source is indistinguishable from caller cancellation — verified, it surfaces as a `TaskCanceledException` with no
  `TimeoutException` inside — so a `RequestOptions.Timeout` is enforced with a separate `CancellationTokenSource`
  whose own state is inspected (§3.3). The SDK-managed client sets `HttpClient.Timeout` to
  `Timeout.InfiniteTimeSpan` so deadlines live in per-call options and the shared client is never mutated
  (**TRANSPORT-5**); a borrowed client's own `Timeout` remains an outer bound, documented. **TRANSPORT-6**'s
  truncation hazard does not arise: `CancelAfter` treats zero as "cancel now", never "no deadline" — verified, a
  0.5 ms `CancelAfter` fired rather than disappearing — but a positive sub-millisecond budget is still clamped up to
  1 ms so the behaviour is stated rather than accidental.
- **`TryAddWithoutValidation` means exactly that, including CR/LF.** Verified on the wire with a raw `TcpListener`:
  `HttpRequestHeaders.TryAddWithoutValidation("X-Custom", "a\r\nInjected: yes")` returns `true` and `HttpClient`
  writes `X-Custom: a` and `Injected: yes` as two header lines; through the as-built adapter, a model header value
  `"a\r\nX-Injected: yes"` reached the socket the same way, a caller-set `Host: evil.example` replaced the real host
  line, and header names went out lower-cased (`x-trace`). The same API returns `false` for a name with a space or a
  non-ASCII byte (which the as-built adapter then silently drops), accepts a non-ASCII *value* and lets the whole
  send fail later with `HttpRequestException` ("Request headers must contain only ASCII characters"), and accepts
  `Transfer-Encoding`. This is why **HTTP-17**/**HTTP-18**/**XCUT-18** are enforced in the model (§4) *and*
  re-checked at the model-to-wire boundary in the adapter (P8's mitigation for construction bypass, §4), why the
  adapter drops the **TRANSPORT-11** framing set (`Content-Length`, `Host`, `Transfer-Encoding`, `Connection`,
  `Keep-Alive`, `Upgrade`, `TE`, `Expect`) with a `Debug` log naming each, and why a header the native client still
  refuses is dropped individually rather than failing the send (**TRANSPORT-12**, **TRANSPORT-13**).
- **Request and content headers are two collections, and trying one then the other loses headers.** Verified:
  `HttpRequestMessage.Headers.TryAddWithoutValidation("Content-Type", ...)` and `("Content-Length", ...)` return
  `false`. The as-built adapter tries request headers first and falls back to content headers only when content
  exists, so a `Content-Type` on a body-less request vanishes (verified on the wire) — and, worse, it skips the
  model's `Content-Type` header entirely whenever a body is present, stamping the body-derived media type instead,
  the opposite of **TRANSPORT-10** ("The caller's explicit request Content-Type header MUST remain authoritative").
  The adapter partitions by name against the fixed set of content headers (`Allow`, `Content-*`, `Expires`,
  `Last-Modified`), gives the caller's `Content-Type` precedence over the body's, and — for a body-less
  POST/PUT/PATCH — relies on `HttpClient`'s own behaviour, verified, of sending `Content-Length: 0` for null content
  (**TRANSPORT-26**). Original header-name casing from the model is emitted on HTTP/1.1 (**HTTP-21**).
- **It sends a body on GET.** Verified: a GET with `StringContent` reached the server with a 3-byte body. **HTTP-7**'s
  own rationale names this divergence between reference transports; the model rejects the combination at
  construction, so the adapter never sees it.
- **A response the SDK cannot adapt leaks.** The as-built adapter parses the inbound `Content-Type` with
  `MediaType.Parse`, which rejects a parameter without `=`; `HttpClient` accepts such a header. Verified: a server
  response with `Content-Type: text/plain; foo` makes the as-built `ExecuteAsync` throw `ArgumentException` after the
  `HttpResponseMessage` is live, and nothing disposes it. That violates **TRANSPORT-27** ("An unparseable or absent
  inbound Content-Type SHOULD be downgraded to 'no media type'") and **TRANSPORT-22** ("the transport MUST close the
  native response before the throwable propagates"). The adapter parses with `MediaType.TryParse`, maps failure to
  `null`, and wraps all adaptation in a guard that disposes the message on any exception. Inbound header values take
  **HTTP-19**'s lenient path, and a header that fails even that is dropped individually (**TRANSPORT-14**).

**Failure mapping, and the two places the .NET shape departs from the letter.** Any failure that produced no
response maps to a `ServiceRequestException` subtype chaining the native exception, reporting itself retryable
(**TRANSPORT-20**); the timeout above maps to `ServiceRequestTimeoutException`. **TRANSPORT-20** and **XCUT-4** also
ask that this type "be a subtype of the platform IOException so existing catch(IOException) sites keep matching". On
.NET the existing catch sites are `catch (HttpRequestException)`, and `HttpRequestException` is not an `IOException`
either; the SDK root is `SdkException` and C# has single inheritance, so the transport failure cannot be both an
`SdkException` and an `IOException` or `HttpRequestException`. The port keeps the SDK root, chains the native
exception as `InnerException`, and records the letter as §10 entry 7. Caller
cancellation is the second: **TRANSPORT-3** asks for "a terminal, non-retryable interrupt-shaped IOException"; .NET's
cancellation shape is `OperationCanceledException` carrying the caller's token, the styleguide says cancellation is
not an error to wrap (`docs/styleguide/csharp/08-error-handling.md`), and the adapter rethrows it untouched. **XCUT-1**
itself anticipates this ("On the reference (JVM) runtime the pattern is ... A port MUST preserve the portable
intent"), so the departure is sanctioned; it is recorded with the rest of the cancellation mapping as §10
entry 8. **TRANSPORT-24**'s total status mapping is free: `(int)StatusCode`
accepts any code.

**Close.** **SEAM-14**'s close contract on this seam is specified in §3.7 with the rest of the lifecycle rules. The
adapter owns an `HttpClient` it constructed and disposes it; it never disposes a borrowed one; and it takes
**SEAM-15**'s option to throw `ObjectDisposedException` after dispose in both cases, rather than inheriting whichever
behaviour the inner client happens to have (verified: an owned, disposed `HttpClient` throws
`ObjectDisposedException`; a borrowed one would keep working).

**As built (d45e64b):** built — diverges: `Execute(Request)` has no options or token and is sync-over-async;
`RequestBodyContent` lacks the synchronous override; the parameterless constructor follows redirects; headers go
through `TryAddWithoutValidation` unvalidated, lower-cased, with `Host`/framing headers passed and `Content-Type`
mis-partitioned; an inbound `Content-Type` the parser rejects throws and leaks the message; no `ObjectDisposedException`
latch; no delegate-backed transport factory.

**As built (2026-10-02, phase 2b):** built. `IHttpClient.Execute(Request, RequestOptions, CancellationToken)` has no
parameter defaults (a default on an implementation's three-parameter member would let `transport.Execute(request, default)`
bind it with `null` options), and the option-less `HttpClientExtensions.Execute` passes `RequestOptions.Empty`. The
delegate-backed factories are `DelegateHttpClient.Create` (asynchronous) and `DelegateHttpClient.CreateBlocking`
(blocking), not two overloads named `Create` as the wording above says: a lambda that only throws is ambiguous between a
`Func<…, Response>` and a `Func<…, Task<Response>>` overload (`CS0121`, verified), and that is the failing transport a
conformance kit needs (P2b-6). `SystemNetHttpClient.Execute` is still sync-over-async and ignores its options until phase
8b, and the `ObjectDisposedException` latch is phase 3b's and 8b's. *Dated correction*; the line above stands as written.

### 3.3 The asynchronous transport seam and the canonical pivot

**What the reference requires and why.** **SEAM-16** requires the async transport's future to "complete either with a
non-null response (caller owns closing it) or exceptionally with the transport failure", never with a null success,
and that cancelling an already-succeeded future not close the delivered response. **SEAM-17** (a **SHOULD**) wants the
async contract expressed through one canonical dependency-free future pivot, with per-ecosystem facades as separate
adapters bridging to and from it, because that platform's async ecosystem is fragmented. **SEAM-30** and **ASYNC-5**
require the producer to close an orphaned response when the future is already settled; **ASYNC-1**/**ASYNC-2** restate
single-value completion and the failure channel; **ASYNC-6** requires bidirectional cancellation; **ASYNC-20**
requires that cancelling a future whose response was already delivered not close it.

**The porter's question.** Does .NET have a dominant async primitive every framework already speaks — in which case
the pivot is that primitive and the bridges are unnecessary — or is it fragmented enough that **SEAM-17**'s
pivot-plus-adapters shape is needed? The Ruby port answered "fragmented" and built its own future. The .NET answer
reverses it.

**The .NET answer: the pivot is `Task<Response>`, and the adapter apparatus collapses.** `Task<T>`,
`CancellationToken` and `IAsyncEnumerable<T>` are in the runtime, and every .NET framework, test runner and library
awaits them. There is no second async ecosystem to reconcile: `ValueTask<T>` is a `Task<T>` optimisation from the
same runtime, F# `Async` and `task { }` consume `Task` natively, `IObservable<T>` is a BCL interface whose Rx
operators bridge to `Task` in one call, and Unity's `UniTask` is outside a server SDK's audience. So **SEAM-17** is
satisfied verbatim — "one canonical, dependency-free async primitive (a future that completes with a value or
exceptionally)" is `Task<Response>` — and its second sentence, "ecosystem-specific facades ... SHOULD be provided as
separate adapter modules", has nothing to apply to (P4: a split the host does not have is not preserved). This is the
same conclusion the Node port reaches with `Promise`, for the same reason.

```csharp
public interface IAsyncHttpClient : IAsyncDisposable
{
    Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
}
```

The pivot is `Task`, not `ValueTask`, deliberately. A transport never completes synchronously in the common case, so
`ValueTask`'s allocation saving does not apply, and `ValueTask` may be awaited only once (**CA2012**), cannot be
passed to `Task.WhenAll`/`WhenAny` without `AsTask()`, and is the wrong shape for a value a paginator or a hedging
caller may observe more than once. The pipeline's internal policy chain uses `ValueTask` where its steps often
complete synchronously (§5); the SPI boundary does not.

**Clause-by-clause (P5).**

- **SEAM-16** / **ASYNC-1** / **TRANSPORT-23**, "MUST NOT complete successfully with a null/absent value": the
  return type is the non-nullable `Task<Response>`, and a nullable-reference-type warning is a build error in this
  repository. *Hidden precondition (P7):* NRT is compile-time only — a third-party transport compiled without
  nullable annotations, or one that writes `return null!`, still produces a null success. The pipeline's terminal
  runner therefore asserts the transport's result and converts a null into a failure. The as-built `HttpPipeline`
  does this through `PipelineAbortedException`; under §5.1's request-in, response-out signature the exception's
  other use (a chain that completes without a response) disappears, and this null check is what remains of it.
- **ASYNC-2** / **TRANSPORT-21**, "delivered through the future's failure channel, never thrown synchronously": an
  `async` method turns every exception, including argument-validation exceptions, into a faulted task — free.
  *Hidden precondition:* only `async` methods do this. A non-`async` method that returns a `Task` and throws before
  returning one throws synchronously; the conformance kit calls every transport with a null request and asserts a
  faulted task, not a thrown exception. This deliberately follows **ASYNC-2** over the Framework Design Guidelines'
  preference for synchronously thrown usage errors; **PIPE-30**'s normalisation of policies is the same rule one
  layer up (§5).
- **ASYNC-20** and the second sentence of **SEAM-16**, "cancelling an already-completed success future does NOT
  close the delivered response": free. A `Task` cannot be cancelled after it completes, and a token signalled after
  delivery touches nothing.
- **SEAM-30** / **ASYNC-5**, the orphaned response: largely *structurally* free, and the precondition is precise. A
  `CompletableFuture` can be cancelled by any holder, so the producer can lose a race it did not start. A `Task` can
  be completed only by its producer, so the "future already cancelled" branch of the race does not exist for an
  `async` method: if `SendAsync` returns a response, the method returns it. *Hidden precondition (P7):* any code that
  settles a task through a `TaskCompletionSource<Response>` reintroduces the race, because `TrySetResult` returns
  `false` when a cancellation callback already called `TrySetCanceled`. The .NET form of the Ruby port's
  check-after-resume rule is therefore one sentence, stated once and cited everywhere else: **every
  `TaskCompletionSource<Response>.TrySetResult` (or `TrySetResult` on any disposable) whose return value is `false`
  disposes the value through §3.7's quiet-dispose helper.** What remains of **SEAM-30** is producer-internal — an
  adaptation that throws after the native response is live (**TRANSPORT-22**, §3.2), a policy that supersedes a
  response (**PIPE-40**) — and those are the same guard.
- **Abandonment is not orphaning, and the SDK does not do it.** Verified: `task.WaitAsync(TimeSpan)` throws
  `TimeoutException` while the producer keeps running, and when the producer later completes, its disposable result
  is never disposed. That is the one way a .NET caller can orphan a response, and it is outside the future contract
  (the future did deliver; nobody was listening). The rule for SDK code is that no SDK path abandons a
  `Task<Response>` — deadlines are enforced by cancelling the token the producer observes, never by `WaitAsync` or
  `WhenAny` around a send — and where an SDK component must race sends (hedged requests, pagination prefetch), the
  loser's task gets a continuation that disposes its result.

**The sync↔async bridges (SEAM-18).** `AsAsync(this IHttpClient)` must not offload a blocking call to the shared
thread pool: **SEAM-18** says so explicitly ("there is intentionally no default, and a shared global fork/join-style
pool is explicitly not acceptable because a blocking call would starve it"), and the .NET thread pool is precisely
such a pool — its starvation heuristic injects threads gradually, so a burst of blocking sends stalls every other
async continuation in the process for seconds. The as-built bridge calls `Task.Run(() => inner.Execute(request), ct)`
on the shared pool, has no scheduler parameter, and — verified — the token only prevents the delegate from
*starting*: `Task.Run(fn, ct)` cancelled mid-run still returns the delegate's result. The bridge becomes
`AsAsync(this IHttpClient client, TaskScheduler scheduler)` with no zero-argument overload, passes the options and
the token *into* `Execute` so cancellation reaches the blocking call cooperatively, and documents per **ASYNC-7**
that cancellation can abort only as far as the blocking transport honours its token. `AsBlocking(this
IAsyncHttpClient)` waits with `GetAwaiter().GetResult()`, which unwraps: verified, `.Result` on a faulted task throws
`AggregateException` while `GetAwaiter().GetResult()` throws the original `InvalidOperationException`, which is
**SEAM-18**'s "unwrap the async wrapper exception so callers observe the original failure" and **ASYNC-13**'s rule
for free. Its hazard is the `SynchronizationContext` deadlock of §1: core's own awaits all use
`ConfigureAwait(false)`, but a third-party async transport that does not can deadlock a blocking caller on a UI
thread, so the bridge is documented as a last resort for genuinely synchronous call sites, not a way to get a sync
API. Both bridges thread `RequestOptions` through (**ASYNC-19**).

**Cancellation is cooperative, token-shaped, and never an interrupt.** The reference's cancellation vocabulary is the
JVM's: thread interruption, the interrupt flag, `InterruptedIOException`, interrupt-mode versus
non-interrupt-mode cancellation (**ASYNC-3**), and the ordering protocol that keeps a stale interrupt from poisoning
a pooled thread (**ASYNC-4**). .NET has `Thread.Interrupt`, but nothing in the async world uses it, it cannot reach a
thread-less `await`, and the platform's cancellation is the `CancellationToken`, observed cooperatively and
surfaced as `OperationCanceledException`. So the interrupt-shaped clauses — **ASYNC-3**'s two modes, **ASYNC-4**'s
poisoning protocol, **SEAM-18**'s and **ASYNC-14**'s "restore the interrupt flag", **CFG-20**'s interruptible task,
**SEAM-14**(3) and **XCUT-13**'s interrupt-safety — describe a mechanism the port does not have, and their intent maps
as follows: "abort an in-flight exchange" is cancelling the token the transport passes to `SendAsync`; "do not
poison a pooled thread" is vacuous because a token is per call and nothing is left on the thread; "preserve the
ambient cancellation flag" is free because a token's state is immutable once signalled. This changes how
cancellation reaches the socket, not whether (P6), and it is recorded once as §10
entry 8.

**Deadlines, end to end.** A per-call deadline is a `CancellationTokenSource` created for that call, linked to the
caller's token, armed with `CancelAfter` (or constructed over the pipeline's `TimeProvider` so tests control it), and
disposed when the call ends (`docs/styleguide/csharp/13-resource-management.md` rule 13.8). Its own
`IsCancellationRequested`, checked while the caller's token is not signalled, is what classifies a timeout as the
retryable failure of **XCUT-2** rather than the terminal cancellation of **XCUT-1**; the ambient token state, not an
exception message, is the discriminator, as both requirements demand. Inter-attempt waits use
`Task.Delay(delay, timeProvider, token)` — verified, a 30-second delay under a 50 ms token completed with
`TaskCanceledException` after 98 ms — which is **XCUT-3**'s prompt, cancellable, non-pinning wait with no thread
held; the synchronous path waits on a `TimeProvider.CreateTimer` that signals a `ManualResetEventSlim`, waited
with the call's token (§6.1, §8.3), never `Thread.Sleep`. **TRANSPORT-5**'s
per-call scoping falls out free, since a derived source is inherently scoped to one call.

**Ecosystem adapters that remain.** The one bridge worth shipping is for *multi-value* streams, not for the pivot:
`Dexpace.Sdk.Reactive` (§2.2) exposes pagination's and SSE's `IAsyncEnumerable<T>` as `IObservable<T>`. Its
**ASYNC-6** mapping is direct — disposing the subscription cancels the token the enumeration observes, and a token
cancelled from the SDK side completes the observable with the cancellation — and **ASYNC-21**'s backpressure clause
is satisfied by construction, because `IAsyncEnumerable<T>` is pull-based: the bridge requests the next element only
when the previous `OnNext` has returned, never eagerly, and it never disposes the caller-owned source.

**As built (d45e64b):** built — diverges: `ExecuteAsync(Request, CancellationToken)` takes no `RequestOptions`;
`AsAsync` offloads to the shared pool with no scheduler parameter and does not pass the token into `Execute`;
`AsBlocking` drops the token; there is no quiet-dispose rule for `TaskCompletionSource` losers (none exist yet); no
Rx bridge.

**As built (2026-10-02, phase 2b):** built. `ExecuteAsync(Request, RequestOptions, CancellationToken)` is the pivot.
`AsAsync(IHttpClient, TaskScheduler)` takes the caller's scheduler, has no overload without one, starts each call with
`LongRunning | DenyChildAttach` (so `TaskScheduler.Default` is accepted and costs a dedicated thread per call), passes the
options and token into `Execute`, and, after `Execute` returns, disposes a response produced once the token is signalled
and completes cancelled, so §5.3's "cancel without interruption" mode cannot orphan a response (P2b-4). `AsBlocking`
passes the options and token, rejects a `null` task or response with `InvalidOperationException`, and neither bridge
disposes what it wraps. `PipelineRunner` converts a `null` transport result into `PipelineAbortedException` before any
policy sees it. A `Dispose` that throws inside the check after return faults the task until phase 3b's `DisposeQuietly`.
`BannedSymbols.txt` bans `TaskCompletionSource<T>.SetResult`/`TrySetResult` and the five `Task<T>.WaitAsync` overloads
in `src/`. *Dated correction*.

**Dated correction, 2026-10-03 (phase 3b, P3b-16):** the interim "a `Dispose` that throws inside the check after return faults the task until phase 3b's
`DisposeQuietly`" is gone. `AsAsync` disposes a response produced after the token is signalled through `Disposal.DisposeQuietly`, so the task completes cancelled and the
failure is reported as an `exception` event tagged `dexpace.dispose.suppressed` on the current `Activity` (and as a warning when a logger is supplied).


### 3.4 The wire-codec (serde) seam → kept separate, even though embedding is free

**SEAM-19** requires the codec seam to bundle a serializer, a deserializer and the media type its serializer
produces, and that the media type "is never defaulted at the seam level"; **SEAM-2** forbids core naming any
concrete implementation; **SERDE-1**/**SERDE-2** restate both. The seam is the as-built `ISerde` interface in
`Dexpace.Sdk.Core.Serialization`: a required `DefaultMediaType` property each codec declares (the name is about the
*body's* default `Content-Type`, per **SERDE-2**; the seam supplies no value), an asynchronous `Stream` pair, and a
synchronous `IBufferWriter<byte>`/`ReadOnlySpan<byte>` pair. The seam exposes no serializer-specific type, which is
what keeps it format-neutral.

**All four allocation profiles ship; the seam carries two and core derives the rest.** **SEAM-20** requires "the
common allocation profiles (produce a fresh string, produce a fresh byte array, stream into a caller-owned output,
encode into a caller-owned scratch buffer at an offset)", and **SERDE-4** adds that the buffer profile "MUST return
the number of bytes written, MUST honor a start offset, and MUST throw a range/overflow error (distinct from the serde
exception type and not chaining one)". On .NET two of the four are primitives and two are derivations, so an adapter
implements the primitives and core supplies the rest once, as extension methods on `ISerde`, so no adapter can get
them subtly wrong:

```csharp
ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default); // stream
void Serialize<T>(IBufferWriter<byte> destination, T value);                                              // buffer
// core extensions over the buffer primitive:
public static byte[] SerializeToUtf8Bytes<T>(this ISerde serde, T value);                // fresh byte array
public static string SerializeToString<T>(this ISerde serde, T value);                   // fresh string
public static int Serialize<T>(this ISerde serde, Span<byte> destination, T value);      // fixed buffer
```

The fixed-buffer profile expresses **SERDE-4**'s offset the .NET way — the caller passes `buffer.AsSpan(offset)`,
so bytes before the offset are untouchable by construction — returns the count written, and throws
`ArgumentOutOfRangeException` when the payload does not fit. That exception is the .NET "range/overflow error": it
is not an `SdkException`, chains nothing, and is what the BCL itself throws for a too-short destination.
`IndexOutOfRangeException` would read closer to the letter but is reserved for the runtime (CA2201). The string
profile decodes the UTF-8 buffer; a codec whose wire format is not UTF-8 text overrides it through an optional
interface rather than the seam guessing. **Neither caller-owned variant closes the caller's target**: verified,
`JsonSerializer.SerializeAsync` leaves the destination stream open, and the §3.1 `leaveOpen` trap is the thing an
adapter over a `TextWriter`-based library (Newtonsoft's `JsonTextWriter` over a `StreamWriter`) must configure away,
asserted per adapter in `Dexpace.Sdk.Conformance`. The decode side mirrors it: `DeserializeAsync` **reads to EOF and
does not dispose the caller's stream** (**SEAM-21**, **SERDE-3**).

**The type token is free on .NET, and its hidden precondition is the shrinker (P7).** **SEAM-21** and **SERDE-5**
exist because an erased generic cannot recover its target type. The CLR reifies generics: inside
`DeserializeAsync<T>`, `typeof(T)` is the full closed type, so `DeserializeAsync<List<Pet>>` *is* the explicit type
witness, **SERDE-6**'s "full-generic type carrier" is `T` itself, and **SERDE-7**'s reified helper is every generic
method. **SEAM-22**/**SERDE-8**'s "reject an unresolved type variable" is vacuous for the generic API — a type
parameter inside a generic method is bound to the real argument at run time — and has exactly one .NET analogue: an
*open* generic `Type` (`typeof(List<>)`), reachable only through a `Type`-taking overload. The seam has no such
overload today; if one is added for non-generic callers it rejects `type.ContainsGenericParameters` with an
actionable message. The precondition is what makes this more than a one-liner: the free path resolves `typeof(T)`
by reflection, and under trimming or NativeAOT reflection-based `System.Text.Json` is disabled — verified, in an
AOT-defaulted file-based app `JsonSerializer.SerializeAsync(stream, 1)` throws `InvalidOperationException`
("Reflection-based serialization has been disabled"). So the working witness on .NET is `JsonTypeInfo<T>` from a
source-generated `JsonSerializerContext`. The as-built adapter already enforces this correctly: it requires a
`TypeInfoResolver`, resolves `JsonTypeInfo<T>` per call, and turns a missing registration into a
`SerializationException`/`DeserializationException` naming the type and pointing at the context — which is
**SERDE-6**'s "fail loudly (serde exception) ... rather than silently decoding into the wrong (raw) type" on this host.

**Failure types (SEAM-23).** Encode and decode failures surface as SDK-owned exceptions chaining the codec's own
(`JsonException`, `NotSupportedException`), never leaking it; a genuine stream `IOException` propagates unwrapped,
which the as-built adapter's `catch ... when (ex is JsonException or NotSupportedException)` filter already
achieves (**SEAM-20**, **SEAM-21**). **SEAM-23** also requires "a base serde failure with encode/decode subtypes"
that is "open for codegen/adapters to add more specific subtypes". As built, `SerializationException` and
`DeserializationException` are `sealed` and derive directly from `SdkException`, so there is no base to catch and
nothing to extend; the design adds an unsealed `SerdeException : SdkException` and unseals both subtypes under it. One
naming hazard is worth a sentence: `System.Runtime.Serialization.SerializationException` also exists in the BCL, so
a file importing both namespaces gets CS0104; the SDK keeps its names (they are the specification's) and its own
code qualifies them.

**The codec ships separately anyway (P3).** `System.Text.Json` is in the shared framework, so embedding the reference
codec in core would cost no dependency. It ships as `Dexpace.Sdk.Serialization.SystemTextJson` because **SEAM-2** is
about coupling and **SEAM-19** is about not defaulting policy. Two .NET-specific arguments make the separation pay
rather than merely preserving symmetry. First, the codec *cannot* be configured by core even in principle: its
AOT-safe form needs the application's own source-generated context, so a core-embedded default would either be
reflection-based (unsafe under **NFR-8**) or empty. Second, it keeps the door open for
`Dexpace.Sdk.Serialization.NewtonsoftJson` without a second code path in core. The adapter uses the in-box
`System.Text.Json` with no `PackageReference`, so its security floor is the runtime's servicing (§2.4).

**Unsafe decode paths, flagged once.** The .NET analogue of Ruby's `JSON.load` hazard is polymorphic type-name
handling: `Newtonsoft.Json`'s `TypeNameHandling` other than `None` instantiates attacker-named types, and
`BinaryFormatter` is obsolete and removed. The future Newtonsoft adapter pins `TypeNameHandling.None`, and both
are banned in core and adapters through the banned-API analyzer (§9), not by convention.

**Adapter defaults.** `System.Text.Json` renders `DateTime`/`DateTimeOffset` as ISO-8601 and parses the same form, so
**SERDE-24**'s round-trip holds by default. **SERDE-23**'s tolerant decode is its default (unmapped members are
skipped). **SERDE-25**'s concern — one part of an application reconfiguring a codec another part is using — is closed
differently from the reference: the adapter calls `MakeReadOnly()` on its options, after which any mutation throws,
so sharing one instance is safe. The trap is *whose* options: as built, `MakeReadOnly()` freezes the *caller's*
object in place — the same shape as the Ruby port's warning about `Ractor.make_shareable` — which **SERDE-26**
forbids, so the adapter copies first (`new JsonSerializerOptions(callerOptions)`) and freezes only its private copy
(§7.3).

**As built (d45e64b):** built — diverges: the fresh-string, fresh-byte-array and fixed-buffer profiles are not
offered; there is no `SerdeException` base and both failure types are sealed; the adapter freezes the caller's
`JsonSerializerOptions` instead of a private copy.

**As built (2026-10-02, phase 2b):** built. `SerdeException` is abstract, with `protected` constructors (it makes every
thrown serde failure an encode or a decode failure), and both subtypes are unsealed under it. `SerdeExtensions` offers the
three profiles once, over any `ISerde`: `SerializeToUtf8Bytes`, `SerializeToString` and the fixed-buffer
`Serialize(Span<byte>, T)` (an overflow is an `ArgumentOutOfRangeException` that leaves the destination untouched).
`SerializeToString` decodes the seam's UTF-8 buffer, and a codec whose wire format is not UTF-8 text implements the
optional interface `IStringSerde` to supply its own string. `SEAM-22` is met by construction: no `Type`-taking decode
overload exists, and an architecture test keeps it so (P2b-2). The adapter's private copy of `JsonSerializerOptions` is
phase 7a's. *Dated correction*.

### 3.5 The operation-input projection seam

**SEAM-26** requires a per-operation declaration of method, path template with named placeholders, and typed
path/query/header/body projections, with the body carried rather than encoded; **SEAM-27** fixes path-parameter
percent-encoding as single segments (so a value cannot inject `/`), mandatory placeholder values, RFC 3986 query
rendering, and the base-URL composition rules (trailing-slash normalisation, empty-path no-op, base query preserved
with the operation query appended, base with a fragment or a malformed URL rejected with a context-bearing error).

The .NET shape is a `sealed record OperationDescriptor` — `Method`, template `string`, and the four projection lists,
each defaulting to empty — plus one `OperationDescriptor.BuildRequest(Uri baseAddress, ...)` in core. No code
generation is implied; the port specifies only the runtime primitive a generator would target. **SEAM-28**'s
optional operation id rides on the descriptor and is handed to the pipeline context, never to the URL.

**The obvious tools, verified (P13) — and for once the obvious one is right.** Verified on 10.0.401 against the
input `a b*~+/!()'`:

| Escaper | Output | Verdict |
|---|---|---|
| `Uri.EscapeDataString` | `a%20b%2A~%2B%2F%21%28%29%27` | RFC 3986 component encoding exactly: unreserved set `A-Za-z0-9-._~`, UTF-8 for non-ASCII (`é` → `%C3%A9`) — the right tool for **HTTP-29**, **HTTP-32** and path segments |
| `WebUtility.UrlEncode` | `a+b*%7E%2B%2F!()%27` | form-style (`+` for space), encodes `~`, leaves `*!()` — neither RFC 3986 nor the WHATWG form serializer |
| `Uri.EscapeUriString` | `a%20b*~+/!()'` | obsolete (SYSLIB0013); escapes only outside the broad URI set, so `/` and `+` survive and a path value could inject a segment |

Unlike Ruby, where none of the three built-ins was usable, `Uri.EscapeDataString` is the component encoder, and its
inverse `Uri.UnescapeDataString` is already **HTTP-31**'s lenient decode (verified: `"a+b%2"` round-trips as
`a+b%2` — `+` stays `+`, a malformed escape stays raw). *Hidden precondition (P7):* this holds for the
`EscapeDataString` of .NET Core; the pre-4.5 .NET Framework escaped a different set, and a port that copied
Framework-era code would inherit it. The form encoder for `application/x-www-form-urlencoded` bodies (**HTTP-38**,
**BODY-35**) is hand-written to the WHATWG serializer, because `WebUtility.UrlEncode` disagrees with it on `~*!()`;
the two are different functions with different names and tests, never interchanged.

**`System.Uri` canonicalises, and three of its canonicalisations matter here.** Verified:

- **Dot segments are removed, including percent-encoded ones.** `https://h/pets/../admin` becomes `https://h/admin`,
  and so do `https://h/a/%2E%2E/b` and `%2e%2e` → `https://h/b`. `Uri.EscapeDataString("..")` returns `..`
  (dots are unreserved). So a path parameter whose value is `..` escapes its segment *after* correct encoding —
  **SEAM-27**'s "a value cannot inject extra '/' segments" is defeated by removal rather than insertion — and
  encoding the dots does not help, because `%2E%2E` is collapsed too. `UriCreationOptions {
  DangerousDisablePathAndQueryCanonicalization = true }` preserves both forms, but it disables canonicalisation for
  the whole URL including the caller's base address. The projection therefore **rejects a path-parameter value that
  is exactly `.` or `..`** with an `ArgumentException` naming the placeholder; the specification does not say what
  to do with such a value, so the resolution is recorded as §11 item 35.
- **`ToString()` is a display form, not a wire form.** `new Uri("https://h/a%20b?x=%26&y=a%20b").ToString()` is
  `https://h/a b?x=%26&y=a b`; `AbsoluteUri` keeps `%20`. Encoded reserved characters survive in both
  (`%2F` and `%26` round-trip), which is what **REDIR-13** needs, while unreserved escapes are normalised (`%41` →
  `A`, `%7E` → `~`), which RFC 3986 §6.2.2.2 declares equivalent. Core renders URLs for the wire, for logs and for
  equality through `AbsoluteUri` (or `GetComponents`), and `Uri.ToString()` joins the banned-API list.
- **Reference resolution is RFC 3986, which is not SEAM-27's composition.** `new Uri(new Uri("https://h/c?sig=1"),
  "pets")` is `https://h/pets` — the last base segment and the base query are both dropped — while the same call
  with a trailing slash on the base yields `https://h/c/pets`. **SEAM-27**'s rules (normalise to exactly one
  separator, keep the base query and append the operation's) are therefore hand-coded over the base's components,
  never delegated to `new Uri(base, relative)`.

Two smaller parse behaviours are pinned by tests because they would surprise a porter: on Linux,
`Uri.TryCreate("/rel", UriKind.Absolute, ...)` succeeds with scheme `file`, so "is it absolute" is never the whole
check (the scheme must be `http`/`https`, as the as-built `Request` constructor already requires), and `\` in a path
is silently converted to `/`.

**As built (d45e64b):** not built (pagination carries its own internal query-pair reader and the URL redactor its own
parser; neither is the projection seam).

**As built (2026-10-02, phase 2b):** built. `OperationDescriptor` carries typed `PathParameters`, `Query`, `Headers` and
`Body`, with validation in its `init` accessors, and `BuildRequest(Uri)` / `BuildRequest(DexpaceClientOptions)` compose
over the base's components with the one internal RFC 3986 encoder. A dot-segment is rejected in the template's literal
text at construction and in a rendered segment at `BuildRequest`, not only as a whole value (§11 item 35, P2b-7), and
`InvalidOperationException` is the build-time failure for a placeholder with no value or a parameter naming none.
`OperationId` is carried and never reaches the request; attaching it to the context chain is phase 4a's. *Dated
correction*.

### 3.6 Discovery and the zero-dependency boundary, restated

After §3.1 retires the byte-stream provider, the seams that would need a resolution story are the two transports and
the codec. **SEAM-5** fixes the precedence — explicit install always wins; else auto-discover; zero discoverable
candidates yields a descriptive install-hint error; more than one yields an error listing all candidates; exactly one
is selected silently — and **XCUT-23** restates it as a cross-cutting invariant. **SEAM-6** makes explicit install
idempotent for the same instance and a hard failure for a different one; **SEAM-7** caches a successful
auto-resolution process-wide while leaving an unresolved state re-evaluable; **SEAM-8** makes replacing an
already-handed-out auto-resolved provider a warning; **SEAM-9** requires reads to see the latest install without
blocking and writes to be serialised.

**.NET has no classpath to scan, and the tools that look like one are the obvious-but-wrong tools (P13).** Two
mechanisms could imitate discovery. Reflection over loaded assemblies (`AppDomain.GetAssemblies()`,
`Assembly.GetTypes()`, `Activator.CreateInstance`) is exactly what ILLink and NativeAOT cannot see, so a trimmed
application would "discover" nothing and **NFR-8** would be violated by the discovery mechanism itself. And a
`[ModuleInitializer]` in each adapter that registers with a core registry — the direct analogue of Ruby's
registration-on-`require` — runs only when the adapter assembly *loads*, which the CLR does lazily on first type
reference: an adapter that is installed but not yet referenced has not registered, so the candidate set depends on
call order, which is the opposite of **SEAM-5**'s determinism.

So the port keeps the *outcomes* and drops the *registry*. In the DI-less path there is no discovery at all: every
component that needs a transport or codec takes it as a non-nullable constructor or factory parameter
(`DexpacePipeline.CreateDefault(IAsyncHttpClient transport, ...)` already does), so **SEAM-5**'s zero-candidate branch
becomes a compile-time error — strictly stronger than an install-hint exception (P9) — and the multiple-candidate
branch is unrepresentable, because one argument holds one transport. In the DI path the container *is* the registry,
and it has one behaviour that violates **SEAM-5** as written: `IServiceCollection` accepts several registrations of
one service type and `GetService` silently returns the last. `Dexpace.Sdk.Extensions.DependencyInjection` therefore
validates at `ValidateOnStart` that each configured client resolves exactly one `IAsyncHttpClient` and one `ISerde`,
failing with a message that lists every registered implementation type — the multi-candidate branch — and with an
install hint naming the `Use...`/`Add...` call when there is none. Registration uses `TryAdd`, which is **SEAM-6**'s
idempotence for the same registration; a second, different explicit registration is the validator's "different
provider" failure, naming both. **SEAM-7**'s caching is the container's singleton lifetime, and its "unresolved state
stays re-evaluable" clause has nothing to apply to, because a built container does not gain registrations.
**SEAM-8**'s warning describes replacing an auto-resolved provider, which never exists. **SEAM-9**'s
single-construction guarantee is the container's documented singleton behaviour. **SEAM-10**'s de-duplication across
loaders is vacuous for the reason §2.4 gives. These branch-by-branch mappings are recorded as §10
entry 9, because **SEAM-5**'s letter ("otherwise the runtime auto-discovers providers registered on
the classpath/plugin registry") names a step the port deliberately does not take.

**Presence-gated activation survives for instrumentation only, and is the platform's own.** The Ruby port allowed an
OpenTelemetry adapter to install itself when OpenTelemetry was loaded, and nothing else. On .NET that asymmetry is
built into the platform: core always emits to its `ActivitySource` and `Meter`, which cost nothing when no listener
is subscribed, and an OpenTelemetry SDK — or any `ActivityListener` — activates them by subscribing by name (§8.1).
For a transport or codec, "whatever happens to be installed silently wins" remains an auditability failure, which is
why neither is ever activated by presence.

**As built (d45e64b):** built — diverges: core has no registry and takes the transport explicitly, as designed; the DI
package and its single-registration validation are not built.

### 3.7 Lifecycle and ownership

Six requirements — **SEAM-14**, **SEAM-25**, **HTTP-43**, **ASYNC-15**–**ASYNC-17**, **XCUT-13** and **XCUT-22** —
say the same three things about closing, in six different vocabularies. Unlike Ruby, .NET has a closing interface
and language support for it (`IDisposable`/`IAsyncDisposable`, `using`/`await using`), so much of the rule is
inferred from a type; what the type does not say is written down here once.

**The interfaces.** Anything the SDK can release implements `IDisposable`, and additionally `IAsyncDisposable` when
its release can be asynchronous; a type holding an async-releasable resource implements both, and its `Dispose` does
the synchronous release rather than blocking on the asynchronous one
(`docs/styleguide/csharp/13-resource-management.md` rules 13.1 and 13.5). Transports implement
`IDisposable` (`IHttpClient`) or `IAsyncDisposable` (`IAsyncHttpClient`) through the seam interfaces, which is
**SEAM-14**(1)'s "closeable" on both seams; **ASYNC-17**'s no-op default is what `DelegateHttpClient.Create` supplies.
The full dispose pattern with a finalizer is used only where an unmanaged handle is held directly, which in this SDK
is nowhere (rule 13.3), so the as-built `GC.SuppressFinalize` calls are harmless noise.

**Idempotence is a latch.** **XCUT-13** requires close to be "idempotent (latched so repeat calls are no-ops)",
**SEAM-14** requires it of both transport seams, **SEAM-25** of any adapter owning a pool, and **HTTP-43** of
`Response` — "Response MUST be closeable and its close MUST be idempotent and forward to the body" — which in turn
delegates to the body's idempotent close (**HTTP-41**, **BODY-15**). All of them use one mechanism:
`if (Interlocked.Exchange(ref _disposed, 1) != 0) return;` followed by the release. Whoever flips the latch runs the
release, everyone else returns; a release that throws still leaves the latch flipped, so no second release is
attempted (**BODY-27**), and the failure propagates once. The Ruby port had to keep its mutex out of the release
because a fiber-owned mutex held across a suspension deadlocks; an `Interlocked` flip holds nothing, so the problem
does not arise. As built, `Response.Dispose` and the body `Dispose` overrides are not latched and rely on the
idempotence of what they wrap (`HttpResponseMessage` and `Stream`, both documented idempotent, `Stream`'s verified);
the latch makes the guarantee the SDK's rather than the inner object's.

**Ownership: the SDK disposes exactly what the SDK created.** **XCUT-22** states it — "A caller-supplied
(bring-your-own) transport client, executor, or connection pool MUST NOT be closed/shut down by the SDK" — and
**SEAM-14**, **SEAM-25**, **ASYNC-15** and **TRANSPORT-15** restate it. On .NET this is a constructor-level fact
recorded in a `readonly bool` at construction: the parameterless `SystemNetHttpClient()` builds and owns its
`HttpClient`; `SystemNetHttpClient(HttpClient)` borrows and never disposes it — which the as-built adapter already
does, and which is `docs/styleguide/csharp/13-resource-management.md` rule 13.4 ("never dispose a dependency injected
into you"). The stream-level form of the same rule is the `leaveOpen` convention of §3.1, and a `HttpClient` from
`IHttpClientFactory` is always borrowed. Making ownership a construction-time fact keeps it from being re-decided,
differently, at each dispose site.

**Close never blocks.** **XCUT-13**'s second clause — close "MUST NOT block on interrupt-sensitive waits ... and
preserves the ambient interrupt/cancel flag as-is" — reduces on .NET to its non-blocking half: there is no interrupt
flag to preserve, and a `CancellationToken` cannot be un-cancelled (§3.3). Dispose never waits for in-flight calls,
never sleeps and never joins; **ASYNC-16**'s graceful drain is a **SHOULD** that applies only to an adapter owning
worker threads, and none of the MVP packages owns any. **SEAM-15** is a **MAY** and the port takes it explicitly: a
send after dispose throws `ObjectDisposedException` (`ObjectDisposedException.ThrowIf`), the .NET-idiomatic
post-dispose failure, documented rather than left to whatever the inner client does.

**Unobserved failures on cleanup paths.** Cleanup runs in `finally` blocks and on discard paths where there is often
no caller left to throw to, and a dispose failure must never replace the failure that caused the cleanup. The Ruby
and JVM ports attach it to the primary exception's suppressed list; .NET exceptions have none, and wrapping both in an
`AggregateException` would change the thrown type and break every `catch (HttpResponseException)`. So one internal
helper, `Disposal.DisposeQuietly(IDisposable? resource, Exception? primary = null)` with an async twin, is the single
sanctioned exit: it is null-safe (**CFG-21**'s last clause), it catches non-fatal exceptions from `Dispose` (the
fatal-exception filter of §5.2), and it disposes of each one in exactly one of two ways and never a third —
**attached to the primary exception through §5.2's `ExceptionTrail.AddSuppressed` when a primary is in flight, and
reported through the diagnostics surface of §8.1 (a `Warning` log and an exception event on the current SDK
`Activity`) when there is none.** It never throws over a primary exception and never swallows silently: a swallowed
dispose failure is a leaked connection nobody can diagnose. The two exceptions to *quiet* disposal are the ones the
specification requires to be loud: an explicit `Dispose` by the caller propagates its failure (**SSE-30**, §7.2), and
a release throwing inside the latched dispose above propagates once (**BODY-27**). Everything else — the
`TaskCompletionSource` losers of §3.3, the discard close of **CFG-21**, the adaptation-failure close of
**TRANSPORT-22**, the superseded-response closes of **PIPE-40** and **REDIR-22**, the pre-wait release of
**RETRY-35**, the drop paths of **PAGE-12** and **PAGE-27** — goes through `DisposeQuietly`.

**As built (d45e64b):** partial: ownership-aware transport disposal is built; there is no dispose latch on
`Response`/bodies/transport, no `ObjectDisposedException` after dispose, and no quiet-dispose helper.

**As built (2026-10-03, phase 3b):** partial. Built: the latch on `ResponseBody` (the standard `Dispose(bool)`/`DisposeAsyncCore()` pattern, no finalizer), on `Response` and
on `SystemNetHttpClient` (**SEAM-14**), and the internal `Disposal`. Still unbuilt: the `ObjectDisposedException` after transport dispose (phase 8b, **SEAM-15**). The text above
stands as written, with two corrections. *Dated correction*.

- **`Disposal`'s signature gains an optional logger (P3b-3).** It is `DisposeQuietly(IDisposable? resource, Exception? primary = null, ILogger? logger = null)` with an async
  twin, internal. Neither `ExceptionTrail.AddSuppressed` nor `ExceptionFacts.IsFatal` exists before phase 4b, and core has no logger plumbing before 5b, so until then it
  reports a non-fatal failure on `Activity.Current` (an `exception` event tagged `dexpace.dispose.suppressed`, plus `dexpace.dispose.primary_type` when a primary is in
  flight) and, when a logger is supplied, as a `Warning` naming the resource and exception types; the fatal filter is `OutOfMemoryException`. With no listener and no logger
  the failure is reported to nobody: an **accepted interim gap** (the lead's ruling of 2026-10-02) that closes in 5b. 4b repoints the primary branch and the filter; 5b plumbs the
  client's logger and owns any counter's name.
- **A stream-backed response body opened after dispose throws `StreamClosedException` (P3b-11)**, after the consumed check, so a body that was read and then disposed still
  reports `StreamConsumedException`. In-memory bodies keep serving after dispose.

### 3.8 The observability, time and configuration seams

The reference routes logging, tracing, metrics, time and configuration through SDK-owned facades so that core stays
dependency-free. On .NET four of the five have a runtime-shipped convergence point, so their facades retire under P2,
and the fifth takes the one recorded dependency. The mapping is summarised here because it is a seam decision; the
designs live in §8.

| Concern | Reference shape | .NET carrier | Basis |
|---|---|---|---|
| Logging | SDK logging facade over a compile-time API | `ILogger` from `Microsoft.Extensions.Logging.Abstractions`, defaulting to `NullLogger` | P14 over P2 — §2.4, §10 entry 1 |
| Tracing | SDK tracer/span abstraction with a no-op default | `ActivitySource("Dexpace.Sdk")`/`Activity`; `StartActivity` returns `null` with no listener | P2 (in-box) |
| Metrics | SDK meter abstraction | `Meter("Dexpace.Sdk")` instruments | P2 (in-box) |
| Time (**CFG-15**) | injectable clock with wall, monotonic and sleep | `TimeProvider` (in-box since .NET 8): `GetUtcNow`, `GetTimestamp`, `Task.Delay(..., TimeProvider, ...)` | P2 (in-box) |
| Configuration | layered lookup with env/system-property seams | plain options types in core; `IConfiguration`/`IOptions<T>` binding in `Dexpace.Sdk.Extensions.DependencyInjection` | P3 in core, P14 in the DI package |

**Summary of the seam decisions.**

| Reference seam | .NET outcome | Section |
|---|---|---|
| Byte-stream provider (**SEAM-3**–**SEAM-10**) | retired; IO contract carried by `Stream` plus internal helpers | §3.1 |
| Synchronous transport (**SEAM-11**–**SEAM-15**) | kept as `IHttpClient`, with a real synchronous path | §3.2 |
| Asynchronous transport and pivot (**SEAM-16**–**SEAM-18**, **SEAM-30**) | kept as `IAsyncHttpClient` over `Task<Response>`; ecosystem adapters collapse | §3.3 |
| Wire codec (**SEAM-19**–**SEAM-23**) | kept as `ISerde`; four profiles, two of them derived in core | §3.4 |
| Operation-input projection (**SEAM-26**–**SEAM-28**) | kept as `OperationDescriptor` | §3.5 |
| Discovery (**SEAM-5**–**SEAM-10**, **XCUT-23**) | retired; explicit construction and DI single-registration validation | §3.6 |
| Lifecycle (**SEAM-14**, **SEAM-25**, **XCUT-13**, **XCUT-22**) | `IDisposable`/`IAsyncDisposable`, latched and ownership-aware | §3.7 |

**As built (d45e64b):** built — `ILogger`, `ActivitySource`, `Meter`, `TimeProvider` and plain options types are all
in use as tabulated; the DI-side configuration binding is not built.

---
