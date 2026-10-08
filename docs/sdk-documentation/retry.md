# Retry

**As built by phase 6a, written against source on 2026-10-08** (branch `72-phase-6a-retry`). This page describes how
`Dexpace.Sdk.Core` retries a call after phase 6a: the one classifier, the re-send gate, `RetryOptions`, the backoff and the
server's pacing headers, the two entry points over one engine (`RetryPolicy` and `RetryRecovery`), the suppressed trail, and the
two timeouts. The requirement IDs it cites are the normative text (`docs/product-spec/09-retry-and-resilience.md`, `RETRY-1` to
`RETRY-45`, and appendix C for `RECOV-16` to `RECOV-34`); the decisions behind each type are in the
[phase 6a design](../work/mvp/phase6/phase6a/2026-10-08-phase6a-retry-design.md) and in
[design §6.1](../sdk-design-dotnet/06-retry-redirect-and-authentication.md). Neither is restated here.

Ruby's counterpart page is not in the local clone; Node's `retry/` tests (`nodejs-sdk@54aeed4`) and Ruby's
`resilience/*_test.rb` (`ruby-sdk@5b17395`) were read for the vectors. This page is written from this repository's source.

## Two entry points, one engine

The SDK has two ways to retry and they share one loop (`RETRY-13`, `RECOV-30`):

| | `RetryPolicy` (pipeline stage) | `RetryRecovery` (recovery stack) |
|---|---|---|
| Where | the `Retry` pillar of an `HttpPipeline` (installed by `DexpacePipeline.CreateDefault` and `AddStandardResilience`) | a third argument of `RecoveryDispatcher` |
| A retryable response | kept live until it is discarded; **returned live and unread** when the retries run out (`PIPE-40`) | mapped to an `HttpResponseException` on arrival; **thrown** when the retries run out |
| Budget | none (`RETRY-28`) | `TotalTimeout`, zero for none (`RETRY-27`) |
| Pacing headers | `RetryOptions.HonorRetryAfter` | always |
| Hooks | `ShouldRetry`, `GetDelayOverride` | none |

A consumer who wants retries on a bare transport, without a pipeline, writes:

```csharp
var retry = new RetryRecovery(new RetryOptions { MaxRetryAttempts = 3 }, totalTimeout: TimeSpan.FromSeconds(20));
var dispatcher = new RecoveryDispatcher(RequestRecoveryChain.Empty, new ResponseRecoveryChain([ErrorMappingStep.Instance], []), retry);
using var response = await dispatcher.DispatchAsync(transport, request, RequestOptions.Empty, ct);
```

The dispatcher runs the request chain **once** (one idempotency key and one client-identity line for the whole call), then for
each send the transport and the response steps, and the recovery steps **once** on the terminal outcome. Every send's
surviving response whose status is in the configured set is buffered (at most 1 MiB, `RECOV-16`) and mapped, so an initial
503 is retried whether or not `ErrorMappingStep` is installed (`RECOV-19`, `P6a-6`).

There is **one retry layer per call path** (`TRANSPORT-2`, design §11 item 24): if you wrap a native client that retries by
itself (for example `AddStandardResilienceHandler` on an `HttpClient`), turn its retries off or set `MaxRetryAttempts = 0`
here. The DI package's guidance (phase 9) says the same.

## What is retried

A **response** is retried when its status is in `RetryOptions.RetryableStatusCodes` (default `408, 429, 500, 502, 503, 504`,
`XCUT-7`; every member must be an error status, 400 to 599). A **failure** is retried when it, or any exception in its cause
chain (depth 64, cycle-safe), reports `IRetryableError.IsRetryable`, or is in the I/O family: `IOException`,
`SocketException`, `TimeoutException`, an `HttpRequestException` with no `StatusCode` (`RETRY-2`). `ServiceRequestException`,
`ServiceResponseException` and `HttpResponseException` for a retryable status report it themselves (`RETRY-3`, `RETRY-4`).

- **An `HttpResponseException` anywhere in the chain is decided by the configured set alone** (`RETRY-37`): a 501 you added to
  the set is retried although the exception's own flag says no, and a `ServiceResponseException` wrapping a 503 is not retried
  when the set excludes 503.
