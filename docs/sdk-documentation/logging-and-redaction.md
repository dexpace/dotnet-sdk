# Logging and redaction

**As built by phase 5b, written against source on 2026-10-07** (branch `65-phase-5b-logging`). This page describes what
`Dexpace.Sdk.Core` writes to an `ILogger`, how it decides what to hide, and how to turn it on. The requirement IDs it cites
are the normative text (`docs/product-spec/15-instrumentation-and-observability.md`, `OBS-1`-`OBS-40`; `XCUT-19`, `XCUT-20`,
`XCUT-24`); the decisions behind each choice are in the
[phase 5b design](../work/mvp/phase5/phase5b/2026-10-07-phase5b-logging-design.md) and in
[design §8.1](../sdk-design-dotnet/08-instrumentation-and-configuration.md). Neither is restated here.

Ruby's counterpart page (`ruby-sdk/docs/sdk-documentation/logging-and-redaction.md`) is the source of the header vectors; this page is
written from this repository's source. Traces and metrics (the operation span, `traceparent`, the instruments) are phase 5c's
and are not described here, except where they meet the log.

## Turning it on

Logging is off. `DexpaceClientOptions.Logging` is an `HttpLoggingOptions` whose `Level` defaults to `HttpLogLevel.None`:

```csharp
var options = new DexpaceClientOptions
{
    Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers },
};
var pipeline = DexpacePipeline.CreateDefault(transport, logger: loggerFactory.CreateLogger("Dexpace.Sdk"));
using var response = await pipeline.SendAsync(request, options, cancellationToken);   // or per call, as here
```

| Level | What is written |
|---|---|
| `None` (default) | Nothing. No `http.request` or `http.response` event, no allocation, no redacted URL computed, no body wrapper. The span and the instruments still record (`OBS-34`). |
| `Headers` | `http.request` before the send and `http.response` after it, with the redacted URL, the status and the allow-listed headers. |
| `Body` | Everything in `Headers` plus a bounded preview of the request and response bodies. |

The options are per call: pass a different `DexpaceClientOptions` to a `SendAsync`/`Send` overload to log one call. The logger is
the one given to the pipeline's `InstrumentationPolicy` (`CreateDefault(…, logger: …)`, `AddStandardResilience(…, logger)` or
`new InstrumentationPolicy(logger)`); a pipeline without that policy logs nothing.

> **`Body` logs payloads verbatim, up to `BodyPreviewSize` bytes (default 8,192).** It is for diagnosis, not for production: a
> body can carry credentials or personal data that no header or URL redaction reaches (`XCUT-19`). Keep it out of shared
> configuration.

## The events

Every name, id and key is a `const` on `DexpaceLogEvents` or `DexpaceLogKeys`, so `PublicAPI.Unshipped.txt` records each value and a
rename is a reviewed change (`OBS-39`). Filter on the constants, not on string literals. The categorisation tag is
`EventId.Name`; the state never carries an `event` key (`OBS-4`).

| Event (`EventId`) | Level | When | Keys |
|---|---|---|---|
| `http.request` (100) | `Information` | before the continuation | `http.request.method`, `url.full`, `http.request.resend_count`, `http.request.header.<name>` for each header, `http.request.body.size` when the declared length is known |
| `http.response` (101) | `Information` | after the continuation returns | `http.response.status_code`, `http.response.duration_ms` (a double), `url.full`, `http.request.resend_count`, `http.response.header.<name>`, `http.response.body.size` when known; at `Body`: `http.request.body.preview`/`.preview.size` and `http.response.body.preview`/`.preview.size` |
| `http.response` (102) | `Warning` | the continuation threw (non-fatal) | `error.type` (the full type name), `http.response.duration_ms`, `url.full`, `http.request.resend_count`; at `Body`: the request preview. The exception is `ILogger`'s exception argument. |
| `http.instrumentation.log_failed` (120) | `Warning` | emitting a log event failed | `dexpace.instrumentation.failed_event`, `error.type`; the exception attached |
| `http.instrumentation.body_capture_failed` (121) | `Warning` | a response preview drain failed | `error.type`; the exception attached |
| `dexpace.dispose.suppressed` (130) | `Warning` | a dispose failed where it must not mask the outcome | `dexpace.dispose.resource_type`, `error.type` (never a message) |

Values are strings, integers, longs and doubles; a header with several values is one key whose value is the individually
redacted values joined with `", "` (`OBS-6`). A `null` value is carried as `null` in the state; a formatter renders it `(null)` (the
residual `design §10 entry 22` names). `http.request.body.size` / `http.response.body.size` are always the declared length (and
absent when unknown); `*.body.preview.size` is the number of bytes the preview was rendered from (`OBS-36`).

