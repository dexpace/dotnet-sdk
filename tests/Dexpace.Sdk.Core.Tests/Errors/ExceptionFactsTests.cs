// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.TestSupport.Recovery;
using Xunit;

#pragma warning disable CA2201 // OutOfMemoryException is the fatal exception under test (design §5.2).

namespace Dexpace.Sdk.Core.Tests.Exceptions;

[Trait("Category", "Unit")]
public sealed class ExceptionFactsTests
{
    [Fact]
    public void IsFatal_is_true_for_OutOfMemoryException_and_its_subtypes()
    {
        Assert.True(ExceptionFacts.IsFatal(new OutOfMemoryException()));
        Assert.True(ExceptionFacts.IsFatal(new InsufficientMemoryException()));
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(TimeoutException))]
    public void IsFatal_is_false_for_other_exceptions_including_OperationCanceledException(Type type)
    {
        Assert.False(ExceptionFacts.IsFatal((Exception)Activator.CreateInstance(type)!));
    }

    [Fact]
    public void IsFatal_tests_the_exception_itself_not_its_chain()
    {
        Assert.False(ExceptionFacts.IsFatal(new IOException("wrapper", new OutOfMemoryException())));
    }

    [Fact]
    public void IsFatal_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionFacts.IsFatal(null!));
    }

    [Fact]
    public void EnumerateCauses_yields_the_exception_itself_first()
    {
        var exception = new InvalidOperationException();

        var causes = ExceptionFacts.EnumerateCauses(exception).ToList();

        Assert.Same(exception, Assert.Single(causes));
    }

    [Fact]
    public void EnumerateCauses_walks_inner_exceptions_breadth_first()
    {
        var c = new InvalidOperationException("c");
        var b = new IOException("b", c);
        var a = new TimeoutException("a", b);

        var causes = ExceptionFacts.EnumerateCauses(a).ToList();

        Assert.Equal(3, causes.Count);
        Assert.Same(a, causes[0]);
        Assert.Same(b, causes[1]);
        Assert.Same(c, causes[2]);
    }

    [Fact]
    public void EnumerateCauses_walks_aggregate_inner_exceptions()
    {
        var x = new InvalidOperationException("x");
        var y = new IOException("y");
        var z = new TimeoutException("z");
        var aggregate = new AggregateException(x, y, z);

        var causes = ExceptionFacts.EnumerateCauses(aggregate).ToList();

        Assert.Equal(4, causes.Count);
        Assert.Same(aggregate, causes[0]);
        Assert.Same(x, causes[1]);
        Assert.Same(y, causes[2]);
        Assert.Same(z, causes[3]);
    }

    [Fact]
    public void EnumerateCauses_yields_an_aggregates_first_inner_once()
    {
        var x = new InvalidOperationException("x");
        var aggregate = new AggregateException(x);
        Assert.Same(aggregate.InnerException, aggregate.InnerExceptions[0]);

        var causes = ExceptionFacts.EnumerateCauses(aggregate).ToList();

        Assert.Equal(2, causes.Count);
        Assert.Same(x, causes[1]);
    }

    [Fact]
    public void EnumerateCauses_terminates_on_a_self_cycle()
    {
        var exception = CyclicExceptions.SelfCycle();

        var causes = ExceptionFacts.EnumerateCauses(exception).ToList();

        Assert.Same(exception, Assert.Single(causes));
    }

    [Fact]
    public void EnumerateCauses_terminates_on_a_two_node_cycle()
    {
        var a = CyclicExceptions.TwoNodeCycle();
        var b = a.InnerException!;

        var causes = ExceptionFacts.EnumerateCauses(a).ToList();

        Assert.Equal(2, causes.Count);
        Assert.Same(a, causes[0]);
        Assert.Same(b, causes[1]);
    }

    [Fact]
    public void EnumerateCauses_uses_reference_identity()
    {
        var first = new StructurallyEqualException("same");
        var second = new StructurallyEqualException("same");
        var aggregate = new AggregateException(first, second);

        var causes = ExceptionFacts.EnumerateCauses(aggregate).ToList();

        Assert.Equal(3, causes.Count);
        Assert.Same(first, causes[1]);
        Assert.Same(second, causes[2]);
    }

    [Fact]
    public void EnumerateCauses_stops_below_depth_64()
    {
        Exception chain = new InvalidOperationException("leaf");
        for (var i = 0; i < 100; i++)
        {
            chain = new InvalidOperationException("n" + i, chain);
        }

        Assert.Equal(65, ExceptionFacts.EnumerateCauses(chain).Count());
    }

    [Fact]
    public void EnumerateCauses_rejects_null_at_the_call_and_is_lazy_afterwards()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionFacts.EnumerateCauses(null!));

        var enumerable = ExceptionFacts.EnumerateCauses(new InvalidOperationException());
        Assert.NotNull(enumerable);
    }
}
