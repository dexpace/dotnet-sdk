// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from the PAGE-19 cases of nodejs-sdk@c0ff3fd packages/core/src/pagination/strategies.test.ts. Where this port
// diverges the .NET expectation is asserted and the Node case is named: Node follows an absolute target on another
// origin ("an absolute rel=next target is used as-is") and follows `<not a url>` as a relative path; this port ends
// the stream on a cross-origin target unless allowCrossOrigin is set (design P7c-12) and rejects a target holding SP,
// controls, `<`, `>` or `"` before resolving it (design fact 3). The Node `http://[` case (a valid scheme with a broken
// authority) ends the stream here as well.

using System.Text;
using Dexpace.Sdk.Core.Pagination;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-19 and the P7c-12 origin guard: the internal <c>Link</c> target resolver.</summary>
[Trait("Category", "Unit")]
public sealed class LinkTargetTests
{
    private static readonly Uri s_base = new("https://api.example/repo/issues?page=1");
    private static readonly Uri s_template = new("https://api.example/repo/issues?page=1");

    private static Uri? Resolve(string? raw, bool allowCrossOrigin = false, Uri? responseUrl = null, Uri? template = null) =>
        LinkTarget.TryResolve(responseUrl ?? s_base, raw, template ?? s_template, allowCrossOrigin, out var target) ? target : null;

