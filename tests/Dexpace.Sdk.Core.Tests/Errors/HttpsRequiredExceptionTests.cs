// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>AUTH-28, P6c-8, P6c-41: the typed HTTPS refusal.</summary>
[Trait("Category", "Unit")]
public sealed class HttpsRequiredExceptionTests
{
    [Fact]
    public void Carries_policy_name_and_scheme()
    {
        var ex = new HttpsRequiredException("BasicAuthPolicy", "http");

        Assert.Equal("BasicAuthPolicy", ex.PolicyName);
        Assert.Equal("http", ex.Scheme);
    }

    [Fact]
    public void Is_an_SdkException_but_not_a_ServiceRequestException()
    {
        var ex = new HttpsRequiredException("P", "http");

        Assert.IsAssignableFrom<SdkException>(ex);
        Assert.IsNotAssignableFrom<ServiceRequestException>(ex);
        Assert.False(ex.IsRetryable);
    }

    [Fact]
    public void The_message_is_the_as_built_text()
    {
        Assert.Equal(
            "BasicAuthPolicy refused to attach a credential to a request over the 'http' scheme; credentials are only sent over https.",
            new HttpsRequiredException("BasicAuthPolicy", "http").Message);
    }
}

/// <summary>P6c-41: the auth failures are never retried.</summary>
[Trait("Category", "Unit")]
public sealed class AuthExceptionsAreNotRetryableTests
{
    public static TheoryData<SdkException> Exceptions =>
    [
        new HttpsRequiredException("P", "http"),
        new AuthResolutionException([AuthScheme.Digest], [AuthScheme.Basic]),
        new TokenProviderException("The token provider returned a default token."),
    ];

    [Theory]
    [MemberData(nameof(Exceptions))]
    public void None_is_retryable_or_a_ServiceRequestException(SdkException exception)
    {
        Assert.False(exception.IsRetryable);
        Assert.IsNotAssignableFrom<ServiceRequestException>(exception);
    }
}
