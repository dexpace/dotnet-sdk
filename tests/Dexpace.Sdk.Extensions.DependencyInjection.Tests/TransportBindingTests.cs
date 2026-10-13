// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Http.SystemNet;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>P9-8, P9-13: the <c>Transport:*</c> keys of <c>SystemNetHttpClientOptions</c> and the primary handler's pooled-connection lifetime.</summary>
[Trait("Category", "Unit")]
public sealed class TransportBindingTests
{
    [Fact]
    public void Timeout_binds_a_duration_and_none_means_no_transport_default()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), Config.Read(("Transport:Timeout", "PT10S")).Transport.Timeout);
        Assert.Null(Config.Read(("Transport:Timeout", "none")).Transport.Timeout);
        Assert.Equal(new SystemNetHttpClientOptions().Timeout, Config.Read().Transport.Timeout);
    }

    [Fact]
    public void An_out_of_range_timeout_fails_through_the_record()
    {
        Assert.Contains("Transport:Timeout", Assert.Single(Config.Failures(Config.Read(("Transport:Timeout", "60d")))), StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderDropLogging_is_read_by_name_and_a_number_fails()
    {
        Assert.Equal(HeaderDropLogging.Every, Config.Read(("Transport:HeaderDropLogging", "every")).Transport.HeaderDropLogging);
        Assert.Single(Config.Failures(Config.Read(("Transport:HeaderDropLogging", "1"))));
    }

    [Fact]
    public void ResendBufferLimit_binds_a_long_and_a_negative_fails_through_the_record()
    {
        Assert.Equal(5_000_000_000L, Config.Read(("Transport:ResendBufferLimit", "5000000000")).Transport.ResendBufferLimit);
        Assert.Contains("Transport:ResendBufferLimit", Assert.Single(Config.Failures(Config.Read(("Transport:ResendBufferLimit", "-1")))), StringComparison.Ordinal);
    }

    [Fact]
    public void PooledConnectionLifetime_defaults_to_two_minutes_binds_a_duration_and_none_is_infinite()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), Config.Read().PooledConnectionLifetime);
        Assert.Equal(TimeSpan.FromSeconds(10), Config.Read(("Transport:PooledConnectionLifetime", "10s")).PooledConnectionLifetime);
        Assert.Equal(Timeout.InfiniteTimeSpan, Config.Read(("Transport:PooledConnectionLifetime", "none")).PooledConnectionLifetime);
        Assert.Single(Config.Failures(Config.Read(("Transport:PooledConnectionLifetime", "0s"))));
    }

    [Fact]
    public void Logger_and_clock_are_never_read_from_configuration()
    {
        Assert.Contains("unknown key", Assert.Single(Config.Failures(Config.Read(("Transport:Logger", "x")))), StringComparison.Ordinal);
        Assert.Contains("unknown key", Assert.Single(Config.Failures(Config.Read(("Transport:TimeProvider", "x")))), StringComparison.Ordinal);
    }
}
