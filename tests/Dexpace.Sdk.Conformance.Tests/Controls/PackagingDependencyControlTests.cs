// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>nfr-1</c> and <c>nfr-2</c> (plan 3.2), over synthetic packages.</summary>
[Trait("Category", "Unit")]
public sealed class PackagingDependencyControlTests
{
    private const string Core = "Dexpace.Sdk.Core";
    private const string DependencyInjection = "Dexpace.Sdk.Extensions.DependencyInjection";
    private const string Facade = "Microsoft.Extensions.Logging.Abstractions";

    internal static IReadOnlyList<PackagingControlRow> Rows { get; } =
    [
        new("nfr-1.core-dependencies", "core carries an extra concrete dependency", set => PackagingControlRow.Edit(set, Core, c => c.DependingOn((Facade, "10.0.12"), ("Newtonsoft.Json", "13.0.3"))), "exactly the logging facade"),
        new("nfr-1.core-dependencies", "core carries a second dependency group", set => PackagingControlRow.Edit(set, Core, c => c with { Groups = [.. c.Groups, ("net8.0", [(Facade, "8.0.0")])] }), "dependency groups"),
        new("nfr-1.core-dependencies", "core is missing the facade", set => PackagingControlRow.Edit(set, Core, c => c.DependingOn()), "exactly the logging facade"),
        new("nfr-1.core-dependencies", "core's group targets another framework", set => PackagingControlRow.Edit(set, Core, c => c with { Groups = [("net8.0", [(Facade, "10.0.12")])] }), "target framework"),
        new("nfr-2.adapter-budget", "an adapter brings a second third-party root", set => PackagingControlRow.Edit(set, DependencyInjection, p => p.DependingOn((Core, "1.2.3"), ("Dexpace.Sdk.Http.SystemNet", "1.2.3"), ("Microsoft.Extensions.Http", "10.0.12"), ("Polly", "8.0.0"), (Facade, "10.0.12"))), "third-party root beyond"),
        new("nfr-2.adapter-budget", "an adapter depends on a first-party package its policy does not name", set => PackagingControlRow.Edit(set, "Dexpace.Sdk.Http.SystemNet", p => p.DependingOn((Core, "1.2.3"), ("Dexpace.Sdk.Serialization.SystemTextJson", "1.2.3"), (Facade, "10.0.12"))), "its policy does not name"),
        new("nfr-2.adapter-budget", "an adapter requires core at another version", set => PackagingControlRow.Edit(set, "Dexpace.Sdk.Conformance", p => p.DependingOn((Core, "9.9.9"), (Facade, "10.0.12"))), "lock-stepped"),
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
