// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Vectors: tests/vectors/http/media-type.json (HTTP-23, HTTP-25, HTTP-53), ported from nodejs-sdk
// packages/core/src/http/media-type.test.ts. HTTP-26 (a media type that is not header-safe is rejected) is held by the
// Security class HeaderInjectionValidationTests (its media-type rows), which is cited here and not edited.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

[Trait("Category", "Unit")]
public class MediaTypeTests
{
    /// <summary>One row of media-type.json.</summary>
    public sealed record MediaTypeVector(
        string Op, string? Input, string? Type, string? Subtype, Dictionary<string, string>? Parameters);

    public static TheoryData<int> ParseVectors() => Indexes("parse");

    public static TheoryData<int> RejectVectors() => Indexes("reject");

    public static TheoryData<int> RoundTripVectors() => Indexes("roundtrip");

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

    [Theory]
    [MemberData(nameof(ParseVectors))]
    public void Parse_case_table_matches_the_vectors(int index)
    {
        var vector = Row("parse", index);

        var mediaType = MediaType.Parse(vector.Input!);

        Assert.Equal(vector.Type, mediaType.Type);
        Assert.Equal(vector.Subtype, mediaType.Subtype);
        Assert.Equal(vector.Parameters!.OrderBy(p => p.Key, StringComparer.Ordinal), mediaType.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(RoundTripVectors))]
    public void Round_trip_table_matches_the_vectors(int index)
    {
        var vector = Row("roundtrip", index);
        var original = MediaType.Of(vector.Type!, vector.Subtype!, vector.Parameters);

        var restored = MediaType.Parse(original.ToString());

        Assert.Equal(original, restored);
        Assert.Equal(original.ToString(), restored.ToString());
        foreach (var (key, value) in vector.Parameters!)
        {
            Assert.Equal(value, restored.Parameters[key]);
        }
    }

    [Fact]
    public void Of_rejects_a_wildcard_subtype_under_a_concrete_type()
    {
        // HTTP-27: Of("*", "json") is a half-wildcard.
        Assert.Throws<ArgumentException>(() => MediaType.Of("*", "json"));
        Assert.Throws<ArgumentException>(() => MediaType.Parse("*/json"));
    }

    [Fact]
    public void Includes_honours_receiver_wildcards_and_ignores_parameters()
    {
        var textAny = MediaType.Parse("text/*");
        var plain = MediaType.Parse("text/plain; charset=utf-8");

        Assert.True(textAny.Includes(plain));
        Assert.False(plain.Includes(textAny));
        Assert.True(MediaType.Parse("*/*").Includes(plain));
        Assert.True(MediaType.Parse("*/*").Includes(textAny));
        Assert.False(textAny.Includes(MediaType.Parse("application/json")));
        Assert.True(MediaType.Parse("text/plain").Includes(plain));
    }

    [Theory]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("text/plain; charset=UTF-8")]
    [InlineData("text/plain; Charset=utf-8")]
    [InlineData("text/plain; CHARSET=Utf-8")]
    public void Charset_resolves_utf8_in_any_case_and_key_case(string header)
    {
        Assert.Equal(Encoding.UTF8.WebName, MediaType.Parse(header).Charset?.WebName);
    }

    [Theory]
    [InlineData("text/plain; charset=utf-7")]
    [InlineData("text/plain; charset=UTF-7")]
    [InlineData("text/plain; charset=bogus")]
    [InlineData("text/plain; charset=\"not an encoding\"")]
    [InlineData("text/plain")]
    public void Charset_is_null_for_bogus_utf_7_and_absent(string header)
    {
        // utf-7 is the red one: Encoding.GetEncoding("utf-7") throws NotSupportedException, which used to escape.
        Assert.Null(MediaType.Parse(header).Charset);
    }

    [Theory]
    [MemberData(nameof(RejectVectors))]
    public void Parse_rejection_table_matches_the_vectors(int index)
    {
        var vector = Row("reject", index);

        Assert.Throws<ArgumentException>(() => MediaType.Parse(vector.Input!));
        Assert.False(MediaType.TryParse(vector.Input, out var mediaType));
        Assert.Null(mediaType);
    }

    [Theory]
    [InlineData("text/plain;a=")]
    [InlineData("text/plain;a=;b=c")]
    [InlineData("text/plain; a = ")]
    public void Parse_rejects_a_parameter_with_an_empty_raw_value(string header)
    {
        Assert.Throws<ArgumentException>(() => MediaType.Parse(header));
    }

    [Fact]
    public void Parse_accepts_a_quoted_empty_value()
    {
        var mediaType = MediaType.Parse("text/plain;a=\"\"");

        Assert.Equal(string.Empty, mediaType.Parameters["a"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("text")]
    [InlineData("/json")]
    [InlineData("text/")]
    public void Parse_still_rejects_blank_text_slash_json_and_text_slash(string header)
    {
        Assert.Throws<ArgumentException>(() => MediaType.Parse(header));
    }

    [Theory]
    [InlineData("text/plain;")]
    [InlineData("text/plain; ;")]
    [InlineData("text/plain;;a=b")]
    public void An_empty_segment_after_a_semicolon_is_still_skipped(string header)
    {
        // Pin: RFC 9110's *( OWS ";" OWS [ parameter ] ) allows an empty segment; it is not a parameter.
        var mediaType = MediaType.Parse(header);

        Assert.Equal("text/plain", mediaType.FullType);
    }

    private static MediaTypeVector Row(string op, int index) =>
        VectorFile.Load<MediaTypeVector>("http/media-type.json").Where(v => v.Op == op).ElementAt(index);

    private static TheoryData<int> Indexes(string op)
    {
        var data = new TheoryData<int>();
        var count = VectorFile.Load<MediaTypeVector>("http/media-type.json").Count(v => v.Op == op);
        for (var i = 0; i < count; i++)
        {
            data.Add(i);
        }

        return data;
    }
}
