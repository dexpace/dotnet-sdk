// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// Internal facts about <see cref="PipelineStage"/>: which stages are pillars and which values are stages at all.
/// </summary>
internal static class PipelineStageFacts
{
    /// <summary>
    /// The set of all pillar stages: each admits at most one policy (PIPE-4).
    /// </summary>
    internal static FrozenSet<PipelineStage> PillarStages { get; } = new[]
    {
        PipelineStage.Operation,
        PipelineStage.Redirect,
        PipelineStage.Retry,
        PipelineStage.Auth,
        PipelineStage.Diagnostics,
        PipelineStage.Serde,
    }.ToFrozenSet();

    private static readonly FrozenSet<PipelineStage> s_definedStages = new[]
    {
        PipelineStage.Operation,
        PipelineStage.PerCall,
        PipelineStage.Redirect,
        PipelineStage.PerHop,
        PipelineStage.Retry,
        PipelineStage.PerAttempt,
        PipelineStage.Auth,
        PipelineStage.Diagnostics,
        PipelineStage.Serde,
    }.ToFrozenSet();

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="stage"/> is a pillar stage that admits at most one policy.
    /// </summary>
    internal static bool IsPillar(PipelineStage stage) => PillarStages.Contains(stage);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="stage"/> is one of the named stages; an undefined numeric
    /// value (<c>(PipelineStage)800</c>) is a legal value of the type and not a stage (PIPE-8).
    /// </summary>
    internal static bool IsDefinedStage(PipelineStage stage) => s_definedStages.Contains(stage);
}
