# 06 — Logging & Observability

A production request you cannot trace is a request you cannot debug, so telemetry is part of the design, not an afterthought bolted on during the incident. This chapter embodies ASP-5: logs are structured and levelled, traces and metrics flow through OpenTelemetry, every signal carries the request's correlation id, and secrets never reach a sink. It extends the core idiom of saying *why* (core [07](../csharp/07-csharp-idioms.md)) to runtime, where the "why" is the trace that must already be there when the page fires.

## What good looks like

```csharp
namespace Dexpace.Billing;

public sealed partial class PaymentService(ILogger<PaymentService> logger)    // framework interface keeps its I- (6.1)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Charged {OrderId} for {Amount}")]
    private partial void LogCharged(Guid orderId, decimal amount);            // source-generated, allocation-free (6.2)

    public async Task Charge(Order order, CancellationToken cancellationToken)
    {
        using Activity? span = ActivitySource.StartActivity("charge");        // span carries the trace id (6.4)
        try
        {
            await _gateway.Send(order, cancellationToken);
            LogCharged(order.Id.Value, order.Amount);                         // structured placeholders (6.1)
        }
        catch (PaymentException ex)
        {
            logger.LogError(ex, "Charge failed for {OrderId}", order.Id.Value); // carries the exception object (6.3)
            throw;
        }
    }
}
```

The service logs through `ILogger<T>` with named structural placeholders rather than interpolation (6.1), the hot-path message routes through a source-generated `LoggerMessage` partial (6.2), the failure logs at `Error` with the exception object attached, not its bare message (6.3), and the work runs inside an `Activity` span so the correlation id is on every signal (6.4). No secret or PII enters a message (6.7), and `Console.WriteLine` appears nowhere (6.8).

## Rules

### 6.1 — Log through `ILogger<T>` with named structural placeholders; never interpolate the message.

**Reasoning, step by step:**
1. A structured logger treats the message as a *template* — `"Charged {OrderId} for {Amount}"` — and stores `OrderId` and `Amount` as named fields the sink can index and query. String interpolation or concatenation collapses that into one opaque rendered string, so the value is no longer a field you can filter or aggregate on; you have thrown away the entire reason to use structured logging. The template stays a constant, and the values arrive as arguments.
2. Inject `ILogger<T>` so the category is the type name automatically and the framework interface keeps its `I-` prefix (it is not first-party). Pass values positionally to the placeholders, never `$"...{value}..."` into the message — interpolation renders eagerly even when the level is disabled, paying the formatting cost for a line that is dropped, and defeats the template the analyzer wants to keep static (6.2).

**Worked example:**
```csharp
logger.LogInformation($"Charged order {id}");                       // bad — opaque string, no queryable field
logger.LogInformation("Charged {OrderId} for {Amount}", id, amount); // good — named fields, indexable
```
**Enforcement:** `CA2254` (template should be a static expression); review rejects interpolation into a log message.

### 6.2 — Use the `LoggerMessage` source generator on hot-path log statements.

**Reasoning, step by step:**
1. A plain `logger.LogInformation(...)` call boxes every value-type argument into `object` for the `params object?[]` array and parses the template on each call. The `LoggerMessage` source generator emits a strongly typed `partial` method that caches the template, skips the boxing, and short-circuits with a level check before evaluating arguments — so a disabled `Debug` line costs almost nothing. On a path that runs per request, that is allocation removed from the steady state (core [15](../csharp/15-performance.md)).
2. Declare the method `partial` with `[LoggerMessage(...)]` carrying the level, event id, and message template; the generator writes the body. The template is checked at compile time against the parameters, so a placeholder with no matching argument is a build error rather than a silently empty field. Reserve the direct `ILogger` calls for cold paths where the ergonomics matter more than the nanoseconds.

