// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// Design §5.4 / §8.1, P6c-25: <c>ExecutionContext.SuppressFlow</c> appears in exactly one source file, the background-work
/// helper that <c>BannedSymbols.txt</c> reserves it for. A source scan, with a self-test that proves it can fail.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BannedSymbolsSiteTests
{
    [Fact]
    public void SuppressFlow_appears_only_in_BackgroundWork()
    {
        var files = FilesContaining("SuppressFlow", SourceRoot());

        Assert.Equal(["Internal/BackgroundWork.cs"], files);
    }

    [Fact]
    public void The_scanner_can_fail()
    {
        var directory = Directory.CreateTempSubdirectory("scan");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "a.cs"), "ExecutionContext.SuppressFlow();");
            File.WriteAllText(Path.Combine(directory.FullName, "b.cs"), "// SuppressFlow again");
            File.WriteAllText(Path.Combine(directory.FullName, "c.cs"), "nothing here");

            Assert.Equal(["a.cs", "b.cs"], FilesContaining("SuppressFlow", directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static List<string> FilesContaining(string token, string root) =>
        [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains(token, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

    private static string SourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Dexpace.Sdk.Core");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The src/Dexpace.Sdk.Core tree was not found above the test output.");
    }
}
