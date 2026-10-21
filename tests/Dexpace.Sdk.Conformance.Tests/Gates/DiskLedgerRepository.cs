// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>The repository on disk, rooted where <see cref="RepositoryRoot"/> finds it.</summary>
internal sealed class DiskLedgerRepository : ILedgerRepository
{
    /// <inheritdoc/>
    public string? ReadText(string relative)
    {
        var path = RepositoryRoot.Of(relative);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <inheritdoc/>
    public IEnumerable<string> Files(string directory, IReadOnlyCollection<string> extensions)
    {
        var root = RepositoryRoot.Of(directory);
        if (!Directory.Exists(root))
        {
            yield break;
        }

        var separator = Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                || file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                || !extensions.Any(extension => file.EndsWith(extension, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return Path.GetRelativePath(RepositoryRoot.Path, file).Replace('\\', '/');
        }
    }
}
