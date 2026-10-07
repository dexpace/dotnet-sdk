// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class SyncPathTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    [Fact]
    public async Task Sync_and_async_sends_visit_the_same_policies_in_the_same_order()
    {
        var syncLog = new List<string>();
        var asyncLog = new List<string>();
        var (syncPipeline, syncProbes) = Build(syncLog);
        var (asyncPipeline, asyncProbes) = Build(asyncLog);

        using var syncResponse = syncPipeline.Send(MakeRequest(), TestContext.Current.CancellationToken);
        using var asyncResponse = await asyncPipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(asyncLog, syncLog);
        Assert.Equal(["a:in", "b:in", "b:out", "a:out"], syncLog);
        Assert.All(syncProbes, probe => Assert.Equal(["sync"], probe.EntryPoints));
        Assert.All(asyncProbes, probe => Assert.Equal(["async"], probe.EntryPoints));

        static (HttpPipeline, ProbePolicy[]) Build(List<string> log)
        {
            ProbePolicy[] probes =
            [
                new("a", PipelineStage.Operation, log),
                new("b", PipelineStage.PerAttempt, log),
            ];
            return (new PipelineBuilder().Add(probes[0]).Add(probes[1]).Build(new RecordingTransport()), probes);
        }
    }

    [Theory]
    [MemberData(nameof(AsyncErrorModelTests.ShippedPolicies), MemberType = typeof(AsyncErrorModelTests))]
    public void The_sync_send_never_calls_an_async_member_of_a_shipped_policy(string name)
    {
        // Structural: the policy (or AuthorizationPolicy, which seals both entry points) overrides Process itself, so the
        // base class's blocking bridge is never the shipped path.
        var policy = AsyncErrorModelTests.Shipped(name);
        Assert.NotEqual(typeof(HttpPipelinePolicy), policy.GetType().GetMethod(nameof(HttpPipelinePolicy.Process))!.DeclaringType);

        // Behavioural: over a transport whose ExecuteAsync throws, the sync send still completes.
        var transport = new SyncFirstTransport();
        var pipeline = new PipelineBuilder().Add(policy).Build(transport);

        using var response = pipeline.Send(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public void Run_reaches_the_transports_Execute_when_it_implements_IHttpClient()
    {
        var transport = new SyncFirstTransport();

        using var response = TestContexts.RunnerOver(transport).Run(MakeRequest(), TestContexts.For());

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public void Run_uses_AsBlocking_for_an_async_only_transport()
    {
        using var transport = new RecordingTransport();
        var asyncOnly = DelegateOnly(transport);

        using var response = TestContexts.RunnerOver(asyncOnly).Run(MakeRequest(), TestContexts.For());

        Assert.Equal(1, transport.CallCount);
        Assert.False(asyncOnly is IHttpClient);
    }

    [Fact]
    public void GetCompletedResult_rethrows_the_original_exception_of_a_faulted_completed_task()
    {
        // Fact 2: a completed faulted task rethrows the original, never an AggregateException.
        var failure = new InvalidOperationException("boom");
        var task = new ValueTask<Response>(Task.FromException<Response>(failure));

        var thrown = Assert.Throws<InvalidOperationException>(() => SyncPath.GetCompletedResult(task, "test"));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void GetCompletedResult_throws_when_the_task_has_not_completed()
    {
        var pending = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);

        var ex = Assert.Throws<InvalidOperationException>(
            () => SyncPath.GetCompletedResult(new ValueTask<Response>(pending.Task), "TestPolicy"));

        Assert.Contains("TestPolicy", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_default_Process_bridges_to_ProcessAsync()
    {
        // A third-party policy that only overrides ProcessAsync is bridged by the documented default.
        var policy = new DelegatePolicy(PipelineStage.Operation, async (request, context, next) =>
        {
            await Task.Yield();
            return await next.RunAsync(request, context).ConfigureAwait(false);
        });
        var pipeline = new PipelineBuilder().Add(policy).Build(new RecordingTransport());

        using var response = pipeline.Send(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
    }

    private static IAsyncHttpClient DelegateOnly(RecordingTransport inner) =>
        DelegateHttpClient.Create((request, options, token) => inner.ExecuteAsync(request, options, token));
}
