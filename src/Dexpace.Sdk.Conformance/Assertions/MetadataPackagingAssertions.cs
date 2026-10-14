// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/packaging_suite/ (gemspec metadata and the version constant).
using System.Globalization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>NFR-13</c>, <c>NFR-14</c> and <c>NFR-15</c> over the published metadata: the license and copyright every package declares (the package-level face
/// of the header rule), one version for every package and every dependency between them, and a version the library can read back at run time.
/// </summary>
internal static class MetadataPackagingAssertions
{
    internal static ConformanceAssertion[] Assertions() =>
    [
        PackagingAssertionBuilder.Define("nfr-13.license-declared", ["NFR-13"], RequirementLevel.Should, LicenseDeclared),
        PackagingAssertionBuilder.Define("nfr-14.lockstep-version", ["NFR-14"], RequirementLevel.Should, LockstepVersion),
        PackagingAssertionBuilder.Define("nfr-15.version-resolvable", ["NFR-15"], RequirementLevel.Should, VersionResolvable),
    ];

    // A license expression and a copyright, the same in every package.
    private static void LicenseDeclared(PackagingContext context)
    {
        foreach (var package in context.Packages)
        {
            Check.True(!string.IsNullOrWhiteSpace(package.License), $"'{package.Id}' declares no license expression", "a license expression", "none");
            Check.True(!string.IsNullOrWhiteSpace(package.Copyright), $"'{package.Id}' declares no copyright", "a copyright", "none");
        }

        var first = context.Packages[0];
        foreach (var package in context.Packages)
        {
            Check.Equal(package.License, first.License, $"the license expression of '{package.Id}' (equal across packages)");
            Check.Equal(package.Copyright, first.Copyright, $"the copyright of '{package.Id}' (equal across packages)");
        }
    }

    // One version for every package, and every dependency between two of them requires that version.
    private static void LockstepVersion(PackagingContext context)
    {
        var version = context.Packages[0].Version;
        var ids = context.Packages.Select(package => package.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var package in context.Packages)
        {
            Check.Equal(package.Version, version, $"the version of '{package.Id}' (every package ships at one version)");
            foreach (var dependency in package.DependencyGroups.SelectMany(g => g.Dependencies).Where(d => ids.Contains(d.Id)))
            {
                Check.Equal(dependency.Version, version, $"the version '{package.Id}' requires of '{dependency.Id}'");
            }
        }
    }

    // The informational version, without its "+" build metadata, is the package's version, and is never a placeholder.
    private static void VersionResolvable(PackagingContext context)
    {
        foreach (var package in context.Packages)
        {
            var informational = package.Assembly?.InformationalVersion
                ?? throw new ConformanceException($"the assembly in '{package.Id}' carries no AssemblyInformationalVersion", "an informational version", "none");
            var version = informational.Split('+')[0];
            Check.True(
                !string.Equals(version, "unknown", StringComparison.OrdinalIgnoreCase) && !version.StartsWith("0.0.0", StringComparison.Ordinal),
                $"the informational version of '{package.Id}' is a placeholder",
                "a real version",
                informational);
            Check.Equal(version, package.Version, string.Create(CultureInfo.InvariantCulture, $"the informational version of '{package.Id}' (without its + metadata) against the nuspec version"));
        }
    }
}
