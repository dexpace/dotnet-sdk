// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Vectors: tests/vectors/http/query.json, ported from nodejs-sdk packages/core/src/http/query-params.test.ts.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>HTTP-28, HTTP-29, HTTP-30, HTTP-5 (Query half) and HTTP-3 (Query half).</summary>
[Trait("Category", "Unit")]
public class QueryTests
{
    /// <summary>One row of query.json.</summary>
    public sealed record QueryVector(string Op, string? Input, string?[][] Pairs, string Encoded);

    public static TheoryData<int> EncodeVectorIndexes() => Indexes("encode");

    public static TheoryData<string, string> EqualityPairs() => new()
    {
        { "a=1&b=2", "a=1&b=2" },
        { "a=1&b=2", "b=2&a=1" },
        { "a=1&a=2", "a=2&a=1" },
        { "a=1", "A=1" },
        { "flag", "flag=" },
        { "a=1&b=2&a=3", "a=1&a=3&b=2" },
        { "a=1&b=2&a=3", "a=1&b=2&a=3" },
        { "", "" },
        { "a=1", "" },
    };

    [Fact]
    public void Names_are_case_sensitive()
    {
        var query = new Query.Builder().Add("page", "1").Add("Page", "2").Build();

        Assert.Equal("1", query.Get("page"));
        Assert.Equal("2", query.Get("Page"));
        Assert.Equal(2, query.Count);
        Assert.False(query.Contains("PAGE"));
    }

    [Fact]
    public void Insertion_order_is_first_appearance()
    {
        var query = new Query.Builder().Add("z", "1").Add("a", "2").Add("z", "3").Add("m", "4").Build();

        Assert.Equal((string[])["z", "a", "m"], query.Names);
        Assert.Equal(["z", "a", "m"], query.Select(pair => pair.Key));
    }

    [Fact]
    public void Multiple_values_per_name_are_kept_in_order()
    {
        var query = new Query.Builder().Add("x", "1").Add("y", "9").Add("x", "2").Add("x", "3").Build();

        Assert.Equal((string[])["1", "2", "3"], query.GetAll("x"));
        Assert.Equal("1", query.Get("x"));
        Assert.Equal(2, query.Count);
    }

    [Fact]
    public void Add_null_is_stored_as_the_empty_string()
    {
        var query = new Query.Builder().Add("flag", null).Build();

        Assert.Equal(string.Empty, query.Get("flag"));
        Assert.True(query.Contains("flag"));
        Assert.Equal((string[])[string.Empty], query.GetAll("flag"));
    }

    [Fact]
    public void Set_null_stores_a_single_empty_string_and_does_not_remove()
    {
        var query = new Query.Builder().Add("flag", "1").Add("flag", "2").Set("flag", (string?)null).Build();

        Assert.True(query.Contains("flag"));
        Assert.Equal((string[])[string.Empty], query.GetAll("flag"));
    }

    [Fact]
    public void Set_replaces_in_place_and_keeps_the_names_position()
    {
        var query = new Query.Builder().Add("a", "1").Add("b", "2").Add("c", "3").Set("b", "9").Build();

        Assert.Equal((string[])["a", "b", "c"], query.Names);
        Assert.Equal("9", query.Get("b"));
    }

    [Fact]
    public void Remove_drops_the_name()
    {
        var query = new Query.Builder().Add("a", "1").Add("b", "2").Remove("a").Remove("absent").Build();

        Assert.False(query.Contains("a"));
        Assert.Equal((string[])["b"], query.Names);
    }

    [Fact]
    public void Get_of_an_absent_name_is_null()
    {
        Assert.Null(Query.Empty.Get("missing"));
        Assert.False(Query.Empty.Contains("missing"));
    }

    [Fact]
    public void GetAll_of_an_absent_name_is_empty()
    {
        Assert.Empty(Query.Empty.GetAll("missing"));
    }

    [Theory]
    [MemberData(nameof(EncodeVectorIndexes))]
    public void Encode_matches_the_vectors(int index)
    {
        var vector = VectorFile.Load<QueryVector>("http/query.json").Where(v => v.Op == "encode").ElementAt(index);
        var builder = new Query.Builder();
        foreach (var pair in vector.Pairs)
        {
            builder.Add(pair[0]!, pair[1]);
        }

        Assert.Equal(vector.Encoded, builder.Build().Encode());
    }

    [Fact]
    public void Encode_has_no_leading_question_mark_and_is_empty_for_an_empty_query()
    {
        Assert.Equal(string.Empty, Query.Empty.Encode());
        Assert.Equal(string.Empty, new Query.Builder().Build().Encode());
        Assert.DoesNotContain('?', new Query.Builder().Add("a", "1").Build().Encode());
    }

    [Fact]
    public void Builder_rejects_an_empty_name()
    {
        Assert.Throws<ArgumentException>(() => new Query.Builder().Add(string.Empty, "v"));
        Assert.Throws<ArgumentException>(() => new Query.Builder().Set(string.Empty, "v"));
        Assert.Throws<ArgumentNullException>(() => new Query.Builder().Add(null!, "v"));
    }

    [Fact]
    public void Builder_rejects_a_lone_surrogate_in_a_name_or_value()
    {
        // A fact, not a theory: xUnit's data serialisation would replace a lone surrogate with U+FFFD.
        foreach (var (name, value) in new[] { ("sec\uD800ret", "v"), ("n", "sec\uDFFFret"), ("n", "ok\uD800") })
        {
            AssertRejected(name, value);
        }
    }

