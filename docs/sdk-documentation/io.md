# Bodies and I/O

**As built by phase 3a, written against source on 2026-10-03** (branch `49-phase-3a-io`). This page describes what phase 3a
changed about request and response bodies in `Dexpace.Sdk.Core`: synchronous twins of every body member, exact-length
stream bodies, a cap on materialising a response, and the single-open rule across both forms. The requirement IDs it cites
are the normative text (`docs/product-spec/05-i-o-contracts.md`, `docs/product-spec/06-request-and-response-body-lifecycle.md`);
the decisions behind each change are in the [phase 3a design](../work/mvp/phase3/phase3a/2026-10-02-phase3a-io-design.md)
and in [design §3.1](../sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md). Neither is restated here.

## Synchronous and asynchronous twins

Every async body member has a synchronous twin with the same name minus `Async` (`HTTP-36`):

| Async | Synchronous twin |
|---|---|
| `RequestBody.WriteToAsync(Stream, CancellationToken)` | `RequestBody.WriteTo(Stream, CancellationToken)` |
| `RequestBody.ToReplayableAsync(CancellationToken)` | `RequestBody.ToReplayable(CancellationToken)` |
| `ResponseBody.OpenReadAsync(CancellationToken)` | `ResponseBody.OpenRead(CancellationToken)` |
| `ResponseBody.ReadAsBytesAsync(CancellationToken)` | `ResponseBody.ReadAsBytes(CancellationToken)` |
| `ResponseBody.ReadAsStringAsync(CancellationToken)` | `ResponseBody.ReadAsString(CancellationToken)` |

Both forms of a pair produce identical bytes and share one consume guard: a single-use stream body written once through
`WriteTo` throws `StreamConsumedException` if it is then written through `WriteToAsync`, and the other way round.

### Third-party bodies

The twins are `virtual`, not `abstract`, so a body written for the async path alone still compiles. The base
`RequestBody.WriteTo` and `ResponseBody.OpenRead` throw `NotSupportedException`, with a message that names the subclass and
the member to override; `ToReplayable`, `ReadAsBytes` and `ReadAsString` are built over those two, so a subclass overrides
one member per direction. Every body the SDK creates (`FromBytes`, `FromString`, `FromValue`, `FromStream`, the buffered
copies) supports both forms. A transport's own response body supports the sync path only once that transport implements it;
`SystemNetHttpClient`'s does, since phase 7b (*dated correction, 2026-10-09: P3a-5 left it to 8b, but the blocking server-sent-events
views need it*): `OpenRead` shares the open latch with `OpenReadAsync` and reads `HttpContent.ReadAsStream`.

```csharp
public sealed class GeneratedBody : RequestBody
{
    public override MediaType? ContentType => null;
    public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) { /* ... */ }
    public override void WriteTo(Stream destination, CancellationToken cancellationToken = default) { /* ... */ }
}
```

## Streams with a declared length

`RequestBody.FromStream(source, contentType, contentLength)` copies exactly `contentLength` bytes when the length is known
(`HTTP-39`, `IO-12`):

| Source | Result |
|---|---|
| Exactly `contentLength` bytes | Written; the transport sends a matching `Content-Length` |
| Fewer bytes | The write throws `EndOfStreamException`: `The source ended after 6 of 10 bytes.` The transport fails the send instead of framing a short body |
| More bytes | Exactly `contentLength` bytes are written and the remainder is **left unread**: the body does not own your stream, and reading one byte further could block on a live source |
| `contentLength` of `0` | Nothing is read |
| `-1` (the default) | The source is copied to its end |

`FromStream` also rejects a `contentLength` below `-1` (`ArgumentOutOfRangeException`, on both request and response bodies)
and, on the request side, a source that is not readable (`ArgumentException`). A stream body is single-use; call
`ToReplayable()` or `ToReplayableAsync()` before the first send if retries are needed.

## The materialisation cap

`ResponseBody.ReadAsBytesAsync`, `ReadAsStringAsync` and their sync twins read the whole body into memory, so they refuse a
body larger than `ResponseBody.DefaultMaxMaterializedBytes` (64 MiB) with `BodyTooLargeException` (`IO-9`). When the
response declares a `Content-Length` above the cap, the call fails before reading a byte; otherwise it reads up to the cap
and probes one more byte. To read a larger body, stream it:

```csharp
await using var stream = await response.Body.OpenReadAsync(ct);   // or OpenRead(ct)
await stream.CopyToAsync(destination, ct);
```

The cap is a constant today; making it configurable is phase 5a. `RequestBody.ToReplayable(Async)` has no 64 MiB cap, because
the size is the caller's own data; it refuses only what an array cannot hold (`Array.MaxLength`), before writing when the
length is known.

`BodyTooLargeException` derives from `StreamingException` and is distinct from `StreamConsumedException`. Its message names
the limit and, when known, the declared length, and never echoes body content.

## A response body opens once, in either form

The first of `OpenRead` and `OpenReadAsync` claims the body; a second open in either form throws `StreamConsumedException`
(`BODY-14`, `HTTP-41`; design §10 entry 6). The specification would return the same handle, but a second holder of the same
partly read stream interleaves reads silently. The buffered error body that `EnsureSuccessAsync` attaches to an
`HttpResponseException` is replayable by design and opens a fresh view each time.

## What stays internal

The helpers behind these members are `internal` and carry no public vocabulary of their own: `StreamCopy` (exact, capped and
unbounded copies), `TeeStream` (mirroring writes into a bounded tap), `CapturedBytes` (non-consuming read-only views and
slices over a never-pooled array), `Utf8LineReader` (UTF-8 lines in a default and a WHATWG mode, with a mandatory line cap),
`BodyMaterializer` and `BoundedBufferStream`. You reach their behaviour through the members above; the logging wrappers
(phase 3b) and the SSE parser (phase 7b, [`sse.md`](./sse.md)) are their consumers. `BannedSymbols.txt` keeps three rules in force for `src/`:
captured bytes are never rented from a pool, core never sets a stream timeout, and `StreamReader.ReadLine` is not used (it
splits on a lone CR that `IO-14` keeps as content).

## Migrating

Phase 3a changes five behaviours. All are in `CHANGELOG.md` `[Unreleased]` and marked **Breaking** in the XML docs.

| # | Member | Before | Now |
|---|---|---|---|
| 1 | `RequestBody.FromStream(source, type, contentLength)` with a known length | Copied the source to its end whatever was declared | Writes exactly `contentLength` bytes: a short source throws `EndOfStreamException`, a longer source's remainder is left unread |
| 2 | `FromStream` on both bodies | Accepted any `contentLength`; the request side accepted an unreadable stream | `contentLength` below `-1` throws `ArgumentOutOfRangeException`; an unreadable request source throws `ArgumentException` |
| 3 | `ResponseBody.ReadAsBytesAsync`, `ReadAsStringAsync` | Unbounded | Throw `BodyTooLargeException` above 64 MiB; stream through `OpenReadAsync` instead |
| 4 | `RequestBody.ToReplayableAsync` | Failed with `IOException` ("Stream was too long") or `OutOfMemoryException`, after consuming the body | Throws `BodyTooLargeException` above `Array.MaxLength`, before writing when the length is known |
| 5 | A response body's `OpenRead` after `OpenReadAsync`, or the reverse | No sync form existed | Throws `StreamConsumedException` |

Requirement IDs: `HTTP-36`, `HTTP-39`, `IO-3`, `IO-9`, `IO-11`, `IO-12`, `IO-13`, `IO-14`, `IO-17`.
