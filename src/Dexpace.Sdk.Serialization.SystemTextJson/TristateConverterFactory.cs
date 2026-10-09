// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Serialization.SystemTextJson;

/// <summary>
/// Creates a <see cref="TristateConverter{T}"/> for every closed <see cref="Tristate{T}"/> (SERDE-16), without
/// <c>MakeGenericType</c> or <c>Activator</c>, so the path is NativeAOT-safe (P7a-6, NFR-9).
/// </summary>
/// <remarks>
/// The closed type reaches generic code through core's <see cref="ITristate"/> / <see cref="ITristateVisitor{TResult}"/>
/// double dispatch: a default (Absent) instance of the closed struct is created, boxed as <see cref="ITristate"/>, and
/// asked to <see cref="ITristate.Accept{TResult}"/> a visitor whose <c>Visit&lt;T&gt;</c> is statically typed.
/// </remarks>
internal sealed class TristateConverterFactory : JsonConverterFactory
{
    private static readonly ConverterVisitor s_visitor = new();

    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Tristate<>);

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "typeToConvert is a closed Tristate<> constructed by the caller's source-generated metadata, and a "
                      + "Tristate<> has no constructor to preserve: an uninitialised (default, i.e. Absent) instance is all "
                      + "that is needed. The AOT smoke consumer's value-type Tristate<int> proves it (NFR-9, P7a-6).")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var boxed = (ITristate)RuntimeHelpers.GetUninitializedObject(typeToConvert);
        return boxed.Accept(s_visitor);
    }

    private sealed class ConverterVisitor : ITristateVisitor<JsonConverter>
    {
        public JsonConverter Visit<T>(Tristate<T> value)
            where T : notnull => new TristateConverter<T>();
    }
}
