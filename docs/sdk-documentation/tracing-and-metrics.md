# Tracing and metrics

**As built by phase 5c, written against source on 2026-10-07** (branch `66-phase-5c-tracing`). This page describes the spans,
span events and instruments `Dexpace.Sdk.Core` emits, how to collect them, and what changed from the earlier attempt-only
instrumentation. The requirement IDs it cites are the normative text
(`docs/product-spec/15-instrumentation-and-observability.md`, `OBS-21`-`OBS-33`); the decisions behind each choice are in the
[phase 5c design](../work/mvp/phase5/phase5c/2026-10-07-phase5c-tracing-design.md) and in
[design §8.1](../sdk-design-dotnet/08-instrumentation-and-configuration.md). Neither is restated here. Logging and redaction
are [`logging-and-redaction.md`](./logging-and-redaction.md) (phase 5b).

## Collecting it

Tracing is `System.Diagnostics.Activity` and metrics are `System.Diagnostics.Metrics`; both live in the shared framework, so
the SDK adds no dependency. Subscribe to the source and the meter by name:

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Dexpace.Sdk"))
    .WithMetrics(m => m.AddMeter("Dexpace.Sdk"));
```

With no listener `ActivitySource.StartActivity` returns `null`, the call context carries the shared untraced bundle, no tag
list is built and no URL is redacted: an untraced synchronous call allocates nothing for tracing or metrics (`OBS-25`,
`UntracedAllocationTests`).

## The span tree

```
caller's Activity.Current (optional)
 └─ GET                      Internal   one per HttpPipeline call (the operation span)
     ├─ GET                  Client     one per transmission (the attempt span)
     │   └─ GET              Client     the runtime's own System.Net.Http span, if you listen to that source
     └─ GET                  Client     the next transmission after a retry or a redirect hop
