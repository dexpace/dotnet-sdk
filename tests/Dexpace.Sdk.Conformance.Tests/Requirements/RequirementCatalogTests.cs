// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Catalogue;

/// <summary>P8a-13: the ID-to-level table is generated from appendix C and cannot drift from it.</summary>
[Trait("Category", "Unit")]
public sealed partial class RequirementCatalogTests
{
    [GeneratedRegex(@"^\| ([A-Z]+-\d+) \| (MUST NOT|MUST|SHOULD|MAY) \|", RegexOptions.CultureInvariant)]
    private static partial Regex RowPattern();

    [GeneratedRegex(@"lists (\d+) requirements", RegexOptions.CultureInvariant)]
    private static partial Regex CountPattern();

    private static string AppendixC => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "spec", "appendix-c.md"));

    private static List<(string Id, RequirementLevel Level)> ParseAppendix() =>
        [.. AppendixC.Split('\n')
            .Select(line => RowPattern().Match(line))
            .Where(match => match.Success)
            .Select(match => (match.Groups[1].Value, match.Groups[2].Value switch
            {
                "SHOULD" => RequirementLevel.Should,
                "MAY" => RequirementLevel.May,
                _ => RequirementLevel.Must,
            }))];

    [Fact]
    public void The_generated_catalogue_matches_appendix_C_row_for_row()
    {
        var parsed = ParseAppendix();

        Assert.Equal(parsed, RequirementCatalog.Rows);
    }

    [Fact]
    public void The_catalogue_holds_the_number_of_requirements_appendix_C_says_it_lists()
    {
        var said = int.Parse(CountPattern().Match(AppendixC).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(645, said);
        Assert.Equal(said, RequirementCatalog.Rows.Count);
        Assert.Equal(said, RequirementIndex.All.Count);
        Assert.Equal(said, RequirementIndex.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_drift_check_can_fail()
    {
        // Negative control: dropping a row or changing a level from the parsed appendix must be visible to the comparison.
        var parsed = ParseAppendix();
        var dropped = parsed.Skip(1).ToList();
        var changed = parsed.Select(row => row.Id == "TRANSPORT-6" ? (row.Id, RequirementLevel.Must) : row).ToList();

        Assert.NotEqual(dropped, RequirementCatalog.Rows);
        Assert.NotEqual(changed, RequirementCatalog.Rows);
    }

    [Theory]
    [InlineData("TRANSPORT-24", RequirementLevel.Must)]
    [InlineData("TRANSPORT-6", RequirementLevel.Should)]
    [InlineData("SEAM-15", RequirementLevel.May)]
    public void Lookup_returns_the_level_of_a_catalogued_requirement(string id, RequirementLevel expected)
    {
        Assert.True(RequirementIndex.TryGetLevel(id, out var level));
        Assert.Equal(expected, level);
        Assert.True(RequirementIndex.Contains(id));
    }

    [Theory]
    [InlineData("NOPE-1")]
    [InlineData("transport-24")]
    [InlineData("TRANSPORT-99")]
    [InlineData("")]
    public void Lookup_is_ordinal_case_sensitive_and_rejects_unknown_ids(string id)
    {
        Assert.False(RequirementIndex.TryGetLevel(id, out _));
        Assert.False(RequirementIndex.Contains(id));
        Assert.Throws<ArgumentException>(() => RequirementIndex.Require(id));
    }

    [Fact]
    public void A_must_not_row_is_folded_into_must()
    {
        var mustNot = AppendixC.Split('\n').Select(line => RowPattern().Match(line)).Single(match => match.Success && match.Groups[2].Value == "MUST NOT");

        Assert.True(RequirementIndex.TryGetLevel(mustNot.Groups[1].Value, out var level));
        Assert.Equal(RequirementLevel.Must, level);
    }
}
