// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

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

    [Fact]
    public void Status_FromCode_ResolvesKnownAndUnknown()
    {
        Assert.Equal(Status.Ok, Status.FromCode(200));
        Assert.Equal("OK", Status.FromCode(200).Name);
        Assert.Null(Status.FromCode(799).Name);
    }

    [Fact]
    public void Status_RangeHelpers()
    {
        Assert.True(Status.FromCode(204).IsSuccess);
        Assert.True(Status.FromCode(301).IsRedirect);
        Assert.True(Status.FromCode(404).IsClientError);
        Assert.True(Status.FromCode(503).IsServerError);
    }

    [Fact]
    public void Status_EqualityIsByCode()
    {
        Assert.Equal(Status.NotFound, Status.FromCode(404));
    }

    [Fact]
    public void Protocol_ParseAndWireRoundTrip()
    {
        Assert.Equal(Protocol.Http2, ProtocolExtensions.Parse("HTTP/2.0"));
        Assert.Equal("http/1.1", Protocol.Http11.ToWireString());
    }
}
