# The pipeline

**As built by phase 4c, written against source on 2026-10-07** (branch `phase-4c-pipeline`). This page describes the
staged pipeline of `Dexpace.Sdk.Core` as it exists after the pipeline rework: the stages, the request-in/response-out
policy signature, `PipelineContext`, the builder, `HttpPipeline` as a transport, `ErrorMappingPolicy`, and the
synchronous path. The requirement IDs it cites are the normative text
(`docs/product-spec/08-execution-pipelines.md`, `PIPE-1`-`PIPE-40`); the decisions behind each type are in the
[phase 4c design](../work/mvp/phase4/phase4c/2026-10-05-phase4c-pipeline-design.md) and in
[design §5](../sdk-design-dotnet/05-pipeline-architecture.md). Neither is restated here.

Ruby's counterpart page (`ruby-sdk/docs/sdk-documentation/pipelines.md`) is not in the local clone, as the 2a to 3b
pages also found; this page is written from this repository's source.

## The stages

A policy declares one `PipelineStage`; the builder sorts policies by it, stably (`PIPE-1`).

| Stage | Value | Pillar | Loop it sits in | What runs there |
|---|---|---|---|---|
| `Operation` | 100 | yes | outside everything | `OperationPolicy`: the overall deadline |
| `PerCall` | 150 | no | once per call, outside redirect and retry | `IdempotencyPolicy`, `ClientIdentityPolicy`, `ErrorMappingPolicy` |
| `Redirect` | 200 | yes | the redirect loop | `RedirectPolicy` |
| `PerHop` | 250 | no | once per redirect hop, outside retry | (user policies) |
| `Retry` | 300 | yes | the retry loop | `RetryPolicy` |
| `PerAttempt` | 400 | no | once per attempt | `SetDatePolicy` |
| `Auth` | 500 | yes | per attempt | the auth policies |
| `Diagnostics` | 600 | yes | per attempt | `InstrumentationPolicy` |
| `Serde` | 700 | yes | per attempt | reserved; no shipped policy |

A pillar admits one policy. The keys are sparse (`PIPE-3`); there is no user slot after `Auth`, `Diagnostics` or `Serde`,
and a first consumer that needs one adds it additively. `Operation` is a sixth singleton outside the spec's single
pre-redirect slot (design §11).

`PerCall` is 150: it runs outside both loops and sees only the terminal response (`PIPE-2`). The slot that used to be
called `PerCall` (250, inside the redirect loop) is `PerHop`.

## Writing a policy

```csharp
public sealed class StampPolicy : HttpPipelinePolicy
{
    public override PipelineStage Stage => PipelineStage.PerAttempt;

    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        continuation.RunAsync(request.WithHeader("X-Stamp", "1"), context);

    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        continuation.Run(request.WithHeader("X-Stamp", "1"), context);
}
```

- A policy receives the request, may call `continuation` any number of times, and returns the response it chooses
  (`PIPE-12`). The request it passes on is the request every downstream policy and the transport receive (`PIPE-14`). A
  short-circuit is returning a synthetic `Response` without calling `continuation`.
- `PipelineRunner` is a `readonly struct`: each `RunAsync` / `Run` call on the same value is an independent drive of the
  whole downstream tail from the same position (`PIPE-13`, `PIPE-15`). A re-driver (retry, redirect) calls it once per
  drive with the request it holds, so a downstream stamp never reaches the next attempt (`RETRY-44`, `PIPE-16`, S6).
- Every shipped policy implements both entry points through one private `ProcessCoreAsync(..., bool async)`, so there is
  one body of logic (`PIPE-28`). `Process` has a default that blocks on `ProcessAsync`; override both to keep the
  synchronous path non-blocking. A synchronous throw from `ProcessAsync` becomes a faulted task (`PIPE-30`).
- A policy keeps no per-call state in its fields: policies are shared across calls (`PIPE-11`).
- A superseded response (one a re-driver replaces) is disposed before the next drive; the in-flight response is returned
  undisposed on every path that stops re-driving (`PIPE-40`).

## `PipelineContext`

