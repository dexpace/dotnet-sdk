// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Docs;

/// <summary>
/// <c>docs/sdk-documentation/conformance.md</c> cannot drift from the code it describes (plan 3.3): its catalogue table is the
/// catalogue, its quoted preamble is <see cref="ConformanceReport.Preamble"/>, its driver sample is the compiled driver, and it
/// names every capability hook.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConformancePageTests
{
    private static string[] PageLines => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "spec", "conformance.md"));

    private static string[] Section(string heading)
    {
        var lines = PageLines;
        var start = Array.FindIndex(lines, line => line == $"## {heading}");
        Assert.True(start >= 0, $"the page has no '## {heading}' section");
        var end = Array.FindIndex(lines, start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        return lines[(start + 1)..(end < 0 ? lines.Length : end)];
    }

    [Fact]
    public void The_catalogue_table_is_the_catalogue_name_for_name_with_its_requirements_level_and_faces()
    {
        var rows = Section("The assertion catalogue")
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
            .ToArray();

        Assert.Equal(TransportSuite.Assertions.Count, rows.Length);
        for (var i = 0; i < rows.Length; i++)
        {
            var assertion = TransportSuite.Assertions[i];
            Assert.Equal($"`{assertion.Name}`", rows[i][0]);
            Assert.Equal(string.Join(", ", assertion.RequirementIds), rows[i][1]);
            Assert.Equal(assertion.Level.ToString().ToUpperInvariant(), rows[i][2]);
            Assert.Equal(assertion.Faces.Count == 2 ? "both" : "async", rows[i][3]);
        }
    }

    [Fact]
    public void The_quoted_preamble_is_the_reports_preamble()
    {
        var quoted = string.Join(' ', Section("What the kit is").Where(line => line.StartsWith("> ", StringComparison.Ordinal)).Select(line => line[2..]));

        Assert.Equal(ConformanceReport.Preamble, quoted);
    }

    [Fact]
    public void The_driver_sample_is_the_compiled_driver_verbatim()
    {
        var source = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "spec", "RawSocketConformanceTests.cs.txt"));
        var start = Array.FindIndex(source, line => line.Trim() == "// <driver>");
        var end = Array.FindIndex(source, line => line.Trim() == "// </driver>");
        Assert.True(start >= 0 && end > start, "the driver markers were not found in the compiled driver");
        var compiled = string.Join('\n', source[(start + 1)..end].Select(line => line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line)).Trim('\n');

        var page = PageLines;
        var marker = Array.FindIndex(page, line => line == "<!-- driver -->");
        Assert.True(marker >= 0 && page[marker + 1] == "```csharp", "the page has no marked driver sample");
        var close = Array.FindIndex(page, marker + 2, line => line == "```");
        var documented = string.Join('\n', page[(marker + 2)..close]);

        Assert.Equal(compiled, documented);
    }

    [Fact]
    public void Every_capability_hook_and_every_status_is_named_on_the_page()
    {
        var text = string.Join('\n', PageLines);
        var hooks = typeof(TransportSubject).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Where(name => name.StartsWith("CreateWith", StringComparison.Ordinal) || name == "CreateBorrowed");

        Assert.All(hooks, hook => Assert.Contains($"`{hook}`", text, StringComparison.Ordinal));
        Assert.All(Enum.GetNames<ConformanceStatus>(), status => Assert.Contains($"`{status}`", text, StringComparison.Ordinal));
    }

    [Fact]
    public void The_page_can_be_checked_it_fails_when_a_name_is_missing()
    {
        // Negative control: dropping one catalogue name from the page's text must be visible to the comparison.
        var names = Section("The assertion catalogue").Where(line => line.StartsWith("| `", StringComparison.Ordinal)).Select(line => line.Split('|')[1].Trim()).ToList();

        names.RemoveAt(0);

        Assert.NotEqual(TransportSuite.Assertions.Select(a => $"`{a.Name}`"), names);
    }
}
