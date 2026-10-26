# Write a response handler

**As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). Ported from Node's [`write-a-response-handler.md`](./write-a-response-handler.md) (`nodejs-sdk`, `origin/main`) and restated for
`IResponseHandler<T>`. A response handler turns a `Response` into your model. Core ships two as factories and a lazy wrapper; write your own when none fits, which a generated SDK does for every operation
whose status codes mean something of its own.

```csharp
public interface IResponseHandler<T>
{
    ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken);
}
```

The one rule that shapes everything else: **a handler owns the response it is given.** It disposes it on every path, and its caller never does. Say so in the XML doc of any handler you write, because it is
the single fact a caller cannot recover from the signature.

## The shipped shapes

All three are in `Dexpace.Sdk.Core`.

| Shape | Use when |
|---|---|
| `ResponseHandlers.DeserializeOnSuccess<T>(serde)` | The common case: decode a 2xx, throw `HttpResponseException` for a 4xx or 5xx, and throw a `DeserializationException` for anything else (a 1xx, an unfollowed 3xx such as a 304) |
| `ResponseHandlers.Deserialize<T>(serde)` | You want the body decoded whatever the status: an RFC 9457 problem document, say |
| `new TypedResponse<T>(response, handler, token)` | You need the status, headers and request **alongside** the value, parsed lazily and once |

For a `Response` you hold yourself, `await response.Body.ReadValueAsync<T>(serde)` is the same streaming read without a handler. Streaming, disposal and the null rules are in [`serde.md`](./serde.md).

`DeserializeOnSuccess` throws for a 4xx or 5xx with the bounded, buffered copy of the error body (up to `Response.MaxBufferedErrorBytes`, 1 MiB) taken before the response was disposed, so the body is still
readable afterwards: `await ex.GetErrorAsync<Problem>(serde)`. The third branch leads its message with the status code and carries `ETag` and a redacted `Location`, so conditional and redirect context survives
the dispose (`SERDE-28`).

## Writing one

This handler decodes a body of one value per line. It is complete and compiled by the documentation build. Everything in it is a rule below.

```csharp
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;

await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

// The pipeline hands the response to the handler and disposes it afterwards in every outcome; disposal is latched, so the handler's own dispose is harmless.
var numbers = new LinesHandler<int>(int.Parse);
IReadOnlyList<int> values = await pipeline.SendAsync(
    Request.Get("https://api.example.com/numbers"), numbers.HandleAsync, RequestOptions.Empty, CancellationToken.None);

Console.WriteLine(values.Sum());

/// <summary>Decodes a body of one value per line. Owns, and disposes, the response it is given.</summary>
internal sealed class LinesHandler<T>(Func<string, T> parseLine) : IResponseHandler<IReadOnlyList<T>>
{
    public async ValueTask<IReadOnlyList<T>> HandleAsync(Response response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        Exception? primary = null;
        try
        {
            // ReadAsStringAsync drains the body and disposes it; the response is still ours to dispose below.
            string text = await response.Body.ReadAsStringAsync(cancellationToken);
            return [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(parseLine)];
        }
        catch (FormatException ex)
        {
            // Only a payload failure is re-typed (SERDE-12). An IOException from the stream propagates as itself.
            primary = new DeserializationException($"The body is not a list of {typeof(T).Name} values.", ex);
            throw primary;
        }
        catch (Exception ex)
        {
            primary = ex;
            throw;
        }
        finally
        {
            await DisposeAsync(response, primary);
        }
    }

    // A dispose failure never displaces the failure already in flight: it is attached to it. With none in flight it is the only failure there is.
    private static async ValueTask DisposeAsync(Response response, Exception? primary)
    {
        try
        {
            await response.DisposeAsync();
        }
        catch (Exception ex) when (primary is not null && !ExceptionFacts.IsFatal(ex))
        {
            ExceptionTrail.AddSuppressed(primary, ex);
        }
    }
}
```

## What the shipped handlers guarantee, and what you must reproduce

**1. The response is disposed on every path.** Success, a missing body, a codec failure, a stream failure, a cancelled token. A handler that returns early on one branch strands a connection, because a streamed
body holds its connection until the `Response` is disposed. Disposal is latched (`HTTP-43`): a `Response` releases at most once, so a handler that disposes and a caller that also does is safe.

**2. A dispose failure never displaces the real failure.** This looks like a one-liner and is not. The obvious code is wrong:

```csharp
// WRONG: when DisposeAsync throws while an exception is already in flight, the finally's exception REPLACES it. The caller is told their
// connection could not be released when in fact their payload was malformed.
try
{
    return await DecodeAsync(response, cancellationToken);
}
finally
{
    await response.DisposeAsync();
}
```

