// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace KnowledgeHarvest.Tests;

public sealed class SectionsTests
{
    [Fact]
    public void Sections_tile_the_whole_file_including_the_preamble()
    {
        var sections = Sections.Parse("intro line\n\n## First\nbody\n\n## Second\nmore\n");
        Assert.Equal([null, "First", "Second"], sections.Select(s => s.Heading));
        Assert.Equal([1, 2], sections[0].Lines);
        Assert.Equal([3, 5], sections[1].Lines);
        Assert.Equal([6, 7], sections[2].Lines);
    }

    [Fact]
    public void Headings_inside_fences_are_ignored()
    {
        var sections = Sections.Parse("## Real\n```sh\n## not a heading\n```\n## Also real\n");
        Assert.Equal(["Real", "Also real"], sections.Select(s => s.Heading));
    }

    [Fact]
    public void A_level_three_heading_is_not_a_boundary_at_level_two() =>
        Assert.Single(Sections.Parse("## Two\n### Three\n"));

    [Fact]
    public void The_split_level_falls_back_to_three_when_two_does_not_divide()
    {
        var (level, sections) = Sections.Choose("## Title\nintro\n\n### One\na\n\n### Two\nb\n");
        Assert.Equal(3, level);
        Assert.Equal([null, "One", "Two"], sections.Select(s => s.Heading));
    }

    [Fact]
    public void The_split_level_prefers_two_when_it_divides()
    {
        var (level, sections) = Sections.Choose("## One\na\n\n## Two\n### Nested\nb\n");
        Assert.Equal(2, level);
        Assert.Equal(["One", "Two"], sections.Select(s => s.Heading));
    }

    [Fact]
    public void Markdown_without_headings_is_one_section_and_an_empty_file_has_none()
    {
        Assert.Null(Assert.Single(Sections.Parse("just prose\nmore prose\n")).Heading);
        Assert.Empty(Sections.Parse(string.Empty));
    }

    [Fact]
    public void Section_bytes_are_utf8_bytes()
    {
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("## é\nx\n"), Assert.Single(Sections.Parse("## é\nx\n")).Bytes);
    }
}
