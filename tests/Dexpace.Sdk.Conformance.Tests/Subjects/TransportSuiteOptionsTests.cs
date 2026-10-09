// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Subjects;

[Trait("Category", "Unit")]
public sealed class TransportSuiteOptionsTests
{
    [Fact]
    public void Defaults_are_no_waivers_thirty_seconds_and_ten_seconds()
    {
        var options = new TransportSuiteOptions();

        Assert.Empty(options.Waivers);
        Assert.Equal(TimeSpan.FromSeconds(30), options.AssertionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), options.ReleaseTimeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_timeout_is_rejected_at_init(int milliseconds)
    {
        var bad = TimeSpan.FromMilliseconds(milliseconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TransportSuiteOptions { AssertionTimeout = bad });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransportSuiteOptions { ReleaseTimeout = bad });
    }

    [Fact]
    public void Waivers_are_copied_and_never_null()
    {
        var list = new List<ConformanceWaiver> { new("TRANSPORT-8", "reason") };
        var options = new TransportSuiteOptions { Waivers = list };

        list.Clear();

        Assert.Single(options.Waivers);
        Assert.Throws<ArgumentNullException>(() => new TransportSuiteOptions { Waivers = null! });
    }
}
