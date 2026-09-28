// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;

namespace Dexpace.Sdk.TestSupport;

/// <summary>
/// The <c>[Trait("Category", …)]</c> values of design §9.3 and roadmap constraint 4, and an audit that finds a test
/// carrying none of them. Categories are what a filter selects on (<c>--filter-trait "Category=Integration"</c>), so an
/// uncategorised test would silently drop out of every filtered run.
/// </summary>
/// <remarks>
/// Framework-free: the audit reads attributes by name through <see cref="CustomAttributeData"/>, so this project
/// takes no xUnit dependency.
/// </remarks>
public static class TestCategories
{
    /// <summary>The trait name every test carries.</summary>
    public const string Key = "Category";

    /// <summary>In-memory fakes and TimeProvider fakes; no socket. The large majority.</summary>
    public const string Unit = "Unit";

    /// <summary>A real transport against the loopback server.</summary>
    public const string Integration = "Integration";

    /// <summary>The transport conformance kit (roadmap phase 8).</summary>
    public const string Conformance = "Conformance";

    /// <summary>The NativeAOT smoke consumer's checks.</summary>
    public const string AotSmoke = "AotSmoke";

    /// <summary>A permanent regression test of a phase-1 security fix (roadmap constraint 5).</summary>
    public const string Security = "Security";

    /// <summary>Every category, in the order design §9.3 lists them.</summary>
    public static IReadOnlyList<string> All { get; } = [Unit, Integration, Conformance, AotSmoke, Security];

    /// <summary>
    /// The tests in <paramref name="testAssembly"/> (methods carrying xUnit's <c>[Fact]</c> or an attribute derived from
    /// it, such as <c>[Theory]</c>) with no <c>Category</c> trait from <see cref="All"/> on the method, its class or a
    /// base class, as <c>Type.Method</c> names.
    /// </summary>
    /// <param name="testAssembly">The test assembly to audit.</param>
    public static IReadOnlyList<string> FindUncategorized(Assembly testAssembly)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;

        return testAssembly.GetTypes()
            .SelectMany(type => type.GetMethods(Declared).Select(method => (Type: type, Method: method)))
            .Where(test => test.Method.CustomAttributes.Any(IsFact))
            .Where(test => !HasCategory(test.Method) && !TypeHasCategory(test.Type))
            .Select(test => $"{test.Type.FullName}.{test.Method.Name}")
            .ToArray();
    }

    private static bool IsFact(CustomAttributeData attribute)
    {
        for (var type = attribute.AttributeType; type is not null; type = type.BaseType)
        {
            if (type.FullName == "Xunit.FactAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static bool TypeHasCategory(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (HasCategory(current))
            {
                return true;
            }
        }

        return type.DeclaringType is { } outer && TypeHasCategory(outer);
    }

    private static bool HasCategory(MemberInfo member) =>
        member.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == "Xunit.TraitAttribute"
            && attribute.ConstructorArguments is [{ Value: Key }, { Value: string value }]
            && All.Contains(value));
}
