// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

public class MediaTypeTests
{
    [Fact]
    public void Of_LowerCasesTypeAndSubtype()
    {
        var mt = MediaType.Of("Application", "JSON");
        Assert.Equal("application", mt.Type);
        Assert.Equal("json", mt.Subtype);
        Assert.Equal("application/json", mt.FullType);
    }

    [Fact]
    public void Parse_ReadsParametersAndCharset()
    {
        var mt = MediaType.Parse("text/plain; charset=UTF-8");
        Assert.Equal("text", mt.Type);
        Assert.Equal("plain", mt.Subtype);
        Assert.Equal("UTF-8", mt.Parameters["charset"]);
        Assert.Equal(Encoding.UTF8, mt.Charset);
    }

    [Fact]
    public void ToString_RoundTripsQuotedBoundaryValue()
    {
        var mt = MediaType.Of(
            "multipart",
            "form-data",
            new Dictionary<string, string> { ["boundary"] = "a;b" });
        var roundTripped = MediaType.Parse(mt.ToString());
        Assert.Equal(mt, roundTripped);
        Assert.Equal("a;b", roundTripped.Parameters["boundary"]);
    }

    [Fact]
    public void Includes_HonoursWildcards()
    {
        var wildcard = MediaType.Of("application", "*");
        Assert.True(wildcard.Includes(CommonMediaTypes.ApplicationJson));
        Assert.False(wildcard.Includes(CommonMediaTypes.TextPlain));
    }

    [Fact]
    public void Equality_IsCaseInsensitiveOnTypeAndSubtype()
    {
        Assert.Equal(MediaType.Parse("Application/Json"), CommonMediaTypes.ApplicationJson);
    }

    [Fact]
    public void Charset_ReturnsNullForUnknownEncoding()
    {
        var mt = MediaType.Parse("text/plain; charset=not-a-real-charset");
        Assert.Null(mt.Charset);
    }
}
