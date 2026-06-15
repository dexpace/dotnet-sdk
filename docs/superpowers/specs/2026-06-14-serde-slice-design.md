# Serde + System.Text.Json — slice design

- **Date:** 2026-06-14
- **Status:** Approved; ready for implementation planning.
- **Part of:** [.NET SDK Platform Architecture & Build Plan](2026-06-14-dotnet-sdk-platform-design.md) — slice 1.
- **Related:** OmarAlJarrah/dotnet-sdk#1 (deferred three-state `Optional<T>` / PATCH support).

## 1. Purpose & scope

Define the SDK's serialization seam and ship the reference System.Text.Json implementation, plus
the request/response conveniences and the typed-error accessor that depend on it. This slice lands
first because typed error bodies, pagination item parsing, and webhook payload handling all build on
it.

**In scope:**

- `ISerde` seam in `Dexpace.Sdk.Core` (no third-party dependency).
- `Dexpace.Sdk.Serialization.SystemTextJson` with `SystemTextJsonSerde` and DI registration.
- `RequestBody.FromValue<T>` / `ResponseBody.ReadValueAsync<T>` conveniences in `Core`.
- `HttpResponseException.GetErrorAsync<T>` deserialize-on-demand accessor.

**Out of scope:**

- Three-state optional/PATCH type (`Optional<T>`) — tracked in #1.
- The bounded error-body buffering that makes the error accessor readable after a throw — owned by
  the policies slice; this slice only defines the accessor's contract.
- Non-JSON formats and streaming-array deserialization (`IAsyncEnumerable<T>`) — the seam permits
  both; pagination revisits streaming.

## 2. The `ISerde` seam

```csharp
namespace Dexpace.Sdk.Core.Serialization;

public interface ISerde
{
    /// Media type stamped on bodies created from values (e.g. application/json).
    MediaType DefaultMediaType { get; }

    // Streaming (primary path — matches the existing body model)
    ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken ct = default);
    ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken ct = default);

    // In-memory fast paths
    void Serialize<T>(IBufferWriter<byte> destination, T value);
    T? Deserialize<T>(ReadOnlySpan<byte> utf8);
}
```

**Semantics:**

- **UTF-8 JSON** is the assumed wire encoding for the reference implementation; the seam itself is
  format-neutral.
- **Failures map to the existing hierarchy:** serialization errors throw `SerializationException`,
  deserialization errors throw `DeserializationException` (both already defined). Implementations
  wrap their native exceptions.
- **Generic + context-backed:** `T` is resolved to type metadata by the implementation, not by the
  seam. The seam exposes no serializer-specific types, preserving agnosticism.

## 3. System.Text.Json implementation

```csharp
namespace Dexpace.Sdk.Serialization.SystemTextJson;

public sealed class SystemTextJsonSerde : ISerde
{
    public SystemTextJsonSerde(JsonSerializerOptions options);
    public SystemTextJsonSerde(JsonSerializerContext context);   // convenience
    public MediaType DefaultMediaType => CommonMediaTypes.ApplicationJsonUtf8;
}
```

- **AOT-safe by construction.** Type metadata comes from `JsonSerializerOptions.TypeInfoResolver`
  (a source-generated `JsonSerializerContext`). Each call resolves `JsonTypeInfo<T>` from the
  resolver.
- **Unknown-type behavior.** With no `JsonTypeInfo<T>` available, throw a `SerializationException` /
  `DeserializationException` whose message names the missing type and points to registering it on a
  `JsonSerializerContext`. No silent reflection fallback under the default configuration.
- **Optional reflection mode.** A non-AOT convenience may enable STJ's reflection-based resolver for
  apps that have not adopted source generation. Off by default to keep AOT behavior honest; surfaces
  the standard STJ trim/AOT analyzer warnings when enabled.
- **Lenient reads.** Default options ignore unknown JSON members (forward compatibility), mirroring
  the siblings' "payloads can grow without breaking clients." `JsonSerializerDefaults.Web`
  (camelCase, case-insensitive) is the default; fully overridable.
- **DI registration.** `services.AddSystemTextJsonSerde(MyJsonContext.Default)` registers `ISerde`.

## 4. Body conveniences (Core)

```csharp
// Request side — eager, replayable
public static RequestBody FromValue<T>(T value, ISerde serde, MediaType? contentType = null);

// Response side — single-use (honors existing body semantics)
public ValueTask<T?> ReadValueAsync<T>(this ResponseBody body, ISerde serde, CancellationToken ct = default);
```

- `FromValue<T>` serializes eagerly into a pooled buffer via `serde.Serialize`, producing a
  **replayable** byte-backed body (retries are free, no re-serialization). Content-type defaults to
  `serde.DefaultMediaType`. Trade-off accepted: typed-model payloads are small, so eager buffering is
  the pragmatic choice.
- `ReadValueAsync<T>` opens the response stream and delegates to `serde.DeserializeAsync<T>`.
  Single-use rules apply — a second read throws `StreamConsumedException`, as today.

## 5. Typed error bodies

```csharp
// On HttpResponseException (Core)
public ValueTask<T?> GetErrorAsync<T>(ISerde serde, CancellationToken ct = default);
```

- Reads the error response body through the serde and returns the caller-chosen `T`. The caller
  picks the model; the throw site does not need to know it.
- **Contract / dependency:** the accessor requires `Response.Body` to be replayable. The pipeline
  buffers the error body (bounded) before throwing on 4xx/5xx — implemented in the policies slice. If
  invoked on a consumed or non-replayable body, the accessor throws `ResponseNotReadException`.

## 6. Project & repo changes

- New `src/Dexpace.Sdk.Serialization.SystemTextJson/` (multi-target `net8.0;net10.0`,
  `IsTrimmable` / `IsAotCompatible`, references `Core` + `System.Text.Json`).
- New `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/`.
- `Core`: add `Serialization/ISerde.cs`; add `RequestBody.FromValue`; add the `ResponseBody`
  extension; add `HttpResponseException.GetErrorAsync<T>`. No new `Core` dependency (the seam is
  BCL-only).
- `Directory.Packages.props`: add `System.Text.Json` (and
  `Microsoft.Extensions.DependencyInjection.Abstractions` if the registration helper ships in the STJ
  package).
- Apply the platform spec's multi-target + AOT flags to the new projects.

## 7. Testing

- Seam contract, via a hand-written fake `ISerde` and the STJ impl: round-trip; context-resolved
  types; unknown-type error message; lenient unknown-member reads; `DefaultMediaType`; native
  exception → `SerializationException` / `DeserializationException` mapping.
- Conveniences: `FromValue` is replayable and stamps the right content-type; `ReadValueAsync` is
  single-use.
- Error accessor: succeeds against a buffered error body; throws `ResponseNotReadException` on a
  consumed body.
- AOT: a NativeAOT publish smoke test in CI — compile a small consumer with a source-gen context and
  assert no trim/AOT warnings (can follow as a CI task).

## 8. Open items (resolve during planning)

- Registration-helper location: the STJ package (referencing
  `Microsoft.Extensions.DependencyInjection.Abstractions`) vs. `Extensions.DependencyInjection`.
  Leaning toward the STJ package, per the per-package `AddX` convention.
- Final seam namespace (`Dexpace.Sdk.Core.Serialization` proposed).
- Whether the synchronous `IBufferWriter<byte>` / `ReadOnlySpan<byte>` members are needed in v1 or
  wait until a synchronous caller exists (YAGNI check).
