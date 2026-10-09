// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// SERDE-14 / P3 (design position B): <see cref="Tristate{T}"/> is a plain value type in core and carries no codec
/// attribute. Its System.Text.Json wiring lives entirely in the adapter, so a different codec (the unscheduled Newtonsoft
/// adapter) is not forced to fight an attribute it did not ask for. The adapter reaches the closed type through
/// <see cref="ITristate"/> and <see cref="ITristateVisitor{TResult}"/>, not through an attribute or a core reference.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TristateArchitectureTests
{
    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type[] s_tristateTypes =
    [
        typeof(Tristate<>),
        typeof(Tristate),
        typeof(TristateSentinel),
        typeof(TristateState),
        typeof(ITristate),
        typeof(ITristateVisitor<>),
    ];

    [Fact]
    public void Tristate_types_carry_no_System_Text_Json_attribute()
    {
        var offenders = new List<string>();
        foreach (var type in s_tristateTypes)
        {
            Collect(type.Name, type.GetCustomAttributesData(), offenders);
            foreach (var member in type.GetMembers(AllMembers))
            {
                Collect(type.Name + "." + member.Name, member.GetCustomAttributesData(), offenders);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "P3: a Tristate type carries a System.Text.Json attribute (" + string.Join(", ", offenders) + "). The "
            + "converter and the property modifier live in Dexpace.Sdk.Serialization.SystemTextJson, never on the core type.");
    }

    [Fact]
    public void Core_references_no_System_Text_Json_assembly()
    {
        // SEAM-1's cousin (Seam1ArchitectureTests): the Tristate-specific evidence that the wiring is not smuggled in by a
        // reference from the type's own assembly.
        var referenced = typeof(Tristate<>).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("System.Text.Json", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(referenced);
    }

    [Fact]
    public void Tristate_of_T_is_a_readonly_struct_and_not_a_record()
    {
        var type = typeof(Tristate<>);

        Assert.True(type.IsValueType);
        Assert.Contains(type.GetCustomAttributesData(), a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
        Assert.All(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public), f => Assert.True(f.IsInitOnly, f.Name));

        // A record struct would synthesise these (P7a-3): a printing ToString over private fields and a public positional ctor.
        Assert.Null(type.GetProperty("EqualityContract", AllMembers));
        Assert.Null(type.GetMethod("PrintMembers", AllMembers));
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    private static void Collect(string owner, IEnumerable<CustomAttributeData> attributes, List<string> offenders)
    {
        foreach (var attribute in attributes)
        {
            var ns = attribute.AttributeType.Namespace ?? string.Empty;
            if (ns.StartsWith("System.Text.Json", StringComparison.Ordinal))
            {
                offenders.Add(owner + " -> " + attribute.AttributeType.FullName);
            }
        }
    }
}
