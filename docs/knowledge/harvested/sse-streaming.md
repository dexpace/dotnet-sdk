# sse-streaming

## Rules
- A port aiming for parity with the SSE subsystem MUST replicate its deliberate deviations from strict WHATWG (comment exposure, permissive dispatch, EOF partial-dispatch) or else offer a strict-WHATWG mode.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:5-5` · high · sha:dd401a407f5d</sub>
- The SSE stream MUST be parsed line by line with a blank line acting as the event-dispatch boundary, collapsing accumulated fields into exactly one event and resetting per-event accumulators for the next block. (SSE-1)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:9-9` · high · sha:dd401a407f5d</sub>
- SSE line termination MUST recognize LF, CR, and CRLF, treating CRLF as a single terminator with terminators stripped, and a lone CR terminates a line by itself. (SSE-2)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:10-10` · high · sha:dd401a407f5d</sub>
- A non-comment SSE line MUST be split at its first colon into field name and value, with no colon yielding an empty-value field named after the whole line and a trailing colon yielding an empty value. (SSE-3)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:11-11` · high · sha:dd401a407f5d</sub>
- An SSE field present with an empty value MUST be recorded as an empty string and counted as "field seen", distinct from an absent field, and a port MUST NOT collapse present-but-empty into absent. (SSE-4)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:12-12` · high · sha:dd401a407f5d</sub>
- Extracting an SSE field value or comment text MUST strip exactly one leading U+0020 space immediately after the colon if present, preserving further leading spaces. (SSE-5)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:13-13` · high · sha:dd401a407f5d</sub>
- An SSE line whose first character is `:` MUST be treated as a comment, captured latest-wins within a block after stripping one leading space, and a comment counts as a "field seen" that triggers dispatch on its own. (SSE-6)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:14-14` · high · sha:dd401a407f5d</sub>
- Only the SSE field names `id`, `event`, `data`, and `retry` MUST be interpreted; any other field name MUST be silently discarded, setting no state and causing no dispatch. (SSE-7)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:15-15` · high · sha:dd401a407f5d</sub>
- Consecutive `data` fields within an SSE block MUST accumulate in wire order into an ordered list of raw per-line values, with line-joining deferred to the typed layer. (SSE-8)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:16-16` · high · sha:dd401a407f5d</sub>
- An SSE `id` value containing a U+0000 NUL MUST be ignored entirely — not setting the id, not counting as a field seen, and not overwriting a valid id already seen in the same block — while a valid id is stored verbatim with latest-wins semantics. (SSE-9)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:17-17` · high · sha:dd401a407f5d</sub>
- The SSE `event` field MUST be stored raw with latest-wins semantics and surfaced as absent/null when no `event` field was sent, and it MUST NOT be defaulted to `message`. (SSE-10)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:18-18` · high · sha:dd401a407f5d</sub>
- An SSE `retry` value MUST be accepted only if it consists solely of ASCII digits, with a sign, embedded non-digit, empty value, or value exceeding the maximum representable millisecond magnitude causing the field to be ignored, and an accepted value surfaces as a non-negative millisecond duration. (SSE-11)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:19-19` · high · sha:dd401a407f5d</sub>
- A single leading UTF-8 BOM at the very start of an SSE stream MUST be consumed once via non-consuming lookahead, while any BOM occurring later in the stream MUST be preserved as ordinary data. (SSE-12)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:20-20` · high · sha:dd401a407f5d</sub>
- SSE dispatch MUST be permissive, emitting an event whenever any of the five tracked fields (id, event, data, comment, retry) was set in the block, while a block with no field set MUST be skipped. (SSE-13)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:24-24` · high · sha:dd401a407f5d</sub>
- At end-of-stream, if SSE fields have accumulated but no terminating blank line was seen, the parser MUST dispatch the pending event, and if no field accumulated it MUST signal end, with a final unterminated line at EOF returned as content. (SSE-14)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:25-25` · high · sha:dd401a407f5d</sub>
- The SSE reader's `next()` MUST return an end-of-stream sentinel exactly when the source is exhausted with no pending dispatchable fields, and MUST continue to report end on subsequent calls. (SSE-15)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:26-26` · high · sha:dd401a407f5d</sub>
- The SSE reader MUST be single-pass and stateful, persisting only the "BOM already consumed" flag across calls, and MUST NOT carry the last-event-id forward across events. (SSE-16)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:30-30` · high · sha:dd401a407f5d</sub>
- The SSE reader MUST NOT own or close the underlying byte source; source lifecycle is the caller's responsibility, with resource ownership introduced only by the stream facade. (SSE-17)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:31-31` · high · sha:dd401a407f5d</sub>
- A parsed SSE event MUST be immutable and hold a defensively-copied, read-only data list so neither the caller's original list nor later mutations can reach inside it, and any copy-with-changes operation must likewise copy the data list. (SSE-20)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:37-37` · high · sha:dd401a407f5d</sub>
- An SSE event SHOULD provide structural value semantics — equality/hash over all five fields and a stable string form. (SSE-21)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:38-38` · high · sha:dd401a407f5d</sub>
- An SSE event SHOULD expose an is-empty predicate that is true only when all five fields are unset/empty, so a comment-only event reports non-empty since a comment counts as content. (SSE-22)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:39-39` · high · sha:dd401a407f5d</sub>
- The SSE streaming facade MUST own exactly one closeable resource and MUST close it exactly once across the stream's whole life, regardless of termination path. (SSE-23)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:45-45` · high · sha:dd401a407f5d</sub>
- On reader end-of-stream during iteration, the SSE streaming facade MUST both terminate the iterator cleanly and release the resource, so a fully-consumed stream needs no explicit close. (SSE-24)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:46-46` · high · sha:dd401a407f5d</sub>
- A partial consume of the SSE streaming facade MUST NOT strand the resource — closing after reading only some events MUST release it. (SSE-25)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:47-47` · high · sha:dd401a407f5d</sub>
- The SSE streaming facade MUST be single-pass, so obtaining an iterator succeeds at most once and a second attempt MUST fail loudly. (SSE-26)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:48-48` · high · sha:dd401a407f5d</sub>
- After the SSE streaming facade is closed, requesting an iterator MUST fail loudly, and an in-flight iterator MUST observe the closed state and end cleanly on its next pull. (SSE-27)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:49-49` · high · sha:dd401a407f5d</sub>
- The SSE streaming facade's `close()` MUST be idempotent, with only the first call propagating to the owned resource, even after an automatic release on a terminal/failure path. (SSE-28)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:50-50` · high · sha:dd401a407f5d</sub>
- A mid-stream SSE reader failure MUST release the resource before the error propagates, and if releasing itself fails while an error is in flight, that release failure MUST be attached to the primary error as suppressed/secondary. (SSE-29)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:51-51` · high · sha:dd401a407f5d</sub>
- A release failure on an automatic clean-terminal path of the SSE facade MUST NOT be turned into a thrown result that discards delivered events and MUST instead be reported out-of-band and swallowed, while a release failure during an explicit `close()` MUST propagate. (SSE-30)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:52-52` · high · sha:dd401a407f5d</sub>
- The SSE streaming facade's `close()` MUST be safe to call from a different thread than the one iterating, with the closed state guarded atomically so a close between pulls ends iteration cleanly while a close tearing down an in-flight blocked read surfaces as a read failure. (SSE-31)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:53-53` · high · sha:dd401a407f5d</sub>
- The convenience that opens an SSE stream over an HTTP response MUST bind the stream's lifecycle to the response body, so closing the stream closes the response, and MUST fail loudly if the response has no body. (SSE-32)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:54-54` · high · sha:dd401a407f5d</sub>
- The SSE typed adapter MUST invoke the mapper with the raw `event` field name (absent if omitted) and the data lines joined with a single `\n`, and MUST yield the mapper's decoded value. (SSE-33)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:58-58` · high · sha:dd401a407f5d</sub>
- The SSE typed adapter MUST honor three mapper outcomes: a value is yielded, a Skip silently drops the event and advances, and a Done ends iteration cleanly and closes the stream without yielding a model for the sentinel event. (SSE-34)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:59-59` · high · sha:dd401a407f5d</sub>
- SSE typed decoding MUST be lazy and per-element, running the mapper only when the consumer pulls the next element, so a partial consume decodes only the events taken. (SSE-35)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:60-60` · high · sha:dd401a407f5d</sub>
- A mapper that throws in the SSE typed adapter MUST propagate the exception to the consumer's pull but must first release the underlying resource, with any resulting release failure attached to the mapper error as suppressed. (SSE-36)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:61-61` · high · sha:dd401a407f5d</sub>
- Reconnection and last-event-id continuity MUST remain the caller's responsibility — the SSE subsystem surfaces the retry hint and each event's raw id but MUST NOT auto-reconnect, persist a last-event-id, or set a reconnect request header. (SSE-38, SSE-16)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:66-66` · high · sha:dd401a407f5d</sub>
- SSE event delivery MUST be pull-based with no eager read-ahead, advancing the source only when the consumer requests the next event so a blocking source read is the backpressure mechanism and no unbounded internal buffer accumulates. (SSE-39)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:67-67` · high · sha:dd401a407f5d</sub>
- Sequence/iterable convenience views over a raw SSE source SHOULD be lazy, single-pass, propagate read exceptions at the offending pull, and reuse one reader instance to preserve per-stream state, and MUST NOT be invoked twice on the same source. (SSE-40)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:68-68` · high · sha:dd401a407f5d</sub>

## Constraints
- A single SSE reader instance MUST be driven from one thread at a time, since the parser offers no thread-safety for concurrent `next()` calls. (SSE-18)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:32-32` · high · sha:dd401a407f5d</sub>
- Core SSE parsing/streaming MUST remain format- and API-agnostic with no built-in done-sentinel, no error-envelope recognition, and no serialization dependency, since sdk-core carries zero serialization dependency as a hard architectural invariant. (SSE-37)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:65-65` · high · sha:dd401a407f5d</sub>

## Conclusions
- The SSE subsystem deliberately owns no reconnection policy, no last-event-id continuity, and no per-API sentinel conventions, leaving those to caller-supplied code.
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:3-3` · high · sha:dd401a407f5d</sub>

