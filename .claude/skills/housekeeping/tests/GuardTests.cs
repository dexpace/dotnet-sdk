// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Housekeeping.Tests;

/// <summary>
/// The guard is the one part of this skill that must not be wrong, because everything it protects is a
/// document no other copy of exists. These cases are the four ways a naive <c>StartsWith</c>
/// implementation fails — sibling prefix, <c>..</c> traversal, absolute path and symlink — plus proof
/// that the mutable half stays mutable.
/// </summary>
public sealed class GuardTests
{
    // A root that does not exist, so the lexical half is exercised without the filesystem.
    private static readonly string s_fakeRoot = Path.Combine(Path.GetTempPath(), "housekeeping-guard-no-such-repo");

    [Fact]
    public void Every_frozen_entry_is_itself_refused()
    {
        foreach (var entry in Guard.Frozen)
        {
            Assert.Equal(entry, Guard.FrozenEntryFor(entry, s_fakeRoot));
        }
    }

    [Fact]
    public void A_file_under_a_frozen_tree_is_refused_at_any_depth()
    {
        Assert.Equal("docs/product-spec", Guard.FrozenEntryFor("docs/product-spec/04-core.md", s_fakeRoot));
        Assert.Equal("docs/knowledge", Guard.FrozenEntryFor("docs/knowledge/harvested/documentation.md", s_fakeRoot));
        Assert.Equal("docs/knowledge", Guard.FrozenEntryFor("docs/knowledge/notes/pagination.md", s_fakeRoot));
        Assert.Equal("docs/sdk-design-dotnet", Guard.FrozenEntryFor("docs/sdk-design-dotnet/10-deviations.md", s_fakeRoot));
        Assert.Equal("docs/styleguide", Guard.FrozenEntryFor("docs/styleguide/csharp/09-concurrency.md", s_fakeRoot));
    }

    // Bypass 1. The failure a raw string prefix test would produce, and the reason the comparison
    // is segment-wise.
    [Theory]
    [InlineData("docs/product-specs/04-core.md")]
    [InlineData("docs/product-spec-draft/04-core.md")]
    [InlineData("docs/knowledge-notes.md")]
    [InlineData("docs/sdk-design-dotnet-old/01.md")]
    [InlineData("docs/styleguides/x.md")]
    [InlineData("docs/product-spec.md.bak")]
    public void A_sibling_whose_name_merely_starts_with_a_frozen_one_is_writable(string path) =>
        Assert.False(Guard.IsFrozen(path, s_fakeRoot));

    // Bypass 2. A path that spells its way in must not spell its way past the check.
    [Theory]
    [InlineData("docs/work/../product-spec/04-core.md", "docs/product-spec")]
    [InlineData("docs/sdk-documentation/../knowledge/x.md", "docs/knowledge")]
    [InlineData("docs/work/mvp/../../product-spec/x.md", "docs/product-spec")]
    [InlineData("docs/work/../styleguide/README.md", "docs/styleguide")]
    public void A_dotdot_segment_that_lands_inside_is_refused(string path, string entry) =>
        Assert.Equal(entry, Guard.FrozenEntryFor(path, s_fakeRoot));

    [Fact]
    public void A_dotdot_segment_that_escapes_upward_is_not_mistaken_for_containment()
    {
        Assert.False(Guard.IsFrozen("docs/product-spec/../work/mvp/phase1/x.md", s_fakeRoot));
        Assert.False(Guard.IsFrozen("docs/knowledge/../README.md", s_fakeRoot));
    }

    // Bypass 3. An absolute path is resolved against nothing; it is already resolved.
    [Fact]
    public void An_absolute_path_is_resolved_not_treated_as_relative()
    {
        Assert.Equal("docs/product-spec", Guard.FrozenEntryFor(Path.Combine(s_fakeRoot, "docs/product-spec/04.md"), s_fakeRoot));
        // Outside the repository is nobody's business, but is certainly not frozen.
        Assert.False(Guard.IsFrozen(Path.Combine(Path.GetTempPath(), "elsewhere/docs/product-spec/04.md"), s_fakeRoot));
        Assert.False(Guard.IsFrozen("/docs/knowledge/x.md", s_fakeRoot));
    }

