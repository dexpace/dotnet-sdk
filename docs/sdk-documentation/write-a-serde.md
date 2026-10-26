# Write a serde

**As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). Ported from Node's [`write-a-serde.md`](./write-a-serde.md) (`nodejs-sdk`, `origin/main`) and restated for `ISerde`. The seam
and the typed readers that sit on it are described in [`seams.md`](./seams.md) and [`serde.md`](./serde.md); the normative text is `docs/product-spec/14-serialization-serde.md` (`SERDE-1` to `SERDE-30`).
This page is for the author of a **codec**: an implementation of `ISerde` for a wire format core does not ship.

A serde is one interface, and smaller than Node's six-method bundle because core adds the allocation profiles once, for every codec:

```csharp
public interface ISerde
{
    MediaType DefaultMediaType { get; }
    ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default);
    ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default);
    void Serialize<T>(IBufferWriter<byte> destination, T value);
    T? Deserialize<T>(ReadOnlySpan<byte> utf8);
    T? Deserialize<T>(Stream source);   // a default interface member: materialises under the 64 MiB cap, then calls the span decode
}
```

You owe five members and may override the sixth. `SerdeExtensions` then gives every codec the other profiles for free (`SEAM-20`): `SerializeToUtf8Bytes` (a fresh array), `SerializeToString` (UTF-8, or the
codec's own string if it implements the optional `IStringSerde`) and `Serialize(Span<byte>, T)` (the fixed-buffer profile, which throws `ArgumentOutOfRangeException` and leaves the destination untouched when the
payload does not fit, `SERDE-4`; the codec has nothing to do for it). `Dexpace.Sdk.Core` ships **no** codec. `Dexpace.Sdk.Serialization.SystemTextJson` is the reference implementation and a sibling of core,
never a dependency of it, which is why the seam is public.

## The target type is the generic argument

Node needs a `Schema<T>` as the type witness because TypeScript erases generics. The CLR does not: inside `Deserialize<T>`, `typeof(T)` is the full closed type, `List<Dto>` included, so a parametric target
(`SERDE-6`) is free and the seam takes no `System.Type` (`SEAM-22`). Two consequences:

- A decode that needs the type at run time reads `typeof(T)`, or better a **compile-time** table keyed by it. Reflection over `T` is what trimming and NativeAOT remove, which is why the System.Text.Json codec
  resolves metadata from a source-generated `JsonSerializerContext` and not from reflection. A codec that wants to run in the NativeAOT smoke consumer must do the same in its own format.
- The codec receives a `T` it did not choose. An unsupported `T` is a failure of the **codec**, thrown as a `SerializationException` or `DeserializationException` that names `T`, never a `NullReferenceException` two
  lines later.

## The minimum

All five members, no shortcuts. This is a complete codec for one made-up format, a list of text lines, compiled by the documentation build together with the program that uses it. Everything it does is one
of the rules below.

```csharp
using System.Buffers;
using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Serialization;

var serde = new LinesSerde();
string text = serde.SerializeToString<IReadOnlyList<string>>(["alpha", "beta"]);   // "alpha\nbeta"
IReadOnlyList<string>? back = serde.Deserialize<IReadOnlyList<string>>(Encoding.UTF8.GetBytes(text));
Console.WriteLine($"{serde.DefaultMediaType}: {back?.Count} lines");

RequestBody body = RequestBody.FromValue<IReadOnlyList<string>>(["alpha", "beta"], serde);   // Content-Type: text/plain; charset=utf-8
Console.WriteLine(body.ContentType);

internal sealed class LinesSerde : ISerde
{
    private static readonly UTF8Encoding s_strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public MediaType DefaultMediaType { get; } = MediaType.Parse("text/plain; charset=utf-8");   // SERDE-2: declared, never a format-agnostic default

    public async ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default)
    {
        byte[] bytes = Encode(value);

        // SERDE-3: write fully, and never close or take ownership of the caller's stream. An IOException from it propagates unwrapped (SERDE-12).
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public void Serialize<T>(IBufferWriter<byte> destination, T value) => destination.Write(Encode(value));

    public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();

        // SERDE-3 again, on the read side: read to the end, leave the stream open. A stream failure is not a payload failure.
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Deserialize<T>(buffer.ToArray());
    }

    public T? Deserialize<T>(ReadOnlySpan<byte> utf8)
    {
        string text;
        try
        {
            text = s_strict.GetString(utf8);
        }
        catch (DecoderFallbackException ex)
        {
            // SERDE-9, SERDE-10: a read-path failure is the DeserializationException subtype, with the library's exception chained.
            throw new DeserializationException($"The payload is not valid UTF-8, so it cannot be decoded as '{typeof(T)}'.", ex);
        }

        string[] lines = text.Length == 0 ? [] : text.Split('\n');
        if (lines is T target)
        {
            return target;
        }

        throw new DeserializationException($"This codec decodes only a list of lines, not '{typeof(T)}'.");
    }

    private static byte[] Encode<T>(T value)
    {
        if (value is not IReadOnlyList<string> lines)
        {
            // SERDE-10: a write-path failure is the SerializationException subtype.
            throw new SerializationException($"This codec encodes only a list of lines, not '{typeof(T)}'.");
        }

        return Encoding.UTF8.GetBytes(string.Join('\n', lines));
    }
}
```

