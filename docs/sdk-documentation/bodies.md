# Request and response bodies

**As built by phase 3b, written against source on 2026-10-03** (branch `50-phase-3b-bodies`, stacked on phase 3a). This page
describes what phase 3b added to the bodies of `Dexpace.Sdk.Core`: the file, form and multipart request bodies, the seekable
promotion of `FromStream`, the dispose latches on `Response` and `ResponseBody`, the byte-order-mark rule, and the two
internal logging wrappers. The synchronous twins, exact-length streams and the materialisation cap are phase 3a's, on
[`io.md`](./io.md). The requirement IDs cited are the normative text
(`docs/product-spec/06-request-and-response-body-lifecycle.md`, chapter 05 for the tee); the decisions behind each change
are in the [phase 3b design](../work/mvp/phase3/phase3b/2026-10-02-phase3b-bodies-design.md) and
[design §3.1](../sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md). Neither is restated here.

## Request bodies

| Factory | Replayable | `ContentLength` | Notes |
|---|---|---|---|
| `RequestBody.FromBytes`, `FromString`, `FromValue` | yes | exact | Compare by value (`HTTP-46`) |
| `RequestBody.FromForm(fields)` | yes | exact | WHATWG form encoding; the in-memory variant, so it compares by value |
| `RequestBody.FromFile(path, contentType, offset, count)` | yes | exact, never `-1` | Public `FileRequestBody`; a fresh handle per write |
| `RequestBody.Multipart(parts, boundary)` | if every part is | exact, or `-1` if any part's is | Identity equality |
| `RequestBody.FromStream(stream, contentType, contentLength)` | only if seekable with a known length | `contentLength` | See below |

Every replayable factory writes byte-identical content on every write, in both the async and the sync form, and
`ToReplayableAsync()` returns the same instance (`HTTP-36`, `HTTP-37`, `BODY-1`, `BODY-3`).

### Forms

```csharp
var body = RequestBody.FromForm([new("grant_type", "client_credentials"), new("scope", "read write")]);
// grant_type=client_credentials&scope=read+write
```

The encoder is the WHATWG `application/x-www-form-urlencoded` serializer, written by hand because no built-in is it
(`HTTP-38`). Names and values are UTF-8 encoded strictly; `A-Z a-z 0-9 * - . _` stay literal, a space is `+`, and every other
byte is `%XX` in upper case. That differs from `Uri.EscapeDataString` (RFC 3986: a space is `%20`, `~` stays literal, `*` is
encoded) and from `WebUtility.UrlEncode` (leaves `!()` literal), and from the Node sibling, which encodes with RFC 3986 and
rewrites `%20` to `+`: on `~` and `*` the two ports disagree, and the vector file `tests/vectors/body/form-urlencoded.json`
records WHATWG's answer. Order and duplicate names are kept; a null name or value throws `ArgumentNullException`, and a lone
surrogate throws `ArgumentException` naming the field index and never the value. The content type carries no `charset`.

### Files

```csharp
var body = RequestBody.FromFile("/var/data/report.csv", MediaType.Parse("text/csv"), offset: 0, count: -1);
```

`FromFile` validates now and captures the size: a missing file throws `FileNotFoundException`, a directory throws
`ArgumentException`, a symbolic link is resolved to its target before the size is taken (a link's own `Length` is the length
of its path), and an `offset` or `count` outside the file throws `ArgumentOutOfRangeException` (`HTTP-40`, `BODY-11`). A
`count` of `-1` means the rest of the file. There is no default media type: the SDK does not guess one from an extension.

Each write opens a fresh read-only handle with `FileShare.ReadWrite | FileShare.Delete`, seeks to the offset, copies exactly
the captured count through the shared exact-length copy, and disposes the handle in the same write, on success and on
failure (`BODY-8`). A file that shrank after construction makes the write fail with `EndOfStreamException` naming
delivered-of-total (`BODY-13`); one that grew is ignored past the count; a writer elsewhere is not locked out. A file
rewritten in place between two writes breaks byte-identity of the two. `FileRequestBody` is recognisable by type and exposes
`FilePath`, `Offset` and `ContentLength` (`BODY-12`), but the transfer is a large-buffer copy from an unbuffered
`FileStream`, **not** a kernel file-to-socket path: `SocketsHttpHandler` offers none for request content.

