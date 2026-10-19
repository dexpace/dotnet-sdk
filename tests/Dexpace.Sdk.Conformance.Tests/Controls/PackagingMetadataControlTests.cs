// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>nfr-13</c>, <c>nfr-14</c> and <c>nfr-15</c> (plan 3.4), over synthetic packages.</summary>
[Trait("Category", "Unit")]
public sealed class PackagingMetadataControlTests
{
    private const string Core = "Dexpace.Sdk.Core";
    private const string Conformance = "Dexpace.Sdk.Conformance";

    internal static IReadOnlyList<PackagingControlRow> Rows { get; } =
    [
        new("nfr-13.license-declared", "a package declares no license expression", set => PackagingControlRow.Edit(set, Core, p => p with { License = null }), "no license expression"),
        new("nfr-13.license-declared", "a package declares no copyright", set => PackagingControlRow.Edit(set, Core, p => p with { Copyright = null }), "no copyright"),
        new("nfr-13.license-declared", "a package's copyright differs from its siblings'", set => PackagingControlRow.Edit(set, Conformance, p => p with { Copyright = "Copyright (c) 2025 someone else" }), "equal across packages"),
        new("nfr-13.license-declared", "a package's license differs from its siblings'", set => PackagingControlRow.Edit(set, Conformance, p => p with { License = "Apache-2.0" }), "equal across packages"),
        new("nfr-14.lockstep-version", "a package ships at another version", set => PackagingControlRow.Edit(set, Conformance, p => p with { Version = "1.2.4", InformationalVersion = "1.2.4" }), "one version"),
        new("nfr-14.lockstep-version", "a package requires a sibling at another version", set => PackagingControlRow.Edit(set, Conformance, p => p.DependingOn((Core, "1.2.2"), ("Microsoft.Extensions.Logging.Abstractions", "10.0.12"))), "requires of"),
        new("nfr-15.version-resolvable", "the informational version is unknown", set => PackagingControlRow.Edit(set, Core, p => p with { InformationalVersion = "unknown" }), "placeholder"),
        new("nfr-15.version-resolvable", "the informational version is 0.0.0", set => PackagingControlRow.Edit(set, Core, p => p with { InformationalVersion = "0.0.0+local" }), "placeholder"),
        new("nfr-15.version-resolvable", "the informational version differs from the nuspec version", set => PackagingControlRow.Edit(set, Core, p => p with { InformationalVersion = "1.2.4+abc" }), "against the nuspec version"),
        new("nfr-15.version-resolvable", "the assembly carries no informational version", set => PackagingControlRow.Edit(set, Core, p => p with { InformationalVersion = null }), "no AssemblyInformationalVersion"),
    ];

    public static IReadOnlyCollection<string> Covered => PackagingControlRow.CoveredBy(Rows);

    public static TheoryData<string, string> Cases => PackagingControlRow.Cases(Rows);

    public static TheoryData<string> PositiveCases => PackagingControlRow.PositiveCases(Rows);

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_broken_package_set_is_caught(string assertion, string control) => PackagingControlRow.Run(Rows, assertion, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public void The_conforming_set_passes(string assertion) => PackagingControlRow.RunPositive(assertion);
}
