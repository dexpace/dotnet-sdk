# Recovery: outcomes, steps, chains and the dispatcher

**As built by phase 4b, written against source on 2026-10-07** (branch `phase-4b-recovery`). This page describes the recovery layer
of `Dexpace.Sdk.Core`: the `Outcome` carrier, the three step contracts and both forms, the two chains, `RecoveryDispatcher`, the
error-mapping step and its 1 MiB cap, the two shipped request steps and the policy defaults they changed, and `ExceptionFacts` and
`ExceptionTrail`. The retry engine that will sit on top of it is phase 6a's. The requirement IDs cited are the normative text
(`docs/product-spec/08-execution-pipelines.md` §8.2 and appendix C); the decisions behind each change are in the
[phase 4b design](../work/mvp/phase4/phase4b/2026-10-05-phase4b-recovery-design.md) and
[design §5.2](../sdk-design-dotnet/05-pipeline-architecture.md). Neither is restated here.

## `Outcome`

`Outcome` is the closed result of a call attempt (`RECOV-1`): exactly one of `Outcome.Success(Response)` or
`Outcome.Failure(Exception)`. It is an abstract class with a private constructor and two nested sealed classes, so no outside type
can be a third variant.

```csharp
Outcome outcome = new Outcome.Failure(new IOException("reset"));

string text = outcome.Match(response => response.Status.ToString(), error => error.GetType().Name);

if (outcome.TryGetError(out var error)) { /* error is non-null here */ }
bool failed = outcome.IsFailure;
```

`ToString()` names the status (`Success(NOT_FOUND(404))`) or the exception type (`Failure(System.IO.IOException)`) and never a message.
Equality is by reference.

## The step contracts

| Contract | Transforms | Runs |
|---|---|---|
| `IRequestStep` | `Request` to `Request` | before the send, in `RequestRecoveryChain` (`RECOV-3`) |
| `IResponseStep` | `Response` to `Response`, or throws | only while the outcome is a success (`RECOV-4`) |
| `IRecoveryStep` | `Outcome` to `Outcome` | on every outcome, always, after the response phase (`RECOV-5`) |

Each declares both `Apply` and `ApplyAsync` (a value and a `CancellationToken`, nothing else) and both are required. One instance may
be applied concurrently by many calls, so keep per-call state in the value (`RECOV-14`). A recovery step SHOULD return
`new Outcome.Failure(...)` rather than throw (`RECOV-9`), and a step that returns a different outcome owns whatever it dropped, such as
the original response (`RECOV-13`).

## The chains

`RequestRecoveryChain` folds a request through its steps left to right; a throw aborts the rest and propagates. `ResponseRecoveryChain`
runs the response phase and then the recovery phase over an `Outcome` (`RECOV-6`):

- a throwing response step becomes a `Failure`, the remaining response steps are skipped, and the recovery steps still run
  (`RECOV-7`);
- a throwing recovery step becomes a `Failure` fed to the next recovery step (`RECOV-8`);
- a step that returns `null` is treated as having thrown `InvalidOperationException` naming the step's type;
- when a step throws while a `Success` is in hand, the response is released exactly once and a close failure is attached to the thrown
  exception's trail rather than replacing it (`RECOV-12`);
- the chain never throws except for a fatal exception (`OutOfMemoryException` and its subtypes), and never checks the token itself.

Both chains copy their step lists at construction (`RECOV-14`) and expose read-only views.

## `RecoveryDispatcher`

```csharp
var dispatcher = new RecoveryDispatcher(
    new RequestRecoveryChain([new IdempotencyKeyStep()]),
    new ResponseRecoveryChain([ErrorMappingStep.Instance], [new MyRecoveryStep()]));

Response response = await dispatcher.DispatchAsync(transport, request, RequestOptions.Empty, cancellationToken);
Response sync = dispatcher.Dispatch(syncTransport, request, RequestOptions.Empty, cancellationToken);
```

