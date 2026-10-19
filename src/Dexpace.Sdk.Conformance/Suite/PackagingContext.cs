// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>What one run of one packaging assertion owns: the subject, and the facts read from its packages (shared by a whole run, read once).</summary>
/// <param name="subject">The packages under test.</param>
/// <param name="packages">The facts, loaded on first use; a missing or corrupt package surfaces when it is first read.</param>
internal sealed class PackagingContext(PackagingSubject subject, Lazy<IReadOnlyList<PackageFacts>> packages)
{
    /// <summary>The packages under test.</summary>
    internal PackagingSubject Subject { get; } = subject;

    /// <summary>The facts of every package, in the order the subject lists them.</summary>
    internal IReadOnlyList<PackageFacts> Packages => packages.Value;

    /// <summary>The facts of the core package.</summary>
    /// <exception cref="ConformanceException">The subject lists no package with the core id.</exception>
    internal PackageFacts Core =>
        Packages.FirstOrDefault(package => string.Equals(package.Id, Subject.CoreId, StringComparison.Ordinal))
        ?? throw new ConformanceException($"the subject lists no package '{Subject.CoreId}'", Subject.CoreId, string.Join(", ", Packages.Select(package => package.Id)));

    /// <summary>The packages other than core.</summary>
    internal IEnumerable<PackageFacts> Adapters => Packages.Where(package => !string.Equals(package.Id, Subject.CoreId, StringComparison.Ordinal));

    /// <summary>Loads the facts of every path of <paramref name="subject"/>.</summary>
    /// <param name="subject">The packages to read.</param>
    /// <exception cref="ConformanceException">The subject lists no package.</exception>
    internal static IReadOnlyList<PackageFacts> Load(PackagingSubject subject)
    {
        if (subject.PackagePaths.Count == 0)
        {
            throw new ConformanceException("the packaging subject lists no package to judge", "at least one .nupkg", "none");
        }

        return [.. subject.PackagePaths.Select(NupkgReader.Read)];
    }
}
