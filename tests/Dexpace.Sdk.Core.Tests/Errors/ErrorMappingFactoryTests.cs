// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>
/// XCUT-8: the one internal factory every core mapping path goes through; the public constructor is unchanged.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ErrorMappingFactoryTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(199)]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(308)]
    [InlineData(399)]
    [InlineData(600)]
    [InlineData(999)]
    public void A_non_error_status_is_rejected(int code)
    {
        using var response = TestResponses.Create(Status.FromCode(code));

        Assert.Equal("response", Assert.Throws<ArgumentException>(() => ErrorMapping.ToException(response)).ParamName);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(599)]
    public void A_buffered_error_response_maps_to_HttpResponseException(int code)
    {
        using var response = TestResponses.Create(Status.FromCode(code));

        var ex = ErrorMapping.ToException(response);

        Assert.Same(response, ex.Response);
        Assert.Equal(code, ex.Status.Code);
    }

    [Fact]
    public void The_public_HttpResponseException_constructor_is_unchanged()
    {
        var constructor = Assert.Single(typeof(HttpResponseException).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        Assert.Equal([typeof(Response), typeof(string)], constructor.GetParameters().Select(p => p.ParameterType));
    }
}
