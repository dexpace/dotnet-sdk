// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// SEAM-22 (design §3.4, position B): the type a codec decodes into is always the closed generic argument the CLR binds
/// at run time, so no seam member may take or return a <see cref="Type"/> — the one door through which an open generic
/// could reach a codec.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerdeSeamArchitectureTests
{
    private static readonly Type[] s_seams =
    [
        typeof(ISerde),
        typeof(IStringSerde),
        typeof(IHttpClient),
        typeof(IAsyncHttpClient),
        typeof(SerdeExtensions),
        typeof(ResponseBodySerdeExtensions),
    ];

    [Fact]
    public void No_seam_member_takes_a_System_Type()
    {
        var offenders = new List<string>();
        foreach (var seam in s_seams)
        {
            foreach (var method in seam.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var types = method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType);
                if (types.Any(MentionsType))
                {
                    offenders.Add($"{seam.Name}.{method.Name}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "SEAM-22: a seam member takes or returns System.Type (" + string.Join(", ", offenders) + "). A Type-taking "
            + "decode overload lets an open generic reach a codec; if one is ever added it must reject "
            + "Type.ContainsGenericParameters (design §3.4) and this test must be updated with the reason.");
    }

    private static bool MentionsType(Type type) =>
        type == typeof(Type)
        || (type.HasElementType && MentionsType(type.GetElementType()!))
        || (type.IsGenericType && type.GetGenericArguments().Any(MentionsType));
}