- **A `false` capability does not veto.** A `DeserializationException` over an `IOException` is retried (`P6a-8`).
- **A cancelled call is never retried**, whatever the exception (`RETRY-23`). A `TaskCanceledException` whose inner exception
  is a `TimeoutException`, with the caller's token unsignalled, is an `HttpClient` timeout and is retried (`RETRY-24`).
- A fatal exception (`OutOfMemoryException`) passes every frame untouched (`RETRY-25`).

To make your own exception retryable without touching the SDK, implement the capability:

```csharp
public sealed class QuotaBackoffException : Exception, IRetryableError
{
    public bool IsRetryable => true;
}
```

## The re-send gate

Whatever the failure, a request is re-sent only when `RetryFacts.IsResendable` holds (`RETRY-5`, `XCUT-10`): it has **no body
and an idempotent method** (GET, HEAD, OPTIONS, PUT, DELETE; `RETRY-6`), or its **body is replayable**, whatever its method.
So a POST with a `RequestBody.FromBytes(...)` body is retried, a POST with no body is sent once, and a stream-backed body is
sent once unless it is a seekable stream with a declared length (`BODY-5`). There is no switch: `RetryNonIdempotentWhenReplayable`
is gone (`RETRY-7`). Retries are capped at `MaxRetryAttempts` after the first send (default `2`, three sends; `0` disables), or
`RequestOptions.MaxRetries` for one call when set (`RETRY-41`, `HTTP-35`).

## `RetryOptions`

| Member | Default | Rule |
|---|---|---|
| `MaxRetryAttempts` | `2` | `>= 0` |
| `BaseDelay` | 200 ms | `0` to about 292 years |
| `Multiplier` | `2.0` | finite, `>= 1` |
| `MaxDelay` | 8 s | `0` to about 292 years |
| `Jitter` | `0.2` | `0` to `1` |
| `FixedDelay` | `null` | when set, replaces the whole schedule: no growth, no jitter, no `MaxDelay` cap (`RETRY-43`) |
| `HonorRetryAfter` | `true` | the stage policy honours the server's pacing headers |
| `RetryableStatusCodes` | the six above | copied at `init`; 400 to 599 |
| `AttemptHeaderName` | `null` | when set, every send carries `name: <1-based send number>` (`RETRY-38`, `RECOV-31`) |

Every member validates where it is set (`RECOV-34`), including through `with`; equality is by value. The delay before retry `n`
is `BaseDelay × Multiplier^(n-1)`, capped at `MaxDelay`, then jittered symmetrically over `[d(1-j/2), d(1+j/2)]`
(`RETRY-9` to `RETRY-11`); the calculation saturates and never throws, and every delay is clamped to 365 days (`RETRY-18`).
`BaseDelay` above `MaxDelay` is not an error: the cap simply wins.

## The server's pacing hint

The first usable value wins, in this order (`RETRY-15`, `RETRY-21`): `Retry-After` as delta-seconds (`5`, `1.5`; ASCII digits with
an optional fraction, honoured to 100 ns) or as an HTTP-date (`HttpDate`), `retry-after-ms`, `x-ms-retry-after-ms`, then
`X-RateLimit-Reset` as a Unix epoch in seconds, which gets 0 to 20 percent positive jitter (`RECOV-25`). A malformed value is
"no hint", never zero (`RETRY-16`); a date or epoch already past is zero (`RETRY-17`). A hint **replaces** the schedule and is not
jittered (`RETRY-20`). An all-digit numeral of any size is honoured and clamped to 365 days.

**Caveat (risk 3 of the design):** some APIs send `X-RateLimit-Reset` as seconds until the reset, not an epoch. The specification
says epoch, so a small value reads as an instant long past and the retry happens at once. Turn `HonorRetryAfter` off for such an
API and use `FixedDelay`, or override the delay (below).

## Hooks and the `X-Should-Retry` recipe

`RetryPolicy` is unsealed and exposes two protected virtual hooks; everything else, including the stage, is sealed. They must
be pure, stateless and fast, because the policy is shared across calls.

