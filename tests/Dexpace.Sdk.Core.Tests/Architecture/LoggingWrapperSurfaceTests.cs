// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// BODY-37, the wrapper half: a logging wrapper exposes no handle onto its tap or capture buffer, only copying snapshots.
/// The tee's own refusal to hand out its tap is phase 3a's IO-28.
/// </summary>
[Trait("Category", "Unit")]
public class LoggingWrapperSurfaceTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // The documented copying snapshots, and the two opens the ResponseBody base type requires (they serve a view over the
    // capture, never the capture buffer itself).
    private static readonly HashSet<string> s_allowed = new(StringComparer.Ordinal)
    {
        "Snapshot", "SnapshotAsync", "OpenRead", "OpenReadAsync",
    };

    public static TheoryData<Type> Wrappers() => [typeof(LoggingRequestBody), typeof(LoggingResponseBody)];

    private static bool ReturnsBuffer(Type type)
    {
        var inner = type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() ?? type : type;
        return type == typeof(Stream) || inner == typeof(Stream) || type == typeof(Memory<byte>) || type == typeof(ReadOnlyMemory<byte>)
            || type == typeof(Span<byte>) || type == typeof(byte[]) || inner == typeof(byte[]);
    }

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void No_member_of_either_wrapper_returns_Stream_Memory_or_byte_array_other_than_Snapshot(Type wrapper)
    {
        var offenders = wrapper.GetMembers(Declared)
            .Where(m => !s_allowed.Contains(m.Name))
            .Where(m => m switch
            {
                MethodInfo method => !method.IsPrivate && !method.IsSpecialName && ReturnsBuffer(method.ReturnType),
                PropertyInfo property => ReturnsBuffer(property.PropertyType),
                FieldInfo field => ReturnsBuffer(field.FieldType) && !field.IsPrivate,
                _ => false,
            })
            .Select(m => $"{wrapper.Name}.{m.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void The_snapshot_members_return_byte_arrays_never_a_live_view(Type wrapper)
    {
        var snapshots = wrapper.GetMethods(Declared).Where(m => m.Name is "Snapshot" or "SnapshotAsync").ToList();

        Assert.NotEmpty(snapshots);
        Assert.All(snapshots, m => Assert.True(
            m.ReturnType == typeof(byte[]) || m.ReturnType == typeof(Task<byte[]>),
            $"{m.Name} must return a copying byte[] snapshot."));
    }
}
