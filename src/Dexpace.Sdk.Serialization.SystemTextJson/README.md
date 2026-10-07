# Dexpace.Sdk.Serialization.SystemTextJson

The `System.Text.Json` implementation of the
[`Dexpace.Sdk.Core`](https://www.nuget.org/packages/Dexpace.Sdk.Core) `ISerde` serialization seam.
`SystemTextJsonSerde` resolves type metadata from a source-generated `JsonSerializerContext`, so
serialization is trim-safe and NativeAOT-safe, with no runtime reflection.

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`.

## Behaviour

- **Source generation.** Build the serde from a `JsonSerializerContext`, or from `JsonSerializerOptions` whose
  `TypeInfoResolver` is set; options without a resolver are rejected. The options are made read-only.
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

var serde = new SystemTextJsonSerde(AppJsonContext.Default);
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

## Links

- Repository and design: <https://github.com/dexpace/dotnet-sdk>
- License: MIT
