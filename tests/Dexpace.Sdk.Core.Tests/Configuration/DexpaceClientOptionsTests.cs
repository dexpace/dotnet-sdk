// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

[Trait("Category", "Unit")]
public class DexpaceClientOptionsTests
{
    [Fact]
    public void DexpaceClientOptions_Defaults_AreCorrect()
    {
        var opts = new DexpaceClientOptions();

        Assert.Null(opts.BaseAddress);
        Assert.NotNull(opts.UserAgent);
        Assert.StartsWith("dexpace-dotnet/", opts.UserAgent);
        Assert.Null(opts.OverallTimeout);
        Assert.Null(opts.AttemptTimeout);
        Assert.NotNull(opts.Retry);
        Assert.NotNull(opts.Redirect);
    }

    [Fact]
    public void Logging_defaults_to_HttpLoggingOptions_Default()
    {
        var opts = new DexpaceClientOptions();

        Assert.Same(HttpLoggingOptions.Default, opts.Logging);
        Assert.Equal(HttpLogLevel.None, opts.Logging.Level);
        Assert.Throws<ArgumentNullException>(() => opts with { Logging = null! });
        Assert.Throws<ArgumentNullException>(() => new DexpaceClientOptions { Logging = null! });
    }

    [Fact]
    public void The_default_user_agent_is_the_identity_tokens_joined()
    {
        var userAgent = new DexpaceClientOptions().UserAgent;

        Assert.Equal(string.Join(' ', BuildInfo.IdentityTokens), userAgent);
        Assert.Contains(" dotnet/", userAgent, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0.0", userAgent, StringComparison.Ordinal);
    }

    [Fact]
    public void RetryOptions_Defaults_AreCorrect()
    {
        var retry = new RetryOptions();

        Assert.Equal(2, retry.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), retry.BaseDelay);
        Assert.Equal(2.0, retry.Multiplier);
        Assert.Equal(TimeSpan.FromSeconds(8), retry.MaxDelay);
        Assert.Equal(0.2, retry.Jitter);
        Assert.Null(retry.FixedDelay);
        Assert.True(retry.HonorRetryAfter);
        Assert.Null(retry.AttemptHeaderName);
    }

    [Fact]
    public void RedirectOptions_Defaults_AreCorrect()
    {
        var redirect = new RedirectOptions();

        Assert.Equal(20, redirect.MaxRedirects);
        Assert.False(redirect.AllowHttpsToHttpDowngrade);
        Assert.True(redirect.StripSensitiveHeadersOnCrossOrigin);
    }

    [Fact]
    public void DexpaceClientOptions_RetryAndRedirect_AreNonNullOnFreshInstance()
    {
        var opts = new DexpaceClientOptions();

        // Property bag objects must be initialized — not null — so callers can derive with
        // opts with { Retry = opts.Retry with { MaxRetryAttempts = 5 } } without a null ref.
        Assert.NotNull(opts.Retry);
        Assert.NotNull(opts.Redirect);
    }

    [Fact]
    public void BaseAddress_is_read_by_BuildRequest()
    {
        // SEAM-27: BaseAddress is no longer inert; OperationDescriptor.BuildRequest(DexpaceClientOptions) reads it.
        var options = new DexpaceClientOptions { BaseAddress = new Uri("https://api.example.com/v1") };
        var descriptor = new Dexpace.Sdk.Core.Operations.OperationDescriptor { Method = Dexpace.Sdk.Core.Http.Common.Method.Get, PathTemplate = "/pets" };

        Assert.Equal("https://api.example.com/v1/pets", descriptor.BuildRequest(options).Url.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://h")]
    [InlineData("https://h/v1")]
    [InlineData("https://h:8443/a/b?x=1")]
    public void BaseAddress_accepts_an_absolute_http_or_https_uri_without_a_fragment(string address)
    {
        var options = new DexpaceClientOptions { BaseAddress = new Uri(address) };

        Assert.Equal(new Uri(address), options.BaseAddress);
        Assert.Null(new DexpaceClientOptions { BaseAddress = null }.BaseAddress);
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("https://host/c#frag")]
    [InlineData("https://host/#")]
    [InlineData("ftp://host/")]
    [InlineData("https://host/c?sig=secret#f")]
    public void BaseAddress_rejects_what_BuildRequest_used_to_reject(string address)
    {
        var uri = new Uri(address, UriKind.RelativeOrAbsolute);

        var error = Assert.Throws<ArgumentException>(() => new DexpaceClientOptions { BaseAddress = uri });

        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_renders_the_members_and_redacts_the_base_address()
    {
        var options = new DexpaceClientOptions { BaseAddress = new Uri("https://h/v1?sig=abc123") };

        var text = options.ToString();

        Assert.DoesNotContain("abc123", text, StringComparison.Ordinal);
        Assert.Contains("BaseAddress", text, StringComparison.Ordinal);
        Assert.Contains("UserAgent", text, StringComparison.Ordinal);
        Assert.Contains("Retry", text, StringComparison.Ordinal);
        Assert.Contains("Redirect", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_of_the_nested_records_renders_every_member()
    {
        var retry = new RetryOptions().ToString();
        var redirect = new RedirectOptions().ToString();

        foreach (var name in new[] { "MaxRetryAttempts", "BaseDelay", "Multiplier", "MaxDelay", "Jitter", "FixedDelay", "HonorRetryAfter", "RetryableStatusCodes", "AttemptHeaderName" })
        {
            Assert.Contains(name, retry, StringComparison.Ordinal);
        }

        foreach (var name in new[] { "MaxRedirects", "AllowHttpsToHttpDowngrade", "StripSensitiveHeadersOnCrossOrigin" })
        {
            Assert.Contains(name, redirect, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OverallTimeout_and_AttemptTimeout_accept_null_and_values_in_0_to_49_days()
    {
        Assert.Null(new DexpaceClientOptions { OverallTimeout = null, AttemptTimeout = null }.OverallTimeout);
        var edge = new DexpaceClientOptions { OverallTimeout = TimeSpan.FromDays(49), AttemptTimeout = TimeSpan.FromTicks(1) };

        Assert.Equal(TimeSpan.FromDays(49), edge.OverallTimeout);
        Assert.Equal(TimeSpan.FromTicks(1), edge.AttemptTimeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OverallTimeout_and_AttemptTimeout_reject_zero_and_negative(long ticks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DexpaceClientOptions { OverallTimeout = TimeSpan.FromTicks(ticks) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new DexpaceClientOptions { AttemptTimeout = TimeSpan.FromTicks(ticks) });
    }

    [Fact]
    public void OverallTimeout_and_AttemptTimeout_reject_InfiniteTimeSpan_and_above_the_limit()
    {
        var tooLong = TimeSpan.FromDays(49) + TimeSpan.FromTicks(1);
        var timerCeiling = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

        foreach (var value in new[] { Timeout.InfiniteTimeSpan, tooLong, timerCeiling, TimeSpan.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DexpaceClientOptions { OverallTimeout = value });
            Assert.Throws<ArgumentOutOfRangeException>(() => new DexpaceClientOptions { AttemptTimeout = value });
        }
    }
}
