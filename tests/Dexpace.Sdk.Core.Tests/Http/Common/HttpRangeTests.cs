// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Vectors: tests/vectors/http/http-range.json, ported from nodejs-sdk packages/core/src/http/http-range.test.ts (Ruby
// has no HttpRange test).

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

/// <summary>HTTP-49 (and HTTP-1 for HttpRange).</summary>
[Trait("Category", "Unit")]
public class HttpRangeTests
{
    /// <summary>One row of http-range.json.</summary>
    public sealed record RangeVector(string Op, string? Input, string? Kind, long? Offset, long? Length, string? Text);

    public static TheoryData<int> ParseVectors() => Indexes("parse");

    public static TheoryData<int> InvalidVectors() => Indexes("invalid");

    public static TheoryData<int> CanonicalVectors() => Indexes("canonical");

    [Theory]
    [MemberData(nameof(ParseVectors))]
    public void Parse_matches_the_vectors(int index)
    {
        var vector = Row("parse", index);

        var range = HttpRange.Parse(vector.Input!);

        Assert.Equal(vector.Offset, range.Offset);
        Assert.Equal(vector.Length, range.Length);
        Assert.Equal(vector.Input, range.ToString());
        Assert.True(HttpRange.TryParse(vector.Input, out var tried));
        Assert.Equal(range, tried);
    }

    [Theory]
    [MemberData(nameof(InvalidVectors))]
    public void Parse_accepts_only_the_bytes_unit_case_insensitively_and_one_range(int index)
    {
        var vector = Row("invalid", index);

        Assert.Throws<ArgumentException>(() => HttpRange.Parse(vector.Input!));
        Assert.False(HttpRange.TryParse(vector.Input, out var range));
        Assert.Null(range);
    }

    [Theory]
    [MemberData(nameof(CanonicalVectors))]
    public void Suffix_and_From_render_canonically(int index)
    {
        var vector = Row("canonical", index);

        var range = vector.Kind switch
        {
            "bounded" => HttpRange.Bounded(vector.Offset!.Value, vector.Length!.Value),
            "suffix" => HttpRange.Suffix(vector.Length!.Value),
            _ => HttpRange.From(vector.Offset!.Value),
        };

        Assert.Equal(vector.Text, range.ToString());
        Assert.Equal(vector.Offset, range.Offset);
        Assert.Equal(vector.Length, range.Length);
        Assert.Equal(range, HttpRange.Parse(range.ToString()));
    }

    [Fact]
    public void Bounded_rejects_a_negative_offset_a_non_positive_length_and_overflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpRange.Bounded(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpRange.Bounded(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpRange.Bounded(0, -5));
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpRange.Bounded(long.MaxValue, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpRange.Bounded(2, long.MaxValue));

        // offset + length - 1 == long.MaxValue is representable: the last byte's index fits.
        Assert.Equal(long.MaxValue, HttpRange.Bounded(long.MaxValue, 1).Offset);
        Assert.Equal(long.MaxValue, HttpRange.Bounded(0, long.MaxValue).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpRange.Suffix(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpRange.Suffix(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpRange.From(-1));
    }

    [Fact]
    public void Equality_is_semantic_over_Offset_and_Length()
    {
        var parsed = HttpRange.Parse("Bytes=0-9");
        var built = HttpRange.Bounded(0, 10);

        Assert.Equal(built, parsed);
        Assert.True(built == parsed);
        Assert.Equal(built.GetHashCode(), parsed.GetHashCode());
        Assert.NotEqual(parsed.ToString(), built.ToString());
        Assert.Equal("Bytes=0-9", parsed.ToString());
        Assert.Equal("bytes=0-9", built.ToString());
        Assert.NotEqual(HttpRange.Bounded(0, 10), HttpRange.Bounded(0, 11));
        Assert.NotEqual(HttpRange.Suffix(10), HttpRange.Bounded(0, 10));
        Assert.NotEqual(HttpRange.From(0), HttpRange.Bounded(0, 10));
        Assert.NotEqual(HttpRange.Suffix(10), HttpRange.From(10));
        Assert.False(built.Equals((HttpRange?)null));
    }

    [Fact]
    public void TryParse_never_throws()
    {
        var corpus = new[]
        {
            null, "", " ", "bytes", "bytes=", "bytes=-", "bytes=--", "bytes=1-2-3", "bytes=1-2,", ",", "=", "\0",
            "bytes=\uD800", "bytes=" + new string('9', 10_000), new string('-', 10_000), "bytes=-9223372036854775808",
            "bytes=9223372036854775808-", "bytes=0-9223372036854775807",
        };

        foreach (var input in corpus)
        {
            _ = HttpRange.TryParse(input, out _);
        }

        Assert.False(HttpRange.TryParse("bytes=0-9223372036854775807", out _));
    }

    [Fact]
    public void Parse_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => HttpRange.Parse(null!));
    }

    [Fact]
    public void Round_trips_for_a_range_of_generated_bounded_values()
    {
        foreach (var offset in new long[] { 0, 1, 99, 1_000_000 })
        {
            foreach (var length in new long[] { 1, 2, 500, 1_000_000 })
            {
                var range = HttpRange.Bounded(offset, length);
                Assert.Equal(range, HttpRange.Parse(range.ToString()));
            }
        }
    }

    [Fact]
    public void HttpRange_is_a_sealed_reference_type_with_no_public_constructor()
    {
        Assert.True(typeof(HttpRange).IsClass);
        Assert.True(typeof(HttpRange).IsSealed);
        Assert.Empty(typeof(HttpRange).GetConstructors());
    }

    private static RangeVector Row(string op, int index) =>
        VectorFile.Load<RangeVector>("http/http-range.json").Where(v => v.Op == op).ElementAt(index);

    private static TheoryData<int> Indexes(string op)
    {
        var data = new TheoryData<int>();
        var count = VectorFile.Load<RangeVector>("http/http-range.json").Count(v => v.Op == op);
        for (var i = 0; i < count; i++)
        {
            data.Add(i);
        }

        return data;
    }
}
