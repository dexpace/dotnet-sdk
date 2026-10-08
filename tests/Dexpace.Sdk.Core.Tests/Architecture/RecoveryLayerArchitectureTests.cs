// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.Core.Resilience;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// The two-layer prohibition (spec §8.3, P4b-5): the recovery layer never references the pipeline layer. The reverse edge,
/// a policy calling a step, is allowed.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RecoveryLayerArchitectureTests
{
    private const string RecoveryNamespace = "Dexpace.Sdk.Core.Recovery";
    private const string ResilienceNamespace = "Dexpace.Sdk.Core.Resilience";
    private const string PipelineNamespace = "Dexpace.Sdk.Core.Pipeline";

    [Fact]
    public void No_recovery_type_references_a_pipeline_type()
    {
        var recoveryTypes = typeof(Outcome).Assembly.GetTypes()
            .Where(t => t.Namespace is RecoveryNamespace || (t.Namespace?.StartsWith(RecoveryNamespace + ".", StringComparison.Ordinal) ?? false))
            .ToList();
        Assert.NotEmpty(recoveryTypes);

        var offences = recoveryTypes
            .SelectMany(t => TypeReferences.Of(t).Select(referenced => (Type: t, Referenced: referenced)))
            .Where(pair => pair.Referenced.Namespace is { } ns
                && (ns == PipelineNamespace || ns.StartsWith(PipelineNamespace + ".", StringComparison.Ordinal)))
            .Select(pair => pair.Type.FullName + " -> " + pair.Referenced.FullName)
            .ToList();

        Assert.Empty(offences);
    }

    // 6a (P6a-4): the retry engine lives in Resilience and never draws an edge to Pipeline; the stage adapter hands it
    // delegates instead.
    [Fact]
    public void No_resilience_type_references_a_pipeline_type()
    {
        var resilienceTypes = typeof(RetryFacts).Assembly.GetTypes()
            .Where(t => t.Namespace is ResilienceNamespace || (t.Namespace?.StartsWith(ResilienceNamespace + ".", StringComparison.Ordinal) ?? false))
            .ToList();
        Assert.NotEmpty(resilienceTypes);

        var offences = resilienceTypes
            .SelectMany(t => TypeReferences.Of(t).Select(referenced => (Type: t, Referenced: referenced)))
            .Where(pair => pair.Referenced.Namespace is { } ns
                && (ns == PipelineNamespace || ns.StartsWith(PipelineNamespace + ".", StringComparison.Ordinal)))
            .Select(pair => pair.Type.FullName + " -> " + pair.Referenced.FullName)
            .ToList();

        Assert.Empty(offences);
    }
}
