// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Serialization;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Serdes;

/// <summary>
/// SEAM-20 (design §3.4, position G; plan ruling 4): the derived profiles over any codec's buffer primitive, and the
/// optional <see cref="IStringSerde"/> override.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerdeProfileTests
{
    private static readonly byte[] s_eAcute = [0xC3, 0xA9];

    private static BytesSerde Codec(byte[] payload, MediaType? type = null, Exception? failure = null) =>
        new(type ?? CommonMediaTypes.ApplicationJsonUtf8, payload, failure);

    [Fact]
    public void SerializeToUtf8Bytes_returns_the_payload_bytes()
    {
        Assert.Equal(new byte[] { 1, 2, 3 }, Codec([1, 2, 3]).SerializeToUtf8Bytes("v"));
    }

    [Fact]
    public void Two_SerializeToUtf8Bytes_calls_return_distinct_arrays()
    {
        var serde = Codec([1, 2, 3]);

        var first = serde.SerializeToUtf8Bytes("v");
        var second = serde.SerializeToUtf8Bytes("v");

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void SerializeToString_decodes_the_buffer_as_UTF8()
    {
        Assert.Equal("é", Codec(s_eAcute).SerializeToString("v"));
    }

    [Fact]
    public void SerializeToString_ignores_the_declared_charset()
    {
        var latin1 = Codec(s_eAcute, MediaType.Parse("text/plain; charset=iso-8859-1"));
        var utf7 = Codec(s_eAcute, MediaType.Parse("text/plain; charset=utf-7"));

        Assert.Equal("é", latin1.SerializeToString("v"));
        Assert.Equal("é", utf7.SerializeToString("v"));
    }

    [Fact]
    public void An_IStringSerde_override_wins_over_the_UTF8_decode()
    {
        var serde = new StringOverrideSerde(CommonMediaTypes.ApplicationJsonUtf8, s_eAcute, "override");

        Assert.Equal("override", serde.SerializeToString("v"));
    }

    [Fact]
    public void Fixed_buffer_serialize_returns_the_count_and_writes_at_the_offset()
    {
        var buffer = new byte[] { 9, 9, 0, 0, 0, 9 };

        var count = Codec([1, 2, 3]).Serialize(buffer.AsSpan(2), "v");

        Assert.Equal(3, count);
        Assert.Equal(new byte[] { 9, 9, 1, 2, 3, 9 }, buffer);
    }

    [Fact]
    public void Fixed_buffer_serialize_of_an_exact_fit_succeeds()
    {
        var buffer = new byte[3];

        Assert.Equal(3, Codec([4, 5, 6]).Serialize(buffer, "v"));
        Assert.Equal(new byte[] { 4, 5, 6 }, buffer);
    }

    [Fact]
    public void Fixed_buffer_overflow_throws_ArgumentOutOfRangeException_and_leaves_every_byte_untouched()
    {
        var buffer = new byte[] { 7, 7, 7, 7, 7, 7 };
        var serde = Codec([1, 2, 3, 4]);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => serde.Serialize(buffer.AsSpan(2, 3), "v"));

        Assert.Equal(new byte[] { 7, 7, 7, 7, 7, 7 }, buffer);
        Assert.Null(ex.InnerException);
        Assert.IsNotType<SdkException>(ex, exactMatch: false);
    }

    [Fact]
    public void A_SerializationException_from_the_primitive_surfaces_as_the_same_instance()
    {
        var failure = new SerializationException("encode failed");
        var serde = Codec([1], failure: failure);
        var buffer = new byte[4];

        Assert.Same(failure, Assert.Throws<SerializationException>(() => serde.SerializeToUtf8Bytes("v")));
        Assert.Same(failure, Assert.Throws<SerializationException>(() => serde.SerializeToString("v")));
        Assert.Same(failure, Assert.Throws<SerializationException>(() => serde.Serialize(buffer, "v")));
    }

    [Fact]
    public void An_IOException_from_the_primitive_propagates_unwrapped()
    {
        var failure = new IOException("disk");
        var serde = Codec([1], failure: failure);

        Assert.Same(failure, Assert.Throws<IOException>(() => serde.SerializeToUtf8Bytes("v")));
    }

    [Fact]
    public void A_null_serde_is_rejected_with_ArgumentNullException()
    {
        ISerde serde = null!;
        var buffer = new byte[4];

        Assert.Equal("serde", Assert.Throws<ArgumentNullException>(() => serde.SerializeToUtf8Bytes("v")).ParamName);
        Assert.Equal("serde", Assert.Throws<ArgumentNullException>(() => serde.SerializeToString("v")).ParamName);
        Assert.Equal("serde", Assert.Throws<ArgumentNullException>(() => serde.Serialize(buffer, "v")).ParamName);
    }
}
