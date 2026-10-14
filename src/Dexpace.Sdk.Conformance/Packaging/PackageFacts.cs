// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>What the packaging suite reads from one <c>.nupkg</c> (phase 10, P10-10): the nuspec's metadata and dependency groups, the <c>lib/</c> folders, and the packed assembly's attributes.</summary>
/// <param name="Path">The file the facts were read from.</param>
/// <param name="Id">The package id.</param>
/// <param name="Version">The package version.</param>
/// <param name="License">The nuspec's license expression, or <see langword="null"/>.</param>
/// <param name="Copyright">The nuspec's copyright, or <see langword="null"/>.</param>
/// <param name="DependencyGroups">The nuspec's dependency groups, each a target framework and its dependencies.</param>
/// <param name="LibFolders">The target framework monikers of the <c>lib/</c> folders that hold an assembly.</param>
/// <param name="Assembly">The facts of the package's main assembly (the one named for the package id), or <see langword="null"/> when it is not packed.</param>
internal sealed record PackageFacts(
    string Path,
    string Id,
    string Version,
    string? License,
    string? Copyright,
    IReadOnlyList<DependencyGroup> DependencyGroups,
    IReadOnlyList<string> LibFolders,
    AssemblyFacts? Assembly);

/// <summary>One <c>&lt;group&gt;</c> of a nuspec's dependencies.</summary>
/// <param name="TargetFramework">The group's target framework moniker, empty for a group with none.</param>
/// <param name="Dependencies">The dependencies in the group.</param>
internal sealed record DependencyGroup(string TargetFramework, IReadOnlyList<PackageDependency> Dependencies);

/// <summary>One dependency of a nuspec group.</summary>
/// <param name="Id">The dependency's package id.</param>
/// <param name="Version">The version range as the nuspec writes it.</param>
internal sealed record PackageDependency(string Id, string Version);

/// <summary>The attributes of a packed assembly the suite judges, read from its metadata and never by loading it.</summary>
/// <param name="AssemblyMetadata">The <c>AssemblyMetadata(key, value)</c> pairs.</param>
/// <param name="InformationalVersion">The <c>AssemblyInformationalVersion</c>, or <see langword="null"/>.</param>
/// <param name="InternalsVisibleTo">The assemblies named by <c>InternalsVisibleTo</c>.</param>
/// <param name="TargetFramework">The <c>TargetFramework</c> attribute's framework name, or <see langword="null"/>.</param>
internal sealed record AssemblyFacts(
    IReadOnlyDictionary<string, string> AssemblyMetadata,
    string? InformationalVersion,
    IReadOnlyList<string> InternalsVisibleTo,
    string? TargetFramework);
