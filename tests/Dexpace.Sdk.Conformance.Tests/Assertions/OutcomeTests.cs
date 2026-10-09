// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Assertions;

/// <summary>The two helpers <c>transport-3</c> and <c>transport-7.attempt-timeout-aborts</c> judge a failure with.</summary>
[Trait("Category", "Unit")]
public sealed class OutcomeTests
{
    [Fact]
    public void A_cancellation_carries_the_callers_token_when_it_is_the_failure_or_anywhere_in_its_cause_chain()
    {
        using var caller = new CancellationTokenSource();
        using var other = new CancellationTokenSource();
        caller.Cancel();
        other.Cancel();

        var direct = new OperationCanceledException(caller.Token);
        var wrapped = new SdkException("wrapper", new IOException("io", new OperationCanceledException("inner", caller.Token)));

        Assert.Same(direct, Outcome.FindCancellationOf(direct, caller.Token));
        Assert.NotNull(Outcome.FindCancellationOf(wrapped, caller.Token));
    }

    [Fact]
    public void A_cancellation_without_the_callers_token_is_not_the_callers_cancellation()
    {
        using var caller = new CancellationTokenSource();
        using var other = new CancellationTokenSource();
        caller.Cancel();
        other.Cancel();

        Assert.Null(Outcome.FindCancellationOf(new OperationCanceledException(), caller.Token));
        Assert.Null(Outcome.FindCancellationOf(new OperationCanceledException(other.Token), caller.Token));
        Assert.Null(Outcome.FindCancellationOf(new InvalidOperationException("not a cancellation"), caller.Token));
        Assert.Null(Outcome.FindCancellationOf(null, caller.Token));
    }

    [Fact]
    public void A_timeout_is_found_on_the_failure_in_its_causes_and_in_its_trail()
    {
        var timeout = new ServiceRequestTimeoutException("attempt timed out");
        var inCause = new SdkException("wrapper", new IOException("io", new TimeoutException("clock")));
        var inTrail = new SdkException("exhausted");
        ExceptionTrail.AddSuppressed(inTrail, new OperationTimeoutException("deadline"));

        Assert.Same(timeout, Outcome.FindTimeout(timeout));
        Assert.IsType<TimeoutException>(Outcome.FindTimeout(inCause));
        Assert.IsType<OperationTimeoutException>(Outcome.FindTimeout(inTrail));
    }

    [Fact]
    public void An_unrelated_failure_holds_no_timeout_and_a_cycle_ends_the_search()
    {
        var unrelated = new SdkException("the exchange failed", new IOException("reset"));
        var cycle = new SdkException("a");
        ExceptionTrail.AddSuppressed(cycle, new SdkException("b", cycle));

        Assert.Null(Outcome.FindTimeout(unrelated));
        Assert.Null(Outcome.FindTimeout(null));
        Assert.Null(Outcome.FindTimeout(cycle));
    }
}
