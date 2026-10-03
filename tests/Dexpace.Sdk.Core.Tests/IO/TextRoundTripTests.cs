// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>IO-13: text round-trips through UTF-8 and ISO-8859-1 (design fact 8: both are in-box).</summary>
[Trait("Category", "Unit")]
public class TextRoundTripTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Non_ASCII_text_round_trips_through_UTF_8_and_ISO_8859_1(bool useAsync)
    {
        const string Text = "café über";

        // ISO-8859-1: the request writes the Latin-1 bytes, and a response declaring that charset reads them back.
        var latin1 = RequestBody.FromString(Text, encoding: Encoding.Latin1);
        using var latin1Bytes = new MemoryStream();
        await latin1.WriteToAsync(latin1Bytes, Token);
        Assert.Equal(0xE9, latin1Bytes.ToArray()[3]);
        var latin1Response = ResponseBody.FromBytes(latin1Bytes.ToArray(), MediaType.Parse("text/plain; charset=iso-8859-1"));
        Assert.Equal(Text, useAsync ? await latin1Response.ReadAsStringAsync(Token) : latin1Response.ReadAsString(Token));

        // UTF-8: the same text with a character outside Latin-1.
        const string Wide = "café €";
        var utf8 = RequestBody.FromString(Wide);
        using var utf8Bytes = new MemoryStream();
        await utf8.WriteToAsync(utf8Bytes, Token);
        var utf8Response = ResponseBody.FromBytes(utf8Bytes.ToArray(), MediaType.Parse("text/plain; charset=utf-8"));
        Assert.Equal(Wide, useAsync ? await utf8Response.ReadAsStringAsync(Token) : utf8Response.ReadAsString(Token));
    }
}