```

| Span | Kind | Name | Opens | Ends |
|---|---|---|---|---|
| Operation | `Internal` | the method; `HTTP` for a method outside RFC 9110 plus `PATCH` | when the call enters `HttpPipeline`, before the call context exists, for every pipeline shape (`CreateEmpty` included) | exactly once, when the response is returned or the call throws |
| Attempt | `Client` | the same | each transmission, in `InstrumentationPolicy` | when the response (or failure) is recorded |

**When the operation span ends.** At response headers, as `HttpClient`'s own span does, not when the body is consumed.
`Send<T>(request, handler, …)` therefore ends the span before the handler runs. A response whose body you never read leaves
no span open.

**An attempt span exists only under an operation span.** The attempt span is started through the call's correlation bundle
(`PipelineContext.Instrumentation`), not from `Activity.Current`. A call whose operation span the sampler dropped has the
untraced bundle, so it gets no attempt spans either. That is parent-based sampling read the obvious way.

**Untraced calls and an ambient activity.** When `Dexpace.Sdk`'s source has no listener, `PipelineContext.Instrumentation` is
`InstrumentationContext.None` and `CallKey.TraceId` is all-zero, even if the caller has an `Activity.Current` from another
source. Nothing is created, so `Activity.Current` stays the caller's and the host's logging scopes still fold it.

### Operation span attributes

| Attribute | When | Value |
|---|---|---|
| `http.request.method` | start | the method; `_OTHER` for an unknown one (`http.request.method_original` carries the original) |
| `server.address`, `server.port` | start | the host and the port number (`443` for an implicit `https` port) |
| `url.full` | start | the seed URL, redacted with the call's `Logging.AllowedQueryParameters` (default `api-version` only) |
| `http.response.status_code` | success | the final response's status |
| `error.type` | failure | the exception's full type name |

**Status.** A successful operation leaves the status `Unset` (OpenTelemetry reserves `Ok` for the application). A failure sets
`Error` with the exception's message, adds OpenTelemetry's `exception` event (`exception.type`, `exception.message`,
`exception.stacktrace` from `Exception.StackTrace`, never `ToString()`, so a suppressed-exception trail that could carry a URL
is not rendered) and tags `error.type`. A cancellation, a deadline included, is a failure. A fatal exception (out of memory)
is not caught anywhere: the span ends `Error` with no description and no event.

**A returned 4xx or 5xx is a successful operation unless something maps it.** `DexpacePipeline.CreateDefault` does **not**
contain `ErrorMappingPolicy`, so on the default pipeline a 404 or an exhausted 503 comes back as a response, the operation
span is not `Error`, and no `dexpace.retry.exhausted` event is emitted. The attempt span for that response does carry `error.type` and `Error` (below). Add an
`ErrorMappingPolicy` at `PerCall` and a mapped status becomes the operation's failure.

### Attempt span attributes

| Attribute | When | Value |
|---|---|---|
| `http.request.method`, `http.request.method_original` | start | as above |
| `server.address`, `server.port`, `url.scheme` | start | the host, the port number, the scheme |
| `url.full` | start | redacted as above (computed only when the span is recording) |
| `http.request.resend_count` | start | the call's transmission ordinal across retries **and** redirect hops; **absent on the first transmission** |
| `http.response.status_code`, `network.protocol.version` | response | the status and `1.0`/`1.1`/`2`/`3` |
| `error.type` | failure, or a status of 400 or more | the exception's full type name, or the status code as a string; a status of 400 or more also sets the status to `Error` (no description) |

Every write is guarded by `Activity.IsAllDataRequested`, so the SDK writes nothing to a span a sampler kept only for
propagation (`OBS-21`). (`SetTag` itself is not inert on a non-recording activity; the guarantee is about the SDK's own writes.)
The attempt span is `Activity.Current` at every SDK log site and inside the transport, and the caller's activity is restored
after the call, on success and on throw, sync and async (`OBS-22`, `OBS-23`).

## Span events

Events are on the **operation** span, because the attempt span has already ended when the retry policy decides:

| Event | When | Attributes |
|---|---|---|
| `dexpace.attempt.failed` | an attempt failed and another follows | `http.request.resend_count` (the failed transmission's ordinal), `error.type` (exception type, or the status as a string), `http.response.status_code` (when there was a response), `dexpace.retry.delay` (seconds, a `double`) |
| `dexpace.retry.exhausted` | a failing operation whose last retry sequence ended with its budget spent | `dexpace.retry.attempts` (transmissions in the exhausted sequence), `error.type` (equal to the next event's `exception.type`) |
| `dexpace.redirect.hop` | a redirect is followed (emitted by phase 6b; defined and unit-tested now) | `dexpace.redirect.hop` (1-based), `http.response.status_code`, `url.full` (redacted), `dexpace.redirect.cross_origin` |
| `exception` | the operation fails | OpenTelemetry's exception event |

`dexpace.retry.exhausted` is recorded on the call by the retry policy and **emitted by the operation's failure path,
immediately before the `exception` event**, so the pairing holds by construction: it fires only when the operation fails, and
its `error.type` is always the type of the exception that follows (`OBS-29`). A retry budget of zero is "retries off", never
"exhausted". `dexpace.attempt.failed`'s ordinal is read from the diagnostics policy's transmission counter, so in a pipeline
without `InstrumentationPolicy` it reports `0`.

## Metrics

| Instrument | Unit | Attributes |
|---|---|---|
| `http.client.request.duration` (histogram, OpenTelemetry's bucket advice `0.005` to `10` s) | `s` | `http.request.method`, `server.address`, `server.port`, `url.scheme`, and on completion `http.response.status_code` and `network.protocol.version`, or `error.type` (an exception, or a status of 400 or more) |
| `http.client.active_requests` (up-down counter) | `{request}` | `http.request.method`, `server.address`, `server.port`, `url.scheme`; the `+1` and the `-1` carry the same tags |

There is no request-count instrument (the histogram's count is the count) and no operation-level instrument. Unknown methods
are `_OTHER`, never a verbatim metric dimension. The up-down counter is decremented only if this call incremented it, so a
listener that appears mid-call never sees a lone decrement (`OBS-33`). Spans and metrics record at every log level,
including the default `None` (`OBS-34`).

**Two meters.** `System.Net.Http` publishes the same two instrument names. Enable the `Dexpace.Sdk` meter **or**
`System.Net.Http`'s, not both: each attempt is otherwise reported twice under one name.

## Trace context on the wire

`InstrumentationPolicy` stamps the attempt span's `traceparent` (and `tracestate`) on the request it forwards, so a custom
transport that does not propagate still carries the trace. `SystemNetHttpClient` drops that stamp when it equals the current
activity's id, a `System.Net.Http` listener exists and runtime propagation is on, so the wire carries the runtime's recorded
child span id and the server's span is parented to a span you can see. A `traceparent` you set yourself is sent unchanged.
With no `System.Net.Http` listener the stamp stays (the runtime would otherwise write the id of a span nobody records).

A borrowed `HttpClient` whose handler chain does not propagate (no `SocketsHttpHandler` at its root, or an
`ActivityHeadersPropagator` that injects nothing) sends no `traceparent` for a traced call while a `System.Net.Http`
listener exists: the adapter cannot see the handler. Leave propagation on, or do not listen to `System.Net.Http`.

## What is not emitted

- Byte-count milestones (`OBS-28`'s SHOULD): not emitted. Transport milestones are the runtime's own `System.Net.Http` spans.
- A Datadog trace-id flavour (`OBS-27`): the runtime models W3C ids only (design §10 entry 24). The SDK never sets
  `Activity.TraceIdGenerator`; the build bans it in `src/`. A zero id from the runtime's generator is not coerced by the SDK.
- Log correlation keys `trace.id` and `span.id`: log correlation is the host's `ActivityTrackingOptions` (`TraceId`, `SpanId`;
  design §10 entry 23).
- A shared no-op span object: the untraced span is `null`.
- Throwing listeners are not defended against: a throwing `ActivityListener` or `MeterListener` callback propagates out of the
  call (`OBS-20`, `OBS-30`). If one throws after a response exists, the SDK disposes that response before the exception
  propagates.

## Migrating from the earlier instrumentation

| Was | Is |
|---|---|
| Attempt spans were children of the caller's `Activity.Current` | They are children of the operation span; the operation span is the child |
| `PipelineContext.Instrumentation` was built from the ambient activity | It is the operation span's bundle, or `None` when untraced; `CallKey.TraceId` is zero when untraced |
| `server.port` was `-1` for a default port | The port number |
| `http.request.resend_count` was `0` on the first attempt and reset per redirect hop | Absent on the first transmission; counts retries and hops |
| A 4xx/5xx response left the attempt span status unset | `error.type` is the status code and the status is `Error` |
| Unknown methods were verbatim | `_OTHER`; the span is `HTTP` |
| Metrics were tagged with the method and status only | The stable attribute sets above |
| The SDK's `traceparent` always reached the wire | `SystemNetHttpClient` lets the runtime's child span id through when it is recorded |
| A throwing `ActivityStopped` leaked the response | The response is disposed first |
