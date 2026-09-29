// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

[Trait("Category", "Unit")]
public class UrlRedactorTests
{
    // Use the default-set instance for most tests.
    private static readonly UrlRedactor s_defaultRedactor = new();

    [Fact]
    public void Redact_UserInfo_IsMasked()
    {
        var uri = new Uri("https://user:secret@api.example.com/path");
        var result = s_defaultRedactor.Redact(uri);

        Assert.DoesNotContain("user", result);
        Assert.DoesNotContain("secret", result);
        Assert.Contains("***:***@api.example.com", result); // masked, not removed (OBS-11)
    }

    [Fact]
    public void Redact_SensitiveQueryParam_ValueIsReplaced()
    {
        var uri = new Uri("https://api.example.com/v1/items?access_token=super-secret&page=2");
        var result = s_defaultRedactor.Redact(uri);

        Assert.Contains("access_token=***", result);
        Assert.Contains("page=***", result); // default-deny: page is not allow-listed (OBS-12)
        Assert.DoesNotContain("super-secret", result);
    }

    [Fact]
    public void Redact_NonAllowListedQueryParam_IsRedacted_AllowListedIsPreserved()
    {
        var uri = new Uri("https://api.example.com/search?q=hello&lang=en&api-version=2026-01-01");
        var result = s_defaultRedactor.Redact(uri);

        Assert.Contains("q=***", result);
        Assert.Contains("lang=***", result);
        Assert.Contains("api-version=2026-01-01", result);
    }

    [Fact]
    public void Redact_AllowListCheck_IsCaseInsensitive()
    {
        var uri = new Uri("https://api.example.com/v1?API_KEY=abc123&API-VERSION=1");
        var result = s_defaultRedactor.Redact(uri);

        Assert.Contains("API_KEY=***", result);
        Assert.DoesNotContain("abc123", result);
        Assert.Contains("API-VERSION=1", result);
    }

    [Fact]
    public void Redact_NoQueryString_ReturnsSafeUrl()
    {
        var uri = new Uri("https://api.example.com/v1/resource");
        var result = s_defaultRedactor.Redact(uri);

        Assert.Equal("https://api.example.com/v1/resource", result);
    }

    [Fact]
    public void Redact_CustomAllowList_KeepsOnlyListedValues()
    {
        var redactor = new UrlRedactor(["x-public"]);
        var uri = new Uri("https://api.example.com/?x-custom-secret=mysecret&x-public=value&api-version=1");
        var result = redactor.Redact(uri);

        Assert.Contains("x-custom-secret=***", result);
        Assert.Contains("x-public=value", result);
        Assert.Contains("api-version=***", result); // a custom list replaces the default
    }

    // ── Edge-case tests added by code-review hardening ────────────────────────

    [Fact]
    public void Redact_RelativeUri_SensitiveQueryParamIsRedacted_NoException()
    {
        // A relative URI carrying a sensitive query param must not throw and must not leak.
        var uri = new Uri("/v1/items?token=super-secret&page=2", UriKind.Relative);
        var result = s_defaultRedactor.Redact(uri);

        Assert.Contains("token=***", result);
        Assert.DoesNotContain("super-secret", result);
        Assert.Contains("page=***", result);
        Assert.Contains("/v1/items", result);
    }

    [Fact]
    public void Redact_PlainFragment_IsPreserved()
    {
        // A fragment with no key=value token is kept verbatim (OBS-13).
        var uri = new Uri("https://api.example.com/path?q=hello#section");
        var result = s_defaultRedactor.Redact(uri);

        Assert.EndsWith("#section", result);
        Assert.Contains("q=***", result);
    }

    [Fact]
    public void Redact_RelativeUri_PlainFragment_IsPreserved()
    {
        // Relative URIs follow the same fragment rule (OBS-13).
        var uri = new Uri("/path?q=hello#section", UriKind.Relative);
        var result = s_defaultRedactor.Redact(uri);

        Assert.EndsWith("#section", result);
        Assert.Contains("q=***", result);
    }

    [Fact]
    public void Redact_ValuelessParam_DoesNotCrash_AndSensitiveParamIsStillRedacted()
    {
        // "?flag" has no '=' so it has no value to leak and is kept verbatim (OBS-14); token must still be redacted.
        var uri = new Uri("https://api.example.com/v1?flag&token=x");
        var result = s_defaultRedactor.Redact(uri);

        // The bare flag is preserved as given.
        Assert.Contains("?flag&", result);
        Assert.DoesNotContain("flag=", result);
        // The sensitive token value must not appear.
        Assert.Contains("token=***", result);
        Assert.DoesNotContain("=x", result);
    }

    [Fact]
    public void Redact_RepeatedSensitiveParam_BothValuesAreRedacted()
    {
        // Both occurrences of a repeated sensitive param must be redacted.
        var uri = new Uri("https://api.example.com/v1?token=A&token=B");
        var result = s_defaultRedactor.Redact(uri);

        Assert.DoesNotContain("=A", result);
        Assert.DoesNotContain("=B", result);
        // Both occurrences of the key should appear, both redacted.
        Assert.Equal(2, result.Split("token=***").Length - 1);
    }

    [Fact]
    public void Redact_PercentEncodedSensitiveValue_IsRedacted()
    {
        // A percent-encoded sensitive value must still be caught and redacted.
        var uri = new Uri("https://api.example.com/v1?token=my%2Fsecret%3Dvalue");
        var result = s_defaultRedactor.Redact(uri);

        Assert.Contains("token=***", result);
        Assert.DoesNotContain("secret", result);
    }

    [Fact]
    public void Redact_PathSegmentThatLooksSecret_IsPreservedVerbatim()
    {
        // Path components are NOT inspected — secrets in the path are the caller's responsibility.
        // This test documents the boundary: path is preserved, no redaction is applied there.
        var uri = new Uri("https://api.example.com/token/super-secret-value?page=1");
        var result = s_defaultRedactor.Redact(uri);

        Assert.Contains("/token/super-secret-value", result);
        Assert.Contains("page=***", result);
    }
}
