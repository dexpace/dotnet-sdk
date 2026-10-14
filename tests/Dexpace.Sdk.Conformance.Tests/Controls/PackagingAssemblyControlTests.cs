// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>nfr-3</c>, <c>nfr-8</c> and <c>nfr-10</c> (plan 3.3), over synthetic packages with emitted assemblies.</summary>
[Trait("Category", "Unit")]
public sealed class PackagingAssemblyControlTests
{
    private const string Core = "Dexpace.Sdk.Core";

    internal static IReadOnlyList<PackagingControlRow> Rows { get; } =
    [
        new("nfr-3.no-internals-granted", "an assembly grants its internals to another assembly", set => PackagingControlRow.Edit(set, Core, p => p with { InternalsVisibleTo = [Core + ".Tests", "Other.Assembly"] }), "grants its internals"),
        new("nfr-3.no-internals-granted", "an assembly grants its internals to a sibling's tests", set => PackagingControlRow.Edit(set, Core, p => p with { InternalsVisibleTo = ["Dexpace.Sdk.Http.SystemNet.Tests"] }), "grants its internals"),
        new("nfr-8.trim-and-aot-declared", "an assembly does not declare IsTrimmable", set => PackagingControlRow.Edit(set, Core, p => p with { Metadata = new Dictionary<string, string> { ["IsAotCompatible"] = "True" } }), "IsTrimmable"),
        new("nfr-8.trim-and-aot-declared", "an assembly declares IsAotCompatible False", set => PackagingControlRow.Edit(set, Core, p => p with { Metadata = new Dictionary<string, string> { ["IsTrimmable"] = "True", ["IsAotCompatible"] = "False" } }), "IsAotCompatible"),
        new("nfr-10.single-floor", "a package holds two lib folders", set => PackagingControlRow.Edit(set, Core, p => p with { LibFolders = ["net10.0", "net8.0"] }), "lib folders"),
        new("nfr-10.single-floor", "a package declares two dependency groups", set => PackagingControlRow.Edit(set, Core, p => p with { Groups = [.. p.Groups, ("net8.0", [])] }), "dependency groups"),
        new("nfr-10.single-floor", "an assembly targets another framework", set => PackagingControlRow.Edit(set, Core, p => p with { TargetFramework = ".NETCoreApp,Version=v8.0" }), "TargetFramework attribute"),
        new("nfr-10.single-floor", "a package packs its assembly under another floor", set => PackagingControlRow.Edit(set, Core, p => p with { LibFolders = ["net8.0"] }), "lib folders"),
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
