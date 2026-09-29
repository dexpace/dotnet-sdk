// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S5 (OBS-11–OBS-15, XCUT-19; design §8.1): URL redaction is default-deny. Every query value and
/// fragment <c>key=value</c> token becomes <c>***</c> unless its decoded name is allow-listed (default exactly
/// <c>{api-version}</c>); userinfo becomes <c>***:***@</c>; scheme, host, port, path, the empty <c>?</c> and
/// value-less parameters are kept verbatim, nothing is re-encoded, and a failure yields <c>[malformed url]</c>.
/// Permanent (roadmap constraint 5); phase 5b's redactor rewrite (header redaction, OBS-16–OBS-18) owns the rework.
/// </summary>
/// <remarks>
/// Ported from ruby-sdk@5b17395 gems/dexpace-core/test/dexpace/instrumentation/redactor_test.rb (the <c>url</c>
/// entry point's pairs) and nodejs-sdk@54aeed4 packages/core/src/observability/redaction.test.ts (the
/// <c>redactUrl</c> pairs). Not ported: the header-value entry point (OBS-16–OBS-18, phase 5b); Node's WHATWG
/// normalisation pins (<c>https://EXAMPLE.com:443</c> → <c>https://example.com/</c>), which this port does not
/// perform, since OBS-14 forbids altering host or port; and Ruby's sentinel cases whose failure is a property of its
/// parser rather than of the URL (<c>http://h/%zz</c>, <c>https://h/x?a=%FF#%zz=1</c> — <see cref="Uri"/> accepts
/// both, and each still redacts safely).
/// </remarks>
[Trait("Category", "Security")]
public sealed class UrlRedactionDefaultDenyTests
{
    private static readonly UrlRedactor s_default = new();

