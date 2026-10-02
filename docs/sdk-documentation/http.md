# The HTTP model

**As built by phase 2a, written against source on 2026-10-02** (branch `phase-2a-domain-model`). This page describes
the types in `Dexpace.Sdk.Core.Http.*` as they exist after the domain-model rework. The requirement IDs it cites are
the normative text (`docs/product-spec/04-core-http-domain-model.md`); the decisions behind each type are in the
[phase 2a design](../work/mvp/phase2/phase2a/2026-09-29-phase2a-domain-model-design.md) and in
[design §4](../sdk-design-dotnet/04-domain-model-construction.md). Neither is restated here.

## The rules every model type follows

- **Immutable after construction** (`HTTP-1`). A model type is a `sealed record`, a `readonly record struct`, or a
  sealed class that holds immutable storage. Nothing has a public setter that mutates; `init` accessors validate.
- **Four ways to make one** (`HTTP-2`, `SEAM-29`). A static factory (`Method.Of`, `HttpHeaderName.Of`,
  `MediaType.Parse`, `ETag.Strong`, `HttpRange.Bounded`, `Query.Parse`); a validating constructor (`Request`,
  `Response`); a parameterless record whose every `init` validates (`RequestOptions`, `RequestConditions`); or one of
  the two multimap builders (`Headers.Builder`, `Query.Builder`). There is no generic `Builder<T>`: composition takes
  a `Func<T, T>`.
- **Derivation never aliases** (`HTTP-3`). `Request` derives through `With*`, `Response` through `WithBody`, the
  records through `with`, and the two multimaps through `ToBuilder()`. The original is never changed.
- **Collections are read-only** (`HTTP-5`). `Headers.Names`, `Headers.GetAll`, `Query.Names` and `Query.GetAll`
  return read-only lists; casting one to `IList<string>` and calling `Add` throws `NotSupportedException`.
- **Argument errors say which argument.** A missing required value is an `ArgumentNullException` whose `ParamName` is
  the field (`HTTP-4`). A rejected value is an `ArgumentException` that names the offending character by code point
  (`U+000D`) and never echoes the value (`HTTP-20`).

## `Request`

```csharp
var request = Request.Post(
    "https://api.example.com/items",
    RequestBody.FromString("{\"name\":\"widget\"}", CommonMediaTypes.ApplicationJson));

var traced = request
    .WithHeader("X-Trace-Id", "abc")
    .WithHeader(HttpHeaderName.WellKnown.Accept, "application/json");
```

A request carries exactly a method, a URL, headers and an optional body (`HTTP-6`). All four properties are get-only,
so `request with { Url = u }` no longer compiles; use `WithMethod`, `WithUrl`, `WithHeaders`, `WithHeader`,
`WithBody` and `WithoutBody`. Every one of them goes through the constructor, which enforces:

- the URL is absolute `http` or `https` (`HTTP-47`); anything else, including a relative string and the `file:` URL
  Linux gives `"/rel"`, is an `ArgumentException` with `ParamName` `url` whose message carries the input as
  `UrlRedactor` renders it, so a password or token in the URL never lands in the message;
- no body on GET, HEAD, TRACE or CONNECT (`HTTP-7`): `ParamName` `body`, and the message says to clear the body first.
  A POST with a body therefore cannot become a GET with `WithMethod(Method.Get)`; call `WithoutBody()` first.

Equality (`HTTP-46`) compares the method, `Url.AbsoluteUri` (ordinally, so userinfo and fragment count and no DNS runs),
the headers by value, and the body. Bodies built from bytes, a string, a value or `ToReplayableAsync()` compare by their
bytes and content type; a body over a stream, and any `RequestBody` subclass you write, compares by reference.
`ToString()` prints the method and the redacted URL (`GET https://***:***@host/p?token=***`), never headers or body.

## `Response`

```csharp
using var response = new Response(request, Status.Ok, Protocol.Http11, headers, body, reasonPhrase: "OK");
```

The constructor takes the request that produced the response and a required `Protocol` (there is no default). It adds
`ReasonPhrase` (`string?`; a value with a control character is rejected, and the reference transport drops an unsafe
one to `null`), the classifications `IsInformational`, `IsSuccess`, `IsRedirect`, `IsClientError`, `IsServerError` and
`IsError` (`HTTP-11`), and `WithBody(ResponseBody)`. `WithBody` is the only derivation, because a `WithHeaders` would
leave two responses owning one body. An absent body is an empty, replayable buffered body, never `null`.

