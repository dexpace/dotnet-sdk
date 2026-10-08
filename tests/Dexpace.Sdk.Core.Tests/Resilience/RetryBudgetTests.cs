// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Resilience;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Resilience;

/// <summary>The recovery stack's total-time budget (RETRY-27, RETRY-28, CFG-16).</summary>
[Trait("Category", "Unit")]
public sealed class RetryBudgetTests
{
    private static readonly TimeSpan s_total = TimeSpan.FromSeconds(10);

    [Fact]
    public void Unbounded_never_expires()
    {
        var clock = new FakeTimeProvider();
        var start = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromDays(3650));

        var budget = RetryBudget.Unbounded;

        Assert.True(budget.IsUnbounded);
        Assert.False(budget.IsSpent(start));
        Assert.True(budget.Allows(start, TimeSpan.FromDays(365)));
        Assert.Equal(TimeSpan.FromDays(1), budget.Clamp(start, TimeSpan.FromDays(1)));
    }

    [Fact]
    public void A_zero_total_timeout_disables_the_budget()
    {
        var clock = new FakeTimeProvider();

        Assert.True(RetryBudget.For(TimeSpan.Zero, clock).IsUnbounded);
        Assert.False(RetryBudget.For(s_total, clock).IsUnbounded);
    }

    [Fact]
    public void Aborts_when_elapsed_reaches_the_budget()
    {
        var clock = new FakeTimeProvider();
        var start = clock.GetTimestamp();
        var budget = RetryBudget.For(s_total, clock);

        clock.Advance(TimeSpan.FromSeconds(9.999));
        Assert.False(budget.IsSpent(start));

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(budget.IsSpent(start));
    }

    [Fact]
    public void Aborts_when_elapsed_plus_delay_exceeds_the_budget()
    {
        var clock = new FakeTimeProvider();
        var start = clock.GetTimestamp();
        var budget = RetryBudget.For(s_total, clock);
        clock.Advance(TimeSpan.FromSeconds(4));

        Assert.True(budget.Allows(start, TimeSpan.FromSeconds(6)));
        Assert.False(budget.Allows(start, TimeSpan.FromSeconds(6) + TimeSpan.FromTicks(1)));
    }

    [Fact]
    public void Clamps_the_delay_to_the_remainder()
    {
        var clock = new FakeTimeProvider();
        var start = clock.GetTimestamp();
        var budget = RetryBudget.For(s_total, clock);
        clock.Advance(TimeSpan.FromSeconds(7));

        // The clock moved on between the fit check and the wait: the wait narrows to what remains.
        Assert.Equal(TimeSpan.FromSeconds(3), budget.Clamp(start, TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.FromSeconds(1), budget.Clamp(start, TimeSpan.FromSeconds(1)));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.Zero, budget.Clamp(start, TimeSpan.FromSeconds(1)));
    }
}
