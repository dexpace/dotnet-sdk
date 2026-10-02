// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// The adapter half of the no-defaults reflection test (design fact 2; plan task 3.2): Core.Tests cannot see the
/// adapter, so this half lives here.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SystemNetHttpClientSurfaceTests
{
    [Fact]
    public void The_implementation_declares_no_defaults_on_the_SPI_members()
    {
        var spiMembers = typeof(SystemNetHttpClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name is "Execute" or "ExecuteAsync")
            .ToArray();

        var threeParameter = spiMembers.Where(m => m.GetParameters().Length == 3).ToArray();
        Assert.Equal(2, threeParameter.Length);
        foreach (var member in threeParameter)
        {
            Assert.Equal(typeof(RequestOptions), member.GetParameters()[1].ParameterType);
            Assert.All(member.GetParameters(), p => Assert.False(p.HasDefaultValue, $"{member.Name}({p.Name}) declares a default"));
        }
    }
}
