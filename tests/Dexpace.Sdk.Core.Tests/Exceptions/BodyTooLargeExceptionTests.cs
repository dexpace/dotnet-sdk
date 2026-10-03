// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>The public failure of a refused materialisation (IO-9, P3a-12, styleguide 8.6).</summary>
/// <remarks>The namespace is <c>…Tests.Exceptions</c> because <c>…Tests.Errors</c> would shadow the type under test.</remarks>
[Trait("Category", "Unit")]
public class BodyTooLargeExceptionTests
{
    [Fact]
    public void BodyTooLargeException_is_sealed_and_derives_from_StreamingException()
    {
        Assert.True(typeof(BodyTooLargeException).IsSealed);
        Assert.Equal(typeof(StreamingException), typeof(BodyTooLargeException).BaseType);
    }

    [Fact]
    public void Its_constructors_are_the_three_standard_ones()
    {
        var inner = new InvalidOperationException("cause");

        Assert.NotNull(new BodyTooLargeException().Message);
        Assert.Equal("m", new BodyTooLargeException("m").Message);
        var chained = new BodyTooLargeException("m", inner);
        Assert.Equal("m", chained.Message);
        Assert.Same(inner, chained.InnerException);
        Assert.Equal(3, typeof(BodyTooLargeException).GetConstructors().Length);
    }

    [Fact]
    public void It_is_distinct_from_StreamConsumedException()
    {
        Assert.False(typeof(StreamConsumedException).IsAssignableFrom(typeof(BodyTooLargeException)));
        Assert.False(typeof(BodyTooLargeException).IsAssignableFrom(typeof(StreamConsumedException)));
    }

    [Fact]
    public void The_chained_cause_survives()
    {
        var inner = new IOException("io");

        var error = new BodyTooLargeException("m", inner);

        Assert.Same(inner, error.InnerException);
    }
}