**Worked example:**
```csharp
public sealed partial class OrderLog
{
    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Placed {OrderId}")]
    public static partial void Placed(ILogger logger, Guid orderId);  // generated body, no boxing, level-gated
}
```
**Enforcement:** `CA1848` (use the `LoggerMessage` delegates); review on hot-path logging.

### 6.3 — Pick the log level deliberately; errors carry the exception object.

**Reasoning, step by step:**
1. The level is a contract with whoever reads the logs, and each rung means something specific: `Trace` and `Debug` are developer detail off in production, `Information` is the production default that records normal milestones, `Warning` is a recoverable anomaly worth noticing, `Error` is a failed operation, and `Critical` is the process or a core dependency down. Choosing the level by feel floods `Information` with noise or buries a real failure at `Debug`, and either way the level filter — the operator's primary tool — stops working.
2. When you log a failure, pass the caught exception as the first argument (`logger.LogError(ex, "...")`) so the sink captures the type, message, stack trace, and chained inner exceptions (core [08.3](../csharp/08-error-handling.md)). Logging only `ex.Message` discards the stack trace, the single most useful fact in the report. Log the failure where it is handled, not at every frame it passes through, so one fault is one error line.

**Worked example:**
```csharp
logger.LogError("save failed: {Message}", ex.Message);              // bad — stack trace and inner cause lost
logger.LogError(ex, "save failed for {OrderId}", order.Id.Value);   // good — full exception captured
```
**Enforcement:** review of level choice and exception-object passing; `Information` is the configured production floor (chapter [01](./01-host-and-configuration.md)).

### 6.4 — Carry the request correlation id on every log and span.

**Reasoning, step by step:**
1. A single request fans out across middleware, services, and downstream calls, producing log lines and spans in different components; without a shared id they are orphans that cannot be stitched into the one story of what that request did. ASP.NET Core and OpenTelemetry already establish that id — the `TraceId` on `Activity.Current` — and the logging provider can attach it to every line automatically, so the correlation is free if you let the ambient `Activity` carry it.
2. Propagate the context across every `async` and service boundary so it survives the await and the outbound call: `Activity.Current` flows with the execution context, and `IHttpClientFactory` injects the trace headers into downstream requests so their logs join the same trace. Never invent a parallel correlation id passed by hand — use the ambient one the framework maintains, and a client disconnect or a downstream failure is traceable end to end.

**Worked example:**
```csharp
using Activity? span = ActivitySource.StartActivity("place-order"); // new span, child of the current trace
span?.SetTag("order.sku", sku.Value);                               // structured context on the span
logger.LogInformation("Placing {Sku}", sku.Value);                 // line auto-tagged with the same TraceId
```
**Enforcement:** OpenTelemetry logging enabled so `TraceId` is on every line; review of any hand-rolled correlation id.

### 6.5 — Emit traces and metrics through OpenTelemetry, exported via OTLP.

**Reasoning, step by step:**
1. Logs answer "what happened to this one request"; metrics answer "what is the rate and latency across all of them" and traces answer "where did the time go across services". Scraping logs to reconstruct a rate or a P99 is slow, lossy, and expensive — a counter and a histogram give those directly. Register OpenTelemetry (`AddOpenTelemetry().WithTracing().WithMetrics()`), instrument the spans and instruments that matter — the calls that can be slow or fail — and export over OTLP to whatever backend the platform runs.
2. Instrument deliberately rather than everywhere: a span on every trivial method is cost and noise, while the database call, the outbound HTTP request, and the queue publish are where latency hides. Metrics over log-scraping for anything counted or timed; the exporter is OTLP so the backend is a deployment choice, not a code dependency, and swapping it touches configuration only (chapter [01](./01-host-and-configuration.md)).

**Worked example:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()) // the calls that matter
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation())
    .UseOtlpExporter();                                                                // backend is config, not code
