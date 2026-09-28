# observability

## Rules
- When the requested log level is disabled, obtaining a log event and invoking its builder and terminal-emit methods must allocate nothing and produce no output, with enabled/disabled decided once at event-creation time via a shared inert event. (OBS-1)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:7-7` · high · sha:1b678eca176d</sub>
- The structured-logging facade must expose exactly four severity levels—ERROR, WARNING, INFO, and VERBOSE—mapped onto the backend's ERROR, WARN, INFO, and most-verbose/DEBUG levels. (OBS-2)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:8-8` · high · sha:1b678eca176d</sub>
- A log field key must be rejected when empty, and a null field value must not be dropped but must be emitted as the literal string "null". (OBS-3)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:9-9` · high · sha:1b678eca176d</sub>
- Calling event(name) sets an authoritative categorisation tag under the reserved key "event", clears the tag when given an empty name, and suppresses any "event" key from global context, folded diagnostic context, or a per-event field so the emitted event carries "event" exactly once, because JSON appenders would otherwise produce invalid duplicate-key output. (OBS-4)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:10-10` · high · sha:1b678eca176d</sub>
- When the same field key is contributed by more than one source, precedence must be per-event field over global context over folded diagnostic context, and a key must appear at most once in the emitted event. (OBS-5)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:11-11` · high · sha:1b678eca176d</sub>
- Field-value rendering must be total and never throw, rendering throwables as "SimpleClassName: message", arrays/collections/maps as a bracketed textual form, and numeric/boolean/char primitives type-preserving, substituting a diagnostic placeholder if a value's own string conversion throws. (OBS-6)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:12-12` · high · sha:1b678eca176d</sub>
- A rendered log field value should be truncated to a bounded maximum (reference default 8 KiB) with a truncation marker, exempting primitive values. (OBS-7)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:13-13` · high · sha:1b678eca176d</sub>
- A single log event must be emitted at most once, with a second terminal emit becoming a no-op under a guard that is correct under concurrent invocation, though field/tag/cause accumulation need not itself be thread-safe. (OBS-8)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:14-14` · high · sha:1b678eca176d</sub>
- A global key/value context configured on the logger must attach to every event subject to field-precedence rules, and for hot-path efficiency the implementation should reference the caller-supplied context rather than deep-copying it per event. (OBS-9, OBS-5)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:15-15` · high · sha:1b678eca176d</sub>
- A once-per-logger diagnostic should warn, throttled to at most one emission per logger and gated on the verbose level being enabled, when a caller sets a per-event field colliding with the reserved "event" key, while ambient "event" keys from global or diagnostic context defer silently without being warned about. (OBS-40)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:16-16` · high · sha:1b678eca176d</sub>
- When folding thread-local diagnostic context into a log event, only allow-listed keys are folded, the default allow-list is exactly {trace.id, span.id}, a null allow-list opts into folding every present key, and keys with null values are skipped, to prevent arbitrary application context from leaking into SDK-owned events. (OBS-10)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:20-20` · high · sha:1b678eca176d</sub>
- Every log-emission site (request/response/failure events and the body-drain feeding them) must catch any exception and re-surface it as a best-effort http.instrumentation.* diagnostic, swallowing any secondary failure while emitting that diagnostic, so instrumentation logging never fails the request. (OBS-20, OBS-30)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:36-36` · high · sha:1b678eca176d</sub>
- A Span must expose a recording flag, all mutators must be inert and end() a no-op when non-recording, and end() in both its success and error variants must be idempotent. (OBS-21)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:40-40` · high · sha:1b678eca176d</sub>
- Activating a span as current must return a closeable scope handle that restores the previously-active span on close, usable from a try/using construct, and must restore even when the guarded code throws. (OBS-22)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:41-41` · high · sha:1b678eca176d</sub>
- Activating a span for log correlation must push the trace id and span id onto the thread-local diagnostic context under keys trace.id and span.id for the scope's lifetime, restoring each to its prior value or removing it on close, while a non-recording span skips the push and delegates to plain current-span activation. (OBS-23)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:42-42` · high · sha:1b678eca176d</sub>
- Thread-local diagnostic context must be bridgeable across async thread boundaries via an immutable snapshot, captured on the originating thread, reinstalled on the executing thread for a block's duration, and restored afterward including on exception. (OBS-24)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:43-43` · high · sha:1b678eca176d</sub>
- Tracing abstractions must provide allocation-free no-op defaults for when tracing is disabled—a no-op tracer returning a shared no-op span, a no-op span with a cached current-scope, a no-op instrumentation context with all-invalid sentinels, and a no-op HTTP-tracer/factory—such that selecting a no-op path never allocates per call. (OBS-25)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:44-44` · high · sha:1b678eca176d</sub>
- An instrumentation/trace context exposes W3C-compliant identifiers—a trace id, a 16-lowercase-hex-character span id, trace flags as a two-hex-character byte, and a trace-state list—plus validity and remoteness flags, with reserved invalid sentinels of a 32-hex-zero trace id, 16-hex-zero span id, "00" trace flags, and empty trace-state; an all-zero trace or span id is treated as invalid. (OBS-26)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:48-48` · high · sha:1b678eca176d</sub>
- Trace-id generation must support at least a W3C flavour (128-bit as 32 lowercase hex), a Datadog flavour (64-bit unsigned decimal), and a no-op flavour yielding the invalid sentinel, and must never produce the reserved all-zero id, coercing a zero draw to non-zero. (OBS-27)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:49-49` · high · sha:1b678eca176d</sub>
- The SDK should provide an HTTP-shaped tracer vocabulary richer than start/end, covering operation started/succeeded/failed, per-attempt started/failed-with-next-delay/retries-exhausted, and transport milestones (URL resolved, connection acquired, request sent with byte count, response headers received, response received with byte count), with every method defaulting to a no-op so adding an event is non-breaking. (OBS-28)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:53-53` · high · sha:1b678eca176d</sub>
- HTTP-tracer lifecycle ordering requires operationStarted exactly once at the start, operationSucceeded and operationFailed to be mutually exclusive and each fire at most once at the end, attempt events to fire multiple times, and retries-exhausted, when it fires, to be immediately followed by operationFailed carrying the same throwable, with one tracer instance corresponding 1:1 to a single logical operation. (OBS-29)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:54-54` · high · sha:1b678eca176d</sub>
- Tracer and HTTP-tracer callbacks, as well as metrics instruments, must be safe to invoke concurrently and from transport threads and must never throw, since the runtime does not defensively catch these callbacks and a violation breaks the caller's request. (OBS-30, OBS-20)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:55-55` · high · sha:1b678eca176d</sub>
- The metrics SPI exposes a Meter that manufactures at least a monotonic integer counter and a floating-point histogram, each accepting per-measurement key/value attributes, with a default no-op Meter that discards every measurement, returns shared instrument singletons, and keeps the core free of any metrics runtime dependency. (OBS-31)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:59-59` · high · sha:1b678eca176d</sub>
- Metric names, descriptions, and units should follow OpenTelemetry semantic conventions and UCUM unit symbols, with the default HTTP instrumentation emitting a request counter http.client.request.count (unit {request}) and a latency histogram http.client.request.duration (unit ms), tagged with method and either status code on success or error type on failure. (OBS-32)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:60-60` · high · sha:1b678eca176d</sub>
- A monotonic counter documents that only non-negative increments are valid with negative deltas undefined and the caller's responsibility, and the core instrument must not validate on the hot path, while a histogram must tolerate any input including non-finite values without throwing, delegating handling of non-finite values to concrete adapters. (OBS-33)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:61-61` · high · sha:1b678eca176d</sub>
- HTTP logging granularity is selectable across at least none, headers-only, and headers-plus-body, defaulting to none; at none, request/response log events are not emitted, while span lifecycle and metric recording still run on every request independent of the log level. (OBS-34)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:65-65` · high · sha:1b678eca176d</sub>
- A log-level value should be resolvable from layered configuration (explicit override, then environment variable, then normalized system property, then default) with tolerant, case-insensitive, whitespace-trimmed parsing, falling back to a caller-supplied default that itself defaults to "none", and the SDK must not bake in a default config key name. (OBS-35)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:66-66` · high · sha:1b678eca176d</sub>
- Under body logging, body capture is bounded to a configurable preview size (reference default 8 KiB) and must not buffer the whole body, so a body larger than the cap still streams in full to the caller by replaying the captured prefix then continuing from the live tail while only the preview occupies memory. (OBS-36)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:67-67` · high · sha:1b678eca176d</sub>
- For unknown-length (streaming/chunked) response bodies, the async logging path should skip body capture entirely and stream the body unwrapped so a slow producer cannot block the completion thread. (OBS-37)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:68-68` · high · sha:1b678eca176d</sub>
- A captured body preview should be charset-aware for text (decoding with the media type's charset, falling back to UTF-8) and binary-safe for non-text content via a size-only marker such as [binary N bytes captured]; decoding must never throw, and empty input yields an empty preview. (OBS-38)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:69-69` · high · sha:1b678eca176d</sub>
- Emitted structured event names and field keys must be stable, at minimum including http.request and http.response events carrying http.request.method, redacted url.full, http.response.status_code, http.response.duration_ms, and content-length/header fields, with a failure additionally carrying error.type and the throwable cause, and url.full always being the redacted form. (OBS-39)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:70-70` · high · sha:1b678eca176d</sub>
- Observability code paths (redaction, event emission, span/metric recording) MUST NEVER throw into the caller's request path; a failure to redact/render/emit MUST degrade gracefully via a safe placeholder or a self-describing instrumentation-error event and let the request proceed. (XCUT-20)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:48` · high · sha:d6123be82c9e</sub>

## Constraints
- Tracer callbacks (span start, scope activation, end) and metrics callbacks (counter/histogram) are not defensively wrapped by the runtime, so a throwing tracer or meter will propagate and can fail the request. (OBS-20, OBS-30)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:36-36` · high · sha:1b678eca176d</sub>

## Conclusions
- The observability design deliberately treats log-emission failures as always caught and swallowed while leaving tracer and metrics calls unwrapped, relying on the SPI contract that those callbacks never throw. (OBS-30)
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:3-3` · medium · sha:1b678eca176d</sub>

## Reference
- The diagnostic-context (MDC) allow-list is the set of thread-local logging-context keys folded into an SDK log event, defaulting to {trace.id, span.id}, preventing arbitrary application context from leaking into SDK-owned events.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:25` · high · sha:f0b3d2058626</sub>
- W3C Trace Context is the interoperable trace-correlation format the instrumentation context complies with, consisting of trace id, span id, trace flags, and trace state, with reserved all-zero invalid sentinels.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:71` · high · sha:f0b3d2058626</sub>
- A disabled log level allocates nothing and emits nothing, returning a shared inert event object. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- Four log levels are mapped to the logging backend. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- An empty log field key is rejected, and a null field value renders as the literal string "null". (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- The reserved `event` log tag is emitted exactly once per event, and setting it to empty clears it. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- Log field precedence is field-level over global over diagnostic-context, with only one occurrence emitted per key. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- Log event rendering is total, substituting a placeholder when a value's string conversion throws. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- Log field values are truncated with a bound, except primitive values which are exempt from truncation. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- The log event's single-emit guard behaves correctly under concurrent races. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- Global logging context is attached to every event. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- A reserved-key collision in log fields is warned about exactly once per logger. (OBS-1, OBS-2, OBS-3, OBS-4, OBS-5, OBS-6, OBS-7, OBS-8, OBS-9, OBS-40)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39` · high · sha:0451cc7f3bb4</sub>
- The diagnostic-context (MDC) allow-list defaults to {trace.id, span.id}, a null allow-list folds all context keys in, and null context values are skipped. (OBS-10)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:40` · high · sha:0451cc7f3bb4</sub>
- URL userinfo is always redacted in logs, regardless of allow-listing. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- URL query values are redacted unless the parameter name is allow-listed, with multi-value parameters redacted atomically. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- A URL fragment's key=value tokens are scrubbed, while a plain fragment is preserved. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- URL redaction preserves the scheme, host, port, and path, and does not introduce a spurious `?` from a fragment containing one. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- A malformed URL redacts to the literal `[malformed url]` and never throws. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- A URL embedded in a header value is redacted, falling back to keeping the path with `?***` for the query. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- The Location and Content-Location headers are redacted through the shared redaction policy. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- The header-name logging allow-list is default-deny. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- A policy governs the verbosity of logging for headers dropped from output. (OBS-11, OBS-12, OBS-13, OBS-14, OBS-15, OBS-16, OBS-17, OBS-18, OBS-19)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41` · high · sha:0451cc7f3bb4</sub>
- A logging failure is caught and re-emitted as an `http.instrumentation.*` event, whereas a tracer or meter throw propagates. (OBS-20)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:42` · high · sha:0451cc7f3bb4</sub>
- A span exposes a recording flag and its end operation is idempotent. (OBS-21, OBS-22, OBS-23, OBS-24, OBS-25)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43` · high · sha:0451cc7f3bb4</sub>
- A tracing scope restores the prior span on close, including when closed via a throw. (OBS-21, OBS-22, OBS-23, OBS-24, OBS-25)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43` · high · sha:0451cc7f3bb4</sub>
- The log-correlation scope pushes and restores trace.id/span.id, and is skipped for a non-recording span. (OBS-21, OBS-22, OBS-23, OBS-24, OBS-25)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43` · high · sha:0451cc7f3bb4</sub>
- The async MDC bridge saves, installs, and restores logging context, including across a throw. (OBS-21, OBS-22, OBS-23, OBS-24, OBS-25)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43` · high · sha:0451cc7f3bb4</sub>
- The default no-op tracing implementation is allocation-free. (OBS-21, OBS-22, OBS-23, OBS-24, OBS-25)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43` · high · sha:0451cc7f3bb4</sub>
- Trace/span identifiers follow the W3C format with reserved all-zero sentinels marking invalid ids. (OBS-26, OBS-27, OBS-28, OBS-29, OBS-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44` · high · sha:0451cc7f3bb4</sub>
- The W3C, Datadog, and no-op id generators never produce an all-zero id. (OBS-26, OBS-27, OBS-28, OBS-29, OBS-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44` · high · sha:0451cc7f3bb4</sub>
- An HTTP-tracer vocabulary is defined with no-op defaults. (OBS-26, OBS-27, OBS-28, OBS-29, OBS-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44` · high · sha:0451cc7f3bb4</sub>
- Tracer lifecycle events are ordered correctly, including pairing a retries-exhausted event with a failed event. (OBS-26, OBS-27, OBS-28, OBS-29, OBS-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44` · high · sha:0451cc7f3bb4</sub>
- Tracer and meter callbacks are concurrency-safe and never throw. (OBS-26, OBS-27, OBS-28, OBS-29, OBS-30)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44` · high · sha:0451cc7f3bb4</sub>
- The meter exposes a monotonic counter and a histogram with attributes, its no-op default discards all recordings, and no metrics runtime is pulled in as a dependency. (OBS-31, OBS-32, OBS-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:45` · high · sha:0451cc7f3bb4</sub>
- Metric names and units follow OpenTelemetry conventions. (OBS-31, OBS-32, OBS-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:45` · high · sha:0451cc7f3bb4</sub>
- The counter accepts only non-negative increments, while the histogram tolerates any input value. (OBS-31, OBS-32, OBS-33)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:45` · high · sha:0451cc7f3bb4</sub>
- The log level (none/headers/body) is independent of whether spans and metrics are running. (OBS-34, OBS-35, OBS-36, OBS-37, OBS-38, OBS-39)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46` · high · sha:0451cc7f3bb4</sub>
- Log level resolution is layered and tolerant, with no hard-coded configuration key. (OBS-34, OBS-35, OBS-36, OBS-37, OBS-38, OBS-39)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46` · high · sha:0451cc7f3bb4</sub>
- A bounded body preview for logging still streams the full body through to the caller. (OBS-34, OBS-35, OBS-36, OBS-37, OBS-38, OBS-39)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46` · high · sha:0451cc7f3bb4</sub>
- The async instrumentation path skips body capture for bodies of unknown length. (OBS-34, OBS-35, OBS-36, OBS-37, OBS-38, OBS-39)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46` · high · sha:0451cc7f3bb4</sub>
- The body preview is charset-aware, binary-safe, and never throws. (OBS-34, OBS-35, OBS-36, OBS-37, OBS-38, OBS-39)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46` · high · sha:0451cc7f3bb4</sub>
- Instrumentation event names and keys are stable, with `url.full` redacted. (OBS-34, OBS-35, OBS-36, OBS-37, OBS-38, OBS-39)
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46` · high · sha:0451cc7f3bb4</sub>

## Conflicts

## Superseded
