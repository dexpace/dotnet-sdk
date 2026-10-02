// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Tests.Security;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

/// <summary>HTTP-21 and HTTP-1 (HttpHeaderName).</summary>
[Trait("Category", "Unit")]
public class HttpHeaderNameTests
{
    [Fact]
    public void Equality_and_hash_ignore_casing()
    {
        var names = new[] { HttpHeaderName.Of("X-Trace"), HttpHeaderName.Of("x-trace"), HttpHeaderName.Of("X-TRACE") };

        Assert.All(names, n => Assert.Equal(names[0], n));
        Assert.All(names, n => Assert.Equal(names[0].GetHashCode(), n.GetHashCode()));
        Assert.True(names[0] == names[1]);
        Assert.False(names[0] != names[2]);
        Assert.NotEqual(HttpHeaderName.Of("X-Trace"), HttpHeaderName.Of("X-Other"));
        Assert.False(names[0].Equals((HttpHeaderName?)null));
    }

    [Fact]
    public void Original_keeps_the_trimmed_caller_spelling()
    {
        Assert.Equal("X-Trace-Id", HttpHeaderName.Of("X-Trace-Id").Original);
        Assert.Equal("X-Trace-Id", HttpHeaderName.Of(" \tX-Trace-Id\t ").Original);
        Assert.Equal("x-lower", HttpHeaderName.Of("x-lower").Original);
    }

    [Fact]
    public void CanonicalName_is_ASCII_folded()
    {
        Assert.Equal("x-trace-id", HttpHeaderName.Of("X-Trace-Id").CanonicalName);
        Assert.Equal("x-trace-id", HttpHeaderName.Of("X-TRACE-ID").CanonicalName);
    }

    [Fact]
    public void CanonicalName_is_ASCII_folded_under_tr_TR()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal("title", HttpHeaderName.Of("TITLE").CanonicalName);
            Assert.Equal(HttpHeaderName.Of("title"), HttpHeaderName.Of("TITLE"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [MemberData(nameof(HeaderInjectionValidationTests.InvalidNames), MemberType = typeof(HeaderInjectionValidationTests))]
    public void Of_validates_like_the_headers_entry_points(string name)
    {
        // Parity with the Security data: every name Headers rejects, HttpHeaderName.Of rejects too.
        Assert.Throws<ArgumentException>(() => HttpHeaderName.Of(name));
        Assert.Throws<ArgumentException>(() => Headers.Empty.With(name, "v"));
    }

    [Fact]
    public void Of_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => HttpHeaderName.Of(null!));
    }

    [Fact]
    public void ToString_returns_Original()
    {
        // Breaking: it used to return CanonicalName.
        Assert.Equal("X-Trace-Id", HttpHeaderName.Of("X-Trace-Id").ToString());
        Assert.Equal("Content-Type", HttpHeaderName.WellKnown.ContentType.ToString());
    }

    [Fact]
    public void WellKnown_includes_IdempotencyKey_IfMatch_IfNoneMatch_IfModifiedSince_IfUnmodifiedSince_Range()
    {
        Assert.Equal(("idempotency-key", "Idempotency-Key"), Pair(HttpHeaderName.WellKnown.IdempotencyKey));
        Assert.Equal(("if-match", "If-Match"), Pair(HttpHeaderName.WellKnown.IfMatch));
        Assert.Equal(("if-none-match", "If-None-Match"), Pair(HttpHeaderName.WellKnown.IfNoneMatch));
        Assert.Equal(("if-modified-since", "If-Modified-Since"), Pair(HttpHeaderName.WellKnown.IfModifiedSince));
        Assert.Equal(("if-unmodified-since", "If-Unmodified-Since"), Pair(HttpHeaderName.WellKnown.IfUnmodifiedSince));
        Assert.Equal(("range", "Range"), Pair(HttpHeaderName.WellKnown.Range));
    }

    [Fact]
    public void HttpHeaderName_is_a_reference_type()
    {
        Assert.True(typeof(HttpHeaderName).IsClass);
        Assert.True(typeof(HttpHeaderName).IsSealed);
        Assert.Null(default(HttpHeaderName));
    }

    private static (string Canonical, string Original) Pair(HttpHeaderName name) => (name.CanonicalName, name.Original);
}
