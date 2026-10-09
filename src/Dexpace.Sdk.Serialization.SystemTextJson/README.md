# Dexpace.Sdk.Serialization.SystemTextJson

The `System.Text.Json` implementation of the
[`Dexpace.Sdk.Core`](https://www.nuget.org/packages/Dexpace.Sdk.Core) `ISerde` serialization seam.
`SystemTextJsonSerde` resolves type metadata from a source-generated `JsonSerializerContext`, so
serialization is trim-safe and NativeAOT-safe, with no runtime reflection.

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`.

## Behaviour

- **Source generation.** Build the serde from a `JsonSerializerContext`, or from `JsonSerializerOptions` whose
  `TypeInfoResolver` is set; options without a resolver are rejected. The serde works on a private copy: your
  `JsonSerializerOptions` is never made read-only and never gains a converter, and a later change to it does not
  affect the serde.
- **Recommended options.** `SystemTextJsonSerde.CreateDefaultOptions(MyContext.Default)` returns a fresh, mutable
  instance with `Web` naming (camelCase, case-insensitive reads), strict numbers (`"5"` does not bind to an `int`, an
  integer widens to a `double`) and `RespectNullableAnnotations` on: pass it to the constructor.
- **`Tristate<T>`.** A PATCH field that is Absent (omitted from the JSON), Null (`null`) or Present is written and read
  correctly with no setup: both constructors wire it on their private copy. To serialize SDK models with
  `JsonSerializer` directly, call `options.AddTristateSupport()` after setting the resolver. Use a context generated in
  the default or `Metadata` mode; a fast-path-only (`GenerationMode = Serialization`) context carries no property
  metadata and cannot be wired.
- **Errors.** A `System.Text.Json` failure surfaces as the SDK's `SerializationException` or
  `DeserializationException`; cancellation propagates unwrapped.
- **Media type.** The default is `application/json; charset=utf-8`.

## Usage

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(AppJsonContext.Default));
await using var transport = new SystemNetHttpClient();
var pipeline = DexpacePipeline.CreateDefault(transport);

var request = Request.Post("https://api.example.com/widgets", RequestBody.FromValue(new Widget("gear", 9), serde));
using var response = await pipeline.SendAsync(request, new DexpaceClientOptions(), CancellationToken.None);
await response.EnsureSuccessAsync();
Widget? created = await response.Body.ReadValueAsync<Widget>(serde);

internal sealed record Widget(string Name, int Teeth);

[JsonSerializable(typeof(Widget))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

A typed error body is read the same way: `HttpResponseException.GetErrorAsync<TError>(serde)`.

A PATCH body distinguishes "leave it alone" from "clear it":

```csharp
public sealed record WidgetPatch(Tristate<string> Name, Tristate<int> Teeth);

var patch = new WidgetPatch(Name: Tristate.Null, Teeth: Tristate.Present(12));   // {"name":null,"teeth":12}
var untouched = patch with { Name = Tristate.Absent };                            // {"teeth":12}
```

## Links

- Repository and design: <https://github.com/dexpace/dotnet-sdk>
- License: MIT
