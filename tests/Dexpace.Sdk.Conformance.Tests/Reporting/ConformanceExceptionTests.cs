// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Reporting;

/// <summary>P8a-6: a conformance failure is a test result, never an SDK failure a caller's catch could swallow.</summary>
[Trait("Category", "Unit")]
public sealed class ConformanceExceptionTests
{
    [Fact]
    public void A_conformance_failure_is_an_Exception_and_not_an_SdkException()
    {
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(ConformanceException)));
        Assert.False(typeof(SdkException).IsAssignableFrom(typeof(ConformanceException)));
        Assert.False(typeof(SdkException).IsAssignableFrom(typeof(ConformanceVacuousException)));
    }

    [Fact]
    public void The_expected_and_actual_constructor_sets_both_and_leaves_the_message_one_line()
    {
        var error = new ConformanceException("TRANSPORT-24: status", "520", "502");

        Assert.Equal("TRANSPORT-24: status", error.Message);
        Assert.Equal("520", error.Expected);
        Assert.Equal("502", error.Actual);
        Assert.DoesNotContain("520", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_plain_constructors_leave_expected_and_actual_null()
    {
        var cause = new InvalidOperationException("inner");

        Assert.Null(new ConformanceException().Expected);
        Assert.Null(new ConformanceException("m").Actual);
        var wrapped = new ConformanceException("m", cause);
        Assert.Same(cause, wrapped.InnerException);
        Assert.Null(wrapped.Expected);
        Assert.Null(wrapped.Actual);
    }

    [Fact]
    public void A_vacuous_signal_carries_its_reason_as_the_message()
    {
        var cause = new InvalidOperationException("inner");

        Assert.Equal("no antecedent", new ConformanceVacuousException("no antecedent").Message);
        Assert.Same(cause, new ConformanceVacuousException("r", cause).InnerException);
        Assert.NotNull(new ConformanceVacuousException().Message);
    }
}
