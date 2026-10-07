// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/builder.test.ts (flatten/nest seeding).

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class FlattenNestTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    private static HttpPipeline InnerWithForkingRedirect(RecordingTransport transport, DexpaceClientOptions? options = null) =>
        new PipelineBuilder()
            .Add(new ForkingProbe(PipelineStage.Redirect, drives: 2))
            .Build(transport, options ?? new DexpaceClientOptions());

    [Fact]
    public async Task A_probe_added_after_flatten_runs_inside_the_inner_loops_and_after_nest_runs_once()
    {
        var transport = new RecordingTransport();
        using var inner = InnerWithForkingRedirect(transport);

        var flatLog = new List<string>();
        var flatProbe = new ProbePolicy("flat", PipelineStage.PerHop, flatLog);
        using var flattened = PipelineBuilder.Flatten(inner).Add(flatProbe).Build();
        using var flatResponse = await flattened.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        var nestLog = new List<string>();
        var nestProbe = new ProbePolicy("nest", PipelineStage.PerHop, nestLog);
        using var nested = PipelineBuilder.Nest(inner).Add(nestProbe).Build();
        using var nestResponse = await nested.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(2, flatProbe.Entered);
        Assert.Equal(1, nestProbe.Entered);
    }

    [Fact]
    public void Flatten_copies_policies_transport_and_client_options()
    {
        var transport = new RecordingTransport();
        var options = new DexpaceClientOptions { UserAgent = "inner/1" };
        using var inner = InnerWithForkingRedirect(transport, options);

        var builder = PipelineBuilder.Flatten(inner);
        using var flattened = builder.Build();

        Assert.Equal(inner.Policies.Count, flattened.Policies.Count);
        Assert.Same(inner.Policies[0], flattened.Policies[0]);
        Assert.Same(inner.Transport, flattened.Transport);
        Assert.Same(options, flattened.ClientOptions);
    }

    [Fact]
    public void Nest_starts_empty_with_the_pipeline_as_its_transport()
    {
        using var inner = InnerWithForkingRedirect(new RecordingTransport());

        using var nested = PipelineBuilder.Nest(inner).Build();

        Assert.Empty(nested.Policies);
        Assert.Same(inner, nested.Transport);
    }

    [Fact]
    public void Build_without_a_seed_throws_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => new PipelineBuilder().Build());
    }

    [Fact]
    public void Adding_a_second_pillar_policy_after_flatten_follows_PIPE_5()
    {
        using var inner = InnerWithForkingRedirect(new RecordingTransport());

        var builder = PipelineBuilder.Flatten(inner);

        Assert.Throws<InvalidOperationException>(() => builder.Add(new ValueEqualPolicy(PipelineStage.Redirect)));
        builder.Replace<ForkingProbe>(new ValueEqualPolicy(PipelineStage.Redirect));
        Assert.Single(builder.Entries);
    }
}
