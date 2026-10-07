// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-22, CFG-8 (collection rule), TRANSPORT-30 (masked ToString), P5a-13, R6, R9.
[Trait("Category", "Unit")]
public sealed class ProxyOptionsTests
{
    private static ProxyOptions Make(string host = "proxy.internal", int port = 3128) => new() { Host = host, Port = port };

    [Fact]
    public void Defaults_are_Http_no_credentials_no_bypass()
    {
        var options = Make();

        Assert.Equal(ProxyType.Http, options.Type);
        Assert.Empty(options.NonProxyHosts);
        Assert.Null(options.UserName);
        Assert.Null(options.Password);
        Assert.Null(options.ChallengeCredentials);
        Assert.False(options.BypassAll);
    }

    [Fact]
    public void Host_and_Port_are_required_members()
    {
        var required = typeof(ProxyOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<RequiredMemberAttribute>() is not null)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["Host", "Port"], required);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a b")]
    [InlineData("a/b")]
    [InlineData("a@b")]
    [InlineData("[::1]")]
    [InlineData("a\u0007b")]
    public void Host_rejects_blank_whitespace_and_delimiters(string host)
    {
        var error = Assert.Throws<ArgumentException>(() => Make(host));

        Assert.IsNotType<ArgumentNullException>(error);
        if (host.Trim().Length > 1)
        {
            Assert.DoesNotContain(host, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Host_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ProxyOptions { Host = null!, Port = 1 });
    }

    [Theory]
    [InlineData("proxy.internal")]
    [InlineData("10.0.0.1")]
    [InlineData("2001:db8::1")]
    public void Host_accepts_a_name_an_ipv4_and_a_bare_ipv6_address(string host)
    {
        Assert.Equal(host, Make(host).Host);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(int.MinValue)]
    public void Port_must_be_in_0_to_65535(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(port: port));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    public void Port_accepts_the_boundaries(int port)
    {
        Assert.Equal(port, Make(port: port).Port);
    }

    [Fact]
    public void An_undefined_Type_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProxyOptions { Host = "h", Port = 1, Type = (ProxyType)99 });
    }

    [Fact]
    public void NonProxyHosts_is_copied_at_init()
    {
        var source = new List<string> { "a.test", "b.test" };
        var options = new ProxyOptions { Host = "h", Port = 1, NonProxyHosts = source };

        source.Add("c.test");
        source[0] = "changed";

        Assert.Equal(["a.test", "b.test"], options.NonProxyHosts);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(options.NonProxyHosts);
        Assert.NotSame(source, options.NonProxyHosts);
        Assert.True(options.IsBypassed("a.test"));
        Assert.False(options.IsBypassed("changed"));
    }

    [Fact]
    public void NonProxyHosts_rejects_null_and_null_entries()
    {
        Assert.Throws<ArgumentNullException>(() => new ProxyOptions { Host = "h", Port = 1, NonProxyHosts = null! });
        Assert.Throws<ArgumentNullException>(() => new ProxyOptions { Host = "h", Port = 1, NonProxyHosts = ["a", null!] });
    }

    [Fact]
    public void ToString_masks_credentials()
    {
        var options = new ProxyOptions
        {
            Host = "proxy.internal",
            Port = 3128,
            NonProxyHosts = ["*.internal", "localhost"],
            UserName = "alice",
            Password = "hunter2",
            ChallengeCredentials = new NetworkCredential("x", "y"),
        };

        Assert.Equal(
            "ProxyOptions { Type = Http, Host = proxy.internal, Port = 3128, NonProxyHosts = [*.internal, localhost], UserName = ***, Password = ***, ChallengeCredentials = set, BypassAll = False }",
            options.ToString());
        Assert.Equal(
            "ProxyOptions { Type = Http, Host = proxy.internal, Port = 3128, NonProxyHosts = [], UserName = (none), Password = (none), ChallengeCredentials = (none), BypassAll = False }",
            Make().ToString());
    }

    [Theory]
    [InlineData("alice", "hunter2", "bob", "swordfish")]
    [InlineData("", "", "x", "y")]
    [InlineData("proxy", "internal", "p", "q")]
    [InlineData("u", "", "", "p")]
    [InlineData("3128", "Http", "a", "b")]
    [InlineData("secret-user", "secret-pass", "other", "other2")]
    public void ToString_text_is_independent_of_the_credential_values(string user1, string pass1, string user2, string pass2)
    {
        var first = new ProxyOptions { Host = "proxy.internal", Port = 3128, UserName = user1, Password = pass1 };
        var second = new ProxyOptions { Host = "proxy.internal", Port = 3128, UserName = user2, Password = pass2 };

        Assert.Equal(first.ToString(), second.ToString());
    }

    [Fact]
    public void ToString_renders_an_ipv6_host_re_bracketed()
    {
        Assert.Contains("Host = [2001:db8::1],", Make("2001:db8::1").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Equality_compares_the_pattern_list_by_content_and_credentials_by_value()
    {
        var a = new ProxyOptions { Host = "h", Port = 1, NonProxyHosts = ["a.test", "b.test"], UserName = "u", Password = "p" };
        var b = new ProxyOptions { Host = "h", Port = 1, NonProxyHosts = new List<string> { "a.test", "b.test" }, UserName = "u", Password = "p" };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a == b);
        Assert.NotEqual(a, a with { NonProxyHosts = ["b.test", "a.test"] });
        Assert.NotEqual(a, a with { Password = "other" });
        Assert.NotEqual(a, a with { Port = 2 });

        var credentials = new NetworkCredential("x", "y");
        var c = a with { ChallengeCredentials = credentials };
        Assert.Equal(c, a with { ChallengeCredentials = credentials });
        Assert.NotEqual(c, a with { ChallengeCredentials = new NetworkCredential("x", "y") });
    }

    [Fact]
    public void With_on_an_unrelated_member_keeps_the_compiled_patterns_and_with_on_NonProxyHosts_recompiles()
    {
        var original = new ProxyOptions { Host = "h", Port = 1, NonProxyHosts = ["a.test"] };

        var samePatterns = original with { Port = 2 };
        var newPatterns = original with { NonProxyHosts = ["b.test"] };

        Assert.Same(original.CompiledPatterns, samePatterns.CompiledPatterns);
        Assert.True(samePatterns.IsBypassed("a.test"));
        Assert.False(newPatterns.IsBypassed("a.test"));
        Assert.True(newPatterns.IsBypassed("b.test"));
    }
}
