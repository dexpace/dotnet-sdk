// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>The total Location resolver (REDIR-12, REDIR-13, REDIR-14, REDIR-18, REDIR-19; P6b-10, P6b-30).</summary>
[Trait("Category", "Unit")]
public sealed class RedirectLocationTests
{
    private static Headers Loc(params string[] values)
    {
        var builder = new Headers.Builder();
        foreach (var value in values)
        {
            builder.AddInbound("Location", value);
        }

        return builder.Build();
    }

    private static LocationOutcome Resolve(string current, Headers headers, out Uri? target, out string? raw) =>
        RedirectLocation.TryResolve(new Uri(current), headers, out target, out raw);

    private static string Ok(string current, string location)
    {
        Assert.Equal(LocationOutcome.Resolved, Resolve(current, Loc(location), out var target, out var raw));
        Assert.Null(raw);
        return target!.AbsoluteUri;
    }

    [Fact]
    public void A_missing_header_is_absent()
    {
        Assert.Equal(LocationOutcome.Absent, Resolve("https://h/a", Headers.Empty, out var target, out var raw));
        Assert.Null(target);
        Assert.Null(raw);
    }

    [Fact]
    public void An_empty_header_is_absent()
    {
        Assert.Equal(LocationOutcome.Absent, Resolve("https://h/a", Loc(string.Empty), out _, out var raw));
        Assert.Null(raw);
    }

    [Fact]
    public void An_all_whitespace_header_is_absent()
    {
        Assert.Equal(LocationOutcome.Absent, Resolve("https://h/a", Loc("   \t "), out _, out var raw));
        Assert.Null(raw);
    }

    [Theory]
    [InlineData("https://h/a/b", "c", "https://h/a/c")]
    [InlineData("https://h/a/b", "/x?y=1", "https://h/x?y=1")]
    [InlineData("https://h/a/b", "../z", "https://h/z")]
    public void A_relative_location_resolves_against_the_current_hop_not_the_seed(string current, string location, string expected) =>
        Assert.Equal(expected, Ok(current, location));

    [Fact]
    public void A_protocol_relative_location_keeps_the_current_scheme() =>
        Assert.Equal("https://other.example/p", Ok("https://h/a", "//other.example/p"));

    [Fact]
    public void An_absolute_location_is_used_as_is() =>
        Assert.Equal("http://other.example/p?q=1", Ok("https://h/a", "http://other.example/p?q=1"));

    [Theory]
    [InlineData("https://u:p@h/y", "https://h/y")]
    [InlineData("https://token@h/y", "https://h/y")]
    [InlineData("/y", "https://h/y")]
    public void Userinfo_is_dropped(string location, string expected) => Assert.Equal(expected, Ok("https://h/a", location));

    [Fact]
    public void Reserved_escapes_survive_resolution_and_stripping()
    {
        Assert.Equal("https://h/y%2Fz?a=%26", Ok("https://h/a", "https://u:p@h/y%2Fz?a=%26"));
        Assert.Equal("https://h/y#f%2F", Ok("https://h/a", "https://u:p@h/y#f%2F"));
        Assert.Equal("https://[::1]:8443/y", Ok("https://h/a", "https://u:p@[::1]:8443/y"));
        Assert.Equal("https://h:8443/y", Ok("https://h/a", "https://u:p@h:8443/y"));
        Assert.Equal("https://h/y%2Fz?a=%26", Ok("https://h/a", "/y%2Fz?a=%26"));
    }

    [Fact]
    public void An_explicit_default_port_is_elided() =>
        // P6b-30: the residue is stated, not a bug. The origin and the wire meaning are unchanged.
        Assert.Equal("https://h/y", Ok("https://h/a", "https://h:443/y"));

    [Theory]
    [InlineData("ftp://x/y")]
    [InlineData("FTP://x/y")]
    [InlineData("mailto:a@b")]
    [InlineData("javascript:1")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:,x")]
    public void A_non_http_scheme_is_malformed(string location)
    {
        Assert.Equal(LocationOutcome.Malformed, Resolve("https://h/a", Loc(location), out var target, out var raw));
        Assert.Null(target);
        Assert.Equal(location, raw);
    }

    [Theory]
    [InlineData("http:///p")]
    [InlineData("https://")]
    [InlineData("http:foo")]
    [InlineData("//")]
    [InlineData("http://")]
    public void An_empty_host_is_malformed(string location) =>
        Assert.Equal(LocationOutcome.Malformed, Resolve("https://h/a", Loc(location), out _, out _));

    [Fact]
    public void Two_location_values_are_malformed()
    {
        Assert.Equal(LocationOutcome.Malformed, Resolve("https://h/a", Loc("/a", "/b"), out var target, out var raw));
        Assert.Null(target);
        Assert.NotNull(raw);
    }

    [Theory]
    [InlineData("/a b", "https://h/a%20b")]
    [InlineData("/é", "https://h/%C3%A9")]
    public void A_value_with_spaces_or_non_ASCII_is_followed_percent_encoded(string location, string expected) =>
        Assert.Equal(expected, Ok("https://h/x", location));

    [Fact]
    public void A_fragment_only_or_fragment_bearing_location_resolves()
    {
        Assert.Equal("https://h/a/b?q=1#frag", Ok("https://h/a/b?q=1", "#frag"));
        Assert.Equal("https://h/c#frag", Ok("https://h/a", "/c#frag"));
    }

    [Theory]
    [InlineData("http://\u00e4.xn--zz/")]
    [InlineData("http://\uffff/")]
    [InlineData("http://a\u200db/")]
    public void An_invalid_IDN_host_is_malformed_not_a_throw(string location) =>
        Assert.Equal(LocationOutcome.Malformed, Resolve("https://h/a", Loc(location), out _, out _));

    [Fact]
    public void The_resolver_never_throws_for_hostile_values()
    {
        string[] hostile =
        [
            new string('a', 70_000), "http://[::1", "http://a b/", "//", "\\\\host\\share", "http://a:99999/", "%",
            "http://%41/", "\ud800", "   x   ", "http://", "https://:80/", "http://@/", "?", "..", "///",
            "http://\u00e4.xn--zz/", "http://\uffff/", "http://a\u200db/",
        ];
        foreach (var value in hostile)
        {
            var outcome = Resolve("https://h/a", Loc(value), out var target, out _);
            Assert.True(
                outcome != LocationOutcome.Resolved || (target is { IsAbsoluteUri: true } && target.IdnHost.Length > 0),
                $"unusable target accepted for '{value}'");
        }
    }
}
