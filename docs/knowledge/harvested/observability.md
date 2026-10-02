# observability

## Rules
- A port MUST either honour the contract that tracer and metrics callbacks never throw or add its own guards.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:3-3` · high · sha:1b678eca176d</sub>
- OBS-1 (MUST) When the requested level is disabled, obtaining a log event and calling its builder methods and terminal emit MUST allocate nothing and produce no output, and the facade MUST decide enabled/disabled once at event-creation time and return a shared inert event for the disabled case.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:7-7` · high · sha:1b678eca176d</sub>
- OBS-2 (MUST) The logging facade MUST expose exactly four severity levels, ERROR, WARNING, INFO and VERBOSE, mapped onto the backend's ERROR, WARN, INFO and most-verbose/DEBUG levels.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:8-8` · high · sha:1b678eca176d</sub>
- OBS-3 (MUST) A log field key MUST be rejected when empty, and a null field value MUST NOT be dropped but MUST be emitted as the literal string `null`.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:9-9` · high · sha:1b678eca176d</sub>
- OBS-4 (MUST) `event(name)` MUST set an authoritative categorisation tag under the reserved key `event`, an empty name MUST clear the tag, and when a non-empty tag is set any `event` key from the global context, folded diagnostic context or a per-event field MUST be suppressed so the emitted event carries `event` exactly once (otherwise JSON appenders produce invalid duplicate-key output).
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:10-10` · high · sha:1b678eca176d</sub>
- OBS-5 (MUST) When the same field key is contributed by more than one source, precedence MUST be per-event field over global context over folded diagnostic context, and a key MUST appear at most once.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:11-11` · high · sha:1b678eca176d</sub>
- OBS-6 (MUST) Field-value rendering MUST be total and never throw, with throwables rendered as `SimpleClassName: message`, arrays/collections/maps as a bracketed textual form, numeric/boolean/char primitives passed through type-preserving, and a diagnostic placeholder substituted if a value's own string conversion throws.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:12-12` · high · sha:1b678eca176d</sub>
- OBS-7 (SHOULD) A rendered field value SHOULD be truncated to a bounded maximum (reference 8 KiB) with a truncation marker, and primitives are exempt from truncation.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:13-13` · high · sha:1b678eca176d</sub>
- OBS-8 (MUST) A single log event MUST be emitted at most once, a second terminal emit MUST be a no-op that is correct under concurrent invocation, and while field/tag/cause accumulation need not be thread-safe, terminal emit MUST be safe from any thread.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:14-14` · high · sha:1b678eca176d</sub>
- OBS-9 (MUST) A global key/value context configured on the logger MUST attach to every event subject to OBS-5 precedence, and implementations SHOULD reference the caller-supplied context rather than deep-copying it per event (so the context is expected to be effectively immutable, though a copying port is still conformant).
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:15-15` · high · sha:1b678eca176d</sub>
- OBS-40 (SHOULD) A once-per-logger diagnostic SHOULD warn when a caller sets a per-event field colliding with the reserved `event` key, throttled to at most one emission per logger and gated on the verbose level being enabled, while ambient `event` keys from global/diagnostic context defer silently and MUST NOT be warned about.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:16-16` · high · sha:1b678eca176d</sub>
- OBS-10 (MUST) When folding thread-local diagnostic context into an event only allow-listed keys MUST be folded, the default allow-list MUST be exactly `{trace.id, span.id}`, a null (absent) allow-list MUST fold every present key (opt-in unfiltered mode), and keys with null values MUST be skipped.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:20-20` · high · sha:1b678eca176d</sub>
- OBS-21 (MUST) A Span MUST expose a recording flag, when non-recording all mutators MUST be inert and `end()` a no-op, and `end()` (success and error variants) MUST be idempotent.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:40-40` · high · sha:1b678eca176d</sub>
- OBS-22 (MUST) Activating a span as current MUST return a scope handle that restores the previously-active span when closed, is closeable from a try/using construct, and restores even when the guarded code throws.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:41-41` · high · sha:1b678eca176d</sub>
- OBS-23 (MUST) Activating a span for log correlation MUST push the trace id and span id onto the thread-local diagnostic context (keys `trace.id`, `span.id`) for the scope's lifetime and restore each to its prior value (or remove it) on close, while for a non-recording span the push MUST be skipped and activation delegates to plain current-span activation.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:42-42` · high · sha:1b678eca176d</sub>
- OBS-24 (MUST) Thread-local diagnostic context MUST be bridgeable across async thread boundaries via an immutable, shareable snapshot that is captured on the originating thread, reinstalled on the executing thread for a block's duration, and the prior context restored afterward including on exception.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:43-43` · high · sha:1b678eca176d</sub>
- OBS-25 (MUST) Tracing abstractions MUST provide allocation-free no-op defaults used when tracing is disabled (a no-op tracer returning a shared no-op span, a no-op span whose current-scope is cached, a no-op instrumentation context with all-invalid sentinels, and a no-op HTTP-tracer/factory), and selecting a no-op path MUST NOT allocate per call.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:44-44` · high · sha:1b678eca176d</sub>
- OBS-26 (MUST) An instrumentation/trace context MUST expose W3C-compliant identifiers (a trace id, a span id of 16 lowercase hex chars, trace flags as a two-hex-char byte, and a trace-state list) plus validity and remoteness flags.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:48-48` · high · sha:1b678eca176d</sub>
- OBS-27 (MUST) Trace-id generation MUST support at least a W3C flavour (128-bit as 32 lowercase hex), a Datadog flavour (64-bit unsigned decimal) and a no-op flavour yielding the invalid sentinel, and generation MUST NOT produce the reserved all-zero id, so a zero draw MUST be coerced non-zero.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:49-49` · high · sha:1b678eca176d</sub>
- OBS-28 (SHOULD) The SDK SHOULD provide an HTTP-shaped tracer vocabulary richer than start/end covering operation started/succeeded/failed, per-attempt started/failed (with next-delay)/retries-exhausted, and transport milestones (request URL resolved, connection acquired, request sent with byte count, response headers received, response received with byte count), with every method defaulting to a no-op so adding an event is non-breaking.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:53-53` · high · sha:1b678eca176d</sub>
- OBS-29 (MUST) HTTP-tracer lifecycle ordering MUST hold: operationStarted fires once at the start, operationSucceeded and operationFailed are mutually exclusive and each fire once at the end, attempt events may fire multiple times, and retries-exhausted when it fires is immediately followed by operationFailed with the same throwable.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:54-54` · high · sha:1b678eca176d</sub>
- OBS-30 (MUST) Tracer and HTTP-tracer callbacks MUST be safe to invoke concurrently and from transport threads, implementations MUST NOT throw from any callback, and metrics instruments MUST likewise be safe to call concurrently and MUST NOT throw.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:55-55` · high · sha:1b678eca176d</sub>
- OBS-31 (MUST) The metrics SPI MUST expose a Meter manufacturing at least a monotonic integer counter and a floating-point histogram each accepting per-measurement key/value attributes, the default Meter MUST be a no-op that discards every measurement and returns shared instrument singletons, and the core MUST NOT pull a metrics runtime into its dependencies.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:59-59` · high · sha:1b678eca176d</sub>
- OBS-32 (SHOULD) Metric names, descriptions and units SHOULD follow OpenTelemetry semantic conventions and UCUM unit symbols.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:60-60` · high · sha:1b678eca176d</sub>
- OBS-33 (MUST) A monotonic counter MUST document that only non-negative increments are valid (negative deltas are undefined and the caller's responsibility), the core instrument MUST NOT validate on the hot path, and a histogram MUST tolerate any input without throwing with handling of non-finite values delegated to concrete adapters.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:61-61` · high · sha:1b678eca176d</sub>
- OBS-34 (MUST) HTTP logging granularity MUST be selectable across at least none, headers-only and headers-plus-body, defaulting to none, where at none request/response log events MUST NOT be emitted and body capture occurs only at the body level.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:65-65` · high · sha:1b678eca176d</sub>
- OBS-35 (SHOULD) A log-level value SHOULD be resolvable from layered configuration (explicit override, then environment variable, then normalized system property, then default) with tolerant case-insensitive whitespace-trimmed parsing, falling back to a caller-supplied default that itself defaults to none.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:66-66` · high · sha:1b678eca176d</sub>
- OBS-35 (MUST) The SDK MUST NOT bake in a default config key name for resolving the log level.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:66-66` · high · sha:1b678eca176d</sub>
- OBS-36 (MUST) Under body logging, body capture MUST be bounded to a configurable preview size (reference default 8 KiB) and MUST NOT buffer the whole body, so a body larger than the cap MUST still stream in full to the caller (replaying the captured prefix then continuing from the live tail) while only the preview occupies memory.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:67-67` · high · sha:1b678eca176d</sub>
- OBS-37 (SHOULD) For unknown-length (streaming/chunked) response bodies the async logging path SHOULD skip body capture entirely and stream the body unwrapped, so a slow producer cannot block the completion thread.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:68-68` · high · sha:1b678eca176d</sub>
- OBS-38 (SHOULD) A captured body preview SHOULD be charset-aware for text (using the media type's charset, falling back to UTF-8) and binary-safe for non-text (a size-only marker such as `[binary N bytes captured]`).
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:69-69` · high · sha:1b678eca176d</sub>
- OBS-38 (MUST) Body-preview decoding MUST NOT throw, malformed or truncated input yields replacement characters, and empty input yields an empty preview.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:69-69` · high · sha:1b678eca176d</sub>
- OBS-39 (MUST) Emitted structured event names and field keys MUST be stable, with at minimum events `http.request` and `http.response` carrying `http.request.method`, `url.full` (redacted), `http.response.status_code`, `http.response.duration_ms` and content-length/header fields.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:70-70` · high · sha:1b678eca176d</sub>
- A failed request emits an `http.response` event with `error.type` and the throwable cause, and the logged `url.full` MUST always be the redacted URL.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:70-70` · high · sha:1b678eca176d</sub>
- XCUT-24 (SHOULD) Diagnostic/preview reads of caller- or server-controlled payloads (error-body snapshots, request/response body log previews) MUST be byte-capped, so a preview MUST NOT materialize an unbounded payload into memory.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:53-53` · high · sha:d6123be82c9e</sub>
- XCUT-24 (SHOULD) Diagnostic/preview reads SHOULD be non-consuming, so a preview does not disturb the primary read path the consumer will use.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:53-53` · high · sha:d6123be82c9e</sub>
- OBS-1: a disabled level allocates nothing and emits nothing, returning a shared inert event.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-2: the four levels map to the logging backend.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-3: an empty key is rejected and a null value renders as the literal `null`.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-4: the reserved `event` tag is emitted exactly once and an empty value clears it.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-5: field values take precedence over global values over diagnostic-context values, with one occurrence per key.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-6: rendering is total, using a placeholder when a string conversion throws.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-7: values are truncated to a bound with primitives exempt.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-8: the single-emit guard is correct under races.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-9: global context appears on every event.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-40: a reserved-key collision is warned once per logger.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:39-39` · high · sha:0451cc7f3bb4</sub>
- OBS-10: the diagnostic-context allow-list defaults to `{trace.id, span.id}`, null folds all and null values are skipped.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:40-40` · high · sha:0451cc7f3bb4</sub>
- OBS-11: URL userinfo is always redacted.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-12: URL query values are redacted unless allow-listed, atomically for multi-value parameters.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-13: a fragment key=value is scrubbed while a plain fragment is preserved.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-14: scheme, host, port and path are preserved and a `?` inside a fragment does not produce a spurious `?`.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-15: a malformed URL renders as `[malformed url]` and redaction never throws.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-16: URLs inside header values are redacted with a path-kept `?***` fallback.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-17: Location and Content-Location are redacted via the shared policy.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-18: the header-name allow-list is default-deny.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-19: a dropped-header verbosity policy exists.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:41-41` · high · sha:0451cc7f3bb4</sub>
- OBS-20: logging failures are caught and re-emitted as `http.instrumentation.*` events, while tracer and meter throws propagate.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:42-42` · high · sha:0451cc7f3bb4</sub>
- OBS-21: spans have a recording flag and ending is idempotent.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43-43` · high · sha:0451cc7f3bb4</sub>
- OBS-22: a span scope restores the prior span on close, including on throw.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43-43` · high · sha:0451cc7f3bb4</sub>
- OBS-23: the log-correlation scope pushes and restores trace.id and span.id and is skipped for non-recording spans.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43-43` · high · sha:0451cc7f3bb4</sub>
- OBS-24: the async MDC bridge saves, installs and restores context, including on throw.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43-43` · high · sha:0451cc7f3bb4</sub>
- OBS-25: no-op tracing defaults are allocation-free.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:43-43` · high · sha:0451cc7f3bb4</sub>
- OBS-26: W3C identifiers are used with all-zero invalid sentinels.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44-44` · high · sha:0451cc7f3bb4</sub>
- OBS-27: W3C, Datadog and no-op id generation never produce all-zero ids.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44-44` · high · sha:0451cc7f3bb4</sub>
- OBS-28: an HTTP-tracer vocabulary exists with default no-ops.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44-44` · high · sha:0451cc7f3bb4</sub>
- OBS-29: lifecycle ordering holds, including exhausted-to-failed pairing.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44-44` · high · sha:0451cc7f3bb4</sub>
- OBS-30: tracer and meter callbacks are concurrent-safe and never throw.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:44-44` · high · sha:0451cc7f3bb4</sub>
- OBS-31: the meter yields a monotonic counter and a histogram with attributes, the no-op default discards, and no metrics runtime is pulled in.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:45-45` · high · sha:0451cc7f3bb4</sub>
- OBS-32: metric names and units follow OpenTelemetry conventions.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:45-45` · high · sha:0451cc7f3bb4</sub>
- OBS-33: the counter accepts non-negative values only while the histogram tolerates any input.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:45-45` · high · sha:0451cc7f3bb4</sub>
- OBS-34: log levels none, headers and body exist, with span and metrics running independent of level.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46-46` · high · sha:0451cc7f3bb4</sub>
- OBS-35: level resolution is layered and tolerant with no baked-in key.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46-46` · high · sha:0451cc7f3bb4</sub>
- OBS-36: a bounded body preview streams the full body to the caller.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46-46` · high · sha:0451cc7f3bb4</sub>
- OBS-37: async skips capture for unknown-length bodies.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46-46` · high · sha:0451cc7f3bb4</sub>
- OBS-38: body preview is charset-aware, binary-safe and non-throwing.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46-46` · high · sha:0451cc7f3bb4</sub>
- OBS-39: event names and keys are stable with a redacted url.full.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:46-46` · high · sha:0451cc7f3bb4</sub>
- The request-logging wrapper wraps the destination Stream handed to the delegate's single write in a write-only TeeStream that mirrors each chunk into a capped tap before forwarding it, so the wire receives every byte and the upstream is consumed exactly once (BODY-17, IO-25).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:206-208` · high · sha:da6000c93fc5</sub>
- The request-logging tap is cleared at the start of every write so a snapshot reflects only the most recent attempt (BODY-18, IO-27).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:208-209` · high · sha:da6000c93fc5</sub>
- The request-logging tap stops copying at the cap while forwarding continues, so a multi-gigabyte upload mirrors only a preview (BODY-19, IO-26).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:209-210` · high · sha:da6000c93fc5</sub>
- Mirroring precedes forwarding in the tee, so a primary-side failure still leaves the failing chunk captured (BODY-20).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:210-211` · high · sha:da6000c93fc5</sub>
- TeeStream Flush and Dispose go to the primary only (IO-29).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:212-212` · high · sha:da6000c93fc5</sub>
- The tap is never a MemoryStream handed to anyone and snapshots are copies, so no caller can write around the primary (IO-28, BODY-37).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:212-216` · high · sha:da6000c93fc5</sub>
- The request-logging wrapper reports the delegate's replayability verbatim, and its ToReplayableAsync wraps the delegate's replayable form with the cap preserved (BODY-21).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:216-217` · high · sha:da6000c93fc5</sub>
- The response-logging wrapper drains the delegate at most once, lazily, on first access (a read, a snapshot, or the cached-error query), buffering up to the cap, with concurrent first accesses serialised so the upstream is read exactly once (BODY-22).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:218-220` · high · sha:da6000c93fc5</sub>
- The response-logging drain runs under the wrapper's own lifetime token, not the first caller's, so one caller cancelling cannot poison the shared drain for the others.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:222-223` · high · sha:da6000c93fc5</sub>
- When the response fits within the cap, the response-logging wrapper captures everything, disposes the delegate, and serves every later read as a fresh read-only view, fully repeatable (BODY-23).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:223-225` · high · sha:da6000c93fc5</sub>
- When the response exceeds the cap, the response-logging wrapper buffers only the prefix, leaves the delegate open, and serves the next read as a single-use concatenating stream that replays the prefix then continues from the live tail, with a second read failing (BODY-24).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:225-227` · high · sha:da6000c93fc5</sub>
- A mid-drain failure retains the bytes already read and caches the exception, so reads rethrow it via ExceptionDispatchInfo preserving the original stack, a snapshot returns the partial bytes without throwing, and the error query never triggers a drain (BODY-26).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:227-229` · high · sha:da6000c93fc5</sub>
- Every close path of the response-logging wrapper and its tail stream routes through one Interlocked close-once guard, and a delegate whose Dispose throws is still marked closed (BODY-27).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:230-231` · high · sha:da6000c93fc5</sub>
- On the fits-cap path a close failure after a complete capture is reported through the quiet-dispose helper of design section 3.7, never as a drain error, and the captured array outlives the wrapper (BODY-28).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:231-233` · high · sha:da6000c93fc5</sub>
- The response-logging wrapper's reported length is the captured size only when capture was complete (BODY-29).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:233-234` · high · sha:da6000c93fc5</sub>
- Both logging wrappers engage only when body-level logging is enabled and share one preview-size setting (BODY-34).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:236-236` · high · sha:da6000c93fc5</sub>
- Under OBS-2 the SDK emits exactly four log levels (Error, Warning, Information, Debug) out of ILogger's six, with VERBOSE mapped to Debug.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:48-49` · high · sha:ddf8f695ff61</sub>
- To meet OBS-20 ("every log-emission site MUST catch any exception"), each log emission is wrapped in a catch that excludes OperationCanceledException, attempts one http.instrumentation.error event, and swallows a second failure; the as-built code lacks this guard.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:63-65` · high · sha:ddf8f695ff61</sub>
- Under OBS-7, rendered strings such as the URL are truncated at 8 KiB with a marker before they reach a log template.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:66-66` · high · sha:ddf8f695ff61</sub>
- OBS-39 fixes the events http.request and http.response carrying http.request.method, url.full, http.response.status_code, http.response.duration_ms and content-length/header fields, and these stable names are produced with LoggerMessage.Define delegates rather than the generator.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:73-80` · high · sha:ddf8f695ff61</sub>
- OBS-2's mapping puts request/response log events at Debug and failures at Warning, as built.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:84-84` · high · sha:ddf8f695ff61</sub>
- Under OBS-29's ordering there is one operation start, exactly one operation end whose status is Ok or Error, repeating attempt spans, and retries-exhausted recorded as an ActivityEvent on the operation span immediately before it ends in Error with the same exception.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:126-129` · high · sha:ddf8f695ff61</sub>
- One operation Activity corresponds to exactly one logical operation, and an ActivityListener ordering test asserts it.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:129-130` · high · sha:ddf8f695ff61</sub>
- The body preview wraps the body in a tee stream that copies the first 8 KiB into a pooled buffer while the caller reads everything (OBS-36, XCUT-24).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:190-192` · high · sha:ddf8f695ff61</sub>
- The body preview is skipped for unknown-length bodies on the async path (OBS-37), and decodes with the media type's charset or writes "[binary N bytes captured]" (OBS-38).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:192-193` · high · sha:ddf8f695ff61</sub>

## Constraints
- The never-break-the-caller guarantee is asymmetric because log-emission failures are caught and swallowed whereas tracing and metrics calls are not defensively wrapped and rely on the SPI contract that callbacks never throw (OBS-30).
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:3-3` · high · sha:1b678eca176d</sub>
- One HTTP-tracer instance corresponds 1:1 to a single logical operation.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:54-54` · high · sha:1b678eca176d</sub>
- Because the runtime does not defensively catch tracer and metrics callbacks (OBS-20), a callback that throws breaks the caller's request.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:55-55` · high · sha:1b678eca176d</sub>
- Span lifecycle and metric recording run on every request independent of the log level, so a log level of none silences log events without disabling tracing or metrics.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:65-65` · high · sha:1b678eca176d</sub>
- new MemoryStream().TryGetBuffer(out _) returns true and exposes the backing array, while a stream built with publiclyVisible: false makes TryGetBuffer false and GetBuffer throw UnauthorizedAccessException.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:212-215` · high · sha:da6000c93fc5</sub>
- ActivitySource, Activity and Meter live in System.Diagnostics.DiagnosticSource, which ships in the shared framework (verified in Microsoft.NETCore.App 10.0.12 and the 8.0.31 reference pack), so by principle P2 core owes no SDK-defined listener seam for tracing or metrics.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:20-24` · high · sha:ddf8f695ff61</sub>
- The [LoggerMessage] generator cannot express OBS-39's dotted keys because placeholders must bind to C# parameter names, so a placeholder such as {http.request.method} is rejected with SYSLIB1014 "Template ... is not provided as argument".
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:73-78` · high · sha:ddf8f695ff61</sub>
- OBS-27 is not met because Activity generates W3C identifiers only; Activity.TraceIdGenerator is a settable hook that can supply ids, but a Datadog flavour (64-bit, decimal) is a propagation format the runtime does not model and Datadog's own .NET tracer handles it; recorded as section 10 entry 24.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:95-98` · high · sha:ddf8f695ff61</sub>
- The runtime's propagator does not overwrite a traceparent header already present, so the policy's unconditional traceparent stamping puts the SDK span's id on the wire and makes the runtime's child span invisible to the server, whereas with no pre-set header the wire carries the runtime child's id, which is correct.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:143-147` · high · sha:ddf8f695ff61</sub>
- A consumer enabling both the SDK's and System.Net.Http's meters sees each attempt measured twice under one instrument name, which collides in an exporter that flattens meter names (Prometheus).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:148-150` · high · sha:ddf8f695ff61</sub>

## Conclusions
- Observability is designed to be always safe (never leaks secrets), always cheap when disabled (no hot-path allocation), and never breaking the caller.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:3-3` · high · sha:1b678eca176d</sub>
- The diagnostic-context allow-list exists to prevent arbitrary application context from leaking into SDK-owned events.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:20-20` · high · sha:1b678eca176d</sub>
- The response-logging drain latch is a SemaphoreSlim(1, 1) awaited with WaitAsync, because C# forbids await inside lock (CS1996) and a SemaphoreSlim wait parks no thread, which is the .NET reading of "does not pin the carrier thread".
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:220-222` · high · sha:da6000c93fc5</sub>
- Presence-gated activation survives for instrumentation only, via the platform: core always emits to its ActivitySource and Meter, which cost nothing with no listener, and an OpenTelemetry SDK or any ActivityListener activates them by subscribing by name.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:729-732` · high · sha:da6000c93fc5</sub>
- Four of the five reference facades (tracing, metrics, time, and the others with runtime convergence points) retire under P2 because .NET ships a convergence point for them, and the fifth, logging, takes the one recorded dependency (P14 over P2).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:807-814` · medium · sha:da6000c93fc5</sub>
- The port adopts the .NET ecosystem's own observability types directly (ILogger, ActivitySource/Activity, Meter, IConfiguration/IOptions<T>, TimeProvider) instead of defining a facade, per principle P14, and spends its argument on the MUSTs those types were not designed to satisfy.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:3-9` · high · sha:ddf8f695ff61</sub>
- The port deliberately builds no structured log event object, reversing Ruby's central conclusion, because a [LoggerMessage]-generated method or cached LoggerMessage.Define delegate performs the logger.IsEnabled(level) check once at the call, passes arguments in a generated state struct, and never invokes the formatter when disabled.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:35-46` · high · sha:ddf8f695ff61</sub>
- OBS-1's observable guarantee (a disabled level allocates nothing and emits nothing) holds, but its conformance step of asserting that the returned event is the shared reference-identical singleton has no referent because nothing is returned, and building an event builder over ILogger would allocate the object the generated path avoids (P4).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:35-46` · high · sha:ddf8f695ff61</sub>
- For OBS-3, log keys are template placeholders validated at compile time by the generator (a placeholder binding to no parameter is a SYSLIB1014 build error), which is stronger than a runtime rejection.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:50-53` · high · sha:ddf8f695ff61</sub>
- For OBS-4 and OBS-40 the categorisation tag is EventId.Name, carried beside the key/value state rather than in it (the state list holds only the template keys plus {OriginalFormat}), so a duplicate event key cannot arise and the collision diagnostic has nothing to diagnose.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:55-57` · high · sha:ddf8f695ff61</sub>
- For OBS-5 and OBS-9, precedence between per-event fields, global context and diagnostic context is decided by the logging provider, global context is attached by the host (BeginScope, OpenTelemetry resource attributes), and the SDK offers no global-context channel of its own.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:58-60` · high · sha:ddf8f695ff61</sub>
- OBS-8's "emit at most once" is vacuous in the port because a log call is one call.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:67-67` · high · sha:ddf8f695ff61</sub>
- LoggerMessage.Define is chosen over the generator for semconv-keyed events because a Define<string,string> over "{http.request.method} {url.full}" with EventId(1, "http.request") yields exactly those state keys and event name, using the same cached-delegate, check-once, allocation-free-when-disabled mechanism, so CA1848 is satisfied either way.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:78-81` · high · sha:ddf8f695ff61</sub>
- Tracing uses System.Diagnostics.Activity directly (OBS-21 to OBS-27), since OpenTelemetry .NET's tracing API is the runtime type itself, so the P14 subset Ruby had to define is the runtime type.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:86-88` · high · sha:ddf8f695ff61</sub>
- OBS-30's contract that callbacks never throw is left as a contract, because .NET does not guard it either: a throwing ActivityListener.ActivityStarted propagates out of StartActivity (verified).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:98-99` · high · sha:ddf8f695ff61</sub>
- OBS-23 and OBS-10 log correlation is host configuration, not SDK code: LoggerFactoryOptions.ActivityTrackingOptions folds the current Activity's TraceId/SpanId into every log scope.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:106-108` · high · sha:ddf8f695ff61</sub>
- The OBS-28/OBS-29 HTTP-tracer vocabulary maps to span structure rather than a listener object: an operation Activity opened at the Operation stage, one client-kind attempt Activity per attempt (as built, InstrumentationPolicy at the Diagnostics stage), and ActivityEvents for in-between milestones.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:123-126` · high · sha:ddf8f695ff61</sub>
- OBS-28's transport milestones (connection acquired, request sent, headers received) are emitted by the runtime itself on the reference transport, since System.Net.Http has its own ActivitySource (verified) with experimental connection-setup and DNS sources from .NET 9.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:130-133` · high · sha:ddf8f695ff61</sub>
- The traceparent fix belongs in the adapter, not the SPI: Http.SystemNet strips a traceparent/tracestate equal to the current activity's before dispatch and lets the runtime propagate, while transports that do not propagate keep the policy's stamping.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:145-147` · high · sha:ddf8f695ff61</sub>
- The SDK keeps the semantic-convention metric names on its own meter because core is transport-agnostic and a non-HttpClient transport emits nothing, and the reference transport's documentation tells users to enable one meter or the other.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:149-152` · high · sha:ddf8f695ff61</sub>
- OBS-32's naming conflict (it names http.client.request.count with unit {request} and http.client.request.duration with unit ms while saying names SHOULD follow OpenTelemetry semantic conventions) is resolved as section 11 item 38: duration in seconds and no separate count instrument, matching the runtime and OpenTelemetry.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:152-157` · high · sha:ddf8f695ff61</sub>
- Metrics (OBS-31 to OBS-33) use Meter with Counter<long> and Histogram<double>; with no listener an instrument's Record is a cheap no-op and the static instruments are shared, which is OBS-31's default without a no-op class.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:163-165` · high · sha:ddf8f695ff61</sub>
- Log granularity is modelled as a core HttpLoggingOptions record copying the shape of Microsoft.Extensions.Http.Diagnostics' LoggingOptions (a body flag, a body size limit, header allow-lists) without taking that package as a dependency, since it brings the compliance and redaction packages with it.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:187-190` · high · sha:ddf8f695ff61</sub>
- OBS-35's layered log-level lookup is satisfied by IConfiguration binding (section 8.2).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:193-193` · high · sha:ddf8f695ff61</sub>
- CTX-14's correlation bundle (trace id, span id, flags, state, flavour, validity, remoteness, active span, per-operation tracer factory) is ActivityContext plus PipelineContext.Activity plus the static ActivitySource, and CTX-15's shared untraced sentinel is default(ActivityContext); the context chain itself is section 5.4's.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:195-198` · high · sha:ddf8f695ff61</sub>

## Reference
- The reserved invalid trace-context sentinels are a trace id of 32 hex zeros, a span id of 16 hex zeros, trace flags `00` and an empty trace-state, and an all-zero trace or span id MUST be treated as invalid.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:48-48` · high · sha:1b678eca176d</sub>
- The default HTTP instrumentation SHOULD emit a request counter `http.client.request.count` (unit `{request}`) and a latency histogram `http.client.request.duration` (unit `ms`), tagged with method and either status code (success) or error type (failure).
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:60-60` · high · sha:1b678eca176d</sub>
- The .NET carriers for the observability, time and configuration seams are ILogger from Microsoft.Extensions.Logging.Abstractions defaulting to NullLogger (logging), ActivitySource("Dexpace.Sdk")/Activity with StartActivity returning null with no listener (tracing), Meter("Dexpace.Sdk") instruments (metrics), TimeProvider in-box since .NET 8 (time, CFG-15), and plain options types in core with IConfiguration/IOptions<T> binding in Dexpace.Sdk.Extensions.DependencyInjection (configuration).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:812-818` · high · sha:da6000c93fc5</sub>
- As built (d45e64b), ILogger, ActivitySource, Meter, TimeProvider and plain options types are all in use as tabulated, and the DI-side configuration binding is not built.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:832-833` · high · sha:da6000c93fc5</sub>
- Verified against a disabled logger, the formatter ran zero times and Log was never called.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:40-42` · high · sha:ddf8f695ff61</sub>
- A null log value is carried as null in the structured state so a JSON sink writes null, but the rendered message text says "(null)", which is not the literal string "null" that OBS-3 states; this residual is named under P6.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:50-54` · high · sha:ddf8f695ff61</sub>
- SDK-owned log events carry only strings and integers, so a value whose ToString throws is unreachable from them, but a throwing provider or formatter is reachable: a throwing ToString on a logged value was verified to propagate out of the log call into the caller.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:61-63` · high · sha:ddf8f695ff61</sub>
- The as-built InstrumentationPolicy uses the generator with {Method}/{Url}/{StatusCode} placeholders and event names defaulted from method names (LogSendingRequest), and is to move to Define delegates with the semconv keys.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:81-83` · high · sha:ddf8f695ff61</sub>
- OBS-25's allocation-free no-op holds because ActivitySource.StartActivity returns null when there is no listener (verified), so the untraced path allocates nothing and there is no shared no-op span.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:87-90` · high · sha:ddf8f695ff61</sub>
- OBS-21's recording flag is Activity.IsAllDataRequested/Recorded, and Activity.Stop is idempotent (verified: two Stop calls and a Dispose produced one stop callback).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:90-91` · high · sha:ddf8f695ff61</sub>
- OBS-22's scope restoration is Activity.Current, an AsyncLocal that Stop resets to the parent (verified).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:91-92` · high · sha:ddf8f695ff61</sub>
- OBS-26's reserved sentinels are default(ActivityTraceId) and default(ActivitySpanId), which render as 32 and 16 hex zeros (verified), with ActivityTraceFlags, a trace-state string and ActivityContext.IsRemote covering flags, state and remoteness.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:92-95` · high · sha:ddf8f695ff61</sub>
- The ActivityTrackingOptions flags enum is an allow-list with members TraceId, SpanId, ParentId, TraceState, TraceFlags, Tags and Baggage, matching OBS-10's shape.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:108-110` · high · sha:ddf8f695ff61</sub>
- Log correlation differs from the OBS letter in two ways: the keys are TraceId/SpanId rather than trace.id/span.id, and the generic host's default also folds ParentId.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:110-111` · high · sha:ddf8f695ff61</sub>
- The operation span is not built: the PipelineStage.Operation documentation says the stage "opens the operation span", but OperationPolicy does not.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:133-136` · high · sha:ddf8f695ff61</sub>
- Verified with a loopback server, every HttpClient send produces a System.Net.Http client span as a child of the SDK's attempt span, and the System.Net.Http meter publishes http.client.request.duration (unit s) and http.client.active_requests, the same instrument names the as-built InstrumentationPolicy records on the Dexpace.Sdk meter.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:138-143` · high · sha:ddf8f695ff61</sub>
- Known small defects of the as-built InstrumentationPolicy are that server.port is recorded as -1 for a default port (the convention wants the port number), the duration histogram's status tag is a boxed nullable, and the redacted URL is computed before the listener or log check so the disabled path allocates a UriBuilder, a StringBuilder and a string on every attempt.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:157-161` · high · sha:ddf8f695ff61</sub>
- A histogram tolerates any double and a counter is documented non-negative (OBS-33).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:165-165` · high · sha:ddf8f695ff61</sub>
- Log granularity and body preview (OBS-34 to OBS-38) are not built; OBS-34 separates what is logged (none/headers/body, default none) from ILogger's whether, and requires spans and metrics to run at every granularity.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:185-187` · high · sha:ddf8f695ff61</sub>
- As built (d45e64b) for instrumentation: DexpaceDiagnostics (source and meter), per-attempt spans, metrics and generator-based logs are built; default-allow redaction (OBS-12), non-semconv log keys (OBS-39), unconditional traceparent stamping and no emission guard (OBS-20) diverge; the operation span, granularity/body preview and header redaction are missing.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:200-203` · high · sha:ddf8f695ff61</sub>

## Conflicts

## Superseded

