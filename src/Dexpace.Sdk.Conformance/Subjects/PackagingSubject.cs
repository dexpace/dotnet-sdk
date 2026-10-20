// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The packages the packaging suite judges (phase 10, P10-10): the <c>.nupkg</c> files a pack produced, which package is the core one, what each
/// package may depend on, and the one target framework every package declares. The suite reads the published artefacts, not the build output, so a
/// package built from another tree is a different subject.
/// </summary>
public sealed record PackagingSubject
{
    /// <summary>The name the report carries.</summary>
    public string Name
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = "packages";

    /// <summary>The <c>.nupkg</c> files to judge. Copied. Required: an empty list is an error the suite reports.</summary>
    public IReadOnlyList<string> PackagePaths
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(PackagePaths));
            field = [.. value];
        }
    } = [];

    /// <summary>The id of the core package, whose only dependency may be the logging facade (NFR-1). <c>Dexpace.Sdk.Core</c> by default.</summary>
    public string CoreId
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = "Dexpace.Sdk.Core";

    /// <summary>What each non-core package may depend on, by package id. A package with no entry may depend on core and the facade only. Copied.</summary>
    public IReadOnlyDictionary<string, PackagePolicy> Policies
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Policies));
            field = new Dictionary<string, PackagePolicy>(value, StringComparer.Ordinal);
        }
    } = new Dictionary<string, PackagePolicy>(StringComparer.Ordinal);

    /// <summary>The target framework moniker every package declares, once (NFR-10, roadmap decision D1). <c>net10.0</c> by default.</summary>
    public string DeclaredFloor
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = "net10.0";
}
