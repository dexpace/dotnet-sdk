# Serialization (serde)

**As built by phase 7a, written against source on 2026-10-09** (branch `79-phase-7a-serde`). This page describes how
`Dexpace.Sdk.Core` and `Dexpace.Sdk.Serialization.SystemTextJson` read and write typed values after phase 7a: the codec seam, the
three-state `Tristate<T>`, the System.Text.Json adapter and its default options, the typed body readers, the two response
handlers, and the lazy `TypedResponse<T>`. The requirement IDs it cites are the normative text
(`docs/product-spec/14-serialization-serde.md`, `SERDE-1` to `SERDE-30`; `HTTP-44` and `HTTP-45` in
`docs/product-spec/06-request-and-response-body-lifecycle.md`); the decisions behind each type are in the
[phase 7a design](../work/mvp/phase7/phase7a/2026-10-09-phase7a-serde-design.md) and in
[design §3.4 and §7.3](../sdk-design-dotnet/07-pagination-sse-and-serialization.md). Neither is restated here.

The seam itself (the four profiles, the failure hierarchy) is in [seams.md](./seams.md); this page covers what sits on it.

## The codec seam, in one place

`ISerde` has **six members**: `DefaultMediaType`, two encode primitives and three decode primitives.

| Member | What it is |
|---|---|
| `MediaType DefaultMediaType` | the codec's own media type, stamped by `RequestBody.FromValue` when you pass none (`SERDE-2`); no default implementation |
| `SerializeAsync<T>(Stream, T, CancellationToken)` | encode to a caller's stream; the codec leaves it open (`SERDE-3`) |
| `Serialize<T>(IBufferWriter<byte>, T)` | the synchronous encode primitive |
| `DeserializeAsync<T>(Stream, CancellationToken)` | decode from a caller's stream; leaves it open |
| `Deserialize<T>(ReadOnlySpan<byte>)` | decode from a UTF-8 buffer |
| `Deserialize<T>(Stream)` | the **synchronous** stream decode, a *default interface member* (P7a-13) |

The sixth, `Deserialize<T>(Stream)`, has a default so that every existing implementer keeps compiling and loading: the default
**materialises**, reading the stream to the end under `ResponseBody.DefaultMaxMaterializedBytes` (64 MiB) and calling the span
decode. A seekable stream that declares more than the cap is refused with `BodyTooLargeException` before any read; an
`IOException` from the stream propagates unwrapped. A codec with a streaming synchronous decoder overrides it; `SystemTextJsonSerde`
does. It takes no `CancellationToken` (a synchronous stream decode has no cancellation point of its own), and there is no synchronous
stream *encode*: `Serialize(IBufferWriter<byte>, T)` is the synchronous primitive.

The seam stays `Type`-free (`SEAM-22`, `SERDE-8`): the target is the generic argument, bound to a closed type at run time, so
`ReadValueAsync<List<Dto>>` carries `List<Dto>` and an open generic can never reach a codec (`SERDE-5` to `SERDE-7`).

## `Tristate<T>`: PATCH without ambiguity

A JSON PATCH needs three things a nullable cannot say: leave the field alone, clear it, set it. `Tristate<T>` (in
`Dexpace.Sdk.Core.Serialization`) is a `readonly struct` with exactly those states (`SERDE-14`):

| State | On the wire | Reads as |
|---|---|---|
| `Absent` | the key is omitted | `default(Tristate<T>)`, so a field with no initializer is Absent (`SERDE-17`) |
| `Null` | `"name": null` | the literal `null` |
| `Present` | `"name": "gear"` | a non-null value |

```csharp
public sealed record WidgetPatch(Tristate<string> Name, Tristate<int> Size, Tristate<string> Note);

var patch = new WidgetPatch(Name: Tristate.Null, Size: Tristate.Present(3), Note: default);   // {"name":null,"size":3}
var untouched = patch with { Name = Tristate.Absent };                                          // {"size":3}
Tristate<string> viaImplicit = "gear";   // Present("gear")
Tristate<string> cleared = (string?)null;   // Null: an implicit conversion from null is Null, and it never throws (P7a-2)
```

