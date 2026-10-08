// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

[Trait("Category", "Unit")]
public class AccessTokenTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var token = "tok_abc";
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var refresh = DateTimeOffset.UtcNow.AddMinutes(50);

        var at = new AccessToken(token, expires, refresh);

        Assert.Equal(token, at.Token);
        Assert.Equal(expires, at.ExpiresOn);
        Assert.Equal(refresh, at.RefreshOn);
    }

    [Fact]
    public void Ctor_NullableRefreshOn_IsNull_WhenOmitted()
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var at = new AccessToken("tok", expires);

        Assert.Null(at.RefreshOn);
    }

    [Fact]
    public void Ctor_ThrowsOnNullToken()
    {
        Assert.Throws<ArgumentNullException>(() => new AccessToken(null!, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AccessToken_rejects_null_empty_and_whitespace_tokens(string? token)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AccessToken(token!));
        Assert.ThrowsAny<ArgumentException>(() => new AccessToken(token!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AccessToken_without_expiry_never_expires()
    {
        var token = new AccessToken("t");

        Assert.Null(token.ExpiresOn);
        Assert.False(token.IsExpired(DateTimeOffset.MaxValue, TimeSpan.Zero));
    }

    [Fact]
    public void IsExpired_is_strictly_after()
    {
        var expiry = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var token = new AccessToken("t", expiry);

        Assert.False(token.IsExpired(expiry, TimeSpan.Zero));
        Assert.True(token.IsExpired(expiry.AddTicks(1), TimeSpan.Zero));
        Assert.True(token.IsExpired(expiry.AddSeconds(-29), TimeSpan.FromSeconds(30)));
        Assert.False(token.IsExpired(expiry.AddSeconds(-31), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void AccessToken_equality_is_by_value_over_token_expiry_and_refresh()
    {
        var expiry = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new AccessToken("t", expiry, expiry.AddMinutes(-5));
        var b = new AccessToken("t", expiry, expiry.AddMinutes(-5));
        var c = new AccessToken("t", expiry, expiry.AddMinutes(-4));

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.True(a != c);
        Assert.NotEqual(a, c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(default(AccessToken) == default);
    }

    [Fact]
    public void AccessToken_ToString_redacts_the_token()
    {
        var text = new AccessToken("s3cr3t-Zx9", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)).ToString();

        Assert.Contains("***", text, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t-Zx9", text, StringComparison.Ordinal);
        Assert.Contains("2030", text, StringComparison.Ordinal);
        Assert.Contains("***", default(AccessToken).ToString(), StringComparison.Ordinal);
    }
}

[Trait("Category", "Unit")]
public class TokenRequestContextTests
{
    [Fact]
    public void Ctor_SetsScopes()
    {
        IReadOnlyList<string> scopes = ["https://example.com/.default"];
        var ctx = new TokenRequestContext(scopes);

        Assert.Equal(scopes, ctx.Scopes);
        Assert.Null(ctx.Claims);
    }

    [Fact]
    public void Ctor_SetsScopesAndClaims()
    {
        IReadOnlyList<string> scopes = ["scope1", "scope2"];
        var ctx = new TokenRequestContext(scopes, "some-claims");

        Assert.Equal(scopes, ctx.Scopes);
        Assert.Equal("some-claims", ctx.Claims);
    }

    [Fact]
    public void CacheKey_IsSameForSameInputs()
    {
        IReadOnlyList<string> scopes = ["a", "b"];
        var ctx1 = new TokenRequestContext(scopes, "claims");
        var ctx2 = new TokenRequestContext(scopes, "claims");

        Assert.Equal(ctx1.CacheKey, ctx2.CacheKey);
    }

    [Fact]
    public void CacheKey_DiffersForDifferentScopes()
    {
        var ctx1 = new TokenRequestContext(["scopeA"]);
        var ctx2 = new TokenRequestContext(["scopeB"]);

        Assert.NotEqual(ctx1.CacheKey, ctx2.CacheKey);
    }

    [Fact]
    public void CacheKey_DiffersWhenClaimsChange()
    {
        IReadOnlyList<string> scopes = ["s"];
        var ctx1 = new TokenRequestContext(scopes);
        var ctx2 = new TokenRequestContext(scopes, "extra");

        Assert.NotEqual(ctx1.CacheKey, ctx2.CacheKey);
    }

    [Fact]
    public void CacheKey_StableForSameScopesInSameOrder()
    {
        var ctx1 = new TokenRequestContext(["x", "y"]);
        var ctx2 = new TokenRequestContext(["x", "y"]);

        Assert.Equal(ctx1.CacheKey, ctx2.CacheKey);
    }

    [Fact]
    public void Ctor_ThrowsOnNullScopes()
    {
        Assert.Throws<ArgumentNullException>(() => new TokenRequestContext(null!));
    }

    [Fact]
    public void TokenRequestContext_copies_its_scopes()
    {
        var scopes = new List<string> { "a" };
        var ctx = new TokenRequestContext(scopes);
        var key = ctx.CacheKey;

        scopes.Add("b");

        Assert.Equal(["a"], ctx.Scopes);
        Assert.Equal(key, ctx.CacheKey);
        Assert.Empty(default(TokenRequestContext).Scopes);
    }
}

[Trait("Category", "Unit")]
public class TokenCredentialTests
{
    private sealed class ConstantTokenCredential : TokenCredential
    {
        private readonly AccessToken _token;

        public ConstantTokenCredential(AccessToken token) => _token = token;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
            => ValueTask.FromResult(_token);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsExpectedToken()
    {
        var expected = new AccessToken("async_tok", DateTimeOffset.UtcNow.AddHours(1));
        var cred = new ConstantTokenCredential(expected);
        var ctx = new TokenRequestContext(["scope"]);

        var actual = await cred.GetTokenAsync(ctx, TestContext.Current.CancellationToken);

        Assert.Equal(expected.Token, actual.Token);
    }

    [Fact]
    public void GetToken_SyncBridge_ReturnsSameAsAsync()
    {
        var expected = new AccessToken("sync_tok", DateTimeOffset.UtcNow.AddHours(1));
        var cred = new ConstantTokenCredential(expected);
        var ctx = new TokenRequestContext(["scope"]);

        var actual = cred.GetToken(ctx, TestContext.Current.CancellationToken);

        Assert.Equal(expected.Token, actual.Token);
    }
}

[Trait("Category", "Unit")]
public class ApiKeyCredentialTests
{
    [Fact]
    public void Ctor_DefaultsToAuthorizationHeader_AndNoScheme()
    {
        var cred = new ApiKeyCredential("my-key");

        Assert.Equal("my-key", cred.Key);
        Assert.Equal(HttpHeaderName.WellKnown.Authorization, cred.HeaderName);
        Assert.Null(cred.Scheme);
    }

    [Fact]
    public void Ctor_CustomHeader_AndScheme()
    {
        var header = HttpHeaderName.Of("X-Api-Key");
        var cred = new ApiKeyCredential("k", header, "Bearer");

        Assert.Equal("k", cred.Key);
        Assert.Equal(header, cred.HeaderName);
        Assert.Equal("Bearer", cred.Scheme);
    }

    [Fact]
    public void Ctor_ThrowsOnNullKey()
    {
        Assert.Throws<ArgumentNullException>(() => new ApiKeyCredential(null!));
    }

    [Fact]
    public void Ctor_ThrowsOnEmptyKey()
    {
        Assert.Throws<ArgumentException>(() => new ApiKeyCredential(string.Empty));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ApiKeyCredential_rejects_a_whitespace_only_key(string key)
    {
        Assert.Throws<ArgumentException>(() => new ApiKeyCredential(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Shared Key")]
    public void ApiKeyCredential_scheme_must_be_null_or_non_blank_without_whitespace(string scheme)
    {
        Assert.Throws<ArgumentException>(() => new ApiKeyCredential("k", null, scheme));
        Assert.Equal("SharedAccessKey", new ApiKeyCredential("k", null, "SharedAccessKey").Scheme);
    }

    [Theory]
    [InlineData("a\r\nb-Zx9")]
    [InlineData("caf\u00e9-Zx9")]
    public void ApiKeyCredential_rejects_a_value_the_outbound_header_grammar_refuses(string key)
    {
        var ex = Assert.Throws<ArgumentException>(() => new ApiKeyCredential(key));

        Assert.DoesNotContain("Zx9", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiKeyCredential_exposes_the_stamped_value_computed_once()
    {
        Assert.Equal("SharedAccessKey k", new ApiKeyCredential("k", null, "SharedAccessKey").HeaderValue);
        Assert.Equal("k", new ApiKeyCredential("k").HeaderValue);
    }

    [Fact]
    public void ApiKeyCredential_ToString_redacts_the_key()
    {
        var text = new ApiKeyCredential("s3cr3t-Zx9", HttpHeaderName.Of("X-Api-Key")).ToString();

        Assert.Contains("***", text, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t-Zx9", text, StringComparison.Ordinal);
        Assert.Contains("X-Api-Key", text, StringComparison.Ordinal);
    }
}

[Trait("Category", "Unit")]
public class BasicCredentialTests
{
    [Fact]
    public void Ctor_SetsUsernameAndPassword()
    {
        var cred = new BasicCredential("user", "pass");

        Assert.Equal("user", cred.Username);
        Assert.Equal("pass", cred.Password);
    }

    [Fact]
    public void Ctor_ThrowsOnNullUsername()
    {
        Assert.Throws<ArgumentNullException>(() => new BasicCredential(null!, "p"));
    }

    [Fact]
    public void Ctor_ThrowsOnNullPassword()
    {
        Assert.Throws<ArgumentNullException>(() => new BasicCredential("u", null!));
    }

    [Fact]
    public void ToBase64_ProducesCorrectEncoding()
    {
        var cred = new BasicCredential("user", "pass");
        var expected = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user:pass"));

        Assert.Equal(expected, cred.ToBase64());
    }

    [Fact]
    public void ToBase64_HandlesSpecialChars()
    {
        var cred = new BasicCredential("user@example.com", "p@ss:word");
        var expected = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user@example.com:p@ss:word"));

        Assert.Equal(expected, cred.ToBase64());
    }

    [Fact]
    public void BasicCredential_rejects_empty_username_or_password_but_allows_whitespace()
    {
        Assert.Throws<ArgumentException>(() => new BasicCredential("u", ""));
        Assert.Throws<ArgumentException>(() => new BasicCredential("", "p"));
        Assert.Equal(" ", new BasicCredential("u", " ").Password);
        Assert.Equal(" ", new BasicCredential(" ", "p").Username);
    }

    [Fact]
    public void BasicCredential_rejects_a_colon_in_the_username()
    {
        Assert.Throws<ArgumentException>(() => new BasicCredential("a:b", "p"));
        Assert.Equal("p:q", new BasicCredential("a", "p:q").Password);
    }

    [Fact]
    public void BasicCredential_with_an_empty_password_is_rejected_at_construction()
    {
        Assert.Throws<ArgumentException>(() => new BasicCredential("user", string.Empty));
    }

    [Fact]
    public void BasicCredential_exposes_the_header_value_computed_once()
    {
        var credential = new BasicCredential("Aladdin", "open sesame");

        Assert.Equal("Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==", credential.HeaderValue);
    }

    [Fact]
    public void BasicCredential_ToString_redacts_the_password()
    {
        var text = new BasicCredential("alice", "s3cr3t-Zx9").ToString();

        Assert.Contains("***", text, StringComparison.Ordinal);
        Assert.Contains("alice", text, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t-Zx9", text, StringComparison.Ordinal);
    }
}

[Trait("Category", "Unit")]
public class DigestCredentialTests
{
    [Fact]
    public void DigestCredential_rejects_empty_username_or_password_and_redacts()
    {
        Assert.Throws<ArgumentNullException>(() => new DigestCredential(null!, "p"));
        Assert.Throws<ArgumentNullException>(() => new DigestCredential("u", null!));
        Assert.Throws<ArgumentException>(() => new DigestCredential("", "p"));
        Assert.Throws<ArgumentException>(() => new DigestCredential("u", ""));

        var text = new DigestCredential("alice", "s3cr3t-Zx9").ToString();
        Assert.Contains("***", text, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t-Zx9", text, StringComparison.Ordinal);
    }
}

[Trait("Category", "Unit")]
public class AuthCredentialsTests
{
    [Fact]
    public void Defaults_have_no_credentials_and_a_30_second_refresh_margin()
    {
        var credentials = new AuthCredentials();

        Assert.Null(credentials.Token);
        Assert.Null(credentials.ApiKey);
        Assert.Null(credentials.Basic);
        Assert.Null(credentials.Digest);
        Assert.Equal(TimeSpan.FromSeconds(30), credentials.TokenRefreshMargin);
        Assert.Empty(credentials.Available);
    }

    [Fact]
    public void Margin_must_be_non_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthCredentials { TokenRefreshMargin = TimeSpan.FromTicks(-1) });
        Assert.Equal(TimeSpan.Zero, new AuthCredentials { TokenRefreshMargin = TimeSpan.Zero }.TokenRefreshMargin);
    }

    [Fact]
    public void Available_lists_configured_schemes_in_enum_order()
    {
        var credentials = new AuthCredentials
        {
            Digest = new DigestCredential("u", "p"),
            Basic = new BasicCredential("u", "p"),
        };

        Assert.Equal([AuthScheme.Basic, AuthScheme.Digest], credentials.Available);
    }
}
