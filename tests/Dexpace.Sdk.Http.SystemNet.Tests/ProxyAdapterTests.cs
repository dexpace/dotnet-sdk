// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-30, CFG-22, CFG-23, CFG-27, XCUT-18 (phase 8b, P8b-5): the <c>IWebProxy</c> installed over <see cref="ProxyOptions"/>,
/// its Basic-only credentials and the construction warnings.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProxyAdapterTests
{
    private static readonly Uri s_destination = new("http://origin.test/resource");

    [Theory]
    [InlineData(ProxyType.Http, "http://proxy.test:3128/")]
    [InlineData(ProxyType.Socks4, "socks4://proxy.test:3128/")]
    [InlineData(ProxyType.Socks5, "socks5://proxy.test:3128/")]
    public void GetProxy_returns_the_proxy_uri_for_its_type(ProxyType type, string expected)
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Type = type, Host = "proxy.test", Port = 3128 });

        Assert.Equal(expected, proxy.GetProxy(s_destination)!.AbsoluteUri);
    }

    [Fact]
    public void An_ipv6_proxy_host_is_bracketed()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "::1", Port = 8080 });

        var uri = proxy.GetProxy(s_destination)!;

        Assert.Equal("http://[::1]:8080/", uri.AbsoluteUri);
    }

    [Fact]
    public void The_proxy_uri_never_carries_the_credentials()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "proxy.test", Port = 3128, UserName = "alice", Password = "s3cret" });

        var uri = proxy.GetProxy(s_destination)!;

        Assert.Equal(string.Empty, uri.UserInfo);
        Assert.DoesNotContain("alice", uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", uri.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public void BypassAll_bypasses_every_destination_and_an_empty_list_bypasses_none()
    {
        Assert.True(new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, BypassAll = true }).IsBypassed(s_destination));
        Assert.False(new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1 }).IsBypassed(s_destination));
    }

    [Fact]
    public void Bypass_globs_follow_the_spec_dialect()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, NonProxyHosts = ["*.internal.example.com", ".literal.test", "10.0.0.?"] });

        Assert.True(proxy.IsBypassed(new Uri("http://a.internal.example.com/")));
        Assert.False(proxy.IsBypassed(new Uri("http://internal.example.com.evil.test/")));
        Assert.False(proxy.IsBypassed(new Uri("http://internal.example.com/")));

        // A leading dot is a literal (P5a-14), unlike curl.
        Assert.False(proxy.IsBypassed(new Uri("http://sub.literal.test/")));
        Assert.True(proxy.IsBypassed(new Uri("http://10.0.0.7/")));
        Assert.False(proxy.IsBypassed(new Uri("http://10.0.0.77/")));
    }

    [Fact]
    public void The_host_compared_is_the_idn_host_and_loopback_is_not_special_cased()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, NonProxyHosts = ["xn--bcher-kva.example", "::1"] });

        Assert.True(proxy.IsBypassed(new Uri("http://bücher.example/")));
        Assert.True(proxy.IsBypassed(new Uri("http://[::1]/")));
        Assert.False(proxy.IsBypassed(new Uri("http://127.0.0.1/")));
        Assert.False(proxy.IsBypassed(new Uri("http://localhost/")));
    }

    [Fact]
    public void Nothing_configured_answers_no_credentials()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1 });

        Assert.Null(proxy.Credentials);
    }

    [Fact]
    public void ChallengeCredentials_are_installed_as_they_are()
    {
        var hook = new CredentialCache();
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, ChallengeCredentials = hook, UserName = "ignored", Password = "ignored" });

        Assert.Same(hook, proxy.Credentials);
    }

    [Fact]
    public void A_user_name_installs_a_credential_that_answers_basic_only()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, UserName = "alice", Password = "s3cret" });
        var proxyUri = proxy.GetProxy(s_destination)!;

        var basic = proxy.Credentials!.GetCredential(proxyUri, "Basic");

        Assert.NotNull(basic);
        Assert.Equal("alice", basic.UserName);
        Assert.Equal("s3cret", basic.Password);
        Assert.NotNull(proxy.Credentials.GetCredential(proxyUri, "basic"));
        Assert.Null(proxy.Credentials.GetCredential(proxyUri, "Digest"));
        Assert.Null(proxy.Credentials.GetCredential(proxyUri, "NTLM"));
        Assert.Null(proxy.Credentials.GetCredential(proxyUri, "Negotiate"));
        Assert.Null(proxy.Credentials.GetCredential(proxyUri, "Kerberos"));
    }

    [Fact]
    public void A_user_name_without_a_password_answers_an_empty_password()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, UserName = "alice" });

        var basic = proxy.Credentials!.GetCredential(proxy.GetProxy(s_destination)!, "Basic");

        Assert.Equal("alice", basic!.UserName);
        Assert.Equal(string.Empty, basic.Password);
    }

    [Fact]
    public void Each_answer_is_a_fresh_credential_so_a_caller_cannot_change_the_next_one()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, UserName = "alice", Password = "s3cret" });
        var proxyUri = proxy.GetProxy(s_destination)!;

        var first = proxy.Credentials!.GetCredential(proxyUri, "Basic")!;
        first.Password = "tampered";

        Assert.Equal("s3cret", proxy.Credentials.GetCredential(proxyUri, "Basic")!.Password);
    }

    // Measured on .NET 10.0.401 (plan task 0.1): SocketsHttpHandler asks a proxy's ICredentials for a SOCKS proxy once per
    // connection with authType equal to the proxy URI's scheme, not "Basic"; the pair then drives the SOCKS5 username/password
    // sub-negotiation. SOCKS4 carries a user id only.
    [Fact]
    public void A_socks5_proxy_is_answered_under_the_scheme_the_runtime_passes_as_the_auth_type()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Type = ProxyType.Socks5, Host = "p", Port = 1, UserName = "alice", Password = "s3cret" });
        var proxyUri = proxy.GetProxy(s_destination)!;

        var socks = proxy.Credentials!.GetCredential(proxyUri, "socks5");

        Assert.Equal("alice", socks!.UserName);
        Assert.Equal("s3cret", socks.Password);
        Assert.Null(proxy.Credentials.GetCredential(proxyUri, "Basic"));
        Assert.Null(proxy.Credentials.GetCredential(proxyUri, "socks4"));
    }

    [Fact]
    public void A_socks4_proxy_is_answered_with_the_user_id_only()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Type = ProxyType.Socks4, Host = "p", Port = 1, UserName = "alice", Password = "s3cret" });
        var proxyUri = proxy.GetProxy(s_destination)!;

        var socks = proxy.Credentials!.GetCredential(proxyUri, "socks4");

        Assert.Equal("alice", socks!.UserName);
        Assert.Equal(string.Empty, socks.Password);
        Assert.Null(proxy.Credentials.GetCredential(proxyUri, "Basic"));
    }

    [Fact]
    public void Nothing_outside_can_inject_a_credential()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, UserName = "alice" });

        Assert.Throws<NotSupportedException>(() => proxy.Credentials = new NetworkCredential("x", "y"));
    }

    [Fact]
    public async Task The_adapter_is_stateless_and_thread_safe()
    {
        var proxy = new ProxyOptionsWebProxy(new ProxyOptions { Host = "p", Port = 1, NonProxyHosts = ["*.bypass.test"], UserName = "u", Password = "p" });

        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(
            () =>
            {
                for (var i = 0; i < 1_000 / 8; i++)
                {
                    try
                    {
                        _ = proxy.GetProxy(s_destination);
                        Assert.True(proxy.IsBypassed(new Uri("http://a.bypass.test/")));
                        Assert.False(proxy.IsBypassed(s_destination));
                        Assert.NotNull(proxy.Credentials!.GetCredential(proxy.GetProxy(s_destination)!, "Basic"));
                    }
                    catch (Xunit.Sdk.XunitException ex)
                    {
                        errors.Enqueue(ex);
                    }
                }
            },
            TestContext.Current.CancellationToken)));

        Assert.Empty(errors);
    }

    [Fact]
    public void A_clean_configuration_logs_nothing()
    {
        var logger = new RecordingLogger();

        ProxyWarnings.Emit(logger, new ProxyOptions { Host = "p", Port = 1, UserName = "alice", Password = "s3cret" });
        ProxyWarnings.Emit(logger, new ProxyOptions { Type = ProxyType.Socks5, Host = "p", Port = 1, UserName = "alice", Password = "s3cret" });
        ProxyWarnings.Emit(logger, new ProxyOptions { Type = ProxyType.Http, Host = "p", Port = 1, ChallengeCredentials = new CredentialCache() });

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void A_password_on_a_socks4_proxy_is_warned_about_once_without_the_values()
    {
        var logger = new RecordingLogger();

        ProxyWarnings.Emit(logger, new ProxyOptions { Type = ProxyType.Socks4, Host = "secret-proxy.test", Port = 1, UserName = "alice", Password = "s3cret" });

        var entry = Assert.Single(logger.Entries);
        AssertWarning(entry, "Password");
    }

    [Fact]
    public void Challenge_credentials_on_a_socks_proxy_are_warned_about_once()
    {
        foreach (var type in new[] { ProxyType.Socks4, ProxyType.Socks5 })
        {
            var logger = new RecordingLogger();

            ProxyWarnings.Emit(logger, new ProxyOptions { Type = type, Host = "secret-proxy.test", Port = 1, ChallengeCredentials = new CredentialCache() });

            var entry = Assert.Single(logger.Entries);
            AssertWarning(entry, "ChallengeCredentials");
        }
    }

    [Fact]
    public void Challenge_credentials_together_with_a_user_name_are_warned_about_once()
    {
        var logger = new RecordingLogger();

        ProxyWarnings.Emit(logger, new ProxyOptions { Host = "secret-proxy.test", Port = 1, ChallengeCredentials = new CredentialCache(), UserName = "alice", Password = "s3cret" });

        var entry = Assert.Single(logger.Entries);
        AssertWarning(entry, "UserName");
    }

    private static void AssertWarning(RecordedLog entry, string member)
    {
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(DexpaceLogEvents.TransportProxyFeatureUnsupportedId, entry.EventId.Id);
        Assert.Equal(DexpaceLogEvents.TransportProxyFeatureUnsupported, entry.EventId.Name);
        Assert.Contains(member, entry.Message, StringComparison.Ordinal);

        // XCUT-18, TRANSPORT-30: neither the rendered text nor any structured value carries a credential or the host.
        var everything = entry.Message + string.Join('|', entry.State.Select(pair => $"{pair.Key}={pair.Value}"));
        foreach (var secret in new[] { "alice", "s3cret", "secret-proxy.test" })
        {
            Assert.DoesNotContain(secret, everything, StringComparison.Ordinal);
        }
    }
}
