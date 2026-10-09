// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

/// <summary>The PATCH model of the phase 7a smoke: three Tristate fields of two element types (string and the value type int).</summary>
public sealed record WidgetPatch(Tristate<string> Name, Tristate<int> Size, Tristate<string> Note);

/// <summary>A class with init-only Tristate properties and object and list element types.</summary>
public sealed class NestedPatch
{
    public Tristate<Widget> Inner { get; init; }

    public Tristate<List<Widget>> Many { get; init; }
}

/// <summary>A model that holds a Tristate-bearing DTO directly and inside a Present.</summary>
public sealed record Holder(WidgetPatch Patch, Tristate<WidgetPatch> NestedTristate);

/// <summary>Three fields for the mixed-document decode (<c>{"B":null,"C":"v"}</c> gives Absent, Null, Present).</summary>
public sealed record Shaped(Tristate<string> A, Tristate<string> B, Tristate<string> C);

/// <summary>A dictionary of Tristate values: an Absent value degrades to <c>null</c> (SERDE-20).</summary>
public sealed record DictHolder(Dictionary<string, Tristate<string>> Map);

/// <summary>An array of Tristate values: an Absent element degrades to <c>null</c> and indices never shift (SERDE-20).</summary>
public sealed record ArrayHolder(Tristate<string>[] Items);

/// <summary>A non-nullable member, for <c>RespectNullableAnnotations</c>.</summary>
public sealed record NonNullable(string Required);

/// <summary>A property whose JSON name is the empty string.</summary>
public sealed class EmptyName
{
    [JsonPropertyName("")]
    public Tristate<string> Weird { get; init; }
}

/// <summary>An element type whose test converter returns <see langword="null"/> for a non-null token.</summary>
public sealed class Quirky;

/// <summary>Holds a <see cref="Tristate{T}"/> of <see cref="Quirky"/>.</summary>
public sealed record QuirkHolder(Tristate<Quirky> Q);

/// <summary>A converter that violates the contract: a non-null token decodes to <see langword="null"/>.</summary>
public sealed class NullReturningQuirkyConverter : JsonConverter<Quirky>
{
    public override Quirky? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Quirky value, JsonSerializerOptions options) => writer.WriteNullValue();
}

/// <summary>
/// The default-generation-mode context every Tristate test runs over (design fact 8 / R1): no <c>GenerationMode</c>,
/// so the fast path is the one the modifier has to defeat.
/// </summary>
[JsonSerializable(typeof(WidgetPatch))]
[JsonSerializable(typeof(NestedPatch))]
[JsonSerializable(typeof(Holder))]
[JsonSerializable(typeof(Shaped))]
[JsonSerializable(typeof(DictHolder))]
[JsonSerializable(typeof(ArrayHolder))]
[JsonSerializable(typeof(NonNullable))]
[JsonSerializable(typeof(EmptyName))]
[JsonSerializable(typeof(QuirkHolder))]
[JsonSerializable(typeof(Quirky))]
[JsonSerializable(typeof(Widget))]
[JsonSerializable(typeof(List<Widget>))]
[JsonSerializable(typeof(Tristate<string>))]
[JsonSerializable(typeof(Tristate<int>))]
[JsonSerializable(typeof(Tristate<Widget>))]
[JsonSerializable(typeof(Tristate<List<Widget>>))]
[JsonSerializable(typeof(Tristate<WidgetPatch>))]
[JsonSerializable(typeof(Tristate<Quirky>))]
[JsonSerializable(typeof(Tristate<string>[]))]
[JsonSerializable(typeof(Dictionary<string, Tristate<string>>))]
internal sealed partial class TristateTestContext : JsonSerializerContext;

/// <summary>The same models under <c>Metadata</c> generation only: the R1 fallback of design fact 8.</summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WidgetPatch))]
[JsonSerializable(typeof(Tristate<string>))]
[JsonSerializable(typeof(Tristate<int>))]
internal sealed partial class TristateMetadataContext : JsonSerializerContext;

/// <summary>The same models under the fast-path-only <c>Serialization</c> mode, which carries no property metadata.</summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(WidgetPatch))]
[JsonSerializable(typeof(Tristate<string>))]
[JsonSerializable(typeof(Tristate<int>))]
internal sealed partial class TristateFastPathContext : JsonSerializerContext;