## Reference
- The SSE parser MAY accept arbitrarily long lines/values with no built-in size cap, which is a potential unbounded-memory surface for untrusted servers that a port may bound with a configurable cap. (SSE-19)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:33-33` · high · sha:dd401a407f5d</sub>
- A reactive SSE adapter MAY catch only recoverable exceptions and let the runtime's fatal/VM error family escape rather than routing it through the error channel, and MAY leave source lifecycle to the caller. (SSE-41)
  <sub>spec · `docs/product-spec/13-server-sent-events-and-streaming.md:69-69` · high · sha:dd401a407f5d</sub>
- Backpressure is flow control in which a consumer's demand governs how fast a producer is polled, and in this SDK the blocking source read is the backpressure mechanism for SSE. (SSE-39)
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:9` · high · sha:f0b3d2058626</sub>
- A cold publisher performs per-subscription capture, meaning a reusable async object that (re)issues its request and (re)captures logging context on each subscription rather than once at assembly time.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:15` · high · sha:f0b3d2058626</sub>
- Dispatch is the framing act, triggered by a blank line, of collapsing the fields accumulated since the previous boundary into one Server-Sent Event.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:27` · high · sha:f0b3d2058626</sub>
- On the SSE / body-preview exceeds-cap path, the live tail is the still-open delegate source retained after the prefix was captured, carrying the un-buffered remainder and readable exactly once.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:33` · high · sha:f0b3d2058626</sub>
- A blank line triggers SSE event dispatch, and each block after dispatch starts with fresh field accumulators. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- SSE parsing recognizes LF, CR, and CRLF as line terminators. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- An SSE field is split at the first colon, and a colon-less line or a trailing colon yields an empty value. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- A present-but-empty SSE field value is distinguished from an absent field. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- A single leading space after the colon in an SSE field value is stripped. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- An SSE comment line's content still counts toward whether the block is empty. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- Unknown SSE field names are discarded. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- Multiple `data` lines within one SSE block accumulate into a list. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- An SSE `id` field containing a NUL byte is ignored. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- An absent SSE `event` field is not defaulted to "message". (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- The SSE `retry` field accepts digit-only values and rejects on overflow. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- An SSE byte-order-mark is stripped only at the very start of the stream, while a mid-stream BOM is preserved. (SSE-1, SSE-2, SSE-3, SSE-4, SSE-5, SSE-6, SSE-7, SSE-8, SSE-9, SSE-10, SSE-11, SSE-12)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:20` · high · sha:0451cc7f3bb4</sub>
- An SSE block containing only id, retry, or comment fields still dispatches permissively, while a block with no fields at all is skipped. (SSE-13, SSE-14, SSE-15, SSE-16, SSE-17, SSE-18, SSE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21` · high · sha:0451cc7f3bb4</sub>
- A partial SSE block still present at end-of-file is dispatched. (SSE-13, SSE-14, SSE-15, SSE-16, SSE-17, SSE-18, SSE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21` · high · sha:0451cc7f3bb4</sub>
- SSE parsing produces a stable, distinct end-of-stream sentinel value. (SSE-13, SSE-14, SSE-15, SSE-16, SSE-17, SSE-18, SSE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21` · high · sha:0451cc7f3bb4</sub>
- The SSE reader does not persist a last-event-id across reconnects. (SSE-13, SSE-14, SSE-15, SSE-16, SSE-17, SSE-18, SSE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21` · high · sha:0451cc7f3bb4</sub>
- The SSE reader does not own or close the underlying source it reads from. (SSE-13, SSE-14, SSE-15, SSE-16, SSE-17, SSE-18, SSE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21` · high · sha:0451cc7f3bb4</sub>
- The SSE reader has a single-threaded usage contract. (SSE-13, SSE-14, SSE-15, SSE-16, SSE-17, SSE-18, SSE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21` · high · sha:0451cc7f3bb4</sub>
- SSE line length is either accepted unbounded or capped per a documented limit. (SSE-13, SSE-14, SSE-15, SSE-16, SSE-17, SSE-18, SSE-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:21` · high · sha:0451cc7f3bb4</sub>
- The SSE event object is immutable and defensively copies its data. (SSE-20, SSE-21, SSE-22)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:22` · high · sha:0451cc7f3bb4</sub>
- SSE event value equality is computed over five fields. (SSE-20, SSE-21, SSE-22)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:22` · high · sha:0451cc7f3bb4</sub>
- The SSE event's is-empty predicate treats a comment as non-empty content. (SSE-20, SSE-21, SSE-22)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:22` · high · sha:0451cc7f3bb4</sub>
- The SSE facade closes its owned resource exactly once regardless of whether termination is a clean end, an explicit close, a use-block exit, a partial consume, or a mid-stream failure. (SSE-23, SSE-24, SSE-25, SSE-26, SSE-27, SSE-28, SSE-29, SSE-30, SSE-31, SSE-32)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23` · high · sha:0451cc7f3bb4</sub>
- The SSE facade exposes a single-pass iterator. (SSE-23, SSE-24, SSE-25, SSE-26, SSE-27, SSE-28, SSE-29, SSE-30, SSE-31, SSE-32)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23` · high · sha:0451cc7f3bb4</sub>
- Iterating an SSE facade after close fails, while an already in-flight iteration ends cleanly. (SSE-23, SSE-24, SSE-25, SSE-26, SSE-27, SSE-28, SSE-29, SSE-30, SSE-31, SSE-32)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23` · high · sha:0451cc7f3bb4</sub>
- Closing the SSE facade is idempotent. (SSE-23, SSE-24, SSE-25, SSE-26, SSE-27, SSE-28, SSE-29, SSE-30, SSE-31, SSE-32)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23` · high · sha:0451cc7f3bb4</sub>
- A mid-stream SSE failure releases the resource before propagating the failure, suppressing any close error. (SSE-23, SSE-24, SSE-25, SSE-26, SSE-27, SSE-28, SSE-29, SSE-30, SSE-31, SSE-32)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23` · high · sha:0451cc7f3bb4</sub>
- An automatic terminal release failure on an SSE facade is swallowed, while an explicit close's failure propagates. (SSE-23, SSE-24, SSE-25, SSE-26, SSE-27, SSE-28, SSE-29, SSE-30, SSE-31, SSE-32)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23` · high · sha:0451cc7f3bb4</sub>
- The SSE facade can be closed from a thread other than the one reading it. (SSE-23, SSE-24, SSE-25, SSE-26, SSE-27, SSE-28, SSE-29, SSE-30, SSE-31, SSE-32)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23` · high · sha:0451cc7f3bb4</sub>
- The response-opening convenience method binds the SSE lifecycle to the response and rejects a response with no body. (SSE-23, SSE-24, SSE-25, SSE-26, SSE-27, SSE-28, SSE-29, SSE-30, SSE-31, SSE-32)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:23` · high · sha:0451cc7f3bb4</sub>
- The typed SSE adapter's mapper function receives the event name and the data lines joined by newline. (SSE-33, SSE-34, SSE-35, SSE-36)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:24` · high · sha:0451cc7f3bb4</sub>
- The typed SSE adapter honors Value, Skip, and Done outcomes returned from the mapper. (SSE-33, SSE-34, SSE-35, SSE-36)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:24` · high · sha:0451cc7f3bb4</sub>
- The typed SSE adapter decodes each element lazily, one at a time. (SSE-33, SSE-34, SSE-35, SSE-36)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:24` · high · sha:0451cc7f3bb4</sub>
- A mapper throw in the typed SSE adapter releases the underlying resource before the exception propagates. (SSE-33, SSE-34, SSE-35, SSE-36)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:24` · high · sha:0451cc7f3bb4</sub>
- The SSE core carries no built-in sentinel, error, or serialization convention. (SSE-37, SSE-38, SSE-39, SSE-40, SSE-41)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25` · high · sha:0451cc7f3bb4</sub>
- The SSE core performs no auto-reconnect and sends no Last-Event-ID header. (SSE-37, SSE-38, SSE-39, SSE-40, SSE-41)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25` · high · sha:0451cc7f3bb4</sub>
- The SSE core is pull-based, issuing one poll of the source per unit of downstream demand. (SSE-37, SSE-38, SSE-39, SSE-40, SSE-41)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25` · high · sha:0451cc7f3bb4</sub>
- Lazy single-pass convenience views over an SSE stream reuse a single underlying reader. (SSE-37, SSE-38, SSE-39, SSE-40, SSE-41)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25` · high · sha:0451cc7f3bb4</sub>
- The reactive SSE bridge documents its fatal-versus-non-fatal error split and its source-ownership behavior. (SSE-37, SSE-38, SSE-39, SSE-40, SSE-41)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:25` · high · sha:0451cc7f3bb4</sub>

## Conflicts

## Superseded
