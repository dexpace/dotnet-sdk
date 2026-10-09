// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Serialization;

/// <summary>SERDE-14 / SERDE-30 (design position A, P7a-7): the two-state marker behind <c>Tristate.Absent</c> and <c>Tristate.Null</c>.</summary>
[Trait("Category", "Unit")]
public sealed class TristateSentinelTests
{
    [Fact]
    public void TristateState_has_the_stable_values()
    {
        // A numeric pin (styleguide 6.8): default(TristateState) must stay Absent so default(Tristate<T>) is Absent.
        Assert.Equal(0, (int)TristateState.Absent);
        Assert.Equal(1, (int)TristateState.Null);
        Assert.Equal(2, (int)TristateState.Present);
    }

    [Fact]
    public void ToString_is_Absent_and_Null()
    {
        Assert.Equal("Absent", TristateSentinel.Absent.ToString());
        Assert.Equal("Null", TristateSentinel.Null.ToString());
    }

    [Fact]
    public void Equality_is_by_kind()
    {
        Assert.True(TristateSentinel.Absent == TristateSentinel.Absent);
        Assert.True(TristateSentinel.Null == TristateSentinel.Null);
        Assert.True(TristateSentinel.Absent != TristateSentinel.Null);
        Assert.Equal(TristateSentinel.Absent.GetHashCode(), TristateSentinel.Absent.GetHashCode());
        Assert.Equal(TristateSentinel.Null.GetHashCode(), TristateSentinel.Null.GetHashCode());
        Assert.False(TristateSentinel.Absent.Equals(TristateSentinel.Null));
        Assert.False(TristateSentinel.Absent.Equals((object)TristateSentinel.Null));
        Assert.True(TristateSentinel.Null.Equals((object)TristateSentinel.Null));
        Assert.False(TristateSentinel.Null.Equals("Null"));
    }

    [Fact]
    public void The_sentinel_default_is_Absent()
    {
        Assert.Equal("Absent", default(TristateSentinel).ToString());
        Assert.True(default(TristateSentinel) == TristateSentinel.Absent);
    }

    [Fact]
    public void A_Present_sentinel_cannot_be_built()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TristateSentinel(TristateState.Present));
    }
}
