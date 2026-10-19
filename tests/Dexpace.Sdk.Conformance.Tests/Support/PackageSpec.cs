// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>One synthetic package for the packaging controls (plan 3.1): every fact the packaging suite reads, with the conforming value as the default, so a control states only what it breaks.</summary>
/// <param name="Id">The package id.</param>
internal sealed record PackageSpec(string Id)
{
    /// <summary>The package version, and the version dependencies on it require.</summary>
    internal string Version { get; init; } = "1.2.3";

    /// <summary>The <c>lib/</c> folders that hold the assembly.</summary>
    internal IReadOnlyList<string> LibFolders { get; init; } = ["net10.0"];

    /// <summary>Whether the assembly is packed at all.</summary>
    internal bool WithAssembly { get; init; } = true;

    /// <summary>The dependency groups: a target framework and its <c>(id, version)</c> dependencies.</summary>
    internal IReadOnlyList<(string Tfm, IReadOnlyList<(string Id, string Version)> Dependencies)> Groups { get; init; } = [("net10.0", [("Microsoft.Extensions.Logging.Abstractions", "10.0.12")])];

    /// <summary>The license expression, or <see langword="null"/> for none.</summary>
    internal string? License { get; init; } = "MIT";

    /// <summary>The copyright, or <see langword="null"/> for none.</summary>
    internal string? Copyright { get; init; } = "Copyright (c) 2026 test";

    /// <summary>The assembly's <c>AssemblyMetadata</c> pairs.</summary>
    internal IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string> { ["IsTrimmable"] = "True", ["IsAotCompatible"] = "True" };

    /// <summary>The assembly's informational version, or <see langword="null"/> for none; the package version when not set.</summary>
    internal string? InformationalVersion { get; init; } = "1.2.3";

    /// <summary>The assemblies the packed assembly grants its internals to.</summary>
    internal IReadOnlyList<string>? InternalsVisibleTo { get; init; }

    /// <summary>The assembly's <c>TargetFramework</c> attribute.</summary>
    internal string? TargetFramework { get; init; } = ".NETCoreApp,Version=v10.0";

    /// <summary>The internals grants, defaulting to the package's own test project.</summary>
    internal IReadOnlyList<string> Grants => InternalsVisibleTo ?? [Id + ".Tests"];

    /// <summary>The dependencies of the single default group, replaced.</summary>
    /// <param name="dependencies">The dependencies.</param>
    internal PackageSpec DependingOn(params (string Id, string Version)[] dependencies) => this with { Groups = [("net10.0", dependencies)] };
}