One context is created per call and copied per drive. The call-scoped values are shared by reference with every copy:
`SeedRequest` (the request handed to `Send`, the origin the redirect and auth policies compare against, `REDIR-11`,
`REDIR-24`), `RequestOptions` (the caller's per-call options, `PIPE-17`), `Options` (the `DexpaceClientOptions`),
`CallKey`, `Instrumentation` and a property bag. The per-drive values travel by copy: `CancellationToken`, `Activity`,
`AttemptNumber`, `HopNumber`, through `ForAttempt`, `ForHop`, `WithActivity` and `WithCancellationToken`. There is no
`Request` and no `Response` on it, so nothing can write a request upward.

The bag is keyed by `PipelinePropertyKey<T>` instances compared by reference: declare a key as a `static readonly` field
of the policy that owns it. The public string key `"dexpace.auth.origin"` is retired.

## The builder

```csharp
var pipeline = new PipelineBuilder()
    .AddStandardResilience()            // Operation, Redirect, Retry, Diagnostics, into empty pillars only
    .Add(new IdempotencyPolicy())
    .Add(new ErrorMappingPolicy())      // throw HttpResponseException for a 4xx or 5xx
    .Build(transport);
```

- `Add` appends and `Prepend` puts a policy at the head of its stage (`PIPE-7`); `AddRange` keeps the batch's order and
  `PrependRange` reverses it (`PIPE-38`); both are all-or-nothing (`PIPE-23`).
- A distinct second policy for an occupied pillar throws at the call that adds it, naming both types and `Replace<T>`
  (`PIPE-5`); re-adding the same instance is a no-op, by reference identity (`PIPE-6`). A policy reporting a stage that
  is not a `PipelineStage` member is rejected (`PIPE-8`).
- `InsertBefore<T>` / `InsertAfter<T>` / `Replace<T>` stay within the anchor's stage and throw `ArgumentException`
  otherwise; a missing anchor throws `InvalidOperationException`; `Remove<T>` removes every instance (`PIPE-18`-`PIPE-21`).
  The order after any edit equals the order of a builder seeded from scratch with the resulting set (`PIPE-22`).
- `AddStandardResilience` installs the four resilience policies into EMPTY pillars only and installs nothing if any is
  occupied (`PIPE-24`). `DexpacePipeline.CreateDefault` is built from it plus the per-call defaults and an optional auth
  policy; `DexpacePipeline.CreateEmpty` forwards straight to the transport (`PIPE-39`).
- `PipelineBuilder.Flatten(pipeline)` copies the policies, transport and client options, so a policy added afterwards
  runs inside the inner loops; `PipelineBuilder.Nest(pipeline)` starts empty with the pipeline as its transport, so the
  inner loops are opaque (`PIPE-35`). `Build()` builds over the seeded transport.

## `HttpPipeline`

- **As a transport.** `HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient` (`PIPE-26`), so one pipeline can wrap
  another and back a paginator. Disposal is a no-op toward the transport, which the pipeline never owns, and there is no
  latch: a disposed pipeline stays usable (`PIPE-27`; `SEAM-15` is a MAY).
- **Two kinds of options.** The seam carries `RequestOptions` (per call), which reach every policy and the transport by
  reference; the policies read `DexpaceClientOptions`, which the pipeline captures at `Build`. The overloads taking a
  `DexpaceClientOptions` override the captured ones for one call; phase 5a, which makes that record immutable, decides
  their future. No overload of the `SendAsync` / `Send` family carries an optional token: pass `CancellationToken.None`.
- **Handlers.** `SendAsync<T>(request, handler, options, token)` and `Send<T>` apply the handler and dispose the response
  in every outcome; a handler failure surfaces as itself and a dispose failure is attached to it through
  `ExceptionTrail` (`PIPE-31`).
- **The bridges.** `AsAsync(scheduler)` and `AsBlocking()` (2b) wrap a pipeline like any transport (`PIPE-33`, `PIPE-34`).

## `ErrorMappingPolicy` versus `EnsureSuccess`

`ErrorMappingPolicy` (`PerCall`) turns a 400..599 response into an `HttpResponseException` carrying a replayable copy of
the body, capped at `Response.MaxBufferedErrorBytes`, through the same step and buffer as `Response.EnsureSuccessAsync` and
the new synchronous `Response.EnsureSuccess` (`PIPE-37`, `RECOV-15`, `RECOV-16`). Any other status is returned untouched:
its body is not read, consumed or disposed (`BODY-31`). A response whose body is the empty replayable body is mapped
without a drain (`BODY-30`). The policy is not in `CreateDefault`: returning the response for any status is the default.

## The async-redirect asymmetry

The reference contract has the asynchronous standard pipeline not follow redirects (`PIPE-32`, `REDIR-25`). This port
reverses that: `AddStandardResilience` installs one `RedirectPolicy`, so `SendAsync` and `Send` both follow redirects
(design §10 entry 14, topic `async-redirect-pillar`). The invariant it protected, that one layer follows redirects, is
kept by the transport (S3). A caller who wants the 3xx verbatim sets `MaxRedirects` to zero or builds without the policy; the defaults, the allowed-method set and the refusals are on [`redirect.md`](./redirect.md).

## The synchronous path, and its residuals

`Send` drives `Process` all the way down; the retry policy waits with a genuine blocking wait over the `TimeProvider`
(`TimeProviderWaits.Sleep`, no task under it). Two residuals remain, both documented bridges: `BearerTokenAuthPolicy` resolves its
token through the async token cache until phase 6c gives `AccessTokenCache` a synchronous path, and
`SystemNetHttpClient.Execute` blocks on the async send until phase 8b (`PIPE-28`).

## Migrating a policy from phase 4b and earlier

| Was | Is |
|---|---|
| `ProcessAsync(PipelineContext, PipelineRunner) -> ValueTask` | `ProcessAsync(Request, PipelineContext, PipelineRunner) -> ValueTask<Response>` |
| `context.Request = x; await continuation.RunAsync(context)` | `return await continuation.RunAsync(x, context)` |
| `context.Response` after the call | the returned value |
| `context.AttemptNumber = n` | `continuation.RunAsync(request, context.ForAttempt(n))` |
| `context.GetProperty<T>("k")` | `context.TryGetProperty(key, out var value)` with a `PipelinePropertyKey<T>` |
| `new PipelineContext(...)` | none: the pipeline creates the context |
| `PipelineStage.PerCall` meaning "per hop" | `PipelineStage.PerHop` |
| `SendAsync(request, options)` | `SendAsync(request, options, CancellationToken.None)` |
| a second pillar policy throwing at `Build` | throws at `Add` |