    // Bypass 4. Lexically `docs/work/...` escapes every frozen entry; physically it lands inside
    // `docs/product-spec`, and Directory.CreateDirectory follows the link, so a purely lexical guard
    // says yes and `git mv` writes into the normative tree.
    [Fact]
    public void A_symlink_into_a_frozen_tree_is_refused()
    {
        var root = TempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "docs/product-spec"));
            Fixture.Symlink(root, "docs/product-spec", "docs/work");

            Assert.Equal("docs/product-spec", Guard.FrozenEntryFor("docs/work/mvp/phase9/x.md", root));
            Assert.Throws<FrozenPathException>(() => Guard.AssertWritable("docs/work/mvp/phase9/x.md", root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // The same bypass one level up: the link is an INTERMEDIATE component, which the BCL's
    // ResolveLinkTarget does not see on its own.
    [Fact]
    public void A_symlinked_ancestor_directory_is_resolved_too()
    {
        var root = TempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "docs/styleguide/csharp"));
            Fixture.Symlink(root, "docs/styleguide", "docs/notes");

            Assert.Equal("docs/styleguide", Guard.FrozenEntryFor("docs/notes/csharp/new/x.md", root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_real_docs_work_directory_stays_writable()
    {
        // The other half: resolving symlinks must not make the ordinary tree frozen.
        var root = TempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "docs/product-spec"));
            Directory.CreateDirectory(Path.Combine(root, "docs/work/mvp/phase9"));

            Assert.False(Guard.IsFrozen("docs/work/mvp/phase9/x.md", root));
            Assert.True(Guard.IsFrozen("docs/product-spec/04.md", root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_target_whose_ancestors_do_not_exist_yet_is_still_judged()
    {
        // The normal case for a move: nothing at the destination.
        var root = TempRoot();
        try
        {
            Assert.Equal("docs/product-spec", Guard.FrozenEntryFor("docs/product-spec/new/deep/x.md", root));
            Assert.False(Guard.IsFrozen("docs/work/mvp/phase1/x.md", root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("docs/README.md")]
    [InlineData("docs/architecture.md")]
    [InlineData("docs/work/mvp/phase1/2026-09-05-phase1-core-design.md")]
    [InlineData("docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-plan.md")]
    [InlineData("docs/superpowers/specs/2026-09-05-x-design.md")]
    [InlineData("docs/assets/dexpace-wordmark-dark.svg")]
    [InlineData("CLAUDE.md")]
    [InlineData("README.md")]
    [InlineData("CHANGELOG.md")]
    [InlineData("src/Dexpace.Sdk.Core/README.md")]
    public void Everything_the_skill_is_allowed_to_write_stays_writable(string path)
    {
        Assert.False(Guard.IsFrozen(path, s_fakeRoot));
        Assert.Equal(path, Guard.AssertWritable(path, s_fakeRoot));
    }

    [Fact]
    public void AssertWritable_throws_a_frozen_path_exception_naming_both_paths()
    {
        var error = Assert.Throws<FrozenPathException>(() => Guard.AssertWritable("docs/product-spec/04-core.md", s_fakeRoot));

        Assert.Equal("docs/product-spec/04-core.md", error.PathName);
        Assert.Equal("docs/product-spec", error.Entry);
        Assert.Contains("refusing to write", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertAllWritable_refuses_the_whole_batch()
    {
        Assert.Throws<FrozenPathException>(
            () => Guard.AssertAllWritable(["docs/README.md", "docs/product-spec/04-core.md", "CLAUDE.md"], s_fakeRoot));
        Assert.Equal(["docs/README.md", "CLAUDE.md"], Guard.AssertAllWritable(["docs/README.md", "CLAUDE.md"], s_fakeRoot));
    }

    [Fact]
    public void The_frozen_list_is_pinned()
    {
        // Widening this list is a decision about what a maintenance tool may edit. It has to be a
        // reviewed diff here rather than a silent constant change.
        Assert.Equal(
            [
                "docs/knowledge",
                "docs/product-spec",
                "docs/sdk-design-dotnet",
                "docs/styleguide",
                "docs/product-spec.md",
                "docs/sdk-design-dotnet.md",
            ],
            Guard.Frozen);
    }

    [Fact]
    public void FrozenSymlinks_reports_a_frozen_entry_that_became_a_link()
    {
        var root = TempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "elsewhere"));
            Fixture.Symlink(root, "elsewhere", "docs/knowledge");

            Assert.Equal(["docs/knowledge"], Guard.FrozenSymlinks(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_real_fixture_tree_has_no_frozen_symlinks()
    {
        using var fixture = Fixture.Create();

        Assert.Empty(Guard.FrozenSymlinks(fixture.Root));
    }

    private static string TempRoot() => Guard.RealPath(Directory.CreateTempSubdirectory("housekeeping-guard-").FullName);
}
