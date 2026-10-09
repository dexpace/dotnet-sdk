// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Serialization.SystemTextJson;

/// <summary>
/// Wires <see cref="Tristate{T}"/> into a <see cref="JsonSerializerOptions"/> used directly with
/// <see cref="System.Text.Json.JsonSerializer"/> (SERDE-19, P7a-4).
/// </summary>
/// <remarks>
/// <see cref="SystemTextJsonSerde"/> does this on its own private copy of the options, always. This extension is for the
/// caller who serializes SDK models with <see cref="System.Text.Json.JsonSerializer"/> themselves (an ASP.NET endpoint, a
/// test): without it, an Absent field would serialize as an object holding the struct's public properties, which is exactly
/// the silent PATCH corruption SERDE-19 warns of.
/// </remarks>
public static class TristateJsonSerializerOptionsExtensions
{
    /// <summary>
    /// Registers the <see cref="Tristate{T}"/> converter factory (unless one is already present) and wraps the options'
    /// resolver with the modifier that omits an Absent property.
    /// </summary>
    /// <param name="options">The options to wire. Mutated in place.</param>
    /// <remarks>
    /// <para>
    /// Call this <b>after</b> setting <see cref="JsonSerializerOptions.TypeInfoResolver"/>: the modifier wraps the resolver
    /// that is current at the time of the call, so a resolver assigned afterwards would drop it (risk R6). Calling it twice
    /// has the same effect as calling it once. A converter the caller registered for <c>Tristate&lt;&gt;</c> earlier precedes
    /// this one in <see cref="JsonSerializerOptions.Converters"/> and wins; the Absent omission applies either way.
    /// </para>
    /// <para>
    /// A <see cref="JsonSerializerContext"/> generated with <c>GenerationMode = Serialization</c> (the fast-path-only mode)
    /// carries no property metadata and cannot be wired; use the default mode or <c>Metadata</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options have no <see cref="JsonSerializerOptions.TypeInfoResolver"/>.</exception>
    /// <exception cref="InvalidOperationException">The options are already read-only.</exception>
    public static void AddTristateSupport(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.TypeInfoResolver is null)
        {
            throw new ArgumentException(
                "AddTristateSupport needs a JsonSerializerOptions.TypeInfoResolver (for example, a source-generated "
                + "JsonSerializerContext): the modifier that omits an Absent property wraps it. Set the resolver first.",
                nameof(options));
        }

        if (!options.Converters.Any(static converter => converter is TristateConverterFactory))
        {
            options.Converters.Add(new TristateConverterFactory());
        }

        options.TypeInfoResolver = options.TypeInfoResolver.WithAddedModifier(TristateModifier.Apply);
    }
}