Every non-fatal exception from the request chain or the transport becomes a `Failure` the recovery steps observe (`RECOV-2`),
including `OperationCanceledException`: the token stays cancelled (`RECOV-11`). A transport that returns `null` is a failure with
`InvalidOperationException`. The terminal `Success` returns its response; the terminal `Failure` is rethrown with
`ExceptionDispatchInfo`, the same instance with no wrapper (`RECOV-10`). The transport is a per-call argument: the dispatcher owns
none, disposes none and holds no state, so one instance serves both transport forms.

## `ErrorMappingStep` and the 1 MiB cap

`ErrorMappingStep.Instance` maps only 400 to 599 to `HttpResponseException`, over a replayable copy of the body buffered by the one
error-body capture in core (`RECOV-15`, `RECOV-16`). The drain stops at `Response.MaxBufferedErrorBytes` (1 MiB) with a hard,
markerless truncation, and the original response is disposed whether or not the drain completed. Every other status is returned by
reference with its body never opened, read or disposed. `Response.EnsureSuccessAsync` uses the same capture.

## `IdempotencyKeyStep` and `ClientIdentityStep`

```csharp
var key = new IdempotencyKeyStep { Methods = new HashSet<Method> { Method.Post }, RespectExisting = false };
var identity = new ClientIdentityStep(["acme-app/2.1", "(linux)"]) { Mode = ClientIdentityMode.Append };
```

`IdempotencyKeyStep` (`RECOV-32`) stamps `Idempotency-Key` on POST, PUT and PATCH by default; with `RespectExisting` (the default) a request
that already carries the header passes through untouched and the strategy is not invoked; the strategy runs at most once per applicable
request. `ClientIdentityStep` (`RECOV-33`) joins its tokens with one space and, by default, appends the line after the first existing
`User-Agent` value, keeping every other value; `Replace` overwrites them all; a blank line emits no header. `IdempotencyPolicy` and
`ClientIdentityPolicy` delegate to these steps, on the same pipeline signature and stage as before.

## `ExceptionFacts` and `ExceptionTrail`

`ExceptionFacts.IsFatal(ex)` is the filter every SDK catch uses (`catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))`).
`ExceptionFacts.EnumerateCauses(ex)` yields the exception itself first, then its causes breadth-first through `InnerException` and
`AggregateException.InnerExceptions`, by reference identity, cycle-safe, to depth 64.

`ExceptionTrail.AddSuppressed(primary, secondary)` records a secondary failure without replacing the primary. An `SdkException` exposes it as
`Suppressed`; **a foreign exception's trail is readable only through `ExceptionTrail.GetSuppressed(ex)`** (its `ToString()` does not render
it). If a foreign exception's `Data` is read-only the secondary is dropped rather than thrown. Nothing is attached to or from a fatal
exception, to the primary itself, or twice.

## Breaking changes and migration

| # | Was | Now | To get the old behaviour |
|---|---|---|---|
| 1 | `IdempotencyPolicy` stamped POST only | stamps POST, PUT and PATCH | `new IdempotencyPolicy(new IdempotencyKeyStep { Methods = new HashSet<Method> { Method.Post } })` |
| 2 | `new IdempotencyPolicy(IEnumerable<Method>? methods = null)` | `new IdempotencyPolicy()` or `new IdempotencyPolicy(IdempotencyKeyStep)` | move the method set onto the step |
| 3 | `ClientIdentityPolicy` replaced a caller-supplied `User-Agent`, and stamped a blank one | appends the SDK line after the first existing value; a blank `UserAgent` emits no header | `new ClientIdentityPolicy(ClientIdentityMode.Replace)` |
| 4 | `SdkException.ToString()` was the base rendering | appends `(Suppressed Exception #n)` blocks when a trail exists | none needed |
| 5 | a dispose failure after a failed drain in `Response.EnsureSuccessAsync` replaced the drain's exception | it is attached to the drain exception's trail | none needed |
