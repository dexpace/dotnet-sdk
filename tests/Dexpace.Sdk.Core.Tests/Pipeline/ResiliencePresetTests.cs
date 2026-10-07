// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Cases adopted from the Ruby 4c design's testing strategy (PIPE-23/PIPE-24; the gem tests are absent locally, R8).

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
[Collection("Instrumentation")]
public sealed class ResiliencePresetTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/items");

    public static TheoryData<PipelineStage> TargetPillars => new(
        PipelineStage.Operation,
        PipelineStage.Redirect,
        PipelineStage.Retry,
        PipelineStage.Diagnostics);

    [Theory]
    [MemberData(nameof(TargetPillars))]
    public void The_preset_installs_nothing_when_any_target_pillar_is_occupied(PipelineStage occupied)
    {
        var existing = new ValueEqualPolicy(occupied);
        var builder = new PipelineBuilder().Add(existing);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddStandardResilience());

        Assert.Contains(occupied.ToString(), ex.Message, StringComparison.Ordinal);
        Assert.Same(existing, Assert.Single(builder.Entries).Policy);
    }

    [Fact]
    public void The_preset_fills_empty_pillars_and_leaves_non_pillar_stages_alone()
    {
        var perCall = new ValueEqualPolicy(PipelineStage.PerCall);

        var pipeline = new PipelineBuilder().Add(perCall).AddStandardResilience(new InstantTimeProvider()).Build(new RecordingTransport());

        Assert.Equal(
            [typeof(OperationPolicy), typeof(ValueEqualPolicy), typeof(RedirectPolicy), typeof(RetryPolicy), typeof(InstrumentationPolicy)],
            pipeline.Policies.Select(p => p.GetType()));
        Assert.Same(perCall, pipeline.Policies[1]);
    }

    [Fact]
    public void The_preset_checks_all_four_before_installing_any()
    {
        var builder = new PipelineBuilder().Add(new ValueEqualPolicy(PipelineStage.Diagnostics));

        Assert.Throws<InvalidOperationException>(() => builder.AddStandardResilience());

        Assert.Single(builder.Entries);
        Assert.DoesNotContain(builder.Entries, e => e.Policy is OperationPolicy or RedirectPolicy or RetryPolicy);
    }

    [Fact]
    public async Task CreateEmpty_forwards_straight_to_the_transport()
    {
        var transport = new RecordingTransport();
        using var pipeline = DexpacePipeline.CreateEmpty(transport);
        var request = MakeRequest();
        var options = new RequestOptions { MaxRetries = 2 };

        using var response = await pipeline.SendAsync(request, options, TestContext.Current.CancellationToken);

        Assert.Empty(pipeline.Policies);
        Assert.Same(request, transport.LastCall!.Request);
        Assert.Same(options, transport.LastCall!.Options);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_async_and_sync_standard_pipelines_both_follow_a_redirect(bool useAsync)
    {
        // PIPE-32 / REDIR-25 (design §10 entry 14, async-redirect-pillar): one RedirectPolicy serves both paths.
        var transport = new ScriptedTransport(TestResponses.Redirect(307, "https://api.example.com/v1/moved"), TestResponses.Create(Status.Ok));
        using var pipeline = DexpacePipeline.CreateDefault(transport, timeProvider: new InstantTimeProvider());
        var options = new DexpaceClientOptions();

        using var response = useAsync
            ? await pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken)
            : pipeline.Send(MakeRequest(), options, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(2, transport.CallCount);
        Assert.Contains(pipeline.Policies, p => p is RedirectPolicy);
    }
}
