// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.TestSupport.Recovery;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

[Trait("Category", "Unit")]
public sealed class RecoveryFixtureTests
{
    [Fact]
    public void SelfCycle_points_at_itself()
    {
        var exception = CyclicExceptions.SelfCycle();

        Assert.Same(exception, exception.InnerException);
    }

    [Fact]
    public void TwoNodeCycle_closes()
    {
        var a = CyclicExceptions.TwoNodeCycle();
        var b = a.InnerException;

        Assert.NotNull(b);
        Assert.NotSame(a, b);
        Assert.Same(a, b.InnerException);
    }

    [Fact]
    public void StructurallyEqualException_equals_a_distinct_instance_with_the_same_message()
    {
        var a = new StructurallyEqualException("same");
        var b = new StructurallyEqualException("same");

        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ReadOnlyDataException_throws_NotSupportedException_on_a_data_write()
    {
        var exception = new ReadOnlyDataException();

        Assert.Throws<NotSupportedException>(() => exception.Data["k"] = "v");
    }
}
