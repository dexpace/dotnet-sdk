# The seams

**As built by phase 2b, written against source on 2026-10-02** (branch `40-phase-2b-seams`). This page describes the
three seams of `Dexpace.Sdk.Core` — the transport, the codec and the operation-input projection — as they exist after
the seams rework. The requirement IDs it cites are the normative text
(`docs/product-spec/03-pluggable-seams-and-extension-model.md`); the decisions behind each type are in the
[phase 2b design](../work/mvp/phase2/phase2b/2026-09-30-phase2b-seams-design.md) and in
[design §3](../sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md). Neither is restated here.

Core ships **adapters over** a seam (`DelegateHttpClient`, the two bridges, `BuildRequest`) but no implementation **of**
the external concern behind one: no transport, no codec (`SEAM-1`, `SEAM-2`).

## The transport seam

```csharp
public interface IAsyncHttpClient : IAsyncDisposable
{
    Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
}

public interface IHttpClient : IDisposable
{
    Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken);
}
```

- **One request, one response** (`SEAM-11`). The body is never pre-buffered; the caller reads and disposes it. Options
  are optional and ignorable: a transport that ignores them behaves exactly as it does for `RequestOptions.Empty`.
  `SystemNetHttpClient` ignores them until phase 8b.
- **No parameter defaults.** A default on an implementation's three-parameter member would let
  `transport.ExecuteAsync(request, default)` bind it with `null` options. The option-less calls are extensions in
  `Dexpace.Sdk.Core.Client`: `client.ExecuteAsync(request, token)` and `client.Execute(request, token)` pass
  `RequestOptions.Empty`, so `using Dexpace.Sdk.Core.Client;` keeps the familiar call shape.
- **Concurrency** (`SEAM-12`). Both are safe for concurrent calls; a transport keeps per-call state in locals.
- **Cancellation** (`SEAM-13`). The call's token is the only cancellation channel. Cancelling after the response was
  delivered does not dispose it. Honouring the token inside blocking I/O needs phase 8b's real synchronous send.
- **Never `null`** (`SEAM-16`, `ASYNC-2`). The async seam returns `Task<Response>` (`SEAM-17`), never `null`, and a
  `null` argument is an `ArgumentNullException` delivered through the returned task. The pipeline's runner converts a
  transport that returns `null` anyway into a `PipelineAbortedException` before any policy sees it.
- **Lifecycle** (`SEAM-14`, `SEAM-15`). Dispose is ownership-aware: `SystemNetHttpClient` disposes the `HttpClient` it
  created and never one it was given. What a call does after dispose is implementation-defined, and each SDK type
  documents its own: a bridge and a `DelegateHttpClient` keep working (they hold nothing); an owned `HttpClient`
  throws `ObjectDisposedException`. The SDK-owned idempotence latch is phase 3b's.

### `DelegateHttpClient`

A bare send function is a transport (`SEAM-11`):

```csharp
IAsyncHttpClient transport = DelegateHttpClient.Create((request, options, ct) =>
    Task.FromResult(new Response(request, Status.Ok, Protocol.Http11)));
IHttpClient blocking = DelegateHttpClient.CreateBlocking((request, options, ct) =>
    new Response(request, Status.Ok, Protocol.Http11));
```

The factories are `Create` and `CreateBlocking`, not two overloads of `Create`, because a lambda that only throws is
ambiguous between them. Every failure of the async form — a `null` argument, a throw from the function before it returns
a task, a `null` task, a `null` result — arrives through the returned task. The blocking form throws.

### The two bridges (`SEAM-18`)

- **`AsAsync(this IHttpClient, TaskScheduler)`** runs each blocking call on the scheduler *you* choose; there is no
  overload without one, because blocking calls starve a shared pool. Each call is one task started with `LongRunning`
  and `DenyChildAttach`. `TaskScheduler.Default` is accepted and then gives each call a dedicated thread, which costs one
  thread per call; a custom scheduler receives `LongRunning` as a hint it may ignore. The caller's `AsyncLocal<T>` values,
  `Activity.Current` and logging scopes flow to the worker (`SEAM-24`). Cancellation maps both ways through the one token.
  A blocking call cannot be interrupted, so cancellation is cooperative.
  **No orphaned response** (`SEAM-30`): a response produced after the token was signalled is disposed and the call
  completes cancelled, so awaiting through `task.WaitAsync(token)` and walking away cannot leak one.
- **`AsBlocking(this IAsyncHttpClient)`** blocks with `GetAwaiter().GetResult()`, so a failure surfaces as itself. It is
  a last resort: under a `SynchronizationContext` that runs one callback at a time, a wrapped transport whose awaits do
  not use `ConfigureAwait(false)` can deadlock. It does not hop to the pool to avoid that.
- **Neither disposes what it wraps** (`SEAM-14`, `SEAM-25`), and neither owns the scheduler. Dispose the transport
  yourself.

## The codec seam

`ISerde` has two encode primitives (a `Stream` and an `IBufferWriter<byte>`) and three decode primitives (an asynchronous
`Stream`, a UTF-8 `ReadOnlySpan<byte>`, and, since phase 7a, a synchronous `Stream` with a bounded default implementation; see
[serde.md](./serde.md)). The codec declares its `DefaultMediaType`, which `RequestBody.FromValue` stamps when you pass
none (`SEAM-19`). The target type is the generic argument, which the runtime binds to a closed type, so no open generic
reaches a codec and the seam takes no `System.Type` (`SEAM-22`).

`SerdeExtensions` adds the other profiles once, over any codec (`SEAM-20`):