> **Domain limit (design §10 entry 31).** On Unix the shared framework cannot tell a FIFO or a character device from a
> regular file, and opening a FIFO blocks. Such a file reports a size of 0, and a count of 0 never opens the file, so it
> uploads as an **empty body** instead of failing at construction. Windows rejects a `Device` path.

### Multipart

```csharp
var body = RequestBody.Multipart(
[
    new MultipartPart("title", RequestBody.FromString("Quarterly report")),
    new MultipartPart("file", RequestBody.FromFile("report.pdf", MediaType.Parse("application/pdf")), fileName: "report.pdf"),
]);
```

The part headers and the trailer are computed once, at construction, and used for both `ContentLength` and the write, so
the two cannot drift (`HTTP-51`). The body is replayable when every part is, and its length is `-1` when any part's is
(`BODY-2`); a non-replayable composite refuses a second write with `StreamConsumedException` before a byte is written
(`BODY-6`), while a replayable one admits concurrent writes. Each part's bytes go through a length guard sized to the part's
own declared length: a part that writes more than it declared is stopped before the extra chunk reaches the sink, and one
that writes less fails when it finishes.

- **Boundary.** `dexpace-` plus 32 random alphanumerics, unless one is supplied; a supplied boundary must be 1 to 70
  RFC 2046 `bchars` not ending in a space (`ArgumentException`). It is rendered through `MediaType`, which quotes a value that
  is not a bare token, so `boundary=a,b` is sent as `boundary="a,b"`.