- `ShouldRetry(RetryAttemptContext)` returns `true`, `false` or `null` (defer). It flips the **condition** only: the re-send
  gate and the attempt cap still apply, and it is not asked about a cancelled call. If it throws, the call fails with an
  `InvalidOperationException` (inner exception: what it threw; the attempt's own failure suppressed under it).
- `GetDelayOverride(RetryAttemptContext)` returns a delay or `null`. A throw or a negative value is logged as event 140
  (`dexpace.retry.delay_override_failed`) and the next source is used (`RETRY-40`).

```csharp
public sealed class ServerHintRetryPolicy(TimeProvider? clock = null) : RetryPolicy(clock)
{
    protected override bool? ShouldRetry(RetryAttemptContext attempt) =>
        attempt.Response?.Headers.Get("X-Should-Retry") switch
        {
            "true" or "1" or "yes" or "retry" => true,
            "false" or "0" or "no" or "stop" => false,
            _ => null,
        };
}
```

`RetryAttemptContext.Response` is the live response: read its headers, do not dispose it or read its body (`RETRY-29`).

## What a failed call carries

When a call fails, the exception it throws carries every earlier attempt's failure, oldest first: on an `SdkException` in
`Suppressed` (and in `ToString()`), on any other exception through `ExceptionTrail.GetSuppressed` (`RETRY-34`). A discarded
error response is drained (at most 1 MiB) into an `HttpResponseException` for that trail and its connection is released
(`RETRY-35`); a response with a non-error status that a hook forced to retry is simply disposed. When the retries run out on a
response, the stage policy returns it live and drops the trail.

## Timeouts

- **`OverallTimeout`** bounds the whole call, every retry and redirect hop included. When it expires, the call throws
  `OperationTimeoutException` (not retryable; the failed attempts' trail copied onto it). A call you cancelled still throws
  `OperationCanceledException` (`XCUT-1`).
- **`AttemptTimeout`** bounds one send. `RetryPolicy` arms it per attempt on its `TimeProvider`; an attempt that exceeds it is a
  retried `ServiceRequestTimeoutException` (`XCUT-2`). It is cooperative: a transport that ignores its token is not bounded, it
  covers the time until the response headers arrive (not your later body read), and it is not enforced when the pipeline has
  no `RetryPolicy`.

Both accept `null` or a value above zero and at most 49 days.

## Telemetry

The operation span records `dexpace.attempt.failed` for every retried failure, with the wait before the next attempt, and
`dexpace.retry.exhausted` when the call failed after the attempt cap was spent while the condition and the re-send gate held
and the effective count was above zero (`OBS-28`, `OBS-29`). `MaxRetryAttempts = 0` is "retries off", never "exhausted".

## Migrating from before phase 6a

| Before | Now |
|---|---|
| `MaxRetryAttempts = 3` default (four sends), `MaxDelay` 30 s | `2` (three sends), 8 s |
| full jitter over `[0, delay]` | symmetric jitter around the capped delay |
| `RetryNonIdempotentWhenReplayable = true` | removed: a replayable body is re-sent whatever the method |
| only `ServiceRequestException` / `ServiceResponseException` retried | the I/O family and `IRetryableError` too; the configured status set decides a wrapped `HttpResponseException` |
| `HonorRetryAfter` covered `Retry-After` in whole seconds | covers all four headers, fractional seconds included |
| a retried response's body disposed unread; no trail | drained (1 MiB) first; the trail is on the thrown exception |
| `RetryPolicy` sealed | unsealed with sealed overrides and two hooks |
| `AttemptTimeout` read by nothing | enforced by `RetryPolicy` |
| a response arriving after your token fired was returned | it is disposed and the call throws `OperationCanceledException` |
| `OverallTimeout` expiry threw `OperationCanceledException` | throws `OperationTimeoutException` |
| `OverallTimeout = TimeSpan.Zero` or negative meant "none" | rejected where set; use `null` |
| `new OperationPolicy()` | `new OperationPolicy(TimeProvider? = null)` (source-compatible) |
