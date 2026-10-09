// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list: every case of nodejs-sdk@c0ff3fd packages/core/src/pagination/query-splice.test.ts (22 cases) except the
// one that pins the WHATWG URL query encode set (`?tag=<raw>` rewritten to `%3Craw%3E`), which asserts a JavaScript
// host fact; the rest of that case (RFC 3986-legal characters survive) is kept. Where .NET diverges the .NET
// expectation is asserted and the Node case it diverges from is named in a comment: names match ordinally on the
// decoded name (Node compares encoded names), and `System.Uri` canonicalises percent-escapes of unreserved
// characters before the splice sees the query (design section 10 entry 18, the PAGE-21 residual).

using Dexpace.Sdk.Core.Pagination;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-21 to PAGE-24: the internal query splice (design P7c-9, P7c-10).</summary>
[Trait("Category", "Unit")]
public sealed class QuerySpliceTests
{
    private static Uri At(string href) => new(href, UriKind.Absolute);

    private static string Query(Uri url) => url.Query.TrimStart('?');

    [Fact]
    public void Set_replaces_the_first_match_in_place_and_keeps_every_other_segment_verbatim()
    {
        var result = QuerySplice.Set(new Uri("https://h/p?flag&x=1&q=a+b&r=%2F"), "x", "2");
        Assert.Equal("https://h/p?flag&x=2&q=a+b&r=%2F", result.AbsoluteUri);
    }

    [Fact]
    public void Untargeted_parameters_survive_byte_for_byte_with_order_preserved()
    {
        var result = QuerySplice.Set(At("https://h/p?flag&filter=a:b&page=1"), "page", "2");
        Assert.Equal("flag&filter=a:b&page=2", Query(result));
    }

