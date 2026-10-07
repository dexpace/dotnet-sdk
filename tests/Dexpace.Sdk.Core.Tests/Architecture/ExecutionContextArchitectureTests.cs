// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Execution;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// CTX-1, CTX-7, P4a-5, P4a-7: the context chain is immutable, closed to three sealed flavours, one-way, and not
/// disposable. Checked by reflection, in the shape of <see cref="ModelImmutabilityArchitectureTests"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExecutionContextArchitectureTests
{
    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Type[] s_contextTypes =
    [
        typeof(CallContext),
        typeof(DispatchContext),
        typeof(RequestContext),
        typeof(ExchangeContext),
        typeof(InstrumentationContext),
    ];

    [Fact]
    public void Every_instance_field_is_readonly()
    {
        var mutable = s_contextTypes
            .SelectMany(type => type.GetFields(Declared).Select(field => (type, field)))
            .Where(pair => !pair.field.IsInitOnly)
            .Select(pair => pair.type.Name + "." + pair.field.Name)
            .ToList();

        Assert.Empty(mutable);
        Assert.Contains(typeof(CallContext).GetFields(Declared), field => field.Name == "_store" && field.IsInitOnly);
    }

    [Fact]
    public void No_property_has_a_public_setter()
    {
        var settable = s_contextTypes
            .SelectMany(type => type.GetProperties(Declared).Select(property => (type, property)))
            .Where(pair => pair.property.SetMethod is { IsPublic: true })
            .Select(pair => pair.type.Name + "." + pair.property.Name)
            .ToList();

        Assert.Empty(settable);
    }

    [Fact]
    public void The_hierarchy_has_exactly_three_concrete_flavours()
    {
        var concrete = typeof(CallContext).Assembly.GetTypes()
            .Where(type => typeof(CallContext).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        Assert.Equivalent(new[] { typeof(DispatchContext), typeof(RequestContext), typeof(ExchangeContext) }, concrete);
        Assert.All(concrete, type => Assert.True(type.IsSealed));
    }

    [Fact]
    public void The_hierarchy_is_closed_by_an_internal_abstract_member()
    {
        var closing = typeof(CallContext).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.IsAbstract && method.IsAssembly);

        Assert.NotEmpty(closing);
    }

    [Fact]
    public void The_exchange_context_declares_no_method_returning_a_context()
    {
        var offenders = typeof(ExchangeContext).GetMethods(Declared)
            .Where(method => !method.IsSpecialName && !method.Name.StartsWith('<'))
            .Where(method => typeof(CallContext).IsAssignableFrom(method.ReturnType))
            .Select(method => method.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Promotion_methods_exist_only_on_dispatch_and_request()
    {
        var promoters = s_contextTypes
            .SelectMany(type => type.GetMethods(Declared).Select(method => (type, method)))
            .Where(pair => pair.method.Name.StartsWith("PromoteTo", StringComparison.Ordinal))
            .Select(pair => pair.type.Name + "." + pair.method.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["DispatchContext.PromoteToRequest", "RequestContext.PromoteToExchange"], promoters);
    }

    [Fact]
    public void Contexts_are_not_disposable()
    {
        Assert.All(s_contextTypes, type =>
        {
            Assert.False(typeof(IDisposable).IsAssignableFrom(type));
            Assert.False(typeof(IAsyncDisposable).IsAssignableFrom(type));
        });
    }
}
