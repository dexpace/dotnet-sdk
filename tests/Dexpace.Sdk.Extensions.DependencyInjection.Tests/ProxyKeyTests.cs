// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-28, CFG-24's first layer (P9-15): the explicit <c>Transport:Proxy:*</c> keys, and no key that reads the environment.</summary>
[Trait("Category", "Unit")]
public sealed class ProxyKeyTests
{
    [Fact]
    public void The_seven_keys_build_a_ProxyOptions()
    {
        var configuration = Config.Read(
            ("Transport:Proxy:Type", "socks5"),
            ("Transport:Proxy:Host", "proxy.test"),
            ("Transport:Proxy:Port", "1080"),
            ("Transport:Proxy:NonProxyHosts", "*.internal, localhost"),
            ("Transport:Proxy:UserName", "alice"),
            ("Transport:Proxy:Password", "s3cr3t"),
            ("Transport:Proxy:BypassAll", "false"));

        Assert.Empty(configuration.Failures);
        var proxy = Assert.IsType<ProxyOptions>(configuration.Transport.Proxy);
        Assert.Equal(ProxyType.Socks5, proxy.Type);
        Assert.Equal("proxy.test", proxy.Host);
        Assert.Equal(1080, proxy.Port);
        Assert.Equal(["*.internal", "localhost"], proxy.NonProxyHosts);
        Assert.Equal("alice", proxy.UserName);
        Assert.False(proxy.BypassAll);
        Assert.DoesNotContain("s3cr3t", proxy.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void No_proxy_key_means_no_proxy()
    {
        Assert.Null(Config.Read().Transport.Proxy);
    }

    [Theory]
    [InlineData("Transport:Proxy:Port", "Transport:Proxy:Host")]
    [InlineData("Transport:Proxy:Host", "Transport:Proxy:Port")]
    public void Any_proxy_key_requires_host_and_port(string present, string missing)
    {
        var configuration = Config.Read((present, present.EndsWith("Port", StringComparison.Ordinal) ? "8080" : "proxy.test"));

        var failure = Assert.Single(Config.Failures(configuration));
        Assert.Equal(Config.SectionPath + ":" + missing + ": required when any proxy key is present", failure);
        Assert.Null(configuration.Transport.Proxy);
    }

    [Theory]
    [InlineData("0x10")]
    [InlineData("70000")]
    [InlineData("-1")]
    public void A_bad_port_fails_under_its_key(string port)
    {
        var configuration = Config.Read(("Transport:Proxy:Host", "proxy.test"), ("Transport:Proxy:Port", port));

        Assert.Contains("Transport:Proxy:Port", Assert.Single(Config.Failures(configuration)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_bad_host_fails_without_the_value_and_a_bad_pattern_without_the_pattern()
    {
        var host = Assert.Single(Config.Failures(Config.Read(("Transport:Proxy:Host", "has/slash-s3cr3t"), ("Transport:Proxy:Port", "1"))));
        Assert.DoesNotContain("s3cr3t", host, StringComparison.Ordinal);
    }

    [Fact]
    public void No_key_turns_the_environment_read_on()
    {
        // P9-15: a value that makes the package read the environment is the implicit read P8b-4 forbids by another route.
        var configuration = Config.Read(("Transport:Proxy:FromEnvironment", "true"));

        Assert.Equal(Config.SectionPath + ":Transport:Proxy:FromEnvironment: unknown key", Assert.Single(Config.Failures(configuration)));
    }

    [Fact]
    public void The_password_never_appears_in_a_failure()
    {
        var configuration = Config.Read(
            ("Transport:Proxy:Host", "proxy.test"),
            ("Transport:Proxy:Port", "abc"),
            ("Transport:Proxy:Password", "s3cr3t"));

        Assert.DoesNotContain("s3cr3t", string.Join('|', Config.Failures(configuration)), StringComparison.Ordinal);
    }
}
