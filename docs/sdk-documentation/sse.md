# Server-sent events

**As built by phase 7b, written against source on 2026-10-09** (branch `80-phase-7b-sse`). This page describes how
`Dexpace.Sdk.Core` reads a `text/event-stream` response after phase 7b: the reader and the grammar it parses, the event value,
the streaming facade that owns a response, the typed adapter, the line cap, and what a caller writes to reconnect. The
requirement IDs it cites are the normative text (`docs/product-spec/13-server-sent-events-and-streaming.md`, `SSE-1` to `SSE-41`);
the decisions behind each type are in the [phase 7b design](../work/mvp/phase7/phase7b/2026-10-09-phase7b-sse-design.md) and in
[design §7.2](../sdk-design-dotnet/07-pagination-sse-and-serialization.md). Neither is restated here.

Everything is in the `Dexpace.Sdk.Core.ServerSentEvents` namespace. There is **no reconnecting client** and no request helper:
core parses the stream you hand it and reports the server's `retry` hint and each event's `id`; the loop that reconnects is yours
([below](#reconnecting-is-your-loop-sse-38)).

## The shapes

| Type | What it is |
|---|---|
| `ServerSentEvent` | One event: `Id`, `Event`, `Data` (a list), `Comment`, `Retry`; immutable, with value equality |
| `ServerSentEventReader` | Reads events from a `Stream` you own: `ReadNextAsync` / `ReadNext`, and the lazy `ReadAllAsync` / `ReadAll` views |
| `ServerSentEventStream` | Owns one `Response`: `FromResponse`, then one of four single-use views, and a release rule for every way to stop |
| `SseMapResult<T>`, `SseMapResult`, `SseMapResultKind` | The mapper's verdict for the typed adapter: `Value`, `Skip` or `Done` |
| `ServerSentEventLineTooLongException` | A `StreamingException`: a line passed the cap |

## A response, end to end

`FromResponse` takes the response a pipeline returned. **It owns that response from the call, success or failure** (`SSE-32`):

```csharp
var request = Request.Create(Method.Get, "https://api.example.com/events",
    new Headers.Builder().Add("Accept", "text/event-stream").Build());   // setting Accept is yours: core adds nothing

await using var events = ServerSentEventStream.FromResponse(await pipeline.SendAsync(request, cancellationToken));
await foreach (var ev in events.WithCancellation(cancellationToken))
{
    Console.WriteLine($"{ev.Event ?? "(no name)"}: {string.Join("\n", ev.Data)}");
}
```

If the response has no body to stream, `FromResponse` throws `ArgumentException` (`ParamName` `response`) **after disposing the response**,
so the one-line shape above never leaks it. "No body" is a status of `204`, `205` or `304`; a `HEAD` request; a `Content-Length` of
zero; or an absent body. The message names the reason and never the URL. There is no status or `Content-Type` check: the default
pipeline's error mapping already throws on a `4xx` or `5xx`, servers mislabel event streams, and a convention like `Accept` stays out
of core (`SSE-37`). An invalid `maxLineBytes` also disposes the response first. Nothing is read by `FromResponse` itself: the body
opens at the first pull, so a stream you never enumerate only releases its response.

### One stream, four views, one pass

| View | Call | Opens the body through |
|---|---|---|
| `await foreach` | `events.GetAsyncEnumerator()` / `await foreach (… in events)` | `ResponseBody.OpenReadAsync` |
| blocking `foreach` | `events.AsEnumerable()` | `ResponseBody.OpenRead` |
| typed, asynchronous | `events.MapAsync<T>(mapper)` | `ResponseBody.OpenReadAsync` |
| typed, blocking | `events.Map<T>(mapper)` | `ResponseBody.OpenRead` |

The four share **one** latch (`SSE-26`). The first view taken wins; a second throws `InvalidOperationException` at the call, not at the
first pull, and asking for any view after the stream is closed throws `ObjectDisposedException` (which derives from
`InvalidOperationException`, so one `catch` sees both; `SSE-27`). `AsEnumerable`, `MapAsync` and `Map` return a lazy sequence, and the
latch is taken when its enumerator is requested, so `var typed = events.MapAsync(f);` costs nothing until it is enumerated.

The facade implements `IAsyncEnumerable<ServerSentEvent>` and deliberately **not** `IEnumerable<ServerSentEvent>`: a type with both makes
every LINQ call on it ambiguous (`CS0121`, verified in the phase 7b pre-flight on SDK 10.0.401, because `System.Linq` carries
`System.Linq.AsyncEnumerable`), and would let `foreach` versus `await foreach` pick the path silently. The blocking view is
`AsEnumerable()`, a genuinely synchronous walk over `Stream.Read`. **Do not call `ToBlockingEnumerable` on the facade**: that adapter
blocks a thread on the asynchronous view, which is the sync-over-async the blocking view exists to avoid. Over the reference
transport the blocking views need `HttpResponseMessageBody.OpenRead`, which phase 7b added (it was left to phase 8b by P3a-5); a
third-party `ResponseBody` must override `OpenRead` for them, or they throw `NotSupportedException`.

## What an event is

```csharp
public sealed record ServerSentEvent
{
    public string? Id { get; init; }                  // null when the block set no id
    public string? Event { get; init; }               // raw; null when absent, never "message"
    public IReadOnlyList<string> Data { get; init; }  // never null; empty when no data line; one entry per data line
    public string? Comment { get; init; }
    public TimeSpan? Retry { get; init; }             // whole milliseconds, 0 to int.MaxValue
    public bool IsEmpty { get; }
}
```

**Present-but-empty is not absent** (`SSE-4`): `event:` gives `Event == ""`, a colon-less `data` gives `Data == [""]`, and a block with no
`data` line at all gives `Data == []`. Those are different events and compare unequal. `IsEmpty` is true only when `Id`, `Event`,
`Comment` and `Retry` are `null` and `Data` is empty (`SSE-22`), so a comment-only keep-alive is not empty.

**Data is not joined** (`SSE-8`): it is the list of `data` values in wire order. The typed adapter joins them; nothing else does.

**An event is immutable** (`SSE-20`): the `Data` setter copies the list it is given into a private read-only collection, and
`ev with { Data = list }` copies again, so a mutation of your list never reaches the event. The setters also refuse what the parser
could never produce: an `Id` containing U+0000 and a `Retry` that is negative, not a whole number of milliseconds or above
`int.MaxValue` milliseconds throw (`ArgumentException` / `ArgumentOutOfRangeException`, never echoing the value). Build one with an
object initializer or `with`. Equality is hand-written because a record compares a collection by reference (`SSE-21`): `Data` is compared
element by element and every string ordinally. `ToString()` is lossless (strings quoted and escaped, `null` as `null`, the retry as
`5000ms`), so two unequal events never print alike; the SDK never logs an event.

## The grammar, in order

Per line, after the line layer (below):

1. an empty line ends the block: if any field was set, one event is dispatched and **every** accumulator resets; otherwise nothing is
   (`SSE-1`, `SSE-13`);
2. a line starting `:` is a comment: the text after the colon with one leading space stripped, latest wins, and it counts as a field set
   (`SSE-6`), so a comment-only block is an event;
3. otherwise the line is split at its **first** colon; no colon means the whole line is the name and the value is empty; exactly one leading
   `U+0020` is stripped from the value (`SSE-3`, `SSE-5`);
4. only `data`, `event`, `id` and `retry` are read, ordinally and case-sensitively, so `Data:` is an unknown field (`SSE-7`). `data` appends;
   `event` and `id` are latest-wins and stored raw; an `id` containing U+0000 is ignored entirely; a `retry` is ignored unless it is one or more
   ASCII digits not above `2147483647`, so `-5`, `+5`, `1.5`, ` 5`, an Arabic-Indic digit and a twenty-digit value are all dropped and never wrap
   (`SSE-9`, `SSE-10`, `SSE-11`). **Only a field that is set marks the block as seen**: a block of ignored fields is skipped, and an invalid
   `retry` does not overwrite an earlier valid one in the same block.

At the end of the stream a pending block is dispatched as one last event, and every call after that returns `null` (`SSE-14`, `SSE-15`). No
last-event-id is carried from one event to the next (`SSE-16`); `id: 1` then a block with no `id` yields `Id == null` the second time.

### The line layer

Lines come from the SDK's own byte-level `Utf8LineReader` in WHATWG mode (`SSE-2`, `IO-14`): LF, CR and CRLF each end a line, **a CR ends the
line at once** without waiting for a following LF, and a CRLF split across two reads is still one terminator. That is why an event ended by
`CR CR` reaches you before the server writes anything more; `StreamReader.ReadLine` would hold it until the next chunk (design §7.2, and
`ServerSentEventWireTests.A_CR_terminated_event_is_delivered_before_the_server_sends_more` proves it on a socket). Malformed UTF-8 decodes to U+FFFD; an
unterminated final line is content (`SSE-14`).

A leading byte-order mark is consumed **once**, by the reader's one persistent flag, and only when the stream's first three bytes are `EF BB BF`
(`SSE-12`): a doubled BOM keeps the second as data (so the line is an unknown field), a BOM at the start of any later line is never stripped, and
`EF BB` alone is content. The three BOM bytes count against the first line's cap.

## The line cap (`SSE-19`, issue #10)

A hostile or broken server can stream a line that never ends. The reader holds at most `maxLineBytes` content bytes of a line, **1 MiB by default**
(`ServerSentEventReader.DefaultMaxLineBytes`); over that it throws `ServerSentEventLineTooLongException` (a `StreamingException`, so `catch
(StreamingException)` and `catch (SdkException)` see it) after reading at most one cap plus two 4 KiB buffers, and the reader **stays failed**: the source is
mid-line, so every later pull throws it again. The message names the cap and never a byte of the line; `MaxLineBytes` carries it. Change it with the
constructor's `maxLineBytes`, `ReadAll{,Async}`'s, or `FromResponse`'s. This is the divergence the chapter's MAY licenses and design §10 entry 20 records;
`Security/ServerSentEventLineCapTests` pins it permanently.

**Residual R1: the cap bounds a line, not an event.** Lines that each fit are accumulated into the pending block without a limit until a blank line ends
it, so a server that sends `data:` lines forever and never a blank line can still grow the block. Bound a stream from an untrusted server with the
enumeration token or `OverallTimeout` (`AttemptTimeout` covers only the time to the response headers, so a live stream is not cut by it). A second, per-event cap is
an optional-parameter addition later; the design left it open for the lead (P7b-5).

## Releasing the response

The facade owns the response and releases it **exactly once**, whichever of these comes first, and every later call is a no-op (`SSE-23`, `SSE-28`): the
stream ends, the enumerator is disposed, a read fails, the typed adapter's `Done`, or an explicit `Dispose` / `DisposeAsync`. The body stream is disposed first and
the response second; both always run (P7b-11). What happens to a **failure while releasing** depends on the path (P7b-12):

| Path | A release failure | Rationale |
|---|---|---|
| the stream ends (`SSE-24`) | reported out of band, swallowed | a clean terminal path (`SSE-30`) |
| the typed adapter's `Done` (`SSE-34`) | reported out of band, swallowed | `SSE-30` names the done-sentinel explicitly |
| the enumerator is disposed early: `break`, an exception in your loop body, `FirstAsync` (`SSE-25`) | reported out of band, swallowed | the enumerator cannot know your exception is in flight, and a throw there would replace it |
| a read, the line cap or a cancellation fails mid-stream (`SSE-29`) | attached to that exception (`ExceptionTrail.GetSuppressed`), which is rethrown **unchanged** | the primary stays the primary |
| the mapper throws (`SSE-36`) | attached to the mapper's exception, which is rethrown unchanged | the same |
| explicit `Dispose()` / `DisposeAsync()` (`SSE-30`) | **propagates**; the stream's failure is primary and the response's is attached to it | you asked |
| any call after the first release (`SSE-28`) | nothing happens | the latch |

"Reported out of band" is the SDK's one channel for a failure it must not throw: an `exception` event on `Activity.Current` tagged `dexpace.dispose.suppressed`
and, when `FromResponse` was given a logger, the `dexpace.dispose.suppressed` warning (event id 130) carrying the resource and exception *type* names, never a
message. **Residual R3:** with no logger and no activity listener such a failure is reported to nobody; pass an `ILogger` to `FromResponse` if you want it.
The release table is pinned as one theory, `ServerSentEventStreamReleaseTableTests`.

A compiler-generated enumerator that you abandon without disposing never runs its `finally`, so the facade does not rely on it: `await using var events = …`
releases the response even when you enumerated by hand and walked away.

### Closing from another thread (`SSE-31`)

`Dispose` and `DisposeAsync` are safe to call from a thread other than the one iterating, to cancel a long-lived stream. A close observed **between pulls** ends
the iteration cleanly. A close that tears the stream down **while a read is in flight** surfaces from that read as an `IOException` ("The event stream was closed
while a read was in flight.") whose inner exception is the failure the teardown caused (an `ObjectDisposedException` or an `OperationCanceledException`), and the
response is still released once. The closing call owns that release: it takes it before it cancels anything, so the read it tears down can never release the response
ahead of it. A release failure therefore propagates from `Dispose` / `DisposeAsync` as the table says, and the response is already released when that call returns (unless
an earlier release, the stream ending or an earlier close, had already taken it, `SSE-28`). The facade cancels an internal token every read observes, so a cooperative stream unblocks at once and a transport's stream
unblocks when disposal tears it down. **Residual R2:** a stream that honours neither stays blocked until data or the end of the stream arrives. Cancelling the
enumeration token yourself is different: that surfaces as `OperationCanceledException` and is never rewritten to `IOException` (`XCUT-1`). Beyond this one call
the types are single-threaded (`SSE-18`): two threads pulling one reader or one stream is undefined.

## The typed adapter

`MapAsync<T>` / `Map<T>` take a synchronous mapper `Func<string?, string, SseMapResult<T>>`: the event's name (`null` when absent) and its data lines joined with
`"\n"` (the empty string when there are none; `SSE-33`). The sentinel is **yours**; core names none (`SSE-37`):

```csharp
await foreach (var chunk in events.MapAsync<Chunk>((name, data) =>
    data == "[DONE]"
        ? SseMapResult.Done
        : data.Length == 0 ? SseMapResult.Skip : SseMapResult.Value(JsonSerializer.Deserialize(data, ChunkContext.Default.Chunk)!)))
{
    Handle(chunk);
}
```

| Verdict | Effect (`SSE-34`) |
|---|---|
| `SseMapResult.Value(x)` | `x` is yielded |
| `SseMapResult.Skip` | the adapter advances silently to the next raw event |
| `SseMapResult.Done` | the stream is released (the quiet path) and the sequence completes **without** a model; nothing after the sentinel is read, parsed or mapped |

`Skip` and `Done` are non-generic and convert implicitly to any `SseMapResult<T>`. **`default` is not a verdict**: its `Kind` is `0`, and a mapper that returns
`default(SseMapResult<T>)` fails the call with `InvalidOperationException`, after the usual release, instead of silently skipping every event or silently ending the
stream. Converting a `default(SseMapResult)` to a model type is caught earlier, inside the mapper: the implicit conversion throws `ArgumentException`, which the adapter
treats like any other mapper throw (release, then the same exception unchanged, `SSE-36`). The adapter is lazy (`SSE-35`): nothing is read, opened or mapped until the
first pull, and the mapper runs once per raw event pulled, only inside a pull. A mapper that throws releases the stream first and then propagates unchanged, with any
release failure attached (`SSE-36`). The mapper is synchronous because the data is already in memory; an asynchronous mapper would invite I/O inside the pull.

## The reader on its own

`ServerSentEventReader` parses a stream you own; it never closes, disposes or reads past what it needs from it (`SSE-17`, `SSE-39`), so it is not `IDisposable`.

```csharp
var reader = new ServerSentEventReader(stream);            // maxLineBytes defaults to 1 MiB
while (await reader.ReadNextAsync(cancellationToken) is { } ev) { … }   // null is the end, and stays the end

await foreach (var ev in ServerSentEventReader.ReadAllAsync(stream)) { … }   // lazy, single-use, per-stream BOM
foreach (var ev in ServerSentEventReader.ReadAll(stream)) { … }
```

`ReadNext` has no token because `Stream.Read` has none. The `ReadAll` views validate their arguments at the call, read nothing until enumerated, build one reader
at their first pull, surface a read error at the pull that hit it after the events already parsed, and throw on a second enumeration. **Do not call `ReadAll` twice over
the same source** (`SSE-40`): a second reader would start mid-stream. The reader is pull-based (`SSE-39`): the 4 KiB buffer is a read-ahead of bytes, never of events.

## Reconnecting is your loop (`SSE-38`)

A reconnecting client would have to make decisions core refuses to: which exceptions mean "try again", how long to back off, what a changed `Last-Event-ID` does to your
request. There is none. The SDK hands you what a loop needs (`Retry`, each event's `Id`) and the loop is a few lines:

```csharp
string? lastEventId = null;
var delay = TimeSpan.FromSeconds(3);
while (!cancellationToken.IsCancellationRequested)
{
    var headers = new Headers.Builder().Add("Accept", "text/event-stream");
    if (lastEventId is not null)
    {
        headers.Add("Last-Event-ID", lastEventId);
    }

    try
    {
        var response = await pipeline.SendAsync(Request.Create(Method.Get, url, headers.Build()), cancellationToken);
        await using var events = ServerSentEventStream.FromResponse(response);
        await foreach (var ev in events.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(ev.Id)) { lastEventId = ev.Id; }
            if (ev.Retry is { } hint) { delay = hint; }
            Handle(ev);
        }
    }
    catch (Exception ex) when (ex is IOException or ServiceRequestException or ServiceResponseException)
    {
        // the connection dropped or could not be made: fall through and reconnect
    }

    await Task.Delay(delay, cancellationToken);
}
```

Nothing in core sets `Last-Event-ID`, calls a transport, or reconnects; `Sse38ArchitectureTests` fails the test run if a core type holds that header name or an SSE type
refers to a transport or `HttpPipeline`.

## Not built

- **A reconnecting client and a request helper** (`SSE-38`, P7b-18): the recipe above is the whole of it.
- **A strict-WHATWG mode** (design §11 item 17): `System.Net.ServerSentEvents.SseParser` dispatches nothing for a comment-only or id-only block, joins `data` lines, defaults the type to `message` and
  keeps a last-event-id, each of which a MUST forbids, so core ships its own grammar. An `SseItem<T>` bridge is a possible later convenience.
- **A reactive adapter** (`SSE-41`, design §11 item 21): `IObservable<T>` over this would be an adapter package, listed in [`first-release.md`](../first-release.md); `SSE-41` is not applicable until one ships.
- **An aggregate cap on one event** (residual R1, above).