The request preview rides on the response (or failure) event, not on `http.request`: the body is written inside the continuation,
after the request event has gone.

**Event-id ranges.** Core's ids are partitioned by subsystem so later phases never collide: 100-109 request and response,
110-119 transport header drops (reserved for the transport conformance phase, `OBS-19`), 120-129 instrumentation diagnostics,
130-139 disposal, 140-149 retry, 150-159 redirect, 160-169 authentication.

### Levels

The two HTTP events are `Information` and the failure event and the diagnostics are `Warning` (P5b-4). `HttpLogLevel` is already the
opt-in, so a caller who asks for `Headers` should not also have to lower a category's minimum level; an attempt failure is often
retried, which is a recoverable anomaly (`Warning`), and the operation's outcome is the span's status. This differs from the Ruby and
Node SDKs on the failure level.

## What is hidden

**Headers are default-deny** (`OBS-18`, `XCUT-19`). Only `HttpLoggingOptions.AllowedHeaderNames` are logged with their value (matched
case-insensitively; the key is the lower-cased name). The default is 26 diagnostic, non-credential names: `accept`, `accept-encoding`,
`cache-control`, `connection`, `content-encoding`, `content-length`, `content-location`, `content-type`, `date`, `etag`, `expires`,
`if-match`, `if-modified-since`, `if-none-match`, `if-unmodified-since`, `last-modified`, `location`, `retry-after`, `server`,
`traceparent`, `tracestate`, `user-agent`, `vary`, `via`, `x-correlation-id`, `x-request-id`. Any other header is logged as
`REDACTED`, so "present and hidden" stays distinguishable from "absent"; set `OmitDisallowedHeaders = true` to leave it out
instead. `Authorization`, `Cookie`, `Set-Cookie` and `X-Api-Key` are never in the default list.

**URL-valued headers are redacted as URLs** (`OBS-16`, `OBS-17`): `UrlValuedHeaderNames` defaults to `location`, `content-location`
and `referer`. Their values go through `UrlRedactor.RedactHeaderValue` before any limit is applied, on the synchronous and the
asynchronous path alike (one policy body serves both).

**The URL is always redacted** (`OBS-11`-`OBS-15`): userinfo becomes `***:***@`, and every query and fragment value becomes `***`
unless its name is in `AllowedQueryParameters` (default exactly `api-version`, case-insensitive). The same redacted string is
the `url.full` span tag, so a span and a log line about one attempt cannot disagree (P5b-10).

**`UrlRedactor.RedactHeaderValue(string)`** is public because callers need it for their own headers:

| Input | Output |
|---|---|
| `https://u:p@h/cb?code=S&api-version=1` | `https://***:***@h/cb?code=***&api-version=1` (an absolute value is redacted exactly like a request URL) |
| `/cb?code=SECRET`, `/cb#t=T`, `/cb?` | `/cb?***` (a relative value keeps its path; a cut query or fragment leaves `?***`) |
| `/static/path`, `//h:8443/x` | unchanged |
| `//user:secret@h/x` | `//***:***@h/x` (userinfo is masked on every route, `OBS-11`) |
| `http://user:secret@h/p x` | `http://***:***@h/p x` (an unparseable value is cut by text surgery, never the sentinel) |

The method is total, never throws except for a `null` argument, and never returns `[malformed url]`. "Absolute" is decided by a
scheme prefix in the text, not by `Uri.IsAbsoluteUri`, which on Unix reads `/cb?code=x` as a `file:` URI.

**Bounds** (`OBS-7`): `url.full` and each header value are cut at 8,192 UTF-16 characters and end with `…[truncated]`; a cut never
splits a surrogate pair. Previews are bounded by `BodyPreviewSize` instead.

## Body previews

At `Body`, with `BodyPreviewSize` as the one cap for both directions (`OBS-36`, `BODY-34`):

- The **request** body is tapped on the request passed downstream (never on the one a retry or redirect policy holds), so each
  attempt's preview reflects that attempt only. The tap mirrors what the transport writes, up to the cap.
- A **response** whose body has a known length and is not `text/event-stream` is wrapped: up to the cap plus one byte is read before
  `SendAsync` returns (this is added latency), then the response is handed back. A body that fits is served from memory and can be
  opened again; a larger one is served as the captured prefix followed by the live remainder, so **the caller always receives every
  byte**. `ContentLength` follows `BODY-29`.
