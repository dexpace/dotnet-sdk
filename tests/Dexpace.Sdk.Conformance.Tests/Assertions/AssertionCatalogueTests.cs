// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Text.RegularExpressions;
using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Assertions;

/// <summary>
/// The pin over the whole catalogue (plan 2.12): the names, their order, their requirement IDs, levels and faces, the rows
/// they cover, and a negative control for every one. The literal list is the design's table plus the two clauses of
/// <c>TRANSPORT-14</c> that phase 8b asked 8a to split out and <c>transport-2.bodyless-not-resent</c>, added by the review of the
/// phase (the native client's own retry of a body-less request), so a later edit to the catalogue must edit this list on purpose.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class AssertionCatalogueTests
{
    private const RequirementLevel M = RequirementLevel.Must;
    private const RequirementLevel S = RequirementLevel.Should;
    private const RequirementLevel Y = RequirementLevel.May;

    private static readonly string[] s_perTransportAsyncRows = ["ASYNC-1", "ASYNC-15", "ASYNC-2", "ASYNC-20", "ASYNC-22"];

    private static readonly TransportFace[] s_both = [TransportFace.Async, TransportFace.Blocking];
    private static readonly TransportFace[] s_async = [TransportFace.Async];

    private static readonly (string Name, RequirementLevel Level, TransportFace[] Faces)[] s_expected =
    [
        ("transport-1.redirect-not-followed", M, s_both),
        ("transport-2.no-silent-resend", M, s_both),
        ("transport-2.bodyless-not-resent", M, s_both),
        ("transport-3.cancel-is-terminal", M, s_both),
        ("transport-4.timeout-is-retryable", M, s_both),
        ("transport-5.per-call-timeout", M, s_async),
        ("transport-6.sub-resolution-timeout", S, s_both),
        ("transport-7.cancel-releases-exchange", M, s_async),
        ("transport-7.attempt-timeout-aborts", M, s_async),
        ("transport-8.internal-cancel-is-terminal", M, s_async),
        ("transport-9.settle-race-releases", M, s_async),
        ("transport-10.content-type-authoritative", M, s_both),
        ("transport-11.framing-recomputed", M, s_both),
        ("transport-11.drop-logged", S, s_async),
        ("transport-12.native-rejected-header-dropped", M, s_both),
        ("transport-13.drop-log-once-per-name", S, s_async),
        ("transport-14.value-control-dropped", M, s_both),
        ("transport-14.obs-text-preserved", S, s_both),
        ("transport-14.name-malformed-dropped", M, s_both),
        ("transport-15.borrowed-survives", M, s_async),
        ("transport-15.owned-released", M, s_async),
        ("transport-16.close-idempotent-nonblocking", M, s_both),
        ("transport-17.single-use-written-once", M, s_both),
        ("transport-18.native-resend-identical", M, s_async),
        ("transport-19.abandoned-body-unblocks", S, s_async),
        ("transport-20.no-response-is-retryable", M, s_both),
        ("transport-20.retried-by-the-pipeline", M, s_async),
        ("transport-21.pre-dispatch-failure-via-task", M, s_async),
        ("transport-22.adaptation-failure-releases", M, s_async),
        ("transport-23.never-null", M, s_async),
        ("transport-24.vendor-status-readable", M, s_both),
        ("transport-25.lazy-body", M, s_both),
        ("transport-25.large-round-trip", M, s_both),
        ("transport-25.dispose-releases", M, s_both),
        ("transport-26.bodyless-methods", M, s_both),
        ("transport-27.inbound-downgrade", S, s_both),
        ("transport-28.file-range-replayable", S, s_both),
        ("transport-29.concurrent-no-crosstalk", M, s_both),
        ("transport-30.proxy-discoverable-no-leak", S, s_async),
        ("async-1.single-non-null-response", M, s_async),
        ("async-20.late-cancel-leaves-response-open", M, s_async),
        ("seam-15.after-dispose", Y, s_both),
        ("http-39.short-source-fails", M, s_both),
    ];

    [GeneratedRegex(@"^[a-z]+-\d+\.[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    private static IReadOnlyList<ConformanceAssertion> All => TransportSuite.Assertions;

    [Fact]
    public void The_catalogue_is_the_43_named_assertions_in_the_design_order()
    {
        Assert.Equal(43, All.Count);
        Assert.Equal(s_expected.Select(e => e.Name), All.Select(a => a.Name));
        Assert.Equal(All.Count, All.Select(a => a.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Each_assertion_has_the_level_and_the_faces_of_the_design_table()
    {
        for (var i = 0; i < s_expected.Length; i++)
        {
            Assert.Equal(s_expected[i].Level, All[i].Level);
            Assert.Equal(s_expected[i].Faces, All[i].Faces);
        }
    }

    [Fact]
    public void Names_are_stable_identifiers_and_every_cited_requirement_exists_with_a_level_the_assertion_does_not_exceed()
    {
        foreach (var assertion in All)
        {
            Assert.Matches(NamePattern(), assertion.Name);
            Assert.All(assertion.RequirementIds, id => Assert.True(RequirementIndex.Contains(id), id));
            var strongest = assertion.RequirementIds.Select(id => { RequirementIndex.TryGetLevel(id, out var level); return level; }).Min();
            Assert.True(assertion.Level >= strongest, $"{assertion.Name} is {assertion.Level} but its strongest requirement is only {strongest}");
            if (assertion.Level == RequirementLevel.May)
            {
                Assert.All(assertion.RequirementIds, id =>
                {
                    RequirementIndex.TryGetLevel(id, out var level);
                    Assert.Equal(RequirementLevel.May, level);
                });
            }
        }
    }

    [Fact]
    public void Every_transport_requirement_and_the_per_transport_async_rows_are_cited()
    {
        var cited = All.SelectMany(a => a.RequirementIds).ToHashSet(StringComparer.Ordinal);
        string[] expected =
        [
            .. Enumerable.Range(1, 30).Select(n => $"TRANSPORT-{n}"),
            "ASYNC-1", "ASYNC-2", "ASYNC-15", "ASYNC-20", "ASYNC-22",
            "SEAM-11", "SEAM-12", "SEAM-13", "SEAM-15", "SEAM-16", "SEAM-30",
            "HTTP-39", "XCUT-1", "XCUT-2", "XCUT-4", "XCUT-22", "OBS-19",
        ];

        Assert.All(expected, id => Assert.Contains(id, cited));
    }

    [Fact]
    public void No_async_row_is_claimed_beyond_the_five_the_kit_proves_per_transport()
    {
        var claimed = All.SelectMany(a => a.RequirementIds).Where(id => id.StartsWith("ASYNC-", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

        Assert.Equal(s_perTransportAsyncRows.Order(StringComparer.Ordinal), claimed);
    }

    [Fact]
    public void Every_assertion_has_a_negative_control_found_without_any_test_having_run()
    {
        var uncovered = All.Select(a => a.Name).Except(ControlClasses.Covered, StringComparer.Ordinal).ToArray();

        Assert.Empty(uncovered);
    }

    [Fact]
    public void Control_classes_are_discovered_by_shape_so_a_new_one_cannot_be_missed()
    {
        var withoutCovered = ControlClasses.Classes.Where(type => type.GetProperty("Covered", BindingFlags.Public | BindingFlags.Static) is null).Select(type => type.Name);

        Assert.Empty(withoutCovered);
        Assert.True(ControlClasses.Classes.Count >= 12, "the twelve control classes of plan 2.5 to 2.11 must all be found");
    }

    [Fact]
    public void Every_control_names_an_assertion_that_exists()
    {
        var unknown = ControlClasses.Covered.Except(All.Select(a => a.Name), StringComparer.Ordinal).ToArray();

        Assert.Empty(unknown);
    }
}
