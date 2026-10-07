// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/builder.test.ts (bulk all-or-nothing, append/prepend asymmetry).

using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class BulkTests
{
    private static ProbePolicy Probe(string name, PipelineStage stage) => new(name, stage, []);

    private static HttpPipelinePolicy[] Snapshot(PipelineBuilder builder) => [.. builder.Entries.Select(e => e.Policy)];

    [Theory]
    [InlineData("AddRange", true)]
    [InlineData("AddRange", false)]
    [InlineData("PrependRange", true)]
    [InlineData("PrependRange", false)]
    public void A_colliding_batch_leaves_the_builder_unchanged(string method, bool collideWithBuilder)
    {
        var existing = Probe("existing", PipelineStage.Retry);
        var builder = new PipelineBuilder().Add(existing).Add(Probe("per-call", PipelineStage.PerCall));
        var before = Snapshot(builder);
        HttpPipelinePolicy[] batch = collideWithBuilder
            ? [Probe("ok", PipelineStage.PerCall), new ValueEqualPolicy(PipelineStage.Retry)]
            : [Probe("ok", PipelineStage.PerCall), new ValueEqualPolicy(PipelineStage.Auth), Probe("again", PipelineStage.Auth)];

        Assert.Throws<InvalidOperationException>(() =>
            _ = method == "AddRange" ? builder.AddRange(batch) : builder.PrependRange(batch));

        Assert.Equal(before, Snapshot(builder));
    }

    [Fact]
    public void AddRange_keeps_batch_order_and_PrependRange_reverses_it()
    {
        var a = Probe("a", PipelineStage.PerCall);
        var b = Probe("b", PipelineStage.PerCall);
        var c = Probe("c", PipelineStage.PerCall);
        var pillar = Probe("pillar", PipelineStage.Retry);

        var added = new PipelineBuilder().AddRange([a, b, c, pillar]).Build(new RecordingTransport());
        var prepended = new PipelineBuilder().PrependRange([a, b, c, pillar]).Build(new RecordingTransport());

        Assert.Equal<HttpPipelinePolicy>([a, b, c, pillar], added.Policies);
        Assert.Equal<HttpPipelinePolicy>([c, b, a, pillar], prepended.Policies);
    }
}
