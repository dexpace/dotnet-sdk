// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-core/test/dexpace/resilience/budget_equivalence_test.rb.
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.Core.Resilience;
using Dexpace.Sdk.Core.Tests.Resilience;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

#pragma warning disable CA2000 // The fakes and responses hold nothing a test needs to release.

namespace Dexpace.Sdk.Core.Tests.Recovery;

/// <summary>RETRY-13, RETRY-14, RECOV-30: the two stacks share one loop, one calculator and one set of defaults.</summary>
[Trait("Category", "Unit")]
public sealed class RetryBudgetEquivalenceTests
{
    private static Func<double> Sequence()
    {
        double[] samples = [0.1, 0.9, 0.4, 0.6];
        var index = -1;
        return () => samples[Interlocked.Increment(ref index) % samples.Length];
    }

    private static ScriptedTransport Failing() => new(
        TestResponses.Create(Status.ServiceUnavailable),
        TestResponses.Create(Status.ServiceUnavailable),
        TestResponses.Create(Status.ServiceUnavailable),
        TestResponses.Create(Status.ServiceUnavailable));

    [Fact]
    public async Task At_the_defaults_both_stacks_make_three_sends_wait_the_same_delays_and_surface_the_same_failure_type()
    {
        var options = new RetryOptions();

        var stageClock = new RecordingFakeTimeProvider();
        var stageTransport = Failing();
        var stagePolicy = new RetryPolicy(stageClock, Sequence());
        var stagePipeline = new PipelineBuilder().Add(stagePolicy).Add(new ErrorMappingPolicy()).Build(stageTransport);
        var stage = await Assert.ThrowsAsync<HttpResponseException>(async () =>
            await EngineHarness.DriveAsync(stagePipeline.SendAsync(EngineHarness.s_get, options.ToClientOptions(), TestContext.Current.CancellationToken).AsTask(), stageClock));

        var recoveryClock = new RecordingFakeTimeProvider();
        var recoveryTransport = Failing();
        var retry = new RetryRecovery(options, TimeSpan.Zero, recoveryClock, Sequence());
        var dispatcher = new RecoveryDispatcher(new RequestRecoveryChain([]), ResponseRecoveryChain.Empty, retry);
        var recovery = await Assert.ThrowsAsync<HttpResponseException>(async () =>
            await EngineHarness.DriveAsync(
                dispatcher.DispatchAsync(recoveryTransport, EngineHarness.s_get, RequestOptions.Empty, TestContext.Current.CancellationToken).AsTask(),
                recoveryClock));

        Assert.Equal(3, stageTransport.CallCount);
        Assert.Equal(3, recoveryTransport.CallCount);
        Assert.Equal(stageClock.Timers, recoveryClock.Timers);
        Assert.Equal(2, stageClock.Timers.Count);
        // The stage stack returns the last 503 live and maps it outside the loop, so its trail is dropped (PIPE-40); the
        // recovery stack maps on arrival and keeps it (RECOV-20). The surfaced type and status agree.
        Assert.Equal(stage.Status, recovery.Status);

        // RETRY-13: both stacks route through the one engine type.
        Assert.IsType<RetryEngine>(stagePolicy.Engine);
        Assert.IsType<RetryEngine>(retry.Engine);
    }
}
