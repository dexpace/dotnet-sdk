// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// CFG-8, P5a-3: the options-record rule (design 5a position A, rules A.1 to A.5) holds for every public type in
/// <c>Dexpace.Sdk.Core.Configuration</c>: records are sealed, no property has a public setter other than
/// <see langword="init"/>, and no collection member is a mutable collection type. 5b's <c>HttpLoggingOptions</c> is
/// bound by it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OptionsRecordArchitectureTests
{
    private static IEnumerable<Type> ConfigurationTypes() =>
        typeof(DexpaceClientOptions).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == "Dexpace.Sdk.Core.Configuration");

    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;

    private static bool HasPublicSetter(PropertyInfo property) =>
        property.SetMethod is { IsPublic: true } setter
        && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

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
        return definition == typeof(List<>) || definition == typeof(Dictionary<,>) || definition == typeof(HashSet<>)
            || definition == typeof(ICollection<>) || definition == typeof(IList<>) || definition == typeof(IDictionary<,>);
    }

    private static PropertyInfo[] PublicProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

    [Fact]
    public void Every_record_in_the_Configuration_namespace_is_sealed()
    {
        var open = ConfigurationTypes().Where(type => IsRecord(type) && !type.IsSealed).Select(type => type.Name).ToList();

        Assert.Empty(open);
        Assert.Contains(ConfigurationTypes(), IsRecord);
    }

    [Fact]
    public void No_public_property_in_the_Configuration_namespace_has_a_public_setter()
    {
        var settable = ConfigurationTypes()
            .SelectMany(type => PublicProperties(type).Where(HasPublicSetter).Select(p => type.Name + "." + p.Name))
            .ToList();

        Assert.Empty(settable);
    }

    [Fact]
    public void No_public_collection_member_is_a_mutable_collection_type()
    {
        var mutable = ConfigurationTypes()
            .SelectMany(type => PublicProperties(type).Where(p => IsMutableCollection(p.PropertyType)).Select(p => type.Name + "." + p.Name))
            .ToList();

        Assert.Empty(mutable);
    }

    [Fact]
    public void The_scanner_recognises_a_public_setter_a_List_and_an_array()
    {
        Assert.True(HasPublicSetter(typeof(Violations).GetProperty(nameof(Violations.Settable))!));
        Assert.False(HasPublicSetter(typeof(Violations).GetProperty(nameof(Violations.InitOnly))!));
        Assert.True(IsMutableCollection(typeof(Violations).GetProperty(nameof(Violations.Items))!.PropertyType));
        Assert.True(IsMutableCollection(typeof(Violations).GetProperty(nameof(Violations.Bytes))!.PropertyType));
        Assert.False(IsMutableCollection(typeof(IReadOnlyList<string>)));
        Assert.True(IsRecord(typeof(RetryOptions)));
        Assert.False(IsRecord(typeof(Violations)));
    }

    private sealed class Violations
    {
        public int Settable { get; set; }

        public int InitOnly { get; init; }

        public List<string> Items { get; init; } = [];

        public byte[] Bytes { get; init; } = [];
    }
}
