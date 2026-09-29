// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-core/test/dexpace/http/header_syntax_test.rb,
// ruby-sdk@5b17395 gems/dexpace-core/test/dexpace/http/header_name_test.rb,
// ruby-sdk@5b17395 gems/dexpace-core/test/dexpace/http/media_type_test.rb (HTTP-26 EncodingTest),
// nodejs-sdk@54aeed4 packages/core/src/http/headers.test.ts (HTTP-17 .. HTTP-20 describes) and
// nodejs-sdk@54aeed4 packages/core/src/http/media-type.test.ts (HTTP-26 describe).
// Not ported: the Ruby encoding-tag cases (ISO-2022-JP, UTF-16LE retagging, invalid UTF-8) and the "every predicate
// is total over a non-String" case, which are Ruby host-language facts (a .NET string has no encoding tag and the
// parameter is typed); the Ruby `escape` helper's exact `\x0D` spelling, which this port renders as `U+000D`
// (design §4.1); and the HeaderName equality/interning rows, which are HTTP-21/HTTP-13 and phase 2a's.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S1 (HTTP-17, HTTP-18, HTTP-20, HTTP-26, XCUT-18; design §4.1, §4.4): the model rejects a header
/// name or outbound value that could split a request, and a media type that could carry a line break into a
/// <c>Content-Type</c>, before any transport sees it. The verified defect: <c>Headers</c> validated nothing, so a value
/// <c>"a\r\nX-Injected: yes"</c> reached the socket as two header lines; <c>MediaType</c> accepted CR/LF in a parameter
/// value; and <c>HttpHeaderName.Of</c> echoed a raw carriage return in its message. The wire-level proof is
/// <c>Dexpace.Sdk.Http.SystemNet.Tests.Security.HeaderInjectionWireTests</c>. Permanent (roadmap constraint 5); phase
/// 2a's <c>Headers</c> rebuild owns the structural form.
/// </summary>
[Trait("Category", "Security")]
public sealed class HeaderInjectionValidationTests
{
    // Names HTTP-17 rejects (blank, C0 including CR/LF/NUL, DEL, non-ASCII), plus the RFC 9110 token-grammar
    // rejections this port adds (design §11 item 36): an interior space and a colon.
    public static TheoryData<string> InvalidNames => new()
    {
        string.Empty,
        "   ",
        "a\r\nb",
        "a\rb",
        "a\nb",
        "a\0b",
        "a\0",
        "a\r",
        "a\u007Fb",
        "X Trace",
        "héder",
        "X-Bad:Name",
    };

