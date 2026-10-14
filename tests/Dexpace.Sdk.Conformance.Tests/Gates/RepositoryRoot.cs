// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// Finds the repository root by walking up from the test assembly to the directory holding <c>Dexpace.Sdk.sln</c> (plan 3.5, validation F13): the
/// gates of phase 10 read the repository's own files (scripts, workflow, editorconfig, sources, documents), which are not copied beside the test
/// assembly the way the specification appendices are.
/// </summary>
internal static class RepositoryRoot
{
    /// <summary>The repository root.</summary>
    /// <exception cref="DirectoryNotFoundException">No parent directory holds the solution file.</exception>
    internal static string Path { get; } = Find();

    /// <summary>The path of <paramref name="relative"/> under the repository root.</summary>
    /// <param name="relative">A repository-relative path.</param>
    internal static string Of(string relative) => System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Dexpace.Sdk.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Dexpace.Sdk.sln was not found above the test output; the repository gates cannot locate the repository.");
    }
}