| Work | Dispose | Result |
|---|---|---|
| threw | ok | the work's exception propagates |
| threw | threw | the work's exception stays **primary**; the dispose failure is attached to its trail with `ExceptionTrail.AddSuppressed` and read back with `ExceptionTrail.GetSuppressed` (and rendered by `ToString()`) |
| ok | threw | a choice you make and document: the shipped handlers swallow it, because the value you asked for exists, and record it on the current `Activity` (`dexpace.dispose.suppressed`); the example above lets it propagate, because it is then the only failure there is |

The trail is not `InnerException`: the inner exception is the *cause* of the primary, and a dispose failure is not. A transport failure (an `IOException`) has no `Suppressed` property, so always read through
`ExceptionTrail`. Never swallow a **fatal** exception (`ExceptionFacts.IsFatal`, an `OutOfMemoryException`): the filter in the example above keeps it propagating.

**3. Only payload failures are re-typed** (`SERDE-12`). A malformed body or a shape mismatch becomes a `DeserializationException` with the original chained as `InnerException`; a genuine stream failure
(`IOException`, a connection reset mid-body) propagates unwrapped, because re-wrapping it tells a caller their payload was malformed when their socket dropped. `ReadValueAsync<T>` already behaves this way, which
is why a handler built on it needs no `catch` at all.

**4. Map statuses deliberately.** A 4xx or 5xx is an `HttpResponseException` (retryable per the status), unless your operation gives a code a meaning of its own. Map that one to your exception, then delegate
the rest to the core handler, so the buffering, the cap and the redaction stay core's:

```csharp
internal sealed class WidgetHandler(ISerde serde) : IResponseHandler<Widget>
{
    private readonly IResponseHandler<Widget> _inner = ResponseHandlers.DeserializeOnSuccess<Widget>(serde);

    public async ValueTask<Widget> HandleAsync(Response response, CancellationToken cancellationToken)
    {
        if (response.Status == Status.NotFound)
        {
            await using (response)   // this path does not delegate, so it disposes
            {
                throw new WidgetNotFoundException(response.Request.Url);
            }
        }

        return await _inner.HandleAsync(response, cancellationToken);
    }
}
```

The same type is built into a complete program in [`example-end-to-end.md`](./example-end-to-end.md). An exception message ends up in logs, so keep the URL's query, header values and credentials out of it
(`XCUT-19`): name the resource by its path, as above, not by `response.Request.Url.ToString()`.

**5. A handler that reads once, reads once.** `ResponseBody.FromStream` bodies are single-use: the first convenience read (`ReadAsBytesAsync`, `ReadAsStringAsync`, `ReadValueAsync`) disposes the body, so a
second read or open throws `StreamClosedException`. Buffer first (`ToReplayableAsync` on a request body, a copy of the bytes on a response body) when you must look twice, and do not inspect the body in a
logging statement before the decoder.

## When the answer is needed twice: `TypedResponse<T>`

`TypedResponse<T>` copies the metadata (`Request`, `Status`, `Headers`, `Protocol`, `ReasonPhrase`) at construction and parses the body **once**, on the first `GetValueAsync`. The handler you give it owns the
response on its own paths, so a wrapper whose value you never ask for must itself be disposed.

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(WidgetJson.Default));
await using var transport = new SystemNetHttpClient();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

using Response response = await pipeline.SendAsync(Request.Get("https://api.example.com/widgets/7"), CancellationToken.None);
using var typed = new TypedResponse<Widget>(response, ResponseHandlers.DeserializeOnSuccess<Widget>(serde), CancellationToken.None);

Console.WriteLine(typed.Status);                        // readable at once; the body is not touched
Widget widget = await typed.GetValueAsync();            // parses now
Widget same = await typed.GetValueAsync();              // the same instance; the handler did not run again
Console.WriteLine(ReferenceEquals(widget, same));

internal sealed record Widget(string Id, string Name);

[JsonSerializable(typeof(Widget))]
internal sealed partial class WidgetJson : JsonSerializerContext;
```

A success **and a failure** are memoized: a failure is thrown as the same exception object on every access. A caller's token cancels only that caller's wait, never the parse for the others. There is no
synchronous `TypedResponse`, because it would be sync-over-async; synchronous callers use `ReadValue<T>`.

## Two more things a response can be

**Server-sent events.** `ServerSentEventStream.FromResponse(response)` takes ownership of a streaming response from the call, even when it throws, and releases it once on every path; `MapAsync<T>` takes your
mapper, which returns `SseMapResult.Value(...)`, `SseMapResult.Skip` or `SseMapResult.Done`. Core's reader has no serde dependency, no done-sentinel and no reconnecting client (`SSE-37`, `SSE-38`); see [`sse.md`](./sse.md).

**A page.** [`write-a-paging-strategy.md`](./write-a-paging-strategy.md).

## Ownership, once more

The pipeline never disposes a response it hands you from `SendAsync`; `SendAsync<T>(request, handler, ...)` disposes it after your handler returns, in every outcome, and attaches a failing dispose to your handler's
exception. The core handlers and `ReadValueAsync` dispose because they read it. A handler you write must decide which it is and say so in its own XML doc.
