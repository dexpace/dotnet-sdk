// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Resilience;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Resilience;

/// <summary>The backoff calculator (RETRY-9 to RETRY-11, RETRY-13, RETRY-43, RECOV-21, RECOV-26).</summary>
[Trait("Category", "Unit")]
public sealed class RetryBackoffTests
{
    private static readonly TimeSpan s_year = TimeSpan.FromDays(365);

    public sealed record BackoffCase(
        string Name,
        int Attempt,
        double BaseMs,
        double Multiplier,
        double MaxMs,
        double Jitter,
        double? FixedMs,
        double U,
        long? ExpectedTicks,
        bool Throws);

    private static RetryOptions Options(BackoffCase c) => new()
    {
        BaseDelay = TimeSpan.FromMilliseconds(c.BaseMs),
        Multiplier = c.Multiplier,
        MaxDelay = TimeSpan.FromMilliseconds(c.MaxMs),
        Jitter = c.Jitter,
        FixedDelay = c.FixedMs is { } f ? TimeSpan.FromMilliseconds(f) : null,
    };

    private static RetryOptions Opts(double baseMs = 200, double mult = 2, double maxMs = 8000, double jitter = 0) => new()
    {
        BaseDelay = TimeSpan.FromMilliseconds(baseMs),
        Multiplier = mult,
        MaxDelay = TimeSpan.FromMilliseconds(maxMs),
        Jitter = jitter,
    };

    [Fact]
    public void Matches_every_vector()
    {
        var cases = VectorFile.Load<BackoffCase>("retry/backoff.json");
        Assert.NotEmpty(cases);

        foreach (var c in cases)
        {
            if (c.Throws)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => RetryBackoff.Compute(c.Attempt, Options(c), () => c.U));
                continue;
            }

            var actual = RetryBackoff.Compute(c.Attempt, Options(c), () => c.U);
            Assert.True(c.ExpectedTicks == actual.Ticks, $"{c.Name}: expected {c.ExpectedTicks} ticks, got {actual.Ticks}.");
        }
    }

    [Fact]
    public void Jitter_is_symmetric_over_d_minus_w_over_2_to_d_plus_w_over_2()
    {
        var options = Opts(jitter: 0.2);

        Assert.Equal(TimeSpan.FromMilliseconds(720), RetryBackoff.Compute(3, options, () => 0.0));
        Assert.Equal(TimeSpan.FromMilliseconds(800), RetryBackoff.Compute(3, options, () => 0.5));
        Assert.Equal(TimeSpan.FromMilliseconds(880), RetryBackoff.Compute(3, options, () => 1.0));
    }

    [Fact]
    public void Zero_jitter_returns_the_unjittered_delay_and_a_sub_tick_width_returns_the_base()
    {
        var calls = 0;
        double Random()
        {
            calls++;
            return 0.0;
        }

        Assert.Equal(TimeSpan.FromMilliseconds(800), RetryBackoff.Compute(3, Opts(), Random));
        Assert.Equal(0, calls);

        // 5 ticks at jitter 0.1 is a width of 0.5 tick: below the resolution, so the base is returned (P6a-14).
        var tiny = new RetryOptions { BaseDelay = TimeSpan.FromTicks(5), MaxDelay = TimeSpan.FromTicks(5), Multiplier = 1, Jitter = 0.1 };
        Assert.Equal(TimeSpan.FromTicks(5), RetryBackoff.Compute(1, tiny, Random));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(-5.0)]
    [InlineData(1e18)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_negative_sample_floors_to_zero_and_a_hostile_random_never_throws(double sample)
    {
        var options = Opts(jitter: 1.0);

        var delay = RetryBackoff.Compute(3, options, () => sample);

        Assert.InRange(delay, TimeSpan.Zero, s_year);
    }

    [Fact]
    public void A_negative_sample_is_exactly_zero()
    {
        Assert.Equal(TimeSpan.Zero, RetryBackoff.Compute(1, Opts(baseMs: 10, mult: 1, maxMs: 10, jitter: 1), () => -100));
    }

    [Fact]
    public void Saturates_and_never_overflows()
    {
        var day = TimeSpan.FromDays(1);
        var huge = new RetryOptions { BaseDelay = day, Multiplier = 1e6, MaxDelay = TimeSpan.FromDays(400), Jitter = 0 };

        Assert.Equal(s_year, RetryBackoff.Compute(1000, huge, () => 0.5));
        Assert.Equal(TimeSpan.FromDays(365), RetryBackoff.Compute(1, new RetryOptions { BaseDelay = TimeSpan.FromDays(400), MaxDelay = TimeSpan.FromDays(400), Jitter = 0 }, () => 0.5));
        Assert.Equal(TimeSpan.FromMilliseconds(8000), RetryBackoff.Compute(5000, Opts(), () => 0.5));
        Assert.Equal(TimeSpan.Zero, RetryBackoff.Compute(1100, Opts(baseMs: 0), () => 0.5));
    }

    [Fact]
    public void Attempt_below_one_is_a_programmer_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetryBackoff.Compute(0, Opts(), () => 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetryBackoff.Compute(-1, Opts(), () => 0.5));
    }

    [Fact]
    public void Every_result_is_clamped_to_365_days()
    {
        var wide = new RetryOptions { BaseDelay = TimeSpan.FromDays(400), MaxDelay = TimeSpan.FromDays(400), Jitter = 1 };
        var fixedWide = new RetryOptions { FixedDelay = TimeSpan.FromDays(900) };

        Assert.Equal(s_year, RetryBackoff.Compute(1, wide, () => 1.0));
        Assert.Equal(s_year, RetryBackoff.Compute(1, fixedWide, () => 0.5));
        Assert.Equal(s_year, RetryBackoff.ClampToCeiling(TimeSpan.FromDays(900)));
        Assert.Equal(TimeSpan.Zero, RetryBackoff.ClampToCeiling(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Random_source_is_called_at_most_once_per_computation()
    {
        var calls = 0;
        _ = RetryBackoff.Compute(2, Opts(jitter: 0.2), () =>
        {
            calls++;
            return 0.5;
        });

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Every_sample_in_the_unit_interval_stays_inside_the_symmetric_window()
    {
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            foreach (var jitter in new[] { 0.0, 0.1, 0.5, 1.0 })
            {
                foreach (var u in new[] { 0.0, 0.25, 0.5, 0.999 })
                {
                    var baseMs = Math.Min(200 * Math.Pow(2, attempt - 1), 8000);
                    var delay = RetryBackoff.Compute(attempt, Opts(jitter: jitter), () => u).TotalMilliseconds;
                    Assert.InRange(delay, (baseMs * (1 - (jitter / 2))) - 0.001, (baseMs * (1 + (jitter / 2))) + 0.001);
                }
            }
        }
    }
}
