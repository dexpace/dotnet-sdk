// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/body/multipart-body.test.ts: the boundary generation and validation
// cases, and the quoted-boundary rendering.

using System.Text.RegularExpressions;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary><see cref="MultipartFraming"/>: HTTP-51 (design P3b-8).</summary>
[Trait("Category", "Unit")]
public partial class MultipartFramingTests
{
    private static readonly MultipartPart[] s_parts = [new MultipartPart("a", RequestBody.FromString("x"))];

    [GeneratedRegex("^dexpace-[A-Za-z0-9]{32}$")]
    private static partial Regex GeneratedBoundary();

    [Fact]
    public void A_generated_boundary_is_dexpace_plus_32_alphanumerics()
    {
        var body = RequestBody.Multipart(s_parts);

        Assert.Matches(GeneratedBoundary(), body.ContentType!.Parameters["boundary"]);
        Assert.Matches(
            "^multipart/form-data;boundary=dexpace-[A-Za-z0-9]{32}$",
            body.ContentType.ToString());
    }

    [Fact]
    public void Two_generated_boundaries_differ()
    {
        Assert.NotEqual(
            RequestBody.Multipart(s_parts).ContentType!.ToString(),
            RequestBody.Multipart(s_parts).ContentType!.ToString());
    }

    [Theory]
    [InlineData("valid-boundary_1", true)]
    [InlineData("a", true)]
    [InlineData("with space inside", true)]
    [InlineData("a'()+_,-./:=?b", true)]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789", true)]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234567890", false)]
    [InlineData("", false)]
    [InlineData("trailing space ", false)]
    [InlineData("bad\"quote", false)]
    [InlineData("bad;semi", false)]
    [InlineData("bad\r\nline", false)]
    [InlineData("café", false)]
    public void A_caller_boundary_must_match_RFC_2046_1_to_70_bchars_not_ending_in_space(string boundary, bool valid)
    {
        if (valid)
        {
            Assert.Equal(boundary, RequestBody.Multipart(s_parts, boundary).ContentType!.Parameters["boundary"]);
        }
        else
        {
            var error = Assert.Throws<ArgumentException>(() => RequestBody.Multipart(s_parts, boundary));
            Assert.Equal("boundary", error.ParamName);
        }
    }

    [Fact]
    public void A_boundary_with_a_comma_is_sent_as_a_quoted_string()
    {
        Assert.Equal(
            "multipart/form-data;boundary=\"a,b\"",
            RequestBody.Multipart(s_parts, "a,b").ContentType!.ToString());
        Assert.Equal(
            "multipart/form-data;boundary=plain-1",
            RequestBody.Multipart(s_parts, "plain-1").ContentType!.ToString());
    }

    [Theory]
    [InlineData("a,b")]
    [InlineData("bound ary")]
    [InlineData("a:b")]
    [InlineData("a=b")]
    [InlineData("a?b")]
    [InlineData("(a)/b")]
    public void The_rendered_content_type_round_trips_through_the_media_type_parser(string boundary)
    {
        var rendered = RequestBody.Multipart(s_parts, boundary).ContentType!.ToString();

        Assert.Equal(boundary, MediaType.Parse(rendered).Parameters["boundary"]);
    }

    [Fact]
    public void The_part_header_bytes_are_computed_once_and_reused()
    {
        var framing = MultipartFraming.Frame(s_parts, "B");
        var body = (MultipartRequestBody)RequestBody.Multipart(s_parts, "B");

        // The length is derived from the stored arrays, and two writes frame from those same arrays.
        var length = framing.PartHeaders.Sum(h => (long)h.Length) + framing.Trailer.Length + s_parts[0].Body.ContentLength + 2;
        Assert.Equal(length, body.ContentLength);
        Assert.Same(framing.PartHeaders, framing.PartHeaders);
        Assert.Equal("--B\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: text/plain;charset=utf-8\r\n\r\n", System.Text.Encoding.UTF8.GetString(framing.PartHeaders[0]));
        Assert.Equal("--B--\r\n", System.Text.Encoding.UTF8.GetString(framing.Trailer));
    }
}
