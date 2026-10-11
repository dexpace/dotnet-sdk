// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
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
}
