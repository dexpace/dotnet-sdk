// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

/// <summary>OBS-34, OBS-36, OBS-12, OBS-18: <see cref="HttpLoggingOptions"/>.</summary>
[Trait("Category", "Unit")]
public sealed class HttpLoggingOptionsTests
{
    private static readonly string[] s_credentialNames =
    [
        "authorization", "proxy-authorization", "cookie", "set-cookie", "x-api-key", "x-auth-token",
        "x-amz-security-token", "www-authenticate", "proxy-authenticate",
    ];

    private static readonly string[] s_defaultNames =
    [
        "accept", "accept-encoding", "cache-control", "connection", "content-encoding", "content-length",
        "content-location", "content-type", "date", "etag", "expires", "if-match", "if-modified-since", "if-none-match",
        "if-unmodified-since", "last-modified", "location", "retry-after", "server", "traceparent", "tracestate",
        "user-agent", "vary", "via", "x-correlation-id", "x-request-id",
    ];

    [Fact]
    public void Defaults_are_none_8_KiB_26_names_api_version_and_three_url_headers()
    {
        var options = new HttpLoggingOptions();

        Assert.Equal(HttpLogLevel.None, options.Level);
        Assert.Equal(8192, options.BodyPreviewSize);
        Assert.Equal(8192, HttpLoggingOptions.DefaultBodyPreviewSize);
        Assert.Equal(26, options.AllowedHeaderNames.Count);
        Assert.Equal(["api-version"], options.AllowedQueryParameters);
        Assert.Equivalent((string[])["location", "content-location", "referer"], options.UrlValuedHeaderNames);
        Assert.False(options.OmitDisallowedHeaders);
    }

    [Fact]
    public void The_default_header_allow_list_contains_no_credential_name()
    {
        Assert.Empty(HttpLoggingOptions.DefaultAllowedHeaderNames.Intersect(s_credentialNames, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_default_allow_list_is_the_26_names()
    {
        Assert.Equal(s_defaultNames, HttpLoggingOptions.DefaultAllowedHeaderNames);
    }

    [Fact]
    public void A_negative_preview_size_throws_and_an_oversized_one_clamps()
    {
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new HttpLoggingOptions { BodyPreviewSize = -1 });
        Assert.Equal("value", thrown.ParamName, ignoreCase: true);

        Assert.Equal(Array.MaxLength, new HttpLoggingOptions { BodyPreviewSize = int.MaxValue }.BodyPreviewSize);
        Assert.Equal(0, new HttpLoggingOptions { BodyPreviewSize = 0 }.BodyPreviewSize);
    }

    [Fact]
    public void The_init_accessors_copy_the_callers_collection()
    {
        var names = new List<string> { "x-one" };
        var queries = new List<string> { "keep" };
        var urls = new List<string> { "link" };
        var options = new HttpLoggingOptions
        {
            AllowedHeaderNames = names,
            AllowedQueryParameters = queries,
            UrlValuedHeaderNames = urls,
        };

        names.Add("x-two");
        queries.Clear();
        urls[0] = "other";

        Assert.Equal(["x-one"], options.AllowedHeaderNames);
        Assert.Equal(["keep"], options.AllowedQueryParameters);
        Assert.Equal(["link"], options.UrlValuedHeaderNames);
        Assert.False(options.AllowedHeaderNames is List<string>);
    }

    [Fact]
    public void A_null_collection_or_element_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpLoggingOptions { AllowedHeaderNames = null! });
        Assert.Throws<ArgumentException>(() => new HttpLoggingOptions { AllowedHeaderNames = ["a", null!] });
        Assert.Throws<ArgumentException>(() => new HttpLoggingOptions { AllowedQueryParameters = [null!] });
        Assert.Throws<ArgumentException>(() => new HttpLoggingOptions { UrlValuedHeaderNames = [null!] });
    }

    [Fact]
    public void With_changes_one_property_and_keeps_the_rest()
    {
        var original = new HttpLoggingOptions
        {
            BodyPreviewSize = 10,
            AllowedHeaderNames = ["a"],
            AllowedQueryParameters = ["b"],
            UrlValuedHeaderNames = ["c"],
            OmitDisallowedHeaders = true,
        };

        var copy = original with { Level = HttpLogLevel.Body };

        Assert.Equal(HttpLogLevel.Body, copy.Level);
        Assert.Equal(HttpLogLevel.None, original.Level);
        Assert.Equal(10, copy.BodyPreviewSize);
        Assert.Equal(["a"], copy.AllowedHeaderNames);
        Assert.Equal(["b"], copy.AllowedQueryParameters);
        Assert.Equal(["c"], copy.UrlValuedHeaderNames);
        Assert.True(copy.OmitDisallowedHeaders);
    }

    [Fact]
    public void Default_is_a_shared_instance_with_level_none()
    {
        Assert.Same(HttpLoggingOptions.Default, HttpLoggingOptions.Default);
        Assert.Equal(HttpLogLevel.None, HttpLoggingOptions.Default.Level);
    }

    [Fact]
    public void The_level_enum_is_none_headers_body_in_that_order()
    {
        Assert.Equal(0, (int)HttpLogLevel.None);
        Assert.Equal(1, (int)HttpLogLevel.Headers);
        Assert.Equal(2, (int)HttpLogLevel.Body);
    }
}
