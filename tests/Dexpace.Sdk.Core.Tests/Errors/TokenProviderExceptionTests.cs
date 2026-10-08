// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>AUTH-35: the failure for a default or already-expired provider token.</summary>
[Trait("Category", "Unit")]
public sealed class TokenProviderExceptionTests
{
    [Fact]
    public void Is_an_SdkException_and_not_retryable()
    {
        var ex = new TokenProviderException("bad token");

        Assert.IsAssignableFrom<SdkException>(ex);
        Assert.IsNotAssignableFrom<ServiceRequestException>(ex);
        Assert.False(ex.IsRetryable);
        Assert.Equal("bad token", ex.Message);
    }

    [Fact]
    public async Task The_message_never_contains_a_token()
    {
        // The cache is the only producer; its messages are fixed text.
        var cache = new Dexpace.Sdk.Core.Auth.AccessTokenCache(
            new FixedExpired("s3cr3t-Zx9"),
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        var ex = await Assert.ThrowsAsync<TokenProviderException>(
            () => cache.GetAsync(new Dexpace.Sdk.Core.Auth.TokenRequestContext(["s"]), TestContext.Current.CancellationToken).AsTask());

        Assert.DoesNotContain("s3cr3t-Zx9", ex.Message, StringComparison.Ordinal);
    }

    private sealed class FixedExpired(string token) : Dexpace.Sdk.Core.Auth.TokenCredential
    {
        public override ValueTask<Dexpace.Sdk.Core.Auth.AccessToken> GetTokenAsync(
            Dexpace.Sdk.Core.Auth.TokenRequestContext context,
            CancellationToken ct = default) =>
            new(new Dexpace.Sdk.Core.Auth.AccessToken(token, new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }
}
