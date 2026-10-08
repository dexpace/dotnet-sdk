// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

/// <summary>REDIR-8 and REDIR-11: the RFC 6454 origin triple (design §6.2, P6b-12).</summary>
[Trait("Category", "Unit")]
public sealed class HttpOriginTests
{
    private static HttpOrigin O(string url) => HttpOrigin.From(new Uri(url));

    [Fact]
    public void From_lowercases_the_scheme_and_host()
    {
        Assert.Equal(new HttpOrigin("https", "example.com", 443), O("HTTPS://EXAMPLE.COM/a"));
    }

    [Fact]
    public void An_explicit_default_port_equals_an_absent_one()
    {
        Assert.Equal(O("https://h/"), O("https://h:443/"));
        Assert.Equal(O("http://h/"), O("http://h:80/"));
    }

    [Fact]
    public void A_different_port_is_a_different_origin() => Assert.NotEqual(O("https://h/"), O("https://h:8443/"));

    [Fact]
    public void A_different_scheme_is_a_different_origin() => Assert.NotEqual(O("https://h/"), O("http://h/"));

    [Fact]
    public void An_IDN_host_and_its_punycode_spelling_are_one_origin() =>
        Assert.Equal(O("https://bücher.example/"), O("https://xn--bcher-kva.example/"));

    [Fact]
    public void Userinfo_path_query_and_fragment_are_ignored() => Assert.Equal(O("https://h/"), O("https://u:p@h/a?b#c"));

    [Fact]
    public void An_IPv6_literal_keeps_its_brackets_out_of_the_host_comparison() =>
        Assert.Equal(O("https://[::1]:8443/"), O("https://[0:0:0:0:0:0:0:1]:8443/"));

    [Fact]
    public void From_rejects_a_relative_uri() =>
        Assert.Throws<ArgumentException>(() => HttpOrigin.From(new Uri("/a", UriKind.Relative)));
}