- An **unknown-length** body (`ContentLength < 0`) or a `text/event-stream` body is never wrapped, on either path (`OBS-37`,
  P5b-12): no capture can wait on a slow producer. Such a response has no preview keys.
- A failed drain is reported as `http.instrumentation.body_capture_failed`, the `http.response` event still goes out with the partial
  preview, and the caller's own read sees the failure, once cached (`BODY-26`). Cancelling the call's token during the drain
  propagates, after the response is disposed.

Rendering (`OBS-38`): `text/*`, `application/json`, `xml`, `x-www-form-urlencoded`, `javascript`, `x-ndjson`, `yaml`, `graphql` and any
`+json`/`+xml`/`+yaml` subtype are decoded with the declared charset (UTF-8 when absent or unknown, with replacement characters, never
throwing; a matching BOM is stripped). Anything else renders as `[binary N bytes captured]`, an absent media type and `multipart/*`
included. Empty input renders as `""`. The decision keys on the media type, never on a guess at the bytes.

## What a failing logger does

A logger or sink that throws never fails the request it describes (`OBS-20`, `XCUT-20`). Each emission is guarded: a non-fatal
failure becomes one `http.instrumentation.log_failed` event, a failure while reporting that is swallowed, and an exception whose
rendering throws inside the provider is contained the same way (`OBS-6`). Two things are deliberately **not** swallowed: a fatal
exception (`OutOfMemoryException` and its derived types, `ExceptionFacts.IsFatal`), and `OperationCanceledException` when the call's
own token is cancelled. The span and meter calls are outside the guard: a throwing `ActivityListener` or `MeterListener` propagates
(`OBS-20`'s second sentence, design §11 item 37).

## Correlation

The SDK adds no logging scope or context of its own (`OBS-5`, `OBS-8`, `OBS-9`: the SDK offers no event object and no global
context). The diagnostic context is `Activity.Current` and the host's `BeginScope` state, both carried by `ExecutionContext` across
every await and thread hop the SDK makes (`OBS-24`, pinned by a test). The per-attempt `Activity` starts before `http.request`, so
these events carry the attempt span. To stamp the ids on every log line, configure the host:

```csharp
loggerFactory = LoggerFactory.Create(b => b.Configure(o => o.ActivityTrackingOptions =
    ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId));
```

That is the nearest .NET form of `OBS-10`'s `{trace.id, span.id}` fold (design §10 entry 23, `activity-as-tracing-model`).

## Nesting, and the two residuals

- A pipeline used as another pipeline's transport (`PipelineBuilder.Nest`) logs at both `Diagnostics` stages: that is the caller's
  composition. `Flatten` copies the inner policies into one pipeline, which logs once.
- **Dispose failures reach the logger** through the pipeline's `InstrumentationPolicy`: the retry and redirect policies, and the
  response wrapper, report a throwing dispose of a superseded response as `dexpace.dispose.suppressed`. Two paths remain with no
  logger, reporting to `Activity` only (P5b-6, the narrowed P3b-3 gap): a pipeline with no `InstrumentationPolicy`, and the blocking
  bridge of `HttpClientExtensions`, which has no pipeline.

## Configuration binding

`OBS-35` (layered resolution of the level) is the DI package's (phase 9): it binds `Logging.Level` from `IConfiguration` with the
section name as an argument, so no key is baked into core. An unrecognised value is expected to fail at startup rather than fall back
(design §10 entry 26). The runtime check of the binder's enum conversion (V4: case-insensitive, whitespace-tolerant) was not run in
5b: the binder package is not part of core and was not available offline; phase 9's checklist owns that evidence.

## Migrating from the earlier policy

| Was | Is now |
|---|---|
| Request/response events written on every call at `Debug`, whatever the options | Nothing unless `Logging.Level` is `Headers` or `Body` |
| Generated names `LogSendingRequest`…, ids 1-3, keys `{Method}`, `{Url}`, `{StatusCode}` | `http.request` (100), `http.response` (101/102), OpenTelemetry keys; `Information`/`Warning` |
| `error.type` was the short type name in the log | The full type name, as in the span |
| A throwing logger failed the request | The request is unaffected; `http.instrumentation.log_failed` |
| `DisposeSuppressed` (id 1), no logger on retry and redirect | `dexpace.dispose.suppressed` (130), reaches the pipeline's logger |
| At `Body`: no such level | A known-length response body comes back wrapped, with the read-ahead above |
| `url.full` span tag redacted with a fixed list | With the call's `AllowedQueryParameters` (default unchanged: `api-version`) |