    public static TheoryData<string, string> Pairs { get; } = new()
    {
        // OBS-11, XCUT-19(a): userinfo is masked unconditionally (ruby).
        { "https://alice:s3cret@example.com/path?api-version=1", "https://***:***@example.com/path?api-version=1" },
        { "https://alice@example.com/", "https://***:***@example.com/" },
        { "https://al%40ice:p%3Ass@example.com/", "https://***:***@example.com/" },
        { "https://user:secret@example.com/path", "https://***:***@example.com/path" }, // node

        // OBS-12: default-deny under the {api-version} allow-list, decoded and case-folded (ruby).
        {
            "https://example.com/p?api-version=2026-01-01&token=secret&API-Version=2&api%2Dversion=3",
            "https://example.com/p?api-version=2026-01-01&token=***&API-Version=2&api%2Dversion=3"
        },
        { "https://h/p?token=1&token=2&token=3&flag&empty=", "https://h/p?token=***&token=***&token=***&flag&empty=***" },
        { "https://h/p?%FF=secret&api-version=1", "https://h/p?%FF=***&api-version=1" },
        { "https://h/p?%zz=1&api-version=2&b=3", "https://h/p?%zz=***&api-version=2&b=***" },
        { "https://user:secret@h/p?%zz=1", "https://***:***@h/p?%zz=***" },
        { "https://h/p?a%=1", "https://h/p?a%=***" },
        { "https://h/x?a=%zz", "https://h/x?a=***" },
        { "https://example.com/p?api-version=1&token=abc", "https://example.com/p?api-version=1&token=***" }, // node
        { "https://example.com/p?a%20b=1", "https://example.com/p?a%20b=***" }, // node
        { "https://example.com/p?api-version=2&secret=123", "https://example.com/p?api-version=2&secret=***" }, // node

        // OBS-12: the names the deny-list never knew (design §8.1).
        {
            "https://bucket.s3.amazonaws.com/k?X-Amz-Credential=AKIA%2F20260928&X-Amz-Signature=abc123",
            "https://bucket.s3.amazonaws.com/k?X-Amz-Credential=***&X-Amz-Signature=***"
        },
        { "https://login.example.com/token?client_secret=s3cr3t&sv=2026", "https://login.example.com/token?client_secret=***&sv=***" },

        // OBS-13: fragment key=value tokens follow the query rule; a plain fragment is kept (ruby, node).
        { "https://h/p#access_token=SECRET", "https://h/p#access_token=***" },
        { "https://h/p#api-version=1&t=2", "https://h/p#api-version=1&t=***" },
        { "https://h/p#section", "https://h/p#section" },
        { "https://h/p#a/b?c", "https://h/p#a/b?c" },
        { "https://example.com/p#", "https://example.com/p#" },

        // OBS-14: scheme, host, port and path untouched; the empty ? survives; no spurious separator (ruby, node).
        { "https://Example.COM:8443/A/b%20c?", "https://Example.COM:8443/A/b%20c?" },
        { "http://h:80/", "http://h:80/" },
        { "https://h:443/p?t=1", "https://h:443/p?t=***" },
        { "https://[::1]:8080/p", "https://[::1]:8080/p" },
        { "file:///etc/hosts", "file:///etc/hosts" },
        { "//h/p?a=1", "//h/p?a=***" },
        { "/relative?a=1#b", "/relative?a=***#b" },
        { "", "" },
        { "https://h/p", "https://h/p" },
        { "http://h?", "http://h?" },
        { "http://h#", "http://h#" },
        { "https://example.com/p?", "https://example.com/p?" },
        { "https://example.com:8443/a/b?token=x", "https://example.com:8443/a/b?token=***" },
        { "http://h/p#a?b=c", "http://h/p#a?b=***" },
        { "https://example.com/p#a?b", "https://example.com/p#a?b" },

        // OBS-14: a trailing & (empty final pair) is dropped in a query and in a fragment (ruby).
        { "https://h/p?a=1&", "https://h/p?a=***" },
        { "https://h/p?a=1&&b=2", "https://h/p?a=***&&b=***" },
        { "https://h/p#a=1&", "https://h/p#a=***" },

        // Opaque URIs: nothing absent is written back; a query-shaped tail is redacted (ruby, node).
        { "mailto:support@example.com", "mailto:support@example.com" },
        { "mailto:a@b.com", "mailto:a@b.com" },
        { "urn:isbn:0451450523", "urn:isbn:0451450523" },
        { "data:text/plain,hello", "data:text/plain,hello" },
        { "mailto:support@example.com?subject=SECRET", "mailto:support@example.com?subject=***" },
        { "mailto:a@b?x=1&api-version=1#frag", "mailto:a@b?x=***&api-version=1#frag" },
        { "urn:isbn:1?q=1", "urn:isbn:1?q=***" },
        { "data:text/plain,hello?x=1", "data:text/plain,hello?x=***" },

        // OBS-11–OBS-13 together (ruby).
        { "https://u:p@h/x?k=v#k=v", "https://***:***@h/x?k=***#k=***" },
        { "https://u:p@h/?t=1", "https://***:***@h/?t=***" },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void A_parsed_uri_redacts_to_the_expected_form(string input, string expected) =>
        Assert.Equal(expected, s_default.Redact(new Uri(input, UriKind.RelativeOrAbsolute)));

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Url_text_redacts_to_the_same_form(string input, string expected) =>
        Assert.Equal(expected, s_default.Redact(input));

    [Theory]
    [InlineData("not a url at all ###")] // ruby, node
    [InlineData("https://h/a b")]        // ruby
    [InlineData("http://[::1")]          // ruby
    [InlineData("\0")]                   // ruby
    [InlineData("http://[bad")]
    [InlineData(" https://user:secret@h/p")]
    [InlineData("https:\\\\user:secret@h\\p?code=S")]
    public void Malformed_url_text_yields_the_sentinel(string input) =>
        Assert.Equal("[malformed url]", s_default.Redact(input));

    [Fact]
    public void A_backslash_authority_the_parser_accepts_is_still_masked()
    {
        // System.Uri reads "\\" as "//", so the text has no authority where the parsed URI has one; the parsed
        // form decides.
        var redacted = s_default.Redact(new Uri("http:\\\\user:secret@h/p?code=S"));

        Assert.Equal("http://***:***@h/p?code=***", redacted);
    }

    [Theory]
    [InlineData(" ftp://h/a?x=SEC")]
    [InlineData("ftp://h/a b?x=SEC")]
    [InlineData(" news://h/a?x=SEC")]
    [InlineData("gopher://U:PWD@h/a b?x=SEC")]
    public void A_query_the_canonical_form_loses_yields_the_sentinel(string input)
    {
        // System.Uri gives these schemes no query and escapes '?' to "%3F" in AbsoluteUri, so the canonical fallback
        // cannot find the value to redact; it fails closed rather than logging it.
        var redacted = s_default.Redact(new Uri(input));

        Assert.Equal("[malformed url]", redacted);
    }

    [Theory]
    [InlineData(" http://h/a?x=SEC", "http://h/a?x=***")]
    [InlineData("https://h/a b?x=SEC", "https://h/a%20b?x=***")]
    [InlineData(" https://U:PWD@h/a?x=SEC#k=v", "https://***:***@h/a?x=***#k=***")]
    public void A_query_the_canonical_form_keeps_is_still_redacted(string input, string expected) =>
        Assert.Equal(expected, s_default.Redact(new Uri(input)));

    [Fact]
    public void An_empty_allow_list_redacts_every_value()
    {
        // ruby: Policy.build(query_allow_list: []); node: redactUrl(url, new Set()).
        var redactor = new UrlRedactor([]);

        Assert.Equal(
            "https://h/p?api-version=***&token=***",
            redactor.Redact(new Uri("https://h/p?api-version=1&token=x")));
    }

    [Fact]
    public void A_custom_allow_list_is_matched_case_insensitively()
    {
        // node: redactUrl(url, new Set(['custom-PARAM'])).
        var redactor = new UrlRedactor(["custom-PARAM"]);

        Assert.Equal(
            "https://example.com/p?Custom-Param=val&secret=***&api-version=***",
            redactor.Redact(new Uri("https://example.com/p?Custom-Param=val&secret=123&api-version=1")));
    }

    [Theory]
    [InlineData("  https://u:p@H/p?q=1  ")]
    [InlineData("https://u:p@h/p?q=1\t")]
    public void A_uri_whose_original_text_is_not_canonical_still_loses_its_userinfo_and_values(string input)
    {
        // System.Uri trims leading and trailing whitespace before it parses, so the original text is not what it parsed.
        var redacted = s_default.Redact(new Uri(input));

        Assert.StartsWith("https://***:***@h/p", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("u:p", redacted, StringComparison.Ordinal);
        Assert.EndsWith("?q=***", redacted, StringComparison.Ordinal);
    }
}
