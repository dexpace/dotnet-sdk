// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Execution;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// P4a-11 (design §5.4 P13): the SDK holds no ambient per-call state. The `RS0030` ban on <c>AsyncLocal&lt;T&gt;</c> is
/// the compile-time layer; this reflection scan over every field of every type in the library assembly (nested and
/// compiler-generated included) is the second layer. <c>Activity.Current</c> stays the one ambient value (the runtime's).
/// </summary>
[Trait("Category", "Unit")]
public sealed class NoAmbientStateArchitectureTests
{
    private const BindingFlags AllFields =
        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void No_field_in_the_library_assembly_is_an_AsyncLocal()
    {
        var offenders = typeof(CallKey).Assembly.GetTypes()
            .SelectMany(type => type.GetFields(AllFields).Select(field => (type, field)))
            .Where(pair => MentionsAsyncLocal(pair.field.FieldType))
            .Select(pair => pair.type.FullName + "." + pair.field.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_scanner_recognises_a_plain_nested_and_array_AsyncLocal_field_type()
    {
        Assert.True(MentionsAsyncLocal(typeof(AsyncLocal<int>)));
        Assert.True(MentionsAsyncLocal(typeof(List<AsyncLocal<string>>)));
        Assert.True(MentionsAsyncLocal(typeof(AsyncLocal<object>[])));
        Assert.False(MentionsAsyncLocal(typeof(List<int>)));
    }

    private static bool MentionsAsyncLocal(Type type)
    {
        if (type.HasElementType)
        {
            return MentionsAsyncLocal(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            return type.GetGenericTypeDefinition() == typeof(AsyncLocal<>) || type.GetGenericArguments().Any(MentionsAsyncLocal);
        }

        return false;
    }
}
