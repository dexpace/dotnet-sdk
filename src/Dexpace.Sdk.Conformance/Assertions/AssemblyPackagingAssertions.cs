// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/packaging_suite/ (declarations the artefact carries), over .NET
// assemblies: the trim and AOT declarations are the packed assembly's own AssemblyMetadata (design §9.2, V6).
using System.Text.RegularExpressions;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>NFR-3</c>, <c>NFR-8</c> (with <c>NFR-9</c>'s rule-file clause) and <c>NFR-10</c> over the packed assemblies: nothing grants its internals to
/// another assembly than its own test project, every library declares itself trimmable and AOT-compatible, and every package targets the one floor.
/// </summary>
internal static partial class AssemblyPackagingAssertions
{
    internal static ConformanceAssertion[] Assertions() =>
    [
        PackagingAssertionBuilder.Define("nfr-3.no-internals-granted", ["NFR-3"], RequirementLevel.Should, NoInternalsGranted),
        PackagingAssertionBuilder.Define("nfr-8.trim-and-aot-declared", ["NFR-8", "NFR-9"], RequirementLevel.Must, TrimAndAotDeclared),
        PackagingAssertionBuilder.Define("nfr-10.single-floor", ["NFR-10"], RequirementLevel.Must, SingleFloor),
    ];

    [GeneratedRegex(@"^net(\d+)\.(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Moniker();

    // Every InternalsVisibleTo names the package's own test project.
    private static void NoInternalsGranted(PackagingContext context)
    {
        foreach (var package in context.Packages)
        {
            var assembly = Library(package);
            foreach (var granted in assembly.InternalsVisibleTo)
            {
                var name = granted.Split(',')[0].Trim();
                Check.Equal(name, package.Id + ".Tests", $"the assembly '{package.Id}' grants its internals to (only its own test project may see them)");
            }
        }
    }

    // The keep configuration is the declaration in the assembly: IsTrimmable and IsAotCompatible, both True (design §9.2: the rule files are
    // metadata carried in the assembly, and the trim and AOT analyzers ran at build).
    private static void TrimAndAotDeclared(PackagingContext context)
    {
        foreach (var package in context.Packages)
        {
            var metadata = Library(package).AssemblyMetadata;
            foreach (var key in new[] { "IsTrimmable", "IsAotCompatible" })
            {
                metadata.TryGetValue(key, out var value);
                Check.Equal(value ?? "(absent)", "True", $"the AssemblyMetadata '{key}' of '{package.Id}'");
            }
        }
    }

    // One lib folder, one dependency group and one TargetFramework attribute, all the declared floor.
    private static void SingleFloor(PackagingContext context)
    {
        var floor = context.Subject.DeclaredFloor;
        var expectedAttribute = FrameworkName(floor);
        foreach (var package in context.Packages)
        {
            Check.SequenceEqual(package.LibFolders, [floor], $"the lib folders of '{package.Id}' (one, the declared floor)");
            Check.True(package.DependencyGroups.Count <= 1, $"'{package.Id}' declares {package.DependencyGroups.Count} dependency groups", "at most one group", package.DependencyGroups.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var group in package.DependencyGroups)
            {
                Check.Equal(group.TargetFramework, floor, $"the target framework of the dependency group of '{package.Id}'");
            }

            Check.Equal(Library(package).TargetFramework ?? "(absent)", expectedAttribute, $"the TargetFramework attribute of the assembly in '{package.Id}'");
        }
    }

    private static AssemblyFacts Library(PackageFacts package) =>
        package.Assembly ?? throw new ConformanceException($"'{package.Id}' packs no assembly named '{package.Id}.dll'", $"lib/<tfm>/{package.Id}.dll", "none");

    private static string FrameworkName(string moniker)
    {
        var match = Moniker().Match(moniker);
        return match.Success
            ? $".NETCoreApp,Version=v{match.Groups[1].Value}.{match.Groups[2].Value}"
            : throw new ConformanceException($"the declared floor '{moniker}' is not a netX.Y moniker the suite can map to a TargetFramework attribute");
    }
}
