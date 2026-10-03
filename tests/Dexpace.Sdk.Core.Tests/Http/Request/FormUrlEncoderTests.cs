// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>
/// The WHATWG form serializer (HTTP-38; design P3b-7, fact 6) over the shared vector file
/// <c>tests/vectors/body/form-urlencoded.json</c>.
/// </summary>
[Trait("Category", "Unit")]
public class FormUrlEncoderTests
{
    public sealed record FormVector(string Name, string[][] Fields, string? Wire, bool Rejects);

    private static readonly IReadOnlyList<FormVector> s_vectors = VectorFile.Load<FormVector>("body/form-urlencoded.json");

    public static TheoryData<int> Cases()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < s_vectors.Count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    private static List<KeyValuePair<string, string>> Fields(FormVector vector) =>
        [.. vector.Fields.Select(f => KeyValuePair.Create(Unmark(f[0]), Unmark(f[1])))];

    private static string Unmark(string text) => text.Replace("<U+D800>", "\uD800", StringComparison.Ordinal);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_vector_case_encodes_to_its_wire_bytes_or_is_rejected(int index)
    {
        var vector = s_vectors[index];
        if (vector.Rejects)
        {
            Assert.Throws<ArgumentException>(() => FormUrlEncoder.Encode(Fields(vector)));
            return;
        }

        Assert.Equal(vector.Wire, Encoding.ASCII.GetString(FormUrlEncoder.Encode(Fields(vector))));
    }

    [Fact]
    public void A_lone_surrogate_throws_ArgumentException_naming_the_field_index_never_the_value()
    {
        KeyValuePair<string, string>[] fields = [new("ok", "fine"), new("bad", "secret\uD800")];

        var error = Assert.Throws<ArgumentException>(() => FormUrlEncoder.Encode(fields));

        Assert.Contains("field 1", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_null_name_or_value_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => FormUrlEncoder.Encode([new KeyValuePair<string, string>(null!, "v")]));
        Assert.Throws<ArgumentNullException>(() => FormUrlEncoder.Encode([new KeyValuePair<string, string>("k", null!)]));
        Assert.Throws<ArgumentNullException>(() => FormUrlEncoder.Encode(null!));
    }

    [Fact]
    public void Order_and_duplicates_are_kept()
    {
        var wire = FormUrlEncoder.Encode([new("b", "1"), new("a", "2"), new("b", "3")]);

        Assert.Equal("b=1&a=2&b=3", Encoding.ASCII.GetString(wire));
    }

    [Fact]
    public void Unreserved_star_dash_dot_underscore_are_literal_and_every_other_byte_is_uppercase_hex()
    {
        var all = new string([.. Enumerable.Range(0x21, 0x5E).Select(c => (char)c)]);

        var wire = Encoding.ASCII.GetString(FormUrlEncoder.Encode([new("k", all)]));

        Assert.Equal(
            "k=%21%22%23%24%25%26%27%28%29*%2B%2C-.%2F0123456789%3A%3B%3C%3D%3E%3F%40"
            + "ABCDEFGHIJKLMNOPQRSTUVWXYZ%5B%5C%5D%5E_%60abcdefghijklmnopqrstuvwxyz%7B%7C%7D%7E",
            wire);
    }
}
