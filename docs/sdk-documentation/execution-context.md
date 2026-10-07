# Execution context

As built by phase 4a, written against source on 2026-10-07. Namespace `Dexpace.Sdk.Core.Execution`.

## The three stages

A call's context is a one-way chain of three immutable records over the closed base `CallContext` (`CTX-1`, `CTX-2`):

```csharp
var dispatch = new DispatchContext();
var request  = dispatch.PromoteToRequest(sentRequest, operationName: "ListWidgets");
var exchange = request.PromoteToExchange(response);
// ... when the call is over
exchange.Close();
```

Each promotion returns a new link that carries the same `Key` and the same `InstrumentationContext` reference and adds
one artifact. `ExchangeContext` has no promotion member. `OperationName` is advisory, `null` or non-blank (`CTX-16`).

## The call key

`CallKey` is a trace id, a span id and a process-wide sequence number (`CTX-4`, `CTX-6`). It has no public constructor:
to pin one key across several contexts, mint it with `CallKey.Next()` or `CallKey.Next(in ActivityContext)` and pass it
to each constructor (`CTX-5`). `default(CallKey)` was never minted and is rejected.

## The correlation bundle

`InstrumentationContext` composes an `ActivityContext`, the active `Activity?` and a tracer factory over
`DexpaceDiagnostics.ActivitySource` (`CTX-14`, `CTX-20`). `InstrumentationContext.None` is the shared untraced default:
zero ids, empty trace state, no span, and a factory that returns `null` (`CTX-15`). Build real ones with `FromActivity`
or `FromContext`.

## Registration and `Close()`

Promotion registers the new link in the store the chain is bound to; constructing a context registers nothing (`CTX-17`).
`Close()` evicts a context only when it is the registered occupant, so closing an earlier link, a value-equal clone or an
already closed context is a no-op and never evicts the live one (`CTX-9`, `CTX-10`, `CTX-18`). Close the furthest-reached
link. A context is not `IDisposable`, and `Close()` does not dispose the `Response` it carries.

## The bounded store

The store holds at most 10 000 contexts, with arbitrary eviction, and holds them strongly (`CTX-11`-`CTX-13`, `CTX-19`).
A leaked `Response` therefore pins its context until the cap evicts it, and an undisposed body pins a connection.
`DexpaceCallContexts.TryGet(key, out context)` looks a call up; a miss does not prove the call never existed.

## Not shipped by 4a

The `CallKey` route to a transport `DelegatingHandler` (a `RequestOptions` carrier and an `HttpRequestOptionsKey<CallKey>`
stamp) is deferred to 4c and 8b (P4a-13), so `TryGet` has no production reader until then. The pipeline does not yet
create these contexts (4c).