The rules, all visible above:

1. **Raise `SerializationException` / `DeserializationException`, never a library type** (`SERDE-9`, `SERDE-10`). Both derive from the abstract `SerdeException`, so one `catch` covers any serde failure and
   the direction still distinguishes. **Chain the original as `InnerException`**: the backing library's message is what a caller debugs with. A `FormatException`, a `JsonException` or a
   `DecoderFallbackException` that escapes the seam is a bug.
2. **A stream failure propagates unwrapped** (`SERDE-12`). Re-wrapping an `IOException` from a socket as a `DeserializationException` tells a caller their payload was malformed when their connection dropped. The
   rule is direction-agnostic: only malformed input, a shape mismatch or an unencodable value is wrapped.
3. **Never close or take ownership of a caller's stream** (`SERDE-3`). Write or read it fully (to the end, on the read side) and leave it open, on the failure path too. A codec whose engine auto-closes its
   destination turns that off.
4. **Declare the media type** (`SERDE-2`). `DefaultMediaType` has no default implementation, because a format-agnostic default would let a non-JSON codec stamp `application/json`. `RequestBody.FromValue(value,
   serde)` uses it when you pass none.
5. **A wire `null` is honest.** `ISerde` returns `T?`; return `null` for a `null` literal. The typed readers (`ResponseBody.ReadValueAsync<T>`) reject it for a reference type after the codec returns and
   name `T` in the failure (`SERDE-13`, judged by core's tests, not by the codec suite), so the codec reports what the wire said and the caller's reader decides.
6. **Decode strictly** (`SERDE-21`, `SERDE-22`). Do not coerce a string into a number, a float into an integer or a boolean into a number; do widen an integer into a floating-point target and bind an empty
   string to a string. Ignore unknown members (`SERDE-23`); write dates as ISO-8601 that round-trips to the same instant (`SERDE-24`).
7. **Build a fresh instance, never mutate the caller's engine, and be safe to share** (`SERDE-25`, `SERDE-26`, `SERDE-29`). A factory returns a new configuration per call. A constructor that takes a native
   codec instance (`JsonSerializerOptions`, say) works on a **private copy**, and the copy is read-only after construction, so one serde serves concurrent calls. `SystemTextJsonSerde` is the model.
8. **Override `Deserialize<T>(Stream)` if you can stream.** The default materialises the stream under `ResponseBody.DefaultMaxMaterializedBytes` (64 MiB) and calls the span decode, which is correct and
   bounded. A codec with a streaming synchronous decoder overrides it (`SystemTextJsonSerde` does); there is no synchronous stream *encode*, because `Serialize(IBufferWriter<byte>, T)` is the synchronous
   primitive.

## Using one

The program above ends with the one-line use: `RequestBody.FromValue(value, serde)` serialises through the codec's buffer primitive into a replayable body and stamps the codec's `DefaultMediaType` when you pass none (`SERDE-2`).
The decode side is [`write-a-response-handler.md`](./write-a-response-handler.md): `response.Body.ReadValueAsync<T>(serde)` streams the body through your `DeserializeAsync<T>`, and
`ResponseHandlers.Deserialize<T>(serde)` is the same read as an `IResponseHandler<T>`. `HttpResponseException.GetErrorAsync<T>(serde)` reads a buffered error body through it.

## The tri-state problem

This is the part a JSON codec gets wrong by default, and the reason `Dexpace.Sdk.Serialization.SystemTextJson` is a reference and not a five-line wrapper around `JsonSerializer`.

A PATCH request has **three** meanings for a field, and a nullable has two:

| Intent | On the wire | `Tristate<T>` |
|---|---|---|
| Leave it alone | the key is absent | `Tristate<T>.Absent` (`default`) |
| Clear it | `"x": null` | `Tristate<T>.Null` |
| Set it | `"x": 1` | `Tristate<T>.Present(1)` |

`Tristate<T>` lives in core (`Dexpace.Sdk.Core.Serialization`) as a `readonly struct` with those three states and no codec attribute. A codec reaches the closed type without reflection through the public
`ITristate` / `ITristateVisitor<TResult>` pair (`Accept` calls `Visit<T>(Tristate<T>)` with the closed type, so it needs no `MakeGenericType` and is NativeAOT-safe). What a codec owes (`SERDE-15` to
`SERDE-17`, `SERDE-19`, `SERDE-20`):

- **Emission.** Inside an object, `Absent` omits the key, `Null` writes the key with a wire null, `Present` writes the encoded inner value.
- **Decoding.** A missing key is `Absent` (so a field with no initializer, `default(Tristate<T>)`, must resolve to `Absent`, not `Null`), an explicit null is `Null`, anything else is `Present(value)`.
- **On by default.** Building the serde from a bare native codec with default settings must wire this; opting out is for a caller who already installed equivalent wiring. Without the wiring `Absent` and `Null`
  are indistinguishable on the wire.
- **Where there is no key.** At the document root or as an array element, degrade to a wire null for both `Absent` and `Null` rather than throwing.

If your format genuinely has two states, say so in the package README and waive `SERDE-15`, `-16`, `-17`, `-19` and `-20` in your driver with a reason ([below](#prove-it)); do not silently collapse `Absent`
into `Null`.

## Prove it

Do not hand-roll the assertions. `CodecSuite` runs 11 assertions over the `SERDE` rows a codec can be asked, on both profiles of `ISerde` (the asynchronous stream pair and the synchronous buffer and span pair),
against a `CodecSubject`. This program is the repository's first-party driver, over `SystemTextJsonSerde`, written as a console program (the xUnit form is
`tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/Conformance/CodecConformanceTests.cs`: one theory case per (assertion, face), plus the whole suite as a report). It is complete and compiled by the
documentation build:

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Conformance.Codec;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Serialization.SystemTextJson;

var subject = new CodecSubject
{
    Name = "Acme.Serialization.SystemTextJson",
    // Built exactly as a consumer builds it, with no extra configuration: SERDE-19 asks whether the defaults wire the PATCH semantics.
    CreateSerde = static () => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(ProbeContext.Default)),
    ExpectedMediaType = CommonMediaTypes.ApplicationJson,
};

ConformanceReport report = await CodecSuite.RunAllAsync(subject, new SuiteOptions { AssertionTimeout = TimeSpan.FromSeconds(30) });
Console.WriteLine(report);
return report.IsGreen ? 0 : 1;

// The suite serializes only its own probe models, so a codec that needs compile-time metadata registers them: one line each, in the default
// generation mode (a fast-path-only context carries no property metadata and cannot carry the tri-state wiring).
[JsonSerializable(typeof(ProbeAddress))]
[JsonSerializable(typeof(ProbeContact))]
[JsonSerializable(typeof(ProbePatch))]
[JsonSerializable(typeof(ProbeSequence))]
[JsonSerializable(typeof(Tristate<string>))]
[JsonSerializable(typeof(ProbeUnencodable))]
internal sealed partial class ProbeContext : JsonSerializerContext;
```

| Assertion | Judges |
|---|---|
| `serde-1.round-trip` | what you write you can read back |
| `serde-2.declared-media-type` | `DefaultMediaType` equals `ExpectedMediaType` |
| `serde-3.target-not-closed` | the caller's stream is left open |
| `serde-9.no-library-type-escapes`, `serde-10.directional-subtypes` | malformed input and an unencodable value raise the right serde subtype, with the library's exception chained |
| `serde-12.stream-error-unwrapped` | an `IOException` from the stream propagates as itself |
| `serde-15` to `serde-17`, `serde-19`, `serde-20` | the tri-state rules above |

`CreateSerde` is a factory, so each run builds a fresh codec. `MalformedPayload` defaults to a broken JSON object; set it to something your format rejects. The suite judges what a codec preserves and refuses
through its own round trip, not how it spells it on the wire. A waiver is `new ConformanceWaiver("SERDE-15", reason)` in `SuiteOptions.Waivers`, narrowed with `Assertion` and `Face`, and **a waiver whose
assertion now passes fails the run**. The first-party driver has no waiver and nothing not exercised, and pins both lists.

**What a green codec run does not prove**: the codec's metadata for models it was not given (the probe models are the only types the suite serializes); configuration it was not handed; the wire format byte for
byte; core's typed readers, `SerdeExtensions` and `Tristate<T>`'s own API (`SERDE-4`, `-13`, `-14`, `-18`, `-30`, which judge core code and are cited by reference), or any requirement no assertion cites.

## What the seam does not do

- **No default codec and no fallback to JSON.** A pipeline with no serde configured serializes nothing; under dependency injection a client without `UseSerde` fails validation.
- **No content negotiation.** `DefaultMediaType` is a default for `RequestBody.FromValue`, not a negotiation with the server's `Accept`.
- **No coupling to streaming.** Core's server-sent-events reader holds no serializer (`SSE-37`); `ServerSentEventStream.MapAsync` takes your mapper, which is where you call your codec. Pagination reads a
  page envelope through the serde you hand `Pageable.Create`.
