// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Assertions;

/// <summary>The pin over the packaging catalogue (plan 3.1 to 3.4): the 8 names in order with their requirement IDs and levels, and a negative control for every one.</summary>
[Trait("Category", "Unit")]
public sealed partial class PackagingCatalogueTests
{
    private const RequirementLevel M = RequirementLevel.Must;
    private const RequirementLevel S = RequirementLevel.Should;

    private static readonly (string Name, string[] Ids, RequirementLevel Level)[] s_expected =
    [
        ("nfr-1.core-dependencies", ["NFR-1"], M),
        ("nfr-2.adapter-budget", ["NFR-2"], S),
        ("nfr-3.no-internals-granted", ["NFR-3"], S),
        ("nfr-8.trim-and-aot-declared", ["NFR-8", "NFR-9"], M),
        ("nfr-10.single-floor", ["NFR-10"], M),
        ("nfr-13.license-declared", ["NFR-13"], S),
        ("nfr-14.lockstep-version", ["NFR-14"], S),
        ("nfr-15.version-resolvable", ["NFR-15"], S),
    ];

    [GeneratedRegex(@"^[a-z]+-\d+\.[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    private static IReadOnlyList<ConformanceAssertion> All => PackagingSuite.Assertions;

    [Fact]
    public void The_catalogue_is_the_8_named_assertions_in_order()
    {
        Assert.Equal(8, All.Count);
        Assert.Equal(s_expected.Select(e => e.Name), All.Select(a => a.Name));
        for (var i = 0; i < s_expected.Length; i++)
        {
            Assert.Equal(s_expected[i].Ids, All[i].RequirementIds);
            Assert.Equal(s_expected[i].Level, All[i].Level);
        }
    }

    [Fact]
    public void Names_are_stable_identifiers_and_no_assertion_is_stronger_than_its_strongest_requirement()
    {
        foreach (var assertion in All)
        {
            Assert.Matches(NamePattern(), assertion.Name);
            var strongest = assertion.RequirementIds.Select(id => { RequirementIndex.TryGetLevel(id, out var level); return level; }).Min();
            Assert.True(assertion.Level >= strongest, $"{assertion.Name} is {assertion.Level} but its strongest requirement is only {strongest}");
        }
    }

    [Fact]
    public void Every_packaging_assertion_has_a_negative_control()
    {
        var uncovered = All.Select(a => a.Name).Except(ControlClasses.Covered, StringComparer.Ordinal).ToArray();

        Assert.Empty(uncovered);
    }
}
