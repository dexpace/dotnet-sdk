// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Tools.Knowledge.Tests;

/// <summary>
/// Printed paths are repo-relative with <c>/</c> on every host. The Windows cases run everywhere through the
/// separator-explicit <see cref="KnowledgePaths.RelativeTo"/>, so a regression that only Windows CI would see
/// (an absolute <c>C:\...</c> path leaking into a report) fails on Linux too.
/// </summary>
public sealed class KnowledgePathsTests
{
    [Theory]
    [InlineData(@"C:\repo", @"C:\repo\docs\product-spec\appendix-c.md", "docs/product-spec/appendix-c.md")]
    [InlineData(@"C:\repo\", @"C:\repo\docs\work\mvp\phase1\x.md", "docs/work/mvp/phase1/x.md")]
    [InlineData(@"C:\repo", @"C:\repo/docs/work/mixed.md", "docs/work/mixed.md")]
    [InlineData(@"C:\repo", @"C:\repository\docs\x.md", @"C:\repository\docs\x.md")]
    [InlineData(@"C:\repo", @"D:\elsewhere\x.md", @"D:\elsewhere\x.md")]
    public void RelativeTo_WindowsPaths_IsRepoRelativeWithForwardSlashes(string root, string path, string expected) =>
        Assert.Equal(expected, KnowledgePaths.RelativeTo(root, path, '\\'));

    [Theory]
    [InlineData("/repo", "/repo/docs/knowledge/harvested/INDEX.md", "docs/knowledge/harvested/INDEX.md")]
    [InlineData("/repo/", "/repo/docs/x.md", "docs/x.md")]
    [InlineData("/repo", "/repository/docs/x.md", "/repository/docs/x.md")]
    [InlineData("/repo", "/elsewhere/x.md", "/elsewhere/x.md")]
    public void RelativeTo_PosixPaths_IsRepoRelative(string root, string path, string expected) =>
        Assert.Equal(expected, KnowledgePaths.RelativeTo(root, path, '/'));

    [Fact]
    public void Relative_OfAPathBuiltWithTheHostSeparator_UsesForwardSlashes()
    {
        var paths = new KnowledgePaths(Path.GetTempPath());

        Assert.Equal(
            "docs/product-spec/appendix-c-consolidated-normative-requirement-index.md",
            paths.Relative(paths.AppendixC));
        Assert.Equal("docs/work", paths.Relative(paths.WorkDir));
    }

    [Fact]
    public void Resolve_KeepsAHostAbsolutePath_AndRootsARelativeOne()
    {
        var paths = new KnowledgePaths(Path.GetTempPath());
        var absolute = Path.Combine(Path.GetTempPath(), "sibling", "styleguide.md");

        Assert.Equal(absolute, paths.Resolve(absolute));
        Assert.Equal(Path.Combine(paths.Root, "docs/x.md"), paths.Resolve("docs/x.md"));
        Assert.Equal("docs/x.md", paths.Relative(paths.Resolve("docs/x.md")));
    }
}