- **Construction.** `Tristate<T>.Absent`, `.Null`, `.Present(value)` (throws `ArgumentNullException` for `null`, so the fourth state,
  Present-holding-null, cannot exist) and `.FromNullable(value)` (null is Null, anything else Present, never Absent). The
  non-generic `Tristate` class infers `T` (`Tristate.Present("a")`, `Tristate.FromNullable(int?)`), and its `Absent` / `Null` return a
  type-less marker, `TristateSentinel`, that converts to *every* `Tristate<T>`: that is the stand-in for the covariance C# cannot give a
  struct (`SERDE-14`'s SHOULD is unmet, design §10 entry 21).
- **Reading.** `State` (a `TristateState` enum, so a `switch` is exhaustive up to the compiler's unnamed-value arm), the three `IsAbsent`
  / `IsNull` / `IsPresent` predicates, `Value` (throws `InvalidOperationException` naming the state unless Present), `TryGetValue`,
  `GetValueOrDefault()` and `GetValueOrDefault(fallback)`, `Tristate.GetValueOrNull()` for a value-type `T`, and
  `Match(onAbsent, onNull, onPresent)`. Pattern matching works on the predicates: `t is { IsPresent: true, Value: var v }`.
- **Equality and text.** Equality is by state and, for Present, by value. `ToString()` is `Absent`, `Null` or `Present(<value>)`
  (`SERDE-30`; it is hand-written, because a record struct would print its private fields).
- **No codec attribute.** The struct carries no System.Text.Json attribute (an architecture test pins it); a codec adapter reaches the
  closed type through the public `ITristate` / `ITristateVisitor<TResult>` pair, which needs no `MakeGenericType` and so is
  NativeAOT-safe. A future Newtonsoft adapter would use the same door.

`T : notnull` applies: `Tristate<string?>` is a nullability warning in your build. RFC 7386 merge-patch documents are out of scope.

## `SystemTextJsonSerde`

```csharp
var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(AppJsonContext.Default));
```

- **A private copy, never the caller's instance** (`SERDE-26`). Both constructors copy the options they are given
  (`new JsonSerializerOptions(options)`), wire `Tristate<T>` on the copy and freeze the copy. Your `JsonSerializerOptions` is not made
  read-only, gains no converter and keeps its resolver; a later change to it does not reach the serde. *(Breaking: before 7a the
  constructor froze your instance.)*
- **Tristate is always on** (`SERDE-19`). There is no opt-out. A converter you registered for `Tristate<>` in your options before
  construction precedes ours and wins; the omit-Absent rule still applies on top.
- **`CreateDefaultOptions(IJsonTypeInfoResolver)`** returns a fresh, mutable instance per call (`SERDE-25`) with `Web` naming (camelCase
  properties, case-insensitive reads), `NumberHandling = Strict` and `RespectNullableAnnotations = true`, Tristate-wired. The resolver is
  required (the AOT-safe path needs one, and the modifier wraps it). These three choices govern the *default* configuration only: options
  you pass yourself, or a context's own `[JsonSourceGenerationOptions]`, keep your choices, and the serde adds the Tristate wiring and
  nothing else.
- **`AddTristateSupport(this JsonSerializerOptions)`** does the same wiring in place, for callers who use `JsonSerializer` directly on
  SDK models (an ASP.NET endpoint, a test); without it an Absent field would serialize as an object. Call it *after* setting the
  resolver; it is idempotent in effect, throws `ArgumentException` without a resolver and `InvalidOperationException` on read-only
  options.
- **Generation mode.** Use a `JsonSerializerContext` in the default mode or `Metadata`. A context generated with
  `GenerationMode = Serialization` (the fast-path-only mode) carries no property metadata, so neither the Tristate wiring nor any options
  other than the context's own can serialize through it (the failure is an `InvalidOperationException` from System.Text.Json, which
  the synchronous `Serialize` wraps as a `SerializationException`; measured on SDK 10.0.401 in the phase's pre-flight and pinned by a test).

### Strict coercion (`SERDE-21`, `SERDE-22`)

`Web` alone would bind `"5"` to an `int` (`AllowReadingFromString`), the first thing `SERDE-21` forbids, which is why
`CreateDefaultOptions` forces `Strict` back. A plain `General` context is strict too. Each row throws a `DeserializationException`
whose inner exception is a `JsonException`, and each is its own test, under both configurations:

| JSON | Target | Result |
|---|---|---|
| `"5"` | `int` | rejected |
| `"1.5"` | `double` | rejected |
| `"true"` | `bool` | rejected |
| `""` | `int` | rejected |
| `1.5` | `int` | rejected |
| `true` | `int` | rejected |
| `1` | `bool` | rejected |
| `true` | `double` | rejected |
| `5` | `string` | rejected |
| `5` | `double` | `5.0` (an integer widens, `SERDE-22`) |
| `""` | `string` | `""` (`SERDE-22`) |

`RespectNullableAnnotations` has one consequence to know: a JSON `null` for a **non-nullable member** is a failure, not a silent
`null`. A model for a loose server declares the member nullable. Unmapped members are skipped (`SERDE-23`), dates are ISO-8601 and
round-trip to the same instant (`SERDE-24`), and a read-only serde is safe to share across threads (`SERDE-29`).

### Where the wire has no key (`SERDE-20`)

An Absent `Tristate<T>` *property* is omitted. Where there is no property to omit it degrades to `null`: at the document root, as an
array element (indices never shift), and as a dictionary value (System.Text.Json has no per-entry `ShouldSerialize`). A root `null`
decodes to Null.

## Reading a body

`ResponseBody` has four typed readers (`SERDE-7`, `SERDE-13`, `SERDE-27`):

```csharp
Widget widget = await response.Body.ReadValueAsync<Widget>(serde, ct);        // never null for a reference type
Widget? maybe = await response.Body.ReadValueOrDefaultAsync<Widget>(serde, ct); // admits a wire null
Widget sync   = response.Body.ReadValue<Widget>(serde, ct);                    // the blocking twins
Widget? syncMaybe = response.Body.ReadValueOrDefault<Widget>(serde, ct);
```

- **They stream.** The reader opens the body, reads one byte to tell a payload from none, and hands the codec a stream that replays that
  byte: nothing is materialised, whatever the size.
- **A missing payload is a failure.** A body with `ContentLength == 0`, or that yields no bytes, is a `DeserializationException`
  `The response has no body to deserialize as '<T>'.` with no inner exception, for all four readers: `ReadValueOrDefault*` admits a
  wire `null`, not an absent payload.
- **A wire `null`.** `ReadValue(Async)` throws a `DeserializationException` naming `T` for a `null` into a reference type; a
  `Nullable<>` target legitimately decodes `null`, and a non-nullable value type fails in the codec (`null` into `int`). `ISerde`'s own
  members keep their honest `T?` return, and `HttpResponseException.GetError(Async)<T>` keeps `T?` too ("possibly null": an error model
  is read opportunistically). This reading of `SERDE-13` is recorded as open for the lead (P7a-9).
- **They dispose the body on every path**, success, codec failure, `IOException` and root `null` alike; a dispose failure after a failed
  read is attached to that failure's suppressed trail, never substituted for it. A codec failure is the codec's own exception (never
  re-wrapped, `SERDE-9`); an `IOException` from the stream propagates unwrapped (`SERDE-12`); a second read throws
  `StreamConsumedException`.
- **The sync readers** call `body.OpenRead` and the seam's `Deserialize<T>(Stream)`. Over a transport's own response body they throw
  `NotSupportedException` until the transport implements the synchronous open (phase 8b), as every synchronous body reader does.

`HttpResponseException` has the matching pair: `GetErrorAsync<T>(serde, ct)` and `GetError<T>(serde)`, both over the buffered error
body, both mapping a consumed body to `ResponseNotReadException`.

## Response handlers

`IResponseHandler<T>` (in `Dexpace.Sdk.Core.Http.Response`) turns a received `Response` into a value and **owns** the response: it
disposes it on every path, so the caller never does. It is asynchronous only. Core ships two, as factories:

| Factory | Behaviour |
|---|---|
| `ResponseHandlers.Deserialize<T>(serde)` (`SERDE-27`) | decodes the body whatever the status: streams, closes the response on every path, names `T` for a missing body or a wire `null`, chains a codec failure, lets an `IOException` through |
| `ResponseHandlers.DeserializeOnSuccess<T>(serde)` (`SERDE-28`) | **2xx** decodes as above; **400 to 599** (a non-canonical 599 included) throws `HttpResponseException` over the bounded buffered error body, the same capture and 1 MiB cap as `EnsureSuccessAsync`; **anything else** (a 1xx, an unfollowed 3xx, a 304) disposes the response and throws a `DeserializationException` |

The third branch leads with the code and names what it saw: `304 Not Modified: expected a 2xx response to deserialize as 'Widget'. ETag:
"abc". Location: https://host/path?token=***.` Each part is omitted when its header is absent. `ETag` is copied raw (an opaque validator:
parsing it on an error path would turn a diagnostic into a second failure). `Location` is resolved against the request URL and passed
through `UrlRedactor`, because an exception message is logged and a redirect target can carry a credential in its query or userinfo; one
that does not parse is shown as `<unparseable>`. A control character in any part becomes `?`, so the message is one line.

A generated SDK supplies its own handler by implementing the interface. This one maps a `404` to its own exception and defers to the
status-aware handler for everything else:

```csharp
internal sealed class WidgetHandler(ISerde serde) : IResponseHandler<Widget>
{
    private readonly IResponseHandler<Widget> _inner = ResponseHandlers.DeserializeOnSuccess<Widget>(serde);

    public async ValueTask<Widget> HandleAsync(Response response, CancellationToken cancellationToken)
    {
        if (response.Status == Status.NotFound)
        {
            await using (response)   // the handler owns the response, so a path that does not delegate disposes it
            {
                throw new WidgetNotFoundException(response.Request.Url);
            }
        }

        return await _inner.HandleAsync(response, cancellationToken);
    }
}
```

## `TypedResponse<T>`: parse lazily, once

```csharp
using var typed = new TypedResponse<Widget>(response, ResponseHandlers.DeserializeOnSuccess<Widget>(serde), ct);
Console.WriteLine(typed.Status);                 // readable at once; the body is not touched
Widget widget = await typed.GetValueAsync();     // parses now
Widget same   = await typed.GetValueAsync();     // the same instance; the handler did not run again
```

- **Metadata without the body** (`HTTP-44`): `Request`, `Status`, `Headers`, `Protocol` and `ReasonPhrase` are copied at construction.
  The wrapped `Response` is deliberately not exposed: a `Body` accessor would let a caller consume the single-use body behind the memo.
- **One parse, success and failure memoized.** A `null` success is memoized, and a failure is thrown as the *same exception object* on
  every access; a cancelled construction token is a failure like any other.
- **No caller blocks another** (`HTTP-45`). Exactly-once is a compare-and-swap on a `TaskCompletionSource<T>`, not a lock and not
  `Lazy<Task<T>>`, whose monitor is held across the factory's synchronous prefix (for a buffered body, the whole parse). A concurrent
  first caller gets an incomplete task at once and awaits it. In the phase's pre-flight, eight threads on a `Lazy` over a 500 ms
  synchronous prefix each waited about 500 ms; on the compare-and-swap, only the winner did.
- **A caller's token cancels only its wait.** The parse runs under the token given at construction (the call's), so one impatient
  reader cannot poison the memo for the others.
- **Ownership.** The handler disposes the response on its own paths, so a caller who never calls `GetValueAsync` must dispose the
  wrapper. `Dispose` and `DisposeAsync` forward to the response's latched dispose (`HTTP-43`).
- There is no synchronous `TypedResponse`: it would be sync-over-async. Synchronous callers use `ReadValue<T>`.

## Not built

- **A per-read materialisation limit** (phase 5a's hand-off, P5a-23) is not built, by decision (P7a-21, open for the lead): the typed
  readers stream and are not bounded by the cap, `ReadAsBytes*` / `ReadAsString*` keep the documented 64 MiB constant, and a larger
  read already has a route in `OpenRead(Async)`.
- **`DeepValue` stays internal** (P5a-20 left the public promotion to 7a): STJ's round trip is value-level, not equality-level.
- **A Newtonsoft adapter** is unscheduled; it would implement Tristate through `ITristate` and pin `TypeNameHandling.None`.
- **The conformance kit's serde assertions** are phase 8a's; **DI registration** of an `ISerde` is phase 9's.
