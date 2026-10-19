// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Subjects;

[Trait("Category", "Unit")]
public sealed class SuiteOptionsTests
{
    [Fact]
    public void The_defaults_are_thirty_seconds_and_no_waivers()
    {
        var options = new SuiteOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), options.AssertionTimeout);
        Assert.Empty(options.Waivers);
    }

    [Fact]
    public void A_non_positive_assertion_timeout_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SuiteOptions { AssertionTimeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SuiteOptions { AssertionTimeout = TimeSpan.FromSeconds(-1) });
    }

    [Fact]
    public void The_waivers_are_copied_and_null_is_rejected()
    {
        var waivers = new List<ConformanceWaiver> { new("XCUT-1", "r") };
        var options = new SuiteOptions { Waivers = waivers };

        waivers.Clear();

        Assert.Single(options.Waivers);
        Assert.Throws<ArgumentNullException>(() => new SuiteOptions { Waivers = null! });
    }
}
