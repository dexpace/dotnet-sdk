// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.AotSmoke;

/// <summary>The payload the pipeline round trip serializes and reads back.</summary>
internal sealed record Widget(string Name, int Size);

/// <summary>The PATCH payload of the phase 7a check: two reference-type fields and the value-type <c>Tristate&lt;int&gt;</c>.</summary>
internal sealed record WidgetPatch(Tristate<string> Name, Tristate<int> Size, Tristate<string> Note);

/// <summary>Source-generated metadata: the only serialization path that survives NativeAOT.</summary>
[JsonSerializable(typeof(Widget))]
[JsonSerializable(typeof(WidgetPatch))]
[JsonSerializable(typeof(Tristate<string>))]
[JsonSerializable(typeof(Tristate<int>))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;

/// <summary>A failed smoke check; its message names the check.</summary>
internal sealed class SmokeFailureException(string message) : Exception(message);
