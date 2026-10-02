# sse-streaming

## Rules
- A port aiming for parity with the SSE subsystem MUST replicate its deliberate deviations from strict WHATWG (comment exposure, permissive dispatch, EOF partial-dispatch), or offer a strict-WHATWG mode.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:5-5` · high · sha:dd401a407f5d</sub>
- SSE-1 (MUST) The stream MUST be parsed line by line, with a blank line as the event-dispatch boundary where accumulated fields collapse into exactly one event and fresh per-event accumulators govern the next block (e.g. `id: 1\ndata: a\n\ndata: b\n\n` yields a second event whose id is absent).
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:9-9` · high · sha:dd401a407f5d</sub>
- SSE-2 (MUST) Line termination MUST recognize LF, CR, and CRLF, treating CRLF as a single terminator with terminators stripped, and a lone CR terminates a line by itself.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:10-10` · high · sha:dd401a407f5d</sub>
- SSE-3 (MUST) A non-comment line MUST be split at its first colon into field name and value; with no colon the whole line is the field name with empty value, and a trailing colon yields an empty value; an unrecognized field with no colon dispatches no event.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:11-11` · high · sha:dd401a407f5d</sub>
- SSE-4 (MUST) A field present with an empty value (colon-less `data`, `data:`, or empty `event:`/`id:`) MUST be recorded as the empty string and count as a "field seen", distinct from the field being absent, and a port MUST NOT collapse present-but-empty into absent (e.g. `event:\ndata:x\n\n` gives event `''`, not absent).
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:12-12` · high · sha:dd401a407f5d</sub>
- SSE-5 (MUST) Extracting a field value or comment text MUST strip exactly one leading U+0020 SPACE immediately after the colon if present, preserving further leading spaces (`data:   hello` gives `  hello`).
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:13-13` · high · sha:dd401a407f5d</sub>
- SSE-6 (MUST) A line whose first character is `:` MUST be a comment whose text (after the single-space strip) is captured latest-wins within a block, and a comment counts as a "field seen" so a comment-only block dispatches (`:keep-alive\n\n` gives an event with comment `keep-alive` and empty data).
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:14-14` · high · sha:dd401a407f5d</sub>
- SSE-7 (MUST) Only field names `id`, `event`, `data`, and `retry` MUST be interpreted; any other field name MUST be silently discarded, setting no state and causing no dispatch.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:15-15` · high · sha:dd401a407f5d</sub>
- SSE-8 (MUST) Consecutive `data` fields within a block MUST accumulate in wire order into an ordered list of raw per-line values, and the parser MUST NOT join them (joining is deferred to the typed layer).
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:16-16` · high · sha:dd401a407f5d</sub>
- SSE-9 (MUST) An `id` value containing U+0000 NUL MUST be ignored entirely (does not set the id, does not count as a field seen, does not overwrite a valid id already seen in the block), and a valid id is stored verbatim, latest-wins.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:17-17` · high · sha:dd401a407f5d</sub>
- SSE-10 (MUST) The `event` field MUST be stored raw with latest-wins semantics and surfaced as absent/null when no `event` field was sent, and MUST NOT be defaulted to `message`.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:18-18` · high · sha:dd401a407f5d</sub>
- SSE-11 (MUST) The `retry` value MUST be accepted only if it consists solely of ASCII digits 0-9; a sign, embedded non-digit, empty value, or value exceeding the maximum representable millisecond magnitude MUST cause the field to be ignored, and an accepted value is surfaced as a non-negative millisecond duration, latest-wins.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:19-19` · high · sha:dd401a407f5d</sub>
- SSE-11 (MUST) The representable maximum of a retry value is runtime-specific (signed 64-bit milliseconds in the reference), and a port MUST pick a documented cap and reject values beyond it rather than wrap.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:19-19` · high · sha:dd401a407f5d</sub>
- SSE-12 (MUST) A single leading UTF-8 BOM (EF BB BF / U+FEFF) at the very start of the stream MUST be consumed once using non-consuming lookahead so a non-BOM prefix is left intact, and any BOM later in the stream MUST be preserved as ordinary data.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:20-20` · high · sha:dd401a407f5d</sub>
- SSE-13 (MUST) Dispatch MUST be permissive, emitting an event whenever any of the five tracked fields (id, event, data, comment, retry) was set in the block so id-only, retry-only, and comment-only events are visible, while a block in which no field was set (pure blank lines) MUST be skipped.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:24-24` · high · sha:dd401a407f5d</sub>
- SSE-14 (MUST) At end-of-stream, if fields have accumulated but no terminating blank line was seen the parser MUST dispatch the pending event, if no field accumulated it MUST signal end, and a final line without a terminator at EOF is returned as content.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:25-25` · high · sha:dd401a407f5d</sub>
- SSE-15 (MUST) `next()` MUST return an end-of-stream sentinel exactly when the source is exhausted with no pending dispatchable fields, and MUST continue to report end on subsequent calls.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:26-26` · high · sha:dd401a407f5d</sub>
- SSE-16 (MUST) The reader MUST be single-pass and stateful with only the "BOM already consumed" flag persisting across calls; the last-event-id is NOT carried forward, each event surfaces only the id in its own block, and a port MUST NOT maintain a WHATWG-style persistent last-event-id buffer inside the parser.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:30-30` · high · sha:dd401a407f5d</sub>
- SSE-17 (MUST) The reader MUST NOT own or close the underlying byte source; source lifecycle is the caller's responsibility, and resource ownership is introduced only by the stream facade.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:31-31` · high · sha:dd401a407f5d</sub>
- SSE-18 (MUST) A single reader instance MUST be driven from one thread at a time, the parser offers no thread-safety for concurrent `next()` calls, and a port MAY leave the parser non-thread-safe.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:32-32` · high · sha:dd401a407f5d</sub>
- SSE-19 (MAY) The parser MAY accept arbitrarily long lines/values with no built-in size cap (the reference imposes none), and a port MAY add a configurable cap that rejects or truncates oversized lines, documenting the divergence.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:33-33` · high · sha:dd401a407f5d</sub>
- SSE-20 (MUST) The parsed event MUST be immutable and hold a defensively-copied, read-only data list so neither the caller's originally-supplied list nor later mutations can reach inside a constructed event, and any copy-with-changes operation MUST likewise copy the data list.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:37-37` · high · sha:dd401a407f5d</sub>
- SSE-21 (SHOULD) The event SHOULD provide structural value semantics, with equality and hash over all five fields and a stable string form.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:38-38` · high · sha:dd401a407f5d</sub>
- SSE-22 (SHOULD) The event SHOULD expose an is-empty predicate true only when all five fields are unset/empty, so a comment-only event reports non-empty because a comment counts as content.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:39-39` · high · sha:dd401a407f5d</sub>
- SSE-23 (MUST) The facade MUST own exactly one closeable resource and MUST close it exactly once across the stream's whole life, regardless of termination (clean end, explicit close, use-block exit, partial consume, mid-stream failure).
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:45-45` · high · sha:dd401a407f5d</sub>
- SSE-24 (MUST) On reader end-of-stream during iteration the facade MUST both terminate the iterator cleanly and release the resource, so a fully-consumed stream needs no explicit close.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:46-46` · high · sha:dd401a407f5d</sub>
- SSE-25 (MUST) A partial consume MUST NOT strand the resource, so closing after reading only some events MUST release it.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:47-47` · high · sha:dd401a407f5d</sub>
- SSE-26 (MUST) The facade MUST be single-pass, obtaining an iterator succeeds at most once, and a second attempt MUST fail loudly.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:48-48` · high · sha:dd401a407f5d</sub>
- SSE-27 (MUST) After close, requesting an iterator MUST fail loudly and an in-flight iterator MUST observe the closed state and end cleanly on its next pull, with neither reading from the torn-down resource.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:49-49` · high · sha:dd401a407f5d</sub>
- SSE-28 (MUST) `close()` MUST be idempotent, with only the first call propagating to the owned resource, and this MUST hold even after an automatic release on a terminal or failure path.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:50-50` · high · sha:dd401a407f5d</sub>
- SSE-29 (MUST) A mid-stream reader failure MUST release the resource before the error propagates, and if releasing itself fails while an error is in flight the release failure MUST be attached to the primary error as a suppressed/secondary throwable.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:51-51` · high · sha:dd401a407f5d</sub>
- SSE-30 (MUST) A release failure on an automatic clean-terminal path (natural end or done-sentinel, no error in flight) MUST NOT be turned into a thrown result that discards delivered events and MUST be reported out-of-band and swallowed, while a release failure during an explicit `close()` MUST propagate.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:52-52` · high · sha:dd401a407f5d</sub>
- SSE-31 (MUST) `close()` MUST be safe from a different thread than the one iterating, with the closed state guarded atomically, so a close between pulls ends iteration cleanly while a close that tears the resource down during an in-flight blocked read surfaces as a read failure (an I/O error), and either way the resource is released exactly once.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:53-53` · high · sha:dd401a407f5d</sub>
- SSE-32 (MUST) The convenience that opens a stream over an HTTP response MUST bind the stream's lifecycle to the response body (closing the stream closes the response) and MUST fail loudly if the response has no body.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:54-54` · high · sha:dd401a407f5d</sub>
- SSE-33 (MUST) The typed adapter MUST invoke the mapper with (event-name, joined-data), where event-name is the raw `event` field (absent/null if omitted) and joined-data is the data lines joined with a single `\n` (empty string when no data), and MUST yield the mapper's decoded value.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:58-58` · high · sha:dd401a407f5d</sub>
- SSE-34 (MUST) The typed adapter MUST honor the mapper's three outcomes: a value is yielded; a Skip silently drops the event and advances without surfacing to the consumer; a Done ends iteration cleanly and closes the stream without yielding a model for the sentinel event.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:59-59` · high · sha:dd401a407f5d</sub>
- SSE-35 (MUST) Typed decoding MUST be lazy and per-element, so the mapper runs only when the consumer pulls the next element and a partial consume decodes only the events taken.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:60-60` · high · sha:dd401a407f5d</sub>
- SSE-36 (MUST) A mapper that throws MUST propagate the exception to the consumer's pull but MUST first release the underlying resource, and a resulting release failure MUST be attached to the mapper error as suppressed.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:61-61` · high · sha:dd401a407f5d</sub>
- SSE-38 (MUST) Reconnection and last-event-id continuity MUST remain the caller's responsibility, so the subsystem surfaces the retry hint and each event's raw id but MUST NOT auto-reconnect, MUST NOT persist a last-event-id across events, and MUST NOT set a reconnect request header.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:66-66` · high · sha:dd401a407f5d</sub>
- SSE-38 (MAY) A callback listener contract MAY be offered whose retry/close/error hooks default to no-ops and are not auto-driven by core.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:66-66` · high · sha:dd401a407f5d</sub>
- SSE-39 (MUST) Event delivery MUST be pull-based with no eager read-ahead, the parser advancing the source only when the consumer requests the next event, so a blocking source read is the backpressure mechanism and no unbounded internal buffer accumulates.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:67-67` · high · sha:dd401a407f5d</sub>
- SSE-39 (MUST) A reactive/async adapter MUST preserve pull-based delivery by polling the source at most once per unit of downstream demand, while the typed layer may pull several raw events per yielded element to drain Skips but only as many as needed to produce one element.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:67-67` · high · sha:dd401a407f5d</sub>
- SSE-40 (SHOULD) Sequence/iterable convenience views over a raw source SHOULD be lazy, single-pass, propagate read exceptions at the offending pull, and reuse one reader instance so per-stream state (BOM consumption) is preserved, and MUST NOT be invoked twice on the same source.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:68-68` · high · sha:dd401a407f5d</sub>
- SSE-41 (MAY) A reactive adapter MAY catch only recoverable exceptions and let the runtime's fatal/VM error family escape rather than routing it through the error channel, and MAY leave source lifecycle to the caller.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:69-69` · high · sha:dd401a407f5d</sub>
- SSE-41 (SHOULD) A port SHOULD apply its own runtime's fatal/non-fatal exception split and document source ownership.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:69-69` · high · sha:dd401a407f5d</sub>
- SSE-1: a blank line dispatches an event and each block starts with fresh accumulators.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-2: LF, CR and CRLF are all accepted as line terminators.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-3: a field line is split at the first colon, and a colon-less line or a trailing colon yields an empty value.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-4: a present-but-empty field value is distinct from an absent field.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-5: a single leading space after the colon is stripped.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-6: a captured comment counts as event content.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-7: unknown fields are discarded.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-8: multiple data fields accumulate as a list.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-9: an id containing NUL is ignored.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-10: an absent event field is not defaulted to `message`.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-11: retry accepts digits only and rejects overflow.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-12: a BOM is stripped only at the start of the stream and a mid-stream BOM is preserved.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20-20` · high · sha:0451cc7f3bb4</sub>
- SSE-13: permissive dispatch emits id-only, retry-only and comment-only blocks, while blocks with no fields are skipped.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21-21` · high · sha:0451cc7f3bb4</sub>
- SSE-14: a partial event at EOF is dispatched.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21-21` · high · sha:0451cc7f3bb4</sub>
- SSE-15: the reader returns a stable end sentinel at end of stream.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21-21` · high · sha:0451cc7f3bb4</sub>
- SSE-16: the reader keeps no persistent last-event-id.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21-21` · high · sha:0451cc7f3bb4</sub>
- SSE-17: the reader does not own its source.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21-21` · high · sha:0451cc7f3bb4</sub>
- SSE-18: the reader has a single-thread contract.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21-21` · high · sha:0451cc7f3bb4</sub>
- SSE-19: unbounded line lengths are accepted or a documented cap exists.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21-21` · high · sha:0451cc7f3bb4</sub>
- SSE-20: the event is immutable with defensively copied data.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:22-22` · high · sha:0451cc7f3bb4</sub>
- SSE-21: event value equality covers five fields.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:22-22` · high · sha:0451cc7f3bb4</sub>
- SSE-22: the is-empty predicate treats an event with a comment as non-empty.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:22-22` · high · sha:0451cc7f3bb4</sub>
- SSE-23, SSE-24 and SSE-25: the facade closes the owned resource exactly once across clean end, explicit close, use-block, partial consumption and mid-stream failure.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23-23` · high · sha:0451cc7f3bb4</sub>
- SSE-26: the facade iterator is single-pass.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23-23` · high · sha:0451cc7f3bb4</sub>
- SSE-27: an iterator obtained after close fails while an in-flight iteration ends cleanly.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23-23` · high · sha:0451cc7f3bb4</sub>
- SSE-28: close is idempotent.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23-23` · high · sha:0451cc7f3bb4</sub>
- SSE-29: a mid-stream failure releases the resource before propagating, with a close error suppressed.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23-23` · high · sha:0451cc7f3bb4</sub>
- SSE-30: a release failure at automatic termination is swallowed while an explicit close propagates it.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23-23` · high · sha:0451cc7f3bb4</sub>
- SSE-31: close may be invoked from another thread.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23-23` · high · sha:0451cc7f3bb4</sub>
- SSE-32: the response-opening convenience binds the lifecycle and rejects a bodyless response.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23-23` · high · sha:0451cc7f3bb4</sub>
- SSE-33: the typed adapter mapper receives the event name and the data joined with `\n`.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:24-24` · high · sha:0451cc7f3bb4</sub>
- SSE-34: the mapper results Value, Skip and Done are honored.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:24-24` · high · sha:0451cc7f3bb4</sub>
- SSE-35: decoding is lazy per element.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:24-24` · high · sha:0451cc7f3bb4</sub>
- SSE-36: a mapper throw releases the resource first.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:24-24` · high · sha:0451cc7f3bb4</sub>
- SSE-37: core holds no sentinel, error or serde convention for SSE.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25-25` · high · sha:0451cc7f3bb4</sub>
- SSE-38: there is no auto-reconnect and no last-event-id header.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25-25` · high · sha:0451cc7f3bb4</sub>
- SSE-39: SSE is pull-based with one poll per demand.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25-25` · high · sha:0451cc7f3bb4</sub>
- SSE-40: lazy single-pass convenience views reuse one reader.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25-25` · high · sha:0451cc7f3bb4</sub>
- SSE-41: the reactive fatal versus non-fatal split and source ownership are documented.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25-25` · high · sha:0451cc7f3bb4</sub>
- The leading-BOM check inspects the first three bytes once and keeps them if they are not EF BB BF (SSE-12).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:197-198` · high · sha:68af5c6bf0ea</sub>
- The SSE parser is non-thread-safe by contract (SSE-18) and never owns or disposes the stream it reads (SSE-17).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:208-209` · high · sha:68af5c6bf0ea</sub>
- ServerSentEvent overrides Equals/GetHashCode to compare Data element-wise (SSE-21).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:213-216` · high · sha:68af5c6bf0ea</sub>
- SSE-20's defensive copy lives in the Data property's init accessor, which copies into a private array, so neither the constructor argument nor a `with { Data = list }` expression can alias a mutable list, while a `with` that leaves Data alone shares the already-immutable array.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:216-219` · high · sha:68af5c6bf0ea</sub>
- ServerSentEvent.IsEmpty (SSE-22) is true only when all five fields are null, so a comment-only keep-alive is non-empty.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:219-220` · high · sha:68af5c6bf0ea</sub>
- The raw view ServerSentEventReader.ReadAsync(Stream) (SSE-40) reuses one reader across pulls so BOM consumption is per-stream, and is single-pass via the same latched guard as the page view because a second view would resume mid-stream.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:227-229` · high · sha:68af5c6bf0ea</sub>
- The facade's closed state is an Interlocked.Exchange flag, making close idempotent (SSE-28) and safe from another thread (SSE-31), and a close observed between pulls ends iteration cleanly.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:238-240` · high · sha:68af5c6bf0ea</sub>
- A close that disposes the body during an in-flight ReadAsync surfaces as an IOException from that read, which the facade normalises (a transport body may throw ObjectDisposedException instead) so SSE-31's "surfaces as a read failure (an I/O error)" holds across transports.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:239-242` · high · sha:68af5c6bf0ea</sub>
- SSE-26/SSE-27 are implemented as a single-use latch plus a closed check at GetAsyncEnumerator.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:243-244` · high · sha:68af5c6bf0ea</sub>
- SSE-29 and SSE-36 use section 7.1's suppressed-error helper and never a throwing finally.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:244-244` · high · sha:68af5c6bf0ea</sub>
- SSE-32's response-opening factory binds the stream to the response and fails loudly on a bodyless response (a 204, or a response whose content length is zero).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:247-249` · high · sha:68af5c6bf0ea</sub>
- The typed adapter decodes lazily per element as an async iterator (SSE-35), a Skip pulls the next raw event, and a Done closes the stream and completes without yielding the sentinel (SSE-34).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:254-255` · high · sha:68af5c6bf0ea</sub>
- SSE-38 states the subsystem MUST NOT auto-reconnect, MUST NOT persist a last-event-id across events, and MUST NOT set a reconnect request header.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:261-263` · high · sha:68af5c6bf0ea</sub>
- SSE-37's hard boundary of no serialization dependency is enforced by an architecture test asserting no type in Dexpace.Sdk.Core.ServerSentEvents or the Dexpace.Sdk.Core.Pagination engine references Dexpace.Sdk.Core.Serialization (section 9.2), while the paging factory takes an ISerde by design so the rule binds the engine and SSE namespace, not the convenience entry point.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:265-268` · high · sha:68af5c6bf0ea</sub>
- The webhook body is verified as ReadOnlySpan<byte> because a string round trip can change the signed bytes.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:408-409` · high · sha:68af5c6bf0ea</sub>
- Webhook signature comparison uses CryptographicOperations.FixedTimeEquals.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:409-409` · high · sha:68af5c6bf0ea</sub>
- Webhook time comes from TimeProvider.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:409-409` · high · sha:68af5c6bf0ea</sub>

## Constraints
- The SSE subsystem owns no reconnection policy, no last-event-id continuity, and no per-API sentinel conventions; those live in caller-supplied code.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:3-3` · high · sha:dd401a407f5d</sub>
- SSE-37 (MUST) Core parsing/streaming MUST remain format- and API-agnostic, with no built-in done-sentinel, no error-envelope recognition, and no serialization dependency, all such conventions living only in the caller-supplied mapper.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:65-65` · high · sha:dd401a407f5d</sub>
- System.Net.ServerSentEvents.SseParser is in the shared framework from .NET 10 and a NuGet package on .NET 8, and it implements strict WHATWG dispatch, which the specification's preamble says a port must replicate the deviations from or offer as a strict-WHATWG mode.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:161-166` · high · sha:68af5c6bf0ea</sub>
- SseParser reports an event with no event: field as type "message", violating SSE-10 ("MUST NOT be defaulted to message").
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:166-167` · high · sha:68af5c6bf0ea</sub>
- SseParser joins `data: a\ndata: b` into `a\nb`, violating SSE-8 ("the parser MUST NOT join them").
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:167-168` · high · sha:68af5c6bf0ea</sub>
- SseParser dispatches nothing for a comment-only block, an id-only block or a retry-only block, violating SSE-6 and SSE-13.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:168-169` · high · sha:68af5c6bf0ea</sub>
- SseParser drops an unterminated final `data: hello` at end of stream, violating SSE-14.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:169-170` · high · sha:68af5c6bf0ea</sub>
- SseParser keeps a LastEventId across events, the WHATWG buffer that SSE-16 forbids.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:170-170` · high · sha:68af5c6bf0ea</sub>
- StreamReader's byte-order-mark detection is on by default and switches encodings (bytes FF FE 64 00 61 00 0A 00 came back as the line "da"), whereas an event stream is UTF-8 by definition.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:179-181` · high · sha:68af5c6bf0ea</sub>
- StreamReader.ReadLine blocks on a lone CR at the end of the currently available bytes until more bytes arrive (it must peek for an LF), so a server terminating events with CR has every event delivered one event late, which SSE-2's "a lone CR terminates a line by itself" excludes.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:185-190` · high · sha:68af5c6bf0ea</sub>
- System.IO.Pipelines is in the shared framework from .NET 9 but only a NuGet package on .NET 8 (present in the 9.0.18 and 10.0.12 runtimes, absent from the 8.0.31 reference pack), so core cannot use PipeReader before the floor rises (sections 2.4, 9.2).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:191-193` · high · sha:68af5c6bf0ea</sub>
- A C# record's synthesized Equals compares collection members by reference (two records wrapping equal int[] contents compare unequal), as do ImmutableArray<T> and ReadOnlyCollection<T>, so SSE-21's structural equality is not free.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:213-216` · high · sha:68af5c6bf0ea</sub>
- Because the webhook verifier takes an ISerde for its Unwrap<T> convenience, it must stay outside the SSE and paging engines' serde boundary (SSE-37).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:409-410` · high · sha:68af5c6bf0ea</sub>

