// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>P9-18, XCUT-18, XCUT-19: the <c>Auth:*</c> keys feed the redacting credentials, and no failure carries a value.</summary>
[Trait("Category", "Unit")]
public sealed class AuthKeyTests
{
    [Fact]
    public void The_credential_keys_bind()
    {
        var configuration = Config.Read(
            ("Auth:ApiKey:Key", "k-123"),
            ("Auth:ApiKey:HeaderName", "X-Api-Key"),
            ("Auth:ApiKey:Scheme", "Token"),
            ("Auth:Basic:Username", "alice"),
            ("Auth:Basic:Password", "pw"),
            ("Auth:Digest:Username", "bob"),
            ("Auth:Digest:Password", "pw2"),
            ("Auth:TokenRefreshMargin", "1m"));

        Assert.Empty(configuration.Failures);
        var credentials = Assert.IsType<AuthCredentials>(configuration.Credentials);
        Assert.Equal("k-123", credentials.ApiKey!.Key);
        Assert.Equal("X-Api-Key", credentials.ApiKey.HeaderName.Original);
        Assert.Equal("Token", credentials.ApiKey.Scheme);
        Assert.Equal("alice", credentials.Basic!.Username);
        Assert.Equal("bob", credentials.Digest!.Username);
        Assert.Equal(TimeSpan.FromMinutes(1), credentials.TokenRefreshMargin);
    }

    [Fact]
    public void No_auth_key_means_no_credentials()
    {
        Assert.Null(Config.Read().Credentials);
    }

    [Fact]
    public void Each_credential_requires_its_mandatory_members()
    {
        Assert.EndsWith("Auth:Basic:Password: required when any key of this credential is present", Assert.Single(Config.Failures(Config.Read(("Auth:Basic:Username", "alice")))), StringComparison.Ordinal);
        Assert.EndsWith("Auth:Digest:Username: required when any key of this credential is present", Assert.Single(Config.Failures(Config.Read(("Auth:Digest:Password", "pw")))), StringComparison.Ordinal);
        Assert.EndsWith("Auth:ApiKey:Key: required when any key of this credential is present", Assert.Single(Config.Failures(Config.Read(("Auth:ApiKey:HeaderName", "X-Key")))), StringComparison.Ordinal);
    }

    [Fact]
    public void The_token_is_code_only()
    {
        Assert.Contains("unknown key", Assert.Single(Config.Failures(Config.Read(("Auth:Token", "x")))), StringComparison.Ordinal);
    }

    [Fact]
    public void A_bad_credential_fails_with_fixed_text_and_the_secrets_are_redacted_everywhere()
    {
        var configuration = Config.Read(
            ("Auth:ApiKey:Key", "s3cr3t-key"),
            ("Auth:ApiKey:HeaderName", "bad header"),
            ("Auth:Basic:Username", "a:b"),
            ("Auth:Basic:Password", "s3cr3t-pass"),
            ("Auth:Digest:Username", "bob"),
            ("Auth:Digest:Password", "s3cr3t-digest"));

        var failures = string.Join('|', Config.Failures(configuration));
        Assert.Contains("Auth:ApiKey:HeaderName", failures, StringComparison.Ordinal);
        Assert.Contains("Auth:Basic:Username", failures, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", failures, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", configuration.Credentials!.ToString(), StringComparison.Ordinal);
    }
}
