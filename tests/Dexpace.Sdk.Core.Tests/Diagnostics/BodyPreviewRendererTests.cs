// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported preview cases (roadmap constraint 10) live in tests/vectors/redaction/body-previews.json; each names its origin
// in "note". Not ported: Ruby's duck-typed media type and frozen/BINARY String cases (host facts).
// Sources: ruby-sdk@5b17395 gems/dexpace-core/test/dexpace/instrumentation/preview_test.rb,
// nodejs-sdk@54aeed4 packages/core/src/observability/logging-step.test.ts.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-38, P5b-14: <see cref="BodyPreviewRenderer"/>.</summary>
[Trait("Category", "Unit")]
public sealed class BodyPreviewRendererTests
{
    public sealed record PreviewCase(string? MediaType, string BytesBase64, string Expected, string Note);

    public static TheoryData<string?, string, string, string> Vectors()
    {
        var data = new TheoryData<string?, string, string, string>();
        foreach (var c in VectorFile.Load<PreviewCase>("redaction/body-previews.json"))
        {
            data.Add(c.MediaType, c.BytesBase64, c.Expected, c.Note);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Vectors_match(string? mediaType, string bytesBase64, string expected, string note)
    {
        Assert.True(note.Length > 0);
        var parsed = mediaType is null ? null : MediaType.Parse(mediaType);

        Assert.Equal(expected, BodyPreviewRenderer.Render(Convert.FromBase64String(bytesBase64), parsed));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/json")]
    [InlineData("application/xml")]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("application/javascript")]
    [InlineData("application/x-ndjson")]
    [InlineData("application/yaml")]
    [InlineData("application/graphql")]
    [InlineData("application/problem+json")]
    [InlineData("application/hal+xml")]
    [InlineData("application/x-foo+yaml")]
    public void Every_text_media_type_in_the_design_list_is_text(string mediaType)
    {
        Assert.Equal("ok", BodyPreviewRenderer.Render("ok"u8, MediaType.Parse(mediaType)));
    }

    [Fact]
    public void A_cut_multibyte_sequence_yields_the_replacement_character_and_does_not_throw()
    {
        // V6: [0xE2, 0x82] is the first two bytes of the three-byte euro sign.
        Assert.Equal("�", BodyPreviewRenderer.Render([0xE2, 0x82], MediaType.Parse("text/plain")));
        Assert.Equal("a�", System.Text.Encoding.UTF8.GetString([(byte)'a', 0xE2, 0x82]));
    }

    [Fact]
    public void A_matching_BOM_is_stripped()
    {
        Assert.Equal("x", BodyPreviewRenderer.Render([0xEF, 0xBB, 0xBF, (byte)'x'], MediaType.Parse("text/plain; charset=utf-8")));
    }

    [Fact]
    public void The_renderer_is_total()
    {
        var random = new Random(38);
        string[] types = ["text/plain", "application/json; charset=utf-16", "image/png", "text/plain; charset=shift_jis", "multipart/mixed"];
        var buffer = new byte[64];
        for (var i = 0; i < 2000; i++)
        {
            random.NextBytes(buffer);
            var mediaType = random.Next(0, types.Length + 1) is var pick && pick == types.Length ? null : MediaType.Parse(types[pick]);

            var rendered = BodyPreviewRenderer.Render(buffer.AsSpan(0, random.Next(0, buffer.Length)), mediaType);

            Assert.NotNull(rendered);
        }
    }
}
