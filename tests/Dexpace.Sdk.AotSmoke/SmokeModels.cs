// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.AotSmoke;

/// <summary>The payload the pipeline round trip serializes and reads back.</summary>
internal sealed record Widget(string Name, int Size);

/// <summary>The PATCH payload of the phase 7a check: two reference-type fields and the value-type <c>Tristate&lt;int&gt;</c>.</summary>
internal sealed record WidgetPatch(Tristate<string> Name, Tristate<int> Size, Tristate<string> Note);

/// <summary>The model the server-sent-events check maps each event to; the wire name is lower-case.</summary>
internal sealed record SmokeChunk([property: JsonPropertyName("name")] string Name);

/// <summary>Source-generated metadata: the only serialization path that survives NativeAOT.</summary>
[JsonSerializable(typeof(Widget))]
[JsonSerializable(typeof(WidgetPatch))]
[JsonSerializable(typeof(Tristate<string>))]
[JsonSerializable(typeof(Tristate<int>))]
[JsonSerializable(typeof(SmokeChunk))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;

/// <summary>The page envelope of the paged round trip (phase 7c): a list of widgets and the next cursor.</summary>
internal sealed record WidgetPage(List<Widget> Items, string? Next);

/// <summary>Source-generated metadata for the paged round trip, in its own context so the shared one is not edited.</summary>
[JsonSerializable(typeof(WidgetPage))]
internal sealed partial class PaginationSmokeContext : JsonSerializerContext;

/// <summary>A failed smoke check; its message names the check.</summary>
internal sealed class SmokeFailureException(string message) : Exception(message);
