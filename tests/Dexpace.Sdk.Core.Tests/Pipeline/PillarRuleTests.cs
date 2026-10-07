// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/builder.test.ts (collision, idempotence, reserved stage).

using System.Reflection;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class PillarRuleTests
{
    public static TheoryData<PipelineStage> Pillars => new(
        PipelineStage.Operation,
        PipelineStage.Redirect,
        PipelineStage.Retry,
        PipelineStage.Auth,
        PipelineStage.Diagnostics,
        PipelineStage.Serde);

    public static TheoryData<string> InsertionPaths => new("Add", "Prepend", "InsertBefore", "InsertAfter", "AddRange", "PrependRange");

    private static PipelineBuilder Insert(PipelineBuilder builder, string path, HttpPipelinePolicy policy) => path switch
    {
        "Add" => builder.Add(policy),
        "Prepend" => builder.Prepend(policy),
        "InsertBefore" => builder.InsertBefore<ProbePolicy>(policy),
        "InsertAfter" => builder.InsertAfter<ProbePolicy>(policy),
        "AddRange" => builder.AddRange([policy]),
        "PrependRange" => builder.PrependRange([policy]),
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    [Theory]
    [MemberData(nameof(Pillars))]
    public void Each_pillar_admits_one_policy(PipelineStage stage)
    {
        var builder = new PipelineBuilder().Add(new ProbePolicy("a", stage, []));

        Assert.Throws<InvalidOperationException>(() => builder.Add(new ValueEqualPolicy(stage)));
        Assert.Single(builder.Entries);
    }

    [Fact]
    public void A_custom_policy_may_occupy_the_serde_pillar()
    {
        var policy = new ValueEqualPolicy(PipelineStage.Serde);

        var pipeline = new PipelineBuilder().Add(policy).Build(new RecordingTransport());

        Assert.Same(policy, Assert.Single(pipeline.Policies));
    }

    [Theory]
    [MemberData(nameof(InsertionPaths))]
    public void A_second_distinct_policy_on_a_pillar_fails_at_add_naming_both_types(string path)
    {
        var builder = new PipelineBuilder().Add(new ProbePolicy("first", PipelineStage.Retry, []));
        var second = new ValueEqualPolicy(PipelineStage.Retry);

        var ex = Assert.Throws<InvalidOperationException>(() => Insert(builder, path, second));

        Assert.Contains("Retry", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ProbePolicy), ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ValueEqualPolicy), ex.Message, StringComparison.Ordinal);
        Assert.Contains("Replace", ex.Message, StringComparison.Ordinal);
        Assert.Single(builder.Entries);
    }

    [Fact]
    public void Replace_swaps_a_pillar_occupant_without_a_collision()
    {
        var replacement = new ValueEqualPolicy(PipelineStage.Retry);
        var builder = new PipelineBuilder()
            .Add(new ProbePolicy("first", PipelineStage.Retry, []))
            .Replace<ProbePolicy>(replacement);

        Assert.Same(replacement, Assert.Single(builder.Entries).Policy);
    }

    [Fact]
    public void Re_adding_the_same_instance_is_idempotent()
    {
        var policy = new ProbePolicy("p", PipelineStage.Retry, []);
        var builder = new PipelineBuilder().Add(policy).Add(policy).Prepend(policy).AddRange([policy]).PrependRange([policy]);

        Assert.Same(policy, Assert.Single(builder.Entries).Policy);
    }

    [Fact]
    public void Two_value_equal_policies_are_distinct()
    {
        // Fact 4: PIPE-6 is reference identity, not value equality. Both halves in one test: the pair IS Equals and is
        // NOT ReferenceEquals, and the second Add still throws; an Equals-based check would treat it as a no-op.
        var first = new ValueEqualPolicy(PipelineStage.Retry);
        var second = new ValueEqualPolicy(PipelineStage.Retry);
        Assert.Equal(first, second);
        Assert.NotSame(first, second);

        var builder = new PipelineBuilder().Add(first);

        Assert.Throws<InvalidOperationException>(() => builder.Add(second));
    }

    [Theory]
    [MemberData(nameof(InsertionPaths))]
    public void A_policy_reporting_an_undefined_stage_is_rejected(string path)
    {
        foreach (var value in new[] { 800, 0 })
        {
            var builder = new PipelineBuilder().Add(new ProbePolicy("anchor", PipelineStage.PerCall, []));

            var ex = Assert.Throws<ArgumentException>(() => Insert(builder, path, new ValueEqualPolicy((PipelineStage)value)));

            Assert.Equal("policy", ex.ParamName);
            Assert.Single(builder.Entries);
        }
    }

    [Fact]
    public void A_policy_whose_stage_flips_cannot_pass_validation_in_one_slot_and_run_in_another()
    {
        // Fact 6: the builder reads Stage once, so the recorded stage is the one that validated and the one that sorts.
        var flipping = new FlippingStagePolicy(PipelineStage.Operation, PipelineStage.Retry);
        var tail = new ProbePolicy("tail", PipelineStage.Redirect, []);

        var pipeline = new PipelineBuilder().Add(tail).Add(flipping).Build(new RecordingTransport());

        Assert.Equal(1, flipping.Reads);
        Assert.Same(flipping, pipeline.Policies[0]);
        Assert.Same(tail, pipeline.Policies[1]);
    }

    [Fact]
    public void Authorization_policy_entry_points_are_sealed()
    {
        // AUTH-28, PIPE-36: a subclass cannot skip the HTTPS guard on either path.
        Assert.True(typeof(AuthorizationPolicy).GetMethod(nameof(AuthorizationPolicy.Process))!.IsFinal);
        Assert.True(typeof(AuthorizationPolicy).GetMethod(nameof(AuthorizationPolicy.ProcessAsync))!.IsFinal);
        Assert.True(typeof(AuthorizationPolicy).GetProperty(nameof(AuthorizationPolicy.Stage))!.GetMethod!.IsFinal);
    }

    [Fact]
    public void Every_shipped_pillar_policy_locks_its_stage()
    {
        var policies = typeof(HttpPipelinePolicy).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == typeof(RetryPolicy).Namespace && typeof(HttpPipelinePolicy).IsAssignableFrom(t))
            .ToArray();

        Assert.NotEmpty(policies);
        Assert.All(policies, type =>
            Assert.True(
                type.IsSealed || type.GetProperty(nameof(HttpPipelinePolicy.Stage), BindingFlags.Public | BindingFlags.Instance)!.GetMethod!.IsFinal,
                $"{type.Name} must be sealed or seal Stage"));
    }
}
