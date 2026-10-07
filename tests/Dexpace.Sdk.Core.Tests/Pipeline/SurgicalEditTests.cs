// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/builder.test.ts (anchor-not-found, cross-stage).

using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class SurgicalEditTests
{
    public static TheoryData<string> Edits => new("InsertBefore", "InsertAfter", "Replace");

    private static ProbePolicy Probe(PipelineStage stage = PipelineStage.PerCall) => new("p", stage, []);

    private static HttpPipelinePolicy[] Snapshot(PipelineBuilder builder) => [.. builder.Entries.Select(e => e.Policy)];

    private static PipelineBuilder Edit(PipelineBuilder builder, string edit, HttpPipelinePolicy policy) => edit switch
    {
        "InsertBefore" => builder.InsertBefore<ProbePolicy>(policy),
        "InsertAfter" => builder.InsertAfter<ProbePolicy>(policy),
        "Replace" => builder.Replace<ProbePolicy>(policy),
        _ => throw new ArgumentOutOfRangeException(nameof(edit)),
    };

    [Theory]
    [InlineData("InsertBefore")]
    [InlineData("InsertAfter")]
    public void Insert_relative_to_an_anchor_in_another_stage_is_rejected(string edit)
    {
        var builder = new PipelineBuilder().Add(Probe(PipelineStage.PerCall));
        var before = Snapshot(builder);

        var ex = Assert.Throws<ArgumentException>(() => Edit(builder, edit, new ValueEqualPolicy(PipelineStage.PerAttempt)));

        Assert.Equal("policy", ex.ParamName);
        Assert.Contains("PerCall", ex.Message, StringComparison.Ordinal);
        Assert.Contains("PerAttempt", ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(builder));
    }

    [Fact]
    public void Insert_next_to_the_first_instance_of_T_in_the_same_stage()
    {
        var anchor = Probe();
        var other = Probe();
        var inserted = new ValueEqualPolicy(PipelineStage.PerCall);

        var builder = new PipelineBuilder().Add(anchor).Add(other).InsertAfter<ProbePolicy>(inserted);

        Assert.Equal<HttpPipelinePolicy>([anchor, inserted, other], Snapshot(builder));
    }

    [Fact]
    public void A_cross_stage_replace_is_rejected()
    {
        var builder = new PipelineBuilder().Add(Probe(PipelineStage.PerCall));
        var before = Snapshot(builder);

        var ex = Assert.Throws<ArgumentException>(() => builder.Replace<ProbePolicy>(new ValueEqualPolicy(PipelineStage.PerAttempt)));

        Assert.Equal("policy", ex.ParamName);
        Assert.Equal(before, Snapshot(builder));
    }

    [Fact]
    public void Replace_swaps_only_the_first_instance()
    {
        var first = Probe();
        var second = Probe();
        var replacement = new ValueEqualPolicy(PipelineStage.PerCall);

        var builder = new PipelineBuilder().Add(first).Add(second).Replace<ProbePolicy>(replacement);

        Assert.Equal<HttpPipelinePolicy>([replacement, second], Snapshot(builder));
    }

    [Fact]
    public void Remove_deletes_every_instance_and_preserves_order()
    {
        var a1 = Probe();
        var b = new ValueEqualPolicy(PipelineStage.PerCall);
        var a2 = Probe();
        var c = new ShortCircuitPolicy(PipelineStage.PerCall, TestResponses.Create(Dexpace.Sdk.Core.Http.Response.Status.Ok));

        var builder = new PipelineBuilder().Add(a1).Add(b).Add(a2).Add(c).Remove<ProbePolicy>();

        Assert.Equal<HttpPipelinePolicy>([b, c], Snapshot(builder));
    }

    [Fact]
    public void Remove_of_an_absent_type_is_a_no_op()
    {
        var a = Probe();
        var builder = new PipelineBuilder().Add(a).Remove<ValueEqualPolicy>();

        Assert.Equal<HttpPipelinePolicy>([a], Snapshot(builder));
    }

    [Theory]
    [MemberData(nameof(Edits))]
    public void A_missing_anchor_throws_InvalidOperationException_naming_T(string edit)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Edit(new PipelineBuilder(), edit, Probe()));

        Assert.Contains(nameof(ProbePolicy), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Edited_order_equals_order_built_from_scratch()
    {
        // PIPE-22: every edit re-derives order from the recorded stage.
        var x = new ShortCircuitPolicy(PipelineStage.Operation, TestResponses.Create(Dexpace.Sdk.Core.Http.Response.Status.Ok));
        var a = Probe();
        var b = new ValueEqualPolicy(PipelineStage.PerCall);
        var y = new ForkingProbe(PipelineStage.Retry, drives: 1);
        var scratchPolicy = new DelegatePolicy(PipelineStage.PerCall, (r, c, n) => n.RunAsync(r, c));

        var edited = new PipelineBuilder()
            .Add(y)
            .Add(a)
            .Add(scratchPolicy)
            .InsertAfter<ProbePolicy>(b)
            .Add(x)
            .Remove<DelegatePolicy>()
            .Replace<ValueEqualPolicy>(b)
            .Build(new RecordingTransport());
        var fromScratch = new PipelineBuilder().Add(x).Add(a).Add(b).Add(y).Build(new RecordingTransport());

        Assert.Equal(fromScratch.Policies.Count, edited.Policies.Count);
        for (var i = 0; i < edited.Policies.Count; i++)
        {
            Assert.Same(fromScratch.Policies[i], edited.Policies[i]);
        }
    }
}