    // Values HTTP-18 rejects: every C0 but HTAB, DEL, and anything at or above 0x80.
    public static TheoryData<string> InvalidValues => new()
    {
        "a\r\nX-Injected: yes",
        "a\rb",
        "a\nb",
        "a\0b",
        "a\u0001b",
        "a\u007Fb",
        "vålue",
        "café",
    };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Every_string_entry_point_rejects_an_invalid_name(string name)
    {
        Assert.Throws<ArgumentException>(() => Headers.Empty.With(name, "v"));
        Assert.Throws<ArgumentException>(() => Headers.Empty.Set(name, "v"));
        Assert.Throws<ArgumentException>(() => new Headers.Builder().Add(name, "v"));
        Assert.Throws<ArgumentException>(() => new Headers.Builder().Set(name, "v"));
        Assert.Throws<ArgumentException>(() => HttpHeaderName.Of(name));
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void Every_string_entry_point_rejects_an_invalid_outbound_value(string value)
    {
        Assert.Throws<ArgumentException>(() => Headers.Empty.With("X-Tag", value));
        Assert.Throws<ArgumentException>(() => Headers.Empty.Set("X-Tag", value));
        Assert.Throws<ArgumentException>(() => new Headers.Builder().Add("X-Tag", value));
        Assert.Throws<ArgumentException>(() => new Headers.Builder().Set("X-Tag", value));
    }

    [Fact]
    public void A_horizontal_tab_and_printable_ascii_are_accepted_in_a_value()
    {
        var headers = Headers.Empty.With("X-Tag", "a\tb ~!").Set("X-Other", string.Empty);

        Assert.Equal("a\tb ~!", headers.Get("X-Tag"));
        Assert.Equal(string.Empty, headers.Get("X-Other"));
    }

    [Theory]
    [InlineData("  X-Trace  ")]
    [InlineData("\tX-Trace\t")]
    public void Surrounding_whitespace_is_trimmed_from_a_name_before_validation(string name)
    {
        var headers = Headers.Empty.Set(name, "v");

        Assert.Equal("v", headers.Get("X-Trace"));
        Assert.Equal("x-trace", Assert.Single(headers.Names));
        Assert.Equal("X-Trace", HttpHeaderName.Of(name).Original);
    }

    [Fact]
    public void A_rejected_value_is_named_by_code_point_and_never_echoed()
    {
        var error = Assert.Throws<ArgumentException>(
            () => Headers.Empty.With("Authorization", "secret-value-abc\r\ntoken"));

        Assert.Contains("U+000D", error.Message, StringComparison.Ordinal);
        Assert.Contains("Authorization", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value-abc", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', error.Message);
        Assert.DoesNotContain('\n', error.Message);
    }

    [Theory]
    [InlineData("a\rb", "U+000D")]
    [InlineData("a\nb", "U+000A")]
    [InlineData("a\0b", "U+0000")]
    [InlineData("héder", "U+00E9")]
    public void A_rejected_name_is_named_by_code_point_and_never_carries_the_raw_byte(string name, string codePoint)
    {
        foreach (var error in new[]
        {
            Assert.Throws<ArgumentException>(() => HttpHeaderName.Of(name)),
            Assert.Throws<ArgumentException>(() => Headers.Empty.Set(name, "v")),
        })
        {
            Assert.Contains(codePoint, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(name, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', error.Message);
            Assert.DoesNotContain('\n', error.Message);
            Assert.DoesNotContain('\0', error.Message);
        }
    }

    // HTTP-19 and XCUT-18's inbound half: a distinct lenient path for response headers relaxes only the non-ASCII rule.
    [Fact]
    public void The_inbound_path_permits_obs_text_that_the_outbound_path_rejects()
    {
        var headers = new Headers.Builder().AddInbound("Content-Disposition", "attachment; filename=caf\u00E9").Build();

        Assert.Equal("attachment; filename=caf\u00E9", headers.Get("content-disposition"));
        Assert.Throws<ArgumentException>(() => new Headers.Builder().Add("Content-Disposition", "caf\u00E9"));
    }

    [Theory]
    [InlineData("a\r\nb")]
    [InlineData("a\0b")]
    [InlineData("a\u007Fb")]
    [InlineData("a\u0001b")]
    public void The_inbound_path_still_rejects_a_control_character(string value)
    {
        var error = Assert.Throws<ArgumentException>(() => new Headers.Builder().AddInbound("X-Tag", value));

        Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void The_inbound_path_keeps_names_strict(string name) =>
        Assert.Throws<ArgumentException>(() => new Headers.Builder().AddInbound(name, "v"));

    [Fact]
    public void Request_WithHeader_goes_through_the_same_validation() =>
        Assert.Throws<ArgumentException>(
            () => Request.Get("https://api.example.com/").WithHeader("X-Custom", "a\r\nX-Injected: yes"));

    // HTTP-26: the HTTP-18 predicate applies to every media-type component, parameter values included.
    public static TheoryData<string> InvalidParameterValues => new()
    {
        "a\r\nX-Injected: yes",
        "a\nb",
        "a\0b",
        "a\u007Fb",
        "vålue",
    };

    [Theory]
    [MemberData(nameof(InvalidParameterValues))]
    public void A_media_type_parameter_value_that_is_not_header_safe_is_rejected(string value)
    {
        var error = Assert.Throws<ArgumentException>(
            () => MediaType.Of("text", "plain", new Dictionary<string, string> { ["name"] = value }));

        Assert.Contains("U+00", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("text/plain; q=\"a\r\nb\"")]
    [InlineData("text/plain; q=\"\u007F\"")]
    [InlineData("text/plain; q=vålue")]
    [InlineData("text/pl\rain")]
    [InlineData("text/pléin")]
    public void Parse_rejects_a_media_type_that_is_not_header_safe(string value)
    {
        var error = Assert.Throws<ArgumentException>(() => MediaType.Parse(value));

        Assert.DoesNotContain('\r', error.Message);
        Assert.DoesNotContain('\n', error.Message);
        Assert.DoesNotContain('\u007F', error.Message);
    }

    // Only RFC 9110 OWS (SP, HTAB) is trimmed while parsing: string.Trim() used to strip CR, LF and Unicode whitespace,
    // so each of these parsed.
    [Theory]
    [InlineData("text/plain\r\n", "U+000D")]
    [InlineData("text/plain;\r\n charset=utf-8", "U+000D")]
    [InlineData("text/plain; charset\n=utf-8", "U+000A")]
    [InlineData("\u00A0text/plain", "U+00A0")]
    [InlineData("text/html\r\n", "U+000D")]
    [InlineData("text/plain; charset=utf-8\r", "U+000D")]
    public void Parse_trims_only_optional_whitespace_and_rejects_any_other(string value, string codePoint)
    {
        var error = Assert.Throws<ArgumentException>(() => MediaType.Parse(value));

        Assert.Contains(codePoint, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("plain", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("html", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', error.Message);
        Assert.DoesNotContain('\n', error.Message);
        Assert.False(MediaType.TryParse(value, out var mediaType));
        Assert.Null(mediaType);
    }

    [Fact]
    public void Parse_still_trims_spaces_and_tabs_around_every_part()
    {
        var mediaType = MediaType.Parse(" \ttext/plain \t;\t charset = utf-8 \t");

        Assert.Equal("text/plain", mediaType.FullType);
        Assert.Equal("utf-8", mediaType.Parameters["charset"]);
    }

    [Fact]
    public void A_media_type_with_a_tab_or_quoted_specials_still_round_trips()
    {
        var mediaType = MediaType.Of("multipart", "form-data", new Dictionary<string, string> { ["boundary"] = "a;b\t\"c\"" });

        Assert.Equal(mediaType, MediaType.Parse(mediaType.ToString()));
    }
}
