// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Pipeline;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class PipelineStageFactsTests
{
    [Fact]
    public void Pillars_are_exactly_the_six()
    {
        Assert.Equal(
            new[]
            {
                PipelineStage.Operation,
                PipelineStage.Redirect,
                PipelineStage.Retry,
                PipelineStage.Auth,
                PipelineStage.Diagnostics,
                PipelineStage.Serde,
            }.OrderBy(s => (int)s),
            PipelineStageFacts.PillarStages.OrderBy(s => (int)s));
        Assert.All(
            Enum.GetValues<PipelineStage>().Except(PipelineStageFacts.PillarStages),
            stage => Assert.False(PipelineStageFacts.IsPillar(stage)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(800)]
    [InlineData(-1)]
    [InlineData(125)]
    public void IsDefinedStage_rejects_values_outside_the_enum(int value)
    {
        // Fact 3: an undefined value is a legal value of the type and not a stage.
        Assert.False(PipelineStageFacts.IsDefinedStage((PipelineStage)value));
    }

    [Fact]
    public void IsDefinedStage_accepts_every_member()
    {
        Assert.All(Enum.GetValues<PipelineStage>(), stage => Assert.True(PipelineStageFacts.IsDefinedStage(stage)));
    }
}
