// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary><see cref="MultipartPart"/>: HTTP-51 (design P3b-8).</summary>
[Trait("Category", "Unit")]
public class MultipartPartTests
{
    private static readonly RequestBody s_body = RequestBody.FromString("x");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Render(MultipartPart part)
    {
        var body = RequestBody.Multipart([part], "B");
        using var sink = new MemoryStream();
        body.WriteTo(sink, Token);
        return Encoding.UTF8.GetString(sink.ToArray());
    }

    [Theory]
    [InlineData("a\rb", "U+000D")]
    [InlineData("a\nb", "U+000A")]
    [InlineData("a\0b", "U+0000")]
    [InlineData("a\tb", "U+0009")]
    [InlineData("a\u001Fb", "U+001F")]
    [InlineData("a\u007Fb", "U+007F")]
    public void Name_and_FileName_reject_CR_LF_and_other_C0_controls_and_DEL_naming_the_code_point_never_the_value(string value, string codePoint)
    {
        var name = Assert.Throws<ArgumentException>(() => new MultipartPart(value, s_body));
        var file = Assert.Throws<ArgumentException>(() => new MultipartPart("f", s_body, value));

        foreach (var error in new[] { name, file })
        {
            Assert.Contains(codePoint, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("a\rb", error.Message, StringComparison.Ordinal);
        }

        Assert.Equal("name", name.ParamName);
        Assert.Equal("fileName", file.ParamName);
    }

    [Fact]
    public void A_lone_surrogate_is_rejected_without_echoing_it() =>
        Assert.Throws<ArgumentException>(() => new MultipartPart("a\uD800", s_body));

    [Fact]
    public void Quote_and_backslash_are_backslash_escaped_in_the_header()
    {
        var rendered = Render(new MultipartPart("a\"b\\c", s_body, "d\"e\\f"));

        Assert.Contains("name=\"a\\\"b\\\\c\"", rendered, StringComparison.Ordinal);
        Assert.Contains("filename=\"d\\\"e\\\\f\"", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_ASCII_is_written_as_UTF8()
    {
        var body = RequestBody.Multipart([new MultipartPart("café", s_body)], "B");
        using var sink = new MemoryStream();
        body.WriteTo(sink, Token);

        var bytes = sink.ToArray();
        Assert.True(bytes.AsSpan().IndexOf("name=\"caf\u00E9\""u8) >= 0);
        Assert.True(bytes.AsSpan().IndexOf("caf\u00C3\u00A9"u8) < 0);
    }

    [Fact]
    public void A_null_name_or_body_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MultipartPart(null!, s_body));
        Assert.Throws<ArgumentNullException>(() => new MultipartPart("n", null!));
    }

    [Fact]
    public void An_empty_Name_is_allowed()
    {
        var part = new MultipartPart(string.Empty, s_body);

        Assert.Equal(string.Empty, part.Name);
        Assert.Contains("name=\"\"", Render(part), StringComparison.Ordinal);
    }

    [Fact]
    public void The_part_is_a_sealed_class_with_a_validating_constructor()
    {
        var type = typeof(MultipartPart);

        Assert.True(type.IsSealed);
        Assert.False(type.IsValueType);
        Assert.Null(type.GetMethod("<Clone>$"));
        Assert.Single(type.GetConstructors());
    }

    [Fact]
    public void The_accessors_return_what_was_given()
    {
        var part = new MultipartPart("n", s_body, "f.txt");

        Assert.Equal("n", part.Name);
        Assert.Equal("f.txt", part.FileName);
        Assert.Same(s_body, part.Body);
        Assert.Null(new MultipartPart("n", s_body).FileName);
    }
}