    private static void AssertRejected(string name, string value)
    {
        var add = Assert.Throws<ArgumentException>(() => new Query.Builder().Add(name, value));
        var set = Assert.Throws<ArgumentException>(() => new Query.Builder().Set(name, value));
        var setMany = Assert.Throws<ArgumentException>(() => new Query.Builder().Set(name, [value]));

        foreach (var exception in new[] { add, set, setMany })
        {
            Assert.Contains("U+D", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("sec", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("ret", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_well_formed_surrogate_pair_is_accepted()
    {
        var query = new Query.Builder().Add("emoji", "\U0001F600").Build();

        Assert.Equal("emoji=%F0%9F%98%80", query.Encode());
    }

    [Fact]
    public void GetAll_result_is_read_only()
    {
        // HTTP-5 pin: not a List<string>, and a mutating call through IList<string> throws.
        var query = new Query.Builder().Add("x", "1").Build();

        var values = query.GetAll("x");

        Assert.False(values is List<string>);
        var list = Assert.IsAssignableFrom<IList<string>>(values);
        Assert.Throws<NotSupportedException>(() => list.Add("2"));
        Assert.IsAssignableFrom<IList<string>>(query.Names);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)query.Names).Add("y"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)Query.Empty.GetAll("nope")).Add("y"));
    }

    [Fact]
    public void A_built_query_is_unaffected_by_later_builder_edits()
    {
        var builder = new Query.Builder().Add("a", "1");
        var query = builder.Build();

        builder.Add("a", "2").Add("b", "3").Remove("a");

        Assert.Equal((string[])["1"], query.GetAll("a"));
        Assert.False(query.Contains("b"));
        Assert.Equal("a=1", query.Encode());
    }

    [Fact]
    public void ToBuilder_copies_into_a_fresh_builder()
    {
        var original = new Query.Builder().Add("x", "1").Add("x", "2").Add("y", "3").Build();

        var derived = original.ToBuilder().Add("x", "4").Build();

        Assert.Equal((string[])["1", "2", "4"], derived.GetAll("x"));
        Assert.Equal("3", derived.Get("y"));
        Assert.Equal((string[])["1", "2"], original.GetAll("x"));
    }

    [Fact]
    public void ToString_lists_names_only_never_values()
    {
        var query = new Query.Builder().Add("page", "1").Add("token", "secret").Build();

        Assert.Equal("Query[page, token]", query.ToString());
        Assert.DoesNotContain("secret", query.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EqualityPairs))]
    public void Equality_holds_exactly_when_Encode_is_equal(string left, string right)
    {
        var a = Build(left);
        var b = Build(right);

        var encodedEqual = a.Encode() == b.Encode();

        Assert.Equal(encodedEqual, a.Equals(b));
        Assert.Equal(encodedEqual, a == b);
        Assert.Equal(!encodedEqual, a != b);
        if (encodedEqual)
        {
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }
    }

    [Fact]
    public void Equality_is_order_sensitive_across_names()
    {
        Assert.NotEqual(Build("a=1&b=2"), Build("b=2&a=1"));
    }

    [Fact]
    public void Equality_handles_null_and_other_types()
    {
        Assert.False(Query.Empty.Equals((Query?)null));
        Assert.False(Query.Empty.Equals((object?)"x"));
        Assert.True(Query.Empty == new Query.Builder().Build());
        Assert.True((Query?)null == null);
        Assert.True(Query.Empty != null);
    }

    [Fact]
    public void Set_with_an_empty_sequence_drops_the_name_at_Build()
    {
        var query = new Query.Builder().Add("x", "1").Add("y", "2").Set("x", Array.Empty<string?>()).Build();

        Assert.False(query.Contains("x"));
        Assert.Equal(1, query.Count);
        Assert.Equal("y=2", query.Encode());
        Assert.Equal(0, new Query.Builder().Set("x", Array.Empty<string?>()).Build().Count);
    }

    [Fact]
    public void Set_with_a_sequence_replaces_the_values_and_maps_null_to_empty()
    {
        var query = new Query.Builder().Add("x", "old").Set("x", ["1", null, "3"]).Build();

        Assert.Equal((string[])["1", string.Empty, "3"], query.GetAll("x"));
    }

    [Fact]
    public void A_query_works_as_a_Dictionary_key()
    {
        var map = new Dictionary<Query, string> { [Build("a=1&b=2")] = "found" };

        Assert.Equal("found", map[Build("a=1&b=2")]);
        Assert.False(map.ContainsKey(Build("b=2&a=1")));
    }

    private static Query Build(string text)
    {
        var builder = new Query.Builder();
        foreach (var segment in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0)
            {
                builder.Add(segment, null);
            }
            else
            {
                builder.Add(segment[..eq], segment[(eq + 1)..]);
            }
        }

        return builder.Build();
    }

    private static TheoryData<int> Indexes(string op)
    {
        var data = new TheoryData<int>();
        var count = VectorFile.Load<QueryVector>("http/query.json").Count(v => v.Op == op);
        for (var i = 0; i < count; i++)
        {
            data.Add(i);
        }

        return data;
    }
}
