// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Assertions;

/// <summary>
/// The pin over the codec catalogue (plan 2.1 to 2.4): the 11 names in order with their requirement IDs, levels and faces, and a negative control
/// for every one. <c>SERDE-4</c>, <c>-13</c>, <c>-14</c>, <c>-18</c> and <c>-30</c> are not here by design (validation F6: they judge core code).
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class CodecCatalogueTests
{
    private const RequirementLevel M = RequirementLevel.Must;
    private const RequirementLevel S = RequirementLevel.Should;

    private static readonly TransportFace[] s_both = [TransportFace.Async, TransportFace.Blocking];
    private static readonly TransportFace[] s_none = [TransportFace.None];

    private static readonly (string Name, string[] Ids, RequirementLevel Level, TransportFace[] Faces)[] s_expected =
    [
        ("serde-1.round-trip", ["SERDE-1"], M, s_both),
        ("serde-2.declared-media-type", ["SERDE-2"], M, s_none),
        ("serde-3.target-not-closed", ["SERDE-3", "SEAM-20"], M, s_both),
        ("serde-9.no-library-type-escapes", ["SERDE-9"], M, s_both),
        ("serde-10.directional-subtypes", ["SERDE-10"], M, s_both),
        ("serde-12.stream-error-unwrapped", ["SERDE-12"], M, s_both),
        ("serde-15.tristate-emission", ["SERDE-15"], M, s_both),
        ("serde-16.tristate-decode", ["SERDE-16"], M, s_both),
        ("serde-17.missing-key-absent", ["SERDE-17"], M, s_both),
        ("serde-19.default-wiring", ["SERDE-19"], M, s_both),
        ("serde-20.top-level-degrades", ["SERDE-20"], S, s_both),
    ];

    private static readonly string[] s_byReference = ["SERDE-4", "SERDE-13", "SERDE-14", "SERDE-18", "SERDE-30"];
    private static readonly string[] s_lifted = ["SERDE-1", "SERDE-2", "SERDE-3", "SEAM-20", "SERDE-9", "SERDE-10", "SERDE-12", "SERDE-15", "SERDE-16", "SERDE-17", "SERDE-19", "SERDE-20"];

    [GeneratedRegex(@"^[a-z]+-\d+\.[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    private static IReadOnlyList<ConformanceAssertion> All => CodecSuite.Assertions;

    [Fact]
    public void The_catalogue_is_the_11_named_assertions_in_the_design_order()
    {
        Assert.Equal(11, All.Count);
        Assert.Equal(s_expected.Select(e => e.Name), All.Select(a => a.Name));
    }

    [Fact]
    public void Each_assertion_has_the_ids_level_and_faces_of_the_design_table()
    {
        for (var i = 0; i < s_expected.Length; i++)
        {
            Assert.Equal(s_expected[i].Ids, All[i].RequirementIds);
            Assert.Equal(s_expected[i].Level, All[i].Level);
            Assert.Equal(s_expected[i].Faces, All[i].Faces);
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
    public void The_five_requirements_that_judge_core_code_are_deliberately_not_lifted()
    {
        var cited = All.SelectMany(a => a.RequirementIds).ToHashSet(StringComparer.Ordinal);

        Assert.All(s_byReference, id => Assert.DoesNotContain(id, cited));
        Assert.All(s_lifted, id => Assert.Contains(id, cited));
    }

    [Fact]
    public void Every_codec_assertion_has_a_negative_control()
    {
        var uncovered = All.Select(a => a.Name).Except(ControlClasses.Covered, StringComparer.Ordinal).ToArray();

        Assert.Empty(uncovered);
    }
}
