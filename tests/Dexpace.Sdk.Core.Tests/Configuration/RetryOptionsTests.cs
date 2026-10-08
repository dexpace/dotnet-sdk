// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Resilience;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

/// <summary>RetryOptions members, defaults, validation and equality (RETRY-12, RETRY-41, RETRY-43, RECOV-34).</summary>
[Trait("Category", "Unit")]
public sealed class RetryOptionsTests
{
    private static readonly TimeSpan s_ceiling = TimeSpan.FromTicks(long.MaxValue / 100);

    [Fact]
    public void Defaults_are_200ms_2_8s_0_2_and_two_retries()
    {
        var options = new RetryOptions();

        Assert.Equal(2, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.BaseDelay);
        Assert.Equal(2.0, options.Multiplier);
        Assert.Equal(TimeSpan.FromSeconds(8), options.MaxDelay);
        Assert.Equal(0.2, options.Jitter);
        Assert.Null(options.FixedDelay);
        Assert.True(options.HonorRetryAfter);
        Assert.Null(options.AttemptHeaderName);
        Assert.True(options.RetryableStatusCodes.SetEquals(RetryFacts.DefaultRetryableStatusCodes));
    }

    [Fact]
    public void MaxRetryAttempts_rejects_a_negative_and_accepts_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { MaxRetryAttempts = -1 });
        Assert.Equal(0, new RetryOptions { MaxRetryAttempts = 0 }.MaxRetryAttempts);
    }

    [Fact]
    public void Durations_reject_negative_and_above_the_292_year_ceiling_and_accept_the_boundary()
    {
        Assert.Equal(s_ceiling, new RetryOptions { BaseDelay = s_ceiling }.BaseDelay);
        Assert.Equal(s_ceiling, new RetryOptions { MaxDelay = s_ceiling }.MaxDelay);
        Assert.Equal(s_ceiling, new RetryOptions { FixedDelay = s_ceiling }.FixedDelay);
        Assert.Equal(TimeSpan.Zero, new RetryOptions { FixedDelay = TimeSpan.Zero }.FixedDelay);

        var tooBig = s_ceiling + TimeSpan.FromTicks(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { BaseDelay = tooBig });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { MaxDelay = tooBig });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { FixedDelay = tooBig });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { BaseDelay = TimeSpan.MaxValue });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { BaseDelay = TimeSpan.FromTicks(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { MaxDelay = Timeout.InfiniteTimeSpan });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { FixedDelay = TimeSpan.FromTicks(-1) });
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Multiplier_must_be_finite_and_at_least_one_rejects(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { Multiplier = value });
    }

    [Fact]
    public void Multiplier_one_is_accepted()
    {
        Assert.Equal(1.0, new RetryOptions { Multiplier = 1.0 }.Multiplier);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Jitter_must_lie_in_0_to_1_rejects(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { Jitter = value });
    }

    [Fact]
    public void Jitter_bounds_are_accepted()
    {
        Assert.Equal(0.0, new RetryOptions { Jitter = 0 }.Jitter);
        Assert.Equal(1.0, new RetryOptions { Jitter = 1 }.Jitter);
    }

    [Fact]
    public void RetryableStatusCodes_rejects_null_and_a_status_outside_400_to_599()
    {
        Assert.Throws<ArgumentNullException>(() => new RetryOptions { RetryableStatusCodes = null! });
        foreach (var bad in new[] { 399, 600, 0, -1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { RetryableStatusCodes = new HashSet<int> { bad } });
        }

        var edges = new RetryOptions { RetryableStatusCodes = new HashSet<int> { 400, 599 } };
        Assert.True(edges.RetryableStatusCodes.SetEquals([400, 599]));
    }

    [Fact]
    public void RetryableStatusCodes_is_copied_so_later_mutation_of_the_source_changes_nothing()
    {
        var source = new HashSet<int> { 503 };
        var options = new RetryOptions { RetryableStatusCodes = source };

        source.Add(500);

        Assert.True(options.RetryableStatusCodes.SetEquals([503]));
    }

    [Fact]
    public void AttemptHeaderName_accepts_null_and_a_token_and_rejects_a_space_or_CRLF()
    {
        Assert.Null(new RetryOptions { AttemptHeaderName = null }.AttemptHeaderName);
        Assert.Equal("X-Retry-Count", new RetryOptions { AttemptHeaderName = "X-Retry-Count" }.AttemptHeaderName);
        Assert.Throws<ArgumentException>(() => new RetryOptions { AttemptHeaderName = "X Retry" });
        Assert.Throws<ArgumentException>(() => new RetryOptions { AttemptHeaderName = "X-Retry\r\nEvil: 1" });
        Assert.Throws<ArgumentException>(() => new RetryOptions { AttemptHeaderName = string.Empty });
    }

    [Fact]
    public void Equality_compares_the_status_set_by_content_and_hashes_alike()
    {
        var a = new RetryOptions { RetryableStatusCodes = new List<int> { 503, 500, 429 }.ToHashSet() };
        var b = new RetryOptions { RetryableStatusCodes = new List<int> { 429, 503, 500 }.ToHashSet() };
        var c = new RetryOptions { RetryableStatusCodes = new HashSet<int> { 429, 503 } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.Equal(new RetryOptions(), new RetryOptions());
        Assert.NotEqual(new RetryOptions(), new RetryOptions { Jitter = 0.3 });
    }

    [Fact]
    public void ToString_renders_the_status_set_in_ascending_order()
    {
        var text = new RetryOptions { RetryableStatusCodes = new List<int> { 503, 429, 500 }.ToHashSet() }.ToString();

        Assert.Contains("RetryableStatusCodes = [429, 500, 503]", text, StringComparison.Ordinal);
        foreach (var name in new[] { "MaxRetryAttempts", "BaseDelay", "Multiplier", "MaxDelay", "Jitter", "FixedDelay", "HonorRetryAfter", "AttemptHeaderName" })
        {
            Assert.Contains(name, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RetryNonIdempotentWhenReplayable_no_longer_exists()
    {
        Assert.Null(typeof(RetryOptions).GetProperty("RetryNonIdempotentWhenReplayable", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void With_derives_a_copy_and_validates_the_changed_member()
    {
        var options = new RetryOptions();

        Assert.Equal(0.5, (options with { Jitter = 0.5 }).Jitter);
        Assert.Throws<ArgumentOutOfRangeException>(() => options with { Jitter = 2 });
    }
}