    [Fact]
    public void A_value_less_flag_stays_value_less()
    {
        var query = Query(QuerySplice.Set(At("https://h/p?flag&page=1"), "page", "2"));
        Assert.Contains("flag&", query, StringComparison.Ordinal);
        Assert.DoesNotContain("flag=", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Reserved_characters_in_untargeted_values_are_not_rewritten()
    {
        var result = QuerySplice.Set(At("https://h/p?a=x:y/z&page=1"), "page", "2");
        Assert.Equal("a=x:y/z&page=2", Query(result));
    }

    [Fact]
    public void Characters_rfc3986_permits_raw_in_a_query_survive_untouched()
    {
        var result = QuerySplice.Set(At("https://h/p?f=a:b/c!d$e(f)*g,h;i@j&page=1"), "page", "2");
        Assert.Equal("f=a:b/c!d$e(f)*g,h;i@j&page=2", Query(result));
    }

    [Fact]
    public void Existing_percent_escapes_in_untargeted_segments_are_not_re_encoded()
    {
        // Node: "URLSearchParams-style canonicalization does NOT happen".
        var result = QuerySplice.Set(At("https://h/p?msg=a%20b&path=x:y&page=1"), "page", "2");
        Assert.Equal("msg=a%20b&path=x:y&page=2", Query(result));
    }

    [Fact]
    public void A_newly_set_value_uses_rfc3986_component_encoding()
    {
        Assert.Equal("q=a%20b", Query(QuerySplice.Set(At("https://h/p"), "q", "a b")));
        Assert.Equal("token=a%2Bb%2Fc%3D", Query(QuerySplice.Set(At("https://h/p"), "token", "a+b/c=")));
        Assert.Equal("q=a%20b%2Bc%2Fd%3D", Query(QuerySplice.Set(At("https://h/p"), "q", "a b+c/d=")));
    }

    [Fact]
    public void A_non_ascii_value_is_written_as_utf8_percent_escapes()
    {
        Assert.Equal("q=%C3%A9%E2%82%AC", Query(QuerySplice.Set(At("https://h/p"), "q", "\u00E9\u20AC")));
    }

    [Fact]
    public void Reading_decodes_with_the_same_semantics_and_a_literal_plus_reads_back_as_plus()
    {
        Assert.Equal("a+b", QuerySplice.Get(At("https://h/p?q=a+b"), "q"));
        Assert.Equal("a b", QuerySplice.Get(At("https://h/p?q=a%20b"), "q"));
        Assert.Equal("a+b c", QuerySplice.Get(At("https://h/p?q=a%2Bb%20c"), "q"));
    }

    [Fact]
    public void A_value_less_flag_reads_as_empty_and_an_absent_name_as_null()
    {
        Assert.Equal(string.Empty, QuerySplice.Get(At("https://h/p?flag"), "flag"));
        Assert.Null(QuerySplice.Get(At("https://h/p?flag"), "other"));
        Assert.Null(QuerySplice.Get(At("https://h/p"), "other"));
    }

    [Fact]
    public void Get_shapes_empty_value_and_equals_inside_the_value()
    {
        Assert.Equal(string.Empty, QuerySplice.Get(At("https://h/p?a="), "a"));
        Assert.Equal("b=c", QuerySplice.Get(At("https://h/p?a=b=c"), "a"));
        Assert.Equal("1", QuerySplice.Get(At("https://h/p?&&a=1"), "a"));
    }

    [Fact]
    public void Reading_takes_the_first_match_when_a_name_repeats()
    {
        Assert.Equal("1", QuerySplice.Get(At("https://h/p?page=1&page=9"), "page"));
    }

    [Fact]
    public void Setting_replaces_the_first_occurrence_in_place_and_drops_later_duplicates()
    {
        // Breaking 5: the as-built splice kept duplicates.
        Assert.Equal("page=2&sort=asc", Query(QuerySplice.Set(At("https://h/p?page=1&sort=asc&page=9"), "page", "2")));
        Assert.Equal("x=9&y=2", Query(QuerySplice.Set(At("https://h/p?x=1&y=2&x=3"), "x", "9")));
    }

    [Fact]
    public void Setting_an_absent_parameter_appends_it()
    {
        Assert.Equal("sort=asc&page=2", Query(QuerySplice.Set(At("https://h/p?sort=asc"), "page", "2")));
        Assert.Equal("a=1&b=v", Query(QuerySplice.Set(At("https://h/p?a=1"), "b", "v")));
    }

    [Fact]
    public void Setting_on_a_url_with_no_query_creates_one_without_a_stray_separator()
    {
        var result = QuerySplice.Set(At("https://h/p"), "page", "2");
        Assert.Equal("page=2", Query(result));
        Assert.Equal("https://h/p?page=2", result.AbsoluteUri);
    }

    [Fact]
    public void Setting_null_removes_the_first_match_and_every_later_duplicate()
    {
        Assert.Equal("sort=asc", Query(QuerySplice.Set(At("https://h/p?page=1&sort=asc"), "page", null)));
        Assert.Equal("sort=asc", Query(QuerySplice.Set(At("https://h/p?page=1&sort=asc&page=9"), "page", null)));
    }

    [Fact]
    public void Removing_the_only_parameter_leaves_an_empty_query()
    {
        var result = QuerySplice.Set(At("https://h/p?page=1"), "page", null);
        Assert.Equal(string.Empty, Query(result));
        Assert.Equal("https://h/p", result.AbsoluteUri);
    }

    [Fact]
    public void Removing_an_absent_name_is_a_no_op_that_returns_an_equal_uri()
    {
        var source = At("https://h/p?a=1&b=2");
        Assert.Equal(source, QuerySplice.Set(source, "page", null));
    }

    [Fact]
    public void Stray_empty_segments_are_skipped_matching_http_31_query_parsing()
    {
        Assert.Equal("a=1&b=2&page=2", Query(QuerySplice.Set(At("https://h/p?a=1&&b=2&page=1"), "page", "2")));
    }

    [Fact]
    public void The_input_uri_is_not_mutated()
    {
        var source = At("https://h/p?page=1");
        _ = QuerySplice.Set(source, "page", "2");
        Assert.Equal("page=1", Query(source));
    }

    // ── name matching (P7c-9) ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Name_matching_is_ordinal_so_a_differently_cased_name_is_a_different_parameter()
    {
        // The as-built splice compared names case-insensitively (design 7.1): `?Page=1` + page=2 replaced `Page`.
        var result = QuerySplice.Set(At("https://h/p?Page=1"), "page", "2");
        Assert.Equal("Page=1&page=2", Query(result));
        Assert.Null(QuerySplice.Get(At("https://h/p?Page=1"), "page"));
    }

    [Fact]
    public void Names_match_on_the_decoded_form_so_an_escaped_space_and_a_literal_one_are_the_same_parameter()
    {
        // Node compares the encoded names; this port decodes first (P7c-9), so both spellings are one parameter.
        Assert.Equal("1", QuerySplice.Get(At("https://h/p?my%20key=1"), "my key"));
        Assert.Equal("1", QuerySplice.Get(At("https://h/p?my key=1"), "my key"));
        var result = QuerySplice.Set(At("https://h/p?my%20key=1&z=2"), "my key", "9");
        Assert.Equal("my%20key=9&z=2", Query(result));
    }

    [Fact]
    public void A_name_that_is_percent_encoded_in_the_url_matches_its_decoded_text()
    {
        Assert.Equal("v", QuerySplice.Get(At("https://h/p?a%2Fb=v"), "a/b"));
        Assert.Equal("a%2Fb=w", Query(QuerySplice.Set(At("https://h/p?a%2Fb=v"), "a/b", "w")));
    }

    // ── rebuild (PAGE-24, fact 1) ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_non_query_component_is_preserved_exactly()
    {
        var source = At("https://user:pw@host.example:8443/deep/path?page=1&keep=yes#frag");
        var result = QuerySplice.Set(source, "page", "2");
        Assert.Equal(source.Scheme, result.Scheme);
        Assert.Equal(source.UserInfo, result.UserInfo);
        Assert.Equal(source.Host, result.Host);
        Assert.Equal(source.Port, result.Port);
        Assert.Equal(source.AbsolutePath, result.AbsolutePath);
        Assert.Equal(source.Fragment, result.Fragment);
        Assert.Equal("page=2&keep=yes", Query(result));
    }

    [Fact]
    public void Userinfo_port_escaped_path_and_fragment_survive_the_rebuild()
    {
        var result = QuerySplice.Set(At("https://u:p@h:8443/a%2Fb/c?x=1#f"), "x", "2");
        Assert.Equal("https://u:p@h:8443/a%2Fb/c?x=2#f", result.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://h:443/p?x=1", "https://h/p?x=2")]
    [InlineData("http://h:80/p?x=1", "http://h/p?x=2")]
    public void No_explicit_default_port_is_written(string source, string expected)
    {
        // Pins that UriBuilder is gone: UriBuilder writes an explicit default port (design fact 1).
        var result = QuerySplice.Set(At(source), "x", "2");
        Assert.Equal(expected, result.AbsoluteUri);
        Assert.DoesNotContain(":443", result.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain(":80/", result.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void A_non_default_port_is_kept()
    {
        Assert.Equal("https://h:8443/p?x=2", QuerySplice.Set(At("https://h:8443/p?x=1"), "x", "2").AbsoluteUri);
    }

    [Fact]
    public void A_fragment_is_kept_when_the_query_is_created_or_removed()
    {
        Assert.Equal("https://h/p?x=2#f", QuerySplice.Set(At("https://h/p#f"), "x", "2").AbsoluteUri);
        Assert.Equal("https://h/p#f", QuerySplice.Set(At("https://h/p?x=1#f"), "x", null).AbsoluteUri);
    }

    [Fact]
    public void An_empty_path_url_rebuilds_with_the_root_path()
    {
        Assert.Equal("https://h/?x=2", QuerySplice.Set(At("https://h?x=1"), "x", "2").AbsoluteUri);
    }

    [Fact]
    public void The_uri_canonical_form_of_unreserved_escapes_is_the_documented_residual()
    {
        // Design section 10 entry 18: System.Uri decodes percent-escaped unreserved characters (%7E -> ~, %41 -> A)
        // before the splice reads the query, so `x=%7E%41` comes back as `x=~A`. This documents the residual of
        // PAGE-21's byte-for-byte rule on .NET; it is not a goal.
        var result = QuerySplice.Set(At("https://h/p?x=%7E%41&page=1"), "page", "2");
        Assert.Equal("x=~A&page=2", Query(result));
    }

    // ── lone surrogates (P7c-10, fact 11) ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_lone_surrogate_in_a_value_is_rejected_naming_the_parameter()
    {
        // A loop, not InlineData: the C# compiler writes attribute strings as UTF-8, which turns a lone surrogate
        // into U+FFFD before the test ever sees it.
        foreach (var value in new[] { "\uD800", "\uDFFF", "ok\uD800ok", "\uDC00\uD800" })
        {
            var ex = Assert.Throws<ArgumentException>(() => QuerySplice.Set(At("https://h/p?a=1"), "cursor", value));
            Assert.Equal("value", ex.ParamName);
        }
    }

    [Fact]
    public void The_message_names_the_query_parameter_and_never_echoes_the_value()
    {
        var ex = Assert.Throws<ArgumentException>(() => QuerySplice.Set(At("https://h/p?a=1"), "cursor", "secret\uD800"));
        Assert.Contains("cursor", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_lone_surrogate_in_the_name_is_rejected_by_set_get_and_remove()
    {
        var url = At("https://h/p?a=1");
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => QuerySplice.Set(url, "\uD800", "2")).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => QuerySplice.Get(url, "\uD800")).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => QuerySplice.Set(url, "\uDC00", null)).ParamName);
    }

    [Fact]
    public void A_paired_surrogate_is_ordinary_text_and_round_trips()
    {
        var result = QuerySplice.Set(At("https://h/p?a=1"), "cursor", "\U0001F600");
        Assert.Equal("a=1&cursor=%F0%9F%98%80", Query(result));
        Assert.Equal("\U0001F600", QuerySplice.Get(result, "cursor"));
    }

    // ── argument checks ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_null_url_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => QuerySplice.Set(null!, "a", "1"));
        Assert.Throws<ArgumentNullException>(() => QuerySplice.Get(null!, "a"));
    }

    [Fact]
    public void A_null_or_empty_name_is_rejected()
    {
        var url = At("https://h/p");
        Assert.Throws<ArgumentNullException>(() => QuerySplice.Set(url, null!, "1"));
        Assert.Throws<ArgumentException>(() => QuerySplice.Set(url, string.Empty, "1"));
        Assert.Throws<ArgumentNullException>(() => QuerySplice.Get(url, null!));
        Assert.Throws<ArgumentException>(() => QuerySplice.Get(url, string.Empty));
    }
}