## Conclusions
- The WHATWG SSE parser, the one place in the SDK that wants a lone \r to terminate a line, uses the same internal line reader in its SSE mode rather than relying on StreamReader's accidental agreement.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:79-80` · high · sha:da6000c93fc5</sub>
- Core implements its own SSE parser rather than using SseParser, which looks like the answer but is the strict mode the specification declines to make the default, and is also unavailable in-box on the net8.0 floor; the replicate-versus-strict-mode choice is resolved as section 11 item 17.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:170-173` · high · sha:68af5c6bf0ea</sub>
- An adapter to SseItem<T> for callers who want strict WHATWG semantics is a possible later convenience, not a seam.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:174-174` · high · sha:68af5c6bf0ea</sub>
- StreamReader.ReadLineAsync is rejected for the SSE line reader even though its terminator handling is correct (LF, CR and CRLF end a line and an unterminated final line is returned, per SSE-2 and SSE-14).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:176-179` · high · sha:68af5c6bf0ea</sub>
- The SSE line reader is a hand-written byte-level state machine over Stream.ReadAsync into an ArrayPool<byte>-rented buffer, where a CR ends the line immediately and sets a one-bit "swallow a following LF" flag so no read waits on lookahead.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:195-198` · high · sha:68af5c6bf0ea</sub>
- SSE-19 is a MAY with a documented-divergence escape, and the reader exercises it by failing the read on lines over a configurable cap (default 1 MiB, matching BODY-30's error-body cap) because an unbounded line from a hostile server is an unbounded allocation (recorded as sanctioned, section 10 entry 20).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:199-203` · high · sha:68af5c6bf0ea</sub>
- SSE-11's requirement to pick a documented cap and reject beyond it rather than wrap is met with a cap of int.MaxValue milliseconds (about 24.8 days), accumulating digits with an overflow check, ignoring anything longer, and surfacing the value as a TimeSpan.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:203-206` · high · sha:68af5c6bf0ea</sub>
- The retry cap of int.MaxValue milliseconds was chosen because every .NET timer API accepts it and it matches the Ruby port's cap so two ports agree on the same fixture.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:206-207` · high · sha:68af5c6bf0ea</sub>
- ServerSentEvent is a sealed record over the five fields Id, Event, Data, Comment and Retry, with Data an IReadOnlyList<string> and every absent field null rather than empty, so SSE-4's present-but-empty stays distinct from absent.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:211-215` · high · sha:68af5c6bf0ea</sub>
- SSE delivery is pull-based (SSE-39): the parser advances only when the consumer's MoveNextAsync asks, a pending ReadAsync is the backpressure mechanism, and no queue sits between parser and consumer.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:222-224` · high · sha:68af5c6bf0ea</sub>
- The sync twin, IEnumerable<ServerSentEvent> over Stream.Read, is a real synchronous path on .NET and ships with the blocking pipeline (section 5.3).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:229-230` · high · sha:68af5c6bf0ea</sub>
- The streaming facade (SSE-23 to SSE-32) is ServerSentEventStream : IAsyncEnumerable<ServerSentEvent>, IAsyncDisposable, IDisposable, owning exactly one Response.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:232-233` · high · sha:68af5c6bf0ea</sub>
- Unlike the pager, the SSE facade holds its response across yields because an event stream is long-lived, and does not rely on the enumerator's finally alone: it exposes DisposeAsync itself and its enumerator's finally calls the same close-once helper, so await using releases it even if the caller enumerated by hand and abandoned the enumerator (SSE-25).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:233-238` · high · sha:68af5c6bf0ea</sub>
- SSE-30's sync/async asymmetry is two call sites into one helper: the automatic terminal path logs a release failure at Warning through the stream's ILogger and swallows it, while an explicit DisposeAsync rethrows.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:245-247` · high · sha:68af5c6bf0ea</sub>
- The typed adapter (SSE-33 to SSE-36) is a facade method taking Func<string?, string, SseMapResult<T>> over the raw event name and the data lines joined with a single \n, where SseMapResult<T> is a readonly struct with factories Value(T), Skip and Done matched exhaustively.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:251-253` · high · sha:68af5c6bf0ea</sub>
- The pre-roadmap SSE sketch (ServerSentEvent with default "message" event type, sticky id, joined data, dropped comments, and a reconnecting stream using Last-Event-ID and retry:) was overturned because each element conflicts with a MUST (SSE-10, SSE-16, SSE-8, SSE-6/SSE-13, SSE-38) and reproduced SseParser's WHATWG semantics one layer up.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:257-264` · high · sha:68af5c6bf0ea</sub>
- The reconnect loop is documented as a caller recipe over the retry hint and each event's raw id and does not ship.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:263-265` · high · sha:68af5c6bf0ea</sub>
- ASYNC-21's reactive adapter is optional: an IObservable<T> bridge over System.Reactive would be an adapter package, not a seam, and would inherit the one-poll-per-demand property from the pull-based reader (section 11 item 21).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:268-270` · high · sha:68af5c6bf0ea</sub>
- Webhooks (Standard Webhooks HMAC-SHA256 verification behind an IWebhookVerifier seam, never built) have no counterpart in the specification, so the design document neither overturns nor adopts the PR #3 webhooks design beyond three .NET facts.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:404-408` · high · sha:68af5c6bf0ea</sub>

## Reference
- The SSE subsystem parses a byte stream into discrete events following the WHATWG SSE line/field grammar, exposes each event as an immutable value, and layers a resource-owning, single-pass, lazily-decoding streaming facade (raw and typed) whose lifecycle is bound to the underlying HTTP response.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:3-3` · high · sha:dd401a407f5d</sub>
- Having no line-size cap is a potential unbounded-memory surface when reading from untrusted servers.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:33-33` · high · sha:dd401a407f5d</sub>
- The streaming facade wraps a reader plus a closeable resource (the response/body) and guarantees exactly-once release across every termination path.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:43-43` · high · sha:dd401a407f5d</sub>
- The out-of-band reporting mechanism for swallowed release failures (a WARN log in the reference) is an implementation detail, while the swallow-versus-propagate split is the portable contract.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:52-52` · high · sha:dd401a407f5d</sub>
- In the reference implementation, sdk-core carries zero serialization dependency, making SSE format-agnosticism a hard architectural invariant.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:65-65` · high · sha:dd401a407f5d</sub>
- new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: false) strips exactly one leading EF BB BF and preserves a second consecutive BOM and any later one as U+FEFF, which is SSE-12, while new UTF8Encoding(false) strips none.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:181-184` · high · sha:68af5c6bf0ea</sub>
- StreamReader also allocates a string per line and has no length bound.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:190-190` · high · sha:68af5c6bf0ea</sub>
- Each completed SSE line is decoded with Encoding.UTF8, whose default replacement fallback turns malformed input into U+FFFD rather than throwing.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:198-199` · high · sha:68af5c6bf0ea</sub>
- SSE-1, SSE-3 to SSE-9 and SSE-13 to SSE-16 (field and dispatch grammar) are implemented as written on top of the line reader.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:207-208` · high · sha:68af5c6bf0ea</sub>
- The reader's byte buffer is a read-ahead of bytes, not events (a chunk may hold several events and the stream is not read again until they are consumed), which is the level at which SSE-39's conformance of raw next() call count against consumer pulls measures.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:224-227` · high · sha:68af5c6bf0ea</sub>
- Cancelling the enumeration token is the cooperative alternative to closing the stream and surfaces OperationCanceledException.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:242-243` · high · sha:68af5c6bf0ea</sub>
- As built (d45e64b), the SSE subsystem is not built.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:272-272` · high · sha:68af5c6bf0ea</sub>
- As built (d45e64b), webhooks are not built.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:412-412` · high · sha:68af5c6bf0ea</sub>

## Conflicts

## Superseded

