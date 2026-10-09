// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json.Serialization.Metadata;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Serialization.SystemTextJson;

/// <summary>
/// The <see cref="JsonTypeInfo"/> modifier that omits an Absent <see cref="Tristate{T}"/> property from the output
/// (SERDE-15). A converter cannot omit a property; <see cref="JsonPropertyInfo.ShouldSerialize"/> can (design fact 2).
/// </summary>
/// <remarks>
/// The predicate is ANDed with any <see cref="JsonPropertyInfo.ShouldSerialize"/> already set, so a caller's own filter is
/// kept (P7a-4). A property the modifier has already wired is recognised by its delegate's target and left alone, so
/// applying the modifier twice (a caller who calls <c>AddTristateSupport</c> and then constructs a serde) does not stack.
/// The value reaches the predicate boxed, once per Tristate property per write (risk R5, accepted).
/// </remarks>
internal static class TristateModifier
{
    internal static void Apply(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            if (!IsTristate(property.PropertyType) || property.ShouldSerialize?.Target is OmitAbsentFilter)
            {
                continue;
            }

            property.ShouldSerialize = new OmitAbsentFilter(property.ShouldSerialize).ShouldSerialize;
        }
    }

    private static bool IsTristate(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Tristate<>);

    private sealed class OmitAbsentFilter(Func<object, object?, bool>? prior)
    {
        internal bool ShouldSerialize(object parent, object? value) =>
            value is not ITristate { State: TristateState.Absent } && (prior is null || prior(parent, value));
    }
}
