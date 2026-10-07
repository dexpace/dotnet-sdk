// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class NonPillarOrderTests
{
    private static ProbePolicy Probe(string name, PipelineStage stage) => new(name, stage, []);

    private static IReadOnlyList<HttpPipelinePolicy> Built(PipelineBuilder builder) =>
        builder.Build(new RecordingTransport()).Policies;

    [Fact]
    public void Append_adds_to_the_tail_and_prepend_to_the_head()
    {
        var a = Probe("a", PipelineStage.PerCall);
        var b = Probe("b", PipelineStage.PerCall);
        var head = Probe("head", PipelineStage.PerCall);

        var policies = Built(new PipelineBuilder().Add(a).Add(b).Prepend(head));

        Assert.Equal<HttpPipelinePolicy>([head, a, b], policies);
    }

    [Fact]
    public void Order_within_a_stage_survives_an_edit_in_another_stage()
    {
        var a = Probe("a", PipelineStage.PerCall);
        var b = Probe("b", PipelineStage.PerCall);
        var builder = new PipelineBuilder().Add(a).Add(Probe("hop", PipelineStage.PerHop)).Add(b);

        builder.Add(Probe("attempt", PipelineStage.PerAttempt)).Remove<ValueEqualPolicy>();
        var perCall = Built(builder).Where(p => p.Stage == PipelineStage.PerCall).ToArray();

        Assert.Equal<HttpPipelinePolicy>([a, b], perCall);
    }

    [Fact]
    public void Re_adding_a_non_pillar_instance_appends_it_again()
    {
        // PIPE-7 (the letter): PIPE-6 speaks of pillars only.
        var a = Probe("a", PipelineStage.PerCall);

        var policies = Built(new PipelineBuilder().Add(a).Add(a));

        Assert.Equal<HttpPipelinePolicy>([a, a], policies);
    }
}
