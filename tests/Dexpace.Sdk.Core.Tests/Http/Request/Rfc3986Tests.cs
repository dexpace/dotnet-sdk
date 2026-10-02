// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Vectors: tests/vectors/http/rfc3986.json, ported from nodejs-sdk packages/core/src/http/rfc3986.test.ts (see the
// file's "source" field for the pinned revision).

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>HTTP-32: the one RFC 3986 component encoder.</summary>
[Trait("Category", "Unit")]
public class Rfc3986Tests
{
    public sealed record CodecCase(string Op, string Input, string Expected);

    public static TheoryData<string, string> EncodeVectors() => Vectors("encode");

    public static TheoryData<string, string> DecodeVectors() => Vectors("decode");

    [Theory]
    [MemberData(nameof(EncodeVectors))]
    public void EncodeComponent_matches_the_vectors(string input, string expected) =>
        Assert.Equal(expected, Rfc3986.EncodeComponent(input));

    [Theory]
    [MemberData(nameof(DecodeVectors))]
    public void DecodeComponent_matches_the_vectors(string input, string expected) =>
        Assert.Equal(expected, Rfc3986.DecodeComponent(input));

    [Fact]
    public void EncodeComponent_turns_a_lone_surrogate_into_the_replacement_bytes()
    {
        // Pin: documents why Query.Builder rejects lone surrogates upstream of the encoder (design HTTP-32).
        Assert.Equal("%EF%BF%BD", Rfc3986.EncodeComponent("\uD800"));
        Assert.Equal("a%EF%BF%BDb", Rfc3986.EncodeComponent("a\uDC00b"));
    }

    [Theory]
    [InlineData("%ED%A0%80")]
    [InlineData("%FF")]
    [InlineData("a%FFb")]
    public void DecodeComponent_leaves_an_escaped_invalid_utf8_sequence_raw(string input)
    {
        // Pin (design HTTP-31): an encoded lone surrogate and a bare 0xFF come back unchanged.
        Assert.Equal(input, Rfc3986.DecodeComponent(input));
    }

    private static TheoryData<string, string> Vectors(string op)
    {
        var data = new TheoryData<string, string>();
        foreach (var c in VectorFile.Load<CodecCase>("http/rfc3986.json").Where(c => c.Op == op))
        {
            data.Add(c.Input, c.Expected);
        }

        return data;
    }
}
