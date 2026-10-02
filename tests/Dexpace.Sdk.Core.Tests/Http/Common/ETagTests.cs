// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Vectors: tests/vectors/http/etag.json, ported from nodejs-sdk packages/core/src/http/etag.test.ts (Ruby has no ETag
// test).

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

/// <summary>HTTP-48 (and HTTP-1 for ETag).</summary>
[Trait("Category", "Unit")]
public class ETagTests
{
    /// <summary>One row of etag.json.</summary>
    public sealed record ETagVector(string Op, string? Input, string? Kind, string? Opaque, string? Text);

    public static TheoryData<int> ParseVectors() => Indexes("parse");

    public static TheoryData<int> InvalidVectors() => Indexes("invalid");

    public static TheoryData<int> BlankVectors() => Indexes("blank");

    [Theory]
    [MemberData(nameof(ParseVectors))]
    public void Parse_matches_the_vectors(int index)
    {
        var vector = Row("parse", index);

        var etag = ETag.Parse(vector.Input);

        Assert.NotNull(etag);
        Assert.Equal(vector.Kind == "weak", etag.IsWeak);
        Assert.Equal(vector.Kind == "any", etag.IsAny);
        Assert.Equal(vector.Opaque, etag.Opaque);
        Assert.Equal(vector.Text, etag.ToString());
        Assert.True(ETag.TryParse(vector.Input, out var tried));
        Assert.Equal(etag, tried);
        Assert.Equal(etag, ETag.Parse(etag.ToString()));
    }

    [Theory]
    [MemberData(nameof(InvalidVectors))]
    public void Parse_of_a_malformed_form_throws(int index)
    {
        var vector = Row("invalid", index);

        Assert.Throws<ArgumentException>(() => ETag.Parse(vector.Input));
        Assert.False(ETag.TryParse(vector.Input, out var etag));
        Assert.Null(etag);
    }

    [Theory]
    [MemberData(nameof(BlankVectors))]
    public void Parse_of_null_or_blank_is_null(int index)
    {
        var vector = Row("blank", index);

        Assert.Null(ETag.Parse(vector.Input));
        Assert.False(ETag.TryParse(vector.Input, out var etag));
        Assert.Null(etag);
    }

    [Fact]
    public void TryParse_never_throws()
    {
        var corpus = new[]
        {
            null, "", " ", "*", "**", "\"", "W/", "W/\"", "\"\"\"", "\0", "\"\0\"", "W/\"x", "\uD800", "\"\uD800\"",
            new string('"', 1000), "W/" + new string('a', 10_000), "\"" + new string('a', 10_000) + "\"",
        };

        foreach (var input in corpus)
        {
            _ = ETag.TryParse(input, out _);
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("é")]
    [InlineData("!#~")]
    [InlineData("a,b;c")]
    public void Strong_accepts_etagc_characters(string opaque)
    {
        var etag = ETag.Strong(opaque);

        Assert.False(etag.IsWeak);
        Assert.False(etag.IsAny);
        Assert.Equal(opaque, etag.Opaque);
    }

    [Fact]
    public void Strong_rejects_empty_and_non_etagc_characters()
    {
        Assert.Throws<ArgumentException>(() => ETag.Strong(string.Empty));
        Assert.Throws<ArgumentException>(() => ETag.Strong("a b"));
        Assert.Throws<ArgumentException>(() => ETag.Strong("a\"b"));
        Assert.Throws<ArgumentException>(() => ETag.Strong("a\r\nb"));
        Assert.Throws<ArgumentException>(() => ETag.Strong("a\u007Fb"));
        Assert.Throws<ArgumentException>(() => ETag.Strong("€"));
        Assert.Throws<ArgumentNullException>(() => ETag.Strong(null!));
    }

    [Fact]
    public void Weak_may_be_empty()
    {
        var etag = ETag.Weak(string.Empty);

        Assert.True(etag.IsWeak);
        Assert.Equal(string.Empty, etag.Opaque);
        Assert.Equal("W/\"\"", etag.ToString());
        Assert.Throws<ArgumentException>(() => ETag.Weak("a b"));
        Assert.Throws<ArgumentNullException>(() => ETag.Weak(null!));
    }

    [Fact]
    public void Characters_are_etagc()
    {
        // etagc = 0x21, 0x23-0x7E and obs-text (0x80-0xFF); the quote (0x22) and the space (0x20) are not.
        for (var code = 0; code <= 0x140; code++)
        {
            var text = ((char)code).ToString();
            var expected = code is 0x21 or (>= 0x23 and <= 0x7E) or (>= 0x80 and <= 0xFF);

            Assert.Equal(expected, ETag.TryParse($"W/\"{text}\"", out _));
            Assert.Equal(expected, Passes(() => ETag.Weak(text)));
        }
    }

    [Fact]
    public void Any_is_star_and_has_empty_opaque()
    {
        Assert.True(ETag.Any.IsAny);
        Assert.False(ETag.Any.IsWeak);
        Assert.Equal(string.Empty, ETag.Any.Opaque);
        Assert.Equal("*", ETag.Any.ToString());
        Assert.Same(ETag.Any, ETag.Any);
    }

    [Fact]
    public void Equality_is_by_kind_and_opaque()
    {
        Assert.Equal(ETag.Strong("a"), ETag.Strong("a"));
        Assert.Equal(ETag.Strong("a").GetHashCode(), ETag.Strong("a").GetHashCode());
        Assert.NotEqual(ETag.Strong("a"), ETag.Weak("a"));
        Assert.NotEqual(ETag.Strong("a"), ETag.Strong("b"));
        Assert.NotEqual(ETag.Any, ETag.Strong("a"));
        Assert.Equal(ETag.Any, ETag.Parse("*"));
        Assert.True(ETag.Strong("a") == ETag.Parse("\"a\""));
        Assert.True(ETag.Strong("a") != ETag.Weak("a"));
    }

    [Fact]
    public void ToString_renders_star_quoted_and_weak()
    {
        Assert.Equal("*", ETag.Any.ToString());
        Assert.Equal("\"x\"", ETag.Strong("x").ToString());
        Assert.Equal("W/\"x\"", ETag.Weak("x").ToString());
    }

    [Fact]
    public void ETag_is_a_sealed_reference_type_with_no_public_constructor()
    {
        Assert.True(typeof(ETag).IsClass);
        Assert.True(typeof(ETag).IsSealed);
        Assert.Empty(typeof(ETag).GetConstructors());
    }

    private static bool Passes(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ETagVector Row(string op, int index) =>
        VectorFile.Load<ETagVector>("http/etag.json").Where(v => v.Op == op).ElementAt(index);

    private static TheoryData<int> Indexes(string op)
    {
        var data = new TheoryData<int>();
        var count = VectorFile.Load<ETagVector>("http/etag.json").Count(v => v.Op == op);
        for (var i = 0; i < count; i++)
        {
            data.Add(i);
        }

        return data;
    }
}
