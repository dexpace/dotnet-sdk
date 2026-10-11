// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>TRANSPORT-5, -13, -18, -30 (settings): the transport's options record, its defaults and its validation (phase 8b, P8b-2, R6).</summary>
[Trait("Category", "Unit")]
public sealed class SystemNetHttpClientOptionsTests
{
    [Fact]
    public void Defaults_are_the_documented_ones()
    {
        var options = new SystemNetHttpClientOptions();

        Assert.Same(NullLogger.Instance, options.Logger);
        Assert.Null(options.Proxy);
        Assert.Equal(TimeSpan.FromSeconds(100), options.Timeout);
        Assert.Equal(HeaderDropLogging.FirstPerName, options.HeaderDropLogging);
        Assert.Equal(1_048_576L, options.ResendBufferLimit);
        Assert.Same(TimeProvider.System, options.TimeProvider);
    }

    [Fact]
    public void A_null_logger_or_time_provider_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemNetHttpClientOptions { Logger = null! });
        Assert.Throws<ArgumentNullException>(() => new SystemNetHttpClientOptions { TimeProvider = null! });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_timeout_throws(int milliseconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemNetHttpClientOptions { Timeout = TimeSpan.FromMilliseconds(milliseconds) });

    [Fact]
    public void An_infinite_timeout_or_one_above_49_days_throws_and_the_bounds_are_accepted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemNetHttpClientOptions { Timeout = Timeout.InfiniteTimeSpan });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemNetHttpClientOptions { Timeout = TimeSpan.FromDays(49) + TimeSpan.FromMilliseconds(1) });

        Assert.Equal(TimeSpan.FromMilliseconds(1), new SystemNetHttpClientOptions { Timeout = TimeSpan.FromMilliseconds(1) }.Timeout);
        Assert.Equal(TimeSpan.FromDays(49), new SystemNetHttpClientOptions { Timeout = TimeSpan.FromDays(49) }.Timeout);
        Assert.Null(new SystemNetHttpClientOptions { Timeout = null }.Timeout);
    }

    [Fact]
    public void An_undefined_drop_logging_mode_throws_and_the_three_modes_are_accepted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemNetHttpClientOptions { HeaderDropLogging = (HeaderDropLogging)99 });

        Assert.All(
            Enum.GetValues<HeaderDropLogging>(),
            mode => Assert.Equal(mode, new SystemNetHttpClientOptions { HeaderDropLogging = mode }.HeaderDropLogging));
        Assert.Equal(0, (int)HeaderDropLogging.FirstPerName);
        Assert.Equal(1, (int)HeaderDropLogging.Every);
        Assert.Equal(2, (int)HeaderDropLogging.VerboseOnly);
    }

    [Fact]
    public void A_negative_resend_limit_throws_and_zero_is_accepted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemNetHttpClientOptions { ResendBufferLimit = -1 });
        Assert.Equal(0L, new SystemNetHttpClientOptions { ResendBufferLimit = 0 }.ResendBufferLimit);
    }

    [Fact]
    public void With_cannot_bypass_validation()
    {
        var options = new SystemNetHttpClientOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options with { Timeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => options with { ResendBufferLimit = -5 });
        Assert.Throws<ArgumentNullException>(() => options with { Logger = null! });
    }

    [Fact]
    public void Equality_is_by_value_over_all_six_members()
    {
        var proxy = new ProxyOptions { Host = "p", Port = 1 };
        var logger = new NullLogger<SystemNetHttpClientOptionsTests>();
        var a = new SystemNetHttpClientOptions { Logger = logger, Proxy = proxy, Timeout = TimeSpan.FromSeconds(5), HeaderDropLogging = HeaderDropLogging.Every, ResendBufferLimit = 10 };
        var b = new SystemNetHttpClientOptions { Logger = logger, Proxy = proxy, Timeout = TimeSpan.FromSeconds(5), HeaderDropLogging = HeaderDropLogging.Every, ResendBufferLimit = 10 };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { Timeout = null });
        Assert.NotEqual(a, a with { HeaderDropLogging = HeaderDropLogging.VerboseOnly });
        Assert.NotEqual(a, a with { ResendBufferLimit = 11 });
        Assert.NotEqual(a, a with { Proxy = null });
        Assert.NotEqual(a, a with { Logger = NullLogger.Instance });
        Assert.NotEqual(a, a with { TimeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider() });
    }

    [Fact]
    public void ToString_does_not_print_the_proxy_credentials()
    {
        var options = new SystemNetHttpClientOptions
        {
            Proxy = new ProxyOptions { Host = "p", Port = 1, UserName = "alice", Password = "s3cret" },
        };

        var text = options.ToString();

        Assert.DoesNotContain("alice", text, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", text, StringComparison.Ordinal);
        Assert.Contains("***", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_transport_snapshots_its_options_when_it_is_constructed()
    {
        var options = new SystemNetHttpClientOptions { Timeout = TimeSpan.FromSeconds(5), ResendBufferLimit = 7 };
        using var transport = new SystemNetHttpClient(options);

        var later = options with { Timeout = TimeSpan.FromSeconds(99), ResendBufferLimit = 9 };

        Assert.NotEqual(options, later);
        Assert.Equal(TimeSpan.FromSeconds(5), transport.Settings.Timeout);
        Assert.Equal(7L, transport.Settings.ResendBufferLimit);
    }

    [Fact]
    public void The_logger_constructors_carry_the_logger_and_otherwise_the_defaults()
    {
        var logger = new RecordingLogger();
        using var owned = new SystemNetHttpClient(logger);
        using var borrowed = new SystemNetHttpClient(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }), logger);

        Assert.Same(logger, owned.Settings.Logger);
        Assert.Same(logger, borrowed.Settings.Logger);
        Assert.Equal(TimeSpan.FromSeconds(100), owned.Settings.Timeout);
        Assert.Throws<ArgumentNullException>(() => new SystemNetHttpClient((ILogger)null!));
    }

    [Fact]
    public void An_unhonourable_proxy_configuration_is_warned_about_once_at_construction()
    {
        var logger = new RecordingLogger();
        var options = new SystemNetHttpClientOptions
        {
            Logger = logger,
            Proxy = new ProxyOptions { Type = ProxyType.Socks4, Host = "proxy.test", Port = 1080, UserName = "alice", Password = "s3cret" },
        };

        using var transport = new SystemNetHttpClient(options);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(DexpaceLogEvents.TransportProxyFeatureUnsupportedId, entry.EventId.Id);
        Assert.DoesNotContain("s3cret", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_owned_handler_goes_direct_unless_a_proxy_is_given_so_the_runtimes_default_is_never_consulted()
    {
        SocketsHttpHandler? seen = null;
        using var transport = new SystemNetHttpClient(handler => seen = handler, new SystemNetHttpClientOptions());

        Assert.NotNull(seen);
        Assert.False(seen.UseProxy);
        Assert.Null(seen.Proxy);
        Assert.False(seen.AllowAutoRedirect);
    }

    [Fact]
    public void The_owned_handler_installs_the_adapter_and_leaves_every_server_credential_unset()
    {
        SocketsHttpHandler? seen = null;
        var options = new SystemNetHttpClientOptions { Proxy = new ProxyOptions { Host = "proxy.test", Port = 3128, UserName = "alice", Password = "s3cret" } };
        using var transport = new SystemNetHttpClient(handler => seen = handler, options);

        Assert.NotNull(seen);
        Assert.True(seen.UseProxy);
        Assert.IsType<ProxyOptionsWebProxy>(seen.Proxy);
        Assert.Null(seen.Credentials);
        Assert.Null(seen.DefaultProxyCredentials);
        Assert.False(seen.PreAuthenticate);
        Assert.False(seen.AllowAutoRedirect);
    }

    [Fact]
    public void The_proxy_uri_given_to_the_runtime_has_no_userinfo()
    {
        SocketsHttpHandler? seen = null;
        var options = new SystemNetHttpClientOptions { Proxy = new ProxyOptions { Host = "proxy.test", Port = 3128, UserName = "alice", Password = "s3cret" } };
        using var transport = new SystemNetHttpClient(handler => seen = handler, options);

        var uri = seen!.Proxy!.GetProxy(new Uri("http://origin.test/"));

        Assert.Equal(string.Empty, uri!.UserInfo);
    }
}
