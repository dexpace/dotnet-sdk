// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

[Trait("Category", "Unit")]
public class MethodAndStatusTests
{
    [Theory]
    [InlineData("get", "GET")]
    [InlineData("  Post ", "POST")]
    public void Method_Of_NormalisesKnownVerbs(string input, string expected)
    {
        Assert.Equal(expected, Method.Of(input).Name);
    }

    [Fact]
    public void Method_Of_PreservesUnknownVerb()
    {
        Assert.Equal("PROPFIND", Method.Of("PROPFIND").Name);
    }

    [Fact]
    public void Method_SafetyAndIdempotency()
    {
        Assert.True(Method.Get.IsSafe);
        Assert.True(Method.Get.IsIdempotent);
        Assert.False(Method.Post.IsSafe);
        Assert.False(Method.Post.IsIdempotent);
        Assert.True(Method.Put.IsIdempotent);
    }
}