```csharp
byte[] bytes = serde.SerializeToUtf8Bytes(value);   // a fresh array per call
string text  = serde.SerializeToString(value);       // UTF-8, or the codec's own string if it is an IStringSerde
int written  = serde.Serialize(buffer.AsSpan(2), value); // throws ArgumentOutOfRangeException, destination untouched, if it does not fit
```

A codec whose wire format is not UTF-8 text implements the optional `IStringSerde` to supply its own string. A binary
codec has no meaningful string form and does not implement it.

**Failures** (`SEAM-23`). Encode failures are `SerializationException` and decode failures `DeserializationException`,
both unsealed under the abstract `SerdeException`, so one handler can catch any serde failure and codegen can add more
specific subtypes. The codec's own exception is chained as `InnerException`. A failure of the stream itself
(`IOException`) propagates unwrapped, and a codec leaves a caller-supplied stream open (`SEAM-21`).

## The operation-input projection

`OperationDescriptor` is what generated code declares for one operation (`SEAM-26`):

```csharp
var descriptor = new OperationDescriptor
{
    Method = Method.Get,
    PathTemplate = "/pets/{id}",
    PathParameters = ImmutableDictionary<string, string>.Empty.Add("id", "a/b"),
    Query = new Query.Builder().Add("limit", "10").Build(),
};

Request request = descriptor.BuildRequest(new Uri("https://host/c?sig=abc"));
// GET https://host/c/pets/a%2Fb?sig=abc&limit=10
```

- `Method` and `PathTemplate` are `required`; everything else defaults to empty. Validation lives in the `init`
  accessors, so `with` cannot bypass it. `Body` is carried by reference and never encoded.
- A **template** is literal text and `{name}` placeholders (any non-empty text without braces, compared ordinally; a
  repeated name takes one value). Outside placeholders it must be an RFC 3986 path, so `?`, `#`, a space and a bare `%`
  are rejected, and so is a literal dot-segment (`.` or `..`, `%2E` read as `.`), which `System.Uri` would remove.
- A **path value** is encoded as one segment, so `a/b` becomes `a%2Fb`. A value of exactly `.` or `..`, or one with a lone
  surrogate, is rejected naming the key, never the value. Formatting a non-string value is the generator's job, with the
  invariant culture.
- **`BuildRequest`** composes over the base's components, never `new Uri(base, relative)` (`SEAM-27`): exactly one
  separator between the base path and the template, an empty template leaves the base path untouched, the base query is
  kept (a dangling `&` dropped) with the operation query appended, and user info and port are kept. A base that is
  relative, not `http`/`https`, or carries a fragment (even an empty `#`) is an `ArgumentException` whose message carries
  the base through `UrlRedactor`. A placeholder with no value, a parameter naming no placeholder, or a rendered
  dot-segment is an `InvalidOperationException` naming it and never a value.
- `BuildRequest(DexpaceClientOptions)` reads `BaseAddress`, which is no longer inert.
- `OperationId` (`SEAM-28`) never reaches the URL, headers or body; attaching it to the context chain is phase 4a's.

## Migrating from before phase 2b

Each change is marked **Breaking** in the XML docs of the member it changes and in `CHANGELOG.md`.

| # | Before | Now |
|---|---|---|
| 1 | `SerializationException` and `DeserializationException` were `sealed`, deriving from `SdkException` | Unsealed, deriving from the abstract `SerdeException`. Source-compatible at every throw and catch site; binary-incompatible if compiled against the sealed types |
| 2 | `IHttpClient.Execute(Request)` | `Execute(Request, RequestOptions, CancellationToken)`, no defaults. Callers use the option-less `client.Execute(request, token)` extension; every implementer adds the parameters |
| 3 | `IAsyncHttpClient.ExecuteAsync(Request, CancellationToken = default)` | `ExecuteAsync(Request, RequestOptions, CancellationToken)`, no defaults. Callers that import `Dexpace.Sdk.Core.Client` keep calling `ExecuteAsync(request, ct)` through the extension |
| 4 | `SystemNetHttpClient.Execute(Request)` / `ExecuteAsync(Request, CancellationToken = default)` | The same three-parameter signatures; add `using Dexpace.Sdk.Core.Client;` for the two-argument call |
| 5 | `AsAsync(this IHttpClient)` offloaded to the thread pool, and both bridges disposed what they wrapped | `AsAsync(this IHttpClient, TaskScheduler)` runs on your scheduler; neither bridge disposes the client it wraps, so dispose it yourself |
| 6 | A response `AsAsync` produced after the token fired was returned | It is disposed, and the call completes cancelled |
| 7 | A transport returning `null` reached the policies as a `null` response, and `HttpPipeline` threw at the end | `PipelineRunner` throws `PipelineAbortedException` before any policy sees it |

Additive: `SerdeException`, `IStringSerde`, `SerdeExtensions`, the option-less `Execute` / `ExecuteAsync` extensions,
`DelegateHttpClient`, `OperationDescriptor` and the `BaseAddress` read.

## What is not built yet

The per-transport proofs of `SEAM-11` (no pre-buffering), `SEAM-12` (concurrency) and `SEAM-13` (async abort) are phase
8a's conformance kit; the real synchronous send, the `ObjectDisposedException` latch and `RequestOptions.Timeout` are
phase 8b's; the SDK-owned dispose latch and `DisposeQuietly` are phase 3b's; attaching `OperationId` to the context chain
is phase 4a's; the DI half of `SEAM-5` and `SEAM-6` is phase 9's.