`Status` stays a `readonly record struct`: `Status.FromCode(code)` is total, `Status.TryGetKnown(code, out var status)`
tells a named status from a nameless one (`HTTP-10`), and `Status.IsError` is 400 to 599.

## `Method`, `HttpHeaderName` and `MediaType`

`Method` and `HttpHeaderName` are `sealed record`s (classes), so `default(Method)` and `Nullable<Method>` do not exist.
`Method.Of(token)` trims SP and HTAB, requires an RFC 9110 token, maps the nine well-known verbs to their cached
instances ASCII case-insensitively (`get` is `Method.Get`) and keeps any other token verbatim. The public `IsSafe` and
`IsIdempotent` are gone (`HTTP-9`); the retry policy reads an internal set, and TRACE is no longer retried.

`HttpHeaderName` compares and hashes by its ASCII-folded `CanonicalName` and keeps the trimmed `Original` spelling
for the wire (`HTTP-21`); `ToString()` returns `Original`. `HttpHeaderName.WellKnown` adds `IdempotencyKey`, `IfMatch`,
`IfNoneMatch`, `IfModifiedSince`, `IfUnmodifiedSince` and `Range`.

`MediaType.Parse` rejects a parameter with an empty raw value (`a=`; `a=""` is fine) and `MediaType.Charset` is `null`
for `utf-7` instead of throwing (`HTTP-24`, `HTTP-53`). `Protocol.Parse` folds case with ASCII rules (`HTTP-33`).

## `Headers`

```csharp
var headers = Headers.Empty
    .With("Accept", "text/plain")
    .With("accept", "application/json")   // appended under the first insertion's casing
    .Set("X-Trace-Id", "abc")             // replaces; null removes
    .Set("X-Old", null);
```

- **Casing and order** (`HTTP-13`, `HTTP-16`, `HTTP-21`). Names are compared with an ASCII-only fold, so `"TITLE"`
  and `"title"` match under any culture and a non-ASCII lookup name finds nothing instead of folding (the Kelvin sign is
  not `k`). Enumeration and `Names` yield the casing of each name's first insertion, in insertion order. `Set` on an
  existing name keeps its position; `Without` then `With` moves it to the end.
- **Add appends, set replaces** (`HTTP-14`); `Set(name, null)` removes the header and returns the same instance when
  it was absent (`HTTP-15`).
- **Value equality.** Two sets are equal when their folded names, order and values match; casing is ignored. The hash
  agrees.
- **Typed overloads** for `Contains`, `Get`, `GetAll`, `With`, `Set` and `Without` take an `HttpHeaderName`.
- **Validation** (`HTTP-17`, `HTTP-18`, `HTTP-19`): names are trimmed of SP/HTAB and must be tokens; outbound values
  accept only HTAB and 0x20 to 0x7E. `Headers.Builder.AddInbound` is the lenient path for received headers: obs-text is
  allowed, controls are not, and the sender's casing is kept.
- `ToString()` lists names only, never values.

### For transport authors: `HttpHeaderSyntax`

A transport must re-check what it is about to put on the wire, because reflection can forge a model. The four
predicates the model itself uses are public so that no transport restates them (`HTTP-17` to `HTTP-20`):

| Member | Meaning |
|---|---|
| `IsValidName(span)` | a non-empty RFC 9110 token; no trimming |
| `IsValidOutboundValue(span)` | HTAB and 0x20 to 0x7E only |
| `IsValidInboundValue(span)` | no C0 control but HTAB, no DEL; obs-text allowed |
| `EscapeName(string)` | every non-token character as `\uXXXX`, for a log line |

Header names that `HttpClient` knows (`Accept`, `Content-Type`, `User-Agent`, ...) go out in `HttpClient`'s own
casing; a custom name goes out as the caller wrote it. Mapping the known names is phase 8b's (`TRANSPORT-10`).

## `Query`

```csharp
var query = new Query.Builder().Add("q", "a b").Add("tag", "x").Add("tag", "y").Build();
query.Encode();                    // "q=a%20b&tag=x&tag=y"
Query.Parse("?q=a%20b&flag");      // total and lenient: never throws
```

