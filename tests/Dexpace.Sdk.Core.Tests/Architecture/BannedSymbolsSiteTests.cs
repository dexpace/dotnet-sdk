// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// Design §5.4 / §8.1, P6c-25: <c>ExecutionContext.SuppressFlow</c> appears in exactly one source file, the background-work
/// helper that <c>BannedSymbols.txt</c> reserves it for. Phase 10 (P10-18) adds the two <c>XCUT</c> gates the same way:
/// <c>ConcurrentDictionary</c> is constructed only in <c>BoundedMap</c> (<c>XCUT-14</c>) and <c>Exception.GetBaseException</c>
/// is used nowhere (<c>XCUT-9</c>). The build is the gate (<c>RS0030</c>); these are source scans with a self-test that proves they can fail.
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
    public void ConcurrentDictionary_is_constructed_only_in_BoundedMap()
    {
        // XCUT-14, V13: a process- or instance-lived concurrent map is a BoundedMap or it does not land. The constructors are banned (RS0030), so
        // the build already refuses a new one; this scan keeps the type itself out of every other file of every library, target-typed new included.
        var files = FilesContaining("ConcurrentDictionary", SourceTreeRoot());

        Assert.Equal(["Dexpace.Sdk.Core/Internal/BoundedMap.cs"], files);
    }

    [Fact]
    public void The_one_construction_of_ConcurrentDictionary_sits_in_a_scoped_pragma_with_a_why_comment()
    {
        // The constructor ban needs exactly one exemption (the construction at BoundedMap's constructor), beside the existing one for the value-comparing TryRemove.
        var lines = File.ReadAllLines(Path.Combine(SourceRoot(), "Internal", "BoundedMap.cs"));
        var construction = Array.FindIndex(lines, static line => line.Contains("new ConcurrentDictionary<", StringComparison.Ordinal));

        Assert.True(construction > 0);
        Assert.StartsWith("#pragma warning disable RS0030 //", lines[construction - 1], StringComparison.Ordinal);
        Assert.Contains("XCUT-14", lines[construction - 1], StringComparison.Ordinal);
        Assert.Equal("#pragma warning restore RS0030", lines[construction + 1]);
        Assert.Equal(2, lines.Count(static line => line.Contains("#pragma warning disable RS0030", StringComparison.Ordinal)));
    }

    [Fact]
    public void GetBaseException_appears_nowhere_in_src()
    {
        // XCUT-9, V14: the cause walk behind the Windows test-host hang. Every walk goes through ExceptionFacts.EnumerateCauses.
        Assert.Empty(FilesContaining("GetBaseException", SourceTreeRoot()));
    }

    [Fact]
    public void The_scanner_can_fail_for_the_two_xcut_tokens()
    {
        var directory = Directory.CreateTempSubdirectory("scan");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "a.cs"), "var map = new ConcurrentDictionary<int, int>();");
            File.WriteAllText(Path.Combine(directory.FullName, "b.cs"), "var root = failure.GetBaseException();");
            File.WriteAllText(Path.Combine(directory.FullName, "c.cs"), "nothing here");

            Assert.Equal(["a.cs"], FilesContaining("ConcurrentDictionary", directory.FullName));
            Assert.Equal(["b.cs"], FilesContaining("GetBaseException", directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
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

    private static string SourceTreeRoot() => Path.GetDirectoryName(SourceRoot())!;

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
