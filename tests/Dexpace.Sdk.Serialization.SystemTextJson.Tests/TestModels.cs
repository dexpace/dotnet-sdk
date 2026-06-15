// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json.Serialization;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

public sealed record Widget(string Name, int Size);

public sealed record ApiError(string Code, string Message);

/// <summary>A linked-list node used to test reference-cycle serialization failures.</summary>
public sealed class Node
{
    public Node? Next { get; set; }
}

[JsonSerializable(typeof(Widget))]
[JsonSerializable(typeof(ApiError))]
[JsonSerializable(typeof(Node))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
