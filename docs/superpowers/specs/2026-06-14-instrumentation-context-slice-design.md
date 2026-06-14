# Instrumentation + context chain — slice design

- **Date:** 2026-06-14
- **Status:** Approved; ready for implementation planning.
- **Part of:** [.NET SDK Platform Architecture & Build Plan](2026-06-14-dotnet-sdk-platform-design.md) — slice 3.

## 1. Purpose & scope

Define the diagnostics surface and the per-call context object that the pipeline threads through its
policies.

**In scope:** the `ActivitySource`/`Meter` declarations and instrument set, span naming + tags
(OpenTelemetry HTTP semantic conventions), `ILogger` event conventions + redaction, and the
`PipelineContext` type.

**Out of scope:** the pipeline delegate/stages that *thread* the context (slice 4) and the policies
that *emit* the telemetry (slice 5) — defined there, consuming what this slice declares.

## 2. Decisions

- **No bespoke telemetry types.** Spans are `Activity` from one `ActivitySource`; metrics are a
  `Meter`; logs go through `ILogger`. This is OpenTelemetry-native — collectors pick it up with no
  adapters.
- **One source/meter named `"Dexpace.Sdk"`**, version-stamped, declared once in a `Diagnostics`
  static class.
- **Explicit per-call `PipelineContext`**, mutable, threaded through the pipeline delegate. Trace
  correlation comes from `Activity.Current`; there is no `ContextStore` and no ambient SDK state.
- **Near-zero overhead when unobserved.** `Activity` is created only when the `ActivitySource` has
  listeners; otherwise it stays null and the hot path allocates nothing for tracing.

## 3. `PipelineContext` (sketch)

```csharp
namespace Dexpace.Sdk.Core.Pipeline;

public sealed class PipelineContext
{
    public Request Request { get; set; }                  // current request; policies may replace it (redirect, auth)
    public Response? Response { get; internal set; }      // populated once the response arrives
    public Activity? Activity { get; internal set; }      // active SDK span, or null when no listener
    public DexpaceClientOptions Options { get; }          // per-call snapshot
    public CancellationToken CancellationToken { get; }
    public int AttemptNumber { get; internal set; }       // retry attempt counter

    public T? GetProperty<T>(string key);                 // typed property bag for cross-policy state
    public void SetProperty<T>(string key, T value);
}
```

- One instance per call, created by the pipeline at dispatch and passed down the delegate chain.
- Policies read/replace `Request`, stash cross-policy state in the property bag (e.g. the idempotency
  policy parks its key for the retry policy to reuse).
- No ambient access. When user code needs call metadata it reads it from the returned `Response` or
  the thrown exception, not from hidden state.

## 4. Diagnostics conventions

```csharp
namespace Dexpace.Sdk.Core.Diagnostics;

public static class DexpaceDiagnostics
{
    public static readonly ActivitySource ActivitySource = new("Dexpace.Sdk", AssemblyVersion);
    public static readonly Meter Meter = new("Dexpace.Sdk", AssemblyVersion);
}
```

**Spans** — client-kind `Activity`; name = HTTP method (low cardinality, per current semconv).
Tags: `http.request.method`, `url.full` (redacted), `url.scheme`, `server.address`, `server.port`,
`http.response.status_code`, `http.request.resend_count`, `error.type`.

**Metrics** — OTel semantic-convention names where they exist:
- `http.client.request.duration` — `Histogram<double>` (seconds)
- `http.client.active_requests` — `UpDownCounter<long>`
- `dexpace.client.retries` — `Counter<long>` (SDK-specific)
- `dexpace.client.redirects` — `Counter<long>` (SDK-specific)

**Logging** — `ILogger` with stable `EventId`s and source-generated `LoggerMessage` methods
(zero-alloc, AOT-safe). High-volume events at `Debug`/`Trace`; retries and failures at `Warning`.
Structured fields: method, redacted URL, status, attempt, elapsed. In DI, `ILogger<T>` is injected;
in manual construction, policies accept an `ILogger` defaulting to `NullLogger`.

**Redaction** — a `UrlRedactor` strips `Authorization`/`Cookie`/`Set-Cookie` and configurable query
parameters before any value is logged or attached as a span tag.

## 5. Project & repo changes

- `Core`: add `Pipeline/PipelineContext.cs`, `Diagnostics/DexpaceDiagnostics.cs`, and
  `Diagnostics/UrlRedactor.cs`. Uses `System.Diagnostics.DiagnosticSource` and
  `Microsoft.Extensions.Logging.Abstractions` — both already `Core` dependencies.
- No new package references.

## 6. Open items (resolve during planning or in dependent slices)

- `AttemptNumber` 0- vs 1-based — finalize alongside the retry policy (slice 5).
- Property-bag shape: `string`-keyed typed helpers (above) vs. a small set of strongly-typed slots.
- Confirm span-name policy against the current OTel HTTP semantic conventions at implementation time
  (method-only when no route template is available).
