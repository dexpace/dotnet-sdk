// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json.Serialization;

namespace Dexpace.Sdk.AotSmoke;

/// <summary>The payload the pipeline round trip serializes and reads back.</summary>
internal sealed record Widget(string Name, int Size);

/// <summary>Source-generated metadata: the only serialization path that survives NativeAOT.</summary>
[JsonSerializable(typeof(Widget))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;

/// <summary>A failed smoke check; its message names the check.</summary>
internal sealed class SmokeFailureException(string message) : Exception(message);
