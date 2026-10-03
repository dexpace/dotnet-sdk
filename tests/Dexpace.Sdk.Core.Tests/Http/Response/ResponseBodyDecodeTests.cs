// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>
/// The one text-decode routine both string readers share (HTTP-42; design P3b-12, fact 5): the declared charset, else UTF-8,
/// with a leading BOM that matches the resolved encoding's preamble stripped. Every case runs through the async and the
/// sync reader, so 3a's IO-13 decode and the 3b BOM cases pass over the same routine.
/// </summary>
[Trait("Category", "Unit")]
public class ResponseBodyDecodeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<bool> Forms => [false, true];

    private static byte[] WithPreamble(Encoding encoding, string text) => [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];

    private static async Task<string> ReadAsync(bool sync, byte[] bytes, string? contentType) =>
        await ReadBodyAsync(sync, ResponseBody.FromBytes(bytes, contentType is null ? null : MediaType.Parse(contentType)));

    private static Task<string> ReadBodyAsync(bool sync, ResponseBody body) =>
        sync ? Task.FromResult(body.ReadAsString(Token)) : body.ReadAsStringAsync(Token);

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task A_UTF8_BOM_is_stripped_with_no_charset(bool sync) =>
        Assert.Equal("hi", await ReadAsync(sync, [0xEF, 0xBB, 0xBF, 0x68, 0x69], null));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task A_UTF8_BOM_is_stripped_with_charset_utf_8(bool sync) =>
        Assert.Equal("hi", await ReadAsync(sync, [0xEF, 0xBB, 0xBF, 0x68, 0x69], "text/plain; charset=utf-8"));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task A_UTF16_LE_BOM_is_stripped_under_charset_utf_16(bool sync) =>
        Assert.Equal("hi", await ReadAsync(sync, WithPreamble(Encoding.Unicode, "hi"), "text/plain; charset=utf-16"));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task A_UTF16_BE_BOM_is_stripped_under_charset_utf_16be(bool sync) =>
        Assert.Equal("hi", await ReadAsync(sync, WithPreamble(Encoding.BigEndianUnicode, "hi"), "text/plain; charset=utf-16be"));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task A_UTF32_BOM_is_stripped_under_charset_utf_32(bool sync) =>
        Assert.Equal("hi", await ReadAsync(sync, WithPreamble(Encoding.UTF32, "hi"), "text/plain; charset=utf-32"));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task A_BOM_that_does_not_match_the_resolved_charset_is_kept(bool sync)
    {
        // The declared charset wins (HTTP-42): UTF-8 BOM bytes under iso-8859-1 are three Latin-1 characters.
        var text = await ReadAsync(sync, [0xEF, 0xBB, 0xBF, 0x68], "text/plain; charset=iso-8859-1");

        Assert.Equal("ï»¿h", text);
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task Only_a_leading_BOM_is_stripped(bool sync) =>
        Assert.Equal("a﻿b", await ReadAsync(sync, [.. Encoding.UTF8.GetPreamble(), .. "a﻿b"u8.ToArray()], null));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task No_charset_decodes_as_UTF8(bool sync) =>
        Assert.Equal("café", await ReadAsync(sync, "café"u8.ToArray(), "text/plain"));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task An_unknown_charset_falls_back_to_UTF8(bool sync) =>
        Assert.Equal("café", await ReadAsync(sync, "café"u8.ToArray(), "text/plain; charset=no-such-charset"));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task An_empty_body_decodes_to_the_empty_string(bool sync) =>
        Assert.Equal(string.Empty, await ReadAsync(sync, [], null));

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task A_body_that_is_only_a_BOM_decodes_to_the_empty_string(bool sync) =>
        Assert.Equal(string.Empty, await ReadAsync(sync, Encoding.UTF8.GetPreamble(), null));

    [Fact]
    public async Task Decoding_is_the_one_routine_the_readers_share()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, 0x68, 0x69];
        var direct = TextDecoding.Decode(bytes, null);

        Assert.Equal("hi", direct);
        Assert.Equal(direct, await ReadAsync(sync: false, bytes, null));
        Assert.Equal(direct, await ReadAsync(sync: true, bytes, null));
        Assert.Equal("ï»¿", TextDecoding.Decode(bytes.AsSpan(0, 3), Encoding.Latin1));
    }
}
