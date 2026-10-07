// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/stage.test.ts, and from the Ruby 4c design's testing strategy
// (the gem tests are absent locally, R8).

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class StageOrderTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    [Fact]
    public void Stage_values_are_strictly_increasing_and_sparse()
    {
        // PIPE-3: declaration order equals numeric order, and the keys are sparse.
        var stages = Enum.GetValues<PipelineStage>();

        Assert.Equal(
            [
                (PipelineStage.Operation, 100),
                (PipelineStage.PerCall, 150),
                (PipelineStage.Redirect, 200),
                (PipelineStage.PerHop, 250),
                (PipelineStage.Retry, 300),
                (PipelineStage.PerAttempt, 400),
                (PipelineStage.Auth, 500),
                (PipelineStage.Diagnostics, 600),
                (PipelineStage.Serde, 700),
            ],
            stages.Select(stage => (stage, (int)stage)));
        for (var i = 1; i < stages.Length; i++)
        {
            Assert.True((int)stages[i] > (int)stages[i - 1]);
            Assert.True((int)stages[i] - (int)stages[i - 1] >= 50);
        }
    }

    [Fact]
    public void PerCall_runs_outside_redirect_and_PerHop_inside_it()
    {
        Assert.True(PipelineStage.PerCall < PipelineStage.Redirect);
        Assert.True(PipelineStage.Redirect < PipelineStage.PerHop);
        Assert.True(PipelineStage.PerHop < PipelineStage.Retry);
        Assert.True(PipelineStage.Operation < PipelineStage.PerCall);
    }

    [Fact]
    public async Task One_probe_per_stage_runs_in_stage_order_whatever_the_insertion_order()
    {
        var stages = Enum.GetValues<PipelineStage>();
        var log = new List<string>();
        var random = new Random(42);
        var shuffled = stages.OrderBy(_ => random.Next()).ToArray();
        Assert.NotEqual(stages, shuffled);

        var builder = new PipelineBuilder();
        foreach (var stage in shuffled)
        {
            builder.Add(new ProbePolicy(stage.ToString(), stage, log));
        }

        var pipeline = builder.Build(new RecordingTransport());
        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal([.. stages.Select(s => $"{s}:in")], log.Where(e => e.EndsWith(":in", StringComparison.Ordinal)));
        Assert.Equal([.. stages.Reverse().Select(s => $"{s}:out")], log.Where(e => e.EndsWith(":out", StringComparison.Ordinal)));
        Assert.Equal(stages.Length * 2, log.Count);
    }

    [Fact]
    public async Task A_per_call_probe_runs_once_and_sees_the_final_response_while_an_auth_probe_runs_per_hop()
    {
        var log = new List<string>();
        var perCall = new ProbePolicy("perCall", PipelineStage.PerCall, log);
        var auth = new ProbePolicy("auth", PipelineStage.Auth, log);
        var pipeline = new PipelineBuilder()
            .Add(perCall)
            .Add(new ForkingProbe(PipelineStage.Redirect, drives: 2))
            .Add(auth)
            .Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, perCall.Entered);
        Assert.Equal(2, auth.Entered);
        Assert.Same(response, perCall.LastResponse);
    }

    [Fact]
    public async Task A_per_hop_probe_runs_once_while_a_per_attempt_probe_runs_per_attempt()
    {
        var log = new List<string>();
        var perHop = new ProbePolicy("perHop", PipelineStage.PerHop, log);
        var perAttempt = new ProbePolicy("perAttempt", PipelineStage.PerAttempt, log);
        var pipeline = new PipelineBuilder()
            .Add(perHop)
            .Add(new ForkingProbe(PipelineStage.Retry, drives: 2))
            .Add(perAttempt)
            .Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, perHop.Entered);
        Assert.Equal(2, perAttempt.Entered);
        Assert.Same(response, perHop.LastResponse);
    }
}
