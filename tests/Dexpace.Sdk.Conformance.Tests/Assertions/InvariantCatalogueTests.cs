// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Assertions;

/// <summary>
/// The pin over the invariant catalogue (plan 1.3, "Invariant catalogue: the 28 assertions"): the names in order with their
/// requirement IDs, levels and faces, and a negative control for every one. The literal list is the design's table, so a later
/// edit to the catalogue must edit this list on purpose.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class InvariantCatalogueTests
{
    private const RequirementLevel M = RequirementLevel.Must;
    private const RequirementLevel S = RequirementLevel.Should;

    private static readonly TransportFace[] s_both = [TransportFace.Async, TransportFace.Blocking];
    private static readonly TransportFace[] s_none = [TransportFace.None];

    internal static (string Name, string[] Ids, RequirementLevel Level, TransportFace[] Faces)[] Expected { get; } =
    [
        ("xcut-1.cancel-is-terminal", ["XCUT-1"], M, s_both),
        ("xcut-2.timeout-is-retryable", ["XCUT-2"], M, s_both),
        ("xcut-3.wait-promptly-cancellable", ["XCUT-3"], M, s_both),
        ("xcut-4.two-branches", ["XCUT-4"], M, s_both),
        ("xcut-5.one-classifier", ["XCUT-5"], M, s_none),
        ("xcut-6.capability-queried", ["XCUT-6"], M, s_both),
        ("xcut-7.configured-set-authoritative", ["XCUT-7"], M, s_both),
        ("xcut-8.strict-factory-rejects", ["XCUT-8"], M, s_none),
        ("xcut-8.convenience-returns-response", ["XCUT-8"], M, s_both),
        ("xcut-9.cycle-safe-walk", ["XCUT-9"], M, s_none),
        ("xcut-10.safety-gate-uniform", ["XCUT-10"], M, s_both),
        ("xcut-11.shared-no-crosstalk", ["XCUT-11"], M, s_both),
        ("xcut-12.single-flight-scoped", ["XCUT-12"], S, s_both),
        ("xcut-13.dispose-latched", ["XCUT-13"], M, s_both),
        ("xcut-13.dispose-non-blocking", ["XCUT-13"], M, s_both),
        ("xcut-14.bounded-under-burst", ["XCUT-14"], M, s_none),
        ("xcut-14.drains-after-insert", ["XCUT-14"], M, s_none),
        ("xcut-15.no-external-alias", ["XCUT-15"], M, s_none),
        ("xcut-16.no-credential-over-http", ["XCUT-16"], M, s_both),
        ("xcut-17.redirect-hygiene", ["XCUT-17"], M, s_both),
        ("xcut-18.model-validation", ["XCUT-18"], M, s_none),
        ("xcut-19.redaction-default-deny", ["XCUT-19"], M, s_both),
        ("xcut-20.observability-never-throws", ["XCUT-20"], M, s_both),
        ("xcut-21.cnonce-unpredictable", ["XCUT-21"], M, s_none),
        ("xcut-22.core-borrows", ["XCUT-22"], M, s_both),
        ("xcut-23.resolution-deterministic", ["XCUT-23"], M, s_none),
        ("xcut-24.preview-capped-non-consuming", ["XCUT-24"], S, s_both),
        ("nfr-11.scheduler-agnostic", ["NFR-11"], S, s_both),
    ];

    [GeneratedRegex(@"^[a-z]+-\d+\.[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    private static IReadOnlyList<ConformanceAssertion> All => InvariantSuite.Assertions;

    /// <summary>The expected rows the catalogue has registered so far: the full 28 once task 1.9 lands (the one deliberate staged pin).</summary>
    private static (string Name, string[] Ids, RequirementLevel Level, TransportFace[] Faces)[] Registered => [.. Expected.Take(All.Count)];

    [Fact]
    public void The_registered_assertions_are_the_design_order_prefix_of_the_28_names()
    {
        Assert.Equal(28, Expected.Length);
        Assert.Equal(Registered.Select(e => e.Name), All.Select(a => a.Name));
    }

    [Fact]
    public void Each_registered_assertion_has_the_ids_level_and_faces_of_the_design_table()
    {
        var expected = Registered;
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Ids, All[i].RequirementIds);
            Assert.Equal(expected[i].Level, All[i].Level);
            Assert.Equal(expected[i].Faces, All[i].Faces);
        }
    }

    [Fact]
    public void Names_are_stable_identifiers_and_no_assertion_is_stronger_than_its_strongest_requirement()
    {
        foreach (var assertion in All)
        {
            Assert.Matches(NamePattern(), assertion.Name);
            Assert.All(assertion.RequirementIds, id => Assert.True(RequirementIndex.Contains(id), id));
            var strongest = assertion.RequirementIds.Select(id => { RequirementIndex.TryGetLevel(id, out var level); return level; }).Min();
            Assert.True(assertion.Level >= strongest, $"{assertion.Name} is {assertion.Level} but its strongest requirement is only {strongest}");
        }
    }

    [Fact]
    public void Every_invariant_assertion_has_a_negative_control()
    {
        var uncovered = All.Select(a => a.Name).Except(ControlClasses.Covered, StringComparer.Ordinal).ToArray();

        Assert.Empty(uncovered);
    }
}
