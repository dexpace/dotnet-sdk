# Pipeline spine — slice design

- **Date:** 2026-06-14
- **Status:** Approved; ready for implementation planning.
- **Part of:** [.NET SDK Platform Architecture & Build Plan](2026-06-14-dotnet-sdk-platform-design.md) — slice 4.

## 1. Purpose & scope

The composable request/response pipeline — the spine every cross-cutting concern plugs into.

**In scope:** the `HttpPipelinePolicy` abstraction, the `PipelineRunner` ("next") mechanism, the
named `PipelineStage` model with pillar semantics, the `PipelineBuilder` (type-targeted edits), the
`HttpPipeline` entry point, the fixed terminal transport runner, and the sync bridge. It threads the
`PipelineContext` from slice 3.

**Out of scope:** the concrete policies (slice 5) and auth (slice 6).

## 2. Decisions

- **Object policies with explicit `next`** (Azure.Core `HttpPipelinePolicy` pattern).
- **Named stages with pillar semantics**, streamlined from the siblings; sparse numbering leaves room
  to insert.
- **The transport is the fixed terminal** of the chain, not a replaceable policy.
- **Retry/redirect re-invoke `next` in a loop.** Because `PipelineContext` is the single mutable
  carrier and the runner is re-entrant from a fixed index, no per-attempt state cloning is needed —
  everything *downstream* of the retry/redirect policy re-runs per attempt; everything *upstream* runs
  once. Per-call vs. per-attempt is purely a function of position.
- **`ValueTask`-based**, `CancellationToken` carried on the context.

## 3. Policy abstraction (sketch)

```csharp
namespace Dexpace.Sdk.Core.Pipeline;

public abstract class HttpPipelinePolicy
{
    public abstract PipelineStage Stage { get; }
    public abstract ValueTask ProcessAsync(PipelineContext context, PipelineRunner next);
    public virtual void Process(PipelineContext context, PipelineRunner next);   // default: blocks on ProcessAsync
}

public readonly struct PipelineRunner          // the "next"
{
    public ValueTask RunAsync(PipelineContext context);   // runs the remaining chain; sets context.Response
    public void Run(PipelineContext context);             // sync variant
}
```

- A policy mutates `context.Request` before calling `next`, then inspects or replaces
  `context.Response` after.
- `RunAsync` invokes `policies[index].ProcessAsync(context, new PipelineRunner(policies, index + 1))`;
  when `index` reaches the end it calls the transport and sets `context.Response`.
- Retry/redirect: `do { await next.RunAsync(ctx); } while (ShouldRetry(ctx));` — each call re-runs
  everything positioned after the policy.

## 4. Stage model

```csharp
public enum PipelineStage           // sparse; outermost (lowest) → innermost
{
    Operation   = 100,  // once-per-call: operation span, overall timeout         (pillar)
    Redirect    = 200,  //                                                        (pillar)
    Retry       = 300,  //                                                        (pillar)
    PerAttempt  = 400,  // user policies that run each attempt (set-date, client-identity, idempotency)
    Auth        = 500,  // inside Retry, so a refreshed token is used on retry    (pillar)
    Diagnostics = 600,  // per-attempt logging / tracing / metrics, near the wire (pillar)
    // transport terminal runs last
}
```

- **Pillar stages admit exactly one policy**; a second is a clear configuration error.
- **`PerAttempt`** (and other non-pillar stages) stack multiple user policies in insertion order.
- Final numeric ordering of Redirect-vs-Retry and Auth/Diagnostics placement is validated when the
  concrete policies land (slice 5); this is the working default.

## 5. Builder

```csharp
public sealed class PipelineBuilder
{
    public PipelineBuilder Add(HttpPipelinePolicy policy);                                   // placed by Stage
    public PipelineBuilder InsertBefore<T>(HttpPipelinePolicy p) where T : HttpPipelinePolicy;
    public PipelineBuilder InsertAfter<T>(HttpPipelinePolicy p)  where T : HttpPipelinePolicy;
    public PipelineBuilder Replace<T>(HttpPipelinePolicy p)      where T : HttpPipelinePolicy;
    public PipelineBuilder Remove<T>()                          where T : HttpPipelinePolicy;
    public HttpPipeline Build(IAsyncHttpClient transport);
}
```

Policies are ordered by `Stage` then insertion order; pillar violations throw with an actionable
message; `Build` captures the transport as the terminal runner.

## 6. Entry point

```csharp
public sealed class HttpPipeline
{
    public ValueTask<Response> SendAsync(Request request, DexpaceClientOptions options, CancellationToken ct = default);
    public Response Send(Request request, DexpaceClientOptions options, CancellationToken ct = default);
}
```

`SendAsync` builds the `PipelineContext`, opens the once-per-call `Activity` at the `Operation` stage
(only when the source has listeners), runs the chain through `PipelineRunner`, and returns
`context.Response` or throws the mapped exception.

## 7. Sync support

Async-first. The sync `Send` drives `PipelineRunner.Run` and `HttpPipelinePolicy.Process`, whose
default implementation blocks on the async path — so the existing `IHttpClient` seam works end-to-end
without every policy hand-writing a sync variant.

## 8. Project & repo changes

- `Core`: add `Pipeline/HttpPipelinePolicy.cs`, `PipelineRunner.cs`, `PipelineStage.cs`,
  `PipelineBuilder.cs`, `HttpPipeline.cs` (joining `PipelineContext` from slice 3).
- No new dependencies.

## 9. Open items (resolve during planning or in slice 5)

- Redirect-vs-Retry outermost ordering and final Auth/Diagnostics placement.
- `PipelineRunner` as a `readonly struct` (holding the policy array + index + transport) to avoid
  per-hop allocation.
- Whether shipped policies implement true sync paths or rely on the blocking bridge for v1 (leaning
  blocking bridge, documented).
- Overall/attempt timeout handling at the `Operation` stage via linked `CancellationTokenSource`,
  coordinated with `DexpaceClientOptions` timeouts.