A `Query` is an ordered multimap with case-sensitive (ordinal) names (`HTTP-28`). `Add("flag", null)` stores `""`, and
`Set("flag", (string?)null)` also stores `""`: unlike `Headers.Set`, `null` never removes (use `Remove`). Builder
`Set(name, [])` with an empty sequence drops the name. `Encode()` is RFC 3986 (`%20`, never `+`; `HTTP-29`) and equality
holds exactly when `Encode()` is equal (`HTTP-30`). `Parse` skips stray `&` and empty names, keeps a malformed escape
raw, leaves `+` alone, keeps an escaped invalid UTF-8 sequence raw, and turns a literal lone surrogate into U+FFFD
(`HTTP-31`). The builder rejects a lone surrogate. A `Query` is not a member of `Request`; splicing it into a URL is the
transport projection's job (`SEAM-27`).

## `RequestOptions`

```csharp
var options = new RequestOptions { Timeout = TimeSpan.FromSeconds(5), MaxRetries = 0 }.WithTag("op", "list");
```

Per-call overrides (`HTTP-34`, `HTTP-35`). `null` means do not override. `Timeout` must be positive (so
`Timeout.InfiniteTimeSpan` is rejected) and `MaxRetries` at least 0, where 0 means no retries for this call; the
checks live in the `init` accessors, so `with` cannot bypass them. `Tags` is never `null`, keys are ordinal, and
equality compares tags by content. The phase 2b transport SPI carries this type.

## Conditional requests: `ETag`, `HttpRange`, `RequestConditions`

```csharp
var conditions = new RequestConditions
{
    IfNoneMatch = [ETag.Parse("\"v1\"")!],
    IfModifiedSince = DateTimeOffset.UtcNow.AddHours(-1),
};
var conditional = conditions.ApplyTo(request);

var range = HttpRange.Parse("bytes=0-499");        // or HttpRange.Bounded(0, 500), Suffix(500), From(9500)
var ranged = request.WithHeader(HttpHeaderName.WellKnown.Range, range.ToString());
```

- `ETag` is `*`, strong or weak (`HTTP-48`). Opaque characters are `etagc` (0x21, 0x23 to 0x7E, obs-text); a strong tag
  may not be empty, a weak one may. `Parse` returns `null` for blank input and throws for a malformed form; its
  `ToString()` round-trips. An obs-text tag is valid but `RequestConditions.ApplyTo` throws for it, because the
  outbound header rule wins (see design §11).
- `HttpRange` is one `bytes` range (`HTTP-49`). Equality is over `Offset` and `Length`, so `Bytes=0-9` equals
  `Bounded(0, 10)`; `ToString()` returns the parsed text verbatim.
- `RequestConditions` (`HTTP-50`) normalises in its `init` accessors (`*` is exclusive with concrete tags, a repeated
  `*` collapses), writes with `Set` (so applying twice is idempotent), renders dates as UTC in `"R"` format, and
  leaves unset members' headers alone.

## Migrating from before phase 2a

| Before | Now |
|---|---|
| `request with { Url = u }`, `{ Headers = h }`, `{ Method = m }`, `{ Body = b }` | `WithUrl(u)`, `WithHeaders(h)`, `WithMethod(m)`, `WithBody(b)` / `WithoutBody()` |
| a request with a body on GET, HEAD, TRACE or CONNECT | rejected with `ArgumentException`; clear the body first |
| `new Response(status, headers, body, protocol)` | `new Response(request, status, protocol, headers, body, reasonPhrase)`; `protocol` has no default |
| `default(Method)`, `Method?` as `Nullable<Method>`, same for `HttpHeaderName` | both are classes; `Method?` and `HttpHeaderName?` are nullable references |
| `Method.IsSafe`, `Method.IsIdempotent` | removed from the public surface |
| `HttpHeaderName.ToString()` returned the lower-case name | returns `Original`; use `CanonicalName` for the folded form |
| `Headers` enumerated lower-cased names in unspecified order | original casing of the first insertion, in insertion order |
| `Headers.Names` was `IEnumerable<string>` | `IReadOnlyList<string>` |
| `headers.Set(name, value)` with a non-null value only | `value` is `string?`; `null` removes the header |
| `Headers` equality was by reference | by value |
| `MediaType.Parse("text/plain;a=")` parsed | rejected; `a=""` is the way to say empty |
| `RetryPolicy` retried TRACE | it does not |
| `RedirectPolicy` sent a hop to an `ftp:` or `mailto:` `Location` | returns the 3xx unfollowed |