- **Part names.** `MultipartPart` is a sealed class with a validating constructor (not a record, so `with` cannot bypass the
  check). `Name` and `FileName` reject CR, LF, every other C0 control and DEL, naming the code point and never the value;
  `"` and `\` are backslash-escaped; non-ASCII is written as UTF-8. The Node sibling strips CR and LF silently; this port
  rejects, so a field name is never changed behind the caller's back.
- **Parts.** An empty part list is rejected (RFC 2046 section 5.1.1). Nothing the composite is given is disposed: not a part,
  not a part's source, not the destination.

### Streams: single-use, or seekable and replayable

`FromStream` over a readable, **seekable** stream with a declared `contentLength` in `[0, Array.MaxLength]` is replayable
(`BODY-9`). The body captures the stream's position at construction and, on **every** write (the first included), seeks to it
and copies exactly `contentLength` bytes; the caller's stream position therefore moves, and the stream stays open and the
caller's. One write runs at a time: a concurrent second write throws `InvalidOperationException` before touching the stream.
A length of `-1` stays single-use (`StreamConsumedException` on a second write); the length is never inferred from
`Length - Position`, because that would turn a chunked upload into a `Content-Length` one behind the caller's back. A
non-seekable stream stays single-use whatever the length.

### Who closes what

A request body closes exactly what it opened (`BODY-8`, design §10 entry 5):

| Body | Opens | Closes |
|---|---|---|
| `FromStream`, single-use or seekable | nothing | nothing; the caller's stream stays open after success, failure and `ToReplayableAsync` |
| `FromFile` | a `FileStream` per write | that handle, in the same write |
| `FromForm`, `Multipart` | nothing | nothing; multipart never disposes a part or the destination |
| `ResponseBody.FromStream` | n/a | **takes ownership** of the transport's stream |

## Response bodies

### Disposal is latched

`ResponseBody.Dispose()` and `DisposeAsync()` share one latch: across any mix of the two, the release runs **at most once**,
and a release that throws still flips the latch, so the exception propagates from that one call and later calls are
no-ops (`HTTP-41`, `BODY-15`). `Response.Dispose()` and `DisposeAsync()` share another and forward to the body once
(`HTTP-43`). A subclass puts its release in `protected override void Dispose(bool disposing)` and, if it has an
asynchronous release, `protected override ValueTask DisposeAsyncCore()`; there is no finalizer.

```csharp
public sealed class MyBody : ResponseBody
{
    public override MediaType? ContentType => null;
    public override Task<Stream> OpenReadAsync(CancellationToken ct = default) => /* ... */;
    protected override void Dispose(bool disposing)
    {
        // release the connection here; called at most once
        base.Dispose(disposing);
    }
}
```

### Read after dispose, and the second open

A stream-backed body (`ResponseBody.FromStream`, and the transport's `HttpResponseMessageBody`) opened after it was disposed
throws `StreamClosedException`; one that was already opened reports `StreamConsumedException` first, whatever the dispose
order. An in-memory body (`FromBytes`) that was never opened still serves after dispose, because it holds memory and not a
connection. A second open in either form throws `StreamConsumedException`, and its message now names the buffering route:
read the body once and keep the bytes (`ReadAsBytesAsync`, then `ResponseBody.FromBytes`) (`BODY-14`). The buffered error
body an `HttpResponseException` carries is replayable and opens a fresh view every time, before and after the response is
disposed (`BODY-33`).

### The convenience readers close the body

`ReadAsBytesAsync`, `ReadAsStringAsync` and their sync twins dispose the **body**, not only the stream, when they finish,
on success and on failure (`BODY-16`). Do not read a body again after one of them.

### The byte-order mark

`ReadAsStringAsync` and `ReadAsString` decode through one routine that resolves the encoding (the declared `charset`, else
UTF-8) and skips a leading byte-order mark **when it matches that encoding's preamble** (UTF-8, UTF-16 LE and BE, UTF-32). A
mark that does not match the declared charset is kept, because the declared charset wins; only a leading mark is stripped
(`HTTP-42`). `Encoding.GetString` itself keeps a BOM as U+FEFF for every encoding.

## The logging wrappers (internal)

`LoggingRequestBody` and `LoggingResponseBody` are `internal`: phase 5b's instrumentation is their only consumer, and engages
them only when body-level logging is on. They are described here because their contracts are tested now.

- **Request side** (`BODY-17` to `BODY-21`, `BODY-37`): each write goes through a tee into a tap capped at a required
  limit, clears the tap first, forwards every byte whatever the cap, and leaves the failing chunk captured when the write
  fails. The wrapper returns only copying snapshots, never the tap.
- **Response side** (`BODY-22` to `BODY-29`): the first open or snapshot drains the delegate once (concurrent first
  accessors share one drain, serialised on a `SemaphoreSlim`). A body that fits the cap is captured whole and the delegate
  released; every open is then a fresh read-only view. A larger body keeps the delegate open, and the next open is a single-use
  stream over the captured prefix and the live tail. A drain failure keeps the partial bytes and is rethrown by every open;
  asking for the failure or a snapshot never throws it. The drain runs under a token linked from the accessor that started
  it and the wrapper's lifetime, so a call's deadline still works with body logging on (design P3b-10).

## Breaking changes from phase 3a

| # | Before | Now | Migrate by |
|---|---|---|---|
| 1 | `ResponseBody.Dispose()` / `DisposeAsync()` were `public virtual` | Not virtual; the protected `Dispose(bool)` / `DisposeAsyncCore()` are the hooks | Move the override's body into `Dispose(bool disposing)` (and `DisposeAsyncCore()`); call `base.Dispose(disposing)` |
| 2 | Disposing a `Response` or `ResponseBody` twice released twice | At most one release; a throwing release propagates once | Nothing, unless a test counted double releases |
| 3 | `ReadAsBytesAsync` / `ReadAsStringAsync` left the body open | They dispose the body when they finish | Do not reuse the body; keep the bytes |
| 4 | `ReadAsStringAsync` kept a leading BOM as U+FEFF | A BOM matching the charset's preamble is stripped | Drop any manual BOM trimming |
| 5 | A stream body read after dispose returned the disposed stream | Throws `StreamClosedException` | Read before disposing |
| 6 | `AsAsync`: a throwing `Dispose` of a response produced after cancellation faulted the task | The task completes cancelled; the failure is an `exception` event tagged `dexpace.dispose.suppressed` on the current activity | Observe the activity, not the task |
| 7 | `FromStream` over a seekable stream with a known length was single-use | Replayable; seeks the caller's stream before each write | Do not rely on the stream's position after constructing the body |

## What is not built yet

Rebuilding a typed response lazily (`HTTP-44`, `HTTP-45`) is phase 7a's. Whether `ReadValueAsync` closes the body is also
7a's (`SERDE`). The three replay gates of `BODY-4` and the retry-only gate of `BODY-5` are 6a, 6b and 6c's. Engaging the
logging wrappers with one shared preview size is 5b's (`BODY-34`). The memory-mapped body view (`BODY-36`) is declined for
v1 (`docs/first-release.md`).