    [Fact]
    public void A_query_only_reference_keeps_the_path()
    {
        Assert.True(LinkTarget.TryResolve(s_base, "?page=2", s_template, allowCrossOrigin: false, out var target));
        Assert.Equal("https://api.example/repo/issues?page=2", target!.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://api.example/repo/issues?page=2", "https://api.example/repo/issues?page=2")]
    [InlineData("other?page=2", "https://api.example/repo/other?page=2")]
    [InlineData("/x?y=1", "https://api.example/x?y=1")]
    [InlineData("../pulls", "https://api.example/pulls")]
    public void Same_origin_references_resolve_by_rfc3986(string raw, string expected)
    {
        Assert.Equal(expected, Resolve(raw)!.AbsoluteUri);
    }

    [Fact]
    public void The_base_is_the_response_url_not_the_template()
    {
        var response = new Uri("https://api.example/repo/redirected/");
        Assert.Equal("https://api.example/repo/redirected/next?page=2", Resolve("next?page=2", responseUrl: response)!.AbsoluteUri);
    }

    [Fact]
    public void Surrounding_space_and_tab_are_trimmed_before_resolving()
    {
        Assert.Equal("https://api.example/repo/issues?page=2", Resolve(" \t?page=2 \t")!.AbsoluteUri);
    }

    // ── rejected before resolving (fact 3) ─────────────────────────────────────────────────────────────

    [Fact]
    public void A_target_that_is_not_a_url_ends_the_stream()
    {
        // Node follows `<not a url>` as the relative path /repo/not%20a%20url; System.Uri accepts it too (fact 3),
        // so the resolver screens the raw text first.
        Assert.Null(Resolve("not a url"));
    }

    [Theory]
    [InlineData("a\tb")]
    [InlineData("a b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a\"b")]
    [InlineData("https://api.example/x\ty")]
    [InlineData("https://api.example/<script>")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_target_with_forbidden_characters_or_no_text_is_rejected(string? raw)
    {
        Assert.Null(Resolve(raw));
    }

    [Fact]
    public void A_control_character_or_delete_is_rejected()
    {
        foreach (var control in new[] { (char)0, (char)1, (char)10, (char)13, (char)31, (char)127 })
        {
            Assert.Null(Resolve("https://api.example/x" + control + "y"));
        }
    }

    // ── scheme and host guard ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ftp://api.example/x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:a@b.example")]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://[")]
    [InlineData("https://")]
    [InlineData("https:///path")]
    public void A_non_http_scheme_or_a_broken_authority_ends_the_stream(string raw)
    {
        Assert.Null(Resolve(raw, allowCrossOrigin: true));
    }

    // ── userinfo (the REDIR-12 analogue) ───────────────────────────────────────────────────────────────

    [Fact]
    public void Userinfo_is_stripped_from_a_same_origin_target()
    {
        var target = Resolve("https://user:pw@api.example/repo/issues?page=2");
        Assert.NotNull(target);
        Assert.Equal(string.Empty, target.UserInfo);
        Assert.DoesNotContain("user", target.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("pw", target.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("https://api.example/repo/issues?page=2", target.AbsoluteUri);
    }

    // ── origin guard (P7c-12, facts 4 and 9) ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://evil.example/p2")]
    [InlineData("//evil.example/x")]
    [InlineData("https://api.example:8443/x")]
    [InlineData("http://api.example/x")]
    public void A_cross_origin_target_ends_the_stream_unless_allowed(string raw)
    {
        Assert.Null(Resolve(raw));
        Assert.NotNull(Resolve(raw, allowCrossOrigin: true));
    }

    [Fact]
    public void With_allowCrossOrigin_a_cross_origin_target_resolves_with_its_userinfo_still_stripped()
    {
        var target = Resolve("https://user:pw@evil.example/p2", allowCrossOrigin: true);
        Assert.Equal("https://evil.example/p2", target!.AbsoluteUri);
    }

    [Fact]
    public void A_network_path_reference_resolves_to_the_other_origin_when_allowed()
    {
        Assert.Equal("https://evil.example/x", Resolve("//evil.example/x", allowCrossOrigin: true)!.AbsoluteUri);
    }

    [Fact]
    public void The_comparison_is_against_the_template_origin_not_the_response_url()
    {
        // A first page that redirected to another origin does not license following links there (P7c-12,
        // design section 10 entry 15's seed-origin rule): the response URL is other.example, the template api.example.
        var response = new Uri("https://other.example/");
        Assert.Null(Resolve("https://other.example/n", responseUrl: response));
        Assert.Null(Resolve("n", responseUrl: response));
        Assert.NotNull(Resolve("https://other.example/n", allowCrossOrigin: true, responseUrl: response));
    }

    [Fact]
    public void The_default_port_spelled_out_is_still_the_same_origin()
    {
        Assert.NotNull(Resolve("https://api.example:443/p2"));
        Assert.NotNull(Resolve("HTTPS://API.EXAMPLE/p2"));
    }

    // ── total ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryResolve_never_throws_for_arbitrary_input_seed_20261009()
    {
        string[] alphabet =
        [
            "a", "b", "/", ":", "?", "#", "%", "%zz", "%41", " ", "\t", "<", ">", "\"", ".", "..", "//", "@", "[", "]",
            "http://", "https://", "\uD800", "\uDC00", "\0", "\n", "x=y", "&",
        ];
        var rng = new Random(20261009);
        for (var i = 0; i < 500; i++)
        {
            var builder = new StringBuilder();
            for (var c = rng.Next(0, 20); c > 0; c--)
            {
                builder.Append(alphabet[rng.Next(alphabet.Length)]);
            }

            // Also one very long string every so often.
            var raw = i % 100 == 0 ? new string('a', 70_000) + builder : builder.ToString();
            var thrown = Record.Exception(() => LinkTarget.TryResolve(s_base, raw, s_template, allowCrossOrigin: i % 2 == 0, out _));
            Assert.True(thrown is null, $"seed 20261009, case {i}: {thrown?.GetType().Name}");
        }
    }

    [Fact]
    public void A_resolved_target_is_always_absolute_http_or_https_with_a_host_and_no_userinfo_seed_20261009()
    {
        string[] alphabet = ["a", "b", "/", ":", "?", "#", "%41", ".", "..", "//", "@", "http://", "https://", "x=y", "&", "u:p@"];
        var rng = new Random(20261009);
        for (var i = 0; i < 500; i++)
        {
            var builder = new StringBuilder();
            for (var c = rng.Next(1, 12); c > 0; c--)
            {
                builder.Append(alphabet[rng.Next(alphabet.Length)]);
            }

            var raw = builder.ToString();
            if (!LinkTarget.TryResolve(s_base, raw, s_template, allowCrossOrigin: true, out var target))
            {
                continue;
            }

            var context = $"seed 20261009, case {i}: '{raw}'";
            Assert.True(target!.IsAbsoluteUri, context);
            Assert.True(target.Scheme is "http" or "https", context);
            Assert.True(target.IdnHost.Length > 0, context);
            Assert.True(target.UserInfo.Length == 0, context);
        }
    }
}
