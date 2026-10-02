// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

/// <summary>HTTP-33: <see cref="ProtocolExtensions.Parse"/> folds case with ASCII rules.</summary>
[Trait("Category", "Unit")]
public class ProtocolTests
{
    [Theory]
    [InlineData("HTTP/2", Protocol.Http2)]
    [InlineData("HTTP/2.0", Protocol.Http2)]
    [InlineData("http/2", Protocol.Http2)]
    [InlineData("Http/2.0", Protocol.Http2)]
    [InlineData("HTTP/1.1", Protocol.Http11)]
    [InlineData("http/1.1", Protocol.Http11)]
    [InlineData("hTtP/1.0", Protocol.Http10)]
    [InlineData("H2_PRIOR_KNOWLEDGE", Protocol.H2PriorKnowledge)]
    [InlineData("QUIC", Protocol.Quic)]
    [InlineData("quic", Protocol.Quic)]
    public void Parse_accepts_HTTP_2_HTTP_2_0_and_mixed_case(string text, Protocol expected) =>
        Assert.Equal(expected, ProtocolExtensions.Parse(text));

    [Fact]
    public void Parse_is_culture_invariant_under_tr_TR()
    {
        // The tr-TR case is the one that could fail with a culture-sensitive fold; ToUpperInvariant was already
        // invariant, so on the as-built code this is expected to be a pin; the fold is now ASCII-only by construction.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal(Protocol.Http11, ProtocolExtensions.Parse("HTTP/1.1"));
            Assert.Equal(Protocol.Http11, ProtocolExtensions.Parse("http/1.1"));
            Assert.Equal(Protocol.Quic, ProtocolExtensions.Parse("quic"));
            Assert.Equal(Protocol.Quic, ProtocolExtensions.Parse("QUIC"));
            Assert.Throws<ArgumentException>(() => ProtocolExtensions.Parse("HTTPİ/1.1"));
            Assert.Throws<ArgumentException>(() => ProtocolExtensions.Parse("quİc"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("http/3")]
    [InlineData("HTTP/3")]
    [InlineData("")]
    [InlineData("spdy")]
    [InlineData("http/1.1 ")]
    [InlineData("ＨＴＴＰ/1.1")]
    public void Parse_rejects_http_3_and_unknown_text(string text) =>
        Assert.Throws<ArgumentException>(() => ProtocolExtensions.Parse(text));

    [Fact]
    public void Parse_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => ProtocolExtensions.Parse(null!));
    }

    [Theory]
    [InlineData(Protocol.Http10, "http/1.0")]
    [InlineData(Protocol.Http11, "http/1.1")]
    [InlineData(Protocol.Http2, "http/2")]
    [InlineData(Protocol.H2PriorKnowledge, "h2_prior_knowledge")]
    [InlineData(Protocol.Quic, "quic")]
    public void Wire_string_round_trips(Protocol protocol, string wire)
    {
        Assert.Equal(wire, protocol.ToWireString());
        Assert.Equal(protocol, ProtocolExtensions.Parse(wire));
    }
}
