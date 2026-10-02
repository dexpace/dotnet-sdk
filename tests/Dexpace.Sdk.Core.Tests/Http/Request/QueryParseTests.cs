// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Vectors: tests/vectors/http/query.json, ported from nodejs-sdk packages/core/src/http/query-params.test.ts.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>HTTP-31: <see cref="Query.Parse"/> is total and lenient.</summary>
[Trait("Category", "Unit")]
public class QueryParseTests
{
    public static TheoryData<int> ParseVectorIndexes()
    {
        var data = new TheoryData<int>();
        var count = VectorFile.Load<QueryTests.QueryVector>("http/query.json").Count(v => v.Op == "parse");
        for (var i = 0; i < count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    public static TheoryData<int> AllVectorIndexes()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < VectorFile.Load<QueryTests.QueryVector>("http/query.json").Count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ParseVectorIndexes))]
    public void Parse_matches_the_vectors(int index)
    {
        var vector = VectorFile.Load<QueryTests.QueryVector>("http/query.json").Where(v => v.Op == "parse").ElementAt(index);

        var query = Query.Parse(vector.Input);

        var actual = query.SelectMany(group => group.Value.Select(value => new[] { group.Key, value })).ToArray();
        Assert.Equal(vector.Pairs.Length, actual.Length);
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(vector.Pairs[i][0], actual[i][0]);
            Assert.Equal(vector.Pairs[i][1], actual[i][1]);
        }

        Assert.Equal(vector.Encoded, query.Encode());
    }

    [Theory]
    [MemberData(nameof(AllVectorIndexes))]
    public void Parse_of_Encode_round_trips_for_every_vector(int index)
    {
        var vector = VectorFile.Load<QueryTests.QueryVector>("http/query.json")[index];
        var original = vector.Op == "parse"
            ? Query.Parse(vector.Input)
            : vector.Pairs.Aggregate(new Query.Builder(), (b, pair) => b.Add(pair[0]!, pair[1])).Build();

        var restored = Query.Parse(original.Encode());

        Assert.Equal(original, restored);
        Assert.Equal(original.Encode(), restored.Encode());
    }

    [Fact]
    public void Parse_null_or_blank_returns_Empty()
    {
        Assert.Same(Query.Empty, Query.Parse(null));
        Assert.Same(Query.Empty, Query.Parse(string.Empty));
        Assert.Same(Query.Empty, Query.Parse("  \t"));
    }

    [Fact]
    public void An_escaped_invalid_utf8_sequence_stays_raw()
    {
        var query = Query.Parse("a=%ED%A0%80&b=%FF");

        Assert.Equal("%ED%A0%80", query.Get("a"));
        Assert.Equal("%FF", query.Get("b"));
    }

    [Fact]
    public void A_literal_lone_surrogate_in_the_input_becomes_U_FFFD()
    {
        var withSurrogate = Query.Parse("a=\uD800");

        Assert.Equal("�", withSurrogate.Get("a"));
        Assert.Equal(Query.Parse("a=�"), withSurrogate);
        Assert.Equal(Query.Parse("a=�").Encode(), withSurrogate.Encode());
        Assert.True(Query.Parse("\uDC00=v").Contains("�"));
    }

    [Fact]
    public void A_literal_surrogate_pair_survives()
    {
        Assert.Equal("\U0001F600", Query.Parse("a=\U0001F600").Get("a"));
    }

    [Fact]
    public void Parse_never_throws_on_arbitrary_input()
    {
        var corpus = new[]
        {
            "%", "%%", "%%%", "=&=", "=", "==", "&", "?", "??", "?&", "a=%", "a=%2", "%=%", "a%=b", "\0", "a=\0",
            "%00", "a=\uD800\uD800", "\uDC00\uD800=\uDC00", "+", "a=+", new string('&', 4096), new string('=', 4096),
            "?" + string.Concat(Enumerable.Repeat("k=v&", 2000)), new string('%', 4097),
        };

        foreach (var input in corpus)
        {
            var query = Query.Parse(input);
            _ = query.Encode();
            _ = query.ToString();
            Assert.Equal(query, Query.Parse(query.Encode()));
        }
    }
}