```
**Enforcement:** OpenTelemetry registered with OTLP export; review of span and metric coverage on slow/failing paths.

### 6.6 — Make health honest: liveness checks the process, readiness checks the dependencies.

**Reasoning, step by step:**
1. An orchestrator asks two different questions and conflating them causes outages. *Liveness* — "is this process wedged, should you restart it" — must check only the process itself; if it also pings the database, a brief database blip restarts every healthy instance and turns a small problem into a cascading one. *Readiness* — "can this instance serve traffic right now" — checks the dependencies the instance actually needs, so the orchestrator routes around an instance whose database connection is down without killing it.
2. Register the dependency checks with `AddHealthChecks()` and tag them to the right endpoint, so readiness reflects real serving capacity and liveness reflects only process health (chapter [08](./08-build-and-deployment.md)). A readiness probe that checks a dependency the instance does not use is a false negative that sheds healthy capacity; check exactly what serving a request requires, no more.

**Worked example:**
```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, tags: ["ready"]);                  // readiness depends on the database
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });            // liveness: process only
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") }); // readiness: deps
```
**Enforcement:** distinct liveness and readiness endpoints; review confirms liveness omits dependency checks (chapter [08](./08-build-and-deployment.md)).

### 6.7 — Never log secrets or PII; redact before the value reaches a sink.

**Reasoning, step by step:**
1. A logged secret is a leaked secret — once a token, password, connection string, or personal record lands in a log store, it lives in backups, indexes, and screens far outside the system that guarded it, and rotating it is the only remedy. Logs are read widely and retained long, so they are the worst place for a credential. Never put a secret or personal datum in a message template or a span tag, and never log a whole request or DTO that might contain one.
2. Redact at the source, before the value is handed to the logger: log an account *id*, not the email; a card's last four, not the PAN; the fact a header was present, not its value. Where a type could carry sensitive fields, give it a logging projection that omits them rather than trusting every call site to remember (see [security.md](../security.md)). Redaction is the default, and logging a sensitive value is a deliberate, reviewed exception that should essentially never occur.

**Worked example:**
```csharp
logger.LogInformation("Authenticated {Email} with {Token}", email, token); // bad — PII and a live credential leaked
logger.LogInformation("Authenticated {UserId}", user.Id.Value);            // good — opaque id, nothing sensitive
```
**Enforcement:** review for secrets/PII in templates and tags; redaction policy per [security.md](../security.md).

### 6.8 — Ban `Console.WriteLine` for diagnostics; logging goes through the abstraction.

**Reasoning, step by step:**
1. `Console.WriteLine` writes an unstructured, unlevelled, uncorrelated string straight to stdout: it cannot be filtered by level, carries no `TraceId`, exposes no named fields, and ignores every sink and formatter the host configured. It is invisible to the log pipeline and useless in an incident, yet it looks like logging, so it lingers as dead noise. A diagnostic worth writing is worth writing through `ILogger<T>` (6.1).
2. The one legitimate `Console` use is a CLI tool deliberately printing program output for a human at a terminal — that is the program's interface, not diagnostics. Everything a service emits about its own operation goes through the logging abstraction so it inherits levels, structure, correlation, and the configured sinks; a left-behind `Console.WriteLine` in request code is a review finding.

**Worked example:**
```csharp
Console.WriteLine($"processing {orderId}");                         // bad — unstructured, unlevelled, uncorrelated
logger.LogDebug("Processing {OrderId}", orderId);                   // good — levelled, structured, correlated
```
**Enforcement:** review rejects `Console.WriteLine` in service code; an analyzer can flag `Console` use outside CLI entry points.

## Cross-references

- Structured logging idioms, pattern matching on results, and saying *why*: [../csharp/07-csharp-idioms.md](../csharp/07-csharp-idioms.md). Allocation, boxing, and the source-generator cost model: [../csharp/15-performance.md](../csharp/15-performance.md).
- Health probes, graceful shutdown, and the orchestrator contract: [08-build-and-deployment.md](./08-build-and-deployment.md). Secret and PII redaction policy: [../security.md](../security.md).
