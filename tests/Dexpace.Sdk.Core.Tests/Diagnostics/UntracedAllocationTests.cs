// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>
/// OBS-25: the untraced path allocates nothing (design 5c position G, P5c-15). The class installs no listener and runs in
/// <c>NoDiagnosticListeners</c>, so no process-wide listener from a parallel test can make a span exist or a measurement
/// count; the constructor fails loudly if one leaked.
/// </summary>
[Collection("NoDiagnosticListeners")]
[Trait("Category", "Unit")]
public sealed class UntracedAllocationTests
{
    private const int WarmUp = 500;
    private const int Iterations = 1000;

    private static readonly Request s_request = Request.Get("https://api.example.com/v1/items?token=SECRET&other=" + new string('x', 200));
    private static readonly DexpaceClientOptions s_options = new();
    private static readonly InvalidOperationException s_failure = new("preallocated");

    public UntracedAllocationTests()
    {
        Assert.False(
            DexpaceDiagnostics.ActivitySource.HasListeners(),
            "A Dexpace.Sdk ActivityListener leaked from another test; the allocation measurements would be corrupted.");
    }

    private static long AllocatedBytes(Action action)
    {
        for (var i = 0; i < WarmUp; i++)
        {
            action();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // The least allocation over several rounds: one-off type-loading and tiered-JIT allocations belong to early rounds, not
    // to the steady state OBS-25 is about.
    private static long BestOfRounds(Action action)
    {
        var best = long.MaxValue;
        for (var round = 0; round < 6; round++)
        {
            best = Math.Min(best, AllocatedBytes(action));
        }

        return best;
    }

    [Fact]
    public void Untraced_tracing_primitives_allocate_nothing()
    {
        using var response = TestResponses.Create(Status.Ok);
        var context = TestContexts.For(s_request);
        var state = context.State;
        var none = InstrumentationContext.None;

        Assert.Equal(0, BestOfRounds(() => Assert.Null(OperationTelemetry.Start(s_request, s_options))));
        Assert.Equal(0, BestOfRounds(() => InstrumentationContext.FromActivity(null)));
        Assert.Equal(0, BestOfRounds(() => none.StartActivity("GET", ActivityKind.Client)));
        Assert.Equal(0, BestOfRounds(() => OperationTelemetry.Complete(null, response)));
        Assert.Equal(0, BestOfRounds(() => OperationTelemetry.Fail(null, s_failure, state)));
        Assert.Equal(0, BestOfRounds(() => OperationTelemetry.Stop(null, settled: true)));
        Assert.Equal(0, BestOfRounds(() => state.NextTransmission()));
        Assert.Equal(0, BestOfRounds(() =>
        {
            var scope = new AttemptScope(s_request, context, UrlRedactor.Default);
            var telemetry = AttemptTelemetry.Begin(ref scope, s_request, context);
            telemetry.Succeeded(response, ref scope);
            telemetry.End();
        }));
        Assert.Equal(0, BestOfRounds(() =>
        {
            var counted = HttpClientMetrics.RequestStarted(s_request);
            HttpClientMetrics.RecordDuration(s_request, 0.01, response, failure: null);
            HttpClientMetrics.RequestEnded(s_request, counted);
        }));
    }

    [Fact]
    public void An_untraced_sync_send_through_InstrumentationPolicy_allocates_no_more_than_a_pass_through()
    {
        using var cached = TestResponses.Create(Status.Ok);
        var transport = new RecordingTransport(_ => cached);
        var instrumented = new PipelineBuilder().Add(new InstrumentationPolicy(NullLogger.Instance)).Build(transport);
        var passThrough = new PipelineBuilder().Add(new PassThroughPolicy()).Build(transport);

        var withPolicy = BestOfRounds(() => instrumented.Send(s_request, CancellationToken.None));
        var without = BestOfRounds(() => passThrough.Send(s_request, CancellationToken.None));

        Assert.True(withPolicy <= without, $"instrumented {withPolicy} bytes, pass-through {without} bytes over {Iterations} calls");
    }

    [Fact]
    public void Untraced_retry_and_redirect_event_calls_allocate_nothing()
    {
        using var response = TestResponses.Create(Status.ServiceUnavailable);
        var context = TestContexts.For(s_request);
        var target = new Uri("https://other.example.com/next");

        Assert.Equal(0, BestOfRounds(() => OperationTelemetry.AttemptFailed(context, response, failure: null, TimeSpan.FromSeconds(1))));
        Assert.Equal(0, BestOfRounds(() => OperationTelemetry.AttemptFailed(context, response: null, s_failure, TimeSpan.FromSeconds(1))));
        Assert.Equal(0, BestOfRounds(() => OperationTelemetry.RetrySequenceStarted(context)));
        Assert.Equal(0, BestOfRounds(() => OperationTelemetry.RetriesExhausted(context, 3)));
        Assert.Equal(0, BestOfRounds(() => OperationTelemetry.RedirectHop(context, 1, 302, target, crossOrigin: true)));
    }

    [Fact]
    public async Task The_untraced_path_does_not_compute_the_redacted_url()
    {
        using var cached = TestResponses.Create(Status.Ok);
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(NullLogger.Instance)).Build(new RecordingTransport(_ => cached));
        var shortUrl = Request.Get("https://api.example.com/p?a=1");
        var longUrl = Request.Get("https://api.example.com/p?a=" + new string('x', 20_000));

        var shortCost = await PerCallAsync(pipeline, shortUrl);
        var longCost = await PerCallAsync(pipeline, longUrl);

        // Redacting the 20 KB URL would cost tens of KB a call; a skipped redaction costs the same as the short one.
        Assert.True(longCost - shortCost < 200, $"short {shortCost} B, long {longCost} B per call");
    }

    private static async Task<long> PerCallAsync(HttpPipeline pipeline, Request request)
    {
        for (var i = 0; i < WarmUp; i++)
        {
            await pipeline.SendAsync(request, CancellationToken.None);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            await pipeline.SendAsync(request, CancellationToken.None);
        }

        return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
    }

    private sealed class PassThroughPolicy : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Diagnostics;

        public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
            continuation.RunAsync(request, context);

        public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
            continuation.Run(request, context);
    }
}
