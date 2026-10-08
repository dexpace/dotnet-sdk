// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>The decision value and the stop-reason enum (R6, R7, R8).</summary>
[Trait("Category", "Unit")]
public sealed class RedirectDecisionTests
{
    [Fact]
    public void ReturnCurrent_carries_its_reason()
    {
        var target = new Uri("https://h/b");

        var decision = RedirectDecision.ReturnCurrent(RedirectStopReason.LoopDetected, target, "raw");

        Assert.Equal(RedirectDecisionKind.ReturnCurrent, decision.Kind);
        Assert.Equal(RedirectStopReason.LoopDetected, decision.Reason);
        Assert.Same(target, decision.Target);
        Assert.Equal("raw", decision.MalformedRaw);
    }

    [Fact]
    public void Follow_carries_the_built_request()
    {
        var next = Request.Get("https://h/b");

        var decision = RedirectDecision.Follow(next, next.Url, crossOrigin: true, downgraded: true);

        Assert.Equal(RedirectDecisionKind.Follow, decision.Kind);
        Assert.Same(next, decision.Next);
        Assert.Same(next.Url, decision.Target);
        Assert.True(decision.CrossOrigin);
        Assert.True(decision.Downgraded);
    }

    [Fact]
    public void Fail_carries_the_exception_and_its_kind()
    {
        var ex = new RedirectSchemeDowngradeException("x");

        var decision = RedirectDecision.Fail(ex, RedirectFailureKind.SchemeDowngrade);

        Assert.Equal(RedirectDecisionKind.Fail, decision.Kind);
        Assert.Same(ex, decision.Exception);
        Assert.Equal(RedirectFailureKind.SchemeDowngrade, decision.FailureKind);
    }

    [Fact]
    public void RedirectStopReason_values_are_explicit()
    {
        Assert.Equal(0, (int)RedirectStopReason.NotARedirect);
        Assert.Equal(1, (int)RedirectStopReason.NotEligible);
        Assert.Equal(2, (int)RedirectStopReason.MalformedLocation);
        Assert.Equal(3, (int)RedirectStopReason.LoopDetected);
        Assert.Equal(4, (int)RedirectStopReason.HopCap);
        Assert.Equal(1, (int)RedirectFailureKind.SchemeDowngrade);
        Assert.Equal(2, (int)RedirectFailureKind.BodyNotReplayable);
    }
}
