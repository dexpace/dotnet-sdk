// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Operations;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// HTTP-1 and HTTP-5 (design §4; styleguide 6.6, 10.3): every HTTP model type is immutable after construction, exposes
/// only read-only collections, and is sealed. Checked by reflection over a fixed list, so a type that slips (a mutable
/// field, a plain setter, a leaked <c>List&lt;T&gt;</c>) fails here. The builders (<c>Headers.Builder</c>,
/// <c>Query.Builder</c>) are mutable by design and are excluded by name; <c>ResponseBody</c> and <c>RequestBody</c>
/// carry documented single-use state and are not model types in this sense.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ModelImmutabilityArchitectureTests
{
    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>The model types under test; the builders are excluded by name and are never listed.</summary>
    public static readonly Type[] ModelTypes =
    [
        typeof(Method),
        typeof(HttpHeaderName),
        typeof(Headers),
        typeof(Query),
        typeof(MediaType),
        typeof(Status),
        typeof(Request),
        typeof(Response),
        typeof(RequestOptions),
        typeof(RequestConditions),
        typeof(ETag),
        typeof(HttpRange),
        typeof(OperationDescriptor),
    ];

    public static TheoryData<Type> Models()
    {
        var data = new TheoryData<Type>();
        foreach (var type in ModelTypes)
        {
            data.Add(type);
        }

        return data;
    }

    [Fact]
    public void The_builders_are_excluded_by_name_and_are_not_in_the_list()
    {
        Assert.DoesNotContain(ModelTypes, t => t.Name == "Builder" || t.IsNested);
        Assert.Contains(typeof(Headers).GetNestedTypes().Select(t => t.Name), name => name == "Builder");
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Every_instance_field_is_readonly_except_the_documented_body_state(Type type)
    {
        // Allow-list: a new entry here is a reviewed decision. Response._disposed is the idempotent-close latch
        // (HTTP-43, P3b-2, phase 3b); every other model type has no mutable instance field.
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "Response._disposed" };

        var mutable = type.GetFields(Declared)
            .Where(field => !field.IsInitOnly)
            .Select(field => $"{type.Name}.{field.Name}")
            .Where(name => !allowed.Contains(name))
            .ToList();

        Assert.Empty(mutable);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void No_public_property_has_a_non_init_setter(Type type)
    {
        var withSetters = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(property => property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
            .Select(property => $"{type.Name}.{property.Name}")
            .ToList();

        Assert.Empty(withSetters);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Collections_exposed_are_read_only_types(Type type)
    {
        // HTTP-5: no public member returns or exposes a List<>, a Dictionary<,> or an array.
        var offenders = new List<string>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            if (IsMutableCollection(property.PropertyType))
            {
                offenders.Add($"{type.Name}.{property.Name}");
            }
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (IsMutableCollection(method.ReturnType) && !method.IsSpecialName)
            {
                offenders.Add($"{type.Name}.{method.Name}()");
            }
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            if (IsMutableCollection(field.FieldType))
            {
                offenders.Add($"{type.Name}.{field.Name}");
            }
        }

        Assert.Empty(offenders);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Every_model_type_is_sealed(Type type)
    {
        // Styleguide 6.6: sealed by default. A value type is implicitly sealed.
        Assert.True(type.IsSealed, $"{type.Name} must be sealed.");
    }

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static bool IsMutableCollection(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>) || definition == typeof(Dictionary<,>) || definition == typeof(HashSet<>);
    }
}
