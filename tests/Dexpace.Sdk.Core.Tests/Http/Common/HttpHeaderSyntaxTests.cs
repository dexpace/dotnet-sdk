// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Tables follow nodejs-sdk packages/core/src/http/ascii-validation.test.ts (the plan names it as the source for
// HttpHeaderSyntax); the invalid-name set is the Security class's own, read through a shared MemberData and never
// edited.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Tests.Security;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

/// <summary>HTTP-17, HTTP-18, HTTP-19 and HTTP-20: the public header-syntax predicates (design position B).</summary>
[Trait("Category", "Unit")]
public class HttpHeaderSyntaxTests
{
    [Theory]
    [InlineData("X-Trace")]
    [InlineData("a")]
    [InlineData("Content-Type")]
    [InlineData("!#$%&'*+-.^_`|~09azAZ")]
    public void IsValidName_accepts_tokens(string name) => Assert.True(HttpHeaderSyntax.IsValidName(name));

    [Theory]
    [MemberData(nameof(HeaderInjectionValidationTests.InvalidNames), MemberType = typeof(HeaderInjectionValidationTests))]
    public void IsValidName_rejects_empty_space_colon_and_controls_from_the_shared_set(string name) =>
        Assert.False(HttpHeaderSyntax.IsValidName(name));

    [Theory]
    [InlineData(" X")]
    [InlineData("X ")]
    [InlineData("\tX")]
    [InlineData("X:")]
    [InlineData("a(b")]
    [InlineData("a,b")]
    [InlineData("a\"b")]
    public void IsValidName_does_not_trim_and_rejects_separators(string name) =>
        Assert.False(HttpHeaderSyntax.IsValidName(name));

    [Fact]
    public void IsValidName_rejects_the_empty_span() => Assert.False(HttpHeaderSyntax.IsValidName([]));

    [Theory]
    [InlineData("")]
    [InlineData("plain value")]
    [InlineData("a\tb")]
    [InlineData(" ~!\"#")]
    public void IsValidOutboundValue_accepts_only_HTAB_and_0x20_to_0x7E(string value) =>
        Assert.True(HttpHeaderSyntax.IsValidOutboundValue(value));

    [Theory]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    [InlineData("a\0b")]
    [InlineData("a\u007Fb")]
    [InlineData("café")]
    [InlineData("\u0001")]
    [InlineData("a\u0080")]
    public void IsValidOutboundValue_rejects_CR_LF_NUL_DEL_and_obs_text(string value) =>
        Assert.False(HttpHeaderSyntax.IsValidOutboundValue(value));

    [Theory]
    [InlineData("")]
    [InlineData("a\tb")]
    [InlineData("café")]
    [InlineData("\u0080ÿ")]
    [InlineData("plain ~")]
    public void IsValidInboundValue_allows_obs_text(string value) =>
        Assert.True(HttpHeaderSyntax.IsValidInboundValue(value));

    [Theory]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    [InlineData("a\0b")]
    [InlineData("a\u0001b")]
    [InlineData("a\u007Fb")]
    public void IsValidInboundValue_rejects_C0_but_HTAB_and_DEL(string value) =>
        Assert.False(HttpHeaderSyntax.IsValidInboundValue(value));

    [Fact]
    public void EscapeName_renders_every_non_token_char_as_backslash_u_XXXX()
    {
        Assert.Equal("a\\u0020b\\u000D", HttpHeaderSyntax.EscapeName("a b\r"));
        Assert.Equal("X-Trace", HttpHeaderSyntax.EscapeName("X-Trace"));
        Assert.Equal("h\\u00E9der", HttpHeaderSyntax.EscapeName("héder"));
        Assert.Equal("\\u0000", HttpHeaderSyntax.EscapeName("\0"));
        Assert.Equal(string.Empty, HttpHeaderSyntax.EscapeName(string.Empty));
        Assert.Equal("a\\u003Ab", HttpHeaderSyntax.EscapeName("a:b"));
    }

    [Fact]
    public void EscapeName_returns_the_same_instance_for_a_valid_token()
    {
        var name = "Content-Type";

        Assert.Same(name, HttpHeaderSyntax.EscapeName(name));
    }

    [Fact]
    public void EscapeName_never_carries_a_raw_control_character()
    {
        var escaped = HttpHeaderSyntax.EscapeName("a\r\nX-Injected: yes\0");

        Assert.DoesNotContain('\r', escaped);
        Assert.DoesNotContain('\n', escaped);
        Assert.DoesNotContain('\0', escaped);
        Assert.DoesNotContain(' ', escaped);
        Assert.DoesNotContain(':', escaped);
    }

    [Fact]
    public void EscapeName_rejects_null() => Assert.Throws<ArgumentNullException>(() => HttpHeaderSyntax.EscapeName(null!));

    [Fact]
    public void The_predicates_agree_with_the_throwing_validators()
    {
        // One character table (position B): for every character 0..0xFF a predicate result equals "the internal
        // validator does not throw".
        for (var code = 0; code <= 0xFF; code++)
        {
            var text = ((char)code).ToString();

            Assert.Equal(Passes(() => HeaderSyntax.ValidateName(text, "name")), HttpHeaderSyntax.IsValidName(text));
            Assert.Equal(
                Passes(() => HeaderSyntax.ValidateOutboundValue(text, "X", "value")),
                HttpHeaderSyntax.IsValidOutboundValue(text));
            Assert.Equal(
                Passes(() => HeaderSyntax.ValidateInboundValue(text, "X", "value")),
                HttpHeaderSyntax.IsValidInboundValue(text));
        }
    }

    private static bool Passes(Action validator)
    {
        try
        {
            validator();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
