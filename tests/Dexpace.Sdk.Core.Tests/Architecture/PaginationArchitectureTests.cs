// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// The shape rules of the pagination namespace (design section 10 entry 17; PAGE-2, PAGE-3, PAGE-11, PAGE-14, PAGE-35),
/// checked by reflection so a change that slips fails here: a page owns no response, the single-use and flattening
/// guarantees cannot be overridden away, no public member leaks a <see cref="Response"/> except the two that must, and the
/// value types of the namespace stay immutable.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PaginationArchitectureTests
{
    private const string Namespace = "Dexpace.Sdk.Core.Pagination";

    private static readonly Assembly s_core = typeof(Page<>).Assembly;

    private static IEnumerable<Type> PublicTypes() =>
        s_core.GetExportedTypes().Where(t => t.Namespace == Namespace);

    private static readonly string[] s_valueTypeNames = ["Page`1", "PageInfo`1", "FetchedPage`1"];

    public static TheoryData<string> ValueTypeNames() => [.. s_valueTypeNames];

    private static Type ValueType(string metadataName) =>
        s_core.GetType(Namespace + "." + metadataName) ?? throw new InvalidOperationException($"{metadataName} is missing from the public surface.");

    [Theory]
    [MemberData(nameof(ValueTypeNames))]
    public void A_page_value_owns_no_response_so_it_is_neither_IDisposable_nor_IAsyncDisposable(string name)
    {
        var type = ValueType(name);

        Assert.False(typeof(IDisposable).IsAssignableFrom(type), $"{type.Name} must not be IDisposable (entry 17).");
        Assert.False(typeof(IAsyncDisposable).IsAssignableFrom(type), $"{type.Name} must not be IAsyncDisposable (entry 17).");
    }

    [Theory]
    [MemberData(nameof(ValueTypeNames))]
    public void A_page_value_exposes_no_public_setter_and_no_mutable_collection(string name)
    {
        var type = ValueType(name);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var setter = property.SetMethod;
            Assert.True(
                setter is null || setter.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"),
                $"{type.Name}.{property.Name} has a setter that is not init.");
            Assert.False(IsMutableCollection(property.PropertyType), $"{type.Name}.{property.Name} is a mutable collection.");
        }

        Assert.All(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), f => Assert.True(f.IsInitOnly, $"{type.Name}.{f.Name} is not readonly."));
    }

    // PagingOptions is excluded by name from the two rules above: it is mutable on purpose (PAGE-35). The spec asks for one
    // mutable instance per walk, written by the engine before each next call and readable by custom retrievers; a fresh
    // instance per walk keeps PAGE-8's independent re-iteration true. It is per-walk scratch, never a page value.
    [Fact]
    public void PagingOptions_is_the_one_documented_mutable_type_and_is_not_in_the_value_rules()
    {
        var type = s_core.GetType(Namespace + ".PagingOptions")!;

        Assert.NotNull(type.GetProperty("NextLink")!.SetMethod);
        Assert.NotNull(type.GetProperty("ContinuationToken")!.SetMethod);
        Assert.DoesNotContain("PagingOptions", s_valueTypeNames);
    }

    [Fact]
    public void The_view_and_enumerator_members_are_not_virtual_so_the_guarantees_hold_for_every_subclass()
    {
        AssertNotOverridable(typeof(AsyncPageable<>), nameof(AsyncPageable<int>.AsPages));
        AssertNotOverridable(typeof(AsyncPageable<>), nameof(AsyncPageable<int>.GetAsyncEnumerator));
        AssertNotOverridable(typeof(Pageable<>), nameof(Pageable<int>.AsPages));
        AssertNotOverridable(typeof(Pageable<>), nameof(Pageable<int>.GetEnumerator));
    }

    // An implicit interface implementation is `virtual final` in IL, so "not overridable" is not-virtual or final.
    private static void AssertNotOverridable(Type type, string method)
    {
        var member = type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance)!;
        Assert.True(!member.IsVirtual || member.IsFinal, $"{type.Name}.{method} must not be overridable.");
    }

    [Fact]
    public void The_walk_hooks_are_protected_abstract()
    {
        var async = typeof(AsyncPageable<>).GetMethod("WalkPagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var blocking = typeof(Pageable<>).GetMethod("WalkPages", BindingFlags.NonPublic | BindingFlags.Instance)!;

        Assert.True(async.IsFamily && async.IsAbstract);
        Assert.True(blocking.IsFamily && blocking.IsAbstract);
    }

    [Fact]
    public void No_public_member_of_the_namespace_exposes_a_Response_except_the_two_that_must()
    {
        // A page owns no response; the strategy and the delegate adapter are the only places a response is handed over.
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "IPageStrategy`2.Parse", "PaginationStrategies.Create" };
        var leaks = new List<string>();
        foreach (var type in PublicTypes())
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var isPublicSurface = member switch
                {
                    MethodBase m => m.IsPublic || m.IsFamily,
                    PropertyInfo p => (p.GetMethod?.IsPublic ?? false) || (p.GetMethod?.IsFamily ?? false),
                    _ => false,
                };
                if (isPublicSurface && Mentions(member, typeof(Response)) && !allowed.Contains($"{type.Name}.{member.Name}"))
                {
                    leaks.Add($"{type.Name}.{member.Name}");
                }
            }
        }

        Assert.Empty(leaks);
    }

    private static bool Mentions(MemberInfo member, Type target) => member switch
    {
        MethodBase m => m.GetParameters().Select(p => p.ParameterType).Append((m as MethodInfo)?.ReturnType ?? typeof(void)).Any(t => Contains(t, target)),
        PropertyInfo p => Contains(p.PropertyType, target),
        _ => false,
    };

    private static bool Contains(Type type, Type target) =>
        type == target
        || (type.HasElementType && Contains(type.GetElementType()!, target))
        || (type.IsGenericType && type.GetGenericArguments().Any(a => Contains(a, target)));

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
