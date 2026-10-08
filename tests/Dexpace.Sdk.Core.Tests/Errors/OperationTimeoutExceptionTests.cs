// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>XCUT-1: the type an expired overall deadline throws (P6a-24).</summary>
[Trait("Category", "Unit")]
public sealed class OperationTimeoutExceptionTests
{
    [Fact]
    public void The_three_standard_constructors_work()
    {
        var inner = new OperationCanceledException();

        Assert.NotNull(new OperationTimeoutException().Message);
        Assert.Equal("m", new OperationTimeoutException("m").Message);
        var both = new OperationTimeoutException("m", inner);
        Assert.Same(inner, both.InnerException);
    }

    [Fact]
    public void It_is_an_SdkException_that_is_not_retryable()
    {
        var exception = new OperationTimeoutException("m");

        Assert.IsAssignableFrom<SdkException>(exception);
        Assert.False(exception.IsRetryable);
        Assert.True(typeof(OperationTimeoutException).IsSealed);
    }
}
