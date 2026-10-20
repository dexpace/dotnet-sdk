// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>
/// XCUT-8 and NFR-7 (phase 11, task 1.6; design §10 entry 28, P11-9, V12, V17): the public constructor of <see cref="HttpResponseException"/> guards its own
/// argument. It was the one finding of <c>CA1062</c> over the libraries, a <see cref="NullReferenceException"/> from a null response, and it built "The server
/// returned an error response: OK(200)." for a successful one, a "successful exception" the status factory is required to refuse.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpResponseExceptionTests
{
    [Fact]
    public void The_constructor_rejects_a_null_response()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => new HttpResponseException(null!));

        Assert.Equal("response", thrown.ParamName);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(302)]
    [InlineData(304)]
    public void The_constructor_rejects_a_non_error_status(int code)
    {
        using var response = TestResponses.Create(Status.FromCode(code));

        var thrown = Assert.Throws<ArgumentException>(() => new HttpResponseException(response));

        Assert.Equal("response", thrown.ParamName);
        Assert.DoesNotContain("example.test", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(599)]
    public void The_constructor_accepts_an_error_status_and_carries_the_response(int code)
    {
        using var response = TestResponses.Create(Status.FromCode(code));

        var made = new HttpResponseException(response);

        Assert.Same(response, made.Response);
        Assert.Equal(code, made.Status.Code);
    }
}
