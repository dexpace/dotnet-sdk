// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Reporting;

/// <summary>Design section C: the worst-of order is Errored, Failed, Waived, NotExercised, Vacuous, Passed.</summary>
[Trait("Category", "Unit")]
public sealed class StatusSeverityTests
{
    private static readonly ConformanceStatus[] s_worstFirst =
    [
        ConformanceStatus.Errored,
        ConformanceStatus.Failed,
        ConformanceStatus.Waived,
        ConformanceStatus.NotExercised,
        ConformanceStatus.Vacuous,
        ConformanceStatus.Passed,
    ];

    public static TheoryData<ConformanceStatus, ConformanceStatus> OrderedPairs()
    {
        var data = new TheoryData<ConformanceStatus, ConformanceStatus>();
        foreach (var left in Enum.GetValues<ConformanceStatus>())
        {
            foreach (var right in Enum.GetValues<ConformanceStatus>())
            {
                data.Add(left, right);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(OrderedPairs))]
    public void The_order_is_total_and_matches_the_design(ConformanceStatus left, ConformanceStatus right)
    {
        var expected = Array.IndexOf(s_worstFirst, right).CompareTo(Array.IndexOf(s_worstFirst, left));

        Assert.Equal(expected, StatusSeverity.Rank(left).CompareTo(StatusSeverity.Rank(right)));
    }

    [Fact]
    public void Worst_picks_the_most_severe_status()
    {
        Assert.Equal(ConformanceStatus.Failed, StatusSeverity.Worst([ConformanceStatus.Passed, ConformanceStatus.Failed, ConformanceStatus.Vacuous]));
        Assert.Equal(ConformanceStatus.Errored, StatusSeverity.Worst([ConformanceStatus.Waived, ConformanceStatus.Errored, ConformanceStatus.Failed]));
        Assert.Equal(ConformanceStatus.Passed, StatusSeverity.Worst([ConformanceStatus.Passed]));
    }

    [Fact]
    public void Worst_of_nothing_throws()
    {
        Assert.Throws<InvalidOperationException>(() => StatusSeverity.Worst([]));
    }
}
