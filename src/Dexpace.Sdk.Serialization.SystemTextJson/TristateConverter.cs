// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Serialization.SystemTextJson;

/// <summary>
/// The System.Text.Json converter for one closed <see cref="Tristate{T}"/> (SERDE-16, SERDE-20): a JSON <c>null</c> is Null,
/// any other token is decoded through the element type's <see cref="JsonTypeInfo{T}"/> and is Present.
/// </summary>
/// <typeparam name="T">The wrapped value type.</typeparam>
/// <remarks>
/// <para>
/// <see cref="HandleNull"/> is <see langword="true"/> explicitly, so the converter sees the <c>null</c> token at the root,
/// in an array and as a property value (design fact 9, verified in the phase's pre-flight). A property whose key is missing
/// never reaches the converter: the member keeps <c>default</c>, which is Absent (SERDE-17).
/// </para>
/// <para>
/// A property-held Absent is omitted by <see cref="TristateModifier"/> before this converter is asked to write it; where
/// there is no property to omit (the document root, an array element, a dictionary value) the converter degrades Absent to
/// <c>null</c> (SERDE-20).
/// </para>
/// <para>
/// The element type's metadata is read from the options' resolver, so it is present in any source-generated context that
/// registers a model holding this <see cref="Tristate{T}"/> (the generator walks the struct's public <c>Value</c> property).
/// </para>
/// </remarks>
internal sealed class TristateConverter<T> : JsonConverter<Tristate<T>>
    where T : notnull
{
    // Resolved on first use, per converter instance. A benign race stores the same reference twice.
    private JsonTypeInfo<T>? _info;

    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override Tristate<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Tristate<T>.Null;
        }

        var value = JsonSerializer.Deserialize(ref reader, ElementInfo(options));
        return value is null
            ? throw new JsonException($"The JSON value for a Tristate<{typeof(T)}> decoded to null; null must be the JSON literal null.")
            : Tristate<T>.Present(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Tristate<T> value, JsonSerializerOptions options)
    {
        if (!value.IsPresent)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value.Value, ElementInfo(options));
    }

    private JsonTypeInfo<T> ElementInfo(JsonSerializerOptions options) =>
        _info ??= (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}
